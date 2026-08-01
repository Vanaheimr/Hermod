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

namespace org.GraphDefined.Vanaheimr.Hermod.Ethernet
{

    /// <summary>
    /// The IEEE 802.1Q Priority Code Point (PCP), formerly IEEE 802.1p, and its
    /// recommended traffic types (IEEE 802.1Q-2014, table I-2).
    ///
    /// Note that the numeric order is NOT the priority order: background traffic (1)
    /// has a *lower* priority than best effort (0).
    /// </summary>
    public enum PCPPriorities : Byte
    {

        /// <summary>
        /// Best Effort (BE) - the default traffic class.
        /// </summary>
        BestEffort            = 0,

        /// <summary>
        /// Background (BK) - the lowest priority.
        /// </summary>
        Background            = 1,

        /// <summary>
        /// Excellent Effort (EE).
        /// </summary>
        ExcellentEffort       = 2,

        /// <summary>
        /// Critical Applications (CA).
        /// </summary>
        CriticalApplications  = 3,

        /// <summary>
        /// Video (VI), &lt; 100 ms latency and jitter.
        /// </summary>
        Video                 = 4,

        /// <summary>
        /// Voice (VO), &lt; 10 ms latency and jitter.
        /// </summary>
        Voice                 = 5,

        /// <summary>
        /// Internetwork Control (IC).
        /// </summary>
        InternetworkControl   = 6,

        /// <summary>
        /// Network Control (NC) - the highest priority.
        /// </summary>
        NetworkControl        = 7

    }


