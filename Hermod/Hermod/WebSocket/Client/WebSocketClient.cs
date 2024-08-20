/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Security.Authentication;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Logging;
using System.Runtime.CompilerServices;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Reflection;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    #region Delegates

    public delegate Task  OnWebSocketClientFrameSentDelegate             (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          WebSocketFrame                     Frame,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientFrameReceivedDelegate         (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          WebSocketFrame                     Frame,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientTextMessageSentDelegate       (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          String                             TextMessage,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientTextMessageReceivedDelegate   (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          String                             TextMessage,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientBinaryMessageSentDelegate     (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             BinaryMessage,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientBinaryMessageReceivedDelegate (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             BinaryMessage,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientPingMessageSentDelegate       (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             PingMessage,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientPingMessageReceivedDelegate   (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             PingMessage,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientPongMessageSentDelegate       (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             PongMessage,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientPongMessageReceivedDelegate   (DateTime                           Timestamp,
                                                                          WebSocketClient                    Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          Byte[]                             PongMessage,
                                                                          CancellationToken                  CancellationToken);


    public delegate Task  OnWebSocketClientCloseMessageSentDelegate      (DateTime                           Timestamp,
                                                                          IWebSocketClient                   Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          WebSocketFrame.ClosingStatusCode   StatusCode,
                                                                          String?                            Reason,
                                                                          SentStatus                         SentStatus,
                                                                          CancellationToken                  CancellationToken);

    public delegate Task  OnWebSocketClientCloseMessageReceivedDelegate  (DateTime                           Timestamp,
                                                                          IWebSocketClient                   Client,
                                                                          WebSocketClientConnection          Connection,
                                                                          WebSocketFrame                     Frame,
                                                                          EventTracking_Id                   EventTrackingId,
                                                                          WebSocketFrame.ClosingStatusCode   StatusCode,
                                                                          String?                            Reason,
                                                                          CancellationToken                  CancellationToken);

    #endregion


    public interface IWebSocketClient : IHTTPClient
    {

        /// <summary>
        /// An event sent whenever a text message was sent.
        /// </summary>
        event OnWebSocketClientTextMessageSentDelegate?         OnTextMessageSent;

        /// <summary>
        /// An event sent whenever a text message was received.
        /// </summary>
        event OnWebSocketClientTextMessageReceivedDelegate?     OnTextMessageReceived;


        /// <summary>
        /// An event sent whenever a binary message was sent.
        /// </summary>
        event OnWebSocketClientBinaryMessageSentDelegate?       OnBinaryMessageSent;

        /// <summary>
        /// An event sent whenever a binary message was received.
        /// </summary>
        event OnWebSocketClientBinaryMessageReceivedDelegate?   OnBinaryMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket ping frame was sent.
        /// </summary>
        event OnWebSocketClientPingMessageSentDelegate?         OnPingMessageSent;

        /// <summary>
        /// An event sent whenever a web socket ping frame was received.
        /// </summary>
        event OnWebSocketClientPingMessageReceivedDelegate?     OnPingMessageReceived;


        /// <summary>
        /// An event sent whenever a web socket pong frame was sent.
        /// </summary>
        event OnWebSocketClientPongMessageSentDelegate?         OnPongMessageSent;

        /// <summary>
        /// An event sent whenever a web socket pong frame was received.
        /// </summary>
        event OnWebSocketClientPongMessageReceivedDelegate?     OnPongMessageReceived;


        /// <summary>
        /// An event sent whenever a HTTP Web Socket CLOSE frame was sent.
        /// </summary>
        event OnWebSocketClientCloseMessageSentDelegate?        OnCloseMessageSent;

        /// <summary>
        /// An event sent whenever a HTTP Web Socket CLOSE frame was received.
        /// </summary>
        event OnWebSocketClientCloseMessageReceivedDelegate     OnCloseMessageReceived;


        new RemoteTLSServerCertificateValidationHandler<IWebSocketClient>? RemoteCertificateValidator { get; }

    }


    /// <summary>
    /// A HTTP web socket client.
    /// </summary>
    public class WebSocketClient : IWebSocketClient
    {

        #region Data

        /// <summary>
        /// The default HTTP user agent string.
        /// </summary>
        public const           String  DefaultHTTPUserAgent  = "GraphDefined HTTP Web Socket Client";

        /// <summary>
        /// The default remote TCP port to connect to.
        /// </summary>
        public static readonly IPPort  DefaultRemotePort     = IPPort.Parse(443);


        private   Socket?           TCPSocket;
        private   MyNetworkStream?  TCPNetworkStream;
        private   SslStream?        TLSStream;
        protected Stream?           HTTPStream;

        /// <summary>
        /// The default maintenance interval.
        /// </summary>
        public           readonly TimeSpan                 DefaultMaintenanceEvery     = TimeSpan.FromSeconds(1);
        private          readonly Timer                    MaintenanceTimer;

        protected static readonly SemaphoreSlim            MaintenanceSemaphore        = new(1, 1);

        public           readonly TimeSpan                 DefaultWebSocketPingEvery   = TimeSpan.FromSeconds(30);

        private          readonly Timer                    WebSocketPingTimer;

        protected static readonly TimeSpan                 SemaphoreSlimTimeout        = TimeSpan.FromSeconds(5);

        private const             String                   LogfileName                 = "WebSocketClient.log";

        private readonly          CancellationTokenSource  networkingCancellationTokenSource;
        private readonly          CancellationToken        networkingCancellationToken;
        private                   Task                     networkingThread;


        protected WebSocketClientConnection webSocketClientConnection;

        #endregion

        #region Properties

        /// <summary>
        /// The attached OCPP CP client (HTTP/websocket client) logger.
        /// </summary>
       // public WebSocketClient.CPClientLogger    Logger                          { get; }



        /// <summary>
        /// The remote URL of the HTTP endpoint to connect to.
        /// </summary>
        public URL                                                             RemoteURL                                 { get; }

        /// <summary>
        /// The virtual HTTP hostname to connect to.
        /// </summary>
        public HTTPHostname?                                                   VirtualHostname                           { get; }

        /// <summary>
        /// An optional description of this HTTP Web Socket client.
        /// </summary>
        public I18NString                                                      Description                               { get; set; }

        /// <summary>
        /// The remote TLS certificate validator.
        /// </summary>
        RemoteTLSServerCertificateValidationHandler<IHTTPClient>?              IHTTPClient.RemoteCertificateValidator    { get; }

        /// <summary>
        /// The remote TLS certificate validator.
        /// </summary>
        public RemoteTLSServerCertificateValidationHandler<IWebSocketClient>?  RemoteCertificateValidator                { get; private set; }

        /// <summary>
        /// A delegate to select a TLS client certificate.
        /// </summary>
        public LocalCertificateSelectionHandler?                               LocalCertificateSelector                  { get; }

        /// <summary>
        /// The TLS client certificate to use of HTTP authentication.
        /// </summary>
        public X509Certificate?                                                ClientCert                                { get; }

        /// <summary>
        /// The TLS protocol to use.
        /// </summary>
        public SslProtocols                                                    TLSProtocol                               { get; }

        /// <summary>
        /// Prefer IPv4 instead of IPv6.
        /// </summary>
        public Boolean                                                         PreferIPv4                                { get; }

        /// <summary>
        /// The HTTP user agent identification.
        /// </summary>
        public String                                                          HTTPUserAgent                             { get; }

        /// <summary>
        /// The timeout for upstream requests.
        /// </summary>
        public TimeSpan                                                        RequestTimeout                            { get; set; }

        /// <summary>
        /// The delay between transmission retries.
        /// </summary>
        public TransmissionRetryDelayDelegate                                  TransmissionRetryDelay                    { get; }

        /// <summary>
        /// The maximum number of retries when communicationg with the remote OICP service.
        /// </summary>
        public UInt16                                                          MaxNumberOfRetries                        { get; }

        /// <summary>
        /// Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.
        /// </summary>
        public Boolean                                                         UseHTTPPipelining
            => false;

        /// <summary>
        /// The CPO client (HTTP client) logger.
        /// </summary>
        public HTTPClientLogger?                                               HTTPLogger                                { get; set; }




        /// <summary>
        /// The DNS client defines which DNS servers to use.
        /// </summary>
        public DNSClient?                                                      DNSClient                                 { get; }



        /// <summary>
        /// Our local IP port.
        /// </summary>
        public IPPort                                                          LocalPort                                 { get; private set; }

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        public IIPAddress?                                                     RemoteIPAddress                           { get; protected set; }


        public Int32? Available
                    => TCPSocket?.Available;

        public Boolean Connected
            => TCPSocket?.Connected ?? false;

        [DisallowNull]
        public LingerOption? LingerState
        {
            get
            {
                return TCPSocket?.LingerState;
            }
            set
            {
                if (TCPSocket is not null)
                    TCPSocket.LingerState = value;
            }
        }

        [DisallowNull]
        public Boolean? NoDelay
        {
            get
            {
                return TCPSocket?.NoDelay;
            }
            set
            {
                if (TCPSocket is not null)
                    TCPSocket.NoDelay = value.Value;
            }
        }

        [DisallowNull]
        public Byte TTL
        {
            get
            {
                return (Byte) (TCPSocket?.Ttl ?? 0);
            }
            set
            {
                if (TCPSocket is not null)
                    TCPSocket.Ttl = value;
            }
        }


        public IHTTPAuthentication?                 HTTPAuthentication                   { get; }


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
        /// An event sent whenever a HTTP Web Socket CLOSE frame was sent.
        /// </summary>
        public event OnWebSocketClientCloseMessageSentDelegate?        OnCloseMessageSent;

        /// <summary>
        /// An event sent whenever a HTTP Web Socket CLOSE frame was received.
        /// </summary>
        public event OnWebSocketClientCloseMessageReceivedDelegate     OnCloseMessageReceived;


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

        /// <summary>
        /// Create a new charge point websocket client running on a charge point
        /// and connecting to a central system to invoke methods.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this HTTP/websocket client.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use of HTTP authentication.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use, e.g. HTTP Basic Auth.</param>
        /// <param name="RequestTimeout">An optional Request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="LoggingContext">An optional context for logging client methods.</param>
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public WebSocketClient(URL                                                             RemoteURL,
                               HTTPHostname?                                                   VirtualHostname              = null,
                               I18NString?                                                     Description                  = null,
                               Boolean?                                                        PreferIPv4                   = null,
                               RemoteTLSServerCertificateValidationHandler<IWebSocketClient>?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionHandler?                               LocalCertificateSelector     = null,
                               X509Certificate?                                                ClientCert                   = null,
                               SslProtocols?                                                   TLSProtocol                  = null,
                               String?                                                         HTTPUserAgent                = DefaultHTTPUserAgent,
                               IHTTPAuthentication?                                            HTTPAuthentication           = null,
                               TimeSpan?                                                       RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?                                 TransmissionRetryDelay       = null,
                               UInt16?                                                         MaxNumberOfRetries           = 3,
                               UInt32?                                                         InternalBufferSize           = null,

                               IEnumerable<String>?                                            SecWebSocketProtocols        = null,

                               Boolean                                                         DisableWebSocketPings        = false,
                               TimeSpan?                                                       WebSocketPingEvery           = null,
                               TimeSpan?                                                       SlowNetworkSimulationDelay   = null,

                               Boolean                                                         DisableMaintenanceTasks      = false,
                               TimeSpan?                                                       MaintenanceEvery             = null,

                               String?                                                         LoggingPath                  = null,
                               String                                                          LoggingContext               = "logcontext", //CPClientLogger.DefaultContext,
                               LogfileCreatorDelegate?                                         LogfileCreator               = null,
                               HTTPClientLogger?                                               HTTPLogger                   = null,
                               DNSClient?                                                      DNSClient                    = null)

        {

            this.RemoteURL                          = RemoteURL;
            this.VirtualHostname                    = VirtualHostname;
            this.Description                        = Description             ?? I18NString.Empty;
            this.RemoteCertificateValidator         = RemoteCertificateValidator;
            this.LocalCertificateSelector           = LocalCertificateSelector;
            this.ClientCert                         = ClientCert;
            this.HTTPUserAgent                      = HTTPUserAgent           ?? DefaultHTTPUserAgent;
            this.TLSProtocol                        = TLSProtocol             ?? SslProtocols.Tls12 | SslProtocols.Tls13;
            this.PreferIPv4                         = PreferIPv4              ?? false;
            this.HTTPAuthentication                 = HTTPAuthentication;
            this.RequestTimeout                     = RequestTimeout          ?? TimeSpan.FromMinutes(10);
            this.TransmissionRetryDelay             = TransmissionRetryDelay  ?? (retryCount => TimeSpan.FromSeconds(5));
            this.MaxNumberOfRetries                 = MaxNumberOfRetries      ?? 3;
            this.HTTPLogger                         = HTTPLogger;
            this.DNSClient                          = DNSClient;

            this.SecWebSocketProtocols              = SecWebSocketProtocols   ?? [];

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
            this.networkingCancellationToken        = new CancellationTokenSource().Token;

            //this.Logger                             = new ChargePointwebsocketClient.CPClientLogger(this,
            //                                                                                   LoggingPath,
            //                                                                                   LoggingContext,
            //                                                                                   LogfileCreator);

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
        public Task<Tuple<WebSocketClientConnection, HTTPResponse>>

            Connect(EventTracking_Id?             EventTrackingId      = null,
                    TimeSpan?                     RequestTimeout       = null,
                    UInt16?                       MaxNumberOfRetries   = 0,
                    Action<HTTPRequest.Builder>?  HTTPRequestBuilder   = null,
                    CancellationToken             CancellationToken    = default)

        {

            HTTPResponse? waitingForHTTPResponse = null;

            if (networkingThread is null)
            {

                networkingThread = Task.Run(async () => {

                    do
                    {

                        HTTPRequest?  httpRequest   = null;
                        HTTPResponse? httpResponse  = null;

                        if (!RequestTimeout.HasValue)
                            RequestTimeout = TimeSpan.FromMinutes(10);

                        try
                        {

                            #region Data

                            var HTTPHeaderBytes  = Array.Empty<Byte>();
                            var HTTPBodyBytes    = Array.Empty<Byte>();
                            var sw               = new Stopwatch();

                            #endregion

                            #region Create TCP connection (possibly also do DNS lookups)

                            Boolean restart;

                            do
                            {

                                restart = false;

                                #region Setup TCP socket

                                if (TCPSocket is null)
                                {

                                    System.Net.IPEndPoint? remoteIPEndPoint = null;

                                    if (RemoteIPAddress is null)
                                    {

                                        if      (IPAddress.IsIPv4Localhost(RemoteURL.Hostname))
                                            RemoteIPAddress = IPv4Address.Localhost;

                                        else if (IPAddress.IsIPv6Localhost(RemoteURL.Hostname))
                                            RemoteIPAddress = IPv6Address.Localhost;

                                        else if (IPAddress.IsIPv4(RemoteURL.Hostname.Name))
                                            RemoteIPAddress = IPv4Address.Parse(RemoteURL.Hostname.Name);

                                        else if (IPAddress.IsIPv6(RemoteURL.Hostname.Name))
                                            RemoteIPAddress = IPv6Address.Parse(RemoteURL.Hostname.Name);

                                        #region DNS lookup...

                                        if (RemoteIPAddress is null &&
                                            DNSClient       is not null)
                                        {

                                            var IPv4AddressLookupTask  = DNSClient.
                                                                             Query<A>(RemoteURL.Hostname.Name).
                                                                             ContinueWith(query => query.Result.Select(ARecord    => ARecord.IPv4Address));

                                            var IPv6AddressLookupTask  = DNSClient.
                                                                             Query<AAAA>(RemoteURL.Hostname.Name).
                                                                             ContinueWith(query => query.Result.Select(AAAARecord => AAAARecord.IPv6Address));

                                            await Task.WhenAll(IPv4AddressLookupTask,
                                                               IPv6AddressLookupTask).
                                                       ConfigureAwait(false);


                                            if (IPv4AddressLookupTask.Result.Any())
                                                RemoteIPAddress = IPv4AddressLookupTask.Result.First();

                                            else if (IPv6AddressLookupTask.Result.Any())
                                                RemoteIPAddress = IPv6AddressLookupTask.Result.First();


                                            if (RemoteIPAddress is null || RemoteIPAddress.GetBytes() is null)
                                                throw new Exception("DNS lookup failed!");

                                        }

                                        #endregion

                                    }

                                    remoteIPEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(RemoteIPAddress.GetBytes()),
                                                                                 RemoteURL.Port.Value.ToInt32());

                                    sw.Start();


                                    if (RemoteIPAddress.IsIPv4)
                                        TCPSocket = new Socket(AddressFamily.InterNetwork,
                                                               SocketType.Stream,
                                                               ProtocolType.Tcp);

                                    else if (RemoteIPAddress.IsIPv6)
                                        TCPSocket = new Socket(AddressFamily.InterNetworkV6,
                                                               SocketType.Stream,
                                                               ProtocolType.Tcp);

                                    if (TCPSocket is not null) {
                                        TCPSocket.SendTimeout    = (Int32) RequestTimeout.Value.TotalMilliseconds;
                                        TCPSocket.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;
                                        TCPSocket.Connect(remoteIPEndPoint);
                                        TCPSocket.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;
                                    }

                                }

                                TCPNetworkStream = TCPSocket is not null
                                                ? new MyNetworkStream(TCPSocket, true) {
                                                      ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds
                                                  }
                                                : null;

                                #endregion

                                #region Create (Crypto-)Stream

                                if (RemoteCertificateValidator is null &&
                                   (RemoteURL.Protocol == URLProtocols.wss || RemoteURL.Protocol == URLProtocols.https))
                                {
                                    RemoteCertificateValidator = (sender, certificate, chain, server, sslPolicyErrors) => {
                                        return (true, Array.Empty<String>());
                                    };
                                }

                                if (RemoteURL.Protocol == URLProtocols.https &&
                                    TCPNetworkStream           is not null   &&
                                    RemoteCertificateValidator is not null)
                                {

                                    if (TLSStream is null)
                                    {

                                        var remoteCertificateValidatorErrors = new List<String>();

                                        TLSStream = new SslStream(
                                                        innerStream:                         TCPNetworkStream,
                                                        leaveInnerStreamOpen:                false,
                                                        userCertificateValidationCallback:  (sender,
                                                                                             certificate,
                                                                                             chain,
                                                                                             policyErrors) => {

                                                                                                 var check = RemoteCertificateValidator(
                                                                                                                 sender,
                                                                                                                 certificate is not null
                                                                                                                     ? new X509Certificate2(certificate)
                                                                                                                     : null,
                                                                                                                 chain,
                                                                                                                 null,
                                                                                                                 policyErrors
                                                                                                             );

                                                                                                 if (check.Item2.Any())
                                                                                                     remoteCertificateValidatorErrors.AddRange(check.Item2);

                                                                                                 return check.Item1;

                                                                                             },
                                                        userCertificateSelectionCallback:    LocalCertificateSelector is null
                                                                                                 ? null
                                                                                                 : (sender,
                                                                                                    targetHost,
                                                                                                    localCertificates,
                                                                                                    remoteCertificate,
                                                                                                    acceptableIssuers) => LocalCertificateSelector(
                                                                                                                              sender,
                                                                                                                              targetHost,
                                                                                                                              localCertificates.
                                                                                                                                  Cast<X509Certificate>().
                                                                                                                                  Select(certificate => new X509Certificate2(certificate)),
                                                                                                                              remoteCertificate is not null
                                                                                                                                  ? new X509Certificate2(remoteCertificate)
                                                                                                                                  : null,
                                                                                                                              acceptableIssuers
                                                                                                                          ),
                                                        encryptionPolicy:                    EncryptionPolicy.RequireEncryption
                                                    )
                                        {

                                            ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds

                                        };

                                        HTTPStream = TLSStream;

                                        try
                                        {

                                            await TLSStream.AuthenticateAsClientAsync(RemoteURL.Hostname.Name,
                                                                                      ClientCert is not null
                                                                                          ? new X509CertificateCollection(new X509Certificate[] { ClientCert })
                                                                                          : null,
                                                                                      SslProtocols.Tls12 | SslProtocols.Tls13,
                                                                                      false);

                                        }
                                        catch (Exception e)
                                        {

                                            //timings.AddError($"TLS.AuthenticateAsClientAsync: {e.Message}");

                                            //foreach (var error in remoteCertificateValidatorErrors)
                                            //    timings.AddError(error);

                                            TCPSocket  = null;
                                            restart    = true;

                                        }

                                    }

                                }

                                else
                                {
                                    TLSStream   = null;
                                    HTTPStream  = TCPNetworkStream;
                                }

                                HTTPStream.ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                                #endregion

                            }
                            while (restart);

                            this.LocalPort = (IPSocket.FromIPEndPoint(TCPNetworkStream?.Socket.LocalEndPoint) ?? IPSocket.Zero).Port;

                            #endregion

                            #region Send Request

                            // GET /webServices/ocpp/CP3211 HTTP/1.1
                            // Host:                    some.server.com:33033
                            // Connection:              Upgrade
                            // Upgrade:                 websocket
                            // Sec-WebSocket-Key:       x3JJHMbDL1EzLkh9GBhXDw==
                            // Sec-WebSocket-Protocol:  ocpp2.1, ocpp2.0.1
                            // Sec-WebSocket-Version:   13

                            var swkaSHA1Base64      = RandomExtensions.RandomBytes(16).ToBase64();
                            var expectedWSAccept    = System.Security.Cryptography.SHA1.HashData((swkaSHA1Base64 + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").ToUTF8Bytes()).ToBase64();

                            var httpRequestBuilder  = new HTTPRequest.Builder {
                                                          Path                  = RemoteURL.Path,
                                                          Host                  = HTTPHostname.Parse(String.Concat(RemoteURL.Hostname, ":", RemoteURL.Port)),
                                                          Connection            = "Upgrade",
                                                          Upgrade               = "websocket",
                                                          SecWebSocketKey       = swkaSHA1Base64,
                                                          SecWebSocketProtocol  = SecWebSocketProtocols,
                                                          SecWebSocketVersion   = "13",
                                                          Authorization         = HTTPAuthentication
                                                      };

                            HTTPRequestBuilder?.Invoke(httpRequestBuilder);

                            httpRequest             = httpRequestBuilder.AsImmutable;

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

                            HTTPStream.Write((httpRequest.EntirePDU + "\r\n\r\n").ToUTF8Bytes());

                            HTTPStream.Flush();

                            //File.AppendAllText(LogfileName,
                            //                   String.Concat("Timestamp: ",         Timestamp.Now.ToIso8601(),                                                Environment.NewLine,
                            //                                 "ChargeBoxId: ",       ChargeBoxIdentity.ToString(),                                             Environment.NewLine,
                            //                                 "HTTP request: ",      Environment.NewLine,                                                      Environment.NewLine,
                            //                                 httpRequest.EntirePDU,                                                                           Environment.NewLine,
                            //                                 "--------------------------------------------------------------------------------------------",  Environment.NewLine));

                            #endregion

                            #region Wait for HTTP response

                            var buffer  = new Byte[16 * 1024];
                            var pos     = 0U;

                            do
                            {

                                pos += (UInt32) HTTPStream.Read(buffer, (Int32) pos, 2048);

                                if (sw.ElapsedMilliseconds >= RequestTimeout.Value.TotalMilliseconds)
                                    throw new HTTPTimeoutException(sw.Elapsed);

                                Thread.Sleep(1);

                            } while (TCPNetworkStream.DataAvailable && pos < buffer.Length - 2048);

                            var responseData  = buffer.ToUTF8String(pos);
                            var lines         = responseData.Split('\n').Select(line => line?.Trim()).TakeWhile(line => line.IsNotNullOrEmpty()).ToArray();
                            httpResponse      = HTTPResponse.Parse(
                                                    lines.AggregateWith(Environment.NewLine),
                                                    [],
                                                    httpRequest
                                                );

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

                            if (101 != httpResponse.HTTPStatusCode.Code) {
                                ClientCloseMessage  = $"Invalid HTTP StatusCode response: 101 != {httpResponse.HTTPStatusCode.Code}!";
                                networkingCancellationTokenSource.Cancel();
                            }

                            else if (expectedWSAccept != httpResponse.SecWebSocketAccept) {
                                ClientCloseMessage  = $"Invalid HTTP Sec-WebSocket-Accept response: {expectedWSAccept} != {httpResponse.SecWebSocketAccept}!";
                                networkingCancellationTokenSource.Cancel();
                            }

                            waitingForHTTPResponse = httpResponse;

                            #endregion


                            webSocketClientConnection = new WebSocketClientConnection(
                                                            this,
                                                            TCPSocket,
                                                            TCPNetworkStream,
                                                            HTTPStream,
                                                            httpRequest,
                                                            httpResponse,
                                                            CustomData:                  null,
                                                            SlowNetworkSimulationDelay:  null
                                                        );

                            do
                            {

                                if (webSocketClientConnection?.DataAvailable == true)
                                {

                                    buffer = [];
                                    pos    = 0;

                                    do
                                    {

                                        var buffer2 = new Byte[buffer.Length + (TCPSocket?.Available ?? 0)];

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

                                            Thread.Sleep(1);

                                        } while (webSocketClientConnection.DataAvailable);

                                        Array.Resize(ref buffer, (Int32) pos);

                                        if (WebSocketFrame.TryParse(buffer,
                                                                    out var frame,
                                                                    out var frameLength,
                                                                    out var errorResponse))
                                        {

                                            switch (frame.Opcode)
                                            {

                                                #region Text

                                                case WebSocketFrame.Opcodes.Text:

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

                                                break;

                                                #endregion

                                                #region Binary

                                                case WebSocketFrame.Opcodes.Binary:

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

                                                break;

                                                #endregion

                                                #region Ping

                                                case WebSocketFrame.Opcodes.Ping:

                                                    DebugX.Log($"HTTP Web Socket Client '{Description?.FirstText() ?? RemoteURL.ToString()}' Ping received:   '{frame.Payload.ToUTF8String()}'");

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
                                                        DebugX.Log($"HTTP Web Socket Client '{Description?.FirstText() ?? RemoteURL.ToString()}' sending a CLOSE frame failed!");

                                                    #endregion

                                                    break;

                                                #endregion

                                                #region Pong

                                                case WebSocketFrame.Opcodes.Pong:

                                                    DebugX.Log($"HTTP Web Socket Client '{Description?.FirstText() ?? RemoteURL.ToString()}' Pong received:   '{frame.Payload.ToUTF8String()}'");

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

                                                    DebugX.Log(nameof(WebSocketClient), $" Received unknown {frame.Opcode} frame!");

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
                                    Thread.Sleep(10);

                            }
                            while (!networkingCancellationToken.IsCancellationRequested && ClientCloseMessage is null);


                            #region Close connection if requested!

                            if (httpResponse.Connection is null    ||
                                httpResponse.Connection == "close" ||
                                ClientCloseMessage is not null)
                            {

                                if (TLSStream is not null)
                                {
                                    TLSStream.Close();
                                    TLSStream = null;
                                }

                                if (TCPSocket is not null)
                                {
                                    TCPSocket.Close();
                                    //TCPClient.Dispose();
                                    TCPSocket = null;
                                }

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

                            if (TLSStream is not null)
                            {
                                TLSStream.Close();
                                TLSStream = null;
                            }

                            if (TCPSocket is not null)
                            {
                                TCPSocket.Close();
                                //TCPClient.Dispose();
                                TCPSocket = null;
                            }

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

                            if (TLSStream is not null)
                            {
                                TLSStream.Close();
                                TLSStream = null;
                            }

                            if (TCPSocket is not null)
                            {
                                TCPSocket.Close();
                                //TCPClient.Dispose();
                                TCPSocket = null;
                            }

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
                            DebugX.Log(e2, nameof(HTTPClient) + "." + nameof(ResponseLogDelegate));
                        }

                        #endregion

                    }
                    while (!networkingCancellationToken.IsCancellationRequested && ClientCloseMessage is null);

                }, CancellationToken);

                while (waitingForHTTPResponse is null) {
                    Thread.Sleep(10);
                }

            }

            waitingForHTTPResponse ??= new HTTPResponse.Builder() {
                                           HTTPStatusCode = HTTPStatusCode.BadRequest
                                       };

            return Task.FromResult(
                       new Tuple<WebSocketClientConnection, HTTPResponse>(
                           webSocketClientConnection,
                           waitingForHTTPResponse
                       )
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

        private void DoWebSocketPingSync(Object? State)
        {
            if (!DisableWebSocketPings)
                DoWebSocketPing(State).Wait();
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

                    DebugX.LogException(e);

                }
                finally
                {
                    MaintenanceSemaphore.Release();
                }
            }
            else
                DebugX.LogT("Could not aquire the HTTP web socket ping task lock!");

        }

        #endregion

        #region (Timer) DoMaintenance(State)

        private void DoMaintenanceSync(Object? State)
        {
            if (!DisableMaintenanceTasks)
                DoMaintenanceAsync(State).Wait();
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
                    TCPNetworkStream   = null;
                    HTTPStream  = null;
                }
                catch (Exception e)
                {

                    while (e.InnerException is not null)
                        e = e.InnerException;

                    DebugX.LogException(e);

                }
                finally
                {
                    MaintenanceSemaphore.Release();
                }
            }
            else
                DebugX.LogT("Could not aquire the maintenance tasks lock!");

        }

        protected internal virtual Task DoMaintenanceAsyncStep2(Object? State)
        {

            return Task.CompletedTask;

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

        #region SendWebSocketFrame (WebSocketFrame, ...)

        public async Task<SentStatus> SendWebSocketFrame(WebSocketFrame     WebSocketFrame,
                                                         EventTracking_Id?  EventTrackingId     = null,
                                                         CancellationToken  CancellationToken   = default)
        {

            var eventTrackingId  = EventTrackingId ?? EventTracking_Id.New;

            var sentStatus       = await webSocketClientConnection.SendWebSocketFrame(
                                             WebSocketFrame,
                                             CancellationToken
                                         );

            #region OnTextMessageSent

            if (WebSocketFrame.Opcode == WebSocketFrame.Opcodes.Text)
            {

                //DebugX.Log($"HTTP Web Socket Client '{Description?.FirstText() ?? RemoteURL.ToString()}' Text sent:       '{WebSocketFrame.Payload.ToUTF8String()}' => {sentStatus}");

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

                //DebugX.Log($"HTTP Web Socket Client '{Description?.FirstText() ?? RemoteURL.ToString()}' Binary sent:     '{WebSocketFrame.Payload.ToHexString()}' => {sentStatus}");

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

                DebugX.Log($"HTTP Web Socket Client '{Description?.FirstText() ?? RemoteURL.ToString()}' Ping sent:       '{WebSocketFrame.Payload.ToUTF8String()}' => {sentStatus}");

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

                DebugX.Log($"HTTP Web Socket Client '{Description?.FirstText() ?? RemoteURL.ToString()}' Pong sent:       '{WebSocketFrame.Payload.ToUTF8String()}' => {sentStatus}");

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

                DebugX.Log($"HTTP Web Socket Client '{Description?.FirstText() ?? RemoteURL.ToString()}' Close sent: '{WebSocketFrame.GetClosingStatusCode()}', '{WebSocketFrame.GetClosingReason() ?? ""}' => {sentStatus}");

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

                    HTTPStream.Close();
                    HTTPStream.Dispose();

                }
            }
            catch
            { }

            try
            {
                if (TLSStream is not null)
                {
                    TLSStream.Close();
                    TLSStream.Dispose();
                }
            }
            catch
            { }

            try
            {
                if (TCPNetworkStream is not null)
                {
                    TCPNetworkStream.Close();
                    TCPNetworkStream.Dispose();
                }
            }
            catch
            { }

            try
            {
                if (TCPSocket is not null)
                {
                    TCPSocket.Close();
                    //TCPClient.Dispose();
                }
            }
            catch
            { }

        }

        #endregion


        #region (private) LogEvent(Logger, LogHandler, ...)

        private async Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                               Func<TDelegate, Task>                              LogHandler,
                                               [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                               [CallerMemberName()]                       String  OCPPCommand   = "")

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
                    await HandleErrors($"WebSocketClient: {OCPPCommand}.{EventName}", e);
                }
            }
        }

        #endregion

        #region (private) HandleErrors(Caller, ExceptionOccured)

        private Task HandleErrors(String     Caller,
                                  Exception  ExceptionOccured)
        {

            DebugX.LogException(ExceptionOccured, Caller);

            return Task.CompletedTask;

        }

        #endregion


        #region Dispose()

        /// <summary>
        /// Dispose this object.
        /// </summary>
        public void Dispose()
        {
            Close().GetAwaiter().GetResult();
        }

        #endregion

    }

}
