///*
// * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
// * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
// *
// * Licensed under the Apache License, Version 2.0 (the "License");
// * you may not use this file except in compliance with the License.
// * You may obtain a copy of the License at
// *
// *     http://www.apache.org/licenses/LICENSE-2.0
// *
// * Unless required by applicable law or agreed to in writing, software
// * distributed under the License is distributed on an "AS IS" BASIS,
// * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// * See the License for the specific language governing permissions and
// * limitations under the License.
// */

//#region Usings

//using System;

//using org.GraphDefined.Vanaheimr.Illias;

//#endregion

//namespace social.OpenData.HTTPExtAPI.Notifications
//{

//    /// <summary>
//    /// The unique identification of a notification.
//    /// </summary>
//    public struct NotificationMessageGroupId : IId,
//                                               IEquatable<NotificationMessageGroupId>,
//                                               IComparable<NotificationMessageGroupId>

//    {

//        #region Data

//        /// <summary>
//        /// The internal identification.
//        /// </summary>
//        private readonly String  InternalId;

//        #endregion

//        #region Properties

//        /// <summary>
//        /// Indicates whether this identification is null or empty.
//        /// </summary>
//        public Boolean IsNullOrEmpty
//            => InternalId.IsNullOrEmpty();

//        /// <summary>
//        /// The length of the notification identification.
//        /// </summary>
//        public UInt64 Length
//            => (UInt64) InternalId?.Length;

//        #endregion

//        #region Constructor(s)

//        /// <summary>
//        /// Create a new notification identification based on the given string.
//        /// </summary>
//        /// <param name="String">The string representation of the notification identification.</param>
//        private NotificationMessageGroupId(String  String)
//        {
//            this.InternalId  = String;
//        }

//        #endregion


//        #region (static) Parse   (Text)

//        /// <summary>
//        /// Parse the given string as a notification identification.
//        /// </summary>
//        /// <param name="Text">A text representation of a notification identification.</param>
//        public static NotificationMessageGroupId Parse(String Text)
//        {

//            #region Initial checks

//            if (Text != null)
//                Text = Text.Trim();

//            if (Text.IsNullOrEmpty())
//                throw new ArgumentNullException(nameof(Text), "The given text representation of a notification identification must not be null or empty!");

//            #endregion

//            return new NotificationMessageGroupId(Text);

//        }

//        #endregion

//        #region (static) TryParse(Text)

//        /// <summary>
//        /// Try to parse the given string as a notification identification.
//        /// </summary>
//        /// <param name="Text">A text representation of a notification identification.</param>
//        public static NotificationMessageGroupId? TryParse(String Text)
//        {

//            #region Initial checks

//            if (Text != null)
//                Text = Text.Trim();

//            if (Text.IsNullOrEmpty())
//                throw new ArgumentNullException(nameof(Text), "The given text representation of a notification identification must not be null or empty!");

//            #endregion

//            if (TryParse(Text, out NotificationMessageGroupId _NotificationId))
//                return _NotificationId;

//            return new NotificationMessageGroupId?();

//        }

//        #endregion

//        #region (static) TryParse(Text, out NotificationId)

//        /// <summary>
//        /// Try to parse the given string as a notification identification.
//        /// </summary>
//        /// <param name="Text">A text representation of a notification identification.</param>
//        /// <param name="NotificationId">The parsed notification identification.</param>
//        public static Boolean TryParse(String Text, out NotificationMessageGroupId NotificationId)
//        {

//            #region Initial checks

//            if (Text != null)
//                Text = Text.Trim();

//            if (Text.IsNullOrEmpty())
//                throw new ArgumentNullException(nameof(Text), "The given text representation of a notification identification must not be null or empty!");

//            #endregion

//            try
//            {
//                NotificationId = new NotificationMessageGroupId(Text);
//                return true;
//            }
//            catch
//            {
//                NotificationId = default(NotificationMessageGroupId);
//                return false;
//            }

//        }

//        #endregion

//        #region Clone

//        /// <summary>
//        /// Clone this notification identification.
//        /// </summary>
//        public NotificationMessageGroupId Clone
//            => new NotificationMessageGroupId(new String(InternalId?.ToCharArray()));

//        #endregion


//        #region Operator overloading

//        #region Operator == (NotificationId1, NotificationId2)

//        /// <summary>
//        /// Compares two instances of this object.
//        /// </summary>
//        /// <param name="NotificationId1">A notification identification.</param>
//        /// <param name="NotificationId2">Another notification identification.</param>
//        /// <returns>true|false</returns>
//        public static Boolean operator == (NotificationMessageGroupId NotificationId1, NotificationMessageGroupId NotificationId2)
//        {

//            // If both are null, or both are same instance, return true.
//            if (Object.ReferenceEquals(NotificationId1, NotificationId2))
//                return true;

//            // If one is null, but not both, return false.
//            if (((Object) NotificationId1 == null) || ((Object) NotificationId2 == null))
//                return false;

//            return NotificationId1.Equals(NotificationId2);

//        }

//        #endregion

//        #region Operator != (NotificationId1, NotificationId2)

