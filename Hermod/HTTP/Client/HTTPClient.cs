/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

        public const String DefaultUserAgent = "Vanaheimr Hermod HTTP Client v0.1";

        #endregion

        #region Properties

        /// <summary>
        /// The Hostname to which the HTTPClient connects.
        /// </summary>
        public String           Hostname            { get; }

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        public IIPAddress       RemoteIPAddress     { get; }

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

        public X509Certificate ClientCert { get; }

        //    public LocalCertificateSelectionCallback ClientCertificateSelector { get; set; }

        #endregion

        #region Events


        #endregion

        #region Constructor(s)

        #region HTTPClient(RemoteIPAddress, RemotePort, RemoteCertificateValidator = null, ClientCert = null, DNSClient = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteIPAddress">The remote IP address to connect to.</param>
        /// <param name="RemotePort">The remote IP port to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(IIPAddress                           RemoteIPAddress,
                          IPPort                               RemotePort,
                          RemoteCertificateValidationCallback  RemoteCertificateValidator  = null,
                          X509Certificate                      ClientCert                  = null,
                          DNSClient                            DNSClient                   = null)
        {

            this.RemoteIPAddress             = RemoteIPAddress;
            this.Hostname                    = RemoteIPAddress.ToString();
            this.RemotePort                  = RemotePort;
            this.RemoteCertificateValidator  = RemoteCertificateValidator;
            this.ClientCert                  = ClientCert;
            this.DNSClient                   = DNSClient == null
                                                   ? new DNSClient()
                                                   : DNSClient;

        }

        #endregion

        #region HTTPClient(Socket, RemoteCertificateValidator = null, ClientCert = null, DNSClient = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteSocket">The remote IP socket to connect to.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(IPSocket                             RemoteSocket,
                          RemoteCertificateValidationCallback  RemoteCertificateValidator  = null,
                          X509Certificate                      ClientCert                  = null,
                          DNSClient                            DNSClient                   = null)

            : this(RemoteSocket.IPAddress,
                   RemoteSocket.Port,
                   RemoteCertificateValidator,
                   ClientCert,
                   DNSClient)

        { }

        #endregion

        #region HTTPClient(RemoteHost, RemotePort = null, RemoteCertificateValidator = null, ClientCert = null, DNSClient = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteHost">The remote hostname to connect to.</param>
        /// <param name="RemotePort">The remote IP port to connect to.</param>
        /// <param name="ClientCert">The TLS client certificate to use.</param>
        /// <param name="RemoteCertificateValidator">A delegate to verify the remote TLS certificate.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(String                               RemoteHost,
                          IPPort                               RemotePort                  = null,
                          RemoteCertificateValidationCallback  RemoteCertificateValidator  = null,
                          X509Certificate                      ClientCert                  = null,
                          DNSClient                            DNSClient                   = null)
        {

            this.Hostname                    = RemoteHost;
            this.RemotePort                  = RemotePort != null
                                                   ? RemotePort
                                                   : IPPort.Parse(80);

            this.RemoteCertificateValidator  = RemoteCertificateValidator;

            this.ClientCert                  = ClientCert;

            this.DNSClient                   = DNSClient != null
                                                   ? DNSClient
                                                   : new DNSClient();

        }

        #endregion

        #endregion


        #region CreateRequest(HTTPClient, HTTPMethod, URI, CancellationToken, BuilderAction = null)

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">A HTTP method.</param>
        /// <param name="URI">An URI.</param>
        /// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        /// <returns>A new HTTPRequest object.</returns>
        public HTTPRequestBuilder CreateRequest(HTTPMethod                  HTTPMethod,
                                                String                      URI,
                                                Action<HTTPRequestBuilder>  BuilderAction  = null)
        {

            var Builder     = new HTTPRequestBuilder(this) {
                HTTPMethod  = HTTPMethod,
                URI         = URI
            };

            BuilderAction?.Invoke(Builder);

            return Builder;

        }

        #endregion

        #region CreateRequest(HTTPMethod, URI = "/", BuilderAction = null)

        ///// <summary>
        ///// Create a new HTTP request.
        ///// </summary>
        ///// <param name="HTTPMethod">A HTTP method.</param>
        ///// <param name="URI">An URL path.</param>
        ///// <param name="BuilderAction">A delegate to configure the new HTTP request builder.</param>
        ///// <returns>A new HTTPRequest object.</returns>
        //public HTTPRequestBuilder CreateRequest(String                      HTTPMethod,
        //                                        String                      URI        = "/",
        //                                        Action<HTTPRequestBuilder>  BuilderAction  = null)
        //{

        //    var Builder = new HTTPRequestBuilder(this) {
        //        HTTPMethod  = new HTTPMethod(HTTPMethod),
        //        URI         = URI
        //    };

        //    BuilderAction.FailSafeInvoke(Builder);

        //    return Builder;

        //}

        #endregion


        #region Execute(HTTPRequestDelegate, RequestLogDelegate = null, ResponseLogDelegate = null, Timeout = null, CancellationToken = null)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="HTTPRequestDelegate">A delegate for producing a HTTP request for a given HTTP client.</param>
        /// <param name="RequestLogDelegate">A delegate for logging the HTTP request.</param>
        /// <param name="ResponseLogDelegate">A delegate for logging the HTTP request/response.</param>
        /// <param name="Timeout">An optional timeout.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async Task<HTTPResponse> Execute(Func<HTTPClient, HTTPRequest>  HTTPRequestDelegate,
                                                ClientRequestLogHandler        RequestLogDelegate   = null,
                                                ClientResponseLogHandler       ResponseLogDelegate  = null,
                                                TimeSpan?                      Timeout              = null,
                                                CancellationToken?             CancellationToken    = null)
        {

            #region Initial checks

            if (HTTPRequestDelegate == null)
                throw new ArgumentNullException(nameof(HTTPRequestDelegate), "The given delegate must not be null!");

            #endregion

            return await Execute(HTTPRequestDelegate(this), RequestLogDelegate, ResponseLogDelegate, Timeout, CancellationToken);

        }

        #endregion

        #region Execute(Request, RequestLogDelegate = null, ResponseLogDelegate = null, Timeout = null, CancellationToken = null)

        /// <summary>
        /// Execute the given HTTP request and return its result.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="RequestLogDelegate">A delegate for logging the HTTP request.</param>
        /// <param name="ResponseLogDelegate">A delegate for logging the HTTP request/response.</param>
        /// <param name="Timeout">An optional timeout.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async Task<HTTPResponse> Execute(HTTPRequest               Request,
                                                ClientRequestLogHandler   RequestLogDelegate   = null,
                                                ClientResponseLogHandler  ResponseLogDelegate  = null,
                                                TimeSpan?                 Timeout              = null,
                                                CancellationToken?        CancellationToken    = null)
        {

            #region Call the optional HTTP request log delegate

            try
            {

                RequestLogDelegate?.Invoke(DateTime.Now, this, Request);

            }
            catch (Exception e)
            {
                e.Log(nameof(HTTPClient) + "." + nameof(RequestLogDelegate));
            }

            #endregion

            var task = Task<HTTPResponse>.Factory.StartNew(() => {

                HTTPResponse Response = null;

                try
                {

                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

                    #region Data

                    var HTTPHeaderAsArray = new Byte[0];
                    var sw = new Stopwatch();

                    if (!Timeout.HasValue)
                        Timeout = TimeSpan.FromSeconds(60);

                    #endregion

                    #region Create TCP connection (possibly also do DNS lookups)

                    if (TCPClient == null)
                    {

                        System.Net.IPEndPoint _FinalIPEndPoint          = null;
                        IIPAddress            _ResolvedRemoteIPAddress  = null;

                        if (RemoteIPAddress == null)
                        {

                            if (Hostname.Trim() == "127.0.0.1")
                                _ResolvedRemoteIPAddress = IPv4Address.Localhost;

                            else
                            {

                                var RegExpr = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");

                                if (RegExpr.IsMatch(Hostname))
                                    _ResolvedRemoteIPAddress = IPv4Address.Parse(Hostname);

                            }

                            #region DNS lookup...

                            if (_ResolvedRemoteIPAddress == null)
                            {

                                try
                                {

                                    var IPv4AddressTask = DNSClient.
                                                              Query<A>(Hostname).
                                                                  ContinueWith(QueryTask => QueryTask.Result.
                                                                                                Select(ARecord => ARecord.IPv4Address).
                                                                                                FirstOrDefault());

                                    IPv4AddressTask.Wait();

                                    _ResolvedRemoteIPAddress = IPv4AddressTask.Result;

                                }
                                catch (Exception e)
                                {
                                    Debug.WriteLine("[" + DateTime.Now + "] " + e.Message);
                                }

                            }

                            #endregion

                        }

                        else
                            _ResolvedRemoteIPAddress = RemoteIPAddress;

                        _FinalIPEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(_ResolvedRemoteIPAddress.GetBytes()), RemotePort.ToInt32());

                        sw.Start();

                        TCPClient = new TcpClient();
                        TCPClient.Connect(_FinalIPEndPoint);
                        TCPClient.ReceiveTimeout = (Int32) Timeout.Value.TotalMilliseconds;

                    }

                    #endregion

                    #region Create (Crypto-)Stream

                    TCPStream = TCPClient.GetStream();
                    TCPStream.ReadTimeout = (Int32)Timeout.Value.TotalMilliseconds;

                    TLSStream = RemoteCertificateValidator != null
                                     ? new SslStream(TCPStream,
                                                     false,
                                                     RemoteCertificateValidator)
                                     //    ClientCertificateSelector,
                                     //EncryptionPolicy.RequireEncryption)
                                     : null;

                    if (TLSStream != null)
                        TLSStream.ReadTimeout = (Int32)Timeout.Value.TotalMilliseconds;

                    HTTPStream = null;

                    if (RemoteCertificateValidator != null)
                    {
                        HTTPStream = TLSStream;
                        TLSStream.AuthenticateAsClient(Hostname);//, new X509CertificateCollection(new X509Certificate[] { ClientCert }), SslProtocols.Default, false);
                    }

                    else
                        HTTPStream = TCPStream;

                    HTTPStream.ReadTimeout = (Int32)Timeout.Value.TotalMilliseconds;

                    #endregion

                    #region Send Request

                    HTTPStream.Write(String.Concat(Request.EntireRequestHeader,
                                                   Environment.NewLine,
                                                   Environment.NewLine).
                                     ToUTF8Bytes());

                    var RequestBodyLength = Request.HTTPBody == null
                                                ? Request.ContentLength.HasValue ? (Int32) Request.ContentLength.Value : 0
                                                : Request.ContentLength.HasValue ? Math.Min((Int32) Request.ContentLength.Value, Request.HTTPBody.Length) : Request.HTTPBody.Length;

                    if (RequestBodyLength > 0)
                        HTTPStream.Write(Request.HTTPBody, 0, RequestBodyLength);

                    var _HTTPHeaderStream  = new MemoryStream();
                    var _Buffer        = new Byte[10485760]; // 10 MBytes, a smaller value leads to read errors!

                    #endregion

                    #region Wait timeout for the server to react!

                    //Debug.WriteLine("[" + DateTime.Now + "] HTTPClient timeout: " + Timeout.Value.ToString());

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
                                _HTTPHeaderStream.Write(_Buffer, 0, CurrentDataLength);
        //                        Debug.WriteLine("[" + DateTime.Now + "] Read " + CurrentDataLength + " bytes from HTTP connection (" + TCPClient.Client.LocalEndPoint.ToString() + " -> " + RemoteSocket.ToString() + ") (" + sw.ElapsedMilliseconds + "ms)!");
                            }

                        }

                        #endregion

                        #region Check if the entire HTTP header was already read into the buffer

                        if (_HTTPHeaderStream.Length > 4)
                        {

                            var MemoryCopy = _HTTPHeaderStream.ToArray();

                            for (var pos = 3; pos < MemoryCopy.Length; pos++)
                            {

                                if (MemoryCopy[pos    ] == 0x0a &&
                                    MemoryCopy[pos - 1] == 0x0d &&
                                    MemoryCopy[pos - 2] == 0x0a &&
                                    MemoryCopy[pos - 3] == 0x0d)
                                {
                                    Array.Resize(ref HTTPHeaderAsArray, pos - 3);
                                    Array.Copy(MemoryCopy, 0, HTTPHeaderAsArray, 0, pos - 3);
                                    break;
                                }

                            }

                            //if (HTTPHeaderBytes.Length > 0)
                            //    Debug.WriteLine("[" + DateTime.Now + "] End of (" + TCPClient.Client.LocalEndPoint.ToString() + " -> " + RemoteSocket.ToString() + ") HTTP header at " + HTTPHeaderBytes.Length + " bytes (" + sw.ElapsedMilliseconds + "ms)!");

                        }

                        #endregion

                        Thread.Sleep(1);

                    }
                    // Note: Delayed parts of the HTTP body may not be read into the buffer
                    //       => Must be read later!
                    while (TCPStream.DataAvailable ||
                           ((sw.ElapsedMilliseconds < HTTPStream.ReadTimeout) && HTTPHeaderAsArray.Length == 0));

                    //Debug.WriteLine("[" + DateTime.Now + "] Finally read " + _MemoryStream.Length + " bytes of HTTP client (" + TCPClient.Client.LocalEndPoint.ToString() + " -> " + RemoteSocket.ToString() + ") data (" + sw.ElapsedMilliseconds + "ms)!");

                    #endregion

                    #region Copy HTTP header data and create HTTP response

                    if (HTTPHeaderAsArray.Length == 0)
                        throw new ApplicationException(DateTime.Now + " Could not find the end of the HTTP protocol header!");

                    Response = HTTPResponse.Parse(HTTPHeaderAsArray.ToUTF8String(),
                                                  Request);

                    #endregion

                    #region Read 'Content-Length' bytes...

                    // Copy only the number of bytes given within
                    // the HTTP header element 'Content-Length'!
                    if (Response.ContentLength.HasValue && Response.ContentLength.Value > 0)
                    {

                        _HTTPHeaderStream.Seek(HTTPHeaderAsArray.Length + 4, SeekOrigin.Begin);
                        var _Read = _HTTPHeaderStream.Read(_Buffer, 0, _Buffer.Length);
                        var _StillToRead = (Int32) Response.ContentLength.Value - _Read;
                        Response.HTTPBodyStream.Write(_Buffer, 0, _Read);
                        var _CurrentBufferSize = 0;

                        do
                        {

                            while (TCPStream.DataAvailable && _StillToRead > 0)
                            {
                                _CurrentBufferSize = Math.Min(_Buffer.Length, (Int32) _StillToRead);
                                _Read = HTTPStream.Read(_Buffer, 0, _CurrentBufferSize);
                                Response.HTTPBodyStream.Write(_Buffer, 0, _Read);
                                _StillToRead -= _Read;
                            }

                            if (_StillToRead <= 0)
                                break;

                            Thread.Sleep(1);

                        }
                        while (sw.ElapsedMilliseconds < HTTPStream.ReadTimeout);

                        Response.ContentStreamToArray();

                    }

                    #endregion

                    #region ...or chunked transport...

                    else if (Response.TransferEncoding == "chunked")
                    {

                        Debug.WriteLine(DateTime.Now + " Chunked encoding detected");

                        try
                        {

                            // Write the first buffer (without the HTTP header) to the HTTPBodyStream...
                            _HTTPHeaderStream.Seek(HTTPHeaderAsArray.Length + 4, SeekOrigin.Begin);
                            Response.NewContentStream();
                            Response.HTTPBodyStream.Write(_Buffer, 0, _HTTPHeaderStream.Read(_Buffer, 0, _Buffer.Length));

                            do
                            {

                                while (TCPStream.DataAvailable)
                                    Response.HTTPBodyStream.Write(_Buffer, 0, HTTPStream.Read(_Buffer, 0, _Buffer.Length));

                                Thread.Sleep(10);

                            } while (sw.ElapsedMilliseconds < Timeout.Value.TotalMilliseconds);


                            var TEContent        = ((MemoryStream) Response.HTTPBodyStream).ToArray();
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

                            Response.ContentStreamToArray(TEMemStram);

                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(DateTime.Now + " Chunked decoding failed => " + e.Message);
                        }

                    }

                    #endregion

                    #region ...or read till timeout!

                    else
                        Response.ContentStreamToArray();

                    #endregion

                    #region Close connection if requested!

                    if (Response.Connection == null ||
                        Response.Connection == "close")
                    {
                        TCPClient.Close();
                        HTTPStream  = null;
                        TCPClient   = null;
                    }

                    #endregion

                }
                catch (Exception e)
                {

                    #region Create a HTTP response for the exception...

                    while (e.InnerException != null)
                        e = e.InnerException;

                    Response = new HTTPResponseBuilder(Request,
                                                       HTTPStatusCode.BadRequest)
                    {

                        ContentType  = HTTPContentType.JSON_UTF8,
                        Content      = JSONObject.Create(new JProperty("Message",     e.Message),
                                                         new JProperty("StackTrace",  e.StackTrace)).
                                                  ToUTF8Bytes()

                    };

                    #endregion

                }

                #region Call the optional HTTP response log delegate

                try
                {

                    ResponseLogDelegate?.Invoke(DateTime.Now, this, Request, Response);

                }
                catch (Exception e2)
                {
                    e2.Log(nameof(HTTPClient) + "." + nameof(ResponseLogDelegate));
                }

                #endregion


                return Response;


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

        #region (override) ToString()

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
