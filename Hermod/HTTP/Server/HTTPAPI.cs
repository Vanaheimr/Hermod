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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

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


    /// <summary>
    /// A HTTP API.
    /// </summary>
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

        /// <summary>
        /// JSON whitespace regular expression...
        /// </summary>
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
        /// An event called whenever a HTTP request came in.
        /// </summary>
        public RequestLogEvent   RequestLog    = new RequestLogEvent();

        /// <summary>
        /// An event called whenever a HTTP request could successfully be processed.
        /// </summary>
        public ResponseLogEvent  ResponseLog   = new ResponseLogEvent();

        /// <summary>
        /// An event called whenever a HTTP request resulted in an error.
        /// </summary>
        public ErrorLogEvent     ErrorLog      = new ErrorLogEvent();

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP API.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="HTTPHostname">A HTTP hostname.</param>
        /// <param name="URIPrefix">An URI prefix.</param>
        /// <param name="ServiceName">A service name.</param>
        public HTTPAPI(HTTPServer     HTTPServer,
                       HTTPHostname?  HTTPHostname   = null,
                       HTTPURI?       URIPrefix      = null,
                       String         ServiceName    = DefaultServiceName)

        {

            this.HTTPServer                   = HTTPServer   ?? throw new ArgumentNullException(nameof(HTTPServer), "HTTPServer!");
            this.Hostname                     = HTTPHostname ?? HTTP.HTTPHostname.Any;
            this.URIPrefix                    = URIPrefix    ?? HTTPURI.Parse("/");

            this.ServiceName                  = ServiceName.IsNotNullOrEmpty() ? ServiceName  : "HTTPAPI";

            this.SystemId                     = System_Id.Parse(Environment.MachineName.Replace("/", "") + "/" + HTTPServer.DefaultHTTPServerPort);


            HTTPServer.RequestLog   += (HTTPProcessor, ServerTimestamp, Request)                                 => RequestLog. WhenAll(HTTPProcessor, ServerTimestamp, Request);
            HTTPServer.ResponseLog  += (HTTPProcessor, ServerTimestamp, Request, Response)                       => ResponseLog.WhenAll(HTTPProcessor, ServerTimestamp, Request, Response);
            HTTPServer.ErrorLog     += (HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException) => ErrorLog.   WhenAll(HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException);

        }

        #endregion


        #region Start()

        public void Start()
        {

            lock (HTTPServer)
            {

                if (!HTTPServer.IsStarted)
                    HTTPServer.Start();

                //SendStarted(this, DateTime.UtcNow);

            }

        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        public void Shutdown(String Message = null, Boolean Wait = true)
        {

            lock (HTTPServer)
            {

                HTTPServer.Shutdown(Message, Wait);
                //SendCompleted(this, DateTime.UtcNow, Message);

            }

        }

        #endregion

        #region Dispose()

        public void Dispose()
        {

            lock (HTTPServer)
            {
                HTTPServer.Dispose();
            }

        }

        #endregion

    }

}
