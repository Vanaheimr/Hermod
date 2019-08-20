/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Diagnostics;
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

    public static class Ext2
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

        private static readonly Random                       _Random     = new Random(DateTime.Now.Millisecond);
        private static readonly SHA256CryptoServiceProvider  _SHAHasher  = new SHA256CryptoServiceProvider();

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

        private MessageId GenerateMessageId(EMail Mail, String DomainPart = null)
        {

            if (DomainPart.IsNullOrEmpty())
                DomainPart = base.RemoteHost;

            var RandomBytes  = new Byte[16];
            _Random.NextBytes(RandomBytes);

            var HashedBytes = _SHAHasher.ComputeHash(RandomBytes.
                                                     Concat(Mail.From.   ToString().ToUTF8Bytes()).
                                                     Concat(Mail.Subject.           ToUTF8Bytes()).
                                                     Concat(Mail.Date.   ToString().ToUTF8Bytes()).
                                                     ToArray());

            return MessageId.Parse(HashedBytes.ToHexString().Substring(0, 24) + "@" + DomainPart);

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

        #region (private) ReadSMTPResponse()

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
                UInt16 _scode;

                if (UInt16.TryParse(aa, out _scode))
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


        #region (protected) SendCommandAndWait(Command)

        protected SMTPExtendedResponse SendCommandAndWait(String Command)
        {

            SendCommand(Command);
            //Debug.WriteLine(">> " + Command);

            return ReadSMTPResponse();

        }

        #endregion

        #region (protected) SendCommandAndWaits(Command)

        protected IEnumerable<SMTPExtendedResponse> SendCommandAndWaits(String Command)
        {

            SendCommand(Command);
            //Debug.WriteLine(">> " + Command);

            return ReadSMTPResponses();

        }

        #endregion


        #region Send(EMail, NumberOfRetries = 3, AutoStart = true)

        public Task<MailSentStatus> Send(EMail    EMail,
                                         Byte     NumberOfRetries  = 3,
                                         Boolean  AutoStart        = true)

            => Send(new EMailEnvelop(EMail),
                    NumberOfRetries,
                    AutoStart);

        #endregion

        #region Send(EMailEnvelop, NumberOfRetries = 3, AutoStart = true)

        public Task<MailSentStatus> Send(EMailEnvelop  EMailEnvelop,
                                         Byte          NumberOfRetries  = 3,
                                         Boolean       AutoStart        = true)
        {

            var SendMailTask = new Task<MailSentStatus>(() => {

                lock (Lock) {

                switch (Connect())
                {

                    case TCPConnectResult.InvalidDomainName:
                        return MailSentStatus.failed;

                    case TCPConnectResult.NoIPAddressFound:
                        return MailSentStatus.failed;

                    case TCPConnectResult.UnknownError:
                        return MailSentStatus.failed;

                    case TCPConnectResult.Ok:

                        // 220 mail.ahzf.de ESMTP Postfix (Debian/GNU)
                        var LoginResponse = this.ReadSMTPResponse();
                        if (LoginResponse.StatusCode != SMTPStatusCode.ServiceReady)
                            throw new SMTPClientException("SMTP login error: " + LoginResponse.ToString());

                        switch (LoginResponse.StatusCode)
                        {

                            case SMTPStatusCode.ServiceReady:

                            #region Send EHLO

                            var EHLOResponses = SendCommandAndWaits("EHLO " + LocalDomain);

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

                                    var StartTLSResponse = SendCommandAndWait("STARTTLS");

                                    if (StartTLSResponse.StatusCode == SMTPStatusCode.ServiceReady)
                                        EnableTLS();

                                }

                                else
                                    throw new Exception("TLS is not supported by the SMTP server!");

                                // Send EHLO again in order to get the new list of supported extensions!
                                EHLOResponses = SendCommandAndWaits("EHLO " + LocalDomain);

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

                            EHLOResponses.Skip(1).ForEach(v => {

                                #region PIPELINING

                                if      (v.Response == "PIPELINING")
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

                                    SMTPAuthMethods ParsedAuthMethod;
                                    var AuthType            = v.Response.Substring(4, 1);
                                    var AuthMethods         = v.Response.Substring(5).Split(' ');

                                    // GMail: "AUTH LOGIN PLAIN XOAUTH XOAUTH2 PLAIN-CLIENTTOKEN"
                                    foreach (var AuthMethod in AuthMethods)
                                    {

                                        if (Enum.TryParse<SMTPAuthMethods>(AuthMethod.Replace('-', '_'), true, out ParsedAuthMethod))
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

                                var AuthPLAINResponse  = SendCommandAndWait("AUTH PLAIN " +
                                                                            Convert.ToBase64String(ByteZero.
                                                                                                   Concat(Login.   ToUTF8Bytes()).
                                                                                                   Concat(ByteZero).
                                                                                                   Concat(Password.ToUTF8Bytes()).
                                                                                                   ToArray()));

                            }

                            #endregion

                            #region ...or Auth LOGIN...

                            else if (_AuthMethods.HasFlag(SMTPAuthMethods.LOGIN))
                            {

                                var AuthLOGIN1Response  = SendCommandAndWait("AUTH LOGIN");
                                var AuthLOGIN2Response  = SendCommandAndWait(Convert.ToBase64String(Login.   ToUTF8Bytes()));
                                var AuthLOGIN3Response  = SendCommandAndWait(Convert.ToBase64String(Password.ToUTF8Bytes()));

                            }

                            #endregion

                            #region ...or AUTH CRAM-MD5

                            else if (_AuthMethods.HasFlag(SMTPAuthMethods.CRAM_MD5))
                            {

                                var AuthCRAMMD5Response = SendCommandAndWait("AUTH CRAM-MD5");

                                if (AuthCRAMMD5Response.StatusCode == SMTPStatusCode.AuthenticationChallenge)
                                {

                                    var aa = Ext2.CRAM_MD5("<1896.697170952@postoffice.reston.mci.net>",
                                                           "tim", "tanstaaftanstaaf");
                                    var a2 = Convert.ToBase64String(aa) == "dGltIGI5MTNhNjAyYzdlZGE3YTQ5NWI0ZTZlNzMzNGQzODkw";


                                    var bb = Ext2.CRAM_MD5("<17893.1320679123@tesseract.susam.in>",
                                                           "alice", "wonderland");
                                    var b2 = Convert.ToBase64String(bb) == "YWxpY2UgNjRiMmE0M2MxZjZlZDY4MDZhOTgwOTE0ZTIzZTc1ZjA=";

                                    var cc = Ext2.CRAM_MD5("<1529645438.10349126@mail.ahzf.de>", "ahzf", "ahzf2305!");

                                    var zz = Ext2.CRAM_MD5(Convert.FromBase64String(AuthCRAMMD5Response.Response).ToUTF8String(),
                                                           Login, Password);

                                    var AuthPLAINResponse = SendCommandAndWait(Convert.ToBase64String(zz));

                                }

                            }

                            #endregion

                            #region MAIL FROM:

                            foreach (var MailFrom in EMailEnvelop.MailFrom)
                            {

                                // MAIL FROM:<test@example.com>
                                // 250 2.1.0 Ok
                                var MailFromCommand = "MAIL FROM: <" + MailFrom.Address.ToString() + ">";

                                if      (Capabilities.HasFlag(SmtpCapabilities.EightBitMime))
                                    MailFromCommand += " BODY=8BITMIME";
                                else if (Capabilities.HasFlag(SmtpCapabilities.BinaryMime))
                                    MailFromCommand += " BODY=BINARYMIME";

                                var _MailFromResponse = SendCommandAndWait(MailFromCommand);
                                if (_MailFromResponse.StatusCode != SMTPStatusCode.Ok)
                                    throw new SMTPClientException("SMTP MAIL FROM command error: " + _MailFromResponse.ToString());

                            }

                            #endregion

                            #region RCPT TO(s):

                            // RCPT TO:<user@example.com>
                            // 250 2.1.5 Ok
                            EMailEnvelop.RcptTo.ForEach(Rcpt => {

                                var _RcptToResponse = SendCommandAndWait("RCPT TO: <" + Rcpt.Address.ToString() + ">");

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
                            var _DataResponse = SendCommandAndWait("DATA");
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
                            var _FinishedResponse = SendCommandAndWait(".");
                            if (_FinishedResponse.StatusCode != SMTPStatusCode.Ok)
                                throw new SMTPClientException("SMTP DATA '.' command error: " + _FinishedResponse.ToString());

                            #endregion

                            #region QUIT

                            // QUIT
                            // 221 2.0.0 Bye
                            var _QuitResponse     = SendCommandAndWait("QUIT");
                            if (_QuitResponse.StatusCode != SMTPStatusCode.ServiceClosingTransmissionChannel)
                                throw new SMTPClientException("SMTP QUIT command error: " + _QuitResponse.ToString());

                            #endregion

                            break;

                        }

                        return MailSentStatus.ok;

                    default:
                        return MailSentStatus.failed;

                }}

            });

            if (AutoStart)
                SendMailTask.Start();

            return SendMailTask;

        }

        #endregion


        #region Dispose()

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion

    }

}
