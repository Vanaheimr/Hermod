/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public sealed class SMTPOutboundClient
    {

        private readonly SmtpOutboundConfig    _config;
        private readonly DkimSigner?           __dkimSigner;
        private readonly Func<String, DkimSigner?>? _dkimSignerFor;
        private readonly Func<String, SmartHostRoute?>? _smartHostFor;
        private readonly MtaStsResolver        _mtaStsResolver;
        private readonly DaneResolver?         _daneResolver;
        private readonly DNSSECValidator?      _dnssecValidator;
        private readonly Action<TlsRptEvent>?  _tlsRptRecorder;
        private readonly IDNSClient            _dnsClient;
        private readonly ILogger               _logger;

        /// <param name="dkimSignerFor">
        /// The DKIM signer for the domain of a message's From: address, or null for one this
        /// server does not sign for - so that every domain is signed with its own key (H5).
        /// When it gives none, the one signer above is asked, if there is one.
        /// </param>
        /// <param name="smartHostFor">
        /// The way out for the domain of a message's From: address - directly, or through a smart
        /// host of its own - or null for the way of the configuration: so that a domain whose SPF
        /// names its provider's relay leaves through that relay, and the others as the server does.
        /// </param>
        public SMTPOutboundClient(SmtpOutboundConfig              config,
                                  DkimSigner?                     _dkimSigner,
                                  IDNSClient                      dnsClient,
                                  ILogger                         logger,
                                  Action<TlsRptEvent>?            tlsRptRecorder   = null,
                                  Func<String, DkimSigner?>?      dkimSignerFor    = null,
                                  Func<String, SmartHostRoute?>?  smartHostFor     = null)
        {

            this._config          = config;
            this.__dkimSigner     = _dkimSigner;
            this._dkimSignerFor   = dkimSignerFor;
            this._smartHostFor    = smartHostFor;
            this._dnsClient       = dnsClient;
            this._logger          = logger;
            this._mtaStsResolver  = new MtaStsResolver(dnsClient,
                                                       logger,
                                                       config.MtaStsHttpHandler,
                                                       config.PublicAddressesOnly ? config.IsPublicAddress : null);
            this._dnssecValidator = config.EnableDane
                                        ? config.DnssecTrustAnchors is { } anchors
                                              ? new DNSSECValidator(dnsClient, anchors)
                                              : DNSSECValidator.WithRootTrustAnchor(dnsClient)
                                        : null;
            this._daneResolver    = _dnssecValidator is not null
                                        ? new DaneResolver(dnsClient, logger, _dnssecValidator)
                                        : null;
            this._tlsRptRecorder  = tlsRptRecorder;

        }

        // Raw-string send. Kept internal on purpose: external callers should compose a typed
        // EMail/EMailEnvelop and go through MailSender (which serializes it), rather than passing
        // an unchecked message string here. The QueueProcessor drives this for queued delivery.
        internal async Task<SendResult> SendAsync(String             targetDomain,
                                                  String             envelopeFrom,
                                                  String[]           recipients,
                                                  String             messageContent,
                                                  Boolean            requireTls   = false,
                                                  DsnParameters?     dsn          = null,
                                                  SByte              priority     = 0,
                                                  CancellationToken  ct           = default,
                                                  IReadOnlyCollection<RecipientDsn>? recipientDsns = null,
                                                  Byte[]?            messageOctets = null)
        {

            var startTime = Timestamp.Now;

            // RFC 6531 §3.2: "When doing lookups, the SMTPUTF8-aware SMTP client or server MUST
            // either use a Unicode-aware DNS library, or transform the internationalized domain
            // name to A-label form" - the MX records, the MTA-STS policy, the address of a domain
            // in U-labels are those of its A-labels.
            targetDomain = Idna.ToAscii(targetDomain);

            try
            {

                // DKIM: with the key of the From: domain - unless the message carries a signature of
                // that domain already, made before it was queued, so that the copy its sender kept
                // is the message that was sent.
                // A message given as octets is signed - and sent - as exactly those octets.
                var asText     = messageOctets is not null ? MessageOctets.ToText(messageOctets) : messageContent;
                var fromDomain = DkimSigning.FromDomain(asText);
                var signer     = (fromDomain is not null ? _dkimSignerFor?.Invoke(fromDomain) : null) ?? __dkimSigner;

                if (signer is not null && !(fromDomain is not null && DkimSigning.IsSignedBy(asText, fromDomain)))
                {
                    if (messageOctets is not null)
                        messageOctets  = signer.SignOctets(messageOctets);
                    else
                        messageContent = signer.SignMessage(messageContent);
                }

                // The way out: the one of the From: domain; for a message forwarded here - its From:
                // of elsewhere, its envelope sender of here - the one of the envelope sender's domain,
                // whose SPF the next hop asks; or the configuration's.
                var senderDomain  = envelopeFrom.LastIndexOf('@') is var at and > 0 ? envelopeFrom[(at + 1)..] : null;
                var route         = (fromDomain   is not null ? _smartHostFor?.Invoke(fromDomain) : null) ??
                                    (senderDomain is not null && !senderDomain.Equals(fromDomain, StringComparison.OrdinalIgnoreCase)
                                         ? _smartHostFor?.Invoke(senderDomain)
                                         : null) ??
                                    SmartHostRoute.Of(_config);

                // MTA-STS (RFC 8461). §3.4: through a smart host, "compliant senders MUST treat the
                // smart host domain as the Policy Domain" - the recipient domain's policy names its
                // own MX hosts, not the relay we hand the message to. An address literal has none.
                //
                // RFC 8689 §3: a message with "TLS-Required: No" goes "regardless of its ability to
                // negotiate STARTTLS with the SMTP server, ignoring policy-based mechanisms (including
                // MTA-STS and DANE), if any, asserted by the recipient domain" - it may be the report
                // on that domain's broken TLS. §4.1: "If the REQUIRETLS MAIL FROM parameter is
                // specified, the TLS-Required header field MUST be ignored".
                var tlsOptional   = !requireTls &&
                                    TlsRequiredNo(messageOctets is not null
                                                      ? MessageOctets.ToText(MessageOctets.Header(messageOctets))
                                                      : messageContent);

                // RFC 8689 §4.2.1 holds for a message that requires TLS "for which the MAIL FROM
                // return-path is not empty (an empty MAIL FROM return-path indicating a bounce
                // message)": a bounce goes on its way, with REQUIRETLS where the next hop offers it.
                var tlsRequired   = requireTls && !IsNullReversePath(envelopeFrom);

                var policyDomain  = Idna.ToAscii(route.Host ?? targetDomain);
                var mtaStsPolicy  = tlsOptional || System.Net.IPAddress.TryParse(policyDomain.Trim('[', ']'), out _)
                                        ? MtaStsPolicy.None
                                        : await _mtaStsResolver.GetPolicyAsync(policyDomain, ct);
                var enforceTls    = tlsRequired ||
                                    mtaStsPolicy.Mode == MtaStsMode.Enforce ||
                                    _config.RequireStartTls;

                if (mtaStsPolicy.Mode != MtaStsMode.None)
                {
                    _logger.Log(LogLevel.Info, $"MTA-STS policy for {policyDomain}: {mtaStsPolicy.Mode}");
                }

                if (tlsOptional)
                    _logger.Log(LogLevel.Info, $"TLS-Required: No - the TLS policies of {policyDomain}, if any, are set aside (RFC 8689 §3)");

                // The MX answer the hosts were taken from - none for a smart host, an address literal,
                // the implicit MX.
                DNSInfo? mxAnswer = null;

                // Determine target hosts
                IReadOnlyList<MxRecord> mxHosts;

                if (route.Host is not null)
                {
                    // Use smarthost relay
                    mxHosts = [new MxRecord(Idna.ToAscii(route.Host), 0)];
                    _logger.Log(LogLevel.Debug, $"Using smarthost: {route}");
                }
                else if (targetDomain.StartsWith('['))
                {
                    // An address literal (RFC 5321 §4.1.3) names the host itself: there is no MX
                    // record to look up.
                    mxHosts = [new MxRecord(targetDomain, 0)];
                }
                else
                {

                    // MX lookup. RFC 5321 §5.1: "If a non-existent domain error is returned, this
                    // situation MUST be reported as an error. If a temporary error is returned, the
                    // message MUST be queued and retried later" - the implicit MX below is for "an
                    // empty list of MXs" only, and "applies only if there are no MX records present".
                    var (lookup, records, response) = await ResolveMxAsync(targetDomain, ct);

                    if (lookup == MxLookup.TemporaryError)
                        return SendResult.TempFail(451, $"4.4.3 The MX lookup of {targetDomain} failed; the message is queued");

                    if (lookup == MxLookup.NonExistentDomain)
                        return SendResult.PermFail(550, $"5.1.2 The domain {targetDomain} does not exist");

                    mxHosts = records;

                    // Null MX (RFC 7505): "MX 0 ." says the domain accepts no mail. Delivery
                    // MUST NOT be attempted (§4.1), so this is a bounce now - not a connection
                    // to an empty host name, which failed as a temporary error and was retried.
                    if (mxHosts.Count > 0 && mxHosts.All(mx => mx.Host.Length == 0))
                    {
                        _logger.Log(LogLevel.Warning, $"{targetDomain} has a null MX (RFC 7505); not delivering");
                        return SendResult.PermFail(556, "5.1.10 Recipient address has null MX");
                    }

                    // A null MX next to real ones is malformed (RFC 7505 §3); the real ones are tried.
                    mxHosts = [.. mxHosts.Where(mx => mx.Host.Length > 0)];

                    if (mxHosts.Count > 0)
                        mxAnswer = response;

                    if (mxHosts.Count == 0)
                    {
                        // Fallback to A/AAAA record
                        mxHosts = [new MxRecord(targetDomain, 0)];
                    }

                    _logger.Log(LogLevel.Debug, $"MX records for {targetDomain}: {String.Join(", ", mxHosts.Select(m => $"{m.Host}:{m.Priority}"))}");

                    // RFC 5321 §5.1: a relay "MUST then inspect the list for any of the names or
                    // addresses by which it might be known in mail transactions. If a matching record
                    // is found, all records at that preference level and higher-numbered ones MUST
                    // be discarded from consideration. If there are no records left at that point, it
                    // is an error condition, and the message MUST be returned as undeliverable."
                    var own = mxHosts.Where(mx => IsOwnName(mx.Host)).ToList();
                    if (own.Count > 0)
                    {

                        var level = own.Min(mx => mx.Priority);
                        mxHosts   = [.. mxHosts.Where(mx => mx.Priority < level)];

                        if (mxHosts.Count == 0)
                        {
                            _logger.Log(LogLevel.Warning, $"{_config.LocalHostname} is the best MX host of {targetDomain}; not delivering to itself");
                            return SendResult.PermFail(550, $"5.4.6 {_config.LocalHostname} is the best MX host of {targetDomain}: a routing loop");
                        }

                    }

                }

                // RFC 8461 §5: under an "enforce" policy no delivery to a host the policy does not
                // name (§4.1) - the smart host included. And "a compliant MTA MUST NOT permanently
                // fail to deliver messages before checking, via DNS, for the presence of an updated
                // policy ... MTAs SHOULD treat such failures as transient errors": the TXT record
                // was looked at for this attempt, and the next attempt looks again.
                if (mtaStsPolicy.Mode == MtaStsMode.Enforce)
                {
                    var allowed = mxHosts.Where(mx => mtaStsPolicy.MatchesMx(mx.Host)).ToList();
                    if (allowed.Count == 0)
                    {
                        _logger.Log(LogLevel.Error, $"MTA-STS policy of {policyDomain} allows none of {String.Join(", ", mxHosts.Select(mx => mx.Host))}");
                        return SendResult.TempFail(451, $"4.7.5 MTA-STS policy of {policyDomain} allows none of its MX hosts");
                    }
                    mxHosts = allowed;
                }

                // RFC 8689 §2: "If the SMTP server to which the message is being transmitted is
                // identified through an MX record lookup, its name MUST be validated via a DNSSEC
                // signature on the recipient domain's MX record, or the MX hostname MUST be validated
                // by an MTA-STS policy as described in Section 4.1 of [RFC8461]"; §4.2.1: "If there
                // are no more MX hosts, the client MUST NOT transmit the message to the domain" -
                // an MX record that may be forged leads to whoever forged it (§8.2).
                if (tlsRequired && mxAnswer is not null)
                {

                    if (mtaStsPolicy.Mode is MtaStsMode.Enforce or MtaStsMode.Testing)
                    {

                        var named = mxHosts.Where(mx => mtaStsPolicy.MatchesMx(mx.Host)).ToList();

                        if (named.Count == 0)
                            return SendResult.PermFail(550, $"5.7.10 REQUIRETLS: the MTA-STS policy of {policyDomain} names none of the MX hosts of {targetDomain}");

                        mxHosts = named;

                    }

                    else
                    {

                        var dnssec = _dnssecValidator is not null
                                         ? await _dnssecValidator.ValidateAsync(mxAnswer, CancellationToken: ct)
                                         : DNSSECValidationResult.Insecure;

                        if (dnssec is DNSSECValidationResult.Bogus or DNSSECValidationResult.Indeterminate)
                            return SendResult.TempFail(451, $"4.7.5 REQUIRETLS: the MX records of {targetDomain} did not validate ({dnssec}); the message is queued");

                        if (dnssec != DNSSECValidationResult.Secure)
                            return SendResult.PermFail(550, $"5.7.10 REQUIRETLS: neither DNSSEC nor an MTA-STS policy vouches for the MX hosts of {targetDomain}");

                    }

                }

                // Try each MX host in priority order
                Exception?  lastException  = null;
                SendResult? lastResult     = null;
                var         notPublic      = new List<String>();
                var         noAddress      = new List<String>();

                // RFC 5321 §5.1: "If there are multiple destinations with the same preference and
                // there is no clear reason to favor one ... then the sender-SMTP MUST randomize them
                // to spread the load across multiple mail exchangers for a specific organization."
                foreach (var mx in mxHosts.Select(mx => (MX: mx, Order: Random.Shared.Next())).
                                           OrderBy(mx => mx.MX.Priority).
                                           ThenBy (mx => mx.Order).
                                           Select (mx => mx.MX))
                {
                    try
                    {
                        var result = await TrySendToMxAsync(
                                               mx.Host,
                                               route,
                                               route.Host is not null
                                                   ? route.Port
                                                   : _config.MxPort,
                                               envelopeFrom,
                                               recipients,
                                               messageContent,
                                               messageOctets,
                                               enforceTls,
                                               requireTls,
                                               tlsOptional,
                                               targetDomain,
                                               policyDomain,
                                               mtaStsPolicy,
                                               dsn ?? DsnParameters.None,
                                               priority,
                                               recipientDsns ?? [],
                                               ct
                                           );

                        if (result.Status == SendStatus.Success)
                        {
                            var duration = Timestamp.Now - startTime;
                            return result with { Duration = duration };
                        }

                        // A permanent failure of the message - a refused sender, recipients, content - is
                        // the domain's answer: no other MX host is tried. A refused session is not: RFC
                        // 5321 §5.1, "the SMTP client MUST be able to try (and retry) each of the relevant
                        // addresses in this list in order, until a delivery attempt succeeds".
                        if (result.Status == SendStatus.PermFail && !result.SessionRefused && !result.UnsuitableHost)
                        {
                            return result;
                        }

                        // Refused for now, or the session refused - try the next MX host. What one of
                        // them refused only for now is tried again later, whatever the others said.
                        if (lastResult is null || lastResult.Status != SendStatus.TempFail || result.Status == SendStatus.TempFail)
                            lastResult = result;

                        _logger.Log(LogLevel.Warning, $"MX {mx.Host} failed: {result.ResponseCode} {result.ResponseText}");
                    }
                    catch (NoPublicAddressException ex)
                    {
                        notPublic.Add(ex.Message);
                        _logger.Log(LogLevel.Warning, $"MX {ex.Message} - not connected to");
                    }
                    catch (DNS.NameResolutionException ex)
                    {
                        lastException = ex;
                        _logger.Log(LogLevel.Warning, $"MX {mx.Host}: {ex.Message}");

                        // A name its name servers say has no address - not one they could not be asked about.
                        if (await HasNoAddressAsync(mx.Host, ct))
                            noAddress.Add(mx.Host);
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.Log(LogLevel.Warning, $"MX {mx.Host} connection failed: {ex.Message}");
                    }
                }

                // Every MX host leads into a network that is not the internet's: there is no way to
                // this domain from here, now or at the next attempt - unless its DNS changes, which
                // its owner learns sooner from a bounce than after days of retries.
                if (notPublic.Count == mxHosts.Count)
                    return SendResult.PermFail(550, $"5.4.4 No MX host of {targetDomain} has a public address: {String.Join("; ", notPublic)}");

                // RFC 5321 §5.1: "If MX records are present, but none of them are usable, or the
                // implicit MX is unusable, this situation MUST be reported as an error" - no MX host
                // has an address, as its name servers say: not something a later attempt changes.
                if (noAddress.Count > 0 && noAddress.Count + notPublic.Count == mxHosts.Count)
                    return SendResult.PermFail(550, $"5.4.4 No MX host of {targetDomain} has an address: {String.Join(", ", noAddress)}");

                // All MX hosts refused the session, or could not take the message the way it has to
                // go, and one could not be reached at all: that one may take it at the next attempt.
                if (lastResult is { Status: SendStatus.PermFail } && (lastResult.SessionRefused || lastResult.UnsuitableHost) && lastException is not null)
                    return SendResult.TempFail(lastResult.ResponseCode is >= 500 and < 600 ? 451 : lastResult.ResponseCode,
                                               $"{lastResult.ResponseText}; and not reached: {lastException.Message}",
                                               lastResult.RemoteMx);

                // All MX hosts failed: the last answer as it was - with its recipients' answers.
                return lastResult
                           ?? SendResult.TempFail($"All MX hosts unreachable: {lastException?.Message}");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Send failed to {targetDomain}: {ex.Message}");
                return SendResult.TempFail($"Send error: {ex.Message}");
            }
        }

        /// <param name="nextHopDomain">The recipient domain: with an MX host, a reference identifier of DANE (RFC 7672 §3.2.3).</param>
        /// <param name="policyDomain">The Policy Domain of MTA-STS (RFC 8461 §3.4) - the smart host's through one - and so
        /// the "policy-domain" of the session's TLS-RPT event (RFC 8460 §4.4).</param>
        /// <param name="stsPolicy">The MTA-STS policy of the Policy Domain.</param>
        private async Task<SendResult> TrySendToMxAsync(String              mxHost,
                                                        SmartHostRoute      route,
                                                        IPPort              port,
                                                        String              envelopeFrom,
                                                        String[]            recipients,
                                                        String              messageContent,
                                                        Byte[]?             messageOctets,
                                                        Boolean             enforceTls,
                                                        Boolean             requireTls,
                                                        Boolean             tlsOptional,
                                                        String              nextHopDomain,
                                                        String              policyDomain,
                                                        MtaStsPolicy        stsPolicy,
                                                        DsnParameters       dsn,
                                                        SByte               priority,
                                                        IReadOnlyCollection<RecipientDsn> recipientDsns,
                                                        CancellationToken   ct)
        {

            var stsMode     = stsPolicy.Mode;

            // RFC 8689 §4.2.1, for a message that requires TLS and has a sender to report to.
            var tlsRequired = requireTls && !IsNullReversePath(envelopeFrom);

            // The addresses of the host, from the DNS client - never from the resolver of the operating
            // system - and the internet's only, when so configured, for an MX host, never for a smart
            // host. Asked before anything else is looked up for this host; connected to are then
            // these addresses, not the name, which could have others a moment later.
            var all       = await PublicAddresses.ResolveAsync(mxHost, _dnsClient, ct);
            var addresses = all;

            if (route.Host is null && _config.PublicAddressesOnly)
            {

                addresses = [.. all.Where(_config.IsPublicAddress)];

                if (addresses.Length == 0)
                    throw new NoPublicAddressException($"{mxHost} has no public address ({String.Join(", ", all.Select(address => address.ToString()))})");

            }

            // DANE (RFC 7672): resolve DNSSEC-validated TLSA records for this MX host up front.
            // A "bogus" result means the destination advertises DANE but the records cannot be
            // trusted — fail closed and defer rather than risk an unauthenticated channel.
            var                  daneActive   = false;
            var                  daneTls      = false;
            IReadOnlyList<TLSA>  daneRecords  = [];
            IReadOnlyList<TLSA>  daneRRset    = [];

            // RFC 7672 §2.2: "When the original next-hop destination is an address literal ...
            // DANE TLS does not apply."
            // RFC 8689 §3: not for a message with "TLS-Required: No".
            if (_daneResolver is not null && !tlsOptional && !System.Net.IPAddress.TryParse(mxHost.Trim('[', ']'), out _))
            {

                var dane = await _daneResolver.ResolveTlsaAsync(mxHost, port, ct);

                if (dane.MustDefer)
                {
                    _logger.Log(LogLevel.Error,
                        $"DANE: whether DANE applies to {mxHost} cannot be known ({dane.Detail}); deferring (RFC 7672 §2.1.2)");
                    // TLS-RPT (RFC 8460 §4.3): a bogus DNSSEC result under DANE.
                    _tlsRptRecorder?.Invoke(new TlsRptEvent(policyDomain, TlsRptPolicyType.Tlsa, mxHost, null, null, false, "dnssec-invalid"));
                    return SendResult.TempFail(450, $"4.7.5 DANE TLSA lookup for {mxHost} failed: {dane.Detail}", mxHost);
                }

                daneActive   = dane.IsUsable;
                daneTls      = dane.RequiresTls;
                daneRecords  = [.. dane.Records.Where(DaneResult.IsUsableRecord)];
                daneRRset    = dane.Records;

                if (daneActive)
                    _logger.Log(LogLevel.Info,
                        $"DANE active for {mxHost}: {daneRecords.Count} usable TLSA record(s), TLS enforced");

                else if (daneTls)
                    _logger.Log(LogLevel.Info,
                        $"DANE: {mxHost} has secure TLSA records, none usable - TLS required, without authentication (RFC 7672 §2.2)");

            }

            // DANE mandates TLS to this MX (RFC 7672 §2.2), authenticated when a record is usable.
            // Credentials go to a smart host over TLS whose certificate is valid for its name only -
            // never to whoever answers STARTTLS on the way - unless cleartext was allowed outright.
            // SmartHostAuthWithoutTls is for a smart host on this machine - the loopback - and not
            // for a way that leads across a network, whoever configured it.
            var cleartextAuth  = _config.SmartHostAuthWithoutTls && IsLoopback(route.Host);
            var authenticating = route.Host is not null && route.Username is not null && !cleartextAuth;
            var mustEnforceTls = enforceTls || daneTls || authenticating;

            // RFC 7672 §3.2.3: the reference identifiers of a DANE-TA(2) name check - the host, and
            // for an MX host the next-hop domain.
            IReadOnlyCollection<String> referenceIdentifiers = route.Host is null
                                                                   ? [ mxHost, nextHopDomain ]
                                                                   : [ mxHost ];

            // RFC 5321 §5.1: "The destination host (perhaps taken from the preferred MX record) may
            // be multihomed, in which case the domain name resolver will return a list of alternative
            // IP addresses. ... the SMTP sender MUST try them in the order presented" - each with a
            // connect timeout of its own, not one for all: an address that does not answer at all
            // would leave none for the next. An address that answers and refuses the session -
            // greets with no 2yz - is followed by the next one as well; anything later in the
            // session is the host's answer.
            SendResult? refused     = null;
            Exception?  unreachable = null;

            foreach (var address in addresses)
            {

                using var client = new TcpClient(address.AddressFamily);

                try
                {

                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    connectCts.CancelAfter((Int32) _config.ConnectTimeoutMs);

                    _logger.Log(LogLevel.Debug, $"Connecting to {mxHost} ({address}):{port}");

                    await client.ConnectAsync(address, port.ToInt32(), connectCts.Token);

                }
                catch (Exception e) when (!ct.IsCancellationRequested)
                {
                    unreachable = e;
                    _logger.Log(LogLevel.Warning, $"{mxHost} ({address}) not reached: {e.Message}");
                    continue;
                }

                var result = await SessionAsync(client);

                if (!result.SessionRefused)
                    return result;

                // What one address refused only for now is tried again, whatever another said.
                if (refused is null || refused.Status != SendStatus.TempFail || result.Status == SendStatus.TempFail)
                    refused = result;

                _logger.Log(LogLevel.Warning, $"{mxHost} ({address}) refused the session: {result.ResponseCode} {result.ResponseText}");

            }

            // Every address that answered refused the session: that is the host's answer - unless
            // one refused only for now, or one could not be reached, which the next attempt may.
            if (refused is not null)
                return refused.Status == SendStatus.PermFail && unreachable is not null
                           ? SendResult.TempFail(451, $"{refused.ResponseText}; and not reached: {unreachable.Message}", mxHost) with { SessionRefused = true }
                           : refused;

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(unreachable!);
            return null!;

            // One session with the host, at the address the client is connected to.
            async Task<SendResult> SessionAsync(TcpClient client)
            {

                // TLS-RPT session context (RFC 8460): remember the peer/local IPs and record the
                // outcome under the policy type that governs this session.
                var receivingIp = (client.Client.RemoteEndPoint as System.Net.IPEndPoint)?.Address.ToString();
                var sendingIp   = (client.Client.LocalEndPoint  as System.Net.IPEndPoint)?.Address.ToString();

                void RecordTls(Boolean success, String? failureType)
                {
                    if (_tlsRptRecorder is null)
                        return;
                    var policyType = daneActive
                                         ? TlsRptPolicyType.Tlsa
                                         : stsMode is MtaStsMode.Enforce or MtaStsMode.Testing
                                             ? TlsRptPolicyType.Sts
                                             : TlsRptPolicyType.NoPolicyFound;
                    // RFC 8460 §4.5: the policy applied - the TLSA records, "a space-separated list of
                    // its four TLSA fields ... in presentation format", or the lines of the MTA-STS policy.
                    IReadOnlyList<String> policyString = policyType switch {
                        TlsRptPolicyType.Tlsa  => [.. daneRRset.Select(tlsa => $"{tlsa.CertificateUsage} {tlsa.Selector} {tlsa.MatchingType} {Convert.ToHexString(tlsa.CertificateAssociationData)}")],
                        TlsRptPolicyType.Sts   => stsPolicy.Lines,
                        _                      => []
                    };
                    _tlsRptRecorder(new TlsRptEvent(policyDomain, policyType, mxHost, receivingIp, sendingIp, success, failureType, policyString));
                }

                // RFC 8461 §5: "testing" - "Sending MTAs that also implement the TLSRPT specification
                // [RFC8460] send a report indicating policy application failures ...; in any case,
                // messages may be delivered as though there were no MTA-STS validation failure."
                // The mail goes on; the report says what the policy would have refused.
                var stsTesting = !daneActive && stsMode == MtaStsMode.Testing;

                // Every read and write within its timeout: the socket's ReceiveTimeout/SendTimeout do not
                // apply to asynchronous I/O, so a silent next hop would otherwise hold the delivery forever.
                var connection = new SMTPConnection(client.GetStream(),
                                                    TimeSpan.FromMilliseconds(_config.ReadTimeoutMs),
                                                    TimeSpan.FromMilliseconds(_config.WriteTimeoutMs));

                // Whether the session can still be ended with QUIT: from the greeting on, until the
                // connection is lost, a read times out, or a TLS handshake fails halfway.
                var sessionOpen = false;

                try
                {

                    // Read greeting
                    var greeting = await connection.ReadReplyAsync(ct);
                    sessionOpen  = true;
                    var secured  = false;
                    // RFC 5321 §4.2.1: every reply is read by its first digit.
                    if (!greeting.IsPositiveCompletion)
                        return ParseResponse(greeting.ToString(), mxHost) with { SessionRefused = true };

                    // EHLO
                    await connection.WriteLineAsync($"EHLO {EhloName}", ct);
                    var ehloResponse = await connection.ReadReplyAsync(ct);

                    // RFC 5321 §3.2: a server that does not know EHLO answers 500, 501, 502, 504 or 550,
                    // and the client falls back to HELO. Anything else - a 421 that ends the session, a
                    // 554 - is the server's answer to the attempt.
                    if (ehloResponse.Code is 500 or 501 or 502 or 504 or 550)
                    {

                        await connection.WriteLineAsync($"HELO {EhloName}", ct);
                        ehloResponse = await connection.ReadReplyAsync(ct);

                    }

                    if (!ehloResponse.IsPositiveCompletion)
                        return ParseResponse(ehloResponse.ToString(), mxHost);

                    var extensions = Extensions(ehloResponse);

                    // STARTTLS if available and desired/required
                    var supportsStartTls = extensions.ContainsKey("STARTTLS");

                    // RFC 8689 §3: "Nevertheless, the client SHOULD negotiate STARTTLS with the server if
                    // available" - for a message with "TLS-Required: No", and for a bounce with REQUIRETLS.
                    var wantTls = mustEnforceTls || _config.RequireStartTls || _config.PreferStartTls || stsTesting || tlsOptional || requireTls;

                    if (supportsStartTls && wantTls)
                    {

                        await connection.WriteLineAsync("STARTTLS", ct);
                        var starttlsResponse = await connection.ReadReplyAsync(ct);

                        if (starttlsResponse.IsPositiveCompletion)
                        {
                            // Under DANE the TLSA record authenticates the certificate directly — no
                            // PKIX path or name check (RFC 7672 §3.1). Otherwise validate strictly when
                            // TLS is enforced, else opportunistically (RFC 7435): encrypt but tolerate a bad cert.
                            var validateStrict = mustEnforceTls || _config.RequireValidCertificate;

                            // What strict validation finds wrong with the certificate, as a result type
                            // of RFC 8460 §4.3.1 - whether or not it is accepted all the same.
                            String? certificateFailure = null;

                            var sslStream = new SslStream(connection.Stream, false,
                                (_, cert, chain, errors) => {

                                    if (daneActive)
                                        return ValidateDaneCertificate(mxHost, daneRecords, referenceIdentifiers, cert, chain);

                                    // Secure TLSA records, none usable: encrypted, not authenticated (§2.2) -
                                    // unless the message requires TLS: RFC 8689 §4.2.1, "authenticate the
                                    // server's certificate as specified in [RFC6125] or [RFC7672]".
                                    if (daneTls && !tlsRequired)
                                        return true;

                                    var accepted = ValidateServerCertificate(mxHost, validateStrict, cert, chain, errors);

                                    // A validator of the operator's own decides what is valid.
                                    certificateFailure = _config.RemoteCertificateValidator is not null
                                                             ? accepted ? null : CertificateFailure(errors, chain) ?? "certificate-not-trusted"
                                                             : CertificateFailure(errors, chain);

                                    return accepted;

                                });

                            try
                            {
                                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                                {
                                    TargetHost = mxHost,
                                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                                    // Under DANE the chain the server presents is all there is to authenticate
                                    // against (RFC 7672 §3.1): build it offline. Otherwise the platform may go
                                    // fetching issuers from AIA URLs - on Windows for seconds, and when that
                                    // times out the chain comes back without the certificates the server sent,
                                    // the DANE-TA anchor among them. PKIX revocation does not apply either.
                                    CertificateChainPolicy = daneTls
                                                                 ? new X509ChainPolicy {
                                                                       DisableCertificateDownloads  = true,
                                                                       RevocationMode               = X509RevocationMode.NoCheck
                                                                   }
                                                                 : null
                                }, ct);
                            }
                            catch (AuthenticationException ex)
                            {
                                sessionOpen = false;     // half a handshake: neither TLS nor cleartext
                                // A rejected/mismatched certificate under enforced TLS (including a DANE
                                // TLSA mismatch) must not be bypassed: defer instead of downgrading.
                                var why = daneActive ? "DANE TLSA mismatch" : "certificate validation failed";
                                RecordTls(false, daneActive ? "validation-failure" : certificateFailure ?? "certificate-not-trusted");

                                // RFC 8689 §4.2.1: "Unable to establish TLS-protected SMTP session: 5.7.10
                                // Encryption needed" - this host; another MX host may do.
                                if (tlsRequired)
                                    return SendResult.PermFail(550, $"5.7.10 REQUIRETLS: TLS {why} for {mxHost}: {ex.Message}", mxHost) with { UnsuitableHost = true };

                                return SendResult.TempFail(454, $"TLS {why} for {mxHost}: {ex.Message}", mxHost);
                            }

                            // Whatever was read in cleartext behind the 220 is discarded (RFC 3207 §4.2).
                            connection.SwitchTo(sslStream);
                            secured = true;

                            _logger.Log(LogLevel.Debug,
                                $"TLS established with {mxHost}: {sslStream.SslProtocol}{(daneActive ? " (DANE-authenticated)" : "")}");

                            // TLS-RPT (RFC 8460): a compliant TLS session was established - unless an
                            // MTA-STS policy in testing mode would have refused it: a certificate that is
                            // not valid (RFC 8461 §4.2), or an MX host the policy does not name (§4.1).
                            var policyFailure = stsTesting
                                                    ? certificateFailure ?? (stsPolicy.MatchesMx(mxHost) ? null : "validation-failure")
                                                    : null;

                            if (policyFailure is not null)
                                _logger.Log(LogLevel.Warning, $"MTA-STS policy of {policyDomain} (testing) fails for {mxHost}: {policyFailure}; delivering all the same");

                            RecordTls(policyFailure is null, policyFailure);

                            // Re-send EHLO after TLS
                            await connection.WriteLineAsync($"EHLO {EhloName}", ct);
                            ehloResponse = await connection.ReadReplyAsync(ct);
                            if (!ehloResponse.IsPositiveCompletion)
                                return ParseResponse(ehloResponse.ToString(), mxHost);

                            // RFC 3207 §4.2: what was offered before TLS counts no more.
                            extensions = Extensions(ehloResponse);
                        }
                        else if (mustEnforceTls || _config.RequireStartTls)
                        {

                            RecordTls(false, "starttls-not-supported");

                            // A STARTTLS refused for good is no TLS for a message that requires it (RFC
                            // 8689 §4.2.1); one refused for now may be TLS at the next attempt.
                            if (tlsRequired && starttlsResponse.Code is >= 500 and < 600)
                                return SendResult.PermFail(550, $"5.7.10 REQUIRETLS: {mxHost} refused STARTTLS: {starttlsResponse}", mxHost) with { UnsuitableHost = true };

                            return SendResult.TempFail(454, $"STARTTLS required but failed: {starttlsResponse}", mxHost);

                        }

                        // RFC 8461 §5: testing mode goes on in cleartext, and reports it.
                        else if (stsTesting)
                            RecordTls(false, "starttls-not-supported");

                    }
                    else if (mustEnforceTls || _config.RequireStartTls)
                    {

                        RecordTls(false, "starttls-not-supported");

                        // RFC 8689 §4.2.1: "Unable to establish TLS-protected SMTP session: 5.7.10
                        // Encryption needed" - at this host; another MX host may offer STARTTLS.
                        if (tlsRequired)
                            return SendResult.PermFail(550, $"5.7.10 REQUIRETLS: {mxHost} offers no STARTTLS", mxHost) with { UnsuitableHost = true };

                        return SendResult.TempFail(454,
                            daneActive
                                ? $"DANE requires STARTTLS but {mxHost} does not offer it"
                                : "STARTTLS required but not supported",
                            mxHost);
                    }

                    // RFC 8461 §5: testing mode goes on in cleartext, and reports it.
                    else if (stsTesting)
                        RecordTls(false, "starttls-not-supported");

                    // AUTH if smarthost credentials provided - never in cleartext across a network: a
                    // password sent so is a password given away (RFC 4954 §4).
                    var signedIn = false;

                    if (route.Host is not null && route.Username is not null)
                    {

                        if (!secured && !cleartextAuth)
                            return SendResult.TempFail(454, $"No TLS with {mxHost}: the credentials for the smart host are not sent in cleartext", mxHost);

                        var authResult = await AuthenticateAsync(connection, extensions, route, ct);
                        if (!authResult.StartsWith('2'))
                        {
                            return ParseResponse(authResult, mxHost);
                        }

                        signedIn = true;
                    }

                    // DSN (RFC 3461) / MT-PRIORITY (RFC 6710): only emit params the remote advertised.
                    var supportsDsn         = extensions.ContainsKey("DSN");
                    var supportsMtPriority  = extensions.ContainsKey(MtPriority.Keyword);

                    // What the message needs of the next hop, and whether it offers it. A message it
                    // cannot take is not handed over in a form it did not ask for: no 7-bit conversion,
                    // which would break DKIM and OpenPGP signatures over the content.
                    var needsSmtpUtf8  = NeedsSmtpUtf8(envelopeFrom, recipients, messageOctets is not null ? MessageOctets.ToText(MessageOctets.Header(messageOctets)) : messageContent);
                    var has8Bit        = messageOctets is not null ? MessageOctets.Has8Bit(messageOctets) : messageContent.Any(c => c > '\x7F');

                    // RFC 3030 §3: binary data - a line of more than 998 octets, a NUL (RFC 2045 §2.7 to
                    // §2.9) - goes as BODY=BINARYMIME, which "can only be used with the "CHUNKING" service
                    // extension", and to a server that does not offer it "the sender-SMTP MUST NOT, under
                    // any circumstances, send binary data". There is no conversion here - re-encoding
                    // would break the signatures over the content -, so it "may treat this as a permanent
                    // error".
                    var isBinary       = messageOctets is not null ? MessageOctets.IsBinary(messageOctets) : MessageOctets.IsBinary(DataLines(messageContent));

                    if (isBinary && !(extensions.ContainsKey("BINARYMIME") && extensions.ContainsKey("CHUNKING")))
                        return SendResult.PermFail(554, $"5.6.3 {mxHost} does not offer BINARYMIME with CHUNKING, and the message has binary content", mxHost);

                    // RFC 6531 §3.2: a message that needs SMTPUTF8 goes only to a server that offers it.
                    if (needsSmtpUtf8 && !extensions.ContainsKey("SMTPUTF8"))
                        return SendResult.PermFail(553, $"5.6.7 {mxHost} does not offer SMTPUTF8, which the message needs", mxHost);

                    // RFC 5321 §2.4, RFC 6152 §3: no 8-bit content to a server without 8BITMIME - binary
                    // data, which goes as BINARYMIME, is 8-bit content as well.
                    if (has8Bit && !isBinary && !extensions.ContainsKey("8BITMIME"))
                        return SendResult.PermFail(554, $"5.6.3 {mxHost} does not offer 8BITMIME, and the message has 8-bit content", mxHost);

                    // RFC 8689 §4.2.1: a REQUIRETLS message goes only to a next hop that offers REQUIRETLS
                    // (which it does inside TLS only), and carries the option on. Not this one - "repeat
                    // steps 2-5 with each host on the recipient domain's list of MX hosts". A bounce goes
                    // on all the same (§5).
                    if (tlsRequired && !extensions.ContainsKey("REQUIRETLS"))
                        return SendResult.PermFail(550, $"5.7.30 REQUIRETLS support required, and {mxHost} does not offer it", mxHost) with { UnsuitableHost = true };

                    // MAIL FROM (with RET/ENVID and MT-PRIORITY when requested and supported)
                    var mailFromCommand = DsnCommands.MailFrom(envelopeFrom, dsn, supportsDsn);
                    mailFromCommand     = MtPriority.AppendMailFromParam(mailFromCommand, priority, supportsMtPriority);

                    if (isBinary)
                        mailFromCommand += " BODY=BINARYMIME";      // RFC 3030 §3
                    else if (has8Bit)
                        mailFromCommand += " BODY=8BITMIME";        // RFC 6152 §3

                    if (needsSmtpUtf8)
                        mailFromCommand += " SMTPUTF8";             // RFC 6531 §3.4

                    if (requireTls && extensions.ContainsKey("REQUIRETLS"))
                        mailFromCommand += " REQUIRETLS";           // RFC 8689 §4.2.1, §5

                    // RFC 4954 §5: "If the server does not sufficiently trust the authenticated identity
                    // of the client, or if the client is not authenticated, then the server MUST behave
                    // as if the AUTH=<> parameter was supplied", and "MUST supply the AUTH=<> parameter
                    // when relaying the message to any server which it has authenticated to". The relay
                    // vouches for no submitter: every message - forwarded, a bounce, one of a client
                    // that signed in - goes with AUTH=<>, so the smart host does not take it for one
                    // its account submitted.
                    if (signedIn)
                        mailFromCommand += " AUTH=<>";
                    await connection.WriteLineAsync(mailFromCommand, ct);
                    var mailResponse = await connection.ReadReplyAsync(ct);

                    // RFC 5321 §4.2.1: "a sender-SMTP MUST be prepared to handle codes not specified in
                    // this document and MUST do so by interpreting the first digit only" - a 252 to MAIL
                    // is the sender accepted, and the transaction goes on. Anything but a 2yz ends it; a
                    // 3yz, which MAIL never has, as a failure for now - never as a delivery.
                    if (!mailResponse.IsPositiveCompletion)
                        return Refused(mailResponse, mxHost);

                    // RCPT TO for each recipient (with NOTIFY/ORCPT when requested and supported)
                    var outcomes = new List<RecipientOutcome>();
                    foreach (var recipient in recipients)
                    {

                        // A relayed recipient carries the NOTIFY/ORCPT it was received with (RFC 3461
                        // §5.2.1); a message of our own applies the message-wide request to every one.
                        var recipientDsn = recipientDsns.FirstOrDefault(r => r.Recipient == recipient);

                        await connection.WriteLineAsync(recipientDsn is not null
                                                            ? $"RCPT TO:<{recipient}>" + DsnCommands.RcptToParams(recipientDsn, supportsDsn, needsSmtpUtf8)
                                                            : DsnCommands.RcptTo(recipient, dsn, supportsDsn, needsSmtpUtf8),
                                                        ct);
                        var rcptResponse = await connection.ReadReplyAsync(ct);

                        outcomes.Add(new RecipientOutcome(recipient, rcptResponse.Code, rcptResponse.Text, rcptResponse.Transcript));

                        if (outcomes[^1].Status != SendStatus.Success)
                            _logger.Log(LogLevel.Warning, $"Recipient {recipient} rejected: {rcptResponse}");

                    }

                    // Nobody accepted: refused for now if any was refused for now - those are tried
                    // again - else refused. Each recipient's answer goes with the result.
                    if (outcomes.All(outcome => outcome.Status != SendStatus.Success))
                    {
                        var first = outcomes.FirstOrDefault(outcome => outcome.Status == SendStatus.TempFail) ?? outcomes[0];
                        return (first.Status == SendStatus.TempFail
                                    ? SendResult.TempFail(first.Code, $"No recipient accepted: {first.Text}", mxHost)
                                    : SendResult.PermFail(first.Code, $"No recipient accepted: {first.Text}", mxHost))
                               with { Recipients = outcomes };
                    }

                    // Binary data as BDAT (RFC 3030 §3: "BINARYMIME cannot be used with the DATA
                    // command"), in one chunk, its octets as they are; everything else as DATA.
                    if (isBinary)
                    {
                        var chunk = MessageOctets.Bdat(messageOctets ?? Encoding.UTF8.GetBytes(String.Join("\r\n", DataLines(messageContent))));
                        await connection.WriteAsync([ .. Encoding.ASCII.GetBytes($"BDAT {chunk.Length} LAST\r\n"), .. chunk ], ct);
                    }

                    else
                    {

                        // DATA
                        await connection.WriteLineAsync("DATA", ct);
                        var dataResponse = await connection.ReadReplyAsync(ct);

                        // A 3yz is the go-ahead for the message: a 350 as a 354. A 2yz to DATA is no
                        // delivery - not a line of the message was sent.
                        if (!dataResponse.IsPositiveIntermediate)
                            return Refused(dataResponse, mxHost);

                        // Send message content with dot-stuffing, and end with <CRLF>.<CRLF>
                        await connection.WriteAsync(messageOctets is not null ? MessageOctets.Data(messageOctets) : DataOctets(messageContent), ct);

                    }

                    // RFC 5321 §4.5.3.2.6: 10 minutes for the reply to the end of data - the reply to BDAT
                    // LAST is that reply (RFC 3030 §2) -, the next hop is delivering. Given up on too soon,
                    // the message is sent again: twice.
                    var finalResponse = await connection.ReadReplyAsync(ct, TimeSpan.FromMilliseconds(_config.DataTerminationTimeoutMs));

                    // The message was taken for the accepted recipients; what became of the others goes
                    // with the result, for the queue to bounce or retry them. The reply as it was, every
                    // line of it, for a report to transcribe (RFC 3461 §9.2).
                    var delivery = ParseResponse(finalResponse.Transcript, mxHost) with { RemoteSupportsDsn = supportsDsn };

                    return delivery.Status == SendStatus.Success
                               ? delivery with { Recipients = outcomes }
                               : delivery;

                }
                catch
                {
                    // A lost connection, a timeout: nothing to say QUIT on.
                    sessionOpen = false;
                    throw;
                }
                finally
                {

                    // RFC 5321 §4.1.1.10: "The sender MUST NOT intentionally close the transmission
                    // channel until it sends a QUIT command, and it SHOULD wait until it receives the
                    // reply" - after a refused MAIL, RCPT, DATA or message, a refused STARTTLS or EHLO,
                    // as after a delivery. Best effort: the reply changes nothing about the result.
                    if (sessionOpen)
                    {
                        try
                        {
                            await connection.WriteLineAsync("QUIT", ct);
                            await connection.ReadReplyAsync(ct);
                        }
                        catch
                        {
                            // the next hop is gone already
                        }
                    }

                    client.Close();

                }

            }
        }

        /// <summary>
        /// Whether an AUTH command with its initial response fits the 512 octets of a command line,
        /// CR LF included (RFC 5321 §4.5.3.1.4, RFC 4954 §4).
        /// </summary>
        internal static Boolean AuthLineFits(String Line)
            => Encoding.UTF8.GetByteCount(Line) + 2 <= 512;

        private async Task<String> AuthenticateAsync(SMTPConnection                       connection,
                                                     IReadOnlyDictionary<String, String>  extensions,
                                                     SmartHostRoute                       route,
                                                     CancellationToken                    ct)
        {

            // Check supported mechanisms
            if (!extensions.TryGetValue("AUTH", out var authParameters))
            {
                return "504 AUTH not supported";
            }

            var mechanisms = authParameters.Split(' ', StringSplitOptions.RemoveEmptyEntries).
                                            Select(mechanism => mechanism.ToUpperInvariant()).
                                            ToHashSet();

            // Prefer PLAIN for simplicity (already over TLS)
            if (mechanisms.Contains("PLAIN"))
            {

                var authString = $"\0{route.Username}\0{route.Password}";
                var authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));

                // RFC 4954 §4: "If use of the initial response argument would cause the AUTH command
                // to exceed this length, the client MUST NOT use the initial response parameter" -
                // RFC 5321 §4.5.3.1.4: 512 octets, CR LF included. The response then follows the
                // empty challenge (RFC 4422 §5.1).
                if (AuthLineFits($"AUTH PLAIN {authBase64}"))
                {
                    await connection.WriteLineAsync($"AUTH PLAIN {authBase64}", ct);
                    return (await connection.ReadReplyAsync(ct)).ToString();
                }

                await connection.WriteLineAsync("AUTH PLAIN", ct);
                var challenge = await connection.ReadReplyAsync(ct);
                if (!challenge.IsPositiveIntermediate)
                    return challenge.ToString();

                await connection.WriteLineAsync(authBase64, ct);
                return (await connection.ReadReplyAsync(ct)).ToString();

            }

            // Fallback to LOGIN
            if (mechanisms.Contains("LOGIN"))
            {
                await connection.WriteLineAsync("AUTH LOGIN", ct);
                var response = await connection.ReadReplyAsync(ct);
                if (!response.IsPositiveIntermediate)
                    return response.ToString();

                await connection.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(route.Username!)), ct);
                response = await connection.ReadReplyAsync(ct);
                if (!response.IsPositiveIntermediate)
                    return response.ToString();

                await connection.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(route.Password!)), ct);
                return (await connection.ReadReplyAsync(ct)).ToString();
            }

            return "504 No supported AUTH mechanism";
        }

        /// <summary>
        /// The lines of a message given as text: CR LF, a bare CR and a bare LF each end one.
        /// </summary>
        private static IEnumerable<String> DataLines(String content)
        {

            using var contentReader = new StringReader(content);
            String? line;

            while ((line = contentReader.ReadLine()) is not null)
                yield return line;

        }

        /// <summary>
        /// The message as it goes after 354: line by line (CR LF, a bare CR and a bare LF each end
        /// one), dot-stuffed (RFC 5321 §4.5.2), every line with CR LF, and the terminating ".".
        /// </summary>
        private static Byte[] DataOctets(String content)
        {

            var data = new StringBuilder();

            foreach (var line in DataLines(content))
            {
                // Dot-stuffing: lines starting with "." get an extra "."
                if (line.StartsWith('.'))
                    data.Append('.');
                data.Append(line).Append("\r\n");
            }

            data.Append(".\r\n");

            return Encoding.UTF8.GetBytes(data.ToString());

        }

        #region REQUIRETLS (RFC 8689)

        /// <summary>
        /// A null reverse-path - the sender of a bounce (RFC 5321 §4.5.5).
        /// </summary>
        private static Boolean IsNullReversePath(String? reversePath)
            => String.IsNullOrEmpty(reversePath) || reversePath == "<>";

        /// <summary>
        /// Whether the header of a message has "TLS-Required: No" (RFC 8689 §3): one such field -
        /// "More than one instance of the TLS-Required header field MUST NOT appear in a given
        /// message" - whose value, folding white space aside, is "No".
        /// </summary>
        /// <param name="message">The message, or its header.</param>
        internal static Boolean TlsRequiredNo(String message)
        {

            var       found   = 0;
            var       no      = false;
            String?   field   = null;

            void Take(String? f)
            {
                if (f is not null && f.StartsWith("TLS-Required:", StringComparison.OrdinalIgnoreCase))
                {
                    found++;
                    no = f["TLS-Required:".Length..].Trim().Equals("No", StringComparison.OrdinalIgnoreCase);
                }
            }

            using var reader = new StringReader(message);

            for (var line = reader.ReadLine(); line is not null && line.Length > 0; line = reader.ReadLine())
            {

                // RFC 5322 §2.2.3: a line that begins with white space continues the field before.
                if (line[0] is ' ' or '\t' && field is not null)
                    field += line;

                else
                {
                    Take(field);
                    field = line;
                }

            }

            Take(field);

            return found == 1 && no;

        }

        #endregion

        #region MX Resolution

        /// <summary>
        /// How the MX lookup of a domain ended (RFC 5321 §5.1).
        /// </summary>
        private enum MxLookup
        {
            Answered,
            NonExistentDomain,
            TemporaryError
        }

        /// <summary>
        /// The MX records of a domain - none for "an empty list of MXs" - or that the domain does
        /// not exist (NXDOMAIN), or that the lookup failed for now: another RCODE, a timeout, an
        /// exception.
        /// </summary>
        private async Task<(MxLookup Lookup, IReadOnlyList<MxRecord> Records, DNSInfo? Response)> ResolveMxAsync(String domain, CancellationToken ct)
        {

            DNSInfo response;

            try
            {
                response = await _dnsClient.Query(
                                     DomainName.Parse(domain),
                                     [ DNSResourceRecordTypes.MX ],
                                     CancellationToken: ct
                                 );
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"MX lookup failed for {domain}: {ex.Message}");
                return (MxLookup.TemporaryError, [], null);
            }

            if (response.IsTimeout)
            {
                _logger.Log(LogLevel.Warning, $"MX lookup for {domain} timed out");
                return (MxLookup.TemporaryError, [], null);
            }

            switch (response.ResponseCode)
            {

                case DNSResponseCodes.NoError:
                    return (MxLookup.Answered,
                            [.. response.Answers.
                                    OfType<MX>().
                                    Select(mx => new MxRecord(mx.Exchange.FullName.TrimEnd('.'), mx.Preference))],
                            response);

                case DNSResponseCodes.NameError:
                    _logger.Log(LogLevel.Warning, $"{domain} does not exist (NXDOMAIN)");
                    return (MxLookup.NonExistentDomain, [], null);

                default:
                    _logger.Log(LogLevel.Warning, $"MX lookup failed for {domain}: {response.ResponseCode}");
                    return (MxLookup.TemporaryError, [], null);

            }

        }

        /// <summary>
        /// Whether the name servers of a host say it has no address: its A and AAAA lookups both
        /// answered, with no record or NXDOMAIN. A lookup that failed says nothing.
        /// </summary>
        private async Task<Boolean> HasNoAddressAsync(String host, CancellationToken ct)
        {

            if (!DomainName.TryParse(host.TrimEnd('.'), out var name, out _))
                return true;

            foreach (var type in new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA })
            {

                try
                {

                    var response = await _dnsClient.Query(name, [ type ], CancellationToken: ct);

                    if (response.IsTimeout ||
                        response.ResponseCode is not (DNSResponseCodes.NoError or DNSResponseCodes.NameError) ||
                        response.Answers.Any(answer => answer is A or AAAA))
                        return false;

                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    return false;
                }

            }

            return true;

        }

        /// <summary>
        /// Whether a host name is this relay's own: the name it greets with.
        /// </summary>
        private Boolean IsOwnName(String host)
            => Idna.SameDomain(host, _config.LocalHostname);

        /// <summary>
        /// The name the relay gives itself in EHLO and HELO. RFC 6531 §3.7.1: "the SMTPUTF8-aware
        /// SMTP client MUST send only ASCII (LDH label or A-label [RFC5890]) domains in the EHLO
        /// command" - a relay named in U-labels says its A-labels.
        /// </summary>
        private String EhloName
            => Idna.ToAscii(_config.LocalHostname);

        #endregion

        #region Response Parsing

        /// <summary>
        /// The extensions an EHLO reply offers: keyword in upper case, and its parameters. A keyword
        /// is the first word of a line after the first, which greets (RFC 5321 §4.1.1.1) - "DSN" in
        /// the server's name, or in another extension's parameters, offers nothing. The obsolete
        /// "AUTH=LOGIN PLAIN" form counts as AUTH.
        /// </summary>
        internal static IReadOnlyDictionary<String, String> Extensions(SMTPReply EhloReply)
        {

            var extensions = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in EhloReply.Lines.Skip(1))
            {

                var text     = (line.Length > 4 ? line[4..] : "").Trim();
                var space    = text.IndexOf(' ');
                var keyword  = (space < 0 ? text : text[..space]).ToUpperInvariant();
                var values   = space < 0 ? "" : text[(space + 1)..].Trim();

                if (keyword.StartsWith("AUTH="))
                {
                    values  = (keyword[5..] + " " + values).Trim();
                    keyword = "AUTH";
                }

                if (keyword.Length > 0)
                    extensions[keyword] = extensions.TryGetValue(keyword, out var earlier) && earlier.Length > 0
                                              ? (earlier + " " + values).Trim()
                                              : values;

            }

            return extensions;

        }

        /// <summary>
        /// Whether the message needs SMTPUTF8 (RFC 6531): a non-ASCII envelope address, or a
        /// non-ASCII header field - the header section of an internationalized message is UTF-8.
        /// </summary>
        private static Boolean NeedsSmtpUtf8(String envelopeFrom, IEnumerable<String> recipients, String messageContent)
        {

            static Boolean NonAscii(String text)
                => text.Any(c => c > '\x7F');

            if (NonAscii(envelopeFrom) || recipients.Any(NonAscii))
                return true;

            var headerEnd = messageContent.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
                headerEnd = messageContent.IndexOf("\n\n", StringComparison.Ordinal);

            return NonAscii(headerEnd < 0 ? messageContent : messageContent[..headerEnd]);

        }

        /// <summary>
        /// An MX host whose addresses are none of the internet's.
        /// </summary>
        private sealed class NoPublicAddressException(String Message) : Exception(Message);

        /// <summary>
        /// Whether a host is this machine: localhost, or a loopback address.
        /// </summary>
        private static Boolean IsLoopback(String? Host)

            => Host is not null &&
               (Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                System.Net.IPAddress.TryParse(Host.Trim('[', ']'), out var address) && System.Net.IPAddress.IsLoopback(address));

        /// <summary>
        /// The result of a reply that ends the transaction before the end of data: a 4yz or 5yz
        /// as it says, anything else - a reply of the wrong kind - a failure for now. Never a
        /// delivery: no message was sent. The reply goes with it as it was, every line of it.
        /// </summary>
        private static SendResult Refused(SMTPReply Reply, String? mx)
            => Reply.Code is >= 500 and < 600
                   ? SendResult.PermFail(Reply.Code, Reply.Transcript, mx)
                   : SendResult.TempFail(Reply.Code, Reply.Transcript, mx);

        private static SendResult ParseResponse(String response, String? mx)
        {

            if (String.IsNullOrEmpty(response))
                return SendResult.TempFail(0, "Empty response", mx);

            if (!int.TryParse(response.AsSpan(0, Math.Min(3, response.Length)), out var code))
                return SendResult.TempFail(0, response, mx);

            return code switch {
                >= 200 and < 300 => SendResult.Success (      response, mx ?? "", TimeSpan.Zero),
                >= 400 and < 500 => SendResult.TempFail(code, response, mx),
                >= 500           => SendResult.PermFail(code, response, mx),
                _                => SendResult.TempFail(code, response, mx)
            };

        }

        #endregion

        #region Certificate Validation

        /// <summary>
        /// DANE (RFC 7672) certificate check: accept the server certificate iff it matches at
        /// least one DNSSEC-validated TLSA record. PKIX chain/name errors are irrelevant here —
        /// the TLSA record is the sole authenticator.
        /// </summary>
        private Boolean ValidateDaneCertificate(String                       mxHost,
                                                IReadOnlyList<TLSA>          tlsaRecords,
                                                IReadOnlyCollection<String>  referenceIdentifiers,
                                                X509Certificate?             certificate,
                                                X509Chain?                   chain)
        {

            if (certificate is null)
            {
                _logger.Log(LogLevel.Error, $"DANE: {mxHost} presented no certificate; refusing delivery");
                return false;
            }

            var leaf = certificate as X509Certificate2
                           ?? X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());

            var matched = DaneAuthenticator.Matches(tlsaRecords, leaf, chain, referenceIdentifiers, _logger);

            if (!matched)
                _logger.Log(LogLevel.Error,
                    $"DANE: server certificate for {mxHost} matched none of the {tlsaRecords.Count} TLSA record(s); refusing delivery");

            return matched;

        }

        private Boolean ValidateServerCertificate(String            mxHost,
                                                  Boolean           strict,
                                                  X509Certificate?  certificate,
                                                  X509Chain?        chain,
                                                  SslPolicyErrors   sslPolicyErrors)
        {

            // A validator of the operator's own - a private CA, a pinned certificate - decides alone.
            if (_config.RemoteCertificateValidator is not null)
                return _config.RemoteCertificateValidator(mxHost, certificate as X509Certificate2, chain, sslPolicyErrors);

            // Fully valid: chains to a trusted root, matches the MX host, and is present.
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            if (strict)
            {
                // Enforced TLS: the certificate MUST be PKIX-valid and match the MX host name
                // (RFC 8461 §4.1 / RFC 8689). Reject so delivery is deferred, not downgraded.
                _logger.Log(LogLevel.Error,
                    $"TLS certificate for {mxHost} rejected under enforced TLS: {sslPolicyErrors}");
                return false;
            }

            // Opportunistic TLS (RFC 7435): encryption is still better than cleartext, so accept
            // the certificate but record the problem for visibility.
            _logger.Log(LogLevel.Warning,
                $"TLS certificate issue for {mxHost} (opportunistic, accepting): {sslPolicyErrors}");
            return true;

        }

        /// <summary>
        /// What is wrong with a certificate, as a result type of RFC 8460 §4.3.1, or null when
        /// nothing is: "certificate-expired", "certificate-host-mismatch" ("the MX hostname does
        /// not match any identities listed in the subject alternative name"), or
        /// "certificate-not-trusted" (untrusted CAs, "certificate chain errors, etc.").
        /// </summary>
        private static String? CertificateFailure(SslPolicyErrors  sslPolicyErrors,
                                                  X509Chain?       chain)
        {

            if (sslPolicyErrors == SslPolicyErrors.None)
                return null;

            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) &&
                chain is not null &&
                chain.ChainStatus.Any(status => status.Status.HasFlag(X509ChainStatusFlags.NotTimeValid)))
                return "certificate-expired";

            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                return "certificate-host-mismatch";

            return "certificate-not-trusted";

        }

        #endregion

    }

}
