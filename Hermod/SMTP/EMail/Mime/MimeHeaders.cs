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

using System.Collections;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// One header field, unfolded but otherwise as it arrived: its value is not
    /// decoded, because how to decode it depends on which header it is.
    /// </summary>
    /// <param name="Name">The field name as it was written.</param>
    /// <param name="Value">The field body, unfolded (RFC 5322 §2.2.3), without the white space around it.</param>
    public sealed record MimeHeader(String  Name,
                                    String  Value);


    /// <summary>
    /// The header fields of a message or a MIME part, in their order and with every
    /// field that occurs more than once - Received:, above all, whose order is the
    /// path the mail took.
    /// </summary>
    public sealed class MimeHeaders : IReadOnlyList<MimeHeader>
    {

        #region Data

        private readonly List<MimeHeader> headers;

        #endregion

        #region Constructor(s)

        private MimeHeaders(List<MimeHeader> Headers)
        {
            this.headers = Headers;
        }

        #endregion


        #region (static) Parse(HeaderBlock)

        /// <summary>
        /// Read a header block: one field per line, a line starting with white space
        /// continuing the field before it. A line without a colon that continues
        /// nothing is not a field and is passed over.
        /// </summary>
        /// <param name="HeaderBlock">The header block as text, lines ending in CR LF or LF.</param>
        public static MimeHeaders Parse(String HeaderBlock)
        {

            var headers  = new List<MimeHeader>();
            String?        name   = null;
            StringBuilder? value  = null;

            void Add()
            {
                if (name is not null && value is not null)
                    headers.Add(new MimeHeader(name, value.ToString().Trim()));
                name  = null;
                value = null;
            }

            foreach (var rawLine in HeaderBlock.Split('\n'))
            {

                var line = rawLine.TrimEnd('\r');

                if (line.Length == 0)
                    continue;

                // Unfolding is removing the line break and nothing else (RFC 5322
                // §2.2.3): the white space that starts the next line stays.
                if (line[0] is ' ' or '\t')
                {
                    value?.Append(line);
                    continue;
                }

                Add();

                var colon = line.IndexOf(':');

                if (colon <= 0)
                    continue;

                name  = line[..colon].Trim();
                value = new StringBuilder(line[(colon + 1)..]);

            }

            Add();

            return new MimeHeaders(headers);

        }

        #endregion

        #region Empty

        /// <summary>
        /// No header fields.
        /// </summary>
        public static MimeHeaders Empty
            => new ([]);

        #endregion


        #region Get(Name) / GetAll(Name) / GetText(Name)

        /// <summary>
        /// The value of the first field of the given name, or null.
        /// </summary>
        public String? Get(String Name)
            => headers.FirstOrDefault(header => header.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))?.Value;

        /// <summary>
        /// The values of every field of the given name, in their order.
        /// </summary>
        public IEnumerable<String> GetAll(String Name)
            => headers.Where (header => header.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)).
                       Select(header => header.Value);

        /// <summary>
        /// The value of the first field of the given name as text: its encoded words
        /// decoded (RFC 2047) - for unstructured fields like Subject - or null.
        /// </summary>
        public String? GetText(String Name)
            => Get(Name) is { } value
                   ? EncodedWords.Decode(value)
                   : null;

        #endregion

        #region IReadOnlyList

        public MimeHeader this[Int32 Index]
            => headers[Index];

        public Int32 Count
            => headers.Count;

        public IEnumerator<MimeHeader> GetEnumerator()
            => headers.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => headers.GetEnumerator();

        #endregion

    }

}
