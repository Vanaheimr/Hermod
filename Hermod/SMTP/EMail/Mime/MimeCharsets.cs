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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// Turning the octets of a mail into text: by the charset it declares, and
    /// sensibly when it declares none, a wrong one, or one nobody knows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The code pages beyond the few .NET knows by itself (ISO-8859-x, Windows-125x,
    /// KOI8-R, Shift_JIS, ...) are registered once, here.
    /// </para>
    /// <para>
    /// The guesses follow what mail programs and browsers learnt the hard way
    /// (WHATWG Encoding): "iso-8859-1" and "us-ascii" are read as Windows-1252,
    /// which agrees with them on every octet they define and gives the rest - the
    /// curly quotes of a Windows mail program - a meaning; text without a charset is
    /// UTF-8 when it is valid UTF-8 and Windows-1252 otherwise; and text that says
    /// UTF-8 but is not is read as Windows-1252 too, because every octet then still
    /// is a character instead of a U+FFFD.
    /// </para>
    /// </remarks>
    public static class MimeCharsets
    {

        #region Data

        private static readonly UTF8Encoding  strictUTF8  = new (encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        private static readonly Encoding      windows1252;

        #endregion

        #region (static) MimeCharsets()

        static MimeCharsets()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            windows1252 = Encoding.GetEncoding(1252);
        }

        #endregion


        #region Windows1252

        /// <summary>
        /// Windows-1252, the fallback for octets of unknown meaning.
        /// </summary>
        public static Encoding Windows1252
            => windows1252;

        #endregion

        #region Find(Charset)

        /// <summary>
        /// The encoding for the given charset name, or null when it is unknown -
        /// or when it is "us-ascii", which promises 7 bits and is so often wrong
        /// that it is better treated as no promise at all.
        /// </summary>
        /// <param name="Charset">A charset name as a Content-Type or an encoded word gives it.</param>
        public static Encoding? Find(String? Charset)
        {

            if (String.IsNullOrWhiteSpace(Charset))
                return null;

            var name = Charset.Trim().Trim('"', '\'').ToLowerInvariant();

            switch (name)
            {

                case "us-ascii":
                case "ascii":
                case "ansi_x3.4-1968":
                case "iso646-us":
                case "unknown-8bit":
                case "x-unknown":
                case "default":
                    return null;

                case "utf-8":
                case "utf8":
                    return Encoding.UTF8;

                case "iso-8859-1":
                case "iso8859-1":
                case "iso_8859-1":
                case "latin1":
                case "latin-1":
                case "l1":
                case "cp819":
                case "windows-1252":
                case "cp1252":
                    return windows1252;

                // What GB2312 mail actually contains is GBK; GB18030 reads both.
                case "gb2312":
                case "gbk":
                case "x-gbk":
                    name = "gb18030";
                    break;

                case "iso-8859-8-i":
                    name = "iso-8859-8";
                    break;

                case "ks_c_5601-1987":
                    name = "euc-kr";
                    break;

            }

            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }

        }

        #endregion

        #region Decode(Octets, Charset)

        /// <summary>
        /// The given octets as text in the given charset - or, when the charset is
        /// unknown or wrong, as UTF-8 when they are valid UTF-8 and Windows-1252
        /// otherwise.
        /// </summary>
        /// <param name="Octets">The octets.</param>
        /// <param name="Charset">The charset they claim to be in, or null.</param>
        public static String Decode(ReadOnlySpan<Byte>  Octets,
                                    String?             Charset)
        {

            var encoding = Find(Charset);

            if (encoding is null || encoding.CodePage == Encoding.UTF8.CodePage)
                return DecodeUnknown(Octets);

            try
            {
                return encoding.GetString(Octets);
            }
            catch (DecoderFallbackException)
            {
                return DecodeUnknown(Octets);
            }

        }

        #endregion

        #region DecodeUnknown(Octets)

        /// <summary>
        /// Octets of unknown charset as text: UTF-8 when they are valid UTF-8 (which
        /// includes plain ASCII), Windows-1252 otherwise.
        /// </summary>
        public static String DecodeUnknown(ReadOnlySpan<Byte> Octets)
        {

            try
            {
                return strictUTF8.GetString(Octets);
            }
            catch (DecoderFallbackException)
            {
                return windows1252.GetString(Octets);
            }

        }

        #endregion

    }

}
