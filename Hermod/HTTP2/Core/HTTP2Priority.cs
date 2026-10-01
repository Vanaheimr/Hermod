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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// RFC 9218 (Extensible Prioritization Scheme for HTTP), Section 4: the
    /// urgency/incremental pair carried by a "priority" header field or a
    /// PRIORITY_UPDATE frame. Urgency ranges 0 (most urgent) to 7 (least),
    /// default 3; incremental defaults to false ("send as a single unit").
    /// Read by <see cref="HTTP2Connection"/>'s writer loop to decide which
    /// stream's queued bytes go on the wire next when several are ready at once
    /// (see <see cref="HTTP2SendOrder"/>).
    /// </summary>
    public readonly record struct HTTP2Priority(byte Urgency, bool Incremental)
    {
        public const byte DefaultUrgency = 3;

        public static readonly HTTP2Priority Default = new(DefaultUrgency, false);

        /// <summary>
        /// Serialize as an RFC 9218 Priority Field Value — the RFC 8941 Structured
        /// Fields Dictionary <c>u=&lt;urgency&gt;</c> plus a bare <c>i</c> when
        /// incremental (a bare key is shorthand for the Boolean true). Used both
        /// for the <c>priority</c> request header and the PRIORITY_UPDATE payload
        /// a client sends; the mirror of <see cref="Parse"/>.
        /// </summary>
        public string ToHeaderValue()
            => Incremental ? $"u={Urgency}, i" : $"u={Urgency}";

        /// <summary>
        /// Parse an RFC 9218 Priority Field Value — a Structured Fields
        /// Dictionary (RFC 8941) with two recognized keys, "u" (urgency, integer
        /// 0-7, default 3) and "i" (incremental, boolean, default false). Used
        /// both for the request's own "priority" header field (Section 4) and
        /// for a PRIORITY_UPDATE frame's payload (Section 7.1), which share the
        /// identical value grammar.
        ///
        /// Deliberately lenient (Section 4): a parse failure, an unknown key, or
        /// an out-of-range urgency just falls back to that parameter's default
        /// rather than raising a stream/connection error — a malformed priority
        /// hint is a hint gone wrong, not a protocol violation. Per RFC 8941,
        /// Section 3.3.6, a bare key with no "=value" is shorthand for a true
        /// Boolean, which is how a bare "i" (e.g. "u=1, i") means "i=?1".
        /// </summary>
        public static HTTP2Priority Parse(string Value)
        {

            var urgency     = DefaultUrgency;
            var incremental = false;

            foreach (var rawMember in Value.Split(','))
            {

                var member = rawMember.Trim();
                if (member.Length == 0)
                    continue;

                var eq    = member.IndexOf('=');
                var key   = (eq < 0 ? member : member[..eq]).Trim();
                var value =  eq < 0 ? "?1"   : member[(eq + 1)..].Trim();

                switch (key)
                {

                    case "u" when byte.TryParse(value, out var u) && u <= 7:
                        urgency = u;
                        break;

                    case "i":
                        incremental = value == "?1";
                        break;

                    // Unknown key, or a recognized key with an out-of-range /
                    // malformed value — ignored, that parameter keeps its default.

                }

            }

            return new HTTP2Priority(urgency, incremental);

        }
    }

}
