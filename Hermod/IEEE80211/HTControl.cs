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
    /// The variant of an HT control field, selected by its two lowest bits.
    /// </summary>
    public enum HTControlVariants : Byte
    {

        /// <summary>
        /// The HT variant of IEEE 802.11n (B0 = 0).
        /// </summary>
        HT   = 0,

        /// <summary>
        /// The VHT variant of IEEE 802.11ac (B0 = 1, B1 = 0).
        /// </summary>
        VHT  = 1,

        /// <summary>
        /// The HE variant of IEEE 802.11ax (B0 = 1, B1 = 1).
        /// </summary>
        HE   = 2

    }


    /// <summary>
    /// The 4-byte HT control field, added by IEEE 802.11n and reshaped by every
    /// amendment since. It is present in QoS data and management frames whose
    /// order / +HTC bit is set.
    ///
    /// Its interior differs per variant and per amendment, so only the variant and
    /// the raw value are decoded here - anything more would have to be re-done with
    /// every new amendment.
    ///
    /// Transmitted in little-endian byte order.
    /// </summary>
    public readonly struct HTControl : IEquatable<HTControl>
    {

        #region Data

        /// <summary>
        /// The number of bytes of an HT control field.
        /// </summary>
        public const Byte Length = 4;

        #endregion

        #region Properties

        /// <summary>
        /// The raw little-endian value of this HT control field.
        /// </summary>
        public UInt32             Value      { get; }


        /// <summary>
        /// The variant of this HT control field.
        /// </summary>
        public HTControlVariants  Variant

            => (Value & 0x01) == 0
                   ? HTControlVariants.HT
                   : (Value & 0x02) == 0
                         ? HTControlVariants.VHT
                         : HTControlVariants.HE;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HT control field.
        /// </summary>
        /// <param name="Value">The raw value.</param>
        private HTControl(UInt32 Value)
        {
            this.Value = Value;
        }

        #endregion


        #region From     (Value)

        /// <summary>
        /// Create a new HT control field from its raw value.
        /// </summary>
        /// <param name="Value">The raw value.</param>
        public static HTControl From(UInt32 Value)

            => new (Value);

        #endregion

        #region TryParse (Bytes, out HTControl)

        /// <summary>
        /// Try to read an HT control field from the beginning of the given bytes.
        /// </summary>
        /// <param name="Bytes">A span of at least 4 bytes.</param>
        /// <param name="HTControl">The parsed HT control field.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>  Bytes,
                                       out HTControl       HTControl)
        {

            HTControl = default;

            if (Bytes.Length < Length)
                return false;

            HTControl = new HTControl(BinaryPrimitives.ReadUInt32LittleEndian(Bytes));

            return true;

        }

        #endregion

        #region WriteTo  (Destination)

        /// <summary>
        /// Write the 4 bytes of this HT control field into the given destination span.
        /// </summary>
        /// <param name="Destination">A span to write the bytes into.</param>
        public void WriteTo(Span<Byte> Destination)
        {

            if (Destination.Length < Length)
                throw new ArgumentException("Destination span too small.", nameof(Destination));

            BinaryPrimitives.WriteUInt32LittleEndian(Destination, Value);

        }

        #endregion

        #region GetBytes ()

        /// <summary>
        /// Return the 4 bytes of this HT control field in transmission order.
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
        /// <param name="HTControl1">An HT control field.</param>
        /// <param name="HTControl2">Another HT control field.</param>
        public static Boolean operator == (HTControl HTControl1,
                                           HTControl HTControl2)

            => HTControl1.Equals(HTControl2);


        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTControl1">An HT control field.</param>
        /// <param name="HTControl2">Another HT control field.</param>
        public static Boolean operator != (HTControl HTControl1,
                                           HTControl HTControl2)

            => !HTControl1.Equals(HTControl2);

        #endregion

        #region IEquatable<HTControl> Members

        /// <summary>
        /// Compares two HT control fields for equality.
        /// </summary>
        /// <param name="Object">An HT control field to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTControl htControl &&
                   Equals(htControl);


        /// <summary>
        /// Compares two HT control fields for equality.
        /// </summary>
        /// <param name="HTControl">An HT control field to compare with.</param>
        public Boolean Equals(HTControl HTControl)

            => Value == HTControl.Value;

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

            => $"{Variant} control (0x{Value:X8})";

        #endregion

    }

}
