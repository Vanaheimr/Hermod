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

using System.Collections;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// A store for all notifications.
    /// </summary>
    public class NotificationStore : IEnumerable<ANotification>
    {

        #region Data

        private readonly List<ANotification> notifications;

        public IEnumerable<ANotification> NotificationTypes
            => notifications;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new notification store.
        /// </summary>
        public NotificationStore(IEnumerable<ANotification>? Notifications = null)
        {

            this.notifications = new List<ANotification>();

            if (Notifications is not null && Notifications.Any())
                notifications.AddRange(Notifications);

        }

        #endregion


        #region Add(NotificationType,                           OnUpdate = null)

        public T Add<T>(T          NotificationType,
                        Action<T>  OnUpdate  = null)

            where T : ANotification

        {

            lock (notifications)
            {

                var notification = notifications.OfType<T>().FirstOrDefault(typeT => typeT.Equals(NotificationType));

                // Create a new notification...
                if (notification == null)
                {
                    notifications.Add(NotificationType);
                    notification = NotificationType;
                    OnUpdate?.Invoke(notification);
                }

                else
                {
                    // When reloaded from disc: Merge notifications.
                    //var Updated = false;

                    // Some optional parameters are different...
                    if (!NotificationType.OptionalEquals(notification))
                    {

                        notifications.Remove(notification);
                        notifications.Add   (NotificationType);

                        OnUpdate?.Invoke(NotificationType);

                    }

                    //else
                    //{

                    //    foreach (var notificationMessageType in NotificationType.NotificationMessageTypes)
                    //    {

                    //        notification.Clear();

                    //        if (!notification.Contains(notificationMessageType))
                    //        {
                    //            notification.Add(notificationMessageType,
                    //                             () => Updated = true);
                    //        }
                    //    }

                    //}

                    //if (Updated)
                    //    OnUpdate?.Invoke(notification);

                }

                return notification;

            }

        }

        #endregion

        #region Add(NotificationType, NotificationMessageType,  OnUpdate = null)

        public T Add<T>(T                        NotificationType,
                        NotificationMessageType  NotificationMessageType,
                        Action<T>                OnUpdate  = null)

            where T : ANotification

        {

            lock (notifications)
            {

                var notification = notifications.OfType<T>().FirstOrDefault(typeT => typeT.Equals(NotificationType));

                if (notification == null)
                {
                    notifications.Add(NotificationType);
                    notification = NotificationType;
                }

                notification.Add(NotificationMessageType,
                                 () => OnUpdate?.Invoke(notification));

                return notification;

            }

        }

        #endregion

        #region Add(NotificationType, NotificationMessageTypes, OnUpdate = null)

        public T Add<T>(T                                     NotificationType,
                        IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                        Action<T>                             OnUpdate  = null)

            where T : ANotification

        {

            lock (notifications)
            {

                var notification = notifications.OfType<T>().FirstOrDefault(typeT => typeT.Equals(NotificationType));

                if (notification == null)
                {
                    notifications.Add(NotificationType);
                    notification = NotificationType;
                }

                notification.Add(NotificationMessageTypes,
                                 () => OnUpdate?.Invoke(notification));

                return notification;

            }

        }

        #endregion

        #region Add(Notifications)

        public void Add<T>(IEnumerable<T>  Notifications)
            where T : ANotification
        {

            lock (notifications)
            {
                notifications.AddRange(Notifications);
            }

        }

        #endregion


        #region GetNotifications  (NotificationMessageType = null)

        public IEnumerable<ANotification> GetNotifications(NotificationMessageType?  NotificationMessageType = null)
        {

            lock (notifications)
            {

                var results = NotificationMessageType.HasValue
                                  ? notifications.Where(typeT => typeT.Contains(NotificationMessageType.Value)).ToArray()
                                  : notifications.ToArray();

                //// When no specialized notification was found... return a general notification!
                //return results.Length > 0
                //           ? results
                //           : _NotificationTypes.Where(typeT => typeT.Count == 0);

                return results;

            }

        }

        #endregion

        #region GetNotificationsOf(params NotificationMessageTypes)

        public IEnumerable<T> GetNotificationsOf<T>(params NotificationMessageType[]  NotificationMessageTypes)

            where T : ANotification

        {

            lock (notifications)
            {

                var results = NotificationMessageTypes != null && NotificationMessageTypes.Length > 0
                                  ? notifications.OfType<T>().Where(typeT => typeT.Contains(NotificationMessageTypes)).ToArray()
                                  : notifications.OfType<T>().ToArray();

                //// When no specialized notification was found... return a general notification!
                //return results.Length > 0
                //           ? results
                //           : _NotificationTypes.OfType<T>().Where(typeT => typeT.Count == 0);

                return results;

            }

        }

        #endregion

        #region GetNotifications  (NotificationMessageTypeFilter)

        public IEnumerable<ANotification> GetNotifications(Func<NotificationMessageType, Boolean>  NotificationMessageTypeFilter)
        {

            lock (notifications)
            {

                if (NotificationMessageTypeFilter == null)
                    NotificationMessageTypeFilter = (_ => true);

                var results = notifications.Where(typeT => typeT.Any(NotificationMessageTypeFilter)).ToArray();

                //// When no specialized notification was found... return a general notification!
                //return results.Length > 0
                //           ? results
                //           : _NotificationTypes.Where(typeT => typeT.Count == 0);

                return results;

            }

        }

        #endregion

        #region GetNotificationsOf(NotificationMessageTypeFilter)

        public IEnumerable<T> GetNotificationsOf<T>(Func<NotificationMessageType, Boolean> NotificationMessageTypeFilter)

            where T : ANotification

        {

            lock (notifications)
            {

                if (NotificationMessageTypeFilter == null)
                    NotificationMessageTypeFilter = (_ => true);

                var results = notifications.OfType<T>().Where(typeT => typeT.Any(NotificationMessageTypeFilter)).ToArray();

                //// When no specialized notification was found... return a general notification!
                //return results.Length > 0
                //           ? results
                //           : _NotificationTypes.OfType<T>().Where(typeT => typeT.Count == 0);

                return results;

            }

        }

        #endregion


        #region Remove(NotificationType, OnRemoval = null)

        public async Task Remove<T>(T          NotificationType,
                                    Action<T>  OnRemoval  = null)

            where T : ANotification

        {

            lock (notifications)
            {
                foreach (var notification in notifications.OfType<T>().Where(typeT => typeT.Equals(NotificationType)).ToArray())
                {
                    notifications.Remove(notification);
                    OnRemoval?.Invoke(notification);
                }
            }

        }

        #endregion


        public JArray ToJSON()

            => new JArray(notifications.
                              OrderBy   (notification => notification.SortKey).
                              SafeSelect(notification => notification.ToJSON(false)));


        public JObject ToJSON(UInt32 Number)

            => notifications.
                  OrderBy(notification => notification.SortKey).
                   Skip(Number - 1).
                   FirstOrDefault()?.
                   ToJSON(false)

                ?? new JObject();

        public IEnumerator<ANotification> GetEnumerator()
        {
            lock (notifications)
            {
                return new List<ANotification>(notifications).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (notifications)
            {
                return new List<ANotification>(notifications).GetEnumerator();
            }
        }

    }

}
