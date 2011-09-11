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
                SetHeaderField(HTTPResponseHeaderField.CacheControl, value);
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
                SetHeaderField(HTTPResponseHeaderField.Connection, value);
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
                SetHeaderField(HTTPResponseHeaderField.ContentEncoding, value);
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
                SetHeaderField(HTTPResponseHeaderField.ContentLanguage, value);
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
                SetHeaderField(HTTPResponseHeaderField.ContentLength, value);
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
                SetHeaderField(HTTPResponseHeaderField.ContentLocation, value);
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
                SetHeaderField(HTTPResponseHeaderField.ContentMD5, value);
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
                SetHeaderField(HTTPResponseHeaderField.ContentRange, value);
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
                SetHeaderField(HTTPResponseHeaderField.ContentType, value);
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
                SetHeaderField(HTTPResponseHeaderField.Via, value);
            }

        }

        #endregion

        #endregion


        #region Server

        public new String Server
        {

            get
            {
                return base.Date;
            }

            set
            {
                SetHeaderField("Server", value);
            }

        }

        #endregion

        #region Location

        public new String Location
        {

            get
            {
                return base.Date;
            }

            set
            {
                SetHeaderField("Location", value);
            }

        }

        #endregion

        //#region KeepAlive

        //public Boolean KeepAlive
        //{

        //    get
        //    {

        //        if (HeaderFields.ContainsKey("Connection"))
        //            if (HeaderFields["Connection"] is String)
        //                if (((String) HeaderFields["Connection"]) == "Keep-Alive")
        //                    return true;

        //        return false;

        //    }

        //    set
        //    {

        //        if (value == true)
        //        {
        //            if (HeaderFields.ContainsKey("Connection"))
        //                HeaderFields["Connection"] = "Keep-Alive";
        //            else
        //                HeaderFields.Add("Content-Type", value);
        //        }

        //        else
        //            if (HeaderFields.ContainsKey("Connection"))
        //                HeaderFields.Remove("Connection");

        //    }

        //}

        //#endregion

        #region Date

        public new String Date
        {

            get
            {
                return base.Date;
            }

            set
            {
                SetHeaderField("Date", value);
            }

        }

        #endregion

        public List<AcceptType> AcceptTypes { get; set; }
        public String AcceptEncoding { get; set; }
        public List<HTTPMethod> Allow { get; set; }
        public String Destination { get; set; }
        public HTTPBasicAuthentication Authorization { get; set; }

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

}
