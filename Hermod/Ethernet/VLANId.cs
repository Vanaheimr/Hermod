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

using System.Globalization;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Ethernet
{

    /// <summary>
    /// Extension methods for VLAN identifiers.
    /// </summary>
    public static class VLANIdExtensions
    {

        /// <summary>
        /// Indicates whether this VLAN identifier is null or the null VLAN identifier (0).
        /// </summary>
        /// <param name="VLANId">A VLAN identifier.</param>
        public static Boolean IsNullOrNullVLAN(this VLANId? VLANId)
            => !VLANId.HasValue || VLANId.Value.IsNullVLAN;

        /// <summary>
        /// Indicates whether this VLAN identifier is NOT null and NOT the null VLAN identifier (0).
        /// </summary>
        /// <param name="VLANId">A VLAN identifier.</param>
        public static Boolean IsNotNullOrNullVLAN(this VLANId? VLANId)
            => VLANId.HasValue && !VLANId.Value.IsNullVLAN;

    }


    /// <summary>
    /// A 12-bit IEEE 802.1Q VLAN identifier (VID), 0..4095.
    /// </summary>
    public readonly struct VLANId : IEquatable<VLANId>,
                                    IComparable<VLANId>,
                                    IComparable,
                                    IParsable<VLANId>,
                                    IFormattable
    {

        #region Data

        /// <summary>
        /// The number of bits of a VLAN identifier.
        /// </summary>
        public const  Byte    Bits      = 12;

        /// <summary>
        /// The largest possible VLAN identifier (4095).
        /// </summary>
        public const  UInt16  MaxValue  = 4095;

        #endregion

        #region Properties

        /// <summary>
        /// The numeric value of this VLAN identifier.
        /// </summary>
        public UInt16   Value                { get; }


        /// <summary>
        /// The null VLAN identifier (0). A frame carrying this VID is "priority-tagged":
        /// it does not belong to any VLAN, the tag only conveys the priority code point.
        /// </summary>
        public static VLANId  Null     { get; } = new (0);

        /// <summary>
        /// The default port VLAN identifier (1).
        /// </summary>
        public static VLANId  Default  { get; } = new (1);

        /// <summary>
        /// The reserved VLAN identifier (4095), which must not be transmitted.
        /// </summary>
        public static VLANId  Reserved { get; } = new (MaxValue);


        /// <summary>
        /// Whether this is the null VLAN identifier (0), i.e. the frame is priority-tagged only.
        /// </summary>
        public Boolean  IsNullVLAN

            => Value == 0;


        /// <summary>
        /// Whether this is the default VLAN identifier (1).
        /// </summary>
        public Boolean  IsDefaultVLAN

            => Value == 1;


        /// <summary>
        /// Whether this is the reserved VLAN identifier (4095), which must not be transmitted.
        /// </summary>
        public Boolean  IsReserved

            => Value == MaxValue;


        /// <summary>
        /// Whether this VLAN identifier may be assigned to a VLAN (1..4094).
        /// </summary>
        public Boolean  IsAssignable

            => Value >= 1 && Value <= MaxValue - 1;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new VLAN identifier.
        /// </summary>
        /// <param name="Value">The numeric value of the VLAN identifier, 0..4095.</param>
        private VLANId(UInt16 Value)
        {
            this.Value = Value;
        }

        #endregion


        #region From     (Value)

        /// <summary>
        /// Create a new VLAN identifier from the given numeric value.
        /// </summary>
        /// <param name="Value">The numeric value of the VLAN identifier, 0..4095.</param>
        public static VLANId From(UInt16 Value)
        {

            if (Value > MaxValue)
                throw new ArgumentOutOfRangeException(nameof(Value),
                                                      $"A VLAN identifier must not be larger than {MaxValue}!");

            return new VLANId(Value);

        }

        #endregion

        #region TryFrom  (Value)

        /// <summary>
        /// Try to create a new VLAN identifier from the given numeric value.
        /// </summary>
        /// <param name="Value">The numeric value of the VLAN identifier, 0..4095.</param>
        public static VLANId? TryFrom(UInt16 Value)

            => Value <= MaxValue
                   ? new VLANId(Value)
                   : null;

        #endregion


        #region Parse    (Text)

        /// <summary>
        /// Parse the given text as a VLAN identifier.
        /// </summary>
        /// <param name="Text">A decimal text representation of a VLAN identifier.</param>
        public static VLANId Parse(String Text)

            => TryParse(Text, out var vlanId)
                   ? vlanId
                   : throw new FormatException($"Invalid VLAN identifier: '{Text}'!");

        #endregion

        #region Parse    (Text, Provider)

        /// <summary>
        /// Parse the given text as a VLAN identifier.
        /// </summary>
        /// <param name="Text">A decimal text representation of a VLAN identifier.</param>
        /// <param name="Provider">A format provider (ignored).</param>
        public static VLANId Parse(String Text, IFormatProvider? Provider)

            => Parse(Text);

        #endregion

        #region TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a VLAN identifier.
        /// </summary>
        /// <param name="Text">A decimal text representation of a VLAN identifier.</param>
        public static VLANId? TryParse(String? Text)

            => TryParse(Text, out var vlanId)
                   ? vlanId
                   : null;

        #endregion

        #region TryParse (Text, out VLANId)

        /// <summary>
        /// Try to parse the given text as a VLAN identifier.
        /// </summary>
        /// <param name="Text">A decimal text representation of a VLAN identifier.</param>
        /// <param name="VLANId">The parsed VLAN identifier.</param>
        public static Boolean TryParse(String? Text, out VLANId VLANId)
        {

            VLANId = default;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            if (UInt16.TryParse(Text.Trim(),
                                NumberStyles.None,
                                CultureInfo.InvariantCulture,
                                out var value) &&
                value <= MaxValue)
            {
                VLANId = new VLANId(value);
                return true;
            }

            return false;

        }

        #endregion

        #region TryParse (Text, Provider, out VLANId)

        /// <summary>
        /// Try to parse the given text as a VLAN identifier.
        /// </summary>
        /// <param name="Text">A decimal text representation of a VLAN identifier.</param>
        /// <param name="Provider">VLAN identifiers are culture-invariant, so we can ignore the provider!</param>
        /// <param name="VLANId">The parsed VLAN identifier.</param>
        public static Boolean TryParse([NotNullWhen(true)] String?  Text,
                                       IFormatProvider?             Provider,
                                       out VLANId                   VLANId)

            => TryParse(Text, out VLANId);

        #endregion


        #region Operator overloading

        #region Operator == (VLANId1, VLANId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANId1">A VLAN identifier.</param>
        /// <param name="VLANId2">Another VLAN identifier.</param>
        public static Boolean operator == (VLANId VLANId1,
                                           VLANId VLANId2)

            => VLANId1.Equals(VLANId2);

        #endregion

        #region Operator != (VLANId1, VLANId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANId1">A VLAN identifier.</param>
        /// <param name="VLANId2">Another VLAN identifier.</param>
        public static Boolean operator != (VLANId VLANId1,
                                           VLANId VLANId2)

            => !VLANId1.Equals(VLANId2);

        #endregion

        #region Operator <  (VLANId1, VLANId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANId1">A VLAN identifier.</param>
        /// <param name="VLANId2">Another VLAN identifier.</param>
        public static Boolean operator < (VLANId VLANId1,
                                          VLANId VLANId2)

            => VLANId1.CompareTo(VLANId2) < 0;

        #endregion

        #region Operator <= (VLANId1, VLANId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANId1">A VLAN identifier.</param>
        /// <param name="VLANId2">Another VLAN identifier.</param>
        public static Boolean operator <= (VLANId VLANId1,
                                           VLANId VLANId2)

            => VLANId1.CompareTo(VLANId2) <= 0;

        #endregion

        #region Operator >  (VLANId1, VLANId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANId1">A VLAN identifier.</param>
        /// <param name="VLANId2">Another VLAN identifier.</param>
        public static Boolean operator > (VLANId VLANId1,
                                          VLANId VLANId2)

            => VLANId1.CompareTo(VLANId2) > 0;

        #endregion

        #region Operator >= (VLANId1, VLANId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VLANId1">A VLAN identifier.</param>
        /// <param name="VLANId2">Another VLAN identifier.</param>
        public static Boolean operator >= (VLANId VLANId1,
                                           VLANId VLANId2)

            => VLANId1.CompareTo(VLANId2) >= 0;

        #endregion

        #endregion

        #region IComparable<VLANId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two VLAN identifiers.
        /// </summary>
        /// <param name="Object">A VLAN identifier to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is VLANId vlanId
                   ? CompareTo(vlanId)
                   : throw new ArgumentException("The given object is not a VLAN identifier!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(VLANId)

        /// <summary>
        /// Compares two VLAN identifiers.
        /// </summary>
        /// <param name="VLANId">A VLAN identifier to compare with.</param>
        public Int32 CompareTo(VLANId VLANId)

            => Value.CompareTo(VLANId.Value);

        #endregion

        #endregion

        #region IEquatable<VLANId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two VLAN identifiers for equality.
        /// </summary>
        /// <param name="Object">A VLAN identifier to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is VLANId vlanId &&
                   Equals(vlanId);

        #endregion

        #region Equals(VLANId)

        /// <summary>
        /// Compares two VLAN identifiers for equality.
        /// </summary>
        /// <param name="VLANId">A VLAN identifier to compare with.</param>
        public Boolean Equals(VLANId VLANId)

            => Value == VLANId.Value;

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => Value.GetHashCode();

        #endregion

        #region ToString (Format)

        /// <summary>
        /// Return a text representation of this object,
        /// using the specified format.
        /// </summary>
        /// <param name="Format">The format string to use when formatting the VLAN identifier.</param>
        public String ToString(String? Format)

            => ToString(Format, null);

        #endregion

        #region ToString (Format, FormatProvider)

        /// <summary>
        /// Return a text representation of this object,
        /// using the specified format and provider.
        /// </summary>
        /// <param name="Format">The format string to use when formatting the VLAN identifier.</param>
        /// <param name="FormatProvider">The format provider to use. This parameter is ignored, since VLAN identifiers are culture-invariant.</param>
        public String ToString(String?           Format,
                               IFormatProvider?  FormatProvider)

            => Format switch {
                   null or "" or "G" or "D" => Value.ToString(CultureInfo.InvariantCulture),
                   "X"                      => $"0x{Value:X3}",
                   "x"                      => $"0x{Value:x3}",
                   _                        => throw new FormatException($"Invalid VLAN identifier format: '{Format}'!")
               };

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Value.ToString(CultureInfo.InvariantCulture);

        #endregion

    }

}
