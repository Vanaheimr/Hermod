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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Ethernet
{

    /// <summary>
    /// An Ethernet II / IEEE 802.3 MAC frame with an optional stack of
    /// IEEE 802.1Q / IEEE 802.1ad VLAN tags.
    ///
    /// <code>
    /// +-------------+-------------+-----------------+---------------+-----------+-------+
    /// | Destination |   Source    |   VLAN tags     | EtherType or  |  Payload  |  FCS  |
    /// |  (6 bytes)  |  (6 bytes)  | (0..n * 4 bytes)| Length (2 B)  |  (n bytes)| (4 B) |
    /// +-------------+-------------+-----------------+---------------+-----------+-------+
    /// </code>
    ///
    /// The preamble, the start frame delimiter and the interpacket gap belong to the
    /// physical layer and are therefore not part of this data structure.
    /// </summary>
    public class EthernetFrame
    {

        #region Data

        /// <summary>
        /// The length of an untagged Ethernet header
        /// (6 bytes destination + 6 bytes source + 2 bytes EtherType/Length).
        /// </summary>
        public const  UInt16  UntaggedHeaderLength  = 14;

        /// <summary>
        /// The length of the Frame Check Sequence in bytes.
        /// </summary>
        public const  UInt16  FCSLength             =  4;

        /// <summary>
        /// The minimum length of a frame on the wire, including the Frame Check
        /// Sequence, but excluding any VLAN tags (IEEE 802.3 minFrameSize).
        /// </summary>
        public const  UInt16  MinFrameLength        = 64;

        /// <summary>
        /// The minimum length of the MAC client data. Shorter payloads have to be
        /// padded, so that the resulting untagged frame reaches minFrameSize.
        /// </summary>
        public const  UInt16  MinPayloadLength      = MinFrameLength - UntaggedHeaderLength - FCSLength;  // 46

        /// <summary>
        /// The maximum length of the MAC client data of a standard frame (the MTU).
        /// Larger payloads are jumbo frames, which are not covered by IEEE 802.3.
        /// </summary>
        public const  UInt16  MaxPayloadLength      = 1500;

        /// <summary>
        /// The maximum number of stacked VLAN tags accepted while parsing.
        /// IEEE 802.1ad uses two (S-Tag + C-Tag); the third slot tolerates
        /// vendor stacks without opening a denial-of-service vector.
        /// </summary>
        public const  Byte    MaxVLANTagStackDepth  =  3;

        /// <summary>
        /// The residue of a CRC-32 computed over a frame that already includes a
        /// valid Frame Check Sequence.
        /// </summary>
        public const  UInt32  ValidFCSResidue       = 0x2144DF1C;


        /// <summary>
        /// The lookup table of the reflected IEEE 802.3 CRC-32 polynomial (0xEDB88320).
        /// </summary>
        private static readonly UInt32[] crc32Table = CreateCRC32Table();

        #endregion

        #region Properties

        /// <summary>
        /// The destination MAC address.
        /// </summary>
        public MACAddress              DestinationAddress    { get; }

        /// <summary>
        /// The source MAC address.
        /// </summary>
        public MACAddress              SourceAddress         { get; }

        /// <summary>
        /// The stack of VLAN tags, outermost tag first. Empty for untagged frames.
        /// </summary>
        public IReadOnlyList<VLANTag>  VLANTags              { get; }

        /// <summary>
        /// The EtherType (Ethernet II, &gt;= 1536) or the length of the MAC client
        /// data (IEEE 802.3, &lt;= 1500).
        /// </summary>
        public EtherType               EtherTypeOrLength     { get; }

        /// <summary>
        /// The MAC client data, without any padding. It may consist of several segments,
        /// e.g. a protocol header in front of a payload that was never copied.
        /// </summary>
        public ReadOnlySequence<Byte>  Payload               { get; }

        /// <summary>
        /// The Frame Check Sequence, if it was captured along with the frame.
        /// Most capture APIs strip it, therefore it is optional.
        /// </summary>
        public UInt32?                 FrameCheckSequence    { get; }


        /// <summary>
        /// Whether this frame carries at least one VLAN tag.
        /// </summary>
        public Boolean    IsVLANTagged

            => VLANTags.Count > 0;


        /// <summary>
        /// Whether this frame carries more than one VLAN tag (IEEE 802.1ad "QinQ").
        /// </summary>
        public Boolean    IsQinQ

            => VLANTags.Count > 1;


        /// <summary>
        /// The outermost VLAN tag, or null.
        /// </summary>
        public VLANTag?   OuterVLANTag

            => VLANTags.Count > 0
                   ? VLANTags[0]
                   : null;


        /// <summary>
        /// The innermost VLAN tag, or null.
        /// </summary>
        public VLANTag?   InnerVLANTag

            => VLANTags.Count > 0
                   ? VLANTags[^1]
                   : null;


        /// <summary>
        /// The VLAN identifier of the outermost VLAN tag, or null.
        /// </summary>
        public VLANId?    VLANId

            => VLANTags.Count > 0
                   ? VLANTags[0].VID
                   : null;


        /// <summary>
        /// Whether this is an Ethernet II frame, i.e. the EtherType/Length
        /// field carries a type.
        /// </summary>
        public Boolean    IsEthernetII

            => EtherTypeOrLength.IsEtherType;


        /// <summary>
        /// Whether this is an IEEE 802.3 frame, i.e. the EtherType/Length
        /// field carries a length and the payload starts with an LLC header.
        /// </summary>
        public Boolean    IsIEEE8023

            => EtherTypeOrLength.IsLength;


        /// <summary>
        /// The length of the MAC header in bytes, including all VLAN tags.
        /// </summary>
        public UInt16     HeaderLength

            => (UInt16) (UntaggedHeaderLength + VLANTag.Length * VLANTags.Count);


        /// <summary>
        /// The number of padding bytes required to reach the minimum frame size.
        /// </summary>
        public UInt16     PaddingLength

            => (UInt16) Math.Max(0L, MinPayloadLength - Payload.Length);


        /// <summary>
        /// The length of this frame on the wire, including padding and
        /// the Frame Check Sequence.
        /// </summary>
        public UInt32     FrameLength

            => (UInt32) (HeaderLength + Math.Max(Payload.Length, MinPayloadLength) + FCSLength);


        /// <summary>
        /// Whether the payload of this frame exceeds the standard MTU of 1500 bytes.
        /// </summary>
        public Boolean    IsJumboFrame

            => Payload.Length > MaxPayloadLength;


        /// <summary>
        /// Whether this frame is addressed to all stations.
        /// </summary>
        public Boolean    IsBroadcast

            => DestinationAddress.IsBroadcast;


        /// <summary>
        /// Whether this frame is addressed to a group of stations.
        /// </summary>
        public Boolean    IsMulticast

            => DestinationAddress.IsMulticast;


        /// <summary>
        /// Whether this frame is addressed to a single station.
        /// </summary>
        public Boolean    IsUnicast

            => DestinationAddress.IsUnicast;


        /// <summary>
        /// The protocol carried by the payload: the EtherType for Ethernet II frames,
        /// the SNAP protocol identifier for IEEE 802.3 LLC/SNAP frames, and null for
        /// plain LLC frames, which identify their protocol via service access points.
        /// </summary>
        public EtherType? PayloadProtocol
        {
            get
            {

                if (IsEthernetII)
                    return EtherTypeOrLength;

                if (TryGetSNAPHeader(out var snapHeader))
                    return snapHeader.ProtocolId;

                return null;

            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new Ethernet frame.
        /// </summary>
        /// <param name="DestinationAddress">The destination MAC address.</param>
        /// <param name="SourceAddress">The source MAC address.</param>
        /// <param name="EtherTypeOrLength">The EtherType, or the length of the MAC client data.</param>
        /// <param name="Payload">The MAC client data, without any padding.</param>
        /// <param name="VLANTags">An optional stack of VLAN tags, outermost tag first.</param>
        /// <param name="FrameCheckSequence">An optional Frame Check Sequence.</param>
        public EthernetFrame(MACAddress             DestinationAddress,
                             MACAddress             SourceAddress,
                             EtherType              EtherTypeOrLength,
                             ReadOnlyMemory<Byte>   Payload              = default,
                             IEnumerable<VLANTag>?  VLANTags             = null,
                             UInt32?                FrameCheckSequence   = null)

            // A caller-provided buffer might be mutated or returned to a pool later on,
            // therefore the public constructor always takes a copy of the payload!
            : this(DestinationAddress,
                   SourceAddress,
                   EtherTypeOrLength,
                   new ReadOnlySequence<Byte>(Payload.ToArray()),
                   VLANTags?.ToArray() ?? [],
                   FrameCheckSequence)

        { }


        /// <summary>
        /// Create a new Ethernet frame over the given sequence of payload segments.
        ///
        /// Unlike the other constructors this one does NOT copy the payload - flattening
        /// a sequence would defeat its very purpose. The frame therefore takes ownership
        /// of the given segments, which must neither be mutated nor returned to a pool
        /// while the frame is still in use.
        /// </summary>
        /// <param name="DestinationAddress">The destination MAC address.</param>
        /// <param name="SourceAddress">The source MAC address.</param>
        /// <param name="EtherTypeOrLength">The EtherType, or the length of the MAC client data.</param>
        /// <param name="Payload">The MAC client data, without any padding.</param>
        /// <param name="VLANTags">An optional stack of VLAN tags, outermost tag first.</param>
        /// <param name="FrameCheckSequence">An optional Frame Check Sequence.</param>
        public EthernetFrame(MACAddress              DestinationAddress,
                             MACAddress              SourceAddress,
                             EtherType               EtherTypeOrLength,
                             ReadOnlySequence<Byte>  Payload,
                             IEnumerable<VLANTag>?   VLANTags             = null,
                             UInt32?                 FrameCheckSequence   = null)

            : this(DestinationAddress,
                   SourceAddress,
                   EtherTypeOrLength,
                   Payload,
                   VLANTags?.ToArray() ?? [],
                   FrameCheckSequence)

        { }


        /// <summary>
        /// Create a new VLAN-tagged Ethernet frame.
        /// </summary>
        /// <param name="DestinationAddress">The destination MAC address.</param>
        /// <param name="SourceAddress">The source MAC address.</param>
        /// <param name="EtherTypeOrLength">The EtherType, or the length of the MAC client data.</param>
        /// <param name="VLANTag">A single VLAN tag.</param>
        /// <param name="Payload">The MAC client data, without any padding.</param>
        public EthernetFrame(MACAddress            DestinationAddress,
                             MACAddress            SourceAddress,
                             EtherType             EtherTypeOrLength,
                             VLANTag               VLANTag,
                             ReadOnlyMemory<Byte>  Payload   = default)

            : this(DestinationAddress,
                   SourceAddress,
                   EtherTypeOrLength,
                   new ReadOnlySequence<Byte>(Payload.ToArray()),
                   new [] { VLANTag },
                   null)

        { }


        /// <summary>
        /// Create a new Ethernet frame, adopting the given payload instead of copying it.
        /// This is the single point where the invariants of a frame are checked.
        /// </summary>
        /// <param name="DestinationAddress">The destination MAC address.</param>
        /// <param name="SourceAddress">The source MAC address.</param>
        /// <param name="EtherTypeOrLength">The EtherType, or the length of the MAC client data.</param>
        /// <param name="Payload">The MAC client data, without any padding. The frame takes ownership of it, so it must not be mutated afterwards.</param>
        /// <param name="VLANTags">The stack of VLAN tags, outermost tag first. The frame takes ownership of the array.</param>
        /// <param name="FrameCheckSequence">An optional Frame Check Sequence.</param>
        private EthernetFrame(MACAddress              DestinationAddress,
                              MACAddress              SourceAddress,
                              EtherType               EtherTypeOrLength,
                              ReadOnlySequence<Byte>  Payload,
                              VLANTag[]               VLANTags,
                              UInt32?                 FrameCheckSequence)
        {

            if (EtherTypeOrLength.IsUndefined)
                throw new ArgumentException($"The EtherType/Length field must not be within the undefined range of " +
                                            $"{EtherType.MaxLengthValue + 1}..{EtherType.MinEtherTypeValue - 1}!",
                                            nameof(EtherTypeOrLength));

            if (Payload.Length > Int32.MaxValue)
                throw new ArgumentException("The payload of an Ethernet frame must not exceed Int32.MaxValue bytes!",
                                            nameof(Payload));

            if (VLANTags.Length > MaxVLANTagStackDepth)
                throw new ArgumentException($"An Ethernet frame must not carry more than {MaxVLANTagStackDepth} stacked VLAN tags!",
                                            nameof(VLANTags));

            if (EtherTypeOrLength.IsLength && EtherTypeOrLength.Value != Payload.Length)
                throw new ArgumentException($"The IEEE 802.3 length field ({EtherTypeOrLength.Value}) does not match " +
                                            $"the length of the payload ({Payload.Length})!",
                                            nameof(EtherTypeOrLength));

            this.DestinationAddress  = DestinationAddress;
            this.SourceAddress       = SourceAddress;
            this.EtherTypeOrLength   = EtherTypeOrLength;
            this.Payload             = Payload;
            this.VLANTags            = VLANTags;
            this.FrameCheckSequence  = FrameCheckSequence;

        }

        #endregion

        #region (private) Adopt(DestinationAddress, SourceAddress, EtherTypeOrLength, Payload, VLANTags, FrameCheckSequence)

        /// <summary>
        /// Create a new Ethernet frame from a payload that is already owned by us,
        /// so that no further copy is needed.
        /// </summary>
        private static EthernetFrame Adopt(MACAddress              DestinationAddress,
                                           MACAddress              SourceAddress,
                                           EtherType               EtherTypeOrLength,
                                           ReadOnlySequence<Byte>  Payload,
                                           VLANTag[]               VLANTags,
                                           UInt32?                 FrameCheckSequence)

            => new (DestinationAddress,
                    SourceAddress,
                    EtherTypeOrLength,
                    Payload,
                    VLANTags,
                    FrameCheckSequence);

        #endregion


        #region CreateIEEE8023     (DestinationAddress, SourceAddress, LLCHeader, Payload, ...)

        /// <summary>
        /// Create a new IEEE 802.3 frame with an LLC header, computing the length field.
        /// </summary>
        /// <param name="DestinationAddress">The destination MAC address.</param>
        /// <param name="SourceAddress">The source MAC address.</param>
        /// <param name="LLCHeader">The IEEE 802.2 LLC header.</param>
        /// <param name="Payload">The LLC payload.</param>
        /// <param name="VLANTags">An optional stack of VLAN tags, outermost tag first.</param>
        public static EthernetFrame CreateIEEE8023(MACAddress             DestinationAddress,
                                                   MACAddress             SourceAddress,
                                                   LLCHeader               LLCHeader,
                                                   ReadOnlySequence<Byte>  Payload    = default,
                                                   IEnumerable<VLANTag>?   VLANTags   = null)
        {

            var llcBytes = new Byte[LLCHeader.Length];
            LLCHeader.WriteTo(llcBytes);

            // The LLC header becomes a segment of its own, the payload is not touched at all.
            var payload  = BufferSegment.Prepend(llcBytes, Payload);

            return Adopt(
                       DestinationAddress,
                       SourceAddress,
                       EtherType.FromLength((UInt16) payload.Length),
                       payload,
                       VLANTags?.ToArray() ?? [],
                       null
                   );

        }


        /// <summary>
        /// Create a new IEEE 802.3 frame with an LLC header, computing the length field.
        /// </summary>
        /// <param name="DestinationAddress">The destination MAC address.</param>
        /// <param name="SourceAddress">The source MAC address.</param>
        /// <param name="LLCHeader">The IEEE 802.2 LLC header.</param>
        /// <param name="Payload">The LLC payload.</param>
        /// <param name="VLANTags">An optional stack of VLAN tags, outermost tag first.</param>
        public static EthernetFrame CreateIEEE8023(MACAddress             DestinationAddress,
                                                   MACAddress             SourceAddress,
                                                   LLCHeader              LLCHeader,
                                                   ReadOnlyMemory<Byte>   Payload,
                                                   IEnumerable<VLANTag>?  VLANTags   = null)

            => CreateIEEE8023(
                   DestinationAddress,
                   SourceAddress,
                   LLCHeader,
                   new ReadOnlySequence<Byte>(Payload),
                   VLANTags
               );

        #endregion

        #region CreateIEEE8023SNAP (DestinationAddress, SourceAddress, SNAPHeader, Payload, ...)

        /// <summary>
        /// Create a new IEEE 802.3 frame with an LLC/SNAP header, computing the length field.
        /// </summary>
        /// <param name="DestinationAddress">The destination MAC address.</param>
        /// <param name="SourceAddress">The source MAC address.</param>
        /// <param name="SNAPHeader">The SubNetwork Access Protocol header.</param>
        /// <param name="Payload">The SNAP payload.</param>
        /// <param name="VLANTags">An optional stack of VLAN tags, outermost tag first.</param>
        public static EthernetFrame CreateIEEE8023SNAP(MACAddress             DestinationAddress,
                                                       MACAddress             SourceAddress,
                                                       SNAPHeader              SNAPHeader,
                                                       ReadOnlySequence<Byte>  Payload    = default,
                                                       IEnumerable<VLANTag>?   VLANTags   = null)
        {

            var snapBytes = new Byte[Ethernet.SNAPHeader.Length];
            SNAPHeader.WriteTo(snapBytes);

            // LLC header, SNAP header and payload end up as three separate
            // segments - none of them is ever copied.
            return CreateIEEE8023(
                       DestinationAddress,
                       SourceAddress,
                       LLCHeader.SNAP,
                       BufferSegment.Prepend(snapBytes, Payload),
                       VLANTags
                   );

        }


        /// <summary>
        /// Create a new IEEE 802.3 frame with an LLC/SNAP header, computing the length field.
        /// </summary>
        /// <param name="DestinationAddress">The destination MAC address.</param>
        /// <param name="SourceAddress">The source MAC address.</param>
        /// <param name="SNAPHeader">The SubNetwork Access Protocol header.</param>
        /// <param name="Payload">The SNAP payload.</param>
        /// <param name="VLANTags">An optional stack of VLAN tags, outermost tag first.</param>
        public static EthernetFrame CreateIEEE8023SNAP(MACAddress             DestinationAddress,
                                                       MACAddress             SourceAddress,
                                                       SNAPHeader             SNAPHeader,
                                                       ReadOnlyMemory<Byte>   Payload,
                                                       IEnumerable<VLANTag>?  VLANTags   = null)

            => CreateIEEE8023SNAP(
                   DestinationAddress,
                   SourceAddress,
                   SNAPHeader,
                   new ReadOnlySequence<Byte>(Payload),
                   VLANTags
               );

        #endregion


        #region TryParse (Bytes, out EthernetFrame, IncludesFCS = false)

        /// <summary>
        /// Try to parse the given bytes as an Ethernet frame.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame, starting with the destination MAC address.</param>
        /// <param name="EthernetFrame">The parsed Ethernet frame.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the Frame Check Sequence. Most capture APIs strip it.</param>
        public static Boolean TryParse(Byte[]?                                 Bytes,
                                       [NotNullWhen(true)] out EthernetFrame?  EthernetFrame,
                                       Boolean                                 IncludesFCS   = false)
        {

            EthernetFrame = null;

            if (Bytes is null)
                return false;

            return TryParse(Bytes.AsSpan(), out EthernetFrame, IncludesFCS);

        }


        /// <summary>
        /// Try to parse the given bytes as an Ethernet frame.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame, starting with the destination MAC address.</param>
        /// <param name="EthernetFrame">The parsed Ethernet frame.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the Frame Check Sequence. Most capture APIs strip it.</param>
        /// <param name="CopyPayload">Whether to copy the payload out of the given memory. When false the frame merely
        /// slices it, which avoids the copy entirely, but ties the lifetime of the frame to the given buffer - do not
        /// use it for pooled or otherwise reused buffers!</param>
        public static Boolean TryParse(ReadOnlyMemory<Byte>                    Bytes,
                                       [NotNullWhen(true)] out EthernetFrame?  EthernetFrame,
                                       Boolean                                 IncludesFCS   = false,
                                       Boolean                                 CopyPayload   = true)
        {

            EthernetFrame = null;

            if (!TryParseHeader(Bytes.Span,
                                IncludesFCS,
                                out var header))
            {
                return false;
            }

            var payload = Bytes.Slice(header.PayloadOffset, header.PayloadLength);

            EthernetFrame = Adopt(
                                header.DestinationAddress,
                                header.SourceAddress,
                                header.EtherTypeOrLength,
                                new ReadOnlySequence<Byte>(
                                    CopyPayload
                                        ? payload.ToArray()
                                        : payload
                                ),
                                header.VLANTags,
                                header.FrameCheckSequence
                            );

            return true;

        }


        /// <summary>
        /// Try to parse the given bytes as an Ethernet frame. A span cannot outlive this
        /// call, therefore the payload is always copied.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame, starting with the destination MAC address.</param>
        /// <param name="EthernetFrame">The parsed Ethernet frame.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the Frame Check Sequence. Most capture APIs strip it.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>                      Bytes,
                                       [NotNullWhen(true)] out EthernetFrame?  EthernetFrame,
                                       Boolean                                 IncludesFCS   = false)
        {

            EthernetFrame = null;

            if (!TryParseHeader(Bytes,
                                IncludesFCS,
                                out var header))
            {
                return false;
            }

            EthernetFrame = Adopt(
                                header.DestinationAddress,
                                header.SourceAddress,
                                header.EtherTypeOrLength,
                                new ReadOnlySequence<Byte>(
                                    Bytes.Slice(header.PayloadOffset, header.PayloadLength).ToArray()
                                ),
                                header.VLANTags,
                                header.FrameCheckSequence
                            );

            return true;

        }

        #endregion

        #region (private) TryParseHeader (Bytes, IncludesFCS, out Header)

        /// <summary>
        /// Everything a parsed frame consists of, except for the payload bytes themselves,
        /// which are only located within the given buffer.
        /// </summary>
        private readonly record struct ParsedHeader(MACAddress  DestinationAddress,
                                                    MACAddress  SourceAddress,
                                                    EtherType   EtherTypeOrLength,
                                                    VLANTag[]   VLANTags,
                                                    UInt32?     FrameCheckSequence,
                                                    Int32       PayloadOffset,
                                                    Int32       PayloadLength);

        /// <summary>
        /// Parse everything but the payload, and locate the payload within the given bytes.
        /// This is the one and only place where the wire format is decoded; the public
        /// overloads only differ in how they get hold of the payload afterwards.
        /// </summary>
        /// <param name="Bytes">The bytes of the frame, starting with the destination MAC address.</param>
        /// <param name="IncludesFCS">Whether the last 4 bytes are the Frame Check Sequence.</param>
        /// <param name="Header">The parsed header.</param>
        private static Boolean TryParseHeader(ReadOnlySpan<Byte>  Bytes,
                                              Boolean             IncludesFCS,
                                              out ParsedHeader    Header)
        {

            Header = default;

            var     available           = Bytes.Length;
            UInt32? frameCheckSequence  = null;

            if (IncludesFCS)
            {

                if (available < UntaggedHeaderLength + FCSLength)
                    return false;

                // The Frame Check Sequence is transmitted least significant octet first!
                frameCheckSequence  = BinaryPrimitives.ReadUInt32LittleEndian(Bytes[^FCSLength..]);
                available          -= FCSLength;

            }

            if (available < UntaggedHeaderLength)
                return false;

            var destinationAddress  = MACAddress.TryFrom(Bytes[0.. 6]);
            var sourceAddress       = MACAddress.TryFrom(Bytes[6..12]);

            if (!destinationAddress.HasValue || !sourceAddress.HasValue)
                return false;

            var offset              = 12;

            // The tag stack is bounded, so it lives on the stack while parsing.
            Span<VLANTag> vlanTags  = stackalloc VLANTag[MaxVLANTagStackDepth];
            var           tagCount  = 0;

            while (offset + VLANTag.Length <= available &&
                   VLANTag.TryParse(Bytes[offset..available], out var vlanTag))
            {

                if (tagCount == MaxVLANTagStackDepth)
                    return false;

                vlanTags[tagCount++]  = vlanTag;
                offset               += VLANTag.Length;

            }

            if (offset + EtherType.Length > available)
                return false;

            var etherTypeOrLength   = EtherType.From(Bytes.Slice(offset, EtherType.Length));
            offset                 += EtherType.Length;

            if (etherTypeOrLength.IsUndefined)
                return false;

            var payloadLength       = available - offset;

            // An IEEE 802.3 length field delimits the MAC client data exactly,
            // everything beyond it is padding. For an Ethernet II frame the
            // padding is indistinguishable from the payload and therefore
            // remains part of it.
            if (etherTypeOrLength.IsLength)
            {

                if (etherTypeOrLength.Value > payloadLength)
                    return false;

                payloadLength = etherTypeOrLength.Value;

            }

            Header = new ParsedHeader(
                         destinationAddress.Value,
                         sourceAddress.Value,
                         etherTypeOrLength,
                         vlanTags[..tagCount].ToArray(),
                         frameCheckSequence,
                         offset,
                         payloadLength
                     );

            return true;

        }

        #endregion


        #region GetLength (IncludeFCS = false, AddPadding = true)

        /// <summary>
        /// Return the number of bytes that <see cref="TryWrite"/> will write.
        /// </summary>
        /// <param name="IncludeFCS">Whether a Frame Check Sequence will be appended.</param>
        /// <param name="AddPadding">Whether short payloads will be padded to the minimum frame size.</param>
        public Int32 GetLength(Boolean  IncludeFCS   = false,
                               Boolean  AddPadding   = true)

            => HeaderLength +

               (Int32) (AddPadding
                            ? Math.Max(Payload.Length, MinPayloadLength)
                            : Payload.Length) +

               (IncludeFCS
                    ? FCSLength
                    : 0);

        #endregion

        #region TryWrite  (Destination, out BytesWritten, IncludeFCS = false, AddPadding = true)

        /// <summary>
        /// Write this Ethernet frame in network byte order into the given destination span.
        /// This is the primitive that <see cref="GetBytes"/> is built upon, so that callers
        /// owning a buffer - a NIC ring, a pooled array, a stack buffer - never have to
        /// allocate at all.
        /// </summary>
        /// <param name="Destination">The span to write the frame into.</param>
        /// <param name="BytesWritten">The number of bytes written into the destination span.</param>
        /// <param name="IncludeFCS">Whether to append a freshly computed Frame Check Sequence.</param>
        /// <param name="AddPadding">Whether to pad short payloads to the minimum frame size.</param>
        public Boolean TryWrite(Span<Byte>  Destination,
                                out Int32   BytesWritten,
                                Boolean     IncludeFCS   = false,
                                Boolean     AddPadding   = true)
        {

            BytesWritten = 0;

            var length = GetLength(IncludeFCS, AddPadding);

            if (Destination.Length < length)
                return false;

            Destination = Destination[..length];

            var offset  = 0;

            DestinationAddress.CopyTo(Destination[offset..]);
            offset += 6;

            SourceAddress.     CopyTo(Destination[offset..]);
            offset += 6;

            foreach (var vlanTag in VLANTags)
            {
                vlanTag.WriteTo(Destination[offset..]);
                offset += VLANTag.Length;
            }

            EtherTypeOrLength.WriteTo(Destination[offset..]);
            offset += EtherType.Length;

            Payload.CopyTo(Destination[offset..]);
            offset += (Int32) Payload.Length;

            // The destination may be a reused buffer, so the padding has to be zeroed explicitly.
            var paddingEnd = length - (IncludeFCS ? FCSLength : 0);

            if (offset < paddingEnd)
                Destination[offset..paddingEnd].Clear();

            if (IncludeFCS)
                BinaryPrimitives.WriteUInt32LittleEndian(
                    Destination[paddingEnd..],
                    ComputeFCS(Destination[..paddingEnd])
                );

            BytesWritten = length;
            return true;

        }

        #endregion

        #region GetBytes  (IncludeFCS = false, AddPadding = true)

        /// <summary>
        /// Return the bytes of this Ethernet frame in network byte order.
        /// </summary>
        /// <param name="IncludeFCS">Whether to append a freshly computed Frame Check Sequence.</param>
        /// <param name="AddPadding">Whether to pad short payloads to the minimum frame size.</param>
        public Byte[] GetBytes(Boolean  IncludeFCS   = false,
                               Boolean  AddPadding   = true)
        {

            var bytes = new Byte[GetLength(IncludeFCS, AddPadding)];

            TryWrite(bytes, out _, IncludeFCS, AddPadding);

            return bytes;

        }

        #endregion


        #region PushVLANTag    (VLANTag)

        /// <summary>
        /// Return a copy of this frame with the given VLAN tag added as the outermost tag.
        /// </summary>
        /// <param name="VLANTag">The VLAN tag to add.</param>
        public EthernetFrame PushVLANTag(VLANTag VLANTag)

            => new (DestinationAddress,
                    SourceAddress,
                    EtherTypeOrLength,
                    Payload,
                    [ VLANTag, .. VLANTags ],
                    FrameCheckSequence);

        #endregion

        #region PopVLANTag     ()

        /// <summary>
        /// Return a copy of this frame with the outermost VLAN tag removed.
        /// </summary>
        public EthernetFrame PopVLANTag()

            => VLANTags.Count == 0

                   ? this

                   : new EthernetFrame(
                         DestinationAddress,
                         SourceAddress,
                         EtherTypeOrLength,
                         Payload,
                         [.. VLANTags.Skip(1)],
                         FrameCheckSequence
                     );

        #endregion

        #region WithoutVLANTags()

        /// <summary>
        /// Return a copy of this frame with all VLAN tags removed.
        /// </summary>
        public EthernetFrame WithoutVLANTags()

            => VLANTags.Count == 0

                   ? this

                   : new EthernetFrame(
                         DestinationAddress,
                         SourceAddress,
                         EtherTypeOrLength,
                         Payload,
                         [],
                         FrameCheckSequence
                     );

        #endregion


        #region (private) CopyPayloadPrefix (Destination)

        /// <summary>
        /// The largest prefix of the MAC client data that the LLC and SNAP accessors
        /// have to look at: a 2-byte control field LLC header plus a SNAP header.
        /// </summary>
        private const Byte MaxLLCSNAPHeaderLength = 4 + SNAPHeader.Length;

        /// <summary>
        /// Copy the first bytes of the payload into the given destination span and return
        /// how many bytes were copied. A segmented payload has no contiguous prefix of its
        /// own, so the few header bytes have to be gathered into one buffer first.
        /// </summary>
        /// <param name="Destination">A span to gather the prefix into.</param>
        private Int32 CopyPayloadPrefix(Span<Byte> Destination)
        {

            var length = (Int32) Math.Min(Destination.Length, Payload.Length);

            Payload.Slice(0, length).CopyTo(Destination);

            return length;

        }

        #endregion

        #region TryGetLLCHeader  (out LLCHeader)

        /// <summary>
        /// Try to read the IEEE 802.2 LLC header from the payload of this frame.
        /// Only IEEE 802.3 frames carry one.
        /// </summary>
        /// <param name="LLCHeader">The parsed LLC header.</param>
        public Boolean TryGetLLCHeader(out LLCHeader LLCHeader)
        {

            LLCHeader = default;

            if (!IsIEEE8023)
                return false;

            Span<Byte> prefix = stackalloc Byte[MaxLLCSNAPHeaderLength];
            var        length = CopyPayloadPrefix(prefix);

            return Ethernet.LLCHeader.TryParse(prefix[..length], out LLCHeader);

        }

        #endregion

        #region TryGetSNAPHeader (out SNAPHeader)

        /// <summary>
        /// Try to read the SubNetwork Access Protocol header from the payload of this frame.
        /// Only IEEE 802.3 frames with an LLC/SNAP header carry one.
        /// </summary>
        /// <param name="SNAPHeader">The parsed SNAP header.</param>
        public Boolean TryGetSNAPHeader(out SNAPHeader SNAPHeader)
        {

            SNAPHeader = default;

            if (!TryGetLLCHeader(out var llcHeader) || !llcHeader.IsSNAP)
                return false;

            Span<Byte> prefix = stackalloc Byte[MaxLLCSNAPHeaderLength];
            var        length = CopyPayloadPrefix(prefix);

            return length >= llcHeader.Length + Ethernet.SNAPHeader.Length &&
                   Ethernet.SNAPHeader.TryParse(prefix[llcHeader.Length..length], out SNAPHeader);

        }

        #endregion

        #region GetLLCPayload    ()

        /// <summary>
        /// Return the payload beyond the LLC and SNAP headers of an IEEE 802.3 frame,
        /// or the entire payload of an Ethernet II frame. This is a slice of the payload,
        /// not a copy of it.
        /// </summary>
        public ReadOnlySequence<Byte> GetLLCPayload()
        {

            if (!TryGetLLCHeader(out var llcHeader))
                return Payload;

            Int64 offset = llcHeader.Length;

            if (llcHeader.IsSNAP)
                offset += SNAPHeader.Length;

            return offset <= Payload.Length
                       ? Payload.Slice(offset)
                       : ReadOnlySequence<Byte>.Empty;

        }

        #endregion


        #region (static) ComputeFCS (Bytes)

        /// <summary>
        /// Compute the IEEE 802.3 Frame Check Sequence (CRC-32) of the given bytes.
        /// </summary>
        /// <param name="Bytes">The bytes of a frame, from the destination MAC address up to, but excluding, the Frame Check Sequence.</param>
        public static UInt32 ComputeFCS(ReadOnlySpan<Byte> Bytes)
        {

            var crc = 0xFFFFFFFFu;

            foreach (var b in Bytes)
                crc = crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);

            return ~crc;

        }

        #endregion

        #region (static) VerifyFCS  (Bytes)

        /// <summary>
        /// Verify the Frame Check Sequence of the given frame by recomputing the
        /// CRC-32 over the frame including its Frame Check Sequence, which yields
        /// a constant residue for every intact frame.
        /// </summary>
        /// <param name="Bytes">The bytes of a frame, including its Frame Check Sequence.</param>
        public static Boolean VerifyFCS(ReadOnlySpan<Byte> Bytes)

            => Bytes.Length > FCSLength &&
               ComputeFCS(Bytes) == ValidFCSResidue;

        #endregion

        #region HasValidFCS

        /// <summary>
        /// Whether this frame carries a Frame Check Sequence and it matches the
        /// freshly computed one. Note that the padding of the captured frame is
        /// assumed to have been all zeroes.
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

        #region (private, static) CreateCRC32Table()

        /// <summary>
        /// Create the lookup table of the reflected IEEE 802.3 CRC-32 polynomial.
        /// </summary>
        private static UInt32[] CreateCRC32Table()
        {

            var table = new UInt32[256];

            for (var i = 0U; i < 256; i++)
            {

                var value = i;

                for (var bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0
                                ? 0xEDB88320u ^ (value >> 1)
                                : value >> 1;

                table[i] = value;

            }

            return table;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"{SourceAddress} -> {DestinationAddress}",

                   VLANTags.Count > 0
                       ? $", VLAN {String.Join(".", VLANTags.Select(vlanTag => vlanTag.VID.Value))}"
                       : "",

                   IsEthernetII
                       ? $", {EtherTypeOrLength:F}"
                       : $", IEEE 802.3 length {EtherTypeOrLength.Value}",

                   $", {Payload.Length} byte(s) payload"

               );

        #endregion

    }

}
