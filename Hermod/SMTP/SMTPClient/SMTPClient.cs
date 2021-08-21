/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public static class SMTPClientExtentions
    {

        #region CRAM_MD5(Token, Login, Password)

        public static Byte[] CRAM_MD5(String Token, String Login, String Password)
        {

            var HMAC_MD5 = new HMACMD5(Password.ToUTF8Bytes());
            var digest   = HMAC_MD5.ComputeHash(Token.ToUTF8Bytes());

            // result := login[space]digest
            return Login.ToUTF8Bytes().
                   Concat(new Byte[1] { 0x20 }).
                   Concat(digest.ToHexString().ToUTF8Bytes()).
                   ToArray();

        }

        #endregion

        #region CRAM_MD5_(Token, Login, Password)

        public static Byte[] CRAM_MD5_(String Token, String Login, String Password)
        {

            var token       = Token.   ToUTF8Bytes();
            var password    = Password.ToUTF8Bytes();
            var ipad        = new Byte[64];
            var opad        = new Byte[64];
            var startIndex  = 0;
            var length      = token.Length;

            // see also: http://tools.ietf.org/html/rfc2195 - 2. Challenge-Response Authentication Mechanism (CRAM)
            //           http://tools.ietf.org/html/rfc2104 - 2. Definition of HMAC

            #region Copy the password into inner/outer padding and XOR it accordingly

            if (password.Length > ipad.Length)
            {
                var HashedPassword = new MD5CryptoServiceProvider().ComputeHash(password);
                Array.Copy(HashedPassword, ipad, HashedPassword.Length);
                Array.Copy(HashedPassword, opad, HashedPassword.Length);
            }
            else
            {
                Array.Copy(password, ipad, password.Length);
                Array.Copy(password, opad, password.Length);
            }

            for (var i = 0; i < ipad.Length; i++) {
                ipad[i] ^= 0x36;
                opad[i] ^= 0x5c;
            }

            #endregion

            #region Calculate the inner padding

            byte[] digest;

            using (var MD5 = new MD5CryptoServiceProvider())
            {
                MD5.TransformBlock     (ipad, 0, ipad.Length, null, 0);
                MD5.TransformFinalBlock(token, startIndex, length);
                digest = MD5.Hash;
            }

            #endregion

            #region Calculate the outer padding

            // oPAD (will use iPAD digest!)
            using (var MD5 = new MD5CryptoServiceProvider())
            {
                MD5.TransformBlock     (opad, 0, opad.Length, null, 0);
                MD5.TransformFinalBlock(digest, 0, digest.Length);
                digest = MD5.Hash;
            }

            #endregion


            // result := login[space]digest
            return Login.ToUTF8Bytes().
                   Concat(new Byte[1] { 0x20 }).
                   Concat(digest.ToHexString().ToUTF8Bytes()).
                   ToArray();

        }

        #endregion

    }

    /// <summary>
    /// A SMTP client for sending e-mails.
    /// </summary>
    public class SMTPClient : TCPClient
    {

        #region Data

        private readonly Object Lock = new Object();

        private                 Byte[]                       input;
        private static readonly Byte[]                       ByteZero    = new Byte[1] { 0x00 };

        private static readonly Random                       _Random     = new Random();
        private static readonly SHA256CryptoServiceProvider  _SHAHasher  = new SHA256CryptoServiceProvider();

        private static readonly SemaphoreSlim SendEMailSemaphore = new SemaphoreSlim(1, 1);

        #endregion

        #region Properties

        /// <summary>
        /// The local domain is used in the HELO or EHLO commands sent to
        /// the SMTP server. If left unset, the local IP address will be
        /// used instead.
        /// </summary>
        public String LocalDomain   { get; }

        /// <summary>
        /// A login name which can be used for SMTP authentication.
        /// </summary>
        public String Login         { get; }

        /// <summary>
        /// The password for the login name, which both will be used
        /// for SMTP authentication.
        /// </summary>
        public String Password      { get; }


        #region MaxMailSize

        private UInt64 _MaxMailSize;

        public UInt64 MaxMailSize
        {
            get
            {
                return _MaxMailSize;
            }
        }

        #endregion

        #region AuthMethods

        private SMTPAuthMethods _AuthMethods;

        public SMTPAuthMethods AuthMethods
        {
            get
            {
                return _AuthMethods;
            }
        }

        #endregion

        #region UnknownAuthMethods

        private List<String> _UnknownAuthMethods;

        public IEnumerable<String> UnknownAuthMethods
        {
            get
            {
                return _UnknownAuthMethods;
            }
        }

        #endregion

        public SmtpCapabilities Capabilities;

        #endregion

        #region Events

        public delegate Task OnSendEMailRequestDelegate (DateTime                        LogTimestamp,
                                                         SMTPClient                      Sender,
                                                         EventTracking_Id                EventTrackingId,
                                                         EMailEnvelop                    EMailEnvelop,
                                                         TimeSpan?                       RequestTimeout);

        public event OnSendEMailRequestDelegate OnSendEMailRequest;


        public delegate Task OnSendEMailResponseDelegate(DateTime                        LogTimestamp,
                                                         SMTPClient                      Sender,
                                                         EventTracking_Id                EventTrackingId,
                                                         EMailEnvelop                    EMailEnvelop,
                                                         TimeSpan?                       RequestTimeout,
                                                         MailSentStatus                  Result,
                                                         TimeSpan                        Runtime);

        public event OnSendEMailResponseDelegate OnSendEMailResponse;

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
        /// <param name="ValidateServerCertificate">A callback for validating the remote server certificate.</param>
        /// <param name="ConnectionTimeout">The timeout connecting to the remote service.</param>
        /// <param name="DNSClient">An optional DNS client used to resolve DNS names.</param>
        /// <param name="AutoConnect">Connect to the TCP service automatically on startup. Default is false.</param>
        /// <param name="CancellationToken"></param>
        public SMTPClient(String                             RemoteHost,
                          IPPort                             RemotePort,
                          String                             Login                      = null,
                          String                             Password                   = null,
                          String                             LocalDomain                = null,
                          Boolean                            UseIPv4                    = true,
                          Boolean                            UseIPv6                    = false,
                          Boolean                            PreferIPv6                 = false,
                          TLSUsage                           UseTLS                     = TLSUsage.STARTTLS,
                          ValidateRemoteCertificateDelegate  ValidateServerCertificate  = null,
                          TimeSpan?                          ConnectionTimeout          = null,
                          DNSClient                          DNSClient                  = null,
                          Boolean                            AutoConnect                = false,
                          CancellationToken?                 CancellationToken          = null)

            : base(RemoteHost,
                   RemotePort,
                   UseIPv4,
                   UseIPv6,
                   PreferIPv6,
                   UseTLS,
                   ValidateServerCertificate,
                   ConnectionTimeout,
                   DNSClient,
                   AutoConnect,
                   CancellationToken)

        {

            this.Login                = Login;
            this.Password             = Password;
            this.LocalDomain          = LocalDomain ?? System.Net.Dns.GetHostName();
            this._UnknownAuthMethods  = new List<String>();

        }

        #endregion


        #region (private) GenerateMessageId(Mail, DomainPart = null)

        private Message_Id GenerateMessageId(EMail Mail, String DomainPart = null)
        {

            if (DomainPart != null)
                DomainPart = DomainPart.Trim();

            var RandomBytes  = new Byte[16];
            _Random.NextBytes(RandomBytes);

            var HashedBytes = _SHAHasher.ComputeHash(RandomBytes.
                                                     Concat(Mail.From.   ToString(). ToUTF8Bytes()).
                                                     Concat(Mail.Subject.            ToUTF8Bytes()).
                                                     Concat(Mail.Date.   ToIso8601().ToUTF8Bytes()).
                                                     ToArray());

            return Message_Id.Parse(HashedBytes.ToHexString().Substring(0, 24),
                                    DomainPart.IsNeitherNullNorEmpty() ? DomainPart : RemoteHost);

        }

        #endregion


        #region (protected) SendCommand(Command)

        protected void SendCommand(String Command)
        {

            var CommandBytes = Encoding.UTF8.GetBytes(Command + "\r\n");

            TCPSocket.Poll(SelectMode.SelectWrite, CancellationToken.Value);
            Stream.Write(CommandBytes, 0, CommandBytes.Length);

        }

        #endregion

        #region (private)   ReadSMTPResponse()

        private SMTPExtendedResponse ReadSMTPResponse()
        {

            try
            {

                if (input == null || input.Length == 0)
                {

                    input = new Byte[64 * 1024];

                    var nread = 0;

                    TCPSocket.Poll(SelectMode.SelectRead, CancellationToken.Value);

                    if ((nread = Stream.Read(input, 0, input.Length)) == 0)
                        throw new Exception("The SMTP server unexpectedly disconnected.");

                    Array.Resize(ref input, nread);

                }

                var aa = input.TakeWhile(b => b != ' ' && b != '-').ToArray().ToUTF8String();
                var more = input[aa.Length] == '-';
                var resp = input.Skip(aa.Length + 1).
                                 TakeWhile(b => b != '\r' && b != '\n').
                                 ToArray();

                var scode = SMTPStatusCode.SyntaxError;

                if (UInt16.TryParse(aa, out ushort _scode))
                    scode = (SMTPStatusCode)_scode;

                if (more)
                    input = input.Skip(aa.Length + 1 + resp.Length).SkipWhile(b => b == '\r' || b == '\n').
                                  ToArray();
                else
                    input = null;

                return new SMTPExtendedResponse(scode, resp.ToUTF8String().Trim(), more);

            } catch (Exception e)
            {
                return new SMTPExtendedResponse(SMTPStatusCode.TransactionFailed, e.Message);
            }

        }

        #endregion

        #region (protected) ReadSMTPResponses()

        protected IEnumerable<SMTPExtendedResponse> ReadSMTPResponses()
        {

            var ResponseList = new List<SMTPExtendedResponse>();
            SMTPExtendedResponse SR = null;
            do
            {
                SR = this.ReadSMTPResponse();
                ResponseList.Add(SR);
            } while (SR.MoreDataAvailable);

            return ResponseList;

        }

        #endregion


        #region (protected) SendCommandAndWaitForResponse (Command)

        protected SMTPExtendedResponse SendCommandAndWaitForResponse(String Command)
        {

            SendCommand(Command);
            //Debug.WriteLine(">> " + Command);

            return ReadSMTPResponse();

        }

        #endregion

        #region (protected) SendCommandAndWaitForResponses(Command)

        protected IEnumerable<SMTPExtendedResponse> SendCommandAndWaitForResponses(String Command)
        {

            SendCommand(Command);
            //Debug.WriteLine(">> " + Command);

            return ReadSMTPResponses();

        }

        #endregion


        #region Send(EMail,        NumberOfRetries = 3, RequestTimeout = null)

        public Task<MailSentStatus> Send(EMail      EMail,
                                         Byte       NumberOfRetries  = 3,
                                         TimeSpan?  RequestTimeout   = null)

        {

            if (EMail is null)
                throw new ArgumentNullException(nameof(EMail), "The given e-mail must not be null!");

            return Send(new EMailEnvelop(EMail),
                        NumberOfRetries,
                        RequestTimeout);

        }

        #endregion

        #region Send(EMailEnvelop, NumberOfRetries = 3, RequestTimeout = null)

        public async Task<MailSentStatus> Send(EMailEnvelop  EMailEnvelop,
                                               Byte          NumberOfRetries  = 3,
                                               TimeSpan?     RequestTimeout   = null)
        {

            #region Initial checks

            if (EMailEnvelop is null)
                throw new ArgumentNullException(nameof(EMailEnvelop), "The given e-mail envelop must not be null!");

            var result = MailSentStatus.failed;

            #endregion

            #region Send OnSendEMailRequest event

            var StartTime = DateTime.UtcNow;

            try
            {

                if (OnSendEMailRequest != null)
                    await Task.WhenAll(OnSendEMailRequest.GetInvocationList().
                                        Cast<OnSendEMailRequestDelegate>().
                                        Select(e => e(StartTime,
                                                      this,
                                                      EMailEnvelop.EventTrackingId,
                                                      EMailEnvelop,
                                                      RequestTimeout))).
                                        ConfigureAwait(false);

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(SMTPClient) + "." + nameof(OnSendEMailRequest));
            }

            #endregion


            var success = await SendEMailSemaphore.WaitAsync(RequestTimeout ?? TimeSpan.FromSeconds(60));

            if (success)
            {
                try
                {

                    switch (Connect())
                    {

                        case TCPConnectResult.InvalidRemoteHost:
                            result = MailSentStatus.InvalidRemoteHost;
                            break;

                        case TCPConnectResult.InvalidDomainName:
                            result = MailSentStatus.InvalidDomainName;
                            break;

                        case TCPConnectResult.NoIPAddressFound:
                            result = MailSentStatus.NoIPAddressFound;
                            break;

                        case TCPConnectResult.UnknownError:
                            result = MailSentStatus.UnknownError;
                            break;

                        case TCPConnectResult.Ok:

                            // 220 mail.ahzf.de ESMTP Postfix (Debian/GNU)
                            var LoginResponse = ReadSMTPResponse();

                            switch (LoginResponse.StatusCode)
                            {

                                case SMTPStatusCode.ServiceReady:

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

                                    if (EHLOResponses.Any(v => v.StatusCode != SMTPStatusCode.Ok))
                                    {

                                        var Error = EHLOResponses.Where(v => v.StatusCode != SMTPStatusCode.Ok).
                                                                    FirstOrDefault();

                                        if (Error.StatusCode != SMTPStatusCode.Ok)
                                            throw new SMTPClientException("SMTP EHLO command error: " + Error.ToString());

                                    }

                                    #endregion

                                    #region Check for STARTTLS

                                    if (UseTLS == TLSUsage.STARTTLS)
                                    {

                                        if (EHLOResponses.Any(v => v.Response == "STARTTLS"))
                                        {

                                            var StartTLSResponse = SendCommandAndWaitForResponse("STARTTLS");

                                            if (StartTLSResponse.StatusCode == SMTPStatusCode.ServiceReady)
                                                EnableTLS();

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

                                            if (!UInt64.TryParse(v.Response.Substring(5), out _MaxMailSize))
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

                                            var AuthType    = v.Response.Substring(4, 1);
                                            var AuthMethods = v.Response.Substring(5).Split(' ');

                                            // GMail: "AUTH LOGIN PLAIN XOAUTH XOAUTH2 PLAIN-CLIENTTOKEN"
                                            foreach (var AuthMethod in AuthMethods)
                                            {

                                                if (Enum.TryParse(AuthMethod.Replace('-', '_'), true, out SMTPAuthMethods ParsedAuthMethod))
                                                {
                                                    if (AuthType == " ")
                                                        _AuthMethods |= ParsedAuthMethod;
                                                }
                                                else
                                                    _UnknownAuthMethods.Add(AuthMethod);

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

                                    if (_AuthMethods.HasFlag(SMTPAuthMethods.PLAIN))
                                    {

                                        var response = SendCommandAndWaitForResponse("AUTH PLAIN " +
                                                                                     Convert.ToBase64String(ByteZero.
                                                                                                            Concat(Login.ToUTF8Bytes()).
                                                                                                            Concat(ByteZero).
                                                                                                            Concat(Password.ToUTF8Bytes()).
                                                                                                            ToArray()));

                                        DebugX.Log("SMTP Auth PLAIN response: " + response.ToString());

                                    }

                                    #endregion

                                    #region ...or Auth LOGIN...

                                    else if (_AuthMethods.HasFlag(SMTPAuthMethods.LOGIN))
                                    {

                                        var response1 = SendCommandAndWaitForResponse("AUTH LOGIN");
                                        var response2 = SendCommandAndWaitForResponse(Convert.ToBase64String(Login.   ToUTF8Bytes()));
                                        var response3 = SendCommandAndWaitForResponse(Convert.ToBase64String(Password.ToUTF8Bytes()));

                                        DebugX.Log(String.Concat("SMTP Auth LOGIN responses: ", response1.ToString(), ", ", response2.ToString(), ", ", response3.ToString()));

                                    }

                                    #endregion

                                    #region ...or AUTH CRAM-MD5

                                    else if (_AuthMethods.HasFlag(SMTPAuthMethods.CRAM_MD5))
                                    {

                                        var AuthCRAMMD5Response = SendCommandAndWaitForResponse("AUTH CRAM-MD5");

                                        if (AuthCRAMMD5Response.StatusCode == SMTPStatusCode.AuthenticationChallenge)
                                        {

                                            var response = SendCommandAndWaitForResponse(
                                                               Convert.ToBase64String(
                                                                   SMTPClientExtentions.CRAM_MD5(Convert.FromBase64String(AuthCRAMMD5Response.Response).ToUTF8String(),
                                                                                                 Login,
                                                                                                 Password)));

                                            DebugX.Log("SMTP Auth CRAM-MD5 response: " + response.ToString());

                                        }

                                    }

                                    #endregion

                                    #region MAIL FROM:

                                    foreach (var MailFrom in EMailEnvelop.MailFrom)
                                    {

                                        // MAIL FROM:<test@example.com>
                                        // 250 2.1.0 Ok
                                        var MailFromCommand = "MAIL FROM: <" + MailFrom.Address.ToString() + ">";

                                        if (Capabilities.HasFlag(SmtpCapabilities.EightBitMime))
                                            MailFromCommand += " BODY=8BITMIME";
                                        else if (Capabilities.HasFlag(SmtpCapabilities.BinaryMime))
                                            MailFromCommand += " BODY=BINARYMIME";

                                        var _MailFromResponse = SendCommandAndWaitForResponse(MailFromCommand);
                                        if (_MailFromResponse.StatusCode != SMTPStatusCode.Ok)
                                            throw new SMTPClientException("SMTP MAIL FROM command error: " + _MailFromResponse.ToString());

                                    }

                                    #endregion

                                    #region RCPT TO(s):

                                    // RCPT TO:<user@example.com>
                                    // 250 2.1.5 Ok
                                    EMailEnvelop.RcptTo.ForEach(Rcpt =>
                                    {

                                        var _RcptToResponse = SendCommandAndWaitForResponse("RCPT TO: <" + Rcpt.Address.ToString() + ">");

                                        switch (_RcptToResponse.StatusCode)
                                        {

                                            case SMTPStatusCode.UserNotLocalWillForward:
                                            case SMTPStatusCode.Ok:
                                                break;

                                            case SMTPStatusCode.UserNotLocalTryAlternatePath:
                                            case SMTPStatusCode.MailboxNameNotAllowed:
                                            case SMTPStatusCode.MailboxUnavailable:
                                            case SMTPStatusCode.MailboxBusy:
                                            //    throw new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, _RcptToResponse.StatusCode, mailbox, _RcptToResponse.Response);

                                            case SMTPStatusCode.AuthenticationRequired:
                                                throw new UnauthorizedAccessException(_RcptToResponse.Response);

                                                //default:
                                                //    throw new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, _RcptToResponse.StatusCode, _RcptToResponse.Response);

                                        }

                                        //Debug.WriteLine(_RcptToResponse);

                                    });

                                    #endregion

                                    #region Mail DATA

                                    // The encoded MIME text lines must not be longer than 76 characters!

                                    // 354 End data with <CR><LF>.<CR><LF>
                                    var _DataResponse = SendCommandAndWaitForResponse("DATA");
                                    if (_DataResponse.StatusCode != SMTPStatusCode.StartMailInput)
                                        throw new SMTPClientException("SMTP DATA command error: " + _DataResponse.ToString());

                                    // Send e-mail headers...
                                    if (EMailEnvelop.Mail != null)
                                    {

                                        EMailEnvelop.Mail.
                                                        Header.
                                                        Select(header => header.Key + ": " + header.Value).
                                                        ForEach(line => SendCommand(line));

                                        //SendCommand("Message-Id: <" + (EMailEnvelop.Mail.MessageId != null
                                        //                                    ? EMailEnvelop.Mail.MessageId.ToString()
                                        //                                    : GenerateMessageId(EMailEnvelop.Mail, RemoteHost).ToString()) + ">");

                                        SendCommand("");

                                        // Send e-mail body(parts)...
                                        //if (EMailEnvelop.Mail.MailBody != null)
                                        //{
                                        EMailEnvelop.Mail.Body.ToText(false).ForEach(line => SendCommand(line));
                                        SendCommand("");
                                        //}

                                    }

                                    else if (EMailEnvelop.Mail.ToText != null)
                                    {
                                        EMailEnvelop.Mail.ToText.ForEach(line => SendCommand(line));
                                        SendCommand("");
                                    }

                                    #endregion

                                    #region End-of-DATA

                                    // .
                                    // 250 2.0.0 Ok: queued as 83398728027
                                    var _FinishedResponse = SendCommandAndWaitForResponse(".");
                                    if (_FinishedResponse.StatusCode != SMTPStatusCode.Ok)
                                        throw new SMTPClientException("SMTP DATA '.' command error: " + _FinishedResponse.ToString());

                                    #endregion

                                    #region QUIT

                                    // QUIT
                                    // 221 2.0.0 Bye
                                    var _QuitResponse = SendCommandAndWaitForResponse("QUIT");
                                    if (_QuitResponse.StatusCode != SMTPStatusCode.ServiceClosingTransmissionChannel)
                                        throw new SMTPClientException("SMTP QUIT command error: " + _QuitResponse.ToString());

                                    #endregion

                                    result = MailSentStatus.ok;

                                    break;

                                default:
                                    result = MailSentStatus.InvalidLogin;
                                    break;

                            }

                            break;

                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    result = MailSentStatus.ExceptionOccured;
                }
                finally
                {
                    SendEMailSemaphore.Release();
                }
            }

            #region Send OnSendEMailResponse event

            var Endtime = DateTime.UtcNow;

            try
            {

                if (OnSendEMailResponse != null)
                    await Task.WhenAll(OnSendEMailResponse.GetInvocationList().
                                       Cast<OnSendEMailResponseDelegate>().
                                       Select(e => e(Endtime,
                                                     this,
                                                     EMailEnvelop.EventTrackingId,
                                                     EMailEnvelop,
                                                     RequestTimeout,
                                                     result,
                                                     Endtime - StartTime))).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(SMTPClient) + "." + nameof(OnSendEMailResponse));
            }

            #endregion

            return result;

        }

        #endregion


        #region Dispose()

        public void Dispose()
        {
            
        }

        #endregion

    }

}
