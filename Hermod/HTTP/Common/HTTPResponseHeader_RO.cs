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

    public class HTTPResponseHeader_RO : AHTTPResponseHeader
    {

        #region Properties

        #region RAWHTTPHeader

        public String RAWHTTPHeader
        {
            get
            {
                
                if (_HeaderFields.Count > 0)
                    return (from   _KeyValuePair in _HeaderFields
                            where  _KeyValuePair.Key   != null
                            where  _KeyValuePair.Value != null
                            select _KeyValuePair.Key + ": " + _KeyValuePair.Value).
                            Aggregate((a, b) => a + Environment.NewLine + b);

                return null;

            }
        }

        #endregion

        #region GetResponseHeader

        public String GetResponseHeader
        {
            get
            {

                var _RAWHTTPHeader = HttpStatusCode.SimpleString + Environment.NewLine + RAWHTTPHeader + Environment.NewLine + Environment.NewLine;

                return _RAWHTTPHeader;

                ////      AddToResponseString(result, "Cache-Control",    CacheControl);
                //      AddToResponseString(result, "Content-Length",   _HeaderFields["Content-Length"].ToString());
                //      AddToResponseString(result, "Content-Type",     _HeaderFields["Content-Type"].ToString());
                //      AddToResponseString(result, "Date",             DateTime.Now.ToString());
                //      AddToResponseString(result, "Server",           Server);

                //      if (KeepAlive)
                //          AddToResponseString(result, "Connection", "Keep-Alive");
            }
        }

        #endregion


        #region Connection

        public String Connection
        {
            get
            {

                if (_HeaderFields.ContainsKey("Connection"))
                    return _HeaderFields["Connection"] as String;

                return null;

            }
        }

        #endregion

        #region ContentLength

        public UInt64? ContentLength
        {
            get
            {

                try
                {
                    if (_HeaderFields.ContainsKey("Content-Length"))
                        return (UInt64) _HeaderFields["Content-Length"];
                }
                finally
                {}

                return default(UInt64);

            }
        }

        #endregion

        #region ContentType

        public HTTPContentType ContentType
        {
            get
            {
                
                if (_HeaderFields.ContainsKey("Content-Type"))
                    return _HeaderFields["Content-Type"] as HTTPContentType;
                
                return null;

            }
        }

        #endregion

        #region CacheControl

        public String CacheControl
        {
            get
            {

                if (_HeaderFields.ContainsKey("Cache-Control"))
                    return _HeaderFields["Cache-Control"] as String;

                return null;

            }
        }

        #endregion

        #region Server

        public String Server
        {
            get
            {

                if (_HeaderFields.ContainsKey("Server"))
                    return _HeaderFields["Server"] as String;

                return null;

            }
        }

        #endregion

        #region Location

        public String Location
        {
            get
            {

                if (_HeaderFields.ContainsKey("Location"))
                    return _HeaderFields["Location"] as String;

                return null;

            }
        }

        #endregion

        #region KeepAlive

        public Boolean KeepAlive
        {
            get
            {

                if (_HeaderFields.ContainsKey("Connection"))
                    if (_HeaderFields["Connection"] is String)
                        if (((String) _HeaderFields["Connection"]) == "Keep-Alive")
                            return true;

                return false;

            }
        }

        #endregion

        #region Date

        public String Date
        {
            get
            {

                if (_HeaderFields.ContainsKey("Date"))
                    return _HeaderFields["Date"] as String;

                return null;

            }
        }

        #endregion


        public List<AcceptType> AcceptTypes { get; set; }
        public Encoding ContentEncoding { get; set; }
        public String AcceptEncoding { get; set; }

        public List<HTTPMethod> Allow { get; set; }



        public String Destination { get; set; }

        public HTTPStatusCode HttpStatusCode { get; set; }

        

        public HTTPBasicAuthentication Authorization { get; set; }

        #endregion

        #region Constructor(s)

        #region HTTPResponseHeader_RO()

        public HTTPResponseHeader_RO()
        {
            HttpStatusCode = HTTPStatusCode.OK;
        }

        #endregion

        #region HTTPResponseHeader_RO()

        public HTTPResponseHeader_RO(String HTTPHeader)
        {
            this.HttpStatusCode = HTTPStatusCode.OK;
        }

        #endregion

        #endregion


        #region ToString()

        public override String ToString()
        {
            return RAWHTTPHeader;
        }

        #endregion

    }

}
