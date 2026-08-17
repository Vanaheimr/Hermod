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
using System.Globalization;
using System.Text.Json;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Writes an asciicast v2 recording as JSON-lines to a <see cref="TextWriter"/>: a header object on the
    /// first line, then one <c>[elapsed, code, data]</c> array per event. Events are flushed incrementally
    /// (never buffering the whole session), so a recording truncated by a crash is still a valid prefix.
    /// </summary>
    public sealed class AsciicastWriter
    {

        #region Data

        private readonly TextWriter  writer;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an asciicast writer over the given text sink.
        /// </summary>
        public AsciicastWriter(TextWriter Writer)
        {
            this.writer = Writer;
        }

        #endregion


        #region WriteHeaderAsync(Header, CancellationToken)

        /// <summary>
        /// Write the header line (must be called exactly once, before any events).
        /// </summary>
        public async ValueTask WriteHeaderAsync(AsciicastHeader Header, CancellationToken CancellationToken = default)
        {

            var buffer = new ArrayBufferWriter<Byte>();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteNumber("version", Header.Version);
                json.WriteNumber("width",   Header.Width);
                json.WriteNumber("height",  Header.Height);

                if (Header.Timestamp is { } ts)
                    json.WriteNumber("timestamp", ts.ToUnixTimeSeconds());

                if (Header.Command is not null)
                    json.WriteString("command", Header.Command);

                if (Header.Title is not null)
                    json.WriteString("title", Header.Title);

                if (Header.ExitStatus is { } exit)
                    json.WriteNumber("exit_status", exit);

                if (Header.Env is { Count: > 0 })
                {
                    json.WriteStartObject("env");
                    foreach (var (key, value) in Header.Env)
                        json.WriteString(key, value);
                    json.WriteEndObject();
                }

                json.WriteEndObject();
            }

            await writer.WriteLineAsync(System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan)).ConfigureAwait(false);
            await writer.FlushAsync(CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region WriteEventAsync(Event, CancellationToken)

        /// <summary>
        /// Append one event line and flush it.
        /// </summary>
        public async ValueTask WriteEventAsync(AsciicastEvent Event, CancellationToken CancellationToken = default)
        {

            var buffer = new ArrayBufferWriter<Byte>();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartArray();
                json.WriteNumberValue(Math.Round(Event.ElapsedSeconds, 6));
                json.WriteStringValue(Event.Code.ToWire());
                json.WriteStringValue(Event.Data);
                json.WriteEndArray();
            }

            await writer.WriteLineAsync(System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan)).ConfigureAwait(false);
            await writer.FlushAsync(CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region (static) FormatElapsed(Seconds)

        /// <summary>
        /// Format an elapsed value the way asciicast expects (invariant, up to 6 decimals).
        /// </summary>
        public static String FormatElapsed(Double Seconds)
            => Math.Round(Seconds, 6).ToString("0.######", CultureInfo.InvariantCulture);

        #endregion

    }

}
