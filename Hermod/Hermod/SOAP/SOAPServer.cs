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

using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    /// <summary>
    /// An HTTP/SOAP/XML server.
    /// </summary>
    public class SOAPServer
    {

        #region Data

        /// <summary>
        /// The default HTTP content type used for all SOAP requests/responses.
        /// </summary>
        public static readonly  HTTPContentType                       DefaultSOAPContentType   = HTTPContentType.Application.SOAPXML_UTF8;

        private readonly        Dictionary<HTTPPath, SOAPDispatcher>  soapDispatchers          = [];

        #endregion

        #region Properties

        /// <summary>
        /// The underlying HTTP server.
        /// </summary>
        public HTTPTestServerX  HTTPServer         { get; }

        /// <summary>
        /// The SOAP XML HTTP content type.
        /// </summary>
        public HTTPContentType  SOAPContentType    { get; }

        /// <summary>
        /// All registered SOAP dispatchers.
        /// </summary>
        public ILookup<HTTPPath, SOAPDispatcher> SOAPDispatchers

            => soapDispatchers.ToLookup(kvp => kvp.Key,
                                        kvp => kvp.Value);

        #endregion

        #region Constructor(s)

        #region SOAPServer(TCPPort, ...)

        /// <summary>
        /// Initialize the SOAP server using the given parameters.
        /// </summary>
        /// <param name="TCPPort">An IP port to listen on.</param>
        /// <param name="DefaultServerName">The default HTTP server name, used whenever no HTTP Host-header has been given.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// 
        /// <param name="SOAPContentType">The default HTTP content type used for all SOAP requests/responses.</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="AutoStart">Start the HTTP server thread immediately (default: no).</param>
        public SOAPServer(IPPort                                                    TCPPort,
                          String                                                    DefaultServerName            = HTTPTestServer.DefaultHTTPServerName,
                          String?                                                   ServiceName                  = null,
                          HTTPContentType?                                          SOAPContentType              = null,

                          ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                          RemoteTLSClientCertificateValidationHandler<SOAPServer>?  ClientCertificateValidator   = null,
                          LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                          SslProtocols?                                             AllowedTLSProtocols          = null,
                          Boolean?                                                  ClientCertificateRequired    = null,
                          Boolean?                                                  CheckCertificateRevocation   = null,

                          ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                          TimeSpan?                                                 ConnectionTimeout            = null,
                          UInt32?                                                   MaxClientConnections         = null,

                          IDNSClient?                                               DNSClient                    = null,
                          Boolean                                                   AutoStart                    = false)

            : this(new HTTPTestServerX(
                       IPAddress.Any,
                       TCPPort,
                       DefaultServerName,
                       //ServiceName,

                       null,
                       null,
                       null,
                       null,

                       ServerCertificateSelector,
                       null,
                       LocalCertificateSelector,
                       AllowedTLSProtocols,
                       ClientCertificateRequired,
                       CheckCertificateRevocation,

                       //ConnectionTimeout,
                       ConnectionIdBuilder,
                       MaxClientConnections,
                       DNSClient
                   ),
                   SOAPContentType
                  )

        {

            if (AutoStart)
                HTTPServer.Start().GetAwaiter().GetResult();

        }

        #endregion

        #region SOAPServer(HTTPServer, ...)

        /// <summary>
        /// Initialize the SOAP server using the given parameters.
        /// </summary>
        /// <param name="HTTPServer">The underlying HTTP server.</param>
        /// <param name="SOAPContentType">The default HTTP content type used for all SOAP requests/responses.</param>
        public SOAPServer(HTTPTestServerX   HTTPServer,
                          HTTPContentType?  SOAPContentType   = null)

        {

            this.SOAPContentType  = SOAPContentType ?? DefaultSOAPContentType;
            this.HTTPServer       = HTTPServer;

        }

        #endregion

        #endregion


        #region RegisterSOAPDelegate(Hostname, URLTemplate, Description, SOAPMatch, SOAPBodyDelegate)

        /// <summary>
        /// Register a SOAP delegate.
        /// </summary>
        /// <param name="Hostname">The HTTP Hostname.</param>
        /// <param name="URLTemplate">The URL template.</param>
        /// <param name="Description">A description of this SOAP delegate.</param>
        /// <param name="SOAPMatch">A delegate to check whether this dispatcher matches the given XML.</param>
        /// <param name="SOAPBodyDelegate">A delegate to process a matching SOAP request.</param>
        public void RegisterSOAPDelegate(HTTPAPIX          HTTPAPI,
                                         HTTPHostname      Hostname,
                                         HTTPPath          URLTemplate,
                                         String            Description,
                                         SOAPMatch         SOAPMatch,
                                         SOAPBodyDelegate  SOAPBodyDelegate)
        {

            SOAPDispatcher? soapDispatcher = null;

            var requestHandle = HTTPAPI.GetRequestHandle(
                                    URLTemplate,
                                    HTTPMethod.POST,
                                    SOAPContentType
                                );

            if (requestHandle.RouteNode is null)
            {

                soapDispatcher = new SOAPDispatcher(URLTemplate, SOAPContentType);
                soapDispatchers.Add(URLTemplate, soapDispatcher);

                // Register a new SOAP dispatcher
                HTTPAPI.AddHandler(
                    HTTPMethod.POST,
                    URLTemplate,
                    soapDispatcher.Invoke,
                    SOAPContentType
                );

                // Register some information text for people using HTTP GET
                HTTPAPI.AddHandler(
                    HTTPMethod.GET,
                    URLTemplate,
                    soapDispatcher.EndpointTextInfo,
                    SOAPContentType
                );

            }

            if (soapDispatchers.TryGetValue(URLTemplate, out var existingSOAPDispatcher))
                existingSOAPDispatcher.RegisterSOAPDelegate(
                    Description,
                    SOAPMatch,
                    SOAPBodyDelegate
                );

        }

        #endregion

        #region RegisterSOAPDelegate(Hostname, URITemplate, Description, SOAPMatch, SOAPHeaderAndBodyDelegate)

        /// <summary>
        /// Register a SOAP delegate.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="Description">A description of this SOAP delegate.</param>
        /// <param name="SOAPMatch">A delegate to check whether this dispatcher matches the given XML.</param>
        /// <param name="SOAPHeaderAndBodyDelegate">A delegate to process a matching SOAP request.</param>
        public void RegisterSOAPDelegate(HTTPAPIX                   HTTPAPI,
                                         HTTPHostname               Hostname,
                                         HTTPPath                   URITemplate,
                                         String                     Description,
                                         SOAPMatch                  SOAPMatch,
                                         SOAPHeaderAndBodyDelegate  SOAPHeaderAndBodyDelegate)
        {

            SOAPDispatcher? soapDispatcher = null;

            // Check if there are other SOAP dispatchers at the given URI template.
            var requestHandle = HTTPServer.GetRequestHandle(
                                    HTTPHostname.Any,
                                    //out var errorResponse,
                                    HTTPMethod.POST,
                                    URITemplate,
                                    hTTPContentTypes => SOAPContentType
                                );

            if (requestHandle is null)
            {

                soapDispatcher = new SOAPDispatcher(URITemplate, SOAPContentType);
                soapDispatchers.Add(URITemplate, soapDispatcher);

                // Register a new SOAP dispatcher
                HTTPServer.AddHandler(
                    HTTPAPI,
                    soapDispatcher.Invoke,
                    Hostname,
                    URITemplate,
                    HTTPMethod.POST,
                    SOAPContentType
                );

                // Register some information text for people using HTTP GET
                HTTPServer.AddHandler(
                    HTTPAPI,
                    soapDispatcher.EndpointTextInfo,
                    Hostname,
                    URITemplate,
                    HTTPMethod.GET,
                    SOAPContentType
                );

            }

            else
                soapDispatcher = requestHandle?.RequestHandlers?.RequestHandler?.Target as SOAPDispatcher;

            if (soapDispatcher is null)
                throw new Exception("'" + URITemplate.ToString() + "' does not seem to be a valid SOAP endpoint!");

            soapDispatcher.RegisterSOAPDelegate(
                Description,
                SOAPMatch,
                SOAPHeaderAndBodyDelegate
            );

        }

        #endregion


    }

}
