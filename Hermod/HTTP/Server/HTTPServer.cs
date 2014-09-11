/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Xml.Linq;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Illias.Commons.ConsoleLog;
using eu.Vanaheimr.Hermod.Sockets.TCP;
using eu.Vanaheimr.Hermod.Services.TCP;
using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    public static class HTTPExtentions
    {

        #region (protected) GetRequestBodyAsUTF8String(this Request, HTTPContentType)

        public static HTTPResult<String> GetRequestBodyAsUTF8String(this HTTPRequest  Request,
                                                                    HTTPContentType   HTTPContentType)
        {

            if (Request.ContentType != HTTPContentType)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            if (Request.ContentLength == 0)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            if (Request.TryReadHTTPBody() == false)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            if (Request.Content == null || Request.Content.Length == 0)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            var RequestBodyString = Request.Content.ToUTF8String();

            if (RequestBodyString.IsNullOrEmpty())
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            return new HTTPResult<String>(Result: RequestBodyString);

        }

        #endregion

        #region ParseJSONRequestBody()

        public static HTTPResult<JObject> ParseJSONRequestBody(this HTTPRequest Request)
        {

            var RequestBodyString = Request.GetRequestBodyAsUTF8String(HTTPContentType.JSON_UTF8);
            if (RequestBodyString.HasErrors)
                return new HTTPResult<JObject>(RequestBodyString.Error);

            JObject RequestBodyJSON;

            try
            {
                RequestBodyJSON = JObject.Parse(RequestBodyString.Data);
            }
            catch (Exception)
            {
                return new HTTPResult<JObject>(Request, HTTPStatusCode.BadRequest);
            }

            return new HTTPResult<JObject>(RequestBodyJSON);

        }

        #endregion

        #region ParseXMLRequestBody()

        public static HTTPResult<XDocument> ParseXMLRequestBody(this HTTPRequest Request)
        {

            var RequestBodyString = Request.GetRequestBodyAsUTF8String(HTTPContentType.XMLTEXT_UTF8);
            if (RequestBodyString.HasErrors)
                return new HTTPResult<XDocument>(RequestBodyString.Error);

            XDocument RequestBodyXML;

            try
            {
                RequestBodyXML = XDocument.Parse(RequestBodyString.Data);
            }
            catch (Exception e)
            {
                Log.WriteLine(e.Message);
                return new HTTPResult<XDocument>(Request, HTTPStatusCode.BadRequest);
            }

            return new HTTPResult<XDocument>(RequestBodyXML);

        }

        #endregion

    }


    /// <summary>
    /// A HTTP/1.1 server.
    /// </summary>
    public class HTTPServer : ACustomTCPServers,
                              IBoomerangSender<String, DateTime, HTTPRequest, HTTPResponse>
    {

        #region Data

        internal const    String             __DefaultServerName  = "Vanaheimr Hermod HTTP Service v0.9";
        private readonly  URIMapping         _URIMapping;

        private readonly  HTTPProcessor      _HTTPProcessor;

        #endregion

        #region Properties

        #region DefaultServerName

        private String _DefaultServerName;

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

            set
            {
                if (value.IsNotNullOrEmpty())
                    _DefaultServerName = value;
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
        /// <param name="DefaultServerName">The default HTTP servername, used whenever no HTTP Host-header had been given.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder"></param>
        /// <param name="ConnectionThreadsNameCreator">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriority">The optional priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP conncection threads are background threads or not.</param>
        /// <param name="ConnectionTimeoutSeconds">The TCP client timeout for all incoming client connections in seconds.</param>
        /// <param name="Autostart">Start the HTTP server thread immediately.</param>
        public HTTPServer(IPPort                       IPPort                          = null,
                          String                       DefaultServerName               = __DefaultServerName,
                          String                       ServerThreadName                = null,
                          ThreadPriority               ServerThreadPriority            = ThreadPriority.AboveNormal,
                          Boolean                      ServerThreadIsBackground        = true,
                          Func<IPSocket, String>       ConnectionIdBuilder             = null,
                          Func<TCPConnection, String>  ConnectionThreadsNameCreator    = null,
                          ThreadPriority               ConnectionThreadsPriority       = ThreadPriority.AboveNormal,
                          Boolean                      ConnectionThreadsAreBackground  = true,
                          TimeSpan?                    ConnectionTimeout               = null,
                          IEnumerable<Assembly>        CallingAssemblies               = null,
                          Boolean                      Autostart                       = false)

            : base(DefaultServerName,
                   ServerThreadName,
                   ServerThreadPriority,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionThreadsNameCreator,
                   ConnectionThreadsPriority,
                   ConnectionThreadsAreBackground,
                   ConnectionTimeout)

        {

            this._DefaultServerName                = DefaultServerName;
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

            _HTTPProcessor                         = new HTTPProcessor();
            _HTTPProcessor.OnNotification         += ProcessBoomerang;
            _HTTPProcessor.RequestLog             += (HTTPProcessor, ServerTimestamp, Request)                                 => LogRequest(ServerTimestamp, Request);
            _HTTPProcessor.AccessLog              += (HTTPProcessor, ServerTimestamp, Request, Response)                       => LogAccess (ServerTimestamp, Request, Response);
            _HTTPProcessor.ErrorLog               += (HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException) => LogError  (ServerTimestamp, Request, Response, Error, LastException);

            if (IPPort != null)
                this.AttachTCPPort(IPPort);

            if (Autostart)
                Start();

        }

        #endregion


        // Underlying TCP sockets...

        #region AttachTCPPort(Port)

        public HTTPServer AttachTCPPort(IPPort Port)
        {

            AttachTCPPorts(Port);

            return this;

        }

        #endregion

        #region AttachTCPPorts(params Ports)

        public HTTPServer AttachTCPPorts(params IPPort[] Ports)
        {

            base._AttachTCPPorts(_TCPServer => _TCPServer.SendTo(_HTTPProcessor), Ports);

            return this;

        }

        #endregion

        #region AttachTCPSocket(Socket)

        public HTTPServer AttachTCPSocket(IPSocket Socket)
        {

            AttachTCPSockets(Socket);

            return this;

        }

        #endregion

        #region AttachTCPSockets(params Sockets)

        public HTTPServer AttachTCPSockets(params IPSocket[] Sockets)
        {

            base._AttachTCPSockets(_TCPServer => _TCPServer.SendTo(_HTTPProcessor), Sockets);

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

            base._DetachTCPPorts(_TCPServer => {
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

            #region Check if any HTTP delegate matches...

            var Handler = GetHandler(HTTPRequest);

            if (Handler != null)
                return Handler(HTTPRequest);

            #endregion

            #region ...or call default delegate!

            var OnNotificationLocal = OnNotification;
            if (OnNotificationLocal != null)
                return OnNotificationLocal(ConnectionId,
                                           Timestamp,
                                           HTTPRequest);

            #endregion

            #region ...or fail!

            return new HTTPResponseBuilder() {
                HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                ContentType     = HTTPContentType.TEXT_UTF8,
                Content         = "Error 500 - Internal Server Error!".ToUTF8Bytes(),
                Server          = "Hermod",
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

        #region AddMethodCallback(HTTPMethod, URITemplate, Hostname = "*", HTTPContentType = null, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPMethod          HTTPMethod,
                                      String              URITemplate,
                                      HTTPContentType     HTTPContentType,
                                      HTTPDelegate        HTTPDelegate)

        {

            _URIMapping.AddHandler(HTTPDelegate,
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
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPMethod          HTTPMethod                  = null,
                                      String              URITemplate                 = "/",
                                      String              Hostname                    = "*",
                                      HTTPContentType     HTTPContentType             = null,
                                      HTTPAuthentication  HostAuthentication          = null,
                                      HTTPAuthentication  URIAuthentication           = null,
                                      HTTPAuthentication  HTTPMethodAuthentication    = null,
                                      HTTPAuthentication  ContentTypeAuthentication   = null,
                                      HTTPDelegate        HTTPDelegate                = null)

        {

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
        public HTTPDelegate GetHandler(HTTPRequest HTTPRequest)
        {

            return _URIMapping.GetHandler(HTTPRequest);

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

        #region AddEventSource(MethodInfo, Host, URITemplate, HTTPMethod, EventIdentification, MaxNumberOfCachedEvents = 100, RetryIntervall = null, IsSharedEventSource = false, HostAuthentication = false, URIAuthentication = false)

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
                                              UInt32              MaxNumberOfCachedEvents     = 100,
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
