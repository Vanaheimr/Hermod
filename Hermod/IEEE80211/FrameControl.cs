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

using System.Buffers.Binary;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.IEEE80211
{

    /// <summary>
    /// The four IEEE 802.11 frame types.
    /// </summary>
    public enum FrameTypes : Byte
    {

        /// <summary>
        /// Management frames: beacons, probes, association, authentication, ...
        /// </summary>
        Management  = 0,

        /// <summary>
        /// Control frames: RTS, CTS, ACK, Block Ack, PS-Poll, ...
        /// </summary>
        Control     = 1,

        /// <summary>
        /// Data frames, the only ones that carry a MAC service data unit.
        /// </summary>
        Data        = 2,

        /// <summary>
        /// Extension frames (IEEE 802.11ah DMG Beacon and beyond).
        /// </summary>
        Extension   = 3

    }


    /// <summary>
    /// The IEEE 802.11 frame subtypes, encoded as (type &lt;&lt; 4) | subtype,
    /// so that a subtype is unique across all frame types.
    /// </summary>
    public enum FrameSubtypes : Byte
    {

        #region Management (0x0X)

        /// <summary>
        /// Association request.
        /// </summary>
        ManagementAssociationRequest     = 0x00,

        /// <summary>
        /// Association response.
        /// </summary>
        ManagementAssociationResponse    = 0x01,

        /// <summary>
        /// Reassociation request.
        /// </summary>
        ManagementReassociationRequest   = 0x02,

        /// <summary>
        /// Reassociation response.
        /// </summary>
        ManagementReassociationResponse  = 0x03,

        /// <summary>
        /// Probe request.
        /// </summary>
        ManagementProbeRequest           = 0x04,

        /// <summary>
        /// Probe response.
        /// </summary>
        ManagementProbeResponse          = 0x05,

        /// <summary>
        /// Timing advertisement (IEEE 802.11p).
        /// </summary>
        ManagementTimingAdvertisement    = 0x06,

        /// <summary>
        /// Beacon.
        /// </summary>
        ManagementBeacon                 = 0x08,

        /// <summary>
        /// Announcement traffic indication message.
        /// </summary>
        ManagementATIM                   = 0x09,

        /// <summary>
        /// Disassociation.
        /// </summary>
        ManagementDisassociation         = 0x0A,

        /// <summary>
        /// Authentication.
        /// </summary>
        ManagementAuthentication         = 0x0B,

        /// <summary>
        /// Deauthentication.
        /// </summary>
        ManagementDeauthentication       = 0x0C,

        /// <summary>
        /// Action.
        /// </summary>
        ManagementAction                 = 0x0D,

        /// <summary>
        /// Action, no acknowledgment (IEEE 802.11w).
        /// </summary>
        ManagementActionNoAck            = 0x0E,

        #endregion

        #region Control (0x1X)

        /// <summary>
        /// Beamforming report poll (IEEE 802.11ac).
        /// </summary>
        ControlBeamformingReportPoll     = 0x14,

        /// <summary>
        /// VHT null data packet announcement (IEEE 802.11ac).
        /// </summary>
        ControlVHTNDPAnnouncement        = 0x15,

        /// <summary>
        /// Control frame extension.
        /// </summary>
        ControlFrameExtension            = 0x16,

        /// <summary>
        /// Control wrapper.
        /// </summary>
        ControlWrapper                   = 0x17,

        /// <summary>
        /// Block acknowledgment request.
        /// </summary>
        ControlBlockAckRequest           = 0x18,

        /// <summary>
        /// Block acknowledgment.
        /// </summary>
        ControlBlockAck                  = 0x19,

        /// <summary>
        /// Power save poll.
        /// </summary>
        ControlPSPoll                    = 0x1A,

        /// <summary>
        /// Request to send.
        /// </summary>
        ControlRTS                       = 0x1B,

        /// <summary>
        /// Clear to send.
        /// </summary>
        ControlCTS                       = 0x1C,

        /// <summary>
        /// Acknowledgment.
        /// </summary>
        ControlACK                       = 0x1D,

        /// <summary>
        /// Contention free period end.
        /// </summary>
        ControlCFEnd                     = 0x1E,

        /// <summary>
        /// Contention free period end + contention free acknowledgment.
        /// </summary>
        ControlCFEndCFAck                = 0x1F,

        #endregion

        #region Data (0x2X)

        /// <summary>
        /// Data.
        /// </summary>
        Data                             = 0x20,

        /// <summary>
        /// Data + contention free acknowledgment.
        /// </summary>
        DataCFAck                        = 0x21,

        /// <summary>
        /// Data + contention free poll.
        /// </summary>
        DataCFPoll                       = 0x22,

        /// <summary>
        /// Data + contention free acknowledgment + poll.
        /// </summary>
        DataCFAckCFPoll                  = 0x23,

        /// <summary>
        /// Null function, no data.
        /// </summary>
        DataNull                         = 0x24,

        /// <summary>
        /// Contention free acknowledgment, no data.
        /// </summary>
        DataCFAckNoData                  = 0x25,

        /// <summary>
        /// Contention free poll, no data.
        /// </summary>
        DataCFPollNoData                 = 0x26,

        /// <summary>
        /// Contention free acknowledgment + poll, no data.
        /// </summary>
        DataCFAckCFPollNoData            = 0x27,

        /// <summary>
        /// QoS data (IEEE 802.11e).
        /// </summary>
        DataQoS                          = 0x28,

        /// <summary>
        /// QoS data + contention free acknowledgment.
        /// </summary>
        DataQoSCFAck                     = 0x29,

        /// <summary>
        /// QoS data + contention free poll.
        /// </summary>
        DataQoSCFPoll                    = 0x2A,

        /// <summary>
        /// QoS data + contention free acknowledgment + poll.
        /// </summary>
        DataQoSCFAckCFPoll               = 0x2B,

        /// <summary>
        /// QoS null, no data.
        /// </summary>
        DataQoSNull                      = 0x2C,

        /// <summary>
        /// QoS contention free poll, no data.
        /// </summary>
        DataQoSCFPollNoData              = 0x2E,

        /// <summary>
        /// QoS contention free acknowledgment + poll, no data.
        /// </summary>
        DataQoSCFAckCFPollNoData         = 0x2F

        #endregion

    }


    /// <summary>
    /// The four addressing modes of IEEE 802.11, selected by the
    /// ToDS and FromDS bits of the frame control field.
    /// </summary>
    public enum AddressModes : Byte
    {

        /// <summary>
        /// ToDS = 0, FromDS = 0: within an independent BSS (ad-hoc),
        /// or any management frame. Three addresses.
        /// </summary>
        IndependentBSS              = 0,

        /// <summary>
        /// ToDS = 0, FromDS = 1: from the distribution system to a station,
        /// i.e. the downlink of an infrastructure BSS. Three addresses.
        /// </summary>
        FromDistributionSystem      = 1,

        /// <summary>
        /// ToDS = 1, FromDS = 0: from a station into the distribution system,
        /// i.e. the uplink of an infrastructure BSS. Three addresses.
        /// </summary>
        ToDistributionSystem        = 2,

        /// <summary>
        /// ToDS = 1, FromDS = 1: between two access points or mesh stations.
        /// This is the four-address mode, the only one that carries the original
        /// destination and source next to the wireless hop.
        /// </summary>
        WirelessDistributionSystem  = 3

    }


    /// <summary>
    /// The 2-byte IEEE 802.11 frame control field.
    ///
    /// <code>
    /// Octet 0                          Octet 1
    /// +--------+------+---------+      +----+----+----+----+----+----+----+----+
    /// |Subtype | Type | Version |      |Ordr|Prot|More|Pwr |Retr|More|From| To |
    /// | (4)    | (2)  |  (2)    |      |    |    |Data|Mgmt|    |Frag| DS | DS |
    /// +--------+------+---------+      +----+----+----+----+----+----+----+----+
    ///  b7    b4 b3  b2 b1     b0        b7   b6   b5   b4   b3   b2   b1   b0
    /// </code>
    ///
    /// Note that the presence of several later fields - the fourth address, the QoS
    /// control field and the HT control field - is decided by bits within this field.
    /// An IEEE 802.11 MAC header therefore has no fixed length, unlike an Ethernet one.
    /// </summary>
    public readonly struct FrameControl : IEquatable<FrameControl>
    {

        #region Data

        /// <summary>
        /// The number of bytes of a frame control field.
        /// </summary>
        public const Byte Length = 2;

        #endregion

        #region Properties

        /// <summary>
        /// The raw value of this frame control field, with octet 0 in the
        /// high byte and octet 1 in the low byte.
        /// </summary>
        public UInt16         Value              { get; }


        /// <summary>
        /// The protocol version, always 0 for every IEEE 802.11 revision so far.
        /// </summary>
        public Byte           ProtocolVersion

            => (Byte) ((Value >> 8) & 0x03);


        /// <summary>
        /// The frame type.
        /// </summary>
        public FrameTypes     Type

            => (FrameTypes) ((Value >> 10) & 0x03);


        /// <summary>
        /// The frame subtype, combined with the frame type.
        /// </summary>
        public FrameSubtypes  Subtype

            => (FrameSubtypes) ((((Value >> 10) & 0x03) << 4) |
                                 ((Value >> 12) & 0x0F));


        /// <summary>
        /// Whether this frame is destined for the distribution system.
        /// </summary>
        public Boolean        ToDS

            => (Value & 0x0001) != 0;


        /// <summary>
        /// Whether this frame originates from the distribution system.
        /// </summary>
        public Boolean        FromDS

            => (Value & 0x0002) != 0;


        /// <summary>
        /// Whether more fragments of the current MSDU follow.
        /// </summary>
        public Boolean        MoreFragments

            => (Value & 0x0004) != 0;


        /// <summary>
        /// Whether this frame is a retransmission.
        /// </summary>
        public Boolean        Retry

            => (Value & 0x0008) != 0;


        /// <summary>
        /// Whether the sending station will be in power save mode afterwards.
        /// </summary>
        public Boolean        PowerManagement

            => (Value & 0x0010) != 0;


        /// <summary>
        /// Whether more frames are buffered for the receiving station.
        /// </summary>
        public Boolean        MoreData

            => (Value & 0x0020) != 0;


        /// <summary>
        /// Whether the frame body is encrypted (WEP, TKIP, CCMP or GCMP).
        /// </summary>
        public Boolean        Protected

            => (Value & 0x0040) != 0;


        /// <summary>
        /// The order bit. For QoS data and management frames it is the "+HTC" bit,
        /// which announces an HT control field.
        /// </summary>
        public Boolean        Order

            => (Value & 0x0080) != 0;


        /// <summary>
        /// The addressing mode, i.e. the combination of the ToDS and FromDS bits.
        /// </summary>
        public AddressModes   AddressMode

            => (AddressModes) (((ToDS   ? 1 : 0) << 1) |
                                (FromDS ? 1 : 0));


        /// <summary>
        /// Whether this frame carries a fourth address, which is exactly the case
        /// for the wireless distribution system mode (ToDS = FromDS = 1).
        /// </summary>
        public Boolean        HasFourthAddress

            => ToDS && FromDS;


        /// <summary>
        /// Whether this frame carries a QoS control field, i.e. whether it is one
        /// of the QoS data subtypes introduced by IEEE 802.11e.
        /// </summary>
        public Boolean        HasQoSControl

            => Type == FrameTypes.Data &&
               ((Byte) Subtype & 0x08) != 0;


        /// <summary>
        /// Whether this frame carries an HT control field (IEEE 802.11n and later),
        /// which is announced by the order bit of QoS data and management frames.
        /// </summary>
        public Boolean        HasHTControl

            => Order &&
               (Type == FrameTypes.Management || HasQoSControl);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new frame control field.
        /// </summary>
        /// <param name="Value">The raw value, octet 0 in the high byte.</param>
        private FrameControl(UInt16 Value)
        {
            this.Value = Value;
        }

        #endregion


        #region From     (Value)

        /// <summary>
        /// Create a new frame control field from its raw value.
        /// </summary>
        /// <param name="Value">The raw value, octet 0 in the high byte.</param>
        public static FrameControl From(UInt16 Value)

            => new (Value);

        #endregion

        #region Create   (Subtype, ToDS = false, FromDS = false, ...)

        /// <summary>
        /// Create a new frame control field from its individual fields.
        /// </summary>
        /// <param name="Subtype">The frame subtype, which also determines the frame type.</param>
        /// <param name="ToDS">Whether this frame is destined for the distribution system.</param>
        /// <param name="FromDS">Whether this frame originates from the distribution system.</param>
        /// <param name="MoreFragments">Whether more fragments follow.</param>
        /// <param name="Retry">Whether this frame is a retransmission.</param>
        /// <param name="PowerManagement">Whether the sender will be in power save mode.</param>
        /// <param name="MoreData">Whether more frames are buffered for the receiver.</param>
        /// <param name="Protected">Whether the frame body is encrypted.</param>
        /// <param name="Order">The order / +HTC bit.</param>
        /// <param name="ProtocolVersion">The protocol version, always 0 so far.</param>
        public static FrameControl Create(FrameSubtypes  Subtype,
                                          Boolean        ToDS              = false,
                                          Boolean        FromDS            = false,
                                          Boolean        MoreFragments     = false,
                                          Boolean        Retry             = false,
                                          Boolean        PowerManagement   = false,
                                          Boolean        MoreData          = false,
                                          Boolean        Protected         = false,
                                          Boolean        Order             = false,
                                          Byte           ProtocolVersion   = 0)
        {

            if (ProtocolVersion > 3)
                throw new ArgumentOutOfRangeException(nameof(ProtocolVersion),
                                                      "The protocol version must be in the range of 0..3!");

            var type     = (Byte) (((Byte) Subtype >> 4) & 0x03);
            var subtype  = (Byte)  ((Byte) Subtype       & 0x0F);

            var octet0   = (Byte) ((subtype << 4) |
                                   (type    << 2) |
                                   ProtocolVersion);

            var octet1   = (Byte) ((ToDS             ? 0x01 : 0x00) |
                                   (FromDS           ? 0x02 : 0x00) |
                                   (MoreFragments    ? 0x04 : 0x00) |
                                   (Retry            ? 0x08 : 0x00) |
                                   (PowerManagement  ? 0x10 : 0x00) |
                                   (MoreData         ? 0x20 : 0x00) |
                                   (Protected        ? 0x40 : 0x00) |
                                   (Order            ? 0x80 : 0x00));

            return new FrameControl((UInt16) ((octet0 << 8) | octet1));

        }

        #endregion

        #region TryParse (Bytes, out FrameControl)

        /// <summary>
        /// Try to read a frame control field from the beginning of the given bytes.
        /// </summary>
        /// <param name="Bytes">A span of at least 2 bytes.</param>
        /// <param name="FrameControl">The parsed frame control field.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>  Bytes,
                                       out FrameControl    FrameControl)
        {

            FrameControl = default;

            if (Bytes.Length < Length)
                return false;

            // Octet 0 carries version, type and subtype and thus becomes the high byte.
            FrameControl = new FrameControl(BinaryPrimitives.ReadUInt16BigEndian(Bytes));

            return true;

        }

        #endregion


        #region GetBytes ()

        /// <summary>
        /// Return the 2 bytes of this frame control field in transmission order.
        /// </summary>
        public Byte[] GetBytes()

            => [ (Byte) (Value >> 8),
                 (Byte) (Value & 0xFF) ];

        #endregion

        #region WriteTo  (Destination)

        /// <summary>
        /// Write the 2 bytes of this frame control field into the given destination span.
        /// </summary>
        /// <param name="Destination">A span to write the bytes into.</param>
        public void WriteTo(Span<Byte> Destination)
        {

            if (Destination.Length < Length)
                throw new ArgumentException("Destination span too small.", nameof(Destination));

            BinaryPrimitives.WriteUInt16BigEndian(Destination, Value);

        }

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FrameControl1">A frame control field.</param>
        /// <param name="FrameControl2">Another frame control field.</param>
        public static Boolean operator == (FrameControl FrameControl1,
                                           FrameControl FrameControl2)

            => FrameControl1.Equals(FrameControl2);


        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="FrameControl1">A frame control field.</param>
        /// <param name="FrameControl2">Another frame control field.</param>
        public static Boolean operator != (FrameControl FrameControl1,
                                           FrameControl FrameControl2)

            => !FrameControl1.Equals(FrameControl2);

        #endregion

        #region IEquatable<FrameControl> Members

        /// <summary>
        /// Compares two frame control fields for equality.
        /// </summary>
        /// <param name="Object">A frame control field to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is FrameControl frameControl &&
                   Equals(frameControl);


        /// <summary>
        /// Compares two frame control fields for equality.
        /// </summary>
        /// <param name="FrameControl">A frame control field to compare with.</param>
        public Boolean Equals(FrameControl FrameControl)

            => Value == FrameControl.Value;

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => Value.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   Subtype.ToString(),

                   $" [{AddressMode}]",

                   Retry            ? ", retry"      : "",
                   Protected        ? ", protected"  : "",
                   MoreFragments    ? ", more frags" : "",
                   MoreData         ? ", more data"  : "",
                   PowerManagement  ? ", power save" : "",
                   HasHTControl     ? ", +HTC"       : ""

               );

        #endregion

    }

}
