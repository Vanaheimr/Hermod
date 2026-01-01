/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An HTTP client logger.
    /// </summary>
    public class HTTPClientLogger : AHTTPLogger
    {

        #region (class) HTTPClientRequestLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPClientRequestLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, ClientRequestLogHandler>  subscriptionDelegates;
            private readonly HashSet<LogTargets>                              subscriptionStatus;

            #endregion

            #region Properties

            /// <summary>
            /// The logging path.
            /// </summary>
            public String                           LoggingPath                     { get; }

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                           Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                           LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscribed to.
            /// </summary>
            public Action<ClientRequestLogHandler>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<ClientRequestLogHandler>  UnsubscribeFromEventDelegate    { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked HTTP API event.
            /// </summary>
            /// <param name="LoggingPath">The logging path.</param>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPClientRequestLogger(String                           LoggingPath,
                                           String                           Context,
                                           String                           LogEventName,
                                           Action<ClientRequestLogHandler>  SubscribeToEventDelegate,
                                           Action<ClientRequestLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName), "The given log event name must not be null or empty!");

                #endregion

                this.LoggingPath                   = LoggingPath ?? "";
                this.Context                       = Context     ?? "";
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this.subscriptionDelegates         = new Dictionary<LogTargets, ClientRequestLogHandler>();
                this.subscriptionStatus            = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPRequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPRequestDelegate">A delegate to call.</param>
            /// <returns>An HTTP request logger.</returns>
            public HTTPClientRequestLogger RegisterLogTarget(LogTargets                 LogTarget,
                                                             HTTPRequestLoggerDelegate  HTTPRequestDelegate)
            {

                if (subscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                subscriptionDelegates.Add(LogTarget,
                                          (timestamp, HTTPAPI, Request) => HTTPRequestDelegate(LoggingPath, Context, LogEventName, Request));

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
                                                      out var clientRequestLogHandler) &&
                    clientRequestLogHandler is not null)
                {
                    SubscribeToEventDelegate(clientRequestLogHandler);
                    subscriptionStatus.Add(LogTarget);
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

                => subscriptionStatus.Contains(LogTarget);

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
                                                      out var clientRequestLogHandler) &&
                    clientRequestLogHandler is not null)
                {
                    UnsubscribeFromEventDelegate(clientRequestLogHandler);
                    subscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion

        #region (class) HTTPClientResponseLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPClientResponseLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, ClientResponseLogHandler>  subscriptionDelegates;
            private readonly HashSet<LogTargets>                               subscriptionStatus;

            #endregion

            #region Properties

            /// <summary>
            /// The logging path.
            /// </summary>
            public String                            LoggingPath                     { get; }

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                            Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                            LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscribed to.
            /// </summary>
            public Action<ClientResponseLogHandler>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<ClientResponseLogHandler>  UnsubscribeFromEventDelegate    { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked HTTP API event.
            /// </summary>
            /// <param name="LoggingPath">The logging path.</param>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPClientResponseLogger(String                            LoggingPath,
                                            String                            Context,
                                            String                            LogEventName,
                                            Action<ClientResponseLogHandler>  SubscribeToEventDelegate,
                                            Action<ClientResponseLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName), "The given log event name must not be null or empty!");

                #endregion

                this.LoggingPath                   = LoggingPath ?? "";
                this.Context                       = Context     ?? "";
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this.subscriptionDelegates         = new Dictionary<LogTargets, ClientResponseLogHandler>();
                this.subscriptionStatus            = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, HTTPResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="HTTPResponseDelegate">A delegate to call.</param>
            /// <returns>An HTTP response logger.</returns>
            public HTTPClientResponseLogger RegisterLogTarget(LogTargets                  LogTarget,
                                                              HTTPResponseLoggerDelegate  HTTPResponseDelegate)
            {

                if (subscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                subscriptionDelegates.Add(LogTarget,
                                          (timestamp, HTTPAPI, Request, Response) => HTTPResponseDelegate(LoggingPath, Context, LogEventName, Request, Response));

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
                                                      out var clientResponseLogHandler) &&
                    clientResponseLogHandler is not null)
                {
                    SubscribeToEventDelegate(clientResponseLogHandler);
                    subscriptionStatus.Add(LogTarget);
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

                => subscriptionStatus.Contains(LogTarget);

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
                                                      out var clientResponseLogHandler) &&
                    clientResponseLogHandler is not null)
                {
                    UnsubscribeFromEventDelegate(clientResponseLogHandler);
                    subscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion


        #region Data

        private readonly ConcurrentDictionary<String, HTTPClientRequestLogger>   httpClientRequestLoggers;
        private readonly ConcurrentDictionary<String, HTTPClientResponseLogger>  httpClientResponseLoggers;

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP client of this logger.
        /// </summary>
        public IHTTPClient  HTTPClient        { get; }

        /// <summary>
        /// Whether to disable HTTP client logging.
        /// </summary>
        public Boolean      DisableLogging    { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP client logger using the given logging delegates.
        /// </summary>
        /// <param name="HTTPClient">An HTTP client.</param>
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
        public HTTPClientLogger(IHTTPClient                  HTTPClient,
                                String?                      LoggingPath,
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

            this.HTTPClient                 = HTTPClient ?? throw new ArgumentNullException(nameof(HTTPClient), "The given HTTP client must not be null!");

            this.httpClientRequestLoggers   = new ConcurrentDictionary<String, HTTPClientRequestLogger>();
            this.httpClientResponseLoggers  = new ConcurrentDictionary<String, HTTPClientResponseLogger>();


            //ToDo: Evaluate Logging targets!

          //  HTTPAPI.ErrorLog += async (Timestamp,
          //                             HTTPServer,
          //                             HTTPRequest,
          //                             HTTPResponse,
          //                             Error,
          //                             LastException) => {
          //
          //              DebugX.Log(Timestamp + " - " +
          //                         HTTPRequest.RemoteSocket.IPAddress + ":" +
          //                         HTTPRequest.RemoteSocket.Port + " " +
          //                         HTTPRequest.HTTPMethod + " " +
          //                         HTTPRequest.URI + " " +
          //                         HTTPRequest.ProtocolVersion + " => " +
          //                         HTTPResponse.HTTPStatusCode + " - " +
          //                         Error);

          //         };

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
        protected HTTPClientRequestLogger RegisterEvent(String                           LogEventName,
                                                        Action<ClientRequestLogHandler>  SubscribeToEventDelegate,
                                                        Action<ClientRequestLogHandler>  UnsubscribeFromEventDelegate,
                                                        params String[]                  GroupTags)
        {

            #region Initial checks

            LogEventName = LogEventName.Trim();

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName), "The given log event name must not be null or empty!");

            #endregion

            if (!httpClientRequestLoggers. TryGetValue(LogEventName, out var httpClientRequestLogger) &&
                !httpClientResponseLoggers.ContainsKey(LogEventName))
            {

                httpClientRequestLogger = new HTTPClientRequestLogger(LoggingPath, Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                httpClientRequestLoggers.TryAdd(LogEventName, httpClientRequestLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (groupTags.TryGetValue(GroupTag, out var logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        groupTags.TryAdd(GroupTag, new HashSet<String>(new[] { LogEventName }));

                }

                #endregion

                return httpClientRequestLogger;

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
        protected HTTPClientResponseLogger RegisterEvent(String                            LogEventName,
                                                         Action<ClientResponseLogHandler>  SubscribeToEventDelegate,
                                                         Action<ClientResponseLogHandler>  UnsubscribeFromEventDelegate,
                                                         params String[]                   GroupTags)
        {

            #region Initial checks

            LogEventName = LogEventName.Trim();

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName), "The given log event name must not be null or empty!");

            #endregion

            if (!httpClientResponseLoggers.TryGetValue(LogEventName, out var httpClientResponseLogger) &&
                !httpClientRequestLoggers. ContainsKey(LogEventName))
            {

                httpClientResponseLogger = new HTTPClientResponseLogger(LoggingPath, Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                httpClientResponseLoggers.TryAdd(LogEventName, httpClientResponseLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (groupTags.TryGetValue(GroupTag, out var logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        groupTags.TryAdd(GroupTag, new HashSet<String>(new[] { LogEventName }));

                }

                #endregion

                return httpClientResponseLogger;

            }

            throw new Exception("Duplicate log event name!");

        }

        #endregion


        #region (protected) InternalDebug  (LogEventName, LogTarget)

        protected override Boolean InternalDebug(String      LogEventName,
                                                 LogTargets  LogTarget)
        {

            var found = false;

            if (httpClientRequestLoggers. TryGetValue(LogEventName, out var httpClientRequestLogger))
                found |= httpClientRequestLogger. Subscribe(LogTarget);

            if (httpClientResponseLoggers.TryGetValue(LogEventName, out var httpClientResponseLogger))
                found |= httpClientResponseLogger.Subscribe(LogTarget);

            return found;

        }

        #endregion

        #region (protected) InternalUndebug(LogEventName, LogTarget)

        protected override Boolean InternalUndebug(String      LogEventName,
                                                   LogTargets  LogTarget)
        {

            var found = false;

            if (httpClientRequestLoggers. TryGetValue(LogEventName, out var httpClientRequestLogger))
                found |= httpClientRequestLogger. Unsubscribe(LogTarget);

            if (httpClientResponseLoggers.TryGetValue(LogEventName, out var httpClientResponseLogger))
                found |= httpClientResponseLogger.Unsubscribe(LogTarget);

            return found;

        }

        #endregion


        #region RegisterLogTarget(LogTarget, RequestLogHandler)

        public void RegisterLogTarget(LogTargets                 LogTarget,
                                      HTTPRequestLoggerDelegate  RequestLogHandler)
        {

            foreach (var httpClientRequestLogger in httpClientRequestLoggers.Values)
            {
                httpClientRequestLogger.RegisterLogTarget(LogTarget, RequestLogHandler);
            }

        }

        #endregion

        #region RegisterLogTarget(LogTarget, ResponseLogHandler)

        public void RegisterLogTarget(LogTargets                  LogTarget,
                                      HTTPResponseLoggerDelegate  ResponseLogHandler)
        {

            foreach (var httpClientResponseLogger in httpClientResponseLoggers.Values)
            {
                httpClientResponseLogger.RegisterLogTarget(LogTarget, ResponseLogHandler);
            }

        }

        #endregion


    }

}
