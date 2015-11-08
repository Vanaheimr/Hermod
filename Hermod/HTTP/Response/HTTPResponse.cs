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
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    #region HTTPResponse<T>

    /// <summary>
    /// A helper class to transport HTTP data and its metadata.
    /// </summary>
    /// <typeparam name="T">The type of the transported data.</typeparam>
    public class HTTPResponse<T>
    {

        #region Properties

        public  readonly HTTPResponse   HttpResponse;

        public  readonly T              Content;

        public  readonly Exception      Exception;


        private readonly Boolean        IsFault;


        #region HasErrors

        public Boolean HasErrors
        {
            get
            {
                return Exception != null && !IsFault;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        public HTTPResponse(HTTPResponse  HttpResponse,
                            T             Content,
                            Boolean       IsFault = false)
        {
            this.HttpResponse  = HttpResponse;
            this.Content       = Content;
            this.IsFault       = IsFault;
            this.Exception     = null;
        }

        public HTTPResponse()
        {
            this.HttpResponse  = new HTTPResponse() { HTTPStatusCode = HTTPStatusCode.OK };
            this.Content       = default(T);
            this.IsFault       = false;
            this.Exception     = null;
        }

        public HTTPResponse(Exception e)
        {
            this.HttpResponse  = null;
            this.Content       = default(T);
            this.IsFault       = true;
            this.Exception     = e;
        }

        public HTTPResponse(HTTPResponse  HttpResponse,
                            Exception     e)
        {
            this.HttpResponse  = HttpResponse;
            this.Content       = default(T);
            this.IsFault       = true;
            this.Exception     = e;
        }

        public HTTPResponse(HTTPResponse  HttpResponse,
                            T             Content,
                            Exception     e)
        {
            this.HttpResponse  = HttpResponse;
            this.Content       = Content;
            this.IsFault       = true;
            this.Exception     = e;
        }

        #endregion

    }

    #endregion

    #region HTTPResponse

    /// <summary>
    /// A read-only HTTP response header.
    /// </summary>
    public class HTTPResponse : AHTTPPDU
    {

        #region Properties

        #region HTTPRequest

        private readonly HTTPRequest _HTTPRequest;

        public HTTPRequest  HTTPRequest
        {
            get
            {
                return _HTTPRequest;
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

        #region TransferEncoding

        public String TransferEncoding
        {
            get
            {
                return GetHeaderField(HTTPHeaderField.TransferEncoding);
            }
        }

        #endregion


        #region Exception

        private readonly Exception _Exception;

        public Exception Exception
        {
            get
            {
                return _Exception;
            }
        }

        #endregion

        #region HasException

        public Boolean HasException
        {
            get
            {
                return _Exception != null;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPResponse()

        public HTTPResponse()
        { }

        #endregion

        #region HTTPResponse(HTTPRequest, HTTPHeaderAsText)

        public HTTPResponse(HTTPRequest  HTTPRequest,
                            String       HTTPHeaderAsText)
        {

            this._HTTPRequest = HTTPRequest;

            if (ParseResponseHeader(HTTPHeaderAsText))
                base.ContentStream = new MemoryStream();

        }

        #endregion

        #region HTTPResponse(HTTPRequest, HTTPHeader, Content)

        public HTTPResponse(HTTPRequest  HTTPRequest,
                            String       HTTPHeader,
                            Byte[]       Content)
        {

            this._HTTPRequest = HTTPRequest;

            if (ParseResponseHeader(HTTPHeader))
                base.Content = Content;

        }

        #endregion

        #region HTTPResponse(HTTPRequest, HTTPHeader, ContentStream)

        public HTTPResponse(HTTPRequest  HTTPRequest,
                            String       HTTPHeader,
                            Stream       ContentStream)
        {

            this._HTTPRequest = HTTPRequest;

            if (ParseResponseHeader(HTTPHeader))
                base.ContentStream  = ContentStream;

        }

        #endregion

        #region HTTPResponse(Exception)

        public HTTPResponse(Exception Exception)
        {

            base.HTTPStatusCode  = HTTPStatusCode;
            this._Exception      = Exception;

        }

        #endregion

        #region HTTPResponse(HTTPStatusCode, Content = null)

        public HTTPResponse(HTTPStatusCode  HTTPStatusCode,
                            String          Content = null)
        {

            base.HTTPStatusCode  = HTTPStatusCode;
            base.Content         = Content.ToUTF8Bytes();

        }

        #endregion

        #endregion


        #region ParseResponseHeader(HTTPHeader)

        protected Boolean ParseResponseHeader(String HTTPHeader)
        {

            //this.HTTPStatusCode = HTTPStatusCode.BadRequest;

            RawHTTPHeader = HTTPHeader;

            var _HTTPHeaderLines = HTTPHeader.Split(_LineSeparator, StringSplitOptions.RemoveEmptyEntries);
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

            //if (HTTPStatusCode != HTTPStatusCode.BadRequest)
            //{

                String[] _KeyValuePairs = null;

                foreach (var _Line in _HTTPHeaderLines.Skip(1))
                {

                    _KeyValuePairs = _Line.Split(_ColonSeparator, 2, StringSplitOptions.RemoveEmptyEntries);

                    if (_KeyValuePairs.Length == 2)
                        HeaderFields.Add(_KeyValuePairs[0].Trim(), _KeyValuePairs[1].Trim());
                    else
                    {
                        HTTPStatusCode = HTTPStatusCode.BadRequest;
                        return false;
                    }

                }

            //}

            //this.HTTPStatusCode = HTTPStatusCode.OK;
            return true;

        }

        #endregion

        #region NewContentStream()

        public MemoryStream NewContentStream()
        {

            var _MemoryStream = new MemoryStream();

            base.ContentStream = _MemoryStream;

            return _MemoryStream;

        }

        #endregion

        #region ContentStreamToArray(DataStream = null)

        public void ContentStreamToArray(Stream DataStream = null)
        {

            if (DataStream == null)
                Content = ((MemoryStream) ContentStream).ToArray();
            else
                Content = ((MemoryStream) DataStream).ToArray();

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {
            return HTTPStatusCode.ToString();
        }

        #endregion

    }

    #endregion

}
