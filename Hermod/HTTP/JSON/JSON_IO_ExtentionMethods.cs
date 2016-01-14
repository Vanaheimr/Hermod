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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate Boolean PFunc<TResult>(String Input, out TResult arg);

    /// <summary>
    /// WWCP HTTP API - JSON I/O.
    /// </summary>
    public static class JSON_IO_ExtentionMethods
    {

        #region ParseMandatory<T>(this JSONObject, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory<T>(this JObject      JSONObject,
                                                String            PropertyName,
                                                String            PropertyDescription,
                                                String            DefaultServerName,
                                                PFunc<T>          Parser,
                                                out T             Value,
                                                HTTPRequest       HTTPRequest,
                                                out HTTPResponse  HTTPResponse)
        {

            JToken JSONToken = null;

            if (!JSONObject.TryGetValue(PropertyName, out JSONToken))
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = new JObject(new JProperty("description", "Missing JSON property '" + PropertyName + "'!")).ToString().ToUTF8Bytes()
                };

                Value = default(T);

                return false;

            }

            if (!Parser(JSONToken.Value<String>(), out Value))
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = new JObject(new JProperty("description", "Unknown " + PropertyDescription + "!")).ToString().ToUTF8Bytes()
                };

                Value = default(T);

                return false;

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseMandatory<TEnum>(this JSONObject, PropertyName, PropertyDescription, DefaultServerName, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory<TEnum>(this JObject      JSONObject,
                                                    String            PropertyName,
                                                    String            PropertyDescription,
                                                    String            DefaultServerName,
                                                    out TEnum         Value,
                                                    HTTPRequest       HTTPRequest,
                                                    out HTTPResponse  HTTPResponse)

             where TEnum : struct

        {

            JToken JSONToken = null;

            if (!JSONObject.TryGetValue(PropertyName, out JSONToken))
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = new JObject(new JProperty("description", "Missing JSON property '" + PropertyName + "'!")).ToString().ToUTF8Bytes()
                };

                Value = default(TEnum);

                return false;

            }

            if (!Enum.TryParse<TEnum>(JSONToken.Value<String>(), true, out Value))
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = new JObject(new JProperty("description", "Unknown " + PropertyDescription + "!")).ToString().ToUTF8Bytes()
                };

                Value = default(TEnum);

                return false;

            }

            HTTPResponse  = null;

            return true;

        }

        #endregion

        #region ParseOptional<T>(this JSONObject, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject      JSONObject,
                                               String            PropertyName,
                                               String            PropertyDescription,
                                               String            DefaultServerName,
                                               PFunc<T>          Parser,
                                               out T             Value,
                                               HTTPRequest       HTTPRequest,
                                               out HTTPResponse  HTTPResponse)
        {

            JToken JSONToken = null;
            Value = default(T);

            if (JSONObject.TryGetValue(PropertyName, out JSONToken))
            {

                var JSONValue = JSONToken.Value<String>();

                if (JSONValue != null && !Parser(JSONValue, out Value))
                {

                    HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                        Server          = DefaultServerName,
                        Date            = DateTime.Now,
                        ContentType     = HTTPContentType.JSON_UTF8,
                        Content         = new JObject(new JProperty("description", "Unknown " + PropertyDescription + "!")).ToString().ToUTF8Bytes()
                    };

                    Value = default(T);
                    return false;

                }

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional<TEnum>(this JSONObject, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<TEnum>(this JObject      JSONObject,
                                                   String            PropertyName,
                                                   String            PropertyDescription,
                                                   String            DefaultServerName,
                                                   out TEnum         Value,
                                                   HTTPRequest       HTTPRequest,
                                                   out HTTPResponse  HTTPResponse)

            where TEnum : struct

        {

            JToken JSONToken = null;
            Value = default(TEnum);

            if (JSONObject.TryGetValue(PropertyName, out JSONToken))
            {

                var JSONValue = JSONToken.Value<String>();

                if (JSONValue != null && !Enum.TryParse<TEnum>(JSONValue, true, out Value))
                {

                    HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                        Server          = DefaultServerName,
                        Date            = DateTime.Now,
                        ContentType     = HTTPContentType.JSON_UTF8,
                        Content         = new JObject(new JProperty("description", "Unknown " + PropertyDescription + "!")).ToString().ToUTF8Bytes()
                    };

                    Value = default(TEnum);
                    return false;

                }

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

    }

}
