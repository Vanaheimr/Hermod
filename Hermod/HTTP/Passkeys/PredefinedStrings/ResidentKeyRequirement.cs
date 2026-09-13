/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    // https://w3c.github.io/webauthn/#enumdef-residentkeyrequirement

    /// <summary>
    /// Extension methods for ResidentKeyRequirements.
    /// </summary>
    public static class ResidentKeyRequirementExtensions
    {

        /// <summary>
        /// Indicates whether this ResidentKeyRequirement is null or empty.
        /// </summary>
        /// <param name="ResidentKeyRequirement">An ResidentKeyRequirement.</param>
        public static Boolean IsNullOrEmpty(this ResidentKeyRequirement? ResidentKeyRequirement)
            => !ResidentKeyRequirement.HasValue || ResidentKeyRequirement.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this ResidentKeyRequirement is null or empty.
        /// </summary>
        /// <param name="ResidentKeyRequirement">An ResidentKeyRequirement.</param>
        public static Boolean IsNotNullOrEmpty(this ResidentKeyRequirement? ResidentKeyRequirement)
            => ResidentKeyRequirement.HasValue && ResidentKeyRequirement.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A ResidentKeyRequirement.
    /// </summary>
    public readonly struct ResidentKeyRequirement : IId,
                                                         IEquatable<ResidentKeyRequirement>,
                                                         IComparable<ResidentKeyRequirement>
    {

        #region Data

        private readonly static Dictionary<String, ResidentKeyRequirement>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                           InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this ResidentKeyRequirement is null or empty.
        /// </summary>
        public readonly  Boolean                    IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this ResidentKeyRequirement is NOT null or empty.
        /// </summary>
        public readonly  Boolean                    IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the ResidentKeyRequirement.
        /// </summary>
        public readonly  UInt64                     Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// All registered ResidentKeyRequirements.
        /// </summary>
        public static    IEnumerable<ResidentKeyRequirement>  All
            => lookup.Values;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new ResidentKeyRequirement based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of an ResidentKeyRequirement.</param>
        private ResidentKeyRequirement(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static ResidentKeyRequirement Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new ResidentKeyRequirement(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as an ResidentKeyRequirement.
        /// </summary>
        /// <param name="Text">A text representation of an ResidentKeyRequirement.</param>
        public static ResidentKeyRequirement Parse(String Text)
        {

            if (TryParse(Text, out var residentKeyRequirement))
                return residentKeyRequirement;

            throw new ArgumentException($"Invalid text representation of an ResidentKeyRequirement: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as an ResidentKeyRequirement.
        /// </summary>
        /// <param name="Text">A text representation of an ResidentKeyRequirement.</param>
        public static ResidentKeyRequirement? TryParse(String Text)
        {

            if (TryParse(Text, out var residentKeyRequirement))
                return residentKeyRequirement;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out ResidentKeyRequirement)

        /// <summary>
        /// Try to parse the given text as an ResidentKeyRequirement.
        /// </summary>
        /// <param name="Text">A text representation of an ResidentKeyRequirement.</param>
        /// <param name="ResidentKeyRequirement">The parsed ResidentKeyRequirement.</param>
        public static Boolean TryParse(String Text, out ResidentKeyRequirement ResidentKeyRequirement)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out ResidentKeyRequirement))
                    ResidentKeyRequirement = Register(Text);

                return true;

            }

            ResidentKeyRequirement = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this ResidentKeyRequirement.
        /// </summary>
        public ResidentKeyRequirement Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// required
        /// </summary>
        public static ResidentKeyRequirement  Required       { get; }
            = Register("required");

        /// <summary>
        /// preferred (default)
        /// </summary>
        public static ResidentKeyRequirement  Preferred      { get; }
            = Register("preferred");

        /// <summary>
        /// discouraged
        /// </summary>
        public static ResidentKeyRequirement  Discouraged    { get; }
            = Register("discouraged");

        #endregion


        #region Operator overloading

        #region Operator == (ResidentKeyRequirement1, ResidentKeyRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ResidentKeyRequirement1">An ResidentKeyRequirement.</param>
        /// <param name="ResidentKeyRequirement2">Another ResidentKeyRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (ResidentKeyRequirement ResidentKeyRequirement1,
                                           ResidentKeyRequirement ResidentKeyRequirement2)

            => ResidentKeyRequirement1.Equals(ResidentKeyRequirement2);

        #endregion

        #region Operator != (ResidentKeyRequirement1, ResidentKeyRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ResidentKeyRequirement1">An ResidentKeyRequirement.</param>
        /// <param name="ResidentKeyRequirement2">Another ResidentKeyRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (ResidentKeyRequirement ResidentKeyRequirement1,
                                           ResidentKeyRequirement ResidentKeyRequirement2)

            => !ResidentKeyRequirement1.Equals(ResidentKeyRequirement2);

        #endregion

        #region Operator <  (ResidentKeyRequirement1, ResidentKeyRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ResidentKeyRequirement1">An ResidentKeyRequirement.</param>
        /// <param name="ResidentKeyRequirement2">Another ResidentKeyRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (ResidentKeyRequirement ResidentKeyRequirement1,
                                          ResidentKeyRequirement ResidentKeyRequirement2)

            => ResidentKeyRequirement1.CompareTo(ResidentKeyRequirement2) < 0;

        #endregion

        #region Operator <= (ResidentKeyRequirement1, ResidentKeyRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ResidentKeyRequirement1">An ResidentKeyRequirement.</param>
        /// <param name="ResidentKeyRequirement2">Another ResidentKeyRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (ResidentKeyRequirement ResidentKeyRequirement1,
                                           ResidentKeyRequirement ResidentKeyRequirement2)

            => ResidentKeyRequirement1.CompareTo(ResidentKeyRequirement2) <= 0;

        #endregion

        #region Operator >  (ResidentKeyRequirement1, ResidentKeyRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ResidentKeyRequirement1">An ResidentKeyRequirement.</param>
        /// <param name="ResidentKeyRequirement2">Another ResidentKeyRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (ResidentKeyRequirement ResidentKeyRequirement1,
                                          ResidentKeyRequirement ResidentKeyRequirement2)

            => ResidentKeyRequirement1.CompareTo(ResidentKeyRequirement2) > 0;

        #endregion

        #region Operator >= (ResidentKeyRequirement1, ResidentKeyRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ResidentKeyRequirement1">An ResidentKeyRequirement.</param>
        /// <param name="ResidentKeyRequirement2">Another ResidentKeyRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (ResidentKeyRequirement ResidentKeyRequirement1,
                                           ResidentKeyRequirement ResidentKeyRequirement2)

            => ResidentKeyRequirement1.CompareTo(ResidentKeyRequirement2) >= 0;

        #endregion

        #endregion

        #region IComparable<ResidentKeyRequirement> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two ResidentKeyRequirements.
        /// </summary>
        /// <param name="Object">An ResidentKeyRequirement to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is ResidentKeyRequirement residentKeyRequirement
                   ? CompareTo(residentKeyRequirement)
                   : throw new ArgumentException("The given object is not an ResidentKeyRequirement!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(ResidentKeyRequirement)

        /// <summary>
        /// Compares two ResidentKeyRequirements.
        /// </summary>
        /// <param name="ResidentKeyRequirement">An ResidentKeyRequirement to compare with.</param>
        public Int32 CompareTo(ResidentKeyRequirement ResidentKeyRequirement)

            => String.Compare(InternalId,
                              ResidentKeyRequirement.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<ResidentKeyRequirement> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two ResidentKeyRequirements for equality.
        /// </summary>
        /// <param name="Object">An ResidentKeyRequirement to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is ResidentKeyRequirement residentKeyRequirement &&
                   Equals(residentKeyRequirement);

        #endregion

        #region Equals(ResidentKeyRequirement)

        /// <summary>
        /// Compares two ResidentKeyRequirements for equality.
        /// </summary>
        /// <param name="ResidentKeyRequirement">An ResidentKeyRequirement to compare with.</param>
        public Boolean Equals(ResidentKeyRequirement ResidentKeyRequirement)

            => String.Equals(InternalId,
                             ResidentKeyRequirement.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => InternalId?.ToLower().GetHashCode() ?? 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
