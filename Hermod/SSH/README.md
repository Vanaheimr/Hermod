# SSH

A from-scratch SSH2 stack — transport (RFC 4253), authentication (RFC 4252), the connection
protocol (RFC 4254) and SFTP — implemented **once for both roles**: the same wire format, the
same key exchange and the same channel multiplexer serve the client and the server. Post-quantum
hybrid key exchange, OpenSSH certificates, port forwarding and an SFTP subsystem are part of it,
not bolted on beside it.

Dependencies are the BCL plus BouncyCastle, and BouncyCastle only for what .NET does not expose:
X25519, Ed25519, sntrup761 and Poly1305. ML-KEM comes from the BCL. Everything that is a
*construction* rather than a primitive — AES-CTR, `chacha20-poly1305@openssh.com`, `bcrypt_pbkdf`,
the KDF, the exchange hash — is written here against official test vectors, because that is where
the interesting mistakes live. The ChaCha20 core is ours too, and vectorised
(`Vector128<UInt32>`: NEON on ARM, SSE/AVX on x86, one implementation).

> **Verified against ten independent implementations**, in both directions: OpenSSH and Dropbear
> drive our server *and* our client drives theirs, with TinySSH, PuTTY, AsyncSSH, Paramiko,
> Go `x/crypto/ssh`, SSH.NET, curl/libssh2 and `ncclient` (NETCONF) alongside, gated on every
> commit and repeated nightly (including OpenSSH 8.9/9.6/9.9 backward spread and newest upstream
> OpenSSH). The harness lives in a sibling repository,
> [SSHConformanceTests](https://github.com/Vanaheimr/SSHConformanceTests) — see [Interop](#interop).

---

## Layout

| Folder | Contents |
|---|---|
| `Core/` | Wire format: `SshPacketReader`/`SshPacketWriter`, message numbers, disconnect reasons |
| `Crypto/` | X25519, Ed25519, ChaCha20, the ML-KEM/sntrup761 hybrids, Diffie-Hellman, `bcrypt_pbkdf` |
| `Transport/` | Version exchange, KEXINIT negotiation, the KEX core, ciphers/MACs, framing, rekeying, ext-info |
| `Auth/` | The authentication pipeline: public key, password, keyboard-interactive, TOTP, access profiles, the audit event catalogue, the ssh-agent client |
| `Keys/` | Key formats, `authorized_keys`, `known_hosts`, host-key policies, SSHFP, OpenSSH certificates + a mini-CA |
| `Connection/` | Channels and the multiplexer, exec/shell/pty, keepalive, flow control, `ProxyJump` |
| `SFTP/` | SFTP v3 client and server, file-system abstractions, `SftpFileStream`, OpenSSH extensions |
| `Forwarding/` | `NetworkAcl` and the `ForwardingPolicy` presets for `direct-tcpip` and `tcpip-forward` |
| `Recording/` | Session recording: asciicast v2 and an SFTP transcript |
| `Messaging/` | Whole messages over a subsystem channel: `SshMessageServer`, `SshMessageClient`, the frames - see its README |
| `OverWebSocket/` | SSH over WebSocket: `WebSocketSshBridge`, `SshWebSocketEndpoint`, `SshWebSocketClient` |
| `Client/` | The high-level `SSHClient` façade |
| `Server/` | The high-level `SshServer` façade |

`Client/` and `Server/` both depend on the folders above them and **never on each other**. That
was an assembly boundary once; it is a source convention now, and the reason a feature lands in
exactly one place instead of twice.

## Using it

### A client

```csharp
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;

var key    = SshKeyGenerator.LoadPrivateKey(await File.ReadAllTextAsync("id_ed25519")).Key;
var policy = HostKeyPolicy.Pin("SHA256:5Q3…").OrKnownHostsFile("known_hosts");

await using var client = await SSHClient.Connect("host.example.org", IPPort.Parse(22), new SSHClientOptions {
    Username       = User_Id.Parse("achim"),
    Credentials    = [ key ],
    VerifyHostKey  = blob => policy.Verify("host.example.org", IPPort.Parse(22), blob),
    DNSClient      = dnsClient
});

var result = await client.Execute("uname -a");
Console.WriteLine($"[{result.ExitCode}] {result.StandardOutput}");
```

A host name is resolved with `DNSClient` - the server's, the first jump host's, the local target of
a remote forward - and **never with the resolver of the operating system**: an application that
resolves its names securely, with name servers of its own, DNS over TLS or HTTPS and DNSSEC, must not
be connected around them. An IP address, or `localhost`, needs no DNS client; a host name without one is
refused with an `SshNameResolutionException`. On the server, a forwarding target is resolved with
`SshServerOptions.DNSClient` where there is no `AddressResolver`, and is otherwise reached by IP
address only. `SshTcp.ResolveAsync` and `SshTcp.ResolverOf` are where all of them resolve, through
`NameResolution.ResolveAsync`, the one place all of Hermod does (see the DNS README).

One connection carries everything at once — exec, SFTP and tunnels multiplex over it:

```csharp
await using var sftp   = await client.OpenSFTPClient();
await using var tunnel = await client.OpenTCPStream("10.0.0.5", IPPort.Parse(5432));   // -L, as a Stream

await sftp.UploadAsync("/firmware.bin", File.ReadAllBytes("firmware.bin"));
foreach (var entry in await sftp.ListDirectoryAsync("/"))
    Console.WriteLine($"{entry.Name}  {entry.Attributes.Size}");
```

`VerifyHostKey` takes the raw key blob, so anything can decide it — but the ready-made chain is
`HostKeyPolicy`, and it is a chain on purpose: each source answers *accept*, *reject* or *no
opinion*, and the next one is asked only for the last.

```csharp
var policy = HostKeyPolicy.Pin("SHA256:5Q3…")                   // this key, and no other
                          .OrHostCertificate(hostCaTrust)       // …or one signed by a trusted host CA
                          .OrKnownHostsFile("known_hosts")      // …or a remembered one
                          .OrInteractiveTofu(prompt => Ask(prompt.Sha256Fingerprint));
```

A `reject` — a `known_hosts` mismatch, a revoked certificate — ends the chain there. Falling off
the end without an accept is a refusal too: no source is ever a silent yes.

DNS-published fingerprints (RFC 4255) are verified separately, because the lookup is asynchronous
and the trust question is a different one: an SSHFP record is only evidence if the answer was
DNSSEC-validated, which is why `RequireDnssec` is the default rather than an option.

```csharp
var sshfp   = new SshfpVerifier(new HermodSshfpResolver(dnsClient), SshfpTrust.RequireDnssec);
var verdict = await sshfp.VerifyAsync("host.example.org", hostKeyBlob);
// SecureMatch (auto-accept) · InsecureMatch (advisory only) · Mismatch (reject) · NoRecords (no opinion)
```

### A server

```csharp
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

await using var server = new SshServer(new SshServerOptions {
    HostKeys          = [ hostKey ],
    Authenticator     = SshUserAuthenticator.ForAuthorizedKeys(
                            AuthorizedKeysFile.Parse(await File.ReadAllTextAsync("authorized_keys"))),
    ExecHandler       = async (context, ct) => {
                            await context.WriteLineAsync($"you asked for: {context.Command}", ct);
                            return 0;                                   // the exit status
                        },
    SftpFileSystem    = new LocalSftpFileSystem("./served"),
    SftpProfile       = SshAccessProfile.SftpUploadOnly,                // least privilege by default
    ForwardingPolicy  = ForwardingPolicy.LoopbackOnly,
    AuditSink         = auditSink
});

await server.StartAsync(new IPSocket(IPv4Address.Any, IPPort.Parse("2222")));
```

Every capability is off until it is configured: no `SftpFileSystem` means no SFTP subsystem, and
`ForwardingPolicy` defaults to `None`. A server that was never told to forward cannot be talked
into it.

### An interactive shell

An exec handler is given a command and answers it. A `ShellHandler` is a conversation: it gets the
terminal the client asked for (`pty-req`: its name, size and modes), the environment, who signed
in with which key and from where, and the keys as they are typed - and the channel's requests go
on being read while it runs, so a resized window and a signal reach it at once.

```csharp
await using var server = new SshServer(new SshServerOptions {
    HostKeys      = [ hostKey ],
    Authenticator = authenticator,
    ShellHandler  = async (context, ct) => {
                        context.WindowChanged += size => Redraw(size.Columns);
                        await context.WriteAsync($"Hello {context.Session.Username} " +
                                                 $"({context.Session.PublicKeyFingerprint})\r\n", ct);
                        // ... read context.Input until the user leaves ...
                        return 0;                                       // the exit status
                    },
    Limits        = new SshServerLimits { LoginGraceTime = TimeSpan.FromSeconds(30) }
});
```

There is no terminal driver in between, so the handler is its own line discipline: Enter arrives as
CR, Backspace as DEL, Ctrl+C as 3, and `"\r\n"` ends a line on the screen. With a shell handler and
nothing else the server serves the shell and nothing else - exec, SFTP and tunnels are refused - and
a key confined to one command (`command="..."`) is never given the shell. `SSHClient.OpenShell`
is the client half, for a program or a test that wants to be the person at the terminal.

Stopping the server is orderly: no connection is accepted any more, every shell is told through its
token while its channel is still open - `context.Stopping` says that this is why - and is given
`ShutdownGracePeriod` to say goodbye before its connection ends with a DISCONNECT that says why.

`LocalSftpFileSystem` is root-jailed — paths are resolved and then checked against the root, so
`../` and a symlink pointing out both fail. `SshAccessProfile` narrows it further to
upload-only or download-only, and `SftpLimits` adds quotas and a bandwidth ceiling.

### Port forwarding, and what may be reached

A forwarding policy is a `NetworkAcl`, and it is evaluated against the **resolved** address, not
the requested name — otherwise a hostname that resolves to `127.0.0.1` walks straight through a
loopback-only rule (DNS rebinding):

```csharp
ForwardingPolicy.Custom(NetworkAcl.DenyByDefault()
                                  .Allow(Cidr: "10.0.0.0/24", Ports: "5432,6379"))
```

`ProxyJump` chains connections the way `ssh -J` does: the tunnel opened on the first hop becomes
the transport of the second.

### Subsystems of your own, and messages over them

`SshServerOptions.Subsystems` serves subsystems besides SFTP, session by session. The factory is
told who signed in and which name they asked for, and answers a handler or null. The handler gets
the session and the channel, and the server closes the channel once it returns.
`SSHClient.OpenSubsystem(name)` is the client's side.

A credential can be confined to some subsystems with `SshSessionRestrictions.PermittedSubsystems`:
it then gets only those, and no shell, no exec and no forced command.

A subsystem can be a console for people as well as a protocol for programs: `ssh -t -s station
admin@...`. The terminal the client asked for before it is `SshSubsystemContext.Terminal`, with
its `Pty` and current `Size`, and a resize, a signal or a break reaches the handler as an event while
it runs, just as for a shell. A request the subsystem cannot serve, such as a second terminal or an
exec on its channel, is refused at once. `SshSessionInfo.Certificate` is the OpenSSH certificate that
signed in, so a factory can grant such a console by what the CA wrote into it: an extension of its
own, say, with the key id naming the operator. `SSHClient.OpenSubsystem(name, pty)` asks for the
terminal first.

`Messaging/` puts whole messages, text or binary, over such a subsystem: HELLO and WELCOME
negotiate a subprotocol, and frames are length-checked before anything is allocated for them. It is
what a WebSocket does over HTTP. `SshMessageServer` and `SshMessageClient` are the two ends, and an
`SshMessageConnection` is an `IMessageConnection` just as a WebSocket connection is. The protocol is
written down in [`Messaging/README.md`](Messaging/README.md).

### Over a WebSocket

Where nothing but HTTPS gets out - a charging station behind a firewall or a carrier-grade NAT -
the byte stream of SSH goes in the binary messages of a WebSocket of its own, with the subprotocol
`ssh.open.charging.cloud`. SSH is not changed by it: key exchange, host key and authentication are
SSH's, end to end, and TLS is the envelope for whatever is in between. One protocol a connection:
OCPP keeps its WebSocket, and a large SFTP transfer holds none of its messages up.

Whoever opens the WebSocket need not be the SSH client - all four ways round:

| | Opens the WebSocket | Accepts it |
|---|---|---|
| SSH client | `SSHClient.Connect(SshWebSocketClient, …)` | `SSHClient.ConnectOver(connection.Pipe, …)` |
| SSH server | `SshServer.ConnectOut(SshWebSocketClient)` | `SshServer.ServeAsync(connection.Pipe, …)` |

`SshWebSocketEndpoint` goes on an HTTP path with `WebSocketUpgrade.For()` and hands every connection
to its `OnConnection`, with who the HTTP authentication of the upgrade said it is. A station that
may only connect out is the SSH server (`ConnectOut`), and its back end the SSH client
(`ConnectOver`):

```csharp
var endpoint = new SshWebSocketEndpoint(async (connection, ct) => {
                   // connection.Login: who the HTTP authentication said it is
                   await using var station = await SSHClient.ConnectOver(connection.Pipe, options, ct);
                   var result = await station.Execute("uptime", ct);
               });

httpAPI.AddHandler(HTTPMethod.GET, HTTPPath.Parse("/ssh"), HTTPDelegate: WebSocketUpgrade.For(endpoint));
```

Who may reach SSH at all is said by the HTTP authentication of the upgrade, as at every WebSocket
endpoint: HTTP Basic, one-time passwords - bound to the TLS connection or not -, client
certificates. A client that does not offer `ssh.open.charging.cloud` is answered 400, a text message
closes the connection with 1003, and nothing is compressed. Where the messages are cut has no
meaning; one carries at most 32 KiB, and one received may be 1 MiB at most. Both ways are bounded:
a received message is given to SSH before the next is read - 1 MiB may lie unread -, and a send is
waited for before the next.

## Algorithms

| | Offered, in preference order |
|---|---|
| **Key exchange** | `mlkem768x25519-sha256`, `sntrup761x25519-sha512`(`@openssh.com`), `curve25519-sha256`(`@libssh.org`), `ecdh-sha2-nistp256/384/521`, `diffie-hellman-group14-sha256`, `diffie-hellman-group16-sha512` |
| **Host keys** | `ssh-ed25519`, `ecdsa-sha2-nistp256/384/521`, `rsa-sha2-512/256`, plus the `-cert-v01@openssh.com` certificate variants of each |
| **Ciphers** | `chacha20-poly1305@openssh.com`, `aes256-gcm@openssh.com`, `aes128-gcm@openssh.com`, `aes256/192/128-ctr` |
| **MACs** | `hmac-sha2-256/512-etm@openssh.com`, then the plain variants (ignored under an AEAD cipher) |
| **Compression** | `none` |

ChaCha20 is first on purpose: the deployment target is an ARM device fleet, where AES-GCM has no
AES-NI advantage to fall back on. On x86 *with* AES-NI it costs something — a 32 KiB record runs
at 878 MB/s on `aes256-gcm`, 191 MB/s on `aes256-ctr`+EtM and 132 MB/s on ChaCha20, and the full
SFTP stack reaches 87–111 MB/s. The numbers and the optimisation rounds behind them are in
[docs/BENCHMARKS.md](https://github.com/Vanaheimr/SSHConformanceTests/blob/master/docs/BENCHMARKS.md).

`ssh-rsa` (SHA-1 signatures) is understood as a *key type* but not offered as a signature algorithm.

Two markers ride along in the key-exchange name-list and are never selectable as one:
`kex-strict-…@openssh.com` (Terrapin, CVE-2023-48795 — strict KEX is negotiated whenever the peer
offers it, and then a sequence-number reset and a hard ban on IGNORE/DEBUG during a handshake
follow) and `ext-info-c`/`-s` for RFC 8308.

## RFC compliance

| RFC / draft | Title | Status |
|---|---|---|
| **4251 / 4250** | Architecture, assigned numbers | ✅ |
| **4253** | Transport layer protocol | ✅ Version exchange, KEXINIT, rekeying, §7.1 guessed first packet (discarded when wrong), §11 IGNORE/DEBUG/UNIMPLEMENTED |
| **4252 / 4256** | Authentication, keyboard-interactive | ✅ publickey (query-then-sign), password, keyboard-interactive, banner, method chaining, partial success |
| **4254** | Connection protocol | ✅ Sessions, exec/shell/pty, window adjust, `direct-tcpip`, `tcpip-forward` |
| **4255** | SSHFP DNS records | ✅ Both roles, via Hermod's DNSSEC-validating resolver |
| **4716** | Public key file format | ✅ Plus `openssh-key-v1` (incl. `bcrypt_pbkdf` encryption) and PKCS#8 |
| **5656 / 8731 / 8268** | ECDH, curve25519-sha256, DH-SHA2 | ✅ |
| **7748 / 8032 / 8709** | X25519, Ed25519, `ssh-ed25519` | ✅ RFC 7748 and RFC 8032 vectors asserted |
| **8332** | `rsa-sha2-256` / `rsa-sha2-512` | ✅ |
| **4344 / 5647 / 8439** | AES-CTR, AES-GCM, ChaCha20-Poly1305 | ✅ CTR against NIST SP 800-38A, AEAD against RFC 8439 §2.3.2/§2.4.2 |
| **6668** | SHA-2 based HMAC | ✅ incl. the EtM variants |
| **8308** | Extension negotiation (`ext-info`, `server-sig-algs`) | ✅ |
| **9142** | Key exchange method updates | ✅ Recommended set offered, SHA-1 methods absent |
| **4226 / 6238** | HOTP / TOTP | ✅ Second factor in the authentication chain |
| Hermod `TOTPGenerator` (TOTP.ts format) | Hermod's own one-time password over keyboard-interactive, unbound (`HermodTotpKeyboardInteractive`) | ✅ Server factor and client answer; an unmodified OpenSSH takes part through [SSHTOTPTool](https://github.com/Vanaheimr/SSHTOTPTool) (`pam_exec`, `SSH_ASKPASS`). Bound to the session: `totp@open.charging.cloud` |
| **9941** | `sntrup761x25519-sha512` | ✅ Bare IANA name and the `@openssh.com` name |
| **draft-ietf-sshm-mlkem-hybrid-kex** | `mlkem768x25519-sha256` | ✅ Draft-10, in the RFC Editor queue; names and encoding stable |
| **draft-ietf-secsh-filexfer-02** | SFTP version 3 | ✅ Client + server |
| **RFC 9987 / draft-miller-ssh-agent** | SSH agent protocol | 🔶 Client only — list identities, request signatures; no agent *forwarding* |
| OpenSSH `PROTOCOL` | Certificates, `hostkeys-00@openssh.com`, and the SFTP extensions `posix-rename`, `fsync`, `statvfs`/`fstatvfs`, `lsetstat`, `limits@openssh.com`, `copy-data` | ✅ Server answers all seven; the client drives all but `fstatvfs`. `hardlink` and `expand-path` are not implemented |
| `kexguess2@matt.ucc.asn.au` | Dropbear's narrowed guess rule | ✅ Both roles |

RFC 4253 §7.1 is worth a note, because it is the one place where "obviously correct" was wrong for
a while. A peer may guess the first key-exchange packet and send it early; if the guess was wrong,
that packet must be **read and thrown away**, or every subsequent message is off by one. Dropbear
guesses by default, which is how the omission was found — and `kexguess2` is its answer to the
fact that the RFC also requires the *host-key* algorithm to match, which a client can rarely
predict.

## Interop

Interoperability lives in [SSHConformanceTests](https://github.com/Vanaheimr/SSHConformanceTests),
a separate repository, because these tests need software the machine has to provide — WSL, an
`ssh` binary, a Python environment, a Go toolchain — and the suite in this repository must stay
runnable anywhere.

| Peer | Version | Direction |
|---|---|---|
| OpenSSH | 10.2p1 / 10.0p2 | **both** — their client against our server, and our client (incl. SFTP) against their `sshd` |
| Dropbear | 2025.89 | **both** |
| TinySSH | 20250601 | our client → their server |
| PuTTY (`plink`) | 0.83 | their client → our server |
| AsyncSSH | 2.24.0 | their client → our server |
| Paramiko | 5.0.0 | their client → our server |
| Go `x/crypto/ssh` | v0.54.0 | their client → our server |
| SSH.NET | 2026.0.0 | their client → our server (in-process) |
| curl / libssh2 | 8.14.1 / 1.11.1 | their client → our server (SFTP) |
| `ncclient` | NETCONF 1.0 / 1.1 | their client → our server (`netconf` subsystem) |

A peer that is absent is **skipped with a precise reason**, never counted as passing: the matrix
distinguishes "disagreed" from "no evidence either way".

## Test

Over 680 hermetic tests live under [`HermodTests/SSH`](../../HermodTests/SSH), mirroring this
folder layout. They need nothing but the code — unit tests and loopback round-trips between our
own client and our own server.

```powershell
dotnet test HermodTests/HermodTests.csproj --filter "FullyQualifiedName~Hermod.SSH.Tests"
```

Three things about them are worth knowing:

- **Official vectors are tests.** RFC 7748 (X25519), RFC 8032 (Ed25519), RFC 8439 (ChaCha20-Poly1305),
  NIST SP 800-38A (CTR), NIST KATs (ML-KEM) and OpenSSH's own regress vectors
  (`chacha20-poly1305@openssh.com`, `bcrypt_pbkdf`) are asserted byte-exactly. Interop rests on
  these before it rests on any live peer.
- **Peer quirks become tests, not folklore.** Every behaviour learned from a foreign implementation
  — the guessed KEX packet, `SSH_MSG_IGNORE` arriving mid-authentication, a `winadj@putty.projects.tartarus.org`
  request that must be answered with `CHANNEL_FAILURE` and never with success — is pinned here,
  where it runs on every commit, rather than only in the interop suite that needs the peer present.
- **The security tests are adversarial.** `HermodTests/SSH/Security` asserts what must *not* happen,
  and most of it is about failing closed: an `authorized_keys` option we cannot enforce invalidates
  the whole line rather than being ignored, a certificate carrying a critical option we do not
  understand is refused, a resolver that rebinds a permitted name to a forbidden address never
  reaches it, a client without a host-key verifier does not connect at all. Plus a parser fuzz
  harness that must survive arbitrary input with a clean rejection.

## Hardening & observability

- **A typed audit event stream.** `ISshAuditSink` receives an `SshAuditEvent` per thing that
  happened — `KexCompletedEvent` (with the negotiated algorithms, `PostQuantum` and `StrictKex`),
  `AuthAttemptEvent`, `HostKeyRejectedEvent` with the real reason, `PolicyDeniedEvent` with what
  was denied and why, `SftpOperationEvent`, `LimitExceededEvent`. One record per event, not a
  format string — meant for a SIEM rather than a log file.
- **A generic `DISCONNECT` on the wire while the detail goes to the audit sink**: an attacker
  learns "protocol error", the operator learns which one.
- **Limits that are enforced, not only configured.** `SshServerLimits` bounds the attempts per
  connection, the time to authenticate in, the sessions per connection, the connections at once, and
  those not yet authenticated - in all and per address, so that one address cannot take every slot -
  and probes clients that have gone quiet. Every event of a connection carries its id and its peer,
  and a client that leaves is recorded as having left: only one that speaks nonsense, or fails to
  authenticate, is a protocol error.
- **Constant-time comparison** for everything that authenticates — AEAD tags, TOTP codes,
  `known_hosts` and SSHFP fingerprints.
- **Keystroke-timing obfuscation** for interactive sessions (OpenSSH's countermeasure to inferring
  typed content from packet timing).
- **`TimeProvider` everywhere** — no `DateTime.Now`, so timeouts, certificate validity windows and
  rate limits are all testable without sleeping.

## Not here yet

- **No compression.** `zlib@openssh.com` is not implemented; `none` is what we offer.
- **SSH over WebSocket through a forward proxy.** `SshWebSocketClient` connects to the endpoint
  itself; an HTTP `CONNECT` through a proxy, with its `Proxy-Authorization`, is not there yet.
- **SFTP v3 only** — versions 4–6 are not negotiated. Of the OpenSSH extensions, `hardlink@openssh.com`
  is deliberately omitted because hard links would quietly defeat the per-session byte quota — that is a
  decision, not a gap. `home-directory` and `users-groups-by-id@openssh.com` are deliberately not
  offered: a root-jailed session has no business learning host paths or resolving UIDs to names.
  Symbolic links (`SYMLINK`, `READLINK`, `LSTAT`, `lsetstat@openssh.com`) are supported and confined to
  the root jail.
- **No agent forwarding, no X11 forwarding.** The `authorized_keys` parser understands and enforces
  the options that restrict them, which is the part that matters for a server that does not offer
  them either way.
- **Certificates** are verified bidirectionally against OpenSSH, AsyncSSH, and Go `x/crypto/ssh`
  (both user and host certificates, issued by our mini-CA and by the peers).

## License

Apache License, Version 2.0 — see [LICENSE](../../LICENSE)
