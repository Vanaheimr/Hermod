/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.Threading;

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
            this.HttpResponse  = HTTPResponseBuilder.OK();
            this.Content       = default(T);
            this.IsFault       = false;
            this.Exception     = null;
        }

        public HTTPResponse(T Content)
        {
            this.HttpResponse  = HTTPResponseBuilder.OK();
            this.Content       = Content;
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

        #region Non-http header fields

        #region HTTPRequest

        private readonly HTTPRequest _HTTPRequest;

        /// <summary>
        /// The HTTP request for this HTTP response.
        /// </summary>
        public HTTPRequest  HTTPRequest
        {
            get
            {
                return _HTTPRequest;
            }
        }

        #endregion

        #region CancellationToken

        /// <summary>
        /// The cancellation token.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        #endregion

        #endregion

        #region HTTPStatusCode

        private readonly HTTPStatusCode _HTTPStatusCode;

        /// <summary>
        /// The HTTP status code.
        /// </summary>
        public HTTPStatusCode HTTPStatusCode
        {
            get
            {
                return _HTTPStatusCode;
            }
        }

        #endregion

        #region Response header fields

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

        #endregion

        #endregion

        #region Constructor(s)

        // remove me!
        public HTTPResponse()
        { }

        #region HTTPResponse(HTTPResponseHeader, HTTPRequest)

        /// <summary>
        /// Parse the given HTTP response header.
        /// </summary>
        /// <param name="HTTPResponseHeader">A string representation of a HTTP response header.</param>
        /// <param name="HTTPRequest">The HTTP request for this HTTP response.</param>
        public HTTPResponse(String       HTTPResponseHeader,
                            HTTPRequest  HTTPRequest)

            : base(HTTPResponseHeader)

        {

            this._HTTPRequest    = HTTPRequest;
            base.HTTPBodyStream  = new MemoryStream();

            #region Parse HTTP status code

            var _StatusCodeLine = FirstPDULine.Split(' ');

            if (_StatusCodeLine.Length < 3)
                throw new Exception("Bad request");

            this._HTTPStatusCode = HTTPStatusCode.ParseString(_StatusCodeLine[1]);

            #endregion

        }

        #endregion

        #region HTTPResponse(HTTPResponseHeader, HTTPResponseBody, HTTPRequest)

        /// <summary>
        /// Parse the given HTTP response header.
        /// </summary>
        /// <param name="HTTPResponseHeader">A string representation of a HTTP response header.</param>
        /// <param name="HTTPResponseBody">The response body as array of bytes.</param>
        /// <param name="HTTPRequest">The HTTP request for this HTTP response.</param>
        public HTTPResponse(String       HTTPResponseHeader,
                            Byte[]       HTTPResponseBody,
                            HTTPRequest  HTTPRequest)

            : this(HTTPResponseHeader, HTTPRequest)

        {

            this.HTTPBody      = HTTPResponseBody;

        }

        #endregion

        #region HTTPResponse(HTTPResponseHeader, HTTPResponseBodyStream, HTTPRequest)

        /// <summary>
        /// Parse the given HTTP response header.
        /// </summary>
        /// <param name="HTTPResponseHeader">A string representation of a HTTP response header.</param>
        /// <param name="HTTPResponseBodyStream">The response body as stream of bytes.</param>
        /// <param name="HTTPRequest">The HTTP request for this HTTP response.</param>
        public HTTPResponse(String       HTTPResponseHeader,
                            Stream       HTTPResponseBodyStream,
                            HTTPRequest  HTTPRequest)

            : this(HTTPResponseHeader, HTTPRequest)

        {

            base.HTTPBodyStream  = HTTPBodyStream;

        }

        #endregion

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {
            return HTTPStatusCode != null ? HTTPStatusCode.ToString() : "";
        }

        #endregion

    }

    #endregion

}
