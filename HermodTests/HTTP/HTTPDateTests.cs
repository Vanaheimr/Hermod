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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 9110 §5.6.7, the three date formats: "A recipient that parses a
    /// timestamp value in an HTTP field MUST accept all three HTTP-date
    /// formats", and a sender MUST generate the preferred one.
    ///
    /// The three examples the section itself gives all mean the same instant,
    /// which is what makes them a test: 6 November 1994, 08:49:37 UTC.
    /// </summary>
    /// <remarks>
    /// H-11 in HTTP1ConformanceTests. The date fields were parsed with
    /// DateTimeOffset.TryParse, which takes neither obsolete format — and names
    /// no culture either, so what it accepted depended on the machine it ran
    /// on.
    /// </remarks>
    [TestFixture]
    public class HTTPDateTests
    {

        #region Data

        /// <summary>
        /// The instant all three examples in §5.6.7 denote.
        /// </summary>
        private static readonly DateTimeOffset expected = new (1994, 11, 6, 8, 49, 37, TimeSpan.Zero);

        #endregion


        #region The three formats of §5.6.7

        [Test]
        public void An_IMF_Fixdate_Is_Parsed()
        {
            Assert.That(HTTPDate.TryParse("Sun, 06 Nov 1994 08:49:37 GMT", out var timestamp), Is.True);
            Assert.That(timestamp, Is.EqualTo(expected));
        }

        [Test]
        public void The_Obsolete_RFC850_Format_Is_Parsed()
        {
            Assert.That(HTTPDate.TryParse("Sunday, 06-Nov-94 08:49:37 GMT", out var timestamp), Is.True);
            Assert.That(timestamp, Is.EqualTo(expected));
        }

        /// <summary>
        /// Two spaces before the single-digit day, and no zone at all — §5.6.7
        /// says asctime values are assumed to be UTC.
        /// </summary>
        [Test]
        public void The_Obsolete_Asctime_Format_Is_Parsed()
        {
            Assert.That(HTTPDate.TryParse("Sun Nov  6 08:49:37 1994", out var timestamp), Is.True);
            Assert.That(timestamp, Is.EqualTo(expected));
        }

        /// <summary>
        /// A two-digit day in asctime takes one space, not two.
        /// </summary>
        [Test]
        public void An_Asctime_With_A_Two_Digit_Day_Is_Parsed()
        {
            Assert.That(HTTPDate.TryParse("Tue Nov 15 12:45:26 1994", out var timestamp), Is.True);
            Assert.That(timestamp, Is.EqualTo(new DateTimeOffset(1994, 11, 15, 12, 45, 26, TimeSpan.Zero)));
        }

        #endregion

        #region The two-digit year of the RFC 850 format

        /// <summary>
        /// "Recipients of a timestamp value in rfc850-date format, which uses a
        /// two-digit year, MUST interpret a timestamp that appears to be more
        /// than 50 years in the future as representing the most recent year in
        /// the past that had the same last two digits."
        ///
        /// Now is a parameter precisely so this can be asserted rather than
        /// waited for.
        /// </summary>
        [Test]
        public void A_Two_Digit_Year_Within_Fifty_Years_Stays_In_The_Future()
        {

            var now = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);

            Assert.That(HTTPDate.TryParse("Friday, 06-Nov-70 08:49:37 GMT", now, out var timestamp), Is.True);
            Assert.That(timestamp.Year, Is.EqualTo(2070), timestamp.ToString());

        }

        [Test]
        public void A_Two_Digit_Year_More_Than_Fifty_Years_Ahead_Goes_Back_A_Century()
        {

            var now = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);

            // 2080 is fifty-four years ahead, so the year meant is 1980.
            Assert.That(HTTPDate.TryParse("Thursday, 06-Nov-80 08:49:37 GMT", now, out var timestamp), Is.True);
            Assert.That(timestamp.Year, Is.EqualTo(1980), timestamp.ToString());

        }

        /// <summary>
        /// The case .NET's own two-digit window gets wrong, and the reason the
        /// century is chosen here instead of being left to the calendar: with
        /// InvariantCulture's fixed pivot of 2049, "55" is 1955 whatever year
        /// it is read in. In 2060 that is a timestamp five years in the past
        /// being read as one a hundred and five years in the past.
        /// </summary>
        [Test]
        public void The_Window_Slides_With_The_Clock_Rather_Than_Sitting_At_2049()
        {

            var now = new DateTimeOffset(2060, 1, 1, 0, 0, 0, TimeSpan.Zero);

            Assert.That(HTTPDate.TryParse("Saturday, 06-Nov-55 08:49:37 GMT", now, out var timestamp), Is.True);
            Assert.That(timestamp.Year, Is.EqualTo(2055), timestamp.ToString());

        }

        /// <summary>
        /// The asymmetry this pins as a decision rather than an accident: an
        /// IMF-fixdate whose day name contradicts its date is refused, and an
        /// RFC 850 date's day name is not looked at.
        ///
        /// It cannot be. The year is two digits, so the weekday a check would
        /// compare against belongs to whichever century the calendar's pivot
        /// chose, and not to the one the rule above settles on — 6 November
        /// 1955 and 6 November 2055 do not fall on the same day. Checking it
        /// would refuse correct timestamps for the wrong century's calendar,
        /// and RFC 9110 §5.6.7 asks recipients to be robust rather than exact.
        /// </summary>
        [Test]
        public void A_Day_Name_Is_Checked_Where_It_Can_Be_And_Not_Where_It_Cannot()
        {

            var now = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);

            Assert.Multiple(() => {

                // 6 November 1994 was a Sunday, not a Monday.
                Assert.That(HTTPDate.TryParse("Mon, 06 Nov 1994 08:49:37 GMT", out _),
                            Is.False,
                            "an IMF-fixdate with the wrong day name");

                Assert.That(HTTPDate.TryParse("Mon Nov  6 08:49:37 1994", out _),
                            Is.False,
                            "an asctime with the wrong day name");

                Assert.That(HTTPDate.TryParse("Monday, 06-Nov-94 08:49:37 GMT", now, out var timestamp),
                            Is.True,
                            "an RFC 850 date's day name is not checked");

                Assert.That(timestamp, Is.EqualTo(expected));

            });

        }

        #endregion

        #region What is refused

        [Test]
        public void Neither_Nothing_Nor_Nonsense_Is_A_Date()
        {
            Assert.Multiple(() => {

                Assert.That(HTTPDate.TryParse("",                              out _), Is.False, "the empty string");
                Assert.That(HTTPDate.TryParse("   ",                           out _), Is.False, "whitespace");
                Assert.That(HTTPDate.TryParse("tomorrow",                      out _), Is.False, "a word");
                Assert.That(HTTPDate.TryParse("1994-11-06T08:49:37Z",          out _), Is.False, "ISO 8601, which is not an HTTP-date");
                Assert.That(HTTPDate.TryParse("Sun, 06 Nov 1994 08:49:37 UTC", out _), Is.False, "a zone that is not GMT");

            });
        }

        #endregion

        #region Generating

        /// <summary>
        /// §5.6.7: a sender MUST generate the IMF-fixdate format. And it is
        /// generated from UTC, whatever offset the value carries.
        /// </summary>
        [Test]
        public void A_Timestamp_Is_Generated_As_An_IMF_Fixdate()
        {
            Assert.Multiple(() => {

                Assert.That(expected.ToHTTPDate(),                      Is.EqualTo("Sun, 06 Nov 1994 08:49:37 GMT"));

                Assert.That(new DateTimeOffset(1994, 11, 6, 10, 49, 37, TimeSpan.FromHours(2)).ToHTTPDate(),
                            Is.EqualTo("Sun, 06 Nov 1994 08:49:37 GMT"),
                            "an offset must be converted, not dropped");

            });
        }

        /// <summary>
        /// And what is generated is parsed back: the round trip is the property
        /// a peer depends on.
        /// </summary>
        [Test]
        public void What_Is_Generated_Parses_Back()
        {

            var now = new DateTimeOffset(2026, 10, 3, 16, 27, 23, TimeSpan.Zero);

            Assert.That(HTTPDate.TryParse(now.ToHTTPDate(), out var timestamp), Is.True);
            Assert.That(timestamp, Is.EqualTo(now));

        }

        #endregion

        #region Through the header fields

        /// <summary>
        /// The parser is reached through the fields that use it, which is where
        /// the MUST applies — a helper nothing calls would satisfy nobody.
        /// </summary>
        [Test]
        public void The_Date_Field_Accepts_All_Three_Formats()
        {
            Assert.Multiple(() => {

                foreach (var text in new[] {
                             "Sun, 06 Nov 1994 08:49:37 GMT",
                             "Sunday, 06-Nov-94 08:49:37 GMT",
                             "Sun Nov  6 08:49:37 1994"
                         })
                {

                    var parsed = HTTPHeaderField.Date.StringParser!(text, out var timestamp, out _);

                    Assert.That(parsed,     Is.True,              text);
                    Assert.That(timestamp,  Is.EqualTo(expected), text);

                }

            });
        }

        [Test]
        public void The_Last_Modified_Field_Accepts_All_Three_Formats()
        {
            Assert.Multiple(() => {

                foreach (var text in new[] {
                             "Sun, 06 Nov 1994 08:49:37 GMT",
                             "Sunday, 06-Nov-94 08:49:37 GMT",
                             "Sun Nov  6 08:49:37 1994"
                         })
                {

                    var parsed = HTTPResponseHeaderField.LastModified.StringParser!(text, out var timestamp, out _);

                    Assert.That(parsed,     Is.True,              text);
                    Assert.That(timestamp,  Is.EqualTo(expected), text);

                }

            });
        }

        /// <summary>
        /// And the field serializes as an HTTP-date rather than as ISO 8601,
        /// which is what it said until 2026-10-03. Nothing on the wire carried
        /// that, because AHTTPPDUBuilder serializes every DateTimeOffset-valued
        /// field with the Date field's serializer — so this is the trap rather
        /// than the bug, and it is now shut.
        /// </summary>
        [Test]
        public void The_Last_Modified_Field_Serializes_As_An_HTTP_Date()
        {
            Assert.That(
                HTTPResponseHeaderField.LastModified.ValueSerializer(expected),
                Is.EqualTo("Sun, 06 Nov 1994 08:49:37 GMT")
            );
        }

        #endregion

    }

}
