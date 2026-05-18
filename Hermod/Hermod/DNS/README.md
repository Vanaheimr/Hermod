# Vanaheimr Hermod — DNS Subsystem

A fully asynchronous DNS client framework for .NET 10 supporting all common
transport protocols, EDNS0 extensions, caching, DNSSEC validation, and
automatic CNAME resolution.


## Transport Protocols

| Client             | Protocol                   | RFC         |
|--------------------|----------------------------|-------------|
| `DNSUDPClient`     | DNS over UDP               | RFC 1035    |
| `DNSTCPClient`     | DNS over TCP               | RFC 7766    |
| `DNSTLSClient`     | DNS over TLS (DoT)         | RFC 7858    |
| `DNSHTTPSClient`   | DNS over HTTPS (DoH)       | RFC 8484    |
| `DNSClient`        | Orchestrator / Multi-Server| —           |

### DNSHTTPSClient — Modes

- **GET**  — Base64url-encoded DNS query in the URL parameter (RFC 8484 §4.1)
- **POST** — Binary DNS message in the request body (`application/dns-message`)
- **JSON** — Google/Cloudflare JSON API (`application/dns-json`)

For JSON queries requesting multiple record types, the client automatically
fans out into individual per-type queries, since the JSON APIs only support
a single type per request.

The `DNSTransport` enum exposes these as `UDP`, `TCP`, `TLS`, `HTTPS`,
`HTTPS_Binary`, `HTTPS_JSON`, `HTTPS_GET` (plus unencrypted `HTTP`/`HTTP_Binary`/
`HTTP_JSON` variants for testing).


## DNSClient — The Orchestrator

`DNSClient` wraps the individual transport clients and adds cross-cutting
concerns:

- **Multi-server queries** with configurable timeout and race semantics
  (fastest valid response wins)
- **SERVFAIL retry logic** via `MaxRetries` (default: 1) — if a DNS server
  returns SERVFAIL, the query is automatically retried up to N times before
  failing over to the next server
- **Transport client pooling** via `ConcurrentDictionary<DNSServerConfig,
  IDNSClientWithTransport>` — TCP / TLS / HTTPS clients are kept alive and
  reused across queries; only UDP clients are stateless per query
- **DNS cache** with timer-based TTL eviction
- **NODATA negative cache** (RFC 2308) — Empty responses are cached per
  `(DomainName, RecordType)` to avoid redundant queries for missing record
  types
- **Aggressive NSEC Caching** (RFC 8198) — NSEC records from authority
  sections are stored as ranges and used to synthesize NXDOMAIN / NODATA
  responses *before* sending a wire query
- **DNSSEC validation** (RFC 4033 / 4034 / 4035) with the validation result
  exposed via `DNSInfo.DNSSECStatus`
- **Trust Anchor Rollover** (RFC 5011) — automated tracking of root
  KSK rotation
- **Automatic CNAME / DNAME following** (see below)
- **EDNS0 options** are forwarded to all transport clients
- **DNS Cookies** (RFC 7873) — automatically included in every query for
  spoofing protection
- **EDNS Client Subnet auto-injection** — set `DNSClient.ClientSubnet` once,
  and the truncated client subnet is added to every outgoing query
  (RFC 7871)


## CNAME / DNAME Chasing

When a DNS response contains CNAME or DNAME records but not the originally
requested record type, `DNSClient` automatically follows the alias chain:

- Configurable via `FollowCNAMEs` (default: `true`) and `MaxCNAMEFollows` (default: `8`)
- **CNAME following**: Classic single-name alias resolution
- **DNAME following** (RFC 6672): Entire subtree delegation — the client
  synthesizes the rewritten name by replacing the DNAME owner suffix with
  the target domain
- **Loop detection** via a case-insensitive `HashSet<String>` of all visited names
- Follow-up queries go through `Query()`, so they automatically benefit from
  caching, EDNS0 options, and further CNAME/DNAME chasing
- The final response contains the full alias chain plus the resolved records
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

// Cookies are sent automatically by DNSClient on every query.
// To customize the client secret, add an EDNSCookieOption manually:
client.EDNSOptions.Add(EDNSCookieOption.CreateInitial());

