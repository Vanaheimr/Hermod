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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Argus
{

    public class URLJsonConverter : JsonConverter<URL>
    {

        public override URL Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {

            if (reader.TokenType is not JsonTokenType.String)
                throw new JsonException("URL values must be encoded as strings!");

            var text = reader.GetString();

            if (String.IsNullOrWhiteSpace(text))
                throw new JsonException("URL values must not be empty!");

            if (URL.TryParse(text, out var url))
                return url;

            throw new JsonException($"Invalid URL value '{text}'!");

        }

        public override void Write(Utf8JsonWriter writer, URL value, JsonSerializerOptions options)

            => writer.WriteStringValue(value.ToString());

    }

}
