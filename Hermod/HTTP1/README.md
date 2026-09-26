# Hermod HTTP/1.0 and HTTP/1.1 support

This document describes the HTTP/1.x capabilities of the Hermod HTTP client and
HTTP server. It distinguishes wire-protocol support from application semantics:
Hermod validates and processes HTTP messages, while resource-specific behavior
such as caching, range selection, authorization policy, or WebDAV operations is
implemented by the application handler.

Last verified: **2026-09-26**

## Support levels

| Level | Meaning |
|---|---|
| **Implemented and regression-tested** | Hermod implements the behavior and the repository contains focused automated tests. |
| **Interoperability-tested** | The behavior is tested between Hermod and the .NET HTTP stack in at least one direction. |
| **Model/API support** | Hermod exposes methods, status codes, headers, or authentication values, but application semantics remain the handler's responsibility. |
| **Out of scope** | The behavior is intentionally not implemented by the HTTP/1.x origin client/server. |

## Standards and specifications

| Specification | Hermod support |
|---|---|
| [RFC 1945](https://www.rfc-editor.org/rfc/rfc1945.html), HTTP/1.0 | Implemented and regression-tested for requests, responses, `Content-Length`, close-delimited responses, default connection closing, optional negotiated keep-alive, and HTTP/1.0-specific rejection/fallback behavior. |
| [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html), HTTP semantics | Core message semantics are implemented: methods and status codes, `Host`, connection handling, `Expect: 100-continue`, bodyless responses, representation metadata, and extensible header fields. Every status code in the IANA registry is defined with its registered reason phrase — the six that predate RFC 9110 keep their older phrase, pinned explicitly by `HTTPStatusCodeTests`. Resource semantics remain application-defined. |
| [RFC 9112](https://www.rfc-editor.org/rfc/rfc9112.html), HTTP/1.1 message syntax and routing | Implemented and regression-tested for start lines, header parsing, message framing, persistent connections, pipelining, chunked transfer coding, trailers, and malformed-message rejection. |
| [RFC 10008](https://www.rfc-editor.org/rfc/rfc10008.html), HTTP `QUERY` method | The method is modeled as safe and idempotent and is end-to-end tested with fixed-length and chunked request content, trailers, and chunk extensions. Media-type policy, `Accept-Query`, caching, conditional requests, and query-result URI policy are handler responsibilities. |
| [RFC 4918](https://www.rfc-editor.org/rfc/rfc4918.html), WebDAV | Method tokens are modeled (`COPY`, `LOCK`, `MKCOL`, `MOVE`, `PROPFIND`, `PROPPATCH`, and `UNLOCK`). Hermod does not provide a complete WebDAV resource implementation. |
| [RFC 7617](https://www.rfc-editor.org/rfc/rfc7617.html), Basic authentication | Typed parsing/serialization and end-to-end server authorization tests, including challenge, malformed credentials, invalid credentials, and forbidden users. Authentication policy is configured by the application. |
| [RFC 6750](https://www.rfc-editor.org/rfc/rfc6750.html), Bearer tokens | Typed Bearer authorization parsing/serialization is available. Token validation and authorization policy are application concerns. |
| [RFC 6455](https://www.rfc-editor.org/rfc/rfc6455.html), WebSocket | Implemented in Hermod's WebSocket client/server subsystem. The HTTP/1.1 server can also serve a WebSocket on a path of its own, beside ordinary HTTP on the same listener: `WebSocketUpgrade.For` hands a request that asks for an upgrade over to a WebSocket server, whose single implementation of the opening handshake (RFC 6455 §4.2) validates it and answers the `101` - the HTTP layer only decides whether a request is asking. Regression-tested by `WebSocketOnAnHTTPPathTests`; see [WebSocket/README.md](WebSocket/README.md#on-an-http-path). |
| [WHATWG Server-Sent Events](https://html.spec.whatwg.org/multipage/server-sent-events.html) | Implemented and regression-tested for `text/event-stream`, parsing, live streaming, reconnection, `Last-Event-ID`, retry intervals, comments/heartbeats, cancellation, and disconnect cleanup. |

Older source comments may refer to RFC 2616 or the RFC 7230 series. The current
normative HTTP references for this document are RFC 9110 and RFC 9112.

## Transport

- HTTP/1.x is supported over plain TCP and over TLS (`http` and `https`).
- The server can listen for IPv4 and IPv6 clients.
- TLS certificate selection and validation are configurable by the client and
  server APIs.
- Slow or timed-out TLS handshakes do not block the accept loop and are removed
  from active-client tracking.
- HTTP framing and semantics are identical after the TCP or TLS stream has been
  established; TLS protocol-version and certificate policy are separate from
  HTTP/1.x conformance.

## HTTP/1.0

The server accepts canonical `HTTP/1.0` requests and emits an `HTTP/1.0` status
line in the corresponding response.

Implemented behavior:

- `Host` is optional.
- Request bodies framed with a valid `Content-Length` are supported.
- Connections close after the response by default.
- Keep-alive is honored only when it is explicitly negotiated in both
  directions.
- `Connection: close` takes precedence over keep-alive.
- Close-delimited response bodies are supported by the Hermod client.
- `HEAD` and responses whose status forbids content remain bodyless.
- `Expect: 100-continue` does not cause an HTTP/1.0 interim response.
- Unsupported expectations are rejected with `417 Expectation Failed`.
- Chunked requests are rejected because transfer coding was introduced by
  HTTP/1.1.
- A static response configured for automatic chunking falls back to a
  close-delimited HTTP/1.0 response. `Transfer-Encoding` and `Trailer` are
  removed.
- A manually streamed chunked response cannot be represented safely in
  HTTP/1.0 and is rejected before chunk bytes are written.

Non-canonical versions such as `HTTP/1`, `HTTP/01.1`, or differently cased
variants are rejected.

## HTTP/1.1

### Request and response syntax

The server validates the request line and header section before routing:

- HTTP/1.1 requests require exactly one valid `Host` field.
- Field names must use valid token syntax; whitespace before the colon is
  rejected.
- Obsolete line folding is rejected.
- Control characters in field values are rejected.
- Incomplete headers are terminated after the configured receive timeout.
- Requests fragmented across multiple TCP reads are assembled correctly.
- Origin-form request targets are supported.
- Asterisk-form is supported for server-wide `OPTIONS *`.
- Absolute-form targets are rejected because Hermod is not acting as a proxy.
- Authority-form `CONNECT` requests return `501 Not Implemented`.
- Fragments, malformed percent escapes, encoded path separators, dot segments,
  repeated path separators, and double-encoded separators are rejected by the
  server's request-target validation.

### Message framing

| Framing mode | Requests | Responses |
|---|---:|---:|
| `Content-Length` | Yes | Yes |
| `Transfer-Encoding: chunked` | Yes | Yes |
| Close-delimited body | No | Yes, when connection close defines the response boundary |
| Automatic static-body chunking | Not applicable | Yes |
| Live/streamed chunking | Yes through the client `ChunkWorker` API | Yes through the response `ChunkWorker` API |

Hermod rejects ambiguous or invalid framing, including:

- repeated or comma-combined `Content-Length` fields;
- simultaneous `Content-Length` and `Transfer-Encoding`;
- invalid, negative, signed, overflowing, or non-decimal content lengths;
- transfer codings where `chunked` is not the final coding;
- unsupported transfer codings;
- truncated fixed-length or chunked bodies;
- malformed chunk-size lines and invalid chunk delimiters;
- response framing that would make a client connection unsafe to reuse.

This strict handling is intended to prevent request/response desynchronization
and common message-smuggling ambiguities.

### Persistent connections and pipelining

- HTTP/1.1 connections are persistent unless closing is requested or required.
- `Connection: close` overrides a contradictory keep-alive token.
- The Hermod client reconnects before a subsequent request after the peer has
  closed the connection.
- Sequential requests reuse an eligible keep-alive connection.
- Multiple pipelined requests are processed in wire order.
- A chunked request is fully delimited before the next pipelined request is
  parsed.
- An invalid leading pipeline request closes the connection before trailing
  bytes can be interpreted as another request.
- Completed and aborted requests are removed from the server's active-client
  tracking.

### Informational and bodyless responses

- `100 Continue` is sent before the server reads an expected request body.
- The Hermod client waits for `100 Continue` before sending the body when
  requested.
- A final rejection such as `417 Expectation Failed` prevents the client from
  sending the body.
- Other headerless informational responses are skipped until the final
  response is received.
- Malformed informational responses are rejected.
- `HEAD`, all `1xx` responses, `204 No Content`, `205 Reset Content`, and
  `304 Not Modified` are treated as bodyless according to their applicable
  semantics.
- Unexpected body bytes on a reusable connection cause the connection to be
  discarded rather than reused unsafely.

### Routing and method handling

The HTTP API routes by host, path, and method. It supports server-wide
`OPTIONS`, resource-level `OPTIONS`, `405 Method Not Allowed`, and generation of
the corresponding `Allow` field.

Handlers are registered at an `HTTPAPI` (`AddHandler`) with URL templates
relative to the root path of that API; a trailing `{name..}` parameter catches
the rest of the path. `HTTPServer.AddMethodCallback` and the `Register...`
helpers take server-wide templates instead and hand the handler to the HTTP API
owning the path, creating a default API at `/` for the hostname when none
exists yet.

Built-in method values include:

- Core HTTP methods: `GET`, `HEAD`, `POST`, `PUT`, `DELETE`, `OPTIONS`, `TRACE`,
  and the modeled `CONNECT` token.
- WebDAV method values: `COPY`, `LOCK`, `MKCOL`, `MOVE`, `PROPFIND`,
  `PROPPATCH`, and `UNLOCK`.
- Standard `QUERY` according to RFC 10008.
- Hermod/application extension methods such as `MIRROR`, `SEARCH`, `EXISTS`,
  `COUNT`, `FILTER`, `STATUS`, `PATCH`, and others declared by `HTTPMethod`.
- Additional application methods can be parsed and registered with their safe
  and idempotent metadata.

Having a method value means that it can be parsed, serialized, and routed. It
does not create resource semantics automatically; a handler must be registered
for the method and path.

## Chunked transfer coding

Hermod supports chunked transfer coding in both directions and in both client
and server roles.

### Receiving

- Hexadecimal chunk sizes are parsed with overflow protection.
- Chunk data can arrive in arbitrary TCP segmentation.
- The terminal zero-size chunk is required.
- Chunk extensions are validated and exposed in wire order through
  `AHTTPPDU.ChunkExtensions`.
- Repeated extension names retain all values.
- Token values, valueless extensions, quoted values, and escaped characters are
  supported.
- Malformed extension syntax, control characters, and incomplete quoted values
  are rejected.
- Trailers are parsed after the terminal chunk and exposed as trailing headers.
- Body-size and metadata limits apply while streaming; buffering the entire
  encoded message is not required.

### Sending

- Static response bodies can be chunked automatically.
- Request and response workers can stream chunks asynchronously.
- A worker needs no stream of its own: announcing `Transfer-Encoding: chunked`
  and supplying a `ChunkWorker` is enough, and the server wraps the connection.
  Supplying a `ChunkedTransferEncodingStream` as the body still works and is
  what a handler that wants its own chunk sizes or its own lifetime should do.
- The terminal zero-size chunk is written whether or not the worker wrote it,
  so a worker that returns early - or throws - cannot leave the recipient
  waiting for a message that is already over. The same holds for a chunked
  *request* body written by the client's `ChunkWorker`. `Finish` is idempotent,
  so a worker that ends its own body is unaffected.
- Workers can attach token, valueless, or quoted chunk extensions.
- Unsafe extension names or values are rejected before being written.
- Both static and live responses can send trailer fields.
- Outgoing trailers are validated before the terminal chunk is emitted.

A byte array or an ordinary stream on a response that announces
`Transfer-Encoding: chunked` is taken to be **framed already** and is copied
through untouched; `AutomaticallyChunkContent` is how a handler says that its
bytes are raw and wants them framed. That is a contract rather than an
oversight, and it is why the server does not simply frame everything that says
"chunked".

The following trailer fields are rejected for incoming and outgoing trailers:

`Authorization`, `Cache-Control`, `Content-Encoding`, `Content-Length`,
`Content-Range`, `Content-Type`, `Host`, `Max-Forwards`, `TE`, `Trailer`, and
`Transfer-Encoding`.

### Default chunk metadata limits

| Limit | Default |
|---|---:|
| Chunk-size line | 8 KiB |
| Individual trailer line | 8 KiB |
| Trailer field count | 100 |
| Total trailer size | 32 KiB |
| Total chunk metadata | 64 KiB |

All limits are configurable on the HTTP server.

## Content codings

`Content-Encoding` is undone on the message, not in the client or in the server:
the three methods on `AHTTPPDU` serve both directions, so a gzipped request body
a handler receives and a gzipped response body a client receives take the same
path.

| Undoing a coding | |
|---|---|
| `DecodeBody(...)` | Returns the representation of a body already in hand, leaving the message alone. |
| `TryDecodeBodyInPlace(...)` | The same, but the message stops claiming a coding it no longer has: `Content-Encoding` goes, `Content-Length` is corrected to the decoded length, and what was undone is kept in `DecodedContentEncoding`. |
| `TryDecodeBodyStream(...)` | Puts the reversal *into* `HTTPBodyStream`, for the bodies that never are an array: chunked, close-delimited, and event streams. `Content-Encoding` goes and `Content-Length` goes with it — it counted the encoded octets, and the buffering loop stops reading at it, so leaving it would truncate the body at its compressed size. `RawHTTPHeader` keeps both: it is the record of what arrived. |

| | |
|---|---|
| Codings | `br`, `gzip`, `deflate` — the last one both zlib-wrapped (RFC 1950, what RFC 9110 names) and raw (RFC 1951, what many servers send). The octets are sniffed, because `DeflateStream` reads only the raw form. |
| Stacked codings | Undone in reverse order, as RFC 9110, Section 8.4 defines the list. |
| `identity` | The absence of a coding, so nothing is decoded. |
| An unknown coding | Refused: `DecodeBody(...)` throws, `TryDecodeBody(...)` returns false. Returning the encoded octets as if they were the representation is the one answer that would be dangerous. |
| Decompression bound | 64 MiB by default, overridable per call. The ceiling bites *during* decompression, not after it. |

Decoding is explicit: `HTTPBody` is what arrived, `DecodeBody(...)` is what it
means. Nothing decodes a body behind a caller's back, and a message declaring no
coding gets its body back unchanged and uncopied.

### The client asks

`AHTTPClient.AutomaticDecompression` — **off by default**, because it changes
what goes out on the wire and what the caller gets back — advertises
`Accept-Encoding: br, gzip, deflate` on requests that do not already carry one,
and hands the caller the representation. Never over a caller's own field:
`identity` is how compression is switched off for one request. The ceiling is
`MaxDecodedBodySize`, 64 MiB by default.

All three body shapes decode, and each takes a different route for a reason: a
length-framed or close-delimited body is wrapped before it is read, an event
stream has no alternative to being wrapped, and a chunked body consumed on
arrival was already an array before anything could wrap it.

### The server offers

`AHTTPServer.AutomaticContentCompression` — **off by default**, because
compressing per response rather than once per representation is a trade only the
operator can make. `SinglePageAppHandler` keeps doing it the better way for
static files, compressing each once at startup; this is for every other handler,
and it skips a response that already carries a `Content-Encoding`.

It runs after the handler and after the error paths, so what gets compressed is
what is about to be sent. What it declines to compress is the longer list, and
every entry is a way to be wrong rather than merely inefficient:

| Left alone | Why |
|---|---|
| A body that is still a stream | It belongs to whoever is about to write it — a live chunked worker, an event source — and taking it over breaks the framing they own. Checked *before* anything reads `HTTPBody`, which would drain that stream to find out whether there is one. |
| A chunked response | Its length must not be restated and its trailers belong to the stream. |
| A response that already has a coding | The handler has already thought about this. |
| `206`, or anything with a `Content-Range` | A range is part of the *selected representation*; a coding applies to the representation as a whole (RFC 9110, Section 14.4). |
| A media type that is already compressed | The decision is made on the type, not on how well the bytes would happen to compress. |
| A body under `MinimumCompressibleSize` | 1 KiB by default: below that the gzip framing and the extra field cost more than they save. |
| `gzip;q=0` | A refusal, not a request (Section 12.4.2). |
| A result that came out no smaller | Sending more bytes and calling them compressed helps nobody. |

When it does compress, three fields follow the octets: `Vary` gains
`Accept-Encoding`, merged rather than overwritten (Section 12.5.5), a strong
`ETag` gains the coding, because the encoded and the identity form are different
representations (Section 8.8.3), and `HEAD` is treated exactly like `GET` so the
two agree on `Content-Length` (Section 9.3.2).

The server offers `br` and `gzip` where the client accepts `br`, `gzip` and
`deflate`: `deflate` is the one the wire disagrees about, so it is read and not
written.

## Authentication

The RFC 9110 Section 11 framework — the authenticator, the scheme interface, the
credential parser, the identity, and the Basic / Bearer / Digest / Token schemes
— lives in `HTTP/Authentication/` and is version-independent. It sat under
`HTTP2/` until 2026-09-24, which is what had made RFC 7616 Digest unreachable
from HTTP/1.x; moving it changed no behaviour.

| Scheme | Support |
|---|---|
| Basic (RFC 7617) | Typed credentials and a store-agnostic validator. |
| Bearer (RFC 6750) | Typed credentials; token validation is the application's. |
| **Digest (RFC 7616)** | Challenge and validation: stateless signed nonce with an age bound, `qop=auth` with `nc`/`cnonce`, the legacy RFC 2069 no-`qop` form, `-sess` variants, SHA-256 (default) and MD5. `auth-int` is not advertised. |
| Token (non-standard) | Vanaheimr's own. |

`HTTPDigestAuthentication` parses the credentials off the wire;
`DigestAuthenticationScheme` decides whether they are valid, because that needs
the nonce's own integrity and age, the realm, and a password lookup.

`BuildChallenges(realm, algorithms…)` emits one challenge per algorithm, most
preferred first, all sharing one nonce — RFC 7616 Section 3.3. Sharing the nonce
is what makes them one offer the client may answer either way.

**Interoperability, measured rather than claimed** (2026-09-24, against the
HTTP/1.1 conformance suite's demo host):

| Client | MD5 | SHA-256 |
|---|---|---|
| curl 8.14.1, `x86_64-pc-linux-gnu`, OpenSSL | 200 | 200 |
| curl 8.21.0, `x86_64-w64-mingw32`, Schannel | 200 | no `Authorization` sent |

Note also what curl does with more than one challenge in a single
`WWW-Authenticate` field: nothing. Two Digest challenges, or Digest beside
Basic, and it sends no credentials at all, in either order. RFC 9110
Section 11.6.1 permits several challenges per field and observes in the same
breath that parsing them is ambiguous, since auth-params are comma-separated
too. Separate header lines are the unambiguous form; one challenge per response
is the form that works today.

## Server-Sent Events

Hermod implements SSE as an HTTP streaming extension using
`Content-Type: text/event-stream`.

Server capabilities:

- static and live event streams;
- event `id`, `event`, `data`, and `retry` fields;
- multiple `data:` lines;
- bounded event history;
- replay after `Last-Event-ID`;
- per-request event filtering;
- multiple parallel clients;
- comment-based heartbeat messages;
- prompt worker cancellation and connection cleanup after a client abort.

Client capabilities:

- parsing of event fields and multiple data lines;
- ignoring comments and unknown fields;
- propagation of server-provided retry intervals;
- automatic reconnection;
- continuation from the last event ID;
- configurable maximum reconnect attempts;
- cancellation-aware parsing and reconnect delays.

SSE is not a separate HTTP version and uses the normal HTTP/1.1 connection and
stream lifecycle. It is specified by the WHATWG HTML Living Standard rather
than an IETF RFC.

## Headers, authentication, and application semantics

Hermod has typed models for many common request and response fields and also
allows extension fields. This includes content negotiation, representation
metadata, authorization, CORS fields, cookies, conditional request fields,
ETags, range-related fields, and WebDAV fields.

The presence of a typed field does not imply an automatic policy engine:

- Basic and Bearer credentials can be parsed and serialized; handlers decide
  whether credentials are valid and authorized.
- Conditional fields and ETags are available to handlers; Hermod is not a
  shared HTTP cache.
- Range and `Content-Range` fields are modeled; handlers select and generate
  partial representations.
- CORS fields are modeled; applications configure the desired cross-origin
  policy.
- Cookies are modeled; application code owns session and persistence policy.
- `Accept-Query` can be emitted as an extension field, but RFC 10008 content
  negotiation is not automatically enforced.

## Resource limits and timeouts

The server rejects oversized messages before routing or while streaming.
Defaults are:

| Limit | Default |
|---|---:|
| HTTP body | 8 MiB |
| Complete header section | 32 KiB, capped by the configured receive buffer |
| Individual header line | 8 KiB |
| Request target | 8 KiB |
| Header field count | 100 |
| TCP receive timeout | 30 seconds |
| TCP send timeout | 30 seconds |
| Hermod client request timeout | 120 seconds |

Body, header, request-target, header-count, chunk-line, trailer, and chunk
metadata limits are configurable. Incomplete request bodies time out with
`408 Request Timeout`; malformed or truncated messages are rejected and the
connection is not reused.

## Interoperability test matrix

The HTTP tests intentionally cover three integration directions:

| Client | Server | Purpose |
|---|---|---|
| Hermod HTTP client | Hermod HTTP server | Native end-to-end behavior and public API coverage |
| Hermod HTTP client | .NET Minimal API | Client interoperability with the .NET server stack |
| .NET `HttpClient` | Hermod HTTP server | Server interoperability with the .NET client stack |

Raw TCP/TLS clients and purpose-built raw servers supplement this matrix for
wire-level behavior that higher-level clients refuse to generate, including
malformed framing, pipelining, invalid trailers, connection aborts, and exact
HTTP/1.0 behavior.

The principal regression suites are:

- `HermodTests/HTTP/HTTPClientTests.cs`
- `HermodTests/HTTP/dotNET/HTTPClientTests_MinimalAPI.cs`
- `HermodTests/HTTP/dotNET/DotNetHTTPClientTests.cs`
- `HermodTests/HTTP/HTTPServerSocketRegressionTests.cs`
- `HermodTests/HTTP/HTTPClientProtocolRegressionTests.cs`
- `HermodTests/HTTP/HTTP11AuditRegressionTests.cs`
- `HermodTests/HTTP/HTTPServerListenerMatrixTests.cs`
- `HermodTests/HTTP/HTTPStatusCodeTests.cs`
- `HermodTests/HTTP/ContentEncodingHeaderTests.cs`
- `HermodTests/HTTP/HTTPDigestAuthenticationTests.cs`
- `HermodTests/HTTP/ContentCodingStreamTests.cs`
- `HermodTests/HTTP/ClientContentDecodingTests.cs`
- `HermodTests/HTTP/ServerContentCompressionTests.cs`

As of the verification date, the broad HTTP/1.x regression selection contains
**372 passing tests, 0 failed, 0 skipped**.

Run it with:

```powershell
dotnet test HermodTests\HermodTests.csproj --filter "FullyQualifiedName~HTTPClientTests|FullyQualifiedName~HTTPServerSocketRegressionTests|FullyQualifiedName~HTTPClientProtocolRegressionTests|FullyQualifiedName~HTTP11AuditRegressionTests|FullyQualifiedName~HTTPServerListenerMatrixTests|FullyQualifiedName~HTTPStatusCodeTests|FullyQualifiedName~ContentEncodingHeaderTests|FullyQualifiedName~HTTPDigestAuthenticationTests|FullyQualifiedName~ContentCodingStreamTests|FullyQualifiedName~ClientContentDecodingTests|FullyQualifiedName~ServerContentCompressionTests"
```

## Deliberate exclusions and qualification

Hermod's HTTP/1.x origin client/server is feature-complete for its intended
scope, with these explicit boundaries:

- It is not an HTTP forward proxy or gateway; `CONNECT`, proxy absolute-form
  routing, proxy authentication, and intermediary transformations are out of
  scope.
- It does not provide a shared RFC 9111 cache.
- It does not automatically implement application-specific conditional,
  range, WebDAV, or QUERY semantics.
- WebSocket is tested and maintained as a separate subsystem; the HTTP/1.1
  server only hands a request that asks for an upgrade over to it
  (`WebSocketUpgrade.For`).
- HTTP/2 and HTTP/3 are separate protocols and are not covered here.
- Typed or generic support for a header is not a claim that every RFC defining
  application semantics for that header is implemented automatically.

The current tests provide strong regression and interoperability coverage. A
claim of exhaustive RFC conformance would additionally require a maintained
requirement-by-requirement RFC matrix, external compliance tooling, and
continuous parser fuzzing.

## Maintenance rule

When HTTP/1.x behavior changes:

1. Add a focused protocol regression test.
2. Add applicable end-to-end tests across the Hermod/Hermod and Hermod/.NET
   matrix.
3. Use a raw peer when an invalid or wire-specific message cannot be produced
   by a high-level client.
4. Update this document's support statement, limits, exclusions, and last
   verification date.
