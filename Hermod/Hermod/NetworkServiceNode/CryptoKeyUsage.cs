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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for crypto key usages.
    /// </summary>
    public static class CryptoKeyUsageExtensions
    {

        /// <summary>
        /// Indicates whether this crypto key usage is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key usage.</param>
        public static Boolean IsNullOrEmpty(this CryptoKeyUsage? CryptoKeyUsage)
            => CryptoKeyUsage is null || CryptoKeyUsage.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this crypto key usage is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key usage.</param>
        public static Boolean IsNotNullOrEmpty(this CryptoKeyUsage? CryptoKeyUsage)
            => CryptoKeyUsage is not null && CryptoKeyUsage.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A crypto key usage.
    /// </summary>
    public class CryptoKeyUsage : IId,
                                  IEquatable <CryptoKeyUsage>,
                                  IComparable<CryptoKeyUsage>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private        readonly String                                        InternalId;

        private static readonly ConcurrentDictionary<String, CryptoKeyUsage>  lookup        = new();

        private        readonly HashSet<CryptoKeyUsageTuple>                  usageTuples   = new();

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


        public IEnumerable<CryptoKeyUsageTuple> Relations
            => usageTuples;

        public IEnumerable<CryptoKeyUsageTriple> RelationTriples
        {
            get
            {
                var me = this;
                return usageTuples.Select(tuple => tuple.RelatesFrom(me));
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique crypto key usage based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a crypto key usage.</param>
        /// <param name="KeyUsageTuples">An optional enumeration of crypto key usage tuples.</param>
        private CryptoKeyUsage(String                             Text,
                               IEnumerable<CryptoKeyUsageTuple>?  KeyUsageTuples   = null)
        {

            InternalId = Text;

            if (KeyUsageTuples is not null)
                foreach (var keyUsageTuple in KeyUsageTuples)
                    usageTuples.Add(keyUsageTuple);

        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a crypto key usage.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key usage.</param>
        public static CryptoKeyUsage Parse(String Text)
        {

            if (TryParse(Text, out var cryptoKeyUsage))
                return cryptoKeyUsage!;

            throw new ArgumentException($"Invalid text representation of a crypto key usage: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a crypto key usage.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key usage.</param>
        public static CryptoKeyUsage? TryParse(String Text)
        {

            if (TryParse(Text, out var cryptoKeyUsage))
                return cryptoKeyUsage;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out CryptoKeyUsage)

        /// <summary>
        /// Parse the given string as a crypto key usage.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key usage.</param>
        /// <param name="CryptoKeyUsage">The parsed crypto key usage.</param>
        public static Boolean TryParse(String Text, out CryptoKeyUsage? CryptoKeyUsage)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    CryptoKeyUsage = Lookup(Text);
                    return true;
                }
                catch
                { }
            }

            CryptoKeyUsage = default;
            return false;

        }

        #endregion


        #region (private static) Lookup(KeyUsage)

        private static CryptoKeyUsage Lookup(String KeyUsage)
        {

            if (lookup.TryGetValue(KeyUsage, out var keyUsage))
                return keyUsage;

            keyUsage = new CryptoKeyUsage(KeyUsage);

            lookup.TryAdd(KeyUsage, keyUsage);

            return keyUsage;

        }

        #endregion


        #region Static definitions

        /// <summary>
        /// Identity
        /// </summary>
        public static CryptoKeyUsage Identity
            => Lookup("https://open.charging.cloud/contexts/crypto/keyUsages/identity");

        /// <summary>
        /// Identity Group (Membership)
        /// </summary>
        public static CryptoKeyUsage IdentityGroup
            => Lookup("https://open.charging.cloud/contexts/crypto/keyUsages/identityGroup");

        /// <summary>
        /// Encryption
        /// </summary>
        public static CryptoKeyUsage Encryption
            => Lookup("https://open.charging.cloud/contexts/crypto/keyUsages/encryption");

        /// <summary>
        /// Signature
        /// </summary>
        public static CryptoKeyUsage Signature
            => Lookup("https://open.charging.cloud/contexts/crypto/keyUsages/signature");



        /// <summary>
        /// Signature for the Measuring Instruments Directive (MID)
        /// </summary>
        public static CryptoKeyUsage MeasuringInstrumentsDirective
            => Lookup("https://open.charging.cloud/contexts/crypto/keyUsages/MeasuringInstrumentsDirective/signature");

        /// <summary>
        /// Signature for the German Calibration Law: Type Approval (Module B)
        /// </summary>
        public static CryptoKeyUsage GermanCalibrationLaw_TypeApproval
            => Lookup("https://open.charging.cloud/contexts/crypto/keyUsages/GermanCalibrationLaw/TypeApproval/signature");

        /// <summary>
        /// Signature for the German Calibration Law: Quality Assurance (Module D)
        /// </summary>
        public static CryptoKeyUsage GermanCalibrationLaw_QualityAssurance
            => Lookup("https://open.charging.cloud/contexts/crypto/keyUsages/GermanCalibrationLaw/QualityAssurance/signature");

        /// <summary>
        /// Signature for the Office of Weights and Measures (Eichamt)
        /// </summary>
        public static CryptoKeyUsage Eichamt
            => Lookup("https://open.charging.cloud/contexts/crypto/keyUsages/WeightsAndMeasuresOffice/signature");

        #endregion


        #region Operator overloading

        #region Operator == (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usages for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (CryptoKeyUsage CryptoKeyUsage1,
                                           CryptoKeyUsage CryptoKeyUsage2)
        {

            if (Object.ReferenceEquals(CryptoKeyUsage1, CryptoKeyUsage2))
                return true;

            if (CryptoKeyUsage1 is null || CryptoKeyUsage2 is null)
                return false;

            return CryptoKeyUsage1.Equals(CryptoKeyUsage2);

        }

        #endregion

        #region Operator != (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usages for inequality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (CryptoKeyUsage CryptoKeyUsage1,
                                           CryptoKeyUsage CryptoKeyUsage2)

            => !(CryptoKeyUsage1 == CryptoKeyUsage2);

        #endregion

        #region Operator <  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usages.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (CryptoKeyUsage CryptoKeyUsage1,
                                          CryptoKeyUsage CryptoKeyUsage2)

            => CryptoKeyUsage1 is null
                   ? throw new ArgumentNullException(nameof(CryptoKeyUsage1), "The given crypto key usage must not be null!")
                   : CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) < 0;

        #endregion

        #region Operator <= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usages.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (CryptoKeyUsage CryptoKeyUsage1,
                                           CryptoKeyUsage CryptoKeyUsage2)

            => !(CryptoKeyUsage1 > CryptoKeyUsage2);

        #endregion

        #region Operator >  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usages.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (CryptoKeyUsage CryptoKeyUsage1,
                                          CryptoKeyUsage CryptoKeyUsage2)

            => CryptoKeyUsage1 is null
                   ? throw new ArgumentNullException(nameof(CryptoKeyUsage1), "The given crypto key usage must not be null!")
                   : CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) > 0;

        #endregion

        #region Operator >= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key usages.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key usage.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key usage.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (CryptoKeyUsage CryptoKeyUsage1,
                                           CryptoKeyUsage CryptoKeyUsage2)

            => !(CryptoKeyUsage1 < CryptoKeyUsage2);

        #endregion

        #endregion

        #region IComparable<CryptoKeyUsage> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two crypto key usages.
        /// </summary>
        /// <param name="Object">A crypto key usage to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is CryptoKeyUsage cryptoKeyUsage
                   ? CompareTo(cryptoKeyUsage)
                   : throw new ArgumentException("The given object is not a crypto key usage!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto key usages.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key usage to compare with.</param>
        public Int32 CompareTo(CryptoKeyUsage? CryptoKeyUsage)
        {

            if (CryptoKeyUsage is null)
                throw new ArgumentNullException(nameof(CryptoKeyUsage), "The given crypto key usage must not be null!");

            return String.Compare(InternalId,
                                  CryptoKeyUsage.InternalId,
                                  StringComparison.OrdinalIgnoreCase);

        }

        #endregion

        #endregion

        #region IEquatable<CryptoKeyUsage> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two crypto key usages for equality.
        /// </summary>
        /// <param name="Object">A crypto key usage to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is CryptoKeyUsage cryptoKeyUsage &&
                   Equals(cryptoKeyUsage);

        #endregion

        #region Equals(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto key usages for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key usage to compare with.</param>
        public Boolean Equals(CryptoKeyUsage? CryptoKeyUsage)

            => CryptoKeyUsage is not null &&

               String.Equals(InternalId,
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
