/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.Linq;

using eu.Vanaheimr.Hermod.HTTP;

using NUnit.Framework;

#endregion

namespace eu.Vanaheimr.Hermod.UnitTests
{

    /// <summary>
    /// QueryString test.
    /// </summary>
    [TestFixture]
    public class QueryStringTests
    {

        #region ParseQueryString_001()

        [Test]
        public void ParseQueryString_001()
        {

            var QueryString = new QueryString("abc?a=b");

            Assert.AreEqual(1,   QueryString.Count());
            Assert.AreEqual("a", QueryString.First().Key);
            Assert.AreEqual("b", QueryString.First().Value.First());

        }

        #endregion

        #region ParseQueryString_002()

        [Test]
        public void ParseQueryString_002()
        {

            var QueryString = new QueryString("?a=b&a=c");

            Assert.AreEqual(1, QueryString.Count());
            Assert.AreEqual("a", QueryString.First().Key);
            Assert.AreEqual("b", QueryString.First().Value[0]);
            Assert.AreEqual("c", QueryString.First().Value[1]);

        }

        #endregion

        #region ParseQueryString_003()

        [Test]
        public void ParseQueryString_003()
        {

            var QueryString = new QueryString("a=b&c=d");

            Assert.AreEqual(2, QueryString.Count());
            Assert.AreEqual("a", QueryString.First().Key);
            Assert.AreEqual("b", QueryString.First().Value.First());
            Assert.AreEqual("c", QueryString.Skip(1).First().Key);
            Assert.AreEqual("d", QueryString.Skip(1).First().Value[0]);

        }

        #endregion


        #region ParseQueryString_004()

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ParseQueryString_004()
        {
            var QueryString = new QueryString("");
        }

        #endregion

        #region ParseQueryString_005()

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ParseQueryString_005()
        {
            var QueryString = new QueryString("?");
        }

        #endregion


        #region ParseQueryString_006()

        [Test]
        public void ParseQueryString_006()
        {
            
            var QueryString = new QueryString("&");
            
            Assert.AreEqual(0, QueryString.Count());

        }

        #endregion

        #region ParseQueryString_007()

        [Test]
        public void ParseQueryString_007()
        {

            var QueryString = new QueryString("a=b&");

            Assert.AreEqual(1, QueryString.Count());

        }

        #endregion

        #region ParseQueryString_008()

        [Test]
        public void ParseQueryString_008()
        {

            var QueryString = new QueryString("a=");

            Assert.AreEqual(0, QueryString.Count());

        }

        #endregion

        #region ParseQueryString_009()

        [Test]
        public void ParseQueryString_009()
        {

            var QueryString = new QueryString("=b");

            Assert.AreEqual(0, QueryString.Count());

        }

        #endregion

    }

}
