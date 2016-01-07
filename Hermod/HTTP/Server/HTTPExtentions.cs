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
using System.Linq;
using System.Xml.Linq;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Illias.ConsoleLog;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class HTTPExtentions
    {

        #region CreateJObject(params Properties)

        public static JObject CreateJObject(params JProperty[] Properties)
        {

            var FilteredData = Properties.Where(p => p != null).ToArray();

            return (FilteredData.Length > 0)
                       ? new JObject(FilteredData)
                       : new JObject();

        }

        #endregion

        #region (protected) GetRequestBodyAsUTF8String(this Request, HTTPContentType)

        public static HTTPResult<String> GetRequestBodyAsUTF8String(this HTTPRequest  Request,
                                                                    HTTPContentType   HTTPContentType)
        {

            if (Request.ContentType != HTTPContentType)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            if (Request.ContentLength == 0)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            if (Request.TryReadHTTPBody() == false)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            if (Request.HTTPBody == null || Request.HTTPBody.Length == 0)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            var RequestBodyString = Request.HTTPBody.ToUTF8String().Trim();

            if (RequestBodyString.IsNullOrEmpty())
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            return new HTTPResult<String>(Result: RequestBodyString);

        }

        #endregion


        #region TryParseJObjectRequestBody(this Request, out JSON, out HTTPResponse, AllowEmptyHTTPBody = false, JSONLDContext = null)

        public static Boolean TryParseJObjectRequestBody(this HTTPRequest  HTTPRequest,
                                                         out JObject       JSON,
                                                         out HTTPResponse  HTTPResponse,
                                                         Boolean           AllowEmptyHTTPBody = false,
                                                         String            JSONLDContext      = null)
        {

            #region AllowEmptyHTTPBody

            JSON          = null;
            HTTPResponse  = null;

            if (HTTPRequest.ContentLength == 0 && AllowEmptyHTTPBody)
            {

                HTTPResponse = new HTTPResponseBuilder() {
                    HTTPStatusCode = HTTPStatusCode.OK,
                };

                return false;

            }

            #endregion

            #region Get text body

            var RequestBodyString = HTTPRequest.GetRequestBodyAsUTF8String(HTTPContentType.JSON_UTF8);
            if (RequestBodyString.HasErrors)
            {
                HTTPResponse  = RequestBodyString.Error;
                return false;
            }

            #endregion

            #region Try to parse the JSON

            try
            {
                JSON = JObject.Parse(RequestBodyString.Data);
            }
            catch (Exception e)
            {

                HTTPResponse  = new HTTPResponseBuilder() {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = CreateJObject(JSONLDContext.IsNotNullOrEmpty()
                                                        ? new JProperty("context",  JSONLDContext)
                                                        : null,
                                                    new JProperty("description",  "Invalid JSON request body!"),
                                                    new JProperty("hint",         e.Message)).
                                                    ToUTF8Bytes()
                };

                return false;

            }

            return true;

            #endregion

        }

        #endregion

        #region TryParseJArrayRequestBody(this Request, out JSON, out HTTPResponse, AllowEmptyHTTPBody = false, JSONLDContext = null)

        public static Boolean TryParseJArrayRequestBody(this HTTPRequest  HTTPRequest,
                                                        out JArray        JSON,
                                                        out HTTPResponse  HTTPResponse,
                                                        Boolean           AllowEmptyHTTPBody = false,
                                                        String            JSONLDContext      = null)
        {

            #region AllowEmptyHTTPBody

            JSON          = null;
            HTTPResponse  = null;

            if (HTTPRequest.ContentLength == 0 && AllowEmptyHTTPBody)
            {

                HTTPResponse = new HTTPResponseBuilder() {
                    HTTPStatusCode = HTTPStatusCode.OK,
                };

                return false;

            }

            #endregion

            #region Get text body

            var RequestBodyString = HTTPRequest.GetRequestBodyAsUTF8String(HTTPContentType.JSON_UTF8);
            if (RequestBodyString.HasErrors)
            {
                HTTPResponse  = RequestBodyString.Error;
                return false;
            }

            #endregion

            #region Try to parse the JSON

            try
            {
                JSON = JArray.Parse(RequestBodyString.Data);
            }
            catch (Exception e)
            {

                HTTPResponse  = new HTTPResponseBuilder() {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = CreateJObject(JSONLDContext.IsNotNullOrEmpty()
                                                        ? new JProperty("context",  JSONLDContext)
                                                        : null,
                                                    new JProperty("description",  "Invalid JSON request body!"),
                                                    new JProperty("hint",         e.Message)).
                                                    ToUTF8Bytes()
                };

                return false;

            }

            return true;

            #endregion

        }

        #endregion


        #region ParseXMLRequestBody()

        public static HTTPResult<XDocument> ParseXMLRequestBody(this HTTPRequest Request)
        {

            var RequestBodyString = Request.GetRequestBodyAsUTF8String(HTTPContentType.XMLTEXT_UTF8);
            if (RequestBodyString.HasErrors)
                return new HTTPResult<XDocument>(RequestBodyString.Error);

            XDocument RequestBodyXML;

            try
            {
                RequestBodyXML = XDocument.Parse(RequestBodyString.Data);
            }
            catch (Exception e)
            {
                Log.WriteLine(e.Message);
                return new HTTPResult<XDocument>(Request, HTTPStatusCode.BadRequest);
            }

            return new HTTPResult<XDocument>(RequestBodyXML);

        }

        #endregion

    }

}
