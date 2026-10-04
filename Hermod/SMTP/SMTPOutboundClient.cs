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
        private readonly MtaStsResolver        _mtaStsResolver;
        private readonly DaneResolver?         _daneResolver;
        private readonly Action<TlsRptEvent>?  _tlsRptRecorder;
        private readonly IDNSClient            _dnsClient;
        private readonly ILogger               _logger;

        public SMTPOutboundClient(SmtpOutboundConfig    config,
                                  DkimSigner?           _dkimSigner,
                                  IDNSClient            dnsClient,
                                  ILogger               logger,
                                  Action<TlsRptEvent>?  tlsRptRecorder   = null)
        {

            this._config          = config;
            this.__dkimSigner     = _dkimSigner;
            this._dnsClient       = dnsClient;
            this._logger          = logger;
            this._mtaStsResolver  = new MtaStsResolver(dnsClient, logger);
            this._daneResolver    = config.EnableDane
                                        ? new DaneResolver(dnsClient,
                                                           logger,
                                                           config.DnssecTrustAnchors is { } anchors
                                                               ? new DNSSECValidator(dnsClient, anchors)
                                                               : null)
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
                                                  IReadOnlyCollection<RecipientDsn>? recipientDsns = null)
        {

            var startTime = Timestamp.Now;

            try
            {

                // Sign message with DKIM if signer is configured
                if (__dkimSigner is not null)
                {
                    messageContent = __dkimSigner.SignMessage(messageContent);
                }

                // Check MTA-STS policy
                var mtaStsPolicy  = await _mtaStsResolver.GetPolicyAsync(targetDomain, ct);
                var enforceTls    = requireTls ||
                                    mtaStsPolicy.Mode == MtaStsMode.Enforce ||
                                    _config.RequireStartTls;

                if (mtaStsPolicy.Mode != MtaStsMode.None)
                {
                    _logger.Log(LogLevel.Info, $"MTA-STS policy for {targetDomain}: {mtaStsPolicy.Mode}");
                }

                // Determine target hosts
                IReadOnlyList<MxRecord> mxHosts;

                if (_config.SmartHost is not null)
                {
                    // Use smarthost relay
                    mxHosts = [new MxRecord(_config.SmartHost, 0)];
                    _logger.Log(LogLevel.Debug, $"Using smarthost: {_config.SmartHost}");
                }
                else
                {

                    // MX lookup
                    mxHosts = await ResolveMxAsync(targetDomain, ct);

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

                    if (mxHosts.Count == 0)
                    {
                        // Fallback to A/AAAA record
                        mxHosts = [new MxRecord(targetDomain, 0)];
                    }

                    _logger.Log(LogLevel.Debug, $"MX records for {targetDomain}: {String.Join(", ", mxHosts.Select(m => $"{m.Host}:{m.Priority}"))}");

                    // If MTA-STS is in enforce mode, filter MX hosts to match policy
                    if (mtaStsPolicy.Mode == MtaStsMode.Enforce && mtaStsPolicy.MxPatterns.Count > 0)
                    {
                        var filteredHosts = mxHosts.Where(mx => mtaStsPolicy.MatchesMx(mx.Host)).ToList();
                        if (filteredHosts.Count == 0)
                        {
                            _logger.Log(LogLevel.Error, $"No MX hosts match MTA-STS policy for {targetDomain}");
                            return SendResult.PermFail(550, "MTA-STS policy violation: no matching MX hosts");
                        }
                        mxHosts = filteredHosts;
                    }

                }

                // Try each MX host in priority order
                Exception?  lastException  = null;
                SendResult? lastResult     = null;

                foreach (var mx in mxHosts.OrderBy(m => m.Priority))
                {
                    try
                    {
                        var result = await TrySendToMxAsync(
                                               mx.Host,
                                               _config.SmartHost is not null
                                                   ? _config.SmartHostPort
                                                   : (UInt16) 25,
                                               envelopeFrom,
                                               recipients,
                                               messageContent,
                                               enforceTls,
                                               requireTls,
                                               targetDomain,
                                               mtaStsPolicy.Mode,
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

                        // Permanent failure - don't try other MX hosts
                        if (result.Status == SendStatus.PermFail)
                        {
                            return result;
                        }

                        // Temp failure - try next MX
                        lastResult = result;
                        _logger.Log(LogLevel.Warning, $"MX {mx.Host} temp failed: {result.ResponseCode} {result.ResponseText}");
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.Log(LogLevel.Warning, $"MX {mx.Host} connection failed: {ex.Message}");
                    }
                }

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

        private async Task<SendResult> TrySendToMxAsync(String              mxHost,
                                                        UInt16              port,
                                                        String              envelopeFrom,
                                                        String[]            recipients,
                                                        String              messageContent,
                                                        Boolean             enforceTls,
                                                        Boolean             requireTls,
                                                        String              policyDomain,
                                                        MtaStsMode          stsMode,
                                                        DsnParameters       dsn,
                                                        SByte               priority,
                                                        IReadOnlyCollection<RecipientDsn> recipientDsns,
                                                        CancellationToken   ct)
        {

            // DANE (RFC 7672): resolve DNSSEC-validated TLSA records for this MX host up front.
            // A "bogus" result means the destination advertises DANE but the records cannot be
            // trusted — fail closed and defer rather than risk an unauthenticated channel.
            var                  daneActive   = false;
            IReadOnlyList<TLSA>  daneRecords  = [];

            if (_daneResolver is not null)
            {

                var dane = await _daneResolver.ResolveTlsaAsync(mxHost, port, ct);

                if (dane.MustDefer)
                {
                    _logger.Log(LogLevel.Error,
                        $"DANE: TLSA records for {mxHost} failed DNSSEC validation ({dane.Detail}); deferring");
                    // TLS-RPT (RFC 8460 §4.3): a bogus DNSSEC result under DANE.
                    _tlsRptRecorder?.Invoke(new TlsRptEvent(policyDomain, TlsRptPolicyType.Tlsa, mxHost, null, null, false, "dnssec-invalid"));
                    return SendResult.TempFail(450, $"DANE TLSA validation failed for {mxHost}: {dane.Detail}", mxHost);
                }

                daneActive   = dane.IsUsable;
                daneRecords  = dane.Records;

                if (daneActive)
                    _logger.Log(LogLevel.Info,
                        $"DANE active for {mxHost}: {daneRecords.Count} usable TLSA record(s), TLS enforced");

            }

            // DANE mandates authenticated TLS to this MX (RFC 7672 §2.2).
            var mustEnforceTls = enforceTls || daneActive;

            using var client = new TcpClient();

            // Connect with timeout
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter((Int32) _config.ConnectTimeoutMs);

            _logger.Log(LogLevel.Debug, $"Connecting to {mxHost}:{port}");
            await client.ConnectAsync(mxHost, port, connectCts.Token);

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
                _tlsRptRecorder(new TlsRptEvent(policyDomain, policyType, mxHost, receivingIp, sendingIp, success, failureType));
            }

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
                if (greeting.Code != 220)
                    return ParseResponse(greeting.ToString(), mxHost);

                // EHLO
                await connection.WriteLineAsync($"EHLO {_config.LocalHostname}", ct);
                var ehloResponse = await connection.ReadReplyAsync(ct);

                // RFC 5321 §3.2: a server that does not know EHLO answers 500, 501, 502, 504 or 550,
                // and the client falls back to HELO. Anything else - a 421 that ends the session, a
                // 554 - is the server's answer to the attempt.
                if (ehloResponse.Code is 500 or 501 or 502 or 504 or 550)
                {

                    await connection.WriteLineAsync($"HELO {_config.LocalHostname}", ct);
                    ehloResponse = await connection.ReadReplyAsync(ct);

                }

                if (ehloResponse.Code != 250)
                    return ParseResponse(ehloResponse.ToString(), mxHost);

                var extensions = Extensions(ehloResponse);

                // STARTTLS if available and desired/required
                var supportsStartTls = extensions.ContainsKey("STARTTLS");

                var wantTls = mustEnforceTls || _config.RequireStartTls || _config.PreferStartTls;

                if (supportsStartTls && wantTls)
                {

                    await connection.WriteLineAsync("STARTTLS", ct);
                    var starttlsResponse = await connection.ReadReplyAsync(ct);

                    if (starttlsResponse.Code == 220)
                    {
                        // Under DANE the TLSA record authenticates the certificate directly — no
                        // PKIX path or name check (RFC 7672 §3.1). Otherwise validate strictly when
                        // TLS is enforced, else opportunistically (RFC 7435): encrypt but tolerate a bad cert.
                        var validateStrict = mustEnforceTls || _config.RequireValidCertificate;

                        var sslStream = new SslStream(connection.Stream, false,
                            (_, cert, chain, errors) => daneActive
                                ? ValidateDaneCertificate(mxHost, daneRecords, cert, chain)
                                : ValidateServerCertificate(mxHost, validateStrict, cert, chain, errors));

                        try
                        {
                            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                            {
                                TargetHost = mxHost,
                                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                            }, ct);
                        }
                        catch (AuthenticationException ex)
                        {
                            sessionOpen = false;     // half a handshake: neither TLS nor cleartext
                            // A rejected/mismatched certificate under enforced TLS (including a DANE
                            // TLSA mismatch) must not be bypassed: defer instead of downgrading.
                            var why = daneActive ? "DANE TLSA mismatch" : "certificate validation failed";
                            RecordTls(false, daneActive ? "validation-failure" : "certificate-not-trusted");
                            return SendResult.TempFail(454, $"TLS {why} for {mxHost}: {ex.Message}", mxHost);
                        }

                        // Whatever was read in cleartext behind the 220 is discarded (RFC 3207 §4.2).
                        connection.SwitchTo(sslStream);

                        _logger.Log(LogLevel.Debug,
                            $"TLS established with {mxHost}: {sslStream.SslProtocol}{(daneActive ? " (DANE-authenticated)" : "")}");

                        // TLS-RPT (RFC 8460): a compliant TLS session was established.
                        RecordTls(true, null);

                        // Re-send EHLO after TLS
                        await connection.WriteLineAsync($"EHLO {_config.LocalHostname}", ct);
                        ehloResponse = await connection.ReadReplyAsync(ct);
                        if (ehloResponse.Code != 250)
                            return ParseResponse(ehloResponse.ToString(), mxHost);

                        // RFC 3207 §4.2: what was offered before TLS counts no more.
                        extensions = Extensions(ehloResponse);
                    }
                    else if (mustEnforceTls || _config.RequireStartTls)
                    {
                        RecordTls(false, "starttls-not-supported");
                        return SendResult.TempFail(454, $"STARTTLS required but failed: {starttlsResponse}", mxHost);
                    }

                }
                else if (mustEnforceTls || _config.RequireStartTls)
                {
                    RecordTls(false, "starttls-not-supported");
                    return SendResult.TempFail(454,
                        daneActive
                            ? $"DANE requires STARTTLS but {mxHost} does not offer it"
                            : "STARTTLS required but not supported",
                        mxHost);
                }

                // AUTH if smarthost credentials provided
                if (_config.SmartHost is not null && _config.SmartHostUsername is not null)
                {
                    var authResult = await AuthenticateAsync(connection, extensions, ct);
                    if (!authResult.StartsWith("235"))
                    {
                        return ParseResponse(authResult, mxHost);
                    }
                }

                // DSN (RFC 3461) / MT-PRIORITY (RFC 6710): only emit params the remote advertised.
                var supportsDsn         = extensions.ContainsKey("DSN");
                var supportsMtPriority  = extensions.ContainsKey(MtPriority.Keyword);

                // What the message needs of the next hop, and whether it offers it. A message it
                // cannot take is not handed over in a form it did not ask for: no 7-bit conversion,
                // which would break DKIM and OpenPGP signatures over the content.
                var needsSmtpUtf8  = NeedsSmtpUtf8(envelopeFrom, recipients, messageContent);
                var has8Bit        = messageContent.Any(c => c > '\x7F');

                // RFC 6531 §3.2: a message that needs SMTPUTF8 goes only to a server that offers it.
                if (needsSmtpUtf8 && !extensions.ContainsKey("SMTPUTF8"))
                    return SendResult.PermFail(553, $"5.6.7 {mxHost} does not offer SMTPUTF8, which the message needs", mxHost);

                // RFC 5321 §2.4, RFC 6152 §3: no 8-bit content to a server without 8BITMIME.
                if (has8Bit && !extensions.ContainsKey("8BITMIME"))
                    return SendResult.PermFail(554, $"5.6.3 {mxHost} does not offer 8BITMIME, and the message has 8-bit content", mxHost);

                // RFC 8689 §4.2.1: a REQUIRETLS message goes only to a next hop that offers REQUIRETLS
                // (which it does inside TLS only), and carries the option on.
                if (requireTls && !extensions.ContainsKey("REQUIRETLS"))
                    return SendResult.PermFail(550, $"5.7.30 REQUIRETLS support required, and {mxHost} does not offer it", mxHost);

                // MAIL FROM (with RET/ENVID and MT-PRIORITY when requested and supported)
                var mailFromCommand = DsnCommands.MailFrom(envelopeFrom, dsn, supportsDsn);
                mailFromCommand     = MtPriority.AppendMailFromParam(mailFromCommand, priority, supportsMtPriority);

                if (has8Bit)
                    mailFromCommand += " BODY=8BITMIME";        // RFC 6152 §3

                if (needsSmtpUtf8)
                    mailFromCommand += " SMTPUTF8";             // RFC 6531 §3.4

                if (requireTls)
                    mailFromCommand += " REQUIRETLS";           // RFC 8689 §4.2.1
                await connection.WriteLineAsync(mailFromCommand, ct);
                var mailResponse = await connection.ReadReplyAsync(ct);
                if (mailResponse.Code != 250)
                {
                    return ParseResponse(mailResponse.ToString(), mxHost);
                }

                // RCPT TO for each recipient (with NOTIFY/ORCPT when requested and supported)
                var outcomes = new List<RecipientOutcome>();
                foreach (var recipient in recipients)
                {

                    // A relayed recipient carries the NOTIFY/ORCPT it was received with (RFC 3461
                    // §5.2.1); a message of our own applies the message-wide request to every one.
                    var recipientDsn = recipientDsns.FirstOrDefault(r => r.Recipient == recipient);

                    await connection.WriteLineAsync(recipientDsn is not null
                                                        ? $"RCPT TO:<{recipient}>" + DsnCommands.RcptToParams(recipientDsn, supportsDsn)
                                                        : DsnCommands.RcptTo(recipient, dsn, supportsDsn),
                                                    ct);
                    var rcptResponse = await connection.ReadReplyAsync(ct);

                    outcomes.Add(new RecipientOutcome(recipient, rcptResponse.Code, rcptResponse.Text));

                    if (rcptResponse.Code is not (250 or 251))
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

                // DATA
                await connection.WriteLineAsync("DATA", ct);
                var dataResponse = await connection.ReadReplyAsync(ct);
                if (dataResponse.Code != 354)
                    return ParseResponse(dataResponse.ToString(), mxHost);

                // Send message content with dot-stuffing, and end with <CRLF>.<CRLF>
                await connection.WriteAsync(DataOctets(messageContent), ct);
                var finalResponse = await connection.ReadReplyAsync(ct);

                // The message was taken for the accepted recipients; what became of the others goes
                // with the result, for the queue to bounce or retry them.
                var delivery = ParseResponse(finalResponse.ToString(), mxHost) with { RemoteSupportsDsn = supportsDsn };

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

        private async Task<String> AuthenticateAsync(SMTPConnection                       connection,
                                                     IReadOnlyDictionary<String, String>  extensions,
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

                var authString = $"\0{_config.SmartHostUsername}\0{_config.SmartHostPassword}";
                var authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));

                await connection.WriteLineAsync($"AUTH PLAIN {authBase64}", ct);
                return (await connection.ReadReplyAsync(ct)).ToString();

            }

            // Fallback to LOGIN
            if (mechanisms.Contains("LOGIN"))
            {
                await connection.WriteLineAsync("AUTH LOGIN", ct);
                var response = await connection.ReadReplyAsync(ct);
                if (response.Code != 334)
                    return response.ToString();

                await connection.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(_config.SmartHostUsername!)), ct);
                response = await connection.ReadReplyAsync(ct);
                if (response.Code != 334)
                    return response.ToString();

                await connection.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(_config.SmartHostPassword!)), ct);
                return (await connection.ReadReplyAsync(ct)).ToString();
            }

            return "504 No supported AUTH mechanism";
        }

        /// <summary>
        /// The message as it goes after 354: line by line (CR LF, a bare CR and a bare LF each end
        /// one), dot-stuffed (RFC 5321 §4.5.2), every line with CR LF, and the terminating ".".
        /// </summary>
        private static Byte[] DataOctets(String content)
        {

            var data = new StringBuilder();

            using var contentReader = new StringReader(content);
            String? line;

            while ((line = contentReader.ReadLine()) is not null)
            {
                // Dot-stuffing: lines starting with "." get an extra "."
                if (line.StartsWith('.'))
                    data.Append('.');
                data.Append(line).Append("\r\n");
            }

            data.Append(".\r\n");

            return Encoding.UTF8.GetBytes(data.ToString());

        }

        #region MX Resolution

        private async Task<IReadOnlyList<MxRecord>> ResolveMxAsync(String domain, CancellationToken ct)
        {
            try
            {
                var response = await _dnsClient.Query(
                                         DomainName.Parse(domain),
                                         [ DNSResourceRecordTypes.MX ],
                                         CancellationToken: ct
                                     );

                return response.Answers.
                           OfType<MX>().
                           Select(mx => new MxRecord(mx.Exchange.FullName.TrimEnd('.'), mx.Preference)).
                           ToList();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"MX lookup failed for {domain}: {ex.Message}");
                return [];
            }
        }

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
        private Boolean ValidateDaneCertificate(String               mxHost,
                                                IReadOnlyList<TLSA>  tlsaRecords,
                                                X509Certificate?     certificate,
                                                X509Chain?           chain)
        {

            if (certificate is null)
            {
                _logger.Log(LogLevel.Error, $"DANE: {mxHost} presented no certificate; refusing delivery");
                return false;
            }

            var leaf = certificate as X509Certificate2
                           ?? X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());

            var matched = DaneAuthenticator.Matches(tlsaRecords, leaf, chain, _logger);

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

        #endregion

    }

}
