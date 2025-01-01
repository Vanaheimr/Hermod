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
    /// IPAddressFactory test.
    /// </summary>
    [TestFixture]
    public class IPAddressTests
    {

        #region ParseIPv4String_001()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4String_001()
        {

            var ipv4Address = IPAddressHelper.Parse("141.24.12.2");

            ClassicAssert.IsTrue (ipv4Address is IIPAddress);
            ClassicAssert.IsTrue (ipv4Address is IPv4Address);
            ClassicAssert.IsFalse(ipv4Address is IPv6Address);

        }

        #endregion


        #region ParseTooShortByteArray()

        /// <summary>
        /// IPAddressFactory byte array parsing test.
        /// </summary>
        [Test]
        public void ParseTooShortByteArray()
        {
            Assert.Throws<FormatException>(() => IPAddressHelper.Build(new Byte[] { 10, 0, 0 }));
        }

        #endregion

        #region ParseIPv4ByteArray()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4ByteArray()
        {

            var ipv4Address = IPAddressHelper.Build(new Byte[] { 10, 0, 0, 0 });

            ClassicAssert.IsTrue (ipv4Address is IIPAddress);
            ClassicAssert.IsTrue (ipv4Address is IPv4Address);
            ClassicAssert.IsFalse(ipv4Address is IPv6Address);

        }

        #endregion

        #region ParseIPv6ByteArray()

        /// <summary>
        /// IPv6Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv6ByteArray()
        {

            var ipv6Address = IPAddressHelper.Build(new Byte[] { 10, 0, 0, 0, 10, 0, 0, 0, 10, 0, 0, 0, 10, 0, 0, 0 });

            ClassicAssert.IsTrue (ipv6Address is IIPAddress);
            ClassicAssert.IsFalse(ipv6Address is IPv4Address);
            ClassicAssert.IsTrue (ipv6Address is IPv6Address);

        }

        #endregion

    }

}
