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
    /// An IEEE 802.11 management frame: beacons, probes, association, authentication
    /// and action frames.
    ///
    /// <code>
    /// +------+------+------+------+------+------+------+---------+
    /// |Frame |Dur  2|  DA  |  SA  |BSSID | Seq. |  HT  |  Frame  |
    /// |Ctrl 2|      |    6 |    6 |    6 |Ctrl 2|Ctrl 4|   Body  |
    /// +------+------+------+------+------+------+------+---------+
    ///                                            ^
    ///                                            +-- only if the order/+HTC bit is set
    /// </code>
    ///
    /// Management frames always carry exactly three addresses and always in the same
    /// meaning - their ToDS and FromDS bits are both zero. The four-address mode
    /// therefore simply does not exist here.
    /// </summary>
    public class IEEE80211ManagementFrame : AIEEE80211Frame
    {

        #region Properties

        /// <summary>
        /// The destination address, which is also the receiver address.
        /// </summary>
        public MACAddress             DestinationAddress  { get; }

        /// <summary>
        /// The source address, which is also the transmitter address.
        /// </summary>
        public MACAddress             SourceAddress       { get; }

        /// <summary>
        /// The identifier of the basic service set.
        /// </summary>
        public MACAddress             BSSID               { get; }

        /// <summary>
        /// The sequence control field.
        /// </summary>
        public SequenceControl        SequenceControl     { get; }

        /// <summary>
        /// The HT control field, present only if the order / +HTC bit is set.
        /// </summary>
        public HTControl?             HTControl           { get; }

        /// <summary>
        /// The frame body, which for most subtypes is a list of information elements.
        /// </summary>
        public ReadOnlySequence<Byte> Body                { get; }


        /// <summary>
        /// The receiver address of this hop, which for a management frame
        /// is always the destination address.
        /// </summary>
        public MACAddress    ReceiverAddress

            => DestinationAddress;


        /// <summary>
        /// The transmitter address of this hop, which for a management frame
        /// is always the source address.
        /// </summary>
        public MACAddress    TransmitterAddress

            => SourceAddress;


        /// <summary>
        /// Whether this frame is broadcast to every station, as beacons
        /// and wildcard probe requests are.
        /// </summary>
        public Boolean       IsBroadcast

            => DestinationAddress.IsBroadcast;


        /// <summary>
        /// The length of the MAC header of this frame in bytes: 24, or 28 with
        /// an HT control field.
        /// </summary>
        public override UInt16 HeaderLength

            => (UInt16) (ThreeAddressHeaderLength +
                         (HTControl.HasValue ? IEEE80211.HTControl.Length : 0));

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new IEEE 802.11 management frame.
        /// </summary>
        /// <param name="FrameControl">The frame control field.</param>
        /// <param name="DurationOrId">The duration/ID field.</param>
        /// <param name="DestinationAddress">The destination address.</param>
        /// <param name="SourceAddress">The source address.</param>
        /// <param name="BSSID">The identifier of the basic service set.</param>
        /// <param name="SequenceControl">The sequence control field.</param>
        /// <param name="HTControl">The HT control field, required if and only if the order / +HTC bit is set.</param>
        /// <param name="Body">The frame body.</param>
        /// <param name="FrameCheckSequence">An optional frame check sequence.</param>
        public IEEE80211ManagementFrame(FrameControl            FrameControl,
                                        UInt16                  DurationOrId,
                                        MACAddress              DestinationAddress,
                                        MACAddress              SourceAddress,
                                        MACAddress              BSSID,
                                        SequenceControl         SequenceControl,
                                        HTControl?              HTControl            = null,
                                        ReadOnlySequence<Byte>  Body                 = default,
                                        UInt32?                 FrameCheckSequence   = null)

            : base(FrameControl,
                   DurationOrId,
                   FrameCheckSequence)

        {

            if (FrameControl.Type != FrameTypes.Management)
                throw new ArgumentException($"'{FrameControl.Subtype}' is not a management frame subtype!",
                                            nameof(FrameControl));

            if (FrameControl.ToDS || FrameControl.FromDS)
                throw new ArgumentException("The ToDS and FromDS bits of a management frame must both be zero!",
                                            nameof(FrameControl));

            if (FrameControl.HasHTControl != HTControl.HasValue)
                throw new ArgumentException(FrameControl.HasHTControl
                                                ? "A frame whose order / +HTC bit is set requires an HT control field!"
                                                : "An HT control field requires the order / +HTC bit to be set!",
                                            nameof(HTControl));

            this.DestinationAddress  = DestinationAddress;
            this.SourceAddress       = SourceAddress;
            this.BSSID               = BSSID;
            this.SequenceControl     = SequenceControl;
            this.HTControl           = HTControl;
            this.Body                = Body;

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

            DestinationAddress.CopyTo(Destination[offset..]);
            offset += 6;

            SourceAddress.     CopyTo(Destination[offset..]);
            offset += 6;

            BSSID.             CopyTo(Destination[offset..]);
            offset += 6;

            SequenceControl.WriteTo(Destination[offset..]);
            offset += IEEE80211.SequenceControl.Length;

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

        #region (static) TryParse (Bytes, out ManagementFrame, IncludesFCS = false)

        /// <summary>
        /// Try to parse the given bytes as an IEEE 802.11 management frame.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame, starting with the frame control field.</param>
        /// <param name="ManagementFrame">The parsed management frame.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the frame check sequence.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>                                 Bytes,
                                       [NotNullWhen(true)] out IEEE80211ManagementFrame?  ManagementFrame,
                                       Boolean                                            IncludesFCS   = false)
        {

            ManagementFrame = null;

            if (!TryReadCommonHeader(Bytes,
                                     IncludesFCS,
                                     out var frameControl,
                                     out var durationOrId,
                                     out var frameCheckSequence,
                                     out var available))
            {
                return false;
            }

            if (frameControl.Type != FrameTypes.Management ||
                frameControl.ToDS || frameControl.FromDS)
            {
                return false;
            }

            if (available < ThreeAddressHeaderLength)
                return false;

            var offset               = (Int32) MinHeaderLength;

            var destinationAddress   = MACAddress.TryFrom(Bytes.Slice(offset, 6));  offset += 6;
            var sourceAddress        = MACAddress.TryFrom(Bytes.Slice(offset, 6));  offset += 6;
            var bssid                = MACAddress.TryFrom(Bytes.Slice(offset, 6));  offset += 6;

            if (!destinationAddress.HasValue || !sourceAddress.HasValue || !bssid.HasValue)
                return false;

            if (!IEEE80211.SequenceControl.TryParse(Bytes[offset..available], out var sequenceControl))
                return false;

            offset += IEEE80211.SequenceControl.Length;

            HTControl? htControl = null;

            if (frameControl.HasHTControl)
            {

                if (!IEEE80211.HTControl.TryParse(Bytes[offset..available], out var ht))
                    return false;

                htControl  = ht;
                offset    += IEEE80211.HTControl.Length;

            }

            ManagementFrame = new IEEE80211ManagementFrame(
                                  frameControl,
                                  durationOrId,
                                  destinationAddress.Value,
                                  sourceAddress.Value,
                                  bssid.Value,
                                  sequenceControl,
                                  htControl,
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

            => $"{FrameControl}: {SourceAddress} -> {DestinationAddress} [BSSID {BSSID}], {Body.Length} byte(s) body";

        #endregion

    }

}
