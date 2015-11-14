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
using System.Text;

using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// JSON helpers.
    /// </summary>    
    public static class JSONExtentions
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

    }

}
