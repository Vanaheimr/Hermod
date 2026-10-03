# Hermod SMTP support

This document describes the SMTP capabilities of the Hermod mail stack: the
inbound **MTA/MSA server**, the MTA→MTA **relay client**, the app→server
**submission client**, and the typed **message model** (MIME/OpenPGP). It
distinguishes wire-protocol and cryptographic-protocol support from operational
policy: Hermod implements and validates the SMTP, TLS, and email-authentication
protocols, while site policy such as anti-spam scoring, mailbox provisioning,
and reputation management is out of scope (see
[Deliberate exclusions](#deliberate-exclusions-and-qualification)).

The stack lives in the [Vanaheimr Hermod](https://github.com/Vanaheimr/Hermod)
library under namespace `org.GraphDefined.Vanaheimr.Hermod.SMTP` (folder
`Hermod/SMTP/`). Inbound-server-only pieces (listener, per-connection session,
SASL auth, rate-limiting, user/mailbox storage, inbound report ingestion) live
in the child namespace `…SMTP.Server`; everything reusable (message model,
SPF/DKIM/DMARC/ARC/DANE/TLS-RPT engines, the outbound client and queue) stays in
`…SMTP`. A companion operational guide — configuration, DNS records, deployment,
and worked API examples — lives in a separate repository, the deployable server
instance: [**Vanaheimr/SMTPServerCLI** → `README.md`](https://github.com/Vanaheimr/SMTPServerCLI/blob/master/README.md).

Last verified: **2026-10-03**

## Support levels

| Level | Meaning |
|---|---|
| **Implemented and regression-tested** | Hermod implements the behavior and the repository contains focused automated tests. |
| **Implemented and cross-validated** | Implemented and checked against an independent reference implementation (`dkimpy`, GnuPG), the RFC example vectors, and/or live signed/published domains. |
| **Model/API support** | Hermod exposes the commands, parameters, or data types, but enforcement or downstream policy is the application's responsibility. |
| **Opt-in** | Implemented but disabled by default; enabled by configuration (privacy-sensitive or deployment-specific behavior). |
| **Out of scope** | Intentionally not implemented by this stack. |

## Standards and specifications

### Core SMTP / ESMTP transport

| Specification | Hermod support |
|---|---|
| [RFC 5321](https://www.rfc-editor.org/rfc/rfc5321.html), SMTP | Implemented and regression-tested: `HELO`/`EHLO`/`MAIL`/`RCPT`/`DATA`/`RSET`/`NOOP`/`QUIT`/`VRFY`, the transaction state machine (`RSET` aborts the mail transaction and nothing else, §4.1.1.5), `Received:` trace fields (§4.4), dot-stuffing (§4.5.2), line-length limits (§4.5.3.1) enforced while reading, and null-sender (`<>`) handling. Lines end at CR LF and nowhere else (§2.3.8): a bare CR or LF never ends a command or `DATA`, which closes SMTP smuggling; content containing one is rejected by default (`RejectBareLineEndings`). Correct `MAIL FROM:<…>`/`RCPT TO:<…>` syntax on both server and client. |
| [RFC 5322](https://www.rfc-editor.org/rfc/rfc5322.html), Internet Message Format | Implemented: RFC 5322 address parsing (display names, angle addresses, quoted local-parts, domain-literals, comments, groups, comma-aware lists) and the typed header/body message model. |
| [RFC 1870](https://www.rfc-editor.org/rfc/rfc1870.html), SMTP SIZE | Implemented: advertised with the configured maximum; a declared `SIZE=` above it is refused with `552` at `MAIL` (§6.1); the submission/relay client declares `SIZE=` on `MAIL FROM` and pre-checks the server's limit. |
| [RFC 6152](https://www.rfc-editor.org/rfc/rfc6152.html), 8BITMIME | Implemented: 8-bit content accepted; `BODY=8BITMIME` emitted when advertised. The submission client does not send 8-bit content to a server without 8BITMIME: there is no 7-bit conversion, so it ends with `MailSentStatus.EightBitNotSupported` before `MAIL` (RFC 6152 §3, second option). |
| [RFC 6531](https://www.rfc-editor.org/rfc/rfc6531.html), SMTPUTF8 | Implemented and regression-tested: UTF-8 preserved end-to-end on `DATA` and `BDAT` and in `MAIL`/`RCPT` addresses (§3.3); the client emits `SMTPUTF8` only when an address or the message actually needs it. |
| [RFC 6532](https://www.rfc-editor.org/rfc/rfc6532.html), Internationalized headers | Partial: UTF-8 header content is carried transparently; RFC 2047 encoded-word normalization is not fully implemented. |
| [RFC 2034](https://www.rfc-editor.org/rfc/rfc2034.html), Enhanced status codes | Implemented: `ENHANCEDSTATUSCODES` advertised; `x.y.z` codes on responses. |
| [RFC 3463](https://www.rfc-editor.org/rfc/rfc3463.html), Enhanced status code structure | Implemented: enhanced codes are parsed from replies and surfaced per-recipient and per-transaction in the submission client's `SMTPSendResult`. |
| [RFC 3030](https://www.rfc-editor.org/rfc/rfc3030.html), CHUNKING / BDAT (+ BINARYMIME) | Implemented and regression-tested: binary-safe `BDAT` chunking, no dot-stuffing; a refused `BDAT` still consumes its chunk (§2), so the chunk is never read as commands. On a submission port `BDAT` needs authentication exactly as `DATA` does. `BINARYMIME` is modeled on the client. |
| [RFC 2920](https://www.rfc-editor.org/rfc/rfc2920.html), PIPELINING | Implemented and regression-tested: command groups are read from a buffered, pipeline-safe reader and answered in wire order; buffered plaintext is discarded across `STARTTLS` (RFC 3207 §4.2 injection defense). |
| [RFC 6409](https://www.rfc-editor.org/rfc/rfc6409.html), Message submission | Implemented and regression-tested: submission ports require authentication before a message is accepted, by `DATA` or `BDAT` (`530 5.7.0`); the submission client is a first-class MSA client. |
| [RFC 3848](https://www.rfc-editor.org/rfc/rfc3848.html), ESMTP transmission types | Implemented: `with` protocol names `ESMTP`/`ESMTPS`/`ESMTPA`/`ESMTPSA` in the `Received:` header reflect TLS and auth state. |
| [RFC 3461](https://www.rfc-editor.org/rfc/rfc3461.html), Delivery Status Notifications (extension) | Implemented and regression-tested: `ENVID`/`RET`/`NOTIFY`/`ORCPT` parsed inbound and emitted outbound only when the peer advertises `DSN`. A relayed message keeps `ENVID` and `RET`, and each recipient its own `NOTIFY` and its received `ORCPT` (§5.2.1). |
| [RFC 3464](https://www.rfc-editor.org/rfc/rfc3464.html), DSN message format | Implemented: failure / delay / **success** `multipart/report; report-type=delivery-status` reports, with the RFC 3461-correct `delivered` vs `relayed` action. |
| [RFC 8098](https://www.rfc-editor.org/rfc/rfc8098.html), Message Disposition Notifications | Implemented and regression-tested (client-side): detect a request and generate a `multipart/report; report-type=disposition-notification`, loop-safe via `Auto-Submitted: auto-replied`. |
| [RFC 6710](https://www.rfc-editor.org/rfc/rfc6710.html), MT-PRIORITY | Implemented and regression-tested: advertised in EHLO, parsed from `MAIL FROM`, carried onto the relay, and used to order the outbound queue. |
| [RFC 2156](https://www.rfc-editor.org/rfc/rfc2156.html), MIXER (`Importance` header) | Implemented: header-level message importance build + parse (`Importance`/`Priority`/`X-Priority`/`X-MSMail-Priority`). |
| [RFC 8689](https://www.rfc-editor.org/rfc/rfc8689.html), REQUIRETLS | Implemented and regression-tested: honored on `MAIL FROM`, advertised only after TLS, kept on a relayed message, and propagated to enforced outbound delivery. |
| [RFC 7505](https://www.rfc-editor.org/rfc/rfc7505.html), Null MX | Implemented and regression-tested (relay client): a domain whose only MX is `0 .` gets no delivery attempt; the message bounces at once with `556 5.1.10` instead of being retried. |

### Authentication (SASL)

| Specification | Hermod support |
|---|---|
| [RFC 4954](https://www.rfc-editor.org/rfc/rfc4954.html), SMTP AUTH | Implemented and regression-tested: mechanism set depends on TLS state; the AUTH result is checked; submission requires authentication. A client response that is not base64 is `501 5.5.2`; `=` is the empty response (§4). A completed AUTH lasts for the session: `RSET` keeps it, and a second `AUTH` is `503`. `STARTTLS` ends it (RFC 3207 §4.2): an `AUTH` from before the upgrade does not carry into TLS. |
| [RFC 4616](https://www.rfc-editor.org/rfc/rfc4616.html), PLAIN | Implemented; refused in cleartext (`538`). |
| draft-murchison-sasl-login, LOGIN | Implemented; refused in cleartext. |
| [RFC 7677](https://www.rfc-editor.org/rfc/rfc7677.html) / [RFC 5802](https://www.rfc-editor.org/rfc/rfc5802.html), SCRAM-SHA-256 | Implemented and cross-validated: server and client halves interoperate; the password is never transmitted; server signature verified (mutual auth). Live STARTTLS+SCRAM end-to-end test. |
| [RFC 2195](https://www.rfc-editor.org/rfc/rfc2195.html), CRAM-MD5 | Model/API support on the submission client (offered as a fallback mechanism). |
| [RFC 4422](https://www.rfc-editor.org/rfc/rfc4422.html), SASL / EXTERNAL | Implemented and regression-tested (server): `EXTERNAL` uses the TLS client certificate, which opens an account only when its SHA-1 or SHA-256 thumbprint is listed for it; never by its subject name and never by a wildcard, since the handshake checks no CA. The server does not request a client certificate, so `EXTERNAL` is offered only when a client sends one unasked. |
| [RFC 8314](https://www.rfc-editor.org/rfc/rfc8314.html), TLS for submission/access | Implemented and regression-tested: implicit-TLS submission port; credentials are never sent over an unencrypted connection (server and client both refuse). |

### Transport security and TLS reporting

| Specification | Hermod support |
|---|---|
| [RFC 3207](https://www.rfc-editor.org/rfc/rfc3207.html), STARTTLS | Implemented and regression-tested on the MTA and submission ports; pipelined-plaintext discarded on upgrade. After the upgrade the server discards what it learned from the client in cleartext (§4.2): the mail transaction and a completed `AUTH`, so the client sends `EHLO` and authenticates again inside TLS; the client certificate, which comes from the negotiation, stays for `EXTERNAL`. The submission client in `STARTTLS` mode never falls back to cleartext: STARTTLS not offered, refused (`454`), data behind the `220` (where only the handshake may follow) or a failed handshake ends the attempt with `MailSentStatus.TLSUnavailable`, without retry. A server that refuses `EHLO` (500/501/502/504/550) gets `HELO` instead (RFC 5321 §3.2), with no ESMTP parameter on `MAIL`. |
| [RFC 7435](https://www.rfc-editor.org/rfc/rfc7435.html), Opportunistic security | Implemented: default MTA→MTA policy accepts an imperfect certificate (encryption beats cleartext) but logs it; enforced modes never downgrade. |
| [RFC 8461](https://www.rfc-editor.org/rfc/rfc8461.html), MTA-STS | Implemented: policy fetched via `_mta-sts` TXT + `https://mta-sts.<domain>/.well-known/mta-sts.txt`; MX filtered and TLS enforced in `enforce` mode. |
| [RFC 8460](https://www.rfc-editor.org/rfc/rfc8460.html), SMTP TLS Reporting (TLS-RPT) | Implemented and regression-tested, **opt-in**: outbound per-domain success/typed-failure aggregate reports (gzipped `application/tlsrpt+gzip`, DKIM-signed) **and** inbound ingestion of received reports. |
| [RFC 7672](https://www.rfc-editor.org/rfc/rfc7672.html) / [RFC 6698](https://www.rfc-editor.org/rfc/rfc6698.html), DANE / TLSA for SMTP | Implemented and cross-validated, **opt-in**: `_25._tcp.<mx>` TLSA lookup, DNSSEC-validated, certificate matched (usages DANE-EE(3)/DANE-TA(2); selectors cert/SPKI; matching exact/SHA-256/SHA-512). Fail-closed on bogus DNSSEC; verified against live signed zones. |
| [RFC 4033](https://www.rfc-editor.org/rfc/rfc4033.html)–[4035](https://www.rfc-editor.org/rfc/rfc4035.html), DNSSEC | Implemented and cross-validated (via Hermod `DNSSECValidator`): RRSIG signature verification (RSA-SHA1/256/512, ECDSA P-256/P-384, Ed25519/Ed448) and DS-based delegation walking to the IANA root. |

### Email authentication and reporting

| Specification | Hermod support |
|---|---|
| [RFC 7208](https://www.rfc-editor.org/rfc/rfc7208.html), SPF | Implemented and cross-validated: `all`/`ip4`/`ip6`/`a`/`mx`/`exists`/`include` + `redirect`; the 10-lookup limit; full macro expansion (§7), validated against the §7.4 vectors. `ptr` parsed/counted but not evaluated (deprecated §5.5); `exp=` not implemented. Hard `-all` rejected at the end of `DATA` / `BDAT`. Switched off by `VerifySpf = false`. |
| [RFC 6376](https://www.rfc-editor.org/rfc/rfc6376.html), DKIM | Implemented and cross-validated (both directions with `dkimpy`): one shared canonicalizer over raw bytes (`simple`/`relaxed` exact), UTF-8-correct hashing, bottom-up `h=`, empty-body handling. Signing (`rsa-sha256`) and verification; a broken signature is advisory (§6.1), left to DMARC. Signatures never carry the `l=` body-length tag (§8.2). Verification is reported unless `VerifyDkim = false`. |
| [RFC 7489](https://www.rfc-editor.org/rfc/rfc7489.html), DMARC | Implemented and cross-validated: policy lookup with organizational-domain fallback (embedded Public Suffix List), identifier alignment (relaxed/strict, SPF and DKIM), `pct` sampling, `p`/`sp` enforcement, all switched off by `VerifyDmarc = false`. Aggregate (RUA) + forensic (RUF) report generation is **opt-in**, with external-destination consent (§7.1). |
| [RFC 8617](https://www.rfc-editor.org/rfc/rfc8617.html), ARC | Implemented and cross-validated (both directions with `dkimpy`): chain validation (`arc=pass\|fail\|none`) on every inbound message, and sealing (AAR+AMS+AS) provided as a forwarder component. |
| [RFC 8601](https://www.rfc-editor.org/rfc/rfc8601.html), Authentication-Results | Implemented and regression-tested: accurate `spf=… smtp.mailfrom=`, `dkim=… header.d=`, `dmarc=… header.from=`, and `arc=` results written above the trace header; a method switched off in the configuration is left out rather than reported as `none`. |
| [RFC 6591](https://www.rfc-editor.org/rfc/rfc6591.html), ARF (feedback report) | Implemented, **opt-in**: DMARC forensic (RUF) reports carry the offending message's headers, rate-limited per domain. |
| [Public Suffix List](https://publicsuffix.org/) (not an RFC) | Implemented and cross-validated: embedded Mozilla PSL snapshot, validated against all official `test_psl.txt` vectors; drives DMARC organizational-domain derivation. |

### Message model (MIME / OpenPGP)

| Specification | Hermod support |
|---|---|
| [RFC 2045](https://www.rfc-editor.org/rfc/rfc2045.html)–[2049](https://www.rfc-editor.org/rfc/rfc2049.html), MIME | Implemented: typed compose/parse of text, HTML, `multipart/alternative`, `multipart/mixed`, attachments, and `multipart/report`. |
| [RFC 1847](https://www.rfc-editor.org/rfc/rfc1847.html), Security multiparts | Implemented and regression-tested: `multipart/signed` and `multipart/encrypted`; signature verification runs against the **raw on-the-wire bytes** preserved at parse time, not a re-serialization. |
| [RFC 3156](https://www.rfc-editor.org/rfc/rfc3156.html), MIME Security with OpenPGP | Implemented and cross-validated (GnuPG): compose (sign / sign+encrypt, all `To`+`Cc` recipients) and inbound verify / decrypt / decrypt-and-verify. There is deliberately no encrypt-without-sign mode. |
| [RFC 4880](https://www.rfc-editor.org/rfc/rfc4880.html), OpenPGP message format | Implemented via Bouncy Castle: detached signatures, PKESK-per-recipient encryption, one-pass signature verification. |
| [RFC 2047](https://www.rfc-editor.org/rfc/rfc2047.html), Encoded-word headers | Partial (see RFC 6532 above). |

## Transport and ports

- Three listeners: MTA (`25`), submission with STARTTLS (`587`), and implicit-TLS
  submission (`465`, bound only when a certificate is configured). Ports are
  configurable and default to non-privileged values (2525/2587/2465) so the
  server runs without elevated privileges.
- STARTTLS (RFC 3207) and implicit TLS (RFC 8314) share one handshake path;
  TLS 1.2 / 1.3.
- Outbound relay uses opportunistic TLS by default and enforced TLS under
  MTA-STS `enforce`, REQUIRETLS, DANE, or an explicit flag — enforced modes defer
  delivery rather than downgrade.
- All DNS (TXT, MX, A/AAAA, PTR, MTA-STS, TLSA, and DNSSEC RRSIG/DNSKEY/DS) goes
  through the injected Hermod `DNSClient`; there are no `nslookup`/`dig`
  subprocesses or `System.Net.Dns` calls in the active code.

## Roles

The stack separates three sending roles and one receiving role. A method value
or advertised extension applies to the roles noted here, not automatically to all.

| Role | Type | Responsibility |
|---|---|---|
| Inbound server | `SMTPServer` + `SMTPSession` | Accept connections on 25/587/465, run the transaction state machine, authenticate (SASL), evaluate SPF/DKIM/DMARC/ARC, write trace + `Authentication-Results`, store or relay. |
| Relay client (MTA→MTA) | `SMTPOutboundClient` | MX lookup, opportunistic/enforced TLS, DANE/MTA-STS, DKIM signing, delivery; driven by `MailSender` and the persistent queue. |
| Submission client (app→MSA) | `ISMTPSubmissionClient` / `SMTPSubmissionClient` | Hand a typed message to one configured submission server over TLS with SASL AUTH (SCRAM-SHA-256 preferred, never cleartext); fully-async transport with fast-fail connection detection, bounded retries, and a detailed `SMTPSendResult`. |
| Message model | `EMail` + builders + `OpenPGP` | Compose/parse MIME and OpenPGP; DSN/MDN request and generation; importance/priority. |

## Commands and EHLO extensions

**Commands:** `HELO`, `EHLO`, `STARTTLS`, `AUTH`, `MAIL FROM`, `RCPT TO`, `DATA`,
`BDAT`, `RSET`, `NOOP`, `QUIT`, `VRFY` (returns `252`; does not verify a mailbox).

**EHLO extensions advertised:** `SIZE`, `8BITMIME`, `PIPELINING`, `SMTPUTF8`,
`ENHANCEDSTATUSCODES`, `CHUNKING`, `DSN`, `MT-PRIORITY`, `STARTTLS` (until TLS is
active), `REQUIRETLS` (only after STARTTLS), and `AUTH` (mechanism set depends on
TLS state). See the [core transport](#core-smtp--esmtp-transport) and
[authentication](#authentication-sasl) tables for the governing RFCs.

## Transaction correctness

- Dot-stuffing / de-stuffing on `DATA` (RFC 5321 §4.5.2); a bare `.` body line is
  stuffed to `..` so it cannot terminate `DATA` early. `BDAT` is binary-safe and
  needs no stuffing.
- Configurable command (default 1024) and text-line (default 2048) length limits
  (RFC 5321 §4.5.3.1), enforced while the line is read; an over-long `DATA` line
  is rejected only after the terminating `.` so the connection stays in sync.
- Lines end at CR LF only (RFC 5321 §2.3.8). `<LF>.<LF>`, `<CR>.<CR>` and the
  mixed forms do not end `DATA`, so a message cannot carry a second, smuggled
  transaction (CVE-2023-51764 class). A bare CR or LF in a command makes it an
  invalid command (`500 5.5.2`); in content it rejects the message with
  `550 5.5.2` after the end of data, or — with `RejectBareLineEndings = false` —
  is normalized to CR LF inside the one message.
- A connection lost before the end of `DATA` delivers nothing.
- A refused `BDAT` (out of sequence, unauthenticated on a submission port, too
  large, rate-limited) reads and discards its announced octets before answering
  (RFC 3030 §2).
- `MAIL`/`RCPT` parameters are checked (RFC 5321 §4.1.1.11): an unknown one, or
  any after `HELO`, is `555`; a known one with an invalid value (`SIZE=huge`,
  `BODY=9BITMIME`, `RET=BODY`, `NOTIFY=NEVER,SUCCESS`, …) is `501`. A parameter
  repeated with the same value is tolerated; with a different value it is `501`.
- UTF-8 preserved on both `DATA` and `BDAT`; CRLF forced independent of host OS.
- The submission/relay client sends the canonical serialized message
  (`EMail.ToText()`), never a header-dictionary reconstruction, so DKIM signatures
  survive.

## Session limits

`RateLimitConfig` bounds what one session and one address may do:

- `MaxRcptPerSession` counts recipients over all transactions of a session;
  beyond it `RCPT` is answered `452 4.5.3`.
- `MaxMessagesPerSession`: the next `MAIL` is answered `421 4.7.0` and the
  connection closed, so the client reconnects and meets the per-address limits.
- A failed `AUTH` is answered only after `AuthFailDelayMs` and counts towards
  `MaxAuthAttemptsPerIpPerHour`; past that, `AUTH` is answered `421 4.7.0`.
- `MaxConnectionsPerIp`, `MaxConnectionsPerIpPerMinute`, `MaxTotalConnections`
  and `MaxMessagesPerIpPerHour` (unauthenticated only) apply per address.

`WhitelistedIps` (default: the loopback addresses) skips the per-address limits;
the per-session limits and the `AUTH` delay apply to every client.

## Testing and reference implementations

Correctness is validated against **independent** implementations, the RFC example
vectors, and live domains — not only self-consistency.

| Area | Reference / method |
|---|---|
| **DKIM / ARC** | Cross-validated both directions with Python [`dkimpy`](https://launchpad.net/dkimpy) (verifies our signatures/seals; our verifier accepts theirs), plus RFC 6376 §3.4.5 canonicalization vectors and tamper detection. |
| **SPF macros** | All RFC 7208 §7.4 example vectors. |
| **DMARC / PSL** | All 78 official `test_psl.txt` vectors; alignment building-block checks; live-DNS end-to-end against real published policies (`google.com`, `github.com`). |
| **OpenPGP / dot-stuffing** | Messages signed with GnuPG delivered over `DATA` and `BDAT`; signatures re-verified after the server round-trip to prove byte-exact preservation. |
| **DANE / DNSSEC** | Live TLSA + full DNSSEC chain validation against real signed zones (`posteo.de`, `mailbox.org`); deterministic certificate-matcher tests. |
| **TLS-RPT** | Aggregation + RFC 8460 §4 JSON round-trip (outbound); closed-loop gzip/JSON ingestion (inbound); live `_smtp._tls` lookup. |
| **Submission client** | SCRAM-SHA-256 crypto cross-validated against the server credential generator; a scriptable in-process fake server (Autobahn-style "mean" connection drops/stalls detected in < 10 s, never a 60 s hang; a TLS handshake the server never answers, after STARTTLS or on an implicit TLS port, ends after the command timeout); a live STARTTLS+SCRAM end-to-end send; a hanging server aborted promptly on caller cancellation. |
| **DNS** | Live queries via the Hermod `DNSClient` against real domains. |
| **Inbound session** | Raw-socket wire tests (`SMTPSessionWireTests`) for line endings, smuggling variants, BDAT chunk consumption, submission-port authentication, the session state `STARTTLS` discards, session limits and the verification switches; the whole server is additionally run against the external [SMTPConformanceTests](https://github.com/Vanaheimr/SMTPConformanceTests) suite (RFC-by-RFC, plus swaks, smtplib, openssl and Postfix smtp-sink). |

The committed SMTP regression suite (`HermodTests/SMTP/`) contains **186 passing
tests, 0 failed, 0 skipped** as of the verification date, covering the message
builders and OpenPGP (`EMailBuilderTests`), MDN (`MdnTests`, `MdnStorageTests`),
DSN (`DsnTests`), priority (`PriorityTests`), `MAIL`/`RCPT` parameters
(`ESMTPParametersTests`), the inbound session on the wire
(`SMTPSessionWireTests`), relayed parameters (`SMTPRelayParameterTests`), null MX
(`SMTPOutboundNullMxTests`), the user store (`FileUserStoreTests`), and the
submission client (`SMTPSubmissionClientTests`, `SMTPSubmissionClientWireTests`,
`SMTPSubmissionClientStartTlsTests`, `SMTPSubmissionClientTlsScramTests`,
`SMTPSubmissionClientHeloFallbackTests`, `SMTPSubmissionClient8BitTests`). The
SPF/DKIM/DMARC/ARC/DANE/TLS-RPT engines were validated with the dedicated
harnesses listed above.

Run the committed suite with:

```powershell
dotnet test HermodTests\HermodTests.csproj --filter "FullyQualifiedName~Tests.SMTP"
```

## Deliberate exclusions and qualification

Hermod's SMTP stack is **protocol- and standards-complete for its intended
scope** — a correct, tested reference implementation of the modern SMTP,
transport-security, and email-authentication standards. These boundaries are
explicit:

- **Not a hardened production MX.** There is no anti-spam / anti-abuse layer
  (greylisting, DNSBL/RBL, content scoring, reputation); recipients are accepted
  catch-all with no mailbox-existence check (backscatter risk); the parsers
  process untrusted input and have not been fuzzed or security-audited.
- **No mailbox access protocol.** Inbound mail is stored as flat `.eml` files;
  IMAP/POP/Maildir and quota are out of scope.
- **SPF `exp=` and `ptr` evaluation** are not implemented (`ptr` is deprecated).
- **Header internationalization** (RFC 2047 encoded-words / full RFC 6532) is only
  partially handled.
- **Operational gaps:** no mail-loop/`Received`-hop-count limit, no
  metrics/alerting; queue durability is not battle-tested.
- **A demonstration user store** (`FileUserStore`, a flat file): passwords are
  checked against salted PBKDF2 SCRAM keys, with an unsalted SHA-256 column only
  as a fallback for accounts without them; a missing file is created with no
  accounts. Replace it with a real database/LDAP.
- Modeling a command, parameter, or extension does not imply a policy engine:
  DSN/MDN/MT-PRIORITY/REQUIRETLS are honored per their RFCs, but site delivery
  policy is the operator's.

A claim of exhaustive RFC conformance would additionally require a maintained
requirement-by-requirement matrix, external compliance tooling, and continuous
parser fuzzing.

## Maintenance rule

When SMTP behavior changes:

1. Add a focused protocol/unit regression test under `HermodTests/SMTP/`.
2. Where an independent oracle exists (`dkimpy`, GnuPG, PSL vectors, a live signed
   domain), cross-validate against it rather than only self-consistency.
3. Use a raw/scriptable peer for wire-specific or "mean" behavior a high-level
   client cannot produce (abrupt drops, stalls, malformed replies).
4. Update this document's support statements, the standards tables, the exclusions,
   and the last verification date. If the change is user-visible, also update the
   operational guide, the `README.md` of the separate
   [**Vanaheimr/SMTPServerCLI**](https://github.com/Vanaheimr/SMTPServerCLI/blob/master/README.md)
   repository.
