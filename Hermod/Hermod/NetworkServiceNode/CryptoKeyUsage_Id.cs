/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for crypto key usage identifications.
    /// </summary>
    public static class ServiceCryptoKeyUsageExtensions
    {

        /// <summary>
        /// Indicates whether this crypto key usage identification is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key usage identification.</param>
        public static Boolean IsNullOrEmpty(this CryptoKeyUsage_Id? CryptoKeyUsage)
            => !CryptoKeyUsage.HasValue || CryptoKeyUsage.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this crypto key usage identification is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key usage identification.</param>
        public static Boolean IsNotNullOrEmpty(this CryptoKeyUsage_Id? CryptoKeyUsage)
            => CryptoKeyUsage.HasValue && CryptoKeyUsage.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of a crypto key usage.
    /// </summary>
    public readonly struct CryptoKeyUsage_Id : IId,
                                               IEquatable <CryptoKeyUsage_Id>,
                                               IComparable<CryptoKeyUsage_Id>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

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
        /// The length of the node identificator.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique crypto key usage identification based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a crypto key usage identification.</param>
        private CryptoKeyUsage_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a crypto key usage identification.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key usage identification.</param>
        public static CryptoKeyUsage_Id Parse(String Text)
        {

            if (TryParse(Text, out var networkServiceCryptoKeyUsage))
                return networkServiceCryptoKeyUsage;

            throw new ArgumentException($"Invalid text representation of a crypto key usage identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a crypto key usage identification.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key usage identification.</param>
        public static CryptoKeyUsage_Id? TryParse(String Text)
        {

            if (TryParse(Text, out var networkServiceCryptoKeyUsage))
                return networkServiceCryptoKeyUsage;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out CryptoKeyUsage)

        /// <summary>
        /// Parse the given string as a crypto key usage identification.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key usage identification.</param>
        /// <param name="CryptoKeyUsage">The parsed crypto key usage identification.</param>
        public static Boolean TryParse(String Text, out CryptoKeyUsage_Id CryptoKeyUsage)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    CryptoKeyUsage = new CryptoKeyUsage_Id(Text);
                    return true;
                }
                catch
                { }
            }

            CryptoKeyUsage = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this crypto key usage identification.
        /// </summary>
        public CryptoKeyUsage_Id Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// Identity
        /// </summary>
        public static CryptoKeyUsage_Id Identity
            => new ("https://open.charging.cloud/contexts/crypto/keyUsages/identity");

        /// <summary>
        /// Identity Group (Membership)
        /// </summary>
        public static CryptoKeyUsage_Id IdentityGroup
            => new ("https://open.charging.cloud/contexts/crypto/keyUsages/identityGroup");

        /// <summary>
        /// Encryption
        /// </summary>
        public static CryptoKeyUsage_Id Encryption
            => new("https://open.charging.cloud/contexts/crypto/keyUsages/encryption");

        /// <summary>
        /// Signature
        /// </summary>
        public static CryptoKeyUsage_Id Signature
            => new("https://open.charging.cloud/contexts/crypto/keyUsages/signature");

        #endregion


        #region Operator overloading

        #region Operator == (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usage identifications for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage identification.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (CryptoKeyUsage_Id CryptoKeyUsage1,
                                           CryptoKeyUsage_Id CryptoKeyUsage2)

            => CryptoKeyUsage1.Equals(CryptoKeyUsage2);

        #endregion

        #region Operator != (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usage identifications for inequality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage identification.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (CryptoKeyUsage_Id CryptoKeyUsage1,
                                           CryptoKeyUsage_Id CryptoKeyUsage2)

            => !CryptoKeyUsage1.Equals(CryptoKeyUsage2);

        #endregion

        #region Operator <  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usage identifications.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage identification.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (CryptoKeyUsage_Id CryptoKeyUsage1,
                                          CryptoKeyUsage_Id CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) < 0;

        #endregion

        #region Operator <= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usage identifications.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage identification.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (CryptoKeyUsage_Id CryptoKeyUsage1,
                                           CryptoKeyUsage_Id CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) <= 0;

        #endregion

        #region Operator >  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usage identifications.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage identification.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (CryptoKeyUsage_Id CryptoKeyUsage1,
                                          CryptoKeyUsage_Id CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) > 0;

        #endregion

        #region Operator >= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usage identifications.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage identification.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (CryptoKeyUsage_Id CryptoKeyUsage1,
                                           CryptoKeyUsage_Id CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) >= 0;

        #endregion

        #endregion

        #region IComparable<Node_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two crypto key usage identifications.
        /// </summary>
        /// <param name="Object">A crypto key usage identification to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is CryptoKeyUsage_Id networkServiceCryptoKeyUsage
                   ? CompareTo(networkServiceCryptoKeyUsage)
                   : throw new ArgumentException("The given object is not a crypto key usage identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto key usage identifications.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key usage identification to compare with.</param>
        public Int32 CompareTo(CryptoKeyUsage_Id CryptoKeyUsage)

            => String.Compare(InternalId,
                              CryptoKeyUsage.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<Node_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two crypto key usage identifications for equality.
        /// </summary>
        /// <param name="Object">A crypto key usage identification to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is CryptoKeyUsage_Id networkServiceCryptoKeyUsage &&
                   Equals(networkServiceCryptoKeyUsage);

        #endregion

        #region Equals(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto key usage identifications for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key usage identification to compare with.</param>
        public Boolean Equals(CryptoKeyUsage_Id CryptoKeyUsage)

            => String.Equals(InternalId,
                             CryptoKeyUsage.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region GetHashCode()

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
