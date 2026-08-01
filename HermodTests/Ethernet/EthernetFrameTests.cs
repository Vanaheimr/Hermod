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

using System.Text;
using System.Buffers;

using org.GraphDefined.Vanaheimr.Hermod.Ethernet;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Ethernet
{

    /// <summary>
    /// EthernetFrame tests.
    /// </summary>
    [TestFixture]
    public class EthernetFrameTests
    {

        #region Data

        private static readonly MACAddress broadcast    = MACAddress.Broadcast;
        private static readonly MACAddress source       = MACAddress.Parse("00:1A:2B:3C:4D:5E");
        private static readonly MACAddress destination  = MACAddress.Parse("AA:BB:CC:DD:EE:FF");
        private static readonly MACAddress stpBridges   = MACAddress.Parse("01:80:C2:00:00:00");

        #endregion

        #region (private) Hex(Text) / Pad(Text, ByteLength)

        /// <summary>
        /// Convert the given hexadecimal text, which may contain whitespace, into bytes.
        /// </summary>
        private static Byte[] Hex(String Text)

            => Convert.FromHexString(
                   new String(
                       Text.Where(Char.IsAsciiHexDigit).ToArray()
                   )
               );


        /// <summary>
        /// Zero-pad the given hexadecimal text to the given number of bytes.
        /// </summary>
        private static String Pad(String Text, Int32 ByteLength)

            => Text.PadRight(2 * ByteLength, '0');

        #endregion


        #region ParseUntaggedEthernetIIFrame()

        /// <summary>
        /// An untagged Ethernet II frame has a 14 byte header.
        /// </summary>
        [Test]
        public void ParseUntaggedEthernetIIFrame()
        {

            var bytes = Hex("FFFFFFFFFFFF"  +  // Destination: broadcast
                            "001A2B3C4D5E"  +  // Source
                            "0806"          +  // EtherType: ARP

                            Pad("0001"           +  // Hardware type:   Ethernet
                                "0800"           +  // Protocol type:   IPv4
                                "06"             +  // Hardware length
                                "04"             +  // Protocol length
                                "0001"           +  // Operation:       request
                                "001A2B3C4D5E"   +  // Sender hardware address
                                "C0A80001"       +  // Sender protocol address
                                "000000000000"   +  // Target hardware address
                                "C0A80002",         // Target protocol address
                                46));               // ... padded to the minimum client data size

            Assert.That(bytes.Length, Is.EqualTo(60));

            Assert.That(EthernetFrame.TryParse(bytes, out var frame), Is.True);
            Assert.That(frame,                          Is.Not.Null);
            Assert.That(frame!.DestinationAddress,       Is.EqualTo(broadcast));
            Assert.That(frame.SourceAddress,            Is.EqualTo(source));
            Assert.That(frame.EtherTypeOrLength,        Is.EqualTo(EtherType.ARP));
            Assert.That(frame.PayloadProtocol,          Is.EqualTo(EtherType.ARP));
            Assert.That(frame.IsEthernetII,             Is.True);
            Assert.That(frame.IsIEEE8023,               Is.False);
            Assert.That(frame.IsVLANTagged,             Is.False);
            Assert.That(frame.VLANTags,                 Is.Empty);
            Assert.That(frame.OuterVLANTag,             Is.Null);
            Assert.That(frame.InnerVLANTag,             Is.Null);
            Assert.That(frame.VLANId,                   Is.Null);
            Assert.That(frame.HeaderLength,             Is.EqualTo(14));
            Assert.That(frame.IsBroadcast,              Is.True);
            Assert.That(frame.IsMulticast,              Is.True);
            Assert.That(frame.IsUnicast,                Is.False);
            Assert.That(frame.IsJumboFrame,             Is.False);
            Assert.That(frame.FrameCheckSequence,       Is.Null);

            // For an Ethernet II frame the padding is indistinguishable from the payload!
            Assert.That(frame.Payload.Length,           Is.EqualTo(46));
            Assert.That(frame.PaddingLength,            Is.EqualTo(0));

            // The frame round-trips byte for byte.
            Assert.That(frame.GetBytes(),               Is.EqualTo(bytes));

        }

        #endregion

        #region ParseSingleVLANTaggedFrame()

        /// <summary>
        /// A single IEEE 802.1Q tag adds 4 bytes between the source address
        /// and the EtherType.
        /// </summary>
        [Test]
        public void ParseSingleVLANTaggedFrame()
        {

            var bytes = Hex("AABBCCDDEEFF"  +  // Destination
                            "001A2B3C4D5E"  +  // Source
                            "8100"          +  // TPID: IEEE 802.1Q
                            "2064"          +  // TCI:  PCP 1, DEI 0, VID 100
                            "0800"          +  // EtherType: IPv4

                            Pad("45000014000100004006BEEF" +  // An IPv4 header ...
                                "0A000001" + "0A000002", 46));

            Assert.That(bytes.Length, Is.EqualTo(64));

            Assert.That(EthernetFrame.TryParse(bytes, out var frame), Is.True);
            Assert.That(frame,                          Is.Not.Null);
            Assert.That(frame!.IsVLANTagged,             Is.True);
            Assert.That(frame.IsQinQ,                   Is.False);
            Assert.That(frame.VLANTags.Count,           Is.EqualTo(1));
            Assert.That(frame.HeaderLength,             Is.EqualTo(18));
            Assert.That(frame.EtherTypeOrLength,        Is.EqualTo(EtherType.IPv4));
            Assert.That(frame.IsUnicast,                Is.True);

            var vlanTag = frame.OuterVLANTag!.Value;

            Assert.That(vlanTag.TPID,                   Is.EqualTo(EtherType.VLAN));
            Assert.That(vlanTag.VID.Value,              Is.EqualTo(100));
            Assert.That(vlanTag.PCP,                    Is.EqualTo(1));
            Assert.That(vlanTag.DEI,                    Is.False);
            Assert.That(vlanTag.IsCustomerTag,          Is.True);

            Assert.That(frame.VLANId,                   Is.EqualTo(VLANId.From(100)));
            Assert.That(frame.InnerVLANTag,             Is.EqualTo(frame.OuterVLANTag));

            Assert.That(frame.GetBytes(),               Is.EqualTo(bytes));

        }

        #endregion

        #region ParseQinQTaggedFrame()

        /// <summary>
        /// IEEE 802.1ad stacks a service tag in front of the customer tag.
        /// </summary>
        [Test]
        public void ParseQinQTaggedFrame()
        {

            var bytes = Hex("AABBCCDDEEFF"  +  // Destination
                            "001A2B3C4D5E"  +  // Source
                            "88A8"          +  // Outer TPID: IEEE 802.1ad S-Tag
                            "00C8"          +  // Outer TCI:  PCP 0, DEI 0, VID 200
                            "8100"          +  // Inner TPID: IEEE 802.1Q C-Tag
                            "B064"          +  // Inner TCI:  PCP 5, DEI 1, VID 100
                            "86DD"          +  // EtherType: IPv6

                            Pad("6000000000003B40", 46));

            Assert.That(bytes.Length, Is.EqualTo(68));

            Assert.That(EthernetFrame.TryParse(bytes, out var frame), Is.True);
            Assert.That(frame,                          Is.Not.Null);
            Assert.That(frame!.IsVLANTagged,             Is.True);
            Assert.That(frame.IsQinQ,                   Is.True);
            Assert.That(frame.VLANTags.Count,           Is.EqualTo(2));
            Assert.That(frame.HeaderLength,             Is.EqualTo(22));
            Assert.That(frame.EtherTypeOrLength,        Is.EqualTo(EtherType.IPv6));

            var outer = frame.OuterVLANTag!.Value;
            var inner = frame.InnerVLANTag!.Value;

            Assert.That(outer.TPID,                     Is.EqualTo(EtherType.ProviderBridging));
            Assert.That(outer.VID.Value,                Is.EqualTo(200));
            Assert.That(outer.PCP,                      Is.EqualTo(0));
            Assert.That(outer.IsServiceTag,             Is.True);

            Assert.That(inner.TPID,                     Is.EqualTo(EtherType.VLAN));
            Assert.That(inner.VID.Value,                Is.EqualTo(100));
            Assert.That(inner.PCP,                      Is.EqualTo(5));
            Assert.That(inner.DEI,                      Is.True);
            Assert.That(inner.Priority,                 Is.EqualTo(PCPPriorities.Voice));
            Assert.That(inner.IsCustomerTag,            Is.True);

            // The outermost tag decides the VLAN of the frame.
            Assert.That(frame.VLANId,                   Is.EqualTo(VLANId.From(200)));

            Assert.That(frame.GetBytes(),               Is.EqualTo(bytes));

        }

        #endregion

        #region BuildAndParseFrameRoundtrip()

        /// <summary>
        /// A frame built from its parts serializes and parses back to itself.
        /// </summary>
        [Test]
        public void BuildAndParseFrameRoundtrip()
        {

            var payload  = Encoding.ASCII.GetBytes("Hello Vanaheimr, this is a nice little Ethernet payload!");

            var frame    = new EthernetFrame(
                               destination,
                               source,
                               EtherType.IPv4,
                               VLANTag.CustomerTag(4094, (Byte) PCPPriorities.Video),
                               payload
                           );

            var bytes    = frame.GetBytes();

            Assert.That(payload.Length, Is.GreaterThan(EthernetFrame.MinPayloadLength));
            Assert.That(bytes.Length,   Is.EqualTo(18 + payload.Length));

            Assert.That(EthernetFrame.TryParse(bytes, out var parsed), Is.True);
            Assert.That(parsed,                     Is.Not.Null);
            Assert.That(parsed!.DestinationAddress,  Is.EqualTo(destination));
            Assert.That(parsed.SourceAddress,       Is.EqualTo(source));
            Assert.That(parsed.EtherTypeOrLength,   Is.EqualTo(EtherType.IPv4));
            Assert.That(parsed.Payload.ToArray(),             Is.EqualTo(payload));
            Assert.That(parsed.VLANTags,            Is.EqualTo(frame.VLANTags));
            Assert.That(parsed.VLANId,              Is.EqualTo(VLANId.From(4094)));
            Assert.That(parsed.GetBytes(),          Is.EqualTo(bytes));

        }

        #endregion

        #region ShortPayloadsArePaddedToTheMinimumFrameSize()

        /// <summary>
        /// The MAC client data is padded to 46 bytes, so that an untagged frame
        /// reaches the minimum frame size of 64 bytes including the FCS. A VLAN
        /// tag is inserted after padding, hence a tagged frame is 68 bytes.
        /// </summary>
        [Test]
        public void ShortPayloadsArePaddedToTheMinimumFrameSize()
        {

            var untagged = new EthernetFrame(
                               destination,
                               source,
                               EtherType.IPv4,
                               new Byte[] { 0x01, 0x02, 0x03, 0x04 }
                           );

            Assert.That(untagged.Payload.Length,     Is.EqualTo( 4));
            Assert.That(untagged.PaddingLength,      Is.EqualTo(42));
            Assert.That(untagged.FrameLength,        Is.EqualTo(64));

            var padded = untagged.GetBytes();
            Assert.That(padded.Length,               Is.EqualTo(60));
            Assert.That(padded[14..18],              Is.EqualTo(new Byte[] { 0x01, 0x02, 0x03, 0x04 }));
            Assert.That(padded[18..].All(b => b == 0), Is.True);

            var unpadded = untagged.GetBytes(AddPadding: false);
            Assert.That(unpadded.Length,             Is.EqualTo(18));

            var tagged = untagged.PushVLANTag(VLANTag.CustomerTag(100));
            Assert.That(tagged.PaddingLength,        Is.EqualTo(42));
            Assert.That(tagged.FrameLength,          Is.EqualTo(68));
            Assert.That(tagged.GetBytes().Length,    Is.EqualTo(64));

            Assert.That(EthernetFrame.MinFrameLength,       Is.EqualTo(64));
            Assert.That(EthernetFrame.MinPayloadLength,     Is.EqualTo(46));
            Assert.That(EthernetFrame.MaxPayloadLength,     Is.EqualTo(1500));
            Assert.That(EthernetFrame.UntaggedHeaderLength, Is.EqualTo(14));
            Assert.That(EthernetFrame.FCSLength,            Is.EqualTo(4));

        }

        #endregion

        #region FrameCheckSequence()

        /// <summary>
        /// The Frame Check Sequence is an IEEE 802.3 CRC-32, transmitted
        /// least significant octet first.
        /// </summary>
        [Test]
        public void FrameCheckSequence()
        {

            // The canonical CRC-32 check value.
            Assert.That(EthernetFrame.ComputeFCS(Encoding.ASCII.GetBytes("123456789")),
                        Is.EqualTo(0xCBF43926));

            var frame       = new EthernetFrame(
                                  destination,
                                  source,
                                  EtherType.IPv4,
                                  Encoding.ASCII.GetBytes("An Ethernet payload with a Frame Check Sequence.")
                              );

            var withoutFCS  = frame.GetBytes();
            var withFCS     = frame.GetBytes(IncludeFCS: true);

            Assert.That(withFCS.Length, Is.EqualTo(withoutFCS.Length + 4));

            var expectedFCS = EthernetFrame.ComputeFCS(withoutFCS);

            // The FCS is transmitted least significant octet first!
            Assert.That(withFCS[^4..], Is.EqualTo(new [] {
                                                      (Byte)  (expectedFCS        & 0xFF),
                                                      (Byte) ((expectedFCS >>  8) & 0xFF),
                                                      (Byte) ((expectedFCS >> 16) & 0xFF),
                                                      (Byte) ((expectedFCS >> 24) & 0xFF)
                                                  }));

            // A frame including its FCS always yields the same CRC-32 residue.
            Assert.That(EthernetFrame.VerifyFCS(withFCS), Is.True);

            var corrupted   = withFCS.ToArray();
            corrupted[20]  ^= 0xFF;
            Assert.That(EthernetFrame.VerifyFCS(corrupted), Is.False);

            // Parsing a frame including its FCS.
            Assert.That(EthernetFrame.TryParse(withFCS, out var parsed, IncludesFCS: true), Is.True);
            Assert.That(parsed,                     Is.Not.Null);
            Assert.That(parsed!.FrameCheckSequence,  Is.EqualTo(expectedFCS));
            Assert.That(parsed.HasValidFCS,         Is.True);
            Assert.That(parsed.Payload.ToArray(),   Is.EqualTo(frame.Payload.ToArray()));
            Assert.That(parsed.GetBytes(IncludeFCS: true), Is.EqualTo(withFCS));

            // A frame without a captured FCS cannot be verified.
            Assert.That(EthernetFrame.TryParse(withoutFCS, out var stripped), Is.True);
            Assert.That(stripped!.FrameCheckSequence, Is.Null);
            Assert.That(stripped. HasValidFCS,        Is.False);

            // A frame that is nothing but a Frame Check Sequence.
            Assert.That(EthernetFrame.TryParse(new Byte[4], out _, IncludesFCS: true), Is.False);

        }

        #endregion

        #region ParseIEEE8023FrameWithLLCHeader()

        /// <summary>
        /// An IEEE 802.3 frame carries the length of the MAC client data instead of
        /// an EtherType and starts with an IEEE 802.2 LLC header - like an STP BPDU.
        /// </summary>
        [Test]
        public void ParseIEEE8023FrameWithLLCHeader()
        {

            var bytes = Hex("0180C2000000"  +  // Destination: STP bridge group address
                            "001A2B3C4D5E"  +  // Source
                            "0026"          +  // Length: 38 bytes of MAC client data

                            Pad("424203"             +  // LLC: DSAP 0x42, SSAP 0x42, control 0x03 (UI)
                                "0000"               +  // Protocol identifier
                                "00"                 +  // Protocol version
                                "00"                 +  // BPDU type: configuration
                                "00"                 +  // Flags
                                "8000001A2B3C4D5E"   +  // Root identifier
                                "00000000"           +  // Root path cost
                                "8000001A2B3C4D5E"   +  // Bridge identifier
                                "8001",                 // Port identifier
                                46));                   // ... padded to the minimum client data size

            Assert.That(bytes.Length, Is.EqualTo(60));

            Assert.That(EthernetFrame.TryParse(bytes, out var frame), Is.True);
            Assert.That(frame,                          Is.Not.Null);
            Assert.That(frame!.IsIEEE8023,               Is.True);
            Assert.That(frame.IsEthernetII,             Is.False);
            Assert.That(frame.DestinationAddress,       Is.EqualTo(stpBridges));
            Assert.That(frame.DestinationAddress.IsSTP, Is.True);
            Assert.That(frame.EtherTypeOrLength.Value,  Is.EqualTo(38));

            // The length field delimits the payload exactly, everything beyond is padding.
            Assert.That(frame.Payload.Length,           Is.EqualTo(38));
            Assert.That(frame.PaddingLength,            Is.EqualTo( 8));

            Assert.That(frame.TryGetLLCHeader(out var llcHeader), Is.True);
            Assert.That(llcHeader.DSAP,                 Is.EqualTo(LLCHeader.STPServiceAccessPoint));
            Assert.That(llcHeader.SSAP,                 Is.EqualTo(LLCHeader.STPServiceAccessPoint));
            Assert.That(llcHeader.Control,              Is.EqualTo(LLCHeader.UnnumberedInformation));
            Assert.That(llcHeader.ControlLength,        Is.EqualTo(1));
            Assert.That(llcHeader.Length,               Is.EqualTo(3));
            Assert.That(llcHeader.IsUnnumberedFormat,   Is.True);
            Assert.That(llcHeader.IsSNAP,               Is.False);
            Assert.That(llcHeader.IsIndividualDSAP,     Is.True);
            Assert.That(llcHeader.IsCommand,            Is.True);
            Assert.That(llcHeader.GetBytes(),           Is.EqualTo(Hex("424203")));
            Assert.That(llcHeader.ToString(),           Does.StartWith("LLC DSAP 0x42"));

            Assert.That(frame.TryGetSNAPHeader(out _),  Is.False);
            Assert.That(frame.PayloadProtocol,          Is.Null);
            Assert.That(frame.GetLLCPayload().Length,   Is.EqualTo(35));

            // The frame round-trips, since the padding is all zeroes.
            Assert.That(frame.GetBytes(),               Is.EqualTo(bytes));

        }

        #endregion

        #region ParseIEEE8023FrameWithSNAPHeader()

        /// <summary>
        /// An LLC/SNAP header re-introduces the EtherType that the IEEE 802.3
        /// length field displaced.
        /// </summary>
        [Test]
        public void ParseIEEE8023FrameWithSNAPHeader()
        {

            var payload = Encoding.ASCII.GetBytes("SNAP encapsulated payload, RFC 1042.");

            var frame   = EthernetFrame.CreateIEEE8023SNAP(
                              destination,
                              source,
                              new SNAPHeader(EtherType.IPv4),
                              payload
                          );

            Assert.That(frame.IsIEEE8023,              Is.True);
            Assert.That(frame.EtherTypeOrLength.Value, Is.EqualTo(3 + 5 + payload.Length));
            Assert.That(frame.Payload.Length,          Is.EqualTo(3 + 5 + payload.Length));

            Assert.That(frame.TryGetLLCHeader(out var llcHeader), Is.True);
            Assert.That(llcHeader.IsSNAP,              Is.True);
            Assert.That(llcHeader,                     Is.EqualTo(LLCHeader.SNAP));
            Assert.That(llcHeader.ToString(),          Is.EqualTo("LLC/SNAP"));

            Assert.That(frame.TryGetSNAPHeader(out var snapHeader), Is.True);
            Assert.That(snapHeader.OUI,                Is.EqualTo(SNAPHeader.RFC1042OUI));
            Assert.That(snapHeader.IsRFC1042,          Is.True);
            Assert.That(snapHeader.ProtocolId,         Is.EqualTo(EtherType.IPv4));
            Assert.That(snapHeader.GetBytes(),         Is.EqualTo(Hex("0000000800")));

            Assert.That(frame.PayloadProtocol,         Is.EqualTo(EtherType.IPv4));
            Assert.That(frame.GetLLCPayload().ToArray(),         Is.EqualTo(payload));

            // The LLC/SNAP prefix on the wire.
            var bytes = frame.GetBytes();
            Assert.That(bytes[14..22], Is.EqualTo(Hex("AAAA03" + "000000" + "0800")));

            Assert.That(EthernetFrame.TryParse(bytes, out var parsed), Is.True);
            Assert.That(parsed!.Payload.ToArray(),     Is.EqualTo(frame.Payload.ToArray()));
            Assert.That(parsed. GetLLCPayload().ToArray(),       Is.EqualTo(payload));
            Assert.That(parsed. PayloadProtocol,       Is.EqualTo(EtherType.IPv4));

        }

        #endregion

        #region PushAndPopVLANTags()

        /// <summary>
        /// Pushing and popping VLAN tags models what a VLAN-aware bridge does
        /// at its ingress and egress ports.
        /// </summary>
        [Test]
        public void PushAndPopVLANTags()
        {

            var untagged   = new EthernetFrame(
                                 destination,
                                 source,
                                 EtherType.IPv4,
                                 Encoding.ASCII.GetBytes("A payload long enough to avoid any padding at all.")
                             );

            var customer   = untagged.PushVLANTag(VLANTag.CustomerTag(100));
            var provider   = customer.PushVLANTag(VLANTag.ServiceTag (200));

            Assert.That(untagged.VLANTags.Count,     Is.EqualTo(0));
            Assert.That(customer.VLANTags.Count,     Is.EqualTo(1));
            Assert.That(provider.VLANTags.Count,     Is.EqualTo(2));

            Assert.That(provider.OuterVLANTag,       Is.EqualTo(VLANTag.ServiceTag (200)));
            Assert.That(provider.InnerVLANTag,       Is.EqualTo(VLANTag.CustomerTag(100)));

            Assert.That(provider.GetBytes()[12..16], Is.EqualTo(Hex("88A800C8")));
            Assert.That(provider.GetBytes()[16..20], Is.EqualTo(Hex("81000064")));
            Assert.That(provider.GetBytes()[20..22], Is.EqualTo(Hex("0800")));

            Assert.That(provider.PopVLANTag().VLANTags,        Is.EqualTo(customer.VLANTags));
            Assert.That(provider.WithoutVLANTags().VLANTags,   Is.Empty);
            Assert.That(provider.WithoutVLANTags().GetBytes(), Is.EqualTo(untagged.GetBytes()));

            // Popping an untagged frame is a no-op.
            Assert.That(untagged.PopVLANTag(),        Is.SameAs(untagged));
            Assert.That(untagged.WithoutVLANTags(),   Is.SameAs(untagged));

            // The payload survives all of this untouched.
            Assert.That(provider.Payload.ToArray(),   Is.EqualTo(untagged.Payload.ToArray()));

        }

        #endregion

        #region TryParseFrame_RejectsMalformedInput()

        /// <summary>
        /// Truncated frames, undefined EtherTypes and excessive VLAN tag
        /// stacks have to be rejected.
        /// </summary>
        [Test]
        public void TryParseFrame_RejectsMalformedInput()
        {

            Assert.That(EthernetFrame.TryParse((Byte[]?) null,      out _), Is.False);
            Assert.That(EthernetFrame.TryParse(Array.Empty<Byte>(), out _), Is.False);

            // Too short for a header.
            Assert.That(EthernetFrame.TryParse(new Byte[13], out _), Is.False);

            // A header, but no EtherType behind the VLAN tag.
            Assert.That(EthernetFrame.TryParse(Hex("AABBCCDDEEFF001A2B3C4D5E81002064"), out _), Is.False);

            // The undefined range between 1501 (0x05DD) and 1535 (0x05FF).
            Assert.That(EthernetFrame.TryParse(Hex("AABBCCDDEEFF001A2B3C4D5E05DD0102030405060708"), out _), Is.False);
            Assert.That(EthernetFrame.TryParse(Hex("AABBCCDDEEFF001A2B3C4D5E05FF0102030405060708"), out _), Is.False);

            // An IEEE 802.3 length field larger than the remaining bytes.
            Assert.That(EthernetFrame.TryParse(Hex("AABBCCDDEEFF001A2B3C4D5E00FF0102030405"), out _), Is.False);

            // Four stacked VLAN tags exceed the accepted stack depth of three.
            Assert.That(EthernetFrame.TryParse(Hex("AABBCCDDEEFF001A2B3C4D5E" +
                                                   "88A800C8" + "91000064" + "81000065" + "81000066" +
                                                   "0800" + "0102030405060708"), out _), Is.False);

            // ... while three are still accepted.
            Assert.That(EthernetFrame.TryParse(Hex("AABBCCDDEEFF001A2B3C4D5E" +
                                                   "88A800C8" + "91000064" + "81000065" +
                                                   "0800" + "0102030405060708"), out var threeTags), Is.True);
            Assert.That(threeTags!.VLANTags.Count,      Is.EqualTo(3));
            Assert.That(threeTags. HeaderLength,        Is.EqualTo(26));
            Assert.That(threeTags. Payload.Length,      Is.EqualTo(8));
            Assert.That(EthernetFrame.MaxVLANTagStackDepth, Is.EqualTo(3));

        }

        #endregion

        #region ConstructorValidatesItsArguments()

        /// <summary>
        /// The invariants of a frame are checked when it is created.
        /// </summary>
        [Test]
        public void ConstructorValidatesItsArguments()
        {

            // The undefined EtherType/Length range.
            Assert.Throws<ArgumentException>(() => new EthernetFrame(
                                                       destination,
                                                       source,
                                                       EtherType.From(1501)
                                                   ));

            // An IEEE 802.3 length field must match the payload.
            Assert.Throws<ArgumentException>(() => new EthernetFrame(
                                                       destination,
                                                       source,
                                                       EtherType.FromLength(10),
                                                       new Byte[5]
                                                   ));

            // Too many stacked VLAN tags.
            Assert.Throws<ArgumentException>(() => new EthernetFrame(
                                                       destination,
                                                       source,
                                                       EtherType.IPv4,
                                                       default(ReadOnlyMemory<Byte>),
                                                       new[] {
                                                           VLANTag.ServiceTag (100),
                                                           VLANTag.CustomerTag(101),
                                                           VLANTag.CustomerTag(102),
                                                           VLANTag.CustomerTag(103)
                                                       }
                                                   ));

            // A matching IEEE 802.3 length field is fine.
            var ieee8023 = new EthernetFrame(
                               destination,
                               source,
                               EtherType.FromLength(5),
                               new Byte[5]
                           );

            Assert.That(ieee8023.IsIEEE8023,              Is.True);
            Assert.That(ieee8023.EtherTypeOrLength.Value, Is.EqualTo(5));

            // An empty frame is legal, it will just be padded.
            var empty = new EthernetFrame(destination, source, EtherType.IPv4);

            Assert.That(empty.Payload.ToArray(),   Is.Empty);
            Assert.That(empty.GetBytes().Length,   Is.EqualTo(60));

        }

        #endregion

        #region FramesStoreCopiesOfTheirArguments()

        /// <summary>
        /// A frame stores copies of its payload and VLAN tags, so that later
        /// changes to the given collections cannot alter it.
        /// </summary>
        [Test]
        public void FramesStoreCopiesOfTheirArguments()
        {

            var payload  = new Byte[] { 0x01, 0x02, 0x03, 0x04 };
            var vlanTags = new List<VLANTag> { VLANTag.CustomerTag(100) };

            var frame    = new EthernetFrame(
                               destination,
                               source,
                               EtherType.IPv4,
                               payload,
                               vlanTags
                           );

            payload[0] = 0xFF;
            vlanTags.Add(VLANTag.CustomerTag(200));

            Assert.That(frame.Payload.FirstSpan[0],     Is.EqualTo(0x01));
            Assert.That(frame.VLANTags.Count, Is.EqualTo(1));

        }

        #endregion

        #region JumboFrames()

        /// <summary>
        /// Payloads beyond the standard MTU of 1500 bytes are jumbo frames.
        /// </summary>
        [Test]
        public void JumboFrames()
        {

            var standard = new EthernetFrame(destination, source, EtherType.IPv4, new Byte[1500]);
            var jumbo    = new EthernetFrame(destination, source, EtherType.IPv4, new Byte[9000]);

            Assert.That(standard.IsJumboFrame, Is.False);
            Assert.That(jumbo.   IsJumboFrame, Is.True);
            Assert.That(jumbo.   FrameLength,  Is.EqualTo(14 + 9000 + 4));

            Assert.That(EthernetFrame.TryParse(jumbo.GetBytes(), out var parsed), Is.True);
            Assert.That(parsed!.Payload.Length, Is.EqualTo(9000));

        }

        #endregion

        #region WriteFrameIntoACallerOwnedBuffer()

        /// <summary>
        /// TryWrite is the primitive of the serialization path, so that callers owning
        /// a buffer never have to allocate. GetBytes is only its allocating convenience.
        /// </summary>
        [Test]
        public void WriteFrameIntoACallerOwnedBuffer()
        {

            var frame = new EthernetFrame(
                            destination,
                            source,
                            EtherType.IPv4,
                            VLANTag.CustomerTag(100),
                            new Byte[] { 0x01, 0x02, 0x03, 0x04 }
                        );

            Assert.That(frame.GetLength(),                                  Is.EqualTo(64));
            Assert.That(frame.GetLength(AddPadding: false),                 Is.EqualTo(22));
            Assert.That(frame.GetLength(IncludeFCS: true),                  Is.EqualTo(68));
            Assert.That(frame.GetLength(IncludeFCS: true, AddPadding: false), Is.EqualTo(26));

            // A buffer that is too small is rejected without writing anything.
            Span<Byte> tooSmall = stackalloc Byte[63];
            Assert.That(frame.TryWrite(tooSmall, out var notWritten), Is.False);
            Assert.That(notWritten,      Is.EqualTo(0));

            // An exactly fitting buffer.
            Span<Byte> exact = stackalloc Byte[64];
            Assert.That(frame.TryWrite(exact, out var exactlyWritten), Is.True);
            Assert.That(exactlyWritten,  Is.EqualTo(64));
            Assert.That(exact.ToArray(), Is.EqualTo(frame.GetBytes()));

            // An oversized - and deliberately dirty - buffer: the padding has to be
            // zeroed by TryWrite, and nothing beyond BytesWritten may be touched.
            Span<Byte> oversized = stackalloc Byte[128];
            oversized.Fill(0xCC);

            Assert.That(frame.TryWrite(oversized, out var bytesWritten), Is.True);
            Assert.That(bytesWritten,                     Is.EqualTo(64));
            Assert.That(oversized[..64].ToArray(),        Is.EqualTo(frame.GetBytes()));
            Assert.That(oversized[22..64].ToArray(),      Is.All.EqualTo((Byte) 0x00));
            Assert.That(oversized[64..].ToArray(),        Is.All.EqualTo((Byte) 0xCC));

            // The very same buffer, now with a Frame Check Sequence.
            Assert.That(frame.TryWrite(oversized, out bytesWritten, IncludeFCS: true), Is.True);
            Assert.That(bytesWritten,                     Is.EqualTo(68));
            Assert.That(EthernetFrame.VerifyFCS(oversized[..68]), Is.True);

            // The payload is reachable without copying it.
            Assert.That(frame.Payload.FirstSpan.Length,         Is.EqualTo(4));
            Assert.That(frame.Payload.FirstSpan.ToArray(),  Is.EqualTo(frame.Payload.ToArray()));

        }

        #endregion

        #region ParseFrameWithAndWithoutCopyingThePayload()

        /// <summary>
        /// Parsing from a ReadOnlyMemory can slice the payload instead of copying it.
        /// That is the whole point of the payload being a ReadOnlyMemory - but it ties
        /// the frame to the lifetime of the given buffer.
        /// </summary>
        [Test]
        public void ParseFrameWithAndWithoutCopyingThePayload()
        {

            var buffer = Hex("AABBCCDDEEFF"  +
                             "001A2B3C4D5E"  +
                             "88B8"          +
                             Pad("DEADBEEF", 46));

            Assert.That(EthernetFrame.TryParse(buffer.AsMemory(), out var copied,  CopyPayload: true),  Is.True);
            Assert.That(EthernetFrame.TryParse(buffer.AsMemory(), out var adopted, CopyPayload: false), Is.True);

            Assert.That(copied !.Payload.ToArray(), Is.EqualTo(adopted!.Payload.ToArray()));
            Assert.That(copied.  Payload.Length,    Is.EqualTo(46));

            // The adopted payload is a window into the original buffer ...
            buffer[14] = 0x11;

            Assert.That(adopted.Payload.FirstSpan[0], Is.EqualTo(0x11));

            // ... while the copied one is independent of it.
            Assert.That(copied. Payload.FirstSpan[0], Is.EqualTo(0xDE));

            // The array overload always copies, since an array may be mutated as well.
            Assert.That(EthernetFrame.TryParse(buffer, out var fromArray), Is.True);
            buffer[14] = 0x22;
            Assert.That(fromArray!.Payload.FirstSpan[0], Is.EqualTo(0x11));

        }

        #endregion

        #region SegmentedPayloadIsNeverCopied()

        /// <summary>
        /// Encapsulating a payload into LLC/SNAP only prepends two header segments,
        /// the payload itself is never copied. That is what the payload being a
        /// ReadOnlySequence buys us.
        /// </summary>
        [Test]
        public void SegmentedPayloadIsNeverCopied()
        {

            var payload = Encoding.ASCII.GetBytes("This payload must never be copied into a new buffer!");

            var frame   = EthernetFrame.CreateIEEE8023SNAP(
                              destination,
                              source,
                              new SNAPHeader(EtherType.IPv4),
                              new ReadOnlySequence<Byte>(payload)
                          );

            // LLC header, SNAP header and payload are three separate segments.
            Assert.That(frame.Payload.IsSingleSegment, Is.False);
            Assert.That(frame.Payload.Length,          Is.EqualTo(3 + 5 + payload.Length));

            var segments = new List<Int32>();
            foreach (var segment in frame.Payload)
                segments.Add(segment.Length);

            Assert.That(segments, Is.EqualTo(new [] { 3, 5, payload.Length }));

            // The last segment still IS the original array, not a copy of it.
            payload[0] = (Byte) '!';
            Assert.That(frame.Payload.Slice(8).ToArray()[0], Is.EqualTo((Byte) '!'));

            // Serialization gathers the segments in order.
            var bytes = frame.GetBytes();
            Assert.That(bytes[14..22],  Is.EqualTo(Hex("AAAA03" + "000000" + "0800")));
            Assert.That(bytes[22..(22 + payload.Length)], Is.EqualTo(payload));

            // ... and the accessors see through the segment boundaries.
            Assert.That(frame.TryGetLLCHeader (out var llcHeader),  Is.True);
            Assert.That(llcHeader.IsSNAP,                           Is.True);
            Assert.That(frame.TryGetSNAPHeader(out var snapHeader), Is.True);
            Assert.That(snapHeader.ProtocolId,                      Is.EqualTo(EtherType.IPv4));
            Assert.That(frame.PayloadProtocol,                      Is.EqualTo(EtherType.IPv4));
            Assert.That(frame.GetLLCPayload().ToArray(),            Is.EqualTo(payload));

        }

        #endregion

        #region BuildAFrameOverSeveralOwnSegments()

        /// <summary>
        /// A caller can assemble a payload from several buffers of their own -
        /// e.g. a protocol header and a body - without concatenating them.
        /// </summary>
        [Test]
        public void BuildAFrameOverSeveralOwnSegments()
        {

            var header  = Hex("DEADBEEF");
            var body    = Encoding.ASCII.GetBytes("... and a body that lives in a buffer of its own.");

            var frame   = new EthernetFrame(
                              destination,
                              source,
                              EtherType.From(0x88B8),
                              BufferSegment.Sequence(header, body)
                          );

            Assert.That(frame.Payload.IsSingleSegment, Is.False);
            Assert.That(frame.Payload.Length,          Is.EqualTo(header.Length + body.Length));

            var bytes = frame.GetBytes();

            Assert.That(bytes[14..18],                          Is.EqualTo(header));
            Assert.That(bytes[18..(18 + body.Length)],          Is.EqualTo(body));

            // Round-trips through the parser, which yields a single contiguous segment again.
            Assert.That(EthernetFrame.TryParse(bytes, out var parsed), Is.True);
            Assert.That(parsed!.Payload.IsSingleSegment, Is.True);
            Assert.That(parsed. Payload.ToArray(),       Is.EqualTo(frame.Payload.ToArray()));

            // Empty segments are skipped, a single one needs no chain at all.
            Assert.That(BufferSegment.Sequence().IsEmpty,                            Is.True);
            Assert.That(BufferSegment.Sequence(header).IsSingleSegment,              Is.True);
            Assert.That(BufferSegment.Sequence(header, default).IsSingleSegment,     Is.True);
            Assert.That(BufferSegment.Sequence(default, body).ToArray(),             Is.EqualTo(body));

        }

        #endregion

        #region FrameToString()

        /// <summary>
        /// Checks the text representation of Ethernet frames.
        /// </summary>
        [Test]
        public void FrameToString()
        {

            var untagged = new EthernetFrame(destination, source, EtherType.IPv4, new Byte[20]);

            Assert.That(untagged.ToString(),
                        Is.EqualTo("00:1A:2B:3C:4D:5E -> AA:BB:CC:DD:EE:FF, IPv4 (0x0800), 20 byte(s) payload"));

            var qinq = untagged.PushVLANTag(VLANTag.CustomerTag(100)).
                                PushVLANTag(VLANTag.ServiceTag (200));

            Assert.That(qinq.ToString(),
                        Is.EqualTo("00:1A:2B:3C:4D:5E -> AA:BB:CC:DD:EE:FF, VLAN 200.100, IPv4 (0x0800), 20 byte(s) payload"));

            var ieee8023 = EthernetFrame.CreateIEEE8023(
                               stpBridges,
                               source,
                               new LLCHeader(LLCHeader.STPServiceAccessPoint,
                                             LLCHeader.STPServiceAccessPoint,
                                             LLCHeader.UnnumberedInformation),
                               new Byte[35]
                           );

            Assert.That(ieee8023.ToString(),
                        Is.EqualTo("00:1A:2B:3C:4D:5E -> 01:80:C2:00:00:00, IEEE 802.3 length 38, 38 byte(s) payload"));

        }

        #endregion

    }

}
