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

**HTTP/3 is not in this repository.** QPACK and the H3 framing/mapping live in the
sibling project
[HTTP/3 Conformance Tests](https://github.com/Vanaheimr/HTTP3ConformanceTests),
which consumes this transport. The line runs between transport and HTTP mapping.

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

## RFC compliance

| RFC / draft | Title | Status |
|---|---|---|
| **9000** | QUIC: A UDP-Based Multiplexed and Secure Transport | ✅ Packets, frames, streams, flow control, connection lifecycle, migration incl. preferred_address, CID management, stateless reset, version negotiation, Retry (incl. stateless), NEW_TOKEN, path MTU discovery, transport-error matrix |
| **9001** | Using TLS to Secure QUIC | ✅ Initial/handshake/1-RTT keys, header protection, key update, key discard, 0-RTT, Retry integrity. Appendix A vectors byte-exact |
| **9002** | QUIC Loss Detection and Congestion Control | ✅ RTT estimation, ACK-based + PTO loss detection, NewReno, pacing, persistent congestion |
| **8446** | TLS 1.3 | ✅ Full handshake engine: HelloRetryRequest, session resumption/PSK, 0-RTT, client certificates. Key schedule verified against RFC 8448 |
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

Note that `quic.streams.opened` is a counter and not a gauge of live streams:
QUIC stream state stays for the lifetime of the connection, because a peer may
still reference a finished stream, so there is no close to count down.

## Not here yet

- **Receive-side GRO and per-connection parallelism** — send-side GSO exists;
  the receive path is still one loop.
- Delayed acknowledgments per RFC 9000 §13.2.2 are on by default
  (`DelayedAcknowledgments`). The **draft ACK-frequency extension** on top of them
  is implemented: we advertise `min_ack_delay` and honour a peer's ACK_FREQUENCY /
  IMMEDIATE_ACK, while sending them ourselves is an opt-in call
  (`TrySendAckFrequency` / `TrySendImmediateAck`) rather than an automatic policy.
