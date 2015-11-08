/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using org.GraphDefined.Vanaheimr.Hermod.Services.DNS;

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


    /// <summary>
    /// A http client.
    /// </summary>
    public class HTTPClient : IDisposable
    {

        #region Data

        private TcpClient       TCPClient;
        private NetworkStream   TCPStream;
        private SslStream       TLSStream;
        private Stream          HTTPStream;

        #endregion

        #region Properties

        #region Hostname

        /// <summary>
        /// The Hostname to which the HTTPClient connects.
        /// </summary>
        public String Hostname { get; private set; }

        #endregion

        #region RemoteIPAddress

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        public IIPAddress RemoteIPAddress { get; set; }

        #endregion

        #region RemotePort

        /// <summary>
        /// The IP port to connect to.
        /// </summary>
        public IPPort RemotePort { get; set; }

        #endregion

        #region RemoteSocket

        /// <summary>
        /// The IP socket to connect to.
        /// </summary>
        public IPSocket RemoteSocket
        {

            get
            {
                return new IPSocket(RemoteIPAddress, RemotePort);
            }

            set
            {

                if (value == null)
                    throw new ArgumentNullException("The remote socket must not be null!");

                this.RemoteIPAddress = value.IPAddress;
                this.RemotePort      = value.Port;

            }

        }

        #endregion


        #region UserAgent

        private const String _UserAgent = "Hermod HTTP Client v0.1";

        /// <summary>
        /// The default server name.
        /// </summary>
        public virtual String UserAgent
        {
            get
            {
                return _UserAgent;
            }
        }

        #endregion

        #region DNSClient

        private readonly DNSClient _DNSClient;

        /// <summary>
        /// The default server name.
        /// </summary>
        public DNSClient DNSClient
        {
            get
            {
                return _DNSClient;
            }
        }

        #endregion

        public X509Certificate ClientCert { get; set; }

        public X509Certificate2 ServerCert { get; set; }

        public RemoteCertificateValidationCallback RemoteCertificateValidator { get; set; }

        public LocalCertificateSelectionCallback ClientCertificateSelector { get; set; }

        #region UseTLS

        private readonly Boolean _UseTLS;

        public Boolean UseTLS
        {
            get
            {
                return _UseTLS;
            }
        }

        #endregion

        #endregion

        #region Events


        #endregion

        #region Constructor(s)

        #region HTTPClient(RemoteIPAddress, RemotePort, UseTLS = false, DNSClient  = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteIPAddress">The remote IP address to connect to.</param>
        /// <param name="RemotePort">The remote IP port to connect to.</param>
        /// <param name="UseTLS">Use transport layer security [default: false].</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(IIPAddress  RemoteIPAddress,
                          IPPort      RemotePort,
                          Boolean     UseTLS     = false,
                          DNSClient   DNSClient  = null)
        {

            this.RemoteIPAddress  = RemoteIPAddress;
            this.RemotePort       = RemotePort;
            this._UseTLS          = UseTLS;
            this._DNSClient       = DNSClient == null
                                       ? new DNSClient()
                                       : DNSClient;

        }

        #endregion

        #region HTTPClient(Socket, UseTLS = false, DNSClient  = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteSocket">The remote IP socket to connect to.</param>
        /// <param name="UseTLS">Use transport layer security [default: false].</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(IPSocket   RemoteSocket,
                          Boolean    UseTLS     = false,
                          DNSClient  DNSClient  = null)

            : this(RemoteSocket.IPAddress, RemoteSocket.Port, UseTLS, DNSClient)

        { }

        #endregion

        #region HTTPClient(RemoteHost, RemotePort = null, UseTLS = false, DNSClient  = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteHost">The remote hostname to connect to.</param>
        /// <param name="RemotePort">The remote IP port to connect to.</param>
        /// <param name="UseTLS">Use transport layer security [default: false].</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(String     RemoteHost,
                          IPPort     RemotePort = null,
                          Boolean    UseTLS     = false,
                          DNSClient  DNSClient  = null)
        {

            this.Hostname    = RemoteHost;

            this.RemotePort  = RemotePort != null
                                  ? RemotePort
                                  : IPPort.Parse(80);

            this._UseTLS     = UseTLS;

            this._DNSClient  = DNSClient != null
                                  ? DNSClient
                                  : new DNSClient();

        }

        #endregion

        #endregion


        #region CreateRequest(HTTPClient, HTTPMethod, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">A HTTP method.</param>
        /// <param name="URI">An URI.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A new HTTPRequest object.</returns>
        public HTTPRequestBuilder CreateRequest(HTTPMethod                  HTTPMethod,
                                                String                      URI            = "/",
                                                Action<HTTPRequestBuilder>  BuilderAction  = null)
        {

            var Builder     = new HTTPRequestBuilder(this) {
                HTTPMethod  = HTTPMethod,
                URI         = URI
            };

            BuilderAction.FailSafeInvoke(Builder);

            return Builder;

        }

        #endregion

        #region CreateRequest(HTTPMethod, URI = "/", BuilderAction = null)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">A HTTP method.</param>
        /// <param name="URI">An URL path.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A new HTTPRequest object.</returns>
        public HTTPRequestBuilder CreateRequest(String                      HTTPMethod,
                                                String                      URI        = "/",
                                                Action<HTTPRequestBuilder>  BuilderAction  = null)
        {

            var Builder = new HTTPRequestBuilder(this) {
                HTTPMethod  = new HTTPMethod(HTTPMethod),
                URI         = URI
            };

            BuilderAction.FailSafeInvoke(Builder);

            return Builder;

        }

        #endregion


        #region Execute(HTTPRequest, Timeout = null)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="HTTPRequest">A HTTP request.</param>
        /// <param name="Timeout">An optional timeout.</param>
        public async Task<HTTPResponse> Execute(HTTPRequest  HTTPRequest,
                                                TimeSpan?    Timeout = null)
        {
            return await Execute(HTTPRequest, null, Timeout);
        }

        #endregion

        #region Execute(HTTPRequest, RequestResponseDelegate, Timeout = null)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="HTTPRequest">A HTTP request.</param>
        /// <param name="RequestResponseDelegate">A delegate for processing the HTTP request/response.</param>
        /// <param name="Timeout">An optional timeout.</param>
        public async Task<HTTPResponse> Execute(HTTPRequest                        HTTPRequest,
                                                Action<HTTPRequest, HTTPResponse>  RequestResponseDelegate,
                                                TimeSpan?                          Timeout  = null)
        {

            var task = Task<HTTPResponse>.Factory.StartNew(() => {

                //DebugX.Log("[" + DateTime.Now.ToIso8601() + "] HTTPClient started...");

                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

                HTTPResponse _HTTPResponse = null;

                try
                {

                    #region Data

                    var HTTPHeaderBytes  = new Byte[0];
                    var sw               = new Stopwatch();

                    if (!Timeout.HasValue)
                        Timeout = TimeSpan.FromSeconds(60);

                    #endregion

                    #region Create TCP connection (possibly also do DNS lookups)

                    if (TCPClient == null)
                    {

                        #region DNS lookup...

                        if (RemoteIPAddress == null)
                        {

                            try
                            {

                                var RegExpr = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");

                                if (RegExpr.IsMatch(Hostname))
                                    this.RemoteIPAddress = IPv4Address.Parse(Hostname);

                                else
                                {

                                    var IPv4AddressTask = _DNSClient.
                                                              Query<A>(Hostname).
                                                                  ContinueWith(QueryTask => QueryTask.Result.
                                                                                                Select(ARecord => ARecord.IPv4Address).
                                                                                                FirstOrDefault());

                                    IPv4AddressTask.Wait();

                                    this.RemoteIPAddress = IPv4AddressTask.Result;

                                }

                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine("[" + DateTime.Now + "] " + e.Message);
                            }

                        }

                        #endregion

                        sw.Start();

                        TCPClient = new TcpClient(RemoteIPAddress.ToString(), this.RemotePort.ToInt32());
                        TCPClient.ReceiveTimeout = (Int32) Timeout.Value.TotalMilliseconds;

                    }

                    #endregion

                    #region Create (Crypto-)Stream

                    TCPStream  = TCPClient.GetStream();
                    TCPStream.ReadTimeout = (Int32) Timeout.Value.TotalMilliseconds;

                    TLSStream  = UseTLS
                                     ? new SslStream(TCPStream,
                                                     false,
                                                     RemoteCertificateValidator)
                                                 //    ClientCertificateSelector,
                                                     //EncryptionPolicy.RequireEncryption)
                                     : null;

                    if (TLSStream != null)
                        TLSStream.ReadTimeout = (Int32) Timeout.Value.TotalMilliseconds;

                    HTTPStream = null;

                    if (UseTLS)
                    {
                        HTTPStream = TLSStream;
                        TLSStream.AuthenticateAsClient(Hostname);//, new X509CertificateCollection(new X509Certificate[] { ClientCert }), SslProtocols.Default, false);
                    }

                    else
                        HTTPStream = TCPStream;

                    HTTPStream.ReadTimeout = (Int32) Timeout.Value.TotalMilliseconds;

                    #endregion

                    #region Send Request

                    HTTPStream.Write(String.Concat(HTTPRequest.EntireRequestHeader,
                                                   Environment.NewLine,
                                                   Environment.NewLine).
                                     ToUTF8Bytes());

                    var RequestBodyLength = HTTPRequest.Content == null
                                                ? (Int32) HTTPRequest.ContentLength
                                                : Math.Min((Int32) HTTPRequest.ContentLength, HTTPRequest.Content.Length);

                    if (RequestBodyLength > 0)
                        HTTPStream.Write(HTTPRequest.Content, 0, RequestBodyLength);

                    var _MemoryStream  = new MemoryStream();
                    var _Buffer        = new Byte[10485760]; // 10 MBytes, a smaller value leads to read errors!

                    #endregion

                    #region Wait timeout for the server to react!

                    while (!TCPStream.DataAvailable)
                    {

                        if (sw.ElapsedMilliseconds >= Timeout.Value.TotalMilliseconds)
                        {
                            TCPClient.Close();
                            throw new Exception("[" + DateTime.Now + "] Could not read from the TCP stream for " + sw.ElapsedMilliseconds + "ms!");
                        }

                        Thread.Sleep(1);

                    }

                    //Debug.WriteLine("[" + DateTime.Now + "] HTTPClient (" + TCPClient.Client.LocalEndPoint.ToString() + " -> " + RemoteSocket.ToString() + ") got first response after " + sw.ElapsedMilliseconds + "ms!");

                    #endregion

                    #region Read the entire HTTP header, and maybe some of the HTTP body

                    var CurrentDataLength = 0;

                    do
                    {

                        #region When data available, write it to the buffer...

                        while (TCPStream.DataAvailable)
                        {

                            CurrentDataLength = HTTPStream.Read(_Buffer, 0, _Buffer.Length);

                            if (CurrentDataLength > -1)
                            {
                                _MemoryStream.Write(_Buffer, 0, CurrentDataLength);
                                //Debug.WriteLine("[" + DateTime.Now + "] Read " + CurrentDataLength + " bytes from HTTP connection (" + TCPClient.Client.LocalEndPoint.ToString() + " -> " + RemoteSocket.ToString() + ") (" + sw.ElapsedMilliseconds + "ms)!");
                            }

                        }

                        #endregion

                        #region Check if the entire HTTP header was already read into the buffer

                        if (_MemoryStream.Length > 4)
                        {

                            var MemoryCopy = _MemoryStream.ToArray();

                            for (var pos = 3; pos < MemoryCopy.Length; pos++)
                            {

                                if (MemoryCopy[pos    ] == 0x0a &&
                                    MemoryCopy[pos - 1] == 0x0d &&
                                    MemoryCopy[pos - 2] == 0x0a &&
                                    MemoryCopy[pos - 3] == 0x0d)
                                {
                                    Array.Resize(ref HTTPHeaderBytes, pos - 3);
                                    Array.Copy(MemoryCopy, 0, HTTPHeaderBytes, 0, pos - 3);
                                    break;
                                }

                            }

                            //if (HTTPHeaderBytes.Length > 0)
                            //    Debug.WriteLine("[" + DateTime.Now + "] End of (" + TCPClient.Client.LocalEndPoint.ToString() + " -> " + RemoteSocket.ToString() + ") HTTP header at " + HTTPHeaderBytes.Length + " bytes (" + sw.ElapsedMilliseconds + "ms)!");

                        }
                        else
                            Thread.Sleep(1);

                        #endregion

                    }
                    // Note: Delayed parts of the HTTP body may not be read into the buffer
                    //       => Must be read later!
                    while (TCPStream.DataAvailable ||
                           ((sw.ElapsedMilliseconds < HTTPStream.ReadTimeout) && HTTPHeaderBytes.Length == 0));

                    //Debug.WriteLine("[" + DateTime.Now + "] Finally read " + _MemoryStream.Length + " bytes of HTTP client (" + TCPClient.Client.LocalEndPoint.ToString() + " -> " + RemoteSocket.ToString() + ") data (" + sw.ElapsedMilliseconds + "ms)!");

                    #endregion

                    #region Copy HTTP header data

                    if (HTTPHeaderBytes.Length == 0)
                        throw new ApplicationException(DateTime.Now + " Could not find the end of the HTTP protocol header!");

                    _HTTPResponse = new HTTPResponse(HTTPRequest, HTTPHeaderBytes.ToUTF8String());

                    #endregion

                    #region Read 'Content-Length' bytes...

                    // Copy only the number of bytes given within
                    // the HTTP header element 'Content-Length'!
                    if (_HTTPResponse.ContentLength.HasValue && _HTTPResponse.ContentLength.Value > 0)
                    {

                        _MemoryStream.Seek(HTTPHeaderBytes.Length + 4, SeekOrigin.Begin);
                        var _Read = _MemoryStream.Read(_Buffer, 0, _Buffer.Length);
                        var _StillToRead = (Int32) _HTTPResponse.ContentLength.Value - _Read;
                        _HTTPResponse.ContentStream.Write(_Buffer, 0, _Read);
                        var _CurrentBufferSize = 0;

                        do
                        {

                            while (TCPStream.DataAvailable && _StillToRead > 0)
                            {
                                _CurrentBufferSize = Math.Min(_Buffer.Length, (Int32) _StillToRead);
                                _Read = HTTPStream.Read(_Buffer, 0, _CurrentBufferSize);
                                _HTTPResponse.ContentStream.Write(_Buffer, 0, _Read);
                                _StillToRead -= _Read;
                            }

                            if (_StillToRead <= 0)
                                break;

                            Thread.Sleep(1);

                        }
                        while (sw.ElapsedMilliseconds < HTTPStream.ReadTimeout);

                        _HTTPResponse.ContentStreamToArray();

                    }

                    #endregion

                    #region ...or read till timeout (e.g. for chunked transport)!

                    else
                    {

                        try
                        {

                            _MemoryStream.Seek(HTTPHeaderBytes.Length + 4, SeekOrigin.Begin);
                            _HTTPResponse.NewContentStream();
                            _HTTPResponse.ContentStream.Write(_Buffer, 0, _MemoryStream.Read(_Buffer, 0, _Buffer.Length));

                            var Retries = 0;

                            while (Retries < 10)
                            {

                                while (TCPStream.DataAvailable)
                                {
                                    _HTTPResponse.ContentStream.Write(_Buffer, 0, HTTPStream.Read(_Buffer, 0, _Buffer.Length));
                                    Retries = 0;
                                }

                                Thread.Sleep(10);
                                Retries++;

                            }

                            if (_HTTPResponse.TransferEncoding == "chunked")
                            {

                                //Debug.WriteLine(DateTime.Now + " Chunked encoding detected");

                                var TEContent        = ((MemoryStream) _HTTPResponse.ContentStream).ToArray();
                                var TEString         = TEContent.ToUTF8String();
                                var ReadBlockLength  = true;
                                var TEMemStram       = new MemoryStream();
                                var LastPos          = 0;

                                var i = 0;
                                do
                                {

                                    if (i > 2 &&
                                        ReadBlockLength &&
                                        TEContent[i - 1] == '\n' &&
                                        TEContent[i - 2] == '\r')
                                    {

                                        var len = TEContent.ReadTEBlockLength(LastPos, i - LastPos - 2);

                                        //Debug.WriteLine(DateTime.Now + " Chunked encoded block of length " + len + " bytes detected");

                                        if (len == 0)
                                            break;

                                        if (i + len <= TEContent.Length)
                                        {

                                            TEMemStram.Write(TEContent, i, len);
                                            i = i + len;

                                            if (TEContent[i] == 0x0d)
                                                i++;

                                            if (i < TEContent.Length - 1)
                                            {
                                                if (TEContent[i] == 0x0a)
                                                    i++;
                                            }
                                            else
                                            {
                                            }

                                            LastPos = i;

                                            ReadBlockLength = false;

                                        }

                                        else
                                        {
                                            // Reaching this point seems to be an endless loop!
                                            break;

                                        }

                                    }

                                    else
                                    {
                                        ReadBlockLength = true;
                                        i++;
                                    }

                                } while (i < TEContent.Length);

                                _HTTPResponse.ContentStreamToArray(TEMemStram);

                            }

                            else
                                _HTTPResponse.ContentStreamToArray();

                        }

                        catch (Exception e)
                        {
                            Debug.WriteLine(DateTime.Now + " " + e.Message);
                        }

                    }

                    #endregion

                    #region Close connection if requested!

                    if (_HTTPResponse.Connection == null ||
                        _HTTPResponse.Connection == "close")
                    {
                        TCPClient.Close();
                        HTTPStream = null;
                        TCPClient = null;
                    }

                    #endregion

                    #region Call the optional HTTPResponse delegate(s)

                    var RequestResponseDelegateLocal = RequestResponseDelegate;
                    if (RequestResponseDelegateLocal != null)
                        RequestResponseDelegateLocal(HTTPRequest, _HTTPResponse);

                    #endregion

                }
                catch (Exception e)
                {
                    _HTTPResponse = new HTTPResponse(e);
                }

                return _HTTPResponse;

            }, TaskCreationOptions.AttachedToParent);

            return await task;

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
                if (TCPClient != null)
                {
                    TCPClient.Close();
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

        #region ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {
            return String.Concat(this.GetType().Name, " ", RemoteIPAddress.ToString(), ":", RemotePort);
        }

        #endregion

    }

}
