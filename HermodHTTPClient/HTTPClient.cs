/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Hermod.Sockets.TCP;
using eu.Vanaheimr.Hermod.Datastructures;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

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

        #endregion

        #region Events


        #endregion

        #region Constructor(s)

        #region HTTPClient(IPAddress = null, Port = null)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteIPAddress">The IP address to connect to.</param>
        /// <param name="RemotePort">The IP port to connect to.</param>
        public HTTPClient(IIPAddress RemoteIPAddress = null, IPPort RemotePort = null)
        {
            this.RemoteIPAddress = RemoteIPAddress;
            this.RemotePort      = RemotePort;
        }

        #endregion

        #region HTTPClient(Socket)

        /// <summary>
        /// Create a new HTTPClient using the given optional parameters.
        /// </summary>
        /// <param name="RemoteSocket">The IP socket to connect to.</param>
        public HTTPClient(IPSocket RemoteSocket)
        {
            this.RemoteIPAddress = RemoteSocket.IPAddress;
            this.RemotePort      = RemoteSocket.Port;
        }

        #endregion

        #endregion


        #region CreateRequest(HTTPMethod, UrlPath = "/")

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        /// <param name="HTTPMethod">A HTTP method.</param>
        /// <param name="UrlPath">An URL path.</param>
        /// <returns>A new HTTPRequest object.</returns>
        public HTTPRequestBuilder CreateRequest(HTTPMethod HTTPMethod, String UrlPath = "/")
        {
            return new HTTPRequestBuilder()
            {
                HTTPMethod = HTTPMethod,
                UrlPath    = UrlPath
            };
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

        public Task<HTTPClient> Execute(HTTPRequest HTTPRequest, Action<HTTPRequest, HTTPResponse> RequestResponseDelegate)
        {

            return Task<HTTPClient>.Factory.StartNew(() =>
            {

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
                    var Timeout = 20000;

                    #region Send Request

                    // Open stream for reading and writting
                    var TCPStream = TCPClient.GetStream();

                    var _RequestBytes = (HTTPRequest.EntireRequestHeader + Environment.NewLine + Environment.NewLine).ToUTF8Bytes();
                    TCPStream.Write(_RequestBytes, 0, _RequestBytes.Length);

                    var RequestBodyLength = (Int32)HTTPRequest.ContentLength;

                    if (HTTPRequest.Content != null)
                        RequestBodyLength = Math.Min((Int32)HTTPRequest.ContentLength, HTTPRequest.Content.Length);

                    if (HTTPRequest.ContentLength > 0)
                        TCPStream.Write(HTTPRequest.Content, 0, RequestBodyLength);

                    var _MemoryStream = new MemoryStream();
                    var _Buffer = new Byte[65535];
                    var sw = new Stopwatch();

                    #endregion

                    #region Wait for the server to react!

                    sw.Start();

                    try
                    {

                        while (!TCPStream.DataAvailable)
                        {

                            if (sw.ElapsedMilliseconds >= Timeout)
                            {
                                TCPClient.Close();
                                throw new ApplicationException("Could not read from the TCP stream!");
                            }

                            Thread.Sleep(1);

                        }

                    }
                    catch (Exception e)
                    {
                        var f = e.Message + "...";
                    }

                    sw.Stop();

                    #endregion

                    #region Read

                    TCPStream.ReadTimeout = 2;

                    while (!_EndOfHTTPHeader || TCPStream != null || TCPStream.DataAvailable || !TCPClient.Connected)
                    {

                        #region Read the entire stream into the memory <= Rethink this someday!

                        while (TCPStream.DataAvailable)
                            _MemoryStream.Write(_Buffer, 0, TCPStream.Read(_Buffer, 0, _Buffer.Length));

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

                    if (_EndOfHTTPHeader == false)
                        throw new ApplicationException("Could not find the end of the HTTP protocol header!");

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
                    if (_HTTPResponse.ContentLength.HasValue)
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

                    #endregion

                    #region ...or read till timeout!

                    else
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

                            var TEContent = ((MemoryStream)_HTTPResponse.ContentStream).ToArray();
                            var TEMemStram = new MemoryStream();
                            var LastPos = 0;

                            var i = 0;
                            do
                            {

                                if (TEContent[i] == '\n' && TEContent[i - 1] == '\r')
                                {

                                    var Byte1 = new Byte[i - 1 - LastPos];
                                    Array.Copy(TEContent, LastPos, Byte1, 0, i - 1 - LastPos);
                                    var len = Convert.ToInt32(Byte1.ToUTF8String(), 16);
                                    TEMemStram.Write(TEContent, i + 1, len);
                                    i = i + len + 1;

                                    if (TEContent[i] == 0x0d)
                                        i++;

                                    if (TEContent[i] == 0x0a)
                                        i++;

                                    LastPos = i;
                                    i--;

                                }

                                i++;

                            } while (i < TEContent.Length);

                            _HTTPResponse.ContentStreamToArray(TEMemStram);

                        }

                        else
                            _HTTPResponse.ContentStreamToArray();

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

                    if (RequestResponseDelegate != null)
                        RequestResponseDelegate(HTTPRequest, _HTTPResponse);

                    #endregion

                }
                catch (Exception e)
                {
                    
                }

                return this;

            }, TaskCreationOptions.AttachedToParent);

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
