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

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Hermod.Ethernet;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.IEEE80211
{

    /// <summary>
    /// An IEEE 802.11 data frame - the only frame type that carries user payload.
    ///
    /// <code>
    /// +------+------+------+------+------+------+------+------+------+---------+
    /// |Frame |Dur / |Addr1 |Addr2 |Addr3 | Seq. |Addr4 | QoS  |  HT  |  Frame  |
    /// |Ctrl 2|ID   2|    6 |    6 |    6 |Ctrl 2|   6* |Ctrl 2|Ctrl 4|   Body  |
    /// +------+------+------+------+------+------+------+------+------+---------+
    ///                                            ^      ^      ^
    ///                                            |      |      +-- only if the order/+HTC bit is set
    ///                                            |      +--------- only for the QoS data subtypes
    ///                                            +---------------- only if ToDS = FromDS = 1
    /// </code>
    ///
    /// The three optional fields are what makes an IEEE 802.11 header variable-length,
    /// and each of them is announced by a bit inside the frame control field. The fourth
    /// address is therefore not a separate frame format, but one more optional field -
    /// exactly like a VLAN tag is to an Ethernet frame.
    ///
    /// What the four addresses mean, however, depends on the addressing mode:
    ///
    /// <code>
    /// ToDS FromDS | Address1 | Address2 | Address3 | Address4
    /// ------------+----------+----------+----------+---------
    ///   0     0   |    DA    |    SA    |  BSSID   |    -      independent BSS
    ///   0     1   |    DA    |  BSSID   |    SA    |    -      from the distribution system
    ///   1     0   |  BSSID   |    SA    |    DA    |    -      to the distribution system
    ///   1     1   |    RA    |    TA    |    DA    |   SA      wireless distribution system
    /// </code>
    ///
    /// Only the four-address mode carries the original destination and source next to
    /// the addresses of the wireless hop, which is what makes it able to bridge a whole
    /// Ethernet segment across a wireless link.
    /// </summary>
    public class IEEE80211DataFrame : AIEEE80211Frame
    {

        #region Properties

        /// <summary>
        /// The first address, which is always the address of the receiver of this hop.
        /// </summary>
        public MACAddress             Address1            { get; }

        /// <summary>
        /// The second address, which is always the address of the transmitter of this hop.
        /// </summary>
        public MACAddress             Address2            { get; }

        /// <summary>
        /// The third address, whose meaning depends on the addressing mode.
        /// </summary>
        public MACAddress             Address3            { get; }

        /// <summary>
        /// The fourth address, present only in the wireless distribution system mode.
        /// </summary>
        public MACAddress?            Address4            { get; }

        /// <summary>
        /// The sequence control field.
        /// </summary>
        public SequenceControl        SequenceControl     { get; }

        /// <summary>
        /// The QoS control field, present only for the QoS data subtypes.
        /// </summary>
        public QoSControl?            QoSControl          { get; }

        /// <summary>
        /// The HT control field, present only if the order / +HTC bit is set.
        /// </summary>
        public HTControl?             HTControl           { get; }

        /// <summary>
        /// The frame body, i.e. the MAC service data unit. For an unencrypted frame
        /// it usually starts with an IEEE 802.2 LLC/SNAP header.
        /// </summary>
        public ReadOnlySequence<Byte> Body                { get; }


        /// <summary>
        /// The addressing mode of this frame.
        /// </summary>
        public AddressModes  AddressMode

            => FrameControl.AddressMode;


        /// <summary>
        /// Whether this frame uses the four-address mode of a wireless distribution
        /// system, i.e. a WDS link or an IEEE 802.11s mesh.
        /// </summary>
        public Boolean       IsFourAddressFrame

            => Address4.HasValue;


        /// <summary>
        /// Whether this is one of the QoS data subtypes of IEEE 802.11e.
        /// </summary>
        public Boolean       IsQoSData

            => QoSControl.HasValue;


        /// <summary>
        /// Whether this frame actually carries data, as opposed to being one of
        /// the null function subtypes that only convey the frame control bits.
        /// </summary>
        public Boolean       HasData

            => Subtype is not FrameSubtypes.DataNull
                       and not FrameSubtypes.DataQoSNull
                       and not FrameSubtypes.DataCFAckNoData
                       and not FrameSubtypes.DataCFPollNoData
                       and not FrameSubtypes.DataCFAckCFPollNoData
                       and not FrameSubtypes.DataQoSCFPollNoData
                       and not FrameSubtypes.DataQoSCFAckCFPollNoData;


        /// <summary>
        /// The address of the station that receives this frame over the air.
        /// </summary>
        public MACAddress    ReceiverAddress

            => Address1;


        /// <summary>
        /// The address of the station that sends this frame over the air.
        /// </summary>
        public MACAddress    TransmitterAddress

            => Address2;


        /// <summary>
        /// The address of the final destination of the payload, which is not
        /// necessarily the receiver of this wireless hop.
        /// </summary>
        public MACAddress    DestinationAddress

            => AddressMode switch {
                   AddressModes.IndependentBSS          => Address1,
                   AddressModes.FromDistributionSystem  => Address1,
                   _                                    => Address3
               };


        /// <summary>
        /// The address of the original source of the payload, which is not
        /// necessarily the transmitter of this wireless hop.
        /// </summary>
        public MACAddress    SourceAddress

            => AddressMode switch {
                   AddressModes.IndependentBSS              => Address2,
                   AddressModes.FromDistributionSystem      => Address3,
                   AddressModes.ToDistributionSystem        => Address2,
                   AddressModes.WirelessDistributionSystem  => Address4!.Value,
                   _                                        => Address2
               };


        /// <summary>
        /// The identifier of the basic service set this frame belongs to, or null
        /// in the four-address mode, which spans two of them.
        /// </summary>
        public MACAddress?   BSSID

            => AddressMode switch {
                   AddressModes.IndependentBSS          => Address3,
                   AddressModes.FromDistributionSystem  => Address2,
                   AddressModes.ToDistributionSystem    => Address1,
                   _                                    => null
               };


        /// <summary>
        /// The length of the MAC header of this frame in bytes: 24, 26, 28, 30, 32 or 36,
        /// depending on which of the optional fields are present.
        /// </summary>
        public override UInt16 HeaderLength

            => (UInt16) (ThreeAddressHeaderLength +
                         (Address4.  HasValue ? 6                : 0) +
                         (QoSControl.HasValue ? IEEE80211.QoSControl.Length : 0) +
                         (HTControl. HasValue ? IEEE80211.HTControl.Length : 0));

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new IEEE 802.11 data frame.
        /// </summary>
        /// <param name="FrameControl">The frame control field, which decides which of the optional fields are present.</param>
        /// <param name="DurationOrId">The duration/ID field.</param>
        /// <param name="Address1">The receiver address of this hop.</param>
        /// <param name="Address2">The transmitter address of this hop.</param>
        /// <param name="Address3">The third address, whose meaning depends on the addressing mode.</param>
        /// <param name="SequenceControl">The sequence control field.</param>
        /// <param name="Address4">The fourth address, required if and only if ToDS = FromDS = 1.</param>
        /// <param name="QoSControl">The QoS control field, required if and only if this is a QoS data subtype.</param>
        /// <param name="HTControl">The HT control field, required if and only if the order / +HTC bit is set.</param>
        /// <param name="Body">The frame body.</param>
        /// <param name="FrameCheckSequence">An optional frame check sequence.</param>
        public IEEE80211DataFrame(FrameControl            FrameControl,
                                  UInt16                  DurationOrId,
                                  MACAddress              Address1,
                                  MACAddress              Address2,
                                  MACAddress              Address3,
                                  SequenceControl         SequenceControl,
                                  MACAddress?             Address4             = null,
                                  QoSControl?             QoSControl           = null,
                                  HTControl?              HTControl            = null,
                                  ReadOnlySequence<Byte>  Body                 = default,
                                  UInt32?                 FrameCheckSequence   = null)

            : base(FrameControl,
                   DurationOrId,
                   FrameCheckSequence)

        {

            if (FrameControl.Type != FrameTypes.Data)
                throw new ArgumentException($"'{FrameControl.Subtype}' is not a data frame subtype!",
                                            nameof(FrameControl));

            // The frame control field and the optional fields have to agree,
            // otherwise the frame could not be parsed back again!

            if (FrameControl.HasFourthAddress != Address4.HasValue)
                throw new ArgumentException(FrameControl.HasFourthAddress
                                                ? "The wireless distribution system mode (ToDS = FromDS = 1) requires a fourth address!"
                                                : "A fourth address is only allowed in the wireless distribution system mode (ToDS = FromDS = 1)!",
                                            nameof(Address4));

            if (FrameControl.HasQoSControl != QoSControl.HasValue)
                throw new ArgumentException(FrameControl.HasQoSControl
                                                ? $"The QoS data subtype '{FrameControl.Subtype}' requires a QoS control field!"
                                                : $"The non-QoS subtype '{FrameControl.Subtype}' must not carry a QoS control field!",
                                            nameof(QoSControl));

            if (FrameControl.HasHTControl != HTControl.HasValue)
                throw new ArgumentException(FrameControl.HasHTControl
                                                ? "A frame whose order / +HTC bit is set requires an HT control field!"
                                                : "An HT control field requires the order / +HTC bit to be set on a QoS data frame!",
                                            nameof(HTControl));

            this.Address1         = Address1;
            this.Address2         = Address2;
            this.Address3         = Address3;
            this.SequenceControl  = SequenceControl;
            this.Address4         = Address4;
            this.QoSControl       = QoSControl;
            this.HTControl        = HTControl;
            this.Body             = Body;

        }

        #endregion


        #region GetLength (IncludeFCS = false)

        /// <summary>
        /// Return the number of bytes that <see cref="TryWrite"/> will write.
        /// </summary>
        /// <param name="IncludeFCS">Whether a frame check sequence will be appended.</param>
        public override Int32 GetLength(Boolean IncludeFCS = false)

            => HeaderLength +
               (Int32) Body.Length +
               (IncludeFCS ? FCSLength : 0);

        #endregion

        #region TryWrite  (Destination, out BytesWritten, IncludeFCS = false)

        /// <summary>
        /// Write this frame in transmission order into the given destination span.
        /// </summary>
        /// <param name="Destination">The span to write the frame into.</param>
        /// <param name="BytesWritten">The number of bytes written into the destination span.</param>
        /// <param name="IncludeFCS">Whether to append a freshly computed frame check sequence.</param>
        public override Boolean TryWrite(Span<Byte>  Destination,
                                         out Int32   BytesWritten,
                                         Boolean     IncludeFCS   = false)
        {

            BytesWritten = 0;

            var length = GetLength(IncludeFCS);

            if (Destination.Length < length)
                return false;

            Destination  = Destination[..length];

            var offset   = WriteCommonHeader(Destination);

            Address1.CopyTo(Destination[offset..]);
            offset += 6;

            Address2.CopyTo(Destination[offset..]);
            offset += 6;

            Address3.CopyTo(Destination[offset..]);
            offset += 6;

            SequenceControl.WriteTo(Destination[offset..]);
            offset += IEEE80211.SequenceControl.Length;

            if (Address4.HasValue)
            {
                Address4.Value.CopyTo(Destination[offset..]);
                offset += 6;
            }

            if (QoSControl.HasValue)
            {
                QoSControl.Value.WriteTo(Destination[offset..]);
                offset += IEEE80211.QoSControl.Length;
            }

            if (HTControl.HasValue)
            {
                HTControl.Value.WriteTo(Destination[offset..]);
                offset += IEEE80211.HTControl.Length;
            }

            Body.CopyTo(Destination[offset..]);
            offset += (Int32) Body.Length;

            if (IncludeFCS)
                WriteFCS(Destination, offset);

            BytesWritten = length;
            return true;

        }

        #endregion

        #region (static) TryParse (Bytes, out DataFrame, IncludesFCS = false)

        /// <summary>
        /// Try to parse the given bytes as an IEEE 802.11 data frame.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame, starting with the frame control field.</param>
        /// <param name="DataFrame">The parsed data frame.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the frame check sequence.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>                           Bytes,
                                       [NotNullWhen(true)] out IEEE80211DataFrame?  DataFrame,
                                       Boolean                                      IncludesFCS   = false)
        {

            DataFrame = null;

            if (!TryReadCommonHeader(Bytes,
                                     IncludesFCS,
                                     out var frameControl,
                                     out var durationOrId,
                                     out var frameCheckSequence,
                                     out var available))
            {
                return false;
            }

            if (frameControl.Type != FrameTypes.Data)
                return false;

            var offset = (Int32) MinHeaderLength;

            if (available < ThreeAddressHeaderLength)
                return false;

            var address1 = MACAddress.TryFrom(Bytes.Slice(offset, 6));  offset += 6;
            var address2 = MACAddress.TryFrom(Bytes.Slice(offset, 6));  offset += 6;
            var address3 = MACAddress.TryFrom(Bytes.Slice(offset, 6));  offset += 6;

            if (!address1.HasValue || !address2.HasValue || !address3.HasValue)
                return false;

            if (!IEEE80211.SequenceControl.TryParse(Bytes[offset..available], out var sequenceControl))
                return false;

            offset += IEEE80211.SequenceControl.Length;

            MACAddress? address4   = null;
            QoSControl? qosControl = null;
            HTControl?  htControl  = null;

            if (frameControl.HasFourthAddress)
            {

                if (offset + 6 > available)
                    return false;

                address4 = MACAddress.TryFrom(Bytes.Slice(offset, 6));

                if (!address4.HasValue)
                    return false;

                offset += 6;

            }

            if (frameControl.HasQoSControl)
            {

                if (!IEEE80211.QoSControl.TryParse(Bytes[offset..available], out var qos))
                    return false;

                qosControl  = qos;
                offset     += IEEE80211.QoSControl.Length;

            }

            if (frameControl.HasHTControl)
            {

                if (!IEEE80211.HTControl.TryParse(Bytes[offset..available], out var ht))
                    return false;

                htControl  = ht;
                offset    += IEEE80211.HTControl.Length;

            }

            DataFrame = new IEEE80211DataFrame(
                            frameControl,
                            durationOrId,
                            address1.Value,
                            address2.Value,
                            address3.Value,
                            sequenceControl,
                            address4,
                            qosControl,
                            htControl,
                            new ReadOnlySequence<Byte>(Bytes[offset..available].ToArray()),
                            frameCheckSequence
                        );

            return true;

        }

        #endregion


        #region TryGetLLCHeader  (out LLCHeader)

        /// <summary>
        /// Try to read the IEEE 802.2 LLC header from the body of this frame.
        /// An encrypted frame body starts with a cryptographic header instead.
        /// </summary>
        /// <param name="LLCHeader">The parsed LLC header.</param>
        public Boolean TryGetLLCHeader(out LLCHeader LLCHeader)
        {

            LLCHeader = default;

            if (FrameControl.Protected || !HasData)
                return false;

            Span<Byte> prefix = stackalloc Byte[4 + SNAPHeader.Length];
            var        length = (Int32) Math.Min(prefix.Length, Body.Length);

            Body.Slice(0, length).CopyTo(prefix);

            return Ethernet.LLCHeader.TryParse(prefix[..length], out LLCHeader);

        }

        #endregion

        #region TryGetSNAPHeader (out SNAPHeader)

        /// <summary>
        /// Try to read the SubNetwork Access Protocol header from the body of this frame.
        /// </summary>
        /// <param name="SNAPHeader">The parsed SNAP header.</param>
        public Boolean TryGetSNAPHeader(out SNAPHeader SNAPHeader)
        {

            SNAPHeader = default;

            if (!TryGetLLCHeader(out var llcHeader) || !llcHeader.IsSNAP)
                return false;

            Span<Byte> prefix = stackalloc Byte[4 + Ethernet.SNAPHeader.Length];
            var        length = (Int32) Math.Min(prefix.Length, Body.Length);

            Body.Slice(0, length).CopyTo(prefix);

            return length >= llcHeader.Length + Ethernet.SNAPHeader.Length &&
                   Ethernet.SNAPHeader.TryParse(prefix[llcHeader.Length..length], out SNAPHeader);

        }

        #endregion

        #region PayloadProtocol

        /// <summary>
        /// The EtherType of the payload, if the frame body carries an LLC/SNAP header.
        /// </summary>
        public EtherType? PayloadProtocol

            => TryGetSNAPHeader(out var snapHeader)
                   ? snapHeader.ProtocolId
                   : null;

        #endregion

        #region ToEthernetFrame  ()

        /// <summary>
        /// Convert this frame into the Ethernet frame that an access point or a bridge
        /// would forward onto its wired segment: the LLC/SNAP header is dropped and its
        /// protocol identifier becomes the EtherType, while the destination and source
        /// addresses are taken from wherever the addressing mode put them.
        ///
        /// Returns null for frames that cannot be bridged: encrypted ones, null function
        /// ones and those whose body is not LLC/SNAP encapsulated.
        /// </summary>
        public EthernetFrame? ToEthernetFrame()
        {

            if (!TryGetLLCHeader (out var llcHeader) || !llcHeader.IsSNAP ||
                !TryGetSNAPHeader(out var snapHeader))
            {
                return null;
            }

            // Only the RFC 1042 OUI turns the protocol identifier into an EtherType.
            if (!snapHeader.IsRFC1042)
                return null;

            var offset = llcHeader.Length + Ethernet.SNAPHeader.Length;

            return new EthernetFrame(
                       DestinationAddress,
                       SourceAddress,
                       snapHeader.ProtocolId,
                       Body.Slice(offset)
                   );

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"{FrameControl}: ",

                   IsFourAddressFrame
                       ? $"{SourceAddress} -> {DestinationAddress} via {TransmitterAddress} -> {ReceiverAddress}"
                       : $"{SourceAddress} -> {DestinationAddress}",

                   BSSID.HasValue
                       ? $" [BSSID {BSSID}]"
                       : "",

                   QoSControl.HasValue
                       ? $", {QoSControl}"
                       : "",

                   $", {Body.Length} byte(s) body"

               );

        #endregion

    }

}
