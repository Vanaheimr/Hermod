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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

using static org.GraphDefined.Vanaheimr.Hermod.Logging.AClientLogger;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Logging
{

    public static class AClientLoggerExtensions
    {

        #region RegisterDefaultConsoleLogTarget(this APIClientRequestLogger,  Logger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="APIClientRequestLogger">A request logger.</param>
        /// <param name="Logger">A logger.</param>
        public static APIClientRequestLogger  RegisterDefaultConsoleLogTarget(this APIClientRequestLogger  APIClientRequestLogger,
                                                                              ALogger                      Logger)

            => APIClientRequestLogger.RegisterLogTarget(LogTargets.Console,
                                                        Logger.Default_LogRequest_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTarget(this APIClientResponseLogger, Logger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="APIClientResponseLogger">A response logger.</param>
        /// <param name="Logger">A logger.</param>
        public static APIClientResponseLogger RegisterDefaultConsoleLogTarget(this APIClientResponseLogger  APIClientResponseLogger,
                                                                              ALogger                       Logger)

            => APIClientResponseLogger.RegisterLogTarget(LogTargets.Console,
                                                         Logger.Default_LogResponse_toConsole);

        #endregion


        #region RegisterDefaultDiscLogTarget(this APIClientRequestLogger,  Logger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="APIClientRequestLogger">A request logger.</param>
        /// <param name="Logger">A logger.</param>
        public static APIClientRequestLogger  RegisterDefaultDiscLogTarget(this APIClientRequestLogger  APIClientRequestLogger,
                                                                           ALogger                      Logger)

            => APIClientRequestLogger.RegisterLogTarget(LogTargets.Disc,
                                                        Logger.Default_LogRequest_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTarget(this APIClientResponseLogger, Logger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="APIClientResponseLogger">A response logger.</param>
        /// <param name="Logger">A logger.</param>
        public static APIClientResponseLogger RegisterDefaultDiscLogTarget(this APIClientResponseLogger  APIClientResponseLogger,
                                                                           ALogger                       Logger)

            => APIClientResponseLogger.RegisterLogTarget(LogTargets.Disc,
                                                         Logger.Default_LogResponse_toDisc);

        #endregion

    }


    /// <summary>
    /// An abstract client logger.
    /// </summary>
    public abstract class AClientLogger : ALogger
    {

        #region (class) APIClientRequestLogger

        /// <summary>
        /// A wrapper class to manage API event subscriptions for logging purposes.
        /// </summary>
        public class APIClientRequestLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, APIClientRequestLogHandler>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                                 _SubscriptionStatus;

            #endregion

            #region Properties

            /// <summary>
            /// The logging path.
            /// </summary>
            public String                              LoggingPath                     { get; }

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                              Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                              LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscribed to.
            /// </summary>
            public Action<APIClientRequestLogHandler>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<APIClientRequestLogHandler>  UnsubscribeFromEventDelegate    { get; }

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
            public APIClientRequestLogger(String                              LoggingPath,
                                          String                              Context,
                                          String                              LogEventName,
                                          Action<APIClientRequestLogHandler>  SubscribeToEventDelegate,
                                          Action<APIClientRequestLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate     is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.LoggingPath                   = LoggingPath ?? "";
                this.Context                       = Context     ?? "";
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, APIClientRequestLogHandler>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, RequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="RequestDelegate">A delegate to call.</param>
            /// <returns>A request logger.</returns>
            public APIClientRequestLogger RegisterLogTarget(LogTargets             LogTarget,
                                                            RequestLoggerDelegate  RequestDelegate)
            {

                #region Initial checks

                if (RequestDelegate is null)
                    throw new ArgumentNullException(nameof(RequestDelegate),  "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                _SubscriptionDelegates.Add(LogTarget,
                                           (timestamp, HTTPAPI, Request) => RequestDelegate(LoggingPath, Context, LogEventName, Request));

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
                                                       out APIClientRequestLogHandler clientRequestLogHandler))
                {
                    SubscribeToEventDelegate(clientRequestLogHandler);
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
                                                       out APIClientRequestLogHandler clientRequestLogHandler))
                {
                    UnsubscribeFromEventDelegate(clientRequestLogHandler);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion

        #region (class) APIClientResponseLogger

        /// <summary>
        /// A wrapper class to manage API event subscriptions for logging purposes.
        /// </summary>
        public class APIClientResponseLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, APIClientResponseLogHandler>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                                  _SubscriptionStatus;

            #endregion

            #region Properties

            /// <summary>
            /// The logging path.
            /// </summary>
            public String                               LoggingPath                     { get; }

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                               Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                               LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscribed to.
            /// </summary>
            public Action<APIClientResponseLogHandler>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<APIClientResponseLogHandler>  UnsubscribeFromEventDelegate    { get; }

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
            public APIClientResponseLogger(String                               LoggingPath,
                                           String                               Context,
                                           String                               LogEventName,
                                           Action<APIClientResponseLogHandler>  SubscribeToEventDelegate,
                                           Action<APIClientResponseLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate     is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked  HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.LoggingPath                   = LoggingPath ?? "";
                this.Context                       = Context     ?? "";
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, APIClientResponseLogHandler>();
                this._SubscriptionStatus           = new HashSet<LogTargets>();

            }

            #endregion


            #region RegisterLogTarget(LogTarget, ResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="ResponseDelegate">A delegate to call.</param>
            /// <returns>An HTTP response logger.</returns>
            public APIClientResponseLogger RegisterLogTarget(LogTargets             LogTarget,
                                                             ResponseLoggerDelegate ResponseDelegate)
            {

                #region Initial checks

                if (ResponseDelegate is null)
                    throw new ArgumentNullException(nameof(ResponseDelegate), "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                _SubscriptionDelegates.Add(LogTarget,
                                           (timestamp, HTTPAPI, Request, Response, Runtime) => ResponseDelegate(LoggingPath, Context, LogEventName, Request, Response, Runtime));

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
                                                       out APIClientResponseLogHandler clientResponseLogHandler))
                {
                    SubscribeToEventDelegate(clientResponseLogHandler);
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
                                                       out APIClientResponseLogHandler clientResponseLogHandler))
                {
                    UnsubscribeFromEventDelegate(clientResponseLogHandler);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion


        #region Data

        private readonly ConcurrentDictionary<String, APIClientRequestLogger>   _HTTPClientRequestLoggers;
        private readonly ConcurrentDictionary<String, APIClientResponseLogger>  _HTTPClientResponseLoggers;

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
        public AClientLogger(IHTTPClient              HTTPClient,
                            String?                  LoggingPath,
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

                            LogfileCreatorDelegate?  LogfileCreator          = null)

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

            this.HTTPClient                  = HTTPClient ?? throw new ArgumentNullException(nameof(HTTPClient), "The given HTTP client must not be null!");

            this._HTTPClientRequestLoggers   = new ConcurrentDictionary<String, APIClientRequestLogger>();
            this._HTTPClientResponseLoggers  = new ConcurrentDictionary<String, APIClientResponseLogger>();


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


        #region (protected) RegisterRequestEvent  (LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate, params GroupTags)

        /// <summary>
        /// Register a log event for the linked HTTP API event.
        /// </summary>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
        /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
        /// <param name="GroupTags">An array of log event groups the given log event name is part of.</param>
        protected APIClientRequestLogger RegisterRequestEvent(String                              LogEventName,
                                                              Action<APIClientRequestLogHandler>  SubscribeToEventDelegate,
                                                              Action<APIClientRequestLogHandler>  UnsubscribeFromEventDelegate,
                                                              params String[]                     GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate is null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate is null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            if (!_HTTPClientRequestLoggers. TryGetValue(LogEventName, out APIClientRequestLogger? apiClientRequestLogger) &&
                !_HTTPClientResponseLoggers.ContainsKey(LogEventName))
            {

                apiClientRequestLogger = new APIClientRequestLogger(LoggingPath, Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _HTTPClientRequestLoggers.TryAdd(LogEventName, apiClientRequestLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (groupTags.TryGetValue(GroupTag, out HashSet<String>? logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        groupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return apiClientRequestLogger;

            }

            throw new Exception("Duplicate log event name!");

        }

        #endregion

        #region (protected) RegisterResponseEvent (LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate, params GroupTags)

        /// <summary>
        /// Register a log event for the linked HTTP API event.
        /// </summary>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
        /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
        /// <param name="GroupTags">An array of log event groups the given log event name is part of.</param>
        protected APIClientResponseLogger RegisterResponseEvent(String                               LogEventName,
                                                                Action<APIClientResponseLogHandler>  SubscribeToEventDelegate,
                                                                Action<APIClientResponseLogHandler>  UnsubscribeFromEventDelegate,
                                                                params String[]                      GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate is null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate is null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            if (!_HTTPClientResponseLoggers.TryGetValue(LogEventName, out APIClientResponseLogger? httpClientResponseLogger) &&
                !_HTTPClientRequestLoggers. ContainsKey(LogEventName))
            {

                httpClientResponseLogger = new APIClientResponseLogger(LoggingPath, Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _HTTPClientResponseLoggers.TryAdd(LogEventName, httpClientResponseLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (groupTags.TryGetValue(GroupTag, out HashSet<String>? logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        groupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return httpClientResponseLogger;

            }

            throw new Exception("Duplicate log event name!");

        }

        #endregion


        #region (protected) InternalDebug   (LogEventName, LogTarget)

        protected override Boolean InternalDebug(String      LogEventName,
                                                 LogTargets  LogTarget)
        {

            var found = false;

            // HTTP Client
            if (_HTTPClientRequestLoggers. TryGetValue(LogEventName, out APIClientRequestLogger?  apiClientRequestLogger))
                found |= apiClientRequestLogger. Subscribe(LogTarget);

            if (_HTTPClientResponseLoggers.TryGetValue(LogEventName, out APIClientResponseLogger? apiClientResponseLogger))
                found |= apiClientResponseLogger.Subscribe(LogTarget);

            return found;

        }

        #endregion

        #region (protected) InternalUndebug (LogEventName, LogTarget)

        protected override Boolean InternalUndebug(String      LogEventName,
                                                   LogTargets  LogTarget)
        {

            var found = false;

            if (_HTTPClientRequestLoggers. TryGetValue(LogEventName, out APIClientRequestLogger?  apiClientRequestLogger))
                found |= apiClientRequestLogger. Unsubscribe(LogTarget);

            if (_HTTPClientResponseLoggers.TryGetValue(LogEventName, out APIClientResponseLogger? apiClientResponseLogger))
                found |= apiClientResponseLogger.Unsubscribe(LogTarget);

            return found;

        }

        #endregion


    }

}
