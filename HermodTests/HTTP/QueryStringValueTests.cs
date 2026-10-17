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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// A value of a query string is kept whole: a single value is all of it, commas and all, and a list is its parts.
    /// </summary>
    /// <remarks>
    /// Every value was split at its commas when it was parsed, and every single value was the last part: a search for
    /// "Jena, Nord" searched for "Nord", and "!a,b" for "b" - not even negated. A value was decoded before the query
    /// string was split, so an encoded "&amp;" split it, and a value holding a "=" was dropped altogether.
    /// </remarks>
    [TestFixture]
    public class QueryStringValueTests
    {

        #region A single value is all of it

        [Test]
        public void A_single_value_keeps_its_commas()
        {

            var queryString = QueryString.Parse("?match=Jena,%20Nord");

            Assert.That(queryString.GetString("match"), Is.EqualTo("Jena, Nord"));
            Assert.That(queryString.TryGetString("match", out var value), Is.True);
            Assert.That(value, Is.EqualTo("Jena, Nord"));

        }

        [Test]
        public void An_encoded_comma_is_kept_as_well()
        {
            Assert.That(QueryString.Parse("?match=Jena%2C%20Nord").GetString("match"), Is.EqualTo("Jena, Nord"));
        }

        [Test]
        public void A_single_value_is_the_one_of_the_last_parameter_of_its_name()
        {
            Assert.That(QueryString.Parse("?match=a,b&match=c,d").GetString("match"), Is.EqualTo("c,d"));
        }

        [Test]
        public void A_string_filter_takes_the_whole_value_and_its_negation()
        {

            var cities  = new[] { "Jena, Nord", "Jena, Süd", "Nordhausen" };

            var matched = cities.Where(QueryString.Parse("?match=Jena,%20Nord").
                                           CreateStringFilter<String>("match", (city, text) => city.Contains(text))).
                                 ToArray();

            Assert.That(matched, Is.EqualTo(new[] { "Jena, Nord" }));

            var notMatched = cities.Where(QueryString.Parse("?match=!Jena,%20Nord").
                                              CreateStringFilter<String>("match", (city, text) => city.Contains(text))).
                                    ToArray();

            Assert.That(notMatched, Is.EqualTo(new[] { "Jena, Süd", "Nordhausen" }));

        }

        [Test]
        public void A_number_with_a_decimal_comma_is_not_its_part_after_the_comma()
        {
            // Not 5: the whole value.
            Assert.That(QueryString.Parse("?olderThan=1,5").GetString("olderThan"), Is.EqualTo("1,5"));
        }

        #endregion

        #region A real number

        [Test]
        public void A_real_number_is_written_as_on_the_wire()
        {

            var queryString = QueryString.Parse("?a=1.5&b=-2.5e3&c=%2B0.25&d=0.1");

            Assert.That(queryString.GetDouble ("a"),        Is.EqualTo(1.5));
            Assert.That(queryString.GetDouble ("b"),        Is.EqualTo(-2500));
            Assert.That(queryString.GetSingle ("c"),        Is.EqualTo(0.25f));
            Assert.That(queryString.GetDecimal("d"),        Is.EqualTo(0.1m));
            Assert.That(queryString.GetDouble ("a", 7),     Is.EqualTo(1.5));

        }

        [Test]
        public void A_decimal_comma_or_a_thousands_separator_is_no_number()
        {

            // Not 15, as NumberStyles.Any read it with its thousands separators, nor 1,500.
            var queryString = QueryString.Parse("?a=1,5&b=1,500&c=$5&d=(5)");

            foreach (var name in new[] { "a", "b", "c", "d" })
            {
                Assert.That(queryString.GetDouble (name),       Is.Null,          name);
                Assert.That(queryString.GetSingle (name),       Is.Null,          name);
                Assert.That(queryString.GetDecimal(name),       Is.Null,          name);
                Assert.That(queryString.GetDouble (name, 7),    Is.EqualTo(7),    name);
                Assert.That(queryString.TryGetDouble(name, out _), Is.False,      name);
            }

        }

        [Test]
        public void A_real_number_is_read_alike_whatever_culture_the_server_runs_in()
        {

            var culture = CultureInfo.CurrentCulture;

            try
            {

                // A German machine read "1,5" as 1.5 and "1.5" as 15 in TryGet…: now as everywhere else.
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

                var queryString = QueryString.Parse("?a=1.5&b=1,5");

                Assert.That(queryString.TryGetDouble ("a", out var a),  Is.True);
                Assert.That(a,                                          Is.EqualTo(1.5));
                Assert.That(queryString.TryGetSingle ("a", out var s),  Is.True);
                Assert.That(s,                                          Is.EqualTo(1.5f));
                Assert.That(queryString.TryGetDecimal("a", out var d),  Is.True);
                Assert.That(d,                                          Is.EqualTo(1.5m));
                Assert.That(queryString.TryGetDouble ("b", out _),      Is.False);

            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }

        }

        [Test]
        public void What_is_not_finite_is_no_number_and_no_time_span()
        {

            var queryString = QueryString.Parse("?a=NaN&b=Infinity&c=1e300&d=90");

            Assert.That(queryString.GetDouble  ("a"),   Is.Null);
            Assert.That(queryString.GetDouble  ("b"),   Is.Null);

            // Longer than any time span: none, not an OverflowException.
            Assert.That(queryString.GetTimeSpan("b"),   Is.Null);
            Assert.That(queryString.GetTimeSpan("c"),   Is.Null);
            Assert.That(queryString.GetTimeSpan("c", TimeSpan.FromSeconds(1)),  Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(queryString.GetTimeSpan("d"),   Is.EqualTo(TimeSpan.FromSeconds(90)));

        }

        #endregion

        #region A list is the parts

        [Test]
        public void A_list_is_the_parts_of_every_parameter_of_its_name()
        {
            Assert.That(QueryString.Parse("?expand=a,%20b&expand=c").GetStrings("expand"), Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void A_list_takes_encoded_commas_as_separators_too()
        {
            // As a client encoding its value with encodeURIComponent sends it.
            Assert.That(QueryString.Parse("?expand=a%2Cb").GetStrings("expand"), Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void A_list_leaves_out_empty_parts()
        {
            Assert.That(QueryString.Parse("?expand=a,,b,").GetStrings("expand"), Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void A_multi_filter_takes_every_part()
        {

            var filter = QueryString.Parse("?status=1,!3&status=2").CreateMultiFilter<Int32>("status", Int32.TryParse);

            Assert.That(new[] { 1, 2, 3, 4 }.Where(filter), Is.EqualTo(new[] { 1, 2 }));

        }

        [Test]
        public void The_enumerator_gives_the_parts()
        {

            var parameter = QueryString.Parse("?expand=a,b").Single();

            Assert.That(parameter.Key,    Is.EqualTo("expand"));
            Assert.That(parameter.Value,  Is.EqualTo(new Object[] { "a", "b" }));

        }

        #endregion

        #region Split before decoded

        [Test]
        public void An_encoded_ampersand_is_part_of_a_value()
        {

            var queryString = QueryString.Parse("?name=Fish%20%26%20Chips&limit=3");

            Assert.That(queryString.GetString("name"),   Is.EqualTo("Fish & Chips"));
            Assert.That(queryString.GetString("limit"),  Is.EqualTo("3"));
            Assert.That(queryString.GetString(" Chips"), Is.Null);

        }

        [Test]
        public void A_value_holding_an_equals_sign_is_kept()
        {
            Assert.That(QueryString.Parse("?dns=AAAB=&match=a=b").GetString("dns"),   Is.EqualTo("AAAB="));
            Assert.That(QueryString.Parse("?dns=AAAB=&match=a=b").GetString("match"), Is.EqualTo("a=b"));
        }

        [Test]
        public void An_encoded_question_mark_is_part_of_a_value()
        {
            Assert.That(QueryString.Parse("/search?q=why%3F&x=1").GetString("q"), Is.EqualTo("why?"));
        }

        [Test]
        public void A_parameter_without_a_value_is_an_empty_one()
        {

            var queryString = QueryString.Parse("?flag&empty=");

            Assert.That(queryString.GetString("flag"),   Is.EqualTo(""));
            Assert.That(queryString.GetString("empty"),  Is.EqualTo(""));

        }

        [Test]
        public void An_encoded_key_is_decoded()
        {
            Assert.That(QueryString.Parse("?a%20b=c").GetString("a b"), Is.EqualTo("c"));
        }

        #endregion

        #region Round trip

        [Test]
        public void A_query_string_round_trips_with_its_values_whole()
        {

            var queryString = QueryString.Parse("?match=Jena,%20Nord&expand=a,b&name=Fish%20%26%20Chips");
            var again       = QueryString.Parse(queryString.ToString());

            Assert.That(again.GetString ("match"),   Is.EqualTo("Jena, Nord"));
            Assert.That(again.GetStrings("expand"),  Is.EqualTo(new[] { "a", "b" }));
            Assert.That(again.GetString ("name"),    Is.EqualTo("Fish & Chips"));

        }

        [Test]
        public void Removing_a_part_keeps_the_other_parts()
        {

            var queryString = QueryString.Parse("?expand=a,b,c").Remove("expand", "b");

            Assert.That(queryString.GetStrings("expand"), Is.EqualTo(new[] { "a", "c" }));

        }

        #endregion

    }

}
