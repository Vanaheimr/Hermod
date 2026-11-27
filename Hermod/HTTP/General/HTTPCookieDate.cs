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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The date of a cookie's Expires attribute, read the way a user agent
    /// reads it: RFC 6265, Section 5.1.1, unchanged in draft-ietf-httpbis-rfc6265bis-22.
    /// </summary>
    /// <remarks>
    /// A server writes an IMF-fixdate (RFC 9110, Section 5.6.7), and that is
    /// what <see cref="ToString(DateTimeOffset)"/> produces. What arrives is
    /// whatever servers have written in thirty years: RFC 850's two-digit
    /// years, asctime's order, dashes, no weekday, the time first. The
    /// algorithm reads all of them by looking at each token for the first
    /// time, day, month and year it has not yet found, and it never reads a
    /// time zone: a cookie date is UTC.
    /// </remarks>
    public static class HTTPCookieDate
    {

        #region Data

        private static readonly String[] months = [ "jan", "feb", "mar", "apr", "may", "jun",
                                                    "jul", "aug", "sep", "oct", "nov", "dec" ];

        #endregion


        #region TryParse(Text, out Date)

        /// <summary>
        /// Parse the given text as a cookie-date.
        /// </summary>
        /// <param name="Text">The value of an Expires attribute.</param>
        /// <param name="Date">The date, in UTC.</param>
        public static Boolean TryParse(String?                                  Text,
                                       [NotNullWhen(true)] out DateTimeOffset?  Date)
        {

            Date = null;

            if (Text is null)
                return false;

            Int32? hour  = null, minute = null, second = null;
            Int32? day   = null, month  = null, year   = null;

            var position = 0;

            while (position < Text.Length)
            {

                // date-token = 1*non-delimiter
                while (position < Text.Length && IsDelimiter(Text[position]))
                    position++;

                var start = position;

                while (position < Text.Length && !IsDelimiter(Text[position]))
                    position++;

                if (position == start)
                    break;

                var token = Text.AsSpan(start, position - start);

                if (hour is null && TryReadTime(token, out var h, out var m, out var s))
                {
                    hour = h; minute = m; second = s;
                    continue;
                }

                if (day is null && TryReadDigits(token, 1, 2, out var d))
                {
                    day = d;
                    continue;
                }

                if (month is null && TryReadMonth(token, out var mo))
                {
                    month = mo;
                    continue;
                }

                if (year is null && TryReadDigits(token, 2, 4, out var y))
                {
                    year = y;
                    continue;
                }

            }

            if (year is >= 70 and <= 99)
                year += 1900;

            else if (year is >= 0 and <= 69)
                year += 2000;

            if (hour  is null || day    is null || month  is null || year is null ||
                day   <  1    || day    > 31    || year   <  1601 ||
                hour  > 23    || minute > 59    || second > 59)
            {
                return false;
            }

            // "If no such date exists" - the 31st of a month with thirty days.
            if (day > DateTime.DaysInMonth(year.Value, month.Value))
                return false;

            Date = new DateTimeOffset(year.Value, month.Value, day.Value,
                                      hour.Value, minute!.Value, second!.Value,
                                      TimeSpan.Zero);

            return true;

        }

        #endregion

        #region ToString(Date)

        /// <summary>
        /// The given date as an IMF-fixdate, the form RFC 6265, Section 4.1.1
        /// asks a server to write ("Sun, 06 Nov 1994 08:49:37 GMT").
        /// </summary>
        /// <param name="Date">A date.</param>
        public static String ToString(DateTimeOffset Date)

            => Date.ToUniversalTime().ToString("r", System.Globalization.CultureInfo.InvariantCulture);

        #endregion


        #region (private) IsDelimiter(Character)

        /// <summary>
        /// delimiter = %x09 / %x20-2F / %x3B-40 / %x5B-60 / %x7B-7E
        /// </summary>
        private static Boolean IsDelimiter(Char Character)

            => Character == '\t'
            || Character is >= '\x20' and <= '\x2F'
            || Character is >= '\x3B' and <= '\x40'
            || Character is >= '\x5B' and <= '\x60'
            || Character is >= '\x7B' and <= '\x7E';

        #endregion

        #region (private) TryReadDigits(Token, Min, Max, out Number)

        /// <summary>
        /// Min*Max DIGIT [ non-digit *OCTET ]: the digits the token begins
        /// with, and after them the token's end or anything but a digit.
        /// </summary>
        private static Boolean TryReadDigits(ReadOnlySpan<Char> Token, Int32 Min, Int32 Max, out Int32 Number)
        {

            Number = 0;

            var count = 0;

            while (count < Token.Length && Char.IsAsciiDigit(Token[count]))
            {

                if (count == Max)
                    return false;

                Number = Number * 10 + (Token[count] - '0');
                count++;

            }

            return count >= Min;

        }

        #endregion

        #region (private) TryReadTime(Token, out Hour, out Minute, out Second)

        /// <summary>
        /// time = hms-time [ non-digit *OCTET ], hms-time = time-field ":" time-field ":" time-field,
        /// time-field = 1*2DIGIT.
        /// </summary>
        private static Boolean TryReadTime(ReadOnlySpan<Char> Token, out Int32 Hour, out Int32 Minute, out Int32 Second)
        {

            Hour = Minute = Second = 0;

            var position = 0;

            for (var field = 0; field < 3; field++)
            {

                if (field > 0)
                {
                    if (position >= Token.Length || Token[position] != ':')
                        return false;
                    position++;
                }

                var value = 0;
                var count = 0;

                while (position < Token.Length && Char.IsAsciiDigit(Token[position]) && count < 2)
                {
                    value = value * 10 + (Token[position] - '0');
                    position++;
                    count++;
                }

                if (count == 0)
                    return false;

                switch (field)
                {
                    case 0:  Hour   = value; break;
                    case 1:  Minute = value; break;
                    default: Second = value; break;
                }

            }

            // A third digit is no time-field of two; anything else may follow.
            return position == Token.Length || !Char.IsAsciiDigit(Token[position]);

        }

        #endregion

        #region (private) TryReadMonth(Token, out Month)

        /// <summary>
        /// month = ( "jan" / "feb" / ... / "dec" ) *OCTET, case-insensitively.
        /// </summary>
        private static Boolean TryReadMonth(ReadOnlySpan<Char> Token, out Int32 Month)
        {

            Month = 0;

            if (Token.Length < 3)
                return false;

            for (var i = 0; i < months.Length; i++)
            {
                if (Token[..3].Equals(months[i], StringComparison.OrdinalIgnoreCase))
                {
                    Month = i + 1;
                    return true;
                }
            }

            return false;

        }

        #endregion

    }

}
