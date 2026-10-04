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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.IP
{

    /// <summary>
    /// The static Create(…, ref bytesCopied) factories read every field from the place
    /// the RFCs put it, so they parse what GetProtocolPacketBytes(…) writes.
    /// </summary>
    [TestFixture]
    public class ProtocolHeaderRoundTripTests
    {

        #region Ipv6Header

        /// <summary>
        /// RFC 8200 §3: version, traffic class and flow label share the first four bytes,
        /// followed by payload length (2), next header (1), hop limit (1) and both addresses.
        /// </summary>
        [Test]
        public void Ipv6Header_Create_reads_every_field_at_its_RFC_8200_offset()
        {

            var source      = IPv6Address.Parse("2001:db8::1");
            var target      = IPv6Address.Parse("2001:db8::2");

            Byte[] packet   = [
                0x6A, 0xBC, 0xDE, 0xF1,
                0x12, 0x34,
                58,
                0x40,
                .. source.GetBytes(),
                .. target.GetBytes()
            ];

            var ipv6Header  = CreateIpv6Header(packet);

            Assert.That(ipv6Header.Version,             Is.EqualTo(6));
            Assert.That(ipv6Header.TrafficClass,        Is.EqualTo(0xAB));
            Assert.That(ipv6Header.Flow,                Is.EqualTo(0xCDEF1));
            Assert.That(ipv6Header.PayloadLength,       Is.EqualTo(0x1234));
            Assert.That(ipv6Header.NextHeader,          Is.EqualTo(58));
            Assert.That(ipv6Header.HopLimit,            Is.EqualTo(0x40));
            Assert.That(ipv6Header.SourceAddress,       Is.EqualTo(source));
            Assert.That(ipv6Header.DestinationAddress,  Is.EqualTo(target));

        }

        [TestCase((Byte) 0x00, 0x00000U)]
        [TestCase((Byte) 0xAB, 0xCDEF1U)]
        [TestCase((Byte) 0x0F, 0x10000U)]
        [TestCase((Byte) 0xF0, 0x0FFFFU)]
        [TestCase((Byte) 0xFF, 0xFFFFFU)]
        public void Ipv6Header_round_trips_through_GetProtocolPacketBytes(Byte TrafficClass, UInt32 Flow)
        {

            var original    = new Ipv6Header() {
                                  TrafficClass        = TrafficClass,
                                  Flow                = Flow,
                                  PayloadLength       = 0x0102,
                                  NextHeader          = 17,
                                  HopLimit            = 255,
                                  SourceAddress       = IPv6Address.Parse("fe80::1:2:3:4"),
                                  DestinationAddress  = IPv6Address.Parse("ff02::fb")
                              };

            var packet      = original.GetProtocolPacketBytes([ 0xAA, 0xBB ]);
            var ipv6Header  = CreateIpv6Header(packet);

            Assert.That(ipv6Header.Version,             Is.EqualTo(6));
            Assert.That(ipv6Header.TrafficClass,        Is.EqualTo(TrafficClass));
            Assert.That(ipv6Header.Flow,                Is.EqualTo(Flow));
            Assert.That(ipv6Header.PayloadLength,       Is.EqualTo(0x0102));
            Assert.That(ipv6Header.NextHeader,          Is.EqualTo(17));
            Assert.That(ipv6Header.HopLimit,            Is.EqualTo(255));
            Assert.That(ipv6Header.SourceAddress,       Is.EqualTo(original.SourceAddress));
            Assert.That(ipv6Header.DestinationAddress,  Is.EqualTo(original.DestinationAddress));

            Assert.That(ipv6Header.GetProtocolPacketBytes([]),
                        Is.EqualTo(packet[..Ipv6Header.Ipv6HeaderLength]),
                        "the parsed header writes the same bytes again");

        }

        #endregion

        #region IgmpHeader

        /// <summary>
        /// RFC 2236 §2: type, max response time, checksum and the group address.
        /// </summary>
        [Test]
        public void IgmpHeader_Create_reads_the_group_address()
        {

            var igmpHeader = CreateIgmpHeader([ 0x16, 100, 0xAB, 0xCD, 239, 1, 2, 3 ]);

            Assert.That(igmpHeader.GroupAddress, Is.EqualTo(IPv4Address.Parse("239.1.2.3")));

        }

        [Test]
        public void IgmpHeader_round_trips_through_GetProtocolPacketBytes()
        {

            var original    = new IgmpHeader() {
                                  VersionType          = IgmpHeader.IgmpLeaveGroup,
                                  MaximumResponseTime  = 0,
                                  GroupAddress         = IPv4Address.Parse("239.255.255.250")
                              };

            var packet      = original.GetProtocolPacketBytes([]);
            var igmpHeader  = CreateIgmpHeader(packet);

            Assert.That(AProtocolHeader.ComputeChecksum(packet),  Is.EqualTo(0), "the written checksum is valid");

            Assert.That(igmpHeader.VersionType,                   Is.EqualTo(IgmpHeader.IgmpLeaveGroup));
            Assert.That(igmpHeader.MaximumResponseTime,           Is.EqualTo(0));
            Assert.That(igmpHeader.Checksum,                      Is.EqualTo(original.Checksum));
            Assert.That(igmpHeader.GroupAddress,                  Is.EqualTo(original.GroupAddress));

            Assert.That(igmpHeader.GetProtocolPacketBytes([]),    Is.EqualTo(packet), "the parsed header writes the same bytes again");

        }

        #endregion


        #region (private, static) CreateIpv6Header(Packet)

        private static Ipv6Header CreateIpv6Header(Byte[] Packet)
        {

            var bytesCopied  = -1;
            var ipv6Header   = Ipv6Header.Create(Packet, ref bytesCopied);

            Assert.That(ipv6Header,   Is.Not.Null);
            Assert.That(bytesCopied,  Is.EqualTo(Ipv6Header.Ipv6HeaderLength));

            return ipv6Header!;

        }

        #endregion

        #region (private, static) CreateIgmpHeader(Packet)

        private static IgmpHeader CreateIgmpHeader(Byte[] Packet)
        {

            var bytesCopied  = -1;
            var igmpHeader   = IgmpHeader.Create(Packet, ref bytesCopied);

            Assert.That(igmpHeader,   Is.Not.Null);
            Assert.That(bytesCopied,  Is.EqualTo(IgmpHeader.IgmpHeaderLength));

            return igmpHeader!;

        }

        #endregion

    }

}
