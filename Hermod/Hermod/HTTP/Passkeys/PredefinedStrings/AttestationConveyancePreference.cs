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

    // https://w3c.github.io/webauthn/#enumdef-attestationconveyancepreference

    /// <summary>
    /// Extension methods for AttestationConveyancePreferences.
    /// </summary>
    public static class AttestationConveyancePreferenceExtensions
    {

        /// <summary>
        /// Indicates whether this AttestationConveyancePreference is null or empty.
        /// </summary>
        /// <param name="AttestationConveyancePreference">An AttestationConveyancePreference.</param>
        public static Boolean IsNullOrEmpty(this AttestationConveyancePreference? AttestationConveyancePreference)
            => !AttestationConveyancePreference.HasValue || AttestationConveyancePreference.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this AttestationConveyancePreference is null or empty.
        /// </summary>
        /// <param name="AttestationConveyancePreference">An AttestationConveyancePreference.</param>
        public static Boolean IsNotNullOrEmpty(this AttestationConveyancePreference? AttestationConveyancePreference)
            => AttestationConveyancePreference.HasValue && AttestationConveyancePreference.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// An AttestationConveyancePreference.
    /// </summary>
    public readonly struct AttestationConveyancePreference : IId,
                                                     IEquatable<AttestationConveyancePreference>,
                                                     IComparable<AttestationConveyancePreference>
    {

        #region Data

        private readonly static Dictionary<String, AttestationConveyancePreference>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                       InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this AttestationConveyancePreference is null or empty.
        /// </summary>
        public readonly  Boolean                    IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this AttestationConveyancePreference is NOT null or empty.
        /// </summary>
        public readonly  Boolean                    IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the AttestationConveyancePreference.
        /// </summary>
        public readonly  UInt64                     Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// All registered AttestationConveyancePreferences.
        /// </summary>
        public static    IEnumerable<AttestationConveyancePreference>  All
            => lookup.Values;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new AttestationConveyancePreference based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of an AttestationConveyancePreference.</param>
        private AttestationConveyancePreference(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static AttestationConveyancePreference Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new AttestationConveyancePreference(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as an AttestationConveyancePreference.
        /// </summary>
        /// <param name="Text">A text representation of an AttestationConveyancePreference.</param>
        public static AttestationConveyancePreference Parse(String Text)
        {

            if (TryParse(Text, out var attestationConveyance))
                return attestationConveyance;

            throw new ArgumentException($"Invalid text representation of an AttestationConveyancePreference: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as an AttestationConveyancePreference.
        /// </summary>
        /// <param name="Text">A text representation of an AttestationConveyancePreference.</param>
        public static AttestationConveyancePreference? TryParse(String Text)
        {

            if (TryParse(Text, out var attestationConveyance))
                return attestationConveyance;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out AttestationConveyancePreference)

        /// <summary>
        /// Try to parse the given text as an AttestationConveyancePreference.
        /// </summary>
        /// <param name="Text">A text representation of an AttestationConveyancePreference.</param>
        /// <param name="AttestationConveyancePreference">The parsed AttestationConveyancePreference.</param>
        public static Boolean TryParse(String Text, out AttestationConveyancePreference AttestationConveyancePreference)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out AttestationConveyancePreference))
                    AttestationConveyancePreference = Register(Text);

                return true;

            }

            AttestationConveyancePreference = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this AttestationConveyancePreference.
        /// </summary>
        public AttestationConveyancePreference Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// None
        /// </summary>
        public static AttestationConveyancePreference  None          { get; }
            = Register("none");

        /// <summary>
        /// Indirect
        /// </summary>
        public static AttestationConveyancePreference  Indirect      { get; }
            = Register("indirect");

        /// <summary>
        /// Direct
        /// </summary>
        public static AttestationConveyancePreference  Direct        { get; }
            = Register("direct");

        /// <summary>
        /// Enterprise
        /// </summary>
        public static AttestationConveyancePreference  Enterprise    { get; }
            = Register("enterprise");

        #endregion


        #region Operator overloading

        #region Operator == (AttestationConveyancePreference1, AttestationConveyancePreference2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttestationConveyancePreference1">An AttestationConveyancePreference.</param>
        /// <param name="AttestationConveyancePreference2">Another AttestationConveyancePreference.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (AttestationConveyancePreference AttestationConveyancePreference1,
                                           AttestationConveyancePreference AttestationConveyancePreference2)

            => AttestationConveyancePreference1.Equals(AttestationConveyancePreference2);

        #endregion

        #region Operator != (AttestationConveyancePreference1, AttestationConveyancePreference2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttestationConveyancePreference1">An AttestationConveyancePreference.</param>
        /// <param name="AttestationConveyancePreference2">Another AttestationConveyancePreference.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (AttestationConveyancePreference AttestationConveyancePreference1,
                                           AttestationConveyancePreference AttestationConveyancePreference2)

            => !AttestationConveyancePreference1.Equals(AttestationConveyancePreference2);

        #endregion

        #region Operator <  (AttestationConveyancePreference1, AttestationConveyancePreference2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttestationConveyancePreference1">An AttestationConveyancePreference.</param>
        /// <param name="AttestationConveyancePreference2">Another AttestationConveyancePreference.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (AttestationConveyancePreference AttestationConveyancePreference1,
                                          AttestationConveyancePreference AttestationConveyancePreference2)

            => AttestationConveyancePreference1.CompareTo(AttestationConveyancePreference2) < 0;

        #endregion

        #region Operator <= (AttestationConveyancePreference1, AttestationConveyancePreference2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttestationConveyancePreference1">An AttestationConveyancePreference.</param>
        /// <param name="AttestationConveyancePreference2">Another AttestationConveyancePreference.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (AttestationConveyancePreference AttestationConveyancePreference1,
                                           AttestationConveyancePreference AttestationConveyancePreference2)

            => AttestationConveyancePreference1.CompareTo(AttestationConveyancePreference2) <= 0;

        #endregion

        #region Operator >  (AttestationConveyancePreference1, AttestationConveyancePreference2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttestationConveyancePreference1">An AttestationConveyancePreference.</param>
        /// <param name="AttestationConveyancePreference2">Another AttestationConveyancePreference.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (AttestationConveyancePreference AttestationConveyancePreference1,
                                          AttestationConveyancePreference AttestationConveyancePreference2)

            => AttestationConveyancePreference1.CompareTo(AttestationConveyancePreference2) > 0;

        #endregion

        #region Operator >= (AttestationConveyancePreference1, AttestationConveyancePreference2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttestationConveyancePreference1">An AttestationConveyancePreference.</param>
        /// <param name="AttestationConveyancePreference2">Another AttestationConveyancePreference.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (AttestationConveyancePreference AttestationConveyancePreference1,
                                           AttestationConveyancePreference AttestationConveyancePreference2)

            => AttestationConveyancePreference1.CompareTo(AttestationConveyancePreference2) >= 0;

        #endregion

        #endregion

        #region IComparable<AttestationConveyancePreference> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two AttestationConveyancePreferences.
        /// </summary>
        /// <param name="Object">An AttestationConveyancePreference to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is AttestationConveyancePreference attestationConveyance
                   ? CompareTo(attestationConveyance)
                   : throw new ArgumentException("The given object is not an AttestationConveyancePreference!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(AttestationConveyancePreference)

        /// <summary>
        /// Compares two AttestationConveyancePreferences.
        /// </summary>
        /// <param name="AttestationConveyancePreference">An AttestationConveyancePreference to compare with.</param>
        public Int32 CompareTo(AttestationConveyancePreference AttestationConveyancePreference)

            => String.Compare(InternalId,
                              AttestationConveyancePreference.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<AttestationConveyancePreference> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two AttestationConveyancePreferences for equality.
        /// </summary>
        /// <param name="Object">An AttestationConveyancePreference to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is AttestationConveyancePreference attestationConveyance &&
                   Equals(attestationConveyance);

        #endregion

        #region Equals(AttestationConveyancePreference)

        /// <summary>
        /// Compares two AttestationConveyancePreferences for equality.
        /// </summary>
        /// <param name="AttestationConveyancePreference">An AttestationConveyancePreference to compare with.</param>
        public Boolean Equals(AttestationConveyancePreference AttestationConveyancePreference)

            => String.Equals(InternalId,
                             AttestationConveyancePreference.InternalId,
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
