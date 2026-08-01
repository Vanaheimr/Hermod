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
    /// An IEEE 802.11 control frame: RTS, CTS, ACK, Block Ack, PS-Poll and the
    /// contention free period delimiters.
    ///
    /// This is the frame family that genuinely is a format of its own rather than a
    /// variation of the data and management header: control frames have no sequence
    /// control field and no MAC service data unit at all, and the number of addresses
    /// is decided per subtype instead of by the ToDS and FromDS bits:
    ///
    /// <code>
    /// ACK, CTS                     : Frame Control | Duration | RA               | FCS   = 14 bytes
    /// RTS, PS-Poll, CF-End, BlockAck: Frame Control | Duration | RA | TA | [info] | FCS  = 20+ bytes
    /// </code>
    ///
    /// Squeezing these into the data frame layout would mean four optional fields that
    /// are almost always absent, which is why they get their own class.
    /// </summary>
    public class IEEE80211ControlFrame : AIEEE80211Frame
    {

        #region Data

        /// <summary>
        /// The length of a control frame header carrying only a receiver address.
        /// </summary>
        public const UInt16 OneAddressHeaderLength  = MinHeaderLength + 6;   // 10

        /// <summary>
        /// The length of a control frame header carrying a receiver and a transmitter address.
        /// </summary>
        public const UInt16 TwoAddressHeaderLength  = MinHeaderLength + 12;  // 16

        #endregion

        #region Properties

        /// <summary>
        /// The address of the station this frame is directed at.
        /// </summary>
        public MACAddress             ReceiverAddress     { get; }

        /// <summary>
        /// The address of the sending station. Acknowledgments and clear to send
        /// frames do not carry one - which is precisely why they are so short.
        /// </summary>
        public MACAddress?            TransmitterAddress  { get; }

        /// <summary>
        /// Any subtype specific information following the addresses, e.g. the block
        /// acknowledgment control field and bitmap of a Block Ack frame.
        /// </summary>
        public ReadOnlySequence<Byte> Body                { get; }


        /// <summary>
        /// The identifier of the basic service set, for the subtypes that carry one:
        /// a PS-Poll frame addresses its access point, a CF-End frame announces the
        /// end of a contention free period on behalf of it.
        /// </summary>
        public MACAddress?  BSSID

            => Subtype switch {
                   FrameSubtypes.ControlPSPoll      => ReceiverAddress,
                   FrameSubtypes.ControlCFEnd       => TransmitterAddress,
                   FrameSubtypes.ControlCFEndCFAck  => TransmitterAddress,
                   _                                => null
               };


        /// <summary>
        /// The length of the MAC header of this frame in bytes: 10 or 16.
        /// </summary>
        public override UInt16 HeaderLength

            => TransmitterAddress.HasValue
                   ? TwoAddressHeaderLength
                   : OneAddressHeaderLength;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new IEEE 802.11 control frame.
        /// </summary>
        /// <param name="FrameControl">The frame control field.</param>
        /// <param name="DurationOrId">The duration/ID field, or the association identifier of a PS-Poll frame.</param>
        /// <param name="ReceiverAddress">The address of the station this frame is directed at.</param>
        /// <param name="TransmitterAddress">The address of the sending station, required for every subtype but ACK and CTS.</param>
        /// <param name="Body">Any subtype specific information following the addresses.</param>
        /// <param name="FrameCheckSequence">An optional frame check sequence.</param>
        public IEEE80211ControlFrame(FrameControl            FrameControl,
                                     UInt16                  DurationOrId,
                                     MACAddress              ReceiverAddress,
                                     MACAddress?             TransmitterAddress   = null,
                                     ReadOnlySequence<Byte>  Body                 = default,
                                     UInt32?                 FrameCheckSequence   = null)

            : base(FrameControl,
                   DurationOrId,
                   FrameCheckSequence)

        {

            if (FrameControl.Type != FrameTypes.Control)
                throw new ArgumentException($"'{FrameControl.Subtype}' is not a control frame subtype!",
                                            nameof(FrameControl));

            if (HasTransmitterAddress(FrameControl.Subtype) != TransmitterAddress.HasValue)
                throw new ArgumentException(HasTransmitterAddress(FrameControl.Subtype)
                                                ? $"A '{FrameControl.Subtype}' frame requires a transmitter address!"
                                                : $"A '{FrameControl.Subtype}' frame must not carry a transmitter address!",
                                            nameof(TransmitterAddress));

            this.ReceiverAddress     = ReceiverAddress;
            this.TransmitterAddress  = TransmitterAddress;
            this.Body                = Body;

        }

        #endregion


        #region (static) HasTransmitterAddress (Subtype)

        /// <summary>
        /// Whether a control frame of the given subtype carries a transmitter address.
        /// Acknowledgments and clear to send frames do not, every other subtype does.
        /// </summary>
        /// <param name="Subtype">A control frame subtype.</param>
        public static Boolean HasTransmitterAddress(FrameSubtypes Subtype)

            => Subtype is FrameSubtypes.ControlBeamformingReportPoll
                       or FrameSubtypes.ControlVHTNDPAnnouncement
                       or FrameSubtypes.ControlBlockAckRequest
                       or FrameSubtypes.ControlBlockAck
                       or FrameSubtypes.ControlPSPoll
                       or FrameSubtypes.ControlRTS
                       or FrameSubtypes.ControlCFEnd
                       or FrameSubtypes.ControlCFEndCFAck;

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

            ReceiverAddress.CopyTo(Destination[offset..]);
            offset += 6;

            if (TransmitterAddress.HasValue)
            {
                TransmitterAddress.Value.CopyTo(Destination[offset..]);
                offset += 6;
            }

            Body.CopyTo(Destination[offset..]);
            offset += (Int32) Body.Length;

            if (IncludeFCS)
                WriteFCS(Destination, offset);

            BytesWritten = length;
            return true;

        }

        #endregion

        #region (static) TryParse (Bytes, out ControlFrame, IncludesFCS = false)

        /// <summary>
        /// Try to parse the given bytes as an IEEE 802.11 control frame.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame, starting with the frame control field.</param>
        /// <param name="ControlFrame">The parsed control frame.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the frame check sequence.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>                              Bytes,
                                       [NotNullWhen(true)] out IEEE80211ControlFrame?  ControlFrame,
                                       Boolean                                         IncludesFCS   = false)
        {

            ControlFrame = null;

            if (!TryReadCommonHeader(Bytes,
                                     IncludesFCS,
                                     out var frameControl,
                                     out var durationOrId,
                                     out var frameCheckSequence,
                                     out var available))
            {
                return false;
            }

            if (frameControl.Type != FrameTypes.Control)
                return false;

            var needsTransmitter  = HasTransmitterAddress(frameControl.Subtype);
            var headerLength      = needsTransmitter
                                        ? TwoAddressHeaderLength
                                        : OneAddressHeaderLength;

            if (available < headerLength)
                return false;

            var offset             = (Int32) MinHeaderLength;

            var receiverAddress    = MACAddress.TryFrom(Bytes.Slice(offset, 6));
            offset += 6;

            if (!receiverAddress.HasValue)
                return false;

            MACAddress? transmitterAddress = null;

            if (needsTransmitter)
            {

                transmitterAddress = MACAddress.TryFrom(Bytes.Slice(offset, 6));

                if (!transmitterAddress.HasValue)
                    return false;

                offset += 6;

            }

            ControlFrame = new IEEE80211ControlFrame(
                               frameControl,
                               durationOrId,
                               receiverAddress.Value,
                               transmitterAddress,
                               new ReadOnlySequence<Byte>(Bytes[offset..available].ToArray()),
                               frameCheckSequence
                           );

            return true;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"{FrameControl}: ",

                   TransmitterAddress.HasValue
                       ? $"{TransmitterAddress} -> {ReceiverAddress}"
                       : $"-> {ReceiverAddress}",

                   AssociationId.HasValue
                       ? $", AID {AssociationId}"
                       : Duration.HasValue
                             ? $", {Duration} us"
                             : "",

                   Body.Length > 0
                       ? $", {Body.Length} byte(s) info"
                       : ""

               );

        #endregion

    }

}
