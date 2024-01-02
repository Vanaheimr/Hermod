/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// Extension methods for user group identifications.
    /// </summary>
    public static class UserGroupIdExtensions
    {

        /// <summary>
        /// Indicates whether this user group identification is null or empty.
        /// </summary>
        /// <param name="UserGroupId">An user group identification.</param>
        public static Boolean IsNullOrEmpty(this UserGroup_Id? UserGroupId)
            => !UserGroupId.HasValue || UserGroupId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this user group identification is null or empty.
        /// </summary>
        /// <param name="UserGroupId">An user group identification.</param>
        public static Boolean IsNotNullOrEmpty(this UserGroup_Id? UserGroupId)
            => UserGroupId.HasValue && UserGroupId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of an user group.
    /// </summary>
    public readonly struct UserGroup_Id : IId<UserGroup_Id>
    {

        #region Data

        /// <summary>
        /// The internal user group identification.
        /// </summary>
        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this user group identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this user group identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the user group identificator.
        /// </summary>
        public UInt64 Length
            => (UInt64) InternalId?.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new user group identification based on the given string.
        /// </summary>
        private UserGroup_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Random(Length)

        /// <summary>
        /// Create a new user group identification.
        /// </summary>
        /// <param name="Length">The expected length of the user group identification.</param>
        public static UserGroup_Id Random(Byte Length = 20)

            => new (RandomExtensions.RandomString(Length));

        #endregion

        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as an user group identification.
        /// </summary>
        /// <param name="Text">A text representation of an user group identification.</param>
        public static UserGroup_Id Parse(String Text)
        {

            if (TryParse(Text, out UserGroup_Id userGroupId))
                return userGroupId;

            throw new ArgumentException($"Invalid text representation of an user group identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given string as an user group identification.
        /// </summary>
        /// <param name="Text">A text representation of an user group identification.</param>
        public static UserGroup_Id? TryParse(String Text)
        {

            if (TryParse(Text, out UserGroup_Id userGroupId))
                return userGroupId;

            return null;

        }

        #endregion

        #region TryParse(Text, out UserGroupId)

        /// <summary>
        /// Try to parse the given string as an user group identification.
        /// </summary>
        /// <param name="Text">A text representation of an user group identification.</param>
        /// <param name="UserGroupId">The parsed user group identification.</param>
        public static Boolean TryParse(String Text, out UserGroup_Id UserGroupId)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    UserGroupId = new UserGroup_Id(Text);
                    return true;
                }
                catch
                { }
            }

            UserGroupId = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this user group identification.
        /// </summary>
        public UserGroup_Id Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (UserGroupId1, UserGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroupId1">An user group identification.</param>
        /// <param name="UserGroupId2">Another user group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (UserGroup_Id UserGroupId1,
                                           UserGroup_Id UserGroupId2)

            => UserGroupId1.Equals(UserGroupId2);

        #endregion

        #region Operator != (UserGroupId1, UserGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroupId1">An user group identification.</param>
        /// <param name="UserGroupId2">Another user group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (UserGroup_Id UserGroupId1,
                                           UserGroup_Id UserGroupId2)

            => !UserGroupId1.Equals(UserGroupId2);

        #endregion

        #region Operator <  (UserGroupId1, UserGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroupId1">An user group identification.</param>
        /// <param name="UserGroupId2">Another user group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (UserGroup_Id UserGroupId1,
                                          UserGroup_Id UserGroupId2)

            => UserGroupId1.CompareTo(UserGroupId2) < 0;

        #endregion

        #region Operator <= (UserGroupId1, UserGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroupId1">An user group identification.</param>
        /// <param name="UserGroupId2">Another user group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (UserGroup_Id UserGroupId1,
                                           UserGroup_Id UserGroupId2)

            => UserGroupId1.CompareTo(UserGroupId2) <= 0;

        #endregion

        #region Operator >  (UserGroupId1, UserGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroupId1">An user group identification.</param>
        /// <param name="UserGroupId2">Another user group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (UserGroup_Id UserGroupId1,
                                          UserGroup_Id UserGroupId2)

            => UserGroupId1.CompareTo(UserGroupId2) > 0;

        #endregion

        #region Operator >= (UserGroupId1, UserGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroupId1">An user group identification.</param>
        /// <param name="UserGroupId2">Another user group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (UserGroup_Id UserGroupId1,
                                           UserGroup_Id UserGroupId2)

            => UserGroupId1.CompareTo(UserGroupId2) >= 0;

        #endregion

        #endregion

        #region IComparable<UserGroup_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is UserGroup_Id userGroupId
                   ? CompareTo(userGroupId)
                   : throw new ArgumentException("The given object is not an user group identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(UserGroupId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroupId">An object to compare with.</param>
        public Int32 CompareTo(UserGroup_Id UserGroupId)

            => String.Compare(InternalId,
                              UserGroupId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<UserGroup_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is UserGroup_Id userGroupId &&
                   Equals(userGroupId);

        #endregion

        #region Equals(UserGroupId)

        /// <summary>
        /// Compares two user group identifications for equality.
        /// </summary>
        /// <param name="UserGroupId">An user group identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(UserGroup_Id UserGroupId)

            => String.Equals(InternalId,
                             UserGroupId.InternalId,
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
