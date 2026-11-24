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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The base64 of SASL challenges and responses in SMTP (RFC 4954 §4, §8): "servers and
    /// clients MUST reject (and not ignore) any character not explicitly allowed by the BASE64
    /// alphabet, and MUST reject any sequence of BASE64 characters that contains the pad
    /// character ('=') anywhere other than the end of the string". Convert.FromBase64String
    /// skips white space - a space, a tab, a CR or an LF anywhere - which this does not.
    /// </summary>
    public static class SaslBase64
    {

        /// <summary>
        /// The octets of a challenge or response, or null when it is no base64 as RFC 4954 has
        /// it: the alphabet only, groups of four, the pad character at the end only. The empty
        /// text, and a single "=" (§4: an initial response "present, but contains no data"),
        /// are no octets.
        /// </summary>
        /// <param name="Text">A challenge or response, as it came.</param>
        public static Byte[]? Decode(String Text)
        {

            if (Text.Length == 0 || Text == "=")
                return [];

            if (Text.Length % 4 != 0)
                return null;

            var pad = Text.EndsWith("==", StringComparison.Ordinal) ? 2
                    : Text.EndsWith('=')                            ? 1
                    : 0;

            for (var i = 0; i < Text.Length - pad; i++)
                if (!IsAlphabet(Text[i]))
                    return null;

            try
            {
                return Convert.FromBase64String(Text);
            }
            catch (FormatException)
            {
                return null;
            }

        }

        /// <summary>
        /// Whether a character is one of the 64 of the base64 alphabet (RFC 4648 §4).
        /// </summary>
        private static Boolean IsAlphabet(Char Character)

            => Character is (>= 'A' and <= 'Z')
                         or (>= 'a' and <= 'z')
                         or (>= '0' and <= '9')
                         or '+'
                         or '/';

    }

}
