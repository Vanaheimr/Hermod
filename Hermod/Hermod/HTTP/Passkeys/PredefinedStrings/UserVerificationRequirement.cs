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

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    // https://w3c.github.io/webauthn/#enumdef-userverificationrequirement

    /// <summary>
    /// Extension methods for UserVerificationRequirements.
    /// </summary>
    public static class UserVerificationRequirementExtensions
    {

        /// <summary>
        /// Indicates whether this UserVerificationRequirement is null or empty.
        /// </summary>
        /// <param name="UserVerificationRequirement">An UserVerificationRequirement.</param>
        public static Boolean IsNullOrEmpty(this UserVerificationRequirement? UserVerificationRequirement)
            => !UserVerificationRequirement.HasValue || UserVerificationRequirement.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this UserVerificationRequirement is null or empty.
        /// </summary>
        /// <param name="UserVerificationRequirement">An UserVerificationRequirement.</param>
        public static Boolean IsNotNullOrEmpty(this UserVerificationRequirement? UserVerificationRequirement)
            => UserVerificationRequirement.HasValue && UserVerificationRequirement.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A UserVerificationRequirement.
    /// </summary>
    public readonly struct UserVerificationRequirement : IId,
                                                         IEquatable<UserVerificationRequirement>,
                                                         IComparable<UserVerificationRequirement>
    {

        #region Data

        private readonly static Dictionary<String, UserVerificationRequirement>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                           InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this UserVerificationRequirement is null or empty.
        /// </summary>
        public readonly  Boolean                    IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this UserVerificationRequirement is NOT null or empty.
        /// </summary>
        public readonly  Boolean                    IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the UserVerificationRequirement.
        /// </summary>
        public readonly  UInt64                     Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// All registered UserVerificationRequirements.
        /// </summary>
        public static    IEnumerable<UserVerificationRequirement>  All
            => lookup.Values;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new UserVerificationRequirement based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of an UserVerificationRequirement.</param>
        private UserVerificationRequirement(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static UserVerificationRequirement Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new UserVerificationRequirement(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as an UserVerificationRequirement.
        /// </summary>
        /// <param name="Text">A text representation of an UserVerificationRequirement.</param>
        public static UserVerificationRequirement Parse(String Text)
        {

            if (TryParse(Text, out var userVerificationRequirement))
                return userVerificationRequirement;

            throw new ArgumentException($"Invalid text representation of an UserVerificationRequirement: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as an UserVerificationRequirement.
        /// </summary>
        /// <param name="Text">A text representation of an UserVerificationRequirement.</param>
        public static UserVerificationRequirement? TryParse(String Text)
        {

            if (TryParse(Text, out var userVerificationRequirement))
                return userVerificationRequirement;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out UserVerificationRequirement)

        /// <summary>
        /// Try to parse the given text as an UserVerificationRequirement.
        /// </summary>
        /// <param name="Text">A text representation of an UserVerificationRequirement.</param>
        /// <param name="UserVerificationRequirement">The parsed UserVerificationRequirement.</param>
        public static Boolean TryParse(String Text, out UserVerificationRequirement UserVerificationRequirement)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out UserVerificationRequirement))
                    UserVerificationRequirement = Register(Text);

                return true;

            }

            UserVerificationRequirement = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this UserVerificationRequirement.
        /// </summary>
        public UserVerificationRequirement Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// required
        /// </summary>
        public static UserVerificationRequirement  Required       { get; }
            = Register("required");

        /// <summary>
        /// preferred (default)
        /// </summary>
        public static UserVerificationRequirement  Preferred      { get; }
            = Register("preferred");

        /// <summary>
        /// discouraged
        /// </summary>
        public static UserVerificationRequirement  Discouraged    { get; }
            = Register("discouraged");

        #endregion


        #region Operator overloading

        #region Operator == (UserVerificationRequirement1, UserVerificationRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserVerificationRequirement1">An UserVerificationRequirement.</param>
        /// <param name="UserVerificationRequirement2">Another UserVerificationRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (UserVerificationRequirement UserVerificationRequirement1,
                                           UserVerificationRequirement UserVerificationRequirement2)

            => UserVerificationRequirement1.Equals(UserVerificationRequirement2);

        #endregion

        #region Operator != (UserVerificationRequirement1, UserVerificationRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserVerificationRequirement1">An UserVerificationRequirement.</param>
        /// <param name="UserVerificationRequirement2">Another UserVerificationRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (UserVerificationRequirement UserVerificationRequirement1,
                                           UserVerificationRequirement UserVerificationRequirement2)

            => !UserVerificationRequirement1.Equals(UserVerificationRequirement2);

        #endregion

        #region Operator <  (UserVerificationRequirement1, UserVerificationRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserVerificationRequirement1">An UserVerificationRequirement.</param>
        /// <param name="UserVerificationRequirement2">Another UserVerificationRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (UserVerificationRequirement UserVerificationRequirement1,
                                          UserVerificationRequirement UserVerificationRequirement2)

            => UserVerificationRequirement1.CompareTo(UserVerificationRequirement2) < 0;

        #endregion

        #region Operator <= (UserVerificationRequirement1, UserVerificationRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserVerificationRequirement1">An UserVerificationRequirement.</param>
        /// <param name="UserVerificationRequirement2">Another UserVerificationRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (UserVerificationRequirement UserVerificationRequirement1,
                                           UserVerificationRequirement UserVerificationRequirement2)

            => UserVerificationRequirement1.CompareTo(UserVerificationRequirement2) <= 0;

        #endregion

        #region Operator >  (UserVerificationRequirement1, UserVerificationRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserVerificationRequirement1">An UserVerificationRequirement.</param>
        /// <param name="UserVerificationRequirement2">Another UserVerificationRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (UserVerificationRequirement UserVerificationRequirement1,
                                          UserVerificationRequirement UserVerificationRequirement2)

            => UserVerificationRequirement1.CompareTo(UserVerificationRequirement2) > 0;

        #endregion

        #region Operator >= (UserVerificationRequirement1, UserVerificationRequirement2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserVerificationRequirement1">An UserVerificationRequirement.</param>
        /// <param name="UserVerificationRequirement2">Another UserVerificationRequirement.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (UserVerificationRequirement UserVerificationRequirement1,
                                           UserVerificationRequirement UserVerificationRequirement2)

            => UserVerificationRequirement1.CompareTo(UserVerificationRequirement2) >= 0;

        #endregion

        #endregion

        #region IComparable<UserVerificationRequirement> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two UserVerificationRequirements.
        /// </summary>
        /// <param name="Object">An UserVerificationRequirement to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is UserVerificationRequirement userVerificationRequirement
                   ? CompareTo(userVerificationRequirement)
                   : throw new ArgumentException("The given object is not an UserVerificationRequirement!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(UserVerificationRequirement)

        /// <summary>
        /// Compares two UserVerificationRequirements.
        /// </summary>
        /// <param name="UserVerificationRequirement">An UserVerificationRequirement to compare with.</param>
        public Int32 CompareTo(UserVerificationRequirement UserVerificationRequirement)

            => String.Compare(InternalId,
                              UserVerificationRequirement.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<UserVerificationRequirement> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two UserVerificationRequirements for equality.
        /// </summary>
        /// <param name="Object">An UserVerificationRequirement to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is UserVerificationRequirement userVerificationRequirement &&
                   Equals(userVerificationRequirement);

        #endregion

        #region Equals(UserVerificationRequirement)

        /// <summary>
        /// Compares two UserVerificationRequirements for equality.
        /// </summary>
        /// <param name="UserVerificationRequirement">An UserVerificationRequirement to compare with.</param>
        public Boolean Equals(UserVerificationRequirement UserVerificationRequirement)

            => String.Equals(InternalId,
                             UserVerificationRequirement.InternalId,
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
