# Hermod

[![CI](https://github.com/Vanaheimr/Hermod/actions/workflows/ci.yml/badge.svg)](https://github.com/Vanaheimr/Hermod/actions/workflows/ci.yml)

Hermod is a .NET library for simplified advanced networking tasks...


## Generic Networking Protocols

- Ethernet frames
- IPv4 and IPv6 packets
- Generic UDP clients / servers
- Generic TCP clients / servers
- [QUIC clients / servers (RFC 9000/9001/9002)](Hermod/QUIC/README.md) incl. the
  TLS 1.3 handshake it carries — the transport under HTTP/3, which itself lives in
  the sibling project
  [HTTP/3 Conformance Tests](https://github.com/Vanaheimr/HTTP3ConformanceTests)


## Common Application Protocols

- [DNS (UDP/TCP/TLS/HTTPS) Clients / Servers](Hermod/DNS/README.md)
- [HTTP/1.0 and HTTP/1.1 Client / Server](Hermod/HTTP1/README.md)
- [HTTP/1.1 WebSocket Client / Server](Hermod/HTTP1/WebSocket/README.md)
- [HTTP/2.0 Client / Server](Hermod/HTTP2/README.md)
- [SMTP Submission/Outbound Clients / Server](Hermod/SMTP/README.md) with OpenPGP/MIME


## Specialized Application Protocols or Extensions

- ModbusTCP/TLS client / server
- HTTP SOAP client / server
- HTTP Passkeys
- HTTP TOTP Authentication
- Argus
- Warden
