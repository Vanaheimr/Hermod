# Hermod WebSocket

An explicit HTTP/1.1 WebSocket **server** and **client** implementation
([RFC 6455](https://www.rfc-editor.org/rfc/rfc6455.html)) with optional
**permessage-deflate** message compression
([RFC 7692](https://www.rfc-editor.org/rfc/rfc7692.html)).

Primarily used as transport for OCPP (Open Charge Point Protocol) within the
Open Charging Cloud, but fully generic.


## Conformance

Verified against the [Autobahn WebSocket Testsuite](https://github.com/crossbario/autobahn-testsuite):

| Suite | Result |
|---|---|
| Server, sections 1–10 (framing, pings, fragmentation, UTF-8, limits, close) | 296 OK / **0 FAILED** (2 NON-STRICT, 3 INFO) |
| Client, sections 1–7, 10 | 242 OK / **0 FAILED** (2 NON-STRICT, 3 INFO) |
| Server, compression sections 12/13 | 126 OK / **0 FAILED** |
| Client, compression sections 12/13 | 126 OK / **0 FAILED** |

Implemented (both server and client, unless noted):

- Complete RFC 6455 framing: opcodes, FIN/RSV bits, extended payload lengths,
  masking (client→server frames are masked; the server fails the connection on
  unmasked client frames).
- Strict fragmentation state machine: interleaved control frames are handled,
  a new data frame within a fragmented message or an orphaned continuation
  frame fails the connection (1002).
- Control frame rules: not fragmented, payload ≤ 125 bytes, pong echoes the
  ping payload.
- Strict UTF-8 validation of text messages (RFC 6455 Section 8.1), including
  incremental fail-fast validation across fragment boundaries
  (`IncrementalUtf8Validator`); violations close with 1007.
- Close handshake: close status code validation, close reason UTF-8
  validation, echoing the received status code, bounded close timeout.
- Server handshake validation (RFC 6455 Section 4.2): `Upgrade`, `Connection`,
  `Sec-WebSocket-Key` (Base64, 16 bytes) → `400 Bad Request`;
  `Sec-WebSocket-Version` ≠ 13 → `426 Upgrade Required` with
  `Sec-WebSocket-Version: 13`.
- Client handshake validation (RFC 6455 Section 4.1): HTTP status 101, the
  `Sec-WebSocket-Accept` hash, the `Upgrade: websocket` / `Connection: Upgrade`
  tokens, and rejection of any unsolicited extension or subprotocol in the
  response.
- Client nonces (`Sec-WebSocket-Key`) and all masking keys come from a
  cryptographically secure RNG (RFC 6455 Section 5.3 / 10.3).
- Subprotocol negotiation (`Sec-WebSocket-Protocol`, e.g. `ocpp2.1`) with an
  optional `SubprotocolSelector` delegate on the server, plus an optional strict
  mode (`RequireMatchingSubprotocol`) that rejects the handshake with
  `400 Bad Request` when the client offers only unsupported subprotocols.
- Client auto-reconnect after an unexpected connection loss, with exponential
  backoff and jitter (`ReconnectPolicy`, opt-in; see below). A clean close or a
  fatal protocol violation never triggers a reconnect. An upgrade answered with
  408, 429 or a 5xx - a server not ready yet, a proxy whose service restarts -
  is tried again; one that also says when, in a `Retry-After` (seconds or an
  HTTP date, counted from the server's own `Date`), is not tried again before
  then, up to `MaxRetryAfter`, with the jitter after it rather than either side.
- Incoming text and binary messages have a 64 MiB aggregate limit by default
  (including fragmented messages). Smaller configured limits are checked before
  fragment accumulation and against decompressed data; violations close with
  1009 (Message Too Big). The frame parser also caps individual frames at 64 MiB.
- `RequireAuthentication` defaults to `true`: the server checks stored HTTP
  Basic credentials or raw TOTP credentials before upgrading. Configure an
  account with `AddOrUpdateHTTPBasicAuth` or `ClientTOTPConfig`, or explicitly
  set `RequireAuthentication: false` for public endpoints. Optional handshake
  validators can impose additional restrictions but do not replace this check.
  A server that knows its clients otherwise (a client certificate, a store of
  its own, credentials in the query string) overrides `AuthenticateAsync` and
  calls the base for the defaults; an exception thrown there refuses with 500.
  `Login` is the authenticated login, and `null` on a connection nobody
  authenticated.
- Server handshake hardening: `HandshakeTimeout` (Slowloris protection, default
  10 s), `MaxHandshakeRequestSize` (oversized/unterminated header block, default
  64 KB), `MaxConnectionsPerIP` (connection-flood protection, opt-in), and an
  optional `AllowedOrigins` allow-list (`403 Forbidden`, CSWSH protection; a
  request without an `Origin` header is always accepted).
- Fully asynchronous receive path (no busy-polling), TCP connection-loss
  detection on the client (`IsRemoteTCPConnectionClosed`), bounded write and
  close timeouts (a stalled peer can no longer block `Send()`/`Close()`
  forever).
- Send backpressure limit (`MaxBackpressure` / `BackpressureBehaviour`, the
  uWebSockets "maxBackpressure" model, server and client): when the outgoing
  bytes queued/in-flight to a slow peer would exceed the limit, the connection
  is either closed (1009) or the message dropped (`SentStatus.Dropped`). The
  current amount is exposed as `BufferedAmount`. The closing handshake is exempt.
- Heartbeat / zombie detection: periodic pings (`WebSocketPingEvery`) plus a
  liveness deadline (`MaxOutstandingPings`, default 3). If no frame arrives
  within that many ping intervals, the half-open connection is torn down
  locally (no reserved 1006 sent on the wire). Set `MaxOutstandingPings = 0`
  to disable.
- **permessage-deflate** (RFC 7692): negotiated via
  `Sec-WebSocket-Extensions`, DEFLATE with sync-flush termination (so peers
  using context takeover can decode our stream), RSV1 handling only on the
  first frame of a data message, decompression-bomb guard.

Known limitations:

- `permessage-deflate`: .NET's `DeflateStream` does not expose the DEFLATE
  window size, so custom `server_max_window_bits`/`client_max_window_bits`
  values below 15 are not supported; `no_context_takeover` is always
  negotiated in both directions (permitted by RFC 7692). Offers requiring a
  smaller server window are declined.
- TLS-channel-bound TOTP cannot be verified by this HTTP/1.1 WebSocket server
  because its `SslStream` does not expose TLS exporter material; such credentials
  are rejected rather than silently treated as raw TOTP.
- WebSocket over HTTP/2 (RFC 8441) / HTTP/3 (RFC 9220) is out of scope for
  this HTTP/1.1 implementation.


## Server usage

```csharp
var server = new WebSocketServer(
                 HTTPPort:               IPPort.Parse(8080),
                 SecWebSocketProtocols:  [ "ocpp2.1", "ocpp2.0.1" ],
                 RequireAuthentication:  false,
                 AutoStart:              true
             );

server.EnablePerMessageDeflate     = true;          // RFC 7692, off by default
server.MaxTextMessageSizeIn        = 1024 * 1024;   // 1 MB
server.RequireMatchingSubprotocol  = true;          // reject unknown subprotocols with 400

// Handshake hardening (timeout + max header size are on by default):
server.HandshakeTimeout            = TimeSpan.FromSeconds(10);
server.MaxHandshakeRequestSize     = 64 * 1024;
server.MaxConnectionsPerIP         = 100;           // 0 = unlimited (default)
server.AllowedOrigins.Add("https://ui.example.com"); // empty = disabled (default)

// Send backpressure (0 = disabled by default); must exceed your largest message:
server.MaxBackpressure             = 16 * 1024 * 1024;
server.BackpressureBehaviour       = WebSocketBackpressureBehaviour.CloseConnection;

server.OnTextMessageReceived += async (timestamp, srv, connection, frame, eventTrackingId, text, ct) => {
    await server.SendTextMessage(connection, $"Echo: {text}");
};
```

### On an HTTP path

A WebSocket no longer needs a TCP server of its own. `WebSocketUpgrade.For`
turns any `AWebSocketServer` into an HTTP handler, so one listener, one
certificate and one port can serve frames at one path and ordinary HTTP at
another:

```csharp
var webSocketServer = new WebSocketServer(AutoStart: false);   // never accepts anything itself

var httpServer      = await HTTPServer.StartNew();
var api             = httpServer.AddHTTPAPI();

api.AddHandler(HTTPMethod.GET, HTTPPath.Parse("/xmpp"),
               HTTPDelegate: WebSocketUpgrade.For(webSocketServer));

api.AddHandler(HTTPMethod.PUT, HTTPPath.Parse("/upload"),
               HTTPDelegate: TakeTheFile);
```

The case it was written for is XMPP: RFC 7395 over a WebSocket and XEP-0363
file uploads over HTTP, which no client should have to be told two port numbers
for.

**The handshake is not duplicated.** The handler decides only whether a request
is asking for an upgrade; the connection then goes to
`AcceptUpgradedConnectionAsync` together with the request that asked for it, and
the WebSocket server's own single implementation of RFC 6455 &sect;4.2.1
validates it, chooses the subprotocol, negotiates `permessage-deflate` and
writes the 101. Two copies of a security handshake is how one of them comes to
be a version behind.

Underneath it: `HTTPResponse.UpgradeWorker`, a response that says "101, and here
is who takes over". `AHTTPServer` runs it *instead of* sending the response —
the answer to an upgrade carries the `Sec-WebSocket-Accept` and what was
negotiated, and the HTTP layer knows neither — and then ends the connection,
because after a 101 there is no way back to HTTP.

## Client usage

```csharp
var client = new WebSocketClient(
                 URL.Parse("ws://example.org:8080/webServices/ocpp/CP001"),
                 SecWebSocketProtocols: [ "ocpp2.1" ]
             );

client.EnablePerMessageDeflate = true;           // RFC 7692, off by default

// Opt-in auto-reconnect with exponential backoff + jitter (off by default):
client.ReconnectPolicy = new WebSocketClientReconnectPolicy(
                             InitialDelay:   TimeSpan.FromSeconds(1),
                             MaxDelay:       TimeSpan.FromSeconds(30),
                             BackoffFactor:  2.0,
                             JitterRatio:    0.2,    // ±20 %
                             MaxAttempts:    null,   // unlimited
                             MaxRetryAfter:  TimeSpan.FromMinutes(5)   // the longest a Retry-After is waited for
                         );

client.OnTextMessageReceived += async (timestamp, cl, connection, frame, eventTrackingId, text, ct) => {
    // ...
};

var (connection, httpResponse) = await client.Connect();
await client.SendTextMessage("[2,\"19223201\",\"BootNotification\",{}]");
```


## Testing

- Unit tests: `dotnet test --filter FullyQualifiedName~WebSocket` (HermodTests).
- Autobahn server suite: run an echo server on this implementation, then
  `wstest -m fuzzingclient` (Docker: `crossbario/autobahn-testsuite`).
- Autobahn client suite: `wstest -m fuzzingserver`, then connect this client
  against `ws://…:9001`.
