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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

using static org.GraphDefined.Vanaheimr.Hermod.Logging.AServerLogger;

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
                                            String?   Request);

    /// <summary>
    /// The delegate for the HTTP access log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="ServerAPI">The server API.</param>
    /// <param name="Request">The incoming request.</param>
    /// <param name="Response">The outgoing response.</param>
    public delegate Task ResponseLogHandler2(DateTime  Timestamp,
                                             Object    ServerAPI,
                                             String?   Request,
                                             String?   Response,
                                             TimeSpan  Runtime);


    public static class AServerLoggerExtensions
    {

        #region RegisterDefaultConsoleLogTarget(this ServerRequestLogger, HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="ServerRequestLogger">A server request logger.</param>
        /// <param name="ServerLogger">A Server API logger.</param>
        public static ServerRequestLogger RegisterDefaultConsoleLogTarget(this ServerRequestLogger  ServerRequestLogger,
                                                                          AServerLogger             ServerLogger)

            => ServerRequestLogger.RegisterLogTarget(LogTargets.Console,
                                                     ServerLogger.Default_LogRequest_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTarget(this ServerResponseLogger, Logger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="ServerResponseLogger">A server response logger.</param>
        /// <param name="ServerLogger">A Server API logger.</param>
        public static ServerResponseLogger RegisterDefaultConsoleLogTarget(this ServerResponseLogger  ServerResponseLogger,
                                                                           AServerLogger              ServerLogger)

            => ServerResponseLogger.RegisterLogTarget(LogTargets.Console,
                                                      ServerLogger.Default_LogResponse_toConsole);

        #endregion


        #region RegisterDefaultDiscLogTarget(this ServerRequestLogger,  Logger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="ServerRequestLogger">A server request logger.</param>
        /// <param name="ServerLogger">A Server API logger.</param>
        public static ServerRequestLogger RegisterDefaultDiscLogTarget(this ServerRequestLogger  ServerRequestLogger,
                                                                       AServerLogger             ServerLogger)

            => ServerRequestLogger.RegisterLogTarget(LogTargets.Disc,
                                                     ServerLogger.Default_LogRequest_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTarget(this ServerResponseLogger, Logger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="ServerResponseLogger">A server response logger.</param>
        /// <param name="ServerLogger">A Server API logger.</param>
        public static ServerResponseLogger RegisterDefaultDiscLogTarget(this ServerResponseLogger  ServerResponseLogger,
                                                                        AServerLogger              ServerLogger)

            => ServerResponseLogger.RegisterLogTarget(LogTargets.Disc,
                                                     ServerLogger.Default_LogResponse_toDisc);

        #endregion

    }


    /// <summary>
    /// An abstract server API logger.
    /// </summary>
    public abstract class AServerLogger : ALogger
    {

        #region (class) ServerRequestLogger

        /// <summary>
        /// A wrapper class to manage API event subscriptions for logging purposes.
        /// </summary>
        public class ServerRequestLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, RequestLogHandler2>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                         _SubscriptionStatus;

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
            /// Create a new log event for the linked Server API event.
            /// </summary>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public ServerRequestLogger(String                      Context,
                                       String                      LoggingPath,
                                       String                      LogEventName,
                                       Action<RequestLogHandler2>  SubscribeToEventDelegate,
                                       Action<RequestLogHandler2>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked Server API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked Server API event must not be null!");

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
            public ServerRequestLogger RegisterLogTarget(LogTargets             LogTarget,
                                                         RequestLoggerDelegate  RequestDelegate)
            {

                #region Initial checks

                if (RequestDelegate is null)
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
                                                       out RequestLogHandler2 requestLogHandler2))
                {
                    SubscribeToEventDelegate(requestLogHandler2);
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
                                                       out RequestLogHandler2? requestLogHandler2))
                {
                    UnsubscribeFromEventDelegate(requestLogHandler2);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion

        #region (class) ServerResponseLogger

        /// <summary>
        /// A wrapper class to manage API event subscriptions for logging purposes.
        /// </summary>
        public class ServerResponseLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, ResponseLogHandler2>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                          _SubscriptionStatus;

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
            public Action<ResponseLogHandler2>  SubscribeToEventDelegate        { get; }

            /// <summary>
            /// A delegate called whenever the subscription of the event is stopped.
            /// </summary>
            public Action<ResponseLogHandler2>  UnsubscribeFromEventDelegate    { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new log event for the linked Server API event.
            /// </summary>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public ServerResponseLogger(String                       Context,
                                        String                       LoggingPath,
                                        String                       LogEventName,
                                        Action<ResponseLogHandler2>  SubscribeToEventDelegate,
                                        Action<ResponseLogHandler2>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked  HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked Server API event must not be null!");

                #endregion

                this.Context                       = Context ?? "";
                this.LoggingPath                   = LoggingPath;
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, ResponseLogHandler2>();
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
            public ServerResponseLogger RegisterLogTarget(LogTargets              LogTarget,
                                                          ResponseLoggerDelegate  ResponseDelegate)
            {

                #region Initial checks

                if (ResponseDelegate is null)
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
                                                       out ResponseLogHandler2? responseLogHandler2))
                {
                    SubscribeToEventDelegate(responseLogHandler2);
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
                                                       out ResponseLogHandler2? responseLogHandler2))
                {
                    UnsubscribeFromEventDelegate(responseLogHandler2);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion


        #region Data

        private readonly ConcurrentDictionary<String, ServerRequestLogger>   _ServerRequestLoggers;
        private readonly ConcurrentDictionary<String, ServerResponseLogger>  _ServerResponseLoggers;

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP server of this logger.
        /// </summary>
        public Object  ServerAPI   { get; }

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

            this.ServerAPI               = ServerAPI ?? throw new ArgumentNullException(nameof(ServerAPI), "The given Server API must not be null!");

            this._ServerRequestLoggers   = new ConcurrentDictionary<String, ServerRequestLogger>();
            this._ServerResponseLoggers  = new ConcurrentDictionary<String, ServerResponseLogger>();

        }

        #endregion


        #region (protected) RegisterEvent(LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate, params GroupTags)

        /// <summary>
        /// Register a log event for the linked Server API event.
        /// </summary>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
        /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
        /// <param name="GroupTags">An array of log event groups the given log event name is part of.</param>
        protected ServerRequestLogger RegisterEvent(String                      LogEventName,
                                                    Action<RequestLogHandler2>  SubscribeToEventDelegate,
                                                    Action<RequestLogHandler2>  UnsubscribeFromEventDelegate,
                                                    params String[]             GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate is null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked Server API event must not be null!");

            if (UnsubscribeFromEventDelegate is null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked Server API event must not be null!");

            #endregion

            if (!_ServerRequestLoggers. TryGetValue(LogEventName, out ServerRequestLogger? serverRequestLogger) &&
                !_ServerResponseLoggers.ContainsKey(LogEventName))
            {

                serverRequestLogger = new ServerRequestLogger(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _ServerRequestLoggers.TryAdd(LogEventName, serverRequestLogger);

                #region Register group tag mapping

                foreach (var groupTag in GroupTags.Distinct())
                {

                    if (_GroupTags.TryGetValue(groupTag, out var logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        _GroupTags.TryAdd(groupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return serverRequestLogger;

            }

            throw new Exception("Duplicate log event name!");

        }

        #endregion

        #region (protected) RegisterEvent(LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate, params GroupTags)

        /// <summary>
        /// Register a log event for the linked Server API event.
        /// </summary>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
        /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
        /// <param name="GroupTags">An array of log event groups the given log event name is part of.</param>
        protected ServerResponseLogger RegisterEvent(String                       LogEventName,
                                                     Action<ResponseLogHandler2>  SubscribeToEventDelegate,
                                                     Action<ResponseLogHandler2>  UnsubscribeFromEventDelegate,
                                                     params String[]              GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate is null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked Server API event must not be null!");

            if (UnsubscribeFromEventDelegate is null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked Server API event must not be null!");

            #endregion

            if (!_ServerResponseLoggers.TryGetValue(LogEventName, out ServerResponseLogger? serverResponseLogger) &&
                !_ServerRequestLoggers. ContainsKey(LogEventName))
            {

                serverResponseLogger = new ServerResponseLogger(Context, LoggingPath, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _ServerResponseLoggers.TryAdd(LogEventName, serverResponseLogger);

                #region Register group tag mapping

                foreach (var groupTag in GroupTags.Distinct())
                {

                    if (_GroupTags.TryGetValue(groupTag, out var logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        _GroupTags.TryAdd(groupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return serverResponseLogger;

            }

            throw new Exception("Duplicate log event name!");

        }

        #endregion


        #region (protected) InternalDebug  (LogEventName, LogTarget)

        protected override Boolean InternalDebug(String      LogEventName,
                                                 LogTargets  LogTarget)
        {

            var found = false;

            if (_ServerRequestLoggers.  TryGetValue(LogEventName, out ServerRequestLogger?    _HTTPServerRequestLogger))
                found |= _HTTPServerRequestLogger. Subscribe(LogTarget);

            //if (_ServerRequestLoggers2. TryGetValue(LogEventName, out ServerRequestLogger2?   _HTTPServerRequestLogger2))
            //    found |= _HTTPServerRequestLogger2.Subscribe(LogTarget);

            if (_ServerResponseLoggers. TryGetValue(LogEventName, out ServerResponseLogger?   _HTTPServerResponseLogger))
                found |= _HTTPServerResponseLogger.Subscribe(LogTarget);

            //if (_ServerResponseLoggers2.TryGetValue(LogEventName, out ServerResponseLogger2?  _HTTPServerResponseLogger2))
            //    found |= _HTTPServerResponseLogger2.Subscribe(LogTarget);

            return found;

        }

        #endregion

        #region (protected) InternalUndebug(LogEventName, LogTarget)

        protected override Boolean InternalUndebug(String      LogEventName,
                                                   LogTargets  LogTarget)
        {

            var found = false;

            if (_ServerRequestLoggers.  TryGetValue(LogEventName, out ServerRequestLogger?    _HTTPServerRequestLogger))
                found |= _HTTPServerRequestLogger. Unsubscribe(LogTarget);

            //if (_ServerRequestLoggers2. TryGetValue(LogEventName, out ServerRequestLogger2?   _HTTPServerRequestLogger2))
            //    found |= _HTTPServerRequestLogger2.Unsubscribe(LogTarget);

            if (_ServerResponseLoggers. TryGetValue(LogEventName, out ServerResponseLogger?   _HTTPServerResponseLogger))
                found |= _HTTPServerResponseLogger.Unsubscribe(LogTarget);

            //if (_ServerResponseLoggers2.TryGetValue(LogEventName, out ServerResponseLogger2?  _HTTPServerResponseLogger2))
            //    found |= _HTTPServerResponseLogger2.Unsubscribe(LogTarget);

            return found;

        }

        #endregion


    }

}
