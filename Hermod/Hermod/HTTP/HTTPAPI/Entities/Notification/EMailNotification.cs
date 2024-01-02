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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// Extension methods for e-mail notifications.
    /// </summary>
    public static class EMailNotificationExtensions
    {

        #region AddEMailNotification(this HTTPExtAPI, User, NotificationMessageType,  EMailAddress = null, Subject = null, SubjectPrefix = null)

        public static Task AddEMailNotification(this HTTPExtAPI            HTTPExtAPI,
                                                IUser                    User,
                                                NotificationMessageType  NotificationMessageType,
                                                EMailAddress?            EMailAddress      = null,
                                                String?                  Subject           = null,
                                                String?                  SubjectPrefix     = null,
                                                EventTracking_Id?        EventTrackingId   = null,
                                                User_Id?                 CurrentUserId     = null)

            => HTTPExtAPI.AddNotification(User,
                                        new EMailNotification(
                                            EMailAddress ?? User.EMail,
                                            Subject,
                                            SubjectPrefix
                                        ),
                                        NotificationMessageType,
                                        EventTrackingId,
                                        CurrentUserId);

        #endregion

        #region AddEMailNotification(this HTTPExtAPI, User, NotificationMessageTypes, EMailAddress = null, Subject = null, SubjectPrefix = null)

        public static Task AddEMailNotification(this HTTPExtAPI                         HTTPExtAPI,
                                                IUser                                 User,
                                                IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                                EMailAddress?                         EMailAddress      = null,
                                                String?                               Subject           = null,
                                                String?                               SubjectPrefix     = null,
                                                EventTracking_Id?                     EventTrackingId   = null,
                                                User_Id?                              CurrentUserId     = null)

            => HTTPExtAPI.AddNotification(User,
                                        new EMailNotification(
                                            EMailAddress ?? User.EMail,
                                            Subject,
                                            SubjectPrefix
                                        ),
                                        NotificationMessageTypes,
                                        EventTrackingId,
                                        CurrentUserId);

        #endregion


        #region GetEMailNotifications(this HTTPExtAPI, User,         params NotificationMessageTypes)

        public static IEnumerable<EMailNotification> GetEMailNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                           User                              User,
                                                                           params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<EMailNotification>(User,
                                                              NotificationMessageTypes);

        #endregion

        #region GetEMailNotifications(this HTTPExtAPI, Organization, params NotificationMessageTypes)

        public static IEnumerable<EMailNotification> GetEMailNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                           Organization                      Organization,
                                                                           params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<EMailNotification>(Organization,
                                                              NotificationMessageTypes);

        #endregion

        #region GetEMailNotifications(this HTTPExtAPI, UserGroup,    params NotificationMessageTypes)

        public static IEnumerable<EMailNotification> GetEMailNotifications(this HTTPExtAPI                     HTTPExtAPI,
                                                                           UserGroup                         UserGroup,
                                                                           params NotificationMessageType[]  NotificationMessageTypes)


            => HTTPExtAPI.GetNotificationsOf<EMailNotification>(UserGroup,
                                                              NotificationMessageTypes);

        #endregion


        //public static Notifications UnregisterEMailNotification(this HTTPExtAPI  HTTPExtAPI,
        //                                                        User           User,
        //                                                        EMailAddress   EMailAddress)

        //    => HTTPExtAPI.UnregisterNotification<EMailNotification>(User,
        //                                                          a => a.EMailAddress == EMailAddress);

        //public static Notifications UnregisterEMailNotification(this HTTPExtAPI  HTTPExtAPI,
        //                                                        User_Id        User,
        //                                                        EMailAddress   EMailAddress)

        //    => HTTPExtAPI.UnregisterNotification<EMailNotification>(User,
        //                                                          a => a.EMailAddress == EMailAddress);


        //public static Notifications UnregisterEMailNotification(this HTTPExtAPI    HTTPExtAPI,
        //                                                        User             User,
        //                                                        NotificationMessageType  NotificationMessageType,
        //                                                        EMailAddress     EMailAddress)

        //    => HTTPExtAPI.UnregisterNotification<EMailNotification>(User,
        //                                                          NotificationMessageType,
        //                                                          a => a.EMailAddress == EMailAddress);

        //public static Notifications UnregisterEMailNotification(this HTTPExtAPI    HTTPExtAPI,
        //                                                        User_Id          User,
        //                                                        NotificationMessageType  NotificationMessageType,
        //                                                        EMailAddress     EMailAddress)

        //    => HTTPExtAPI.UnregisterNotification<EMailNotification>(User,
        //                                                          NotificationMessageType,
        //                                                          a => a.EMailAddress == EMailAddress);

    }


    /// <summary>
    /// An E-Mail notification.
    /// </summary>
    public class EMailNotification : ANotification,
                                     IEquatable <EMailNotification>,
                                     IComparable<EMailNotification>
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
        public const String JSONLDContext = "https://opendata.social/contexts/UsersAPI/EMailNotification";

        #endregion

        #region Properties

        /// <summary>
        /// The e-mail address of the receiver of this notification.
        /// </summary>
        public EMailAddress  EMailAddress     { get; }

        /// <summary>
        /// An optional customer-specific subject of all e-mails to send.
        /// </summary>
        public String        Subject          { get; }

        /// <summary>
        /// An optional prefix added to the standard subject of the e-mail notification.
        /// </summary>
        public String        SubjectPrefix    { get; }

        /// <summary>
        /// An optional 'List-Id" e-mail header within all e-mails to send.
        /// </summary>
        public String        ListId           { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new e-mail notification.
        /// </summary>
        /// <param name="EMailAddress">The e-mail address of the receiver of this notification.</param>
        /// <param name="Subject">An optional customer-specific subject of all e-mails to send.</param>
        /// <param name="SubjectPrefix">An optional prefix added to the standard subject of all e-mails to send.</param>
        /// <param name="ListId">An optional 'List-Id" e-mail header within all e-mails to send.</param>
        /// <param name="NotificationMessageTypes">An optional enumeration of notification message types.</param>
        /// <param name="Description">Some description to remember why this notification was created.</param>
        public EMailNotification(EMailAddress                          EMailAddress,
                                 String                                Subject                    = null,
                                 String                                SubjectPrefix              = null,
                                 String                                ListId                     = null,
                                 IEnumerable<NotificationMessageType>  NotificationMessageTypes   = null,
                                 String                                Description                = null)

            : base(NotificationMessageTypes,
                   Description,
                   String.Concat(nameof(EMailNotification),
                                 EMailAddress,
                                 Subject,
                                 SubjectPrefix,
                                 ListId))

        {

            this.EMailAddress   = EMailAddress;
            this.Subject        = Subject;
            this.SubjectPrefix  = SubjectPrefix;
            this.ListId         = ListId;

        }

        #endregion


        #region Parse   (JSON)

        public static EMailNotification Parse(JObject JSON)
        {

            if (TryParse(JSON, out EMailNotification Notification))
                return Notification;

            return null;

        }

        #endregion

        #region TryParse(JSON, out Notification)

        public static Boolean TryParse(JObject JSON, out EMailNotification Notification)
        {

            if (JSON["@context"]?.Value<String>() == JSONLDContext &&
                JSON["email"] is JObject EMailJSON &&
                EMailAddress.TryParseJSON(EMailJSON,
                                          out EMailAddress EMail,
                                          out String       ErrorResponse,
                                          true))
            {

                Notification = new EMailNotification(EMail,
                                                     JSON["subject"      ]?.Value<String>(),
                                                     JSON["subjectPrefix"]?.Value<String>(),
                                                     JSON["listId"       ]?.Value<String>(),
                                                     (JSON["messageTypes"] as JArray)?.SafeSelect(element => NotificationMessageType.Parse(element.Value<String>())),
                                                     JSON["description"  ]?.Value<String>());

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
                       ? new JProperty("@context",       JSONLDContext.ToString())
                       : null,

                   new JProperty("email",                EMailAddress.ToJSON(Embedded: true)),

                   Subject.IsNotNullOrEmpty()
                       ? new JProperty("subject",        Subject)
                       : null,

                   SubjectPrefix.IsNotNullOrEmpty()
                       ? new JProperty("subjectPrefix",  SubjectPrefix)
                       : null,

                   ListId.IsNotNullOrEmpty()
                       ? new JProperty("listId",         ListId)
                       : null,

                   NotificationMessageTypes.SafeAny()
                       ? new JProperty("messageTypes",   new JArray(NotificationMessageTypes.Select(msgType => msgType.ToString())))
                       : null,

                   Description.IsNotNullOrEmpty()
                       ? new JProperty("description",    Description)
                       : null

               );

        #endregion


        #region OptionalEquals(EMailNotification)

        public override Boolean OptionalEquals(ANotification other)

            => other is EMailNotification eMailNotification &&
               this.OptionalEquals(eMailNotification);

        public Boolean OptionalEquals(EMailNotification other)

            => EMailAddress.Equals(other.EMailAddress)           &&

               String.Equals(Subject,       other.Subject)       &&
               String.Equals(SubjectPrefix, other.SubjectPrefix) &&
               String.Equals(ListId,        other.ListId)        &&

               String.Equals(Description,   other.Description)   &&

               _NotificationMessageTypes.SetEquals(other._NotificationMessageTypes);

        #endregion


        #region IComparable<EMailNotification> Members

        #region CompareTo(ANotification)

        public override Int32 CompareTo(ANotification other)
            => SortKey.CompareTo(other.SortKey);

        #endregion

        #region CompareTo(EMailNotification)

        public Int32 CompareTo(EMailNotification other)
            => EMailAddress.CompareTo(other.EMailAddress);

        #endregion

        #endregion

        #region IEquatable<EMailNotification> Members

        #region Equals(ANotification)

        public override Boolean Equals(ANotification other)
            => SortKey.Equals(other.SortKey);

        #endregion

        #region Equals(EMailNotification)

        public Boolean Equals(EMailNotification other)
            => EMailAddress.Equals(other.EMailAddress);

        #endregion

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
            => String.Concat(nameof(EMailNotification), ": ", EMailAddress.ToString());

        #endregion

    }

}
