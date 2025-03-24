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

    // https://w3c.github.io/webauthn/#enumdef-publickeycredentialhint

    /// <summary>
    /// Extension methods for PublicKeyCredentialHints.
    /// </summary>
    public static class PublicKeyCredentialHintExtensions
    {

        /// <summary>
        /// Indicates whether this PublicKeyCredentialHint is null or empty.
        /// </summary>
        /// <param name="PublicKeyCredentialHint">A PublicKeyCredentialHint.</param>
        public static Boolean IsNullOrEmpty(this PublicKeyCredentialHint? PublicKeyCredentialHint)
            => !PublicKeyCredentialHint.HasValue || PublicKeyCredentialHint.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this PublicKeyCredentialHint is null or empty.
        /// </summary>
        /// <param name="PublicKeyCredentialHint">A PublicKeyCredentialHint.</param>
        public static Boolean IsNotNullOrEmpty(this PublicKeyCredentialHint? PublicKeyCredentialHint)
            => PublicKeyCredentialHint.HasValue && PublicKeyCredentialHint.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A PublicKeyCredentialHint.
    /// </summary>
    public readonly struct PublicKeyCredentialHint : IId,
                                                     IEquatable<PublicKeyCredentialHint>,
                                                     IComparable<PublicKeyCredentialHint>
    {

        #region Data

        private readonly static Dictionary<String, PublicKeyCredentialHint>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                       InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this PublicKeyCredentialHint is null or empty.
        /// </summary>
        public readonly  Boolean                    IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this PublicKeyCredentialHint is NOT null or empty.
        /// </summary>
        public readonly  Boolean                    IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the PublicKeyCredentialHint.
        /// </summary>
        public readonly  UInt64                     Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// All registered PublicKeyCredentialHints.
        /// </summary>
        public static    IEnumerable<PublicKeyCredentialHint>  All
            => lookup.Values;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new PublicKeyCredentialHint based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a PublicKeyCredentialHint.</param>
        private PublicKeyCredentialHint(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static PublicKeyCredentialHint Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new PublicKeyCredentialHint(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a PublicKeyCredentialHint.
        /// </summary>
        /// <param name="Text">A text representation of a PublicKeyCredentialHint.</param>
        public static PublicKeyCredentialHint Parse(String Text)
        {

            if (TryParse(Text, out var publicKeyCredentialHint))
                return publicKeyCredentialHint;

            throw new ArgumentException($"Invalid text representation of a PublicKeyCredentialHint: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as a PublicKeyCredentialHint.
        /// </summary>
        /// <param name="Text">A text representation of a PublicKeyCredentialHint.</param>
        public static PublicKeyCredentialHint? TryParse(String Text)
        {

            if (TryParse(Text, out var publicKeyCredentialHint))
                return publicKeyCredentialHint;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out PublicKeyCredentialHint)

        /// <summary>
        /// Try to parse the given text as a PublicKeyCredentialHint.
        /// </summary>
        /// <param name="Text">A text representation of a PublicKeyCredentialHint.</param>
        /// <param name="PublicKeyCredentialHint">The parsed PublicKeyCredentialHint.</param>
        public static Boolean TryParse(String Text, out PublicKeyCredentialHint PublicKeyCredentialHint)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out PublicKeyCredentialHint))
                    PublicKeyCredentialHint = Register(Text);

                return true;

            }

            PublicKeyCredentialHint = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this PublicKeyCredentialHint.
        /// </summary>
        public PublicKeyCredentialHint Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// security-key
        /// </summary>
        public static PublicKeyCredentialHint  SecurityKey     { get; }
            = Register("security-key");

        /// <summary>
        /// client-device
        /// </summary>
        public static PublicKeyCredentialHint  ClientDevice    { get; }
            = Register("client-device");

        /// <summary>
        /// hybrid
        /// </summary>
        public static PublicKeyCredentialHint  Hybrid          { get; }
            = Register("hybrid");

        #endregion


        #region Operator overloading

        #region Operator == (PublicKeyCredentialHint1, PublicKeyCredentialHint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialHint1">A PublicKeyCredentialHint.</param>
        /// <param name="PublicKeyCredentialHint2">Another PublicKeyCredentialHint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (PublicKeyCredentialHint PublicKeyCredentialHint1,
                                           PublicKeyCredentialHint PublicKeyCredentialHint2)

            => PublicKeyCredentialHint1.Equals(PublicKeyCredentialHint2);

        #endregion

        #region Operator != (PublicKeyCredentialHint1, PublicKeyCredentialHint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialHint1">A PublicKeyCredentialHint.</param>
        /// <param name="PublicKeyCredentialHint2">Another PublicKeyCredentialHint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (PublicKeyCredentialHint PublicKeyCredentialHint1,
                                           PublicKeyCredentialHint PublicKeyCredentialHint2)

            => !PublicKeyCredentialHint1.Equals(PublicKeyCredentialHint2);

        #endregion

        #region Operator <  (PublicKeyCredentialHint1, PublicKeyCredentialHint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialHint1">A PublicKeyCredentialHint.</param>
        /// <param name="PublicKeyCredentialHint2">Another PublicKeyCredentialHint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (PublicKeyCredentialHint PublicKeyCredentialHint1,
                                          PublicKeyCredentialHint PublicKeyCredentialHint2)

            => PublicKeyCredentialHint1.CompareTo(PublicKeyCredentialHint2) < 0;

        #endregion

        #region Operator <= (PublicKeyCredentialHint1, PublicKeyCredentialHint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialHint1">A PublicKeyCredentialHint.</param>
        /// <param name="PublicKeyCredentialHint2">Another PublicKeyCredentialHint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (PublicKeyCredentialHint PublicKeyCredentialHint1,
                                           PublicKeyCredentialHint PublicKeyCredentialHint2)

            => PublicKeyCredentialHint1.CompareTo(PublicKeyCredentialHint2) <= 0;

        #endregion

        #region Operator >  (PublicKeyCredentialHint1, PublicKeyCredentialHint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialHint1">A PublicKeyCredentialHint.</param>
        /// <param name="PublicKeyCredentialHint2">Another PublicKeyCredentialHint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (PublicKeyCredentialHint PublicKeyCredentialHint1,
                                          PublicKeyCredentialHint PublicKeyCredentialHint2)

            => PublicKeyCredentialHint1.CompareTo(PublicKeyCredentialHint2) > 0;

        #endregion

        #region Operator >= (PublicKeyCredentialHint1, PublicKeyCredentialHint2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PublicKeyCredentialHint1">A PublicKeyCredentialHint.</param>
        /// <param name="PublicKeyCredentialHint2">Another PublicKeyCredentialHint.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (PublicKeyCredentialHint PublicKeyCredentialHint1,
                                           PublicKeyCredentialHint PublicKeyCredentialHint2)

            => PublicKeyCredentialHint1.CompareTo(PublicKeyCredentialHint2) >= 0;

        #endregion

        #endregion

        #region IComparable<PublicKeyCredentialHint> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two PublicKeyCredentialHints.
        /// </summary>
        /// <param name="Object">A PublicKeyCredentialHint to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is PublicKeyCredentialHint publicKeyCredentialHint
                   ? CompareTo(publicKeyCredentialHint)
                   : throw new ArgumentException("The given object is not a PublicKeyCredentialHint!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(PublicKeyCredentialHint)

        /// <summary>
        /// Compares two PublicKeyCredentialHints.
        /// </summary>
        /// <param name="PublicKeyCredentialHint">A PublicKeyCredentialHint to compare with.</param>
        public Int32 CompareTo(PublicKeyCredentialHint PublicKeyCredentialHint)

            => String.Compare(InternalId,
                              PublicKeyCredentialHint.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<PublicKeyCredentialHint> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two PublicKeyCredentialHints for equality.
        /// </summary>
        /// <param name="Object">A PublicKeyCredentialHint to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is PublicKeyCredentialHint publicKeyCredentialHint &&
                   Equals(publicKeyCredentialHint);

        #endregion

        #region Equals(PublicKeyCredentialHint)

        /// <summary>
        /// Compares two PublicKeyCredentialHints for equality.
        /// </summary>
        /// <param name="PublicKeyCredentialHint">A PublicKeyCredentialHint to compare with.</param>
        public Boolean Equals(PublicKeyCredentialHint PublicKeyCredentialHint)

            => String.Equals(InternalId,
                             PublicKeyCredentialHint.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
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
