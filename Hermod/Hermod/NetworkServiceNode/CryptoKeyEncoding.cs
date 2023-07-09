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
    /// Extension methods for crypto key encodings.
    /// </summary>
    public static class CryptoKeyEncodingExtensions
    {

        /// <summary>
        /// Indicates whether this crypto key encoding is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key encoding.</param>
        public static Boolean IsNullOrEmpty(this CryptoKeyEncoding? CryptoKeyUsage)
            => !CryptoKeyUsage.HasValue || CryptoKeyUsage.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this crypto key encoding is null or empty.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key encoding.</param>
        public static Boolean IsNotNullOrEmpty(this CryptoKeyEncoding? CryptoKeyUsage)
            => CryptoKeyUsage.HasValue && CryptoKeyUsage.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A unique crypto key encoding.
    /// </summary>
    public readonly struct CryptoKeyEncoding : IId,
                                               IEquatable <CryptoKeyEncoding>,
                                               IComparable<CryptoKeyEncoding>
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
        /// Create a new unique crypto key encoding based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a crypto key encoding.</param>
        private CryptoKeyEncoding(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a crypto key encoding.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key encoding.</param>
        public static CryptoKeyEncoding Parse(String Text)
        {

            if (TryParse(Text, out var cryptoKeyEncoding))
                return cryptoKeyEncoding;

            throw new ArgumentException($"Invalid text representation of a crypto key encoding: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a crypto key encoding.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key encoding.</param>
        public static CryptoKeyEncoding? TryParse(String Text)
        {

            if (TryParse(Text, out var cryptoKeyEncoding))
                return cryptoKeyEncoding;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out CryptoKeyUsage)

        /// <summary>
        /// Parse the given string as a crypto key encoding.
        /// </summary>
        /// <param name="Text">A text representation of a crypto key encoding.</param>
        /// <param name="CryptoKeyUsage">The parsed crypto key encoding.</param>
        public static Boolean TryParse(String Text, out CryptoKeyEncoding CryptoKeyUsage)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    CryptoKeyUsage = new CryptoKeyEncoding(Text);
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
        /// Clone this crypto key encoding.
        /// </summary>
        public CryptoKeyEncoding Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// HEX
        /// </summary>
        public static CryptoKeyEncoding HEX
            => new ("https://open.charging.cloud/contexts/crypto/encoding/HEX");

        /// <summary>
        /// BASE64
        /// </summary>
        public static CryptoKeyEncoding BASE64
            => new("https://open.charging.cloud/contexts/crypto/encoding/BASE64");

        #endregion


        #region Operator overloading

        #region Operator == (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key encodings for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key encoding.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (CryptoKeyEncoding CryptoKeyUsage1,
                                           CryptoKeyEncoding CryptoKeyUsage2)

            => CryptoKeyUsage1.Equals(CryptoKeyUsage2);

        #endregion

        #region Operator != (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key encodings for inequality.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key encoding.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (CryptoKeyEncoding CryptoKeyUsage1,
                                           CryptoKeyEncoding CryptoKeyUsage2)

            => !CryptoKeyUsage1.Equals(CryptoKeyUsage2);

        #endregion

        #region Operator <  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key encodings.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key encoding.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (CryptoKeyEncoding CryptoKeyUsage1,
                                          CryptoKeyEncoding CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) < 0;

        #endregion

        #region Operator <= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key encodings.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key encoding.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (CryptoKeyEncoding CryptoKeyUsage1,
                                           CryptoKeyEncoding CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) <= 0;

        #endregion

        #region Operator >  (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key encodings.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key encoding.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (CryptoKeyEncoding CryptoKeyUsage1,
                                          CryptoKeyEncoding CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) > 0;

        #endregion

        #region Operator >= (CryptoKeyUsage1, CryptoKeyUsage2)

        /// <summary>
        /// Compares two crypto key encodings.
        /// </summary>
        /// <param name="CryptoKeyUsage1">A crypto key encoding.</param>
        /// <param name="CryptoKeyUsage2">Another crypto key encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (CryptoKeyEncoding CryptoKeyUsage1,
                                           CryptoKeyEncoding CryptoKeyUsage2)

            => CryptoKeyUsage1.CompareTo(CryptoKeyUsage2) >= 0;

        #endregion

        #endregion

        #region IComparable<Node_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two crypto key encodings.
        /// </summary>
        /// <param name="Object">A crypto key encoding to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is CryptoKeyEncoding cryptoKeyEncoding
                   ? CompareTo(cryptoKeyEncoding)
                   : throw new ArgumentException("The given object is not a crypto key encoding!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto key encodings.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key encoding to compare with.</param>
        public Int32 CompareTo(CryptoKeyEncoding CryptoKeyUsage)

            => String.Compare(InternalId,
                              CryptoKeyUsage.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<Node_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two crypto key encodings for equality.
        /// </summary>
        /// <param name="Object">A crypto key encoding to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is CryptoKeyEncoding cryptoKeyEncoding &&
                   Equals(cryptoKeyEncoding);

        #endregion

        #region Equals(CryptoKeyUsage)

        /// <summary>
        /// Compares two crypto key encodings for equality.
        /// </summary>
        /// <param name="CryptoKeyUsage">A crypto key encoding to compare with.</param>
        public Boolean Equals(CryptoKeyEncoding CryptoKeyUsage)

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
