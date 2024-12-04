/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Web;
using System.Text;
using System.Globalization;
using System.Collections.Concurrent;

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
    public class QueryString : IEnumerable<KeyValuePair<String, IEnumerable<Object>>>
    {

        #region Data

        private        readonly ConcurrentDictionary<String, List<String>>  internalDictionary   = [];

        private static readonly Char[]                                      AndSign              = ['&'];
        private static readonly Char[]                                      EqualsSign           = ['='];
        private static readonly Char[]                                      CommaSign            = [','];

        private const           String                                      exclamationMark      = "!";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new query string based on the optional given string repesentation.
        /// </summary>
        /// <param name="Text">An optional text to parse.</param>
        private QueryString(String? Text = null)
        {

            if (Text is not null && Text.IsNotNullOrEmpty())
            {

                Text = HttpUtility.UrlDecode(Text.Trim());

                var position = Text.IndexOf('?');
                if (position >= 0)
                    Text = Text.Remove(0, position + 1);

                if (Text.IsNotNullOrEmpty())
                {
                    foreach (var keyValuePair in Text.Split(AndSign, StringSplitOptions.RemoveEmptyEntries))
                    {

                        var split = keyValuePair.Split(EqualsSign, StringSplitOptions.RemoveEmptyEntries);

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
        public static QueryString Empty

            => new ();

        #endregion

        #region (static) Parse(Text)

        /// <summary>
        ///  Parse the given string repesentation of a HTTP Query String.
        /// </summary>
        /// <param name="Text">The text to parse.</param>
        public static QueryString Parse(String Text)

            => new (Text);

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
                throw new ArgumentNullException(nameof(Key), "The key must not be null or empty!");

            #endregion

            internalDictionary.AddOrUpdate(
                                   Key,
                                   [Value],
                                   (key, list) => list.AddAndReturnList(Value)
                               );

            return this;

        }


        /// <summary>
        /// Add the given key value pair to the query string.
        /// </summary>
        /// <param name="Key">The key.</param>
        /// <param name="Value">The value associated with the key.</param>
        public QueryString Add(String  Key,
                               Int32   Value)
        {

            #region Initial checks

            if (Key.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Key), "The key must not be null or empty!");

            #endregion

            internalDictionary.AddOrUpdate(
                                   Key,
                                   [Value.ToString()],
                                   (key, list) => list.AddAndReturnList(Value.ToString())
                               );

            return this;

        }


        /// <summary>
        /// Add the given key value pair to the query string.
        /// </summary>
        /// <param name="Key">The key.</param>
        /// <param name="Value">The value associated with the key.</param>
        public QueryString Add(String  Key,
                               Double  Value)
        {

            #region Initial checks

            if (Key.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Key), "The key must not be null or empty!");

            #endregion

            internalDictionary.AddOrUpdate(
                                   Key,
                                   [Value.ToString()],
                                   (key, list) => list.AddAndReturnList(Value.ToString())
                               );

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
                throw new ArgumentNullException(nameof(Key), "The key must not be null or empty!");

            if (!Values.Any())
                return this;

            #endregion

            internalDictionary.AddOrUpdate(
                                   Key,
                                   new List<String>(Values),
                                   (key, list) => list.AddAndReturnList(Values)
                               );

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

            internalDictionary.TryRemove(Key, out _);

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
                throw new ArgumentNullException(nameof(Key), "The key must not be null or empty!");

            if (Value.IsNullOrEmpty())
                return this;

            #endregion

            if (internalDictionary.TryGetValue(Key, out var list) &&
                list is not null)
            {
                internalDictionary.TryUpdate(
                                       Key,
                                       list.RemoveAndReturnList(Value),
                                       list
                                   );
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

            if (!Values.Any())
                return this;

            #endregion

            if (internalDictionary.TryGetValue(Key, out var oldList) &&
                oldList is not null)
            {

                var newList = oldList.ToList();

                foreach (var value in Values)
                    newList.Remove(value);

                internalDictionary.TryUpdate(
                                       Key,
                                       newList,
                                       oldList
                                   );
            }

            return this;

        }

        #endregion


        #region GetChar      (ParameterName, DefaultValue)

        public Char GetChar(String  ParameterName,
                            Char    DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count  > 0)
            {
                return values.Last().ToString()?[0] ?? DefaultValue;
            }

            return DefaultValue;

        }

        #endregion

        #region GetString    (ParameterName)

        public String? GetString(String ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count  > 0)
            {
                return values.Last().ToString();
            }

            return null;

        }

        #endregion

        #region GetString    (ParameterName, DefaultValue)

        public String GetString(String ParameterName,
                                String DefaultValue)

            => GetString(ParameterName) ?? DefaultValue;

        #endregion

        #region GetStrings   (ParameterName)

        public IEnumerable<String> GetStrings(String  ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count  > 0)
            {
                return values.SelectMany(item => item.ToString()?.Split(CommaSign, StringSplitOptions.RemoveEmptyEntries) ?? []);
            }

            return new List<String>();

        }

        #endregion

        #region TryGetString (ParameterName, out Value)

        public Boolean TryGetString(String       ParameterName,
                                    out String?  Value)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count  > 0)
            {
                Value = values.Last().ToString();
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

            if (FilterDelegate is not null &&
                TryGetString(ParameterName, out var value) &&
                value is not null)
            {

                return item => value.StartsWith(exclamationMark, StringComparison.Ordinal)
                                   ? !FilterDelegate(item, value[1..])
                                   :  FilterDelegate(item, value);

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

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var lastValue = values.LastOrDefault()?.ToString()?.ToLower();

                if (lastValue is null)
                    return null;

                if (lastValue == "0" || lastValue == "false")
                    return false;

                if (lastValue == ""  || lastValue == "1" || lastValue == "true")
                    return true;

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

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var lastValue = values.LastOrDefault()?.ToString()?.ToLower();

                if (lastValue is null)
                    return DefaultValue;

                return lastValue == "" || lastValue == "1" || lastValue == "true";

            }

            return DefaultValue;

        }

        #endregion

        #region TryGetBoolean(ParameterName, out Value, DefaultValue = false)

        public Boolean TryGetBoolean(String       ParameterName,
                                     out Boolean  Value,
                                     Boolean      DefaultValue = false)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var lastValue = values.LastOrDefault()?.ToString()?.ToLower();

                if (lastValue is not null)
                {

                    if (lastValue == "1" || lastValue == "true")
                    {
                        Value = true;
                        return true;
                    }

                    if (lastValue == "" || lastValue == "0" || lastValue == "false")
                    {
                        Value = false;
                        return true;
                    }

                }

            }

            Value = DefaultValue;
            return false;

        }

        #endregion


        #region GetInt16 (ParameterName)

        public Int16? GetInt16(String ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count  > 0 &&
                Int16.TryParse(values.LastOrDefault()?.ToString(), out var number))
            {
                return number;
            }

            return null;

        }

        #endregion

        #region GetInt16 (ParameterName, DefaultValue)

        public Int16 GetInt16(String ParameterName,
                              Int16  DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Int16.TryParse(values.Last()?.ToString(), out var number))
            {
                return number;
            }

            return DefaultValue;

        }

        #endregion

        #region GetUInt16(ParameterName)

        public UInt16? GetUInt16(String ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                UInt16.TryParse(values.LastOrDefault()?.ToString(), out var number))
            {
                return number;
            }

            return null;

        }

        #endregion

        #region GetUInt16(ParameterName, DefaultValue)

        public UInt16 GetUInt16(String ParameterName,
                                UInt16 DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                UInt16.TryParse(values.Last()?.ToString(), out var number))
            {
                return number;
            }

            return DefaultValue;

        }

        #endregion


        #region GetInt32 (ParameterName)

        public Int32? GetInt32(String ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Int32.TryParse(values.LastOrDefault()?.ToString(), out var number))
            {
                return number;
            }

            return null;

        }

        #endregion

        #region GetInt32 (ParameterName, DefaultValue)

        public Int32 GetInt32(String ParameterName,
                              Int32  DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Int32.TryParse(values.Last()?.ToString(), out var number))
            {
                return number;
            }

            return DefaultValue;

        }

        #endregion

        #region GetUInt32(ParameterName)

        public UInt32? GetUInt32(String ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count  > 0 &&
                UInt32.TryParse(values.LastOrDefault()?.ToString(), out var number))
            {
                return number;
            }

            return null;

        }

        #endregion

        #region GetUInt32(ParameterName, DefaultValue)

        public UInt32 GetUInt32(String ParameterName,
                                UInt32 DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                UInt32.TryParse(values.Last()?.ToString(), out var number))
            {
                return number;
            }

            return DefaultValue;

        }

        #endregion


        #region GetInt64 (ParameterName)

        public Int64? GetInt64(String ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Int64.TryParse(values.LastOrDefault()?.ToString(), out var number))
            {
                return number;
            }

            return null;

        }

        #endregion

        #region GetInt64 (ParameterName, DefaultValue)

        public Int64 GetInt64(String ParameterName,
                              Int64  DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Int64.TryParse(values.Last()?.ToString(), out var number))
            {
                return number;
            }

            return DefaultValue;

        }

        #endregion

        #region TryGetInt64(ParameterName, out Value, DefaultValue = false)

        public Boolean TryGetInt64(String     ParameterName,
                                   out Int64  Value,
                                   Int64      DefaultValue = 0)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var lastValue = values.LastOrDefault();

                if (lastValue is not null &&
                    Int64.TryParse(lastValue?.ToString(), out var number))
                {
                    Value = number;
                    return true;
                }

            }

            Value = DefaultValue;
            return false;

        }

        #endregion


        #region GetUInt64(ParameterName)

        public UInt64? GetUInt64(String ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                UInt64.TryParse(values.LastOrDefault()?.ToString(), out var number))
            {
                return number;
            }

            return null;

        }

        #endregion

        #region GetUInt64(ParameterName, DefaultValue)

        public UInt64 GetUInt64(String ParameterName,
                                UInt64 DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                UInt64.TryParse(values.Last()?.ToString(), out var number))
            {
                return number;
            }

            return DefaultValue;

        }

        #endregion

        #region TryGetUInt64(ParameterName, out Value, DefaultValue = false)

        public Boolean TryGetUInt64(String      ParameterName,
                                    out UInt64  Value,
                                    UInt64      DefaultValue = 0)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var lastValue = values.LastOrDefault();

                if (lastValue is not null &&
                    UInt64.TryParse(lastValue?.ToString(), out var number))
                {
                    Value = number;
                    return true;
                }

            }

            Value = DefaultValue;
            return false;

        }

        #endregion


        #region GetSingle (ParameterName)

        public Single? GetSingle(String  ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Single.TryParse(values.LastOrDefault()?.ToString(),
                                NumberStyles.Any,
                                CultureInfo.InvariantCulture,
                                out var number))
            {
                return number;
            }

            return null;

        }

        #endregion

        #region GetSingle (ParameterName, DefaultValue)

        public Single GetSingle(String  ParameterName,
                                Single  DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Single.TryParse(values.Last()?.ToString(),
                                NumberStyles.Any,
                                CultureInfo.InvariantCulture,
                                out var number))
            {
                return number;
            }

            return DefaultValue;

        }

        #endregion

        #region TryGetSingle(ParameterName, out Value, DefaultValue = false)

        public Boolean TryGetSingle(String ParameterName,
                                    out Single Value,
                                    Single DefaultValue = 0)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var lastValue = values.LastOrDefault();

                if (lastValue is not null &&
                    Single.TryParse(lastValue?.ToString(), out var number))
                {
                    Value = number;
                    return true;
                }

            }

            Value = DefaultValue;
            return false;

        }

        #endregion


        #region GetDouble (ParameterName)

        public Double? GetDouble(String  ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Double.TryParse(values.LastOrDefault()?.ToString(),
                                NumberStyles.Any,
                                CultureInfo.InvariantCulture,
                                out var number))
            {
                return number;
            }

            return null;

        }

        #endregion

        #region GetDouble (ParameterName, DefaultValue)

        public Double GetDouble(String  ParameterName,
                                Double  DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Double.TryParse(values.Last()?.ToString(),
                                NumberStyles.Any,
                                CultureInfo.InvariantCulture,
                                out var number))
            {
                return number;
            }

            return DefaultValue;

        }

        #endregion

        #region TryGetDouble(ParameterName, out Value, DefaultValue = false)

        public Boolean TryGetDouble(String      ParameterName,
                                    out Double  Value,
                                    Double      DefaultValue = 0)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var lastValue = values.LastOrDefault();

                if (lastValue is not null &&
                    Double.TryParse(lastValue?.ToString(), out var number))
                {
                    Value = number;
                    return true;
                }

            }

            Value = DefaultValue;
            return false;

        }

        #endregion


        #region GetDecimal(ParameterName)

        public Decimal? GetDecimal(String  ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Decimal.TryParse(values.LastOrDefault()?.ToString(),
                                 NumberStyles.Any,
                                 CultureInfo.InvariantCulture,
                                 out var number))
            {
                return number;
            }

            return null;

        }

        #endregion

        #region GetDecimal(ParameterName, DefaultValue)

        public Decimal GetDecimal(String   ParameterName,
                                  Decimal  DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Decimal.TryParse(values.Last()?.ToString(),
                                 NumberStyles.Any,
                                 CultureInfo.InvariantCulture,
                                 out var number))
            {
                return number;
            }

            return DefaultValue;

        }

        #endregion

        #region TryGetDecimal(ParameterName, out Value, DefaultValue = false)

        public Boolean TryGetDecimal(String       ParameterName,
                                     out Decimal  Value,
                                     Decimal      DefaultValue = 0)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var lastValue = values.LastOrDefault();

                if (lastValue is not null &&
                    Decimal.TryParse(lastValue?.ToString(), out var number))
                {
                    Value = number;
                    return true;
                }

            }

            Value = DefaultValue;
            return false;

        }

        #endregion


        #region GetDateTime(ParameterName, DefaultValue = null)

        /// <summary>
        /// Get a timestamp from a HTTP query parameter.
        /// </summary>
        /// <param name="ParameterName">The name of the query parameter.</param>
        /// <param name="DefaultValue">An optional default timestamp.</param>
        public DateTime? GetDateTime(String     ParameterName,
                                     DateTime?  DefaultValue   = null)
        {

            if (TryGetString(ParameterName, out var value) &&
                DateTime.TryParse(value, out var timestamp))
            {
                return DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
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

            if (TryGetString(ParameterName, out var value) &&
                DateTime.TryParse(value, out var timestamp))
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

            if (TryGetString(ParameterName, out var value) &&
                DateTime.TryParse(value, out var timestamp))
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
                TryGetString(ParameterName, out var value) &&
                DateTime.TryParse(value, out var timestamp))
            {
                return item => FilterDelegate(item, DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
            }

            return _ => true;

        }

        #endregion


        #region GetTimeSpan(ParameterName)

        public TimeSpan? GetTimeSpan(String  ParameterName)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Double.TryParse(values.LastOrDefault()?.ToString(),
                                NumberStyles.Any,
                                CultureInfo.InvariantCulture,
                                out var number))
            {
                return TimeSpan.FromSeconds(number);
            }

            return null;

        }

        #endregion

        #region GetTimeSpan(ParameterName, DefaultValue)

        public TimeSpan GetTimeSpan(String    ParameterName,
                                    TimeSpan  DefaultValue)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0 &&
                Double.TryParse(values.Last()?.ToString(),
                                NumberStyles.Any,
                                CultureInfo.InvariantCulture,
                                out var number))
            {
                return TimeSpan.FromSeconds(number);
            }

            return DefaultValue;

        }

        #endregion

        #region GetTimeSpan(ParameterName, DefaultValue = null)

        /// <summary>
        /// Get a time span from a HTTP query parameter.
        /// </summary>
        /// <param name="ParameterName">The name of the query parameter.</param>
        /// <param name="DefaultValue">An optional default timestamp.</param>
        public TimeSpan? GetTimeSpan(String     ParameterName,
                                     TimeSpan?  DefaultValue   = null)
        {

            if (TryGetDouble(ParameterName, out var value))
                return TimeSpan.FromSeconds(value);

            return DefaultValue;

        }

        #endregion


        #region Map(ParameterName, Parser)

        public T? Map<T>(String            ParameterName,
                         Func<String, T?>  Parser)
            where T : struct
        {

            if (Parser is not null &&
                internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var value = values.Last()?.ToString();

                if (value is not null)
                    return Parser(value);

            }

            return new T?();

        }

        #endregion

        #region Map(ParameterName, Parser, DefaultValueT)

        public T Map<T>(String           ParameterName,
                        Func<String, T>  Parser,
                        T                DefaultValue)
        {

            if (Parser is not null &&
                internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var value = values.Last()?.ToString();

                if (value is not null)
                    return Parser(value);

            }

            return DefaultValue;

        }

        #endregion


        #region ParseEnum            (ParameterName, DefaultValueT)

        public TEnum ParseEnum<TEnum>(String  ParameterName,
                                      TEnum   DefaultValueT)
             where TEnum : struct
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0   &&
                Enum.TryParse(values.Last()?.ToString(), out TEnum ValueT))
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

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0   &&
                Enum.TryParse(values.Last()?.ToString(), out TEnum ValueT))
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


            if (FilterDelegate is not null &&
                internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var value = values.Last()?.ToString();

                if (value is not null &&
                    Enum.TryParse(value.StartsWith('!') == true
                                      ? value[1..]
                                      : value,
                                  true,
                                  out TEnum ValueT))
                {

                    return item => value.ToString()?.StartsWith('!') == true
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


            if (FilterDelegate is not null &&
                internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var value = values.Last()?.ToString();

                if (value is not null &&
                    TryParser(value.StartsWith('!') == true
                                  ? value[1..]
                                  : value,
                              out var valueT) &&
                    valueT is not null)
                {

                    return item => value.StartsWith('!')
                                       ? !FilterDelegate(item, valueT)
                                       :  FilterDelegate(item, valueT);

                }

            }

            return item => true;

        }

        #endregion

        #region CreateMultiEnumFilter(ParameterName)

        public Func<TEnum, Boolean> CreateMultiEnumFilter<TEnum>(String ParameterName)
             where TEnum : struct
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var includes = new List<TEnum>();
                var excludes = new List<TEnum>();

                foreach (var Value in values)
                {

                    var value = Value?.ToString();

                    if (value is not null &&
                        Enum.TryParse(value.StartsWith('!')
                                          ? value[1..]
                                          : value,
                                      true,
                                      out TEnum valueT))
                    {

                        if (value.StartsWith('!'))
                            excludes.Add(valueT);

                        else
                            includes.Add(valueT);

                    }

                }

                if (includes.Count == 0 && excludes.Count == 0)
                    return item =>  true;

                if (includes.Count > 0 && excludes.Count == 0)
                    return item =>  includes.Contains(item);

                if (includes.Count == 0 && excludes.Count > 0)
                    return item => !excludes.Contains(item);

                return item => includes.Contains(item) &&
                              !excludes.Contains(item);

            }

            return item => true;

        }

        #endregion

        #region CreateMultiFilter    (ParameterName, TryParser)

        public Func<T, Boolean> CreateMultiFilter<T>(String        ParameterName,
                                                     TryParser<T>  TryParser)
        {

            if (internalDictionary.TryGetValue(ParameterName, out var values) &&
                values is not null &&
                values.Count > 0)
            {

                var includes = new List<T>();
                var excludes = new List<T>();

                foreach (var Value in values)
                {

                    var value = Value?.ToString();

                    if (value is not null &&
                        TryParser(value.StartsWith('!')
                                      ? value[1..]
                                      : value,
                                  out var valueT) &&
                        valueT is not null)
                    {

                        if (value.StartsWith('!'))
                            excludes.Add(valueT);

                        else
                            includes.Add(valueT);

                    }

                }

                if (includes.Count == 0 && excludes.Count == 0)
                    return item =>  true;

                if (includes.Count  > 0 && excludes.Count == 0)
                    return item =>  includes.Contains(item);

                if (includes.Count == 0 && excludes.Count > 0)
                    return item => !excludes.Contains(item);

                return item => includes.Contains(item) &&
                              !excludes.Contains(item);

            }

            return item => true;

        }

        #endregion


        #region GetEnumerator()

        public IEnumerator<KeyValuePair<String, IEnumerable<Object>>> GetEnumerator()

            => internalDictionary.
                   Select(v => new KeyValuePair<String, IEnumerable<Object>>(v.Key, v.Value)).
                   GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()

            => internalDictionary.
                   Select(v => new KeyValuePair<String, IEnumerable<Object>>(v.Key, v.Value)).
                   GetEnumerator();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            if (internalDictionary.Count == 0)
                return String.Empty;

            var stringBuilder = new StringBuilder();

            foreach (var KeyValuePair in internalDictionary)
                foreach (var Value in KeyValuePair.Value)
                    stringBuilder.Append('&').
                                  Append(HttpUtility.UrlEncode(KeyValuePair.Key)).
                                  Append('=').
                                  Append(HttpUtility.UrlEncode(Value.ToString()));

            stringBuilder[0] = '?';

            return stringBuilder.ToString();

        }

        #endregion


    }

}
