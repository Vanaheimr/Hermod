# Vanaheimr Hermod — DNS Subsystem

A fully asynchronous DNS client framework for .NET 10 supporting all common
transport protocols, EDNS0 extensions, caching, and automatic CNAME resolution.


## Transport Protocols

| Client             | Protocol                   | RFC         |
|--------------------|----------------------------|-------------|
| `DNSUDPClient`     | DNS over UDP               | RFC 1035    |
| `DNSTCPClient`     | DNS over TCP               | RFC 7766    |
| `DNSTLSClient`     | DNS over TLS (DoT)         | RFC 7858    |
| `DNSHTTPSClient`   | DNS over HTTPS (DoH)       | RFC 8484    |
| `DNSClient`        | Orchestrator / Multi-Server| —           |

### DNSHTTPSClient — Modes

- **GET** — Base64url-encoded DNS query in the URL parameter
- **POST** — Binary DNS message in the request body (`application/dns-message`)
- **JSON** — Google/Cloudflare JSON API (`application/dns-json`)

For JSON queries requesting multiple record types, the client automatically
fans out into individual per-type queries, since the JSON APIs only support
a single type per request.


## DNSClient — The Orchestrator

`DNSClient` wraps the individual transport clients and adds cross-cutting
concerns:

- **Multi-server queries** with configurable timeout and race semantics
  (fastest valid response wins)
- **DNS cache** with timer-based TTL eviction
- **NODATA negative cache** (RFC 2308) — Empty responses are cached per
  `(DomainName, RecordType)` to avoid redundant queries for missing record types
- **Automatic CNAME following** (see below)
- **EDNS0 options** are forwarded to all transport clients


## CNAME Chasing

When a DNS response contains CNAME records but not the originally requested
record type, `DNSClient` automatically follows the CNAME chain:

- Configurable via `FollowCNAMEs` (default: `true`) and `MaxCNAMEFollows` (default: `8`)
- **Loop detection** via a case-insensitive `HashSet<String>` of all visited names
- Follow-up queries go through `Query()`, so they automatically benefit from
  caching, EDNS0 options, and further CNAME chasing
- The final response contains the full CNAME chain plus the resolved records
- `Any` and `CNAME` queries are never followed
- This logic exists **exclusively** in `DNSClient`, not in the individual
  transport clients


## EDNS0 — Extension Mechanisms for DNS (RFC 6891)

### OPT Pseudo-Record

The `OPT` record is a pseudo-resource record (Type 41) that carries EDNS0
metadata. It is correctly handled both when sending (in `DNSPacket`) and
when receiving (in `DNSInfo.ReadResourceRecord`).

Because `OPT` intentionally does **not** extend `ADNSResourceRecord` (its wire
format differs fundamentally: Class = UDP payload size, TTL = ExtRCODE/Version/Flags),
it is handled explicitly before the reflection-based record type lookup.

### Implemented EDNS0 Options

| Option                  | Code | RFC      | Class                      | Purpose                         |
|-------------------------|------|----------|----------------------------|---------------------------------|
| **Client Subnet**       | 8    | RFC 7871 | `EDNSClientSubnetOption`   | Geo/CDN optimization            |
| **COOKIE**              | 10   | RFC 7873 | `EDNSCookieOption`         | Transaction authentication      |
| **TCP Keepalive**       | 11   | RFC 7828 | `EDNSKeepaliveOption`      | Idle timeout for TCP/TLS        |
| **Padding**             | 12   | RFC 7830 | `EDNSPaddingOption`        | Traffic analysis protection     |
| **Extended DNS Error**  | 15   | RFC 8914 | `EDNSExtendedDNSError`     | Detailed error codes            |

All options are automatically deserialized from the wire format into typed
objects via `EDNSOption.Parse()`. Unknown option codes are preserved as
generic `EDNSOption` instances.

#### Usage

```csharp
var client = new DNSClient("8.8.8.8");

// Cookie for spoofing protection
client.EDNSOptions.Add(EDNSCookieOption.CreateInitial());

// Client Subnet for geo-aware routing
client.EDNSOptions.Add(new EDNSClientSubnetOption(
    IPAddress.Parse("203.0.113.0"),
    SourcePrefixLength: 24
));

// Padding for DNS-over-TLS privacy
client.EDNSOptions.Add(EDNSPaddingOption.Create(currentMessageLength));

// Signal TCP keepalive support
client.EDNSOptions.Add(EDNSKeepaliveOption.CreateQuery());

var result = await client.Query<A>("example.com");

// Read Extended DNS Errors from the response
foreach (var ede in result.EDNSOptions.OfType<EDNSExtendedDNSError>())
    Console.WriteLine($"EDE {ede.InfoCode}: {ede.ExtraText}");
```

### Response Access

`DNSInfo` provides direct access to EDNS0 data from the response:

```csharp
DNSInfo response = ...;

OPT? opt = response.OPTRecord;           // The OPT pseudo-record (or null)
var options = response.EDNSOptions;       // All EDNS options (empty if no OPT)
```


## DNS Cache

- **Positive entries**: `ConcurrentDictionary<DNSServiceName, DNSCacheEntry>`
  with TTL-based eviction via a `Timer`
- **NODATA negative cache** (RFC 2308): `ConcurrentDictionary<String, DateTimeOffset>`
  stores `"domain|TYPE"` → expiry time. Default TTL: 5 minutes
- Cache cleanup runs periodically (default: every 10 seconds)


## Exception Handling

All transport clients use typed exception handling instead of bare `catch` blocks:

| Exception                     | Result             | Meaning                       |
|-------------------------------|--------------------|-------------------------------|
| `OperationCanceledException`  | `DNSInfo.TimedOut` | Query timeout exceeded        |
| `SocketException`             | `DNSInfo.Failed`   | Network error                 |
| `Exception`                   | `DNSInfo.Failed`   | Parse or other error          |

Every error is logged via `DebugX.LogT()`, so no error information is
silently swallowed.


## Supported Resource Record Types

A, AAAA, CNAME, MX, NS, PTR, SOA, SPF, SRV, SSHFP, TXT, NAPTR, HTTPS, SVCB, URI, OPT


## Architecture Overview

```
DNSClient (Orchestrator)
├── Cache (DNSCache + NODATA)
├── CNAME Chasing (loop detection, max depth)
├── EDNS0 Options (forwarded to all transports)
└── Transport Clients
    ├── DNSUDPClient   (UDP, port 53)
    ├── DNSTCPClient   (TCP, port 53)
    ├── DNSTLSClient   (TLS, port 853)
    └── DNSHTTPSClient (HTTPS, port 443)
         ├── GET mode
         ├── POST mode
         └── JSON mode (with multi-type fan-out)
```


## Concurrency

- `DNSHTTPSClient` uses `SemaphoreSlim` to serialize HTTP/1.1 connections
  (one request at a time per client instance)
- `DNSCache` is built on `ConcurrentDictionary` for thread-safe access
- `DNSClient.Query()` uses `Task.WhenAny()` with race semantics across
  multiple DNS servers


## License

Apache License, Version 2.0 — see [LICENSE](../../../../LICENSE)
