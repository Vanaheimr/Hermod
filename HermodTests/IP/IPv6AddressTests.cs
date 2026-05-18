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

using NUnit.Framework;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.IP
{

    /// <summary>
    /// IPv6Address tests.
    /// </summary>
    [TestFixture]
    public class IPv6AddressTests
    {

        #region ParseIPv6String_001()

        /// <summary>
        /// IPv6Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv6String_001()
        {

            var ipv6Address = IPv6Address.Parse("2001:db8::1");

            Assert.That(ipv6Address.ToString(), Is.EqualTo("2001:0db8:0000:0000:0000:0000:0000:0001"));

        }

        #endregion

        #region ParseIPv6String_002()

        /// <summary>
        /// IPv6Address string parsing test for bracketed addresses.
        /// </summary>
        [Test]
        public void ParseIPv6String_002()
        {

            var ipv6Address = IPv6Address.Parse("[2001:db8::2]");

            Assert.That(ipv6Address.ToString(), Is.EqualTo("2001:0db8:0000:0000:0000:0000:0000:0002"));

        }

        #endregion

        #region ParseIPv6String_003()

        /// <summary>
        /// IPv6Address string parsing test for scoped addresses.
        /// </summary>
        [Test]
        public void ParseIPv6String_003()
        {

            var ipv6Address = IPv6Address.Parse("[fe80::1%eth0]");

            Assert.That(ipv6Address.InterfaceId, Is.EqualTo("eth0"));
            Assert.That(ipv6Address.ToString(),  Is.EqualTo("fe80:0000:0000:0000:0000:0000:0000:0001%eth0"));

        }

        #endregion

        #region ParseIPv6String_004()

        /// <summary>
        /// IPv6Address string parsing test for IPv4-mapped notation.
        /// </summary>
        [Test]
        public void ParseIPv6String_004()
        {

            var ipv6Address = IPv6Address.Parse("::ffff:192.0.2.128");

            Assert.That(ipv6Address.ToString(),     Is.EqualTo("0000:0000:0000:0000:0000:ffff:c000:0280"));
            Assert.That(ipv6Address.IsMappedIPv4,   Is.True);
            Assert.That(ipv6Address.MappedIPv4?.ToString(), Is.EqualTo("192.0.2.128"));

        }

        #endregion

        #region TryParseIPv6String_RejectsMalformedInput()

        /// <summary>
        /// IPv6Address string parsing should reject malformed input.
        /// </summary>
        [Test]
        public void TryParseIPv6String_RejectsMalformedInput()
        {

            Assert.That(IPv6Address.TryParse("",              out _), Is.False);
            Assert.That(IPv6Address.TryParse("192.0.2.1",     out _), Is.False);
            Assert.That(IPv6Address.TryParse("2001::db8::1",  out _), Is.False);
            Assert.That(IPv6Address.TryParse("2001:::1",      out _), Is.False);
            Assert.That(IPv6Address.TryParse("[::1",          out _), Is.False);
            Assert.That(IPv6Address.TryParse("::1]",          out _), Is.False);
            Assert.That(IPv6Address.TryParse("fe80::1%",      out _), Is.False);
            Assert.That(IPv6Address.TryParse("gggg::1",       out _), Is.False);
            Assert.That(IPv6Address.TryParse("1:2:3:4:5:6:7:8:9", out _), Is.False);

            Assert.Throws<ArgumentException>(() => IPv6Address.Parse("2001::db8::1"));

        }

        #endregion

        #region IPv6AddressProperties()

        /// <summary>
        /// Checks common IPv6 address properties.
        /// </summary>
        [Test]
        public void IPv6AddressProperties()
        {

            Assert.That(IPv6Address.Any.      IsAny,       Is.True);
            Assert.That(IPv6Address.Any.      ToString(),  Is.EqualTo("[::]"));
            Assert.That(IPv6Address.Localhost.IsLocalhost, Is.True);
            Assert.That(IPv6Address.Localhost.ToString(),  Is.EqualTo("[::1]"));

            Assert.That(IPv6Address.Parse("ff02::1").IsMulticast, Is.True);
            Assert.That(IPv6Address.Parse("2001:db8::1").IsMulticast, Is.False);

        }

        #endregion

        #region IPv6AddressByteOperations()

        /// <summary>
        /// Checks IPv6Address byte conversion helpers.
        /// </summary>
        [Test]
        public void IPv6AddressByteOperations()
        {

            var bytes       = new Byte[] { 0x20, 0x01, 0x0D, 0xB8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
            var ipv6Address = new IPv6Address(bytes);

            Assert.That(ipv6Address.GetBytes(), Is.EqualTo(bytes));

            var destination = new Byte[16];
            ipv6Address.CopyTo(destination);
            Assert.That(destination, Is.EqualTo(bytes));

            Assert.Throws<FormatException>(() => new IPv6Address(new Byte[15]));
            Assert.Throws<ArgumentException>(() => ipv6Address.CopyTo(new Byte[15]));

        }

        #endregion

        #region IPv6AddressFromIPAddress()

        /// <summary>
        /// Checks conversion to and from System.Net.IPAddress.
        /// </summary>
        [Test]
        public void IPv6AddressFromIPAddress()
        {

            var systemIPAddress = System.Net.IPAddress.Parse("2001:db8::1234");
            var ipv6Address     = IPv6Address.From(systemIPAddress);

            Assert.That(ipv6Address.ToString(), Is.EqualTo("2001:0db8:0000:0000:0000:0000:0000:1234"));

            System.Net.IPAddress roundtrip = ipv6Address;
            Assert.That(roundtrip.ToString(), Is.EqualTo("2001:db8::1234"));

            Assert.Throws<FormatException>(() => IPv6Address.From(System.Net.IPAddress.Loopback));

        }

        #endregion

        #region IPv6AddressFromIPv4()

        /// <summary>
        /// Checks IPv4 mapped IPv6 address creation.
        /// </summary>
        [Test]
        public void IPv6AddressFromIPv4()
        {

            var ipv6Address = IPv6Address.FromIPv4(IPv4Address.Parse("192.0.2.128"));

            Assert.That(ipv6Address.ToString(), Is.EqualTo("0000:0000:0000:0000:0000:ffff:c000:0280"));
            Assert.That(ipv6Address.MappedIPv4?.ToString(), Is.EqualTo("192.0.2.128"));

        }

        #endregion

        #region IPv6AddressesAreEqual()

        /// <summary>
        /// Checks if two IPv6Address are equal.
        /// </summary>
        [Test]
        public void IPv6AddressesAreEqual()
        {

            var a = IPv6Address.Parse("2001:db8::1");
            var b = IPv6Address.Parse("2001:0db8:0000:0000:0000:0000:0000:0001");

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a == b,     Is.True);
            Assert.That(a != b,     Is.False);
            Assert.That(b,          Is.EqualTo(a));

        }

        #endregion

        #region CompareIPv6Addresses()

        /// <summary>
        /// Compares two IPv6Addresses.
        /// </summary>
        [Test]
        public void CompareIPv6Addresses()
        {

            var a = IPv6Address.Parse("2001:db8::2");
            var b = IPv6Address.Parse("2001:db8::1");

            Assert.That(a.CompareTo(b) > 0, Is.True);
            Assert.That(a > b,              Is.True);
            Assert.That(b < a,              Is.True);

        }

        #endregion

    }

}
