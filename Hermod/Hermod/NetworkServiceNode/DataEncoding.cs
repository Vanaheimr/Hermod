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
    /// Extension methods for data encodings.
    /// </summary>
    public static class CryptoKeyEncodingExtensions
    {

        /// <summary>
        /// Indicates whether this data encoding is null or empty.
        /// </summary>
        /// <param name="DataEncoding">A data encoding.</param>
        public static Boolean IsNullOrEmpty(this DataEncoding? DataEncoding)
            => !DataEncoding.HasValue || DataEncoding.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this data encoding is null or empty.
        /// </summary>
        /// <param name="DataEncoding">A data encoding.</param>
        public static Boolean IsNotNullOrEmpty(this DataEncoding? DataEncoding)
            => DataEncoding.HasValue && DataEncoding.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A unique data encoding.
    /// </summary>
    public readonly struct DataEncoding : IId,
                                          IEquatable <DataEncoding>,
                                          IComparable<DataEncoding>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

        /// <summary>
        /// The JSON-LD context of the object.
        /// </summary>
        public const String JSONLDContext = "https://open.charging.cloud/contexts/encoding";

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
        /// Create a new unique data encoding based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a data encoding.</param>
        private DataEncoding(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the given text as a data encoding.
        /// </summary>
        /// <param name="Text">A text representation of a data encoding.</param>
        public static DataEncoding Parse(String Text)
        {

            if (TryParse(Text, out var dataEncoding))
                return dataEncoding;

            throw new ArgumentException($"Invalid text representation of a data encoding: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the given text as a data encoding.
        /// </summary>
        /// <param name="Text">A text representation of a data encoding.</param>
        public static DataEncoding? TryParse(String Text)
        {

            if (TryParse(Text, out var dataEncoding))
                return dataEncoding;

            return null;

        }

        #endregion

        #region (static) TryParse (Text, out DataEncoding)

        /// <summary>
        /// Parse the given string as a data encoding.
        /// </summary>
        /// <param name="Text">A text representation of a data encoding.</param>
        /// <param name="DataEncoding">The parsed data encoding.</param>
        public static Boolean TryParse(String Text, out DataEncoding DataEncoding)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    DataEncoding = new DataEncoding(Text);
                    return true;
                }
                catch
                { }
            }

            DataEncoding = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this data encoding.
        /// </summary>
        public DataEncoding Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// HEX
        /// </summary>
        public static DataEncoding HEX
            => new ($"{JSONLDContext}/HEX");

        /// <summary>
        /// BASE64
        /// </summary>
        public static DataEncoding BASE64
            => new($"{JSONLDContext}/BASE64");

        #endregion


        #region Operator overloading

        #region Operator == (DataEncoding1, DataEncoding2)

        /// <summary>
        /// Compares two data encodings for equality.
        /// </summary>
        /// <param name="DataEncoding1">A data encoding.</param>
        /// <param name="DataEncoding2">Another data encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DataEncoding DataEncoding1,
                                           DataEncoding DataEncoding2)

            => DataEncoding1.Equals(DataEncoding2);

        #endregion

        #region Operator != (DataEncoding1, DataEncoding2)

        /// <summary>
        /// Compares two data encodings for inequality.
        /// </summary>
        /// <param name="DataEncoding1">A data encoding.</param>
        /// <param name="DataEncoding2">Another data encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DataEncoding DataEncoding1,
                                           DataEncoding DataEncoding2)

            => !DataEncoding1.Equals(DataEncoding2);

        #endregion

        #region Operator <  (DataEncoding1, DataEncoding2)

        /// <summary>
        /// Compares two data encodings.
        /// </summary>
        /// <param name="DataEncoding1">A data encoding.</param>
        /// <param name="DataEncoding2">Another data encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (DataEncoding DataEncoding1,
                                          DataEncoding DataEncoding2)

            => DataEncoding1.CompareTo(DataEncoding2) < 0;

        #endregion

        #region Operator <= (DataEncoding1, DataEncoding2)

        /// <summary>
        /// Compares two data encodings.
        /// </summary>
        /// <param name="DataEncoding1">A data encoding.</param>
        /// <param name="DataEncoding2">Another data encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (DataEncoding DataEncoding1,
                                           DataEncoding DataEncoding2)

            => DataEncoding1.CompareTo(DataEncoding2) <= 0;

        #endregion

        #region Operator >  (DataEncoding1, DataEncoding2)

        /// <summary>
        /// Compares two data encodings.
        /// </summary>
        /// <param name="DataEncoding1">A data encoding.</param>
        /// <param name="DataEncoding2">Another data encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (DataEncoding DataEncoding1,
                                          DataEncoding DataEncoding2)

            => DataEncoding1.CompareTo(DataEncoding2) > 0;

        #endregion

        #region Operator >= (DataEncoding1, DataEncoding2)

        /// <summary>
        /// Compares two data encodings.
        /// </summary>
        /// <param name="DataEncoding1">A data encoding.</param>
        /// <param name="DataEncoding2">Another data encoding.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (DataEncoding DataEncoding1,
                                           DataEncoding DataEncoding2)

            => DataEncoding1.CompareTo(DataEncoding2) >= 0;

        #endregion

        #endregion

        #region IComparable<Node_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two data encodings.
        /// </summary>
        /// <param name="Object">A data encoding to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DataEncoding dataEncoding
                   ? CompareTo(dataEncoding)
                   : throw new ArgumentException("The given object is not a data encoding!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(DataEncoding)

        /// <summary>
        /// Compares two data encodings.
        /// </summary>
        /// <param name="DataEncoding">A data encoding to compare with.</param>
        public Int32 CompareTo(DataEncoding DataEncoding)

            => String.Compare(InternalId,
                              DataEncoding.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<Node_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two data encodings for equality.
        /// </summary>
        /// <param name="Object">A data encoding to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DataEncoding dataEncoding &&
                   Equals(dataEncoding);

        #endregion

        #region Equals(DataEncoding)

        /// <summary>
        /// Compares two data encodings for equality.
        /// </summary>
        /// <param name="DataEncoding">A data encoding to compare with.</param>
        public Boolean Equals(DataEncoding DataEncoding)

            => String.Equals(InternalId,
                             DataEncoding.InternalId,
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
