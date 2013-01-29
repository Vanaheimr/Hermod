/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Sockets;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A read-only HTTP response header.
    /// </summary>
    public class HTTPResponse : AHTTPPDU
    {

        #region Properties

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

        #region HTTPResponse()

        public HTTPResponse()
        { }

        #endregion

        #region HTTPResponse(HTTPHeader)

        public HTTPResponse(String HTTPHeader)
        {
            if (ParseResponseHeader(HTTPHeader))
                base.ContentStream = new MemoryStream();
        }

        #endregion

        #region HTTPResponse(HTTPHeader, Content)

        public HTTPResponse(String HTTPHeader, Byte[] Content)
        {
            if (ParseResponseHeader(HTTPHeader))
                base.Content = Content;
        }

        #endregion

        #region HTTPResponse(HTTPHeader, ContentStream)

        public HTTPResponse(String HTTPHeader, Stream ContentStream)
        {
            if (ParseResponseHeader(HTTPHeader))
                base.ContentStream = ContentStream;
        }

        #endregion

        #endregion


        #region ParseResponseHeader(HTTPHeader)

        protected Boolean ParseResponseHeader(String HTTPHeader)
        {

            //this.HTTPStatusCode = HTTPStatusCode.BadRequest;

            RawHTTPHeader = HTTPHeader;

            var _HTTPHeaderLines = HTTPHeader.Split(_LineSeperator, StringSplitOptions.RemoveEmptyEntries);
            if (_HTTPHeaderLines.Length == 0)
            {
                HTTPStatusCode = HTTPStatusCode.BadRequest;
                return false;
            }

            FirstPDULine = _HTTPHeaderLines.FirstOrDefault();
            var SplittedFirstLine = FirstPDULine.Split(' ');

            if (SplittedFirstLine.Length < 3)
            {
                HTTPStatusCode = HTTPStatusCode.BadRequest;
                return false;
            }

            HTTPStatusCode = HTTPStatusCode.ParseString(SplittedFirstLine[1]);

            if (HTTPStatusCode != HTTPStatusCode.BadRequest)
            {

                String[] _KeyValuePairs = null;

                foreach (var _Line in _HTTPHeaderLines.Skip(1))
                {

                    _KeyValuePairs = _Line.Split(_ColonSeperator, 2, StringSplitOptions.RemoveEmptyEntries);

                    if (_KeyValuePairs.Length == 2)
                        HeaderFields.Add(_KeyValuePairs[0].Trim(), _KeyValuePairs[1].Trim());
                    else
                    {
                        HTTPStatusCode = HTTPStatusCode.BadRequest;
                        return false;
                    }

                }

            }

            //this.HTTPStatusCode = HTTPStatusCode.OK;
            return true;

        }

        #endregion



        public void ContentStreamToArray()
        {
            Content = ((MemoryStream) ContentStream).ToArray();
        }

    }

}
