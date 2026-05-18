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

using System.Net.NetworkInformation;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.Ethernet;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Ethernet
{

    /// <summary>
    /// MACAddress tests.
    /// </summary>
    [TestFixture]
    public class MACAddressTests
    {

        #region ParseMACAddressString_AcceptsSupportedFormats()

        /// <summary>
        /// MACAddress should parse the common textual formats.
        /// </summary>
        [Test]
        public void ParseMACAddressString_AcceptsSupportedFormats()
        {

            var expected = MACAddress.Parse("AA:BB:CC:DD:EE:FF");

            Assert.That(MACAddress.Parse("aa:bb:cc:dd:ee:ff"), Is.EqualTo(expected));
            Assert.That(MACAddress.Parse("AA-BB-CC-DD-EE-FF"), Is.EqualTo(expected));
            Assert.That(MACAddress.Parse("AABBCCDDEEFF"),      Is.EqualTo(expected));
            Assert.That(MACAddress.Parse("aabb.ccdd.eeff"),    Is.EqualTo(expected));
            Assert.That(expected.ToString(),                    Is.EqualTo("AA:BB:CC:DD:EE:FF"));

        }

        #endregion

        #region TryParseMACAddressString_RejectsMalformedInput()

        /// <summary>
        /// MACAddress should reject malformed strings.
        /// </summary>
        [Test]
        public void TryParseMACAddressString_RejectsMalformedInput()
        {

            Assert.That(MACAddress.TryParse((String?) null, out _), Is.False);
            Assert.That(MACAddress.TryParse("",              out _), Is.False);
            Assert.That(MACAddress.TryParse("AA:BB:CC:DD:EE", out _), Is.False);
            Assert.That(MACAddress.TryParse("AA:BB:CC:DD:EE:FF:00", out _), Is.False);
            Assert.That(MACAddress.TryParse("AA:BB-CC:DD:EE:FF", out _), Is.False);
            Assert.That(MACAddress.TryParse("AA:BB:CC:DD:EE:GG", out _), Is.False);
            Assert.That(MACAddress.TryParse("aabb.ccdd.ee",  out _), Is.False);
            Assert.That(MACAddress.TryParse("aabb.ccdd.eeff.0011", out _), Is.False);

            Assert.Throws<FormatException>(() => MACAddress.Parse("AA:BB:CC:DD:EE"));

        }

        #endregion

        #region TryReadMACAddress_ConsumesOnlyTheAddress()

        /// <summary>
        /// MACAddress.TryRead should read an address prefix and report the consumed characters.
        /// </summary>
        [Test]
        public void TryReadMACAddress_ConsumesOnlyTheAddress()
        {

            Assert.That(MACAddress.TryRead("  aa:bb:cc:dd:ee:ff trailing", out var separated, out var separatedConsumed), Is.True);
            Assert.That(separated.ToString(),    Is.EqualTo("AA:BB:CC:DD:EE:FF"));
            Assert.That(separatedConsumed,       Is.EqualTo(19));

            Assert.That(MACAddress.TryRead("aabb.ccdd.eeff trailing", out var cisco, out var ciscoConsumed), Is.True);
            Assert.That(cisco.ToString("Cisco"), Is.EqualTo("aabb.ccdd.eeff"));
            Assert.That(ciscoConsumed,           Is.EqualTo(14));

            Assert.That(MACAddress.TryRead("AABBCCDDEEFF trailing", out var plain, out var plainConsumed), Is.True);
            Assert.That(plain.ToString("N"),     Is.EqualTo("AABBCCDDEEFF"));
            Assert.That(plainConsumed,           Is.EqualTo(12));

            Assert.That(MACAddress.TryRead("not-a-mac", out _, out var invalidConsumed), Is.False);
            Assert.That(invalidConsumed,         Is.EqualTo(0));

        }

        #endregion

        #region MACAddressFormats()

        /// <summary>
        /// Checks supported MACAddress format strings.
        /// </summary>
        [Test]
        public void MACAddressFormats()
        {

            var macAddress = MACAddress.Parse("AA:BB:CC:DD:EE:FF");

            Assert.That(macAddress.ToString("G"),     Is.EqualTo("AA:BB:CC:DD:EE:FF"));
            Assert.That(macAddress.ToString("C"),     Is.EqualTo("AA:BB:CC:DD:EE:FF"));
            Assert.That(macAddress.ToString("D"),     Is.EqualTo("AA-BB-CC-DD-EE-FF"));
            Assert.That(macAddress.ToString("N"),     Is.EqualTo("AABBCCDDEEFF"));
            Assert.That(macAddress.ToString("c"),     Is.EqualTo("aa:bb:cc:dd:ee:ff"));
            Assert.That(macAddress.ToString("d"),     Is.EqualTo("aa-bb-cc-dd-ee-ff"));
            Assert.That(macAddress.ToString("n"),     Is.EqualTo("aabbccddeeff"));
            Assert.That(macAddress.ToString("Cisco"), Is.EqualTo("aabb.ccdd.eeff"));

            Assert.Throws<FormatException>(() => macAddress.ToString("X"));

        }

        #endregion

        #region TryFormatMACAddress()

        /// <summary>
        /// Checks span formatting.
        /// </summary>
        [Test]
        public void TryFormatMACAddress()
        {

            var macAddress = MACAddress.Parse("AA:BB:CC:DD:EE:FF");

            Span<Char> destination = stackalloc Char[17];
            Assert.That(macAddress.TryFormat(destination, out var charsWritten, "c", null), Is.True);
            Assert.That(charsWritten, Is.EqualTo(17));
            Assert.That(new String(destination), Is.EqualTo("aa:bb:cc:dd:ee:ff"));

            Span<Char> tooSmall = stackalloc Char[16];
            Assert.That(macAddress.TryFormat(tooSmall, out charsWritten, "G", null), Is.False);
            Assert.That(charsWritten, Is.EqualTo(0));

        }

        #endregion

        #region MACAddressByteOperations()

        /// <summary>
        /// Checks byte conversion helpers.
        /// </summary>
        [Test]
        public void MACAddressByteOperations()
        {

            var macAddress = MACAddress.From(new Byte[] { 0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E });

            Assert.That(macAddress.GetBytes(), Is.EqualTo(new Byte[] { 0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E }));

            var destination = new Byte[6];
            macAddress.CopyTo(destination);
            Assert.That(destination, Is.EqualTo(new Byte[] { 0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E }));

            var (byte0, byte1, byte2, byte3, byte4, byte5) = macAddress;
            Assert.That(new [] { byte0, byte1, byte2, byte3, byte4, byte5 }, Is.EqualTo(new Byte[] { 0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E }));

            Assert.That(macAddress.AsEUI64(),             Is.EqualTo(new Byte[] { 0x00, 0x1A, 0x2B, 0xFF, 0xFE, 0x3C, 0x4D, 0x5E }));
            Assert.That(macAddress.AsIPv6ModifiedEUI64(), Is.EqualTo(new Byte[] { 0x02, 0x1A, 0x2B, 0xFF, 0xFE, 0x3C, 0x4D, 0x5E }));

            Assert.Throws<ArgumentException>(() => MACAddress.From(new Byte[5]));
            Assert.That(MACAddress.TryFrom(new Byte[5]), Is.Null);
            Assert.Throws<ArgumentException>(() => macAddress.CopyTo(new Byte[5]));

        }

        #endregion

        #region MACAddressPhysicalAddressRoundtrip()

        /// <summary>
        /// Checks conversion to and from System.Net.NetworkInformation.PhysicalAddress.
        /// </summary>
        [Test]
        public void MACAddressPhysicalAddressRoundtrip()
        {

            var macAddress      = MACAddress.Parse("AA:BB:CC:DD:EE:FF");
            var physicalAddress = macAddress.ToPhysicalAddress();

            Assert.That(physicalAddress, Is.EqualTo(PhysicalAddress.Parse("AABBCCDDEEFF")));
            Assert.That(MACAddress.FromPhysicalAddress(physicalAddress), Is.EqualTo(macAddress));

        }

        #endregion

        #region MACAddressProperties()

        /// <summary>
        /// Checks MACAddress classification helpers.
        /// </summary>
        [Test]
        public void MACAddressProperties()
        {

            Assert.That(MACAddress.Zero.IsZero,              Is.True);
            Assert.That(MACAddress.Zero.IsZeroOrBroadcast,   Is.True);
            Assert.That(MACAddress.Broadcast.IsBroadcast,    Is.True);
            Assert.That(MACAddress.Broadcast.IsZeroOrBroadcast, Is.True);
            Assert.That(MACAddress.Broadcast.IsUnicast,      Is.False);

            var unicast = MACAddress.Parse("00:1A:2B:3C:4D:5E");
            Assert.That(unicast.IsUnicast,                   Is.True);
            Assert.That(unicast.IsMulticast,                 Is.False);
            Assert.That(unicast.IsGloballyAdministered,      Is.True);
            Assert.That(unicast.OUI,                         Is.EqualTo("00:1A:2B"));

            var local = MACAddress.Parse("02:00:00:00:00:01");
            Assert.That(local.IsLocallyAdministered,         Is.True);
            Assert.That(local.IsGloballyAdministered,        Is.False);

            Assert.That(MACAddress.Parse("01:00:5E:00:00:01").IsIPv4Multicast,      Is.True);
            Assert.That(MACAddress.Parse("33:33:00:00:00:01").IsIPv6Multicast,      Is.True);
            Assert.That(MACAddress.Parse("00:00:5E:00:01:7B").IsVRRP,               Is.True);
            Assert.That(MACAddress.Parse("01:80:C2:00:00:00").IsSTP,                Is.True);
            Assert.That(MACAddress.Parse("01:80:C2:00:00:0E").IsLLDP,               Is.True);
            Assert.That(MACAddress.Parse("01:80:C2:00:00:01").IsPauseFrameAddress,  Is.True);
            Assert.That(MACAddress.Parse("01:80:C2:00:00:02").IsLACP,               Is.True);
            Assert.That(MACAddress.Parse("01:80:C2:00:00:03").IsIEEE8021X,          Is.True);

        }

        #endregion

        #region MACAddressMulticastMappings()

        /// <summary>
        /// Checks IPv4 and IPv6 multicast mapping helpers.
        /// </summary>
        [Test]
        public void MACAddressMulticastMappings()
        {

            Assert.That(MACAddress.FromIPv4Multicast(IPv4Address.Parse("224.0.0.1")).ToString(),       Is.EqualTo("01:00:5E:00:00:01"));
            Assert.That(MACAddress.FromIPv4Multicast(IPv4Address.Parse("239.255.255.250")).ToString(), Is.EqualTo("01:00:5E:7F:FF:FA"));
            Assert.Throws<ArgumentException>(() => MACAddress.FromIPv4Multicast(IPv4Address.Parse("192.0.2.1")));

            Assert.That(MACAddress.FromIPv6Multicast(IPv6Address.Parse("ff02::1")).ToString(), Is.EqualTo("33:33:00:00:00:01"));
            Assert.Throws<ArgumentException>(() => MACAddress.FromIPv6Multicast(IPv6Address.Parse("2001:db8::1")));

        }

        #endregion

        #region RandomMACAddressesSetExpectedBits()

        /// <summary>
        /// Checks the guaranteed bits on random local MAC addresses.
        /// </summary>
        [Test]
        public void RandomMACAddressesSetExpectedBits()
        {

            var unicast   = MACAddress.RandomLocalUnicast();
            var multicast = MACAddress.RandomLocalMulticast();

            Assert.That(unicast.IsLocallyAdministered,   Is.True);
            Assert.That(unicast.IsUnicast,               Is.True);
            Assert.That(multicast.IsLocallyAdministered, Is.True);
            Assert.That(multicast.IsMulticast,           Is.True);

        }

        #endregion

    }

}
