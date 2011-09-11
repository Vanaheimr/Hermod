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
using System.Web;
using System.Text;
using System.Linq;
using System.Net.Mime;
using System.Collections.Generic;
using System.Collections.Specialized;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

    public class HTTPResponseHeader_RW : HTTPResponseHeader
    {

        #region Properties

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
        


        #region Connection

        public new String Connection
        {

            get
            {
                return base.Connection;
            }

            set
            {
                SetHeaderField("Connection", value);
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
                SetHeaderField("Content-Length", value);
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
                SetHeaderField("Content-Type", value);
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
                SetHeaderField("Content-Encoding", value);
            }

        }

        #endregion


        #region CacheControl

        public String CacheControl
        {

            get
            {

                if (HeaderFields.ContainsKey("Cache-Control"))
                    return HeaderFields["Cache-Control"] as String;

                return null;

            }

            set
            {

                if (value != null)
                {

                    if (HeaderFields.ContainsKey("Cache-Control"))
                        HeaderFields["Cache-Control"] = value;

                    else
                        HeaderFields.Add("Cache-Control", value);

                }

                else
                    if (HeaderFields.ContainsKey("Cache-Control"))
                        HeaderFields.Remove("Cache-Control");

            }

        }

        #endregion

        #region Server

        public String Server
        {

            get
            {

                if (HeaderFields.ContainsKey("Server"))
                    return HeaderFields["Server"] as String;

                return null;

            }

            set
            {

                if (value != null)
                {
                    
                    if (HeaderFields.ContainsKey("Server"))
                        HeaderFields["Server"] = value;
                    else
                        HeaderFields.Add("Server", value);

                }

                else
                    if (HeaderFields.ContainsKey("Server"))
                        HeaderFields.Remove("Server");

            }

        }

        #endregion

        #region Location

        public String Location
        {

            get
            {

                if (HeaderFields.ContainsKey("Location"))
                    return HeaderFields["Location"] as String;

                return null;

            }

            set
            {

                if (value != null)
                {

                    if (HeaderFields.ContainsKey("Location"))
                        HeaderFields["Location"] = value;
                    else
                        HeaderFields.Add("Location", value);

                }

                else
                    if (HeaderFields.ContainsKey("Location"))
                        HeaderFields.Remove("Location");

            }

        }

        #endregion

        #region KeepAlive

        public Boolean KeepAlive
        {

            get
            {

                if (HeaderFields.ContainsKey("Connection"))
                    if (HeaderFields["Connection"] is String)
                        if (((String) HeaderFields["Connection"]) == "Keep-Alive")
                            return true;

                return false;

            }

            set
            {

                if (value == true)
                {
                    if (HeaderFields.ContainsKey("Connection"))
                        HeaderFields["Connection"] = "Keep-Alive";
                    else
                        HeaderFields.Add("Content-Type", value);
                }

                else
                    if (HeaderFields.ContainsKey("Connection"))
                        HeaderFields.Remove("Connection");

            }

        }

        #endregion

        #region Date

        public String Date
        {

            get
            {

                if (HeaderFields.ContainsKey("Date"))
                    return HeaderFields["Date"] as String;

                return null;

            }

            set
            {

                if (value != null)
                {

                    if (HeaderFields.ContainsKey("Date"))
                        HeaderFields["Date"] = value;
                    else
                        HeaderFields.Add("Date", value);

                }

                else
                    if (HeaderFields.ContainsKey("Date"))
                        HeaderFields.Remove("Date");

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

        #region SetContentEncoding(ContentEncoding)

        /// <summary>
        /// Set the HTTP Content-Encoding.
        /// </summary>
        /// <param name="ContentEncoding">The encoding of the HTTP content/body.</param>
        public HTTPResponseHeader_RW SetContentType(Encoding ContentEncoding)
        {
            this.ContentEncoding = ContentEncoding;
            return this;
        }

        #endregion



    }

}
