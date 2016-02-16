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
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate Boolean PFunc<TResult>(String Input, out TResult arg);

    /// <summary>
    /// HTTP API - JSON I/O.
    /// </summary>
    public static class JSON_IO_ExtentionMethods
    {

        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory<T>(this JObject      JSON,
                                                String            PropertyName,
                                                String            PropertyDescription,
                                                String            DefaultServerName,
                                                PFunc<T>          Parser,
                                                out T             Value,
                                                HTTPRequest       HTTPRequest,
                                                out HTTPResponse  HTTPResponse)
        {

            JToken JSONToken = null;

            if (!JSON.TryGetValue(PropertyName, out JSONToken))
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", "Missing JSON property '" + PropertyName + "'!")
                                      ).ToUTF8Bytes()
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
                    Content         = JSONObject.Create(
                                          new JProperty("description", "Unknown " + PropertyDescription + "!")
                                      ).ToUTF8Bytes()
                };

                Value = default(T);

                return false;

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseMandatory<TEnum>(this JSON, PropertyName, PropertyDescription, DefaultServerName,         out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory<TEnum>(this JObject      JSON,
                                                    String            PropertyName,
                                                    String            PropertyDescription,
                                                    String            DefaultServerName,
                                                    out TEnum         Value,
                                                    HTTPRequest       HTTPRequest,
                                                    out HTTPResponse  HTTPResponse)

             where TEnum : struct

        {

            JToken JSONToken = null;

            if (!JSON.TryGetValue(PropertyName, out JSONToken))
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", "Missing JSON property '" + PropertyName + "'!")
                                      ).ToUTF8Bytes()
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
                    Content         = JSONObject.Create(
                                          new JProperty("description",  "Unknown " + PropertyDescription + "!")
                                      ).ToUTF8Bytes()
                };

                Value = default(TEnum);

                return false;

            }

            HTTPResponse  = null;

            return true;

        }

        #endregion

        #region ParseMandatory(this JSON, PropertyName,  PropertyDescription, DefaultServerName, out Text,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out String        Text,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)

        {

            JToken JSONToken = null;

            if (!JSON.TryGetValue(PropertyName, out JSONToken))
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description",  "Missing JSON property '" + PropertyName + "'!")
                                      ).ToUTF8Bytes()
                };

                Text = String.Empty;

                return false;

            }

            Text          = JSONToken.Value<String>();
            HTTPResponse  = null;

            return true;

        }

        #endregion

        #region ParseMandatory(this JSON, PropertyNames, PropertyDescription, DefaultServerName, out Text,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject         JSON,
                                             IEnumerable<String>  PropertyNames,
                                             String               PropertyDescription,
                                             String               DefaultServerName,
                                             out String           Text,
                                             HTTPRequest          HTTPRequest,
                                             out HTTPResponse     HTTPResponse)

        {

            JToken JSONToken = null;

            // Attention: JSONToken is a side-effect!
            var FirstMatchingPropertyName = PropertyNames.
                                                Where(propertyname => JSON.TryGetValue(propertyname, out JSONToken)).
                                                FirstOrDefault();

            if (FirstMatchingPropertyName != null)
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", "Missing at least one of the following properties: " + PropertyNames.AggregateWith(", ") + "!")
                                      ).ToUTF8Bytes()
                };

                Text = String.Empty;

                return false;

            }

            Text          = JSONToken.Value<String>();
            HTTPResponse  = null;

            return true;

        }

        #endregion

        #region ParseMandatory(this JSON, PropertyName,  PropertyDescription, DefaultServerName, out Timestamp, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out DateTime      Timestamp,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)

        {

            JToken   JSONToken = null;
            Timestamp = DateTime.MinValue;

            if (JSON.TryGetValue(PropertyName, out JSONToken))
            {

                try
                {

                    Timestamp = JSONToken.Value<DateTime>();

                }
                catch (Exception)
                {

                    HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                        Server          = DefaultServerName,
                        Date            = DateTime.Now,
                        ContentType     = HTTPContentType.JSON_UTF8,
                        Content         = JSONObject.Create(
                                              new JProperty("description", "Invalid timestamp '" + JSONToken.Value<String>() + "'!")
                                          ).ToUTF8Bytes()
                    };

                    return false;

                }

                HTTPResponse = null;
                return true;

            }

            else
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", "Missing property '" + PropertyName + "'!")
                                      ).ToUTF8Bytes()
                };

                return false;

            }

        }

        #endregion

        #region ParseMandatory(this JSON, PropertyNames, PropertyDescription, DefaultServerName, out Timestamp, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject         JSON,
                                             IEnumerable<String>  PropertyNames,
                                             String               PropertyDescription,
                                             String               DefaultServerName,
                                             out DateTime         Timestamp,
                                             HTTPRequest          HTTPRequest,
                                             out HTTPResponse     HTTPResponse)

        {

            JToken   JSONToken = null;
            Timestamp = DateTime.MinValue;

            // Attention: JSONToken is a side-effect!
            var FirstMatchingPropertyName = PropertyNames.
                                                Where(propertyname => JSON.TryGetValue(propertyname, out JSONToken)).
                                                FirstOrDefault();

            if (FirstMatchingPropertyName != null)
            {

                try
                {

                    Timestamp = JSONToken.Value<DateTime>();

                }
                catch (Exception)
                {

                    HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                        Server          = DefaultServerName,
                        Date            = DateTime.Now,
                        ContentType     = HTTPContentType.JSON_UTF8,
                        Content         = JSONObject.Create(
                                              new JProperty("description", "Invalid timestamp '" + JSONToken.Value<String>() + "'!")
                                          ).ToUTF8Bytes()
                    };

                    return false;

                }

                HTTPResponse = null;
                return true;

            }

            else
            {

                HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.Now,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", "Missing at least one of the following properties: " + PropertyNames.AggregateWith(", ") + "!")
                                      ).ToUTF8Bytes()
                };

                return false;

            }

        }

        #endregion


        #region ParseOptional<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject      JSON,
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

            if (JSON.TryGetValue(PropertyName, out JSONToken))
            {

                var JSONValue = JSONToken.Value<String>();

                if (JSONValue != null && !Parser(JSONValue, out Value))
                {

                    HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                        Server          = DefaultServerName,
                        Date            = DateTime.Now,
                        ContentType     = HTTPContentType.JSON_UTF8,
                        Content         = JSONObject.Create(
                                              new JProperty("description",  "Unknown " + PropertyDescription + "!")
                                          ).ToUTF8Bytes()
                    };

                    Value = default(T);
                    return false;

                }

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional<TEnum>(this JSON, PropertyName, PropertyDescription, DefaultServerName,         out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<TEnum>(this JObject      JSON,
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

            if (JSON.TryGetValue(PropertyName, out JSONToken))
            {

                var JSONValue = JSONToken.Value<String>();

                if (JSONValue != null && !Enum.TryParse<TEnum>(JSONValue, true, out Value))
                {

                    HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                        Server          = DefaultServerName,
                        Date            = DateTime.Now,
                        ContentType     = HTTPContentType.JSON_UTF8,
                        Content         = JSONObject.Create(
                                              new JProperty("description", "Unknown " + PropertyDescription + "!")
                                          ).ToUTF8Bytes()
                    };

                    Value = default(TEnum);
                    return false;

                }

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional(this JSON, PropertyName, PropertyDescription, DefaultServerName, out Text,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject      JSON,
                                            String            PropertyName,
                                            String            PropertyDescription,
                                            String            DefaultServerName,
                                            out String        Text,
                                            HTTPRequest       HTTPRequest,
                                            out HTTPResponse  HTTPResponse)

        {

            JToken JSONToken = null;
            Text = String.Empty;

            if (JSON.TryGetValue(PropertyName, out JSONToken))
            {
                Text = JSONToken.Value<String>();
            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional(this JSON, PropertyName, PropertyDescription, DefaultServerName, out Timestamp, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject      JSON,
                                            String            PropertyName,
                                            String            PropertyDescription,
                                            String            DefaultServerName,
                                            out DateTime?     Timestamp,
                                            HTTPRequest       HTTPRequest,
                                            out HTTPResponse  HTTPResponse)

        {

            JToken   JSONToken = null;
            Timestamp = new DateTime?();

            if (JSON.TryGetValue(PropertyName, out JSONToken))
            {

                try
                {

                    Timestamp = JSONToken.Value<DateTime>();

                }
                catch (Exception)
                {

                    HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                        Server          = DefaultServerName,
                        Date            = DateTime.Now,
                        ContentType     = HTTPContentType.JSON_UTF8,
                        Content         = JSONObject.Create(
                                              new JProperty("description",  "Invalid timestamp '" + JSONToken.Value<String>() + "'!")
                                          ).ToUTF8Bytes()
                    };

                    return false;

                }

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

    }

}
