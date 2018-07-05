/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.Services;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public class RequestLogEvent
    {

        private readonly List<Func<HTTPAPI, DateTime, HTTPRequest, Task>> invocationList;
        private readonly object locker;

        public RequestLogEvent()
        {
            invocationList = new List<Func<HTTPAPI, DateTime, HTTPRequest, Task>>();
            locker         = new object();
        }

        public static RequestLogEvent operator + (RequestLogEvent e, Func<HTTPAPI, DateTime, HTTPRequest, Task> callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                e = new RequestLogEvent();

            lock (e.locker)
            {
                e.invocationList.Add(callback);
            }

            return e;

        }

        public static RequestLogEvent operator - (RequestLogEvent e, Func<HTTPAPI, DateTime, HTTPRequest, Task> callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                return null;

            lock (e.locker)
            {
                e.invocationList.Remove(callback);
            }

            return e;

        }

        public async Task InvokeAsync(HTTPAPI HTTPProcessor, DateTime ServerTimestamp, HTTPRequest Request)
        {

            Func<HTTPAPI, DateTime, HTTPRequest, Task>[] tmpInvocationList;

            lock (locker)
            {
                tmpInvocationList = invocationList.ToArray();
            }

            foreach (var callback in tmpInvocationList)
                await callback(HTTPProcessor, ServerTimestamp, Request).ConfigureAwait(false);

        }

        public Task WhenAll(HTTPAPI HTTPProcessor, DateTime ServerTimestamp, HTTPRequest Request)
        {

            Task[] tmpInvocationList;

            lock (locker)
            {
                tmpInvocationList = invocationList.
                                        Select(callback => callback(HTTPProcessor, ServerTimestamp, Request)).
                                        ToArray();
            }

            return Task.WhenAll(tmpInvocationList);

        }

    }

    public class ResponseLogEvent
    {

        private readonly List<Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, Task>> invocationList;
        private readonly object locker;

        public ResponseLogEvent()
        {
            invocationList = new List<Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, Task>>();
            locker = new object();
        }

        public static ResponseLogEvent operator + (ResponseLogEvent e, Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, Task> callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                e = new ResponseLogEvent();

            lock (e.locker)
            {
                e.invocationList.Add(callback);
            }

            return e;

        }

        public static ResponseLogEvent operator - (ResponseLogEvent e, Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, Task> callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                return null;

            lock (e.locker)
            {
                e.invocationList.Remove(callback);
            }

            return e;

        }

        public async Task InvokeAsync(HTTPAPI HTTPProcessor, DateTime ServerTimestamp, HTTPRequest Request, HTTPResponse Response)
        {

            Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, Task>[] tmpInvocationList;

            lock (locker)
            {
                tmpInvocationList = invocationList.ToArray();
            }

            foreach (var callback in tmpInvocationList)
                await callback(HTTPProcessor, ServerTimestamp, Request, Response).ConfigureAwait(false);

        }

        public Task WhenAll(HTTPAPI HTTPProcessor, DateTime ServerTimestamp, HTTPRequest Request, HTTPResponse Response)
        {

            Task[] tmpInvocationList;

            lock (locker)
            {
                tmpInvocationList = invocationList.
                                        Select(callback => callback(HTTPProcessor, ServerTimestamp, Request, Response)).
                                        ToArray();
            }

            return Task.WhenAll(tmpInvocationList);

        }

    }

    public class ErrorLogEvent
    {

        private readonly List<Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, String, Exception, Task>> invocationList;
        private readonly object locker;

        public ErrorLogEvent()
        {
            invocationList = new List<Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, String, Exception, Task>>();
            locker = new object();
        }

        public static ErrorLogEvent operator +(ErrorLogEvent e, Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, String, Exception, Task> callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                e = new ErrorLogEvent();

            lock (e.locker)
            {
                e.invocationList.Add(callback);
            }

            return e;

        }

        public static ErrorLogEvent operator -(ErrorLogEvent e, Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, String, Exception, Task> callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                return null;

            lock (e.locker)
            {
                e.invocationList.Remove(callback);
            }

            return e;

        }

        public async Task InvokeAsync(HTTPAPI        HTTPProcessor,
                                      DateTime       ServerTimestamp,
                                      HTTPRequest    Request,
                                      HTTPResponse   Response,
                                      String         Error          = null,
                                      Exception      LastException  = null)
        {

            Func<HTTPAPI, DateTime, HTTPRequest, HTTPResponse, String, Exception, Task>[] tmpInvocationList;

            lock (locker)
            {
                tmpInvocationList = invocationList.ToArray();
            }

            foreach (var callback in tmpInvocationList)
                await callback(HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException).ConfigureAwait(false);

        }

        public Task WhenAll(HTTPAPI        HTTPProcessor,
                            DateTime       ServerTimestamp,
                            HTTPRequest    Request,
                            HTTPResponse   Response,
                            String         Error          = null,
                            Exception      LastException  = null)
        {

            Task[] tmpInvocationList;

            lock (locker)
            {
                tmpInvocationList = invocationList.
                                        Select(callback => callback(HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException)).
                                        ToArray();
            }

            return Task.WhenAll(tmpInvocationList);

        }

    }




    public class HTTPAPI
    {

        #region Data

        /// <summary>
        /// Internal non-cryptographic random number generator.
        /// </summary>
        protected static readonly Random                              _Random                        = new Random();

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public  const             String                              DefaultHTTPServerName          = "GraphDefined HTTP API v0.8";

        /// <summary>
        /// The default HTTP server port.
        /// </summary>
        public  static readonly   IPPort                              DefaultHTTPServerPort          = IPPort.Parse(2002);

        /// <summary>
        /// The default service name.
        /// </summary>
        public  const             String                              DefaultServiceName             = "GraphDefined HTTP API";

        /// <summary>
        /// Default logfile name.
        /// </summary>
        public  const             String                              DefaultLogfileName             = "HTTPAPI.log";

        public static readonly Regex JSONWhitespaceRegEx = new Regex(@"(\s)+", RegexOptions.IgnorePatternWhitespace);

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP server of the API.
        /// </summary>
        public HTTPServer    HTTPServer     { get; }

        /// <summary>
        /// The HTTP hostname for all URIs within this API.
        /// </summary>
        public HTTPHostname  Hostname       { get; }

        /// <summary>
        /// The URI prefix of this HTTP API.
        /// </summary>
        public HTTPURI       URIPrefix      { get; }

        /// <summary>
        /// The name of the Open Data API service.
        /// </summary>
        public String        ServiceName    { get; }

        /// <summary>
        /// The unqiue identification of this system instance.
        /// </summary>
        public System_Id     SystemId       { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event called whenever a request came in.
        /// </summary>
        public RequestLogEvent   RequestLog    = new RequestLogEvent();

        /// <summary>
        /// An event called whenever a request could successfully be processed.
        /// </summary>
        public ResponseLogEvent  ResponseLog   = new ResponseLogEvent();

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        public ErrorLogEvent     ErrorLog      = new ErrorLogEvent();

        #endregion

        protected HTTPAPI(HTTPServer                           HTTPServer,
                          HTTPHostname?                        HTTPHostname                  = null,
                          HTTPURI?                             URIPrefix                     = null,
                          String                               ServiceName                   = DefaultServiceName,

                          Boolean                              SkipURITemplates              = false,
                          Boolean                              DisableLogfile                = false,
                          String                               LogfileName                   = DefaultLogfileName)

        {

            this.HTTPServer                   = HTTPServer   ?? throw new ArgumentNullException(nameof(HTTPServer), "HTTPServer!");
            this.Hostname                     = HTTPHostname ?? HTTP.HTTPHostname.Any;
            this.URIPrefix                    = URIPrefix    ?? HTTPURI.Parse("/");

            this.ServiceName                  = ServiceName.IsNotNullOrEmpty() ? ServiceName  : "HTTPAPI";

            this.SystemId                     = System_Id.Parse(Environment.MachineName.Replace("/", "") + "/" + HTTPServer.DefaultHTTPServerPort);

        }

    }










    ///// <summary>
    ///// This processor will accept incoming HTTP TCP connections and
    ///// decode the transmitted data as HTTP requests.
    ///// </summary>
    //public class HTTPProcessor : IArrowReceiver<TCPConnection>,
    //                             IBoomerangSender<String, DateTime, HTTPRequest, Task<HTTPResponse>>
    //{

    //    #region Data

    //    private const UInt32 ReadTimeout           = 180000U;
    //    private readonly Object myLock;

    //    #endregion

    //    #region Properties

    //    #region HTTPServer

    //    private readonly HTTPServer _HTTPServer;

    //    /// <summary>
    //    /// The HTTP server.
    //    /// </summary>
    //    public HTTPServer HTTPServer
    //    {
    //        get
    //        {
    //            return _HTTPServer;
    //        }
    //    }

    //    #endregion

    //    #endregion

    //    #region Events

    //    public   event StartedEventHandler                                                        OnStarted;

    //    /// <summary>
    //    /// An event called whenever a request came in.
    //    /// </summary>
    //    internal event InternalRequestLogHandler                                                  RequestLog;

    //    public RequestLogEvent RequestLog2 = new RequestLogEvent();

    //    /// <summary>
    //    /// An event called whenever a request came in.
    //    /// </summary>
    //    internal event InternalAccessLogHandler                                                   AccessLog;

    //    public ResponseLogEvent ResponseLog2 = new ResponseLogEvent();

    //    /// <summary>
    //    /// An event called whenever a request resulted in an error.
    //    /// </summary>
    //    internal event InternalErrorLogHandler                                                    ErrorLog;

    //    public ErrorLogEvent ErrorLog2 = new ErrorLogEvent();


    //    public   event BoomerangSenderHandler<String, DateTime, HTTPRequest, Task<HTTPResponse>>  OnNotification;

    //    public   event CompletedEventHandler                                                      OnCompleted;


    //    public   event ExceptionOccuredEventHandler                                               OnExceptionOccured;

    //    #endregion

    //    #region Constructor(s)

    //    /// <summary>
    //    /// This processor will accept incoming HTTP TCP connections and
    //    /// decode the transmitted data as HTTP requests.
    //    /// </summary>
    //    /// <param name="HTTPServer">The HTTP server using this processor.</param>
    //    public HTTPProcessor(HTTPServer HTTPServer)
    //    {
    //        this._HTTPServer  = HTTPServer;
    //        this.myLock       = new Object();
    //    }

    //    #endregion


    //    #region NotifyErrors(...)

    //    private void NotifyErrors(HTTPRequest     HTTPRequest,
    //                              TCPConnection   TCPConnection,
    //                              DateTime        Timestamp,
    //                              HTTPStatusCode  HTTPStatusCode,
    //                              HTTPRequest     Request          = null,
    //                              HTTPResponse    Response         = null,
    //                              String          Error            = null,
    //                              Exception       LastException    = null,
    //                              Boolean         CloseConnection  = true)
    //    {

    //        #region Call OnError delegates

    //        var ErrorLogLocal = ErrorLog;
    //        if (ErrorLogLocal != null)
    //        {
    //            ErrorLogLocal(this, Timestamp, Request, Response, Error, LastException);
    //        }

    //        #endregion

    //        #region Send error page to HTTP client

    //        var Content = String.Empty;

    //        if (Error != null)
    //            Content += Error + Environment.NewLine;

    //        if (LastException != null)
    //            Content += LastException.Message + Environment.NewLine;

    //        var _HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
    //                                HTTPStatusCode  = HTTPStatusCode,
    //                                Date            = Timestamp,
    //                                Content         = Content.ToUTF8Bytes()
    //                            };

    //        TCPConnection.WriteLineToResponseStream(_HTTPResponse.ToString());

    //        if (CloseConnection)
    //            TCPConnection.Close();

    //        #endregion

    //    }

    //    #endregion

    //    #region ProcessArrow(TCPConnection)

    //    public void ProcessArrow(TCPConnection TCPConnection)
    //    {

    //        //lock (myLock)
    //        //{

    //        #region Start

    //        //TCPConnection.WriteLineToResponseStream(ServiceBanner);
    //        TCPConnection.NoDelay = true;

    //        Byte Byte;
    //        var MemoryStream     = new MemoryStream();
    //        var EndOfHTTPHeader  = EOLSearch.NotYet;
    //        var ClientClose      = false;
    //        var ServerClose      = false;

    //        #endregion

    //        try
    //        {

    //            do
    //            {

    //                switch (TCPConnection.TryRead(out Byte, MaxInitialWaitingTimeMS: ReadTimeout))
    //                {

    //                    #region DataAvailable

    //                    case TCPClientResponse.DataAvailable:

    //                        #region Check for end of HTTP header...

    //                        if (EndOfHTTPHeader == EOLSearch.NotYet)
    //                        {

    //                            // \n
    //                            if (Byte == 0x0a)
    //                                EndOfHTTPHeader = EOLSearch.EoL_Found;

    //                            // \r
    //                            else if (Byte == 0x0d)
    //                                EndOfHTTPHeader = EOLSearch.R_Read;

    //                        }

    //                        // \n after a \r
    //                        else if (EndOfHTTPHeader == EOLSearch.R_Read)
    //                        {
    //                            if (Byte == 0x0a)
    //                                EndOfHTTPHeader = EOLSearch.EoL_Found;
    //                            else
    //                                EndOfHTTPHeader = EOLSearch.NotYet;
    //                        }

    //                        // \r after a \r\n
    //                        else if (EndOfHTTPHeader == EOLSearch.EoL_Found)
    //                        {
    //                            if (Byte == 0x0d)
    //                                EndOfHTTPHeader = EOLSearch.RN_Read;
    //                            else
    //                                EndOfHTTPHeader = EOLSearch.NotYet;
    //                        }

    //                        // \r\n\r after a \r\n\r
    //                        else if (EndOfHTTPHeader == EOLSearch.RN_Read)
    //                        {
    //                            if (Byte == 0x0a)
    //                                EndOfHTTPHeader = EOLSearch.Double_EoL_Found;
    //                            else
    //                                EndOfHTTPHeader = EOLSearch.NotYet;
    //                        }

    //                        #endregion

    //                        MemoryStream.WriteByte(Byte);

    //                        #region If end-of-line -> process data...

    //                        if (EndOfHTTPHeader == EOLSearch.Double_EoL_Found)
    //                        {

    //                            if (MemoryStream.Length > 0)
    //                            {

    //                                var RequestTimestamp = DateTime.UtcNow;

    //                                #region Check UTF8 encoding

    //                                var HTTPHeaderString = String.Empty;

    //                                try
    //                                {

    //                                    HTTPHeaderString = Encoding.UTF8.GetString(MemoryStream.ToArray());

    //                                }
    //                                catch (Exception)
    //                                {

    //                                    NotifyErrors(null,
    //                                                 TCPConnection,
    //                                                 RequestTimestamp,
    //                                                 HTTPStatusCode.BadRequest,
    //                                                 Error: "Protocol Error: Invalid UTF8 encoding!");

    //                                }

    //                                #endregion

    //                                #region Try to parse the HTTP header

    //                                HTTPRequest HttpRequest = null;
    //                                var CTS = new CancellationTokenSource();

    //                                try
    //                                {

    //                                    HttpRequest = new HTTPRequest(RequestTimestamp,
    //                                                                  _HTTPServer,
    //                                                                  CTS.Token,
    //                                                                  EventTracking_Id.New,
    //                                                                  new HTTPSource(TCPConnection.RemoteSocket),
    //                                                                  TCPConnection.LocalSocket,
    //                                                                  HTTPHeaderString.Trim(),
    //                                                                  TCPConnection.SSLStream != null
    //                                                                      ? (Stream) TCPConnection.SSLStream
    //                                                                      : (Stream) TCPConnection.NetworkStream);

    //                                }
    //                                catch (Exception e)
    //                                {

    //                                    DebugX.Log("HTTPProcessor (Try to parse the HTTP header): " + e.Message);

    //                                    NotifyErrors(null,
    //                                                 TCPConnection,
    //                                                 RequestTimestamp,
    //                                                 HTTPStatusCode.BadRequest,
    //                                                 LastException:  e,
    //                                                 Error:          "Invalid HTTP header!");

    //                                }

    //                                #endregion

    //                                #region Call RequestLog delegate

    //                                if (HttpRequest != null)
    //                                {

    //                                    RequestLog?.Invoke(this,
    //                                                       RequestTimestamp,
    //                                                       HttpRequest);

    //                                    try
    //                                    {

    //                                        RequestLog2?.WhenAll(this,
    //                                                             RequestTimestamp,
    //                                                             HttpRequest);

    //                                    }
    //                                    catch (Exception e)
    //                                    {
    //                                        DebugX.LogT(nameof(HTTPProcessor) + " => " + e.Message);
    //                                    }

    //                                }

    //                                #endregion

    //                                #region Call OnNotification delegate

    //                                HTTPResponse _HTTPResponse = null;

    //                                var OnNotificationLocal = OnNotification;
    //                                if (OnNotificationLocal != null &&
    //                                    HttpRequest         != null)
    //                                {

    //                                    // ToDo: How to read request body by application code?!
    //                                    _HTTPResponse = OnNotification("TCPConnectionId",
    //                                                                   RequestTimestamp,
    //                                                                   HttpRequest).Result;

    //                                    TCPConnection.WriteToResponseStream((_HTTPResponse.RawHTTPHeader.Trim() +
    //                                                                        "\r\n\r\n").
    //                                                                        ToUTF8Bytes());

    //                                    if (_HTTPResponse.HTTPBodyStream != null)
    //                                    {
    //                                        TCPConnection.WriteToResponseStream(_HTTPResponse.HTTPBodyStream);
    //                                        _HTTPResponse.HTTPBodyStream.Close();
    //                                        _HTTPResponse.HTTPBodyStream.Dispose();
    //                                    }

    //                                    else
    //                                        TCPConnection.WriteToResponseStream(_HTTPResponse.HTTPBody);

    //                                    if (_HTTPResponse.Connection.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0)
    //                                        ServerClose = true;

    //                                }

    //                                #endregion

    //                                #region Call AccessLog delegate

    //                                if ( HttpRequest  != null &&
    //                                    _HTTPResponse != null)
    //                                {

    //                                    AccessLog?.Invoke(this,
    //                                                      RequestTimestamp,
    //                                                      HttpRequest,
    //                                                      _HTTPResponse);

    //                                    try
    //                                    {

    //                                        ResponseLog2?.WhenAll(this as Object as HTTPAPI,
    //                                                              RequestTimestamp,
    //                                                              HttpRequest,
    //                                                              _HTTPResponse);

    //                                    }
    //                                    catch (Exception e)
    //                                    {
    //                                        DebugX.LogT(nameof(HTTPProcessor) + " => " + e.Message);
    //                                    }

    //                                }

    //                                #endregion

    //                                #region if HTTP Status Code == 4xx | 5xx => Call ErrorLog delegate

    //                                if ( HttpRequest  != null &&
    //                                    _HTTPResponse != null &&
    //                                    _HTTPResponse.HTTPStatusCode.Code >  400 &&
    //                                    _HTTPResponse.HTTPStatusCode.Code <= 599)
    //                                {

    //                                    ErrorLog?.Invoke(this,
    //                                                     RequestTimestamp,
    //                                                     HttpRequest,
    //                                                     _HTTPResponse);

    //                                    try
    //                                    {

    //                                        ErrorLog2?.WhenAll(this,
    //                                                           RequestTimestamp,
    //                                                           HttpRequest,
    //                                                           _HTTPResponse);

    //                                    }
    //                                    catch (Exception e)
    //                                    {
    //                                        DebugX.LogT(nameof(HTTPProcessor) + " => " + e.Message);
    //                                    }

    //                                }

    //                                #endregion

    //                            }

    //                            MemoryStream.SetLength(0);
    //                            MemoryStream.Seek(0, SeekOrigin.Begin);
    //                            EndOfHTTPHeader = EOLSearch.NotYet;

    //                        }

    //                        #endregion

    //                        break;

    //                    #endregion

    //                    #region CanNotRead

    //                    case TCPClientResponse.CanNotRead:
    //                        ServerClose = true;
    //                        break;

    //                    #endregion

    //                    #region ClientClose

    //                    case TCPClientResponse.ClientClose:
    //                        ClientClose = true;
    //                        break;

    //                    #endregion

    //                    #region Timeout

    //                    case TCPClientResponse.Timeout:
    //                        ServerClose = true;
    //                        break;

    //                    #endregion

    //                }

    //            } while (!ClientClose && !ServerClose);

    //        }

    //        #region Process exceptions

    //        catch (IOException ioe)
    //        {

    //            if      (ioe.Message.StartsWith("Unable to read data from the transport connection")) { }
    //            else if (ioe.Message.StartsWith("Unable to write data to the transport connection")) { }

    //            else
    //            {

    //                DebugX.Log("HTTPProcessor: " + ioe.Message);

    //                //if (OnError != null)
    //                //    OnError(this, DateTime.UtcNow, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), ioe, MemoryStream);

    //            }

    //        }

    //        catch (Exception e)
    //        {

    //            DebugX.Log("HTTPProcessor: " + e.Message);

    //            //if (OnError != null)
    //            //    OnError(this, DateTime.UtcNow, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), e, MemoryStream);

    //        }

    //        #endregion

    //        #region Close the TCP connection

    //        try
    //        {
    //            TCPConnection.Close(ClientClose
    //                                    ? ConnectionClosedBy.Client
    //                                    : ConnectionClosedBy.Server);
    //        }
    //        catch (Exception)
    //        { }

    //        #endregion

    //        //}

    //    }

    //    #endregion

    //    #region ProcessExceptionOccured(Sender, Timestamp, ExceptionMessage)

    //    public void ProcessExceptionOccured(Object     Sender,
    //                                        DateTime   Timestamp,
    //                                        Exception  ExceptionMessage)
    //    {

    //        var OnExceptionOccuredLocal = OnExceptionOccured;
    //        if (OnExceptionOccuredLocal != null)
    //            OnExceptionOccuredLocal(Sender,
    //                                    Timestamp,
    //                                    ExceptionMessage);

    //    }

    //    #endregion

    //    #region ProcessCompleted(Sender, Timestamp, Message = null)

    //    public void ProcessCompleted(Object    Sender,
    //                                 DateTime  Timestamp,
    //                                 String    Message = null)
    //    {

    //        var OnCompletedLocal = OnCompleted;
    //        if (OnCompletedLocal != null)
    //            OnCompletedLocal(Sender,
    //                             Timestamp,
    //                             Message);

    //    }

    //    #endregion

    //}

}
