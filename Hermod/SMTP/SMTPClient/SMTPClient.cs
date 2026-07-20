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
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TLS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// A SMTP client for sending e-mails.
    /// </summary>
    public class SMTPClient : ATLSClient, ISMTPClient
    {

        #region Data

        private static readonly Byte[]         ByteZero            = new Byte[1] { 0x00 };

        // Per-instance send lock (was a process-wide static that serialized every SMTPClient).
        private readonly SemaphoreSlim         sendLock            = new (1, 1);

        // Per-command read/response timeout — bounds how long a silent server can stall us.
        private readonly TimeSpan              commandTimeout;

        private readonly ILogger<SMTPClient>   smtpLogger;

        #endregion

        #region Properties

        /// <summary>
        /// The local domain is used in the HELO or EHLO commands sent to
        /// the SMTP server. If left unset, the local IP address will be
        /// used instead.
        /// </summary>
        public String               LocalDomain             { get; }

        /// <summary>
        /// A login name which can be used for SMTP authentication.
        /// </summary>
        public String?              Login                   { get; }

        /// <summary>
        /// The password for the login name, which both will be used
        /// for SMTP authentication.
        /// </summary>
        public String?              Password                { get; }



        public UInt64               MaxMailSize             { get; }


        public SMTPAuthMethods      AuthMethods             { get; }


        public IEnumerable<String>  UnknownAuthMethods      { get; }


        public SmtpCapabilities     Capabilities;

        public DomainName           RemoteHost              { get; }

        public TLSUsage             UseTLS                  { get; }

        #endregion

        #region Events


        public event OnSendEMailRequestDelegate?   OnSendEMailRequest;


        public event OnSendEMailResponseDelegate?  OnSendEMailResponse;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SMTP client for sending e-mails.
        /// </summary>
        /// <param name="RemoteHost"></param>
        /// <param name="RemotePort"></param>
        /// <param name="Login"></param>
        /// <param name="Password"></param>
        /// <param name="LocalDomain"></param>
        /// <param name="UseIPv4">Whether to use IPv4 as networking protocol.</param>
        /// <param name="UseIPv6">Whether to use IPv6 as networking protocol.</param>
        /// <param name="PreferIPv6">Prefer IPv6 (instead of IPv4) as networking protocol.</param>
        /// <param name="UseTLS">Whether Transport Layer Security should be used or not.</param>
        /// <param name="RemoteCertificateValidator">A callback for validating the remote server certificate.</param>
        /// <param name="ConnectionTimeout">The timeout connecting to the remote service.</param>
        /// <param name="DNSClient">An optional DNS client used to resolve DNS names.</param>
        /// <param name="AutoConnect">Connect to the TCP service automatically on startup. Default is false.</param>
        /// <param name="CancellationToken"></param>
        public SMTPClient(DomainName                                                RemoteHost,
                          IPPort                                                    RemotePort,
                          String?                                                   Login                        = null,
                          String?                                                   Password                     = null,
                          String?                                                   LocalDomain                  = null,
                          Boolean                                                   UseIPv4                      = true,
                          Boolean                                                   UseIPv6                      = false,
                          Boolean                                                   PreferIPv6                   = false,
                          TLSUsage                                                  UseTLS                       = TLSUsage.STARTTLS,
                          RemoteTLSServerCertificateValidationHandler<SMTPClient>?  RemoteCertificateValidator   = null,
                          TimeSpan?                                                 ConnectionTimeout            = null,
                          TimeSpan?                                                 CommandTimeout               = null,
                          IDNSClient?                                               DNSClient                    = null,
                          Boolean                                                   AutoConnect                  = false,
                          CancellationToken?                                        CancellationToken            = null,
                          ILoggerFactory?                                           LoggerFactory                = null)

            : base(BuildSMTPURL(RemoteHost, RemotePort, UseTLS),

                   RemoteCertificateValidator: RemoteCertificateValidator is not null
                                                   ? (sender,
                                                      certificate,
                                                      certificateChain,
                                                      tlsClient,
                                                      policyErrors) => RemoteCertificateValidator.Invoke(
                                                                           sender,
                                                                           certificate,
                                                                           certificateChain,
                                                                           (SMTPClient) tlsClient,
                                                                           policyErrors
                                                                       )
                                                   : null,
                   EnforceTLS:                 UseTLS == TLSUsage.TLSSocket,

                   IPVersionPreference:        ToIPVersionPreference(UseIPv4, UseIPv6, PreferIPv6),
                   ConnectTimeout:             ConnectionTimeout,
                   DNSClient:                  DNSClient,
                   LoggerFactory:              LoggerFactory)

        {

            this.Login               = Login;
            this.Password            = Password;
            this.LocalDomain         = LocalDomain ?? System.Net.Dns.GetHostName();
            this.UnknownAuthMethods  = [];
            this.RemoteHost          = RemoteHost;
            this.UseTLS              = UseTLS;
            this.commandTimeout      = CommandTimeout ?? TimeSpan.FromSeconds(30);
            this.smtpLogger          = (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<SMTPClient>();

            if (AutoConnect)
                ReconnectAsync(CancellationToken ?? default).GetAwaiter().GetResult();

        }

        #endregion


        #region BuildSMTPURL(RemoteHost, RemotePort, UseTLS)

        private static URL BuildSMTPURL(DomainName  RemoteHost,
                                        IPPort      RemotePort,
                                        TLSUsage    UseTLS)
            => URL.Parse(
                   String.Concat(
                       UseTLS == TLSUsage.TLSSocket
                           ? "tls://"
                           : "tcp://",
                       $"{RemoteHost.Trimmed}:{RemotePort}"
                   )
               );

        #endregion

        #region ToIPVersionPreference(UseIPv4, UseIPv6, PreferIPv6)

        private static IPVersionPreference ToIPVersionPreference(Boolean UseIPv4,
                                                                 Boolean UseIPv6,
                                                                 Boolean PreferIPv6)
        {

            if (UseIPv4 && !UseIPv6)
                return IPVersionPreference.IPv4Only;

            if (!UseIPv4 && UseIPv6)
                return IPVersionPreference.IPv6Only;

            return PreferIPv6
                       ? IPVersionPreference.PreferIPv6
                       : IPVersionPreference.PreferIPv4;

        }

        #endregion


        #region (private) GenerateMessageId(Mail, DomainPart = null)

        private Message_Id GenerateMessageId(EMail        Mail,
                                             DomainName?  DomainPart   = null)
        {

            var randomBytes  = new Byte[16];
            Random.Shared.NextBytes(randomBytes);

            var hashedBytes  = SHA256.HashData([
                                   .. randomBytes,
                                   .. Mail.From.   ToString(). ToUTF8Bytes(),
                                   .. Mail.Subject.            ToUTF8Bytes(),
                                   .. Mail.Date.   ToISO8601().ToUTF8Bytes(),
                               ]);

            return Message_Id.Parse(
                       hashedBytes.ToHexString()[..24],
                       DomainPart is not null
                           ? DomainPart.FullName
                           : RemoteHost.FullName);

        }

        #endregion


        #region (protected) SendCommandAsync                 (Command, CancellationToken = default)

        /// <summary>
        /// Write a single SMTP command line to the server, fully asynchronously. The per-command
        /// deadline (<see cref="commandTimeout"/>) is enforced via a linked cancellation token — a
        /// silent/stalled server is surfaced as an <see cref="SMTPTimeoutException"/>, a dropped
        /// connection as an <see cref="SMTPConnectionClosedException"/>, and a caller cancellation
        /// propagates as an <see cref="OperationCanceledException"/>.
        /// </summary>
        protected async Task SendCommandAsync(String             Command,
                                              CancellationToken  CancellationToken   = default)
        {

            var CommandBytes = Encoding.UTF8.GetBytes(Command + "\r\n");
            var stream       = ActiveStream ?? throw new SMTPConnectionClosedException("SMTP client is not connected.");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            cts.CancelAfter(commandTimeout);

            try
            {
                await stream.WriteAsync(CommandBytes, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).           ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !CancellationToken.IsCancellationRequested)
            {
                // Our per-command deadline elapsed (not a caller cancellation) → timeout.
                throw new SMTPTimeoutException();
            }
            catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException)
            {
                // The server dropped the connection while we were sending (e.g. mid-DATA) → fail fast.
                throw new SMTPConnectionClosedException($"The SMTP connection was lost while sending: {e.Message}");
            }

            smtpLogger.LogTrace("SMTP command to {RemoteSocket}: {Command}",
                                RemoteSocket,
                                Command);

        }

        #endregion

        #region (protected) SendCommandAndWaitForResponseAsync (Command, CancellationToken = default)

        protected async Task<SMTPExtendedResponse> SendCommandAndWaitForResponseAsync(String             Command,
                                                                                      CancellationToken  CancellationToken   = default)
        {

            await SendCommandAsync(Command, CancellationToken).ConfigureAwait(false);

            return (await ReadSMTPResponsesAsync(CancellationToken).ConfigureAwait(false)).First();

        }

        #endregion

        #region (protected) SendCommandAndWaitForResponsesAsync(Command, CancellationToken = default)

        protected async Task<IEnumerable<SMTPExtendedResponse>> SendCommandAndWaitForResponsesAsync(String             Command,
                                                                                                   CancellationToken  CancellationToken   = default)
        {

            await SendCommandAsync(Command, CancellationToken).ConfigureAwait(false);

            return await ReadSMTPResponsesAsync(CancellationToken).ConfigureAwait(false);

        }

        #endregion


        #region (private)   ReadSMTPResponsesAsync              (CancellationToken = default)

        private async Task<IEnumerable<SMTPExtendedResponse>> ReadSMTPResponsesAsync(CancellationToken CancellationToken = default)
        {

            var stream = ActiveStream ?? throw new SMTPConnectionClosedException("SMTP client is not connected.");

            // A single SMTP reply may be multi-line (RFC 5321 §4.2.1): every line but the last has a
            // '-' after the status code, the final line a ' '. It may arrive split across several TCP
            // segments, so keep reading until the reply is complete.
            var sb   = new StringBuilder();
            var buf  = new Byte[64 * 1024];

            while (true)
            {

                Int32 nread;

                // Bound each read so a silent, non-responsive server ("mean" test) is detected within
                // the command timeout instead of blocking indefinitely. Async I/O ignores the stream's
                // Read/WriteTimeout, so the deadline is a linked, self-cancelling token per read.
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken))
                {

                    cts.CancelAfter(commandTimeout);

                    try
                    {
                        nread = await stream.ReadAsync(buf.AsMemory(0, buf.Length), cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested && !CancellationToken.IsCancellationRequested)
                    {
                        throw new SMTPTimeoutException();
                    }
                    catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException)
                    {
                        // Connection reset / aborted / disposed mid-read → treat as an abrupt close.
                        throw new SMTPConnectionClosedException($"The SMTP connection was lost: {e.Message}");
                    }

                }

                // A graceful close (FIN) surfaces as a 0-byte read → fail fast, do not spin.
                if (nread <= 0)
                    throw new SMTPConnectionClosedException();

                sb.Append(Encoding.UTF8.GetString(buf, 0, nread));

                var text = sb.ToString();
                if (!text.EndsWith("\r\n"))
                    continue;

                var completeLines = text.Split("\r\n").Where(l => l.Length > 0).ToArray();
                if (completeLines.Length > 0)
                {
                    var last = completeLines[^1];
                    // final line: 3 digits followed by a space or end-of-line (not a '-')
                    if (last.Length >= 3 && last.Take(3).All(Char.IsDigit) &&
                        (last.Length == 3 || last[3] == ' '))
                        break;
                }

            }

            var responses = new List<SMTPExtendedResponse>();

            foreach (var line in sb.ToString().Split("\r\n").Where(line => line.IsNotNullOrEmpty()))
            {

                var statusCodeChars  = line.TakeWhile(b => b != ' ' && b != '-').ToArray();
                var more             = statusCodeChars.Length < line.Length && line[statusCodeChars.Length] == '-';
                var description      = line.Skip(statusCodeChars.Length + 1).ToArray();

                if (UInt16.TryParse(new String(statusCodeChars), out var statusCode))
                    responses.Add(new SMTPExtendedResponse((SMTPStatusCodes) statusCode,
                                                           new String(description),
                                                           more));

            }

            // A well-formed-but-unparseable reply (e.g. garbage) must not yield an empty set that would
            // NRE the callers' .First(); surface it as a failed transaction.
            if (responses.Count == 0)
                responses.Add(new SMTPExtendedResponse(SMTPStatusCodes.TransactionFailed, "Unparseable server response"));

            return responses;

        }

        #endregion


        #region (private) SendDataAsync(Lines, CancellationToken = default)  — dot-stuffed message body

        /// <summary>
        /// Send the message content on the DATA channel with RFC 5321 §4.5.2 dot-stuffing: any line
        /// beginning with '.' gets an extra leading '.', so a bare "." line cannot terminate DATA early.
        /// The terminating "." is sent separately by the caller.
        /// </summary>
        private async Task SendDataAsync(IEnumerable<String>  Lines,
                                         CancellationToken    CancellationToken   = default)
        {
            foreach (var line in Lines)
                await SendCommandAsync(line.StartsWith('.') ? "." + line : line, CancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region (private) NeedsSmtpUtf8(EMailEnvelop)

        /// <summary>
        /// Whether this transaction needs SMTPUTF8 (RFC 6531): any non-ASCII byte in an envelope
        /// address or in the serialized message.
        /// </summary>
        private static Boolean NeedsSmtpUtf8(EMailEnvelop EMailEnvelop)
        {

            static Boolean NonAscii(String s) => s.Any(c => c > '\x7F');

            if (EMailEnvelop.MailFrom.Any(a => NonAscii(a.Address.ToString())) ||
                EMailEnvelop.RcptTo.  Any(a => NonAscii(a.Address.ToString())))
                return true;

            return EMailEnvelop.Mail is not null && EMailEnvelop.Mail.ToText().Any(NonAscii);

        }

        #endregion

        #region (private) ScramSha256AuthenticateAsync(Login, Password, CancellationToken = default)

        /// <summary>
        /// Perform a client-side SCRAM-SHA-256 (RFC 7677 / RFC 5802) authentication exchange.
        /// Returns true on a "235 Authentication successful", and verifies the server signature.
        /// </summary>
        private async Task<Boolean> ScramSha256AuthenticateAsync(String             Login,
                                                                 String             Password,
                                                                 CancellationToken  CancellationToken   = default)
        {

            static String B64(Byte[] b) => Convert.ToBase64String(b);

            // client-first
            var clientNonce      = B64(RandomNumberGenerator.GetBytes(18));
            var clientFirstBare  = $"n={SaslPrep(Login)},r={clientNonce}";
            var gs2Header        = "n,,";

            var first = await SendCommandAndWaitForResponseAsync("AUTH SCRAM-SHA-256 " + B64((gs2Header + clientFirstBare).ToUTF8Bytes()), CancellationToken).ConfigureAwait(false);
            if (first.StatusCode != SMTPStatusCodes.AuthenticationChallenge)
                return false;

            // server-first: r=<nonce>,s=<salt>,i=<iterations>
            var serverFirst  = Convert.FromBase64String(first.Response).ToUTF8String();
            var attrs        = serverFirst.Split(',').
                                   Where (p => p.Length > 1 && p[1] == '=').
                                   ToDictionary(p => p[0], p => p[2..]);

            if (!attrs.TryGetValue('r', out var serverNonce) || !serverNonce.StartsWith(clientNonce) ||
                !attrs.TryGetValue('s', out var saltB64)     ||
                !attrs.TryGetValue('i', out var iterStr)     || !Int32.TryParse(iterStr, out var iterations))
                return false;

            var salt                    = Convert.FromBase64String(saltB64);
            var saltedPassword          = ScramSha256Client.SaltedPassword(Password, salt, iterations);

            var channelBinding          = B64(gs2Header.ToUTF8Bytes());   // "biws"
            var clientFinalNoProof      = $"c={channelBinding},r={serverNonce}";
            var authMessage             = $"{clientFirstBare},{serverFirst},{clientFinalNoProof}";

            var clientProof             = ScramSha256Client.ClientProof(saltedPassword, authMessage);

            var final = await SendCommandAndWaitForResponseAsync(B64($"{clientFinalNoProof},p={B64(clientProof)}".ToUTF8Bytes()), CancellationToken).ConfigureAwait(false);

            // The server may send its final message (v=…) as a 334 continuation (ack with an empty
            // line) or fold it into the 235 success.
            var expectedServerSig = B64(ScramSha256Client.ServerSignature(saltedPassword, authMessage));

            if (final.StatusCode == SMTPStatusCodes.AuthenticationChallenge)
            {
                var serverFinal = Convert.FromBase64String(final.Response).ToUTF8String();
                if (!serverFinal.Contains("v=" + expectedServerSig))
                    return false;
                final = await SendCommandAndWaitForResponseAsync("", CancellationToken).ConfigureAwait(false);
            }

            return final.StatusCode == SMTPStatusCodes.AuthenticationSuccessful;

        }

        // Minimal SASLprep: reject characters that would break the SCRAM attribute syntax.
        private static String SaslPrep(String value)
            => value.Replace("=", "=3D").Replace(",", "=2C");

        #endregion

        #region (private) AuthPlainAsync / AuthLoginAsync / AuthCramMd5Async

        private async Task<Boolean> AuthPlainAsync(String Login, String Password, CancellationToken CancellationToken = default)
            => (await SendCommandAndWaitForResponseAsync(
                   "AUTH PLAIN " + Convert.ToBase64String([.. ByteZero, .. Login.ToUTF8Bytes(), .. ByteZero, .. Password.ToUTF8Bytes()]),
                   CancellationToken
               ).ConfigureAwait(false)).StatusCode == SMTPStatusCodes.AuthenticationSuccessful;

        private async Task<Boolean> AuthLoginAsync(String Login, String Password, CancellationToken CancellationToken = default)
        {

            if ((await SendCommandAndWaitForResponseAsync("AUTH LOGIN", CancellationToken).ConfigureAwait(false)).StatusCode != SMTPStatusCodes.AuthenticationChallenge)
                return false;

            if ((await SendCommandAndWaitForResponseAsync(Convert.ToBase64String(Login.ToUTF8Bytes()), CancellationToken).ConfigureAwait(false)).StatusCode != SMTPStatusCodes.AuthenticationChallenge)
                return false;

            return (await SendCommandAndWaitForResponseAsync(Convert.ToBase64String(Password.ToUTF8Bytes()), CancellationToken).ConfigureAwait(false)).StatusCode == SMTPStatusCodes.AuthenticationSuccessful;

        }

        private async Task<Boolean> AuthCramMd5Async(String Login, String Password, CancellationToken CancellationToken = default)
        {

            var challenge = await SendCommandAndWaitForResponseAsync("AUTH CRAM-MD5", CancellationToken).ConfigureAwait(false);
            if (challenge.StatusCode != SMTPStatusCodes.AuthenticationChallenge)
                return false;

            var response = await SendCommandAndWaitForResponseAsync(
                               Convert.ToBase64String(
                                   ISMTPClientExtensions.CRAM_MD5(Convert.FromBase64String(challenge.Response).ToUTF8String(),
                                                                  Login,
                                                                  Password)),
                               CancellationToken).ConfigureAwait(false);

            return response.StatusCode == SMTPStatusCodes.AuthenticationSuccessful;

        }

        #endregion


        #region (private) IsTransientFailure(Status)

        /// <summary>
        /// Whether a send outcome is a transient TRANSPORT failure that is safe to retry (the message
        /// was certainly not accepted): a dropped/stalled connection or a failed connect. Protocol
        /// rejections, auth failures and an oversized message are terminal.
        /// </summary>
        private static Boolean IsTransientFailure(MailSentStatus Status)

            => Status is MailSentStatus.ConnectionClosed
                      or MailSentStatus.Timeout
                      or MailSentStatus.NoIPAddressFound
                      or MailSentStatus.UnknownError;

        #endregion

        #region Send(EMail,        NumberOfRetries = 3, EventTrackingId = null, RequestTimeout = null)

        /// <summary>
        /// Send the given e-mail.
        /// </summary>
        /// <param name="EMail">An e-mail.</param>
        /// <param name="NumberOfRetries">The number of retries to send the given e-mail.</param>
        /// <param name="RequestTimeout">The request timeout for sending the given e-mail.</param>
        public Task<MailSentStatus> Send(EMail              EMail,
                                         Byte               NumberOfRetries     = 3,
                                         EventTracking_Id?  EventTrackingId     = null,
                                         TimeSpan?          RequestTimeout      = null,
                                         CancellationToken  CancellationToken   = default)

            => Send(
                   new EMailEnvelop(EMail),
                   NumberOfRetries,
                   EventTrackingId,
                   RequestTimeout,
                   CancellationToken
               );

        #endregion

        #region Send          (EMailEnvelop, NumberOfRetries = 3, EventTrackingId = null, RequestTimeout = null)

        /// <summary>
        /// Send the given e-mail envelop.
        /// </summary>
        /// <param name="EMailEnvelop">An e-mail envelop.</param>
        /// <param name="NumberOfRetries">The number of retries to send the given e-mail.</param>
        /// <param name="RequestTimeout">The request timeout for sending the given e-mail.</param>
        public async Task<MailSentStatus> Send(EMailEnvelop       EMailEnvelop,
                                               Byte               NumberOfRetries     = 3,
                                               EventTracking_Id?  EventTrackingId     = null,
                                               TimeSpan?          RequestTimeout      = null,
                                               CancellationToken  CancellationToken   = default)

            => (await SendWithResult(
                          EMailEnvelop,
                          NumberOfRetries,
                          EventTrackingId,
                          RequestTimeout,
                          CancellationToken
                      )).Status;

        #endregion

        #region SendWithResult(EMail,        NumberOfRetries = 3, EventTrackingId = null, RequestTimeout = null)

        /// <summary>
        /// Send the given e-mail and return the detailed transaction result.
        /// </summary>
        /// <param name="EMail">An e-mail.</param>
        /// <param name="NumberOfRetries">The number of retries to send the given e-mail.</param>
        /// <param name="RequestTimeout">The request timeout for sending the given e-mail.</param>
        public Task<SMTPSendResult> SendWithResult(EMail              EMail,
                                                   Byte               NumberOfRetries     = 3,
                                                   EventTracking_Id?  EventTrackingId     = null,
                                                   TimeSpan?          RequestTimeout      = null,
                                                   CancellationToken  CancellationToken   = default)

            => SendWithResult(
                   new EMailEnvelop(EMail),
                   NumberOfRetries,
                   EventTrackingId,
                   RequestTimeout,
                   CancellationToken
               );

        #endregion

        #region SendWithResult(EMailEnvelop, NumberOfRetries = 3, EventTrackingId = null, RequestTimeout = null)

        /// <summary>
        /// Send the given e-mail envelop and return the detailed transaction result: the final server
        /// reply (status code, text, enhanced status code), the per-recipient results, and the number of
        /// attempts, TLS/authentication state and runtime.
        /// </summary>
        /// <param name="EMailEnvelop">An e-mail envelop.</param>
        /// <param name="NumberOfRetries">The number of retries to send the given e-mail.</param>
        /// <param name="RequestTimeout">The request timeout for sending the given e-mail.</param>
        public async Task<SMTPSendResult> SendWithResult(EMailEnvelop       EMailEnvelop,
                                                         Byte               NumberOfRetries     = 3,
                                                         EventTracking_Id?  EventTrackingId     = null,
                                                         TimeSpan?          RequestTimeout      = null,
                                                         CancellationToken  CancellationToken   = default)
        {

            var eventTrackingId = EventTrackingId ?? EventTracking_Id.New;

            #region Send OnSendEMailRequest event

            var startTime = Timestamp.Now;

            try
            {

                if (OnSendEMailRequest is not null)
                    await Task.WhenAll(OnSendEMailRequest.GetInvocationList().
                                       Cast<OnSendEMailRequestDelegate>().
                                       Select(e => e(startTime,
                                                     this,
                                                     eventTrackingId,
                                                     EMailEnvelop,
                                                     RequestTimeout))).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                smtpLogger.LogError(e, "SMTP OnSendEMailRequest event failed.");
            }

            #endregion


            var result             = MailSentStatus.failed;
            var recipientResults   = new List<SMTPRecipientResult>();
            SMTPExtendedResponse?  finalResponse   = null;   // the end-of-DATA ack on success, or the failing reply
            var tlsActive          = false;
            var authenticated      = false;
            Byte attemptsMade      = 1;

            // The token that drives the actual I/O: the caller's token, plus a RequestTimeout deadline
            // when one was given. Now that the transport is fully async, cancelling the caller's token
            // (or the deadline elapsing) aborts an in-flight read/write instead of being ignored.
            using var requestTimeoutCancellationTokenSource = RequestTimeout.HasValue
                                                                  ? new CancellationTokenSource(RequestTimeout.Value)
                                                                  : null;
            using var linkedCancellationTokenSource         = requestTimeoutCancellationTokenSource is not null
                                                                  ? CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, requestTimeoutCancellationTokenSource.Token)
                                                                  : null;
            var cancellationToken = linkedCancellationTokenSource?.Token ?? CancellationToken;

            if (await sendLock.WaitAsync(
                          RequestTimeout ?? TimeSpan.FromSeconds(60),
                          CancellationToken))
            {
                try
                {
                  for (var attempt = 0; ; attempt++)
                  {
                    // Reset per-attempt observations so a retry reports only its own transaction.
                    attemptsMade = (Byte) (attempt + 1);
                    recipientResults.Clear();
                    finalResponse = null;
                    tlsActive     = false;
                    authenticated = false;
                    try
                    {

                    var connectionResult = await ReconnectAsync(cancellationToken).ConfigureAwait(false);

                    if (connectionResult.IsFailure)
                    {
                        smtpLogger.LogWarning("SMTP connection to {RemoteHost}:{RemotePort} failed: {Errors}",
                                              RemoteHost,
                                              RemotePort,
                                              connectionResult.Errors.AggregateWith(", "));

                        result = connectionResult.Errors.Any(error => error.ToString().Contains("No valid remote IP address", StringComparison.OrdinalIgnoreCase))
                                     ? MailSentStatus.NoIPAddressFound
                                     : MailSentStatus.UnknownError;
                    }

                    else
                    {

                            var authMethods         = SMTPAuthMethods.None;
                            var unknownAuthMethods  = new HashSet<String>();
                            var serverMaxSize       = 0UL;   // RFC 1870 SIZE limit (0 = unknown/none)
                            var supportsMtPriority  = false; // RFC 6710
                            var supportsRequireTls  = false; // RFC 8689

                            // 220 mail.ahzf.de ESMTP Postfix (Debian/GNU)
                            var LoginResponse       = await ReadSMTPResponsesAsync(cancellationToken).ConfigureAwait(false);

                            switch (LoginResponse.First().StatusCode)
                            {

                                case SMTPStatusCodes.ServiceReady:

                                    #region Send EHLO

                                    var EHLOResponses = await SendCommandAndWaitForResponsesAsync("EHLO " + LocalDomain, cancellationToken).ConfigureAwait(false);

                                    // 250-mail.ahzf.de
                                    // 250-PIPELINING
                                    // 250-SIZE 30720000
                                    // 250-VRFY
                                    // 250-ETRN
                                    // 250-STARTTLS
                                    // 250-AUTH LOGIN DIGEST-MD5 PLAIN CRAM-MD5
                                    // 250-AUTH=LOGIN DIGEST-MD5 PLAIN CRAM-MD5
                                    // 250-ENHANCEDSTATUSCODES
                                    // 250-8BITMIME
                                    // 250 DSN

                                    if (EHLOResponses.Any(v => v.StatusCode != SMTPStatusCodes.Ok))
                                    {

                                        var Error = EHLOResponses.Where(v => v.StatusCode != SMTPStatusCodes.Ok).
                                                                    FirstOrDefault();

                                        if (Error.StatusCode != SMTPStatusCodes.Ok)
                                            throw new SMTPClientException("SMTP EHLO command error: " + Error.ToString());

                                    }

                                    #endregion

                                    #region Check for STARTTLS

                                    if (UseTLS == TLSUsage.STARTTLS)
                                    {

                                        if (EHLOResponses.Any(v => v.Response == "STARTTLS"))
                                        {

                                            var StartTLSResponse = await SendCommandAndWaitForResponseAsync("STARTTLS", cancellationToken).ConfigureAwait(false);

                                            if (StartTLSResponse.StatusCode == SMTPStatusCodes.ServiceReady)
                                            {
                                                var startTLSResult = await StartTLS(cancellationToken).ConfigureAwait(false);

                                                if (startTLSResult.IsFailure)
                                                    throw new SMTPClientException("SMTP STARTTLS failed: " + startTLSResult.Errors.AggregateWith(", "));
                                            }

                                        }

                                        else
                                            throw new Exception("TLS is not supported by the SMTP server!");

                                        // Send EHLO again in order to get the new list of supported extensions!
                                        EHLOResponses = await SendCommandAndWaitForResponsesAsync("EHLO " + LocalDomain, cancellationToken).ConfigureAwait(false);

                                    }

                                    #endregion

                                    #region Analyze EHLO responses and set SMTP capabilities

                                    // 250-mail.ahzf.de
                                    // 250-PIPELINING
                                    // 250-SIZE 30720000
                                    // 250-VRFY
                                    // 250-ETRN
                                    // 250-STARTTLS
                                    // 250-AUTH LOGIN DIGEST-MD5 PLAIN CRAM-MD5
                                    // 250-AUTH=LOGIN DIGEST-MD5 PLAIN CRAM-MD5
                                    // 250-ENHANCEDSTATUSCODES
                                    // 250-8BITMIME
                                    // 250 DSN

                                    var MailServerName = EHLOResponses.FirstOrDefault();

                                    EHLOResponses.Skip(1).ForEach(v =>
                                    {

                                        #region PIPELINING

                                        if (v.Response == "PIPELINING")
                                            Capabilities |= SmtpCapabilities.Pipelining;

                                        #endregion

                                        #region SIZE

                                        else if (v.Response.StartsWith("SIZE"))
                                        {

                                            Capabilities |= SmtpCapabilities.Size;

                                            // "SIZE 10485760" — the value is optional (RFC 1870 §6.1); 0 means "no fixed limit".
                                            var sizeArg = v.Response.Length > 5 ? v.Response[5..].Trim() : "";
                                            if (sizeArg.Length > 0 && UInt64.TryParse(sizeArg, out var maxMailSize))
                                                serverMaxSize = maxMailSize;

                                        }

                                        #endregion

                                        #region MT-PRIORITY (RFC 6710) / REQUIRETLS (RFC 8689)

                                        if (v.Response == "MT-PRIORITY" || v.Response.StartsWith("MT-PRIORITY "))
                                            supportsMtPriority = true;

                                        if (v.Response == "REQUIRETLS")
                                            supportsRequireTls = true;

                                        #endregion

                                        //   else if (v.Response == "VRFY")
                                        //       Capabilities |= SmtpCapabilities.;

                                        //   else if (v.Response == "ETRN")
                                        //       Capabilities |= SmtpCapabilities.;

                                        #region STARTTLS

                                        if (v.Response == "STARTTLS")
                                            Capabilities |= SmtpCapabilities.StartTLS;

                                        #endregion

                                        #region AUTH

                                        else if (v.Response.StartsWith("AUTH "))
                                        {

                                            Capabilities |= SmtpCapabilities.Authentication;

                                            var AuthType            = v.Response.Substring(4, 1);
                                            var AuthMethods         = v.Response.Substring(5).
                                                                                 Split    (new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).
                                                                                 Select   (method => method?.Trim());

                                            // GMail: "AUTH LOGIN PLAIN XOAUTH XOAUTH2 PLAIN-CLIENTTOKEN"
                                            foreach (var authMethod in AuthMethods)
                                            {
                                                if (Enum.TryParse(authMethod.Replace('-', '_'), true, out SMTPAuthMethods parsedAuthMethod))
                                                {
                                                    if (AuthType == " ")
                                                        authMethods |= parsedAuthMethod;
                                                }
                                                else
                                                    unknownAuthMethods.Add(authMethod?.Trim());
                                            }

                                        }

                                        #endregion

                                        #region ENHANCEDSTATUSCODES

                                        if (v.Response == "ENHANCEDSTATUSCODES")
                                            Capabilities |= SmtpCapabilities.EnhancedStatusCodes;

                                        #endregion

                                        #region 8BITMIME

                                        if (v.Response == "8BITMIME")
                                            Capabilities |= SmtpCapabilities.EightBitMime;

                                        #endregion

                                        #region DSN

                                        if (v.Response == "DSN")
                                            Capabilities |= SmtpCapabilities.Dsn;

                                        #endregion

                                        #region BINARYMIME

                                        if (v.Response == "BINARYMIME")
                                            Capabilities |= SmtpCapabilities.BinaryMime;

                                        #endregion

                                        #region CHUNKING

                                        if (v.Response == "CHUNKING")
                                            Capabilities |= SmtpCapabilities.Chunking;

                                        #endregion

                                        #region SMTPUTF8

                                        if (v.Response == "SMTPUTF8")
                                            Capabilities |= SmtpCapabilities.UTF8;

                                        #endregion

                                    });

                                    #endregion

                                    #region AUTH (if credentials configured)

                                    if (Login is not null)
                                    {

                                        // RFC 8314: never send credentials over an unencrypted connection.
                                        if (!IsTLSActive)
                                        {
                                            smtpLogger.LogWarning("SMTP AUTH refused: connection to {RemoteHost} is not TLS-secured", RemoteHost);
                                            result = MailSentStatus.InvalidLogin;
                                            break;
                                        }

                                        var authOk =
                                            authMethods.HasFlag(SMTPAuthMethods.SCRAM_SHA_256) ? await ScramSha256AuthenticateAsync(Login, Password ?? "", cancellationToken).ConfigureAwait(false) :
                                            authMethods.HasFlag(SMTPAuthMethods.PLAIN)         ? await AuthPlainAsync           (Login, Password ?? "", cancellationToken).ConfigureAwait(false) :
                                            authMethods.HasFlag(SMTPAuthMethods.LOGIN)         ? await AuthLoginAsync           (Login, Password ?? "", cancellationToken).ConfigureAwait(false) :
                                            authMethods.HasFlag(SMTPAuthMethods.CRAM_MD5)      ? await AuthCramMd5Async         (Login, Password ?? "", cancellationToken).ConfigureAwait(false) :
                                            throw new SMTPClientException("The SMTP server offers no supported authentication mechanism.");

                                        if (!authOk)
                                        {
                                            smtpLogger.LogWarning("SMTP authentication at {RemoteHost} failed", RemoteHost);
                                            result = MailSentStatus.InvalidLogin;
                                            break;
                                        }

                                        authenticated = true;

                                    }

                                    #endregion

                                    #region Pre-checks: REQUIRETLS and message SIZE

                                    // REQUIRETLS (RFC 8689): if the caller demands authenticated TLS, never proceed in the clear.
                                    if (EMailEnvelop.RequireTls && !IsTLSActive)
                                    {
                                        smtpLogger.LogWarning("REQUIRETLS demanded but the connection to {RemoteHost} is not TLS-secured", RemoteHost);
                                        result = MailSentStatus.failed;
                                        break;
                                    }

                                    // Serialize once, so we can declare/verify SIZE (RFC 1870) and send the body.
                                    var messageLines  = EMailEnvelop.Mail?.ToText().ToArray() ?? [];
                                    var messageBytes  = (UInt64) String.Join("\r\n", messageLines).ToUTF8Bytes().LongLength;

                                    if (serverMaxSize > 0 && messageBytes > serverMaxSize)
                                    {
                                        smtpLogger.LogWarning("Message ({MessageBytes} B) exceeds the server SIZE limit ({ServerMaxSize} B)", messageBytes, serverMaxSize);
                                        result = MailSentStatus.MessageSizeExceeded;
                                        break;
                                    }

                                    #endregion

                                    #region MAIL FROM:

                                    // Record the transport security state at the moment we submit the message.
                                    tlsActive        = IsTLSActive;

                                    // A transaction has exactly one MAIL FROM (RFC 5321 §3.3).
                                    var mailFrom     = EMailEnvelop.MailFrom.FirstOrDefault();
                                    var needsUtf8    = Capabilities.HasFlag(SmtpCapabilities.UTF8) && NeedsSmtpUtf8(EMailEnvelop);

                                    // MAIL FROM:<test@example.com>  (no space after the colon, RFC 5321)
                                    var mailFromCommand = "MAIL FROM:<" + (mailFrom?.Address.ToString() ?? "") + ">";

                                    if (Capabilities.HasFlag(SmtpCapabilities.EightBitMime))
                                        mailFromCommand += " BODY=8BITMIME";
                                    else if (Capabilities.HasFlag(SmtpCapabilities.BinaryMime))
                                        mailFromCommand += " BODY=BINARYMIME";

                                    if (needsUtf8)
                                        mailFromCommand += " SMTPUTF8";

                                    // SIZE (RFC 1870): declare the message size so the server can reject early.
                                    if (Capabilities.HasFlag(SmtpCapabilities.Size))
                                        mailFromCommand += $" SIZE={messageBytes}";

                                    // DSN (RFC 3461) / MT-PRIORITY (RFC 6710) / REQUIRETLS (RFC 8689) — the envelope's
                                    // transaction parameters, emitted only when the server advertised the extension.
                                    mailFromCommand += DsnCommands.MailFromParams(EMailEnvelop.Dsn, Capabilities.HasFlag(SmtpCapabilities.Dsn));
                                    mailFromCommand  = MtPriority.AppendMailFromParam(mailFromCommand, EMailEnvelop.Priority, supportsMtPriority);

                                    if (EMailEnvelop.RequireTls && supportsRequireTls)
                                        mailFromCommand += " REQUIRETLS";

                                    var mailFromResponse = await SendCommandAndWaitForResponseAsync(mailFromCommand, cancellationToken).ConfigureAwait(false);
                                    if (mailFromResponse.StatusCode != SMTPStatusCodes.Ok)
                                        throw new SMTPClientException("SMTP MAIL FROM command error: " + mailFromResponse.ToString());

                                    #endregion

                                    #region RCPT TO(s):

                                    // RCPT TO:<user@example.com>  (+ NOTIFY/ORCPT when DSN was requested and supported)
                                    // 250 2.1.5 Ok
                                    var dsnSupported = Capabilities.HasFlag(SmtpCapabilities.Dsn);
                                    foreach (var rcpt in EMailEnvelop.RcptTo)
                                    {

                                        var rcptToResponse = await SendCommandAndWaitForResponseAsync(
                                                                 "RCPT TO:<" + rcpt.Address.ToString() + ">" +
                                                                 DsnCommands.RcptToParams(EMailEnvelop.Dsn, rcpt.Address.ToString(), dsnSupported),
                                                                 cancellationToken).ConfigureAwait(false);

                                        recipientResults.Add(
                                            new SMTPRecipientResult(
                                                rcpt.Address,
                                                rcptToResponse.StatusCode,
                                                rcptToResponse.Response,
                                                SMTPSendResult.ExtractEnhancedStatusCode(rcptToResponse.Response)
                                            )
                                        );

                                        switch (rcptToResponse.StatusCode)
                                        {

                                            case SMTPStatusCodes.UserNotLocalWillForward:
                                            case SMTPStatusCodes.Ok:
                                                break;

                                            case SMTPStatusCodes.UserNotLocalTryAlternatePath:
                                            case SMTPStatusCodes.MailboxNameNotAllowed:
                                            case SMTPStatusCodes.MailboxUnavailable:
                                            case SMTPStatusCodes.MailboxBusy:
                                                throw new SMTPClientException        (rcpt.Address.ToString() + " => " + rcptToResponse.StatusCode);

                                            case SMTPStatusCodes.AuthenticationRequired:
                                                throw new UnauthorizedAccessException(rcpt.Address.ToString() + " => " + rcptToResponse.StatusCode);

                                            default:
                                                throw new SMTPClientException        (rcpt.Address.ToString() + " => " + rcptToResponse.StatusCode);

                                        }

                                    }

                                    #endregion

                                    #region Mail DATA

                                    // The encoded MIME text lines must not be longer than 76 characters!

                                    // 354 End data with <CR><LF>.<CR><LF>
                                    var dataResponse = await SendCommandAndWaitForResponseAsync("DATA", cancellationToken).ConfigureAwait(false);
                                    if (dataResponse.StatusCode != SMTPStatusCodes.StartMailInput)
                                        throw new SMTPClientException("SMTP DATA command error: " + dataResponse.ToString());

                                    // Send the complete RFC 5322 message (headers, blank line, body) in its
                                    // canonical serialized order — NOT reconstructed from the header dictionary
                                    // (whose order is not guaranteed and would break DKIM). Dot-stuffed.
                                    if (messageLines.Length > 0)
                                        await SendDataAsync(messageLines, cancellationToken).ConfigureAwait(false);

                                    #endregion

                                    #region End-of-DATA

                                    // .
                                    // 250 2.0.0 Ok: queued as 83398728027
                                    var _FinishedResponse = await SendCommandAndWaitForResponseAsync(".", cancellationToken).ConfigureAwait(false);
                                    if (_FinishedResponse.StatusCode != SMTPStatusCodes.Ok)
                                        throw new SMTPClientException("SMTP DATA '.' command error: " + _FinishedResponse.ToString());

                                    // The authoritative final acknowledgement ("250 2.0.0 Ok: queued as ...").
                                    finalResponse = _FinishedResponse;

                                    #endregion

                                    #region QUIT

                                    // QUIT
                                    // 221 2.0.0 Bye
                                    var _QuitResponse = await SendCommandAndWaitForResponseAsync("QUIT", cancellationToken).ConfigureAwait(false);
                                    if (_QuitResponse.StatusCode != SMTPStatusCodes.ServiceClosingTransmissionChannel)
                                        throw new SMTPClientException("SMTP QUIT command error: " + _QuitResponse.ToString());

                                    #endregion

                                    result = MailSentStatus.ok;

                                    break;

                                default:
                                    result = MailSentStatus.InvalidLogin;
                                    break;

                            }

                    }

                    }
                    catch (SMTPTimeoutException e)
                    {
                        smtpLogger.LogWarning(e, "SMTP server did not respond in time.");
                        result = MailSentStatus.Timeout;
                    }
                    catch (SMTPConnectionClosedException e)
                    {
                        // "Mean" case: the server dropped the TCP connection. Detected immediately.
                        smtpLogger.LogWarning(e, "SMTP server closed the connection unexpectedly.");
                        result = MailSentStatus.ConnectionClosed;
                    }
                    catch (Exception e)
                    {
                        smtpLogger.LogError(e, "SMTP send failed.");
                        result = MailSentStatus.ExceptionOccurred;
                    }

                    // Retry only transient TRANSPORT failures (a dropped/stalled connection or a failed
                    // connect), bounded by NumberOfRetries. Protocol rejections, auth failures, an
                    // oversized message and, conservatively, any other exception are NOT retried (the
                    // message may already have been accepted, so a resend could duplicate it).
                    if (!IsTransientFailure(result) || attempt >= NumberOfRetries)
                        break;

                    smtpLogger.LogInformation("SMTP send to {RemoteHost} failed transiently ({Result}); retry {Next}/{Max}",
                                              RemoteHost, result, attempt + 1, NumberOfRetries);

                    try   { await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)), CancellationToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }

                  }
                }
                finally
                {
                    sendLock.Release();
                }
            }


            #region Send OnSendEMailResponse event

            var endTime = Timestamp.Now;

            try
            {

                if (OnSendEMailResponse is not null)
                    await Task.WhenAll(OnSendEMailResponse.GetInvocationList().
                                       Cast<OnSendEMailResponseDelegate>().
                                       Select(e => e(endTime,
                                                     this,
                                                     eventTrackingId,
                                                     EMailEnvelop,
                                                     RequestTimeout,
                                                     result,
                                                     endTime - startTime))).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                smtpLogger.LogError(e, "SMTP OnSendEMailResponse event failed.");
            }

            #endregion

            return new SMTPSendResult(
                       Status:           result,
                       EventTrackingId:  eventTrackingId,
                       StatusCode:       finalResponse?.StatusCode,
                       Response:         finalResponse?.Response,
                       Recipients:       recipientResults,
                       Attempts:         attemptsMade,
                       TLSActive:        tlsActive,
                       Authenticated:    authenticated,
                       Runtime:          endTime - startTime
                   );

        }

        #endregion


        #region Dispose()

        public override void Dispose()
        {
            base.Dispose();
        }

        #endregion

    }

}
