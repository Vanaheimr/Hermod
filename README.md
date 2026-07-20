# Hermod

Hermod is a .NET library for simplified advanced networking tasks.

- TCP server / client
- UDP server / client
- DNS client
- HTTP server / client
- WebSocket server / client ([RFC 6455](https://www.rfc-editor.org/rfc/rfc6455.html), permessage-deflate [RFC 7692](https://www.rfc-editor.org/rfc/rfc7692.html); Autobahn-testsuite verified — see [WebSocket documentation](Hermod/WebSocket/README.md))
- ModbusTCP server / client
- ModbusUDP server / client
- SMTP server & client — ESMTP, STARTTLS/implicit TLS, SASL AUTH, SPF/DKIM/DMARC/ARC, MTA-STS/DANE/TLS-RPT, DSN/MDN, and OpenPGP/MIME (see [SMTP documentation](SMTP_SUPPORT.md))

Protocol documentation:

- [HTTP/1.0 and HTTP/1.1 support](HTTP1_SUPPORT.md)
- [SMTP support](SMTP_SUPPORT.md)
