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
> WebSocket (RFC 6455) tunneling, RFC 9218 priority-aware response
> scheduling, streaming request/response bodies with response trailers
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

The PowerShell/Docker harnesses that produced those two numbers did not come
across when the stack was vendored into Hermod. They stand as results that were
achieved, not as something this repository can currently reproduce; re-running
either means pointing h2spec or Autobahn at a demo host yourself.

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

        // RFC 9110 §8.4 / §11 — the client half of the semantics the server has
        // had all along. Both off by default: they change what goes out on the
        // wire and what comes back, so the caller opts in.
        AutomaticDecompression   = true,                      // ask for br/gzip/deflate, decode transparently
        MaxDecodedBodySize       = 16 * 1024 * 1024,          // and refuse a decompression bomb
        Credentials              = HTTPClientCredentials.UserNameAndPassword("alice", "secret"),
    });

// If the server announced one, its Origin Set (RFC 8336) is here — null until an
// ORIGIN frame arrives, and never populated over cleartext h2c.
Console.WriteLine(conn.OriginSet is null ? "no ORIGIN frame" : String.Join(", ", conn.OriginSet));
```

Concurrent requests beyond the server's `MAX_CONCURRENT_STREAMS` queue (rather
than fail), and a request the server provably never processed (a
`REFUSED_STREAM` past the retry budget, or a stream above a `GOAWAY`'s
last-stream-id) surfaces as `HTTP2RequestNotProcessedException` — a signal it's
safe to retry on a fresh connection.

The client can also open CONNECT tunnels and WebSockets (RFC 9113 §8.5 / RFC
8441 / RFC 6455), the mirror of the server's tunneling — both ends of the wire
hand-rolled:

```csharp
// plain CONNECT — a raw bidirectional byte tunnel
var tunnel = await conn.OpenTunnelAsync("proxy.target:443");
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

For full-duplex request/response streaming — the enabler for client-streaming and
bidirectional gRPC — `StartStreamingRequestAsync` returns a handle whose request
body is written incrementally while the response is read incrementally, both at
once:

```csharp
var s = await conn.StartStreamingRequestAsync("POST", "https", "localhost:8443", "/svc.Greeter/Bidi",
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
| **9218** | Extensible Prioritization Scheme | ✅ | `priority` header, `PRIORITY_UPDATE`, `SETTINGS_NO_RFC7540_PRIORITIES`; priority-aware writer. Both roles emit + the server acts on it. |
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
- `content-length` vs. DATA-length enforcement (§8.1.2.6).
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

### Flow control

- Per-stream and connection-level windows; signal-based send-window reservation
  (no polling).
- **WINDOW_UPDATE batching** (replenish once per half-window, not per DATA
  frame) + larger default windows (1 MiB stream + connection).
- **Consumption-driven backpressure**: for streaming/tunnel bodies the receive
  window is returned only as the *application* reads, so a slow consumer forces
  the peer to stop — the window *is* the memory bound.
- Bounded buffered request body (`MaxRequestBodySize`, default 16 MiB).
- Padding counted against flow control (§6.1); closed-stream DATA still
  window-accounted (§6.9); cookie-crumb reassembly (§8.2.3).

### Stream management & hardening (RFC 9113 §5)

- **Rapid Reset mitigation (CVE-2023-44487)** — a peer-reset-ratio guard.
- **CONTINUATION-flood mitigation (CVE-2024-27316)** — bounded header-block
  accumulation + a per-block CONTINUATION cap (server **and** client).
- PING/SETTINGS/PRIORITY_UPDATE flood counting.
- Stream-ID exhaustion handling (proactive GOAWAY + `REFUSED_STREAM`).
- Inbound + outbound `MAX_HEADER_LIST_SIZE` enforcement, on **both** roles: the
  limit is advisory in the RFC's words but refusing early is strictly better than
  spending a round trip on headers that come back as a stream reset. Measured on
  the *uncompressed* list (`HTTP2HeaderList.UncompressedSize`, name + value + 32
  per field), since the compressed size depends on whichever connection's dynamic
  table the block travels on. The client refuses a request before allocating its
  stream, so nothing declined consumes a stream ID.
- Per-stream `RST_STREAM` cancellation (a `CancellationToken` into the handler).
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
  afterwards would mean the bomb had already gone off. The client-side
  counterpart of the server's `MaxRequestBodySize`.
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
- A **priority-aware multiplexed writer**: a single per-connection writer loop
  schedules DATA by urgency → non-incremental-first → round-robin fairness.
- Client emits the signals too (`Priority` param, `UpdatePriorityAsync`).

### CONNECT & tunneling

- Plain CONNECT (RFC 9113 §8.5) — `:authority` present, `:scheme`/`:path` absent.
- Extended CONNECT (RFC 8441) — `:protocol` + mandatory `:scheme`/`:path`.
- `HTTP2Tunnel` (server) / `HTTP2ClientTunnel` (client): a raw, flow-controlled,
  transport-agnostic byte tunnel behind the `IHTTP2Tunnel` interface.

### WebSocket (RFC 6455 + RFC 7692)

- Full framing: masking (direction-aware — client masks, server doesn't),
  opcodes, fragmentation reassembly, automatic ping→pong, close handshake.
- Strict UTF-8 validation of text (§8.1, incremental across fragments) and
  close-frame validation (§5.5 / §7.4.1).
- **permessage-deflate** (RFC 7692) in no-context-takeover mode, negotiated over
  both the Autobahn HTTP/1.1-Upgrade path and the production HTTP/2 CONNECT path.
- Server **and** client roles (`WebSocketRole`), over `IHTTP2Tunnel` on both
  ends.

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
- **gRPC** runs over the stack (unary, server-streaming, client-streaming, bidi)
  with `grpc-status` in trailers — verified against the real `Grpc.Net.Client`,
  with **zero gRPC-specific production code**.

### 1xx interim responses

- Automatic **`100 Continue`** (server) for `Expect: 100-continue`.
- Handler-driven **103 Early Hints** (RFC 8297).
- Client surfaces interim responses on `HTTP2Response.InformationalResponses`.

### Client features

- Full client-side multiplexing; flow-control receive replenishment; priority
  signaling.
- **Robustness**: REFUSED_STREAM auto-retry, `MAX_CONCURRENT_STREAMS` gating
  (queue, don't fail), GOAWAY/exhaustion → retry-safe
  `HTTP2RequestNotProcessedException`, PING keepalive / dead-connection
  detection, client-side flood bounds.
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
| CONTINUATION flood (CVE-2024-27316) | Bounded header buffer + per-block CONTINUATION cap (both roles) |
| PING / SETTINGS / PRIORITY_UPDATE floods | Unproductive-frame counting |
| Slowloris (trickle / withhold) | Handshake / preface / idle / in-progress / SETTINGS-ACK timeouts |
| Memory exhaustion by fast producer | Consumption-driven backpressure + bounded buffered body |
| Stream-ID exhaustion | Proactive GOAWAY + `REFUSED_STREAM` |
| Oversized header lists | Inbound + outbound `MAX_HEADER_LIST_SIZE`, both roles |
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
  sibling project **`HTTP3FromScratch`**.
  Its transport does now live in this repository, though: QUIC and the TLS 1.3
  handshake it needs (RFC 9000/9001/9002, RFC 8446) sit under `Hermod/QUIC`, with
  their tests under `HermodTests/QUIC`, and `HTTP3FromScratch` consumes them from
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

