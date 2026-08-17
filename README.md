# Hermod

[![CI](https://github.com/Vanaheimr/Hermod/actions/workflows/ci.yml/badge.svg)](https://github.com/Vanaheimr/Hermod/actions/workflows/ci.yml)
[![Nightly](https://github.com/Vanaheimr/Hermod/actions/workflows/nightly.yml/badge.svg)](https://github.com/Vanaheimr/Hermod/actions/workflows/nightly.yml)

Hermod is a .NET library for simplified advanced networking tasks...


## Generic Networking Protocols

- Ethernet frames
- IPv4 and IPv6 packets
- Generic UDP clients / servers
- Generic TCP clients / servers
- [QUIC clients / servers (RFC 9000/9001/9002)](Hermod/QUIC/README.md) incl. the
  TLS 1.3 handshake it carries


## Common Application Protocols

- [DNS (UDP/TCP/TLS/HTTPS) Clients / Servers](Hermod/DNS/README.md)
- [HTTP/1.0 and HTTP/1.1 Client / Server](Hermod/HTTP1/README.md)
- [HTTP/1.1 WebSocket Client / Server](Hermod/HTTP1/WebSocket/README.md)
- [HTTP/2.0 Client / Server](Hermod/HTTP2/README.md)
- [HTTP/3 Client / Server (RFC 9114) + QPACK (RFC 9204)](Hermod/HTTP3/README.md)
  incl. WebSockets (RFC 9220), HTTP datagrams (RFC 9297) and WebTransport
- [SMTP Submission/Outbound Clients / Server](Hermod/SMTP/README.md) with OpenPGP/MIME
- [SSH2 Client / Server + SFTP (RFC 4251-4254)](Hermod/SSH/README.md) with post-quantum
  hybrid key exchange, OpenSSH certificates and port forwarding


## Specialized Application Protocols or Extensions

- ModbusTCP/TLS client / server
- HTTP SOAP client / server
- HTTP Passkeys
- HTTP TOTP Authentication
- Argus
- Warden


## Shared Infrastructure

- `EventInvocation.InvokeAllAsync` — one way to raise an event, so that a
  handler which throws is a handler which throws and not a connection which
  dies. It awaits every subscriber, in the order subscribed, wraps each of them
  on its own, and lets nothing back out to whoever raised the event. A failing
  handler is reported either to an `ILogger` or to a sink of the caller's own,
  for whoever already reports somewhere — an overridable `HandleErrors`, a
  `DebugX` — and would lose that by handing over a logger.

  Everything in this library that raises an event goes through it, the six
  copies of the private `LogEvent` helper included. Three places deliberately
  do not, and say so where they stand: `OnValidateWebSocketConnection`,
  `OnValidateTCPConnection` and `HTTPTestServer.ProcessHTTP` read what their
  handlers return, and an invoker that returns `Task` and swallows exceptions
  cannot carry a refusal.
