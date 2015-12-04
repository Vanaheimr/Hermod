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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public static class JSONObject
    {

        #region Create(params Properties)

        public static JObject Create(params JProperty[] Properties)
        {

            var FilteredData = Properties.Where(p => p != null).ToArray();

            return (FilteredData.Length > 0)
                       ? new JObject(FilteredData)
                       : new JObject();

        }

        #endregion

    }

    public static class JSON
    {

        #region ToUTF8Bytes(this JSONArray)

        public static Byte[] ToUTF8Bytes(this JArray JSONArray)
        {

            if (JSONArray == null)
                return new Byte[0];

            return Encoding.UTF8.GetBytes(JSONArray.ToString());

        }

        #endregion

        #region ToUTF8Bytes(this JSONObject)

        public static Byte[] ToUTF8Bytes(this JObject JSONObject)
        {

            if (JSONObject == null)
                return new Byte[0];

            return Encoding.UTF8.GetBytes(JSONObject.ToString());

        }

        #endregion


        #region ToJSON(this Timestamp, JPropertyKey)

        /// <summary>
        /// Create a Iso8601 representation of the given DateTime.
        /// </summary>
        /// <param name="Timestamp">A timestamp.</param>
        /// <param name="JPropertyKey">The name of the JSON property key.</param>
        public static JProperty ToJSON(this DateTime Timestamp, String JPropertyKey)
        {
            return new JProperty(JPropertyKey, Timestamp.ToIso8601());
        }

        #endregion

        #region ToJSON(this I18N)

        /// <summary>
        /// Create a JSON representation of the given internationalized string.
        /// </summary>
        /// <param name="I18N">An internationalized string.</param>
        public static JObject ToJSON(this I18NString I18N)
        {
            return new JObject(I18N.SafeSelect(i18n => new JProperty(i18n.Language.ToString(), i18n.Text)));
        }

        #endregion

        #region ToJSON(this I18N, JPropertyKey)

        /// <summary>
        /// Create a JSON representation of the given internationalized string.
        /// </summary>
        /// <param name="I18N">An internationalized string.</param>
        /// <param name="JPropertyKey">The name of the JSON property key.</param>
        public static JProperty ToJSON(this I18NString I18N, String JPropertyKey)
        {

            if (I18N.Any())
                return new JProperty(JPropertyKey, I18N.ToJSON());
            else
                return null;

        }

        #endregion

    }

}
