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

using System.Text;
using System.Reflection;
using System.Security.Authentication;
using System.Collections.Concurrent;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Services;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate IUser?         HTTPAuth1Delegate   (                   HTTPRequest Request);
    public delegate IUser?         HTTPAuth2Delegate   (HTTPServer Server, HTTPRequest Request);

    public delegate HTTPResponse?  HTTPFilter1Delegate (                   HTTPRequest Request);
    public delegate HTTPResponse?  HTTPFilter2Delegate (HTTPServer Server, HTTPRequest Request);

    public delegate HTTPRequest?   HTTPRewrite1Delegate(                   HTTPRequest Request);
    public delegate HTTPRequest?   HTTPRewrite2Delegate(HTTPServer Server, HTTPRequest Request);


    /// <summary>
    /// A multitenant HTTP/1.1 server.
    /// </summary>
    /// <typeparam name="T">The type of a collection of tenants.</typeparam>
    /// <typeparam name="U">The type of the tenants.</typeparam>
    public class HTTPServer<T, U> : IHTTPServer
        where T : IEnumerable<U>
    {

        #region Data

        private readonly HTTPServer                             httpServer;
        private readonly ConcurrentDictionary<HTTPHostname, T>  multitenancy;

        #endregion

        #region Properties

        /// <summary>
        /// The default HTTP server name, used whenever no HTTP "Host"-header had been given.
        /// </summary>
        public String                                                     DefaultServerName
            => httpServer.DefaultServerName;

        /// <summary>
        /// An associated HTTP security object.
        /// </summary>
        public HTTPSecurity?                                              HTTPSecurity
            => httpServer.HTTPSecurity;

        /// <summary>
        /// The optional delegate to select a TLS server certificate.
        /// </summary>
        public ServerCertificateSelectorDelegate?                         ServerCertificateSelector
            => httpServer.ServerCertificateSelector;

        /// <summary>
        /// The optional delegate to verify the TLS client certificate used for authentication.
        /// </summary>
        public RemoteTLSClientCertificateValidationHandler<IHTTPServer>?  ClientCertificateValidator
            => httpServer.ClientCertificateValidator;

        /// <summary>
        /// The optional delegate to select the TLS client certificate used for authentication.
        /// </summary>
        public LocalCertificateSelectionHandler?                          LocalCertificateSelector
            => httpServer.LocalCertificateSelector;

        /// <summary>
        /// The TLS protocol(s) allowed for this connection.
        /// </summary>
        public SslProtocols                                               AllowedTLSProtocols
            => httpServer.AllowedTLSProtocols;

        /// <summary>
        /// Is the server already started?
        /// </summary>
        public Boolean                                                    IsStarted
            => httpServer.IsStarted;

        /// <summary>
        /// The current number of attached TCP clients.
        /// </summary>
        public UInt32                                                     NumberOfConnectedClients
            => httpServer.NumberOfConnectedClients;

        /// <summary>
        /// The DNS defines which DNS servers to use.
        /// </summary>
        public DNSClient                                                  DNSClient
            => httpServer.DNSClient;

        #endregion

        #region Events

        /// <summary>
        /// An event called whenever a HTTP request came in.
        /// </summary>
        public HTTPRequestLogEvent   RequestLog    = new ();

        /// <summary>
        /// An event called whenever a HTTP request could successfully be processed.
        /// </summary>
        public HTTPResponseLogEvent  ResponseLog   = new ();

        /// <summary>
        /// An event called whenever a HTTP request resulted in an error.
        /// </summary>
        public HTTPErrorLogEvent     ErrorLog      = new ();

        #endregion

        #region Constructor(s)

        #region HTTPServer(TCPPort = null, DefaultServerName = DefaultHTTPServerName, ...)

        /// <summary>
        /// Initialize the multitenant HTTP server using the given parameters.
        /// </summary>
        /// <param name="TCPPort">The TCP port to listen on.</param>
        /// <param name="DefaultServerName">The default HTTP server name, used whenever no HTTP Host-header has been given.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="ServerThreadNameCreator">Sets the optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPrioritySetter">Sets the optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// 
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="AutoStart">Start the HTTP server thread immediately (default: no).</param>
        public HTTPServer(IPPort?                                                    TCPPort                      = null,
                          String                                                     DefaultServerName            = HTTPServer.DefaultHTTPServerName,
                          String?                                                    ServiceName                  = null,

                          ServerCertificateSelectorDelegate?                         ServerCertificateSelector    = null,
                          RemoteTLSClientCertificateValidationHandler<IHTTPServer>?  ClientCertificateValidator   = null,
                          LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                          SslProtocols?                                              AllowedTLSProtocols          = null,
                          Boolean?                                                   ClientCertificateRequired    = null,
                          Boolean?                                                   CheckCertificateRevocation   = null,

                          ServerThreadNameCreatorDelegate?                           ServerThreadNameCreator      = null,
                          ServerThreadPriorityDelegate?                              ServerThreadPrioritySetter   = null,
                          Boolean?                                                   ServerThreadIsBackground     = true,
                          ConnectionIdBuilder?                                       ConnectionIdBuilder          = null,
                          TimeSpan?                                                  ConnectionTimeout            = null,
                          UInt32?                                                    MaxClientConnections         = TCPServer.__DefaultMaxClientConnections,

                          DNSClient?                                                 DNSClient                    = null,
                          Boolean                                                    AutoStart                    = false)

            : this(new HTTPServer(
                       TCPPort,
                       DefaultServerName,
                       ServiceName,

                       ServerCertificateSelector,
                       ClientCertificateValidator,
                       LocalCertificateSelector,
                       AllowedTLSProtocols,
                       ClientCertificateRequired,
                       CheckCertificateRevocation,

                       ServerThreadNameCreator,
                       ServerThreadPrioritySetter,
                       ServerThreadIsBackground,
                       ConnectionIdBuilder,
                       ConnectionTimeout,
                       MaxClientConnections,

                       DNSClient,
                       AutoStart
                   ))

        {  }

        #endregion

        #region HTTPServer(HTTPServer)

        /// <summary>
        /// Initialize the multitenant HTTP server using the given parameters.
        /// </summary>
        /// <param name="HTTPServer">An existing non-multitenant HTTP server.</param>
        public HTTPServer(HTTPServer HTTPServer)
        {

            this.httpServer    = HTTPServer;
            this.multitenancy  = new ConcurrentDictionary<HTTPHostname, T>();

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

            if (multitenancy.TryGetValue(Hostname, out Tenants))
                foreach (var Tenant in Tenants)
                    Set.Add(Tenant);

            if (multitenancy.TryGetValue(Hostname.AnyHost, out Tenants))
                foreach (var Tenant in Tenants)
                    Set.Add(Tenant);

            if (multitenancy.TryGetValue(Hostname.AnyPort, out Tenants))
                foreach (var Tenant in Tenants)
                    Set.Add(Tenant);

            if (multitenancy.TryGetValue(HTTPHostname.Any, out Tenants))
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

            => multitenancy.TryGetValue(Hostname, out Tenants);

        #endregion

        #region TryAddTenants(Hostname, Tenants)

        /// <summary>
        ///Try to return all tenants available for the given hostname.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="Tenants">A tenant.</param>
        public Boolean TryAddTenants(HTTPHostname  Hostname,
                                     T             Tenants)

            => multitenancy.TryAdd(Hostname, Tenants);

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

            if (multitenancy.TryRemove(Hostname, out _Tenants))
                return _Tenants;

            return default(T);

        }

        #endregion

        #endregion


        #region Manage the underlying TCP sockets...

        #region AttachTCPPorts  (params Ports)

        public async Task<IHTTPServer> AttachTCPPorts(params IPPort[] Ports)
        {

            await httpServer.AttachTCPPorts(Ports);

            return this;

        }

        #endregion

        #region AttachTCPSockets(params Sockets)

        public async Task<IHTTPServer> AttachTCPSockets(params IPSocket[] Sockets)
        {

            await httpServer.AttachTCPSockets(Sockets);

            return this;

        }

        #endregion

        #region DetachTCPPorts  (params Sockets)

        public async Task<IHTTPServer> DetachTCPPorts(params IPPort[] Ports)
        {

            await httpServer.DetachTCPPorts(Ports);

            return this;

        }

        #endregion

        #endregion


        #region Method Callbacks

        #region Redirect(HTTPAPI, Hostname, HTTPMethod, URLTemplate, HTTPContentType, URLTarget)

        /// <summary>
        /// Add a URL based method redirect for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URLTarget">The target URL of the redirect.</param>
        public void Redirect(HTTPAPI          HTTPAPI,
                             HTTPHostname     Hostname,
                             HTTPMethod       HTTPMethod,
                             HTTPPath         URLTemplate,
                             HTTPContentType  HTTPContentType,
                             HTTPPath         URLTarget)

            => httpServer.Redirect(HTTPAPI,
                                   Hostname,
                                   HTTPMethod,
                                   URLTemplate,
                                   HTTPContentType,
                                   URLTarget);

        #endregion

        #region Redirect(HTTPAPI, HTTPMethod, URLTemplate, HTTPContentType, URLTarget)

        /// <summary>
        /// Add a URL based method redirect for the given URL template.
        /// </summary>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URLTarget">The target URL of the redirect.</param>
        public void Redirect(HTTPAPI          HTTPAPI,
                             HTTPMethod       HTTPMethod,
                             HTTPPath         URLTemplate,
                             HTTPContentType  HTTPContentType,
                             HTTPPath         URLTarget)

            => httpServer.Redirect(HTTPAPI,
                                   HTTPMethod,
                                   URLTemplate,
                                   HTTPContentType,
                                   URLTarget);

        #endregion


        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate, HTTPContentType = null, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

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
        public void AddMethodCallback(HTTPAPI                  HTTPAPI,
                                      HTTPHostname             Hostname,
                                      HTTPMethod               HTTPMethod,
                                      HTTPPath                 URLTemplate,
                                      HTTPContentType?         HTTPContentType             = null,
                                      Boolean                  OpenEnd                     = false,
                                      HTTPAuthentication?      URLAuthentication           = null,
                                      HTTPAuthentication?      HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?      ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?  HTTPResponseLogger          = null,
                                      HTTPDelegate?            DefaultErrorHandler         = null,
                                      HTTPDelegate?            HTTPDelegate                = null,
                                      URLReplacement           AllowReplacement            = URLReplacement.Fail)

            => httpServer.AddMethodCallback(
                   HTTPAPI,
                   Hostname,
                   HTTPMethod,
                   URLTemplate,
                   HTTPContentType,
                   OpenEnd,
                   URLAuthentication,
                   HTTPMethodAuthentication,
                   ContentTypeAuthentication,
                   HTTPRequestLogger,
                   HTTPResponseLogger,
                   DefaultErrorHandler,
                   HTTPDelegate,
                   AllowReplacement
               );

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplates, HTTPContentType = null, ..., HTTPDelegate = null, AllowReplacement = URLReplacement.Fail)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplates">An enumeration of URL templates.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPAPI                  HTTPAPI,
                                      HTTPHostname             Hostname,
                                      HTTPMethod               HTTPMethod,
                                      IEnumerable<HTTPPath>    URLTemplates,
                                      HTTPContentType?         HTTPContentType             = null,
                                      Boolean                  OpenEnd                     = false,
                                      HTTPAuthentication?      URLAuthentication           = null,
                                      HTTPAuthentication?      HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?      ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?  HTTPResponseLogger          = null,
                                      HTTPDelegate?            DefaultErrorHandler         = null,
                                      HTTPDelegate?            HTTPDelegate                = null,
                                      URLReplacement           AllowReplacement            = URLReplacement.Fail)

            => httpServer.AddMethodCallback(
                   HTTPAPI,
                   Hostname,
                   HTTPMethod,
                   URLTemplates,
                   HTTPContentType,
                   OpenEnd,
                   URLAuthentication,
                   HTTPMethodAuthentication,
                   ContentTypeAuthentication,
                   HTTPRequestLogger,
                   HTTPResponseLogger,
                   DefaultErrorHandler,
                   HTTPDelegate,
                   AllowReplacement
               );

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate, HTTPContentTypes, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPAPI                        HTTPAPI,
                                      HTTPHostname                   Hostname,
                                      HTTPMethod                     HTTPMethod,
                                      HTTPPath                       URLTemplate,
                                      IEnumerable<HTTPContentType>   HTTPContentTypes,
                                      Boolean                        OpenEnd                     = false,
                                      HTTPAuthentication?            URLAuthentication           = null,
                                      HTTPAuthentication?            HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?            ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?         HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?        HTTPResponseLogger          = null,
                                      HTTPDelegate?                  DefaultErrorHandler         = null,
                                      HTTPDelegate?                  HTTPDelegate                = null,
                                      URLReplacement                 AllowReplacement            = URLReplacement.Fail)

            => httpServer.AddMethodCallback(
                   HTTPAPI,
                   Hostname,
                   HTTPMethod,
                   URLTemplate,
                   HTTPContentTypes,
                   OpenEnd,
                   URLAuthentication,
                   HTTPMethodAuthentication,
                   ContentTypeAuthentication,
                   HTTPRequestLogger,
                   HTTPResponseLogger,
                   DefaultErrorHandler,
                   HTTPDelegate,
                   AllowReplacement
               );

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate, HTTPContentTypes, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplates">An enumeration of URL templates.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPAPI                       HTTPAPI,
                                      HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      IEnumerable<HTTPPath>         URLTemplates,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      Boolean                       OpenEnd                     = false,
                                      HTTPAuthentication?           URLAuthentication           = null,
                                      HTTPAuthentication?           HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?           ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?        HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?       HTTPResponseLogger          = null,
                                      HTTPDelegate?                 DefaultErrorHandler         = null,
                                      HTTPDelegate?                 HTTPDelegate                = null,
                                      URLReplacement                AllowReplacement            = URLReplacement.Fail)

            => httpServer.AddMethodCallback(
                   HTTPAPI,
                   Hostname,
                   HTTPMethod,
                   URLTemplates,
                   HTTPContentTypes,
                   OpenEnd,
                   URLAuthentication,
                   HTTPMethodAuthentication,
                   ContentTypeAuthentication,
                   HTTPRequestLogger,
                   HTTPResponseLogger,
                   DefaultErrorHandler,
                   HTTPDelegate,
                   AllowReplacement
               );

        #endregion


        #region (protected) GetHandlers(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        protected HTTPServer.HTTPRequestHandle?

            GetHandlers(HTTPHostname                               Host,
                        HTTPPath                                   URL,
                        out String?                                ErrorResponse,
                        HTTPMethod?                                HTTPMethod                    = null,
                        Func<HTTPContentType[], HTTPContentType>?  HTTPContentTypeSelector       = null,
                        Action<IEnumerable<String>>?               ParsedURLParametersDelegate   = null)

            => httpServer.GetRequestHandle(Host,
                                           URL,
                                           out ErrorResponse,
                                           HTTPMethod,
                                           HTTPContentTypeSelector,
                                           ParsedURLParametersDelegate);

        #endregion

        public void AddFilter(HTTPFilter1Delegate Filter)
        {
            httpServer.AddFilter(Filter);
        }

        public void AddFilter(HTTPFilter2Delegate Filter)
        {
            httpServer.AddFilter(Filter);
        }

        public void Rewrite(HTTPRewrite1Delegate Rewrite)
        {
            httpServer.Rewrite(Rewrite);
        }

        public void Rewrite(HTTPRewrite2Delegate Rewrite)
        {
            httpServer.Rewrite(Rewrite);
        }


        #region InvokeHandler(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        public Task<HTTPResponse> InvokeRequestHandle(HTTPRequest Request)

            => httpServer.InvokeRequestHandle(Request);

        #endregion

        #endregion

        #region HTTP Server Sent Events

        #region AddEventSource(EventIdentification, MaxNumberOfCachedEvents, RetryInterval  = null, LogfileName = null)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryInterval ">The retry interval.</param>
        /// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        /// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        public HTTPEventSource<TData> AddEventSource<TData>(HTTPEventSource_Id               EventIdentification,
                                                            HTTPAPI                          HTTPAPI,
                                                            UInt32                           MaxNumberOfCachedEvents      = 500,
                                                            TimeSpan?                        RetryInterval                = null,
                                                            Func<TData, String>?             DataSerializer               = null,
                                                            Func<String, TData>?             DataDeserializer             = null,
                                                            Boolean                          EnableLogging                = true,
                                                            String?                          LogfilePath                  = null,
                                                            String?                          LogfilePrefix                = null,
                                                            Func<String, DateTime, String>?  LogfileName                  = null,
                                                            String?                          LogfileReloadSearchPattern   = null)

            => httpServer.AddEventSource(
                              EventIdentification,
                              HTTPAPI,
                              MaxNumberOfCachedEvents,
                              RetryInterval ,
                              DataSerializer,
                              DataDeserializer,
                              EnableLogging,
                              LogfilePath,
                              LogfilePrefix,
                              LogfileName,
                              LogfileReloadSearchPattern
                          );

        #endregion

        #region AddEventSource(EventIdentification, URLTemplate, MaxNumberOfCachedEvents = 500, RetryInterval  = null, EnableLogging = false, LogfileName = null, ...)

        /// <summary>
        /// Add a HTTP Sever Sent Events source and a method call back for the given URL template.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// 
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="IncludeFilterAtRuntime">Include this events within the HTTP SSE output. Can e.g. be used to filter events by HTTP users.</param>
        /// <param name="RetryInterval ">The retry interval.</param>
        /// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        /// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        /// <param name="EnableLogging">Enables storing and reloading events </param>
        /// <param name="LogfilePrefix">A prefix for the log file names or locations.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public HTTPEventSource<TData> AddEventSource<TData>(HTTPEventSource_Id                EventIdentification,
                                                            HTTPAPI                           HTTPAPI,
                                                            HTTPPath                          URLTemplate,

                                                            UInt32                            MaxNumberOfCachedEvents      = 500,
                                                            Func<HTTPEvent<TData>, Boolean>?  IncludeFilterAtRuntime       = null,
                                                            TimeSpan?                         RetryInterval                = null,
                                                            Func<TData, String>?              DataSerializer               = null,
                                                            Func<String, TData>?              DataDeserializer             = null,
                                                            Boolean                           EnableLogging                = true,
                                                            String?                           LogfilePath                  = null,
                                                            String?                           LogfilePrefix                = null,
                                                            Func<String, DateTime, String>?   LogfileName                  = null,
                                                            String?                           LogfileReloadSearchPattern   = null,

                                                            HTTPHostname?                     Hostname                     = null,
                                                            HTTPMethod?                       HTTPMethod                   = null,
                                                            HTTPContentType?                  HTTPContentType              = null,

                                                            Boolean                           RequireAuthentication        = false,
                                                            HTTPAuthentication?               URLAuthentication            = null,
                                                            HTTPAuthentication?               HTTPMethodAuthentication     = null,

                                                            HTTPDelegate?                     DefaultErrorHandler          = null)

            => httpServer.AddEventSource(EventIdentification,
                                         HTTPAPI,
                                         URLTemplate,

                                         MaxNumberOfCachedEvents,
                                         IncludeFilterAtRuntime,
                                         RetryInterval ,
                                         DataSerializer,
                                         DataDeserializer,
                                         EnableLogging,
                                         LogfilePath,
                                         LogfilePrefix,
                                         LogfileName,
                                         LogfileReloadSearchPattern,

                                         Hostname,
                                         HTTPMethod,
                                         HTTPContentType,

                                         RequireAuthentication,
                                         URLAuthentication,
                                         HTTPMethodAuthentication,

                                         DefaultErrorHandler);

        #endregion


        #region Get   (EventSourceIdentification)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public IHTTPEventSource Get(HTTPEventSource_Id EventSourceIdentification)

            => httpServer.Get(EventSourceIdentification);


        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public IHTTPEventSource<TData> Get<TData>(HTTPEventSource_Id EventSourceIdentification)

            => httpServer.Get<TData>(EventSourceIdentification);

        #endregion

        #region TryGet(EventSourceIdentification, EventSource)

        /// <summary>
        /// Try to return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        public Boolean TryGet(HTTPEventSource_Id    EventSourceIdentification,
                              out IHTTPEventSource  EventSource)

            => httpServer.TryGet(EventSourceIdentification,
                                 out EventSource);


        /// <summary>
        /// Try to return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        public Boolean TryGet<TData>(HTTPEventSource_Id           EventSourceIdentification,
                                     out IHTTPEventSource<TData>  EventSource)

            => httpServer.TryGet(EventSourceIdentification,
                                 out EventSource);

        #endregion

        #region EventSources(IncludeEventSource = null)

        /// <summary>
        /// Return a filtered enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        public IEnumerable<IHTTPEventSource> EventSources(Func<IHTTPEventSource, Boolean>?  IncludeEventSource   = null)

            => httpServer.EventSources(IncludeEventSource);


        /// <summary>
        /// Return a filtered enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        public IEnumerable<IHTTPEventSource<TData>> EventSources<TData>(Func<IHTTPEventSource, Boolean>?  IncludeEventSource   = null)

            => httpServer.EventSources<TData>(IncludeEventSource);

        #endregion

        #endregion

        #region GetErrorHandler(Host, URL, HTTPMethod = null, HTTPContentType = null, HTTPStatusCode = null)

        /// <summary>
        /// Return the best matching error handler for the given parameters.
        /// </summary>
        public Tuple<MethodInfo, IEnumerable<Object>> GetErrorHandler(String            Host,
                                                                      String            URL,
                                                                      HTTPMethod?       HTTPMethod        = null,
                                                                      HTTPContentType?  HTTPContentType   = null,
                                                                      HTTPStatusCode?   HTTPStatusCode    = null)

            => httpServer.GetErrorHandler(
                              Host,
                              URL,
                              HTTPMethod,
                              HTTPContentType,
                              HTTPStatusCode
                          );

        #endregion


        #region Start(EventTrackingId = null)

        public Task<Boolean> Start(EventTracking_Id? EventTrackingId = null)

            => httpServer.Start(EventTrackingId);

        #endregion

        #region Start(Delay, EventTrackingId = null, InBackground = true)

        public Task<Boolean> Start(TimeSpan           Delay,
                                   EventTracking_Id?  EventTrackingId   = null,
                                   Boolean            InBackground      = true)

            => httpServer.Start(
                   Delay,
                   EventTrackingId,
                   InBackground
               );

        #endregion

        #region Shutdown(EventTrackingId = null, Message = null, Wait = true)

        public Task<Boolean> Shutdown(EventTracking_Id?  EventTrackingId   = null,
                                      String?            Message           = null,
                                      Boolean            Wait              = true)

            => httpServer.Shutdown(
                   EventTrackingId,
                   Message,
                   Wait
               );

        #endregion


        #region Dispose()

        public void Dispose()
        {
            httpServer.Dispose();
        }

        #endregion


    }


    /// <summary>
    /// An HTTP/1.1 server.
    /// </summary>
    public class HTTPServer : ATCPServers,
                              IHTTPServer,
                              IArrowReceiver<TCPConnection>
    {

        public class HTTPRequestHandle(HTTPAPI                                    HTTPAPI,
                                       HTTPDelegate?                              RequestHandler,
                                       HTTPRequestLogHandler?                     HTTPRequestLogger,
                                       HTTPResponseLogHandler?                    HTTPResponseLogger,
                                       HTTPDelegate?                              DefaultErrorHandler,
                                       Dictionary<HTTPStatusCode, HTTPDelegate>?  ErrorHandlers)
        {

            #region Properties

            public HTTPAPI                                    HTTPAPI                { get; } = HTTPAPI;
            public HTTPDelegate?                              RequestHandler         { get; } = RequestHandler;
            public HTTPRequestLogHandler?                     HTTPRequestLogger      { get; } = HTTPRequestLogger;
            public HTTPResponseLogHandler?                    HTTPResponseLogger     { get; } = HTTPResponseLogger;
            public HTTPDelegate?                              DefaultErrorHandler    { get; } = DefaultErrorHandler;
            public Dictionary<HTTPStatusCode, HTTPDelegate>?  ErrorHandlers          { get; } = ErrorHandlers;

            #endregion

            public static HTTPRequestHandle FromURLNode(URL_Node URLNode)

                => new (URLNode.HTTPAPI,
                        URLNode.RequestHandler,
                        URLNode.HTTPRequestLogger,
                        URLNode.HTTPResponseLogger,
                        URLNode.DefaultErrorHandler,
                        URLNode.ErrorHandlers);

            public static HTTPRequestHandle FromMethodNode(HTTPMethodNode MethodNode)

                => new (MethodNode.HTTPAPI,
                        MethodNode.RequestHandler,
                        MethodNode.HTTPRequestLogger,
                        MethodNode.HTTPResponseLogger,
                        MethodNode.DefaultErrorHandler,
                        MethodNode.ErrorHandlers);

            public static HTTPRequestHandle FromContentTypeNode(ContentTypeNode ContentTypeNode)

                => new (ContentTypeNode.HTTPAPI,
                        ContentTypeNode.RequestHandler,
                        ContentTypeNode.HTTPRequestLogger,
                        ContentTypeNode.HTTPResponseLogger,
                        ContentTypeNode.DefaultErrorHandler,
                        ContentTypeNode.ErrorHandlers);

        }


        #region Data

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public  const           String                                                      DefaultHTTPServerName    = "GraphDefined Hermod HTTP Server v1.0";

        /// <summary>
        /// The default HTTP service name.
        /// </summary>
        public  const           String                                                      DefaultHTTPServiceName   = "GraphDefined Hermod HTTP Service v1.0";

        /// <summary>
        /// The default HTTP server TCP port.
        /// </summary>
        public static readonly  IPPort                                                      DefaultHTTPServerPort    = IPPort.HTTP;

        private const           UInt32                                                      ReadTimeout              = 180000U;

        private readonly        ConcurrentDictionary<HTTPHostname,       HostnameNode>      hostnameNodes;
        private readonly        ConcurrentDictionary<HTTPEventSource_Id, IHTTPEventSource>  eventSources;

        #endregion

        #region Properties

        /// <summary>
        /// The default HTTP server name, used whenever
        /// no HTTP Host-header had been given.
        /// </summary>
        public String                                                         DefaultServerName             { get; }

        /// <summary>
        /// An associated HTTP security object.
        /// </summary>
        public HTTPSecurity?                                                  HTTPSecurity                  { get; }


        /// <summary>
        /// The optional delegate to verify the TLS client certificate used for authentication.
        /// </summary>
        public new RemoteTLSClientCertificateValidationHandler<IHTTPServer>?  ClientCertificateValidator    { get; }


        public RequestLogHandler?                                             RequestLogger                 { get; }

        public AccessLogHandler?                                              ResponseLogger                { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event called whenever a HTTP request came in.
        /// </summary>
        public HTTPRequestLogEvent   RequestLog    = new ();

        /// <summary>
        /// An event called whenever a HTTP request could successfully be processed.
        /// </summary>
        public HTTPResponseLogEvent  ResponseLog   = new ();

        /// <summary>
        /// An event called whenever a HTTP request resulted in an error.
        /// </summary>
        public HTTPErrorLogEvent     ErrorLog      = new ();

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Initialize the HTTP server using the given parameters.
        /// </summary>
        /// <param name="HTTPPort">The TCP port to listen on.</param>
        /// <param name="DefaultServerName">The default HTTP server name, used whenever no HTTP Host-header has been given.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// 
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="AutoStart">Start the HTTP server thread immediately (default: no).</param>
        public HTTPServer(IPPort?                                                    HTTPPort                     = null,
                          String?                                                    DefaultServerName            = null,
                          String?                                                    ServiceName                  = null,

                          ServerCertificateSelectorDelegate?                         ServerCertificateSelector    = null,
                          RemoteTLSClientCertificateValidationHandler<IHTTPServer>?  ClientCertificateValidator   = null,
                          LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                          SslProtocols?                                              AllowedTLSProtocols          = null,
                          Boolean?                                                   ClientCertificateRequired    = null,
                          Boolean?                                                   CheckCertificateRevocation   = null,

                          ServerThreadNameCreatorDelegate?                           ServerThreadNameCreator      = null,
                          ServerThreadPriorityDelegate?                              ServerThreadPrioritySetter   = null,
                          Boolean?                                                   ServerThreadIsBackground     = null,
                          ConnectionIdBuilder?                                       ConnectionIdBuilder          = null,
                          TimeSpan?                                                  ConnectionTimeout            = null,
                          UInt32?                                                    MaxClientConnections         = null,

                          DNSClient?                                                 DNSClient                    = null,
                          Boolean                                                    AutoStart                    = false)

            : base(ServiceName                  ?? DefaultHTTPServiceName,
                   DefaultServerName            ?? DefaultHTTPServerName,

                   ServerCertificateSelector,
                   null,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServerThreadNameCreator      ?? (ServerCertificateSelector is null
                                                        ? socket => "HTTP Server :"  + (HTTPPort ?? (ServerCertificateSelector is null ? IPPort.HTTP : IPPort.HTTPS))
                                                        : socket => "HTTPS Server :" + (HTTPPort ?? (ServerCertificateSelector is null ? IPPort.HTTP : IPPort.HTTPS))),
                   ServerThreadPrioritySetter,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionTimeout,
                   MaxClientConnections,

                   DNSClient,
                   false)

        {

            this.DefaultServerName  = DefaultServerName ?? DefaultHTTPServerName;
            this.hostnameNodes      = new ConcurrentDictionary<HTTPHostname,       HostnameNode>();
            this.eventSources       = new ConcurrentDictionary<HTTPEventSource_Id, IHTTPEventSource>();

            this.ClientCertificateValidator  = ClientCertificateValidator;
            //base.ClientCertificateValidator  = (sender,
            //                                    certificate,
            //                                    certificateChain,
            //                                    tlsServer,
            //                                    policyErrors) => DoClientCertificateValidator(
            //                                                         sender,
            //                                                         certificate,
            //                                                         certificateChain,
            //                                                         policyErrors
            //                                                     );

            AttachTCPPort(HTTPPort ?? (ServerCertificateSelector is null
                                           ? IPPort.HTTP
                                           : IPPort.HTTPS));

            if (AutoStart)
                Start(EventTracking_Id.New).Wait();

        }

        #endregion


        #region Manage the underlying TCP sockets...

        #region AttachTCPPort   (TCPPort)

        public Task<IHTTPServer> AttachTCPPort(IPPort TCPPort)

            => AttachTCPPorts(TCPPort);

        #endregion

        #region AttachTCPPorts  (params TCPPorts)

        public async Task<IHTTPServer> AttachTCPPorts(params IPPort[] TCPPorts)
        {

            await AttachTCPPorts(tcpServer => tcpServer.SendTo(this), TCPPorts);

            return this;

        }

        #endregion

        #region AttachTCPSocket (Socket)

        public Task<IHTTPServer> AttachTCPSocket(IPSocket Socket)

            => AttachTCPSockets(Socket);

        #endregion

        #region AttachTCPSockets(params Sockets)

        public async Task<IHTTPServer> AttachTCPSockets(params IPSocket[] Sockets)
        {

            await AttachTCPSockets(
                      tcpServer => tcpServer.SendTo(this),
                      Sockets
                  );

            return this;

        }

        #endregion


        #region DetachTCPPort (TCPPort)

        public Task<IHTTPServer> DetachTCPPort(IPPort TCPPort)

            => DetachTCPPorts(TCPPort);

        #endregion

        #region DetachTCPPorts(params Sockets)

        public async Task<IHTTPServer> DetachTCPPorts(params IPPort[] Ports)
        {

            await DetachTCPPorts(
                      tcpServer => {
                          tcpServer.OnNotification     -= ProcessArrow;
                          tcpServer.OnExceptionOccurred -= ProcessExceptionOccurred;
                          tcpServer.OnCompleted        -= ProcessCompleted;
                      },
                      Ports
                  );

            return this;

        }

        #endregion

        #endregion


        #region NotifyErrors(...)

        private void NotifyErrors(HTTPRequest     HTTPRequest,
                                  TCPConnection   TCPConnection,
                                  DateTime        Timestamp,
                                  HTTPStatusCode  HTTPStatusCode,
                                  //HTTPRequest?    Request           = null,
                                  HTTPResponse?   Response          = null,
                                  String?         Error             = null,
                                  Exception?      LastException     = null,
                                  Boolean         CloseConnection   = true)
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

            if (Error is not null)
                Content += Error + Environment.NewLine;

            if (LastException is not null)
                Content += LastException.Message + Environment.NewLine;

            var httpStatusCode = HTTPStatusCode;

            if (Content == "Invalid HTTP header!\r\nHTTP version not supported!\r\n")
                httpStatusCode = HTTPStatusCode.HTTPVersionNotSupported;

            var httpResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = httpStatusCode,
                                   Date            = Timestamp,
                                   Content         = Content.ToUTF8Bytes()
                               };

            TCPConnection.WriteLineToResponseStream(httpResponse.AsImmutable.EntirePDU);

            if (CloseConnection)
                TCPConnection.Close();

            #endregion

        }

        #endregion

        #region ProcessArrow(EventTracking, TCPConnection)

        public void ProcessArrow(EventTracking_Id  EventTracking,
                                 TCPConnection     TCPConnection)
        {

            if (TCPConnection is null)
                return;

            //lock (myLock)
            //{

            #region Start

            TCPConnection.NoDelay  = true;

            Byte Byte;
            var MemoryStream       = new MemoryStream();
            var EndOfHTTPHeader    = EOLSearch.NotYet;
            var ClientClose        = false;
            var ServerClose        = false;

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

                                    var RequestTimestamp = Timestamp.Now;

                                    #region Check UTF8 encoding

                                    var HTTPHeaderString = String.Empty;

                                    try
                                    {

                                        HTTPHeaderString = Encoding.UTF8.GetString(MemoryStream.ToArray());

                                    }
                                    catch
                                    {

                                        NotifyErrors(null,
                                                     TCPConnection,
                                                     RequestTimestamp,
                                                     HTTPStatusCode.BadRequest,
                                                     Error: "Protocol Error: Invalid UTF8 encoding!");

                                    }

                                    #endregion

                                    #region Try to parse the HTTP header

                                    HTTPRequest? HttpRequest = null;
                                    var CTS = new CancellationTokenSource();

                                    try
                                    {

                                        HttpRequest = new HTTPRequest(Timestamp:          RequestTimestamp,
                                                                      HTTPSource:         new HTTPSource(TCPConnection.RemoteSocket),
                                                                      LocalSocket:        TCPConnection.LocalSocket,
                                                                      RemoteSocket:       TCPConnection.RemoteSocket,
                                                                      HTTPServer:         this,
                                                                      ServerCertificate:  TCPConnection.ServerCertificate,
                                                                      ClientCertificate:  TCPConnection.ClientCertificate,

                                                                      HTTPHeader:         HTTPHeaderString.Trim(),
                                                                      HTTPBody:           null,
                                                                      HTTPBodyStream:     TCPConnection.SSLStream is not null
                                                                                              ? (Stream) TCPConnection.SSLStream
                                                                                              : (Stream) TCPConnection.NetworkStream,

                                                                      CancellationToken:  CTS.Token,
                                                                      EventTrackingId:    EventTracking_Id.New);

                                    }
                                    catch (Exception e)
                                    {

                                        DebugX.Log("Exception in " + nameof(HTTPServer) + " while trying to parse the HTTP header: " + e.Message);

                                        NotifyErrors(null,
                                                     TCPConnection,
                                                     RequestTimestamp,
                                                     HTTPStatusCode.BadRequest,
                                                     LastException:  e,
                                                     Error:          "Invalid HTTP header!");

                                    }

                                    #endregion

                                    if (HttpRequest is not null)
                                    {

                                        #region Call RequestLog delegate

                                        try
                                        {

                                            RequestLog?.WhenAll(RequestTimestamp,
                                                                this as Object as HTTPAPI,
                                                                HttpRequest);

                                        }
                                        catch (Exception e)
                                        {
                                            DebugX.LogT(nameof(HTTPServer) + " request log => " + e.Message);
                                        }

                                        #endregion

                                        #region Invoke HTTP handler

                                        HTTPResponse? httpResponse = default;

                                        // ToDo: How to read request body by application code?!
                                        //_HTTPResponse = OnNotification("TCPConnectionId",
                                        //                               RequestTimestamp,
                                        //                               HttpRequest).Result;

                                        try
                                        {

                                            httpResponse = InvokeRequestHandle(HttpRequest).Result;

                                            if (httpResponse is null)
                                            {

                                                DebugX.Log(nameof(HTTPServer) + ": HTTP response is null!");

                                                httpResponse = new HTTPResponse.Builder(HttpRequest) {
                                                                    HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                                                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                                                                    Content         = new JObject(
                                                                                          new JProperty("description", "HTTP response is null!")
                                                                                      ).ToUTF8Bytes(),
                                                                    CacheControl    = "private",
                                                                    Connection      = ConnectionType.Close
                                                                };

                                                ServerClose = true;

                                            }

                                        }
                                        catch (Exception e)
                                        {

                                            DebugX.Log(nameof(HTTPServer) + " while invoking request: " + Environment.NewLine + e);

                                            var exception = e.InnerException ?? e;

                                            httpResponse = new HTTPResponse.Builder(HttpRequest) {
                                                                HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                                                ContentType     = HTTPContentType.Application.JSON_UTF8,
                                                                Content         = new JObject(
                                                                                      new JProperty("exception",   exception.Message),
                                                                                      new JProperty("stacktrace",  exception.StackTrace)
                                                                                  ).ToUTF8Bytes(),
                                                                CacheControl    = "private",
                                                                Connection      = ConnectionType.Close
                                                            };

                                            ServerClose = true;

                                        }

                                        try
                                        {

                                            TCPConnection.WriteToResponseStream((httpResponse.RawHTTPHeader.Trim() +
                                                                                "\r\n\r\n").
                                                                                ToUTF8Bytes());

                                        }
                                        catch (Exception e)
                                        {

                                            if (TCPConnection is null)
                                                DebugX.Log(nameof(HTTPServer) + " TCPConnection is null!");

                                            if (httpResponse.RawHTTPHeader.IsNullOrEmpty())
                                                DebugX.Log(nameof(HTTPServer) + " HTTP response header is null or empty!");

                                            DebugX.Log(nameof(HTTPServer) + " writing response header: " + Environment.NewLine + e);
                                            ServerClose = true;

                                        }

                                        if (httpResponse.HTTPBody?.Length > 0)
                                        {
                                            try
                                            {
                                                TCPConnection.WriteToResponseStream(httpResponse.HTTPBody);
                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(nameof(HTTPServer) + " writing response body: " + Environment.NewLine + e);
                                                ServerClose = true;
                                            }
                                        }

                                        else if (httpResponse.HTTPBodyStream is not null)
                                        {
                                            try
                                            {
                                                TCPConnection.WriteToResponseStream(httpResponse.HTTPBodyStream);
                                                httpResponse.HTTPBodyStream.Close();
                                                httpResponse.HTTPBodyStream.Dispose();
                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.Log(nameof(HTTPServer) + " writing response body stream: " + Environment.NewLine + e);
                                                ServerClose = true;
                                            }
                                        }

                                        try
                                        {

                                            ServerClose = httpResponse.Connection is not null &&
                                                          httpResponse.Connection == ConnectionType.Close;

                                        }
                                        catch (Exception e)
                                        {
                                            DebugX.Log(nameof(HTTPServer) + " closing the connection: " + Environment.NewLine + e);
                                            ServerClose = true;
                                        }

                                        #endregion

                                        #region Call ResponseLog delegate

                                        try
                                        {

                                            ResponseLog?.WhenAll(RequestTimestamp,
                                                                 this as Object as HTTPAPI,
                                                                 HttpRequest,
                                                                 httpResponse);

                                        }
                                        catch (Exception e)
                                        {
                                            DebugX.LogT(nameof(HTTPServer) + " response log => " + e.Message);
                                        }

                                        #endregion

                                        #region if HTTP Status Code == 4xx | 5xx => Call ErrorLog delegate

                                        if (HttpRequest  is not null &&
                                            httpResponse is not null &&
                                            httpResponse.HTTPStatusCode.Code >  400 &&
                                            httpResponse.HTTPStatusCode.Code <= 599)
                                        {

                                            try
                                            {

                                                ErrorLog?.WhenAll(RequestTimestamp,
                                                                  this as Object as HTTPAPI,
                                                                  HttpRequest,
                                                                  httpResponse,
                                                                  httpResponse.HTTPStatusCode.ToString());

                                            }
                                            catch (Exception e)
                                            {
                                                DebugX.LogT(nameof(HTTPServer) + " => " + e.Message);
                                            }

                                        }

                                        #endregion

                                    }

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
                            DebugX.LogT("Server close!");
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

                if      (ioe.Message.StartsWith("Unable to read data from the transport connection")) {
                }

                else if (ioe.Message.StartsWith("Unable to write data to the transport connection")) {
                }

                else
                {

                    DebugX.Log("HTTPServer IO exception: " + Environment.NewLine + ioe);

                    //if (OnError != null)
                    //    OnError(this, Timestamp.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), ioe, MemoryStream);

                }

            }

            catch (Exception e)
            {

                DebugX.Log("HTTPServer exception: " + Environment.NewLine + e);

                //if (OnError != null)
                //    OnError(this, Timestamp.Now, ConnectionIdBuilder(newTCPConnection.RemoteIPAddress, newTCPConnection.RemotePort), e, MemoryStream);

            }

            #endregion

            #region Close the TCP connection

            try
            {

                TCPConnection?.Close(ClientClose
                                         ? ConnectionClosedBy.Client
                                         : ConnectionClosedBy.Server);

            }
            catch (Exception e)
            {
                DebugX.Log("HTTPServer exception when closing the TCP connection: " + e);
            }

            #endregion

            //}

        }

        #endregion

        #region ProcessExceptionOccurred(Sender, Timestamp, EventTracking, ExceptionMessage)

        public void ProcessExceptionOccurred(Object            Sender,
                                            DateTime          Timestamp,
                                            EventTracking_Id  EventTracking,
                                            Exception         ExceptionMessage)
        {

            //var OnExceptionOccurredLocal = OnExceptionOccurred;
            //if (OnExceptionOccurredLocal != null)
            //    OnExceptionOccurredLocal(Sender,
            //                            Timestamp,
            //                            ExceptionMessage);

        }

        #endregion

        #region ProcessCompleted(Sender, Timestamp, EventTracking, Message = null)

        public void ProcessCompleted(Object            Sender,
                                     DateTime          Timestamp,
                                     EventTracking_Id  EventTracking,
                                     String?           Message   = null)
        {

            //var OnCompletedLocal = OnCompleted;
            //if (OnCompletedLocal != null)
            //    OnCompletedLocal(Sender,
            //                     Timestamp,
            //                     Message);

        }

        #endregion


        #region Method Callbacks

        #region HTTP Auth

        private readonly List<HTTPAuth2Delegate> httpAuths = [];

        public void AddAuth(HTTPAuth1Delegate Filter)
        {
            httpAuths.Add((server, request) => Filter(request));
        }

        public void AddAuth(HTTPAuth2Delegate Filter)
        {
            httpAuths.Add(Filter);
        }

        #endregion

        #region HTTP Filters

        private readonly List<HTTPFilter2Delegate> httpFilters = [];

        public void AddFilter(HTTPFilter1Delegate Filter)
        {
            httpFilters.Add((server, request) => Filter(request));
        }

        public void AddFilter(HTTPFilter2Delegate Filter)
        {
            httpFilters.Add(Filter);
        }

        public void ClearFilters(HTTPFilter2Delegate? Filter = null)
        {

            if (Filter is null)
                httpFilters.Clear();

            else
                httpFilters.RemoveAll(filter => filter == Filter);

        }

        #endregion

        #region HTTP Rewrites

        private readonly List<HTTPRewrite2Delegate> httpRewrites = [];

        public void Rewrite(HTTPRewrite1Delegate Rewrite)
        {
            httpRewrites.Add((server, request) => Rewrite(request));
        }

        public void Rewrite(HTTPRewrite2Delegate Rewrite)
        {
            httpRewrites.Add(Rewrite);
        }

        #endregion


        #region Redirect(Hostname, HTTPMethod, URLTemplate, HTTPContentType, URLTarget)

        /// <summary>
        /// Add a URL based method redirect for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URLTarget">The target URL of the redirect.</param>
        public void Redirect(HTTPAPI          HTTPAPI,
                             HTTPHostname     Hostname,
                             HTTPMethod       HTTPMethod,
                             HTTPPath         URLTemplate,
                             HTTPContentType  HTTPContentType,
                             HTTPPath         URLTarget)

        {

            AddHandler(
                HTTPAPI,
                req => InvokeRequestHandle(new HTTPRequest.Builder(req).SetURL(URLTarget)),
                Hostname,
                URLTemplate,
                HTTPMethod,
                HTTPContentType ?? HTTPContentType.Text.HTML_UTF8,
                false,
                null,
                null,
                null,
                null
            );

        }

        #endregion

        #region Redirect(HTTPMethod, URLTemplate, HTTPContentType, URLTarget)

        /// <summary>
        /// Add a URL based method redirect for the given URL template.
        /// </summary>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="URLTarget">The target URL of the redirect.</param>
        public void Redirect(HTTPAPI          HTTPAPI,
                             HTTPMethod       HTTPMethod,
                             HTTPPath         URLTemplate,
                             HTTPContentType  HTTPContentType,
                             HTTPPath         URLTarget)

        {

            AddHandler(
                HTTPAPI,
                req => InvokeRequestHandle(new HTTPRequest.Builder(req).SetURL(URLTarget)),
                HTTPHostname.Any,
                URLTemplate,
                HTTPMethod,
                HTTPContentType ?? HTTPContentType.Text.HTML_UTF8,
                false,
                null,
                null,
                null,
                null
            );

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
        internal void AddHandler(HTTPAPI                  HTTPAPI,
                                 HTTPDelegate             HTTPDelegate,

                                 HTTPHostname?            Hostname                    = null,
                                 HTTPPath?                URLTemplate                 = null,
                                 HTTPMethod?              HTTPMethod                  = null,
                                 HTTPContentType?         HTTPContentType             = null,
                                 Boolean                  OpenEnd                     = false,

                                 HTTPAuthentication?      URLAuthentication           = null,
                                 HTTPAuthentication?      HTTPMethodAuthentication    = null,
                                 HTTPAuthentication?      ContentTypeAuthentication   = null,

                                 HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                                 HTTPResponseLogHandler?  HTTPResponseLogger          = null,

                                 HTTPDelegate?            DefaultErrorHandler         = null,
                                 URLReplacement           AllowReplacement            = URLReplacement.Fail)

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
                                                 new HostnameNode(
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
        public void AddMethodCallback(HTTPHostname             Hostname,
                                      HTTPMethod               HTTPMethod,
                                      HTTPPath                 URLTemplate,
                                      HTTPContentType?         HTTPContentType             = null,
                                      Boolean                  OpenEnd                     = false,
                                      HTTPAuthentication?      URLAuthentication           = null,
                                      HTTPAuthentication?      HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?      ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?  HTTPResponseLogger          = null,
                                      HTTPDelegate?            DefaultErrorHandler         = null,
                                      HTTPDelegate?            HTTPDelegate                = null,
                                      URLReplacement           AllowReplacement            = URLReplacement.Fail)

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
        public void AddMethodCallback(HTTPAPI                  HTTPAPI,
                                      HTTPHostname             Hostname,
                                      HTTPMethod               HTTPMethod,
                                      HTTPPath                 URLTemplate,
                                      HTTPContentType?         HTTPContentType             = null,
                                      Boolean                  OpenEnd                     = false,
                                      HTTPAuthentication?      URLAuthentication           = null,
                                      HTTPAuthentication?      HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?      ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?  HTTPResponseLogger          = null,
                                      HTTPDelegate?            DefaultErrorHandler         = null,
                                      HTTPDelegate?            HTTPDelegate                = null,
                                      URLReplacement           AllowReplacement            = URLReplacement.Fail)

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

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplates, HTTPContentType = null, ..., HTTPDelegate = null, AllowReplacement = URLReplacement.Fail)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplates">An enumeration of URL templates.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// <param name="HostAuthentication">Whether this method needs explicit host authentication or not.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPAPI                  HTTPAPI,
                                      HTTPHostname             Hostname,
                                      HTTPMethod               HTTPMethod,
                                      IEnumerable<HTTPPath>    URLTemplates,
                                      HTTPContentType?         HTTPContentType             = null,
                                      Boolean                  OpenEnd                     = false,
                                      HTTPAuthentication?      URLAuthentication           = null,
                                      HTTPAuthentication?      HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?      ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?  HTTPResponseLogger          = null,
                                      HTTPDelegate?            DefaultErrorHandler         = null,
                                      HTTPDelegate?            HTTPDelegate                = null,
                                      URLReplacement           AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial checks

            if (!URLTemplates.SafeAny())
                throw new ArgumentNullException(nameof(URLTemplates),  "The given URL template must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),  "The given HTTP delegate must not be null!");

            #endregion

            URLTemplates.ForEach(URLTemplate =>
                AddHandler(HTTPAPI,
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
                           AllowReplacement));

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate,  HTTPContentTypes, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPAPI                       HTTPAPI,
                                      HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      HTTPPath                      URLTemplate,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      Boolean                       OpenEnd                     = false,
                                      HTTPAuthentication?           URLAuthentication           = null,
                                      HTTPAuthentication?           HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?           ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?        HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?       HTTPResponseLogger          = null,
                                      HTTPDelegate?                 DefaultErrorHandler         = null,
                                      HTTPDelegate?                 HTTPDelegate                = null,
                                      URLReplacement                AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial checks

            if (URLTemplate.IsNullOrEmpty)
                throw new ArgumentNullException(nameof(URLTemplate),       "The given URL template must not be null or empty!");

            if (!HTTPContentTypes.Any())
                throw new ArgumentNullException(nameof(HTTPContentTypes),  "The given content types must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),      "The given HTTP delegate must not be null!");

            #endregion

            foreach (var contenttype in HTTPContentTypes)
                AddHandler(HTTPAPI,
                           HTTPDelegate,
                           Hostname,
                           URLTemplate,
                           HTTPMethod,
                           contenttype,
                           OpenEnd,
                           URLAuthentication,
                           HTTPMethodAuthentication,
                           ContentTypeAuthentication,
                           HTTPRequestLogger,
                           HTTPResponseLogger,
                           DefaultErrorHandler,
                           AllowReplacement);

        }

        #endregion

        #region AddMethodCallback(Hostname, HTTPMethod, URLTemplate,  HTTPContentTypes, HostAuthentication = false, URLAuthentication = false, HTTPMethodAuthentication = false, ContentTypeAuthentication = false, HTTPDelegate = null)

        /// <summary>
        /// Add a method callback for the given URL template.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="HTTPMethod">The HTTP method.</param>
        /// <param name="URLTemplates">An enumeration of URL templates.</param>
        /// <param name="HTTPContentTypes">An enumeration of HTTP content types.</param>
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// <param name="ContentTypeAuthentication">Whether this method needs explicit HTTP content type authentication or not.</param>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        /// <param name="HTTPDelegate">The method to call.</param>
        public void AddMethodCallback(HTTPAPI                       HTTPAPI,
                                      HTTPHostname                  Hostname,
                                      HTTPMethod                    HTTPMethod,
                                      IEnumerable<HTTPPath>         URLTemplates,
                                      IEnumerable<HTTPContentType>  HTTPContentTypes,
                                      Boolean                       OpenEnd                     = false,
                                      HTTPAuthentication?           URLAuthentication           = null,
                                      HTTPAuthentication?           HTTPMethodAuthentication    = null,
                                      HTTPAuthentication?           ContentTypeAuthentication   = null,
                                      HTTPRequestLogHandler?        HTTPRequestLogger           = null,
                                      HTTPResponseLogHandler?       HTTPResponseLogger          = null,
                                      HTTPDelegate?                 DefaultErrorHandler         = null,
                                      HTTPDelegate?                 HTTPDelegate                = null,
                                      URLReplacement                AllowReplacement            = URLReplacement.Fail)

        {

            #region Initial checks

            if (!URLTemplates.Any())
                throw new ArgumentNullException(nameof(URLTemplates),      "The given URL template must not be null or empty!");

            if (!HTTPContentTypes.Any())
                throw new ArgumentNullException(nameof(HTTPContentTypes),  "The given content types must not be null or empty!");

            if (HTTPDelegate is null)
                throw new ArgumentNullException(nameof(HTTPDelegate),      "The given HTTP delegate must not be null!");

            #endregion

            foreach (var uritemplate in URLTemplates)
                foreach (var contenttype in HTTPContentTypes)
                    AddHandler(HTTPAPI,
                               HTTPDelegate,
                               Hostname,
                               uritemplate,
                               HTTPMethod,
                               contenttype,
                               OpenEnd,
                               URLAuthentication,
                               HTTPMethodAuthentication,
                               ContentTypeAuthentication,
                               HTTPRequestLogger,
                               HTTPResponseLogger,
                               DefaultErrorHandler,
                               AllowReplacement);

        }

        #endregion

        #region (internal) ReplaceHandler(HTTPDelegate, Hostname = "*", URLTemplate = "/", HTTPMethod = null, HTTPContentType = null, HostAuthentication = null, URLAuthentication = null, HTTPMethodAuthentication = null, ContentTypeAuthentication = null, DefaultErrorHandler = null)

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
        internal void ReplaceHandler(HTTPAPI                  HTTPAPI,
                                     HTTPDelegate             HTTPDelegate,

                                     HTTPHostname?            Hostname                    = null,
                                     HTTPPath?                URLTemplate                 = null,
                                     Boolean                  OpenEnd                     = false,
                                     HTTPMethod?              HTTPMethod                  = null,
                                     HTTPContentType?         HTTPContentType             = null,

                                     HTTPAuthentication?      URLAuthentication           = null,
                                     HTTPAuthentication?      HTTPMethodAuthentication    = null,
                                     HTTPAuthentication?      ContentTypeAuthentication   = null,

                                     HTTPRequestLogHandler?   HTTPRequestLogger           = null,
                                     HTTPResponseLogHandler?  HTTPResponseLogger          = null,

                                     HTTPDelegate?            DefaultErrorHandler         = null)

        {

            lock (hostnameNodes)
            {

                #region Initial Checks

                if (HTTPDelegate is null)
                    throw new ArgumentNullException(nameof(HTTPDelegate), "The given parameter must not be null!");

                var hostname = Hostname ?? HTTPHostname.Any;

                if (HTTPMethod is not null && HTTPContentType is not null)
                    throw new ArgumentException("If HTTP method is null the HTTP content type must also be null!");

                #endregion

                if (!hostnameNodes.TryGetValue(hostname, out var hostnameNode))
                    hostnameNode = hostnameNodes.AddAndReturnValue(hostname, new HostnameNode(HTTPAPI, hostname));

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

                    DefaultErrorHandler
                );

            }

        }

        #endregion


        #region (internal) GetRequestHandle(Request)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        internal HTTPRequestHandle? GetRequestHandle(HTTPRequest Request,
                                                     out String? ErrorResponse)

            => GetRequestHandle(
                   Request.Host,
                   Request.Path.IsNullOrEmpty ? HTTPPath.Parse("/") : Request.Path,
                   out ErrorResponse,
                   Request.HTTPMethod,
                   AvailableContentTypes => Request.Accept.BestMatchingContentType(AvailableContentTypes),// ?? AvailableContentTypes.First(),
                   ParsedURLParameters   => Request.ParsedURLParameters = ParsedURLParameters.ToArray()
               );

        #endregion

        #region (internal) GetRequestHandle(Host = "*", Path = "/", ErrorResponse, HTTPMethod = HTTPMethod.GET, HTTPContentTypeSelector = null)

        /// <summary>
        /// Return the best matching method handler for the given parameters.
        /// </summary>
        internal HTTPRequestHandle? GetRequestHandle(HTTPHostname                               Host,
                                                     HTTPPath                                   Path,
                                                     out String?                                ErrorResponse,
                                                     HTTPMethod?                                HTTPMethod                    = null,
                                                     Func<HTTPContentType[], HTTPContentType>?  HTTPContentTypeSelector       = null,
                                                     Action<IEnumerable<String>>?               ParsedURLParametersDelegate   = null)
        {

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
                ErrorResponse = "Could not find a matching hostnode!";
                return null;
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

                return null;

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
                        return HTTPRequestHandle.FromMethodNode(httpMethodNode);

                    // A single content type is defined...
                //    else if (_HTTPMethodNode.Count() == 1)
                        return HTTPRequestHandle.FromContentTypeNode(httpMethodNode.First());

                //    else
                //        throw new ArgumentException(String.Concat(URL, " ", _HTTPMethodNode, " but multiple content type choices!"));

                }

                // The requested content type was found...
                else if (httpMethodNode.TryGet(bestMatchingContentType, out var httpContentTypeNode) && httpContentTypeNode is not null)
                    return HTTPRequestHandle.FromContentTypeNode(httpContentTypeNode);

                else
                    return HTTPRequestHandle.FromMethodNode(httpMethodNode);

            }

            //}

            // No HTTPMethod was found => return best matching URL Handler
            return HTTPRequestHandle.FromURLNode(bestMatch.URLNode);

            //return GetErrorHandler(Host, URL, HTTPMethod, HTTPContentType, HTTPStatusCode.BadRequest);

        }

        #endregion


        #region InvokeRequestHandle(HTTPRequest)

        /// <summary>
        /// Call the best matching method handler for the given HTTP request.
        /// </summary>
        public async Task<HTTPResponse> InvokeRequestHandle(HTTPRequest Request)
        {

            #region Process HTTP auths...

            IUser? user = null;

            foreach (var httpAuth in httpAuths.ReverseAndReturn())
            {

                user = httpAuth(this, Request);

                if (user is not null)
                    break;

            }

            Request.User = user;

            #endregion

            #region Process HTTP filters...

            HTTPResponse? httpResponse = null;

            foreach (var httpFilter in httpFilters.ReverseAndReturn())
            {

                httpResponse = httpFilter(this, Request);

                if (httpResponse is not null)
                    return httpResponse;

            }

            #endregion

            #region Process HTTP rewrites...

            foreach (var httpRewrite in httpRewrites.ReverseAndReturn())
            {

                var newRequest = httpRewrite(this, Request);

                if (newRequest is not null)
                {
                    Request = newRequest;
                    break;
                }

            }

            #endregion


            #region HTTP request logger

            var now = Timestamp.Now;

            var requestLogger = RequestLogger;
            if (requestLogger is not null)
            {

                var requestLoggerTasks = requestLogger.GetInvocationList().
                                                       OfType<RequestLogHandler>().
                                                       Select(loggingDelegate => loggingDelegate.Invoke(
                                                                                     now,
                                                                                     this,
                                                                                     Request
                                                                                 )).
                                                       ToArray();

                try
                {
                    await Task.WhenAll(requestLoggerTasks);
                }
                catch (Exception e)
                {
                    //await HandleErrors(
                    //          nameof(TestChargingStation),
                    //          nameof(OnRequestStartTransactionRequest),
                    //          e
                    //      );
                    DebugX.LogT("HTTP server request logger exception: " + e.Message);
                }

            }

            #endregion


            var httpRequestHandle = GetRequestHandle(Request,
                                                     out var errorResponse);

            if (httpRequestHandle is not null)
            {

                #region URL specific HTTP request logger

                now = Timestamp.Now;

                var requestLoggerURL = httpRequestHandle.HTTPRequestLogger;
                if (requestLoggerURL is not null)
                {

                    var requestLoggerTasks = requestLoggerURL.GetInvocationList().
                                                              OfType <HTTPRequestLogHandler>().
                                                              Select (loggingDelegate => loggingDelegate.Invoke(
                                                                                             now,
                                                                                             httpRequestHandle.HTTPAPI,
                                                                                             Request
                                                                                         )).
                                                              ToArray();

                    try
                    {
                        await Task.WhenAll(requestLoggerTasks);
                    }
                    catch (Exception e)
                    {
                        //await HandleErrors(
                        //          nameof(TestChargingStation),
                        //          nameof(OnRequestStartTransactionRequest),
                        //          e
                        //      );
                        DebugX.LogT("HTTP server request logger exception: " + e.Message);
                    }

                }

                #endregion

                #region Process HTTP request

                var httpDelegate = httpRequestHandle.RequestHandler;
                if (httpDelegate is not null)
                {

                    try
                    {
                         httpResponse = await httpDelegate(Request);
                    }
                    catch (Exception e)
                    {

                        DebugX.LogT("HTTP server request processing exception: " + e.Message);

                        httpResponse = new HTTPResponse.Builder(Request) {
                                           HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                           Server          = DefaultServerName,
                                           ContentType     = HTTPContentType.Application.JSON_UTF8,
                                           Content         = JSONObject.Create(
                                                                 new JProperty("request",      Request.FirstPDULine),
                                                                 new JProperty("description",  e.Message),
                                                                 new JProperty("stacktrace",   e.StackTrace),
                                                                 new JProperty("source",       e.TargetSite?.Module.Name),
                                                                 new JProperty("type",         e.TargetSite?.ReflectedType?.Name)
                                                             ).ToUTF8Bytes(),
                                           Connection      = ConnectionType.Close
                                       };

                    }

                }

                httpResponse ??= new HTTPResponse.Builder(Request) {
                                     HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                     Server          = DefaultServerName,
                                     ContentType     = HTTPContentType.Application.JSON_UTF8,
                                     Content         = JSONObject.Create(
                                                             new JProperty("request",       Request.FirstPDULine),
                                                             new JProperty("description",  "HTTP request handler must not be null!")
                                                         ).ToUTF8Bytes(),
                                     Connection      = ConnectionType.Close
                                 };

                #endregion

                #region URL specific HTTP response logger

                now = Timestamp.Now;

                var responseLoggerURL = httpRequestHandle.HTTPResponseLogger;
                if (responseLoggerURL is not null)
                {

                    var responseLoggerTasks = responseLoggerURL.GetInvocationList().
                                                                OfType <HTTPResponseLogHandler>().
                                                                Select (loggingDelegate => loggingDelegate.Invoke(
                                                                                               now,
                                                                                               httpRequestHandle.HTTPAPI,
                                                                                               Request,
                                                                                               httpResponse
                                                                                           )).
                                                                ToArray();

                    try
                    {
                        await Task.WhenAll(responseLoggerTasks);
                    }
                    catch (Exception e)
                    {
                        //await HandleErrors(
                        //          nameof(TestChargingStation),
                        //          nameof(OnRequestStartTransactionRequest),
                        //          e
                        //      );
                        DebugX.LogT("HTTP server response logger exception: " + e.Message);
                    }

                }

                #endregion

            }


            if (errorResponse == "This HTTP method is not allowed!")
                httpResponse = new HTTPResponse.Builder(Request) {
                                   HTTPStatusCode  = HTTPStatusCode.MethodNotAllowed,
                                   Server          = Request.Host.ToString(),
                                   Date            = Timestamp.Now,
                                   ContentType     = HTTPContentType.Text.PLAIN,
                                   Content         = errorResponse.ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               };

             httpResponse ??= new HTTPResponse.Builder(Request) {
                                  HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                  Server          = DefaultServerName,
                                  ContentType     = HTTPContentType.Application.JSON_UTF8,
                                  Content         = JSONObject.Create(
                                                          new JProperty("request",      Request.FirstPDULine),
                                                          new JProperty("description",  errorResponse)
                                                      ).ToUTF8Bytes(),
                                  Connection      = ConnectionType.Close
                              };


            #region HTTP response logger

            now = Timestamp.Now;

            var responseLogger = ResponseLogger;
            if (responseLogger is not null)
            {

                var responseLoggerTasks = responseLogger.GetInvocationList().
                                                         OfType<AccessLogHandler>().
                                                         Select(loggingDelegate => loggingDelegate.Invoke(
                                                                                       now,
                                                                                       this,
                                                                                       Request,
                                                                                       httpResponse
                                                                                   )).
                                                         ToArray();

                try
                {
                    await Task.WhenAll(responseLoggerTasks);
                }
                catch (Exception e)
                {
                    //await HandleErrors(
                    //          nameof(TestChargingStation),
                    //          nameof(OnRequestStartTransactionRequest),
                    //          e
                    //      );
                    DebugX.LogT("HTTP server response logger exception: " + e.Message);
                }

            }

            #endregion

            return httpResponse;

        }

        #endregion

        #endregion

        #region HTTP Server Sent Events

        #region AddEventSource(HTTPAPI, EventIdentification, MaxNumberOfCachedEvents = 500, RetryInterval  = null, LogfileName = null)

        /// <summary>
        /// Add a HTTP Sever Sent Events source.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryInterval ">The retry interval.</param>
        /// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        /// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        /// <param name="EnableLogging">Whether to enable event logging.</param>
        /// <param name="LogfilePrefix">The prefix of the logfile names.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        public HTTPEventSource<T> AddEventSource<T>(HTTPEventSource_Id               EventIdentification,
                                                    HTTPAPI                          HTTPAPI,
                                                    UInt32                           MaxNumberOfCachedEvents      = 500,
                                                    TimeSpan?                        RetryInterval                = null,
                                                    Func<T, String>?                 DataSerializer               = null,
                                                    Func<String, T>?                 DataDeserializer             = null,
                                                    Boolean                          EnableLogging                = true,
                                                    String?                          LogfilePath                  = null,
                                                    String?                          LogfilePrefix                = null,
                                                    Func<String, DateTime, String>?  LogfileName                  = null,
                                                    String?                          LogfileReloadSearchPattern   = null)

        {

            if (eventSources.ContainsKey(EventIdentification))
                throw new ArgumentException("Duplicate event identification!");

            var eventSource = new HTTPEventSource<T>(
                                  EventIdentification,
                                  HTTPAPI,
                                  MaxNumberOfCachedEvents,
                                  RetryInterval ,
                                  DataSerializer,
                                  DataDeserializer,
                                  EnableLogging,
                                  LogfilePath,
                                  EnableLogging || LogfileName is not null
                                      ? LogfileName ?? ((eventid, time) => String.Concat(LogfilePrefix ?? "",
                                                                                         eventid, "_",
                                                                                         time.Year, "-", time.Month.ToString("D2"),
                                                                                         ".log"))
                                      : null,
                                  LogfileReloadSearchPattern ?? String.Concat(LogfilePrefix ?? "", EventIdentification, "_*.log")
                              );

            eventSources.TryAdd(EventIdentification,
                                eventSource);

            return eventSource;

        }

        #endregion

        #region AddEventSource(EventIdentification, URLTemplate, MaxNumberOfCachedEvents = 500, RetryInterval  = null, LogfileName = null, ...)

        /// <summary>
        /// Add a HTTP Sever Sent Events source and a method call back for the given URL template.
        /// </summary>
        /// <param name="EventIdentification">The unique identification of the event source.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// 
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="IncludeFilterAtRuntime">Include this events within the HTTP SSE output. Can e.g. be used to filter events by HTTP users.</param>
        /// <param name="RetryInterval ">The retry interval.</param>
        /// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        /// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        /// <param name="EnableLogging">Whether to enable event logging.</param>
        /// <param name="LogfilePrefix">The prefix of the logfile names.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        /// 
        /// <param name="Hostname">The HTTP host.</param>
        /// <param name="HttpMethod">The HTTP method.</param>
        /// <param name="HTTPContentType">The HTTP content type.</param>
        /// 
        /// <param name="URLAuthentication">Whether this method needs explicit uri authentication or not.</param>
        /// <param name="HTTPMethodAuthentication">Whether this method needs explicit HTTP method authentication or not.</param>
        /// 
        /// <param name="DefaultErrorHandler">The default error handler.</param>
        public HTTPEventSource<T> AddEventSource<T>(HTTPEventSource_Id               EventIdentification,
                                                    HTTPAPI                          HTTPAPI,
                                                    HTTPPath                         URLTemplate,

                                                    UInt32                           MaxNumberOfCachedEvents      = 500,
                                                    Func<HTTPEvent<T>, Boolean>?     IncludeFilterAtRuntime       = null,
                                                    TimeSpan?                        RetryInterval                = null,
                                                    Func<T, String>?                 DataSerializer               = null,
                                                    Func<String, T>?                 DataDeserializer             = null,
                                                    Boolean                          EnableLogging                = true,
                                                    String?                          LogfilePath                  = null,
                                                    String?                          LogfilePrefix                = null,
                                                    Func<String, DateTime, String>?  LogfileName                  = null,
                                                    String?                          LogfileReloadSearchPattern   = null,

                                                    HTTPHostname?                    Hostname                     = null,
                                                    HTTPMethod?                      HttpMethod                   = null,
                                                    HTTPContentType?                 HTTPContentType              = null,

                                                    Boolean                          RequireAuthentication        = true,
                                                    HTTPAuthentication?              URLAuthentication            = null,
                                                    HTTPAuthentication?              HTTPMethodAuthentication     = null,

                                                    HTTPDelegate?                    DefaultErrorHandler          = null)

        {

            var eventSource = AddEventSource(
                                  EventIdentification,
                                  HTTPAPI,
                                  MaxNumberOfCachedEvents,
                                  RetryInterval ,
                                  DataSerializer,
                                  DataDeserializer,
                                  EnableLogging,
                                  LogfilePath,
                                  LogfilePrefix,
                                  LogfileName,
                                  LogfileReloadSearchPattern
                              );

            IncludeFilterAtRuntime ??= httpEvent => true;

            AddHandler(HTTPAPI,
                       request => {

                           var httpEvents = eventSource.GetAllEventsGreater(request.GetHeaderField(HTTPRequestHeaderField.LastEventId)).
                                                        Where(IncludeFilterAtRuntime).
                                                        Aggregate(new StringBuilder(),
                                                                  (stringBuilder, httpEvent) => stringBuilder.Append    (httpEvent.SerializedHeader).
                                                                                                              AppendLine(httpEvent.SerializedData).
                                                                                                              AppendLine()).
                                                        Append(Environment.NewLine).
                                                        Append("retry: ").Append((UInt32) eventSource.RetryInterval .TotalMilliseconds).
                                                        Append(Environment.NewLine).
                                                        Append(Environment.NewLine).
                                                        ToString();


                           return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode            = HTTPStatusCode.OK,
                                   Server                    = HTTPServer.DefaultHTTPServerName,
                                   ContentType               = HTTPContentType.Text.EVENTSTREAM,
                                   CacheControl              = "no-cache",
                                   Connection                = ConnectionType.KeepAlive,
                                   AccessControlAllowOrigin  = "*",
                                   KeepAlive                 = new KeepAliveType(TimeSpan.FromSeconds(2 * eventSource.RetryInterval .TotalSeconds)),
                                   Content                   = httpEvents.ToUTF8Bytes()
                               }.AsImmutable);

                       },
                       Hostname,
                       URLTemplate,
                       HttpMethod      ?? HTTPMethod.GET,
                       HTTPContentType ?? HTTPContentType.Text.EVENTSTREAM,
                       false,

                       URLAuthentication,
                       HTTPMethodAuthentication,
                       null,

                       null,
                       null,

                       DefaultErrorHandler);

            return eventSource;

        }

        #endregion


        #region Get   (EventSourceIdentification)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public IHTTPEventSource? Get(HTTPEventSource_Id EventSourceIdentification)
        {

            if (eventSources.TryGetValue(EventSourceIdentification, out var httpEventSource))
                return httpEventSource;

            return null;

        }


        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        public IHTTPEventSource<TData>? Get<TData>(HTTPEventSource_Id EventSourceIdentification)
        {

            if (eventSources.TryGetValue(EventSourceIdentification, out var httpEventSource))
                return httpEventSource as IHTTPEventSource<TData>;

            return null;

        }

        #endregion

        #region TryGet(EventSourceIdentification, out EventSource)

        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        public Boolean TryGet(HTTPEventSource_Id EventSourceIdentification, out IHTTPEventSource? EventSource)

            => eventSources.TryGetValue(EventSourceIdentification, out EventSource);


        /// <summary>
        /// Return the event source identified by the given event source identification.
        /// </summary>
        /// <param name="EventSourceIdentification">A string to identify an event source.</param>
        /// <param name="EventSource">The event source.</param>
        public Boolean TryGet<TData>(HTTPEventSource_Id EventSourceIdentification, out IHTTPEventSource<TData>? EventSource)
        {

            if (eventSources.TryGetValue(EventSourceIdentification, out var eventSource) &&
                eventSource is IHTTPEventSource<TData> eventSourceTData)
            {
                EventSource = eventSourceTData;
                return true;
            }

            EventSource = null;
            return false;

        }

        #endregion

        #region EventSources(IncludeEventSource = null)

        /// <summary>
        /// Return a filtered enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        public IEnumerable<IHTTPEventSource> EventSources(Func<IHTTPEventSource, Boolean>? IncludeEventSource = null)

            => eventSources.Values.Where(IncludeEventSource ?? (eventSource => true));



        /// <summary>
        /// Return a filtered enumeration of all event sources.
        /// </summary>
        /// <param name="IncludeEventSource">An event source filter delegate.</param>
        public IEnumerable<IHTTPEventSource<TData>> EventSources<TData>(Func<IHTTPEventSource, Boolean>? IncludeEventSource = null)
        {

            var filteredEventSources = new List<IHTTPEventSource<TData>>();

            foreach (var eventSource in eventSources.Values.
                                            Where (IncludeEventSource ?? (eventSource => true)).
                                            Select(eventSource => eventSource as IHTTPEventSource<TData>))
            {

                if (eventSource is not null)
                    filteredEventSources.Add(eventSource);

            }

            return filteredEventSources;

        }

        #endregion

        #endregion

        #region HTTP Errors

        #region GetErrorHandler(Host, URL, HTTPMethod = null, HTTPContentType = null, HTTPStatusCode = null)

        /// <summary>
        /// Return the best matching error handler for the given parameters.
        /// </summary>
        public Tuple<MethodInfo, IEnumerable<Object>> GetErrorHandler(String            Host,
                                                                      String            URL,
                                                                      HTTPMethod?       HTTPMethod       = null,
                                                                      HTTPContentType?  HTTPContentType  = null,
                                                                      HTTPStatusCode?   HTTPStatusCode   = null)

        {

            lock (hostnameNodes)
            {
                return null;
            }

        }

        #endregion

        #endregion

    }

}
