/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{


    public static class Helpers
    {

        public static Int32 ReadTEBlockLength(this Byte[] TEContent, Int32 Position, Int32 TELength)
        {

            var TEBlockLength = new Byte[TELength];
            Array.Copy(TEContent, Position, TEBlockLength, 0, TELength);

            return Convert.ToInt32(TEBlockLength.ToUTF8String(), // Hex-String
                                   16);

        }


    }


    public class TimeoutException : Exception
    {

        public TimeSpan Timeout { get; }

        public TimeoutException(TimeSpan Timeout)

            : base("Could not read from the TCP stream for " + Timeout.TotalMilliseconds.ToString() + "ms!")

        {

            this.Timeout = Timeout;

            DebugX.Log("Could not read from the TCP stream for " + Timeout.TotalMilliseconds.ToString() + "ms!");

        }

    }


    /// <summary>
    /// A http client.
    /// </summary>
    public class HTTPClient : IHTTPClient
    {

        #region Data

        private         Socket          TCPSocket;
        private         NetworkStream   TCPStream;
        private         SslStream       TLSStream;
        private         Stream          HTTPStream;

        private static  Regex           IPv4AddressRegExpr     = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");

        /// <summary>
        /// The default HTTPS user agent.
        /// </summary>
        public  const   String          DefaultUserAgent       = "Vanaheimr Hermod HTTP Client v0.1";

        /// <summary>
        /// The default HTTP user agent.
        /// </summary>
        public  static  TimeSpan        DefaultRequestTimeout  = TimeSpan.FromSeconds(60);

        #endregion

        #region Properties

        /// <summary>
        /// The Hostname to which the HTTPClient connects.
        /// </summary>
        public HTTPHostname     Hostname            { get; }

        /// <summary>
        /// The virtual hostname which the HTTPClient sends.
        /// </summary>
        public HTTPHostname?    VirtualHostname     { get; }

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        public IIPAddress       RemoteIPAddress     { get; private set; }

        /// <summary>
        /// The IP port to connect to.
        /// </summary>
        public IPPort           RemotePort          { get; }

        /// <summary>
        /// The IP socket to connect to.
        /// </summary>
        public IPSocket         RemoteSocket
            => new IPSocket(RemoteIPAddress, RemotePort);

        /// <summary>
        /// The default server name.
        /// </summary>
        public String           UserAgent           { get; }

        /// <summary>
        /// The default server name.
        /// </summary>
        public DNSClient        DNSClient           { get; }

        //      public X509Certificate2 ServerCert { get; set; }

        /// <summary>
        /// A delegate to verify the remote TLS certificate.
        /// </summary>
        public RemoteCertificateValidationCallback RemoteCertificateValidator { get; }

        public LocalCertificateSelectionCallback LocalCertificateSelector { get; }

        public X509Certificate ClientCert           { get; }

        //    public LocalCertificateSelectionCallback ClientCertificateSelector { get; set; }

        public TimeSpan?       RequestTimeout       {get; }






        public Int32 Available
            => TCPSocket.Available;

        public Boolean Connected
            => TCPSocket.Connected;

        public LingerOption LingerState {
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

        #endregion

        #region Constructor(s)

        #region HTTPClient(RemoteIPAddress, ...)

        /// <summary>
        /// Create a new HTTP client using the given optional parameters.
        /// </summary>
        /// <param name="RemoteIPAddress">The remote IP address to connect to.</param>
        /// <param name="RemotePort">The remote IP port to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="LocalCertificateSelector">Selects the local certificate used for authentication.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="UserAgent">The HTTP user agent to use.</param>
        /// <param name="RequestTimeout">An optional default HTTP request timeout.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(IIPAddress                           RemoteIPAddress,
                          IPPort?                              RemotePort                   = null,
                          RemoteCertificateValidationCallback  RemoteCertificateValidator   = null,
                          LocalCertificateSelectionCallback    LocalCertificateSelector     = null,
                          X509Certificate                      ClientCert                   = null,
                          String                               UserAgent                    = DefaultUserAgent,
                          TimeSpan?                            RequestTimeout               = null,
                          DNSClient                            DNSClient                    = null)
        {

            this.RemoteIPAddress             = RemoteIPAddress;
            this.Hostname                    = HTTPHostname.Parse(RemoteIPAddress.ToString());
            this.RemotePort                  = RemotePort     ?? IPPort.HTTP;
            this.RemoteCertificateValidator  = RemoteCertificateValidator;
            this.LocalCertificateSelector    = LocalCertificateSelector;
            this.ClientCert                  = ClientCert;
            this.UserAgent                   = UserAgent      ?? DefaultUserAgent;
            this.RequestTimeout              = RequestTimeout ?? DefaultRequestTimeout;
            this.DNSClient                   = DNSClient      ?? new DNSClient();

        }

        #endregion

        #region HTTPClient(Socket, ...)

        /// <summary>
        /// Create a new HTTP client using the given optional parameters.
        /// </summary>
        /// <param name="RemoteSocket">The remote IP socket to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="LocalCertificateSelector">Selects the local certificate used for authentication.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="UserAgent">The HTTP user agent to use.</param>
        /// <param name="RequestTimeout">An optional default HTTP request timeout.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(IPSocket                             RemoteSocket,
                          RemoteCertificateValidationCallback  RemoteCertificateValidator   = null,
                          LocalCertificateSelectionCallback    LocalCertificateSelector     = null,
                          X509Certificate                      ClientCert                   = null,
                          String                               UserAgent                    = DefaultUserAgent,
                          TimeSpan?                            RequestTimeout               = null,
                          DNSClient                            DNSClient                    = null)

            : this(RemoteSocket.IPAddress,
                   RemoteSocket.Port,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   ClientCert,
                   UserAgent      ?? DefaultUserAgent,
                   RequestTimeout ?? DefaultRequestTimeout,
                   DNSClient      ?? new DNSClient())

        { }

        #endregion

        #region HTTPClient(RemoteHost, ...)

        /// <summary>
        /// Create a new HTTP client using the given optional parameters.
        /// </summary>
        /// <param name="RemoteHost">The remote hostname to connect to.</param>
        /// <param name="VirtualHostname">The virtual hostname which the HTTPClient sends.</param>
        /// <param name="RemotePort">The remote HTTP port to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="LocalCertificateSelector">Selects the local certificate used for authentication.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="UserAgent">The HTTP user agent to use.</param>
        /// <param name="RequestTimeout">An optional default HTTP request timeout.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(HTTPHostname                         RemoteHost,
                          IPPort?                              RemotePort                   = null,
                          HTTPHostname?                        VirtualHostname              = null,
                          RemoteCertificateValidationCallback  RemoteCertificateValidator   = null,
                          LocalCertificateSelectionCallback    LocalCertificateSelector     = null,
                          X509Certificate                      ClientCert                   = null,
                          String                               UserAgent                    = DefaultUserAgent,
                          TimeSpan?                            RequestTimeout               = null,
                          DNSClient                            DNSClient                    = null)
        {

            this.Hostname                    = RemoteHost;
            this.VirtualHostname             = VirtualHostname;
            this.RemotePort                  = RemotePort      ?? IPPort.HTTP;
            this.RemoteCertificateValidator  = RemoteCertificateValidator;
            this.LocalCertificateSelector    = LocalCertificateSelector;
            this.ClientCert                  = ClientCert;
            this.UserAgent                   = UserAgent       ?? DefaultUserAgent;
            this.RequestTimeout              = RequestTimeout  ?? DefaultRequestTimeout;
            this.DNSClient                   = DNSClient       ?? new DNSClient();

        }

        #endregion

        #endregion


        #region CreateRequest(HTTPMethod, URI, BuilderAction = null)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">A HTTP method.</param>
        /// <param name="URI">An URI.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A new HTTPRequest object.</returns>
        public HTTPRequest.Builder CreateRequest(HTTPMethod                   HTTPMethod,
                                                 HTTPPath                      URI,
                                                 Action<HTTPRequest.Builder>  BuilderAction  = null)
        {

            //var Host = URI.Substring(Math.Max(URI.IndexOf("://"), 0));
            //Host = Host.Substring(Math.Max(URI.IndexOf("/"), Host.Length));

            var Builder     = new HTTPRequest.Builder(this) {
                Host        = VirtualHostname ?? Hostname,
                HTTPMethod  = HTTPMethod,
                URI         = URI
            };

            if (BuilderAction != null)
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
        public Task<HTTPResponse> Execute(Func<HTTPClient, HTTPRequest>  HTTPRequestDelegate,
                                          ClientRequestLogHandler        RequestLogDelegate    = null,
                                          ClientResponseLogHandler       ResponseLogDelegate   = null,

                                          CancellationToken?             CancellationToken     = null,
                                          EventTracking_Id               EventTrackingId       = null,
                                          TimeSpan?                      RequestTimeout        = null,
                                          Byte                           NumberOfRetry         = 0)

        {

            #region Initial checks

            if (HTTPRequestDelegate == null)
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
        /// <param name="ResponseLogDelegate">A delegate for logging the HTTP request/response.</param>        /// 
        /// 
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <param name="EventTrackingId"></param>
        /// <param name="RequestTimeout">An optional timeout.</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        public async Task<HTTPResponse>

            Execute(HTTPRequest               Request,
                    ClientRequestLogHandler   RequestLogDelegate    = null,
                    ClientResponseLogHandler  ResponseLogDelegate   = null,

                    CancellationToken?        CancellationToken     = null,
                    EventTracking_Id          EventTrackingId       = null,
                    TimeSpan?                 RequestTimeout        = null,
                    Byte                      NumberOfRetry         = 0)

        {

            #region Call the optional HTTP request log delegate

            //DebugX.LogT("HTTPClient pre-logging (" + Request.URI + ")...");

            try
            {

                if (RequestLogDelegate != null)
                    await Task.WhenAll(RequestLogDelegate.GetInvocationList().
                                       Cast<ClientRequestLogHandler>().
                                       Select(e => e(DateTime.UtcNow,
                                                     this,
                                                     Request))).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                e.Log(nameof(HTTPClient) + "." + nameof(RequestLogDelegate));
            }

            //DebugX.LogT("HTTPClient post-logging (" + Request.URI + ")...");

            #endregion

            HTTPResponse Response = null;

            try
            {

                //Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

                #region Data

                var HTTPHeaderBytes = new Byte[0];
                var HTTPBodyBytes   = new Byte[0];
                var sw = new Stopwatch();

                if (!RequestTimeout.HasValue)
                    RequestTimeout = Request.Timeout;

                if (!RequestTimeout.HasValue)
                    RequestTimeout = TimeSpan.FromSeconds(60);

                #endregion

                #region Create TCP connection (possibly also do DNS lookups)

                if (TCPSocket == null)
                {

                    System.Net.IPEndPoint _FinalIPEndPoint = null;
                    //     IIPAddress _ResolvedRemoteIPAddress = null;

                    if (RemoteIPAddress == null)
                    {

                        if (Hostname == "127.0.0.1" || Hostname == "localhost")
                            RemoteIPAddress = IPv4Address.Localhost;

                        else if (Hostname == "::1" || Hostname == "localhost6")
                            RemoteIPAddress = IPv6Address.Localhost;

                        // Hostname is an IPv4 address...
                        else if (IPv4AddressRegExpr.IsMatch(Hostname.Name))
                            RemoteIPAddress = IPv4Address.Parse(Hostname.Name);

                        #region DNS lookup...

                        if (RemoteIPAddress == null)
                        {

                            var IPv4AddressLookupTask  = DNSClient.
                                                             Query<A>(Hostname.Name).
                                                             ContinueWith(query => query.Result.Select(ARecord    => ARecord.IPv4Address));

                            var IPv6AddressLookupTask  = DNSClient.
                                                             Query<AAAA>(Hostname.Name).
                                                             ContinueWith(query => query.Result.Select(AAAARecord => AAAARecord.IPv6Address));

                            await Task.WhenAll(IPv4AddressLookupTask,
                                               IPv6AddressLookupTask).
                                       ConfigureAwait(false);


                            if (IPv4AddressLookupTask.Result.Any())
                                RemoteIPAddress = IPv4AddressLookupTask.Result.First();

                            else if (IPv6AddressLookupTask.Result.Any())
                                RemoteIPAddress = IPv6AddressLookupTask.Result.First();


                            if (RemoteIPAddress == null || RemoteIPAddress.GetBytes() == null)
                                throw new Exception("DNS lookup failed!");

                        }

                        #endregion

                    }

                    _FinalIPEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(RemoteIPAddress.GetBytes()),
                                                                 RemotePort.ToInt32());

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

                    TCPSocket.Connect(_FinalIPEndPoint);
                    TCPSocket.ReceiveTimeout = (Int32)RequestTimeout.Value.TotalMilliseconds;

                }

                #endregion

                #region Create (Crypto-)Stream

                TCPStream = new NetworkStream(TCPSocket, true);
                TCPStream.ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                TLSStream = RemoteCertificateValidator != null

                                 ? new SslStream(TCPStream,
                                                 false,
                                                 RemoteCertificateValidator,
                                                 LocalCertificateSelector,
                                                 EncryptionPolicy.RequireEncryption)

                                 : null;

                if (TLSStream != null)
                    TLSStream.ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                HTTPStream = null;

                if (RemoteCertificateValidator != null)
                {
                    HTTPStream = TLSStream;
                    await TLSStream.AuthenticateAsClientAsync(Hostname.Name);//, new X509CertificateCollection(new X509Certificate[] { ClientCert }), SslProtocols.Default, true);
                }

                else
                    HTTPStream = TCPStream;

                HTTPStream.ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                #endregion

                #region Send Request

                HTTPStream.Write(String.Concat(Request.EntireRequestHeader, "\r\n\r\n").
                                        ToUTF8Bytes());

                var RequestBodyLength = Request.HTTPBody == null
                                            ? Request.ContentLength.HasValue ? (Int32) Request.ContentLength.Value : 0
                                            : Request.ContentLength.HasValue ? Math.Min((Int32)Request.ContentLength.Value, Request.HTTPBody.Length) : Request.HTTPBody.Length;

                if (RequestBodyLength > 0)
                    HTTPStream.Write(Request.HTTPBody, 0, RequestBodyLength);

                //DebugX.LogT("HTTPClient (" + Request.HTTPMethod + " " + Request.URI + ") sent request of " + Request.EntirePDU.Length + " bytes at " + sw.ElapsedMilliseconds + "ms!");

                var _InternalHTTPStream  = new MemoryStream();

                #endregion

                #region Wait timeout for the server to react!


                while (!TCPStream.DataAvailable)
                {

                    if (sw.ElapsedMilliseconds >= RequestTimeout.Value.TotalMilliseconds)
                        throw new TimeoutException(sw.Elapsed);

                    Thread.Sleep(1);

                }

                //DebugX.LogT("HTTPClient (" + Request.HTTPMethod + " " + Request.URI + ") got first response after " + sw.ElapsedMilliseconds + "ms!");

                #endregion


                #region Read the HTTP header

                var CurrentDataLength    = 0;
                var CurrentHeaderLength  = 0;
                var HeaderEndsAt         = 0;

                var _Buffer = new Byte[4096];

                do
                {

                    CurrentDataLength = HTTPStream.Read(_Buffer, 0, _Buffer.Length);

                    if (CurrentDataLength > 0)
                    {

                        if (CurrentDataLength > 3 || CurrentHeaderLength > 3)
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
                                if (HeaderEndsAt + 4 < CurrentDataLength)
                                {
                                    Array.Resize(ref HTTPBodyBytes, CurrentDataLength - 4 - HeaderEndsAt);
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
                    throw new ApplicationException("[" + DateTime.UtcNow.ToString() + "] Could not find the end of the HTTP protocol header!");

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

                            CurrentDataLength = HTTPStream.Read(_Buffer, 0, Math.Min(_Buffer.Length, _StillToRead));

                            if (CurrentDataLength > 0)
                            {
                                var OldSize = Response.HTTPBody.Length;
                                Response.ResizeBody(OldSize + CurrentDataLength);
                                Array.Copy(_Buffer, 0, Response.HTTPBody, OldSize, CurrentDataLength);
                                _StillToRead -= CurrentDataLength;
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

                    //var HTTPBodyStartsAt = HTTPHeaderBytes.Length + 4;

                    //DebugX.Log("[HTTPClient] Chunked encoding detected");

                    try
                    {

                        // Write the first buffer (without the HTTP header) to the HTTPBodyStream...
                        //_InternalHTTPStream.Seek(HTTPBodyStartsAt, SeekOrigin.Begin);
                        Response.NewContentStream();
                        var _ChunkedStream = new MemoryStream();

                        //_ChunkedStream.Write(_Buffer, 0, _InternalHTTPStream.Read(_Buffer, 0, _Buffer.Length));
                        _ChunkedStream.Write(HTTPBodyBytes, 0, HTTPBodyBytes.Length);
                        var ChunkedDecodingFinished = false;
                        var ChunkedStreamLength = 0UL;

                        do
                        {

                            #region If more (new) data is available -> read it!

                            do
                            {

                                while (TCPStream.DataAvailable)
                                {

                                    CurrentDataLength = HTTPStream.Read(_Buffer, 0, _Buffer.Length);

                                    if (CurrentDataLength > 0)
                                        _ChunkedStream.Write(_Buffer, 0, CurrentDataLength);

                                    if (sw.ElapsedMilliseconds > HTTPStream.ReadTimeout)
                                        throw new ApplicationException("HTTPClient timeout!");

                                    Thread.Sleep(1);

                                }

                            } while ((UInt64)_ChunkedStream.Length == ChunkedStreamLength);

                            ChunkedStreamLength = (UInt64)_ChunkedStream.Length;
                            OnDataRead?.Invoke(sw.Elapsed, ChunkedStreamLength);

                            #endregion

                            var ChunkedBytes = _ChunkedStream.ToArray();
                            var DecodedStream = new MemoryStream();
                            var IsStatus_ReadBlockLength = true;
                            var CurrentPosition = 0;
                            var LastPos = 0;
                            var NumberOfBlocks = 0;

                            do
                            {

                                if (CurrentPosition > 2 &&
                                    IsStatus_ReadBlockLength &&
                                    ChunkedBytes[CurrentPosition - 1] == '\n' &&
                                    ChunkedBytes[CurrentPosition - 2] == '\r')
                                {

                                    var BlockLength = ChunkedBytes.ReadTEBlockLength(LastPos,
                                                                                     CurrentPosition - LastPos - 2);

                                    //Debug.WriteLine(DateTime.UtcNow + " Chunked encoded block of length " + BlockLength + " bytes detected");

                                    #region End of stream reached...

                                    if (BlockLength == 0)
                                    {
                                        Response.ContentStreamToArray(DecodedStream);
                                        ChunkedDecodingFinished = true;
                                        break;
                                    }

                                    #endregion

                                    #region ...or read a new block...

                                    if (CurrentPosition + BlockLength <= ChunkedBytes.Length)
                                    {

                                        NumberOfBlocks++;

                                        DecodedStream.Write(ChunkedBytes, CurrentPosition, BlockLength);
                                        CurrentPosition += BlockLength;

                                        if (CurrentPosition < ChunkedBytes.Length &&
                                            ChunkedBytes[CurrentPosition] == 0x0d)
                                        {
                                            CurrentPosition++;
                                        }

                                        if (CurrentPosition < ChunkedBytes.Length - 1 &&
                                            ChunkedBytes[CurrentPosition] == 0x0a)
                                        {
                                            CurrentPosition++;
                                        }

                                        LastPos = CurrentPosition;

                                        IsStatus_ReadBlockLength = false;

                                    }

                                    #endregion

                                    #region ...or start over!

                                    else
                                    {
                                        // Reaching this point means we need to read more
                                        // data from the network stream and decode again!

                                        //Debug.WriteLine(DateTime.UtcNow + " Chunked decoding restarted after reading " + NumberOfBlocks + " blocks!");

                                        break;

                                    }

                                    #endregion

                                }

                                else
                                {
                                    IsStatus_ReadBlockLength = true;
                                    CurrentPosition++;
                                }

                            } while (CurrentPosition < _ChunkedStream.Length);

                        } while (!ChunkedDecodingFinished);

                    }
                    catch (Exception e)
                    {
                        DebugX.Log("Chunked decoding failed: " + e.Message);
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

                if (Response.Connection == null ||
                    Response.Connection == "close")
                {

                    if (TCPSocket != null)
                    {
                        TCPSocket.Close();
                        //TCPClient.Dispose();
                        TCPSocket = null;
                    }

                    HTTPStream = null;

                }

                #endregion

            }
            catch (TimeoutException e)
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

                if (TCPSocket != null)
                {
                    TCPSocket.Close();
                    //TCPClient.Dispose();
                    TCPSocket = null;
                }

            }
            catch (Exception e)
            {

                #region Create a HTTP response for the exception...

                while (e.InnerException != null)
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

                if (TCPSocket != null)
                {
                    TCPSocket.Close();
                    //TCPClient.Dispose();
                    TCPSocket = null;
                }

            }


            #region Call the optional HTTP response log delegate

            try
            {

                if (ResponseLogDelegate != null)
                    await Task.WhenAll(ResponseLogDelegate.GetInvocationList().
                                       Cast<ClientResponseLogHandler>().
                                       Select(e => e(DateTime.UtcNow,
                                                     this,
                                                     Request,
                                                     Response))).
                                       ConfigureAwait(false);

            }
            catch (Exception e2)
            {
                e2.Log(nameof(HTTPClient) + "." + nameof(ResponseLogDelegate));
            }

            #endregion

            return Response;

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

        #region IDisposable Members

        public void Dispose()
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
        {
            return String.Concat(this.GetType().Name, " ", RemoteIPAddress.ToString(), ":", RemotePort);
        }

        #endregion

    }

}
