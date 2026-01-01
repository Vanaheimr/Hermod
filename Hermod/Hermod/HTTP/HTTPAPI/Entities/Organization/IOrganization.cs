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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Illias;

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public interface IOrganization : IEntity<Organization_Id>,
                                     IEquatable<IOrganization>,
                                     IComparable<IOrganization>,
                                     IComparable
    {

        HTTPExtAPI?  API  { get; set; }
        HTTPExtAPIX? APIX { get; set; }

        JSONLDContext JSONLDContext { get; }

        Address? Address { get; }
        IEnumerable<IUser> Admins { get; }
        IEnumerable<AttachedFile> AttachedFiles { get; }
        EMailAddress? EMail { get; }
        GeoCoordinate? GeoLocation { get; }
        IEnumerable<IUser> Guests { get; }
        bool IsDisabled { get; }
        IEnumerable<IUser> Members { get; }
        IEnumerable<Organization2OrganizationEdge> Organization2OrganizationInEdges { get; }
        IEnumerable<Organization2OrganizationEdge> Organization2OrganizationOutEdges { get; }
        IEnumerable<IOrganization> ParentOrganizations { get; }
        IEnumerable<IOrganization> SubOrganizations { get; }
        Tags? Tags { get; }
        PhoneNumber? Telephone { get; }
        IEnumerable<User2OrganizationEdge> User2OrganizationEdges { get; }
        IEnumerable<IUser> Users { get; }
        string? Website { get; }


        

        Organization2OrganizationEdge AddEdge(Organization2OrganizationEdge Edge);
        IEnumerable<Organization2OrganizationEdge> AddEdges(IEnumerable<Organization2OrganizationEdge> Edges);
        Organization2OrganizationEdge AddInEdge(Organization2OrganizationEdgeLabel EdgeLabel, IOrganization SourceOrganization, PrivacyLevel PrivacyLevel = PrivacyLevel.World);
        Organization2OrganizationEdge AddOutEdge(Organization2OrganizationEdgeLabel EdgeLabel, IOrganization TargetOrganization, PrivacyLevel PrivacyLevel = PrivacyLevel.World);
        User2OrganizationEdge AddUser(IUser Source, User2OrganizationEdgeLabel EdgeLabel, PrivacyLevel PrivacyLevel = PrivacyLevel.World);
        User2OrganizationEdge AddUser(User2OrganizationEdge Edge);
        IEnumerable<User2OrganizationEdge> AddUsers(IEnumerable<User2OrganizationEdge> Edges);
        int CompareTo(Organization Organization);
        void CopyAllLinkedDataFrom(IOrganization OldOrganization);
        IEnumerable<Organization2OrganizationEdgeLabel> EdgeLabels(Organization Organization);
        bool Equals(object Object);
        bool Equals(Organization Organization);

        void GetAllChilds(ref HashSet<IOrganization> Childs);
        void GetAllParents(ref HashSet<IOrganization> Parents);

        IEnumerable<IOrganization> GetAllChilds(Func<IOrganization, bool>? Include = null);
        IEnumerable<IOrganization> GetAllParents(Func<IOrganization, bool>? Include = null);
        int GetHashCode();
        IEnumerable<IOrganization> GetMeAndAllMyChilds(Func<IOrganization, bool>? Include = null);
        IEnumerable<IOrganization> GetMeAndAllMyParents(Func<IOrganization, bool>? Include = null);
        JObject GetNotificationInfos();
        IEnumerable<ANotification> GetNotifications(NotificationMessageType? NotificationMessageType = null);
        IEnumerable<ANotification> GetNotifications(Func<NotificationMessageType, bool> NotificationMessageTypeFilter);
        IEnumerable<T> GetNotificationsOf<T>(Func<NotificationMessageType, bool> NotificationMessageTypeFilter) where T : ANotification;
        IEnumerable<T> GetNotificationsOf<T>(params NotificationMessageType[] NotificationMessageTypes) where T : ANotification;
        bool RemoveInEdge(Organization2OrganizationEdge Edge);
        void RemoveInEdges(Organization2OrganizationEdgeLabel EdgeLabel, IOrganization SourceOrganization);
        bool RemoveOutEdge(Organization2OrganizationEdge Edge);
        void RemoveOutEdges(Organization2OrganizationEdgeLabel EdgeLabel, IOrganization TargetOrganization);
        bool RemoveUser(User2OrganizationEdge Edge);
        void RemoveUser(User2OrganizationEdgeLabel EdgeLabel, IUser User);
        Organization.Builder ToBuilder(Organization_Id? NewOrganizationId = null);
        JObject ToJSON(bool Embedded = false);
        JObject ToJSON(bool Embedded = false, InfoStatus ExpandMembers = InfoStatus.ShowIdOnly, InfoStatus ExpandParents = InfoStatus.ShowIdOnly, InfoStatus ExpandSubOrganizations = InfoStatus.ShowIdOnly, InfoStatus ExpandTags = InfoStatus.ShowIdOnly, bool IncludeLastChange = true, CustomJObjectSerializerDelegate<Organization>? CustomOrganizationSerializer = null);
        string ToString();
        IEnumerable<User2OrganizationEdgeLabel> User2OrganizationInEdgeLabels(IUser User);
        IEnumerable<User2OrganizationEdge> User2OrganizationInEdges(IUser User);


        T AddNotification<T>(T           Notification,
                             Action<T>?  OnUpdate   = null)

            where T : ANotification;

        T AddNotification<T>(T                        Notification,
                             NotificationMessageType  NotificationMessageType,
                             Action<T>?               OnUpdate  = null)

            where T : ANotification;

        T AddNotification<T>(T                                     Notification,
                             IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                             Action<T>?                            OnUpdate  = null)

            where T : ANotification;

        Task RemoveNotification<T>(T           NotificationType,
                                   Action<T>?  OnRemoval   = null)

            where T : ANotification;

    }

}
