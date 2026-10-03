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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The three date formats an HTTP timestamp may take (RFC 9110, Section
    /// 5.6.7), and the one a sender is allowed to produce.
    ///
    /// "A recipient that parses a timestamp value in an HTTP field MUST accept
    /// all three HTTP-date formats" — the preferred IMF-fixdate and the two
    /// obsolete ones, RFC 850 and ANSI C's asctime(). "When a sender generates
    /// a field that contains one or more timestamps defined as HTTP-date, the
    /// sender MUST generate those timestamps in the IMF-fixdate format."
    /// </summary>
    /// <remarks>
    /// Version-neutral on purpose: HTTP/2 and HTTP/3 carry the same fields with
    /// the same semantics, and a parser only HTTP/1 can reach is how RFC 7616
    /// Digest came to be unreachable until H-3 moved it out of HTTP2/.
    ///
    /// Parsing is deliberately more permissive than the grammar in one respect:
    /// Section 5.6.7 says HTTP-date is case sensitive, and the month and day
    /// names are matched here without regard to case, because recipients "are
    /// encouraged to be robust in parsing timestamps" and refusing a timestamp
    /// over the case of "Nov" helps nobody. Generation is exact.
    /// </remarks>
    public static class HTTPDate
    {

        #region Data

        /// <summary>
        /// The preferred format, and the only one a sender may generate:
        /// "Sun, 06 Nov 1994 08:49:37 GMT".
        /// </summary>
        public const String IMFFixdate    = "ddd, dd MMM yyyy HH:mm:ss 'GMT'";

        /// <summary>
        /// The obsolete RFC 850 format, with a two-digit year:
        /// "Sunday, 06-Nov-94 08:49:37 GMT".
        /// </summary>
        public const String RFC850Date    = "dddd, dd-MMM-yy HH:mm:ss 'GMT'";

        /// <summary>
        /// The obsolete asctime() format, which carries no zone at all:
        /// "Sun Nov  6 08:49:37 1994".
        /// </summary>
        public const String AsctimeDate   = "ddd MMM d HH:mm:ss yyyy";

        #endregion

        #region TryParse(Text, out Timestamp)

        /// <summary>
        /// Parse a timestamp in any of the three HTTP-date formats.
        /// </summary>
        /// <param name="Text">The field value to parse.</param>
        /// <param name="Timestamp">The timestamp, in UTC.</param>
        public static Boolean TryParse(String              Text,
                                       out DateTimeOffset  Timestamp)

            => TryParse(Text, DateTimeOffset.UtcNow, out Timestamp);

        #endregion

        #region TryParse(Text, Now, out Timestamp)

        /// <summary>
        /// Parse a timestamp in any of the three HTTP-date formats, resolving
        /// the RFC 850 format's two-digit year against the given moment.
        /// </summary>
        /// <param name="Text">The field value to parse.</param>
        /// <param name="Now">What time it is, for the two-digit year rule below. A parameter so that the rule can be tested without waiting fifty years for it.</param>
        /// <param name="Timestamp">The timestamp, in UTC.</param>
        public static Boolean TryParse(String              Text,
                                       DateTimeOffset      Now,
                                       out DateTimeOffset  Timestamp)
        {

            var text = Text?.Trim();

            if (String.IsNullOrEmpty(text))
            {
                Timestamp = default;
                return false;
            }

            const DateTimeStyles utc = DateTimeStyles.AssumeUniversal |
                                       DateTimeStyles.AdjustToUniversal;

            // The format a sender MUST generate, so the one worth trying first.
            if (DateTimeOffset.TryParseExact(text,
                                             IMFFixdate,
                                             CultureInfo.InvariantCulture,
                                             utc,
                                             out Timestamp))
            {
                return true;
            }

            // asctime() has two spaces before a single-digit day ("Nov  6"),
            // which is what AllowInnerWhite tolerates, and no zone at all —
            // Section 5.6.7: "values in the asctime format are assumed to be
            // in UTC".
            if (DateTimeOffset.TryParseExact(text,
                                             AsctimeDate,
                                             CultureInfo.InvariantCulture,
                                             DateTimeStyles.AllowInnerWhite | utc,
                                             out Timestamp))
            {
                return true;
            }

            #region The RFC 850 format, and its two-digit year

            // The long day name is dropped rather than matched. .NET checks a
            // "dddd" against the date, and here it cannot: the year is two
            // digits, so the day of the week it would be checked against is the
            // one in whatever century the calendar's pivot picked — not the one
            // the sender meant, which is only settled by the rule below. A
            // sender writing "Saturday, 06-Nov-55" of 2055 would be refused for
            // 1955 having begun on a different day.
            //
            // The other two formats carry a four-digit year and stay strict,
            // where the check means what it says.
            var afterDayName = text.IndexOf(", ", StringComparison.Ordinal) + 2;

            if (afterDayName > 1 &&
                DateTimeOffset.TryParseExact(text[afterDayName..],
                                             "dd-MMM-yy HH:mm:ss 'GMT'",
                                             CultureInfo.InvariantCulture,
                                             utc,
                                             out var obsolete))
            {

                // The year .NET chose came from InvariantCulture's fixed pivot
                // (TwoDigitYearMax, 2049), and Section 5.6.7's window slides
                // with the current date instead: "Recipients of a timestamp
                // value in rfc850-date format, which uses a two-digit year,
                // MUST interpret a timestamp that appears to be more than 50
                // years in the future as representing the most recent year in
                // the past that had the same last two digits."
                //
                // So only the last two digits are taken from what .NET parsed,
                // and the century is chosen here. AddYears rather than a new
                // DateTimeOffset, because 29 February then moves to the 28th of
                // a year that has no 29th instead of throwing.
                var century    = Now.Year - Now.Year % 100;
                var candidate  = obsolete.AddYears(century + obsolete.Year % 100 - obsolete.Year);

                if (candidate > Now.AddYears(50))
                    candidate = candidate.AddYears(-100);

                Timestamp = candidate;
                return true;

            }

            #endregion

            Timestamp = default;
            return false;

        }

        #endregion

        #region ToHTTPDate(this Timestamp)

        /// <summary>
        /// The timestamp as an IMF-fixdate, which is the only format a sender
        /// may generate (RFC 9110, Section 5.6.7).
        /// </summary>
        /// <param name="Timestamp">A timestamp.</param>
        public static String ToHTTPDate(this DateTimeOffset Timestamp)

            => Timestamp.ToUniversalTime().
                   ToString(IMFFixdate, CultureInfo.InvariantCulture);

        #endregion

    }

}
