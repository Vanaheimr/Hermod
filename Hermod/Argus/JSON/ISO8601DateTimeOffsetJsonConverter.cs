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

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Argus
{

    public class ISO8601DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
    {

        public override DateTimeOffset Read(ref Utf8JsonReader    reader,
                                            Type                  typeToConvert,
                                            JsonSerializerOptions options)
        {

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("DateTimeOffset values must be encoded as ISO 8601 strings!");

            var value = reader.GetString();

            if (String.IsNullOrWhiteSpace(value))
                throw new JsonException("DateTimeOffset string values must not be empty!");

            return DateTimeOffset.Parse(
                       value,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
                   );

        }

        public override void Write(Utf8JsonWriter        writer,
                                   DateTimeOffset        value,
                                   JsonSerializerOptions options)

            => writer.WriteStringValue(value.ToISO8601());

    }

}
