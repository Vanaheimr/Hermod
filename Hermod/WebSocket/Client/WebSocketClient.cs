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

using System.Diagnostics;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{


    /// <summary>
    /// The delegate for logging the HTTP request send by a HTTP client.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the outgoing HTTP request.</param>
    /// <param name="WebSocketClient">The HTTP WebSocket client sending the HTTP request.</param>
    /// <param name="Request">The outgoing HTTP request.</param>
    public delegate Task ClientRequestLogHandler(DateTimeOffset    Timestamp,
                                                 IWebSocketClient  WebSocketClient,
                                                 HTTPRequest       Request);


    /// <summary>
    /// The delegate for logging the HTTP response received by a HTTP client.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming HTTP response.</param>
    /// <param name="WebSocketClient">The HTTP WebSocketclient receiving the HTTP request.</param>
    /// <param name="Request">The outgoing HTTP request.</param>
    /// <param name="Response">The incoming HTTP response.</param>
    public delegate Task ClientResponseLogHandler(DateTimeOffset    Timestamp,
                                                  IWebSocketClient  WebSocketClient,
                                                  HTTPRequest       Request,
                                                  HTTPResponse      Response);


    /// <summary>
    /// A HTTP WebSocket client.
    /// </summary>
    public class WebSocketClient : ATLSClient,
                                   IWebSocketClient
    {

        #region Data

        /// <summary>
        /// The default HTTP user agent string.
        /// </summary>
        public const           String  DefaultHTTPUserAgent  = "GraphDefined HTTP WebSocket Client";

        /// <summary>
        /// The default remote TCP port to connect to.
        /// </summary>
        public static readonly IPPort  DefaultRemotePort     = IPPort.Parse(443);


        protected Stream?           HTTPStream;

        /// <summary>
        /// The default maintenance interval.
        /// </summary>
        public           readonly TimeSpan                    DefaultMaintenanceEvery     = TimeSpan.FromSeconds(1);
        private          readonly Timer                       MaintenanceTimer;

        protected readonly        SemaphoreSlim               MaintenanceSemaphore        = new(1, 1);

        public           readonly TimeSpan                    DefaultWebSocketPingEvery   = TimeSpan.FromSeconds(30);

        private          readonly Timer                       WebSocketPingTimer;

        protected static readonly TimeSpan                    SemaphoreSlimTimeout        = TimeSpan.FromSeconds(5);

        private const             String                      LogfileName                 = "WebSocketClient.log";

        private                   Task?                       networkingTask;
        private readonly          CancellationTokenSource     networkingCancellationTokenSource;
        private readonly          CancellationToken           networkingCancellationToken;

        protected                 WebSocketClientConnection?  webSocketClientConnection;

        #endregion

        #region Properties

        /// <summary>
        /// The attached OCPP CP client (HTTP/websocket client) logger.
        /// </summary>
       // public WebSocketClient.CPClientLogger    Logger                          { get; }



        /// <summary>
        /// The virtual HTTP hostname to connect to.
        /// </summary>
        public HTTPHostname?                                                   VirtualHostname                           { get; }

        /// <summary>
        /// The remote TLS certificate validator.
        /// </summary>
        public new RemoteTLSServerCertificateValidationHandler<IWebSocketClient>?  RemoteCertificateValidator            { get; }

        /// <summary>
        /// An optional HTTP content type.
        /// </summary>
        public HTTPContentType?                                                ContentType                               { get; }

        /// <summary>
        /// The optional HTTP accept header.
        /// </summary>
        public AcceptTypes?                                                    Accept                                    { get; }

        /// <summary>
        /// The optional HTTP authentication to use.
        /// </summary>
        public IHTTPAuthentication?                                            HTTPAuthentication                        { get; set; }

        /// <summary>
        /// The optional Time-Based One-Time Password (TOTP) configuration.
        /// </summary>
        public TOTPConfig?                                                     TOTPConfig                                { get; }

        /// <summary>
        /// The HTTP user agent identification.
        /// </summary>
        public String                                                          HTTPUserAgent                             { get; }

        /// <summary>
        /// The optional HTTP connection type.
        /// </summary>
        public ConnectionType?                                                 Connection                                { get; }

        /// <summary>
        /// The timeout for upstream requests.
        /// </summary>
        public TimeSpan                                                        RequestTimeout                            { get; set; }

        /// <summary>
        /// The CPO client (HTTP client) logger.
        /// </summary>
        public HTTPClientLogger?                                               HTTPLogger                                { get; set; }




        public UInt64                                                          KeepAliveMessageCount                     { get; private set; } = 0;



        /// <summary>
        /// Our local IP port.
        /// </summary>
        public IPPort                                                          LocalPort
            => CurrentLocalPort.HasValue
                   ? IPPort.Parse(CurrentLocalPort.Value)
                   : IPPort.Zero;


        public Int32? Available
            => tcpClient?.Available;

        public Boolean Connected
            => IsConnected;

        [DisallowNull]
        public LingerOption? LingerState
        {
            get
            {
                return tcpClient?.Client?.LingerState;
            }
            set
            {
                if (tcpClient?.Client is not null)
                    tcpClient.Client.LingerState = value;
            }
        }

        [DisallowNull]
        public Boolean? NoDelay
        {
            get
            {
                return tcpClient?.NoDelay;
            }
            set
            {
                if (tcpClient is not null)
                    tcpClient.NoDelay = value.Value;
            }
        }

        [DisallowNull]
        public Byte TTL
        {
            get
            {
                return (Byte) (tcpClient?.Client?.Ttl ?? 0);
            }
            set
            {
                if (tcpClient?.Client is not null)
                    tcpClient.Client.Ttl = value;
            }
        }

        /// <summary>
        /// Disable all maintenance tasks.
        /// </summary>
        public Boolean                              DisableMaintenanceTasks              { get; set; }

        /// <summary>
        /// The maintenance interval.
        /// </summary>
        public TimeSpan                             MaintenanceEvery                     { get; }

        /// <summary>
        /// Disable web socket pings.
        /// </summary>
        public Boolean                              DisableWebSocketPings                { get; set; }

        /// <summary>
        /// The web socket ping interval.
        /// </summary>
        public TimeSpan                             WebSocketPingEvery                   { get; }


        public TimeSpan?                            SlowNetworkSimulationDelay           { get; set; }


        public IEnumerable<String>                  SecWebSocketProtocols                { get; }

        public Boolean                              CloseConnectionOnUnexpectedFrames    { get; set; } = false;

        /// <summary>
        /// The optional error message when this client closed the HTTP WebSocket connection.
        /// </summary>
        public String?                              ClientCloseMessage                   { get; private set; }

        public ECPrivateKeyParameters?              AuthKey                              { get; }

        /// <summary>
        /// The attached debug logger.
        /// </summary>
        public ILogger                              Logger                               { get; }

        /// <summary>
        /// The attached logger factory.
        /// </summary>
        public ILoggerFactory                       LoggerFactory                        { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event sent whenever a text message was sent.
        /// </summary>
        public event OnWebSocketClientTextMessageSentDelegate?         OnTextMessageSent;

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        public event OnWebSocketClientTextMessageReceivedDelegate?     OnTextMessageReceived;


        /// <summary>
        /// An event sent whenever a binary message was sent.
        /// </summary>
        public event OnWebSocketClientBinaryMessageSentDelegate?       OnBinaryMessageSent;

        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        public event OnWebSocketClientBinaryMessageReceivedDelegate?   OnBinaryMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket ping frame was sent.
        /// </summary>
        public event OnWebSocketClientPingMessageSentDelegate?         OnPingMessageSent;

        /// <summary>
        /// An event sent whenever a web socket ping frame was received.
        /// </summary>
        public event OnWebSocketClientPingMessageReceivedDelegate?     OnPingMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket pong frame was sent.
        /// </summary>
        public event OnWebSocketClientPongMessageSentDelegate?         OnPongMessageSent;

        /// <summary>
        /// An event sent whenever a web socket pong frame was received.
        /// </summary>
        public event OnWebSocketClientPongMessageReceivedDelegate?     OnPongMessageReceived;


        /// <summary>
        /// An event sent whenever a HTTP WebSocket CLOSE frame was sent.
        /// </summary>
        public event OnWebSocketClientCloseMessageSentDelegate?        OnCloseMessageSent;

        /// <summary>
        /// An event sent whenever a HTTP WebSocket CLOSE frame was received.
        /// </summary>
        public event OnWebSocketClientCloseMessageReceivedDelegate?    OnCloseMessageReceived;


        #region HTTPRequest-/ResponseLog

        /// <summary>
        /// A delegate for logging the HTTP request.
        /// </summary>
        public event ClientRequestLogHandler?   RequestLogDelegate;

        /// <summary>
        /// A delegate for logging the HTTP request/response.
        /// </summary>
        public event ClientResponseLogHandler?  ResponseLogDelegate;

        #endregion

        #endregion

        #region Constructor(s)

        #region WebSocketClient (IPAddress,  TCPPort, ...)

        /// <summary>
        /// Create a new charge point websocket client running on a charge point
        /// and connecting to a central system to invoke methods.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this HTTP/websocket client.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="RequestTimeout">An optional Request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="LoggingContext">An optional context for logging client methods.</param>
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public WebSocketClient(IIPAddress                                                      IPAddress,
                               IPPort                                                          TCPPort,
                               I18NString?                                                     Description                      = null,

                               HTTPHostname?                                                   VirtualHostname                  = null,
                               String?                                                         HTTPUserAgent                    = DefaultHTTPUserAgent,
                               IHTTPAuthentication?                                            HTTPAuthentication               = null,
                               IEnumerable<String>?                                            SecWebSocketProtocols            = null,
                               TimeSpan?                                                       RequestTimeout                   = null,

                               Boolean                                                         DisableWebSocketPings            = false,
                               TimeSpan?                                                       WebSocketPingEvery               = null,
                               TimeSpan?                                                       SlowNetworkSimulationDelay       = null,

                               Boolean                                                         DisableMaintenanceTasks          = false,
                               TimeSpan?                                                       MaintenanceEvery                 = null,

                               String?                                                         TLSHostname                      = null,
                               RemoteTLSServerCertificateValidationHandler<IWebSocketClient>?  RemoteCertificateValidator       = null,
                               LocalCertificateSelectionHandler?                               LocalCertificateSelector         = null,
                               IEnumerable<X509Certificate2>?                                  ClientCertificates               = null,
                               SslStreamCertificateContext?                                    ClientCertificateContext         = null,
                               IEnumerable<X509Certificate2>?                                  ClientCertificateChain           = null,
                               SslProtocols?                                                   TLSProtocols                     = null,
                               CipherSuitesPolicy?                                             CipherSuitesPolicy               = null,
                               X509ChainPolicy?                                                CertificateChainPolicy           = null,
                               X509RevocationMode?                                             CertificateRevocationCheckMode   = null,
                               Boolean?                                                        EnforceTLS                       = null,
                               IEnumerable<SslApplicationProtocol>?                            ApplicationProtocols             = null,
                               Boolean?                                                        AllowRenegotiation               = null,
                               Boolean?                                                        AllowTLSResume                   = null,
                               TOTPConfig?                                                     TOTPConfig                       = null,

                               IPVersionPreference?                                            IPVersionPreference              = null,
                               TimeSpan?                                                       ConnectTimeout                   = null,
                               TimeSpan?                                                       ReceiveTimeout                   = null,
                               TimeSpan?                                                       SendTimeout                      = null,
                               TransmissionRetryDelayDelegate?                                 TransmissionRetryDelay           = null,
                               UInt16?                                                         MaxNumberOfRetries               = null,
                               UInt32?                                                         InternalBufferSize               = null,

                               Boolean?                                                        DisableLogging                   = null,
                               String?                                                         LoggingPath                      = null,
                               String                                                          LoggingContext                   = "logcontext", //CPClientLogger.DefaultContext,
                               LogfileCreatorDelegate?                                         LogfileCreator                   = null,
                               HTTPClientLogger?                                               HTTPLogger                       = null,

                               ILogger<ATLSClient>?                                            Logger                           = null,
                               ILoggerFactory?                                                 LoggerFactory                    = null)

            : base(IPAddress,
                   TCPPort,
                   Description,

                   TLSHostname,
                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               (IWebSocketClient) tlsClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   EnforceTLS,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   null,
                   LoggerFactory)

        {

            this.VirtualHostname                    = VirtualHostname;
            this.RemoteCertificateValidator         = RemoteCertificateValidator;
            this.HTTPUserAgent                      = HTTPUserAgent           ?? DefaultHTTPUserAgent;
            this.HTTPAuthentication                 = HTTPAuthentication;
            this.RequestTimeout                     = RequestTimeout          ?? TimeSpan.FromMinutes(10);
            this.HTTPLogger                         = HTTPLogger;
            this.LoggerFactory                      = LoggerFactory           ?? NullLoggerFactory.Instance;
            this.Logger                             = Logger                  ?? this.LoggerFactory.CreateLogger<WebSocketClient>();

            this.SecWebSocketProtocols              = SecWebSocketProtocols   ?? [];
            this.TOTPConfig                         = TOTPConfig;

            this.DisableMaintenanceTasks            = DisableMaintenanceTasks;
            this.MaintenanceEvery                   = MaintenanceEvery        ?? DefaultMaintenanceEvery;
            this.MaintenanceTimer                   = new Timer(
                                                          DoMaintenanceSync,
                                                          null,
                                                          this.MaintenanceEvery,
                                                          this.MaintenanceEvery
                                                      );

            this.DisableWebSocketPings              = DisableWebSocketPings;
            this.WebSocketPingEvery                 = WebSocketPingEvery      ?? DefaultWebSocketPingEvery;
            this.WebSocketPingTimer                 = new Timer(
                                                          DoWebSocketPingSync,
                                                          null,
                                                          this.WebSocketPingEvery,
                                                          this.WebSocketPingEvery
                                                      );

            this.SlowNetworkSimulationDelay         = SlowNetworkSimulationDelay;

            this.networkingCancellationTokenSource  = new CancellationTokenSource();
            this.networkingCancellationToken        = networkingCancellationTokenSource.Token;

        }

        #endregion

        #region WebSocketClient (URL, ...)

        /// <summary>
        /// Create a new charge point websocket client running on a charge point
        /// and connecting to a central system to invoke methods.
        /// </summary>
        /// <param name="URL">The remote URL of the HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this HTTP/websocket client.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="RequestTimeout">An optional Request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="LoggingContext">An optional context for logging client methods.</param>
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public WebSocketClient(URL                                                             URL,
                               I18NString?                                                     Description                      = null,

                               HTTPHostname?                                                   VirtualHostname                  = null,
                               String?                                                         HTTPUserAgent                    = DefaultHTTPUserAgent,
                               IHTTPAuthentication?                                            HTTPAuthentication               = null,
                               IEnumerable<String>?                                            SecWebSocketProtocols            = null,
                               TimeSpan?                                                       RequestTimeout                   = null,

                               Boolean                                                         DisableWebSocketPings            = false,
                               TimeSpan?                                                       WebSocketPingEvery               = null,
                               TimeSpan?                                                       SlowNetworkSimulationDelay       = null,

                               Boolean                                                         DisableMaintenanceTasks          = false,
                               TimeSpan?                                                       MaintenanceEvery                 = null,

                               String?                                                         TLSHostname                      = null,
                               RemoteTLSServerCertificateValidationHandler<IWebSocketClient>?  RemoteCertificateValidator       = null,
                               LocalCertificateSelectionHandler?                               LocalCertificateSelector         = null,
                               IEnumerable<X509Certificate2>?                                  ClientCertificates               = null,
                               SslStreamCertificateContext?                                    ClientCertificateContext         = null,
                               IEnumerable<X509Certificate2>?                                  ClientCertificateChain           = null,
                               SslProtocols?                                                   TLSProtocols                     = null,
                               CipherSuitesPolicy?                                             CipherSuitesPolicy               = null,
                               X509ChainPolicy?                                                CertificateChainPolicy           = null,
                               X509RevocationMode?                                             CertificateRevocationCheckMode   = null,
                               Boolean?                                                        EnforceTLS                       = null,
                               IEnumerable<SslApplicationProtocol>?                            ApplicationProtocols             = null,
                               Boolean?                                                        AllowRenegotiation               = null,
                               Boolean?                                                        AllowTLSResume                   = null,
                               TOTPConfig?                                                     TOTPConfig                       = null,

                               IPVersionPreference?                                            IPVersionPreference              = null,
                               TimeSpan?                                                       ConnectTimeout                   = null,
                               TimeSpan?                                                       ReceiveTimeout                   = null,
                               TimeSpan?                                                       SendTimeout                      = null,
                               TransmissionRetryDelayDelegate?                                 TransmissionRetryDelay           = null,
                               UInt16?                                                         MaxNumberOfRetries               = null,
                               UInt32?                                                         InternalBufferSize               = null,

                               Boolean?                                                        DisableLogging                   = null,
                               String?                                                         LoggingPath                      = null,
                               String                                                          LoggingContext                   = "logcontext", //CPClientLogger.DefaultContext,
                               LogfileCreatorDelegate?                                         LogfileCreator                   = null,
                               HTTPClientLogger?                                               HTTPLogger                       = null,

                               IDNSClient?                                                     DNSClient                        = null,
                               ILogger<ATLSClient>?                                            Logger                           = null,
                               ILoggerFactory?                                                 LoggerFactory                    = null)

            : base(URL,
                   Description,

                   TLSHostname,
                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               (IWebSocketClient) tlsClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   EnforceTLS,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   DNSClient,
                   null,
                   LoggerFactory)

        {

            this.VirtualHostname                    = VirtualHostname;
            this.RemoteCertificateValidator         = RemoteCertificateValidator;
            this.HTTPUserAgent                      = HTTPUserAgent           ?? DefaultHTTPUserAgent;
            this.HTTPAuthentication                 = HTTPAuthentication;
            this.RequestTimeout                     = RequestTimeout          ?? TimeSpan.FromMinutes(10);
            this.HTTPLogger                         = HTTPLogger;
            this.LoggerFactory                      = LoggerFactory           ?? NullLoggerFactory.Instance;
            this.Logger                             = Logger                  ?? this.LoggerFactory.CreateLogger<WebSocketClient>();

            this.SecWebSocketProtocols              = SecWebSocketProtocols   ?? [];
            this.TOTPConfig                         = TOTPConfig;

            this.DisableMaintenanceTasks            = DisableMaintenanceTasks;
            this.MaintenanceEvery                   = MaintenanceEvery        ?? DefaultMaintenanceEvery;
            this.MaintenanceTimer                   = new Timer(
                                                          DoMaintenanceSync,
                                                          null,
                                                          this.MaintenanceEvery,
                                                          this.MaintenanceEvery
                                                      );

            this.DisableWebSocketPings              = DisableWebSocketPings;
            this.WebSocketPingEvery                 = WebSocketPingEvery      ?? DefaultWebSocketPingEvery;
            this.WebSocketPingTimer                 = new Timer(
                                                          DoWebSocketPingSync,
                                                          null,
                                                          this.WebSocketPingEvery,
                                                          this.WebSocketPingEvery
                                                      );

            this.SlowNetworkSimulationDelay         = SlowNetworkSimulationDelay;

            this.networkingCancellationTokenSource  = new CancellationTokenSource();
            this.networkingCancellationToken        = networkingCancellationTokenSource.Token;

        }

        #endregion

        #region WebSocketClient (DomainName, DNSService, ...)

        /// <summary>
        /// Create a new charge point websocket client running on a charge point
        /// and connecting to a central system to invoke methods.
        /// </summary>
        /// <param name="DomainName">The domain name of the HTTP endpoint to connect to.</param>
        /// <param name="DNSService">The DNS service to use for resolving the domain name.</param>
        /// 
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this HTTP/websocket client.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use for HTTP authentication.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="RequestTimeout">An optional Request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="LoggingContext">An optional context for logging client methods.</param>
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public WebSocketClient(DomainName                                                      DomainName,
                               SRV_Spec                                                        DNSService,
                               I18NString?                                                     Description                      = null,

                               HTTPHostname?                                                   VirtualHostname                  = null,
                               String?                                                         HTTPUserAgent                    = DefaultHTTPUserAgent,
                               IHTTPAuthentication?                                            HTTPAuthentication               = null,
                               IEnumerable<String>?                                            SecWebSocketProtocols            = null,
                               TimeSpan?                                                       RequestTimeout                   = null,

                               Boolean                                                         DisableWebSocketPings            = false,
                               TimeSpan?                                                       WebSocketPingEvery               = null,
                               TimeSpan?                                                       SlowNetworkSimulationDelay       = null,

                               Boolean                                                         DisableMaintenanceTasks          = false,
                               TimeSpan?                                                       MaintenanceEvery                 = null,

                               String?                                                         TLSHostname                      = null,
                               RemoteTLSServerCertificateValidationHandler<IWebSocketClient>?  RemoteCertificateValidator       = null,
                               LocalCertificateSelectionHandler?                               LocalCertificateSelector         = null,
                               IEnumerable<X509Certificate2>?                                  ClientCertificates               = null,
                               SslStreamCertificateContext?                                    ClientCertificateContext         = null,
                               IEnumerable<X509Certificate2>?                                  ClientCertificateChain           = null,
                               SslProtocols?                                                   TLSProtocols                     = null,
                               CipherSuitesPolicy?                                             CipherSuitesPolicy               = null,
                               X509ChainPolicy?                                                CertificateChainPolicy           = null,
                               X509RevocationMode?                                             CertificateRevocationCheckMode   = null,
                               Boolean?                                                        EnforceTLS                       = null,
                               IEnumerable<SslApplicationProtocol>?                            ApplicationProtocols             = null,
                               Boolean?                                                        AllowRenegotiation               = null,
                               Boolean?                                                        AllowTLSResume                   = null,
                               TOTPConfig?                                                     TOTPConfig                       = null,

                               IPVersionPreference?                                            IPVersionPreference              = null,
                               TimeSpan?                                                       ConnectTimeout                   = null,
                               TimeSpan?                                                       ReceiveTimeout                   = null,
                               TimeSpan?                                                       SendTimeout                      = null,
                               TransmissionRetryDelayDelegate?                                 TransmissionRetryDelay           = null,
                               UInt16?                                                         MaxNumberOfRetries               = null,
                               UInt32?                                                         InternalBufferSize               = null,

                               Boolean?                                                        DisableLogging                   = null,
                               String?                                                         LoggingPath                      = null,
                               String                                                          LoggingContext                   = "logcontext", //CPClientLogger.DefaultContext,
                               LogfileCreatorDelegate?                                         LogfileCreator                   = null,
                               HTTPClientLogger?                                               HTTPLogger                       = null,

                               IDNSClient?                                                     DNSClient                        = null,
                               ILogger<ATLSClient>?                                            Logger                           = null,
                               ILoggerFactory?                                                 LoggerFactory                    = null)

            : base(DomainName,
                   DNSService,
                   Description,

                   TLSHostname,
                   RemoteCertificateValidator is not null
                       ? (sender,
                          certificate,
                          certificateChain,
                          tlsClient,
                          policyErrors) => RemoteCertificateValidator.Invoke(
                                               sender,
                                               certificate,
                                               certificateChain,
                                               (IWebSocketClient) tlsClient,
                                               policyErrors
                                           )
                       : null,
                   LocalCertificateSelector,
                   ClientCertificates,
                   ClientCertificateContext,
                   ClientCertificateChain,
                   TLSProtocols,
                   CipherSuitesPolicy,
                   CertificateChainPolicy,
                   CertificateRevocationCheckMode,
                   EnforceTLS,
                   ApplicationProtocols,
                   AllowRenegotiation,
                   AllowTLSResume,

                   IPVersionPreference,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   DisableLogging,
                   DNSClient,
                   null,
                   LoggerFactory)

        {

            this.VirtualHostname                    = VirtualHostname;
            this.RemoteCertificateValidator         = RemoteCertificateValidator;
            this.HTTPUserAgent                      = HTTPUserAgent           ?? DefaultHTTPUserAgent;
            this.HTTPAuthentication                 = HTTPAuthentication;
            this.RequestTimeout                     = RequestTimeout          ?? TimeSpan.FromMinutes(10);
            this.HTTPLogger                         = HTTPLogger;
            this.LoggerFactory                      = LoggerFactory           ?? NullLoggerFactory.Instance;
            this.Logger                             = Logger                  ?? this.LoggerFactory.CreateLogger<WebSocketClient>();

            this.SecWebSocketProtocols              = SecWebSocketProtocols   ?? [];
            this.TOTPConfig                         = TOTPConfig;

            this.DisableMaintenanceTasks            = DisableMaintenanceTasks;
            this.MaintenanceEvery                   = MaintenanceEvery        ?? DefaultMaintenanceEvery;
            this.MaintenanceTimer                   = new Timer(
                                                          DoMaintenanceSync,
                                                          null,
                                                          this.MaintenanceEvery,
                                                          this.MaintenanceEvery
                                                      );

            this.DisableWebSocketPings              = DisableWebSocketPings;
            this.WebSocketPingEvery                 = WebSocketPingEvery      ?? DefaultWebSocketPingEvery;
            this.WebSocketPingTimer                 = new Timer(
                                                          DoWebSocketPingSync,
                                                          null,
                                                          this.WebSocketPingEvery,
                                                          this.WebSocketPingEvery
                                                      );

            this.SlowNetworkSimulationDelay         = SlowNetworkSimulationDelay;

            this.networkingCancellationTokenSource  = new CancellationTokenSource();
            this.networkingCancellationToken        = networkingCancellationTokenSource.Token;

        }

        #endregion

        #endregion


        #region (private) OpenTCPConnection(RequestTimeout = null)

        private async Task OpenTCPConnection(TimeSpan? RequestTimeout = null)
        {

            RequestTimeout ??= TimeSpan.FromSeconds(60);

            var connectionResult = await ReconnectAsync(CancellationToken.None).ConfigureAwait(false);

            if (connectionResult.IsFailure)
                throw new Exception(connectionResult.Errors.Select(error => error.ToString()).AggregateWith(", "));

            HTTPStream = ActiveStream;

            if (HTTPStream is null)
                throw new Exception("The HTTP/WebSocket stream could not be created!");

            HTTPStream.ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

        }

        #endregion

        #region (private) SendHTTPRequest(HTTPRequestBuilder, HTTPAuthorization, CancellationToken = default)

        private async Task<Tuple<HTTPRequest, String>>

            SendHTTPRequest(Action<HTTPRequest.Builder>?  HTTPRequestBuilder   = null,
                            IHTTPAuthentication?          HTTPAuthorization    = null,
                            CancellationToken             CancellationToken    = default)

        {

            // GET /webServices/ocpp/CP3211 HTTP/1.1
            // Host:                    some.server.com:33033
            // Connection:              Upgrade
            // Upgrade:                 websocket
            // Sec-WebSocket-Key:       x3JJHMbDL1EzLkh9GBhXDw==
            // Sec-WebSocket-Protocol:  ocpp2.1, ocpp2.0.1
            // Sec-WebSocket-Version:   13

            var swkaSHA1Base64      = RandomExtensions.RandomBytes(16).ToBase64();
            var expectedWSAccept    = SHA1.HashData((swkaSHA1Base64 + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").ToUTF8Bytes()).ToBase64();

            var httpRequestBuilder  = new HTTPRequest.Builder(this) {
                                          Path                  = RemoteURL.Path,
                                          Host                  = HTTPHostname.Parse(String.Concat(RemoteURL.Hostname, ":", RemoteURL.Port)),
                                          Connection            = ConnectionType.Upgrade,
                                          Upgrade               = "websocket",
                                          SecWebSocketKey       = swkaSHA1Base64,
                                          SecWebSocketProtocol  = SecWebSocketProtocols,
                                          SecWebSocketVersion   = "13",
                                          Authorization         = HTTPAuthorization
                                      };

            HTTPRequestBuilder?.Invoke(httpRequestBuilder);

            var httpRequest = httpRequestBuilder.AsImmutable;

            #region Call the optional HTTP request log delegate

            await LogEvent(
                      RequestLogDelegate,
                      loggingDelegate => loggingDelegate.Invoke(
                          Timestamp.Now,
                          this,
                          httpRequest
                      )
                  );

            #endregion

            if (HTTPStream is not null)
            {
                await HTTPStream.WriteAsync((httpRequest.EntirePDU + "\r\n\r\n").ToUTF8Bytes(), CancellationToken);
                await HTTPStream.FlushAsync(CancellationToken);
            }

            return new Tuple<HTTPRequest, String>(
                           httpRequest,
                           expectedWSAccept
                       );

        }

        #endregion

        #region (private) WaitForHTTPResponse(HTTPRequest, CancellationToken = default)

        private async Task<HTTPResponse> WaitForHTTPResponse(HTTPRequest        HTTPRequest,
                                                             CancellationToken  CancellationToken = default)
        {

            if (HTTPStream is null || tcpClient is null)
                return HTTPResponse.BadRequest(
                           HTTPRequest
                       );

            var buffer  = new Byte[16 * 1024];
            var pos     = 0U;
            var sw      = Stopwatch.StartNew();

            do
            {

                pos += (UInt32) await HTTPStream.ReadAsync(
                                          buffer,
                                          (Int32) pos,
                                          2048,
                                          CancellationToken
                                      );

                if (sw.Elapsed >= (HTTPRequest.Timeout ?? TimeSpan.FromSeconds(5)))
                    throw new HTTPTimeoutException(sw.Elapsed);

                await Task.Delay(1, CancellationToken);

            } while (tcpClient.GetStream().DataAvailable && pos < buffer.Length - 2048);

            var responseData  = buffer.ToUTF8String(pos);
            var lines         = responseData.Split('\n').Select(line => line?.Trim()).TakeWhile(line => line.IsNotNullOrEmpty()).ToArray();
            var httpResponse  = HTTPResponse.Parse(
                                    lines.AggregateWith(Environment.NewLine),
                                    [],
                                    HTTPRequest,
                                    CancellationToken: CancellationToken
                                );

            return httpResponse;

        }

        #endregion


        #region Connect(EventTrackingId = null, RequestTimeout = null, MaxNumberOfRetries = 0)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="EventTrackingId"></param>
        /// <param name="RequestTimeout">An optional timeout.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of retransmissions of this request.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        public async Task<Tuple<WebSocketClientConnection, HTTPResponse>>

            Connect(EventTracking_Id?             EventTrackingId      = null,
                    TimeSpan?                     RequestTimeout       = null,
                    UInt16?                       MaxNumberOfRetries   = 0,
                    Action<HTTPRequest.Builder>?  HTTPRequestBuilder   = null,
                    CancellationToken             CancellationToken    = default)

        {

            RequestTimeout ??= this.RequestTimeout;

            HTTPResponse? waitingForHTTPResponse = null;

            if (networkingTask is not null)
                return new Tuple<WebSocketClientConnection, HTTPResponse>(
                           webSocketClientConnection,
                           waitingForHTTPResponse
                       );

            networkingTask = Task.Run(async () => {

                do
                {

                    HTTPRequest?  httpRequest   = null;
                    HTTPResponse? httpResponse  = null;

                    try
                    {

                        await OpenTCPConnection(RequestTimeout);

                        var responseTuple     = await SendHTTPRequest(
                                                          HTTPRequestBuilder,
                                                          HTTPAuthentication,
                                                          CancellationToken
                                                      );

                        httpRequest           = responseTuple.Item1;
                        var expectedWSAccept  = responseTuple.Item2;

                        httpResponse          = await WaitForHTTPResponse(httpRequest, CancellationToken);

                        #region Unauthorized? Maybe a "WWW-Authenticate Challenge"?

                        if (httpResponse.HTTPStatusCode == HTTPStatusCode.Unauthorized)
                        {

                            // WWW-Authenticate: Basic     realm     = "Restricted Area"

                            // WWW-Authenticate: Digest    realm     = "example.com",
                            //                             qop       = "auth",
                            //                             nonce     = "dcd98b7102dd2f0e8b11d0f600bfb0c093",
                            //                             opaque    = "5ccc069c403ebaf9f0171e9517f40e41"

                            // WWW-Authenticate: Challenge realm     = "charging.cloud",
                            //                             keyId     = "94g84hg...",
                            //                             hash      = "sha256",
                            //                             algorithm = "ECDSA",
                            //                             nonce     = "dcd98b7102dd2f0e8b11d0f600bfb0c093",
                            //                             opaque    = "5ccc069c403ebaf9f0171e9517f40e41"
                            if (httpResponse.WWWAuthenticate?.Method == "Challenge" &&
                                AuthKey is not null)
                            {

                                var keyId       = httpResponse.WWWAuthenticate.GetParameter("keyId");
                                var hash        = httpResponse.WWWAuthenticate.GetParameter("hash");
                                var algorithm   = httpResponse.WWWAuthenticate.GetParameter("algorithm");
                                var nonce       = httpResponse.WWWAuthenticate.GetParameter("nonce");
                                var opaque      = httpResponse.WWWAuthenticate.GetParameter("opaque");

                                var plainText   = $"{nonce}{opaque}";

                                var hashValue   = hash switch {
                                                      "sha512" => SHA512.HashData(plainText.ToUTF8Bytes()),
                                                      "sha384" => SHA384.HashData(plainText.ToUTF8Bytes()),
                                                      "sha256" => SHA256.HashData(plainText.ToUTF8Bytes()),
                                                      _        => throw new Exception($"Unknown hash method '{hash}' in WWW-Authenticate challenge!")
                                                  };

                                var blockSize   = hash switch {
                                                      "sha512" => 64,
                                                      "sha384" => 48,
                                                      "sha256" => 32,
                                                      _        => throw new Exception($"Unknown hash method '{hash}' in WWW-Authenticate challenge!")
                                                  };

                                var signer      = algorithm switch {
                                                      "sha256" => SignerUtilities.GetSigner("NONEwithECDSA"),
                                                      _        => throw new Exception($"Unknown algorithm '{algorithm}' in WWW-Authenticate challenge!")
                                                  };

                                signer.Init(true, AuthKey);
                                signer.BlockUpdate(hashValue, 0, blockSize);
                                var signature   = signer.GenerateSignature().ToBase64();

                                //ToDo: Reconnect as the http server might have closed the connection!
                                //ToDo: 2. Request
                                //ToDo: 2. Response processing

                            }

                        }

                        #endregion

                        #region Post auth...

                        // HTTP/1.1 101 Switching Protocols
                        // Upgrade:                 websocket
                        // Connection:              Upgrade
                        // Sec-WebSocket-Accept:    s3pPLMBiTxaQ9kYGzzhZRbK+xOo=
                        // Sec-WebSocket-Protocol:  ocpp1.6

                        // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                        // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                        // 3. Compute SHA-1 and Base64 hash of the new value
                        // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                        //var swk             = WSConnection.GetHTTPHeader("Sec-WebSocket-Key");
                        //var swka            = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                        //var swkaSha1        = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                        //var swkaSha1Base64  = Convert.ToBase64String(swkaSha1);

                        var webSocketUpgradeAccepted = true;

                        if (101 != httpResponse.HTTPStatusCode.Code) {
                            ClientCloseMessage  = $"Invalid HTTP StatusCode response: 101 != {httpResponse.HTTPStatusCode.Code}!";
                            networkingCancellationTokenSource.Cancel();
                            webSocketUpgradeAccepted = false;
                        }

                        else if (expectedWSAccept != httpResponse.SecWebSocketAccept) {
                            ClientCloseMessage  = $"Invalid HTTP Sec-WebSocket-Accept response: {expectedWSAccept} != {httpResponse.SecWebSocketAccept}!";
                            networkingCancellationTokenSource.Cancel();
                            webSocketUpgradeAccepted = false;
                        }

                        if (!webSocketUpgradeAccepted)
                            waitingForHTTPResponse = httpResponse;

                        #endregion


                        #region Receive WebSocket frames...

                        var buffer  = new Byte[16 * 1024];
                        var pos     = 0U;

                        if (webSocketUpgradeAccepted &&
                            tcpClient?.Client is not null &&
                            HTTPStream        is not null)
                        {

                            var tcpStream = tcpClient.GetStream();

                            webSocketClientConnection = new WebSocketClientConnection(
                                                            this,
                                                            tcpClient.Client,
                                                            tcpStream,
                                                            HTTPStream,
                                                            httpRequest,
                                                            httpResponse,
                                                            CustomData:                  null,
                                                            SlowNetworkSimulationDelay:  null
                                                        );

                            waitingForHTTPResponse = httpResponse;

                            WebSocketFrame.Opcodes? fragmentOpcode  = null;
                            var                     fragmentPayload = new MemoryStream();

                            do
                            {

                                if (webSocketClientConnection?.DataAvailable == true)
                                {

                                    buffer = [];
                                    pos    = 0;

                                    do
                                    {

                                        var buffer2 = new Byte[buffer.Length + (tcpClient?.Available ?? 0)];

                                        do
                                        {

                                            var read = webSocketClientConnection.Read(buffer2, 0, buffer2.Length);

                                            if (read > 0)
                                            {
                                                Array.Resize(ref buffer, (Int32) (pos + read));
                                                Array.Copy(buffer2, 0, buffer, pos, read);
                                                pos += read;
                                            }

                                            //if (sw.ElapsedMilliseconds >= RequestTimeout.Value.TotalMilliseconds)
                                            //    throw new HTTPTimeoutException(sw.Elapsed);

                                            await Task.Delay(1);

                                        } while (webSocketClientConnection.DataAvailable);

                                        Array.Resize(ref buffer, (Int32) pos);

                                        if (WebSocketFrame.TryParse(buffer.AsSpan(0, (Int32) pos),
                                                                    out var frame,
                                                                    out var frameLength,
                                                                    out var errorResponse,
                                                                    Logger: Logger))
                                        {

                                            switch (frame.Opcode)
                                            {

                                                #region Text

                                                case WebSocketFrame.Opcodes.Text:

                                                    if (frame.IsFinal && fragmentOpcode is null)
                                                    {

                                                        await LogEvent(
                                                                    OnTextMessageReceived,
                                                                    loggingDelegate => loggingDelegate.Invoke(
                                                                        Timestamp.Now,
                                                                        this,
                                                                        webSocketClientConnection,
                                                                        frame,
                                                                        EventTracking_Id.New,
                                                                        frame.Payload.ToUTF8String(),
                                                                        CancellationToken
                                                                    )
                                                                );

                                                    }
                                                    else
                                                    {
                                                        fragmentOpcode = WebSocketFrame.Opcodes.Text;
                                                        fragmentPayload.SetLength(0);
                                                        fragmentPayload.Write(frame.Payload, 0, frame.Payload.Length);
                                                    }

                                                break;

                                                #endregion

                                                #region Binary

                                                case WebSocketFrame.Opcodes.Binary:

                                                    if (frame.IsFinal && fragmentOpcode is null)
                                                    {

                                                        await LogEvent(
                                                                    OnBinaryMessageReceived,
                                                                    loggingDelegate => loggingDelegate.Invoke(
                                                                        Timestamp.Now,
                                                                        this,
                                                                        webSocketClientConnection,
                                                                        frame,
                                                                        EventTracking_Id.New,
                                                                        frame.Payload,
                                                                        CancellationToken
                                                                    )
                                                                );

                                                    }
                                                    else
                                                    {
                                                        fragmentOpcode = WebSocketFrame.Opcodes.Binary;
                                                        fragmentPayload.SetLength(0);
                                                        fragmentPayload.Write(frame.Payload, 0, frame.Payload.Length);
                                                    }

                                                break;

                                                #endregion

                                                #region Continuation

                                                case WebSocketFrame.Opcodes.Continuation:

                                                    if (fragmentOpcode is not null)
                                                    {

                                                        fragmentPayload.Write(frame.Payload, 0, frame.Payload.Length);

                                                        if (frame.IsFinal)
                                                        {

                                                            var completePayload = fragmentPayload.ToArray();

                                                            if (fragmentOpcode == WebSocketFrame.Opcodes.Text)
                                                            {

                                                                await LogEvent(
                                                                            OnTextMessageReceived,
                                                                            loggingDelegate => loggingDelegate.Invoke(
                                                                                Timestamp.Now,
                                                                                this,
                                                                                webSocketClientConnection,
                                                                                frame,
                                                                                EventTracking_Id.New,
                                                                                completePayload.ToUTF8String(),
                                                                                CancellationToken
                                                                            )
                                                                        );

                                                            }
                                                            else if (fragmentOpcode == WebSocketFrame.Opcodes.Binary)
                                                            {

                                                                await LogEvent(
                                                                            OnBinaryMessageReceived,
                                                                            loggingDelegate => loggingDelegate.Invoke(
                                                                                Timestamp.Now,
                                                                                this,
                                                                                webSocketClientConnection,
                                                                                frame,
                                                                                EventTracking_Id.New,
                                                                                completePayload,
                                                                                CancellationToken
                                                                            )
                                                                        );

                                                            }

                                                            fragmentOpcode = null;
                                                            fragmentPayload.SetLength(0);

                                                        }

                                                    }
                                                    else
                                                    {
                                                        Logger.LogWarning("Received Continuation frame without preceding Text/Binary frame.");
                                                        if (CloseConnectionOnUnexpectedFrames)
                                                            await webSocketClientConnection.Close(WebSocketFrame.ClosingStatusCode.ProtocolError);
                                                    }

                                                break;

                                                #endregion

                                                #region Ping

                                                case WebSocketFrame.Opcodes.Ping:

                                                    Logger.LogTrace(
                                                        "HTTP WebSocket client {Client} received Ping frame: {Payload}",
                                                        Description?.FirstText() ?? RemoteURL.ToString(),
                                                        frame.Payload.ToUTF8String()
                                                    );

                                                    #region OnPingMessageReceived

                                                    await LogEvent(
                                                                OnPingMessageReceived,
                                                                loggingDelegate => loggingDelegate.Invoke(
                                                                    Timestamp.Now,
                                                                    this,
                                                                    webSocketClientConnection,
                                                                    frame,
                                                                    EventTracking_Id.New,
                                                                    frame.Payload,
                                                                    CancellationToken
                                                                )
                                                            );

                                                    #endregion

                                                    #region Send Pong

                                                    var sentStatus = await SendWebSocketFrame(
                                                                                WebSocketFrame.Pong(
                                                                                    frame.Payload,
                                                                                    WebSocketFrame.Fin.Final,
                                                                                    WebSocketFrame.MaskStatus.On,
                                                                                    RandomExtensions.RandomBytes(4)
                                                                                ),
                                                                                EventTracking_Id.New,
                                                                                CancellationToken
                                                                            );

                                                    if (sentStatus == SentStatus.Success)
                                                    { }
                                                    else if (sentStatus == SentStatus.FatalError)
                                                    {
                                                        await webSocketClientConnection.Close(
                                                                    WebSocketFrame.ClosingStatusCode.ProtocolError
                                                                );
                                                    }
                                                    else
                                                        Logger.LogWarning(
                                                            "HTTP WebSocket client {Client} sending Pong frame failed with {SentStatus}.",
                                                            Description?.FirstText() ?? RemoteURL.ToString(),
                                                            sentStatus
                                                        );

                                                    #endregion

                                                    break;

                                                #endregion

                                                #region Pong

                                                case WebSocketFrame.Opcodes.Pong:

                                                    Logger.LogTrace(
                                                        "HTTP WebSocket client {Client} received Pong frame: {Payload}",
                                                        Description?.FirstText() ?? RemoteURL.ToString(),
                                                        frame.Payload.ToUTF8String()
                                                    );

                                                    #region OnPongMessageReceived

                                                    await LogEvent(
                                                                OnPongMessageReceived,
                                                                loggingDelegate => loggingDelegate.Invoke(
                                                                    Timestamp.Now,
                                                                    this,
                                                                    webSocketClientConnection,
                                                                    frame,
                                                                    EventTracking_Id.New,
                                                                    frame.Payload,
                                                                    CancellationToken
                                                                )
                                                            );

                                                    #endregion

                                                break;

                                                #endregion

                                                #region Close

                                                case WebSocketFrame.Opcodes.Close:

                                                    await LogEvent(
                                                                OnCloseMessageReceived,
                                                                loggingDelegate => loggingDelegate.Invoke(
                                                                    Timestamp.Now,
                                                                    this,
                                                                    webSocketClientConnection,
                                                                    frame,
                                                                    EventTracking_Id.New,
                                                                    frame.GetClosingStatusCode(),
                                                                    frame.GetClosingReason(),
                                                                    CancellationToken
                                                                )
                                                            );

                                                    // The close handshake demands that we send a close frame back!
                                                    await webSocketClientConnection.Close(
                                                              WebSocketFrame.ClosingStatusCode.NormalClosure
                                                          );

                                                    break;

                                                #endregion

                                                #region ...unknown/unexpected

                                                default:

                                                    Logger.LogWarning("Received unknown WebSocket frame opcode {Opcode}.", frame.Opcode);

                                                    if (CloseConnectionOnUnexpectedFrames)
                                                        await webSocketClientConnection.Close(WebSocketFrame.ClosingStatusCode.ProtocolError);

                                                break;

                                                #endregion

                                            }

                                            if ((UInt64) buffer.Length > frameLength)
                                            {
                                                var newBuffer = new Byte[(UInt64) buffer.Length - frameLength];
                                                Array.Copy(buffer, (UInt32) frameLength, newBuffer, 0, newBuffer.Length);
                                                buffer = newBuffer;
                                                pos    = (UInt32) buffer.Length;
                                            }
                                            else
                                                buffer = null;

                                        }

                                    } while (buffer is not null);

                                }
                                else
                                    await Task.Delay(10);

                            }
                            while (!networkingCancellationToken.IsCancellationRequested && ClientCloseMessage is null);

                        }
                        else
                            waitingForHTTPResponse ??= httpResponse;

                        #endregion


                        #region Close connection if requested!

                        if (httpResponse.Connection is null                 ||
                            httpResponse.Connection == ConnectionType.Close ||
                            ClientCloseMessage is not null)
                        {

                            await base.Close().ConfigureAwait(false);
                            HTTPStream = null;

                        }

                        #endregion

                    }

                    #region Catch...

                    catch (HTTPTimeoutException hte)
                    {

                        #region Create a HTTP response for the exception...

                        httpResponse = new HTTPResponse.Builder(httpRequest) {
                                            HTTPStatusCode  = HTTPStatusCode.RequestTimeout,
                                            ContentType     = HTTPContentType.Application.JSON_UTF8,
                                            Content         = JSONObject.Create(
                                                                    new JProperty("timeout",     (Int32) hte.Timeout.TotalMilliseconds),
                                                                    new JProperty("message",     hte.Message),
                                                                    new JProperty("stackTrace",  hte.StackTrace)
                                                                ).ToUTF8Bytes()
                                        };

                        #endregion

                        await base.Close().ConfigureAwait(false);
                        HTTPStream = null;

                    }
                    catch (Exception e)
                    {

                        #region Create a HTTP response for the exception...

                        while (e.InnerException is not null)
                            e = e.InnerException;

                        httpResponse = new HTTPResponse.Builder(httpRequest) {
                                            HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                            ContentType     = HTTPContentType.Application.JSON_UTF8,
                                            Content         = JSONObject.Create(
                                                                    new JProperty("message",     e.Message),
                                                                    new JProperty("stackTrace",  e.StackTrace)
                                                                ).ToUTF8Bytes()
                                        };

                        #endregion

                        await base.Close().ConfigureAwait(false);
                        HTTPStream = null;

                    }

                    #endregion

                    #region Call the optional HTTP response log delegate

                    try
                    {

                        if (ResponseLogDelegate is not null)
                            await Task.WhenAll(ResponseLogDelegate.GetInvocationList().
                                                Cast<ClientResponseLogHandler>().
                                                Select(e => e(Timestamp.Now,
                                                              this,
                                                              httpRequest,
                                                              httpResponse))).
                                                ConfigureAwait(false);

                    }
                    catch (Exception e2)
                    {
                        Logger.LogError(e2, "Exception while invoking {EventName}.", nameof(ResponseLogDelegate));
                    }

                    #endregion

                }
                while (!networkingCancellationToken.IsCancellationRequested && ClientCloseMessage is null);

            }, CancellationToken);

            var ts = Timestamp.Now;

            while (waitingForHTTPResponse is null && ts + RequestTimeout > Timestamp.Now) {
                await Task.Delay(10, CancellationToken);
            }

            waitingForHTTPResponse ??= new HTTPResponse.Builder(

                                           Timestamp.Now,
                                           EventTracking_Id.New,
                                           TimeSpan.Zero,

                                           new HTTPSource(),
                                           webSocketClientConnection?.LocalSocket  ?? IPSocket.Zero,
                                           webSocketClientConnection?.RemoteSocket ?? IPSocket.Zero,
                                           ConnectionType.Close,

                                           HTTPStatusCode.BadRequest,
                                           $"Timeout of {RequestTimeout.Value.TotalSeconds} seconds reached!!!"

                                       ).AsImmutable;

            return new Tuple<WebSocketClientConnection, HTTPResponse>(
                       webSocketClientConnection,
                       waitingForHTTPResponse
                   );

        }

        #endregion

        #region Disconnect()

        public void Disconnect()
        {
            networkingCancellationTokenSource.Cancel();
        }

        #endregion


        #region (Timer) DoWebSocketPing(State)

        private UInt64 pingCounter;

        private async void DoWebSocketPingSync(Object? State)
        {
            if (!DisableWebSocketPings)
                await DoWebSocketPing(State);
        }

        private async Task DoWebSocketPing(Object? State)
        {

            if (await MaintenanceSemaphore.WaitAsync(SemaphoreSlimTimeout).
                                           ConfigureAwait(false))
            {
                try
                {

                    var tokenSource = new CancellationTokenSource();

                    if (HTTPStream is not null)
                    {

                        pingCounter++;

                        await SendWebSocketFrame(
                                  WebSocketFrame.Ping(
                                      $"{pingCounter}:{Description?.FirstText() ?? RemoteURL.ToString()}:{UUIDv7.Generate()}".ToUTF8Bytes(),
                                      WebSocketFrame.Fin.Final,
                                      WebSocketFrame.MaskStatus.On,
                                      RandomExtensions.RandomBytes(4)
                                  ),
                                  EventTracking_Id.New,
                                  tokenSource.Token
                              );

                    }

                }
                catch (ObjectDisposedException)
                {
                    WebSocketPingTimer.Dispose();
                }
                catch (Exception e)
                {

                    while (e.InnerException is not null)
                        e = e.InnerException;

                    Logger.LogError(e, "Exception within the HTTP WebSocket ping task.");

                }
                finally
                {
                    MaintenanceSemaphore.Release();
                }
            }
            else
                Logger.LogWarning("Could not acquire the HTTP WebSocket ping task lock.");

        }

        #endregion

        #region (Timer) DoMaintenance(State)

        private async void DoMaintenanceSync(Object? State)
        {
            if (!DisableMaintenanceTasks)
                await DoMaintenanceAsync(State);
        }

        private async Task DoMaintenanceAsync(Object? State)
        {

            if (await MaintenanceSemaphore.WaitAsync(SemaphoreSlimTimeout).
                                           ConfigureAwait(false))
            {
                try
                {

                    await DoMaintenanceAsyncStep2(State);

                }
                catch (ObjectDisposedException)
                {
                    MaintenanceTimer.Dispose();
                    HTTPStream = null;
                }
                catch (Exception e)
                {

                    while (e.InnerException is not null)
                        e = e.InnerException;

                    Logger.LogError(e, "Exception within the HTTP WebSocket maintenance task.");

                }
                finally
                {
                    MaintenanceSemaphore.Release();
                }
            }
            else
                Logger.LogWarning(
                    "HTTP WebSocket client {Client} could not acquire the maintenance tasks lock.",
                    Description.IsNotNullOrEmpty() ? Description.FirstText() : RemoteURL
                );

        }

        protected internal virtual Task DoMaintenanceAsyncStep2(Object? State)
        {

            return Task.CompletedTask;

        }

        #endregion


        #region SendWebSocketFrame (WebSocketFrame, ...)

        public async Task<SentStatus> SendWebSocketFrame(WebSocketFrame     WebSocketFrame,
                                                         EventTracking_Id?  EventTrackingId     = null,
                                                         CancellationToken  CancellationToken   = default)
        {

            if (webSocketClientConnection is null)
                return SentStatus.FatalError;

            var eventTrackingId  = EventTrackingId ?? EventTracking_Id.New;

            var sentStatus       = await webSocketClientConnection.SendWebSocketFrame(
                                             WebSocketFrame,
                                             CancellationToken
                                         );

            #region OnTextMessageSent

            if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Text)
            {

                await LogEvent(
                          OnTextMessageSent,
                          loggingDelegate => loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              webSocketClientConnection,
                              WebSocketFrame,
                              eventTrackingId,
                              WebSocketFrame.Payload.ToUTF8String(),
                              sentStatus,
                              CancellationToken
                          )
                      );

            }

            #endregion

            #region OnBinaryMessageSent

            else if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Binary)
            {

                await LogEvent(
                          OnBinaryMessageSent,
                          loggingDelegate => loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              webSocketClientConnection,
                              WebSocketFrame,
                              eventTrackingId,
                              WebSocketFrame.Payload,
                              sentStatus,
                              CancellationToken
                          )
                      );

            }

            #endregion

            #region OnPingMessageSent

            else if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Ping)
            {

                Logger.LogTrace(
                    "HTTP WebSocket client {Client} sent Ping frame: {Payload} => {SentStatus}",
                    Description?.FirstText() ?? RemoteURL.ToString(),
                    WebSocketFrame.Payload.ToUTF8String(),
                    sentStatus
                );

                await LogEvent(
                          OnPingMessageSent,
                          loggingDelegate => loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              webSocketClientConnection,
                              WebSocketFrame,
                              eventTrackingId,
                              WebSocketFrame.Payload,
                              sentStatus,
                              CancellationToken
                          )
                      );

            }

            #endregion

            #region OnPongMessageSent

            else if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Pong)
            {

                Logger.LogTrace(
                    "HTTP WebSocket client {Client} sent Pong frame: {Payload} => {SentStatus}",
                    Description?.FirstText() ?? RemoteURL.ToString(),
                    WebSocketFrame.Payload.ToUTF8String(),
                    sentStatus
                );

                await LogEvent(
                          OnPongMessageSent,
                          loggingDelegate => loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              webSocketClientConnection,
                              WebSocketFrame,
                              eventTrackingId,
                              WebSocketFrame.Payload,
                              sentStatus,
                              CancellationToken
                          )
                      );

            }

            #endregion

            #region OnCloseMessageSent

            else if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Close)
            {

                Logger.LogDebug(
                    "HTTP WebSocket client {Client} sent Close frame: {StatusCode}, {Reason} => {SentStatus}",
                    Description?.FirstText() ?? RemoteURL.ToString(),
                    WebSocketFrame.GetClosingStatusCode(),
                    WebSocketFrame.GetClosingReason() ?? "",
                    sentStatus
                );

                await LogEvent(
                          OnCloseMessageSent,
                          loggingDelegate => loggingDelegate.Invoke(
                              Timestamp.Now,
                              this,
                              webSocketClientConnection,
                              WebSocketFrame,
                              eventTrackingId,
                              WebSocketFrame.GetClosingStatusCode(),
                              WebSocketFrame.GetClosingReason(),
                              sentStatus,
                              CancellationToken
                          )
                      );

            }

            #endregion


            return sentStatus;

        }

        #endregion

        #region SendTextMessage    (Text,           ...)

        /// <summary>
        /// Send a web socket text frame
        /// </summary>
        /// <param name="Text">The text to send.</param>
        public Task<SentStatus> SendTextMessage(String             Text,
                                                EventTracking_Id?  EventTrackingId     = null,
                                                CancellationToken  CancellationToken   = default)

            => SendWebSocketFrame(
                   WebSocketFrame.Text(
                       Text,
                       WebSocketFrame.Fin.Final,
                       WebSocketFrame.MaskStatus.On,
                       RandomExtensions.RandomBytes(4)
                   ),
                   EventTrackingId,
                   CancellationToken
               );

        #endregion

        #region SendBinaryMessage  (Bytes,          ...)

        /// <summary>
        /// Send a web socket binary frame
        /// </summary>
        /// <param name="Bytes">The array of bytes to send.</param>
        public Task<SentStatus> SendBinaryMessage(Byte[]             Bytes,
                                                  EventTracking_Id?  EventTrackingId     = null,
                                                  CancellationToken  CancellationToken   = default)

            => SendWebSocketFrame(
                   WebSocketFrame.Binary(
                       Bytes,
                       WebSocketFrame.Fin.Final,
                       WebSocketFrame.MaskStatus.On,
                       RandomExtensions.RandomBytes(4)
                   ),
                   EventTrackingId,
                   CancellationToken
               );

        #endregion


        #region Close(StatusCode = Normal, Reason = null)

        /// <summary>
        /// Close the connection.
        /// </summary>
        /// <param name="StatusCode">An optional status code for closing.</param>
        /// <param name="Reason">An optional reason for closing.</param>
        public async Task Close(WebSocketFrame.ClosingStatusCode  StatusCode          = WebSocketFrame.ClosingStatusCode.NormalClosure,
                                String?                           Reason              = null,
                                EventTracking_Id?                 EventTrackingId     = null,
                                CancellationToken                 CancellationToken   = default)
        {

            try
            {
                networkingCancellationTokenSource.Cancel();
            }
            catch
            { }

            try
            {
                WebSocketPingTimer.Dispose();
                MaintenanceTimer.  Dispose();
            }
            catch
            { }

            try
            {
                if (HTTPStream is not null)
                {

                    await SendWebSocketFrame(
                              WebSocketFrame.Close(
                                  StatusCode,
                                  Reason,
                                  WebSocketFrame.Fin.Final,
                                  WebSocketFrame.MaskStatus.On,
                                  RandomExtensions.RandomBytes(4)
                              ),
                              EventTrackingId ?? EventTracking_Id.New,
                              CancellationToken
                          );

                }
            }
            catch
            { }

            try
            {
                HTTPStream = null;
                await base.Close().ConfigureAwait(false);
            }
            catch
            { }

        }

        #endregion


        #region (private)   LogEvent     (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                         [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

                => LogEvent(
                       nameof(WebSocketClient),
                       Logger,
                       LogHandler,
                       EventName,
                       Command
                   );

        #endregion

        #region (private)   HandleErrors (Caller, ErrorResponse)

        private Task HandleErrors(String  Caller,
                                  String  ErrorResponse)

            => HandleErrors(
                   nameof(WebSocketClient),
                   Caller,
                   ErrorResponse
               );

        #endregion

        #region (private)   HandleErrors (Caller, ExceptionOccurred)

        private Task HandleErrors(String     Caller,
                                  Exception  ExceptionOccurred)

            => HandleErrors(
                   nameof(WebSocketClient),
                   Caller,
                   ExceptionOccurred
               );

        #endregion


        #region (protected) LogEvent     (Caller, Logger, LogHandler, ...)

        protected new async Task LogEvent<TDelegate>(String                                             Caller,
                                                     TDelegate?                                         Logger,
                                                     Func<TDelegate, Task>                              LogHandler,
                                                     [CallerArgumentExpression(nameof(Logger))] String  EventName   = "",
                                                     [CallerMemberName()]                       String  Command     = "")

            where TDelegate : Delegate

        {
            if (Logger is not null)
            {
                try
                {

                    await Task.WhenAll(
                              Logger.GetInvocationList().
                                     OfType<TDelegate>().
                                     Select(LogHandler)
                          );

                }
                catch (Exception e)
                {
                    await HandleErrors($"{Caller}: {Command}.{EventName}", e);
                }
            }
        }

        #endregion

        #region (protected) HandleErrors (Module, Caller, ErrorResponse)

        public override Task HandleErrors(String  Module,
                                          String  Caller,
                                          String  ErrorResponse)
        {

            Logger.LogError("{Module}.{Caller}: {ErrorResponse}", Module, Caller, ErrorResponse);

            return Task.CompletedTask;

        }

        #endregion

        #region (protected) HandleErrors (Module, Caller, ExceptionOccurred)

        public override Task HandleErrors(String     Module,
                                          String     Caller,
                                          Exception  ExceptionOccurred)
        {

            Logger.LogError(ExceptionOccurred, "{Module}.{Caller}", Module, Caller);

            return Task.CompletedTask;

        }

        #endregion


        #region Dispose()

        /// <summary>
        /// Dispose this object.
        /// </summary>
        public override void Dispose()
        {
            Close().GetAwaiter().GetResult();
        }

        #endregion

    }

}
