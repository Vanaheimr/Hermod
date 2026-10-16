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
    /// A message as octets, where text would lose some: an 8-bit body that is not
    /// UTF-8 - ISO-8859-1, Windows-1252 - decoded as UTF-8 comes out as U+FFFD,
    /// and neither its DKIM signature nor the message itself survives that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Where the code works on text - DKIM canonicalization, finding the header
    /// block - the octets are read as Latin-1: one character per octet, every
    /// octet one character, and back to the very same octets. Everything that is
    /// done to the text there - line breaks, white space, header names - is ASCII,
    /// so the round trip is exact.
    /// </para>
    /// <para>
    /// UTF-8 is not touched by it either: its octets go through as they came.
    /// </para>
    /// </remarks>
    public static class MessageOctets
    {

        #region ToText(Octets) / ToOctets(Text)

        /// <summary>
        /// The octets as text, one character per octet (Latin-1).
        /// </summary>
        public static String ToText(ReadOnlySpan<Byte> Octets)
            => Encoding.Latin1.GetString(Octets);

        /// <summary>
        /// Text made by ToText back into its octets.
        /// </summary>
        public static Byte[] ToOctets(String Text)
            => Encoding.Latin1.GetBytes(Text);

        #endregion

        #region Has8Bit(Octets)

        /// <summary>
        /// Whether an octet is above 127 - the message needs 8BITMIME.
        /// </summary>
        public static Boolean Has8Bit(ReadOnlySpan<Byte> Octets)
        {

            foreach (var octet in Octets)
                if (octet > 0x7F)
                    return true;

            return false;

        }

        #endregion

        #region Header(Octets)

        /// <summary>
        /// The header block of the message, up to the empty line.
        /// </summary>
        public static ReadOnlySpan<Byte> Header(ReadOnlySpan<Byte> Octets)
        {

            var end = Octets.IndexOf("\r\n\r\n"u8);

            if (end < 0)
                end = Octets.IndexOf("\n\n"u8);

            return end < 0 ? Octets : Octets[..end];

        }

        #endregion

        #region Data(Octets)

        /// <summary>
        /// The message as DATA sends it (RFC 5321 §4.5.2): its lines - whatever line
        /// break they came with - ending in CRLF, a line that begins with a dot given
        /// another, and the final dot. The octets of each line as they are.
        /// </summary>
        public static Byte[] Data(ReadOnlySpan<Byte> Octets)
        {

            using var output = new MemoryStream(Octets.Length + Octets.Length / 64 + 8);
            var start = 0;

            void Line(ReadOnlySpan<Byte> Text)
            {
                if (Text.Length > 0 && Text[0] == (Byte) '.')
                    output.WriteByte((Byte) '.');
                output.Write(Text);
                output.Write("\r\n"u8);
            }

            for (var i = 0; i < Octets.Length; i++)
            {

                if (Octets[i] is not ((Byte) '\r' or (Byte) '\n'))
                    continue;

                Line(Octets[start..i]);

                if (Octets[i] == '\r' && i + 1 < Octets.Length && Octets[i + 1] == '\n')
                    i++;

                start = i + 1;

            }

            if (start < Octets.Length)
                Line(Octets[start..]);

            output.Write(".\r\n"u8);

            return output.ToArray();

        }

        #endregion

    }

}
