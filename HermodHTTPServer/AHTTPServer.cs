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
using System.Reflection;
using System.Collections.Generic;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Hermod.Sockets.TCP;
using eu.Vanaheimr.Styx.Arrows;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An abstract generic HTTP server.
    /// </summary>
    /// <typeparam name="HTTPServiceInterface">An interface inheriting from IHTTPService and defining URLMappings.</typeparam>
    public class HTTPServer<TCPConnectionType> : IArrowSender<TCPConnectionType>,
                                                 IHTTPServer<TCPConnectionType>

        where TCPConnectionType : class, IHTTPConnection, new()

    {

        #region Data

        private readonly TCPServer   _TCPServer;
        private readonly URIMapping  _URIMapping;

        #endregion

        #region Properties

        #region IPAdress

        /// <summary>
        /// Gets the IPAddress on which the HTTPServer listens.
        /// </summary>
        public IIPAddress IPAddress
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.IPAddress;

                return null;

            }
        }

        #endregion

        #region Port

        /// <summary>
        /// Gets the port on which the HTTPServer listens.
        /// </summary>
        public IPPort Port
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.Port;

                return null;

            }
        }

        #endregion

        #region IsRunning

        /// <summary>
        /// True while the HTTPServer is listening for new clients.
        /// </summary>
        public Boolean IsRunning
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.IsRunning;

                return false;

            }
        }

        #endregion

        #region StopRequested

        /// <summary>
        /// The HTTPServer was requested to stop and will no
        /// longer accept new client connections.
        /// </summary>
        public Boolean StopRequested
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.StopRequested;

                return false;

            }
        }

        #endregion

        #region NumberOfClients

        /// <summary>
        /// The current number of connected clients.
        /// </summary>
        public UInt64 NumberOfClients
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.NumberOfClients;

                return 0;

            }
        }

        #endregion

        #region MaxClientConnections

        /// <summary>
        /// The maximum number of pending client connections.
        /// </summary>
        public UInt32 MaxClientConnections
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.MaxClientConnections;

                return 0;

            }
        }

        #endregion

        #region ClientTimeout

        /// <summary>
        /// Will set the ClientTimeout for all incoming client connections
        /// </summary>
        public Int32 ClientTimeout
        {
            get
            {

                if (_TCPServer != null)
                    return _TCPServer.ClientTimeout;

                return 0;

            }
        }

        #endregion

        #region DefaultServerName

        private const String _DefaultServerName = "Hermod HTTP Server v0.1";

        /// <summary>
        /// The default server name.
        /// </summary>
        public virtual String DefaultServerName
        {
            get
            {
                return _DefaultServerName;
            }
        }

        #endregion

        #region ServiceBanner

        public String ServiceBanner
        {

            get
            {
                return ServerName;
            }

            set
            {
                ServerName = value;
            }

        }

        #endregion

        #region ServerName

        /// <summary>
        /// The HTTP server name.
        /// </summary>
        public String ServerName { get; set; }

        #endregion

        #region CallingAssemblies

        private List<Assembly> _CallingAssemblies;

        /// <summary>
        /// The list of calling assemblies.
        /// </summary>
        public List<Assembly> CallingAssemblies
        {

            get
            {
                return _CallingAssemblies;
            }

            set
            {

                if (value == null)
                    return;

                this._CallingAssemblies = value;

                // Add Hermod to the list of assemblies
                this._CallingAssemblies.Add(Assembly.GetExecutingAssembly());

                _CallingAssemblies.
                    SelectMany(_Assembly => _Assembly.GetManifestResourceNames().
                                                      Select(_Resource => new {
                                                          Assembly  = _Assembly,
                                                          Ressource = _Resource
                                                      })
                              ).ForEach(v => this._AllResources.Add(v.Ressource, v.Assembly));

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

        /// <summary>
        /// An event called whenever a request came in.
        /// </summary>
        public event RequestLogDelegate  RequestLog;

        /// <summary>
        /// An event called whenever a request could successfully be processed.
        /// </summary>
        public event AccessLogDelegate   AccessLog;

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        public event ErrorLogDelegate    ErrorLog;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates a new abstract HTTP server.
        /// </summary>
        public HTTPServer(IIPAddress IIPAddress, IPPort Port, Boolean Autostart = true)
        {

            this._URIMapping       = new URIMapping();
       //     this._Implementations  = new Dictionary<HTTPContentType, HTTPServiceInterface>();
            this._AllResources     = new Dictionary<String, Assembly>();

          ServerName = _DefaultServerName;

            //if (NewHTTPServiceHandler != null)
            //    OnNewHTTPService += NewHTTPServiceHandler;

            _TCPServer = new TCPServer(IIPAddress,
                                       Port,
                                       NewHTTPConnection => {

                                           NewHTTPConnection.HTTPServer            = this;
                                           NewHTTPConnection.ServerName            = ServerName;
                                           NewHTTPConnection.HTTPSecurity          = HTTPSecurity;
                                           NewHTTPConnection.NewHTTPServiceHandler = OnNewHTTPService;
                                           //NewHTTPConnection.Implementations       = Implementations;

                                           try
                                           {
                                               NewHTTPConnection.ProcessHTTP();
                                           }
                                           catch (Exception Exception)
                                           {
                                               var OnExceptionOccured_Local = _OnExceptionOccured;
                                               if (OnExceptionOccured_Local != null)
                                                   OnExceptionOccured_Local(this, Exception);
                                           }

                                       },
                                       // Don't do it now, do it a bit later...
                                       Autostart: false);
                                       //ThreadDescription: "HTTPServer");

            _TCPServer.OnStarted += (Sender, Timestamp) => {
                if (OnStarted != null)
                    OnStarted(this, Timestamp);
                };

            if (Autostart)
                _TCPServer.Start();

        }

        #endregion


        #region AddMethodCallback(MethodHandler, Hostname, URITemplate, HTTPMethod, HTTPContentType = null, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="MethodHandler">The method to call.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        public void AddMethodCallback(MethodInfo       MethodHandler,
                                      String           Hostname,
                                      String           URITemplate,
                                      HTTPMethod       HTTPMethod,
                                      HTTPContentType  HTTPContentType             = null,
                                      Boolean          HostAuthentication          = false,
                                      Boolean          URIAuthentication           = false,
                                      Boolean          HTTPMethodAuthentication    = false,
                                      Boolean          ContentTypeAuthentication   = false)

        {

            _URIMapping.AddHandler(MethodHandler,
                                   Hostname,
                                   URITemplate,
                                   HTTPMethod,
                                   HTTPContentType,
                                   HostAuthentication,
                                   URIAuthentication,
                                   HTTPMethodAuthentication,
                                   ContentTypeAuthentication);

        }

        #endregion

        #region AddMethodCallback(MethodHandler, Host, URITemplate, HTTPMethod, HTTPContentType = null, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="MethodHandler">The method to call.</param>
        /// <param name="Host">The HTTP host.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        public void AddMethodCallback(MethodInfo           MethodHandler,
                                      IEnumerable<String>  Hostnames,
                                      String               URITemplate,
                                      HTTPMethod           HTTPMethod,
                                      HTTPContentType      HTTPContentType             = null,
                                      Boolean              HostAuthentication          = false,
                                      Boolean              URIAuthentication           = false,
                                      Boolean              HTTPMethodAuthentication    = false,
                                      Boolean              ContentTypeAuthentication   = false)

        {

            _URIMapping.AddHandler(MethodHandler,
                                   Hostnames,
                                   URITemplate,
                                   HTTPMethod,
                                   HTTPContentType,
                                   HostAuthentication,
                                   URIAuthentication,
                                   HTTPMethodAuthentication,
                                   ContentTypeAuthentication);

        }

        #endregion


        #region AddEventSource(EventIdentification, MaxNumberOfCachedEvents = 100, RetryIntervall = null)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        public HTTPEventSource AddEventSource(String     EventIdentification,
                                              UInt32     MaxNumberOfCachedEvents  = 100,
                                              TimeSpan?  RetryIntervall           = null)
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
        public HTTPEventSource AddEventSource(MethodInfo  MethodInfo,
                                              String      Host,
                                              String      URITemplate,
                                              HTTPMethod  HTTPMethod,
                                              String      EventIdentification,
                                              UInt32      MaxNumberOfCachedEvents  = 100,
                                              TimeSpan?   RetryIntervall           = null,
                                              Boolean     IsSharedEventSource      = false,
                                              Boolean     HostAuthentication       = false,
                                              Boolean     URIAuthentication        = false)

        {

            return _URIMapping.AddEventSource(MethodInfo,
                                              Host,
                                              URITemplate,
                                              HTTPMethod,
                                              EventIdentification,
                                              MaxNumberOfCachedEvents,
                                              RetryIntervall,
                                              IsSharedEventSource,
                                              HostAuthentication,
                                              URIAuthentication);

        }

        #endregion

        #region AddEventSourceHandler(MethodInfo, Host, URITemplate, HostAuthentication = false, URLAuthentication = false)

        /// <summary>
        /// Add an HTTP event source method handler for the given URI template.
        /// </summary>
        /// <param name="MethodInfo">The method to call.</param>
        /// <param name="Host">The HTTP host.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        public void AddEventSourceHandler(MethodInfo  MethodInfo,
                                          String      Host,
                                          String      URITemplate,
                                          HTTPMethod  HTTPMethod,
                                          Boolean     HostAuthentication  = false,
                                          Boolean     URIAuthentication   = false)
        {

            _URIMapping.AddEventSourceHandler(MethodInfo,
                                              Host,
                                              URITemplate,
                                              HTTPMethod,
                                              HostAuthentication,
                                              URIAuthentication);

        }

        #endregion



        #region GetHandler(Host, URL, HTTPMethod = null, HTTPContentType = null)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        public Tuple<MethodInfo, IEnumerable<Object>> GetHandler(String           Host,
                                                                 String           URL               = "/",
                                                                 HTTPMethod       HTTPMethod        = null,
                                                                 HTTPContentType  HTTPContentType   = null)
        {

            return _URIMapping.GetHandler(Host,
                                          URL,
                                          HTTPMethod,
                                          HTTPContentType);

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



        #region LogRequest(RequestTime, Request)

        /// <summary>
        /// Log an incoming request.
        /// </summary>
        /// <param name="RequestTime">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        public void LogRequest(DateTime     RequestTime,
                               HTTPRequest  Request)
        {

            var RequestLogLocal = RequestLog;

            if (RequestLogLocal != null)
                RequestLogLocal(RequestTime, Request);

        }

        #endregion

        #region LogAccess(RequestTime, Request, Response)

        /// <summary>
        /// Log an successful request processing.
        /// </summary>
        /// <param name="RequestTime">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        /// <param name="Response">The outgoing response.</param>
        public void LogAccess(DateTime      RequestTime,
                              HTTPRequest   Request,
                              HTTPResponse  Response)
        {

            var AccessLogLocal = AccessLog;

            if (AccessLogLocal != null)
                AccessLogLocal(RequestTime, Request, Response);

        }

        #endregion

        #region LogError(RequestTime, Request, Response, Error = null, LastException = null)

        /// <summary>
        /// Log an error during request processing.
        /// </summary>
        /// <param name="RequestTime">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        /// <param name="HTTPResponse">The outgoing response.</param>
        /// <param name="Error">The occured error.</param>
        /// <param name="LastException">The last occured exception.</param>
        public void LogError(DateTime      RequestTime,
                             HTTPRequest   Request,
                             HTTPResponse  Response,
                             String        Error          = null,
                             Exception     LastException  = null)
        {

            var ErrorLogLocal = ErrorLog;

            if (ErrorLogLocal != null)
                ErrorLogLocal(RequestTime, Request, Response, Error, LastException);

        }

        #endregion





        #region Start()

        /// <summary>
        /// Start the server.
        /// </summary>
        public void Start()
        {
            _TCPServer.Start();
        }

        #endregion

        #region Start(Delay, InBackground = true)

        /// <summary>
        /// Start the server after a little delay.
        /// </summary>
        /// <param name="Delay">The delay.</param>
        /// <param name="InBackground">Whether to wait on the main thread or in a background thread.</param>
        public void Start(TimeSpan Delay, Boolean InBackground = true)
        {
            _TCPServer.Start(Delay, InBackground);
        }

        #endregion

        #region Shutdown(Wait = true)

        /// <summary>
        /// Shutdown the server.
        /// </summary>
        public void Shutdown(Boolean Wait = true)
        {
            _TCPServer.Shutdown();
        }

        #endregion


        public void Dispose()
        {
            throw new NotImplementedException();
        }

    }

}
