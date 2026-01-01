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

using System.Text;
using System.Threading.Channels;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

   // public delegate Task HTTPRequestLoggerDelegate (String LoggingPath, String Context, String LogEventName, HTTPRequest Request);
   // public delegate Task HTTPResponseLoggerDelegate(String LoggingPath, String Context, String LogEventName, HTTPRequest Request, HTTPResponse Response);

    /// <summary>
    /// An HTTP API logger.
    /// </summary>
    public abstract class AHTTPLoggerX
    {

        #region (class) RequestData

        public class RequestData
        {
            public String       LoggingPath     { get; }
            public String       Context         { get; }
            public String       LogEventName    { get; }
            public HTTPRequest  Request         { get; }

            public RequestData(String       loggingPath,
                               String       context,
                               String       logEventName,
                               HTTPRequest  request)
            {
                LoggingPath   = loggingPath;
                Context       = context;
                LogEventName  = logEventName;
                Request       = request;
            }

        }

        #endregion

        #region (class) ResponseData

        public class ResponseData
        {
            public String        LoggingPath     { get; }
            public String        Context         { get; }
            public String        LogEventName    { get; }
            public HTTPRequest   Request         { get; }
            public HTTPResponse  Response        { get; }

            public ResponseData(String        loggingPath,
                                String        context,
                                String        logEventName,
                                HTTPRequest   request,
                                HTTPResponse  response)
            {
                LoggingPath   = loggingPath;
                Context       = context;
                LogEventName  = logEventName;
                Request       = request;
                Response      = response;
            }

        }

        #endregion


        #region Data

        private        readonly  Channel<RequestData>                           cliRequestChannel;
        private        readonly  Channel<ResponseData>                          cliResponseChannel;

        private        readonly  Channel<RequestData>                           discRequestChannel;
        private        readonly  Channel<ResponseData>                          discResponseChannel;

        private        readonly  CancellationTokenSource                        cancellationTokenSource;

        /// <summary>
        /// The maximum number of retries to write to a logfile.
        /// </summary>
        public  static readonly  Byte                                           MaxRetries          = 5;

        /// <summary>
        /// Maximum waiting time to enter a lock around a logfile.
        /// </summary>
        public  static readonly  TimeSpan                                       MaxWaitingForALock  = TimeSpan.FromSeconds(15);

        protected      readonly  ConcurrentDictionary<String, HashSet<String>>  groupTags           = new (StringComparer.OrdinalIgnoreCase);

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
        public AHTTPLoggerX(String?                      LoggingPath                 = null,
                            String?                      Context                     = null,

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

        {

            #region Init data structures

            this.LoggingPath  = LoggingPath ?? AppContext.BaseDirectory;
            this.Context      = Context     ?? "";

            #endregion

            #region Set default delegates

            //LogHTTPRequest_toConsole   ??= Default_LogHTTPRequest_toConsole;
            //LogHTTPRequest_toDisc      ??= Default_LogHTTPRequest_toDisc;
            //LogHTTPResponse_toConsole  ??= Default_LogHTTPResponse_toConsole;
            //LogHTTPResponse_toDisc     ??= Default_LogHTTPResponse_toDisc;

            //if (LogHTTPRequest_toDisc  is not null ||
            //    LogHTTPResponse_toDisc is not null ||
            //    LogHTTPError_toDisc    is not null)
            //{
            //    if (this.LoggingPath.IsNotNullOrEmpty())
            //        Directory.CreateDirectory(this.LoggingPath);
            //}

            this.LogfileCreator  = LogfileCreator ?? ((loggingPath, context, logfileName) => String.Concat(
                                                                                                 loggingPath,
                                                                                                 context is not null ? context + "_" : String.Empty,
                                                                                                 logfileName, "_",
                                                                                                 Timestamp.Now.Year, "-",
                                                                                                 Timestamp.Now.Month.ToString("D2"),
                                                                                                 ".log"
                                                                                             ));

            #endregion

            cliRequestChannel        = Channel.CreateUnbounded<RequestData>();
            cliResponseChannel       = Channel.CreateUnbounded<ResponseData>();

            discRequestChannel       = Channel.CreateUnbounded<RequestData>();
            discResponseChannel      = Channel.CreateUnbounded<ResponseData>();

            cancellationTokenSource  = new CancellationTokenSource();


            #region CLI Logging

            _ = Task.Factory.StartNew(async () => {

                var logfilePath = Path.Combine(AppContext.BaseDirectory, "cliRequestChannel.log");

                do
                {

                    try
                    {

                        var loggingData = await cliRequestChannel.Reader.ReadAsync(cancellationTokenSource.Token);

                        var PreviousColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.Write($"[{loggingData.Request.Timestamp.ToLocalTime():dd.MM.yyyy HH:mm:ss zzz} T:{Environment.CurrentManagedThreadId}] ");

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(this.Context + "/");

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(loggingData.LogEventName);

                        //Console.ForegroundColor = ConsoleColor.Gray;
                        //Console.WriteLine(Request.HTTPSource.Socket == Request.LocalSocket
                        //                      ? String.Concat(Request.LocalSocket, " -> ", Request.RemoteSocket)
                        //                      : String.Concat(Request.HTTPSource,  " -> ", Request.LocalSocket));

                        Console.ForegroundColor = PreviousColor;

                    }
                    catch (OperationCanceledException)
                    {
                        // Log cancellation to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Channel consumer was canceled.\n",
                            cancellationTokenSource.Token);
                        break; // Exit the loop on cancellation
                    }
                    catch (ChannelClosedException)
                    {
                        // Log channel closure to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Channel was closed unexpectedly.\n",
                            cancellationTokenSource.Token);
                        break; // Exit the loop if channel is closed
                    }
                    catch (Exception ex)
                    {
                        // Log other unexpected exceptions to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Error in channel consumer: {ex.Message}\n{ex.StackTrace}\n",
                            cancellationTokenSource.Token);

                        // Log to console for visibility
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error in channel consumer: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                        Console.ForegroundColor = ConsoleColor.White;

                        // Optional delay before retrying
                        await Task.Delay(1000, cancellationTokenSource.Token);
                    }

                }
                while (!cancellationTokenSource.IsCancellationRequested);

            }, cancellationTokenSource.Token,
               TaskCreationOptions.LongRunning,
               TaskScheduler.Default);

            _ = Task.Factory.StartNew(async () => {

                var logfilePath = Path.Combine(AppContext.BaseDirectory, "cliResponseChannel.log");

                do
                {

                    try
                    {

                        var loggingData = await cliResponseChannel.Reader.ReadAsync(cancellationTokenSource.Token);

                        var PreviousColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.Write($"[{loggingData.Request.Timestamp.ToLocalTime():dd.MM.yyyy HH:mm:ss zzz} T:{Environment.CurrentManagedThreadId}] ");

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(this.Context + "/");

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(loggingData.LogEventName);

                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.Write(String.Concat(" from ", loggingData.Request.HTTPSource, " => "));

                        if (loggingData.Response.HTTPStatusCode == HTTPStatusCode.OK ||
                            loggingData.Response.HTTPStatusCode == HTTPStatusCode.Created)
                            Console.ForegroundColor = ConsoleColor.Green;

                        else if (loggingData.Response.HTTPStatusCode == HTTPStatusCode.NoContent)
                            Console.ForegroundColor = ConsoleColor.Yellow;

                        else
                            Console.ForegroundColor = ConsoleColor.Red;

                        Console.Write(loggingData.Response.HTTPStatusCode);

                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine(String.Concat(" in ", Math.Round((loggingData.Response.Timestamp - loggingData.Request.Timestamp).TotalMilliseconds), "ms"));

                        Console.ForegroundColor = PreviousColor;

                    }
                    catch (OperationCanceledException)
                    {
                        // Log cancellation to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Channel consumer was canceled.\n",
                            cancellationTokenSource.Token);
                        break; // Exit the loop on cancellation
                    }
                    catch (ChannelClosedException)
                    {
                        // Log channel closure to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Channel was closed unexpectedly.\n",
                            cancellationTokenSource.Token);
                        break; // Exit the loop if channel is closed
                    }
                    catch (Exception ex)
                    {
                        // Log other unexpected exceptions to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Error in channel consumer: {ex.Message}\n{ex.StackTrace}\n",
                            cancellationTokenSource.Token);

                        // Log to console for visibility
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error in channel consumer: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                        Console.ForegroundColor = ConsoleColor.White;

                        // Optional delay before retrying
                        await Task.Delay(1000, cancellationTokenSource.Token);
                    }

                }
                while (!cancellationTokenSource.IsCancellationRequested);

            }, cancellationTokenSource.Token,
               TaskCreationOptions.LongRunning,
               TaskScheduler.Default);

            #endregion

            #region Disc Logging

            _ = Task.Factory.StartNew(async () => {

                var logfilePath = Path.Combine(AppContext.BaseDirectory, "discRequestChannel.log");

                do
                {

                    try
                    {

                        var requestLogData  = await discRequestChannel.Reader.ReadAsync(cancellationTokenSource.Token);
                        var logFileName     = this.LogfileCreator(this.LoggingPath, this.Context, requestLogData.LogEventName);
                        var retry           = 0;
                        var data            = String.Concat(
                                                  requestLogData.Request.HTTPSource.Socket == requestLogData.Request.LocalSocket // A HTTP Client Request!
                                                       ? $"{requestLogData.Request.LocalSocket} -> {requestLogData.Request.RemoteSocket}"  // A HTTP Client Request!
                                                       : $"{requestLogData.Request.HTTPSource } -> {requestLogData.Request.LocalSocket}",  // A HTTP Server Request!
                                                  //requestLogData.Request.HTTPSource.Socket == requestLogData.Request.LocalSocket
                                                  //    ? $"{requestLogData.Request.LocalSocket} -> {requestLogData.Request.RemoteSocket}"
                                                  //    : $"{requestLogData.Request.HTTPSource } -> {requestLogData.Request.LocalSocket}",
                                                  requestLogData.Request.IsKeepAlive
                                                      ? $" (KeepAlive: {requestLogData.Request.HTTPClient?.KeepAliveMessageCount.ToString() ?? "-"})"
                                                      : "",                                                                            Environment.NewLine,
                                                  ">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------",  Environment.NewLine,
                                                  requestLogData.Request.Timestamp.ToISO8601(),                                        Environment.NewLine,
                                                  requestLogData.Request.EntirePDU,                                                    Environment.NewLine,
                                                  "--------------------------------------------------------------------------------",  Environment.NewLine
                                              );

                        do
                        {

                            try
                            {

                                File.AppendAllText(
                                    logFileName,
                                    data,
                                    Encoding.UTF8
                                );

                                break;

                            }
                            catch (IOException e)
                            {

                                if (e.HResult != -2147024864)
                                {
                                    DebugX.LogT($"File access error while logging to '{logFileName}' (retry: {retry}): {e.Message}");
                                    Thread.Sleep(100);
                                }

                                else
                                {
                                    DebugX.LogT($"Could not log to '{logFileName}': {e.Message}");
                                    break;
                                }

                            }
                            catch (Exception e)
                            {
                                DebugX.LogT($"Could not log to '{logFileName}': {e.Message}");
                                break;
                            }

                        }
                        while (retry++ < MaxRetries);

                        if (retry >= MaxRetries)
                            DebugX.LogT($"Could not write to logfile '{logFileName}' for {retry} retries!");

                        else if (retry > 0)
                            DebugX.LogT($"Successfully written to logfile '{logFileName}' after {retry} retries!");

                    }
                    catch (OperationCanceledException)
                    {
                        // Log cancellation to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Channel consumer was canceled.\n",
                            cancellationTokenSource.Token);
                        break; // Exit the loop on cancellation
                    }
                    catch (ChannelClosedException)
                    {
                        // Log channel closure to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Channel was closed unexpectedly.\n",
                            cancellationTokenSource.Token);
                        break; // Exit the loop if channel is closed
                    }
                    catch (Exception ex)
                    {
                        // Log other unexpected exceptions to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Error in channel consumer: {ex.Message}\n{ex.StackTrace}\n",
                            cancellationTokenSource.Token);

                        // Log to console for visibility
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error in channel consumer: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                        Console.ForegroundColor = ConsoleColor.White;

                        // Optional delay before retrying
                        await Task.Delay(1000, cancellationTokenSource.Token);
                    }

                }
                while (!cancellationTokenSource.IsCancellationRequested);

            }, cancellationTokenSource.Token,
               TaskCreationOptions.LongRunning,
               TaskScheduler.Default);

            _ = Task.Factory.StartNew(async () => {

                var logfilePath = Path.Combine(AppContext.BaseDirectory, "discRequestChannel.log");

                do
                {

                    try
                    {

                        var responseLogData  = await discResponseChannel.Reader.ReadAsync(cancellationTokenSource.Token);
                        var logFileName      = this.LogfileCreator(this.LoggingPath, this.Context, responseLogData.LogEventName);
                        var retry            = 0;
                        var data             = String.Concat(
                                                   responseLogData.Response.HTTPSource.Socket == responseLogData.Response.LocalSocket // A HTTP Client Request!
                                                       ? $"{responseLogData.Response.LocalSocket} -> {responseLogData.Response.RemoteSocket}"  // A HTTP Client Request!
                                                       : $"{responseLogData.Response.HTTPSource } -> {responseLogData.Response.LocalSocket}",  // A HTTP Server Request!
                                                   responseLogData.Response.IsKeepAlive
                                                      ? $" (KeepAlive: {responseLogData.Response.HTTPClient?.KeepAliveMessageCount.ToString() ?? "-"})"
                                                      : "",                                                                                                              Environment.NewLine,
                                                   ">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------",                                   Environment.NewLine,
                                                   responseLogData.Request. Timestamp.ToISO8601(),                                                                       Environment.NewLine,
                                                   responseLogData.Request. EntirePDU,                                                                                   Environment.NewLine,
                                                   "<<<<<<--Response----<<<<<<------<<<<<<------<<<<<<------<<<<<<------<<<<<<------",                                   Environment.NewLine,
                                                $"{responseLogData.Response.Timestamp.ToISO8601()} -> {responseLogData.Response.Runtime.TotalMilliseconds} ms runtime",  Environment.NewLine,
                                                   responseLogData.Response.EntirePDU,                                                                                   Environment.NewLine,
                                                   "--------------------------------------------------------------------------------",                                   Environment.NewLine
                                               );

                        do
                        {

                            try
                            {

                                File.AppendAllText(
                                    logFileName,
                                    data,
                                    Encoding.UTF8
                                );

                                break;

                            }
                            catch (IOException e)
                            {

                                if (e.HResult != -2147024864)
                                {
                                    DebugX.LogT($"File access error while logging to '{logFileName}' (retry: {retry}): {e.Message}");
                                    Thread.Sleep(100);
                                }

                                else
                                {
                                    DebugX.LogT($"Could not log to '{logFileName}': {e.Message}");
                                    break;
                                }

                            }
                            catch (Exception e)
                            {
                                DebugX.LogT($"Could not log to '{logFileName}': {e.Message}");
                                break;
                            }

                        }
                        while (retry++ < MaxRetries);

                        if (retry >= MaxRetries)
                            DebugX.LogT($"Could not write to logfile '{logFileName}' for {retry} retries!");

                        else if (retry > 0)
                            DebugX.LogT($"Successfully written to logfile '{logFileName}' after {retry} retries!");

                    }
                    catch (OperationCanceledException)
                    {
                        // Log cancellation to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Channel consumer was canceled.\n",
                            cancellationTokenSource.Token);
                        break; // Exit the loop on cancellation
                    }
                    catch (ChannelClosedException)
                    {
                        // Log channel closure to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Channel was closed unexpectedly.\n",
                            cancellationTokenSource.Token);
                        break; // Exit the loop if channel is closed
                    }
                    catch (Exception ex)
                    {
                        // Log other unexpected exceptions to file
                        await File.AppendAllTextAsync(logfilePath,
                            $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss zzz}] Error in channel consumer: {ex.Message}\n{ex.StackTrace}\n",
                            cancellationTokenSource.Token);

                        // Log to console for visibility
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error in channel consumer: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                        Console.ForegroundColor = ConsoleColor.White;

                        // Optional delay before retrying
                        await Task.Delay(1000, cancellationTokenSource.Token);
                    }

                }
                while (!cancellationTokenSource.IsCancellationRequested);

            }, cancellationTokenSource.Token,
               TaskCreationOptions.LongRunning,
               TaskScheduler.Default);

            #endregion


        }

        #endregion


        // Default logging delegates

        #region Default_LogHTTPRequest_toConsole (Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming HTTP requests to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        public Task Default_LogHTTPRequest_toConsole(String       LoggingPath,
                                                     String       Context,
                                                     String       LogEventName,
                                                     HTTPRequest  Request)

            => cliRequestChannel.Writer.WriteAsync(
                   new RequestData(
                       LoggingPath,
                       Context,
                       LogEventName,
                       Request
                   )
               ).AsTask();


        #endregion

        #region Default_LogHTTPResponse_toConsole(Context, LogEventName, Request, Response)

        /// <summary>
        /// A default delegate for logging HTTP requests/-responses to console.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public Task Default_LogHTTPResponse_toConsole(String        LoggingPath,
                                                      String        Context,
                                                      String        LogEventName,
                                                      HTTPRequest   Request,
                                                      HTTPResponse  Response)

            => cliResponseChannel.Writer.WriteAsync(
                   new ResponseData(
                       LoggingPath,
                       Context,
                       LogEventName,
                       Request,
                       Response
                   )
               ).AsTask();

        #endregion


        #region Default_LogHTTPRequest_toDisc (Context, LogEventName, Request)

        /// <summary>
        /// A default delegate for logging incoming HTTP requests to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        public Task Default_LogHTTPRequest_toDisc(String       LoggingPath,
                                                  String       Context,
                                                  String       LogEventName,
                                                  HTTPRequest  Request)

            => discRequestChannel.Writer.WriteAsync(
                   new RequestData(
                       LoggingPath,
                       Context,
                       LogEventName,
                       Request
                   )
               ).AsTask();

        #endregion

        #region Default_LogHTTPResponse_toDisc(Context, LogEventName, Request, Response)

        /// <summary>
        /// A default delegate for logging HTTP requests/-responses to disc.
        /// </summary>
        /// <param name="Context">The context of the log request.</param>
        /// <param name="LogEventName">The name of the log event.</param>
        /// <param name="Request">The HTTP request to log.</param>
        /// <param name="Response">The HTTP response to log.</param>
        public Task Default_LogHTTPResponse_toDisc(String        LoggingPath,
                                                   String        Context,
                                                   String        LogEventName,
                                                   HTTPRequest   Request,
                                                   HTTPResponse  Response)

            => discResponseChannel.Writer.WriteAsync(
                   new ResponseData(
                       LoggingPath,
                       Context,
                       LogEventName,
                       Request,
                       Response
                   )
               ).AsTask();

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

            if (groupTags.TryGetValue(LogEventOrGroupName,
                                      out var logEventNames))
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

            if (groupTags.TryGetValue(LogEventOrGroupName,
                                      out var logEventNames))
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


        public void Stop()
        {
            cancellationTokenSource.Cancel();
        }

    }

}
