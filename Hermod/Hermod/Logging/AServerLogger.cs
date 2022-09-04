/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Logging
{



    /// <summary>
    /// The delegate for the request log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="ServerAPI">The server API.</param>
    /// <param name="Request">The incoming request.</param>
    public delegate Task RequestLogHandler2(DateTime  Timestamp,
                                           Object    ServerAPI,
                                           String    Request);


    /// <summary>
    /// The delegate for the HTTP request log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="ServerAPI">The server API.</param>
    /// <param name="Request">The incoming request.</param>
    public delegate Task HTTPRequestLogHandler2(DateTime  Timestamp,
                                               Object    ServerAPI,
                                               String    Request);



    /// <summary>
    /// The delegate for the HTTP access log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="ServerAPI">The server API.</param>
    /// <param name="Request">The incoming request.</param>
    /// <param name="Response">The outgoing response.</param>
    public delegate Task AccessLogHandler2(DateTime  Timestamp,
                                          Object    ServerAPI,
                                          String    Request,
                                          String    Response,
                                          TimeSpan  Runtime);

    /// <summary>
    /// The delegate for the HTTP access log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="ServerAPI">The server API.</param>
    /// <param name="Request">The incoming request.</param>
    /// <param name="Response">The outgoing response.</param>
    public delegate Task HTTPResponseLogHandler(DateTime  Timestamp,
                                                Object    ServerAPI,
                                                String    Request,
                                                String    Response,
                                                TimeSpan  Runtime);



    /// <summary>
    /// A server API logger.
    /// </summary>
    public class AServerLogger : ALogger
    {

        #region (class) HTTPServerRequestLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPServerRequestLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, RequestLogHandler2>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                        _SubscriptionStatus;

            #endregion

            #region Properties

            public String                     LoggingPath                     { get; }

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                     Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                     LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscriped to.
            /// </summary>
            public Action<RequestLogHandler2>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<RequestLogHandler2>  UnsubscribeFromEventDelegate    { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked HTTP API event.
            /// </summary>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPServerRequestLogger(String                     Context,
                                           String                     LoggingPath,
                                           String                     LogEventName,
                                           Action<RequestLogHandler2>  SubscribeToEventDelegate,
                                           Action<RequestLogHandler2>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate == null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate == null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.Context                       = Context ?? "";
                this.LoggingPath                   = LoggingPath;
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, RequestLogHandler2>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, RequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="RequestDelegate">A delegate to call.</param>
            /// <returns>A HTTP request logger.</returns>
            public HTTPServerRequestLogger RegisterLogTarget(LogTargets             LogTarget,
                                                             RequestLoggerDelegate  RequestDelegate)
            {

                #region Initial checks

                if (RequestDelegate == null)
                    throw new ArgumentNullException(nameof(RequestDelegate),  "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request) => RequestDelegate(LoggingPath, Context, LogEventName, Request));

                return this;

            }

            #endregion

            #region Subscribe   (LogTarget)

            /// <summary>
            /// Subscribe the given log target to the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Subscribe(LogTargets LogTarget)
            {

                if (IsSubscribed(LogTarget))
                    return true;

                if (_SubscriptionDelegates.TryGetValue(LogTarget,
                                                       out RequestLogHandler2 _RequestLogHandler2))
                {
                    SubscribeToEventDelegate(_RequestLogHandler2);
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

                => _SubscriptionStatus.Contains(LogTarget);

            #endregion

            #region Unsubscribe (LogTarget)

            /// <summary>
            /// Unsubscribe the given log target from the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Unsubscribe(LogTargets LogTarget)
            {

                if (!IsSubscribed(LogTarget))
                    return true;

                if (_SubscriptionDelegates.TryGetValue(LogTarget,
                                                       out RequestLogHandler2 _RequestLogHandler2))
                {
                    UnsubscribeFromEventDelegate(_RequestLogHandler2);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion

        #region (class) HTTPServerRequestLogger2

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPServerRequestLogger2
        {

            #region Data

            private readonly Dictionary<LogTargets, HTTPRequestLogHandler2>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                            _SubscriptionStatus;

            #endregion

            #region Properties

            public String                         LoggingPath                     { get; }

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                         Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                         LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscriped to.
            /// </summary>
            public Action<HTTPRequestLogHandler2>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<HTTPRequestLogHandler2>  UnsubscribeFromEventDelegate    { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked HTTP API event.
            /// </summary>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPServerRequestLogger2(String                         Context,
                                            String                         LoggingPath,
                                            String                         LogEventName,
                                            Action<HTTPRequestLogHandler2>  SubscribeToEventDelegate,
                                            Action<HTTPRequestLogHandler2>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate == null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate == null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.Context                       = Context ?? "";
                this.LoggingPath                   = LoggingPath;
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, HTTPRequestLogHandler2>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, RequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="RequestDelegate">A delegate to call.</param>
            /// <returns>A HTTP request logger.</returns>
            public HTTPServerRequestLogger2 RegisterLogTarget(LogTargets             LogTarget,
                                                              RequestLoggerDelegate  RequestDelegate)
            {

                #region Initial checks

                if (RequestDelegate == null)
                    throw new ArgumentNullException(nameof(RequestDelegate),  "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request) => RequestDelegate(LoggingPath, Context, LogEventName, Request));

                return this;

            }

            #endregion

            #region Subscribe   (LogTarget)

            /// <summary>
            /// Subscribe the given log target to the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Subscribe(LogTargets LogTarget)
            {

                if (IsSubscribed(LogTarget))
                    return true;

                if (_SubscriptionDelegates.TryGetValue(LogTarget,
                                                       out HTTPRequestLogHandler2 _RequestLogHandler2))
                {
                    SubscribeToEventDelegate(_RequestLogHandler2);
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

                => _SubscriptionStatus.Contains(LogTarget);

            #endregion

            #region Unsubscribe (LogTarget)

            /// <summary>
            /// Unsubscribe the given log target from the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Unsubscribe(LogTargets LogTarget)
            {

                if (!IsSubscribed(LogTarget))
                    return true;

                if (_SubscriptionDelegates.TryGetValue(LogTarget,
                                                       out HTTPRequestLogHandler2 _RequestLogHandler2))
                {
                    UnsubscribeFromEventDelegate(_RequestLogHandler2);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion

        #region (class) HTTPServerResponseLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPServerResponseLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, AccessLogHandler2>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                       _SubscriptionStatus;

            #endregion

            #region Properties

            public String                    LoggingPath                     { get; }

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                    Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                    LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscriped to.
            /// </summary>
            public Action<AccessLogHandler2>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<AccessLogHandler2>  UnsubscribeFromEventDelegate    { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked HTTP API event.
            /// </summary>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPServerResponseLogger(String                    Context,
                                            String                    LoggingPath,
                                            String                    LogEventName,
                                            Action<AccessLogHandler2>  SubscribeToEventDelegate,
                                            Action<AccessLogHandler2>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate == null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked  HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate == null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.Context                       = Context ?? "";
                this.LoggingPath                   = LoggingPath;
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, AccessLogHandler2>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, ResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="ResponseDelegate">A delegate to call.</param>
            /// <returns>A HTTP response logger.</returns>
            public HTTPServerResponseLogger RegisterLogTarget(LogTargets              LogTarget,
                                                              ResponseLoggerDelegate  ResponseDelegate)
            {

                #region Initial checks

                if (ResponseDelegate == null)
                    throw new ArgumentNullException(nameof(ResponseDelegate), "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request, Response, Runtime) => ResponseDelegate(LoggingPath, Context, LogEventName, Request, Response, Runtime));

                return this;

            }

            #endregion

            #region Subscribe   (LogTarget)

            /// <summary>
            /// Subscribe the given log target to the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Subscribe(LogTargets LogTarget)
            {

                if (IsSubscribed(LogTarget))
                    return true;

                if (_SubscriptionDelegates.TryGetValue(LogTarget,
                                                       out AccessLogHandler2 _AccessLogHandler2))
                {
                    SubscribeToEventDelegate(_AccessLogHandler2);
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

                => _SubscriptionStatus.Contains(LogTarget);

            #endregion

            #region Unsubscribe (LogTarget)

            /// <summary>
            /// Unsubscribe the given log target from the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Unsubscribe(LogTargets LogTarget)
            {

                if (!IsSubscribed(LogTarget))
                    return true;

                if (_SubscriptionDelegates.TryGetValue(LogTarget,
                                                       out AccessLogHandler2 _AccessLogHandler2))
                {
                    UnsubscribeFromEventDelegate(_AccessLogHandler2);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion

        #region (class) HTTPServerResponseLogger2

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPServerResponseLogger2
        {

            #region Data

            private readonly Dictionary<LogTargets, HTTPResponseLogHandler>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                       _SubscriptionStatus;

            #endregion

            #region Properties

            public String                    LoggingPath                     { get; }

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                    Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                    LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscriped to.
            /// </summary>
            public Action<HTTPResponseLogHandler>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<HTTPResponseLogHandler>  UnsubscribeFromEventDelegate    { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked HTTP API event.
            /// </summary>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPServerResponseLogger2(String                          Context,
                                             String                          LoggingPath,
                                             String                          LogEventName,
                                             Action<HTTPResponseLogHandler>  SubscribeToEventDelegate,
                                             Action<HTTPResponseLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate == null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked  HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate == null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.Context                       = Context ?? "";
                this.LoggingPath                   = LoggingPath;
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, HTTPResponseLogHandler>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, ResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="ResponseDelegate">A delegate to call.</param>
            /// <returns>A HTTP response logger.</returns>
            public HTTPServerResponseLogger2 RegisterLogTarget(LogTargets              LogTarget,
                                                               ResponseLoggerDelegate  ResponseDelegate)
            {

                #region Initial checks

                if (ResponseDelegate == null)
                    throw new ArgumentNullException(nameof(ResponseDelegate), "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request, Response, Runtime) => ResponseDelegate(LoggingPath, Context, LogEventName, Request, Response, Runtime));

                return this;

            }

            #endregion

            #region Subscribe   (LogTarget)

            /// <summary>
            /// Subscribe the given log target to the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Subscribe(LogTargets LogTarget)
            {

                if (IsSubscribed(LogTarget))
                    return true;

                if (_SubscriptionDelegates.TryGetValue(LogTarget,
                                                       out HTTPResponseLogHandler _AccessLogHandler2))
                {
                    SubscribeToEventDelegate(_AccessLogHandler2);
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

                => _SubscriptionStatus.Contains(LogTarget);

            #endregion

            #region Unsubscribe (LogTarget)

            /// <summary>
            /// Unsubscribe the given log target from the linked event.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <returns>True, if successful; false else.</returns>
            public Boolean Unsubscribe(LogTargets LogTarget)
            {

                if (!IsSubscribed(LogTarget))
                    return true;

                if (_SubscriptionDelegates.TryGetValue(LogTarget,
                                                       out HTTPResponseLogHandler _AccessLogHandler2))
                {
                    UnsubscribeFromEventDelegate(_AccessLogHandler2);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion


        #region Data

        private readonly ConcurrentDictionary<String, HTTPServerRequestLogger>    _HTTPRequestLoggers;
        private readonly ConcurrentDictionary<String, HTTPServerRequestLogger2>   _HTTPRequestLoggers2;
        private readonly ConcurrentDictionary<String, HTTPServerResponseLogger>   _HTTPResponseLoggers;
        private readonly ConcurrentDictionary<String, HTTPServerResponseLogger2>  _HTTPResponseLoggers2;

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP server of this logger.
        /// </summary>
        public Object  HTTPServer   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new server API logger using the given logging delegates.
        /// </summary>
        /// <param name="ServerAPI">A server API.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="Context">A context of this API.</param>
        /// 
        /// <param name="LogRequest_toConsole">A delegate to log incoming requests to console.</param>
        /// <param name="LogResponse_toConsole">A delegate to log requests/responses to console.</param>
        /// <param name="LogRequest_toDisc">A delegate to log incoming requests to disc.</param>
        /// <param name="LogResponse_toDisc">A delegate to log requests/responses to disc.</param>
        /// 
        /// <param name="LogRequest_toNetwork">A delegate to log incoming requests to a network target.</param>
        /// <param name="LogResponse_toNetwork">A delegate to log requests/responses to a network target.</param>
        /// <param name="LogRequest_toHTTPSSE">A delegate to log incoming requests to a HTTP server sent events source.</param>
        /// <param name="LogResponse_toHTTPSSE">A delegate to log requests/responses to a HTTP server sent events source.</param>
        /// 
        /// <param name="LogError_toConsole">A delegate to log errors to console.</param>
        /// <param name="LogError_toDisc">A delegate to log errors to disc.</param>
        /// <param name="LogError_toNetwork">A delegate to log errors to a network target.</param>
        /// <param name="LogError_toHTTPSSE">A delegate to log errors to a HTTP server sent events source.</param>
        /// 
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        public AServerLogger(Object                   ServerAPI,
                             String                   LoggingPath,
                             String                   Context,

                             RequestLoggerDelegate?   LogRequest_toConsole    = null,
                             ResponseLoggerDelegate?  LogResponse_toConsole   = null,
                             RequestLoggerDelegate?   LogRequest_toDisc       = null,
                             ResponseLoggerDelegate?  LogResponse_toDisc      = null,

                             RequestLoggerDelegate?   LogRequest_toNetwork    = null,
                             ResponseLoggerDelegate?  LogResponse_toNetwork   = null,
                             RequestLoggerDelegate?   LogRequest_toHTTPSSE    = null,
                             ResponseLoggerDelegate?  LogResponse_toHTTPSSE   = null,

                             ResponseLoggerDelegate?  LogError_toConsole      = null,
                             ResponseLoggerDelegate?  LogError_toDisc         = null,
                             ResponseLoggerDelegate?  LogError_toNetwork      = null,
                             ResponseLoggerDelegate?  LogError_toHTTPSSE      = null,

                             LogfileCreatorDelegate?  LogfileCreator              = null)

            : base(LoggingPath,
                   Context,

                   LogRequest_toConsole,
                   LogResponse_toConsole,
                   LogRequest_toDisc,
                   LogResponse_toDisc,

                   LogRequest_toNetwork,
                   LogResponse_toNetwork,
                   LogRequest_toHTTPSSE,
                   LogResponse_toHTTPSSE,

                   LogError_toConsole,
                   LogError_toDisc,
                   LogError_toNetwork,
                   LogError_toHTTPSSE,

                   LogfileCreator)

        {

            this.HTTPServer             = ServerAPI ?? throw new ArgumentNullException(nameof(ServerAPI), "The given HTTP API must not be null!");

            this._HTTPRequestLoggers    = new ConcurrentDictionary<String, HTTPServerRequestLogger>();
            this._HTTPRequestLoggers2   = new ConcurrentDictionary<String, HTTPServerRequestLogger2>();
            this._HTTPResponseLoggers2  = new ConcurrentDictionary<String, HTTPServerResponseLogger2>();
            this._HTTPResponseLoggers   = new ConcurrentDictionary<String, HTTPServerResponseLogger>();

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
        protected HTTPServerRequestLogger RegisterEvent(String                     LogEventName,
                                                        Action<RequestLogHandler2>  SubscribeToEventDelegate,
                                                        Action<RequestLogHandler2>  UnsubscribeFromEventDelegate,
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

            if (!_HTTPRequestLoggers. TryGetValue(LogEventName, out HTTPServerRequestLogger _HTTPRequestLogger) &&
                !_HTTPResponseLoggers.ContainsKey(LogEventName))
            {

                _HTTPRequestLogger = new HTTPServerRequestLogger(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
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
        protected HTTPServerRequestLogger2 RegisterEvent2(String                         LogEventName,
                                                          Action<HTTPRequestLogHandler2>  SubscribeToEventDelegate,
                                                          Action<HTTPRequestLogHandler2>  UnsubscribeFromEventDelegate,
                                                          params String[]                GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate == null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate == null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            if (!_HTTPRequestLoggers2. TryGetValue(LogEventName, out HTTPServerRequestLogger2 _HTTPRequestLogger) &&
                !_HTTPResponseLoggers2.ContainsKey(LogEventName))
            {

                _HTTPRequestLogger = new HTTPServerRequestLogger2(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _HTTPRequestLoggers2.TryAdd(LogEventName, _HTTPRequestLogger);

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
        protected HTTPServerResponseLogger RegisterEvent(String                    LogEventName,
                                                         Action<AccessLogHandler2>  SubscribeToEventDelegate,
                                                         Action<AccessLogHandler2>  UnsubscribeFromEventDelegate,
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

            if (!_HTTPResponseLoggers.TryGetValue(LogEventName, out HTTPServerResponseLogger _HTTPResponseLogger) &&
                !_HTTPRequestLoggers. ContainsKey(LogEventName))
            {

                _HTTPResponseLogger = new HTTPServerResponseLogger(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
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

        #region (protected) RegisterEvent(LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate, params GroupTags)

        /// <summary>
        /// Register a log event for the linked HTTP API event.
        /// </summary>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
        /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
        /// <param name="GroupTags">An array of log event groups the given log event name is part of.</param>
        protected HTTPServerResponseLogger2 RegisterEvent2(String                          LogEventName,
                                                           Action<HTTPResponseLogHandler>  SubscribeToEventDelegate,
                                                           Action<HTTPResponseLogHandler>  UnsubscribeFromEventDelegate,
                                                           params String[]                 GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate == null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate == null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            if (!_HTTPResponseLoggers2.TryGetValue(LogEventName, out HTTPServerResponseLogger2 _HTTPResponseLogger) &&
                !_HTTPRequestLoggers2. ContainsKey(LogEventName))
            {

                _HTTPResponseLogger = new HTTPServerResponseLogger2(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _HTTPResponseLoggers2.TryAdd(LogEventName, _HTTPResponseLogger);

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


        #region (protected) InternalDebug(LogEventName, LogTarget)

        protected override Boolean InternalDebug(String      LogEventName,
                                                 LogTargets  LogTarget)
        {

            var _Found = false;

            if (_HTTPRequestLoggers.  TryGetValue(LogEventName, out HTTPServerRequestLogger    _HTTPServerRequestLogger))
                _Found |= _HTTPServerRequestLogger. Subscribe(LogTarget);

            if (_HTTPRequestLoggers2. TryGetValue(LogEventName, out HTTPServerRequestLogger2   _HTTPServerRequestLogger2))
                _Found |= _HTTPServerRequestLogger2.Subscribe(LogTarget);

            if (_HTTPResponseLoggers. TryGetValue(LogEventName, out HTTPServerResponseLogger   _HTTPServerResponseLogger))
                _Found |= _HTTPServerResponseLogger.Subscribe(LogTarget);

            if (_HTTPResponseLoggers2.TryGetValue(LogEventName, out HTTPServerResponseLogger2  _HTTPServerResponseLogger2))
                _Found |= _HTTPServerResponseLogger2.Subscribe(LogTarget);

            return _Found;

        }

        #endregion

        #region (protected) InternalUndebug(LogEventName, LogTarget)

        protected override Boolean InternalUndebug(String      LogEventName,
                                                   LogTargets  LogTarget)
        {

            var _Found = false;

            if (_HTTPRequestLoggers.  TryGetValue(LogEventName, out HTTPServerRequestLogger    _HTTPServerRequestLogger))
                _Found |= _HTTPServerRequestLogger. Unsubscribe(LogTarget);

            if (_HTTPRequestLoggers2. TryGetValue(LogEventName, out HTTPServerRequestLogger2   _HTTPServerRequestLogger2))
                _Found |= _HTTPServerRequestLogger2.Unsubscribe(LogTarget);

            if (_HTTPResponseLoggers. TryGetValue(LogEventName, out HTTPServerResponseLogger   _HTTPServerResponseLogger))
                _Found |= _HTTPServerResponseLogger.Unsubscribe(LogTarget);

            if (_HTTPResponseLoggers2.TryGetValue(LogEventName, out HTTPServerResponseLogger2  _HTTPServerResponseLogger2))
                _Found |= _HTTPServerResponseLogger2.Unsubscribe(LogTarget);

            return _Found;

        }

        #endregion


    }

}
