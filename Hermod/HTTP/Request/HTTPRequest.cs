/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.Web;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using System.Net.Sockets;
using System.Threading;
using System.IO;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A read-only HTTP request header.
    /// </summary>
    public class HTTPRequest : AHTTPPDU
    {

        #region Properties

        #region Non-HTTP header fields

        #region CancellationToken

        private readonly CancellationToken _CancellationToken;

        /// <summary>
        /// The cancellation token.
        /// </summary>
        public CancellationToken CancellationToken
        {
            get
            {
                return _CancellationToken;
            }
        }

        #endregion

        #region Timestamp

        private readonly DateTime _Timestamp;

        /// <summary>
        /// The timestamp of the HTTP request generation.
        /// </summary>
        public DateTime Timestamp
        {
            get
            {
                return _Timestamp;
            }
        }

        #endregion

        #region RemoteSocket

        private readonly IPSocket _RemoteSocket;

        /// <summary>
        /// The remote TCP/IP socket.
        /// </summary>
        public IPSocket RemoteSocket
        {
            get
            {
                return _RemoteSocket;
            }
        }

        #endregion

        #region LocalSocket

        private readonly IPSocket _LocalSocket;

        /// <summary>
        /// The local TCP/IP socket.
        /// </summary>
        public IPSocket LocalSocket
        {
            get
            {
                return _LocalSocket;
            }
        }

        #endregion


        #region EntireRequestHeader

        public String EntireRequestHeader
        {
            get
            {
                return HTTPRequestLine + Environment.NewLine + ConstructedHTTPHeader;
            }
        }

        #endregion

        #region HTTPRequestLine

        public String HTTPRequestLine
        {
            get
            {
                return HTTPMethod.ToString() + " " + this.FakeURIPrefix + this.URI + QueryString + " " + ProtocolName + "/" + ProtocolVersion;
            }
        }

        #endregion

        public HTTPContentType BestMatchingAcceptType { get; set; }

        #region HTTPMethod

        private HTTPMethod _x_HTTPMethod;

        /// <summary>
        /// The HTTP method.
        /// </summary>
        public HTTPMethod HTTPMethod
        {
            get
            {
                return _x_HTTPMethod;
            }
            protected set {
                _x_HTTPMethod = value;
            }
        }

        #endregion

        #region FakeURIPrefix

        public String FakeURIPrefix { get; set; }

        #endregion

        #region URI

        /// <summary>
        /// The minimal URI (this means e.g. without the query string).
        /// </summary>
        public String URI { get; protected set; }

        #endregion

        #region QueryString

        /// <summary>
        /// The HTTP query string.
        /// </summary>
        public QueryString QueryString { get; protected set; }

        #endregion

        public String[] ParsedURIParameters { get; set; }

        #endregion

        #region Request header fields

        #region Accept

        protected AcceptTypes _Accept;

        /// <summary>
        /// The http content types accepted by the client.
        /// </summary>
        public AcceptTypes Accept
        {

            get
            {

                _Accept = GetHeaderField<AcceptTypes>("Accept");
                if (_Accept != null)
                    return _Accept;

                var _AcceptString = GetHeaderField<String>("Accept");

                if (!_AcceptString.IsNullOrEmpty())
                {
                    _Accept = new AcceptTypes(_AcceptString);
                    SetHeaderField("Accept", _Accept);
                    return _Accept;
                }

                else
                    return new AcceptTypes();

            }

        }

        #endregion

        #region Accept-Charset

        public String AcceptCharset
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.AcceptCharset);
            }
        }

        #endregion

        #region Accept-Encoding

        public String AcceptEncoding
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.AcceptEncoding);
            }
        }

        #endregion

        #region Accept-Language

        public String AcceptLanguage
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.AcceptLanguage);
            }
        }

        #endregion

        #region Accept-Ranges

        public String AcceptRanges
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.AcceptRanges);
            }
        }

        #endregion

        #region Authorization

        public HTTPBasicAuthentication Authorization
        {
            get
            {

                var _Authorization = GetHeaderField<HTTPBasicAuthentication>("Authorization");
                if (_Authorization != null)
                    return _Authorization;

                var _AuthString = GetHeaderField<String>("Authorization");

                if (_AuthString == null)
                    return null;

                _Authorization = new HTTPBasicAuthentication(_AuthString);

                SetHeaderField("Authorization", _Authorization);

                return _Authorization;

            }
        }

        #endregion

        #region Depth

        public String Depth
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Depth);
            }
        }

        #endregion

        #region Destination

        public String Destination
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Destination);
            }
        }

        #endregion

        #region Expect

        public String Expect
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Expect);
            }
        }

        #endregion

        #region From

        public String From
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.From);
            }
        }

        #endregion

        #region Host

        public HTTPHostname Host
        {
            get
            {
                return HTTPHostname.Parse(GetHeaderField(HTTPHeaderField.Host));
            }
        }

        #endregion

        #region If

        public String If
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.If);
            }
        }

        #endregion

        #region If-Match

        public String IfMatch
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfMatch);
            }
        }

        #endregion

        #region If-Modified-Since

        public String IfModifiedSince
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfModifiedSince);
            }
        }

        #endregion

        #region If-None-Match

        public String IfNoneMatch
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfNoneMatch);
            }
        }

        #endregion

        #region If-Range

        public String IfRange
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfRange);
            }
        }

        #endregion

        #region If-Unmodified-Since

        public String IfUnmodifiedSince
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.IfUnmodifiedSince);
            }
        }

        #endregion

        #region Lock-Token

        public String LockToken
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.LockToken);
            }
        }

        #endregion

        #region MaxForwards

        public UInt64? MaxForwards
        {
            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.MaxForwards);
            }
        }

        #endregion

        #region Overwrite

        public String Overwrite
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Overwrite);
            }
        }

        #endregion

        #region Proxy-Authorization

        public String ProxyAuthorization
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.ProxyAuthorization);
            }
        }

        #endregion

        #region Range

        public String Range
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Range);
            }
        }

        #endregion

        #region Referer

        public String Referer
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Referer);
            }
        }

        #endregion

        #region TE

        public String TE
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.TE);
            }
        }

        #endregion

        #region Timeout

        public UInt64? Timeout
        {
            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.Timeout);
            }
        }

        #endregion

        #region User-Agent

        public String UserAgent
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.UserAgent);
            }
        }

        #endregion

        #region Last-Event-Id

        public UInt64? LastEventId
        {
            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.LastEventId);
            }
        }

        #endregion

        #region Cookie

        public String Cookie
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Cookie);
            }
        }

        #endregion

        #endregion

        #region Non-standard request header fields

        #region X-Real-IP

        /// <summary>
        /// Intermediary HTTP proxies might include this field to
        /// indicate the real IP address of the HTTP client.
        /// </summary>
        /// <example>X-Real-IP: 95.91.73.30</example>
        public IIPAddress X_Real_IP
        {

            get
            {

                Object Value;

                if (!TryGetHeaderField(HTTPHeaderField.X_Real_IP, out Value))
                    return null;

                IPv4Address IPv4;
                IPv6Address IPv6;

                if      (IPv4Address.TryParse((String) Value, out IPv4))
                    return IPv4;

                else if (IPv6Address.TryParse((String) Value, out IPv6))
                    return IPv6;

                else return null;

            }

        }

        #endregion

        #region X-Forwarded-For

        /// <summary>
        /// Intermediary HTTP proxies might include this field to
        /// indicate the real IP address of the HTTP client.
        /// </summary>
        /// <example>X-Forwarded-For: 95.91.73.30</example>
        public IIPAddress X_Forwarded_For
        {

            get
            {

                Object Value;

                if (!TryGetHeaderField(HTTPHeaderField.X_Forwarded_For, out Value))
                    return null;

                IPv4Address IPv4;
                IPv6Address IPv6;

                if      (IPv4Address.TryParse((String) Value, out IPv4))
                    return IPv4;

                else if (IPv6Address.TryParse((String) Value, out IPv6))
                    return IPv6;

                else return null;

            }

        }

        #endregion

        #endregion

        #region HTTP Body

        private readonly Stream _HTTPBodyStream;

        public Stream HTTPBodyStream
        {
            get
            {
                return _HTTPBodyStream;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPRequest(RemoteSocket, LocalSocket, HTTPHeader, HTTPBodyStream, CancellationToken)

        /// <summary>
        /// Create a new http request header based on the given string representation.
        /// </summary>
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="LocalSocket">The local TCP/IP socket.</param>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        /// <param name="HTTPBodyStream">The networks stream of the HTTP request.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        private HTTPRequest(IPSocket           RemoteSocket,
                            IPSocket           LocalSocket,
                            String             HTTPHeader,
                            Stream             HTTPBodyStream,
                            CancellationToken  CancellationToken)

            : this(HTTPHeader, null, CancellationToken)

        {

            this._RemoteSocket    = RemoteSocket;
            this._LocalSocket     = LocalSocket;
            this._HTTPBodyStream  = HTTPBodyStream;

            if (!HeaderFields.ContainsKey("Host"))
                HeaderFields.Add("Host", "*");

        }

        #endregion

        #region HTTPRequest(HTTPHeader, Content, CancellationToken)

        /// <summary>
        /// Create a new http request header based on the given string representation.
        /// </summary>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        private HTTPRequest(String             HTTPHeader,
                            Byte[]             Content,
                            CancellationToken  CancellationToken)
        {

            this._CancellationToken  = CancellationToken;

            this._Timestamp   = DateTime.Now;
            this.QueryString  = new QueryString();
            this.Content      = Content;

            if (!TryParseHeader(HTTPHeader))
                return;

            #region Parse HTTPMethod (first line of the http request)

            var _HTTPMethodHeader = FirstPDULine.Split(_SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);

            // e.g: PROPFIND /file/file Name HTTP/1.1
            if (_HTTPMethodHeader.Length != 3)
            {
                this.HTTPStatusCode = HTTPStatusCode.BadRequest;
                return;
            }

            // Parse HTTP method
            // Propably not usefull to define here, as we can not send a response having an "Allow-header" here!
            HTTPMethod _HTTPMethod;
            this.HTTPMethod = (HTTPMethod.TryParseString(_HTTPMethodHeader[0], out _HTTPMethod)) ? _HTTPMethod : new HTTPMethod(_HTTPMethodHeader[0]);

            #endregion

            #region Parse URL and QueryString (first line of the http request)

            var RawUrl     = _HTTPMethodHeader[1];
            var _ParsedURL = RawUrl.Split(_URLSeparator, 2, StringSplitOptions.None);
            URI            = HttpUtility.UrlDecode(_ParsedURL[0]);

            if (URI == "" || URI == null)
                URI = "/";

            // Parse QueryString after '?'
            if (RawUrl.IndexOf('?') > -1 && _ParsedURL[1].IsNeitherNullNorEmpty())
                this.QueryString = new QueryString(_ParsedURL[1]);

            #endregion

            #region Parse protocol name and -version (first line of the http request)

            var _ProtocolArray  = _HTTPMethodHeader[2].Split(_SlashSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
            ProtocolName        = _ProtocolArray[0].ToUpper();

            if (ProtocolName.ToUpper() != "HTTP")
            {
                this.HTTPStatusCode = HTTPStatusCode.InternalServerError;
                return;
            }

            HTTPVersion _HTTPVersion = null;
            if (HTTPVersion.TryParseVersionString(_ProtocolArray[1], out _HTTPVersion))
                ProtocolVersion = _HTTPVersion;
            if (ProtocolVersion != HTTPVersion.HTTP_1_0 && ProtocolVersion != HTTPVersion.HTTP_1_1)
            {
                this.HTTPStatusCode = HTTPStatusCode.HTTPVersionNotSupported;
                return;
            }

            #endregion

            this.HTTPStatusCode = HTTPStatusCode.OK;

        }

        #endregion

        #endregion


        public static Boolean TryParse(String             HTTPRequestString,
                                       CancellationToken  CancellationToken,
                                       out HTTPRequest    HTTPRequest)
        {

            HTTPRequest = new HTTPRequest(HTTPRequestString, null, CancellationToken);

            return true;

        }

        public static Boolean TryParse(String             HTTPHeader,
                                       Byte[]             HTTPBody,
                                       CancellationToken  CancellationToken,
                                       out HTTPRequest    HTTPRequest)
        {

            HTTPRequest = new HTTPRequest(HTTPHeader, HTTPBody, CancellationToken);

            return true;

        }

        public static Boolean TryParse(IPSocket           RemoteSocket,
                                       IPSocket           LocalSocket,
                                       String             HTTPHeader,
                                       Stream             HTTPBodyStream,
                                       CancellationToken  CancellationToken,
                                       out HTTPRequest    HTTPRequest)
        {

            HTTPRequest = new HTTPRequest(RemoteSocket, LocalSocket, HTTPHeader, HTTPBodyStream, CancellationToken);

            return true;

        }


        //ToDo: Fix me for slow clients!
        public Boolean TryReadHTTPBody()
        {

            if (Content != null)
                return true;

            var Buffer  = new Byte[(Int32) ContentLength.Value];
            Thread.Sleep(500);
            var Read    = _HTTPBodyStream.Read(Buffer, 0, (Int32) ContentLength.Value);

            if (Read == (Int32) ContentLength.Value)
            {
                Content = Buffer;
                return true;
            }

            return false;

        }


        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {
            return EntireRequestHeader;
        }

        #endregion

    }

}
