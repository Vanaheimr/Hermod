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
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// RFC 2047 encoded words - "=?UTF-8?B?R3LDvMOfZQ==?=", "=?iso-8859-1?Q?Gr=FC=DFe?=" -
    /// the way a header that is not ASCII is written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Decoding follows RFC 2047 where it is clear and the mail programs where it
    /// is not: white space between two encoded words is dropped (§6.2), and
    /// adjacent words in the same charset are decoded together, because encoders
    /// split a multi-byte character across two words more often than they should.
    /// The language suffix of RFC 2231 §5 ("=?UTF-8*de?Q?...?=") is accepted.
    /// </para>
    /// <para>
    /// A word that cannot be decoded - an unknown encoding, broken base64 - is left
    /// as it is: what the sender wrote, rather than nothing.
    /// </para>
    /// </remarks>
    public static class EncodedWords
    {

        #region Data

        private static readonly Regex Word = new (@"=\?([^?\s]+)\?([bBqQ])\?([^?]*)\?=", RegexOptions.CultureInvariant);

        #endregion


        #region Decode(Text)

        /// <summary>
        /// The given header text with its encoded words decoded.
        /// </summary>
        /// <param name="Text">An unstructured header value, a display name or a parameter value.</param>
        public static String Decode(String? Text)
        {

            if (String.IsNullOrEmpty(Text) || !Text.Contains("=?", StringComparison.Ordinal))
                return Text ?? "";

            var result          = new StringBuilder(Text.Length);
            var position        = 0;
            var afterWord       = false;
            String? charset     = null;
            var pending         = new List<Byte>();

            void Flush()
            {
                if (pending.Count > 0)
                    result.Append(MimeCharsets.Decode([.. pending], charset));
                pending.Clear();
                charset = null;
            }

            foreach (Match match in Word.Matches(Text))
            {

                var between = Text[position..match.Index];

                // RFC 2047 §6.2: white space between two encoded words is not shown.
                if (!(afterWord && String.IsNullOrWhiteSpace(between)))
                {
                    Flush();
                    result.Append(between);
                }

                var wordCharset = match.Groups[1].Value;
                var star        = wordCharset.IndexOf('*');

                if (star >= 0)
                    wordCharset = wordCharset[..star];

                var octets      = match.Groups[2].Value is "B" or "b"
                                      ? DecodeBase64(match.Groups[3].Value)
                                      : DecodeQ     (match.Groups[3].Value);

                if (octets is null)
                {
                    Flush();
                    result.Append(match.Value);
                    afterWord  = false;
                    position   = match.Index + match.Length;
                    continue;
                }

                if (charset is not null && !charset.Equals(wordCharset, StringComparison.OrdinalIgnoreCase))
                    Flush();

                charset    = wordCharset;
                pending.AddRange(octets);

                afterWord  = true;
                position   = match.Index + match.Length;

            }

            Flush();
            result.Append(Text[position..]);

            return result.ToString();

        }

        #endregion

        #region (private) DecodeBase64(Text) / DecodeQ(Text)

        private static Byte[]? DecodeBase64(String Text)
        {

            var cleaned = Text.Trim().TrimEnd('=');

            if (cleaned.Length % 4 == 1)
                return null;

            cleaned = cleaned.PadRight(cleaned.Length + (4 - cleaned.Length % 4) % 4, '=');

            try
            {
                return Convert.FromBase64String(cleaned);
            }
            catch (FormatException)
            {
                return null;
            }

        }

        private static Byte[] DecodeQ(String Text)
        {

            var octets = new List<Byte>(Text.Length);

            for (var i = 0; i < Text.Length; i++)
            {

                var c = Text[i];

                if (c == '_')
                    octets.Add(0x20);

                else if (c == '=' && i + 2 < Text.Length && Uri.IsHexDigit(Text[i + 1]) && Uri.IsHexDigit(Text[i + 2]))
                {
                    octets.Add(Convert.ToByte(Text.Substring(i + 1, 2), 16));
                    i += 2;
                }

                // Not allowed in a Q word, and still sent: take the character as it is.
                else if (c > 0x7F)
                    octets.AddRange(Encoding.UTF8.GetBytes(c.ToString()));

                else
                    octets.Add((Byte) c);

            }

            return [.. octets];

        }

        #endregion

    }

}
