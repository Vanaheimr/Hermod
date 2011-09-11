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
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{

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



        #region Server

        public String Server
        {
            get
            {
                return GetHeaderField("Server");
            }
        }

        #endregion

        #region Location

        public String Location
        {
            get
            {
                return GetHeaderField("Location");
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
        //}

        //#endregion

        #region Date

        public String Date
        {
            get
            {
                return GetHeaderField("Date");
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

}
