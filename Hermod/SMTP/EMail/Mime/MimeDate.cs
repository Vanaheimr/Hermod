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

using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// The Date: of a mail, as mail programs write it - which is RFC 5322 §3.3 far
    /// less often than one would hope.
    /// </summary>
    /// <remarks>
    /// Accepted besides the standard form: a trailing comment ("(CET)"), no day name
    /// or a wrong one, two- and three-digit years (§4.3), no seconds, the zone names
    /// UT, GMT and the North American ones of §4.3, a zone written "+01:00", and the
    /// asctime() order "Wed Oct 14 09:30:00 2026". A military zone letter, or no zone
    /// at all, is read as UTC, as §4.3 says to.
    /// </remarks>
    public static class MimeDate
    {

        #region Data

        private static readonly Regex Comment  = new (@"\([^()]*\)", RegexOptions.CultureInvariant);
        private static readonly Regex Time     = new (@"^(\d{1,2}):(\d{2})(?::(\d{2}))?$", RegexOptions.CultureInvariant);
        private static readonly Regex Offset   = new (@"^([+-])(\d{2}):?(\d{2})$", RegexOptions.CultureInvariant);

        private static readonly String[] Months = [ "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" ];

        private static readonly Dictionary<String, Int32> Zones = new (StringComparer.OrdinalIgnoreCase) {
            ["UT"]  =  0, ["UTC"] =  0, ["GMT"] =  0, ["Z"] = 0,
            ["EST"] = -5, ["EDT"] = -4,
            ["CST"] = -6, ["CDT"] = -5,
            ["MST"] = -7, ["MDT"] = -6,
            ["PST"] = -8, ["PDT"] = -7
        };

        #endregion


        #region TryParse(Text, out Date)

        /// <summary>
        /// Read the given Date: value.
        /// </summary>
        public static Boolean TryParse(String?             Text,
                                       out DateTimeOffset  Date)
        {

            Date = default;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            // Comments go, nested ones from the inside out.
            var text = Text;
            while (Comment.IsMatch(text))
                text = Comment.Replace(text, " ");

            var tokens = text.Replace(",", " ").
                              Split([ ' ', '\t', '\r', '\n' ], StringSplitOptions.RemoveEmptyEntries).
                              ToList();

            // The day name, wherever it is, says nothing the date does not.
            tokens.RemoveAll(token => token.Length >= 3 && Char.IsLetter(token[0]) && MonthOf(token) is null && !Zones.ContainsKey(token) && token.Length != 1);

            Int32? day = null, month = null, year = null;
            TimeSpan?   time    = null;
            TimeSpan    offset  = TimeSpan.Zero;

            foreach (var token in tokens)
            {

                if (time is null && Time.Match(token) is { Success: true } t)
                {
                    var hours   = Int32.Parse(t.Groups[1].Value);
                    var minutes = Int32.Parse(t.Groups[2].Value);
                    var seconds = t.Groups[3].Success ? Int32.Parse(t.Groups[3].Value) : 0;
                    if (hours > 23 || minutes > 59 || seconds > 60)
                        return false;
                    time = new TimeSpan(hours, minutes, Math.Min(seconds, 59));
                }

                else if (Offset.Match(token) is { Success: true } o)
                {
                    var span = new TimeSpan(Int32.Parse(o.Groups[2].Value), Int32.Parse(o.Groups[3].Value), 0);
                    offset   = o.Groups[1].Value == "-" ? -span : span;
                }

                else if (Zones.TryGetValue(token, out var hoursOffset))
                    offset = TimeSpan.FromHours(hoursOffset);

                // A military zone letter: RFC 5322 §4.3 says they were used so wrongly
                // that they mean nothing, i.e. -0000.
                else if (token.Length == 1 && Char.IsLetter(token[0]))
                    offset = TimeSpan.Zero;

                else if (MonthOf(token) is { } m)
                    month = m;

                else if (Int32.TryParse(token, out var number))
                {
                    if      (day  is null && number is >= 1 and <= 31 && token.Length <= 2 && (month is null || year is null))
                        day   = number;
                    else if (year is null)
                        year  = token.Length switch {
                                    2 => number < 50 ? 2000 + number : 1900 + number,
                                    3 => 1900 + number,
                                    _ => number
                                };
                }

            }

            if (day is null || month is null || year is null)
                return false;

            try
            {
                Date = new DateTimeOffset(year.Value, month.Value, day.Value, 0, 0, 0, offset) + (time ?? TimeSpan.Zero);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

        }

        #endregion

        #region (private) MonthOf(Token)

        private static Int32? MonthOf(String Token)
        {

            if (Token.Length < 3)
                return null;

            var index = Array.IndexOf(Months, Token[..3].ToLowerInvariant());

            return index >= 0 && Token.All(Char.IsLetter)
                       ? index + 1
                       : null;

        }

        #endregion

    }

}
