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

using System.Reflection;
using System.Collections.Concurrent;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Logging;
using System.Reflection.Emit;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    #region (class) HTTPRequestLogEvent

    /// <summary>
    /// An async event notifying about HTTP requests.
    /// </summary>
    public class HTTPRequestLogEventX
    {

        #region Data

        private readonly List<HTTPRequestLogHandlerX> subscribers = [];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about incoming HTTP requests.
        /// </summary>
        public HTTPRequestLogEventX()
        { }

        #endregion


        #region + / Add

        public static HTTPRequestLogEventX operator + (HTTPRequestLogEventX e, HTTPRequestLogHandlerX callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Add(callback);
            }

            return e;

        }

        public HTTPRequestLogEventX Add(HTTPRequestLogHandlerX callback)
        {

            lock (subscribers)
            {
                subscribers.Add(callback);
            }

            return this;

        }

        #endregion

        #region - / Remove

        public static HTTPRequestLogEventX operator - (HTTPRequestLogEventX e, HTTPRequestLogHandlerX callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPRequestLogEventX Remove(HTTPRequestLogHandlerX callback)
        {

            lock (subscribers)
            {
                subscribers.Remove(callback);
            }

            return this;

        }

        #endregion


        #region InvokeAsync(ServerTimestamp, HTTPAPI, Request)

        /// <summary>
        /// Call all subscribers sequentially.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        public async Task InvokeAsync(DateTimeOffset     ServerTimestamp,
                                      HTTPAPIX           HTTPAPI,
                                      HTTPRequest        Request,
                                      CancellationToken  CancellationToken)
        {

            HTTPRequestLogHandlerX[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers];
            }

            foreach (var callback in invocationList)
                await callback(ServerTimestamp, HTTPAPI, Request, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region WhenAny    (ServerTimestamp, HTTPAPI, Request,               Timeout = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for any to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        public Task WhenAny(DateTimeOffset     ServerTimestamp,
                            HTTPAPIX           HTTPAPI,
                            HTTPRequest        Request,
                            CancellationToken  CancellationToken,
                            TimeSpan?          Timeout   = null)
        {

            List<Task> invocationList;

            lock (subscribers)
            {

                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, CancellationToken))];

                if (Timeout.HasValue)
                    invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(invocationList);

        }

        #endregion

        #region WhenFirst  (ServerTimestamp, HTTPAPI, Request, VerifyResult, Timeout = null, DefaultResult = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <typeparam name="T">The type of the results.</typeparam>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="VerifyResult">A delegate to verify and filter results.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        /// <param name="DefaultResult">A default result in case of errors or a timeout.</param>
        public Task<T> WhenFirst<T>(DateTimeOffset      ServerTimestamp,
                                    HTTPAPIX            HTTPAPI,
                                    HTTPRequest         Request,
                                    Func<T, Boolean>    VerifyResult,
                                    CancellationToken   CancellationToken,
                                    TimeSpan?           Timeout         = null,
                                    Func<TimeSpan, T>?  DefaultResult   = null)
        {

            #region Data

            List<Task>      invocationList;
            Task?           WorkDone;
            Task<T>?        Result;
            DateTimeOffset  StartTime     = Timestamp.Now;
            Task?           TimeoutTask   = null;

            #endregion

            lock (subscribers)
            {

                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, CancellationToken))];

                if (Timeout.HasValue)
                    invocationList.Add(TimeoutTask = Task.Run(() => Thread.Sleep(Timeout.Value)));

            }

            do
            {

                try
                {

                    WorkDone = Task.WhenAny(invocationList);

                    invocationList.Remove(WorkDone);

                    if (WorkDone != TimeoutTask)
                    {

                        Result = WorkDone as Task<T>;

                        if (Result is not null &&
                            !EqualityComparer<T>.Default.Equals(Result.Result, default) &&
                            VerifyResult(Result.Result))
                        {
                            return Result;
                        }

                    }

                }
                catch (Exception e)
                {
                    DebugX.LogT(e.Message);
                    WorkDone = null;
                }

            }
            while (!(WorkDone == TimeoutTask || invocationList.Count == 0));

            return Task.FromResult(DefaultResult(Timestamp.Now - StartTime));

        }

        #endregion

        #region WhenAll    (ServerTimestamp, HTTPAPI, Request)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        public Task WhenAll(DateTimeOffset     ServerTimestamp,
                            HTTPAPIX           HTTPAPI,
                            HTTPRequest        Request,
                            CancellationToken  CancellationToken)
        {

            Task[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, CancellationToken))];
            }

            return Task.WhenAll(invocationList);

        }

        #endregion

    }

    #endregion

    #region (class) HTTPResponseLogEvent

    /// <summary>
    /// An async event notifying about HTTP responses.
    /// </summary>
    public class HTTPResponseLogEventX
    {

        #region Data

        private readonly List<HTTPResponseLogHandlerX> subscribers = [];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about HTTP responses.
        /// </summary>
        public HTTPResponseLogEventX()
        { }

        #endregion


        #region + / Add

        public static HTTPResponseLogEventX operator + (HTTPResponseLogEventX e, HTTPResponseLogHandlerX callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Add(
                    (timestamp, api, request, response, cancellationToken)
                        => callback(timestamp, api, request, response, cancellationToken)
                );
            }

            return e;

        }

        public HTTPResponseLogEventX Add(HTTPResponseLogHandlerX callback)
        {

            lock (subscribers)
            {
                subscribers.Add(callback);
            }

            return this;

        }

        #endregion

        #region - / Remove

        public static HTTPResponseLogEventX operator - (HTTPResponseLogEventX e, HTTPResponseLogHandlerX callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPResponseLogEventX Remove(HTTPResponseLogHandlerX callback)
        {

            lock (subscribers)
            {
                subscribers.Remove(callback);
            }

            return this;

        }

        #endregion


        #region InvokeAsync(ServerTimestamp, HTTPAPI, Request, Response)

        /// <summary>
        /// Call all subscribers sequentially.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        public async Task InvokeAsync(DateTimeOffset     ServerTimestamp,
                                      HTTPAPIX           HTTPAPI,
                                      HTTPRequest        Request,
                                      HTTPResponse       Response,
                                      CancellationToken  CancellationToken)
        {

            HTTPResponseLogHandlerX[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers];
            }

            foreach (var callback in invocationList)
                await callback(ServerTimestamp, HTTPAPI, Request, Response, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region WhenAny    (ServerTimestamp, HTTPAPI, Request, Response,               Timeout = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for any to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        public Task WhenAny(DateTimeOffset     ServerTimestamp,
                            HTTPAPIX           HTTPAPI,
                            HTTPRequest        Request,
                            HTTPResponse       Response,
                            CancellationToken  CancellationToken,
                            TimeSpan?          Timeout = null)
        {

            List<Task> invocationList;

            lock (subscribers)
            {

                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, CancellationToken))];

                if (Timeout.HasValue)
                    invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(invocationList);

        }

        #endregion

        #region WhenFirst  (ServerTimestamp, HTTPAPI, Request, Response, VerifyResult, Timeout = null, DefaultResult = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <typeparam name="T">The type of the results.</typeparam>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="VerifyResult">A delegate to verify and filter results.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        /// <param name="DefaultResult">A default result in case of errors or a timeout.</param>
        public Task<T> WhenFirst<T>(DateTimeOffset      ServerTimestamp,
                                    HTTPAPIX            HTTPAPI,
                                    HTTPRequest         Request,
                                    HTTPResponse        Response,
                                    Func<T, Boolean>    VerifyResult,
                                    CancellationToken   CancellationToken,
                                    TimeSpan?           Timeout         = null,
                                    Func<TimeSpan, T>?  DefaultResult   = null)
        {

            #region Data

            List<Task>      invocationList;
            Task?           WorkDone;
            Task<T>?        Result;
            DateTimeOffset  StartTime     = Timestamp.Now;
            Task?           TimeoutTask   = null;

            #endregion

            lock (subscribers)
            {

                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, CancellationToken))];

                if (Timeout.HasValue)
                    invocationList.Add(TimeoutTask = Task.Run(() => Thread.Sleep(Timeout.Value)));

            }

            do
            {

                try
                {

                    WorkDone = Task.WhenAny(invocationList);

                    invocationList.Remove(WorkDone);

                    if (WorkDone != TimeoutTask)
                    {

                        Result = WorkDone as Task<T>;

                        if (Result is not null &&
                            !EqualityComparer<T>.Default.Equals(Result.Result, default) &&
                            VerifyResult(Result.Result))
                        {
                            return Result;
                        }

                    }

                }
                catch (Exception e)
                {
                    DebugX.LogT(e.Message);
                    WorkDone = null;
                }

            }
            while (!(WorkDone == TimeoutTask || invocationList.Count == 0));

            return Task.FromResult(DefaultResult(Timestamp.Now - StartTime));

        }

        #endregion

        #region WhenAll    (ServerTimestamp, HTTPAPI, Request, Response)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        public Task WhenAll(DateTimeOffset     ServerTimestamp,
                            HTTPAPIX           HTTPAPI,
                            HTTPRequest        Request,
                            HTTPResponse       Response,
                            CancellationToken  CancellationToken)
        {

            Task[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, CancellationToken))];
            }

            return Task.WhenAll(invocationList);

        }

        #endregion

    }

    #endregion


    public static class HTTPAPIXExtensions
    {

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="HTTPAPIX">An HTTP API.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPDelegate">A delegate called for each incoming HTTP request.</param>
        public static HTTPTestServerX

            StartServer(this HTTPAPIX            HTTPAPIX,
                        HTTPPath                 Path,
                        HTTPHostname?            Hostname         = null,

                        IIPAddress?              IPAddress        = null,
                        IPPort?                  TCPPort          = null,
                        String?                  HTTPServerName   = null,
                        UInt32?                  BufferSize       = null,
                        TimeSpan?                ReceiveTimeout   = null,
                        TimeSpan?                SendTimeout      = null,
                        TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var server = new HTTPTestServerX(
                             IPAddress,
                             TCPPort,
                             HTTPServerName,
                             BufferSize,
                             ReceiveTimeout,
                             SendTimeout,
                             LoggingHandler
                         );

            server.AddHTTPAPI(
                       Path,
                       Hostname,
                       (server, path) => HTTPAPIX
                   );

            return server;

        }

    }


    /// <summary>
    /// A URL node which stores some child nodes and a callback
    /// </summary>
    public class HTTPAPIX : AHTTPAPIXBase
    {

        #region Data

        /// <summary>
        /// The default maintenance interval.
        /// </summary>
        public           readonly  TimeSpan       DefaultMaintenanceEvery  = TimeSpan.FromMinutes(1);

        private          readonly  Timer          MaintenanceTimer;

        protected static readonly  TimeSpan       SemaphoreSlimTimeout     = TimeSpan.FromSeconds(5);

        protected static readonly  SemaphoreSlim  MaintenanceSemaphore     = new (1, 1);

        /// <summary>
        /// The HTTP root for embedded resources.
        /// </summary>
        public  const              String         HTTPRoot                             = "org.GraphDefined.Vanaheimr.Hermod.HTTPRoot.";

        #endregion

        #region Properties

        public HTTPTestServerX               HTTPServer              { get; internal set; }

        /// <summary>
        /// The HTTP hostname of this HTTP API.
        /// </summary>
        public IEnumerable<HTTPHostname>     Hostnames                   { get; }

        /// <summary>
        /// The HTTP root path of this HTTP API.
        /// </summary>
        public HTTPPath                      RootPath
            => URLPathPrefix;


        public HTTPPath?                     BasePath                    { get; }

        /// <summary>
        /// The HTTP content types served by this HTTP API.
        /// </summary>
        public IEnumerable<HTTPContentType>  HTTPContentTypes            { get; }

        /// <summary>
        /// An optional description of this HTTP API.
        /// </summary>
        public I18NString?                   Description                 { get; }


        public String?                       ExternalDNSName             { get; }


        public Warden.Warden                 Warden                      { get; }
        public ECPrivateKeyParameters?       ServiceCheckPrivateKey      { get; set; }

        public ECPublicKeyParameters?        ServiceCheckPublicKey       { get; set; }
        public System_Id?                    SystemId                    { get; set; }


        /// <summary>
        /// Whether the reload of the system is finished.
        /// </summary>
        public Boolean                       ReloadFinished              { get; protected set; }

        /// <summary>
        /// The maintenance interval.
        /// </summary>
        public TimeSpan                      MaintenanceEvery            { get; }

        /// <summary>
        /// Disable all maintenance tasks.
        /// </summary>
        public Boolean                       DisableMaintenanceTasks     { get; set; }

        #endregion

        #region Constructor(s)

        public HTTPAPIX(HTTPTestServerX                HTTPServer,
                        IEnumerable<HTTPHostname>?     Hostnames                 = null,
                        HTTPPath?                      RootPath                  = null,
                        IEnumerable<HTTPContentType>?  HTTPContentTypes          = null,
                        I18NString?                    Description               = null,

                        String?                        ExternalDNSName           = null,
                        HTTPPath?                      BasePath                  = null,  // For URL prefixes in HTML!

                        String?                        HTTPServerName            = null,
                        String?                        HTTPServiceName           = null,
                        String?                        APIVersionHash            = null,
                        JObject?                       APIVersionHashes          = null,

                        Boolean?                       DisableMaintenanceTasks   = false,
                        TimeSpan?                      MaintenanceInitialDelay   = null,
                        TimeSpan?                      MaintenanceEvery          = null,

                        Boolean?                       DisableWardenTasks        = false,
                        TimeSpan?                      WardenInitialDelay        = null,
                        TimeSpan?                      WardenCheckEvery          = null,

                        Boolean?                       IsDevelopment             = null,
                        IEnumerable<String>?           DevelopmentServers        = null,
                        Boolean?                       DisableLogging            = false,
                        String?                        LoggingPath               = DefaultHTTPAPI_LoggingPath,
                        String?                        LoggingContext            = DefaultLoggingContext,
                        String?                        LogfileName               = DefaultHTTPAPI_LogfileName,
                        LogfileCreatorDelegate?        LogfileCreator            = null)

            : base(RootPath,
                   BasePath,

                   HTTPServerName,
                   HTTPServiceName,
                   APIVersionHash ?? APIVersionHashes?[nameof(HTTPAPIX)]?.Value<String>()?.Trim(),
                   APIVersionHashes,

                   IsDevelopment,
                   DevelopmentServers,
                   DisableLogging,
                   LoggingPath,
                   LogfileName,
                   LogfileCreator)

        {

            this.Hostnames                = Hostnames?.       Distinct() ?? [];
            this.HTTPContentTypes         = HTTPContentTypes?.Distinct() ?? [];
            this.Description              = Description                  ?? I18NString.Empty;
            this.HTTPServer               = HTTPServer;

            this.ExternalDNSName          = ExternalDNSName;
            this.BasePath                 = BasePath;

            // Register HTTP API within the HTTP server!
            HTTPServer?.AddHTTPAPI(
                this.RootPath,
                HTTPHostname.Any,
                (server, path) => this
            );

            // Setup Maintenance Task
            this.DisableMaintenanceTasks  = DisableMaintenanceTasks ?? false;
            this.MaintenanceEvery         = MaintenanceEvery        ?? DefaultMaintenanceEvery;
            this.MaintenanceTimer         = new Timer(
                                                DoMaintenanceSync,
                                                this,
                                                MaintenanceInitialDelay ?? this.MaintenanceEvery,
                                                this.MaintenanceEvery
                                            );

            // Setup Warden
            this.Warden                   = new Warden.Warden(
                                                WardenInitialDelay ?? TimeSpan.FromMinutes(3),
                                                WardenCheckEvery   ?? TimeSpan.FromMinutes(1),
                                                this.HTTPServer.DNSClient
                                            );

        }


        //internal HTTPAPIX(IEnumerable<HTTPHostname>?     Hostnames                 = null,
        //                  HTTPPath?                      RootPath                  = null,
        //                  IEnumerable<HTTPContentType>?  HTTPContentTypes          = null,
        //                  I18NString?                    Description               = null,
        //                  HTTPTestServerX?               HTTPServer                = null,

        //                  String?                        ExternalDNSName           = null,
        //                  HTTPPath?                      BasePath                  = null,

        //                  String?                        HTTPServerName            = null,
        //                  String?                        HTTPServiceName           = null,
        //                  String?                        APIVersionHash            = null,
        //                  JObject?                       APIVersionHashes          = null,

        //                  Boolean?                       DisableMaintenanceTasks   = false,
        //                  TimeSpan?                      MaintenanceInitialDelay   = null,
        //                  TimeSpan?                      MaintenanceEvery          = null,

        //                  Boolean?                       DisableWardenTasks        = false,
        //                  TimeSpan?                      WardenInitialDelay        = null,
        //                  TimeSpan?                      WardenCheckEvery          = null,

        //                  Boolean?                       IsDevelopment             = null,
        //                  IEnumerable<String>?           DevelopmentServers        = null,
        //                  Boolean?                       DisableLogging            = false,
        //                  String?                        LoggingPath               = DefaultHTTPAPI_LoggingPath,
        //                  String?                        LoggingContext            = DefaultLoggingContext,
        //                  String?                        LogfileName               = DefaultHTTPAPI_LogfileName,
        //                  LogfileCreatorDelegate?        LogfileCreator            = null)

        //    : base(HTTPServerName,
        //           HTTPServiceName,
        //           APIVersionHash ?? APIVersionHashes?[nameof(HTTPAPIX)]?.Value<String>()?.Trim(),
        //           APIVersionHashes,

        //           IsDevelopment,
        //           DevelopmentServers,
        //           DisableLogging,
        //           LoggingPath,
        //           LogfileName,
        //           LogfileCreator)

        //{

        //    this.Hostnames           = Hostnames?.       Distinct() ?? [];
        //    this.RootPath            = RootPath                     ?? HTTPPath.Root;
        //    this.HTTPContentTypes    = HTTPContentTypes?.Distinct() ?? [];
        //    this.Description         = Description                  ?? I18NString.Empty;
        //    this.HTTPServer          = HTTPServer;

        //    this.ExternalDNSName     = ExternalDNSName;
        //    this.BasePath            = BasePath;

        //    // Register HTTP API within the HTTP server!
        //    HTTPServer?.AddHTTPAPI(
        //        this.RootPath,
        //        HTTPHostname.Any,
        //        (server, path) => this
        //    );

        //    // Setup Maintenance Task
        //    this.DisableMaintenanceTasks  = DisableMaintenanceTasks ?? false;
        //    this.MaintenanceEvery         = MaintenanceEvery        ?? DefaultMaintenanceEvery;
        //    this.MaintenanceTimer         = new Timer(
        //                                        DoMaintenanceSync,
        //                                        this,
        //                                        MaintenanceInitialDelay ?? this.MaintenanceEvery,
        //                                        this.MaintenanceEvery
        //                                    );

        //    // Setup Warden
        //    this.Warden = new Warden.Warden(
        //                      WardenInitialDelay ?? TimeSpan.FromMinutes(3),
        //                      WardenCheckEvery   ?? TimeSpan.FromMinutes(1),
        //                      this.HTTPServer.DNSClient
        //                  );

        //}

        #endregion


        private readonly ConcurrentDictionary<String, PathNode> routeNodes = [];

        #region AddHandler(HTTPMethod, URLTemplate, HTTPDelegate, ...

        public void AddHandler(HTTPMethod                                 HTTPMethod,
                               HTTPPath                                   URLTemplate,
                               HTTPDelegate                               HTTPDelegate,

                               HTTPContentType?                           HTTPContentType             = null,

                               HTTPAuthentication?                        URLAuthentication           = null,
                               HTTPAuthentication?                        HTTPMethodAuthentication    = null,
                               HTTPAuthentication?                        ContentTypeAuthentication   = null,

                               OnHTTPRequestLogDelegate2?                 HTTPRequestLogger           = null,
                               OnHTTPResponseLogDelegate2?                HTTPResponseLogger          = null,

                               HTTPDelegate?                              DefaultErrorHandler         = null,
                               Dictionary<HTTPStatusCode, HTTPDelegate>?  ErrorHandlers               = null,
                               URLReplacement?                            AllowReplacement            = null)

            => AddHandler(
                   URLTemplate,
                   HTTPDelegate,

                   HTTPMethod,
                   HTTPContentType,

                   URLAuthentication,
                   HTTPMethodAuthentication,
                   ContentTypeAuthentication,

                   HTTPRequestLogger,
                   HTTPResponseLogger,

                   DefaultErrorHandler,
                   ErrorHandlers,
                   AllowReplacement
               );

        #endregion

        #region AddHandler(HTTPMethod, URLTemplate, HTTPContentType, HTTPDelegate, ...

        public void AddHandler(HTTPMethod                                 HTTPMethod,
                               HTTPPath                                   URLTemplate,
                               HTTPContentType                            HTTPContentType,
                               HTTPDelegate                               HTTPDelegate,

                               HTTPAuthentication?                        URLAuthentication           = null,
                               HTTPAuthentication?                        HTTPMethodAuthentication    = null,
                               HTTPAuthentication?                        ContentTypeAuthentication   = null,

                               OnHTTPRequestLogDelegate2?                  HTTPRequestLogger           = null,
                               OnHTTPResponseLogDelegate2?                 HTTPResponseLogger          = null,

                               HTTPDelegate?                              DefaultErrorHandler         = null,
                               Dictionary<HTTPStatusCode, HTTPDelegate>?  ErrorHandlers               = null,
                               URLReplacement?                            AllowReplacement            = null)

            => AddHandler(
                   URLTemplate,
                   HTTPDelegate,

                   HTTPMethod,
                   HTTPContentType,

                   URLAuthentication,
                   HTTPMethodAuthentication,
                   ContentTypeAuthentication,

                   HTTPRequestLogger,
                   HTTPResponseLogger,

                   DefaultErrorHandler,
                   ErrorHandlers,
                   AllowReplacement
               );

        #endregion

        #region AddHandler(HTTPDelegate, Hostname = "*", URLTemplate = "/", HTTPMethod = null, HTTPContentType = null, HostAuthentication = null, URLAuthentication = null, HTTPMethodAuthentication = null, ContentTypeAuthentication = null, DefaultErrorHandler = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="HTTPDelegate">A delegate called for each incoming HTTP request.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public void AddHandler(HTTPPath                                   URLTemplate,
                               HTTPDelegate                               HTTPDelegate,

                               HTTPMethod?                                HTTPMethod                  = null,
                               HTTPContentType?                           HTTPContentType             = null,

                               HTTPAuthentication?                        URLAuthentication           = null,
                               HTTPAuthentication?                        HTTPMethodAuthentication    = null,
                               HTTPAuthentication?                        ContentTypeAuthentication   = null,

                               OnHTTPRequestLogDelegate2?                  HTTPRequestLogger           = null,
                               OnHTTPResponseLogDelegate2?                 HTTPResponseLogger          = null,

                               HTTPDelegate?                              DefaultErrorHandler         = null,
                               Dictionary<HTTPStatusCode, HTTPDelegate>?  ErrorHandlers               = null,
                               URLReplacement?                            AllowReplacement            = null)

        {

            #region Initial Checks

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate), "The given parameter must not be null!");

            if (HTTPMethod is null && HTTPContentType is not null)
                throw new ArgumentException("If HTTPMethod is null the HTTPContentType must also be null!");

            #endregion

            var requestHandle = new HTTPRequestHandlersX(
                                    this,
                                    HTTPDelegate,
                                    HTTPRequestLogger,
                                    HTTPResponseLogger,
                                    DefaultErrorHandler,
                                    ErrorHandlers
                                );

            var segments      = URLTemplate.ToString().Trim('/').Split('/');

            if (segments[0] == "")
                segments[0] = "/";

            var routeNode1    = routeNodes.GetOrAdd(
                                    segments[0],
                                    segment => {

                                        if (segment.StartsWith('{') && segment.EndsWith('}'))
                                        {

                                            var paramName = segment[1..^1];

                                            if (segment.EndsWith("..}") && ("/" + segment) == URLTemplate.ToString())
                                                return PathNode.ForCatchRestOfPath(
                                                           "/" + segment,
                                                           paramName[..^2],
                                                           AllowReplacement: AllowReplacement
                                                       );

                                            return PathNode.ForParameter(
                                                       "/" + segment,
                                                       paramName,
                                                       AllowReplacement: AllowReplacement
                                                   );

                                        }

                                        return PathNode.FromPath(
                                                   "/" + segments[0],
                                                   HTTPPath.Root.ToString()
                                               );

                                    }
                                );

            foreach (var segment in segments.Skip(1))
            {

                var routeNode2 = routeNode1.Children.GetOrAdd(
                                     segment,
                                     segment => {

                                         if (segment.StartsWith('{') && segment.EndsWith('}'))
                                         {

                                             var paramName = segment[1..^1];

                                             if (segment.EndsWith("..}") && $"{routeNode1.FullPath}/{segment}" == URLTemplate.ToString())
                                                 return PathNode.ForCatchRestOfPath(
                                                            routeNode1.FullPath + "/" + segment,
                                                            paramName[..^2],
                                                            AllowReplacement: AllowReplacement
                                                        );

                                             //if ((routeNode1.FullPath + "/" + segment) == URLTemplate.ToString())
                                             //{
                                             //    return PathNode.ForCatchRestOfPath(
                                             //               routeNode1.FullPath + "/" + segment,
                                             //               paramName,
                                             //               AllowReplacement: AllowReplacement
                                             //           );
                                             //}

                                             return PathNode.ForParameter(
                                                        routeNode1.FullPath + "/" + segment,
                                                        paramName,
                                                        AllowReplacement: AllowReplacement
                                                    );

                                         }

                                         return PathNode.FromPath(
                                                    routeNode1.FullPath + "/" + segment,
                                                    "/" + segment
                                                );

                                     }
                                 );

                routeNode1 = routeNode2;

            }


            if (HTTPMethod is null)
                routeNode1.RequestHandlers = requestHandle;

            else
            {

                var methodNode = routeNode1.Methods.GetOrAdd(
                                     HTTPMethod,
                                     new MethodNode(
                                         HTTPMethod,
                                         AllowReplacement: AllowReplacement
                                     )
                                 );

                if (HTTPContentType is null)
                    methodNode.RequestHandlers  = requestHandle;

                else
                    methodNode.AddContentType(
                        HTTPContentType,
                        requestHandle
                    );

            }

        }

        #endregion



        internal ParsedRequest2 GetRequestHandle(HTTPPath    Path,
                                                 HTTPMethod  Method)
        {

            var parsedRequest = GetRequestHandle(Path);

            if (parsedRequest.RouteNode?.Methods.ContainsKey(Method) == true)
                return ParsedRequest2.Parsed(parsedRequest.RouteNode, parsedRequest.Parameters);

            return ParsedRequest2.Error($"Unknown method {Method}!", parsedRequest.Parameters);

        }

        internal ParsedRequest2 GetRequestHandle(HTTPPath         Path,
                                                 HTTPMethod       Method,
                                                 HTTPContentType  ContentType)
        {

            var parsedRequest = GetRequestHandle(Path);

            if (parsedRequest.ErrorResponse is not null)
                return parsedRequest;

            if (parsedRequest.RouteNode?.Methods.TryGetValue(Method, out var methodNode) == true)
            {

                if (methodNode.ContentTypes.Any(a => a == ContentType))
                    return ParsedRequest2.Parsed(parsedRequest.RouteNode, parsedRequest.Parameters);

                return ParsedRequest2.Error($"Unknown content type {ContentType}!", parsedRequest.Parameters);

            }

            return ParsedRequest2.Error($"Unknown method {Method}!", parsedRequest.Parameters);

        }


        #region (internal) GetRequestHandle(Path)

        internal ParsedRequest2 GetRequestHandle(HTTPPath  Path)
        {

            var segments    = Path.ToString().Trim('/').Split('/');
            var parameters  = new Dictionary<String, String>();

            var pathSegment = segments[0].Trim();
            if (pathSegment.IsNullOrEmpty())
                pathSegment = "/";

            if (!routeNodes.TryGetValue(pathSegment, out var routeNode))
            {

                var parameterCatcher = routeNodes.Values.FirstOrDefault(routeNode => routeNode.ParameterName is not null);
                if (parameterCatcher is not null && parameterCatcher.ParameterName is not null)
                {

                    parameters.Add(
                        parameterCatcher.ParameterName,
                        parameterCatcher.CatchRestOfPath2
                            ? Path.ToString().TrimStart('/')
                            : pathSegment
                    );

                    if (parameterCatcher.CatchRestOfPath2)
                        return ParsedRequest2.Parsed(parameterCatcher, parameters);

                }
                else
                    return ParsedRequest2.Error(
                               $"Unknown path '{Path}'!",
                               parameters
                           );

            }
            else
            {
                if (routeNode.ParameterName is not null)
                {
                    parameters.Add(
                        routeNode.ParameterName,
                        routeNode.CatchRestOfPath2
                            ? segments.Skip(1).AggregateWith('/')
                            : segments[0]
                    );
                }
            }

            if (segments.Length > 1)
            {

                for (var i = 1; i < segments.Length; i++)
                {

                    if (!routeNode.Children.TryGetValue(segments[i], out var routeNode2))
                    {

                        var parameterCatcher = routeNode.Children.Values.FirstOrDefault(routeNode => routeNode.ParameterName is not null);
                        if (parameterCatcher is not null && parameterCatcher.ParameterName is not null)
                        {

                            if (parameterCatcher.CatchRestOfPath2 && i <= segments.Length-1)
                            {

                                parameters.Add(
                                    parameterCatcher.ParameterName,
                                    parameterCatcher.CatchRestOfPath2 && i <= segments.Length-1
                                        ? segments.Skip(i).AggregateWith('/')
                                        : segments[i]
                                );

                                return ParsedRequest2.Parsed(
                                           parameterCatcher,
                                           parameters
                                       );

                            }

                            parameters.Add(
                                parameterCatcher.ParameterName,
                                segments[i]
                            );

                        }
                        else
                            goto Error;
                            //return ParsedRequest2.Error(
                            //           $"Unknown path {Path}!",
                            //           parameters
                            //       );

                        routeNode = parameterCatcher;

                    }

                    else
                        routeNode = routeNode2;

                }

                if (routeNode.FullPath.EndsWith(routeNode.Path))
                    return ParsedRequest2.Parsed(
                               routeNode,
                               parameters
                           );

            }

            if (routeNode is not null)
                return ParsedRequest2.Parsed(
                    routeNode,
                    parameters
                );

Error:

            var parameterCatchers = routeNodes.Where(routeNode => routeNode.Value.ParameterName is not null &&
                                                                  routeNode.Value.CatchRestOfPath2).
                                               ToArray();

            if (parameterCatchers.Length > 0)
            {

                var parameterCatcher = parameterCatchers.First().Value;

                parameters.Add(
                    parameterCatcher.ParameterName!,
                    Path.ToString().Trim('/')
                );

                return ParsedRequest2.Parsed(parameterCatcher, parameters);

            }

            return ParsedRequest2.Error(
                       $"Unknown path {Path}!",
                       parameters
                   );

        }

        #endregion


        #region (protected virtual) GetResourceStream             (ResourceName, ResourceAssemblies)

        protected virtual Stream? GetResourceStream(String ResourceName)

            => GetResourceStream(ResourceName,
                                 new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual Stream? GetResourceStream(String                            ResourceName,
                                                    params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            foreach (var resourceAssembly in ResourceAssemblies)
            {
                try
                {

                    var resourceStream = resourceAssembly.Item2.GetManifestResourceStream(resourceAssembly.Item1 + ResourceName);

                    if (resourceStream is not null)
                        return resourceStream;

                }
                catch
                { }
            }

            return null;

        }

        #endregion

        #region (protected virtual) GetResourceMemoryStream       (ResourceName, ResourceAssemblies)

        protected virtual MemoryStream? GetResourceMemoryStream(String ResourceName)

            => GetResourceMemoryStream(ResourceName,
                                       new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual MemoryStream? GetResourceMemoryStream(String                            ResourceName,
                                                                params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            try
            {

                var resourceStream = GetResourceStream(
                                         ResourceName,
                                         ResourceAssemblies
                                     );

                if (resourceStream is not null)
                {

                    var outputStream = new MemoryStream();
                    resourceStream.CopyTo(outputStream);
                    outputStream.Seek(0, SeekOrigin.Begin);

                    return outputStream;

                }

            }
            catch
            { }

            return null;

        }

        #endregion

        #region (protected virtual) GetResourceString             (ResourceName, ResourceAssemblies)

        protected virtual String GetResourceString(String ResourceName)

            => GetResourceString(ResourceName,
                                 new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String GetResourceString(String                            ResourceName,
                                                   params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToUTF8String() ?? String.Empty;

        #endregion

        #region (protected virtual) GetResourceBytes              (ResourceName, ResourceAssemblies)

        protected virtual Byte[] GetResourceBytes(String ResourceName)

            => GetResourceBytes(ResourceName,
                                new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual Byte[] GetResourceBytes(String                            ResourceName,
                                                  params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToArray() ?? [];

        #endregion


        #region (protected virtual) GetMergedResourceMemoryStream (ResourceName, ResourceAssemblies)

        protected virtual MemoryStream? GetMergedResourceMemoryStream(String ResourceName)

            => GetMergedResourceMemoryStream(ResourceName,
                                             new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual MemoryStream? GetMergedResourceMemoryStream(String                            ResourceName,
                                                                      params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            try
            {

                var outputStream = new MemoryStream();
                var newLine      = "\r\n"u8.ToArray();

                foreach (var resourceAssembly in ResourceAssemblies)
                {
                    try
                    {

                        var data = resourceAssembly.Item2.GetManifestResourceStream(resourceAssembly.Item1 + ResourceName);
                        if (data is not null)
                        {

                            data.CopyTo(outputStream);

                            outputStream.Write(newLine, 0, newLine.Length);

                        }

                    }
                    catch
                    { }
                }

                outputStream.Seek(0, SeekOrigin.Begin);

                return outputStream;

            }
            catch
            { }

            return null;

        }

        #endregion

        #region (protected virtual) GetMergedResourceString       (ResourceName, ResourceAssemblies)

        protected virtual String GetMergedResourceString(String ResourceName)

            => GetMergedResourceString(ResourceName,
                                       new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String GetMergedResourceString(String                            ResourceName,
                                                         params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetMergedResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToUTF8String() ?? String.Empty;

        #endregion

        #region (protected virtual) GetMergedResourceBytes        (ResourceName, ResourceAssemblies)

        protected virtual Byte[] GetMergedResourceBytes(String ResourceName)

            => GetMergedResourceBytes(ResourceName,
                                      new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual Byte[] GetMergedResourceBytes(String                            ResourceName,
                                                        params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetMergedResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToArray() ?? [];

        #endregion


        #region (Timer) DoMaintenance(State)

        private void DoMaintenanceSync(Object? State)
        {
            if (ReloadFinished && !DisableMaintenanceTasks)
                DoMaintenanceAsync(State).ConfigureAwait(false);
        }

        protected internal virtual Task DoMaintenance(Object? State)
            => Task.CompletedTask;

        private async Task DoMaintenanceAsync(Object? State)
        {

            if (await MaintenanceSemaphore.WaitAsync(SemaphoreSlimTimeout).
                                           ConfigureAwait(false))
            {
                try
                {

                    await DoMaintenance(State);

                }
                catch (Exception e)
                {

                    while (e.InnerException is not null)
                        e = e.InnerException;

                    DebugX.LogException(e);

                }
                finally
                {
                    MaintenanceSemaphore.Release();
                }
            }
            else
                DebugX.LogT("Could not aquire the maintenance tasks lock!");

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   Hostnames.Any()
                       ? $" ({Hostnames.AggregateCSV()})"
                       : String.Empty,

                   RootPath,

                   HTTPContentTypes.Any()
                       ? $" ({HTTPContentTypes.AggregateCSV()})"
                       : String.Empty

               );

        #endregion

    }

}
