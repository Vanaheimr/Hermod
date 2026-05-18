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

            Assert.That(ipv4Address.ToString(), Is.EqualTo(ipv4String));

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
            Assert.That(new IPv4Address(new Byte[4]).ToString(), Is.EqualTo("0.0.0.0"));
        }

        #endregion

        #region ParseIPv4ByteArray_002()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4ByteArray_002()
        {
            Assert.That(new IPv4Address(new Byte[] { 0x0A, 0x0B, 0x0C, 0x0D }).ToString(), Is.EqualTo("10.11.12.13"));
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

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a == b, Is.True);
            Assert.That(a != b, Is.False);
            Assert.That(b, Is.EqualTo(a));

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

            Assert.That(a.Equals(b), Is.False);
            Assert.That(a == b, Is.False);
            Assert.That(a != b, Is.True);
            Assert.That(b, Is.Not.EqualTo(a));

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

            Assert.That(a.CompareTo(b) > 0, Is.True);

        }

        #endregion


        #region TryParseIPv4String_RejectsMalformedInput()

        /// <summary>
        /// IPv4Address string parsing should reject malformed octets.
        /// </summary>
        [Test]
        public void TryParseIPv4String_RejectsMalformedInput()
        {

            Assert.That(IPv4Address.TryParse("141.24.12",       out _), Is.False);
            Assert.That(IPv4Address.TryParse("141.24.12.2.1",   out _), Is.False);
            Assert.That(IPv4Address.TryParse("141.24.12.-1",    out _), Is.False);
            Assert.That(IPv4Address.TryParse("141.24.12.+1",    out _), Is.False);
            Assert.That(IPv4Address.TryParse("141.24.12.one",   out _), Is.False);
            Assert.That(IPv4Address.TryParse("141.24.12.256",   out _), Is.False);
            Assert.That(IPv4Address.TryParse("141.24.12. 2",    out _), Is.False);

        }

        #endregion

        #region TryParseIPv4String_TrimsOuterWhitespace()

        /// <summary>
        /// IPv4Address string parsing should allow outer whitespace only.
        /// </summary>
        [Test]
        public void TryParseIPv4String_TrimsOuterWhitespace()
        {

            Assert.That(IPv4Address.TryParse(" 141.24.12.2 ", out var ipv4Address), Is.True);
            Assert.That(ipv4Address.ToString(), Is.EqualTo("141.24.12.2"));

        }

        #endregion

        #region IPv4AddressProperties()

        /// <summary>
        /// Checks common IPv4 address properties.
        /// </summary>
        [Test]
        public void IPv4AddressProperties()
        {

            Assert.That(IPv4Address.Any.      IsAny,       Is.True);
            Assert.That(IPv4Address.Any.      ToString(),  Is.EqualTo("0.0.0.0"));
            Assert.That(IPv4Address.Localhost.IsLocalhost, Is.True);
            Assert.That(IPv4Address.Localhost.IsLoopback,  Is.True);
            Assert.That(IPv4Address.Localhost.ToString(),  Is.EqualTo("127.0.0.1"));
            Assert.That(IPv4Address.Broadcast.ToString(),  Is.EqualTo("255.255.255.255"));

            Assert.That(IPv4Address.Parse("127.10.20.30").IsLoopback,  Is.True);
            Assert.That(IPv4Address.Parse("224.0.0.1").   IsMulticast, Is.True);
            Assert.That(IPv4Address.Parse("239.255.255.255").IsMulticast, Is.True);
            Assert.That(IPv4Address.Parse("240.0.0.1").   IsMulticast, Is.False);

        }

        #endregion

        #region IPv4AddressByteOperations()

        /// <summary>
        /// Checks IPv4Address byte conversion helpers.
        /// </summary>
        [Test]
        public void IPv4AddressByteOperations()
        {

            var ipv4Address = IPv4Address.Parse("10.11.12.13");

            Assert.That(ipv4Address.GetBytes(), Is.EqualTo(new Byte[] { 10, 11, 12, 13 }));

            var destination = new Byte[4];
            ipv4Address.CopyTo(destination);
            Assert.That(destination, Is.EqualTo(new Byte[] { 10, 11, 12, 13 }));

            var (byte0, byte1, byte2, byte3) = ipv4Address;
            Assert.That(new [] { byte0, byte1, byte2, byte3 }, Is.EqualTo(new Byte[] { 10, 11, 12, 13 }));

            Assert.Throws<ArgumentException>(() => ipv4Address.CopyTo(new Byte[3]));

        }

        #endregion

        #region IPv4AddressFromIPAddress()

        /// <summary>
        /// Checks conversion to and from System.Net.IPAddress.
        /// </summary>
        [Test]
        public void IPv4AddressFromIPAddress()
        {

            var systemIPAddress = System.Net.IPAddress.Parse("192.0.2.123");
            var ipv4Address     = IPv4Address.From(systemIPAddress);

            Assert.That(ipv4Address.ToString(), Is.EqualTo("192.0.2.123"));

            System.Net.IPAddress roundtrip = ipv4Address;
            Assert.That(roundtrip.ToString(), Is.EqualTo("192.0.2.123"));

            Assert.Throws<FormatException>(() => IPv4Address.From(System.Net.IPAddress.IPv6Loopback));

        }

        #endregion

        #region IPv4AddressFromIntegerUsesBitConverterOrder()

        /// <summary>
        /// Checks IPv4Address integer conversion used by BitConverter based packet parsing.
        /// </summary>
        [Test]
        public void IPv4AddressFromIntegerUsesBitConverterOrder()
        {

            Assert.That(IPv4Address.From(0x04030201U).ToString(), Is.EqualTo("1.2.3.4"));

        }

        #endregion

    }

}
