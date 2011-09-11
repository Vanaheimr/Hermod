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
using System.Text;
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    #region HTTPResponseHeader

    /// <summary>
    /// A read-only HTTP response header.
    /// </summary>
    public class HTTPResponseHeader : AHTTPHeader
    {

        #region Properties

        #region GetResponseHeader

        public String GetResponseHeader
        {
            get
            {
                var _RAWHTTPHeader = HTTPStatusCode.SimpleString + Environment.NewLine + RAWHTTPHeader + Environment.NewLine + Environment.NewLine;
                return _RAWHTTPHeader;
            }
        }

        #endregion


        #region Age

        public UInt64? Age
        {
            get
            {
                return GetHeaderField_UInt64(HTTPHeaderField.Age);
            }
        }

        #endregion

        #region Allow

        public List<HTTPMethod> Allow
        {
            get
            {
                return GetHeaderField<List<HTTPMethod>>(HTTPHeaderField.Allow);
            }
        }

        #endregion

        #region DAV

        public String DAV
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.DAV);
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
        }

        #endregion

        #region Expires

        public String Expires
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Expires);
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
        }

        #endregion

        #region Location

        public String Location
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Location);
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
        }

        #endregion

        #region RetryAfter

        public String RetryAfter
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.RetryAfter);
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
        }

        #endregion

        #region Vary

        public String Vary
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.Vary);
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
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPResponseHeader()

        public HTTPResponseHeader()
        { }

        #endregion

        #region HTTPResponseHeader(HTTPHeader)

        public HTTPResponseHeader(String HTTPHeader)
        {
            ParseHeader(HTTPHeader.Split(_LineSeperator, StringSplitOptions.RemoveEmptyEntries).Skip(1));
        }

        #endregion

        #endregion

    }

    #endregion

    #region HTTPResponseHeader_RW

    /// <summary>
    /// A read-write HTTP response header.
    /// </summary>
    public class HTTPResponseHeader_RW : HTTPResponseHeader
    {

        #region Properties

        #region HTTPStatusCode

        public new HTTPStatusCode HTTPStatusCode
        {
            
            get
            {
                return base.HTTPStatusCode;
            }

            set
            {
                base.HTTPStatusCode = value;
            }

        }

        #endregion

        #region General header fields

        #region CacheControl

        public new String CacheControl
        {

            get
            {
                return base.CacheControl;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.CacheControl, value);
            }

        }

        #endregion

        #region Connection

        public new String Connection
        {

            get
            {
                return base.Connection;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Connection, value);
            }

        }

        #endregion

        #region ContentEncoding

        public new Encoding ContentEncoding
        {

            get
            {
                return base.ContentEncoding;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentEncoding, value);
            }

        }

        #endregion

        #region ContentLanguage

        public new List<String> ContentLanguage
        {

            get
            {
                return base.ContentLanguage;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLanguage, value);
            }

        }

        #endregion

        #region ContentLength

        public new UInt64? ContentLength
        {

            get
            {
                return base.ContentLength;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLength, value);
            }

        }

        #endregion

        #region ContentLocation

        public new String ContentLocation
        {

            get
            {
                return base.ContentLocation;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentLocation, value);
            }

        }

        #endregion

        #region ContentMD5

        public new String ContentMD5
        {

            get
            {
                return base.ContentMD5;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentMD5, value);
            }

        }

        #endregion

        #region ContentRange

        public new String ContentRange
        {

            get
            {
                return base.ContentRange;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentRange, value);
            }

        }

        #endregion

        #region ContentType

        public new HTTPContentType ContentType
        {

            get
            {
                return base.ContentType;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ContentType, value);
            }

        }

        #endregion

        #region Date

        public new String Date
        {

            get
            {
                return base.Date;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Date, value);
            }

        }

        #endregion

        #region Via

        public new String Via
        {

            get
            {
                return base.Via;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Via, value);
            }

        }

        #endregion

        #endregion

        #region Response header fields

        #region Age

        public new UInt64? Age
        {

            get
            {
                return base.Age;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Age, value);
            }

        }

        #endregion

        #region Allow

        public new List<HTTPMethod> Allow
        {

            get
            {
                return base.Allow;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Allow, value);
            }

        }

        #endregion

        #region DAV

        public new String DAV
        {

            get
            {
                return base.DAV;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.DAV, value);
            }

        }

        #endregion

        #region ETag

        public new String ETag
        {

            get
            {
                return base.ETag;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ETag, value);
            }

        }

        #endregion

        #region Expires

        public new String Expires
        {

            get
            {
                return base.Expires;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Expires, value);
            }

        }

        #endregion

        #region LastModified

        public new String LastModified
        {

            get
            {
                return base.LastModified;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.LastModified, value);
            }

        }

        #endregion

        #region Location

        public new String Location
        {

            get
            {
                return base.Location;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Location, value);
            }

        }

        #endregion

        #region ProxyAuthenticate

        public new String ProxyAuthenticate
        {

            get
            {
                return base.ProxyAuthenticate;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.ProxyAuthenticate, value);
            }

        }

        #endregion

        #region RetryAfter

        public new String RetryAfter
        {

            get
            {
                return base.RetryAfter;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.RetryAfter, value);
            }

        }

        #endregion

        #region Server

        public new String Server
        {

            get
            {
                return base.Server;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Server, value);
            }

        }

        #endregion

        #region Vary

        public new String Vary
        {

            get
            {
                return base.Vary;
            }

            set
            {
                SetHeaderField(HTTPHeaderField.Vary, value);
            }

        }

        #endregion

        #region WWWAuthenticate

        public new String WWWAuthenticate
        {

            get
            {
                return base.WWWAuthenticate;
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

        #region HTTPResponseHeader_RW()

        public HTTPResponseHeader_RW()
        { }

        #endregion

        #endregion


        #region SetHTTPStatusCode(HTTPStatusCode)

        /// <summary>
        /// Set the HTTP status code.
        /// </summary>
        /// <param name="HTTPStatusCode">A HTTP status code.</param>
        public HTTPResponseHeader_RW SetHTTPStatusCode(HTTPStatusCode HTTPStatusCode)
        {
            this.HTTPStatusCode = HTTPStatusCode;
            return this;
        }

        #endregion

        #region Set General header fields

        #region SetCacheControl(CacheControl)

        /// <summary>
        /// Set the HTTP CacheControl.
        /// </summary>
        /// <param name="CacheControl">CacheControl.</param>
        public HTTPResponseHeader_RW SetCacheControl(String CacheControl)
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
        public HTTPResponseHeader_RW SetConnection(String Connection)
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
        public HTTPResponseHeader_RW SetContentEncoding(Encoding ContentEncoding)
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
        public HTTPResponseHeader_RW SetContentLanguage(List<String> ContentLanguages)
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
        public HTTPResponseHeader_RW SetContentLength(UInt64? ContentLength)
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
        public HTTPResponseHeader_RW SetContentLocation(String ContentLocation)
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
        public HTTPResponseHeader_RW SetContentMD5(String ContentMD5)
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
        public HTTPResponseHeader_RW SetContentRange(String ContentRange)
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
        public HTTPResponseHeader_RW SetContentType(HTTPContentType ContentType)
        {
            this.ContentType = ContentType;
            return this;
        }

        #endregion

        #region SetVia(Via)

        /// <summary>
        /// Set the HTTP Via.
        /// </summary>
        /// <param name="Via">Via.</param>
        public HTTPResponseHeader_RW SetVia(String Via)
        {
            this.Via = Via;
            return this;
        }

        #endregion

        #endregion

    }

    #endregion

}
