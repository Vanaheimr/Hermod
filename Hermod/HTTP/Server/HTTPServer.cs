/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;
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

    public delegate HTTPResponse HTTPFilter1Delegate (                   HTTPRequest Request);
    public delegate HTTPResponse HTTPFilter2Delegate (HTTPServer Server, HTTPRequest Request);

    public delegate HTTPRequest  HTTPRewrite1Delegate(                   HTTPRequest Request);
    public delegate HTTPRequest  HTTPRewrite2Delegate(HTTPServer Server, HTTPRequest Request);


    /// <summary>
    /// A multitenant HTTP/1.1 server.
    /// </summary>
    /// <typeparam name="T">The type of a collection of tenants.</typeparam>
    /// <typeparam name="U">The type of the tenants.</typeparam>
    public class HTTPServer<T, U> : IHTTPServer
        where T : IEnumerable<U>
    {

        #region Data

        private readonly HTTPServer                             _HTTPServer;
        private readonly ConcurrentDictionary<HTTPHostname, T>  _Multitenancy;

        #endregion

        #region Properties

        /// <summary>
        /// The internal HTTP server.
        /// </summary>
        //internal HTTPServer InternalHTTPServer
        //    => _HTTPServer;

        /// <summary>
        /// The default HTTP servername, used whenever
        /// no HTTP Host-header had been given.
        /// </summary>
        public String DefaultServerName
            => _HTTPServer.DefaultServerName;

        /// <summary>
        /// An associated HTTP security object.
        /// </summary>
        public HTTPSecurity HTTPSecurity
            => _HTTPServer.HTTPSecurity;

        /// <summary>
        /// The DNS defines which DNS servers to use.
        /// </summary>
        public DNSClient DNSClient
            => _HTTPServer.DNSClient;

        /// <summary>
        /// The optional delegate to select a SSL/TLS server certificate.
        /// </summary>
        public ServerCertificateSelectorDelegate ServerCertificateSelector
            => _HTTPServer.ServerCertificateSelector;

        /// <summary>
        /// The optional delegate to verify the SSL/TLS client certificate used for authentication.
        /// </summary>
        public RemoteCertificateValidationCallback ClientCertificateValidator
            => _HTTPServer.ClientCertificateValidator;

        /// <summary>
        /// The optional delegate to select the SSL/TLS client certificate used for authentication.
        /// </summary>
        public LocalCertificateSelectionCallback ClientCertificateSelector
            => _HTTPServer.ClientCertificateSelector;

        /// <summary>
        /// The SSL/TLS protocol(s) allowed for this connection.
        /// </summary>
        public SslProtocols AllowedTLSProtocols
            => _HTTPServer.AllowedTLSProtocols;

        /// <summary>
        /// Is the server already started?
        /// </summary>
        public Boolean IsStarted
            => _HTTPServer.IsStarted;

        /// <summary>
        /// The current number of attached TCP clients.
        /// </summary>
        public UInt64 NumberOfClients
            => _HTTPServer.NumberOfClients;

        #endregion

        #region Events

        public event BoomerangSenderHandler<String, DateTime, HTTPRequest, HTTPResponse> OnNotification
        {

            add
            {
                _HTTPServer.OnNotification += value;
            }

            remove
            {
                _HTTPServer.OnNotification -= value;
            }

        }

        /// <summary>
        /// An event called whenever a request came in.
        /// </summary>
        public event RequestLogHandler RequestLog
        {

            add
            {
                _HTTPServer.RequestLog += value;
            }

            remove
            {
                _HTTPServer.RequestLog -= value;
            }

        }

        /// <summary>
        /// An event called whenever a request could successfully be processed.
        /// </summary>
        public event AccessLogHandler AccessLog
        {

            add
            {
                _HTTPServer.AccessLog += value;
            }

            remove
            {
                _HTTPServer.AccessLog -= value;
            }

        }

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        public event ErrorLogHandler ErrorLog
        {

            add
            {
                _HTTPServer.ErrorLog += value;
            }

            remove
            {
                _HTTPServer.ErrorLog -= value;
            }

        }

        #endregion

        #region Constructor(s)

        #region HTTPServer(TCPPort = null, DefaultServerName = DefaultHTTPServerName, ...)

        /// <summary>
        /// Initialize the multitenant HTTP server using the given parameters.
        /// </summary>
        /// <param name="TCPPort">The TCP port to listen on.</param>
        /// <param name="DefaultServerName">The default HTTP servername, used whenever no HTTP Host-header had been given.</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// 
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="Autostart">Start the HTTP server thread immediately (default: no).</param>
        public HTTPServer(IPPort?                              TCPPort                            = null,
                          String                               DefaultServerName                  = HTTPServer.DefaultHTTPServerName,

                          ServerCertificateSelectorDelegate    ServerCertificateSelector          = null,
                          RemoteCertificateValidationCallback  ClientCertificateValidator         = null,
                          LocalCertificateSelectionCallback    ClientCertificateSelector          = null,
                          SslProtocols                         AllowedTLSProtocols                = SslProtocols.Tls12,

                          String                               ServerThreadName                   = null,
                          ThreadPriority                       ServerThreadPriority               = ThreadPriority.AboveNormal,
                          Boolean                              ServerThreadIsBackground           = true,
                          ConnectionIdBuilder                  ConnectionIdBuilder                = null,
                          ConnectionThreadsNameBuilder         ConnectionThreadsNameBuilder       = null,
                          ConnectionThreadsPriorityBuilder     ConnectionThreadsPriorityBuilder   = null,
                          Boolean                              ConnectionThreadsAreBackground     = true,
                          TimeSpan?                            ConnectionTimeout                  = null,
                          UInt32                               MaxClientConnections               = TCPServer.__DefaultMaxClientConnections,

                          DNSClient                            DNSClient                          = null,
                          Boolean                              Autostart                          = false)

            : this(new HTTPServer(TCPPort,
                                  DefaultServerName,
                                  ServerCertificateSelector,
                                  ClientCertificateValidator,
                                  ClientCertificateSelector,
                                  AllowedTLSProtocols,
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
                                  Autostart))

        {  }

        #endregion

        #region HTTPServer(HTTPServer)

        /// <summary>
        /// Initialize the multitenant HTTP server using the given parameters.
        /// </summary>
        /// <param name="HTTPServer">An existing non-multitenant HTTP server.</param>
        public HTTPServer(HTTPServer HTTPServer)
        {

            this._HTTPServer    = HTTPServer;
            this._Multitenancy  = new ConcurrentDictionary<HTTPHostname, T>();

        }

        #endregion

        #endregion


        #region Multitenancy

        #region GetAllTenants(Hostname)

        /// <summary>
        /// Return all tenants available for the given hostname.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        public IEnumerable<U> GetAllTenants(HTTPHostname  Hostname)
        {

            T Tenants = default(T);

            var Set = new HashSet<U>();

            if (_Multitenancy.TryGetValue(Hostname, out Tenants))
                foreach (var Tenant in Tenants)
                    Set.Add(Tenant);

            if (_Multitenancy.TryGetValue(Hostname.AnyHost, out Tenants))
                foreach (var Tenant in Tenants)
                    Set.Add(Tenant);

            if (_Multitenancy.TryGetValue(Hostname.AnyPort, out Tenants))
                foreach (var Tenant in Tenants)
                    Set.Add(Tenant);

            if (_Multitenancy.TryGetValue(HTTPHostname.Any, out Tenants))
                foreach (var Tenant in Tenants)
                    Set.Add(Tenant);

            return Set;

        }

        #endregion

        #region GetTenant(Hostname, TenantId)

        ///// <summary>
        ///// Return all tenants available for the given hostname.
        ///// </summary>
        ///// <param name="Hostname">The HTTP hostname.</param>
        ///// <param name="TenantId">The unique identification of the new tenant.</param>
        //public U GetTenant(HTTPHostname       Hostname,
        //                                        Tenant_Id  TenantId)
        //{

        //    return GetAllTenants(Hostname).
        //               Where(roamingnetwork => roamingnetwork.Id == TenantId).
        //               FirstOrDefault();

        //}

        #endregion

        #region TryGetTenants(Hostname, out Tenant)

        /// <summary>
        ///Try to return all tenants available for the given hostname.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="Tenants">A tenant.</param>
        public Boolean TryGetTenants(HTTPHostname  Hostname,
                                     out T         Tenants)

            => _Multitenancy.TryGetValue(Hostname, out Tenants);

        #endregion

        #region TryAddTenants(Hostname, Tenants)

        /// <summary>
        ///Try to return all tenants available for the given hostname.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="Tenants">A tenant.</param>
        public Boolean TryAddTenants(HTTPHostname  Hostname,
                                     T             Tenants)

            => _Multitenancy.TryAdd(Hostname, Tenants);

        #endregion

        #region RemoveTenants(Hostname, Tenants)

        /// <summary>
        ///Try to return all tenants available for the given hostname.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="Tenants">A tenant.</param>
        public T RemoveTenants(HTTPHostname  Hostname,
                               T             Tenants)
        {

            T _Tenants;

            if (_Multitenancy.TryRemove(Hostname, out _Tenants))
                return _Tenants;

            return default(T);

        }

        #endregion

        #endregion


        #region Manage the underlying TCP sockets...

        #region AttachTCPPort(Port)

        public IHTTPServer AttachTCPPort(IPPort Port)
        {

            _HTTPServer.AttachTCPPorts(Port);

            return this;

        }

        #endregion

        #region AttachTCPPorts(params Ports)

        public IHTTPServer AttachTCPPorts(params IPPort[] Ports)
        {

            _HTTPServer.AttachTCPPorts(Ports);

            return this;

        }

        #endregion

        #region AttachTCPSocket(Socket)

        public IHTTPServer AttachTCPSocket(IPSocket Socket)
        {

            _HTTPServer.AttachTCPSockets(Socket);

            return this;

        }

        #endregion

        #region AttachTCPSockets(params Sockets)

        public IHTTPServer AttachTCPSockets(params IPSocket[] Sockets)
        {

            _HTTPServer.AttachTCPSockets(Sockets);

            return this;

        }

        #endregion


        #region DetachTCPPort(Port)

        public IHTTPServer DetachTCPPort(IPPort Port)
        {

            _HTTPServer.DetachTCPPorts(Port);

            return this;

        }

        #endregion

        #region DetachTCPPorts(params Sockets)

        public IHTTPServer DetachTCPPorts(params IPPort[] Ports)
        {

            _HTTPServer.DetachTCPPorts(Ports);

            return this;

        }

        #endregion

        #endregion

        #region HTTP Logging

        #region (internal) LogRequest(Timestamp, Request)

        /// <summary>
        /// Log an incoming request.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        internal void LogRequest(DateTime     Timestamp,
                                 HTTPRequest  Request)
        {

            _HTTPServer.LogRequest(Timestamp, Request);

        }

        #endregion

        #region (internal) LogAccess (Timestamp, Request, Response)

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

            _HTTPServer.LogAccess(Timestamp, Request, Response);

        }

        #endregion

        #region (internal) LogError  (Timestamp, Request, Response, Error = null, LastException = null)

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

            _HTTPServer.LogError(Timestamp, Request, Response, Error, LastException);

        }

        #endregion

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
                             HTTPURI          URITemplate,
                             HTTPContentType  HTTPContentType,
                             HTTPURI          URITarget)

        {

            _HTTPServer.Redirect(Hostname,
                                 HTTPMethod,
                                 URITemplate,
                                 HTTPContentType,
                                 URITarget);

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
                             HTTPURI          URITemplate,
                             HTTPContentType  HTTPContentType,
                             HTTPURI          URITarget)

        {

            _HTTPServer.Redirect(HTTPMethod,
                                 URITemplate,
                                 HTTPContentType,
                                 URITarget);

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
                                      HTTPURI             URITemplate,
                                      HTTPContentType     HTTPContentType             = null,
                                      HTTPAuthentication  HostAuthentication          = null,
                                      HTTPAuthentication  URIAuthentication           = null,
                                      HTTPAuthentication  HTTPMethodAuthentication    = null,
                                      HTTPAuthentication  ContentTypeAuthentication   = null,
                                      HTTPDelegate        HTTPDelegate                = null,
                                      URIReplacement      AllowReplacement            = URIReplacement.Fail)

        {

            _HTTPServer.AddMethodCallback(Hostname,
                                          HTTPMethod,
                                          URITemplate,
                                          HTTPContentType,
                                          HostAuthentication,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,
                                          ContentTypeAuthentication,
                                          HTTPDelegate,
                                          AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplates, HTTPContentType = null, ..., HTTPDelegate = null, AllowReplacement = URIReplacement.Fail)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplates">An enumeration of URI templates.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname          Hostname,
                                      HTTPMethod            HTTPMethod,
                                      IEnumerable<HTTPURI>  URITemplates,
                                      HTTPContentType       HTTPContentType             = null,
                                      HTTPAuthentication    HostAuthentication          = null,
                                      HTTPAuthentication    URIAuthentication           = null,
                                      HTTPAuthentication    HTTPMethodAuthentication    = null,
                                      HTTPAuthentication    ContentTypeAuthentication   = null,
                                      HTTPDelegate          HTTPDelegate                = null,
                                      URIReplacement        AllowReplacement            = URIReplacement.Fail)

        {

            _HTTPServer.AddMethodCallback(Hostname,
                                          HTTPMethod,
                                          URITemplates,
                                          HTTPContentType,
                                          HostAuthentication,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,
                                          ContentTypeAuthentication,
                                          HTTPDelegate,
                                          AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate, HTTPContentTypes, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      HTTPURI                       URITemplate,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      HTTPAuthentication            HostAuthentication          = null,
                                      HTTPAuthentication            URIAuthentication           = null,
                                      HTTPAuthentication            HTTPMethodAuthentication    = null,
                                      HTTPAuthentication            ContentTypeAuthentication   = null,
                                      HTTPDelegate                  HTTPDelegate                = null,
                                      URIReplacement                AllowReplacement            = URIReplacement.Fail)

        {

            _HTTPServer.AddMethodCallback(Hostname,
                                          HTTPMethod,
                                          URITemplate,
                                          HTTPContentTypes,
                                          HostAuthentication,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,
                                          ContentTypeAuthentication,
                                          HTTPDelegate,
                                          AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate, HTTPContentTypes, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplates">An enumeration of URI templates.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      IEnumerable<HTTPURI>          URITemplates,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      HTTPAuthentication            HostAuthentication          = null,
                                      HTTPAuthentication            URIAuthentication           = null,
                                      HTTPAuthentication            HTTPMethodAuthentication    = null,
                                      HTTPAuthentication            ContentTypeAuthentication   = null,
                                      HTTPDelegate                  HTTPDelegate                = null,
                                      URIReplacement                AllowReplacement            = URIReplacement.Fail)

        {

            _HTTPServer.AddMethodCallback(Hostname,
                                          HTTPMethod,
                                          URITemplates,
                                          HTTPContentTypes,
                                          HostAuthentication,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,
                                          ContentTypeAuthentication,
                                          HTTPDelegate,
                                          AllowReplacement);

        }

        #endregion


        #region (protected) GetHandler(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        protected HTTPDelegate GetHandler(HTTPHostname                              Host,
                                          HTTPURI                                   URI,
                                          HTTPMethod                                HTTPMethod                   = null,
                                          Func<HTTPContentType[], HTTPContentType>  HTTPContentTypeSelector      = null,
                                          Action<IEnumerable<String>>               ParsedURIParametersDelegate  = null)

            => _HTTPServer.GetHandler(Host,
                                      URI,
                                      HTTPMethod,
                                      HTTPContentTypeSelector,
                                      ParsedURIParametersDelegate);

        #endregion

        public void AddFilter(HTTPFilter1Delegate Filter)
        {
            _HTTPServer.AddFilter(Filter);
        }

        public void AddFilter(HTTPFilter2Delegate Filter)
        {
            _HTTPServer.AddFilter(Filter);
        }

        public void Rewrite(HTTPRewrite1Delegate Rewrite)
        {
            _HTTPServer.Rewrite(Rewrite);
        }

        public void Rewrite(HTTPRewrite2Delegate Rewrite)
        {
            _HTTPServer.Rewrite(Rewrite);
        }


        #region InvokeHandler(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        public Task<HTTPResponse> InvokeHandler(HTTPRequest Request)

            => _HTTPServer.InvokeHandler(Request);

        #endregion

        #endregion

        #region HTTP Server Sent Events

        #region AddEventSource(EventIdentification, MaxNumberOfCachedEvents, RetryIntervall = null, LogfileName = null)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        public HTTPEventSource AddEventSource(String                          EventIdentification,
                                              UInt32                          MaxNumberOfCachedEvents,
                                              TimeSpan?                       RetryIntervall  = null,
                                              Func<String, DateTime, String>  LogfileName     = null)

            => _HTTPServer.AddEventSource(EventIdentification,
                                          MaxNumberOfCachedEvents,
                                          RetryIntervall,
                                          LogfileName);

        #endregion

        #region AddEventSource(EventIdentification, URITemplate, MaxNumberOfCachedEvents = 500, RetryIntervall = null, EnableLogging = false, LogfileName = null, ...)

        /// <summary>
        /// Add a method call back for the given URI template and
        /// add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// 
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public HTTPEventSource AddEventSource(String                          EventIdentification,
                                              HTTPURI                         URITemplate,

                                              UInt32                          MaxNumberOfCachedEvents     = 500,
                                              TimeSpan?                       RetryIntervall              = null,
                                              Boolean                         EnableLogging               = false,
                                              String                          LogfilePrefix               = null,
                                              Func<String, DateTime, String>  LogfileName                 = null,
                                              String                          LogfileReloadSearchPattern  = null,

                                              HTTPHostname?                   Hostname                    = null,
                                              HTTPMethod                      HTTPMethod                  = null,
                                              HTTPContentType                 HTTPContentType             = null,

                                              HTTPAuthentication              HostAuthentication          = null,
                                              HTTPAuthentication              URIAuthentication           = null,
                                              HTTPAuthentication              HTTPMethodAuthentication    = null,

                                              HTTPDelegate                    DefaultErrorHandler         = null)

            => _HTTPServer.AddEventSource(EventIdentification,
                                          URITemplate,

                                          MaxNumberOfCachedEvents,
                                          RetryIntervall,
                                          EnableLogging,
                                          LogfilePrefix,
                                          LogfileName,
                                          LogfileReloadSearchPattern,

                                          Hostname,
                                          HTTPMethod,
                                          HTTPContentType,

                                          HostAuthentication,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,

                                          DefaultErrorHandler);

        #endregion


        #region GetEventSource(EventSourceIdentification)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public HTTPEventSource GetEventSource(String EventSourceIdentification)
            => _HTTPServer.GetEventSource(EventSourceIdentification);

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

            => _HTTPServer.UseEventSource(EventSourceIdentification,
                                          Action);

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

            => _HTTPServer.UseEventSource(EventSourceIdentification,
                                          DataSource,
                                          Action);

        #endregion

        #region TryGetEventSource(EventSourceIdentification, EventSource)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        public Boolean TryGetEventSource(String EventSourceIdentification, out HTTPEventSource EventSource)
            => _HTTPServer.TryGetEventSource(EventSourceIdentification, out EventSource);

        #endregion

        #region GetEventSources(EventSourceSelector = null)

        /// <summary>
        /// An enumeration of all event sources.
        /// </summary>
        public IEnumerable<HTTPEventSource> GetEventSources(Func<HTTPEventSource, Boolean> EventSourceSelector = null)
            => _HTTPServer.GetEventSources(EventSourceSelector);

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

            return _HTTPServer.GetErrorHandler(Host,
                                               URL,
                                               HTTPMethod,
                                               HTTPContentType,
                                               HTTPStatusCode);

        }

        #endregion


        #region Start()

        public void Start()
        {
            _HTTPServer.Start();
        }

        #endregion

        #region Start(Delay, InBackground = true)

        public void Start(TimeSpan Delay, Boolean InBackground = true)
        {

            _HTTPServer.Start(Delay,
                              InBackground);

        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        public void Shutdown(String Message = null, Boolean Wait = true)
        {

            _HTTPServer.Shutdown(Message,
                                 Wait);

        }

        #endregion


        #region Dispose()

        public void Dispose()
        {
            _HTTPServer.Dispose();
        }

        #endregion


    }


    /// <summary>
    /// A HTTP/1.1 server.
    /// </summary>
    public class HTTPServer : ATCPServers,
                              IHTTPServer,
                              IBoomerangSender<String, DateTime, HTTPRequest, HTTPResponse>
    {

        #region Data

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public  const           String           DefaultHTTPServerName  = "GraphDefined Hermod HTTP Server v0.9";

        /// <summary>
        /// The default HTTP server TCP port.
        /// </summary>
        public static readonly  IPPort           DefaultHTTPServerPort  = IPPort.HTTP;

        private readonly        URIMapping       _URIMapping;

        private readonly        HTTPProcessor    _HTTPProcessor;

        #endregion

        #region Properties

        /// <summary>
        /// The default HTTP servername, used whenever
        /// no HTTP Host-header had been given.
        /// </summary>
        public String        DefaultServerName   { get; }

        /// <summary>
        /// An associated HTTP security object.
        /// </summary>
        public HTTPSecurity  HTTPSecurity        { get; }

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
        /// <param name="TCPPort">The TCP port to listen on.</param>
        /// <param name="DefaultServerName">The default HTTP servername, used whenever no HTTP Host-header had been given.</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionThreadsNameBuilder">An optional delegate to set the name of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsPriorityBuilder">An optional delegate to set the priority of the TCP connection threads.</param>
        /// <param name="ConnectionThreadsAreBackground">Whether the TCP connection threads are background threads or not (default: yes).</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// 
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="Autostart">Start the HTTP server thread immediately (default: no).</param>
        public HTTPServer(IPPort?                              TCPPort                            = null,
                          String                               DefaultServerName                  = DefaultHTTPServerName,

                          ServerCertificateSelectorDelegate    ServerCertificateSelector          = null,
                          RemoteCertificateValidationCallback  ClientCertificateValidator         = null,
                          LocalCertificateSelectionCallback    ClientCertificateSelector          = null,
                          SslProtocols                         AllowedTLSProtocols                = SslProtocols.Tls12,

                          String                               ServerThreadName                   = null,
                          ThreadPriority                       ServerThreadPriority               = ThreadPriority.AboveNormal,
                          Boolean                              ServerThreadIsBackground           = true,
                          ConnectionIdBuilder                  ConnectionIdBuilder                = null,
                          ConnectionThreadsNameBuilder         ConnectionThreadsNameBuilder       = null,
                          ConnectionThreadsPriorityBuilder     ConnectionThreadsPriorityBuilder   = null,
                          Boolean                              ConnectionThreadsAreBackground     = true,
                          TimeSpan?                            ConnectionTimeout                  = null,
                          UInt32                               MaxClientConnections               = TCPServer.__DefaultMaxClientConnections,

                          DNSClient                            DNSClient                          = null,
                          Boolean                              Autostart                          = false)

            : base(DefaultServerName,
                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   ClientCertificateSelector,
                   AllowedTLSProtocols,
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

            this.DefaultServerName         = DefaultServerName;
            this._URIMapping                = new URIMapping();

            _HTTPProcessor                  = new HTTPProcessor(this);
            _HTTPProcessor.OnNotification  += ProcessBoomerang;
            _HTTPProcessor.RequestLog      += (HTTPProcessor, ServerTimestamp, Request)                                 => LogRequest(ServerTimestamp, Request);
            _HTTPProcessor.AccessLog       += (HTTPProcessor, ServerTimestamp, Request, Response)                       => LogAccess (ServerTimestamp, Request, Response);
            _HTTPProcessor.ErrorLog        += (HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException) => LogError  (ServerTimestamp, Request, Response, Error, LastException);

            if (TCPPort != null)
                this.AttachTCPPort(TCPPort ?? IPPort.HTTP);
            else
                this.AttachTCPPort(DefaultHTTPServerPort);

            if (Autostart)
                Start();

        }

        #endregion


        #region Manage the underlying TCP sockets...

        #region AttachTCPPort(Port)

        public IHTTPServer AttachTCPPort(IPPort Port)
        {

            this.AttachTCPPorts(Port);

            return this;

        }

        #endregion

        #region AttachTCPPorts(params Ports)

        public IHTTPServer AttachTCPPorts(params IPPort[] Ports)
        {

            AttachTCPPorts(_TCPServer => _TCPServer.SendTo(_HTTPProcessor), Ports);

            return this;

        }

        #endregion

        #region AttachTCPSocket(Socket)

        public IHTTPServer AttachTCPSocket(IPSocket Socket)
        {

            this.AttachTCPSockets(Socket);

            return this;

        }

        #endregion

        #region AttachTCPSockets(params Sockets)

        public IHTTPServer AttachTCPSockets(params IPSocket[] Sockets)
        {

            AttachTCPSockets(_TCPServer => _TCPServer.SendTo(_HTTPProcessor), Sockets);

            return this;

        }

        #endregion


        #region DetachTCPPort(Port)

        public IHTTPServer DetachTCPPort(IPPort Port)
        {

            DetachTCPPorts(Port);

            return this;

        }

        #endregion

        #region DetachTCPPorts(params Sockets)

        public IHTTPServer DetachTCPPorts(params IPPort[] Ports)
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

        #endregion

        #region HTTP Logging

        #region (internal) LogRequest(Timestamp, Request)

        /// <summary>
        /// Log an incoming request.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the incoming request.</param>
        /// <param name="Request">The incoming request.</param>
        internal void LogRequest(DateTime     Timestamp,
                                 HTTPRequest  Request)
        {

            RequestLog?.Invoke(Timestamp, this, Request);

        }

        #endregion

        #region (internal) LogAccess (Timestamp, Request, Response)

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

            AccessLog?.Invoke(Timestamp, this, Request, Response);

        }

        #endregion

        #region (internal) LogError  (Timestamp, Request, Response, Error = null, LastException = null)

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

            ErrorLog?.Invoke(Timestamp, this, Request, Response, Error, LastException);

        }

        #endregion

        #endregion


        #region ProcessBoomerang(ConnectionId, Timestamp, HTTPRequest)

        private async Task<HTTPResponse> ProcessBoomerang(String       ConnectionId,
                                                          DateTime     Timestamp,
                                                          HTTPRequest  HTTPRequest)
        {

            #region 1) Invoke delegate based on URIMapping

            HTTPResponse URIMappingResponse = null;

            try
            {
                URIMappingResponse = await InvokeHandler(HTTPRequest);
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
                    Server          = DefaultServerName,
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
                    Server          = DefaultServerName,
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
                Server          = DefaultServerName,
                Connection      = "close"
            };

            #endregion

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
                             HTTPURI          URITemplate,
                             HTTPContentType  HTTPContentType,
                             HTTPURI          URITarget)

        {

            _URIMapping.AddHandler(req => InvokeHandler(new HTTPRequest.Builder(req).SetURI(URITarget)),
                                   Hostname,
                                   (URITemplate.IsNotNullOrEmpty()) ? URITemplate     : HTTPURI.Parse("/"),
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
                             HTTPURI          URITemplate,
                             HTTPContentType  HTTPContentType,
                             HTTPURI          URITarget)

        {

            _URIMapping.AddHandler(req => InvokeHandler(new HTTPRequest.Builder(req).SetURI(URITarget)),
                                   HTTPHostname.Any,
                                   (URITemplate.IsNotNullOrEmpty()) ? URITemplate     : HTTPURI.Parse("/"),
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
                                      HTTPURI             URITemplate,
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

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplates, HTTPContentType = null, ..., HTTPDelegate = null, AllowReplacement = URIReplacement.Fail)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplates">An enumeration of URI templates.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname          Hostname,
                                      HTTPMethod            HTTPMethod,
                                      IEnumerable<HTTPURI>  URITemplates,
                                      HTTPContentType       HTTPContentType             = null,
                                      HTTPAuthentication    HostAuthentication          = null,
                                      HTTPAuthentication    URIAuthentication           = null,
                                      HTTPAuthentication    HTTPMethodAuthentication    = null,
                                      HTTPAuthentication    ContentTypeAuthentication   = null,
                                      HTTPDelegate          HTTPDelegate                = null,
                                      URIReplacement        AllowReplacement            = URIReplacement.Fail)

        {

            #region Initial checks

            if (HTTPMethod == null)
                throw new ArgumentNullException(nameof(HTTPMethod),       "The given HTTP method must not be null!");

            if (URITemplates == null || !URITemplates.Any())
                throw new ArgumentNullException(nameof(URITemplates),     "The given URI template must not be null or empty!");

            //if (HTTPContentType == null)
            //    throw new ArgumentNullException(nameof(HTTPContentType),  "The given HTTP content type must not be null!");

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

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate, HTTPContentTypes, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      HTTPURI                       URITemplate,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      HTTPAuthentication            HostAuthentication          = null,
                                      HTTPAuthentication            URIAuthentication           = null,
                                      HTTPAuthentication            HTTPMethodAuthentication    = null,
                                      HTTPAuthentication            ContentTypeAuthentication   = null,
                                      HTTPDelegate                  HTTPDelegate                = null,
                                      URIReplacement                AllowReplacement            = URIReplacement.Fail)

        {

            #region Initial checks

            if (Hostname == null)
                throw new ArgumentNullException(nameof(Hostname),          "The given HTTP hostname must not be null!");

            if (HTTPMethod == null)
                throw new ArgumentNullException(nameof(HTTPMethod),        "The given HTTP method must not be null!");

            if (URITemplate.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(URITemplate),       "The given URI template must not be null or empty!");

            if (HTTPContentTypes == null || !HTTPContentTypes.Any())
                throw new ArgumentNullException(nameof(HTTPContentTypes),  "The given content types must not be null or empty!");

            if (HTTPDelegate == null)
                throw new ArgumentNullException(nameof(HTTPDelegate),      "The given HTTP delegate must not be null!");

            #endregion

            foreach (var contenttype in HTTPContentTypes)
                _URIMapping.AddHandler(HTTPDelegate,
                                       Hostname,
                                       URITemplate,
                                       HTTPMethod ?? HTTPMethod.GET,
                                       contenttype,
                                       HostAuthentication,
                                       URIAuthentication,
                                       HTTPMethodAuthentication,
                                       ContentTypeAuthentication,
                                       null,
                                       AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate, HTTPContentTypes, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplates">An enumeration of URI templates.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      IEnumerable<HTTPURI>          URITemplates,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      HTTPAuthentication            HostAuthentication          = null,
                                      HTTPAuthentication            URIAuthentication           = null,
                                      HTTPAuthentication            HTTPMethodAuthentication    = null,
                                      HTTPAuthentication            ContentTypeAuthentication   = null,
                                      HTTPDelegate                  HTTPDelegate                = null,
                                      URIReplacement                AllowReplacement            = URIReplacement.Fail)

        {

            #region Initial checks

            if (Hostname == null)
                throw new ArgumentNullException(nameof(Hostname),          "The given HTTP hostname must not be null!");

            if (HTTPMethod == null)
                throw new ArgumentNullException(nameof(HTTPMethod),        "The given HTTP method must not be null!");

            if (URITemplates     == null || !URITemplates.Any())
                throw new ArgumentNullException(nameof(URITemplates),      "The given URI template must not be null or empty!");

            if (HTTPContentTypes == null || !HTTPContentTypes.Any())
                throw new ArgumentNullException(nameof(HTTPContentTypes),  "The given content types must not be null or empty!");

            if (HTTPDelegate == null)
                throw new ArgumentNullException(nameof(HTTPDelegate),      "The given HTTP delegate must not be null!");

            #endregion

            foreach (var uritemplate in URITemplates)
                foreach (var contenttype in HTTPContentTypes)
                    _URIMapping.AddHandler(HTTPDelegate,
                                           Hostname,
                                           uritemplate,
                                           HTTPMethod ?? HTTPMethod.GET,
                                           contenttype,
                                           HostAuthentication,
                                           URIAuthentication,
                                           HTTPMethodAuthentication,
                                           ContentTypeAuthentication,
                                           null,
                                           AllowReplacement);

        }

        #endregion


        #region (protected internal) GetHandler(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        protected internal HTTPDelegate GetHandler(HTTPHostname                              Host,
                                                   HTTPURI                                   URI,
                                                   HTTPMethod                                HTTPMethod                   = null,
                                                   Func<HTTPContentType[], HTTPContentType>  HTTPContentTypeSelector      = null,
                                                   Action<IEnumerable<String>>               ParsedURIParametersDelegate  = null)

            => _URIMapping.GetHandler(Host,
                                      URI,
                                      HTTPMethod,
                                      HTTPContentTypeSelector,
                                      ParsedURIParametersDelegate);

        #endregion



        private readonly List<HTTPFilter2Delegate> _HTTPFilters = new List<HTTPFilter2Delegate>();

        public void AddFilter(HTTPFilter1Delegate Filter)
        {
            _HTTPFilters.Add((server, request) => Filter(request));
        }

        public void AddFilter(HTTPFilter2Delegate Filter)
        {
            _HTTPFilters.Add(Filter);
        }


        private readonly List<HTTPRewrite2Delegate> _HTTPRewrites = new List<HTTPRewrite2Delegate>();

        public void Rewrite(HTTPRewrite1Delegate Rewrite)
        {
            _HTTPRewrites.Add((server, request) => Rewrite(request));
        }

        public void Rewrite(HTTPRewrite2Delegate Rewrite)
        {
            _HTTPRewrites.Add(Rewrite);
        }


        #region InvokeHandler(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        public async Task<HTTPResponse> InvokeHandler(HTTPRequest Request)
        {

            HTTPResponse _HTTPResponse;

            foreach (var _HTTPFilter in _HTTPFilters)
            {

                _HTTPResponse = _HTTPFilter(this, Request);

                if (_HTTPResponse != null)
                    return _HTTPResponse;

            }

            foreach (var _HTTPRewrite in _HTTPRewrites)
            {

                var NewRequest = _HTTPRewrite(this, Request);

                if (NewRequest != null)
                {
                    Request = NewRequest;
                    break;
                }

            }

            var Handler = _URIMapping.GetHandler(Request);

            if (Handler != null)
                return await Handler(Request);

            return new HTTPResponseBuilder(Request) {
                HTTPStatusCode  = HTTPStatusCode.NotFound,
                Server          = Request.Host.ToString(),
                Date            = DateTime.UtcNow,
                ContentType     = HTTPContentType.TEXT_UTF8,
                Content         = "Error 404 - Not Found!".ToUTF8Bytes(),
                Connection      = "close"
            };

        }

        #endregion

        #endregion

        #region HTTP Server Sent Events

        #region AddEventSource(EventIdentification, MaxNumberOfCachedEvents = 500, RetryIntervall = null, LogfileName = null)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        public HTTPEventSource AddEventSource(String                          EventIdentification,
                                              UInt32                          MaxNumberOfCachedEvents   = 500,
                                              TimeSpan?                       RetryIntervall            = null,
                                              Func<String, DateTime, String>  LogfileName               = null)

            => _URIMapping.AddEventSource(EventIdentification,
                                          MaxNumberOfCachedEvents,
                                          RetryIntervall,
                                          LogfileName);

        #endregion

        #region AddEventSource(EventIdentification, URITemplate, MaxNumberOfCachedEvents = 500, RetryIntervall = null, EnableLogging = false, LogfileName = null, ...)

        /// <summary>
        /// Add a method call back for the given URI template and
        /// add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// 
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public HTTPEventSource AddEventSource(String                          EventIdentification,
                                              HTTPURI                         URITemplate,

                                              UInt32                          MaxNumberOfCachedEvents     = 500,
                                              TimeSpan?                       RetryIntervall              = null,
                                              Boolean                         EnableLogging               = false,
                                              String                          LogfilePrefix               = null,
                                              Func<String, DateTime, String>  LogfileName                 = null,
                                              String                          LogfileReloadSearchPattern  = null,

                                              HTTPHostname?                   Hostname                    = null,
                                              HTTPMethod                      HTTPMethod                  = null,
                                              HTTPContentType                 HTTPContentType             = null,

                                              HTTPAuthentication              HostAuthentication          = null,
                                              HTTPAuthentication              URIAuthentication           = null,
                                              HTTPAuthentication              HTTPMethodAuthentication    = null,

                                              HTTPDelegate                    DefaultErrorHandler         = null)


            => _URIMapping.AddEventSource(EventIdentification,
                                          URITemplate,

                                          MaxNumberOfCachedEvents,
                                          RetryIntervall,
                                          EnableLogging || LogfileName != null
                                              ? LogfileName ?? ((eventid, time) => String.Concat(LogfilePrefix ?? "",
                                                                                                 eventid, "_",
                                                                                                 time.Year, "-", time.Month.ToString("D2"),
                                                                                                 ".log"))
                                              : null,
                                          LogfileReloadSearchPattern ?? String.Concat(LogfilePrefix ?? "", EventIdentification, "_*.log"),

                                          Hostname,
                                          HTTPMethod,
                                          HTTPContentType,

                                          HostAuthentication,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,

                                          DefaultErrorHandler);

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

        //public void AddFilter(Func<HTTPRequest, HTTPResponse> p)
        //{
        //    throw new NotImplementedException();
        //}

    }

}
