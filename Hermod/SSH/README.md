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

> **Verified against nine independent implementations**, in both directions: OpenSSH and Dropbear
> drive our server *and* our client drives theirs, with TinySSH, PuTTY, AsyncSSH, Paramiko,
> Go `x/crypto/ssh`, SSH.NET and curl/libssh2 alongside. **93 interop checks, none failing.**
> The harness that runs them is a sibling repository,
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
| `Client/` | The high-level `SshClient` façade |
| `Server/` | The high-level `SshServer` façade |

`Client/` and `Server/` both depend on the folders above them and **never on each other**. That
was an assembly boundary once; it is a source convention now, and the reason a feature lands in
exactly one place instead of twice.

## Using it

### A client

```csharp
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;

var key    = SshKeyGenerator.LoadPrivateKey(await File.ReadAllTextAsync("id_ed25519")).Key;
var policy = HostKeyPolicy.Pin("SHA256:5Q3…").OrKnownHostsFile("known_hosts");

await using var client = await SshClient.ConnectAsync("host.example.org", 22, new SshClientOptions {
    Username       = "achim",
    Credentials    = [ key ],
    VerifyHostKey  = blob => policy.Verify("host.example.org", IPPort.Parse("22"), blob)
});

var result = await client.ExecuteAsync("uname -a");
Console.WriteLine($"[{result.ExitCode}] {result.StandardOutput}");
```

One connection carries everything at once — exec, SFTP and tunnels multiplex over it:

```csharp
await using var sftp   = await client.OpenSftpClientAsync();
await using var tunnel = await client.OpenTcpStreamAsync("10.0.0.5", 5432);   // -L, as a Stream

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
| **9941** | `sntrup761x25519-sha512` | ✅ Bare IANA name and the `@openssh.com` name |
| **draft-ietf-sshm-mlkem-hybrid-kex** | `mlkem768x25519-sha256` | ✅ Draft-10, in the RFC Editor queue; names and encoding stable |
| **draft-ietf-secsh-filexfer-02** | SFTP version 3 | ✅ Client + server |
| **RFC 9987 / draft-miller-ssh-agent** | SSH agent protocol | 🔶 Client only — list identities, request signatures; no agent *forwarding* |
| OpenSSH `PROTOCOL` | Certificates, `hostkeys-00@openssh.com`, and the SFTP extensions `posix-rename`, `fsync`, `statvfs`/`fstatvfs`, `limits@openssh.com`, `copy-data` | ✅ Server answers all six; the client drives all but `fstatvfs`. `hardlink`, `lsetstat`, `expand-path` are not implemented |
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

A peer that is absent is **skipped with a precise reason**, never counted as passing: the matrix
distinguishes "disagreed" from "no evidence either way".

## Test

321 hermetic tests live under [`HermodTests/SSH`](../../HermodTests/SSH), mirroring this
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
- **Constant-time comparison** for everything that authenticates — AEAD tags, TOTP codes,
  `known_hosts` and SSHFP fingerprints.
- **Keystroke-timing obfuscation** for interactive sessions (OpenSSH's countermeasure to inferring
  typed content from packet timing).
- **`TimeProvider` everywhere** — no `DateTime.Now`, so timeouts, certificate validity windows and
  rate limits are all testable without sleeping.

## Not here yet

- **No compression.** `zlib@openssh.com` is not implemented; `none` is what we offer.
- **SFTP v3 only** — versions 4–6 are not negotiated. Of the OpenSSH extensions, `hardlink@openssh.com`
  and `lsetstat@openssh.com` are missing because `ISftpFileSystem` has no concept of links at all (not
  even symlinks), and hard links would quietly defeat the per-session byte quota — that is a decision,
  not a gap. `home-directory` and `users-groups-by-id@openssh.com` are deliberately not offered: a
  root-jailed session has no business learning host paths or resolving UIDs to names.
- **No agent forwarding, no X11 forwarding.** The `authorized_keys` parser understands and enforces
  the options that restrict them, which is the part that matters for a server that does not offer
  them either way.
- **Certificates are proven against OpenSSH only**, and only in the direction where we validate
  theirs. Our own mini-CA is exercised by our own validator and by OpenSSH, not by a third opinion.
- **The nightly interop matrix is not wired up yet** — everything runs on a developer machine
  today, pending a CI runner decision.

## License

Apache License, Version 2.0 — see [LICENSE](../../LICENSE)
