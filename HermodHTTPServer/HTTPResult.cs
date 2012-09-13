/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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

using de.ahzf.Illias.Commons;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A structure to hold a result and an error of an operation.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public struct HTTPResult<T>
    {

        #region Data

        /// <summary>
        /// The HTTPResponse when an error occured.
        /// </summary>
        public readonly HTTPResponse Error;

        /// <summary>
        /// The result of an operation.
        /// </summary>
        public readonly T Data;

        #endregion

        #region Properties

        #region HasErrors

        /// <summary>
        /// The HTTP result contains errors.
        /// </summary>
        public Boolean HasErrors
        {
            get
            {
                return (Error != null || Data == null);
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPResult(Result)

        public HTTPResult(T Result)
        {
            this.Error = null;
            this.Data  = Result;
        }

        #endregion

        #region HTTPResult(HTTPResponse)

        /// <summary>
        /// Create a new HTTPResult when an error occurred.
        /// </summary>
        /// <param name="Error">The HTTPResponse for this error.</param>
        public HTTPResult(HTTPResponse HTTPResponse)
        {
            this.Error = HTTPResponse;
            this.Data  = default(T);
        }

        #endregion

        #region HTTPResult(HTTPRequest, HTTPStatusCode, Reasons = null)

        /// <summary>
        /// Create a new HTTPResult when an error occurred.
        /// </summary>
        /// <param name="HTTPRequest"></param>
        /// <param name="HTTPStatusCode"></param>
        /// <param name="Error">The HTTPResponse for this error.</param>
        public HTTPResult(HTTPRequest HTTPRequest, HTTPStatusCode HTTPStatusCode, String Reasons = null)
        {
            Error = null;
            Data  = default(T);
            //this.Error = HTTPErrors.HTTPErrorResponse(HTTPRequest, HTTPStatusCode, Reasons);
            Error = HTTPErrorResponse(HTTPRequest, HTTPStatusCode, Reasons);
            Data  = default(T);
        }

        #endregion

        #region HTTPResult(HTTPResponse, Data)

        public HTTPResult(HTTPResponse HTTPResponse, T Data)
        {
            this.Error = HTTPResponse;
            this.Data  = Data;
        }

        #endregion

        #endregion



        public HTTPResponse HTTPErrorResponse(HTTPRequest HTTPRequest, HTTPStatusCode StatusCode, String Reasons = null)
        {

            #region Initial checks

            if (StatusCode == null)
                return HTTPErrorResponse(HTTPRequest, HTTPStatusCode.InternalServerError, "Calling the HTTPError lead to an error!");

            var Content = String.Empty;
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
                Content = (Reasons == null) ? "{ \"error\": { \"code\" : " + StatusCode.Code + ", \"message\" : \"" + StatusCode.Name + "\" } }" :
                                              "{ \"error\": { \"code\" : " + StatusCode.Code + ", \"message\" : \"" + StatusCode.Name + "\", \"reasons\" : \"" + Reasons + "\" } }";

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
                Content = (Reasons == null) ? "<!doctype html><html><head><meta charset=\"UTF-8\"><title>Error " + StatusCode.Code + " - " + StatusCode.Name + "</title></head><body><h1>Error " + StatusCode.Code + " - " + StatusCode.Name + "</h1></body></html>" :
                                              "<!doctype html><html><head><meta charset=\"UTF-8\"><title>Error " + StatusCode.Code + " - " + StatusCode.Name + "</title></head><body><h1>Error " + StatusCode.Code + " - " + StatusCode.Name + "</h1>" + Reasons + "</body></html>";

            #endregion

            #region TEXT_UTF8

            // Error 400 - Bad Request
            // The first paramter is not a valid number!
            else if (ContentType == HTTPContentType.TEXT_UTF8 || ContentType == HTTPContentType.ALL)
                Content = (Reasons == null) ? "Error " + StatusCode.Code + " - " + StatusCode.Name :
                                              "Error " + StatusCode.Code + " - " + StatusCode.Name + Environment.NewLine + Reasons;

            #endregion

            #region XML_UTF8

            // <?xml version="1.0" encoding="UTF-8"?>
            // <error>
            //     <code>400</code>
            //     <message>Bad Request</message>
            //     <reason>The first paramter is not a valid number!</message>
            // </error>
            else if (ContentType == HTTPContentType.XML_UTF8)
                Content = (Reasons == null) ? "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><code>" + StatusCode.Code + "</code><message>" + StatusCode.Name + "</message></error></xml>" :
                                              "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><code>" + StatusCode.Code + "</code><message>" + StatusCode.Name + "</message><reasons>" + Reasons + "</reasons></error></xml>";

            #endregion

            return new HTTPResponseBuilder()
            {
                HTTPStatusCode = StatusCode,
                CacheControl   = "no-cache",
                Connection     = "close",
                Content        = Content.ToUTF8Bytes()
            };

        }


    }

}
