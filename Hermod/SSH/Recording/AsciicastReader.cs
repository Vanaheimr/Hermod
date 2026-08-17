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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A parsed asciicast v2 recording: its header plus the sequence of events.
    /// </summary>
    /// <param name="Header">The recording header.</param>
    /// <param name="Events">The events, in order.</param>
    public sealed record AsciicastRecording(AsciicastHeader Header, IReadOnlyList<AsciicastEvent> Events)
    {

        /// <summary>
        /// Concatenate all output-event data — the terminal transcript a reviewer would see on replay.
        /// </summary>
        public String OutputText
            => String.Concat(Events.Where(e => e.Code == AsciicastEventCode.Output).Select(e => e.Data));

        /// <summary>
        /// The exit status recorded as a terminal marker (<c>exit-status=N</c>), or the header value, if any.
        /// </summary>
        public Int32? ExitStatus
        {
            get
            {
                if (Header.ExitStatus is { } fromHeader)
                    return fromHeader;

                foreach (var e in Events)
                    if (e.Code == AsciicastEventCode.Marker &&
                        e.Data.StartsWith("exit-status=", StringComparison.Ordinal) &&
                        Int32.TryParse(e.Data.AsSpan("exit-status=".Length), out var code))
                        return code;

                return null;
            }
        }

    }


    /// <summary>
    /// Parses asciicast v2 JSON-lines back into a <see cref="AsciicastRecording"/>. Tolerant of a truncated
    /// final line (a crash-interrupted recording): a partial or malformed last line is skipped, so every
    /// complete event that was flushed is still recovered.
    /// </summary>
    public static class AsciicastReader
    {

        #region Parse(Text)

        /// <summary>
        /// Parse a complete or partially-written asciicast document.
        /// </summary>
        public static AsciicastRecording Parse(String Text)
        {

            var lines   = Text.Split('\n');
            var header  = (AsciicastHeader?) null;
            var events  = new List<AsciicastEvent>();

            for (var i = 0; i < lines.Length; i++)
            {

                var line = lines[i].TrimEnd('\r');
                if (line.Length == 0)
                    continue;

                // A truncated final line cannot be parsed — skip it and keep everything before it.
                if (!TryParseJson(line, out var doc))
                    continue;

                using (doc)
                {
                    if (header is null && doc!.RootElement.ValueKind == JsonValueKind.Object)
                        header = ParseHeader(doc.RootElement);

                    else if (doc!.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        if (TryParseEvent(doc.RootElement, out var ev))
                            events.Add(ev);
                    }
                }

            }

            return new AsciicastRecording(header ?? new AsciicastHeader(), events);

        }

        #endregion

        #region ParseAsync(Reader, CancellationToken)

        /// <summary>
        /// Parse an asciicast document from a text reader.
        /// </summary>
        public static async ValueTask<AsciicastRecording> ParseAsync(TextReader Reader, CancellationToken CancellationToken = default)
            => Parse(await Reader.ReadToEndAsync(CancellationToken).ConfigureAwait(false));

        #endregion


        #region (private) helpers

        private static Boolean TryParseJson(String Line, out JsonDocument? Document)
        {
            try     { Document = JsonDocument.Parse(Line); return true; }
            catch   { Document = null; return false; }
        }

        private static AsciicastHeader ParseHeader(JsonElement Root)
        {

            var env = (Dictionary<String, String>?) null;
            if (Root.TryGetProperty("env", out var envEl) && envEl.ValueKind == JsonValueKind.Object)
            {
                env = [];
                foreach (var prop in envEl.EnumerateObject())
                    env[prop.Name] = prop.Value.GetString() ?? "";
            }

            return new AsciicastHeader {
                Version     = Root.TryGetProperty("version",     out var v)  ? v.GetInt32()  : 2,
                Width       = Root.TryGetProperty("width",       out var w)  ? w.GetInt32()  : 80,
                Height      = Root.TryGetProperty("height",      out var h)  ? h.GetInt32()  : 24,
                Timestamp   = Root.TryGetProperty("timestamp",   out var t)  ? DateTimeOffset.FromUnixTimeSeconds(t.GetInt64()) : null,
                Command     = Root.TryGetProperty("command",     out var c)  ? c.GetString() : null,
                Title       = Root.TryGetProperty("title",       out var ti) ? ti.GetString(): null,
                ExitStatus  = Root.TryGetProperty("exit_status", out var e)  ? e.GetInt32()  : null,
                Env         = env
            };

        }

        private static Boolean TryParseEvent(JsonElement Array, out AsciicastEvent Event)
        {

            Event = default;
            if (Array.GetArrayLength() < 3)
                return false;

            var elapsed = Array[0].GetDouble();
            var code    = AsciicastEventCodes.FromWire(Array[1].GetString() ?? "o");
            var data    = Array[2].GetString() ?? "";

            Event = new AsciicastEvent(elapsed, code, data);
            return true;

        }

        #endregion

    }

}
