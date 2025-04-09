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
    /// Extension methods for crypto signature status.
    /// </summary>
    public static class CryptoSignatureStatusExtensions
    {

        /// <summary>
        /// Indicates whether this crypto signature status is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto signature status.</param>
        public static Boolean IsNullOrEmpty(this CryptoSignatureStatus? CryptoKeyUsage)
            => !CryptoKeyUsage.HasValue || CryptoKeyUsage.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this crypto signature status is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto signature status.</param>
        public static Boolean IsNotNullOrEmpty(this CryptoSignatureStatus? CryptoKeyUsage)
            => CryptoKeyUsage.HasValue && CryptoKeyUsage.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A unique crypto signature status.
    /// </summary>
    public readonly struct CryptoSignatureStatus : IId,
                                                   IEquatable <CryptoSignatureStatus>,
                                                   IComparable<CryptoSignatureStatus>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

        /// <summary>
        /// The JSON-LD context of the object.
        /// </summary>
        public const String JSONLDContext = "https://open.charging.cloud/contexts/crypto/signatures/status";

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
        /// Create a new unique crypto signature status based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a crypto signature status.</param>
        private CryptoSignatureStatus(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a crypto signature status.
        /// </summary>
        /// <param name="Text">A text representation of a crypto signature status.</param>
        public static CryptoSignatureStatus Parse(String Text)
        {

            if (TryParse(Text, out var cryptoSignatureStatus))
                return cryptoSignatureStatus;

            throw new ArgumentException($"Invalid text representation of a crypto signature status: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a crypto signature status.
        /// </summary>
        /// <param name="Text">A text representation of a crypto signature status.</param>
        public static CryptoSignatureStatus? TryParse(String Text)
        {

            if (TryParse(Text, out var cryptoSignatureStatus))
                return cryptoSignatureStatus;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out CryptoKeyUsage)

        /// <summary>
        /// Parse the given string as a crypto signature status.
        /// </summary>
        /// <param name="Text">A text representation of a crypto signature status.</param>
        /// <param name="CryptoKeyUsage">The parsed crypto signature status.</param>
        public static Boolean TryParse(String Text, out CryptoSignatureStatus CryptoKeyUsage)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    CryptoKeyUsage = new CryptoSignatureStatus(Text);
                    return true;
                }
                catch
                { }
            }

            CryptoKeyUsage = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this crypto signature status.
        /// </summary>
        public CryptoSignatureStatus Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// Unverified
        /// </summary>
        public static CryptoSignatureStatus  Unverified
            => new($"{JSONLDContext}/unverified");

        /// <summary>
        /// Verified
        /// </summary>
        public static CryptoSignatureStatus  Verified
            => new($"{JSONLDContext}/verified");

        /// <summary>
        /// Invalid public key
        /// </summary>
        public static CryptoSignatureStatus  InvalidPublicKey
            => new($"{JSONLDContext}/invalidPublicKey");

        /// <summary>
        /// Invalid signature
        /// </summary>
        public static CryptoSignatureStatus  InvalidSignature
            => new($"{JSONLDContext}/invalidSignature");

        /// <summary>
        /// Processing errors (
        /// </summary>
        public static CryptoSignatureStatus  ProcessingErrors
            => new($"{JSONLDContext}/processingErrors");

        /// <summary>
        /// Invalid (processable, but invalid)
        /// </summary>
        public static CryptoSignatureStatus  Invalid
            => new($"{JSONLDContext}/invalid");

        #endregion


        #region Operator overloading

        #region Operator == (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto signature status for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto signature status.</param>
        /// <param name="CryptoKeyUsage2">Another crypto signature status.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (CryptoSignatureStatus CryptoKeyUsage1,
                                           CryptoSignatureStatus CryptoKeyUsage2)

            => CryptoKeyUsage1.Equals(CryptoKeyUsage2);

        #endregion

        #region Operator != (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto signature status for inequality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto signature status.</param>
        /// <param name="CryptoKeyUsage2">Another crypto signature status.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (CryptoSignatureStatus CryptoKeyUsage1,
                                           CryptoSignatureStatus CryptoKeyUsage2)

            => !CryptoKeyUsage1.Equals(CryptoKeyUsage2);

        #endregion

        #region Operator <  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto signature status.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto signature status.</param>
        /// <param name="CryptoKeyUsage2">Another crypto signature status.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (CryptoSignatureStatus CryptoKeyUsage1,
                                          CryptoSignatureStatus CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) < 0;

        #endregion

        #region Operator <= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto signature status.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto signature status.</param>
        /// <param name="CryptoKeyUsage2">Another crypto signature status.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (CryptoSignatureStatus CryptoKeyUsage1,
                                           CryptoSignatureStatus CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) <= 0;

        #endregion

        #region Operator >  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto signature status.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto signature status.</param>
        /// <param name="CryptoKeyUsage2">Another crypto signature status.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (CryptoSignatureStatus CryptoKeyUsage1,
                                          CryptoSignatureStatus CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) > 0;

        #endregion

        #region Operator >= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto signature status.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto signature status.</param>
        /// <param name="CryptoKeyUsage2">Another crypto signature status.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (CryptoSignatureStatus CryptoKeyUsage1,
                                           CryptoSignatureStatus CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) >= 0;

        #endregion

        #endregion

        #region IComparable<Node_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two crypto signature status.
        /// </summary>
        /// <param name="Object">A crypto signature status to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is CryptoSignatureStatus cryptoSignatureStatus
                   ? CompareTo(cryptoSignatureStatus)
                   : throw new ArgumentException("The given object is not a crypto signature status!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto signature status.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto signature status to compare with.</param>
        public Int32 CompareTo(CryptoSignatureStatus CryptoKeyUsage)

            => String.Compare(InternalId,
                              CryptoKeyUsage.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<Node_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two crypto signature status for equality.
        /// </summary>
        /// <param name="Object">A crypto signature status to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is CryptoSignatureStatus cryptoSignatureStatus &&
                   Equals(cryptoSignatureStatus);

        #endregion

        #region Equals(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto signature status for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto signature status to compare with.</param>
        public Boolean Equals(CryptoSignatureStatus CryptoKeyUsage)

            => String.Equals(InternalId,
                             CryptoKeyUsage.InternalId,
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
