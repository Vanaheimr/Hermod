/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Net;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
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

        private TcpClient TCPClient;

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
        /// The IP address to connect to.
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
        public virtual DNSClient DNSClient
        {
            get
            {
                return _DNSClient;
            }
        }

        #endregion

        #endregion

        #region Events


        #endregion

        #region Constructor(s)

        #region HTTPClient(RemoteIPAddress = null, RemotePort = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteIPAddress">The IP address to connect to.</param>
        /// <param name="RemotePort">The IP port to connect to.</param>
        public HTTPClient(IIPAddress  RemoteIPAddress = null,
                          IPPort      RemotePort      = null)
        {

            this.RemoteIPAddress  = RemoteIPAddress;
            this.RemotePort       = RemotePort;

        }

        #endregion

        #region HTTPClient(Socket)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteSocket">The IP socket to connect to.</param>
        public HTTPClient(IPSocket RemoteSocket)
        {

            this.RemoteIPAddress  = RemoteSocket.IPAddress;
            this.RemotePort       = RemoteSocket.Port;

        }

        #endregion

        #region HTTPClient(RemoteHost, RemotePort = null, DNSClient  = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteHost">The IP host to connect to.</param>
        /// <param name="RemotePort">The IP port to connect to.</param>
        /// <param name="DNSClient">An optional DNS client.</param>
        public HTTPClient(String     RemoteHost,
                          IPPort     RemotePort = null,
                          DNSClient  DNSClient  = null)
        {

            this._DNSClient       = (DNSClient == null)
                                               ? new DNSClient(SearchForIPv6DNSServers: false)
                                               : DNSClient;

            this.RemoteIPAddress  = _DNSClient.Query<A>(RemoteHost).Select(ARecord => ARecord.IPv4Address).ToArray()[1];
            this.RemotePort       = RemotePort;

        }

        #endregion

        #endregion


        #region CreateRequest(HTTPMethod, URI = "/", BuilderAction = null)

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

            var Builder     = new HTTPRequestBuilder() {
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

            var Builder = new HTTPRequestBuilder() {
                HTTPMethod  = new HTTPMethod(HTTPMethod),
                URI     = URI
            };

            BuilderAction.FailSafeInvoke(Builder);

            return Builder;

        }

        #endregion





        public Task<T> Execute<T>(HTTPRequest HTTPRequest, Action<HTTPRequest> HTTPRequestDelegate, Func<HTTPRequest, HTTPResponse, T> RequestResponseDelegate)
        {

            if (HTTPRequestDelegate != null)
                HTTPRequestDelegate(HTTPRequest);

            return Execute(HTTPRequest, RequestResponseDelegate);

        }


        public Task<T> Execute<T>(HTTPRequest HTTPRequest, Func<HTTPRequest, HTTPResponse, T> RequestResponseDelegate)
        {

            return Task<T>.Factory.StartNew(() => {

                T _T = default(T);
                Execute(HTTPRequest, new Action<HTTPRequest, HTTPResponse>((request, response) => _T = RequestResponseDelegate(request, response))).Wait();
                return _T;

            });

        }

        public Task<HTTPClient> Execute(HTTPRequest HTTPRequest, Action<HTTPRequest> HTTPRequestDelegate, Action<HTTPRequest, HTTPResponse> RequestResponseDelegate)
        {

            if (HTTPRequestDelegate != null)
                HTTPRequestDelegate(HTTPRequest);

            return Execute(HTTPRequest, RequestResponseDelegate);

        }

        public Task<HTTPClient> Execute(HTTPRequest                        HTTPRequest,
                                        Action<HTTPRequest, HTTPResponse>  RequestResponseDelegate  = null,
                                        UInt32                             Timeout                  = 20000)
        {

            return Task<HTTPClient>.Factory.StartNew(
                () => { Execute_Synced(HTTPRequest, RequestResponseDelegate, Timeout); return this; },
                TaskCreationOptions.AttachedToParent);

        }


        public HTTPResponse Execute_Synced(HTTPRequest                        HTTPRequest,
                                           Action<HTTPRequest, HTTPResponse>  RequestResponseDelegate  = null,
                                           UInt32                             Timeout                  = 20000)
        {

            //Debug.WriteLine(DateTime.Now + " HTTPClient started...");

            #region Data

            Boolean _EndOfHTTPHeader    = false;
            long    _Length             = 0;
            long    EndOfHeaderPosition = 6;

            #endregion

            try
            {

                if (TCPClient == null)
                    TCPClient = new TcpClient(this.RemoteIPAddress.ToString(), this.RemotePort.ToInt32());

                //TCPClient.ReceiveTimeout = 5000;

                #region Send Request

                // Open stream for reading and writing
                var TCPStream = TCPClient.GetStream();

                var _RequestBytes = (HTTPRequest.EntireRequestHeader + Environment.NewLine + Environment.NewLine).ToUTF8Bytes();
                TCPStream.Write(_RequestBytes, 0, _RequestBytes.Length);

                var RequestBodyLength = (Int32) HTTPRequest.ContentLength;

                if (HTTPRequest.Content != null)
                    RequestBodyLength = Math.Min((Int32)HTTPRequest.ContentLength, HTTPRequest.Content.Length);

                if (HTTPRequest.ContentLength > 0)
                    TCPStream.Write(HTTPRequest.Content, 0, RequestBodyLength);

                var _MemoryStream  = new MemoryStream();
                var _Buffer        = new Byte[10485760]; // A smaller value leads to read errors!
                var sw             = new Stopwatch();

                #endregion

                #region Wait for the server to react!

                sw.Start();

                try
                {

                    while (!TCPStream.DataAvailable)
                    {

                        if (sw.ElapsedMilliseconds >= Timeout)
                        {
                            //Debug.WriteLine(DateTime.Now + " HTTPClient timeout after " + Timeout + "ms!");
                            TCPClient.Close();
                            throw new ApplicationException(DateTime.Now + " Could not read from the TCP stream!");
                        }

                        Thread.Sleep(5);

                    }

                }
                catch (Exception e)
                {
                    Debug.WriteLine(DateTime.Now + " " + e.Message);
                }

                //Debug.WriteLine(DateTime.Now + " HTTPClient got first response after " + sw.ElapsedMilliseconds + "ms!");

                sw.Stop();

                #endregion

                #region Read

                TCPStream.ReadTimeout = 2;

                while (!_EndOfHTTPHeader || TCPStream != null || TCPStream.DataAvailable || !TCPClient.Connected)
                {

                    try
                    {

                        var OldDataSize       = 0L;
                        var EndOfReadTimeout  = 500;

                        #region Read the entire stream into the memory <= Rethink this someday!

                        do
                        {

                            while (TCPStream.DataAvailable)
                            {
                                _MemoryStream.Write(_Buffer, 0, TCPStream.Read(_Buffer, 0, _Buffer.Length));
                                OldDataSize = _MemoryStream.Length;
                                sw.Restart();
                            }

                            if (OldDataSize < _MemoryStream.Length)
                                Debug.WriteLine(DateTime.Now + " Read " + _MemoryStream.Length + " bytes of data (" + sw.ElapsedMilliseconds + "ms)!");

                            Thread.Sleep(5);

                        }
                        while (TCPStream.DataAvailable || sw.ElapsedMilliseconds < EndOfReadTimeout);

                        //Debug.WriteLine(DateTime.Now + " Finally read " + _MemoryStream.Length + " bytes of data (" + sw.ElapsedMilliseconds + "ms)!");

                        sw.Stop();

                        #endregion

                        #region Walk through the stream and search for two consecutive newlines indicating the end of the HTTP header

                        _Length = _MemoryStream.Length;

                        if (_Length > 4)
                        {

                            _MemoryStream.Seek(0, SeekOrigin.Begin);

                            int state = 0;
                            int _int = 0;
                            _EndOfHTTPHeader = false;

                            while (!_EndOfHTTPHeader || _int == -1)
                            {

                                _int = _MemoryStream.ReadByte();

                                switch (state)
                                {
                                    case 0: if (_int == 0x0d) state = 1; else state = 0; break;
                                    case 1: if (_int == 0x0a) state = 2; else state = 0; break;
                                    case 2: if (_int == 0x0d) state = 3; else state = 0; break;
                                    case 3: if (_int == 0x0a) _EndOfHTTPHeader = true; else state = 0; break;
                                    default: state = 0; break;
                                }

                            }

                            if (_EndOfHTTPHeader)
                                break;

                        }

                        #endregion

                    }

                    catch (Exception e)
                    {
                        Debug.WriteLine(DateTime.Now + " " + e.Message);
                    }

                }

                if (_EndOfHTTPHeader == false)
                    throw new ApplicationException(DateTime.Now + " Could not find the end of the HTTP protocol header!");

                EndOfHeaderPosition = _MemoryStream.Position - 3;

                #endregion

                #region Copy HTTP header data

                var HeaderBytes = new Byte[EndOfHeaderPosition - 1];
                _MemoryStream.Seek(0, SeekOrigin.Begin);
                _MemoryStream.Read(HeaderBytes, 0, HeaderBytes.Length);

                var _HTTPResponse = new HTTPResponse(HeaderBytes.ToUTF8String());

                #endregion

                #region Read 'Content-Length' bytes...

                // Copy only the number of bytes given within
                // the HTTP header element 'Content-Length'!
                if (_HTTPResponse.ContentLength.HasValue && _HTTPResponse.ContentLength.Value > 0)
                {

                    try
                    {

                        _MemoryStream.Seek(4, SeekOrigin.Current);
                        var _Read = _MemoryStream.Read(_Buffer, 0, _Buffer.Length);
                        var _StillToRead = (Int32)_HTTPResponse.ContentLength.Value - _Read;
                        _HTTPResponse.ContentStream.Write(_Buffer, 0, _Read);
                        var _CurrentBufferSize = 0;

                        var Retries = 0;

                        while (Retries < 10)
                        {

                            while (TCPStream.DataAvailable && _StillToRead < 0)
                            {
                                _CurrentBufferSize = Math.Min(_Buffer.Length, (Int32)_StillToRead);
                                _Read = TCPStream.Read(_Buffer, 0, _CurrentBufferSize);
                                _HTTPResponse.ContentStream.Write(_Buffer, 0, _Read);
                                _StillToRead -= _Read;
                                Retries = 0;
                            }

                            if (_StillToRead <= 0)
                                break;

                            Thread.Sleep(10);
                            Retries++;

                        }

                        _HTTPResponse.ContentStreamToArray();

                    }

                    catch (Exception e)
                    {
                        Debug.WriteLine(DateTime.Now + " " + e.Message);
                    }

                }

                #endregion

                #region ...or read till timeout!

                else
                {

                    try
                    {

                        _MemoryStream.Seek(4, SeekOrigin.Current);
                        _HTTPResponse.ContentStream.Write(_Buffer, 0, _MemoryStream.Read(_Buffer, 0, _Buffer.Length));

                        var Retries = 0;

                        while (Retries < 10)
                        {

                            while (TCPStream.DataAvailable)
                            {
                                _HTTPResponse.ContentStream.Write(_Buffer, 0, TCPStream.Read(_Buffer, 0, _Buffer.Length));
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
                    TCPStream = null;
                    TCPClient = null;
                }

                #endregion


                #region Call HTTPResponse delegates

                var RequestResponseDelegateLocal = RequestResponseDelegate;
                if (RequestResponseDelegateLocal != null)
                    RequestResponseDelegateLocal(HTTPRequest, _HTTPResponse);

                #endregion

                return _HTTPResponse;

            }
            catch (Exception e)
            {
                Debug.WriteLine(DateTime.Now + " " + e.Message);
            }

            return null;

        }



        public void Close()
        {
            if (TCPClient != null)
                TCPClient.Close();
        }


        #region ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {

            var _TypeName    = this.GetType().Name;
            var _GenericType = this.GetType().GetGenericArguments()[0].Name;

            return String.Concat(RemoteIPAddress.ToString(), ":", RemotePort);

        }

        #endregion


        #region IDisposable Members

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        #endregion

    }

}
