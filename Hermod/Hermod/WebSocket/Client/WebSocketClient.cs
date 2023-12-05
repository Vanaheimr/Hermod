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
using static org.GraphDefined.Vanaheimr.Hermod.WebSocket.WebSocketFrame;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{


    public delegate Task  OnWebSocketClientTextMessageDelegate  (DateTime                    Timestamp,
                                                                 WebSocketClient             Client,
                                                                 WebSocketClientConnection   Connection,
                                                                 WebSocketFrame              Frame,
                                                                 EventTracking_Id            EventTrackingId,
                                                                 String                      TextMessage);

    public delegate Task  OnWebSocketClientBinaryMessageDelegate(DateTime                    Timestamp,
                                                                 WebSocketClient             Client,
                                                                 WebSocketClientConnection   Connection,
                                                                 WebSocketFrame              Frame,
                                                                 EventTracking_Id            EventTrackingId,
                                                                 Byte[]                      BinaryMessage);


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
        private                   Thread                   networkingThread;


        private WebSocketClientConnection webSocketClientConnection;

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
        /// The remote TLS certificate validator.
        /// </summary>
        public RemoteCertificateValidationHandler?  RemoteCertificateValidator      { get; private set; }

        /// <summary>
        /// A delegate to select a TLS client certificate.
        /// </summary>
        public LocalCertificateSelectionHandler?    ClientCertificateSelector       { get; }

        /// <summary>
        /// The TLS client certificate to use of HTTP authentication.
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
        public Boolean                               UseHTTPPipelining
            => false;

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

        public event OnWebSocketClientTextMessageDelegate?    OnTextMessageReceived;
        public event OnWebSocketClientTextMessageDelegate?    OnTextMessageSent;

        public event OnWebSocketClientBinaryMessageDelegate?  OnBinaryMessageReceived;
        public event OnWebSocketClientBinaryMessageDelegate?  OnBinaryMessageSent;

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
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
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
        public WebSocketClient(URL                                   RemoteURL,
                               HTTPHostname?                         VirtualHostname              = null,
                               String?                               Description                  = null,
                               Boolean?                              PreferIPv4                   = null,
                               RemoteCertificateValidationHandler?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionHandler?    ClientCertificateSelector    = null,
                               X509Certificate?                      ClientCert                   = null,
                               SslProtocols?                         TLSProtocol                  = null,
                               String?                               HTTPUserAgent                = DefaultHTTPUserAgent,
                               IHTTPAuthentication?                  HTTPAuthentication           = null,
                               TimeSpan?                             RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                               UInt16?                               MaxNumberOfRetries           = 3,
                               UInt32?                               InternalBufferSize           = null,

                               IEnumerable<String>?                  SecWebSocketProtocols        = null,

                               Boolean                               DisableWebSocketPings        = false,
                               TimeSpan?                             WebSocketPingEvery           = null,
                               TimeSpan?                             SlowNetworkSimulationDelay   = null,

                               Boolean                               DisableMaintenanceTasks      = false,
                               TimeSpan?                             MaintenanceEvery             = null,

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
            this.HTTPUserAgent                      = HTTPUserAgent           ?? DefaultHTTPUserAgent;
            this.TLSProtocol                        = TLSProtocol             ?? SslProtocols.Tls12 | SslProtocols.Tls13;
            this.PreferIPv4                         = PreferIPv4              ?? false;
            this.HTTPAuthentication                 = HTTPAuthentication;
            this.RequestTimeout                     = RequestTimeout          ?? TimeSpan.FromMinutes(10);
            this.TransmissionRetryDelay             = TransmissionRetryDelay  ?? (retryCount => TimeSpan.FromSeconds(5));
            this.MaxNumberOfRetries                 = MaxNumberOfRetries      ?? 3;
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


        public virtual Task ProcessWebSocketTextFrame  (DateTime                   RequestTimestamp,
                                                        WebSocketClientConnection  Connection,
                                                        EventTracking_Id           EventTrackingId,
                                                        String                     TextMessage,
                                                        CancellationToken          CancellationToken)
            => Task.CompletedTask;

        public virtual Task ProcessWebSocketBinaryFrame(DateTime                   RequestTimestamp,
                                                        WebSocketClientConnection  WebSocketConnection,
                                                        EventTracking_Id           EventTrackingId,
                                                        Byte[]                     BinaryMessage,
                                                        CancellationToken          CancellationToken)
            => Task.CompletedTask;


        #region Connect(EventTrackingId = null, RequestTimeout = null, NumberOfRetries = 0)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="EventTrackingId"></param>
        /// <param name="RequestTimeout">An optional timeout.</param>
        /// <param name="NumberOfRetries">The number of retransmissions of this request.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        public Task<HTTPResponse> Connect(EventTracking_Id?  EventTrackingId     = null,
                                          TimeSpan?          RequestTimeout      = null,
                                          Byte               NumberOfRetries     = 0,
                                          CancellationToken  CancellationToken   = default)
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
                                    RemoteCertificateValidator = (sender, certificate, chain, sslPolicyErrors) => {
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

                                                                                                 var check = RemoteCertificateValidator(sender,
                                                                                                                                        certificate is not null
                                                                                                                                            ? new X509Certificate2(certificate)
                                                                                                                                            : null,
                                                                                                                                        chain,
                                                                                                                                        policyErrors);

                                                                                                 if (check.Item2.Any())
                                                                                                     remoteCertificateValidatorErrors.AddRange(check.Item2);

                                                                                                 return check.Item1;

                                                                                             },
                                                        userCertificateSelectionCallback:    ClientCertificateSelector is null
                                                                                                 ? null
                                                                                                 : (sender,
                                                                                                    targetHost,
                                                                                                    localCertificates,
                                                                                                    remoteCertificate,
                                                                                                    acceptableIssuers) => ClientCertificateSelector(sender,
                                                                                                                                                    targetHost,
                                                                                                                                                    localCertificates.
                                                                                                                                                        Cast<X509Certificate>().
                                                                                                                                                        Select(certificate => new X509Certificate2(certificate)),
                                                                                                                                                    remoteCertificate is not null
                                                                                                                                                        ? new X509Certificate2(remoteCertificate)
                                                                                                                                                        : null,
                                                                                                                                                    acceptableIssuers),
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
                            // Sec-WebSocket-Protocol:  ocpp1.6, ocpp1.5
                            // Sec-WebSocket-Version:   13

                            var swkaSHA1Base64    = RandomExtensions.RandomBytes(16).ToBase64();
                            var expectedWSAccept  = System.Security.Cryptography.SHA1.HashData((swkaSHA1Base64 + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").ToUTF8Bytes()).ToBase64();

                            httpRequest           = new HTTPRequest.Builder {
                                                        Path                  = RemoteURL.Path,
                                                        Host                  = HTTPHostname.Parse(String.Concat(RemoteURL.Hostname, ":", RemoteURL.Port)),
                                                        Connection            = "Upgrade",
                                                        Upgrade               = "websocket",
                                                        SecWebSocketKey       = swkaSHA1Base64,
                                                        SecWebSocketProtocol  = SecWebSocketProtocols,
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
                            //                                 httpRequest.EntirePDU,                                                                           Environment.NewLine,
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

                            } while (TCPNetworkStream.DataAvailable && pos < buffer.Length - 2048);

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


                            webSocketClientConnection = new WebSocketClientConnection(this,
                                                                                      TCPSocket,
                                                                                      TCPNetworkStream,
                                                                                      HTTPStream,
                                                                                      httpRequest,
                                                                                      httpResponse,
                                                                                      CustomData:                  null,
                                                                                      SlowNetworkSimulationDelay:  null);

                            do
                            {

                                if (webSocketClientConnection?.DataAvailable == true)
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

                                        } while (webSocketClientConnection.DataAvailable);

                                        Array.Resize(ref buffer, pos);

                                        if (TryParse(buffer,
                                                     out var frame,
                                                     out var frameLength,
                                                     out var errorResponse) &&
                                            frame is not null)
                                        {

                                            switch (frame.Opcode)
                                            {

                                                #region Text

                                                case WebSocketFrame.Opcodes.Text: {

                                                    #region OnTextMessageReceived

                                                    try
                                                    {

                                                            var onTextMessageReceived = OnTextMessageReceived;
                                                            if (onTextMessageReceived is not null)
                                                                await onTextMessageReceived.Invoke(Timestamp.Now,
                                                                                                   this,
                                                                                                   webSocketClientConnection,
                                                                                                   frame,
                                                                                                   EventTracking_Id.New,
                                                                                                   frame.Payload.ToUTF8String());

                                                    }
                                                    catch (Exception e)
                                                    {
                                                        DebugX.Log(e, nameof(WebSocketClient) + "." + nameof(OnTextMessageReceived));
                                                    }

                                                    #endregion

                                                    await ProcessWebSocketTextFrame(Timestamp.Now,
                                                                                    webSocketClientConnection,
                                                                                    EventTracking_Id.New,
                                                                                    frame.Payload.ToUTF8String(),
                                                                                    CancellationToken);

                                                }
                                                break;

                                                #endregion

                                                #region Binary

                                                case WebSocketFrame.Opcodes.Binary: {

                                                    #region OnBinaryMessageReceived

                                                    try
                                                    {

                                                        var onBinaryMessageReceived = OnBinaryMessageReceived;
                                                        if (onBinaryMessageReceived is not null)
                                                            await onBinaryMessageReceived.Invoke(Timestamp.Now,
                                                                                                    this,
                                                                                                    webSocketClientConnection,
                                                                                                    frame,
                                                                                                    EventTracking_Id.New,
                                                                                                    frame.Payload);

                                                    }
                                                    catch (Exception e)
                                                    {
                                                        DebugX.Log(e, nameof(WebSocketClient) + "." + nameof(OnBinaryMessageReceived));
                                                    }

                                                    #endregion

                                                    await ProcessWebSocketBinaryFrame(Timestamp.Now,
                                                                                      webSocketClientConnection,
                                                                                      EventTracking_Id.New,
                                                                                      frame.Payload,
                                                                                      CancellationToken);

                                                }
                                                break;

                                                #endregion

                                                #region Ping

                                                case WebSocketFrame.Opcodes.Ping: {

                                                    DebugX.Log(nameof(WebSocketClient) + ": Ping received: " + frame.Payload.ToUTF8String());

                                                    await SendWebSocketFrame(WebSocketFrame.Pong(
                                                                                    frame.Payload,
                                                                                    Fin.Final,
                                                                                    MaskStatus.On,
                                                                                    RandomExtensions.RandomBytes(4)
                                                                                ));

                                                }
                                                break;

                                                #endregion

                                                #region Pong

                                                case WebSocketFrame.Opcodes.Pong: {
                                                    DebugX.Log(nameof(WebSocketClient) + ": Pong received: " + frame.Payload.ToUTF8String());
                                                }
                                                break;

                                                #endregion

                                                #region ...unknown

                                                default:
                                                    DebugX.Log(nameof(WebSocketClient), " Received unknown " + frame.Opcode + " frame!");

                                                break;

                                                #endregion

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

                });

                networkingThread.Start();

                while (waitingForHTTPResponse is null) {
                    Thread.Sleep(10);
                }

            }

            waitingForHTTPResponse ??= new HTTPResponse.Builder() {
                                           HTTPStatusCode = HTTPStatusCode.BadRequest
                                       };

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

                        pingCounter++;

                        var payload = pingCounter + ":" + Guid.NewGuid().ToString();

                        await SendWebSocketFrame(WebSocketFrame.Ping(
                                                     payload.ToUTF8Bytes(),
                                                     WebSocketFrame.Fin.Final,
                                                     WebSocketFrame.MaskStatus.On,
                                                     RandomExtensions.RandomBytes(4)
                                                 ));

                        DebugX.Log(nameof(WebSocketClient) + ": Ping sent:     '" + payload + "'!");

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


        #region SendText  (Text)

        /// <summary>
        /// Send a web socket text frame
        /// </summary>
        /// <param name="Text">The text to send.</param>
        public Task SendText(String Text)

            => SendWebSocketFrame(WebSocketFrame.Text(
                                      Text,
                                      Fin.Final,
                                      MaskStatus.On,
                                      RandomExtensions.RandomBytes(4)
                                  ));

        #endregion

        #region SendBinary(Bytes)

        /// <summary>
        /// Send a web socket binary frame
        /// </summary>
        /// <param name="Bytes">The array of bytes to send.</param>
        public Task SendBinary(Byte[] Bytes)

            => SendWebSocketFrame(WebSocketFrame.Binary(
                                      Bytes,
                                      Fin.Final,
                                      MaskStatus.On,
                                      RandomExtensions.RandomBytes(4)
                                  ));

        #endregion

        #region SendWebSocketFrame(WebSocketFrame)

        public async Task SendWebSocketFrame(WebSocketFrame WebSocketFrame)
        {

            await webSocketClientConnection.SendWebSocketFrame(WebSocketFrame);

            #region OnTextMessageSent

            if (WebSocketFrame.Opcode == Opcodes.Text)
            {

                try
                {

                    var onTextMessageSent = OnTextMessageSent;
                    if (onTextMessageSent is not null)
                        await onTextMessageSent.Invoke(Timestamp.Now,
                                                       this,
                                                       webSocketClientConnection,
                                                       WebSocketFrame,
                                                       EventTracking_Id.New,
                                                       WebSocketFrame.Payload.ToUTF8String());

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(WebSocketClient) + "." + nameof(OnTextMessageSent));
                }

            }

            #endregion

            #region OnBinaryMessageSent

            else if (WebSocketFrame.Opcode == Opcodes.Binary)
            {

                try
                {

                    var onBinaryMessageSent = OnBinaryMessageSent;
                    if (onBinaryMessageSent is not null)
                        await onBinaryMessageSent.Invoke(Timestamp.Now,
                                                         this,
                                                         webSocketClientConnection,
                                                         WebSocketFrame,
                                                         EventTracking_Id.New,
                                                         WebSocketFrame.Payload);

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(WebSocketClient) + "." + nameof(OnBinaryMessageSent));
                }

            }

            #endregion

        }

        #endregion


        #region Close(StatusCode = Normal, Reason = null)

        /// <summary>
        /// Close the connection.
        /// </summary>
        /// <param name="StatusCode">An optional status code for closing.</param>
        /// <param name="Reason">An optional reason for closing.</param>
        public async Task Close(ClosingStatusCode  StatusCode   = ClosingStatusCode.NormalClosure,
                                String?            Reason       = null)
        {

            try
            {
                if (HTTPStream is not null)
                {

                    await SendWebSocketFrame(WebSocketFrame.Close(
                                                 StatusCode,
                                                 Reason,
                                                 WebSocketFrame.Fin.Final,
                                                 WebSocketFrame.MaskStatus.On,
                                                 RandomExtensions.RandomBytes(4)
                                             ));

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
