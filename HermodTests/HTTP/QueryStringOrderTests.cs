/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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
    /// QueryString parameter order tests.
    ///
    /// The parameters are stored within a ConcurrentDictionary, which does not have any
    /// defined enumeration order. Without an explicitly tracked insertion order the
    /// parameters would be returned in an arbitrary and even varying order, so that a
    /// query string neither round-trips nor can be compared as text.
    /// </summary>
    [TestFixture]
    public class QueryStringOrderTests
    {

        #region ToString_KeepsTheParsedOrder()

        /// <summary>
        /// The order in which the parameters were parsed must be kept.
        /// </summary>
        [Test]
        public void ToString_KeepsTheParsedOrder()
        {

            Assert.That(QueryString.Parse("?z=1&a=2&m=3").ToString(), Is.EqualTo("?z=1&a=2&m=3"));
            Assert.That(QueryString.Parse("?a=1&m=2&z=3").ToString(), Is.EqualTo("?a=1&m=2&z=3"));

        }

        #endregion

        #region ToString_KeepsTheInsertionOrder()

        /// <summary>
        /// The order in which the parameters were added must be kept, so it must not be
        /// sorted alphabetically either.
        /// </summary>
        [Test]
        public void ToString_KeepsTheInsertionOrder()
        {

            var queryString = QueryString.Empty.
                                  Add("z", "1").
                                  Add("a", "2").
                                  Add("m", "3");

            Assert.That(queryString.ToString(), Is.EqualTo("?z=1&a=2&m=3"));

        }

        #endregion

        #region ToString_IsStableAcrossRepeatedCalls()

        /// <summary>
        /// This is the regression test for the actual defect: the very same query string
        /// used to serialize differently from call to call and from instance to instance.
        /// </summary>
        [Test]
        public void ToString_IsStableAcrossRepeatedCalls()
        {

            var expected = "?z=1&a=2&m=3&q=4&b=5";
            var single   = QueryString.Parse(expected);

            for (var i = 0; i < 100; i++)
            {
                Assert.That(single.ToString(),                  Is.EqualTo(expected));
                Assert.That(QueryString.Parse(expected).ToString(), Is.EqualTo(expected));
            }

        }

        #endregion

        #region Enumeration_UsesTheSameOrderAsToString()

        /// <summary>
        /// GetEnumerator() must not disagree with ToString().
        /// </summary>
        [Test]
        public void Enumeration_UsesTheSameOrderAsToString()
        {

            var queryString = QueryString.Parse("?z=1&a=2&m=3");

            Assert.That(queryString.Select(parameter => parameter.Key).ToArray(),
                        Is.EqualTo(new[] { "z", "a", "m" }));

        }

        #endregion

        #region Remove_DoesNotDisturbTheOrderOfTheRemainingParameters()

        /// <summary>
        /// Removing a parameter must keep the order of the remaining ones, and re-adding
        /// it must put it at the end.
        /// </summary>
        [Test]
        public void Remove_DoesNotDisturbTheOrderOfTheRemainingParameters()
        {

            var queryString = QueryString.Parse("?z=1&a=2&m=3");

            queryString.Remove("a");
            Assert.That(queryString.ToString(), Is.EqualTo("?z=1&m=3"));

            queryString.Add("a", "4");
            Assert.That(queryString.ToString(), Is.EqualTo("?z=1&m=3&a=4"));

        }

        #endregion

        #region URL_RoundTripsItsQueryString()

        /// <summary>
        /// A parsed URL must serialize back to the very same text.
        /// </summary>
        [Test]
        public void URL_RoundTripsItsQueryString()
        {

            Assert.That(URL.Parse("https://example.org/p?z=1&a=2&m=3").ToString(),
                        Is.EqualTo("https://example.org/p?z=1&a=2&m=3"));

        }

        #endregion

    }

}
