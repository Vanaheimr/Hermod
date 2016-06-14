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
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public class HTTPServer<T, U> : HTTPServer
        where T : IEnumerable<U>
    {

        #region Data

        public readonly ConcurrentDictionary<HTTPHostname, T> _Multitenancy;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Initialize the HTTP server using the given parameters.
        /// </summary>
        /// <param name="TCPPort">An IP port to listen on.</param>
        /// <param name="DefaultServerName">The default HTTP servername, used whenever no HTTP Host-header had been given.</param>
        /// <param name="X509Certificate">Use this X509 certificate for TLS.</param>
        /// <param name="CallingAssemblies">A list of calling assemblies to include e.g. into embedded ressources lookups.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="Autostart">Start the HTTP server thread immediately (default: no).</param>
        public HTTPServer(IPPort                            TCPPort                           = null,
                          String                            DefaultServerName                 = DefaultHTTPServerName,
                          X509Certificate2                  X509Certificate                   = null,
                          IEnumerable<Assembly>             CallingAssemblies                 = null,
                          String                            ServerThreadName                  = null,
                          ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                          Boolean                           ServerThreadIsBackground          = true,
                          ConnectionIdBuilder               ConnectionIdBuilder               = null,
                          ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder      = null,
                          ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder  = null,
                          Boolean                           ConnectionThreadsAreBackground    = true,
                          TimeSpan?                         ConnectionTimeout                 = null,
                          UInt32                            MaxClientConnections              = TCPServer.__DefaultMaxClientConnections,
                          DNSClient                         DNSClient                         = null,
                          Boolean                           Autostart                         = false)

            : base(TCPPort,
                   DefaultServerName,
                   X509Certificate,
                   CallingAssemblies,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameBuilder,
                   ConnectionThreadsPriorityBuilder,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout,
                   MaxClientConnections,
                   DNSClient,
                   Autostart)

        {

            this._Multitenancy = new ConcurrentDictionary<HTTPHostname, T>();

        }

        #endregion


        #region GetAllRoamingNetworks(Hostname)

        /// <summary>
        /// Return all roaming networks available for the given hostname.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        public IEnumerable<U> GetAllRoamingNetworks(HTTPHostname  Hostname)
        {

            T RoamingNetworks = default(T);

            var Set = new HashSet<U>();

            if (_Multitenancy.TryGetValue(Hostname, out RoamingNetworks))
                foreach (var RoamingNetwork in RoamingNetworks)
                    Set.Add(RoamingNetwork);

            if (_Multitenancy.TryGetValue(Hostname.AnyHost, out RoamingNetworks))
                foreach (var RoamingNetwork in RoamingNetworks)
                    Set.Add(RoamingNetwork);

            if (_Multitenancy.TryGetValue(Hostname.AnyPort, out RoamingNetworks))
                foreach (var RoamingNetwork in RoamingNetworks)
                    Set.Add(RoamingNetwork);

            if (_Multitenancy.TryGetValue(Vanaheimr.Hermod.HTTP.HTTPHostname.Any, out RoamingNetworks))
                foreach (var RoamingNetwork in RoamingNetworks)
                    Set.Add(RoamingNetwork);

            return Set;//.OrderBy(rn => rn.Id);

        }

        #endregion

        #region GetRoamingNetwork(Hostname, RoamingNetworkId)

        ///// <summary>
        ///// Return all roaming networks available for the given hostname.
        ///// </summary>
        ///// <param name="Hostname">The HTTP hostname.</param>
        ///// <param name="RoamingNetworkId">The unique identification of the new roaming network.</param>
        //public U GetRoamingNetwork(HTTPHostname       Hostname,
        //                                        RoamingNetwork_Id  RoamingNetworkId)
        //{

        //    return GetAllRoamingNetworks(Hostname).
        //               Where(roamingnetwork => roamingnetwork.Id == RoamingNetworkId).
        //               FirstOrDefault();

        //}

        #endregion

        #region TryGetRoamingNetworks(Hostname, out RoamingNetwork)

        /// <summary>
        ///Try to return all roaming networks available for the given hostname.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="RoamingNetworks">A roaming network.</param>
        public Boolean TryGetRoamingNetworks(HTTPHostname  Hostname,
                                             out T         RoamingNetworks)
        {

            return _Multitenancy.TryGetValue(Hostname, out RoamingNetworks);

        }

        #endregion

        #region TryAddRoamingNetworks(Hostname, RoamingNetworks)

        /// <summary>
        ///Try to return all roaming networks available for the given hostname.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="RoamingNetworks">A roaming network.</param>
        public Boolean TryAddRoamingNetworks(HTTPHostname  Hostname,
                                             T             RoamingNetworks)
        {

            return _Multitenancy.TryAdd(Hostname, RoamingNetworks);

        }

        #endregion

    }


    /// <summary>
    /// A HTTP/1.1 server.
    /// </summary>
    public class HTTPServer : ATCPServers,
                              IBoomerangSender<String, DateTime, HTTPRequest, HTTPResponse>
    {

        #region Data

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public  const           String             DefaultHTTPServerName  = "GraphDefined Hermod HTTP Service v0.9";

        /// <summary>
        /// The default HTTP server TCP port.
        /// </summary>
        public static readonly  IPPort             DefaultHTTPServerPort  = new IPPort(80);

        private readonly        URIMapping         _URIMapping;

        private readonly        HTTPProcessor      _HTTPProcessor;

        #endregion

        #region Properties

        #region DefaultServerName

        private readonly String _DefaultServerName;

        /// <summary>
        /// The default HTTP servername, used whenever
        /// no HTTP Host-header had been given.
        /// </summary>
        public String DefaultServerName
        {
            get
            {
                return _DefaultServerName;
            }
        }

        #endregion

        #region HTTPSecurity

        private readonly HTTPSecurity _HTTPSecurity;

        /// <summary>
        /// An associated HTTP security object.
        /// </summary>
        public HTTPSecurity HTTPSecurity
        {
            get
            {
                return _HTTPSecurity;
            }
        }

        #endregion

        #endregion

        #region Events

        public event BoomerangSenderHandler<String, DateTime, HTTPRequest, HTTPResponse>  OnNotification;

        /// <summary>
        /// An event called whenever a request came in.
        /// </summary>
        public event RequestLogHandler                                                    RequestLog;

        /// <summary>
        /// An event called whenever a request could successfully be processed.
        /// </summary>
        public event AccessLogHandler                                                     AccessLog;

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        public event ErrorLogHandler                                                      ErrorLog;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Initialize the HTTP server using the given parameters.
        /// </summary>
        /// <param name="TCPPort">A TCP port to listen on.</param>
        /// <param name="DefaultServerName">The default HTTP servername, used whenever no HTTP Host-header had been given.</param>
        /// <param name="X509Certificate">Use this X509 certificate for TLS.</param>
        /// <param name="CallingAssemblies">A list of calling assemblies to include e.g. into embedded ressources lookups.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="Autostart">Start the HTTP server thread immediately (default: no).</param>
        public HTTPServer(IPPort                            TCPPort                           = null,
                          String                            DefaultServerName                 = DefaultHTTPServerName,
                          X509Certificate2                  X509Certificate                   = null,
                          IEnumerable<Assembly>             CallingAssemblies                 = null,
                          String                            ServerThreadName                  = null,
                          ThreadPriority                    ServerThreadPriority              = ThreadPriority.AboveNormal,
                          Boolean                           ServerThreadIsBackground          = true,
                          ConnectionIdBuilder               ConnectionIdBuilder               = null,
                          ConnectionThreadsNameBuilder      ConnectionThreadsNameBuilder      = null,
                          ConnectionThreadsPriorityBuilder  ConnectionThreadsPriorityBuilder  = null,
                          Boolean                           ConnectionThreadsAreBackground    = true,
                          TimeSpan?                         ConnectionTimeout                 = null,
                          UInt32                            MaxClientConnections              = TCPServer.__DefaultMaxClientConnections,
                          DNSClient                         DNSClient                         = null,
                          Boolean                           Autostart                         = false)

            : base(DefaultServerName,
                   X509Certificate,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameBuilder,
                   ConnectionThreadsPriorityBuilder,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout,
                   MaxClientConnections,
                   DNSClient,
                   false)

        {

            this._DefaultServerName         = DefaultServerName;
            this._URIMapping                = new URIMapping();

            _HTTPProcessor                  = new HTTPProcessor(this);
            _HTTPProcessor.OnNotification  += ProcessBoomerang;
            _HTTPProcessor.RequestLog      += (HTTPProcessor, ServerTimestamp, Request)                                 => LogRequest(ServerTimestamp, Request);
            _HTTPProcessor.AccessLog       += (HTTPProcessor, ServerTimestamp, Request, Response)                       => LogAccess (ServerTimestamp, Request, Response);
            _HTTPProcessor.ErrorLog        += (HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException) => LogError  (ServerTimestamp, Request, Response, Error, LastException);

            if (TCPPort != null)
                this.AttachTCPPort(TCPPort);
            else
                this.AttachTCPPort(DefaultHTTPServerPort);

            if (Autostart)
                Start();

        }

        #endregion


        // Manage the underlying TCP sockets...

        #region AttachTCPPort(Port)

        public HTTPServer AttachTCPPort(IPPort Port)
        {

            this.AttachTCPPorts(Port);

            return this;

        }

        #endregion

        #region AttachTCPPorts(params Ports)

        public HTTPServer AttachTCPPorts(params IPPort[] Ports)
        {

            AttachTCPPorts(_TCPServer => _TCPServer.SendTo(_HTTPProcessor), Ports);

            return this;

        }

        #endregion

        #region AttachTCPSocket(Socket)

        public HTTPServer AttachTCPSocket(IPSocket Socket)
        {

            this.AttachTCPSockets(Socket);

            return this;

        }

        #endregion

        #region AttachTCPSockets(params Sockets)

        public HTTPServer AttachTCPSockets(params IPSocket[] Sockets)
        {

            AttachTCPSockets(_TCPServer => _TCPServer.SendTo(_HTTPProcessor), Sockets);

            return this;

        }

        #endregion


        #region DetachTCPPort(Port)

        public HTTPServer DetachTCPPort(IPPort Port)
        {

            DetachTCPPorts(Port);

            return this;

        }

        #endregion

        #region DetachTCPPorts(params Sockets)

        public HTTPServer DetachTCPPorts(params IPPort[] Ports)
        {

            DetachTCPPorts(_TCPServer => {
                               _TCPServer.OnNotification      -= _HTTPProcessor.ProcessArrow;
                               _TCPServer.OnExceptionOccured  -= _HTTPProcessor.ProcessExceptionOccured;
                               _TCPServer.OnCompleted         -= _HTTPProcessor.ProcessCompleted;
                           },
                           Ports);

            return this;

        }

        #endregion


        // Events

        #region ProcessBoomerang(ConnectionId, Timestamp, HTTPRequest)

        private HTTPResponse ProcessBoomerang(String       ConnectionId,
                                              DateTime     Timestamp,
                                              HTTPRequest  HTTPRequest)
        {

            #region 1) Invoke delegate based on URIMapping

            HTTPResponse URIMappingResponse = null;

            try
            {
                URIMappingResponse = InvokeHandler(HTTPRequest);
            }
            catch (Exception e)
            {

                while (e.InnerException != null)
                    e = e.InnerException;

                return new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description",  e.Message),
                                          new JProperty("stacktrace",   e.StackTrace),
                                          new JProperty("source",       e.TargetSite.Module.Name),
                                          new JProperty("type",         e.TargetSite.ReflectedType.Name)
                                      ).ToUTF8Bytes(),
                    Server          = _DefaultServerName,
                    Connection      = "close"
                };

            }

            #endregion

            #region 2) Call OnNotification delegate, but in most cases just ignore the result!

            HTTPResponse OnNotificationResponse = null;

            try
            {

                var OnNotificationLocal = OnNotification;
                if (OnNotificationLocal != null)
                    OnNotificationResponse = OnNotificationLocal(ConnectionId,
                                                                 Timestamp,
                                                                 HTTPRequest);

            }
            catch (Exception e)
            {

                while (e.InnerException != null)
                    e = e.InnerException;

                return new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description",  e.Message),
                                          new JProperty("stacktrace",   e.StackTrace),
                                          new JProperty("source",       e.TargetSite.Module.Name),
                                          new JProperty("type",         e.TargetSite.ReflectedType.Name)
                                      ).ToUTF8Bytes(),
                    Server          = _DefaultServerName,
                    Connection      = "close"
                };

            }

            #endregion

            #region Return result or fail!

            if (URIMappingResponse     != null)
                return URIMappingResponse;

            if (OnNotificationResponse != null)
                return OnNotificationResponse;

            return new HTTPResponseBuilder(HTTPRequest) {
                HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = JSONObject.Create(
                                      new JProperty("description", "URIMappingResponse AND OnNotificationResponse in ProcessBoomerang() had been null!")
                                  ).ToUTF8Bytes(),
                Server          = _DefaultServerName,
                Connection      = "close"
            };

            #endregion

        }

        #endregion


        // HTTP Logging...

        #region (internal) LogRequest(Timestamp, Request)

        /// <summary>
        /// Log an incoming request.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        internal void LogRequest(DateTime     Timestamp,
                                 HTTPRequest  Request)
        {

            var RequestLogLocal = RequestLog;
            if (RequestLogLocal != null)
                RequestLogLocal(Timestamp, this, Request);

        }

        #endregion

        #region (internal) LogAccess(Timestamp, Request, Response)

        /// <summary>
        /// Log an successful request processing.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        /// <param name="Response">The outgoing response.</param>
        internal void LogAccess(DateTime      Timestamp,
                                HTTPRequest   Request,
                                HTTPResponse  Response)
        {

            var AccessLogLocal = AccessLog;
            if (AccessLogLocal != null)
                AccessLogLocal(Timestamp, this, Request, Response);

        }

        #endregion

        #region (internal) LogError(Timestamp, Request, Response, Error = null, LastException = null)

        /// <summary>
        /// Log an error during request processing.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        /// <param name="Response">The outgoing response.</param>
        /// <param name="Error">The occured error.</param>
        /// <param name="LastException">The last occured exception.</param>
        internal void LogError(DateTime      Timestamp,
                               HTTPRequest   Request,
                               HTTPResponse  Response,
                               String        Error          = null,
                               Exception     LastException  = null)
        {

            var ErrorLogLocal = ErrorLog;
            if (ErrorLogLocal != null)
                ErrorLogLocal(Timestamp, this, Request, Response, Error, LastException);

        }

        #endregion



        #region Method Callbacks

        #region Redirect(Hostname, HTTPMethod, URITemplate, HTTPContentType, URITarget)

        /// <summary>
        /// Add a URI based method redirect for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URITarget">The target URI of the redirect.</param>
        public void Redirect(HTTPHostname     Hostname,
                             HTTPMethod       HTTPMethod,
                             String           URITemplate,
                             HTTPContentType  HTTPContentType,
                             String           URITarget)

        {

            _URIMapping.AddHandler(req => _URIMapping.InvokeHandler(new HTTPRequestBuilder(req).SetURI(URITarget)),
                                   Hostname,
                                   (URITemplate.IsNotNullOrEmpty()) ? URITemplate     : "/",
                                   HTTPMethod      ?? HTTPMethod.GET,
                                   HTTPContentType ?? HTTPContentType.HTML_UTF8,
                                   null,
                                   null,
                                   null,
                                   null);

        }

        #endregion

        #region Redirect(HTTPMethod, URITemplate, HTTPContentType, URITarget)

        /// <summary>
        /// Add a URI based method redirect for the given URI template.
        /// </summary>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URITarget">The target URI of the redirect.</param>
        public void Redirect(HTTPMethod       HTTPMethod,
                             String           URITemplate,
                             HTTPContentType  HTTPContentType,
                             String           URITarget)

        {

            _URIMapping.AddHandler(req => _URIMapping.InvokeHandler(new HTTPRequestBuilder(req).SetURI(URITarget)),
                                   HTTPHostname.Any,
                                   (URITemplate.IsNotNullOrEmpty()) ? URITemplate     : "/",
                                   HTTPMethod      ?? HTTPMethod.GET,
                                   HTTPContentType ?? HTTPContentType.HTML_UTF8,
                                   null,
                                   null,
                                   null,
                                   null);

        }

        #endregion


        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate, HTTPContentType = null, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname        Hostname,
                                      HTTPMethod          HTTPMethod,
                                      String              URITemplate,
                                      HTTPContentType     HTTPContentType             = null,
                                      HTTPAuthentication  HostAuthentication          = null,
                                      HTTPAuthentication  URIAuthentication           = null,
                                      HTTPAuthentication  HTTPMethodAuthentication    = null,
                                      HTTPAuthentication  ContentTypeAuthentication   = null,
                                      HTTPDelegate        HTTPDelegate                = null,
                                      URIReplacement      AllowReplacement            = URIReplacement.Fail)

        {

            #region Initial checks

            if (Hostname == null)
                throw new ArgumentNullException(nameof(Hostname),      "The given HTTP hostname must not be null!");

            if (HTTPMethod == null)
                throw new ArgumentNullException(nameof(HTTPMethod),    "The given HTTP method must not be null!");

            if (URITemplate.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(URITemplate),   "The given URI template must not be null or empty!");

            if (HTTPDelegate == null)
                throw new ArgumentNullException(nameof(HTTPDelegate),  "The given HTTP delegate must not be null!");

            #endregion

            _URIMapping.AddHandler(HTTPDelegate,
                                   Hostname,
                                   URITemplate,
                                   HTTPMethod ?? HTTPMethod.GET,
                                   HTTPContentType,
                                   HostAuthentication,
                                   URIAuthentication,
                                   HTTPMethodAuthentication,
                                   ContentTypeAuthentication,
                                   null,
                                   AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplates, HTTPContentType, HTTPDelegate, AllowReplacement = false)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplates">The URI templates.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname         Hostname,
                                      HTTPMethod           HTTPMethod,
                                      IEnumerable<String>  URITemplates,
                                      HTTPContentType      HTTPContentType             = null,
                                      HTTPAuthentication   HostAuthentication          = null,
                                      HTTPAuthentication   URIAuthentication           = null,
                                      HTTPAuthentication   HTTPMethodAuthentication    = null,
                                      HTTPAuthentication   ContentTypeAuthentication   = null,
                                      HTTPDelegate         HTTPDelegate                = null,
                                      URIReplacement       AllowReplacement            = URIReplacement.Fail)

        {

            #region Initial checks

            if (HTTPMethod == null)
                throw new ArgumentNullException(nameof(HTTPMethod),       "The given HTTP method must not be null!");

            if (URITemplates == null || !URITemplates.Any())
                throw new ArgumentNullException(nameof(URITemplates),     "The given URI template must not be null or empty!");

            if (HTTPContentType == null)
                throw new ArgumentNullException(nameof(HTTPContentType),  "The given HTTP content type must not be null!");

            if (HTTPDelegate == null)
                throw new ArgumentNullException(nameof(HTTPDelegate),     "The given HTTP delegate must not be null!");

            #endregion

            URITemplates.ForEach(URITemplate =>
                _URIMapping.AddHandler(HTTPDelegate,
                                       Hostname,
                                       URITemplate,
                                       HTTPMethod ?? HTTPMethod.GET,
                                       HTTPContentType,
                                       HostAuthentication,
                                       URIAuthentication,
                                       HTTPMethodAuthentication,
                                       ContentTypeAuthentication,
                                       null,
                                       AllowReplacement));

        }

        #endregion


        #region GetHandler(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        public HTTPDelegate GetHandler(HTTPHostname                              Host,
                                       String                                    URI,
                                       HTTPMethod                                HTTPMethod                   = null,
                                       Func<HTTPContentType[], HTTPContentType>  HTTPContentTypeSelector      = null,
                                       Action<IEnumerable<String>>               ParsedURIParametersDelegate  = null)
        {

            return _URIMapping.GetHandler(Host,
                                          URI,
                                          HTTPMethod,
                                          HTTPContentTypeSelector,
                                          ParsedURIParametersDelegate);

        }

        #endregion

        #region InvokeHandler(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        public HTTPResponse InvokeHandler(HTTPRequest HTTPRequest)
        {

            return _URIMapping.InvokeHandler(HTTPRequest);

        }

        #endregion

        #endregion

        #region HTTP Server Sent Events

        #region AddEventSource(EventIdentification)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        public HTTPEventSource AddEventSource(String  EventIdentification)
        {
            return _URIMapping.AddEventSource(EventIdentification, 100, TimeSpan.FromSeconds(5));
        }

        #endregion

        #region AddEventSource(EventIdentification, MaxNumberOfCachedEvents, RetryIntervall = null)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        public HTTPEventSource AddEventSource(String     EventIdentification,
                                              UInt32     MaxNumberOfCachedEvents,
                                              TimeSpan?  RetryIntervall  = null)
        {

            return _URIMapping.AddEventSource(EventIdentification,
                                              MaxNumberOfCachedEvents,
                                              RetryIntervall);

        }

        #endregion

        #region AddEventSource(MethodInfo, Host, URITemplate, HTTPMethod, EventIdentification, MaxNumberOfCachedEvents = 500, RetryIntervall = null, IsSharedEventSource = false, HostAuthentication = false, URIAuthentication = false)

        /// <summary>
        /// Add a method call back for the given URI template and
        /// add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public HTTPEventSource AddEventSource(String              EventIdentification,
                                              UInt32              MaxNumberOfCachedEvents     = 500,
                                              TimeSpan?           RetryIntervall              = null,

                                              HTTPHostname        Hostname                    = null,
                                              String              URITemplate                 = "/",
                                              HTTPMethod          HTTPMethod                  = null,
                                              HTTPContentType     HTTPContentType             = null,

                                              HTTPAuthentication  HostAuthentication          = null,
                                              HTTPAuthentication  URIAuthentication           = null,
                                              HTTPAuthentication  HTTPMethodAuthentication    = null,

                                              HTTPDelegate        DefaultErrorHandler         = null)

        {

            return _URIMapping.AddEventSource(EventIdentification,
                                              MaxNumberOfCachedEvents,
                                              RetryIntervall,

                                              Hostname,
                                              URITemplate,
                                              HTTPMethod,
                                              HTTPContentType,

                                              HostAuthentication,
                                              URIAuthentication,
                                              HTTPMethodAuthentication,

                                              DefaultErrorHandler);

        }

        #endregion


        #region GetEventSource(EventSourceIdentification)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public HTTPEventSource GetEventSource(String EventSourceIdentification)
        {
            return _URIMapping.GetEventSource(EventSourceIdentification);
        }

        #endregion

        #region UseEventSource(EventSourceIdentification, Action)

        /// <summary>
        /// Call the given delegate for the event source identified
        /// by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="Action">A delegate.</param>
        public void UseEventSource(String                   EventSourceIdentification,
                                   Action<HTTPEventSource>  Action)
        {

            if (Action == null)
                return;

            var EventSource = _URIMapping.GetEventSource(EventSourceIdentification);

            if (EventSource != null)
                Action(EventSource);

        }

        #endregion

        #region UseEventSource(EventSourceIdentification, DataSource, Action)

        /// <summary>
        /// Call the given delegate for the event source identified
        /// by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="DataSource">A enumeration of data.</param>
        /// <param name="Action">A delegate.</param>
        public void UseEventSource<T>(String                      EventSourceIdentification,
                                      IEnumerable<T>              DataSource,
                                      Action<HTTPEventSource, T>  Action)
        {

            if (DataSource == null || !DataSource.Any() || Action == null)
                return;

            var EventSource = _URIMapping.GetEventSource(EventSourceIdentification);

            if (EventSource != null)
                foreach (var Data in DataSource)
                    Action(EventSource, Data);

        }

        #endregion

        #region TryGetEventSource(EventSourceIdentification, EventSource)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        public Boolean TryGetEventSource(String EventSourceIdentification, out HTTPEventSource EventSource)
        {
            return _URIMapping.TryGetEventSource(EventSourceIdentification, out EventSource);
        }

        #endregion

        #region GetEventSources(EventSourceSelector = null)

        /// <summary>
        /// An enumeration of all event sources.
        /// </summary>
        public IEnumerable<HTTPEventSource> GetEventSources(Func<HTTPEventSource, Boolean> EventSourceSelector = null)
        {
            return GetEventSources(EventSourceSelector);
        }

        #endregion

        #endregion


        #region GetErrorHandler(Host, URL, HTTPMethod = null, HTTPContentType = null, HTTPStatusCode = null)

        /// <summary>
        /// Return the best matching error handler for the given parameters.
        /// </summary>
        public Tuple<MethodInfo, IEnumerable<Object>> GetErrorHandler(String           Host,
                                                                      String           URL, 
                                                                      HTTPMethod       HTTPMethod       = null,
                                                                      HTTPContentType  HTTPContentType  = null,
                                                                      HTTPStatusCode   HTTPStatusCode   = null)

        {

            return _URIMapping.GetErrorHandler(Host,
                                               URL,
                                               HTTPMethod,
                                               HTTPContentType,
                                               HTTPStatusCode);

        }

        #endregion


    }

}
