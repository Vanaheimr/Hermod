# HTTP/3

HTTP/3 (RFC 9114) and QPACK (RFC 9204) written from scratch on top of the QUIC
stack in [`../QUIC`](../QUIC/README.md) — request/response over QUIC streams,
header compression with a real dynamic table, and the extensions that turn HTTP/3
into a transport in its own right: WebSockets, HTTP datagrams and WebTransport.

There is no `System.Net.Http` underneath and no `HttpClient`. Frames, streams,
QPACK instructions, the message state machine and every error code are hand-rolled
against the RFCs, on a stack whose TLS handshake and packet protection are equally
hand-rolled.

> ⚠️ **Reference implementation.** Requests and responses with bodies in both
> directions, streaming in both directions, trailers, interim responses, request
> cancellation, graceful shutdown, priorities, WebSockets, datagrams and complete
> WebTransport sessions all work end-to-end, and the server is verified against
> `curl --http3` and against **Chrome and Edge**. It is built for learning and for
> conformance testing, not for production traffic — the receive path is a single
> loop, and there is no server push.
> See [what is not here](#not-here-yet).

---

## Layout

| Folder / file | Contents |
|---|---|
| `Http3ClientConnection.cs` / `Http3ServerConnection.cs` | The deterministic core: consumes datagrams, produces datagrams, invokes a handler. No socket, no `await`. |
| `Http3Client.cs` / `Http3Server.cs` | Task-based facades over real UDP sockets: background pump, awaitable requests, `IAsyncDisposable`. |
| `Http3Frame.cs` | RFC 9114 §7.2 frame types + the reader that yields only complete frames |
| `Http3Message.cs` | `Http3Request`, `Http3Response`, header fields, interim responses, trailers |
| `Http3MessageValidator.cs` | §4.1.2 pseudo-header and field-name rules — what makes a message *malformed* |
| `Http3Qpack.cs` | The connection's unidirectional streams: control stream, SETTINGS, QPACK encoder/decoder streams, per-frame placement rules |
| `Http3Priority.cs` | RFC 9218: the `priority` header, PRIORITY_UPDATE and the §10 scheduler |
| `Http3RequestBody.cs` | A thread-safe reader for a request body still arriving |
| `Http3Tunnel.cs` | Extended-CONNECT tunnel: bytes in DATA frames, plus HTTP datagrams |
| `Http3Constants.cs` | Stream types, frame types, SETTINGS identifiers, the §8.1 error codes |
| `UdpBatchSender.cs` | One `sendmsg` per batch via UDP_SEGMENT (GSO) where the platform offers it, a plain send loop otherwise |
| `QPack/` | RFC 9204: static + dynamic table, Huffman, encoder/decoder, encoder-stream instructions |
| `WebSocket/` | RFC 6455 framing on top of a tunnel — see [its README](WebSocket/README.md) |
| `WebTransport/` | draft-ietf-webtrans-http3-13: sessions, streams, datagrams, capsules, flow control |

## Two layers, on purpose

The connection classes are **deterministic and socket-free**, exactly like the QUIC
core beneath them. They take a datagram, hand back the datagrams to send, and never
sleep or await. Everything in the suite — a whole client/server pair, a seeded lossy
link, 0-RTT replays — therefore runs in-process, in milliseconds, reproducibly.

The socket lives one layer up. `Http3Client` and `Http3Server` own a `UdpClient` and
a background pump, and expose an ordinary Task-based API. The pump itself still never
awaits into the core: it *polls* the handler task and pulls the next body chunk, so
adding async handlers did not make a single test non-deterministic.

If you want a different I/O model — an event loop of your own, one socket shared by
many connections, or an in-memory link for tests — use the connection classes
directly and keep your own loop.

## Using it

### Client

```csharp
using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;   // CertificateValidationOptions

await using var client = new Http3Client("cloudflare-quic.com");
await client.ConnectAsync();

Http3Response response = await client.GetAsync("/");
Console.WriteLine($"{response.Status}, {response.Body.Length} bytes");

await client.CloseAsync();          // GOAWAY + CONNECTION_CLOSE, not just a dropped socket
```

A rejected request throws `Http3RequestException`; `IsRetryable` is `true` when the
server rejected it under GOAWAY, i.e. when replaying it on a new connection is safe.

### Server

```csharp
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;          // HeaderField
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;             // ServerCertificate

using var certificate = ServerCertificate.CreateSelfSigned("localhost");

await using var server = new Http3Server(certificate, async (request, cancellationToken) =>
{
    await Task.Yield();                       // the loop carries on past the first await
    return new Http3Response
    {
        Status  = 200,
        Headers = [new HeaderField("content-type", "text/plain")],
        Body    = Encoding.UTF8.GetBytes($"you asked for {request.Path}"),
    };
}, port: 4433);

server.Start();
```

The cancellation token is cancelled when the client aborts the request (RFC 9114
§4.1.1) or the server stops. A handler that throws produces a 500, never a connection
error.

### Bodies that do not fit in memory

Responses stream from a `Stream`, and the pump only reads the next chunk while less
than 64 KiB is unsent — without that watermark a 4 MB body is slurped into memory even
when the peer has granted no credit:

```csharp
return new Http3Response { Status = 200, BodyStream = File.OpenRead(path) };
```

Requests stream through a third handler overload, invoked right after the header
section while the upload is still arriving. `Http3RequestBody` **is** a `Stream`, so
anything that consumes one works on it directly:

```csharp
new Http3ServerConnection(certificate,
    async (request, body, cancellationToken) =>
    {
        using var sha = SHA256.Create();
        byte[] hash = await sha.ComputeHashAsync(body, cancellationToken);
        return new Http3Response { Status = 200, Body = hash };
    });
```

Backpressure is real: more than 64 KiB unread and the connection stops reading from
the QUIC stream, so the receive window closes and the peer throttles.

### Extended CONNECT: WebSockets, datagrams, WebTransport

One `:protocol` value decides which of the three a CONNECT request opens
(RFC 8441/9220 for WebSockets, RFC 9297/9221 for datagrams, the WebTransport draft
for sessions):

```csharp
new Http3ServerConnection(certificate, handler,
    enableDatagrams:         true,
    webTransportMaxSessions: 4,
    connectHandler:          request => request.Protocol == "websocket"
                                 ? new Http3ConnectResult { Status = 200, OnTunnel = tunnel =>
                                       _ = Echo(new WebSocketConnection(tunnel, WebSocketRole.Server)) }
                                 : new Http3ConnectResult { Status = 501 },   // RFC 9220 §3 SHOULD
    webTransportHandler:     request => request.Path == "/wt"
                                 ? session => { /* streams + datagrams */ }
                                 : null);                                     // ⇒ 404
```

A WebTransport session gives you unidirectional and bidirectional streams, unreliable
datagrams, the session-bound keying-material exporter (draft §4.7) and the
flow-control capsules — the whole draft, not a subset.

## RFC compliance

| RFC / draft | Title | Status |
|---|---|---|
| **9114** | HTTP/3 | ✅ Frames, streams, message state machine, malformed detection, `SETTINGS_MAX_FIELD_SECTION_SIZE`, request cancellation, GOAWAY, trailers, interim responses, the full §8.1 error code set |
| **9204** | QPACK | ✅ Static + dynamic table, Huffman, encoder/decoder streams, blocked streams, eviction. Appendix B vectors byte-exact |
| **9218** | Extensible Prioritization Scheme | ✅ `priority` header, PRIORITY_UPDATE, the §10 scheduling recommendation (urgency, incremental) |
| **8441 / 9220** | Bootstrapping WebSockets with HTTP/2 / HTTP/3 | ✅ Extended CONNECT, `SETTINGS_ENABLE_CONNECT_PROTOCOL`, tunnel bytes in DATA frames |
| **6455** | The WebSocket Protocol | ✅ Framing, close handshake, `permessage-deflate` |
| **9297 / 9221** | HTTP Datagrams / QUIC DATAGRAM frames | ✅ `SETTINGS_H3_DATAGRAM`, quarter-stream-ID mapping, unreliable delivery to a tunnel |
| **draft-ietf-webtrans-http3-13** | WebTransport over HTTP/3 | ✅ Complete: sessions, uni/bidi streams, datagrams, capsules, flow control, protocol negotiation (§3.3), keying-material exporter (§4.7) |
| **9110 / 9111** | HTTP Semantics / Caching | ◑ Only what §4 of RFC 9114 requires of a mapping — this is not a full HTTP semantics layer |
| **9114 §4.6** | Server push | ⬜ Deliberately not implemented — see below |

## Interop

The server answers `curl --http3` in two independent builds (ngtcp2/LibreSSL and
OpenSSL-QUIC) and, as of 2026-07-30, **Chrome 150 and Edge 150** — the browsers pass
a self-test page covering a 300 KB body verified byte for byte, a 64 KiB request body,
103 Early Hints with trailers, and a complete WebTransport session with datagram,
bidirectional and unidirectional echo.

Browsers are a harder peer than `curl`, and not only because of the certificate
dance. Chrome scrambles its ClientHello for anti-ossification — roughly 1.7 KB spread
over two Initial packets in a dozen out-of-order CRYPTO frames at shuffled offsets,
interleaved with PING and PADDING — and it decides whether a server supports
WebTransport the instant its handshake completes, which is why this server puts its
SETTINGS on the wire as soon as it has 1-RTT write keys rather than waiting for the
client's Finished.

The client side inherits the QUIC stack's matrix of eight foreign servers. Both
matrices, the exact commands to repeat them and the two browser flags that are
unavoidable live in the sibling project
[HTTP/3 Conformance Tests](https://github.com/Vanaheimr/HTTP3ConformanceTests),
which drives this code over real sockets.

## Test

247 tests live under [`HermodTests/HTTP3`](../../HermodTests/HTTP3), grouped the way
this folder is:

| Folder | Covers |
|---|---|
| `Api/` | The async facades over real loopback sockets, async handlers, streaming request bodies, several clients against one server |
| `Connection/` | End-to-end client↔server, GOAWAY, request cancellation, SETTINGS timing, injected clocks |
| `Messages/` | Frames, the frame/stream state machine, malformed detection, trailers and 1xx, field-section size, priorities |
| `QPack/` | RFC 9204 vectors, dynamic table, encoder↔decoder round trips over the full stack |
| `Security/` | 0-RTT and its anti-replay, mutual TLS, stateless Retry, server hardening, keying-material export |
| `Tunnels/` | HTTP datagrams, WebSockets over HTTP/3, tunnel thread-safety |
| `WebTransport/` | Sessions, streams, capsules, protocol negotiation |
| `Robustness/` | The seeded lossy link and the allocation/throughput regression guards |

```powershell
dotnet test HermodTests/HermodTests.csproj --filter "FullyQualifiedName~Hermod.Tests.HTTP3"
```

Two of these deserve a note. `Messages/Http3FrameStateMachineTests` drives a *raw*
QUIC connection that writes HTTP/3 bytes by hand, so protocol violations no real
client would commit can be asserted to produce the right connection error.
`Robustness/PerformanceBenchTests` measures allocations with
`GC.GetAllocatedBytesForCurrentThread` — exact, because the pump is single-threaded —
with bounds set at least a factor of two above the real numbers, so they guard
against regressions without becoming flaky.

## Not here yet

- **Server push** (RFC 9114 §4.6) is not implemented, and that is a decision rather
  than a gap: browsers removed support for it. PUSH_PROMISE, CANCEL_PUSH and
  MAX_PUSH_ID are still *validated* — a peer that misuses them gets the error the
  RFC asks for — but nothing is ever pushed.
- **The WebSocket framing is still a copy.** `WebSocket/` duplicates
  `HTTP2/WebSocket/` byte for byte apart from the namespace. Now that both live in
  this repository the copy can go away; see [its README](WebSocket/README.md).
- **Firefox is untested.** It ignores the Chromium command-line flags and wants the
  certificate in its own NSS store, so it needs a setup path of its own.
