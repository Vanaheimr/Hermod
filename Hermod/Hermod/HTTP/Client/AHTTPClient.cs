/*
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


    #region (class) HTTPClientTimings

    public class HTTPClientTimings
    {

        public TimeSpan               Elapsed
            => Timestamp.Now - Start;

        public List<Elapsed<String>>  Errors                  { get; }


        public DateTimeOffset         Start                   { get; }
        public TimeSpan?              Request                 { get; }
        public TimeSpan?              RequestLogging1         { get; internal set; }
        public TimeSpan?              RequestLogging2         { get; internal set; }
        public TimeSpan?              DNSLookup               { get; internal set; }
        public TimeSpan?              Connected               { get; internal set; }
        public TimeSpan?              TLSHandshake            { get; internal set; }
        public Byte                   RestartCounter          { get; internal set; }
        public UInt64?                RequestHeaderLength     { get; internal set; }
        public TimeSpan?              WriteRequestHeader      { get; internal set; }
        public UInt64?                RequestBodyLength       { get; internal set; }
        public TimeSpan?              WriteRequestBody        { get; internal set; }
        public List<Elapsed<UInt64>>  DataReceived            { get; }
        public DateTimeOffset         ResponseTimestamp       { get; internal set; }
        public TimeSpan?              ResponseHeaderParsed    { get; internal set; }
        public TimeSpan?              ResponseLogging1        { get; internal set; }
        public TimeSpan?              ResponseLogging2        { get; internal set; }



        public HTTPClientTimings(HTTPRequest? HTTPRequest = null)
        {

            var now            = Timestamp.Now;

            this.Start         = HTTPRequest is not null
                                     ? HTTPRequest.Timestamp < now
                                           ? HTTPRequest.Timestamp
                                           : now
                                     : now;

            this.Request       = HTTPRequest is not null
                                     ? HTTPRequest.Timestamp - Start
                                     : null;

            this.DataReceived  = [];
            this.Errors        = [];

        }

        public void AddHTTPResponse(HTTPResponse HTTPResponse)
        {
            this.ResponseTimestamp = HTTPResponse.Timestamp;
        }



        public void AddError(String Error)
        {
            Errors.Add(new Elapsed<String>(Timestamp.Now - Start, Error));
        }


        public String ErrorsAsString()

            => Errors.Select(elapsed => elapsed.Time.TotalMilliseconds.ToString("F2") + ": " + elapsed.Value).AggregateWith(Environment.NewLine);


        public override String ToString()

            => String.Concat(
                    "Start: ",                Start.                                      ToISO8601(),                             " > ",
                    "Request: ",              Request?.                                   TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "RequestLogging1: ",      RequestLogging1?.                           TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "RequestLogging2: ",      RequestLogging2?.                           TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "DNSLookup: ",            DNSLookup?.                                 TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "Connected: ",            Connected?.                                 TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "TLSHandshake: ",         TLSHandshake?.                              TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "RestartCounter: ",       RestartCounter - 1,                                                                  " > ",
                    "WriteRequestHeader: ",   WriteRequestHeader?.                        TotalMilliseconds.ToString("F2") ?? "-", $" ({RequestHeaderLength} bytes) > ",
                    "WriteRequestBody: ",     WriteRequestBody?.                          TotalMilliseconds.ToString("F2") ?? "-", $" ({RequestBodyLength} bytes) > ",
                    DataReceived.Select(elapsed => $"DataReceived: {elapsed.Time.TotalMilliseconds:F2} ({elapsed.Value} bytes)").AggregateWith(" > "), " > ",
                    "ResponseTimestamp: ",   (ResponseTimestamp - Start - Request!.Value).TotalMilliseconds.ToString("F2"), " > ",
                    "ResponseHeaderParsed: ", ResponseHeaderParsed?.                      TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "ResponseLogging1: ",     ResponseLogging1?.                          TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "ResponseLogging2: ",     ResponseLogging2?.                          TotalMilliseconds.ToString("F2") ?? "-"
                );

    }

    #endregion


    /// <summary>
    /// An abstract base class for all HTTP clients.
    /// </summary>
    public abstract class AHTTPClient : IHTTPClient
    {

        #region Data

        private Socket?           tcpSocket;
        private MyNetworkStream?  tcpStream;
        private SslStream?        tlsStream;
        private Stream?           httpStream;


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
        /// The default size of the internal buffers.
        /// </summary>
        public const           UInt32    DefaultInternalBufferSize       = 65536;

        /// <summary>
        /// The default number of maximum transmission retries.
        /// </summary>
        public const           UInt16    DefaultMaxNumberOfRetries       = 3;

        #endregion

        #region Properties

        /// <summary>
        /// The remote URL of the HTTP endpoint to connect to.
        /// </summary>
        public URL                                                        RemoteURL                     { get; }

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        public IIPAddress?                                                RemoteIPAddress               { get; private set; }

        /// <summary>
        /// The HTTP/TCP port to connect to.
        /// </summary>
        public IPPort?                                                    RemotePort                    { get; }

        /// <summary>
        /// The virtual HTTP hostname to connect to.
        /// </summary>
        public HTTPHostname?                                              VirtualHostname               { get; }

        /// <summary>
        /// The Remote X.509 certificate.
        /// </summary>
        public X509Certificate2?                                          RemoteCertificate             { get; private set; }

        /// <summary>
        /// The Remote X.509 certificate chain.
        /// </summary>
        public X509Chain?                                                 RemoteCertificateChain        { get; private set; }

        /// <summary>
        /// The remote TLS certificate validator.
        /// </summary>
        public RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator    { get; }

        /// <summary>
        /// A delegate to select a TLS client certificate.
        /// </summary>
        public LocalCertificateSelectionHandler?                          LocalCertificateSelector      { get; }

        /// <summary>
        /// The TLS client certificate to use of HTTP authentication.
        /// </summary>
        public X509Certificate?                                           ClientCert                    { get; }

        /// <summary>
        /// The TLS protocol to use.
        /// </summary>
        public SslProtocols                                               TLSProtocol                   { get; }

        /// <summary>
        /// Prefer IPv4 instead of IPv6.
        /// </summary>
        public Boolean                                                    PreferIPv4                    { get; }

        /// <summary>
        /// An optional HTTP content type.
        /// </summary>
        public HTTPContentType?                                           ContentType                   { get; }

        /// <summary>
        /// The optional HTTP accept header.
        /// </summary>
        public AcceptTypes?                                               Accept                        { get; }

        /// <summary>
        /// The optional HTTP authentication to use.
        /// </summary>
        public IHTTPAuthentication?                                       Authentication                { get; }

        /// <summary>
        /// The HTTP user agent identification.
        /// </summary>
        public String                                                     HTTPUserAgent                 { get; }

        ///// <summary>
        ///// The optional HTTP authentication to use, e.g. HTTP Basic Auth.
        ///// </summary>
        //public IHTTPAuthentication?                                       HTTPAuthentication            { get; }

        /// <summary>
        /// The optional HTTP connection type.
        /// </summary>
        public ConnectionType?                                            Connection                    { get; }

        /// <summary>
        /// The timeout for upstream requests.
        /// </summary>
        public TimeSpan                                                   RequestTimeout                { get; set; }

        /// <summary>
        /// The delay between transmission retries.
        /// </summary>
        public TransmissionRetryDelayDelegate                             TransmissionRetryDelay        { get; }

        /// <summary>
        /// The size of the internal HTTP client buffers.
        /// </summary>
        public UInt32                                                     InternalBufferSize            { get; }

        /// <summary>
        /// The maximum number of retries when communicationg with the remote HTTP service.
        /// </summary>
        public UInt16                                                     MaxNumberOfRetries            { get; }

        /// <summary>
        /// Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.
        /// </summary>
        public Boolean                                                    UseHTTPPipelining             { get; }

        /// <summary>
        /// An optional description of this HTTP client.
        /// </summary>
        public I18NString                                                 Description                   { get; set; }

        /// <summary>
        /// Disable any logging.
        /// </summary>
        public Boolean                                                    DisableLogging                { get; }

        /// <summary>
        /// The HTTP client logger.
        /// </summary>
        public HTTPClientLogger?                                          HTTPLogger                    { get; set; }

        /// <summary>
        /// The DNS client defines which DNS servers to use.
        /// </summary>
        public DNSClient                                                  DNSClient                     { get; }


        #region TCP Socket

        #region Available

        /// <summary>
        /// The amount of data waiting to be read from the network stack.
        /// </summary>
        public Int32 Available
            => tcpSocket?.Available ?? 0;

        #endregion

        #region Connected

        /// <summary>
        /// Wether the HTTP client is connected to the remote server or not.
        /// </summary>
        public Boolean Connected

            => tcpSocket?.Connected ?? false;

        #endregion

        #region SendTimeout

        /// <summary>
        /// The send timeout value of the connection.
        /// </summary>
        public TimeSpan SendTimeout
        {

            get
            {

                var result = tcpSocket?.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout);

                if (result is Int32 sendTimeout && sendTimeout >= 0)
                    return TimeSpan.FromMilliseconds(sendTimeout);

                return TimeSpan.Zero;

            }

            set
            {
                tcpSocket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, (Int32) value.TotalMilliseconds);
            }

        }

        #endregion

        #region ReceiveTimeout

        /// <summary>
        /// The receive timeout value of the connection.
        /// </summary>
        public TimeSpan ReceiveTimeout
        {

            get
            {

                var result = tcpSocket?.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout);

                if (result is Int32 sendTimeout && sendTimeout >= 0)
                    return TimeSpan.FromMilliseconds(sendTimeout);

                return TimeSpan.Zero;

            }

            set
            {
                tcpSocket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (Int32) value.TotalMilliseconds);
            }

        }

        #endregion

        #region SendBufferSize

        /// <summary>
        /// The size of the underlying send buffer in bytes.
        /// </summary>
        public UInt64 SendBufferSize
        {

            get
            {

                var result = tcpSocket?.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer);

                if (result is Int32 sendBufferSize && sendBufferSize >= 0)
                    return (UInt64) sendBufferSize;

                return 0;

            }

            set
            {

                tcpSocket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, (Int32) value);

            }

        }

        #endregion

        #region ReceiveBufferSize

        /// <summary>
        /// The size of the underlying receive buffer in bytes.
        /// </summary>
        public UInt64 ReceiveBufferSize
        {

            get
            {

                var result = tcpSocket?.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer);

                if (result is Int32 receiveBufferSize && receiveBufferSize >= 0)
                    return (UInt64) receiveBufferSize;

                return 0;

            }

            set
            {
                tcpSocket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, (Int32) value);
            }

        }

        #endregion

        #region LingerState
        public LingerOption? LingerState
        {
            get
            {

                if (tcpSocket is not null)
                    return tcpSocket.LingerState;

                return null;

            }
            set
            {
                if (tcpSocket is not null && value is not null)
                    tcpSocket.LingerState = value;
            }
        }

        #endregion

        #region NoDelay

        /// <summary>
        /// Whether to use the Nagle algorithm.
        /// </summary>
        public Boolean NoDelay
        {
            get
            {

                var result = tcpSocket?.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay);

                if (result is Int32 noDelay)
                    return noDelay != 0;

                return false;

            }
            set
            {
                tcpSocket?.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, value ? 1 : 0);
            }
        }

        #endregion

        #region TTL

        /// <summary>
        /// Setting the "time to live"/hop count of IP data packets.
        /// </summary>
        public Byte TTL
        {
            get
            {

                if (tcpSocket is not null)
                    return (Byte) tcpSocket.Ttl;

                return 0;

            }
            set
            {

                if (tcpSocket is not null)
                    tcpSocket.Ttl = value;

            }
        }

        #endregion

        #endregion

        #endregion

        #region Events

        public delegate Task OnDataReadDelegate(TimeSpan Time, UInt64 BytesRead, UInt64? BytesExpected = null);

        public event OnDataReadDelegate? OnDataRead;



        public delegate Task OnChunkDataReadDelegate(TimeSpan Time, UInt64 BlockNumber, Byte[] BlockData, UInt32 BlockLength, UInt64 CurrentTotalBytes);

        public event OnChunkDataReadDelegate? OnChunkDataRead;



        public delegate Task OnChunkBlockFoundDelegate(TimeSpan                           Time,
                                                       UInt32                             ChunkNumber,
                                                       UInt32                             ChunkLength,
                                                       Dictionary<String, List<String>>?  ChunkExtensions,
                                                       Byte[]                             ChunkData,
                                                       UInt64                             TotalBytes);

        public event OnChunkBlockFoundDelegate? OnChunkBlockFound;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract HTTP client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this HTTP client.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use of HTTP authentication.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="ContentType">An optional HTTP content type.</param>
        /// <param name="Accept">The optional HTTP accept header.</param>
        /// <param name="HTTPAuthentication">The optional HTTP authentication to use.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="Connection">The optional HTTP connection type.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">An optional maximum number of transmission retries for HTTP request.</param>
        /// <param name="InternalBufferSize">An optional size of the internal HTTP client buffers.</param>
        /// <param name="UseHTTPPipelining">Whether to pipeline multiple HTTP request through a single HTTP/TCP connection.</param>
        /// <param name="DisableLogging">Whether to disable all logging.</param>
        /// <param name="HTTPLogger">An optional delegate to log HTTP(S) requests and responses.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        protected AHTTPClient(URL                                                        RemoteURL,
                              HTTPHostname?                                              VirtualHostname              = null,
                              I18NString?                                                Description                  = null,
                              Boolean?                                                   PreferIPv4                   = null,
                              RemoteTLSServerCertificateValidationHandler<IHTTPClient>?  RemoteCertificateValidator   = null,
                              LocalCertificateSelectionHandler?                          LocalCertificateSelector     = null,
                              X509Certificate?                                           ClientCert                   = null,
                              SslProtocols?                                              TLSProtocol                  = null,
                              HTTPContentType?                                           ContentType                  = null,
                              AcceptTypes?                                               Accept                       = null,
                              IHTTPAuthentication?                                       HTTPAuthentication           = null,
                              String?                                                    HTTPUserAgent                = DefaultHTTPUserAgent,
                              ConnectionType?                                            Connection                   = null,
                              TimeSpan?                                                  RequestTimeout               = null,
                              TransmissionRetryDelayDelegate?                            TransmissionRetryDelay       = null,
                              UInt16?                                                    MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                              UInt32?                                                    InternalBufferSize           = DefaultInternalBufferSize,
                              Boolean?                                                   UseHTTPPipelining            = null,
                              Boolean?                                                   DisableLogging               = false,
                              HTTPClientLogger?                                          HTTPLogger                   = null,
                              DNSClient?                                                 DNSClient                    = null)
        {

            this.RemoteURL                   = RemoteURL;
            this.VirtualHostname             = VirtualHostname;
            this.Description                 = Description            ?? I18NString.Empty;
            this.PreferIPv4                  = PreferIPv4             ?? false;
            this.RemoteCertificateValidator  = RemoteCertificateValidator;
            this.LocalCertificateSelector    = LocalCertificateSelector;
            this.ClientCert                  = ClientCert;
            this.TLSProtocol                 = TLSProtocol            ?? SslProtocols.Tls12|SslProtocols.Tls13;
            this.ContentType                 = ContentType;
            this.Accept                      = Accept;
            this.Authentication              = HTTPAuthentication;
            this.HTTPUserAgent               = HTTPUserAgent          ?? DefaultHTTPUserAgent;
            this.Connection                  = Connection;
            this.RequestTimeout              = RequestTimeout         ?? DefaultRequestTimeout;
            this.TransmissionRetryDelay      = TransmissionRetryDelay ?? (retryCounter => TimeSpan.FromSeconds(retryCounter * retryCounter * DefaultTransmissionRetryDelay.TotalSeconds));
            this.MaxNumberOfRetries          = MaxNumberOfRetries     ?? DefaultMaxNumberOfRetries;
            this.InternalBufferSize          = InternalBufferSize     ?? DefaultInternalBufferSize;
            this.UseHTTPPipelining           = UseHTTPPipelining      ?? false;
            this.DisableLogging              = DisableLogging         ?? false;
            this.HTTPLogger                  = HTTPLogger;
            this.DNSClient                   = DNSClient              ?? new DNSClient();

            this.RemotePort                  = RemoteURL.Port         ?? (RemoteURL.Protocol == URLProtocols.http ||
                                                                          RemoteURL.Protocol == URLProtocols.ws
                                                                             ? IPPort.HTTP
                                                                             : IPPort.HTTPS);

            if (this.LocalCertificateSelector is null && this.ClientCert is not null)
                this.LocalCertificateSelector = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => this.ClientCert;

        }

        #endregion


        #region CreateRequest (HTTPMethod, HTTPPath, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public HTTPRequest.Builder CreateRequest(HTTPMethod                    HTTPMethod,
                                                 HTTPPath                      HTTPPath,
                                                 QueryString?                  QueryString         = null,
                                                 AcceptTypes?                  Accept              = null,
                                                 IHTTPAuthentication?          Authentication      = null,
                                                 String?                       UserAgent           = null,
                                                 ConnectionType?               Connection          = null,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                 CancellationToken             CancellationToken   = default)
{

            var builder = new HTTPRequest.Builder(this, CancellationToken) {
                              Host           = HTTPHostname.Parse((VirtualHostname ?? RemoteURL.Hostname) + (RemoteURL.Port.HasValue && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS ? ":" + RemoteURL.Port.ToString() : String.Empty)),
                              HTTPMethod     = HTTPMethod,
                              Path           = HTTPPath,
                              QueryString    = QueryString ?? QueryString.Empty,
                              Authorization  = Authentication,
                              UserAgent      = UserAgent   ?? HTTPUserAgent,
                              Connection     = Connection
                          };

            if (Accept is not null)
                builder.Accept = Accept;

            RequestBuilder?.Invoke(builder);

            return builder;

        }

        #endregion

        #region CreateRequest (HTTPMethod, HTTPPath,   Content, ContentType, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method.</param>
        /// <param name="HTTPPath">An HTTP path.</param>
        /// <param name="Content">An HTTP content.</param>
        /// <param name="ContentType">An HTTP content type.</param>
        /// <param name="QueryString">An optional HTTP Query String.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public HTTPRequest.Builder CreateRequest(HTTPMethod                    HTTPMethod,
                                                 HTTPPath                      HTTPPath,
                                                 Byte[]                        Content,
                                                 HTTPContentType               ContentType,
                                                 QueryString?                  QueryString         = null,
                                                 AcceptTypes?                  Accept              = null,
                                                 IHTTPAuthentication?          Authentication      = null,
                                                 String?                       UserAgent           = null,
                                                 ConnectionType?               Connection          = null,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                 CancellationToken             CancellationToken   = default)
        {

            var builder = new HTTPRequest.Builder(this, CancellationToken) {
                              Host           = HTTPHostname.Parse((VirtualHostname ?? RemoteURL.Hostname) + (RemoteURL.Port.HasValue && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS ? ":" + RemoteURL.Port.ToString() : String.Empty)),
                              HTTPMethod     = HTTPMethod,
                              Path           = HTTPPath,
                              QueryString    = QueryString ?? QueryString.Empty,
                              Authorization  = Authentication,
                              Content        = Content,
                              ContentType    = ContentType,
                              UserAgent      = UserAgent   ?? HTTPUserAgent,
                              Connection     = Connection
                          };

            if (Accept is not null)
                builder.Accept = Accept;

            RequestBuilder?.Invoke(builder);

            return builder;

        }

        #endregion

        #region CreateRequest (HTTPMethod, RequestURL, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method.</param>
        /// <param name="RequestURL">The URL of this request.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public HTTPRequest.Builder CreateRequest(HTTPMethod                    HTTPMethod,
                                                 URL                           RequestURL,
                                                 AcceptTypes?                  Accept              = null,
                                                 IHTTPAuthentication?          Authentication      = null,
                                                 String?                       UserAgent           = null,
                                                 ConnectionType?               Connection          = null,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                 CancellationToken             CancellationToken   = default)
        {

            var builder = new HTTPRequest.Builder(this, CancellationToken) {
                              Host           = HTTPHostname.Parse((VirtualHostname ?? RemoteURL.Hostname) + (RemoteURL.Port.HasValue && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS ? ":" + RemoteURL.Port.ToString() : String.Empty)),
                              HTTPMethod     = HTTPMethod,
                              Path           = RequestURL.Path,
                              QueryString    = RequestURL.QueryString ?? QueryString.Empty,
                              Authorization  = Authentication,
                              UserAgent      = UserAgent              ?? HTTPUserAgent,
                              Connection     = Connection
                          };

            if (Accept is not null)
                builder.Accept = Accept;

            RequestBuilder?.Invoke(builder);

            return builder;

        }

        #endregion

        #region CreateRequest (HTTPMethod, RequestURL, Content, ContentType, ...)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">An HTTP method.</param>
        /// <param name="RequestURL">The URL of this request.</param>
        /// <param name="Content">An HTTP content.</param>
        /// <param name="ContentType">An HTTP content type.</param>
        /// <param name="Accept">An optional HTTP accept header.</param>
        /// <param name="Authentication">An optional HTTP authentication.</param>
        /// <param name="UserAgent">An optional HTTP user agent.</param>
        /// <param name="Connection">An optional HTTP connection type.</param>
        /// <param name="RequestBuilder">A delegate to configure the new HTTP request builder.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public HTTPRequest.Builder CreateRequest(HTTPMethod                    HTTPMethod,
                                                 URL                           RequestURL,
                                                 Byte[]                        Content,
                                                 HTTPContentType               ContentType,
                                                 AcceptTypes?                  Accept              = null,
                                                 IHTTPAuthentication?          Authentication      = null,
                                                 String?                       UserAgent           = null,
                                                 ConnectionType?               Connection          = null,
                                                 Action<HTTPRequest.Builder>?  RequestBuilder      = null,
                                                 CancellationToken             CancellationToken   = default)
        {

            var builder = new HTTPRequest.Builder(this, CancellationToken) {
                              Host           = HTTPHostname.Parse((VirtualHostname ?? RemoteURL.Hostname) + (RemoteURL.Port.HasValue && RemoteURL.Port != IPPort.HTTP && RemoteURL.Port != IPPort.HTTPS ? ":" + RemoteURL.Port.ToString() : String.Empty)),
                              HTTPMethod     = HTTPMethod,
                              Path           = RequestURL.Path,
                              QueryString    = RequestURL.QueryString ?? QueryString.Empty,
                              Authorization  = Authentication,
                              Content        = Content,
                              ContentType    = ContentType,
                              UserAgent      = UserAgent              ?? HTTPUserAgent,
                              Connection     = Connection
            };

            if (Accept is not null)
                builder.Accept = Accept;

            RequestBuilder?.Invoke(builder);

            return builder;

        }

        #endregion


        #region Execute (HTTPRequestDelegate, RequestLogDelegate = null, ResponseLogDelegate = null, Timeout = null, CancellationToken = null)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="HTTPRequestDelegate">A delegate for producing a HTTP request for a given HTTP client.</param>
        /// <param name="RequestLogDelegate">A delegate for logging the HTTP request.</param>
        /// <param name="ResponseLogDelegate">A delegate for logging the HTTP request/response.</param>
        /// 
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional HTTP request timeout.</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public Task<HTTPResponse> Execute(Func<AHTTPClient, HTTPRequest>  HTTPRequestDelegate,
                                          ClientRequestLogHandler?        RequestLogDelegate    = null,
                                          ClientResponseLogHandler?       ResponseLogDelegate   = null,

                                          EventTracking_Id?               EventTrackingId       = null,
                                          TimeSpan?                       RequestTimeout        = null,
                                          Byte                            NumberOfRetry         = 0,
                                          CancellationToken               CancellationToken     = default)

            => Execute(
                   HTTPRequestDelegate(this),
                   RequestLogDelegate,
                   ResponseLogDelegate,

                   EventTrackingId,
                   RequestTimeout,
                   NumberOfRetry,
                   CancellationToken
               );

        #endregion

        #region Execute (Request,             RequestLogDelegate = null, ResponseLogDelegate = null, Timeout = null, CancellationToken = null)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="RequestLogDelegate">A delegate for logging the HTTP request.</param>
        /// <param name="ResponseLogDelegate">A delegate for logging the HTTP request/response.</param>
        /// 
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout.</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async Task<HTTPResponse> Execute(HTTPRequest                Request,
                                                ClientRequestLogHandler?   RequestLogDelegate    = null,
                                                ClientResponseLogHandler?  ResponseLogDelegate   = null,

                                                EventTracking_Id?          EventTrackingId       = null,
                                                TimeSpan?                  RequestTimeout        = null,
                                                Byte                       NumberOfRetry         = 0,
                                                CancellationToken          CancellationToken     = default)

        {

            #region Data

            var timings          = new HTTPClientTimings(Request);
            var restart          = false;
            var httpHeaderBytes  = Array.Empty<Byte>();
            var httpBodyBytes    = Array.Empty<Byte>();
            var clientClose      = false;

            RequestTimeout     ??= Request.Timeout ?? this.RequestTimeout;

            HTTPResponse? Response = null;

            #endregion

            #region Call the optional HTTP request log delegate

            timings.RequestLogging1 = timings.Elapsed;

            try
            {

                if (RequestLogDelegate is not null)
                    await Task.WhenAll(RequestLogDelegate.GetInvocationList().
                                       Cast<ClientRequestLogHandler>().
                                       Select(async e => {
                                           await e((timings.Start + timings.RequestLogging1.Value).DateTime,
                                                   this,
                                                   Request);
                                       })).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                timings.AddError($"{nameof(RequestLogDelegate)}: {e.Message}");
            }

            timings.RequestLogging2 = timings.Elapsed;

            #endregion


            try
            {

                do
                {

                    if (timings.RestartCounter > 0)
                        timings.AddError($"{timings.RestartCounter}. restart!");

                    timings.RestartCounter++;
                    restart = false;

                    #region Create TCP connection (maybe also some DNS lookups)

                    if (tcpSocket is null)
                    {

                        System.Net.IPEndPoint? remoteIPEndPoint = null;

                        if (RemoteIPAddress is null)
                        {

                            #region Localhost

                            if      (IPAddress.IsIPv4Localhost(RemoteURL.Hostname))
                                RemoteIPAddress = IPv4Address.Localhost;

                            else if (IPAddress.IsIPv6Localhost(RemoteURL.Hostname))
                                RemoteIPAddress = IPv6Address.Localhost;

                            else if (IPAddress.IsIPv4(RemoteURL.Hostname.Name))
                                RemoteIPAddress = IPv4Address.Parse(RemoteURL.Hostname.Name.FullName);

                            else if (IPAddress.IsIPv6(RemoteURL.Hostname.Name))
                                RemoteIPAddress = IPv6Address.Parse(RemoteURL.Hostname.Name.FullName);

                            #endregion

                            #region DNS lookup...

                            if (RemoteIPAddress is null)
                            {

                                var IPv4AddressLookupTask = DNSClient.
                                                                Query<A>   (RemoteURL.Hostname.Name, CancellationToken).
                                                                ContinueWith(query => query.Result.Select(ARecord    => ARecord.   IPv4Address));

                                var IPv6AddressLookupTask = DNSClient.
                                                                Query<AAAA>(RemoteURL.Hostname.Name, CancellationToken).
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

                                timings.DNSLookup = timings.Elapsed;

                            }

                            #endregion

                        }

                        if (RemoteIPAddress is not null &&
                            RemotePort      is not null)
                        {

                            remoteIPEndPoint = new System.Net.IPEndPoint(
                                                   new System.Net.IPAddress(RemoteIPAddress.GetBytes()),
                                                   RemotePort.Value.ToInt32()
                                               );

                            if (RemoteIPAddress.IsIPv4)
                                tcpSocket = new Socket(
                                                AddressFamily.InterNetwork,
                                                SocketType.Stream,
                                                ProtocolType.Tcp
                                            );

                            if (RemoteIPAddress.IsIPv6)
                                tcpSocket = new Socket(
                                                AddressFamily.InterNetworkV6,
                                                SocketType.Stream,
                                                ProtocolType.Tcp
                                            );

                            if (tcpSocket is not null)
                            {
                                try
                                {

                                    NoDelay         = true;
                                    SendTimeout     = RequestTimeout.Value;
                                    ReceiveTimeout  = RequestTimeout.Value;

                                    await tcpSocket.ConnectAsync(remoteIPEndPoint);

                                    ReceiveTimeout  = RequestTimeout.Value;

                                }
                                catch (Exception e)
                                {
                                    DebugX.Log($"TCP Connection to {RemoteURL.Hostname} ({RemoteIPAddress}) : {RemotePort} failed: {e.Message}");
                                    timings.AddError($"TCP Connection to {RemoteURL.Hostname} ({RemoteIPAddress}) : {RemotePort} failed: {e.Message}");
                                    tcpSocket  = null;
                                    restart    = true;
                                }
                            }

                            timings.Connected = timings.Elapsed;

                        }
                        else
                        {
                            tcpSocket  = null;
                            restart    = true;
                        }

                    }

                    tcpStream = tcpSocket is not null
                                    ? new MyNetworkStream(tcpSocket, true) {
                                          ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds
                                      }
                                    : null;

                    #endregion

                    #region Create (Crypto-)Stream

                    if (RemoteURL.Protocol == URLProtocols.https &&
                        RemoteCertificateValidator is not null   &&
                        tcpStream                  is not null)
                    {

                        if (tlsStream is null)
                        {

                            var remoteCertificateValidatorErrors = new List<String>();

                            tlsStream = new SslStream(
                                            innerStream:                         tcpStream,
                                            leaveInnerStreamOpen:                false,
                                            userCertificateValidationCallback:  (sender,
                                                                                 certificate,
                                                                                 chain,
                                                                                 policyErrors) => {

                                                                                     RemoteCertificate       = certificate is not null
                                                                                                                   ? new X509Certificate2(certificate)
                                                                                                                   : null;

                                                                                     RemoteCertificateChain  = chain;

                                                                                     var check               = RemoteCertificateValidator(
                                                                                                                   sender,
                                                                                                                   RemoteCertificate,
                                                                                                                   RemoteCertificateChain,
                                                                                                                   this,
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

                            httpStream = tlsStream;

                            try
                            {

                                await tlsStream.AuthenticateAsClientAsync(targetHost:                  RemoteURL.Hostname.Name.FullName,
                                                                          clientCertificates:          null,  // new X509CertificateCollection(new X509Certificate[] { ClientCert })
                                                                          enabledSslProtocols:         TLSProtocol,
                                                                          checkCertificateRevocation:  false);// true);

                                timings.TLSHandshake = timings.Elapsed;

                            }
                            catch (Exception e)
                            {

                                DebugX.Log($"TLS AuthenticateAsClientAsync to {RemoteURL.Hostname} ({RemoteIPAddress}) : {RemotePort} failed: {e.Message}");
                                timings.AddError($"TLS AuthenticateAsClientAsync to {RemoteURL.Hostname} ({RemoteIPAddress}) : {RemotePort} failed: {e.Message}");

                                foreach (var error in remoteCertificateValidatorErrors)
                                    timings.AddError(error);

                                tcpSocket  = null;
                                restart    = true;

                            }

                        }

                    }
                    else
                    {
                        tlsStream   = null;
                        httpStream  = tcpStream;
                    }

                    #endregion

                    if (httpStream is not null)
                        httpStream.ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                }
                while (restart && timings.RestartCounter < MaxNumberOfRetries);

                if (tcpStream is not null)
                {
                    Request.LocalSocket   =                IPSocket.FromIPEndPoint(tcpStream.Socket.LocalEndPoint)  ?? IPSocket.Zero;
                    Request.HTTPSource    = new HTTPSource(IPSocket.FromIPEndPoint(tcpStream.Socket.LocalEndPoint)  ?? IPSocket.Zero);
                    Request.RemoteSocket  =                IPSocket.FromIPEndPoint(tcpStream.Socket.RemoteEndPoint) ?? IPSocket.Zero;
                }

                if (httpStream is not null)
                {

                    #region Send request header

                    timings.RequestHeaderLength = (UInt64) Request.EntireRequestHeader.Length + 4;

                    await httpStream.WriteAsync($"{Request.EntireRequestHeader}\r\n\r\n".ToUTF8Bytes(), CancellationToken);

                    timings.WriteRequestHeader = timings.Elapsed;

                    #endregion

                    #region Send (optional) request body

                    var requestBodyLength = Request.HTTPBody is null
                                                ? Request.ContentLength.HasValue
                                                      ? (Int32) Request.ContentLength.Value
                                                      : 0
                                                : Request.ContentLength.HasValue
                                                      ? Math.Min((Int32) Request.ContentLength.Value,
                                                                 Request.HTTPBody.Length)
                                                      : Request.HTTPBody.Length;

                    if (Request.HTTPBody is not null && requestBodyLength > 0)
                    {
                        timings.RequestBodyLength = (UInt64) requestBodyLength;
                        await httpStream.WriteAsync(Request.HTTPBody, 0, requestBodyLength, CancellationToken);
                    }

                    await httpStream.FlushAsync(CancellationToken);

                    timings.WriteRequestBody = timings.Elapsed;

                    #endregion


                    #region Read at least the HTTP header

                    var currentDataLength    = 0;
                    var currentHeaderLength  = 0;
                    var httpHeaderEndsAt     = 0;
                    var receiveBuffer        = new Byte[InternalBufferSize];

                    do
                    {

                        currentDataLength = await httpStream.ReadAsync(receiveBuffer, CancellationToken);

                        if (currentDataLength > 0)
                        {

                            timings.DataReceived.Add(new Elapsed<UInt64>(timings.Elapsed, (UInt64) currentDataLength));

                            if (currentDataLength > 3 || currentHeaderLength > 3)
                            {

                                #region Find the \r\n\r\n separator between HTTP header and HTTP body

                                for (var pos = 3; pos < receiveBuffer.Length; pos++)
                                {
                                    if (receiveBuffer[pos    ] == 0x0a &&
                                        receiveBuffer[pos - 1] == 0x0d &&
                                        receiveBuffer[pos - 2] == 0x0a &&
                                        receiveBuffer[pos - 3] == 0x0d)
                                    {
                                        httpHeaderEndsAt = pos - 3;
                                        break;
                                    }
                                }

                                #endregion

                                #region We found the \r\n\r\n separator...

                                if (httpHeaderEndsAt > 0)
                                {

                                    Array.Resize(ref httpHeaderBytes, currentHeaderLength + httpHeaderEndsAt);
                                    Array.Copy(receiveBuffer, 0, httpHeaderBytes, currentHeaderLength, httpHeaderEndsAt);
                                    currentHeaderLength += httpHeaderEndsAt;

                                    // We already read a bit of the HTTP body!
                                    if (httpHeaderEndsAt + 4 < currentDataLength)
                                    {
                                        Array.Resize(ref httpBodyBytes, currentDataLength - 4 - httpHeaderEndsAt);
                                        Array.Copy(receiveBuffer, httpHeaderEndsAt + 4, httpBodyBytes, 0, httpBodyBytes.Length);
                                    }

                                }

                                #endregion

                                #region We did not find the \r\n\r\n separator... try to read next fragment!

                                else
                                {
                                    Array.Resize(ref httpHeaderBytes, currentHeaderLength + receiveBuffer.Length);
                                    Array.Copy(receiveBuffer, 0, httpHeaderBytes, currentHeaderLength, receiveBuffer.Length);
                                    currentHeaderLength += receiveBuffer.Length;
                                    //Thread.Sleep(1);
                                }

                                #endregion

                            }

                        }

                    } while (httpHeaderEndsAt == 0 &&
                             timings.Elapsed.TotalMilliseconds < httpStream.ReadTimeout);

                    #endregion

                    if (httpHeaderBytes.Any())
                    {

                        #region Try to parse the HTTP header

                        Response = HTTPResponse.Parse(ResponseHeader:       httpHeaderBytes.ToUTF8String(),
                                                      ResponseBody:         httpBodyBytes,
                                                      Request:              Request,
                                                      SubprotocolResponse:  null,
                                                      EventTrackingId:      EventTrackingId,
                                                      //Runtime:              timings.Elapsed, // Will be calculated internally!
                                                      NumberOfRetries:      NumberOfRetry,
                                                      CancellationToken:    CancellationToken);

                        timings.AddHTTPResponse(Response);

                        Response.ClientTimings   = timings;

                        timings.ResponseHeaderParsed = timings.Elapsed;

                        #endregion

                        #region A single fixed-length HTTP request -> read '$Content-Length' bytes...

                        //receiveBuffer = new Byte[50 * 1024 * 1024];

                        // Copy only the number of bytes given within
                        // the HTTP header element 'Content-Length'!
                        if (Response.ContentLength.HasValue &&
                            Response.ContentLength.Value > 0 &&
                            Response.HTTPBody is not null)
                        {

                            // Test via:
                            // var aaa = new HTTPClient("www.networksorcery.com").GET("/enp/rfc/rfc1000.txt").ExecuteReturnResult().Result;

                            var stillToRead  = (Int32) Response.ContentLength.Value - Response.HTTPBody.Length;
                            var isError      = false;

                            do
                            {

                                while (//TCPStream.DataAvailable &&  <= Does not work as expected!
                                       stillToRead > 0)
                                {

                                    try
                                    {

                                        currentDataLength = await httpStream.ReadAsync(receiveBuffer, 0, Math.Min(receiveBuffer.Length, stillToRead), CancellationToken);

                                        if (currentDataLength > 0)
                                        {

                                            timings.DataReceived.Add(new Elapsed<UInt64>(timings.Elapsed, (UInt64) currentDataLength));

                                            var oldSize = Response.HTTPBody.Length;
                                            Response.ResizeBody(oldSize + currentDataLength);
                                            Array.Copy(receiveBuffer, 0, Response.HTTPBody, oldSize, currentDataLength);
                                            stillToRead -= currentDataLength;

                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        timings.AddError($"StillToRead: {receiveBuffer.Length}, {stillToRead}: {e.Message}!");
                                        isError = true;
                                        break;
                                    }

                                }

                                OnDataRead?.Invoke(timings.Elapsed,
                                                   Response.ContentLength.Value - (UInt64) stillToRead,
                                                   Response.ContentLength.Value);

                                if (stillToRead <= 0)
                                    break;

                                Thread.Sleep(1);

                            }
                            while (timings.Elapsed.TotalMilliseconds < httpStream.ReadTimeout && !isError);

                            // Do a friendly close of the TCP connection to avoid TCP RST packets!
                            clientClose = true;

                        }

                        #endregion

                        //ToDo: HTTP/1.1 100 Continue

                        #region ...or chunked transport...

                        else if (Response.TransferEncoding == "chunked")
                        {

                            //DebugX.Log("HTTP Client: Chunked Transport detected...");

                            try
                            {

                                var blockNumber              = 1UL;
                                var decodedStream            = new MemoryStream();
                                var currentPosition          = 2U;
                                var lastPosition             = 0U;
                                var currentBlockNumber       = 0U;
                                var chunkedDecodingFinished  = 0;
                                var trailingHeaders          = new Dictionary<String, String?>();

                                Response.NewContentStream();
                                var chunkedStream            = new MemoryStream();

                                #region Maybe there is already some data

                                if (httpBodyBytes.Length > 0)
                                {

                                    chunkedStream.Write(httpBodyBytes, 0, httpBodyBytes.Length);

                                    OnChunkDataRead?.Invoke(timings.Elapsed,
                                                            blockNumber++,
                                                            httpBodyBytes,
                                                            (UInt32) httpBodyBytes.Length,
                                                            (UInt64) httpBodyBytes.Length);

                                }

                                #endregion

                                var chunkedArray = chunkedStream.ToArray();

                                do
                                {

                                    #region Read more data from network

                                    // Skip first read when we already have some HTTP body bytes!
                                    if (chunkedStream.Length == 0 || currentPosition > 2)
                                    {

                                        currentDataLength = httpStream.Read(receiveBuffer, 0, receiveBuffer.Length);

                                        if (currentDataLength > 0)
                                        {

                                            chunkedStream.Write(receiveBuffer, 0, currentDataLength);

                                            if (OnChunkDataRead is not null)
                                            {

                                                var blockData = new Byte[currentDataLength];
                                                Array.Copy(receiveBuffer, 0, blockData, 0, currentDataLength);

                                                OnChunkDataRead?.Invoke(timings.Elapsed,
                                                                        blockNumber++,
                                                                        blockData,
                                                                        (UInt32) currentDataLength,
                                                                        (UInt64) chunkedStream.Length);

                                            }

                                        }

                                    }

                                    #endregion

                                    #region Documentation

                                    // [size]n
                                    // [data]n
                                    // [size]n
                                    // [data]n
                                    // ...
                                    // 0n
                                    // n
                                    // [trailer fields]n
                                    // n

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

                                    #endregion

                                    #region Process chunks

                                    chunkedArray = chunkedStream.ToArray();

                                    while (currentPosition <= chunkedArray.Length)
                                    {

                                        if (chunkedArray[currentPosition - 1] == '\n' &&
                                            chunkedArray[currentPosition - 2] == '\r')
                                        {

                                            #region Read chunks

                                            if (chunkedDecodingFinished == 0)
                                            {

                                                currentBlockNumber++;

                                                var chunkInfo = ChunkInfos.Parse(chunkedArray,
                                                                                 lastPosition,
                                                                                 currentPosition - lastPosition - 2);

                                                #region The final chunk was received

                                                if (chunkInfo.Length == 0)
                                                {

                                                    OnChunkBlockFound?.Invoke(timings.Elapsed,
                                                                              currentBlockNumber,
                                                                              0,
                                                                              chunkInfo.Extensions,
                                                                              Array.Empty<Byte>(),
                                                                              (UInt64) decodedStream.Length);

                                                    Response.ContentStreamToArray(decodedStream);

                                                    chunkedDecodingFinished = 1;
                                                    lastPosition = currentPosition;
                                                    currentPosition += 1;

                                                }

                                                #endregion

                                                #region Read a chunk...

                                                //if (chunkedDecodingFinished == 0 &&
                                                //    currentPosition + chunkInfo.Length + 2 <= chunkedArray.Length)
                                                else if (currentPosition + chunkInfo.Length + 2 <= chunkedArray.Length)
                                                {

                                                    decodedStream.Write(chunkedArray, (Int32) currentPosition, (Int32) chunkInfo.Length);

                                                    if (OnChunkBlockFound is not null)
                                                    {

                                                        var chunkData = new Byte[chunkInfo.Length];
                                                        Array.Copy(chunkedArray, currentPosition, chunkData, 0, chunkInfo.Length);

                                                        await OnChunkBlockFound.Invoke(timings.Elapsed,
                                                                                       currentBlockNumber,
                                                                                       chunkInfo.Length,
                                                                                       chunkInfo.Extensions,
                                                                                       chunkData,
                                                                                       (UInt64)decodedStream.Length);

                                                    }

                                                    currentPosition += chunkInfo.Length + 2;
                                                    lastPosition = currentPosition;
                                                    currentPosition += 1;

                                                }

                                                #endregion

                                                else
                                                    break;

                                            }

                                            #endregion

                                            else
                                            {

                                                // 1. Now, continue to read lines from the connection. If you read a line that is just a CRLF, this indicates the end of the HTTP message. If the line contains text, then it's a trailing header.
                                                // 2. Continue reading trailing headers until you read an empty line.

                                                if (currentPosition - lastPosition == 2)
                                                {
                                                    chunkedDecodingFinished = 2;
                                                    break;
                                                }

                                                var trailingHeaderBuffer = new Byte[currentPosition - lastPosition - 2];
                                                Array.Copy(chunkedArray, lastPosition, trailingHeaderBuffer, 0, currentPosition - lastPosition - 2);

                                                var trailingHeader = trailingHeaderBuffer?.ToUTF8String()?.Trim()?.Split(':');

                                                if (trailingHeader is not null &&
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

                                    if (timings.Elapsed.TotalMilliseconds > httpStream.ReadTimeout)
                                        chunkedDecodingFinished = 3;

                                } while (chunkedDecodingFinished < 2);

                                if (chunkedDecodingFinished == 3)
                                    DebugX.Log("HTTP Client: Chunked decoding timeout!");
                                //else
                                //    DebugX.Log("HTTP Client: Chunked decoding finished!");

                                if (Response.TryGetHeaderField("Transfer-Encoding", out var transferEncoding))
                                {
                                    if (transferEncoding is "chunked")
                                        Response.RemoveHeaderField("Transfer-Encoding");
                                }

                                if (Response.TryGetHeaderField(HTTPHeaderField.Trailer, out var trailerHeaders) &&
                                    trailerHeaders is not null)
                                {

                                    // A sender MUST NOT generate a trailer that contains a field necessary for
                                    //   - message framing (e.g., Transfer-Encoding and Content-Length)
                                    //   - routing (e.g., Host)
                                    //   - request modifiers (e.g., controls and conditionals in Section 5 of [RFC7231])
                                    //   - authentication (e.g., see [RFC7235] and [RFC6265])
                                    //   - response control data (e.g., see Section 7.1 of [RFC7231])
                                    //   - or determining how to process the payload (e.g., Content-Encoding, Content-Type, Content-Range, and Trailer)
                                    var forbiddenTrailingHeaders  = new HashSet<String>(StringComparer.OrdinalIgnoreCase) {
                                        "Transfer-Encoding", "Content-Length",
                                        "Host",
                                        "Content-Encoding", "Content-Type", "Content-Range", "Trailer"
                                    };

                                    var validTrailingHeaders      = new List<String>();

                                    foreach (var trailerHeader in trailerHeaders.Split(',').
                                                                      Select(element => element?.Trim()).
                                                                      Where (element => element is not null && element.IsNotNullOrEmpty()))
                                    {
                                        if (trailerHeader is not null &&
                                            !forbiddenTrailingHeaders.Contains(trailerHeader))
                                        {

                                            validTrailingHeaders.Add(trailerHeader!);

                                            //ToDo: What to do with duplicate header fields?
                                            Response.SetHeaderField(trailerHeader,
                                                                    trailingHeaders[trailerHeader] ?? String.Empty);

                                            Response.RawHTTPHeader += "\r\n" +
                                                                      trailerHeader + ": " +
                                                                      trailingHeaders[trailerHeader];

                                        }
                                    }

                                }

                            }
                            catch (Exception e)
                            {
                                timings.AddError($"Chunked decoding failed: {e.Message}");
                            }

                        }

                        #endregion

                        #region ...or just connect HTTP stream to network stream!

                        else
                            Response.ContentStreamToArray();

                        #endregion

                        #region Close connection if requested!

                        if (Response.Connection is null                 ||
                            Response.Connection == ConnectionType.Close ||
                            clientClose)
                        {

                            if (tlsStream is not null)
                            {
                                tlsStream.Close();
                                tlsStream = null;
                            }

                            if (tcpSocket is not null)
                            {
                                tcpSocket.Close();
                                //TCPClient.Dispose();
                                tcpSocket = null;
                            }

                            httpStream = null;

                        }

                        #endregion

                    }

                }

            }

            #region HTTP timeout exception handling

            catch (HTTPTimeoutException e)
            {

                #region Create a HTTP response for the exception...

                Response = new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.RequestTimeout,
                               ContentType     = HTTPContentType.Application.JSON_UTF8,
                               Content         = JSONObject.Create(
                                                     new JProperty("timeout",     (Int32) e.Timeout.TotalMilliseconds),
                                                     new JProperty("message",     e.Message),
                                                     new JProperty("stackTrace",  e.StackTrace),
                                                     new JProperty("timings",     timings.ToString())
                                                 ).ToUTF8Bytes()

                           };

                #endregion

                timings.AddError($"HTTP Timeout Exception: {e.Message}");

                if (tlsStream is not null)
                {
                    tlsStream.Close();
                    tlsStream = null;
                }

                if (tcpSocket is not null)
                {
                    tcpSocket.Close();
                    //TCPClient.Dispose();
                    tcpSocket = null;
                }

            }

            #endregion

            #region Exception handling

            catch (Exception e)
            {

                #region Create a HTTP response for the exception...

                while (e.InnerException is not null)
                    e = e.InnerException;

                Response = new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.BadRequest,
                               ContentType     = HTTPContentType.Application.JSON_UTF8,
                               Content         = JSONObject.Create(
                                                     new JProperty("message",     e.Message),
                                                     new JProperty("stackTrace",  e.StackTrace),
                                                     new JProperty("timings",     timings.ToString())
                                                 ).ToUTF8Bytes()
                           };

                #endregion

                timings.AddError($"Exception: {e.Message}");

                if (tlsStream is not null)
                {
                    tlsStream.Close();
                    tlsStream = null;
                }

                if (tcpSocket is not null)
                {
                    tcpSocket.Close();
                    //TCPClient.Dispose();
                    tcpSocket = null;
                }

            }

            #endregion


            if (Response is null)
            {

                Response = new HTTPResponse.Builder(Request) {
                             HTTPStatusCode  = HTTPStatusCode.BadRequest,
                             ContentType     = HTTPContentType.Application.JSON_UTF8,
                             Content         = JSONObject.Create(
                                                   new JProperty("message",  "Something wicked happened!"),
                                                   new JProperty("timings",  timings.ToString())
                                               ).ToUTF8Bytes()
                         };

                timings.AddError("Something wicked happened!");

            }


            #region Call the optional HTTP response log delegate

            timings.ResponseLogging1 = timings.Elapsed;

            try
            {

                if (ResponseLogDelegate is not null)
                    await Task.WhenAll(ResponseLogDelegate.GetInvocationList().
                                       Cast<ClientResponseLogHandler>().
                                       Select(e => e((timings.Start + timings.ResponseLogging1.Value).DateTime,
                                                     this,
                                                     Request,
                                                     Response))).
                                       ConfigureAwait(false);

            }
            catch (Exception e2)
            {
                DebugX.Log(e2, nameof(HTTPClient) + "." + nameof(ResponseLogDelegate));
            }

            timings.ResponseLogging2 = timings.Elapsed;

            #endregion

            return Response;

        }

        #endregion


        #region Close()

        public void Close()
        {

            try
            {
                if (httpStream is not null)
                {
                    httpStream.Close();
                    httpStream.Dispose();
                }
            }
            catch
            { }

            try
            {
                if (tlsStream is not null)
                {
                    tlsStream.Close();
                    tlsStream.Dispose();
                }
            }
            catch
            { }

            try
            {
                if (tcpStream is not null)
                {
                    tcpStream.Close();
                    tcpStream.Dispose();
                }
            }
            catch
            { }

            try
            {
                if (tcpSocket is not null)
                {
                    tcpSocket.Close();
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
        public virtual void Dispose()
        {
            Close();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{GetType().Name} {RemoteURL}";

        #endregion


    }

}
