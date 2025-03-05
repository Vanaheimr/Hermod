/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using NUnit.Framework;
using NUnit.Framework.Legacy;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
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

            var ipv4String  = "141.24.12.2";
            var ipv4Address = IPv4Address.Parse(ipv4String);

            ClassicAssert.AreEqual(ipv4String, ipv4Address.ToString());

        }

        #endregion

        #region ParseIPv4String_002()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4String_002()
        {
            Assert.Throws<ArgumentException>(() => IPv4Address.Parse("141.24.12"));
        }

        #endregion

        #region ParseIPv4String_003()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4String_003()
        {
            Assert.Throws<ArgumentException>(() => IPv4Address.Parse("300.24.12.2"));
        }

        #endregion

        #region ParseIPv4String_004()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4String_004()
        {
            Assert.Throws<ArgumentException>(() => IPv4Address.Parse("141.300.12.2"));
        }

        #endregion

        #region ParseIPv4String_005()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4String_005()
        {
            Assert.Throws<ArgumentException>(() => IPv4Address.Parse("141.24.300.2"));
        }

        #endregion

        #region ParseIPv4String_006()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4String_006()
        {
            Assert.Throws<ArgumentException>(() => IPv4Address.Parse("141.24.12.300"));
        }

        #endregion


        #region ParseIPv4ByteArray_001()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4ByteArray_001()
        {
            ClassicAssert.AreEqual("0.0.0.0", new IPv4Address(new Byte[4]).ToString());
        }

        #endregion

        #region ParseIPv4ByteArray_002()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4ByteArray_002()
        {
            ClassicAssert.AreEqual("10.11.12.13", new IPv4Address(new Byte[] { 0x0A, 0x0B, 0x0C, 0x0D }).ToString());
        }

        #endregion

        #region ParseIPv4ByteArray_003()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4ByteArray_003()
        {
            Assert.Throws<FormatException>(() => new IPv4Address(new Byte[2]));
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

            ClassicAssert.IsTrue   (a.Equals(b));
            ClassicAssert.IsTrue   (a == b);
            ClassicAssert.IsFalse  (a != b);
            ClassicAssert.AreEqual (a, b);

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

            ClassicAssert.IsFalse     (a.Equals(b));
            ClassicAssert.IsFalse     (a == b);
            ClassicAssert.IsTrue      (a != b);
            ClassicAssert.AreNotEqual (a, b);

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

            ClassicAssert.IsTrue(a.CompareTo(b) > 0);

        }

        #endregion

    }

}
