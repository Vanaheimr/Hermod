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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    using System.Text;
    using System.Diagnostics.CodeAnalysis;


    /// <summary>
    /// Extended parameter values (RFC 8187): a header field parameter in a
    /// character encoding other than ASCII, with optional language information.
    /// The form is charset'language'value, with the value percent-encoded, as in
    /// <c>title*=UTF-8'de'n%C3%A4chstes%20Kapitel</c>. A parameter whose name
    /// ends in an asterisk carries one.
    ///
    /// Producers MUST use UTF-8 (Section 3.2.1), and <see cref="Encode"/> does.
    /// Recipients MUST support UTF-8 and are encouraged to support ISO-8859-1,
    /// which RFC 5987 required (Section 3.2.2). <see cref="TryDecode"/> reads
    /// both, and refuses any other character encoding rather than guess at it.
    /// It also refuses a malformed percent escape, a character outside attr-char
    /// and an octet sequence the encoding cannot decode. Section 3.2.1 lets a
    /// recipient ignore such a parameter, and that is what a refusal leads to.
    /// </summary>
    public static class HTTPExtendedValue
    {

        #region (private static) IsAttrChar(Character)

        /// <summary>
        /// attr-char: a token character except "*", "'" and "%".
        /// </summary>
        private static Boolean IsAttrChar(Char Character)

            => Character is >= 'a' and <= 'z'
                         or >= 'A' and <= 'Z'
                         or >= '0' and <= '9'
                         or '!' or '#' or '$' or '&' or '+' or '-' or '.'
                         or '^' or '_' or '`' or '|' or '~';

        #endregion

        #region (static) IsLanguageTag(Text)

        /// <summary>
        /// Whether the text has the shape of a language tag: subtags of letters
        /// and digits, separated by hyphens, the first of them letters only
        /// (RFC 5646, Section 2.1). A shape check, not a registry lookup.
        /// </summary>
        public static Boolean IsLanguageTag(String Text)
        {

            if (Text.Length == 0)
                return false;

            var subtags = Text.Split('-');

            if (!subtags[0].All(Char.IsAsciiLetter))
                return false;

            return subtags.All(subtag => subtag.Length is > 0 and <= 8 &&
                                         subtag.All(Char.IsAsciiLetterOrDigit));

        }

        #endregion


        #region (static) Encode    (Value, Language = null)

        /// <summary>
        /// The ext-value for a text: UTF-8, the language if one is given, and
        /// every octet that is not an attr-char percent-encoded.
        /// </summary>
        /// <param name="Value">The text.</param>
        /// <param name="Language">An optional language tag, e.g. "de".</param>
        public static String Encode(String   Value,
                                    String?  Language   = null)
        {

            if (Language is not null && !IsLanguageTag(Language))
                throw new ArgumentException($"'{Language}' is not a language tag.", nameof(Language));

            var text = new StringBuilder("UTF-8'").Append(Language).Append('\'');

            foreach (var octet in Encoding.UTF8.GetBytes(Value))
            {
                if (octet < 0x80 && IsAttrChar((Char) octet))
                    text.Append((Char) octet);
                else
                    text.Append('%').Append(octet.ToString("X2"));
            }

            return text.ToString();

        }

        #endregion

        #region (static) TryDecode (Text, out Value, out Language)

        /// <summary>
        /// Decode an ext-value.
        /// </summary>
        /// <param name="Text">The ext-value, e.g. "UTF-8'de'n%C3%A4chstes%20Kapitel".</param>
        /// <param name="Value">The decoded text.</param>
        /// <param name="Language">The language tag, or null when the ext-value names none.</param>
        public static Boolean TryDecode(String                           Text,
                                        [NotNullWhen(true)] out String?  Value,
                                        out String?                      Language)
        {

            Value     = null;
            Language  = null;

            var first   = Text.IndexOf('\'');
            var second  = first < 0 ? -1 : Text.IndexOf('\'', first + 1);

            // The character encoding is required (Section 3.2).
            if (first < 1 || second < 0)
                return false;

            var charset   = Text[..first];
            var language  = Text[(first + 1)..second];
            var chars     = Text[(second + 1)..];

            Encoding encoding;

            if (charset.Equals("UTF-8", StringComparison.OrdinalIgnoreCase))
                encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false,
                                            throwOnInvalidBytes:            true);

            else if (charset.Equals("ISO-8859-1", StringComparison.OrdinalIgnoreCase))
                encoding = Encoding.Latin1;

            else
                return false;

            if (language.Length > 0 && !IsLanguageTag(language))
                return false;

            var octets = new List<Byte>(chars.Length);

            for (var i = 0; i < chars.Length; i++)
            {

                var character = chars[i];

                if (character == '%')
                {

                    if (i + 2 >= chars.Length ||
                        !Uri.IsHexDigit(chars[i + 1]) ||
                        !Uri.IsHexDigit(chars[i + 2]))
                    {
                        return false;
                    }

                    octets.Add((Byte) ((Uri.FromHex(chars[i + 1]) << 4) | Uri.FromHex(chars[i + 2])));
                    i += 2;

                }

                else if (character < 0x80 && IsAttrChar(character))
                    octets.Add((Byte) character);

                else
                    return false;

            }

            try
            {
                Value = encoding.GetString([.. octets]);
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            Language = language.Length > 0
                           ? language
                           : null;

            return true;

        }

        #endregion

    }

}
