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

using System.Security.Authentication;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Services;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public class MAIL_FROM_FilterResponse
    {

        public readonly Boolean  Forward;
        public readonly String   Description;

        public MAIL_FROM_FilterResponse(Boolean  Forward,
                                        String   Description)
        {

            this.Forward      = Forward;
            this.Description  = Description;

        }

        public static MAIL_FROM_FilterResponse Allowed
        {
            get
            {
                return new MAIL_FROM_FilterResponse(true, "Sender ok");
            }
        }

        public static MAIL_FROM_FilterResponse Denied
        {
            get
            {
                return new MAIL_FROM_FilterResponse(false, "Access denied");
            }
        }

    }

    public class RCPT_TO_FilterResponse
    {

        public readonly Boolean Forward;
        public readonly String Description;

        public RCPT_TO_FilterResponse(Boolean Forward,
                                      String Description)
        {

            this.Forward = Forward;
            this.Description = Description;

        }

        public static RCPT_TO_FilterResponse Allowed
        {
            get
            {
                return new RCPT_TO_FilterResponse(true, "Recipient ok");
            }
        }

        public static RCPT_TO_FilterResponse Denied
        {
            get
            {
                return new RCPT_TO_FilterResponse(false, "Access denied");
            }
        }

        public static RCPT_TO_FilterResponse RelayDenied
        {
            get
            {
                return new RCPT_TO_FilterResponse(false, "Relay access denied");
            }
        }

    }


    public delegate void NewSMTPConnectionHandler      (SMTPServer SMTPServer, DateTimeOffset Timestamp, IPSocket RemoteSocket, TCPConnection TCPConnection);
    public delegate void IncomingEMailEnvelopeHandler  (SMTPServer SMTPServer, IEnumerable<String> MAIL_FROM, IEnumerable<String> RCPT_TO);
    public delegate void SMTPMailEnvelopReceivedHandler(EventTracking_Id EventTrackingId, SMTPServer SMTPServer, EMailEnvelop MailEnvelop);


    public static class SMTPServerExtensions
    {

        public static void WriteLineSMTP(this TCPConnection TCPConn, SMTPStatusCodes StatusCode, params String[] Response)
        {

            var n = (UInt64) Response.Where(line => line.IsNotNullOrEmpty()).Count();

            Response.
                Where(line => line.IsNotNullOrEmpty()).
                ForEachCounted((response, i) => {
                    TCPConn.WriteLineToResponseStream(((Int32) StatusCode) + (i < n ? "-" : " ") + response);
                });

            TCPConn.Flush();

        }

    }


    /// <summary>
    /// A SMTP server.
    /// </summary>
    public partial class SMTPServer : ATCPServer
    {

        #region Data

        internal const    String             __DefaultServerName  = "Vanaheimr Hermod SMTP Service v0.1";

        private readonly  ILogger<SMTPServer>  smtpLogger;

        private const UInt32 ReadTimeout = 180000U;

        #endregion

        #region Properties

        #region DefaultServerName

        private String _DefaultServerName;

        /// <summary>
        /// The default SMTP server name.
        /// </summary>
        public String DefaultServerName
        {

            get
            {
                return _DefaultServerName;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                    _DefaultServerName = value;
            }

        }

        #endregion

        public Boolean AllowStartTLS { get; }

        #endregion

        #region Events

        public delegate  MAIL_FROM_FilterResponse MAIL_FROM_FilterHandler(SMTPServer SMTPServer, String MAIL_FROM);
        public delegate  RCPT_TO_FilterResponse   RCPT_TO_FilterHandler  (SMTPServer SMTPServer, String RCPT_TO);

        public event     NewSMTPConnectionHandler          OnNewConnection;

        public event     MAIL_FROM_FilterHandler           MAIL_FROMFilter;
        public event     RCPT_TO_FilterHandler             RCPT_TOFilter;
        public event     IncomingEMailEnvelopeHandler      OnIncomingEMailEnvelope;

        public event     SMTPMailEnvelopReceivedHandler?   OnEMailEnvelopReceived;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SMTP server.
        /// </summary>
        /// <param name="IPAddress"></param>
        /// <param name="TCPPort"></param>
        /// <param name="HTTPServerName"></param>
        /// <param name="BufferSize"></param>
        /// <param name="ReceiveTimeout"></param>
        /// <param name="SendTimeout"></param>
        /// <param name="LoggingHandler"></param>
        /// <param name="AllowStartTLS">Allow to start TLS via the 'STARTTLS' SMTP command.</param>
        /// <param name="ServerCertificateSelector"></param>
        /// <param name="ClientCertificateValidator"></param>
        /// <param name="LocalCertificateSelector"></param>
        /// <param name="AllowedTLSProtocols"></param>
        /// <param name="ClientCertificateRequired"></param>
        /// <param name="CheckCertificateRevocation"></param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="MaxClientConnections"></param>
        /// <param name="DNSClient"></param>
        /// <param name="DisableMaintenanceTasks"></param>
        /// <param name="MaintenanceInitialDelay"></param>
        /// <param name="MaintenanceEvery"></param>
        /// <param name="DisableWardenTasks"></param>
        /// <param name="WardenInitialDelay"></param>
        /// <param name="WardenCheckEvery"></param>
        /// <param name="Description"></param>
        /// <param name="LoggerFactory"></param>
        /// <param name="AutoStart"></param>
        public SMTPServer(IIPAddress?                                               IPAddress                    = null,
                          IPPort?                                                   TCPPort                      = null,
                          String?                                                   DefaultServerName            = __DefaultServerName,
                          UInt32?                                                   BufferSize                   = null,
                          TimeSpan?                                                 ReceiveTimeout               = null,
                          TimeSpan?                                                 SendTimeout                  = null,
                          TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                          Boolean?                                                  AllowStartTLS                = true,

                          ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                          RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                          LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                          SslProtocols?                                             AllowedTLSProtocols          = null,
                          Boolean?                                                  ClientCertificateRequired    = null,
                          Boolean?                                                  CheckCertificateRevocation   = null,

                          ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                          UInt32?                                                   MaxClientConnections         = null,
                          IDNSClient?                                               DNSClient                    = null,

                          Boolean?                                                  DisableMaintenanceTasks      = false,
                          TimeSpan?                                                 MaintenanceInitialDelay      = null,
                          TimeSpan?                                                 MaintenanceEvery             = null,

                          Boolean?                                                  DisableWardenTasks           = false,
                          TimeSpan?                                                 WardenInitialDelay           = null,
                          TimeSpan?                                                 WardenCheckEvery             = null,

                          String?                                                   Description                  = null,
                          ILoggerFactory?                                           LoggerFactory                = null,
                          Boolean?                                                  AutoStart                    = false)

            : base(IPAddress,
                   TCPPort,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ConnectionIdBuilder,
                   MaxClientConnections,
                   DNSClient,

                   DisableMaintenanceTasks,
                   MaintenanceInitialDelay,
                   MaintenanceEvery,

                   DisableWardenTasks,
                   WardenInitialDelay,
                   WardenCheckEvery,

                   Description,
                   LoggerFactory,
                   AutoStart: false)

        {

            this._DefaultServerName          = DefaultServerName ?? __DefaultServerName;
            this.AllowStartTLS               = AllowStartTLS     ?? (ServerCertificateSelector is not null);
            this.smtpLogger                  = (LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<SMTPServer>();
            base.ClientCertificateValidator  = ClientCertificateValidator is not null
                                                   ? (sender,
                                                      certificate,
                                                      certificateChain,
                                                      tlsServer,
                                                      policyErrors) => ClientCertificateValidator.Invoke(
                                                                            sender,
                                                                            certificate,
                                                                            certificateChain,
                                                                            this,
                                                                            policyErrors
                                                                        )
                                                   : null;

            if (AutoStart ?? false)
                Start().GetAwaiter().GetResult();

        }

        #endregion


        #region HandleConnection(Connection, Token)

        protected override Task HandleConnection(TCPConnection      Connection,
                                                 CancellationToken  Token)
        {
            ProcessTCPConnection(Connection);
            return Task.CompletedTask;
        }

        #endregion

        // Events

        private Task ProcessTCPServerOnNewConnection(ITCPServer        TCPServer,
                                                     DateTimeOffset    Timestamp,
                                                     EventTracking_Id  EventTrackingId,
                                                     IPSocket          RemoteSocket,
                                                     String            ConnectionId,
                                                     TCPConnection     TCPConnection)
        {

            OnNewConnection?.Invoke(this, Timestamp, RemoteSocket, TCPConnection);

            return Task.CompletedTask;

        }


        private MAIL_FROM_FilterResponse Process_MAIL_FROMFilter(String MAIL_FROM)
        {

            var MAIL_FROMFilterLocal = MAIL_FROMFilter;
            if (MAIL_FROMFilterLocal is not null)
                return MAIL_FROMFilterLocal(this, MAIL_FROM);

            return null;

        }

        private RCPT_TO_FilterResponse Process_RCPT_TOFilter(String RCPT_TO)
        {

            var RCPT_TOFilterLocal = RCPT_TOFilter;
            if (RCPT_TOFilterLocal is not null)
                return RCPT_TOFilterLocal(this, RCPT_TO);

            return null;

        }

        #region NotifyErrors(...)

        private void NotifyErrors(TCPConnection          TCPConnection,
                                  DateTimeOffset         Timestamp,
                                  String                 SMTPCommand,
                                  SMTPStatusCodes         SMTPStatusCode,
                                  EMail?                 EMail             = null,
                                  SMTPExtendedResponse?  Response          = null,
                                  String?                Error             = null,
                                  Exception?             LastException     = null,
                                  Boolean                CloseConnection   = true)
        {

            if (LastException is not null)
            {
                smtpLogger.LogWarning(
                    LastException,
                    "SMTP error from {RemoteSocket} on connection {ConnectionId}: {StatusCode} ({StatusCodeNumber}) while processing '{SMTPCommand}'. Error={Error}; Response={Response}; EMail={EMail}; CloseConnection={CloseConnection}; Timestamp={Timestamp}",
                    TCPConnection.RemoteSocket,
                    TCPConnection.ConnectionId,
                    SMTPStatusCode,
                    (Int32) SMTPStatusCode,
                    SMTPCommand,
                    Error,
                    Response,
                    EMail,
                    CloseConnection,
                    Timestamp
                );
            }
            else
            {
                smtpLogger.LogWarning(
                    "SMTP error from {RemoteSocket} on connection {ConnectionId}: {StatusCode} ({StatusCodeNumber}) while processing '{SMTPCommand}'. Error={Error}; Response={Response}; EMail={EMail}; CloseConnection={CloseConnection}; Timestamp={Timestamp}",
                    TCPConnection.RemoteSocket,
                    TCPConnection.ConnectionId,
                    SMTPStatusCode,
                    (Int32) SMTPStatusCode,
                    SMTPCommand,
                    Error,
                    Response,
                    EMail,
                    CloseConnection,
                    Timestamp
                );
            }

        }

        #endregion

        #region ProcessTCPConnection(TCPConnection)

        private void ProcessTCPConnection(TCPConnection TCPConnection)
        {

            #region Start

            ProcessTCPServerOnNewConnection(
                TCPConnection.TCPServer,
                Timestamp.Now,
                EventTracking_Id.New,
                TCPConnection.RemoteSocket,
                TCPConnection.ConnectionId,
                TCPConnection
            );

            //TCPConnection.WriteLineToResponseStream(ServiceBanner);
            TCPConnection.NoDelay = true;

            Byte Byte;
            var MemoryStream      = new MemoryStream();
            var EndOfSMTPCommand  = EOLSearch.NotYet;
            var ClientClose       = false;
            var ServerClose       = false;
            var MailClientName    = "";
            var TLSEnabled        = false;

            #endregion

            try
            {

                var MailFroms  = EMailAddressListBuilder.Empty;
                var RcptTos    = EMailAddressListBuilder.Empty;

                TCPConnection.WriteLineSMTP(SMTPStatusCodes.ServiceReady,
                                            DefaultServerName + " ESMTP Vanaheimr Hermod Mail Transport Service");

                do
                {

                    switch (TCPConnection.TryRead(out Byte, MaxInitialWaitingTimeMS: ReadTimeout))
                    {

                        // 421 4.4.2 mail.ahzf.de Error: timeout exceeded

                        #region DataAvailable

                        case TCPClientResponse.DataAvailable:

                            #region Check for end of SMTP line...

                            if (EndOfSMTPCommand == EOLSearch.NotYet)
                            {
                                // \n
                                if (Byte == 0x0a)
                                    EndOfSMTPCommand = EOLSearch.EoL_Found;
                                // \r
                                else if (Byte == 0x0d)
                                    EndOfSMTPCommand = EOLSearch.R_Read;
                            }

                            // \n after a \r
                            else if (EndOfSMTPCommand == EOLSearch.R_Read)
                            {
                                if (Byte == 0x0a)
                                    EndOfSMTPCommand = EOLSearch.EoL_Found;
                                else
                                    EndOfSMTPCommand = EOLSearch.NotYet;
                            }

                            #endregion

                            MemoryStream.WriteByte(Byte);

                            #region If end-of-line -> process data...

                            if (EndOfSMTPCommand == EOLSearch.EoL_Found)
                            {

                                if (MemoryStream.Length > 0)
                                {

                                    var RequestTimestamp = Timestamp.Now;

                                    #region Check UTF8 encoding

                                    var SMTPCommand = String.Empty;

                                    try
                                    {

                                        SMTPCommand = Encoding.UTF8.GetString(MemoryStream.ToArray()).Trim();

                                        smtpLogger.LogTrace("SMTP command from {RemoteSocket}: {SMTPCommand}",
                                                            TCPConnection.RemoteSocket,
                                                            SMTPCommand);

                                    }
                                    catch (Exception e)
                                    {

                                        NotifyErrors(TCPConnection,
                                                     RequestTimestamp,
                                                     "",
                                                     SMTPStatusCodes.SyntaxError,
                                                     Error: "Protocol Error: Invalid UTF8 encoding!");

                                    }

                                    #endregion

                                    #region Try to parse SMTP commands

                                    #region ""

                                    if (SMTPCommand == "")
                                    { }

                                    #endregion

                                    #region HELO <MailClientName>

                                    else if (SMTPCommand.ToUpper().StartsWith("HELO"))
                                    {

                                        if (SMTPCommand.Trim().Length > 5 && SMTPCommand.Trim()[4] == ' ')
                                        {

                                            MailClientName = SMTPCommand.Trim().Substring(5);

                                            // 250 mail.ahzf.de
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.Ok, DefaultServerName);

                                        }
                                        else
                                        {
                                            // 501 Syntax: HELO hostname
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.SyntaxError, "Syntax: HELO hostname");
                                        }

                                    }

                                    #endregion

                                    #region EHLO <MailClientName>

                                    else if (SMTPCommand.ToUpper().StartsWith("EHLO"))
                                    {

                                        if (SMTPCommand.Trim().Length > 5 && SMTPCommand.Trim()[4] == ' ')
                                        {

                                            MailClientName = SMTPCommand.Trim().Substring(5);

                                            // 250-mail.graphdefined.org
                                            // 250-PIPELINING
                                            // 250-SIZE 204800000
                                            // 250-VRFY
                                            // 250-ETRN
                                            // 250-STARTTLS
                                            // 250-AUTH PLAIN LOGIN CRAM-MD5 DIGEST-MD5
                                            // 250-AUTH=PLAIN LOGIN CRAM-MD5 DIGEST-MD5
                                            // 250-ENHANCEDSTATUSCODES
                                            // 250-8BITMIME
                                            // 250 DSN
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.Ok,
                                                                        DefaultServerName,
                                                                        "VRFY",
                                                                        AllowStartTLS ? "STARTTLS"         : null,
                                                                        TLSEnabled   ? "AUTH PLAIN LOGIN" : null,
                                                                        "SIZE 204800000",
                                                                        "ENHANCEDSTATUSCODES",
                                                                        "8BITMIME");

                                        }
                                        else
                                        {
                                            // 501 Syntax: EHLO hostname
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.SyntaxError, "Syntax: EHLO hostname");
                                        }

                                    }

                                    #endregion

                                    #region STARTTLS

                                    else if (SMTPCommand.ToUpper() == "STARTTLS")
                                    {

                                        if (TLSEnabled)
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.BadCommandSequence, "5.5.1 TLS already started");

                                        else if (MailClientName.IsNullOrEmpty())
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.BadCommandSequence, "5.5.1 EHLO/HELO first");

                                        else
                                        {

                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.ServiceReady, "2.0.0 Ready to start TLS");

                                            //                                            var _TLSStream = new SslStream(TCPConnection.NetworkStream);
                                            //                                            _TLSStream.AuthenticateAsServer(TLSCert, false, SslProtocols.Tls12, false);
                                            TLSEnabled = true;

                                        }

                                    }

                                    #endregion

                                    #region AUTH LOGIN|PLAIN|...

                                    else if (SMTPCommand.ToUpper().StartsWith("AUTH "))
                                    {

                                        if (!TLSEnabled)
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.BadCommandSequence, "5.5.1 STARTTLS first");

                                    }

                                    #endregion

                                    #region MAIL FROM: <SenderMailAddress>

                                    else if (SMTPCommand.ToUpper().StartsWith("MAIL FROM"))
                                    {

                                        var SMTPCommandParts = SMTPCommand.Split(new Char[2] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);

                                        if (SMTPCommandParts.Length >= 3)
                                        {

                                            var MailFrom = SMTPCommandParts[2];

                                            if (MailFrom[0] == '<' && MailFrom[MailFrom.Length - 1] == '>')
                                                MailFrom = MailFrom.Substring(1, MailFrom.Length - 2);

                                            MAIL_FROM_FilterResponse _SMTPFilterResponse = null;

                                            _SMTPFilterResponse = Process_MAIL_FROMFilter(MailFrom);

                                            if (_SMTPFilterResponse is null)
                                            {
                                                TCPConnection.WriteLineSMTP(SMTPStatusCodes.Ok, "2.1.0 " + MailFrom + " Sender ok");
                                                MailFroms.Add(EMailAddress.Parse(MailFrom));
                                            }

                                            else if (_SMTPFilterResponse.Forward)
                                            {
                                                TCPConnection.WriteLineSMTP(SMTPStatusCodes.Ok, "2.1.0 " + MailFrom + " " + _SMTPFilterResponse.Description);
                                                MailFroms.Add(EMailAddress.Parse(MailFrom));
                                            }

                                            else
                                                TCPConnection.WriteLineSMTP(SMTPStatusCodes.TransactionFailed, "5.7.1 " + _SMTPFilterResponse.Description);

                                        }
                                        else
                                        {
                                            // 501 Syntax: EHLO hostname
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.SyntaxError, "Syntax: MAIL FROM: <mail@domain.tld>");
                                        }

                                    }

                                    #endregion

                                    #region RCPT TO: <ReceiverMailAddress>

                                    else if (SMTPCommand.ToUpper().StartsWith("RCPT TO"))
                                    {

                                        var SMTPCommandParts = SMTPCommand.Split(new Char[2] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);

                                        if (SMTPCommandParts.Length >= 3)
                                        {

                                            var RcptTo = SMTPCommandParts[2];

                                            // telnet: > telnet mx1.example.com smtp
                                            // telnet: Trying 192.0.2.2...
                                            // telnet: Connected to mx1.example.com.
                                            // telnet: Escape character is '^]'.
                                            // server: 220 mx1.example.com ESMTP server ready Tue, 20 Jan 2004 22:33:36 +0200
                                            // client: HELO client.example.com
                                            // server: 250 mx1.example.com
                                            // client: MAIL from: <sender@example.com>
                                            // server: 250 Sender <sender@example.com> Ok
                                            //         250 2.1.0 Ok
                                            // client: RCPT to: <recipient@example.com>
                                            // server: 250 Recipient <recipient@example.com> Ok

                                            // server: 554 5.7.1 <recipient@example.com>: Relay access denied

                                            // client: DATA
                                            // server: 354 Ok Send data ending with <CRLF>.<CRLF>
                                            // client: From: sender@example.com
                                            // client: To: recipient@example.com
                                            // client: Subject: Test message
                                            // client: 
                                            // client: This is a test message.
                                            // client: .
                                            // server: 250 Message received: 20040120203404.CCCC18555.mx1.example.com@client.example.com
                                            // client: QUIT
                                            // server: 221 mx1.example.com ESMTP server closing connection

                                            // MAIL FROM: mail@domain.ext
                                            // 250 2.1.0 mail@domain.ext... Sender ok
                                            // 
                                            // RCPT TO: mail@otherdomain.ext
                                            // 250 2.1.0 mail@otherdomain.ext... Recipient ok

                                            if (RcptTo[0] == '<' && RcptTo[RcptTo.Length - 1] == '>')
                                                RcptTo = RcptTo.Substring(1, RcptTo.Length - 2);

                                            RCPT_TO_FilterResponse _SMTPFilterResponse = null;

                                            _SMTPFilterResponse = Process_RCPT_TOFilter(RcptTo);

                                            if (_SMTPFilterResponse is null)
                                            {
                                                TCPConnection.WriteLineSMTP(SMTPStatusCodes.Ok, "2.1.0 " + RcptTo + " Recipient ok");
                                                RcptTos.Add(EMailAddress.Parse(RcptTo));
                                            }

                                            else if (_SMTPFilterResponse.Forward) {
                                                TCPConnection.WriteLineSMTP(SMTPStatusCodes.Ok, "2.1.0 " + RcptTo + " " + _SMTPFilterResponse.Description);
                                                RcptTos.Add(EMailAddress.Parse(RcptTo));
                                            }

                                            else
                                                TCPConnection.WriteLineSMTP(SMTPStatusCodes.TransactionFailed, "5.7.1 " + _SMTPFilterResponse.Description);

                                        }
                                        else
                                        {
                                            // 501 Syntax: EHLO hostname
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.SyntaxError, "Syntax: RCPT TO: <mail@domain.tld>");
                                        }

                                    }

                                    #endregion

                                    #region DATA

                                    else if (SMTPCommand.ToUpper().StartsWith("DATA"))
                                    {

                                        if (MailFroms.Length == 0 || RcptTos.Length == 0)
                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.BadCommandSequence, "Bad command sequence!");

                                        else
                                        {

                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.StartMailInput, "Ok Send data ending with <CRLF>.<CRLF>");

                                            #region Read all e-mail lines...

                                            var MailText  = new List<String>();
                                            var MailLine  = "";

                                            do
                                            {

                                                MailLine = TCPConnection.ReadLine();

                                                // "." == End-of-EMail...
                                                if (MailLine is not null && MailLine != ".")
                                                {
                                                    MailText.Add(MailLine);
                                                    smtpLogger.LogTrace("SMTP DATA line from {RemoteSocket}: {MailLine}",
                                                                        TCPConnection.RemoteSocket,
                                                                        MailLine);
                                                }

                                            } while (MailLine != ".");

                                            #endregion

                                            #region Try to parse the incoming e-mail...

                                            EMail IncomingMail = null;

                                            try
                                            {
                                                IncomingMail = EMail.Parse(MailText);
                                            }
                                            catch
                                            { }

                                            if (IncomingMail is null)
                                            {

                                                TCPConnection.WriteLineSMTP(SMTPStatusCodes.TransactionFailed, "The e-mail could not be parsed!");

                                                smtpLogger.LogDebug("Incoming e-mail from {RemoteSocket} could not be parsed: {MailText}",
                                                                    TCPConnection.RemoteSocket,
                                                                    MailText.AggregateWith(Environment.NewLine));
                                                ServerClose = true;
                                                break;

                                            }

                                            #endregion

                                            #region Generate a MessageId... if needed!

                                            var _MessageId = IncomingMail.MessageId;

                                            if (_MessageId is null)
                                            {
                                                _MessageId = Message_Id.Parse(UUIDv7.Generate().ToString() + "@" + DefaultServerName);
                                                IncomingMail = EMail.Parse(new String[] { "Message-Id: " + _MessageId + Environment.NewLine }.Concat(MailText));
                                            }

                                            #endregion

                                            TCPConnection.WriteLineSMTP(SMTPStatusCodes.Ok, "Message received: " + _MessageId);

                                            OnEMailEnvelopReceived?.Invoke(
                                                EventTracking_Id.New,
                                                this,
                                                new EMailEnvelop(
                                                    MailFrom:      MailFroms,
                                                    RcptTo:        RcptTos,
                                                    EMail:         IncomingMail,
                                                    RemoteSocket:  TCPConnection.RemoteSocket
                                                )
                                            );

                                        }

                                    }

                                    #endregion

                                    #region RSET

                                    else if (SMTPCommand.ToUpper() == "RSET")
                                    {
                                        TCPConnection.WriteLineSMTP(SMTPStatusCodes.Ok, "2.0.0 Ok");
                                        MailClientName = "";
                                        MailFroms.Clear();
                                        RcptTos.  Clear();
                                    }

                                    #endregion

                                    #region NOOP

                                    else if (SMTPCommand.ToUpper() == "NOOP")
                                    {
                                        TCPConnection.WriteLineSMTP(SMTPStatusCodes.Ok, "2.0.0 Ok");
                                    }

                                    #endregion

                                    #region VRFY

                                    else if (SMTPCommand.ToUpper().StartsWith("VRFY"))
                                    {
                                        TCPConnection.WriteLineSMTP(SMTPStatusCodes.CannotVerifyUserWillAttemptDelivery, "2.0.0 Send some mail. I'll try my best!");
                                        MailClientName = "";
                                        MailFroms.Clear();
                                        RcptTos.Clear();
                                    }

                                    #endregion

                                    #region QUIT

                                    else if (SMTPCommand.ToUpper() == "QUIT")
                                    {
                                        TCPConnection.WriteLineSMTP(SMTPStatusCodes.ServiceClosingTransmissionChannel, "2.0.0 closing connection");
                                        ClientClose = true;
                                    }

                                    #endregion

                                    #region else error...

                                    else
                                    {

                                        TCPConnection.WriteLineSMTP(SMTPStatusCodes.CommandUnrecognized, "2.0.0 I don't understand how to handle '" + SMTPCommand + "'!");

                                        NotifyErrors(TCPConnection,
                                                     RequestTimestamp,
                                                     SMTPCommand.Trim(),
                                                     SMTPStatusCodes.BadCommandSequence,
                                                     Error: "Invalid SMTP command!");

                                    }

                                    #endregion

                                    #endregion

                                }

                                MemoryStream.SetLength(0);
                                MemoryStream.Seek(0, SeekOrigin.Begin);
                                EndOfSMTPCommand = EOLSearch.NotYet;

                            }

                            #endregion

                            break;

                        #endregion

                        #region CanNotRead

                        case TCPClientResponse.CanNotRead:
                            ServerClose = true;
                            break;

                        #endregion

                        #region ClientClose

                        case TCPClientResponse.ClientClose:
                            ClientClose = true;
                            break;

                        #endregion

                        #region Timeout

                        case TCPClientResponse.Timeout:
                            ServerClose = true;
                            break;

                        #endregion

                    }

                } while (!ClientClose && !ServerClose);

            }

            #region Process exceptions

            catch (IOException ioe)
            {

                if      (ioe.Message.StartsWith("Unable to read data from the transport connection")) { }
                else if (ioe.Message.StartsWith("Unable to write data to the transport connection")) { }

                else
                {

                    //if (OnError is not null)
                    //    OnError(this, Timestamp.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), ioe, MemoryStream);

                }

            }

            catch (Exception e)
            {

                //if (OnError is not null)
                //    OnError(this, Timestamp.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), e, MemoryStream);

            }

            #endregion

            #region Close the TCP connection

            try
            {
                TCPConnection.Close((ClientClose) ? ConnectionClosedBy.Client : ConnectionClosedBy.Server);
            }
            catch
            { }

            #endregion

        }

        #endregion



        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            //=> $"{IPAddress}:{TCPPort} (BufferSize: {BufferSize}, ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";
            => $"{IPAddress}:{TCPPort} (ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";

        #endregion

    }

}
