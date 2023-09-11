/*
 * Copyright (c) 2014-2023 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The unique identification of an organization group.
    /// </summary>
    public readonly struct OrganizationGroup_Id : IId<OrganizationGroup_Id>
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
        /// The length of the organization group identificator.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new organization group identification based on the given string.
        /// </summary>
        private OrganizationGroup_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Random(Length)

        /// <summary>
        /// Create a new organization group identification.
        /// </summary>
        /// <param name="Length">The expected length of the organization group identification.</param>
        public static OrganizationGroup_Id Random(Byte Length = 20)

            => new (RandomExtensions.RandomString(Length));

        #endregion

        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as an organization group identification.
        /// </summary>
        /// <param name="Text">A text representation of an organization group identification.</param>
        public static OrganizationGroup_Id Parse(String Text)
        {

            if (TryParse(Text, out OrganizationGroup_Id organizationGroupId))
                return organizationGroupId;

            throw new ArgumentException($"Invalid text representation of an organization group identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an organization group identification.
        /// </summary>
        /// <param name="Text">A text representation of an organization group identification.</param>
        public static OrganizationGroup_Id? TryParse(String Text)
        {

            if (TryParse(Text, out OrganizationGroup_Id organizationGroupId))
                return organizationGroupId;

            return null;

        }

        #endregion

        #region TryParse(Text, out OrganizationGroupId)

        /// <summary>
        /// Try to parse the given string as an organization group identification.
        /// </summary>
        /// <param name="Text">A text representation of an organization group identification.</param>
        /// <param name="OrganizationGroupId">The parsed organization group identification.</param>
        public static Boolean TryParse(String Text, out OrganizationGroup_Id OrganizationGroupId)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    OrganizationGroupId = new OrganizationGroup_Id(Text);
                    return true;
                }
                catch
                { }
            }

            OrganizationGroupId = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this organization group identification.
        /// </summary>
        public OrganizationGroup_Id Clone

            => new OrganizationGroup_Id(
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (OrganizationGroupId1, OrganizationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroupId1">An organization group identification.</param>
        /// <param name="OrganizationGroupId2">Another organization group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (OrganizationGroup_Id OrganizationGroupId1,
                                           OrganizationGroup_Id OrganizationGroupId2)

            => OrganizationGroupId1.Equals(OrganizationGroupId2);

        #endregion

        #region Operator != (OrganizationGroupId1, OrganizationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroupId1">An organization group identification.</param>
        /// <param name="OrganizationGroupId2">Another organization group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (OrganizationGroup_Id OrganizationGroupId1,
                                           OrganizationGroup_Id OrganizationGroupId2)

            => !OrganizationGroupId1.Equals(OrganizationGroupId2);

        #endregion

        #region Operator <  (OrganizationGroupId1, OrganizationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroupId1">An organization group identification.</param>
        /// <param name="OrganizationGroupId2">Another organization group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (OrganizationGroup_Id OrganizationGroupId1,
                                          OrganizationGroup_Id OrganizationGroupId2)

            => OrganizationGroupId1.CompareTo(OrganizationGroupId2) < 0;

        #endregion

        #region Operator <= (OrganizationGroupId1, OrganizationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroupId1">An organization group identification.</param>
        /// <param name="OrganizationGroupId2">Another organization group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (OrganizationGroup_Id OrganizationGroupId1,
                                           OrganizationGroup_Id OrganizationGroupId2)

            => OrganizationGroupId1.CompareTo(OrganizationGroupId2) <= 0;

        #endregion

        #region Operator >  (OrganizationGroupId1, OrganizationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroupId1">An organization group identification.</param>
        /// <param name="OrganizationGroupId2">Another organization group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (OrganizationGroup_Id OrganizationGroupId1,
                                          OrganizationGroup_Id OrganizationGroupId2)

            => OrganizationGroupId1.CompareTo(OrganizationGroupId2) > 0;

        #endregion

        #region Operator >= (OrganizationGroupId1, OrganizationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroupId1">An organization group identification.</param>
        /// <param name="OrganizationGroupId2">Another organization group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (OrganizationGroup_Id OrganizationGroupId1,
                                           OrganizationGroup_Id OrganizationGroupId2)

            => OrganizationGroupId1.CompareTo(OrganizationGroupId2) >= 0;

        #endregion

        #endregion

        #region IComparable<OrganizationGroupId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is OrganizationGroup_Id organizationGroupId
                   ? CompareTo(organizationGroupId)
                   : throw new ArgumentException("The given object is not an organization group identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(OrganizationGroupId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroupId">An object to compare with.</param>
        public Int32 CompareTo(OrganizationGroup_Id OrganizationGroupId)

            => String.Compare(InternalId,
                              OrganizationGroupId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<OrganizationGroupId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is OrganizationGroup_Id organizationGroupId &&
                   Equals(organizationGroupId);

        #endregion

        #region Equals(OrganizationGroupId)

        /// <summary>
        /// Compares two OrganizationGroupIds for equality.
        /// </summary>
        /// <param name="OrganizationGroupId">An organization group identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(OrganizationGroup_Id OrganizationGroupId)

            => String.Equals(InternalId,
                             OrganizationGroupId.InternalId,
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
