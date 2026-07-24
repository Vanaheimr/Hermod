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
    /// A <c>Content-Range</c> field value (RFC 9110, Section 14.4) —
    /// <c>bytes 500-999/8000</c> on a 206, or the unsatisfied form
    /// <c>bytes&#160;*&#47;8000</c> on a 416, which carries only the current length.
    ///
    /// The server formats these; a client resuming a download has to *read* them, to
    /// learn where the bytes it just received belong and how long the whole thing
    /// is. Both directions live here so a 206 this stack produces and a 206 it
    /// consumes cannot disagree about the syntax.
    /// </summary>
    /// <param name="Start">First byte position, or null for the unsatisfied form.</param>
    /// <param name="End">Last byte position (inclusive), or null for the unsatisfied form.</param>
    /// <param name="CompleteLength">Total length of the representation, or null when the sender did not know it ("*").</param>
    public sealed record HTTPContentRange(Int64? Start, Int64? End, Int64? CompleteLength)
    {

        #region Properties

        /// <summary>
        /// The unsatisfied form (<c>bytes&#160;*&#47;n</c>), which a 416 uses to say
        /// "that range does not exist, but the resource is this long".
        /// </summary>
        public Boolean IsUnsatisfied
            => Start is null || End is null;

        /// <summary>Number of bytes this range covers, or null for the unsatisfied form.</summary>
        public Int64? Length
            => Start is not null && End is not null
                   ? End.Value - Start.Value + 1
                   : null;

        #endregion

        #region TryParse (Value, out ContentRange)

        /// <summary>
        /// Parse a <c>Content-Range</c> field value. Only the <c>bytes</c> unit is
        /// understood — any other unit, and any syntactic damage, yields false
        /// rather than a half-populated result: a client that cannot read where its
        /// bytes belong must not guess.
        /// </summary>
        public static Boolean TryParse(String Value, out HTTPContentRange? ContentRange)
        {

            ContentRange = null;

            var trimmed = Value.Trim();

            if (!trimmed.StartsWith("bytes ", StringComparison.OrdinalIgnoreCase))
                return false;

            var parts = trimmed[6..].Trim().Split('/');

            if (parts.Length != 2)
                return false;

            var range = parts[0].Trim();
            var total = parts[1].Trim();

            Int64? completeLength = null;

            if (total != "*")
            {
                if (!Int64.TryParse(total, out var parsedTotal) || parsedTotal < 0)
                    return false;
                completeLength = parsedTotal;
            }

            // The unsatisfied form: no range, only the current length.
            if (range == "*")
            {
                ContentRange = new HTTPContentRange(null, null, completeLength);
                return true;
            }

            var dash = range.IndexOf('-');

            if (dash <= 0)
                return false;

            if (!Int64.TryParse(range[..dash],       out var start) ||
                !Int64.TryParse(range[(dash + 1)..], out var end))
                return false;

            if (start < 0 || end < start)
                return false;

            // A range cannot extend past a stated complete length.
            if (completeLength is not null && end >= completeLength.Value)
                return false;

            ContentRange = new HTTPContentRange(start, end, completeLength);
            return true;

        }

        #endregion

        #region ToHeaderValue() / RequestFrom(FirstByte)

        /// <summary>This range as a <c>Content-Range</c> field value.</summary>
        public String ToHeaderValue()

            => IsUnsatisfied
                   ? $"bytes */{CompleteLength?.ToString() ?? "*"}"
                   : $"bytes {Start}-{End}/{CompleteLength?.ToString() ?? "*"}";

        /// <summary>
        /// A <c>Range</c> *request* field value asking for everything from
        /// <paramref name="FirstByte"/> onwards — the open-ended form a resume uses,
        /// since the client knows where it stopped but not where the resource ends.
        /// </summary>
        public static String RequestFrom(Int64 FirstByte)

            => $"bytes={FirstByte}-";

        #endregion

    }

}
