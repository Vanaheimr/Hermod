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
using System.Threading.Channels;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Logging
{

    public delegate String LogfileCreatorDelegate(String LoggingPath, String Context, String LogfileName);

    /// <summary>
    /// An API logger.
    /// </summary>
    public abstract class ALogger
    {

        public class RequestData
        {
            public String   LoggingPath    { get; }
            public String   Context        { get; }
            public String   LogEventName   { get; }
            public String?  Request        { get; }

            public RequestData(String  loggingPath,
                               String  context,
                               String  logEventName,
                               String? request)
            {
                LoggingPath   = loggingPath;
                Context       = context;
                LogEventName  = logEventName;
                Request       = request;
            }

        }

        public class ResponseData
        {
            public String    LoggingPath     { get; }
            public String    Context         { get; }
            public String    LogEventName    { get; }
            public String?   Request         { get; }
            public String?   Response        { get; }
            public TimeSpan  Runtime         { get; }

            public ResponseData(String    loggingPath,
                                String    context,
                                String    logEventName,
                                String?   request,
                                String?   response,
                                TimeSpan  runtime)
            {
                LoggingPath   = loggingPath;
                Context       = context;
                LogEventName  = logEventName;
                Request       = request;
                Response      = response;
                Runtime       = runtime;
            }

        }


        #region Data

        private readonly Channel<RequestData>  cliRequestChannel;
        private readonly Channel<ResponseData> cliResponseChannel;

        private readonly Channel<RequestData>  discRequestChannel;
        private readonly Channel<ResponseData> discResponseChannel;

        /// <summary>
        /// The maximum number of retries to write to a logfile.
        /// </summary>
        public  static readonly Byte      MaxRetries          = 5;

        /// <summary>
        /// Maximum waiting time to enter a lock around a logfile.
        /// </summary>
        public  static readonly TimeSpan  MaxWaitingForALock  = TimeSpan.FromSeconds(15);

        protected readonly ConcurrentDictionary<String, HashSet<String>> groupTags;

        #endregion

        #region Properties

        /// <summary>
        /// The logging path.
        /// </summary>
        public String                  LoggingPath       { get; }

        /// <summary>
        /// The context of this HTTP logger.
        /// </summary>
        public String                  Context           { get; }

        /// <summary>
        /// A delegate for the default ToDisc logger returning a
        /// valid logfile name based on the given log event name.
        /// </summary>
        public LogfileCreatorDelegate  LogfileCreator    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP API logger using the given logging delegates.
        /// </summary>
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
        public ALogger(String                   LoggingPath,
                       String                   Context,

                       RequestLoggerDelegate?   LogHTTPRequest_toConsole    = null,
                       ResponseLoggerDelegate?  LogHTTPResponse_toConsole   = null,
                       RequestLoggerDelegate?   LogHTTPRequest_toDisc       = null,
                       ResponseLoggerDelegate?  LogHTTPResponse_toDisc      = null,

                       RequestLoggerDelegate?   LogHTTPRequest_toNetwork    = null,
                       ResponseLoggerDelegate?  LogHTTPResponse_toNetwork   = null,
                       RequestLoggerDelegate?   LogHTTPRequest_toHTTPSSE    = null,
                       ResponseLoggerDelegate?  LogHTTPResponse_toHTTPSSE   = null,

                       ResponseLoggerDelegate?  LogHTTPError_toConsole      = null,
                       ResponseLoggerDelegate?  LogHTTPError_toDisc         = null,
                       ResponseLoggerDelegate?  LogHTTPError_toNetwork      = null,
                       ResponseLoggerDelegate?  LogHTTPError_toHTTPSSE      = null,

                       LogfileCreatorDelegate?  LogfileCreator              = null)

        {

            #region Init data structures

            this.LoggingPath  = LoggingPath ?? "";
            this.Context      = Context     ?? "";
            this.groupTags    = new ConcurrentDictionary<String, HashSet<String>>();

            #endregion

            #region Set default delegates

            LogHTTPRequest_toConsole   ??= Default_LogRequest_toConsole;
            LogHTTPRequest_toDisc      ??= Default_LogRequest_toDisc;
            LogHTTPResponse_toConsole  ??= Default_LogResponse_toConsole;
            LogHTTPResponse_toDisc     ??= Default_LogResponse_toDisc;

            if (LogHTTPRequest_toDisc  is not null ||
                LogHTTPResponse_toDisc is not null ||
                LogHTTPError_toDisc    is not null)
            {
                if (this.LoggingPath.IsNotNullOrEmpty())
                    Directory.CreateDirectory(this.LoggingPath);
            }

            this.LogfileCreator  = LogfileCreator ?? ((loggingPath, context, logfileName) => String.Concat(loggingPath,
                                                                                                           context is not null ? context + "_" : "",
                                                                                                           logfileName, "_",
                                                                                                           DateTime.UtcNow.Year, "-",
                                                                                                           DateTime.UtcNow.Month.ToString("D2"),
                                                                                                           ".log"));

            #endregion

            cliRequestChannel    = Channel.CreateUnbounded<RequestData>();
            cliResponseChannel   = Channel.CreateUnbounded<ResponseData>();

            discRequestChannel   = Channel.CreateUnbounded<RequestData>();
            discResponseChannel  = Channel.CreateUnbounded<ResponseData>();


            // cli
            _ = Task.Factory.StartNew(async () => {

                var loggingData = await cliRequestChannel.Reader.ReadAsync();

                var PreviousColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("[" + Timestamp.Now.ToLocalTime() + " T:" + Environment.CurrentManagedThreadId.ToString() + "] ");

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(this.Context + "/");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(loggingData.LogEventName);

                Console.Write(" ");

                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine(Request.HTTPSource.Socket == Request.LocalSocket
                //                      ? String.Concat(Request.LocalSocket, " -> ", Request.RemoteSocket)
                //                      : String.Concat(Request.HTTPSource,  " -> ", Request.LocalSocket));
                Console.WriteLine(loggingData.Request);

                Console.ForegroundColor = PreviousColor;

            });

            _ = Task.Factory.StartNew(async () => {

                var loggingData = await cliResponseChannel.Reader.ReadAsync();

                var PreviousColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("[" + Timestamp.Now.ToLocalTime() + " T:" + Environment.CurrentManagedThreadId.ToString() + "] ");

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(this.Context + "/");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(loggingData.LogEventName);

                Console.Write(" ");

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
                Console.WriteLine(String.Concat(loggingData.Response, " in ", Math.Round(loggingData.Runtime.TotalMilliseconds), "ms"));

                Console.ForegroundColor = PreviousColor;

            });


            // disc
            _ = Task.Factory.StartNew(async () => {

                var loggingData = await discRequestChannel.Reader.ReadAsync();

                var retry = 0;

                do
                {

                    try
                    {

                        File.AppendAllText(this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName),
                                           String.Concat("[" + Timestamp.Now.ToLocalTime() + " T:" + Environment.CurrentManagedThreadId.ToString() + "] ",
                                                         this.Context + "/",
                                                         loggingData.LogEventName,
                                                         loggingData.Request,
                                                         Environment.NewLine),
                                           Encoding.UTF8);

                        break;

                    }
                    catch (IOException e)
                    {

                        if (e.HResult != -2147024864)
                        {
                            DebugX.LogT("File access error while logging to '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "' (retry: " + retry + "): " + e.Message);
                            Thread.Sleep(100);
                        }

                        else
                        {
                            DebugX.LogT("Could not log to '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "': " + e.Message);
                            break;
                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.LogT("Could not log to '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "': " + e.Message);
                        break;
                    }

                }
                while (retry++ < MaxRetries);

                if (retry >= MaxRetries)
                    DebugX.LogT("Could not write to logfile '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "' for " + retry + " retries!");

                else if (retry > 0)
                    DebugX.LogT("Successfully written to logfile '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "' after " + retry + " retries!");

            });

            _ = Task.Factory.StartNew(async () => {

                var loggingData = await discResponseChannel.Reader.ReadAsync();

                var retry = 0;

                do
                {

                    try
                    {

                        File.AppendAllText(this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName),
                                           String.Concat("[" + Timestamp.Now.ToLocalTime() + " T:" + Environment.CurrentManagedThreadId.ToString() + "] ",
                                                         Context + "/",
                                                         loggingData.LogEventName,
                                                         loggingData.Request,
                                                         " => ",
                                                         loggingData.Response,
                                                         " in ", Math.Round(loggingData.Runtime.TotalMilliseconds), "ms",
                                                         Environment.NewLine),
                                           Encoding.UTF8);

                        break;

                    }
                    catch (IOException e)
                    {

                        if (e.HResult != -2147024864)
                        {
                            DebugX.LogT("File access error while logging to '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "' (retry: " + retry + "): " + e.Message);
                            Thread.Sleep(100);
                        }

                        else
                        {
                            DebugX.LogT("Could not log to '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "': " + e.Message);
                            break;
                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.LogT("Could not log to '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "': " + e.Message);
                        break;
                    }

                }
                while (retry++ < MaxRetries);

                if (retry >= MaxRetries)
                    DebugX.LogT("Could not write to logfile '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "' for " + retry + " retries!");

                else if (retry > 0)
                    DebugX.LogT("Successfully written to logfile '" + this.LogfileCreator(this.LoggingPath, this.Context, loggingData.LogEventName) + "' after " + retry + " retries!");

            });


        }

        #endregion


        // Default logging delegates

        #region Default_LogRequest_toConsole (Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming HTTP requests to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        public async Task Default_LogRequest_toConsole(String  LoggingPath,
                                                       String  Context,
                                                       String  LogEventName,
                                                       String? Request)
        {

            if (Request is null)
                return;

            await cliRequestChannel.Writer.WriteAsync(new RequestData(LoggingPath,
                                                                      Context,
                                                                      LogEventName,
                                                                      Request));

        }

        #endregion

        #region Default_LogResponse_toConsole(Context, LogEventName, Request, Response, Runtime)

        /// <summary>
        /// A default delegate for logging HTTP requests/-responses to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public async Task Default_LogResponse_toConsole(String    LoggingPath,
                                                        String    Context,
                                                        String    LogEventName,
                                                        String?   Request,
                                                        String?   Response,
                                                        TimeSpan  Runtime)
        {

            if (Response is null)
                return;

            await cliResponseChannel.Writer.WriteAsync(new ResponseData(LoggingPath,
                                                                        Context,
                                                                        LogEventName,
                                                                        Request,
                                                                        Response,
                                                                        Runtime));

        }

        #endregion


        #region Default_LogRequest_toDisc (Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming HTTP requests to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        public async Task Default_LogRequest_toDisc(String  LoggingPath,
                                                    String  Context,
                                                    String  LogEventName,
                                                    String? Request)
        {


            if (Request is null)
                return;

            await discRequestChannel.Writer.WriteAsync(new RequestData(LoggingPath,
                                                                       Context,
                                                                       LogEventName,
                                                                       Request));


            ////ToDo: Can we have a lock per logfile?
            //var LockTaken = await LogRequest_toDisc_Lock.WaitAsync(MaxWaitingForALock);

            //try
            //{

            //    if (LockTaken)
            //    {

            //        var retry = 0;

            //        do
            //        {

            //            try
            //            {

            //                File.AppendAllText(LogfileCreator(LoggingPath, Context, LogEventName),
            //                                   String.Concat("[" + Timestamp.Now.ToLocalTime() + " T:" + Environment.CurrentManagedThreadId.ToString() + "] ",
            //                                                 Context + "/",
            //                                                 LogEventName,
            //                                                 Request,
            //                                                 Environment.NewLine),
            //                                   Encoding.UTF8);

            //                break;

            //            }
            //            catch (IOException e)
            //            {

            //                if (e.HResult != -2147024864)
            //                {
            //                    DebugX.LogT("File access error while logging to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "' (retry: " + retry + "): " + e.Message);
            //                    Thread.Sleep(100);
            //                }

            //                else
            //                {
            //                    DebugX.LogT("Could not log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "': " + e.Message);
            //                    break;
            //                }

            //            }
            //            catch (Exception e)
            //            {
            //                DebugX.LogT("Could not log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "': " + e.Message);
            //                break;
            //            }

            //        }
            //        while (retry++ < MaxRetries);

            //        if (retry >= MaxRetries)
            //            DebugX.LogT("Could not write to logfile '"      + LogfileCreator(LoggingPath, Context, LogEventName) + "' for "   + retry + " retries!");

            //        else if (retry > 0)
            //            DebugX.LogT("Successfully written to logfile '" + LogfileCreator(LoggingPath, Context, LogEventName) + "' after " + retry + " retries!");

            //    }

            //    else
            //        DebugX.LogT("Could not get lock to log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "'!");

            //}
            //finally
            //{
            //    if (LockTaken)
            //        LogRequest_toDisc_Lock.Release();
            //}

        }

        #endregion

        #region Default_LogResponse_toDisc(Context, LogEventName, Request, Response)

        /// <summary>
        /// A default delegate for logging HTTP requests/-responses to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public async Task Default_LogResponse_toDisc(String    LoggingPath,
                                                     String    Context,
                                                     String    LogEventName,
                                                     String?   Request,
                                                     String?   Response,
                                                     TimeSpan  Runtime)
        {

            if (Response is null)
                return;

            await discResponseChannel.Writer.WriteAsync(new ResponseData(LoggingPath,
                                                                         Context,
                                                                         LogEventName,
                                                                         Request,
                                                                         Response,
                                                                         Runtime));


            ////ToDo: Can we have a lock per logfile?
            //var LockTaken = await LogResponse_toDisc_Lock.WaitAsync(MaxWaitingForALock);

            //try
            //{

            //    if (LockTaken)
            //    {

            //        var retry = 0;

            //        do
            //        {

            //            try
            //            {

            //                File.AppendAllText(LogfileCreator(LoggingPath, Context, LogEventName),
            //                                   String.Concat("[" + Timestamp.Now.ToLocalTime() + " T:" + Environment.CurrentManagedThreadId.ToString() + "] ",
            //                                                 Context + "/",
            //                                                 LogEventName,
            //                                                 Request,
            //                                                 " => ",
            //                                                 Response,
            //                                                 " in ", Math.Round(Runtime.TotalMilliseconds), "ms",
            //                                                 Environment.NewLine),
            //                                   Encoding.UTF8);

            //                break;

            //            }
            //            catch (IOException e)
            //            {

            //                if (e.HResult != -2147024864)
            //                {
            //                    DebugX.LogT("File access error while logging to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "' (retry: " + retry + "): " + e.Message);
            //                    Thread.Sleep(100);
            //                }

            //                else
            //                {
            //                    DebugX.LogT("Could not log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "': " + e.Message);
            //                    break;
            //                }

            //            }
            //            catch (Exception e)
            //            {
            //                DebugX.LogT("Could not log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "': " + e.Message);
            //                break;
            //            }

            //        }
            //        while (retry++ < MaxRetries);

            //        if (retry >= MaxRetries)
            //            DebugX.LogT("Could not write to logfile '"      + LogfileCreator(LoggingPath, Context, LogEventName) + "' for "   + retry + " retries!");

            //        else if (retry > 0)
            //            DebugX.LogT("Successfully written to logfile '" + LogfileCreator(LoggingPath, Context, LogEventName) + "' after " + retry + " retries!");

            //    }

            //    else
            //        DebugX.LogT("Could not get lock to log to '" + LogfileCreator(LoggingPath, Context, LogEventName) + "'!");

            //}
            //finally
            //{
            //    if (LockTaken)
            //        LogResponse_toDisc_Lock.Release();
            //}

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

            if (LogEventOrGroupName.IsNullOrEmpty())
                return false;

            if (groupTags.TryGetValue(LogEventOrGroupName,
                                       out HashSet<String>? logEventNames))
            {
                return logEventNames.
                           Select(logname => InternalDebug(logname, LogTarget)).
                           All   (result  => result == true);
            }

            return InternalDebug(LogEventOrGroupName, LogTarget);

        }

        #endregion

        #region (protected) InternalDebug(LogEventName, LogTarget)

        protected abstract Boolean InternalDebug(String      LogEventName,
                                                 LogTargets  LogTarget);

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

            if (LogEventOrGroupName.IsNullOrEmpty())
                return false;

            if (groupTags.TryGetValue(LogEventOrGroupName,
                                       out HashSet<String>? logEventNames))
            {
                return logEventNames.
                           Select(logname => InternalUndebug(logname, LogTarget)).
                           All   (result  => result == true);
            }

            return InternalUndebug(LogEventOrGroupName, LogTarget);

        }

        #endregion

        #region (private) InternalUndebug(LogEventName, LogTarget)

        protected abstract Boolean InternalUndebug(String      LogEventName,
                                                   LogTargets  LogTarget);

        #endregion


    }

}
