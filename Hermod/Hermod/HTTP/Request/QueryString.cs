/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Web;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for the HTTP QueryString class.
    /// </summary>
    public static class QueryStringExtensions
    {

        #region ParseFromTimestampFilter   (this QueryString)

        /// <summary>
        /// Parse optional from-timestamp filter...
        /// </summary>
        /// <param name="QueryString">A HTTP query string.</param>
        public static DateTime? ParseFromTimestampFilter(this QueryString QueryString)
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
        /// <param name="QueryString">A HTTP query string.</param>
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
        /// <param name="QueryString">A HTTP query string.</param>
        /// <param name="FromTimestamp">The optional 'from' query parameter.</param>
        /// <param name="ToTimestamp">The optional 'to' query parameter.</param>
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

        private        readonly Dictionary<String, List<String>> _Dictionary;

        private static readonly Char[] AndSign     = { '&' };
        private static readonly Char[] EqualsSign  = { '=' };
        private static readonly Char[] CommaSign   = { ',' };

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

                Text = HttpUtility.UrlDecode(Text.Trim());

                var position = Text.IndexOf("?", StringComparison.Ordinal);
                if (position >= 0)
                    Text = Text.Remove(0, position + 1);

                String[] split = null;

                if (Text.IsNotNullOrEmpty())
                {
                    foreach (var keyValuePair in Text.Split(AndSign, StringSplitOptions.RemoveEmptyEntries))
                    {

                        split = keyValuePair.Split(EqualsSign, StringSplitOptions.RemoveEmptyEntries);

                        switch (split.Length)
                        {

                            case 1:
                                Add(split[0],
                                    "");
                                break;

                            case 2:
                                Add(split[0],
                                    split[1].Split (CommaSign, StringSplitOptions.RemoveEmptyEntries).
                                             Select(_ => _.Trim()));
                                break;

                        }

                    }
                }

            }

        }

        #endregion


        #region (static) New

        /// <summary>
        /// Return a new empty query string.
        /// </summary>
        public static QueryString New

            => new QueryString();

        #endregion

        #region (static) Parse(Text)

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

            if (Value == null)
                return this;

            #endregion

            if (_Dictionary.TryGetValue(Key, out List<String> ValueList))
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

            if (Values == null || !Values.Any())
                return this;

            #endregion

            if (_Dictionary.TryGetValue(Key, out List<String> ValueList))
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
                return this;

            #endregion

            if (_Dictionary.TryGetValue(Key, out List<String> ValueList))
            {

                ValueList.Remove(Value);

                if (ValueList.Count == 0)
                    _Dictionary.Remove(Key);

            }

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

            #region Initial checks

            if (Key.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Key), "The key must not be null or empty!");

            if (Values == null || !Values.Any())
                return this;

            #endregion

            if (_Dictionary.TryGetValue(Key, out List<String> ValueList))
            {

                foreach (var value in Values)
                    ValueList.Remove(value);

                if (ValueList.Count == 0)
                    _Dictionary.Remove(Key);

            }

            return this;

        }

        #endregion


        #region GetChar      (ParameterName, DefaultValue)

        public Char GetChar(String  ParameterName,
                            Char    DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0)
            {
                return Values.Last()[0];
            }

            return DefaultValue;

        }

        #endregion

        #region GetString    (ParameterName, DefaultValue = null)

        public String? GetString(String  ParameterName,
                                 String? DefaultValue = null)
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

        #region GetStrings   (ParameterName)

        public IEnumerable<String> GetStrings(String  ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Value) &&
                Value       != null &&
                Value.Count  > 0)
            {

                return Value.SelectMany(item => item.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)).
                             Select    (item => item.Trim());

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

        #region CreateStringFilter(ParameterName, FilterDelegate)

        /// <summary>
        /// Create a filter based on the given HTTP query parameter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ParameterName">The name of the query parameter.</param>
        /// <param name="FilterDelegate">A filter delegate.</param>
        public Func<T, Boolean> CreateStringFilter<T>(String                    ParameterName,
                                                      Func<T, String, Boolean>  FilterDelegate)
        {

            if (FilterDelegate != null &&
                TryGetString(ParameterName, out String Value))
            {

                return item => Value.StartsWith("!", StringComparison.Ordinal)
                                   ? !FilterDelegate(item, Value.Substring(1))
                                   :  FilterDelegate(item, Value);

            }

            return _ => true;

        }

        #endregion


        #region GetBoolean   (ParameterName)

        /// <summary>
        /// Get an optional Boolean.
        /// </summary>
        /// <param name="ParameterName">The name of the HTTP query paramerter.</param>
        public Boolean? GetBoolean(String ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0)
            {

                var LastValue = Values.LastOrDefault()?.ToLower();

                if (LastValue == ""  || LastValue == "1" || LastValue == "true")
                    return true;

                if (LastValue == "0" || LastValue == "false")
                    return false;

            }

            return new Boolean?();

        }

        #endregion

        #region GetBoolean   (ParameterName, DefaultValue)

        /// <summary>
        /// Get a Boolean, or the given default value.
        /// </summary>
        /// <param name="ParameterName">The name of the HTTP query paramerter.</param>
        /// <param name="DefaultValue">The default value.</param>
        public Boolean GetBoolean(String  ParameterName,
                                  Boolean DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0)
            {

                var LastValue = Values.LastOrDefault()?.ToLower();

                return LastValue == "" || LastValue == "1" || LastValue == "true";

            }

            return DefaultValue;

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

                var LastValue = Values.LastOrDefault()?.ToLower();

                if (LastValue == "" || LastValue == "1" || LastValue == "true")
                {
                    Value = true;
                    return true;
                }

            }

            Value = DefaultValue ?? false;
            return false;

        }

        #endregion


        #region GetInt16 (ParameterName)

        public Int16? GetInt16(String ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Int16.TryParse(Values.LastOrDefault(), out Int16 Number))
            {
                return Number;
            }

            return null;

        }

        #endregion

        #region GetInt16 (ParameterName, DefaultValue)

        public Int16 GetInt16(String ParameterName,
                              Int16  DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Int16.TryParse(Values.Last(), out Int16 Number))
            {
                return Number;
            }

            return DefaultValue;

        }

        #endregion

        #region GetUInt16(ParameterName)

        public UInt16? GetUInt16(String ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                UInt16.TryParse(Values.LastOrDefault(), out UInt16 Number))
            {
                return Number;
            }

            return null;

        }

        #endregion

        #region GetUInt16(ParameterName, DefaultValue)

        public UInt16 GetUInt16(String ParameterName,
                                UInt16 DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                UInt16.TryParse(Values.Last(), out UInt16 Number))
            {
                return Number;
            }

            return DefaultValue;

        }

        #endregion


        #region GetInt32 (ParameterName)

        public Int32? GetInt32(String ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Int32.TryParse(Values.LastOrDefault(), out Int32 Number))
            {
                return Number;
            }

            return null;

        }

        #endregion

        #region GetInt32 (ParameterName, DefaultValue)

        public Int32 GetInt32(String ParameterName,
                              Int32  DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Int32.TryParse(Values.Last(), out Int32 Number))
            {
                return Number;
            }

            return DefaultValue;

        }

        #endregion

        #region GetUInt32(ParameterName)

        public UInt32? GetUInt32(String ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                UInt32.TryParse(Values.LastOrDefault(), out UInt32 Number))
            {
                return Number;
            }

            return null;

        }

        #endregion

        #region GetUInt32(ParameterName, DefaultValue)

        public UInt32 GetUInt32(String ParameterName,
                                UInt32 DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                UInt32.TryParse(Values.Last(), out UInt32 Number))
            {
                return Number;
            }

            return DefaultValue;

        }

        #endregion


        #region GetInt64 (ParameterName)

        public Int64? GetInt64(String ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Int64.TryParse(Values.LastOrDefault(), out Int64 Number))
            {
                return Number;
            }

            return null;

        }

        #endregion

        #region GetInt64 (ParameterName, DefaultValue)

        public Int64 GetInt64(String ParameterName,
                              Int64  DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Int64.TryParse(Values.Last(), out Int64 Number))
            {
                return Number;
            }

            return DefaultValue;

        }

        #endregion

        #region GetUInt64(ParameterName)

        public UInt64? GetUInt64(String ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                UInt64.TryParse(Values.LastOrDefault(), out UInt64 Number))
            {
                return Number;
            }

            return null;

        }

        #endregion

        #region GetUInt64(ParameterName, DefaultValue)

        public UInt64 GetUInt64(String ParameterName,
                                UInt64 DefaultValue)
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


        #region GetSingle (ParameterName)

        public Single? GetSingle(String  ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Single.TryParse(Values.LastOrDefault(), NumberStyles.Any, CultureInfo.InvariantCulture, out Single Number))
            {
                return Number;
            }

            return null;

        }

        #endregion

        #region GetSingle (ParameterName, DefaultValue)

        public Single GetSingle(String  ParameterName,
                                Single  DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Single.TryParse(Values.Last(), NumberStyles.Any, CultureInfo.InvariantCulture, out Single Number))
            {
                return Number;
            }

            return DefaultValue;

        }

        #endregion

        #region GetDouble (ParameterName)

        public Double? GetDouble(String  ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Double.TryParse(Values.LastOrDefault(), NumberStyles.Any, CultureInfo.InvariantCulture, out Double Number))
            {
                return Number;
            }

            return null;

        }

        #endregion

        #region GetDouble (ParameterName, DefaultValue)

        public Double GetDouble(String  ParameterName,
                                Double  DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Double.TryParse(Values.Last(), NumberStyles.Any, CultureInfo.InvariantCulture, out Double Number))
            {
                return Number;
            }

            return DefaultValue;

        }

        #endregion

        #region GetDecimal(ParameterName)

        public Decimal? GetDecimal(String  ParameterName)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Decimal.TryParse(Values.LastOrDefault(), NumberStyles.Any, CultureInfo.InvariantCulture, out Decimal Number))
            {
                return Number;
            }

            return null;

        }

        #endregion

        #region GetDecimal(ParameterName, DefaultValue)

        public Decimal GetDecimal(String   ParameterName,
                                  Decimal  DefaultValue)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values       != null                                            &&
                Values.Count  > 0                                               &&
                Decimal.TryParse(Values.Last(), NumberStyles.Any, CultureInfo.InvariantCulture, out Decimal Number))
            {
                return Number;
            }

            return DefaultValue;

        }

        #endregion


        #region GetDateTime(ParameterName, DefaultValue = null)

        /// <summary>
        /// Get a timestamp from a HTTP query parameter.
        /// </summary>
        /// <param name="ParameterName">The name of the query parameter.</param>
        /// <param name="DefaultValue">An optional default timestamp.</param>
        public DateTime? GetDateTime(String    ParameterName,
                                     DateTime? DefaultValue  = null)
        {

            if (TryGetString(ParameterName, out String Value) &&
                DateTime.TryParse(Value, out DateTime _Timestamp))
            {
                return DateTime.SpecifyKind(_Timestamp, DateTimeKind.Utc);
            }

            return DefaultValue;

        }

        #endregion

        #region TryGetDateTime(ParameterName)

        /// <summary>
        /// Try to get a timestamp from a HTTP query parameter.
        /// </summary>
        /// <param name="ParameterName">The name of the query parameter.</param>
        public DateTime? TryGetDateTime(String ParameterName)
        {

            if (TryGetString(ParameterName, out String Value) &&
                DateTime.TryParse(Value, out DateTime timestamp))
            {
                return DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
            }

            return null;

        }

        #endregion

        #region TryGetDateTime(ParameterName, out Timestamp)

        /// <summary>
        /// Try to get a timestamp from a HTTP query parameter.
        /// </summary>
        /// <param name="ParameterName">The name of the query parameter.</param>
        /// <param name="Timestamp">The parsed timestamp.</param>
        public Boolean TryGetDateTime(String        ParameterName,
                                      out DateTime  Timestamp)
        {

            if (TryGetString(ParameterName, out String Value) &&
                DateTime.TryParse(Value, out DateTime timestamp))
            {
                Timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
                return true;
            }

            Timestamp = default;
            return false;

        }

        #endregion

        #region CreateDateTimeFilter(ParameterName, FilterDelegate)

        /// <summary>
        /// Create a timestamp filter based on the given HTTP query parameter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ParameterName">The name of the query parameter.</param>
        /// <param name="FilterDelegate">A filter delegate.</param>
        public Func<T, Boolean> CreateDateTimeFilter<T>(String                      ParameterName,
                                                        Func<T, DateTime, Boolean>  FilterDelegate)
        {

            if (FilterDelegate != null &&
                TryGetString(ParameterName, out String Value) &&
                DateTime.TryParse(Value, out DateTime timestamp))
            {
                return item => FilterDelegate(item, DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
            }

            return _ => true;

        }

        #endregion


        #region Map(ParameterName, Parser)

        public T? Map<T>(String            ParameterName,
                         Func<String, T?>  Parser)

            where T : struct

        {


            if (Parser != null                                                  &&
                _Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null                                                  &&
                Values.Count > 0)
            {
                return Parser(Values.LastOrDefault());
            }

            return new T?();

        }

        #endregion

        #region Map(ParameterName, Parser, DefaultValueT)

        public T Map<T>(String           ParameterName,
                        Func<String, T>  Parser,
                        T                DefaultValue)
        {


            if (Parser != null                                                  &&
                _Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null                                                  &&
                Values.Count > 0)
            {
                return Parser(Values.LastOrDefault());
            }

            return DefaultValue;

        }

        #endregion


        #region ParseEnum            (ParameterName, DefaultValueT)

        public TEnum ParseEnum<TEnum>(String  ParameterName,
                                      TEnum   DefaultValueT)
             where TEnum : struct
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0 &&
                Enum.TryParse(Values.Last(), out TEnum ValueT))
            {
                return ValueT;
            }

            return DefaultValueT;

        }

        #endregion

        #region TryParseEnum         (ParameterName)

        public TEnum? TryParseEnum<TEnum>(String  ParameterName)

             where TEnum : struct

        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0 &&
                Enum.TryParse(Values.Last(), out TEnum ValueT))
            {
                return new TEnum?(ValueT);
            }

            return new TEnum?();

        }

        #endregion

        #region CreateEnumFilter     (ParameterName, FilterDelegate)

        public Func<T, Boolean> CreateEnumFilter<T, TEnum>(String                   ParameterName,
                                                           Func<T, TEnum, Boolean>  FilterDelegate)
             where TEnum : struct
        {


            if (FilterDelegate != null &&
                _Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0)
            {

                var Value = Values.Last();

                if (Enum.TryParse(Value.StartsWith("!", StringComparison.Ordinal)
                                      ? Value.Substring(1)
                                      : Value,
                                  true,
                                  out TEnum ValueT))
                {

                    return item => Value.StartsWith("!", StringComparison.Ordinal)
                                      ? !FilterDelegate(item, ValueT)
                                      :  FilterDelegate(item, ValueT);

                }

            }

            return item => true;

        }

        #endregion

        #region CreateFilter         (ParameterName, TryParser, FilterDelegate)

        public Func<T1, Boolean> CreateFilter<T1, T2>(String                 ParameterName,
                                                      TryParser<T2>          TryParser,
                                                      Func<T1, T2, Boolean>  FilterDelegate)
        {


            if (FilterDelegate != null &&
                _Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0)
            {

                var Value = Values.Last();

                if (TryParser(Value.StartsWith("!", StringComparison.Ordinal)
                                  ? Value.Substring(1)
                                  : Value,
                              out T2 ValueT))
                {

                    return item => Value.StartsWith("!", StringComparison.Ordinal)
                                      ? !FilterDelegate(item, ValueT)
                                      :  FilterDelegate(item, ValueT);

                }

            }

            return item => true;

        }

        #endregion

        #region CreateMultiEnumFilter(ParameterName)

        public Func<TEnum, Boolean> CreateMultiEnumFilter<TEnum>(String ParameterName)
             where TEnum : struct
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0)
            {

                var Includes = new List<TEnum>();
                var Excludes = new List<TEnum>();

                foreach (var Value in Values)
                {

                    if (Enum.TryParse(Value.StartsWith("!", StringComparison.Ordinal)
                                          ? Value.Substring(1)
                                          : Value,
                                      true,
                                      out TEnum ValueT))
                    {

                        if (Value.StartsWith("!", StringComparison.Ordinal))
                            Excludes.Add(ValueT);

                        else
                            Includes.Add(ValueT);

                    }

                }

                if (Includes.Count == 0 && Excludes.Count == 0)
                    return item =>  true;

                if (Includes.Count > 0 && Excludes.Count == 0)
                    return item =>  Includes.Contains(item);

                if (Includes.Count == 0 && Excludes.Count > 0)
                    return item => !Excludes.Contains(item);

                return item => Includes.Contains(item) &&
                              !Excludes.Contains(item);

            }

            return item => true;

        }

        #endregion

        #region CreateMultiFilter    (ParameterName, TryParser)

        public Func<T, Boolean> CreateMultiFilter<T>(String        ParameterName,
                                                     TryParser<T>  TryParser)
        {

            if (_Dictionary.TryGetValue(ParameterName, out List<String> Values) &&
                Values != null &&
                Values.Count > 0)
            {

                var Includes = new List<T>();
                var Excludes = new List<T>();

                foreach (var Value in Values)
                {

                    if (TryParser(Value.StartsWith("!", StringComparison.Ordinal)
                                      ? Value.Substring(1)
                                      : Value,
                                  out T ValueT))
                    {

                        if (Value.StartsWith("!", StringComparison.Ordinal))
                            Excludes.Add(ValueT);

                        else
                            Includes.Add(ValueT);

                    }

                }

                if (Includes.Count == 0 && Excludes.Count == 0)
                    return item =>  true;

                if (Includes.Count > 0 && Excludes.Count == 0)
                    return item =>  Includes.Contains(item);

                if (Includes.Count == 0 && Excludes.Count > 0)
                    return item => !Excludes.Contains(item);

                return item => Includes.Contains(item) &&
                              !Excludes.Contains(item);

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
                                   Append(HttpUtility.UrlEncode(KeyValuePair.Key)).
                                   Append("=").
                                   Append(HttpUtility.UrlEncode(Value));

            _StringBuilder[0] = '?';

            return _StringBuilder.ToString();

        }

        #endregion


    }

}
