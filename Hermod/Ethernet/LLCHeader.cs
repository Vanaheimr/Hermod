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
    /// An IEEE 802.2 Logical Link Control (LLC) header, which follows the
    /// length field of an IEEE 802.3 frame.
    ///
    /// The control field is 1 byte for unnumbered (U) frames - identified by the
    /// two least significant bits being set - and 2 bytes for information (I) and
    /// supervisory (S) frames.
    /// </summary>
    public readonly struct LLCHeader : IEquatable<LLCHeader>
    {

        #region Data

        /// <summary>
        /// The Destination/Source Service Access Point of SNAP-encapsulated frames (0xAA).
        /// </summary>
        public const Byte SNAPServiceAccessPoint  = 0xAA;

        /// <summary>
        /// The Destination/Source Service Access Point of the Spanning Tree Protocol (0x42).
        /// </summary>
        public const Byte STPServiceAccessPoint   = 0x42;

        /// <summary>
        /// The Destination/Source Service Access Point of IPX (0xE0).
        /// </summary>
        public const Byte IPXServiceAccessPoint   = 0xE0;

        /// <summary>
        /// The Unnumbered Information (UI) control value (0x03).
        /// </summary>
        public const Byte UnnumberedInformation   = 0x03;

        #endregion

        #region Properties

        /// <summary>
        /// The Destination Service Access Point.
        /// </summary>
        public Byte     DSAP                { get; }

        /// <summary>
        /// The Source Service Access Point.
        /// </summary>
        public Byte     SSAP                { get; }

        /// <summary>
        /// The control field, 1 or 2 bytes wide.
        /// </summary>
        public UInt16   Control             { get; }

        /// <summary>
        /// The width of the control field in bytes, 1 or 2.
        /// </summary>
        public Byte     ControlLength       { get; }


        /// <summary>
        /// The total length of this LLC header in bytes, 3 or 4.
        /// </summary>
        public Byte     Length

            => (Byte) (2 + ControlLength);


        /// <summary>
        /// Whether this is an unnumbered (U) frame with a 1-byte control field.
        /// </summary>
        public Boolean  IsUnnumberedFormat

            => ControlLength == 1;


        /// <summary>
        /// Whether this LLC header introduces a SubNetwork Access Protocol (SNAP) header.
        /// </summary>
        public Boolean  IsSNAP

            => DSAP           == SNAPServiceAccessPoint &&
               SSAP           == SNAPServiceAccessPoint &&
               ControlLength  == 1 &&
               Control        == UnnumberedInformation;


        /// <summary>
        /// Whether the Destination Service Access Point addresses an individual
        /// station (I/G bit cleared) instead of a group.
        /// </summary>
        public Boolean  IsIndividualDSAP

            => (DSAP & 0x01) == 0;


        /// <summary>
        /// Whether this LLC PDU is a command (C/R bit cleared) instead of a response.
        /// </summary>
        public Boolean  IsCommand

            => (SSAP & 0x01) == 0;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new IEEE 802.2 LLC header.
        /// </summary>
        /// <param name="DSAP">The Destination Service Access Point.</param>
        /// <param name="SSAP">The Source Service Access Point.</param>
        /// <param name="Control">The control field.</param>
        /// <param name="ControlLength">The width of the control field in bytes, 1 or 2. Default is 1.</param>
        public LLCHeader(Byte    DSAP,
                         Byte    SSAP,
                         UInt16  Control,
                         Byte    ControlLength   = 1)
        {

            if (ControlLength is not 1 and not 2)
                throw new ArgumentOutOfRangeException(nameof(ControlLength),
                                                      "The LLC control field must be 1 or 2 bytes wide!");

            if (ControlLength == 1 && Control > Byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(Control),
                                                      "A 1-byte LLC control field must not exceed 0xFF!");

            this.DSAP           = DSAP;
            this.SSAP           = SSAP;
            this.Control        = Control;
            this.ControlLength  = ControlLength;

        }

        #endregion


        #region SNAP

        /// <summary>
        /// The LLC header introducing a SubNetwork Access Protocol (SNAP) header
        /// (DSAP 0xAA, SSAP 0xAA, control 0x03).
        /// </summary>
        public static LLCHeader SNAP { get; }

            = new (SNAPServiceAccessPoint,
                   SNAPServiceAccessPoint,
                   UnnumberedInformation);

        #endregion

        #region TryParse (Bytes, out LLCHeader)

        /// <summary>
        /// Try to read an IEEE 802.2 LLC header from the beginning of the given bytes.
        /// </summary>
        /// <param name="Bytes">A span of at least 3 bytes starting with the DSAP.</param>
        /// <param name="LLCHeader">The parsed LLC header.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>  Bytes,
                                       out LLCHeader       LLCHeader)
        {

            LLCHeader = default;

            if (Bytes.Length < 3)
                return false;

            // The two least significant bits of an unnumbered format control
            // field are set; all other formats use a 2-byte control field.
            var isUnnumbered = (Bytes[2] & 0x03) == 0x03;

            if (!isUnnumbered && Bytes.Length < 4)
                return false;

            LLCHeader = isUnnumbered

                            ? new LLCHeader(
                                  Bytes[0],
                                  Bytes[1],
                                  Bytes[2],
                                  1
                              )

                            : new LLCHeader(
                                  Bytes[0],
                                  Bytes[1],
                                  BinaryPrimitives.ReadUInt16BigEndian(Bytes[2..]),
                                  2
                              );

            return true;

        }

        #endregion

        #region GetBytes()

        /// <summary>
        /// Return the bytes of this LLC header in network byte order.
        /// </summary>
        public Byte[] GetBytes()

            => ControlLength == 1

                   ? [ DSAP,
                       SSAP,
                       (Byte) Control ]

                   : [ DSAP,
                       SSAP,
                       (Byte) (Control >> 8),
                       (Byte) (Control & 0xFF) ];

        #endregion

        #region WriteTo(Destination)

        /// <summary>
        /// Write the bytes of this LLC header into the given destination span.
        /// </summary>
        /// <param name="Destination">A span to write the bytes of this LLC header into.</param>
        public void WriteTo(Span<Byte> Destination)
        {

            if (Destination.Length < Length)
                throw new ArgumentException("Destination span too small.", nameof(Destination));

            Destination[0] = DSAP;
            Destination[1] = SSAP;

            if (ControlLength == 1)
                Destination[2] = (Byte) Control;
            else
                BinaryPrimitives.WriteUInt16BigEndian(Destination[2..], Control);

        }

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="LLCHeader1">An LLC header.</param>
        /// <param name="LLCHeader2">Another LLC header.</param>
        public static Boolean operator == (LLCHeader LLCHeader1,
                                           LLCHeader LLCHeader2)

            => LLCHeader1.Equals(LLCHeader2);


        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="LLCHeader1">An LLC header.</param>
        /// <param name="LLCHeader2">Another LLC header.</param>
        public static Boolean operator != (LLCHeader LLCHeader1,
                                           LLCHeader LLCHeader2)

            => !LLCHeader1.Equals(LLCHeader2);

        #endregion

        #region IEquatable<LLCHeader> Members

        /// <summary>
        /// Compares two LLC headers for equality.
        /// </summary>
        /// <param name="Object">An LLC header to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is LLCHeader llcHeader &&
                   Equals(llcHeader);


        /// <summary>
        /// Compares two LLC headers for equality.
        /// </summary>
        /// <param name="LLCHeader">An LLC header to compare with.</param>
        public Boolean Equals(LLCHeader LLCHeader)

            => DSAP           == LLCHeader.DSAP    &&
               SSAP           == LLCHeader.SSAP    &&
               Control        == LLCHeader.Control &&
               ControlLength  == LLCHeader.ControlLength;

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => HashCode.Combine(DSAP, SSAP, Control, ControlLength);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => IsSNAP
                   ? "LLC/SNAP"
                   : $"LLC DSAP 0x{DSAP:X2}, SSAP 0x{SSAP:X2}, control 0x{Control:X2}";

        #endregion

    }


    /// <summary>
    /// An IEEE 802.2 SubNetwork Access Protocol (SNAP) header (RFC 1042), which follows
    /// an LLC header with DSAP == SSAP == 0xAA and control == 0x03. It re-introduces the
    /// 16-bit protocol identifier that the IEEE 802.3 length field displaced.
    /// </summary>
    public readonly struct SNAPHeader : IEquatable<SNAPHeader>
    {

        #region Data

        /// <summary>
        /// The number of bytes of a SNAP header (3 bytes OUI + 2 bytes protocol identifier).
        /// </summary>
        public const Byte   Length          = 5;

        /// <summary>
        /// The RFC 1042 Organizationally Unique Identifier (0x000000), which indicates
        /// that the protocol identifier is an ordinary EtherType.
        /// </summary>
        public const UInt32 RFC1042OUI     = 0x000000;

        /// <summary>
        /// The Cisco Organizationally Unique Identifier (0x00000C), used e.g. by CDP and PVST+.
        /// </summary>
        public const UInt32 CiscoOUI       = 0x00000C;

        /// <summary>
        /// The Bridge-Tunnel Organizationally Unique Identifier (0x0000F8) of RFC 1042.
        /// </summary>
        public const UInt32 BridgeTunnelOUI = 0x0000F8;

        #endregion

        #region Properties

        /// <summary>
        /// The 24-bit Organizationally Unique Identifier / protocol identifier prefix.
        /// </summary>
        public UInt32     OUI          { get; }

        /// <summary>
        /// The 16-bit protocol identifier. For the RFC 1042 OUI this is an EtherType.
        /// </summary>
        public EtherType  ProtocolId   { get; }


        /// <summary>
        /// Whether this SNAP header uses the RFC 1042 OUI, in which case
        /// the protocol identifier is an ordinary EtherType.
        /// </summary>
        public Boolean    IsRFC1042

            => OUI == RFC1042OUI;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SNAP header.
        /// </summary>
        /// <param name="ProtocolId">The 16-bit protocol identifier.</param>
        /// <param name="OUI">The 24-bit Organizationally Unique Identifier. Default is the RFC 1042 OUI (0x000000).</param>
        public SNAPHeader(EtherType  ProtocolId,
                          UInt32     OUI   = RFC1042OUI)
        {

            if (OUI > 0xFFFFFF)
                throw new ArgumentOutOfRangeException(nameof(OUI),
                                                      "An Organizationally Unique Identifier must not exceed 24 bits!");

            this.ProtocolId  = ProtocolId;
            this.OUI         = OUI;

        }

        #endregion


        #region TryParse (Bytes, out SNAPHeader)

        /// <summary>
        /// Try to read a SNAP header from the beginning of the given bytes.
        /// </summary>
        /// <param name="Bytes">A span of at least 5 bytes starting with the OUI.</param>
        /// <param name="SNAPHeader">The parsed SNAP header.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>  Bytes,
                                       out SNAPHeader      SNAPHeader)
        {

            SNAPHeader = default;

            if (Bytes.Length < Length)
                return false;

            SNAPHeader = new SNAPHeader(
                             EtherType.From(BinaryPrimitives.ReadUInt16BigEndian(Bytes[3..])),
                             (UInt32) ((Bytes[0] << 16) | (Bytes[1] << 8) | Bytes[2])
                         );

            return true;

        }

        #endregion

        #region GetBytes()

        /// <summary>
        /// Return the 5 bytes of this SNAP header in network byte order.
        /// </summary>
        public Byte[] GetBytes()

            => [ (Byte) ((OUI >> 16) & 0xFF),
                 (Byte) ((OUI >>  8) & 0xFF),
                 (Byte)  (OUI        & 0xFF),
                 (Byte)  (ProtocolId.Value >> 8),
                 (Byte)  (ProtocolId.Value & 0xFF) ];

        #endregion

        #region WriteTo(Destination)

        /// <summary>
        /// Write the 5 bytes of this SNAP header into the given destination span.
        /// </summary>
        /// <param name="Destination">A span to write the bytes of this SNAP header into.</param>
        public void WriteTo(Span<Byte> Destination)
        {

            if (Destination.Length < Length)
                throw new ArgumentException("Destination span too small.", nameof(Destination));

            Destination[0] = (Byte) ((OUI >> 16) & 0xFF);
            Destination[1] = (Byte) ((OUI >>  8) & 0xFF);
            Destination[2] = (Byte)  (OUI        & 0xFF);

            BinaryPrimitives.WriteUInt16BigEndian(Destination[3..], ProtocolId.Value);

        }

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SNAPHeader1">A SNAP header.</param>
        /// <param name="SNAPHeader2">Another SNAP header.</param>
        public static Boolean operator == (SNAPHeader SNAPHeader1,
                                           SNAPHeader SNAPHeader2)

            => SNAPHeader1.Equals(SNAPHeader2);


        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SNAPHeader1">A SNAP header.</param>
        /// <param name="SNAPHeader2">Another SNAP header.</param>
        public static Boolean operator != (SNAPHeader SNAPHeader1,
                                           SNAPHeader SNAPHeader2)

            => !SNAPHeader1.Equals(SNAPHeader2);

        #endregion

        #region IEquatable<SNAPHeader> Members

        /// <summary>
        /// Compares two SNAP headers for equality.
        /// </summary>
        /// <param name="Object">A SNAP header to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is SNAPHeader snapHeader &&
                   Equals(snapHeader);


        /// <summary>
        /// Compares two SNAP headers for equality.
        /// </summary>
        /// <param name="SNAPHeader">A SNAP header to compare with.</param>
        public Boolean Equals(SNAPHeader SNAPHeader)

            => OUI == SNAPHeader.OUI &&
               ProtocolId.Equals(SNAPHeader.ProtocolId);

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => HashCode.Combine(OUI, ProtocolId);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"SNAP OUI {(OUI >> 16) & 0xFF:X2}:{(OUI >> 8) & 0xFF:X2}:{OUI & 0xFF:X2}, protocol {ProtocolId:F}";

        #endregion

    }

}
