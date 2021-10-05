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
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.Services;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// Accept incoming SMTP TCP connections and
    /// decode the transmitted data as E-Mails.
    /// </summary>
    public class SMTPConnection : IArrowReceiver<TCPConnection>,
                                  IArrowSender<EMailEnvelop>
    {

        #region Data

        private const UInt32 ReadTimeout           = 180000U;

        #endregion

        #region Properties

        #region DefaultServerName

        private readonly String _DefaultServerName;

        /// <summary>
        /// The default SMTP servername.
        /// </summary>
        public String DefaultServerName
        {
            get
            {
                return _DefaultServerName;
            }
        }

        #endregion

        /// <summary>
        /// Allow to start SSL/TLS via the 'STARTTLS' SMTP command.
        /// </summary>
        public Boolean  AllowStartTLS   { get; }

        #region TLSEnabled

        private Boolean _TLSEnabled;

        /// <summary>
        /// TLS was enabled for this SMTP connection.
        /// </summary>
        public Boolean TLSEnabled
        {
            get
            {
                return _TLSEnabled;
            }
        }

        #endregion

        #endregion

        #region Events

        public   event StartedEventHandler                            OnStarted;

        public delegate MAIL_FROM_FilterResponse MAIL_FROM_FilterHandler(String MAIL_FROM);
        public delegate RCPT_TO_FilterResponse   RCPT_TO_FilterHandler  (String RCPT_TO);

        public   event MAIL_FROM_FilterHandler                        MAIL_FROMFilter;
        public   event RCPT_TO_FilterHandler                          RCPT_TOFilter;
        public   event IncomingEMailEnvelopeHandler                   OnIncomingEMailEnvelope;

        public   event NotificationEventHandler<EMailEnvelop>         OnNotification;

        public   event CompletedEventHandler                          OnCompleted;

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        internal event InternalErrorLogHandler                        ErrorLog;

        public   event ExceptionOccuredEventHandler                   OnExceptionOccured;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// This processor will accept incoming SMTP TCP connections and
        /// decode the transmitted data as SMTP requests.
        /// </summary>
        /// <param name="DefaultServername">The default SMTP servername.</param>
        /// <param name="AllowStartTLS">>Allow to start SSL/TLS via the 'STARTTLS' SMTP command.</param>
        public SMTPConnection(String  DefaultServername  = SMTPServer.__DefaultServerName,
                              Boolean AllowStartTLS      = true)
        {

            this._DefaultServerName  = DefaultServername;
            this.AllowStartTLS       = AllowStartTLS;
            this._TLSEnabled         = false;

        }

        #endregion



        #region NotifyErrors(...)

        private void NotifyErrors(TCPConnection         TCPConnection,
                                  DateTime              Timestamp,
                                  String                SMTPCommand,
                                  SMTPStatusCode        SMTPStatusCode,
                                  EMail                 EMail            = null,
                                  SMTPExtendedResponse  Response         = null,
                                  String                Error            = null,
                                  Exception             LastException    = null,
                                  Boolean               CloseConnection  = true)
        {

            var ErrorLogLocal = ErrorLog;
            if (ErrorLogLocal != null)
            {
                ErrorLogLocal(this, Timestamp, SMTPCommand, EMail, Response, Error, LastException);
            }

        }

        #endregion

        #region ProcessArrow(TCPConnection)

        public void ProcessArrow(TCPConnection TCPConnection)
        {

            #region Start

            //TCPConnection.WriteLineToResponseStream(ServiceBanner);
            TCPConnection.NoDelay = true;

            Byte Byte;
            var MemoryStream      = new MemoryStream();
            var EndOfSMTPCommand  = EOLSearch.NotYet;
            var ClientClose       = false;
            var ServerClose       = false;
            var MailClientName    = "";

            #endregion

            try
            {

                var MailFroms  = EMailAddressListBuilder.Empty;
                var RcptTos    = EMailAddressListBuilder.Empty;

                TCPConnection.WriteLineSMTP(SMTPStatusCode.ServiceReady,
                                            _DefaultServerName + " ESMTP Vanaheimr Hermod Mail Transport Service");

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

                                    var RequestTimestamp = DateTime.UtcNow;

                                    #region Check UTF8 encoding

                                    var SMTPCommand = String.Empty;

                                    try
                                    {

                                        SMTPCommand = Encoding.UTF8.GetString(MemoryStream.ToArray()).Trim();

                                        Debug.WriteLine("<< " + SMTPCommand);

                                    }
                                    catch (Exception e)
                                    {

                                        NotifyErrors(TCPConnection,
                                                     RequestTimestamp,
                                                     "",
                                                     SMTPStatusCode.SyntaxError,
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
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok, DefaultServerName);

                                        }
                                        else
                                        {
                                            // 501 Syntax: HELO hostname
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.SyntaxError, "Syntax: HELO hostname");
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
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok,
                                                                        DefaultServerName,
                                                                        "VRFY",
                                                                        AllowStartTLS ? "STARTTLS"         : null,
                                                                        _TLSEnabled   ? "AUTH PLAIN LOGIN" : null,
                                                                        "SIZE 204800000",
                                                                        "ENHANCEDSTATUSCODES",
                                                                        "8BITMIME");

                                        }
                                        else
                                        {
                                            // 501 Syntax: EHLO hostname
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.SyntaxError, "Syntax: EHLO hostname");
                                        }

                                    }

                                    #endregion

                                    #region STARTTLS

                                    else if (SMTPCommand.ToUpper() == "STARTTLS")
                                    {

                                        if (_TLSEnabled)
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.BadCommandSequence, "5.5.1 TLS already started");

                                        else if (MailClientName.IsNullOrEmpty())
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.BadCommandSequence, "5.5.1 EHLO/HELO first");

                                        else
                                        {

                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.ServiceReady, "2.0.0 Ready to start TLS");

                                            //                                            var _TLSStream = new SslStream(TCPConnection.NetworkStream);
                                            //                                            _TLSStream.AuthenticateAsServer(TLSCert, false, SslProtocols.Tls12, false);
                                            _TLSEnabled = true;

                                        }

                                    }

                                    #endregion

                                    #region AUTH LOGIN|PLAIN|...

                                    else if (SMTPCommand.ToUpper().StartsWith("AUTH "))
                                    {

                                        if (!_TLSEnabled)
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.BadCommandSequence, "5.5.1 STARTTLS first");

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

                                            var MAIL_FROMFilterLocal = MAIL_FROMFilter;
                                            if (MAIL_FROMFilterLocal != null)
                                                _SMTPFilterResponse = MAIL_FROMFilterLocal(MailFrom);

                                            if (_SMTPFilterResponse == null)
                                            {
                                                TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok, "2.1.0 " + MailFrom + " Sender ok");
                                                MailFroms.Add(EMailAddress.Parse(MailFrom));
                                            }

                                            else if (_SMTPFilterResponse.Forward)
                                            {
                                                TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok, "2.1.0 " + MailFrom + " " + _SMTPFilterResponse.Description);
                                                MailFroms.Add(EMailAddress.Parse(MailFrom));
                                            }

                                            else
                                                TCPConnection.WriteLineSMTP(SMTPStatusCode.TransactionFailed, "5.7.1 " + _SMTPFilterResponse.Description);

                                        }
                                        else
                                        {
                                            // 501 Syntax: EHLO hostname
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.SyntaxError, "Syntax: MAIL FROM: <mail@domain.tld>");
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

                                            var RCPT_TOFilterLocal = RCPT_TOFilter;
                                            if (RCPT_TOFilterLocal != null)
                                                _SMTPFilterResponse = RCPT_TOFilterLocal(RcptTo);

                                            if (_SMTPFilterResponse == null)
                                            {
                                                TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok, "2.1.0 " + RcptTo + " Recipient ok");
                                                RcptTos.Add(EMailAddress.Parse(RcptTo));
                                            }

                                            else if (_SMTPFilterResponse.Forward) {
                                                TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok, "2.1.0 " + RcptTo + " " + _SMTPFilterResponse.Description);
                                                RcptTos.Add(EMailAddress.Parse(RcptTo));
                                            }

                                            else
                                                TCPConnection.WriteLineSMTP(SMTPStatusCode.TransactionFailed, "5.7.1 " + _SMTPFilterResponse.Description);

                                        }
                                        else
                                        {
                                            // 501 Syntax: EHLO hostname
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.SyntaxError, "Syntax: RCPT TO: <mail@domain.tld>");
                                        }

                                    }

                                    #endregion

                                    #region DATA

                                    else if (SMTPCommand.ToUpper().StartsWith("DATA"))
                                    {

                                        if (MailFroms.Length == 0 || RcptTos.Length == 0)
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.BadCommandSequence, "Bad command sequence!");

                                        else
                                        {

                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.StartMailInput, "Ok Send data ending with <CRLF>.<CRLF>");

                                            #region Read all e-mail lines...

                                            var MailText  = new List<String>();
                                            var MailLine  = "";

                                            do
                                            {

                                                MailLine = TCPConnection.ReadLine();

                                                // "." == End-of-EMail...
                                                if (MailLine != null && MailLine != ".")
                                                {
                                                    MailText.Add(MailLine);
                                                    Debug.WriteLine("<<< " + MailLine);
                                                }

                                            } while (MailLine != ".");

                                            #endregion

                                            #region Try to parse the incoming e-mail...

                                            EMail IncomingMail = null;

                                            try
                                            {
                                                IncomingMail = EMail.Parse(MailText);
                                            }
                                            catch (Exception)
                                            { }

                                            if (IncomingMail == null)
                                            {

                                                TCPConnection.WriteLineSMTP(SMTPStatusCode.TransactionFailed, "The e-mail could not be parsed!");

                                                Debug.WriteLine("[" + DateTime.UtcNow + "] Incoming e-mail could not be parsed!");
                                                Debug.WriteLine(MailText.AggregateWith(Environment.NewLine));

                                            }

                                            #endregion

                                            #region Generate a MessageId... if needed!

                                            var _MessageId = IncomingMail.MessageId;

                                            if (_MessageId == null)
                                            {
                                                _MessageId = Message_Id.Parse(Guid.NewGuid().ToString() + "@" + _DefaultServerName);
                                                IncomingMail = EMail.Parse(new String[] { "Message-Id: " + _MessageId + Environment.NewLine }.Concat(MailText));
                                            }

                                            #endregion

                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok, "Message received: " + _MessageId);

                                            var OnNotificationLocal = OnNotification;
                                            if (OnNotificationLocal != null)
                                                OnNotificationLocal(new EMailEnvelop(MailFrom:      MailFroms,
                                                                                     RcptTo:        RcptTos,
                                                                                     EMail:         IncomingMail,
                                                                                     RemoteSocket:  TCPConnection.RemoteSocket));

                                        }

                                    }

                                    #endregion

                                    #region RSET

                                    else if (SMTPCommand.ToUpper() == "RSET")
                                    {
                                        TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok, "2.0.0 Ok");
                                        MailClientName = "";
                                        MailFroms.Clear();
                                        RcptTos.  Clear();
                                    }

                                    #endregion

                                    #region NOOP

                                    else if (SMTPCommand.ToUpper() == "NOOP")
                                    {
                                        TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok, "2.0.0 Ok");
                                    }

                                    #endregion

                                    #region VRFY

                                    else if (SMTPCommand.ToUpper().StartsWith("VRFY"))
                                    {
                                        TCPConnection.WriteLineSMTP(SMTPStatusCode.CannotVerifyUserWillAttemptDelivery, "2.0.0 Send some mail. I'll try my best!");
                                        MailClientName = "";
                                        MailFroms.Clear();
                                        RcptTos.Clear();
                                    }

                                    #endregion

                                    #region QUIT

                                    else if (SMTPCommand.ToUpper() == "QUIT")
                                    {
                                        TCPConnection.WriteLineSMTP(SMTPStatusCode.ServiceClosingTransmissionChannel, "2.0.0 closing connection");
                                        ClientClose = true;
                                    }

                                    #endregion

                                    #region else error...

                                    else
                                    {

                                        TCPConnection.WriteLineSMTP(SMTPStatusCode.CommandUnrecognized, "2.0.0 I don't understand how to handle '" + SMTPCommand + "'!");

                                        NotifyErrors(TCPConnection,
                                                     RequestTimestamp,
                                                     SMTPCommand.Trim(),
                                                     SMTPStatusCode.BadCommandSequence,
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

                    //if (OnError != null)
                    //    OnError(this, DateTime.UtcNow, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), ioe, MemoryStream);

                }

            }

            catch (Exception e)
            {

                //if (OnError != null)
                //    OnError(this, DateTime.UtcNow, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), e, MemoryStream);

            }

            #endregion

            #region Close the TCP connection

            try
            {
                TCPConnection.Close((ClientClose) ? ConnectionClosedBy.Client : ConnectionClosedBy.Server);
            }
            catch (Exception)
            { }

            #endregion

        }

        #endregion

        #region ProcessExceptionOccured(Sender, Timestamp, ExceptionMessage)

        public void ProcessExceptionOccured(Object     Sender,
                                            DateTime   Timestamp,
                                            Exception  ExceptionMessage)
        {

            var OnExceptionOccuredLocal = OnExceptionOccured;
            if (OnExceptionOccuredLocal != null)
                OnExceptionOccuredLocal(Sender,
                                        Timestamp,
                                        ExceptionMessage);

        }

        #endregion

        #region ProcessCompleted(Sender, Timestamp, Message = null)

        public void ProcessCompleted(Object    Sender,
                                     DateTime  Timestamp,
                                     String    Message = null)
        {

            var OnCompletedLocal = OnCompleted;
            if (OnCompletedLocal != null)
                OnCompletedLocal(Sender,
                                 Timestamp,
                                 Message);

        }

        #endregion


    }

}
