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

using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Runtime.CompilerServices;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    public delegate Task OnHTTPRequestLogDelegate (HTTPTestServerX    HTTPServer,
                                                   HTTPRequest        Request,
                                                   CancellationToken  CancellationToken);

    public delegate Task OnHTTPResponseLogDelegate(HTTPTestServerX    HTTPServer,
                                                   HTTPRequest        Request,
                                                   HTTPResponse       Response,
                                                   CancellationToken  CancellationToken);

    public delegate Task OnHTTPRequestLogDelegate2 (DateTimeOffset     Timestamp,
                                                    HTTPAPIX           API,
                                                    HTTPRequest        Request,
                                                    CancellationToken  CancellationToken);

    public delegate Task OnHTTPResponseLogDelegate2(DateTimeOffset     Timestamp,
                                                    HTTPAPIX           API,
                                                    HTTPRequest        Request,
                                                    HTTPResponse       Response,
                                                    CancellationToken  CancellationToken);


    /// <summary>
    /// A simple HTTP test server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining.
    /// </summary>
    public class HTTPTestServerX : AHTTPTestServer
    {

        #region Data

        private readonly ConcurrentDictionary<HTTPHostname, HostnameNodeX>  hostnameNodes   = [];
        private readonly ConcurrentDictionary<HTTPHostname, HTTPAPINode>    routeNodes      = [];
        private readonly List<AHTTPPipeline>                                httpPipelines   = [];

        /// <summary>
        /// Command-line JSON HTTP clients, libraries, and API tools...
        /// </summary>
        private static readonly Regex JSONUserAgents  = new (
                                                            @"\b(curl|wget|httpie|PostmanRuntime|python-requests|Go-http-client|libcurl|restsharp|okhttp|Apache-HttpClient|Insomnia|Java/\d)\b",
                                                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
                                                        );

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
        public HTTPTestServerX(IIPAddress?                                               IPAddress                    = null,
                               IPPort?                                                   TCPPort                      = null,
                               String?                                                   HTTPServerName               = null,
                               UInt32?                                                   BufferSize                   = null,
                               TimeSpan?                                                 ReceiveTimeout               = null,
                               TimeSpan?                                                 SendTimeout                  = null,
                               TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                               ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                               RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
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

                               HTTPAPIX?                                                 DefaultAPI                   = null,
                               Boolean?                                                  AutoStart                    = false)

            : base(IPAddress,
                   TCPPort,
                   HTTPServerName,
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

                   AutoStart: false)

        {

            if (DefaultAPI is not null)
            {

                var defaultHost = routeNodes.AddAndReturnValue(
                                      HTTPHostname.Any,
                                      new HTTPAPINode(
                                          HTTPHostname.Any.ToString(),
                                          HTTPPath.Root.ToString()
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


        #region (static) StartNew(...)

        public static async Task<HTTPTestServerX>

            StartNew(IIPAddress?                                               IPAddress                    = null,
                     IPPort?                                                   TCPPort                      = null,
                     String?                                                   HTTPServerName               = null,
                     UInt32?                                                   BufferSize                   = null,
                     TimeSpan?                                                 ReceiveTimeout               = null,
                     TimeSpan?                                                 SendTimeout                  = null,
                     TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                     ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                     RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
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

                     HTTPAPIX?                                                 DefaultAPI                   = null)

        {

            var server = new HTTPTestServerX(

                             IPAddress,
                             TCPPort,
                             HTTPServerName,
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

                             AutoStart: false

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


        public HTTPAPIX DefaultAPI

            => routeNodes[HTTPHostname.Any]?.Children["/"]?.HTTPAPI
                   ?? throw new InvalidOperationException("The main API '/' is not registered!");


        public HTTPAPIX AddHTTPAPI(HTTPPath?                                   Path             = null,
                                   HTTPHostname?                               Hostname         = null,
                                   Func<HTTPTestServerX, HTTPPath, HTTPAPIX>?  HTTPAPICreator   = null)
        {

            var path        = Path                               ?? HTTPPath.Root;
            var hostname    = Hostname                           ?? HTTPHostname.Any;
            var httpAPI     = HTTPAPICreator?.Invoke(this, path) ?? new HTTPAPIX(
                                                                        this,
                                                                        RootPath: path
                                                                    );

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

                foreach (var segment in segments)
                {

                    var routeNode2 = routeNode1.Children.GetOrAdd(
                                         segment,
                                         pathSegment => {

                                             if ((routeNode1.FullPath + "/" + pathSegment) == (hostname + path.ToString().TrimEnd('/')))
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



        private void FindMatch(String Path, ref List<String> Matches)
        {
            foreach (var child in routeNodes.Values)
            {
                if (Path.StartsWith(child.Path))
                {
                    Matches.Add(child.Path);
                    //child.FindMatch(Path[(child.Path.Length - 1)..], ref Matches);
                }
            }
        }


        #region (internal) GetRequestHandle(Request)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        internal ParsedRequest GetRequestHandle(HTTPRequest Request)
        {

            try
            {

                if (!routeNodes.TryGetValue(Request.Host,     out var httpAPINode) &&
                    !routeNodes.TryGetValue(HTTPHostname.Any, out     httpAPINode))
                {
                    return ParsedRequest.Error($"Unknown hostname '{Request.Host}'!");
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
                            return ParsedRequest.Error($"Unknown path segment!");
                        }
                    }

                    httpAPINode = httpAPINode2;

                    if (httpAPINode.HTTPAPI is not null)
                    {

                        var newPath          = HTTPPath.Parse(segments.AggregateWith('/')[(httpAPINode.HTTPAPI.RootPath.ToString().Length - 1)..]);
                        var parsedRouteNode  = httpAPINode.HTTPAPI.GetRequestHandle(newPath);

                        if (parsedRouteNode.RouteNode is not null)
                        {

                            if (!parsedRouteNode.RouteNode.Methods.TryGetValue(Request.HTTPMethod, out var methodNode))
                                return ParsedRequest.Error(
                                           HTTPStatusCode.MethodNotAllowed,
                                           "Method not allowed!"
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
                return ParsedRequest.Error(e.Message + Environment.NewLine + e.StackTrace);
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
                            return ParsedRequest.Error($"Unknown path segment!");
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
                                           "Method not allowed!"
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
                return ParsedRequest.Error(e.Message + Environment.NewLine + e.StackTrace);
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
        internal void AddHandler(HTTPAPIX                     HTTPAPI,
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
        public void AddMethodCallback(HTTPAPIX                     HTTPAPI,
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

                                 DebugX.Log(Request.HTTPMethod + " " + Request.Path);

                                 httpResponse                  = await httpDelegate(Request);

                            }
                            catch (Exception e)
                            {

                                httpResponse = new HTTPResponse.Builder(Request) {
                                                   HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                                   Server          = HTTPServerName,
                                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                                   Content         = JSONObject.Create(
                                                                         new JProperty("request",      Request.FirstPDULine),
                                                                         new JProperty("description",  e.Message),
                                                                         new JProperty("stackTrace",   e.StackTrace),
                                                                         new JProperty("source",       e.TargetSite?.Module.Name),
                                                                         new JProperty("type",         e.TargetSite?.ReflectedType?.Name)
                                                                     ).ToUTF8Bytes(),
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
                                                           ).ToUTF8Bytes()
                                      //   Connection      = ConnectionType.KeepAlive
                                     };

                }


                #region Status Code 4xx or 5xx => Log HTTP Error Response...

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

                #region ...else log HTTP Response

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


                return httpResponse;

            }
            catch (Exception e)
            {
                return new HTTPResponse.Builder(Request) {
                           HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                           Server          = HTTPServerName,
                           ContentType     = HTTPContentType.Application.JSON_UTF8,
                           Content         = JSONObject.Create(
                                                 new JProperty("request",      Request?.FirstPDULine ?? "null"),
                                                 new JProperty("description",  e.Message),
                                                 new JProperty("stackTrace",   e.StackTrace),
                                                 new JProperty("source",       e.TargetSite?.Module.Name),
                                                 new JProperty("type",         e.TargetSite?.ReflectedType?.Name)
                                             ).ToUTF8Bytes(),
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
