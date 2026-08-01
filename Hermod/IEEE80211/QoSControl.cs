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

using org.GraphDefined.Vanaheimr.Hermod.Ethernet;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.IEEE80211
{

    /// <summary>
    /// The acknowledgment policy of a QoS data frame.
    /// </summary>
    public enum AckPolicies : Byte
    {

        /// <summary>
        /// Normal acknowledgment: the recipient answers with an ACK or a Block Ack.
        /// </summary>
        NormalAck            = 0,

        /// <summary>
        /// No acknowledgment at all, e.g. for latency-sensitive streams.
        /// </summary>
        NoAck                = 1,

        /// <summary>
        /// No explicit acknowledgment, or PSMP acknowledgment.
        /// </summary>
        NoExplicitAck        = 2,

        /// <summary>
        /// Block acknowledgment: the recipient acknowledges a whole burst later on.
        /// </summary>
        BlockAck             = 3

    }


    /// <summary>
    /// The 2-byte IEEE 802.11e QoS control field, present in every QoS data frame.
    ///
    /// <code>
    /// b15                      b8 b7    b6  b5 b4   b3        b0
    /// +--------------------------+----+-------+---+------------+
    /// | TXOP limit / queue size  |AMSD|Ack pol|EOSP|    TID    |
    /// +--------------------------+----+-------+---+------------+
    /// </code>
    ///
    /// Transmitted in little-endian byte order.
    /// </summary>
    public readonly struct QoSControl : IEquatable<QoSControl>
    {

        #region Data

        /// <summary>
        /// The number of bytes of a QoS control field.
        /// </summary>
        public const Byte Length     = 2;

        /// <summary>
        /// The largest possible traffic identifier (15).
        /// </summary>
        public const Byte MaxTID     = 15;

        #endregion

        #region Properties

        /// <summary>
        /// The raw little-endian value of this QoS control field.
        /// </summary>
        public UInt16         Value              { get; }


        /// <summary>
        /// The 4-bit traffic identifier. Values 0..7 are the access categories
        /// of enhanced distributed channel access, 8..15 are parameterized ones.
        /// </summary>
        public Byte           TID

            => (Byte) (Value & 0x000F);


        /// <summary>
        /// The user priority of this frame, which for the traffic identifiers 0..7
        /// is the very same IEEE 802.1p priority as the one of a VLAN tag - this is
        /// where a wireless access point maps its traffic classes onto a wired network.
        /// </summary>
        public PCPPriorities? UserPriority

            => TID <= 7
                   ? (PCPPriorities) TID
                   : null;


        /// <summary>
        /// The end of service period bit.
        /// </summary>
        public Boolean        EOSP

            => (Value & 0x0010) != 0;


        /// <summary>
        /// The acknowledgment policy.
        /// </summary>
        public AckPolicies    AckPolicy

            => (AckPolicies) ((Value >> 5) & 0x03);


        /// <summary>
        /// Whether the frame body is an aggregated MSDU (IEEE 802.11n).
        /// </summary>
        public Boolean        IsAMSDU

            => (Value & 0x0080) != 0;


        /// <summary>
        /// The upper byte, whose meaning depends on the sender and the subtype:
        /// TXOP limit, TXOP duration requested, AP PS buffer state or queue size.
        /// </summary>
        public Byte           TXOPOrQueueSize

            => (Byte) (Value >> 8);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new QoS control field.
        /// </summary>
        /// <param name="TID">The 4-bit traffic identifier, 0..15.</param>
        /// <param name="AckPolicy">The acknowledgment policy.</param>
        /// <param name="EOSP">The end of service period bit.</param>
        /// <param name="IsAMSDU">Whether the frame body is an aggregated MSDU.</param>
        /// <param name="TXOPOrQueueSize">The upper byte: TXOP limit, queue size, ...</param>
        public QoSControl(Byte         TID,
                          AckPolicies  AckPolicy         = AckPolicies.NormalAck,
                          Boolean      EOSP              = false,
                          Boolean      IsAMSDU           = false,
                          Byte         TXOPOrQueueSize   = 0)
        {

            if (TID > MaxTID)
                throw new ArgumentOutOfRangeException(nameof(TID),
                                                      $"The traffic identifier must not be larger than {MaxTID}!");

            this.Value = (UInt16) (TID                              |
                                   (EOSP    ? 0x0010 : 0x0000)      |
                                   (((Byte) AckPolicy & 0x03) << 5) |
                                   (IsAMSDU ? 0x0080 : 0x0000)      |
                                   (TXOPOrQueueSize << 8));

        }


        #endregion


        #region From     (Value)

        /// <summary>
        /// Create a new QoS control field from its raw value. Every one of the
        /// 16 bits belongs to one of the fields, so this round-trips exactly.
        /// </summary>
        /// <param name="Value">The raw value.</param>
        public static QoSControl From(UInt16 Value)

            => new ((Byte)         (Value        & 0x0F),
                    (AckPolicies) ((Value >>  5) & 0x03),
                                   (Value & 0x0010) != 0,
                                   (Value & 0x0080) != 0,
                    (Byte)         (Value >>  8));

        #endregion

        #region TryParse (Bytes, out QoSControl)

        /// <summary>
        /// Try to read a QoS control field from the beginning of the given bytes.
        /// </summary>
        /// <param name="Bytes">A span of at least 2 bytes.</param>
        /// <param name="QoSControl">The parsed QoS control field.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>  Bytes,
                                       out QoSControl      QoSControl)
        {

            QoSControl = default;

            if (Bytes.Length < Length)
                return false;

            QoSControl = From(BinaryPrimitives.ReadUInt16LittleEndian(Bytes));

            return true;

        }

        #endregion

        #region WriteTo  (Destination)

        /// <summary>
        /// Write the 2 bytes of this QoS control field into the given destination span.
        /// </summary>
        /// <param name="Destination">A span to write the bytes into.</param>
        public void WriteTo(Span<Byte> Destination)
        {

            if (Destination.Length < Length)
                throw new ArgumentException("Destination span too small.", nameof(Destination));

            BinaryPrimitives.WriteUInt16LittleEndian(Destination, Value);

        }

        #endregion

        #region GetBytes ()

        /// <summary>
        /// Return the 2 bytes of this QoS control field in transmission order.
        /// </summary>
        public Byte[] GetBytes()
        {

            var bytes = new Byte[Length];
            WriteTo(bytes);

            return bytes;

        }

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="QoSControl1">A QoS control field.</param>
        /// <param name="QoSControl2">Another QoS control field.</param>
        public static Boolean operator == (QoSControl QoSControl1,
                                           QoSControl QoSControl2)

            => QoSControl1.Equals(QoSControl2);


        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="QoSControl1">A QoS control field.</param>
        /// <param name="QoSControl2">Another QoS control field.</param>
        public static Boolean operator != (QoSControl QoSControl1,
                                           QoSControl QoSControl2)

            => !QoSControl1.Equals(QoSControl2);

        #endregion

        #region IEquatable<QoSControl> Members

        /// <summary>
        /// Compares two QoS control fields for equality.
        /// </summary>
        /// <param name="Object">A QoS control field to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is QoSControl qosControl &&
                   Equals(qosControl);


        /// <summary>
        /// Compares two QoS control fields for equality.
        /// </summary>
        /// <param name="QoSControl">A QoS control field to compare with.</param>
        public Boolean Equals(QoSControl QoSControl)

            => Value == QoSControl.Value;

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

            => $"TID {TID}{(UserPriority.HasValue ? $" ({UserPriority})" : "")}, {AckPolicy}{(IsAMSDU ? ", A-MSDU" : "")}{(EOSP ? ", EOSP" : "")}";

        #endregion

    }

}
