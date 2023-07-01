/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Collections.Concurrent;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for HTTP respones.
    /// </summary>
    public static class HTTPResponseExtensions
    {

        #region GetResponseBodyAsUTF8String  (this Request, HTTPContentType)

        public static String GetResponseBodyAsUTF8String(this HTTPResponse  Response,
                                                         HTTPContentType    HTTPContentType,
                                                         Boolean            AllowEmptyHTTPBody = false)
        {

            if (Response.ContentType != HTTPContentType)
                return "";

            if (!AllowEmptyHTTPBody)
            {

                if (Response.ContentLength == 0)
                    return "";

                if (!Response.TryReadHTTPBodyStream())
                    return "";

                if (Response.HTTPBody == null || Response.HTTPBody.Length == 0)
                    return "";

            }

            var ResponseBodyString = Response.HTTPBody.ToUTF8String().Trim();

            return ResponseBodyString.IsNullOrEmpty()
                       ? AllowEmptyHTTPBody
                             ? ""
                             : ResponseBodyString
                       : ResponseBodyString;

        }

        #endregion


        #region TryParseJObjectResponseBody   (this Request, out JSON, out HTTPResponse, AllowEmptyHTTPBody = false)

        public static Boolean TryParseJObjectResponseBody(this HTTPResponse  Response,
                                                          out JObject        JSON,
                                                          Boolean            AllowEmptyHTTPBody   = false)
        {

            #region AllowEmptyHTTPBody

            if (Response.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                JSON = new JObject();
                return false;
            }

            #endregion

            #region Try to parse the JSON

            try
            {

                JSON = JObject.Parse(Response.GetResponseBodyAsUTF8String(HTTPContentType.JSON_UTF8,
                                                                          AllowEmptyHTTPBody));

                return true;

            }
            catch (Exception)
            {
                JSON = new JObject();
                return false;
            }

            #endregion

        }

        #endregion

        #region TryParseJArrayResponseBody    (this Request, out JSON, out HTTPResponse, AllowEmptyHTTPBody = false)

        public static Boolean TryParseJArrayResponseBody(this HTTPResponse  Response,
                                                         out JArray         JSON,
                                                         Boolean            AllowEmptyHTTPBody   = false)
        {

            #region AllowEmptyHTTPBody

            if (Response.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                JSON = new JArray();
                return true;
            }

            #endregion

            #region Try to parse the JSON array

            try
            {

                JSON = JArray.Parse(Response.GetResponseBodyAsUTF8String(HTTPContentType.JSON_UTF8,
                                                                         AllowEmptyHTTPBody));

                return true;

            }
            catch (Exception)
            {
                JSON = new JArray();
                return false;
            }

            #endregion

        }

        #endregion



        public static Byte[] CreateError(String Text)
            => (@"{ ""description"": """ + Text + @""" }").ToUTF8Bytes();


        public static HTTPResponse.Builder CreateBadRequest(HTTPRequest HTTPRequest, String Context, String ParameterName)
        {

            return new HTTPResponse.Builder(HTTPRequest) {
                HTTPStatusCode  = HTTPStatusCode.BadRequest,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(new JProperty("@context",    Context),
                                              new JProperty("description", "Missing \"" + ParameterName + "\" JSON property!")).ToString().ToUTF8Bytes()
            };

        }

        public static HTTPResponse.Builder CreateBadRequest(HTTPRequest HTTPRequest, String Context, String ParameterName, String Value)
        {

            return new HTTPResponse.Builder(HTTPRequest) {
                HTTPStatusCode  = HTTPStatusCode.BadRequest,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(new JProperty("@context",    Context),
                                              new JProperty("value",       Value),
                                              new JProperty("description", "Invalid \"" + ParameterName + "\" property value!")).ToString().ToUTF8Bytes()
            };

        }

        public static HTTPResponse.Builder CreateNotFound(HTTPRequest HTTPRequest, String Context, String ParameterName, String Value)
        {

            return new HTTPResponse.Builder(HTTPRequest) {
                HTTPStatusCode  = HTTPStatusCode.NotFound,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(new JProperty("@context",    Context),
                                              new JProperty("value",       Value),
                                              new JProperty("description", "Unknown \"" + ParameterName + "\" property value!")).ToString().ToUTF8Bytes()
            };

        }



        #region ParseContent      (this Response, ContentParser)

        public static HTTPResponse<TResult> ParseContent<TResult>(this HTTPResponse      Response,
                                                                  Func<Byte[], TResult>  ContentParser)

            => new (Response,
                    ContentParser(Response.HTTPBody));

        #endregion

        #region ParseContentStream(this Response, ContentParser)

        public static HTTPResponse<TResult> ParseContentStream<TResult>(this HTTPResponse      Response,
                                                                        Func<Stream, TResult>  ContentParser)

            => new (Response,
                    ContentParser(Response.HTTPBodyStream));

        #endregion

        #region CreateLogEntry    (this Response)

        public static String CreateLogEntry(this HTTPResponse Response)

            => String.Concat(HTTPResponse.RequestMarker,                                                                        Environment.NewLine,
                             Response.HTTPRequest.HTTPSource.ToString(), " -> ", Response.HTTPRequest.RemoteSocket.ToString(),  Environment.NewLine,
                             Response.HTTPRequest.Timestamp.ToIso8601(),                                                        Environment.NewLine,
                             Response.HTTPRequest.EventTrackingId,                                                              Environment.NewLine,
                             Response.HTTPRequest.EntirePDU,                                                                    Environment.NewLine,
                             HTTPResponse.ResponseMarker,                                                                       Environment.NewLine,
                             Response.Timestamp.ToIso8601(),                                                                    Environment.NewLine,
                             Response.EntirePDU,                                                                                Environment.NewLine,
                             HTTPResponse.EndMarker,                                                                            Environment.NewLine);

        #endregion

        #region AppendToLogfile   (this Response, Logfilename)

        public static void AppendToLogfile(this HTTPResponse  Response,
                                           String             Logfilename)
        {
            using (var Logfile = File.AppendText(Logfilename))
            {
                Logfile.WriteLine(CreateLogEntry(Response));
            }
        }

        #endregion

    }

    #region HTTPResponse

    /// <summary>
    /// A read-only HTTP response header.
    /// </summary>
    public partial class HTTPResponse : AHTTPPDU
    {

        #region Data

        public const String RequestMarker   = "<<< Request <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<";
        public const String ResponseMarker  = ">>> Response >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>";
        public const String EndMarker       = "======================================================================================";

        #endregion

        #region HTTPRequest

        /// <summary>
        /// The optional HTTP request for this HTTP response.
        /// </summary>
        public HTTPRequest?    HTTPRequest       { get; }

        /// <summary>
        /// The HTTP status code.
        /// </summary>
        public HTTPStatusCode  HTTPStatusCode    { get; }

        #endregion

        #region Standard response header fields

        #region Age

        /// <summary>
        /// Age
        /// </summary>
        public UInt64? Age

            => GetHeaderField(HTTPResponseHeaderField.Age);

        #endregion

        #region Allow

        /// <summary>
        /// Allow
        /// </summary>
        public IEnumerable<HTTPMethod> Allow

            => GetHeaderFields(HTTPResponseHeaderField.Allow) ?? Array.Empty<HTTPMethod>();

        #endregion

        #region DAV

        /// <summary>
        /// DAV
        /// </summary>
        public String? DAV

            => GetHeaderField(HTTPResponseHeaderField.DAV);

        #endregion

        #region ETag

        /// <summary>
        /// E-Tag
        /// </summary>
        public String? ETag

            => GetHeaderField(HTTPResponseHeaderField.ETag);

        #endregion

        #region Expires

        /// <summary>
        /// Expires
        /// </summary>
        public String? Expires

            => GetHeaderField(HTTPResponseHeaderField.Expires);

        #endregion

        #region LastModified

        /// <summary>
        /// LastModified
        /// </summary>
        public DateTime? LastModified

            => GetHeaderField(HTTPResponseHeaderField.LastModified);

        #endregion

        #region Location

        public Location? Location

            => GetHeaderField(HTTPResponseHeaderField.Location);

        #endregion

        #region ProxyAuthenticate

        public String? ProxyAuthenticate

            => GetHeaderField(HTTPResponseHeaderField.ProxyAuthenticate);

        #endregion

        #region Retry-After

        /// <summary>
        /// Retry-After
        /// </summary>
        public String? RetryAfter

            => GetHeaderField(HTTPResponseHeaderField.RetryAfter);

        #endregion

        #region Server

        /// <summary>
        /// Server
        /// </summary>
        public String? Server

            => GetHeaderField(HTTPResponseHeaderField.Server);

        #endregion

        #region Vary

        /// <summary>
        /// Vary
        /// </summary>
        public String? Vary

            => GetHeaderField(HTTPResponseHeaderField.Vary);

        #endregion

        #region WWW-Authenticate

        /// <summary>
        /// WWW-Authenticate
        /// </summary>
        public String? WWWAuthenticate

            => GetHeaderField(HTTPResponseHeaderField.WWWAuthenticate);

        #endregion

        #region Transfer-Encoding

        /// <summary>
        /// Transfer-Encoding
        /// </summary>
        public String? TransferEncoding

            => GetHeaderField(HTTPResponseHeaderField.TransferEncoding);

        #endregion


        // WebSockets

        #region Sec-WebSocket-Accept

        /// <summary>
        /// Sec-WebSocket-Accept
        /// </summary>
        public String? SecWebSocketAccept

            => GetHeaderField(HTTPResponseHeaderField.SecWebSocketAccept);

        #endregion


        // CORS

        #region Access-Control-Allow-Origin

        /// <summary>
        /// Access-Control-Allow-Origin
        /// </summary>
        public String? AccessControlAllowOrigin
            => GetHeaderField(HTTPResponseHeaderField.AccessControlAllowOrigin);

        #endregion

        #region Access-Control-Allow-Methods

        /// <summary>
        /// Access-Control-Allow-Methods
        /// </summary>
        public IEnumerable<String> AccessControlAllowMethods

            => GetHeaderFields(HTTPResponseHeaderField.AccessControlAllowMethods);

        #endregion

        #region Access-Control-Allow-Headers

        /// <summary>
        /// Access-Control-Allow-Headers
        /// </summary>
        public IEnumerable<String> AccessControlAllowHeaders

            => GetHeaderFields(HTTPResponseHeaderField.AccessControlAllowHeaders);

        #endregion

        #region Access-Control-Max-Age

        /// <summary>
        /// Access-Control-Max-Age
        /// </summary>
        public UInt64? AccessControlMaxAge

            => GetHeaderField(HTTPResponseHeaderField.AccessControlMaxAge);

        #endregion

        #endregion

        #region Non-standard response header fields

        /// <summary>
        /// The runtime of the HTTP request/response pair.
        /// </summary>
        public TimeSpan  Runtime                { get; }

        /// <summary>
        /// The number of retransmissions of this request.
        /// </summary>
        public Byte      NumberOfRetries        { get; }

        /// <summary>
        /// The optional HTTP sub protocol response, e.g. HTTP Web Socket.
        /// </summary>
        public Object?   SubprotocolResponse    { get; }

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
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="HTTPHeader">A valid string representation of a http response header.</param>
        /// <param name="HTTPBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPBodyStream">The HTTP body as an stream of bytes.</param>
        /// <param name="HTTPBodyReceiveBufferSize">The size of the HTTP body receive buffer.</param>
        /// <param name="SubprotocolResponse">An optional HTTP sub protocol response, e.g. HTTP Web Socket.</param>
        /// 
        /// <param name="CancellationToken">A token to cancel the HTTP response processing.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="Runtime">The runtime of the HTTP request/response pair.</param>
        /// <param name="NumberOfRetries">The number of retransmissions of this request.</param>
        private HTTPResponse(DateTime            Timestamp,
                             HTTPSource          HTTPSource,
                             IPSocket            LocalSocket,
                             IPSocket            RemoteSocket,
                             String              HTTPHeader,
                             HTTPRequest?        HTTPRequest                 = null,
                             Byte[]?             HTTPBody                    = null,
                             Stream?             HTTPBodyStream              = null,
                             UInt32?             HTTPBodyReceiveBufferSize   = DefaultHTTPBodyReceiveBufferSize,
                             Object?             SubprotocolResponse         = null,

                             CancellationToken?  CancellationToken           = null,
                             EventTracking_Id?   EventTrackingId             = null,
                             TimeSpan?           Runtime                     = null,
                             Byte                NumberOfRetries             = 0)

            : base(Timestamp,
                   HTTPSource,
                   LocalSocket,
                   RemoteSocket,
                   HTTPHeader,
                   HTTPBody,
                   HTTPBodyStream,
                   HTTPBodyReceiveBufferSize,
                   CancellationToken,
                   EventTrackingId)

        {

            this.HTTPRequest          = HTTPRequest;

            var statusCodeLine        = FirstPDULine.Split(new Char[] { ' ' }, 3);
            if (statusCodeLine.Length != 2 && statusCodeLine.Length != 3)
                throw new ArgumentException($"Invalid HTTP response status code line: '{FirstPDULine}'!");

            this.HTTPStatusCode       = HTTPStatusCode.ParseString(statusCodeLine[1]);
            this.NumberOfRetries      = NumberOfRetries;
            this.SubprotocolResponse  = SubprotocolResponse;
            this.Runtime              = Runtime ?? (HTTPRequest is not null
                                                        ? Timestamp - HTTPRequest.Timestamp
                                                        : TimeSpan.Zero);

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

            this.HTTPRequest     = Response.HTTPRequest;
            this.HTTPStatusCode  = Response.HTTPStatusCode;

        }

        #endregion

        #endregion


        #region (static) Parse(ResponseHeader,                     Request = null, SubprotocolResponse = null)

        /// <summary>
        /// Parse the HTTP response from its text representation and
        /// attach the given HTTP body.
        /// </summary>
        /// <param name="ResponseHeader">The HTTP header of the response.</param>
        /// <param name="Request">An optional HTTP request leading to this response.</param>
        /// <param name="SubprotocolResponse">An optional HTTP sub protocol response, e.g. HTTP Web Socket.</param>
        public static HTTPResponse Parse(String              ResponseHeader,
                                         HTTPRequest?        Request               = null,
                                         Object?             SubprotocolResponse   = null,

                                         CancellationToken   CancellationToken     = default,
                                         EventTracking_Id?   EventTrackingId       = null,
                                         TimeSpan?           Runtime               = null,
                                         Byte                NumberOfRetries       = 0)

            => new (Illias.Timestamp.Now,
                    new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                    IPSocket.LocalhostV4(IPPort.HTTPS),
                    IPSocket.LocalhostV4(IPPort.HTTPS),
                    ResponseHeader,
                    Request,
                    null,
                    null,
                    null,
                    SubprotocolResponse,
                    CancellationToken,
                    EventTrackingId,
                    Runtime,
                    NumberOfRetries);

        #endregion

        #region (static) Parse(ResponseHeader, ResponseBody,       Request = null, SubprotocolResponse = null)

        /// <summary>
        /// Parse the HTTP response from its text representation and
        /// attach the given HTTP body.
        /// </summary>
        /// <param name="ResponseHeader">The HTTP header of the response.</param>
        /// <param name="ResponseBody">The HTTP body of the response.</param>
        /// <param name="Request">An optional HTTP request leading to this response.</param>
        /// <param name="SubprotocolResponse">An optional HTTP sub protocol response, e.g. HTTP Web Socket.</param>
        public static HTTPResponse Parse(String              ResponseHeader,
                                         Byte[]              ResponseBody,
                                         HTTPRequest?        Request               = null,
                                         Object?             SubprotocolResponse   = null,

                                         CancellationToken   CancellationToken     = default,
                                         EventTracking_Id?   EventTrackingId       = null,
                                         TimeSpan?           Runtime               = null,
                                         Byte                NumberOfRetries       = 0)

            => new (Illias.Timestamp.Now,
                    new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                    IPSocket.LocalhostV4(IPPort.HTTPS),
                    IPSocket.LocalhostV4(IPPort.HTTPS),
                    ResponseHeader,
                    Request,
                    ResponseBody,
                    null,
                    null,
                    SubprotocolResponse,
                    CancellationToken,
                    EventTrackingId,
                    Runtime,
                    NumberOfRetries);

        #endregion

        #region (static) Parse(ResponseHeader, ResponseBodyStream, Request = null, SubprotocolResponse = null)

        /// <summary>
        /// Parse the HTTP response from its text representation and
        /// attach the given HTTP body.
        /// </summary>
        /// <param name="ResponseHeader">The HTTP header of the response.</param>
        /// <param name="ResponseBodyStream">The HTTP body of the response as stream of bytes.</param>
        /// <param name="Request">An optional HTTP request leading to this response.</param>
        /// <param name="SubprotocolResponse">An optional HTTP sub protocol response, e.g. HTTP Web Socket.</param>
        public static HTTPResponse Parse(String              ResponseHeader,
                                         Stream              ResponseBodyStream,
                                         HTTPRequest?        Request               = null,
                                         Object?             SubprotocolResponse   = null,

                                         CancellationToken   CancellationToken     = default,
                                         EventTracking_Id?   EventTrackingId       = null,
                                         TimeSpan?           Runtime               = null,
                                         Byte                NumberOfRetries       = 0)

            => new (Illias.Timestamp.Now,
                    new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                    IPSocket.LocalhostV4(IPPort.HTTPS),
                    IPSocket.LocalhostV4(IPPort.HTTPS),
                    ResponseHeader,
                    Request,
                    null,
                    ResponseBodyStream,
                    null,
                    SubprotocolResponse,

                    Request?.CancellationToken,
                    Request?.EventTrackingId);

        #endregion

        #region (static) Parse(Text, Timestamp = null, HTTPSource = null, LocalSocket = null, EventTrackingId = null)

        /// <summary>
        /// Parse the given text as a HTTP response.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP response.</param>
        /// <param name="Timestamp">The optional timestamp of the response.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the response.</param>
        /// <param name="LocalSocket">The optional local TCP socket of the response.</param>
        /// <param name="RemoteSocket">The optional remote TCP socket of the request.</param>
        /// <param name="EventTrackingId">The optional event tracking identification of the response.</param>
        /// <param name="Request">The HTTP request for this HTTP response.</param>
        public static HTTPResponse Parse(IEnumerable<String>  Text,
                                         DateTime?            Timestamp         = null,
                                         HTTPSource?          HTTPSource        = null,
                                         IPSocket?            LocalSocket       = null,
                                         IPSocket?            RemoteSocket      = null,
                                         EventTracking_Id?    EventTrackingId   = null,
                                         HTTPRequest?         Request           = null)
        {

                Timestamp ??= Illias.Timestamp.Now;
            var Header      = Text.TakeWhile(line => line != "").AggregateWith("\r\n");
            var Body        = Text.SkipWhile(line => line != "").Skip(1).AggregateWith("\r\n");

            return new HTTPResponse(Timestamp    ?? Illias.Timestamp.Now,
                                    HTTPSource   ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                    LocalSocket  ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                    RemoteSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                    Header,
                                    Request,
                                    Body.ToUTF8Bytes(),
                                    EventTrackingId:  EventTrackingId,
                                    Runtime:          Request is not null ? Timestamp - Request.Timestamp : TimeSpan.Zero);

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
                                                      SearchOption).
                                       OrderByDescending(file => file),
                             new ParallelOptions() { MaxDegreeOfParallelism = 1 },
                             file => {

                var _request            = new List<String>();
                var _response           = new List<String>();
                var copy                = "none";
                var relativelinenumber  = 0;
                var RequestTimestamp    = Illias.Timestamp.Now;
                var ResponseTimestamp   = Illias.Timestamp.Now;

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

                                if (HTTPRequest.TryParse(_request, out HTTPRequest parsedHTTPRequest, Timestamp: RequestTimestamp))
                                {

                                    _responses.Add(Parse(_response,
                                                         Timestamp: ResponseTimestamp,
                                                         Request:   parsedHTTPRequest));

                                }

                                else
                                    DebugX.LogT("Could not parse reloaded HTTP request!");

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
                        DebugX.Log(e.Message);
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
                                                      SearchOption).
                                       OrderByDescending(file => file),
                             new ParallelOptions() { MaxDegreeOfParallelism = 1 },
                             file => {

                var _request            = new List<String>();
                var _response           = new List<String>();
                var copy                = "none";
                var relativelinenumber  = 0;
                var HTTPSource          = new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS));
                var RequestTimestamp    = Illias.Timestamp.Now;
                var ResponseTimestamp   = Illias.Timestamp.Now;

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
                            copy                = "request";
                            relativelinenumber  = 0;
                        }

                        else if (line == ResponseMarker)// "<<<<<<--Response----<<<<<<------<<<<<<------<<<<<<------<<<<<<------<<<<<<------")
                        {
                            copy                = "response";
                            relativelinenumber  = 0;
                        }

                        else if (line == EndMarker)// "--------------------------------------------------------------------------------")
                        {

                            if ((FromTimestamp == null || ResponseTimestamp >= FromTimestamp.Value) &&
                                (  ToTimestamp == null || ResponseTimestamp <    ToTimestamp.Value))
                            {

                                if (HTTPRequest.TryParse(_request.Skip(1),
                                                         out HTTPRequest parsedHTTPRequest,
                                                         RequestTimestamp,
                                                         HTTPSource,
                                                         EventTrackingId: EventTracking_Id.Parse(_request[0])))
                                {

                                    _responses.Add(Parse(_response,
                                                         ResponseTimestamp,
                                                         HTTPSource,
                                                         Request: parsedHTTPRequest));

                                }

                                else
                                    DebugX.LogT("Could not parse reloaded HTTP request!");

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
                        DebugX.LogT("Could not parse reloaded HTTP response: " + e.Message);
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
        public static Builder OK(HTTPRequest      Request,
                                 Action<Builder>  Configurator = null)

            => Builder.OK(Request, Configurator);


        /// <summary>
        /// Create a new 200-OK HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder OK(HTTPRequest             Request,
                                 Func<Builder, Builder>  Configurator)

            => Builder.OK(Request, Configurator);

        #endregion

        #region (static) BadRequest        (Request, Configurator = null)

        /// <summary>
        /// Create a new 400-BadRequest HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder BadRequest(HTTPRequest      Request,
                                         Action<Builder>  Configurator = null)

            => Builder.BadRequest(Request, Configurator);


        /// <summary>
        /// Create a new 400-BadRequest HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder BadRequest(HTTPRequest             Request,
                                         Func<Builder, Builder>  Configurator)

            => Builder.BadRequest(Request, Configurator);

        #endregion

        #region (static) ServiceUnavailable(Request, Configurator = null)

        /// <summary>
        /// Create a new 503-ServiceUnavailable HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder ServiceUnavailable(HTTPRequest      Request,
                                                 Action<Builder>  Configurator = null)

            => Builder.ServiceUnavailable(Request, Configurator);


        /// <summary>
        /// Create a new 503-ServiceUnavailable HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder ServiceUnavailable(HTTPRequest             Request,
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
        public static Builder GatewayTimeout(HTTPRequest      Request,
                                             Action<Builder>  Configurator = null)

            => Builder.GatewayTimeout(Request, Configurator);


        /// <summary>
        /// Create a new 504-GatewayTimeout HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder GatewayTimeout(HTTPRequest             Request,
                                             Func<Builder, Builder>  Configurator)

            => Builder.GatewayTimeout(Request, Configurator);

        #endregion

        #region (static) ClientError       (Request, Configurator = null)

        /// <summary>
        /// Create a new 0-ClientError HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder ClientError(HTTPRequest      Request,
                                          Action<Builder>  Configurator = null)

            => Builder.ClientError(Request, Configurator);

        /// <summary>
        /// Create a new 0-ClientError HTTP response and apply the given delegate.
        /// </summary>
        /// <param name="Request">A HTTP request.</param>
        /// <param name="Configurator">A delegate to configure the HTTP response.</param>
        public static Builder ClientError(HTTPRequest             Request,
                                          Func<Builder, Builder>  Configurator)

            => Builder.ClientError(Request, Configurator);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override String ToString()
            => EntirePDU;

        #endregion

    }

    #endregion

    #region HTTPResponse<TContent>

    /// <summary>
    /// A helper class to transport HTTP data and its metadata.
    /// </summary>
    /// <typeparam name="TContent">The type of the parsed data.</typeparam>
    public class HTTPResponse<TContent> : HTTPResponse
    {

        #region Data

        private readonly Boolean isFault;

        #endregion

        #region Properties

        /// <summary>
        /// The parsed content.
        /// </summary>
        public TContent    Content      { get; }

        /// <summary>
        /// An exception during parsing.
        /// </summary>
        public Exception?  Exception    { get; }

        /// <summary>
        /// An error during parsing.
        /// </summary>
        public Boolean HasErrors
            => Exception is not null && !isFault;

        #endregion

        #region Constructor(s)

        #region (private) HTTPResponse(Response, Content, IsFault = false, NumberOfTransmissionRetries = 0, Exception = null)

        private HTTPResponse(HTTPResponse  Response,
                             TContent      Content,
                             Boolean?      IsFault     = false,
                             Exception?    Exception   = null)

            : base(Response)

        {

            this.Content    = Content;
            this.isFault    = IsFault ?? false;
            this.Exception  = Exception;

        }

        #endregion

        #region HTTPResponse(Response, Content)

        public HTTPResponse(HTTPResponse  Response,
                            TContent      Content)

            : this(Response,
                   Content,
                   null,
                   null)

        { }

        #endregion

        #region HTTPResponse(Response, IsFault)

        public HTTPResponse(HTTPResponse  Response,
                            Boolean       IsFault)

            : this(Response,
                   default,
                   IsFault)

        { }

        #endregion

        #region HTTPResponse(Response, Exception)

        public HTTPResponse(HTTPResponse  Response,
                            Exception     Exception)

            : this(Response,
                   default,
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
            this.isFault    = true;
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

            : this(new Builder(Request) {
                       HTTPStatusCode = HTTPStatusCode.BadRequest
                   },
                   default,
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

            if (ContentConverter is null)
                throw new ArgumentNullException(nameof(ContentConverter),  "The given content converter delegate must not be null!");

            return new HTTPResponse<TResult>(this,
                                             ContentConverter(this.Content));

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
                                                             OnExceptionDelegate?                          OnException   = null)
        {

            if (ContentConverter is null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPResponse<TResult>(this,
                                             ContentConverter(this.Content,
                                                              OnException));

        }

        #endregion


        #region ConvertContent<TRequest, TResult>(Request, ContentConverter)

        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TRequest">The type of the converted HTTP request body content.</typeparam>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="Request">The request leading to this response.</param>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        public HTTPResponse<TResult> ConvertContent<TRequest, TResult>(TRequest                           Request,
                                                                       Func<TRequest, TContent, TResult>  ContentConverter)
        {

            if (ContentConverter is null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPResponse<TResult>(this,
                                             ContentConverter(Request,
                                                              this.Content));

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
                                                                       OnExceptionDelegate?                                    OnException   = null)
        {

            if (ContentConverter is null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPResponse<TResult>(this,
                                             ContentConverter(Request,
                                                              this.Content,
                                                              OnException));

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
        public HTTPResponse<TResult> ConvertContent<TRequest, TResult>(TRequest                                                              Request,
                                                                       Func<TRequest, TContent, HTTPResponse, OnExceptionDelegate, TResult>  ContentConverter,
                                                                       OnExceptionDelegate?                                                  OnException   = null)
        {

            if (ContentConverter is null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPResponse<TResult>(this,
                                             ContentConverter(Request,
                                                              Content,
                                                              this,
                                                              OnException));

        }

        #endregion



        public static HTTPResponse<TContent> OK(HTTPRequest  HTTPRequest,
                                                TContent     Content)

            => new (HTTPRequest, Content);

        public static HTTPResponse<TContent> OK(TContent Content)

            => new (null, Content, null, null);

        public static HTTPResponse<TContent> IsFault(HTTPResponse  Response,
                                                     TContent      Content)

            => new (Response,
                    Content,
                    true);

        public static HTTPResponse<TContent> IsFault(HTTPResponse  Response,
                                                     Exception     Exception)

            => new (Response,
                    default,
                    true,
                    Exception);

        public static HTTPResponse<TContent> ClientError(TContent Content)

            => new (null, Content, IsFault: true);

        public static HTTPResponse<TContent> ExceptionThrown(TContent   Content,
                                                             Exception  Exception)

            => new (Content, Exception);


        #region (static) GatewayTimeout

        public static HTTPResponse<TContent> GatewayTimeout(TContent Content)

            => new (new Builder(null,
                                HTTPStatusCode.GatewayTimeout),
                    Content);

        #endregion


    }

    #endregion

}
