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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Illias.ConsoleLog;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class Ext
    {

        public static String Repeat(this String Text, Int64 Times)
        {
            return Text.Repeat((UInt64)Math.Max(0L, Times));
        }

        public static String Repeat(this String Text, UInt32 Times)
        {
            return Text.Repeat((UInt64)Times);
        }

        public static String Repeat(this String Text, Int32 Times)
        {
            return Text.Repeat((UInt64)Math.Max(0, Times));
        }

        public static String Repeat(this String Text, UInt64 Times)
        {

            var result = "";

            for (var i = 0u; i < Times; i++)
                result += Text;

            return result;

        }

    }

    public static class NewLine
    {

        public static String Concat(params String[] Lines)
        {
            return Lines.Aggregate((a, b) => a + Environment.NewLine + b);
        }

    }


    public static class Parsy
    {

        public static HTTPResponse CreateBadRequest(String Context, String ParameterName)
        {

            return new HTTPResponseBuilder() {
                HTTPStatusCode  = HTTPStatusCode.BadRequest,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(new JProperty("@context",    Context),
                                              new JProperty("description", "Missing \"" + ParameterName + "\" JSON property!")).ToString().ToUTF8Bytes()
            };

        }

        public static HTTPResponse CreateBadRequest(String Context, String ParameterName, String Value)
        {

            return new HTTPResponseBuilder() {
                HTTPStatusCode  = HTTPStatusCode.BadRequest,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(new JProperty("@context",    Context),
                                              new JProperty("value",       Value),
                                              new JProperty("description", "Invalid \"" + ParameterName + "\" property value!")).ToString().ToUTF8Bytes()
            };

        }

        public static HTTPResponse CreateNotFound(String Context, String ParameterName, String Value)
        {

            return new HTTPResponseBuilder() {
                HTTPStatusCode  = HTTPStatusCode.NotFound,
                ContentType     = HTTPContentType.JSON_UTF8,
                Content         = new JObject(new JProperty("@context",    Context),
                                              new JProperty("value",       Value),
                                              new JProperty("description", "Unknown \"" + ParameterName + "\" property value!")).ToString().ToUTF8Bytes()
            };

        }

        public static Boolean ParseHTTP(this JObject JSONRequest, String ParameterName, out Double Value, out HTTPResponse HTTPResp, String Context = null, Double DefaultValue = 0)
        {

            JToken JSONToken;

            if (!JSONRequest.TryGetValue(ParameterName, out JSONToken))
            {

                Log.Timestamp("Bad request: Missing \"" + ParameterName + "\" JSON property!");

                Value     = DefaultValue;
                HTTPResp  = CreateBadRequest(Context, ParameterName);

                return false;

            }

            if (!Double.TryParse(JSONToken.Value<String>(), NumberStyles.Any, CultureInfo.CreateSpecificCulture("en-US"), out Value))
            {

                Log.Timestamp("Bad request: Invalid \"" + ParameterName + "\" property value!");

                Value     = DefaultValue;
                HTTPResp  = CreateBadRequest(Context, ParameterName, JSONToken.Value<String>());

                return false;

            }

            HTTPResp = null;
            return true;

        }

        public static Boolean ParseHTTP(this JObject JSONRequest, String ParameterName, out DateTime Value, out HTTPResponse HTTPResp, String Context = null, DateTime DefaultValue = default(DateTime))
        {

            JToken JSONToken;

            if (!JSONRequest.TryGetValue(ParameterName, out JSONToken))
            {

                Log.Timestamp("Bad request: Missing \"" + ParameterName + "\" JSON property!");

                Value     = DefaultValue;
                HTTPResp  = CreateBadRequest(Context, ParameterName);

                return false;

            }

            try
            {
                Value = JSONToken.Value<DateTime>();
            }
            catch (Exception)
            {

                Log.Timestamp("Bad request: Invalid \"" + ParameterName + "\" property value!");

                Value     = DefaultValue;
                HTTPResp  = CreateBadRequest(Context, ParameterName, JSONToken.Value<String>());

                return false;

            }

            HTTPResp = null;
            return true;

        }

        public static Boolean ParseHTTP(this JObject JSONRequest, String ParameterName, out String Value, out HTTPResponse HTTPResp, String Context = null, String DefaultValue = null)
        {

            JToken JSONToken;

            if (!JSONRequest.TryGetValue(ParameterName, out JSONToken))
            {

                Log.Timestamp("Bad request: Missing \"" + ParameterName + "\" JSON property!");

                Value     = DefaultValue;
                HTTPResp  = CreateBadRequest(Context, ParameterName);

                return false;

            }

            Value = JSONToken.Value<String>();
            if (Value.IsNullOrEmpty())
            {

                Log.Timestamp("Bad request: Invalid \"" + ParameterName + "\" property value!");

                Value     = DefaultValue;
                HTTPResp  = CreateBadRequest(Context, ParameterName, JSONToken.Value<String>());

                return false;

            }

            HTTPResp = null;
            return true;

        }

    }

}
