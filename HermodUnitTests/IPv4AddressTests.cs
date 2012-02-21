/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Pipes.NET
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

using de.ahzf.Hermod.Datastructures;

using NUnit.Framework;

#endregion

namespace de.ahzf.Hermod.UnitTests
{

    /// <summary>
    /// IPv4Address test.
    /// </summary>
    [TestFixture]
    public class IPv4AddressTests
    {

        #region ParseIPv4String_001()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4String_001()
        {

            var _IPv4String  = "141.24.12.2";
            var _IPv4Address = IPv4Address.Parse(_IPv4String);

            Assert.AreEqual(_IPv4String, _IPv4Address.ToString());

        }

        #endregion

        #region ParseIPv4String_002()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        [ExpectedException(typeof(FormatException))]
        public void ParseIPv4String_002()
        {
            IPv4Address.Parse("141.24.12");
        }

        #endregion

        #region ParseIPv4String_003()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        [ExpectedException(typeof(FormatException))]
        public void ParseIPv4String_003()
        {
            IPv4Address.Parse("300.24.12.2");
        }

        #endregion

        #region ParseIPv4String_004()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        [ExpectedException(typeof(FormatException))]
        public void ParseIPv4String_004()
        {
            IPv4Address.Parse("141.300.12.2");
        }

        #endregion

        #region ParseIPv4String_005()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        [ExpectedException(typeof(FormatException))]
        public void ParseIPv4String_005()
        {
            IPv4Address.Parse("141.24.300.2");
        }

        #endregion

        #region ParseIPv4String_006()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        [ExpectedException(typeof(FormatException))]
        public void ParseIPv4String_006()
        {
            IPv4Address.Parse("141.24.12.300");
        }

        #endregion


        #region ParseIPv4ByteArray_001()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4ByteArray_001()
        {
            Assert.AreEqual("0.0.0.0", new IPv4Address(new Byte[4]).ToString());
        }

        #endregion

        #region ParseIPv4ByteArray_002()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4ByteArray_002()
        {
            Assert.AreEqual("10.11.12.13", new IPv4Address(new Byte[4] { 0x0A, 0x0B, 0x0C, 0x0D }).ToString());
        }

        #endregion

        #region ParseIPv4ByteArray_003()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        [ExpectedException(typeof(FormatException))]
        public void ParseIPv4ByteArray_003()
        {
            new IPv4Address(new Byte[2]);
        }

        #endregion


        #region IPv4AddressesAreEqual()

        /// <summary>
        /// Checks if two IPv4Address are equal.
        /// </summary>
        [Test]
        public void IPv4AddressesAreEqual()
        {
            
            var a = IPv4Address.Parse("141.24.12.2");
            var b = IPv4Address.Parse("141.24.12.2");

            Assert.IsTrue   (a.Equals(b));
            Assert.IsTrue   (a == b);
            Assert.IsFalse  (a != b);
            Assert.AreEqual (a, b);

        }

        #endregion

        #region IPv4AddressesAreNotEqual()

        /// <summary>
        /// Checks if two IPv4Address are not equal.
        /// </summary>
        [Test]
        public void IPv4AddressesAreNotEqual()
        {

            var a = IPv4Address.Parse("141.24.12.2");
            var b = IPv4Address.Parse("127.0.0.1");

            Assert.IsFalse     (a.Equals(b));
            Assert.IsFalse     (a == b);
            Assert.IsTrue      (a != b);
            Assert.AreNotEqual (a, b);

        }

        #endregion


        #region CompareIPv4Addresses()

        /// <summary>
        /// Compares two IPv4Addresses.
        /// </summary>
        [Test]
        public void CompareIPv4Addresses()
        {

            var a = IPv4Address.Parse("141.24.12.2");
            var b = IPv4Address.Parse("127.0.0.1");

            Assert.IsTrue(a.CompareTo(b) > 0);

        }

        #endregion

    }

}
