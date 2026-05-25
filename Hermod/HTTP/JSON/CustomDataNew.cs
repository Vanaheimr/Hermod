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

using System.Buffers;
using System.Collections;
using System.Text;
using System.Text.Json;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A compact JSON object abstraction optimized for many objects sharing
    /// the same property names.
    /// </summary>
    public abstract class CustomDataNew : IEnumerable<CustomDataProperty>
    {

        #region Data

        private static readonly JsonWriterOptions DefaultWriterOptions = new() {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        #endregion

        #region Properties

        /// <summary>
        /// The number of properties.
        /// </summary>
        public abstract Int32 Count { get; }

        /// <summary>
        /// Whether the JSON object is empty.
        /// </summary>
        public Boolean IsEmpty
            => Count == 0;

        /// <summary>
        /// Return a property value by property name, if present.
        /// </summary>
        public CustomDataValue? this[String propertyName]
            => TryGetValue(propertyName, out var value)
                   ? value
                   : null;

        /// <summary>
        /// Return a property value by property key id, if present.
        /// </summary>
        public CustomDataValue? this[Int32 propertyKeyId]
            => TryGetValue(propertyKeyId, out var value)
                   ? value
                   : null;

        #endregion


        #region (static) Empty

        /// <summary>
        /// An empty JSON object.
        /// </summary>
        public static CustomDataNewObject Empty
            => CustomDataNewObject.Empty;

        #endregion

        #region (static) ParseJSON(JSON)

        /// <summary>
        /// Parse an UTF-8 encoded JSON object.
        /// </summary>
        public static CustomDataNewObject ParseJSON(Byte[] JSON)
            => ParseJSON(JSON.AsSpan());

        /// <summary>
        /// Parse a JSON object from a .NET string.
        /// Prefer the UTF-8 overloads for hot paths, because strings are UTF-16
        /// and need an additional UTF-8 encoding step before parsing.
        /// </summary>
        public static CustomDataNewObject ParseJSON(String JSON)
        {

            ArgumentNullException.ThrowIfNull(JSON);

            return ParseJSON(Encoding.UTF8.GetBytes(JSON));

        }

        /// <summary>
        /// Parse an UTF-8 encoded JSON object.
        /// </summary>
        public static CustomDataNewObject ParseJSON(ReadOnlyMemory<Byte> JSON)
            => ParseJSON(JSON.Span);

        /// <summary>
        /// Parse an UTF-8 encoded JSON object.
        /// </summary>
        public static CustomDataNewObject ParseJSON(ReadOnlySpan<Byte> JSON)
        {

            var reader = new Utf8JsonReader(JSON, true, default);

            if (!reader.Read())
                throw new JsonException("Expected a JSON object.");

            var value = CustomDataValue.ParseJSON(ref reader);

            if (value.Kind != CustomDataValueKind.Object || value.ObjectValue is null)
                throw new JsonException("Expected a JSON object.");

            if (value.ObjectValue is not CustomDataNewObject customDataObject)
                throw new JsonException("Expected a parsed custom data object.");

            if (reader.Read())
                throw new JsonException("Unexpected content after the JSON object.");

            return customDataObject;

        }

        /// <summary>
        /// Try to parse a JSON object from a .NET string.
        /// Prefer the UTF-8 overloads for hot paths, because strings are UTF-16
        /// and need an additional UTF-8 encoding step before parsing.
        /// </summary>
        public static Boolean TryParseJSON(String              JSON,
                                           out CustomDataNewObject? CustomData,
                                           out String?         ErrorResponse)
        {

            ArgumentNullException.ThrowIfNull(JSON);

            return TryParseJSON(Encoding.UTF8.GetBytes(JSON),
                                out CustomData,
                                out ErrorResponse);

        }

        /// <summary>
        /// Try to parse an UTF-8 encoded JSON object.
        /// </summary>
        public static Boolean TryParseJSON(ReadOnlySpan<Byte> JSON,
                                           out CustomDataNewObject? CustomData,
                                           out String?         ErrorResponse)
        {

            try
            {
                CustomData     = ParseJSON(JSON);
                ErrorResponse  = null;
                return true;
            }
            catch (Exception e)
            {
                CustomData     = null;
                ErrorResponse  = e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSONBytes(Indented = false)

        /// <summary>
        /// Serialize this object as UTF-8 encoded JSON.
        /// </summary>
        public Byte[] ToJSONBytes(Boolean Indented = false)
        {

            using var buffer = new ArrayBufferWriterStream();
            using (var writer = new Utf8JsonWriter(buffer, DefaultWriterOptions with {
                Indented = Indented
            }))
            {
                WriteJSON(writer);
            }

            return buffer.ToArray();

        }

        #endregion

        #region ToJSONString(Indented = false)

        /// <summary>
        /// Serialize this object as a JSON string.
        /// </summary>
        public String ToJSONString(Boolean Indented = false)
            => Encoding.UTF8.GetString(ToJSONBytes(Indented));

        #endregion

        #region ToRAWJSON(Indented = false)

        /// <summary>
        /// Serialize the internal representation as JSON.
        /// This is mainly intended for tests and debugging, not for public APIs.
        /// </summary>
        public String ToRAWJSON(Boolean Indented = false)
        {

            using var buffer = new ArrayBufferWriterStream();
            using (var writer = new Utf8JsonWriter(buffer, DefaultWriterOptions with {
                Indented = Indented
            }))
            {
                WriteRAWJSON(writer);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());

        }

        #endregion

        #region WriteRAWJSON(Writer)

        /// <summary>
        /// Serialize the internal representation as JSON.
        /// This is mainly intended for tests and debugging, not for public APIs.
        /// </summary>
        public void WriteRAWJSON(Utf8JsonWriter Writer)
        {

            Writer.WriteStartObject();

            Writer.WritePropertyName("keys");
            CustomDataPropertyKeyLookup.WriteRAWJSON(Writer);

            Writer.WritePropertyName("object");
            WriteRAWJSONObject(Writer);

            Writer.WriteEndObject();

        }

        #endregion

        #region WriteRAWJSONObject(Writer)

        /// <summary>
        /// Serialize this object's internal property representation as JSON.
        /// This is mainly intended for tests and debugging, not for public APIs.
        /// </summary>
        public void WriteRAWJSONObject(Utf8JsonWriter Writer)
        {

            Writer.WriteStartArray();

            foreach (var property in this)
            {
                Writer.WriteStartObject();
                Writer.WriteNumber("keyId", property.KeyId);
                Writer.WritePropertyName("value");
                property.Value.WriteRAWJSON(Writer);
                Writer.WriteEndObject();
            }

            Writer.WriteEndArray();

        }

        #endregion

        #region WriteJSON(Writer)

        /// <summary>
        /// Serialize this object via the given UTF-8 JSON writer.
        /// </summary>
        public void WriteJSON(Utf8JsonWriter Writer)
        {

            Writer.WriteStartObject();

            foreach (var property in this)
            {
                Writer.WritePropertyName(CustomDataPropertyKeyLookup.GetText(property.KeyId));
                property.Value.WriteJSON(Writer);
            }

            Writer.WriteEndObject();

        }

        #endregion


        #region ContainsKey(PropertyName)

        /// <summary>
        /// Whether a property exists.
        /// </summary>
        public Boolean ContainsKey(String PropertyName)
            => CustomDataPropertyKeyLookup.TryGetId(PropertyName, out var propertyKeyId) &&
               ContainsKey(propertyKeyId);

        #endregion

        #region ContainsKey(PropertyKeyId)

        /// <summary>
        /// Whether a property key id exists.
        /// </summary>
        public Boolean ContainsKey(Int32 PropertyKeyId)
            => TryGetValue(PropertyKeyId, out _);

        #endregion

        #region TryGetValue(PropertyName,  out Value)

        /// <summary>
        /// Try to return a property value by property name.
        /// </summary>
        public Boolean TryGetValue(String PropertyName,
                                   out CustomDataValue Value)
        {

            if (CustomDataPropertyKeyLookup.TryGetId(PropertyName, out var propertyKeyId))
                return TryGetValue(propertyKeyId, out Value);

            Value = default;
            return false;

        }

        #endregion

        #region TryGetValue(PropertyKeyId, out Value)

        /// <summary>
        /// Try to return a property value by property key id.
        /// </summary>
        public abstract Boolean TryGetValue(Int32               PropertyKeyId,
                                            out CustomDataValue  Value);

        #endregion

        #region Set(PropertyName, Value)

        /// <summary>
        /// Return a copy of this object with the given property set.
        /// </summary>
        public virtual CustomDataNew Set(String          PropertyName,
                                         CustomDataValue Value)
            => Set(CustomDataPropertyKeyLookup.GetOrAdd(PropertyName), Value);

        #endregion

        #region Set(PropertyKeyId, Value)

        /// <summary>
        /// Return a copy of this object with the given property set.
        /// </summary>
        public virtual CustomDataNew Set(Int32           PropertyKeyId,
                                         CustomDataValue Value)
            => throw new NotSupportedException($"{GetType().Name} does not support immutable property updates.");

        #endregion

        #region Remove(PropertyName)

        /// <summary>
        /// Return a copy of this object without the given property.
        /// </summary>
        public virtual CustomDataNew Remove(String PropertyName)
            => CustomDataPropertyKeyLookup.TryGetId(PropertyName, out var propertyKeyId)
                   ? Remove(propertyKeyId)
                   : this;

        #endregion

        #region Remove(PropertyKeyId)

        /// <summary>
        /// Return a copy of this object without the given property key id.
        /// </summary>
        public virtual CustomDataNew Remove(Int32 PropertyKeyId)
            => throw new NotSupportedException($"{GetType().Name} does not support immutable property removals.");

        #endregion


        #region GetEnumerator()

        public abstract IEnumerator<CustomDataProperty> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        #endregion

    }


    /// <summary>
    /// Concrete compact JSON object representation using a sorted property array.
    /// </summary>
    public sealed class CustomDataNewObject : CustomDataNew
    {

        #region Data

        private readonly CustomDataProperty[] properties;

        #endregion

        #region Properties

        /// <summary>
        /// The number of properties.
        /// </summary>
        public override Int32 Count
            => properties.Length;

        #endregion

        #region Constructor(s)

        public CustomDataNewObject(IEnumerable<CustomDataProperty>? Properties = null)
        {
            properties = Normalize(Properties);
        }

        private CustomDataNewObject(CustomDataProperty[] Properties,
                                    Boolean              AlreadyNormalized)
        {
            properties = AlreadyNormalized
                             ? Properties
                             : Normalize(Properties);
        }

        #endregion


        #region (static) Empty

        /// <summary>
        /// An empty JSON object.
        /// </summary>
        public static new CustomDataNewObject Empty { get; }
            = new([], true);

        #endregion

        #region TryGetValue(PropertyKeyId, out Value)

        /// <summary>
        /// Try to return a property value by property key id.
        /// </summary>
        public override Boolean TryGetValue(Int32               PropertyKeyId,
                                            out CustomDataValue  Value)
        {

            var index = FindPropertyIndex(PropertyKeyId);

            if (index >= 0)
            {
                Value = properties[index].Value;
                return true;
            }

            Value = default;
            return false;

        }

        #endregion

        #region Set(PropertyName, Value)

        /// <summary>
        /// Return a copy of this object with the given property set.
        /// </summary>
        public override CustomDataNewObject Set(String          PropertyName,
                                                CustomDataValue Value)
            => Set(CustomDataPropertyKeyLookup.GetOrAdd(PropertyName), Value);

        #endregion

        #region Set(PropertyKeyId, Value)

        /// <summary>
        /// Return a copy of this object with the given property set.
        /// </summary>
        public override CustomDataNewObject Set(Int32           PropertyKeyId,
                                                CustomDataValue Value)
        {

            var index = FindPropertyIndex(PropertyKeyId);

            if (index >= 0)
            {
                var updatedProperties = (CustomDataProperty[]) properties.Clone();
                updatedProperties[index] = new CustomDataProperty(PropertyKeyId, Value);
                return new CustomDataNewObject(updatedProperties, true);
            }

            var newProperties = new CustomDataProperty[properties.Length + 1];
            var insertAt      = ~index;

            Array.Copy(properties, 0, newProperties, 0, insertAt);
            newProperties[insertAt] = new CustomDataProperty(PropertyKeyId, Value);
            Array.Copy(properties, insertAt, newProperties, insertAt + 1, properties.Length - insertAt);

            return new CustomDataNewObject(newProperties, true);

        }

        #endregion

        #region Remove(PropertyName)

        /// <summary>
        /// Return a copy of this object without the given property.
        /// </summary>
        public override CustomDataNewObject Remove(String PropertyName)
            => CustomDataPropertyKeyLookup.TryGetId(PropertyName, out var propertyKeyId)
                   ? Remove(propertyKeyId)
                   : this;

        #endregion

        #region Remove(PropertyKeyId)

        /// <summary>
        /// Return a copy of this object without the given property key id.
        /// </summary>
        public override CustomDataNewObject Remove(Int32 PropertyKeyId)
        {

            var index = FindPropertyIndex(PropertyKeyId);

            if (index < 0)
                return this;

            if (properties.Length == 1)
                return Empty;

            var newProperties = new CustomDataProperty[properties.Length - 1];

            Array.Copy(properties, 0, newProperties, 0, index);
            Array.Copy(properties, index + 1, newProperties, index, properties.Length - index - 1);

            return new CustomDataNewObject(newProperties, true);

        }

        #endregion

        #region GetEnumerator()

        public override IEnumerator<CustomDataProperty> GetEnumerator()
            => ((IEnumerable<CustomDataProperty>) properties).GetEnumerator();

        #endregion

        #region (private) FindPropertyIndex(PropertyKeyId)

        private Int32 FindPropertyIndex(Int32 PropertyKeyId)
        {

            var lower = 0;
            var upper = properties.Length - 1;

            while (lower <= upper)
            {

                var index = lower + ((upper - lower) / 2);
                var keyId = properties[index].KeyId;

                if (keyId == PropertyKeyId)
                    return index;

                if (keyId < PropertyKeyId)
                    lower = index + 1;
                else
                    upper = index - 1;

            }

            return ~lower;

        }

        #endregion

        #region (internal static) Normalize(Properties)

        internal static CustomDataProperty[] Normalize(IEnumerable<CustomDataProperty>? Properties)
        {

            if (Properties is null)
                return [];

            var properties = new List<CustomDataProperty>();
            var indexes    = new Dictionary<Int32, Int32>();

            foreach (var property in Properties)
            {

                if (indexes.TryGetValue(property.KeyId, out var index))
                    properties[index] = property;
                else
                {
                    indexes.Add(property.KeyId, properties.Count);
                    properties.Add(property);
                }

            }

            if (properties.Count == 0)
                return [];

            properties.Sort(static (a, b) => a.KeyId.CompareTo(b.KeyId));

            return properties.ToArray();

        }

        #endregion

    }


    /// <summary>
    /// A compact JSON property.
    /// </summary>
    public readonly struct CustomDataProperty
    {

        public Int32           KeyId { get; }
        public CustomDataValue Value { get; }

        public String Key
            => CustomDataPropertyKeyLookup.GetText(KeyId);

        public CustomDataProperty(String          Key,
                                  CustomDataValue Value)
            : this(CustomDataPropertyKeyLookup.GetOrAdd(Key), Value)
        { }

        public CustomDataProperty(Int32           KeyId,
                                  CustomDataValue Value)
        {
            this.KeyId  = KeyId;
            this.Value  = Value;
        }

    }


    /// <summary>
    /// A compact JSON array representation.
    /// </summary>
    public sealed class CustomDataArray : IEnumerable<CustomDataValue>
    {

        private readonly CustomDataValue[] values;

        public Int32 Count
            => values.Length;

        public CustomDataValue this[Int32 Index]
            => values[Index];

        public CustomDataArray(IEnumerable<CustomDataValue>? Values = null)
        {
            values = Values?.ToArray() ?? [];
        }

        private CustomDataArray(CustomDataValue[] Values)
        {
            values = Values;
        }

        public static CustomDataArray Empty { get; }
            = new([]);

        public CustomDataArray Set(Int32           Index,
                                   CustomDataValue Value)
        {

            var newValues = (CustomDataValue[]) values.Clone();
            newValues[Index] = Value;

            return new CustomDataArray(newValues);

        }

        public CustomDataArray Add(CustomDataValue Value)
        {

            var newValues = new CustomDataValue[values.Length + 1];

            Array.Copy(values, newValues, values.Length);
            newValues[^1] = Value;

            return new CustomDataArray(newValues);

        }

        public void WriteJSON(Utf8JsonWriter Writer)
        {

            Writer.WriteStartArray();

            foreach (var value in values)
                value.WriteJSON(Writer);

            Writer.WriteEndArray();

        }

        public void WriteRAWJSON(Utf8JsonWriter Writer)
        {

            Writer.WriteStartArray();

            foreach (var value in values)
                value.WriteRAWJSON(Writer);

            Writer.WriteEndArray();

        }

        public IEnumerator<CustomDataValue> GetEnumerator()
            => ((IEnumerable<CustomDataValue>) values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

    }


    /// <summary>
    /// The JSON value kind.
    /// </summary>
    public enum CustomDataValueKind
    {
        Null,
        Boolean,
        Int64,
        UInt64,
        Decimal,
        Double,
        String,
        Object,
        Array
    }


    /// <summary>
    /// A compact JSON value.
    /// </summary>
    public readonly struct CustomDataValue
    {

        #region Properties

        public CustomDataValueKind Kind         { get; }
        public Int64               Int64Value   { get; }
        public UInt64              UInt64Value  { get; }
        public Decimal             DecimalValue { get; }
        public Double              DoubleValue  { get; }
        public String?             StringValue  { get; }
        public CustomDataNew?      ObjectValue  { get; }
        public CustomDataArray?    ArrayValue   { get; }

        public Boolean BooleanValue
            => Int64Value != 0;

        #endregion

        #region Constructor(s)

        private CustomDataValue(CustomDataValueKind Kind,
                                Int64               Int64Value   = default,
                                UInt64              UInt64Value  = default,
                                Decimal             DecimalValue = default,
                                Double              DoubleValue  = default,
                                String?             StringValue  = default,
                                CustomDataNew?      ObjectValue  = default,
                                CustomDataArray?    ArrayValue   = default)
        {
            this.Kind          = Kind;
            this.Int64Value    = Int64Value;
            this.UInt64Value   = UInt64Value;
            this.DecimalValue  = DecimalValue;
            this.DoubleValue   = DoubleValue;
            this.StringValue   = StringValue;
            this.ObjectValue   = ObjectValue;
            this.ArrayValue    = ArrayValue;
        }

        #endregion


        #region Static values

        public static CustomDataValue Null  { get; }
            = new(CustomDataValueKind.Null);

        public static CustomDataValue True  { get; }
            = new(CustomDataValueKind.Boolean, Int64Value: 1);

        public static CustomDataValue False { get; }
            = new(CustomDataValueKind.Boolean);

        #endregion

        #region Factory methods

        public static CustomDataValue From(Boolean Value)
            => Value ? True : False;

        public static CustomDataValue From(Int32 Value)
            => new(CustomDataValueKind.Int64, Int64Value: Value);

        public static CustomDataValue From(Int64 Value)
            => new(CustomDataValueKind.Int64, Int64Value: Value);

        public static CustomDataValue From(UInt64 Value)
            => new(CustomDataValueKind.UInt64, UInt64Value: Value);

        public static CustomDataValue From(Decimal Value)
            => new(CustomDataValueKind.Decimal, DecimalValue: Value);

        public static CustomDataValue From(Double Value)
            => new(CustomDataValueKind.Double, DoubleValue: Value);

        public static CustomDataValue From(String Value)
            => new(CustomDataValueKind.String, StringValue: Value);

        public static CustomDataValue From(CustomDataNew Value)
            => new(CustomDataValueKind.Object, ObjectValue: Value);

        public static CustomDataValue From(CustomDataArray Value)
            => new(CustomDataValueKind.Array, ArrayValue: Value);

        #endregion

        #region (static) ParseJSON(ref Reader)

        internal static CustomDataValue ParseJSON(ref Utf8JsonReader Reader)
        {

            switch (Reader.TokenType)
            {

                case JsonTokenType.StartObject:
                    return From(ParseObject(ref Reader));

                case JsonTokenType.StartArray:
                    return From(ParseArray(ref Reader));

                case JsonTokenType.String:
                    return From(Reader.GetString() ?? String.Empty);

                case JsonTokenType.Number:
                    return ParseNumber(ref Reader);

                case JsonTokenType.True:
                    return True;

                case JsonTokenType.False:
                    return False;

                case JsonTokenType.Null:
                    return Null;

                default:
                    throw new JsonException($"Unexpected JSON token '{Reader.TokenType}'.");

            }

        }

        #endregion

        #region WriteJSON(Writer)

        public void WriteJSON(Utf8JsonWriter Writer)
        {

            switch (Kind)
            {

                case CustomDataValueKind.Null:
                    Writer.WriteNullValue();
                    break;

                case CustomDataValueKind.Boolean:
                    Writer.WriteBooleanValue(BooleanValue);
                    break;

                case CustomDataValueKind.Int64:
                    Writer.WriteNumberValue(Int64Value);
                    break;

                case CustomDataValueKind.UInt64:
                    Writer.WriteNumberValue(UInt64Value);
                    break;

                case CustomDataValueKind.Decimal:
                    Writer.WriteNumberValue(DecimalValue);
                    break;

                case CustomDataValueKind.Double:
                    Writer.WriteNumberValue(DoubleValue);
                    break;

                case CustomDataValueKind.String:
                    Writer.WriteStringValue(StringValue);
                    break;

                case CustomDataValueKind.Object:
                    if (ObjectValue is null)
                        Writer.WriteNullValue();
                    else
                        ObjectValue.WriteJSON(Writer);
                    break;

                case CustomDataValueKind.Array:
                    if (ArrayValue is null)
                        Writer.WriteNullValue();
                    else
                        ArrayValue.WriteJSON(Writer);
                    break;

                default:
                    throw new JsonException($"Unsupported custom data value kind '{Kind}'.");

            }

        }

        #endregion

        #region WriteRAWJSON(Writer)

        /// <summary>
        /// Serialize the internal representation as JSON.
        /// This is mainly intended for tests and debugging, not for public APIs.
        /// </summary>
        public void WriteRAWJSON(Utf8JsonWriter Writer)
        {

            switch (Kind)
            {

                case CustomDataValueKind.Null:
                    Writer.WriteNullValue();
                    break;

                case CustomDataValueKind.Boolean:
                    Writer.WriteBooleanValue(BooleanValue);
                    break;

                case CustomDataValueKind.Int64:
                    Writer.WriteNumberValue(Int64Value);
                    break;

                case CustomDataValueKind.UInt64:
                    Writer.WriteNumberValue(UInt64Value);
                    break;

                case CustomDataValueKind.Decimal:
                    Writer.WriteNumberValue(DecimalValue);
                    break;

                case CustomDataValueKind.Double:
                    Writer.WriteNumberValue(DoubleValue);
                    break;

                case CustomDataValueKind.String:
                    Writer.WriteStringValue(StringValue);
                    break;

                case CustomDataValueKind.Object:
                    if (ObjectValue is null)
                        Writer.WriteNullValue();
                    else
                        ObjectValue.WriteRAWJSONObject(Writer);
                    break;

                case CustomDataValueKind.Array:
                    if (ArrayValue is null)
                        Writer.WriteNullValue();
                    else
                        ArrayValue.WriteRAWJSON(Writer);
                    break;

                default:
                    throw new JsonException($"Unsupported custom data value kind '{Kind}'.");

            }

        }

        #endregion

        #region (private static) ParseObject(ref Reader)

        private static CustomDataNewObject ParseObject(ref Utf8JsonReader Reader)
        {

            var properties = new List<CustomDataProperty>();
            var indexes    = new Dictionary<Int32, Int32>();

            while (Reader.Read())
            {

                if (Reader.TokenType == JsonTokenType.EndObject)
                    return new CustomDataNewObject(properties);

                if (Reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException($"Expected a JSON property name, but found '{Reader.TokenType}'.");

                var propertyName  = Reader.GetString() ?? String.Empty;
                var propertyKeyId = CustomDataPropertyKeyLookup.GetOrAdd(propertyName);

                if (!Reader.Read())
                    throw new JsonException($"Missing JSON value for property '{propertyName}'.");

                var property = new CustomDataProperty(
                                   propertyKeyId,
                                   ParseJSON(ref Reader)
                               );

                if (indexes.TryGetValue(propertyKeyId, out var index))
                    properties[index] = property;
                else
                {
                    indexes.Add(propertyKeyId, properties.Count);
                    properties.Add(property);
                }

            }

            throw new JsonException("Unexpected end of JSON object.");

        }

        #endregion

        #region (private static) ParseArray(ref Reader)

        private static CustomDataArray ParseArray(ref Utf8JsonReader Reader)
        {

            var values = new List<CustomDataValue>();

            while (Reader.Read())
            {

                if (Reader.TokenType == JsonTokenType.EndArray)
                    return values.Count == 0
                               ? CustomDataArray.Empty
                               : new CustomDataArray(values);

                values.Add(ParseJSON(ref Reader));

            }

            throw new JsonException("Unexpected end of JSON array.");

        }

        #endregion

        #region (private static) ParseNumber(ref Reader)

        private static CustomDataValue ParseNumber(ref Utf8JsonReader Reader)
        {

            if (Reader.TryGetInt64(out var int64Value))
                return From(int64Value);

            if (Reader.TryGetUInt64(out var uint64Value))
                return From(uint64Value);

            if (Reader.TryGetDecimal(out var decimalValue))
                return From(decimalValue);

            if (Reader.TryGetDouble(out var doubleValue))
                return From(doubleValue);

            throw new JsonException("Invalid JSON number.");

        }

        #endregion

    }


    /// <summary>
    /// Shared process-wide lookup for JSON property names.
    /// </summary>
    public static class CustomDataPropertyKeyLookup
    {

        private static readonly Lock                   mutex  = new();
        private static readonly Dictionary<String, Int32> ids = new(StringComparer.Ordinal);
        private static readonly List<String>           keys   = [];

        public static Int32 Count
        {
            get
            {
                lock (mutex)
                    return keys.Count;
            }
        }

        public static Int32 GetOrAdd(String PropertyName)
        {

            ArgumentNullException.ThrowIfNull(PropertyName);

            lock (mutex)
            {

                if (ids.TryGetValue(PropertyName, out var id))
                    return id;

                id = keys.Count + 1;
                keys.Add(PropertyName);
                ids.Add(PropertyName, id);

                return id;

            }

        }

        public static Boolean TryGetId(String PropertyName,
                                       out Int32 Id)
        {

            ArgumentNullException.ThrowIfNull(PropertyName);

            lock (mutex)
                return ids.TryGetValue(PropertyName, out Id);

        }

        public static String GetText(Int32 Id)
        {

            lock (mutex)
            {

                if (Id <= 0 || Id > keys.Count)
                    throw new ArgumentOutOfRangeException(nameof(Id), $"Unknown custom data property key id '{Id}'.");

                return keys[Id - 1];

            }

        }

        public static IReadOnlyList<KeyValuePair<Int32, String>> Snapshot()
        {

            lock (mutex)
            {

                var snapshot = new KeyValuePair<Int32, String>[keys.Count];

                for (var i = 0; i < keys.Count; i++)
                    snapshot[i] = new KeyValuePair<Int32, String>(i + 1, keys[i]);

                return snapshot;

            }

        }

        internal static void WriteRAWJSON(Utf8JsonWriter Writer)
        {

            Writer.WriteStartArray();

            foreach (var key in Snapshot())
            {
                Writer.WriteStartObject();
                Writer.WriteNumber("keyId", key.Key);
                Writer.WriteString("key",   key.Value);
                Writer.WriteEndObject();
            }

            Writer.WriteEndArray();

        }

    }


    internal sealed class ArrayBufferWriterStream : Stream
    {

        private readonly ArrayBufferWriter<Byte> buffer;

        public ArrayBufferWriterStream()
        {
            buffer = new ArrayBufferWriter<Byte>();
        }

        public override Boolean CanRead
            => false;

        public override Boolean CanSeek
            => false;

        public override Boolean CanWrite
            => true;

        public override Int64 Length
            => buffer.WrittenCount;

        public override Int64 Position
        {
            get => buffer.WrittenCount;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        { }

        public override Int32 Read(Byte[] Buffer,
                                   Int32  Offset,
                                   Int32  Count)
            => throw new NotSupportedException();

        public override Int64 Seek(Int64      Offset,
                                   SeekOrigin Origin)
            => throw new NotSupportedException();

        public override void SetLength(Int64 Value)
            => throw new NotSupportedException();

        public override void Write(Byte[] Buffer,
                                   Int32  Offset,
                                   Int32  Count)
            => Write(Buffer.AsSpan(Offset, Count));

        public override void Write(ReadOnlySpan<Byte> Buffer)
        {
            var span = buffer.GetSpan(Buffer.Length);
            Buffer.CopyTo(span);
            buffer.Advance(Buffer.Length);
        }

        public Byte[] ToArray()
            => buffer.WrittenSpan.ToArray();

    }

}