//        /// <summary>
//        /// Compares two instances of this object.
//        /// </summary>
//        /// <param name="NotificationId1">A notification identification.</param>
//        /// <param name="NotificationId2">Another notification identification.</param>
//        /// <returns>true|false</returns>
//        public static Boolean operator != (NotificationMessageGroupId NotificationId1, NotificationMessageGroupId NotificationId2)
//            => !(NotificationId1 == NotificationId2);

//        #endregion

//        #region Operator <  (NotificationId1, NotificationId2)

//        /// <summary>
//        /// Compares two instances of this object.
//        /// </summary>
//        /// <param name="NotificationId1">A notification identification.</param>
//        /// <param name="NotificationId2">Another notification identification.</param>
//        /// <returns>true|false</returns>
//        public static Boolean operator < (NotificationMessageGroupId NotificationId1, NotificationMessageGroupId NotificationId2)
//        {

//            if ((Object) NotificationId1 == null)
//                throw new ArgumentNullException(nameof(NotificationId1), "The given NotificationId1 must not be null!");

//            return NotificationId1.CompareTo(NotificationId2) < 0;

//        }

//        #endregion

//        #region Operator <= (NotificationId1, NotificationId2)

//        /// <summary>
//        /// Compares two instances of this object.
//        /// </summary>
//        /// <param name="NotificationId1">A notification identification.</param>
//        /// <param name="NotificationId2">Another notification identification.</param>
//        /// <returns>true|false</returns>
//        public static Boolean operator <= (NotificationMessageGroupId NotificationId1, NotificationMessageGroupId NotificationId2)
//            => !(NotificationId1 > NotificationId2);

//        #endregion

//        #region Operator >  (NotificationId1, NotificationId2)

//        /// <summary>
//        /// Compares two instances of this object.
//        /// </summary>
//        /// <param name="NotificationId1">A notification identification.</param>
//        /// <param name="NotificationId2">Another notification identification.</param>
//        /// <returns>true|false</returns>
//        public static Boolean operator > (NotificationMessageGroupId NotificationId1, NotificationMessageGroupId NotificationId2)
//        {

//            if ((Object) NotificationId1 == null)
//                throw new ArgumentNullException(nameof(NotificationId1), "The given NotificationId1 must not be null!");

//            return NotificationId1.CompareTo(NotificationId2) > 0;

//        }

//        #endregion

//        #region Operator >= (NotificationId1, NotificationId2)

//        /// <summary>
//        /// Compares two instances of this object.
//        /// </summary>
//        /// <param name="NotificationId1">A notification identification.</param>
//        /// <param name="NotificationId2">Another notification identification.</param>
//        /// <returns>true|false</returns>
//        public static Boolean operator >= (NotificationMessageGroupId NotificationId1, NotificationMessageGroupId NotificationId2)
//            => !(NotificationId1 < NotificationId2);

//        #endregion

//        #endregion

//        #region IComparable<NotificationId> Members

//        #region CompareTo(Object)

//        /// <summary>
//        /// Compares two instances of this object.
//        /// </summary>
//        /// <param name="Object">An object to compare with.</param>
//        public override Int32 CompareTo(Object Object)
//        {

//            if (Object == null)
//                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

//            if (!(Object is NotificationMessageGroupId NotificationId))
//                throw new ArgumentException("The given object is not a notification identification!");

//            return CompareTo(NotificationId);

//        }

//        #endregion

//        #region CompareTo(NotificationId)

//        /// <summary>
//        /// Compares two instances of this object.
//        /// </summary>
//        /// <param name="NotificationId">An object to compare with.</param>
//        public Int32 CompareTo(NotificationMessageGroupId NotificationId)
//        {

//            if ((Object) NotificationId == null)
//                throw new ArgumentNullException(nameof(NotificationId),  "The given notification identification must not be null!");

//            return String.Compare(InternalId, NotificationId.InternalId, StringComparison.Ordinal);

//        }

//        #endregion

//        #endregion

//        #region IEquatable<NotificationId> Members

//        #region Equals(Object)

//        /// <summary>
//        /// Compares two instances of this object.
//        /// </summary>
//        /// <param name="Object">An object to compare with.</param>
//        /// <returns>true|false</returns>
//        public override Boolean Equals(Object Object)
//        {

//            if (Object == null)
//                return false;

//            if (!(Object is NotificationMessageGroupId NotificationId))
//                return false;

//            return Equals(NotificationId);

//        }

//        #endregion

//        #region Equals(NotificationId)

//        /// <summary>
//        /// Compares two notification identifications for equality.
//        /// </summary>
//        /// <param name="NotificationId">An notification identification to compare with.</param>
//        /// <returns>True if both match; False otherwise.</returns>
//        public Boolean Equals(NotificationMessageGroupId NotificationId)
//        {

//            if ((Object) NotificationId == null)
//                return false;

//            return InternalId.Equals(NotificationId.InternalId, StringComparison.OrdinalIgnoreCase);

//        }

//        #endregion

//        #endregion

//        #region (override) GetHashCode()

//        /// <summary>
//        /// Return the HashCode of this object.
//        /// </summary>
//        /// <returns>The HashCode of this object.</returns>
//        public override Int32 GetHashCode()
//            => InternalId.ToLower().GetHashCode();

//        #endregion

//        #region (override) ToString()

//        /// <summary>
//        /// Return a text representation of this object.
//        /// </summary>
//        public override String ToString()
//            => InternalId ?? "";

//        #endregion

//    }

//}
