/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods to parse JSON.
    /// </summary>
    public static class JSONExtensions
    {

        #region ParseMandatoryText   (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Text,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatoryText(this JObject              JSON,
                                                 String                    PropertyName,
                                                 String                    PropertyDescription,
                                                 String                    DefaultServerName,
                                                 out String                Text,
                                                 HTTPRequest               HTTPRequest,
                                                 out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatoryText(PropertyName,
                                                  PropertyDescription,
                                                  out Text,
                                                  out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, Mapper,    out Value,     HTTPRequest, out HTTPResponse)

        //public static Boolean ParseMandatory<T>(this JObject      JSON,
        //                                        String            PropertyName,
        //                                        String            PropertyDescription,
        //                                        String            DefaultServerName,
        //                                        Func<String, T>   Mapper,
        //                                        out T             Value,
        //                                        HTTPRequest       HTTPRequest,
        //                                        out HTTPResponse  HTTPResponse)
        //{

        //    var success = JSON.ParseMandatory(PropertyName,
        //                                      PropertyDescription,
        //                                      Mapper,
        //                                      out Value,
        //                                      out String ErrorResponse);

        //    if (ErrorResponse is not null)
        //    {

        //        HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
        //            HTTPStatusCode  = HTTPStatusCode.BadRequest,
        //            Server          = DefaultServerName,
        //            Date            = Timestamp.Now,
        //            ContentType     = HTTPContentType.Application.JSON_UTF8,
        //            Content         = JSONObject.Create(
        //                                  new JProperty("description", ErrorResponse)
        //                              ).ToUTF8Bytes()
        //        };

        //        return false;

        //    }

        //    HTTPResponse = null;
        //    return success;

        //}

        //public static Boolean ParseMandatory<T>(this JObject      JSON,
        //                                        String            PropertyName,
        //                                        String            PropertyDescription,
        //                                        String            DefaultServerName,
        //                                        Func<String, T>   Mapper,
        //                                        out T?            Value,
        //                                        HTTPRequest       HTTPRequest,
        //                                        out HTTPResponse  HTTPResponse)

        //    where T : struct

        //{

        //    var success = JSON.ParseMandatory(PropertyName,
        //                                      PropertyDescription,
        //                                      Mapper,
        //                                      out Value,
        //                                      out String ErrorResponse);

        //    if (ErrorResponse is not null)
        //    {

        //        HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
        //            HTTPStatusCode  = HTTPStatusCode.BadRequest,
        //            Server          = DefaultServerName,
        //            Date            = Timestamp.Now,
        //            ContentType     = HTTPContentType.Application.JSON_UTF8,
        //            Content         = JSONObject.Create(
        //                                  new JProperty("description", ErrorResponse)
        //                              ).ToUTF8Bytes()
        //        };

        //        return false;

        //    }

        //    HTTPResponse = null;
        //    return success;

        //}

        #endregion

        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, TryParser, out Value,     HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory<T>(this JObject              JSON,
                                                String                    PropertyName,
                                                String                    PropertyDescription,
                                                String                    DefaultServerName,
                                                TryParser<T>              TryParser,
                                                out T                     Value,
                                                HTTPRequest               HTTPRequest,
                                                out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              TryParser,
                                              out Value,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        //public static Boolean ParseMandatory<T>(this JObject      JSON,
        //                                        String            PropertyName,
        //                                        String            PropertyDescription,
        //                                        String            DefaultServerName,
        //                                        TryParser<T>      TryParser,
        //                                        out T?            Value,
        //                                        HTTPRequest       HTTPRequest,
        //                                        out HTTPResponse  HTTPResponse)

        //    where T : struct

        //{

        //    var success = JSON.ParseMandatory(PropertyName,
        //                                      PropertyDescription,
        //                                      TryParser,
        //                                      out Value,
        //                                      out String ErrorResponse);

        //    if (ErrorResponse is not null)
        //    {

        //        HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
        //            HTTPStatusCode  = HTTPStatusCode.BadRequest,
        //            Server          = DefaultServerName,
        //            Date            = Timestamp.Now,
        //            ContentType     = HTTPContentType.Application.JSON_UTF8,
        //            Content         = JSONObject.Create(
        //                                  new JProperty("description", ErrorResponse)
        //                              ).ToUTF8Bytes()
        //        };

        //        return false;

        //    }

        //    HTTPResponse = null;
        //    return success;

        //}

        #endregion

        #region ParseMandatoryJSON<T>(this JSON, PropertyName, PropertyDescription, DefaultServerName, TryJObjectParser, out Value,     HTTPRequest, out HTTPResponse)

        //public static Boolean ParseMandatoryJSON<T>(this JObject              JSON,
        //                                            String                    PropertyName,
        //                                            String                    PropertyDescription,
        //                                            String                    DefaultServerName,
        //                                            TryJObjectParser<T>       TryJObjectParser,
        //                                            out T                     Value,
        //                                            HTTPRequest               HTTPRequest,
        //                                            out HTTPResponse.Builder  HTTPResponse)
        //{

        //    var success = JSON.ParseMandatoryJSON(PropertyName,
        //                                          PropertyDescription,
        //                                          TryJObjectParser,
        //                                          out Value,
        //                                          out String ErrorResponse);

        //    if (ErrorResponse is not null)
        //    {

        //        HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
        //            HTTPStatusCode  = HTTPStatusCode.BadRequest,
        //            Server          = DefaultServerName,
        //            Date            = Timestamp.Now,
        //            ContentType     = HTTPContentType.Application.JSON_UTF8,
        //            Content         = JSONObject.Create(
        //                                  new JProperty("description", ErrorResponse)
        //                              ).ToUTF8Bytes()
        //        };

        //        return false;

        //    }

        //    HTTPResponse = null;
        //    return success;

        //}

        public static Boolean ParseMandatoryJSON<T>(this JObject         JSON,
                                                    String               PropertyName,
                                                    String               PropertyDescription,
                                                    String               DefaultServerName,
                                                    TryJObjectParser2a<T>  TryParser,
                                                    out T?               Value,
                                                    HTTPRequest          HTTPRequest,
                                                    out HTTPResponse     HTTPResponse)

            where T : struct

        {

            var success = JSON.ParseMandatoryJSONS(PropertyName,
                                                  PropertyDescription,
                                                  TryParser,
                                                  out Value,
                                                  out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion



        #region ParseMandatoryEnum<TEnum>(this JSON, PropertyName, PropertyDescription, DefaultServerName,            out EnumValue, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatoryEnum<TEnum>(this JObject              JSON,
                                                        String                    PropertyName,
                                                        String                    PropertyDescription,
                                                        String                    DefaultServerName,
                                                        out TEnum                 EnumValue,
                                                        HTTPRequest               HTTPRequest,
                                                        out HTTPResponse.Builder  HTTPResponse)

             where TEnum : struct

        {

            var success = JSON.ParseMandatoryEnum(PropertyName,
                                                  PropertyDescription,
                                                  out EnumValue,
                                                  out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion


        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Boolean,   HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out Boolean               BooleanValue,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out BooleanValue,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Single,    HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out Single                SingleValue,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out SingleValue,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Double,    HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out Double                DoubleValue,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out DoubleValue,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Decimal,   HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out Decimal               DecimalValue,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out DecimalValue,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Byte,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out Byte                  ByteValue,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out ByteValue,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out SByte,     HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out SByte                 SByteValue,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out SByteValue,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Int32,     HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out Int32                 Int32Value,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out Int32Value,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Int64,     HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out Int64                 Int64Value,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out Int64Value,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Timestamp, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject                                    JSON,
                                             String                                          PropertyName,
                                             String                                          PropertyDescription,
                                             String                                          DefaultServerName,
                                             out DateTime                                    Timestamp,
                                             HTTPRequest                                     HTTPRequest,
                                             [NotNullWhen(false)] out HTTPResponse.Builder?  HTTPResponse)

        {

            if (!JSON.ParseMandatory(PropertyName,
                                     PropertyDescription,
                                     out DateTime  timestamp,
                                     out String?   errorResponse))
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Illias.Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          errorResponse is not null
                                              ? new JProperty("description", errorResponse)
                                              : null
                                      ).ToUTF8Bytes()
                };

                Timestamp = default;
                return false;

            }

            Timestamp     = timestamp;
            HTTPResponse  = null;
            return true;

        }


        public static Boolean ParseMandatory(this JObject                                    JSON,
                                             String                                          PropertyName,
                                             String                                          PropertyDescription,
                                             String                                          DefaultServerName,
                                             out DateTimeOffset                              Timestamp,
                                             HTTPRequest                                     HTTPRequest,
                                             [NotNullWhen(false)] out HTTPResponse.Builder?  HTTPResponse)

        {

            if (!JSON.ParseMandatory(PropertyName,
                                     PropertyDescription,
                                     out DateTimeOffset  timestamp,
                                     out String?         errorResponse))
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Illias.Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          errorResponse is not null
                                              ? new JProperty("description", errorResponse)
                                              : null
                                      ).ToUTF8Bytes()
                };

                Timestamp = default;
                return false;

            }

            Timestamp     = timestamp;
            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out I18NText,  HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out I18NString            I18NText,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)

        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out I18NText,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out JObject,   HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out JObject               JObject,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out JObject,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out JArray,    HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out JArray                JArray,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out JArray,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,       out StringArray,    HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject              JSON,
                                             String                    PropertyName,
                                             String                    PropertyDescription,
                                             String                    DefaultServerName,
                                             out IEnumerable<String>   StringArray,
                                             HTTPRequest               HTTPRequest,
                                             out HTTPResponse.Builder  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out StringArray,
                                              out String ErrorResponse);

            if (ErrorResponse is not null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = Timestamp.Now,
                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        #endregion






        // -------------------------------------------------------------------------------------------------------------------------------------
        // Parse Optional
        // -------------------------------------------------------------------------------------------------------------------------------------

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName, Mapper, out Value,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject              JSON,
                                               String                    PropertyName,
                                               String                    PropertyDescription,
                                               String                    DefaultServerName,
                                               Func<String, T>           Mapper,
                                               out T                     Value,
                                               HTTPRequest               HTTPRequest,
                                               out HTTPResponse.Builder  HTTPResponse)
        {

            var result = JSON.ParseOptional(PropertyName,
                                            PropertyDescription,
                                            Mapper,
                                            out Value,
                                            out var ErrorResponse);

            if (ErrorResponse is null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = Timestamp.Now,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion


        #region ParseOptionalStruct<TStruct?>(this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptionalStruct2<TStruct>(this JObject                                   JSON,
                                                           String                                          PropertyName,
                                                           String                                          PropertyDescription,
                                                           String                                          DefaultServerName,
                                                           TryParser<TStruct>                              Parser,
                                                           out TStruct?                                    Value,
                                                           HTTPRequest                                     HTTPRequest,
                                                           [NotNullWhen(false)] out HTTPResponse.Builder?  HTTPResponse)

            where TStruct : struct

        {

            var result = JSON.ParseOptional(PropertyName,
                                            PropertyDescription,
                                            Parser,
                                            out TStruct value,
                                            out var     errorResponse);

            if (errorResponse is null)
            {
                Value         = value;
                HTTPResponse  = null;
            }

            else
            {
                Value         = default;
                HTTPResponse  = new HTTPResponse.Builder(HTTPRequest) {
                                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                    Server          = DefaultServerName,
                                    Date            = Timestamp.Now,
                                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                                    Content         = JSONObject.Create(
                                                          new JProperty("description", errorResponse)
                                                      ).ToUTF8Bytes()
                                };
            }

            return result;

        }

        #endregion

        #region ParseOptionalStruct<TStruct> (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptionalStruct<TStruct>(this JObject               JSON,
                                                           String                     PropertyName,
                                                           String                     PropertyDescription,
                                                           String                     DefaultServerName,
                                                           TryParser<TStruct>         Parser,
                                                           out TStruct                Value,
                                                           HTTPRequest                HTTPRequest,
                                                           out HTTPResponse.Builder?  HTTPResponse)

            where TStruct : struct

        {

            var result = JSON.ParseOptionalStruct(PropertyName,
                                                  PropertyDescription,
                                                  Parser,
                                                  out TStruct NullableValue,
                                                  out var     errorResponse);

            if (errorResponse is null)
            {
                Value         = NullableValue;
                HTTPResponse  = null;
            }

            else
            {
                Value         = default;
                HTTPResponse  = new HTTPResponse.Builder(HTTPRequest) {
                                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                    Server          = DefaultServerName,
                                    Date            = Timestamp.Now,
                                    ContentType     = HTTPContentType.Application.JSON_UTF8,
                                    Content         = JSONObject.Create(
                                                          new JProperty("description", errorResponse)
                                                      ).ToUTF8Bytes()
                                };
            }

            return result;

        }

        #endregion


        #region ParseOptional<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser,           out Value,     HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject              JSON,
                                               String                    PropertyName,
                                               String                    PropertyDescription,
                                               String                    DefaultServerName,
                                               TryParser<T>              Parser,
                                               out T                     Value,
                                               HTTPRequest               HTTPRequest,
                                               out HTTPResponse.Builder  HTTPResponse)
        {

            var result = JSON.ParseOptional(PropertyName,
                                            PropertyDescription,
                                            Parser,
                                            out Value,
                                            out String ErrorResponse);

            if (ErrorResponse is null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = Timestamp.Now,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, TryJObjectParser, out Value,     HTTPRequest, out HTTPResponse)

        //public static Boolean ParseOptional<T>(this JObject              JSON,
        //                                       String                    PropertyName,
        //                                       String                    PropertyDescription,
        //                                       String                    DefaultServerName,
        //                                       TryJObjectParser2<T>      TryJObjectParser,
        //                                       out T                     Value,
        //                                       HTTPRequest               HTTPRequest,
        //                                       out HTTPResponse.Builder  HTTPResponse)
        //{

        //    var result = JSON.ParseOptionalJSON(PropertyName,
        //                                    PropertyDescription,
        //                                    TryJObjectParser,
        //                                    out Value,
        //                                    out String ErrorResponse);

        //    if (ErrorResponse is null)
        //        HTTPResponse = null;

        //    else
        //        HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
        //            HTTPStatusCode  = HTTPStatusCode.BadRequest,
        //            Server          = DefaultServerName,
        //            Date            = Timestamp.Now,
        //            ContentType     = HTTPContentType.Application.JSON_UTF8,
        //            Content         = JSONObject.Create(
        //                                  new JProperty("description", ErrorResponse)
        //                              ).ToUTF8Bytes()
        //        };

        //    return result;

        //}

        //public static Boolean ParseOptional<T>(this JObject         JSON,
        //                                       String               PropertyName,
        //                                       String               PropertyDescription,
        //                                       String               DefaultServerName,
        //                                       TryJObjectParser2<T>  TryParser,
        //                                       out T?               Value,
        //                                       HTTPRequest          HTTPRequest,
        //                                       out HTTPResponse     HTTPResponse)

        //    where T : struct

        //{

        //    var result = JSON.ParseMandatoryJSONS(PropertyName,
        //                                         PropertyDescription,
        //                                         TryParser,
        //                                         out Value,
        //                                         out String? ErrorResponse);

        //    if (ErrorResponse is null)
        //        HTTPResponse = null;

        //    else
        //        HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
        //            HTTPStatusCode  = HTTPStatusCode.BadRequest,
        //            Server          = DefaultServerName,
        //            Date            = Timestamp.Now,
        //            ContentType     = HTTPContentType.Application.JSON_UTF8,
        //            Content         = JSONObject.Create(
        //                                  new JProperty("description", ErrorResponse)
        //                              ).ToUTF8Bytes()
        //        };

        //    return result;

        //}

        #endregion


        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out I18NText,   HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject              JSON,
                                            String                    PropertyName,
                                            String                    PropertyDescription,
                                            String                    DefaultServerName,
                                            out I18NString            I18NText,
                                            HTTPRequest               HTTPRequest,
                                            out HTTPResponse.Builder  HTTPResponse)
        {

            var result = JSON.ParseOptional(PropertyName,
                                            PropertyDescription,
                                            out I18NText,
                                            out String ErrorResponse);

            if (ErrorResponse is null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = Timestamp.Now,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional<TEnum>(this JSON, PropertyName, PropertyDescription, DefaultServerName,         out EnumValue,  HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<TEnum>(this JObject              JSON,
                                                   String                    PropertyName,
                                                   String                    PropertyDescription,
                                                   String                    DefaultServerName,
                                                   out TEnum?                EnumValue,
                                                   HTTPRequest               HTTPRequest,
                                                   out HTTPResponse.Builder  HTTPResponse)

            where TEnum : struct

        {

            var result = JSON.ParseOptionalEnum(PropertyName,
                                            PropertyDescription,
                                            out EnumValue,
                                            out String ErrorResponse);

            if (ErrorResponse is null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = Timestamp.Now,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out Timestamp,  HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject              JSON,
                                            String                    PropertyName,
                                            String                    PropertyDescription,
                                            String                    DefaultServerName,
                                            out DateTime?             Timestamp,
                                            HTTPRequest               HTTPRequest,
                                            out HTTPResponse.Builder  HTTPResponse)
        {

            var result = JSON.ParseOptional(PropertyName,
                                            PropertyDescription,
                                            out Timestamp,
                                            out String ErrorResponse);

            if (ErrorResponse is null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = Illias.Timestamp.Now,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out TimeSpan,   HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject              JSON,
                                            String                    PropertyName,
                                            String                    PropertyDescription,
                                            String                    DefaultServerName,
                                            out TimeSpan?             TimeSpan,
                                            HTTPRequest               HTTPRequest,
                                            out HTTPResponse.Builder  HTTPResponse)
        {

            var result = JSON.ParseOptional(PropertyName,
                                            PropertyDescription,
                                            out TimeSpan,
                                            out String ErrorResponse);

            if (ErrorResponse is null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = Timestamp.Now,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion


        #region ParseOptional<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Values,     HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject        JSON,
                                               String              PropertyName,
                                               String              PropertyDescription,
                                               TryParser<T>        Parser,
                                               out IEnumerable<T>  Values,
                                               out String          ErrorResponse)
        {

            var _Values    = new List<T>();
            Values         = _Values;
            ErrorResponse  = null;

            if (JSON is null)
            {
                ErrorResponse = "The given JSON object must not be null!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return true;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {

                // "properyKey": null -> will be ignored!
                if (JSONToken is null)
                    return false;

                if (!(JSONToken is JArray JSONArray))
                {
                    ErrorResponse = "Invalid " + PropertyDescription + "!";
                    return true;
                }

                foreach (var item in JSONArray)
                {

                    try
                    {

                        if (!Parser(item.ToString(), out T Value))
                        {
                            ErrorResponse = "Could not parse item '" + item + "' within the " + PropertyDescription + " array!";
                            Values = new T[0];
                            return false;
                        }

                        _Values.Add(Value);

                    }
                    catch
                    {
                        ErrorResponse = "Invalid item '" + item + "' within the " + PropertyDescription + " array!";
                        return true;
                    }

                }

                return true;

            }

            return false;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out JSONObject, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject              JSON,
                                            String                    PropertyName,
                                            String                    PropertyDescription,
                                            String                    DefaultServerName,
                                            out JObject               JSONObject,
                                            HTTPRequest               HTTPRequest,
                                            out HTTPResponse.Builder  HTTPResponse)
        {

            var result = JSON.ParseOptional(PropertyName,
                                            PropertyDescription,
                                            out JSONObject,
                                            out String ErrorResponse);

            if (ErrorResponse is null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = Timestamp.Now,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = Illias.JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out JSONArray,  HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject              JSON,
                                            String                    PropertyName,
                                            String                    PropertyDescription,
                                            String                    DefaultServerName,
                                            out JArray                JSONArray,
                                            HTTPRequest               HTTPRequest,
                                            out HTTPResponse.Builder  HTTPResponse)
        {

            var result = JSON.ParseOptional(PropertyName,
                                            PropertyDescription,
                                            out JSONArray,
                                            out String ErrorResponse);

            if (ErrorResponse is null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = Timestamp.Now,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptionalHashSet(this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out HashSet,    HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptionalHashSet<T>(this JObject              JSON,
                                                      String                    PropertyName,
                                                      String                    PropertyDescription,
                                                      String                    DefaultServerName,
                                                      TryParser<T>              Parser,
                                                      out HashSet<T>            HashSet,
                                                      HTTPRequest               HTTPRequest,
                                                      out HTTPResponse.Builder  HTTPResponse)
        {

            var result = JSON.ParseOptionalHashSet(PropertyName,
                                                   PropertyDescription,
                                                   Parser,
                                                   out HashSet,
                                                   out String ErrorResponse);

            if (ErrorResponse is null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = Timestamp.Now,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion


    }

}
