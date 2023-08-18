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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// Extension methods for notification message identifications.
    /// </summary>
    public static class NotificationMessageIdExtensions
    {

        /// <summary>
        /// Indicates whether this notification message identification is null or empty.
        /// </summary>
        /// <param name="NotificationMessageId">A notification message identification.</param>
        public static Boolean IsNullOrEmpty(this NotificationMessage_Id? NotificationMessageId)
            => !NotificationMessageId.HasValue || NotificationMessageId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this notification message identification is null or empty.
        /// </summary>
        /// <param name="NotificationMessageId">A notification message identification.</param>
        public static Boolean IsNotNullOrEmpty(this NotificationMessage_Id? NotificationMessageId)
            => NotificationMessageId.HasValue && NotificationMessageId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique identification of an user.
    /// </summary>
    public readonly struct NotificationMessage_Id : IId,
                                                  IEquatable<NotificationMessage_Id>,
                                                  IComparable<NotificationMessage_Id>
    {

        #region Data

        /// <summary>
        /// The internal notification message identification.
        /// </summary>
        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this notification message identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this notification message identification is NOT null or empty.
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
        /// Create a new notification message identification based on the given string.
        /// </summary>
        private NotificationMessage_Id(String Text)
        {
            InternalId = Text;
        }

        #endregion


        #region (static) Random(Length)

        /// <summary>
        /// Create a new random notification message identification.
        /// </summary>
        /// <param name="Length">The expected length of the random notification message identification.</param>
        public static NotificationMessage_Id Random(Byte Length = 15)

            => new (RandomExtensions.RandomString(Length).ToUpper());

        #endregion

        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as a notification message identification.
        /// </summary>
        /// <param name="Text">A text representation of a notification message identification.</param>
        public static NotificationMessage_Id Parse(String Text)
        {

            if (TryParse(Text, out NotificationMessage_Id notificationMessageId))
                return notificationMessageId;

            throw new ArgumentException($"Invalid text representation of a notification message identification: '{Text}'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a notification message identification.
        /// </summary>
        /// <param name="Text">A text representation of a notification message identification.</param>
        public static NotificationMessage_Id? TryParse(String Text)
        {

            if (TryParse(Text, out NotificationMessage_Id notificationMessageId))
                return notificationMessageId;

            return null;

        }

        #endregion

        #region TryParse(Text, out NotificationMessageId)

        /// <summary>
        /// Try to parse the given string as a notification message identification.
        /// </summary>
        /// <param name="Text">A text representation of a notification message identification.</param>
        /// <param name="NotificationMessageId">The parsed notification message identification.</param>
        public static Boolean TryParse(String Text, out NotificationMessage_Id NotificationMessageId)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    NotificationMessageId = new NotificationMessage_Id(Text);
                    return true;
                }
                catch
                { }
            }

            NotificationMessageId = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this notification message identification.
        /// </summary>
        public NotificationMessage_Id Clone

            => new NotificationMessage_Id(
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A notification message identification.</param>
        /// <param name="NotificationMessageId2">Another notification message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (NotificationMessage_Id NotificationMessageId1,
                                           NotificationMessage_Id NotificationMessageId2)

            => NotificationMessageId1.Equals(NotificationMessageId2);

        #endregion

        #region Operator != (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A notification message identification.</param>
        /// <param name="NotificationMessageId2">Another notification message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (NotificationMessage_Id NotificationMessageId1,
                                           NotificationMessage_Id NotificationMessageId2)

            => !NotificationMessageId1.Equals(NotificationMessageId2);

        #endregion

        #region Operator <  (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A notification message identification.</param>
        /// <param name="NotificationMessageId2">Another notification message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (NotificationMessage_Id NotificationMessageId1,
                                          NotificationMessage_Id NotificationMessageId2)

            => NotificationMessageId1.CompareTo(NotificationMessageId2) < 0;

        #endregion

        #region Operator <= (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A notification message identification.</param>
        /// <param name="NotificationMessageId2">Another notification message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (NotificationMessage_Id NotificationMessageId1,
                                           NotificationMessage_Id NotificationMessageId2)

            => NotificationMessageId1.CompareTo(NotificationMessageId2) <= 0;

        #endregion

        #region Operator >  (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A notification message identification.</param>
        /// <param name="NotificationMessageId2">Another notification message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (NotificationMessage_Id NotificationMessageId1,
                                          NotificationMessage_Id NotificationMessageId2)

            => NotificationMessageId1.CompareTo(NotificationMessageId2) > 0;

        #endregion

        #region Operator >= (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A notification message identification.</param>
        /// <param name="NotificationMessageId2">Another notification message identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (NotificationMessage_Id NotificationMessageId1,
                                           NotificationMessage_Id NotificationMessageId2)

            => NotificationMessageId1.CompareTo(NotificationMessageId2) >= 0;

        #endregion

        #endregion

        #region IComparable<NotificationMessage_Id> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is NotificationMessage_Id notificationMessageId
                   ? CompareTo(notificationMessageId)
                   : throw new ArgumentException("The given object is not a notification message identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(NotificationMessageId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId">An object to compare with.</param>
        public Int32 CompareTo(NotificationMessage_Id NotificationMessageId)

            => String.Compare(InternalId,
                              NotificationMessageId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<NotificationMessage_Id> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is NotificationMessage_Id notificationMessageId &&
                   Equals(notificationMessageId);

        #endregion

        #region Equals(NotificationMessageId)

        /// <summary>
        /// Compares two notification message identifications for equality.
        /// </summary>
        /// <param name="NotificationMessageId">A notification message identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(NotificationMessage_Id NotificationMessageId)

            => String.Equals(InternalId,
                             NotificationMessageId.InternalId,
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
