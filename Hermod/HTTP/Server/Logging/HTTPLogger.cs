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
using System.Collections.Generic;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP API logger.
    /// </summary>
    public class HTTPLogger
    {

        #region (class) HTTPRequestLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPRequestLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, RequestLogHandler>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                        _SubscriptionStatus;

            #endregion

            #region Properties

            #region Context

            private readonly String _Context;

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String Context
            {
                get
                {
                    return _Context;
                }
            }

            #endregion

            #region LogEventName

            private readonly String _LogEventName;

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String LogEventName
            {
                get
                {
                    return _LogEventName;
                }
            }

            #endregion

            #region SubscribeToEventDelegate

            private readonly Action<RequestLogHandler> _SubscribeToEventDelegate;

            /// <summary>
            /// A delegate called whenever the event is subscriped to.
            /// </summary>
            public Action<RequestLogHandler> SubscribeToEventDelegate
            {
                get
                {
                    return _SubscribeToEventDelegate;
                }
            }

            #endregion

            #region UnsubscribeFromEventDelegate

            private readonly Action<RequestLogHandler> _UnsubscribeFromEventDelegate;

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<RequestLogHandler> UnsubscribeFromEventDelegate
            {
                get
                {
                    return _UnsubscribeFromEventDelegate;
                }
            }

            #endregion

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked HTTP API event.
            /// </summary>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPRequestLogger(String                     Context,
                                     String                     LogEventName,
                                     Action<RequestLogHandler>  SubscribeToEventDelegate,
                                     Action<RequestLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate == null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate == null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this._Context                       = Context != null ? Context : "";
                this._LogEventName                  = LogEventName;
                this._SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this._UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates         = new Dictionary<LogTargets, RequestLogHandler>();
                this._SubscriptionStatus            = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPRequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPRequestDelegate">A delegate to call.</param>
            /// <returns>A HTTP request logger.</returns>
            public HTTPRequestLogger RegisterLogTarget(LogTargets                           LogTarget,
                                                       Action<String, String, HTTPRequest>  HTTPRequestDelegate)
            {

                #region Initial checks

                if (HTTPRequestDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPRequestDelegate),  "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                _SubscriptionDelegates.Add(LogTarget, (Timestamp, HTTPAPI, Request) => HTTPRequestDelegate(_Context, LogEventName, Request));

                return this;

            }

            #endregion

            #region Subscribe(LogTarget)

            /// <summary>
            /// Subscribe the given log target to the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Subscribe(LogTargets LogTarget)
            {

                if (IsSubscribed(LogTarget))
                    return true;

                RequestLogHandler _RequestLogHandler = null;

                if (_SubscriptionDelegates.TryGetValue(LogTarget, out _RequestLogHandler))
                {
                    _SubscribeToEventDelegate(_RequestLogHandler);
                    _SubscriptionStatus.Add(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

            #region IsSubscribed(LogTarget)

            /// <summary>
            /// Return the subscription status of the given log target.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            public Boolean IsSubscribed(LogTargets LogTarget)
            {
                return _SubscriptionStatus.Contains(LogTarget);
            }

            #endregion

            #region Unsubscribe(LogTarget)

            /// <summary>
            /// Unsubscribe the given log target from the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Unsubscribe(LogTargets LogTarget)
            {

                if (!IsSubscribed(LogTarget))
                    return true;

                RequestLogHandler _RequestLogHandler = null;

                if (_SubscriptionDelegates.TryGetValue(LogTarget, out _RequestLogHandler))
                {
                    _UnsubscribeFromEventDelegate(_RequestLogHandler);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion

        #region (class) HTTPResponseLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPResponseLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, AccessLogHandler>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                       _SubscriptionStatus;

            #endregion

            #region Properties

            #region Context

            private readonly String _Context;

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String Context
            {
                get
                {
                    return _Context;
                }
            }

            #endregion

            #region LogEventName

            private readonly String _LogEventName;

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String LogEventName
            {
                get
                {
                    return _LogEventName;
                }
            }

            #endregion

            #region SubscribeToEventDelegate

            private readonly Action<AccessLogHandler> _SubscribeToEventDelegate;

            /// <summary>
            /// A delegate called whenever the event is subscriped to.
            /// </summary>
            public Action<AccessLogHandler> SubscribeToEventDelegate
            {
                get
                {
                    return _SubscribeToEventDelegate;
                }
            }

            #endregion

            #region UnsubscribeFromEventDelegate

            private readonly Action<AccessLogHandler> _UnsubscribeFromEventDelegate;

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<AccessLogHandler> UnsubscribeFromEventDelegate
            {
                get
                {
                    return _UnsubscribeFromEventDelegate;
                }
            }

            #endregion

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked HTTP API event.
            /// </summary>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPResponseLogger(String                    Context,
                                      String                    LogEventName,
                                      Action<AccessLogHandler>  SubscribeToEventDelegate,
                                      Action<AccessLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate == null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked  HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate == null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this._Context                       = Context != null ? Context : "";
                this._LogEventName                  = LogEventName;
                this._SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this._UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates         = new Dictionary<LogTargets, AccessLogHandler>();
                this._SubscriptionStatus            = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPResponseDelegate">A delegate to call.</param>
            /// <returns>A HTTP response logger.</returns>
            public HTTPResponseLogger RegisterLogTarget(LogTargets                                         LogTarget,
                                                        Action<String, String, HTTPRequest, HTTPResponse>  HTTPResponseDelegate)
            {

                #region Initial checks

                if (HTTPResponseDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPResponseDelegate), "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                _SubscriptionDelegates.Add(LogTarget, (Timestamp, HTTPAPI, Request, Response) => HTTPResponseDelegate( Context, LogEventName, Request, Response));

                return this;

            }

            #endregion

            #region Subscribe(LogTarget)

            /// <summary>
            /// Subscribe the given log target to the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Subscribe(LogTargets LogTarget)
            {

                if (IsSubscribed(LogTarget))
                    return true;

                AccessLogHandler _AccessLogHandler = null;

                if (_SubscriptionDelegates.TryGetValue(LogTarget, out _AccessLogHandler))
                {
                    _SubscribeToEventDelegate(_AccessLogHandler);
                    _SubscriptionStatus.Add(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

            #region IsSubscribed(LogTarget)

            /// <summary>
            /// Return the subscription status of the given log target.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            public Boolean IsSubscribed(LogTargets LogTarget)
            {
                return _SubscriptionStatus.Contains(LogTarget);
            }

            #endregion

            #region Unsubscribe(LogTarget)

            /// <summary>
            /// Unsubscribe the given log target from the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Unsubscribe(LogTargets LogTarget)
            {

                if (!IsSubscribed(LogTarget))
                    return true;

                AccessLogHandler _AccessLogHandler = null;

                if (_SubscriptionDelegates.TryGetValue(LogTarget, out _AccessLogHandler))
                {
                    _UnsubscribeFromEventDelegate(_AccessLogHandler);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion


        #region Default logging delegates

        #region LogFileCreator

        private static Func<String, String, String> _LogFileCreator;

        /// <summary>
        /// A delegate for the default ToDisc logger returning a
        /// valid logfile name based on the given log event name.
        /// </summary>
        public static Func<String, String, String> LogFileCreator
        {
            get
            {
                return _LogFileCreator;
            }
        }

        #endregion

        #region (static) Default_LogHTTPRequest_toConsole(Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming HTTP requests to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        public static void Default_LogHTTPRequest_toConsole(String       Context,
                                                            String       LogEventName,
                                                            HTTPRequest  Request)
        {

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("[" + Request.Timestamp + "] ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(Context + "/");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(LogEventName);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" from " + Request.RemoteSocket);

        }

        #endregion

        #region (static) Default_LogHTTPResponse_toConsole(Context, LogEventName, Request, Response)

        /// <summary>
        /// A default delegate for logging HTTP requests/-responses to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public static void Default_LogHTTPResponse_toConsole(String        Context,
                                                             String        LogEventName,
                                                             HTTPRequest   Request,
                                                             HTTPResponse  Response)
        {

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("[" + Request.Timestamp + "] ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(Context + "/");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(LogEventName);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(String.Concat(" from ", Request.RemoteSocket, " => "));

            if (Response.HTTPStatusCode == HTTPStatusCode.OK ||
                Response.HTTPStatusCode == HTTPStatusCode.Created)
                Console.ForegroundColor = ConsoleColor.Green;

            else if (Response.HTTPStatusCode == HTTPStatusCode.NoContent)
                Console.ForegroundColor = ConsoleColor.Yellow;

            else
                Console.ForegroundColor = ConsoleColor.Red;

            Console.Write(Response.HTTPStatusCode);

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(String.Concat(" in ", Math.Round((Response.Timestamp - Request.Timestamp).TotalMilliseconds), "ms"));

        }

        #endregion

        #region (static) Default_LogHTTPRequest_toDisc(Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming HTTP requests to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        public static void Default_LogHTTPRequest_toDisc(String       Context,
                                                         String       LogEventName,
                                                         HTTPRequest  Request)
        {

            lock (Context)
            {
                using (var logfile = File.AppendText(LogFileCreator(Context, LogEventName)))
                {
                    logfile.WriteLine(Request.RemoteSocket.ToString() + " -> " + Request.LocalSocket);
                    logfile.WriteLine(">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------");
                    logfile.WriteLine(Request.Timestamp.ToIso8601());
                    logfile.WriteLine(Request.EntirePDU);
                    logfile.WriteLine("--------------------------------------------------------------------------------");
                }
            }

        }

        #endregion

        #region (static) Default_LogHTTPResponse_toDisc(Context, LogEventName, Request, Response)

        /// <summary>
        /// A default delegate for logging HTTP requests/-responses to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public static void Default_LogHTTPResponse_toDisc(String        Context,
                                                          String        LogEventName,
                                                          HTTPRequest   Request,
                                                          HTTPResponse  Response)
        {

            lock (Context)
            {
                using (var logfile = File.AppendText(LogFileCreator(Context, LogEventName)))
                {
                    logfile.WriteLine(Request.RemoteSocket.ToString() + " -> " + Request.LocalSocket);
                    logfile.WriteLine(">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------");
                    logfile.WriteLine(Request.Timestamp.ToIso8601());
                    logfile.WriteLine(Request.EntirePDU);
                    logfile.WriteLine("<<<<<<--Response----<<<<<<------<<<<<<------<<<<<<------<<<<<<------<<<<<<------");
                    logfile.WriteLine(Response.Timestamp.ToIso8601() + " -> " + (Request.Timestamp - Response.Timestamp).TotalMilliseconds + "ms runtime");
                    logfile.WriteLine(Response.EntirePDU);
                    logfile.WriteLine("--------------------------------------------------------------------------------");
                }
            }

        }

        #endregion

        #endregion


        #region Data

        private readonly HTTPServer                                        _HTTPAPI;
        private readonly ConcurrentDictionary<String, HTTPRequestLogger>   _HTTPRequestLoggers;
        private readonly ConcurrentDictionary<String, HTTPResponseLogger>  _HTTPResponseLoggers;
        private readonly ConcurrentDictionary<String, HashSet<String>>     _GroupTags;

        #endregion

        #region Properties

        #region Context

        private readonly String _Context;

        /// <summary>
        /// The context of this HTTP logger.
        /// </summary>
        public String Context
        {
            get
            {
                return _Context;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPLogger()

        /// <summary>
        /// Create a new HTTP API logger using the default logging delegates.
        /// </summary>
        protected HTTPLogger()
        { }

        #endregion

        #region HTTPLogger(HTTPAPI, Context = "")

        /// <summary>
        /// Create a new HTTP API logger using the default logging delegates.
        /// </summary>
        /// <param name="HTTPAPI">A HTTP API.</param>
        /// <param name="Context">A context of this API.</param>
        public HTTPLogger(HTTPServer  HTTPAPI,
                          String      Context = "")

            : this(HTTPAPI,
                   Context,
                   Default_LogHTTPRequest_toConsole,
                   Default_LogHTTPResponse_toConsole,
                   Default_LogHTTPRequest_toDisc,
                   Default_LogHTTPResponse_toDisc)

        { }

        #endregion

        #region HTTPLogger(HTTPAPI, Context, ... Logging delegates ...)

        /// <summary>
        /// Create a new HTTP API logger using the given logging delegates.
        /// </summary>
        /// <param name="HTTPAPI">A HTTP API.</param>
        /// <param name="Context">A context of this API.</param>
        /// 
        /// <param name="LogHTTPRequest_toConsole">A delegate to log incoming HTTP requests to console.</param>
        /// <param name="LogHTTPResponse_toConsole">A delegate to log HTTP requests/responses to console.</param>
        /// <param name="LogHTTPRequest_toDisc">A delegate to log incoming HTTP requests to disc.</param>
        /// <param name="LogHTTPResponse_toDisc">A delegate to log HTTP requests/responses to disc.</param>
        /// 
        /// <param name="LogHTTPRequest_toNetwork">A delegate to log incoming HTTP requests to a network target.</param>
        /// <param name="LogHTTPResponse_toNetwork">A delegate to log HTTP requests/responses to a network target.</param>
        /// <param name="LogHTTPRequest_toHTTPSSE">A delegate to log incoming HTTP requests to a HTTP server sent events source.</param>
        /// <param name="LogHTTPResponse_toHTTPSSE">A delegate to log HTTP requests/responses to a HTTP server sent events source.</param>
        /// 
        /// <param name="LogHTTPError_toConsole">A delegate to log HTTP errors to console.</param>
        /// <param name="LogHTTPError_toDisc">A delegate to log HTTP errors to disc.</param>
        /// <param name="LogHTTPError_toNetwork">A delegate to log HTTP errors to a network target.</param>
        /// <param name="LogHTTPError_toHTTPSSE">A delegate to log HTTP errors to a HTTP server sent events source.</param>
        /// 
        /// <param name="LogFileCreator">A delegate to create a log file from the given context and log file name.</param>
        public HTTPLogger(HTTPServer                                         HTTPAPI,
                          String                                             Context,

                          Action<String, String, HTTPRequest>                LogHTTPRequest_toConsole,
                          Action<String, String, HTTPRequest, HTTPResponse>  LogHTTPResponse_toConsole,
                          Action<String, String, HTTPRequest>                LogHTTPRequest_toDisc,
                          Action<String, String, HTTPRequest, HTTPResponse>  LogHTTPResponse_toDisc,

                          Action<String, String, HTTPRequest>                LogHTTPRequest_toNetwork   = null,
                          Action<String, String, HTTPRequest, HTTPResponse>  LogHTTPResponse_toNetwork  = null,
                          Action<String, String, HTTPRequest>                LogHTTPRequest_toHTTPSSE   = null,
                          Action<String, String, HTTPRequest, HTTPResponse>  LogHTTPResponse_toHTTPSSE  = null,

                          Action<String, String, HTTPRequest, HTTPResponse>  LogHTTPError_toConsole     = null,
                          Action<String, String, HTTPRequest, HTTPResponse>  LogHTTPError_toDisc        = null,
                          Action<String, String, HTTPRequest, HTTPResponse>  LogHTTPError_toNetwork     = null,
                          Action<String, String, HTTPRequest, HTTPResponse>  LogHTTPError_toHTTPSSE     = null,

                          Func<String, String, String>                       LogFileCreator             = null)

        {

            #region Initial checks

            if (HTTPAPI == null)
                throw new ArgumentNullException(nameof(HTTPAPI), "The given HTTP API must not be null!");

            #endregion

            #region Init data structures

            this._HTTPAPI              = HTTPAPI;
            this._Context              = Context != null ? Context : "";
            this._HTTPRequestLoggers   = new ConcurrentDictionary<String, HTTPRequestLogger>();
            this._HTTPResponseLoggers  = new ConcurrentDictionary<String, HTTPResponseLogger>();
            this._GroupTags            = new ConcurrentDictionary<String, HashSet<String>>();

            #endregion

            #region Set default delegates

            if (LogHTTPRequest_toConsole  == null)
                LogHTTPRequest_toConsole   = Default_LogHTTPRequest_toConsole;

            if (LogHTTPRequest_toDisc     == null)
                LogHTTPRequest_toDisc      = Default_LogHTTPRequest_toDisc;

            if (LogHTTPRequest_toDisc     == null)
                LogHTTPRequest_toDisc      = Default_LogHTTPRequest_toDisc;

            if (LogHTTPResponse_toConsole == null)
                LogHTTPResponse_toConsole  = Default_LogHTTPResponse_toConsole;

            if (LogHTTPResponse_toDisc    == null)
                LogHTTPResponse_toDisc     = Default_LogHTTPResponse_toDisc;

            _LogFileCreator = LogFileCreator != null
                                 ? LogFileCreator
                                 : (context, logfilename) => String.Concat((context != null ? context + "_" : ""),
                                                                           logfilename, "_",
                                                                           DateTime.Now.Year, "-",
                                                                           DateTime.Now.Month.ToString("D2"),
                                                                           ".log");

            #endregion


            //ToDo: Evaluate Logging targets!

            HTTPAPI.ErrorLog += (Timestamp,
                                 HTTPServer,
                                 HTTPRequest,
                                 HTTPResponse,
                                 Error,
                                 LastException)

                => {

                        DebugX.Log(Timestamp + " - " +
                                   HTTPRequest.RemoteSocket.IPAddress + ":" +
                                   HTTPRequest.RemoteSocket.Port + " " +
                                   HTTPRequest.HTTPMethod + " " +
                                   HTTPRequest.URI + " " +
                                   HTTPRequest.ProtocolVersion + " => " +
                                   HTTPResponse.HTTPStatusCode + " - " +
                                   Error);

                   };

        }

        #endregion

        #endregion


        #region (protected) RegisterEvent(LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate, params GroupTags)

        /// <summary>
        /// Register a log event for the linked HTTP API event.
        /// </summary>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
        /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
        /// <param name="GroupTags">An array of log event groups the given log event name is part of.</param>
        protected HTTPRequestLogger RegisterEvent(String                     LogEventName,
                                                  Action<RequestLogHandler>  SubscribeToEventDelegate,
                                                  Action<RequestLogHandler>  UnsubscribeFromEventDelegate,
                                                  params String[]            GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate == null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate == null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            HTTPRequestLogger _HTTPRequestLogger = null;

            if (!_HTTPRequestLoggers. TryGetValue(LogEventName, out _HTTPRequestLogger) &&
                !_HTTPResponseLoggers.ContainsKey(LogEventName))
            {

                _HTTPRequestLogger = new HTTPRequestLogger(Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _HTTPRequestLoggers.TryAdd(LogEventName, _HTTPRequestLogger);

                #region Register group tag mapping

                HashSet<String> _LogEventNames = null;

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (_GroupTags.TryGetValue(GroupTag, out _LogEventNames))
                        _LogEventNames.Add(LogEventName);

                    else
                        _GroupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return _HTTPRequestLogger;

            }

            throw new Exception("Duplicate log event name!");

        }

        #endregion

        #region (protected) RegisterEvent(LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate, params GroupTags)

        /// <summary>
        /// Register a log event for the linked HTTP API event.
        /// </summary>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
        /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
        /// <param name="GroupTags">An array of log event groups the given log event name is part of.</param>
        protected HTTPResponseLogger RegisterEvent(String                    LogEventName,
                                                   Action<AccessLogHandler>  SubscribeToEventDelegate,
                                                   Action<AccessLogHandler>  UnsubscribeFromEventDelegate,
                                                   params String[]           GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate == null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate == null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            HTTPResponseLogger _HTTPResponseLogger = null;

            if (!_HTTPResponseLoggers.TryGetValue(LogEventName, out _HTTPResponseLogger) &&
                !_HTTPRequestLoggers. ContainsKey(LogEventName))
            {

                _HTTPResponseLogger = new HTTPResponseLogger(Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _HTTPResponseLoggers.TryAdd(LogEventName, _HTTPResponseLogger);

                #region Register group tag mapping

                HashSet<String> _LogEventNames = null;

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (_GroupTags.TryGetValue(GroupTag, out _LogEventNames))
                        _LogEventNames.Add(LogEventName);

                    else
                        _GroupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return _HTTPResponseLogger;

            }

            throw new Exception("Duplicate log event name!");

        }

        #endregion


        #region Debug(LogEventOrGroupName, LogTarget)

        /// <summary>
        /// Start debugging the given log event.
        /// </summary>
        /// <param name="LogEventOrGroupName">A log event of group name.</param>
        /// <param name="LogTarget">The log target.</param>
        public Boolean Debug(String      LogEventOrGroupName,
                             LogTargets  LogTarget)
        {

            HashSet<String> _LogEventNames = null;

            if (_GroupTags.TryGetValue(LogEventOrGroupName, out _LogEventNames))
                return _LogEventNames.
                           Select(logname => InternalDebug(logname, LogTarget)).
                           All   (result  => result == true);

            else
                return InternalDebug(LogEventOrGroupName, LogTarget);

        }

        #endregion

        #region (private) InternalDebug(LogEventName, LogTarget)

        private Boolean InternalDebug(String      LogEventName,
                                      LogTargets  LogTarget)
        {

            HTTPRequestLogger  _HTTPRequestLogger   = null;
            HTTPResponseLogger _HTTPResponseLogger  = null;
            Boolean            _Found               = false;

            if (_HTTPRequestLoggers.TryGetValue(LogEventName, out _HTTPRequestLogger))
                _Found |= _HTTPRequestLogger.Subscribe(LogTarget);

            if (_HTTPResponseLoggers.TryGetValue(LogEventName, out _HTTPResponseLogger))
                _Found |= _HTTPResponseLogger.Subscribe(LogTarget);

            return _Found;

        }

        #endregion


        #region Undebug(LogEventOrGroupName, LogTarget)

        /// <summary>
        /// Stop debugging the given log event.
        /// </summary>
        /// <param name="LogEventOrGroupName">A log event of group name.</param>
        /// <param name="LogTarget">The log target.</param>
        public Boolean Undebug(String      LogEventOrGroupName,
                               LogTargets  LogTarget)
        {

            HashSet<String> _LogEventNames = null;

            if (_GroupTags.TryGetValue(LogEventOrGroupName, out _LogEventNames))
                return _LogEventNames.
                           Select(logname => InternalUndebug(logname, LogTarget)).
                           All   (result  => result == true);

            else
                return InternalUndebug(LogEventOrGroupName, LogTarget);

        }

        #endregion

        #region (private) InternalUndebug(LogEventName, LogTarget)

        private Boolean InternalUndebug(String      LogEventName,
                                        LogTargets  LogTarget)
        {

            HTTPRequestLogger  _HTTPRequestLogger   = null;
            HTTPResponseLogger _HTTPResponseLogger  = null;
            Boolean            _Found               = false;

            if (_HTTPRequestLoggers.TryGetValue(LogEventName, out _HTTPRequestLogger))
                _Found |= _HTTPRequestLogger.Unsubscribe(LogTarget);

            if (_HTTPResponseLoggers.TryGetValue(LogEventName, out _HTTPResponseLogger))
                _Found |= _HTTPResponseLogger.Unsubscribe(LogTarget);

            return _Found;

        }

        #endregion

    }

}
