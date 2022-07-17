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
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A delegate to calculate the delay between transmission retries.
    /// </summary>
    /// <param name="RetryCount">The retry counter.</param>
    public delegate TimeSpan TransmissionRetryDelayDelegate(UInt32 RetryCount);


    /// <summary>
    /// An abstract base class for all HTTP clients.
    /// </summary>
    public abstract class AHTTPClient : IHTTPClient
    {

        #region Data

        private Socket?           TCPSocket;
        private MyNetworkStream?  TCPStream;
        private SslStream?        TLSStream;
        private Stream?           HTTPStream;


        /// <summary>
        /// The default HTTP user agent.
        /// </summary>
        public const           String    DefaultHTTPUserAgent            = "GraphDefined HTTP Client";

        /// <summary>
        /// The default remote TCP port to connect to.
        /// </summary>
        public static readonly IPPort    DefaultRemotePort               = IPPort.HTTP;

        /// <summary>
        /// The default timeout for upstream queries.
        /// </summary>
        public static readonly TimeSpan  DefaultRequestTimeout           = TimeSpan.FromSeconds(60);

        /// <summary>
        /// The default delay between transmission retries.
        /// </summary>
        public static readonly TimeSpan  DefaultTransmissionRetryDelay   = TimeSpan.FromSeconds(2);

        /// <summary>
        /// The default number of maximum transmission retries.
        /// </summary>
        public const           UInt16    DefaultMaxNumberOfRetries       = 3;

        #endregion

        #region Properties

        /// <summary>
        /// The remote URL of the HTTP endpoint to connect to.
        /// </summary>
        public URL                                   RemoteURL                     { get; }

        /// <summary>
        /// The virtual HTTP hostname to connect to.
        /// </summary>
        public HTTPHostname?                         VirtualHostname               { get; }

        /// <summary>
        /// An optional description of this HTTP client.
        /// </summary>
        public String?                               Description                   { get; set; }

        /// <summary>
        /// The remote SSL/TLS certificate validator.
        /// </summary>
        public RemoteCertificateValidationCallback?  RemoteCertificateValidator    { get; }

        /// <summary>
        /// A delegate to select a TLS client certificate.
        /// </summary>
        public LocalCertificateSelectionCallback?    ClientCertificateSelector     { get; }

        /// <summary>
        /// The SSL/TLS client certificate to use of HTTP authentication.
        /// </summary>
        public X509Certificate?                      ClientCert                    { get; }

        /// <summary>
        /// The TLS protocol to use.
        /// </summary>
        public SslProtocols                          TLSProtocol                   { get; }

        /// <summary>
        /// Prefer IPv4 instead of IPv6.
        /// </summary>
        public Boolean                               PreferIPv4                    { get; }

        /// <summary>
        /// The HTTP user agent identification.
        /// </summary>
        public String                                HTTPUserAgent                 { get; }

        /// <summary>
        /// The timeout for upstream requests.
        /// </summary>
        public TimeSpan                              RequestTimeout                { get; set; }

        /// <summary>
        /// The delay between transmission retries.
        /// </summary>
        public TransmissionRetryDelayDelegate        TransmissionRetryDelay        { get; }

        /// <summary>
        /// The maximum number of retries when communicationg with the remote OICP service.
        /// </summary>
        public UInt16                                MaxNumberOfRetries            { get; }

        /// <summary>
        /// Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.
        /// </summary>
        public Boolean                               UseHTTPPipelining             { get; }

        /// <summary>
        /// The CPO client (HTTP client) logger.
        /// </summary>
        public HTTPClientLogger?                     HTTPLogger                    { get; set; }

        /// <summary>
        /// The DNS client defines which DNS servers to use.
        /// </summary>
        public DNSClient                             DNSClient                     { get; }


        ///// <summary>
        ///// The Hostname to which the HTTPClient connects.
        ///// </summary>
        //public HTTPHostname     Hostname            { get; }


        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        public IIPAddress?                           RemoteIPAddress               { get; private set; }

        /// <summary>
        /// The IP port to connect to.
        /// </summary>
        public IPPort?                               RemotePort                    { get; }



        public Int32 Available
                    => TCPSocket.Available;

        public Boolean Connected
            => TCPSocket.Connected;

        public LingerOption? LingerState
        {
            get
            {
                return TCPSocket.LingerState;
            }
            set
            {
                TCPSocket.LingerState = value;
            }
        }

        public Boolean NoDelay
        {
            get
            {
                return TCPSocket.NoDelay;
            }
            set
            {
                TCPSocket.NoDelay = value;
            }
        }

        public Byte TTL
        {
            get
            {
                return (Byte) TCPSocket.Ttl;
            }
            set
            {
                TCPSocket.Ttl = value;
            }
        }


        #endregion

        #region Events

        public delegate Task OnDataReadDelegate(TimeSpan Timestamp, UInt64 BytesRead, UInt64? BytesExpected = null);

        public event OnDataReadDelegate OnDataRead;



        public delegate Task OnChunkDataReadDelegate(TimeSpan Timestamp, UInt32 BytesRead, UInt64 TotalBytes);

        public event OnChunkDataReadDelegate OnChunkDataRead;



        public delegate Task OnChunkBlockFoundDelegate(TimeSpan                           Timestamp,
                                                       UInt32                             ChunkNumber,
                                                       UInt32                             ChunkLength,
                                                       Dictionary<String, List<String>>?  ChunkExtentions,
                                                       Byte[]                             ChunkData,
                                                       UInt64                             TotalBytes);

        public event OnChunkBlockFoundDelegate OnChunkBlockFound;

        /// <summary>
        /// An event fired whenever an exception occured.
        /// </summary>
        public event OnExceptionDelegate OnException;


        /// <summary>
        /// A delegate called whenever a HTTP error occured.
        /// </summary>
        public delegate void OnHTTPErrorDelegate(DateTime Timestamp, Object Sender, HTTPResponse HttpResponse);

        /// <summary>
        /// An event fired whenever a HTTP error occured.
        /// </summary>
        public event OnHTTPErrorDelegate OnHTTPError;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract HTTP client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this HTTP client.</param>
        /// <param name="RemoteCertificateValidator">The remote SSL/TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The SSL/TLS client certificate to use of HTTP authentication.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        protected AHTTPClient(URL                                   RemoteURL,
                              HTTPHostname?                         VirtualHostname              = null,
                              String?                               Description                  = null,
                              RemoteCertificateValidationCallback?  RemoteCertificateValidator   = null,
                              LocalCertificateSelectionCallback?    ClientCertificateSelector    = null,
                              X509Certificate?                      ClientCert                   = null,
                              SslProtocols?                         TLSProtocol                  = null,
                              Boolean?                              PreferIPv4                   = null,
                              String                                HTTPUserAgent                = DefaultHTTPUserAgent,
                              TimeSpan?                             RequestTimeout               = null,
                              TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                              UInt16?                               MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                              Boolean                               UseHTTPPipelining            = false,
                              HTTPClientLogger?                     HTTPLogger                   = null,
                              DNSClient?                            DNSClient                    = null)
        {

            this.RemoteURL                   = RemoteURL;
            this.VirtualHostname             = VirtualHostname;
            this.Description                 = Description;
            this.RemoteCertificateValidator  = RemoteCertificateValidator;
            this.ClientCertificateSelector   = ClientCertificateSelector;
            this.ClientCert                  = ClientCert;
            this.PreferIPv4                  = PreferIPv4             ?? false;
            this.TLSProtocol                 = TLSProtocol            ?? SslProtocols.Tls12;
            this.HTTPUserAgent               = HTTPUserAgent          ?? DefaultHTTPUserAgent;
            this.RequestTimeout              = RequestTimeout         ?? DefaultRequestTimeout;
            this.TransmissionRetryDelay      = TransmissionRetryDelay ?? (retryCounter => TimeSpan.FromSeconds(retryCounter * retryCounter * DefaultTransmissionRetryDelay.TotalSeconds));
            this.MaxNumberOfRetries          = MaxNumberOfRetries     ?? DefaultMaxNumberOfRetries;
            this.UseHTTPPipelining           = UseHTTPPipelining;
            this.HTTPLogger                  = HTTPLogger;
            this.DNSClient                   = DNSClient              ?? new DNSClient();

            this.RemotePort                  = RemoteURL.Port         ?? (RemoteURL.Protocol == URLProtocols.http
                                                                             ? IPPort.HTTP
                                                                             : IPPort.HTTPS);

            if (this.ClientCertificateSelector is null && this.ClientCert is not null)
                this.ClientCertificateSelector = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => this.ClientCert;

        }

        #endregion


        #region CreateRequest(HTTPMethod, HTTPPath, BuilderAction = null)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">A HTTP method.</param>
        /// <param name="HTTPPath">An URL.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A new HTTPRequest object.</returns>
        public HTTPRequest.Builder CreateRequest(HTTPMethod                    HTTPMethod,
                                                 HTTPPath                      HTTPPath,
                                                 Action<HTTPRequest.Builder>?  BuilderAction  = null)
        {

            var Builder     = new HTTPRequest.Builder(this) {
                Host        = HTTPHostname.Parse((VirtualHostname ?? RemoteURL.Hostname) + (RemoteURL.Port.HasValue && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS ? ":" + RemoteURL.Port.ToString() : "")),
                HTTPMethod  = HTTPMethod,
                Path        = HTTPPath
            };

            if (BuilderAction is not null)
                BuilderAction?.Invoke(Builder);

            return Builder;

        }

        #endregion


        #region Execute(HTTPRequestDelegate, RequestLogDelegate = null, ResponseLogDelegate = null, Timeout = null, CancellationToken = null)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="HTTPRequestDelegate">A delegate for producing a HTTP request for a given HTTP client.</param>
        /// <param name="RequestLogDelegate">A delegate for logging the HTTP request.</param>
        /// <param name="ResponseLogDelegate">A delegate for logging the HTTP request/response.</param>
        /// 
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <param name="EventTrackingId"></param>
        /// <param name="RequestTimeout">An optional HTTP request timeout.</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        public Task<HTTPResponse> Execute(Func<AHTTPClient, HTTPRequest>  HTTPRequestDelegate,
                                          ClientRequestLogHandler?        RequestLogDelegate    = null,
                                          ClientResponseLogHandler?       ResponseLogDelegate   = null,

                                          CancellationToken?              CancellationToken     = null,
                                          EventTracking_Id?               EventTrackingId       = null,
                                          TimeSpan?                       RequestTimeout        = null,
                                          Byte                            NumberOfRetry         = 0)

        {

            #region Initial checks

            if (HTTPRequestDelegate is null)
                throw new ArgumentNullException(nameof(HTTPRequestDelegate), "The given delegate must not be null!");

            #endregion

            return Execute(HTTPRequestDelegate(this),
                           RequestLogDelegate,
                           ResponseLogDelegate,

                           CancellationToken,
                           EventTrackingId,
                           RequestTimeout,
                           NumberOfRetry);

        }

        #endregion

        #region Execute(Request, RequestLogDelegate = null, ResponseLogDelegate = null, Timeout = null, CancellationToken = null)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="RequestLogDelegate">A delegate for logging the HTTP request.</param>
        /// <param name="ResponseLogDelegate">A delegate for logging the HTTP request/response.</param>
        /// 
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <param name="EventTrackingId"></param>
        /// <param name="RequestTimeout">An optional timeout.</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        public async Task<HTTPResponse> Execute(HTTPRequest                Request,
                                                ClientRequestLogHandler?   RequestLogDelegate    = null,
                                                ClientResponseLogHandler?  ResponseLogDelegate   = null,

                                                CancellationToken?         CancellationToken     = null,
                                                EventTracking_Id?          EventTrackingId       = null,
                                                TimeSpan?                  RequestTimeout        = null,
                                                Byte                       NumberOfRetry         = 0)

        {

            HTTPResponse? Response = null;

            try
            {

                //Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

                #region Data

                var HTTPHeaderBytes  = Array.Empty<Byte>();
                var HTTPBodyBytes    = Array.Empty<Byte>();
                var sw               = new Stopwatch();

                if (!RequestTimeout.HasValue)
                    RequestTimeout = Request.Timeout ?? this.RequestTimeout;

                #endregion

                #region Create TCP connection (possibly also do DNS lookups)

                Boolean restart;

                do
                {

                    restart = false;

                    if (TCPSocket is null)
                    {

                        System.Net.IPEndPoint? _FinalIPEndPoint = null;

                        if (RemoteIPAddress is null)
                        {

                            #region Localhost

                            if (IPAddress.IsIPv4Localhost(RemoteURL.Hostname))
                                RemoteIPAddress = IPv4Address.Localhost;

                            else if (IPAddress.IsIPv6Localhost(RemoteURL.Hostname))
                                RemoteIPAddress = IPv6Address.Localhost;

                            else if (IPAddress.IsIPv4(RemoteURL.Hostname.Name))
                                RemoteIPAddress = IPv4Address.Parse(RemoteURL.Hostname.Name);

                            else if (IPAddress.IsIPv6(RemoteURL.Hostname.Name))
                                RemoteIPAddress = IPv6Address.Parse(RemoteURL.Hostname.Name);

                            #endregion

                            #region DNS lookup...

                            if (RemoteIPAddress is null)
                            {

                                var IPv4AddressLookupTask = DNSClient.
                                                                Query<A>(RemoteURL.Hostname.Name).
                                                                ContinueWith(query => query.Result.Select(ARecord    => ARecord.IPv4Address));

                                var IPv6AddressLookupTask = DNSClient.
                                                                Query<AAAA>(RemoteURL.Hostname.Name).
                                                                ContinueWith(query => query.Result.Select(AAAARecord => AAAARecord.IPv6Address));

                                await Task.WhenAll(IPv4AddressLookupTask,
                                                   IPv6AddressLookupTask).
                                           ConfigureAwait(false);

                                if (PreferIPv4)
                                {
                                    if (IPv6AddressLookupTask.Result.Any())
                                        RemoteIPAddress = IPv6AddressLookupTask.Result.First();

                                    if (IPv4AddressLookupTask.Result.Any())
                                        RemoteIPAddress = IPv4AddressLookupTask.Result.First();
                                }
                                else
                                {
                                    if (IPv4AddressLookupTask.Result.Any())
                                        RemoteIPAddress = IPv4AddressLookupTask.Result.First();

                                    if (IPv6AddressLookupTask.Result.Any())
                                        RemoteIPAddress = IPv6AddressLookupTask.Result.First();
                                }

                            }

                            #endregion

                        }

                        if (RemoteIPAddress is not null && RemotePort is not null)
                            _FinalIPEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(RemoteIPAddress.GetBytes()),
                                                                         RemotePort.Value.ToInt32());
                        else
                            throw new Exception("DNS lookup failed!");


                        sw.Start();

                        //TCPClient = new TcpClient();
                        //TCPClient.Connect(_FinalIPEndPoint);
                        //TCPClient.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                        if (RemoteIPAddress.IsIPv4)
                            TCPSocket = new Socket(AddressFamily.InterNetwork,
                                                   SocketType.Stream,
                                                   ProtocolType.Tcp);

                        if (RemoteIPAddress.IsIPv6)
                            TCPSocket = new Socket(AddressFamily.InterNetworkV6,
                                                   SocketType.Stream,
                                                   ProtocolType.Tcp);

                        TCPSocket!.SendTimeout    = (Int32) RequestTimeout.Value.TotalMilliseconds;
                        TCPSocket!.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;
                        TCPSocket!.Connect(_FinalIPEndPoint);
                        TCPSocket!.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                    }

                    TCPStream = new MyNetworkStream(TCPSocket, true) {
                                    ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds
                                };

                #endregion

                #region Create (Crypto-)Stream

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
                                                                          null,
                                                                          this.TLSProtocol,
                                                                          false);//, new X509CertificateCollection(new X509Certificate[] { ClientCert }), SslProtocols.Default, true);

                            }
                            catch (Exception)
                            {
                                TCPSocket = null;
                                restart   = true;
                            }

                        }

                    }

                    else
                    {
                        TLSStream  = null;
                        HTTPStream = TCPStream;
                    }

                    HTTPStream.ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                }
                while (restart);

                #endregion

                Request.LocalSocket   = IPSocket.FromIPEndPoint(TCPStream.Socket.LocalEndPoint);
                Request.HTTPSource    = new HTTPSource(IPSocket.FromIPEndPoint(TCPStream.Socket.LocalEndPoint));
                Request.RemoteSocket  = IPSocket.FromIPEndPoint(TCPStream.Socket.RemoteEndPoint);

                #region Call the optional HTTP request log delegate

                try
                {

                    if (RequestLogDelegate is not null)
                        await Task.WhenAll(RequestLogDelegate.GetInvocationList().
                                           Cast<ClientRequestLogHandler>().
                                           Select(e => e(Timestamp.Now,
                                                         this,
                                                         Request))).
                                           ConfigureAwait(false);

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(HTTPClient) + "." + nameof(RequestLogDelegate));
                }

                #endregion

                #region Send Request

                // To avoid confusions, better send the entire header incl. separator lines as one block
                HTTPStream.Write(String.Concat(Request.EntireRequestHeader, "\r\n\r\n").
                                        ToUTF8Bytes());

                var RequestBodyLength = Request.HTTPBody is null
                                            ? Request.ContentLength.HasValue
                                                  ? (Int32) Request.ContentLength.Value
                                                  : 0
                                            : Request.ContentLength.HasValue
                                                  ? Math.Min((Int32) Request.ContentLength.Value,
                                                             Request.HTTPBody.Length)
                                                  : Request.HTTPBody.Length;

                if (RequestBodyLength > 0)
                    HTTPStream.Write(Request.HTTPBody, 0, RequestBodyLength);

                //DebugX.LogT("HTTPClient (" + Request.HTTPMethod + " " + Request.URL + ") sent request of " + Request.EntirePDU.Length + " bytes at " + sw.ElapsedMilliseconds + "ms!");

                var _InternalHTTPStream  = new MemoryStream();

                #endregion

                #region Wait timeout for the server to react!


                while (!TCPStream.DataAvailable)
                {

                    if (sw.ElapsedMilliseconds >= RequestTimeout.Value.TotalMilliseconds)
                        throw new HTTPTimeoutException(sw.Elapsed);

                    Thread.Sleep(1);

                }

                //DebugX.LogT("HTTPClient (" + Request.HTTPMethod + " " + Request.URL + ") got first response after " + sw.ElapsedMilliseconds + "ms!");

                #endregion


                #region Read the HTTP header

                var currentDataLength    = 0;
                var CurrentHeaderLength  = 0;
                var HeaderEndsAt         = 0;

                var _Buffer = new Byte[65536];

                do
                {

                    currentDataLength = HTTPStream.Read(_Buffer, 0, _Buffer.Length);

                    if (currentDataLength > 0)
                    {

                        if (currentDataLength > 3 || CurrentHeaderLength > 3)
                        {

                            for (var pos = 3; pos < _Buffer.Length; pos++)
                            {
                                if (_Buffer[pos    ] == 0x0a &&
                                    _Buffer[pos - 1] == 0x0d &&
                                    _Buffer[pos - 2] == 0x0a &&
                                    _Buffer[pos - 3] == 0x0d)
                                {
                                    HeaderEndsAt = pos - 3;
                                    break;
                                }
                            }

                            if (HeaderEndsAt > 0)
                            {

                                Array.Resize(ref HTTPHeaderBytes, CurrentHeaderLength + HeaderEndsAt);
                                Array.Copy(_Buffer, 0, HTTPHeaderBytes, CurrentHeaderLength, HeaderEndsAt);
                                CurrentHeaderLength += HeaderEndsAt;

                                // We already read a bit of the HTTP body!
                                if (HeaderEndsAt + 4 < currentDataLength)
                                {
                                    Array.Resize(ref HTTPBodyBytes, currentDataLength - 4 - HeaderEndsAt);
                                    Array.Copy(_Buffer, HeaderEndsAt + 4, HTTPBodyBytes, 0, HTTPBodyBytes.Length);
                                }

                            }

                            else
                            {
                                Array.Resize(ref HTTPHeaderBytes, CurrentHeaderLength + _Buffer.Length);
                                Array.Copy(_Buffer, 0, HTTPHeaderBytes, CurrentHeaderLength, _Buffer.Length);
                                CurrentHeaderLength += _Buffer.Length;
                                Thread.Sleep(1);
                            }

                        }

                    }

                } while (HeaderEndsAt == 0 &&
                         sw.ElapsedMilliseconds < HTTPStream.ReadTimeout);

                if (HTTPHeaderBytes.Length == 0)
                    throw new ApplicationException("[" + Timestamp.Now.ToString() + "] Could not find the end of the HTTP protocol header!");

                Response = HTTPResponse.Parse(HTTPHeaderBytes.ToUTF8String(),
                                              Request,
                                              HTTPBodyBytes,
                                              NumberOfRetry);

                #endregion


                _Buffer = new Byte[50 * 1024 * 1024];

                #region A single fixed-lenght HTTP request -> read '$Content-Length' bytes...

                // Copy only the number of bytes given within
                // the HTTP header element 'Content-Length'!
                if (Response.ContentLength.HasValue && Response.ContentLength.Value > 0)
                {

                    // Test via:
                    // var aaa = new HTTPClient("www.networksorcery.com").GET("/enp/rfc/rfc1000.txt").ExecuteReturnResult().Result;

                    var _StillToRead = (Int32) Response.ContentLength.Value - Response.HTTPBody.Length;

                    do
                    {

                        while (//TCPStream.DataAvailable &&  <= Does not work as expected!
                               _StillToRead > 0)
                        {

                            currentDataLength = HTTPStream.Read(_Buffer, 0, Math.Min(_Buffer.Length, _StillToRead));

                            if (currentDataLength > 0)
                            {
                                var OldSize = Response.HTTPBody.Length;
                                Response.ResizeBody(OldSize + currentDataLength);
                                Array.Copy(_Buffer, 0, Response.HTTPBody, OldSize, currentDataLength);
                                _StillToRead -= currentDataLength;
                            }

                        }

                        OnDataRead?.Invoke(sw.Elapsed,
                                           Response.ContentLength.Value - (UInt64) _StillToRead,
                                           Response.ContentLength.Value);

                        if (_StillToRead <= 0)
                            break;

                        Thread.Sleep(1);

                    }
                    while (sw.ElapsedMilliseconds < HTTPStream.ReadTimeout);

                }

                #endregion

                #region ...or chunked transport...

                else if (Response.TransferEncoding == "chunked")
                {

                    //DebugX.Log("HTTP Client: Chunked Transport detected...");

                    try
                    {

                        Response.NewContentStream();
                        var chunkedStream            = new MemoryStream();

                        // Write the first buffer (without the HTTP header) to the chunked stream...
                        chunkedStream.Write(HTTPBodyBytes, 0, HTTPBodyBytes.Length);

                        var chunkedArray             = chunkedStream.ToArray();
                        var decodedStream            = new MemoryStream();
                        var currentPosition          = 2U;
                        var lastPosition             = 0U;
                        var currentBlockNumber       = 0U;
                        var chunkedDecodingFinished  = 0;
                        var trailingHeaders          = new Dictionary<String, String?>();

                        do
                        {

                            #region Read more data from network

                            if (TCPStream.DataAvailable)
                            {

                                do
                                {

                                    currentDataLength = HTTPStream.Read(_Buffer, 0, _Buffer.Length);

                                    DebugX.Log("ReadTEBlock read from network: " + currentDataLength);

                                    if (currentDataLength > 0)
                                        chunkedStream.Write(_Buffer, 0, currentDataLength);

                                    OnChunkDataRead?.Invoke(sw.Elapsed,
                                                            (UInt32) currentDataLength,
                                                            (UInt64) chunkedStream.Length);

                                    if (sw.ElapsedMilliseconds > HTTPStream.ReadTimeout)
                                        chunkedDecodingFinished = 3;

                                } while (TCPStream.DataAvailable && chunkedDecodingFinished == 0);

                                chunkedArray = chunkedStream.ToArray();

                            }

                            #endregion

                            #region Process chunks

                            // HTTP/1.1 200 OK\r\n
                            // Server:             Apache/1.3.27\r\n
                            // Transfer-Encoding:  chunked\r\n
                            // Content-Type:       text/html; charset=iso-8859-1\r\n
                            // Trailer:            Cache-Control\r\n
                            // \r\n
                            // ee1;XXX\r\n
                            // [Die ersten 3809 Zeichen der Datei]
                            // \r\n
                            // ffb;XXX\r\n
                            // [Weitere 4091 Zeichen der Datei]
                            // \r\n
                            // c40;XXX\r\n
                            // [Die letzten 3136 Zeichen der Datei]
                            // \r\n
                            // 0\r\n
                            // Cache-Control: no-cache\r\n
                            // \r\n
                            // [Ende]

                            // A process for decoding the chunked transfer coding can be represented
                            //    in pseudo-code as:
                            //
                            //      length := 0
                            //      read chunk-size, chunk-ext (if any), and CRLF
                            //      while (chunk-size > 0) {
                            //         read chunk-data and CRLF
                            //         append chunk-data to decoded-body
                            //         length := length + chunk-size
                            //         read chunk-size, chunk-ext (if any), and CRLF
                            //      }
                            //      read trailer field
                            //      while (trailer field is not empty) {
                            //         if (trailer field is allowed to be sent in a trailer) {
                            //             append trailer field to existing header fields
                            //         }
                            //         read trailer-field
                            //      }
                            //      Content-Length := length
                            //      Remove "chunked" from Transfer-Encoding
                            //      Remove Trailer from existing header fields



                            if (chunkedArray.Length >= currentPosition)
                            {
                                if (chunkedArray[currentPosition - 1] == '\n' &&
                                    chunkedArray[currentPosition - 2] == '\r')
                                {

                                    if (chunkedDecodingFinished == 0)
                                    {

                                        currentBlockNumber++;

                                        var chunkInfo = ChunkInfos.Parse(chunkedArray,
                                                                         lastPosition,
                                                                         currentPosition - lastPosition - 2);

                                        // End of stream reached?
                                        if (chunkInfo.Length == 0)
                                        {

                                            OnChunkBlockFound?.Invoke(sw.Elapsed,
                                                                      currentBlockNumber,
                                                                      0,
                                                                      chunkInfo.Extentions,
                                                                      Array.Empty<Byte>(),
                                                                      (UInt64) decodedStream.Length);

                                            Response.ContentStreamToArray(decodedStream);

                                            chunkedDecodingFinished = 1;
                                            //currentPosition += chunkInfo.Length + 2;
                                            lastPosition     = currentPosition;
                                            currentPosition += 1;

                                        }

                                        // Read a new block... and final "\r\n"
                                        if (chunkedDecodingFinished == 0 &&
                                            currentPosition + chunkInfo.Length + 2 <= chunkedArray.Length)
                                        {

                                            if (OnChunkBlockFound != null)
                                            {

                                                var chunkData = new Byte[chunkInfo.Length];
                                                Array.Copy(chunkedArray, currentPosition, chunkData, 0, chunkInfo.Length);

                                                await OnChunkBlockFound.Invoke(sw.Elapsed,
                                                                               currentBlockNumber,
                                                                               chunkInfo.Length,
                                                                               chunkInfo.Extentions,
                                                                               chunkData,
                                                                               (UInt64) decodedStream.Length);

                                            }

                                            decodedStream.Write(chunkedArray, (Int32) currentPosition, (Int32) chunkInfo.Length);

                                            currentPosition += chunkInfo.Length + 2;
                                            lastPosition     = currentPosition;
                                            currentPosition += 1;

                                        }

                                    }

                                    else
                                    {

                                        if (currentPosition - lastPosition == 2)
                                        {
                                            chunkedDecodingFinished = 2;
                                            break;
                                        }

                                        var trailingHeaderBuffer = new Byte[currentPosition - lastPosition - 2];
                                        Array.Copy(chunkedArray, lastPosition, trailingHeaderBuffer, 0, currentPosition - lastPosition - 2);

                                        var trailingHeader = trailingHeaderBuffer?.ToUTF8String()?.Trim()?.Split(':');

                                        if (trailingHeader != null &&
                                            trailingHeader?.Length > 1 &&
                                            trailingHeader[0]?.Trim()?.IsNotNullOrEmpty() == true)
                                        {
                                            trailingHeaders.Add(trailingHeader[0]!,
                                                                trailingHeader?.Length > 1 ? trailingHeader[1]?.Trim() : null);
                                        }

                                        lastPosition     = currentPosition;
                                        currentPosition += 1;

                                    }

                                }
                                else
                                    currentPosition++;
                            }

                            #endregion

                            if (sw.ElapsedMilliseconds > HTTPStream.ReadTimeout)
                                chunkedDecodingFinished = 3;

                        } while (chunkedDecodingFinished < 2);

                        if (chunkedDecodingFinished == 3)
                            DebugX.Log("HTTP Client: Chunked decoding timeout!");
                        //else
                        //    DebugX.Log("HTTP Client: Chunked decoding finished!");

                        if (Response.TryGetHeaderField("Transfer-Encoding", out Object transferEncoding))
                        {
                            if (transferEncoding is "chunked")
                                Response.RemoveHeaderField("Transfer-Encoding");
                        }

                        if (Response.TryGetHeaderField("Trailer", out Object trailer))
                        {

                            var allowedTrailingHeaderfields = new List<String>();

                            if (trailer is String _trailer)
                            {
                                trailer = _trailer.Split(';')?.Select(element => element?.Trim()).Where(element => element.IsNotNullOrEmpty()).ToList();
                            }

                            if (trailer is List<String> _trailerList)
                            {
                                foreach (var element in _trailerList)
                                {
                                    if (element != "Transfer-Encoding" &&
                                        element != "Content-Length" &&
                                        element != "Trailer")
                                    {
                                        allowedTrailingHeaderfields.Add(element!);
                                    }
                                }
                            }

                            foreach (var trailingHeader in trailingHeaders)
                            {
                                if (allowedTrailingHeaderfields.Contains(trailingHeader.Key))
                                    Response.SetHeaderField(trailingHeader.Key,
                                                            trailingHeader.Value);
                            }

                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.Log("HTTP Client: Chunked decoding failed: " + e.Message);
                        DebugX.Log(e.StackTrace);
                    }

                }

                #endregion

                #region ...or just connect HTTP stream to network stream!

                else
                {
                    Response.ContentStreamToArray();
                }

                #endregion

                #region Close connection if requested!

                if (Response.Connection is null ||
                    Response.Connection == "close")
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
            catch (HTTPTimeoutException e)
            {

                #region Create a HTTP response for the exception...

                Response = new HTTPResponse.Builder(Request,
                                                    HTTPStatusCode.RequestTimeout)
                {

                    ContentType  = HTTPContentType.JSON_UTF8,
                    Content      = JSONObject.Create(new JProperty("timeout",     (Int32) e.Timeout.TotalMilliseconds),
                                                     new JProperty("message",     e.Message),
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
            catch (Exception e)
            {

                #region Create a HTTP response for the exception...

                while (e.InnerException is not null)
                    e = e.InnerException;

                Response = new HTTPResponse.Builder(Request,
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


            #region Call the optional HTTP response log delegate

            try
            {

                if (ResponseLogDelegate is not null)
                    await Task.WhenAll(ResponseLogDelegate.GetInvocationList().
                                       Cast<ClientResponseLogHandler>().
                                       Select(e => e(Timestamp.Now,
                                                     this,
                                                     Request,
                                                     Response))).
                                       ConfigureAwait(false);

            }
            catch (Exception e2)
            {
                DebugX.Log(e2, nameof(HTTPClient) + "." + nameof(ResponseLogDelegate));
            }

            #endregion

            return Response;

        }

        #endregion


        #region (protected) SendHTTPError(Timestamp, Sender, HttpResponse)

        /// <summary>
        /// Notify that an HTTP error occured.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the error received.</param>
        /// <param name="Sender">The sender of this error message.</param>
        /// <param name="HttpResponse">The HTTP response related to this error message.</param>
        protected void SendHTTPError(DateTime      Timestamp,
                                     Object        Sender,
                                     HTTPResponse  HttpResponse)
        {

            DebugX.Log("AHTTPClient => HTTP Status Code: " + HttpResponse != null ? HttpResponse.HTTPStatusCode.ToString() : "<null>");

            OnHTTPError?.Invoke(Timestamp, Sender, HttpResponse);

        }

        #endregion

        #region (protected) SendException(Timestamp, Sender, Exception)

        /// <summary>
        /// Notify that an exception occured.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the exception.</param>
        /// <param name="Sender">The sender of this exception.</param>
        /// <param name="Exception">The exception itself.</param>
        protected void SendException(DateTime   Timestamp,
                                     Object     Sender,
                                     Exception  Exception)
        {

            DebugX.Log("AHTTPClient => Exception: " + Exception.Message);

            OnException?.Invoke(Timestamp, Sender, Exception);

        }

        #endregion


        #region Close()

        public void Close()
        {

            try
            {
                if (HTTPStream != null)
                {
                    HTTPStream.Close();
                    HTTPStream.Dispose();
                }
            }
            catch (Exception)
            { }

            try
            {
                if (TLSStream != null)
                {
                    TLSStream.Close();
                    TLSStream.Dispose();
                }
            }
            catch (Exception)
            { }

            try
            {
                if (TCPStream != null)
                {
                    TCPStream.Close();
                    TCPStream.Dispose();
                }
            }
            catch (Exception)
            { }

            try
            {
                if (TCPSocket != null)
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
        public virtual void Dispose()
        {
            Close();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()

            => String.Concat(GetType().Name, " ",
                             RemoteIPAddress.ToString(), ":", RemotePort);

        #endregion

    }

}
