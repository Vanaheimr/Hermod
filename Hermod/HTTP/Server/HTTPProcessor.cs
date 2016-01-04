/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.Threading;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Services;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// This processor will accept incoming HTTP TCP connections and
    /// decode the transmitted data as HTTP requests.
    /// </summary>
    public class HTTPProcessor : IArrowReceiver<TCPConnection>,
                                 IBoomerangSender<String, DateTime, HTTPRequest, HTTPResponse>
    {

        #region Data

        private const UInt32 ReadTimeout           = 180000U;

        #endregion

        #region Properties

        #region DefaultServerName

        private readonly String _DefaultServerName;

        /// <summary>
        /// The default HTTP servername, used whenever
        /// no HTTP Host-header had been given.
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

        public   event StartedEventHandler                                                  OnStarted;

        /// <summary>
        /// An event called whenever a request came in.
        /// </summary>
        internal event InternalRequestLogHandler                                            RequestLog;

        /// <summary>
        /// An event called whenever a request came in.
        /// </summary>
        internal event InternalAccessLogHandler                                             AccessLog;

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        internal event InternalErrorLogHandler                                              ErrorLog;

        public   event BoomerangSenderHandler<String, DateTime, HTTPRequest, HTTPResponse>  OnNotification;

        public   event CompletedEventHandler                                                OnCompleted;


        public   event ExceptionOccuredEventHandler                                         OnExceptionOccured;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// This processor will accept incoming HTTP TCP connections and
        /// decode the transmitted data as HTTP requests.
        /// </summary>
        /// <param name="DefaultServername">The default HTTP servername, used whenever no HTTP Host-header had been given.</param>
        public HTTPProcessor(String DefaultServername = HTTPServer.__DefaultServerName)
        {

            this._DefaultServerName  = DefaultServername;

        }

        #endregion



        #region NotifyErrors(...)

        private void NotifyErrors(TCPConnection   TCPConnection,
                                  DateTime        Timestamp,
                                  HTTPStatusCode  HTTPStatusCode,
                                  HTTPRequest     Request          = null,
                                  HTTPResponse    Response         = null,
                                  String          Error            = null,
                                  Exception       LastException    = null,
                                  Boolean         CloseConnection  = true)
        {

            #region Call OnError delegates

            var ErrorLogLocal = ErrorLog;
            if (ErrorLogLocal != null)
            {
                ErrorLogLocal(this, Timestamp, Request, Response, Error, LastException);
            }

            #endregion

            #region Send error page to HTTP client

            var Content = String.Empty;

            if (Error != null)
                Content += Error + Environment.NewLine;

            if (LastException != null)
                Content += LastException.Message + Environment.NewLine;

            var _HTTPResponse = new HTTPResponseBuilder() {
                                    HTTPStatusCode  = HTTPStatusCode,
                                    Date            = Timestamp,
                                    Content         = Content.ToUTF8Bytes()
                                };

            TCPConnection.WriteLineToResponseStream(_HTTPResponse.ToString());

            if (CloseConnection)
                TCPConnection.Close();

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
            var MemoryStream     = new MemoryStream();
            var EndOfHTTPHeader  = EOLSearch.NotYet;
            var ClientClose      = false;
            var ServerClose      = false;

            #endregion

            try
            {

                do
                {

                    switch (TCPConnection.TryRead(out Byte, MaxInitialWaitingTimeMS: ReadTimeout))
                    {

                        #region DataAvailable

                        case TCPClientResponse.DataAvailable:

                            #region Check for end of HTTP header...

                            if (EndOfHTTPHeader == EOLSearch.NotYet)
                            {

                                // \n
                                if (Byte == 0x0a)
                                    EndOfHTTPHeader = EOLSearch.EoL_Found;

                                // \r
                                else if (Byte == 0x0d)
                                    EndOfHTTPHeader = EOLSearch.R_Read;

                            }

                            // \n after a \r
                            else if (EndOfHTTPHeader == EOLSearch.R_Read)
                            {
                                if (Byte == 0x0a)
                                    EndOfHTTPHeader = EOLSearch.EoL_Found;
                                else
                                    EndOfHTTPHeader = EOLSearch.NotYet;
                            }

                            // \r after a \r\n
                            else if (EndOfHTTPHeader == EOLSearch.EoL_Found)
                            {
                                if (Byte == 0x0d)
                                    EndOfHTTPHeader = EOLSearch.RN_Read;
                                else
                                    EndOfHTTPHeader = EOLSearch.NotYet;
                            }

                            // \r\n\r after a \r\n\r
                            else if (EndOfHTTPHeader == EOLSearch.RN_Read)
                            {
                                if (Byte == 0x0a)
                                    EndOfHTTPHeader = EOLSearch.Double_EoL_Found;
                                else
                                    EndOfHTTPHeader = EOLSearch.NotYet;
                            }

                            #endregion

                            MemoryStream.WriteByte(Byte);

                            #region If end-of-line -> process data...

                            if (EndOfHTTPHeader == EOLSearch.Double_EoL_Found)
                            {

                                if (MemoryStream.Length > 0)
                                {

                                    var RequestTimestamp = DateTime.Now;

                                    #region Check UTF8 encoding

                                    var HTTPHeaderString = String.Empty;

                                    try
                                    {

                                        HTTPHeaderString = Encoding.UTF8.GetString(MemoryStream.ToArray());

                                    }
                                    catch (Exception)
                                    {

                                        NotifyErrors(TCPConnection,
                                                     RequestTimestamp,
                                                     HTTPStatusCode.BadRequest,
                                                     Error: "Protocol Error: Invalid UTF8 encoding!");

                                    }

                                    #endregion

                                    #region Try to parse the HTTP header

                                    HTTPRequest HttpRequest = null;
                                    var CTS = new CancellationTokenSource();

                                    if (!HTTPRequest.TryParse(TCPConnection.RemoteSocket,
                                                              TCPConnection.LocalSocket,
                                                              HTTPHeaderString.Trim(),
                                                              TCPConnection.NetworkStream,
                                                              CTS.Token,
                                                              out HttpRequest))
                                    {

                                        NotifyErrors(TCPConnection,
                                                     RequestTimestamp,
                                                     HTTPStatusCode.BadRequest,
                                                     Error: "Invalid HTTP header!");

                                        return;

                                    }

                                    #endregion

                                    #region Call RequestLog delegate

                                    var RequestLogLocal = RequestLog;
                                    if (RequestLogLocal != null)
                                    {
                                        RequestLogLocal(this, RequestTimestamp, HttpRequest);
                                    }

                                    #endregion

                                    #region Call OnNotification delegate

                                    HTTPResponse _HTTPResponse = null;

                                    var OnNotificationLocal = OnNotification;
                                    if (OnNotificationLocal != null)
                                    {

                                        // ToDo: How to read request body by application code?!
                                        _HTTPResponse = OnNotification("TCPConnectionId",
                                                                       RequestTimestamp,
                                                                       HttpRequest);

                                        TCPConnection.WriteToResponseStream(_HTTPResponse.RawHTTPHeader.ToUTF8Bytes());

                                        if (_HTTPResponse.Content != null)
                                            TCPConnection.WriteToResponseStream(_HTTPResponse.Content);

                                        else if (_HTTPResponse.ContentStream != null)
                                        {
                                            TCPConnection.WriteToResponseStream(_HTTPResponse.ContentStream);
                                            _HTTPResponse.ContentStream.Dispose();
                                        }

                                        if (_HTTPResponse.Connection.ToLower().Contains("close"))
                                            ServerClose = true;

                                    }

                                    #endregion

                                    #region Call AccessLog delegate

                                    if (_HTTPResponse != null)
                                    {

                                        var AccessLogLocal = AccessLog;
                                        if (AccessLogLocal != null)
                                            AccessLogLocal(this, RequestTimestamp, HttpRequest, _HTTPResponse);

                                    }

                                    #endregion

                                    #region if HTTP Status Code == 4xx | 5xx => Call ErrorLog delegate

                                    if (_HTTPResponse != null &&
                                        _HTTPResponse.HTTPStatusCode.Code >  400 &&
                                        _HTTPResponse.HTTPStatusCode.Code <= 599)
                                    {

                                        var ErrorLogLocal = ErrorLog;
                                        if (ErrorLogLocal != null)
                                            ErrorLogLocal(this, RequestTimestamp, HttpRequest, _HTTPResponse);

                                    }

                                    #endregion

                                }

                                MemoryStream.SetLength(0);
                                MemoryStream.Seek(0, SeekOrigin.Begin);
                                EndOfHTTPHeader = EOLSearch.NotYet;

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
