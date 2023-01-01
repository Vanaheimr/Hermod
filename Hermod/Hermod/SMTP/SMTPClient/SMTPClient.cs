/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// A SMTP client for sending e-mails.
    /// </summary>
    public class SMTPClient : TCPClient, ISMTPClient
    {

        #region Data

        private static readonly Byte[]         ByteZero            = new Byte[1] { 0x00 };

        private static readonly SemaphoreSlim  SendEMailSemaphore  = new (1, 1);

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
        /// <param name="ValidateServerCertificate">A callback for validating the remote server certificate.</param>
        /// <param name="ConnectionTimeout">The timeout connecting to the remote service.</param>
        /// <param name="DNSClient">An optional DNS client used to resolve DNS names.</param>
        /// <param name="AutoConnect">Connect to the TCP service automatically on startup. Default is false.</param>
        /// <param name="CancellationToken"></param>
        public SMTPClient(String                              RemoteHost,
                          IPPort                              RemotePort,
                          String?                             Login                       = null,
                          String?                             Password                    = null,
                          String?                             LocalDomain                 = null,
                          Boolean                             UseIPv4                     = true,
                          Boolean                             UseIPv6                     = false,
                          Boolean                             PreferIPv6                  = false,
                          TLSUsage                            UseTLS                      = TLSUsage.STARTTLS,
                          ValidateRemoteCertificateDelegate?  ValidateServerCertificate   = null,
                          TimeSpan?                           ConnectionTimeout           = null,
                          DNSClient?                          DNSClient                   = null,
                          Boolean                             AutoConnect                 = false,
                          CancellationToken?                  CancellationToken           = null)

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

            this.Login               = Login;
            this.Password            = Password;
            this.LocalDomain         = LocalDomain ?? System.Net.Dns.GetHostName();
            this.UnknownAuthMethods  = new List<String>();

        }

        #endregion


        #region (private) GenerateMessageId(Mail, DomainPart = null)

        private Message_Id GenerateMessageId(EMail    Mail,
                                             String?  DomainPart   = null)
        {

            DomainPart       = DomainPart?.Trim();

            var randomBytes  = new Byte[16];
            Random.Shared.NextBytes(randomBytes);

            var hashedBytes  = SHA256.Create().ComputeHash(randomBytes.
                                                               Concat(Mail.From.   ToString(). ToUTF8Bytes()).
                                                               Concat(Mail.Subject.            ToUTF8Bytes()).
                                                               Concat(Mail.Date.   ToIso8601().ToUTF8Bytes()).
                                                               ToArray());

            return Message_Id.Parse(hashedBytes.ToHexString()[..24],
                                    DomainPart is not null ? DomainPart : RemoteHost);

        }

        #endregion


        #region (protected) SendCommand(Command)

        protected void SendCommand(String Command)
        {

            var CommandBytes = Encoding.UTF8.GetBytes(Command + "\r\n");

            TCPSocket.Poll(SelectMode.SelectWrite, CancellationToken.Value);
            Stream.Write(CommandBytes, 0, CommandBytes.Length);

            DebugX.Log(nameof(SMTPClient) + ": " + Command);

        }

        #endregion

        #region (protected) SendCommandAndWaitForResponse (Command)

        protected SMTPExtendedResponse SendCommandAndWaitForResponse(String Command)
        {

            SendCommand(Command);

            return ReadSMTPResponse();

        }

        #endregion

        #region (protected) SendCommandAndWaitForResponses(Command)

        protected IEnumerable<SMTPExtendedResponse> SendCommandAndWaitForResponses(String Command)
        {

            SendCommand(Command);

            return ReadSMTPResponses();

        }

        #endregion


        #region (private)   ReadSMTPResponse()

        private SMTPExtendedResponse ReadSMTPResponse()
        {

            Byte[] input = Array.Empty<Byte>();

            try
            {

                if (input.Length == 0)
                {

                    input = new Byte[64 * 1024];

                    var nread = 0;

                    TCPSocket.Poll(SelectMode.SelectRead, CancellationToken.Value);

                    if ((nread = Stream.Read(input, 0, input.Length)) == 0)
                        throw new Exception("The SMTP server unexpectedly disconnected.");

                    Array.Resize(ref input, nread);

                }

                var aa   = input.TakeWhile(b => b != ' ' && b != '-').ToArray().ToUTF8String();
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
                    input = Array.Empty<Byte>();

                return new SMTPExtendedResponse(scode,
                                                resp.ToUTF8String().Trim(),
                                                more);

            } catch (Exception e)
            {

                return new SMTPExtendedResponse(SMTPStatusCode.TransactionFailed,
                                                e.Message);

            }

        }

        #endregion

        #region (protected) ReadSMTPResponses()

        protected IEnumerable<SMTPExtendedResponse> ReadSMTPResponses()
        {

            var responseList  = new List<SMTPExtendedResponse>();
            var response      = responseList.AddAndReturnElement(ReadSMTPResponse());

            while (response?.MoreDataAvailable == true) {
                response = responseList.AddAndReturnElement(ReadSMTPResponse());
            }

            return responseList;

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
                DebugX.Log(e, nameof(SMTPClient) + "." + nameof(OnSendEMailRequest));
            }

            #endregion


            var result = MailSentStatus.failed;

            if (await SendEMailSemaphore.WaitAsync(RequestTimeout ?? TimeSpan.FromSeconds(60)))
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

                            var authMethods         = SMTPAuthMethods.None;
                            var unknownAuthMethods  = new HashSet<String>();

                            // 220 mail.ahzf.de ESMTP Postfix (Debian/GNU)
                            var LoginResponse       = ReadSMTPResponse();

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

                                        DebugX.Log("SMTP Auth PLAIN response: " + response.ToString());

                                    }

                                    #endregion

                                    #region ...or Auth LOGIN...

                                    else if (authMethods.HasFlag(SMTPAuthMethods.LOGIN))
                                    {

                                        var response1 = SendCommandAndWaitForResponse("AUTH LOGIN");
                                        var response2 = SendCommandAndWaitForResponse(Convert.ToBase64String(Login.   ToUTF8Bytes()));
                                        var response3 = SendCommandAndWaitForResponse(Convert.ToBase64String(Password.ToUTF8Bytes()));

                                        DebugX.Log(String.Concat("SMTP Auth LOGIN responses: ", response1.ToString(), ", ", response2.ToString(), ", ", response3.ToString()));

                                    }

                                    #endregion

                                    #region ...or AUTH CRAM-MD5

                                    else if (authMethods.HasFlag(SMTPAuthMethods.CRAM_MD5))
                                    {

                                        var AuthCRAMMD5Response = SendCommandAndWaitForResponse("AUTH CRAM-MD5");

                                        if (AuthCRAMMD5Response.StatusCode == SMTPStatusCode.AuthenticationChallenge)
                                        {

                                            var response = SendCommandAndWaitForResponse(
                                                               Convert.ToBase64String(
                                                                   ISMTPClientExtensions.CRAM_MD5(Convert.FromBase64String(AuthCRAMMD5Response.Response).ToUTF8String(),
                                                                                                  Login,
                                                                                                  Password)));

                                            DebugX.Log("SMTP Auth CRAM-MD5 response: " + response.ToString());

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
                                        if (mailFromResponse.StatusCode != SMTPStatusCode.Ok)
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

                                            case SMTPStatusCode.UserNotLocalWillForward:
                                            case SMTPStatusCode.Ok:
                                                break;

                                            case SMTPStatusCode.UserNotLocalTryAlternatePath:
                                            case SMTPStatusCode.MailboxNameNotAllowed:
                                            case SMTPStatusCode.MailboxUnavailable:
                                            case SMTPStatusCode.MailboxBusy:
                                                throw new SMTPClientException        (rcpt.Address.ToString() + " => " + rcptToResponse.StatusCode);

                                            case SMTPStatusCode.AuthenticationRequired:
                                                throw new UnauthorizedAccessException(rcpt.Address.ToString() + " => " + rcptToResponse.StatusCode);

                                            default:
                                                throw new SMTPClientException        (rcpt.Address.ToString() + " => " + rcptToResponse.StatusCode);

                                        }

                                        //Debug.WriteLine(_RcptToResponse);

                                    });

                                    #endregion

                                    #region Mail DATA

                                    // The encoded MIME text lines must not be longer than 76 characters!

                                    // 354 End data with <CR><LF>.<CR><LF>
                                    var dataResponse = SendCommandAndWaitForResponse("DATA");
                                    if (dataResponse.StatusCode != SMTPStatusCode.StartMailInput)
                                        throw new SMTPClientException("SMTP DATA command error: " + dataResponse.ToString());

                                    // Send e-mail headers...
                                    if (EMailEnvelop.Mail is not null) {

                                        EMailEnvelop.Mail.
                                                     Header.
                                                     Select (header => header.Key + ": " + header.Value).
                                                     ForEach(line   => SendCommand(line));

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

                                    else if (EMailEnvelop.Mail?.ToText is not null) {

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
                    DebugX.LogException(e);
                    result = MailSentStatus.ExceptionOccured;
                }
                finally
                {
                    SendEMailSemaphore.Release();
                }
            }


            #region Send OnSendEMailResponse event

            var endtime = Timestamp.Now;

            try
            {

                if (OnSendEMailResponse is not null)
                    await Task.WhenAll(OnSendEMailResponse.GetInvocationList().
                                       Cast<OnSendEMailResponseDelegate>().
                                       Select(e => e(endtime,
                                                     this,
                                                     eventTrackingId,
                                                     EMailEnvelop,
                                                     RequestTimeout,
                                                     result,
                                                     endtime - startTime))).
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
