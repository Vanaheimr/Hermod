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
    /// Extension methods for crypto signature types.
    /// </summary>
    public static class CryptoSignatureTypeExtensions
    {

        /// <summary>
        /// Indicates whether this crypto signature type is null or empty.
        /// </summary>
        /// <param name="CryptoSignatureUsage">A crypto signature type.</param>
        public static Boolean IsNullOrEmpty(this CryptoSignatureType? CryptoSignatureUsage)
            => !CryptoSignatureUsage.HasValue || CryptoSignatureUsage.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this crypto signature type is null or empty.
        /// </summary>
        /// <param name="CryptoSignatureUsage">A crypto signature type.</param>
        public static Boolean IsNotNullOrEmpty(this CryptoSignatureType? CryptoSignatureUsage)
            => CryptoSignatureUsage.HasValue && CryptoSignatureUsage.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A unique crypto signature type.
    /// </summary>
    public readonly struct CryptoSignatureType : IId,
                                           IEquatable <CryptoSignatureType>,
                                           IComparable<CryptoSignatureType>
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
        /// The length of the node identifier.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique crypto signature type based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a crypto signature type.</param>
        private CryptoSignatureType(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a crypto signature type.
        /// </summary>
        /// <param name="Text">A text representation of a crypto signature type.</param>
        public static CryptoSignatureType Parse(String Text)
        {

            if (TryParse(Text, out var cryptoSignatureType))
                return cryptoSignatureType;

            throw new ArgumentException($"Invalid text representation of a crypto signature type: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a crypto signature type.
        /// </summary>
        /// <param name="Text">A text representation of a crypto signature type.</param>
        public static CryptoSignatureType? TryParse(String Text)
        {

            if (TryParse(Text, out var cryptoSignatureType))
                return cryptoSignatureType;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out CryptoSignatureUsage)

        /// <summary>
        /// Parse the given string as a crypto signature type.
        /// </summary>
        /// <param name="Text">A text representation of a crypto signature type.</param>
        /// <param name="CryptoSignatureUsage">The parsed crypto signature type.</param>
        public static Boolean TryParse(String Text, out CryptoSignatureType CryptoSignatureUsage)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    CryptoSignatureUsage = new CryptoSignatureType(Text);
                    return true;
                }
                catch
                { }
            }

            CryptoSignatureUsage = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this crypto signature type.
        /// </summary>
        public CryptoSignatureType Clone

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// ECDSA
        /// </summary>
        public static CryptoSignatureType ECDSA
            => new ($"{CryptoSignature.JSONLDContext}/ECDSA");

        #endregion


        #region Operator overloading

        #region Operator == (CryptoSignatureUsage1, CryptoSignatureUsage2)

        /// <summary>
        /// Compares two crypto signature types for equality.
        /// </summary>
        /// <param name="CryptoSignatureUsage1">A crypto signature type.</param>
        /// <param name="CryptoSignatureUsage2">Another crypto signature type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (CryptoSignatureType CryptoSignatureUsage1,
                                           CryptoSignatureType CryptoSignatureUsage2)

            => CryptoSignatureUsage1.Equals(CryptoSignatureUsage2);

        #endregion

        #region Operator != (CryptoSignatureUsage1, CryptoSignatureUsage2)

        /// <summary>
        /// Compares two crypto signature types for inequality.
        /// </summary>
        /// <param name="CryptoSignatureUsage1">A crypto signature type.</param>
        /// <param name="CryptoSignatureUsage2">Another crypto signature type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (CryptoSignatureType CryptoSignatureUsage1,
                                           CryptoSignatureType CryptoSignatureUsage2)

            => !CryptoSignatureUsage1.Equals(CryptoSignatureUsage2);

        #endregion

        #region Operator <  (CryptoSignatureUsage1, CryptoSignatureUsage2)

        /// <summary>
        /// Compares two crypto signature types.
        /// </summary>
        /// <param name="CryptoSignatureUsage1">A crypto signature type.</param>
        /// <param name="CryptoSignatureUsage2">Another crypto signature type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (CryptoSignatureType CryptoSignatureUsage1,
                                          CryptoSignatureType CryptoSignatureUsage2)

            => CryptoSignatureUsage1.CompareTo(CryptoSignatureUsage2) < 0;

        #endregion

        #region Operator <= (CryptoSignatureUsage1, CryptoSignatureUsage2)

        /// <summary>
        /// Compares two crypto signature types.
        /// </summary>
        /// <param name="CryptoSignatureUsage1">A crypto signature type.</param>
        /// <param name="CryptoSignatureUsage2">Another crypto signature type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (CryptoSignatureType CryptoSignatureUsage1,
                                           CryptoSignatureType CryptoSignatureUsage2)

            => CryptoSignatureUsage1.CompareTo(CryptoSignatureUsage2) <= 0;

        #endregion

        #region Operator >  (CryptoSignatureUsage1, CryptoSignatureUsage2)

        /// <summary>
        /// Compares two crypto signature types.
        /// </summary>
        /// <param name="CryptoSignatureUsage1">A crypto signature type.</param>
        /// <param name="CryptoSignatureUsage2">Another crypto signature type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (CryptoSignatureType CryptoSignatureUsage1,
                                          CryptoSignatureType CryptoSignatureUsage2)

            => CryptoSignatureUsage1.CompareTo(CryptoSignatureUsage2) > 0;

        #endregion

        #region Operator >= (CryptoSignatureUsage1, CryptoSignatureUsage2)

        /// <summary>
        /// Compares two crypto signature types.
        /// </summary>
        /// <param name="CryptoSignatureUsage1">A crypto signature type.</param>
        /// <param name="CryptoSignatureUsage2">Another crypto signature type.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (CryptoSignatureType CryptoSignatureUsage1,
                                           CryptoSignatureType CryptoSignatureUsage2)

            => CryptoSignatureUsage1.CompareTo(CryptoSignatureUsage2) >= 0;

        #endregion

        #endregion

        #region IComparable<Node_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two crypto signature types.
        /// </summary>
        /// <param name="Object">A crypto signature type to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is CryptoSignatureType cryptoSignatureType
                   ? CompareTo(cryptoSignatureType)
                   : throw new ArgumentException("The given object is not a crypto signature type!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(CryptoSignatureUsage)

        /// <summary>
        /// Compares two crypto signature types.
        /// </summary>
        /// <param name="CryptoSignatureUsage">A crypto signature type to compare with.</param>
        public Int32 CompareTo(CryptoSignatureType CryptoSignatureUsage)

            => String.Compare(InternalId,
                              CryptoSignatureUsage.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<Node_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two crypto signature types for equality.
        /// </summary>
        /// <param name="Object">A crypto signature type to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is CryptoSignatureType cryptoSignatureType &&
                   Equals(cryptoSignatureType);

        #endregion

        #region Equals(CryptoSignatureUsage)

        /// <summary>
        /// Compares two crypto signature types for equality.
        /// </summary>
        /// <param name="CryptoSignatureUsage">A crypto signature type to compare with.</param>
        public Boolean Equals(CryptoSignatureType CryptoSignatureUsage)

            => String.Equals(InternalId,
                             CryptoSignatureUsage.InternalId,
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
