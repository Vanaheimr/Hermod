# WebSocket over HTTP/3 (RFC 9220 / RFC 8441 / RFC 6455)

The files `IHTTP2Tunnel.cs`, `WebSocketConnection.cs`, `WebSocketDeflate.cs`,
`WebSocketMessage.cs`, `WebSocketOpcode.cs`, `WebSocketProtocolException.cs` and
`WebSocketRole.cs` are **byte-identical copies** of `../../HTTP2/WebSocket/` and
`../../HTTP2/Core/IHTTP2Tunnel.cs` — **only change: the namespace line**
(`…Hermod.HTTP2` → `…Hermod.HTTP3`). One file there has no copy here, as it is
HTTP/2's alone: `WebSocketConnection.Priority.cs`, see below.

The RFC 6455 framing is written transport-agnostically against the 2-method
interface `IHTTP2Tunnel` (`ReadAsync`/`WriteAsync`); for HTTP/3, `Http3Tunnel`
(RFC 9114 §4.4: tunnel bytes travel in DATA frames of the Extended-CONNECT
stream, RFC 8441/9220) implements the same interface.

**What is one transport's alone goes into a part of its own.** `WebSocketConnection`
is `partial` in both copies, so that what only one transport has can sit in a
part next to the shared file, with no copy, and the shared file stays a copy.
There is one such part, `../../HTTP2/WebSocket/WebSocketConnection.Priority.cs`:
`UpdatePriorityAsync` reprioritizes a client's WebSocket among the other streams
of its HTTP/2 connection (RFC 9218) through `HTTP2ClientTunnel.UpdatePriorityAsync`,
and a server's refuses with `NotSupportedException`, as servers must not send
PRIORITY_UPDATE (RFC 9218, Section 7.1). `Tunnel`, the `IHTTP2Tunnel` a WebSocket
runs over, is transport-neutral and in both copies.

Not built: the same for a WebSocket over HTTP/3. Its tunnel's QUIC stream would
take the new priority as `SendUrgency`/`SendIncremental`, as a request's stream
does, and the server would learn it from a PRIORITY_UPDATE on the control stream
(`Http3ClientConnection.SendPriorityUpdate`, RFC 9218 Section 7.2). That is new
functionality — `Http3Tunnel` does not know its connection, for one — and it would
be a `WebSocketConnection.Priority.cs` of its own here, not a copy.

**Dedup is now possible and still open.** The precondition — both copies in one
repository — is met since the HTTP/3 stack moved into Hermod, and the copies are
still identical to the byte. What is left is a naming decision: the framing is
transport-neutral and belongs in a shared namespace, with one tunnel adapter each
for HTTP/2 and HTTP/3, which changes a public namespace for existing callers.
Until that is decided, keep the diff at exactly one line per file so reconciling
the two stays trivial. A part of one transport's alone then either follows the
class into the shared namespace, or becomes an extension method in its
transport's.

That request is now **enforced**, for every file, after two of them drifted.

The first was `WebSocketDeflate`: `ShouldAccept` was fixed on the HTTP/2 side on
2026-09-22 (parse the permessage-deflate offer instead of pattern-matching it,
RFC 7692 Section 7.1.2.1) and the copy here was left behind, carrying the bug for
a day with nothing pointed at it — the HTTP/2 copy is covered by a nightly
Autobahn run, this one by no foreign suite at all.

`HermodTests/HTTP2/WebSocketDeflateNegotiationTests` now runs one offer table
against **both** copies and asserts they answer identically, and
`HermodTests/HTTP3/Tunnels/` inherits the fixture so it also runs under the
`Hermod.Tests.HTTP3` name the HTTP/3 conformance repository filters on. A fix
applied to only one copy fails there now, instead of waiting for a suite that
never visits this half.

The second was `WebSocketConnection`, which that table does not reach. On
2026-10-01 the HTTP/2 copy got `Tunnel`, `UpdatePriorityAsync` and a sentence on
how a client's tunnel ends with its HTTP/2 connection, and the copy here none of
them. `HermodTests/HTTP3/Tunnels/WebSocketCopyTests` compares the files
themselves: every copy against its original with the namespace line swapped, to
the byte, and every file against its counterpart, but for the parts that are one
transport's alone, which it names. A change that reaches only one copy fails
there, in the pull request that makes it.
