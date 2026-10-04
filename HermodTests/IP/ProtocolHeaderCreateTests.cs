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
    /// The static Create(…, ref bytesCopied) factories of the protocol headers return
    /// null for a buffer that is too short and leave bytesCopied alone.
    /// </summary>
    [TestFixture]
    public class ProtocolHeaderCreateTests
    {

        private const Int32 Untouched = -1;


        #region Icmpv6EchoRequest

        [TestCase(0)]
        [TestCase(3)]
        public void Icmpv6EchoRequest_Create_returns_null_for_a_short_buffer(Int32 Length)
        {

            var bytesCopied = Untouched;

            Assert.That(Icmpv6EchoRequest.Create(new Byte[Length], ref bytesCopied), Is.Null);
            Assert.That(bytesCopied,                                                 Is.EqualTo(Untouched));

        }

        [Test]
        public void Icmpv6EchoRequest_Create_reads_id_and_sequence()
        {

            var bytesCopied  = Untouched;
            var echoRequest  = Icmpv6EchoRequest.Create([ 0x12, 0x34, 0x56, 0x78 ], ref bytesCopied);

            Assert.That(echoRequest,                             Is.Not.Null);
            Assert.That(echoRequest.Id,                         Is.EqualTo(0x1234));
            Assert.That(echoRequest.Sequence,                    Is.EqualTo(0x5678));
            Assert.That(bytesCopied,                             Is.EqualTo(Icmpv6EchoRequest.Icmpv6EchoRequestLength));
            Assert.That(echoRequest.GetProtocolPacketBytes([]),  Is.EqualTo(new Byte[] { 0x12, 0x34, 0x56, 0x78 }));

        }

        #endregion

        #region Icmpv6Header

        [TestCase(0)]
        [TestCase(3)]
        public void Icmpv6Header_Create_returns_null_for_a_short_buffer(Int32 Length)
        {

            var bytesCopied = Untouched;

            Assert.That(Icmpv6Header.Create(new Byte[Length], ref bytesCopied), Is.Null);
            Assert.That(bytesCopied,                                            Is.EqualTo(Untouched));

        }

        [Test]
        public void Icmpv6Header_Create_reads_type_code_and_checksum()
        {

            var bytesCopied   = Untouched;
            var icmpv6Header  = Icmpv6Header.Create([ 128, 0, 0xAB, 0xCD ], ref bytesCopied);

            Assert.That(icmpv6Header,             Is.Not.Null);
            Assert.That(icmpv6Header.Type,       Is.EqualTo(Icmpv6Header.Icmpv6EchoRequestType));
            Assert.That(icmpv6Header.Code,        Is.EqualTo(Icmpv6Header.Icmpv6EchoRequestCode));
            Assert.That(icmpv6Header.Checksum,    Is.EqualTo(0xABCD));
            Assert.That(icmpv6Header.ipv6Header,  Is.Null);
            Assert.That(bytesCopied,              Is.EqualTo(Icmpv6Header.Icmpv6HeaderLength));

        }

        [Test]
        public void Icmpv6Header_without_IPv6_header_cannot_build_its_checksum()
        {

            var icmpv6Header = new Icmpv6Header() {
                                   Type = Icmpv6Header.Icmpv6EchoRequestType
                               };

            Assert.That(() => icmpv6Header.GetProtocolPacketBytes([ 0x01, 0x02 ]),
                        Throws.InvalidOperationException);

        }

        /// <summary>
        /// The ICMPv6 checksum covers the IPv6 pseudo header (RFC 8200 §8.1, RFC 4443 §2.3).
        /// </summary>
        [Test]
        public void Icmpv6Header_with_IPv6_header_has_a_valid_checksum()
        {

            var source        = IPv6Address.Parse("2001:db8::1");
            var target        = IPv6Address.Parse("2001:db8::2");

            var icmpv6Header  = new Icmpv6Header(new Ipv6Header() {
                                                     SourceAddress       = source,
                                                     DestinationAddress  = target,
                                                     NextHeader          = 58
                                                 }) {
                                    Type = Icmpv6Header.Icmpv6EchoRequestType
                                };

            var message       = icmpv6Header.GetProtocolPacketBytes([ 0x12, 0x34, 0x56, 0x78 ]);

            Assert.That(message[0..2],  Is.EqualTo(new Byte[] { 128, 0 }));
            Assert.That(message[2..4],  Is.Not.EqualTo(new Byte[] { 0x00, 0x00 }));
            Assert.That(message[4..],   Is.EqualTo(new Byte[] { 0x12, 0x34, 0x56, 0x78 }));

            // Pseudo header plus the ICMPv6 message including its checksum sums up to 0xFFFF.
            Byte[] pseudoHeader = [
                .. source.GetBytes(),
                .. target.GetBytes(),
                0x00, 0x00, 0x00, (Byte) message.Length,
                0x00, 0x00, 0x00, 58,
                .. message
            ];

            Assert.That(OnesComplementSum(pseudoHeader), Is.EqualTo(0xFFFF));

        }

        #endregion

        #region IgmpHeader

        [TestCase(0)]
        [TestCase(7)]
        public void IgmpHeader_Create_returns_null_for_a_short_buffer(Int32 Length)
        {

            var bytesCopied = Untouched;

            Assert.That(IgmpHeader.Create(new Byte[Length], ref bytesCopied), Is.Null);
            Assert.That(bytesCopied,                                          Is.EqualTo(Untouched));

        }

        [Test]
        public void IgmpHeader_Create_reads_type_response_time_and_checksum()
        {

            var bytesCopied  = Untouched;
            var igmpHeader   = IgmpHeader.Create([ 0x16, 100, 0xAB, 0xCD, 239, 1, 2, 3 ], ref bytesCopied);

            Assert.That(igmpHeader,                       Is.Not.Null);
            Assert.That(igmpHeader.VersionType,          Is.EqualTo(IgmpHeader.IgmpMembershipReportV2));
            Assert.That(igmpHeader.MaximumResponseTime,   Is.EqualTo(100));
            Assert.That(igmpHeader.Checksum,              Is.EqualTo(0xABCD));
            Assert.That(bytesCopied,                      Is.EqualTo(IgmpHeader.IgmpHeaderLength));

        }

        #endregion

        #region Ipv6Header

        [TestCase(0)]
        [TestCase(39)]
        public void Ipv6Header_Create_returns_null_for_a_short_buffer(Int32 Length)
        {

            var bytesCopied = Untouched;

            Assert.That(Ipv6Header.Create(new Byte[Length], ref bytesCopied), Is.Null);
            Assert.That(bytesCopied,                                          Is.EqualTo(Untouched));

        }

        [Test]
        public void Ipv6Header_Create_reads_version_and_destination_address()
        {

            var target       = IPv6Address.Parse("2001:db8::2");
            var packet       = new Byte[Ipv6Header.Ipv6HeaderLength];
            packet[0]        = 0x60;
            target.GetBytes().CopyTo(packet, 24);

            var bytesCopied  = Untouched;
            var ipv6Header   = Ipv6Header.Create(packet, ref bytesCopied);

            Assert.That(ipv6Header,                      Is.Not.Null);
            Assert.That(ipv6Header.Version,             Is.EqualTo(6));
            Assert.That(ipv6Header.DestinationAddress,   Is.EqualTo(target));
            Assert.That(bytesCopied,                     Is.EqualTo(Ipv6Header.Ipv6HeaderLength));

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
