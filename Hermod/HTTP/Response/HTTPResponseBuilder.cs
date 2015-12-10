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
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A read-write HTTP response header.
    /// </summary>
    public class HTTPResponseBuilder : AHTTPPDUBuilder
    {

        #region Properties

        #region HTTPRequest

        private readonly HTTPRequest _HTTPRequest;

        public HTTPRequest HTTPRequest
        {
            get
            {
                return _HTTPRequest;
            }
        }

        #endregion


        #region Non-http header fields

        #region HTTPHeader

        public String HTTPHeader
        {
            get
            {
                return HTTPStatusCode.HTTPResponseString + Environment.NewLine +
                       ConstructedHTTPHeader       + Environment.NewLine +
                       Environment.NewLine;
            }
        }

        #endregion

        #endregion

        #region Response header fields

        #region Age

        public UInt64? Age
        {

            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.Age);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Age, value);
            }

        }

        #endregion

        #region Allow

        public List<HTTPMethod> Allow
        {

            get
            {
                return GetHeaderField<List<HTTPMethod>>(HTTPHeaderField.Age);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Allow, value);
            }

        }

        #endregion

        #region DAV

        public String DAV
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Age);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.DAV, value);
            }

        }

        #endregion

        #region ETag

        public String ETag
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.ETag);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ETag, value);
            }

        }

        #endregion

        #region Expires

        public String Expires
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Expires);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Expires, value);
            }

        }

        #endregion

        #region KeepAlive

        public KeepAliveType KeepAlive
        {

            get
            {
                return new KeepAliveType(GetHeaderField(HTTPHeaderField.KeepAlive));
            }

            set
            {
                SetHeaderField(HTTPHeaderField.KeepAlive, value);
            }

        }

        #endregion

        #region LastModified

        public String LastModified
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.LastModified);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.LastModified, value);
            }

        }

        #endregion

        #region Location

        public String Location
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Location);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Location, value);
            }

        }

        #endregion

        #region ProxyAuthenticate

        public String ProxyAuthenticate
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.ProxyAuthenticate);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ProxyAuthenticate, value);
            }

        }

        #endregion

        #region RetryAfter

        public String RetryAfter
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.RetryAfter);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.RetryAfter, value);
            }

        }

        #endregion

        #region Server

        public String Server
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Server);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Server, value);
            }

        }

        #endregion

        #region SetCookie

        public String SetCookie
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.SetCookie);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.SetCookie, value);
            }

        }

        #endregion

        #region Vary

        public String Vary
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.Vary);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Vary, value);
            }

        }

        #endregion

        #region WWWAuthenticate

        public String WWWAuthenticate
        {

            get
            {
                return GetHeaderField(HTTPHeaderField.WWWAuthenticate);
            }

            set
            {
                SetHeaderField(HTTPHeaderField.WWWAuthenticate, value);
            }

        }

        #endregion

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP response.
        /// </summary>
        /// <param name="HTTPRequest">The HTTP request for this response.</param>
        public HTTPResponseBuilder(HTTPRequest HTTPRequest = null)
        {
            this._HTTPRequest     = HTTPRequest;
            this.HTTPStatusCode   = HTTPStatusCode.ImATeapot;
            this.ProtocolName     = "HTTP";
            this.ProtocolVersion  = new HTTPVersion(1, 1);
        }

        #endregion


        #region Set(HeaderField, Value)

        /// <summary>
        /// Set a HTTP header field.
        /// A field value of NULL will remove the field from the header.
        /// </summary>
        /// <param name="HeaderField">The header field.</param>
        /// <param name="Value">The value. NULL will remove the field from the header.</param>
        public HTTPResponseBuilder Set(HTTPHeaderField HeaderField, Object Value)
        {

            base.SetHeaderField(HeaderField, Value);

            return this;

        }

        #endregion


        #region (implicit operator) HTTPResponseBuilder => HTTPResponse

        /// <summary>
        /// Declare an implicit conversion of a HTTPResponseBuilder
        /// to a HTTPResponse object.
        /// </summary>
        /// <param name="HTTPRequestBuilder">A HTTPResponseBuilder.</param>
        public static implicit operator HTTPResponse(HTTPResponseBuilder HTTPResponseBuilder)
        {
            return HTTPResponseBuilder.AsImmutable();
        }

        #endregion


        #region Set non-http header fields

        #region SetHTTPStatusCode(HTTPStatusCode)

        /// <summary>
        /// Set the HTTP status code.
        /// </summary>
        /// <param name="HTTPStatusCode">A HTTP status code.</param>
        public HTTPResponseBuilder SetHTTPStatusCode(HTTPStatusCode HTTPStatusCode)
        {
            this.HTTPStatusCode = HTTPStatusCode;
            return this;
        }

        #endregion

        #region SetProtocolName(ProtocolName)

        /// <summary>
        /// Set the protocol name.
        /// </summary>
        /// <param name="ProtocolName">The protocol name.</param>
        public HTTPResponseBuilder SetProtocolName(String ProtocolName)
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
        public HTTPResponseBuilder SetProtocolVersion(HTTPVersion ProtocolVersion)
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
        public HTTPResponseBuilder SetContent(Byte[] ByteArray)
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
        public HTTPResponseBuilder SetContent(String String)
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
        public HTTPResponseBuilder SetContent(Stream ContentStream)
        {
            this.ContentStream = ContentStream;
            return this;
        }

        #endregion

        #endregion

        #region Set general header fields

        #region SetCacheControl(CacheControl)

        /// <summary>
        /// Set the HTTP CacheControl.
        /// </summary>
        /// <param name="CacheControl">CacheControl.</param>
        public HTTPResponseBuilder SetCacheControl(String CacheControl)
        {
            this.CacheControl = CacheControl;
            return this;
        }

        #endregion

        #region SetConnection(Connection)

        /// <summary>
        /// Set the HTTP connection.
        /// </summary>
        /// <param name="Connection">A connection.</param>
        public HTTPResponseBuilder SetConnection(String Connection)
        {
            this.Connection = Connection;
            return this;
        }

        #endregion

        #region SetContentEncoding(ContentEncoding)

        /// <summary>
        /// Set the HTTP Content-Encoding.
        /// </summary>
        /// <param name="ContentEncoding">The encoding of the HTTP content/body.</param>
        public HTTPResponseBuilder SetContentEncoding(Encoding ContentEncoding)
        {
            this.ContentEncoding = ContentEncoding;
            return this;
        }

        #endregion

        #region SetContentLanguage(ContentLanguages)

        /// <summary>
        /// Set the HTTP Content-Languages.
        /// </summary>
        /// <param name="ContentLanguages">The languages of the HTTP content/body.</param>
        public HTTPResponseBuilder SetContentLanguage(List<String> ContentLanguages)
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
        public HTTPResponseBuilder SetContentLength(UInt64? ContentLength)
        {
            this.ContentLength = ContentLength;
            return this;
        }

        #endregion

        #region SetContentLocation(ContentLocation)

        /// <summary>
        /// Set the HTTP ContentLocation.
        /// </summary>
        /// <param name="ContentLocation">ContentLocation.</param>
        public HTTPResponseBuilder SetContentLocation(String ContentLocation)
        {
            this.ContentLocation = ContentLocation;
            return this;
        }

        #endregion

        #region SetContentMD5(ContentMD5)

        /// <summary>
        /// Set the HTTP ContentMD5.
        /// </summary>
        /// <param name="ContentMD5">ContentMD5.</param>
        public HTTPResponseBuilder SetContentMD5(String ContentMD5)
        {
            this.ContentMD5 = ContentMD5;
            return this;
        }

        #endregion

        #region SetContentRange(ContentRange)

        /// <summary>
        /// Set the HTTP ContentRange.
        /// </summary>
        /// <param name="ContentRange">ContentRange.</param>
        public HTTPResponseBuilder SetContentRange(String ContentRange)
        {
            this.ContentRange = ContentRange;
            return this;
        }

        #endregion

        #region SetContentType(ContentType)

        /// <summary>
        /// Set the HTTP Content-Type.
        /// </summary>
        /// <param name="ContentType">The type of the HTTP content/body.</param>
        public HTTPResponseBuilder SetContentType(HTTPContentType ContentType)
        {
            this.ContentType = ContentType;
            return this;
        }

        #endregion

        #region SetDate(Date)

        /// <summary>
        /// Set the HTTP Date.
        /// </summary>
        /// <param name="Date">DateTime.</param>
        public HTTPResponseBuilder SetDate(DateTime Date)
        {
            this.Date = Date;
            return this;
        }

        #endregion

        #region SetVia(Via)

        /// <summary>
        /// Set the HTTP Via.
        /// </summary>
        /// <param name="Via">Via.</param>
        public HTTPResponseBuilder SetVia(String Via)
        {
            this.Via = Via;
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
        /// Converts this HTTPResponseBuilder into an immutable HTTPResponse.
        /// </summary>
        public HTTPResponse AsImmutable()
        {

            PrepareImmutability();

            if (Content != null)
                return new HTTPResponse(_HTTPRequest, HTTPHeader, Content);

            else if (ContentStream != null)
                return new HTTPResponse(_HTTPRequest, HTTPHeader, ContentStream);

            else
                return new HTTPResponse(_HTTPRequest, HTTPHeader);

        }

        #endregion

    }

}
