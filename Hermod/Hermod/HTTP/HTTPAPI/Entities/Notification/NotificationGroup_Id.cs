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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// Extension methods for notification group identifications.
    /// </summary>
    public static class NotificationGroupIdExtensions
    {

        /// <summary>
        /// Indicates whether this notification group identification is null or empty.
        /// </summary>
        /// <param name="NotificationGroupId">A notification group identification.</param>
        public static Boolean IsNullOrEmpty(this NotificationGroup_Id? NotificationGroupId)
            => !NotificationGroupId.HasValue || NotificationGroupId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this notification group identification is null or empty.
        /// </summary>
        /// <param name="NotificationGroupId">A notification group identification.</param>
        public static Boolean IsNotNullOrEmpty(this NotificationGroup_Id? NotificationGroupId)
            => NotificationGroupId.HasValue && NotificationGroupId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of an user.
    /// </summary>
    public readonly struct NotificationGroup_Id : IId,
                                                  IEquatable<NotificationGroup_Id>,
                                                  IComparable<NotificationGroup_Id>
    {

        #region Data

        /// <summary>
        /// The internal notification group identification.
        /// </summary>
        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this notification group identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this notification group identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the user identificator.
        /// </summary>
        public UInt64 Length
            => (UInt64) InternalId?.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new notification group identification based on the given string.
        /// </summary>
        private NotificationGroup_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Random(Length)

        /// <summary>
        /// Create a new random notification group identification.
        /// </summary>
        /// <param name="Length">The expected length of the random notification group identification.</param>
        public static NotificationGroup_Id Random(Byte Length = 15)

            => new (RandomExtensions.RandomString(Length).ToUpper());

        #endregion

        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as a notification group identification.
        /// </summary>
        /// <param name="Text">A text representation of a notification group identification.</param>
        public static NotificationGroup_Id Parse(String Text)
        {

            if (TryParse(Text, out NotificationGroup_Id notificationGroupId))
                return notificationGroupId;

            throw new ArgumentException($"Invalid text representation of a notification group identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a notification group identification.
        /// </summary>
        /// <param name="Text">A text representation of a notification group identification.</param>
        public static NotificationGroup_Id? TryParse(String Text)
        {

            if (TryParse(Text, out NotificationGroup_Id notificationGroupId))
                return notificationGroupId;

            return null;

        }

        #endregion

        #region TryParse(Text, out NotificationGroupId)

        /// <summary>
        /// Try to parse the given string as a notification group identification.
        /// </summary>
        /// <param name="Text">A text representation of a notification group identification.</param>
        /// <param name="NotificationGroupId">The parsed notification group identification.</param>
        public static Boolean TryParse(String Text, out NotificationGroup_Id NotificationGroupId)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    NotificationGroupId = new NotificationGroup_Id(Text);
                    return true;
                }
                catch
                { }
            }

            NotificationGroupId = default;
            return false;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this notification group identification.
        /// </summary>
        public NotificationGroup_Id Clone()

            => new (
                   InternalId.CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (NotificationGroupId1, NotificationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationGroupId1">A notification group identification.</param>
        /// <param name="NotificationGroupId2">Another notification group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (NotificationGroup_Id NotificationGroupId1,
                                           NotificationGroup_Id NotificationGroupId2)

            => NotificationGroupId1.Equals(NotificationGroupId2);

        #endregion

        #region Operator != (NotificationGroupId1, NotificationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationGroupId1">A notification group identification.</param>
        /// <param name="NotificationGroupId2">Another notification group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (NotificationGroup_Id NotificationGroupId1,
                                           NotificationGroup_Id NotificationGroupId2)

            => !NotificationGroupId1.Equals(NotificationGroupId2);

        #endregion

        #region Operator <  (NotificationGroupId1, NotificationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationGroupId1">A notification group identification.</param>
        /// <param name="NotificationGroupId2">Another notification group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (NotificationGroup_Id NotificationGroupId1,
                                          NotificationGroup_Id NotificationGroupId2)

            => NotificationGroupId1.CompareTo(NotificationGroupId2) < 0;

        #endregion

        #region Operator <= (NotificationGroupId1, NotificationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationGroupId1">A notification group identification.</param>
        /// <param name="NotificationGroupId2">Another notification group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (NotificationGroup_Id NotificationGroupId1,
                                           NotificationGroup_Id NotificationGroupId2)

            => NotificationGroupId1.CompareTo(NotificationGroupId2) <= 0;

        #endregion

        #region Operator >  (NotificationGroupId1, NotificationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationGroupId1">A notification group identification.</param>
        /// <param name="NotificationGroupId2">Another notification group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (NotificationGroup_Id NotificationGroupId1,
                                          NotificationGroup_Id NotificationGroupId2)

            => NotificationGroupId1.CompareTo(NotificationGroupId2) > 0;

        #endregion

        #region Operator >= (NotificationGroupId1, NotificationGroupId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationGroupId1">A notification group identification.</param>
        /// <param name="NotificationGroupId2">Another notification group identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (NotificationGroup_Id NotificationGroupId1,
                                           NotificationGroup_Id NotificationGroupId2)

            => NotificationGroupId1.CompareTo(NotificationGroupId2) >= 0;

        #endregion

        #endregion

        #region IComparable<NotificationGroup_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is NotificationGroup_Id notificationGroupId
                   ? CompareTo(notificationGroupId)
                   : throw new ArgumentException("The given object is not a notification group identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(NotificationGroupId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationGroupId">An object to compare with.</param>
        public Int32 CompareTo(NotificationGroup_Id NotificationGroupId)

            => String.Compare(InternalId,
                              NotificationGroupId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<NotificationGroup_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is NotificationGroup_Id notificationGroupId &&
                   Equals(notificationGroupId);

        #endregion

        #region Equals(NotificationGroupId)

        /// <summary>
        /// Compares two notification group identifications for equality.
        /// </summary>
        /// <param name="NotificationGroupId">A notification group identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(NotificationGroup_Id NotificationGroupId)

            => String.Equals(InternalId,
                             NotificationGroupId.InternalId,
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
