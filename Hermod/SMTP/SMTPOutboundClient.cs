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

using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    #region Send Result

    public enum SendStatus
    {
        Success,
        TempFail,   // 4xx - retry later
        PermFail    // 5xx - permanent failure, don't retry
    }

    public sealed record SendResult(SendStatus  Status,
                                    Int32       ResponseCode,
                                    String      ResponseText,
                                    String?     RemoteMx = null,
                                    TimeSpan?   Duration = null)
    {
        public static SendResult Success(String response, String mx, TimeSpan duration) =>
            new (SendStatus.Success, 250, response, mx, duration);

        public static SendResult TempFail(Int32 code, String response, String? mx = null) =>
            new (SendStatus.TempFail, code, response, mx);

        public static SendResult PermFail(Int32 code, String response, String? mx = null) =>
            new (SendStatus.PermFail, code, response, mx);

        public static SendResult TempFail(String error) =>
            new (SendStatus.TempFail, 0, error);

        public static SendResult PermFail(String error) =>
            new (SendStatus.PermFail, 0, error);

    }

    #endregion

    #region MX Record

    public sealed record MxRecord(String Host, int Priority);

    #endregion

    #region Outbound Client Configuration

    public sealed record SmtpOutboundConfig
    {
        public required String   LocalHostname        { get; init; }
        public          UInt32   ConnectTimeoutMs     { get; init; } = 30_000;
        public          UInt32   ReadTimeoutMs        { get; init; } = 60_000;
        public          UInt32   WriteTimeoutMs       { get; init; } = 60_000;
        public          Boolean  RequireStartTls      { get; init; } = false;
        public          Boolean  PreferStartTls       { get; init; } = true;

        /// <summary>
        /// Require a valid server certificate for EVERY TLS delivery (not just enforced ones).
        /// Default false = opportunistic TLS (RFC 7435): encrypt even with a bad certificate.
        /// Certificates are always validated strictly when TLS is enforced (MTA-STS enforce,
        /// REQUIRETLS, or RequireStartTls), regardless of this flag.
        /// </summary>
        public          Boolean  RequireValidCertificate { get; init; } = false;

        /// <summary>
        /// Enable DANE (RFC 7672): look up DNSSEC-validated TLSA records for the target MX and,
        /// when present, enforce STARTTLS and authenticate the server certificate against them.
        /// Requires a DNSSEC-aware resolver path (the DNS client's DO bit is enabled automatically).
        /// Default false.
        /// </summary>
        public          Boolean  EnableDane           { get; init; } = false;

        public          String?  SmartHost            { get; init; }  // Optional relay host
        public          UInt16   SmartHostPort        { get; init; } = 25;
        public          String?  SmartHostUsername    { get; init; }
        public          String?  SmartHostPassword    { get; init; }
    }

    #endregion

    #region SMTP Outbound Client

    public sealed class SMTPOutboundClient
    {

        private readonly SmtpOutboundConfig    _config;
        private readonly DkimSigner?           __dkimSigner;
        private readonly MtaStsResolver        _mtaStsResolver;
        private readonly DaneResolver?         _daneResolver;
        private readonly Action<TlsRptEvent>?  _tlsRptRecorder;
        private readonly DNSClient             _dnsClient;
        private readonly ILogger               _logger;

        public SMTPOutboundClient(SmtpOutboundConfig    config,
                                  DkimSigner?           _dkimSigner,
                                  DNSClient             dnsClient,
                                  ILogger               logger,
                                  Action<TlsRptEvent>?  tlsRptRecorder   = null)
        {

            this._config          = config;
            this.__dkimSigner     = _dkimSigner;
            this._dnsClient       = dnsClient;
            this._logger          = logger;
            this._mtaStsResolver  = new MtaStsResolver(dnsClient, logger);
            this._daneResolver    = config.EnableDane
                                        ? new DaneResolver(dnsClient, logger)
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
                                                  CancellationToken  ct           = default)
        {

            var startTime = DateTime.UtcNow;

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
                Exception? lastException = null;
                String? lastError = null;
                int lastCode = 0;

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
                                               targetDomain,
                                               mtaStsPolicy.Mode,
                                               ct
                                           );

                        if (result.Status == SendStatus.Success)
                        {
                            var duration = DateTime.UtcNow - startTime;
                            return result with { Duration = duration };
                        }

                        // Permanent failure - don't try other MX hosts
                        if (result.Status == SendStatus.PermFail)
                        {
                            return result;
                        }

                        // Temp failure - try next MX
                        lastError = result.ResponseText;
                        lastCode = result.ResponseCode;
                        _logger.Log(LogLevel.Warning, $"MX {mx.Host} temp failed: {result.ResponseCode} {result.ResponseText}");
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.Log(LogLevel.Warning, $"MX {mx.Host} connection failed: {ex.Message}");
                    }
                }

                // All MX hosts failed
                return lastError is not null
                    ? SendResult.TempFail(lastCode, lastError)
                    : SendResult.TempFail($"All MX hosts unreachable: {lastException?.Message}");
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
                                                        String              policyDomain,
                                                        MtaStsMode          stsMode,
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
            client.SendTimeout    = (Int32) _config.WriteTimeoutMs;
            client.ReceiveTimeout = (Int32) _config.ReadTimeoutMs;

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

            Stream stream = client.GetStream();
            // UTF-8 (ASCII-compatible) so SMTPUTF8/8BITMIME bodies relay intact; CRLF forced.
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };

            try
            {

                // Read greeting
                var greeting = await ReadResponseAsync(reader, ct);
                if (!greeting.StartsWith("220"))
                    return ParseResponse(greeting, mxHost);

                // EHLO
                await writer.WriteLineAsync($"EHLO {_config.LocalHostname}");
                var ehloResponse = await ReadMultilineResponseAsync(reader, ct);

                if (!ehloResponse.Code.StartsWith("250"))
                {

                    // Try HELO fallback
                    await writer.WriteLineAsync($"HELO {_config.LocalHostname}");
                    ehloResponse = await ReadMultilineResponseAsync(reader, ct);

                    if (!ehloResponse.Code.StartsWith("250"))
                        return ParseResponse($"{ehloResponse.Code} {ehloResponse.LastLine}", mxHost);

                }

                // STARTTLS if available and desired/required
                var supportsStartTls = ehloResponse.Lines.Any(l => 
                    l.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase));

                var wantTls = mustEnforceTls || _config.RequireStartTls || _config.PreferStartTls;

                if (supportsStartTls && wantTls)
                {

                    await writer.WriteLineAsync("STARTTLS");
                    var starttlsResponse = await ReadResponseAsync(reader, ct);

                    if (starttlsResponse.StartsWith("220"))
                    {
                        // Under DANE the TLSA record authenticates the certificate directly — no
                        // PKIX path or name check (RFC 7672 §3.1). Otherwise validate strictly when
                        // TLS is enforced, else opportunistically (RFC 7435): encrypt but tolerate a bad cert.
                        var validateStrict = mustEnforceTls || _config.RequireValidCertificate;

                        var sslStream = new SslStream(stream, false,
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
                            // A rejected/mismatched certificate under enforced TLS (including a DANE
                            // TLSA mismatch) must not be bypassed: defer instead of downgrading.
                            var why = daneActive ? "DANE TLSA mismatch" : "certificate validation failed";
                            RecordTls(false, daneActive ? "validation-failure" : "certificate-not-trusted");
                            return SendResult.TempFail(454, $"TLS {why} for {mxHost}: {ex.Message}", mxHost);
                        }

                        stream = sslStream;
                        reader = new StreamReader(sslStream, Encoding.UTF8);
                        writer = new StreamWriter(sslStream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };

                        _logger.Log(LogLevel.Debug,
                            $"TLS established with {mxHost}: {sslStream.SslProtocol}{(daneActive ? " (DANE-authenticated)" : "")}");

                        // TLS-RPT (RFC 8460): a compliant TLS session was established.
                        RecordTls(true, null);

                        // Re-send EHLO after TLS
                        await writer.WriteLineAsync($"EHLO {_config.LocalHostname}");
                        ehloResponse = await ReadMultilineResponseAsync(reader, ct);
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
                    var authResult = await AuthenticateAsync(reader, writer, ehloResponse, ct);
                    if (!authResult.StartsWith("235"))
                    {
                        return ParseResponse(authResult, mxHost);
                    }
                }

                // MAIL FROM
                await writer.WriteLineAsync($"MAIL FROM:<{envelopeFrom}>");
                var mailResponse = await ReadResponseAsync(reader, ct);
                if (!mailResponse.StartsWith("250"))
                {
                    return ParseResponse(mailResponse, mxHost);
                }

                // RCPT TO for each recipient
                var acceptedRecipients = new List<String>();
                foreach (var recipient in recipients)
                {

                    await writer.WriteLineAsync($"RCPT TO:<{recipient}>");
                    var rcptResponse = await ReadResponseAsync(reader, ct);

                    if (rcptResponse.StartsWith("250") || rcptResponse.StartsWith("251"))
                    {
                        acceptedRecipients.Add(recipient);
                    }
                    else
                    {
                        _logger.Log(LogLevel.Warning, $"Recipient {recipient} rejected: {rcptResponse}");
                        // Continue with other recipients
                    }

                }

                if (acceptedRecipients.Count == 0)
                    return SendResult.PermFail(550, "All recipients rejected", mxHost);

                // DATA
                await writer.WriteLineAsync("DATA");
                var dataResponse = await ReadResponseAsync(reader, ct);
                if (!dataResponse.StartsWith("354"))
                    return ParseResponse(dataResponse, mxHost);

                // Send message content with dot-stuffing
                await SendMessageDataAsync(writer, messageContent, ct);

                // End with <CRLF>.<CRLF>
                await writer.WriteLineAsync(".");
                var finalResponse = await ReadResponseAsync(reader, ct);

                // QUIT (best effort)
                try
                {
                    await writer.WriteLineAsync("QUIT");
                    await ReadResponseAsync(reader, ct);
                }
                catch
                {
                    // Ignore QUIT errors
                }

                return ParseResponse(finalResponse, mxHost);

            }
            finally
            {
                client.Close();
            }
        }

        private async Task<String> AuthenticateAsync(StreamReader       reader,
                                                     StreamWriter       writer,
                                                     MultilineResponse  ehloResponse,
                                                     CancellationToken  ct)
        {

            // Check supported mechanisms
            var authLine = ehloResponse.Lines.FirstOrDefault(l => 
                l.StartsWith("250", StringComparison.OrdinalIgnoreCase) &&
                l.Contains("AUTH", StringComparison.OrdinalIgnoreCase));

            if (authLine is null)
            {
                return "504 AUTH not supported";
            }

            // Prefer PLAIN for simplicity (already over TLS)
            if (authLine.Contains("PLAIN", StringComparison.OrdinalIgnoreCase))
            {

                var authString = $"\0{_config.SmartHostUsername}\0{_config.SmartHostPassword}";
                var authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));

                await writer.WriteLineAsync($"AUTH PLAIN {authBase64}");
                return await ReadResponseAsync(reader, ct);

            }

            // Fallback to LOGIN
            if (authLine.Contains("LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("AUTH LOGIN");
                var response = await ReadResponseAsync(reader, ct);
                if (!response.StartsWith("334"))
                    return response;

                await writer.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(_config.SmartHostUsername!)));
                response = await ReadResponseAsync(reader, ct);
                if (!response.StartsWith("334"))
                    return response;

                await writer.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(_config.SmartHostPassword!)));
                return await ReadResponseAsync(reader, ct);
            }

            return "504 No supported AUTH mechanism";
        }

        private static async Task SendMessageDataAsync(StreamWriter writer, String content, CancellationToken ct)
        {

            // Split into lines and apply dot-stuffing
            using var contentReader = new StringReader(content);
            String? line;

            while ((line = await contentReader.ReadLineAsync(ct)) is not null)
            {
                // Dot-stuffing: lines starting with "." get an extra "."
                if (line.StartsWith('.'))
                {
                    await writer.WriteAsync('.');
                }
                await writer.WriteLineAsync(line);
            }

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

        private sealed record MultilineResponse(String Code, List<String> Lines, String LastLine);

        private static async Task<String> ReadResponseAsync(StreamReader reader, CancellationToken ct)
        {
            var line = await reader.ReadLineAsync(ct);
            return line ?? "";
        }

        private static async Task<MultilineResponse> ReadMultilineResponseAsync(StreamReader reader, CancellationToken ct)
        {
            var lines = new List<String>();
            String? line;
            String code = "";

            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                lines.Add(line);
            
                if (line.Length >= 3)
                {
                    code = line[..3];
                
                    // Check if this is the last line (space after code, not hyphen)
                    if (line.Length == 3 || line[3] != '-')
                        break;
                }
            }

            return new MultilineResponse(code, lines, line ?? "");
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

    #endregion

}
