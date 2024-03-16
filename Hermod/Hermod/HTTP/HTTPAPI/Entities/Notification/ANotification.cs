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

using System;
using System.Collections;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

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
        protected readonly HashSet<NotificationMessageType> _NotificationMessageTypes;

        /// <summary>
        /// All notification messages types.
        /// </summary>
        public IEnumerable<NotificationMessageType> NotificationMessageTypes
            => _NotificationMessageTypes;

        /// <summary>
        /// The number of notification messages types.
        /// </summary>
        public Int32 Count
            => _NotificationMessageTypes.Count;

        /// <summary>
        /// Some description to remember why this notification was created.
        /// </summary>
        public          String  Description    { get; }

        /// <summary>
        /// A helper for sorting.
        /// </summary>
        public          String  SortKey        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an new abstract notification.
        /// </summary>
        /// <param name="NotificationMessageTypes">All notification messages types.</param>
        /// <param name="Description">Some description to remember why this notification was created.</param>
        /// <param name="SortKey">A helper for sorting.</param>
        protected ANotification(IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                String                                Description,
                                String                                SortKey)
        {

            this._NotificationMessageTypes  = new HashSet<NotificationMessageType>();

            if (NotificationMessageTypes != null)
                foreach (var notificationMessageType in NotificationMessageTypes)
                    _NotificationMessageTypes.Add(notificationMessageType);

            this.Description                = Description;
            this.SortKey                    = SortKey;

        }

        #endregion


        #region Add     (NotificationMessageType,  OnAdded   = null)

        internal void Add(NotificationMessageType  NotificationMessageType,
                          Action                   OnAdded  = null)
        {
            lock (_NotificationMessageTypes)
            {

                if (!_NotificationMessageTypes.Contains(NotificationMessageType))
                {
                    _NotificationMessageTypes.Add(NotificationMessageType);
                    OnAdded?.Invoke();
                }

            }
        }

        #endregion

        #region Add     (NotificationMessageTypes, OnAdded   = null)

        internal void Add(IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                          Action                                OnAdded  = null)
        {
            lock (_NotificationMessageTypes)
            {

                var Added = false;

                foreach (var NotificationMessageType in NotificationMessageTypes)
                {
                    if (!_NotificationMessageTypes.Contains(NotificationMessageType))
                    {
                        _NotificationMessageTypes.Add(NotificationMessageType);
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

            if (NotificationMessageTypes == null || NotificationMessageTypes.Length == 0)
                return false;

            lock (_NotificationMessageTypes)
            {

                foreach (var notificationMessageType in NotificationMessageTypes)
                {
                    if (_NotificationMessageTypes.Contains(notificationMessageType))
                        return true;
                }

                return false;

            }
        }

        #endregion

        #region IEnumerable<NotificationMessageType> Members

        public IEnumerator<NotificationMessageType> GetEnumerator()
            => _NotificationMessageTypes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _NotificationMessageTypes.GetEnumerator();

        #endregion

        #region Remove  (NotificationMessageType,  OnRemoved = null)

        internal void Remove(NotificationMessageType  NotificationMessageType,
                             Action                   OnRemoved  = null)
        {
            lock (_NotificationMessageTypes)
            {

                if (_NotificationMessageTypes.Contains(NotificationMessageType))
                {
                    _NotificationMessageTypes.Add(NotificationMessageType);
                    OnRemoved?.Invoke();
                }

            }
        }

        #endregion

        #region Remove  (NotificationMessageTypes, OnRemoved = null)

        internal void Remove(IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                             Action                                OnRemoved  = null)
        {
            lock (_NotificationMessageTypes)
            {

                var Removed = false;

                foreach (var NotificationMessageType in NotificationMessageTypes)
                {
                    if (!_NotificationMessageTypes.Contains(NotificationMessageType))
                    {
                        _NotificationMessageTypes.Add(NotificationMessageType);
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
            lock (_NotificationMessageTypes)
            {

                if (_NotificationMessageTypes.Count > 0)
                {
                    _NotificationMessageTypes.Clear();
                    OnCleared?.Invoke();
                }

            }
        }

        #endregion


        public abstract JObject ToJSON(Boolean Embedded = false);


        public abstract Boolean OptionalEquals(ANotification other);


        #region IComparable<ANotification> Members

        public abstract Int32 CompareTo(ANotification other);

        public Int32 CompareTo(Object obj)
            => 0;

        #endregion

        #region IEquatable<ANotification> Members

        public abstract Boolean Equals(ANotification other);

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Get the hashcode of this object.
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
