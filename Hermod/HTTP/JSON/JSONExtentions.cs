/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Globalization;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Illias.ConsoleLog;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extention methods to parse JSON.
    /// </summary>
    public static class JSONExtentions
    {

        #region Contains             (this JSON, PropertyName)

        public static Boolean Contains(this JObject  JSON,
                                       String        PropertyName)
        {

            if (JSON == null || PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
                return false;

            return JSON[PropertyName] != null;

        }

        #endregion

        #region GetString            (this JSON, PropertyName)

        public static String GetString(this JObject  JSON,
                                       String        PropertyName)
        {

            if (JSON == null || PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
                return null;

            return JSON[PropertyName]?.Value<String>();

        }

        #endregion


        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription,                               out Text,                   out ErrorResponse)

        public static Boolean ParseMandatory(this JObject  JSON,
                                             String        PropertyName,
                                             String        PropertyDescription,
                                             out String    Text,
                                             out String    ErrorResponse)
        {

            Text = String.Empty;

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing property '" + PropertyName + "'!";
                return false;
            }

            try
            {

                Text = JSONToken?.Value<String>();

            }
            catch (Exception)
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Text,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out String        Text,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out Text,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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

        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription,                    Mapper,    out Value,                  out ErrorResponse)

        public static Boolean ParseMandatory<T>(this JObject     JSON,
                                                String           PropertyName,
                                                String           PropertyDescription,
                                                Func<String, T>  Mapper,
                                                out T            Value,
                                                out String       ErrorResponse)
        {

            Value = default(T);

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (Mapper == null)
            {
                ErrorResponse = "Invalid mapper provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "'!";
                return false;
            }

            try
            {

                var JSONValue = JSONToken?.Value<String>()?.Trim();

                if (JSONValue.IsNeitherNullNorEmpty())
                {
                    Value          = Mapper(JSONValue);
                    ErrorResponse  = null;
                    return true;
                }

            }
#pragma warning disable RCS1075  // Avoid empty catch clause that catches System.Exception.
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch (Exception)
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
#pragma warning restore RCS1075  // Avoid empty catch clause that catches System.Exception.
            { }

            Value          = default(T);
            ErrorResponse  = "Invalid " + PropertyDescription ?? PropertyName + "!";
            return false;

        }

        public static Boolean ParseMandatory<T>(this JObject     JSON,
                                                String           PropertyName,
                                                String           PropertyDescription,
                                                Func<String, T>  Mapper,
                                                out T?           Value,
                                                out String       ErrorResponse)

            where T : struct

        {

            Value = null;

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (Mapper == null)
            {
                ErrorResponse = "Invalid mapper provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "'!";
                return false;
            }

            try
            {

                var JSONValue = JSONToken?.Value<String>()?.Trim();

                if (JSONValue.IsNeitherNullNorEmpty())
                {
                    Value          = Mapper(JSONValue);
                    ErrorResponse  = null;
                    return true;
                }

            }
#pragma warning disable RCS1075  // Avoid empty catch clause that catches System.Exception.
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch (Exception)
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
#pragma warning restore RCS1075  // Avoid empty catch clause that catches System.Exception.
            { }

            Value          = null;
            ErrorResponse  = "Invalid " + PropertyDescription ?? PropertyName + "!";
            return false;

        }

        #endregion

        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, Mapper,    out Value,     HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory<T>(this JObject      JSON,
                                                String            PropertyName,
                                                String            PropertyDescription,
                                                String            DefaultServerName,
                                                Func<String, T>   Mapper,
                                                out T             Value,
                                                HTTPRequest       HTTPRequest,
                                                out HTTPResponse  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              Mapper,
                                              out Value,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        public static Boolean ParseMandatory<T>(this JObject      JSON,
                                                String            PropertyName,
                                                String            PropertyDescription,
                                                String            DefaultServerName,
                                                Func<String, T>   Mapper,
                                                out T?            Value,
                                                HTTPRequest       HTTPRequest,
                                                out HTTPResponse  HTTPResponse)

            where T : struct

        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              Mapper,
                                              out Value,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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

        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription,                    TryParser, out Value,                  out ErrorResponse)

        public static Boolean ParseMandatory<T>(this JObject  JSON,
                                                String        PropertyName,
                                                String        PropertyDescription,
                                                TryParser<T>  TryParser,
                                                out T         Value,
                                                out String    ErrorResponse)
        {

            Value = default(T);

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (TryParser == null)
            {
                ErrorResponse = "Invalid mapper provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "'!";
                return false;
            }

            var JSONValue = JSONToken?.Value<String>()?.Trim();

            if (JSONValue.IsNeitherNullNorEmpty() &&
                //!TryParser(JSONValue, out Value))
                TryParser(JSONValue, out Value))
            {
                ErrorResponse = null;
                return true;
            }

            Value          = default(T);
            ErrorResponse  = "Invalid " + PropertyDescription ?? PropertyName + "!";
            return false;

        }

        public static Boolean ParseMandatory<T>(this JObject  JSON,
                                                String        PropertyName,
                                                String        PropertyDescription,
                                                TryParser<T>  TryParser,
                                                out T?        Value,
                                                out String    ErrorResponse)

            where T : struct

        {

            Value = null;

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (TryParser == null)
            {
                ErrorResponse = "Invalid mapper provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "'!";
                return false;
            }

            var JSONValue = JSONToken?.Value<String>()?.Trim();

            if (JSONValue.IsNeitherNullNorEmpty() &&
                //!TryParser(JSONValue, out T _Value))
                TryParser(JSONValue, out T _Value))
            {
                Value          = _Value;
                ErrorResponse  = null;
                return true;
            }

            Value          = null;
            ErrorResponse  = "Invalid " + PropertyDescription ?? PropertyName + "!";
            return false;

        }

        #endregion

        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, TryParser, out Value,     HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory<T>(this JObject      JSON,
                                                String            PropertyName,
                                                String            PropertyDescription,
                                                String            DefaultServerName,
                                                TryParser<T>      TryParser,
                                                out T             Value,
                                                HTTPRequest       HTTPRequest,
                                                out HTTPResponse  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              TryParser,
                                              out Value,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        public static Boolean ParseMandatory<T>(this JObject      JSON,
                                                String            PropertyName,
                                                String            PropertyDescription,
                                                String            DefaultServerName,
                                                TryParser<T>      TryParser,
                                                out T?            Value,
                                                HTTPRequest       HTTPRequest,
                                                out HTTPResponse  HTTPResponse)

            where T : struct

        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              TryParser,
                                              out Value,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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

        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription,                    TryJObjectParser, out Value,                  out ErrorResponse)

        public static Boolean ParseMandatory<T>(this JObject         JSON,
                                                String               PropertyName,
                                                String               PropertyDescription,
                                                TryJObjectParser<T>  TryJObjectParser,
                                                out T                Value,
                                                out String           ErrorResponse)
        {

            Value = default(T);

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (TryJObjectParser == null)
            {
                ErrorResponse = "Invalid mapper provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "' (" + PropertyDescription + ")!";
                return false;
            }

            if (!(JSONToken is JObject JSONValue))
            {
                ErrorResponse = "Invalid JSON object '" + PropertyName + "' (" + PropertyDescription + ")!";
                return false;
            }

            if (!TryJObjectParser(JSONValue, out T _Value, out String ErrorResponse2))
            {
                Value         = default(T);
                ErrorResponse = "JSON property '" + PropertyName + "' (" + PropertyDescription + ") could not be parsed: " + ErrorResponse2;
                return false;
            }

            Value         = _Value;
            ErrorResponse = null;
            return true;

        }

        public static Boolean ParseMandatory<T>(this JObject         JSON,
                                                String               PropertyName,
                                                String               PropertyDescription,
                                                TryJObjectParser<T>  TryJObjectParser,
                                                out T?               Value,
                                                out String           ErrorResponse)

            where T : struct

        {

            Value = null;

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (TryJObjectParser == null)
            {
                ErrorResponse = "Invalid mapper provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "' (" + PropertyDescription + ")!";
                return false;
            }

            if (!(JSONToken is JObject JSONValue))
            {
                ErrorResponse = "Invalid JSON object '" + PropertyName + "' (" + PropertyDescription + ")!";
                return false;
            }

            if (!TryJObjectParser(JSONValue, out T _Value, out String ErrorResponse2))
            {
                Value         = null;
                ErrorResponse = "JSON property '" + PropertyName + "' (" + PropertyDescription + ") could not be parsed: " + ErrorResponse2;
                return false;
            }

            Value         = _Value;
            ErrorResponse = null;
            return true;
 
        }

        #endregion

        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, TryJObjectParser, out Value,     HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory<T>(this JObject         JSON,
                                                String               PropertyName,
                                                String               PropertyDescription,
                                                String               DefaultServerName,
                                                TryJObjectParser<T>  TryJObjectParser,
                                                out T                Value,
                                                HTTPRequest          HTTPRequest,
                                                out HTTPResponse     HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              TryJObjectParser,
                                              out Value,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
                    Content         = JSONObject.Create(
                                          new JProperty("description", ErrorResponse)
                                      ).ToUTF8Bytes()
                };

                return false;

            }

            HTTPResponse = null;
            return success;

        }

        public static Boolean ParseMandatory<T>(this JObject         JSON,
                                                String               PropertyName,
                                                String               PropertyDescription,
                                                String               DefaultServerName,
                                                TryJObjectParser<T>  TryParser,
                                                out T?               Value,
                                                HTTPRequest          HTTPRequest,
                                                out HTTPResponse     HTTPResponse)

            where T : struct

        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              TryParser,
                                              out Value,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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


        #region ParseMandatory<TEnum>(this JSON, PropertyName, PropertyDescription,                               out EnumValue,              out ErrorResponse)

        public static Boolean ParseMandatory<TEnum>(this JObject  JSON,
                                                    String        PropertyName,
                                                    String        PropertyDescription,
                                                    out TEnum     EnumValue,
                                                    out String    ErrorResponse)

             where TEnum : struct

        {

            EnumValue = default(TEnum);

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "'!";
                return false;
            }

            if (JSONToken == null ||
                !Enum.TryParse(JSONToken.Value<String>(), true, out EnumValue))
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion

        #region ParseMandatory<TEnum>(this JSON, PropertyName, PropertyDescription, DefaultServerName,            out EnumValue, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory<TEnum>(this JObject      JSON,
                                                    String            PropertyName,
                                                    String            PropertyDescription,
                                                    String            DefaultServerName,
                                                    out TEnum         EnumValue,
                                                    HTTPRequest       HTTPRequest,
                                                    out HTTPResponse  HTTPResponse)

             where TEnum : struct

        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out EnumValue,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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


        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription,                               out Boolean,                out ErrorResponse)

        public static Boolean ParseMandatory(this JObject  JSON,
                                             String        PropertyName,
                                             String        PropertyDescription,
                                             out Boolean   BooleanValue,
                                             out String    ErrorResponse)
        {

            BooleanValue = default(Boolean);

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "'!";
                return false;
            }

            if (JSONToken == null)
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            try
            {
                BooleanValue = JSONToken.Value<Boolean>();
            }
            catch (Exception e)
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Boolean,   HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out Boolean       BooleanValue,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out BooleanValue,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription,                               out Single,                 out ErrorResponse)

        public static Boolean ParseMandatory(this JObject  JSON,
                                             String        PropertyName,
                                             String        PropertyDescription,
                                             out Single    SingleValue,
                                             out String    ErrorResponse)
        {

            SingleValue = default(Single);

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "'!";
                return false;
            }

            if (JSONToken == null ||
                !Single.TryParse(JSONToken.Value<String>(), out SingleValue))
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Single,    HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out Single        SingleValue,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out SingleValue,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription,                               out Double,                 out ErrorResponse)

        public static Boolean ParseMandatory(this JObject  JSON,
                                             String        PropertyName,
                                             String        PropertyDescription,
                                             out Double    DoubleValue,
                                             out String    ErrorResponse)
        {

            DoubleValue = default(Double);

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing JSON property '" + PropertyName + "'!";
                return false;
            }

            if (JSONToken == null ||
                !Double.TryParse(JSONToken.Value<String>(), out DoubleValue))
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Double,    HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out Double        DoubleValue,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out DoubleValue,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription,                               out Timestamp,              out ErrorResponse)

        public static Boolean ParseMandatory(this JObject  JSON,
                                             String        PropertyName,
                                             String        PropertyDescription,
                                             out DateTime  Timestamp,
                                             out String    ErrorResponse)

        {

            Timestamp = DateTime.MinValue;

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing property '" + PropertyName + "'!";
                return false;
            }

            try
            {

                Timestamp = JSONToken.Value<DateTime>();

                if (Timestamp.Kind != DateTimeKind.Utc)
                    Timestamp = Timestamp.ToUniversalTime();

            }
            catch (Exception)
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out Timestamp, HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out DateTime      Timestamp,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)

        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out Timestamp,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription,                               out I18NText,               out ErrorResponse)

        public static Boolean ParseMandatory(this JObject    JSON,
                                             String          PropertyName,
                                             String          PropertyDescription,
                                             out I18NString  I18NText,
                                             out String      ErrorResponse)

        {

            I18NText = I18NString.Empty;

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing property '" + PropertyName + "'!";
                return false;
            }

            var i18nJSON = JSONToken as JObject;

            if (i18nJSON == null)
            {
                ErrorResponse = "Invalid i18n JSON string provided!";
                return false;
            }

            var i18NString = I18NString.Empty;

            foreach (var i18nProperty in i18nJSON)
            {

                try
                {

                    i18NString.Add((Languages) Enum.Parse(typeof(Languages), i18nProperty.Key),
                                   i18nProperty.Value.Value<String>());

                } catch (Exception)
                {
                    ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                    return false;
                }

            }

            ErrorResponse = null;
            I18NText      = i18NString;
            return true;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out I18NText,  HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out I18NString    I18NText,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)

        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out I18NText,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription,                               out JObject,                out ErrorResponse)

        public static Boolean ParseMandatory(this JObject  JSON,
                                             String        PropertyName,
                                             String        PropertyDescription,
                                             out JObject   JObject,
                                             out String    ErrorResponse)
        {

            JObject = null;

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing property '" + PropertyName + "'!";
                return false;
            }

            try
            {

                JObject = JSONToken as JObject;

            }
            catch (Exception)
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out JObject,   HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out JObject       JObject,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out JObject,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription,                               out JArray,                 out ErrorResponse)

        public static Boolean ParseMandatory(this JObject  JSON,
                                             String        PropertyName,
                                             String        PropertyDescription,
                                             out JArray    JArray,
                                             out String    ErrorResponse)
        {

            JArray = null;

            if (JSON == null)
            {
                ErrorResponse = "Invalid JSON provided!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return false;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing property '" + PropertyName + "'!";
                return false;
            }

            try
            {

                JArray = JSONToken as JArray;

            }
            catch (Exception)
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion

        #region ParseMandatory       (this JSON, PropertyName, PropertyDescription, DefaultServerName,            out JArray,    HTTPRequest, out HTTPResponse)

        public static Boolean ParseMandatory(this JObject      JSON,
                                             String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out JArray        JArray,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)
        {

            var success = JSON.ParseMandatory(PropertyName,
                                              PropertyDescription,
                                              out JArray,
                                              out String ErrorResponse);

            if (ErrorResponse != null)
            {

                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                    HTTPStatusCode  = HTTPStatusCode.BadRequest,
                    Server          = DefaultServerName,
                    Date            = DateTime.UtcNow,
                    ContentType     = HTTPContentType.JSON_UTF8,
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


        // Mandatory multiple values...

        #region GetMandatory(this JSON, Key, out Values)

        public static Boolean GetMandatory(this JObject             JSON,
                                           String                   Key,
                                           out IEnumerable<String>  Values)
        {

            if (JSON.TryGetValue(Key, out JToken JSONToken) && 
                JSONToken is JArray _Values)
            {

                Values = _Values.AsEnumerable().
                                 Select(jtoken => jtoken.Value<String>()).
                                 Where (value  => value != null);

                return true;

            }

            Values = null;
            return false;

        }

        #endregion




        public static Boolean ParseMandatory<T>(this JObject      JSON,
                                                String            PropertyName,
                                                Func<String, T>   Mapper,
                                                T                 InvalidResult,
                                                out T             TOut)
        {

            if (JSON == null ||
                PropertyName.IsNullOrEmpty() ||
                Mapper == null)
            {
                TOut = default(T);
                return false;
            }

            if (JSON.TryGetValue(PropertyName, out JToken _JToken) && _JToken?.Value<String>() != null)
            {

                try
                {

                    TOut = Mapper(_JToken?.Value<String>());

                    return !TOut.Equals(InvalidResult);

                }
#pragma warning disable RCS1075  // Avoid empty catch clause that catches System.Exception.
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
                catch (Exception)
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
#pragma warning restore RCS1075  // Avoid empty catch clause that catches System.Exception.
                { }

            }

            TOut = default(T);
            return false;

        }




        // Parse Optional

        public static Boolean ParseOptional(this JObject  JSON,
                                            String        PropertyName,
                                            out String    StringOut)
        {

            if (JSON == null ||
                PropertyName.IsNullOrEmpty())
            {
                StringOut = String.Empty;
                return false;
            }

            if (JSON.TryGetValue(PropertyName, out JToken _JToken))
            {

                StringOut = _JToken?.Value<String>();

                if (StringOut != null)
                    return true;

                return false;

            }

            StringOut = null;
            return true;

        }

        public static Boolean ParseOptional2(this JObject  JSON,
                                             String        PropertyName,
                                             out String    StringOut)
        {

            if (JSON == null ||
                PropertyName.IsNullOrEmpty())
            {
                StringOut = String.Empty;
                return false;
            }

            if (JSON.TryGetValue(PropertyName, out JToken _JToken))
            {

                StringOut = _JToken?.Value<String>();

                if (StringOut != null)
                    return true;

                return false;

            }

            StringOut = null;
            return false;

        }


        #region ParseOptional       (this JSON, PropertyName, PropertyDescription,                                 out BooleanOut,         out ErrorResponse)

        public static Boolean ParseOptional(this JObject  JSON,
                                            String        PropertyName,
                                            String        PropertyDescription,
                                            out Boolean?  BooleanOut,
                                            out String    ErrorResponse)
        {

            BooleanOut = new Boolean?();

            if (JSON == null)
            {
                ErrorResponse = "The given JSON object must not be null!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return true;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {

                if (JSONToken == null)
                {
                    ErrorResponse = "Unknown or invalid '" + PropertyDescription + "'!";
                    return true;
                }

                BooleanOut    = JSONToken.Value<Boolean>();
                ErrorResponse = null;
                return true;

            }

            ErrorResponse = null;
            return true;

        }

        #endregion


        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName, Mapper, out Value,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject      JSON,
                                               String            PropertyName,
                                               String            PropertyDescription,
                                               String            DefaultServerName,
                                               Func<String, T>   Mapper,
                                               out T             Value,
                                               HTTPRequest       HTTPRequest,
                                               out HTTPResponse  HTTPResponse)
        {

            var result = ParseOptional(JSON,
                                       PropertyName,
                                       PropertyDescription,
                                       Mapper,
                                       out Value,
                                       out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription,                    Mapper, out Value,                   out ErrorResponse)

        public static Boolean ParseOptional<T>(this JObject     JSONIn,
                                               String           PropertyName,
                                               String           PropertyDescription,
                                               Func<String, T>  Mapper,
                                               out T            Value,
                                               out String       ErrorResponse)
        {

            if (JSONIn.TryGetValue(PropertyName, out JToken JSONToken) && JSONToken != null)
            {
                Value          = JSONToken.Type == JTokenType.String
                                     ? Mapper(JSONToken.Value<String>())
                                     : Mapper(JSONToken.ToString());
                ErrorResponse  = null;
                return true;
            }

            Value         = default(T);
            ErrorResponse = null;
            return false;

        }

        #endregion


        #region ParseOptionalS<TStruct>  (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptionalS<TStruct>(this JObject        JSON,
                                                      String              PropertyName,
                                                      String              PropertyDescription,
                                                      String              DefaultServerName,
                                                      TryParser<TStruct>  Parser,
                                                      out TStruct         Value,
                                                      HTTPRequest         HTTPRequest,
                                                      out HTTPResponse    HTTPResponse)

            where TStruct : struct

        {

            var result = ParseOptional(JSON,
                                       PropertyName,
                                       PropertyDescription,
                                       Parser,
                                       out Value,
                                       out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion




        #region ParseOptionalN<TStruct?>(this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptionalN<TStruct>(this JObject        JSON,
                                                      String              PropertyName,
                                                      String              PropertyDescription,
                                                      String              DefaultServerName,
                                                      TryParser<TStruct>  Parser,
                                                      out TStruct?        Value,
                                                      HTTPRequest         HTTPRequest,
                                                      out HTTPResponse    HTTPResponse)

            where TStruct : struct

        {

            var result = ParseOptionalN(JSON,
                                        PropertyName,
                                        PropertyDescription,
                                        Parser,
                                        out Value,
                                        out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptionalN<TStruct?>(this JSON, PropertyName, PropertyDescription,                    Parser, out Value,              out ErrorResponse)

        public static Boolean ParseOptionalN<TStruct>(this JObject        JSON,
                                                      String              PropertyName,
                                                      String              PropertyDescription,
                                                      TryParser<TStruct>  Parser,
                                                      out TStruct?        Value,
                                                      out String          ErrorResponse)

            where TStruct : struct

        {

            Value = new TStruct?();

            if (JSON == null)
            {
                ErrorResponse = "The given JSON object must not be null!";
                return false;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken) && JSONToken != null)
            {

                if ((JSONToken as JObject)?.Count == 0)
                {
                    ErrorResponse = null;
                    return true;
                }

                if ((JSONToken as JArray)?.Count == 0)
                {
                    ErrorResponse = null;
                    return true;
                }

                if (!Parser(JSONToken.ToString(), out TStruct _Value))
                {
                    ErrorResponse =  "Unknown " + PropertyDescription + "!";
                    Value         = new TStruct?();
                    return false;
                }

                Value = new TStruct?(_Value);

            }

            ErrorResponse = null;
            return true;

        }

        #endregion


        #region ParseOptional<T>        (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject      JSON,
                                               String            PropertyName,
                                               String            PropertyDescription,
                                               String            DefaultServerName,
                                               TryParser<T>      Parser,
                                               out T             Value,
                                               HTTPRequest       HTTPRequest,
                                               out HTTPResponse  HTTPResponse)
        {

            var result = ParseOptional(JSON,
                                       PropertyName,
                                       PropertyDescription,
                                       Parser,
                                       out Value,
                                       out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        // !!!!!!!!!!!!!!
        #region ParseOptional<T>        (this JSON, PropertyName, PropertyDescription,                    Parser, out Value,              out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject      JSON,
                                               String            PropertyName,
                                               String            PropertyDescription,
                                               TryParser<T>      Parser,
                                               out T             Value,
                                               out String        ErrorResponse)
        {

            if (JSON == null)
            {
                Value         = default(T);
                ErrorResponse = "The given JSON object must not be null!";
                return false;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken) &&
                JSONToken != null)
            {

                if (!Parser(JSONToken.ToString(), out Value))
                {
                    Value          = default(T);
                    ErrorResponse  = "The value '" + JSONToken + "' is not valid for JSON property '" + PropertyDescription + "!";
                }

                else
                    ErrorResponse  = null;

                return true;

            }

            Value          = default(T);
            ErrorResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional<T>        (this JSON, PropertyName, PropertyDescription,             JObjectParser, out Value,              out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject         JSON,
                                               String               PropertyName,
                                               String               PropertyDescription,
                                               TryJObjectParser<T>  JObjectParser,
                                               out T                Value,
                                               out String           ErrorResponse)
        {

            if (JSON == null)
            {
                Value         = default(T);
                ErrorResponse = "The given JSON object must not be null!";
                return false;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken) &&
                JSONToken is JObject JSON2)
            {

                if (!JObjectParser(JSON2, out Value, out String ErrorResponse2))
                {
                    Value          = default(T);
                    ErrorResponse  = "JSON property '" + PropertyName + "' (" + PropertyDescription + ") could not be parsed: " + ErrorResponse2;
                }

                else
                    ErrorResponse  = null;

                return true;

            }

            Value          = default(T);
            ErrorResponse  = null;
            return true;

        }

        #endregion


        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out I18NText,   HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject      JSON,
                                            String            PropertyName,
                                            String            PropertyDescription,
                                            String            DefaultServerName,
                                            out I18NString    I18NText,
                                            HTTPRequest       HTTPRequest,
                                            out HTTPResponse  HTTPResponse)
        {

            var result = ParseOptional(JSON,
                                       PropertyName,
                                       PropertyDescription,
                                       out I18NText,
                                       out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription,                            out I18NText,                out ErrorResponse)

        public static Boolean ParseOptional(this JObject    JSON,
                                            String          PropertyName,
                                            String          PropertyDescription,
                                            out I18NString  I18NText,
                                            out String      ErrorResponse)

        {

            if (JSON == null)
            {
                I18NText      = I18NString.Empty;
                ErrorResponse = "The given JSON object must not be null!";
                return false;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken) && JSONToken != null)
            {

                var jobject = JSONToken as JObject;

                if (jobject == null)
                {
                    I18NText      = I18NString.Empty;
                    ErrorResponse = null;
                    return true;
                }

                var i18NString = I18NString.Empty;

                foreach (var jproperty in jobject)
                {

                    try
                    {

                        i18NString.Add((Languages) Enum.Parse(typeof(Languages), jproperty.Key),
                                       jproperty.Value.Value<String>());

                    } catch (Exception e)
                    {
                        ErrorResponse = "Invalid " + PropertyDescription + "!";
                        I18NText = null;
                        return false;
                    }

                }

                ErrorResponse = null;
                I18NText      = i18NString;
                return true;

            }

            ErrorResponse = null;
            I18NText      = I18NString.Empty;
            return true;

        }

        #endregion


        #region ParseOptional<TEnum>(this JSON, PropertyName, PropertyDescription, DefaultServerName,         out Value,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<TEnum>(this JObject      JSON,
                                                   String            PropertyName,
                                                   String            PropertyDescription,
                                                   String            DefaultServerName,
                                                   out TEnum?        Value,
                                                   HTTPRequest       HTTPRequest,
                                                   out HTTPResponse  HTTPResponse)

            where TEnum : struct

        {

            var result = ParseOptional(JSON,
                                       PropertyName,
                                       PropertyDescription,
                                       out Value,
                                       out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional<TEnum>(this JSON, PropertyName, PropertyDescription,                            out Value,                   out ErrorResponse)

        public static Boolean ParseOptional<TEnum>(this JObject  JSON,
                                                   String        PropertyName,
                                                   String        PropertyDescription,
                                                   out TEnum?    Value,
                                                   out String    ErrorResponse)

            where TEnum : struct

        {

            if (JSON == null)
            {
                Value         = default(TEnum);
                ErrorResponse = "The given JSON object must not be null!";
                return false;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {

                var JSONValue = JSONToken?.Value<String>();

                if (JSONValue == null)
                {
                    ErrorResponse  = "Unknown " + PropertyDescription + "!";
                    Value          = default(TEnum);
                    return true;
                }

                if (!Enum.TryParse(JSONValue, true, out TEnum _Value))
                {
                    ErrorResponse  = "Invalid " + PropertyDescription + "!";
                    Value          = default(TEnum);
                    return true;
                }

                Value          = _Value;
                ErrorResponse  = null;
                return true;

            }

            Value          = default(TEnum);
            ErrorResponse  = null;
            return false;

        }

        #endregion


        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out Timestamp,  HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject        JSON,
                                            String              PropertyName,
                                            String              PropertyDescription,
                                            String              DefaultServerName,
                                            out DateTime?       Timestamp,
                                            HTTPRequest         HTTPRequest,
                                            out HTTPResponse    HTTPResponse)
        {

            var result = ParseOptional(JSON,
                                       PropertyName,
                                       PropertyDescription,
                                       out Timestamp,
                                       out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription,                            out Timestamp,               out ErrorResponse)

        public static Boolean ParseOptional(this JObject   JSON,
                                            String         PropertyName,
                                            String         PropertyDescription,
                                            out DateTime?  Timestamp,
                                            out String     ErrorResponse)
        {

            Timestamp = null;

            if (JSON == null)
            {
                ErrorResponse = "The given JSON object must not be null!";
                return true;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return true;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing property '" + PropertyName + "'!";
                return true;
            }

            try
            {

                Timestamp = JSONToken.Value<DateTime>();

            }
            catch (Exception)
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion


        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out TimeSpan,   HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject        JSON,
                                            String              PropertyName,
                                            String              PropertyDescription,
                                            String              DefaultServerName,
                                            out TimeSpan?       TimeSpan,
                                            HTTPRequest         HTTPRequest,
                                            out HTTPResponse    HTTPResponse)
        {

            var result = ParseOptional(JSON,
                                       PropertyName,
                                       PropertyDescription,
                                       out TimeSpan,
                                       out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription,                            out TimeSpan,                out ErrorResponse)

        public static Boolean ParseOptional(this JObject   JSON,
                                            String         PropertyName,
                                            String         PropertyDescription,
                                            out TimeSpan?  Timespan,
                                            out String     ErrorResponse)
        {

            Timespan = null;

            if (JSON == null)
            {
                ErrorResponse = "The given JSON object must not be null!";
                return true;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return true;
            }

            if (!JSON.TryGetValue(PropertyName, out JToken JSONToken))
            {
                ErrorResponse = "Missing property '" + PropertyName + "'!";
                return true;
            }

            try
            {

                Timespan = TimeSpan.FromSeconds(JSONToken.Value<UInt32>());

            }
            catch (Exception)
            {
                ErrorResponse = "Invalid " + PropertyDescription ?? PropertyName + "!";
                return false;
            }

            ErrorResponse = null;
            return true;

        }

        #endregion


        #region ParseOptional<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value,      HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional<T>(this JObject        JSON,
                                               String              PropertyName,
                                               String              PropertyDescription,
                                               TryParser<T>        Parser,
                                               out IEnumerable<T>  Values,
                                               out String          ErrorResponse)
        {

            var _Values = new List<T>();

            if (JSON == null)
            {
                Values        = _Values;
                ErrorResponse = "The given JSON object must not be null!";
                return false;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken) &&
                JSONToken is JArray JSONArray)
            {

                foreach (var item in JSONArray)
                {

                    if (!Parser(item.ToString(), out T Value))
                    {
                        ErrorResponse = "Invalid item '" + item + "' in " + PropertyDescription + " array!";
                        Values = new T[0];
                        return false;
                    }

                    _Values.Add(Value);

                }

            }

            Values         = _Values;
            ErrorResponse  = null;
            return true;

        }

        #endregion


        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out JSONObject, HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject        JSON,
                                            String              PropertyName,
                                            String              PropertyDescription,
                                            String              DefaultServerName,
                                            out JObject         JSONObject,
                                            HTTPRequest         HTTPRequest,
                                            out HTTPResponse    HTTPResponse)
        {

            var result = ParseOptional(JSON,
                                       PropertyName,
                                       PropertyDescription,
                                       out JSONObject,
                                       out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = Illias.JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription,                            out JSONObject,              out ErrorResponse)

        public static Boolean ParseOptional(this JObject    JSON,
                                            String          PropertyName,
                                            String          PropertyDescription,
                                            out JObject     JSONObject,
                                            out String      ErrorResponse)

        {

            JSONObject = new JObject();

            if (JSON == null)
            {
                ErrorResponse = "The given JSON object must not be null!";
                return false;
            }

            if (PropertyName.IsNullOrEmpty() || PropertyName.Trim().IsNullOrEmpty())
            {
                ErrorResponse = "Invalid JSON property name provided!";
                return true;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken) && JSONToken != null)
            {

                JSONObject = JSONToken as JObject;

                if (JSONObject == null)
                {
                    ErrorResponse = "The given property '" + PropertyName + "' is not a valid JSON object!";
                    return true;
                }

                ErrorResponse = null;
                return true;

            }

            ErrorResponse = null;
            JSONObject    = null;
            return true;

        }

        #endregion


        #region ParseOptional       (this JSON, PropertyName, PropertyDescription, DefaultServerName,         out JSONArray,  HTTPRequest, out HTTPResponse)

        public static Boolean ParseOptional(this JObject        JSON,
                                            String              PropertyName,
                                            String              PropertyDescription,
                                            String              DefaultServerName,
                                            out JArray          JSONArray,
                                            HTTPRequest         HTTPRequest,
                                            out HTTPResponse    HTTPResponse)
        {

            var result = ParseOptional(JSON,
                                       PropertyName,
                                       PropertyDescription,
                                       out JSONArray,
                                       out String ErrorResponse);

            if (ErrorResponse == null)
                HTTPResponse = null;

            else
                HTTPResponse = new HTTPResponse.Builder(HTTPRequest) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   Server          = DefaultServerName,
                                   Date            = DateTime.UtcNow,
                                   ContentType     = HTTPContentType.JSON_UTF8,
                                   Content         = JSONObject.Create(
                                                         new JProperty("description", ErrorResponse)
                                                     ).ToUTF8Bytes()
                               };

            return result;

        }

        #endregion

        #region ParseOptional       (this JSON, PropertyName, PropertyDescription,                            out JSONArray,               out ErrorResponse)

        public static Boolean ParseOptional(this JObject    JSON,
                                            String          PropertyName,
                                            String          PropertyDescription,
                                            out JArray      JSONArray,
                                            out String      ErrorResponse)

        {

            JSONArray = new JArray();

            if (JSON == null)
            {
                ErrorResponse = "The given JSON object must not be null!";
                return true;
            }

            if (JSON.TryGetValue(PropertyName, out JToken JSONToken) && JSONToken != null)
            {

                JSONArray = JSONToken as JArray;

                if (JSONArray == null)
                {
                    ErrorResponse = "The given property '" + PropertyName + "' is not a valid JSON array!";
                    return false;
                }

                ErrorResponse = null;
                return true;

            }

            ErrorResponse = null;
            JSONArray     = null;
            return true;

        }

        #endregion


        #region GetOptional(this JSON, Key)

        public static String GetOptional(this JObject  JSON,
                                         String        Key)
        {

            if (JSON == null)
                return String.Empty;

            if (JSON.TryGetValue(Key, out JToken JSONToken))
                return JSONToken.Value<String>();

            return String.Empty;

        }

        #endregion

        #region GetOptional(this JSON, Key, out Values)

        public static Boolean GetOptional(this JObject             JSON,
                                          String                   Key,
                                          out IEnumerable<String>  Values)
        {

            if (JSON == null)
            {
                Values = new String[0];
                return false;
            }

            if (JSON.TryGetValue(Key, out JToken JSONToken))
            {

                if (JSONToken is JArray _Values)
                {

                    try
                    {

                        Values = _Values.AsEnumerable().
                                         Select(jtoken => jtoken.Value<String>()).
                                         Where(value => value != null);

                        return true;

                    }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                    catch (Exception)
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
                    { }

                }

                Values = null;
                return false;

            }

            Values = null;
            return true;

        }

        #endregion


    }

}
