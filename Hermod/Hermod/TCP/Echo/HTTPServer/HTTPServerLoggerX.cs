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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Logging;
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An HTTP API logger.
    /// </summary>
    public class HTTPServerLoggerX : AHTTPLoggerX
    {

        #region (class) HTTPServerRequestLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPServerRequestLogger
        {

            #region Data

            private readonly ConcurrentDictionary<LogTargets, RequestLogHandler>  subscriptionDelegates = new();
            private readonly HashSet<LogTargets>                                  _SubscriptionStatus;

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
            /// A delegate called whenever the event is subscribed to.
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
                                           String                     LoggingPath,
                                           String                     LogEventName,
                                           Action<RequestLogHandler>  SubscribeToEventDelegate,
                                           Action<RequestLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate     is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.Context                       = Context ?? "";
                this.LoggingPath                   = LoggingPath;
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPRequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPRequestDelegate">A delegate to call.</param>
            /// <returns>An HTTP request logger.</returns>
            public HTTPServerRequestLogger RegisterLogTarget(LogTargets                 LogTarget,
                                                             HTTPRequestLoggerDelegate  HTTPRequestDelegate)
            {

                if (subscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                subscriptionDelegates.TryAdd(LogTarget,
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

                if (subscriptionDelegates.TryGetValue(LogTarget,
                                                      out var requestLogHandler))
                {
                    SubscribeToEventDelegate(requestLogHandler);
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

                if (subscriptionDelegates.TryGetValue(LogTarget,
                                                       out var requestLogHandler))
                {
                    UnsubscribeFromEventDelegate(requestLogHandler);
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

            private readonly ConcurrentDictionary<LogTargets, HTTPRequestLogHandlerX>  subscriptionDelegates = new();
            private readonly HashSet<LogTargets>                                      _SubscriptionStatus;

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
            /// A delegate called whenever the event is subscribed to.
            /// </summary>
            public Action<HTTPRequestLogHandlerX>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<HTTPRequestLogHandlerX>  UnsubscribeFromEventDelegate    { get; }

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
                                            Action<HTTPRequestLogHandlerX>  SubscribeToEventDelegate,
                                            Action<HTTPRequestLogHandlerX>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate     is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.Context                       = Context ?? "";
                this.LoggingPath                   = LoggingPath;
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPRequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPRequestDelegate">A delegate to call.</param>
            /// <returns>An HTTP request logger.</returns>
            public HTTPServerRequestLogger2 RegisterLogTarget(LogTargets                 LogTarget,
                                                              HTTPRequestLoggerDelegate  HTTPRequestDelegate)
            {

                if (subscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                subscriptionDelegates.TryAdd(LogTarget,
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

                if (subscriptionDelegates.TryGetValue(LogTarget,
                                                      out var requestLogHandler))
                {
                    SubscribeToEventDelegate(requestLogHandler);
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

                if (subscriptionDelegates.TryGetValue(LogTarget,
                                                      out var requestLogHandler))
                {
                    UnsubscribeFromEventDelegate(requestLogHandler);
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

            private readonly ConcurrentDictionary<LogTargets, AccessLogHandler>  subscriptionDelegates = new();
            private readonly HashSet<LogTargets>                                 _SubscriptionStatus;

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
            /// A delegate called whenever the event is subscribed to.
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
                                            String                    LoggingPath,
                                            String                    LogEventName,
                                            Action<AccessLogHandler>  SubscribeToEventDelegate,
                                            Action<AccessLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate     is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked  HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.Context                       = Context ?? "";
                this.LoggingPath                   = LoggingPath;
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPResponseDelegate">A delegate to call.</param>
            /// <returns>An HTTP response logger.</returns>
            public HTTPServerResponseLogger RegisterLogTarget(LogTargets                  LogTarget,
                                                              HTTPResponseLoggerDelegate  HTTPResponseDelegate)
            {

                if (subscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                subscriptionDelegates.TryAdd(LogTarget,
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

                if (subscriptionDelegates.TryGetValue(LogTarget,
                                                      out var accessLogHandler))
                {
                    SubscribeToEventDelegate(accessLogHandler);
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

                if (subscriptionDelegates.TryGetValue(LogTarget,
                                                      out var accessLogHandler))
                {
                    UnsubscribeFromEventDelegate(accessLogHandler);
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

            private readonly ConcurrentDictionary<LogTargets, HTTPResponseLogHandlerX>  subscriptionDelegates = new();
            private readonly HashSet<LogTargets>                                       _SubscriptionStatus;

            #endregion

            #region Properties

            public String                          LoggingPath                     { get; }

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                          Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                          LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscribed to.
            /// </summary>
            public Action<HTTPResponseLogHandlerX>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<HTTPResponseLogHandlerX>  UnsubscribeFromEventDelegate    { get; }

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
                                             Action<HTTPResponseLogHandlerX>  SubscribeToEventDelegate,
                                             Action<HTTPResponseLogHandlerX>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate     is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked  HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.Context                       = Context ?? "";
                this.LoggingPath                   = LoggingPath;
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPResponseDelegate">A delegate to call.</param>
            /// <returns>An HTTP response logger.</returns>
            public HTTPServerResponseLogger2 RegisterLogTarget(LogTargets                  LogTarget,
                                                               HTTPResponseLoggerDelegate  HTTPResponseDelegate)
            {

                if (subscriptionDelegates.ContainsKey(LogTarget))
                    throw new ArgumentException("Duplicate log target!", nameof(LogTarget));

                subscriptionDelegates.TryAdd(LogTarget,
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

                if (subscriptionDelegates.TryGetValue(LogTarget,
                                                      out var accessLogHandler))
                {
                    SubscribeToEventDelegate(accessLogHandler);
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

                if (subscriptionDelegates.TryGetValue(LogTarget,
                                                      out var accessLogHandler))
                {
                    UnsubscribeFromEventDelegate(accessLogHandler);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion


        #region Data

        private readonly ConcurrentDictionary<String, HTTPServerRequestLogger>    httpRequestLoggers;
        private readonly ConcurrentDictionary<String, HTTPServerRequestLogger2>   httpRequestLoggers2;
        private readonly ConcurrentDictionary<String, HTTPServerResponseLogger>   httpResponseLoggers;
        private readonly ConcurrentDictionary<String, HTTPServerResponseLogger2>  httpResponseLoggers2;

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP server of this logger.
        /// </summary>
        public HTTPTestServerX  HTTPServer    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP API logger using the given logging delegates.
        /// </summary>
        /// <param name="HTTPServer">An HTTP server.</param>
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
        public HTTPServerLoggerX(HTTPTestServerX                 HTTPServer,
                                String                       LoggingPath,
                                String                       Context,

                                HTTPRequestLoggerDelegate?   LogHTTPRequest_toConsole    = null,
                                HTTPResponseLoggerDelegate?  LogHTTPResponse_toConsole   = null,
                                HTTPRequestLoggerDelegate?   LogHTTPRequest_toDisc       = null,
                                HTTPResponseLoggerDelegate?  LogHTTPResponse_toDisc      = null,

                                HTTPRequestLoggerDelegate?   LogHTTPRequest_toNetwork    = null,
                                HTTPResponseLoggerDelegate?  LogHTTPResponse_toNetwork   = null,
                                HTTPRequestLoggerDelegate?   LogHTTPRequest_toHTTPSSE    = null,
                                HTTPResponseLoggerDelegate?  LogHTTPResponse_toHTTPSSE   = null,

                                HTTPResponseLoggerDelegate?  LogHTTPError_toConsole      = null,
                                HTTPResponseLoggerDelegate?  LogHTTPError_toDisc         = null,
                                HTTPResponseLoggerDelegate?  LogHTTPError_toNetwork      = null,
                                HTTPResponseLoggerDelegate?  LogHTTPError_toHTTPSSE      = null,

                                LogfileCreatorDelegate?      LogfileCreator              = null)

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

            this.HTTPServer            = HTTPServer ?? throw new ArgumentNullException(nameof(HTTPServer), "The given HTTP API must not be null!");

            this.httpRequestLoggers    = new ConcurrentDictionary<String, HTTPServerRequestLogger>();
            this.httpRequestLoggers2   = new ConcurrentDictionary<String, HTTPServerRequestLogger2>();
            this.httpResponseLoggers2  = new ConcurrentDictionary<String, HTTPServerResponseLogger2>();
            this.httpResponseLoggers   = new ConcurrentDictionary<String, HTTPServerResponseLogger>();

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

            LogEventName = LogEventName.Trim();

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName), "The given log event name must not be null or empty!");

            #endregion

            if (!httpRequestLoggers. TryGetValue(LogEventName, out var httpRequestLogger) &&
                !httpResponseLoggers.ContainsKey(LogEventName))
            {

                httpRequestLogger = new HTTPServerRequestLogger(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                httpRequestLoggers.TryAdd(LogEventName, httpRequestLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (groupTags.TryGetValue(GroupTag, out var logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        groupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return httpRequestLogger;

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
                                                          Action<HTTPRequestLogHandlerX>  SubscribeToEventDelegate,
                                                          Action<HTTPRequestLogHandlerX>  UnsubscribeFromEventDelegate,
                                                          params String[]                GroupTags)
        {

            #region Initial checks

            LogEventName = LogEventName.Trim();

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName), "The given log event name must not be null or empty!");

            #endregion

            if (!httpRequestLoggers2. TryGetValue(LogEventName, out var httpRequestLogger) &&
                !httpResponseLoggers2.ContainsKey(LogEventName))
            {

                httpRequestLogger = new HTTPServerRequestLogger2(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                httpRequestLoggers2.TryAdd(LogEventName, httpRequestLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (groupTags.TryGetValue(GroupTag, out var logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        groupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return httpRequestLogger;

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
                throw new ArgumentNullException(nameof(LogEventName), "The given log event name must not be null or empty!");

            #endregion

            if (!httpResponseLoggers.TryGetValue(LogEventName, out var httpResponseLogger) &&
                !httpRequestLoggers. ContainsKey(LogEventName))
            {

                httpResponseLogger = new HTTPServerResponseLogger(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                httpResponseLoggers.TryAdd(LogEventName, httpResponseLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (groupTags.TryGetValue(GroupTag, out var logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        groupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return httpResponseLogger;

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
                                                           Action<HTTPResponseLogHandlerX>  SubscribeToEventDelegate,
                                                           Action<HTTPResponseLogHandlerX>  UnsubscribeFromEventDelegate,
                                                           params String[]                 GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName), "The given log event name must not be null or empty!");

            #endregion

            if (!httpResponseLoggers2.TryGetValue(LogEventName, out var httpResponseLogger) &&
                !httpRequestLoggers2. ContainsKey(LogEventName))
            {

                httpResponseLogger = new HTTPServerResponseLogger2(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                httpResponseLoggers2.TryAdd(LogEventName, httpResponseLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (groupTags.TryGetValue(GroupTag, out var logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        groupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return httpResponseLogger;

            }

            throw new Exception("Duplicate log event name!");

        }

        #endregion


        #region (protected) InternalDebug  (LogEventName, LogTarget)

        protected override Boolean InternalDebug(String      LogEventName,
                                                 LogTargets  LogTarget)
        {

            var found = false;

            if (httpRequestLoggers.  TryGetValue(LogEventName, out var httpServerRequestLogger))
                found |= httpServerRequestLogger. Subscribe(LogTarget);

            if (httpRequestLoggers2. TryGetValue(LogEventName, out var httpServerRequestLogger2))
                found |= httpServerRequestLogger2.Subscribe(LogTarget);

            if (httpResponseLoggers. TryGetValue(LogEventName, out var httpServerResponseLogger))
                found |= httpServerResponseLogger.Subscribe(LogTarget);

            if (httpResponseLoggers2.TryGetValue(LogEventName, out var httpServerResponseLogger2))
                found |= httpServerResponseLogger2.Subscribe(LogTarget);

            return found;

        }

        #endregion

        #region (protected) InternalUndebug(LogEventName, LogTarget)

        protected override Boolean InternalUndebug(String      LogEventName,
                                                   LogTargets  LogTarget)
        {

            var found = false;

            if (httpRequestLoggers.  TryGetValue(LogEventName, out var httpServerRequestLogger))
                found |= httpServerRequestLogger. Unsubscribe(LogTarget);

            if (httpRequestLoggers2. TryGetValue(LogEventName, out var httpServerRequestLogger2))
                found |= httpServerRequestLogger2.Unsubscribe(LogTarget);

            if (httpResponseLoggers. TryGetValue(LogEventName, out var httpServerResponseLogger))
                found |= httpServerResponseLogger.Unsubscribe(LogTarget);

            if (httpResponseLoggers2.TryGetValue(LogEventName, out var httpServerResponseLogger2))
                found |= httpServerResponseLogger2.Unsubscribe(LogTarget);

            return found;

        }

        #endregion


        #region RegisterLogTarget(LogTarget, RequestLogHandler)

        public void RegisterLogTarget(LogTargets                 LogTarget,
                                      HTTPRequestLoggerDelegate  RequestLogHandler)
        {

            foreach (var httpServerRequestLogger in httpRequestLoggers.Values)
            {
                httpServerRequestLogger.RegisterLogTarget(LogTarget, RequestLogHandler);
            }

        }

        #endregion

        #region RegisterLogTarget(LogTarget, ResponseLogHandler)

        public void RegisterLogTarget(LogTargets                  LogTarget,
                                      HTTPResponseLoggerDelegate  ResponseLogHandler)
        {

            foreach (var httpServerResponseLogger in httpResponseLoggers.Values)
            {
                httpServerResponseLogger.RegisterLogTarget(LogTarget, ResponseLogHandler);
            }

        }

        #endregion


    }

}
