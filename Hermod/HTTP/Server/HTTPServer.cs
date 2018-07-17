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
using System.IO;
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
using org.GraphDefined.Vanaheimr.Hermod.Services;
using System.Text;

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

        /// <summary>
        /// An event called whenever a HTTP request came in.
        /// </summary>
        public HTTPRequestLogEvent   RequestLog    = new HTTPRequestLogEvent();

        /// <summary>
        /// An event called whenever a HTTP request could successfully be processed.
        /// </summary>
        public HTTPResponseLogEvent  ResponseLog   = new HTTPResponseLogEvent();

        /// <summary>
        /// An event called whenever a HTTP request resulted in an error.
        /// </summary>
        public HTTPErrorLogEvent     ErrorLog      = new HTTPErrorLogEvent();

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

            // Link HTTP events...
            HTTPServer.RequestLog   += (HTTPProcessor, ServerTimestamp, Request)                                 => RequestLog. WhenAll(HTTPProcessor, ServerTimestamp, Request);
            HTTPServer.ResponseLog  += (HTTPProcessor, ServerTimestamp, Request, Response)                       => ResponseLog.WhenAll(HTTPProcessor, ServerTimestamp, Request, Response);
            HTTPServer.ErrorLog     += (HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException) => ErrorLog.   WhenAll(HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException);

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


        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate, HTTPContentType = null, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname            Hostname,
                                      HTTPMethod              HTTPMethod,
                                      HTTPURI                 URITemplate,
                                      HTTPContentType         HTTPContentType             = null,
                                      HTTPAuthentication      URIAuthentication           = null,
                                      HTTPAuthentication      HTTPMethodAuthentication    = null,
                                      HTTPAuthentication      ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler   HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler  HTTPResponseLogger          = null,
                                      HTTPDelegate            DefaultErrorHandler         = null,
                                      HTTPDelegate            HTTPDelegate                = null,
                                      URIReplacement          AllowReplacement            = URIReplacement.Fail)

        {

            _HTTPServer.AddMethodCallback(Hostname,
                                          HTTPMethod,
                                          URITemplate,
                                          HTTPContentType,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,
                                          ContentTypeAuthentication,
                                          HTTPRequestLogger,
                                          HTTPResponseLogger,
                                          DefaultErrorHandler,
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
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname            Hostname,
                                      HTTPMethod              HTTPMethod,
                                      IEnumerable<HTTPURI>    URITemplates,
                                      HTTPContentType         HTTPContentType             = null,
                                      HTTPAuthentication      URIAuthentication           = null,
                                      HTTPAuthentication      HTTPMethodAuthentication    = null,
                                      HTTPAuthentication      ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler   HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler  HTTPResponseLogger          = null,
                                      HTTPDelegate            DefaultErrorHandler         = null,
                                      HTTPDelegate            HTTPDelegate                = null,
                                      URIReplacement          AllowReplacement            = URIReplacement.Fail)

        {

            _HTTPServer.AddMethodCallback(Hostname,
                                          HTTPMethod,
                                          URITemplates,
                                          HTTPContentType,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,
                                          ContentTypeAuthentication,
                                          HTTPRequestLogger,
                                          HTTPResponseLogger,
                                          DefaultErrorHandler,
                                          HTTPDelegate,
                                          AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate, HTTPContentTypes, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      HTTPURI                       URITemplate,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      HTTPAuthentication            URIAuthentication           = null,
                                      HTTPAuthentication            HTTPMethodAuthentication    = null,
                                      HTTPAuthentication            ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler         HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler        HTTPResponseLogger          = null,
                                      HTTPDelegate                  DefaultErrorHandler         = null,
                                      HTTPDelegate                  HTTPDelegate                = null,
                                      URIReplacement                AllowReplacement            = URIReplacement.Fail)

        {

            _HTTPServer.AddMethodCallback(Hostname,
                                          HTTPMethod,
                                          URITemplate,
                                          HTTPContentTypes,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,
                                          ContentTypeAuthentication,
                                          HTTPRequestLogger,
                                          HTTPResponseLogger,
                                          DefaultErrorHandler,
                                          HTTPDelegate,
                                          AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate, HTTPContentTypes, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplates">An enumeration of URI templates.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      IEnumerable<HTTPURI>          URITemplates,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      HTTPAuthentication            URIAuthentication           = null,
                                      HTTPAuthentication            HTTPMethodAuthentication    = null,
                                      HTTPAuthentication            ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler         HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler        HTTPResponseLogger          = null,
                                      HTTPDelegate                  DefaultErrorHandler         = null,
                                      HTTPDelegate                  HTTPDelegate                = null,
                                      URIReplacement                AllowReplacement            = URIReplacement.Fail)

        {

            _HTTPServer.AddMethodCallback(Hostname,
                                          HTTPMethod,
                                          URITemplates,
                                          HTTPContentTypes,
                                          URIAuthentication,
                                          HTTPMethodAuthentication,
                                          ContentTypeAuthentication,
                                          HTTPRequestLogger,
                                          HTTPResponseLogger,
                                          DefaultErrorHandler,
                                          HTTPDelegate,
                                          AllowReplacement);

        }

        #endregion


        #region (protected) GetHandlers(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        protected HTTPServer.Handlers GetHandlers(HTTPHostname                              Host,
                                                  HTTPURI                                   URI,
                                                  HTTPMethod?                               HTTPMethod                   = null,
                                                  Func<HTTPContentType[], HTTPContentType>  HTTPContentTypeSelector      = null,
                                                  Action<IEnumerable<String>>               ParsedURIParametersDelegate  = null)

            => _HTTPServer.GetHandlers(Host,
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
        public HTTPEventSource AddEventSource(HTTPEventSource_Id              EventIdentification,
                                              UInt32                          MaxNumberOfCachedEvents,
                                              TimeSpan?                       RetryIntervall  = null,
                                              Boolean                         EnableLogging   = true,
                                              Func<String, DateTime, String>  LogfileName     = null)

            => _HTTPServer.AddEventSource(EventIdentification,
                                          MaxNumberOfCachedEvents,
                                          RetryIntervall,
                                          EnableLogging,
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
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public HTTPEventSource AddEventSource(HTTPEventSource_Id              EventIdentification,
                                              HTTPURI                         URITemplate,

                                              UInt32                          MaxNumberOfCachedEvents     = 500,
                                              TimeSpan?                       RetryIntervall              = null,
                                              Boolean                         EnableLogging               = true,
                                              String                          LogfilePrefix               = null,
                                              Func<String, DateTime, String>  LogfileName                 = null,
                                              String                          LogfileReloadSearchPattern  = null,

                                              HTTPHostname?                   Hostname                    = null,
                                              HTTPMethod?                     HTTPMethod                  = null,
                                              HTTPContentType                 HTTPContentType             = null,

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

                                          URIAuthentication,
                                          HTTPMethodAuthentication,

                                          DefaultErrorHandler);

        #endregion


        #region GetEventSource(EventSourceIdentification)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public HTTPEventSource GetEventSource(HTTPEventSource_Id EventSourceIdentification)

            => _HTTPServer.GetEventSource(EventSourceIdentification);

        #endregion

        #region UseEventSource(EventSourceIdentification, Action)

        /// <summary>
        /// Call the given delegate for the event source identified
        /// by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="Action">A delegate.</param>
        public void UseEventSource(HTTPEventSource_Id       EventSourceIdentification,
                                   Action<HTTPEventSource>  Action)

            => _HTTPServer.UseEventSource(EventSourceIdentification,
                                          Action);

        #endregion

        #region UseEventSource(EventSourceIdentification, DataSource, Action)

        /// <summary>
        /// Call the given delegate for the event source identified
        /// by the given event source identification.
        /// </summary>
        /// <typeparam name="T">The type of the return values.</typeparam>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="DataSource">A enumeration of data.</param>
        /// <param name="Action">A delegate.</param>
        public void UseEventSource<T>(HTTPEventSource_Id          EventSourceIdentification,
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
        public Boolean TryGetEventSource(HTTPEventSource_Id EventSourceIdentification, out HTTPEventSource EventSource)

            => _HTTPServer.TryGetEventSource(EventSourceIdentification, out EventSource);

        #endregion

        #region GetEventSources(IncludeEventSource = null)

        /// <summary>
        /// An enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        public IEnumerable<HTTPEventSource> GetEventSources(Func<HTTPEventSource, Boolean> IncludeEventSource = null)

            => _HTTPServer.GetEventSources(IncludeEventSource);

        #endregion

        #endregion

        #region GetErrorHandler(Host, URL, HTTPMethod = null, HTTPContentType = null, HTTPStatusCode = null)

        /// <summary>
        /// Return the best matching error handler for the given parameters.
        /// </summary>
        public Tuple<MethodInfo, IEnumerable<Object>> GetErrorHandler(String           Host,
                                                                      String           URL, 
                                                                      HTTPMethod?      HTTPMethod       = null,
                                                                      HTTPContentType  HTTPContentType  = null,
                                                                      HTTPStatusCode   HTTPStatusCode   = null)

            => _HTTPServer.GetErrorHandler(Host,
                                           URL,
                                           HTTPMethod,
                                           HTTPContentType,
                                           HTTPStatusCode);

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
                              IArrowReceiver<TCPConnection>
    {

        public class Handlers
        {

            #region Properties

            public HTTPDelegate                              RequestHandler         { get; }
            public HTTPRequestLogHandler                     HTTPRequestLogger      { get; }
            public HTTPResponseLogHandler                    HTTPResponseLogger     { get; }
            public HTTPDelegate                              DefaultErrorHandler    { get; }
            public Dictionary<HTTPStatusCode, HTTPDelegate>  ErrorHandlers          { get; }

            #endregion

            public Handlers(HTTPDelegate                              RequestHandler,
                            HTTPRequestLogHandler                     HTTPRequestLogger,
                            HTTPResponseLogHandler                    HTTPResponseLogger,
                            HTTPDelegate                              DefaultErrorHandler,
                            Dictionary<HTTPStatusCode, HTTPDelegate>  ErrorHandlers)

            {

                this.RequestHandler       = RequestHandler;
                this.HTTPRequestLogger    = HTTPRequestLogger;
                this.HTTPResponseLogger   = HTTPResponseLogger;
                this.DefaultErrorHandler  = DefaultErrorHandler;
                this.ErrorHandlers        = ErrorHandlers;

            }

            public static Handlers FromURINode(URINode URINode)

                => new Handlers(URINode?.RequestHandler,
                                URINode?.HTTPRequestLogger,
                                URINode?.HTTPResponseLogger,
                                URINode?.DefaultErrorHandler,
                                URINode?.ErrorHandlers);

            public static Handlers FromMethodNode(HTTPMethodNode MethodNode)

                => new Handlers(MethodNode?.RequestHandler,
                                MethodNode?.HTTPRequestLogger,
                                MethodNode?.HTTPResponseLogger,
                                MethodNode?.DefaultErrorHandler,
                                MethodNode?.ErrorHandlers);

            public static Handlers FromContentTypeNode(ContentTypeNode ContentTypeNode)

                => new Handlers(ContentTypeNode?.RequestHandler,
                                ContentTypeNode?.HTTPRequestLogger,
                                ContentTypeNode?.HTTPResponseLogger,
                                ContentTypeNode?.DefaultErrorHandler,
                                ContentTypeNode?.ErrorHandlers);

        }

        #region Data

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public  const           String           DefaultHTTPServerName  = "GraphDefined Hermod HTTP Server v0.9";

        /// <summary>
        /// The default HTTP server TCP port.
        /// </summary>
        public static readonly  IPPort           DefaultHTTPServerPort  = IPPort.HTTP;

        private readonly        Dictionary<HTTPHostname,       HostnameNode>     _HostnameNodes;
        private readonly        Dictionary<HTTPEventSource_Id, HTTPEventSource>  _EventSources;

        private const    UInt32 ReadTimeout           = 180000U;

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

        /// <summary>
        /// An event called whenever a HTTP request came in.
        /// </summary>
        public HTTPRequestLogEvent   RequestLog    = new HTTPRequestLogEvent();

        /// <summary>
        /// An event called whenever a HTTP request could successfully be processed.
        /// </summary>
        public HTTPResponseLogEvent  ResponseLog   = new HTTPResponseLogEvent();

        /// <summary>
        /// An event called whenever a HTTP request resulted in an error.
        /// </summary>
        public HTTPErrorLogEvent     ErrorLog      = new HTTPErrorLogEvent();

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
            this._HostnameNodes            = new Dictionary<HTTPHostname,       HostnameNode>();
            this._EventSources             = new Dictionary<HTTPEventSource_Id, HTTPEventSource>();

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

            AttachTCPPorts(_TCPServer => _TCPServer.SendTo(this), Ports);

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

            AttachTCPSockets(_TCPServer => _TCPServer.SendTo(this), Sockets);

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
                               _TCPServer.OnNotification      -= this.ProcessArrow;
                               _TCPServer.OnExceptionOccured  -= this.ProcessExceptionOccured;
                               _TCPServer.OnCompleted         -= this.ProcessCompleted;
                           },
                           Ports);

            return this;

        }

        #endregion

        #endregion


        #region NotifyErrors(...)

        private void NotifyErrors(HTTPRequest     HTTPRequest,
                                  TCPConnection   TCPConnection,
                                  DateTime        Timestamp,
                                  HTTPStatusCode  HTTPStatusCode,
                                  HTTPRequest     Request          = null,
                                  HTTPResponse    Response         = null,
                                  String          Error            = null,
                                  Exception       LastException    = null,
                                  Boolean         CloseConnection  = true)
        {

            #region Call OnError delegates

            //var ErrorLogLocal = ErrorLog;
            //if (ErrorLogLocal != null)
            //{
            //    ErrorLogLocal(Timestamp, this, Request, Response, Error, LastException);
            //}

            #endregion

            #region Send error page to HTTP client

            var Content = String.Empty;

            if (Error != null)
                Content += Error + Environment.NewLine;

            if (LastException != null)
                Content += LastException.Message + Environment.NewLine;

            var _HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                    HTTPStatusCode  = HTTPStatusCode,
                                    Date            = Timestamp,
                                    Content         = Content.ToUTF8Bytes()
                                };

            TCPConnection.WriteLineToResponseStream(_HTTPResponse.ToString());

            if (CloseConnection)
                TCPConnection.Close();

            #endregion

        }

        #endregion

        #region ProcessArrow(TCPConnection)

        public void ProcessArrow(TCPConnection TCPConnection)
        {

            //lock (myLock)
            //{

            #region Start

            //TCPConnection.WriteLineToResponseStream(ServiceBanner);
            TCPConnection.NoDelay = true;

            Byte Byte;
            var MemoryStream     = new MemoryStream();
            var EndOfHTTPHeader  = EOLSearch.NotYet;
            var ClientClose      = false;
            var ServerClose      = false;

            #endregion

            try
            {

                do
                {

                    switch (TCPConnection.TryRead(out Byte, MaxInitialWaitingTimeMS: ReadTimeout))
                    {

                        #region DataAvailable

                        case TCPClientResponse.DataAvailable:

                            #region Check for end of HTTP header...

                            if (EndOfHTTPHeader == EOLSearch.NotYet)
                            {

                                // \n
                                if (Byte == 0x0a)
                                    EndOfHTTPHeader = EOLSearch.EoL_Found;

                                // \r
                                else if (Byte == 0x0d)
                                    EndOfHTTPHeader = EOLSearch.R_Read;

                            }

                            // \n after a \r
                            else if (EndOfHTTPHeader == EOLSearch.R_Read)
                            {
                                if (Byte == 0x0a)
                                    EndOfHTTPHeader = EOLSearch.EoL_Found;
                                else
                                    EndOfHTTPHeader = EOLSearch.NotYet;
                            }

                            // \r after a \r\n
                            else if (EndOfHTTPHeader == EOLSearch.EoL_Found)
                            {
                                if (Byte == 0x0d)
                                    EndOfHTTPHeader = EOLSearch.RN_Read;
                                else
                                    EndOfHTTPHeader = EOLSearch.NotYet;
                            }

                            // \r\n\r after a \r\n\r
                            else if (EndOfHTTPHeader == EOLSearch.RN_Read)
                            {
                                if (Byte == 0x0a)
                                    EndOfHTTPHeader = EOLSearch.Double_EoL_Found;
                                else
                                    EndOfHTTPHeader = EOLSearch.NotYet;
                            }

                            #endregion

                            MemoryStream.WriteByte(Byte);

                            #region If end-of-line -> process data...

                            if (EndOfHTTPHeader == EOLSearch.Double_EoL_Found)
                            {

                                if (MemoryStream.Length > 0)
                                {

                                    var RequestTimestamp = DateTime.UtcNow;

                                    #region Check UTF8 encoding

                                    var HTTPHeaderString = String.Empty;

                                    try
                                    {

                                        HTTPHeaderString = Encoding.UTF8.GetString(MemoryStream.ToArray());

                                    }
                                    catch (Exception)
                                    {

                                        NotifyErrors(null,
                                                     TCPConnection,
                                                     RequestTimestamp,
                                                     HTTPStatusCode.BadRequest,
                                                     Error: "Protocol Error: Invalid UTF8 encoding!");

                                    }

                                    #endregion

                                    #region Try to parse the HTTP header

                                    HTTPRequest HttpRequest = null;
                                    var CTS = new CancellationTokenSource();

                                    try
                                    {

                                        HttpRequest = new HTTPRequest(RequestTimestamp,
                                                                      this,
                                                                      CTS.Token,
                                                                      EventTracking_Id.New,
                                                                      new HTTPSource(TCPConnection.RemoteSocket),
                                                                      TCPConnection.LocalSocket,
                                                                      HTTPHeaderString.Trim(),
                                                                      TCPConnection.SSLStream != null
                                                                          ? (Stream) TCPConnection.SSLStream
                                                                          : (Stream) TCPConnection.NetworkStream);

                                    }
                                    catch (Exception e)
                                    {

                                        DebugX.Log("HTTPProcessor (Try to parse the HTTP header): " + e.Message);

                                        NotifyErrors(null,
                                                     TCPConnection,
                                                     RequestTimestamp,
                                                     HTTPStatusCode.BadRequest,
                                                     LastException:  e,
                                                     Error:          "Invalid HTTP header!");

                                    }

                                    #endregion

                                    #region Call RequestLog delegate

                                    if (HttpRequest != null)
                                    {

                                        try
                                        {

                                            RequestLog?.WhenAll(RequestTimestamp,
                                                                this as Object as HTTPAPI,
                                                                HttpRequest);

                                        }
                                        catch (Exception e)
                                        {
                                            DebugX.LogT(nameof(HTTPServer) + " => " + e.Message);
                                        }

                                    }

                                    #endregion

                                    #region Invoke HTTP handler

                                    HTTPResponse _HTTPResponse = null;

                                    //var OnNotificationLocal = OnNotification;
                                    //if (OnNotificationLocal != null &&
                                    if (HttpRequest         != null)
                                    {

                                        // ToDo: How to read request body by application code?!
                                        //_HTTPResponse = OnNotification("TCPConnectionId",
                                        //                               RequestTimestamp,
                                        //                               HttpRequest).Result;

                                        _HTTPResponse = InvokeHandler(HttpRequest).Result;

                                        TCPConnection.WriteToResponseStream((_HTTPResponse.RawHTTPHeader.Trim() +
                                                                            "\r\n\r\n").
                                                                            ToUTF8Bytes());

                                        if (_HTTPResponse.HTTPBodyStream != null)
                                        {
                                            TCPConnection.WriteToResponseStream(_HTTPResponse.HTTPBodyStream);
                                            _HTTPResponse.HTTPBodyStream.Close();
                                            _HTTPResponse.HTTPBodyStream.Dispose();
                                        }

                                        else
                                            TCPConnection.WriteToResponseStream(_HTTPResponse.HTTPBody);

                                        if (_HTTPResponse.Connection.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0)
                                            ServerClose = true;

                                    }

                                    #endregion

                                    #region Call AccessLog delegate

                                    if ( HttpRequest  != null &&
                                        _HTTPResponse != null)
                                    {

                                        try
                                        {

                                            ResponseLog?.WhenAll(RequestTimestamp,
                                                                 this as Object as HTTPAPI,
                                                                 HttpRequest,
                                                                 _HTTPResponse);

                                        }
                                        catch (Exception e)
                                        {
                                            DebugX.LogT(nameof(HTTPServer) + " => " + e.Message);
                                        }

                                    }

                                    #endregion

                                    #region if HTTP Status Code == 4xx | 5xx => Call ErrorLog delegate

                                    if ( HttpRequest  != null &&
                                        _HTTPResponse != null &&
                                        _HTTPResponse.HTTPStatusCode.Code >  400 &&
                                        _HTTPResponse.HTTPStatusCode.Code <= 599)
                                    {

                                        try
                                        {

                                            ErrorLog?.WhenAll(RequestTimestamp,
                                                              this as Object as HTTPAPI,
                                                              HttpRequest,
                                                              _HTTPResponse,
                                                              _HTTPResponse.HTTPStatusCode.ToString());

                                        }
                                        catch (Exception e)
                                        {
                                            DebugX.LogT(nameof(HTTPServer) + " => " + e.Message);
                                        }

                                    }

                                    #endregion

                                }

                                MemoryStream.SetLength(0);
                                MemoryStream.Seek(0, SeekOrigin.Begin);
                                EndOfHTTPHeader = EOLSearch.NotYet;

                            }

                            #endregion

                            break;

                        #endregion

                        #region CanNotRead

                        case TCPClientResponse.CanNotRead:
                            ServerClose = true;
                            break;

                        #endregion

                        #region ClientClose

                        case TCPClientResponse.ClientClose:
                            ClientClose = true;
                            break;

                        #endregion

                        #region Timeout

                        case TCPClientResponse.Timeout:
                            ServerClose = true;
                            break;

                        #endregion

                    }

                } while (!ClientClose && !ServerClose);

            }

            #region Process exceptions

            catch (IOException ioe)
            {

                if      (ioe.Message.StartsWith("Unable to read data from the transport connection")) { }
                else if (ioe.Message.StartsWith("Unable to write data to the transport connection")) { }

                else
                {

                    DebugX.Log("HTTPProcessor: " + ioe.Message);

                    //if (OnError != null)
                    //    OnError(this, DateTime.UtcNow, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), ioe, MemoryStream);

                }

            }

            catch (Exception e)
            {

                DebugX.Log("HTTPProcessor: " + e.Message);

                //if (OnError != null)
                //    OnError(this, DateTime.UtcNow, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), e, MemoryStream);

            }

            #endregion

            #region Close the TCP connection

            try
            {

                TCPConnection.Close(ClientClose
                                        ? ConnectionClosedBy.Client
                                        : ConnectionClosedBy.Server);

            }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
            catch (Exception)
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
            { }

            #endregion

            //}

        }

        #endregion

        #region ProcessExceptionOccured(Sender, Timestamp, ExceptionMessage)

        public void ProcessExceptionOccured(Object     Sender,
                                            DateTime   Timestamp,
                                            Exception  ExceptionMessage)
        {

            //var OnExceptionOccuredLocal = OnExceptionOccured;
            //if (OnExceptionOccuredLocal != null)
            //    OnExceptionOccuredLocal(Sender,
            //                            Timestamp,
            //                            ExceptionMessage);

        }

        #endregion

        #region ProcessCompleted(Sender, Timestamp, Message = null)

        public void ProcessCompleted(Object Sender,
                                     DateTime Timestamp,
                                     String Message = null)
        {

            //var OnCompletedLocal = OnCompleted;
            //if (OnCompletedLocal != null)
            //    OnCompletedLocal(Sender,
            //                     Timestamp,
            //                     Message);

        }

        #endregion


        #region Method Callbacks

        #region HTTP Filters

        private readonly List<HTTPFilter2Delegate> _HTTPFilters = new List<HTTPFilter2Delegate>();

        public void AddFilter(HTTPFilter1Delegate Filter)
        {
            _HTTPFilters.Add((server, request) => Filter(request));
        }

        public void AddFilter(HTTPFilter2Delegate Filter)
        {
            _HTTPFilters.Add(Filter);
        }

        #endregion

        #region HTTP Rewrites

        private readonly List<HTTPRewrite2Delegate> _HTTPRewrites = new List<HTTPRewrite2Delegate>();

        public void Rewrite(HTTPRewrite1Delegate Rewrite)
        {
            _HTTPRewrites.Add((server, request) => Rewrite(request));
        }

        public void Rewrite(HTTPRewrite2Delegate Rewrite)
        {
            _HTTPRewrites.Add(Rewrite);
        }

        #endregion


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

            AddHandler(req => InvokeHandler(new HTTPRequest.Builder(req).SetURI(URITarget)),
                       Hostname,
                       (URITemplate.IsNotNullOrEmpty()) ? URITemplate     : HTTPURI.Parse("/"),
                       HTTPMethod,
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

            AddHandler(req => InvokeHandler(new HTTPRequest.Builder(req).SetURI(URITarget)),
                       HTTPHostname.Any,
                       (URITemplate.IsNotNullOrEmpty()) ? URITemplate     : HTTPURI.Parse("/"),
                       HTTPMethod,
                       HTTPContentType ?? HTTPContentType.HTML_UTF8,
                       null,
                       null,
                       null,
                       null);

        }

        #endregion


        #region (internal) AddHandler(HTTPDelegate, Hostname = "*", URITemplate = "/", HTTPMethod = null, HTTPContentType = null, HostAuthentication = null, URIAuthentication = null, HTTPMethodAuthentication = null, ContentTypeAuthentication = null, DefaultErrorHandler = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="HTTPDelegate">A delegate called for each incoming HTTP request.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        internal void AddHandler(HTTPDelegate              HTTPDelegate,

                                 HTTPHostname?             Hostname                    = null,
                                 HTTPURI?                  URITemplate                 = null,
                                 HTTPMethod?               HTTPMethod                  = null,
                                 HTTPContentType           HTTPContentType             = null,

                                 HTTPAuthentication        URIAuthentication           = null,
                                 HTTPAuthentication        HTTPMethodAuthentication    = null,
                                 HTTPAuthentication        ContentTypeAuthentication   = null,

                                 HTTPRequestLogHandler     HTTPRequestLogger           = null,
                                 HTTPResponseLogHandler    HTTPResponseLogger          = null,

                                 HTTPDelegate              DefaultErrorHandler         = null,
                                 URIReplacement            AllowReplacement            = URIReplacement.Fail)

        {

            lock (_HostnameNodes)
            {

                #region Initial Checks

                if (HTTPDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPDelegate), "The given parameter must not be null!");

                var _Hostname = Hostname ?? HTTPHostname.Any;

                if (HTTPMethod == null && HTTPContentType != null)
                    throw new ArgumentException("If HTTPMethod is null the HTTPContentType must also be null!");

                #endregion

                if (!_HostnameNodes.TryGetValue(_Hostname, out HostnameNode _HostnameNode))
                    _HostnameNode = _HostnameNodes.AddAndReturnValue(_Hostname, new HostnameNode(_Hostname));

                _HostnameNode.AddHandler(HTTPDelegate,

                                         URITemplate,
                                         HTTPMethod,
                                         HTTPContentType,

                                         URIAuthentication,
                                         HTTPMethodAuthentication,
                                         ContentTypeAuthentication,

                                         HTTPRequestLogger,
                                         HTTPResponseLogger,

                                         DefaultErrorHandler,
                                         AllowReplacement);

            }

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate,  HTTPContentType = null, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname              Hostname,
                                      HTTPMethod                HTTPMethod,
                                      HTTPURI                   URITemplate,
                                      HTTPContentType           HTTPContentType             = null,
                                      HTTPAuthentication        URIAuthentication           = null,
                                      HTTPAuthentication        HTTPMethodAuthentication    = null,
                                      HTTPAuthentication        ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler     HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler    HTTPResponseLogger          = null,
                                      HTTPDelegate              DefaultErrorHandler         = null,
                                      HTTPDelegate              HTTPDelegate                = null,
                                      URIReplacement            AllowReplacement            = URIReplacement.Fail)

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

            AddHandler(HTTPDelegate,
                       Hostname,
                       URITemplate,
                       HTTPMethod,
                       HTTPContentType,
                       URIAuthentication,
                       HTTPMethodAuthentication,
                       ContentTypeAuthentication,
                       HTTPRequestLogger,
                       HTTPResponseLogger,
                       DefaultErrorHandler,
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
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname              Hostname,
                                      HTTPMethod                HTTPMethod,
                                      IEnumerable<HTTPURI>      URITemplates,
                                      HTTPContentType           HTTPContentType             = null,
                                      HTTPAuthentication        URIAuthentication           = null,
                                      HTTPAuthentication        HTTPMethodAuthentication    = null,
                                      HTTPAuthentication        ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler     HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler    HTTPResponseLogger          = null,
                                      HTTPDelegate              DefaultErrorHandler         = null,
                                      HTTPDelegate              HTTPDelegate                = null,
                                      URIReplacement            AllowReplacement            = URIReplacement.Fail)

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
                AddHandler(HTTPDelegate,
                           Hostname,
                           URITemplate,
                           HTTPMethod,
                           HTTPContentType,
                           URIAuthentication,
                           HTTPMethodAuthentication,
                           ContentTypeAuthentication,
                           HTTPRequestLogger,
                           HTTPResponseLogger,
                           DefaultErrorHandler,
                           AllowReplacement));

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate,  HTTPContentTypes, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      HTTPURI                       URITemplate,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      HTTPAuthentication            URIAuthentication           = null,
                                      HTTPAuthentication            HTTPMethodAuthentication    = null,
                                      HTTPAuthentication            ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler         HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler        HTTPResponseLogger          = null,
                                      HTTPDelegate                  DefaultErrorHandler         = null,
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
                AddHandler(HTTPDelegate,
                           Hostname,
                           URITemplate,
                           HTTPMethod,
                           contenttype,
                           URIAuthentication,
                           HTTPMethodAuthentication,
                           ContentTypeAuthentication,
                           HTTPRequestLogger,
                           HTTPResponseLogger,
                           DefaultErrorHandler,
                           AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URITemplate,  HTTPContentTypes, HostAuthentication = false, URIAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URITemplates">An enumeration of URI templates.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      IEnumerable<HTTPURI>          URITemplates,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      HTTPAuthentication            URIAuthentication           = null,
                                      HTTPAuthentication            HTTPMethodAuthentication    = null,
                                      HTTPAuthentication            ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler         HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler        HTTPResponseLogger          = null,
                                      HTTPDelegate                  DefaultErrorHandler         = null,
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
                    AddHandler(HTTPDelegate,
                               Hostname,
                               uritemplate,
                               HTTPMethod,
                               contenttype,
                               URIAuthentication,
                               HTTPMethodAuthentication,
                               ContentTypeAuthentication,
                               HTTPRequestLogger,
                               HTTPResponseLogger,
                               DefaultErrorHandler,
                               AllowReplacement);

        }

        #endregion

        #region (internal) ReplaceHandler(HTTPDelegate, Hostname = "*", URITemplate = "/", HTTPMethod = null, HTTPContentType = null, HostAuthentication = null, URIAuthentication = null, HTTPMethodAuthentication = null, ContentTypeAuthentication = null, DefaultErrorHandler = null)

        /// <summary>
        /// Add a method callback for the given URI template.
        /// </summary>
        /// <param name="HTTPDelegate">A delegate called for each incoming HTTP request.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        internal void ReplaceHandler(HTTPDelegate              HTTPDelegate,

                                     HTTPHostname?             Hostname                    = null,
                                     HTTPURI?                  URITemplate                 = null,
                                     HTTPMethod?               HTTPMethod                  = null,
                                     HTTPContentType           HTTPContentType             = null,

                                     HTTPAuthentication        URIAuthentication           = null,
                                     HTTPAuthentication        HTTPMethodAuthentication    = null,
                                     HTTPAuthentication        ContentTypeAuthentication   = null,

                                     HTTPRequestLogHandler     HTTPRequestLogger           = null,
                                     HTTPResponseLogHandler    HTTPResponseLogger          = null,

                                     HTTPDelegate              DefaultErrorHandler         = null)

        {

            lock (_HostnameNodes)
            {

                #region Initial Checks

                if (HTTPDelegate == null)
                    throw new ArgumentNullException(nameof(HTTPDelegate), "The given parameter must not be null!");

                var _Hostname = Hostname ?? HTTPHostname.Any;

                if (HTTPMethod == null && HTTPContentType != null)
                    throw new ArgumentException("If HTTPMethod is null the HTTPContentType must also be null!");

                #endregion

                if (!_HostnameNodes.TryGetValue(_Hostname, out HostnameNode _HostnameNode))
                    _HostnameNode = _HostnameNodes.AddAndReturnValue(_Hostname, new HostnameNode(_Hostname));

                _HostnameNode.AddHandler(HTTPDelegate,

                                         URITemplate,
                                         HTTPMethod,
                                         HTTPContentType,

                                         URIAuthentication,
                                         HTTPMethodAuthentication,
                                         ContentTypeAuthentication,

                                         HTTPRequestLogger,
                                         HTTPResponseLogger,

                                         DefaultErrorHandler);

            }

        }

        #endregion


        #region (internal) GetHandler(Request)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        internal Handlers GetHandlers(HTTPRequest Request)

            => GetHandlers(Request.Host,
                           Request.URI.IsNullOrEmpty() ? HTTPURI.Parse("/") : Request.URI,
                           Request.HTTPMethod,
                           AvailableContentTypes => Request.Accept.BestMatchingContentType(AvailableContentTypes),
                           ParsedURIParameters   => Request.ParsedURIParameters = ParsedURIParameters.ToArray());

        #endregion

        #region (internal) GetHandler(Host = "*", URL = "/", HTTPMethod = HTTPMethod.GET, HTTPContentTypeSelector = null)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        internal Handlers GetHandlers(HTTPHostname                              Host,
                                          HTTPURI                                   URI,
                                          HTTPMethod?                               Method                       = null,
                                          Func<HTTPContentType[], HTTPContentType>  HTTPContentTypeSelector      = null,
                                          Action<IEnumerable<String>>               ParsedURIParametersDelegate  = null)
        {

            URI                      = URI.IsNullOrEmpty()      ? HTTPURI.Parse("/") : URI;
            var httpMethod           = Method                  ?? HTTPMethod.GET;
            HTTPContentTypeSelector  = HTTPContentTypeSelector ?? (v => HTTPContentType.HTML_UTF8);

            lock (_HostnameNodes)
            {

                #region Get HostNode or "*" or fail

                if (!_HostnameNodes.TryGetValue(Host, out HostnameNode _HostNode))
                    if (!_HostnameNodes.TryGetValue(HTTPHostname.Any, out _HostNode))
                        return null;
                        //return GetErrorHandler(Host, URL, HTTPMethod, HTTPContentType, HTTPStatusCode.BadRequest);

                #endregion

                #region Try to find the best matching URLNode...

                var _RegexList    = from   __URLNode
                                    in     _HostNode.URINodes
                                    select new {
                                        URLNode = __URLNode,
                                        Regex   = __URLNode.URIRegex
                                    };

                var _AllTemplates = from   _RegexTupel
                                    in     _RegexList
                                    select new {
                                        URLNode = _RegexTupel.URLNode,
                                        Match   = _RegexTupel.Regex.Match(URI.ToString())
                                    };

                var _Matches      = from    _Match
                                    in      _AllTemplates
                                    where   _Match.Match.Success
                                    where   _Match.URLNode.Contains(httpMethod)
                                    orderby 100*_Match.URLNode.SortLength +
                                                _Match.URLNode.ParameterCount
                                            descending
                                    select  new {
                                        URLNode = _Match.URLNode,
                                        Match   = _Match.Match
                                    };

                #endregion

                #region ...or return HostNode

                if (!_Matches.Any())
                {

                    //if (_HostNode.RequestHandler != null)
                    //    return _HostNode.RequestHandler;

                    return null;

                }

                #endregion


                HTTPMethodNode  _HTTPMethodNode       = null;
                ContentTypeNode _HTTPContentTypeNode  = null;

                // Caused e.g. by the naming of the variables within the
                // URI templates, there could be multiple matches!
                //foreach (var _Match in _Matches)
                //{

                var FilteredByMethod = _Matches.Where (match      => match.URLNode.Contains(httpMethod)).
                                                Select(match      => match.URLNode.Get(httpMethod)).
                                                Select(methodnode => HTTPContentTypeSelector(methodnode.ContentTypes.ToArray())).
                                                ToArray();

                //foreach (var aa in FilteredByMethod)
                //{

                //    var BestMatchingContentType = HTTPContentTypeSelector(aa.HTTPContentTypes.Keys.ToArray());

                //    //if (aa.HTTPContentTypes

                //}

                // Use best matching URL Handler!
                var _Match2 = _Matches.First();

                #region Copy MethodHandler Parameters

                var _Parameters = new List<String>();
                for (var i = 1; i < _Match2.Match.Groups.Count; i++)
                    _Parameters.Add(_Match2.Match.Groups[i].Value);

                var ParsedURIParametersDelegateLocal = ParsedURIParametersDelegate;
                if (ParsedURIParametersDelegateLocal != null)
                    ParsedURIParametersDelegate(_Parameters);

                #endregion

                // If HTTPMethod was found...
                if (_Match2.URLNode.TryGet(httpMethod, out _HTTPMethodNode))
                {

                    var BestMatchingContentType = HTTPContentTypeSelector(_HTTPMethodNode.ContentTypes.ToArray());

                    if (BestMatchingContentType == HTTPContentType.ALL)
                    {

                        // No content types defined...
                        if (!_HTTPMethodNode.Any())
                            return Handlers.FromMethodNode(_HTTPMethodNode);

                        // A single content type is defined...
                        else if (_HTTPMethodNode.Count() == 1)
                            return Handlers.FromContentTypeNode(_HTTPMethodNode.FirstOrDefault());

                        else
                            throw new ArgumentException(String.Concat(URI, " ", _HTTPMethodNode, " but multiple content type choices!"));

                    }

                    // The requested content type was found...
                    else if (_HTTPMethodNode.TryGet(BestMatchingContentType, out _HTTPContentTypeNode))
                        return Handlers.FromContentTypeNode(_HTTPContentTypeNode);


                    else
                        return Handlers.FromMethodNode(_HTTPMethodNode);

                }

                //}

                // No HTTPMethod was found => return best matching URL Handler
                return Handlers.FromURINode(_Match2.URLNode);

                //return GetErrorHandler(Host, URL, HTTPMethod, HTTPContentType, HTTPStatusCode.BadRequest);

            }

        }

        #endregion


        #region InvokeHandler(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        public async Task<HTTPResponse> InvokeHandler(HTTPRequest Request)
        {

            HTTPResponse _HTTPResponse = null;

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

            var HTTPHandlers = GetHandlers(Request);

            if (HTTPHandlers != null)
            {

                if (HTTPHandlers.HTTPRequestLogger != null)
                {
                    try
                    {

                        await HTTPHandlers.HTTPRequestLogger(DateTime.UtcNow,
                                                             null,
                                                             Request);

                    }
                    catch (Exception e)
                    {
                        DebugX.LogT("HTTP server request logger exception: " + e.Message);
                    }
                }

                try
                {

                    _HTTPResponse = await HTTPHandlers.RequestHandler(Request);

                }
                catch (Exception e)
                {

                    DebugX.LogT("HTTP server request processing exception: " + e.Message);

                    _HTTPResponse = new HTTPResponse.Builder(Request) {
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

                if (HTTPHandlers.HTTPResponseLogger != null)
                {
                    try
                    {

                        await HTTPHandlers.HTTPResponseLogger(DateTime.UtcNow,
                                                              null,
                                                              Request,
                                                              _HTTPResponse);

                    }
                    catch (Exception e)
                    {
                        DebugX.LogT("HTTP server request logger exception: " + e.Message);
                    }
                }

                return _HTTPResponse;

            }

            return _HTTPResponse ?? new HTTPResponse.Builder(Request) {
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
        public HTTPEventSource AddEventSource(HTTPEventSource_Id              EventIdentification,
                                              UInt32                          MaxNumberOfCachedEvents   = 500,
                                              TimeSpan?                       RetryIntervall            = null,
                                              Boolean                         EnableLogging             = true,
                                              Func<String, DateTime, String>  LogfileName               = null)
        {

            lock (_HostnameNodes)
            {

                if (_EventSources.ContainsKey(EventIdentification))
                    throw new ArgumentException("Duplicate event identification!");

                return _EventSources.AddAndReturnValue(EventIdentification,
                                                       new HTTPEventSource(EventIdentification,
                                                                           MaxNumberOfCachedEvents,
                                                                           RetryIntervall,
                                                                           EnableLogging,
                                                                           LogfileName));

            }

        }

        #endregion

        #region AddEventSource(EventIdentification, URITemplate, MaxNumberOfCachedEvents = 500, RetryIntervall = null, LogfileName = null, ...)

        /// <summary>
        /// Add a method call back for the given URI template and
        /// add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// 
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="HttpMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="URIAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public HTTPEventSource AddEventSource(HTTPEventSource_Id              EventIdentification,
                                              HTTPURI                         URITemplate,

                                              UInt32                          MaxNumberOfCachedEvents     = 500,
                                              TimeSpan?                       RetryIntervall              = null,
                                              Boolean                         EnableLogging               = true,
                                              String                          LogfilePrefix               = null,
                                              Func<String, DateTime, String>  LogfileName                 = null,
                                              String                          LogfileReloadSearchPattern  = null,

                                              HTTPHostname?                   Hostname                    = null,
                                              HTTPMethod?                     HttpMethod                  = null,
                                              HTTPContentType                 HTTPContentType             = null,

                                              HTTPAuthentication              URIAuthentication           = null,
                                              HTTPAuthentication              HTTPMethodAuthentication    = null,

                                              HTTPDelegate                    DefaultErrorHandler         = null)

        {

            lock (_EventSources)
            {

                #region Get or Create Event Source

                if (!_EventSources.TryGetValue(EventIdentification, out HTTPEventSource _HTTPEventSource))
                {

                    _HTTPEventSource = _EventSources.AddAndReturnValue(EventIdentification,
                                                                       new HTTPEventSource(EventIdentification,
                                                                                           MaxNumberOfCachedEvents,
                                                                                           RetryIntervall,
                                                                                           EnableLogging,
                                                                                           EnableLogging || LogfileName != null
                                                                                               ? LogfileName ?? ((eventid, time) => String.Concat(LogfilePrefix ?? "",
                                                                                                                                                  eventid, "_",
                                                                                                                                                  time.Year, "-", time.Month.ToString("D2"),
                                                                                                                                                  ".log"))
                                                                                               : null,
                                                                                           LogfileReloadSearchPattern ?? String.Concat(LogfilePrefix ?? "", EventIdentification, "_*.log")));

                }

                #endregion

                AddHandler(Request => {

                               var _LastEventId         = 0UL;
                               var _EventSource         = Get(EventIdentification);

                               if (Request.TryGet("Last-Event-ID", out ulong _Client_LastEventId))
                                   _LastEventId         = _Client_LastEventId;

                               var _HTTPEvents          = (from   _HTTPEvent
                                                           in     _EventSource.GetAllEventsGreater(_LastEventId)
                                                           where  _HTTPEvent != null
                                                           select _HTTPEvent.ToString()).ToArray(); // For thread safety!

                               // Transform HTTP events into an UTF8 string
                               var _ResourceContent     = String.Empty;

                               if (_HTTPEvents.Length > 0)
                                   _ResourceContent     = Environment.NewLine + _HTTPEvents.Aggregate((a, b) => a + Environment.NewLine + b) + Environment.NewLine;

                               else
                                   _ResourceContent += Environment.NewLine + "retry: " + ((UInt32)_EventSource.RetryIntervall.TotalMilliseconds) + Environment.NewLine + Environment.NewLine;


                               return Task.FromResult(
                                   new HTTPResponse.Builder(Request) {
                                       HTTPStatusCode  = HTTPStatusCode.OK,
                                       Server          = HTTPServer.DefaultHTTPServerName,
                                       ContentType     = HTTPContentType.EVENTSTREAM,
                                       CacheControl    = "no-cache",
                                       Connection      = "keep-alive",
                                       KeepAlive       = new KeepAliveType(TimeSpan.FromSeconds(2 * _EventSource.RetryIntervall.TotalSeconds)),
                                       Content         = _ResourceContent.ToUTF8Bytes()
                                   }.AsImmutable);

                           },
                           Hostname,
                           URITemplate,
                           HttpMethod      ?? HTTPMethod.GET,
                           HTTPContentType ?? HTTPContentType.EVENTSTREAM,

                           URIAuthentication,
                           HTTPMethodAuthentication,
                           null,

                           null,
                           null,

                           DefaultErrorHandler);

                return _HTTPEventSource;

            }

        }

        #endregion


        #region Get   (EventSourceIdentification)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public HTTPEventSource Get(HTTPEventSource_Id EventSourceIdentification)
        {

            lock (_EventSources)
            {

                if (_EventSources.TryGetValue(EventSourceIdentification, out HTTPEventSource _HTTPEventSource))
                    return _HTTPEventSource;

                return null;

            }

        }

        #endregion

        #region TryGet(EventSourceIdentification, out EventSource)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        public Boolean TryGet(HTTPEventSource_Id EventSourceIdentification, out HTTPEventSource EventSource)
        {

            lock (_EventSources)
            {
                return _EventSources.TryGetValue(EventSourceIdentification, out EventSource);
            }

        }

        #endregion

        #region GetEventSources(IncludeEventSource = null)

        /// <summary>
        /// An enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        public IEnumerable<HTTPEventSource> GetEventSources(Func<HTTPEventSource, Boolean> IncludeEventSource = null)
        {

            lock (_HostnameNodes)
            {

                if (IncludeEventSource == null)
                    foreach (var EventSource in _EventSources.Values)
                        yield return EventSource;

                else
                    foreach (var EventSource in _EventSources.Values)
                        if (IncludeEventSource(EventSource))
                            yield return EventSource;

            }

        }

        #endregion


        #region GetEventSource(EventSourceIdentification)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public HTTPEventSource GetEventSource(HTTPEventSource_Id EventSourceIdentification)
            => Get(EventSourceIdentification);

        #endregion

        #region UseEventSource(EventSourceIdentification, Action)

        /// <summary>
        /// Call the given delegate for the event source identified
        /// by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="Action">A delegate.</param>
        public void UseEventSource(HTTPEventSource_Id       EventSourceIdentification,
                                   Action<HTTPEventSource>  Action)
        {

            if (Action == null)
                return;

            if (TryGet(EventSourceIdentification, out HTTPEventSource EventSource))
                Action(EventSource);

        }

        #endregion

        #region UseEventSource(EventSourceIdentification, DataSource, Action)

        /// <summary>
        /// Call the given delegate for the event source identified
        /// by the given event source identification.
        /// </summary>
        /// <typeparam name="T">The type of the return values.</typeparam>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="DataSource">A enumeration of data.</param>
        /// <param name="Action">A delegate.</param>
        public void UseEventSource<T>(HTTPEventSource_Id          EventSourceIdentification,
                                      IEnumerable<T>              DataSource,
                                      Action<HTTPEventSource, T>  Action)
        {

            if (DataSource?.Any() != true || Action == null)
                return;

            if (TryGet(EventSourceIdentification, out HTTPEventSource EventSource))
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
        public Boolean TryGetEventSource(HTTPEventSource_Id EventSourceIdentification, out HTTPEventSource EventSource)

            => TryGet(EventSourceIdentification, out EventSource);

        #endregion

        #endregion

        #region HTTP Errors

        #region GetErrorHandler(Host, URL, HTTPMethod = null, HTTPContentType = null, HTTPStatusCode = null)

        /// <summary>
        /// Return the best matching error handler for the given parameters.
        /// </summary>
        public Tuple<MethodInfo, IEnumerable<Object>> GetErrorHandler(String           Host,
                                                                      String           URL,
                                                                      HTTPMethod?      HTTPMethod       = null,
                                                                      HTTPContentType  HTTPContentType  = null,
                                                                      HTTPStatusCode   HTTPStatusCode   = null)

        {

            lock (_HostnameNodes)
            {
                return null;
            }

        }

        #endregion

        #endregion

    }

}
