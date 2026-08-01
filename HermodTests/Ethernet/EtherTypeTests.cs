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

using org.GraphDefined.Vanaheimr.Hermod.Ethernet;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Ethernet
{

    /// <summary>
    /// EtherType tests.
    /// </summary>
    [TestFixture]
    public class EtherTypeTests
    {

        #region WellKnownEtherTypeValues()

        /// <summary>
        /// The well-known EtherTypes should carry their IANA/IEEE assigned values.
        /// </summary>
        [Test]
        public void WellKnownEtherTypeValues()
        {

            Assert.That(EtherType.IPv4.                     Value, Is.EqualTo(0x0800));
            Assert.That(EtherType.ARP.                      Value, Is.EqualTo(0x0806));
            Assert.That(EtherType.WakeOnLAN.                Value, Is.EqualTo(0x0842));
            Assert.That(EtherType.RARP.                     Value, Is.EqualTo(0x8035));
            Assert.That(EtherType.VLAN.                     Value, Is.EqualTo(0x8100));
            Assert.That(EtherType.IPv6.                     Value, Is.EqualTo(0x86DD));
            Assert.That(EtherType.MACControl.               Value, Is.EqualTo(0x8808));
            Assert.That(EtherType.SlowProtocols.            Value, Is.EqualTo(0x8809));
            Assert.That(EtherType.MPLSUnicast.              Value, Is.EqualTo(0x8847));
            Assert.That(EtherType.MPLSMulticast.            Value, Is.EqualTo(0x8848));
            Assert.That(EtherType.PPPoEDiscovery.           Value, Is.EqualTo(0x8863));
            Assert.That(EtherType.PPPoESession.             Value, Is.EqualTo(0x8864));
            Assert.That(EtherType.EAPOL.                    Value, Is.EqualTo(0x888E));
            Assert.That(EtherType.ProviderBridging.         Value, Is.EqualTo(0x88A8));
            Assert.That(EtherType.LLDP.                     Value, Is.EqualTo(0x88CC));
            Assert.That(EtherType.MACsec.                   Value, Is.EqualTo(0x88E5));
            Assert.That(EtherType.ProviderBackboneBridging. Value, Is.EqualTo(0x88E7));
            Assert.That(EtherType.PTP.                      Value, Is.EqualTo(0x88F7));
            Assert.That(EtherType.LegacyQinQ.               Value, Is.EqualTo(0x9100));

            Assert.That(EtherType.IPv4.Name,        Is.EqualTo("IPv4"));
            Assert.That(EtherType.IPv4.IsWellKnown, Is.True);

            Assert.That(EtherType.From(0x1234).Name,        Is.Null);
            Assert.That(EtherType.From(0x1234).IsWellKnown, Is.False);

        }

        #endregion

        #region EtherTypeDiscriminatesLengthAndType()

        /// <summary>
        /// Values &lt;= 1500 are an IEEE 802.3 length, values &gt;= 1536 an Ethernet II type,
        /// everything in between is undefined.
        /// </summary>
        [Test]
        public void EtherTypeDiscriminatesLengthAndType()
        {

            Assert.That(EtherType.From(   0).IsLength,      Is.True);
            Assert.That(EtherType.From(1500).IsLength,      Is.True);
            Assert.That(EtherType.From(1500).IsEtherType,   Is.False);
            Assert.That(EtherType.From(1500).IsUndefined,   Is.False);

            Assert.That(EtherType.From(1501).IsUndefined,   Is.True);
            Assert.That(EtherType.From(1535).IsUndefined,   Is.True);
            Assert.That(EtherType.From(1501).IsLength,      Is.False);
            Assert.That(EtherType.From(1501).IsEtherType,   Is.False);

            Assert.That(EtherType.From(1536).IsEtherType,   Is.True);
            Assert.That(EtherType.From(1536).IsUndefined,   Is.False);
            Assert.That(EtherType.IPv4.      IsEtherType,   Is.True);

            Assert.That(EtherType.MaxLengthValue,           Is.EqualTo(1500));
            Assert.That(EtherType.MinEtherTypeValue,        Is.EqualTo(1536));

        }

        #endregion

        #region EtherTypeFromLength()

        /// <summary>
        /// An IEEE 802.3 length field must not exceed 1500 bytes.
        /// </summary>
        [Test]
        public void EtherTypeFromLength()
        {

            Assert.That(EtherType.FromLength(   0).Value, Is.EqualTo(   0));
            Assert.That(EtherType.FromLength(  38).Value, Is.EqualTo(  38));
            Assert.That(EtherType.FromLength(1500).Value, Is.EqualTo(1500));

            Assert.Throws<ArgumentOutOfRangeException>(() => EtherType.FromLength(1501));

        }

        #endregion

        #region EtherTypeRecognizesVLANTagProtocolIdentifiers()

        /// <summary>
        /// The Tag Protocol Identifiers introducing a VLAN tag should be recognized.
        /// </summary>
        [Test]
        public void EtherTypeRecognizesVLANTagProtocolIdentifiers()
        {

            Assert.That(EtherType.From(0x8100).IsVLANTagProtocolIdentifier, Is.True);
            Assert.That(EtherType.From(0x88A8).IsVLANTagProtocolIdentifier, Is.True);
            Assert.That(EtherType.From(0x9100).IsVLANTagProtocolIdentifier, Is.True);
            Assert.That(EtherType.From(0x9200).IsVLANTagProtocolIdentifier, Is.True);
            Assert.That(EtherType.From(0x9300).IsVLANTagProtocolIdentifier, Is.True);

            Assert.That(EtherType.IPv4.        IsVLANTagProtocolIdentifier, Is.False);
            Assert.That(EtherType.IPv6.        IsVLANTagProtocolIdentifier, Is.False);
            Assert.That(EtherType.From(0x88E7).IsVLANTagProtocolIdentifier, Is.False);

        }

        #endregion

        #region ParseEtherType_AcceptsSupportedFormats()

        /// <summary>
        /// EtherTypes should parse well-known names, hexadecimal and decimal values.
        /// </summary>
        [Test]
        public void ParseEtherType_AcceptsSupportedFormats()
        {

            Assert.That(EtherType.Parse("IPv4"),           Is.EqualTo(EtherType.IPv4));
            Assert.That(EtherType.Parse("ipv4"),           Is.EqualTo(EtherType.IPv4));
            Assert.That(EtherType.Parse("  IPv6  "),       Is.EqualTo(EtherType.IPv6));
            Assert.That(EtherType.Parse("0x0800"),         Is.EqualTo(EtherType.IPv4));
            Assert.That(EtherType.Parse("0X86DD"),         Is.EqualTo(EtherType.IPv6));
            Assert.That(EtherType.Parse("2048"),           Is.EqualTo(EtherType.IPv4));
            Assert.That(EtherType.Parse("88CC"),           Is.EqualTo(EtherType.LLDP));

            // The "F" format is round-trippable as well.
            Assert.That(EtherType.Parse("IPv4 (0x0800)"),  Is.EqualTo(EtherType.IPv4));

            // Bare digits are decimal, not hexadecimal!
            Assert.That(EtherType.Parse("0800").Value,     Is.EqualTo(800));

        }

        #endregion

        #region TryParseEtherType_RejectsMalformedInput()

        /// <summary>
        /// EtherTypes should reject malformed strings.
        /// </summary>
        [Test]
        public void TryParseEtherType_RejectsMalformedInput()
        {

            Assert.That(EtherType.TryParse((String?) null,  out _), Is.False);
            Assert.That(EtherType.TryParse("",              out _), Is.False);
            Assert.That(EtherType.TryParse("   ",           out _), Is.False);
            Assert.That(EtherType.TryParse("NoSuchProto",   out _), Is.False);
            Assert.That(EtherType.TryParse("0x12345",       out _), Is.False);
            Assert.That(EtherType.TryParse("65536",         out _), Is.False);
            Assert.That(EtherType.TryParse("-1",            out _), Is.False);

            Assert.That(EtherType.TryParse("NoSuchProto"),          Is.Null);
            Assert.That(EtherType.TryParse("IPv4"),                 Is.EqualTo(EtherType.IPv4));

            Assert.Throws<FormatException>(() => EtherType.Parse("NoSuchProto"));

        }

        #endregion

        #region EtherTypeFormats()

        /// <summary>
        /// Checks the supported EtherType format strings.
        /// </summary>
        [Test]
        public void EtherTypeFormats()
        {

            var ipv4     = EtherType.IPv4;
            var unknown  = EtherType.From(0x1234);

            Assert.That(ipv4.ToString(),        Is.EqualTo("IPv4"));
            Assert.That(ipv4.ToString("G"),     Is.EqualTo("IPv4"));
            Assert.That(ipv4.ToString("X"),     Is.EqualTo("0x0800"));
            Assert.That(ipv4.ToString("x"),     Is.EqualTo("0x0800"));
            Assert.That(ipv4.ToString("D"),     Is.EqualTo("2048"));
            Assert.That(ipv4.ToString("F"),     Is.EqualTo("IPv4 (0x0800)"));

            Assert.That(unknown.ToString(),     Is.EqualTo("0x1234"));
            Assert.That(unknown.ToString("F"),  Is.EqualTo("0x1234"));
            Assert.That(unknown.ToString("D"),  Is.EqualTo("4660"));

            Assert.That(EtherType.LLDP.ToString("x"), Is.EqualTo("0x88cc"));

            Assert.Throws<FormatException>(() => ipv4.ToString("Q"));

        }

        #endregion

        #region EtherTypeSpanParsingAndFormatting()

        /// <summary>
        /// EtherTypes parse from and format into spans without allocating a string.
        /// </summary>
        [Test]
        public void EtherTypeSpanParsingAndFormatting()
        {

            Assert.That(EtherType.TryParse("IPv4".        AsSpan(), out var name),  Is.True);
            Assert.That(name,     Is.EqualTo(EtherType.IPv4));

            Assert.That(EtherType.TryParse("0x86DD".      AsSpan(), out var hex),   Is.True);
            Assert.That(hex,      Is.EqualTo(EtherType.IPv6));

            Assert.That(EtherType.TryParse("LLDP (0x88CC)".AsSpan(), out var full), Is.True);
            Assert.That(full,     Is.EqualTo(EtherType.LLDP));

            Assert.That(EtherType.Parse("  LLDP  ".AsSpan(), null), Is.EqualTo(EtherType.LLDP));

            Assert.That(EtherType.TryParse(ReadOnlySpan<Char>.Empty, out _), Is.False);
            Assert.That(EtherType.TryParse("Nope".AsSpan(),          out _), Is.False);

            Span<Char> destination = stackalloc Char[13];
            Assert.That(EtherType.IPv4.TryFormat(destination, out var charsWritten, "F", null), Is.True);
            Assert.That(charsWritten,                       Is.EqualTo(13));
            Assert.That(new String(destination),            Is.EqualTo("IPv4 (0x0800)"));

            Span<Char> tooSmall = stackalloc Char[12];
            Assert.That(EtherType.IPv4.TryFormat(tooSmall, out charsWritten, "F", null), Is.False);
            Assert.That(charsWritten,                       Is.EqualTo(0));

        }

        #endregion

        #region EtherTypeByteOperations()

        /// <summary>
        /// EtherTypes are transmitted in network byte order.
        /// </summary>
        [Test]
        public void EtherTypeByteOperations()
        {

            Assert.That(EtherType.IPv4.GetBytes(), Is.EqualTo(new Byte[] { 0x08, 0x00 }));
            Assert.That(EtherType.IPv6.GetBytes(), Is.EqualTo(new Byte[] { 0x86, 0xDD }));

            Assert.That(EtherType.From(new Byte[] { 0x08, 0x00 }), Is.EqualTo(EtherType.IPv4));
            Assert.That(EtherType.From(new Byte[] { 0x86, 0xDD }), Is.EqualTo(EtherType.IPv6));

            var destination = new Byte[2];
            EtherType.LLDP.WriteTo(destination);
            Assert.That(destination, Is.EqualTo(new Byte[] { 0x88, 0xCC }));

            Assert.That(EtherType.TryFrom(new Byte[] { 0x08, 0x00 }), Is.EqualTo(EtherType.IPv4));
            Assert.That(EtherType.TryFrom(new Byte[3]),               Is.Null);

            Assert.Throws<ArgumentException>(() => EtherType.From(new Byte[3]));
            Assert.Throws<ArgumentException>(() => EtherType.IPv4.WriteTo(new Byte[1]));

        }

        #endregion

        #region EtherTypeEqualityAndOrdering()

        /// <summary>
        /// Checks equality, ordering and hash codes.
        /// </summary>
        [Test]
        public void EtherTypeEqualityAndOrdering()
        {

            Assert.That(EtherType.IPv4 == EtherType.From(0x0800), Is.True);
            Assert.That(EtherType.IPv4 != EtherType.IPv6,         Is.True);
            Assert.That(EtherType.IPv4 <  EtherType.IPv6,         Is.True);
            Assert.That(EtherType.IPv6 >  EtherType.IPv4,         Is.True);
            Assert.That(EtherType.IPv4 <= EtherType.From(0x0800), Is.True);
            Assert.That(EtherType.IPv4 >= EtherType.From(0x0800), Is.True);

            Assert.That(EtherType.IPv4.Equals((Object) EtherType.From(0x0800)), Is.True);
            Assert.That(EtherType.IPv4.Equals((Object) "IPv4"),                 Is.False);

            Assert.That(EtherType.IPv4.GetHashCode(), Is.EqualTo(EtherType.From(0x0800).GetHashCode()));

            Assert.Throws<ArgumentException>(() => EtherType.IPv4.CompareTo((Object) "IPv4"));

            var sorted = new[] { EtherType.IPv6, EtherType.ARP, EtherType.IPv4 }.Order().ToArray();
            Assert.That(sorted, Is.EqualTo(new[] { EtherType.IPv4, EtherType.ARP, EtherType.IPv6 }));

        }

        #endregion

        #region EtherTypeNullOrZeroExtensions()

        /// <summary>
        /// Checks the nullable EtherType extension methods.
        /// </summary>
        [Test]
        public void EtherTypeNullOrZeroExtensions()
        {

            EtherType? nothing  = null;
            EtherType? zero     = EtherType.From(0);
            EtherType? ipv4     = EtherType.IPv4;

            Assert.That(nothing.IsNullOrZero(),     Is.True);
            Assert.That(zero.   IsNullOrZero(),     Is.True);
            Assert.That(ipv4.   IsNullOrZero(),     Is.False);

            Assert.That(nothing.IsNotNullOrZero(),  Is.False);
            Assert.That(zero.   IsNotNullOrZero(),  Is.False);
            Assert.That(ipv4.   IsNotNullOrZero(),  Is.True);

        }

        #endregion

    }

}
