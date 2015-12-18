/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using System.Diagnostics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP/1.1 server.
    /// </summary>
    public class HTTPServer : ATCPServers,
                              IBoomerangSender<String, DateTime, HTTPRequest, HTTPResponse>
    {

        #region Data

        internal const    String             __DefaultServerName  = "Vanaheimr Hermod HTTP Service v0.9";
        private readonly  URIMapping         _URIMapping;

        private readonly  HTTPProcessor      _HTTPProcessor;

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

        #region HTTPRoot

        private readonly String _HTTPRoot;

        public String HTTPRoot
        {
            get
            {
                return _HTTPRoot;
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


        #region CallingAssemblies

        private readonly IList<Assembly> _CallingAssemblies;

        /// <summary>
        /// The list of calling assemblies.
        /// </summary>
        public IEnumerable<Assembly> CallingAssemblies
        {
            get
            {
                return _CallingAssemblies;
            }
        }

        #endregion

        #region AllResources

        private readonly Dictionary<String, Assembly> _AllResources;

        /// <summary>
        /// All discovered embedded resources.
        /// </summary>
        public Dictionary<String, Assembly> AllResources
        {
            get
            {
                return _AllResources;
            }
        }

        #endregion

        #endregion

        #region Events

        public event BoomerangSenderHandler<String, DateTime, HTTPRequest, HTTPResponse>    OnNotification;

        /// <summary>
        /// An event called whenever a request came in.
        /// </summary>
        public event RequestLogHandler                                                      RequestLog;

        /// <summary>
        /// An event called whenever a request could successfully be processed.
        /// </summary>
        public event AccessLogHandler                                                       AccessLog;

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        public event ErrorLogHandler                                                        ErrorLog;

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
                          String                            DefaultServerName                 = __DefaultServerName,
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

            this._DefaultServerName                = DefaultServerName;
            this._HTTPRoot                         = HTTPRoot;
            this._URIMapping                       = new URIMapping();
            this._CallingAssemblies                = new List<Assembly>() { Assembly.GetExecutingAssembly(), typeof(HTTPServer).Assembly };

            if (CallingAssemblies != null)
            {

                this._CallingAssemblies.AddAndReturnList(CallingAssemblies);

                CallingAssemblies.
                    SelectMany(_Assembly => _Assembly.GetManifestResourceNames().
                                                      Select(_Resource => new {
                                                          Assembly   = _Assembly,
                                                          Ressource  = _Resource
                                                      })).
                    ForEach(v => this._AllResources.Add(v.Ressource, v.Assembly));

            }

            this._AllResources                     = new Dictionary<String, Assembly>();

            _HTTPProcessor                         = new HTTPProcessor(DefaultServerName);
            _HTTPProcessor.OnNotification         += ProcessBoomerang;
            _HTTPProcessor.RequestLog             += (HTTPProcessor, ServerTimestamp, Request)                                 => LogRequest(ServerTimestamp, Request);
            _HTTPProcessor.AccessLog              += (HTTPProcessor, ServerTimestamp, Request, Response)                       => LogAccess (ServerTimestamp, Request, Response);
            _HTTPProcessor.ErrorLog               += (HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException) => LogError  (ServerTimestamp, Request, Response, Error, LastException);

            if (TCPPort != null)
                this.AttachTCPPort(TCPPort);

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

            base.AttachTCPPorts(_TCPServer => _TCPServer.SendTo(_HTTPProcessor), Ports);

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

            base.AttachTCPSockets(_TCPServer => _TCPServer.SendTo(_HTTPProcessor), Sockets);

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

            base.DetachTCPPorts(_TCPServer => {
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

            HTTPResponse URIMappingResponse      = null;
            HTTPResponse OnNotificationResponse  = null;

            #region 1) Invoke delegate based on URIMapping

            try
            {
                URIMappingResponse = InvokeHandler(HTTPRequest);
            }
            catch (Exception e)
            {

                while (e.InnerException != null)
                    e = e.InnerException;

                var ErrorMessage = new JObject(new JProperty("description",  e.Message),
                                               new JProperty("stacktrace",   e.StackTrace),
                                               new JProperty("source",       e.TargetSite.Module.Name),
                                               new JProperty("type",         e.TargetSite.ReflectedType.Name));

                Debug.WriteLine("HTTPServer => InternalServerError" + Environment.NewLine + ErrorMessage.ToString());

                return new HTTPResponseBuilder() {
                    HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = ErrorMessage.ToUTF8Bytes(),
                    Server          = _DefaultServerName,
                    Connection      = "close"
                };

            }

            #endregion

            #region 2) Call OnNotification delegate, but in most cases just ignore the result!

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

                var ErrorMessage = new JObject(new JProperty("description",  e.Message),
                                               new JProperty("stacktrace",   e.StackTrace),
                                               new JProperty("source",       e.TargetSite.Module.Name),
                                               new JProperty("type",         e.TargetSite.ReflectedType.Name));

                Debug.WriteLine("HTTPServer => InternalServerError" + Environment.NewLine + ErrorMessage.ToString());

                return new HTTPResponseBuilder() {
                    HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = ErrorMessage.ToUTF8Bytes(),
                    Server          = _DefaultServerName,
                    Connection      = "close"
                };

            }

            #endregion

            #region Return result or fail!

            if (URIMappingResponse != null)
                return URIMappingResponse;

            if (OnNotificationResponse != null)
                return OnNotificationResponse;

            Debug.WriteLine("URIMappingResponse AND OnNotificationResponse in ProcessBoomerang() had been null!");

            return new HTTPResponseBuilder() {
                HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(
                                      new JProperty("description", "URIMappingResponse AND OnNotificationResponse in ProcessBoomerang() had been null!")
                                  ).ToUTF8Bytes(),
                Server          = _DefaultServerName,
                Connection      = "close"
            };

            #endregion

        }

        #endregion







        // HTTP Logging...

        #region LogRequest(ServerTimestamp, Request)

        /// <summary>
        /// Log an incoming request.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        public void LogRequest(DateTime     ServerTimestamp,
                               HTTPRequest  Request)
        {

            var RequestLogLocal = RequestLog;

            if (RequestLogLocal != null)
                RequestLogLocal(this, ServerTimestamp, Request);

        }

        #endregion

        #region LogAccess(ServerTimestamp, Request, Response)

        /// <summary>
        /// Log an successful request processing.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        /// <param name="Response">The outgoing response.</param>
        public void LogAccess(DateTime      ServerTimestamp,
                              HTTPRequest   Request,
                              HTTPResponse  Response)
        {

            var AccessLogLocal = AccessLog;

            if (AccessLogLocal != null)
                AccessLogLocal(this, ServerTimestamp, Request, Response);

        }

        #endregion

        #region LogError(ServerTimestamp, Request, Response, Error = null, LastException = null)

        /// <summary>
        /// Log an error during request processing.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        /// <param name="HTTPResponse">The outgoing response.</param>
        /// <param name="Error">The occured error.</param>
        /// <param name="LastException">The last occured exception.</param>
        public void LogError(DateTime      ServerTimestamp,
                             HTTPRequest   Request,
                             HTTPResponse  Response,
                             String        Error          = null,
                             Exception     LastException  = null)
        {

            var ErrorLogLocal = ErrorLog;

            if (ErrorLogLocal != null)
                ErrorLogLocal(this, ServerTimestamp, Request, Response, Error, LastException);

        }

        #endregion



        #region Add Method Callbacks

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate, HTTPContentType, HTTPDelegate)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(String           Hostname,
                                      HTTPMethod       HTTPMethod,
                                      String           URITemplate,
                                      HTTPContentType  HTTPContentType,
                                      HTTPDelegate     HTTPDelegate)

        {

            #region Initial checks

            if (Hostname.IsNullOrEmpty())
                throw new ArgumentNullException("Hostname",         "The given parameter must not be null or empty!");

            if (HTTPMethod == null)
                throw new ArgumentNullException("HTTPMethod",       "The given parameter must not be null!");

            if (URITemplate.IsNullOrEmpty())
                throw new ArgumentNullException("URITemplate",      "The given parameter must not be null or empty!");

            if (HTTPContentType == null)
                throw new ArgumentNullException("HTTPContentType",  "The given parameter must not be null!");

            if (HTTPDelegate == null)
                throw new ArgumentNullException("HTTPDelegate",     "The given parameter must not be null!");

            #endregion

            _URIMapping.AddHandler(HTTPDelegate,
                                   Hostname,
                                   URITemplate,
                                   HTTPMethod,
                                   HTTPContentType,
                                   null,
                                   null,
                                   null,
                                   null);

        }

        #endregion

        #region AddMethodCallback(HTTPMethod, URITemplate, HTTPContentType, HTTPDelegate)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPMethod       HTTPMethod,
                                      String           URITemplate,
                                      HTTPContentType  HTTPContentType,
                                      HTTPDelegate     HTTPDelegate,
                                      Boolean          AllowReplacement = false)

        {

            #region Initial checks

            if (HTTPMethod == null)
                throw new ArgumentNullException("HTTPMethod", "The given parameter must not be null!");

            if (URITemplate.IsNullOrEmpty())
                throw new ArgumentNullException("URITemplate", "The given parameter must not be null or empty!");

            if (HTTPContentType == null)
                throw new ArgumentNullException("HTTPContentType", "The given parameter must not be null!");

            if (HTTPDelegate == null)
                throw new ArgumentNullException("HTTPDelegate", "The given parameter must not be null!");

            #endregion

            _URIMapping.AddHandler(HTTPDelegate,
                                   "*",
                                   URITemplate,
                                   HTTPMethod,
                                   HTTPContentType,
                                   null,
                                   null,
                                   null,
                                   null,
                                   AllowReplacement: AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplates, HTTPContentType, HTTPDelegate)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplates">The URI templates.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(String               Hostname,
                                      HTTPMethod           HTTPMethod,
                                      IEnumerable<String>  URITemplates,
                                      HTTPContentType      HTTPContentType,
                                      HTTPDelegate         HTTPDelegate)

        {

            #region Initial checks

            if (Hostname.IsNullOrEmpty())
                throw new ArgumentNullException("Hostname",         "The given parameter must not be null or empty!");

            if (HTTPMethod == null)
                throw new ArgumentNullException("HTTPMethod",       "The given parameter must not be null!");

            if (URITemplates == null || !URITemplates.Any())
                throw new ArgumentNullException("URITemplates",     "The given parameter must not be null or empty!");

            if (HTTPContentType == null)
                throw new ArgumentNullException("HTTPContentType",  "The given parameter must not be null!");

            if (HTTPDelegate == null)
                throw new ArgumentNullException("HTTPDelegate",     "The given parameter must not be null!");

            #endregion

            URITemplates.ForEach(URITemplate =>
                _URIMapping.AddHandler(HTTPDelegate,
                                       Hostname,
                                       URITemplate,
                                       HTTPMethod,
                                       HTTPContentType,
                                       null,
                                       null,
                                       null,
                                       null));

        }

        #endregion

        #region AddMethodCallback(HTTPMethod, URITemplates, HTTPContentType, HTTPDelegate)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplates">The URI templates.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPMethod           HTTPMethod,
                                      IEnumerable<String>  URITemplates,
                                      HTTPContentType      HTTPContentType,
                                      HTTPDelegate         HTTPDelegate)

        {

            #region Initial checks

            if (HTTPMethod == null)
                throw new ArgumentNullException("HTTPMethod",       "The given parameter must not be null!");

            if (URITemplates == null || !URITemplates.Any())
                throw new ArgumentNullException("URITemplates",     "The given parameter must not be null or empty!");

            if (HTTPContentType == null)
                throw new ArgumentNullException("HTTPContentType",  "The given parameter must not be null!");

            if (HTTPDelegate == null)
                throw new ArgumentNullException("HTTPDelegate",     "The given parameter must not be null!");

            #endregion

            URITemplates.ForEach(URITemplate =>
                _URIMapping.AddHandler(HTTPDelegate,
                                       "*",
                                       URITemplate,
                                       HTTPMethod,
                                       HTTPContentType,
                                       null,
                                       null,
                                       null,
                                       null));

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
                                   "*",
                                   (URITemplate.IsNotNullOrEmpty()) ? URITemplate     : "/",
                                   (HTTPMethod      != null)        ? HTTPMethod      : HTTPMethod.GET,
                                   (HTTPContentType != null)        ? HTTPContentType : HTTPContentType.HTML_UTF8,
                                   null,
                                   null,
                                   null,
                                   null);

        }

        #endregion

        #region AddMethodCallback(HTTPMethod, URITemplate, Hostname = "*", HTTPContentType = null, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPMethod          HTTPMethod,
                                      String              URITemplate,
                                      String              Hostname                    = "*",
                                      HTTPContentType     HTTPContentType             = null,
                                      HTTPAuthentication  HostAuthentication          = null,
                                      HTTPAuthentication  URIAuthentication           = null,
                                      HTTPAuthentication  HTTPMethodAuthentication    = null,
                                      HTTPAuthentication  ContentTypeAuthentication   = null,
                                      HTTPDelegate        HTTPDelegate                = null)

        {

            #region Initial checks

            if (HTTPMethod == null)
                throw new ArgumentNullException("HTTPMethod", "The given parameter must not be null!");

            if (URITemplate.IsNullOrEmpty())
                throw new ArgumentNullException("URITemplate", "The given parameter must not be null or empty!");

            if (HTTPDelegate == null)
                throw new ArgumentNullException("HTTPDelegate", "The given parameter must not be null!");

            #endregion

            _URIMapping.AddHandler(HTTPDelegate,
                                   Hostname,
                                   URITemplate,
                                   (HTTPMethod != null) ? HTTPMethod : HTTPMethod.GET,
                                   HTTPContentType,
                                   HostAuthentication,
                                   URIAuthentication,
                                   HTTPMethodAuthentication,
                                   ContentTypeAuthentication);

        }

        #endregion

        #endregion

        #region Get Method Callbacks

        #region GetHandler(Host, URL, HTTPMethod = null, HTTPContentType = null)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        public HTTPResponse InvokeHandler(HTTPRequest HTTPRequest)
        {

            return _URIMapping.InvokeHandler(HTTPRequest);

        }

        #endregion

        #endregion


        #region Add HTTP Server Sent Events

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
        /// <param name="MethodInfo">The method to call.</param>
        /// <param name="Host">The HTTP host.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="IsSharedEventSource">Whether this event source will be shared.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        public HTTPEventSource AddEventSource(String              EventIdentification,
                                              UInt32              MaxNumberOfCachedEvents     = 500,
                                              TimeSpan?           RetryIntervall              = null,

                                              String              Hostname                    = "*",
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

                                              HostAuthentication,
                                              URIAuthentication,
                                              HTTPMethodAuthentication,

                                              DefaultErrorHandler);

        }

        #endregion

        #endregion

        #region Get HTTP Server Sent Events

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
