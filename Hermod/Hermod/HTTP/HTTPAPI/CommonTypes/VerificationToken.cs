/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of UsersAPI <https://www.github.com/Vanaheimr/UsersAPI>
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

using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A verification token.
    /// </summary>
    public readonly struct VerificationToken : IEquatable<VerificationToken>,
                                               IComparable<VerificationToken>,
                                               IComparable
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String  InternalId;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new verification token.
        /// </summary>
        /// <param name="Seed">The cryptographic seed.</param>
        public VerificationToken(String Seed)
        {
            this.InternalId  = SHA256.HashData((RandomExtensions.RandomString(32) + Seed).ToUTF8Bytes()).ToHexString();
        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as a verification token.
        /// </summary>
        /// <param name="Text">A text representation of a verification token.</param>
        public static VerificationToken Parse(String Text)
        {

            if (TryParse(Text, out VerificationToken verificationToken))
                return verificationToken;

            throw new ArgumentException($"Invalid text representation of a verification token: '" + Text + "'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a verification token.
        /// </summary>
        /// <param name="Text">A text representation of a verification token.</param>
        public static VerificationToken? TryParse(String Text)
        {

            if (TryParse(Text, out VerificationToken verificationToken))
                return verificationToken;

            return null;

        }

        #endregion

        #region TryParse(Text, out VerificationToken)

        /// <summary>
        /// Try to parse the given string as a verification token.
        /// </summary>
        /// <param name="Text">A text representation of a verification token.</param>
        /// <param name="VerificationToken">The parsed verification token.</param>
        public static Boolean TryParse(String Text, out VerificationToken VerificationToken)
        {

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    VerificationToken = new VerificationToken(Text);
                    return true;
                }
                catch
                { }
            }

            VerificationToken = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this verification token.
        /// </summary>
        public VerificationToken Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (VerificationToken1, VerificationToken2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VerificationToken1">A verification token.</param>
        /// <param name="VerificationToken2">Another verification token.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (VerificationToken VerificationToken1,
                                           VerificationToken VerificationToken2)

            => VerificationToken1.Equals(VerificationToken2);

        #endregion

        #region Operator != (VerificationToken1, VerificationToken2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VerificationToken1">A verification token.</param>
        /// <param name="VerificationToken2">Another verification token.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (VerificationToken VerificationToken1,
                                           VerificationToken VerificationToken2)

            => !VerificationToken1.Equals(VerificationToken2);

        #endregion

        #region Operator <  (VerificationToken1, VerificationToken2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VerificationToken1">A verification token.</param>
        /// <param name="VerificationToken2">Another verification token.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (VerificationToken VerificationToken1,
                                          VerificationToken VerificationToken2)

            => VerificationToken1.CompareTo(VerificationToken2) < 0;

        #endregion

        #region Operator <= (VerificationToken1, VerificationToken2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VerificationToken1">A verification token.</param>
        /// <param name="VerificationToken2">Another verification token.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (VerificationToken VerificationToken1,
                                           VerificationToken VerificationToken2)

            => VerificationToken1.CompareTo(VerificationToken2) <= 0;

        #endregion

        #region Operator >  (VerificationToken1, VerificationToken2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VerificationToken1">A verification token.</param>
        /// <param name="VerificationToken2">Another verification token.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (VerificationToken VerificationToken1,
                                          VerificationToken VerificationToken2)

            => VerificationToken1.CompareTo(VerificationToken2) > 0;

        #endregion

        #region Operator >= (VerificationToken1, VerificationToken2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VerificationToken1">A verification token.</param>
        /// <param name="VerificationToken2">Another verification token.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (VerificationToken VerificationToken1,
                                           VerificationToken VerificationToken2)

            => VerificationToken1.CompareTo(VerificationToken2) >= 0;

        #endregion

        #endregion

        #region IComparable<VerificationToken> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is VerificationToken verificationToken
                   ? CompareTo(verificationToken)
                   : throw new ArgumentException("The given object is not a verification token!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(VerificationToken)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="VerificationToken">An object to compare with.</param>
        public Int32 CompareTo(VerificationToken VerificationToken)

            => String.Compare(InternalId,
                              VerificationToken.InternalId,
                              StringComparison.Ordinal);

        #endregion

        #endregion

        #region IEquatable<VerificationToken> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is VerificationToken verificationToken &&
                   Equals(verificationToken);

        #endregion

        #region Equals(VerificationToken)

        /// <summary>
        /// Compares two verification tokens for equality.
        /// </summary>
        /// <param name="VerificationToken">A verification token to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(VerificationToken VerificationToken)

            => String.Equals(InternalId,
                             VerificationToken.InternalId,
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
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
