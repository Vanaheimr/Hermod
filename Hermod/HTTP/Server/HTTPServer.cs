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
    /// A simple HTTP test server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining.
    /// </summary>
    public class HTTPServer : AHTTPServer
    {

        #region Data

        private readonly ConcurrentDictionary<HTTPHostname,       HostnameNodeX>     hostnameNodes   = [];
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
        /// Create a new HTTP server.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="HTTPServerName">An optional HTTP server name. If null or empty, the default HTTP server name will be used.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        /// 
        /// <param name="DisableMaintenanceTasks">Disable all maintenance tasks.</param>
        /// <param name="MaintenanceInitialDelay">The initial delay of the maintenance tasks.</param>
        /// <param name="MaintenanceEvery">The maintenance interval.</param>
        /// 
        /// <param name="DisableWardenTasks">Disable all warden tasks.</param>
        /// <param name="WardenInitialDelay">The initial delay of the warden tasks.</param>
        /// <param name="WardenCheckEvery">The warden interval.</param>
        /// 
        /// <param name="ServerCertificateSelector"></param>
        /// <param name="ClientCertificateValidator"></param>
        /// <param name="LocalCertificateSelector"></param>
        /// <param name="AllowedTLSProtocols"></param>
        /// <param name="ClientCertificateRequired"></param>
        /// <param name="CheckCertificateRevocation"></param>
        /// 
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information. If null, the default connection identification will be used.</param>
        /// <param name="MaxClientConnections">An optional maximum number of concurrent TCP client connections. If null, the default maximum number of concurrent TCP client connections will be used.</param>
        /// <param name="DNSClient"></param>
        /// 
        /// <param name="DisableMaintenanceTasks">Disable all maintenance tasks.</param>
        /// <param name="MaintenanceInitialDelay">The initial delay of the maintenance tasks.</param>
        /// <param name="MaintenanceEvery">The maintenance interval.</param>
        /// 
        /// <param name="DisableWardenTasks">Disable all warden tasks.</param>
        /// <param name="WardenInitialDelay">The initial delay of the warden tasks.</param>
        /// <param name="WardenCheckEvery">The warden interval.</param>
        /// 
        /// <param name="DefaultAPI"></param>
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
                           new JProperty("request",          Request?.FirstPDULine       ?? "null"),
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
                                                                        RootPath: path
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
                routeNode1.Children.GetOrAdd(
                    "/",
                    pathSegment => {

                        if (routeNode1.HTTPAPI is not null)
                            throw new ArgumentException($"An HTTP API at '{path}' is already registered!", nameof(Path));

                        return new HTTPAPINode(
                            routeNode1.FullPath + "/" + pathSegment,
                            pathSegment,
                            httpAPI
                        );

                    }
                );

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
                                         pathSegment => {

                                             if (segmentIndex == segments.Length - 1)
                                             {

                                                 if (routeNode1.HTTPAPI is not null)
                                                     throw new ArgumentException($"An HTTP API at '{path}' is already registered!", nameof(Path));

                                                 return new HTTPAPINode(
                                                     routeNode1.FullPath + "/" + pathSegment,
                                                     "/" + pathSegment,
                                                     httpAPI
                                                 );

                                             }

                                             else return new HTTPAPINode(
                                                             routeNode1.FullPath + "/" + pathSegment,
                                                             "/" + pathSegment
                                                         );

                                         }
                                     );

                    routeNode1 = routeNode2;

                }
            }

            return httpAPI;

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

            try
            {

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
                        var requestPath      = Request.Path.ToString();
                        var rootPath         = httpAPINode.HTTPAPI.RootPath.ToString();
                        var relativePath     = rootPath == "/"
                                                    ? requestPath
                                                    : requestPath[(rootPath.Length - 1)..];
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
                    Request.FirstPDULine,
                    Request.EventTrackingId
                );

                return ParsedRequest.Error(GetExceptionDescriptionForResponse(e));
            }

            return ParsedRequest.Error($"error!");

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

                    if (host.HTTPAPI is not null)
                    {

                        var newPath          = HTTPPath.Parse(segments.AggregateWith('/')[(host.HTTPAPI.RootPath.ToString().Length - 1)..]);
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

            return ParsedRequest.Error($"error!");

        }

        #endregion

        #region (internal) AddHandler(HTTPDelegate, Hostname = "*", URLTemplate = "/", HTTPMethod = null, HTTPContentType = null, HostAuthentication = null, URLAuthentication = null, HTTPMethodAuthentication = null, ContentTypeAuthentication = null, DefaultErrorHandler = null)

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
        internal void AddHandler(HTTPAPI                     HTTPAPI,
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

            var hostname = Hostname ?? HTTPHostname.Any;

            if (HTTPMethod is null && HTTPContentType is not null)
                throw new ArgumentException("If HTTPMethod is null the HTTPContentType must also be null!");

            #endregion

            if (!hostnameNodes.TryGetValue(hostname, out var hostnameNode))
                hostnameNode = hostnameNodes.AddAndReturnValue(
                                                 hostname,
                                                 new HostnameNodeX(
                                                     HTTPAPI,
                                                     hostname
                                                 )
                                             );

            hostnameNode.AddHandler(
                             HTTPAPI,
                             HTTPDelegate,

                             URLTemplate,
                             OpenEnd,
                             HTTPMethod,
                             HTTPContentType,

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

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate,  HTTPContentType = null, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
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
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
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
                                     Request.FirstPDULine,
                                     Request.EventTrackingId
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
