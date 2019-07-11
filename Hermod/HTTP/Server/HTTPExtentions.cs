/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Xml.Linq;

using Newtonsoft.Json.Linq;
using org.GraphDefined.Vanaheimr.Hermod.MIME;
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Illias.ConsoleLog;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class HTTPExtentions
    {

        #region GetRequestBodyAsUTF8String   (this Request, HTTPContentType)

        public static HTTPResult<String> GetRequestBodyAsUTF8String(this HTTPRequest  Request,
                                                                    HTTPContentType   HTTPContentType,
                                                                    Boolean           AllowEmptyHTTPBody = false)
        {

            if (Request.ContentType != HTTPContentType)
                return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            if (!AllowEmptyHTTPBody)
            {

                if (Request.ContentLength == 0)
                    return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

                if (!Request.TryReadHTTPBodyStream())
                    return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

                if (Request.HTTPBody == null || Request.HTTPBody.Length == 0)
                    return new HTTPResult<String>(Request, HTTPStatusCode.BadRequest);

            }

            var RequestBodyString = Request.HTTPBody.ToUTF8String().Trim();

            return RequestBodyString.IsNullOrEmpty()
                ? AllowEmptyHTTPBody
                      ? new HTTPResult<String>(Result: "")
                      : new HTTPResult<String>(Request, HTTPStatusCode.BadRequest)
                : new HTTPResult<String>(Result: RequestBodyString);

        }

        #endregion

        #region TryParseUTF8StringRequestBody(this Request, ContentType, out Text, out HTTPResponse, AllowEmptyHTTPBody = false)

        public static Boolean TryParseUTF8StringRequestBody(this HTTPRequest  Request,
                                                            HTTPContentType   ContentType,
                                                            out String        Text,
                                                            out HTTPResponse  HTTPResponse,
                                                            Boolean           AllowEmptyHTTPBody = false)
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

            var RequestBodyString = Request.GetRequestBodyAsUTF8String(ContentType, AllowEmptyHTTPBody);
            if (RequestBodyString.HasErrors)
            {
                HTTPResponse  = RequestBodyString.Error;
                return false;
            }

            #endregion

            Text = RequestBodyString.Data;

            return true;

        }

        #endregion

        #region TryParseJObjectRequestBody   (this Request, out JSON, out HTTPResponse, AllowEmptyHTTPBody = false, JSONLDContext = null)

        public static Boolean TryParseJObjectRequestBody(this HTTPRequest  Request,
                                                         out JObject       JSON,
                                                         out HTTPResponse  HTTPResponse,
                                                         Boolean           AllowEmptyHTTPBody = false,
                                                         String            JSONLDContext      = null)
        {

            #region AllowEmptyHTTPBody

            JSON          = null;
            HTTPResponse  = null;

            if (Request.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                HTTPResponse = HTTPResponse.OK(Request);
                return false;
            }

            #endregion

            #region Get text body

            var RequestBodyString = Request.GetRequestBodyAsUTF8String(HTTPContentType.JSON_UTF8, AllowEmptyHTTPBody);
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

                HTTPResponse  = new HTTPResponse.Builder(Request) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          JSONLDContext.IsNotNullOrEmpty()
                                              ? new JProperty("context",  JSONLDContext)
                                              : null,
                                          new JProperty("description",  "Invalid JSON Object request body!"),
                                          new JProperty("hint",         e.Message)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            return true;

            #endregion

        }

        #endregion

        #region TryParseJArrayRequestBody    (this Request, out JSON, out HTTPResponse, AllowEmptyHTTPBody = false, JSONLDContext = null)

        public static Boolean TryParseJArrayRequestBody(this HTTPRequest  Request,
                                                        out JArray        JSON,
                                                        out HTTPResponse  HTTPResponse,
                                                        Boolean           AllowEmptyHTTPBody = false,
                                                        String            JSONLDContext      = null)
        {

            #region AllowEmptyHTTPBody

            JSON          = null;
            HTTPResponse  = null;

            if (Request.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                HTTPResponse = HTTPResponse.OK(Request);
                return false;
            }

            #endregion

            #region Get text body

            var RequestBodyString = Request.GetRequestBodyAsUTF8String(HTTPContentType.JSON_UTF8, AllowEmptyHTTPBody);
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

                HTTPResponse  = new HTTPResponse.Builder(Request) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          JSONLDContext.IsNotNullOrEmpty()
                                              ? new JProperty("context",  JSONLDContext)
                                              : null,
                                          new JProperty("description",  "Invalid JSON request body!"),
                                          new JProperty("hint",         e.Message)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            return true;

            #endregion

        }

        #endregion


        #region GetResponseBodyAsUTF8String   (this Request, HTTPContentType)

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

        #region TryParseJObjectRequestBody   (this Request, out JSON, out HTTPResponse, AllowEmptyHTTPBody = false, JSONLDContext = null)

        public static Boolean TryParseJObjectResponseBody(this HTTPResponse  Response,
                                                          out JObject        JSON,
                                                          Boolean            AllowEmptyHTTPBody = false,
                                                          String             JSONLDContext      = null)
        {

            #region AllowEmptyHTTPBody

            if (Response.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                JSON = new JObject();
                return false;
            }

            #endregion

            #region Get text body

            var ResponseBodyString = Response.GetResponseBodyAsUTF8String(HTTPContentType.JSON_UTF8, AllowEmptyHTTPBody);

            #endregion

            #region Try to parse the JSON

            try
            {

                JSON = JObject.Parse(ResponseBodyString);

            }
            catch (Exception e)
            {
                JSON = new JObject();
                return false;
            }

            return true;

            #endregion

        }

        #endregion

        #region TryParseJArrayRequestBody    (this Request, out JSON, out HTTPResponse, AllowEmptyHTTPBody = false, JSONLDContext = null)

        public static Boolean TryParseJObjectResponseBody(this HTTPResponse  Response,
                                                          out JArray         JSON,
                                                          Boolean            AllowEmptyHTTPBody = false,
                                                          String             JSONLDContext      = null)
        {

            #region AllowEmptyHTTPBody

            if (Response.ContentLength == 0 && AllowEmptyHTTPBody)
            {
                JSON = new JArray();
                return false;
            }

            #endregion

            #region Get text body

            var ResponseBodyString = Response.GetResponseBodyAsUTF8String(HTTPContentType.JSON_UTF8, AllowEmptyHTTPBody);

            #endregion

            #region Try to parse the JSON

            try
            {

                JSON = JArray.Parse(ResponseBodyString);

            }
            catch (Exception e)
            {
                JSON = new JArray();
                return false;
            }

            return true;

            #endregion

        }

        #endregion




        #region ParseXMLRequestBody(this Request, ContentType = null)

        public static HTTPResult<XDocument> ParseXMLRequestBody(this HTTPRequest  Request,
                                                                HTTPContentType   ContentType = null)
        {

            var RequestBodyString = Request.GetRequestBodyAsUTF8String(ContentType != null ? ContentType : HTTPContentType.XMLTEXT_UTF8);
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



        #region TryParseMultipartFormDataRequestBody(this Request, MimeMultipart, Response)

        public static Boolean TryParseMultipartFormDataRequestBody(this HTTPRequest  Request,
                                                                   out Multipart     MimeMultipart,
                                                                   out HTTPResponse  Response)
        {

            #region Initial checks

            if (Request.ContentType     != HTTPContentType.MULTIPART_FORMDATA ||
                Request.ContentLength   == 0                                  ||
                !Request.TryReadHTTPBodyStream()                              ||
                Request.HTTPBody        == null                               ||
                Request.HTTPBody.Length == 0)
            {

                MimeMultipart = null;
                Response      = HTTPResponse.BadRequest(Request);

                return false;

            }

            #endregion


            var RequestBodyString = Request.HTTPBody.ToUTF8String().Trim();

            MimeMultipart = Multipart.Parse(Request.HTTPBody,
                                            Request.ContentType.MIMEBoundary);
            Response  = null;
            return true;

        }

        #endregion










        public static HTTPResponse CreateBadRequest(HTTPRequest HTTPRequest, String Context, String ParameterName)
        {

            return new HTTPResponse.Builder(HTTPRequest) {
                HTTPStatusCode  = HTTPStatusCode.BadRequest,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(new JProperty("@context",    Context),
                                              new JProperty("description", "Missing \"" + ParameterName + "\" JSON property!")).ToString().ToUTF8Bytes()
            };

        }

        public static HTTPResponse CreateBadRequest(HTTPRequest HTTPRequest, String Context, String ParameterName, String Value)
        {

            return new HTTPResponse.Builder(HTTPRequest) {
                HTTPStatusCode  = HTTPStatusCode.BadRequest,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(new JProperty("@context",    Context),
                                              new JProperty("value",       Value),
                                              new JProperty("description", "Invalid \"" + ParameterName + "\" property value!")).ToString().ToUTF8Bytes()
            };

        }

        public static HTTPResponse CreateNotFound(HTTPRequest HTTPRequest, String Context, String ParameterName, String Value)
        {

            return new HTTPResponse.Builder(HTTPRequest) {
                HTTPStatusCode  = HTTPStatusCode.NotFound,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(new JProperty("@context",    Context),
                                              new JProperty("value",       Value),
                                              new JProperty("description", "Unknown \"" + ParameterName + "\" property value!")).ToString().ToUTF8Bytes()
            };

        }





        public static Boolean TryParseI18NString(HTTPRequest HTTPRequest, JObject DescriptionJSON, out I18NString I18N, out HTTPResponse Response)
        {

            if (DescriptionJSON == null)
            {

                I18N     = null;

                Response = new HTTPResponse.Builder(HTTPRequest) {
                               HTTPStatusCode  = HTTPStatusCode.BadRequest,
                               ContentType     = HTTPContentType.JSON_UTF8,
                               Content         = new JObject(new JProperty("description", "Invalid roaming network description!")).ToUTF8Bytes()
                           }.AsImmutable;

                return false;

            }

            Languages  Language;
            JValue     Text;
            I18N      = I18NString.Empty;

            foreach (var Description in DescriptionJSON)
            {

                if (!Enum.TryParse(Description.Key, out Language))
                {

                    I18N = null;

                    Response = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = new JObject(new JProperty("description", "Unknown or invalid language definition '" + Description.Key + "'!")).ToUTF8Bytes()
                               }.AsImmutable;

                    return false;

                }

                Text = Description.Value as JValue;

                if (Text == null)
                {

                    I18N = null;

                    Response = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = new JObject(new JProperty("description", "Invalid description text!")).ToUTF8Bytes()
                               }.AsImmutable;

                    return false;

                }

                I18N.Add(Language, Text.Value<String>());

            }

            Response = null;

            return true;

        }



        public static Byte[] CreateError(String Text)
            => (@"{ ""description"": """ + Text + @""" }").ToUTF8Bytes();


    }

}
