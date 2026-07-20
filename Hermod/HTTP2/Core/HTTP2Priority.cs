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
    /// stream's queued bytes go on the wire next when several are ready at once.
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
        /// a client sends; the mirror of <see cref="HTTP2Connection"/>'s parser.
        /// </summary>
        public string ToHeaderValue()
            => Incremental ? $"u={Urgency}, i" : $"u={Urgency}";
    }

}
