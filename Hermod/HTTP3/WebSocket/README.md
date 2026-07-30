# WebSocket over HTTP/3 (RFC 9220 / RFC 8441 / RFC 6455)

The files `IHTTP2Tunnel.cs`, `WebSocketConnection.cs`, `WebSocketDeflate.cs`,
`WebSocketMessage.cs`, `WebSocketOpcode.cs`, `WebSocketProtocolException.cs` and
`WebSocketRole.cs` are **byte-identical copies** of `../../HTTP2/WebSocket/` and
`../../HTTP2/Core/IHTTP2Tunnel.cs` — **only change: the namespace line**
(`…Hermod.HTTP2` → `…Hermod.HTTP3`).

The RFC 6455 framing is written transport-agnostically against the 2-method
interface `IHTTP2Tunnel` (`ReadAsync`/`WriteAsync`); for HTTP/3, `Http3Tunnel`
(RFC 9114 §4.4: tunnel bytes travel in DATA frames of the Extended-CONNECT
stream, RFC 8441/9220) implements the same interface.

**Dedup is now possible and still open.** The precondition — both copies in one
repository — is met since the HTTP/3 stack moved into Hermod, and the copies are
still identical to the byte. What is left is a naming decision: the framing is
transport-neutral and belongs in a shared namespace, with one tunnel adapter each
for HTTP/2 and HTTP/3, which changes a public namespace for existing callers.
Until that is decided, keep the diff at exactly one line per file so reconciling
the two stays trivial.
