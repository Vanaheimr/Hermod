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

using System.Globalization;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// SASLprep (RFC 4013), the stringprep profile (RFC 3454) for user names and passwords:
    /// SCRAM's Normalize(str) (RFC 5802 §2.2), and what PLAIN asks of its strings (RFC 4616
    /// §2). Unassigned code points are allowed, as in a query (RFC 4013 §2.5, RFC 3454 §7).
    /// </summary>
    public static class SaslPrep
    {

        #region Tables (RFC 3454 Appendix B, C, D)

        // B.1, "Commonly mapped to nothing".
        private static readonly (Int32 From, Int32 To)[] MappedToNothing = [
            (0x00AD, 0x00AD), (0x034F, 0x034F), (0x1806, 0x1806), (0x180B, 0x180D),
            (0x200B, 0x200D), (0x2060, 0x2060), (0xFE00, 0xFE0F), (0xFEFF, 0xFEFF)
        ];

        // C.1.2, "Non-ASCII space characters" - mapped to SPACE (RFC 4013 §2.1).
        private static readonly (Int32 From, Int32 To)[] NonAsciiSpace = [
            (0x00A0, 0x00A0), (0x1680, 0x1680), (0x2000, 0x200B), (0x202F, 0x202F),
            (0x205F, 0x205F), (0x3000, 0x3000)
        ];

        // RFC 4013 §2.3: C.1.2, C.2.1, C.2.2, C.3, C.4, C.5, C.6, C.7, C.8, C.9.
        private static readonly (Int32 From, Int32 To)[] Prohibited = [
            // C.1.2
            (0x00A0, 0x00A0), (0x1680, 0x1680), (0x2000, 0x200B), (0x202F, 0x202F), (0x205F, 0x205F), (0x3000, 0x3000),
            // C.2.1
            (0x0000, 0x001F), (0x007F, 0x007F),
            // C.2.2
            (0x0080, 0x009F), (0x06DD, 0x06DD), (0x070F, 0x070F), (0x180E, 0x180E), (0x200C, 0x200D), (0x2028, 0x2029),
            (0x2060, 0x2063), (0x206A, 0x206F), (0xFEFF, 0xFEFF), (0xFFF9, 0xFFFC), (0x1D173, 0x1D17A),
            // C.3
            (0xE000, 0xF8FF), (0xF0000, 0xFFFFD), (0x100000, 0x10FFFD),
            // C.4
            (0xFDD0, 0xFDEF), (0xFFFE, 0xFFFF),
            // C.5
            (0xD800, 0xDFFF),
            // C.6
            (0xFFF9, 0xFFFD),
            // C.7
            (0x2FF0, 0x2FFB),
            // C.8
            (0x0340, 0x0341), (0x200E, 0x200F), (0x202A, 0x202E), (0x206A, 0x206F),
            // C.9
            (0xE0001, 0xE0001), (0xE0020, 0xE007F)
        ];

        // D.1, "Characters with bidirectional property "R" or "AL"".
        private static readonly (Int32 From, Int32 To)[] RandALCat = [
            (0x05BE, 0x05BE), (0x05C0, 0x05C0), (0x05C3, 0x05C3), (0x05D0, 0x05EA), (0x05F0, 0x05F4),
            (0x061B, 0x061B), (0x061F, 0x061F), (0x0621, 0x063A), (0x0640, 0x064A), (0x066D, 0x066F),
            (0x0671, 0x06D5), (0x06DD, 0x06DD), (0x06E5, 0x06E6), (0x06FA, 0x06FE), (0x0700, 0x070D),
            (0x0710, 0x0710), (0x0712, 0x072C), (0x0780, 0x07A5), (0x07B1, 0x07B1), (0x200F, 0x200F),
            (0xFB1D, 0xFB1D), (0xFB1F, 0xFB28), (0xFB2A, 0xFB36), (0xFB38, 0xFB3C), (0xFB3E, 0xFB3E),
            (0xFB40, 0xFB41), (0xFB43, 0xFB44), (0xFB46, 0xFBB1), (0xFBD3, 0xFD3D), (0xFD50, 0xFD8F),
            (0xFD92, 0xFDC7), (0xFDF0, 0xFDFC), (0xFE70, 0xFE74), (0xFE76, 0xFEFC)
        ];

        private static Boolean In((Int32 From, Int32 To)[] Table, Int32 CodePoint)
            => Table.Any(range => CodePoint >= range.From && CodePoint <= range.To);

        // C.4 also has the last two code points of every plane.
        private static Boolean IsProhibited(Int32 CodePoint)
            => In(Prohibited, CodePoint) || (CodePoint & 0xFFFE) == 0xFFFE;

        // D.2, "Characters with bidirectional property "L"": the letters and spacing marks that
        // are not of D.1 - by their Unicode category, which is what the table is made of.
        private static Boolean IsLCat(Rune Rune)
            => !In(RandALCat, Rune.Value) &&
               Rune.GetUnicodeCategory(Rune) is UnicodeCategory.UppercaseLetter
                                              or UnicodeCategory.LowercaseLetter
                                              or UnicodeCategory.TitlecaseLetter
                                              or UnicodeCategory.ModifierLetter
                                              or UnicodeCategory.OtherLetter
                                              or UnicodeCategory.SpacingCombiningMark;

        #endregion


        #region Prepare(Value)

        /// <summary>
        /// The string SASLprep makes of a value (RFC 4013 §2): non-ASCII spaces mapped to SPACE,
        /// the characters "commonly mapped to nothing" removed, normalized with form KC, and
        /// checked for prohibited output and bidirectional characters. Null when the value is
        /// not one SASLprep allows: one with a prohibited character - a control character, a
        /// private use or non-character code point, a surrogate on its own - or right-to-left
        /// characters that break RFC 3454 §6.
        /// </summary>
        public static String? Prepare(String Value)
        {

            // §2.1, mapping - on code points; a surrogate without its partner is prohibited (C.5).
            var mapped = new StringBuilder(Value.Length);

            for (var i = 0; i < Value.Length; )
            {

                if (Rune.DecodeFromUtf16(Value.AsSpan(i), out var rune, out var length) != System.Buffers.OperationStatus.Done)
                    return null;

                i += length;

                if (In(NonAsciiSpace, rune.Value))
                    mapped.Append(' ');

                else if (!In(MappedToNothing, rune.Value))
                    mapped.Append(rune.ToString());

            }

            // §2.2, normalization: form KC.
            String normalized;

            try
            {
                normalized = mapped.ToString().Normalize(NormalizationForm.FormKC);
            }
            catch (ArgumentException)
            {
                return null;
            }

            // §2.3, prohibited output; §2.4, bidirectional characters (RFC 3454 §6).
            var runes = normalized.EnumerateRunes().ToArray();

            if (runes.Any(rune => IsProhibited(rune.Value)))
                return null;

            if (runes.Any(rune => In(RandALCat, rune.Value)) &&
                (runes.Any(IsLCat) ||
                 !In(RandALCat, runes[0].Value) ||
                 !In(RandALCat, runes[^1].Value)))
                return null;

            return normalized;

        }

        #endregion

        #region Normalize(Password)

        /// <summary>
        /// A password as SCRAM's Normalize(str) has it (RFC 5802 §2.2): prepared with SASLprep.
        /// A password SASLprep prohibits is used as it is: a client that keeps to the RFC cannot
        /// use it with SCRAM, but its owner still can with PLAIN, as before.
        /// </summary>
        public static String Normalize(String Password)
            => Prepare(Password) ?? Password;

        #endregion

    }

}
