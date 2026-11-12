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
- A refused upgrade is the last thing said on its connection: every answer but
  a 101 says `Connection: close` (one from an `OnValidateWebSocketConnection`
  handler that does not is sent saying it, without its `keep-alive`), and the
  connection is closed after it. No frame goes out on a connection whose 101
  has not, a close frame least of all: not after a refusal, not after a first
  request that is not a `GET`, not to a client that hangs up before it asks,
  and not to one still waiting for its answer when the server is shut down.
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
- The client waits five seconds for the answer to its upgrade, or as long as
  the request's `Timeout` says, and the deadline holds against a server that
  takes the connection and then says nothing at all: the attempt ends with a
  408, `Connect()` answers with it, and a client with a reconnect policy tries
  again. `Close()` ends an attempt that is still waiting.
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
- TLS-channel-bound TOTP is sent and verified for a WebSocket opened over HTTP/3
  only, where the TLS of QUIC exports the keying material: a client with
  `WebSocketOver = HTTP3` binds its token to the connection, and the server
  verifies it with its side of the same. Over HTTP/1.1 and HTTP/2 `SslStream`
  does not expose TLS exporter material; there such credentials are rejected by
  the server and not sent by the client, rather than silently treated as raw
  TOTP.
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

A WebSocket no longer needs a TCP server of its own. `AWebSocketEndpoint` is
the protocol alone - handshake, frames, pings, close, authentication,
subprotocols, `permessage-deflate`, backpressure and their events - without a
listener, a DNS client, a timer or a warden, and without a port bound for
nothing. `AWebSocketServer` is an endpoint with a TCP listener of its own, which
it owns rather than is; its TCP and TLS settings and events are its own as they
always were, and `TCPListener` is the listener itself. `WebSocketUpgrade.For`
turns any endpoint into an HTTP handler, so one listener, one certificate and
one port can serve frames at one path and ordinary HTTP at another, and as many
endpoints as there are paths cost no more than their connections:

```csharp
var webSocketServer = new WebSocketEndpoint(RequireAuthentication: false);   // no TCP server at all

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

**The firewall is asked too.** An endpoint accepts no connection of its own,
and its `OnValidateTCPConnection` handlers are asked all the same: at the hand-over,
about the TCP connection the HTTP server accepted, with that server as the TCP server - after that server's TLS and
after the request that asked for the upgrade, rather than before them as on a
port of its own. The verdict is the same either way: the first refusal among
all answers, and a handler that throws has refused. A refused client is answered
`403 Forbidden`, since it has sent a handshake and is waiting for an answer - or
`503` where a handler was called off while it was being asked, as for the
handshake's own validators; why it was refused goes to
`OnNewTCPConnectionRejected`, not to the client. What
belongs to a listener stays with the one that listens: the HTTP server's TLS
settings and `MaxClientConnections` are used, and a server put on a path does not
use its own.

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


## Over HTTP/2

The client and the server open a WebSocket over HTTP/2 as well (RFC 8441): by
an extended CONNECT with `:protocol = websocket` where the Upgrade of HTTP/1.1
was, on the port the server takes WebSockets over HTTP/1.1 on.

```csharp
server.AcceptHTTP2   = true;                          // off by default
client.WebSocketOver = WebSocketHTTPVersion.Auto;     // HTTP1_1 by default
```

| `WebSocketOver` | |
|---|---|
| `HTTP1_1` | the Upgrade of HTTP/1.1, as always |
| `HTTP2`   | the extended CONNECT of HTTP/2 and nothing else: with TLS the server has to agree to `h2` (ALPN), without TLS it is spoken to in HTTP/2 from the first byte (prior knowledge) |
| `Auto`    | `h2` and `http/1.1` are offered and the server chooses; a server that chooses `h2` and does not announce `SETTINGS_ENABLE_CONNECT_PROTOCOL` is dialled again with `http/1.1` alone; without TLS it is HTTP/1.1 |

`client.HTTPVersionInUse` and `connection.HTTPVersion` (server) say which one
it came to.

**The handshake is translated, not written a second time.** What a client
offers and a server accepts beside the upgrade itself - the subprotocol, the
extensions, an Authorization, a one-time password - are the same header fields
over both. So the client writes the upgrade request it always wrote into a
stream that sends it as an extended CONNECT and reads the server's 200 back as
the 101 it waits for (`WebSocketOverHTTP2ClientStream`), and the server is
handed the upgrade request a CONNECT stands for and answers it as every other
(`WebSocketOverHTTP2ServerStream`): every validation, every authentication and
every refusal - a 401, a 503 with its Retry-After - is the code it was, once.
`Upgrade`, `Connection`, `Sec-WebSocket-Key` and `Sec-WebSocket-Accept` do not
go over HTTP/2 (RFC 8441, Section 5); a CONNECT that carries a header field of
one connection of HTTP/1.1, or one with a line break in it, is refused with 400.

One WebSocket at a time per HTTP/2 connection: this server knows a connection
by its remote socket. The frames of RFC 6455 are the ones of this folder, not
those of `../../HTTP2/WebSocket`, which is the WebSocket of the HTTP/2 stack by
itself.


## Over HTTP/3

And over HTTP/3 (RFC 9220): the same extended CONNECT, over QUIC, on the UDP
port of the number of the server's TCP port.

```csharp
server.AcceptHTTP3   = true;                          // off by default
client.WebSocketOver = WebSocketHTTPVersion.HTTP3;    // or Auto
```

| `WebSocketOver` | |
|---|---|
| `HTTP3` | the extended CONNECT of HTTP/3 and nothing else. It needs TLS (`wss://`), as QUIC has nothing else; a server that is not reached over it, or does not announce `SETTINGS_ENABLE_CONNECT_PROTOCOL`, is not connected to |
| `Auto`  | HTTP/3 where the server said before that it is reached over it, and as described above otherwise |

