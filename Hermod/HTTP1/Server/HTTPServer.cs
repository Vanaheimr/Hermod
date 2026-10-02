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
using System.Text.RegularExpressions;
using System.Security.Authentication;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate Task OnHTTPRequestLogDelegate  (HTTPServer         HTTPServer,
                                                    HTTPRequest        Request,
                                                    CancellationToken  CancellationToken);

    public delegate Task OnHTTPResponseLogDelegate (HTTPServer         HTTPServer,
                                                    HTTPRequest        Request,
                                                    HTTPResponse       Response,
                                                    CancellationToken  CancellationToken);

    public delegate Task OnHTTPRequestLogDelegate2 (DateTimeOffset     Timestamp,
                                                    HTTPAPI            API,
                                                    HTTPRequest        Request,
                                                    CancellationToken  CancellationToken);

    public delegate Task OnHTTPResponseLogDelegate2(DateTimeOffset     Timestamp,
                                                    HTTPAPI            API,
                                                    HTTPRequest        Request,
                                                    HTTPResponse       Response,
                                                    CancellationToken  CancellationToken);


    /// <summary>
    /// An HTTP server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining:
    /// it runs its pipelines on each request, then the handler of its HTTP APIs that matches the request.
    /// </summary>
    public class HTTPServer : AHTTPServer
    {

        #region Data

        private readonly ConcurrentDictionary<HTTPHostname,       HTTPAPINode>       routeNodes      = [];
        private readonly List<AHTTPPipeline>                                         httpPipelines   = [];
        private volatile Boolean                                                     includeStackTracesInErrorResponses;

        /// <summary>
        /// Command-line JSON HTTP clients, libraries, and API tools...
        /// </summary>
        private static readonly Regex JSONUserAgents  = new (
                                                            @"\b(curl|wget|httpie|PostmanRuntime|python-requests|Go-http-client|libcurl|restsharp|okhttp|Apache-HttpClient|Insomnia|Java/\d)\b",
                                                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
                                                        );

        #endregion

        #region Properties

        /// <summary>
        /// The default HTTP API at '/'.
        /// </summary>
        public HTTPAPI DefaultAPI

            => routeNodes[HTTPHostname.Any]?.Children["/"]?.HTTPAPI
                   ?? throw new InvalidOperationException("The main API '/' is not registered!");

        /// <summary>
        /// Whether internal exception details and stack traces are included in
        /// HTTP error responses. Disabled by default.
        /// </summary>
        public Boolean IncludeStackTracesInErrorResponses
        {
            get => includeStackTracesInErrorResponses;
            set => includeStackTracesInErrorResponses = value;
        }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever an HTTP request was received.
        /// </summary>
        public event OnHTTPRequestLogDelegate?   OnHTTPRequest;

        /// <summary>
        /// An event fired whenever an HTTP response was sent.
        /// </summary>
        public event OnHTTPResponseLogDelegate?  OnHTTPResponse;

        /// <summary>
        /// An event fired whenever an HTTP error response was sent.
        /// </summary>
        public event OnHTTPResponseLogDelegate?  OnHTTPError;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP server that listens on the given IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, both loopback addresses, [::1] and 127.0.0.1.</param>
        /// <param name="TCPPort">The TCP port to listen on. If null or 0, the operating system chooses one, and the TCPPort property tells which.</param>
        /// <param name="HTTPServerName">An optional HTTP server name. If null or empty, the default HTTP server name will be used.</param>
        /// <param name="Description">An optional multilingual description of this HTTP server.</param>
        ///
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size of 32 KByte will be used. An HTTP request header section has to fit into it.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout of 30 seconds will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout of 30 seconds will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        ///
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate for each accepted connection. Without a certificate, a connection does without TLS.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication. Where it is given, the TLS handshake asks the client for a certificate.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the local TLS certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocols to allow. Kept in AllowedTLSProtocols, but not read yet: the TLS handshake allows TLS 1.2 and 1.3.</param>
        /// <param name="ClientCertificateRequired">Whether TLS client certification is required. Kept in ClientCertificateRequired, but not read yet: the TLS handshake asks for a client certificate where a ClientCertificateValidator is given.</param>
        /// <param name="CheckCertificateRevocation">Whether TLS client certificate revocation should be verified. Kept in CheckCertificateRevocation, but not read yet: the TLS handshake checks no revocation.</param>
        ///
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information. If null, the default connection identification will be used.</param>
        /// <param name="MaxClientConnections">An optional maximum number of concurrent TCP client connections. If null, the default maximum of 8192 concurrent TCP client connections will be used.</param>
        /// <param name="DNSClient">An optional DNS client for the warden and other DNS lookups. If null, the server makes one of its own.</param>
        ///
        /// <param name="DisableMaintenanceTasks">Whether to disable all maintenance tasks.</param>
        /// <param name="MaintenanceInitialDelay">The initial delay of the maintenance tasks.</param>
        /// <param name="MaintenanceEvery">The maintenance interval.</param>
        ///
        /// <param name="DisableWardenTasks">Whether to disable all warden tasks. Kept in DisableWardenTasks, but not read yet: the warden runs either way.</param>
        /// <param name="WardenInitialDelay">The initial delay of the warden tasks.</param>
        /// <param name="WardenCheckEvery">The warden check interval.</param>
        ///
        /// <param name="DefaultAPI">An optional default HTTP API at '/', for any HTTP hostname.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        /// <param name="AutoStart">Whether to automatically start the HTTP server.</param>
        /// <param name="MaxHTTPBodySize">The maximum request-body size accepted by this HTTP server, in bytes, up to Int32.MaxValue. If null, 8 MByte.</param>
        /// <param name="MaxHTTPHeaderSize">The maximum size of an HTTP request header section, in bytes, up to BufferSize. If null, 32 KByte, or BufferSize if that is smaller.</param>
        /// <param name="MaxHTTPHeaderLineLength">The maximum length of an HTTP request header field line, in bytes, up to MaxHTTPHeaderSize. If null, 8 KByte, or MaxHTTPHeaderSize if that is smaller.</param>
        /// <param name="MaxHTTPRequestTargetLength">The maximum length of an HTTP request-target, in bytes, up to MaxHTTPHeaderLineLength. If null, 8 KByte, or MaxHTTPHeaderLineLength if that is smaller.</param>
        /// <param name="MaxHTTPHeaderCount">The maximum number of header fields of an HTTP request. If null, 100.</param>
        /// <param name="MaxHTTPChunkSizeLineLength">The maximum length of a chunk-size line of a chunked HTTP request body, chunk extensions included. If null, 8 KByte.</param>
        /// <param name="MaxHTTPChunkTrailerLineLength">The maximum length of a trailer field line of a chunked HTTP request body. If null, 8 KByte.</param>
        /// <param name="MaxHTTPChunkTrailerCount">The maximum number of trailer fields of a chunked HTTP request body. If null, 100.</param>
        /// <param name="MaxHTTPChunkTrailerSize">The maximum size of the trailer section of a chunked HTTP request body, in bytes. If null, 32 KByte.</param>
        /// <param name="MaxHTTPChunkMetadataSize">The maximum size of all chunk-size lines and the trailer section of a chunked HTTP request body together, in bytes. If null, 64 KByte.</param>
        /// <param name="HeaderReadTimeout">The maximum time to wait for one complete HTTP request header section. It starts when the server starts waiting for the request - on a new connection, or once the previous response on a kept-alive connection has been sent - so it bounds the keep-alive idle wait and the header section together, and it does not start again with every read. If null, ReceiveTimeout.</param>
        /// <param name="BodyReadTimeout">The maximum time allowed to receive one complete HTTP request body. If null, ReceiveTimeout.</param>
        /// <param name="IncludeStackTracesInErrorResponses">Whether internal exception details and stack traces are included in HTTP error responses.</param>
        public HTTPServer(IIPAddress?                                               IPAddress                    = null,
                          IPPort?                                                   TCPPort                      = null,
                          String?                                                   HTTPServerName               = null,
                          I18NString?                                               Description                  = null,

                          UInt32?                                                   BufferSize                   = null,
                          TimeSpan?                                                 ReceiveTimeout               = null,
                          TimeSpan?                                                 SendTimeout                  = null,
                          TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                          ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                          RemoteTLSClientCertificateValidationHandler<HTTPServer>?  ClientCertificateValidator   = null,
                          LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                          SslProtocols?                                             AllowedTLSProtocols          = null,
                          Boolean?                                                  ClientCertificateRequired    = null,
                          Boolean?                                                  CheckCertificateRevocation   = null,

                          ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                          UInt32?                                                   MaxClientConnections         = null,
                          IDNSClient?                                               DNSClient                    = null,

                          Boolean?                                                  DisableMaintenanceTasks      = false,
                          TimeSpan?                                                 MaintenanceInitialDelay      = null,
                          TimeSpan?                                                 MaintenanceEvery             = null,

                          Boolean?                                                  DisableWardenTasks           = false,
                          TimeSpan?                                                 WardenInitialDelay           = null,
                          TimeSpan?                                                 WardenCheckEvery             = null,

                          HTTPAPI?                                                  DefaultAPI                   = null,
                          ILoggerFactory?                                           LoggerFactory                = null,
                          Boolean?                                                  AutoStart                    = false,
                          UInt64?                                                   MaxHTTPBodySize              = null,
                          UInt32?                                                   MaxHTTPHeaderSize            = null,
                          UInt32?                                                   MaxHTTPHeaderLineLength      = null,
                          UInt32?                                                   MaxHTTPRequestTargetLength   = null,
                          UInt32?                                                   MaxHTTPHeaderCount           = null,
                          UInt32?                                                   MaxHTTPChunkSizeLineLength   = null,
                          UInt32?                                                   MaxHTTPChunkTrailerLineLength = null,
                          UInt32?                                                   MaxHTTPChunkTrailerCount     = null,
                          UInt32?                                                   MaxHTTPChunkTrailerSize      = null,
                          UInt32?                                                   MaxHTTPChunkMetadataSize     = null,
                          TimeSpan?                                                 HeaderReadTimeout            = null,
                          TimeSpan?                                                 BodyReadTimeout              = null,
                          Boolean                                                   IncludeStackTracesInErrorResponses = false)

            : base(IPAddress,
                   TCPPort,
                   HTTPServerName,
                   Description,

                   BufferSize,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler,

                   ServerCertificateSelector,
                   ClientCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsServer,
                          policyErrors) => ClientCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               tlsServer as HTTPServer,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ConnectionIdBuilder,
                   MaxClientConnections,
                   DNSClient,

                   DisableMaintenanceTasks,
                   MaintenanceInitialDelay,
                   MaintenanceEvery,

                   DisableWardenTasks,
                   WardenInitialDelay,
                   WardenCheckEvery,

                    LoggerFactory,
                    AutoStart:         false,
                    MaxHTTPBodySize:              MaxHTTPBodySize,
                    MaxHTTPHeaderSize:            MaxHTTPHeaderSize,
                    MaxHTTPHeaderLineLength:      MaxHTTPHeaderLineLength,
                    MaxHTTPRequestTargetLength:   MaxHTTPRequestTargetLength,
                    MaxHTTPHeaderCount:           MaxHTTPHeaderCount,
                    MaxHTTPChunkSizeLineLength:   MaxHTTPChunkSizeLineLength,
                    MaxHTTPChunkTrailerLineLength: MaxHTTPChunkTrailerLineLength,
                    MaxHTTPChunkTrailerCount:     MaxHTTPChunkTrailerCount,
                    MaxHTTPChunkTrailerSize:      MaxHTTPChunkTrailerSize,
                    MaxHTTPChunkMetadataSize:     MaxHTTPChunkMetadataSize,
                    HeaderReadTimeout:            HeaderReadTimeout,
                    BodyReadTimeout:              BodyReadTimeout)

        {

            this.IncludeStackTracesInErrorResponses = IncludeStackTracesInErrorResponses;

            if (DefaultAPI is not null)
            {

                var defaultHost = routeNodes.AddAndReturnValue(
                                      HTTPHostname.Any,
                                      new HTTPAPINode(
                                          HTTPHostname.Any. ToString(),
                                          HTTPPath.    Root.ToString()
                                      )
                                  );

                defaultHost.Children.GetOrAdd(
                    "/",
                    pathSegment => {

                        return new HTTPAPINode(
                            defaultHost.FullPath + "/" + pathSegment,
                            pathSegment,
                            DefaultAPI
                        );

                    }
                );

                DefaultAPI.HTTPServer = this;

            }

            if (AutoStart ?? false)
                Start().GetAwaiter().GetResult();

        }

        #endregion


        private const String InternalServerErrorDescription = "An internal server error occurred.";

        private String GetExceptionDescriptionForResponse(Exception Exception)
        {

            var includeExceptionDetails = IncludeStackTracesInErrorResponses;

            return includeExceptionDetails
                       ? Exception.Message + Environment.NewLine + Exception.StackTrace
                       : InternalServerErrorDescription;

        }

        private JObject CreateInternalServerErrorJSON(HTTPRequest? Request,
                                                       Exception    Exception)
        {

            var includeExceptionDetails = IncludeStackTracesInErrorResponses;

            var json = JSONObject.Create(
                           new JProperty("request",          Request?.FirstPDULine               ?? "null"),
                           new JProperty("eventTrackingId",  Request?.EventTrackingId.ToString() ?? "null"),
                           new JProperty("description",      includeExceptionDetails
                                                                   ? Exception.Message
                                                                   : InternalServerErrorDescription)
                       );

            if (includeExceptionDetails)
            {
                json.Add(new JProperty("stackTrace", Exception.StackTrace));
                json.Add(new JProperty("source",     Exception.TargetSite?.Module.Name));
                json.Add(new JProperty("type",       Exception.TargetSite?.ReflectedType?.Name));
            }

            return json;

        }


        #region (static) StartNew(...)

        public static async Task<HTTPServer>

            StartNew(IIPAddress?                                               IPAddress                    = null,
                     IPPort?                                                   TCPPort                      = null,
                     String?                                                   HTTPServerName               = null,
                     I18NString?                                               Description                  = null,

                     UInt32?                                                   BufferSize                   = null,
                     TimeSpan?                                                 ReceiveTimeout               = null,
                     TimeSpan?                                                 SendTimeout                  = null,
                     TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                     ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                     RemoteTLSClientCertificateValidationHandler<HTTPServer>?  ClientCertificateValidator   = null,
                     LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                     SslProtocols?                                             AllowedTLSProtocols          = null,
                     Boolean?                                                  ClientCertificateRequired    = null,
                     Boolean?                                                  CheckCertificateRevocation   = null,

                     ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                     UInt32?                                                   MaxClientConnections         = null,
                     IDNSClient?                                               DNSClient                    = null,

                     Boolean?                                                  DisableMaintenanceTasks      = false,
                     TimeSpan?                                                 MaintenanceInitialDelay      = null,
                     TimeSpan?                                                 MaintenanceEvery             = null,

                     Boolean?                                                  DisableWardenTasks           = false,
                     TimeSpan?                                                 WardenInitialDelay           = null,
                     TimeSpan?                                                 WardenCheckEvery             = null,

                     HTTPAPI?                                                  DefaultAPI                   = null,
                      ILoggerFactory?                                           LoggerFactory                = null,
                      UInt64?                                                   MaxHTTPBodySize              = null,
                      UInt32?                                                   MaxHTTPHeaderSize            = null,
                      UInt32?                                                   MaxHTTPHeaderLineLength      = null,
                      UInt32?                                                   MaxHTTPRequestTargetLength   = null,
                      UInt32?                                                   MaxHTTPHeaderCount           = null,
                      UInt32?                                                   MaxHTTPChunkSizeLineLength   = null,
                      UInt32?                                                   MaxHTTPChunkTrailerLineLength = null,
                      UInt32?                                                   MaxHTTPChunkTrailerCount     = null,
                      UInt32?                                                   MaxHTTPChunkTrailerSize      = null,
                      UInt32?                                                   MaxHTTPChunkMetadataSize     = null,
                      TimeSpan?                                                 HeaderReadTimeout            = null,
                       TimeSpan?                                                 BodyReadTimeout              = null,
                       Boolean                                                   IncludeStackTracesInErrorResponses = false)

        {

            var server = new HTTPServer(

                             IPAddress,
                             TCPPort,
                             HTTPServerName,
                             Description,

                             BufferSize,
                             ReceiveTimeout,
                             SendTimeout,
                             LoggingHandler,

                             ServerCertificateSelector,
                             ClientCertificateValidator,
                             LocalCertificateSelector,
                             AllowedTLSProtocols,
                             ClientCertificateRequired,
                             CheckCertificateRevocation,

                             ConnectionIdBuilder,
                             MaxClientConnections,
                             DNSClient,

                             DisableMaintenanceTasks,
                             MaintenanceInitialDelay,
                             MaintenanceEvery,

                             DisableWardenTasks,
                             WardenInitialDelay,
                             WardenCheckEvery,

                             DefaultAPI,
                             LoggerFactory,

                              AutoStart:         false,
                               MaxHTTPBodySize:              MaxHTTPBodySize,
                               MaxHTTPHeaderSize:            MaxHTTPHeaderSize,
                               MaxHTTPHeaderLineLength:      MaxHTTPHeaderLineLength,
                               MaxHTTPRequestTargetLength:   MaxHTTPRequestTargetLength,
                               MaxHTTPHeaderCount:           MaxHTTPHeaderCount,
                               MaxHTTPChunkSizeLineLength:   MaxHTTPChunkSizeLineLength,
                               MaxHTTPChunkTrailerLineLength: MaxHTTPChunkTrailerLineLength,
                               MaxHTTPChunkTrailerCount:     MaxHTTPChunkTrailerCount,
                               MaxHTTPChunkTrailerSize:      MaxHTTPChunkTrailerSize,
                               MaxHTTPChunkMetadataSize:     MaxHTTPChunkMetadataSize,
                               HeaderReadTimeout:            HeaderReadTimeout,
                               BodyReadTimeout:              BodyReadTimeout,
                               IncludeStackTracesInErrorResponses: IncludeStackTracesInErrorResponses

                         );

            await server.Start();

            return server;

        }

        #endregion


        #region HTTP Pipelines

        public void AddPipeline(AHTTPPipeline Pipeline)
        {
            httpPipelines.Add(Pipeline);
        }

        #endregion


        #region AddHTTPAPI(Path = null, Hostname = null, HTTPAPICreator = null, HTTPAPIConfigurator = null)

        public HTTPAPI AddHTTPAPI(HTTPPath?                             Path                  = null,
                                  HTTPHostname?                         Hostname              = null,
                                  Func<HTTPServer, HTTPPath, HTTPAPI>?  HTTPAPICreator        = null,
                                  Action<HTTPAPI>?                      HTTPAPIConfigurator   = null)
        {

            var path        = Path                               ?? HTTPPath.Root;
            var hostname    = Hostname                           ?? HTTPHostname.Any;
            var httpAPI     = HTTPAPICreator?.Invoke(this, path) ?? new HTTPAPI(
                                                                        this,
                                                                        RootPath:                  path,
                                                                        // ...as this method will do the registration itself!
                                                                        RegisterWithinHTTPServer:  false
                                                                    );
            HTTPAPIConfigurator?.Invoke(httpAPI);


            var routeNode1  = routeNodes.GetOrAdd(
                                  hostname,
                                  hh => new HTTPAPINode(
                                            hostname.ToString(),
                                            HTTPPath.Root.ToString()
                                        )
                              );

            if (path == HTTPPath.Root)
            {

                var rootNode = routeNode1.Children.GetOrAdd(
                                   "/",
                                   pathSegment => new HTTPAPINode(
                                                      routeNode1.FullPath + "/" + pathSegment,
                                                      pathSegment
                                                  )
                               );

                // The node might have been created earlier as an intermediate node
                // of a more specific HTTP API path, e.g. '/webapi'!
                if (rootNode.HTTPAPI is not null)
                    throw new ArgumentException($"An HTTP API at '{path}' is already registered!", nameof(Path));

                rootNode.HTTPAPI = httpAPI;

            }

            else
            {

                var segments = ("/" + path.ToString().Trim('/')).Split('/');

                if (segments[0] == "")
                    segments[0] = "/";

                for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                {

                    var segment = segments[segmentIndex];

                    var routeNode2 = routeNode1.Children.GetOrAdd(
                                         segment,
                                         pathSegment => new HTTPAPINode(
                                                            routeNode1.FullPath + "/" + pathSegment,
                                                            "/" + pathSegment
                                                        )
                                     );

                    if (segmentIndex == segments.Length - 1)
                    {

                        // Only a node that already dispatches to another HTTP API
                        // is a duplicate. A parent node having an HTTP API is fine:
                        // The request dispatcher prefers the most specific API path!
                        if (routeNode2.HTTPAPI is not null)
                            throw new ArgumentException($"An HTTP API at '{path}' is already registered!", nameof(Path));

                        routeNode2.HTTPAPI = httpAPI;

                    }

                    routeNode1 = routeNode2;

                }
            }

            return httpAPI;

        }

        #endregion

        #region GetHTTPAPI(Path, out HTTPAPI, Hostname = null)

        public Boolean TryGetHTTPAPI(HTTPPath                          Path,
                                     [NotNullWhen(true)] out HTTPAPI?  HTTPAPI,
                                     HTTPHostname?                     Hostname   = null)
        {

            var h = Hostname ?? HTTPHostname.Any;

            HTTPAPI = null;

            if (!routeNodes.TryGetValue(h, out var routeNode1))
                return false;

            if (Path == HTTPPath.Root)
            {

                if (routeNode1.Children.TryGetValue("/", out var rootNode) &&
                    rootNode.HTTPAPI is not null)
                {
                    HTTPAPI = rootNode.HTTPAPI;
                    return true;
                }

                return false;

            }

            var segments = ("/" + Path.ToString().Trim('/')).Split('/');
            if (segments[0] == "")
                segments[0] = "/";

            for (var i = 0; i < segments.Length; i++)
            {

                if (!routeNode1.Children.TryGetValue(segments[i], out var routeNode2))
                    return false;

                if (i == segments.Length - 1 && routeNode2.HTTPAPI is not null)
                {
                    HTTPAPI = routeNode2.HTTPAPI;
                    return true;
                }

                routeNode1 = routeNode2;

            }

            return false;

        }

        #endregion


        //private void FindMatch(String Path, ref List<String> Matches)
        //{
        //    foreach (var child in routeNodes.Values)
        //    {
        //        if (Path.StartsWith(child.Path))
        //        {
        //            Matches.Add(child.Path);
        //            //child.FindMatch(Path[(child.Path.Length - 1)..], ref Matches);
        //        }
        //    }
        //}

        #region (internal) GetRequestHandle(Request)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        internal ParsedRequest GetRequestHandle(HTTPRequest Request)
        {

            ArgumentNullException.ThrowIfNull(Request);

            try
            {

                // Not HTTPHostname.Parse(Request.Host.ToString()): an HTTP/1.0 request
                // is allowed to arrive without a Host header, and then the header field
                // is simply absent -- parsing its empty text representation throws and
                // turns a legal request into a 500, while the IsNullOrEmpty check below
                // already maps exactly that case onto the 'any' host.
                var requestHost = Request.Host;

                if (requestHost.IsNullOrEmpty)
                    requestHost = HTTPHostname.Any;

                if (!routeNodes.TryGetValue(requestHost,      out var httpAPINode) &&
                    !routeNodes.TryGetValue(HTTPHostname.Any, out     httpAPINode))
                {
                    return ParsedRequest.Error($"Unknown hostname '{requestHost}'!");
                }

                var segments = ("/" + Request.Path.ToString().Trim('/')).Split('/');

                if (segments[0] == "")
                    segments[0] = "/";


                for (var i=0; i < segments.Length; i++)
                {

                    if (!httpAPINode.Children.TryGetValue(segments[i], out var httpAPINode2))
                    {
                        if (!httpAPINode.Children.TryGetValue("/", out httpAPINode2))
                        {
                            return ParsedRequest.Error(
                                       HTTPStatusCode.NotFound,
                                       $"Unknown path segment!"
                                   );
                        }
                    }

                    httpAPINode = httpAPINode2;

                    // A host node may already contain an API while also being
                    // the parent of a more specific API path. Defer dispatch
                    // to the parent API until no deeper API route matches.
                    var hasMoreSpecificAPI = i + 1 < segments.Length &&
                                             httpAPINode.Children.ContainsKey(segments[i + 1]);

                    if (httpAPINode.HTTPAPI is not null &&
                        !hasMoreSpecificAPI)
                    {

                        // Build the API-relative path from the original request path.
                        // Joining the already slash-prefixed server segments would
                        // produce a doubled leading slash (for example "//test1.txt").
                        // The root path may have been given with or without a trailing
                        // slash ("/api/" or "/api"); both must strip the same prefix.
                        var requestPath      = Request.Path.ToString();
                        var rootPath         = httpAPINode.HTTPAPI.RootPath.ToString().TrimEnd('/');
                        var relativePath     = rootPath.Length == 0
                                                    ? requestPath
                                                    : requestPath[rootPath.Length..];

                        if (relativePath.Length == 0)
                            relativePath = "/";
                        var newPath          = HTTPPath.Parse(relativePath);
                        var parsedRouteNode  = httpAPINode.HTTPAPI.GetRequestHandle(newPath);

                        if (parsedRouteNode.RouteNode is not null)
                        {

                            if (!parsedRouteNode.RouteNode.Methods.TryGetValue(Request.HTTPMethod, out var methodNode))
                                return ParsedRequest.Error(
                                           HTTPStatusCode.MethodNotAllowed,
                                           "Method not allowed!",
                                           parsedRouteNode.RouteNode.Methods.Keys
                                       );

                            if (!methodNode.ContentTypes.Any())
                                return ParsedRequest.Parsed(
                                           methodNode.RequestHandlers,
                                           parsedRouteNode.Parameters
                                       );

                            //var bestMatchingContentType = Request.Accept.BestMatchingContentType([.. methodNode.ContentTypes]);

                            //if (bestMatchingContentType != HTTPContentType.ALL &&
                            //    methodNode.TryGetContentType(bestMatchingContentType, out var contentTypeNode))
                            //{
                            //    return ParsedRequest.Parsed(
                            //               contentTypeNode,
                            //               parsedRouteNode.Parameters
                            //           );
                            //}

                            if (methodNode.TryGetContentType(
                                    Request.Accept.BestMatchingContentType([.. methodNode.ContentTypes]),
                                    out var contentTypeNode
                                ))
                            {
                                return ParsedRequest.Parsed(
                                           contentTypeNode,
                                           parsedRouteNode.Parameters
                                       );
                            }

                            if (methodNode.ContentTypes.Count() == 1)
                                return ParsedRequest.Parsed(
                                           methodNode.HTTPRequestHandlers.First(),
                                           parsedRouteNode.Parameters
                                       );

                            #region HTML + JSON

                            if (methodNode.ContentTypes.Contains(HTTPContentType.Text.       HTML_UTF8) &&
                                methodNode.ContentTypes.Contains(HTTPContentType.Application.JSON_UTF8))
                            {

                                if (JSONUserAgents.IsMatch(Request.UserAgent ?? "") &&
                                    methodNode.TryGetContentType(HTTPContentType.Application.JSON_UTF8, out contentTypeNode))
                                {
                                    return ParsedRequest.Parsed(
                                               contentTypeNode,
                                               parsedRouteNode.Parameters
                                           );
                                }

                                else if (methodNode.TryGetContentType(HTTPContentType.Text. HTML_UTF8, out contentTypeNode))
                                    return ParsedRequest.Parsed(
                                               contentTypeNode,
                                               parsedRouteNode.Parameters
                                           );

                            }

                            #endregion

                            #region HTML + application/xml

                            if (methodNode.ContentTypes.Contains(HTTPContentType.Text.       HTML_UTF8) &&
                                methodNode.ContentTypes.Contains(HTTPContentType.Application.XML_UTF8))
                            {

                                if (JSONUserAgents.IsMatch(Request.UserAgent ?? "") &&
                                    methodNode.TryGetContentType(HTTPContentType.Application.XML_UTF8, out contentTypeNode))
                                {
                                    return ParsedRequest.Parsed(
                                               contentTypeNode,
                                               parsedRouteNode.Parameters
                                           );
                                }

                                else if (methodNode.TryGetContentType(HTTPContentType.Text. HTML_UTF8, out contentTypeNode))
                                    return ParsedRequest.Parsed(
                                               contentTypeNode,
                                               parsedRouteNode.Parameters
                                           );

                            }

                            #endregion

                            #region HTML + text/xml

                            if (methodNode.ContentTypes.Contains(HTTPContentType.Text.HTML_UTF8) &&
                                methodNode.ContentTypes.Contains(HTTPContentType.Text.XML_UTF8))
                            {

                                if (JSONUserAgents.IsMatch(Request.UserAgent ?? "") &&
                                    methodNode.TryGetContentType(HTTPContentType.Text.XML_UTF8, out contentTypeNode))
                                {
                                    return ParsedRequest.Parsed(
                                               contentTypeNode,
                                               parsedRouteNode.Parameters
                                           );
                                }

                                else if (methodNode.TryGetContentType(HTTPContentType.Text. HTML_UTF8, out contentTypeNode))
                                    return ParsedRequest.Parsed(
                                               contentTypeNode,
                                               parsedRouteNode.Parameters
                                           );

                            }

                            #endregion

                            #region HTML + application/soap+xml

                            if (methodNode.ContentTypes.Contains(HTTPContentType.Text.       HTML_UTF8) &&
                                methodNode.ContentTypes.Contains(HTTPContentType.Application.SOAPXML_UTF8))
                            {

                                if (JSONUserAgents.IsMatch(Request.UserAgent ?? "") &&
                                    methodNode.TryGetContentType(HTTPContentType.Application.XML_UTF8, out contentTypeNode))
                                {
                                    return ParsedRequest.Parsed(
                                               contentTypeNode,
                                               parsedRouteNode.Parameters
                                           );
                                }

                                else if (methodNode.TryGetContentType(HTTPContentType.Text. HTML_UTF8, out contentTypeNode))
                                    return ParsedRequest.Parsed(
                                               contentTypeNode,
                                               parsedRouteNode.Parameters
                                           );

                            }

                            #endregion


                            #region Text

                            if (methodNode.TryGetContentType(HTTPContentType.Text.PLAIN, out contentTypeNode))
                                return ParsedRequest.Parsed(
                                           contentTypeNode,
                                           parsedRouteNode.Parameters
                                       );

                            #endregion

                        }

                    }

                }

            }
            catch (Exception e)
            {
                httpLogger.LogError(
                    e,
                    "Exception while resolving HTTP request {FirstPDULine} ({EventTrackingId}).",
                    Request?.FirstPDULine,
                    Request?.EventTrackingId
                );

                return ParsedRequest.Error(GetExceptionDescriptionForResponse(e));
            }

            // Every segment matched an API, but that API has no route for the rest
            // of the path (e.g. the API root itself): not found, not a server error.
            return ParsedRequest.Error(HTTPStatusCode.NotFound, "Unknown path!");

        }

        #endregion

        #region (internal) GetRequestHandle(Host = "*", HTTPMethod, Path, HTTPContentTypeSelector = null)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        internal ParsedRequest

            GetRequestHandle(HTTPHostname                               Hostname,
                             HTTPMethod                                 HTTPMethod,
                             HTTPPath                                   Path,
                             Func<HTTPContentType[], HTTPContentType>?  HTTPContentTypeSelector   = null)

        {

            try
            {

                if (!routeNodes.TryGetValue(Hostname,         out var host) &&
                    !routeNodes.TryGetValue(HTTPHostname.Any, out     host))
                {
                    return ParsedRequest.Error($"Unknown hostname '{Hostname}'!");
                }

                var segments = ("/" + Path.ToString().Trim('/')).Split('/');

                if (segments[0] == "")
                    segments[0] = "/";


                for (var i=0; i < segments.Length; i++)
                {

                    if (!host.Children.TryGetValue(segments[i], out var __routeNode))
                    {
                        if (!host.Children.TryGetValue("/", out __routeNode))
                        {
                            return ParsedRequest.Error(
                                       HTTPStatusCode.NotFound,
                                       $"Unknown path segment!"
                                   );
                        }
                    }

                    host = __routeNode;

                    // A host node may already contain an API while also being
                    // the parent of a more specific API path. Defer dispatch
                    // to the parent API until no deeper API route matches.
                    var hasMoreSpecificAPI = i + 1 < segments.Length &&
                                             host.Children.ContainsKey(segments[i + 1]);

                    if (host.HTTPAPI is not null &&
                        !hasMoreSpecificAPI)
                    {

                        // The API-relative path: strip the root path of the API,
                        // given with or without a trailing slash, from the path.
                        var requestPath      = Path.ToString();
                        var rootPath         = host.HTTPAPI.RootPath.ToString().TrimEnd('/');
                        var relativePath     = rootPath.Length == 0
                                                   ? requestPath
                                                   : requestPath[rootPath.Length..];

                        if (relativePath.Length == 0)
                            relativePath = "/";

                        var newPath          = HTTPPath.Parse(relativePath);
                        var parsedRouteNode  = host.HTTPAPI.GetRequestHandle(newPath);

                        if (parsedRouteNode.RouteNode is not null)
                        {

                            if (!parsedRouteNode.RouteNode.Methods.TryGetValue(HTTPMethod, out var methodNode))
                                return ParsedRequest.Error(
                                           HTTPStatusCode.MethodNotAllowed,
                                           "Method not allowed!",
                                           parsedRouteNode.RouteNode.Methods.Keys
                                       );

                            if (methodNode.ContentTypes.Any() && HTTPContentTypeSelector is not null)
                            {

                                var bestMatchingContentType = HTTPContentTypeSelector([.. methodNode.ContentTypes]);

                                if (bestMatchingContentType != HTTPContentType.ALL)
                                {
                                    if (methodNode.TryGetContentType(bestMatchingContentType, out var contentTypeNode))
                                        return ParsedRequest.Parsed(
                                                   contentTypeNode,
                                                   parsedRouteNode.Parameters
                                               );
                                }

                                if (methodNode.ContentTypes.Count() == 1)
                                    return ParsedRequest.Parsed(
                                               methodNode.HTTPRequestHandlers.First(),
                                               parsedRouteNode.Parameters
                                           );

                            }

                            return ParsedRequest.Parsed(
                                       methodNode.RequestHandlers,
                                       parsedRouteNode.Parameters
                                   );

                        }

                    }

                }

            }
            catch (Exception e)
            {
                httpLogger.LogError(e, "Exception while resolving an HTTP request handler.");

                return ParsedRequest.Error(GetExceptionDescriptionForResponse(e));
            }

            // Every segment matched an API, but that API has no route for the rest
            // of the path (e.g. the API root itself): not found, not a server error.
            return ParsedRequest.Error(HTTPStatusCode.NotFound, "Unknown path!");

        }

        #endregion

        #region (internal) AddHandler(HTTPAPI, HTTPDelegate, Hostname = "*", URLTemplate = "/", HTTPMethod = null, HTTPContentType = null, OpenEnd = false, ...)

        /// <summary>
        /// Add a method callback for the given server-wide URL template.
        /// Requests are dispatched to the HTTP APIs registered at this server,
        /// therefore the handler is registered at the HTTP API owning the URL
        /// template: the given one, or else the most specific HTTP API registered
        /// for the hostname and the path. When no HTTP API covers the path yet,
        /// a default HTTP API at '/' is created for the hostname.
        /// </summary>
        /// <param name="HTTPAPI">An optional HTTP API to register the handler at.</param>
        /// <param name="HTTPDelegate">A delegate called for each incoming HTTP request.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">The server-wide URL template, i.e. including the root path of the HTTP API.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="OpenEnd">Whether the last URL parameter of the template also matches the rest of the path, i.e. "{name}" acts as "{name..}".</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="AllowReplacement">Whether an existing handler may be replaced.</param>
        internal void AddHandler(HTTPAPI?                     HTTPAPI,
                                 HTTPDelegate                 HTTPDelegate,

                                 HTTPHostname?                Hostname                    = null,
                                 HTTPPath?                    URLTemplate                 = null,
                                 HTTPMethod?                  HTTPMethod                  = null,
                                 HTTPContentType?             HTTPContentType             = null,
                                 Boolean                      OpenEnd                     = false,

                                 HTTPAuthentication?          URLAuthentication           = null,
                                 HTTPAuthentication?          HTTPMethodAuthentication    = null,
                                 HTTPAuthentication?          ContentTypeAuthentication   = null,

                                 OnHTTPRequestLogDelegate2?   HTTPRequestLogger           = null,
                                 OnHTTPResponseLogDelegate2?  HTTPResponseLogger          = null,

                                 HTTPDelegate?                DefaultErrorHandler         = null,
                                 URLReplacement               AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial Checks

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate), "The given parameter must not be null!");

            if (HTTPMethod is null && HTTPContentType is not null)
                throw new ArgumentException("If HTTPMethod is null the HTTPContentType must also be null!");

            #endregion

            var hostname     = Hostname    ?? HTTPHostname.Any;
            var urlTemplate  = URLTemplate ?? HTTPPath.Root;

            var httpAPI      = HTTPAPI
                                   ?? FindHTTPAPI(hostname, urlTemplate)
                                   ?? AddHTTPAPI(HTTPPath.Root, hostname);

            // The HTTP API matches its templates relative to its root path.
            var rootPath     = httpAPI.RootPath.ToString().TrimEnd('/');
            var template     = urlTemplate.ToString();

            if (rootPath.Length > 0)
            {

                if (template != rootPath &&
                   !template.StartsWith(rootPath + "/", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"The URL template '{urlTemplate}' is outside the HTTP API at '{httpAPI.RootPath}'!",
                                                nameof(URLTemplate));
                }

                template = template[rootPath.Length..];

                if (template.Length == 0)
                    template = "/";

            }

            // An open end widens the last URL parameter to the rest of the path.
            if (OpenEnd &&
                template.EndsWith('}') &&
               !template.EndsWith("..}", StringComparison.Ordinal))
            {
                template = template[..^1] + "..}";
            }

            httpAPI.AddHandler(
                HTTPPath.Parse(template),
                HTTPDelegate,
                HTTPMethod,
                HTTPContentType,
                URLAuthentication,
                HTTPMethodAuthentication,
                ContentTypeAuthentication,
                HTTPRequestLogger,
                HTTPResponseLogger,
                DefaultErrorHandler,
                AllowReplacement:  AllowReplacement
            );

        }

        #endregion

        #region (private) FindHTTPAPI(Hostname, Path)

        /// <summary>
        /// The most specific HTTP API registered for the given hostname
        /// whose root path covers the given path, if any.
        /// </summary>
        /// <param name="Hostname">An HTTP hostname.</param>
        /// <param name="Path">A server-wide HTTP path.</param>
        private HTTPAPI? FindHTTPAPI(HTTPHostname  Hostname,
                                     HTTPPath      Path)
        {

            if (!routeNodes.TryGetValue(Hostname, out var routeNode))
                return null;

            var segments = ("/" + Path.ToString().Trim('/')).Split('/');

            if (segments[0] == "")
                segments[0] = "/";

            HTTPAPI? httpAPI = null;

            foreach (var segment in segments)
            {

                if (!routeNode.Children.TryGetValue(segment, out var childNode))
                    break;

                routeNode = childNode;

                if (routeNode.HTTPAPI is not null)
                    httpAPI = routeNode.HTTPAPI;

            }

            return httpAPI;

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate,  HTTPContentType = null, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given server-wide URL template.
        /// The handler is registered at the HTTP API owning the path, see AddHandler(...).
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The server-wide URL template, i.e. including the root path of the HTTP API.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="OpenEnd">Whether the last URL parameter of the template also matches the rest of the path.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname                 Hostname,
                                      HTTPMethod                   HTTPMethod,
                                      HTTPPath                     URLTemplate,
                                      HTTPContentType?             HTTPContentType             = null,
                                      Boolean                      OpenEnd                     = false,
                                      HTTPAuthentication?          URLAuthentication           = null,
                                      HTTPAuthentication?          HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?          ContentTypeAuthentication   = null,
                                      OnHTTPRequestLogDelegate2?   HTTPRequestLogger           = null,
                                      OnHTTPResponseLogDelegate2?  HTTPResponseLogger          = null,
                                      HTTPDelegate?                DefaultErrorHandler         = null,
                                      HTTPDelegate?                HTTPDelegate                = null,
                                      URLReplacement               AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial checks

            if (URLTemplate.IsNullOrEmpty)
                throw new ArgumentNullException(nameof(URLTemplate),   "The given URL template must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),  "The given HTTP delegate must not be null!");

            #endregion

            AddHandler(
                null,
                HTTPDelegate,
                Hostname,
                URLTemplate,
                HTTPMethod,
                HTTPContentType,
                OpenEnd,
                URLAuthentication,
                HTTPMethodAuthentication,
                ContentTypeAuthentication,
                HTTPRequestLogger,
                HTTPResponseLogger,
                DefaultErrorHandler,
                AllowReplacement
            );

        }


        /// <summary>
        /// Add a method callback for the given server-wide URL template.
        /// The handler is registered at the HTTP API owning the path, see AddHandler(...).
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The server-wide URL template, i.e. including the root path of the HTTP API.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="OpenEnd">Whether the last URL parameter of the template also matches the rest of the path.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPAPI                     HTTPAPI,
                                      HTTPHostname                 Hostname,
                                      HTTPMethod                   HTTPMethod,
                                      HTTPPath                     URLTemplate,
                                      HTTPContentType?             HTTPContentType             = null,
                                      Boolean                      OpenEnd                     = false,
                                      HTTPAuthentication?          URLAuthentication           = null,
                                      HTTPAuthentication?          HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?          ContentTypeAuthentication   = null,
                                      OnHTTPRequestLogDelegate2?   HTTPRequestLogger           = null,
                                      OnHTTPResponseLogDelegate2?  HTTPResponseLogger          = null,
                                      HTTPDelegate?                DefaultErrorHandler         = null,
                                      HTTPDelegate?                HTTPDelegate                = null,
                                      URLReplacement               AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial checks

            if (URLTemplate.IsNullOrEmpty)
                throw new ArgumentNullException(nameof(URLTemplate),   "The given URL template must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),  "The given HTTP delegate must not be null!");

            #endregion

            AddHandler(
                HTTPAPI,
                HTTPDelegate,
                Hostname,
                URLTemplate,
                HTTPMethod,
                HTTPContentType,
                OpenEnd,
                URLAuthentication,
                HTTPMethodAuthentication,
                ContentTypeAuthentication,
                HTTPRequestLogger,
                HTTPResponseLogger,
                DefaultErrorHandler,
                AllowReplacement
            );

        }

        #endregion



        #region (override) ProcessHTTPRequest(Request, Stream, CancellationToken = default)

        protected override async Task<HTTPResponse>

            ProcessHTTPRequest(HTTPRequest        Request,
                               Stream             Stream,
                               CancellationToken  CancellationToken   = default)

        {
            try
            {

                Request.HTTPTestServerX = this;

                #region Log HTTP Request

                await LogEvent(
                          OnHTTPRequest,
                          loggingDelegate => loggingDelegate.Invoke(
                              this,
                              Request,
                              CancellationToken
                          )
                      );

                #endregion


                #region Process HTTP pipelines...

                HTTPResponse? httpResponse = null;

                foreach (var httpPipeline in httpPipelines)
                {

                    (Request, httpResponse)  = await httpPipeline.ProcessHTTPRequest(
                                                         Request,
                                                         CancellationToken
                                                     );

                    // Stop, when a pipeline returned a response!
                    if (httpResponse is not null)
                        break;

                }

                #endregion

                if (httpResponse is null)
                {

                    var parsedRequest = GetRequestHandle(Request);

                    if (parsedRequest.RequestHandlers is not null)
                    {

                        #region Call HTTP request logger

                        await LogEvent(
                                  parsedRequest.RequestHandlers.HTTPRequestLogger,
                                  loggingDelegate => loggingDelegate.Invoke(
                                      Timestamp.Now,
                                      parsedRequest.RequestHandlers.HTTPAPI,
                                      Request,
                                      CancellationToken
                                  )
                              );

                        #endregion

                        #region Process HTTP request

                        var httpDelegate = parsedRequest.RequestHandlers.RequestHandler;
                        if (httpDelegate is not null)
                        {

                            try
                            {

                                 Request.ParsedURLParametersX  = parsedRequest.Parameters;
                                 // The positional view in template order, for the handlers that index them.
                                 Request.ParsedURLParameters   = [.. parsedRequest.Parameters.Values];
                                 Request.NetworkStream         = Stream;

                                 httpResponse                  = await httpDelegate(Request);

                             }
                              catch (HTTPChunkMetadataTooLargeException)
                              {
                                  httpResponse = CreateInvalidChunkMetadataResponse(Request);
                              }
                               catch (HTTPBodyTooLargeException)
                               {
                                   httpResponse = CreateRequestBodyTooLargeResponse(Request);
                               }
                               catch (HTTPIncompleteBodyException)
                               {
                                   httpResponse = CreateIncompleteRequestBodyResponse(Request);
                               }
                               catch (HTTPInvalidChunkException)
                              {
                                  httpResponse = CreateInvalidChunkResponse(Request);
                              }
                              catch (HTTPReadTimeoutException)
                             {
                                 httpResponse = CreateRequestTimeoutResponse(Request);
                             }
                             catch (Exception e)
                             {

                                 httpLogger.LogError(
                                     e,
                                     "Exception in HTTP request handler for {FirstPDULine} ({EventTrackingId}).",
                                     Request?.FirstPDULine,
                                     Request?.EventTrackingId
                                 );

                                 httpResponse = new HTTPResponse.Builder(Request) {
                                                   HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                                   Server          = HTTPServerName,
                                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                                   Content         = CreateInternalServerErrorJSON(Request, e).ToUTF8Bytes(),
                                                   Connection      = ConnectionType.KeepAlive
                                               };

                            }

                        }
                        else
                            httpResponse = new HTTPResponse.Builder(Request) {
                                               HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                               Server          = HTTPServerName,
                                               ContentType     = HTTPContentType.Application.JSON_UTF8,
                                               Content         = JSONObject.Create(
                                                                       new JProperty("request",       Request.FirstPDULine),
                                                                       new JProperty("description",  "HTTP request handler must not be null!")
                                                                   ).ToUTF8Bytes(),
                                               Connection      = ConnectionType.KeepAlive
                                           };

                        httpResponse ??= new HTTPResponse.Builder(Request) {
                                             HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                             Server          = HTTPServerName,
                                             ContentType     = HTTPContentType.Application.JSON_UTF8,
                                             Content         = JSONObject.Create(
                                                                     new JProperty("request",       Request.FirstPDULine),
                                                                     new JProperty("description",  "HTTP response must not be null!")
                                                                 ).ToUTF8Bytes(),
                                             Connection      = ConnectionType.KeepAlive
                                         };

                        #endregion

                        #region Call HTTP response logger

                        await LogEvent(
                                  parsedRequest.RequestHandlers.HTTPResponseLogger,
                                  loggingDelegate => loggingDelegate.Invoke(
                                      Timestamp.Now,
                                      parsedRequest.RequestHandlers.HTTPAPI,
                                      Request,
                                      httpResponse,
                                      CancellationToken
                                  )
                              );

                        #endregion

                    }

                    //if (parsedRequest.ErrorResponse == "This HTTP method is not allowed!")
                    //    httpResponse = new HTTPResponse.Builder(Request) {
                    //                       HTTPStatusCode  = HTTPStatusCode.MethodNotAllowed,
                    //                       Server          = Request.Host.ToString(),
                    //                       Date            = Timestamp.Now,
                    //                       ContentType     = HTTPContentType.Text.PLAIN,
                    //                       Content         = parsedRequest.ErrorResponse.ToUTF8Bytes()
                    //             //          Connection      = ConnectionType.KeepAlive
                    //                   };

                    httpResponse ??= new HTTPResponse.Builder(Request) {
                                         HTTPStatusCode  = parsedRequest.HTTPStatusCode ?? HTTPStatusCode.InternalServerError,
                                         Server          = HTTPServerName,
                                         Date            = Timestamp.Now,
                                         ContentType     = HTTPContentType.Application.JSON_UTF8,
                                          Content         = JSONObject.Create(
                                                                new JProperty("request",      Request.FirstPDULine),
                                                                new JProperty("description",  parsedRequest.ErrorResponse)
                                                            ).ToUTF8Bytes(),
                                          Allow           = parsedRequest.AllowedMethods,
                                          Connection      = ConnectionType.KeepAlive
                                      };

                }


                #region Log HTTP Response

                await LogEvent(
                          OnHTTPResponse,
                          loggingDelegate => loggingDelegate.Invoke(
                              this,
                              Request,
                              httpResponse,
                              CancellationToken
                          )
                      );

                #endregion

                #region Status Code 4xx or 5xx => Log HTTP Error Response

                if (httpResponse.HTTPStatusCode.Code >  400 &&
                    httpResponse.HTTPStatusCode.Code <= 599)
                {

                    await LogEvent(
                          OnHTTPError,
                          loggingDelegate => loggingDelegate.Invoke(
                              this,
                              Request,
                              httpResponse,
                              CancellationToken
                          )
                      );

                }

                #endregion

                return httpResponse;

            }
            catch (Exception e)
            {
                if (e is HTTPBodyTooLargeException)
                {
                    return new HTTPResponse.Builder(Request) {
                               HTTPStatusCode = HTTPStatusCode.RequestEntityTooLarge,
                               Server         = HTTPServerName,
                               ContentType    = HTTPContentType.Application.JSON_UTF8,
                               Content        = JSONObject.Create(
                                                    new JProperty("description", "The request body is too large."),
                                                    new JProperty("maximumBytes", MaxHTTPBodySize)
                                                ).ToUTF8Bytes(),
                               Connection     = ConnectionType.Close
                           };
                }

                if (e is HTTPIncompleteBodyException)
                    return CreateIncompleteRequestBodyResponse(Request);

                if (e is HTTPChunkMetadataTooLargeException)
                    return CreateInvalidChunkMetadataResponse(Request);

                if (e is HTTPInvalidChunkException)
                    return CreateInvalidChunkResponse(Request);

                if (e is HTTPReadTimeoutException)
                    return CreateRequestTimeoutResponse(Request);

                httpLogger.LogError(
                    e,
                    "Exception while processing HTTP request {FirstPDULine} ({EventTrackingId}).",
                    Request?.FirstPDULine ?? "null",
                    Request?.EventTrackingId.ToString() ?? "null"
                );

                return new HTTPResponse.Builder(Request) {
                           HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                           Server          = HTTPServerName,
                           ContentType     = HTTPContentType.Application.JSON_UTF8,
                            Content         = CreateInternalServerErrorJSON(Request, e).ToUTF8Bytes(),
                           Connection      = ConnectionType.KeepAlive
                       };
            }

            //await SendResponse(
            //          Stream,
            //          httpResponse,
            //          CancellationToken
            //      );

            //if (httpResponse.Worker is not null)
            //{
            //    try
            //    {
            //        httpResponse.Worker(httpResponse, httpResponse.HTTPBodyStream as ChunkedTransferEncodingStream);
            //    }
            //    catch (Exception e)
            //    {
            //        DebugX.LogT("HTTP server response worker exception: " + e.Message);
            //    }
            //}

        }

        private HTTPResponse CreateRequestTimeoutResponse(HTTPRequest Request)
            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode = HTTPStatusCode.RequestTimeout,
                   Server         = HTTPServerName,
                   Date           = Timestamp.Now,
                   Connection     = ConnectionType.Close,
                   ContentType    = HTTPContentType.Application.JSON_UTF8,
                   Content        = JSONObject.Create(
                                        new JProperty("description", "The request body read timed out.")
                                    ).ToUTF8Bytes()
               }.AsImmutable;

        #endregion



        #region (private) LogEvent (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(HTTPTestServer),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


    }

}
