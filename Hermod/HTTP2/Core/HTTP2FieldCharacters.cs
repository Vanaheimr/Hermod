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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// The characters a field may carry in HTTP/2 (RFC 9113, Section 8.2.1).
    /// HPACK hands a field over as any octets at all, so these rules are what
    /// stands between a peer and a field HTTP/1.1 could not even have framed:
    ///
    ///   - A field name MUST NOT contain an octet from 0x00 to 0x20, from 0x41
    ///     to 0x5A, or from 0x7F to 0xFF, nor a colon, except for the one that
    ///     opens a pseudo-header field's name.
    ///   - A field value MUST NOT contain NUL, LF or CR, and MUST NOT start or
    ///     end with SP or HTAB.
    ///
    /// A message that carries such a field is malformed (Section 8.1.1). The
    /// rules are the same in both directions, which is why they sit in
    /// <c>Core</c>: the server holds a request's fields to them, the client a
    /// response's. Uppercase letters, the 0x41 to 0x5A of the first rule, are
    /// left to each side's own check, which runs first and names them as such.
    ///
    /// A field arrives as the HPACK decoder made it, one character per octet
    /// (<see cref="HPACKDecoder"/>), so a character here is an octet on the wire.
    /// </summary>
    public static class HTTP2FieldCharacters
    {

        #region TryFindViolation(Name, Value, out Violation)

        /// <summary>
        /// Whether a field breaks one of Section 8.2.1's rules on characters, with
        /// which one in <paramref name="Violation"/>, the field's characters
        /// outside 0x21 to 0x7E written as \xNN. A pseudo-header field's name is
        /// not checked here: it is one of a few known names, or the message is
        /// malformed for that already. Its value is.
        /// </summary>
        /// <param name="Name">The field's name, as decoded.</param>
        /// <param name="Value">The field's value, as decoded.</param>
        /// <param name="Violation">What is wrong with the field, or "" if nothing is.</param>
        public static Boolean TryFindViolation(String      Name,
                                               String      Value,
                                               out String  Violation)
        {

            if (Name.Length > 0 && Name[0] != ':')
            {
                foreach (var character in Name)
                {
                    if (character is <= ' ' or >= '\x7F' or ':')
                    {
                        Violation = $"field name '{Printable(Name)}' contains octet 0x{(Int32) character:X2}";
                        return true;
                    }
                }
            }

            foreach (var character in Value)
            {
                if (character is '\0' or '\n' or '\r')
                {
                    Violation = $"field value of '{Printable(Name)}' contains octet 0x{(Int32) character:X2}";
                    return true;
                }
            }

            if (Value.Length > 0 && Value[0] is ' ' or '\t')
            {
                Violation = $"field value of '{Printable(Name)}' starts with whitespace";
                return true;
            }

            if (Value.Length > 0 && Value[^1] is ' ' or '\t')
            {
                Violation = $"field value of '{Printable(Name)}' ends with whitespace";
                return true;
            }

            Violation = "";
            return false;

        }

        #endregion

        #region Printable(Text)

        /// <summary>
        /// The text with every character outside 0x21 to 0x7E, and the backslash,
        /// written as \xNN: what a peer put into a field must not put line breaks
        /// or control characters into a message or a log.
        /// </summary>
        public static String Printable(String Text)
        {

            var printable = new StringBuilder(Text.Length);

            foreach (var character in Text)
            {
                if (character is > ' ' and < '\x7F' and not '\\')
                    printable.Append(character);
                else
                    printable.Append($"\\x{(Int32) character:X2}");
            }

            return printable.ToString();

        }

        #endregion

    }

}
