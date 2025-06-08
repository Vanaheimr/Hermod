/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

    // https://w3c.github.io/webauthn/#enumdef-publickeycredentialtype

    /// <summary>
    /// Extension methods for PublicKeyCredentialTypes.
    /// </summary>
    public static class PublicKeyCredentialTypeExtensions
    {

        /// <summary>
        /// Indicates whether this PublicKeyCredentialType is null or empty.
        /// </summary>
        /// <param name="PublicKeyCredentialType">A PublicKeyCredentialType.</param>
        public static Boolean IsNullOrEmpty(this PublicKeyCredentialType? PublicKeyCredentialType)
            => !PublicKeyCredentialType.HasValue || PublicKeyCredentialType.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this PublicKeyCredentialType is null or empty.
        /// </summary>
        /// <param name="PublicKeyCredentialType">A PublicKeyCredentialType.</param>
        public static Boolean IsNotNullOrEmpty(this PublicKeyCredentialType? PublicKeyCredentialType)
            => PublicKeyCredentialType.HasValue && PublicKeyCredentialType.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A PublicKeyCredentialType.
    /// </summary>
    public readonly struct PublicKeyCredentialType : IId,
                                                     IEquatable<PublicKeyCredentialType>,
                                                     IComparable<PublicKeyCredentialType>
    {

        #region Data

        private readonly static Dictionary<String, PublicKeyCredentialType>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                       InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this PublicKeyCredentialType is null or empty.
        /// </summary>
        public readonly  Boolean                    IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this PublicKeyCredentialType is NOT null or empty.
        /// </summary>
        public readonly  Boolean                    IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the PublicKeyCredentialType.
        /// </summary>
        public readonly  UInt64                     Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// All registered PublicKeyCredentialTypes.
        /// </summary>
        public static    IEnumerable<PublicKeyCredentialType>  All
            => lookup.Values;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new PublicKeyCredentialType based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a PublicKeyCredentialType.</param>
        private PublicKeyCredentialType(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static PublicKeyCredentialType Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new PublicKeyCredentialType(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a PublicKeyCredentialType.
        /// </summary>
        /// <param name="Text">A text representation of a PublicKeyCredentialType.</param>
        public static PublicKeyCredentialType Parse(String Text)
        {

            if (TryParse(Text, out var publicKeyCredentialType))
                return publicKeyCredentialType;

            throw new ArgumentException($"Invalid text representation of a PublicKeyCredentialType: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a PublicKeyCredentialType.
        /// </summary>
        /// <param name="Text">A text representation of a PublicKeyCredentialType.</param>
        public static PublicKeyCredentialType? TryParse(String Text)
        {

            if (TryParse(Text, out var publicKeyCredentialType))
                return publicKeyCredentialType;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out PublicKeyCredentialType)

        /// <summary>
        /// Try to parse the given text as a PublicKeyCredentialType.
        /// </summary>
        /// <param name="Text">A text representation of a PublicKeyCredentialType.</param>
        /// <param name="PublicKeyCredentialType">The parsed PublicKeyCredentialType.</param>
        public static Boolean TryParse(String Text, out PublicKeyCredentialType PublicKeyCredentialType)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out PublicKeyCredentialType))
                    PublicKeyCredentialType = Register(Text);

                return true;

            }

            PublicKeyCredentialType = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this PublicKeyCredentialType.
        /// </summary>
        public PublicKeyCredentialType Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// public-key
        /// </summary>
        public static PublicKeyCredentialType  PublicKey    { get; }
            = Register("public-key");

        #endregion


        #region Operator overloading

        #region Operator == (PublicKeyCredentialType1, PublicKeyCredentialType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialType1">A PublicKeyCredentialType.</param>
        /// <param name="PublicKeyCredentialType2">Another PublicKeyCredentialType.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (PublicKeyCredentialType PublicKeyCredentialType1,
                                           PublicKeyCredentialType PublicKeyCredentialType2)

            => PublicKeyCredentialType1.Equals(PublicKeyCredentialType2);

        #endregion

        #region Operator != (PublicKeyCredentialType1, PublicKeyCredentialType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialType1">A PublicKeyCredentialType.</param>
        /// <param name="PublicKeyCredentialType2">Another PublicKeyCredentialType.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (PublicKeyCredentialType PublicKeyCredentialType1,
                                           PublicKeyCredentialType PublicKeyCredentialType2)

            => !PublicKeyCredentialType1.Equals(PublicKeyCredentialType2);

        #endregion

        #region Operator <  (PublicKeyCredentialType1, PublicKeyCredentialType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialType1">A PublicKeyCredentialType.</param>
        /// <param name="PublicKeyCredentialType2">Another PublicKeyCredentialType.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (PublicKeyCredentialType PublicKeyCredentialType1,
                                          PublicKeyCredentialType PublicKeyCredentialType2)

            => PublicKeyCredentialType1.CompareTo(PublicKeyCredentialType2) < 0;

        #endregion

        #region Operator <= (PublicKeyCredentialType1, PublicKeyCredentialType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialType1">A PublicKeyCredentialType.</param>
        /// <param name="PublicKeyCredentialType2">Another PublicKeyCredentialType.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (PublicKeyCredentialType PublicKeyCredentialType1,
                                           PublicKeyCredentialType PublicKeyCredentialType2)

            => PublicKeyCredentialType1.CompareTo(PublicKeyCredentialType2) <= 0;

        #endregion

        #region Operator >  (PublicKeyCredentialType1, PublicKeyCredentialType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialType1">A PublicKeyCredentialType.</param>
        /// <param name="PublicKeyCredentialType2">Another PublicKeyCredentialType.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (PublicKeyCredentialType PublicKeyCredentialType1,
                                          PublicKeyCredentialType PublicKeyCredentialType2)

            => PublicKeyCredentialType1.CompareTo(PublicKeyCredentialType2) > 0;

        #endregion

        #region Operator >= (PublicKeyCredentialType1, PublicKeyCredentialType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialType1">A PublicKeyCredentialType.</param>
        /// <param name="PublicKeyCredentialType2">Another PublicKeyCredentialType.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (PublicKeyCredentialType PublicKeyCredentialType1,
                                           PublicKeyCredentialType PublicKeyCredentialType2)

            => PublicKeyCredentialType1.CompareTo(PublicKeyCredentialType2) >= 0;

        #endregion

        #endregion

        #region IComparable<PublicKeyCredentialType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two PublicKeyCredentialTypes.
        /// </summary>
        /// <param name="Object">A PublicKeyCredentialType to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is PublicKeyCredentialType publicKeyCredentialType
                   ? CompareTo(publicKeyCredentialType)
                   : throw new ArgumentException("The given object is not a PublicKeyCredentialType!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(PublicKeyCredentialType)

        /// <summary>
        /// Compares two PublicKeyCredentialTypes.
        /// </summary>
        /// <param name="PublicKeyCredentialType">A PublicKeyCredentialType to compare with.</param>
        public Int32 CompareTo(PublicKeyCredentialType PublicKeyCredentialType)

            => String.Compare(InternalId,
                              PublicKeyCredentialType.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<PublicKeyCredentialType> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two PublicKeyCredentialTypes for equality.
        /// </summary>
        /// <param name="Object">A PublicKeyCredentialType to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is PublicKeyCredentialType publicKeyCredentialType &&
                   Equals(publicKeyCredentialType);

        #endregion

        #region Equals(PublicKeyCredentialType)

        /// <summary>
        /// Compares two PublicKeyCredentialTypes for equality.
        /// </summary>
        /// <param name="PublicKeyCredentialType">A PublicKeyCredentialType to compare with.</param>
        public Boolean Equals(PublicKeyCredentialType PublicKeyCredentialType)

            => String.Equals(InternalId,
                             PublicKeyCredentialType.InternalId,
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
