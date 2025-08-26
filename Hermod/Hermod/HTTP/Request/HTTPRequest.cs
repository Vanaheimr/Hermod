/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Xml.Linq;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.MIME;
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for HTTP requests.
    /// </summary>
    public static class HTTPRequestExtensions
    {

        #region GetRequestBodyAsUTF8String   (this Request, ExpectedContentType, AllowEmptyHTTPBody = false)

        /// <summary>
        /// Return the HTTP request body as an UTF8 string.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="ExpectedContentType">The expected HTTP request content type.</param>
        /// <param name="AllowEmptyHTTPBody">Allow the HTTP request body to be empty!</param>
        public static HTTPResult<String> GetRequestBodyAsUTF8String(this HTTPRequest  Request,
                                                                    HTTPContentType   ExpectedContentType,
                                                                    Boolean           AllowEmptyHTTPBody   = false)
        {

            if (Request.ContentType is null ||
                Request.ContentType != ExpectedContentType)
            {
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);
            }

            if (!AllowEmptyHTTPBody)
            {

                if (Request.ContentLength == 0)
                    return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

                if (!Request.TryReadHTTPBodyStream())
                    return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

                if (Request.HTTPBody is null || Request.HTTPBody.Length == 0)
                    return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            }

            var requestBodyString = Request.HTTPBody?.ToUTF8String().Trim() ?? String.Empty;

            return requestBodyString.IsNullOrEmpty()
                       ? AllowEmptyHTTPBody
                             ? new HTTPResult<String>(Result:  String.Empty)
                             : new HTTPResult<String>(Request, HTTPStatusCode.BadRequest)
                       : new HTTPResult<String>(Result: requestBodyString);

        }

        #endregion

        #region TryParseUTF8StringRequestBody(this Request, ExpectedContentType, out Text, out HTTPResponse, AllowEmptyHTTPBody = false)

        /// <summary>
        /// Return the HTTP request body as an UTF8 string.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="ExpectedContentType">The expected HTTP request content type.</param>
        /// <param name="Text">The HTTP request body as an UTF8 string.</param>
        /// <param name="HTTPResponse">An HTTP error response.</param>
        /// <param name="AllowEmptyHTTPBody">Allow the HTTP request body to be empty!</param>
        public static Boolean TryParseUTF8StringRequestBody(this HTTPRequest   Request,
                                                            HTTPContentType    ExpectedContentType,
                                                            out String?        Text,
                                                            out HTTPResponse?  HTTPResponse,
                                                            Boolean            AllowEmptyHTTPBody   = false)
        {

            #region AllowEmptyHTTPBody

            Text          = null;
            HTTPResponse  = null;

            if (Request.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                HTTPResponse = HTTPResponse.OK(Request);
                return false;
            }

            #endregion

            #region Get text body

            var requestBodyString = Request.GetRequestBodyAsUTF8String(ExpectedContentType,
                                                                       AllowEmptyHTTPBody);

            if (requestBodyString.HasErrors)
            {
                HTTPResponse = requestBodyString.Error;
                return false;
            }

            #endregion

            Text = requestBodyString.Data;

            return true;

        }

        #endregion


        #region TryParseUTF8StringRequestBody(this Request, out Text,                      out HTTPResponseBuilder, AllowEmptyHTTPBody = false)

        /// <summary>
        /// Return the HTTP request body as JSON object.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="Text">The HTTP request body as a string.</param>
        /// <param name="HTTPResponseBuilder">An HTTP error response builder.</param>
        /// <param name="AllowEmptyHTTPBody">Allow the HTTP request body to be empty!</param>
        public static Boolean TryParseUTF8StringRequestBody(this HTTPRequest                                Request,
                                                            [NotNullWhen(true)]  out String?                Text,
                                                            [NotNullWhen(false)] out HTTPResponse.Builder?  HTTPResponseBuilder,
                                                            Boolean                                         AllowEmptyHTTPBody   = false)
        {

            #region AllowEmptyHTTPBody

            Text                 = String.Empty;
            HTTPResponseBuilder  = null;

            if (Request.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                HTTPResponseBuilder = HTTPResponse.OK(Request);
                return false;
            }

            #endregion

            #region Get text body

            var requestBodyString = Request.GetRequestBodyAsUTF8String(HTTPContentType.Text.PLAIN,
                                                                       AllowEmptyHTTPBody);

            if (requestBodyString.HasErrors)
            {
                HTTPResponseBuilder = requestBodyString.Error;
                return false;
            }

            #endregion

            Text = requestBodyString.Data;

            return true;

        }

        #endregion

        #region TryParseJSONRequestBody      (this Request, out JSONArray, out JSONObject, out HTTPResponseBuilder, AllowEmptyHTTPBody = false, JSONLDContext = null)

        /// <summary>
        /// Return the HTTP request body as JSON array or object.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="JSONArray">The HTTP request body as a JSON array.</param>
        /// <param name="JSONObject">The HTTP request body as a JSON object.</param>
        /// <param name="HTTPResponseBuilder">An HTTP error response builder.</param>
        /// <param name="AllowEmptyHTTPBody">Allow the HTTP request body to be empty!</param>
        /// <param name="JSONLDContext">An optional JSON-LD context for HTTP error responses.</param>
        public static Boolean TryParseJSONRequestBody(this HTTPRequest          Request,
                                                      out JArray?               JSONArray,
                                                      out JObject?              JSONObject,
                                                      ref HTTPResponse.Builder  HTTPResponseBuilder,
                                                      Boolean                   AllowEmptyHTTPBody   = false,
                                                      String?                   JSONLDContext        = null)
        {

            #region Allow empty HTTP body?

            JSONArray   = null;
            JSONObject  = null;

            if (Request.ContentLength == 0 && AllowEmptyHTTPBody)
                return true;

            #endregion

            #region Get text body

            var httpResult = Request.GetRequestBodyAsUTF8String(HTTPContentType.Application.JSON_UTF8,
                                                                AllowEmptyHTTPBody);

            if (httpResult.HasErrors    ||
                httpResult.Data is null ||
                httpResult.Data.IsNullOrEmpty())
            {
                return false;
            }

            #endregion

            #region Try to parse the JSON array or object

            try
            {

                var json = httpResult.Data.Trim();

                if (json.StartsWith('['))
                    JSONArray  = JArray. Parse(json);
                else
                    JSONObject = JObject.Parse(json);

            }
            catch (Exception e)
            {

                HTTPResponseBuilder.Content = Illias.JSONObject.Create(

                                                  JSONLDContext.IsNotNullOrEmpty()
                                                      ? new JProperty("context",      JSONLDContext?.ToString())
                                                      : null,

                                                        new JProperty("description",  "Invalid JSON in request body!"),
                                                        new JProperty("exception",    e.Message),
                                                        new JProperty("source",       httpResult.Data)

                                              ).ToUTF8Bytes();

                return false;

            }

            return true;

            #endregion

        }

        #endregion

        #region TryParseJSONArrayRequestBody (this Request, out JSONArray,                 out HTTPResponseBuilder, AllowEmptyHTTPBody = false, JSONLDContext = null)

        /// <summary>
        /// Return the HTTP request body as JSON array.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="JSONArray">The HTTP request body as a JSON array.</param>
        /// <param name="HTTPResponseBuilder">An HTTP error response builder.</param>
        /// <param name="AllowEmptyHTTPBody">Allow the HTTP request body to be empty!</param>
        /// <param name="JSONLDContext">An optional JSON-LD context for HTTP error responses.</param>
        public static Boolean TryParseJSONArrayRequestBody(this HTTPRequest                                Request,
                                                           [NotNullWhen(true)]  out JArray?                JSONArray,
                                                           [NotNullWhen(false)] out HTTPResponse.Builder?  HTTPResponseBuilder,
                                                           Boolean                                         AllowEmptyHTTPBody   = false,
                                                           String?                                         JSONLDContext        = null)
        {

            #region Allow empty HTTP body?

            JSONArray            = null;
            HTTPResponseBuilder  = null;

            if (Request.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                HTTPResponseBuilder = HTTPResponse.OK(Request);
                return false;
            }

            #endregion

            #region Get text body

            var httpResult = Request.GetRequestBodyAsUTF8String(HTTPContentType.Application.JSON_UTF8,
                                                                AllowEmptyHTTPBody);

            if (httpResult.HasErrors    ||
                httpResult.Data is null ||
                httpResult.Data.IsNullOrEmpty())
            {
                HTTPResponseBuilder = httpResult.Error;
                return false;
            }

            var json = httpResult.Data.Trim();

            #endregion

            #region Try to parse the JSON array

            try
            {

                JSONArray = JArray.Parse(json);

            }
            catch (Exception e)
            {

                HTTPResponseBuilder = new HTTPResponse.Builder(Request) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          JSONLDContext.IsNotNullOrEmpty()
                                              ? new JProperty("context",  JSONLDContext?.ToString())
                                              : null,
                                          new JProperty("description",  "Invalid JSON array in request body!"),
                                          new JProperty("exception",    e.Message)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            return true;

            #endregion

        }

        #endregion

        #region TryParseJSONObjectRequestBody(this Request,                out JSONObject, out HTTPResponseBuilder, AllowEmptyHTTPBody = false, JSONLDContext = null)

        /// <summary>
        /// Return the HTTP request body as JSON object.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="JSONObject">The HTTP request body as a JSON object.</param>
        /// <param name="HTTPResponseBuilder">An HTTP error response builder.</param>
        /// <param name="AllowEmptyHTTPBody">Allow the HTTP request body to be empty!</param>
        /// <param name="JSONLDContext">An optional JSON-LD context for HTTP error responses.</param>
        public static Boolean TryParseJSONObjectRequestBody(this HTTPRequest                                Request,
                                                            [NotNullWhen(true)]  out JObject?               JSONObject,
                                                            [NotNullWhen(false)] out HTTPResponse.Builder?  HTTPResponseBuilder,
                                                            Boolean                                         AllowEmptyHTTPBody   = false,
                                                            String?                                         JSONLDContext        = null)
        {

            #region Allow empty HTTP body?

            JSONObject           = null;
            HTTPResponseBuilder  = null;

            if (Request.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                HTTPResponseBuilder = HTTPResponse.OK(Request);
                return false;
            }

            #endregion

            #region Get text body

            var httpResult = Request.GetRequestBodyAsUTF8String(HTTPContentType.Application.JSON_UTF8,
                                                                AllowEmptyHTTPBody);

            if (httpResult.HasErrors    ||
                httpResult.Data is null ||
                httpResult.Data.IsNullOrEmpty())
            {
                HTTPResponseBuilder = httpResult.Error;
                return false;
            }

            var text = httpResult.Data.Trim();

            #endregion

            #region Try to parse the JSON object

            try
            {

                JSONObject = JObject.Parse(text);

            }
            catch (Exception e)
            {

                HTTPResponseBuilder  = new HTTPResponse.Builder(Request) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = Illias.JSONObject.Create(
                                          JSONLDContext.IsNotNullOrEmpty()
                                              ? new JProperty("context",  JSONLDContext?.ToString())
                                              : null,
                                          new JProperty("description",  "Invalid JSON object in request body!"),
                                          new JProperty("exception",    e.Message),
                                          new JProperty("source",       httpResult.Data)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            return true;

            #endregion

        }


        /// <summary>
        /// Return the HTTP request body as JSON object.
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="JSONObject">The HTTP request body as a JSON object.</param>
        /// <param name="HTTPResponseBuilder">An HTTP error response builder.</param>
        /// <param name="AllowEmptyHTTPBody">Allow the HTTP request body to be empty!</param>
        /// <param name="JSONLDContext">An optional JSON-LD context for HTTP error responses.</param>
        public static Boolean TryParseJSONObjectRequestBody2(this HTTPRequest          Request,
                                                             out JObject               JSONObject,
                                                             ref HTTPResponse.Builder  HTTPResponseBuilder,
                                                             Boolean                   AllowEmptyHTTPBody   = false,
                                                             String?                   JSONLDContext        = null)
        {

            #region Allow empty HTTP body?

            if (Request.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                JSONObject = new JObject();
                return false;
            }

            #endregion

            #region Get text body

            var httpResult = Request.GetRequestBodyAsUTF8String(HTTPContentType.Application.JSON_UTF8,
                                                                AllowEmptyHTTPBody);

            if (httpResult.HasErrors    ||
                httpResult.Data is null ||
                httpResult.Data.IsNullOrEmpty())
            {
                JSONObject = new JObject();
                return false;
            }

            #endregion

            #region Try to parse the JSON object

            try
            {

                JSONObject = JObject.Parse(httpResult.Data.Trim());

            }
            catch (Exception e)
            {

                HTTPResponseBuilder.Content = Illias.JSONObject.Create(

                                                  JSONLDContext.IsNotNullOrEmpty()
                                                      ? new JProperty("context",      JSONLDContext?.ToString())
                                                      : null,

                                                        new JProperty("description",  "Invalid JSON object in request body!"),
                                                        new JProperty("exception",    e.Message),
                                                        new JProperty("source",       httpResult.Data)

                                              ).ToUTF8Bytes();

                JSONObject = new JObject();
                return false;

            }

            #endregion

            return true;

        }


        #endregion


        #region ParseXMLRequestBody(this Request, ContentType = null)

        public static HTTPResult<XDocument> ParseXMLRequestBody(this HTTPRequest  Request,
                                                                HTTPContentType?  ContentType   = null)
        {

            var requestBodyString = Request.GetRequestBodyAsUTF8String(ContentType ?? HTTPContentType.Text.XML_UTF8);
            if (requestBodyString.HasErrors)
                return new HTTPResult<XDocument>(requestBodyString.Error);

            try
            {
                return new HTTPResult<XDocument>(XDocument.Parse(requestBodyString.Data));
            }
            catch (Exception e)
            {
                return new HTTPResult<XDocument>(Request, HTTPStatusCode.BadRequest);
            }

        }

        #endregion

        #region TryParseXMLRequestBody(this Request, out XMLDocument, out HTTPResponseBuilder, ContentType = null, AllowEmptyHTTPBody = false)

        public static Boolean TryParseXMLRequestBody(this HTTPRequest                                Request,
                                                     [NotNullWhen(true)]  out XDocument?             XMLDocument,
                                                     [NotNullWhen(false)] out HTTPResponse.Builder?  HTTPResponseBuilder,
                                                     HTTPContentType?                                ContentType          = null,
                                                     Boolean                                         AllowEmptyHTTPBody   = false)
        {

            #region Allow empty HTTP body?

            XMLDocument          = null;
            HTTPResponseBuilder  = null;

            if (Request.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                HTTPResponseBuilder = HTTPResponse.OK(Request);
                return false;
            }

            #endregion

            #region Get text body

            var httpResult = Request.GetRequestBodyAsUTF8String(ContentType ?? HTTPContentType.Application.XML_UTF8,
                                                                AllowEmptyHTTPBody);

            if (httpResult.HasErrors    ||
                httpResult.Data is null ||
                httpResult.Data.IsNullOrEmpty())
            {
                HTTPResponseBuilder = httpResult.Error;
                return false;
            }

            var text = httpResult.Data.Trim();

            #endregion

            #region Try to parse the XML document

            try
            {

                XMLDocument = XDocument.Parse(text);

            }
            catch (Exception e)
            {

                HTTPResponseBuilder = new HTTPResponse.Builder(Request) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description",  "Invalid JSON object in request body!"),
                                          new JProperty("exception",    e.Message),
                                          new JProperty("source",       httpResult.Data)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            return true;

            #endregion

        }

        #endregion


        #region TryParseMultipartFormDataRequestBody(this Request, MimeMultipart, Response)

        public static Boolean TryParseMultipartFormDataRequestBody(this HTTPRequest                        Request,
                                                                   [NotNullWhen(true)]  out Multipart?     MimeMultipart,
                                                                   [NotNullWhen(false)] out HTTPResponse?  Response)
        {

            #region Initial checks

            if (Request.ContentType     != HTTPContentType.Multipart.FORMDATA ||
                Request.ContentLength   == 0                                  ||
               !Request.TryReadHTTPBodyStream()                               ||
                Request.HTTPBody        is null                               ||
                Request.HTTPBody.Length == 0)
            {

                MimeMultipart  = null;
                Response       = HTTPResponse.BadRequest(Request);

                return false;

            }

            #endregion

            Response       = null;
            MimeMultipart  = Multipart.Parse(Request.HTTPBody,
                                             Request.ContentType.MIMEBoundary);

            return true;

        }

        #endregion

        #region TryParseI18NString(HTTPRequest, DescriptionJSON, out I18N, out HTTPResponse)

        public static Boolean TryParseI18NString(HTTPRequest                             HTTPRequest,
                                                 JObject                                 DescriptionJSON,
                                                 [NotNullWhen(true)]  out I18NString?    I18N,
                                                 [NotNullWhen(false)] out HTTPResponse?  HTTPResponse)
        {

            if (DescriptionJSON is null)
            {

                I18N          = null;

                HTTPResponse  = new HTTPResponse.Builder(HTTPRequest) {
                                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                                    Content         = new JObject(new JProperty("description", "Invalid roaming network description!")).ToUTF8Bytes()
                                }.AsImmutable;

                return false;

            }

            JValue Text;
            I18N = I18NString.Empty;

            foreach (var Description in DescriptionJSON)
            {

                if (!Enum.TryParse(Description.Key, out Languages Language))
                {

                    I18N          = null;

                    HTTPResponse  = new HTTPResponse.Builder(HTTPRequest) {
                                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                        ContentType     = HTTPContentType.Application.JSON_UTF8,
                                        Content         = new JObject(new JProperty("description", "Unknown or invalid language definition '" + Description.Key + "'!")).ToUTF8Bytes()
                                    }.AsImmutable;

                    return false;

                }

                Text = Description.Value as JValue;

                if (Text is null)
                {

                    I18N          = null;

                    HTTPResponse  = new HTTPResponse.Builder(HTTPRequest) {
                                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                        ContentType     = HTTPContentType.Application.JSON_UTF8,
                                        Content         = new JObject(new JProperty("description", "Invalid description text!")).ToUTF8Bytes()
                                    }.AsImmutable;

                    return false;

                }

                I18N.Set(Language, Text.Value<String>());

            }

            HTTPResponse = null;
            return true;

        }

        #endregion


        #region Reply(this HTTPRequest)

        /// <summary>
        /// Create a new HTTP response builder for the given request.
        /// </summary>
        /// <param name="HTTPRequest">An HTTP request.</param>
        public static HTTPResponse.Builder Reply(this HTTPRequest  HTTPRequest,
                                                 HTTPStatusCode?   HTTPStatusCode = null)

            => new (HTTPRequest) {
                       HTTPStatusCode = HTTPStatusCode
                   };

        #endregion

    }


    /// <summary>
    /// An HTTP request.
    /// </summary>
    public partial class HTTPRequest : AHTTPPDU
    {

        #region Properties

        /// <summary>
        /// The HTTP server of this request.
        /// </summary>
        public HTTPServer?        HTTPServer                { get; }

        public X509Certificate2?  ServerCertificate         { get; }

        public X509Certificate2?  ClientCertificate         { get; }

        /// <summary>
        /// Add this prefix to the URL before sending the request.
        /// </summary>
        public String?            FakeURLPrefix             { get; internal set; }

        /// <summary>
        /// The best matching accept type.
        /// Set by the HTTP server.
        /// </summary>
        public HTTPContentType    BestMatchingAcceptType    { get; internal set; } = HTTPContentType.Application.OCTETSTREAM;


        public Object?            SubprotocolRequest        { get; set; }

        #endregion

        #region First request header line

        /// <summary>
        /// The HTTP method.
        /// </summary>
        public HTTPMethod                  HTTPMethod              { get; } = HTTPMethod.GET;

        /// <summary>
        /// The minimal URL (this means e.g. without the query string).
        /// </summary>
        public HTTPPath                    Path                    { get; private set; }

        /// <summary>
        /// The parsed URL parameters of the best matching URL template.
        /// Set by the HTTP server.
        /// </summary>
        public String[]                    ParsedURLParameters     { get; internal set; } = [];
        public Dictionary<String, String>  ParsedURLParametersX    { get; internal set; } = [];

        /// <summary>
        /// The HTTP query string.
        /// </summary>
        public QueryString                 QueryString             { get; }

        /// <summary>
        /// The HTTP protocol name field.
        /// </summary>
        public String                      ProtocolName            { get; }

        /// <summary>
        /// The HTTP protocol version.
        /// </summary>
        public HTTPVersion                 ProtocolVersion         { get; }

        #endregion

        #region Standard     request header fields

        #region Accept

        /// <summary>
        /// The http content types accepted by the client.
        /// </summary>
        public AcceptTypes Accept

            => GetHeaderField(HTTPRequestHeaderField.Accept) ?? [];

        #endregion

        #region Accept-Charset

        public String? AcceptCharset

            => GetHeaderField(HTTPRequestHeaderField.AcceptCharset);

        #endregion

        #region Accept-Encoding

        public String? AcceptEncoding

            => GetHeaderField(HTTPRequestHeaderField.AcceptEncoding);

        #endregion

        #region Accept-Language

        public String? AcceptLanguage

            => GetHeaderField(HTTPRequestHeaderField.AcceptLanguage);

        #endregion

        #region Accept-Ranges

        public String? AcceptRanges

            => GetHeaderField(HTTPRequestHeaderField.AcceptRanges);

        #endregion

        #region Authorization

        /// <summary>
        /// The HTTP basic authentication.
        /// </summary>
        public IHTTPAuthentication? Authorization

            => GetHeaderField(HTTPRequestHeaderField.Authorization);

        #endregion

        #region Depth

        public String? Depth

            => GetHeaderField(HTTPRequestHeaderField.Depth);

        #endregion

        #region Destination

        public String? Destination

            => GetHeaderField(HTTPRequestHeaderField.Destination);

        #endregion

        #region Expect

        public String? Expect

            => GetHeaderField(HTTPRequestHeaderField.Expect);

        #endregion

        #region From

        public String? From

            => GetHeaderField(HTTPRequestHeaderField.From);

        #endregion

        #region Host

        public HTTPHostname Host

            => GetHeaderField(HTTPRequestHeaderField.Host);

        #endregion

        #region If

        public String? If

            => GetHeaderField(HTTPRequestHeaderField.If);

        #endregion

        #region If-Match

        public String? IfMatch

            => GetHeaderField(HTTPRequestHeaderField.IfMatch);

        #endregion

        #region If-Modified-Since

        public String? IfModifiedSince

            => GetHeaderField(HTTPRequestHeaderField.IfModifiedSince);

        #endregion

        #region If-None-Match

        public String? IfNoneMatch

            => GetHeaderField(HTTPRequestHeaderField.IfNoneMatch);

        #endregion

        #region If-Range

        public String? IfRange

            => GetHeaderField(HTTPRequestHeaderField.IfRange);

        #endregion

        #region If-Unmodified-Since

        public String? IfUnmodifiedSince

            => GetHeaderField(HTTPRequestHeaderField.IfUnmodifiedSince);

        #endregion

        #region Lock-Token

        public String? LockToken

            => GetHeaderField(HTTPRequestHeaderField.LockToken);

        #endregion

        #region MaxForwards

        public UInt64? MaxForwards

            => GetHeaderField(HTTPRequestHeaderField.MaxForwards);

        #endregion

        #region Overwrite

        public String? Overwrite

            => GetHeaderField(HTTPRequestHeaderField.Overwrite);

        #endregion

        #region Proxy-Authorization

        public String? ProxyAuthorization

            => GetHeaderField(HTTPRequestHeaderField.ProxyAuthorization);

        #endregion

        #region Range

        public String? Range

            => GetHeaderField(HTTPRequestHeaderField.Range);

        #endregion

        #region Referer

        public String? Referer

            => GetHeaderField(HTTPRequestHeaderField.Referer);

        #endregion

        #region TE

        public String? TE

            => GetHeaderField(HTTPRequestHeaderField.TE);

        #endregion

        #region Timeout

        public TimeSpan? Timeout

            => GetHeaderField(HTTPRequestHeaderField.Timeout);

        #endregion

        #region User-Agent

        public String? UserAgent

            => GetHeaderField(HTTPRequestHeaderField.UserAgent);

        #endregion

        #region Last-Event-Id

        public UInt64? LastEventId

            => GetHeaderField(HTTPRequestHeaderField.LastEventId);

        #endregion

        #region Cookie

        /// <summary>
        /// HTTP cookies.
        /// </summary>
        public HTTPCookies? Cookies

            => GetHeaderField(HTTPRequestHeaderField.Cookie);

        #endregion

        #region DNT

        /// <summary>
        /// Do Not Track
        /// </summary>
        public Boolean DNT

            => GetHeaderField(HTTPRequestHeaderField.DNT);

        #endregion

        #region SecWebSocketKey

        public String SecWebSocketKey
            => GetHeaderField(HTTPHeaderField.SecWebSocketKey);

        #endregion

        #region Sec-WebSocket-Protocol

        /// <summary>
        /// Sec-WebSocket-Protocol
        /// </summary>
        public IEnumerable<String> SecWebSocketProtocol

            => GetHeaderFields(HTTPHeaderField.SecWebSocketProtocol_Request) ?? [];

        #endregion

        #endregion

        #region Non-standard request header fields

        #region X-Real-IP

        /// <summary>
        /// Intermediary HTTP proxies might include this field to
        /// indicate the real IP address of the HTTP client.
        /// </summary>
        /// <example>X-Real-IP: 95.91.73.30</example>
        public IIPAddress? X_Real_IP

            => GetHeaderField(HTTPRequestHeaderField.X_Real_IP);

        #endregion

        #region X-Forwarded-For

        /// <summary>
        /// Intermediary HTTP proxies might include this field to
        /// indicate the real IP address of the HTTP client.
        /// </summary>
        /// <example>X-Forwarded-For: client, proxy1, proxy2</example>
        public IEnumerable<IIPAddress> X_Forwarded_For

            => GetHeaderFields(HTTPRequestHeaderField.X_Forwarded_For) ?? [];

        #endregion

        //ToDo: Forwarded / RFC 7239

        #region API-Key

        /// <summary>
        /// An optional API key for authentication.
        /// </summary>
        /// <example>API-Key: vfsf87wefh8743tzfgw9f489fh9fgs9z9z237hd208du79ehcv86egfsrf</example>
        public APIKey_Id? API_Key

            => GetHeaderField(HTTPRequestHeaderField.API_Key);

        #endregion

        #region X-Portal

        /// <summary>
        /// This is a non-standard HTTP header to idicate that the intended
        /// HTTP portal is calling. By this a special HTTP content type processing
        /// might be implemented, which is different from the processing of other
        /// HTTP client requests.
        /// </summary>
        /// <example>X-Portal: true</example>
        public Boolean X_Portal

            => GetHeaderField(HTTPRequestHeaderField.X_Portal);

        #endregion

        #endregion

        #region Internal     request header fields

        /// <summary>
        /// Construct the entire HTTP request header.
        /// </summary>
        public String                                                  EntireRequestHeader

            => $"{HTTPMethod} {FakeURLPrefix}{Path}{QueryString} {ProtocolName}/{ProtocolVersion}\r\n{ConstructedHTTPHeader}";

        public NetworkStream?                                          NetworkStream           { get; internal set; }
        public Func<HTTPRequest, ChunkedTransferEncodingStream, Task>  ChunkWorker             { get; set; } = (request, stream) => Task.CompletedTask;


        #region User

        /// <summary>
        /// This is an internal HTTP request field to idicate the HTTP user.
        /// </summary>
        public IUser? User { get; set; }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPRequest(Timestamp, HTTPSource, LocalSocket, RemoteSocket, HTTPHeader, HTTPBody = null, HTTPBodyStream = null, HTTPServer = null, ...)

        /// <summary>
        /// Create a new http request header based on the given string representation.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="HTTPSource">The HTTP source.</param>
        /// <param name="LocalSocket">The local TCP/IP socket.</param>
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        /// <param name="HTTPBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPBodyStream">The HTTP body as an stream of bytes.</param>
        /// <param name="HTTPServer">An optional HTTP server that has received this request.</param>
        /// 
        /// <param name="HTTPBodyReceiveBufferSize">The size of the HTTP body receive buffer.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        public HTTPRequest(DateTimeOffset               Timestamp,
                           HTTPSource                   HTTPSource,
                           IPSocket                     LocalSocket,
                           IPSocket                     RemoteSocket,

                           String                       HTTPHeader,
                           HTTPMethod                   HTTPMethod,
                           HTTPPath                     Path,
                           String                       ProtocolName,
                           HTTPVersion                  ProtocolVersion,
                           Dictionary<String, Object?>  HTTPHeaderFields,

                           QueryString?                 QueryString                 = null,
                           Byte[]?                      HTTPBody                    = null,
                           Stream?                      HTTPBodyStream              = null,
                           HTTPServer?                  HTTPServer                  = null,
                           X509Certificate2?            ServerCertificate           = null,
                           X509Certificate2?            ClientCertificate           = null,

                           UInt32                       HTTPBodyReceiveBufferSize   = DefaultHTTPBodyReceiveBufferSize,
                           EventTracking_Id?            EventTrackingId             = null,
                           CancellationToken            CancellationToken           = default)

            : base(Timestamp,
                   HTTPSource,
                   LocalSocket,
                   RemoteSocket,
                   HTTPHeader,
                   HTTPBody,
                   HTTPBodyStream,
                   HTTPBodyReceiveBufferSize,

                   EventTrackingId,
                   CancellationToken)

        {

            this.HTTPServer         = HTTPServer;
            this.ServerCertificate  = ServerCertificate;
            this.ClientCertificate  = ClientCertificate;

            this.HTTPMethod         = HTTPMethod;
            this.Path               = Path;
            this.ProtocolName       = ProtocolName;
            this.ProtocolVersion    = ProtocolVersion;
            this.QueryString        = QueryString ?? QueryString.Empty;

            this.headerFields.Clear();
            foreach (var kvp in HTTPHeaderFields)
                this.headerFields.Add(kvp.Key, kvp.Value);

        }

        #endregion


        #region (internal) HTTPRequest(Timestamp, HTTPSource, LocalSocket, RemoteSocket, HTTPHeader, HTTPBody = null, HTTPBodyStream = null, HTTPServer = null, ...)

        /// <summary>
        /// Create a new http request header based on the given string representation.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="HTTPSource">The HTTP source.</param>
        /// <param name="LocalSocket">The local TCP/IP socket.</param>
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        /// <param name="HTTPBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPBodyStream">The HTTP body as an stream of bytes.</param>
        /// <param name="HTTPServer">An optional HTTP server that has received this request.</param>
        /// 
        /// <param name="HTTPBodyReceiveBufferSize">The size of the HTTP body receive buffer.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        internal HTTPRequest(DateTimeOffset     Timestamp,
                             HTTPSource         HTTPSource,
                             IPSocket           LocalSocket,
                             IPSocket           RemoteSocket,

                             String             HTTPHeader,
                             Byte[]?            HTTPBody                    = null,
                             Stream?            HTTPBodyStream              = null,
                             HTTPServer?        HTTPServer                  = null,
                             X509Certificate2?  ServerCertificate           = null,
                             X509Certificate2?  ClientCertificate           = null,

                             UInt32             HTTPBodyReceiveBufferSize   = DefaultHTTPBodyReceiveBufferSize,
                             EventTracking_Id?  EventTrackingId             = null,
                             CancellationToken  CancellationToken           = default)

            : base(Timestamp,
                   HTTPSource,
                   LocalSocket,
                   RemoteSocket,
                   HTTPHeader,
                   HTTPBody,
                   HTTPBodyStream,
                   HTTPBodyReceiveBufferSize,

                   EventTrackingId,
                   CancellationToken)

        {

            this.HTTPServer         = HTTPServer;
            this.ServerCertificate  = ServerCertificate;
            this.ClientCertificate  = ClientCertificate;


            // 1st line of the request

            #region Parse method

            var httpMethodHeader    = FirstPDULine.Split(spaceSeparator,
                                                         StringSplitOptions.RemoveEmptyEntries);

            // e.g: PROPFIND /file/file Name HTTP/1.1
            if (httpMethodHeader.Length != 3)
                throw new Exception("Invalid first HTTP PDU line!");

            // Parse HTTP method
            // Probably not useful to define here, as we can not send a response having an "Allow-header" here!
            this.HTTPMethod = HTTPMethod.TryParse(httpMethodHeader[0]) ??
                                  throw new Exception("Invalid HTTP method!");

            #endregion

            #region Parse protocol name and -version

            var protocolArray       = httpMethodHeader[2].Split(slashSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
            this.ProtocolName       = protocolArray[0].ToUpper();

            if (!String.Equals(ProtocolName, "HTTP", StringComparison.CurrentCultureIgnoreCase))
                throw new Exception("Invalid protocol!");

            if (HTTPVersion.TryParse(protocolArray[1], out var httpVersion))
                this.ProtocolVersion  = httpVersion;

            if (ProtocolVersion != HTTPVersion.HTTP_1_0 && ProtocolVersion != HTTPVersion.HTTP_1_1)
                throw new Exception("HTTP version not supported!");

            #endregion

            #region Parse Path

            var rawURL      = httpMethodHeader[1];
            var parsedURL   = rawURL.Split(urlSeparator, 2, StringSplitOptions.None);
            this.Path       = HTTPPath.Parse(parsedURL[0]);

            //if (URL.StartsWith("http", StringComparison.Ordinal) || URL.StartsWith("https", StringComparison.Ordinal))
            if (Path.Contains("://"))
            {
                Path = Path.Substring(Path.IndexOf("://", StringComparison.Ordinal) + 3);
                Path = Path.Substring(Path.IndexOf("/",   StringComparison.Ordinal));
            }

            if (Path == "")
                Path = HTTPPath.Root;

            #endregion

            #region Parse QueryString (optional)

            // Parse QueryString after '?'
            if (rawURL.IndexOf('?') > -1 && parsedURL[1].IsNeitherNullNorEmpty())
                QueryString = QueryString.Parse(parsedURL[1]);
            else
                QueryString = QueryString.Empty;

            #endregion


            // 2nd line of the request

            #region Check Host header

            // rfc 2616 - Section 19.6.1.1
            // A client that sends an HTTP/1.1 request MUST send a Host header.

            // rfc 2616 - Section 14.23
            // All Internet-based HTTP/1.1 servers MUST respond with a 400 (Bad Request)
            // status code to any HTTP/1.1 request message which lacks a Host header field.

            // rfc 2616 - Section 5.2 The Resource Identified by a Request
            // 1. If Request-URL is an absoluteURL, the host is part of the Request-URL.
            //    Any Host header field value in the request MUST be ignored.
            // 2. If the Request-URL is not an absoluteURL, and the request includes a
            //    Host header field, the host is determined by the Host header field value.
            // 3. If the host as determined by rule 1 or 2 is not a valid host on the server,
            //    the response MUST be a 400 (Bad Request) error message. (Not valid for proxies?!)
            if (!headerFields.TryGetValue(HTTPRequestHeaderField.Host.Name, out var hostHeaderRAW) ||
                hostHeaderRAW is not String hostHeaderString)
            {
                throw new Exception("The HTTP request must have have a valid HOST header!");
            }

            // rfc 2616 - 3.2.2
            // If the port is empty or not given, port 80 is assumed.
            var hostHeader   = hostHeaderString.
                                   Replace(":*", "").
                                   Split  (colonSeparator, StringSplitOptions.RemoveEmptyEntries).
                                   Select (v => v.Trim()).
                                   ToArray();


            //if (hostHeader.Length == 1)
            //    headerFields[HTTPRequestHeaderField.Host.Name] = headerFields[HTTPRequestHeaderField.Host.Name].ToString();// + ":80"; ":80" will cause side effects!

            if (hostHeader.Length == 2 && !UInt16.TryParse(hostHeader[1], out var hostPort))
                throw new Exception("Invalid HTTP port in host header!");

            if (hostHeader.Length  > 2)
                throw new Exception("Invalid HTTP host header!");

            #endregion

        }

        #endregion

        #region (internal) HTTPRequest(Request)

        /// <summary>
        /// Create a new HTTP request based on the given HTTP request.
        /// (e.g. upgrade a HTTPRequest to a HTTPRequest&lt;TContent&gt;)
        /// </summary>
        /// <param name="Request">An HTTP request.</param>
        internal HTTPRequest(HTTPRequest Request)

            : base(Request)

        {

            ProtocolName            = Request.ProtocolName;
            ParsedURLParameters     = Request.ParsedURLParameters;
            QueryString             = Request.QueryString;
            BestMatchingAcceptType  = Request.BestMatchingAcceptType;

        }

        #endregion

        #endregion


        #region TryGetURLParameter   (ParameterName)

        public String? TryGetURLParameter(String ParameterName)
        {

            if (ParsedURLParametersX.TryGetValue(ParameterName, out var value) &&
                value.IsNotNullOrEmpty())
            {
                return HTTPTools.URLDecode(value.Trim());
            }

            return default;

        }

        #endregion

        #region TryGetURLParameter   (ParameterName, out Value)

        public Boolean TryGetURLParameter(String                           ParameterName,
                                          [NotNullWhen(true)] out String?  Value)
        {

            if (ParsedURLParametersX.TryGetValue(ParameterName, out var value) &&
                value.IsNotNullOrEmpty())
            {
                Value = HTTPTools.URLDecode(value.Trim());
                return true;
            }

            Value = default;
            return false;

        }

        #endregion

        #region TryGetURLParameter   (ParameterName, out Value, out ErrorResponse)

        public Boolean TryGetURLParameter(String                            ParameterName,
                                          [NotNullWhen(true)]  out String?  Value,
                                          [NotNullWhen(false)] out String?  ErrorResponse)
        {

            ErrorResponse  = null;
            Value          = default;

            if (ParsedURLParametersX.TryGetValue(ParameterName, out var value) &&
                value.IsNotNullOrEmpty())
            {
                Value = HTTPTools.URLDecode(value.Trim());
                return true;
            }

            ErrorResponse  = $"The given URL parameter '{ParameterName}' does not exist!";
            return false;

        }

        #endregion


        #region TryParseURLParameter (ParameterName, TryParser)

        public T? TryParseURLParameter<T>(String        ParameterName,
                                          TryParser<T>  TryParser)
        {

            if (ParsedURLParametersX.TryGetValue(ParameterName, out var value) &&
                value.IsNotNullOrEmpty() &&
                TryParser(HTTPTools.URLDecode(value.Trim()), out var valueT))
            {
                return valueT;
            }

            return default;

        }

        #endregion

        #region TryParseURLParameter (ParameterName, TryParser, out Value)

        public Boolean TryParseURLParameter<T>(String                       ParameterName,
                                               TryParser<T>                 TryParser,
                                               [NotNullWhen(true)]  out T?  Value)
        {

            if (ParsedURLParametersX.TryGetValue(ParameterName, out var value) &&
                TryParser(HTTPTools.URLDecode(value.Trim()), out var valueT))
            {
                Value = valueT;
                return true;
            }

            Value = default;
            return false;

        }

        #endregion

        #region TryParseURLParameter (ParameterName, TryParser, out Value, out ErrorResponse)

        public Boolean TryParseURLParameter<T>(String                            ParameterName,
                                               TryParser2<T>                     TryParser,
                                               [NotNullWhen(true)]  out T?       Value,
                                               [NotNullWhen(false)] out String?  ErrorResponse)
        {

            ErrorResponse  = null;
            Value          = default;

            if (ParsedURLParametersX.TryGetValue(ParameterName, out var value))
            {

                if (TryParser(HTTPTools.URLDecode(value.Trim()), out var valueT, out var errorResponse) &&
                    value.IsNotNullOrEmpty())
                {
                    Value = valueT;
                    return true;
                }

                ErrorResponse = errorResponse ?? $"The given URL parameter '{ParameterName}' does not exist!";
                return false;

            }

            ErrorResponse  = $"The given URL parameter '{ParameterName}' does not exist!";
            return false;

        }

        #endregion



        public Task<IEnumerable<(String, String)>>

            ReadAllChunks(Action<HTTPRequestChunk>  OnChunkReceived,
                          CancellationToken         CancellationToken   = default)

        {

            if (HTTPBodyStream is ChunkedTransferEncodingStream chunkedStream)
                return chunkedStream.ReadAllChunks(
                           (timestamp, elapsed, counter, data) => OnChunkReceived(new HTTPRequestChunk(this, timestamp, elapsed, counter, data)),
                           CancellationToken
                       );

            return Task.FromResult<IEnumerable<(String, String)>>([]);

        }


        public static HTTPRequest ChangePath(HTTPRequest             Request,
                                             HTTPPath                NewPath,
                                             Tuple<String, Object>?  SetHeaderField = null)
        {

            var headerFields = Request.headerFields;

            if (SetHeaderField is not null)
                headerFields[SetHeaderField.Item1] = SetHeaderField.Item2;


            return new (Request.Timestamp,
                        Request.HTTPSource,
                        Request.LocalSocket,
                        Request.RemoteSocket,

                        Request.RawHTTPHeader,
                        Request.HTTPMethod,
                        NewPath,
                        Request.ProtocolName,
                        Request.ProtocolVersion,
                        headerFields,

                        Request.QueryString,
                        Request.HTTPBody,
                        Request.HTTPBodyStream,
                        Request.HTTPServer,
                        Request.ServerCertificate,
                        Request.ClientCertificate,

                        Request.HTTPBodyReceiveBufferSize,
                        Request.EventTrackingId,
                        Request.CancellationToken);

        }


        #region Parse(Timestamp, HTTPSource, LocalSocket, RemoteSocket, HTTPHeader, HTTPBody = null, HTTPBodyStream = null, HTTPServer = null, ...)

        /// <summary>
        /// Create a new http request header based on the given string representation.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="HTTPSource">The HTTP source.</param>
        /// <param name="LocalSocket">The local TCP/IP socket.</param>
        /// <param name="RemoteSocket">The remote TCP/IP socket.</param>
        /// <param name="HTTPHeader">A valid string representation of a http request header.</param>
        /// <param name="HTTPBody">The HTTP body as an array of bytes.</param>
        /// <param name="HTTPBodyStream">The HTTP body as an stream of bytes.</param>
        /// <param name="HTTPServer">An optional HTTP server that has received this request.</param>
        /// 
        /// <param name="HTTPBodyReceiveBufferSize">The size of the HTTP body receive buffer.</param>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        public static HTTPRequest Parse(DateTimeOffset     Timestamp,
                                        HTTPSource         HTTPSource,
                                        IPSocket           LocalSocket,
                                        IPSocket           RemoteSocket,

                                        String             HTTPHeader,
                                        Byte[]?            HTTPBody                    = null,
                                        Stream?            HTTPBodyStream              = null,
                                        HTTPServer?        HTTPServer                  = null,
                                        X509Certificate2?  ServerCertificate           = null,
                                        X509Certificate2?  ClientCertificate           = null,

                                        UInt32             HTTPBodyReceiveBufferSize   = DefaultHTTPBodyReceiveBufferSize,
                                        EventTracking_Id?  EventTrackingId             = null,
                                        CancellationToken  CancellationToken           = default)
        {

            #region Process first line...

            var allLines = HTTPHeader.Split(lineSeparator,
                                            StringSplitOptions.RemoveEmptyEntries);

            if (allLines is null || allLines.Length < 2)
                throw new Exception("Bad request");

            var firstPDULine = allLines.First();
            if (firstPDULine.IsNullOrEmpty())
                throw new Exception("Bad request");

            #endregion

            #region ...process all other header lines

            var headerFields = new Dictionary<String, Object?>();

            foreach (var headerLine in allLines.Skip(1))
            {

                if (headerLine.IsNullOrEmpty())
                    break;

                var keyValuePair = headerLine.Split(colonSeparator, 2);

                // Not valid for every HTTP header... but at least for most...
                if (keyValuePair.Length == 1)
                    headerFields.Add(keyValuePair[0].Trim(), String.Empty);

                else
                {

                    var key = keyValuePair[0].Trim();

                    if (key.IsNotNullOrEmpty())
                    {

                        if (!headerFields.ContainsKey(key))
                            headerFields.Add(key, keyValuePair[1].Trim());

                        else
                        {

                            if (headerFields[key] is String existingValue)
                                headerFields[key] = new[] { existingValue, keyValuePair[1].Trim() };

                            else if (headerFields[key] is String[] existingValues)
                                headerFields[key] = existingValues.Append(keyValuePair[1].Trim()).ToArray();

                        }

                    }

                }

            }

            #endregion


            // 1st line of the request

            #region Parse method

            var httpMethodHeader    = firstPDULine.Split(spaceSeparator,
                                                         StringSplitOptions.RemoveEmptyEntries);

            // e.g: PROPFIND /file/file Name HTTP/1.1
            if (httpMethodHeader.Length != 3)
                throw new Exception("Invalid first HTTP PDU line!");

            // Parse HTTP method
            // Probably not useful to define here, as we can not send a response having an "Allow-header" here!
            var httpMethod = HTTPMethod.TryParse(httpMethodHeader[0]) ??
                                  throw new Exception("Invalid HTTP method!");

            #endregion

            #region Parse protocol name and -version

            var protocolArray      = httpMethodHeader[2].Split(slashSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
            var protocolName       = protocolArray[0].ToUpper();

            if (!String.Equals(protocolName, "HTTP", StringComparison.CurrentCultureIgnoreCase))
                throw new Exception("Invalid protocol!");

            if (!HTTPVersion.TryParse(protocolArray[1], out var httpProtocolVersion))
                throw new Exception("Invalid HTTP version!");

            if (httpProtocolVersion != HTTPVersion.HTTP_1_0 && httpProtocolVersion != HTTPVersion.HTTP_1_1)
                throw new Exception("HTTP version not supported!");

            #endregion

            #region Parse Path

            var rawURL     = httpMethodHeader[1];
            var parsedURL  = rawURL.Split(urlSeparator, 2, StringSplitOptions.None);
            var path       = HTTPPath.Parse(parsedURL[0]);

            //if (URL.StartsWith("http", StringComparison.Ordinal) || URL.StartsWith("https", StringComparison.Ordinal))
            if (path.Contains("://"))
            {
                path = path.Substring(path.IndexOf("://", StringComparison.Ordinal) + 3);
                path = path.Substring(path.IndexOf("/",   StringComparison.Ordinal));
            }

            if (path == "")
                path = HTTPPath.Root;

            #endregion

            #region Parse QueryString (optional)

            QueryString? queryString = null;

            // Parse QueryString after '?'
            if (rawURL.IndexOf('?') > -1 && parsedURL[1].IsNeitherNullNorEmpty())
                queryString = QueryString.Parse(parsedURL[1]);
            else
                queryString = QueryString.Empty;

            #endregion


            // 2nd line of the request

            #region Check Host header

            // rfc 2616 - Section 19.6.1.1
            // A client that sends an HTTP/1.1 request MUST send a Host header.

            // rfc 2616 - Section 14.23
            // All Internet-based HTTP/1.1 servers MUST respond with a 400 (Bad Request)
            // status code to any HTTP/1.1 request message which lacks a Host header field.

            // rfc 2616 - Section 5.2 The Resource Identified by a Request
            // 1. If Request-URL is an absoluteURL, the host is part of the Request-URL.
            //    Any Host header field value in the request MUST be ignored.
            // 2. If the Request-URL is not an absoluteURL, and the request includes a
            //    Host header field, the host is determined by the Host header field value.
            // 3. If the host as determined by rule 1 or 2 is not a valid host on the server,
            //    the response MUST be a 400 (Bad Request) error message. (Not valid for proxies?!)
            if (!headerFields.TryGetValue(HTTPRequestHeaderField.Host.Name, out var hostHeaderRAW) ||
                hostHeaderRAW is not String hostHeaderString)
            {
                throw new Exception("The HTTP request must have have a valid HOST header!");
            }

            // rfc 2616 - 3.2.2
            // If the port is empty or not given, port 80 is assumed.
            var hostHeader   = hostHeaderString.
                                   Replace(":*", "").
                                   Split  (colonSeparator, StringSplitOptions.RemoveEmptyEntries).
                                   Select (v => v.Trim()).
                                   ToArray();


            //if (hostHeader.Length == 1)
            //    headerFields[HTTPRequestHeaderField.Host.Name] = headerFields[HTTPRequestHeaderField.Host.Name].ToString();// + ":80"; ":80" will cause side effects!

            if (hostHeader.Length == 2 && !UInt16.TryParse(hostHeader[1], out var hostPort))
                throw new Exception("Invalid HTTP port in host header!");

            if (hostHeader.Length  > 2)
                throw new Exception("Invalid HTTP host header!");

            #endregion


            var request = new HTTPRequest(

                              Timestamp,
                              HTTPSource,
                              LocalSocket,
                              RemoteSocket,

                              HTTPHeader,
                              httpMethod,
                              path,
                              protocolName,
                              httpProtocolVersion,
                              headerFields,

                              queryString,
                              HTTPBody,
                              HTTPBodyStream,
                              HTTPServer,
                              ServerCertificate,
                              ClientCertificate,

                              //HTTPBodyReceiveBufferSize,
                              EventTrackingId:    EventTrackingId,
                              CancellationToken:  CancellationToken

                          );

            var httpSources = request.X_Forwarded_For;
            if (httpSources.Any())
            {
                request.HTTPSource = new HTTPSource(
                    request.RemoteSocket,
                    httpSources.Skip(1)
                );
            }

            return request;

        }

        #endregion


        #region (static) TryParse(Text,        out Request, Timestamp = null, HTTPSource = null, LocalSocket = null, HTTPServer = null, ...)

        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCP socket of the request.</param>
        /// <param name="RemoteSocket">The optional remote TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        public static Boolean TryParse(String                                Text,
                                       [NotNullWhen(true)] out HTTPRequest?  Request,

                                       DateTimeOffset?                       Timestamp           = null,
                                       HTTPSource?                           HTTPSource          = null,
                                       IPSocket?                             LocalSocket         = null,
                                       IPSocket?                             RemoteSocket        = null,
                                       HTTPServer?                           HTTPServer          = null,

                                       EventTracking_Id?                     EventTrackingId     = null,
                                       CancellationToken                     CancellationToken   = default)
        {

            if (Text.IsNeitherNullNorEmpty())
            {

                try
                {

                    String? Header       = null;
                    Byte[]? Body         = null;
                    var     EndOfHeader  = Text.IndexOf("\r\n\r\n");

                    if (EndOfHeader == -1)
                        Header  = Text;

                    else
                    {

                        Header  = Text[..(EndOfHeader + 2)];

                        if (EndOfHeader + 4 < Text.Length)
                            Body  = Text[(EndOfHeader + 4)..].ToUTF8Bytes();

                    }


                    Request = new HTTPRequest(
                                  Timestamp    ?? Illias.Timestamp.Now,
                                  HTTPSource   ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                  LocalSocket  ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                  RemoteSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),

                                  Header,
                                  Body,

                                  HTTPServer:         HTTPServer,
                                  EventTrackingId:    EventTrackingId,
                                  CancellationToken:  CancellationToken
                              );

                    var httpSources = Request.X_Forwarded_For;
                    if (httpSources.Any())
                    {
                        Request.HTTPSource = new HTTPSource(
                            Request.RemoteSocket,
                            httpSources.Skip(1)
                        );
                    }

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request: " + e.Message);
                }

            }

            Request = null;
            return false;

        }

        #endregion

        #region (static) TryParse(Text,  Body, out Request, Timestamp = null, HTTPSource = null, LocalSocket = null, HTTPServer = null, ...)

        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP request.</param>
        /// <param name="Body">The body of the HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCP socket of the request.</param>
        /// <param name="RemoteSocket">The optional remote TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        public static Boolean TryParse(String                                Text,
                                       Byte[]                                Body,
                                       [NotNullWhen(true)] out HTTPRequest?  Request,

                                       DateTimeOffset?                       Timestamp           = null,
                                       HTTPSource?                           HTTPSource          = null,
                                       IPSocket?                             LocalSocket         = null,
                                       IPSocket?                             RemoteSocket        = null,
                                       HTTPServer?                           HTTPServer          = null,

                                       EventTracking_Id?                     EventTrackingId     = null,
                                       CancellationToken                     CancellationToken   = default)
        {

            if (Text.IsNeitherNullNorEmpty())
            {

                try
                {

                    var EndOfHeader = Text.IndexOf("\r\n\r\n");

                    Request = new HTTPRequest(
                                  Timestamp    ?? Illias.Timestamp.Now,
                                  HTTPSource   ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                  LocalSocket  ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                  RemoteSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),

                                  EndOfHeader == -1 ? Text : Text.Substring(0, EndOfHeader + 2),
                                  Body,

                                  HTTPServer:         HTTPServer,
                                  EventTrackingId:    EventTrackingId,
                                  CancellationToken:  CancellationToken
                              );

                    var httpSources = Request.X_Forwarded_For;
                    if (httpSources.Any())
                    {
                        Request.HTTPSource = new HTTPSource(
                            Request.RemoteSocket,
                            httpSources.Skip(1)
                        );
                    }

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request: " + e.Message);
                }

            }

            Request = null;
            return false;

        }


        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP request.</param>
        /// <param name="Body">The body of the HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        public static Boolean TryParse(String               Text,
                                       Stream               Body,
                                       out HTTPRequest?     Request,

                                       DateTime?            Timestamp           = null,
                                       HTTPSource?          HTTPSource          = null,
                                       IPSocket?            LocalSocket         = null,
                                       IPSocket?            RemoteSocket        = null,
                                       HTTPServer?          HTTPServer          = null,

                                       EventTracking_Id?    EventTrackingId     = null,
                                       CancellationToken    CancellationToken   = default)
        {

            if (Text.IsNeitherNullNorEmpty())
            {

                try
                {

                    var EndOfHeader = Text.IndexOf("\r\n\r\n");

                    Request = new HTTPRequest(
                                  Timestamp    ?? Illias.Timestamp.Now,
                                  HTTPSource   ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                  LocalSocket  ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                  RemoteSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),

                                  EndOfHeader == -1 ? Text : Text.Substring(0, EndOfHeader + 2),
                                  null,
                                  Body,

                                  HTTPServer:         HTTPServer,
                                  EventTrackingId:    EventTrackingId,
                                  CancellationToken:  CancellationToken
                              );

                    var httpSources = Request.X_Forwarded_For;
                    if (httpSources.Any())
                    {
                        Request.HTTPSource = new HTTPSource(
                            Request.RemoteSocket,
                            httpSources.Skip(1)
                        );
                    }

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request: " + e.Message);
                }

            }

            Request = null;
            return false;

        }

        #endregion

        #region (static) TryParse(Lines,       out Request, Timestamp = null, HTTPSource = null, LocalSocket = null, HTTPServer = null, ...)

        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Lines">The lines of the text representation of a HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCp socket of the request.</param>
        /// <param name="RemoteSocket">The optional remote TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        public static Boolean TryParse(IEnumerable<String>                   Lines,
                                       [NotNullWhen(true)] out HTTPRequest?  Request,

                                       DateTimeOffset?                       Timestamp           = null,
                                       HTTPSource?                           HTTPSource          = null,
                                       IPSocket?                             LocalSocket         = null,
                                       IPSocket?                             RemoteSocket        = null,
                                       HTTPServer?                           HTTPServer          = null,

                                       EventTracking_Id?                     EventTrackingId     = null,
                                       CancellationToken                     CancellationToken   = default)
        {

            if (Lines.Any())
            {
                try
                {

                    Request = new HTTPRequest(
                                  Timestamp    ?? Illias.Timestamp.Now,
                                  HTTPSource   ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                  LocalSocket  ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                  RemoteSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),

                                  Lines.TakeWhile(line => line != "").        AggregateWith("\r\n"),
                                  Lines.SkipWhile(line => line != "").Skip(1).AggregateWith("\r\n").ToUTF8Bytes(),

                                  HTTPServer:         HTTPServer,
                                  EventTrackingId:    EventTrackingId,
                                  CancellationToken:  CancellationToken
                              );

                    var httpSources = Request.X_Forwarded_For;
                    if (httpSources.Any())
                    {
                        Request.HTTPSource = new HTTPSource(
                            Request.RemoteSocket,
                            httpSources.Skip(1)
                        );
                    }

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request lines: " + e.Message);
                }
            }

            Request = null;
            return false;

        }

        #endregion

        #region (static) TryParse(Lines, Body, out Request, Timestamp = null, HTTPSource = null, LocalSocket = null, HTTPServer = null, ...)

        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Lines">The lines of the text representation of a HTTP request.</param>
        /// <param name="Body">The body of the HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCp socket of the request.</param>
        /// <param name="RemoteSocket">The optional remote TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        public static Boolean TryParse(IEnumerable<String>                   Lines,
                                       Byte[]                                Body,
                                       [NotNullWhen(true)] out HTTPRequest?  Request,

                                       DateTimeOffset?                       Timestamp           = null,
                                       HTTPSource?                           HTTPSource          = null,
                                       IPSocket?                             LocalSocket         = null,
                                       IPSocket?                             RemoteSocket        = null,
                                       HTTPServer?                           HTTPServer          = null,

                                       EventTracking_Id?                     EventTrackingId     = null,
                                       CancellationToken                     CancellationToken   = default)

        {

            if (Lines.SafeAny())
            {
                try
                {

                    Request = new HTTPRequest(
                                  Timestamp    ?? Illias.Timestamp.Now,
                                  HTTPSource   ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                  LocalSocket  ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                  RemoteSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                  Lines.TakeWhile(line => line != "").AggregateWith("\r\n"),
                                  Body,

                                  HTTPServer:         HTTPServer,
                                  EventTrackingId:    EventTrackingId,
                                  CancellationToken:  CancellationToken
                              );

                    var httpSources = Request.X_Forwarded_For;
                    if (httpSources.Any())
                    {
                        Request.HTTPSource = new HTTPSource(
                            Request.RemoteSocket,
                            httpSources.Skip(1)
                        );
                    }

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request lines: " + e.Message);
                }
            }

            Request = null;
            return false;

        }


        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Lines">The lines of the text representation of a HTTP request.</param>
        /// <param name="Body">The body of the HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCp socket of the request.</param>
        /// <param name="RemoteSocket">The optional remote TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        public static Boolean TryParse(IEnumerable<String>                   Lines,
                                       Stream                                Body,
                                       [NotNullWhen(true)] out HTTPRequest?  Request,

                                       DateTime?                             Timestamp           = null,
                                       HTTPSource?                           HTTPSource          = null,
                                       IPSocket?                             LocalSocket         = null,
                                       IPSocket?                             RemoteSocket        = null,
                                       HTTPServer?                           HTTPServer          = null,

                                       EventTracking_Id?                     EventTrackingId     = null,
                                       CancellationToken                     CancellationToken   = default)
        {

            if (Lines.SafeAny())
            {
                try
                {

                    Request = new HTTPRequest(
                                  Timestamp    ?? org.GraphDefined.Vanaheimr.Illias.Timestamp.Now,
                                  HTTPSource   ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                  LocalSocket  ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                  RemoteSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                  Lines.TakeWhile(line => line != "").AggregateWith("\r\n"),
                                  null,
                                  Body,

                                  HTTPServer:         HTTPServer,
                                  EventTrackingId:    EventTrackingId,
                                  CancellationToken:  CancellationToken
                              );

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request lines: " + e.Message);
                }
            }

            Request = null;
            return false;

        }

        #endregion

        #region (static) TryParse(Bytes,       out Request, Timestamp = null, HTTPSource = null, LocalSocket = null, HTTPServer = null, ...)

        /// <summary>
        /// Parse the given text as a HTTP request.
        /// </summary>
        /// <param name="Bytes">The lines of the text representation of a HTTP request.</param>
        /// <param name="Request">The parsed HTTP request.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="HTTPSource">The optional remote TCP socket of the request.</param>
        /// <param name="LocalSocket">The optional local TCp socket of the request.</param>
        /// <param name="RemoteSocket">The optional remote TCP socket of the request.</param>
        /// <param name="HTTPServer">The optional HTTP server who has received this request.</param>
        /// 
        /// <param name="EventTrackingId">The optional event tracking identification of the request.</param>
        /// <param name="CancellationToken">A token to cancel the HTTP request processing.</param>
        public static Boolean TryParse(Byte[]                                Bytes,
                                       [NotNullWhen(true)] out HTTPRequest?  Request,

                                       DateTimeOffset?                       Timestamp           = null,
                                       HTTPSource?                           HTTPSource          = null,
                                       IPSocket?                             LocalSocket         = null,
                                       IPSocket?                             RemoteSocket        = null,
                                       HTTPServer?                           HTTPServer          = null,

                                       EventTracking_Id?                     EventTrackingId     = null,
                                       CancellationToken                     CancellationToken   = default)
        {

            if (Bytes.SafeAny())
            {
                try
                {

                    var header  = Bytes.ToUTF8String().
                                        Split([ "\r\n" ],
                                              StringSplitOptions.None).
                                        Where(line => line?.Trim().IsNotNullOrEmpty() == true).
                                        ToArray();

                    Request     = new HTTPRequest(
                                      Timestamp    ?? Illias.Timestamp.Now,
                                      HTTPSource   ?? new HTTPSource(IPSocket.LocalhostV4(IPPort.HTTPS)),
                                      LocalSocket  ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                      RemoteSocket ?? IPSocket.LocalhostV4(IPPort.HTTPS),
                                      header.        AggregateWith("\r\n"),
                                      Array.Empty<Byte>(),

                                      HTTPServer:         HTTPServer,
                                      EventTrackingId:    EventTrackingId,
                                      CancellationToken:  CancellationToken
                                  );

                    var httpSources = Request.X_Forwarded_For;
                    if (httpSources.Any())
                    {
                        Request.HTTPSource = new HTTPSource(
                            Request.RemoteSocket,
                            httpSources.Skip(1)
                        );
                    }

                    return true;

                }
                catch (Exception e)
                {
                    DebugX.LogT("Could not parse HTTP request lines: " + e.Message);
                }
            }

            Request = null;
            return false;

        }

        #endregion


        public JObject ToJSON()
        {

            var json = JSONObject.Create();

            return json;

        }


        #region ConvertContent<TResult>(ContentConverter, OnException = null)

        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPRequest<TResult> ConvertContent<TResult>(Func<String, OnExceptionDelegate, TResult>  ContentConverter,
                                                            OnExceptionDelegate?                        OnException   = null)
        {

            if (ContentConverter is null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPRequest<TResult>(
                       this,
                       ContentConverter(
                           HTTPBodyAsUTF8String,
                           OnException
                       )
                   );

        }


        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPRequest<TResult> ConvertContent<TResult>(Func<Byte[], OnExceptionDelegate, TResult>  ContentConverter,
                                                            OnExceptionDelegate?                        OnException   = null)
        {

            if (ContentConverter is null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPRequest<TResult>(
                       this,
                       ContentConverter(
                           HTTPBody,
                           OnException
                       )
                   );

        }


        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPRequest<TResult> ConvertContent<TResult>(Func<Stream, OnExceptionDelegate, TResult>  ContentConverter,
                                                            OnExceptionDelegate?                        OnException   = null)
        {

            if (ContentConverter is null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPRequest<TResult>(
                       this,
                       ContentConverter(
                           HTTPBodyStream,
                           OnException
                       )
                   );

        }

        #endregion


        #region (static) LoadHTTPRequestLogfiles_old(FilePath, FilePattern, FromTimestamp = null, ToTimestamp = null)

        public static IEnumerable<HTTPRequest> LoadHTTPRequestLogfiles_old(String     FilePath,
                                                                           String     FilePattern,
                                                                           DateTime?  FromTimestamp  = null,
                                                                           DateTime?  ToTimestamp    = null)
        {

            var requests = new ConcurrentBag<HTTPRequest>();

            Parallel.ForEach(Directory.EnumerateFiles(Directory.GetCurrentDirectory() + System.IO.Path.DirectorySeparatorChar + FilePath,
                                                      FilePattern,
                                                      SearchOption.TopDirectoryOnly).
                                       OrderByDescending(file => file),
                             new ParallelOptions() { MaxDegreeOfParallelism = 1 },
                             file => {

                var requestLines        = new List<String>();
                var copy                = "none";
                var relativelinenumber  = 0;
                var RequestTimestamp    = Illias.Timestamp.Now;

                foreach (var line in File.ReadLines(file))
                {

                    try
                    {

                        if      (relativelinenumber == 1 && copy == "request")
                            RequestTimestamp  = DateTime.SpecifyKind(DateTime.Parse(line), DateTimeKind.Utc);

                        else if (line == ">>>>>>--Request----->>>>>>------>>>>>>------>>>>>>------>>>>>>------>>>>>>------")
                        {
                            copy               = "request";
                            relativelinenumber = 0;
                        }

                        else if (line == "--------------------------------------------------------------------------------")
                        {

                            if ((FromTimestamp is null || RequestTimestamp >= FromTimestamp.Value) &&
                                (  ToTimestamp is null || RequestTimestamp <    ToTimestamp.Value))
                            {

                                if (TryParse(requestLines,
                                             out var parsedHTTPRequest,
                                             Timestamp:  RequestTimestamp))
                                {
                                    requests.Add(parsedHTTPRequest);
                                }

                                else
                                    DebugX.LogT("Could not parse reloaded HTTP request!");

                            }

                            copy          = "none";
                            requestLines  = [];

                        }

                        else if (copy == "request")
                            requestLines.Add(line);

                        relativelinenumber++;

                    }
                    catch (Exception e)
                    {
                        DebugX.LogT("Could not parse reloaded HTTP request: " + e.Message);
                    }

                }

            });

            return requests.OrderBy(request => request.Timestamp);

        }

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


    #region HTTPRequest<TContent>

    /// <summary>
    /// A generic HTTP request.
    /// </summary>
    /// <typeparam name="TContent">The type of the HTTP body data.</typeparam>
    /// <param name="Request">The non-generic HTTP request.</param>
    /// <param name="Content">The generic HTTP body data.</param>
    /// <param name="IsFault">Whether there is an error.</param>
    /// <param name="Exception">An optional exception.</param>
    public class HTTPRequest<TContent>(HTTPRequest  Request,
                                       TContent     Content,
                                       Boolean      IsFault     = false,
                                       Exception?   Exception   = null) : HTTPRequest(Request)
    {

        #region Data

        private readonly Boolean isFault = IsFault;

        #endregion

        #region Properties

        /// <summary>
        /// The parsed content.
        /// </summary>
        public TContent    Content      { get; } = Content;

        /// <summary>
        /// An exception during parsing.
        /// </summary>
        public Exception?  Exception    { get; } = Exception;

        /// <summary>
        /// An error during parsing.
        /// </summary>
        public Boolean     HasErrors
            => Exception is not null && !isFault;

        #endregion


        #region ConvertContent<TResult>(ContentConverter, OnException = null)

        /// <summary>
        /// Convert the content of the HTTP response body via the given
        /// content converter delegate.
        /// </summary>
        /// <typeparam name="TResult">The type of the converted HTTP response body content.</typeparam>
        /// <param name="ContentConverter">A delegate to convert the given HTTP response content.</param>
        /// <param name="OnException">A delegate to call whenever an exception during the conversion occures.</param>
        public HTTPRequest<TResult> ConvertContent<TResult>(Func<TContent, OnExceptionDelegate?, TResult>  ContentConverter,
                                                            OnExceptionDelegate?                           OnException   = null)
        {

            if (ContentConverter is null)
                throw new ArgumentNullException(nameof(ContentConverter), "The given content converter delegate must not be null!");

            return new HTTPRequest<TResult>(
                       this,
                       ContentConverter(
                           Content,
                           OnException
                       )
                   );

        }

        #endregion


    }

    #endregion

}
