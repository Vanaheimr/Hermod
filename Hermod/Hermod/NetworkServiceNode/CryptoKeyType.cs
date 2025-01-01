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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for crypto key types.
    /// </summary>
    public static class CryptoKeyTypeExtensions
    {

        /// <summary>
        /// Indicates whether this crypto key type is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key type.</param>
        public static Boolean IsNullOrEmpty(this CryptoKeyType? CryptoKeyUsage)
            => !CryptoKeyUsage.HasValue || CryptoKeyUsage.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this crypto key type is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key type.</param>
        public static Boolean IsNotNullOrEmpty(this CryptoKeyType? CryptoKeyUsage)
            => CryptoKeyUsage.HasValue && CryptoKeyUsage.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A unique crypto key type.
    /// </summary>
    public readonly struct CryptoKeyType : IId,
                                           IEquatable <CryptoKeyType>,
                                           IComparable<CryptoKeyType>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

        /// <summary>
        /// The JSON-LD context of the object.
        /// </summary>
        public const String JSONLDContext = "https://open.charging.cloud/contexts/crypto/keyTypes";

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
        /// Create a new unique crypto key type based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a crypto key type.</param>
        private CryptoKeyType(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a crypto key type.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key type.</param>
        public static CryptoKeyType Parse(String Text)
        {

            if (TryParse(Text, out var cryptoKeyType))
                return cryptoKeyType;

            throw new ArgumentException($"Invalid text representation of a crypto key type: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a crypto key type.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key type.</param>
        public static CryptoKeyType? TryParse(String Text)
        {

            if (TryParse(Text, out var cryptoKeyType))
                return cryptoKeyType;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out CryptoKeyUsage)

        /// <summary>
        /// Parse the given string as a crypto key type.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key type.</param>
        /// <param name="CryptoKeyUsage">The parsed crypto key type.</param>
        public static Boolean TryParse(String Text, out CryptoKeyType CryptoKeyUsage)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    CryptoKeyUsage = new CryptoKeyType(Text);
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
        /// Clone this crypto key type.
        /// </summary>
        public CryptoKeyType Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// secp256r1 / P‐256 (OID: 1.2.840.10045.3.1.7)
        /// </summary>
        public static CryptoKeyType SecP256r1
            => new($"{JSONLDContext}/secp256r1");

        /// <summary>
        /// secp384r1 / P‐384 (OID: 1.3.132.0.34)
        /// </summary>
        public static CryptoKeyType SecP384r1
            => new($"{JSONLDContext}/secp384r1");

        /// <summary>
        /// secp521r1 / P‐521 (OID: 1.3.132.0.35)
        /// </summary>
        public static CryptoKeyType SecP521r1
            => new($"{JSONLDContext}/secp521r1");

        #endregion


        #region Operator overloading

        #region Operator == (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key types for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key type.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (CryptoKeyType CryptoKeyUsage1,
                                           CryptoKeyType CryptoKeyUsage2)

            => CryptoKeyUsage1.Equals(CryptoKeyUsage2);

        #endregion

        #region Operator != (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key types for inequality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key type.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (CryptoKeyType CryptoKeyUsage1,
                                           CryptoKeyType CryptoKeyUsage2)

            => !CryptoKeyUsage1.Equals(CryptoKeyUsage2);

        #endregion

        #region Operator <  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key types.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key type.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (CryptoKeyType CryptoKeyUsage1,
                                          CryptoKeyType CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) < 0;

        #endregion

        #region Operator <= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key types.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key type.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (CryptoKeyType CryptoKeyUsage1,
                                           CryptoKeyType CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) <= 0;

        #endregion

        #region Operator >  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key types.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key type.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (CryptoKeyType CryptoKeyUsage1,
                                          CryptoKeyType CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) > 0;

        #endregion

        #region Operator >= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key types.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key type.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (CryptoKeyType CryptoKeyUsage1,
                                           CryptoKeyType CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) >= 0;

        #endregion

        #endregion

        #region IComparable<Node_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two crypto key types.
        /// </summary>
        /// <param name="Object">A crypto key type to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is CryptoKeyType cryptoKeyType
                   ? CompareTo(cryptoKeyType)
                   : throw new ArgumentException("The given object is not a crypto key type!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto key types.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key type to compare with.</param>
        public Int32 CompareTo(CryptoKeyType CryptoKeyUsage)

            => String.Compare(InternalId,
                              CryptoKeyUsage.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<Node_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two crypto key types for equality.
        /// </summary>
        /// <param name="Object">A crypto key type to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is CryptoKeyType cryptoKeyType &&
                   Equals(cryptoKeyType);

        #endregion

        #region Equals(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto key types for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key type to compare with.</param>
        public Boolean Equals(CryptoKeyType CryptoKeyUsage)

            => String.Equals(InternalId,
                             CryptoKeyUsage.InternalId,
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
