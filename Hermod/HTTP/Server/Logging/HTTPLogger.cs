/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using System.Diagnostics;
using System.Threading;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate String LogfileCreatorDelegate    (String Context, String LogfileName);
    public delegate Task   HTTPRequestLoggerDelegate (String Context, String LogEventName, HTTPRequest HTTPRequest);
    public delegate Task   HTTPResponseLoggerDelegate(String Context, String LogEventName, HTTPRequest HTTPRequest, HTTPResponse HTTPResponse);


    /// <summary>
    /// A HTTP API logger.
    /// </summary>
    public class HTTPLogger
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

                this.Context                       = Context != null ? Context : "";
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
                    throw new Exception("Duplicate log target!");

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request) => HTTPRequestDelegate(Context, LogEventName, Request));

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

                this.Context                       = Context != null ? Context : "";
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
                    throw new Exception("Duplicate log target!");

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request, Response) => HTTPResponseDelegate(Context, LogEventName, Request, Response));

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
                    UnsubscribeFromEventDelegate(_AccessLogHandler);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion

        #region (class) HTTPClientRequestLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class HTTPClientRequestLogger
        {

            #region Data

            private readonly Dictionary<LogTargets, ClientRequestLogHandler>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                              _SubscriptionStatus;

            #endregion

            #region Properties

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                           Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                           LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscriped to.
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
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPClientRequestLogger(String                           Context,
                                           String                           LogEventName,
                                           Action<ClientRequestLogHandler>  SubscribeToEventDelegate,
                                           Action<ClientRequestLogHandler>  UnsubscribeFromEventDelegate)
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
                this._SubscriptionDelegates        = new Dictionary<LogTargets, ClientRequestLogHandler>();
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
            public HTTPClientRequestLogger RegisterLogTarget(LogTargets                 LogTarget,
                                                             HTTPRequestLoggerDelegate  HTTPRequestDelegate)
            {

                #region Initial checks

                if (HTTPRequestDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPRequestDelegate),  "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request) => HTTPRequestDelegate(Context, LogEventName, Request));

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

                ClientRequestLogHandler _ClientRequestLogHandler = null;

                if (_SubscriptionDelegates.TryGetValue(LogTarget, out _ClientRequestLogHandler))
                {
                    SubscribeToEventDelegate(_ClientRequestLogHandler);
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

                ClientRequestLogHandler _ClientRequestLogHandler = null;

                if (_SubscriptionDelegates.TryGetValue(LogTarget, out _ClientRequestLogHandler))
                {
                    UnsubscribeFromEventDelegate(_ClientRequestLogHandler);
                    _SubscriptionStatus.Remove(LogTarget);
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

            private readonly Dictionary<LogTargets, ClientResponseLogHandler>  _SubscriptionDelegates;
            private readonly HashSet<LogTargets>                               _SubscriptionStatus;

            #endregion

            #region Properties

            /// <summary>
            /// The context of the event to be logged.
            /// </summary>
            public String                            Context                         { get; }

            /// <summary>
            /// The name of the event to be logged.
            /// </summary>
            public String                            LogEventName                    { get; }

            /// <summary>
            /// A delegate called whenever the event is subscriped to.
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
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public HTTPClientResponseLogger(String                            Context,
                                            String                            LogEventName,
                                            Action<ClientResponseLogHandler>  SubscribeToEventDelegate,
                                            Action<ClientResponseLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate == null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked  HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate == null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

                #endregion

                this.Context                       = Context != null ? Context : "";
                this.LogEventName                  = LogEventName;
                this.SubscribeToEventDelegate      = SubscribeToEventDelegate;
                this.UnsubscribeFromEventDelegate  = UnsubscribeFromEventDelegate;
                this._SubscriptionDelegates        = new Dictionary<LogTargets, ClientResponseLogHandler>();
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
            public HTTPClientResponseLogger RegisterLogTarget(LogTargets                  LogTarget,
                                                              HTTPResponseLoggerDelegate  HTTPResponseDelegate)
            {

                #region Initial checks

                if (HTTPResponseDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPResponseDelegate), "The given delegate must not be null!");

                #endregion

                if (_SubscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                _SubscriptionDelegates.Add(LogTarget,
                                           (Timestamp, HTTPAPI, Request, Response) => HTTPResponseDelegate(Context, LogEventName, Request, Response));

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

                ClientResponseLogHandler _ClientResponseLogHandler = null;

                if (_SubscriptionDelegates.TryGetValue(LogTarget, out _ClientResponseLogHandler))
                {
                    SubscribeToEventDelegate(_ClientResponseLogHandler);
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

                ClientResponseLogHandler _ClientResponseLogHandler = null;

                if (_SubscriptionDelegates.TryGetValue(LogTarget, out _ClientResponseLogHandler))
                {
                    UnsubscribeFromEventDelegate(_ClientResponseLogHandler);
                    _SubscriptionStatus.Remove(LogTarget);
                    return true;
                }

                return false;

            }

            #endregion

        }

        #endregion


        #region Data

        private static readonly Object         LockObject                   = new Object();
        private static          SemaphoreSlim  LogHTTPRequest_toDisc_Lock   = new SemaphoreSlim(1,1);
        private static          SemaphoreSlim  LogHTTPResponse_toDisc_Lock  = new SemaphoreSlim(1,1);

        #endregion


        #region Default logging delegates

        #region Default_LogHTTPRequest_toConsole (Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming HTTP requests to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        public async Task Default_LogHTTPRequest_toConsole(String       Context,
                                                           String       LogEventName,
                                                           HTTPRequest  Request)
        {

            lock (LockObject)
            {

                var PreviousColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("[" + Request.Timestamp.ToLocalTime() + ", Thread " + Thread.CurrentThread.ManagedThreadId.ToString() + "] ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(Context + "/");
                Console.ForegroundColor = ConsoleColor.Yellow;

                if (Request.RemoteSocket != null)
                {
                    Console.Write(LogEventName);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine(" from " + Request.RemoteSocket);
                }

                else
                    Console.WriteLine(LogEventName);

                Console.ForegroundColor = PreviousColor;

            }

        }

        #endregion

        #region Default_LogHTTPResponse_toConsole(Context, LogEventName, Request, Response)

        /// <summary>
        /// A default delegate for logging HTTP requests/-responses to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public async Task Default_LogHTTPResponse_toConsole(String        Context,
                                                            String        LogEventName,
                                                            HTTPRequest   Request,
                                                            HTTPResponse  Response)
        {

            lock (LockObject)
            {

                var PreviousColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("[" + Request.Timestamp.ToLocalTime() + ", Thread " + Thread.CurrentThread.ManagedThreadId.ToString() + "] ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(Context + "/");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(LogEventName);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write(String.Concat(" from ", (Request.RemoteSocket != null ? Request.RemoteSocket.ToString() : "<local>"), " => "));

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

                Console.ForegroundColor = PreviousColor;

            }

        }

        #endregion

        #region LogfileCreator

        /// <summary>
        /// A delegate for the default ToDisc logger returning a
        /// valid logfile name based on the given log event name.
        /// </summary>
        public LogfileCreatorDelegate LogfileCreator { get; }

        #endregion

        #region (private) OpenFileWithRetry(WorkToDo, Timeout = null)

        private async Task OpenFileWithRetry(Func<Task>  WorkToDo,
                                             TimeSpan?   Timeout = null)
        {

            if (Timeout == null)
                Timeout = TimeSpan.FromSeconds(10);

            var _Stopwatch = Stopwatch.StartNew();

            while (_Stopwatch.Elapsed < Timeout)
            {
                try
                {

                    await WorkToDo().ConfigureAwait(false);

                    return;

                }
                catch (IOException e)
                {
                    // access error
                    if (e.HResult != -2147024864)
                        throw;
                }
            }

            throw new Exception("Failed perform action within allotted time.");

        }

        #endregion

        #region Default_LogHTTPRequest_toDisc (Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming HTTP requests to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        public async Task Default_LogHTTPRequest_toDisc(String       Context,
                                                        String       LogEventName,
                                                        HTTPRequest  Request)
        {

            var LockTaken = await LogHTTPRequest_toDisc_Lock.WaitAsync(5000);

            try
            {

                if (LockTaken)
                {

                    try
                    {

                        //await OpenFileWithRetry(
                        //          async () =>
                        //          {
                                      using (var logfile = File.AppendText(LogfileCreator(Context, LogEventName)))
                                      {

                                          await logfile.WriteLineAsync(
                                                    String.Concat(Request.RemoteSocket != null && Request.LocalSocket != null
                                                                      ? String.Concat(Request.RemoteSocket, " -> ", Request.LocalSocket)
                                                                      : "", Environment.NewLine,
                                                                  ">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------", Environment.NewLine,
                                                                  Request.Timestamp.ToIso8601(), Environment.NewLine,
                                                                  Request.EntirePDU, Environment.NewLine,
                                                                  "--------------------------------------------------------------------------------"));

                                          await logfile.FlushAsync();

                                      }
                                  //});
                    }
                    catch (Exception e)
                    {
                        DebugX.LogT("Could not log to '" + LogfileCreator(Context, LogEventName) + "': " + e.Message);
                    }

                }

                else
                    DebugX.LogT("Could not get lock to log to '" + LogfileCreator(Context, LogEventName) + "'!");

            }
            finally
            {
                LogHTTPRequest_toDisc_Lock.Release();
            }

        }

        #endregion

        #region Default_LogHTTPResponse_toDisc(Context, LogEventName, Request, Response)

        /// <summary>
        /// A default delegate for logging HTTP requests/-responses to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public async Task Default_LogHTTPResponse_toDisc(String        Context,
                                                         String        LogEventName,
                                                         HTTPRequest   Request,
                                                         HTTPResponse  Response)
        {

            var LockTaken = await LogHTTPResponse_toDisc_Lock.WaitAsync(5000);

            try
            {

                if (LockTaken)
                {

                    try
                    {

            //            try
            //{

            //    await OpenFileWithRetry(
            //              async () => {
                              using (var logfile = File.AppendText(LogfileCreator(Context, LogEventName)))
                              {

                                  await logfile.WriteLineAsync(
                                            String.Concat(Request.RemoteSocket != null && Request.LocalSocket != null
                                                              ? String.Concat(Request.RemoteSocket, " -> ", Request.LocalSocket)
                                                              : "", Environment.NewLine,
                                                          ">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------",  Environment.NewLine,
                                                          Request.Timestamp.ToIso8601(),                                                       Environment.NewLine,
                                                          Request.EntirePDU,                                                                   Environment.NewLine,
                                                          "<<<<<<--Response----<<<<<<------<<<<<<------<<<<<<------<<<<<<------<<<<<<------",  Environment.NewLine,
                                                          Response.Timestamp.ToIso8601(),
                                                              " -> ",
                                                              (Request.Timestamp - Response.Timestamp).TotalMilliseconds, "ms runtime",        Environment.NewLine,
                                                          Response.EntirePDU,                                                                  Environment.NewLine,
                                                          "--------------------------------------------------------------------------------"));

                                  await logfile.FlushAsync();

                              }
                            //              }).

                            //    ConfigureAwait(false);

                            //}
                            //catch (Exception e)
                            //{
                            //    DebugX.Log("Could not log to disc: " + e.Message);
                            //}
                            //finally
                            //{
                            //    LogHTTPResponse_toDisc_Lock.Release();
                            //}

                        }
                    catch (Exception e)
                    {
                        DebugX.LogT("Could not log to '" + LogfileCreator(Context, LogEventName) + "': " + e.Message);
                    }

                }

                else
                    DebugX.LogT("Could not get lock to log to '" + LogfileCreator(Context, LogEventName) + "'!");

            }
            finally
            {
                LogHTTPRequest_toDisc_Lock.Release();
            }

        }

        #endregion

        #endregion


        #region Data

        private readonly IHTTPServer                                             _HTTPAPI;
        private readonly ConcurrentDictionary<String, HTTPServerRequestLogger>   _HTTPRequestLoggers;
        private readonly ConcurrentDictionary<String, HTTPServerResponseLogger>  _HTTPResponseLoggers;
        private readonly ConcurrentDictionary<String, HTTPClientRequestLogger>   _HTTPClientRequestLoggers;
        private readonly ConcurrentDictionary<String, HTTPClientResponseLogger>  _HTTPClientResponseLoggers;
        private readonly ConcurrentDictionary<String, HashSet<String>>           _GroupTags;

        #endregion

        #region Properties

        /// <summary>
        /// The context of this HTTP logger.
        /// </summary>
        public String  Context   { get; }

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
        public HTTPLogger(IHTTPServer  HTTPAPI,
                          String       Context = "")

            : this(HTTPAPI,
                   Context,
                   null,
                   null,
                   null,
                   null)

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
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        public HTTPLogger(IHTTPServer                 HTTPAPI,
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

            : this(Context,

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

            #region Initial checks

            if (HTTPAPI == null)
                throw new ArgumentNullException(nameof(HTTPAPI), "The given HTTP API must not be null!");

            this._HTTPAPI              = HTTPAPI;

            #endregion


            //ToDo: Evaluate Logging targets!

            HTTPAPI.ErrorLog += (Timestamp,
                                 HTTPServer,
                                 HTTPRequest,
                                 HTTPResponse,
                                 Error,
                                 LastException) => {

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

        #region HTTPLogger(Context,          ... Logging delegates ...)

        /// <summary>
        /// Create a new HTTP API logger using the given logging delegates.
        /// </summary>
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
        public HTTPLogger(String                      Context,

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

        {

            #region Init data structures

            this.Context                     = Context ?? "";

            this._HTTPRequestLoggers         = new ConcurrentDictionary<String, HTTPServerRequestLogger>();
            this._HTTPResponseLoggers        = new ConcurrentDictionary<String, HTTPServerResponseLogger>();
            this._HTTPClientRequestLoggers   = new ConcurrentDictionary<String, HTTPClientRequestLogger>();
            this._HTTPClientResponseLoggers  = new ConcurrentDictionary<String, HTTPClientResponseLogger>();
            this._GroupTags                  = new ConcurrentDictionary<String, HashSet<String>>();

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

            this.LogfileCreator = LogfileCreator != null
                                      ? LogfileCreator
                                      : (context, logfilename) => String.Concat(context != null ? context + "_" : "",
                                                                                logfilename, "_",
                                                                                DateTime.Now.ToUniversalTime().Year, "-",
                                                                                DateTime.Now.ToUniversalTime().Month.ToString("D2"),
                                                                                ".log");

            #endregion

        }

        #endregion

        #endregion


        // HTTP Server

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

            HTTPServerRequestLogger _HTTPRequestLogger = null;

            if (!_HTTPRequestLoggers. TryGetValue(LogEventName, out _HTTPRequestLogger) &&
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

            HTTPServerResponseLogger _HTTPResponseLogger = null;

            if (!_HTTPResponseLoggers.TryGetValue(LogEventName, out _HTTPResponseLogger) &&
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

        // HTTP Client

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

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate == null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate == null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            HTTPClientRequestLogger _HTTPClientRequestLogger = null;

            if (!_HTTPClientRequestLoggers. TryGetValue(LogEventName, out _HTTPClientRequestLogger) &&
                !_HTTPClientResponseLoggers.ContainsKey(LogEventName))
            {

                _HTTPClientRequestLogger = new HTTPClientRequestLogger(Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _HTTPClientRequestLoggers.TryAdd(LogEventName, _HTTPClientRequestLogger);

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

                return _HTTPClientRequestLogger;

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

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                 "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate == null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),     "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate == null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate), "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            HTTPClientResponseLogger _HTTPClientResponseLogger = null;

            if (!_HTTPClientResponseLoggers.TryGetValue(LogEventName, out _HTTPClientResponseLogger) &&
                !_HTTPClientRequestLoggers. ContainsKey(LogEventName))
            {

                _HTTPClientResponseLogger = new HTTPClientResponseLogger(Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _HTTPClientResponseLoggers.TryAdd(LogEventName, _HTTPClientResponseLogger);

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

                return _HTTPClientResponseLogger;

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

            HTTPClientRequestLogger  _HTTPClientRequestLogger   = null;
            HTTPClientResponseLogger _HTTPClientResponseLogger  = null;
            HTTPServerRequestLogger  _HTTPServerRequestLogger   = null;
            HTTPServerResponseLogger _HTTPServerResponseLogger  = null;
            Boolean                  _Found                     = false;

            // HTTP Client
            if (_HTTPClientRequestLoggers.TryGetValue(LogEventName, out _HTTPClientRequestLogger))
                _Found |= _HTTPClientRequestLogger. Subscribe(LogTarget);

            if (_HTTPClientResponseLoggers.TryGetValue(LogEventName, out _HTTPClientResponseLogger))
                _Found |= _HTTPClientResponseLogger.Subscribe(LogTarget);

            // HTTP Server
            if (_HTTPRequestLoggers.TryGetValue(LogEventName, out _HTTPServerRequestLogger))
                _Found |= _HTTPServerRequestLogger. Subscribe(LogTarget);

            if (_HTTPResponseLoggers.TryGetValue(LogEventName, out _HTTPServerResponseLogger))
                _Found |= _HTTPServerResponseLogger.Subscribe(LogTarget);

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

            HTTPServerRequestLogger  _HTTPRequestLogger   = null;
            HTTPServerResponseLogger _HTTPResponseLogger  = null;
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
