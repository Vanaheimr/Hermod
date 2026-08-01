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
using org.GraphDefined.Vanaheimr.Hermod.IEEE80211;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.IEEE80211
{

    /// <summary>
    /// IEEE 802.11 frame tests.
    /// </summary>
    [TestFixture]
    public class IEEE80211FrameTests
    {

        #region Data

        private static readonly MACAddress station      = MACAddress.Parse("00:1A:2B:3C:4D:5E");
        private static readonly MACAddress accessPoint  = MACAddress.Parse("AA:BB:CC:DD:EE:FF");
        private static readonly MACAddress wiredHost    = MACAddress.Parse("11:22:33:44:55:66");
        private static readonly MACAddress secondAP     = MACAddress.Parse("A0:B0:C0:D0:E0:F0");

        #endregion

        #region (private) Hex(Text)

        /// <summary>
        /// Convert the given hexadecimal text, which may contain whitespace, into bytes.
        /// </summary>
        private static Byte[] Hex(String Text)

            => Convert.FromHexString(
                   new String(
                       Text.Where(Char.IsAsciiHexDigit).ToArray()
                   )
               );

        #endregion


        #region FrameControlBitLayout()

        /// <summary>
        /// Octet 0 carries version, type and subtype, octet 1 the flags.
        /// </summary>
        [Test]
        public void FrameControlBitLayout()
        {

            // 0x08 = subtype 0 (data), type 2 (data), version 0
            // 0x41 = ToDS, protected
            Assert.That(FrameControl.TryParse(Hex("0841"), out var frameControl), Is.True);

            Assert.That(frameControl.ProtocolVersion,   Is.EqualTo(0));
            Assert.That(frameControl.Type,              Is.EqualTo(FrameTypes.Data));
            Assert.That(frameControl.Subtype,           Is.EqualTo(FrameSubtypes.Data));
            Assert.That(frameControl.ToDS,              Is.True);
            Assert.That(frameControl.FromDS,            Is.False);
            Assert.That(frameControl.Protected,         Is.True);
            Assert.That(frameControl.Retry,             Is.False);
            Assert.That(frameControl.AddressMode,       Is.EqualTo(AddressModes.ToDistributionSystem));
            Assert.That(frameControl.HasFourthAddress,  Is.False);
            Assert.That(frameControl.HasQoSControl,     Is.False);
            Assert.That(frameControl.HasHTControl,      Is.False);
            Assert.That(frameControl.GetBytes(),        Is.EqualTo(Hex("0841")));

            // 0x80 = subtype 8 (beacon), type 0 (management)
            Assert.That(FrameControl.TryParse(Hex("8000"), out var beacon), Is.True);
            Assert.That(beacon.Type,     Is.EqualTo(FrameTypes.Management));
            Assert.That(beacon.Subtype,  Is.EqualTo(FrameSubtypes.ManagementBeacon));

            // 0xD4 = subtype 13 (ACK), type 1 (control)
            Assert.That(FrameControl.TryParse(Hex("D400"), out var ack), Is.True);
            Assert.That(ack.Type,        Is.EqualTo(FrameTypes.Control));
            Assert.That(ack.Subtype,     Is.EqualTo(FrameSubtypes.ControlACK));

            // 0x88 = subtype 8 (QoS data), type 2 (data)
            Assert.That(FrameControl.TryParse(Hex("8800"), out var qos), Is.True);
            Assert.That(qos.Subtype,        Is.EqualTo(FrameSubtypes.DataQoS));
            Assert.That(qos.HasQoSControl,  Is.True);

            Assert.That(FrameControl.TryParse(Hex("08"), out _), Is.False);

        }

        #endregion

        #region FrameControlRoundtripsThroughItsFields()

        /// <summary>
        /// Create() and the accessors have to be exact inverses of each other.
        /// </summary>
        [Test]
        public void FrameControlRoundtripsThroughItsFields()
        {

            var frameControl = FrameControl.Create(
                                   FrameSubtypes.DataQoS,
                                   ToDS:             true,
                                   FromDS:           true,
                                   Retry:            true,
                                   PowerManagement:  true,
                                   Order:            true
                               );

            Assert.That(frameControl.Subtype,           Is.EqualTo(FrameSubtypes.DataQoS));
            Assert.That(frameControl.Type,              Is.EqualTo(FrameTypes.Data));
            Assert.That(frameControl.ToDS,              Is.True);
            Assert.That(frameControl.FromDS,            Is.True);
            Assert.That(frameControl.Retry,             Is.True);
            Assert.That(frameControl.PowerManagement,   Is.True);
            Assert.That(frameControl.MoreData,          Is.False);
            Assert.That(frameControl.Protected,         Is.False);
            Assert.That(frameControl.Order,             Is.True);

            Assert.That(frameControl.AddressMode,       Is.EqualTo(AddressModes.WirelessDistributionSystem));
            Assert.That(frameControl.HasFourthAddress,  Is.True);
            Assert.That(frameControl.HasQoSControl,     Is.True);
            Assert.That(frameControl.HasHTControl,      Is.True);

            Assert.That(FrameControl.From(frameControl.Value), Is.EqualTo(frameControl));

            // The order bit only means "+HTC" for QoS data and management frames.
            var orderedNonQoS = FrameControl.Create(FrameSubtypes.Data, Order: true);
            Assert.That(orderedNonQoS.Order,         Is.True);
            Assert.That(orderedNonQoS.HasHTControl,  Is.False);

            var orderedBeacon = FrameControl.Create(FrameSubtypes.ManagementBeacon, Order: true);
            Assert.That(orderedBeacon.HasHTControl,  Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => FrameControl.Create(FrameSubtypes.Data, ProtocolVersion: 4));

        }

        #endregion

        #region SequenceAndQoSControlAreLittleEndian()

        /// <summary>
        /// Unlike the frame control field, the sequence and QoS control fields
        /// are transmitted in little-endian byte order.
        /// </summary>
        [Test]
        public void SequenceAndQoSControlAreLittleEndian()
        {

            //  Sequence number 1234, fragment 5  =>  0x4D25  =>  on the wire: 25 4D
            var sequenceControl = new SequenceControl(1234, 5);

            Assert.That(sequenceControl.SequenceNumber,  Is.EqualTo(1234));
            Assert.That(sequenceControl.FragmentNumber,  Is.EqualTo(5));
            Assert.That(sequenceControl.IsFirstFragment, Is.False);
            Assert.That(sequenceControl.Value,           Is.EqualTo(0x4D25));
            Assert.That(sequenceControl.GetBytes(),      Is.EqualTo(Hex("254D")));

            Assert.That(SequenceControl.TryParse(Hex("254D"), out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(sequenceControl));

            Assert.Throws<ArgumentOutOfRangeException>(() => new SequenceControl(4096));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SequenceControl(0, 16));

            //  TID 6 | block ack (3 << 5 = 0x60) | A-MSDU (0x80)  =>  0x00E6  =>  on the wire: E6 00
            var qosControl = new QoSControl(6, AckPolicies.BlockAck, IsAMSDU: true);

            Assert.That(qosControl.TID,               Is.EqualTo(6));
            Assert.That(qosControl.AckPolicy,         Is.EqualTo(AckPolicies.BlockAck));
            Assert.That(qosControl.IsAMSDU,           Is.True);
            Assert.That(qosControl.EOSP,              Is.False);
            Assert.That(qosControl.UserPriority,      Is.EqualTo(PCPPriorities.InternetworkControl));
            Assert.That(qosControl.GetBytes(),        Is.EqualTo(Hex("E600")));

            Assert.That(QoSControl.From(qosControl.Value), Is.EqualTo(qosControl));

            // Traffic identifiers beyond 7 are not IEEE 802.1p priorities any more.
            Assert.That(new QoSControl(11).UserPriority, Is.Null);

            Assert.Throws<ArgumentOutOfRangeException>(() => new QoSControl(16));

        }

        #endregion

        #region ThreeAddressModesMapTheirAddresses()

        /// <summary>
        /// The very point of the addressing modes: the same three address slots
        /// mean different things depending on the ToDS and FromDS bits.
        /// </summary>
        [Test]
        public void ThreeAddressModesMapTheirAddresses()
        {

            var sequenceControl = new SequenceControl(1);

            // ToDS = 0, FromDS = 0: an ad-hoc network. Addr1 = DA, Addr2 = SA, Addr3 = BSSID.
            var adHoc = new IEEE80211DataFrame(
                            FrameControl.Create(FrameSubtypes.Data),
                            0, wiredHost, station, accessPoint, sequenceControl
                        );

            Assert.That(adHoc.AddressMode,         Is.EqualTo(AddressModes.IndependentBSS));
            Assert.That(adHoc.DestinationAddress,  Is.EqualTo(wiredHost));
            Assert.That(adHoc.SourceAddress,       Is.EqualTo(station));
            Assert.That(adHoc.BSSID,               Is.EqualTo(accessPoint));
            Assert.That(adHoc.ReceiverAddress,     Is.EqualTo(wiredHost));
            Assert.That(adHoc.TransmitterAddress,  Is.EqualTo(station));
            Assert.That(adHoc.HeaderLength,        Is.EqualTo(24));

            // ToDS = 0, FromDS = 1: the downlink. Addr1 = DA, Addr2 = BSSID, Addr3 = SA.
            var downlink = new IEEE80211DataFrame(
                               FrameControl.Create(FrameSubtypes.Data, FromDS: true),
                               0, station, accessPoint, wiredHost, sequenceControl
                           );

            Assert.That(downlink.AddressMode,         Is.EqualTo(AddressModes.FromDistributionSystem));
            Assert.That(downlink.DestinationAddress,  Is.EqualTo(station));
            Assert.That(downlink.SourceAddress,       Is.EqualTo(wiredHost));
            Assert.That(downlink.BSSID,               Is.EqualTo(accessPoint));
            Assert.That(downlink.TransmitterAddress,  Is.EqualTo(accessPoint));

            // ToDS = 1, FromDS = 0: the uplink. Addr1 = BSSID, Addr2 = SA, Addr3 = DA.
            var uplink = new IEEE80211DataFrame(
                             FrameControl.Create(FrameSubtypes.Data, ToDS: true),
                             0, accessPoint, station, wiredHost, sequenceControl
                         );

            Assert.That(uplink.AddressMode,         Is.EqualTo(AddressModes.ToDistributionSystem));
            Assert.That(uplink.DestinationAddress,  Is.EqualTo(wiredHost));
            Assert.That(uplink.SourceAddress,       Is.EqualTo(station));
            Assert.That(uplink.BSSID,               Is.EqualTo(accessPoint));
            Assert.That(uplink.ReceiverAddress,     Is.EqualTo(accessPoint));

            // None of them carries a fourth address.
            Assert.That(adHoc.   IsFourAddressFrame, Is.False);
            Assert.That(downlink.IsFourAddressFrame, Is.False);
            Assert.That(uplink.  IsFourAddressFrame, Is.False);

        }

        #endregion

        #region FourAddressModeCarriesBothHopAndEndpointAddresses()

        /// <summary>
        /// The four-address mode is the only one that carries the wireless hop
        /// AND the original endpoints, which is what lets it bridge a whole
        /// Ethernet segment across a wireless link.
        /// </summary>
        [Test]
        public void FourAddressModeCarriesBothHopAndEndpointAddresses()
        {

            var frame = new IEEE80211DataFrame(
                            FrameControl.Create(FrameSubtypes.Data, ToDS: true, FromDS: true),
                            0,
                            Address1:         secondAP,     // RA - the receiving access point
                            Address2:         accessPoint,  // TA - the transmitting access point
                            Address3:         wiredHost,    // DA - the final destination
                            SequenceControl:  new SequenceControl(7),
                            Address4:         station,      // SA - the original source
                            Body:             new ReadOnlySequence<Byte>(Encoding.ASCII.GetBytes("bridged"))
                        );

            Assert.That(frame.AddressMode,         Is.EqualTo(AddressModes.WirelessDistributionSystem));
            Assert.That(frame.IsFourAddressFrame,  Is.True);
            Assert.That(frame.HeaderLength,        Is.EqualTo(30));

            // The wireless hop ...
            Assert.That(frame.ReceiverAddress,     Is.EqualTo(secondAP));
            Assert.That(frame.TransmitterAddress,  Is.EqualTo(accessPoint));

            // ... and the endpoints it carries.
            Assert.That(frame.DestinationAddress,  Is.EqualTo(wiredHost));
            Assert.That(frame.SourceAddress,       Is.EqualTo(station));

            // A four-address frame spans two basic service sets, so it has no single BSSID.
            Assert.That(frame.BSSID,               Is.Null);

            // Round-trip.
            var bytes = frame.GetBytes();
            Assert.That(bytes.Length, Is.EqualTo(30 + 7));

            Assert.That(IEEE80211DataFrame.TryParse(bytes, out var parsed), Is.True);
            Assert.That(parsed!.Address1,           Is.EqualTo(secondAP));
            Assert.That(parsed. Address2,           Is.EqualTo(accessPoint));
            Assert.That(parsed. Address3,           Is.EqualTo(wiredHost));
            Assert.That(parsed. Address4,           Is.EqualTo(station));
            Assert.That(parsed. SourceAddress,      Is.EqualTo(station));
            Assert.That(parsed. DestinationAddress, Is.EqualTo(wiredHost));
            Assert.That(parsed. Body.ToArray(),     Is.EqualTo(Encoding.ASCII.GetBytes("bridged")));
            Assert.That(parsed. GetBytes(),         Is.EqualTo(bytes));

        }

        #endregion

        #region OptionalFieldsMustAgreeWithTheFrameControlField()

        /// <summary>
        /// The presence of every optional field is announced by a bit of the frame
        /// control field, so a frame whose fields contradict it could never be
        /// parsed back and is rejected right away.
        /// </summary>
        [Test]
        public void OptionalFieldsMustAgreeWithTheFrameControlField()
        {

            var sequenceControl = new SequenceControl(1);

            // A fourth address without ToDS = FromDS = 1.
            Assert.Throws<ArgumentException>(() => new IEEE80211DataFrame(
                FrameControl.Create(FrameSubtypes.Data, ToDS: true),
                0, station, accessPoint, wiredHost, sequenceControl, Address4: secondAP));

            // ToDS = FromDS = 1 without a fourth address.
            Assert.Throws<ArgumentException>(() => new IEEE80211DataFrame(
                FrameControl.Create(FrameSubtypes.Data, ToDS: true, FromDS: true),
                0, station, accessPoint, wiredHost, sequenceControl));

            // A QoS control field on a non-QoS subtype.
            Assert.Throws<ArgumentException>(() => new IEEE80211DataFrame(
                FrameControl.Create(FrameSubtypes.Data),
                0, station, accessPoint, wiredHost, sequenceControl, QoSControl: new QoSControl(0)));

            // A QoS subtype without a QoS control field.
            Assert.Throws<ArgumentException>(() => new IEEE80211DataFrame(
                FrameControl.Create(FrameSubtypes.DataQoS),
                0, station, accessPoint, wiredHost, sequenceControl));

            // An HT control field without the order / +HTC bit.
            Assert.Throws<ArgumentException>(() => new IEEE80211DataFrame(
                FrameControl.Create(FrameSubtypes.DataQoS),
                0, station, accessPoint, wiredHost, sequenceControl,
                QoSControl: new QoSControl(0), HTControl: HTControl.From(0)));

            // A management frame subtype in a data frame.
            Assert.Throws<ArgumentException>(() => new IEEE80211DataFrame(
                FrameControl.Create(FrameSubtypes.ManagementBeacon),
                0, station, accessPoint, wiredHost, sequenceControl));

            // A management frame may not set ToDS or FromDS.
            Assert.Throws<ArgumentException>(() => new IEEE80211ManagementFrame(
                FrameControl.Create(FrameSubtypes.ManagementBeacon, ToDS: true),
                0, station, accessPoint, accessPoint, sequenceControl));

        }

        #endregion

        #region HeaderLengthGrowsWithEveryOptionalField()

        /// <summary>
        /// 24, 26, 28, 30, 32 and 36 bytes are all valid header lengths, depending
        /// on which of the optional fields the frame control field announces.
        /// </summary>
        [Test]
        public void HeaderLengthGrowsWithEveryOptionalField()
        {

            var sequenceControl = new SequenceControl(1);

            IEEE80211DataFrame Frame(Boolean ToDS, Boolean FromDS, Boolean QoS, Boolean HTC)

                => new (FrameControl.Create(QoS ? FrameSubtypes.DataQoS : FrameSubtypes.Data,
                                            ToDS:   ToDS,
                                            FromDS: FromDS,
                                            Order:  HTC),
                        0, station, accessPoint, wiredHost, sequenceControl,
                        Address4:   ToDS && FromDS ? secondAP             : null,
                        QoSControl: QoS            ? new QoSControl(0)    : null,
                        HTControl:  HTC            ? HTControl.From(0)    : null);

            Assert.That(Frame(false, false, false, false).HeaderLength, Is.EqualTo(24));
            Assert.That(Frame(false, false, true,  false).HeaderLength, Is.EqualTo(26));
            Assert.That(Frame(true,  true,  false, false).HeaderLength, Is.EqualTo(30));
            Assert.That(Frame(true,  true,  true,  false).HeaderLength, Is.EqualTo(32));
            Assert.That(Frame(false, false, true,  true ).HeaderLength, Is.EqualTo(30));
            Assert.That(Frame(true,  true,  true,  true ).HeaderLength, Is.EqualTo(36));

            // Every one of them round-trips.
            foreach (var frame in new[] {
                         Frame(false, false, false, false),
                         Frame(false, false, true,  false),
                         Frame(true,  true,  false, false),
                         Frame(true,  true,  true,  false),
                         Frame(false, false, true,  true ),
                         Frame(true,  true,  true,  true )
                     })
            {

                var bytes = frame.GetBytes();

                Assert.That(bytes.Length, Is.EqualTo(frame.HeaderLength));
                Assert.That(IEEE80211DataFrame.TryParse(bytes, out var parsed), Is.True);
                Assert.That(parsed!.HeaderLength, Is.EqualTo(frame.HeaderLength));
                Assert.That(parsed. GetBytes(),   Is.EqualTo(bytes));

            }

        }

        #endregion

        #region ControlFramesAreAFormatOfTheirOwn()

        /// <summary>
        /// An acknowledgment is 14 bytes in total and carries a single address -
        /// this is where modelling everything as one variable-length header would
        /// break down.
        /// </summary>
        [Test]
        public void ControlFramesAreAFormatOfTheirOwn()
        {

            var ack = new IEEE80211ControlFrame(
                          FrameControl.Create(FrameSubtypes.ControlACK),
                          0,
                          station
                      );

            Assert.That(ack.HeaderLength,        Is.EqualTo(10));
            Assert.That(ack.GetLength(IncludeFCS: true), Is.EqualTo(14));
            Assert.That(ack.ReceiverAddress,     Is.EqualTo(station));
            Assert.That(ack.TransmitterAddress,  Is.Null);
            Assert.That(ack.BSSID,               Is.Null);

            var ackBytes = ack.GetBytes(IncludeFCS: true);
            Assert.That(ackBytes.Length, Is.EqualTo(14));
            Assert.That(AIEEE80211Frame.VerifyFCS(ackBytes), Is.True);

            // A request to send carries both addresses.
            var rts = new IEEE80211ControlFrame(
                          FrameControl.Create(FrameSubtypes.ControlRTS),
                          1234,
                          accessPoint,
                          station
                      );

            Assert.That(rts.HeaderLength,        Is.EqualTo(16));
            Assert.That(rts.Duration,            Is.EqualTo(1234));
            Assert.That(rts.AssociationId,       Is.Null);
            Assert.That(rts.TransmitterAddress,  Is.EqualTo(station));

            // A PS-Poll frame puts an association identifier into the duration field.
            var psPoll = new IEEE80211ControlFrame(
                             FrameControl.Create(FrameSubtypes.ControlPSPoll),
                             0xC000 | 42,
                             accessPoint,
                             station
                         );

            Assert.That(psPoll.IsDuration,     Is.False);
            Assert.That(psPoll.Duration,       Is.Null);
            Assert.That(psPoll.AssociationId,  Is.EqualTo(42));
            Assert.That(psPoll.BSSID,          Is.EqualTo(accessPoint));

            // Presence of the transmitter address is decided per subtype.
            Assert.Throws<ArgumentException>(() => new IEEE80211ControlFrame(
                FrameControl.Create(FrameSubtypes.ControlACK), 0, station, accessPoint));

            Assert.Throws<ArgumentException>(() => new IEEE80211ControlFrame(
                FrameControl.Create(FrameSubtypes.ControlRTS), 0, station));

            // Round-trip through the parser.
            foreach (var frame in new[] { ack, rts, psPoll })
            {
                var bytes = frame.GetBytes();
                Assert.That(IEEE80211ControlFrame.TryParse(bytes, out var parsed), Is.True);
                Assert.That(parsed!.GetBytes(), Is.EqualTo(bytes));
                Assert.That(parsed. Subtype,    Is.EqualTo(frame.Subtype));
            }

        }

        #endregion

        #region ManagementFrameRoundtrip()

        /// <summary>
        /// A beacon is a management frame with three fixed-meaning addresses.
        /// </summary>
        [Test]
        public void ManagementFrameRoundtrip()
        {

            var beacon = new IEEE80211ManagementFrame(
                             FrameControl.Create(FrameSubtypes.ManagementBeacon),
                             0,
                             MACAddress.Broadcast,
                             accessPoint,
                             accessPoint,
                             new SequenceControl(2048),
                             Body: new ReadOnlySequence<Byte>(Hex("0007566" + "16E616865696D72"))
                         );

            Assert.That(beacon.Type,                Is.EqualTo(FrameTypes.Management));
            Assert.That(beacon.Subtype,             Is.EqualTo(FrameSubtypes.ManagementBeacon));
            Assert.That(beacon.HeaderLength,        Is.EqualTo(24));
            Assert.That(beacon.IsBroadcast,         Is.True);
            Assert.That(beacon.BSSID,               Is.EqualTo(accessPoint));
            Assert.That(beacon.ReceiverAddress,     Is.EqualTo(MACAddress.Broadcast));
            Assert.That(beacon.TransmitterAddress,  Is.EqualTo(accessPoint));

            var bytes = beacon.GetBytes(IncludeFCS: true);

            Assert.That(AIEEE80211Frame.VerifyFCS(bytes), Is.True);

            Assert.That(IEEE80211ManagementFrame.TryParse(bytes, out var parsed, IncludesFCS: true), Is.True);
            Assert.That(parsed!.SequenceControl.SequenceNumber, Is.EqualTo(2048));
            Assert.That(parsed. Body.ToArray(),                 Is.EqualTo(beacon.Body.ToArray()));
            Assert.That(parsed. HasValidFCS,                    Is.True);

        }

        #endregion

        #region DispatcherPicksTheRightFrameClass()

        /// <summary>
        /// The abstract base dispatches by frame type.
        /// </summary>
        [Test]
        public void DispatcherPicksTheRightFrameClass()
        {

            var sequenceControl = new SequenceControl(1);

            var data = new IEEE80211DataFrame(
                           FrameControl.Create(FrameSubtypes.Data, FromDS: true),
                           0, station, accessPoint, wiredHost, sequenceControl
                       ).GetBytes();

            var management = new IEEE80211ManagementFrame(
                                 FrameControl.Create(FrameSubtypes.ManagementProbeRequest),
                                 0, MACAddress.Broadcast, station, MACAddress.Broadcast, sequenceControl
                             ).GetBytes();

            var control = new IEEE80211ControlFrame(
                              FrameControl.Create(FrameSubtypes.ControlCTS), 0, accessPoint
                          ).GetBytes();

            Assert.That(AIEEE80211Frame.TryParse(data,       out var f1), Is.True);
            Assert.That(AIEEE80211Frame.TryParse(management, out var f2), Is.True);
            Assert.That(AIEEE80211Frame.TryParse(control,    out var f3), Is.True);

            Assert.That(f1, Is.InstanceOf<IEEE80211DataFrame>());
            Assert.That(f2, Is.InstanceOf<IEEE80211ManagementFrame>());
            Assert.That(f3, Is.InstanceOf<IEEE80211ControlFrame>());

            Assert.That(f1!.HeaderLength, Is.EqualTo(24));
            Assert.That(f3!.HeaderLength, Is.EqualTo(10));

            Assert.That(AIEEE80211Frame.TryParse((Byte[]?) null, out _), Is.False);
            Assert.That(AIEEE80211Frame.TryParse(new Byte[3],    out _), Is.False);

            // Extension frames (IEEE 802.11ah and beyond) are a format of their own
            // and deliberately not decoded here.
            Assert.That(AIEEE80211Frame.TryParse(Hex("0C00" + "0000" + "000000000000"), out _), Is.False);

        }

        #endregion

        #region BridgeAWLANFrameOntoAWiredSegment()

        /// <summary>
        /// What an access point actually does: take the uplink frame of a station,
        /// unwrap its LLC/SNAP header and forward it as an Ethernet II frame. The
        /// addressing mode decides which of the three addresses end up where.
        /// </summary>
        [Test]
        public void BridgeAWLANFrameOntoAWiredSegment()
        {

            var payload = Encoding.ASCII.GetBytes("An IP packet on its way from the air onto a cable.");

            // The station sends upstream: Addr1 = BSSID, Addr2 = SA, Addr3 = DA.
            var uplink  = new IEEE80211DataFrame(
                              FrameControl.Create(FrameSubtypes.DataQoS, ToDS: true),
                              44,
                              Address1:         accessPoint,
                              Address2:         station,
                              Address3:         wiredHost,
                              SequenceControl:  new SequenceControl(99),
                              QoSControl:       new QoSControl(5),
                              Body:             BufferSegment.Sequence(
                                                    Hex("AAAA03" + "000000" + "0800"),  // LLC/SNAP, IPv4
                                                    payload
                                                )
                          );

            Assert.That(uplink.HeaderLength,     Is.EqualTo(26));
            Assert.That(uplink.IsQoSData,        Is.True);
            Assert.That(uplink.HasData,          Is.True);
            Assert.That(uplink.PayloadProtocol,  Is.EqualTo(EtherType.IPv4));

            Assert.That(uplink.TryGetLLCHeader (out var llcHeader),  Is.True);
            Assert.That(llcHeader.IsSNAP,                            Is.True);
            Assert.That(uplink.TryGetSNAPHeader(out var snapHeader), Is.True);
            Assert.That(snapHeader.ProtocolId,                       Is.EqualTo(EtherType.IPv4));

            // The QoS traffic identifier is the very same IEEE 802.1p priority
            // that a VLAN tag would carry on the wired side.
            Assert.That(uplink.QoSControl!.Value.UserPriority, Is.EqualTo(PCPPriorities.Voice));

            var ethernetFrame = uplink.ToEthernetFrame();

            Assert.That(ethernetFrame,                       Is.Not.Null);
            Assert.That(ethernetFrame!.DestinationAddress,   Is.EqualTo(wiredHost));
            Assert.That(ethernetFrame. SourceAddress,        Is.EqualTo(station));
            Assert.That(ethernetFrame. EtherTypeOrLength,    Is.EqualTo(EtherType.IPv4));
            Assert.That(ethernetFrame. Payload.ToArray(),    Is.EqualTo(payload));

            // An encrypted frame cannot be bridged, its body is not LLC/SNAP.
            var encrypted = new IEEE80211DataFrame(
                                FrameControl.Create(FrameSubtypes.Data, ToDS: true, Protected: true),
                                0, accessPoint, station, wiredHost, new SequenceControl(1),
                                Body: new ReadOnlySequence<Byte>(payload)
                            );

            Assert.That(encrypted.TryGetLLCHeader(out _), Is.False);
            Assert.That(encrypted.ToEthernetFrame(),      Is.Null);

            // Neither can a null function frame, which has no body at all.
            var nullFrame = new IEEE80211DataFrame(
                                FrameControl.Create(FrameSubtypes.DataNull, ToDS: true),
                                0, accessPoint, station, wiredHost, new SequenceControl(1)
                            );

            Assert.That(nullFrame.HasData,           Is.False);
            Assert.That(nullFrame.ToEthernetFrame(), Is.Null);

        }

        #endregion

        #region WriteFrameIntoACallerOwnedBuffer()

        /// <summary>
        /// The serialization path is span-first here as well.
        /// </summary>
        [Test]
        public void WriteFrameIntoACallerOwnedBuffer()
        {

            var frame = new IEEE80211DataFrame(
                            FrameControl.Create(FrameSubtypes.DataQoS, ToDS: true, FromDS: true),
                            0,
                            secondAP, accessPoint, wiredHost,
                            new SequenceControl(3),
                            Address4:   station,
                            QoSControl: new QoSControl(1),
                            Body:       new ReadOnlySequence<Byte>(Hex("0102030405"))
                        );

            Assert.That(frame.HeaderLength,                Is.EqualTo(32));
            Assert.That(frame.GetLength(),                 Is.EqualTo(37));
            Assert.That(frame.GetLength(IncludeFCS: true), Is.EqualTo(41));

            Span<Byte> tooSmall = stackalloc Byte[36];
            Assert.That(frame.TryWrite(tooSmall, out var notWritten), Is.False);
            Assert.That(notWritten, Is.EqualTo(0));

            Span<Byte> buffer = stackalloc Byte[64];
            Assert.That(frame.TryWrite(buffer, out var bytesWritten, IncludeFCS: true), Is.True);
            Assert.That(bytesWritten, Is.EqualTo(41));
            Assert.That(AIEEE80211Frame.VerifyFCS(buffer[..41]), Is.True);

            Assert.That(AIEEE80211Frame.TryParse(buffer[..41].ToArray(), out var parsed, IncludesFCS: true), Is.True);
            Assert.That(parsed!.HasValidFCS, Is.True);

        }

        #endregion

    }

}
