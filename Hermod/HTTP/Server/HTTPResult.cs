/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A structure to hold a result and an error of an operation.
    /// </summary>
    /// <typeparam name="TData">The type of the result.</typeparam>
    public struct HTTPResult<TData>
    {

        #region Properties

        /// <summary>
        /// The HTTPResponse when an error occured.
        /// </summary>
        public HTTPResponse.Builder  Error          { get; }

        /// <summary>
        /// The result of an operation.
        /// </summary>
        public TData                 Data           { get; }

        /// <summary>
        /// The result of an operation.
        /// </summary>
        public Boolean               ValidData      { get; }

        /// <summary>
        /// The current ETag as state or revision of the resource.
        /// </summary>
        public String                ETag           { get; }


        /// <summary>
        /// The HTTP result contains errors.
        /// </summary>
        public Boolean               HasErrors

            => Error != null || Data == null;

        #endregion

        #region Constructor(s)

        #region HTTPResult()

        public HTTPResult(String ETag)
        {
            this.Error      = null;
            this.Data       = default;
            this.ValidData  = false;
            this.ETag       = ETag;
        }

        #endregion

        #region HTTPResult(Result, ETag = null)

        public HTTPResult(TData   Result,
                          String  ETag   = null)
        {

            this.Error      = null;
            this.Data       = Result;
            this.ValidData  = true;
            this.ETag       = ETag;

        }

        #endregion

        #region HTTPResult(HTTPResponse, ETag = null)

        /// <summary>
        /// Create a new HTTPResult when an error occurred.
        /// </summary>
        public HTTPResult(HTTPResponse.Builder  HTTPResponse,
                          String                ETag   = null)
        {

            this.Error      = HTTPResponse;
            this.Data       = default;
            this.ValidData  = false;
            this.ETag       = ETag;

        }

        #endregion

        #region HTTPResult(HTTPRequest, HTTPStatusCode, Reason = null, ETag = null)

        /// <summary>
        /// Create a new HTTPResult when an error occurred.
        /// </summary>
        public HTTPResult(HTTPRequest     HTTPRequest,
                          HTTPStatusCode  HTTPStatusCode,
                          String          Reason   = null,
                          String          ETag     = null)
        {

            this.Error      = null;
            this.Data       = default;
            this.ValidData  = false;
            this.ETag       = ETag;
            this.Error      = HTTPErrorResponse(HTTPRequest, HTTPStatusCode, Reason, ETag);

        }

        #endregion

        #region HTTPResult(HTTPResponse, Data, ETag = null)

        public HTTPResult(HTTPResponse.Builder  HTTPResponse,
                          TData                 Data,
                          String                ETag   = null)
        {

            this.Error      = HTTPResponse;
            this.Data       = Data;
            this.ValidData  = true;
            this.ETag       = ETag;

        }

        #endregion

        #endregion


        public HTTPResponse.Builder HTTPErrorResponse(HTTPRequest     HTTPRequest,
                                                      HTTPStatusCode  StatusCode,
                                                      String          Reason   = null,
                                                      String          ETag     = null)
        {

            #region Initial checks

            if (StatusCode == null)
                return HTTPErrorResponse(HTTPRequest, HTTPStatusCode.InternalServerError, "Calling the HTTPError lead to an error!");

            var Content     = String.Empty;
            var ContentType = HTTPRequest.Accept.BestMatchingContentType(HTTPContentType.JSON_UTF8,
                                                                         HTTPContentType.HTML_UTF8,
                                                                         HTTPContentType.TEXT_UTF8,
                                                                         HTTPContentType.XML_UTF8);

            #endregion

            #region JSON_UTF8

            // {
            //     "error": {
            //         "code"    : 400
            //         "message" : "Bad Request"
            //         "reason"  : "The first paramter is not a valid number!"
            //     }
            // }
            if (ContentType == HTTPContentType.JSON_UTF8)
                Content = (Reason == null) ? "{\r\n  \"error\":\r\n  {\r\n    \"code\": " + StatusCode.Code + ",\r\n    \"message\": \"" + StatusCode.Name + "\"\r\n  }\r\n}" :
                                             "{\r\n  \"error\":\r\n  {\r\n    \"code\": " + StatusCode.Code + ",\r\n    \"message\": \"" + StatusCode.Name + "\",\r\n    \"reason\": \"" + Reason + "\"\r\n  }\r\n}";

            #endregion

            #region HTML_UTF8

            //<!doctype html>
            //<html>
            //  <head>
            //    <meta charset="UTF-8">
            //    <title>Error 400 - Bad Request</title>
            //  </head>
            //  <body>
            //    <h1>Error 400 - Bad Request</h1>
            //    The first paramter is not a valid number!
            //  </body>
            //</html>
            else if (ContentType == HTTPContentType.HTML_UTF8)
                Content = (Reason == null) ? "<!doctype html><html><head><meta charset=\"UTF-8\"><title>Error " + StatusCode.Code + " - " + StatusCode.Name + "</title></head><body><h1>Error " + StatusCode.Code + " - " + StatusCode.Name + "</h1></body></html>" :
                                             "<!doctype html><html><head><meta charset=\"UTF-8\"><title>Error " + StatusCode.Code + " - " + StatusCode.Name + "</title></head><body><h1>Error " + StatusCode.Code + " - " + StatusCode.Name + "</h1>" + Reason + "</body></html>";

            #endregion

            #region TEXT_UTF8

            // Error 400 - Bad Request
            // The first paramter is not a valid number!
            else if (ContentType == HTTPContentType.TEXT_UTF8 || ContentType == HTTPContentType.ALL)
                Content = (Reason == null) ? "Error " + StatusCode.Code + " - " + StatusCode.Name :
                                             "Error " + StatusCode.Code + " - " + StatusCode.Name + Environment.NewLine + Reason;

            #endregion

            #region XML_UTF8

            // <?xml version="1.0" encoding="UTF-8"?>
            // <error>
            //     <code>400</code>
            //     <message>Bad Request</message>
            //     <reason>The first paramter is not a valid number!</message>
            // </error>
            else if (ContentType == HTTPContentType.XML_UTF8)
                Content = (Reason == null) ? "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><code>" + StatusCode.Code + "</code><message>" + StatusCode.Name + "</message></error></xml>" :
                                             "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><code>" + StatusCode.Code + "</code><message>" + StatusCode.Name + "</message><reasons>" + Reason + "</reasons></error></xml>";

            #endregion

            var response = new HTTPResponse.Builder(HTTPRequest) {
                Date            = DateTime.UtcNow,
                HTTPStatusCode  = StatusCode,
                CacheControl    = "no-cache",
                Connection      = "close",
                ContentType     = ContentType,
                Content         = Content.ToUTF8Bytes()
            };

            if (ETag != null)
                response.ETag = ETag;

            return response;

        }


    }

}
