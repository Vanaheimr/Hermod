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

using System.Text;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    public delegate Task   ModbusRequestLoggerDelegate (String LoggingPath, String Context, String LogEventName, ModbusTCPRequest Request);
    public delegate Task   ModbusResponseLoggerDelegate(String LoggingPath, String Context, String LogEventName, ModbusTCPRequest Request, ModbusTCPResponse Response);


    /// <summary>
    /// The delegate for logging the Modbus/TCP request send by a Modbus/TCP client.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the outgoing Modbus/TCP request.</param>
    /// <param name="ModbusClient">The Modbus/TCP client sending the Modbus/TCP request.</param>
    /// <param name="Request">The outgoing Modbus/TCP request.</param>
    public delegate Task ClientRequestLogHandler(DateTime         Timestamp,
                                                 ModbusTCPClient  ModbusClient,
                                                 ModbusTCPRequest    Request);


    /// <summary>
    /// The delegate for logging the HTTP response received by a Modbus/TCP client.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming HTTP response.</param>
    /// <param name="ModbusClient">The Modbus/TCP client receiving the Modbus/TCP request.</param>
    /// <param name="Request">The outgoing Modbus/TCP request.</param>
    /// <param name="Response">The incoming HTTP response.</param>
    public delegate Task ClientResponseLogHandler(DateTime         Timestamp,
                                                  ModbusTCPClient  ModbusClient,
                                                  ModbusTCPRequest    Request,
                                                  ModbusTCPResponse   Response);


    /// <summary>
    /// A Modbus/TCP client logger.
    /// </summary>
    public class ModbusTCPClientLogger
    {

        #region (class) ModbusClientRequestLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class ModbusClientRequestLogger
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
            /// <param name="LoggingPath">The logging path.</param>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public ModbusClientRequestLogger(String                           LoggingPath,
                                             String                           Context,
                                             String                           LogEventName,
                                             Action<ClientRequestLogHandler>  SubscribeToEventDelegate,
                                             Action<ClientRequestLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate     is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

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


            #region RegisterLogTarget(LogTarget, ModbusRequestDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="ModbusRequestDelegate">A delegate to call.</param>
            /// <returns>A Modbus/TCP request logger.</returns>
            public ModbusClientRequestLogger RegisterLogTarget(LogTargets                   LogTarget,
                                                               ModbusRequestLoggerDelegate  ModbusRequestDelegate)
            {

                if (subscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                subscriptionDelegates.Add(LogTarget,
                                          (timestamp, HTTPAPI, Request) => ModbusRequestDelegate(LoggingPath, Context, LogEventName, Request));

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
                                                      out var clientRequestLogHandler))
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
                                                      out var clientRequestLogHandler))
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

        #region (class) ModbusClientResponseLogger

        /// <summary>
        /// A wrapper class to manage HTTP API event subscriptions
        /// for logging purposes.
        /// </summary>
        public class ModbusClientResponseLogger
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
            /// <param name="LoggingPath">The logging path.</param>
            /// <param name="Context">The context of the event.</param>
            /// <param name="LogEventName">The name of the event.</param>
            /// <param name="SubscribeToEventDelegate">A delegate for subscribing to the linked event.</param>
            /// <param name="UnsubscribeFromEventDelegate">A delegate for subscribing from the linked event.</param>
            public ModbusClientResponseLogger(String                            LoggingPath,
                                              String                            Context,
                                              String                            LogEventName,
                                              Action<ClientResponseLogHandler>  SubscribeToEventDelegate,
                                              Action<ClientResponseLogHandler>  UnsubscribeFromEventDelegate)
            {

                #region Initial checks

                if (LogEventName.IsNullOrEmpty())
                    throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

                if (SubscribeToEventDelegate     is null)
                    throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked  HTTP API event must not be null!");

                if (UnsubscribeFromEventDelegate is null)
                    throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

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


            #region RegisterLogTarget(LogTarget, ModbusResponseDelegate)

            /// <summary>
            /// Register the given log target and delegate combination.
            /// </summary>
            /// <param name="LogTarget">A log target.</param>
            /// <param name="ModbusResponseDelegate">A delegate to call.</param>
            /// <returns>A HTTP response logger.</returns>
            public ModbusClientResponseLogger RegisterLogTarget(LogTargets                    LogTarget,
                                                                ModbusResponseLoggerDelegate  ModbusResponseDelegate)
            {

                if (subscriptionDelegates.ContainsKey(LogTarget))
                    throw new Exception("Duplicate log target!");

                subscriptionDelegates.Add(LogTarget,
                                          (timestamp, HTTPAPI, Request, Response) => ModbusResponseDelegate(LoggingPath, Context, LogEventName, Request, Response));

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
                                                      out var clientResponseLogHandler))
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
                                                      out var clientResponseLogHandler))
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

        private static readonly Object         LockObject                   = new ();
        private static          SemaphoreSlim  LogModbusRequest_toDisc_Lock   = new (1,1);
        private static          SemaphoreSlim  LogModbusResponse_toDisc_Lock  = new (1,1);

        /// <summary>
        /// The maximum number of retries to write to a logfile.
        /// </summary>
        public  static readonly Byte           MaxRetries                   = 5;

        /// <summary>
        /// Maximum waiting time to enter a lock around a logfile.
        /// </summary>
        public  static readonly TimeSpan       MaxWaitingForALock           = TimeSpan.FromSeconds(15);

        /// <summary>
        /// A delegate for the default ToDisc logger returning a
        /// valid logfile name based on the given log event name.
        /// </summary>
        public         LogfileCreatorDelegate  LogfileCreator               { get; }

        protected readonly ConcurrentDictionary<String, HashSet<String>> _GroupTags;


        private readonly ConcurrentDictionary<String, ModbusClientRequestLogger>   _ModbusClientRequestLoggers;
        private readonly ConcurrentDictionary<String, ModbusClientResponseLogger>  _ModbusClientResponseLoggers;

        #endregion

        #region Properties

        /// <summary>
        /// The logging path.
        /// </summary>
        public String           LoggingPath       { get; }

        /// <summary>
        /// The context of this HTTP logger.
        /// </summary>
        public String           Context           { get; }

        /// <summary>
        /// The Modbus/TCP client of this logger.
        /// </summary>
        public ModbusTCPClient  ModbusClient      { get; }

        /// <summary>
        /// Whether to disable Modbus/TCP client logging.
        /// </summary>
        public Boolean          DisableLogging    { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new Modbus/TCP client logger using the given logging delegates.
        /// </summary>
        /// <param name="ModbusClient">A Modbus/TCP client.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="Context">A context of this API.</param>
        /// 
        /// <param name="LogModbusRequest_toConsole">A delegate to log incoming Modbus/TCP requests to console.</param>
        /// <param name="LogModbusResponse_toConsole">A delegate to log Modbus/TCP requests/responses to console.</param>
        /// <param name="LogModbusRequest_toDisc">A delegate to log incoming Modbus/TCP requests to disc.</param>
        /// <param name="LogModbusResponse_toDisc">A delegate to log Modbus/TCP requests/responses to disc.</param>
        /// 
        /// <param name="LogModbusRequest_toNetwork">A delegate to log incoming Modbus/TCP requests to a network target.</param>
        /// <param name="LogModbusResponse_toNetwork">A delegate to log Modbus/TCP requests/responses to a network target.</param>
        /// <param name="LogModbusRequest_toHTTPSSE">A delegate to log incoming Modbus/TCP requests to a HTTP server sent events source.</param>
        /// <param name="LogModbusResponse_toHTTPSSE">A delegate to log Modbus/TCP requests/responses to a HTTP server sent events source.</param>
        /// 
        /// <param name="LogHTTPError_toConsole">A delegate to log HTTP errors to console.</param>
        /// <param name="LogHTTPError_toDisc">A delegate to log HTTP errors to disc.</param>
        /// <param name="LogHTTPError_toNetwork">A delegate to log HTTP errors to a network target.</param>
        /// <param name="LogHTTPError_toHTTPSSE">A delegate to log HTTP errors to a HTTP server sent events source.</param>
        /// 
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        public ModbusTCPClientLogger(ModbusTCPClient                ModbusClient,
                                     String?                        LoggingPath,
                                     String                         Context,

                                     ModbusRequestLoggerDelegate?   LogModbusRequest_toConsole    = null,
                                     ModbusResponseLoggerDelegate?  LogModbusResponse_toConsole   = null,
                                     ModbusRequestLoggerDelegate?   LogModbusRequest_toDisc       = null,
                                     ModbusResponseLoggerDelegate?  LogModbusResponse_toDisc      = null,

                                     ModbusRequestLoggerDelegate?   LogModbusRequest_toNetwork    = null,
                                     ModbusResponseLoggerDelegate?  LogModbusResponse_toNetwork   = null,
                                     ModbusRequestLoggerDelegate?   LogModbusRequest_toHTTPSSE    = null,
                                     ModbusResponseLoggerDelegate?  LogModbusResponse_toHTTPSSE   = null,

                                     ModbusResponseLoggerDelegate?  LogHTTPError_toConsole        = null,
                                     ModbusResponseLoggerDelegate?  LogHTTPError_toDisc           = null,
                                     ModbusResponseLoggerDelegate?  LogHTTPError_toNetwork        = null,
                                     ModbusResponseLoggerDelegate?  LogHTTPError_toHTTPSSE        = null,

                                     LogfileCreatorDelegate?        LogfileCreator                = null)

            //: base(LoggingPath,
            //       Context,

            //       LogModbusRequest_toConsole,
            //       LogModbusResponse_toConsole,
            //       LogModbusRequest_toDisc,
            //       LogModbusResponse_toDisc,

            //       LogModbusRequest_toNetwork,
            //       LogModbusResponse_toNetwork,
            //       LogModbusRequest_toHTTPSSE,
            //       LogModbusResponse_toHTTPSSE,

            //       LogHTTPError_toConsole,
            //       LogHTTPError_toDisc,
            //       LogHTTPError_toNetwork,
            //       LogHTTPError_toHTTPSSE,

            //       LogfileCreator)

        {

            this.ModbusClient                = ModbusClient ?? throw new ArgumentNullException(nameof(ModbusClient), "The given Modbus/TCP client must not be null!");

            #region Init data structures

            this.LoggingPath  = LoggingPath ?? "";
            this.Context      = Context     ?? "";
            this._GroupTags   = new ConcurrentDictionary<String, HashSet<String>>();

            #endregion

            #region Set default delegates

            if (LogModbusRequest_toConsole  is null)
                LogModbusRequest_toConsole   = Default_LogModbusRequest_toConsole;

            if (LogModbusRequest_toDisc     is null)
                LogModbusRequest_toDisc      = Default_LogModbusRequest_toDisc;

            if (LogModbusResponse_toConsole is null)
                LogModbusResponse_toConsole  = Default_LogModbusResponse_toConsole;

            if (LogModbusResponse_toDisc    is null)
                LogModbusResponse_toDisc     = Default_LogModbusResponse_toDisc;


            if (LogModbusRequest_toDisc  is not null ||
                LogModbusResponse_toDisc is not null ||
                LogHTTPError_toDisc    is not null)
            {
                if (this.LoggingPath.IsNotNullOrEmpty())
                    Directory.CreateDirectory(this.LoggingPath);
            }

            this.LogfileCreator  = LogfileCreator ?? ((loggingPath, context, logfileName) => String.Concat(loggingPath,
                                                                                                           context != null ? context + "_" : "",
                                                                                                           logfileName, "_",
                                                                                                           DateTime.UtcNow.Year, "-",
                                                                                                           DateTime.UtcNow.Month.ToString("D2"),
                                                                                                           ".log"));

            #endregion


            this._ModbusClientRequestLoggers   = new ConcurrentDictionary<String, ModbusClientRequestLogger>();
            this._ModbusClientResponseLoggers  = new ConcurrentDictionary<String, ModbusClientResponseLogger>();


            //ToDo: Evaluate Logging targets!

          //  HTTPAPI.ErrorLog += async (Timestamp,
          //                             HTTPServer,
          //                             ModbusRequest,
          //                             ModbusResponse,
          //                             Error,
          //                             LastException) => {
          //
          //              DebugX.Log(Timestamp + " - " +
          //                         ModbusRequest.RemoteSocket.IPAddress + ":" +
          //                         ModbusRequest.RemoteSocket.Port + " " +
          //                         ModbusRequest.HTTPMethod + " " +
          //                         ModbusRequest.URI + " " +
          //                         ModbusRequest.ProtocolVersion + " => " +
          //                         ModbusResponse.HTTPStatusCode + " - " +
          //                         Error);

          //         };

        }

        #endregion



        // Default logging delegates

        #region Default_LogModbusRequest_toConsole (Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming Modbus/TCP requests to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The Modbus/TCP request to log.</param>
        public Task Default_LogModbusRequest_toConsole(String         LoggingPath,
                                                       String         Context,
                                                       String         LogEventName,
                                                       ModbusTCPRequest  Request)
        {

            lock (LockObject)
            {

                var PreviousColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("[" + Request.Timestamp.ToLocalTime() + " T:" + Environment.CurrentManagedThreadId.ToString() + "] ");

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(Context + "/");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(LogEventName);

                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine(Request.HTTPSource.Socket == Request.LocalSocket
                //                      ? String.Concat(Request.LocalSocket, " -> ", Request.RemoteSocket)
                //                      : String.Concat(Request.HTTPSource,  " -> ", Request.LocalSocket));

                Console.ForegroundColor = PreviousColor;

            }

            return Task.CompletedTask;

        }

        #endregion

        #region Default_LogModbusResponse_toConsole(Context, LogEventName, Request, Response)

        /// <summary>
        /// A default delegate for logging Modbus/TCP requests/-responses to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The Modbus/TCP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public Task Default_LogModbusResponse_toConsole(String          LoggingPath,
                                                        String          Context,
                                                        String          LogEventName,
                                                        ModbusTCPRequest   Request,
                                                        ModbusTCPResponse  Response)
        {

            lock (LockObject)
            {

                var PreviousColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("[" + Request.Timestamp.ToLocalTime() + " T:" + Environment.CurrentManagedThreadId.ToString() + "] ");

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(Context + "/");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(LogEventName);

                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.Write(String.Concat(" from ", Request.HTTPSource, " => "));

                //if (Response.HTTPStatusCode == HTTPStatusCode.OK ||
                //    Response.HTTPStatusCode == HTTPStatusCode.Created)
                //    Console.ForegroundColor = ConsoleColor.Green;

                //else if (Response.HTTPStatusCode == HTTPStatusCode.NoContent)
                //    Console.ForegroundColor = ConsoleColor.Yellow;

                //else
                //    Console.ForegroundColor = ConsoleColor.Red;

                //Console.Write(Response.HTTPStatusCode);

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(String.Concat(" in ", Math.Round((Response.Timestamp - Request.Timestamp).TotalMilliseconds), "ms"));

                Console.ForegroundColor = PreviousColor;

            }

            return Task.CompletedTask;

        }

        #endregion


        #region Default_LogModbusRequest_toDisc (Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming Modbus/TCP requests to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The Modbus/TCP request to log.</param>
        public async Task Default_LogModbusRequest_toDisc(String         LoggingPath,
                                                          String         Context,
                                                          String         LogEventName,
                                                          ModbusTCPRequest  Request)
        {

            //ToDo: Can we have a lock per logfile?
            var LockTaken = await LogModbusRequest_toDisc_Lock.WaitAsync(MaxWaitingForALock);

            try
            {

                if (LockTaken)
                {

                    var retry = 0;

                    do
                    {

                        try
                        {

                            File.AppendAllText(

                                LogfileCreator(
                                    LoggingPath,
                                    Context,
                                    LogEventName
                                ),

                                String.Concat(
                                    String.Concat(Request.LocalSocket, " -> ", Request.RemoteSocket),                    Environment.NewLine,
                                    ">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------",  Environment.NewLine,
                                    Request.Timestamp.ToIso8601(),                                                       Environment.NewLine,
                                    Request.EntirePDU.ToHexString(),                                                     Environment.NewLine,
                                    "--------------------------------------------------------------------------------",  Environment.NewLine
                                ),

                                Encoding.UTF8

                            );

                            break;

                        }
                        catch (IOException e)
                        {

                            if (e.HResult != -2147024864)
                            {
                                DebugX.LogT("File access error while logging to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "' (retry: " + retry + "): " + e.Message);
                                Thread.Sleep(100);
                            }

                            else
                            {
                                DebugX.LogT("Could not log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "': " + e.Message);
                                break;
                            }

                        }
                        catch (Exception e)
                        {
                            DebugX.LogT("Could not log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "': " + e.Message);
                            break;
                        }

                    }
                    while (retry++ < MaxRetries);

                    if (retry >= MaxRetries)
                        DebugX.LogT("Could not write to logfile '"      + LogfileCreator(LoggingPath, Context, LogEventName) + "' for "   + retry + " retries!");

                    else if (retry > 0)
                        DebugX.LogT("Successfully written to logfile '" + LogfileCreator(LoggingPath, Context, LogEventName) + "' after " + retry + " retries!");

                }

                else
                    DebugX.LogT("Could not get lock to log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "'!");

            }
            finally
            {
                if (LockTaken)
                    LogModbusRequest_toDisc_Lock.Release();
            }

        }

        #endregion

        #region Default_LogModbusResponse_toDisc(Context, LogEventName, Request, Response)

        /// <summary>
        /// A default delegate for logging Modbus/TCP requests/-responses to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The Modbus/TCP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public async Task Default_LogModbusResponse_toDisc(String          LoggingPath,
                                                           String          Context,
                                                           String          LogEventName,
                                                           ModbusTCPRequest   Request,
                                                           ModbusTCPResponse  Response)
        {

            //ToDo: Can we have a lock per logfile?
            var LockTaken = await LogModbusResponse_toDisc_Lock.WaitAsync(MaxWaitingForALock);

            try
            {

                if (LockTaken)
                {

                    var retry = 0;

                    do
                    {

                        try
                        {

                            File.AppendAllText(

                                LogfileCreator(
                                    LoggingPath,
                                    Context,
                                    LogEventName
                                ),

                                String.Concat(
                                    String.Concat(Request.LocalSocket, " -> ", Request.RemoteSocket),                    Environment.NewLine,
                                    ">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------",  Environment.NewLine,
                                    Request.Timestamp.ToIso8601(),                                                       Environment.NewLine,
                                    Request.EntirePDU,                                                                   Environment.NewLine,
                                    "<<<<<<--Response----<<<<<<------<<<<<<------<<<<<<------<<<<<<------<<<<<<------",  Environment.NewLine,
                                    Response.Timestamp.ToIso8601(),
                                        " -> ",
                                        (Response.Timestamp - Request.Timestamp).TotalMilliseconds, "ms runtime",        Environment.NewLine,
                                    Response.EntirePDU,                                                                  Environment.NewLine,
                                    "--------------------------------------------------------------------------------",  Environment.NewLine
                                ),

                                Encoding.UTF8

                            );

                            break;

                        }
                        catch (IOException e)
                        {

                            if (e.HResult != -2147024864)
                            {
                                DebugX.LogT("File access error while logging to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "' (retry: " + retry + "): " + e.Message);
                                Thread.Sleep(100);
                            }

                            else
                            {
                                DebugX.LogT("Could not log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "': " + e.Message);
                                break;
                            }

                        }
                        catch (Exception e)
                        {
                            DebugX.LogT("Could not log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "': " + e.Message);
                            break;
                        }

                    }
                    while (retry++ < MaxRetries);

                    if (retry >= MaxRetries)
                        DebugX.LogT("Could not write to logfile '"      + LogfileCreator(LoggingPath, Context, LogEventName) + "' for "   + retry + " retries!");

                    else if (retry > 0)
                        DebugX.LogT("Successfully written to logfile '" + LogfileCreator(LoggingPath, Context, LogEventName) + "' after " + retry + " retries!");

                }

                else
                    DebugX.LogT("Could not get lock to log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "'!");

            }
            finally
            {
                if (LockTaken)
                    LogModbusResponse_toDisc_Lock.Release();
            }

        }

        #endregion


        #region Debug  (LogEventOrGroupName, LogTarget)

        /// <summary>
        /// Start debugging the given log event.
        /// </summary>
        /// <param name="LogEventOrGroupName">A log event of group name.</param>
        /// <param name="LogTarget">The log target.</param>
        public Boolean Debug(String      LogEventOrGroupName,
                             LogTargets  LogTarget)
        {

            if (_GroupTags.TryGetValue(LogEventOrGroupName,
                                       out var _LogEventNames))

                return _LogEventNames.
                           Select(logname => InternalDebug(logname, LogTarget)).
                           All   (result  => result == true);


            return InternalDebug(LogEventOrGroupName, LogTarget);

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

            if (_GroupTags.TryGetValue(LogEventOrGroupName,
                                       out var _LogEventNames))

                return _LogEventNames.
                           Select(logname => InternalUndebug(logname, LogTarget)).
                           All   (result  => result == true);


            return InternalUndebug(LogEventOrGroupName, LogTarget);

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
        protected ModbusClientRequestLogger RegisterEvent(String                           LogEventName,
                                                        Action<ClientRequestLogHandler>  SubscribeToEventDelegate,
                                                        Action<ClientRequestLogHandler>  UnsubscribeFromEventDelegate,
                                                        params String[]                  GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate is null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate is null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            if (!_ModbusClientRequestLoggers. TryGetValue(LogEventName, out ModbusClientRequestLogger? httpClientRequestLogger) &&
                !_ModbusClientResponseLoggers.ContainsKey(LogEventName))
            {

                httpClientRequestLogger = new ModbusClientRequestLogger(LoggingPath, Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _ModbusClientRequestLoggers.TryAdd(LogEventName, httpClientRequestLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (_GroupTags.TryGetValue(GroupTag, out HashSet<String>? logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        _GroupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

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
        protected ModbusClientResponseLogger RegisterEvent(String                            LogEventName,
                                                         Action<ClientResponseLogHandler>  SubscribeToEventDelegate,
                                                         Action<ClientResponseLogHandler>  UnsubscribeFromEventDelegate,
                                                         params String[]                   GroupTags)
        {

            #region Initial checks

            if (LogEventName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(LogEventName),                  "The given log event name must not be null or empty!");

            if (SubscribeToEventDelegate is null)
                throw new ArgumentNullException(nameof(SubscribeToEventDelegate),      "The given delegate for subscribing to the linked HTTP API event must not be null!");

            if (UnsubscribeFromEventDelegate is null)
                throw new ArgumentNullException(nameof(UnsubscribeFromEventDelegate),  "The given delegate for unsubscribing from the linked HTTP API event must not be null!");

            #endregion

            if (!_ModbusClientResponseLoggers.TryGetValue(LogEventName, out ModbusClientResponseLogger? httpClientResponseLogger) &&
                !_ModbusClientRequestLoggers. ContainsKey(LogEventName))
            {

                httpClientResponseLogger = new ModbusClientResponseLogger(LoggingPath, Context, LogEventName, SubscribeToEventDelegate, UnsubscribeFromEventDelegate);
                _ModbusClientResponseLoggers.TryAdd(LogEventName, httpClientResponseLogger);

                #region Register group tag mapping

                foreach (var GroupTag in GroupTags.Distinct())
                {

                    if (_GroupTags.TryGetValue(GroupTag, out HashSet<String>? logEventNames))
                        logEventNames.Add(LogEventName);

                    else
                        _GroupTags.TryAdd(GroupTag, new HashSet<String>(new String[] { LogEventName }));

                }

                #endregion

                return httpClientResponseLogger;

            }

            throw new Exception("Duplicate log event name!");

        }

        #endregion


        #region (protected) InternalDebug  (LogEventName, LogTarget)

        protected Boolean InternalDebug(String      LogEventName,
                                        LogTargets  LogTarget)
        {

            var found = false;

            // HTTP Client
            if (_ModbusClientRequestLoggers. TryGetValue(LogEventName, out ModbusClientRequestLogger?  httpClientRequestLogger))
                found |= httpClientRequestLogger. Subscribe(LogTarget);

            if (_ModbusClientResponseLoggers.TryGetValue(LogEventName, out ModbusClientResponseLogger? httpClientResponseLogger))
                found |= httpClientResponseLogger.Subscribe(LogTarget);

            return found;

        }

        #endregion

        #region (protected) InternalUndebug(LogEventName, LogTarget)

        protected Boolean InternalUndebug(String      LogEventName,
                                          LogTargets  LogTarget)
        {

            var found = false;

            if (_ModbusClientRequestLoggers. TryGetValue(LogEventName, out ModbusClientRequestLogger?  httpClientRequestLogger))
                found |= httpClientRequestLogger. Unsubscribe(LogTarget);

            if (_ModbusClientResponseLoggers.TryGetValue(LogEventName, out ModbusClientResponseLogger? httpClientResponseLogger))
                found |= httpClientResponseLogger.Unsubscribe(LogTarget);

            return found;

        }

        #endregion


    }

}
