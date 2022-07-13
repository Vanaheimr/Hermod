/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Net.Security;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    /// <summary>
    /// A HTTP/SOAP/XML server API.
    /// </summary>
    public abstract class ASOAPServer
    {

        #region Data

        /// <summary>
        /// The default HTTP/SOAP/XML server name.
        /// </summary>
        public const           String           DefaultHTTPServerName   = "GraphDefined HTTP/SOAP/XML Server API";

        /// <summary>
        /// The default HTTP/SOAP/XML server TCP port.
        /// </summary>
        public static readonly IPPort           DefaultHTTPServerPort   = IPPort.HTTPS;

        /// <summary>
        /// The default HTTP/SOAP/XML server URL prefix.
        /// </summary>
        public static readonly HTTPPath         DefaultURLPrefix        = HTTPPath.Parse("/");

        /// <summary>
        /// The default HTTP/SOAP/XML content type.
        /// </summary>
        public static readonly HTTPContentType  DefaultContentType      = SOAPServer.DefaultSOAPContentType;

        /// <summary>
        /// The default request timeout.
        /// </summary>
        public static readonly TimeSpan         DefaultRequestTimeout   = TimeSpan.FromMinutes(1);

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP/SOAP server.
        /// </summary>
        public SOAPServer  SOAPServer   { get; }

        /// <summary>
        /// The common URL prefix for this HTTP/SOAP service.
        /// </summary>
        public HTTPPath    URLPrefix    { get; }

        /// <summary>
        /// The DNS client used by this server.
        /// </summary>
        public DNSClient   DNSClient    { get; }

        /// <summary>
        /// All TCP ports this SOAP server listens on.
        /// </summary>
        public IEnumerable<IPPort> IPPorts
            => SOAPServer.HTTPServer.IPPorts;


        /// <summary>
        /// The SOAP server logger.
        /// </summary>
        public HTTPServerLogger  HTTPLogger    { get; set; }

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

        #region ASOAPServer(HTTPServerName, TCPPort = default, URLPrefix = default, SOAPContentType = default, DNSClient = null, RegisterHTTPRootService = true, AutoStart = false)

        /// <summary>
        /// Initialize a new HTTP server for the HTTP/SOAP/XML Server API using IPAddress.Any.
        /// </summary>
        /// <param name="HTTPServerName">An optional identification string for the HTTP server.</param>
        /// <param name="TCPPort">An optional TCP port for the HTTP server.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// 
        /// <param name="URLPrefix">An optional prefix for the HTTP URIs.</param>
        /// <param name="SOAPContentType">The HTTP content type for SOAP messages.</param>
        /// <param name="DNSClient">An optional DNS client to use.</param>
        /// <param name="RegisterHTTPRootService">Register HTTP root services for sending a notice to clients connecting via HTML or plain text.</param>
        /// <param name="AutoStart">Start the server immediately.</param>
        protected ASOAPServer(String            HTTPServerName            = DefaultHTTPServerName,
                              IPPort?           TCPPort                   = null,
                              String            ServiceName               = null,

                              HTTPPath?         URLPrefix                 = null,
                              HTTPContentType   SOAPContentType           = null,
                              Boolean           RegisterHTTPRootService   = true,
                              DNSClient         DNSClient                 = null,
                              Boolean           AutoStart                 = false)

            : this(new SOAPServer(TCPPort:            TCPPort         ?? DefaultHTTPServerPort,
                                  DefaultServerName:  HTTPServerName,
                                  ServiceName:        ServiceName,

                                  SOAPContentType:    SOAPContentType ?? DefaultContentType,
                                  DNSClient:          DNSClient,
                                  Autostart:          false),
                   URLPrefix)

        {

            if (RegisterHTTPRootService)
                RegisterRootService();

            if (AutoStart)
                Start();

        }

        #endregion

        #region ASOAPServer(HTTPServerName, TCPPort = default, URLPrefix = default, SOAPContentType = default, DNSClient = null, RegisterHTTPRootService = true, AutoStart = false)

        /// <summary>
        /// Initialize a new HTTP server for the HTTP/SOAP/XML Server API using IPAddress.Any.
        /// </summary>
        /// <param name="HTTPServerName">An optional identification string for the HTTP server.</param>
        /// <param name="TCPPort">An optional TCP port for the HTTP server.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// <param name="URLPrefix">An optional prefix for the HTTP URIs.</param>
        /// <param name="SOAPContentType">The HTTP content type for SOAP messages.</param>
        /// <param name="DNSClient">An optional DNS client to use.</param>
        /// <param name="RegisterHTTPRootService">Register HTTP root services for sending a notice to clients connecting via HTML or plain text.</param>
        /// <param name="AutoStart">Start the server immediately.</param>
        protected ASOAPServer(String                               HTTPServerName               = DefaultHTTPServerName,
                              IPPort?                              TCPPort                      = null,
                              String                               ServiceName                  = null,

                              ServerCertificateSelectorDelegate    ServerCertificateSelector    = null,
                              RemoteCertificateValidationCallback  ClientCertificateValidator   = null,
                              LocalCertificateSelectionCallback    ClientCertificateSelector    = null,
                              SslProtocols                         AllowedTLSProtocols          = SslProtocols.Tls12,
                              HTTPPath?                            URLPrefix                    = null,
                              HTTPContentType                      SOAPContentType              = null,
                              Boolean                              RegisterHTTPRootService      = true,
                              DNSClient                            DNSClient                    = null,
                              Boolean                              AutoStart                    = false)

            : this(new SOAPServer(TCPPort:                     TCPPort ?? DefaultHTTPServerPort,
                                  DefaultServerName:           HTTPServerName,
                                  ServiceName:                 ServiceName,

                                  SOAPContentType:             SOAPContentType ?? DefaultContentType,
                                  ServerCertificateSelector:   ServerCertificateSelector,
                                  ClientCertificateValidator:  ClientCertificateValidator,
                                  ClientCertificateSelector:   ClientCertificateSelector,
                                  AllowedTLSProtocols:         AllowedTLSProtocols,
                                  DNSClient:                   DNSClient,
                                  Autostart:                   false),
                   URLPrefix)

        {

            if (RegisterHTTPRootService)
                RegisterRootService();

            if (AutoStart)
                Start();

        }

        #endregion

        #region ASOAPServer(SOAPServer, URLPrefix = default, RegisterHTTPRootService = false)

        /// <summary>
        /// Use the given HTTP server for the HTTP/SOAP/XML Server API.
        /// </summary>
        /// <param name="SOAPServer">A SOAP server.</param>
        /// <param name="URLPrefix">An optional URL prefix for the SOAP URI templates.</param>
        protected ASOAPServer(SOAPServer  SOAPServer,
                              HTTPPath?   URLPrefix = null)
        {

            #region Initial checks

            if (URLPrefix.HasValue)
                while (URLPrefix.Value.EndsWith("/", StringComparison.Ordinal) && URLPrefix.Value.Length > 1)
                    URLPrefix = URLPrefix.Value.Substring(0, (Int32) URLPrefix.Value.Length - 1);

            #endregion

            this.SOAPServer  = SOAPServer ?? throw new ArgumentNullException(nameof(SOAPServer), "The given SOAP server must not be null!");
            this.URLPrefix   = URLPrefix ?? DefaultURLPrefix;
            this.DNSClient   = SOAPServer.HTTPServer.DNSClient;

            SOAPServer.HTTPServer.RequestLog   += (HTTPProcessor, ServerTimestamp, Request)                                 => RequestLog. WhenAll(HTTPProcessor, ServerTimestamp, Request);
            SOAPServer.HTTPServer.ResponseLog  += (HTTPProcessor, ServerTimestamp, Request, Response)                       => ResponseLog.WhenAll(HTTPProcessor, ServerTimestamp, Request, Response);
            SOAPServer.HTTPServer.ErrorLog     += (HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException) => ErrorLog.   WhenAll(HTTPProcessor, ServerTimestamp, Request, Response, Error, LastException);

        }

        #endregion

        #endregion


        #region (private) RegisterRootService()

        private void RegisterRootService()
        {

            SOAPServer.HTTPServer.
                AddMethodCallback(HTTPHostname.Any,
                                  HTTPMethod.GET,

                                  new HTTPPath[] {
                                      HTTPPath.Parse("/"),
                                      URLPrefix + "/"
                                  },

                                  new HTTPContentType[] {
                                      HTTPContentType.TEXT_UTF8,
                                      HTTPContentType.HTML_UTF8
                                  },

                                  HTTPDelegate: Request => {

                                      return Task.FromResult(
                                          new HTTPResponse.Builder(Request) {

                                              HTTPStatusCode  = HTTPStatusCode.BadGateway,
                                              ContentType     = HTTPContentType.TEXT_UTF8,
                                              Content         = ("Welcome at " + DefaultHTTPServerName + Environment.NewLine +
                                                                 "This is a HTTP/SOAP/XML endpoint!" + Environment.NewLine + Environment.NewLine +

                                                                 ((Request.HTTPBodyStream is SslStream)
                                                                      ? (Request.HTTPBodyStream as SslStream)?.RemoteCertificate.Subject + Environment.NewLine +
                                                                        (Request.HTTPBodyStream as SslStream)?.RemoteCertificate.Issuer  + Environment.NewLine +
                                                                         Environment.NewLine
                                                                      : "") +

                                                                 "Defined endpoints: " + Environment.NewLine + Environment.NewLine +
                                                                 SOAPServer.
                                                                     SOAPDispatchers.
                                                                     Select(group => " - " + group.Key + Environment.NewLine +
                                                                                     "   " + group.SelectMany(dispatcher => dispatcher.SOAPDispatches).
                                                                                                   Select    (dispatch   => dispatch.  Description).
                                                                                                   AggregateWith(", ")
                                                                           ).AggregateWith(Environment.NewLine + Environment.NewLine)
                                                                ).ToUTF8Bytes(),
                                              Connection      = "close"

                                          }.AsImmutable);

                                  },

                                  AllowReplacement: URLReplacement.Allow);

        }

        #endregion


        #region Start()

        /// <summary>
        /// Start the SOAP API.
        /// </summary>
        public virtual void Start()
        {
            SOAPServer.HTTPServer.Start();
        }

        #endregion

        #region Shutdown(Message = null, Wait = true)

        /// <summary>
        /// Stop the SOAP API.
        /// </summary>
        /// <param name="Message">An optional shutdown message.</param>
        /// <param name="Wait">Wait for a clean shutdown of the API.</param>
        public virtual void Shutdown(String   Message  = null,
                                     Boolean  Wait     = true)
        {
            SOAPServer.HTTPServer.Shutdown(Message, Wait);
        }

        #endregion


    }

}
