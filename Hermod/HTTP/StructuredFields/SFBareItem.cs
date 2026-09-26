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
    using System.Globalization;

    /// <summary>
    /// One bare item of a structured field (RFC 9651, Section 3.3): an integer,
    /// a decimal, a string, a token, a byte sequence, a boolean, a date or a
    /// display string.
    ///
    /// The type is carried explicitly rather than inferred, because in this
    /// grammar a token and a string are different things that can hold the same
    /// characters, and a field's definition says which one it wants. Reading
    /// "foo" as if it were foo is how a field that specifies one ends up
    /// accepting both.
    ///
    /// Serialization is exact by construction: the value keeps the type it was
    /// parsed as, so re-serializing produces the same octets. That matters for
    /// anything that has to agree with the sender about the bytes - the reason
    /// this is worth having at all is fields like RFC 9530 digests and RFC 9421
    /// signatures, where a re-serialization that differs is a verification that
    /// fails.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9651.html#section-3.3"/>
    public readonly struct SFBareItem
    {

        #region Data

        /// <summary>
        /// The largest magnitude an sf-integer may have (RFC 9651, Section 3.3.1).
        /// </summary>
        public const Int64 MaxInteger = 999_999_999_999_999;

        private readonly Int64    number;      // Integer, Date
        private readonly Decimal  fraction;    // Decimal
        private readonly String?  text;        // String, Token, DisplayString
        private readonly Byte[]?  bytes;       // ByteSequence
        private readonly Boolean  flag;        // Boolean

        #endregion

        #region Properties

        /// <summary>
        /// Which of the eight bare types this is.
        /// </summary>
        public SFType Type { get; }


        /// <summary>
        /// The value as an integer.
        /// </summary>
        public Int64 AsInteger

            => Type == SFType.Integer
                   ? number
                   : throw new InvalidOperationException($"This structured field item is a {Type}, not an Integer!");

        /// <summary>
        /// The value as a decimal.
        /// </summary>
        public Decimal AsDecimal

            => Type == SFType.Decimal
                   ? fraction
                   : throw new InvalidOperationException($"This structured field item is a {Type}, not a Decimal!");

        /// <summary>
        /// The value as a string.
        /// </summary>
        public String AsString

            => Type == SFType.String
                   ? text!
                   : throw new InvalidOperationException($"This structured field item is a {Type}, not a String!");

        /// <summary>
        /// The value as a token.
        /// </summary>
        public String AsToken

            => Type == SFType.Token
                   ? text!
                   : throw new InvalidOperationException($"This structured field item is a {Type}, not a Token!");

        /// <summary>
        /// The value as a byte sequence.
        /// </summary>
        public Byte[] AsByteSequence

            => Type == SFType.ByteSequence
                   ? bytes!
                   : throw new InvalidOperationException($"This structured field item is a {Type}, not a Byte Sequence!");

        /// <summary>
        /// The value as a boolean.
        /// </summary>
        public Boolean AsBoolean

            => Type == SFType.Boolean
                   ? flag
                   : throw new InvalidOperationException($"This structured field item is a {Type}, not a Boolean!");

        /// <summary>
        /// A date as the seconds since the Unix epoch it is written as.
        /// </summary>
        /// <remarks>
        /// The grammar allows a date far outside what DateTimeOffset can hold -
        /// an sf-integer reaches about 31 million years - so the raw seconds are
        /// what this type keeps, and <see cref="AsDate"/> is the conversion that
        /// may refuse.
        /// </remarks>
        public Int64 AsDateSeconds

            => Type == SFType.Date
                   ? number
                   : throw new InvalidOperationException($"This structured field item is a {Type}, not a Date!");

        /// <summary>
        /// The value as a date.
        /// </summary>
        public DateTimeOffset AsDate

            => DateTimeOffset.FromUnixTimeSeconds(AsDateSeconds);

        /// <summary>
        /// The value as a display string.
        /// </summary>
        public String AsDisplayString

            => Type == SFType.DisplayString
                   ? text!
                   : throw new InvalidOperationException($"This structured field item is a {Type}, not a Display String!");

        #endregion

        #region Constructor(s)

        private SFBareItem(SFType    Type,
                           Int64     Number     = 0,
                           Decimal   Fraction   = 0,
                           String?   Text       = null,
                           Byte[]?   Bytes      = null,
                           Boolean   Flag       = false)
        {

            this.Type      = Type;
            this.number    = Number;
            this.fraction  = Fraction;
            this.text      = Text;
            this.bytes     = Bytes;
            this.flag      = Flag;

        }

        #endregion


        #region (static) Integer      (Value)

        /// <summary>
        /// An integer item.
        /// </summary>
        public static SFBareItem Integer(Int64 Value)

            => Math.Abs(Value) <= MaxInteger
                   ? new (SFType.Integer, Number: Value)
                   : throw new ArgumentOutOfRangeException(nameof(Value), $"An sf-integer is limited to +/- {MaxInteger}!");

        #endregion

        #region (static) Decimal      (Value)

        /// <summary>
        /// A decimal item, rounded to three fractional digits.
        /// </summary>
        public static SFBareItem Decimal(Decimal Value)
        {

            // RFC 9651, Section 4.1.5: round to three fractional digits, ties to
            // even. Done here rather than at serialization time so that what the
            // item holds is what it will render as.
            var rounded = Math.Round(Value, 3, MidpointRounding.ToEven);

            if (Math.Abs(Math.Truncate(rounded)) > 999_999_999_999m)
                throw new ArgumentOutOfRangeException(nameof(Value), "An sf-decimal is limited to twelve integer digits!");

            return new (SFType.Decimal, Fraction: rounded);

        }

        #endregion

        #region (static) String       (Value)

        /// <summary>
        /// A string item. Only printable ASCII may be sent as one; anything else
        /// belongs in a display string.
        /// </summary>
        public static SFBareItem String(String Value)

            => Value.All(character => character is >= ' ' and <= '~')
                   ? new (SFType.String, Text: Value)
                   : throw new ArgumentException("An sf-string holds printable ASCII only - use a display string!", nameof(Value));

        #endregion

        #region (static) Token        (Value)

        /// <summary>
        /// A token item.
        /// </summary>
        public static SFBareItem Token(String Value)

            => IsToken(Value)
                   ? new (SFType.Token, Text: Value)
                   : throw new ArgumentException($"'{Value}' is not a valid sf-token!", nameof(Value));

        #endregion

        #region (static) ByteSequence (Value)

        /// <summary>
        /// A byte sequence item.
        /// </summary>
        public static SFBareItem ByteSequence(Byte[] Value)

            => new (SFType.ByteSequence, Bytes: Value);

        #endregion

        #region (static) Boolean      (Value)

        /// <summary>
        /// A boolean item.
        /// </summary>
        public static SFBareItem Boolean(Boolean Value)

            => new (SFType.Boolean, Flag: Value);

        #endregion

        #region (static) Date         (Value)

        /// <summary>
        /// A date item.
        /// </summary>
        public static SFBareItem Date(DateTimeOffset Value)

            => Date(Value.ToUnixTimeSeconds());

        /// <summary>
        /// A date item, as seconds since the Unix epoch.
        /// </summary>
        public static SFBareItem Date(Int64 UnixSeconds)

            => Math.Abs(UnixSeconds) <= MaxInteger
                   ? new (SFType.Date, Number: UnixSeconds)
                   : throw new ArgumentOutOfRangeException(nameof(UnixSeconds), $"An sf-date is limited to +/- {MaxInteger} seconds!");

        #endregion

        #region (static) DisplayString(Value)

        /// <summary>
        /// A display string item, which is the only bare item that may hold
        /// anything outside printable ASCII.
        /// </summary>
        public static SFBareItem DisplayString(String Value)

            => new (SFType.DisplayString, Text: Value);

        #endregion


        #region (internal static) IsToken(Text)

        /// <summary>
        /// RFC 9651, Section 3.3.4: sf-token = ( ALPHA / "*" ) *( tchar / ":" / "/" )
        /// </summary>
        internal static Boolean IsToken(String Text)
        {

            if (Text.Length == 0)
                return false;

            if (!Char.IsAsciiLetter(Text[0]) && Text[0] != '*')
                return false;

            foreach (var character in Text.Skip(1))
            {
                if (!IsTokenCharacter(character))
                    return false;
            }

            return true;

        }

        internal static Boolean IsTokenCharacter(Char Character)

            => Char.IsAsciiLetterOrDigit(Character) ||
               Character is ':' or '/' or
                            '!' or '#' or '$' or '%' or '&' or '\'' or '*' or
                            '+' or '-' or '.' or '^' or '_' or '`'  or '|' or '~';

        #endregion


        #region ToString()

        /// <summary>
        /// Serialize this bare item (RFC 9651, Section 4.1.3).
        /// </summary>
        public override String ToString()

            => Type switch {

                   SFType.Integer       => number.ToString(CultureInfo.InvariantCulture),

                   // "1.0", never "1" - an sf-decimal must show a fractional
                   // digit, or it would read back as an integer.
                   SFType.Decimal       => fraction.ToString("0.0##", CultureInfo.InvariantCulture),

                   SFType.String        => $"\"{text!.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",

                   SFType.Token         => text!,

                   SFType.ByteSequence  => $":{Convert.ToBase64String(bytes!)}:",

                   SFType.Boolean       => flag ? "?1" : "?0",

                   SFType.Date          => $"@{number.ToString(CultureInfo.InvariantCulture)}",

                   SFType.DisplayString => $"%\"{PercentEncode(text!)}\"",

                   _                    => throw new InvalidOperationException($"Unknown structured field type '{Type}'!")

               };

        #endregion

        #region (private static) PercentEncode(Text)

        /// <summary>
        /// RFC 9651, Section 4.1.10: UTF-8, with everything that is not
        /// printable ASCII - and the two delimiters - written as "%" and two
        /// *lower-case* hexadecimal digits.
        /// </summary>
        private static String PercentEncode(String Text)
        {

            var encoded = new StringBuilder();

            foreach (var octet in Encoding.UTF8.GetBytes(Text))
            {

                if (octet is (Byte) '%' or (Byte) '"' ||
                    octet < 0x20 || octet > 0x7E)
                    encoded.Append('%').Append(octet.ToString("x2", CultureInfo.InvariantCulture));

                else
                    encoded.Append((Char) octet);

            }

            return encoded.ToString();

        }

        #endregion

    }

}