**How `Auto` comes to HTTP/3.** A server that takes WebSockets over HTTP/3
says so with every upgrade it accepts over HTTP/1.1 and HTTP/2:
`Alt-Svc: h3=":port"` (RFC 7838). A client keeps that for the origin - host
and port, over TLS - for as long as the server said, in memory, for every
client of the process (`WebSocketAlternativeServices`), and `Auto` asks over
HTTP/3 from then on. A server that is not found there within three seconds
(`AlternativeServiceTimeout`) is asked the way it was when it said so, and
not believed again for an hour - a firewall that lets no UDP through would
otherwise cost every attempt those seconds. Only an
alternative on the origin's own host is kept, and the server found there is
judged by the client's certificate validator, as the origin is. A client that
names its version is not moved by any of this.

**The handshake is the same one again**: `WebSocketOverHTTP3ClientStream`
stands on the base the HTTP/2 stream stands on
(`AWebSocketOverHTTPClientStream`), and the server runs the bridge it runs
over HTTP/2 on a tunnel of HTTP/3.

**Who is who.** The client asks for the server by the name its TLS over TCP
would ask for, and judges it by its `RemoteCertificateValidator`: told the
certificate, the chain and what is wrong with them, as an `SslStream` tells it
(`CertificateValidationOptions.Judge`). A certificate it refuses ends the
attempt at once, and the reason says so. A client certificate with its private
key is shown where the server asks for one. The server shows the certificate
its `ServerCertificateSelector` gives for a client nothing is known of - QUIC
shows one certificate to all - and asks for a client certificate where it has
a `ClientCertificateValidator` or requires one, judged by that validator.

What TLS over TCP has and this has not:

- A certificate that is there already is shown with a key of **ECDSA P-256,
  ECDSA P-384 or RSA** (`ServerCertificate.From`; RSA as RSA-PSS, with 2048
  bits or more). A server with another key takes no WebSockets over HTTP/3
  and runs as it would without `AcceptHTTP3`; so does one without TLS, and
  one whose UDP port is taken. `server.HTTP3Port` says whether it does, and
  why not is logged.
- Where a client certificate is asked for and not required, a client whose
  certificate is refused is a client without one. Over TCP its handshake
  fails.
- The client's `LocalCertificateSelector` is not asked, and its protocols,
  cipher suites and revocation mode are those of QUIC's TLS 1.3.
- A one-time password bound to the TLS channel is refused over every version,
  so nothing is lost there.

One WebSocket at a time per HTTP/3 connection, as over HTTP/2.


## Testing

- Unit tests: `dotnet test --filter FullyQualifiedName~WebSocket` (HermodTests).
- Autobahn server suite: run an echo server on this implementation, then
  `wstest -m fuzzingclient` (Docker: `crossbario/autobahn-testsuite`).
- Autobahn client suite: `wstest -m fuzzingserver`, then connect this client
  against `ws://…:9001`.
