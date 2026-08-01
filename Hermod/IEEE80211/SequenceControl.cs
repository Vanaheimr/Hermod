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
    /// The 2-byte IEEE 802.11 sequence control field: a 4-bit fragment number
    /// and a 12-bit sequence number.
    ///
    /// Unlike the frame control field - and unlike everything in Ethernet and IP -
    /// this field is transmitted in little-endian byte order.
    /// </summary>
    public readonly struct SequenceControl : IEquatable<SequenceControl>
    {

        #region Data

        /// <summary>
        /// The number of bytes of a sequence control field.
        /// </summary>
        public const Byte    Length             = 2;

        /// <summary>
        /// The largest possible sequence number (4095), after which it wraps around.
        /// </summary>
        public const UInt16  MaxSequenceNumber  = 4095;

        /// <summary>
        /// The largest possible fragment number (15).
        /// </summary>
        public const Byte    MaxFragmentNumber  = 15;

        #endregion

        #region Properties

        /// <summary>
        /// The raw little-endian value of this sequence control field.
        /// </summary>
        public UInt16  Value           { get; }


        /// <summary>
        /// The 4-bit fragment number, counting the fragments of one MSDU from 0.
        /// </summary>
        public Byte    FragmentNumber

            => (Byte) (Value & 0x000F);


        /// <summary>
        /// The 12-bit sequence number, counting the MSDUs of one station from 0.
        /// </summary>
        public UInt16  SequenceNumber

            => (UInt16) (Value >> 4);


        /// <summary>
        /// Whether this is the first fragment of an MSDU.
        /// </summary>
        public Boolean IsFirstFragment

            => FragmentNumber == 0;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new sequence control field.
        /// </summary>
        /// <param name="SequenceNumber">The 12-bit sequence number, 0..4095.</param>
        /// <param name="FragmentNumber">The 4-bit fragment number, 0..15.</param>
        public SequenceControl(UInt16  SequenceNumber,
                               Byte    FragmentNumber   = 0)
        {

            if (SequenceNumber > MaxSequenceNumber)
                throw new ArgumentOutOfRangeException(nameof(SequenceNumber),
                                                      $"The sequence number must not be larger than {MaxSequenceNumber}!");

            if (FragmentNumber > MaxFragmentNumber)
                throw new ArgumentOutOfRangeException(nameof(FragmentNumber),
                                                      $"The fragment number must not be larger than {MaxFragmentNumber}!");

            this.Value = (UInt16) ((SequenceNumber << 4) | FragmentNumber);

        }


        #endregion


        #region From     (Value)

        /// <summary>
        /// Create a new sequence control field from its raw value.
        /// </summary>
        /// <param name="Value">The raw value.</param>
        public static SequenceControl From(UInt16 Value)

            => new ((UInt16) (Value >> 4),
                    (Byte)   (Value & 0x0F));

        #endregion

        #region TryParse (Bytes, out SequenceControl)

        /// <summary>
        /// Try to read a sequence control field from the beginning of the given bytes.
        /// </summary>
        /// <param name="Bytes">A span of at least 2 bytes.</param>
        /// <param name="SequenceControl">The parsed sequence control field.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>   Bytes,
                                       out SequenceControl  SequenceControl)
        {

            SequenceControl = default;

            if (Bytes.Length < Length)
                return false;

            SequenceControl = From(BinaryPrimitives.ReadUInt16LittleEndian(Bytes));

            return true;

        }

        #endregion

        #region WriteTo  (Destination)

        /// <summary>
        /// Write the 2 bytes of this sequence control field into the given destination span.
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
        /// Return the 2 bytes of this sequence control field in transmission order.
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
        /// <param name="SequenceControl1">A sequence control field.</param>
        /// <param name="SequenceControl2">Another sequence control field.</param>
        public static Boolean operator == (SequenceControl SequenceControl1,
                                           SequenceControl SequenceControl2)

            => SequenceControl1.Equals(SequenceControl2);


        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SequenceControl1">A sequence control field.</param>
        /// <param name="SequenceControl2">Another sequence control field.</param>
        public static Boolean operator != (SequenceControl SequenceControl1,
                                           SequenceControl SequenceControl2)

            => !SequenceControl1.Equals(SequenceControl2);

        #endregion

        #region IEquatable<SequenceControl> Members

        /// <summary>
        /// Compares two sequence control fields for equality.
        /// </summary>
        /// <param name="Object">A sequence control field to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is SequenceControl sequenceControl &&
                   Equals(sequenceControl);


        /// <summary>
        /// Compares two sequence control fields for equality.
        /// </summary>
        /// <param name="SequenceControl">A sequence control field to compare with.</param>
        public Boolean Equals(SequenceControl SequenceControl)

            => Value == SequenceControl.Value;

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

            => $"seq {SequenceNumber}, frag {FragmentNumber}";

        #endregion

    }

}
