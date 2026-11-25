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

using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// A header field Hermod writes itself, of a value it was told - by a client, by DNS, by a
    /// message: the store's fields on top of a message, the fields of a feedback report.
    /// </summary>
    internal static class HeaderField
    {

        /// <summary>
        /// The most octets of a header line, CR LF aside (RFC 5322 §2.1.1).
        /// </summary>
        internal const Int32 MaxLineOctets = 998;

        private const Char LineSeparator       = (Char) 0x2028;
        private const Char ParagraphSeparator  = (Char) 0x2029;

        /// <summary>
        /// A header field, CR LF and all. RFC 5322 §2.2: "A field body MUST NOT include CR and LF
        /// except when used in 'folding' and 'unfolding'" - a value from DNS or from a message
        /// may have line breaks and other control characters in it: each run of them is a space,
        /// so that it makes no field of its own and does not end the header. And §2.1.1: "Each
        /// line of characters MUST be no more than 998 characters" - a longer value is folded
        /// before white space in it.
        /// </summary>
        internal static String Of(String Name, String Value)
        {

            var body    = new StringBuilder(Value.Length);
            var inBreak = false;

            foreach (var c in Value)
            {

                if (Char.IsControl(c) && c != '\t' || c is LineSeparator or ParagraphSeparator)
                {
                    if (!inBreak)
                        body.Append(' ');
                    inBreak = true;
                    continue;
                }

                body.Append(c);
                inBreak = false;

            }

            var field  = new StringBuilder();
            var line   = new StringBuilder(Name).Append(':');
            var words  = body.ToString().Split(' ');

            foreach (var word in words)
            {

                // Folding puts CR LF before the white space; the next line begins with it.
                if (line.Length > Name.Length + 1 &&
                    Encoding.UTF8.GetByteCount(line.ToString()) + 1 + Encoding.UTF8.GetByteCount(word) > MaxLineOctets)
                {
                    field.Append(line).Append("\r\n");
                    line.Clear();
                }

                line.Append(' ').Append(word);

            }

            return field.Append(line).Append("\r\n").ToString();

        }

    }

}
