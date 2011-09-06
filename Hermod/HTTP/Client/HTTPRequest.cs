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
using System.Net;

using de.ahzf.Hermod.Sockets.TCP;
using de.ahzf.Hermod.Datastructures;
using de.ahzf.Hermod.HTTP.Common;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A HTTP client request.
    /// </summary>
    public class HTTPRequest
    {

        #region Properties

        #region HTTPClient

        public HTTPClient HTTPClient { get; private set; }

        #endregion

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

        #region HTTPRequestHeader

        public HTTPRequestHeader_RW HTTPRequestHeader { get; private set; }

        #endregion

        public Byte[] ResponseBody { get; protected set; }

        public String _Response { get; private set; }

        #endregion

        #region Events

        public delegate void NewHTTPServiceHandler(String myHTTPServiceType);
        public event         NewHTTPServiceHandler OnNewHTTPService;

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
                throw new ArgumentNullException("The given HTTPClient must not be null!");

            #endregion

            this.HTTPClient                     = HTTPClient;
            this.HTTPRequestHeader              = new HTTPRequestHeader_RW();
            this.HTTPRequestHeader.HTTPMethod   = (HTTPMethod == null) ? HTTPMethod.GET : HTTPMethod;
            this.URLPattern                     = URLPattern;

        }

        #endregion
                     
        #endregion



        public void Execute()
        {

            Boolean _EndOfHTTPHeader = false;
            long    _Length          = 0;
            long    _ReadPosition    = 6;

            // Client initialisieren und mit dem Server verbinden
            var TCPClient = new TcpClient("localhost", this.HTTPClient.Port.ToInt32());
   //         _TCPClient.ReceiveTimeout = 5000;
            
            // Stream für lesen und schreiben holen
            var _HTTPStream         = TCPClient.GetStream();
            var _RequestBytes       = this.ToString().ToUTF8Bytes();
            _HTTPStream.Write(_RequestBytes, 0, _RequestBytes.Length);

            var _MemoryStream       = new MemoryStream();
            var _Buffer             = new Byte[65535];
            Byte[] _ResponseBytes   = null;

            // Wait for the server to react!
            while (!_HTTPStream.DataAvailable)
                Thread.Sleep(10);

            while (!_EndOfHTTPHeader || _HTTPStream.DataAvailable || !TCPClient.Connected)
            {

                while (_HTTPStream.DataAvailable)
                {
                    _MemoryStream.Write(_Buffer, 0, _HTTPStream.Read(_Buffer, 0, _Buffer.Length));
                }

                _Length = _MemoryStream.Length;

                if (_Length > 4)
                {

                    _ResponseBytes = _MemoryStream.ToArray();
                    _ReadPosition = _ReadPosition - 3;

                    while (_ReadPosition < _Length)
                    {

                        if (_ResponseBytes[_ReadPosition - 3] == 0x0d &&
                            _ResponseBytes[_ReadPosition - 2] == 0x0a &&
                            _ResponseBytes[_ReadPosition - 1] == 0x0d &&
                            _ResponseBytes[_ReadPosition    ] == 0x0a)
                        {
                            _EndOfHTTPHeader = true;
                            break;
                        }

                        _ReadPosition++;

                    }

                    if (_EndOfHTTPHeader)
                        break;

                }

            }

            if (_EndOfHTTPHeader == false)
                throw new Exception("Protocol Error!");

            // Create a new HTTPResponseHeader
            var HeaderBytes = new Byte[_ReadPosition - 1];
            Array.Copy(_ResponseBytes, 0, HeaderBytes, 0, _ReadPosition - 1);

            HTTPStatusCode __HTTPStatusCode = null;
            var ResponseHeader = new HTTPResponseHeader(Encoding.UTF8.GetString(HeaderBytes), out __HTTPStatusCode);

            // Copy only the number of bytes given within
            // the HTTP header element 'ContentType'!
            if (ResponseHeader.ContentLength.HasValue)
            {
                ResponseBody = new Byte[ResponseHeader.ContentLength.Value];
                Array.Copy(_ResponseBytes, _ReadPosition + 1, ResponseBody, 0, (Int64)ResponseHeader.ContentLength.Value);
            }
            else
                ResponseBody = new Byte[0];

            // The parsing of the http header failed!
            if (__HTTPStatusCode != HTTPStatusCode.OK)
            {
                throw new Exception(__HTTPStatusCode.ToString());
            }


            
            //var _ReallyRead    = _TCPStream.Read(_ResponseBytes, 0, 4096);
                  _Response      = _ResponseBytes.ToUTF8String();

            // Hier kann in den Stream geschrieben werden
            // oder aus dem Stream gelesen werden
            // Verbindung schließen
            TCPClient.Close();

        }



        #region ToString()

        public override String ToString()
        {
            return HTTPRequestHeader.HTTPMethod.ToString() + " " + this.URLPattern + " " + HTTPRequestHeader.ProtocolName + "/" + HTTPRequestHeader.ProtocolVersion + Environment.NewLine + "Host: 127.0.0.1" + Environment.NewLine + Environment.NewLine;
        }

        #endregion

    }

}
