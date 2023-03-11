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

using System.Reflection;
using System.Diagnostics;
using System.Net.Security;
using System.Globalization;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Warden;
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

        private readonly List<HTTPRequestLogHandler> subscribers;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about incoming HTTP requests.
        /// </summary>
        public HTTPRequestLogEvent()
        {
            subscribers = new List<HTTPRequestLogHandler>();
        }

        #endregion


        #region + / Add

        public static HTTPRequestLogEvent operator + (HTTPRequestLogEvent e, HTTPRequestLogHandler callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                e = new HTTPRequestLogEvent();

            lock (e.subscribers)
            {
                e.subscribers.Add(callback);
            }

            return e;

        }

        public HTTPRequestLogEvent Add(HTTPRequestLogHandler callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

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

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                return null;

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPRequestLogEvent Remove(HTTPRequestLogHandler callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

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

            HTTPRequestLogHandler[] _invocationList;

            lock (subscribers)
            {
                _invocationList = subscribers.ToArray();
            }

            foreach (var callback in _invocationList)
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
                            TimeSpan?     Timeout = null)
        {

            List<Task> _invocationList;

            lock (subscribers)
            {

                _invocationList = subscribers.
                                        Select(callback => callback(ServerTimestamp, HTTPAPI, Request)).
                                        ToList();

                if (Timeout.HasValue)
                    _invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(_invocationList);

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
        public Task<T> WhenFirst<T>(DateTime           ServerTimestamp,
                                    HTTPAPI            HTTPAPI,
                                    HTTPRequest        Request,
                                    Func<T, Boolean>   VerifyResult,
                                    TimeSpan?          Timeout        = null,
                                    Func<TimeSpan, T>  DefaultResult  = null)
        {

            #region Data

            List<Task>  _invocationList;
            Task        WorkDone;
            Task<T>     Result;
            DateTime    StartTime    = DateTime.UtcNow;
            Task        TimeoutTask  = null;

            #endregion

            lock (subscribers)
            {

                _invocationList = subscribers.
                                        Select(callback => callback(ServerTimestamp, HTTPAPI, Request)).
                                        ToList();

                if (Timeout.HasValue)
                    _invocationList.Add(TimeoutTask = Task.Run(() => System.Threading.Thread.Sleep(Timeout.Value)));

            }

            do
            {

                try
                {

                    WorkDone = Task.WhenAny(_invocationList);

                    _invocationList.Remove(WorkDone);

                    if (WorkDone != TimeoutTask)
                    {

                        Result = WorkDone as Task<T>;

                        if (Result != null &&
                            !EqualityComparer<T>.Default.Equals(Result.Result, default(T)) &&
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
            while (!(WorkDone == TimeoutTask || _invocationList.Count == 0));

            return Task.FromResult(DefaultResult(DateTime.UtcNow - StartTime));

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

            Task[] _invocationList;

            lock (subscribers)
            {
                _invocationList = subscribers.
                                        Select(callback => callback(ServerTimestamp, HTTPAPI, Request)).
                                        ToArray();
            }

            return Task.WhenAll(_invocationList);

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

        private readonly List<HTTPResponseLogHandler> subscribers;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about HTTP responses.
        /// </summary>
        public HTTPResponseLogEvent()
        {
            subscribers = new List<HTTPResponseLogHandler>();
        }

        #endregion


        #region + / Add

        public static HTTPResponseLogEvent operator + (HTTPResponseLogEvent e, HTTPResponseLogHandler callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                e = new HTTPResponseLogEvent();

            lock (e.subscribers)
            {
                e.subscribers.Add((timestamp, api, request, response) => callback(timestamp, api, request, response));
            }

            return e;

        }

        public HTTPResponseLogEvent Add(HTTPResponseLogHandler callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

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

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                return null;

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPResponseLogEvent Remove(HTTPResponseLogHandler callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

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

            HTTPResponseLogHandler[] _invocationList;

            lock (subscribers)
            {
                _invocationList = subscribers.ToArray();
            }

            foreach (var callback in _invocationList)
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

            List<Task> _invocationList;

            lock (subscribers)
            {

                _invocationList = subscribers.
                                        Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response)).
                                        ToList();

                if (Timeout.HasValue)
                    _invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(_invocationList);

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
        public Task<T> WhenFirst<T>(DateTime           ServerTimestamp,
                                    HTTPAPI            HTTPAPI,
                                    HTTPRequest        Request,
                                    HTTPResponse       Response,
                                    Func<T, Boolean>   VerifyResult,
                                    TimeSpan?          Timeout        = null,
                                    Func<TimeSpan, T>  DefaultResult  = null)
        {

            #region Data

            List<Task>  _invocationList;
            Task        WorkDone;
            Task<T>     Result;
            DateTime    StartTime    = DateTime.UtcNow;
            Task        TimeoutTask  = null;

            #endregion

            lock (subscribers)
            {

                _invocationList = subscribers.
                                        Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response)).
                                        ToList();

                if (Timeout.HasValue)
                    _invocationList.Add(TimeoutTask = Task.Run(() => System.Threading.Thread.Sleep(Timeout.Value)));

            }

            do
            {

                try
                {

                    WorkDone = Task.WhenAny(_invocationList);

                    _invocationList.Remove(WorkDone);

                    if (WorkDone != TimeoutTask)
                    {

                        Result = WorkDone as Task<T>;

                        if (Result != null &&
                            !EqualityComparer<T>.Default.Equals(Result.Result, default(T)) &&
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
            while (!(WorkDone == TimeoutTask || _invocationList.Count == 0));

            return Task.FromResult(DefaultResult(DateTime.UtcNow - StartTime));

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

            Task[] _invocationList;

            lock (subscribers)
            {
                _invocationList = subscribers.
                                        Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response)).
                                        ToArray();
            }

            return Task.WhenAll(_invocationList);

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

        private readonly List<HTTPErrorLogHandler> subscribers;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about HTTP errors.
        /// </summary>
        public HTTPErrorLogEvent()
        {
            subscribers = new List<HTTPErrorLogHandler>();
        }

        #endregion


        #region + / Add

        public static HTTPErrorLogEvent operator + (HTTPErrorLogEvent e, HTTPErrorLogHandler callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                e = new HTTPErrorLogEvent();

            lock (e.subscribers)
            {
                e.subscribers.Add(callback);
            }

            return e;

        }

        public HTTPErrorLogEvent Add(HTTPErrorLogHandler callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

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

            if (callback == null)
                throw new NullReferenceException("callback is null");

            if (e == null)
                return null;

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPErrorLogEvent Remove(HTTPErrorLogHandler callback)
        {

            if (callback == null)
                throw new NullReferenceException("callback is null");

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
                                      String        Error          = null,
                                      Exception     LastException  = null)
        {

            HTTPErrorLogHandler[] _invocationList;

            lock (subscribers)
            {
                _invocationList = subscribers.ToArray();
            }

            foreach (var callback in _invocationList)
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
                            String        Error          = null,
                            Exception     LastException  = null,
                            TimeSpan?     Timeout        = null)
        {

            List<Task> _invocationList;

            lock (subscribers)
            {

                _invocationList = subscribers.
                                        Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, Error, LastException)).
                                        ToList();

                if (Timeout.HasValue)
                    _invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(_invocationList);

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
        public Task<T> WhenFirst<T>(DateTime           ServerTimestamp,
                                    HTTPAPI            HTTPAPI,
                                    HTTPRequest        Request,
                                    HTTPResponse       Response,
                                    String             Error,
                                    Exception          LastException,
                                    Func<T, Boolean>   VerifyResult,
                                    TimeSpan?          Timeout        = null,
                                    Func<TimeSpan, T>  DefaultResult  = null)
        {

            #region Data

            List<Task>  _invocationList;
            Task        WorkDone;
            Task<T>     Result;
            DateTime    StartTime    = DateTime.UtcNow;
            Task        TimeoutTask  = null;

            #endregion

            lock (subscribers)
            {

                _invocationList = subscribers.
                                        Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, Error, LastException)).
                                        ToList();

                if (Timeout.HasValue)
                    _invocationList.Add(TimeoutTask = Task.Run(() => System.Threading.Thread.Sleep(Timeout.Value)));

            }

            do
            {

                try
                {

                    WorkDone = Task.WhenAny(_invocationList);

                    _invocationList.Remove(WorkDone);

                    if (WorkDone != TimeoutTask)
                    {

                        Result = WorkDone as Task<T>;

                        if (Result != null &&
                            !EqualityComparer<T>.Default.Equals(Result.Result, default(T)) &&
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
            while (!(WorkDone == TimeoutTask || _invocationList.Count == 0));

            return Task.FromResult(DefaultResult(DateTime.UtcNow - StartTime));

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
                            String        Error          = null,
                            Exception     LastException  = null)
        {

            Task[] _invocationList;

            lock (subscribers)
            {
                _invocationList = subscribers.
                                        Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, Error, LastException)).
                                        ToArray();
            }

            return Task.WhenAll(_invocationList);

        }

        #endregion

    }

    #endregion



    public static class HTTPAPIExtensions
    {

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


            => HTTPAPI.AddEventSource(EventIdentification,
                                      HTTPAPI,
                                      MaxNumberOfCachedEvents,
                                      RetryIntervall,
                                      data => data.ToString(Newtonsoft.Json.Formatting.None),
                                      text => JObject.Parse(text),
                                      EnableLogging,
                                      LogfilePath,
                                      LogfilePrefix,
                                      LogfileName,
                                      LogfileReloadSearchPattern);



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

            => HTTPAPI.AddEventSource(EventIdentification,
                                      HTTPAPI,
                                      URLTemplate,

                                      MaxNumberOfCachedEvents,
                                      IncludeFilterAtRuntime,
                                      RetryIntervall,
                                      data => data.ToString(Newtonsoft.Json.Formatting.None),
                                      text => JObject.Parse(text),
                                      EnableLogging,
                                      LogfilePath,
                                      LogfilePrefix,
                                      LogfileName,
                                      LogfileReloadSearchPattern,

                                      Hostname,
                                      HTTPMethod,
                                      HTTPContentType,

                                      URIAuthentication,
                                      HTTPMethodAuthentication,

                                      DefaultErrorHandler);

    }

    /// <summary>
    /// A HTTP API.
    /// </summary>
    public class HTTPAPI
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
        /// The default HTTP server name.
        /// </summary>
        public  const              String                  DefaultHTTPServerName           = "GraphDefined HTTP API";

        /// <summary>
        /// The default HTTP service name.
        /// </summary>
        public  const              String                  DefaultHTTPServiceName          = "GraphDefined HTTP API";

        /// <summary>
        /// The default HTTP server port.
        /// </summary>
        public  static readonly    IPPort                  DefaultHTTPServerPort           = IPPort.HTTP;

        /// <summary>
        /// The default HTTP URL path prefix.
        /// </summary>
        public  static readonly    HTTPPath                DefaultURLPathPrefix            = HTTPPath.Parse("/");


        public  const              String                  DefaultHTTPAPI_LoggingPath      = "default";

        public  const              String                  DefaultHTTPAPI_LogfileName      = "HTTPAPI.log";


        /// <summary>
        /// The HTTP root for embedded ressources.
        /// </summary>
        public  const              String                  HTTPRoot                        = "org.GraphDefined.Vanaheimr.Hermod.HTTPRoot.";

        /// <summary>
        /// The default maintenance interval.
        /// </summary>
        public readonly            TimeSpan                DefaultMaintenanceEvery         = TimeSpan.FromMinutes(1);

        private readonly           Timer                   MaintenanceTimer;


        protected static readonly  TimeSpan                SemaphoreSlimTimeout            = TimeSpan.FromSeconds(5);

        protected static readonly  SemaphoreSlim           MaintenanceSemaphore            = new (1, 1);


        /// <summary>
        /// The performance counter to measure the total RAM usage.
        /// </summary>
        protected readonly         PerformanceCounter      totalRAM_PerformanceCounter;

        /// <summary>
        /// The performance counter to measure the total CPU usage.
        /// </summary>
        protected readonly         PerformanceCounter      totalCPU_PerformanceCounter;

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
        /// The name of the HTTP API service.
        /// </summary>
        public String                   ServiceName                 { get; }

        /// <summary>
        /// The offical URL/DNS name of this service, e.g. for sending e-mails.
        /// </summary>
        public String                   ExternalDNSName             { get; }

        /// <summary>
        /// The optional URL path prefix, used when defining URL templates.
        /// </summary>
        public HTTPPath                 URLPathPrefix               { get; }

        /// <summary>
        /// When the API is served from an optional subdirectory path.
        /// </summary>
        public HTTPPath?                BasePath                    { get; }

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

        /// <summary>
        /// An optional HTML template.
        /// </summary>
        public String?                  HTMLTemplate                { get; protected set; }


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



        /// <summary>
        /// This HTTP API runs in development mode.
        /// </summary>
        public Boolean                  IsDevelopment               { get; }

        /// <summary>
        /// The enumeration of server names which will imply to run this service in development mode.
        /// </summary>
        public HashSet<String>          DevelopmentServers          { get; }


        /// <summary>
        /// Disable any logging.
        /// </summary>
        public Boolean                  DisableLogging              { get; }

        /// <summary>
        /// The path for all logfiles.
        /// </summary>
        public String                   LoggingPath                 { get; }

        public String                   HTTPRequestsPath            { get; }

        public String                   HTTPResponsesPath           { get; }

        public String                   HTTPSSEsPath                { get; }

        public String                   MetricsPath                 { get; }

        //public String                   LoggingContext              { get; }

        public String                   LogfileName                 { get; }

        public LogfileCreatorDelegate   LogfileCreator              { get; }


        /// <summary>
        /// The CPO client (HTTP client) logger.
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
        public HTTPRequestLogEvent   RequestLog    = new();

        /// <summary>
        /// An event called whenever a HTTP request could successfully be processed.
        /// </summary>
        public HTTPResponseLogEvent  ResponseLog   = new();

        /// <summary>
        /// An event called whenever a HTTP request resulted in an error.
        /// </summary>
        public HTTPErrorLogEvent     ErrorLog      = new();

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
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
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
        /// <param name="Autostart">Whether to start the API automatically.</param>
        public HTTPAPI(HTTPHostname?                         HTTPHostname                       = null,
                       String?                               ExternalDNSName                    = null,
                       IPPort?                               HTTPServerPort                     = null,
                       HTTPPath?                             BasePath                           = null,
                       String?                               HTTPServerName                     = DefaultHTTPServerName,

                       HTTPPath?                             URLPathPrefix                      = null,
                       String?                               HTTPServiceName                    = DefaultHTTPServiceName,
                       String?                               HTMLTemplate                       = null,
                       JObject?                              APIVersionHashes                   = null,

                       ServerCertificateSelectorDelegate?    ServerCertificateSelector          = null,
                       RemoteCertificateValidationCallback?  ClientCertificateValidator         = null,
                       LocalCertificateSelectionCallback?    ClientCertificateSelector          = null,
                       SslProtocols?                         AllowedTLSProtocols                = null,
                       Boolean?                              ClientCertificateRequired          = null,
                       Boolean?                              CheckCertificateRevocation         = null,

                       String?                               ServerThreadName                   = null,
                       ThreadPriority?                       ServerThreadPriority               = null,
                       Boolean?                              ServerThreadIsBackground           = null,

                       ConnectionIdBuilder?                  ConnectionIdBuilder                = null,
                       TimeSpan?                             ConnectionTimeout                  = null,
                       UInt32?                               MaxClientConnections               = null,

                       Boolean?                              DisableMaintenanceTasks            = null,
                       TimeSpan?                             MaintenanceInitialDelay            = null,
                       TimeSpan?                             MaintenanceEvery                   = null,

                       Boolean?                              DisableWardenTasks                 = null,
                       TimeSpan?                             WardenInitialDelay                 = null,
                       TimeSpan?                             WardenCheckEvery                   = null,

                       Boolean?                              IsDevelopment                      = null,
                       IEnumerable<String>?                  DevelopmentServers                 = null,
                       Boolean?                              DisableLogging                     = null,
                       String?                               LoggingPath                        = null,
                       String?                               LogfileName                        = null,
                       LogfileCreatorDelegate?               LogfileCreator                     = null,
                       DNSClient?                            DNSClient                          = null,
                       Boolean                               Autostart                          = false)

            : this(new HTTPServer(HTTPServerPort ?? DefaultHTTPServerPort,
                                  HTTPServerName ?? DefaultHTTPServerName,
                                  HTTPServiceName,

                                  ServerCertificateSelector,
                                  ClientCertificateSelector,
                                  ClientCertificateValidator,
                                  AllowedTLSProtocols,
                                  ClientCertificateRequired,
                                  CheckCertificateRevocation,

                                  ServerThreadName,
                                  ServerThreadPriority,
                                  ServerThreadIsBackground,
                                  ConnectionIdBuilder,
                                  //ConnectionThreadsNameBuilder,
                                  //ConnectionThreadsPriorityBuilder,
                                  //ConnectionThreadsAreBackground,
                                  ConnectionTimeout,
                                  MaxClientConnections,

                                  DNSClient,
                                  Autostart: false),

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
                   Autostart: false)

        {

            if (Autostart && HTTPServer.Start())
                DebugX.Log(nameof(HTTPAPI) + " version '" + APIVersionHash + "' started...");

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
        /// <param name="Autostart">Whether to start the API automatically.</param>
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
                       Boolean                  Autostart                 = false)

        {

            this.HTTPServer               = HTTPServer      ?? throw new ArgumentNullException(nameof(HTTPServer), "The given HTTP server must not be null!");
            this.Hostname                 = HTTPHostname    ?? HTTP.HTTPHostname.Any;
            this.ExternalDNSName          = ExternalDNSName ?? "";
            this.ServiceName              = HTTPServiceName ?? DefaultHTTPServiceName;
            this.BasePath                 = BasePath;

            this.URLPathPrefix            = URLPathPrefix   ?? DefaultURLPathPrefix;
            this.HTMLTemplate             = HTMLTemplate    ?? "";
            this.APIVersionHash           = APIVersionHashes?[nameof(HTTPAPI)]?.Value<String>()?.Trim();
            this.LoggingPath              = LoggingPath     ?? Path.Combine(AppContext.BaseDirectory, DefaultHTTPAPI_LoggingPath);

            if (this.LoggingPath[^1] != Path.DirectorySeparatorChar)
                this.LoggingPath += Path.DirectorySeparatorChar;

            this.HTTPRequestsPath         = this.LoggingPath + "HTTPRequests"   + Path.DirectorySeparatorChar;
            this.HTTPResponsesPath        = this.LoggingPath + "HTTPResponses"  + Path.DirectorySeparatorChar;
            this.HTTPSSEsPath             = this.LoggingPath + "HTTPSSEs"       + Path.DirectorySeparatorChar;
            this.MetricsPath              = this.LoggingPath + "Metrics"        + Path.DirectorySeparatorChar;

            this.RunType                  = ApplicationRunType.GetRunType();
            this.SystemId                 = System_Id.Parse(Environment.MachineName.Replace("/", "") + "/" + HTTPServer.DefaultHTTPServerPort);
            this.IsDevelopment            = IsDevelopment ?? false;
            this.DevelopmentServers       = DevelopmentServers is not null
                                                ? new HashSet<String>(DevelopmentServers)
                                                : new HashSet<String>();

            if (this.DevelopmentServers.Contains(Environment.MachineName))
                this.IsDevelopment = true;

            this.LogfileName              = LogfileName    ?? DefaultHTTPAPI_LogfileName;
            this.LogfileCreator           = LogfileCreator ?? ((loggingPath, context, logfileName) => String.Concat(loggingPath,
                                                                                                                    context.IsNotNullOrEmpty() ? context + Path.DirectorySeparatorChar : "",
                                                                                                                    logfileName.Replace(".log", ""), "_",
                                                                                                                    DateTime.Now.Year, "-",
                                                                                                                    DateTime.Now.Month.ToString("D2"),
                                                                                                                    ".log"));

            if (DisableLogging == false)
            {
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.LoggingPath));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.HTTPRequestsPath));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.HTTPResponsesPath));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.HTTPSSEsPath));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, this.MetricsPath));
            }


            // Link HTTP events...
            HTTPServer.RequestLog   += (HTTPProcessor, ServerTimestamp, Request)                                 => RequestLog. WhenAll(HTTPProcessor, ServerTimestamp, Request);
            HTTPServer.ResponseLog  += (HTTPProcessor, ServerTimestamp, Request, Response)                       => ResponseLog.WhenAll(HTTPProcessor, ServerTimestamp, Request, Response);
            HTTPServer.ErrorLog     += (HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException) => ErrorLog.   WhenAll(HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException);


            // Setup Maintenance Task
            this.DisableMaintenanceTasks  = DisableMaintenanceTasks ?? false;
            this.MaintenanceEvery         = MaintenanceEvery        ?? DefaultMaintenanceEvery;
            this.MaintenanceTimer         = new Timer(DoMaintenanceSync,
                                                      null,
                                                      this.MaintenanceEvery,
                                                      this.MaintenanceEvery);

            // Setup Warden
            this.Warden = new Warden.Warden(WardenInitialDelay ?? TimeSpan.FromMinutes(3),
                                            WardenCheckEvery   ?? TimeSpan.FromMinutes(1),
                                            DNSClient);

            #region Warden: Observe CPU/RAM

            Thread.CurrentThread.CurrentCulture   = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {

                // If those lines fail, try to run "lodctr /R" as administrator in an cmd.exe environment
                totalRAM_PerformanceCounter = new PerformanceCounter("Process", "Working Set",      Process.GetCurrentProcess().ProcessName);
                totalCPU_PerformanceCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                totalRAM_PerformanceCounter.NextValue();
                totalCPU_PerformanceCounter.NextValue();

                Warden.EveryMinutes(1,
                                    Process.GetCurrentProcess(),
                                    async (timestamp, process, ct) => {
                                        using (var writer = File.AppendText(String.Concat(this.MetricsPath,
                                                                                          Path.DirectorySeparatorChar,
                                                                                          "process-stats_",
                                                                                          DateTime.Now.Year, "-",
                                                                                          DateTime.Now.Month.ToString("D2"),
                                                                                          ".log")))
                                        {

                                            await writer.WriteLineAsync(String.Concat(timestamp.ToIso8601(), ";",
                                                                                      process.VirtualMemorySize64, ";",
                                                                                      process.WorkingSet64, ";",
                                                                                      process.TotalProcessorTime, ";",
                                                                                      totalRAM_PerformanceCounter.NextValue() / 1024 / 1024, ";",
                                                                                      totalCPU_PerformanceCounter.NextValue())).
                                                         ConfigureAwait(false);

                                        }

                                    });

                Warden.EveryMinutes(15,
                                    Environment.OSVersion.Platform == PlatformID.Unix
                                        ? new DriveInfo("/")
                                        : new DriveInfo(Directory.GetCurrentDirectory()),
                                    async (timestamp, driveInfo, ct) => {
                                        using (var writer = File.AppendText(String.Concat(this.MetricsPath,
                                                                                          Path.DirectorySeparatorChar,
                                                                                          "disc-stats_",
                                                                                          DateTime.Now.Year, "-",
                                                                                          DateTime.Now.Month.ToString("D2"),
                                                                                          ".log")))
                                        {

                                            var MBytesFree       = driveInfo.AvailableFreeSpace / 1024 / 1024;
                                            var HDPercentageFree = 100 * driveInfo.AvailableFreeSpace / driveInfo.TotalSize;

                                            await writer.WriteLineAsync(String.Concat(timestamp.ToIso8601(), ";",
                                                                                      MBytesFree, ";",
                                                                                      HDPercentageFree)).
                                                         ConfigureAwait(false);

                                        }

                                    });

            }

            #endregion


            if (Autostart == true && HTTPServer.Start())
                DebugX.Log(nameof(HTTPAPI) + " version '" + APIVersionHash + "' started...");

        }

        #endregion

        #region HTTPAPI(HTTPAPI)

        public HTTPAPI(HTTPAPI HTTPAPI)

            : this(HTTPServer:               HTTPAPI.HTTPServer,
                   HTTPHostname:             HTTPAPI.Hostname,
                   ExternalDNSName:          HTTPAPI.ExternalDNSName,
                   HTTPServiceName:          HTTPAPI.ServiceName,
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

            if (URLTemplate.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(URLTemplate),   "The given URL template must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),  "The given HTTP delegate must not be null!");

            #endregion

            HTTPServer.AddMethodCallback(this,
                                         Hostname,
                                         HTTPMethod,
                                         URLTemplate,
                                         HTTPContentType,
                                         URLAuthentication,
                                         HTTPMethodAuthentication,
                                         ContentTypeAuthentication,
                                         HTTPRequestLogger,
                                         HTTPResponseLogger,
                                         DefaultErrorHandler,
                                         HTTPDelegate,
                                         AllowReplacement);

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
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
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

            HTTPServer.AddMethodCallback(this,
                                         Hostname,
                                         HTTPMethod,
                                         URLTemplates,
                                         HTTPContentType,
                                         URLAuthentication,
                                         HTTPMethodAuthentication,
                                         ContentTypeAuthentication,
                                         HTTPRequestLogger,
                                         HTTPResponseLogger,
                                         DefaultErrorHandler,
                                         HTTPDelegate,
                                         AllowReplacement);

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

            if (URLTemplate.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(URLTemplate),       "The given URL template must not be null or empty!");

            if (!HTTPContentTypes.Any())
                throw new ArgumentNullException(nameof(HTTPContentTypes),  "The given content types must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),      "The given HTTP delegate must not be null!");

            #endregion

            HTTPServer.AddMethodCallback(this,
                                         Hostname,
                                         HTTPMethod,
                                         URLTemplate,
                                         HTTPContentTypes,
                                         URLAuthentication,
                                         HTTPMethodAuthentication,
                                         ContentTypeAuthentication,
                                         HTTPRequestLogger,
                                         HTTPResponseLogger,
                                         DefaultErrorHandler,
                                         HTTPDelegate,
                                         AllowReplacement);

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

            HTTPServer.AddMethodCallback(this,
                                         Hostname,
                                         HTTPMethod,
                                         URLTemplates,
                                         HTTPContentTypes,
                                         URLAuthentication,
                                         HTTPMethodAuthentication,
                                         ContentTypeAuthentication,
                                         HTTPRequestLogger,
                                         HTTPResponseLogger,
                                         DefaultErrorHandler,
                                         HTTPDelegate,
                                         AllowReplacement);

        }

        #endregion

        #endregion

        #region HTTP Server Sent Events

        #region AddEventSource(EventIdentification, MaxNumberOfCachedEvents = 500, RetryIntervall = null, LogfileName = null)

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
                                                            HTTPAPI                          HTTPAPI,
                                                            UInt32                           MaxNumberOfCachedEvents      = 500,
                                                            TimeSpan?                        RetryIntervall               = null,
                                                            Func<TData, String>?             DataSerializer               = null,
                                                            Func<String, TData>?             DataDeserializer             = null,
                                                            Boolean                          EnableLogging                = true,
                                                            String?                          LogfilePath                  = null,
                                                            String?                          LogfilePrefix                = null,
                                                            Func<String, DateTime, String>?  LogfileName                  = null,
                                                            String?                          LogfileReloadSearchPattern   = null)

            => HTTPServer.AddEventSource(EventIdentification,
                                         HTTPAPI,
                                         MaxNumberOfCachedEvents,
                                         RetryIntervall,
                                         DataSerializer,
                                         DataDeserializer,
                                         EnableLogging,
                                         LogfilePath,
                                         LogfilePrefix,
                                         LogfileName,
                                         LogfileReloadSearchPattern);

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
                                                    HTTPAPI                          HTTPAPI,
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

                                                    HTTPAuthentication?              URIAuthentication            = null,
                                                    HTTPAuthentication?              HTTPMethodAuthentication     = null,

                                                    HTTPDelegate?                    DefaultErrorHandler          = null)

            => HTTPServer.AddEventSource(EventIdentification,
                                         HTTPAPI,
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

                                         URIAuthentication,
                                         HTTPMethodAuthentication,

                                         DefaultErrorHandler);

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
        public IEnumerable<IHTTPEventSource> EventSources(Func<IHTTPEventSource, Boolean> IncludeEventSource = null)

            => HTTPServer.EventSources(IncludeEventSource);


        /// <summary>
        /// Return a filtered enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        public IEnumerable<IHTTPEventSource<TData>> EventSources<TData>(Func<IHTTPEventSource, Boolean> IncludeEventSource = null)

            => HTTPServer.EventSources<TData>(IncludeEventSource);

        #endregion

        #endregion


        #region (protected virtual) GetResourceStream      (ResourceName, ResourceAssemblies)

        protected virtual Stream? GetResourceStream(String ResourceName)

            => GetResourceStream(ResourceName,
                                 new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual Stream? GetResourceStream(String                            ResourceName,
                                                    params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            foreach (var resourceAssembly in ResourceAssemblies)
            {
                try
                {

                    var resourceStream = resourceAssembly.Item2.GetManifestResourceStream(resourceAssembly.Item1 + ResourceName);

                    if (resourceStream is not null)
                        return resourceStream;

                }
                catch
                { }
            }

            return null;

        }

        #endregion

        #region (protected virtual) GetResourceMemoryStream(ResourceName, ResourceAssemblies)

        protected virtual MemoryStream? GetResourceMemoryStream(String ResourceName)

            => GetResourceMemoryStream(ResourceName,
                                       new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual MemoryStream? GetResourceMemoryStream(String                            ResourceName,
                                                                params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            try
            {

                var resourceStream = GetResourceStream(ResourceName,
                                                       ResourceAssemblies);

                if (resourceStream is not null)
                {

                    var outputStream = new MemoryStream();
                    resourceStream.CopyTo(outputStream);
                    outputStream.Seek(0, SeekOrigin.Begin);

                    return outputStream;

                }

            }
            catch
            { }

            return null;

        }

        #endregion

        #region (protected virtual) GetResourceString      (ResourceName, ResourceAssemblies)

        protected virtual String GetResourceString(String ResourceName)

            => GetResourceString(ResourceName,
                                 new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String GetResourceString(String                            ResourceName,
                                                   params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToUTF8String() ?? String.Empty;

        #endregion

        #region (protected virtual) GetResourceBytes       (ResourceName, ResourceAssemblies)

        protected virtual Byte[] GetResourceBytes(String ResourceName)

            => GetResourceBytes(ResourceName,
                                new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual Byte[] GetResourceBytes(String                            ResourceName,
                                                  params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToArray() ?? Array.Empty<Byte>();

        #endregion

        #region (protected virtual) MixWithHTMLTemplate    (ResourceName, ResourceAssemblies)

        protected virtual String MixWithHTMLTemplate(String ResourceName)

            => MixWithHTMLTemplate(ResourceName,
                                   new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String MixWithHTMLTemplate(String                            ResourceName,
                                                     params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            if (HTMLTemplate is not null)
            {

                var htmlStream = new MemoryStream();

                foreach (var assembly in ResourceAssemblies)
                {

                    var resourceStream = assembly.Item2.GetManifestResourceStream(assembly.Item1 + ResourceName);
                    if (resourceStream is not null)
                    {

                        resourceStream.Seek(3, SeekOrigin.Begin);
                        resourceStream.CopyTo(htmlStream);

                        return HTMLTemplate.Replace("<%= content %>",  htmlStream.ToArray().ToUTF8String()).
                                            Replace("{{BasePath}}",    BasePath?.ToString() ?? "");

                    }

                }

                return HTMLTemplate.Replace("<%= content %>",  "").
                                    Replace("{{BasePath}}",    "");

            }

            return String.Empty;

        }

        #endregion

        #region (protected virtual) MixWithHTMLTemplate    (ResourceName, HTMLConverter, ResourceAssemblies)

        protected virtual String MixWithHTMLTemplate(String                ResourceName,
                                                     Func<String, String>  HTMLConverter)

            => MixWithHTMLTemplate(ResourceName,
                                   HTMLConverter,
                                   new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String MixWithHTMLTemplate(String                            ResourceName,
                                                     Func<String, String>              HTMLConverter,
                                                     params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            if (HTMLTemplate is not null)
            {

                var htmlStream = new MemoryStream();

                foreach (var assembly in ResourceAssemblies)
                {

                    var resourceStream = assembly.Item2.GetManifestResourceStream(assembly.Item1 + ResourceName);
                    if (resourceStream is not null)
                    {

                        resourceStream.Seek(3, SeekOrigin.Begin);
                        resourceStream.CopyTo(htmlStream);

                        return HTMLConverter(HTMLTemplate.Replace("<%= content %>",  htmlStream.ToArray().ToUTF8String()).
                                                          Replace("{{BasePath}}",    BasePath?.ToString() ?? ""));

                    }

                }

                return HTMLConverter(HTMLTemplate.Replace("<%= content %>",  "").
                                                  Replace("{{BasePath}}",    ""));

            }

            return String.Empty;

        }

        #endregion

        #region (protected virtual) MixWithHTMLTemplate    (Template, ResourceName, ResourceAssemblies)

        protected virtual String MixWithHTMLTemplate(String   Template,
                                                     String   ResourceName,
                                                     String?  Content   = null)

            => MixWithHTMLTemplate(Template,
                                   ResourceName,
                                   new Tuple<String, Assembly>[] { new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly) },
                                   Content);

        protected virtual String MixWithHTMLTemplate(String                     Template,
                                                     String                     ResourceName,
                                                     Tuple<String, Assembly>[]  ResourceAssemblies,
                                                     String?                    Content   = null)
        {

            var htmlStream = new MemoryStream();

            foreach (var assembly in ResourceAssemblies)
            {

                var resourceStream = assembly.Item2.GetManifestResourceStream(assembly.Item1 + ResourceName);
                if (resourceStream is not null)
                {

                    resourceStream.Seek(3, SeekOrigin.Begin);
                    resourceStream.CopyTo(htmlStream);

                    return Template.Replace("<%= content %>",  htmlStream.ToArray().ToUTF8String()).
                                    Replace("{{BasePath}}",    BasePath?.ToString() ?? "");

                }

            }

            return Template.Replace("<%= content %>",  "").
                            Replace("{{BasePath}}",    "");

        }

        #endregion


        #region (Timer) DoMaintenance(State)

        private void DoMaintenanceSync(Object State)
        {
            if (ReloadFinished && !DisableMaintenanceTasks)
                DoMaintenance(State).Wait();
        }

        protected internal virtual async Task _DoMaintenance(Object State)
        {

        }

        private async Task DoMaintenance(Object State)
        {

            if (await MaintenanceSemaphore.WaitAsync(SemaphoreSlimTimeout).
                                           ConfigureAwait(false))
            {
                try
                {

                    await _DoMaintenance(State);

                }
                catch (Exception e)
                {

                    while (e.InnerException != null)
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


        #region Start()

        public virtual Boolean Start()
        {

            lock (HTTPServer)
            {

                if (!HTTPServer.IsStarted)
                    return HTTPServer.Start();

                return true;

                //SendStarted(this, CurrentTimestamp);

            }

        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        public virtual Boolean Shutdown(String?  Message   = null,
                                        Boolean  Wait      = true)
        {
            lock (HTTPServer)
            {

                if (HTTPServer.IsStarted)
                    return HTTPServer.Shutdown(Message, Wait);

                return true;

            }
        }

        #endregion

        #region Dispose()

        public virtual void Dispose()
        {

            lock (HTTPServer)
            {
                HTTPServer.Dispose();
            }

        }

        #endregion

    }

}
