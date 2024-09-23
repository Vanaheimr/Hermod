/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Text;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP request.
    /// </summary>
    public partial class HTTPRequest : AHTTPPDU
    {

        /// <summary>
        /// A read-write HTTP request header.
        /// </summary>
        public class Builder : AHTTPPDUBuilder
        {

            #region Properties

            private readonly AHTTPClient? httpClient;

            #region Non-http header fields

            /// <summary>
            /// The related HTTP server.
            /// </summary>
            public HTTPServer?  HTTPServer      { get; set; }

            #region EntireRequestHeader

            public String EntireRequestHeader

                => $"{HTTPRequestLine}{Environment.NewLine}{ConstructedHTTPHeader}";

            #endregion

            #region HTTPRequestLine

            public String HTTPRequestLine

                => $"{HTTPMethod} {FakeURLPrefix}{Path}{QueryString} {ProtocolName}/{ProtocolVersion}";

            #endregion

            /// <summary>
            /// The http method.
            /// </summary>
            public HTTPMethod   HTTPMethod      { get; set; }

            /// <summary>
            /// Fake URL prefix.
            /// </summary>
            public String?      FakeURLPrefix   { get; set; }

            /// <summary>
            /// The minimal path (this means e.g. without the query string).
            /// </summary>
            public HTTPPath     Path            { get; set; }

            /// <summary>
            /// The HTTP query string.
            /// </summary>
            public QueryString  QueryString     { get; internal set; }

            #endregion

            #region Request header fields

            #region Accept

            /// <summary>
            /// The http content types accepted by the client.
            /// </summary>
            public AcceptTypes?  Accept
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Accept);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Accept, value);
                }

            }

            #endregion

            #region Accept-Charset

            public String? AcceptCharset
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.AcceptCharset);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.AcceptCharset, value);
                }

            }

            #endregion

            #region Accept-Encoding

            public String? AcceptEncoding
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.AcceptEncoding);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.AcceptEncoding, value);
                }

            }

            #endregion

            #region Accept-Language

            public String? AcceptLanguage
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.AcceptLanguage);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.AcceptLanguage, value);
                }

            }

            #endregion

            #region Accept-Ranges

            public String? AcceptRanges
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.AcceptRanges);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.AcceptRanges, value);
                }

            }

            #endregion

            #region Authorization

            public IHTTPAuthentication? Authorization
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Authorization);
                }

                set
                {
                    if (value is not null)
                        SetHeaderField(HTTPRequestHeaderField.Authorization, value);
                }

            }

            #endregion

            #region Depth

            public String? Depth
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Depth);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Depth, value);
                }

            }

            #endregion

            #region Destination

            public String? Destination
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Destination);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Destination, value);
                }

            }

            #endregion

            #region Expect

            public String? Expect
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Expect);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Expect, value);
                }

            }

            #endregion

            #region From

            public String? From
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.From);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.From, value);
                }

            }

            #endregion

            #region Host

            public HTTPHostname Host
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Host);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Host, value);
                }

            }

            #endregion

            #region If

            public String? If
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.If);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.If, value);
                }

            }

            #endregion

            #region If-Match

            public String? IfMatch
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.IfMatch);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.IfMatch, value);
                }

            }

            #endregion

            #region If-Modified-Since

            public String? IfModifiedSince
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.IfModifiedSince);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.IfModifiedSince, value);
                }

            }

            #endregion

            #region If-None-Match

            public String? IfNoneMatch
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.IfNoneMatch);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.IfNoneMatch, value);
                }

            }

            #endregion

            #region If-Range

            public String? IfRange
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.IfRange);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.IfRange, value);
                }

            }

            #endregion

            #region If-Unmodified-Since

            public String? IfUnmodifiedSince
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.IfModifiedSince);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.IfUnmodifiedSince, value);
                }

            }

            #endregion

            #region Lock-Token

            public String? LockToken
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.LockToken);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.LockToken, value);
                }

            }

            #endregion

            #region Max-Forwards

            public UInt64? MaxForwards
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.MaxForwards);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.MaxForwards, value);
                }

            }

            #endregion

            #region Overwrite

            public String? Overwrite
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Overwrite);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Overwrite, value);
                }

            }

            #endregion

            #region Proxy-Authorization

            public String? ProxyAuthorization
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.ProxyAuthorization);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.ProxyAuthorization, value);
                }

            }

            #endregion

            #region Range

            public String? Range
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Range);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Range, value);
                }

            }

            #endregion

            #region Referer

            public String? Referer
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Referer);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Referer, value);
                }

            }

            #endregion

            #region TE

            public String? TE
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.TE);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.TE, value);
                }

            }

            #endregion

            #region Timeout

            public TimeSpan? Timeout
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Timeout);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Timeout, value);
                }

            }

            #endregion

            #region User-Agent

            public String? UserAgent
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.UserAgent);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.UserAgent, value);
                }

            }

            #endregion

            #region LastEventId

            public UInt64? LastEventId
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.LastEventId);
                }

                set
                {
                    if (value != null && value.HasValue)
                        SetHeaderField(HTTPRequestHeaderField.LastEventId, value.Value);
                    else
                        throw new Exception("Could not set the HTTP request header 'Last-Event-Id' field!");
                }

            }

            #endregion

            #region Cookie

            public HTTPCookies? Cookie
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.Cookie);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.Cookie, value);
                }

            }

            #endregion

            #region API_Key

            public APIKey_Id? API_Key
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.API_Key);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.API_Key, value);
                }

            }

            #endregion

            #region SecWebSocketKey

            public String? SecWebSocketKey
            {

                get
                {
                    return GetHeaderField(HTTPHeaderField.SecWebSocketKey);
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.SecWebSocketKey, value);
                }

            }

            #endregion

            #region X_ClientId

            public String? X_ClientId
            {

                get
                {
                    return GetHeaderField(HTTPRequestHeaderField.X_ClientId);
                }

                set
                {
                    SetHeaderField(HTTPRequestHeaderField.X_ClientId, value);
                }

            }

            #endregion

            #endregion

            #endregion

            #region Constructor(s)

            #region HTTPRequestBuilder(Client = null, CancellationToken = default)

            /// <summary>
            /// Create a new HTTP request.
            /// </summary>
            /// <param name="CancellationToken">An optional cancellation token.</param>
            public Builder(AHTTPClient?       Client              = null,
                           CancellationToken  CancellationToken   = default)

                : base(CancellationToken)

            {

                this.httpClient       = Client;

                this.HTTPStatusCode   = HTTPStatusCode.OK;
                this.HTTPMethod       = HTTPMethod.GET;
                this.Path             = HTTPPath.Parse("/");
                this.QueryString      = QueryString.Empty;
                SetHeaderField(HTTPRequestHeaderField.Accept, new AcceptTypes());
                this.ProtocolName     = "HTTP";
                this.ProtocolVersion  = new HTTPVersion(1, 1);

            }

            #endregion

            #region HTTPRequestBuilder(Request)

            /// <summary>
            /// Create a new HTTP request.
            /// </summary>
            public Builder(HTTPRequest Request)

                : base(Request.CancellationToken)

            {

                this.HTTPServer       = Request.HTTPServer;
                this.HTTPMethod       = Request.HTTPMethod;
                this.Path             = Request.Path;
                this.QueryString      = Request.QueryString;
                if (Request.Accept is not null)
                    SetHeaderField(HTTPRequestHeaderField.Accept, Request.Accept.Clone());
                this.ProtocolName     = Request.ProtocolName;
                this.ProtocolVersion  = Request.ProtocolVersion;

                this.Content          = Request.HTTPBody;
                this.ContentStream    = Request.HTTPBodyStream;

                foreach (var kvp in Request)
                    Set(kvp.Key, kvp.Value);

            }

            #endregion

            #endregion


            #region SetURL(URL)

            /// <summary>
            /// Set the HTTP URL.
            /// </summary>
            /// <param name="URL">The new URL.</param>
            public Builder SetURL(HTTPPath URL)
            {
                this.Path = URL;
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
            public Builder SetConnection(ConnectionType Connection)
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

            #region SetAuthorization(Authentication)

            /// <summary>
            /// Set the HTTP Authentication header field.
            /// </summary>
            /// <param name="Authentication">Authentication.</param>
            public Builder SetAuthorization(IHTTPAuthentication Authentication)
            {
                this.Authorization = Authentication;
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
            public Builder SetTimeout(TimeSpan? Timeout)
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
            public Builder SetCookie(HTTPCookies Cookies)
            {
                this.Cookie = Cookies;
                return this;
            }

            #endregion

            #endregion


            #region Set(Field, Value)

            public void Set(String Field, Object Value)
            {

                SetHeaderField(new HTTPHeaderField(
                                   Field,
                                   HeaderFieldType.Request,
                                   RequestPathSemantic.both
                               ),
                               Value);

            }

            #endregion


            public Task<HTTPResponse> ExecuteReturnResult()
                => httpClient?.Execute(AsImmutable);


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

                    return new HTTPRequest(
                               Illias.Timestamp.Now,
                               new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                               IPSocket.LocalhostV4(IPPort.HTTPS),
                               IPSocket.LocalhostV4(IPPort.HTTPS),
                               EntireRequestHeader,
                               Content,
                               HTTPServer: HTTPServer) {

                        FakeURLPrefix = FakeURLPrefix

                    };

                }
            }

            #endregion

        }

    }

}
