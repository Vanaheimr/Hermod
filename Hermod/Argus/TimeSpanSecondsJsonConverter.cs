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

using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Argus
{

    public class TimeSpanSecondsJsonConverter : JsonConverter<TimeSpan>
    {

        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {

            return reader.TokenType switch
            {

                JsonTokenType.Number  => TimeSpan.FromSeconds(reader.GetDouble()),

                JsonTokenType.String  => TimeSpan.TryParse(reader.GetString(), out var value)
                                             ? value
                                             : throw new JsonException("TimeSpan string values must use a valid TimeSpan format!"),

                _                     => throw new JsonException("TimeSpan values must be encoded as seconds or as a valid TimeSpan string!")

            };

        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value.TotalSeconds);

    }

}
