/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for security tokens.
    /// </summary>
    public static class SecurityTokenIdExtensions
    {

        /// <summary>
        /// Indicates whether this security token is null or empty.
        /// </summary>
        /// <param name="SecurityTokenId">A security token.</param>
        public static Boolean IsNullOrEmpty(this SecurityToken_Id? SecurityTokenId)
            => !SecurityTokenId.HasValue || SecurityTokenId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this security token is null or empty.
        /// </summary>
        /// <param name="SecurityTokenId">A security token.</param>
        public static Boolean IsNotNullOrEmpty(this SecurityToken_Id? SecurityTokenId)
            => SecurityTokenId.HasValue && SecurityTokenId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of a security token.
    /// </summary>
    public readonly struct SecurityToken_Id : IId,
                                              IEquatable<SecurityToken_Id>,
                                              IComparable<SecurityToken_Id>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String  InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the security token identification.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique security token identification based on the given text representation.
        /// </summary>
        /// <param name="String">The text representation of the security token identification.</param>
        private SecurityToken_Id(String String)
        {
            this.InternalId = String;
        }

        #endregion


        #region (static) Random  (Length = 40)

        /// <summary>
        /// Generate a new random security token identification.
        /// </summary>
        /// <param name="Length">The expected length of the random string.</param>
        public static SecurityToken_Id Random(UInt16 Length   = 40)

            => new (RandomExtensions.RandomString(Length));

        #endregion

        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text representation of a security token identification.
        /// </summary>
        /// <param name="Text">A text representation of a security token identification.</param>
        public static SecurityToken_Id Parse(String Text)
        {

            if (TryParse(Text, out SecurityToken_Id securityTokenId))
                return securityTokenId;

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a security token identification must not be null or empty!");

            throw new ArgumentException("The given text representation of a security token identification is invalid!", nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text representation of a security token identification.
        /// </summary>
        /// <param name="Text">A text representation of a security token identification.</param>
        public static SecurityToken_Id? TryParse(String Text)
        {

            if (TryParse(Text, out SecurityToken_Id securityTokenId))
                return securityTokenId;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out SecurityTokenId)

        /// <summary>
        /// Try to parse the given text representation of a security token identification.
        /// </summary>
        /// <param name="Text">A text representation of a security token identification.</param>
        /// <param name="SecurityTokenId">The parsed security token identification.</param>
        public static Boolean TryParse(String Text, out SecurityToken_Id SecurityTokenId)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    SecurityTokenId = new SecurityToken_Id(Text);
                    return true;
                }
                catch
                { }
            }

            SecurityTokenId = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this security token identification.
        /// </summary>
        public SecurityToken_Id Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (SecurityTokenId1, SecurityTokenId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SecurityTokenId1">A security token identification.</param>
        /// <param name="SecurityTokenId2">Another security token identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (SecurityToken_Id SecurityTokenId1,
                                           SecurityToken_Id SecurityTokenId2)

            => SecurityTokenId1.Equals(SecurityTokenId2);

        #endregion

        #region Operator != (SecurityTokenId1, SecurityTokenId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SecurityTokenId1">A security token identification.</param>
        /// <param name="SecurityTokenId2">Another security token identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (SecurityToken_Id SecurityTokenId1,
                                           SecurityToken_Id SecurityTokenId2)

            => !SecurityTokenId1.Equals(SecurityTokenId2);

        #endregion

        #region Operator <  (SecurityTokenId1, SecurityTokenId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SecurityTokenId1">A security token identification.</param>
        /// <param name="SecurityTokenId2">Another security token identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (SecurityToken_Id SecurityTokenId1,
                                          SecurityToken_Id SecurityTokenId2)

            => SecurityTokenId1.CompareTo(SecurityTokenId2) < 0;

        #endregion

        #region Operator <= (SecurityTokenId1, SecurityTokenId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SecurityTokenId1">A security token identification.</param>
        /// <param name="SecurityTokenId2">Another security token identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (SecurityToken_Id SecurityTokenId1,
                                           SecurityToken_Id SecurityTokenId2)

            => SecurityTokenId1.CompareTo(SecurityTokenId2) <= 0;

        #endregion

        #region Operator >  (SecurityTokenId1, SecurityTokenId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SecurityTokenId1">A security token identification.</param>
        /// <param name="SecurityTokenId2">Another security token identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (SecurityToken_Id SecurityTokenId1,
                                          SecurityToken_Id SecurityTokenId2)

            => SecurityTokenId1.CompareTo(SecurityTokenId2) > 0;

        #endregion

        #region Operator >= (SecurityTokenId1, SecurityTokenId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SecurityTokenId1">A security token identification.</param>
        /// <param name="SecurityTokenId2">Another security token identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (SecurityToken_Id SecurityTokenId1,
                                           SecurityToken_Id SecurityTokenId2)

            => SecurityTokenId1.CompareTo(SecurityTokenId2) >= 0;

        #endregion

        #endregion

        #region IComparable<SecurityToken_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is SecurityToken_Id securityTokenId
                   ? CompareTo(securityTokenId)
                   : throw new ArgumentException("The given object is not a security token identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(SecurityToken_Id)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SecurityToken_Id">An object to compare with.</param>
        public Int32 CompareTo(SecurityToken_Id SecurityToken_Id)

            => String.Compare(InternalId,
                              SecurityToken_Id.InternalId,
                              StringComparison.Ordinal);

        #endregion

        #endregion

        #region IEquatable<SecurityToken_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is SecurityToken_Id securityTokenId &&
                   Equals(securityTokenId);

        #endregion

        #region Equals(SecurityToken_Id)

        /// <summary>
        /// Compares two security token identifications for equality.
        /// </summary>
        /// <param name="SecurityToken_Id">An security token identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(SecurityToken_Id SecurityToken_Id)

            => String.Equals(InternalId,
                             SecurityToken_Id.InternalId,
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => InternalId?.GetHashCode() ?? 0;

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
