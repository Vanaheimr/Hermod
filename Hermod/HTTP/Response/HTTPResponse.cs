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

    public static class XXXX
    {

        public static HTTPResponse<TResult> Parse<TResult>(this HTTPResponse      Response,
                                                           Func<Byte[], TResult>  ContentParser)
        {
            return new HTTPResponse<TResult>(Response, ContentParser(Response.HTTPBody));
        }

        public static HTTPResponse<TResult> Parse<TResult>(this HTTPResponse      Response,
                                                           Func<Stream, TResult>  ContentParser)
        {
            return new HTTPResponse<TResult>(Response, ContentParser(Response.HTTPBodyStream));
        }


        public static HTTPResponse<TResult> Parse<TResult, TInput>(this HTTPResponse<TInput>  Response,
                                                                   Func<TInput, TResult>      ContentParser)
        {
            return new HTTPResponse<TResult>(Response, ContentParser(Response.Content));
        }

        public static HTTPResponse<TResult> Parse<TResult, TInput>(this HTTPResponse<TInput>                   Response,
                                                                   Func<TInput, OnExceptionDelegate, TResult>  ContentParser,
                                                                   OnExceptionDelegate                         OnException = null)
        {
            return new HTTPResponse<TResult>(Response, ContentParser(Response.Content, OnException));
        }

    }


    #region HTTPResponse<TContent>

    /// <summary>
    /// A helper class to transport HTTP data and its metadata.
    /// </summary>
    /// <typeparam name="TContent">The type of the parsed data.</typeparam>
    public class HTTPResponse<TContent> : HTTPResponse
    {

        #region Data

        private readonly Boolean _IsFault;

        #endregion

        #region Properties

        #region Content

        private readonly TContent _Content;

        /// <summary>
        /// The parsed content.
        /// </summary>
        public TContent Content
        {
            get
            {
                return _Content;
            }
        }

        #endregion

        #region Exception

        private readonly Exception _Exception;

        /// <summary>
        /// An exception during parsing.
        /// </summary>
        public Exception Exception
        {
            get
            {
                return _Exception;
            }
        }

        #endregion

        #region HasErrors

        public Boolean HasErrors
        {
            get
            {
                return _Exception != null && !_IsFault;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPResponse(Response, Content, IsFault = false, Exception = null)

        public HTTPResponse(HTTPResponse  Response,
                            TContent      Content,
                            Boolean       IsFault    = false,
                            Exception     Exception  = null)

            : base(Response)

        {

            this._Content      = Content;
            this._IsFault      = IsFault;
            this._Exception    = Exception;

        }

        #endregion

        #region HTTPResponse(Response, IsFault)

        public HTTPResponse(HTTPResponse  Response,
                            Boolean       IsFault)

            : this(Response,
                   default(TContent),
                   IsFault)

        { }

        #endregion

        #region HTTPResponse(Response, Exception)

        public HTTPResponse(HTTPResponse  Response,
                            Exception     Exception)

            : this(Response,
                   default(TContent),
                   true,
                   Exception)

        { }

        #endregion


        #region HTTPResponse(Request, Content)

        private HTTPResponse(HTTPRequest  Request,
                             TContent     Content)

            : this(HTTPResponseBuilder.OK(Request), Content, false)

        { }

        #endregion

        #region HTTPResponse(Request, Exception)

        public HTTPResponse(HTTPRequest  Request,
                            Exception    Exception)

            : this(new HTTPResponseBuilder(Request) { HTTPStatusCode = HTTPStatusCode.BadRequest },
                   default(TContent),
                   true,
                   Exception)

        { }

        #endregion

        #endregion


        public static HTTPResponse<TContent> OK(HTTPRequest  HTTPRequest,
                                                TContent     Content)
        {
            return new HTTPResponse<TContent>(HTTPRequest, Content);
        }

        public static HTTPResponse<TContent> OK(TContent Content)
        {
            return new HTTPResponse<TContent>(null, Content);
        }

    }

    #endregion

    #region HTTPResponse

    /// <summary>
    /// A read-only HTTP response header.
    /// </summary>
    public class HTTPResponse : AHTTPPDU
    {

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

        #region First PDU line

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

        #endregion

        #region Standard response header fields

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

        #region Non-standard response header fields

        #endregion


        #region Constructor(s)

        #region HTTPResponse(Response)

        /// <summary>
        /// Create a new HTTP response based on the given HTTP response.
        /// </summary>
        /// <param name="Response">A HTTP response.</param>
        public HTTPResponse(HTTPResponse Response)

            : base(Response)

        {

            this._HTTPRequest     = Response.HTTPRequest;
            this._HTTPStatusCode  = Response.HTTPStatusCode;

        }

        #endregion

        #region (private) HTTPResponse(...)

        /// <summary>
        /// Parse the given HTTP response header.
        /// </summary>
        /// <param name="HTTPRequest">The HTTP request for this HTTP response.</param>
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="LocalSocket">The local TCP/IP socket.</param>
        /// <param name="HTTPHeader">A valid string representation of a http response header.</param>
        /// <param name="HTTPBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPBodyStream">The HTTP body as an stream of bytes.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP response processing.</param>
        private HTTPResponse(HTTPRequest         HTTPRequest,
                             IPSocket            RemoteSocket,
                             IPSocket            LocalSocket,
                             String              HTTPHeader,
                             Byte[]              HTTPBody           = null,
                             Stream              HTTPBodyStream     = null,
                             CancellationToken?  CancellationToken  = null)

            : base(RemoteSocket, LocalSocket, HTTPHeader, HTTPBody, HTTPBodyStream, CancellationToken)

        {

            this._HTTPRequest  = HTTPRequest;

            #region Parse HTTP status code

            var _StatusCodeLine = FirstPDULine.Split(' ');

            if (_StatusCodeLine.Length < 3)
                throw new Exception("Bad request");

            this._HTTPStatusCode = HTTPStatusCode.ParseString(_StatusCodeLine[1]);

            #endregion

        }

        #endregion

        #region HTTPResponse(HTTPResponseHeader, HTTPRequest)

        /// <summary>
        /// Parse the given HTTP response header.
        /// </summary>
        /// <param name="HTTPResponseHeader">A string representation of a HTTP response header.</param>
        /// <param name="HTTPRequest">The HTTP request for this HTTP response.</param>
        public HTTPResponse(String       HTTPResponseHeader,
                            HTTPRequest  HTTPRequest)

            : this(HTTPRequest, null, null, HTTPResponseHeader, null, new MemoryStream())

        {

            this._HTTPRequest  = HTTPRequest;

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
        /// <param name="HTTPResponseBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPRequest">The HTTP request for this HTTP response.</param>
        public HTTPResponse(String       HTTPResponseHeader,
                            Byte[]       HTTPResponseBody,
                            HTTPRequest  HTTPRequest)

            : this(HTTPRequest, null, null, HTTPResponseHeader, HTTPResponseBody)

        { }

        #endregion

        #region HTTPResponse(HTTPResponseHeader, HTTPResponseBodyStream, HTTPRequest)

        /// <summary>
        /// Parse the given HTTP response header.
        /// </summary>
        /// <param name="HTTPResponseHeader">A string representation of a HTTP response header.</param>
        /// <param name="HTTPResponseBodyStream">The HTTP body as an stream of bytes.</param>
        /// <param name="HTTPRequest">The HTTP request for this HTTP response.</param>
        public HTTPResponse(String       HTTPResponseHeader,
                            Stream       HTTPResponseBodyStream,
                            HTTPRequest  HTTPRequest)

            : this(HTTPRequest, null, null, HTTPResponseHeader, null, HTTPResponseBodyStream)

        { }

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
