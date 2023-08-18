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

using Newtonsoft.Json.Linq;
using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Illias;
using Org.BouncyCastle.Bcpg.OpenPgp;
using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{
    public interface IUser : IEntity<User_Id>,
                             IEquatable<IUser>,
                             IComparable<IUser>,
                             IComparable
    {

        HTTPExtAPI? API { get; set; }




        /// <summary>
        /// The primary E-Mail address of the user.
        /// </summary>
        [Mandatory]
        public EMailAddress               EMail                { get; }

        ///// <summary>
        ///// The offical public name of the user.
        ///// </summary>
        //[Optional]
        //public String                     Name                 { get; }

        /// <summary>
        /// The language setting of the user.
        /// </summary>
        [Mandatory]
        public Languages                  UserLanguage         { get; }

        ///// <summary>
        ///// An optional (multi-language) description of the user.
        ///// </summary>
        //[Optional]
        //public I18NString                 Description          { get; }

        /// <summary>
        /// Timestamp when the user accepted the End-User-License-Agreement.
        /// </summary>
        [Mandatory]
        public DateTime?                  AcceptedEULA         { get; }

        /// <summary>
        /// The user will not be shown in user listings, as its
        /// primary e-mail address is not yet authenticated.
        /// </summary>
        [Mandatory]
        public Boolean                    IsAuthenticated      { get; }

        /// <summary>
        /// The user is disabled.
        /// </summary>
        [Mandatory]
        public Boolean                    IsDisabled           { get; }

















        IEnumerable<User2UserEdge> __User2UserEdges { get; }
        Address? Address { get; }
        IEnumerable<AttachedFile> AttachedFiles { get; }
        IEnumerable<IUser> FollowsUsers { get; }
        IEnumerable<IUser> Genimi { get; }
        GeoCoordinate? GeoLocation { get; }
        string? Homepage { get; }
        IEnumerable<IUser> IsFollowedBy { get; }
        PhoneNumber? MobilePhone { get; }
        PrivacyLevel PrivacyLevel { get; }
        PgpPublicKeyRing? PublicKeyRing { get; }
        PgpSecretKeyRing? SecretKeyRing { get; }
        string? Telegram { get; }
        PhoneNumber? Telephone { get; }
        Use2AuthFactor Use2AuthFactor { get; }
        IEnumerable<User2UserGroupEdge> User2Group_OutEdges { get; }
        IEnumerable<User2OrganizationEdge> User2Organization_OutEdges { get; }
        IEnumerable<User2OrganizationEdge> Add(IEnumerable<User2OrganizationEdge> Edges);
        IEnumerable<User2UserEdge> Add(IEnumerable<User2UserEdge> Edges);
        IEnumerable<User2UserGroupEdge> Add(IEnumerable<User2UserGroupEdge> Edges);
        User2OrganizationEdge Add(User2OrganizationEdge Edge);
        User2UserEdge Add(User2UserEdge Edge);
        User2UserGroupEdge Add(User2UserGroupEdge Edge);
        User2UserEdge AddIncomingEdge(IUser SourceUser, User2UserEdgeTypes EdgeLabel, PrivacyLevel PrivacyLevel = PrivacyLevel.World);
        User2UserEdge AddIncomingEdge(User2UserEdge Edge);
        User2OrganizationEdge AddOutgoingEdge(User2OrganizationEdgeLabel EdgeLabel, IOrganization Target, PrivacyLevel PrivacyLevel = PrivacyLevel.World);
        User2UserEdge AddOutgoingEdge(User2UserEdge Edge);
        User2UserEdge AddOutgoingEdge(User2UserEdgeTypes EdgeLabel, IUser Target, PrivacyLevel PrivacyLevel = PrivacyLevel.World);
        User2UserGroupEdge AddToUserGroup(User2UserGroupEdgeLabel EdgeLabel, IUserGroup Target, PrivacyLevel PrivacyLevel = PrivacyLevel.World);
        User Clone(User_Id? NewUserId = null);
        int CompareTo(User User);
        void CopyAllLinkedDataFrom(IUser OldUser);
        IEnumerable<User2OrganizationEdgeLabel> EdgeLabels(IOrganization Organization);
        IEnumerable<User2UserGroupEdgeLabel> EdgeLabels(UserGroup UserGroup);
        IEnumerable<User2OrganizationEdge> Edges(IOrganization Organization);
        IEnumerable<User2OrganizationEdge> Edges(IOrganization Organization, User2OrganizationEdgeLabel EdgeLabel);
        IEnumerable<User2UserGroupEdge> Edges(User2UserGroupEdgeLabel EdgeLabel, UserGroup UserGroup);
        IEnumerable<User2UserGroupEdge> Edges(UserGroup UserGroup);
        bool Equals(object? Object);
        bool Equals(User User);
        int GetHashCode();
        JObject GetNotificationInfo(uint NotificationId);
        JObject GetNotificationInfos();
        IEnumerable<ANotification> GetNotifications(NotificationMessageType? NotificationMessageType = null);
        IEnumerable<ANotification> GetNotifications(Func<NotificationMessageType, bool> NotificationMessageTypeFilter);
        IEnumerable<T> GetNotificationsOf<T>(Func<NotificationMessageType, bool> NotificationMessageTypeFilter) where T : ANotification;
        IEnumerable<T> GetNotificationsOf<T>(params NotificationMessageType[] NotificationMessageTypes) where T : ANotification;
        bool HasAccessToOrganization(Access_Levels AccessLevel, IOrganization Organization, bool Recursive = true);
        bool HasAccessToOrganization(Access_Levels AccessLevel, Organization_Id OrganizationId, bool Recursive = true);
        bool HasEdge(User2UserGroupEdgeLabel EdgeLabel, UserGroup UserGroup);
        IEnumerable<IOrganization> Organizations(Access_Levels AccessLevel, bool Recursive = true);
        IEnumerable<IOrganization> ParentOrganizations();
        bool RemoveOutEdge(User2OrganizationEdge Edge);
        bool RemoveOutEdge(User2UserGroupEdge Edge);
        User.Builder ToBuilder(User_Id? NewUserId = null);
        JObject ToJSON(bool Embedded = false, InfoStatus ExpandOrganizations = InfoStatus.Hidden, InfoStatus ExpandGroups = InfoStatus.Hidden, bool IncludeLastChange = true, CustomJObjectSerializerDelegate<User>? CustomUserSerializer = null);
        string ToString();
        IEnumerable<User2UserGroupEdge> User2GroupOutEdges(Func<User2UserGroupEdgeLabel, bool> User2GroupEdgeFilter);
        IEnumerable<IUserGroup> UserGroups(bool RequireReadWriteAccess = false, bool Recursive = false);
        IEnumerable<IUserGroup> UserGroups(User2UserGroupEdgeLabel EdgeFilter);


        T AddNotification<T>(T                                     Notification,
                             Action<T>?                            OnUpdate   = null)

            where T : ANotification;

        T AddNotification<T>(T                                     Notification,
                             NotificationMessageType               NotificationMessageType,
                             Action<T>?                            OnUpdate   = null)

            where T : ANotification;

        T AddNotification<T>(T                                     Notification,
                             IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                             Action<T>?                            OnUpdate   = null)

            where T : ANotification;


        Task RemoveNotification<T>(T           NotificationType,
                                   Action<T>?  OnRemoval   = null)

            where T : ANotification;

    }

}
