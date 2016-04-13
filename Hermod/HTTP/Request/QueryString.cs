/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Web;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.Specialized;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A chainable query string helper class.
    /// Example usage :
    /// string strQuery = QueryString.Current.Add("id", "179").ToString();
    /// string strQuery = new QueryString().Add("id", "179").ToString();
    /// </summary>
    public class QueryString : IEnumerable<KeyValuePair<String, IEnumerable<String>>>
    {

        #region Data

        private Dictionary<String, List<String>> _Dictionary;

        #endregion

        #region Constructor(s)

        #region QueryString()

        /// <summary>
        /// Create a new HTTP QueryString.
        /// </summary>
        public QueryString()
        {
            _Dictionary = new Dictionary<String, List<String>>();
        }

        #endregion

        #region QueryString(QueryString)

        /// <summary>
        /// Parse the given string repesentation of a HTTP QueryString.
        /// </summary>
        public QueryString(String QueryString)
            : this()
        {

            #region Initial checks

            if (String.IsNullOrEmpty(QueryString))
                throw new ArgumentNullException("The given QueryString must not be null or its length zero!");

            var position = QueryString.IndexOf("?");
            if (position >= 0)
            {

                QueryString = QueryString.Remove(0, position + 1);

                if (String.IsNullOrEmpty(QueryString))
                    throw new ArgumentNullException("The given QueryString must not be null or its length zero!");

            }

            #endregion

            var a = new Char[] { '&' };
            var b = new Char[] { '=' };

            foreach (var keyValuePair in QueryString.Split(a, StringSplitOptions.RemoveEmptyEntries))
            {

                var split = keyValuePair.Split(b, StringSplitOptions.RemoveEmptyEntries);

                if (split.Length == 2)
                    Add(HttpUtility.UrlDecode(split[0]),
                        HttpUtility.UrlDecode(split[1]));

            }

        }

        #endregion

        #endregion


        #region Add(Key, Value)

        /// <summary>
        /// Add KeyValuePair to the QueryString.
        /// </summary>
        /// <param name="Key">The key.</param>
        /// <param name="Value">The value(s) associated with the key.</param>
        public QueryString Add(String Key, String Value)
        {

            #region Initial checks

            if (String.IsNullOrEmpty(Key))
                throw new ArgumentNullException("The key must not be null or empty!");

            if (String.IsNullOrEmpty(Value))
                throw new ArgumentNullException("The value must not be null or empty!");

            #endregion

            List<String> ValueList = null;

            if (_Dictionary.TryGetValue(Key, out ValueList))
                ValueList.Add(Value);
            else
                _Dictionary.Add(Key, new List<String>() { Value });

            return this;

        }

        #endregion

        #region Remove(Key)

        /// <summary>
        /// Remove a KeyValuesPair.
        /// </summary>
        /// <param name="Key">The key.</param>
        public new QueryString Remove(String Key)
        {
            _Dictionary.Remove(Key);
            return this;
        }

        #endregion

        #region Remove(Key, Value)

        /// <summary>
        /// Remove a KeyValuePair.
        /// </summary>
        /// <param name="Key">The key.</param>
        /// <param name="Value">The value.</param>
        public QueryString Remove(String Key, String Value)
        {

            List<String> ValueList = null;

            if (_Dictionary.TryGetValue(Key, out ValueList))
                ValueList.Remove(Value);

            return this;

        }

        #endregion

        public String GetString(String Parameter)
        {

            List<String> Value = null;

            if (_Dictionary.TryGetValue(Parameter, out Value))
                return Value.LastOrDefault();

            return null;

        }

        public String GetStringOrDefault(String Parameter, String DefaultValue)
        {

            List<String> Value = null;

            if (_Dictionary.TryGetValue(Parameter, out Value))
                if (Value != null && Value.Count > 0)
                    return Value.Last();

            return DefaultValue;

        }

        public IEnumerable<String> GetStrings(String Parameter, Boolean ToLowerCase = false)
        {

            List<String>         Value   = null;
            Func<String, String> ToLower = null;

            if (ToLowerCase)
                ToLower = s => s.ToLower();
            else
                ToLower = s => s;

            if (_Dictionary.TryGetValue(Parameter, out Value))
                if (Value != null && Value.Count > 0)
                    return Value.SelectMany(item => item.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).
                                                         Select(s => ToLower(s)));

            return new List<String>();

        }

        public Int32? GetInt32(String Parameter)
        {

            List<String>  Values;
            Int32         Int;

            if (_Dictionary.TryGetValue(Parameter, out Values))
                if (Int32.TryParse(Values.LastOrDefault(), out Int))
                    return Int;

            return null;

        }

        public UInt32? GetUInt32(String Parameter)
        {

            List<String>  Values;
            UInt32        Int;

            if (_Dictionary.TryGetValue(Parameter, out Values))
                if (UInt32.TryParse(Values.LastOrDefault(), out Int))
                    return Int;

            return null;

        }

        public UInt32 GetUInt32OrDefault(String  Parameter,
                                         UInt32  DefaultValue = 0)
        {

            List<String> Values;
            UInt32 Int;

            if (_Dictionary.TryGetValue(Parameter, out Values))
                if (UInt32.TryParse(Values.LastOrDefault(), out Int))
                    return Int;

            return DefaultValue;

        }



        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {

            if (_Dictionary.Count == 0)
                return String.Empty;

            var _StringBuilder = new StringBuilder();

            foreach (var KeyValuePair in _Dictionary)
                foreach (var Value in KeyValuePair.Value)
                    _StringBuilder.Append("&").
                                   Append(HttpUtility.UrlEncodeUnicode(KeyValuePair.Key)).
                                   Append("=").
                                   Append(HttpUtility.UrlEncodeUnicode(Value));

            return '?' + _StringBuilder.Remove(0, 1).ToString();

        }

        #endregion


        public IEnumerator<KeyValuePair<String, IEnumerable<String>>> GetEnumerator()
        {

            return _Dictionary.
                       Select(v => new KeyValuePair<String, IEnumerable<String>>(v.Key, v.Value)).
                       GetEnumerator();

        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {

            return _Dictionary.
                       Select(v => new KeyValuePair<String, IEnumerable<String>>(v.Key, v.Value)).
                       GetEnumerator();

        }

    }

}


