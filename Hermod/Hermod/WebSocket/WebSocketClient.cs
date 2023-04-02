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
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// The delegate for the HTTP web socket request log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="WebSocketClient">The sending WebSocket client.</param>
    /// <param name="Request">The incoming request.</param>
    public delegate Task WSClientRequestLogHandler(DateTime         Timestamp,
                                                   WebSocketClient  WebSocketClient,
                                                   JArray           Request);

    /// <summary>
    /// The delegate for the HTTP web socket response log.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming request.</param>
    /// <param name="WebSocketClient">The sending WebSocket client.</param>
    /// <param name="Request">The incoming WebSocket request.</param>
    /// <param name="Response">The outgoing WebSocket response.</param>
    public delegate Task WSClientResponseLogHandler(DateTime         Timestamp,
                                                    WebSocketClient  WebSocketClient,
                                                    JArray           Request,
                                                    JArray           Response);


    public delegate Task  OnWebSocketClientTextMessageDelegate  (DateTime                         Timestamp,
                                                                 WebSocketClient                  Client,
                                                                 WebSocketConnection              Connection,
                                                                 WebSocketFrame                   Frame,
                                                                 EventTracking_Id                 EventTrackingId,
                                                                 String                           message);

    public delegate Task  OnWebSocketClientBinaryMessageDelegate(DateTime                         Timestamp,
                                                                 WebSocketClient                  Client,
                                                                 WebSocketConnection              Connection,
                                                                 WebSocketFrame                   Frame,
                                                                 EventTracking_Id                 EventTrackingId,
                                                                 Byte[]                           message);


    /// <summary>
    /// A HTTP web socket client.
    /// </summary>
    public class WebSocketClient : IHTTPClient
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
        private   MyNetworkStream?  TCPStream;
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
        private                   Thread                   networkingThread;

        #endregion

        #region Properties

        /// <summary>
        /// The attached OCPP CP client (HTTP/websocket client) logger.
        /// </summary>
       // public WebSocketClient.CPClientLogger    Logger                          { get; }



        /// <summary>
        /// The remote URL of the HTTP endpoint to connect to.
        /// </summary>
        public URL                                   RemoteURL                       { get; }

        /// <summary>
        /// The virtual HTTP hostname to connect to.
        /// </summary>
        public HTTPHostname?                         VirtualHostname                 { get; }

        /// <summary>
        /// An optional description of this HTTP client.
        /// </summary>
        public String?                               Description                     { get; set; }

        /// <summary>
        /// The remote SSL/TLS certificate validator.
        /// </summary>
        public RemoteCertificateValidationCallback?  RemoteCertificateValidator      { get; private set; }

        /// <summary>
        /// A delegate to select a TLS client certificate.
        /// </summary>
        public LocalCertificateSelectionCallback?    ClientCertificateSelector       { get; }

        /// <summary>
        /// The SSL/TLS client certificate to use of HTTP authentication.
        /// </summary>
        public X509Certificate?                      ClientCert                      { get; }

        /// <summary>
        /// The TLS protocol to use.
        /// </summary>
        public SslProtocols                          TLSProtocol                     { get; }

        /// <summary>
        /// Prefer IPv4 instead of IPv6.
        /// </summary>
        public Boolean                               PreferIPv4                      { get; }

        /// <summary>
        /// The HTTP user agent identification.
        /// </summary>
        public String                                HTTPUserAgent                   { get; }

        /// <summary>
        /// The timeout for upstream requests.
        /// </summary>
        public TimeSpan                              RequestTimeout                  { get; set; }

        /// <summary>
        /// The delay between transmission retries.
        /// </summary>
        public TransmissionRetryDelayDelegate        TransmissionRetryDelay          { get; }

        /// <summary>
        /// The maximum number of retries when communicationg with the remote OICP service.
        /// </summary>
        public UInt16                                MaxNumberOfRetries              { get; }

        /// <summary>
        /// Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.
        /// </summary>
        public Boolean                               UseHTTPPipelining               { get; }

        /// <summary>
        /// The CPO client (HTTP client) logger.
        /// </summary>
        public HTTPClientLogger?                     HTTPLogger                      { get; set; }




        /// <summary>
        /// The DNS client defines which DNS servers to use.
        /// </summary>
        public DNSClient?                            DNSClient                       { get; }



        /// <summary>
        /// Our local IP port.
        /// </summary>
        public IPPort                                LocalPort                       { get; private set; }

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        public IIPAddress?                           RemoteIPAddress                 { get; protected set; }


        public Int32? Available
                    => TCPSocket?.Available;

        public Boolean? Connected
            => TCPSocket?.Connected;

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


        public IHTTPAuthentication?                 HTTPAuthentication              { get; }


        /// <summary>
        /// Disable all maintenance tasks.
        /// </summary>
        public Boolean                              DisableMaintenanceTasks         { get; set; }

        /// <summary>
        /// The maintenance interval.
        /// </summary>
        public TimeSpan                             MaintenanceEvery                { get; }

        /// <summary>
        /// Disable web socket pings.
        /// </summary>
        public Boolean                              DisableWebSocketPings           { get; set; }

        /// <summary>
        /// The web socket ping interval.
        /// </summary>
        public TimeSpan                             WebSocketPingEvery              { get; }


        public TimeSpan?                            SlowNetworkSimulationDelay      { get; set; }


        public IEnumerable<String>                  SecWebSocketProtocols           { get; }

        /// <summary>
        /// The optional error message when this client closed the HTTP WebSocket connection.
        /// </summary>
        public String?                              ClientCloseMessage              { get; private set; }

        #endregion

        #region Events

        public event OnWebSocketClientTextMessageDelegate    OnIncomingTextMessage;
        public event OnWebSocketClientTextMessageDelegate    OnOutgoingTextMessage;

        public event OnWebSocketClientBinaryMessageDelegate  OnIncomingBinaryMessage;
        public event OnWebSocketClientBinaryMessageDelegate  OnOutgoingBinaryMessage;

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
        /// <param name="RemoteCertificateValidator">The remote SSL/TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The SSL/TLS client certificate to use of HTTP authentication.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="URLPathPrefix">An optional default URL path prefix.</param>
        /// <param name="HTTPAuthentication">The WebService-Security username/password.</param>
        /// <param name="RequestTimeout">An optional Request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP Request through a single HTTP/TCP connection.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="LoggingContext">An optional context for logging client methods.</param>
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public WebSocketClient(URL                                   RemoteURL,
                               HTTPHostname?                         VirtualHostname              = null,
                               String?                               Description                  = null,
                               RemoteCertificateValidationCallback?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionCallback?    ClientCertificateSelector    = null,
                               X509Certificate?                      ClientCert                   = null,
                               String                                HTTPUserAgent                = DefaultHTTPUserAgent,
                               HTTPPath?                             URLPathPrefix                = null,
                               SslProtocols?                         TLSProtocol                  = null,
                               Boolean?                              PreferIPv4                   = null,
                               IHTTPAuthentication?                  HTTPAuthentication           = null,
                               TimeSpan?                             RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                               UInt16?                               MaxNumberOfRetries           = 3,
                               Boolean                               UseHTTPPipelining            = false,

                               IEnumerable<String>?                  SecWebSocketProtocols        = null,

                               Boolean                               DisableMaintenanceTasks      = false,
                               TimeSpan?                             MaintenanceEvery             = null,
                               Boolean                               DisableWebSocketPings        = false,
                               TimeSpan?                             WebSocketPingEvery           = null,
                               TimeSpan?                             SlowNetworkSimulationDelay   = null,

                               String?                               LoggingPath                  = null,
                               String                                LoggingContext               = "logcontext", //CPClientLogger.DefaultContext,
                               LogfileCreatorDelegate?               LogfileCreator               = null,
                               HTTPClientLogger?                     HTTPLogger                   = null,
                               DNSClient?                            DNSClient                    = null)

        {

            this.RemoteURL                          = RemoteURL;
            this.VirtualHostname                    = VirtualHostname;
            this.Description                        = Description;
            this.RemoteCertificateValidator         = RemoteCertificateValidator;
            this.ClientCertificateSelector          = ClientCertificateSelector;
            this.ClientCert                         = ClientCert;
            this.HTTPUserAgent                      = HTTPUserAgent;
            //this.URLPathPrefix                      = URLPathPrefix;
            this.TLSProtocol                        = TLSProtocol             ?? SslProtocols.Tls12 | SslProtocols.Tls13;
            this.PreferIPv4                         = PreferIPv4              ?? false;
            this.HTTPAuthentication                 = HTTPAuthentication;
            this.RequestTimeout                     = RequestTimeout          ?? TimeSpan.FromMinutes(10);
            this.TransmissionRetryDelay             = TransmissionRetryDelay  ?? (retryCount => TimeSpan.FromSeconds(5));
            this.MaxNumberOfRetries                 = MaxNumberOfRetries      ?? 3;
            this.UseHTTPPipelining                  = UseHTTPPipelining;
            this.HTTPLogger                         = HTTPLogger;
            this.DNSClient                          = DNSClient;

            this.SecWebSocketProtocols              = SecWebSocketProtocols   ?? Array.Empty<String>();

            this.DisableMaintenanceTasks            = DisableMaintenanceTasks;
            this.MaintenanceEvery                   = MaintenanceEvery        ?? DefaultMaintenanceEvery;
            this.MaintenanceTimer                   = new Timer(DoMaintenanceSync,
                                                                null,
                                                                this.MaintenanceEvery,
                                                                this.MaintenanceEvery);

            this.DisableWebSocketPings              = DisableWebSocketPings;
            this.WebSocketPingEvery                 = WebSocketPingEvery      ?? DefaultWebSocketPingEvery;
            this.WebSocketPingTimer                 = new Timer(DoWebSocketPingSync,
                                                                null,
                                                                this.WebSocketPingEvery,
                                                                this.WebSocketPingEvery);

            this.SlowNetworkSimulationDelay         = SlowNetworkSimulationDelay;

            this.networkingCancellationTokenSource  = new CancellationTokenSource();
            this.networkingCancellationToken        = new CancellationTokenSource().Token;

            //this.Logger                             = new ChargePointwebsocketClient.CPClientLogger(this,
            //                                                                                   LoggingPath,
            //                                                                                   LoggingContext,
            //                                                                                   LogfileCreator);

        }

        #endregion


        public virtual Task ProcessWebSocketTextFrame(WebSocketFrame frame)
            => Task.CompletedTask;

        public virtual Task ProcessWebSocketBinaryFrame(WebSocketFrame frame)
            => Task.CompletedTask;


        #region Connect(EventTrackingId = null, RequestTimeout = null, NumberOfRetries = 0)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="EventTrackingId"></param>
        /// <param name="RequestTimeout">An optional timeout.</param>
        /// <param name="NumberOfRetries">The number of retransmissions of this request.</param>
        public Task<HTTPResponse?> Connect(EventTracking_Id?   EventTrackingId     = null,
                                           TimeSpan?           RequestTimeout      = null,
                                           Byte                NumberOfRetries     = 0)
        {

            HTTPResponse? waitingForHTTPResponse = null;

            if (networkingThread is null)
            {

                networkingThread = new Thread(async () => {

                    do
                    {

                        HTTPRequest?  httpRequest   = null;
                        HTTPResponse? httpResponse  = null;

                        if (!RequestTimeout.HasValue)
                            RequestTimeout = TimeSpan.FromMinutes(10);

                        try
                        {

                            //Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

                            #region Data

                            var HTTPHeaderBytes   = Array.Empty<Byte>();
                            var HTTPBodyBytes     = Array.Empty<Byte>();
                            var sw                = new Stopwatch();

                            #endregion

                            #region Create TCP connection (possibly also do DNS lookups)

                            Boolean restart;

                            do
                            {

                                restart = false;

                                #region Setup TCP socket

                                if (TCPSocket is null)
                                {

                                    System.Net.IPEndPoint? _FinalIPEndPoint = null;

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

                                    _FinalIPEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(RemoteIPAddress.GetBytes()),
                                                                                 RemoteURL.Port.Value.ToInt32());

                                    sw.Start();

                                    //TCPClient = new TcpClient();
                                    //TCPClient.Connect(_FinalIPEndPoint);
                                    //TCPClient.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;


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
                                        TCPSocket.Connect(_FinalIPEndPoint);
                                        TCPSocket.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;
                                    }

                                }

                                TCPStream = TCPSocket is not null
                                                ? new MyNetworkStream(TCPSocket, true) {
                                                      ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds
                                                  }
                                                : null;

                                #endregion

                                #region Create (Crypto-)Stream

                                if (RemoteCertificateValidator is null &&
                                   (RemoteURL.Protocol == URLProtocols.wss || RemoteURL.Protocol == URLProtocols.https))
                                {
                                    RemoteCertificateValidator = (sender, certificate, chain, sslPolicyErrors) => {
                                        return true;
                                    };
                                }

                                if (RemoteCertificateValidator is not null)
                                {

                                    if (TLSStream is null)
                                    {

                                        TLSStream = new SslStream(TCPStream,
                                                                  false,
                                                                  RemoteCertificateValidator,
                                                                  ClientCertificateSelector,
                                                                  EncryptionPolicy.RequireEncryption)
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
                                        catch (Exception)
                                        {
                                            TCPSocket  = null;
                                            restart    = true;
                                        }

                                    }

                                }

                                else
                                {
                                    TLSStream   = null;
                                    HTTPStream  = TCPStream;
                                }

                                HTTPStream.ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                                #endregion

                            }
                            while (restart);

                            this.LocalPort = IPSocket.FromIPEndPoint(TCPStream!.Socket.LocalEndPoint!).Port;

                            #endregion

                            #region Send Request

                            // GET /webServices/ocpp/CP3211 HTTP/1.1
                            // Host:                    some.server.com:33033
                            // Connection:              Upgrade
                            // Upgrade:                 websocket
                            // Sec-WebSocket-Key:       x3JJHMbDL1EzLkh9GBhXDw==
                            // Sec-WebSocket-Protocol:  ocpp1.6, ocpp1.5
                            // Sec-WebSocket-Version:   13

                            var swkaSHA1Base64    = RandomExtensions.GetBytes(16).ToBase64();
                            var expectedWSAccept  = System.Security.Cryptography.SHA1.HashData((swkaSHA1Base64 + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").ToUTF8Bytes()).ToBase64();

                            httpRequest           = new HTTPRequest.Builder {
                                                        Path                  = RemoteURL.Path,
                                                        Host                  = HTTPHostname.Parse(String.Concat(RemoteURL.Hostname, ":", RemoteURL.Port)),
                                                        Connection            = "Upgrade",
                                                        Upgrade               = "websocket",
                                                        SecWebSocketKey       = swkaSHA1Base64,
                                                        SecWebSocketProtocol  = SecWebSocketProtocols.Any()
                                                                                    ? SecWebSocketProtocols.AggregateWith(", ")
                                                                                    : null,
                                                        SecWebSocketVersion   = "13",
                                                        Authorization         = HTTPAuthentication
                                                    }.AsImmutable;

                            #region Call the optional HTTP request log delegate

                            try
                            {

                                if (RequestLogDelegate is not null)
                                    await Task.WhenAll(RequestLogDelegate.GetInvocationList().
                                                       Cast<ClientRequestLogHandler>().
                                                       Select(e => e(Timestamp.Now,
                                                                     this,
                                                                     httpRequest))).
                                                       ConfigureAwait(false);

                            }
                            catch (Exception e)
                            {
                                DebugX.Log(e, nameof(HTTPClient) + "." + nameof(RequestLogDelegate));
                            }

                            #endregion

                            HTTPStream.Write((httpRequest.EntirePDU + "\r\n\r\n").ToUTF8Bytes());

                            HTTPStream.Flush();

                            //File.AppendAllText(LogfileName,
                            //                   String.Concat("Timestamp: ",         Timestamp.Now.ToIso8601(),                                                Environment.NewLine,
                            //                                 "ChargeBoxId: ",       ChargeBoxIdentity.ToString(),                                             Environment.NewLine,
                            //                                 "HTTP request: ",      Environment.NewLine,                                                      Environment.NewLine,
                            //                                 httpRequest.EntirePDU,                                                                               Environment.NewLine,
                            //                                 "--------------------------------------------------------------------------------------------",  Environment.NewLine));

                            #endregion

                            #region Wait for HTTP response

                            var buffer  = new Byte[16 * 1024];
                            var pos     = 0;

                            do
                            {

                                pos += HTTPStream.Read(buffer, pos, 2048);

                                if (sw.ElapsedMilliseconds >= RequestTimeout.Value.TotalMilliseconds)
                                    throw new HTTPTimeoutException(sw.Elapsed);

                                Thread.Sleep(1);

                            } while (TCPStream.DataAvailable && pos < buffer.Length - 2048);

                            var responseData  = buffer.ToUTF8String(pos);
                            var lines         = responseData.Split('\n').Select(line => line?.Trim()).TakeWhile(line => line.IsNotNullOrEmpty()).ToArray();
                            httpResponse      = HTTPResponse.Parse(lines.AggregateWith(Environment.NewLine),
                                                                   Array.Empty<byte>(),
                                                                   httpRequest);

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


                            do
                            {

                                if (TCPStream?.DataAvailable == true)
                                {

                                    buffer = Array.Empty<Byte>();
                                    pos    = 0;

                                    do
                                    {

                                        var buffer2 = new Byte[buffer.Length + (TCPSocket?.Available ?? 0)];

                                        do
                                        {

                                            var read = HTTPStream.Read(buffer2, 0, buffer2.Length);

                                            if (read > 0)
                                            {
                                                Array.Resize(ref buffer, pos+read);
                                                Array.Copy(buffer2, 0, buffer, pos, read);
                                                pos += read;
                                            }

                                            //if (sw.ElapsedMilliseconds >= RequestTimeout.Value.TotalMilliseconds)
                                            //    throw new HTTPTimeoutException(sw.Elapsed);

                                            Thread.Sleep(1);

                                        } while (TCPStream.DataAvailable);

                                        Array.Resize(ref buffer, pos);

                                        if (WebSocketFrame.TryParse(buffer,
                                                                    out WebSocketFrame?  frame,
                                                                    out UInt64           frameLength,
                                                                    out String?          errorResponse))
                                        {

                                            if (frame is not null)
                                            {

                                                switch (frame.Opcode)
                                                {

                                                    case WebSocketFrame.Opcodes.Text: {

                                                        #region OnIncomingTextMessage

                                                        try
                                                        {

                                                            OnIncomingTextMessage?.Invoke(Timestamp.Now,
                                                                                          this,
                                                                                          null, //webSocketConnection,
                                                                                          frame,
                                                                                          EventTracking_Id.New,
                                                                                          frame.Payload.ToUTF8String());

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketClient) + "." + nameof(OnIncomingTextMessage));
                                                        }

                                                        #endregion

                                                        await ProcessWebSocketTextFrame(frame);

                                                    }
                                                    break;

                                                    case WebSocketFrame.Opcodes.Binary: {

                                                        #region OnIncomingBinaryMessage

                                                        try
                                                        {

                                                            OnIncomingBinaryMessage?.Invoke(Timestamp.Now,
                                                                                            this,
                                                                                            null, //webSocketConnection,
                                                                                            frame,
                                                                                            EventTracking_Id.New,
                                                                                            frame.Payload);

                                                        }
                                                        catch (Exception e)
                                                        {
                                                            DebugX.Log(e, nameof(WebSocketClient) + "." + nameof(OnIncomingBinaryMessage));
                                                        }

                                                        #endregion

                                                        await ProcessWebSocketBinaryFrame(frame);

                                                    }
                                                    break;

                                                    case WebSocketFrame.Opcodes.Ping: {

                                                        DebugX.Log(nameof(WebSocketClient) + ": Ping received: " + frame.Payload.ToUTF8String());

                                                        SendWebSocketFrame(new WebSocketFrame(
                                                                               WebSocketFrame.Fin.Final,
                                                                               WebSocketFrame.MaskStatus.On,
                                                                               new Byte[] { 0xaa, 0xaa, 0xaa, 0xaa },
                                                                               WebSocketFrame.Opcodes.Pong,
                                                                               frame.Payload,
                                                                               WebSocketFrame.Rsv.Off,
                                                                               WebSocketFrame.Rsv.Off,
                                                                               WebSocketFrame.Rsv.Off
                                                                           ));

                                                    }
                                                    break;

                                                    case WebSocketFrame.Opcodes.Pong: {
                                                        DebugX.Log(nameof(WebSocketClient) + ": Pong received: " + frame.Payload.ToUTF8String());
                                                    }
                                                    break;

                                                    default: {
                                                        DebugX.Log(nameof(WebSocketClient), " Received unknown " + frame.Opcode + " frame!");
                                                    }
                                                    break;

                                                }

                                                if ((UInt64) buffer.Length > frameLength)
                                                {
                                                    var newBuffer = new Byte[(UInt64) buffer.Length - frameLength];
                                                    Array.Copy(buffer, (UInt32) frameLength, newBuffer, 0, newBuffer.Length);
                                                    buffer = newBuffer;
                                                }
                                                else
                                                    buffer = null;

                                            }

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

                            httpResponse = new HTTPResponse.Builder(httpRequest,
                                                                    HTTPStatusCode.RequestTimeout)
                            {

                                ContentType  = HTTPContentType.JSON_UTF8,
                                Content      = JSONObject.Create(new JProperty("timeout",     (Int32) hte.Timeout.TotalMilliseconds),
                                                                 new JProperty("message",     hte.Message),
                                                                 new JProperty("stackTrace",  hte.StackTrace)).
                                                          ToUTF8Bytes()

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

                            httpResponse = new HTTPResponse.Builder(httpRequest,
                                                                    HTTPStatusCode.BadRequest)
                            {

                                ContentType  = HTTPContentType.JSON_UTF8,
                                Content      = JSONObject.Create(new JProperty("message",     e.Message),
                                                                 new JProperty("stackTrace",  e.StackTrace)).
                                                          ToUTF8Bytes()

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

                });

                networkingThread.Start();

                while (waitingForHTTPResponse is null) {
                    Thread.Sleep(10);
                }

            }

            return Task.FromResult(waitingForHTTPResponse);

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
                    if (HTTPStream is not null)
                    {

                        lock (HTTPStream)
                        {

                            pingCounter++;

                            var payload = pingCounter + ":" + Guid.NewGuid().ToString();

                            SendWebSocketFrame(new WebSocketFrame(
                                                   WebSocketFrame.Fin.Final,
                                                   WebSocketFrame.MaskStatus.On,
                                                   new Byte[] { 0xaa, 0xbb, 0xcc, 0xdd },
                                                   WebSocketFrame.Opcodes.Ping,
                                                   payload.ToUTF8Bytes(),
                                                   WebSocketFrame.Rsv.Off,
                                                   WebSocketFrame.Rsv.Off,
                                                   WebSocketFrame.Rsv.Off
                                               ));

                            DebugX.Log(nameof(WebSocketClient) + ": Ping sent:     '" + payload + "'!");

                        }

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
                    TCPStream   = null;
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


        #region SendText  (Text)

        /// <summary>
        /// Send a web socket text frame
        /// </summary>
        /// <param name="Text">The text to send.</param>
        public void SendText(String Text)
        {

            var webSocketFrame = new WebSocketFrame(
                                     WebSocketFrame.Fin.Final,
                                     WebSocketFrame.MaskStatus.On,
                                     new Byte[] { 0xaa, 0xaa, 0xaa, 0xaa },
                                     WebSocketFrame.Opcodes.Text,
                                     Text.ToUTF8Bytes(),
                                     WebSocketFrame.Rsv.Off,
                                     WebSocketFrame.Rsv.Off,
                                     WebSocketFrame.Rsv.Off
                                 );

            #region OnOutgoingTextMessage

            try
            {

                OnOutgoingTextMessage?.Invoke(Timestamp.Now,
                                              this,
                                              null, //webSocketConnection,
                                              webSocketFrame,
                                              EventTracking_Id.New,
                                              webSocketFrame.Payload.ToUTF8String());

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(WebSocketClient) + "." + nameof(OnOutgoingTextMessage));
            }

            #endregion

            SendWebSocketFrame(webSocketFrame);

        }

        #endregion

        #region SendBinary(Bytes)

        /// <summary>
        /// Send a web socket binary frame
        /// </summary>
        /// <param name="Bytes">The array of bytes to send.</param>
        public void SendBinary(Byte[] Bytes)
        {

            var webSocketFrame = new WebSocketFrame(
                                     WebSocketFrame.Fin.Final,
                                     WebSocketFrame.MaskStatus.On,
                                     new Byte[] { 0xaa, 0xaa, 0xaa, 0xaa },
                                     WebSocketFrame.Opcodes.Binary,
                                     Bytes,
                                     WebSocketFrame.Rsv.Off,
                                     WebSocketFrame.Rsv.Off,
                                     WebSocketFrame.Rsv.Off
                                 );

            #region OnOutgoingBinaryMessage

            try
            {

                OnOutgoingBinaryMessage?.Invoke(Timestamp.Now,
                                                this,
                                                null, //webSocketConnection,
                                                webSocketFrame,
                                                EventTracking_Id.New,
                                                webSocketFrame.Payload);

            }
            catch (Exception e)
            {
                DebugX.Log(e, nameof(WebSocketClient) + "." + nameof(OnOutgoingBinaryMessage));
            }

            #endregion


            SendWebSocketFrame(webSocketFrame);

        }

        #endregion

        #region SendWebSocketFrame(WebSocketFrame)

        public void SendWebSocketFrame(WebSocketFrame WebSocketFrame)
        {
            if (HTTPStream is not null)
            {
                lock (HTTPStream)
                {

                    try
                    {

                        if (SlowNetworkSimulationDelay.HasValue)
                        {
                            foreach (var _byte in WebSocketFrame.ToByteArray())
                            {
                                HTTPStream.Write(new Byte[] { _byte });
                                HTTPStream.Flush();
                                Thread.Sleep(SlowNetworkSimulationDelay.Value);
                            }
                        }

                        else
                        {
                            HTTPStream.Write(WebSocketFrame.ToByteArray());
                            HTTPStream.Flush();
                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.LogException(e, "Sending a web socket frame in " + nameof(WebSocketClient));
                    }

                }
            }
        }

        #endregion


        #region Close()

        public void Close()
        {

            try
            {
                if (HTTPStream is not null)
                {

                    SendWebSocketFrame(new WebSocketFrame(
                                           WebSocketFrame.Fin.Final,
                                           WebSocketFrame.MaskStatus.On,
                                           new Byte[] { 0xaa, 0xaa, 0xaa, 0xaa },
                                           WebSocketFrame.Opcodes.Close,
                                           Array.Empty<Byte>(),
                                           WebSocketFrame.Rsv.Off,
                                           WebSocketFrame.Rsv.Off,
                                           WebSocketFrame.Rsv.Off
                                       ));

                    HTTPStream.Close();
                    HTTPStream.Dispose();

                }
            }
            catch (Exception)
            { }

            try
            {
                if (TLSStream is not null)
                {
                    TLSStream.Close();
                    TLSStream.Dispose();
                }
            }
            catch (Exception)
            { }

            try
            {
                if (TCPStream is not null)
                {
                    TCPStream.Close();
                    TCPStream.Dispose();
                }
            }
            catch (Exception)
            { }

            try
            {
                if (TCPSocket is not null)
                {
                    TCPSocket.Close();
                    //TCPClient.Dispose();
                }
            }
            catch (Exception)
            { }

        }

        #endregion

        #region Dispose()

        /// <summary>
        /// Dispose this object.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        #endregion

    }

}
