﻿/*
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

using Newtonsoft.Json.Linq;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    public delegate Task OnHTTPRequestLogDelegate (HTTPTestServerX    HTTPServer,
                                                   HTTPRequest        Request,
                                                   CancellationToken  CancellationToken);

    public delegate Task OnHTTPResponseLogDelegate(HTTPTestServerX    HTTPServer,
                                                   HTTPResponse       Response,
                                                   CancellationToken  CancellationToken);


    /// <summary>
    /// A simple HTTP test server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining.
    /// </summary>
    /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
    /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
    /// <param name="HTTPServerName">An optional HTTP server name. If null or empty, the default HTTP server name will be used.</param>
    /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
    /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
    /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
    /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
    public class HTTPTestServerX(IIPAddress?              IPAddress        = null,
                                 IPPort?                  TCPPort          = null,
                                 String?                  HTTPServerName   = null,
                                 UInt32?                  BufferSize       = null,
                                 TimeSpan?                ReceiveTimeout   = null,
                                 TimeSpan?                SendTimeout      = null,
                                 TCPEchoLoggingDelegate?  LoggingHandler   = null)

        : AHTTPTestServer(
              IPAddress,
              TCPPort,
              HTTPServerName,
              BufferSize,
              ReceiveTimeout,
              SendTimeout,
              LoggingHandler
          )

    {

        #region Data

        private readonly ConcurrentDictionary<HTTPHostname, HostnameNodeX>  hostnameNodes = [];

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


        #region StartNew(...)

        public static async Task<HTTPTestServerX>

            StartNew(IIPAddress?              IPAddress        = null,
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

            await server.Start();

            return server;

        }

        #endregion


        #region HTTP Pipelines

        private readonly List<AHTTPPipeline> httpPipelines = [];

        public void AddPipeline(AHTTPPipeline Pipeline)
        {
            httpPipelines.Add(Pipeline);
        }

        #endregion


        #region (internal) GetRequestHandle(Request)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        internal (HTTPRequestHandleX?, Dictionary<String, String>)

            GetRequestHandle(HTTPRequest  Request,
                             out String?  ErrorResponse)

                => GetRequestHandle(
                       Request.Host,
                       Request.Path.IsNullOrEmpty ? HTTPPath.Parse("/") : Request.Path,
                       out ErrorResponse,
                       Request.HTTPMethod,
                       AvailableContentTypes => Request.Accept.BestMatchingContentType(AvailableContentTypes),// ?? AvailableContentTypes.First(),
                       ParsedURLParameters   => Request.ParsedURLParameters = ParsedURLParameters.ToArray()
                   );

        #endregion


        private readonly ConcurrentDictionary<HTTPHostname, RouteNode> routeNodes = [];

        public HTTPAPIX AddHTTPAPI(HTTPPath?                                   Path             = null,
                                   HTTPHostname?                               Hostname         = null,
                                   Func<HTTPTestServerX, HTTPPath, HTTPAPIX>?  HTTPAPICreator   = null)
        {

            var path        = Path                               ?? HTTPPath.Root;
            var hostname    = Hostname                           ?? HTTPHostname.Any;
            var httpAPI     = HTTPAPICreator?.Invoke(this, path) ?? new HTTPAPIX(
                                                                        HTTPTestServer:  this,
                                                                        RootPath:        path
                                                                    );

            var routeNode1  = routeNodes.GetOrAdd(
                                  hostname,
                                  hh => new RouteNode(
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

                        return new RouteNode(
                            routeNode1.FullPath + "/" + pathSegment,
                            pathSegment,
                            httpAPI
                        );

                    }
                );

            else
            {
                foreach (var segment in path.ToString().Trim('/').Split('/'))
                {

                    var routeNode2 = routeNode1.Children.GetOrAdd(
                                         segment,
                                         pathSegment => {

                                             if ((routeNode1.FullPath + "/" + pathSegment) == (hostname + path.ToString().TrimEnd('/')))
                                             {

                                                 if (routeNode1.HTTPAPI is not null)
                                                     throw new ArgumentException($"An HTTP API at '{path}' is already registered!", nameof(Path));

                                                 return new RouteNode(
                                                     routeNode1.FullPath + "/" + pathSegment,
                                                     "/" + pathSegment,
                                                     httpAPI
                                                 );

                                             }

                                             else return new RouteNode(
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


        #region (internal) GetRequestHandle(Host = "*", Path = "/", ErrorResponse, HTTPMethod = HTTPMethod.GET, HTTPContentTypeSelector = null)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        internal (HTTPRequestHandleX?, Dictionary<String, String>)

            GetRequestHandle(HTTPHostname                               Host,
                             HTTPPath                                   Path,
                             out String?                                ErrorResponse,
                             HTTPMethod?                                HTTPMethod                    = null,
                             Func<HTTPContentType[], HTTPContentType>?  HTTPContentTypeSelector       = null,
                             Action<IEnumerable<String>>?               ParsedURLParametersDelegate   = null)

        {

            if (!routeNodes.IsEmpty)
            {

                if (!routeNodes.TryGetValue(Host, out var host) && !routeNodes.TryGetValue(HTTPHostname.Any, out host))
                {
                    ErrorResponse = "Unknown host!";
                    return (null, []);
                }

                var segments  = Path.ToString().Trim('/').Split('/');

                for (var i=0; i < segments.Length; i++)
                {

                    if (!host.Children.TryGetValue(segments[i], out var xxxx))
                    {
                        if (!host.Children.TryGetValue("/", out xxxx))
                        {
                            ErrorResponse = "Unknown path segment!";
                            return (null, []);
                        }
                    }

                    host = xxxx;

                    if (host.HTTPAPI is not null)
                    {
                        var newPath = HTTPPath.Parse(segments.AggregateWith('/')[(host.HTTPAPI.RootPath.ToString().Length - 1)..]);
                        var ss = host.HTTPAPI.GetRequestHandle(Host, newPath, out ErrorResponse, HTTPMethod, HTTPContentTypeSelector, ParsedURLParametersDelegate);
                        return ss;
                    }

                }

                ErrorResponse = "error!";
                return (null, []);

            }


            Path                       = Path.IsNullOrEmpty
                                             ? HTTPPath.Parse("/")
                                             : Path;
            HTTPMethod               ??= HTTPMethod.GET;
            HTTPContentTypeSelector  ??= (v => HTTPContentType.Text.HTML_UTF8);
            ErrorResponse              = null;

            #region Get HostNode or "*" or fail

            if (!hostnameNodes.TryGetValue(Host,             out var hostnameNode) &&
                !hostnameNodes.TryGetValue(HTTPHostname.Any, out     hostnameNode))
            {
                ErrorResponse = "Could not find a matching hostname node!";
                return (null, []);
            }

            #endregion

            #region Try to find the best matching URLNode...

            var regexList      = from   urlNode
                                 in     hostnameNode.URLNodes
                                 select new {
                                     URLNode = urlNode,
                                     Regex   = urlNode.URLRegex
                                 };

            var allTemplates   = from   regexTupel
                                 in     regexList
                                 select new {
                                     URLNode = regexTupel.URLNode,
                                     Match   = regexTupel.Regex.Match(Path.ToString())
                                 };

            var matches        = from    match
                                 in      allTemplates
                                 where   match.Match.Success
                                 orderby 100*match.URLNode.SortLength +
                                             match.URLNode.ParameterCount
                                         descending
                                 select  new {
                                     match.URLNode,
                                     match.Match
                                 };

            var matchesMethod  = from    match
                                 in      matches
                                 where   match.URLNode.Contains(HTTPMethod)
                                 orderby 100*match.URLNode.SortLength +
                                             match.URLNode.ParameterCount
                                         descending
                                 select  new {
                                     match.URLNode,
                                     match.Match
                                 };

            #endregion

            #region ...or fail!

            if (!matchesMethod.Any())
            {

                ErrorResponse = matches.Any()
                                    ? "This HTTP method is not allowed!"
                                    : "No matching URL template found!";

                //if (_HostNode.RequestHandler != null)
                //    return _HostNode.RequestHandler;

                return (null, []);

            }

            #endregion


            // Caused e.g. by the naming of the variables within the
            // URL templates, there could be multiple matches!
            //foreach (var _Match in _Matches)
            //{

            var filteredByMethod  = matches.Where (match      => match.URLNode.Contains(HTTPMethod)).
                                            Select(match      => match.URLNode.Get     (HTTPMethod)).
                                            Where (methodnode => methodnode is not null).
                                            Select(methodnode => HTTPContentTypeSelector(methodnode!.ContentTypes.ToArray())).
                                            ToArray();

            //foreach (var aa in FilteredByMethod)
            //{

            //    var BestMatchingContentType = HTTPContentTypeSelector(aa.HTTPContentTypes.Keys.ToArray());

            //    //if (aa.HTTPContentTypes

            //}

            // Use best matching URL Handler!
            var bestMatch = matches.First();

            #region Copy MethodHandler Parameters

            var parameters = new List<String>();
            for (var i = 1; i < bestMatch.Match.Groups.Count; i++)
                parameters.Add(bestMatch.Match.Groups[i].Value);

            var parsedURLParametersDelegateLocal = ParsedURLParametersDelegate;
            if (parsedURLParametersDelegateLocal is not null)
                parsedURLParametersDelegateLocal(parameters);

            #endregion

            // If HTTPMethod was found...
            if (bestMatch.URLNode.TryGet(HTTPMethod, out var httpMethodNode) &&
                httpMethodNode is not null)
            {

                var bestMatchingContentType = HTTPContentTypeSelector(httpMethodNode.ContentTypes.ToArray());

                if (bestMatchingContentType == HTTPContentType.ALL)
                {

                    // No content types defined...
                    if (!httpMethodNode.Any())
                        return (HTTPRequestHandleX.FromMethodNode(httpMethodNode), []);

                    // A single content type is defined...
                //    else if (_HTTPMethodNode.Count() == 1)
                        return (HTTPRequestHandleX.FromContentTypeNode(httpMethodNode.First()), []);

                //    else
                //        throw new ArgumentException(String.Concat(URL, " ", _HTTPMethodNode, " but multiple content type choices!"));

                }

                // The requested content type was found...
                else if (httpMethodNode.TryGet(bestMatchingContentType, out var httpContentTypeNode) && httpContentTypeNode is not null)
                    return (HTTPRequestHandleX.FromContentTypeNode(httpContentTypeNode), []);

                else
                    return (HTTPRequestHandleX.FromMethodNode(httpMethodNode), []);

            }

            //}

            // No HTTPMethod was found => return best matching URL Handler
            return (HTTPRequestHandleX.FromURLNode(bestMatch.URLNode), []);

            //return GetErrorHandler(Host, URL, HTTPMethod, HTTPContentType, HTTPStatusCode.BadRequest);

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
        internal void AddHandler(HTTPAPIX                    HTTPAPI,
                                 HTTPDelegate                HTTPDelegate,

                                 HTTPHostname?               Hostname                    = null,
                                 HTTPPath?                   URLTemplate                 = null,
                                 HTTPMethod?                 HTTPMethod                  = null,
                                 HTTPContentType?            HTTPContentType             = null,
                                 Boolean                     OpenEnd                     = false,

                                 HTTPAuthentication?         URLAuthentication           = null,
                                 HTTPAuthentication?         HTTPMethodAuthentication    = null,
                                 HTTPAuthentication?         ContentTypeAuthentication   = null,

                                 OnHTTPRequestLogDelegate?   HTTPRequestLogger           = null,
                                 OnHTTPResponseLogDelegate?  HTTPResponseLogger          = null,

                                 HTTPDelegate?               DefaultErrorHandler         = null,
                                 URLReplacement              AllowReplacement            = URLReplacement.Fail)

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
        public void AddMethodCallback(HTTPHostname                Hostname,
                                      HTTPMethod                  HTTPMethod,
                                      HTTPPath                    URLTemplate,
                                      HTTPContentType?            HTTPContentType             = null,
                                      Boolean                     OpenEnd                     = false,
                                      HTTPAuthentication?         URLAuthentication           = null,
                                      HTTPAuthentication?         HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?         ContentTypeAuthentication   = null,
                                      OnHTTPRequestLogDelegate?   HTTPRequestLogger           = null,
                                      OnHTTPResponseLogDelegate?  HTTPResponseLogger          = null,
                                      HTTPDelegate?               DefaultErrorHandler         = null,
                                      HTTPDelegate?               HTTPDelegate                = null,
                                      URLReplacement              AllowReplacement            = URLReplacement.Fail)

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
        public void AddMethodCallback(HTTPAPIX                    HTTPAPI,
                                      HTTPHostname                Hostname,
                                      HTTPMethod                  HTTPMethod,
                                      HTTPPath                    URLTemplate,
                                      HTTPContentType?            HTTPContentType             = null,
                                      Boolean                     OpenEnd                     = false,
                                      HTTPAuthentication?         URLAuthentication           = null,
                                      HTTPAuthentication?         HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?         ContentTypeAuthentication   = null,
                                      OnHTTPRequestLogDelegate?   HTTPRequestLogger           = null,
                                      OnHTTPResponseLogDelegate?  HTTPResponseLogger          = null,
                                      HTTPDelegate?               DefaultErrorHandler         = null,
                                      HTTPDelegate?               HTTPDelegate                = null,
                                      URLReplacement              AllowReplacement            = URLReplacement.Fail)

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
                               NetworkStream      Stream,
                               CancellationToken  CancellationToken   = default)

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

                var httpRequestHandle = GetRequestHandle(
                                            Request,
                                            out var errorResponse
                                        );

                if (httpRequestHandle.Item1 is not null)
                {

                    #region URL specific HTTP request logger

                    await LogEvent(
                              httpRequestHandle.Item1.HTTPRequestLogger,
                              loggingDelegate => loggingDelegate.Invoke(
                                  this,
                                  Request,
                                  CancellationToken
                              )
                          );

                    #endregion

                    #region Process HTTP request

                    var httpDelegate = httpRequestHandle.Item1.RequestHandler;
                    if (httpDelegate is not null)
                    {

                        try
                        {

                             Request.ParsedURLParametersX  = httpRequestHandle.Item2;
                             Request.NetworkStream         = Stream;

                             httpResponse                  = await httpDelegate(Request);

                        }
                        catch (Exception e)
                        {

                            DebugX.LogT("HTTP server request processing exception: " + e.Message);

                            httpResponse = new HTTPResponse.Builder(Request) {
                                               HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                              // Server          = DefaultServerName,
                                               ContentType     = HTTPContentType.Application.JSON_UTF8,
                                               Content         = JSONObject.Create(
                                                                     new JProperty("request",      Request.FirstPDULine),
                                                                     new JProperty("description",  e.Message),
                                                                     new JProperty("stackTrace",   e.StackTrace),
                                                                     new JProperty("source",       e.TargetSite?.Module.Name),
                                                                     new JProperty("type",         e.TargetSite?.ReflectedType?.Name)
                                                                 ).ToUTF8Bytes(),
                                               Connection      = ConnectionType.Close
                                           };

                        }

                    }

                    httpResponse ??= new HTTPResponse.Builder(Request) {
                                         HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                      //   Server          = DefaultServerName,
                                         ContentType     = HTTPContentType.Application.JSON_UTF8,
                                         Content         = JSONObject.Create(
                                                                 new JProperty("request",       Request.FirstPDULine),
                                                                 new JProperty("description",  "HTTP request handler must not be null!")
                                                             ).ToUTF8Bytes(),
                                         Connection      = ConnectionType.Close
                                     };

                    #endregion

                    #region URL specific HTTP response logger

                    await LogEvent(
                              httpRequestHandle.Item1.HTTPResponseLogger,
                              loggingDelegate => loggingDelegate.Invoke(
                                  this,
                                  httpResponse,
                                  CancellationToken
                              )
                          );

                    #endregion

                }


                if (errorResponse == "This HTTP method is not allowed!")
                    httpResponse = new HTTPResponse.Builder(Request) {
                                       HTTPStatusCode  = HTTPStatusCode.MethodNotAllowed,
                                       Server          = Request.Host.ToString(),
                                       Date            = Timestamp.Now,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = errorResponse.ToUTF8Bytes()
                             //          Connection      = ConnectionType.Close
                                   };

                httpResponse ??= new HTTPResponse.Builder(Request) {
                                     HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                    // Server          = DefaultServerName,
                                     ContentType     = HTTPContentType.Application.JSON_UTF8,
                                     Content         = JSONObject.Create(
                                                           new JProperty("request",      Request.FirstPDULine),
                                                           new JProperty("description",  errorResponse)
                                                       ).ToUTF8Bytes()
                                  //   Connection      = ConnectionType.Close
                                 };

            }


            #region Status Code 4xx or 5xx => Log HTTP Error Response...

            if (httpResponse.HTTPStatusCode.Code > 400 &&
                httpResponse.HTTPStatusCode.Code <= 599)
            {

                await LogEvent(
                      OnHTTPError,
                      loggingDelegate => loggingDelegate.Invoke(
                          this,
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
                          httpResponse,
                          CancellationToken
                      )
                  );

            #endregion


            return httpResponse;

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
