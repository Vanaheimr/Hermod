/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Collections;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// An abstract notification.
    /// </summary>
    public abstract class ANotification : IEnumerable<NotificationMessageType>,
                                          IEquatable <ANotification>,
                                          IComparable<ANotification>,
                                          IComparable
    {

        #region Properties

        /// <summary>
        /// All notification messages types.
        /// </summary>
        protected readonly HashSet<NotificationMessageType> notificationMessageTypes;

        /// <summary>
        /// All notification messages types.
        /// </summary>
        public IEnumerable<NotificationMessageType> NotificationMessageTypes
            => notificationMessageTypes;

        /// <summary>
        /// The number of notification messages types.
        /// </summary>
        public Int32 Count
            => notificationMessageTypes.Count;

        /// <summary>
        /// Some description to remember why this notification was created.
        /// </summary>
        public          I18NString  Description    { get; }

        /// <summary>
        /// A helper for sorting.
        /// </summary>
        public          String      SortKey        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an new abstract notification.
        /// </summary>
        /// <param name="NotificationMessageTypes">All notification messages types.</param>
        /// <param name="Description">Some description to remember why this notification was created.</param>
        /// <param name="SortKey">A helper for sorting.</param>
        protected ANotification(IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                I18NString?                           Description,
                                String                                SortKey)
        {

            this.notificationMessageTypes  = [.. NotificationMessageTypes];
            this.Description               = Description ?? I18NString.Empty;
            this.SortKey                   = SortKey;

        }

        #endregion


        #region Add     (NotificationMessageType,  OnAdded   = null)

        internal void Add(NotificationMessageType  NotificationMessageType,
                          Action?                   OnAdded  = null)
        {
            lock (notificationMessageTypes)
            {

                if (!notificationMessageTypes.Contains(NotificationMessageType))
                {
                    notificationMessageTypes.Add(NotificationMessageType);
                    OnAdded?.Invoke();
                }

            }
        }

        #endregion

        #region Add     (NotificationMessageTypes, OnAdded   = null)

        internal void Add(IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                          Action?                                OnAdded  = null)
        {
            lock (notificationMessageTypes)
            {

                var Added = false;

                foreach (var NotificationMessageType in NotificationMessageTypes)
                {
                    if (!notificationMessageTypes.Contains(NotificationMessageType))
                    {
                        notificationMessageTypes.Add(NotificationMessageType);
                        Added = true;
                    }
                }

                if (Added)
                    OnAdded?.Invoke();

            }
        }

        #endregion

        #region Contains(params NotificationMessageTypes)

        public Boolean Contains(params NotificationMessageType[] NotificationMessageTypes)
        {

            if (NotificationMessageTypes is null || NotificationMessageTypes.Length == 0)
                return false;

            lock (notificationMessageTypes)
            {

                foreach (var notificationMessageType in NotificationMessageTypes)
                {
                    if (notificationMessageTypes.Contains(notificationMessageType))
                        return true;
                }

                return false;

            }
        }

        #endregion

        #region IEnumerable<NotificationMessageType> Members

        public IEnumerator<NotificationMessageType> GetEnumerator()
            => notificationMessageTypes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => notificationMessageTypes.GetEnumerator();

        #endregion

        #region Remove  (NotificationMessageType,  OnRemoved = null)

        internal void Remove(NotificationMessageType  NotificationMessageType,
                             Action?                   OnRemoved  = null)
        {
            lock (notificationMessageTypes)
            {

                if (notificationMessageTypes.Contains(NotificationMessageType))
                {
                    notificationMessageTypes.Add(NotificationMessageType);
                    OnRemoved?.Invoke();
                }

            }
        }

        #endregion

        #region Remove  (NotificationMessageTypes, OnRemoved = null)

        internal void Remove(IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                             Action?                                OnRemoved  = null)
        {
            lock (notificationMessageTypes)
            {

                var Removed = false;

                foreach (var NotificationMessageType in NotificationMessageTypes)
                {
                    if (!notificationMessageTypes.Contains(NotificationMessageType))
                    {
                        notificationMessageTypes.Add(NotificationMessageType);
                        Removed = true;
                    }
                }

                if (Removed)
                    OnRemoved?.Invoke();

            }
        }

        #endregion

        #region Clear   (OnCleared = null)

        internal void Clear(Action? OnCleared = null)
        {
            lock (notificationMessageTypes)
            {

                if (notificationMessageTypes.Count > 0)
                {
                    notificationMessageTypes.Clear();
                    OnCleared?.Invoke();
                }

            }
        }

        #endregion


        public abstract JObject ToJSON(Boolean Embedded = false);


        public abstract Boolean OptionalEquals(ANotification other);




        public static bool operator ==(ANotification left, ANotification right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(ANotification left, ANotification right)
        {
            return !(left == right);
        }

        public static bool operator <(ANotification left, ANotification right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(ANotification left, ANotification right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(ANotification left, ANotification right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(ANotification left, ANotification right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }



        #region IComparable<ANotification> Members

        public abstract Int32 CompareTo(ANotification? other);

        public abstract Int32 CompareTo(Object? obj);

        #endregion

        #region IEquatable<ANotification> Members

        //public abstract Boolean Equals(Object? obj);

        public abstract Boolean Equals(ANotification? other);

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Get the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => SortKey.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => SortKey;

        #endregion

    }

}
