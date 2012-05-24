/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using de.ahzf.Illias.Commons;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A read-write HTTP request header.
    /// </summary>
    public class HTTPRequestBuilder : AHTTPPDUBuilder
    {

        #region Properties

        #region Non-http header fields

        //public HTTPClient HTTPClient { get; private set; }

        public String EntireRequestHeader
        {
            get
            {
                return HTTPRequestLine + Environment.NewLine + ConstructedHTTPHeader;
            }
        }

        public String HTTPRequestLine
        {
            get
            {
                return HTTPMethod.ToString() + " " + this._UrlPath + " " + ProtocolName + "/" + ProtocolVersion;
            }
        }

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

        #region Url

        private String _UrlPath;

        /// <summary>
        /// The minimal URL (this means e.g. without the query string).
        /// </summary>
        public String UrlPath
        {

            get
            {
                return _UrlPath;
            }

            set
            {
                SetProperty(ref _UrlPath, value, "UrlPath");
            }

        }

        #endregion

        #region QueryString

        private readonly QueryString _QueryString;

        /// <summary>
        /// The HTTP query string.
        /// </summary>
        public QueryString QueryString
        {
            get
            {
                return _QueryString;
            }
        }

        #endregion

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

            set
            {
                SetHeaderField(HTTPHeaderField.Accept, value);
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

        public HTTPBasicAuthentication Authorization
        {

            get
            {
                return GetHeaderField<HTTPBasicAuthentication>(HTTPHeaderField.Authorization);
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

        public String Host
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Host);
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

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPRequestBuilder()

        /// <summary>
        /// Create a new HTTP request.
        /// </summary>
        public HTTPRequestBuilder()
        {
            this.HTTPStatusCode  = HTTPStatusCode.OK;
            this.HTTPMethod      = HTTPMethod.GET;
            this.UrlPath         = "/";
            this._QueryString    = new QueryString();
            this.Accept          = new AcceptTypes();
            this.ProtocolName    = "HTTP";
            this.ProtocolVersion = new HTTPVersion(1, 1);
        }

        #endregion

        //#region HTTPRequestBuilder(HTTPClient)

        ///// <summary>
        ///// Create a new HTTP request for the given HTTPClient.
        ///// </summary>
        //public HTTPRequestBuilder(HTTPClient HTTPClient)
        //    : this()
        //{
        //    this.HTTPClient = HTTPClient;
        //}

        //#endregion

        #endregion


        #region (operator) HTTPRequestBuilder => HTTPRequestHeader

        /// <summary>
        /// Declare an explicit conversion from a HTTPRequestBuilder to an HTTPRequestHeader
        /// </summary>
        /// <param name="HTTPRequestBuilder"></param>
        /// <returns></returns>
        public static implicit operator HTTPRequest(HTTPRequestBuilder Builder)
        {
            return Builder.AsImmutable();
        }

        #endregion


        #region Set non-http header fields

        #region SetHTTPStatusCode(HTTPStatusCode)

        /// <summary>
        /// Set the HTTP status code.
        /// </summary>
        /// <param name="HTTPStatusCode">A HTTP status code.</param>
        public HTTPRequestBuilder SetHTTPStatusCode(HTTPStatusCode HTTPStatusCode)
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
        public HTTPRequestBuilder SetHTTPMethod(HTTPMethod HTTPMethod)
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
        public HTTPRequestBuilder SetProtocolName(String ProtocolName)
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
        public HTTPRequestBuilder SetProtocolVersion(HTTPVersion ProtocolVersion)
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
        public HTTPRequestBuilder SetContent(Byte[] ByteArray)
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
        public HTTPRequestBuilder SetContent(String String)
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
        /// <param name="Stream">The HTTP content/body as a stream.</param>
        public HTTPRequestBuilder SetContent(Stream ContentStream)
        {
            this.ContentStream = ContentStream;
            return this;
        }

        #endregion

        #endregion

        #region Set general header fields

        #region SetCacheControl(CacheControl)

        /// <summary>
        /// Set the HTTP CacheControl header field.
        /// </summary>
        /// <param name="CacheControl">CacheControl.</param>
        public HTTPRequestBuilder SetCacheControl(String CacheControl)
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
        public HTTPRequestBuilder SetConnection(String Connection)
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
        public HTTPRequestBuilder SetContentEncoding(Encoding ContentEncoding)
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
        public HTTPRequestBuilder SetContentLanguage(List<String> ContentLanguages)
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
        public HTTPRequestBuilder SetContentLength(UInt64? ContentLength)
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
        public HTTPRequestBuilder SetContentLocation(String ContentLocation)
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
        public HTTPRequestBuilder SetContentMD5(String ContentMD5)
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
        public HTTPRequestBuilder SetContentRange(String ContentRange)
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
        public HTTPRequestBuilder SetContentType(HTTPContentType ContentType)
        {
            this.ContentType = ContentType;
            return this;
        }

        #endregion

        #region SetDate(DateTime)

        /// <summary>
        /// Set the HTTP Date header field.
        /// </summary>
        /// <param name="DateTime">DateTime.</param>
        public HTTPRequestBuilder SetVia(DateTime DateTime)
        {
            this.Date = DateTime.ToString();
            return this;
        }

        #endregion

        #region SetVia(Via)

        /// <summary>
        /// Set the HTTP Via header field.
        /// </summary>
        /// <param name="Via">Via.</param>
        public HTTPRequestBuilder SetVia(String Via)
        {
            this.Via = Via;
            return this;
        }

        #endregion

        #endregion

        #region Set request header fields

        #region SetAccept(AcceptTypes)

        /// <summary>
        /// Set the HTTP Accept header field.
        /// </summary>
        /// <param name="AcceptTypes">AcceptTypes.</param>
        public HTTPRequestBuilder SetAccept(AcceptTypes AcceptTypes)
        {
            this.Accept = Accept;
            return this;
        }

        #endregion

        #region AddAccept(AcceptType)

        /// <summary>
        /// Add an AcceptType to the Accept header field.
        /// </summary>
        /// <param name="AcceptType">An AcceptType.</param>
        public HTTPRequestBuilder AddAccept(AcceptType AcceptType)
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
        public HTTPRequestBuilder AddAccept(HTTPContentType HTTPContentType, Double Quality = 1)
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
        public HTTPRequestBuilder SetAcceptCharset(String AcceptCharset)
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
        public HTTPRequestBuilder SetAcceptEncoding(String AcceptEncoding)
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
        public HTTPRequestBuilder SetAcceptLanguage(String AcceptLanguage)
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
        public HTTPRequestBuilder SetAcceptRanges(String AcceptRanges)
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
        public HTTPRequestBuilder SetAuthorization(HTTPBasicAuthentication Authorization)
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
        public HTTPRequestBuilder SetDepth(String Depth)
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
        public HTTPRequestBuilder SetExpect(String Expect)
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
        public HTTPRequestBuilder SetFrom(String From)
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
        public HTTPRequestBuilder SetHost(String Host)
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
        public HTTPRequestBuilder SetIf(String If)
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
        public HTTPRequestBuilder SetIfMatch(String IfMatch)
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
        public HTTPRequestBuilder SetIfModifiedSince(String IfModifiedSince)
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
        public HTTPRequestBuilder SetIfNoneMatch(String IfNoneMatch)
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
        public HTTPRequestBuilder SetIfRange(String IfRange)
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
        public HTTPRequestBuilder SetIfUnmodifiedSince(String IfUnmodifiedSince)
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
        public HTTPRequestBuilder SetLockToken(String LockToken)
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
        public HTTPRequestBuilder SetMaxForwards(UInt64? MaxForwards)
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
        public HTTPRequestBuilder SetOverwrite(String Overwrite)
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
        public HTTPRequestBuilder SetProxyAuthorization(String ProxyAuthorization)
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
        public HTTPRequestBuilder SetRange(String Range)
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
        public HTTPRequestBuilder SetReferer(String Referer)
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
        public HTTPRequestBuilder SetTE(String TE)
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
        public HTTPRequestBuilder SetTimeout(UInt64? Timeout)
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
        public HTTPRequestBuilder SetUserAgent(String UserAgent)
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
        public HTTPRequestBuilder SetLastEventId(UInt64? LastEventId)
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
        public HTTPRequestBuilder SetCookie(String Cookie)
        {
            this.Cookie = Cookie;
            return this;
        }

        #endregion

        #endregion


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

        #region AsImmutable()

        /// <summary>
        /// Converts this HTTPRequestBuilder into an immutable HTTPRequest.
        /// </summary>
        public HTTPRequest AsImmutable()
        {

            PrepareImmutability();

            return new HTTPRequest();

        }

        #endregion

    }

}
