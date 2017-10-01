/*
 * Copyright (c) 2010-2017, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class HTTPContentHelper
    {

        public static HTTPResponse<TResult> ParseContent<TResult>(this HTTPResponse      Response,
                                                                  Func<Byte[], TResult>  ContentParser)

            => new HTTPResponse<TResult>(Response, ContentParser(Response.HTTPBody));


        public static HTTPResponse<TResult> ParseContentStream<TResult>(this HTTPResponse      Response,
                                                                        Func<Stream, TResult>  ContentParser)

            => new HTTPResponse<TResult>(Response, ContentParser(Response.HTTPBodyStream));

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

        /// <summary>
        /// The parsed content.
        /// </summary>
        public TContent   Content    { get; }

        /// <summary>
        /// An exception during parsing.
        /// </summary>
        public Exception  Exception  { get; }

        /// <summary>
        /// An error during parsing.
        /// </summary>
        public Boolean HasErrors
            => Exception != null && !_IsFault;

        #endregion

        #region Constructor(s)

        #region HTTPResponse(Response, Content, IsFault = false, NumberOfTransmissionRetries = 0, Exception = null)

        public HTTPResponse(HTTPResponse  Response,
                            TContent      Content,
                            Boolean       IsFault    = false,
                            Exception     Exception  = null)

            : base(Response)

        {

            this.Content    = Content;
            this._IsFault   = IsFault;
            this.Exception  = Exception;

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


        #region (private) HTTPResponse(Content, Exception)

        private HTTPResponse(TContent   Content,
                             Exception  Exception)

            : base(null)

        {

            this.Content    = Content;
            this._IsFault   = true;
            this.Exception  = Exception;

        }

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


        #region ConvertContent<TResult>(ContentConverter)

        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        public HTTPResponse<TResult> ConvertContent<TResult>(Func<TContent, TResult> ContentConverter)
        {

            if (ContentConverter == null)
                throw new ArgumentNullException(nameof(ContentConverter),  "The given content converter delegate must not be null!");

            return new HTTPResponse<TResult>(this, ContentConverter(this.Content));

        }

        #endregion

        #region ConvertContent<TResult>(ContentConverter, OnException = null)

        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPResponse<TResult> ConvertContent<TResult>(Func<TContent, OnExceptionDelegate, TResult>  ContentConverter,
                                                             OnExceptionDelegate                           OnException = null)
        {

            if (ContentConverter == null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPResponse<TResult>(this, ContentConverter(this.Content, OnException));

        }

        #endregion


        #region ConvertContent<TRequest, TResult>(Request, ContentConverter, OnException = null)

        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TRequest">The type of the converted HTTP request body content.</typeparam>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="Request">The request leading to this response.</param>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPResponse<TResult> ConvertContent<TRequest, TResult>(TRequest                                                Request,
                                                                       Func<TRequest, TContent, OnExceptionDelegate, TResult>  ContentConverter,
                                                                       OnExceptionDelegate                                     OnException  = null)
        {

            if (ContentConverter == null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPResponse<TResult>(this,
                                             ContentConverter(Request,
                                                              Content,
                                                              OnException));

        }

        #endregion



        public static HTTPResponse<TContent> OK(HTTPRequest  HTTPRequest,
                                                TContent     Content)

            => new HTTPResponse<TContent>(HTTPRequest, Content);

        public static HTTPResponse<TContent> OK(TContent Content)

            => new HTTPResponse<TContent>(null, Content);

        public static HTTPResponse<TContent> ClientError(TContent Content)

            => new HTTPResponse<TContent>(null, Content, IsFault: true);

        public static HTTPResponse<TContent> ExceptionThrown(TContent   Content,
                                                             Exception  Exception)

            => new HTTPResponse<TContent>(Content, Exception);


        #region (static) GatewayTimeout

        public static HTTPResponse<TContent> GatewayTimeout(TContent Content)
            => new HTTPResponse<TContent>(new HTTPResponseBuilder(null, HTTPStatusCode.GatewayTimeout), Content);

        #endregion


    }

    #endregion

    #region HTTPResponse

    /// <summary>
    /// A read-only HTTP response header.
    /// </summary>
    public class HTTPResponse : AHTTPPDU
    {

        #region HTTPRequest

        /// <summary>
        /// The HTTP request for this HTTP response.
        /// </summary>
        public HTTPRequest     HTTPRequest       { get; }

        /// <summary>
        /// The HTTP status code.
        /// </summary>
        public HTTPStatusCode  HTTPStatusCode    { get; }

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

        /// <summary>
        /// The runtime of the HTTP request/response pair.
        /// </summary>
        public TimeSpan? Runtime            { get; }

        /// <summary>
        /// The number of retransmissions of this request.
        /// </summary>
        public Byte      NumberOfRetries    { get; }

        #endregion


        #region Constructor(s)

        #region (internal) HTTPResponse(Response)

        /// <summary>
        /// Create a new HTTP response based on the given HTTP response.
        /// (e.g. upgrade a HTTPResponse to a HTTPResponse&lt;TContent&gt;)
        /// </summary>
        /// <param name="Response">A HTTP response.</param>
        internal HTTPResponse(HTTPResponse Response)

            : base(Response)

        {

            this.HTTPRequest     = Response?.HTTPRequest;
            this.HTTPStatusCode  = Response?.HTTPStatusCode;
            this.Runtime         = Response.Runtime;

        }

        #endregion

        #region (private) HTTPResponse(...)

        /// <summary>
        /// Parse the given HTTP response header.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the response.</param>
        /// <param name="HTTPRequest">The HTTP request for this HTTP response.</param>
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="LocalSocket">The local TCP/IP socket.</param>
        /// <param name="HTTPHeader">A valid string representation of a http response header.</param>
        /// <param name="HTTPBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPBodyStream">The HTTP body as an stream of bytes.</param>
        /// <param name="HTTPBodyReceiveBufferSize">The size of the HTTP body receive buffer.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP response processing.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="Runtime">The runtime of the HTTP request/response pair.</param>
        /// <param name="NumberOfRetries">The number of retransmissions of this request.</param>
        private HTTPResponse(DateTime            Timestamp,
                             IPSocket            RemoteSocket,
                             IPSocket            LocalSocket,
                             HTTPRequest         HTTPRequest,
                             String              HTTPHeader,
                             Byte[]              HTTPBody                    = null,
                             Stream              HTTPBodyStream              = null,
                             UInt32              HTTPBodyReceiveBufferSize   = DefaultHTTPBodyReceiveBufferSize,
                             CancellationToken?  CancellationToken           = null,
                             EventTracking_Id    EventTrackingId             = null,
                             TimeSpan?           Runtime                     = null,
                             Byte                NumberOfRetries             = 0)

            : base(Timestamp,
                   RemoteSocket,
                   LocalSocket,
                   HTTPHeader,
                   HTTPBody,
                   HTTPBodyStream,
                   HTTPBodyReceiveBufferSize,
                   CancellationToken,
                   EventTrackingId)

        {

            this.HTTPRequest      = HTTPRequest;

            var _StatusCodeLine   = FirstPDULine.Split(' ');
            if (_StatusCodeLine.Length < 3)
                throw new Exception("Invalid HTTP response!");

            this.HTTPStatusCode   = HTTPStatusCode.ParseString(_StatusCodeLine[1]);
            this.Runtime          = Runtime ?? DateTime.UtcNow - HTTPRequest.Timestamp;
            this.NumberOfRetries  = NumberOfRetries;

        }

        #endregion


        // Parse the HTTP response from its text-representation...

        #region (private) HTTPResponse(ResponseHeader, Request)

        /// <summary>
        /// Create a new HTTP response.
        /// </summary>
        /// <param name="ResponseHeader">The HTTP header of the response.</param>
        /// <param name="Request">The HTTP request leading to this response.</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        private HTTPResponse(String       ResponseHeader,
                             HTTPRequest  Request,
                             Byte         NumberOfRetry = 0)

            : this(DateTime.UtcNow,
                   null,
                   null,
                   Request,
                   ResponseHeader,
                   null,
                   new MemoryStream(),
                   DefaultHTTPBodyReceiveBufferSize,
                   Request?.CancellationToken,
                   Request?.EventTrackingId,
                   DateTime.UtcNow - Request.Timestamp,
                   NumberOfRetry)

        {

            this.HTTPRequest     = Request;

            var _StatusCodeLine  = FirstPDULine.Split(' ');
            if (_StatusCodeLine.Length < 3)
                throw new Exception("Invalid HTTP response!");

            this.HTTPStatusCode  = HTTPStatusCode.ParseString(_StatusCodeLine[1]);

        }

        #endregion

        #region (private) HTTPResponse(ResponseHeader, ResponseBody, Request)

        /// <summary>
        /// Create a new HTTP response.
        /// </summary>
        /// <param name="ResponseHeader">The HTTP header of the response.</param>
        /// <param name="ResponseBody">The HTTP body of the response.</param>
        /// <param name="Request">The HTTP request leading to this response.</param>
        private HTTPResponse(String       ResponseHeader,
                             Byte[]       ResponseBody,
                             HTTPRequest  Request)

            : this(DateTime.UtcNow,
                   null,
                   null,
                   Request,
                   ResponseHeader,
                   ResponseBody,
                   null,
                   DefaultHTTPBodyReceiveBufferSize,
                   Request?.CancellationToken,
                   Request?.EventTrackingId,
                   DateTime.UtcNow - Request.Timestamp)

        { }

        #endregion

        #region (private) HTTPResponse(ResponseHeader, ResponseBodyStream, Request, HTTPBodyReceiveBufferSize = default)

        /// <summary>
        /// Create a new HTTP response.
        /// </summary>
        /// <param name="ResponseHeader">The HTTP header of the response.</param>
        /// <param name="ResponseBodyStream">The HTTP body of the response.</param>
        /// <param name="Request">The HTTP request leading to this response.</param>
        /// <param name="HTTPBodyReceiveBufferSize">The size of the HTTP body receive buffer.</param>
        private HTTPResponse(String       ResponseHeader,
                             Stream       ResponseBodyStream,
                             HTTPRequest  Request,
                             UInt32       HTTPBodyReceiveBufferSize  = DefaultHTTPBodyReceiveBufferSize)

            : this(DateTime.UtcNow,
                   null,
                   null,
                   Request,
                   ResponseHeader,
                   null,
                   ResponseBodyStream,
                   HTTPBodyReceiveBufferSize,
                   Request?.CancellationToken,
                   Request?.EventTrackingId,
                   DateTime.UtcNow - Request.Timestamp)

        { }

        #endregion

        #endregion


        #region (static) Parse(HTTPResponseHeader, HTTPRequest)

        /// <summary>
        /// Parse the HTTP response from its text-representation.
        /// </summary>
        /// <param name="HTTPResponseHeader">The HTTP header of the response.</param>
        /// <param name="HTTPRequest">The HTTP request leading to this response.</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        public static HTTPResponse Parse(String       HTTPResponseHeader,
                                         HTTPRequest  HTTPRequest,
                                         Byte         NumberOfRetry = 0)

            => new HTTPResponse(HTTPResponseHeader,
                                HTTPRequest,
                                NumberOfRetry);

        #endregion

        #region (static) Parse(HTTPResponseHeader, HTTPResponseBody, HTTPRequest)

        /// <summary>
        /// Parse the HTTP response from its text-representation and
        /// attach the given HTTP body.
        /// </summary>
        /// <param name="HTTPResponseHeader">The HTTP header of the response.</param>
        /// <param name="HTTPResponseBody">The HTTP body of the response.</param>
        /// <param name="HTTPRequest">The HTTP request leading to this response.</param>
        public static HTTPResponse Parse(String       HTTPResponseHeader,
                                         Byte[]       HTTPResponseBody,
                                         HTTPRequest  HTTPRequest)

            => new HTTPResponse(HTTPResponseHeader,
                                HTTPResponseBody,
                                HTTPRequest);

        /// <summary>
        /// Parse the HTTP response from its text-representation and
        /// attach the given HTTP body.
        /// </summary>
        /// <param name="HTTPResponseHeader">The HTTP header of the response.</param>
        /// <param name="HTTPResponseBody">The HTTP body of the response.</param>
        /// <param name="HTTPRequest">The HTTP request leading to this response.</param>
        public static HTTPResponse Parse(String       HTTPResponseHeader,
                                         Stream       HTTPResponseBody,
                                         HTTPRequest  HTTPRequest)

            => new HTTPResponse(HTTPResponseHeader,
                                HTTPResponseBody,
                                HTTPRequest);

        #endregion

        #region (static) Parse(Text, Timestamp = null, RemoteSocket = null, LocalSocket = null, EventTrackingId = null)

        /// <summary>
        /// Parse the given text as a HTTP response.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP response.</param>
        /// <param name="Timestamp">The optional timestamp of the response.</param>
        /// <param name="RemoteSocket">The optional remote TCP socket of the response.</param>
        /// <param name="LocalSocket">The optional local TCP socket of the response.</param>
        /// <param name="EventTrackingId">The optional event tracking identification of the response.</param>
        /// <param name="Request">The HTTP request for this HTTP response.</param>
        public static HTTPResponse Parse(IEnumerable<String>  Text,
                                         DateTime?            Timestamp        = null,
                                         IPSocket             RemoteSocket     = null,
                                         IPSocket             LocalSocket      = null,
                                         EventTracking_Id     EventTrackingId  = null,
                                         HTTPRequest          Request          = null)
        {

                Timestamp  = Timestamp ?? DateTime.UtcNow;
            var Header     = Text.TakeWhile(line => line != "").AggregateWith("\r\n");
            var Body       = Text.SkipWhile(line => line != "").Skip(1).AggregateWith("\r\n");

            return new HTTPResponse(Timestamp ?? DateTime.UtcNow,
                                    RemoteSocket,
                                    LocalSocket,
                                    Request,
                                    Header,
                                    Body.ToUTF8Bytes(),
                                    EventTrackingId:  EventTrackingId,
                                    Runtime:          Timestamp - Request.Timestamp);

        }

        #endregion


        #region (static) LoadHTTPResponseLogfiles(FilePath, FilePattern, FromTimestamp = null, ToTimestamp = null)

        public static IEnumerable<HTTPResponse> LoadHTTPResponseLogfiles(String     FilePath,
                                                                         String     FilePattern,
                                                                         DateTime?  FromTimestamp  = null,
                                                                         DateTime?  ToTimestamp    = null)
        {

            var _responses  = new ConcurrentBag<HTTPResponse>();

            Parallel.ForEach(Directory.EnumerateFiles(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + FilePath,
                                                      FilePattern),
                             file => {

                var _request            = new List<String>();
                var _response           = new List<String>();
                var copy                = "none";
                var relativelinenumber  = 0;
                var RequestTimestamp    = DateTime.Now;
                var ResponseTimestamp   = DateTime.Now;

                foreach (var line in File.ReadLines(file))
                {

                    try
                    {

                        if      (relativelinenumber == 1 && copy == "request")
                            RequestTimestamp  = DateTime.Parse(line);

                        else if (relativelinenumber == 1 && copy == "response")
                            ResponseTimestamp = DateTime.Parse(line.Substring(0, line.IndexOf(" ")));

                        else if (line == ">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------")
                        {
                            copy = "request";
                            relativelinenumber = 0;
                        }

                        else if (line == "<<<<<<--Response----<<<<<<------<<<<<<------<<<<<<------<<<<<<------<<<<<<------")
                        {
                            copy = "response";
                            relativelinenumber = 0;
                        }

                        else if (line == "--------------------------------------------------------------------------------")
                        {

                            if ((FromTimestamp == null || ResponseTimestamp >= FromTimestamp.Value) &&
                                (  ToTimestamp == null || ResponseTimestamp <    ToTimestamp.Value))
                            {

                                _responses.Add(HTTPResponse.Parse(_response,
                                                                  Timestamp: ResponseTimestamp,
                                                                  Request:   HTTPRequest.Parse(_request, Timestamp: RequestTimestamp)));

                            }

                            copy       = "none";
                            _request   = new List<String>();
                            _response  = new List<String>();

                        }

                        else if (copy == "request")
                            _request.Add(line);

                        else if (copy == "response")
                            _response.Add(line);

                        relativelinenumber++;

                    }
                    catch (Exception)
                    {
                    }

                }

            });

            return _responses.OrderBy(response => response.Timestamp);

        }

        #endregion


        #region (static) BadRequest

        public static HTTPResponse BadRequest
            => new HTTPResponse(new HTTPResponseBuilder(null, HTTPStatusCode.BadRequest));

        #endregion

        #region (static) ServiceUnavailable

        public static HTTPResponse ServiceUnavailable
            => new HTTPResponse(new HTTPResponseBuilder(null, HTTPStatusCode.ServiceUnavailable));

        #endregion

        #region (static) GatewayTimeout

        public static HTTPResponse GatewayTimeout
            => new HTTPResponse(new HTTPResponseBuilder(null, HTTPStatusCode.GatewayTimeout));

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {
            return EntirePDU;
        }

        #endregion

    }

    #endregion

}
