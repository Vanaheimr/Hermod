# ACME client (RFC 8555)

A client of the Automatic Certificate Management Environment, with which a
Hermod server gets its own certificates from Let's Encrypt or any other ACME
certificate authority, and renews them before they run out. It does not need
certbot or a restart.

| Class | What it does |
|---|---|
| `ACMEAccountKey` | The account key: ECDSA P-256, signs every request (ES256). `LoadOrCreate(path)` keeps it in a file, because the key *is* the account. |
| `ACMEClient` | The protocol itself. It handles the directory, nonces (and retries a `badNonce`), opening or finding an account (with an external account binding where the CA asks for one), orders, authorizations, challenges, finalizing with a new key and CSR, downloading the chain, revocation, and renewal information (ARI, RFC 9773). `ObtainCertificateAsync` does all of RFC 8555 section 7.4 in one call. |
| `HTTP01ChallengeResponder` | Answers http-01 on `/.well-known/acme-challenge/{token}` of a Hermod `HTTPServer`. It cannot answer for a wildcard. |
| `DNS01ChallengeResponder` | Answers dns-01 through two delegates that put a TXT record into a zone and take it out again. An optional check says when the record is visible, and a propagation delay is waited once per order. |
| `ACMECertificateManager` | Keeps a certificate. On a schedule it fetches the certificate, renews it, and writes `fullchain.pem` and `privkey.pem` (certbot's names). It hands the certificate to a TLS server through `Current` / `Selector` and the `OnRenewed` event. |

The client works with HTTP-01 and DNS-01 identifiers, wildcards, and IP
addresses (RFC 8738). Certificate keys can be ECDSA P-256 (the default) or
P-384, or RSA with 2048 or 4096 bits. The client also supports certificate
profiles, such as Let's Encrypt's `shortlived`, where the CA offers them.

## A server reachable on port 80

```csharp
using var accountKey = ACMEAccountKey.LoadOrCreate("/var/lib/myserver/acme/account.pem");
using var client     = new ACMEClient(ACMEClient.LetsEncrypt, accountKey);

await client.CreateAccountAsync(["admin@example.org"], TermsOfServiceAgreed: true);

var http01 = new HTTP01ChallengeResponder();
http01.RegisterAt(port80);                    // a Hermod HTTPServer on port 80

await using var certificates = new ACMECertificateManager(
                                   client,
                                   ["www.example.org"],
                                   [ http01 ],
                                   Directory: "/var/lib/myserver/acme");

certificates.OnRenewed += certificate => Console.WriteLine($"New certificate: {certificate}");

await certificates.StartAsync();             // fetches now if needed, then keeps looking

// A TLS server asks at every handshake, and so presents a renewed certificate
// from the next handshake on:
var chain = certificates.Selector(serverName);
```

Let's Encrypt asks on port 80 of the name being proven, from the internet. It
follows redirects to HTTPS, so a port 80 that redirects everything still works,
as long as the HTTPS side serves the path as well.

## A server nobody can reach: dns-01

A time server in a local network has no public port 80. The certificate
authority then asks the public DNS instead of the server:

```csharp
var dns01 = new DNS01ChallengeResponder(
                Publish:           (name, value, ct) => myDnsProvider.AddTxtAsync   (name, value, ct),
                Withdraw:          (name, value, ct) => myDnsProvider.DeleteTxtAsync(name, value, ct),
                PropagationDelay:  TimeSpan.FromSeconds(30));
```

How the zone is changed is up to the caller: a provider's API, an RFC 2136
update, or a zone file. A zone that should not be handed to the server at all
can stay as it is, with a CNAME from `_acme-challenge.time.example.org` to a
name in a zone kept only for this purpose. Wildcard certificates are always
proven this way.

## When a certificate is renewed

- **When the CA suggests**: where the CA offers renewal information (ARI), the
  manager picks a random time inside the suggested window. It looks again
  every `CheckInterval` (six hours by default), so a window the CA moves, ahead
  of a revocation for instance, is noticed.
- **Otherwise by lifetime**: when a third of the certificate's lifetime is
  left. That is 30 days of a 90-day certificate, and two days of a six-day one.
- **At once** when there is no certificate, or it is for other names than
  configured.

A failed renewal keeps the old certificate. `LastError` and `OnRenewalFailed`
say why, and the renewal is tried again after `RetryInterval` (one hour by
default). A renewal order names the certificate it replaces (ARI `replaces`).

The files are written atomically, key first. A `ServerCertificateFile` in
another process therefore reads them exactly as it reads certbot's files. On
the next start the manager finds them and asks the CA for nothing until they
are due.

## Not here (yet)

tls-alpn-01, choosing an alternate chain, account key rollover, and account
deactivation.

## Tests

`HermodTests/ACME` runs against `TestACMEServer`, a small ACME CA running in the
test process on a Hermod HTTP server. It is strict about:

- the ES256 signature of every request against the account key;
- nonces that were handed out and not used before;
- the `url` in the protected header;
- `jwk` versus `kid`;
- the external account binding's MAC;
- a CSR that names exactly the order's identifiers.

It checks http-01 by really requesting the token from a second Hermod HTTP
server, with the name as Host, and dns-01 in a zone the test's delegates write
to. It issues certificates from its own intermediate CA.

The tests cover:

- accounts;
- http-01, dns-01 and wildcard orders, and IP addresses;
- `badNonce` retries, refused answers, and responders that are cleaned up;
- the RFC 9773 example identifier;
- the manager's files, ARI windows, the one-third rule, failures, and the
  background loop.

One `External` test fetches Let's Encrypt staging's directory and asks it
whether a new key has an account. That needs a correctly signed request.
