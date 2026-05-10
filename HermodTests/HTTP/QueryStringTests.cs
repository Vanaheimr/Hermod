///*
// * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
// * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
// *
// * Licensed under the Apache License, Version 2.0 (the "License");
// * you may not use this file except in compliance with the License.
// * You may obtain a copy of the License at
// *
// *     http://www.apache.org/licenses/LICENSE-2.0
// *
// * Unless required by applicable law or agreed to in writing, software
// * distributed under the License is distributed on an "AS IS" BASIS,
// * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// * See the License for the specific language governing permissions and
// * limitations under the License.
// */

//#region Usings

//using System;
//using System.Linq;

//using org.GraphDefined.Vanaheimr.Hermod.HTTP;

//using NUnit.Framework;

//#endregion

//namespace org.GraphDefined.Vanaheimr.Hermod.Tests
//{

//    /// <summary>
//    /// QueryString test.
//    /// </summary>
//    [TestFixture]
//    public class QueryStringTests
//    {

//        #region ParseQueryString_001()

//        [Test]
//        public void ParseQueryString_001()
//        {

//            var QueryString = new QueryString("abc?a=b");

//            Assert.That(QueryString.Count(), Is.EqualTo(1));
//            Assert.That(QueryString.First().Key, Is.EqualTo("a"));
//            Assert.That(QueryString.First().Value.First(), Is.EqualTo("b"));

//        }

//        #endregion

//        #region ParseQueryString_002()

//        [Test]
//        public void ParseQueryString_002()
//        {

//            var QueryString = new QueryString("?a=b&a=c");

//            Assert.That(QueryString.Count(), Is.EqualTo(1));
//            Assert.That(QueryString.First().Key, Is.EqualTo("a"));
//            Assert.That(QueryString.First().Value.First(), Is.EqualTo("b"));
//            Assert.That(QueryString.First().Value.Skip(1).First(), Is.EqualTo("c"));

//        }

//        #endregion

//        #region ParseQueryString_003()

//        [Test]
//        public void ParseQueryString_003()
//        {

//            var QueryString = new QueryString("a=b&c=d");

//            Assert.That(QueryString.Count(), Is.EqualTo(2));
//            Assert.That(QueryString.First().Key, Is.EqualTo("a"));
//            Assert.That(QueryString.First().Value.First(), Is.EqualTo("b"));
//            Assert.That(QueryString.Skip(1).First().Key, Is.EqualTo("c"));
//            Assert.That(QueryString.Skip(1).First().Value.First(), Is.EqualTo("d"));

//        }

//        #endregion


//        #region ParseQueryString_004()

//        [Test]
//        //[ExpectedException(typeof(ArgumentNullException))]
//        public void ParseQueryString_004()
//        {
//            var QueryString = new QueryString("");
//        }

//        #endregion

//        #region ParseQueryString_005()

//        [Test]
//        //[ExpectedException(typeof(ArgumentNullException))]
//        public void ParseQueryString_005()
//        {
//            var QueryString = new QueryString("?");
//        }

//        #endregion


//        #region ParseQueryString_006()

//        [Test]
//        public void ParseQueryString_006()
//        {
            
//            var QueryString = new QueryString("&");
            
//            Assert.That(QueryString.Count(), Is.EqualTo(0));

//        }

//        #endregion

//        #region ParseQueryString_007()

//        [Test]
//        public void ParseQueryString_007()
//        {

//            var QueryString = new QueryString("a=b&");

//            Assert.That(QueryString.Count(), Is.EqualTo(1));

//        }

//        #endregion

//        #region ParseQueryString_008()

//        [Test]
//        public void ParseQueryString_008()
//        {

//            var QueryString = new QueryString("a=");

//            Assert.That(QueryString.Count(), Is.EqualTo(0));

//        }

//        #endregion

//        #region ParseQueryString_009()

//        [Test]
//        public void ParseQueryString_009()
//        {

//            var QueryString = new QueryString("=b");

//            Assert.That(QueryString.Count(), Is.EqualTo(0));

//        }

//        #endregion

//    }

//}
