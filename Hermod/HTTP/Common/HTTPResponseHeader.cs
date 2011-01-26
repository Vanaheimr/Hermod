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

    public class HTTPResponseHeader
    {

        #region Data

        private readonly Dictionary<String, Object> _HeaderFields;

        #endregion

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

            set
            {

                if (value != null)
                {

                    if (_HeaderFields.ContainsKey("Connection"))
                        _HeaderFields["Connection"] = value;
                    else
                        _HeaderFields.Add("Connection", value);

                }

                else
                    if (_HeaderFields.ContainsKey("Connection"))
                        _HeaderFields.Remove("Connection");

            }

        }

        #endregion

        #region ContentLength

        public UInt64 ContentLength
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

            set
            {

                if (_HeaderFields.ContainsKey("Content-Length"))
                    _HeaderFields["Content-Length"] = value;
                
                else
                    _HeaderFields.Add("Content-Length", value);

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

            set
            {

                if (value != null)
                {
                
                    if (_HeaderFields.ContainsKey("Content-Type"))
                        _HeaderFields["Content-Type"] = value;
                    else
                        _HeaderFields.Add("Content-Type", value);

                }
                
                else
                    if (_HeaderFields.ContainsKey("Content-Type"))
                        _HeaderFields.Remove("Content-Type");

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

            set
            {

                if (value != null)
                {

                    if (_HeaderFields.ContainsKey("Cache-Control"))
                        _HeaderFields["Cache-Control"] = value;

                    else
                        _HeaderFields.Add("Cache-Control", value);

                }

                else
                    if (_HeaderFields.ContainsKey("Cache-Control"))
                        _HeaderFields.Remove("Cache-Control");

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

            set
            {

                if (value != null)
                {
                    
                    if (_HeaderFields.ContainsKey("Server"))
                        _HeaderFields["Server"] = value;
                    else
                        _HeaderFields.Add("Server", value);

                }

                else
                    if (_HeaderFields.ContainsKey("Server"))
                        _HeaderFields.Remove("Server");

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

            set
            {

                if (value == true)
                {
                    if (_HeaderFields.ContainsKey("Connection"))
                        _HeaderFields["Connection"] = "Keep-Alive";
                    else
                        _HeaderFields.Add("Content-Type", value);
                }

                else
                    if (_HeaderFields.ContainsKey("Connection"))
                        _HeaderFields.Remove("Connection");

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

            set
            {

                if (value != null)
                {

                    if (_HeaderFields.ContainsKey("Date"))
                        _HeaderFields["Date"] = value;
                    else
                        _HeaderFields.Add("Date", value);

                }

                else
                    if (_HeaderFields.ContainsKey("Date"))
                        _HeaderFields.Remove("Date");

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

        #region HTTPResponseHeader()

        public HTTPResponseHeader()
        {
            
            _HeaderFields  = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
            
            ContentLength  = 0;
            HttpStatusCode = HTTPStatusCode.OK;
            Date           = DateTime.Now.ToString();

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
