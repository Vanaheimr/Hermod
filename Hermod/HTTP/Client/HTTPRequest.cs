/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A HTTP client request.
    /// </summary>
    public class HTTPRequest
    {

        #region Properties

        /// <summary>
        /// The associated HTTPClient.
        /// </summary>
        public HTTPClient           HTTPClient        { get; private set; }


        public HTTPRequestBuilder HTTPRequestHeader { get; private set; }


        public Byte[]               ResponseBody      { get; protected set; }



        #region HTTPMethod

        public HTTPMethod HTTPMethod
        {
            get
            {
                return HTTPRequestHeader.HTTPMethod;
            }
        }

        #endregion

        #region URLPattern

        /// <summary>
        /// The URL pattern to which the HTTPClient connects.
        /// </summary>
        public String URLPattern { get; private set; }

        #endregion

        #endregion

        #region Events

        #endregion

        #region Constructor(s)

        #region HTTPRequest(HTTPClient, HTTPMethod => GET, URLPattern = "/")

        /// <summary>
        /// Create a new HTTP client request.
        /// </summary>
        /// <param name="HTTPClient">The associated HTTP client.</param>
        /// <param name="HTTPMethod">An optional HTTP method (default: GET).</param>
        /// <param name="URLPattern">An optional URL pattern (default: '/').</param>
        public HTTPRequest(HTTPClient HTTPClient, HTTPMethod HTTPMethod = null, String URLPattern = "/")
        {

            #region Initial checks

            if (HTTPClient == null)
                throw new ArgumentNullException("The given associated HTTPClient must not be null!");

            #endregion

            this.HTTPClient                   = HTTPClient;
            this.HTTPRequestHeader            = new HTTPRequestBuilder();
            this.HTTPRequestHeader.HTTPMethod = (HTTPMethod == null) ? HTTPMethod.GET : HTTPMethod;
            this.URLPattern                   = URLPattern;

        }

        #endregion
                     
        #endregion



        public Task<HTTPRequest> Execute()
        {

            return Task<HTTPRequest>.Factory.StartNew(() =>
            {

                Boolean _EndOfHTTPHeader = false;
                long   _Length        = 0;
                long   _ReadPosition  = 6;

                // Client initialisieren und mit dem Server verbinden
                var     TCPClient     = new TcpClient("localhost", this.HTTPClient.Port.ToInt32());
                //         _TCPClient.ReceiveTimeout = 5000;

                #region Open stream for reading and writting

                var    _TCPStream     = TCPClient.GetStream();
                var    _RequestBytes  = (HTTPHeader.Aggregate((a, b) => a + Environment.NewLine + b) + Environment.NewLine + Environment.NewLine).ToUTF8Bytes();
                _TCPStream.Write(_RequestBytes, 0, _RequestBytes.Length);

                var    _MemoryStream  = new MemoryStream();
                var    _Buffer        = new Byte[65535];
                //Byte[] _ResponseBytes = null;
                var    sw             = new Stopwatch();

                #endregion

                #region Wait for the server to react!

                sw.Start();

                while (!_TCPStream.DataAvailable || sw.ElapsedMilliseconds > 20000)
                    Thread.Sleep(1);

                if (!_TCPStream.DataAvailable && sw.ElapsedMilliseconds > 20000)
                {
                    TCPClient.Close();
                    throw new ApplicationException("Could not read from the TCP stream!");
                }

                sw.Stop();

                #endregion

                #region Read

                while (!_EndOfHTTPHeader || _TCPStream.DataAvailable || !TCPClient.Connected)
                {

                    #region Read the entire stream into the memory <= Rethink this someday!

                    while (_TCPStream.DataAvailable)
                        _MemoryStream.Write(_Buffer, 0, _TCPStream.Read(_Buffer, 0, _Buffer.Length));

                    #endregion

                    #region Walk through the stream and search for two consecutive newlines indicating the end of the HTTP header

                    _Length = _MemoryStream.Length;

                    if (_Length > 4)
                    {

                        _MemoryStream.Seek(0, SeekOrigin.Begin);

                        int state = 0;
                        int _int  = 0;
                        _EndOfHTTPHeader = false;

                        while (!_EndOfHTTPHeader || _int == -1)
                        {
                            
                            _int = _MemoryStream.ReadByte();

                            switch (state)
                            {
                                case 0 : if (_int == 0x0d) state = 1; else state = 0; break;
                                case 1 : if (_int == 0x0a) state = 2; else state = 0; break;
                                case 2 : if (_int == 0x0d) state = 3; else state = 0; break;
                                case 3 : if (_int == 0x0a) _EndOfHTTPHeader = true; else state = 0; break;
                                default : state = 0; break;
                            }

                        }

                        if (_EndOfHTTPHeader)
                            break;

                    }

                    #endregion

                }

                if (_EndOfHTTPHeader == false)
                    throw new ApplicationException("Could not find the end of the HTTP protocol header!");

                _ReadPosition = _MemoryStream.Position - 3;

                #endregion

                #region Create a new HTTPResponseHeader

                // Copy HTTP header data
                var HeaderBytes = new Byte[_ReadPosition - 1];
                _MemoryStream.Seek(0, SeekOrigin.Begin);
                _MemoryStream.Read(HeaderBytes, 0, HeaderBytes.Length);

                // Parse HTTP header
                var ResponseHeader = new HTTPResponseHeader(Encoding.UTF8.GetString(HeaderBytes));

                // The parsing of the http header failed!
                if (ResponseHeader.HTTPStatusCode != HTTPStatusCode.OK)
                    throw new Exception(ResponseHeader.HTTPStatusCode.ToString());

                #endregion

                // Copy only the number of bytes given within
                // the HTTP header element 'Content-Length'!
                if (ResponseHeader.ContentLength.HasValue)
                {
                    ResponseBody = new Byte[ResponseHeader.ContentLength.Value];
                    _MemoryStream.Seek(4, SeekOrigin.Current);
                    _MemoryStream.Read(ResponseBody, 0, ResponseBody.Length);
                }
                else
                    ResponseBody = new Byte[0];

                // Verbindung schließen
                TCPClient.Close();

                return this;

            }, TaskCreationOptions.AttachedToParent);

        }


        public String HTTPRequestLine
        {
            get
            {
                return HTTPRequestHeader.HTTPMethod.ToString() + " " + this.URLPattern + " " + HTTPRequestHeader.ProtocolName + "/" + HTTPRequestHeader.ProtocolVersion;
            }
        }

        public IEnumerable<String> HTTPHeader
        {
            get
            {

                yield return HTTPRequestLine;

                foreach (var field in HTTPRequestHeader)
                    yield return field;

            }
        }


        #region Host

        /// <summary>
        /// Sets the Host header.
        /// </summary>
        /// <param name="Host">The value of the Host header.</param>
        public HTTPRequest Host(String Host)
        {
            this.HTTPRequestHeader.Host = Host;
            return this;
        }

        #endregion

        #region Accept

        /// <summary>
        /// Set the accept header.
        /// </summary>
        /// <param name="HTTPContentType"></param>
        /// <param name="Quality"></param>
        public HTTPRequest Accept(HTTPContentType HTTPContentType, Double Quality = 1)
        {

            if (this.HTTPRequestHeader.Accept == null)
                this.HTTPRequestHeader.Accept = new List<AcceptType>();

            this.HTTPRequestHeader.Accept.Clear();
            this.HTTPRequestHeader.Accept.Add(new AcceptType(HTTPContentType, Quality));
            
            return this;

        }

        #endregion

        #region AddAccept

        /// <summary>
        /// Add a content type to the accept header.
        /// </summary>
        /// <param name="HTTPContentType"></param>
        /// <param name="Quality"></param>
        public HTTPRequest AddAccept(HTTPContentType HTTPContentType, Double Quality = 1)
        {
            
            if (this.HTTPRequestHeader.Accept == null)
                this.HTTPRequestHeader.Accept = new List<AcceptType>();

            this.HTTPRequestHeader.Accept.Add(new AcceptType(HTTPContentType, Quality));
            
            return this;

        }

        #endregion


        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {
            return HTTPRequestLine + Environment.NewLine + "Host: 127.0.0.1" + Environment.NewLine + Environment.NewLine;
        }

        #endregion

    }

}
