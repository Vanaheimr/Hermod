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

        private static readonly SemaphoreSlim  SendEMailSemaphore  = new (1, 1);

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
                       RemoteHost,
                       ":",
                       RemotePort
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


        #region (protected) SendCommand(Command)

        protected void SendCommand(String Command)
        {

            var CommandBytes = Encoding.UTF8.GetBytes(Command + "\r\n");
            var stream       = ActiveStream ?? throw new InvalidOperationException("SMTP client is not connected.");

            stream.Write(CommandBytes, 0, CommandBytes.Length);
            stream.Flush();

            smtpLogger.LogTrace("SMTP command to {RemoteSocket}: {Command}",
                                RemoteSocket,
                                Command);

        }

        #endregion

        #region (protected) SendCommandAndWaitForResponse (Command)

        protected SMTPExtendedResponse SendCommandAndWaitForResponse(String Command)
        {

            SendCommand(Command);

            return ReadSMTPResponses().First();

        }

        #endregion

        #region (protected) SendCommandAndWaitForResponses(Command)

        protected IEnumerable<SMTPExtendedResponse> SendCommandAndWaitForResponses(String Command)
        {

            SendCommand(Command);

            return ReadSMTPResponses();

        }

        #endregion


        #region (private)   ReadSMTPResponses()

        private IEnumerable<SMTPExtendedResponse> ReadSMTPResponses()
        {

            var buffer = Array.Empty<Byte>();

            try
            {

                if (buffer.Length == 0)
                {

                    buffer     = new Byte[64 * 1024];

                    var stream = ActiveStream ?? throw new InvalidOperationException("SMTP client is not connected.");
                    var nread  = stream.Read(buffer, 0, buffer.Length);

                    Array.Resize(ref buffer, nread);

                }

                // 250-mail.ahzf.de
                // 250-PIPELINING
                // 250-SIZE 204800000
                // 250-VRFY
                // 250-ETRN
                // 250-STARTTLS
                // 250-AUTH PLAIN LOGIN CRAM-MD5 DIGEST-MD5
                // 250-AUTH=PLAIN LOGIN CRAM-MD5 DIGEST-MD5
                // 250-ENHANCEDSTATUSCODES
                // 250-8BITMIME
                // 250-DSN
                // 250-SMTPUTF8
                // 250 CHUNKING

                var responses = new List<SMTPExtendedResponse>();

                var lines     = buffer.ToUTF8String().
                                       Split("\r\n").
                                       Where(line => line.IsNotNullOrEmpty()).
                                       ToArray();

                foreach (var line in lines) {

                    var statusCodeChars  = line.TakeWhile(b => b != ' ' && b != '-').ToArray();
                    var more             = line[statusCodeChars.Length] == '-';
                    var description      = line.Skip(statusCodeChars.Length + 1).ToArray();

                    if (UInt16.TryParse(new String(statusCodeChars), out var statusCode))
                        responses.Add(new SMTPExtendedResponse((SMTPStatusCodes) statusCode,
                                                               new String(description),
                                                               more));

                }

                return responses;

            } catch (Exception e)
            {

                return new SMTPExtendedResponse[] {
                           new SMTPExtendedResponse(
                               SMTPStatusCodes.TransactionFailed,
                               e.Message
                           )
                       };

            }

        }

        #endregion


        #region Send(EMail,        NumberOfRetries = 3, EventTrackingId = null, RequestTimeout = null)

        /// <summary>
        /// Send the given e-mail.
        /// </summary>
        /// <param name="EMail">An e-mail.</param>
        /// <param name="NumberOfRetries">The number of retries to send the given e-mail.</param>
        /// <param name="RequestTimeout">The request timeout for sending the given e-mail.</param>
        public Task<MailSentStatus> Send(EMail              EMail,
                                         Byte               NumberOfRetries   = 3,
                                         EventTracking_Id?  EventTrackingId   = null,
                                         TimeSpan?          RequestTimeout    = null)

            => Send(new EMailEnvelop(EMail),
                    NumberOfRetries,
                    EventTrackingId,
                    RequestTimeout);

        #endregion

        #region Send(EMailEnvelop, NumberOfRetries = 3, EventTrackingId = null, RequestTimeout = null)

        /// <summary>
        /// Send the given e-mail envelop.
        /// </summary>
        /// <param name="EMailEnvelop">An e-mail envelop.</param>
        /// <param name="NumberOfRetries">The number of retries to send the given e-mail.</param>
        /// <param name="RequestTimeout">The request timeout for sending the given e-mail.</param>
        public async Task<MailSentStatus> Send(EMailEnvelop       EMailEnvelop,
                                               Byte               NumberOfRetries   = 3,
                                               EventTracking_Id?  EventTrackingId   = null,
                                               TimeSpan?          RequestTimeout    = null)
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


            var result = MailSentStatus.failed;
            using var requestTimeoutCancellationTokenSource = RequestTimeout.HasValue
                                                                  ? new CancellationTokenSource(RequestTimeout.Value)
                                                                  : null;
            var cancellationToken = requestTimeoutCancellationTokenSource?.Token ?? default;

            if (await SendEMailSemaphore.WaitAsync(RequestTimeout ?? TimeSpan.FromSeconds(60)))
            {
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

                            // 220 mail.ahzf.de ESMTP Postfix (Debian/GNU)
                            var LoginResponse       = ReadSMTPResponses();

                            switch (LoginResponse.First().StatusCode)
                            {

                                case SMTPStatusCodes.ServiceReady:

                                    #region Send EHLO

                                    var EHLOResponses = SendCommandAndWaitForResponses("EHLO " + LocalDomain);

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

                                            var StartTLSResponse = SendCommandAndWaitForResponse("STARTTLS");

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
                                        EHLOResponses = SendCommandAndWaitForResponses("EHLO " + LocalDomain);

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

                                        else if (v.Response.StartsWith("SIZE "))
                                        {

                                            Capabilities |= SmtpCapabilities.Size;

                                            if (!UInt64.TryParse(v.Response.Substring(5), out UInt64 maxMailSize))
                                                throw new Exception("Invalid SIZE capability!");

                                        }

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

                                        #region UTF8

                                        if (v.Response == "UTF8")
                                            Capabilities |= SmtpCapabilities.UTF8;

                                        #endregion

                                    });

                                    #endregion

                                    #region Auth PLAIN...

                                    if (authMethods.HasFlag(SMTPAuthMethods.PLAIN))
                                    {

                                        var response = SendCommandAndWaitForResponse("AUTH PLAIN " +
                                                                                     Convert.ToBase64String(ByteZero.
                                                                                                            Concat(Login.ToUTF8Bytes()).
                                                                                                            Concat(ByteZero).
                                                                                                            Concat(Password.ToUTF8Bytes()).
                                                                                                            ToArray()));

                                        smtpLogger.LogTrace("SMTP AUTH PLAIN response from {RemoteSocket}: {Response}",
                                                            RemoteSocket,
                                                            response);

                                    }

                                    #endregion

                                    #region ...or Auth LOGIN...

                                    else if (authMethods.HasFlag(SMTPAuthMethods.LOGIN))
                                    {

                                        var response1 = SendCommandAndWaitForResponse("AUTH LOGIN");
                                        var response2 = SendCommandAndWaitForResponse(Convert.ToBase64String(Login.   ToUTF8Bytes()));
                                        var response3 = SendCommandAndWaitForResponse(Convert.ToBase64String(Password.ToUTF8Bytes()));

                                        smtpLogger.LogTrace("SMTP AUTH LOGIN responses from {RemoteSocket}: {Response1}, {Response2}, {Response3}",
                                                            RemoteSocket,
                                                            response1,
                                                            response2,
                                                            response3);

                                    }

                                    #endregion

                                    #region ...or AUTH CRAM-MD5

                                    else if (authMethods.HasFlag(SMTPAuthMethods.CRAM_MD5))
                                    {

                                        var AuthCRAMMD5Response = SendCommandAndWaitForResponse("AUTH CRAM-MD5");

                                        if (AuthCRAMMD5Response.StatusCode == SMTPStatusCodes.AuthenticationChallenge)
                                        {

                                            var response = SendCommandAndWaitForResponse(
                                                               Convert.ToBase64String(
                                                                   ISMTPClientExtensions.CRAM_MD5(Convert.FromBase64String(AuthCRAMMD5Response.Response).ToUTF8String(),
                                                                                                  Login,
                                                                                                  Password)));

                                            smtpLogger.LogTrace("SMTP AUTH CRAM-MD5 response from {RemoteSocket}: {Response}",
                                                                RemoteSocket,
                                                                response);

                                        }

                                    }

                                    #endregion

                                    #region MAIL FROM:

                                    foreach (var MailFrom in EMailEnvelop.MailFrom) {

                                        // MAIL FROM:<test@example.com>
                                        // 250 2.1.0 Ok
                                        var mailFromCommand = "MAIL FROM: <" + MailFrom.Address.ToString() + ">";

                                        if (Capabilities.HasFlag(SmtpCapabilities.EightBitMime))
                                            mailFromCommand += " BODY=8BITMIME";
                                        else if (Capabilities.HasFlag(SmtpCapabilities.BinaryMime))
                                            mailFromCommand += " BODY=BINARYMIME";

                                        var mailFromResponse = SendCommandAndWaitForResponse(mailFromCommand);
                                        if (mailFromResponse.StatusCode != SMTPStatusCodes.Ok)
                                            throw new SMTPClientException("SMTP MAIL FROM command error: " + mailFromResponse.ToString());

                                    }

                                    #endregion

                                    #region RCPT TO(s):

                                    // RCPT TO:<user@example.com>
                                    // 250 2.1.5 Ok
                                    EMailEnvelop.RcptTo.ForEach(rcpt => {

                                        var rcptToResponse = SendCommandAndWaitForResponse("RCPT TO: <" + rcpt.Address.ToString() + ">");

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

                                    });

                                    #endregion

                                    #region Mail DATA

                                    // The encoded MIME text lines must not be longer than 76 characters!

                                    // 354 End data with <CR><LF>.<CR><LF>
                                    var dataResponse = SendCommandAndWaitForResponse("DATA");
                                    if (dataResponse.StatusCode != SMTPStatusCodes.StartMailInput)
                                        throw new SMTPClientException("SMTP DATA command error: " + dataResponse.ToString());

                                    // Send e-mail headers...
                                    if (EMailEnvelop.Mail is not null) {

                                        EMailEnvelop.Mail.
                                                     Headers.
                                                     Select (header => header.Key + ": " + header.Value).
                                                     ForEach(line   => SendCommand(line));

                                        //SendCommand("Message-Id: <" + (EMailEnvelop.Mail.MessageId is not null
                                        //                                    ? EMailEnvelop.Mail.MessageId.ToString()
                                        //                                    : GenerateMessageId(EMailEnvelop.Mail, RemoteHost).ToString()) + ">");

                                        SendCommand("");

                                        // Send e-mail body(parts)...
                                        //if (EMailEnvelop.Mail.MailBody is not null)
                                        //{
                                        EMailEnvelop.Mail.Body.ToText(false).ForEach(line => SendCommand(line));
                                        SendCommand("");
                                        //}

                                    }

                                    else if (EMailEnvelop.Mail?.ToText() is not null) {

                                        EMailEnvelop.Mail.ToText().ForEach(SendCommand);
                                        SendCommand("");

                                    }

                                    #endregion

                                    #region End-of-DATA

                                    // .
                                    // 250 2.0.0 Ok: queued as 83398728027
                                    var _FinishedResponse = SendCommandAndWaitForResponse(".");
                                    if (_FinishedResponse.StatusCode != SMTPStatusCodes.Ok)
                                        throw new SMTPClientException("SMTP DATA '.' command error: " + _FinishedResponse.ToString());

                                    #endregion

                                    #region QUIT

                                    // QUIT
                                    // 221 2.0.0 Bye
                                    var _QuitResponse = SendCommandAndWaitForResponse("QUIT");
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
                catch (Exception e)
                {
                    smtpLogger.LogError(e, "SMTP send failed.");
                    result = MailSentStatus.ExceptionOccurred;
                }
                finally
                {
                    SendEMailSemaphore.Release();
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

            return result;

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
