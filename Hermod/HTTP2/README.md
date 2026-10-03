# HTTP/2

A from-scratch HTTP/2 stack built directly on `SslStream`, focused on the
**binary framing layer**: frame parsing, HPACK header compression, the stream
state machine, flow control, and TLS + ALPN (`h2`) negotiation. No Kestrel, no
`System.Net.Http` HTTP/2 stack — everything is hand-rolled.

It's three parts: a shared protocol library (`Core` — direction-neutral
framing, HPACK, the stream layer, WebSocket framing, HTTP semantics), an HTTP/2
**server**, and an HTTP/2 **client**, each its own project. Both roles are
interop-verified against .NET (`HttpClient`/curl for the server; a Kestrel
HTTP/2 server for the client).

📋 This README doubles as the **complete reference** — the [RFC compliance
matrix](#rfc-compliance-matrix), a [feature-by-feature breakdown](#feature-detail),
the [security-hardening summary](#security-hardening-summary), and [what's
explicitly out of scope](#explicitly-out-of-scope) are all below.

> ⚠️ **Reference implementation.** Requests, responses, flow control, real
> stream multiplexing, CONTINUATION-flood/Rapid-Reset/stream-ID-exhaustion
> and Slowloris/timeout hardening, RFC 9113 §8 request validation,
> trailers/implicit stream
> closure, per-stream RST_STREAM cancellation, graceful `GOAWAY` shutdown, a
> table-driven Huffman decoder *and* encoder, a full HPACK encoder (static +
> dynamic table + Huffman), CONNECT + extended CONNECT (RFC 8441) +
> WebSocket (RFC 6455) tunneling, RFC 9218 priority-aware response (and
> client upload) scheduling, streaming request/response bodies with response trailers
> (gRPC-style, verified against .NET `HttpClient` — and a real gRPC service
> interop-tested against `Grpc.Net.Client`), 1xx interim responses
> (`Expect: 100-continue`, 103 Early Hints), an RFC 9110 semantics
> layer (GET/HEAD/OPTIONS, conditional
> requests, Range requests, proactive content negotiation with `Vary`,
> opt-in on-the-fly gzip/brotli/deflate compression), cleartext h2c
> (prior-knowledge, no TLS — server and client),
> authentication (RFC 9110 §11 framework with Basic/Bearer/Digest/Token, plus mutual TLS on
> server and client), and an RFC 9111 client-side cache (freshness, conditional
> revalidation, `Vary`, shared/private semantics) all work end-to-end (verified
> against .NET's strict `HttpClient`/Kestrel and raw frame-level attack
> clients). See `CLAUDE.md` for the full status. Still built for learning the
> wire protocol, not for production traffic (single-process demo host, no
> server push, etc.).

## Test

The tests live under [`HermodTests/HTTP2`](../../HermodTests/HTTP2) — 212 NUnit
tests, including the raw frame-level attack clients and the gRPC interop against
`Grpc.Net.Client`:

```powershell
dotnet test HermodTests/HermodTests.csproj --filter "FullyQualifiedName~Hermod.Tests.HTTP2"
```

Two external conformance results were reached while this stack was still its own
project: **146/146 on [h2spec](https://github.com/summerwind/h2spec)** (the
canonical HTTP/2 conformance suite) over *both* the TLS and cleartext-h2c
listeners, and **517/517** on the
[Autobahn TestSuite](https://github.com/crossbario/autobahn-testsuite) for the
WebSocket framing (RFC 6455) — the full suite, including `permessage-deflate`
(RFC 7692) compression.

The harnesses that produced those two numbers did not come across when the stack
was vendored into Hermod — they stayed in the project this stack grew up in,
which is still maintained as the demo host and conformance driver for it:
[**Vanaheimr/HTTP2ConformanceTests**](https://github.com/Vanaheimr/HTTP2ConformanceTests).
It pulls Hermod back in as a submodule and adds the runnable demo, the live-host
raw-frame harnesses, and the conformance drivers, each in a bash and a PowerShell
variant (`tests/h2spec.sh` / `.ps1`, `tests/autobahn.sh` / `.ps1`,
`tests/run-tests.sh` / `.ps1`) — so both numbers stay reproducible on Linux and
Windows alike, just not from inside this repository, which has no demo host to
point them at.

Ad-hoc `curl` checks against the demo host:

```bash
curl --http2 -k https://localhost:8443/
curl --http2 -k https://localhost:8443/echo -d "Hello HTTP/2!"
curl --http2 -k https://localhost:8443/large   # 128 KiB — exercises flow control
curl --http2 -k https://localhost:8443/slow    # 2 s handler — exercises multiplexing

# RFC 9110 core mechanics — GET/HEAD/OPTIONS, conditional requests, Range:
curl --http2 -k -I https://localhost:8443/files/resource.txt          # HEAD
curl --http2 -k -X OPTIONS https://localhost:8443/files/resource.txt  # -> 204 + Allow
curl --http2 -k -H 'Range: bytes=0-9' https://localhost:8443/files/resource.txt
curl --http2 -k -H 'If-None-Match: "<etag from a prior response>"' https://localhost:8443/files/resource.txt

# RFC 9530 digest fields — a 206 is the one response carrying both:
curl --http2 -k -i -H 'Range: bytes=0-31' https://localhost:8443/files/resource.txt   # Content-Digest (slice) + Repr-Digest (whole)
curl --http2 -k -i -H 'Want-Content-Digest: sha-512=10' https://localhost:8443/files/resource.txt

# RFC 10008 — the HTTP QUERY method (a safe, body-carrying read). /search:
curl --http2 -k https://localhost:8443/search                 # GET -> whole corpus
curl --http2 -k -X QUERY --data 'ap' https://localhost:8443/search   # QUERY -> filtered (note Content-Location)

# RFC 9110 content negotiation — /files/greeting has en/de text + en JSON variants:
curl --http2 -k https://localhost:8443/files/greeting                        # server default (en text)
curl --http2 -k -H 'Accept-Language: de' https://localhost:8443/files/greeting   # -> German
curl --http2 -k -H 'Accept: application/json' https://localhost:8443/files/greeting  # -> JSON (note the Vary header)

# RFC 9110 §11 auth — /secret needs Basic alice:secret or Bearer valid-token-123:
curl --http2 -k -i https://localhost:8443/secret                             # -> 401 + WWW-Authenticate
curl --http2 -k -u alice:secret https://localhost:8443/secret                # -> 200
curl --http2 -k -H 'authorization: Bearer valid-token-123' https://localhost:8443/secret  # -> 200

# cleartext h2c (prior knowledge — no TLS), on :8080:
curl --http2-prior-knowledge http://localhost:8080/
curl --http2-prior-knowledge http://localhost:8080/echo -d "Hello h2c!"
```

`-k` skips certificate verification (self-signed). `--http2` forces HTTP/2 over
TLS via ALPN; `--http2-prior-knowledge` speaks cleartext HTTP/2 directly (no
Upgrade, no TLS). Note: the curl bundled with Windows has no HTTP/2 support and
silently falls back to HTTP/1.1.


## Where application logic plugs in

The `HTTP2RequestHandler` delegate (see `HTTP2Connection.cs`) receives decoded
request headers + body and returns response headers + body. That is the seam
where an existing HTTP/1.1 handler would attach. The parallel seam for
tunnels — CONNECT and extended CONNECT (RFC 8441), e.g. to bootstrap a
WebSocket — is `HTTP2ConnectHandler`: it decides accept/reject up front, and
if accepted, runs against an `HTTP2Tunnel` (a raw bidirectional byte stream
over the accepted stream). A third, narrower seam sits one level above the
first: `HTTPResourceHandler` (see `HTTPSemantics.cs`) just answers "what is
this resource's current representation, or null for 404" — `HTTPSemantics.Wrap`
turns that into an ordinary `HTTP2RequestHandler`, adding RFC 9110
GET/HEAD/OPTIONS method semantics, conditional requests, and Range requests
(single-range and multi-range `multipart/byteranges`) on top, entirely without
touching HTTP/2 framing. Its `HTTPVariantHandler`
sibling returns *several* representations of a resource, and `Wrap` picks
among them by the client's `Accept` / `Accept-Encoding` / `Accept-Language`
(proactive content negotiation, emitting the appropriate `Vary`). Passing
`CompressResponses: true` to `Wrap` additionally compresses a compressible
identity body on the fly (brotli/gzip/deflate, per the request's
`Accept-Encoding`), weakening the `ETag` and adding `Vary: accept-encoding`.

For streaming — server-streaming, SSE, large transfers without buffering, or
full bidirectional streaming (gRPC) — register an `HTTP2StreamingHandler` on
`HTTP2Server` instead (`StreamingHandler:`). It receives an
`IHTTP2RequestStream` (pull request-body chunks with `ReadAsync` as DATA
arrives; read request `Trailers` once the body ends) and an
`IHTTP2ResponseStream` (optional `WriteInterimResponseAsync` for 1xx — e.g. a
103 Early Hints with `Link` preload headers — then `WriteHeadersAsync` once,
then `WriteAsync` body chunks, then `CompleteAsync(trailers)` — e.g. gRPC's
`grpc-status`). The handler is invoked as soon as the request headers arrive, so
both directions flow at once. `Expect: 100-continue` is handled automatically by
the server. This seam is enough to serve real **gRPC**:
[`GrpcInteropTests`](../../HermodTests/HTTP2/GrpcInteropTests.cs) runs a Greeter
service (unary + server-streaming, length-prefixed messages, `grpc-status` in
trailers) over the stack and interop-tests it against the real `Grpc.Net.Client`.

For authentication, `HTTPAuthentication.RequireAuthentication` wraps a handler
with the RFC 9110 §11 challenge/response flow (401 + `WWW-Authenticate` when
unauthenticated), backed by pluggable schemes — `BasicAuthenticationScheme`
(RFC 7617), `BearerAuthenticationScheme` (RFC 6750),
`DigestAuthenticationScheme` (RFC 7616 — challenge-response, SHA-256, the
password never crosses the wire), and `TokenAuthenticationScheme` (non-standard
but common — Rails/GitHub-style `Token`), each taking an app-supplied validator
so no credential store is baked in. Mutual TLS is a
separate, transport-layer option on `HTTP2Server` (`RequireClientCertificate`)
and `HTTP2Client` (`ClientCertificate`).

Which origins the listener answers for is a server-level question, decided
before any handler runs — by default the identities in its own certificate, or
an explicitly announced Origin Set:

```csharp
var server = new HTTP2Server(IPAddress.Any, 8443, certificate, MyRequestHandler,

    // RFC 8336: state the origins this connection is authoritative for, instead
    // of leaving the client to infer them from the certificate. Also becomes the
    // yardstick for the 421 check below.
    OriginSet: ["https://example.com", "https://www.example.com"],

    // ... which is otherwise derived from the certificate. Requests naming
    // anything else are answered 421 (Misdirected Request). Pass `_ => true` to
    // answer for every origin, as the server did before this existed.
    IsAuthorityServed: null,

    // RFC 9113 §9.2.2: null applies the Appendix A rule. `_ => false` reaches a
    // peer stuck on a legacy TLS 1.2 cipher suite.
    IsBlocklistedCipherSuite: null);
```

## Using the client

`HTTP2Client` dials a server, negotiates TLS + ALPN `h2`, and returns a
connection you can send concurrent requests on:

```csharp
var conn = await HTTP2Client.ConnectAsync("localhost", 8443,
    ValidateServerCertificate: (_, _, _, _) => true);   // accept the demo's self-signed cert

var response = await conn.SendRequestAsync("GET", "https", "localhost:8443", "/");
Console.WriteLine($"{response.Status}: {Encoding.UTF8.GetString(response.Body)}");

await conn.CloseAsync();
```

It reuses the same framing/HPACK/flow-control code as the server, and is
interop-tested against both this server and a .NET Kestrel HTTP/2 server. Pass
`HTTP2ClientOptions` to `ConnectAsync` for robustness knobs — automatic retry of
server-refused streams (`REFUSED_STREAM` is guaranteed unprocessed, so retrying
is side-effect-safe), and an opt-in PING keepalive that drops a silently-dead
connection instead of hanging:

```csharp
var conn = await HTTP2Client.ConnectAsync("localhost", 8443,
    ValidateServerCertificate: (_, _, _, _) => true,
    Options: new HTTP2ClientOptions {
        MaxRefusedStreamRetries = 2,
        KeepAliveInterval       = TimeSpan.FromSeconds(30),   // 0 = disabled
        TimeProvider            = TimeProvider.System,        // inject a test clock here
        IsBlocklistedCipherSuite = null,                      // null = the RFC 9113 §9.2.2 rule
        MaxResponseBodySize     = 16 * 1024 * 1024,           // the most a buffered response may bring

        // RFC 9110 §8.4 / §11 — the client half of the semantics the server has
        // had all along. Both off by default: they change what goes out on the
        // wire and what comes back, so the caller opts in.
        AutomaticDecompression   = true,                      // ask for br/gzip/deflate, decode transparently
        MaxDecodedBodySize       = 16 * 1024 * 1024,          // and refuse a decompression bomb
        Credentials              = HTTPClientCredentials.UserNameAndPassword("alice", "secret"),

        // What the server may have in flight on the connection, all streams
        // together; four stream windows by default (see "Window sizes" below).
        ConnectionWindowSize     = 4 * 1024 * 1024,
    });

// If the server announced one, its Origin Set (RFC 8336) is here — null until an
// ORIGIN frame arrives, and never populated over cleartext h2c.
Console.WriteLine(conn.OriginSet is null ? "no ORIGIN frame" : String.Join(", ", conn.OriginSet));
```

Concurrent requests beyond the server's `MAX_CONCURRENT_STREAMS` queue (rather
than fail), and a request the server provably never processed (a
`REFUSED_STREAM` past the retry budget, a stream above a `GOAWAY`'s
last-stream-id, or a request started after a `GOAWAY`, which opens no new stream)
surfaces as `HTTP2RequestNotProcessedException` — a signal it's safe to retry on
a fresh connection.

The client can also open CONNECT tunnels and WebSockets (RFC 9113 §8.5 / RFC
8441 / RFC 6455), the mirror of the server's tunneling — both ends of the wire
hand-rolled:

```csharp
// plain CONNECT — a raw bidirectional byte tunnel; DisposeAsync gives it up
// (RST_STREAM CANCEL) unless both sides have ended it
await using var tunnel = await conn.OpenTunnelAsync("proxy.target:443");
await tunnel.WriteAsync(bytes);
var reply = await tunnel.ReadAsync(CancellationToken.None);

// extended CONNECT — a WebSocket (client masks its frames per RFC 6455)
var ws = await conn.OpenWebSocketAsync("localhost", "https", "/ws-echo");
await ws.SendTextAsync("hello", CancellationToken.None);
var msg = await ws.ReceiveAsync(CancellationToken.None);

// opt into permessage-deflate (RFC 7692) — offered on the CONNECT handshake,
// only actually used if the server echoes acceptance back
var wsz = await conn.OpenWebSocketAsync("localhost", "https", "/ws-echo", PerMessageDeflate: true);
```
Requests can carry an RFC 9218 priority hint, and an in-flight request can be
reprioritized (both honored by the priority-aware server):

```csharp
var r = await conn.SendRequestAsync("GET", "https", "localhost:8443", "/big",
    Priority: new HTTP2Priority(Urgency: 0, Incremental: false));   // most urgent

var h = await conn.StartRequestAsync("GET", "https", "localhost:8443", "/slow");
await conn.UpdatePriorityAsync(h.StreamId, new HTTP2Priority(0, false));   // PRIORITY_UPDATE
var slow = await h.Response;
```

The client sends its own DATA by the same priorities: one writer loop per
connection sends every request body and tunnel write, a DATA frame at a time,
by the priority each stream asked the server for. Several WebSockets over one
connection — real-time messages on one, a log upload on another — each open
with their own, and a message on the urgent one overtakes what the upload still
has queued, in both directions:

```csharp
var ocpp = await conn.OpenWebSocketAsync("csms.example", "https", "/ocpp/CS01",
    Priority: new HTTP2Priority(0, false));                          // real-time
var logs = await conn.OpenWebSocketAsync("csms.example", "https", "/logs/CS01",
    Priority: new HTTP2Priority(6, true));                           // background

_ = logs.SendBinaryAsync(logFile, CancellationToken.None);           // a mebibyte or two
await ocpp.SendTextAsync(heartbeat, CancellationToken.None);         // goes first

await logs.UpdatePriorityAsync(new HTTP2Priority(1, false));          // the backend now waits for it
```

For full-duplex request/response streaming — the enabler for client-streaming and
bidirectional gRPC — `StartStreamingRequestAsync` returns a handle whose request
body is written incrementally while the response is read incrementally, both at
once:

```csharp
await using var s = await conn.StartStreamingRequestAsync("POST", "https", "localhost:8443", "/svc.Greeter/Bidi",
    ExtraHeaders: [("content-type", "application/grpc"), ("te", "trailers")]);
var head = await s.GetResponseAsync();                 // status + headers
await s.WriteAsync(frame);                              // send a request-body chunk (DATA)
byte[]? chunk = await s.ReadAsync();                    // read a response-body chunk (null at end)
await s.CompleteRequestAsync();                         // half-close the request side
var trailers = await s.GetTrailersAsync();              // e.g. grpc-status

// …or half-close with request trailers of our own (RFC 9113 §8.1), the mirror of
// the server's IHTTP2ResponseStream.CompleteAsync(Trailers):
await s.CompleteRequestAsync([("x-checksum", "deadbeef")]);
```

`HTTP2CachingClient` wraps a connection with an RFC 9111 cache — it serves fresh
responses without a round trip, revalidates stale ones with conditional
requests, keys variants by `Vary`, and honors `Cache-Control` (with private vs.
shared-cache semantics):

```csharp
var cache = new HTTP2CachingClient(conn, "https", "localhost:8443", HTTPCacheMode.Private);
var a = await cache.GetAsync("/files/resource.txt");   // MISS — fetched from origin
var b = await cache.GetAsync("/files/resource.txt");   // HIT  — served from cache
```

`HTTP2ClientPool` keeps several warm connections to a single origin and hands
each request to the least-loaded one. A connection may die (GOAWAY, socket loss)
without the caller noticing — it's reconnected in the background, and a request
the server provably never processed is transparently retried on another
connection:

```csharp
await using var pool = await HTTP2ClientPool.ConnectAsync("localhost", 8443, acceptAnyCert, MaxConnections: 4);
var r = await pool.SendRequestAsync("GET", "https", "localhost:8443", "/");   // any live connection serves it
// pool.ConnectionCount / pool.Reconnects / pool.Failovers are all observable
```


## RFC compliance matrix

| RFC | Title | Status | Notes |
|---|---|---|---|
| **9113** | HTTP/2 | ✅ Complete | Framing, streams, flow control, settings, GOAWAY, §9.2 TLS profile, §9.1.1 authority checking. h2spec 146/146. |
| **7541** | HPACK: Header Compression | ✅ Complete | Full decoder **and** encoder (static + dynamic table + Huffman both ways). |
| **7301** | TLS ALPN | ✅ | `h2` negotiation in the TLS handshake. |
| **9218** | Extensible Prioritization Scheme | ✅ | `priority` header, `PRIORITY_UPDATE`, `SETTINGS_NO_RFC7540_PRIORITIES`; priority-aware writer on both roles. Both roles emit; the server sends its responses by it, the client its own request bodies and tunnel bytes. A `PRIORITY_UPDATE` from the server is a connection error for the client (§7.1). |
| **8441** | Bootstrapping WebSockets with HTTP/2 | ✅ | Extended CONNECT, `:protocol`, `SETTINGS_ENABLE_CONNECT_PROTOCOL`. |
| **8336** | The ORIGIN HTTP/2 Frame | ✅ | Server announces its Origin Set; client parses it (ignored on stream ≠ 0 and over h2c). |
| **7838** | HTTP Alternative Services | ✅ | ALTSVC frame both directions + the `Alt-Svc` field-value grammar; client records alternatives, does not act on them (no HTTP/3 endpoint to act on yet). |
| **6455** | The WebSocket Protocol | ✅ Complete | Framing, masking, fragmentation, close handshake, UTF-8 validation. Autobahn 517/517. Server **and** client roles. |
| **7692** | Compression Extensions for WebSocket (permessage-deflate) | ✅ | No-context-takeover mode, negotiated on both HTTP/1.1-Upgrade and HTTP/2-CONNECT handshakes. |
| **9110** | HTTP Semantics | ✅ | Methods, conditional requests, Range (single + multi), content negotiation, the §11 auth framework — all mirrored on the client (decode, 401 answering, conditional/Range download resume, redirects). |
| **9111** | HTTP Caching | ✅ | Client-side cache with shared/private semantics. |
| **9530** | Digest Fields | ✅ | `Content-Digest` / `Repr-Digest` + the two `Want-…` fields, both directions, sha-256 and sha-512. Opt-in on either role. |
| **8470** | Using Early Data in HTTP | ◑ Partial | The reachable half: the server judges an intermediary's `Early-Data: 1` and answers **425 (Too Early)**; the client repeats a 425 once, without the field. We terminate no 0-RTT ourselves — `SslStream` has no early-data API — so there is nothing else to implement. |
| **7617** | Basic Authentication | ✅ | |
| **6750** | Bearer Token Usage | ✅ | |
| **7616** | Digest Access Authentication | ✅ | Challenge-response, SHA-256 (+ MD5 interop), stateless nonce, `qop=auth`. |
| **8297** | An HTTP Status Code for Indicating Hints (103 Early Hints) | ✅ | Handler-driven interim responses. |
| **10008** | The HTTP QUERY Method | ✅ | Safe/idempotent/cacheable body-carrying read (published 2026-06). |
| **5861** | HTTP Cache-Control Extensions for Stale Content | ✅ | `stale-while-revalidate`, `stale-if-error` (part of caching). |
| **8941** | Structured Field Values | ◑ Partial | The Dictionary grammar needed to parse the `priority` header. |
| **4647** | Matching of Language Tags | ◑ Partial | Basic-filtering + lookup-truncation for `Accept-Language`. |
| **1123** | (HTTP-date format) | ✅ | Date parsing/formatting for conditional requests. |
| **2069 / 2617** | (legacy Digest) | ✅ | Accepted for interop: no-`qop` responses and `algorithm=MD5`. |

✅ = implemented · ◑ = the subset this stack needs.

---

## Feature detail

### Connection & framing (RFC 9113)

- 9-byte frame header parse/serialize; all frame types
  (DATA, HEADERS, PRIORITY, RST_STREAM, SETTINGS, PUSH_PROMISE, PING, GOAWAY,
  WINDOW_UPDATE, CONTINUATION, ALTSVC, ORIGIN, PRIORITY_UPDATE) — every frame
  type that is not deprecated.
- Connection preface + SETTINGS handshake (server-preface-first ordering,
  SETTINGS ACK).
- Decoupled read/write loops with **true multiplexing** — application handlers
  run on their own tasks; the frame read loop never blocks on app logic.
- Reserved-bit masking, padding handling, atomic HEADERS+CONTINUATION sequences.
- GOAWAY (graceful + error), with a bounded inbound drain so the peer actually
  receives it.
- Request validation (§8): pseudo-header ordering/uniqueness, lowercase field
  names, connection-specific header rejection, `te: trailers` only — malformed
  requests are stream errors, not connection errors.
- Trailers (§8.1) and implicit stream closure (§5.1.1).
- `content-length` vs. DATA-length enforcement (§8.1.1), wherever the body
  ends: at its last DATA frame, or at trailers — buffered and streamed alike. A
  body longer than declared is reset sooner, with `PROTOCOL_ERROR`, at the DATA
  frame that takes it past: a streaming handler never reads a byte past the
  declared length, and a buffered upload is no longer taken in up to the
  body-size cap first. A buffered upload that declared its length and ended with
  trailers used to be reset with `PROTOCOL_ERROR`, as if it had sent no body at
  all; and trailers let a body of another length through.
- The client holds **responses** to their `content-length` the same way ("Clients
  MUST NOT accept a malformed response", §8.1.1), buffered and streamed alike.
  A response fails with an `HTTP2StreamException` carrying `PROTOCOL_ERROR`,
  and its stream is reset with `RST_STREAM PROTOCOL_ERROR`, in these cases:
  - at its HEADERS, if its `content-length` is no number of octets (`1*DIGIT`:
    no sign, no blanks, no list) or two of them differ;
  - at the DATA frame that takes its body past the declared length — that
    frame is not taken in, and a streamed body's reader gets what came before
    it, then the failure;
  - where its body ends shorter than declared: at END_STREAM, or at trailers,
    which it then hands over none of.

  A stream both sides have ended by then is closed and takes no `RST_STREAM`.
  A request body still being sent stops at the reset, and a streamed request's
  next write fails with `PROTOCOL_ERROR`. The refused frame goes back to the
  connection's window, the stream's slot is free, and the connection goes on.
  A response that can have no content (to a HEAD request, a 204, a 304) may
  declare a length it does not send, but not a malformed one. A CONNECT
  tunnel's `content-length` bounds nothing. The client used to accept all of
  these, and handed over whatever body arrived.
- Cleartext **h2c** (prior knowledge, RFC 9113 §3.3) — server and client. (The
  RFC 7540 `Upgrade: h2c` negotiation was removed in RFC 9113 and is
  deliberately not implemented.)

### HPACK (RFC 7541)

- Full decoder: static + dynamic table, integer/string coding, **Huffman decode
  via a bit-level trie**, dynamic-table-size-update bounds (§4.2 / §6.3),
  truncated-block → `COMPRESSION_ERROR`. Integers accumulate in 64 bits and are
  bounds-checked (§5.1): a five-octet encoding that would wrap a 32-bit
  accumulator is a decoding error, not a negative length handed to a slice.
- Full encoder: 61-entry static table, per-connection dynamic table (with a
  volatile-value denylist and *never-indexed* for sensitive fields §7.1.3),
  **Huffman encode**, table-size signaling from the peer's
  `SETTINGS_HEADER_TABLE_SIZE`.
- The 257-entry Huffman table is self-validated at class-init (prefix-collision
  check).
- The client decodes every response header block, also one no exchange takes
  any more — trailers that crossed its `RST_STREAM`, say — and then drops it
  (RFC 9113 §5.1, "updating header compression state"). It used to drop such a
  block undecoded, which left its dynamic table a step behind the server's, so
  the next block that referred to it was decoded wrong; split into `HEADERS`
  and `CONTINUATION`, the block ended the connection. And the `END_STREAM` of a
  continued `HEADERS` frame ends the stream: it was taken from the
  `CONTINUATION` frame, which has no such flag (§6.10), and the response never
  ended.

### Flow control

- Per-stream and connection-level windows; signal-based send-window reservation
  (no polling).
- A peer's `SETTINGS_INITIAL_WINDOW_SIZE` moves the send window of every open
  stream by the difference from its previous value (§6.9.2), which until the
  peer first states it is the RFC's 65 535 (§6.5.2), not our own default. A
  peer that left the setting out of its first SETTINGS and stated it later,
  while a stream was open, used to stall that stream: grpc-go does so in both
  roles, from its BDP estimator.
- **WINDOW_UPDATE batching** (replenish once per half-window, not per DATA
  frame) + larger default windows: 1 MiB per stream, 4 MiB per connection
  (`ConnectionWindowSize`, configurable on both roles — see
  [below](#window-sizes-memory-against-throughput)).
- **Consumption-driven backpressure**: for streaming/tunnel bodies the receive
  window is returned only as the *application* reads, so a slow consumer forces
  the peer to stop — the window *is* the memory bound.
- **A stalled stream does not stop the others.** All streams of a connection
  draw on its window, and a stream whose handler does not read keeps the window
  of what it was sent. With a connection window no larger than a stream's —
  1 MiB each, as before — one such stream took all of it, and the client could
  send DATA on no other stream of the connection: with an OCPP WebSocket and a
  log-file WebSocket over one connection (RFC 8441), a log file written away
  slowly held up the OCPP channel. The connection window is now four stream
  windows: up to three streams can stall with their whole window taken, and
  the others still share a stream window's worth.
- **What is owed is given back in time.** The server returns the connection
  window of what has been read once half of the window is owed — or once the
  client has no more of it left than that, checked at every read and every DATA
  frame. It used to wait for the half alone, which never comes while unread
  streams hold more than half of the window: the streams that were read waited
  for good, though most of what they had been sent was read. (Go's
  `x/net/http2` uses the same test, `inflow.add`; Chromium instead returns what
  is owed at the next read once 5 s have passed since its last update.)
- The **client** pushes back on a tunnel or a streamed response as well, with
  its stream window alone: the stream window of their DATA goes back as the
  application reads it, so one the application does not read holds no more than
  its window, 1 MiB, and DATA beyond it is a `FLOW_CONTROL_ERROR`. It used to
  give back every window on receipt, read or not, and buffered whatever the
  server sent. The connection window still goes back on receipt — as .NET's
  `SocketsHttpHandler` does — so an unread stream keeps no window from the
  others, and no number of them can stall the connection. The client decides
  how many streams it opens, and often reads them one after the other: of five
  streamed responses opened at once, the one read now never waits for window
  that the four not read yet hold. A buffered response, padding and DATA no
  exchange takes go back on receipt, too. No stream-level `WINDOW_UPDATE` goes out once the
  server can send nothing more on the stream. A stream that will not be read to
  its end is given up with `DisposeAsync` (see
  [below](#streaming-trailers--grpc)).
- Bounded buffered request body (`MaxRequestBodySize`, default 16 MiB).
- Bounded buffered **response** body on the client (`MaxResponseBodySize`,
  default 16 MiB — the counterpart of `MaxRequestBodySize`). A response that
  `SendRequestAsync` collects whole fails with `HTTP2ResponseTooLargeException`
  at the DATA frame that would take its body past the limit (padding not
  counted), which is not taken in — or at its HEADERS already, if it declares
  a larger `content-length`, unless it can have no body at all (the answer to
  a HEAD request, a 204, a 304; §8.1.1). The stream is reset with
  `RST_STREAM CANCEL`: the server did nothing wrong, the client wants no more
  of it. (A body that goes past its own `content-length` as well is malformed,
  and that goes first: `PROTOCOL_ERROR`, see
  [Connection & framing](#connection--framing-rfc-9113).) The refused frame, and whatever the server sent before it read the
  reset, still go back to the connection window; nothing of the request follows
  the reset, and the stream's slot is free once the reset is out: the
  connection goes on. The client used to buffer whatever the server sent — it
  gives a buffered body's window back on receipt, and `MaxDecodedBodySize`
  bounds only what decoding makes of a body taken in whole. Streamed responses
  (`StartStreamingRequestAsync`, `DownloadAsync`) and tunnels are read chunk
  by chunk, and not bound by it.
- Padding counted against flow control (§6.1); closed-stream DATA still
  window-accounted (§6.9); cookie-crumb reassembly (§8.2.3).

### Window sizes: memory against throughput

The two windows bound different things, and whatever one gains, another loses
(`HTTP2FlowControl` says the same in code):

- **Memory.** The server returns the window of a streamed body or of tunnel
  bytes only once the handler has read them, so the connection window is the
  most one connection can be made to hold for handlers that do not read — by a
  stalled log writer, or by a client that opens streams to slow handlers on
  purpose. 4 MiB per connection, where it used to be 1 MiB: ten thousand
  charging stations, each with four stalled channels, could pin about 40 GiB
  instead of about 10 GiB. A connection with a single stalled stream still pins
  no more than that stream's window, 1 MiB.
- **Throughput.** Per round trip, a stream carries at most its window, and all
  streams of a connection together at most the connection window. At 100 ms
  round-trip time: 10 MiB/s for a stream (1 MiB), 40 MiB/s for all streams of a
  connection together (4 MiB, where it was 10 MiB/s).
- **Isolation.** Each stalled stream takes up to a stream window of the
  connection's; what is left, the others share. At or below one stream window,
  a single stalled stream stops all others again.

`ConnectionWindowSize` moves along that line, on the server
(`HTTP2Server(…, ConnectionWindowSize: …)`) and on the client
(`HTTP2ClientOptions.ConnectionWindowSize`): at least RFC 9113's 65 535 octets —
a connection window can be raised, not lowered — and at most 2³¹ − 1. The stream
window stays at 1 MiB. On the client, which returns the connection window on
receipt, it bounds only how much can be in flight: what the client holds for a
stream the application does not read, the stream window bounds.

```csharp
// Many connections, memory for one stalled channel each: two stream windows.
var server = new HTTP2Server(IPAddress.Any, 8443, certificate, MyRequestHandler,
                             ConnectionWindowSize: 2 * HTTP2FlowControl.StreamWindowSize);

// Several large downloads at a time over a long round trip:
var conn = await HTTP2Client.ConnectAsync("example.com", 443,
                                          Options: new HTTP2ClientOptions { ConnectionWindowSize = 16 * 1024 * 1024 });
```

What others grant their peer by default, looked up in their sources on
2026-10-01:

| Implementation | Role | Stream window | Connection window | Connection window returned |
|---|---|---|---|---|
| **Hermod** | server, client | 1 MiB | 4 MiB, configurable | server: as the handler reads; client: on receipt |
| Chrome — Chromium `net/` ([`http_network_session.cc`](https://github.com/chromium/chromium/blob/ac463d9560ba0dca5c6ade6f48fd64b2942754a5/net/http/http_network_session.cc#L47-L49)) | client | 6 MiB | 15 MiB | as the consumer reads: once more than half is owed, or 5 s after the last update ([`spdy_session.cc`](https://github.com/chromium/chromium/blob/ac463d9560ba0dca5c6ade6f48fd64b2942754a5/net/spdy/spdy_session.cc#L3355-L3395)) |
| Go `golang.org/x/net/http2` v0.59.0, `Server` ([`config.go`](https://github.com/golang/net/blob/v0.59.0/http2/config.go#L100-L114)) | server | 1 MiB | 1 MiB | as the handler reads ([`server.go`](https://github.com/golang/net/blob/v0.59.0/http2/server.go#L2396-L2404)) — one stream can take all of it |
| Go `golang.org/x/net/http2` v0.59.0, `Transport` ([`transport.go`](https://github.com/golang/net/blob/v0.59.0/http2/transport.go#L42-L49)) | client | 4 MiB | 1 GiB + 65 535 | as the body is read; from 4 KiB owed on, or once owed ≥ left ([`flow.go`](https://github.com/golang/net/blob/v0.59.0/http2/flow.go#L33-L53)) |
| nghttp2 v1.70.0, library ([`nghttp2.h`](https://github.com/nghttp2/nghttp2/blob/v1.70.0/lib/includes/nghttp2/nghttp2.h#L224-L237)) | both | 65 535 | 65 535 | on receipt by default, once half is owed ([`nghttp2_helper.c`](https://github.com/nghttp2/nghttp2/blob/v1.70.0/lib/nghttp2_helper.c#L248-L251)) |
| nghttpx v1.70.0, frontend ([`shrpx.cc`](https://github.com/nghttp2/nghttp2/blob/v1.70.0/src/shrpx.cc#L1704-L1709)) | server (proxy) | 65 535 | 65 535 | as forwarded (`nghttp2_session_consume`) — one stream can take all of it |
| nghttpx v1.70.0, backend ([`shrpx.cc`](https://github.com/nghttp2/nghttp2/blob/v1.70.0/src/shrpx.cc#L1741-L1742)) | client (proxy) | 65 535 | 2³¹ − 1 | as forwarded |
| Kestrel, ASP.NET Core 10 ([`Http2Limits.cs`](https://github.com/dotnet/aspnetcore/blob/78e514743d906ef9e6dd48afe9f40544a230186d/src/Servers/Kestrel/Core/src/Http2Limits.cs#L19-L20)) | server | 768 KiB | 1 MiB | as the application reads ([`StreamInputFlowControl.cs`](https://github.com/dotnet/aspnetcore/blob/477666aeb0d813bda0d9d35cbd04514c7438a6a5/src/Servers/Kestrel/Core/src/Internal/Http2/FlowControl/StreamInputFlowControl.cs#L61-L77)) |
| `SocketsHttpHandler`, .NET 10 ([`Http2Connection.cs`](https://github.com/dotnet/runtime/blob/8a7187feef73b1fbea664903f1a95db5c4cba449/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/Http2Connection.cs#L99-L102)) | client | 64 KiB, scaled with the round trip up to 16 MiB | 64 MiB | on receipt |
| Envoy v1.39.1 ([`protocol.proto`](https://github.com/envoyproxy/envoy/blob/v1.39.1/api/envoy/config/core/v3/protocol.proto#L612-L631)) | proxy | 16 MiB — 64 KiB for an edge proxy ([`edge.rst`](https://github.com/envoyproxy/envoy/blob/v1.39.1/docs/root/configuration/best_practices/edge.rst#L24-L25)) | 24 MiB — 1 MiB for an edge proxy | |

The nghttp client takes the library's defaults; h2load, the benchmark, grants
2³⁰ − 1 for both. Two camps: servers, which hold what they are sent for many
peers, keep the connection window small and close to a stream's — Go and
nghttpx equal to it, with the stall described above; Kestrel gives a stream
three quarters of it, "able to use most (3/4ths) of the connection window by
itself", as its source says, which leaves the rest a quarter. Clients, which
decide themselves how many streams they open, make it large — 2.5 stream
windows for Chrome, 256 for Go, four or more for .NET. Hermod takes four on
both roles: one stalled stream per connection costs the server what it did
before, and the most a connection can be made to hold is four times that.

### Stream management & hardening (RFC 9113 §5)

- **Rapid Reset mitigation (CVE-2023-44487)** — a peer-reset-ratio guard.
- **CONTINUATION-flood mitigation (CVE-2024-27316)** — bounded header-block
  accumulation (at most twice `MAX_HEADER_LIST_SIZE` of compressed bytes, held
  until the block can be decoded) + a per-block CONTINUATION cap (server **and**
  client).
- PING/SETTINGS/PRIORITY_UPDATE flood counting.
- Stream-ID exhaustion handling (proactive GOAWAY + `REFUSED_STREAM`).
- Inbound + outbound `MAX_HEADER_LIST_SIZE` enforcement, on **both** roles. Each
  states its own limit, 32 KiB, in its connection preface, and holds the peer to
  the limit the peer stated — to none before that: the setting's initial value is
  unlimited (§6.5.2). Both used to keep their limit to themselves, and to hold a
  peer that stated none to their own. On the way out the limit is advisory in the
  RFC's words, but refusing early is strictly better than spending a round trip
  on headers that come back as a stream reset. Measured on the *uncompressed* list
  (`HTTP2HeaderList.UncompressedSize`, name + value + 32 per field), since the
  compressed size depends on whichever connection's dynamic table the block
  travels on. The client refuses a request before allocating its stream, so
  nothing declined consumes a stream ID.
- On the way in, each block's *decoded* list is measured against our limit,
  before anything else looks at it, and a list past it is answered on its stream
  (§10.5.1): the server answers a request with **431** without calling a handler
  (a body that follows is dropped, and the client asked to stop it with
  `RST_STREAM NO_ERROR` once the 431 is complete), resets a stream whose trailers
  are past it with `PROTOCOL_ERROR`; the client fails that one request, and resets
  its stream with `PROTOCOL_ERROR`. The block is decoded either way — HPACK has to
  see every block — and only one past twice the limit in compressed bytes ends the
  connection. This used to be a connection error at the limit itself, measured on
  the compressed bytes: every stream went down with the one that carried a large
  block, and a small block that decoded to a huge list passed (an HPACK bomb: 2000
  one-byte references to a 4000-byte cookie in the dynamic table reached the
  handler as one cookie of 8 MB).
- Per-stream `RST_STREAM` cancellation (a `CancellationToken` into the handler).
  The end of a connection resets every stream still open on it, as the peer's
  `RST_STREAM` would: each handler's token fires, its reads and writes fail and
  its tunnel reads end, rather than wait for a connection that is gone. The
  token of a reset stream fires on the thread pool: a handler that goes on from
  it never runs on the read loop or the writer loop, whichever made the reset,
  and holds up no other stream.
- A failing DATA writer loop is contained like the read loop: a stream error
  resets that one stream (`RST_STREAM INTERNAL_ERROR`) and the loop serves the
  others on; anything else ends the connection at once with `GOAWAY
  INTERNAL_ERROR`, rather than leave it taking requests whose bodies nothing
  would send.
- A write on a reset stream — a response body, a streamed chunk or its end, a
  tunnel write — fails at once with an `OperationCanceledException` carrying the
  stream's own token (the handler's), whether it was queued before the reset or
  comes after it, rather than wait for the connection to end; so does a write
  after both sides have ended the stream. A handler that fails on a reset stream
  gets no 500 either. The `CancellationToken` a write is given ends its wait,
  not the write.
- Nor does a reset stream get a header block (§5.1): a response's HEADERS,
  streamed or buffered, an interim 1xx, the 200 the server writes for a
  streaming handler that returns without headers, a 421/425 refusal, a CONNECT
  answer and trailers are not sent, and the header write fails as a body write
  there does, with the stream's own token. A handler that answers a reset stream
  is reported cancelled, and an accepted tunnel whose answer could not go out is
  not run. Whether to send is decided under the connection's write lock, right
  before the header list is HPACK-encoded: a block encoded and then dropped
  would leave the client's decoder a step behind for the rest of the connection.
- Nor does DATA the writer loop has already taken for a stream go out once the
  stream is reset: whether to send it is decided under the write lock too, so it
  goes out before the stream's `RST_STREAM` or not at all, and the send window
  taken for it goes back to the connection. A write — a response body, a
  streamed chunk or its end, a tunnel write or a tunnel's end — returns once its
  last frame is the next to go out, no longer as soon as the writer loop takes
  it off the queue: a handler that failed right after its last write could get
  its `RST_STREAM` out first, and that DATA followed on the closed stream.
- A read of a streamed request body on a reset stream fails likewise, with an
  `OperationCanceledException` carrying the stream's own token, whether the
  handler passed that token or not: a read waiting when the reset comes, and
  every read after it once the chunks that did arrive are read, rather than wait
  for a chunk that never comes. It never returns `null`, which would pass a
  truncated upload off as a whole one; a body the client ended before its reset
  still reads whole.
- DATA that reaches a streamed request body or a tunnel just as another task
  resets its stream — a failing handler, or the writer loop — is dropped, and its
  window given back to the connection at once, as for DATA on a closed stream
  (§6.9). The read loop used to write it into the channel the reset had
  completed, and that failure ended the whole connection.
- The window of streamed-body and tunnel DATA still unread when its stream is
  reset — by the client's `RST_STREAM`, a stream error, a failing handler or the
  writer loop — is given back to the connection once the reset is handled. It is
  withheld until the handler reads the bytes, and a handler that honours its
  token, or has failed, never does: a whole window's worth left unread used to
  close the connection's window for good. The bytes stay readable, in order, but
  reading them gives nothing back a second time. A stream-level `WINDOW_UPDATE`
  goes out only while the client may still send DATA on the stream, checked again
  under the write lock: never once the client has ended its side, and never
  after the stream's `RST_STREAM`.
- So is it once nothing reads the stream any more, without a reset: its handler
  has returned, or failed and been answered with a 500, a tunnel's handler has
  returned, or none was started — a streamed request refused with 421 or 425, a
  CONNECT refused. A handler that answered early and returned used to leave what
  it had not read, and all the client sent after, holding the connection's
  window for good. From then on, DATA on the stream is dropped and its window
  given back at once, and the body channel ends with an
  `InvalidOperationException` (a tunnel's simply ends), so a read left behind
  cannot pass a body cut short off as whole. Once the response is complete while
  the client may still send, the server asks it to stop with `RST_STREAM
  NO_ERROR` (§8.1), whichever comes second: the end of the reading, or the
  `END_STREAM` the writer loop sends.
- A handler's cancellation of its own — an `OperationCanceledException` that is
  neither its stream's reset nor the connection's end, a timeout of its own,
  say — is a failure like any other: answered with a 500, or with `RST_STREAM
  INTERNAL_ERROR` once the response has begun, and for a tunnel. It used to be
  taken for a cancellation and only reported, and the stream stayed open,
  unanswered, until the connection ended.
- A streamed response the handler has completed stands, though the handler
  fails after it: the failure is reported, and a client still sending is
  stopped with `RST_STREAM NO_ERROR`. It used to be reset with `INTERNAL_ERROR`,
  for which a client may discard a complete response (§8.1), and so was a
  stream both sides had ended, where nothing but PRIORITY may go (§5.1).
- A tunnel whose handler fails is reset with no `END_STREAM` before it: a
  tunnel's error is an `RST_STREAM` (§8.5). It used to be ended cleanly first,
  and reset on a closed stream once the client had ended its side too. Nor is
  a tunnel reset a second time when its handler fails after the client's
  `RST_STREAM` (§5.4.2).
- What the client sent before it read the server's own `RST_STREAM` — DATA,
  trailers and their CONTINUATION frames, still in flight when a handler failed
  or the read loop found a stream error — is minimally processed and discarded
  (§5.1): DATA is still window-accounted and trailers still HPACK-decoded, but
  nothing is answered. Each such frame used to draw another `RST_STREAM
  STREAM_CLOSED`; trailers went undecoded, and the next request died with
  `COMPRESSION_ERROR`; a split trailer block, or one on a stream pruned since,
  ended the connection. The newest `MaxConcurrentStreams` such stream IDs are
  kept past pruning. After the client's own `RST_STREAM`, or once it had ended
  its side, a frame is still a stream error `STREAM_CLOSED` — a header block
  decoded first.
- Closed-stream pruning; graceful shutdown (GOAWAY to every active connection).

### Parser fuzzing

- `ParserFuzzTests` fuzzes the two parsers a peer reaches before any
  authentication or application code runs: the frame header (§4.1) and the HPACK
  decoder (RFC 7541). Random blocks, and — for far deeper coverage — *mutations
  of a valid block* (bit flips, truncation, garbage runs, appended noise), which
  actually reach string literals, Huffman runs and dynamic-table updates instead
  of being rejected on the first octet.
- The invariant is not "it parses" but "it fails in the protocol's own
  vocabulary": a typed `HTTP2ConnectionException` / `HTTP2StreamException`, never
  an `IndexOutOfRangeException`, `ArgumentException` or `OverflowException`. On
  the wire that distinction is the difference between the correct GOAWAY code and
  an INTERNAL_ERROR with a logged surprise.
- Seeds are deterministic and a failure reports the seed plus the input as hex,
  so any finding replays exactly. The gate runs 20 000 iterations per case; set
  `HERMOD_FUZZ_ITERATIONS` to soak (500 000 per case ≈ 20 s).
- Alongside the random cases, the RFC 7541 §5–§6 MUST-errors are pinned
  explicitly: integer overflow and unterminated integers (§5.1), an explicitly
  encoded EOS and bad Huffman padding (§5.2), a zero index (§6.1), and an
  oversized dynamic-table update (§6.3) — including that *exactly* the limit is
  still legal.

### Slowloris / timeout hardening

- TLS-handshake, preface, SETTINGS-ACK, idle, and in-progress (partial
  frame/header-block) timeouts (`HTTP2Timeouts`) — reclaiming a peer that sends
  *too little*, complementing the flood defenses against *too much*.

### Client-side HTTP semantics (RFC 9110)

The server has carried the RFC 9110 semantics from the start; the client is
catching up. What it has so far:

- **Content coding, decode direction** (§8.4) — with `AutomaticDecompression`
  the client advertises `accept-encoding: br, gzip, deflate` and hands the
  caller the identity representation, reporting what it undid in
  `HTTP2Response.DecodedContentEncoding`. A caller's own `accept-encoding` is
  never widened behind their back (`identity` switches compression off for one
  request). Chained codings are undone right to left; an unknown coding leaves
  the message exactly as received rather than passing undecodable bytes off as
  identity. `HTTPContentCoding` holds both directions, so the codings we can
  produce and the codings we can consume cannot drift apart — and "deflate"
  reads both the zlib-wrapped (RFC 1950) and raw (RFC 1951) flavours the wire
  disagrees about.
- **Decompression-bomb bound** — `MaxDecodedBodySize` (16 MiB default) is
  enforced *during* decompression, not after: checking the output size
  afterwards would mean the bomb had already gone off. It bounds what decoding
  makes of a body; the body as it arrives, before any decoding, is bounded by
  `MaxResponseBodySize` (see [Flow control](#flow-control)).
- **Answering a 401** (§11) — with `Credentials` set, the client parses the
  `WWW-Authenticate` challenge, picks the strongest scheme it can answer
  (Digest > Bearer > Token > Basic — Basic last, since it hands the password
  over) and re-issues the request **once**. Nothing is sent preemptively, and
  because the retry re-sends the very same request, credentials cannot leak to
  an origin that did not ask for them. `HTTPClientAuthenticator` is the mirror
  of the `Auth/` schemes: they validate, it computes — one algorithm, one place,
  which matters most for Digest, where both ends must agree exactly. It is
  per-connection state because RFC 7616 requires the nonce count to increase
  while a nonce is reused.

- **Conditional requests** (§13) — `HTTP2ResponseHead` exposes the validators
  (`ETag`, `LastModified`, `Validator`, `AcceptsByteRanges`, `ContentRange`), and
  `HTTPValidators` builds and compares them: HTTP-date in both directions,
  entity-tag lists, and the strong/weak comparison rules. A client-built
  `if-none-match` / `if-modified-since` round-trips to a 304 from our own server.
- **Resumable download** (§14 + §13.1.5) — `DownloadAsync` writes a
  representation into a `Stream` and continues an interrupted transfer with
  `Range: bytes=<n>-` plus `If-Range`, so the *server* decides whether the two
  halves belong to the same representation: 206 to splice, 200 to start over
  (the stale prefix is truncated away). Built on the streaming response path
  deliberately — the buffered API discards a partial body, and you cannot resume
  a download whose received prefix you threw away. A **weak** entity-tag does not
  qualify as a resume guard: "semantically equivalent" is not enough to
  concatenate what may be different bytes. With no validator at all the failure
  propagates rather than returning a silently truncated file, and a 416 whose
  `Content-Range` says the resource is exactly as long as what we hold counts as
  complete. Content codings are kept out of it (`accept-encoding: identity`),
  since ranges over a compressed representation would mean splicing compressed
  fragments and decoding the seam.

- **Redirect following** (§15.4) — `MaxRedirects` above zero follows `Location`,
  resolving a relative reference against the request URI (RFC 3986 §5) and
  applying the asymmetric rewriting rules: **301/302** turn a POST into a GET,
  **303** turns everything except HEAD into a GET, **307/308** preserve method
  *and* body — which is the whole reason those two exist. A dropped body also
  drops the `content-length` that described it. 300 and 304 are not followable.
  `HTTP2Response.RedirectChain` records where the response actually came from.
  Following stops at the **origin boundary** — see below.

- **Content integrity** (RFC 9530) — `VerifyDigests` asks every request for a
  digest and checks the ones that come back. See the section below; the client
  half is `HTTP2Response.DigestVerification` and, for a spliced download,
  `HTTP2DownloadResult.DigestVerification`.

Still open on the client: a cookie jar.

**Why redirect following stops at the origin.** A connection speaks to the origin
it dialed, and pooling here is single-origin *by design*. Dialing a second origin
from inside `HTTP2ClientConnection` would quietly make it a multi-origin client,
contradicting that decision — so a cross-origin `Location` is handed back
unfollowed, 3xx and `Location` intact, for a layer that does own connection
creation. The same boundary is what makes automatic following safe alongside
`Credentials`: every followed hop is same-origin, so an `Authorization` header can
never travel to an origin that did not ask for it. Cross-origin following stays
open, and deliberately so: it is the multi-origin question, not a redirect
question.

`HTTPValidators` and `HTTPContentRange` are the direction-neutral primitives this
required — lifted out of `HTTPSemantics`, which the server still uses through
them, so precondition evaluation and precondition construction cannot drift
apart. `HTTPContentRange` also adds the parse direction the stack never had: the
server only ever formatted `Content-Range`.

### Digest fields (RFC 9530)

TLS protects the hop, not the object: it says nothing about what a gateway, a
cache, or a disk did to a representation along the way. `Content-Digest` and
`Repr-Digest` close that gap, and `HTTPDigest` implements both directions for
both roles.

The two fields answer different questions, which is why there are two:

- **`Content-Digest`** covers the octets *this message* carries. On a `206` that
  is the slice, not the resource.
- **`Repr-Digest`** covers the *selected representation* (RFC 9110 §8.1) —
  unaffected by `Content-Range`. It is the only thing that can verify a download
  assembled out of several range responses, because no single one of them
  carries a digest of the whole.

Both are computed **after** content coding, since representation data is defined
to be in its `Content-Encoding`. That fixes an ordering the client cannot get
wrong quietly: verification runs on the bytes as they arrived, *before*
`AutomaticDecompression` rewrites them. A test exists for nothing but that order.

Only `sha-256` and `sha-512` are computed. The registry's other entries (`md5`,
`sha`, `unixsum`, `unixcksum`, `adler`, `crc32c`) are deprecated or were never
collision-resistant, and a digest field is an integrity claim — honouring a
broken algorithm would make it a false one. `Want-Content-Digest` /
`Want-Repr-Digest` select among the two by preference (0 = unacceptable); a peer
that rules both out gets no digest rather than one it declined.

**Server** (`HTTPSemantics.Wrap(…, ContentDigests: true)`): every response that
carries content gets a `Content-Digest`; a `206` additionally gets the
`Repr-Digest` that makes the splice checkable. Bodiless responses (HEAD, 304,
412, 416) get neither, deliberately — with content coding in play we would have
to guess which encoding the corresponding 200 would have carried, and a digest of
the representation we did not send is a claim we cannot stand behind. In the
request direction, a `Content-Digest` on a QUERY is checked against the request
content and answered `400` if it disagrees.

**Client** (`HTTP2ClientOptions.VerifyDigests`): sends `Want-Content-Digest`,
verifies what comes back, and reports the outcome on
`HTTP2Response.DigestVerification`. `DownloadAsync` asks for `Want-Repr-Digest`
instead and hashes incrementally as it writes, so a resumed download is verified
end to end without re-reading the file — and a restart discards the hash along
with the bytes it belonged to.

A mismatch **throws** (`HTTPDigestMismatchException`) rather than being returned
as a flag: a caller who switched verification on did so precisely to not be
handed those bytes. Notably, that also means a mismatch is *not* a retryable
interruption — `DownloadAsync` excludes it from its resume filter, since the
whole representation arrived and was wrong, and retrying would only fetch the
same wrong bytes while masking the detection.

Everything else is reported rather than thrown, and
`HTTPDigestVerification` keeps the outcomes apart on purpose:
`NotPresent` / `Unsupported` / `Match` / `Mismatch`. Three of those four mean
nothing was checked. Collapsing "there was no digest" into a boolean `true` would
quietly turn an unverified body into a verified one, which is the one failure
this feature exists to prevent.

### Early data and 425 (RFC 8470)

TLS 1.3 lets a client put application data in its first flight, before the
handshake completes. Those octets carry no proof of freshness: an attacker who
captured them can send them again, and the server cannot tell the copy from the
original. Everything here follows from that one hazard — **replay**, not
eavesdropping.

**This stack terminates no early data.** `SslStream` exposes no 0-RTT API at all
— nothing to offer it with, nothing to accept it with, and no way to ask whether
bytes arrived that way. (Checked, not assumed: the type has `AllowTlsResume` for
session resumption and nothing whatsoever for early data.) On a connection we
terminate there is therefore no replay window, and the honest thing is to say so
rather than to implement a defence against a condition that cannot arise.

What *can* reach us is the other case the RFC defines: an intermediary. A CDN or
reverse proxy that accepted early data and forwarded the request onward must mark
it `Early-Data: 1` (§5.1). The origin behind it holds the risk without having
seen the handshake, and the field exists precisely so it can decide. Ignoring the
field is not neutral — it is silently accepting a replay the peer went out of its
way to warn about, which is what this stack used to do.

- **Server.** A flagged request is judged by `AcceptEarlyData`, defaulting to
  `HTTP2EarlyData.IsSafeToProcess`: safe methods pass, everything else is
  declined with **425 (Too Early)** and `Cache-Control: no-store`, so a refusal
  cannot outlive the reason for it. Safety, not idempotence, is the bar —
  replaying a `PUT` *after* a later request changed the resource undoes that
  change, so the idempotence guarantee (which is about repetition) does not cover
  reordering. Pass `_ => true` to accept the risk deliberately. Checked on the
  buffered *and* the streaming dispatch path; the latter never passes through the
  former, which is the same trap 421 fell into once.
- **Client.** A 425 says the request was not processed and should be repeated
  once the handshake has completed — which, on a connection we already own, it
  long since has. `SendRequestAsync` therefore repeats it exactly once, **dropping
  any `Early-Data` field**, since leaving it on would restate the very thing the
  origin refused. A second 425 is an answer, not a hint, and is handed back.

Put together, the two halves recover silently: our server declines the flagged
POST, our client repeats it clean, and the caller sees a 200. That is the
mechanism working — and it is also why the server-side tests have to go one layer
down to `StartRequestAsync` to observe the refusal at all.

### TLS profile (RFC 9113, Section 9.2)

- HTTP/2 over TLS 1.2 must not use a cipher suite from Appendix A, and an
  endpoint may answer one with `INADEQUATE_SECURITY` (§9.2.2). Both roles check
  the negotiated suite after the handshake — the server turns the connection
  down with its SETTINGS preface followed by `GOAWAY(INADEQUATE_SECURITY)`, the
  client refuses before sending its preface at all. (Detection rather than
  prevention: `CipherSuitesPolicy` would prevent it, but throws
  `PlatformNotSupportedException` on Windows.)
- `HTTP2CipherSuites` tests the two structural properties Appendix A enumerates
  — *ephemeral* key exchange and an *AEAD* cipher — instead of transcribing the
  ~300-entry table. Same verdict for every listed suite, and it cannot go stale.
  A suite the runtime cannot even name counts as permitted: Appendix A is a
  closed list, so anything registered after RFC 9113 is not on it.
- Overridable per role (`IsBlocklistedCipherSuite` on the server,
  `HTTP2ClientOptions.IsBlocklistedCipherSuite`) — §9.2.2 states the rejection as
  a MAY, so both a laxer and a stricter policy are legitimate.
- §9.2.1: renegotiation is disabled explicitly (`AllowRenegotiation = false`);
  TLS compression is never offered by .NET.

### Authoritative origins (421 + ORIGIN)

- A client may reuse an existing connection for *any* origin our certificate
  covers (§9.1.1, "connection coalescing"), so `:authority` is not necessarily
  the name the peer dialed. Requests naming an origin we are not authoritative
  for are answered **421 (Misdirected Request)** — a stream-level answer, so the
  connection stays usable for the origins we do serve.
- The default origin set is derived from the server certificate (SAN dNSNames
  with RFC 6125 wildcard matching, plus iPAddress SANs; the common name only for
  certificates carrying no SAN at all). Cleartext h2c has no certificate and so
  no basis to judge — it checks nothing unless given a predicate.
- Plain CONNECT is exempt: there `:authority` is the *tunnel target*, not the
  origin being addressed. Extended CONNECT (RFC 8441) is not exempt — there it
  means exactly what it means in an ordinary request.
- **ALTSVC frame** (RFC 7838): the neighbouring question — not "which origins do
  you serve" but "where else is this origin reachable". An alternative is *not* a
  redirect: it names another protocol/host/port for the **same** origin, so the
  authority in requests never changes and the 421 check above is unaffected. The
  server advertises via `AlternativeServices`; the client parses the `Alt-Svc`
  grammar (`h3=":443"; ma=3600; persist=1`, percent-encoded ALPN names, quoted
  alt-authorities, `clear` as a distinct "forget everything" signal) into
  `HTTP2ClientConnection.AlternativeServices`. §4's two shapes are enforced on
  receipt — an origin is required on stream 0 and forbidden on a request stream,
  and either mismatch means *ignore the frame*, since the RFC defines no error
  code for a bad ALTSVC. Recorded but not acted on: acting means dialling HTTP/3,
  which is a different transport and a different project (see "Explicitly out of
  scope").
- **ORIGIN frame** (RFC 8336): a server can state the set instead of leaving the
  client to infer it (`OriginSet` on `HTTP2Server`, sent right after the
  preface). An announced set also becomes the yardstick for the 421 check —
  having told the client what we serve, answering for something else would
  contradict our own announcement. The client exposes what it received as
  `HTTP2ClientConnection.OriginSet`, and ignores the frame on a non-zero stream
  (§2.1) or over h2c, where an unauthenticated peer's claim about its own
  identity is worth nothing (§2.4).

### ALPN: advertise only what you serve

`h2` is always offered. `http/1.1` is offered **only** when the application
supplied an `HTTP11Fallback` handler — otherwise this is an h2-only endpoint and
an http/1.1-only client fails ALPN negotiation outright.

That is the honest answer, and the previous behaviour was the worst of both:
`http/1.1` was advertised and then handed to a stub that wrote a fixed response
and closed. Offering a protocol you cannot serve is worse than not offering it,
because a client that *could* have spoken h2 may pick http/1.1 on the strength of
the offer and get nothing. (The stub also declared `Content-Length: 39` for a
38-byte body, so a client waited for a byte that only ever arrived as EOF.)

With a handler registered, the fallback receives the authenticated `SslStream`
positioned at the first application byte — an existing HTTP/1.1 pipeline takes it
as-is. A peer that offers **no** ALPN at all is routed there too: over TLS that
means it is not speaking h2 (RFC 9113 §3.2 requires ALPN for that), so it belongs
to the same handler. With neither ALPN nor a handler, the connection is closed
rather than left hanging.

### Observability (events + tracing)

The stack writes **nothing** to the console. It emits structured events through
`HTTP2EventSource` and spans through `HTTP2Diagnostics.ActivitySource`, and a
consumer decides what to do with them — both APIs are BCL, so this costs no
dependency, which matters when the alternative (a logging abstraction) would
have been the first thing to break the no-NuGet rule.

- **Events** (`EventSource` named `Vanaheimr-Hermod-HTTP2`): connection lifecycle
  including the negotiated ALPN, TLS version and cipher suite — parameters that
  were otherwise invisible after the handshake — plus connection/stream errors,
  peer resets, GOAWAY with its code, handler failures, and per-request
  method/path/status. Attach an `EventListener`, ETW, or an OpenTelemetry
  exporter.
- **Counters** (`dotnet-counters monitor --counters Vanaheimr-Hermod-HTTP2`):
  connections started, requests handled, streams reset, connection errors, and
  abuse defences fired. Created on first subscription, not at construction — an
  unobserved counter still costs a timer.
- **Spans** (`AddSource("Vanaheimr.Hermod.HTTP2")`): one per connection with a
  request span nested inside it, tagged per the OpenTelemetry semantic
  conventions (`http.request.method`, `url.path`, `http.response.status_code`,
  `network.protocol.version`), so an exporter needs no translation layer. The
  nesting is the point: it makes "this slow request shared a connection with
  forty others" visible.
- **The abuse defences finally report.** Rapid Reset, CONTINUATION floods,
  unproductive-frame floods and timeout kills previously detected their
  conditions and then told nobody but stdout — unobservable in exactly the
  situations they exist for.
- **Unobserved, it costs nothing**, and that is asserted rather than claimed:
  `StartActivity` returns null with no listener, and the `EventSource` reports
  itself disabled so payloads are never built. There is a test for both.

The `Demo` shows the seam from the consumer's side — a ~30-line `EventListener`
that prints, which is roughly what the library used to hardcode.

### Testable time (TimeProvider)

- Every time source in the stack is injectable via the BCL
  `System.TimeProvider`: `HTTP2ClientOptions.TimeProvider` drives the client's
  keepalive pacing, liveness tracking, PING-ACK timeout and pool back-off;
  `HTTP2Timeouts.TimeProvider` schedules all server timeouts (frame-read
  timeouts run on a `CreateTimer` that cancels the read's linked CTS);
  `HTTP2CachingClient` and `DigestAuthenticationScheme` take an optional
  `TimeProvider` for RFC 9111 age math and nonce issue/expiry.
- The default is `TimeProvider.System` everywhere — without injection the
  behavior is unchanged. With a fake clock, clock-dependent behavior becomes
  deterministic: `DigestNonceExpiry_FakeClock` proves a five-minute nonce
  lifetime in ~40 ms, using a minimal hand-rolled `TimeProvider` subclass
  (only `GetUtcNow()` overridden — no test-clock package needed).

### Prioritization (RFC 9218)

- `SETTINGS_NO_RFC7540_PRIORITIES=1` advertised (RFC 7540 priority is
  parsed-and-ignored, per §5.3.1 self-dependency validation only).
- The `priority` request/response header (urgency + incremental) and
  `PRIORITY_UPDATE` frame — parsed leniently (bad hint → default, not an error).
- `PRIORITY_UPDATE` goes from client to server only (§7.1): a client that
  receives one ends the connection with `GOAWAY PROTOCOL_ERROR`. It used to
  ignore the frame, as one of a type it does not handle.
- A **priority-aware multiplexed writer**: a single per-connection writer loop
  schedules DATA by urgency → non-incremental-first → round-robin fairness
  (`HTTP2SendOrder`, shared by both roles).
- Client emits the signals too (`Priority` param on requests, streamed requests,
  tunnels and WebSockets; `UpdatePriorityAsync` on the connection, a tunnel or
  a `WebSocketConnection`), and **sends its own DATA by them**: a writer loop of
  its own, the mirror of the server's, sends every request body, tunnel write
  and END_STREAM on DATA by the priority the stream's request asked the server
  for — the `Priority`, a `priority` field among the caller's headers, or a later
  `UpdatePriorityAsync`. A message on an urgent stream waits for the DATA frame
  being written, not for what a bulk upload on another stream still has queued.
  RFC 9218 orders the server's sending; that the client orders its own by the
  same signal is this stack's choice. The writer loop orders what it hands to
  the transport, no more: bytes already in the TLS and TCP send buffers go out
  first, whatever their stream.

### CONNECT & tunneling

- Plain CONNECT (RFC 9113 §8.5) — `:authority` present, `:scheme`/`:path` absent.
- Extended CONNECT (RFC 8441) — `:protocol` + mandatory `:scheme`/`:path`.
- `HTTP2Tunnel` (server) / `HTTP2ClientTunnel` (client): a raw, flow-controlled,
  transport-agnostic byte tunnel behind the `IHTTP2Tunnel` interface.
- A tunnel, and a WebSocket over one, is prioritized like any other stream
  (RFC 9218 §11: the scheduling guidance applies to the frames of a CONNECT
  stream): `OpenTunnelAsync`/`OpenWebSocketAsync(…, Priority:)` send the
  `priority` field with the CONNECT, `HTTP2ClientTunnel.UpdatePriorityAsync` and
  `WebSocketConnection.UpdatePriorityAsync` change it later, and both ends' writer
  loops send the tunnel's bytes by it.

### WebSocket (RFC 6455 + RFC 7692)

- Full framing: masking (direction-aware — client masks, server doesn't),
  opcodes, fragmentation reassembly, automatic ping→pong, close handshake.
- Strict UTF-8 validation of text (§8.1, incremental across fragments) and
  close-frame validation (§5.5 / §7.4.1).
- **permessage-deflate** (RFC 7692) in no-context-takeover mode, negotiated over
  both the Autobahn HTTP/1.1-Upgrade path and the production HTTP/2 CONNECT path.
- Server **and** client roles (`WebSocketRole`), over `IHTTP2Tunnel` on both
  ends.
- **Bounded messages** — `MaxMessageSize` (constructor, or
  `OpenWebSocketAsync(…, MaxMessageSize:)`; 64 MiB default, as the HTTP/1.1
  stack's `MaxTextMessageSizeIn`/`MaxBinaryMessageSizeIn`) bounds a received
  message across all its fragments, and under permessage-deflate both as it
  travels and once inflated. Past it the connection fails with 1009 — at the
  header of the frame that would go past, before its payload is read, and
  *during* inflation, at the first byte past the limit, so a deflate bomb never
  goes off. A single frame stays capped at 16 MiB (1002).

### HTTP semantics (RFC 9110)

- **Methods**: GET/HEAD (shared path), OPTIONS (204 + `Allow`), 405 for
  unsupported (with `Allow`).
- **Conditional requests** (§13): `If-Match`/`If-None-Match` (strong/weak),
  `If-Modified-Since`/`If-Unmodified-Since`, `If-Range`, in the §13.2.2
  precedence order → 304 / 412.
- **Range** (§14): single-range → 206 + `Content-Range`; **multi-range →
  `multipart/byteranges`**; unsatisfiable → 416; `Accept-Ranges: bytes`. A
  `MaxRanges` cap guards against range-amplification.
- **Proactive content negotiation** (§12): `Accept`, `Accept-Encoding`,
  `Accept-Language` with `q`-values, `Vary`, and the 406-vs-default policy.
- **On-the-fly content coding**: opt-in gzip / brotli / deflate compression
  (weakens the ETag, updates `Vary`).
- **QUERY** (RFC 10008): a safe/idempotent/cacheable body-carrying read; runs the
  same representation pipeline as GET (ETag/304, negotiation), with
  `Content-Location` and the §4 `Content-Type`-required rule.

### Authentication (RFC 9110 §11)

- A scheme-agnostic framework: reads `Authorization`, dispatches to a registered
  scheme, answers 401 with one `WWW-Authenticate` challenge per scheme. Never
  validates itself — each scheme defers to an app-supplied validator, so `Core`
  carries no credential store.
- **Basic** (RFC 7617), **Bearer** (RFC 6750), **Digest** (RFC 7616 —
  challenge-response, SHA-256 + MD5-interop, stateless HMAC nonce, `qop=auth`,
  constant-time compare), **Token** (non-standard — Rails/GitHub-style, bare +
  parameterized forms).
- **mutual TLS (mTLS)** — a separate transport-layer mechanism: server requires
  + validates a client cert, surfaces the subject to handlers; the client can
  present one.

### Caching (RFC 9111)

- Direction-neutral caching *logic* in `Core` (Cache-Control grammar, age /
  freshness §4.2, storability §3, revalidation, `Vary` keying §4.1,
  private/shared §3.5) + a client-side cache (`HTTP2CachingClient`) that serves
  fresh hits with no round trip, revalidates stale entries conditionally, serves
  stale within `max-stale`/`stale-while-revalidate`, returns 504 for
  `only-if-cached` misses, and invalidates on unsafe methods (§4.4).

### Streaming, trailers & gRPC

- A streaming seam alongside the buffered handler: incremental request-body read
  + response-body write + **trailers in both directions** (RFC 9113 §8.1) —
  server and client (`HTTP2ClientStream`).
- **Trailers, symmetrically.** The server sends response trailers through
  `IHTTP2ResponseStream.CompleteAsync(Trailers)` and surfaces inbound request
  trailers on `IHTTP2RequestStream.Trailers`; the client is now the exact mirror
  — `HTTP2ClientStream.CompleteRequestAsync(Trailers)` ends the request with a
  trailing HEADERS block instead of an empty END_STREAM DATA frame, and reads
  response trailers off `GetTrailersAsync()` (or `HTTP2Response.Trailers` on the
  buffered path). The validation rules — no pseudo-header fields, lowercase names
  — live in `Core` as `HTTP2Trailers`, so the two directions cannot drift apart,
  and a bad list throws at the call that made it rather than earning a remote
  stream reset. A trailer-only request (no DATA at all) is legal and works.
  Encoding happens under the same lock that orders request HEADERS: the HPACK
  dynamic table is stateful, so a trailer block encoded between another request's
  encode and its write would desynchronize the peer's decoder.
- **No trailers on a reset stream** (§5.1): `CompleteRequestAsync(Trailers)` on
  a stream the server has reset sends nothing and fails as the response side
  does, with an `HTTP2StreamException` carrying the reset's error code. That
  holds for a reset after a complete response too, which is how a server may stop
  the rest of an upload it no longer needs (`RST_STREAM NO_ERROR`, §8.1). The
  client used to ignore such a reset. It now closes the stream: the rest of the
  upload stays unsent, a write waiting for window ends, and the stream no
  longer counts against `MAX_CONCURRENT_STREAMS`; the response stands. Whether to
  send the trailers is decided under the lock that orders request HEADERS,
  before they are HPACK-encoded: a block encoded and then dropped would leave the
  server's decoder a step behind for the rest of the connection.
- **No DATA on a reset stream** either (§5.1): a write (`WriteAsync` of an
  `HTTP2ClientStream` or an `HTTP2ClientTunnel`), the end of a request without
  trailers or of a tunnel (`CompleteRequestAsync()`, `CloseAsync()`) and a
  buffered request's body send nothing once the server has reset the stream. The
  calls fail as the trailers do, with an `HTTP2StreamException` carrying the
  reset's error code: at once, when the reset comes while they wait in the
  stream's queue, or once the writer loop has the write lock for them. Whether
  to send is decided under that lock, right before the write, since the read
  loop handles `RST_STREAM` without it. The end used to go out on the reset
  stream regardless, and so did a write that had taken its window before the
  reset; both reported success, as did a write that met the reset and sent
  nothing. A frame kept off the wire gives its send window back to the
  connection. A buffered body stops quietly: the reset has decided its exchange
  already, as a failure, a retry on a new stream, or a complete response that
  stands. `DownloadAsync` reads such a response even when ending its request
  meets the `RST_STREAM NO_ERROR` that followed it.
- **Nothing after our own end** (§5.1): a second `CompleteRequestAsync()` or
  `CloseAsync()` sends nothing and returns, and a write or trailers after the end
  fail at once with an `InvalidOperationException`. A second END_STREAM, and a
  write after the end, used to go out onto the stream half-closed (local).
- **Writes are queued** for the connection's writer loop (see Prioritization).
  A write returns once its last frame is the next to go out; the end of a
  request — on DATA or as trailers — goes out behind every write before it. The
  `CancellationToken` a write is given ends its wait, not the write: what it
  queued still goes out, in order. It used to be ignored, and trailers went out
  ahead of a write still waiting for window, whose DATA then followed the
  END_STREAM.
- **Giving a stream up.** The client gives back the stream window of a tunnel or
  a streamed response as the application reads it (see
  [Flow control](#flow-control)), so one nobody reads to its end keeps the
  server waiting, and its stream slot, until the connection ends.
  `HTTP2ClientStream` and `HTTP2ClientTunnel` are `IAsyncDisposable`:
  `DisposeAsync` resets the stream with `RST_STREAM CANCEL` unless both sides
  have ended it or it was reset — no `RST_STREAM` on a closed stream, none in
  answer to the server's — which frees its slot, and drops what was not read.
  A read or a write waiting on it then fails with an `ObjectDisposedException`,
  as does every call after; a response head or trailers that had arrived stay.
  The reset is made under the write lock, right before its `RST_STREAM` goes
  out: no `WINDOW_UPDATE` or DATA of the stream follows it, and the request
  given the slot sends its `HEADERS` after it. A tunnel whose `OpenTunnelAsync`
  was cancelled before the server answered is reset the same way, as nobody
  would hold it, and `DownloadAsync` gives up a response it does not read to its
  end — an error page, or a body whose writing failed.
- **gRPC** runs over the stack (unary, server-streaming, client-streaming, bidi)
  with `grpc-status` in trailers — verified against the real `Grpc.Net.Client`,
  with **zero gRPC-specific production code**.

### 1xx interim responses

- Automatic **`100 Continue`** (server) for `Expect: 100-continue`.
- Handler-driven **103 Early Hints** (RFC 8297).
- Client surfaces interim responses on `HTTP2Response.InformationalResponses`.

### Client features

- Full client-side multiplexing; flow-control receive replenishment; priority
  signaling, and a priority-aware DATA writer loop for what the client sends
  (see Prioritization).
- **Robustness**: REFUSED_STREAM auto-retry, `MAX_CONCURRENT_STREAMS` gating
  (queue, don't fail), GOAWAY/exhaustion → retry-safe
  `HTTP2RequestNotProcessedException`, PING keepalive / dead-connection
  detection, client-side flood bounds. No new stream after a `GOAWAY` (§6.8): a
  request started then, or waiting for a stream slot when it comes, fails at once
  with `HTTP2RequestNotProcessedException`. A stream the `GOAWAY` leaves
  unprocessed, above its last-stream-id, is closed at once, as though it had
  never been opened, sending nothing: its request fails as before, a request body
  still being sent there stops rather than go out to a server that ignores it, a
  write fails with an `OperationCanceledException`, and an accepted tunnel — only
  a server that breaks §6.8 leaves one above its last-stream-id — reads its end,
  `null`, rather than wait until the whole connection ends.
- **The writer loop's failures are contained** as the server's are: a stream
  error — DATA after our own END_STREAM, from a write racing the end of its
  request — resets that one stream (`RST_STREAM INTERNAL_ERROR`; its writes and
  its response side fail with that code) and the loop serves the others on;
  anything else ends the connection at once with `GOAWAY INTERNAL_ERROR`, and
  every request on it fails with the reason. The connection's own loops — the
  read loop, the writer loop, the keepalive — cancel its token with
  `CancelAsync()`: whatever waited with it goes on on the thread pool, not inside
  the loop that ended the connection, and the connection's end, which a pool
  waits for, waits for none of it.
- **The end of a connection resets every stream still open on it**, as a server
  connection's end does, however it ends: the server goes away, the keepalive
  gets no answer, `CloseAsync`, or a failed writer loop. An accepted CONNECT
  tunnel's `ReadAsync` returns `null`, as after the server's `RST_STREAM`, and so
  does a client WebSocket's `ReceiveAsync`, where both used to wait for good on a
  connection that was gone — an OCPP charging station never learned that it had
  lost its backend. Requests still waiting for their answer fail as before, and
  a write still queued then, or made after, fails with an
  `OperationCanceledException`, as before: the end of a connection is no reset
  with an error code, which a write would report as an `HTTP2StreamException`.
  The reset sends nothing, and an ended connection counts no stream as active.
- **Every end of the read loop fails what is left on the connection**, also one
  that comes between two frames: `CloseAsync`, or the token the connection was
  made with, while the loop handles a frame, or while it takes one from a read
  that does not look at the token — `SslStream` hands out what it has decrypted
  already, whatever the token says. The loop used to stop at its condition then,
  with no exception, and failed nothing. A buffered request failed all the same,
  as it waits with the connection's token, but a streamed response waits with
  its caller's alone: its `GetResponseAsync`, `ReadAsync` and `GetTrailersAsync`
  waited for good. They fail now with an `OperationCanceledException`, as when
  the end finds the loop in a read.
- **Slots and windows follow the stream**: the `MAX_CONCURRENT_STREAMS` gate,
  and `AvailableStreamSlots` for the pool, count the streams that are open or
  half-closed (§5.1.2), as the stream allocator and the server do, not the
  requests still waiting for a response. A server may answer before the request
  body is all sent (§8.1): the stream then keeps its slot until the body's
  `END_STREAM` and still takes the server's `WINDOW_UPDATE`s, so the upload runs
  to its end and the next request waits for the slot rather than fail. A rejected
  CONNECT ends its stream (`END_STREAM`, or `RST_STREAM CANCEL` while the
  rejection has a body to come), and a tunnel closes once both sides have ended.
- **Connection errors are reported to the server** (§5.4.1): one the client
  finds in what the server sends — a frame the server must not send, a
  malformed frame, a setting out of range — ends the connection with a `GOAWAY`
  of its code, the error's message as debug data, where the client used to end
  it without a word. The `GOAWAY` is the last frame the client sends. It gets a
  second, on the options' `TimeProvider`, to get past a write in progress, and
  is given up after that, so that a write that never ends cannot hold up the
  connection's end, which `Closed` and a pool wait for.
- **What waits on the server fails with the connection error**: a request in
  flight, a tunnel being opened and `StartAsync` fail with the
  `HTTP2ConnectionException` the connection ended over. They used to fail with
  "A task was canceled.": the read loop fails them with the error and then
  cancels the connection, and the cancellation reached their waits first.
- **The client closes its TCP connection when the connection ends**: after
  `CloseAsync`, after a connection error's `GOAWAY` (§5.4.1), after the
  keepalive's teardown and after the server closed its side, and when
  `ConnectAsync` fails once the socket is open (a TLS handshake, ALPN, or a
  start that fails). It used to leave the socket to the GC, in `CLOSE_WAIT` once
  the server had closed: cancelling a pending read does not close a socket.
  After a connection error's `GOAWAY` it reads and discards what the server
  still sends, for up to 250 ms, as the server does, so that the close is a FIN
  behind the `GOAWAY`, not a reset that may discard it. `CloseAsync` returns once
  the connection has ended and the socket is closed, and its `GOAWAY NO_ERROR`
  gets the same second as a connection error's: behind a write that never
  ended, `CloseAsync` used not to return. A connection built on a stream of
  your own (`new HTTP2ClientConnection(stream)`) leaves it open, as before;
  `OwnsTransport: true` hands it over to be closed.
- **`HTTP2ClientPool`**: a single-origin pool that keeps N warm connections
  (default 4), routes to the least-loaded, transparently fails over
  not-processed requests, and self-heals dead connections in the background.

### Transports

- TLS `h2` (ALPN, TLS 1.2/1.3), with optional mTLS.
- Cleartext `h2c` (prior knowledge) — server and client.

---

## Non-standard extensions supported

These are widely used but are **not** IETF standards; they're supported because
they're common in the wild:

- **gRPC** — the de-facto RPC protocol on HTTP/2 (length-prefixed messages,
  `application/grpc`, `grpc-status` trailers). Not an RFC.
- **Token authentication** — Rails' `ActionController::HttpAuthentication::Token`
  and GitHub-style `Authorization: token …` (the `draft-hammer-http-token-auth`
  I-D expired).

## Security hardening summary

| Threat | Defense |
|---|---|
| HTTP/2 Rapid Reset (CVE-2023-44487) | Peer-reset-ratio guard → `GOAWAY ENHANCE_YOUR_CALM` |
| CONTINUATION flood (CVE-2024-27316) | Header buffer bounded at twice `MAX_HEADER_LIST_SIZE` + per-block CONTINUATION cap (both roles) |
| PING / SETTINGS / PRIORITY_UPDATE floods | Unproductive-frame counting |
| Slowloris (trickle / withhold) | Handshake / preface / idle / in-progress / SETTINGS-ACK timeouts |
| Memory exhaustion by fast producer | Consumption-driven backpressure (at most `ConnectionWindowSize` held per connection) + bounded buffered body |
| One stalled stream holding up the connection | Connection window four stream windows; owed window returned once the peer has no more left |
| Stream-ID exhaustion | Proactive GOAWAY + `REFUSED_STREAM` |
| Oversized header lists, HPACK bombs | `MAX_HEADER_LIST_SIZE` stated, and held to the *decoded* list: 431 / stream reset inbound, refusal outbound (both roles) |
| Range amplification | `MaxRanges` cap on a byte-range set |
| Weak TLS 1.2 cipher suites | RFC 9113 Appendix A check → `GOAWAY INADEQUATE_SECURITY` |
| Decompression bombs | `MaxDecodedBodySize`, enforced *during* decode |
| Credential leakage on retry | 401 answered only to the origin that challenged, once, never preemptively |
| Malformed input reaching unhandled code | Parser fuzzing: every rejection must be a typed protocol error |
| Answering for a foreign origin | `:authority` checked against the certificate / Origin Set → 421 |
| Credential timing oracles | Constant-time compare in Digest (`FixedTimeEquals`) |

## Explicitly out of scope

- **Server push** (`PUSH_PROMISE` outbound) — deprecated; we advertise
  `ENABLE_PUSH=0` and reject inbound pushes.
- **RFC 7540 priority** (stream dependencies/weights) — superseded by RFC 9218;
  parsed-and-ignored (only structural self-dependency is validated).
- **RFC 7540 `Upgrade: h2c`** — removed in RFC 9113 §3.1; only prior-knowledge
  h2c is implemented.
- **`Accept-Charset`** — deprecated in RFC 9110 §12.5.2.
- **Multi-origin connection pooling** — the pool is single-origin by design.
- **HTTP/3** (QPACK + H3 framing) — a different transport sharing only the
  version-independent HTTP semantics with this stack, which is precisely why
  `Core` was cut the way it is. It is not a future track here: it lives in the
  sibling project
  **[HTTP/3 Conformance Tests](https://github.com/Vanaheimr/HTTP3ConformanceTests)**.
  Its transport does now live in this repository, though: QUIC and the TLS 1.3
  handshake it needs (RFC 9000/9001/9002, RFC 8446) sit under `Hermod/QUIC`, with
  their tests under `HermodTests/QUIC`, and HTTP/3 Conformance Tests consumes them
  here. So the line is between *transport* and *HTTP mapping*, not between the two
  repositories — nothing of HTTP/3 itself is implemented here.

---


## Interop reference peers (test-only)

| Peer | Exercises |
|---|---|
| .NET `HttpClient` (strict) | our **server** — semantics, auth, conditional/range, compression, interim, HPACK decode of our encoder |
| .NET **Kestrel** | our **client** — HPACK decode, flow control, h2c |
| **curl** (nghttp2, Linux) | our server over both `h2` and `h2c` |
| **`Grpc.Net.Client`** | our server + streaming seam — all four gRPC call types |

---


## References

- RFC 9113 — HTTP/2
- RFC 7541 — HPACK: Header Compression for HTTP/2
- RFC 7301 — TLS Application-Layer Protocol Negotiation (ALPN)
- RFC 9218 — Extensible Prioritization Scheme for HTTP
- RFC 8441 — Bootstrapping WebSockets with HTTP/2
- RFC 8336 — The ORIGIN HTTP/2 Frame
- RFC 6125 — Representation and Verification of Domain-Based Application Service Identity
- RFC 6455 — The WebSocket Protocol
- RFC 7692 — Compression Extensions for WebSocket (permessage-deflate)
- RFC 9110 — HTTP Semantics
- RFC 9111 — HTTP Caching
- RFC 9530 — Digest Fields
- RFC 8470 — Using Early Data in HTTP
- RFC 7617 — The 'Basic' HTTP Authentication Scheme
- RFC 6750 — OAuth 2.0 Bearer Token Usage
- RFC 7616 — HTTP Digest Access Authentication
- RFC 8297 — An HTTP Status Code for Indicating Hints (103 Early Hints)
- RFC 10008 — The HTTP QUERY Method
- RFC 5861 — HTTP Cache-Control Extensions for Stale Content
- RFC 8941 — Structured Field Values for HTTP
- RFC 4647 — Matching of Language Tags

