/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Text;
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


        public static void AppendLogfile(this HTTPResponse  Response,
                                         String             Logfilename)
        {

            using (var Logfile = File.AppendText(Logfilename))
            {

                Logfile.WriteLine(
                    String.Concat(HTTPResponse.RequestMarker,                     Environment.NewLine,
                                  Response.HTTPRequest.HTTPSource.ToString(),   Environment.NewLine,
                                  Response.HTTPRequest.Timestamp.   ToIso8601(),  Environment.NewLine,
                                  Response.HTTPRequest.EventTrackingId,           Environment.NewLine,
                                  Response.HTTPRequest.EntirePDU,                 Environment.NewLine,
                                  HTTPResponse.ResponseMarker,                    Environment.NewLine,
                                  Response.Timestamp.               ToIso8601(),  Environment.NewLine,
                                  Response.EntirePDU,                             Environment.NewLine,
                                  HTTPResponse.EndMarker,                         Environment.NewLine));

            }

        }



        #region Reply(this HTTPRequest)

        /// <summary>
        /// Create a new HTTP response builder for the given request.
        /// </summary>
        /// <param name="HTTPRequest">A HTTP request.</param>
        public static HTTPResponse.Builder Reply(this HTTPRequest HTTPRequest)

            => new HTTPResponse.Builder(HTTPRequest);

        #endregion

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

            : this(Builder.OK(Request), Content, false)

        { }

        #endregion

        #region HTTPResponse(Request, Exception)

        public HTTPResponse(HTTPRequest  Request,
                            Exception    Exception)

            : this(new Builder(Request) { HTTPStatusCode = HTTPStatusCode.BadRequest },
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
            => new HTTPResponse<TContent>(new Builder(null, HTTPStatusCode.GatewayTimeout), Content);

        #endregion


    }

    #endregion

    #region HTTPResponse

    /// <summary>
    /// A read-only HTTP response header.
    /// </summary>
    public class HTTPResponse : AHTTPPDU
    {

        #region Data

        public const String RequestMarker   = "<<< Request <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<";
        public const String ResponseMarker  = ">>> Response >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>";
        public const String EndMarker       = "======================================================================================";

        #endregion

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

        #region (private)  HTTPResponse(...)

        /// <summary>
        /// Parse the given HTTP response header.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the response.</param>
        /// <param name="HTTPRequest">The HTTP request for this HTTP response.</param>
        /// <param name="HTTPSource">The remote TCP/IP socket.</param>
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
                             HTTPSource          HTTPSource,
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
                   HTTPSource,
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


        // Parse the HTTP response from its text-representation...

        #region (private) HTTPResponse(ResponseHeader, Request, HTTPBody = null, NumberOfRetry = 0)

        /// <summary>
        /// Create a new HTTP response.
        /// </summary>
        /// <param name="ResponseHeader">The HTTP header of the response.</param>
        /// <param name="Request">The HTTP request leading to this response.</param>
        /// <param name="HTTPBody">A HTTP body.</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        private HTTPResponse(String       ResponseHeader,
                             HTTPRequest  Request,
                             Byte[]       HTTPBody       = null,
                             Byte         NumberOfRetry  = 0)

            : this(DateTime.UtcNow,
                   new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                   IPSocket.LocalhostV4(IPPort.HTTPS),
                   Request,
                   ResponseHeader,
                   HTTPBody,
                   new MemoryStream(),
                   DefaultHTTPBodyReceiveBufferSize,
                   Request?.CancellationToken,
                   Request?.EventTrackingId,
                   Request != null ? DateTime.UtcNow - Request.Timestamp : TimeSpan.Zero,
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
                   new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                   IPSocket.LocalhostV4(IPPort.HTTPS),
                   Request,
                   ResponseHeader,
                   ResponseBody,
                   null,
                   DefaultHTTPBodyReceiveBufferSize,
                   Request?.CancellationToken,
                   Request?.EventTrackingId,
                   Request != null ? DateTime.UtcNow - Request.Timestamp : TimeSpan.Zero)

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
                   new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                   IPSocket.LocalhostV4(IPPort.HTTPS),
                   Request,
                   ResponseHeader,
                   null,
                   ResponseBodyStream,
                   HTTPBodyReceiveBufferSize,
                   Request?.CancellationToken,
                   Request?.EventTrackingId,
                   Request != null ? DateTime.UtcNow - Request.Timestamp : TimeSpan.Zero)

        { }

        #endregion

        #endregion


        #region (static) Parse(HTTPResponseHeader, HTTPRequest, HTTPBody = null, NumberOfRetry = 0, )

        /// <summary>
        /// Parse the HTTP response from its text-representation.
        /// </summary>
        /// <param name="HTTPResponseHeader">The HTTP header of the response.</param>
        /// <param name="HTTPRequest">The HTTP request leading to this response.</param>
        /// <param name="HTTPBody">An optional HTTP body.</param>
        /// <param name="NumberOfRetry">The number of retransmissions of this request.</param>
        public static HTTPResponse Parse(String       HTTPResponseHeader,
                                         HTTPRequest  HTTPRequest,
                                         Byte[]       HTTPBody       = null,
                                         Byte         NumberOfRetry  = 0)

            => new HTTPResponse(HTTPResponseHeader,
                                HTTPRequest,
                                HTTPBody,
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

        #region (static) Parse(Text, Timestamp = null, HTTPSource = null, LocalSocket = null, EventTrackingId = null)

        /// <summary>
        /// Parse the given text as a HTTP response.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP response.</param>
        /// <param name="Timestamp">The optional timestamp of the response.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the response.</param>
        /// <param name="LocalSocket">The optional local TCP socket of the response.</param>
        /// <param name="EventTrackingId">The optional event tracking identification of the response.</param>
        /// <param name="Request">The HTTP request for this HTTP response.</param>
        public static HTTPResponse Parse(IEnumerable<String>  Text,
                                         DateTime?            Timestamp        = null,
                                         HTTPSource?          HTTPSource       = null,
                                         IPSocket?            LocalSocket      = null,
                                         EventTracking_Id     EventTrackingId  = null,
                                         HTTPRequest          Request          = null)
        {

                Timestamp  = Timestamp ?? DateTime.UtcNow;
            var Header     = Text.TakeWhile(line => line != "").AggregateWith("\r\n");
            var Body       = Text.SkipWhile(line => line != "").Skip(1).AggregateWith("\r\n");

            return new HTTPResponse(Timestamp   ?? DateTime.UtcNow,
                                    HTTPSource  ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                    LocalSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                    Request,
                                    Header,
                                    Body.ToUTF8Bytes(),
                                    EventTrackingId:  EventTrackingId,
                                    Runtime:          Request != null ? Timestamp - Request.Timestamp : TimeSpan.Zero);

        }

        #endregion


        #region (static) LoadHTTPResponseLogfiles_old(FilePath, FilePattern, SearchOption = TopDirectoryOnly, FromTimestamp = null, ToTimestamp = null)

        public static IEnumerable<HTTPResponse> LoadHTTPResponseLogfiles_old(String        FilePath,
                                                                             String        FilePattern,
                                                                             DateTime?     FromTimestamp  = null,
                                                                             DateTime?     ToTimestamp    = null)

            => LoadHTTPResponseLogfiles_old(FilePath,
                                            FilePattern,
                                            SearchOption.TopDirectoryOnly,
                                            FromTimestamp,
                                            ToTimestamp);


        public static IEnumerable<HTTPResponse> LoadHTTPResponseLogfiles_old(String        FilePath,
                                                                             String        FilePattern,
                                                                             SearchOption  SearchOption   = SearchOption.TopDirectoryOnly,
                                                                             DateTime?     FromTimestamp  = null,
                                                                             DateTime?     ToTimestamp    = null)
        {

            var _responses  = new ConcurrentBag<HTTPResponse>();

            Parallel.ForEach(Directory.EnumerateFiles(FilePath,
                                                      FilePattern,
                                                      SearchOption),
                             new ParallelOptions() { MaxDegreeOfParallelism = 1 },
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
                            RequestTimestamp  = DateTime.SpecifyKind(DateTime.Parse(line), DateTimeKind.Utc);

                        else if (relativelinenumber == 1 && copy == "response")
                            ResponseTimestamp = DateTime.SpecifyKind(DateTime.Parse(line.Substring(0, line.IndexOf(" "))), DateTimeKind.Utc);

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

                                _responses.Add(Parse(_response,
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

        #region (static) LoadHTTPResponseLogfiles(FilePath, FilePattern, SearchOption = TopDirectoryOnly, FromTimestamp = null, ToTimestamp = null)

        public static IEnumerable<HTTPResponse> LoadHTTPResponseLogfiles(String        FilePath,
                                                                         String        FilePattern,
                                                                         DateTime?     FromTimestamp  = null,
                                                                         DateTime?     ToTimestamp    = null)

            => LoadHTTPResponseLogfiles(FilePath,
                                        FilePattern,
                                        SearchOption.TopDirectoryOnly,
                                        FromTimestamp,
                                        ToTimestamp);


        public static IEnumerable<HTTPResponse> LoadHTTPResponseLogfiles(String        FilePath,
                                                                         String        FilePattern,
                                                                         SearchOption  SearchOption   = SearchOption.TopDirectoryOnly,
                                                                         DateTime?     FromTimestamp  = null,
                                                                         DateTime?     ToTimestamp    = null)
        {

            var _responses  = new ConcurrentBag<HTTPResponse>();

            Parallel.ForEach(Directory.EnumerateFiles(FilePath,
                                                      FilePattern,
                                                      SearchOption),
                             new ParallelOptions() { MaxDegreeOfParallelism = 1 },
                             file => {

                var _request            = new List<String>();
                var _response           = new List<String>();
                var copy                = "none";
                var relativelinenumber  = 0;
                var HTTPSource          = new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS));
                var RequestTimestamp    = DateTime.Now;
                var ResponseTimestamp   = DateTime.Now;

                foreach (var line in File.ReadLines(file))
                {

                    try
                    {

                        if      (relativelinenumber == 1 && copy == "request")
                            HTTPSource        = new HTTPSource(IPSocket.Parse(line));

                        else if (relativelinenumber == 2 && copy == "request")
                            RequestTimestamp  = DateTime.SpecifyKind(DateTime.Parse(line), DateTimeKind.Utc);

                        else if (relativelinenumber == 1 && copy == "response")
                            ResponseTimestamp = DateTime.SpecifyKind(DateTime.Parse(line), DateTimeKind.Utc);//.Substring(0, line.IndexOf(" ")));

                        else if (line == RequestMarker)//">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------")
                        {
                            copy = "request";
                            relativelinenumber = 0;
                        }

                        else if (line == ResponseMarker)// "<<<<<<--Response----<<<<<<------<<<<<<------<<<<<<------<<<<<<------<<<<<<------")
                        {
                            copy = "response";
                            relativelinenumber = 0;
                        }

                        else if (line == EndMarker)// "--------------------------------------------------------------------------------")
                        {

                            if ((FromTimestamp == null || ResponseTimestamp >= FromTimestamp.Value) &&
                                (  ToTimestamp == null || ResponseTimestamp <    ToTimestamp.Value))
                            {

                                _responses.Add(Parse(_response,
                                                     ResponseTimestamp,
                                                     HTTPSource,
                                                     Request: HTTPRequest.Parse(_request.Skip(1),
                                                                                RequestTimestamp,
                                                                                HTTPSource,
                                                                                EventTrackingId: EventTracking_Id.Parse(_request[0]))));

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
                    catch (Exception e)
                    {
                    }

                }

            });

            return _responses.OrderBy(response => response.Timestamp);

        }

        #endregion


        #region (static) OK                (Request, Configurator = null)

        /// <summary>
        /// Create a new 200-OK HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse OK(HTTPRequest      Request,
                                      Action<Builder>  Configurator = null)

            => Builder.OK(Request, Configurator);


        /// <summary>
        /// Create a new 200-OK HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse OK(HTTPRequest             Request,
                                      Func<Builder, Builder>  Configurator)

            => Builder.OK(Request, Configurator);

        #endregion

        #region (static) BadRequest        (Request, Configurator = null)

        /// <summary>
        /// Create a new 400-BadRequest HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse BadRequest(HTTPRequest      Request,
                                              Action<Builder>  Configurator = null)

            => Builder.BadRequest(Request, Configurator);


        /// <summary>
        /// Create a new 400-BadRequest HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse BadRequest(HTTPRequest             Request,
                                              Func<Builder, Builder>  Configurator)

            => Builder.BadRequest(Request, Configurator);

        #endregion

        #region (static) ServiceUnavailable(Request, Configurator = null)

        /// <summary>
        /// Create a new 503-ServiceUnavailable HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse ServiceUnavailable(HTTPRequest      Request,
                                                      Action<Builder>  Configurator = null)

            => Builder.ServiceUnavailable(Request, Configurator);


        /// <summary>
        /// Create a new 503-ServiceUnavailable HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse ServiceUnavailable(HTTPRequest             Request,
                                                      Func<Builder, Builder>  Configurator)

            => Builder.ServiceUnavailable(Request, Configurator);

        #endregion

        #region (static) FailedDependency  (Request, Configurator = null)

        /// <summary>
        /// Create a new 424-FailedDependency HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder FailedDependency(HTTPRequest      Request,
                                               Action<Builder>  Configurator  = null)

            => Builder.ServiceUnavailable(Request, Configurator);

        /// <summary>
        /// Create a new 424-FailedDependency HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder FailedDependency(HTTPRequest             Request,
                                               Func<Builder, Builder>  Configurator)

            => Builder.ServiceUnavailable(Request, Configurator);

        #endregion

        #region (static) GatewayTimeout    (Request, Configurator = null)

        /// <summary>
        /// Create a new 504-GatewayTimeout HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse GatewayTimeout(HTTPRequest      Request,
                                                  Action<Builder>  Configurator = null)

            => Builder.GatewayTimeout(Request, Configurator);


        /// <summary>
        /// Create a new 504-GatewayTimeout HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse GatewayTimeout(HTTPRequest             Request,
                                                  Func<Builder, Builder>  Configurator)

            => Builder.GatewayTimeout(Request, Configurator);

        #endregion

        #region (static) ClientError       (Request, Configurator = null)

        /// <summary>
        /// Create a new 0-ClientError HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse ClientError(HTTPRequest      Request,
                                               Action<Builder>  Configurator = null)

            => Builder.ClientError(Request, Configurator);

        /// <summary>
        /// Create a new 0-ClientError HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static HTTPResponse ClientError(HTTPRequest             Request,
                                               Func<Builder, Builder>  Configurator)

            => Builder.ClientError(Request, Configurator);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
        {
            return EntirePDU;
        }

        #endregion


        #region (class) Builder

        /// <summary>
        /// A read-write HTTP response header.
        /// </summary>
        public class Builder : AHTTPPDUBuilder
        {

            #region Properties

            /// <summary>
            /// The correlated HTTP request.
            /// </summary>
            public HTTPRequest        HTTPRequest          { get; }

            /// <summary>
            /// The timestamp of the HTTP response.
            /// </summary>
            public DateTime           Timestamp            { get; }

            /// <summary>
            /// The cancellation token.
            /// </summary>
            public CancellationToken  CancellationToken    { get; set; }

            /// <summary>
            /// The runtime of the HTTP request/response pair.
            /// </summary>
            public TimeSpan?          Runtime              { get; }

            /// <summary>
            /// The entire HTTP header.
            /// </summary>
            public String             HTTPHeader

                => HTTPStatusCode.HTTPResponseString + Environment.NewLine +
                         ConstructedHTTPHeader       + Environment.NewLine +
                         Environment.NewLine;

            #endregion

            #region Response header fields

            #region Age

            public UInt64? Age
            {

                get
                {
                    return GetHeaderField_UInt64(HTTPHeaderField.Age);
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.Age, value);
                }

            }

            #endregion

            #region Allow

            public List<HTTPMethod> Allow
            {

                get
                {
                    return GetHeaderField<List<HTTPMethod>>(HTTPHeaderField.Age);
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.Allow, value);
                }

            }

            #endregion

            #region DAV

            public String DAV
            {

                get
                {
                    return GetHeaderField(HTTPHeaderField.Age);
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.DAV, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.ETag, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Expires, value);
                }

            }

            #endregion

            #region KeepAlive

            public KeepAliveType KeepAlive
            {

                get
                {
                    return new KeepAliveType(GetHeaderField(HTTPHeaderField.KeepAlive));
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.KeepAlive, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.LastModified, value);
                }

            }

            #endregion

            #region Location

            public HTTPURI Location
            {

                get
                {
                    return HTTPURI.Parse(GetHeaderField(HTTPHeaderField.Location));
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.Location, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.ProxyAuthenticate, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.RetryAfter, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Server, value);
                }

            }

            #endregion

            #region SetCookie

            public String SetCookie
            {

                get
                {
                    return GetHeaderField(HTTPHeaderField.SetCookie);
                }

                set
                {
                    SetHeaderField(HTTPHeaderField.SetCookie, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.Vary, value);
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

                set
                {
                    SetHeaderField(HTTPHeaderField.WWWAuthenticate, value);
                }

            }

            #endregion

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new HTTP response.
            /// </summary>
            /// <param name="HTTPStatusCode">A HTTP status code</param>
            public Builder(HTTPStatusCode  HTTPStatusCode = null)
            {

                this._HTTPStatusCode    = HTTPStatusCode;
                this.Timestamp          = DateTime.UtcNow;
                this.ProtocolName       = "HTTP";
                this.ProtocolVersion    = new HTTPVersion(1, 1);
                this.CancellationToken  = new CancellationTokenSource().Token;
                base.EventTrackingId    = EventTracking_Id.New;
                this.Runtime            = TimeSpan.Zero;

            }

            /// <summary>
            /// Create a new HTTP response.
            /// </summary>
            /// <param name="HTTPRequest">The HTTP request for this response.</param>
            /// <param name="HTTPStatusCode">A HTTP status code</param>
            public Builder(HTTPRequest     HTTPRequest,
                           HTTPStatusCode  HTTPStatusCode = null)
            {

                this.HTTPRequest        = HTTPRequest;
                this._HTTPStatusCode    = HTTPStatusCode;
                this.Timestamp          = DateTime.UtcNow;
                this.ProtocolName       = "HTTP";
                this.ProtocolVersion    = new HTTPVersion(1, 1);
                this.CancellationToken  = HTTPRequest?.CancellationToken ?? new CancellationTokenSource().Token;
                base.EventTrackingId    = HTTPRequest?.EventTrackingId   ?? EventTracking_Id.New;
                this.Runtime            = HTTPRequest != null
                                              ? DateTime.UtcNow - HTTPRequest.Timestamp
                                              : TimeSpan.Zero;

            }

            #endregion


            #region Set(HeaderField, Value)

            /// <summary>
            /// Set a HTTP header field.
            /// A field value of NULL will remove the field from the header.
            /// </summary>
            /// <param name="HeaderField">The header field.</param>
            /// <param name="Value">The value. NULL will remove the field from the header.</param>
            public Builder Set(HTTPHeaderField HeaderField, Object Value)
            {

                base.SetHeaderField(HeaderField, Value);

                return this;

            }

            #endregion


            #region (implicit operator) HTTPResponseBuilder => HTTPResponse

            /// <summary>
            /// An implicit conversion of a HTTPResponseBuilder into a HTTPResponse.
            /// </summary>
            /// <param name="HTTPResponseBuilder">A HTTP response builder.</param>
            public static implicit operator HTTPResponse(Builder HTTPResponseBuilder)
                => HTTPResponseBuilder.AsImmutable;

            #endregion


            #region Set non-http header fields

            #region SetHTTPStatusCode(HTTPStatusCode)

            /// <summary>
            /// Set the HTTP status code.
            /// </summary>
            /// <param name="HTTPStatusCode">A HTTP status code.</param>
            public Builder SetHTTPStatusCode(HTTPStatusCode HTTPStatusCode)
            {
                this.HTTPStatusCode = HTTPStatusCode;
                return this;
            }

            #endregion

            #region SetProtocolName(ProtocolName)

            /// <summary>
            /// Set the protocol name.
            /// </summary>
            /// <param name="ProtocolName">The protocol name.</param>
            public Builder SetProtocolName(String ProtocolName)
            {
                this.ProtocolName = ProtocolName;
                return this;
            }

            #endregion

            #region SetProtocolVersion(ProtocolVersion)

            /// <summary>
            /// Set the protocol version.
            /// </summary>
            /// <param name="ProtocolVersion">The protocol version.</param>
            public Builder SetProtocolVersion(HTTPVersion ProtocolVersion)
            {
                this.ProtocolVersion = ProtocolVersion;
                return this;
            }

            #endregion

            #region SetContent(...)

            #region SetContent(ByteArray)

            /// <summary>
            /// The HTTP content/body.
            /// </summary>
            /// <param name="ByteArray">The HTTP content/body.</param>
            public Builder SetContent(Byte[] ByteArray)
            {
                this.Content = ByteArray;
                return this;
            }

            #endregion

            #region SetContent(String)

            /// <summary>
            /// The HTTP content/body.
            /// </summary>
            /// <param name="String">The HTTP content/body.</param>
            public Builder SetContent(String String)
            {
                this.Content = String.ToUTF8Bytes();
                return this;
            }

            #endregion

            #endregion

            #region SetContentStream(ContentStream)

            /// <summary>
            /// The HTTP content/body as a stream.
            /// </summary>
            /// <param name="ContentStream">The HTTP content/body as a stream.</param>
            public Builder SetContent(Stream ContentStream)
            {
                this.ContentStream = ContentStream;
                return this;
            }

            #endregion

            #endregion

            #region Set general header fields

            #region SetCacheControl(CacheControl)

            /// <summary>
            /// Set the HTTP CacheControl.
            /// </summary>
            /// <param name="CacheControl">CacheControl.</param>
            public Builder SetCacheControl(String CacheControl)
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
            public Builder SetConnection(String Connection)
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
            public Builder SetContentEncoding(Encoding ContentEncoding)
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
            public Builder SetContentLanguage(List<String> ContentLanguages)
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
            public Builder SetContentLength(UInt64? ContentLength)
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
            public Builder SetContentLocation(String ContentLocation)
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
            public Builder SetContentMD5(String ContentMD5)
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
            public Builder SetContentRange(String ContentRange)
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
            public Builder SetContentType(HTTPContentType ContentType)
            {
                this.ContentType = ContentType;
                return this;
            }

            #endregion

            #region SetDate(Date)

            /// <summary>
            /// Set the HTTP Date.
            /// </summary>
            /// <param name="Date">DateTime.</param>
            public Builder SetDate(DateTime Date)
            {
                this.Date = Date;
                return this;
            }

            #endregion

            #region SetVia(Via)

            /// <summary>
            /// Set the HTTP Via.
            /// </summary>
            /// <param name="Via">Via.</param>
            public Builder SetVia(String Via)
            {
                this.Via = Via;
                return this;
            }

            #endregion

            #endregion


            #region (static) ClientError       (Request, Configurator = null)

            /// <summary>
            /// Create a new 0-ClientError HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder ClientError(HTTPRequest      Request,
                                              Action<Builder>  Configurator = null)
            {

                var response = new Builder(Request, HTTPStatusCode.ClientError);

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 0-ClientError HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder ClientError(HTTPRequest             Request,
                                              Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request, HTTPStatusCode.ClientError);

                Configurator?.Invoke(response);

                return response;

            }

            #endregion


            #region (static) OK                (Request, Configurator = null)

            /// <summary>
            /// Create a new 200-OK HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder OK(HTTPRequest      Request,
                                     Action<Builder>  Configurator = null)
            {

                var response = new Builder(Request, HTTPStatusCode.OK);

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 200-OK HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder OK(HTTPRequest             Request,
                                     Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request, HTTPStatusCode.OK);

                if (Configurator != null)
                    return Configurator(response);

                return response;

            }

            #endregion

            #region (static) BadRequest        (Request, Configurator = null)

            /// <summary>
            /// Create a new 400-BadRequest HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder BadRequest(HTTPRequest      Request,
                                             Action<Builder>  Configurator = null)
            {

                var response = new Builder(Request, HTTPStatusCode.BadRequest);

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 400-BadRequest HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder BadRequest(HTTPRequest             Request,
                                             Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request, HTTPStatusCode.BadRequest);

                Configurator?.Invoke(response);

                return response;

            }

            #endregion

            #region (static) ServiceUnavailable(Request, Configurator = null)

            /// <summary>
            /// Create a new 503-ServiceUnavailable HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder ServiceUnavailable(HTTPRequest      Request,
                                                     Action<Builder>  Configurator = null)
            {

                var response = new Builder(Request, HTTPStatusCode.ServiceUnavailable);

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 503-ServiceUnavailable HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder ServiceUnavailable(HTTPRequest             Request,
                                                     Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request, HTTPStatusCode.ServiceUnavailable);

                Configurator?.Invoke(response);

                return response;

            }

            #endregion

            #region (static) FailedDependency  (Request, Configurator = null)

            /// <summary>
            /// Create a new 424-FailedDependency HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder FailedDependency(HTTPRequest      Request,
                                                   Action<Builder>  Configurator = null)
            {

                var response = new Builder(Request, HTTPStatusCode.FailedDependency);

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 424-FailedDependency HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder FailedDependency(HTTPRequest             Request,
                                                   Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request, HTTPStatusCode.FailedDependency);

                Configurator?.Invoke(response);

                return response;

            }

            #endregion

            #region (static) GatewayTimeout    (Request, Configurator = null)

            /// <summary>
            /// Create a new 504-GatewayTimeout HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder GatewayTimeout(HTTPRequest      Request,
                                                 Action<Builder>  Configurator = null)
            {

                var response = new Builder(Request, HTTPStatusCode.GatewayTimeout);

                Configurator?.Invoke(response);

                return response;

            }

            /// <summary>
            /// Create a new 504-GatewayTimeout HTTP response and apply the given delegate.
            /// </summary>
            /// <param name="Request">A HTTP request.</param>
            /// <param name="Configurator">A delegate to configure the HTTP response.</param>
            public static Builder GatewayTimeout(HTTPRequest             Request,
                                                 Func<Builder, Builder>  Configurator)
            {

                var response = new Builder(Request, HTTPStatusCode.GatewayTimeout);

                Configurator?.Invoke(response);

                return response;

            }

            #endregion


            #region PrepareImmutability()

            /// <summary>
            /// Prepares the immutability of an HTTP PDU, e.g. calculates
            /// and set the Content-Length header.
            /// </summary>
            protected override void PrepareImmutability()
            {
                base.PrepareImmutability();
            }

            #endregion

            #region AsImmutable

            /// <summary>
            /// Converts this HTTPResponseBuilder into an immutable HTTPResponse.
            /// </summary>
            public HTTPResponse AsImmutable
            {
                get
                {

                    PrepareImmutability();

                    if (Content != null)
                        return Parse(HTTPHeader, Content,       HTTPRequest);

                    else if (ContentStream != null)
                        return Parse(HTTPHeader, ContentStream, HTTPRequest);

                    else
                        return Parse(HTTPHeader, HTTPRequest);

                }
            }

            #endregion

        }

        #endregion

    }

    #endregion

}
