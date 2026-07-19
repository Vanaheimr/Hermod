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

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{


    public enum SMTPSessionState {
        Connected,
        Greeted,
        MailFrom,
        RcptTo,
        Data,
        Quit
    }

    public sealed class SMTPSession(TcpClient           client,
                                    SMTPServerConfig    config,
                                    IMailStorage        storage,
                                    DNSVerifier         dnsVerifier,
                                    X509Certificate2?   certificate,
                                    IUserStore          userStore,
                                    IMailQueue?         mailQueue,
                                    Boolean             isSubmissionPort,
                                    ConnectionTracker?  connectionTracker,
                                    RateLimitConfig     rateLimitConfig,
                                    ILogger             logger,
                                    Boolean             implicitTls        = false,
                                    DmarcReportService? dmarcReportService = null,
                                    TlsRptIngestor?     tlsRptIngestor     = null)
    {

        private Stream                         _stream         = client.GetStream();
        // Use Latin1 (ISO-8859-1) encoding: 1:1 byte-to-char mapping for 0-255
        // ASCII only handles 0-127, which breaks BDAT with binary data!
        private StreamReader                   _reader         = new(client.GetStream(), Encoding.Latin1);
        // NewLine must be CRLF regardless of host OS (SMTP requires <CRLF>).
        private StreamWriter                   _writer         = new(client.GetStream(), Encoding.Latin1) { AutoFlush = true, NewLine = "\r\n" };
        private SMTPSessionState               _state          = SMTPSessionState.Connected;
        private String?                        _mailFrom;
        private readonly List<String>          _rcptTo         = [];
        private readonly List<String>          _localRcptTo    = []; // Recipients on local domains
        private readonly List<String>          _remoteRcptTo   = []; // Recipients on remote domains (relay)
        private Boolean                        _tlsActive;
        private readonly System.Net.IPAddress  _clientIp       = ((IPEndPoint) client.Client.RemoteEndPoint!).Address;
        private String                         _heloHostname   = "";
        private Boolean                        _extendedSmtp;   // true after EHLO (ESMTP), false after HELO
        private readonly SmtpAuthManager       _authManager    = new (userStore, logger);
        private Boolean                        _inAuthExchange;
        private X509Certificate2?              _clientCertificate;

        // DSN support (RFC 3461)
        private string?                        _dsnEnvId;
        private DsnRet                         _dsnRet          = DsnRet.Full;
        private readonly List<RecipientDsn>    _recipientDsns   = [];

        // REQUIRETLS support (RFC 8689)
        private bool                           _requireTls;

        // BDAT/CHUNKING support (RFC 3030)
        private bool                           _inBdatSequence;
        private readonly MemoryStream          _bdatBuffer      = new();

        // Rate limiting
        private readonly SessionCounters       _counters        = new();



        public async Task HandleAsync(CancellationToken ct)
        {

            // Register connection for rate limiting
            connectionTracker?.RegisterConnection(_clientIp);

            try
            {
                // Implicit TLS (RFC 8314): the connection is TLS from the very first byte,
                // established before the greeting — there is no plaintext STARTTLS exchange.
                if (implicitTls)
                {
                    if (certificate is null)
                    {
                        logger.Log(LogLevel.Error, "Implicit-TLS connection but no certificate configured; closing");
                        return;
                    }

                    try
                    {
                        await EstablishTlsAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        logger.Log(LogLevel.Error, $"Implicit TLS handshake failed: {ex.Message}");
                        return;
                    }
                }

                await SendResponseAsync(220, $"{config.Hostname} ESMTP AchimSMTP ready");

                while (!ct.IsCancellationRequested && client.Connected)
                {
                    var line = await ReadLineAsync(ct);
                    if (line is null)
                        break;

                    // Command line-length limit (RFC 5321 §4.5.3.1.4)
                    if (line.Length + 2 > config.MaxCommandLineLength)
                    {
                        _counters.InvalidCommands++;
                        await SendResponseAsync(500, "5.5.6 Line too long");
                        if (_counters.InvalidCommands >= rateLimitConfig.MaxInvalidCommands)
                        {
                            await SendResponseAsync(421, "4.7.0 Too many errors, closing connection");
                            break;
                        }
                        continue;
                    }

                    logger.Log(LogLevel.Debug, $"C: {line}");

                    // Handle AUTH exchange specially
                    if (_inAuthExchange)
                    {
                        await ProcessAuthResponseAsync(line, ct);
                        continue;
                    }

                    var (command, args) = ParseCommand(line);
                    await ProcessCommandAsync(command, args, ct);

                    if (_state == SMTPSessionState.Quit)
                        break;

                    // Check for too many invalid commands
                    if (_counters.InvalidCommands >= rateLimitConfig.MaxInvalidCommands)
                    {
                        logger.Log(LogLevel.Warning, $"Too many invalid commands from {_clientIp}, disconnecting");
                        await SendResponseAsync(421, "4.7.0 Too many errors, closing connection");
                        break;
                    }

                }
            }
            catch (OperationCanceledException)
            {
                logger.Log(LogLevel.Debug, "Session cancelled");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, $"Session error: {ex.Message}");
            }
            finally
            {
                connectionTracker?.UnregisterConnection(_clientIp);
                _bdatBuffer.Dispose();
                client.Close();
            }
        }

        private async Task ProcessAuthResponseAsync(String response, CancellationToken ct)
        {
            // Handle AUTH cancellation
            if (response == "*")
            {
                _inAuthExchange = false;
                _authManager.Reset();
                await SendResponseAsync(501, "Authentication cancelled");
                return;
            }

            var result = await _authManager.ProcessResponseAsync(response, ct);
            await HandleAuthResultAsync(result);
        }

        private async Task ProcessCommandAsync(String command, String args, CancellationToken ct)
        {
            switch (command.ToUpperInvariant())
            {

                case "HELO":
                    await HandleHeloAsync(args);
                    break;

                case "EHLO":
                    await HandleEhloAsync(args);
                    break;

                case "STARTTLS":
                    await HandleStartTlsAsync(ct);
                    break;

                case "AUTH":
                    if (connectionTracker is not null && !connectionTracker.CanAttemptAuth(_clientIp))
                    {
                        await SendResponseAsync(421, "4.7.0 Too many authentication attempts, try again later");
                        return;
                    }
                    await HandleAuthAsync(args, ct);
                    break;

                case "MAIL":
                    await HandleMailFromAsync(args);
                    break;

                case "RCPT":
                    await HandleRcptToAsync(args);
                    break;

                case "DATA":
                    await HandleDataAsync(ct);
                    break;

                case "BDAT":
                    await HandleBdatAsync(args, ct);
                    break;

                case "RSET":
                    await HandleRsetAsync();
                    break;

                case "NOOP":
                    await SendResponseAsync(250, "OK");
                    break;

                case "QUIT":
                    await HandleQuitAsync();
                    break;

                case "VRFY":
                    await SendResponseAsync(252, "Cannot verify user");
                    break;

                default:
                    _counters.InvalidCommands++;
                    await SendResponseAsync(500, "Unrecognized command");
                    break;

            }
        }

        private async Task HandleHeloAsync(String hostname)
        {
            _heloHostname = hostname;
            _extendedSmtp = false;
            _state = SMTPSessionState.Greeted;
            await SendResponseAsync(250, $"Hello {hostname}, pleased to meet you");
        }

        private async Task HandleEhloAsync(String hostname)
        {

            _heloHostname   = hostname;
            _extendedSmtp   = true;
            _state          = SMTPSessionState.Greeted;

            var extensions  = new List<String> {
                $"{config.Hostname} Hello {hostname}",
                $"SIZE {config.MaxMessageSize}",
                "8BITMIME",
                "PIPELINING", // RFC 2920 - command pipelining
                "ENHANCEDSTATUSCODES",
                "CHUNKING",  // RFC 3030 - BDAT command
                "DSN",       // RFC 3461 - Delivery Status Notifications
                "SMTPUTF8"   // RFC 6531 - Internationalized Email
            };

            if (certificate is not null && !_tlsActive)
                extensions.Add("STARTTLS");

            // REQUIRETLS only available after STARTTLS (RFC 8689)
            if (_tlsActive)
                extensions.Add("REQUIRETLS");

            // Advertise AUTH mechanisms
            var authMechanisms = _authManager.GetAvailableMechanisms(_tlsActive).ToList();
            if (authMechanisms.Count > 0)
                extensions.Add($"AUTH {String.Join(' ', authMechanisms)}");

            for (var i = 0; i < extensions.Count - 1; i++)
                await SendResponseAsync(250, extensions[i], multiline: true);

            await SendResponseAsync(250, extensions[^1]);

        }

        private async Task HandleAuthAsync(String args, CancellationToken ct)
        {
            if (_state < SMTPSessionState.Greeted)
            {
                await SendResponseAsync(503, "Say HELO/EHLO first");
                return;
            }

            if (_authManager.IsAuthenticated)
            {
                await SendResponseAsync(503, "Already authenticated");
                return;
            }

            // Check if PLAIN/LOGIN requires TLS
            var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                await SendResponseAsync(501, "Syntax: AUTH mechanism [initial-response]");
                return;
            }

            var mechanism = parts[0].ToUpperInvariant();
        
            // PLAIN and LOGIN should require TLS (but allow SCRAM without)
            if (!_tlsActive && (mechanism == "PLAIN" || mechanism == "LOGIN"))
            {
                await SendResponseAsync(538, "5.7.11 Encryption required for requested authentication mechanism");
                return;
            }

            // EXTERNAL requires client certificate
            if (mechanism == "EXTERNAL" && _clientCertificate is null)
            {
                await SendResponseAsync(535, "5.7.8 Client certificate required for EXTERNAL authentication");
                return;
            }

            var startResult = _authManager.StartAuth(mechanism);
            if (startResult.Result == AuthResult.InvalidMechanism)
            {
                await SendResponseAsync(504, startResult.ErrorCode ?? "Unrecognized authentication type");
                return;
            }

            // Check for initial response (AUTH PLAIN <initial-response>)
            if (parts.Length > 1)
            {
                var result = await _authManager.ProcessResponseAsync(parts[1], ct);
                await HandleAuthResultAsync(result);
            }
            else
            {
                // Request initial response
                _inAuthExchange = true;
                await SendResponseAsync(334, startResult.Challenge ?? "");
            }
        }

        private async Task HandleAuthResultAsync(AuthResponse result)
        {
            switch (result.Result)
            {

                case AuthResult.Success:
                    _inAuthExchange = false;
                    var successMsg = result.Message is not null 
                        ? $"2.7.0 Authentication successful {result.Message}"
                        : "2.7.0 Authentication successful";
                    await SendResponseAsync(235, successMsg);
                    logger.Log(LogLevel.Info, $"Authenticated: {result.Username} via {_authManager.AuthenticationMethod}");
                    break;

                case AuthResult.Continue:
                    _inAuthExchange = true;
                    await SendResponseAsync(334, result.Challenge ?? "");
                    break;

                case AuthResult.Fail:
                    _inAuthExchange = false;
                    await SendResponseAsync(535, result.ErrorCode ?? "5.7.8 Authentication failed");
                    break;

            }
        }

        private async Task HandleStartTlsAsync(CancellationToken ct)
        {

            if (certificate is null)
            {
                await SendResponseAsync(454, "TLS not available");
                return;
            }

            if (_tlsActive)
            {
                await SendResponseAsync(503, "TLS already active");
                return;
            }

            await SendResponseAsync(220, "Ready to start TLS");

            try
            {
                await EstablishTlsAsync(ct);
                // RFC 3207 §4.2: the client must reset its protocol state after STARTTLS.
                _state = SMTPSessionState.Connected;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, $"TLS handshake failed: {ex.Message}");
                throw;
            }

        }

        /// <summary>
        /// Perform the server-side TLS handshake on the current stream and switch the reader/
        /// writer over to the encrypted stream. Shared by STARTTLS (RFC 3207) and implicit TLS
        /// (RFC 8314); the caller is responsible for any protocol handshake around it.
        /// </summary>
        private async Task EstablishTlsAsync(CancellationToken ct)
        {

            var sslStream = new SslStream(_stream, false, ValidateClientCertificate);

            await sslStream.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    ClientCertificateRequired = false,  // Optional client cert for EXTERNAL auth
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                                          System.Security.Authentication.SslProtocols.Tls13
                },
                ct
            );

            // Replace the reader/writer with fresh ones on the encrypted stream. This also
            // DISCARDS any bytes the old plaintext reader may have buffered past the STARTTLS
            // line — a client MUST NOT pipeline across STARTTLS (RFC 3207 §4.2), so any such
            // pre-TLS plaintext is treated as an injection attempt and dropped, never executed
            // as a post-TLS command (the plaintext-command-injection class, CVE-2011-0411).
            _stream = sslStream;
            _reader = new StreamReader(sslStream, Encoding.Latin1);
            _writer = new StreamWriter(sslStream, Encoding.Latin1) { AutoFlush = true, NewLine = "\r\n" };
            _tlsActive = true;

            // Capture client certificate for EXTERNAL auth
            if (sslStream.RemoteCertificate is X509Certificate remoteCert)
            {
                _clientCertificate = new X509Certificate2(remoteCert);
                _authManager.SetClientCertificate(_clientCertificate);
                logger.Log(LogLevel.Info, $"Client certificate: {_clientCertificate.Subject} (Thumbprint: {_clientCertificate.Thumbprint[..8]}...)");
            }

            logger.Log(LogLevel.Info, $"TLS established: {sslStream.SslProtocol}, {sslStream.NegotiatedCipherSuite}");

        }

        private Boolean ValidateClientCertificate(Object            sender,
                                                  X509Certificate?  certificate,
                                                  X509Chain?        chain,
                                                  SslPolicyErrors   sslPolicyErrors)
        {

            // Accept any client certificate (or none) - validation happens during AUTH EXTERNAL
            if (certificate is not null)
            {
                logger.Log(LogLevel.Debug, $"Client presented certificate: {certificate.Subject}");
            }

            return true;

        }

        private async Task HandleMailFromAsync(String args)
        {

            if (_state < SMTPSessionState.Greeted)
            {
                await SendResponseAsync(503, "Say HELO first");
                return;
            }

            if (config.RequireStartTls && !_tlsActive)
            {
                await SendResponseAsync(530, "5.7.0 Must issue STARTTLS first");
                return;
            }

            var match = Regex.Match(args, @"FROM:\s*<([^>]*)>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                await SendResponseAsync(501, "5.5.4 Syntax error in MAIL command");
                return;
            }

            _mailFrom = match.Groups[1].Value;

            // Get everything after the closing >
            var paramStart = args.IndexOf('>');
            var parameters = paramStart > 0 ? args[(paramStart + 1)..].Trim() : "";

            // Parse DSN parameters (RFC 3461)
            var (envId, ret) = DsnParser.ParseMailFromParams(parameters);
            _dsnEnvId = envId;
            _dsnRet   = ret;

            // Parse REQUIRETLS (RFC 8689)
            _requireTls = RequireTlsHandler.ParseRequireTls(parameters);
            if (_requireTls && !_tlsActive)
            {
                await SendResponseAsync(530, "5.7.0 REQUIRETLS requires active TLS connection");
                return;
            }

            // Reset state
            _rcptTo.Clear();
            _localRcptTo.Clear();
            _remoteRcptTo.Clear();
            _recipientDsns.Clear();
            _inBdatSequence = false;
            _bdatBuffer.SetLength(0);
            _state = SMTPSessionState.MailFrom;

            await SendResponseAsync(250, "2.1.0 OK");

        }

        private async Task HandleRcptToAsync(String args)
        {

            if (_state < SMTPSessionState.MailFrom)
            {
                await SendResponseAsync(503, "5.5.1 Need MAIL command first");
                return;
            }

            var match = Regex.Match(args, @"TO:\s*<([^>]+)>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                await SendResponseAsync(501, "5.5.4 Syntax error in RCPT command");
                return;
            }

            if (_rcptTo.Count >= config.MaxRecipients)
            {
                await SendResponseAsync(452, "4.5.3 Too many recipients");
                return;
            }

            var recipient       = match.Groups[1].Value;
            var recipientDomain = ExtractDomain(recipient);

            // Parse DSN parameters (RFC 3461)
            var paramStart = args.IndexOf('>');
            var parameters = paramStart > 0 ? args[(paramStart + 1)..].Trim() : "";
            var (notify, orcpt) = DsnParser.ParseRcptToParams(parameters);

            // Check if this is a local or remote recipient
            var isLocalRecipient = config.IsLocalDomain(recipientDomain);

            if (!isLocalRecipient)
            {
                // Remote recipient = relay request
                // Require authentication to prevent open relay
                if (config.RequireAuthForRelay && !_authManager.IsAuthenticated)
                {
                    logger.Log(LogLevel.Warning,
                        $"Relay denied for {recipient}: not authenticated (from {_clientIp})");
                    await SendResponseAsync(550, "5.7.1 Relay access denied. Authentication required.");
                    return;
                }

                _remoteRcptTo.Add(recipient);
                logger.Log(LogLevel.Debug, $"Remote recipient (relay): {recipient}");
            }
            else
            {
                _localRcptTo.Add(recipient);
                logger.Log(LogLevel.Debug, $"Local recipient: {recipient}");
            }

            // Submission port (587) requires auth for all mail per RFC 6409
            if (isSubmissionPort && config.RequireAuthOnSubmission && !_authManager.IsAuthenticated)
            {
                // Allow the RCPT but will check again at DATA
                logger.Log(LogLevel.Debug, $"Submission port recipient (auth check deferred): {recipient}");
            }

            // Store DSN info for this recipient
            _recipientDsns.Add(
                new RecipientDsn {
                    Recipient          = recipient,
                    Notify             = notify,
                    OriginalRecipient  = orcpt
                }
            );

            _rcptTo.Add(recipient);
            _state = SMTPSessionState.RcptTo;

            await SendResponseAsync(250, "2.1.5 OK");

        }

        private async Task HandleDataAsync(CancellationToken ct)
        {

            if (_state < SMTPSessionState.RcptTo || _rcptTo.Count == 0)
            {
                await SendResponseAsync(503, "5.5.1 Need RCPT command first");
                return;
            }

            // Submission port requires authentication per RFC 6409
            if (isSubmissionPort && config.RequireAuthOnSubmission && !_authManager.IsAuthenticated)
            {
                await SendResponseAsync(530, "5.7.0 Authentication required");
                return;
            }

            // Check message rate limit
            if (connectionTracker is not null && 
                !connectionTracker.CanSendMessage(_clientIp, _authManager.IsAuthenticated))
            {
                await SendResponseAsync(452, "4.7.1 Too many messages, try again later");
                return;
            }

            await SendResponseAsync(354, "Start mail input; end with <CRLF>.<CRLF>");

            var messageBuilder = new StringBuilder();
            var totalSize = 0;
            int?    errorCode    = null;
            String? errorMessage = null;

            while (!ct.IsCancellationRequested)
            {

                var line = await ReadLineAsync(ct);

                if (line is null)
                    break;              // connection lost

                if (line == ".")
                    break;              // end of data

                // Once an error is flagged, keep consuming until the terminator so the
                // connection stays in sync; reject only after the whole DATA block is read.
                if (errorCode is not null)
                    continue;

                // Text line-length limit (RFC 5321 §4.5.3.1.6), checked on the wire line.
                if (line.Length + 2 > config.MaxTextLineLength)
                {
                    errorCode    = 500;
                    errorMessage = "5.6.0 Line too long";
                    continue;
                }

                // Dot-unstuffing: remove one leading dot (RFC 5321 §4.5.2)
                if (line.StartsWith('.'))
                    line = line[1..];

                totalSize += line.Length + 2;
                if (totalSize > config.MaxMessageSize)
                {
                    errorCode    = 552;
                    errorMessage = "5.3.4 Message size exceeds maximum";
                    continue;
                }

                // Always terminate lines with CRLF, independent of the host OS.
                messageBuilder.Append(line).Append("\r\n");

            }

            if (errorCode is not null)
            {
                await SendResponseAsync(errorCode.Value, errorMessage!);
                ResetTransaction();
                return;
            }

            // The reader uses Latin1 (1 char == 1 wire byte). Reconstruct the original
            // bytes and decode them as UTF-8 so SMTPUTF8/8BITMIME content is preserved
            // (mirrors the BDAT path). ASCII content is unaffected.
            var rawMessage = Encoding.UTF8.GetString(
                                 Encoding.Latin1.GetBytes(messageBuilder.ToString())
                             );

            await ProcessReceivedMessageAsync(rawMessage, ct);

        }


        /// <summary>
        /// Handle BDAT command (RFC 3030 CHUNKING)
        /// BDAT allows sending message data in chunks without dot-stuffing
        /// </summary>
        private async Task HandleBdatAsync(String args, CancellationToken ct)
        {

            if (_state < SMTPSessionState.RcptTo || _rcptTo.Count == 0)
            {
                await SendResponseAsync(503, "5.5.1 Need RCPT command first");
                return;
            }

            // Parse BDAT arguments: BDAT <size> [LAST]
            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1 || !int.TryParse(parts[0], out var chunkSize))
            {
                await SendResponseAsync(501, "5.5.4 Syntax: BDAT size [LAST]");
                return;
            }

            var isLast = parts.Length > 1 && parts[1].Equals("LAST", StringComparison.OrdinalIgnoreCase);

            // Validate chunk size
            if (chunkSize < 0)
            {
                await SendResponseAsync(501, "5.5.4 Invalid chunk size");
                return;
            }

            if (_bdatBuffer.Length + chunkSize > config.MaxMessageSize)
            {
                await SendResponseAsync(552, "5.3.4 Message size exceeds maximum");
                ResetTransaction();
                return;
            }

            // Check message rate limit on first chunk
            if (!_inBdatSequence)
            {
                if (connectionTracker is not null && 
                    !connectionTracker.CanSendMessage(_clientIp, _authManager.IsAuthenticated))
                {
                    await SendResponseAsync(452, "4.7.1 Too many messages, try again later");
                    return;
                }
                _inBdatSequence = true;
            }


            // Read exact number of bytes
            // IMPORTANT: We must read from _reader (not _stream) because StreamReader buffers!
            // The StreamReader may have already read ahead and buffered the BDAT data.
            var charBuffer = new char[chunkSize];
            var charsRead = 0;
        
            while (charsRead < chunkSize)
            {
                var read = await _reader.ReadBlockAsync(charBuffer.AsMemory(charsRead, chunkSize - charsRead), ct);
                if (read == 0)
                {
                    await SendResponseAsync(451, "4.3.0 Connection lost during BDAT");
                    ResetTransaction();
                    return;
                }
                charsRead += read;
            }

            // Convert chars back to bytes using Latin1 (1:1 mapping)
            // The StreamReader uses Latin1, so each char == one byte
            var buffer = Encoding.Latin1.GetBytes(charBuffer, 0, chunkSize);

            // Append to buffer
            _bdatBuffer.Write(buffer, 0, buffer.Length);

            if (isLast)
            {
                // Process the complete message
                // Message content is typically UTF-8 encoded
                var rawMessage = Encoding.UTF8.GetString(_bdatBuffer.ToArray());
                _bdatBuffer.SetLength(0);
                _inBdatSequence = false;
            
                await ProcessReceivedMessageAsync(rawMessage, ct);
            }
            else
            {
                // More chunks expected
                await SendResponseAsync(250, $"2.0.0 {chunkSize} bytes received, continue");
            }
        }


        /// <summary>
        /// Process a received message (from DATA or BDAT)
        /// </summary>
        private async Task ProcessReceivedMessageAsync(String rawMessage, CancellationToken ct)
        {

            var message   = EMailMessage.Parse(rawMessage);

            // Perform DNS verification (for inbound mail from other servers)
            var senderDomain = ExtractDomain(_mailFrom ?? "");
            DnsVerificationResult? verification = null;
            var dmarcQuarantine = false;

            if (!String.IsNullOrEmpty(senderDomain) && !_authManager.IsAuthenticated)
            {
                // Only verify external mail (not from authenticated local users)
                verification = await dnsVerifier.VerifyAsync(
                    senderDomain,
                    _clientIp,
                    _mailFrom ?? "",
                    _heloHostname,
                    message,
                    ct
                );
                message.Verification = verification;

                LogVerificationResult(verification);

                // === DMARC REPORTING (RFC 7489 §7) ===
                // Record every DMARC-evaluated message (pass or fail) for aggregate reports;
                // fire a forensic report for failures. Done here so it runs on all paths,
                // including the reject/quarantine early-returns below. The disposition is
                // already baked into the evaluation by the verifier.
                if (verification.DmarcDetail is { } dmarcEval)
                {
                    dmarcReportService?.RecordInbound(dmarcEval, _clientIp);

                    if (dmarcEval.Failed && dmarcReportService is not null)
                    {
                        var headerBlock = ExtractHeaderBlock(rawMessage);
                        var authResults = $"{config.Hostname}; dmarc=fail header.from={dmarcEval.HeaderFromDomain}";
                        // Fire-and-forget: forensic sending does a DNS consent lookup we don't
                        // want to block the inbound DATA acknowledgement on.
                        _ = dmarcReportService.SendForensicAsync(dmarcEval, _clientIp, headerBlock, _mailFrom ?? "", authResults, ct);
                    }
                }

                // === SPF HARD-FAIL REJECT ===
                if (verification.Spf == SPFResult.Fail)
                {
                    logger.Log(LogLevel.Warning, $"SPF hard-fail for {_mailFrom} from {_clientIp}");
                    await SendResponseAsync(550, $"5.7.23 SPF validation failed: {senderDomain} does not authorize {_clientIp}");
                    ResetTransaction();
                    return;
                }

                // === DKIM ===
                // DKIM is advisory (RFC 6376 §6.1): a broken/absent signature is not by itself
                // a reason to reject. The result is recorded in Authentication-Results and left
                // to DMARC (and downstream filters). Only log it here.
                if (verification.Dkim == DkimResult.Fail)
                    logger.Log(LogLevel.Info, $"DKIM verification failed for message from {_mailFrom} (advisory, not rejecting)");

                // === DMARC POLICY ENFORCEMENT ===
                if (verification.Dmarc == DmarcResult.Fail)
                {

                    var dmarcPolicy = verification.DmarcPolicy?.ToLowerInvariant() ?? "none";

                    switch (dmarcPolicy)
                    {

                        case "reject":
                            logger.Log(LogLevel.Warning, $"DMARC reject policy for {senderDomain}");
                            await SendResponseAsync(550, $"5.7.1 DMARC policy violation: {senderDomain} has p=reject");
                            ResetTransaction();
                            return;

                        case "quarantine":
                            logger.Log(LogLevel.Warning, $"DMARC quarantine policy for {senderDomain} - marking as suspicious");
                            dmarcQuarantine = true;
                            break;

                        case "none":
                        default:
                            // Log but deliver
                            logger.Log(LogLevel.Info, $"DMARC failed but policy is {dmarcPolicy} for {senderDomain}");
                            break;

                    }

                }
            }

            // Record message for rate limiting
            connectionTracker?.RecordMessage(_clientIp);
            _counters.Messages++;

            // Prepend trace information (RFC 5321 §4.4). Done after verification so SPF/DKIM/DMARC
            // run on the original message; the stamped copy is what we store and relay.
            // Authentication-Results (RFC 8601) goes above the Received header of this hop.
            var traceHeaders = "";
            if (verification is not null)
                traceHeaders += BuildAuthenticationResultsHeader(verification, senderDomain, ExtractDomain(message.From ?? ""));
            traceHeaders += await BuildReceivedHeaderAsync(ct);
            if (dmarcQuarantine)
                traceHeaders += "X-DMARC-Quarantine: true\r\n";

            var stampedRaw     = traceHeaders + rawMessage;
            var stampedMessage = EMailMessage.Parse(stampedRaw);
            stampedMessage.Verification = verification;

            // Determine what to do with the message
            var hasLocalRecipients = _localRcptTo.Count > 0;
            var hasRemoteRecipients = _remoteRcptTo.Count > 0;

            logger.Log(LogLevel.Info,
                $"Message from {_mailFrom}: {_localRcptTo.Count} local, {_remoteRcptTo.Count} remote recipients");

            // Store locally for local recipients
            string? filePath = null;
            if (hasLocalRecipients)
            {
                filePath = await storage.StoreAsync(stampedMessage, _mailFrom ?? "<>", _localRcptTo, ct);
                logger.Log(LogLevel.Info, $"Stored locally: {Path.GetFileName(filePath)}");

                // TLS-RPT (RFC 8460) inbound: if this is a TLS report delivered to our rua
                // mailbox, decompress + parse it and record a summary (best-effort side effect).
                if (tlsRptIngestor is not null && TlsRptIngestor.IsTlsRptReport(stampedMessage))
                    tlsRptIngestor.Ingest(stampedMessage);
            }

            // Queue for outbound delivery for remote recipients (relay)
            if (hasRemoteRecipients && mailQueue is not null)
            {
                // This should only happen if user is authenticated (checked in RCPT TO)
                // But double-check here for safety
                if (!_authManager.IsAuthenticated && config.RequireAuthForRelay)
                {
                    logger.Log(LogLevel.Error, "BUG: Remote recipients without auth - this should not happen!");
                    await SendResponseAsync(550, "5.7.1 Relay access denied");
                    ResetTransaction();
                    return;
                }

                // Group remote recipients by domain for efficient delivery
                var recipientsByDomain = _remoteRcptTo
                    .GroupBy(r => ExtractDomain(r))
                    .Where(g => !string.IsNullOrEmpty(g.Key));

                foreach (var domainGroup in recipientsByDomain)
                {
                    var mailId = filePath is not null
                        ? $"{Path.GetFileNameWithoutExtension(filePath)}-{domainGroup.Key}"
                        : $"{Guid.NewGuid():N}-{domainGroup.Key}";

                    var queuedMail = new QueuedMail
                    {
                        Id = mailId,
                        EnvelopeFrom = _mailFrom ?? "",
                        EnvelopeTo = domainGroup.ToArray(),
                        MessageContent = stampedRaw,
                        TargetDomain = domainGroup.Key,
                        QueuedAt = DateTime.UtcNow,
                        NextRetry = DateTime.UtcNow
                    };

                    await mailQueue.EnqueueAsync(queuedMail, ct);
                    logger.Log(LogLevel.Info, $"Queued for relay to {domainGroup.Key}: {domainGroup.Count()} recipients");
                }
            }

            // Also store a copy locally for remote mail if configured (for sent mail archive)
            if (hasRemoteRecipients && !hasLocalRecipients)
            {
                // Optionally store sent mail - for now just log
                logger.Log(LogLevel.Debug, "Outbound-only message (not stored locally)");
            }

            await SendResponseAsync(250, "2.0.0 OK: Message accepted for delivery");
            ResetTransaction();

        }


        /// <summary>
        /// Build an "Authentication-Results:" header for this hop (RFC 8601) from the
        /// SPF/DKIM/DMARC verification results. Returned terminated with CRLF.
        /// </summary>
        private String BuildAuthenticationResultsHeader(DnsVerificationResult v, String senderDomain, String? fromDomain)
        {

            static String Spf(SPFResult r) => r switch {
                SPFResult.Pass      => "pass",
                SPFResult.Fail      => "fail",
                SPFResult.SoftFail  => "softfail",
                SPFResult.Neutral   => "neutral",
                SPFResult.TempError => "temperror",
                SPFResult.PermError => "permerror",
                _                   => "none"
            };

            static String Dkim(DkimResult r) => r switch {
                DkimResult.Pass      => "pass",
                DkimResult.Fail      => "fail",
                DkimResult.TempError => "temperror",
                DkimResult.PermError => "permerror",
                _                    => "none"
            };

            static String Dmarc(DmarcResult r) => r switch {
                DmarcResult.Pass      => "pass",
                DmarcResult.Fail      => "fail",
                DmarcResult.TempError => "temperror",
                DmarcResult.PermError => "permerror",
                _                     => "none"
            };

            static String Arc(ArcResult r) => r switch {
                ArcResult.Pass => "pass",
                ArcResult.Fail => "fail",
                _              => "none"
            };

            var mailFrom     = String.IsNullOrEmpty(_mailFrom) ? "<>" : _mailFrom;
            var dmarcComment = v.DmarcPolicy is not null ? $" (p={v.DmarcPolicy})" : "";
            var fromDom      = String.IsNullOrEmpty(fromDomain) ? senderDomain : fromDomain;

            // header.d is only meaningful when a signature was actually evaluated.
            var dkimClause   = v.DkimDomain is not null
                                   ? $"dkim={Dkim(v.Dkim)} header.d={v.DkimDomain}"
                                   : $"dkim={Dkim(v.Dkim)}";

            // arc= reflects the received-chain validation status (RFC 8617 §4.1.1); omitted
            // when the message carries no ARC headers.
            var arcClause = v.Arc != ArcResult.None ? $";\r\n\tarc={Arc(v.Arc)}" : "";

            return $"Authentication-Results: {config.Hostname};\r\n" +
                   $"\tspf={Spf(v.Spf)} smtp.mailfrom={mailFrom};\r\n" +
                   $"\t{dkimClause};\r\n" +
                   $"\tdmarc={Dmarc(v.Dmarc)} header.from={fromDom}{dmarcComment}{arcClause}\r\n";

        }

        /// <summary>
        /// Build a "Received:" trace header for this hop (RFC 5321 §4.4).
        /// The header is returned terminated with CRLF, ready to be prepended to the message.
        /// </summary>
        private async Task<String> BuildReceivedHeaderAsync(CancellationToken ct)
        {

            // from-clause: HELO name + (reverse-DNS or "unknown") + [address-literal]
            var reverseDns   = await dnsVerifier.ReverseLookupAsync(_clientIp, ct) ?? "unknown";
            var addrLiteral  = _clientIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                                   ? $"[IPv6:{_clientIp}]"
                                   : $"[{_clientIp}]";
            var fromClause   = $"from {(_heloHostname.Length > 0 ? _heloHostname : "unknown")} ({reverseDns} {addrLiteral})";

            // with-clause: RFC 3848 protocol name (HELO=SMTP, EHLO=ESMTP + S for TLS + A for AUTH)
            var protocol     = !_extendedSmtp
                                   ? "SMTP"
                                   : "ESMTP" + (_tlsActive ? "S" : "") + (_authManager.IsAuthenticated ? "A" : "");

            var id           = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            var timestamp    = DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss +0000",
                                                              System.Globalization.CultureInfo.InvariantCulture);

            // for-clause only for a single recipient (RFC 5321 §4.4: omit to avoid disclosing the list)
            var forClause    = _rcptTo.Count == 1 ? $"\r\n\tfor <{_rcptTo[0]}>" : "";

            return $"Received: {fromClause}\r\n" +
                   $"\tby {config.Hostname} (AchimSMTP) with {protocol} id {id}{forClause};\r\n" +
                   $"\t{timestamp}\r\n";

        }


        private void LogVerificationResult(DnsVerificationResult v)
        {

            var spfIcon   = v.Spf   == SPFResult.  Pass ? "✓" : v.Spf   == SPFResult.  Fail ? "✗" : "?";
            var dkimIcon  = v.Dkim  == DkimResult. Pass ? "✓" : v.Dkim  == DkimResult. Fail ? "✗" : "?";
            var dmarcIcon = v.Dmarc == DmarcResult.Pass ? "✓" : v.Dmarc == DmarcResult.Fail ? "✗" : "?";

            logger.Log(LogLevel.Info, $"Verification: SPF={spfIcon}{v.Spf} DKIM={dkimIcon}{v.Dkim} DMARC={dmarcIcon}{v.Dmarc}");

            if (v.MxRecords.Length > 0)
                logger.Log(LogLevel.Debug, $"MX Records: {string.Join(", ", v.MxRecords)}");

        }

        private static string ExtractDomain(string email)
        {
            var atIndex = email.IndexOf('@');
            return atIndex > 0 ? email[(atIndex + 1)..] : "";
        }

        // The header block of a raw message (everything up to the first empty line), used for
        // the headers-only body of a DMARC forensic (ARF) report.
        private static string ExtractHeaderBlock(string rawMessage)
        {
            var split = rawMessage.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (split < 0)
                split = rawMessage.IndexOf("\n\n", StringComparison.Ordinal);
            return split > 0 ? rawMessage[..split] : rawMessage;
        }

        private async Task HandleRsetAsync()
        {
            ResetTransaction();
            _authManager.Reset();
            await SendResponseAsync(250, "OK");
        }

        private void ResetTransaction()
        {
            _mailFrom        = null;
            _dsnEnvId        = null;
            _dsnRet          = DsnRet.Full;
            _requireTls      = false;
            _inBdatSequence  = false;
            _state           = SMTPSessionState.Greeted;
            _rcptTo.       Clear();
            _localRcptTo.  Clear();
            _remoteRcptTo. Clear();
            _recipientDsns.Clear();
            _bdatBuffer.   SetLength(0);
        }

        private async Task HandleQuitAsync()
        {
            await SendResponseAsync(221, $"{config.Hostname} closing connection");
            _state = SMTPSessionState.Quit;
        }

        private async Task SendResponseAsync(int code, string message, bool multiline = false)
        {
            var separator = multiline ? '-' : ' ';
            var response = $"{code}{separator}{message}";
            logger.Log(LogLevel.Debug, $"S: {response}");
            await _writer.WriteLineAsync(response);
        }

        private async Task<String?> ReadLineAsync(CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(config.SessionTimeout);
                return await _reader.ReadLineAsync(cts.Token);
            }
            catch
            {
                return null;
            }
        }

        private static (String Command, String Args) ParseCommand(String line)
        {
            var spaceIndex = line.IndexOf(' ');
            if (spaceIndex < 0)
                return (line, "");
            return (line[..spaceIndex], line[(spaceIndex + 1)..]);
        }

    }

}
