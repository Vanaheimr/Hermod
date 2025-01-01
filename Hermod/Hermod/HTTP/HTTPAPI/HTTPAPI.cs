/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Globalization;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Logging;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    #region (class) HTTPRequestLogEvent

    /// <summary>
    /// An async event notifying about HTTP requests.
    /// </summary>
    public class HTTPRequestLogEvent
    {

        #region Data

        private readonly List<HTTPRequestLogHandler> subscribers = [];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about incoming HTTP requests.
        /// </summary>
        public HTTPRequestLogEvent()
        { }

        #endregion


        #region + / Add

        public static HTTPRequestLogEvent operator + (HTTPRequestLogEvent e, HTTPRequestLogHandler callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Add(callback);
            }

            return e;

        }

        public HTTPRequestLogEvent Add(HTTPRequestLogHandler callback)
        {

            lock (subscribers)
            {
                subscribers.Add(callback);
            }

            return this;

        }

        #endregion

        #region - / Remove

        public static HTTPRequestLogEvent operator - (HTTPRequestLogEvent e, HTTPRequestLogHandler callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPRequestLogEvent Remove(HTTPRequestLogHandler callback)
        {

            lock (subscribers)
            {
                subscribers.Remove(callback);
            }

            return this;

        }

        #endregion


        #region InvokeAsync(ServerTimestamp, HTTPAPI, Request)

        /// <summary>
        /// Call all subscribers sequentially.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        public async Task InvokeAsync(DateTime     ServerTimestamp,
                                      HTTPAPI      HTTPAPI,
                                      HTTPRequest  Request)
        {

            HTTPRequestLogHandler[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers];
            }

            foreach (var callback in invocationList)
                await callback(ServerTimestamp, HTTPAPI, Request).ConfigureAwait(false);

        }

        #endregion

        #region WhenAny    (ServerTimestamp, HTTPAPI, Request,               Timeout = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for any to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        public Task WhenAny(DateTime      ServerTimestamp,
                            HTTPAPI       HTTPAPI,
                            HTTPRequest   Request,
                            TimeSpan?     Timeout   = null)
        {

            List<Task> invocationList;

            lock (subscribers)
            {

                invocationList = subscribers.
                                     Select(callback => callback(ServerTimestamp, HTTPAPI, Request)).
                                     ToList();

                if (Timeout.HasValue)
                    invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(invocationList);

        }

        #endregion

        #region WhenFirst  (ServerTimestamp, HTTPAPI, Request, VerifyResult, Timeout = null, DefaultResult = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <typeparam name="T">The type of the results.</typeparam>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="VerifyResult">A delegate to verify and filter results.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        /// <param name="DefaultResult">A default result in case of errors or a timeout.</param>
        public Task<T> WhenFirst<T>(DateTime            ServerTimestamp,
                                    HTTPAPI             HTTPAPI,
                                    HTTPRequest         Request,
                                    Func<T, Boolean>    VerifyResult,
                                    TimeSpan?           Timeout         = null,
                                    Func<TimeSpan, T>?  DefaultResult   = null)
        {

            #region Data

            List<Task>  invocationList;
            Task?       WorkDone;
            Task<T>?    Result;
            DateTime    StartTime     = Timestamp.Now;
            Task?       TimeoutTask   = null;

            #endregion

            lock (subscribers)
            {

                invocationList = subscribers.
                                     Select(callback => callback(ServerTimestamp, HTTPAPI, Request)).
                                     ToList();

                if (Timeout.HasValue)
                    invocationList.Add(TimeoutTask = Task.Run(() => Thread.Sleep(Timeout.Value)));

            }

            do
            {

                try
                {

                    WorkDone = Task.WhenAny(invocationList);

                    invocationList.Remove(WorkDone);

                    if (WorkDone != TimeoutTask)
                    {

                        Result = WorkDone as Task<T>;

                        if (Result is not null &&
                            !EqualityComparer<T>.Default.Equals(Result.Result, default) &&
                            VerifyResult(Result.Result))
                        {
                            return Result;
                        }

                    }

                }
                catch (Exception e)
                {
                    DebugX.LogT(e.Message);
                    WorkDone = null;
                }

            }
            while (!(WorkDone == TimeoutTask || invocationList.Count == 0));

            return Task.FromResult(DefaultResult(Timestamp.Now - StartTime));

        }

        #endregion

        #region WhenAll    (ServerTimestamp, HTTPAPI, Request)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        public Task WhenAll(DateTime      ServerTimestamp,
                            HTTPAPI       HTTPAPI,
                            HTTPRequest   Request)
        {

            Task[] invocationList;

            lock (subscribers)
            {
                invocationList = subscribers.
                                     Select(callback => callback(ServerTimestamp, HTTPAPI, Request)).
                                     ToArray();
            }

            return Task.WhenAll(invocationList);

        }

        #endregion

    }

    #endregion

    #region (class) HTTPResponseLogEvent

    /// <summary>
    /// An async event notifying about HTTP responses.
    /// </summary>
    public class HTTPResponseLogEvent
    {

        #region Data

        private readonly List<HTTPResponseLogHandler> subscribers = [];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about HTTP responses.
        /// </summary>
        public HTTPResponseLogEvent()
        { }

        #endregion


        #region + / Add

        public static HTTPResponseLogEvent operator + (HTTPResponseLogEvent e, HTTPResponseLogHandler callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Add((timestamp, api, request, response) => callback(timestamp, api, request, response));
            }

            return e;

        }

        public HTTPResponseLogEvent Add(HTTPResponseLogHandler callback)
        {

            lock (subscribers)
            {
                subscribers.Add(callback);
            }

            return this;

        }

        #endregion

        #region - / Remove

        public static HTTPResponseLogEvent operator - (HTTPResponseLogEvent e, HTTPResponseLogHandler callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPResponseLogEvent Remove(HTTPResponseLogHandler callback)
        {

            lock (subscribers)
            {
                subscribers.Remove(callback);
            }

            return this;

        }

        #endregion


        #region InvokeAsync(ServerTimestamp, HTTPAPI, Request, Response)

        /// <summary>
        /// Call all subscribers sequentially.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        public async Task InvokeAsync(DateTime      ServerTimestamp,
                                      HTTPAPI       HTTPAPI,
                                      HTTPRequest   Request,
                                      HTTPResponse  Response)
        {

            HTTPResponseLogHandler[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers];
            }

            foreach (var callback in invocationList)
                await callback(ServerTimestamp, HTTPAPI, Request, Response).ConfigureAwait(false);

        }

        #endregion

        #region WhenAny    (ServerTimestamp, HTTPAPI, Request, Response,               Timeout = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for any to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        public Task WhenAny(DateTime      ServerTimestamp,
                            HTTPAPI       HTTPAPI,
                            HTTPRequest   Request,
                            HTTPResponse  Response,
                            TimeSpan?     Timeout = null)
        {

            List<Task> invocationList;

            lock (subscribers)
            {

                invocationList = subscribers.
                                     Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response)).
                                     ToList();

                if (Timeout.HasValue)
                    invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(invocationList);

        }

        #endregion

        #region WhenFirst  (ServerTimestamp, HTTPAPI, Request, Response, VerifyResult, Timeout = null, DefaultResult = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <typeparam name="T">The type of the results.</typeparam>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="VerifyResult">A delegate to verify and filter results.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        /// <param name="DefaultResult">A default result in case of errors or a timeout.</param>
        public Task<T> WhenFirst<T>(DateTime            ServerTimestamp,
                                    HTTPAPI             HTTPAPI,
                                    HTTPRequest         Request,
                                    HTTPResponse        Response,
                                    Func<T, Boolean>    VerifyResult,
                                    TimeSpan?           Timeout        = null,
                                    Func<TimeSpan, T>?  DefaultResult  = null)
        {

            #region Data

            List<Task>  invocationList;
            Task?       WorkDone;
            Task<T>?    Result;
            DateTime    StartTime     = Timestamp.Now;
            Task?       TimeoutTask   = null;

            #endregion

            lock (subscribers)
            {

                invocationList = subscribers.
                                     Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response)).
                                     ToList();

                if (Timeout.HasValue)
                    invocationList.Add(TimeoutTask = Task.Run(() => Thread.Sleep(Timeout.Value)));

            }

            do
            {

                try
                {

                    WorkDone = Task.WhenAny(invocationList);

                    invocationList.Remove(WorkDone);

                    if (WorkDone != TimeoutTask)
                    {

                        Result = WorkDone as Task<T>;

                        if (Result is not null &&
                            !EqualityComparer<T>.Default.Equals(Result.Result, default) &&
                            VerifyResult(Result.Result))
                        {
                            return Result;
                        }

                    }

                }
                catch (Exception e)
                {
                    DebugX.LogT(e.Message);
                    WorkDone = null;
                }

            }
            while (!(WorkDone == TimeoutTask || invocationList.Count == 0));

            return Task.FromResult(DefaultResult(Timestamp.Now - StartTime));

        }

        #endregion

        #region WhenAll    (ServerTimestamp, HTTPAPI, Request, Response)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        public Task WhenAll(DateTime      ServerTimestamp,
                            HTTPAPI       HTTPAPI,
                            HTTPRequest   Request,
                            HTTPResponse  Response)
        {

            Task[] invocationList;

            lock (subscribers)
            {
                invocationList = subscribers.
                                     Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response)).
                                     ToArray();
            }

            return Task.WhenAll(invocationList);

        }

        #endregion

    }

    #endregion

    #region (class) HTTPErrorLogEvent

    /// <summary>
    /// An async event notifying about HTTP errors.
    /// </summary>
    public class HTTPErrorLogEvent
    {

        #region Data

        private readonly List<HTTPErrorLogHandler> subscribers = [];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about HTTP errors.
        /// </summary>
        public HTTPErrorLogEvent()
        { }

        #endregion


        #region + / Add

        public static HTTPErrorLogEvent operator + (HTTPErrorLogEvent e, HTTPErrorLogHandler callback)
        {

            e ??= new HTTPErrorLogEvent();

            lock (e.subscribers)
            {
                e.subscribers.Add(callback);
            }

            return e;

        }

        public HTTPErrorLogEvent Add(HTTPErrorLogHandler callback)
        {

            lock (subscribers)
            {
                subscribers.Add(callback);
            }

            return this;

        }

        #endregion

        #region - / Remove

        public static HTTPErrorLogEvent operator - (HTTPErrorLogEvent e, HTTPErrorLogHandler callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPErrorLogEvent Remove(HTTPErrorLogHandler callback)
        {

            lock (subscribers)
            {
                subscribers.Remove(callback);
            }

            return this;

        }

        #endregion


        #region InvokeAsync(ServerTimestamp, HTTPAPI, Request, Response, Error = null, LastException = null)

        /// <summary>
        /// Call all subscribers sequentially.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="Error">An error message.</param>
        /// <param name="LastException">The last exception occured.</param>
        public async Task InvokeAsync(DateTime      ServerTimestamp,
                                      HTTPAPI       HTTPAPI,
                                      HTTPRequest   Request,
                                      HTTPResponse  Response,
                                      String?       Error           = null,
                                      Exception?    LastException   = null)
        {

            HTTPErrorLogHandler[] invocationList;

            lock (subscribers)
            {
                invocationList = subscribers.ToArray();
            }

            foreach (var callback in invocationList)
                await callback(ServerTimestamp, HTTPAPI, Request, Response, Error, LastException).ConfigureAwait(false);

        }

        #endregion

        #region WhenAny    (ServerTimestamp, HTTPAPI, Request, Response, Error = null, LastException = null, Timeout = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for any to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="Error">An error message.</param>
        /// <param name="LastException">The last exception occured.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        public Task WhenAny(DateTime      ServerTimestamp,
                            HTTPAPI       HTTPAPI,
                            HTTPRequest   Request,
                            HTTPResponse  Response,
                            String?       Error           = null,
                            Exception?    LastException   = null,
                            TimeSpan?     Timeout         = null)
        {

            List<Task> invocationList;

            lock (subscribers)
            {

                invocationList = subscribers.
                                     Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, Error, LastException)).
                                     ToList();

                if (Timeout.HasValue)
                    invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(invocationList);

        }

        #endregion

        #region WhenFirst  (ServerTimestamp, HTTPAPI, Request, Response, Error, LastException, VerifyResult, Timeout = null, DefaultResult = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <typeparam name="T">The type of the results.</typeparam>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="Error">An error message.</param>
        /// <param name="LastException">The last exception occured.</param>
        /// <param name="VerifyResult">A delegate to verify and filter results.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        /// <param name="DefaultResult">A default result in case of errors or a timeout.</param>
        public Task<T> WhenFirst<T>(DateTime            ServerTimestamp,
                                    HTTPAPI             HTTPAPI,
                                    HTTPRequest         Request,
                                    HTTPResponse        Response,
                                    String              Error,
                                    Exception           LastException,
                                    Func<T, Boolean>    VerifyResult,
                                    TimeSpan?           Timeout        = null,
                                    Func<TimeSpan, T>?  DefaultResult  = null)

            where T: notnull

        {

            #region Data

            List<Task>  invocationList;
            Task?       WorkDone;
            Task<T>?    Result;
            DateTime    StartTime     = Timestamp.Now;
            Task?       TimeoutTask   = null;

            #endregion

            lock (subscribers)
            {

                invocationList = subscribers.
                                     Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, Error, LastException)).
                                     ToList();

                if (Timeout.HasValue)
                    invocationList.Add(TimeoutTask = Task.Run(() => Thread.Sleep(Timeout.Value)));

            }

            do
            {

                try
                {

                    WorkDone = Task.WhenAny(invocationList);

                    invocationList.Remove(WorkDone);

                    if (WorkDone != TimeoutTask)
                    {

                        Result = WorkDone as Task<T>;

                        if (Result is not null &&
                            !EqualityComparer<T>.Default.Equals(Result.Result, default) &&
                            VerifyResult(Result.Result))
                        {
                            return Result;
                        }

                    }

                }
                catch (Exception e)
                {
                    DebugX.LogT(e.Message);
                    WorkDone = null;
                }

            }
            while (!(WorkDone == TimeoutTask || invocationList.Count == 0));

            return Task.FromResult((DefaultResult is not null
                                        ? DefaultResult(Timestamp.Now - StartTime)
                                        : default)!);

        }

        #endregion

        #region WhenAll    (ServerTimestamp, HTTPAPI, Request, Response, Error = null, LastException = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="Error">An error message.</param>
        /// <param name="LastException">The last exception occured.</param>
        public Task WhenAll(DateTime      ServerTimestamp,
                            HTTPAPI       HTTPAPI,
                            HTTPRequest   Request,
                            HTTPResponse  Response,
                            String?       Error          = null,
                            Exception?    LastException  = null)
        {

            Task[] invocationList;

            lock (subscribers)
            {
                invocationList = subscribers.
                                     Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, Error, LastException)).
                                     ToArray();
            }

            return Task.WhenAll(invocationList);

        }

        #endregion

    }

    #endregion


    /// <summary>
    /// Extension methods for the HTTP API.
    /// </summary>
    public static class HTTPAPIExtensions
    {

        #region AddJSONEventSource(this HTTPAPI, EventIdentification, ...)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        public static HTTPEventSource<JObject> AddJSONEventSource(this HTTPAPI                     HTTPAPI,
                                                                  HTTPEventSource_Id               EventIdentification,
                                                                  UInt32                           MaxNumberOfCachedEvents      = 500,
                                                                  TimeSpan?                        RetryIntervall               = null,
                                                                  Boolean                          EnableLogging                = true,
                                                                  String?                          LogfilePath                  = null,
                                                                  String?                          LogfilePrefix                = null,
                                                                  Func<String, DateTime, String>?  LogfileName                  = null,
                                                                  String?                          LogfileReloadSearchPattern   = null)

            => HTTPAPI.AddEventSource(
                           EventIdentification,
                           MaxNumberOfCachedEvents,
                           RetryIntervall,
                           data => data.ToString(Newtonsoft.Json.Formatting.None),
                           JObject.Parse,
                           EnableLogging,
                           LogfilePath,
                           LogfilePrefix,
                           LogfileName,
                           LogfileReloadSearchPattern
                       );

        #endregion

        #region AddJSONEventSource(this HTTPAPI, EventIdentification, URLTemplate, ...)

        /// <summary>
        /// Add a HTTP Sever Sent Events source and a method call back for the given URL template.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// 
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="IncludeFilterAtRuntime">Include this events within the HTTP SSE output. Can e.g. be used to filter events by HTTP users.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public static HTTPEventSource<JObject> AddJSONEventSource(this HTTPAPI                        HTTPAPI,
                                                                  HTTPEventSource_Id                  EventIdentification,
                                                                  HTTPPath                            URLTemplate,

                                                                  UInt32                              MaxNumberOfCachedEvents      = 500,
                                                                  Func<HTTPEvent<JObject>, Boolean>?  IncludeFilterAtRuntime       = null,
                                                                  TimeSpan?                           RetryIntervall               = null,
                                                                  Boolean                             EnableLogging                = false,
                                                                  String?                             LogfilePath                  = null,
                                                                  String?                             LogfilePrefix                = null,
                                                                  Func<String, DateTime, String>?     LogfileName                  = null,
                                                                  String?                             LogfileReloadSearchPattern   = null,

                                                                  HTTPHostname?                       Hostname                     = null,
                                                                  HTTPMethod?                         HTTPMethod                   = null,
                                                                  HTTPContentType?                    HTTPContentType              = null,

                                                                  HTTPAuthentication?                 URIAuthentication            = null,
                                                                  HTTPAuthentication?                 HTTPMethodAuthentication     = null,

                                                                  HTTPDelegate?                       DefaultErrorHandler          = null)

            => HTTPAPI.AddEventSource(
                           EventIdentification,
                           URLTemplate,

                           MaxNumberOfCachedEvents,
                           IncludeFilterAtRuntime,
                           RetryIntervall,
                           data => data.ToString(Newtonsoft.Json.Formatting.None),
                           JObject.Parse,
                           EnableLogging,
                           LogfilePath,
                           LogfilePrefix,
                           LogfileName,
                           LogfileReloadSearchPattern,

                           Hostname,
                           HTTPMethod,
                           HTTPContentType,

                           false,
                           URIAuthentication,
                           HTTPMethodAuthentication,

                           DefaultErrorHandler
                       );

        #endregion

    }


    /// <summary>
    /// A HTTP API.
    /// </summary>
    public class HTTPAPI : AHTTPAPIBase,
                           IServerStartStop
    {

        #region Data

        /// <summary>
        /// ASCII unit/cell separator
        /// </summary>
        protected const Char US = (Char) 0x1F;

        /// <summary>
        /// ASCII record/row separator
        /// </summary>
        protected const Char RS = (Char) 0x1E;

        /// <summary>
        /// ASCII group separator
        /// </summary>
        protected const Char GS = (Char) 0x1D;


        /// <summary>
        /// The default HTTP service name.
        /// </summary>
        public  const              String                  DefaultHTTPServiceName          = "GraphDefined HTTP API";

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public  const              String                  DefaultHTTPServerName           = DefaultHTTPServiceName;

        /// <summary>
        /// The default HTTP server port.
        /// </summary>
        public  static readonly    IPPort                  DefaultHTTPServerPort           = IPPort.HTTP;

        /// <summary>
        /// The default HTTP URL path prefix.
        /// </summary>
        public  static readonly    HTTPPath                DefaultURLPathPrefix            = HTTPPath.Parse("/");

        /// <summary>
        /// The HTTP root for embedded ressources.
        /// </summary>
        public  const              String                  HTTPRoot                        = "org.GraphDefined.Vanaheimr.Hermod.HTTPRoot.";

        /// <summary>
        /// The default maintenance interval.
        /// </summary>
        public           readonly  TimeSpan                DefaultMaintenanceEvery         = TimeSpan.FromMinutes(1);

        private          readonly  Timer                   MaintenanceTimer;

        protected static readonly  TimeSpan                SemaphoreSlimTimeout            = TimeSpan.FromSeconds(5);

        protected static readonly  SemaphoreSlim           MaintenanceSemaphore            = new (1, 1);

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP server of the API.
        /// </summary>
        public HTTPServer               HTTPServer                  { get; }

        /// <summary>
        /// The HTTP hostname for all URIs within this API.
        /// </summary>
        public HTTPHostname             Hostname                    { get; }

        /// <summary>
        /// The offical URL/DNS name of this service, e.g. for sending e-mails.
        /// </summary>
        public String                   ExternalDNSName             { get; }

        /// <summary>
        /// The default request timeout for incoming HTTP requests.
        /// </summary>
        public TimeSpan                 DefaultRequestTimeout       { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// The API version hash (git commit hash value).
        /// </summary>
        public String?                  APIVersionHash              { get; }


        public ApplicationRunTypes      RunType                     { get; }

        /// <summary>
        /// The unqiue identification of this HTTP API instance.
        /// </summary>
        public System_Id                SystemId                    { get; }


        public X509Certificate?         ServerCert                  { get; }



        /// <summary>
        /// Whether the reload of the system is finished.
        /// </summary>
        public Boolean                  ReloadFinished              { get; protected set; }

        /// <summary>
        /// The maintenance interval.
        /// </summary>
        public TimeSpan                 MaintenanceEvery            { get; }

        /// <summary>
        /// Disable all maintenance tasks.
        /// </summary>
        public Boolean                  DisableMaintenanceTasks     { get; set; }





        public String                   HTTPRequestsPath            { get; }

        public String                   HTTPResponsesPath           { get; }

        public String                   HTTPSSEsPath                { get; }

        public String                   MetricsPath                 { get; }




        /// <summary>
        /// The HTTP logger.
        /// </summary>
        public HTTPServerLogger?        HTTPLogger                  { get; set; }


        public Warden.Warden            Warden                      { get; }

        public ECPrivateKeyParameters?  ServiceCheckPrivateKey      { get; set; }

        public ECPublicKeyParameters?   ServiceCheckPublicKey       { get; set; }


        public DNSClient                DNSClient
            => HTTPServer.DNSClient;

        #endregion

        #region Events

        /// <summary>
        /// An event called whenever a HTTP request came in.
        /// </summary>
        public HTTPRequestLogEvent   RequestLog    = new ();

        /// <summary>
        /// An event called whenever a HTTP request could successfully be processed.
        /// </summary>
        public HTTPResponseLogEvent  ResponseLog   = new ();

        /// <summary>
        /// An event called whenever a HTTP request resulted in an error.
        /// </summary>
        public HTTPErrorLogEvent     ErrorLog      = new ();

        #endregion

        #region Constructor(s)

        #region HTTPAPI(HTTPHostname = null, ...)

        /// <summary>
        /// Create a new HTTP API.
        /// </summary>
        /// <param name="HTTPHostname">The HTTP hostname for all URLs within this API.</param>
        /// <param name="ExternalDNSName">The offical URL/DNS name of this service, e.g. for sending e-mails.</param>
        /// <param name="HTTPServerPort">A TCP port to listen on.</param>
        /// <param name="BasePath">When the API is served from an optional subdirectory path.</param>
        /// <param name="HTTPServerName">The default HTTP servername, used whenever no HTTP Host-header has been given.</param>
        /// 
        /// <param name="URLPathPrefix">A common prefix for all URLs.</param>
        /// <param name="HTTPServiceName">The name of the HTTP service.</param>
        /// <param name="HTMLTemplate">An optional HTML template.</param>
        /// <param name="APIVersionHashes">The API version hashes (git commit hash values).</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="ServerThreadNameCreator">Sets the optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPrioritySetter">Sets the optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// 
        /// <param name="DisableMaintenanceTasks">Disable all maintenance tasks.</param>
        /// <param name="MaintenanceInitialDelay">The initial delay of the maintenance tasks.</param>
        /// <param name="MaintenanceEvery">The maintenance intervall.</param>
        /// 
        /// <param name="DisableWardenTasks">Disable all warden tasks.</param>
        /// <param name="WardenInitialDelay">The initial delay of the warden tasks.</param>
        /// <param name="WardenCheckEvery">The warden intervall.</param>
        /// 
        /// <param name="IsDevelopment">This HTTP API runs in development mode.</param>
        /// <param name="DevelopmentServers">An enumeration of server names which will imply to run this service in development mode.</param>
        /// <param name="DisableLogging">Disable any logging.</param>
        /// <param name="LoggingPath">The path for all logfiles.</param>
        /// <param name="LogfileName">The name of the logfile.</param>
        /// <param name="LogfileCreator">A delegate for creating the name of the logfile for this API.</param>
        /// <param name="DNSClient">The DNS client of the API.</param>
        /// <param name="AutoStart">Whether to start the API automatically.</param>
        public HTTPAPI(HTTPHostname?                                              HTTPHostname                 = null,
                       String?                                                    ExternalDNSName              = null,
                       IPPort?                                                    HTTPServerPort               = null,
                       HTTPPath?                                                  BasePath                     = null,
                       String?                                                    HTTPServerName               = DefaultHTTPServerName,

                       HTTPPath?                                                  URLPathPrefix                = null,
                       String?                                                    HTTPServiceName              = DefaultHTTPServiceName,
                       String?                                                    HTMLTemplate                 = null,
                       JObject?                                                   APIVersionHashes             = null,

                       ServerCertificateSelectorDelegate?                         ServerCertificateSelector    = null,
                       RemoteTLSClientCertificateValidationHandler<IHTTPServer>?  ClientCertificateValidator   = null,
                       LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                       SslProtocols?                                              AllowedTLSProtocols          = null,
                       Boolean?                                                   ClientCertificateRequired    = null,
                       Boolean?                                                   CheckCertificateRevocation   = null,

                       ServerThreadNameCreatorDelegate?                           ServerThreadNameCreator      = null,
                       ServerThreadPriorityDelegate?                              ServerThreadPrioritySetter   = null,
                       Boolean?                                                   ServerThreadIsBackground     = null,
                       ConnectionIdBuilder?                                       ConnectionIdBuilder          = null,
                       TimeSpan?                                                  ConnectionTimeout            = null,
                       UInt32?                                                    MaxClientConnections         = null,

                       Boolean?                                                   DisableMaintenanceTasks      = null,
                       TimeSpan?                                                  MaintenanceInitialDelay      = null,
                       TimeSpan?                                                  MaintenanceEvery             = null,

                       Boolean?                                                   DisableWardenTasks           = null,
                       TimeSpan?                                                  WardenInitialDelay           = null,
                       TimeSpan?                                                  WardenCheckEvery             = null,

                       Boolean?                                                   IsDevelopment                = null,
                       IEnumerable<String>?                                       DevelopmentServers           = null,
                       Boolean?                                                   DisableLogging               = null,
                       String?                                                    LoggingPath                  = null,
                       String?                                                    LogfileName                  = null,
                       LogfileCreatorDelegate?                                    LogfileCreator               = null,
                       DNSClient?                                                 DNSClient                    = null,
                       Boolean                                                    AutoStart                    = false)

            : this(new HTTPServer(
                       HTTPServerPort ?? DefaultHTTPServerPort,
                       HTTPServerName ?? DefaultHTTPServerName,
                       HTTPServiceName,

                       ServerCertificateSelector,
                       ClientCertificateValidator,
                       LocalCertificateSelector,
                       AllowedTLSProtocols,
                       ClientCertificateRequired,
                       CheckCertificateRevocation,

                       ServerThreadNameCreator,
                       ServerThreadPrioritySetter,
                       ServerThreadIsBackground,
                       ConnectionIdBuilder,
                       ConnectionTimeout,
                       MaxClientConnections,

                       DNSClient,
                       AutoStart: false
                   ),

                   HTTPHostname,
                   ExternalDNSName,
                   HTTPServiceName ?? DefaultHTTPServiceName,

                   BasePath,
                   URLPathPrefix   ?? DefaultURLPathPrefix,
                   HTMLTemplate,
                   APIVersionHashes,

                   DisableMaintenanceTasks,
                   MaintenanceInitialDelay,
                   MaintenanceEvery,

                   DisableWardenTasks,
                   WardenInitialDelay,
                   WardenCheckEvery,

                   IsDevelopment,
                   DevelopmentServers,
                   DisableLogging,
                   LoggingPath,
                   LogfileName,
                   LogfileCreator,
                   AutoStart: false)

        {

            if (AutoStart)
                HTTPServer.Start(EventTracking_Id.New).Wait();

            //if (AutoStart && HTTPServer.Start())
            //    DebugX.Log(nameof(HTTPAPI) + $" version '{APIVersionHash}' started...");

        }

        #endregion

        #region HTTPAPI(HTTPServer, HTTPHostname = null, ...)

        /// <summary>
        /// Create a new HTTP API.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="HTTPHostname">An optional HTTP hostname.</param>
        /// <param name="ExternalDNSName">The offical URL/DNS name of this service, e.g. for sending e-mails.</param>
        /// <param name="HTTPServiceName">An optional name of the HTTP API service.</param>
        /// <param name="BasePath">When the API is served from an optional subdirectory path.</param>
        /// 
        /// <param name="URLPathPrefix">An optional URL path prefix, used when defining URL templates.</param>
        /// <param name="HTMLTemplate">An optional HTML template.</param>
        /// <param name="APIVersionHashes">The API version hashes (git commit hash values).</param>
        /// 
        /// <param name="DisableMaintenanceTasks">Disable all maintenance tasks.</param>
        /// <param name="MaintenanceInitialDelay">The initial delay of the maintenance tasks.</param>
        /// <param name="MaintenanceEvery">The maintenance intervall.</param>
        /// 
        /// <param name="DisableWardenTasks">Disable all warden tasks.</param>
        /// <param name="WardenInitialDelay">The initial delay of the warden tasks.</param>
        /// <param name="WardenCheckEvery">The warden intervall.</param>
        /// 
        /// <param name="IsDevelopment">This HTTP API runs in development mode.</param>
        /// <param name="DevelopmentServers">An enumeration of server names which will imply to run this service in development mode.</param>
        /// <param name="DisableLogging">Disable the log file.</param>
        /// <param name="LoggingPath">The path for all logfiles.</param>
        /// <param name="LogfileName">The name of the logfile.</param>
        /// <param name="LogfileCreator">A delegate for creating the name of the logfile for this API.</param>
        /// 
        /// <param name="AutoStart">Whether to start the API automatically.</param>
        public HTTPAPI(HTTPServer               HTTPServer,
                       HTTPHostname?            HTTPHostname              = null,
                       String?                  ExternalDNSName           = "",
                       String?                  HTTPServiceName           = DefaultHTTPServiceName,
                       HTTPPath?                BasePath                  = null,

                       HTTPPath?                URLPathPrefix             = null,
                       String?                  HTMLTemplate              = null,
                       JObject?                 APIVersionHashes          = null,

                       Boolean?                 DisableMaintenanceTasks   = false,
                       TimeSpan?                MaintenanceInitialDelay   = null,
                       TimeSpan?                MaintenanceEvery          = null,

                       Boolean?                 DisableWardenTasks        = false,
                       TimeSpan?                WardenInitialDelay        = null,
                       TimeSpan?                WardenCheckEvery          = null,

                       Boolean?                 IsDevelopment             = false,
                       IEnumerable<String>?     DevelopmentServers        = null,
                       Boolean?                 DisableLogging            = false,
                       String?                  LoggingPath               = null,
                       String?                  LogfileName               = DefaultHTTPAPI_LogfileName,
                       LogfileCreatorDelegate?  LogfileCreator            = null,

                       Boolean                  AutoStart                 = false)

            : base(HTTPServiceName ?? DefaultHTTPServiceName,
                   URLPathPrefix,
                   BasePath,
                   HTMLTemplate,

                   IsDevelopment,
                   DevelopmentServers,
                   DisableLogging,
                   LoggingPath,
                   LogfileName,
                   LogfileCreator)

        {

            this.HTTPServer               = HTTPServer      ?? throw new ArgumentNullException(nameof(HTTPServer), "The given HTTP server must not be null!");
            this.Hostname                 = HTTPHostname    ?? HTTP.HTTPHostname.Any;
            this.ExternalDNSName          = ExternalDNSName ?? "";

            this.APIVersionHash           = APIVersionHashes?[nameof(HTTPAPI)]?.Value<String>()?.Trim();

            this.HTTPRequestsPath         = this.LoggingPath + "HTTPRequests"   + Path.DirectorySeparatorChar;
            this.HTTPResponsesPath        = this.LoggingPath + "HTTPResponses"  + Path.DirectorySeparatorChar;
            this.HTTPSSEsPath             = this.LoggingPath + "HTTPSSEs"       + Path.DirectorySeparatorChar;
            this.MetricsPath              = this.LoggingPath + "Metrics"        + Path.DirectorySeparatorChar;

            this.RunType                  = ApplicationRunType.GetRunType();
            this.SystemId                 = System_Id.Parse(Environment.MachineName.Replace("/", "") + "/" + HTTPServer.DefaultHTTPServerPort);

            if (this.DisableLogging == false)
            {
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.LoggingPath));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.HTTPRequestsPath));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.HTTPResponsesPath));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.HTTPSSEsPath));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.MetricsPath));
            }


            // Link HTTP events...
            HTTPServer.RequestLog   += RequestLog. WhenAll;
            HTTPServer.ResponseLog  += ResponseLog.WhenAll;
            HTTPServer.ErrorLog     += ErrorLog.   WhenAll;


            // Setup Maintenance Task
            this.DisableMaintenanceTasks  = DisableMaintenanceTasks ?? false;
            this.MaintenanceEvery         = MaintenanceEvery        ?? DefaultMaintenanceEvery;
            this.MaintenanceTimer         = new Timer(
                                                DoMaintenanceSync,
                                                this,
                                                MaintenanceInitialDelay ?? this.MaintenanceEvery,
                                                this.MaintenanceEvery
                                            );

            // Setup Warden
            this.Warden = new Warden.Warden(
                              WardenInitialDelay ?? TimeSpan.FromMinutes(3),
                              WardenCheckEvery   ?? TimeSpan.FromMinutes(1),
                              DNSClient
                          );

            #region Warden: Observe CPU/RAM

            Thread.CurrentThread.CurrentCulture   = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            //if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            //{

            //    //// If those lines fail, try to run "lodctr /R" as administrator in an cmd.exe environment
            //    //totalRAM_PerformanceCounter = new PerformanceCounter("Process", "Working Set",      Process.GetCurrentProcess().ProcessName);
            //    //totalCPU_PerformanceCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
            //    //totalRAM_PerformanceCounter.NextValue();
            //    //totalCPU_PerformanceCounter.NextValue();

            //    Warden.EveryMinutes(1,
            //                        Process.GetCurrentProcess(),
            //                        async (timestamp, process, ct) => {
            //                            using (var writer = File.AppendText(String.Concat(this.MetricsPath,
            //                                                                              Path.DirectorySeparatorChar,
            //                                                                              "process-stats_",
            //                                                                              DateTime.Now.Year, "-",
            //                                                                              DateTime.Now.Month.ToString("D2"),
            //                                                                              ".log")))
            //                            {

            //                                await writer.WriteLineAsync(String.Concat(timestamp.ToIso8601(), ";",
            //                                                                          process.VirtualMemorySize64, ";",
            //                                                                          process.WorkingSet64, ";",
            //                                                                          process.TotalProcessorTime, ";",
            //                                                                          totalRAM_PerformanceCounter.NextValue() / 1024 / 1024, ";",
            //                                                                          totalCPU_PerformanceCounter.NextValue())).
            //                                             ConfigureAwait(false);

            //                            }

            //                        });

            //    Warden.EveryMinutes(15,
            //                        Environment.OSVersion.Platform == PlatformID.Unix
            //                            ? new DriveInfo("/")
            //                            : new DriveInfo(Directory.GetCurrentDirectory()),
            //                        async (timestamp, driveInfo, ct) => {
            //                            using (var writer = File.AppendText(String.Concat(this.MetricsPath,
            //                                                                              Path.DirectorySeparatorChar,
            //                                                                              "disc-stats_",
            //                                                                              DateTime.Now.Year, "-",
            //                                                                              DateTime.Now.Month.ToString("D2"),
            //                                                                              ".log")))
            //                            {

            //                                var MBytesFree       = driveInfo.AvailableFreeSpace / 1024 / 1024;
            //                                var HDPercentageFree = 100 * driveInfo.AvailableFreeSpace / driveInfo.TotalSize;

            //                                await writer.WriteLineAsync(String.Concat(timestamp.ToIso8601(), ";",
            //                                                                          MBytesFree, ";",
            //                                                                          HDPercentageFree)).
            //                                             ConfigureAwait(false);

            //                            }

            //                        });

            //}

            #endregion


            if (AutoStart)
                HTTPServer.Start(EventTracking_Id.New).Wait();

            //if (AutoStart == true && HTTPServer.Start())
            //    DebugX.Log(nameof(HTTPAPI) + $" version '{APIVersionHash}' started...");

        }

        #endregion

        #region HTTPAPI(HTTPAPI)

        public HTTPAPI(HTTPAPI HTTPAPI)

            : this(HTTPServer:               HTTPAPI.HTTPServer,
                   HTTPHostname:             HTTPAPI.Hostname,
                   ExternalDNSName:          HTTPAPI.ExternalDNSName,
                   HTTPServiceName:          HTTPAPI.HTTPServiceName,
                   BasePath:                 HTTPAPI.BasePath,

                   URLPathPrefix:            HTTPAPI.URLPathPrefix,
                   HTMLTemplate:             HTTPAPI.HTMLTemplate,
                   APIVersionHashes:         null,

                   DisableMaintenanceTasks:  HTTPAPI.DisableMaintenanceTasks,
                   MaintenanceInitialDelay:  null,
                   MaintenanceEvery:         HTTPAPI.MaintenanceEvery,

                   DisableWardenTasks:       null, //HTTPAPI.DisableWardenTasks,
                   WardenInitialDelay:       null,
                   WardenCheckEvery:         null, //HTTPAPI.WardenCheckEvery,

                   IsDevelopment:            HTTPAPI.IsDevelopment,
                   DevelopmentServers:       HTTPAPI.DevelopmentServers,
                   DisableLogging:           HTTPAPI.DisableLogging,
                   LoggingPath:              HTTPAPI.LoggingPath,
                   LogfileName:              HTTPAPI.LogfileName,
                   LogfileCreator:           HTTPAPI.LogfileCreator)

        { }

        #endregion

        #endregion


        #region Add Methon Callbacks

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate,  HTTPContentType = null, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname             Hostname,
                                      HTTPMethod               HTTPMethod,
                                      HTTPPath                 URLTemplate,
                                      HTTPContentType?         HTTPContentType             = null,
                                      Boolean                  OpenEnd                     = false,
                                      HTTPAuthentication?      URLAuthentication           = null,
                                      HTTPAuthentication?      HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?      ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?  HTTPResponseLogger          = null,
                                      HTTPDelegate?            DefaultErrorHandler         = null,
                                      HTTPDelegate?            HTTPDelegate                = null,
                                      URLReplacement           AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial checks

            if (URLTemplate.IsNullOrEmpty)
                throw new ArgumentNullException(nameof(URLTemplate),   "The given URL template must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),  "The given HTTP delegate must not be null!");

            #endregion

            HTTPServer.AddMethodCallback(
                this,
                Hostname,
                HTTPMethod,
                URLTemplate,
                HTTPContentType,
                OpenEnd,
                URLAuthentication,
                HTTPMethodAuthentication,
                ContentTypeAuthentication,
                HTTPRequestLogger,
                HTTPResponseLogger,
                DefaultErrorHandler,
                HTTPDelegate,
                AllowReplacement
            );

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplates, HTTPContentType = null, ..., HTTPDelegate = null, AllowReplacement = URLReplacement.Fail)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplates">An enumeration of URL templates.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname             Hostname,
                                      HTTPMethod               HTTPMethod,
                                      IEnumerable<HTTPPath>    URLTemplates,
                                      HTTPContentType?         HTTPContentType             = null,
                                      Boolean                  OpenEnd                     = false,
                                      HTTPAuthentication?      URLAuthentication           = null,
                                      HTTPAuthentication?      HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?      ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?  HTTPResponseLogger          = null,
                                      HTTPDelegate?            DefaultErrorHandler         = null,
                                      HTTPDelegate?            HTTPDelegate                = null,
                                      URLReplacement           AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial checks

            if (!URLTemplates.SafeAny())
                throw new ArgumentNullException(nameof(URLTemplates),  "The given URL template must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),  "The given HTTP delegate must not be null!");

            #endregion

            HTTPServer.AddMethodCallback(
                this,
                Hostname,
                HTTPMethod,
                URLTemplates,
                HTTPContentType,
                OpenEnd,
                URLAuthentication,
                HTTPMethodAuthentication,
                ContentTypeAuthentication,
                HTTPRequestLogger,
                HTTPResponseLogger,
                DefaultErrorHandler,
                HTTPDelegate,
                AllowReplacement
            );

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate,  HTTPContentTypes, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPAPI                       HTTPAPI,
                                      HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      HTTPPath                      URLTemplate,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      Boolean                       OpenEnd                     = false,
                                      HTTPAuthentication?           URLAuthentication           = null,
                                      HTTPAuthentication?           HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?           ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?        HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?       HTTPResponseLogger          = null,
                                      HTTPDelegate?                 DefaultErrorHandler         = null,
                                      HTTPDelegate?                 HTTPDelegate                = null,
                                      URLReplacement                AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial checks

            if (URLTemplate.IsNullOrEmpty)
                throw new ArgumentNullException(nameof(URLTemplate),       "The given URL template must not be null or empty!");

            if (!HTTPContentTypes.Any())
                throw new ArgumentNullException(nameof(HTTPContentTypes),  "The given content types must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),      "The given HTTP delegate must not be null!");

            #endregion

            HTTPServer.AddMethodCallback(
                this,
                Hostname,
                HTTPMethod,
                URLTemplate,
                HTTPContentTypes,
                OpenEnd,
                URLAuthentication,
                HTTPMethodAuthentication,
                ContentTypeAuthentication,
                HTTPRequestLogger,
                HTTPResponseLogger,
                DefaultErrorHandler,
                HTTPDelegate,
                AllowReplacement
            );

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate,  HTTPContentTypes, HostAuthentication = false, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplates">An enumeration of URL templates.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPAPI                       HTTPAPI,
                                      HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      IEnumerable<HTTPPath>         URLTemplates,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      Boolean                       OpenEnd                     = false,
                                      HTTPAuthentication?           URLAuthentication           = null,
                                      HTTPAuthentication?           HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?           ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?        HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?       HTTPResponseLogger          = null,
                                      HTTPDelegate?                 DefaultErrorHandler         = null,
                                      HTTPDelegate?                 HTTPDelegate                = null,
                                      URLReplacement                AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial checks

            if (!URLTemplates.Any())
                throw new ArgumentNullException(nameof(URLTemplates),      "The given URL template must not be null or empty!");

            if (!HTTPContentTypes.Any())
                throw new ArgumentNullException(nameof(HTTPContentTypes),  "The given content types must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),      "The given HTTP delegate must not be null!");

            #endregion

            HTTPServer.AddMethodCallback(
                this,
                Hostname,
                HTTPMethod,
                URLTemplates,
                HTTPContentTypes,
                OpenEnd,
                URLAuthentication,
                HTTPMethodAuthentication,
                ContentTypeAuthentication,
                HTTPRequestLogger,
                HTTPResponseLogger,
                DefaultErrorHandler,
                HTTPDelegate,
                AllowReplacement
            );

        }

        #endregion

        #endregion

        #region HTTP Server Sent Events

        #region AddEventSource(EventIdentification,              MaxNumberOfCachedEvents = 500, RetryIntervall = null, LogfileName = null)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        /// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        public HTTPEventSource<TData> AddEventSource<TData>(HTTPEventSource_Id               EventIdentification,
                                                            UInt32                           MaxNumberOfCachedEvents      = 500,
                                                            TimeSpan?                        RetryIntervall               = null,
                                                            Func<TData, String>?             DataSerializer               = null,
                                                            Func<String, TData>?             DataDeserializer             = null,
                                                            Boolean                          EnableLogging                = true,
                                                            String?                          LogfilePath                  = null,
                                                            String?                          LogfilePrefix                = null,
                                                            Func<String, DateTime, String>?  LogfileName                  = null,
                                                            String?                          LogfileReloadSearchPattern   = null)

            => HTTPServer.AddEventSource(
                              EventIdentification,
                              this,
                              MaxNumberOfCachedEvents,
                              RetryIntervall,
                              DataSerializer,
                              DataDeserializer,
                              EnableLogging,
                              LogfilePath,
                              LogfilePrefix,
                              LogfileName,
                              LogfileReloadSearchPattern
                          );

        #endregion

        #region AddEventSource(EventIdentification, URITemplate, MaxNumberOfCachedEvents = 500, RetryIntervall = null, LogfileName = null, ...)

        /// <summary>
        /// Add a HTTP Sever Sent Events source and a method call back for the given URI template.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// 
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="IncludeFilterAtRuntime">Include this events within the HTTP SSE output. Can e.g. be used to filter events by HTTP users.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        /// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        /// <param name="EnableLogging">Whether to enable event logging.</param>
        /// <param name="LogfilePrefix">The prefix of the logfile names.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="HttpMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public HTTPEventSource<T> AddEventSource<T>(HTTPEventSource_Id               EventIdentification,
                                                    HTTPPath                         URITemplate,

                                                    UInt32                           MaxNumberOfCachedEvents      = 500,
                                                    Func<HTTPEvent<T>, Boolean>?     IncludeFilterAtRuntime       = null,
                                                    TimeSpan?                        RetryIntervall               = null,
                                                    Func<T, String>?                 DataSerializer               = null,
                                                    Func<String, T>?                 DataDeserializer             = null,
                                                    Boolean                          EnableLogging                = true,
                                                    String?                          LogfilePath                  = null,
                                                    String?                          LogfilePrefix                = null,
                                                    Func<String, DateTime, String>?  LogfileName                  = null,
                                                    String?                          LogfileReloadSearchPattern   = null,

                                                    HTTPHostname?                    Hostname                     = null,
                                                    HTTPMethod?                      HttpMethod                   = null,
                                                    HTTPContentType?                 HTTPContentType              = null,

                                                    Boolean                          RequireAuthentication        = true,
                                                    HTTPAuthentication?              URIAuthentication            = null,
                                                    HTTPAuthentication?              HTTPMethodAuthentication     = null,

                                                    HTTPDelegate?                    DefaultErrorHandler          = null)

            => HTTPServer.AddEventSource(
                              EventIdentification,
                              this,
                              URITemplate,

                              MaxNumberOfCachedEvents,
                              IncludeFilterAtRuntime,
                              RetryIntervall,
                              DataSerializer,
                              DataDeserializer,
                              EnableLogging,
                              LogfilePath,
                              LogfilePrefix,
                              LogfileName,
                              LogfileReloadSearchPattern,

                              Hostname,
                              HttpMethod,
                              HTTPContentType,

                              RequireAuthentication,
                              URIAuthentication,
                              HTTPMethodAuthentication,

                              DefaultErrorHandler
                          );

        #endregion


        #region Get   (EventSourceIdentification)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public IHTTPEventSource Get(HTTPEventSource_Id EventSourceIdentification)

            => HTTPServer.Get(EventSourceIdentification);


        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public IHTTPEventSource<TData> Get<TData>(HTTPEventSource_Id EventSourceIdentification)

            => HTTPServer.Get<TData>(EventSourceIdentification);

        #endregion

        #region TryGet(EventSourceIdentification, out EventSource)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        public Boolean TryGet(HTTPEventSource_Id EventSourceIdentification, out IHTTPEventSource EventSource)

            => HTTPServer.TryGet(EventSourceIdentification, out EventSource);


        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        public Boolean TryGet<TData>(HTTPEventSource_Id EventSourceIdentification, out IHTTPEventSource<TData> EventSource)

            => HTTPServer.TryGet(EventSourceIdentification, out EventSource);

        #endregion

        #region EventSources(IncludeEventSource = null)

        /// <summary>
        /// Return a filtered enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        public IEnumerable<IHTTPEventSource> EventSources(Func<IHTTPEventSource, Boolean>? IncludeEventSource = null)

            => HTTPServer.EventSources(IncludeEventSource);


        /// <summary>
        /// Return a filtered enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        public IEnumerable<IHTTPEventSource<TData>> EventSources<TData>(Func<IHTTPEventSource, Boolean>? IncludeEventSource = null)

            => HTTPServer.EventSources<TData>(IncludeEventSource);

        #endregion

        #endregion


        #region (Timer) DoMaintenance(State)

        private void DoMaintenanceSync(Object? State)
        {
            if (ReloadFinished && !DisableMaintenanceTasks)
                DoMaintenanceAsync(State).ConfigureAwait(false);
        }

        protected internal virtual Task DoMaintenance(Object? State)
            => Task.CompletedTask;

        private async Task DoMaintenanceAsync(Object? State)
        {

            if (await MaintenanceSemaphore.WaitAsync(SemaphoreSlimTimeout).
                                           ConfigureAwait(false))
            {
                try
                {

                    await DoMaintenance(State);

                }
                catch (Exception e)
                {

                    while (e.InnerException is not null)
                        e = e.InnerException;

                    DebugX.LogException(e);

                }
                finally
                {
                    MaintenanceSemaphore.Release();
                }
            }
            else
                DebugX.LogT("Could not aquire the maintenance tasks lock!");

        }

        #endregion


        #region Start    (EventTrackingId = null)

        /// <summary>
        /// Start this HTTP API.
        /// </summary>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        public async virtual Task<Boolean> Start(EventTracking_Id? EventTrackingId = null)
        {

            var result = await HTTPServer.Start(
                                   EventTrackingId ?? EventTracking_Id.New
                               );

            //SendStarted(this, CurrentTimestamp);

            return result;

        }

        #endregion

        #region Start    (Delay, EventTrackingId = null, InBackground = true)

        /// <summary>
        /// Start the server after a little delay.
        /// </summary>
        /// <param name="Delay">The delay.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="InBackground">Whether to wait on the main thread or in a background thread.</param>
        public async virtual Task<Boolean> Start(TimeSpan           Delay,
                                                 EventTracking_Id?  EventTrackingId   = null,
                                                 Boolean            InBackground      = true)
        {

            var result = await HTTPServer.Start(
                                   Delay,
                                   EventTrackingId ?? EventTracking_Id.New,
                                   InBackground
                               );

            //SendStarted(this, CurrentTimestamp);

            return result;

        }

        #endregion

        #region Shutdown (EventTrackingId = null, Message = null, Wait = true)

        /// <summary>
        /// Shutdown this HTTP API.
        /// </summary>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Whether to wait for the shutdown to complete.</param>
        public async virtual Task<Boolean> Shutdown(EventTracking_Id?  EventTrackingId   = null,
                                                    String?            Message           = null,
                                                    Boolean            Wait              = true)
        {

            var result = await HTTPServer.Shutdown(
                                   EventTrackingId ?? EventTracking_Id.New,
                                   Message,
                                   Wait
                               );

            //SendShutdown(this, CurrentTimestamp);

            return result;

        }

        #endregion


        #region Dispose()

        public virtual void Dispose()
        {
            HTTPServer.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion


    }

}
