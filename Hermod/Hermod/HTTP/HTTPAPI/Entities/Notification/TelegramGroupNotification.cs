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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// Extension methods for Telegram group notifications.
    /// </summary>
    public static class TelegramGroupNotificationExtensions
    {

        #region AddTelegramGroupNotification(this HTTPExtAPI, User,                             Username, TextTemplate = null)

        public static Task AddTelegramGroupNotification(this HTTPExtAPI  HTTPExtAPI,
                                                   User           User,
                                                   String         Username,
                                                   Int32?         ChatId         = null,
                                                   String         SharedSecret   = null,
                                                   String         TextTemplate   = null)

            => HTTPExtAPI.AddNotification(User,
                                        new TelegramGroupNotification(Username,
                                                                 ChatId,
                                                                 SharedSecret,
                                                                 TextTemplate));

        #endregion

        #region AddTelegramGroupNotification(this HTTPExtAPI, UserId,                           Username, TextTemplate = null)

        public static Task AddTelegramGroupNotification(this HTTPExtAPI  HTTPExtAPI,
                                                   User_Id        UserId,
                                                   String         Username,
                                                   Int32?         ChatId         = null,
                                                   String         SharedSecret   = null,
                                                   String         TextTemplate   = null)

            => HTTPExtAPI.AddNotification(UserId,
                                        new TelegramGroupNotification(Username,
                                                                 ChatId,
                                                                 SharedSecret,
                                                                 TextTemplate));

        #endregion

        #region AddTelegramGroupNotification(this HTTPExtAPI, User,   NotificationMessageType,  Username, TextTemplate = null)

        public static Task AddTelegramGroupNotification(this HTTPExtAPI            HTTPExtAPI,
                                                   User                     User,
                                                   NotificationMessageType  NotificationMessageType,
                                                   String                   Username,
                                                   Int32?                   ChatId         = null,
                                                   String                   SharedSecret   = null,
                                                   String                   TextTemplate   = null)

            => HTTPExtAPI.AddNotification(User,
                                        new TelegramGroupNotification(Username,
                                                                 ChatId,
                                                                 SharedSecret,
                                                                 TextTemplate),
                                        NotificationMessageType);

        #endregion

        #region AddTelegramGroupNotification(this HTTPExtAPI, UserId, NotificationMessageType,  Username, TextTemplate = null)

        public static Task AddTelegramGroupNotification(this HTTPExtAPI            HTTPExtAPI,
                                                   User_Id                  UserId,
                                                   NotificationMessageType  NotificationMessageType,
                                                   String                   Username,
                                                   Int32?                   ChatId         = null,
                                                   String                   SharedSecret   = null,
                                                   String                   TextTemplate   = null)

            => HTTPExtAPI.AddNotification(UserId,
                                        new TelegramGroupNotification(Username,
                                                                 ChatId,
                                                                 SharedSecret,
                                                                 TextTemplate),
                                        NotificationMessageType);

        #endregion

        #region AddTelegramGroupNotification(this HTTPExtAPI, User,   NotificationMessageTypes, Username, TextTemplate = null)

        public static Task AddTelegramGroupNotification(this HTTPExtAPI                         HTTPExtAPI,
                                                   User                                  User,
                                                   IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                                   String                                Username,
                                                   Int32?                                ChatId         = null,
                                                   String                                SharedSecret   = null,
                                                   String                                TextTemplate   = null)

            => HTTPExtAPI.AddNotification(User,
                                        new TelegramGroupNotification(Username,
                                                                 ChatId,
                                                                 SharedSecret,
                                                                 TextTemplate),
                                        NotificationMessageTypes);

        #endregion

        #region AddTelegramGroupNotification(this HTTPExtAPI, UserId, NotificationMessageTypes, Username, TextTemplate = null)

        public static Task AddTelegramGroupNotification(this HTTPExtAPI                         HTTPExtAPI,
                                                   User_Id                               UserId,
                                                   IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                                   String                                Username,
                                                   Int32?                                ChatId         = null,
                                                   String                                SharedSecret   = null,
                                                   String                                TextTemplate   = null)

            => HTTPExtAPI.AddNotification(UserId,
                                        new TelegramGroupNotification(Username,
                                                                 ChatId,
                                                                 SharedSecret,
                                                                 TextTemplate),
                                        NotificationMessageTypes);

        #endregion


        #region GetTelegramGroupNotifications(this HTTPExtAPI, User,           params NotificationMessageTypes)

        public static IEnumerable<TelegramGroupNotification> GetTelegramGroupNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                                 User                              User,
                                                                                 params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<TelegramGroupNotification>(User,
                                                            NotificationMessageTypes);

        #endregion

        #region GetTelegramGroupNotifications(this HTTPExtAPI, UserId,         params NotificationMessageTypes)

        public static IEnumerable<TelegramGroupNotification> GetTelegramGroupNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                                 User_Id                           UserId,
                                                                                 params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<TelegramGroupNotification>(UserId,
                                                                 NotificationMessageTypes);

        #endregion

        #region GetTelegramGroupNotifications(this HTTPExtAPI, Organization,   params NotificationMessageTypes)

        public static IEnumerable<TelegramGroupNotification> GetTelegramGroupNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                                 Organization                      Organization,
                                                                                 params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<TelegramGroupNotification>(Organization,
                                                            NotificationMessageTypes);

        #endregion

        #region GetTelegramGroupNotifications(this HTTPExtAPI, OrganizationId, params NotificationMessageTypes)

        public static IEnumerable<TelegramGroupNotification> GetTelegramGroupNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                                 Organization_Id                   OrganizationId,
                                                                                 params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<TelegramGroupNotification>(OrganizationId,
                                                                 NotificationMessageTypes);

        #endregion


        //public static Notifications UnregisterTelegramGroupNotification(this HTTPExtAPI  HTTPExtAPI,
        //                                                      User           User,
        //                                                      Username   Username)

        //    => HTTPExtAPI.UnregisterNotification<TelegramGroupNotification>(User,
        //                                                        a => a.Username == Username);

        //public static Notifications UnregisterTelegramGroupNotification(this HTTPExtAPI  HTTPExtAPI,
        //                                                      User_Id        User,
        //                                                      Username   Username)

        //    => HTTPExtAPI.UnregisterNotification<TelegramGroupNotification>(User,
        //                                                        a => a.Username == Username);


        //public static Notifications UnregisterTelegramGroupNotification(this HTTPExtAPI    HTTPExtAPI,
        //                                                      User             User,
        //                                                      NotificationMessageType  NotificationMessageType,
        //                                                      Username     Username)

        //    => HTTPExtAPI.UnregisterNotification<TelegramGroupNotification>(User,
        //                                                        NotificationMessageType,
        //                                                        a => a.Username == Username);

        //public static Notifications UnregisterTelegramGroupNotification(this HTTPExtAPI    HTTPExtAPI,
        //                                                      User_Id          User,
        //                                                      NotificationMessageType  NotificationMessageType,
        //                                                      Username     Username)

        //    => HTTPExtAPI.UnregisterNotification<TelegramGroupNotification>(User,
        //                                                        NotificationMessageType,
        //                                                        a => a.Username == Username);

    }

    /// <summary>
    /// A Telegram group notification.
    /// </summary>
    public class TelegramGroupNotification : ANotification,
                                             IEquatable <TelegramGroupNotification>,
                                             IComparable<TelegramGroupNotification>
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
        public const String JSONLDContext = "https://opendata.social/contexts/HTTPExtAPI/TelegramGroupNotification";

        #endregion

        #region Properties

        /// <summary>
        /// The Telegram group name of this Telegram notification.
        /// </summary>
        public String  GroupName       { get; }

        /// <summary>
        /// The Telegram chat identification of this Telegram notification.
        /// </summary>
        public Int32?  ChatId          { get; }

        /// <summary>
        /// The Telegram shared secret of this Telegram notification.
        /// </summary>
        public String  SharedSecret    { get; }

        /// <summary>
        /// An optional text template for the SMS notification.
        /// </summary>
        public String  TextTemplate    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new Telegram group notification.
        /// </summary>
        /// <param name="GroupName">The Telegram group name of this Telegram notification.</param>
        /// <param name="TextTemplate">An optional text template for the SMS notification.</param>
        /// <param name="ChatId">The Telegram chat identification of this Telegram notification.</param>
        /// <param name="SharedSecret">The Telegram shared secret of this Telegram notification.</param>
        /// <param name="NotificationMessageTypes">An optional enumeration of notification message types.</param>
        /// <param name="Description">Some description to remember why this notification was created.</param>
        public TelegramGroupNotification(String                                GroupName,
                                         Int32?                                ChatId                     = null,
                                         String                                SharedSecret               = null,
                                         String                                TextTemplate               = null,
                                         IEnumerable<NotificationMessageType>  NotificationMessageTypes   = null,
                                         String                                Description                = null)

            : base(NotificationMessageTypes,
                   Description,
                   String.Concat(nameof(TelegramGroupNotification),
                                 GroupName,
                                 ChatId ?? 0,
                                 SharedSecret,
                                 TextTemplate))

        {

            this.GroupName     = GroupName;
            this.ChatId        = ChatId;
            this.SharedSecret  = SharedSecret;
            this.TextTemplate  = TextTemplate;

        }

        #endregion


        #region Parse   (JSON)

        public static TelegramGroupNotification Parse(JObject JSON)
        {

            if (TryParse(JSON, out TelegramGroupNotification Notification))
                return Notification;

            return null;

        }

        #endregion

        #region TryParse(JSON, out Notification)

        public static Boolean TryParse(JObject JSON, out TelegramGroupNotification Notification)
        {

            var GroupName = JSON["groupName"]?.Value<String>();

            if (JSON["@context"]?.Value<String>() == JSONLDContext && GroupName.IsNeitherNullNorEmpty())
            {

                Notification = new TelegramGroupNotification(GroupName,
                                                             JSON["chatId"]?.      Value<Int32>(),
                                                             JSON["sharedSecret"]?.Value<String>(),
                                                             JSON["textTemplate"]?.Value<String>(),
                                                            (JSON["messageTypes"] as JArray)?.SafeSelect(element => NotificationMessageType.Parse(element.Value<String>())),
                                                             JSON["description" ]?.Value<String>());

                return true;

            }

            Notification = null;
            return false;

        }

        #endregion

        #region ToJSON(Embedded = false)

        public override JObject ToJSON(Boolean Embedded = false)

            => JSONObject.Create(

                   !Embedded
                       ? new JProperty("@context",      JSONLDContext.ToString())
                       : null,

                   new JProperty("groupName",           GroupName),

                   ChatId.HasValue
                       ? new JProperty("chatId",        ChatId.Value)
                       : null,

                   SharedSecret.IsNotNullOrEmpty()
                       ? new JProperty("sharedSecret",  SharedSecret)
                       : null,

                   TextTemplate.IsNotNullOrEmpty()
                       ? new JProperty("textTemplate",  TextTemplate)
                       : null,

                   NotificationMessageTypes.SafeAny()
                       ? new JProperty("messageTypes",  new JArray(NotificationMessageTypes.Select(msgType => msgType.ToString())))
                       : null,

                   Description.IsNotNullOrEmpty()
                       ? new JProperty("description",   Description)
                       : null

               );

        #endregion


        #region OptionalEquals(TelegramGroupNotification)

        public override Boolean OptionalEquals(ANotification other)

            => other is TelegramGroupNotification TelegramGroupNotification &&
               this.OptionalEquals(TelegramGroupNotification);

        public Boolean OptionalEquals(TelegramGroupNotification other)

            => String.Equals(GroupName,    other.GroupName)    &&
               String.Equals(Description,  other.Description)  &&
               String.Equals(TextTemplate, other.TextTemplate) &&

               _NotificationMessageTypes.SetEquals(other._NotificationMessageTypes);

        #endregion


        #region IComparable<TelegramGroupNotification> Members

        #region CompareTo(ANotification)

        public override Int32 CompareTo(ANotification other)
            => SortKey.CompareTo(other.SortKey);

        #endregion

        #region CompareTo(TelegramGroupNotification)

        public Int32 CompareTo(TelegramGroupNotification other)
            => GroupName.CompareTo(other.GroupName);

        #endregion

        #endregion

        #region IEquatable<TelegramGroupNotification> Members

        #region Equals(ANotification)

        public override Boolean Equals(ANotification other)
            => SortKey.Equals(other.SortKey);

        #endregion

        #region Equals(TelegramGroupNotification)

        public Boolean Equals(TelegramGroupNotification other)
            => GroupName.Equals(other.GroupName);

        #endregion

        #endregion

        #region GetHashCode()

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
            => String.Concat(nameof(TelegramGroupNotification), ": ", GroupName.ToString());

        #endregion

    }

}
