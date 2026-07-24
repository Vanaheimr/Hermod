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
explicitly out of scope](#explicitly-out-of-scope) are all below. See
[`docs/BUILD_LOG.md`](docs/BUILD_LOG.md) for the full chronological build history.

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

The interop + attack harnesses live under [`tests/`](tests/). Run the whole
suite (builds, starts the demo host, drives every harness) with:

```powershell
powershell -ExecutionPolicy Bypass -File tests/run-tests.ps1
```

Current status: **70/70 harness runs pass**, and the stack scores **146/146 on
[h2spec](https://github.com/summerwind/h2spec)** (the canonical HTTP/2
conformance suite) over *both* the TLS and cleartext-h2c listeners. Reproduce
the h2spec run with a single command —

```powershell
pwsh tests/h2spec.ps1   # builds, starts the demo, runs h2spec on both transports
```

— see [`tests/TestingAgainst_h2spec.md`](tests/TestingAgainst_h2spec.md) for the
full h2spec walkthrough, [`tests/README.md`](tests/README.md) for the harness
layout, and [`docs/BUILD_LOG.md`](docs/BUILD_LOG.md) for the conformance breakdown.

The WebSocket framing (RFC 6455) likewise passes **517/517** cases of the
canonical [Autobahn TestSuite](https://github.com/crossbario/autobahn-testsuite)
— the full suite, including `permessage-deflate` (RFC 7692) compression —
`pwsh tests/autobahn.ps1` / `tests/autobahn.sh` (Docker), with the critical
cases also pinned in the committed `h2wsconformance` harness (no Docker needed);
see [`tests/TestingAgainst_Autobahn.md`](tests/TestingAgainst_Autobahn.md).

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
the server. This seam is enough to serve real **gRPC**: the
[`grpc`](tests/grpc/Program.cs) harness runs a Greeter service (unary +
server-streaming, length-prefixed messages, `grpc-status` in trailers) over the
stack and interop-tests it against the real `Grpc.Net.Client`.

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
| **6455** | The WebSocket Protocol | ✅ Complete | Framing, masking, fragmentation, close handshake, UTF-8 validation. Autobahn 517/517. Server **and** client roles. |
| **7692** | Compression Extensions for WebSocket (permessage-deflate) | ✅ | No-context-takeover mode, negotiated on both HTTP/1.1-Upgrade and HTTP/2-CONNECT handshakes. |
| **9110** | HTTP Semantics | ✅ | Methods, conditional requests, Range (single + multi), content negotiation, the §11 auth framework. |
| **9111** | HTTP Caching | ✅ | Client-side cache with shared/private semantics. |
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
  WINDOW_UPDATE, CONTINUATION, ORIGIN, PRIORITY_UPDATE).
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
  truncated-block → `COMPRESSION_ERROR`.
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
- Inbound + outbound `MAX_HEADER_LIST_SIZE` enforcement.
- Per-stream `RST_STREAM` cancellation (a `CancellationToken` into the handler).
- Closed-stream pruning; graceful shutdown (GOAWAY to every active connection).

### Slowloris / timeout hardening

- TLS-handshake, preface, SETTINGS-ACK, idle, and in-progress (partial
  frame/header-block) timeouts (`HTTP2Timeouts`) — reclaiming a peer that sends
  *too little*, complementing the flood defenses against *too much*.

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
- **ORIGIN frame** (RFC 8336): a server can state the set instead of leaving the
  client to infer it (`OriginSet` on `HTTP2Server`, sent right after the
  preface). An announced set also becomes the yardstick for the 421 check —
  having told the client what we serve, answering for something else would
  contradict our own announcement. The client exposes what it received as
  `HTTP2ClientConnection.OriginSet`, and ignores the frame on a non-zero stream
  (§2.1) or over h2c, where an unauthenticated peer's claim about its own
  identity is worth nothing (§2.4).

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
  + response-body write + **response trailers** (RFC 9113 §8.1) — server and
  client (`HTTP2ClientStream`).
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
| Oversized header lists | Inbound + outbound `MAX_HEADER_LIST_SIZE` |
| Range amplification | `MaxRanges` cap on a byte-range set |
| Weak TLS 1.2 cipher suites | RFC 9113 Appendix A check → `GOAWAY INADEQUATE_SECURITY` |
| Answering for a foreign origin | `:authority` checked against the certificate / Origin Set → 421 |
| Credential timing oracles | Constant-time compare in Digest (`FixedTimeEquals`) |

## Explicitly out of scope

- **Server push** (`PUSH_PROMISE` outbound) — deprecated; we advertise
  `ENABLE_PUSH=0` and reject inbound pushes.
- **RFC 7540 priority** (stream dependencies/weights) — superseded by RFC 9218;
  parsed-and-ignored (only structural self-dependency is validated).
- **RFC 7540 `Upgrade: h2c`** — removed in RFC 9113 §3.1; only prior-knowledge
  h2c is implemented.
- **ALTSVC (RFC 7838)** — not implemented (yet); it is the one remaining live
  extension frame, and the natural bridge to an HTTP/3 endpoint.
- **`Accept-Charset`** — deprecated in RFC 9110 §12.5.2.
- **Multi-origin connection pooling** — the pool is single-origin by design.

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
- RFC 7617 — The 'Basic' HTTP Authentication Scheme
- RFC 6750 — OAuth 2.0 Bearer Token Usage
- RFC 7616 — HTTP Digest Access Authentication
- RFC 8297 — An HTTP Status Code for Indicating Hints (103 Early Hints)
- RFC 10008 — The HTTP QUERY Method
- RFC 5861 — HTTP Cache-Control Extensions for Stale Content
- RFC 8941 — Structured Field Values for HTTP
- RFC 4647 — Matching of Language Tags

