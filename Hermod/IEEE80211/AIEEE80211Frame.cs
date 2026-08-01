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
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Hermod.Ethernet;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.IEEE80211
{

    /// <summary>
    /// The common ground of every IEEE 802.11 MAC frame: a frame control field,
    /// a duration/ID field and an optional frame check sequence.
    ///
    /// Everything between those two ends differs so much between the three frame
    /// type families that they are modelled as three separate frame classes:
    ///
    /// <list type="bullet">
    ///   <item><see cref="IEEE80211DataFrame"/> - three or four addresses, sequence
    ///         control, optional QoS control, optional HT control and a frame body.</item>
    ///   <item><see cref="IEEE80211ManagementFrame"/> - always three addresses, sequence
    ///         control, optional HT control and a frame body.</item>
    ///   <item><see cref="IEEE80211ControlFrame"/> - one or two addresses, no sequence
    ///         control and no frame body. An acknowledgment is 14 bytes in total.</item>
    /// </list>
    ///
    /// The physical layer convergence header and any radiotap or PPI capture header
    /// are not part of this data structure.
    /// </summary>
    public abstract class AIEEE80211Frame
    {

        #region Data

        /// <summary>
        /// The length of the frame check sequence in bytes.
        /// </summary>
        public const UInt16  FCSLength                  = 4;

        /// <summary>
        /// The length of the frame control and duration/ID fields, which every
        /// IEEE 802.11 frame starts with.
        /// </summary>
        public const UInt16  MinHeaderLength            = FrameControl.Length + 2;

        /// <summary>
        /// The length of a data or management frame header without any of its
        /// optional fields: frame control, duration, three addresses and
        /// sequence control.
        /// </summary>
        public const UInt16  ThreeAddressHeaderLength   = 24;

        /// <summary>
        /// The residue of a CRC-32 computed over a frame that already includes a
        /// valid frame check sequence.
        /// </summary>
        public const UInt32  ValidFCSResidue            = EthernetFrame.ValidFCSResidue;

        #endregion

        #region Properties

        /// <summary>
        /// The frame control field, which decides the type of this frame
        /// as well as the presence of several of its later fields.
        /// </summary>
        public FrameControl   FrameControl        { get; }

        /// <summary>
        /// The duration/ID field: either a medium reservation in microseconds,
        /// or the association identifier of a PS-Poll frame.
        /// </summary>
        public UInt16         DurationOrId        { get; }

        /// <summary>
        /// The frame check sequence, if it was captured along with the frame.
        /// </summary>
        public UInt32?        FrameCheckSequence  { get; }


        /// <summary>
        /// The frame type.
        /// </summary>
        public FrameTypes     Type

            => FrameControl.Type;


        /// <summary>
        /// The frame subtype.
        /// </summary>
        public FrameSubtypes  Subtype

            => FrameControl.Subtype;


        /// <summary>
        /// Whether the duration/ID field carries a duration, which is the case
        /// whenever its most significant bit is cleared.
        /// </summary>
        public Boolean        IsDuration

            => (DurationOrId & 0x8000) == 0;


        /// <summary>
        /// The medium reservation in microseconds, or null if the duration/ID field
        /// does not carry a duration.
        /// </summary>
        public UInt16?        Duration

            => IsDuration
                   ? DurationOrId
                   : null;


        /// <summary>
        /// The association identifier of a PS-Poll frame, which is the meaning of the
        /// duration/ID field when its two most significant bits are both set.
        /// </summary>
        public UInt16?        AssociationId

            => (DurationOrId & 0xC000) == 0xC000
                   ? (UInt16) (DurationOrId & 0x3FFF)
                   : null;


        /// <summary>
        /// The length of the MAC header of this frame in bytes.
        /// </summary>
        public abstract UInt16 HeaderLength { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new IEEE 802.11 MAC frame.
        /// </summary>
        /// <param name="FrameControl">The frame control field.</param>
        /// <param name="DurationOrId">The duration/ID field.</param>
        /// <param name="FrameCheckSequence">An optional frame check sequence.</param>
        protected AIEEE80211Frame(FrameControl  FrameControl,
                                  UInt16        DurationOrId,
                                  UInt32?       FrameCheckSequence)
        {

            this.FrameControl        = FrameControl;
            this.DurationOrId        = DurationOrId;
            this.FrameCheckSequence  = FrameCheckSequence;

        }

        #endregion


        #region GetLength (IncludeFCS = false)

        /// <summary>
        /// Return the number of bytes that <see cref="TryWrite"/> will write.
        /// </summary>
        /// <param name="IncludeFCS">Whether a frame check sequence will be appended.</param>
        public abstract Int32 GetLength(Boolean IncludeFCS = false);

        #endregion

        #region TryWrite  (Destination, out BytesWritten, IncludeFCS = false)

        /// <summary>
        /// Write this frame in transmission order into the given destination span.
        /// </summary>
        /// <param name="Destination">The span to write the frame into.</param>
        /// <param name="BytesWritten">The number of bytes written into the destination span.</param>
        /// <param name="IncludeFCS">Whether to append a freshly computed frame check sequence.</param>
        public abstract Boolean TryWrite(Span<Byte>  Destination,
                                         out Int32   BytesWritten,
                                         Boolean     IncludeFCS   = false);

        #endregion

        #region GetBytes  (IncludeFCS = false)

        /// <summary>
        /// Return the bytes of this frame in transmission order.
        /// </summary>
        /// <param name="IncludeFCS">Whether to append a freshly computed frame check sequence.</param>
        public Byte[] GetBytes(Boolean IncludeFCS = false)
        {

            var bytes = new Byte[GetLength(IncludeFCS)];

            TryWrite(bytes, out _, IncludeFCS);

            return bytes;

        }

        #endregion


        #region (protected) WriteCommonHeader (Destination)

        /// <summary>
        /// Write the frame control and duration/ID fields, which every frame starts with,
        /// and return how many bytes were written.
        /// </summary>
        /// <param name="Destination">The span to write into.</param>
        protected Int32 WriteCommonHeader(Span<Byte> Destination)
        {

            FrameControl.WriteTo(Destination);

            // The duration/ID field is little-endian, unlike the frame control field!
            BinaryPrimitives.WriteUInt16LittleEndian(Destination[FrameControl.Length..], DurationOrId);

            return MinHeaderLength;

        }

        #endregion

        #region (protected, static) WriteFCS  (Destination, Offset)

        /// <summary>
        /// Compute the frame check sequence over the first Offset bytes of the given
        /// span and append it, least significant octet first.
        /// </summary>
        /// <param name="Destination">The span holding the frame.</param>
        /// <param name="Offset">The number of bytes written so far.</param>
        protected static void WriteFCS(Span<Byte>  Destination,
                                       Int32       Offset)

            => BinaryPrimitives.WriteUInt32LittleEndian(
                   Destination[Offset..],
                   ComputeFCS(Destination[..Offset])
               );

        #endregion


        #region (static) ComputeFCS (Bytes)

        /// <summary>
        /// Compute the IEEE 802.11 frame check sequence of the given bytes. It is the
        /// very same CRC-32 as the one of IEEE 802.3.
        /// </summary>
        /// <param name="Bytes">The bytes of a frame, up to but excluding the frame check sequence.</param>
        public static UInt32 ComputeFCS(ReadOnlySpan<Byte> Bytes)

            => EthernetFrame.ComputeFCS(Bytes);

        #endregion

        #region (static) VerifyFCS  (Bytes)

        /// <summary>
        /// Verify the frame check sequence of the given frame.
        /// </summary>
        /// <param name="Bytes">The bytes of a frame, including its frame check sequence.</param>
        public static Boolean VerifyFCS(ReadOnlySpan<Byte> Bytes)

            => EthernetFrame.VerifyFCS(Bytes);

        #endregion

        #region HasValidFCS

        /// <summary>
        /// Whether this frame carries a frame check sequence and it matches
        /// the freshly computed one.
        /// </summary>
        public Boolean HasValidFCS
        {
            get
            {

                if (!FrameCheckSequence.HasValue)
                    return false;

                var length = GetLength(IncludeFCS: false);
                var buffer = ArrayPool<Byte>.Shared.Rent(length);

                try
                {

                    TryWrite(buffer, out var bytesWritten, IncludeFCS: false);

                    return FrameCheckSequence.Value == ComputeFCS(buffer.AsSpan(0, bytesWritten));

                }
                finally
                {
                    ArrayPool<Byte>.Shared.Return(buffer);
                }

            }
        }

        #endregion


        #region (static) TryParse (Bytes, out Frame, IncludesFCS = false)

        /// <summary>
        /// Try to parse the given bytes as an IEEE 802.11 MAC frame, dispatching
        /// to the frame class of its type.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame, starting with the frame control field.</param>
        /// <param name="Frame">The parsed frame.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the frame check sequence.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>                        Bytes,
                                       [NotNullWhen(true)] out AIEEE80211Frame?  Frame,
                                       Boolean                                   IncludesFCS   = false)
        {

            Frame = null;

            if (!FrameControl.TryParse(Bytes, out var frameControl))
                return false;

            switch (frameControl.Type)
            {

                case FrameTypes.Data:
                    if (IEEE80211DataFrame.TryParse(Bytes, out var dataFrame, IncludesFCS))
                    {
                        Frame = dataFrame;
                        return true;
                    }
                    return false;

                case FrameTypes.Management:
                    if (IEEE80211ManagementFrame.TryParse(Bytes, out var managementFrame, IncludesFCS))
                    {
                        Frame = managementFrame;
                        return true;
                    }
                    return false;

                case FrameTypes.Control:
                    if (IEEE80211ControlFrame.TryParse(Bytes, out var controlFrame, IncludesFCS))
                    {
                        Frame = controlFrame;
                        return true;
                    }
                    return false;

                // IEEE 802.11ah and beyond, whose layout is not a variation of the
                // classic MAC header but a format of its own.
                default:
                    return false;

            }

        }


        /// <summary>
        /// Try to parse the given bytes as an IEEE 802.11 MAC frame, dispatching
        /// to the frame class of its type.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame, starting with the frame control field.</param>
        /// <param name="Frame">The parsed frame.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the frame check sequence.</param>
        public static Boolean TryParse(Byte[]?                                   Bytes,
                                       [NotNullWhen(true)] out AIEEE80211Frame?  Frame,
                                       Boolean                                   IncludesFCS   = false)
        {

            Frame = null;

            if (Bytes is null)
                return false;

            return TryParse(Bytes.AsSpan(), out Frame, IncludesFCS);

        }

        #endregion

        #region (protected, static) TryReadCommonHeader (Bytes, IncludesFCS, out FrameControl, out DurationOrId, out FCS, out Available)

        /// <summary>
        /// Read the frame control and duration/ID fields and split off the frame check
        /// sequence, which every frame class has to do first.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the frame check sequence.</param>
        /// <param name="FrameControl">The parsed frame control field.</param>
        /// <param name="DurationOrId">The parsed duration/ID field.</param>
        /// <param name="FrameCheckSequence">The frame check sequence, if any.</param>
        /// <param name="Available">The number of bytes of the frame without its frame check sequence.</param>
        protected static Boolean TryReadCommonHeader(ReadOnlySpan<Byte>  Bytes,
                                                     Boolean             IncludesFCS,
                                                     out FrameControl    FrameControl,
                                                     out UInt16          DurationOrId,
                                                     out UInt32?         FrameCheckSequence,
                                                     out Int32           Available)
        {

            FrameControl        = default;
            DurationOrId        = 0;
            FrameCheckSequence  = null;
            Available           = Bytes.Length;

            if (IncludesFCS)
            {

                if (Available < MinHeaderLength + FCSLength)
                    return false;

                // The frame check sequence is transmitted least significant octet first.
                FrameCheckSequence  = BinaryPrimitives.ReadUInt32LittleEndian(Bytes[^FCSLength..]);
                Available          -= FCSLength;

            }

            if (Available < MinHeaderLength)
                return false;

            if (!IEEE80211.FrameControl.TryParse(Bytes, out FrameControl))
                return false;

            // The duration/ID field is little-endian, unlike the frame control field!
            DurationOrId = BinaryPrimitives.ReadUInt16LittleEndian(Bytes[IEEE80211.FrameControl.Length..]);

            return true;

        }

        #endregion

    }

}
