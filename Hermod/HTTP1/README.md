# Hermod HTTP/1.0 and HTTP/1.1 support

This document describes the HTTP/1.x capabilities of the Hermod HTTP client and
HTTP server. It distinguishes wire-protocol support from application semantics:
Hermod validates and processes HTTP messages, while resource-specific behavior
such as caching, range selection, authorization policy, or WebDAV operations is
implemented by the application handler.

Last verified: **2026-11-20**

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
| [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html), HTTP semantics | Core message semantics are implemented: methods and status codes, `Host` and the §7.4 refusal of a request for an origin the TLS certificate does not cover (since 2026-10-29), connection handling, `Expect: 100-continue`, bodyless responses, representation metadata, and extensible header fields. Every status code in the IANA registry is defined with its registered reason phrase. Six carried pre-RFC-9110 phrases until 2026-10-03; five now match the registry (413 `Content Too Large`, 414 `URI Too Long`, 416 `Range Not Satisfiable`, 422 `Unprocessable Content` and 306 `(Unused)`) without any field being renamed, since the wire carries the phrase and downstream code refers to the field. 418 keeps `I'm a teapot`, which RFC 9110 does not mention and RFC 2324 does. `HTTPStatusCodeTests` pins that one remaining divergence. Resource semantics remain application-defined, unless a resource is registered with `AddResource`: then negotiation, preconditions and ranges (§12–14) come from the library, the same code HTTP/2 uses, since 2026-11-20 — see "Resources: negotiation, preconditions and ranges". |
| [RFC 9112](https://www.rfc-editor.org/rfc/rfc9112.html), HTTP/1.1 message syntax and routing | Implemented and regression-tested for start lines, all four forms of request-target (absolute-form accepted since 2026-11-20), header parsing, message framing, persistent connections, pipelining, chunked transfer coding, trailers, and malformed-message rejection. |
| [RFC 10008](https://www.rfc-editor.org/rfc/rfc10008.html), HTTP `QUERY` method | The method is modeled as safe and idempotent and is end-to-end tested with fixed-length and chunked request content, trailers, and chunk extensions. Media-type policy, `Accept-Query`, caching, conditional requests, and query-result URI policy are handler responsibilities, except for a resource registered with `AddResource` and a query handler: its `QUERY` runs through the `GET` pipeline, conditional requests and negotiation included, content without a `Content-Type` is a `400` (RFC 10008, Section 4), and the result can name its own URI in `Content-Location`. |
| [RFC 4918](https://www.rfc-editor.org/rfc/rfc4918.html), WebDAV | Method tokens are modeled (`COPY`, `LOCK`, `MKCOL`, `MOVE`, `PROPFIND`, `PROPPATCH`, and `UNLOCK`). Hermod does not provide a complete WebDAV resource implementation. |
| [RFC 7617](https://www.rfc-editor.org/rfc/rfc7617.html), Basic authentication | Typed parsing/serialization and end-to-end server authorization tests, including challenge, malformed credentials, invalid credentials, and forbidden users. Authentication policy is configured by the application. The client answers a challenge when it is given `Credentials`, since 2026-11-20; see [The client: challenges and redirects](#the-client-challenges-and-redirects). |
| [RFC 6750](https://www.rfc-editor.org/rfc/rfc6750.html), Bearer tokens | Typed Bearer authorization parsing/serialization is available. Token validation and authorization policy are application concerns. The client answers a Bearer challenge with a token from its `Credentials`. |
| [RFC 9651](https://www.rfc-editor.org/rfc/rfc9651.html), Structured Field Values | Lists, Dictionaries and Items with all eight bare types, including the Date and Display String that RFC 8941 did not have. Parsing follows Section 4.2 exactly, so what is malformed is refused rather than repaired, and serializing is the canonical form of Section 4.1 - the round trip is byte-for-byte, which is the property RFC 9421 signatures would need. The one leniency Section 4.2.7 asks for is there since 2026-10-28: a Byte Sequence without its `=` padding parses. Its first users are the RFC 9530 digest fields. |
| [RFC 9530](https://www.rfc-editor.org/rfc/rfc9530.html), Digest Fields | `Content-Digest`, `Repr-Digest`, `Want-Content-Digest` and `Want-Repr-Digest` are typed on both directions, in `sha-256` and `sha-512`, with the grammar shared with HTTP/2. Opt-in on either role: the server's `AutomaticContentDigests` digests what it sends, after its compression; the client's `VerifyDigests` asks for a digest and checks it before it decodes. A handler's own digest is computed again when the server compresses. The obsolete `Content-MD5` stays readable. See [Digest fields](#digest-fields-rfc-9530). |
| [RFC 7239](https://www.rfc-editor.org/rfc/rfc7239.html), Forwarded | The field is parsed into typed elements - `for`, `by`, `host`, `proto` and any extension parameters - and can be built to be sent onwards when a request is proxied. Node identifiers cover addresses, `unknown` and obfuscated identifiers; the last two are kept opaque rather than resolved into an address that was never given. Where a request carries both, `Forwarded` is preferred over the `X-Forwarded-For` family (Section 7.4). Either way the addresses are recorded *beside* the peer socket and never instead of it: everything in these fields is hearsay from whoever wrote them (Section 8.1), and forgeable by the client when no proxy strips them. |
| [RFC 6266](https://www.rfc-editor.org/rfc/rfc6266.html), Content-Disposition | `Content-Disposition` is typed: `HTTPContentDisposition`, with the disposition type, the file name and extension parameters. Parsing follows Section 4.1, and `filename*` wins over `filename` (Section 4.3). A sender writes what Appendix D advises: `filename` alone where ASCII is enough, and otherwise an ASCII `filename` first and `filename*` in UTF-8 after it. `SafeFileName` is the name made fit to store under. Since 2026-10-29; see [Content-Disposition](#content-disposition-rfc-6266). |
| [RFC 7838](https://www.rfc-editor.org/rfc/rfc7838.html), HTTP Alternative Services | `Alt-Svc` is typed for responses and `Alt-Used` for requests, sent by handlers and read by the client, with the field's grammar shared with HTTP/2's ALTSVC frame. The client records alternatives and does not act on them. Announcing one is the application's decision. |
| [RFC 8288](https://www.rfc-editor.org/rfc/rfc8288.html), Web Linking | `Link` is typed on responses: `HTTPLinks`, a list of `HTTPLink`, each with its target, relation types, anchor and target attributes. A sender is held to the grammar, and parsing follows Appendix B. `title*` and other starred attributes are RFC 8187 values, through `HTTPExtendedValue`. Which links to send is the application's decision. Since 2026-10-29; see [Links](#links-rfc-8288). |
| [RFC 8297](https://www.rfc-editor.org/rfc/rfc8297.html), 103 Early Hints | A handler sends a `103` with `request.SendEarlyHints(links)` while it is still working on the final response, and any other interim response with `SendInformationalResponse`. The client keeps the interim responses it receives on `HTTPResponse.InformationalResponses`, where a `103`'s `Link` fields can be read. Whether to send hints is the application's decision. Since 2026-10-29; see [Informational and bodyless responses](#informational-and-bodyless-responses). |
| [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457.html), Problem Details for HTTP APIs | `HTTPProblemDetails` is the JSON object of Section 3, served as `application/problem+json`. Handlers send it with `SetProblemDetails`, which also sets the status code it names, and clients read it with `TryParseProblemDetails`. The server's own errors stay `application/json` unless a request's `Accept` prefers problem details. JSON only: the XML format of Appendix B is not implemented. Since 2026-10-29; see [Problem details](#problem-details-rfc-9457). |
| [RFC 6455](https://www.rfc-editor.org/rfc/rfc6455.html), WebSocket | Implemented in Hermod's WebSocket client/server subsystem. The HTTP/1.1 server can also serve a WebSocket on a path of its own, beside ordinary HTTP on the same listener: `WebSocketUpgrade.For` hands a request that asks for an upgrade over to a WebSocket server, whose single implementation of the opening handshake (RFC 6455 §4.2) validates it and answers the `101` - the HTTP layer only decides whether a request is asking. Regression-tested by `WebSocketOnAnHTTPPathTests`; see [WebSocket/README.md](WebSocket/README.md#on-an-http-path). |
| [WHATWG Server-Sent Events](https://html.spec.whatwg.org/multipage/server-sent-events.html) | Implemented and regression-tested for `text/event-stream`, parsing, live streaming, reconnection, `Last-Event-ID`, retry intervals, comments/heartbeats, cancellation, and disconnect cleanup. |

The normative HTTP references for this document, and for the source comments,
are RFC 9110 and RFC 9112. The 62 comments that still cited RFC 2616 or the
RFC 7230 series were rewritten on 2026-09-26 to each field's current defining
document and section, taken from the IANA HTTP Field Name registry rather than
from memory. Six mentions remain deliberately: they are RFC 4918 quoting
RFC 2616 in text this document quotes in turn, where rewriting them would
misquote RFC 4918, so each block carries a remark saying where the current
reference is. There was a seventh, inside commented-out code under
`HTTP1/Server/URLMapping_old/`; it went with that directory on 2026-10-04.

## Transport

- HTTP/1.x is supported over plain TCP and over TLS (`http` and `https`).
- The server can listen for IPv4 and IPv6 clients.
- TLS certificate selection and validation are configurable by the client and
  server APIs.
- The server answers ALPN (RFC 7301) with `http/1.1`, since 2026-10-04. It
  offered nothing before, so a client's extension went unacknowledged and a
  client that could also speak h2 had to guess from the silence. Only
  `http/1.1` is offered, because that is what this server speaks: a client
  insisting on `h2` alone is refused with `no_application_protocol` rather than
  served HTTP/1.1 anyway. A client that sends no ALPN extension is unaffected.
- A request for an origin the connection's certificate is not valid for is
  refused with `421 (Misdirected Request)`, since 2026-10-29. RFC 9110 §7.4
  requires it of an origin server: "a request for an "https" resource MUST be
  rejected unless it has been received over a connection that has been secured
  via a certificate valid for that target URI's origin". Until then every
  origin a request named was answered, so a server whose certificate names
  `localhost` served `Host: evil.example`, with SNI `evil.example` or without.
  - The certificate asked is the one the connection presented, so with a
    `ServerCertificateSelector` the answer can differ from one connection to
    the next. Names are matched as HTTP/2 matches them: subjectAltName before
    the common name, a wildcard for one left-most label, an IP literal only
    against an iPAddress entry.
  - The check comes before routing and before the body is read, so the
    connection is closed after the `421` and the client tries a new one
    (§15.5.20).
  - A cleartext connection has no certificate and is not checked by name.
    A target in absolute form naming `https` on it is refused with `421`
    since 2026-11-20: no certificate stands behind it at all, and only that
    form names a scheme. A request
    without `Host`, which only HTTP/1.0 allows, names no origin and cannot be
    misdirected.
  - `AHTTPServer.IsAuthorityServed` is asked in place of the certificate, on
    every connection, as `HTTP2Server`'s parameter of that name is. It is also
    the way out: §7.4 excepts "a trusted gateway", which a server cannot
    recognise by itself, so a server behind a proxy that terminates TLS and
    connects with a certificate of its own is given `_ => true`, or the names
    the proxy forwards. With a predicate given, an `https` target on cleartext
    is left to it as well.
  - `HTTPAuthority`, the HTTP/2 stack's answer to the same question, moved
    from `HTTP2/Core` to `HTTP/` to be used here.
- The connection a handler reaches through `request.TCPConnection` is the one
  the TCP server accepted, since 2026-10-29. It carries the server and client
  certificates of its handshake and the identifier the TCP server gave it. It
  used to be a second connection object, built by the full `TCPConnection`
  constructor, with two consequences:
  - it asked the `ServerCertificateSelector`, the `ServerCertificateChainSelector`
    and the `ConnectionIdBuilder` a second time, and a selector in the middle of
    a rotation could name a certificate the handshake never presented;
  - it never learned the client certificate, because the handshake had run
    before the wrap. On a mutual-TLS connection, `request.TCPConnection`
    showed none, although `request.ClientCertificate` had it.
- Slow or timed-out TLS handshakes do not block the accept loop and are removed
  from active-client tracking.
- HTTP framing and semantics are identical after the TCP or TLS stream has been
  established; TLS protocol-version and certificate policy are separate from
  HTTP/1.x conformance.
- An IPv6 zone identifier in a URL is understood, since 2026-10-04: RFC 6874
  has the `%` percent-encoded there, so `fe80::a%en1` is written
  `http://[fe80::a%25en1]`. The plain address parser splits at the first `%`
  and read that zone as `25en1` — a success with the wrong answer rather than
  a refusal. The `Host` field is sent without the zone, which §3 calls "highly
  desirable" rather than required: it names an interface of this machine, and
  the peer has no use for it.
- A client makes the DNS client it was handed none of when it first asks for
  one. Since 2026-10-03: the default searches the machine's network
  configuration for resolvers, which was 38.3 ms of the 39.4 ms a fresh client
  per request cost - for a request taking 1.06 ms - and a client dialling a
  literal IP address resolves nothing at all. Long-lived clients never noticed;
  one per request paid it every time.

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
- Absolute-form targets are accepted, since 2026-11-20, as RFC 9112 §3.2.2
  requires of every server: "A server MUST accept the absolute-form in
  requests". The target's authority replaces the `Host` field, which an
  HTTP/1.1 request must still carry, so routing, the `421` check and the
  handler all see one origin, and the path is held to the origin-form rules.
  Only `http` and `https` are accepted; userinfo and an empty host are refused
  with `400`. `HTTPRequest.TargetScheme` and `HTTPRequest.OriginFormTarget`
  give the parts. Until then the form was refused with `400`, on the reasoning
  that Hermod is not a proxy (H-42 in HTTP1ConformanceTests).
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

What the client states about a request without content follows RFC 9110
Section 8.6 since 2026-11-20. A `GET`, `HEAD`, `DELETE`, `CONNECT`, `OPTIONS`
or `TRACE` without content carries no `Content-Length`, since the method
anticipates none. A `POST`, `PUT`, `PATCH`, `QUERY` or any other method
without content says `Content-Length: 0`, since its method defines a meaning
for content. Before, every request without content said `0`, because the
request builder's `Content` setter writes it for null. A response without a
body still states its `0` on purpose: without a length it would end only
with its connection.

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
- Other informational responses are skipped until the final response is
  received, and kept on `HTTPResponse.InformationalResponses`, in order: the
  first `AHTTPClient.MaxInformationalResponses` (16) of them, since RFC 9110
  sets no limit and a server could send them without end. `101` is not among
  them, since it is not interim.
- A handler sends an interim response ahead of its final one with
  `request.SendInformationalResponse(status, fields)`, or a `103 Early Hints`
  (RFC 8297) with `request.SendEarlyHints(links)`. It goes out at once, while
  the handler is still working, which is what Early Hints are for.
  - It returns false and sends nothing to an HTTP/1.0 client, which MUST NOT
    get a `1xx` (RFC 9110, Section 15.2), and once the handler has returned:
    the server closes the way before it writes the final response, so nothing
    can follow that or land inside it.
  - It throws for a status that is not `1xx` or is `101`, a field name that is
    not a token, a value that is not printable ASCII, and `Content-Length` or
    `Transfer-Encoding`, which a `1xx` MUST NOT carry.
  - Sending is the application's decision. RFC 8297's security considerations
    note that an HTTP/1.1 client which takes a `1xx` for the final response
    misreads everything after it on the connection.
- Malformed informational responses are rejected.
- `HEAD`, all `1xx` responses, `204 No Content`, `205 Reset Content`, and
  `304 Not Modified` are treated as bodyless according to their applicable
  semantics.
- Unexpected body bytes on a reusable connection cause the connection to be
  discarded rather than reused unsafely.

### Routing and method handling

The HTTP API routes by host, path, and method. It supports server-wide
`OPTIONS *`, resource-level `OPTIONS`, `405 Method Not Allowed`, and generation
of the corresponding `Allow` field.

The routing code is one generation, since 2026-10-04. `URLMapping/` had a
`URLMapping_old/` beside it, left by the April 2026 commit that deprecated the
previous implementation: five files of nothing but commented-out code, and two
that were neither old nor commented - the `URLReplacement` enum that today's
`MethodNode` and `PathNode` both read, and 848 lines of live extension methods
on `HTTPAPI`, `HTTPExtAPI` and `HTTPServer` that this library's own tests and
several other repositories call. Those methods had been modernised under the
name `HTTPStandardHandlersX`, while the deprecated twin beside them kept the
plain name: the modernisation had happened to the copy. Both live files are now
in `URLMapping/`, the class is `HTTPStandardHandlers` again, and the rest is
deleted. No behaviour changed - the risk in a move like this is losing an
overload, which breaks a caller in another repository at compile time and would
never show up here, so `HTTPStandardHandlersTests` pins all eleven entry points
and that each is still an extension method. Four of the eleven had a caller in
the test suite then. All eleven have one since 2026-10-28: `MapFileSystemFolder`
on both APIs in `MapFileSystemFolderTests` (below), `RegisterFileSystemFile` in
`RegisterFileSystemFileTests`, and the four embedded-resource overloads in
`ResourceAssemblyHandlersTests`, each over the wire.

`MapFileSystemFolder` keeps to its root since 2026-10-04. Its guard was
`Path.Combine(root, path)` followed by `GetFullPath(...).StartsWith(root)`, and
over the wire that served a file from outside the root. The request parser
refuses `../` in every spelling tried - plain, `%2e`, with `%2f` or `%5c`, or
double-encoded - but lets a colon through, and on Windows `C:secret.txt` is a
drive-relative path: `Path.Combine` returns it in place of the root, it
resolves against the current directory of drive `C:`, and `StartsWith` without
a trailing separator counted `...\site-secrets\` as inside `...\site`. The root
is now resolved once and ends in a separator, the request path is appended with
`Path.Join`, which never discards the root, and on Windows, where no file name
can hold one, a colon is refused - which also ends `a.txt::$DATA` as a second
name for `a.txt` that neither the content type nor an `HTMLTemplateHandler`
recognised. Two more defects were found the same way. A missing file, a
directory or a device name was a `500` whose body was the exception message,
that is the absolute path on the server, for any mistyped URL; it is a `404`
now, and an unexpected exception goes to `HTTPStandardHandlers.Logger` rather
than to the client. A relative `ResourcePath` refused every file.
`MapFileSystemFolderTests` sends each of these as a raw request and also pins
the parser's refusal of dot-segments, which the guard relies on.

The folder's own URL serves its `DefaultFilename` since 2026-10-28. Both
overloads registered only `URLTemplate/{ResourceName..}`, so `/files` and
`/files/` had no route and answered `405`, and the `DefaultFilename` parameter
was never reached by a request. They now register the template itself as well,
as `MapResourceAssemblyFolder` always has, with the same handler. On
`HTTPExtAPI` that includes the same authentication, so the folder's index is
not the one file served without it. A folder without its `DefaultFilename` is a
`404` like any other miss. A subdirectory's URL is still a `404` rather than its
own index: directory listings and per-directory defaults are the application's.
`RegisterFileSystemFile` has no root to keep to - it opens whatever its
`ResourceFilenameBuilder` returns - so confining that is the caller's job, as
its documentation now says.

`RegisterFileSystemFile` was fixed on 2026-10-28 for the rest of what
`MapFileSystemFolder` had been fixed for. A directory was a `500` carrying the
absolute path; `File.Exists` was asked only when a `DefaultFile` was given, so
on Windows `nul` was opened as the NUL device and answered `200`; and the
content type was decided once, at registration, from the builder's answer to
empty parameters, which made every file `application/octet-stream`. Anything
that is not a file is a `404` now, decided before anything is opened, an
exception while opening goes to the log, and the type comes from the extension
of the file served unless the caller names one. Its builder receives the URL
parameters URL-decoded, which the documentation had said it did not.

The embedded-resource overloads needed no fix. Two things about them a caller
would not guess: a manifest resource has a flat, dot-separated name, so
`assets/app.js` and `assets.app.js` are the same resource; and the `Hostname`
that six of the eleven take is not used, an `HTTPAPI` being bound to its
hostnames when it is created - the parameter is left over from the
`HTTPServer`-based API these methods were written for, and their documentation
says so now.

The same rework had left one more `X` behind: `HTTPRequestHandlersX`, the
record of handlers the routing tree passes around, with no twin to be told
apart from. It is `HTTPRequestHandlers` since 2026-10-04. Nothing outside
Hermod named it, so the rename reaches no other repository; inside, the name
was free as a type and taken as a member - `MethodNode` has a property of
exactly that name - which C# allows.

Both `OPTIONS` forms are answered by the server itself (RFC 9110 §9.3.7): a
request for a routed resource that has no `OPTIONS` handler is answered
`204 No Content` with `Allow`, because the router is the only thing that knows
the method set. A registered `OPTIONS` handler takes precedence. Every `Allow`
the server emits — including the one on a `405` — lists `OPTIONS` as well, since
the resource does support it (RFC 9110 §10.2.1).

Until 2026-10-03 the resource-level form was answered `405` unless a handler had
been registered by hand, while the rejection already carried the method set it
was being asked for.

`HEAD` is answered by the `GET` handler of the same resource when no `HEAD`
handler was registered, since 2026-10-03. RFC 9110 §9.1 makes `GET` and `HEAD` a
`MUST` for every general-purpose server, and §9.3.2 defines `HEAD` as `GET`
without the content; a registered `HEAD` handler still takes precedence. Before
that, every `GET` route had to register `HEAD` by hand or answer `405` — which
the `Allow` field then confirmed, so a client that asked was told the truth about
a server offering less than it had to.

Nothing is special-cased for the body: the writer already omits it for a `HEAD`
request, starts no chunk worker and no event-stream worker, so the header fields
are the ones `GET` would have sent, as §9.3.2 asks. A chunked `GET` answered as
`HEAD` therefore carries `Transfer-Encoding: chunked` and no body, and the
connection stays usable — RFC 9112 §6.3 item 1 ends any `HEAD` response at the
blank line whatever the framing fields say.

**It costs what the `GET` costs.** The handler generates the content and the
writer drops it. §9.3.2 is explicit that servers prefer minor header
inconsistencies to generating and discarding content, so a handler for which that
matters should read `Request.HTTPMethod` and return the header fields alone — a
64 MiB download answered as `HEAD` otherwise allocates 64 MiB to send nothing.

What a resource advertises is one definition rather than a rule per answer:
`PathNode.AdvertisedMethods` is the registered methods plus the two the server
adds itself (`HEAD` where `GET` exists, `OPTIONS` always), and the `405`, the
automatic `OPTIONS` and `HTTPServer.GetRegisteredMethods` — through which the
CORS pipeline builds `Access-Control-Allow-Methods` — all read it. A resource
whose `OPTIONS` names a method its `405` does not is one resource giving two
accounts of itself.

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
  `COUNT`, `FILTER`, `STATUS`, `PATCH`, and others declared by `HTTPMethod`;
  `COMMIT`, not safe but idempotent, commits a change set to a history, with
  the client extension `COMMIT(path, content, contentType)`.
- Additional application methods can be parsed and registered with their safe
  and idempotent metadata.

Having a method value means that it can be parsed, serialized, and routed. It
does not create resource semantics automatically; a handler must be registered
for the method and path.

**`TRACE` is deliberately not implemented**, and since 2026-10-04 says so with
`501` rather than the `405` routing used to give: RFC 9110 §9.1 keeps `405` for
a method "recognized and implemented, but not allowed for the target resource"
and puts one "unrecognized or not implemented" on the `501` side, and this
refusal is the server's for every resource at once. §9.1 also makes the choice
a choice — every method but `GET` and `HEAD` is OPTIONAL.

The reason is §9.3.8's own: a `TRACE` response carries the request's fields
back, so the final recipient "SHOULD exclude any request fields that are likely
to contain sensitive data" — a judgement about `Authorization`, `Cookie` and
whatever an application invented, which a library would make once, for
everybody, and wrong. Cross-Site Tracing was that mistake in the browsers of
2003; modern ones forbid the method outright. An application that wants to
reflect registers a `TRACE` handler and keeps the judgement where it belongs.

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
- Chunk data is handed over as it arrives: a read returns once it has octets,
  rather than filling the caller's buffer. Until 2026-11-20 it read on, and a
  connection that ended inside a chunk took the octets already in the buffer
  with the exception, so a body cut short in its first chunk looked as if none
  of it had come.
- Trailers are parsed after the terminal chunk and exposed as trailing headers.
- Body-size and metadata limits apply while streaming; buffering the entire
  encoded message is not required.
- Malformed framing is refused as malformed: every rejection raises
  `HTTPInvalidChunkException`, a `FormatException`, so a caller can tell bad
  input from a defect in the decoder without catching `Exception`. One of the
  eleven throw sites — the synchronous CRLF check — raised a bare
  `System.Exception` until 2026-10-03, where its asynchronous sibling thirty
  lines above raised the right one for the same condition.

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
| A digest it cannot compute again | A handler's `Content-Digest` or `Repr-Digest` in an algorithm other than `sha-256` and `sha-512`, or one that does not parse. Compressing would leave it false, and dropping it is not the filter's to decide (RFC 9530, Section 2). |

When it does compress, three fields follow the octets: `Vary` gains
`Accept-Encoding`, merged rather than overwritten (Section 12.5.5), a strong
`ETag` gains the coding, because the encoded and the identity form are different
representations (Section 8.8.3), and `HEAD` is treated exactly like `GET` so the
two agree on `Content-Length` (Section 9.3.2). Since 2026-10-28, so do a
handler's `Content-Digest` and `Repr-Digest`: they were copied on verbatim,
digests of the identity octets on a gzipped response, and are computed again
over the compressed ones, in each algorithm the handler used.

## Digest fields (RFC 9530)

`Content-Digest` is a digest of the octets one message carries, `Repr-Digest`
one of the whole selected representation, and the two `Want-…` fields ask for
either, with a preference from 0 (not acceptable) to 10. All four are typed
since 2026-10-28, in both directions: `ContentDigest`, `ReprDigest`,
`WantContentDigest` and `WantReprDigest` on `AHTTPPDU` and its builder, the
getters joining repeated field lines into one Dictionary. `HTTPDigest` builds,
parses, selects and checks them, in `sha-256` and `sha-512`, the two algorithms
the registry marks active. It was written for HTTP/2 and lived in `HTTP2/Core`,
where this stack could not reach it; it is in `Hermod/HTTP/` now, beside
`HTTPDigestVerification` and `HTTPDigestMismatchException`.

The RFC's one sentence that matters here is that both digests are of the octets
after content coding. Three places in this stack touch them, and each follows
from it:

| | |
|---|---|
| `AutomaticContentCompression` | Computes a handler's digests again over the compressed octets, and does not compress a response whose digest it cannot compute again. |
| `AHTTPServer.AutomaticContentDigests` | **Off by default.** Gives every response that carries content a `Content-Digest` of what is sent, after the compression, in the algorithm the request's `Want-Content-Digest` prefers, `sha-256` without one, and none when it rules out both. A handler's own digest is kept. A `HEAD`, a `1xx`, `204`, `205` and `304` carry no content and get none, and neither does a streamed body, whose octets are not known before they are sent. |
| `AHTTPClient.VerifyDigests` | **Off by default.** Sends `Want-Content-Digest` unless the request has one, and checks the response's `Content-Digest`, in its header section, its trailer section, or both, against the octets as they arrived. A trailer section is seen where the chunked body is read on arrival, the client's default (`ConsumeChunkedTransferEncodingImmediately`); the other path reads the stream and keeps no trailers, so a digest there is `NotPresent`. With `AutomaticDecompression` the body is then read whole and decoded afterwards, where otherwise the decoder sits in the stream. The outcome is `HTTPResponse.DigestVerification`, which keeps "there was none" (`NotPresent`) and "in an algorithm not computed here" (`Unsupported`) apart from `Match`. Content that does not match is not handed on: the caller gets a `ClientError` with `DigestVerification` `Mismatch`, and the request is not tried again, since the framing was sound. The HTTP/2 client throws an `HTTPDigestMismatchException` there. |

In the request direction, a handler calls `request.VerifyContentDigest()`
before it undoes a content coding. Afterwards the method refuses, with an
`InvalidOperationException`, because the octets the digest is of are gone and a
`Mismatch` would blame the client for it.

Moving the grammar found it lenient where RFC 9651 says it must not be. A
field that fails to parse is ignored whole (Section 4.2), "and field
specifications that use Structured Fields are not allowed to loosen this
requirement"; one that breaks its own definition's constraints is ignored the
same way (Section 2.2). The grammar read both apart and kept what it could, and
HTTP/2 gets the strict reading as well:

- `md5=:not-base64!:, sha-256=:…:` verified against its second half;
- an upper-case key was lowered and accepted;
- a member that was not a Byte Sequence was skipped, and the rest of the field
  kept;
- a `Want-…` preference outside 0 to 10 was clamped into it, and a bare key was
  read as preference 1.

`Content-MD5` stays typed and readable. RFC 7231, Appendix B, removed it from
HTTP, and its doc comments say so and point here.

The server offers `br` and `gzip` where the client accepts `br`, `gzip` and
`deflate`: `deflate` is the one the wire disagrees about, so it is read and not
written.

## Content-Disposition (RFC 6266)

`Content-Disposition` has been typed since 2026-10-29: a builder sets it with
`ContentDisposition = HTTPContentDisposition.Attachment("report.pdf")`, and
`ContentDisposition` on a request or a response reads it. The value is
`HTTPContentDisposition`. It was a string before, and code that set a string
now has to say which disposition it means. The model is in `Hermod/HTTP/`.

| | |
|---|---|
| reading | Section 4.1. A value that does not fit the grammar, or repeats a parameter, which the section makes invalid, reads as null, and so do two field lines. The type and the parameter names are kept in lower case. An unknown type is an attachment (Section 4.2). `filename*` wins over `filename`, and falls back to it when it does not decode. Extension parameters are kept in order, and a `name*` that decodes replaces `name`. |
| sending | Appendix D. A file name printable ASCII can carry goes out as `filename`, as a token or quoted. Any other, and one with `\`, `"` or a `%` before two hex digits, goes out as an ASCII `filename` first and `filename*` in UTF-8 after it. In the fallback the German letters are spelt out (`ä` becomes `ae`), other accents are dropped, and what is still not plain becomes `_`. A file name with a control character in it is refused. |
| storing | Section 4.3 makes the file name advisory, and `FileName` is what was sent. `SafeFileName` keeps the last path segment, after `/` or `\`. It drops control characters and leading and trailing whitespace, and turns the characters Windows does not allow, among them the `:` of a drive-relative `C:name`, into `_`. It refuses `.`, `..`, `~` and the Windows device names, with or without an extension. What it does not do is check the extension against the media type, which Section 4.3 leaves to a recipient that goes by extensions. |

## Links (RFC 8288)

`HTTPResponse.Builder.Link` sends links and `HTTPResponse.Link` reads them,
since 2026-10-29. The value is `HTTPLinks`, a list of `HTTPLink`. Each link has
its target, its relation types, an optional anchor, and the target attributes
Section 3.4 defines (`hreflang`, `media`, `title`, `type`) or that an
extension adds. The getter joins repeated field lines, which Section 3.5 says
are the same as one line with commas. The model is in `Hermod/HTTP/`, for
HTTP/2 and HTTP/3 to use as well.

| | |
|---|---|
| sending | `new HTTPLink(target, "next")` checks what a sender must get right: the target and the anchor are URI references, every relation type is a registered one or an absolute URI (Section 3.3), and `type` is a media type. `rel`, `anchor`, `title` and `media` are quoted and `hreflang` is a token, the forms Section 3 calls the most interoperable. A title that is not printable ASCII, or has a language, goes out as `title*`. |
| reading | Appendix B. A link-value without `rel` is no link. Only the first `rel`, `anchor`, `media`, `title`, `title*` and `type` count. A token and a quoted-string are the same value. Relation types are kept in lower case, since they compare case-insensitively. `title*` wins over `title`, and falls back to it when it does not decode. Parsing stops at the first thing that is not a link-value, and keeps the links before it. |
| RFC 8187 | `HTTPExtendedValue` encodes in UTF-8, as a producer MUST, and decodes UTF-8 and the ISO-8859-1 that Section 3.2.2 encourages. It refuses any other charset, a malformed percent escape, a character outside `attr-char` and an octet sequence that does not decode. `Content-Disposition`'s `filename*` (RFC 6266) uses it too. |

One deviation from Appendix B, which is advisory: read literally, B.3 stops at
the comma between two link-values without consuming it, and B.2 then finds no
`<` and returns. That would end every field after its first link-value, and
contradict Section 3.5, so the comma is consumed.

Relative targets are kept as sent. Resolving them against the request's URL
(Section 3.1) is the caller's step, since only the caller knows that URL.

## Problem details (RFC 9457)

`HTTPProblemDetails`, in `Hermod/HTTP/`, is the problem details object of
Section 3: `type`, `status`, `title`, `detail` and `instance`, and the extension
members beside them. It is served as `application/problem+json`,
`HTTPContentType.Application.ProblemJSON`, without a charset, because Section 6
registers the type without parameters. Since 2026-10-29.

| | |
|---|---|
| a handler | `HTTPResponse.Builder.SetProblemDetails(problem)` sets the content, the content type and the status code the problem names, which Section 3.1.2 says the response MUST carry. `HTTPProblemDetails.ForStatus(code, detail)` is the `about:blank` problem, whose title is the reason phrase (Section 4.2.1). |
| a client | `response.TryParseProblemDetails(out problem)` reads an `application/problem+json` response. A member of the wrong type is ignored, as Section 3.1 requires: `"type": 42` leaves the type `about:blank`, and a `status` that cannot be a status code is dropped. Extensions are kept. |
| the server's own errors | `404`, `405`, `408`, `413`, `417`, `421`, the `400`s for a malformed body, and the `500`s go out as `application/json` with a `description`, as they always have. A request whose `Accept` prefers `application/problem+json` gets them as problem details. |

`Accept` prefers problem details when it names `application/problem+json`
itself, with a quality above 0 and no lower than the one it gives
`application/json`, judged by the most specific range that matches
`application/json`. `*/*` alone does not ask. A client that lists both at equal
quality, as the RFC's first example does, gets problem details. The decision is
`HTTPProblemDetails.IsPreferredBy`'s. Negotiating rather than switching keeps
every existing client's `description`, and Section 4 names content negotiation
as the way to add problem details to an API that has a format already.

The server's errors as problem details are `about:blank`: they are protocol
errors, which Section 4 says a status code describes better than a problem type
does. The title is the reason phrase, `description` becomes `detail`, and the
other members, such as `request` or `maximumBytes`, stay as extensions. A `500`
with `IncludeStackTracesInErrorResponses` names the exception's type in a member
called `type`. In problem details that name is the problem type, so there it is
`exceptionType`.

Not negotiated:

- errors raised before the request's header section is parsed, which have no
  `Accept` to read;
- the plain-text errors: `TRACE`'s `501` and the two HTTP/1.0 refusals.

Reading `Accept` on an error path found that `AcceptType.TryParse` threw on a
weight that is not a number (`q=abc`) and on an element with nothing in it. With
the errors negotiating, a malformed `Accept` would have cost a request its error
response, and the connection went silent. Such an element is dropped now, and
the rest of the field stands.

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

## The client: challenges and redirects

Since 2026-11-20 the HTTP/1.x client does both of what the HTTP/2 client
already did, with the same two classes, which moved from `HTTP2/Core/` to
`HTTP/` for it: `HTTPClientAuthenticator` and `HTTPRedirect`. Both are off by
default, and neither is a conformance requirement: RFC 9110 lets a user agent
hand a `401` or a `3xx` to its user, and that is still what happens unless the
client is told otherwise.

- **`Credentials`** answers a `401` (RFC 9110 §11.6.1) once, with the strongest
  scheme the `WWW-Authenticate` field lines offer that it holds credentials
  for: Digest (RFC 7616, SHA-256 or MD5, `qop=auth`), then Bearer, then Token,
  then Basic. Nothing is sent before a server asks. A request that already
  carries `Authorization` is the caller's choice and is not answered for, and
  a second `401` is handed back rather than answered again. The Digest nonce
  count lives as long as the credentials it counts for, since RFC 7616 has it
  increase with every request made with one nonce.
- **`MaxRedirects`** follows `301`, `302`, `303`, `307` and `308` (§15.4) for
  that many hops. `POST` becomes `GET` after the first three, and the fields
  about the content go with the body; `307` and `308` send method and body
  again. `300` and `304` are not redirects to follow. A relative `Location`
  resolves against the request as it was addressed. Only targets on the
  client's own origin are followed: a client is one connection to one origin,
  and a `Location` elsewhere comes back unfollowed with the `3xx` intact. That
  boundary is also what keeps `Authorization` from reaching an origin that did
  not ask for it. The targets followed are on `HTTPResponse.RedirectChain`.

Each hop is answered by the same `401` logic, from the request that was
redirected rather than from the one that answered a challenge: Digest signs
the request-target, so a credential for one hop is wrong for the next. A body
a chunk worker writes cannot be sent twice, so a request with one is neither
answered for nor followed through `307` or `308`. A `3xx` or `401` whose body
is an event stream comes back as it is: it is the one body this client does
not read before returning, it need never end, and the connection is the
stream's from then on.

## The client: downloads that resume

`AHTTPClient.DownloadAsync(path, destination)` is the third thing the HTTP/2
client did alone until 2026-11-20 (RFC 9110 §14 and §13.1.5). Its body goes to
the destination as it arrives, not into `HTTPBody`: every other request keeps
a body only once it is whole, and a body discarded when it breaks off is
nothing to resume from. `HTTPContentRange` and `HTTPValidators` moved from
`HTTP2/Core/` to `HTTP/` for it.

- When the body breaks off, the rest is asked for with `Range: bytes=n-` and
  `If-Range`, up to `MaxAttempts` requests in all. The validator is a strong
  entity-tag, or else the `Last-Modified` date; a weak entity-tag promises
  equivalence, not the same octets, and does not guard a resume. Without a
  validator a broken body is reported, not sent for again.
- A `206` that starts where the destination stops is appended. A `200`, or a
  `206` that starts anywhere else, means the representation changed, and the
  destination starts over, which it must be able to seek for. A `416` whose
  `Content-Range` names exactly the length already written finishes the
  download: the body broke off at its very end.
- `Accept-Encoding` is `identity` unless the request builder says otherwise,
  since ranges of a compressed representation would splice compressed parts.
  With `VerifyDigests` the request asks for `Repr-Digest` instead of
  `Content-Digest`, and the octets of all parts together are checked against
  it: no single range's content digest says anything about the whole.
- Only the body of a `200` or `206` reaches the destination, so an error page
  cannot pass for the representation. `Credentials` and `MaxRedirects` apply
  as they do to every request.

`HTTPDownloadResult` says how it ended: `IsComplete`, the octets written, the
attempts, resumes and restarts, the validator and the digest check. A download
that did not finish has the status `ClientError`, which no server sends, and
says why in `Error`. Nothing is thrown, as nothing is by the rest of this
client.

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

## Resources: negotiation, preconditions and ranges

Since 2026-11-20 a resource can leave RFC 9110's core mechanics to the
library. Its handler says what the representation is, and the library answers
the request:

```csharp
httpAPI.AddResource(
    HTTPPath.Root + "report.txt",
    (path, headers, cancellationToken) => Task.FromResult<HTTPResource?>(
        new HTTPResource {
            Body          = bytes,
            ContentType   = "text/plain; charset=utf-8",
            LastModified  = modified
        }
    )
);
```

| | |
|---|---|
| Preconditions (Section 13) | `If-Match`, `If-Unmodified-Since`, `If-None-Match` and `If-Modified-Since` in Section 13.2.2's order: a `412`, or a `304` with the validators and no content |
| Ranges (Section 14) | One range is a `206` with `Content-Range`, several are one `multipart/byteranges` body, and none satisfiable is a `416` with `bytes */length`. `If-Range` lets a range through only on the current strong entity-tag or the exact date. An unknown unit, a malformed set or more than 100 ranges is ignored, and the answer is a `200` (Section 14.2) |
| Negotiation (Section 12) | An `HTTPVariantHandler` returns several representations. `Accept`, `Accept-Encoding` and `Accept-Language` pick one, `Vary` names every axis on which they differ, and a `406` comes only when every one is refused with `q=0` |
| Validators | An `ETag` derived from the octets unless the representation states one, `Last-Modified` when it has one, `Accept-Ranges: bytes` |
| `QUERY` (RFC 10008) | With a query handler, the result runs through the same pipeline as a `GET`. Content without a `Content-Type` is a `400`, and `Content-Location` can name a URI for the result |

`CompressResponses` and `ContentDigests` are off by default, as on HTTP/2.
The server's `AutomaticContentCompression` and `AutomaticContentDigests` apply
to these answers as to any other handler's.

It is the code HTTP/2 has used all along through `HTTPSemantics.Wrap`. The
rules moved out of `HTTP2/Core` into the version-neutral
`HTTPResourceSemantics` in `Hermod/HTTP/`, together with `HTTPResource` and
the three handler delegates, and `HTTP2.HTTPSemantics.Wrap` kept its
signatures. `HTTPResourceHandlers` adapts it to HTTP/1.x. The header fields go
in under their lower-case names, and the target goes in as sent, in origin
form with its query. The answer comes back with each field under its usual
spelling, and `Content-Type` verbatim: the typed field keeps a charset and
nothing else.

Two things stay the server's:

- **`HEAD`** is derived from the `GET` handler, which is called with the
  request's own method. A conditional `HEAD` therefore gets its `304`, and a
  `HEAD` with a `Range` gets the whole length, since Section 14.2 defines
  ranges for `GET` alone.
- **`OPTIONS` and the `405`** come from the methods registered. `QUERY` is
  among them only where a query handler was given.

It is opt-in. A handler registered with `AddHandler` still sees every
precondition and range as typed fields and decides for itself. Before, the
stack made this decision twice, in opposite directions: a library layer on
HTTP/2, and every HTTP/1.x handler's own work.

### Static files

Static files go through the same layer since 2026-11-21 (H-43 in
HTTP1ConformanceTests). Before, `MapFileSystemFolder`,
`MapResourceAssemblyFolder`, `MapResourceAssembliesFolder`,
`RegisterFileSystemFile` and `RegisterResourcesFile` sent a `200` with neither
`ETag` nor `Last-Modified`. So no request for a static file could be answered
`304` or `206`, although RFC 9110, Sections 8.8.2.1 and 8.8.3 say an origin
server SHOULD send both where it can determine them. `SinglePageAppHandler`
answered `If-None-Match` itself and nothing else.

- **Validators of a file on disk.** `Last-Modified` is the time of its last
  write, never later than the response's `Date` (§8.8.2.1). The `ETag` is made
  of that time and the length, as nginx and Apache httpd make theirs, so that
  no request hashes the file.
- **Validators of other content.** An embedded resource and a page through an
  `HTMLTemplateHandler` get the entity tag of their content and no
  `Last-Modified`.
- **Files up to `HTTPStandardHandlers.MaxBufferedFileSize`** (16 MiB by default)
  are read into memory and answered by the layer: `304`, `412`, `206` and `416`.
- **Larger files** are streamed as before. They keep their validators and the
  `304` and `412` of their preconditions. They get no ranges and no
  `Accept-Ranges`, because ranges need the representation in hand.
- **`SinglePageAppHandler`** still chooses the content coding itself, from the
  encodings it caches per file, and hands the layer that one representation.
  The entity tag names the coding, and `Vary: Accept-Encoding` stays on every
  answer, the `304` included.
- **Dating SPA files.** A `FileSystemContentSource` dates its files. An
  `EmbeddedContentSource` does not, and neither does a transformed
  `index.html`.

## Headers, authentication, and application semantics

Hermod has typed models for many common request and response fields and also
allows extension fields. This includes content negotiation, representation
metadata, authorization, CORS fields, cookies, conditional request fields,
ETags, range-related fields, and WebDAV fields.

`Strict-Transport-Security` is typed since 2026-10-04: `StrictTransportSecurity`
carries `max-age`, `includeSubDomains` and the non-standard `preload`, parses
what RFC 6797 §6.1 permits and refuses what it does not — a field without
`max-age`, a directive appearing twice, a value that is not `delta-seconds` —
while ignoring unrecognised directives, as §6.1 item 5 requires. Emitting the
text was already possible through `SecurityHeaderOptions`; what was missing was
a value anything could read. **Whether to send it stays the application's
decision and the default stays "nothing"**: a policy is a promise about every
future request to a host, and a host that promises and then loses its
certificate has locked its own users out.

`Alt-Svc` and `Alt-Used` (RFC 7838) are typed since 2026-10-28. Over HTTP/1.1
the field is the only way to announce an alternative, and the grammar for it
existed all along, written for HTTP/2's ALTSVC frame in `HTTP2/Core`, where
this stack could not reach it. `HTTPAlternativeService` is version-neutral in
`Hermod/HTTP/` now, and `AlternativeServices` is the field's value: the
alternatives in order of preference, or `clear`. A handler sets
`HTTPResponse.Builder.AltSvc`, and the client reads `HTTPResponse.AltSvc`,
which joins repeated field lines first, since the field is a list.
`HTTPRequest.AltUsed` carries §5's field. Moving the grammar found three places
where it did not follow §3, and all three are fixed for HTTP/2 as well:

- `clear` beside alternatives was skipped as an entry without `=`, and the
  alternatives beside it kept. §3 has it invalidate everything, "including
  those specified in the same response";
- `Clear` was not `clear`, although an ABNF literal is case-insensitive;
- an ALPN name outside the token alphabet went onto the wire as it was:
  `http/1.1` is `http%2F1.1`, and `%` itself is `%25`.

A malformed alternative is still skipped and the well-formed ones beside it
kept, because this is advisory routing information. A value with nothing usable
in it at all is refused rather than read as an empty list, which a client could
not tell apart from `clear`. **Announcing an alternative stays the
application's**, as with HSTS: it claims the origin is served there too, and
only the application knows whether it is. The client records what it reads and
does not act on it. Acting means connecting with another protocol, and the
HTTP/1.x client speaks one.

Timestamps go through `HTTPDate` (RFC 9110 §5.6.7), which since 2026-10-03
accepts **all three** HTTP-date formats — the preferred IMF-fixdate and the
obsolete RFC 850 and `asctime()` ones, which a recipient MUST accept — and
generates only the first, which is the only one a sender may. The two obsolete
formats were not parsed before, and the general parser in use named no culture,
so what a date field accepted depended on the machine it ran on. The RFC 850
format's two-digit year follows §5.6.7's sliding rule rather than the
calendar's fixed pivot: a timestamp more than fifty years in the future means
the most recent past year with the same last two digits.

The presence of a typed field does not imply an automatic policy engine:

- Basic and Bearer credentials can be parsed and serialized; handlers decide
  whether credentials are valid and authorized.
- Conditional fields and ETags are available to handlers; Hermod is not a
  shared HTTP cache. A resource registered with `AddResource` has its
  preconditions evaluated by the library instead, and so does a static file
  since 2026-11-21.
- Range and `Content-Range` fields are modeled; handlers select and generate
  partial representations, or leave it to `AddResource`.
- CORS fields are modeled, and the one part no handler can reach — the
  preflight — is available as an opt-in `HTTPCORSPipeline` carrying a
  `CORSPolicy` the application states. A server that installs none does no CORS,
  and an ordinary `OPTIONS` still goes to the router. The pipeline deliberately
  does not decorate ordinary responses: that is per-resource, and the handler
  already sets it.
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

The HTTP/1.x regression selection is the fixtures below, by concern, all of
them under `HermodTests/HTTP/`. The filter matches a substring of each test's
fully qualified name, so `HTTPClientTests` also takes in
`Concurrent/HTTPClientTests` and the .NET interop fixtures under `dotNET/` and
`Concurrent/dotNET/`: `DotNetHTTPClientTests`, `HTTPClientTests_MinimalAPI`
and `DotNetHTTPClientTests_MinimalAPI`.

- **Framing, connections, the client and the server end to end:**
  `HTTPClientTests`, `HTTPServerSocketRegressionTests`,
  `HTTPClientProtocolRegressionTests`, `HTTP11AuditRegressionTests`,
  `HTTP_TCPClientTests`, `ChunkedFramingTests`, `TransferCodingTests`,
  `BodilessResponseTests`, `HTTPLineEndingTests`, `HTTPClientPoolTests`,
  `NagleTests`, `HTTPClientDownloadTests`, `AbsoluteFormTests`
- **Methods and status codes, interim responses included:**
  `HTTPStatusCodeTests`, `HTTPHeadDerivedFromGetTests`,
  `HTTPServerTraceTests`, `HTTPCORSPreflightTests`, `EarlyHintsTests`,
  `ProblemDetailsTests`, `HTTPClientRedirectTests`
- **Resources — negotiation, preconditions and ranges:**
  `HTTPResourceSemanticsTests`, `StaticFileValidatorsTests`
- **TLS, and the origin a connection is for:**
  `HTTPServerListenerMatrixTests`, `HTTPServerALPNTests`,
  `HTTPServerAllowedTLSProtocolsTests`, `HTTPSClientTLSByDefaultTests`,
  `HTTPSClientHandshakeTimeoutTests`, `AcceptedConnectionTests`,
  `MisdirectedRequestTests`
- **Content codings, in both roles:** `ContentEncodingHeaderTests`,
  `ContentCodingStreamTests`, `ClientContentDecodingTests`,
  `ServerContentCompressionTests`
- **Typed fields:** `ResponseHeaderFieldTests`, `StructuredFieldsTests`,
  `HTTPDateTests`, `HTTPCookiesTests`, `ForwardedFieldTests`,
  `AlternativeServicesTests`, `DigestFieldsTests`, `LinkFieldTests`,
  `ContentDispositionTests`, `StrictTransportSecurityTests`
- **Authentication:** `HTTPDigestAuthenticationTests`, `HTTPClientChallengeTests`

Under `Tests.HTTP` and deliberately not in it: the version-neutral models of
URLs, query strings, hostnames and methods, IPv6 zone identifiers included;
the `HTTPAPI` and `HTTPExtAPI` application layer, with its users, keys,
sessions, passkeys and entities; the routing helpers and the static-content
handlers; the `HTTPTestServer`; logging; Server-Sent Events, an application
protocol of their own; and WebSockets, which have a filter of their own,
`FullyQualifiedName~Hermod.Tests.HTTP.WebSockets`. `HTTPClientLazyDNSClientTests`
is not in it either: its subject is when a client builds its DNS client, which
is a cost and not the protocol.

A fixture joins the selection when it is named here, and not before. Until
2026-11-03 the filter had eleven names, and twenty-seven protocol fixtures were
outside it, all but `HTTP_TCPClientTests` added after the list was first
written: the typed fields, `103`, `421`, `HEAD`, `TRACE`, ALPN and the TLS
versions among them, many of them the regression tests of findings from
HTTP1ConformanceTests. On master `e0a9413e` the eleven names select **381** tests
and the thirty-eight below select **782 passing, 0 failed, 0 skipped**.
`NagleTests` came on 2026-11-18 with `19db5cad` and joined the next day, which
makes thirty-nine. On master `4b8be28a` they select **786 passing, 0 failed,
0 skipped**. `HTTPClientRedirectTests` and `HTTPClientChallengeTests` came on
2026-11-20 with the client's half of finding H-38 and joined with it, which
makes forty-one, and H-41 added eight cases to `HTTPClientProtocolRegressionTests`
the same day. On master `7d821a1c` they select **819 passing, 0 failed,
0 skipped**: the 786, H-38's 25 and H-41's 8. `HTTPClientDownloadTests` came
with H-38's second half and joined with it, which makes forty-two, and the
same change put two cases into `ChunkedFramingTests`: **833**, the 819 and
their 14. `HTTPResourceSemanticsTests` came with finding H-37 and joined with
it, which makes forty-three: **880**, the 833 and its 47. `AbsoluteFormTests`
came with finding H-42 and joined with it, which makes forty-four, and the
same change put four cases into `MisdirectedRequestTests` and one more into
`HTTPServerSocketRegressionTests`: **904**, the 880 and their 24.
`StaticFileValidatorsTests` came with finding H-43 and joined with it, which
makes forty-five: **920**, the 904 and its 16. The static-content
handlers' own fixtures stay outside, as said above. This one tests what RFC
9110 asks of the answers they give.

Run it with:

```powershell
dotnet test HermodTests\HermodTests.csproj --filter "FullyQualifiedName~HTTPClientTests|FullyQualifiedName~HTTPServerSocketRegressionTests|FullyQualifiedName~HTTPClientProtocolRegressionTests|FullyQualifiedName~HTTP11AuditRegressionTests|FullyQualifiedName~HTTP_TCPClientTests|FullyQualifiedName~ChunkedFramingTests|FullyQualifiedName~TransferCodingTests|FullyQualifiedName~BodilessResponseTests|FullyQualifiedName~HTTPLineEndingTests|FullyQualifiedName~HTTPClientPoolTests|FullyQualifiedName~NagleTests|FullyQualifiedName~HTTPStatusCodeTests|FullyQualifiedName~HTTPHeadDerivedFromGetTests|FullyQualifiedName~HTTPServerTraceTests|FullyQualifiedName~HTTPCORSPreflightTests|FullyQualifiedName~EarlyHintsTests|FullyQualifiedName~ProblemDetailsTests|FullyQualifiedName~HTTPServerListenerMatrixTests|FullyQualifiedName~HTTPServerALPNTests|FullyQualifiedName~HTTPServerAllowedTLSProtocolsTests|FullyQualifiedName~HTTPSClientTLSByDefaultTests|FullyQualifiedName~HTTPSClientHandshakeTimeoutTests|FullyQualifiedName~AcceptedConnectionTests|FullyQualifiedName~MisdirectedRequestTests|FullyQualifiedName~ContentEncodingHeaderTests|FullyQualifiedName~ContentCodingStreamTests|FullyQualifiedName~ClientContentDecodingTests|FullyQualifiedName~ServerContentCompressionTests|FullyQualifiedName~ResponseHeaderFieldTests|FullyQualifiedName~StructuredFieldsTests|FullyQualifiedName~HTTPDateTests|FullyQualifiedName~HTTPCookiesTests|FullyQualifiedName~ForwardedFieldTests|FullyQualifiedName~AlternativeServicesTests|FullyQualifiedName~DigestFieldsTests|FullyQualifiedName~LinkFieldTests|FullyQualifiedName~ContentDispositionTests|FullyQualifiedName~StrictTransportSecurityTests|FullyQualifiedName~HTTPDigestAuthenticationTests|FullyQualifiedName~HTTPClientRedirectTests|FullyQualifiedName~HTTPClientChallengeTests|FullyQualifiedName~HTTPClientDownloadTests|FullyQualifiedName~HTTPResourceSemanticsTests|FullyQualifiedName~AbsoluteFormTests|FullyQualifiedName~StaticFileValidatorsTests"
```

## Deliberate exclusions and qualification

Hermod's HTTP/1.x origin client/server is feature-complete for its intended
scope, with these explicit boundaries:

- It is not an HTTP forward proxy or gateway; `CONNECT`, forwarding a request
  in absolute form to another origin, proxy authentication, and intermediary transformations are out of
  scope.
- It does not provide a shared RFC 9111 cache.
- It applies conditional, range and QUERY semantics only to a resource
  registered with `AddResource`, and WebDAV semantics not at all.
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
