/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.IO;
using System.Linq;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Services;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Services.Mail;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.X509.Extension;
using System.Collections.Generic;
using Org.BouncyCastle.Asn1;
using System.Net.Security;
using System.Security.Authentication;


#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.SMTP
{

    public static class Ext3
    {

        public static void WriteSMTP(this TCPConnection TCPConn, SMTPStatusCode Statuscode, String Text)
        {
            TCPConn.WriteToResponseStream(((Int32) Statuscode) + " " + Text);
            TCPConn.Flush();
        }

        public static void WriteLineSMTP(this TCPConnection TCPConn, params SMTPResponse[] Response)
        {

            var n = (UInt64) Response.Where(line => line.Response.IsNotNullOrEmpty()).Count();

            Response.
                Where(line => line.Response.IsNotNullOrEmpty()).
                ForEachCounted((i, response) => TCPConn.WriteLineToResponseStream(((Int32) response.StatusCode) + (i < n ? "-" : " ") + response.Response));

            TCPConn.Flush();

        }

        public static void WriteLineSMTP(this TCPConnection TCPConn, SMTPStatusCode StatusCode, params String[] Response)
        {

            var n = (UInt64) Response.Where(line => line.IsNotNullOrEmpty()).Count();

            Response.
                Where(line => line.IsNotNullOrEmpty()).
                ForEachCounted((i, response) => TCPConn.WriteLineToResponseStream(((Int32)StatusCode) + (i < n ? "-" : " ") + response));

            TCPConn.Flush();

        }

    }

    public enum SMTPServerStatus
    {
        commandmode,
        mailmode
    }


    /// <summary>
    /// This processor will accept incoming SMTP TCP connections and
    /// decode the transmitted data as SMTP requests.
    /// </summary>
    public class SMTPProcessor : IArrowReceiver<TCPConnection>,
                                 IBoomerangSender<String, DateTime, EMail, SMTPExtendedResponse>
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

        #endregion

        #region Events

        public   event StartedEventHandler                                                      OnStarted;

        /// <summary>
        /// An event called whenever a request came in.
        /// </summary>
        internal event InternalAccessLogHandler                                                 AccessLog;

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        internal event InternalErrorLogHandler                                                  ErrorLog;

        public   event BoomerangSenderHandler<String, DateTime, EMail, SMTPExtendedResponse>    OnNotification;

        public   event CompletedEventHandler                                                    OnCompleted;


        public   event ExceptionOccuredEventHandler                                             OnExceptionOccured;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// This processor will accept incoming SMTP TCP connections and
        /// decode the transmitted data as SMTP requests.
        /// </summary>
        /// <param name="DefaultServername">The default SMTP servername.</param>
        public SMTPProcessor(String DefaultServername = SMTPServer.__DefaultServerName)
        {

            this._DefaultServerName  = DefaultServername;

        }

        #endregion



        #region NotifyErrors(...)

        private void NotifyErrors(TCPConnection   TCPConnection,
                                  DateTime        Timestamp,
                                  SMTPStatusCode  SMTPStatusCode,
                                  EMail           EMail            = null,
                                  SMTPExtendedResponse    Response         = null,
                                  String          Error            = null,
                                  Exception       LastException    = null,
                                  Boolean         CloseConnection  = true)
        {

            #region Call OnError delegates

            var ErrorLogLocal = ErrorLog;
            if (ErrorLogLocal != null)
            {
                ErrorLogLocal(this, Timestamp, EMail, Response, Error, LastException);
            }

            #endregion

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
            var TLSEnabled        = false;
            var MailClientName    = "";

            #endregion

            try
            {

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

                                    var RequestTimestamp = DateTime.Now;

                                    #region Check UTF8 encoding

                                    var SMTPCommand = String.Empty;

                                    try
                                    {

                                        SMTPCommand = Encoding.UTF8.GetString(MemoryStream.ToArray()).Trim();

                                    }
                                    catch (Exception)
                                    {

                                        NotifyErrors(TCPConnection,
                                                     RequestTimestamp,
                                                     SMTPStatusCode.SyntaxError,
                                                     Error: "Protocol Error: Invalid UTF8 encoding!");

                                    }

                                    #endregion

                                    #region Try to parse the SMTP command

                                    #region HELO <MailClientName>

                                    if (SMTPCommand.ToUpper().Trim().StartsWith("HELO"))
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

                                    else if (SMTPCommand.ToUpper().Trim().StartsWith("EHLO"))
                                    {

                                        if (SMTPCommand.Trim().Length > 5 && SMTPCommand.Trim()[4] == ' ')
                                        {

                                            MailClientName = SMTPCommand.Trim().Substring(5);

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
                                            // 250 DSN
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.Ok,
                                                                        DefaultServerName,
                                                                        "STARTTLS",
                                                                        TLSEnabled ? "AUTH PLAIN LOGIN" : null,
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

                                    else if (SMTPCommand.Trim().ToUpper() == "STARTTLS")
                                    {

                                        if (TLSEnabled)
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.BadCommandSequence, "5.5.1 TLS already started");

                                        else if (MailClientName.IsNullOrEmpty())
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.BadCommandSequence, "5.5.1 EHLO/HELO first");

                                        else
                                        {

                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.ServiceReady, "2.0.0 Ready to start TLS");

//                                            var _TLSStream = new SslStream(TCPConnection.NetworkStream);
//                                            _TLSStream.AuthenticateAsServer(TLSCert, false, SslProtocols.Tls12, false);
                                            TLSEnabled = true;

                                        }

                                    }

                                    #endregion

                                    #region AUTH LOGIN|PLAIN|...

                                    else if (SMTPCommand.ToUpper().Trim().StartsWith("AUTH "))
                                    {

                                        if (!TLSEnabled)
                                            TCPConnection.WriteLineSMTP(SMTPStatusCode.BadCommandSequence, "5.5.1 STARTTLS first");

                                    }

                                    #endregion

                                    #region QUIT

                                    else if (SMTPCommand.ToUpper().Trim() == "QUIT")
                                    {
                                        TCPConnection.WriteLineSMTP(SMTPStatusCode.ServiceClosingTransmissionChannel, "2.0.0 closing connection");
                                        ClientClose = true;
                                    }

                                    #endregion

                                    #region else error...

                                    else
                                        NotifyErrors(TCPConnection,
                                                     RequestTimestamp,
                                                     SMTPStatusCode.BadCommandSequence,
                                                     Error: "Invalid SMTP command!");

                                    #endregion

                                    #endregion

                                    #region Call OnNotification delegate

                                    SMTPExtendedResponse _SMTPResponse = null;

                                    var OnNotificationLocal = OnNotification;
                                    if (OnNotificationLocal != null)
                                    {

                                        // ToDo: How to read request body by application code?!
                                        //_SMTPResponse = OnNotification("TCPConnectionId",
                                        //                               RequestTimestamp,
                                        //                               RequestHeader);

                                        //TCPConnection.WriteToResponseStream(_SMTPResponse.RawHTTPHeader.ToUTF8Bytes());

                                        //if (_SMTPResponse.Content != null)
                                        //    TCPConnection.WriteToResponseStream(_SMTPResponse.Content);

                                        //else if (_SMTPResponse.ContentStream != null)
                                        //    TCPConnection.WriteToResponseStream(_SMTPResponse.ContentStream);

                                        //if (_SMTPResponse.Connection.ToLower().Contains("close"))
                                        //    ServerClose = true;

                                    }

                                    #endregion

                                    #region Call AccessLog delegate

                                    if (_SMTPResponse != null)
                                    {

                                        //var AccessLogLocal = AccessLog;
                                        //if (AccessLogLocal != null)
                                        //    AccessLogLocal(this, RequestTimestamp, RequestHeader, _SMTPResponse);

                                    }

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
                    //    OnError(this, DateTime.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), ioe, MemoryStream);

                }

            }

            catch (Exception e)
            {

                //if (OnError != null)
                //    OnError(this, DateTime.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), e, MemoryStream);

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