    /// <summary>
    /// An IEEE 802.1Q VLAN tag: a 2-byte Tag Protocol Identifier (TPID) followed by
    /// a 2-byte Tag Control Information (TCI) field consisting of a 3-bit Priority
    /// Code Point (PCP), a 1-bit Drop Eligible Indicator (DEI, formerly CFI) and a
    /// 12-bit VLAN identifier (VID).
    ///
    /// <code>
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |     Tag Protocol Identifier   |PCP  |D|      VLAN Identifier  |
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// </code>
    /// </summary>
    public readonly struct VLANTag : IEquatable<VLANTag>,
                                     IComparable<VLANTag>,
                                     IComparable
    {

        #region Data

        /// <summary>
        /// The number of bytes of a VLAN tag (2 bytes TPID + 2 bytes TCI).
        /// </summary>
        public const Byte Length = 4;

        /// <summary>
        /// The Tag Protocol Identifiers that are recognized as introducing a VLAN tag:
        /// IEEE 802.1Q (0x8100), IEEE 802.1ad (0x88A8) and the legacy vendor QinQ
        /// values 0x9100, 0x9200 and 0x9300.
        /// </summary>
        public static IReadOnlyList<EtherType> KnownTPIDs { get; }

            = [
                  EtherType.From(0x8100),
                  EtherType.From(0x88A8),
                  EtherType.From(0x9100),
                  EtherType.From(0x9200),
                  EtherType.From(0x9300)
              ];

        #endregion

        #region Properties

        /// <summary>
        /// The Tag Protocol Identifier, e.g. 0x8100 for an IEEE 802.1Q customer tag.
        /// </summary>
        public EtherType      TPID           { get; }

        /// <summary>
        /// The 3-bit Priority Code Point (IEEE 802.1p), 0..7.
        /// </summary>
        public Byte           PCP            { get; }

        /// <summary>
        /// The Drop Eligible Indicator (IEEE 802.1Q-2011), formerly the
        /// Canonical Format Indicator (CFI).
        /// </summary>
        public Boolean        DEI            { get; }

        /// <summary>
        /// The 12-bit VLAN identifier.
        /// </summary>
        public VLANId         VID            { get; }


        /// <summary>
        /// The Priority Code Point as its recommended traffic type.
        /// </summary>
        public PCPPriorities  Priority

            => (PCPPriorities) PCP;


        /// <summary>
        /// The 16-bit Tag Control Information field (PCP, DEI and VID).
        /// </summary>
        public UInt16         TCI

            => (UInt16) ((PCP << 13)         |
                         ((DEI ? 1 : 0) << 12) |
                         VID.Value);


        /// <summary>
        /// Whether this is an IEEE 802.1Q customer VLAN tag / C-Tag (TPID 0x8100).
        /// </summary>
        public Boolean        IsCustomerTag

            => TPID.Value == 0x8100;


        /// <summary>
        /// Whether this is an IEEE 802.1ad service VLAN tag / S-Tag (TPID 0x88A8),
        /// or one of the legacy vendor QinQ tags (0x9100, 0x9200, 0x9300).
        /// </summary>
        public Boolean        IsServiceTag

            => TPID.Value is 0x88A8 or 0x9100 or 0x9200 or 0x9300;


        /// <summary>
        /// Whether this tag is priority-tagged only, i.e. it carries a priority
        /// but does not assign the frame to a VLAN (VID == 0).
        /// </summary>
        public Boolean        IsPriorityTagOnly

            => VID.IsNullVLAN;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new IEEE 802.1Q VLAN tag.
        /// </summary>
        /// <param name="VID">The 12-bit VLAN identifier.</param>
        /// <param name="PCP">The 3-bit Priority Code Point, 0..7. Default is 0 (best effort).</param>
        /// <param name="DEI">The Drop Eligible Indicator. Default is false.</param>
        /// <param name="TPID">The Tag Protocol Identifier. Default is 0x8100 (IEEE 802.1Q customer tag).</param>
        public VLANTag(VLANId      VID,
                       Byte        PCP    = 0,
                       Boolean     DEI    = false,
                       EtherType?  TPID   = null)
        {

            if (PCP > 7)
                throw new ArgumentOutOfRangeException(nameof(PCP),
                                                      "The Priority Code Point must be in the range of 0..7!");

            var tpid = TPID ?? EtherType.VLAN;

            if (!tpid.IsVLANTagProtocolIdentifier)
                throw new ArgumentException($"'{tpid:F}' is not a known VLAN Tag Protocol Identifier!",
                                            nameof(TPID));

            this.VID   = VID;
            this.PCP   = PCP;
            this.DEI   = DEI;
            this.TPID  = tpid;

        }


        /// <summary>
        /// Create a new IEEE 802.1Q VLAN tag.
        /// </summary>
        /// <param name="VID">The 12-bit VLAN identifier.</param>
        /// <param name="Priority">The Priority Code Point.</param>
        /// <param name="DEI">The Drop Eligible Indicator. Default is false.</param>
        /// <param name="TPID">The Tag Protocol Identifier. Default is 0x8100 (IEEE 802.1Q customer tag).</param>
        public VLANTag(VLANId         VID,
                       PCPPriorities  Priority,
                       Boolean        DEI    = false,
                       EtherType?     TPID   = null)

            : this(VID,
                   (Byte) Priority,
                   DEI,
                   TPID)

        { }

        #endregion


        #region CustomerTag (VID, PCP = 0, DEI = false)

        /// <summary>
        /// Create a new IEEE 802.1Q customer VLAN tag / C-Tag (TPID 0x8100).
        /// </summary>
        /// <param name="VID">The 12-bit VLAN identifier.</param>
        /// <param name="PCP">The 3-bit Priority Code Point, 0..7.</param>
        /// <param name="DEI">The Drop Eligible Indicator.</param>
        public static VLANTag CustomerTag(UInt16   VID,
                                          Byte     PCP   = 0,
                                          Boolean  DEI   = false)

            => new (VLANId.From(VID),
                    PCP,
                    DEI,
                    EtherType.VLAN);

        #endregion

        #region ServiceTag  (VID, PCP = 0, DEI = false)

        /// <summary>
        /// Create a new IEEE 802.1ad service VLAN tag / S-Tag (TPID 0x88A8).
        /// </summary>
        /// <param name="VID">The 12-bit VLAN identifier.</param>
        /// <param name="PCP">The 3-bit Priority Code Point, 0..7.</param>
        /// <param name="DEI">The Drop Eligible Indicator.</param>
        public static VLANTag ServiceTag(UInt16   VID,
                                         Byte     PCP   = 0,
                                         Boolean  DEI   = false)

            => new (VLANId.From(VID),
                    PCP,
                    DEI,
                    EtherType.ProviderBridging);

        #endregion


        #region From     (Bytes)

        /// <summary>
        /// Create a new VLAN tag from the given 4 bytes in network byte order.
        /// </summary>
        /// <param name="Bytes">A 4-byte span containing TPID and TCI.</param>
        public static VLANTag From(ReadOnlySpan<Byte> Bytes)

            => TryParse(Bytes, out var vlanTag)
                   ? vlanTag
                   : throw new ArgumentException($"The given bytes are not a valid VLAN tag!", nameof(Bytes));

        #endregion

        #region TryParse (Bytes, out VLANTag)

        /// <summary>
        /// Try to read a VLAN tag from the beginning of the given bytes.
        /// </summary>
        /// <param name="Bytes">A span of at least 4 bytes starting with the Tag Protocol Identifier.</param>
        /// <param name="VLANTag">The parsed VLAN tag.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>  Bytes,
                                       out VLANTag         VLANTag)
        {

            VLANTag = default;

            if (Bytes.Length < Length)
                return false;

            var tpid = EtherType.From(BinaryPrimitives.ReadUInt16BigEndian(Bytes));

            if (!tpid.IsVLANTagProtocolIdentifier)
                return false;

            var tci  = BinaryPrimitives.ReadUInt16BigEndian(Bytes[2..]);

            VLANTag  = new VLANTag(
                           VLANId.From((UInt16) (tci & 0x0FFF)),
                           (Byte)              (tci >> 13),
                                              ((tci & 0x1000) != 0),
                           tpid
                       );

            return true;

        }

