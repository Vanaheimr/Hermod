# Messages over an SSH subsystem

A small protocol that carries whole messages, text or binary, in both directions over one SSH
subsystem channel - what a WebSocket carries over HTTP. OCPP and WWCP use it as
`ocpp@open.charging.cloud` and `wwcp@open.charging.cloud`, next to their WebSocket transports.

In Hermod: `SshMessageServer` (hand its `Handler` to `SshServerOptions.Subsystems`),
`SshMessageClient`, and `SshMessageConnection`, which is an `IMessageConnection` like a WebSocket
connection is.

## Why a subsystem

- **Identity.** The SSH login is the client's proven identity: a key, or a certificate, per client.
  No passwords in an HTTP header, and no X.509 chain to run in a private network.
- **Confinement.** A credential can be confined to the subsystems it needs
  (`SshSessionRestrictions.PermittedSubsystems`): no shell, no exec, no SFTP, no forwarding.
- **Sharing.** The subsystem sits beside the server's shell and SFTP on the same port, and a server may
  serve several of them.

## Frames

Every frame is

```
uint32  length    big-endian: the kind and the payload, so at least 1
byte    kind
byte[]  payload   length - 1 bytes
```

| Kind   | Name    | Direction        | Payload |
|--------|---------|------------------|---------|
| `0x01` | HELLO   | client → server  | JSON object, see below |
| `0x02` | WELCOME | server → client  | JSON object, see below |
| `0x03` | REFUSED | server → client  | JSON object `{"code": "...", "description": "...", "retryAfter": 30}` |
| `0x10` | TEXT    | both             | UTF-8 text |
| `0x11` | BINARY  | both             | bytes |
| `0x20` | PING    | both             | any bytes |
| `0x21` | PONG    | both             | the PING's bytes |
| `0x30` | CLOSE   | both             | `uint16` close code (big-endian), then a UTF-8 reason; may be empty |

- A side checks a frame's length **before** it allocates anything for it.
  - TEXT and BINARY may be as long as the receiver's limit (`maxMessageSize`). Hermod's default is 64 MiB, as for its WebSockets.
  - Every other kind may have at most 64 KiB of payload.
  - A longer frame ends the connection: CLOSE `1009` for TEXT and BINARY, `1002` otherwise.
- TEXT that is not valid UTF-8 ends the connection with CLOSE `1007`.
- A frame of an unknown kind from `0x80` on is skipped; that range is kept for extensions. Any other
  unknown kind ends the connection with CLOSE `1002`.
- Close codes are those of RFC 6455, section 7.4.1.

## The handshake

1. The client opens a `session` channel and asks for the subsystem. The server grants or refuses it as
   SSH does for any subsystem.
2. The client sends **HELLO**:

   ```json
   { "subprotocols": [ "ocpp2.1", "ocpp2.0.1" ], "maxMessageSize": 67108864, "...": "..." }
   ```

   - `subprotocols` is optional. It lists what the client speaks, by preference: at most 32
     strings of 1 to 256 characters.
   - `maxMessageSize` is optional: the largest TEXT or BINARY payload the client takes.
   - Any other property is the application's. OCPP and WWCP send, e.g., `networkingNodeId` and
     `networkingMode` there, where the WebSocket transport has its URL path and the
     `X-OCPP-NetworkingMode` header.
   - The server waits a limited time for it (Hermod: 10 s), and closes the channel otherwise.
     Anything but HELLO first is answered with REFUSED `hello-expected`.
3. The server answers with either of two frames:
   - **WELCOME**:

     ```json
     { "subprotocol": "ocpp2.1", "maxMessageSize": 67108864, "...": "..." }
     ```

     - `subprotocol` is the client's first choice that the server speaks. It is left out where the
       client offered none.
     - A client that offered some, none of which the server speaks, gets REFUSED `unsupported-subprotocol`.
     - A client checks that the subprotocol chosen is one it offered.
   - **REFUSED**, with a code such as `unsupported-subprotocol`, `invalid-hello`, or one of the
     application's. The server closes the channel after it.
     - A refusal is final: the client does not try again by itself.
     - Except where it carries `retryAfter`, a whole number of seconds from 0 to 86400: the client may
       try again once that time has passed, as after an HTTP `409` with `Retry-After`. Hermod's
       client waits at least that long before its next attempt. The code
       `connection-exists` says the client's identity has a connection this one may not take over.
4. After WELCOME, both sides send TEXT, BINARY, PING and PONG in any order. Neither sends a message
   larger than the other side's `maxMessageSize`. A HELLO, WELCOME or REFUSED from then on is a
   protocol error.

## Liveness

- A side that sends PINGs every so often (Hermod: `PingEvery`, on the server and on the client) takes
  the other side for dead once nothing at all has come from it for a number of those intervals
  (`MaxOutstandingPings`, 3 by default): no message, no PONG, no frame. It then closes the channel
  without a CLOSE, which a dead peer could not read, as Hermod's WebSockets do.
- Every frame counts, not only a PONG: a PONG may have to wait behind a long message.

## The end

- A side that ends the connection sends **CLOSE** and closes the channel. Nothing follows a CLOSE.
- A side that receives a CLOSE answers with its own CLOSE carrying the same code, where it has sent
  none yet, and closes the channel.
- A server that stops sends CLOSE `1001` (going away).
- A channel that ends without a CLOSE ended abnormally, as RFC 6455's `1006` would say. That code is
  never sent.

## Writing

The SSH channel does not keep two writers apart: a frame larger than one SSH packet, written by two
tasks at once, would interleave. A side writes one frame at a time, whole. Once a frame is being
written, it is written to its end, or the connection ends.
