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

using System.Collections;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.IP
{

    /// <summary>
    /// AProtocolHeader.BuildPacket(…) builds a packet from the innermost header outwards.
    /// </summary>
    [TestFixture]
    public class AProtocolHeaderTests
    {

        #region (class) MarkerHeader

        /// <summary>
        /// A header that is just one marker byte and logs which payload it was given.
        /// </summary>
        private sealed class MarkerHeader(Byte Marker, List<(Byte Marker, Byte[] PayLoad)> Calls) : AProtocolHeader
        {
            public override Byte[] GetProtocolPacketBytes(Byte[] payLoad)
            {
                Calls.Add((Marker, payLoad));
                return [Marker, .. payLoad];
            }
        }

        #endregion


        #region BuildPacket_wraps_the_payload_from_the_innermost_header_outwards()

        [Test]
        public void BuildPacket_wraps_the_payload_from_the_innermost_header_outwards()
        {

            var calls   = new List<(Byte Marker, Byte[] PayLoad)>();
            var outer   = new MarkerHeader(0xA1, calls);
            var inner   = new MarkerHeader(0xB2, calls);

            var packet  = outer.BuildPacket(new ArrayList { outer, inner }, [0x01, 0x02]);

            Assert.That(packet,                       Is.EqualTo(new Byte[] { 0xA1, 0xB2, 0x01, 0x02 }));
            Assert.That(calls.Select(c => c.Marker),  Is.EqualTo(new Byte[] { 0xB2, 0xA1 }),        "the inner header is built first");
            Assert.That(calls[0].PayLoad,             Is.EqualTo(new Byte[] { 0x01, 0x02 }),        "the inner header gets the plain payload");
            Assert.That(calls[1].PayLoad,             Is.EqualTo(new Byte[] { 0xB2, 0x01, 0x02 }),  "the outer header gets the inner header plus payload");

        }

        #endregion

        #region BuildPacket_without_headers_returns_the_payload()

        [Test]
        public void BuildPacket_without_headers_returns_the_payload()
        {

            var payLoad = new Byte[] { 0x01, 0x02 };

            Assert.That(new MarkerHeader(0xA1, []).BuildPacket([], payLoad), Is.SameAs(payLoad));

        }

        #endregion

        #region BuildPacket_IPv6_UDP_has_a_valid_UDP_checksum()

        /// <summary>
        /// IPv6 + UDP: the UDP header computes its checksum over the IPv6 pseudo header
        /// (RFC 8200 §8.1), and the IPv6 header is put in front of it.
        /// </summary>
        [Test]
        public void BuildPacket_IPv6_UDP_has_a_valid_UDP_checksum()
        {

            var payLoad  = Encoding.ASCII.GetBytes("Hermod");
            var source   = IPv6Address.Parse("2001:db8::1");
            var target   = IPv6Address.Parse("2001:db8::2");

            var ipv6     = new Ipv6Header() {
                               SourceAddress       = source,
                               DestinationAddress  = target,
                               NextHeader          = 17,
                               PayloadLength       = (UInt16) (UdpHeader.UDPHeaderLength + payLoad.Length)
                           };

            var udp      = new UdpHeader() {
                               SourcePort          = 5683,
                               DestinationPort     = 5684,
                               Length              = (UInt16) (UdpHeader.UDPHeaderLength + payLoad.Length),
                               ipv6PacketHeader    = ipv6
                           };

            var packet   = ipv6.BuildPacket(new ArrayList { ipv6, udp }, payLoad);
            var segment  = packet[Ipv6Header.Ipv6HeaderLength..];

            Assert.That(packet.Length,                Is.EqualTo(Ipv6Header.Ipv6HeaderLength + UdpHeader.UDPHeaderLength + payLoad.Length));
            Assert.That(packet[0] >> 4,               Is.EqualTo(6),                       "IP version");
            Assert.That(packet[4..6],                 Is.EqualTo(new Byte[] { 0x00, 0x0E }), "IPv6 payload length");
            Assert.That(packet[6],                    Is.EqualTo(17),                      "next header: UDP");
            Assert.That(packet[8..24],                Is.EqualTo(source.GetBytes()));
            Assert.That(packet[24..40],               Is.EqualTo(target.GetBytes()));

            Assert.That(segment[0..2],                Is.EqualTo(new Byte[] { 0x16, 0x33 }), "source port 5683");
            Assert.That(segment[2..4],                Is.EqualTo(new Byte[] { 0x16, 0x34 }), "destination port 5684");
            Assert.That(segment[4..6],                Is.EqualTo(new Byte[] { 0x00, 0x0E }), "UDP length");
            Assert.That(segment[6..8],                Is.Not.EqualTo(new Byte[] { 0x00, 0x00 }));
            Assert.That(segment[8..],                 Is.EqualTo(payLoad));

            // Pseudo header plus the UDP segment including its checksum sums up to 0xFFFF.
            Byte[] pseudoHeader = [
                .. source.GetBytes(),
                .. target.GetBytes(),
                0x00, 0x00, 0x00, (Byte) segment.Length,
                0x00, 0x00, 0x00, 17,
                .. segment
            ];

            Assert.That(OnesComplementSum(pseudoHeader), Is.EqualTo(0xFFFF));

        }

        #endregion

        #region BuildPacket_rejects_a_null_entry()

        [Test]
        public void BuildPacket_rejects_a_null_entry()
        {

            var header = new MarkerHeader(0xA1, []);

            Assert.That(() => header.BuildPacket(new ArrayList { header, null }, [0x01]),
                        Throws.ArgumentException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("headerList"));

        }

        #endregion

        #region BuildPacket_rejects_an_entry_that_is_no_protocol_header()

        [Test]
        public void BuildPacket_rejects_an_entry_that_is_no_protocol_header()
        {

            var header = new MarkerHeader(0xA1, []);

            Assert.That(() => header.BuildPacket(new ArrayList { header, "UDP" }, [0x01]),
                        Throws.InstanceOf<InvalidCastException>());

        }

        #endregion


        #region (private, static) OnesComplementSum(Data)

        /// <summary>
        /// The 16-bit one's complement sum of RFC 1071, for an even number of bytes.
        /// </summary>
        private static UInt32 OnesComplementSum(Byte[] Data)
        {

            var sum = 0U;

            for (var i = 0; i < Data.Length; i += 2)
                sum += (UInt32) ((Data[i] << 8) | Data[i + 1]);

            while (sum >> 16 != 0)
                sum = (sum & 0xFFFF) + (sum >> 16);

            return sum;

        }

        #endregion

    }

}
