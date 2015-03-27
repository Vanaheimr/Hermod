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
using System.Linq;
using System.Xml.Linq;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Illias.ConsoleLog;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Services.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using System.Diagnostics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class HTTPExtentions
    {

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

            if (Request.Content == null || Request.Content.Length == 0)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            var RequestBodyString = Request.Content.ToUTF8String();

            if (RequestBodyString.IsNullOrEmpty())
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            return new HTTPResult<String>(Result: RequestBodyString);

        }

        #endregion

        #region ParseJSONRequestBody()

        public static HTTPResult<JObject> ParseJSONRequestBody(this HTTPRequest Request)
        {

            var RequestBodyString = Request.GetRequestBodyAsUTF8String(HTTPContentType.JSON_UTF8);
            if (RequestBodyString.HasErrors)
                return new HTTPResult<JObject>(RequestBodyString.Error);

            JObject RequestBodyJSON;

            try
            {
                RequestBodyJSON = JObject.Parse(RequestBodyString.Data);
            }
            catch (Exception)
            {
                return new HTTPResult<JObject>(Request, HTTPStatusCode.BadRequest);
            }

            return new HTTPResult<JObject>(RequestBodyJSON);

        }

        #endregion

        #region TryParseJSONRequestBody()

        public static Boolean TryParseJSONRequestBody(this HTTPRequest Request, out JObject JSON, out HTTPResponse HTTPResp, String Context = null)
        {

            var RequestBodyString = Request.GetRequestBodyAsUTF8String(HTTPContentType.JSON_UTF8);
            if (RequestBodyString.HasErrors)
            {
                JSON      = null;
                HTTPResp  = RequestBodyString.Error;
                return false;
            }

            try
            {
                JSON = JObject.Parse(RequestBodyString.Data);
            }
            catch (Exception e)
            {

                JSON      = null;

                HTTPResp  = new HTTPResponseBuilder() {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = new JObject(new JProperty("@context",    Context),
                                                  new JProperty("description", "Invalid JSON request body!"),
                                                  new JProperty("exception",   e.Message)).ToString().ToUTF8Bytes()
                };

                return false;

            }

            HTTPResp = null;

            return true;

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
