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
    using System.Diagnostics.CodeAnalysis;

    using org.GraphDefined.Vanaheimr.Illias;

    /// <summary>
    /// Structured Field Values for HTTP (RFC 9651, which obsoletes RFC 8941):
    /// the three top-level types - List, Dictionary and Item - and the eight
    /// bare types underneath them.
    ///
    /// This is the grammar that most HTTP fields defined since 2021 are written
    /// in, which is why it is worth having as one implementation rather than as
    /// a hand-rolled split(',') per field: RFC 9530 digests, RFC 9218 priority,
    /// RFC 9211 cache status, Client Hints, and - the demanding one - RFC 9421
    /// message signatures, where the signature base must be byte-identical on
    /// both ends and a re-serialization that differs by one space is a
    /// verification that fails.
    ///
    /// Parsing follows Section 4.2 step by step, including the parts that look
    /// pedantic: a trailing comma fails, a repeated key keeps the position of
    /// the first and the value of the last, an integer of sixteen digits fails
    /// rather than saturating, and a display string that is not valid UTF-8
    /// fails rather than producing replacement characters. Being liberal here
    /// is how two implementations come to disagree about what a field said.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9651.html"/>
    public static class StructuredFields
    {

        #region (static) IsKey(Text)

        /// <summary>
        /// RFC 9651, Section 3.1.2:
        ///
        ///    key = ( lcalpha / "*" ) *( lcalpha / DIGIT / "_" / "-" / "." / "*" )
        ///
        /// Lower case only - an upper-case key is not a key that needs
        /// normalizing, it is a syntax error.
        /// </summary>
        public static Boolean IsKey(String Text)
        {

            if (Text is null || Text.Length == 0)
                return false;

            if (!IsLowerAlpha(Text[0]) && Text[0] != '*')
                return false;

            foreach (var character in Text.Skip(1))
            {
                if (!IsLowerAlpha(character) &&
                    !Char.IsAsciiDigit(character) &&
                     character is not ('_' or '-' or '.' or '*'))
                    return false;
            }

            return true;

        }

        private static Boolean IsLowerAlpha(Char Character)

            => Character is >= 'a' and <= 'z';

        #endregion


        #region (static) TryParseList      (Text, out Members)

        /// <summary>
        /// Parse a field value as a List (RFC 9651, Section 4.2.1).
        /// </summary>
        public static Boolean TryParseList(String                                            Text,
                                           [NotNullWhen(true)] out IReadOnlyList<SFMember>?   Members)
        {

            Members = null;

            if (Text is null)
                return false;

            var parser = new Parser(Text);

            parser.SkipSpaces();

            if (!parser.TryParseList(out var members))
                return false;

            parser.SkipSpaces();

            if (!parser.AtEnd)
                return false;

            Members = members;
            return true;

        }

        #endregion

        #region (static) TryParseDictionary(Text, out Dictionary)

        /// <summary>
        /// Parse a field value as a Dictionary (RFC 9651, Section 4.2.2).
        /// </summary>
        public static Boolean TryParseDictionary(String                                 Text,
                                                 [NotNullWhen(true)] out SFDictionary?   Dictionary)
        {

            Dictionary = null;

            if (Text is null)
                return false;

            var parser = new Parser(Text);

            parser.SkipSpaces();

            if (!parser.TryParseDictionary(out var dictionary))
                return false;

            parser.SkipSpaces();

            if (!parser.AtEnd)
                return false;

            Dictionary = dictionary;
            return true;

        }

        #endregion

        #region (static) TryParseItem      (Text, out Item)

        /// <summary>
        /// Parse a field value as a single Item (RFC 9651, Section 4.2.3).
        /// </summary>
        public static Boolean TryParseItem(String                           Text,
                                           [NotNullWhen(true)] out SFItem?   Item)
        {

            Item = null;

            if (Text is null)
                return false;

            var parser = new Parser(Text);

            parser.SkipSpaces();

            if (!parser.TryParseItem(out var item))
                return false;

            parser.SkipSpaces();

            if (!parser.AtEnd)
                return false;

            Item = item;
            return true;

        }

        #endregion


        #region (static) ParseList      (Text)

        /// <summary>
        /// Parse a field value as a List.
        /// </summary>
        public static IReadOnlyList<SFMember> ParseList(String Text)

            => TryParseList(Text, out var members)
                   ? members
                   : throw new ArgumentException($"'{Text}' is not a valid structured field List!", nameof(Text));

        #endregion

        #region (static) ParseDictionary(Text)

        /// <summary>
        /// Parse a field value as a Dictionary.
        /// </summary>
        public static SFDictionary ParseDictionary(String Text)

            => TryParseDictionary(Text, out var dictionary)
                   ? dictionary
                   : throw new ArgumentException($"'{Text}' is not a valid structured field Dictionary!", nameof(Text));

        #endregion

        #region (static) ParseItem      (Text)

        /// <summary>
        /// Parse a field value as a single Item.
        /// </summary>
        public static SFItem ParseItem(String Text)

            => TryParseItem(Text, out var item)
                   ? item
                   : throw new ArgumentException($"'{Text}' is not a valid structured field Item!", nameof(Text));

        #endregion

        #region (static) Serialize      (Members)

        /// <summary>
        /// Serialize a List (RFC 9651, Section 4.1.1).
        /// </summary>
        public static String Serialize(IEnumerable<SFMember> Members)

            => Members.Select(member => member.ToString()).AggregateWith(", ");

        #endregion


        #region (private class) Parser

        /// <summary>
        /// The Section 4.2 algorithms, one method each and in their order, over
        /// a cursor into the field value.
        /// </summary>
        private sealed class Parser
        {

            private readonly String  input;
            private          Int32   position;

            public Parser(String Input)
            {
                input     = Input;
                position  = 0;
            }

            #region Cursor

            public Boolean AtEnd
                => position >= input.Length;

            private Char Current
                => input[position];

            private Boolean Next(Char Character)
                => !AtEnd && input[position] == Character;

            public void SkipSpaces()
            {
                while (!AtEnd && Current == ' ')
                    position++;
            }

            private void SkipOWS()
            {
                while (!AtEnd && (Current == ' ' || Current == '\t'))
                    position++;
            }

            #endregion

            #region TryParseList(out Members)

            public Boolean TryParseList([NotNullWhen(true)] out List<SFMember>? Members)
            {

                Members = null;

                var members = new List<SFMember>();

                while (!AtEnd)
                {

                    if (!TryParseItemOrInnerList(out var member))
                        return false;

                    members.Add(member);

                    SkipOWS();

                    if (AtEnd)
                        break;

                    if (Current != ',')
                        return false;

                    position++;

                    SkipOWS();

                    // A trailing comma is a parse failure rather than an empty
                    // last member: the sender meant to say something else.
                    if (AtEnd)
                        return false;

                }

                Members = members;
                return true;

            }

            #endregion

            #region TryParseDictionary(out Dictionary)

            public Boolean TryParseDictionary([NotNullWhen(true)] out SFDictionary? Dictionary)
            {

                Dictionary = null;

                var dictionary = new SFDictionary();

                while (!AtEnd)
                {

                    if (!TryParseKey(out var key))
                        return false;

                    SFMember member;

                    if (Next('='))
                    {

                        position++;

                        if (!TryParseItemOrInnerList(out var value))
                            return false;

                        member = value;

                    }

                    else
                    {

                        if (!TryParseParameters(out var parameters))
                            return false;

                        member = new SFItem(SFBareItem.Boolean(true), parameters);

                    }

                    dictionary.Set(key, member);

                    SkipOWS();

                    if (AtEnd)
                        break;

                    if (Current != ',')
                        return false;

                    position++;

                    SkipOWS();

                    if (AtEnd)
                        return false;

                }

                Dictionary = dictionary;
                return true;

            }

            #endregion

            #region TryParseItemOrInnerList(out Member)

            private Boolean TryParseItemOrInnerList([NotNullWhen(true)] out SFMember? Member)
            {

                Member = null;

                if (Next('('))
                {

                    if (!TryParseInnerList(out var innerList))
                        return false;

                    Member = innerList;
                    return true;

                }

                if (!TryParseItem(out var item))
                    return false;

                Member = item;
                return true;

            }

            #endregion

            #region TryParseInnerList(out InnerList)

            private Boolean TryParseInnerList([NotNullWhen(true)] out SFInnerList? InnerList)
            {

                InnerList = null;

                if (!Next('('))
                    return false;

                position++;

                var items = new List<SFItem>();

                while (!AtEnd)
                {

                    SkipSpaces();

                    if (Next(')'))
                    {

                        position++;

                        if (!TryParseParameters(out var parameters))
                            return false;

                        InnerList = new SFInnerList(items, parameters);
                        return true;

                    }

                    if (!TryParseItem(out var item))
                        return false;

                    items.Add(item);

                    if (!AtEnd && Current != ' ' && Current != ')')
                        return false;

                }

                // The end of the inner list was never found.
                return false;

            }

            #endregion

            #region TryParseItem(out Item)

            public Boolean TryParseItem([NotNullWhen(true)] out SFItem? Item)
            {

                Item = null;

                if (!TryParseBareItem(out var bareItem))
                    return false;

                if (!TryParseParameters(out var parameters))
                    return false;

                Item = new SFItem(bareItem, parameters);
                return true;

            }

            #endregion

            #region TryParseBareItem(out BareItem)

            private Boolean TryParseBareItem(out SFBareItem BareItem)
            {

                BareItem = default;

                if (AtEnd)
                    return false;

                var character = Current;

                if (character == '-' || Char.IsAsciiDigit(character))
                    return TryParseIntegerOrDecimal(out BareItem);

                if (character == '"')
                    return TryParseString(out BareItem);

                if (character == ':')
                    return TryParseByteSequence(out BareItem);

                if (character == '?')
                    return TryParseBoolean(out BareItem);

                if (character == '@')
                    return TryParseDate(out BareItem);

                if (character == '%')
                    return TryParseDisplayString(out BareItem);

                if (character == '*' || Char.IsAsciiLetter(character))
                    return TryParseToken(out BareItem);

                return false;

            }

            #endregion

            #region TryParseParameters(out Parameters)

            private Boolean TryParseParameters(out SFParameters Parameters)
            {

                Parameters = new SFParameters();

                while (!AtEnd)
                {

                    if (Current != ';')
                        break;

                    position++;

                    SkipSpaces();

                    if (!TryParseKey(out var key))
                        return false;

                    var value = SFBareItem.Boolean(true);

                    if (Next('='))
                    {

                        position++;

                        if (!TryParseBareItem(out value))
                            return false;

                    }

                    Parameters.Set(key, value);

                }

                return true;

            }

            #endregion

            #region TryParseKey(out Key)

            private Boolean TryParseKey([NotNullWhen(true)] out String? Key)
            {

                Key = null;

                if (AtEnd)
                    return false;

                if (!IsLowerAlpha(Current) && Current != '*')
                    return false;

                var start = position;

                while (!AtEnd &&
                       (IsLowerAlpha(Current) ||
                        Char.IsAsciiDigit(Current) ||
                        Current is '_' or '-' or '.' or '*'))
                    position++;

                Key = input[start..position];
                return true;

            }

            #endregion

            #region TryParseIntegerOrDecimal(out BareItem)

            private Boolean TryParseIntegerOrDecimal(out SFBareItem BareItem)
            {

                BareItem = default;

                var isDecimal  = false;
                var sign       = 1;
                var digits     = new StringBuilder();

                if (Next('-'))
                {
                    position++;
                    sign = -1;
                }

                if (AtEnd || !Char.IsAsciiDigit(Current))
                    return false;

                while (!AtEnd)
                {

                    var character = Current;

                    if (Char.IsAsciiDigit(character))
                    {
                        position++;
                        digits.Append(character);
                    }

                    else if (!isDecimal && character == '.')
                    {

                        if (digits.Length > 12)
                            return false;

                        position++;
                        digits.Append(character);
                        isDecimal = true;

                    }

                    else
                        break;

                    if (!isDecimal && digits.Length > 15)
                        return false;

                    if ( isDecimal && digits.Length > 16)
                        return false;

                }

                var text = digits.ToString();

                if (!isDecimal)
                {

                    if (!Int64.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var integer))
                        return false;

                    BareItem = SFBareItem.Integer(sign * integer);
                    return true;

                }

                if (text.EndsWith('.'))
                    return false;

                if (text.Length - text.IndexOf('.') - 1 > 3)
                    return false;

                if (!Decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var fraction))
                    return false;

                BareItem = SFBareItem.Decimal(sign * fraction);
                return true;

            }

            #endregion

            #region TryParseString(out BareItem)

            private Boolean TryParseString(out SFBareItem BareItem)
            {

                BareItem = default;

                if (!Next('"'))
                    return false;

                position++;

                var text = new StringBuilder();

                while (!AtEnd)
                {

                    var character = input[position++];

                    if (character == '\\')
                    {

                        if (AtEnd)
                            return false;

                        var escaped = input[position++];

                        // Only these two may be escaped. A backslash in front of
                        // anything else is a different field than the sender
                        // wrote, so it is refused rather than passed through.
                        if (escaped is not ('"' or '\\'))
                            return false;

                        text.Append(escaped);

                    }

                    else if (character == '"')
                    {
                        BareItem = SFBareItem.String(text.ToString());
                        return true;
                    }

                    else if (character < ' ' || character > '~')
                        return false;

                    else
                        text.Append(character);

                }

                return false;

            }

            #endregion

            #region TryParseToken(out BareItem)

            private Boolean TryParseToken(out SFBareItem BareItem)
            {

                BareItem = default;

                if (AtEnd || (!Char.IsAsciiLetter(Current) && Current != '*'))
                    return false;

                var start = position;

                position++;

                while (!AtEnd && SFBareItem.IsTokenCharacter(Current))
                    position++;

                BareItem = SFBareItem.Token(input[start..position]);
                return true;

            }

            #endregion

            #region TryParseByteSequence(out BareItem)

            private Boolean TryParseByteSequence(out SFBareItem BareItem)
            {

                BareItem = default;

                if (!Next(':'))
                    return false;

                position++;

                var closing = input.IndexOf(':', position);

                if (closing < 0)
                    return false;

                var base64 = input[position..closing];

                position = closing + 1;

                foreach (var character in base64)
                {
                    if (!Char.IsAsciiLetterOrDigit(character) &&
                         character is not ('+' or '/' or '='))
                        return false;
                }

                try
                {
                    BareItem = SFBareItem.ByteSequence(Convert.FromBase64String(base64));
                }
                catch (FormatException)
                {
                    return false;
                }

                return true;

            }

            #endregion

            #region TryParseBoolean(out BareItem)

            private Boolean TryParseBoolean(out SFBareItem BareItem)
            {

                BareItem = default;

                if (!Next('?'))
                    return false;

                position++;

                if (AtEnd)
                    return false;

                var character = input[position++];

                if (character == '1')
                {
                    BareItem = SFBareItem.Boolean(true);
                    return true;
                }

                if (character == '0')
                {
                    BareItem = SFBareItem.Boolean(false);
                    return true;
                }

                return false;

            }

            #endregion

            #region TryParseDate(out BareItem)

            private Boolean TryParseDate(out SFBareItem BareItem)
            {

                BareItem = default;

                if (!Next('@'))
                    return false;

                position++;

                if (!TryParseIntegerOrDecimal(out var number))
                    return false;

                // "@1659578233.0" is not a date with a redundant fraction, it is
                // not a date (RFC 9651, Section 4.2.9).
                if (number.Type != SFType.Integer)
                    return false;

                BareItem = SFBareItem.Date(number.AsInteger);
                return true;

            }

            #endregion

            #region TryParseDisplayString(out BareItem)

            private Boolean TryParseDisplayString(out SFBareItem BareItem)
            {

                BareItem = default;

                if (!Next('%'))
                    return false;

                position++;

                if (!Next('"'))
                    return false;

                position++;

                var octets = new List<Byte>();

                while (!AtEnd)
                {

                    var character = input[position++];

                    if (character < ' ' || character > '~')
                        return false;

                    if (character == '%')
                    {

                        if (position + 1 >= input.Length)
                            return false;

                        var high = input[position++];
                        var low  = input[position++];

                        // Lower case only, so that two senders cannot write the
                        // same string two ways - which is the whole point of a
                        // canonical serialization.
                        if (!IsLowerHexDigit(high) || !IsLowerHexDigit(low))
                            return false;

                        octets.Add((Byte) ((HexValue(high) << 4) | HexValue(low)));

                    }

                    else if (character == '"')
                    {

                        try
                        {
                            BareItem = SFBareItem.DisplayString(
                                           new UTF8Encoding(false, true).GetString([.. octets])
                                       );
                        }
                        catch (DecoderFallbackException)
                        {
                            return false;
                        }

                        return true;

                    }

                    else
                        octets.Add((Byte) character);

                }

                return false;

            }

            private static Boolean IsLowerHexDigit(Char Character)

                => Char.IsAsciiDigit(Character) ||
                   Character is >= 'a' and <= 'f';

            private static Int32 HexValue(Char Character)

                => Char.IsAsciiDigit(Character)
                       ? Character - '0'
                       : Character - 'a' + 10;

            #endregion

        }

        #endregion

    }

}
