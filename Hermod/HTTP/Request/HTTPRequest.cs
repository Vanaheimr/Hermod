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
using System.Web;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP request.
    /// </summary>
    public class HTTPRequest : AHTTPPDU
    {

        #region Properties

        /// <summary>
        /// The HTTP server of this request.
        /// </summary>
        public HTTPServer       HTTPServer                  { get; }

        /// <summary>
        /// Add this prefix to the URI before sending the request.
        /// </summary>
        public String           FakeURIPrefix               { get; internal set; }

        /// <summary>
        /// The best matching accept type.
        /// Set by the HTTP server.
        /// </summary>
        public HTTPContentType  BestMatchingAcceptType      { get; internal set; }

        #endregion

        #region First request header line

        /// <summary>
        /// The HTTP method.
        /// </summary>
        public HTTPMethod   HTTPMethod              { get; }

        /// <summary>
        /// The minimal URI (this means e.g. without the query string).
        /// </summary>
        public HTTPPath     URI                     { get; }

        /// <summary>
        /// The parsed URI parameters of the best matching URI template.
        /// Set by the HTTP server.
        /// </summary>
        public String[]     ParsedURIParameters     { get; internal set; }

        /// <summary>
        /// The HTTP query string.
        /// </summary>
        public QueryString  QueryString             { get; }

        /// <summary>
        /// The HTTP protocol name field.
        /// </summary>
        public String       ProtocolName            { get; }

        /// <summary>
        /// The HTTP protocol version.
        /// </summary>
        public HTTPVersion  ProtocolVersion         { get; }

        /// <summary>
        /// Construct the entire HTTP request header.
        /// </summary>
        public String       EntireRequestHeader

            => String.Concat(HTTPMethod, " ",
                             FakeURIPrefix, URI, QueryString, " ",
                             ProtocolName, "/", ProtocolVersion, "\r\n",

                             ConstructedHTTPHeader);

        #endregion

        #region Standard     request header fields

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

        /// <summary>
        /// The HTTP basic authentication.
        /// </summary>
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

                if (HTTPBasicAuthentication.TryParse(_AuthString, out _Authorization))
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
            => HTTPHostname.Parse(GetHeaderField(HTTPHeaderField.Host));

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
            => GetHeaderField(HTTPHeaderField.Range);

        #endregion

        #region Referer

        public String Referer
            => GetHeaderField(HTTPHeaderField.Referer);

        #endregion

        #region TE

        public String TE
            => GetHeaderField(HTTPHeaderField.TE);

        #endregion

        #region Timeout

        public TimeSpan? Timeout
        {
            get
            {

                var Seconds = GetHeaderField_UInt64(HTTPHeaderField.Timeout);

                if (Seconds.HasValue)
                    return TimeSpan.FromSeconds(Seconds.Value);

                return new TimeSpan?();

            }
        }

        #endregion

        #region User-Agent

        public String UserAgent
            => GetHeaderField(HTTPHeaderField.UserAgent);

        #endregion

        #region Last-Event-Id

        public UInt64? LastEventId
            => GetHeaderField_UInt64(HTTPHeaderField.LastEventId);

        #endregion

        #region Cookie

        /// <summary>
        /// HTTP cookies.
        /// </summary>
        public HTTPCookies Cookies
            => HTTPCookies.Parse(GetHeaderField(HTTPHeaderField.Cookie));

        #endregion

        #region DNT

        /// <summary>
        /// Do Not Track
        /// </summary>
        public Boolean DNT
            => GetHeaderField(HTTPHeaderField.DNT) != "0";

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

                if (!TryGetHeaderField(HTTPHeaderField.X_Real_IP, out Object Value))
                    return null;

                if      (IPv4Address.TryParse((String) Value, out IPv4Address IPv4))
                    return IPv4;

                else if (IPv6Address.TryParse((String) Value, out IPv6Address IPv6))
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
        public IEnumerable<IIPAddress> X_Forwarded_For
        {

            get
            {

                if (!TryGetHeaderField(HTTPHeaderField.X_Forwarded_For, out Object Value))
                    return null;

                if (Value is String list)
                {
                    return list.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).
                                Select(_ => IPAddress.Parse(_.Trim()));
                }

                return null;

            }

        }

        #endregion

        #region API-Key

        /// <summary>
        /// An optional API key for authentication.
        /// </summary>
        /// <example>API-Key: vfsf87wefh8743tzfgw9f489fh9fgs9z9z237hd208du79ehcv86egfsrf</example>
        public APIKey? API_Key
        {

            get
            {

                if (!TryGetHeaderField(HTTPHeaderField.API_Key, out Object Value))
                    return null;

                if (Value is String)
                    return APIKey.TryParse(Value as String);

                else
                    return APIKey.TryParse(Value.ToString());

            }

        }

        #endregion

        #region X-Portal

        /// <summary>
        /// This is a non-standard HTTP header to idicate that the intended
        /// HTTP portal is calling. By this a special HTTP content type processing
        /// might be implemented, which is different from the processing of other
        /// HTTP client requests.
        /// </summary>
        /// <example>X-Portal: true</example>
        public Boolean X_Portal
        {

            get
            {

                if (!TryGetHeaderField(HTTPHeaderField.X_Portal, out Object Value))
                    return false;

                if (Value is Boolean boolean)
                    return boolean;

                return Value is String text && text == "true";

            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region (internal) HTTPRequest(Timestamp, RemoteSocket, LocalSocket, HTTPServer, HTTPHeader, HTTPBody = null, HTTPBodyStream = null, CancellationToken = null, EventTrackingId = null)

        /// <summary>
        /// Create a new http request header based on the given string representation.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="LocalSocket">The local TCP/IP socket.</param>
        /// <param name="HTTPServer">The HTTP server who has received this request.</param>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        /// <param name="HTTPBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPBodyStream">The HTTP body as an stream of bytes.</param>
        /// 
        /// <param name="HTTPBodyReceiveBufferSize">The size of the HTTP body receive buffer.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        internal HTTPRequest(DateTime            Timestamp,
                             HTTPSource          RemoteSocket,
                             IPSocket            LocalSocket,
                             HTTPServer          HTTPServer,

                             String              HTTPHeader,
                             Byte[]              HTTPBody                    = null,
                             Stream              HTTPBodyStream              = null,

                             UInt32              HTTPBodyReceiveBufferSize   = DefaultHTTPBodyReceiveBufferSize,
                             CancellationToken?  CancellationToken           = null,
                             EventTracking_Id    EventTrackingId             = null)

            : base(Timestamp,
                   RemoteSocket,
                   LocalSocket,
                   HTTPHeader,
                   HTTPBody,
                   HTTPBodyStream,
                   HTTPBodyReceiveBufferSize,
                   CancellationToken,
                   EventTrackingId)

        {

            this.HTTPServer = HTTPServer;

            #region Parse HTTPMethod (first line of the http request)

            var _HTTPMethodHeader = FirstPDULine.Split(_SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);

            // e.g: PROPFIND /file/file Name HTTP/1.1
            if (_HTTPMethodHeader.Length != 3)
                throw new Exception("Bad request");

            // Parse HTTP method
            // Propably not usefull to define here, as we can not send a response having an "Allow-header" here!
            this.HTTPMethod = (HTTPMethod.TryParseString(_HTTPMethodHeader[0], out HTTPMethod _HTTPMethod))
                                  ? _HTTPMethod
                                  : HTTPMethod.Create(_HTTPMethodHeader[0]);

            #endregion

            #region Parse URL and QueryString (first line of the http request)

            var RawUrl      = _HTTPMethodHeader[1];
            var _ParsedURL  = RawUrl.Split(_URLSeparator, 2, StringSplitOptions.None);
            this.URI        = HTTPPath.Parse(HttpUtility.UrlDecode(_ParsedURL[0]));

            //if (URI.StartsWith("http", StringComparison.Ordinal) || URI.StartsWith("https", StringComparison.Ordinal))
            if (URI.Contains("://"))
            {
                URI = URI.Substring(URI.IndexOf("://", StringComparison.Ordinal) + 3);
                URI = URI.Substring(URI.IndexOf("/",   StringComparison.Ordinal));
            }

            if (URI == "" || URI == null)
                URI = HTTPPath.Parse("/");

            // Parse QueryString after '?'
            if (RawUrl.IndexOf('?') > -1 && _ParsedURL[1].IsNeitherNullNorEmpty())
                this.QueryString = QueryString.Parse(_ParsedURL[1]);
            else
                this.QueryString = QueryString.New;

            #endregion

            #region Parse protocol name and -version (first line of the http request)

            var _ProtocolArray  = _HTTPMethodHeader[2].Split(_SlashSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
            this.ProtocolName   = _ProtocolArray[0].ToUpper();

            if (!String.Equals(ProtocolName, "HTTP", StringComparison.CurrentCultureIgnoreCase))
                throw new Exception("Bad request");

            if (HTTPVersion.TryParse(_ProtocolArray[1], out HTTPVersion _HTTPVersion))
                this.ProtocolVersion  = _HTTPVersion;

            if (ProtocolVersion != HTTPVersion.HTTP_1_0 && ProtocolVersion != HTTPVersion.HTTP_1_1)
                throw new Exception("HTTP version not supported");

            #endregion

            #region Check Host header

            // rfc 2616 - Section 19.6.1.1
            // A client that sends an HTTP/1.1 request MUST send a Host header.

            // rfc 2616 - Section 14.23
            // All Internet-based HTTP/1.1 servers MUST respond with a 400 (Bad Request)
            // status code to any HTTP/1.1 request message which lacks a Host header field.

            // rfc 2616 - Section 5.2 The Resource Identified by a Request
            // 1. If Request-URI is an absoluteURI, the host is part of the Request-URI.
            //    Any Host header field value in the request MUST be ignored.
            // 2. If the Request-URI is not an absoluteURI, and the request includes a
            //    Host header field, the host is determined by the Host header field value.
            // 3. If the host as determined by rule 1 or 2 is not a valid host on the server,
            //    the response MUST be a 400 (Bad Request) error message. (Not valid for proxies?!)
            if (!_HeaderFields.ContainsKey(HTTPHeaderField.Host.Name))
                throw new Exception("The HTTP PDU does not have a HOST header!");

            // rfc 2616 - 3.2.2
            // If the port is empty or not given, port 80 is assumed.
            var    HostHeader  = _HeaderFields[HTTPHeaderField.Host.Name].ToString().
                                     Replace(":*", "").
                                     Split(_ColonSeparator, StringSplitOptions.RemoveEmptyEntries).
                                     Select(v => v.Trim()).
                                     ToArray();

            UInt16 HostPort    = 80;

            if (HostHeader.Length == 1)
                _HeaderFields[HTTPHeaderField.Host.Name] = _HeaderFields[HTTPHeaderField.Host.Name].ToString();// + ":80"; ":80" will cause side effects!

            else if ((HostHeader.Length == 2 && !UInt16.TryParse(HostHeader[1], out HostPort)) || HostHeader.Length > 2)
                throw new Exception("Bad request");

            #endregion

        }

        #endregion

        #region (internal) HTTPRequest(Request)

        /// <summary>
        /// Create a new HTTP request based on the given HTTP request.
        /// (e.g. upgrade a HTTPRequest to a HTTPRequest&lt;TContent&gt;)
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        internal HTTPRequest(HTTPRequest Request)

            : base(Request)

        { }

        #endregion

        #endregion


        #region (static) TryParse(Text,        out Request, Timestamp = null, HTTPSource = null, LocalSocket = null, HTTPServer = null, ...)

        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        public static Boolean TryParse(String            Text,
                                       out HTTPRequest   Request,

                                       DateTime?            Timestamp           = null,
                                       HTTPSource?          HTTPSource          = null,
                                       IPSocket?            LocalSocket         = null,
                                       HTTPServer           HTTPServer          = null,

                                       CancellationToken?   CancellationToken   = null,
                                       EventTracking_Id     EventTrackingId     = null)
        {

            if (Text.IsNeitherNullNorEmpty())
            {

                try
                {

                    String Header       = null;
                    Byte[] Body         = null;
                    var    EndOfHeader  = Text.IndexOf("\r\n\r\n");

                    if (EndOfHeader == -1)
                        Header  = Text;

                    else
                    {

                        Header  = Text.Substring(0, EndOfHeader + 2);

                        if (EndOfHeader + 4 < Text.Length)
                            Body  = Text.Substring(EndOfHeader + 4).ToUTF8Bytes();

                    }


                    Request = new HTTPRequest(Timestamp   ?? DateTime.UtcNow,
                                              HTTPSource  ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                              LocalSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                              HTTPServer,

                                              Header,
                                              Body,

                                              CancellationToken:  CancellationToken,
                                              EventTrackingId:    EventTrackingId);

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request: " + e.Message);
                }

            }

            Request = null;
            return false;

        }

        #endregion

        #region (static) TryParse(Text,  Body, out Request, Timestamp = null, HTTPSource = null, LocalSocket = null, HTTPServer = null, ...)

        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP request.</param>
        /// <param name="Body">The body of the HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        public static Boolean TryParse(String               Text,
                                       Byte[]               Body,
                                       out HTTPRequest      Request,

                                       DateTime?            Timestamp           = null,
                                       HTTPSource?          HTTPSource          = null,
                                       IPSocket?            LocalSocket         = null,
                                       HTTPServer           HTTPServer          = null,

                                       CancellationToken?   CancellationToken   = null,
                                       EventTracking_Id     EventTrackingId     = null)
        {

            if (Text.IsNeitherNullNorEmpty())
            {

                try
                {

                    var EndOfHeader = Text.IndexOf("\r\n\r\n");

                    Request = new HTTPRequest(Timestamp   ?? DateTime.UtcNow,
                                              HTTPSource  ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                              LocalSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                              HTTPServer,

                                              EndOfHeader == -1 ? Text : Text.Substring(0, EndOfHeader + 2),
                                              Body,

                                              CancellationToken:  CancellationToken,
                                              EventTrackingId:    EventTrackingId);

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request: " + e.Message);
                }

            }

            Request = null;
            return false;

        }


        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP request.</param>
        /// <param name="Body">The body of the HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        public static Boolean TryParse(String               Text,
                                       Stream               Body,
                                       out HTTPRequest      Request,

                                       DateTime?            Timestamp           = null,
                                       HTTPSource?          HTTPSource          = null,
                                       IPSocket?            LocalSocket         = null,
                                       HTTPServer           HTTPServer          = null,

                                       CancellationToken?   CancellationToken   = null,
                                       EventTracking_Id     EventTrackingId     = null)
        {

            if (Text.IsNeitherNullNorEmpty())
            {

                try
                {

                    var EndOfHeader = Text.IndexOf("\r\n\r\n");

                    Request = new HTTPRequest(Timestamp   ?? DateTime.UtcNow,
                                              HTTPSource  ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                              LocalSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                              HTTPServer,

                                              EndOfHeader == -1 ? Text : Text.Substring(0, EndOfHeader + 2),
                                              null,
                                              Body,

                                              EventTrackingId:  EventTrackingId);

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request: " + e.Message);
                }

            }

            Request = null;
            return false;

        }

        #endregion

        #region (static) TryParse(Lines,       out Request, Timestamp = null, HTTPSource = null, LocalSocket = null, HTTPServer = null, ...)

        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Lines">The lines of the text representation of a HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCp socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        public static Boolean TryParse(IEnumerable<String>  Lines,
                                       out HTTPRequest      Request,

                                       DateTime?            Timestamp           = null,
                                       HTTPSource?          HTTPSource          = null,
                                       IPSocket?            LocalSocket         = null,
                                       HTTPServer           HTTPServer          = null,

                                       CancellationToken?   CancellationToken   = null,
                                       EventTracking_Id     EventTrackingId     = null)
        {

            if (Lines.SafeAny())
            {
                try
                {

                    Request = new HTTPRequest(Timestamp   ?? DateTime.UtcNow,
                                              HTTPSource  ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                              LocalSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                              HTTPServer,

                                              Lines.TakeWhile(line => line != "").        AggregateWith("\r\n"),
                                              Lines.SkipWhile(line => line != "").Skip(1).AggregateWith("\r\n").ToUTF8Bytes(),

                                              CancellationToken:  CancellationToken,
                                              EventTrackingId:    EventTrackingId);

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request lines: " + e.Message);
                }
            }

            Request = null;
            return false;

        }

        #endregion

        #region (static) TryParse(Lines, Body, out Request, Timestamp = null, HTTPSource = null, LocalSocket = null, HTTPServer = null, ...)

        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Lines">The lines of the text representation of a HTTP request.</param>
        /// <param name="Body">The body of the HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCp socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        public static Boolean TryParse(IEnumerable<String>  Lines,
                                       Byte[]               Body,
                                       out HTTPRequest      Request,

                                       DateTime?            Timestamp           = null,
                                       HTTPSource?          HTTPSource          = null,
                                       IPSocket?            LocalSocket         = null,
                                       HTTPServer           HTTPServer          = null,

                                       CancellationToken?   CancellationToken   = null,
                                       EventTracking_Id     EventTrackingId     = null)
        {

            if (Lines.SafeAny())
            {
                try
                {

                    Request = new HTTPRequest(Timestamp   ?? DateTime.UtcNow,
                                              HTTPSource  ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                              LocalSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                              HTTPServer,

                                              Lines.TakeWhile(line => line != "").AggregateWith("\r\n"),
                                              Body,

                                              CancellationToken:  CancellationToken,
                                              EventTrackingId:    EventTrackingId);

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request lines: " + e.Message);
                }
            }

            Request = null;
            return false;

        }


        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Lines">The lines of the text representation of a HTTP request.</param>
        /// <param name="Body">The body of the HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCp socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        public static Boolean TryParse(IEnumerable<String>  Lines,
                                       Stream               Body,
                                       out HTTPRequest      Request,

                                       DateTime?            Timestamp        = null,
                                       HTTPSource?          HTTPSource       = null,
                                       IPSocket?            LocalSocket      = null,
                                       HTTPServer           HTTPServer       = null,
                                       EventTracking_Id     EventTrackingId  = null)
        {

            if (Lines.SafeAny())
            {
                try
                {

                    Request = new HTTPRequest(Timestamp   ?? DateTime.UtcNow,
                                              HTTPSource  ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                              LocalSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                              HTTPServer,

                                              Lines.TakeWhile(line => line != "").AggregateWith("\r\n"),
                                              null,
                                              Body,

                                              EventTrackingId:  EventTrackingId);

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request lines: " + e.Message);
                }
            }

            Request = null;
            return false;

        }

        #endregion


        #region ConvertContent<TResult>(ContentConverter, OnException = null)

        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPRequest<TResult> ConvertContent<TResult>(Func<String, OnExceptionDelegate, TResult>  ContentConverter,
                                                            OnExceptionDelegate                         OnException  = null)
        {

            if (ContentConverter == null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPRequest<TResult>(this,
                                            ContentConverter(HTTPBodyAsUTF8String,
                                                             OnException));

        }


        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPRequest<TResult> ConvertContent<TResult>(Func<Byte[], OnExceptionDelegate, TResult>  ContentConverter,
                                                            OnExceptionDelegate                         OnException  = null)
        {

            if (ContentConverter == null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPRequest<TResult>(this,
                                            ContentConverter(HTTPBody,
                                                             OnException));

        }


        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPRequest<TResult> ConvertContent<TResult>(Func<Stream, OnExceptionDelegate, TResult>  ContentConverter,
                                                            OnExceptionDelegate                         OnException  = null)
        {

            if (ContentConverter == null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPRequest<TResult>(this,
                                            ContentConverter(HTTPBodyStream,
                                                             OnException));

        }

        #endregion


        #region (static) LoadHTTPRequestLogfiles_old(FilePath, FilePattern, FromTimestamp = null, ToTimestamp = null)

        public static IEnumerable<HTTPRequest> LoadHTTPRequestLogfiles_old(String     FilePath,
                                                                           String     FilePattern,
                                                                           DateTime?  FromTimestamp  = null,
                                                                           DateTime?  ToTimestamp    = null)
        {

            var _requests  = new ConcurrentBag<HTTPRequest>();

            Parallel.ForEach(Directory.EnumerateFiles(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + FilePath,
                                                      FilePattern,
                                                      SearchOption.TopDirectoryOnly).
                                       OrderByDescending(file => file),
                             new ParallelOptions() { MaxDegreeOfParallelism = 1 },
                             file => {

                var _request            = new List<String>();
                var copy                = "none";
                var relativelinenumber  = 0;
                var RequestTimestamp    = DateTime.Now;

                foreach (var line in File.ReadLines(file))
                {

                    try
                    {

                        if      (relativelinenumber == 1 && copy == "request")
                            RequestTimestamp  = DateTime.SpecifyKind(DateTime.Parse(line), DateTimeKind.Utc);

                        else if (line == ">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------")
                        {
                            copy = "request";
                            relativelinenumber = 0;
                        }

                        else if (line == "--------------------------------------------------------------------------------")
                        {

                            if ((FromTimestamp == null || RequestTimestamp >= FromTimestamp.Value) &&
                                (  ToTimestamp == null || RequestTimestamp <    ToTimestamp.Value))
                            {

                                if (TryParse(_request,
                                             out HTTPRequest  parsedHTTPRequest,
                                             Timestamp:       RequestTimestamp))
                                {
                                    _requests.Add(parsedHTTPRequest);
                                }

                                else
                                    DebugX.LogT("Could not parse reloaded HTTP request!");

                            }

                            copy      = "none";
                            _request  = new List<String>();

                        }

                        else if (copy == "request")
                            _request.Add(line);

                        relativelinenumber++;

                    }
                    catch (Exception e)
                    {
                        DebugX.LogT("Could not parse reloaded HTTP request: " + e.Message);
                    }

                }

            });

            return _requests.OrderBy(request => request.Timestamp);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
            => EntirePDU;

        #endregion


        #region (class) Builder

        /// <summary>
        /// A read-write HTTP request header.
        /// </summary>
        public class Builder : AHTTPPDUBuilder
        {

            #region Properties

            private readonly HTTPClient _HTTPClient;

            #region Non-http header fields

            /// <summary>
            /// The related HTTP server.
            /// </summary>
            public HTTPServer HTTPServer { get; set; }

            #region EntireRequestHeader

            public String EntireRequestHeader
                => HTTPRequestLine + Environment.NewLine + ConstructedHTTPHeader;

            #endregion

            #region HTTPRequestLine

            public String HTTPRequestLine
                => String.Concat(HTTPMethod, " ", _URI, QueryString, " ", ProtocolName, "/", ProtocolVersion);

            #endregion

            #region HTTPMethod

            private HTTPMethod _HTTPMethod;

            /// <summary>
            /// The http method.
            /// </summary>
            public HTTPMethod HTTPMethod
            {

                get
                {
                    return _HTTPMethod;
                }

                set
                {
                    SetProperty(ref _HTTPMethod, value, "HTTPMethod");
                }

            }

            #endregion

            #region FakeURIPrefix

            public String FakeURIPrefix { get; set; }

            #endregion

            #region URI

            private HTTPPath _URI;

            /// <summary>
            /// The minimal URL (this means e.g. without the query string).
            /// </summary>
            public HTTPPath URI
            {

                get
                {
                    return _URI;
                }

                set
                {
                    SetProperty(ref _URI, value, "URI");
                }

            }

            #endregion

            /// <summary>
            /// The HTTP query string.
            /// </summary>
            public QueryString QueryString { get; }

            #endregion

            #region Request header fields

            #region Accept

            /// <summary>
            /// The http content types accepted by the client.
            /// </summary>
            public AcceptTypes Accept
            {
                get
                {
                    return GetHeaderField<AcceptTypes>(HTTPHeaderField.Accept);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.AcceptCharset, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.AcceptEncoding, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.AcceptLanguage, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.AcceptRanges, value);
                }

            }

            #endregion

            #region Authorization

            public IHTTPAuthentication Authorization
            {

                get
                {

                    var aa = GetHeaderField<String>(HTTPHeaderField.Authorization);

                    if (HTTPBasicAuthentication. TryParse(aa, out HTTPBasicAuthentication  BasicAuth))
                        return BasicAuth;

                    if (HTTPBearerAuthentication.TryParse(aa, out HTTPBearerAuthentication BearerAuth))
                        return BearerAuth;

                    return null;

                }

                set
                {
                    SetHeaderField(HTTPHeaderField.Authorization, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Depth, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Destination, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Expect, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.From, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Host, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.If, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.IfMatch, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.IfModifiedSince, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.IfNoneMatch, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.IfRange, value);
                }

            }

            #endregion

            #region If-Unmodified-Since

            public String IfUnmodifiedSince
            {

                get
                {
                    return GetHeaderField(HTTPHeaderField.IfModifiedSince);
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.IfUnmodifiedSince, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.LockToken, value);
                }

            }

            #endregion

            #region Max-Forwards

            public UInt64? MaxForwards
            {

                get
                {
                    return GetHeaderField_UInt64(HTTPHeaderField.MaxForwards);
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.MaxForwards, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Overwrite, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.ProxyAuthorization, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Range, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Referer, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.TE, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Timeout, value);
                }

            }

            #endregion

            #region User-Agent

            public String UserAgent
            {

                get
                {
                    return GetHeaderField(HTTPHeaderField.ContentLocation);
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.UserAgent, value);
                }

            }

            #endregion

            #region LastEventId

            public UInt64? LastEventId
            {
                
                get
                {
                    return GetHeaderField_UInt64(HTTPHeaderField.LastEventId);
                }

                set
                {
                    if (value != null && value.HasValue)
                        SetHeaderField("Last-Event-Id", value.Value);
                    else
                        throw new Exception("Could not set the HTTP request header 'Last-Event-Id' field!");
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Cookie, value);
                }

            }

            #endregion

            #region API_Key

            public String API_Key
            {

                get
                {
                    return GetHeaderField(HTTPHeaderField.API_Key);
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.API_Key, value);
                }

            }

            #endregion

            #endregion

            #endregion

            #region Constructor(s)

            #region HTTPRequestBuilder()

            /// <summary>
            /// Create a new HTTP request.
            /// </summary>
            public Builder(HTTPClient Client)
            {

                this._HTTPClient      = Client;

                this.HTTPStatusCode   = HTTPStatusCode.OK;
                this.HTTPMethod       = HTTPMethod.GET;
                this.URI              = HTTPPath.Parse("/");
                this.QueryString      = QueryString.New;
                SetHeaderField(HTTPHeaderField.Accept, new AcceptTypes());
                this.ProtocolName     = "HTTP";
                this.ProtocolVersion  = new HTTPVersion(1, 1);

            }

            #endregion

            #region HTTPRequestBuilder(Request)

            /// <summary>
            /// Create a new HTTP request.
            /// </summary>
            public Builder(HTTPRequest Request)
            {

             //   this.HTTPStatusCode   = OtherHTTPRequest.HTTPStatusCode;
                this.HTTPServer       = Request.HTTPServer;
                this.HTTPMethod       = Request.HTTPMethod;
                this.URI              = Request.URI;
                this.QueryString      = Request.QueryString;
                SetHeaderField(HTTPHeaderField.Accept, new AcceptTypes(Request.Accept.ToArray()));
                this.ProtocolName     = Request.ProtocolName;
                this.ProtocolVersion  = Request.ProtocolVersion;

                this.Content          = Request.HTTPBody;
                this.ContentStream    = Request.HTTPBodyStream;

                foreach (var kvp in Request)
                    Set(kvp.Key, kvp.Value);

            }

            #endregion

            #endregion


            #region SetURI(URI)

            /// <summary>
            /// Set the HTTP method.
            /// </summary>
            /// <param name="URI">The new URI.</param>
            public Builder SetURI(HTTPPath URI)
            {
                this.URI = URI;
                return this;
            }

            #endregion

            #region Set non-http header fields

            #region SetHTTPStatusCode(HTTPStatusCode)

            /// <summary>
            /// Set the HTTP status code.
            /// </summary>
            /// <param name="HTTPStatusCode">A HTTP status code.</param>
            public Builder SetHTTPStatusCode(HTTPStatusCode HTTPStatusCode)
            {
                this.HTTPStatusCode = HTTPStatusCode;
                return this;
            }

            #endregion

            #region SetHTTPMethod(HTTPMethod)

            /// <summary>
            /// Set the HTTP method.
            /// </summary>
            /// <param name="HTTPMethod">The HTTPMethod.</param>
            public Builder SetHTTPMethod(HTTPMethod HTTPMethod)
            {
                this.HTTPMethod = HTTPMethod;
                return this;
            }

            #endregion

            #region SetProtocolName(ProtocolName)

            /// <summary>
            /// Set the protocol name.
            /// </summary>
            /// <param name="ProtocolName">The protocol name.</param>
            public Builder SetProtocolName(String ProtocolName)
            {
                this.ProtocolName = ProtocolName;
                return this;
            }

            #endregion

            #region SetProtocolVersion(ProtocolVersion)

            /// <summary>
            /// Set the protocol version.
            /// </summary>
            /// <param name="ProtocolVersion">The protocol version.</param>
            public Builder SetProtocolVersion(HTTPVersion ProtocolVersion)
            {
                this.ProtocolVersion = ProtocolVersion;
                return this;
            }

            #endregion

            #region SetContent(...)

            #region SetContent(ByteArray)

            /// <summary>
            /// The HTTP content/body.
            /// </summary>
            /// <param name="ByteArray">The HTTP content/body.</param>
            public Builder SetContent(Byte[] ByteArray)
            {
                this.Content = ByteArray;
                return this;
            }

            #endregion

            #region SetContent(String)

            /// <summary>
            /// The HTTP content/body.
            /// </summary>
            /// <param name="String">The HTTP content/body.</param>
            public Builder SetContent(String String)
            {
                this.Content = String.ToUTF8Bytes();
                return this;
            }

            #endregion

            #endregion

            #region SetContentStream(ContentStream)

            /// <summary>
            /// The HTTP content/body as a stream.
            /// </summary>
            /// <param name="ContentStream">The HTTP content/body as a stream.</param>
            public Builder SetContent(Stream ContentStream)
            {
                this.ContentStream = ContentStream;
                return this;
            }

            #endregion

            #endregion

            #region Set general  header fields

            #region SetCacheControl(CacheControl)

            /// <summary>
            /// Set the HTTP CacheControl header field.
            /// </summary>
            /// <param name="CacheControl">CacheControl.</param>
            public Builder SetCacheControl(String CacheControl)
            {
                this.CacheControl = CacheControl;
                return this;
            }

            #endregion

            #region SetConnection(Connection)

            /// <summary>
            /// Set the HTTP connection header field.
            /// </summary>
            /// <param name="Connection">A connection.</param>
            public Builder SetConnection(String Connection)
            {
                this.Connection = Connection;
                return this;
            }

            #endregion

            #region SetContentEncoding(ContentEncoding)

            /// <summary>
            /// Set the HTTP Content-Encoding header field.
            /// </summary>
            /// <param name="ContentEncoding">The encoding of the HTTP content/body.</param>
            public Builder SetContentEncoding(Encoding ContentEncoding)
            {
                this.ContentEncoding = ContentEncoding;
                return this;
            }

            #endregion

            #region SetContentLanguage(ContentLanguages)

            /// <summary>
            /// Set the HTTP Content-Languages header field.
            /// </summary>
            /// <param name="ContentLanguages">The languages of the HTTP content/body.</param>
            public Builder SetContentLanguage(List<String> ContentLanguages)
            {
                this.ContentLanguage = ContentLanguages;
                return this;
            }

            #endregion

            #region SetContentLength(ContentLength)

            /// <summary>
            /// Set the HTTP Content-Length.
            /// </summary>
            /// <param name="ContentLength">The length of the HTTP content/body.</param>
            public Builder SetContentLength(UInt64? ContentLength)
            {
                this.ContentLength = ContentLength;
                return this;
            }

            #endregion

            #region SetContentLocation(ContentLocation)

            /// <summary>
            /// Set the HTTP ContentLocation header field.
            /// </summary>
            /// <param name="ContentLocation">ContentLocation.</param>
            public Builder SetContentLocation(String ContentLocation)
            {
                this.ContentLocation = ContentLocation;
                return this;
            }

            #endregion

            #region SetContentMD5(ContentMD5)

            /// <summary>
            /// Set the HTTP ContentMD5 header field.
            /// </summary>
            /// <param name="ContentMD5">ContentMD5.</param>
            public Builder SetContentMD5(String ContentMD5)
            {
                this.ContentMD5 = ContentMD5;
                return this;
            }

            #endregion

            #region SetContentRange(ContentRange)

            /// <summary>
            /// Set the HTTP ContentRange header field.
            /// </summary>
            /// <param name="ContentRange">ContentRange.</param>
            public Builder SetContentRange(String ContentRange)
            {
                this.ContentRange = ContentRange;
                return this;
            }

            #endregion

            #region SetContentType(ContentType)

            /// <summary>
            /// Set the HTTP Content-Type header field.
            /// </summary>
            /// <param name="ContentType">The type of the HTTP content/body.</param>
            public Builder SetContentType(HTTPContentType ContentType)
            {
                this.ContentType = ContentType;
                return this;
            }

            #endregion

            #region SetDate(Date)

            /// <summary>
            /// Set the HTTP Date header field.
            /// </summary>
            /// <param name="Date">DateTime.</param>
            public Builder SetDate(DateTime Date)
            {
                this.Date = Date;
                return this;
            }

            #endregion

            #region SetVia(Via)

            /// <summary>
            /// Set the HTTP Via header field.
            /// </summary>
            /// <param name="Via">Via.</param>
            public Builder SetVia(String Via)
            {
                this.Via = Via;
                return this;
            }

            #endregion

            #endregion

            #region Set request  header fields

            #region AddAccept(AcceptType)

            /// <summary>
            /// Add an AcceptType to the Accept header field.
            /// </summary>
            /// <param name="AcceptType">An AcceptType.</param>
            public Builder AddAccept(AcceptType AcceptType)
            {
                this.Accept.Add(AcceptType);
                return this;
            }

            #endregion

            #region AddAccept(HTTPContentType, Quality = 1)

            /// <summary>
            /// Add a HTTPContentType and its quality to the Accept header field.
            /// </summary>
            /// <param name="HTTPContentType">A HTTPContentType.</param>
            /// <param name="Quality">The quality of the HTTPContentType.</param>
            public Builder AddAccept(HTTPContentType HTTPContentType, Double Quality = 1)
            {
                this.Accept.Add(HTTPContentType, Quality);
                return this;
            }

            #endregion

            #region SetAcceptCharset(AcceptCharset)

            /// <summary>
            /// Set the HTTP Accept-Charset header field.
            /// </summary>
            /// <param name="AcceptCharset">AcceptCharset.</param>
            public Builder SetAcceptCharset(String AcceptCharset)
            {
                this.AcceptCharset = AcceptCharset;
                return this;
            }

            #endregion

            #region SetAcceptEncoding(AcceptEncoding)

            /// <summary>
            /// Set the HTTP Accept-Encoding header field.
            /// </summary>
            /// <param name="AcceptEncoding">AcceptEncoding.</param>
            public Builder SetAcceptEncoding(String AcceptEncoding)
            {
                this.AcceptEncoding = AcceptEncoding;
                return this;
            }

            #endregion

            #region SetAcceptLanguage(AcceptLanguage)

            /// <summary>
            /// Set the HTTP Accept-Language header field.
            /// </summary>
            /// <param name="AcceptLanguage">AcceptLanguage.</param>
            public Builder SetAcceptLanguage(String AcceptLanguage)
            {
                this.AcceptLanguage = AcceptLanguage;
                return this;
            }

            #endregion

            #region SetAcceptRanges(AcceptRanges)

            /// <summary>
            /// Set the HTTP Accept-Language header field.
            /// </summary>
            /// <param name="AcceptRanges">AcceptRanges.</param>
            public Builder SetAcceptRanges(String AcceptRanges)
            {
                this.AcceptRanges = AcceptRanges;
                return this;
            }

            #endregion

            #region SetAuthorization(Authorization)

            /// <summary>
            /// Set the HTTP Authorization header field.
            /// </summary>
            /// <param name="Authorization">Authorization.</param>
            public Builder SetAuthorization(HTTPBasicAuthentication Authorization)
            {
                this.Authorization = Authorization;
                return this;
            }

            #endregion

            #region SetDepth(Depth)

            /// <summary>
            /// Set the HTTP Depth header field.
            /// </summary>
            /// <param name="Depth">Depth.</param>
            public Builder SetDepth(String Depth)
            {
                this.Depth = Depth;
                return this;
            }

            #endregion

            #region SetDepth(Expect)

            /// <summary>
            /// Set the HTTP Expect header field.
            /// </summary>
            /// <param name="Expect">Expect.</param>
            public Builder SetExpect(String Expect)
            {
                this.Expect = Expect;
                return this;
            }

            #endregion

            #region SetDepth(From)

            /// <summary>
            /// Set the HTTP From header field.
            /// </summary>
            /// <param name="From">From.</param>
            public Builder SetFrom(String From)
            {
                this.From = From;
                return this;
            }

            #endregion

            #region SetHost(Host)

            /// <summary>
            /// Set the HTTP Host header field.
            /// </summary>
            /// <param name="Host">Host.</param>
            public Builder SetHost(HTTPHostname Host)
            {
                this.Host = Host;
                return this;
            }

            #endregion

            #region SetIf(If)

            /// <summary>
            /// Set the HTTP If header field.
            /// </summary>
            /// <param name="If">If.</param>
            public Builder SetIf(String If)
            {
                this.If = If;
                return this;
            }

            #endregion

            #region SetIfMatch(IfMatch)

            /// <summary>
            /// Set the HTTP If-Match header field.
            /// </summary>
            /// <param name="IfMatch">IfMatch.</param>
            public Builder SetIfMatch(String IfMatch)
            {
                this.IfMatch = IfMatch;
                return this;
            }

            #endregion

            #region SetIfModifiedSince(IfModifiedSince)

            /// <summary>
            /// Set the HTTP If-Modified-Since header field.
            /// </summary>
            /// <param name="IfModifiedSince">IfModifiedSince.</param>
            public Builder SetIfModifiedSince(String IfModifiedSince)
            {
                this.IfModifiedSince = IfModifiedSince;
                return this;
            }

            #endregion

            #region SetIfNoneMatch(IfNoneMatch)

            /// <summary>
            /// Set the HTTP If-None-Match header field.
            /// </summary>
            /// <param name="IfNoneMatch">IfNoneMatch.</param>
            public Builder SetIfNoneMatch(String IfNoneMatch)
            {
                this.IfNoneMatch = IfNoneMatch;
                return this;
            }

            #endregion

            #region SetIfRange(IfRange)

            /// <summary>
            /// Set the HTTP If-Range header field.
            /// </summary>
            /// <param name="IfRange">IfRange.</param>
            public Builder SetIfRange(String IfRange)
            {
                this.IfRange = IfRange;
                return this;
            }

            #endregion

            #region SetIfUnmodifiedSince(IfUnmodifiedSince)

            /// <summary>
            /// Set the HTTP If-Unmodified-Since header field.
            /// </summary>
            /// <param name="IfUnmodifiedSince">IfUnmodifiedSince.</param>
            public Builder SetIfUnmodifiedSince(String IfUnmodifiedSince)
            {
                this.IfUnmodifiedSince = IfUnmodifiedSince;
                return this;
            }

            #endregion

            #region SetLockToken(LockToken)

            /// <summary>
            /// Set the HTTP LockToken header field.
            /// </summary>
            /// <param name="LockToken">LockToken.</param>
            public Builder SetLockToken(String LockToken)
            {
                this.LockToken = LockToken;
                return this;
            }

            #endregion

            #region SetMaxForwards(MaxForwards)

            /// <summary>
            /// Set the HTTP Max-Forwards header field.
            /// </summary>
            /// <param name="MaxForwards">MaxForwards.</param>
            public Builder SetMaxForwards(UInt64? MaxForwards)
            {
                this.MaxForwards = MaxForwards;
                return this;
            }

            #endregion

            #region SetOverwrite(Overwrite)

            /// <summary>
            /// Set the HTTP Overwrite header field.
            /// </summary>
            /// <param name="Overwrite">Overwrite.</param>
            public Builder SetOverwrite(String Overwrite)
            {
                this.Overwrite = Overwrite;
                return this;
            }

            #endregion

            #region SetProxyAuthorization(ProxyAuthorization)

            /// <summary>
            /// Set the HTTP Proxy-Authorization header field.
            /// </summary>
            /// <param name="ProxyAuthorization">ProxyAuthorization.</param>
            public Builder SetProxyAuthorization(String ProxyAuthorization)
            {
                this.ProxyAuthorization = ProxyAuthorization;
                return this;
            }

            #endregion

            #region SetRange(Range)

            /// <summary>
            /// Set the HTTP Range header field.
            /// </summary>
            /// <param name="Range">Range.</param>
            public Builder SetRange(String Range)
            {
                this.Range = Range;
                return this;
            }

            #endregion

            #region SetReferer(Referer)

            /// <summary>
            /// Set the HTTP Referer header field.
            /// </summary>
            /// <param name="Referer">Referer.</param>
            public Builder SetReferer(String Referer)
            {
                this.Referer = Referer;
                return this;
            }

            #endregion

            #region SetTE(TE)

            /// <summary>
            /// Set the HTTP TE header field.
            /// </summary>
            /// <param name="TE">TE.</param>
            public Builder SetTE(String TE)
            {
                this.TE = TE;
                return this;
            }

            #endregion

            #region SetTimeout(Timeout)

            /// <summary>
            /// Set the HTTP Timeout header field.
            /// </summary>
            /// <param name="Timeout">Timeout.</param>
            public Builder SetTimeout(UInt64? Timeout)
            {
                this.Timeout = Timeout;
                return this;
            }

            #endregion

            #region SetUserAgent(UserAgent)

            /// <summary>
            /// Set the HTTP User-Agent header field.
            /// </summary>
            /// <param name="UserAgent">UserAgent.</param>
            public Builder SetUserAgent(String UserAgent)
            {
                this.UserAgent = UserAgent;
                return this;
            }

            #endregion

            #region SetLastEventId(LastEventId)

            /// <summary>
            /// Set the HTTP Last-Event-Id header field.
            /// </summary>
            /// <param name="LastEventId">LastEventId.</param>
            public Builder SetLastEventId(UInt64? LastEventId)
            {
                this.LastEventId = LastEventId;
                return this;
            }

            #endregion

            #region SetCookie(Cookie)

            /// <summary>
            /// Set the HTTP Cookie header field.
            /// </summary>
            /// <param name="Cookie">Cookie.</param>
            public Builder SetCookie(String Cookie)
            {
                this.Cookie = Cookie;
                return this;
            }

            #endregion

            #endregion


            #region Set(Field, Value)

            public void Set(String Field, Object Value)
            {
                SetHeaderField(new HTTPHeaderField(Field, typeof(Object), HeaderFieldType.Request, RequestPathSemantic.both), Value);
            }

            #endregion


            public Task<HTTPResponse> ExecuteReturnResult()
                => _HTTPClient?.Execute(AsImmutable);


            #region PrepareImmutability()

            /// <summary>
            /// Prepares the immutability of an HTTP PDU, e.g. calculates
            /// and set the Content-Length header.
            /// </summary>
            protected override void PrepareImmutability()
            {
                base.PrepareImmutability();
            }

            #endregion


            #region (operator) HTTPRequestBuilder => HTTPRequest

            /// <summary>
            /// An implicit conversion from a HTTP request builder into a HTTP request.
            /// </summary>
            /// <param name="Builder">An HTTP request builder.</param>
            public static implicit operator HTTPRequest(Builder Builder)
                => Builder.AsImmutable;

            #endregion

            #region AsImmutable

            /// <summary>
            /// Converts this HTTPRequestBuilder into an immutable HTTPRequest.
            /// </summary>
            public HTTPRequest AsImmutable
            {
                get
                {

                    PrepareImmutability();

                    return new HTTPRequest(DateTime.UtcNow,
                                           new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                           IPSocket.LocalhostV4(IPPort.HTTPS),
                                           HTTPServer,

                                           EntireRequestHeader,
                                           Content) {

                        FakeURIPrefix = FakeURIPrefix

                    };

                }
            }

            #endregion

        }

        #endregion

    }


    #region HTTPRequest<TContent>

    /// <summary>
    /// A generic HTTP request.
    /// </summary>
    /// <typeparam name="TContent">The type of the HTTP body data.</typeparam>
    public class HTTPRequest<TContent> : HTTPRequest
    {

        #region Data

        private readonly Boolean _IsFault;

        #endregion

        #region Properties

        /// <summary>
        /// The parsed content.
        /// </summary>
        public TContent   Content    { get; }

        /// <summary>
        /// An exception during parsing.
        /// </summary>
        public Exception  Exception  { get; }

        /// <summary>
        /// An error during parsing.
        /// </summary>
        public Boolean    HasErrors
            => Exception != null && !_IsFault;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new generic HTTP request.
        /// </summary>
        /// <param name="Request">The non-generic HTTP request.</param>
        /// <param name="Content">The generic HTTP body data.</param>
        /// <param name="IsFault">Whether there is an error.</param>
        /// <param name="Exception">An optional exception.</param>
        public HTTPRequest(HTTPRequest  Request,
                           TContent     Content,
                           Boolean      IsFault    = false,
                           Exception    Exception  = null)

            : base(Request)

        {

            this.Content    = Content;
            this._IsFault   = IsFault;
            this.Exception  = Exception;

        }

        #endregion


        #region ConvertContent<TResult>(ContentConverter, OnException = null)

        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPRequest<TResult> ConvertContent<TResult>(Func<TContent, OnExceptionDelegate, TResult>  ContentConverter,
                                                            OnExceptionDelegate                           OnException  = null)
        {

            if (ContentConverter == null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPRequest<TResult>(this,
                                            ContentConverter(Content,
                                                             OnException));

        }

        #endregion


    }

    #endregion

}
