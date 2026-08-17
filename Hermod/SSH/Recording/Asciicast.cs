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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The kind of an asciicast v2 event (the second element of each event line).
    /// </summary>
    public enum AsciicastEventCode
    {
        /// <summary>
        /// Terminal output printed to the user ("o").
        /// </summary>
        Output,
        /// <summary>
        /// Terminal input typed by the user ("i") — only present when input capture is enabled.
        /// </summary>
        Input,
        /// <summary>
        /// A terminal resize ("r"); the data is "COLSxROWS".
        /// </summary>
        Resize,
        /// <summary>
        /// A marker / annotation ("m"), e.g. the terminal exit status.
        /// </summary>
        Marker
    }


    /// <summary>
    /// Helpers mapping <see cref="AsciicastEventCode"/> to and from its one-letter wire code.
    /// </summary>
    public static class AsciicastEventCodes
    {

        /// <summary>
        /// The one-letter asciicast code for an event kind.
        /// </summary>
        public static String ToWire(this AsciicastEventCode Code)
            => Code switch {
                   AsciicastEventCode.Output  => "o",
                   AsciicastEventCode.Input   => "i",
                   AsciicastEventCode.Resize  => "r",
                   AsciicastEventCode.Marker  => "m",
                   _                          => "o"
               };

        /// <summary>
        /// Parse a one-letter asciicast code; unknown codes map to <see cref="AsciicastEventCode.Output"/>.
        /// </summary>
        public static AsciicastEventCode FromWire(String Code)
            => Code switch {
                   "o"  => AsciicastEventCode.Output,
                   "i"  => AsciicastEventCode.Input,
                   "r"  => AsciicastEventCode.Resize,
                   "m"  => AsciicastEventCode.Marker,
                   _    => AsciicastEventCode.Output
               };

    }


    /// <summary>
    /// The header of an asciicast v2 recording (the first JSON line). Carries the terminal geometry, the
    /// start time and — for an <c>exec</c> recording — the exact command and its exit status.
    /// </summary>
    public sealed record AsciicastHeader
    {

        /// <summary>
        /// The asciicast format version (always 2).
        /// </summary>
        public Int32                              Version     { get; init; } = 2;

        /// <summary>
        /// The initial terminal width in columns.
        /// </summary>
        public Int32                              Width       { get; init; } = 80;

        /// <summary>
        /// The initial terminal height in rows.
        /// </summary>
        public Int32                              Height      { get; init; } = 24;

        /// <summary>
        /// The wall-clock start time of the recording (serialized as Unix seconds).
        /// </summary>
        public DateTimeOffset?                    Timestamp   { get; init; }

        /// <summary>
        /// The command that was run (for an <c>exec</c> recording).
        /// </summary>
        public String?                            Command     { get; init; }

        /// <summary>
        /// An optional human-readable title.
        /// </summary>
        public String?                            Title       { get; init; }

        /// <summary>
        /// The exit status of the command, once known (serialized as the non-standard <c>exit_status</c> field).
        /// </summary>
        public Int32?                             ExitStatus  { get; init; }

        /// <summary>
        /// Captured environment (e.g. <c>TERM</c>, <c>SHELL</c>).
        /// </summary>
        public IReadOnlyDictionary<String, String>?  Env      { get; init; }

    }


    /// <summary>
    /// One asciicast v2 event line: <c>[elapsed, code, data]</c>.
    /// </summary>
    /// <param name="ElapsedSeconds">Seconds since the recording started.</param>
    /// <param name="Code">The event kind.</param>
    /// <param name="Data">The event payload (UTF-8 text for output/input, "COLSxROWS" for a resize).</param>
    public readonly record struct AsciicastEvent(Double              ElapsedSeconds,
                                                 AsciicastEventCode  Code,
                                                 String              Data);

}