        #endregion


        #region GetBytes()

        /// <summary>
        /// Return the 4 bytes of this VLAN tag in network byte order.
        /// </summary>
        public Byte[] GetBytes()
        {

            var bytes = new Byte[Length];
            WriteTo(bytes);

            return bytes;

        }

        #endregion

        #region WriteTo(Destination)

        /// <summary>
        /// Write the 4 bytes of this VLAN tag in network byte order
        /// into the given destination span.
        /// </summary>
        /// <param name="Destination">A span to write the bytes of this VLAN tag into.</param>
        public void WriteTo(Span<Byte> Destination)
        {

            if (Destination.Length < Length)
                throw new ArgumentException("Destination span too small.", nameof(Destination));

            BinaryPrimitives.WriteUInt16BigEndian(Destination,       TPID.Value);
            BinaryPrimitives.WriteUInt16BigEndian(Destination[2..],  TCI);

        }

        #endregion


        #region Operator overloading

        #region Operator == (VLANTag1, VLANTag2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANTag1">A VLAN tag.</param>
        /// <param name="VLANTag2">Another VLAN tag.</param>
        public static Boolean operator == (VLANTag VLANTag1,
                                           VLANTag VLANTag2)

            => VLANTag1.Equals(VLANTag2);

        #endregion

        #region Operator != (VLANTag1, VLANTag2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANTag1">A VLAN tag.</param>
        /// <param name="VLANTag2">Another VLAN tag.</param>
        public static Boolean operator != (VLANTag VLANTag1,
                                           VLANTag VLANTag2)

            => !VLANTag1.Equals(VLANTag2);

        #endregion

        #region Operator <  (VLANTag1, VLANTag2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANTag1">A VLAN tag.</param>
        /// <param name="VLANTag2">Another VLAN tag.</param>
        public static Boolean operator < (VLANTag VLANTag1,
                                          VLANTag VLANTag2)

            => VLANTag1.CompareTo(VLANTag2) < 0;

        #endregion

        #region Operator <= (VLANTag1, VLANTag2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANTag1">A VLAN tag.</param>
        /// <param name="VLANTag2">Another VLAN tag.</param>
        public static Boolean operator <= (VLANTag VLANTag1,
                                           VLANTag VLANTag2)

            => VLANTag1.CompareTo(VLANTag2) <= 0;

        #endregion

        #region Operator >  (VLANTag1, VLANTag2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANTag1">A VLAN tag.</param>
        /// <param name="VLANTag2">Another VLAN tag.</param>
        public static Boolean operator > (VLANTag VLANTag1,
                                          VLANTag VLANTag2)

            => VLANTag1.CompareTo(VLANTag2) > 0;

        #endregion

        #region Operator >= (VLANTag1, VLANTag2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANTag1">A VLAN tag.</param>
        /// <param name="VLANTag2">Another VLAN tag.</param>
        public static Boolean operator >= (VLANTag VLANTag1,
                                           VLANTag VLANTag2)

            => VLANTag1.CompareTo(VLANTag2) >= 0;

        #endregion

        #endregion

        #region IComparable<VLANTag> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two VLAN tags.
        /// </summary>
        /// <param name="Object">A VLAN tag to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is VLANTag vlanTag
                   ? CompareTo(vlanTag)
                   : throw new ArgumentException("The given object is not a VLAN tag!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(VLANTag)

        /// <summary>
        /// Compares two VLAN tags, first by their Tag Protocol Identifier,
        /// then by their Tag Control Information.
        /// </summary>
        /// <param name="VLANTag">A VLAN tag to compare with.</param>
        public Int32 CompareTo(VLANTag VLANTag)

            => TPID != VLANTag.TPID
                   ? TPID.CompareTo(VLANTag.TPID)
                   : TCI. CompareTo(VLANTag.TCI);

        #endregion

        #endregion

        #region IEquatable<VLANTag> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two VLAN tags for equality.
        /// </summary>
        /// <param name="Object">A VLAN tag to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is VLANTag vlanTag &&
                   Equals(vlanTag);

        #endregion

        #region Equals(VLANTag)

        /// <summary>
        /// Compares two VLAN tags for equality.
        /// </summary>
        /// <param name="VLANTag">A VLAN tag to compare with.</param>
        public Boolean Equals(VLANTag VLANTag)

            => TPID.Equals(VLANTag.TPID) &&
               TCI ==      VLANTag.TCI;

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => HashCode.Combine(TPID, TCI);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{(IsServiceTag ? "S-Tag" : "C-Tag")} VID {VID}, PCP {PCP} ({Priority}){(DEI ? ", DEI" : "")} [TPID {TPID:X}]";

        #endregion

    }

}
