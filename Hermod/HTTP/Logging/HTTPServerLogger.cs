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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP API logger.
    /// </summary>
    public class HTTPServerLogger : AHTTPLogger
    {

        #region (class) HTTPServerRequestLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPServerRequestLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, RequestLogHandler>  _SubscriptionDelegates;
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
            public Action<RequestLogHandler>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<RequestLogHandler>  UnsubscribeFromEventDelegate    { get; }

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

                this.Context                       = Context ?? "";
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, RequestLogHandler>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPRequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPRequestDelegate">A delegate to call.</param>
            /// <returns>A HTTP request logger.</returns>
            public HTTPServerRequestLogger RegisterLogTarget(LogTargets                 LogTarget,
                                                             HTTPRequestLoggerDelegate  HTTPRequestDelegate)
            {

                #region Initial checks

                if (HTTPRequestDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPRequestDelegate),  "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request) => HTTPRequestDelegate(LoggingPath, Context, LogEventName, Request));

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
                                                       out RequestLogHandler _RequestLogHandler))
                {
                    SubscribeToEventDelegate(_RequestLogHandler);
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
                                                       out RequestLogHandler _RequestLogHandler))
                {
                    UnsubscribeFromEventDelegate(_RequestLogHandler);
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

            private readonly Dictionary<LogTargets, HTTPRequestLogHandler>  _SubscriptionDelegates;
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
            public Action<HTTPRequestLogHandler>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<HTTPRequestLogHandler>  UnsubscribeFromEventDelegate    { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked HTTP API event.
            /// </summary>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPServerRequestLogger2(String                     Context,
                                           String                     LogEventName,
                                           Action<HTTPRequestLogHandler>  SubscribeToEventDelegate,
                                           Action<HTTPRequestLogHandler>  UnsubscribeFromEventDelegate)
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
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, HTTPRequestLogHandler>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPRequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPRequestDelegate">A delegate to call.</param>
            /// <returns>A HTTP request logger.</returns>
            public HTTPServerRequestLogger2 RegisterLogTarget(LogTargets                 LogTarget,
                                                             HTTPRequestLoggerDelegate  HTTPRequestDelegate)
            {

                #region Initial checks

                if (HTTPRequestDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPRequestDelegate),  "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request) => HTTPRequestDelegate(LoggingPath, Context, LogEventName, Request));

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
                                                       out HTTPRequestLogHandler _RequestLogHandler))
                {
                    SubscribeToEventDelegate(_RequestLogHandler);
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
                                                       out HTTPRequestLogHandler _RequestLogHandler))
                {
                    UnsubscribeFromEventDelegate(_RequestLogHandler);
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

            private readonly Dictionary<LogTargets, AccessLogHandler>  _SubscriptionDelegates;
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
            public Action<AccessLogHandler>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<AccessLogHandler>  UnsubscribeFromEventDelegate    { get; }

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

                this.Context                       = Context ?? "";
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, AccessLogHandler>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPResponseDelegate">A delegate to call.</param>
            /// <returns>A HTTP response logger.</returns>
            public HTTPServerResponseLogger RegisterLogTarget(LogTargets                  LogTarget,
                                                              HTTPResponseLoggerDelegate  HTTPResponseDelegate)
            {

                #region Initial checks

                if (HTTPResponseDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPResponseDelegate), "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request, Response) => HTTPResponseDelegate(LoggingPath, Context, LogEventName, Request, Response));

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
                                                       out AccessLogHandler _AccessLogHandler))
                {
                    SubscribeToEventDelegate(_AccessLogHandler);
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
                                                       out AccessLogHandler _AccessLogHandler))
                {
                    UnsubscribeFromEventDelegate(_AccessLogHandler);
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
            public HTTPServerResponseLogger2(String                    Context,
                                            String                    LogEventName,
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
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, HTTPResponseLogHandler>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPResponseDelegate">A delegate to call.</param>
            /// <returns>A HTTP response logger.</returns>
            public HTTPServerResponseLogger2 RegisterLogTarget(LogTargets                  LogTarget,
                                                               HTTPResponseLoggerDelegate  HTTPResponseDelegate)
            {

                #region Initial checks

                if (HTTPResponseDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPResponseDelegate), "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request, Response) => HTTPResponseDelegate(LoggingPath, Context, LogEventName, Request, Response));

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
                                                       out HTTPResponseLogHandler _AccessLogHandler))
                {
                    SubscribeToEventDelegate(_AccessLogHandler);
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
                                                       out HTTPResponseLogHandler _AccessLogHandler))
                {
                    UnsubscribeFromEventDelegate(_AccessLogHandler);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion


        #region Data

        private readonly ConcurrentDictionary<String, HTTPServerRequestLogger>   _HTTPRequestLoggers;
        private readonly ConcurrentDictionary<String, HTTPServerRequestLogger2>   _HTTPRequestLoggers2;
        private readonly ConcurrentDictionary<String, HTTPServerResponseLogger>  _HTTPResponseLoggers;
        private readonly ConcurrentDictionary<String, HTTPServerResponseLogger2>  _HTTPResponseLoggers2;

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP server of this logger.
        /// </summary>
        public IHTTPServer  HTTPServer   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP API logger using the given logging delegates.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="LoggingPath">The logging path.</param>
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
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        public HTTPServerLogger(IHTTPServer                 HTTPServer,
                                String                      LoggingPath,
                                String                      Context,

                                HTTPRequestLoggerDelegate   LogHTTPRequest_toConsole,
                                HTTPResponseLoggerDelegate  LogHTTPResponse_toConsole,
                                HTTPRequestLoggerDelegate   LogHTTPRequest_toDisc,
                                HTTPResponseLoggerDelegate  LogHTTPResponse_toDisc,

                                HTTPRequestLoggerDelegate   LogHTTPRequest_toNetwork    = null,
                                HTTPResponseLoggerDelegate  LogHTTPResponse_toNetwork   = null,
                                HTTPRequestLoggerDelegate   LogHTTPRequest_toHTTPSSE    = null,
                                HTTPResponseLoggerDelegate  LogHTTPResponse_toHTTPSSE   = null,

                                HTTPResponseLoggerDelegate  LogHTTPError_toConsole      = null,
                                HTTPResponseLoggerDelegate  LogHTTPError_toDisc         = null,
                                HTTPResponseLoggerDelegate  LogHTTPError_toNetwork      = null,
                                HTTPResponseLoggerDelegate  LogHTTPError_toHTTPSSE      = null,

                                LogfileCreatorDelegate      LogfileCreator              = null)

            : base(LoggingPath,
                   Context,

                   LogHTTPRequest_toConsole,
                   LogHTTPResponse_toConsole,
                   LogHTTPRequest_toDisc,
                   LogHTTPResponse_toDisc,

                   LogHTTPRequest_toNetwork,
                   LogHTTPResponse_toNetwork,
                   LogHTTPRequest_toHTTPSSE,
                   LogHTTPResponse_toHTTPSSE,

                   LogHTTPError_toConsole,
                   LogHTTPError_toDisc,
                   LogHTTPError_toNetwork,
                   LogHTTPError_toHTTPSSE,

                   LogfileCreator)

        {

            this.HTTPServer             = HTTPServer ?? throw new ArgumentNullException(nameof(HTTPServer), "The given HTTP API must not be null!");

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

            if (!_HTTPRequestLoggers. TryGetValue(LogEventName, out HTTPServerRequestLogger _HTTPRequestLogger) &&
                !_HTTPResponseLoggers.ContainsKey(LogEventName))
            {

                _HTTPRequestLogger = new HTTPServerRequestLogger(Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
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
                                                          Action<HTTPRequestLogHandler>  SubscribeToEventDelegate,
                                                          Action<HTTPRequestLogHandler>  UnsubscribeFromEventDelegate,
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

                _HTTPRequestLogger = new HTTPServerRequestLogger2(Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
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

            if (!_HTTPResponseLoggers.TryGetValue(LogEventName, out HTTPServerResponseLogger _HTTPResponseLogger) &&
                !_HTTPRequestLoggers. ContainsKey(LogEventName))
            {

                _HTTPResponseLogger = new HTTPServerResponseLogger(Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
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

                _HTTPResponseLogger = new HTTPServerResponseLogger2(Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
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
