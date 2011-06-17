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

            var _IPv4Address = IPAddressFactory.Parse("141.24.12.2");

            Assert.IsTrue (_IPv4Address is IIPAddress);
            Assert.IsTrue (_IPv4Address is IPv4Address);
            Assert.IsFalse(_IPv4Address is IPv6Address);

        }

        #endregion


        #region ParseTooShortByteArray()

        /// <summary>
        /// IPAddressFactory byte array parsing test.
        /// </summary>
        [Test]
        [ExpectedException(typeof(FormatException))]
        public void ParseTooShortByteArray()
        {
            IPAddressFactory.Build(new Byte[] { 10, 0, 0 });
        }

        #endregion

        #region ParseIPv4ByteArray()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4ByteArray()
        {

            var _IPv4Address = IPAddressFactory.Build(new Byte[] { 10, 0, 0, 0 });

            Assert.IsTrue (_IPv4Address is IIPAddress);
            Assert.IsTrue (_IPv4Address is IPv4Address);
            Assert.IsFalse(_IPv4Address is IPv6Address);

        }

        #endregion

        #region ParseIPv6ByteArray()

        /// <summary>
        /// IPv6Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv6ByteArray()
        {

            var _IPv4Address = IPAddressFactory.Build(new Byte[] { 10, 0, 0, 0, 10, 0, 0, 0, 10, 0, 0, 0, 10, 0, 0, 0 });

            Assert.IsTrue (_IPv4Address is IIPAddress);
            Assert.IsFalse(_IPv4Address is IPv4Address);
            Assert.IsTrue (_IPv4Address is IPv6Address);

        }

        #endregion

    }

}