// Client Subnet for geo-aware routing — set once, auto-injected per query
client.ClientSubnet = new EDNSClientSubnetOption(
    IPAddress.Parse("203.0.113.0"),
    SourcePrefixLength: 24
);

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
DNSSECValidationResult? sec = response.DNSSECStatus;  // DNSSEC chain result
```


## DNS Cache

- **Positive entries**: `ConcurrentDictionary<DNSServiceName, DNSCacheEntry>`
  with TTL-based eviction via a `Timer`
- **Per-record TTL filtering**: On cache reads, individual records whose
  `EndOfLife` has passed are filtered out — only still-valid records are
  returned. A cache entry is fully removed only when *all* its records
  have expired.
- **Atomic updates**: Cache insertions use `AddOrUpdate()` to safely merge
  new records into existing entries under concurrent access
- **NODATA negative cache** (RFC 2308): `ConcurrentDictionary<String, DateTimeOffset>`
  stores `"domain|TYPE"` → expiry time. Default TTL: 5 minutes
- **NSEC range cache** (RFC 8198 — Aggressive NSEC Caching):
  `nsecRangeCache` stores NSEC records harvested from authority sections.
  Before any wire query, `IsNameNegativelyCachedByNSEC()` checks whether
  the queried name falls into a known NSEC gap and synthesizes an
  authoritative negative response without network round-trip
- Cache cleanup runs periodically (default: every 10 seconds)


## Resource Records — Common Patterns

All concrete resource record classes inherit from `ADNSResourceRecord` and
share three serialization patterns:

### Wire Format (binary)

```csharp
record.SerializeRRData(stream, useCompression: true, compressionOffsets);
```

Each record overrides `SerializeRRData()` to encode its RDATA according to
its RFC. Name compression (RFC 1035 §4.1.4) is handled via the optional
`CompressionOffsets` dictionary.

### Zone File Presentation Format (RFC 1035 §5)

```csharp
String zoneLine = record.ToZoneFileString();
// e.g.: "charging.cloud.          3600    IN   A          23.88.66.160"
```

`ADNSResourceRecord.ToZoneFileString()` formats the owner name, TTL, class
and type uniformly, then delegates to an abstract `ZoneFileRData()` for the
record-specific RDATA portion. This means every record type can be rendered
as a standard zone file line.

### JSON Parsing (Google / Cloudflare DoH JSON)

```csharp
A? record = A.TryParseFromJSON(name, ttl, data);
```

Every record class exposes a static `TryParseFromJSON(DomainName, TimeSpan,
String)` factory. Parsing logic is *co-located* with the record type — the
`DNSHTTPSClient` simply dispatches based on the record type code, keeping
the JSON layer thin and the per-type logic discoverable.


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

### Standard Records

| Type     | RFC       | Description                                    |
|----------|-----------|------------------------------------------------|
| A        | RFC 1035  | IPv4 address                                   |
| AAAA     | RFC 3596  | IPv6 address                                   |
| CNAME    | RFC 1035  | Canonical name (alias)                         |
| DNAME    | RFC 6672  | Delegation name (subtree redirection)          |
| MX       | RFC 1035  | Mail exchange                                  |
| NS       | RFC 1035  | Name server                                    |
| PTR      | RFC 1035  | Pointer (reverse DNS)                          |
| SOA      | RFC 1035  | Start of authority                             |
| SRV      | RFC 2782  | Service locator                                |
| TXT      | RFC 1035  | Arbitrary text                                 |
| NAPTR    | RFC 3403  | Naming authority pointer                       |
| HINFO    | RFC 1035  | Host information (CPU / OS)                    |
| RP       | RFC 1183  | Responsible person                             |
| AFSDB    | RFC 1183  | AFS database server                            |
| LOC      | RFC 1876  | Geographic location                            |
| SPF      | RFC 7208  | Sender policy framework                        |
| URI      | RFC 7553  | Uniform resource identifier                    |
| CAA      | RFC 8659  | Certification authority authorization          |
| EUI48    | RFC 7043  | 48-bit MAC address                             |
| EUI64    | RFC 7043  | 64-bit MAC address                             |

### DNSSEC Records

| Type       | RFC       | Description                                  |
|------------|-----------|----------------------------------------------|
| DS         | RFC 4034  | Delegation signer                            |
| RRSIG      | RFC 4034  | Resource record signature                    |
| NSEC       | RFC 4034  | Next secure (authenticated denial)           |
| DNSKEY     | RFC 4034  | DNS public key                               |
| NSEC3      | RFC 5155  | Hashed authenticated denial                  |
| NSEC3PARAM | RFC 5155  | NSEC3 hash parameters                        |
| CDS        | RFC 7344  | Child DS (automated key rotation)            |
| CDNSKEY    | RFC 7344  | Child DNSKEY (automated key rotation)        |
| CSYNC      | RFC 7477  | Child-to-parent synchronization              |
| ZONEMD     | RFC 8976  | Zone message digest                          |

### Security / Certificate Records

| Type       | RFC       | Description                                  |
|------------|-----------|----------------------------------------------|
| TLSA       | RFC 6698  | TLS certificate association (DANE)           |
| SMIMEA     | RFC 8162  | S/MIME certificate association                |
| CERT       | RFC 4398  | Certificate storage (PKIX, SPKI, PGP)        |
| SSHFP      | RFC 4255  | SSH public key fingerprint                   |
| OPENPGPKEY | RFC 7929  | OpenPGP public key                           |

### Service Binding Records (RFC 9460)

| Type   | RFC       | Description                                    |
|--------|-----------|------------------------------------------------|
| SVCB   | RFC 9460  | Generic service binding                        |
| HTTPS  | RFC 9460  | HTTPS service binding                          |

### Pseudo-Records

| Type   | RFC       | Description                                    |
|--------|-----------|------------------------------------------------|
| OPT    | RFC 6891  | EDNS0 options carrier                          |
| TSIG   | RFC 8945  | Transaction signature                          |
| TKEY   | RFC 2930  | Transaction key exchange                       |

All record types are fully supported in:
- **Binary wire format** (UDP / TCP / TLS / DoH binary)
- **JSON API** (DNS-over-HTTPS, Google + Cloudflare) — except TSIG / TKEY,
  which are transport-level pseudo-records not applicable to DoH JSON
- **Zone file presentation format** via `ToZoneFileString()`


## Architecture Overview

```
DNSClient (Orchestrator)
├── Cache
│   ├── Positive (DNSCache, TTL-based eviction)
│   ├── NODATA negative cache (RFC 2308)
│   └── NSEC range cache (RFC 8198, aggressive)
├── DNSSEC validation (RFC 4033/4034/4035 + RFC 5011 trust anchor rollover)
├── CNAME / DNAME chasing (loop detection, max depth)
├── EDNS0 options (forwarded; Cookies + Client Subnet auto-injected)
├── SERVFAIL retry (MaxRetries) + multi-server race semantics
└── Transport client pool (ConcurrentDictionary)
    ├── DNSUDPClient   (UDP, port 53)         — stateless per query
    ├── DNSTCPClient   (TCP, port 53)         — pooled
    ├── DNSTLSClient   (TLS, port 853)        — pooled
    └── DNSHTTPSClient (HTTPS, port 443)      — pooled
         ├── GET mode  (RFC 8484 §4.1)
         ├── POST mode (RFC 8484 §4.1)
         └── JSON mode (per-record TryParseFromJSON, multi-type fan-out)
```


## Concurrency

- `DNSHTTPSClient` uses `SemaphoreSlim` to serialize HTTP/1.1 connections
  (one request at a time per client instance)
- `DNSCache` is built on `ConcurrentDictionary` for thread-safe access
- `DNSClient.Query()` uses `Task.WhenAny()` with race semantics across
  multiple DNS servers
- Pooled transport clients are stored in a `ConcurrentDictionary` and
  disposed by `DNSClient.Dispose()`


## License

Apache License, Version 2.0 — see [LICENSE](../../../../LICENSE)
