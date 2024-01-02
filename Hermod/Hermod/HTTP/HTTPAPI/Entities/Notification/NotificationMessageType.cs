/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// The unique identification of a notification message type.
    /// </summary>
    public readonly struct NotificationMessageType : IId,
                                                     IEquatable<NotificationMessageType>,
                                                     IComparable<NotificationMessageType>

    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String  InternalId;

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
        /// The length of the notification identification.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new notification message type based on the given string.
        /// </summary>
        /// <param name="String">The string representation of the notification identification.</param>
        private NotificationMessageType(String String)
        {
            this.InternalId = String;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a notification identification.
        /// </summary>
        /// <param name="Text">A text representation of a notification identification.</param>
        public static NotificationMessageType Parse(String Text)
        {

            if (TryParse(Text, out NotificationMessageType notificationMessageType))
                return notificationMessageType;

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a notification message type must not be null or empty!");

            throw new ArgumentException("The given text representation of a notification message type is invalid!", nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a notification identification.
        /// </summary>
        /// <param name="Text">A text representation of a notification identification.</param>
        public static NotificationMessageType? TryParse(String Text)
        {

            if (TryParse(Text, out NotificationMessageType notificationMessageType))
                return notificationMessageType;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out NotificationMessageType)

        /// <summary>
        /// Try to parse the given string as a notification identification.
        /// </summary>
        /// <param name="Text">A text representation of a notification identification.</param>
        /// <param name="NotificationMessageType">The parsed notification identification.</param>
        public static Boolean TryParse(String Text, out NotificationMessageType NotificationMessageType)
        {

            Text = Text?.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    NotificationMessageType = new NotificationMessageType(Text);
                    return true;
                }
                catch
                { }
            }

            NotificationMessageType = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this notification identification.
        /// </summary>
        public NotificationMessageType Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (NotificationMessageType1, NotificationMessageType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageType1">A notification identification.</param>
        /// <param name="NotificationMessageType2">Another notification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (NotificationMessageType NotificationMessageType1,
                                           NotificationMessageType NotificationMessageType2)

            => NotificationMessageType1.Equals(NotificationMessageType2);

        #endregion

        #region Operator != (NotificationMessageType1, NotificationMessageType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageType1">A notification identification.</param>
        /// <param name="NotificationMessageType2">Another notification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (NotificationMessageType NotificationMessageType1,
                                           NotificationMessageType NotificationMessageType2)

            => !NotificationMessageType1.Equals(NotificationMessageType2);

        #endregion

        #region Operator <  (NotificationMessageType1, NotificationMessageType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageType1">A notification identification.</param>
        /// <param name="NotificationMessageType2">Another notification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (NotificationMessageType NotificationMessageType1,
                                          NotificationMessageType NotificationMessageType2)

            => NotificationMessageType1.CompareTo(NotificationMessageType2) < 0;

        #endregion

        #region Operator <= (NotificationMessageType1, NotificationMessageType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageType1">A notification identification.</param>
        /// <param name="NotificationMessageType2">Another notification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (NotificationMessageType NotificationMessageType1,
                                           NotificationMessageType NotificationMessageType2)

            => NotificationMessageType1.CompareTo(NotificationMessageType2) <= 0;

        #endregion

        #region Operator >  (NotificationMessageType1, NotificationMessageType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageType1">A notification identification.</param>
        /// <param name="NotificationMessageType2">Another notification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (NotificationMessageType NotificationMessageType1,
                                          NotificationMessageType NotificationMessageType2)

            => NotificationMessageType1.CompareTo(NotificationMessageType2) > 0;

        #endregion

        #region Operator >= (NotificationMessageType1, NotificationMessageType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageType1">A notification identification.</param>
        /// <param name="NotificationMessageType2">Another notification identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (NotificationMessageType NotificationMessageType1,
                                           NotificationMessageType NotificationMessageType2)

            => NotificationMessageType1.CompareTo(NotificationMessageType2) >= 0;

        #endregion

        #endregion

        #region IComparable<NotificationMessageType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is NotificationMessageType notificationMessageType
                   ? CompareTo(notificationMessageType)
                   : throw new ArgumentException("The given object is not a notification message type!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(NotificationMessageType)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageType">An object to compare with.</param>
        public Int32 CompareTo(NotificationMessageType NotificationMessageType)

            => String.Compare(InternalId,
                              NotificationMessageType.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<NotificationMessageType> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is NotificationMessageType notificationMessageType &&
                   Equals(notificationMessageType);

        #endregion

        #region Equals(NotificationMessageType)

        /// <summary>
        /// Compares two notification identifications for equality.
        /// </summary>
        /// <param name="NotificationMessageType">An notification identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(NotificationMessageType NotificationMessageType)

            => String.Equals(InternalId,
                             NotificationMessageType.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => InternalId?.ToLower().
                           GetHashCode() ?? 0;

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
