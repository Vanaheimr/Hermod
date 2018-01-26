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
using System.Web;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extention methods for the QueryString class.
    /// </summary>
    public static class QueryStringExtentions
    {

        #region CreateStringFilter(this QueryString, ParameterName, FilterDelegate)

        public static Func<T, Boolean> CreateStringFilter<T>(this QueryString          QueryString,
                                                             String                    ParameterName,
                                                             Func<T, String, Boolean>  FilterDelegate)
        {

            if (FilterDelegate != null &&
                QueryString.TryGetString(ParameterName, out String Value))
            {

                return item => Value.StartsWith("!", StringComparison.Ordinal)
                                   ? !FilterDelegate(item, Value.Substring(1))
                                   :  FilterDelegate(item, Value);

            }

            return item => true;

        }

        #endregion


        #region GetDateTimeOrDefault(this QueryString, ParameterName, DefaultValue = null)

        public static DateTime? GetDateTimeOrDefault(this QueryString  QueryString,
                                                     String            ParameterName,
                                                     DateTime?         DefaultValue  = null)
        {

            if (QueryString.TryGetString(ParameterName, out String Value) &&
                DateTime.TryParse(Value, out DateTime Timestamp))
            {
                return Timestamp;
            }

            return DefaultValue;

        }

        #endregion

        #region TryGetDateTime      (this QueryString, ParameterName, out Timestamp)

        public static Boolean TryGetDateTime(this QueryString  QueryString,
                                             String            ParameterName,
                                             out DateTime      Timestamp)
        {

            if (QueryString.TryGetString(ParameterName, out String Value) &&
                DateTime.TryParse(Value, out Timestamp))
            {
                return true;
            }

            Timestamp = default(DateTime);
            return false;

        }

        #endregion

        #region CreateDateTimeFilter(this QueryString, ParameterName, FilterDelegate)

        public static Func<T, Boolean> CreateDateTimeFilter<T>(this QueryString            QueryString,
                                                               String                      ParameterName,
                                                               Func<T, DateTime, Boolean>  FilterDelegate)
        {

            if (FilterDelegate != null &&
                QueryString.TryGetDateTime(ParameterName, out DateTime Timestamp))
            {
                return item => FilterDelegate(item, Timestamp);
            }

            return item => true;

        }

        #endregion


        #region ParseFromTimestampFilter   (this QueryString)

        /// <summary>
        /// Parse optional from-timestamp filter...
        /// </summary>
        public static DateTime? ParseFromTimestampFilter(this QueryString  QueryString)
        {

            if (QueryString.TryGetDateTime("from", out DateTime Timestamp))
                return Timestamp;

            return null;

        }

        #endregion

        #region ParseToTimestampFilter     (this QueryString)

        /// <summary>
        /// Parse optional to-timestamp filter...
        /// </summary>
        public static DateTime? ParseToTimestampFilter(this QueryString  QueryString)
        {

            if (QueryString.TryGetDateTime("to", out DateTime Timestamp))
                return Timestamp;

            return null;

        }

        #endregion

        #region ParseFromToTimestampFilters(this QueryString, out FromTimestamp, out ToTimestamp)

        /// <summary>
        /// Parse optional from-/to-timestamp filters...
        /// </summary>
        public static void ParseFromToTimestampFilters(this QueryString  QueryString,
                                                       out  DateTime?    FromTimestamp,
                                                       out  DateTime?    ToTimestamp)
        {

            FromTimestamp = QueryString.ParseFromTimestampFilter();
              ToTimestamp = QueryString.ParseToTimestampFilter();

        }

        #endregion

    }

    /// <summary>
    /// A HTTP Query String.
    /// </summary>
    public class QueryString : IEnumerable<KeyValuePair<String, IEnumerable<String>>>
    {

        #region Data

        private readonly Dictionary<String, List<String>> _Dictionary;

        private static Char[] AndSign    = { '&' };
        private static Char[] EqualsSign = { '=' };

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new query string based on the optional given string repesentation.
        /// </summary>
        /// <param name="Text">An optional text to parse.</param>
        private QueryString(String Text = null)
        {

            _Dictionary = new Dictionary<String, List<String>>();

            if (Text.IsNotNullOrEmpty())
            {

                Text = Text.Trim();

                var position = Text.IndexOf("?", StringComparison.Ordinal);
                if (position >= 0)
                    Text = Text.Remove(0, position + 1);

                String[] split = null;

                if (Text.IsNotNullOrEmpty())
                    foreach (var keyValuePair in Text.Split(AndSign, StringSplitOptions.RemoveEmptyEntries))
                    {

                        split = keyValuePair.Split(EqualsSign, StringSplitOptions.RemoveEmptyEntries);

                        switch (split.Length)
                        {

                            case 1:
                                Add(HttpUtility.UrlDecode(split[0]), "");
                                break;

                            case 2:
                                Add(HttpUtility.UrlDecode(split[0]),
                                    HttpUtility.UrlDecode(split[1]));
                                break;

                        }

                    }

            }

        }

        #endregion


        #region (static) Empty

        /// <summary>
        /// Return an empty query string.
        /// </summary>
        public static QueryString Empty

            => new QueryString();

        #endregion

        #region (static) Parse(String Text)

        /// <summary>
        ///  Parse the given string repesentation of a HTTP Query String.
        /// </summary>
        /// <param name="Text">The text to parse.</param>
        public static QueryString Parse(String Text)

            => new QueryString(Text);

        #endregion


        #region Add(Key, Value)

        /// <summary>
        /// Add the given key value pair to the query string.
        /// </summary>
        /// <param name="Key">The key.</param>
        /// <param name="Value">The value associated with the key.</param>
        public QueryString Add(String  Key,
                               String  Value)
        {

            #region Initial checks

            if (Key.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Key),  "The given key must not be null or empty!");

            #endregion

            if (_Dictionary.TryGetValue(Key, out List<String> ValueList) && Value != null)
                ValueList.Add(Value);

            else
                _Dictionary.Add(Key, new List<String>() { Value });

            return this;

        }

        #endregion

        #region Add(Key, Values)

        /// <summary>
        /// Add the given key value pairs to the query string.
        /// </summary>
        /// <param name="Key">The key.</param>
        /// <param name="Values">The values associated with the key.</param>
        public QueryString Add(String               Key,
                               IEnumerable<String>  Values)
        {

            #region Initial checks

            if (Key.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Key),     "The key must not be null or empty!");

            if (Values == null)
                throw new ArgumentNullException(nameof(Values),  "The values must not be null!");

            #endregion

            List<String> ValueList = null;

            if (_Dictionary.TryGetValue(Key, out ValueList))
                ValueList.AddRange(Values);
            else
                _Dictionary.Add(Key, new List<String>(Values));

            return this;

        }

        #endregion


        #region Remove(Key)

        /// <summary>
        /// Remove a key values pair from the query string.
        /// </summary>
        /// <param name="Key">The key.</param>
        public QueryString Remove(String Key)
        {

            #region Initial checks

            if (Key.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Key),  "The key must not be null or empty!");

            #endregion

            _Dictionary.Remove(Key);

            return this;

        }

        #endregion

        #region Remove(Key, Value)

        /// <summary>
        /// Remove the given key value pair from the query string.
        /// </summary>
        /// <param name="Key">The key.</param>
        /// <param name="Value">The value.</param>
        public QueryString Remove(String  Key,
                                  String  Value)
        {

            #region Initial checks

            if (Key.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Key),    "The key must not be null or empty!");

            if (Value == null)
                throw new ArgumentNullException(nameof(Value),  "The value must not be null!");

            #endregion

            List<String> ValueList = null;

            if (_Dictionary.TryGetValue(Key, out ValueList))
                ValueList.Remove(Value);

            return this;

        }

        #endregion

        #region Remove(Key, Values)

        /// <summary>
        /// Remove the given key value pairs from the query string.
        /// </summary>
        /// <param name="Key">The key.</param>
        /// <param name="Values">The values associated with the key.</param>
        public QueryString Remove(String               Key,
                                  IEnumerable<String>  Values)
        {

            List<String> ValueList = null;

            if (_Dictionary.TryGetValue(Key, out ValueList))
                foreach (var value in Values)
                    ValueList.Remove(value);

            return this;

        }

        #endregion


        #region GetString         (ParameterName, DefaultValue = null)

        public String GetString(String  ParameterName,
                                String  DefaultValue = null)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0)
            {
                return Values.Last();
            }

            return DefaultValue;

        }

        #endregion

        #region GetBoolean        (ParameterName)

        public Boolean? GetBoolean(String ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0)
            {
                return Values.Last() == "true";
            }

            return new Boolean?();

        }

        #endregion

        #region GetBoolean        (ParameterName, DefaultValue = false)

        public Boolean GetBoolean(String  ParameterName,
                                  Boolean DefaultValue  = false)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0)
            {
                return Values.Last() == "true";
            }

            return DefaultValue;

        }

        #endregion

        #region GetStrings        (ParameterName, ToLowerCase = false)

        public IEnumerable<String> GetStrings(String   ParameterName,
                                              Boolean  ToLowerCase = false)
        {

            Func<String, String> ToLower  = ToLowerCase ? ToLower = s => s.ToLower() : ToLower = s => s;

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Value) &&
                Value != null &&
                Value.Count > 0)
            {

                return Value.SelectMany(item => item.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).
                                                     Select(s => ToLower(s)));

            }

            return new List<String>();

        }

        #endregion




        #region TryGetString (ParameterName, out Value)

        public Boolean TryGetString(String      ParameterName,
                                    out String  Value)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0)
            {
                Value = Values.Last();
                return true;
            }

            Value = null;
            return false;

        }

        #endregion

        #region TryGetBoolean(ParameterName, out Value, DefaultValue = false)

        public Boolean TryGetBoolean(String       ParameterName,
                                     out Boolean  Value,
                                     Boolean?     DefaultValue = false)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0)
            {
                Value = Values.Last() == "true";
                return true;
            }

            Value = DefaultValue ?? false;
            return false;

        }

        #endregion


        #region GetInt32(ParameterName)

        public Int32? GetInt32(String ParameterName)
        {

            List<String>  Values;
            Int32         Number;

            if (_Dictionary.TryGetValue(ParameterName, out Values) &&
                Values       != null                               &&
                Values.Count  > 0                                  &&
                Int32.TryParse(Values.LastOrDefault(), out Number))

                return Number;

            return null;

        }

        #endregion

        #region GetUInt32(ParameterName)

        public UInt32? GetUInt32(String ParameterName)
        {

            List<String>  Values;
            UInt32        Number;

            if (_Dictionary.TryGetValue(ParameterName, out Values) &&
                Values       != null                               &&
                Values.Count  > 0                                  &&
                UInt32.TryParse(Values.LastOrDefault(), out Number))

                return Number;

            return null;

        }

        #endregion

        #region GetUInt32OrDefault(ParameterName, DefaultValue = 0)

        public UInt32 GetUInt32OrDefault(String ParameterName,
                                         UInt32 DefaultValue = 0)
        {

            List<String> Values;
            UInt32       Number;

            if (_Dictionary.TryGetValue(ParameterName, out Values) &&
                Values       != null                               &&
                Values.Count  > 0                                  &&
                UInt32.TryParse(Values.Last(), out Number))

                return Number;

            return DefaultValue;

        }

        #endregion

        #region GetUInt64(ParameterName)

        public UInt64? GetUInt64(String ParameterName)
        {

            List<String> Values;
            UInt64       Number;

            if (_Dictionary.TryGetValue(ParameterName, out Values) &&
                Values       != null                               &&
                Values.Count  > 0                                  &&
                UInt64.TryParse(Values.Last(), out Number))

                return Number;

            return null;

        }

        #endregion

        #region GetUInt64OrDefault(ParameterName, DefaultValue = 0)

        public UInt64 GetUInt64OrDefault(String ParameterName,
                                         UInt64 DefaultValue = 0)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                UInt64.TryParse(Values.Last(), out UInt64 Number))
            {
                return Number;
            }

            return DefaultValue;

        }

        #endregion


        #region Map(ParameterName, Mapper, DefaultValueT)

        public T Map<T>(String           ParameterName,
                        Func<String, T>  Mapper,
                        T                DefaultValue)
        {

            List<String> Values = null;

            if (Mapper       != null                               &&
                _Dictionary.TryGetValue(ParameterName, out Values) &&
                Values       != null                               &&
                Values.Count  > 0)

                return Mapper(Values.LastOrDefault());

            return DefaultValue;

        }

        #endregion


        #region ParseEnum(ParameterName, DefaultValueT)

        public TEnum ParseEnum<TEnum>(String  ParameterName,
                                      TEnum   DefaultValueT)
             where TEnum : struct
        {

            List<String> Values = null;
            TEnum        ValueT;

            if (_Dictionary.TryGetValue(ParameterName, out Values) &&
                Values       != null                               &&
                Values.Count  > 0                                  &&
                Enum.TryParse(Values.Last(), out ValueT))

                return ValueT;

            return DefaultValueT;

        }

        #endregion

        #region TryParseEnum(ParameterName)

        public TEnum? TryParseEnum<TEnum>(String  ParameterName)
             where TEnum : struct
        {

            List<String> Values = null;
            TEnum        ValueT;

            if (_Dictionary.TryGetValue(ParameterName, out Values) &&
                Values       != null                               &&
                Values.Count  > 0                                  &&
                Enum.TryParse(Values.Last(), out ValueT))

                return new TEnum?(ValueT);

            return new TEnum?();

        }

        #endregion

        #region CreateEnumFilter(ParameterName, FilterDelegate)

        public Func<T, Boolean> CreateEnumFilter<T, TEnum>(String                   ParameterName,
                                                           Func<T, TEnum, Boolean>  FilterDelegate)
             where TEnum : struct
        {

            List<String> Values = null;
            TEnum        ValueT;

            if (FilterDelegate != null                             &&
                _Dictionary.TryGetValue(ParameterName, out Values) &&
                Values         != null                             &&
                Values.Count    > 0)
            {

                var Value = Values.Last();

                if (Enum.TryParse(Value.StartsWith("!", StringComparison.Ordinal)
                                      ? Value.Substring(1)
                                      : Value,
                                  out ValueT))

                    return item => Value.StartsWith("!", StringComparison.Ordinal)
                                      ? !FilterDelegate(item, ValueT)
                                      :  FilterDelegate(item, ValueT);

            }

            return item => true;

        }

        #endregion


        #region GetEnumerator()

        public IEnumerator<KeyValuePair<String, IEnumerable<String>>> GetEnumerator()

            => _Dictionary.
                   Select(v => new KeyValuePair<String, IEnumerable<String>>(v.Key, v.Value)).
                   GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()

            => _Dictionary.
                   Select(v => new KeyValuePair<String, IEnumerable<String>>(v.Key, v.Value)).
                   GetEnumerator();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
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


    }

}
