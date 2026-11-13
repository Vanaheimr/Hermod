# QUIC

A from-scratch QUIC stack — transport (RFC 9000), the TLS 1.3 handshake it carries
(RFC 9001 + RFC 8446), and loss detection / congestion control (RFC 9002) — built
directly on UDP datagrams. No `System.Net.Quic`, no msquic, no OpenSSL: every
packet, frame, key and timer is hand-rolled.

Because QUIC uses **no TLS record layer**, this is not a TLS stack bolted onto a
transport. The handshake messages travel in QUIC CRYPTO frames and QUIC does the
packet protection itself with keys TLS derives — so what lives here is a TLS 1.3
**handshake engine** (`TLS/`) wired into the transport, not `SslStream`.

Dependencies are the BCL plus BouncyCastle, and BouncyCastle only for the four
curves .NET does not expose: X25519, X448, Ed25519, Ed448.

> ⚠️ **Reference implementation.** The handshake, streams, flow control, loss
> recovery, key update, session resumption + 0-RTT, connection migration,
> connection-ID rotation, stateless reset, stateless Retry, mutual TLS, ECN,
> version negotiation and post-quantum crypto (ML-KEM hybrid key exchange,
> ML-DSA certificates) all work end-to-end and are verified against **eight
> independent foreign QUIC stacks** plus `curl --http3` in both directions.
> It is still built for learning the wire protocol, not for production traffic:
> the receive path is still a single loop.
> See [what is not here](#not-here-yet).

**HTTP/3 lives one folder over**, in [`../HTTP3`](../HTTP3/README.md): QPACK and the
H3 framing/mapping consume this transport and nothing here knows about them. The
line runs between transport and HTTP mapping. The interop matrices and the samples
that drive both over real sockets are in the sibling project
[HTTP/3 Conformance Tests](https://github.com/Vanaheimr/HTTP3ConformanceTests).

---

## Layout

| Folder | Contents |
|---|---|
| `Buffers/` | `BufferReader`/`BufferWriter`, varints, `ByteQueue` (zero-alloc hot path), GSO batching |
| `Packets/` | Long/short headers, packet numbers, connection IDs, Retry, version negotiation, stateless reset, Retry tokens |
| `Frames/` | All RFC 9000 §19 frame types + the parser/serialiser |
| `Crypto/` | Initial secrets, packet + header protection, ChaCha20, Retry integrity |
| `Connection/` | `QuicEndpoint` (shared transport core), `QuicClientConnection`, `QuicServerConnection`, packet number spaces, connection-ID management, idle timeout |
| `Streams/` | Stream state, send/receive buffers, flow control, receive-window auto-tuning |
| `Recovery/` | RTT estimation, loss detection, PTO, NewReno, pacing, path MTU discovery |
| `TLS/` | The TLS 1.3 handshake engine: `Messages/`, `Crypto/` (key schedule, key exchange), `Handshake/` (client + server, certificate validation) |
| `Qlog/` | qlog trace writer (JSON-SEQ, qvis-ready) |
| `Diagnostics/` | EventSource events + `System.Diagnostics.Metrics` counters for a running process |

The core is **deterministic and socket-free**: it consumes datagrams and produces
datagrams. Nothing in here opens a socket, sleeps, or awaits — which is what makes
the whole stack testable in-process with a seeded lossy link, and what lets the
layer above choose its own I/O model.

## Using it

Both roles are driven by the same three calls: hand in what arrived, ask for what
should go out, and tick the timers.

```csharp
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;            // ServerCertificate
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;  // CertificateValidationOptions

var validation = new CertificateValidationOptions { CustomTrustRoots = [caCertificate] };
using var client = new QuicClientConnection("example.org", certificateValidation: validation);
client.Start();

// Your own I/O loop owns the socket:
foreach (byte[] datagram in client.GetDatagramsToSend())
    socket.SendTo(datagram, serverEndpoint);

client.ProcessDatagram(received);      // whatever came back
client.CheckLossDetectionTimeout();    // drives PTO, retransmission, keep-alive, idle timeout

if (client.HandshakeConfirmed)
{
    QuicStream stream = client.OpenBidirectionalStream();
    stream.Write("hello"u8);
    stream.Finish();                   // FIN
    byte[] answer = stream.Read();     // next contiguous received section
}
```

The server side is symmetrical:

```csharp
using var certificate = ServerCertificate.CreateSelfSigned("localhost");
using var server = new QuicServerConnection(certificate);
server.ProcessDatagram(datagram);
foreach (byte[] outgoing in server.GetDatagramsToSend()) { /* … */ }
```

### Address validation without state (RFC 9000 §8.1.2)

A server under load must answer a connection attempt **before** allocating
anything, or a spoofed-source flood buys the attacker a connection object per
packet. `RetryTokenGenerator` makes the token carry the state:

```csharp
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

var tokens = new RetryTokenGenerator();          // AES-256-GCM under an HKDF-derived key

// No token: answer with a Retry and forget everything about it.
byte[] token = tokens.Issue(clientEndPoint, initial.DestinationConnectionId, retryScid);
socket.SendTo(RetryPacket.Build(version, initial.SourceConnectionId, retryScid,
                                token, initial.DestinationConnectionId), clientEndPoint);

// Token returned: validate, consume once, and only then build the connection.
if (tokens.TryValidate(initial.Token, clientEndPoint, out var odcid, out var scid) &&
    tokens.TryConsume(initial.Token))
    connection = new QuicServerConnection(certificate,
                     validatedRetry: new QuicServerConnection.ValidatedRetry(odcid, scid));
```

The token binds client address **and** port (§8.1.4), expires after 10 seconds,
and is single-use. `StatelessClose` answers an unusable one with `INVALID_TOKEN`
in an Initial packet — also without state, as §8.1.2 requires.

### Path MTU discovery (RFC 9000 §14.3)

RFC 9000 guarantees only 1200 bytes on every path, so without discovery every
datagram is sized for the worst case — on an ordinary 1500-byte path that leaves
about 18 % of each packet unused. DPLPMTUD sends deliberately oversized probes
(PING + PADDING) and raises the datagram size for each one that is acknowledged;
a lost probe says something about the path, not about congestion, so it never
reaches the congestion controller (§14.4).

It runs by default up to 1452 bytes and needs no configuration. The ceiling is
settable on both roles — upwards for a datacentre path known to carry jumbo
frames, or down to the floor to switch discovery off entirely:

```csharp
// LAN/datacentre: look for jumbo frames.
var client = new QuicClientConnection("example.org", maxDatagramSizeCeiling: 9000);

// Off: never send anything larger than the guaranteed floor.
var server = new QuicServerConnection(certificate,
                 maxDatagramSizeCeiling: QuicEndpoint.MaxDatagramSize);
```

Probing above the real path MTU is not harmful — those probes are simply lost and
the search backs off — but it costs probes and time, which is why the default
stays at the common Ethernet size instead of reaching for 9000 everywhere.

### Mutual TLS (RFC 8446 §4.3.2)

```csharp
var server = new QuicServerConnection(certificate, clientCertificate: new ClientCertificateOptions
{
    Mode       = ClientCertificateMode.Require,   // or Request: let anonymous clients in
    Validation = new CertificateValidationOptions { VerifyHostname = false,
                                                    CustomTrustRoots = [clientCA] },
});
// afterwards: server.ClientAuthentication.IsAuthenticated / .Certificate / .FailureReason
```

`Require` refuses a missing or untrusted certificate with `certificate_required` /
`bad_certificate`; `Request` completes the handshake and leaves the verdict to the
application. Clients pass their own credential as `clientCertificate:`.

### Key pins instead of a CA

A peer without a certificate any CA vouches for — a Local Controller on a site
network, a station with its own key — is authenticated by the SHA-256 over its
SubjectPublicKeyInfo (TLSA `3 1 1`, RFC 7469 `pin-sha256`): its key, and nothing
else. Chain, name and validity period are not checked, and a mismatch never falls
back to them. The CertificateVerify signature is verified as always.

```csharp
var client = new QuicClientConnection("192.168.10.5",
                 certificateValidation: CertificateValidationOptions.PinnedTo(
                     KeyPinPolicy.PinnedTo(KeyPin.Parse("0db1410b06336e5418c604b97d027a260baf35667e1f6c418029dbf7bfc3ef7e"))));

var server = new QuicServerConnection(certificate,
                 clientCertificate: ClientCertificateOptions.RequirePinned(
                     KeyPinPolicy.PinnedTo(stationPin)));
```

`ServerCertificate.KeyPin` gives a certificate's pin. The same `KeyPinPolicy` works
for Hermod's `SslStream` clients and servers (HTTP/1.1, WebSockets, HTTP/2) through
`KeyPinValidator.ForServerCertificates<T>`, `ForClientCertificates<T>` and
`ForSslStream`.

### Raw public keys (RFC 7250)

A key-pinned peer needs no certificate at all. Where both sides say so, the
Certificate message carries the bare SubjectPublicKeyInfo, CertificateVerify proves
the key as always, and the key is checked against the key pins: no chain, no name,
no validity period, because there is nothing left that could have one.

```csharp
// The peer's key may come raw: said by the key pins it is checked against.
var client = new QuicClientConnection("192.168.10.5",
                 certificateValidation: CertificateValidationOptions.PinnedTo(
                     new KeyPinPolicy([ serverPin ], RawPublicKeys: RawPublicKeyMode.Require)),
                 clientCertificate: stationKey);

// The own key is shown raw: said by the key itself.
serverKey .RawPublicKeys = RawPublicKeyMode.Prefer;
stationKey.RawPublicKeys = RawPublicKeyMode.Prefer;

var server = new QuicServerConnection(serverKey,
                 clientCertificate: ClientCertificateOptions.RequirePinned(
                     new KeyPinPolicy([ stationPin ], RawPublicKeys: RawPublicKeyMode.Prefer)));

// afterwards: client.ServerKey / .ServerCertificateType,
//             server.ClientAuthentication.Key / server.ClientCertificateType
```

| Mode | The peer's key (`KeyPinPolicy.RawPublicKeys`) | The own key (`ServerCertificate.RawPublicKeys`) |
|---|---|---|
| `Off` (default) | comes in a certificate; no extension is sent | is shown in its certificate; a client that takes raw keys alone is refused |
| `Prefer` | raw, or in a certificate | raw to a peer that takes raw keys, in its certificate to every other |
| `Require` | raw alone; a certificate is refused | raw alone: a server ends the handshake with `unsupported_certificate`, a client asked for a certificate shows none |

A key that is shown raw still has its certificate, the carrier that is shown to a
peer without RFC 7250: one key, and the same key pin either way. A raw public key
is taken from a key-pinned peer alone — a CA-checked endpoint never offers the
extension, and refuses a server that answers with it (`unsupported_extension`) or
chooses a type that was not offered (`illegal_parameter`). A key the pins refuse
ends the handshake with `bad_certificate` (client) or `access_denied` (server).
A session ticket remembers the key pin it was issued under and is not used once
that pin is gone from the pins. The mixed cases work: a raw server key with a
client certificate, and the reverse.

## RFC compliance

| RFC / draft | Title | Status |
|---|---|---|
| **9000** | QUIC: A UDP-Based Multiplexed and Secure Transport | ✅ Packets, frames, streams, flow control, connection lifecycle, migration incl. preferred_address, CID management, stateless reset, version negotiation, Retry (incl. stateless), NEW_TOKEN, path MTU discovery, transport-error matrix |
| **9001** | Using TLS to Secure QUIC | ✅ Initial/handshake/1-RTT keys, header protection, key update, key discard, 0-RTT, Retry integrity. Appendix A vectors byte-exact |
| **9002** | QUIC Loss Detection and Congestion Control | ✅ RTT estimation, ACK-based + PTO loss detection, NewReno, pacing, persistent congestion |
| **8446** | TLS 1.3 | ✅ Full handshake engine: HelloRetryRequest, session resumption/PSK, 0-RTT, client certificates. Key schedule verified against RFC 8448 |
| **7250** | Raw Public Keys in TLS | ✅ `server_certificate_type` and `client_certificate_type` in ClientHello and EncryptedExtensions, as server and as client; for key-pinned peers |
| **8448** | Example Handshake Traces for TLS 1.3 | ✅ Used as test vectors for the key schedule and exporter |
| **7748 / 8032 / 8410** | X25519, X448, Ed25519, Ed448 | ✅ Key exchange + signatures (via BouncyCastle) |
| **FIPS 204 / draft-ietf-tls-mldsa** | ML-DSA certificates | ✅ BCL-native; a fully post-quantum handshake runs end-to-end |
| **draft-ietf-tls-hybrid-design** | X25519MLKEM768 hybrid key exchange | ✅ |
| **draft-ietf-quic-reliable-stream-reset** | RESET_STREAM_AT | ✅ `reset_stream_at` transport parameter + frame 0x24 |
| **draft-ietf-quic-ack-frequency** | ACK Frequency | ✅ `min_ack_delay` transport parameter + ACK_FREQUENCY (0xaf) / IMMEDIATE_ACK (0x1f); receiver honours the negotiated thresholds incl. the §6.2 reordering window, sender API is opt-in |
| **draft-ietf-quic-qlog-\*** | qlog main schema 14 / QUIC events 13 | ✅ JSON-SEQ traces, qvis-ready |
| **9369** | QUIC Version 2 | ◑ Recognised for version negotiation; v1 is what we speak |
| **8899** | Datagram PLPMTUD | ⬜ Not implemented — see below |

## Interop

The client is verified against eight independent QUIC stacks, each with **full**
certificate validation (no `-k`):

| Target | Stack |
|---|---|
| cloudflare-quic.com | quiche (Cloudflare) |
| quic.nginx.org | nginx QUIC |
| www.google.com | Google QUIC |
| www.facebook.com | mvfst (Meta) |
| www.litespeedtech.com | lsquic (LiteSpeed) |
| outlook.office.com | msquic (Microsoft) — exercises P-256 + AES-256 + RSA |
| caddyserver.com | quic-go (Caddy) |
| www.akamai.com | Akamai QUIC |

The server side is verified against `curl --http3` in two builds —
ngtcp2/LibreSSL and OpenSSL-QUIC — including the stateless Retry path and mutual
TLS with a client certificate from an OpenSSL CA.

## Test

358 tests live under [`HermodTests/QUIC`](../../HermodTests/QUIC), mirroring this
folder layout.

```powershell
dotnet test HermodTests/HermodTests.csproj --filter "FullyQualifiedName~Hermod.Tests.QUIC"
```

Three things about them are worth knowing:

- **RFC vectors are tests.** RFC 9001 Appendix A (Initial secrets, packet and
  header protection, the Retry integrity tag) and RFC 8448 (key schedule,
  exporter) are asserted byte-exactly. Interop rests on these before it rests on
  any live server.
- **The link can be made hostile.** `LossyNetwork` is a seeded in-process link
  that drops, reorders and duplicates datagrams reproducibly. It found two
  genuine handshake deadlocks that a perfect link structurally cannot find: a
  HANDSHAKE_DONE that was never retransmitted (RFC 9000 §13.3) and a client that
  stopped probing before address validation (RFC 9002 §6.2.2.1).
- **Debugging is built in.** `KeyLog` writes NSS key-log files so Wireshark can
  decrypt a capture; `QlogWriter` emits qlog traces you can drop into qvis.

## Observability

qlog answers *what happened on this one connection* and writes a file per
connection. For *what is this process doing right now*, `Diagnostics/` publishes
metrics and events that cost nothing while nobody listens — every hot path is
guarded by `Instrument.Enabled` or `EventSource.IsEnabled()`.

Meter **`Vanaheimr.Hermod.Quic`**:

| Instrument | Kind | Meaning |
|---|---|---|
| `quic.connections.active` | UpDownCounter | Connections currently alive |
| `quic.handshakes` | Counter | Handshakes that reached the 1-RTT keys |
| `quic.handshake.duration` | Histogram (ms) | Connection object → 1-RTT keys |
| `quic.streams.opened` | Counter | Streams opened, locally or by the peer |
| `quic.streams.closed` | Counter | Streams released by their owner and finished |
| `quic.bytes.received` / `quic.bytes.sent` | Counter (bytes) | UDP payload |
| `quic.packets.sent` / `quic.packets.lost` | Counter | Protected packets |
| `quic.frames.retransmitted` | Counter | Frames re-queued after loss or PTO |
| `quic.rtt.smoothed` | Histogram (ms) | Smoothed RTT per acknowledgment |
| `quic.congestion_window` | Histogram (bytes) | Congestion window per acknowledgment |

All of them carry a `role` tag of `client` or `server`.

```powershell
dotnet-counters monitor --process-id <pid> --counters Vanaheimr.Hermod.Quic
```

EventSource **`Vanaheimr-Hermod-Quic`** adds the lifecycle: `ConnectionStarted`,
`HandshakeCompleted`, `ConnectionClosed` (informational) and `PacketLost`
(verbose).

```powershell
dotnet-trace collect --process-id <pid> --providers Vanaheimr-Hermod-Quic
```

`quic.streams.opened` less `quic.streams.closed` is what the connections hold.
A connection lets go of a stream once its owner calls `ReleaseStream` and nothing
is left on it: our FIN or RESET_STREAM is out, the peer's final size is known.
HTTP/3 releases each request stream once it is answered, and the client each
request it forgets; a tunnel once both its directions are over; a WebTransport
session once it has ended, and each of its streams once it is done; a peer's
unidirectional stream of a type it does not know, grease among them, at once,
having stopped reading it unless it was finished. A stream nobody releases
stays until its connection ends.

## Not here yet

- **Receive-side GRO and per-connection parallelism** — send-side GSO exists;
  the receive path is still one loop.
- Delayed acknowledgments per RFC 9000 §13.2.2 are on by default
  (`DelayedAcknowledgments`). The **draft ACK-frequency extension** on top of them
  is implemented: we advertise `min_ack_delay` and honour a peer's ACK_FREQUENCY /
  IMMEDIATE_ACK, while sending them ourselves is an opt-in call
  (`TrySendAckFrequency` / `TrySendImmediateAck`) rather than an automatic policy.
