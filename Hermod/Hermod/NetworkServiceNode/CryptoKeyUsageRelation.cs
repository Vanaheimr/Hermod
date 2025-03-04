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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for crypto key usage relations.
    /// </summary>
    public static class CryptoKeyUsageRelationExtensions
    {

        /// <summary>
        /// Indicates whether this crypto key usage relation is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation">A crypto key usage relation.</param>
        public static Boolean IsNullOrEmpty(this CryptoKeyUsageRelation? CryptoKeyUsageRelation)
            => CryptoKeyUsageRelation is null || CryptoKeyUsageRelation.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this crypto key usage relation is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation">A crypto key usage relation.</param>
        public static Boolean IsNotNullOrEmpty(this CryptoKeyUsageRelation? CryptoKeyUsageRelation)
            => CryptoKeyUsageRelation is not null && CryptoKeyUsageRelation.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A crypto key usage relation.
    /// </summary>
    public class CryptoKeyUsageRelation : IId,
                                          IEquatable <CryptoKeyUsageRelation>,
                                          IComparable<CryptoKeyUsageRelation>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private        readonly String                                                InternalId;

        private static readonly ConcurrentDictionary<String, CryptoKeyUsageRelation>  lookup = new();

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
        /// The length of the node identifier.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique crypto key usage relation based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a crypto key usage relation.</param>
        private CryptoKeyUsageRelation(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a crypto key usage relation.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key usage relation.</param>
        public static CryptoKeyUsageRelation Parse(String Text)
        {

            if (TryParse(Text, out var cryptoKeyUsageRelation))
                return cryptoKeyUsageRelation!;

            throw new ArgumentException($"Invalid text representation of a crypto key usage relation: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a crypto key usage relation.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key usage relation.</param>
        public static CryptoKeyUsageRelation? TryParse(String Text)
        {

            if (TryParse(Text, out var cryptoKeyUsageRelation))
                return cryptoKeyUsageRelation;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out CryptoKeyUsageRelation)

        /// <summary>
        /// Parse the given string as a crypto key usage relation.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key usage relation.</param>
        /// <param name="CryptoKeyUsageRelation">The parsed crypto key usage relation.</param>
        public static Boolean TryParse(String Text, out CryptoKeyUsageRelation? CryptoKeyUsageRelation)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    CryptoKeyUsageRelation = Lookup(Text);
                    return true;
                }
                catch
                { }
            }

            CryptoKeyUsageRelation = default;
            return false;

        }

        #endregion


        #region (private static) Lookup(KeyUsage)

        private static CryptoKeyUsageRelation Lookup(String KeyUsage)
        {

            if (lookup.TryGetValue(KeyUsage, out var keyUsage))
                return keyUsage;

            keyUsage = new CryptoKeyUsageRelation(KeyUsage);

            lookup.TryAdd(KeyUsage, keyUsage);

            return keyUsage;

        }

        #endregion


        #region Static definitions

        /// <summary>
        /// IsParent
        /// </summary>
        public static CryptoKeyUsageRelation IsParent
            => Lookup("https://open.charging.cloud/contexts/crypto/keyUsageRelations/IsParent");

        #endregion


        #region Operator overloading

        #region Operator == (CryptoKeyUsageRelation1, CryptoKeyUsageRelation2)

        /// <summary>
        /// Compares two crypto key usage relations for equality.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation1">A crypto key usage relation.</param>
        /// <param name="CryptoKeyUsageRelation2">Another crypto key usage relation.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (CryptoKeyUsageRelation CryptoKeyUsageRelation1,
                                           CryptoKeyUsageRelation CryptoKeyUsageRelation2)
        {

            if (Object.ReferenceEquals(CryptoKeyUsageRelation1, CryptoKeyUsageRelation2))
                return true;

            if (CryptoKeyUsageRelation1 is null || CryptoKeyUsageRelation2 is null)
                return false;

            return CryptoKeyUsageRelation1.Equals(CryptoKeyUsageRelation2);

        }

        #endregion

        #region Operator != (CryptoKeyUsageRelation1, CryptoKeyUsageRelation2)

        /// <summary>
        /// Compares two crypto key usage relations for inequality.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation1">A crypto key usage relation.</param>
        /// <param name="CryptoKeyUsageRelation2">Another crypto key usage relation.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (CryptoKeyUsageRelation CryptoKeyUsageRelation1,
                                           CryptoKeyUsageRelation CryptoKeyUsageRelation2)

            => !(CryptoKeyUsageRelation1 == CryptoKeyUsageRelation2);

        #endregion

        #region Operator <  (CryptoKeyUsageRelation1, CryptoKeyUsageRelation2)

        /// <summary>
        /// Compares two crypto key usage relations.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation1">A crypto key usage relation.</param>
        /// <param name="CryptoKeyUsageRelation2">Another crypto key usage relation.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (CryptoKeyUsageRelation CryptoKeyUsageRelation1,
                                          CryptoKeyUsageRelation CryptoKeyUsageRelation2)

            => CryptoKeyUsageRelation1 is null
                   ? throw new ArgumentNullException(nameof(CryptoKeyUsageRelation1), "The given crypto key usage relation must not be null!")
                   : CryptoKeyUsageRelation1.CompareTo(CryptoKeyUsageRelation2) < 0;

        #endregion

        #region Operator <= (CryptoKeyUsageRelation1, CryptoKeyUsageRelation2)

        /// <summary>
        /// Compares two crypto key usage relations.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation1">A crypto key usage relation.</param>
        /// <param name="CryptoKeyUsageRelation2">Another crypto key usage relation.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (CryptoKeyUsageRelation CryptoKeyUsageRelation1,
                                           CryptoKeyUsageRelation CryptoKeyUsageRelation2)

            => !(CryptoKeyUsageRelation1 > CryptoKeyUsageRelation2);

        #endregion

        #region Operator >  (CryptoKeyUsageRelation1, CryptoKeyUsageRelation2)

        /// <summary>
        /// Compares two crypto key usage relations.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation1">A crypto key usage relation.</param>
        /// <param name="CryptoKeyUsageRelation2">Another crypto key usage relation.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (CryptoKeyUsageRelation CryptoKeyUsageRelation1,
                                          CryptoKeyUsageRelation CryptoKeyUsageRelation2)

            => CryptoKeyUsageRelation1 is null
                   ? throw new ArgumentNullException(nameof(CryptoKeyUsageRelation1), "The given crypto key usage relation must not be null!")
                   : CryptoKeyUsageRelation1.CompareTo(CryptoKeyUsageRelation2) > 0;

        #endregion

        #region Operator >= (CryptoKeyUsageRelation1, CryptoKeyUsageRelation2)

        /// <summary>
        /// Compares two crypto key usage relations.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation1">A crypto key usage relation.</param>
        /// <param name="CryptoKeyUsageRelation2">Another crypto key usage relation.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (CryptoKeyUsageRelation CryptoKeyUsageRelation1,
                                           CryptoKeyUsageRelation CryptoKeyUsageRelation2)

            => !(CryptoKeyUsageRelation1 < CryptoKeyUsageRelation2);

        #endregion

        #endregion

        #region IComparable<CryptoKeyUsageRelation> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two crypto key usage relations.
        /// </summary>
        /// <param name="Object">A crypto key usage relation to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is CryptoKeyUsageRelation cryptoKeyUsageRelation
                   ? CompareTo(cryptoKeyUsageRelation)
                   : throw new ArgumentException("The given object is not a crypto key usage relation!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(CryptoKeyUsageRelation)

        /// <summary>
        /// Compares two crypto key usage relations.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation">A crypto key usage relation to compare with.</param>
        public Int32 CompareTo(CryptoKeyUsageRelation? CryptoKeyUsageRelation)
        {

            if (CryptoKeyUsageRelation is null)
                throw new ArgumentNullException(nameof(CryptoKeyUsageRelation), "The given crypto key usage relation must not be null!");

            return String.Compare(InternalId,
                                  CryptoKeyUsageRelation.InternalId,
                                  StringComparison.OrdinalIgnoreCase);

        }

        #endregion

        #endregion

        #region IEquatable<CryptoKeyUsageRelation> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two crypto key usage relations for equality.
        /// </summary>
        /// <param name="Object">A crypto key usage relation to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is CryptoKeyUsageRelation cryptoKeyUsageRelation &&
                   Equals(cryptoKeyUsageRelation);

        #endregion

        #region Equals(CryptoKeyUsageRelation)

        /// <summary>
        /// Compares two crypto key usage relations for equality.
        /// </summary>
        /// <param name="CryptoKeyUsageRelation">A crypto key usage relation to compare with.</param>
        public Boolean Equals(CryptoKeyUsageRelation? CryptoKeyUsageRelation)

            => CryptoKeyUsageRelation is not null &&

               String.Equals(InternalId,
                             CryptoKeyUsageRelation.InternalId,
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
