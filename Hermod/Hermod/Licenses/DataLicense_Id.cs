﻿/*
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
    /// Extension methods for data license identifications.
    /// </summary>
    public static class DataLicenseIdExtensions
    {

        /// <summary>
        /// Indicates whether this data license identification is null or empty.
        /// </summary>
        /// <param name="DataLicenseId">A data license identification.</param>
        public static Boolean IsNullOrEmpty(this DataLicense_Id? DataLicenseId)
            => !DataLicenseId.HasValue || DataLicenseId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this data license identification is null or empty.
        /// </summary>
        /// <param name="DataLicenseId">A data license identification.</param>
        public static Boolean IsNotNullOrEmpty(this DataLicense_Id? DataLicenseId)
            => DataLicenseId.HasValue && DataLicenseId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of a data license.
    /// </summary>
    public readonly struct DataLicense_Id : IId<DataLicense_Id>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this data license identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this data license identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the data license identifier.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new data license identification based on the given string.
        /// </summary>
        private DataLicense_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a data license identification.
        /// </summary>
        /// <param name="Text">A text representation of a data license identification.</param>
        public static DataLicense_Id Parse(String Text)
        {

            if (TryParse(Text, out var dataLicenseId))
                return dataLicenseId;

            throw new ArgumentException($"Invalid text representation of a data license identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a data license identification.
        /// </summary>
        /// <param name="Text">A text representation of a data license identification.</param>
        public static DataLicense_Id? TryParse(String Text)
        {

            if (TryParse(Text, out var dataLicenseId))
                return dataLicenseId;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out DataLicenseId)

        /// <summary>
        /// Try to parse the given string as a data license identification.
        /// </summary>
        /// <param name="Text">A text representation of a data license identification.</param>
        /// <param name="DataLicenseId">The parsed data license identification.</param>
        public static Boolean TryParse(String Text, out DataLicense_Id DataLicenseId)
        {

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    DataLicenseId = new DataLicense_Id(Text.Trim());
                    return true;
                }
                catch
                { }
            }

            DataLicenseId = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this data license identification.
        /// </summary>
        public DataLicense_Id Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (DataLicenseIdId1, DataLicenseIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DataLicenseIdId1">A data license identification.</param>
        /// <param name="DataLicenseIdId2">Another data license identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (DataLicense_Id DataLicenseIdId1,
                                           DataLicense_Id DataLicenseIdId2)

            => DataLicenseIdId1.Equals(DataLicenseIdId2);

        #endregion

        #region Operator != (DataLicenseIdId1, DataLicenseIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DataLicenseIdId1">A data license identification.</param>
        /// <param name="DataLicenseIdId2">Another data license identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (DataLicense_Id DataLicenseIdId1,
                                           DataLicense_Id DataLicenseIdId2)

            => !DataLicenseIdId1.Equals(DataLicenseIdId2);

        #endregion

        #region Operator <  (DataLicenseIdId1, DataLicenseIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DataLicenseIdId1">A data license identification.</param>
        /// <param name="DataLicenseIdId2">Another data license identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (DataLicense_Id DataLicenseIdId1,
                                          DataLicense_Id DataLicenseIdId2)

            => DataLicenseIdId1.CompareTo(DataLicenseIdId2) < 0;

        #endregion

        #region Operator <= (DataLicenseIdId1, DataLicenseIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DataLicenseIdId1">A data license identification.</param>
        /// <param name="DataLicenseIdId2">Another data license identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (DataLicense_Id DataLicenseIdId1,
                                           DataLicense_Id DataLicenseIdId2)

            => DataLicenseIdId1.CompareTo(DataLicenseIdId2) <= 0;

        #endregion

        #region Operator >  (DataLicenseIdId1, DataLicenseIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DataLicenseIdId1">A data license identification.</param>
        /// <param name="DataLicenseIdId2">Another data license identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (DataLicense_Id DataLicenseIdId1,
                                          DataLicense_Id DataLicenseIdId2)

            => DataLicenseIdId1.CompareTo(DataLicenseIdId2) > 0;

        #endregion

        #region Operator >= (DataLicenseIdId1, DataLicenseIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DataLicenseIdId1">A data license identification.</param>
        /// <param name="DataLicenseIdId2">Another data license identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (DataLicense_Id DataLicenseIdId1,
                                           DataLicense_Id DataLicenseIdId2)

            => DataLicenseIdId1.CompareTo(DataLicenseIdId2) >= 0;

        #endregion

        #endregion

        #region IComparable<DataLicenseId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DataLicense_Id dataLicenseId
                   ? CompareTo(dataLicenseId)
                   : throw new ArgumentException("The given object is not a data license identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(DataLicenseId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="DataLicenseId">An object to compare with.</param>
        public Int32 CompareTo(DataLicense_Id DataLicenseId)

            => String.Compare(InternalId,
                              DataLicenseId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<DataLicenseId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object? Object)

            => Object is DataLicense_Id dataLicenseId &&
                   Equals(dataLicenseId);

        #endregion

        #region Equals(DataLicenseId)

        /// <summary>
        /// Compares two DataLicenseIds for equality.
        /// </summary>
        /// <param name="DataLicenseId">A data license identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(DataLicense_Id DataLicenseId)

            => String.Equals(InternalId,
                             DataLicenseId.InternalId,
                             StringComparison.OrdinalIgnoreCase);

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
