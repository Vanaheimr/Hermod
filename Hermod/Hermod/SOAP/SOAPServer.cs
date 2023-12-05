/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using System.Net.Security;
using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    /// <summary>
    /// A HTTP/SOAP/XML server.
    /// </summary>
    public class SOAPServer
    {

        #region Data

        /// <summary>
        /// The default HTTP content type used for all SOAP requests/responses.
        /// </summary>
        public static readonly HTTPContentType DefaultSOAPContentType = HTTPContentType.Application.SOAPXML_UTF8;

        private readonly Dictionary<HTTPPath, SOAPDispatcher> soapDispatchers;

        #endregion

        #region Properties

        /// <summary>
        /// The underlying HTTP server.
        /// </summary>
        public HTTPServer       HTTPServer         { get; }

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
        /// <param name="DefaultServerName">The default HTTP servername, used whenever no HTTP Host-header has been given.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// 
        /// <param name="SOAPContentType">The default HTTP content type used for all SOAP requests/responses.</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="AutoStart">Start the HTTP server thread immediately (default: no).</param>
        public SOAPServer(IPPort                               TCPPort,
                          String                               DefaultServerName            = HTTPServer.DefaultHTTPServerName,
                          String?                              ServiceName                  = null,
                          HTTPContentType?                     SOAPContentType              = null,

                          ServerCertificateSelectorDelegate?   ServerCertificateSelector    = null,
                          RemoteCertificateValidationHandler?  ClientCertificateValidator   = null,
                          LocalCertificateSelectionHandler?    ClientCertificateSelector    = null,
                          SslProtocols?                        AllowedTLSProtocols          = null,
                          Boolean?                             ClientCertificateRequired    = null,
                          Boolean?                             CheckCertificateRevocation   = null,

                          ServerThreadNameCreatorDelegate?     ServerThreadNameCreator      = null,
                          ServerThreadPriorityDelegate?        ServerThreadPrioritySetter   = null,
                          Boolean?                             ServerThreadIsBackground     = null,
                          ConnectionIdBuilder?                 ConnectionIdBuilder          = null,
                          TimeSpan?                            ConnectionTimeout            = null,
                          UInt32?                              MaxClientConnections         = null,

                          DNSClient?                           DNSClient                    = null,
                          Boolean                              AutoStart                    = false)

        {

            this.HTTPServer  = new HTTPServer(
                                   TCPPort,
                                   DefaultServerName,
                                   ServiceName,

                                   ServerCertificateSelector,
                                   ClientCertificateValidator,
                                   ClientCertificateSelector,
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
                                   false
                               );

            this.SOAPContentType  = SOAPContentType ?? DefaultSOAPContentType;
            this.soapDispatchers  = new Dictionary<HTTPPath, SOAPDispatcher>();

            if (AutoStart)
                HTTPServer.Start();

        }

        #endregion

        #region SOAPServer(HTTPServer, ...)

        /// <summary>
        /// Initialize the SOAP server using the given parameters.
        /// </summary>
        /// <param name="HTTPServer">The underlying HTTP server.</param>
        /// <param name="SOAPContentType">The default HTTP content type used for all SOAP requests/responses.</param>
        public SOAPServer(HTTPServer        HTTPServer,
                          HTTPContentType?  SOAPContentType   = null)

        {

            this.HTTPServer       = HTTPServer;
            this.SOAPContentType  = SOAPContentType ?? DefaultSOAPContentType;
            this.soapDispatchers  = new Dictionary<HTTPPath, SOAPDispatcher>();

        }

        #endregion

        #endregion


        #region RegisterSOAPDelegate(Hostname, URITemplate, Description, SOAPMatch, SOAPBodyDelegate)

        /// <summary>
        /// Register a SOAP delegate.
        /// </summary>
        /// <param name="Hostname">The HTTP Hostname.</param>
        /// <param name="URITemplate">The URI template.</param>
        /// <param name="Description">A description of this SOAP delegate.</param>
        /// <param name="SOAPMatch">A delegate to check whether this dispatcher matches the given XML.</param>
        /// <param name="SOAPBodyDelegate">A delegate to process a matching SOAP request.</param>
        public void RegisterSOAPDelegate(HTTPAPI           HTTPAPI,
                                         HTTPHostname      Hostname,
                                         HTTPPath          URITemplate,
                                         String            Description,
                                         SOAPMatch         SOAPMatch,
                                         SOAPBodyDelegate  SOAPBodyDelegate)
        {

            SOAPDispatcher? soapDispatcher = null;

            // Check if there are other SOAP dispatchers at the given URI template.
            var requestHandle = HTTPServer.GetRequestHandle(HTTPHostname.Any,
                                                            URITemplate,
                                                            out var errorResponse,
                                                            HTTPMethod.POST,
                                                            hTTPContentTypes => SOAPContentType);

            if (requestHandle is null)
            {

                soapDispatcher = new SOAPDispatcher(URITemplate, SOAPContentType);
                soapDispatchers.Add(URITemplate, soapDispatcher);

                // Register a new SOAP dispatcher
                HTTPServer.AddMethodCallback(HTTPAPI,
                                             Hostname,
                                             HTTPMethod.POST,
                                             URITemplate,
                                             SOAPContentType,
                                             HTTPDelegate: soapDispatcher.Invoke);

                // Register some information text for people using HTTP GET
                HTTPServer.AddMethodCallback(HTTPAPI,
                                             Hostname,
                                             HTTPMethod.GET,
                                             URITemplate,
                                             SOAPContentType,
                                             HTTPDelegate: soapDispatcher.EndpointTextInfo);

            }

            else
                soapDispatcher = requestHandle.RequestHandler?.Target as SOAPDispatcher;

            if (soapDispatcher is null)
                throw new Exception("'" + URITemplate.ToString() + "' does not seem to be a valid SOAP endpoint!");

            soapDispatcher.RegisterSOAPDelegate(Description,
                                                SOAPMatch,
                                                SOAPBodyDelegate);

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
        public void RegisterSOAPDelegate(HTTPAPI                    HTTPAPI,
                                         HTTPHostname               Hostname,
                                         HTTPPath                   URITemplate,
                                         String                     Description,
                                         SOAPMatch                  SOAPMatch,
                                         SOAPHeaderAndBodyDelegate  SOAPHeaderAndBodyDelegate)
        {

            SOAPDispatcher? soapDispatcher = null;

            // Check if there are other SOAP dispatchers at the given URI template.
            var requestHandle = HTTPServer.GetRequestHandle(HTTPHostname.Any,
                                                            URITemplate,
                                                            out var errorResponse,
                                                            HTTPMethod.POST,
                                                            hTTPContentTypes => SOAPContentType);

            if (requestHandle is null)
            {

                soapDispatcher = new SOAPDispatcher(URITemplate, SOAPContentType);
                soapDispatchers.Add(URITemplate, soapDispatcher);

                // Register a new SOAP dispatcher
                HTTPServer.AddMethodCallback(HTTPAPI,
                                             Hostname,
                                             HTTPMethod.POST,
                                             URITemplate,
                                             SOAPContentType,
                                             HTTPDelegate: soapDispatcher.Invoke);

                // Register some information text for people using HTTP GET
                HTTPServer.AddMethodCallback(HTTPAPI,
                                             Hostname,
                                             HTTPMethod.GET,
                                             URITemplate,
                                             SOAPContentType,
                                             HTTPDelegate: soapDispatcher.EndpointTextInfo);

            }

            else
                soapDispatcher = requestHandle.RequestHandler?.Target as SOAPDispatcher;

            if (soapDispatcher is null)
                throw new Exception("'" + URITemplate.ToString() + "' does not seem to be a valid SOAP endpoint!");

            soapDispatcher.RegisterSOAPDelegate(Description,
                                                SOAPMatch,
                                                SOAPHeaderAndBodyDelegate);

        }

        #endregion


    }

}
