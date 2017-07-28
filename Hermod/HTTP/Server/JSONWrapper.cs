/*
 * Copyright (c) 2010-2017, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

        public static class JSONExt
    {

        public static Boolean ParseMandatory(this JObject  JSONIn,
                                             String        PropertyName,
                                             out JObject   JSONOut)
        {

            if (JSONIn == null ||
                PropertyName.IsNullOrEmpty())
            {
                JSONOut = null;
                return false;
            }

            JToken _JToken;

            if (JSONIn.TryGetValue(PropertyName, out _JToken) && _JToken is JObject)
            {
                JSONOut = _JToken as JObject;
                return true;
            }

            JSONOut = null;
            return false;

        }

        public static Boolean ParseMandatory(this JObject  JSONIn,
                                             String        PropertyName,
                                             out String    StringOut)
        {

            if (JSONIn == null ||
                PropertyName.IsNullOrEmpty())
            {
                StringOut = String.Empty;
                return false;
            }

            JToken _JToken;

            if (JSONIn.TryGetValue(PropertyName, out _JToken) && _JToken?.Value<String>() != null)
            {
                StringOut = _JToken?.Value<String>();
                return true;
            }

            StringOut = null;
            return false;

        }





        public static Boolean ParseMandatory<T>(this JObject     JSONIn,
                                                String           PropertyName,
                                                Func<String, T>  Mapper,
                                                out T            TOut)
        {

            if (JSONIn == null ||
                PropertyName.IsNullOrEmpty() ||
                Mapper == null)
            {
                TOut = default(T);
                return false;
            }

            JToken _JToken;

            if (JSONIn.TryGetValue(PropertyName, out _JToken) && _JToken?.Value<String>() != null)
            {

                try
                {
                    TOut = Mapper(_JToken?.Value<String>());
                    return true;
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

        public static Boolean ParseMandatory<T>(this JObject      JSONIn,
                                                String            PropertyName,
                                                Func<String, T>   Mapper,
                                                T                 InvalidResult,
                                                out T             TOut)
        {

            if (JSONIn == null ||
                PropertyName.IsNullOrEmpty() ||
                Mapper == null)
            {
                TOut = default(T);
                return false;
            }

            JToken _JToken;

            if (JSONIn.TryGetValue(PropertyName, out _JToken) && _JToken?.Value<String>() != null)
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







        public static Boolean ParseOptional(this JObject  JSONIn,
                                            String        PropertyName,
                                            out String    StringOut)
        {

            if (JSONIn == null ||
                PropertyName.IsNullOrEmpty())
            {
                StringOut = String.Empty;
                return false;
            }

            JToken _JToken;

            if (JSONIn.TryGetValue(PropertyName, out _JToken))
            {

                StringOut = _JToken?.Value<String>();

                if (StringOut != null)
                    return true;

                return false;

            }

            StringOut = null;
            return true;

        }


        public static Boolean ParseOptional<T>(this JObject     JSONIn,
                                               String           PropertyName,
                                               Func<String, T>  Mapper,
                                               out T            TOut)
        {

            if (JSONIn == null ||
                PropertyName.IsNullOrEmpty() ||
                Mapper == null)
            {
                TOut = default(T);
                return false;
            }

            JToken _JToken;

            if (JSONIn.TryGetValue(PropertyName, out _JToken))
            {

                var StringOut = _JToken?.Value<String>();

                if (StringOut != null)
                {

                    try
                    {
                        TOut = Mapper(StringOut);
                        return true;
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

            TOut = default(T);
            return true;

        }

    }




    /// <summary>
    /// A JSON object (wrapper).
    /// </summary>
    public class JSONWrapper
    {

        #region Data

        private readonly IDictionary<String, Object> Internal;

        #endregion

        #region Properties

        #region HasProperties

        /// <summary>
        /// The JSOB Object has 
        /// </summary>
        public Boolean HasProperties
        {
            get
            {
                return Internal.Any();
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region JSONWrapper()

        /// <summary>
        /// Create an empty JSON object.
        /// </summary>
        public JSONWrapper()
        {

            Internal = new Dictionary<String, Object>(StringComparer.CurrentCultureIgnoreCase);

        }

        #endregion

        #region JSONWrapper(Text)

        /// <summary>
        /// Parse the given text as JSON object.
        /// </summary>
        /// <param name="Text">A text.</param>
        public JSONWrapper(String Text)
        {

            Internal = new Dictionary<String, Object>(
                           JObject.Parse(Text).ToObject<IDictionary<String, Object>>(),
                           StringComparer.CurrentCultureIgnoreCase);

        }

        #endregion

        #region JSONWrapper(JSON)

        /// <summary>
        /// Create the JSON based on the given JObject.
        /// </summary>
        /// <param name="JSON">A JSON object.</param>
        public JSONWrapper(JObject JSON)
        {

            Internal = new Dictionary<String, Object>(
                           (JSON as JObject).ToObject<IDictionary<String, Object>>(),
                           StringComparer.CurrentCultureIgnoreCase);

        }

        #endregion

        #endregion


        public Object this[String Key]

            => Internal[Key];


        public Boolean TryGetValue(String Key, out Object Object)

            => Internal.TryGetValue(Key, out Object);


        public Boolean TryGetString(String Key, out String Text)
        {

            Object Value = null;

            if (TryGetValue(Key, out Value))
            {

                Text = Value as String;

                if (Text != null)
                    return true;

            }

            Text = null;
            return false;

        }


        public Boolean ContainsKey(String Key)

            => Internal.ContainsKey(Key);


        public String GetString(String Key)
        {

            Object Value = null;

            if (TryGetValue(Key, out Value))
            {

                var ReturnValue = Value as String;

                if (ReturnValue != null)
                    return ReturnValue;

                return Value.ToString();

            }

            return String.Empty;

        }


        public JSONWrapper SetProperty(String Key, Object Value)
        {

            if (!Internal.ContainsKey(Key))
                Internal.Add(Key, Value);

            else
                Internal[Key] = Value;

            return this;

        }


        public JSONWrapper AsJSONObject(String Key)
        {

            var JSON = Internal[Key] as JObject;

            if (JSON != null)
                return new JSONWrapper(JSON);

            else
                return null;

        }

        public Boolean AsJSONObject(String Key, out JSONWrapper JSONWrapper)
        {

            Object Value = null;

            if (TryGetValue(Key, out Value))
            {

                var JSON = Value as JObject;

                if (JSON != null)
                    JSONWrapper = new JSONWrapper(JSON);
                else
                    JSONWrapper = null;

                return true;

            }

            JSONWrapper = null;
            return false;

        }


        #region ParseMandatory<T>    (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public Boolean ParseMandatory<T>(String            PropertyName,
                                         String            PropertyDescription,
                                         String            DefaultServerName,
                                         PFunc<T>          Parser,
                                         out T             Value,
                                         HTTPRequest       HTTPRequest,
                                         out HTTPResponse  HTTPResponse)
        {

            Object JSONToken = null;

            if (!TryGetValue(PropertyName, out JSONToken))
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

            if (JSONToken != null)
            {
                if (!Parser(JSONToken.ToString(), out Value))
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

                HTTPResponse = null;
                return true;

            }

            Value = default(T);
            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseMandatory<TEnum>(this JSON, PropertyName, PropertyDescription, DefaultServerName,         out Value, HTTPRequest, out HTTPResponse)

        public Boolean ParseMandatory<TEnum>(String            PropertyName,
                                             String            PropertyDescription,
                                             String            DefaultServerName,
                                             out TEnum         Value,
                                             HTTPRequest       HTTPRequest,
                                             out HTTPResponse  HTTPResponse)

             where TEnum : struct

        {

            Object JSONToken = null;

            if (!TryGetValue(PropertyName, out JSONToken))
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

            if (JSONToken != null)
            {
                if (!Enum.TryParse<TEnum>(JSONToken.ToString(), true, out Value))
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
            }

            Value = default(TEnum);
            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseMandatory(this JSON, PropertyName,  PropertyDescription, DefaultServerName, out JSON,      HTTPRequest, out HTTPResponse)

        public Boolean ParseMandatory(String            PropertyName,
                                      String            PropertyDescription,
                                      String            DefaultServerName,
                                      out JToken        JSON,
                                      HTTPRequest       HTTPRequest,
                                      out HTTPResponse  HTTPResponse)

        {

            Object JSONToken = null;

            if (PropertyName.IsNotNullOrEmpty())
            {
                if (!TryGetValue(PropertyName, out JSONToken))
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

                    JSON = new JValue(false);

                    return false;

                }
            }


            JSON = null;

            switch (JSONToken.GetType().Name)
            {

                case "String": JSON = new JValue(JSONToken);
                    break;

                case "JObject":
                    JSON = JObject.Parse(JSONToken.ToString());
                    break;

                case "JArray":
                    JSON = JArray.Parse(JSONToken.ToString());
                    break;

            }


           // JSON          = JToken.Parse(JSONToken.ToString());
            HTTPResponse  = null;

            return true;

        }

        #endregion

        #region ParseMandatory(this JSON, PropertyName,  PropertyDescription, DefaultServerName, out Text,      HTTPRequest, out HTTPResponse)

        public Boolean ParseMandatory(String            PropertyName,
                                      String            PropertyDescription,
                                      String            DefaultServerName,
                                      out String        Text,
                                      HTTPRequest       HTTPRequest,
                                      out HTTPResponse  HTTPResponse)

        {

            Object JSONToken = null;

            if (PropertyName.IsNotNullOrEmpty())
            {
                if (!TryGetValue(PropertyName, out JSONToken))
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
            }

            Text          = JSONToken.ToString();
            HTTPResponse  = null;

            return true;

        }

        #endregion

        #region ParseMandatory(this JSON, PropertyNames, PropertyDescription, DefaultServerName, out Text,      HTTPRequest, out HTTPResponse)

        public Boolean ParseMandatory(IEnumerable<String>  PropertyNames,
                                      String               PropertyDescription,
                                      String               DefaultServerName,
                                      out String           Text,
                                      HTTPRequest          HTTPRequest,
                                      out HTTPResponse     HTTPResponse)

        {

            Object JSONToken = null;

            // Attention: JSONToken is a side-effect!
            var FirstMatchingPropertyName = PropertyNames.
                                                Where(propertyname => TryGetValue(propertyname, out JSONToken)).
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

            if (JSONToken != null)
                Text = JSONToken.ToString();

            else
                Text = String.Empty;

            HTTPResponse  = null;

            return true;

        }

        #endregion

        #region ParseMandatory(this JSON, PropertyName,  PropertyDescription, DefaultServerName, out Timestamp, HTTPRequest, out HTTPResponse)

        public Boolean ParseMandatory(String            PropertyName,
                                      String            PropertyDescription,
                                      String            DefaultServerName,
                                      out DateTime      Timestamp,
                                      HTTPRequest       HTTPRequest,
                                      out HTTPResponse  HTTPResponse)

        {

            Object JSONToken = null;
            Timestamp = DateTime.MinValue;

            if (TryGetValue(PropertyName, out JSONToken))
            {

                if (JSONToken != null)
                {

                    try
                    {

                        Timestamp = (DateTime) JSONToken;

                    }
                    catch (Exception)
                    {

                        HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                            HTTPStatusCode  = HTTPStatusCode.BadRequest,
                            Server          = DefaultServerName,
                            Date            = DateTime.Now,
                            ContentType     = HTTPContentType.JSON_UTF8,
                            Content         = JSONObject.Create(
                                                  new JProperty("description", "Invalid timestamp '" + JSONToken.ToString() + "'!")
                                              ).ToUTF8Bytes()
                        };

                        return false;

                    }

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

        public Boolean ParseMandatory(IEnumerable<String>  PropertyNames,
                                      String               PropertyDescription,
                                      String               DefaultServerName,
                                      out DateTime         Timestamp,
                                      HTTPRequest          HTTPRequest,
                                      out HTTPResponse     HTTPResponse)

        {

            Object JSONToken = null;
            Timestamp = DateTime.MinValue;

            // Attention: JSONToken is a side-effect!
            var FirstMatchingPropertyName = PropertyNames.
                                                Where(propertyname => TryGetValue(propertyname, out JSONToken)).
                                                FirstOrDefault();

            if (FirstMatchingPropertyName != null)
            {

                if (JSONToken != null)
                {

                    try
                    {

                        Timestamp = (DateTime) JSONToken;

                    }
                    catch (Exception)
                    {

                        HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                            HTTPStatusCode  = HTTPStatusCode.BadRequest,
                            Server          = DefaultServerName,
                            Date            = DateTime.Now,
                            ContentType     = HTTPContentType.JSON_UTF8,
                            Content         = JSONObject.Create(
                                                  new JProperty("description", "Invalid timestamp '" + JSONToken.ToString() + "'!")
                                              ).ToUTF8Bytes()
                        };

                        return false;

                    }

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

        public Boolean ParseOptional<T>(String            PropertyName,
                                        String            PropertyDescription,
                                        String            DefaultServerName,
                                        PFunc<T>          Parser,
                                        out T             Value,
                                        HTTPRequest       HTTPRequest,
                                        out HTTPResponse  HTTPResponse)
        {

            Object JSONToken = null;
            Value = default(T);

            if (TryGetValue(PropertyName, out JSONToken))
            {

                if (JSONToken != null)
                {

                    var JSONValue = JSONToken.ToString();

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

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptionalN<T?>  (this JSON, PropertyName, PropertyDescription, DefaultServerName, Parser, out Value, HTTPRequest, out HTTPResponse)

        public Boolean ParseOptionalN<T>(String            PropertyName,
                                         String            PropertyDescription,
                                         String            DefaultServerName,
                                         PFunc<T>          Parser,
                                         out T?            Value,
                                         HTTPRequest       HTTPRequest,
                                         out HTTPResponse  HTTPResponse)

            where T : struct

        {

            Object JSONToken = null;
            Value = new T?();

            if (TryGetValue(PropertyName, out JSONToken))
            {

                if (JSONToken != null)
                {

                    var JSONValue = JSONToken.ToString();

                    T _Value = default(T);

                    if (JSONValue != null && !Parser(JSONValue, out _Value))
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

                        Value = new T?();
                        return false;

                    }

                    Value = new T?(_Value);

                }

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional<TEnum>(this JSON, PropertyName, PropertyDescription, DefaultServerName,         out Value, HTTPRequest, out HTTPResponse)

        public Boolean ParseOptional<TEnum>(String            PropertyName,
                                            String            PropertyDescription,
                                            String            DefaultServerName,
                                            out TEnum         Value,
                                            HTTPRequest       HTTPRequest,
                                            out HTTPResponse  HTTPResponse)

            where TEnum : struct

        {

            Object JSONToken = null;
            Value = default(TEnum);

            if (TryGetValue(PropertyName, out JSONToken))
            {

                if (JSONToken != null)
                {

                    var JSONValue = JSONToken.ToString();

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

            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional(this JSON, PropertyName,  PropertyDescription, DefaultServerName, out Text,      HTTPRequest, out HTTPResponse)

        public Boolean ParseOptional(String            PropertyName,
                                     String            DefaultServerName,
                                     out JObject       JSON,
                                     HTTPRequest       HTTPRequest,
                                     out HTTPResponse  HTTPResponse)

        {

            Object JSONToken = null;

            if (TryGetValue(PropertyName, out JSONToken))
            {

                JSON = JSONToken as JObject;

                if (JSON == null)
                {

                    HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                        Server          = DefaultServerName,
                        Date            = DateTime.Now,
                        ContentType     = HTTPContentType.JSON_UTF8,
                        Content         = JSONObject.Create(
                                              new JProperty("description", "JSON object expected!")
                                          ).ToUTF8Bytes()
                    };

                    return false;

                }

                HTTPResponse = null;
                return true;

            }

            JSON          = null;
            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional(this JSON, PropertyName,  PropertyDescription, DefaultServerName, out Text,      HTTPRequest, out HTTPResponse)

        public Boolean ParseOptional(String            PropertyName,
                                     String            PropertyDescription,
                                     String            DefaultServerName,
                                     out String        Text,
                                     HTTPRequest       HTTPRequest,
                                     out HTTPResponse  HTTPResponse)

        {

            Object JSONToken = null;
            Text = String.Empty;

            if (TryGetValue(PropertyName, out JSONToken))
            {
                if (JSONToken != null)
                {
                    Text = JSONToken.ToString();
                }
            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional(this JSON, PropertyNames, PropertyDescription, DefaultServerName, out Text,      HTTPRequest, out HTTPResponse)

        public Boolean ParseOptional(IEnumerable<String>  PropertyNames,
                                     String               PropertyDescription,
                                     String               DefaultServerName,
                                     out String           Text,
                                     HTTPRequest          HTTPRequest,
                                     out HTTPResponse     HTTPResponse)

        {

            Object JSONToken = null;

            // Attention: JSONToken is a side-effect!
            var FirstMatchingPropertyName = PropertyNames.FirstOrDefault(propertyname => TryGetValue(propertyname, out JSONToken));

            if (FirstMatchingPropertyName != null)
            {
                if (JSONToken != null)
                {
                    Text = JSONToken.ToString();
                }
            }

            Text          = null;
            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional(this JSON, PropertyName,  PropertyDescription, DefaultServerName, out Timestamp, HTTPRequest, out HTTPResponse)

        public Boolean ParseOptional(String            PropertyName,
                                     String            PropertyDescription,
                                     String            DefaultServerName,
                                     out DateTime?     Timestamp,
                                     HTTPRequest       HTTPRequest,
                                     out HTTPResponse  HTTPResponse)

        {

            Object JSONToken = null;
            Timestamp = new DateTime?();

            if (TryGetValue(PropertyName, out JSONToken))
            {
                if (JSONToken != null)
                {

                    try
                    {

                        Timestamp = (DateTime) JSONToken;

                    }
                    catch (Exception)
                    {

                        HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                            HTTPStatusCode  = HTTPStatusCode.BadRequest,
                            Server          = DefaultServerName,
                            Date            = DateTime.Now,
                            ContentType     = HTTPContentType.JSON_UTF8,
                            Content         = JSONObject.Create(
                                                  new JProperty("description",  "Invalid timestamp '" + JSONToken + "'!")
                                              ).ToUTF8Bytes()
                        };

                        return false;

                    }

                }
            }

            HTTPResponse  = null;
            return true;

        }

        #endregion

        #region ParseOptional(this JSON, PropertyNames, PropertyDescription, DefaultServerName, out Timestamp, HTTPRequest, out HTTPResponse)

        public Boolean ParseOptional(IEnumerable<String>  PropertyNames,
                                     String               PropertyDescription,
                                     String               DefaultServerName,
                                     out DateTime?        Timestamp,
                                     HTTPRequest          HTTPRequest,
                                     out HTTPResponse     HTTPResponse)

        {

            Object JSONToken = null;
            Timestamp = DateTime.MinValue;

            // Attention: JSONToken is a side-effect!
            var FirstMatchingPropertyName = PropertyNames.
                                                Where(propertyname => TryGetValue(propertyname, out JSONToken)).
                                                FirstOrDefault();

            if (FirstMatchingPropertyName != null)
            {

                if (JSONToken != null)
                {

                    try
                    {

                        Timestamp = (DateTime) JSONToken;

                    }
                    catch (Exception)
                    {

                        HTTPResponse = new HTTPResponseBuilder(HTTPRequest) {
                            HTTPStatusCode  = HTTPStatusCode.BadRequest,
                            Server          = DefaultServerName,
                            Date            = DateTime.Now,
                            ContentType     = HTTPContentType.JSON_UTF8,
                            Content         = JSONObject.Create(
                                                  new JProperty("description",  "Invalid timestamp '" + JSONToken + "'!")
                                              ).ToUTF8Bytes()
                        };

                        return false;

                    }

                }
            }

            HTTPResponse  = null;
            return true;

        }

        #endregion




        public Boolean ParseHTTP(String ParameterName, HTTPRequest HTTPRequest, out Single Value, out HTTPResponse HTTPResp, String Context = null, Single DefaultValue = 0)
        {

            Object JSONToken;

            if (!TryGetValue(ParameterName, out JSONToken))
            {

                Value     = DefaultValue;
                HTTPResp  = HTTPExtentions.CreateBadRequest(HTTPRequest, Context, ParameterName);

                return false;

            }

            if (JSONToken != null)
            {
                if (!Single.TryParse(JSONToken.ToString(), NumberStyles.Any, CultureInfo.CreateSpecificCulture("en-US"), out Value))
                {

                    Log.Timestamp("Bad request: Invalid \"" + ParameterName + "\" property value!");

                    Value = DefaultValue;
                    HTTPResp = HTTPExtentions.CreateBadRequest(HTTPRequest, Context, ParameterName, JSONToken.ToString());

                    return false;

                }
            }

            else
            {
                Value    = 0;
                HTTPResp = null;
                return false;
            }

            HTTPResp = null;
            return true;

        }

        public Boolean ParseHTTP(String ParameterName, HTTPRequest HTTPRequest, out Double Value, out HTTPResponse HTTPResp, String Context = null, Double DefaultValue = 0)
        {

            Object JSONToken;

            if (!TryGetValue(ParameterName, out JSONToken))
            {

                Value     = DefaultValue;
                HTTPResp  = HTTPExtentions.CreateBadRequest(HTTPRequest, Context, ParameterName);

                return false;

            }

            if (JSONToken != null)
            {
                if (!Double.TryParse(JSONToken.ToString(), NumberStyles.Any, CultureInfo.CreateSpecificCulture("en-US"), out Value))
                {

                    Log.Timestamp("Bad request: Invalid \"" + ParameterName + "\" property value!");

                    Value = DefaultValue;
                    HTTPResp = HTTPExtentions.CreateBadRequest(HTTPRequest, Context, ParameterName, JSONToken.ToString());

                    return false;

                }
            }

            else
            {
                Value    = 0;
                HTTPResp = null;
                return false;
            }

            HTTPResp = null;
            return true;

        }

        public Boolean ParseHTTP(String ParameterName, HTTPRequest HTTPRequest, out DateTime Value, out HTTPResponse HTTPResp, String Context = null, DateTime DefaultValue = default(DateTime))
        {

            Object JSONToken;

            if (!TryGetValue(ParameterName, out JSONToken))
            {

                Value     = DefaultValue;
                HTTPResp  = HTTPExtentions.CreateBadRequest(HTTPRequest, Context, ParameterName);

                return false;

            }

            if (JSONToken != null)
            {
                try
                {
                    Value = (DateTime)JSONToken;
                }
                catch (Exception)
                {

                    Log.Timestamp("Bad request: Invalid \"" + ParameterName + "\" property value!");

                    Value = DefaultValue;
                    HTTPResp = HTTPExtentions.CreateBadRequest(HTTPRequest, Context, ParameterName, JSONToken.ToString());

                    return false;

                }
            }

            else
            {
                Value = DateTime.Now;
                HTTPResp = null;
                return false;
            }

            HTTPResp = null;
            return true;

        }

        public Boolean ParseHTTP( String ParameterName, HTTPRequest HTTPRequest, out String Value, out HTTPResponse HTTPResp, String Context = null, String DefaultValue = null)
        {

            Object JSONToken;

            if (!TryGetValue(ParameterName, out JSONToken))
            {

                Value     = DefaultValue;
                HTTPResp  = HTTPExtentions.CreateBadRequest(HTTPRequest, Context, ParameterName);

                return false;

            }

            if (JSONToken != null)
            {

                Value = JSONToken.ToString();

                if (Value.IsNullOrEmpty())
                {

                    Value = DefaultValue;
                    HTTPResp = HTTPExtentions.CreateBadRequest(HTTPRequest, Context, ParameterName, JSONToken.ToString());

                    return false;

                }

            }

            else
            {
                Value = DefaultValue;
                HTTPResp = null;
                return false;
            }

            HTTPResp = null;
            return true;

        }


    }

}
