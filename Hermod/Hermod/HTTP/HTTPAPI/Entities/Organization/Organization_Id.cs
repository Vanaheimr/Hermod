/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
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

using System;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for organization identifications.
    /// </summary>
    public static class OrganizationIdExtensions
    {

        /// <summary>
        /// Indicates whether this organization identification is null or empty.
        /// </summary>
        /// <param name="OrganizationId">An organization identification.</param>
        public static Boolean IsNullOrEmpty(this Organization_Id? OrganizationId)
            => !OrganizationId.HasValue || OrganizationId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this organization identification is NOT null or empty.
        /// </summary>
        /// <param name="OrganizationId">An organization identification.</param>
        public static Boolean IsNotNullOrEmpty(this Organization_Id? OrganizationId)
            => OrganizationId.HasValue && OrganizationId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of an organization.
    /// </summary>
    public readonly struct Organization_Id : IId,
                                             IEquatable<Organization_Id>,
                                             IComparable<Organization_Id>
    {

        #region Data

        /// <summary>
        /// The internal organization identification.
        /// </summary>
        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this organization identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this organization identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the organization identificator.
        /// </summary>
        public UInt64 Length
            => (UInt64) InternalId?.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new organization identification based on the given string.
        /// </summary>
        private Organization_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Random(Length)

        /// <summary>
        /// Create a new organization identification.
        /// </summary>
        /// <param name="Length">The expected length of the organization identification.</param>
        public static Organization_Id Random(Byte Length = 15)

            => new (RandomExtensions.RandomString(Length).ToUpper());

        #endregion

        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as an organization identification.
        /// </summary>
        /// <param name="Text">A text representation of an organization identification.</param>
        public static Organization_Id Parse(String Text)
        {

            if (TryParse(Text, out Organization_Id organizationId))
                return organizationId;

            throw new ArgumentException($"Invalid text representation of an organization identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an organization identification.
        /// </summary>
        /// <param name="Text">A text representation of an organization identification.</param>
        public static Organization_Id? TryParse(String Text)
        {

            if (TryParse(Text, out Organization_Id organizationId))
                return organizationId;

            return null;

        }

        #endregion

        #region TryParse(Text, out OrganizationId)

        /// <summary>
        /// Try to parse the given string as an organization identification.
        /// </summary>
        /// <param name="Text">A text representation of an organization identification.</param>
        /// <param name="OrganizationId">The parsed organization identification.</param>
        public static Boolean TryParse(String Text, out Organization_Id OrganizationId)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    OrganizationId = new Organization_Id(Text);
                    return true;
                }
                catch
                { }
            }

            OrganizationId = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this organization identification.
        /// </summary>
        public Organization_Id Clone

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (OrganizationIdId1, OrganizationIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationIdId1">An organization identification.</param>
        /// <param name="OrganizationIdId2">Another organization identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (Organization_Id OrganizationIdId1,
                                           Organization_Id OrganizationIdId2)

            => OrganizationIdId1.Equals(OrganizationIdId2);

        #endregion

        #region Operator != (OrganizationIdId1, OrganizationIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationIdId1">An organization identification.</param>
        /// <param name="OrganizationIdId2">Another organization identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (Organization_Id OrganizationIdId1,
                                           Organization_Id OrganizationIdId2)

            => !OrganizationIdId1.Equals(OrganizationIdId2);

        #endregion

        #region Operator <  (OrganizationIdId1, OrganizationIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationIdId1">An organization identification.</param>
        /// <param name="OrganizationIdId2">Another organization identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (Organization_Id OrganizationIdId1,
                                          Organization_Id OrganizationIdId2)

            => OrganizationIdId1.CompareTo(OrganizationIdId2) < 0;

        #endregion

        #region Operator <= (OrganizationIdId1, OrganizationIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationIdId1">An organization identification.</param>
        /// <param name="OrganizationIdId2">Another organization identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (Organization_Id OrganizationIdId1,
                                           Organization_Id OrganizationIdId2)

            => OrganizationIdId1.CompareTo(OrganizationIdId2) <= 0;

        #endregion

        #region Operator >  (OrganizationIdId1, OrganizationIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationIdId1">An organization identification.</param>
        /// <param name="OrganizationIdId2">Another organization identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (Organization_Id OrganizationIdId1,
                                          Organization_Id OrganizationIdId2)

            => OrganizationIdId1.CompareTo(OrganizationIdId2) > 0;

        #endregion

        #region Operator >= (OrganizationIdId1, OrganizationIdId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationIdId1">An organization identification.</param>
        /// <param name="OrganizationIdId2">Another organization identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (Organization_Id OrganizationIdId1,
                                           Organization_Id OrganizationIdId2)

            => OrganizationIdId1.CompareTo(OrganizationIdId2) >= 0;

        #endregion

        #endregion

        #region IComparable<Organization_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is Organization_Id organizationId
                   ? CompareTo(organizationId)
                   : throw new ArgumentException("The given object is not an organization identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(OrganizationId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationId">An object to compare with.</param>
        public Int32 CompareTo(Organization_Id OrganizationId)

            => String.Compare(InternalId,
                              OrganizationId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<Organization_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is Organization_Id organizationId &&
                   Equals(organizationId);

        #endregion

        #region Equals(OrganizationId)

        /// <summary>
        /// Compares two organization identifications for equality.
        /// </summary>
        /// <param name="OrganizationId">An organization identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(Organization_Id OrganizationId)

            => String.Equals(InternalId,
                             OrganizationId.InternalId,
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
