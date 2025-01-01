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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate Boolean OrganizationProviderDelegate(Organization_Id OrganizationId, out IOrganization Organization);

    public delegate JObject OrganizationToJSONDelegate(IOrganization  Organization,
                                                       Boolean        Embedded                 = false,
                                                       InfoStatus     ExpandMembers            = InfoStatus.ShowIdOnly,
                                                       InfoStatus     ExpandParents            = InfoStatus.ShowIdOnly,
                                                       InfoStatus     ExpandSubOrganizations   = InfoStatus.ShowIdOnly,
                                                       InfoStatus     ExpandTags               = InfoStatus.ShowIdOnly,
                                                       Boolean        IncludeCryptoHash        = true);


    /// <summary>
    /// Extension methods for organizations.
    /// </summary>
    public static class OrganizationExtensions
    {

        #region ToJSON(this Organizations, Skip = null, Take = null, Embedded = false, ...)

        /// <summary>
        /// Return a JSON representation for the given enumeration of organizations.
        /// </summary>
        /// <param name="Organizations">An enumeration of organizations.</param>
        /// <param name="Skip">The optional number of organizations to skip.</param>
        /// <param name="Take">The optional number of organizations to return.</param>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        public static JArray ToJSON(this IEnumerable<IOrganization>  Organizations,
                                    UInt64?                          Skip                     = null,
                                    UInt64?                          Take                     = null,
                                    Boolean                          Embedded                 = false,
                                    InfoStatus                       ExpandMembers            = InfoStatus.ShowIdOnly,
                                    InfoStatus                       ExpandParents            = InfoStatus.ShowIdOnly,
                                    InfoStatus                       ExpandSubOrganizations   = InfoStatus.ShowIdOnly,
                                    InfoStatus                       ExpandTags               = InfoStatus.ShowIdOnly,
                                    OrganizationToJSONDelegate?      OrganizationToJSON       = null,
                                    Boolean                          IncludeCryptoHash        = true)


            => Organizations?.Any() != true

                   ? new JArray()

                   : new JArray(Organizations.
                                    Where         (dataSet => dataSet is not null).
                                    OrderBy       (dataSet => dataSet.Id).
                                    SkipTakeFilter(Skip, Take).
                                    SafeSelect    (organization => OrganizationToJSON is not null
                                                                       ? OrganizationToJSON (organization,
                                                                                             Embedded,
                                                                                             ExpandMembers,
                                                                                             ExpandParents,
                                                                                             ExpandSubOrganizations,
                                                                                             ExpandTags,
                                                                                             IncludeCryptoHash)

                                                                       : organization.ToJSON(Embedded,
                                                                                             ExpandMembers,
                                                                                             ExpandParents,
                                                                                             ExpandSubOrganizations,
                                                                                             ExpandTags,
                                                                                             IncludeCryptoHash)));

        #endregion

    }

    /// <summary>
    /// An organization.
    /// </summary>
    public class Organization : AEntity<Organization_Id, Organization>,
                                IOrganization
    {

        #region Data

        /// <summary>
        /// The default max size of the aggregated user organizations status history.
        /// </summary>
        public const UInt16 DefaultOrganizationStatusHistorySize = 50;

        /// <summary>
        /// The default JSON-LD context of organizations.
        /// </summary>
        public readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/organization");

        #endregion

        #region Properties

        #region API

        private HTTPExtAPI? api;

        /// <summary>
        /// The HTTPExtAPI of this organization.
        /// </summary>
        public HTTPExtAPI? API
        {

            get
            {
                return api;
            }

            set
            {

                if (api == value)
                    return;

                if (api is not null)
                    throw new ArgumentException("Illegal attempt to change the API of this organization!");

                api = value ?? throw new ArgumentException("Illegal attempt to delete the API reference of this organization!");

            }

        }

        #endregion


        /// <summary>
        /// The website of the organization.
        /// </summary>
        [Optional]
        public String?                    Website              { get; }

        /// <summary>
        /// The primary E-Mail address of the organization.
        /// </summary>
        [Optional]
        public EMailAddress?              EMail                { get; }

        /// <summary>
        /// The telephone number of the organization.
        /// </summary>
        [Optional]
        public PhoneNumber?               Telephone            { get; }

        /// <summary>
        /// The optional address of the organization.
        /// </summary>
        [Optional]
        public Address?                   Address              { get; }

        /// <summary>
        /// The geographical location of this organization.
        /// </summary>
        public GeoCoordinate?             GeoLocation          { get; }

        /// <summary>
        /// An collection of multi-language tags and their relevance.
        /// </summary>
        [Optional]
        public Tags?                      Tags                 { get; }

        /// <summary>
        /// The user will be shown in organization listings.
        /// </summary>
        [Mandatory]
        public Boolean                    IsDisabled           { get; }

        /// <summary>
        /// An enumeration of attached files.
        /// </summary>
        [Optional]
        public IEnumerable<AttachedFile>  AttachedFiles        { get; }

        #endregion

        #region User          -> Organization edges

        protected readonly List<User2OrganizationEdge> _User2Organization_Edges;

        public IEnumerable<User2OrganizationEdge> User2OrganizationEdges
            => _User2Organization_Edges;


        #region AddUser(Edge)

        public User2OrganizationEdge

            AddUser(User2OrganizationEdge Edge)

            => _User2Organization_Edges.AddAndReturnElement(Edge);

        #endregion

        #region AddUser(Source, EdgeLabel, PrivacyLevel = PrivacyLevel.World)

        public User2OrganizationEdge

            AddUser(IUser                       Source,
                    User2OrganizationEdgeLabel  EdgeLabel,
                    PrivacyLevel                PrivacyLevel = PrivacyLevel.World)

            => _User2Organization_Edges.
                   AddAndReturnElement(new User2OrganizationEdge(Source,
                                                                 EdgeLabel,
                                                                 this,
                                                                 PrivacyLevel));

        #endregion

        #region AddUsers(Edges)

        public IEnumerable<User2OrganizationEdge> AddUsers(IEnumerable<User2OrganizationEdge> Edges)

            => _User2Organization_Edges.AddAndReturnList(Edges);

        #endregion


        public IEnumerable<IUser> Admins

            => _User2Organization_Edges.Where(edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdmin).
                                        SafeSelect(edge => edge.Source).
                                        Distinct();

        public IEnumerable<IUser> Members

            => _User2Organization_Edges.Where(edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsMember).
                                        SafeSelect(edge => edge.Source).
                                        Distinct();

        public IEnumerable<IUser> Guests

            => _User2Organization_Edges.Where(edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsGuest).
                                        SafeSelect(edge => edge.Source).
                                        Distinct();

        public IEnumerable<IUser> Users

            => _User2Organization_Edges.Where(edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdmin ||
                                                           edge.EdgeLabel == User2OrganizationEdgeLabel.IsMember ||
                                                           edge.EdgeLabel == User2OrganizationEdgeLabel.IsGuest).
                                        SafeSelect(edge => edge.Source).
                                        Distinct();



        #region User2OrganizationInEdges     (User)

        /// <summary>
        /// The edge labels of all (incoming) edges between the given user and this organization.
        /// </summary>
        public IEnumerable<User2OrganizationEdge> User2OrganizationInEdges(IUser User)

            => _User2Organization_Edges.
                   Where(edge => edge.Source == User);

        #endregion

        #region User2OrganizationInEdgeLabels(User)

        /// <summary>
        /// The edge labels of all (incoming) edges between the given user and this organization.
        /// </summary>
        public IEnumerable<User2OrganizationEdgeLabel> User2OrganizationInEdgeLabels(IUser User)

            => _User2Organization_Edges.
                   Where(edge => edge.Source == User).
                   Select(edge => edge.EdgeLabel);

        #endregion


        #region RemoveUser(EdgeLabel, User)

        public void RemoveUser(User2OrganizationEdgeLabel EdgeLabel,
                               IUser User)
        {

            var edges = _User2Organization_Edges.
                            Where(edge => edge.EdgeLabel == EdgeLabel &&
                                          edge.Source == User).
                            ToArray();

            foreach (var edge in edges)
                _User2Organization_Edges.Remove(edge);

        }

        #endregion

        #region RemoveUser(Edge)

        public Boolean RemoveUser(User2OrganizationEdge Edge)

            => _User2Organization_Edges.Remove(Edge);

        #endregion

        #endregion

        #region Organization <-> Organization edges

        protected readonly List<Organization2OrganizationEdge> _Organization2Organization_InEdges;
        protected readonly List<Organization2OrganizationEdge> _Organization2Organization_OutEdges;

        public IEnumerable<Organization2OrganizationEdge> Organization2OrganizationInEdges
            => _Organization2Organization_InEdges;

        public IEnumerable<Organization2OrganizationEdge> Organization2OrganizationOutEdges
            => _Organization2Organization_OutEdges;


        #region AddEdge (Edge)

        public Organization2OrganizationEdge AddEdge(Organization2OrganizationEdge Edge)

            => Edge.Target == this
                   ? _Organization2Organization_InEdges.AddAndReturnElement(Edge)
                   : _Organization2Organization_OutEdges.AddAndReturnElement(Edge);

        #endregion

        #region AddEdges(Edges)

        public IEnumerable<Organization2OrganizationEdge> AddEdges(IEnumerable<Organization2OrganizationEdge> Edges)
        {

            foreach (var edge in Edges)
                AddEdge(edge);

            return Edges;

        }

        #endregion

        #region AddInEdge (EdgeLabel, SourceOrganization, PrivacyLevel = PrivacyLevel.World)

        public Organization2OrganizationEdge AddInEdge(Organization2OrganizationEdgeLabel  EdgeLabel,
                                                       IOrganization                       SourceOrganization,
                                                       PrivacyLevel                        PrivacyLevel = PrivacyLevel.World)

            => _Organization2Organization_InEdges.AddAndReturnElement(new Organization2OrganizationEdge(SourceOrganization,
                                                                                                        EdgeLabel,
                                                                                                        this,
                                                                                                        PrivacyLevel));

        #endregion

        #region AddOutEdge(EdgeLabel, TargetOrganization, PrivacyLevel = PrivacyLevel.World)

        public Organization2OrganizationEdge AddOutEdge(Organization2OrganizationEdgeLabel  EdgeLabel,
                                                        IOrganization                       TargetOrganization,
                                                        PrivacyLevel                        PrivacyLevel = PrivacyLevel.World)

            => _Organization2Organization_OutEdges.AddAndReturnElement(new Organization2OrganizationEdge(this,
                                                                                                         EdgeLabel,
                                                                                                         TargetOrganization,
                                                                                                         PrivacyLevel));

        #endregion


        #region GetAllParents(ref Parents)

        public void GetAllParents(ref HashSet<IOrganization> Parents)
        {

            var parents = _Organization2Organization_OutEdges.
                              Where (edge => edge.Source == this && edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf).
                              Select(edge => edge.Target).
                              ToArray();

            foreach (var parent in parents)
            {
                // Detect loops!
                if (Parents.Add(parent))
                    parent.GetAllParents(ref Parents);
            }

        }

        #endregion

        #region GetAllParents(Filter = null)

        public IEnumerable<IOrganization> GetAllParents(Func<IOrganization, Boolean>? Include = null)
        {

            var parents = new HashSet<IOrganization>();

            GetAllParents(ref parents);

            return Include is not null
                       ? parents.Where(Include)
                       : parents;

        }

        #endregion

        #region GetMeAndAllMyParents(Filter = null)

        public IEnumerable<IOrganization> GetMeAndAllMyParents(Func<IOrganization, Boolean>? Include = null)
        {

            var parentsAndMe = new HashSet<IOrganization> {
                                   this
                               };

            GetAllParents(ref parentsAndMe);

            return Include is not null
                       ? parentsAndMe.Where(Include)
                       : parentsAndMe;

        }

        #endregion

        #region ParentOrganizations

        public IEnumerable<IOrganization> ParentOrganizations

            => _Organization2Organization_OutEdges.
                   Where(edge => edge.Source == this && edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf).
                   Select(edge => edge.Target).
                   ToArray();

        #endregion


        #region GetAllChilds(ref Childs)

        public void GetAllChilds(ref HashSet<IOrganization> Childs)
        {

            var childs = _Organization2Organization_InEdges.
                             Where (edge => edge.Target == this && edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf).
                             Select(edge => edge.Source).
                             ToArray();

            foreach (var child in childs)
            {
                // Detect loops!
                if (Childs.Add(child))
                    child.GetAllChilds(ref Childs);
            }

        }

        #endregion

        #region GetAllChilds(Filter = null)

        public IEnumerable<IOrganization> GetAllChilds(Func<IOrganization, Boolean>? Include = null)
        {

            var childs = new HashSet<IOrganization>();

            GetAllChilds(ref childs);

            return Include is not null
                       ? childs.Where(Include)
                       : childs;

        }

        #endregion

        #region GetMeAndAllMyChilds(Filter = null)

        public IEnumerable<IOrganization> GetMeAndAllMyChilds(Func<IOrganization, Boolean>? Include = null)
        {

            var childAndMe = new HashSet<IOrganization> {
                                 this
                             };

            GetAllChilds(ref childAndMe);

            return Include != null
                       ? childAndMe.Where(Include)
                       : childAndMe;

        }

        #endregion

        #region SubOrganizations

        /// <summary>
        /// A relationship between two organizations where the first includes the second, e.g., as a subsidiary. See also: the more specific 'department' property.
        /// </summary>
        public IEnumerable<IOrganization> SubOrganizations

            => _Organization2Organization_InEdges.
                   Where(edge => edge.Target == this && edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf).
                   Select(edge => edge.Source).
                   ToArray();

        #endregion


        #region EdgeLabels(Organization Organization)

        /// <summary>
        /// All edge labels between this and the given organization.
        /// </summary>
        public IEnumerable<Organization2OrganizationEdgeLabel> EdgeLabels(Organization Organization)

            => Organization is null

                   ? new Organization2OrganizationEdgeLabel[0]

                   : _Organization2Organization_InEdges.
                         Where(edge => edge.Source == Organization).
                         Select(edge => edge.EdgeLabel).Concat(

                     _Organization2Organization_OutEdges.
                         Where(edge => edge.Target == Organization).
                         Select(edge => edge.EdgeLabel));

        #endregion


        #region RemoveInEdge  (EdgeLabel)

        public Boolean RemoveInEdge(Organization2OrganizationEdge Edge)

            => _Organization2Organization_InEdges.Remove(Edge);

        #endregion

        #region RemoveInEdges (EdgeLabel, SourceOrganization)

        public void RemoveInEdges(Organization2OrganizationEdgeLabel EdgeLabel,
                                  IOrganization SourceOrganization)
        {

            var edges = _Organization2Organization_InEdges.
                            Where(edge => edge.EdgeLabel == EdgeLabel &&
                                          edge.Source == SourceOrganization).
                            ToArray();

            foreach (var edge in edges)
                _Organization2Organization_InEdges.Remove(edge);

        }

        #endregion

        #region RemoveOutEdges(EdgeLabel, TargetOrganization)

        public Boolean RemoveOutEdge(Organization2OrganizationEdge Edge)

            => _Organization2Organization_OutEdges.Remove(Edge);

        #endregion

        #region RemoveOutEdges(EdgeLabel, TargetOrganization)

        public void RemoveOutEdges(Organization2OrganizationEdgeLabel EdgeLabel,
                                   IOrganization TargetOrganization)
        {

            var edges = _Organization2Organization_OutEdges.
                            Where(edge => edge.EdgeLabel == EdgeLabel &&
                                          edge.Target == TargetOrganization).
                            ToArray();

            foreach (var edge in edges)
                _Organization2Organization_OutEdges.Remove(edge);

        }

        #endregion

        #endregion

        #region Events

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new organization.
        /// </summary>
        /// <param name="Id">The unique identification of the organization.</param>
        /// 
        /// <param name="Name">The offical (multi-language) name of the organization.</param>
        /// <param name="Description">An optional (multi-language) description of the organization.</param>
        /// <param name="Website">The website of the organization.</param>
        /// <param name="EMail">The primary e-mail of the organisation.</param>
        /// <param name="Telephone">An optional telephone number of the organisation.</param>
        /// <param name="Address">An optional address of the organisation.</param>
        /// <param name="GeoLocation">An optional geographical location of the organisation.</param>
        /// <param name="IsDisabled">The organization is disabled.</param>
        /// 
        /// <param name="CustomData">Custom data to be stored with this organization.</param>
        /// <param name="AttachedFiles">Optional files attached to this organization.</param>
        /// <param name="JSONLDContext">The JSON-LD context of this organization.</param>
        /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
        /// <param name="LastChange">The timestamp of the last changes within this organization. Can e.g. be used as a HTTP ETag.</param>
        public Organization(Organization_Id                              Id,
                            I18NString?                                  Name                                = null,
                            I18NString?                                  Description                         = null,
                            String?                                      Website                             = null,
                            EMailAddress?                                EMail                               = null,
                            PhoneNumber?                                 Telephone                           = null,
                            Address?                                     Address                             = null,
                            GeoCoordinate?                               GeoLocation                         = null,
                            Func<Tags.Builder, Tags>?                    Tags                                = null,
                            Boolean                                      IsDisabled                          = false,

                            IEnumerable<ANotification>?                  Notifications                       = null,

                            IEnumerable<User2OrganizationEdge>?          User2OrganizationEdges              = null,
                            IEnumerable<Organization2OrganizationEdge>?  Organization2OrganizationInEdges    = null,
                            IEnumerable<Organization2OrganizationEdge>?  Organization2OrganizationOutEdges   = null,

                            JObject?                                     CustomData                          = default,
                            IEnumerable<AttachedFile>?                   AttachedFiles                       = default,
                            JSONLDContext?                               JSONLDContext                       = default,
                            String?                                      DataSource                          = default,
                            DateTime?                                    LastChange                          = default)

            : base(Id,
                   JSONLDContext ?? DefaultJSONLDContext,
                   Name,
                   Description,
                   null,
                   CustomData,
                   null,
                   LastChange,
                   DataSource)

        {

            this.Website                              = Website;
            this.EMail                                = EMail;
            this.Telephone                            = Telephone;
            this.Address                              = Address;
            this.GeoLocation                          = GeoLocation;
            var _TagsBuilder                          = new Tags.Builder();
            this.Tags                                 = Tags is not null
                                                            ? Tags(_TagsBuilder)
                                                            : _TagsBuilder;
            this.IsDisabled                           = IsDisabled;
            this.AttachedFiles                        = AttachedFiles ?? Array.Empty<AttachedFile>();

            this.notifications                        = new NotificationStore(Notifications);

            this._User2Organization_Edges             = User2OrganizationEdges            is not null && User2OrganizationEdges.           IsNeitherNullNorEmpty()
                                                            ? new List<User2OrganizationEdge>        (User2OrganizationEdges)
                                                            : new List<User2OrganizationEdge>();

            this._Organization2Organization_InEdges   = Organization2OrganizationInEdges  is not null && Organization2OrganizationInEdges. IsNeitherNullNorEmpty()
                                                            ? new List<Organization2OrganizationEdge>(Organization2OrganizationInEdges)
                                                            : new List<Organization2OrganizationEdge>();

            this._Organization2Organization_OutEdges  = Organization2OrganizationOutEdges is not null && Organization2OrganizationOutEdges.IsNeitherNullNorEmpty()
                                                            ? new List<Organization2OrganizationEdge>(Organization2OrganizationOutEdges)
                                                            : new List<Organization2OrganizationEdge>();

        }

        #endregion


        #region Notifications

        private readonly NotificationStore notifications;

        #region AddNotification(Notification,                           OnUpdate = null)

        public T AddNotification<T>(T           Notification,
                                    Action<T>?  OnUpdate   = null)

            where T : ANotification

            => notifications.Add(Notification,
                                 OnUpdate);

        #endregion

        #region AddNotification(Notification, NotificationMessageType,  OnUpdate = null)

        public T AddNotification<T>(T                        Notification,
                                    NotificationMessageType  NotificationMessageType,
                                    Action<T>?               OnUpdate  = null)

            where T : ANotification

            => notifications.Add(Notification,
                                 NotificationMessageType,
                                 OnUpdate);

        #endregion

        #region AddNotification(Notification, NotificationMessageTypes, OnUpdate = null)

        public T AddNotification<T>(T                                     Notification,
                                    IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                    Action<T>?                            OnUpdate  = null)

            where T : ANotification

            => notifications.Add(Notification,
                                 NotificationMessageTypes,
                                 OnUpdate);

        #endregion

        #region RemoveNotification(NotificationType,                           OnRemoval = null)

        public Task RemoveNotification<T>(T           NotificationType,
                                          Action<T>?  OnRemoval   = null)

            where T : ANotification

            => notifications.Remove(NotificationType,
                                    OnRemoval);

        #endregion


        #region GetNotifications  (NotificationMessageType = null)

        public IEnumerable<ANotification> GetNotifications(NotificationMessageType?  NotificationMessageType = null)
        {
            lock (notifications)
            {
                return notifications.GetNotifications(NotificationMessageType);
            }
        }

        #endregion

        #region GetNotificationsOf(params NotificationMessageTypes)

        public IEnumerable<T> GetNotificationsOf<T>(params NotificationMessageType[] NotificationMessageTypes)

            where T : ANotification

        {

            lock (notifications)
            {

                var organizationNotifications  = notifications.GetNotificationsOf<T>(NotificationMessageTypes);

                var userNotifications          = GetMeAndAllMyParents(parent => parent.Id.ToString() != "NoOwner").
                                                 SelectMany          (parent => parent.User2OrganizationEdges).
                                                 SelectMany          (edge   => edge.Source.GetNotificationsOf<T>(NotificationMessageTypes));

                return organizationNotifications.Concat(userNotifications).ToArray();

            }

        }

        #endregion

        #region GetNotifications  (NotificationMessageTypeFilter)

        public IEnumerable<ANotification> GetNotifications(Func<NotificationMessageType, Boolean> NotificationMessageTypeFilter)
        {
            lock (notifications)
            {
                return notifications.GetNotifications(NotificationMessageTypeFilter);
            }
        }

        #endregion

        #region GetNotificationsOf(NotificationMessageTypeFilter)

        public IEnumerable<T> GetNotificationsOf<T>(Func<NotificationMessageType, Boolean> NotificationMessageTypeFilter)

            where T : ANotification

        {

            lock (notifications)
            {
                return notifications.GetNotificationsOf<T>(NotificationMessageTypeFilter);
            }

        }

        #endregion


        #region GetNotificationInfos()

        public JObject GetNotificationInfos()

            => JSONObject.Create(new JProperty("user", JSONObject.Create(

                                     new JProperty("name",               EMail.OwnerName),
                                     new JProperty("email",              EMail.Address.ToString())

                                     //MobilePhone.HasValue
                                     //    ? new JProperty("phoneNumber",  MobilePhone.Value.ToString())
                                     //    : null

                                 )),
                                 new JProperty("notifications",  notifications.ToJSON()));

        #endregion

        #endregion


        #region ToJSON(Embedded = false)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        public override JObject ToJSON(Boolean  Embedded   = false)

            => ToJSON(Embedded:                false,
                      ExpandParents:           InfoStatus.ShowIdOnly,
                      ExpandSubOrganizations:  InfoStatus.ShowIdOnly,
                      ExpandTags:              InfoStatus.ShowIdOnly);


        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        public JObject ToJSON(Boolean                                         Embedded                       = false,
                              InfoStatus                                      ExpandMembers                  = InfoStatus.ShowIdOnly,
                              InfoStatus                                      ExpandParents                  = InfoStatus.ShowIdOnly,
                              InfoStatus                                      ExpandSubOrganizations         = InfoStatus.ShowIdOnly,
                              InfoStatus                                      ExpandTags                     = InfoStatus.ShowIdOnly,
                              Boolean                                         IncludeLastChange              = true,
                              CustomJObjectSerializerDelegate<Organization>?  CustomOrganizationSerializer   = null)

        {

            var json = base.ToJSON(Embedded,
                                   IncludeLastChange,
                                   null,
                                   new JProperty?[] {

                                       new JProperty("name",                    Name.           ToJSON()),

                                       Description.IsNotNullOrEmpty()
                                           ? new JProperty("description",       Description.    ToJSON())
                                           : null,

                                       Website is not null && Website.IsNeitherNullNorEmpty()
                                           ? new JProperty("website",           Website)
                                           : null,

                                       EMail is not null
                                           ? new JProperty("email",             EMail.Address.  ToString())
                                           : null,

                                       Telephone.HasValue
                                           ? new JProperty("telephone",         Telephone.Value.ToString())
                                           : null,

                                       Address is not null
                                           ? new JProperty("address",           Address.ToJSON())
                                           : null,

                                       GeoLocation.HasValue
                                           ? new JProperty("geoLocation",       GeoLocation.Value.ToJSON())
                                           : null,

                                       Tags is not null && Tags.Any()
                                           ? new JProperty("tags",              Tags.ToJSON(ExpandTags))
                                           : null,


                                       new JProperty("parents",                 Organization2OrganizationOutEdges.
                                                                                    Where     (edge => edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf).
                                                                                    SafeSelect(edge => ExpandParents.Switch(edge,
                                                                                                                            _edge => _edge.Target.Id.ToString(),
                                                                                                                            _edge => _edge.Target.ToJSON(true)))),

                                       Organization2OrganizationInEdges.SafeAny(edge => edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf)
                                           ? new JProperty("subOrganizations",  Organization2OrganizationInEdges.
                                                                                    Where     (edge => edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf).
                                                                                    SafeSelect(edge => ExpandSubOrganizations.Switch(edge,
                                                                                                                           _edge => _edge.Source.Id.ToString(),
                                                                                                                           _edge => _edge.Source.ToJSON(true))))
                                           : null,

                                       Admins.SafeAny()
                                           ? new JProperty("admins",            Admins.
                                                                                    SafeSelect(user => ExpandMembers.Switch(user,
                                                                                                                           _user => _user.Id.ToString(),
                                                                                                                           _user => _user.ToJSON())))
                                           : null,

                                       Members.SafeAny()
                                           ? new JProperty("members",           Members.
                                                                                    SafeSelect(user => ExpandMembers.Switch(user,
                                                                                                                           _user => _user.Id.ToString(),
                                                                                                                           _user => _user.ToJSON())))
                                           : null,

                                       Guests.SafeAny()
                                           ? new JProperty("guests",            Guests.
                                                                                    SafeSelect(user => ExpandMembers.Switch(user,
                                                                                                                           _user => _user.Id.ToString(),
                                                                                                                           _user => _user.ToJSON())))
                                           : null,


                                       new JProperty("isDisabled",              IsDisabled)

                                    });


            return CustomOrganizationSerializer is not null
                       ? CustomOrganizationSerializer(this, json)
                       : json;

        }

        #endregion

        #region (static) TryParseJSON(JSONObject, ..., out Organization, out ErrorResponse)

        public static Boolean TryParseJSON(JObject            JSONObject,
                                           out Organization?  Organization,
                                           out String?        ErrorResponse,
                                           Organization_Id?   OrganizationIdURL   = null)
        {

            try
            {

                Organization = null;

                #region Parse OrganizationId   [optional]

                // Verify that a given organization identification
                //   is at least valid.
                if (JSONObject.ParseOptional("@id",
                                             "organization identification",
                                             Organization_Id.TryParse,
                                             out Organization_Id? OrganizationIdBody,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                if (!OrganizationIdURL.HasValue && !OrganizationIdBody.HasValue)
                {
                    ErrorResponse = "The organization identification is missing!";
                    return false;
                }

                if (OrganizationIdURL.HasValue && OrganizationIdBody.HasValue && OrganizationIdURL.Value != OrganizationIdBody.Value)
                {
                    ErrorResponse = "The optional organization identification given within the JSON body does not match the one given in the URI!";
                    return false;
                }

                #endregion

                #region Parse Context          [mandatory]

                if (!JSONObject.ParseMandatory("@context",
                                               "JSON-LinkedData context information",
                                               JSONLDContext.TryParse,
                                               out JSONLDContext Context,
                                               out ErrorResponse))
                {
                    ErrorResponse = @"The JSON-LD ""@context"" information is missing!";
                    return false;
                }

                if (Context != DefaultJSONLDContext && Context != OrganizationInfo.DefaultJSONLDContext && Context != OrganizationInfo2.DefaultJSONLDContext)
                {
                    ErrorResponse = @"The given JSON-LD ""@context"" information '" + Context + "' is not supported!";
                    return false;
                }

                #endregion

                #region Parse Name             [mandatory]

                if (!JSONObject.ParseMandatory("name",
                                               "name",
                                               out I18NString Name,
                                               out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Description      [optional]

                if (JSONObject.ParseOptional("description",
                                             "description",
                                             out I18NString Description,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse Website          [optional]

                if (JSONObject.ParseOptional("website",
                                             "website",
                                             out String Website,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse E-Mail           [optional]

                if (JSONObject.ParseOptional("email",
                                             "e-mail address",
                                             SimpleEMailAddress.TryParse,
                                             out SimpleEMailAddress? EMail,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse Telephone        [optional]

                if (JSONObject.ParseOptional("telephone",
                                             "phone number",
                                             PhoneNumber.TryParse,
                                             out PhoneNumber? Telephone,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse GeoLocation      [optional]

                if (JSONObject.ParseOptionalJSON("geoLocation",
                                                 "geo location",
                                                 GeoCoordinate.TryParse,
                                                 out GeoCoordinate? GeoLocation,
                                                 out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse Address          [optional]

                if (JSONObject.ParseOptionalJSON("address",
                                                 "address",
                                                 org.GraphDefined.Vanaheimr.Illias.Address.TryParse,
                                                 out Address Address,
                                                 out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                var Tags = new Tags();

                #region Parse PrivacyLevel     [optional]

                if (JSONObject.ParseOptionalEnum("privacyLevel",
                                             "privacy level",
                                             out PrivacyLevel? PrivacyLevel,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                var IsDisabled       = JSONObject["isDisabled"]?.     Value<Boolean>();

                #region Get   DataSource       [optional]

                var DataSource = JSONObject.GetOptional("dataSource");

                #endregion

                #region Parse CryptoHash       [optional]

                var CryptoHash    = JSONObject.GetOptional("cryptoHash");

                #endregion


                Organization = new Organization(

                                   OrganizationIdBody ?? OrganizationIdURL.Value,

                                   Name,
                                   Description,
                                   Website,
                                   EMail,
                                   Telephone,
                                   Address,
                                   GeoLocation,
                                   _ => Tags,
                                   IsDisabled ?? false

                               );

                                          //      CustomData,
                                          //      AttachedFiles,
                                          //      JSONLDContext,
                                          //      DataSource,
                                          //      LastChange);

                ErrorResponse = null;
                return true;

            }
            catch (Exception e)
            {
                ErrorResponse  = e.Message;
                Organization   = null;
                return false;
            }

        }

        #endregion


        #region CopyAllLinkedDataFrom(OldOrganization)

        public void CopyAllLinkedDataFrom(IOrganization OldOrganization)
            => CopyAllLinkedDataFromBase(OldOrganization as Organization);

        public override void CopyAllLinkedDataFromBase(Organization OldOrganization)
        {

            if (OldOrganization._User2Organization_Edges.Any() && !_User2Organization_Edges.Any())
            {

                AddUsers(OldOrganization._User2Organization_Edges);

                foreach (var edge in _User2Organization_Edges)
                    edge.Target = this;

            }

            if (OldOrganization._Organization2Organization_InEdges.Any() && !_Organization2Organization_InEdges.Any())
            {

                AddEdges(OldOrganization._Organization2Organization_InEdges);

                foreach (var edge in _Organization2Organization_InEdges)
                    edge.Target = this;

            }

            if (OldOrganization._Organization2Organization_OutEdges.Any() && !_Organization2Organization_OutEdges.Any())
            {

                AddEdges(OldOrganization._Organization2Organization_OutEdges);

                foreach (var edge in _Organization2Organization_OutEdges)
                    edge.Source = this;

            }

            if (OldOrganization.notifications.SafeAny() && !notifications.SafeAny())
                notifications.Add(OldOrganization.notifications);

        }

        #endregion


        #region Operator overloading

        #region Operator == (Organization1, Organization2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Organization1">A organization.</param>
        /// <param name="Organization2">Another organization.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (Organization? Organization1,
                                           Organization? Organization2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(Organization1, Organization2))
                return true;

            // If one is null, but not both, return false.
            if (Organization1 is null || Organization2 is null)
                return false;

            return Organization1.Equals(Organization2);

        }

        #endregion

        #region Operator != (Organization1, Organization2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Organization1">A organization.</param>
        /// <param name="Organization2">Another organization.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (Organization? Organization1,
                                           Organization? Organization2)

            => !(Organization1 == Organization2);

        #endregion

        #region Operator <  (Organization1, Organization2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Organization1">A organization.</param>
        /// <param name="Organization2">Another organization.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (Organization? Organization1,
                                          Organization? Organization2)
        {

            if (Organization1 is null)
                throw new ArgumentNullException(nameof(Organization1), "The given Organization1 must not be null!");

            return Organization1.CompareTo(Organization2) < 0;

        }

        #endregion

        #region Operator <= (Organization1, Organization2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Organization1">A organization.</param>
        /// <param name="Organization2">Another organization.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (Organization? Organization1,
                                           Organization? Organization2)

            => !(Organization1 > Organization2);

        #endregion

        #region Operator >  (Organization1, Organization2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Organization1">A organization.</param>
        /// <param name="Organization2">Another organization.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (Organization? Organization1,
                                          Organization? Organization2)
        {

            if (Organization1 is null)
                throw new ArgumentNullException(nameof(Organization1), "The given Organization1 must not be null!");

            return Organization1.CompareTo(Organization2) > 0;

        }

        #endregion

        #region Operator >= (Organization1, Organization2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Organization1">A organization.</param>
        /// <param name="Organization2">Another organization.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (Organization? Organization1,
                                           Organization? Organization2)

            => !(Organization1 < Organization2);

        #endregion

        #endregion

        #region IComparable<Organization> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two organizations.
        /// </summary>
        /// <param name="Object">An organization to compare with.</param>
        public override Int32 CompareTo(Object? Object)

            => Object is Organization organization
                   ? CompareTo(organization)
                   : throw new ArgumentException("The given object is not an organization!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(Organization)

        /// <summary>
        /// Compares two organizations.
        /// </summary>
        /// <param name="Organization">An organization to compare with.</param>
        public override Int32 CompareTo(Organization? Organization)

            => CompareTo(Organization as IOrganization);


        /// <summary>
        /// Compares two organizations.
        /// </summary>
        /// <param name="Organization">An organization to compare with.</param>
        public Int32 CompareTo(IOrganization? Organization)

            => Organization is null
                   ? throw new ArgumentNullException(nameof(Organization), "The given organization must not be null!")
                   : Id.CompareTo(Organization.Id);

        #endregion

        #endregion

        #region IEquatable<Organization> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two organizations for equality.
        /// </summary>
        /// <param name="Object">An organization to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is Organization organization &&
                  Equals(organization);

        #endregion

        #region Equals(Organization)

        /// <summary>
        /// Compares two organizations for equality.
        /// </summary>
        /// <param name="Organization">An organization to compare with.</param>
        public override Boolean Equals(Organization? Organization)

            => Equals(Organization as IOrganization);


        /// <summary>
        /// Compares two organizations for equality.
        /// </summary>
        /// <param name="Organization">An organization to compare with.</param>
        public Boolean Equals(IOrganization? Organization)

            => Organization is Organization &&
                   Id.Equals(Organization.Id);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Get the hashcode of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => Id.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => Id.ToString();

        #endregion


        #region ToBuilder(NewOrganizationId = null)

        /// <summary>
        /// Return a builder for this organization.
        /// </summary>
        /// <param name="NewOrganizationId">An optional new organization identification.</param>
        public Builder ToBuilder(Organization_Id? NewOrganizationId = null)

            => new Builder(NewOrganizationId ?? Id,
                           Name,
                           Description,
                           Website,
                           EMail,
                           Telephone,
                           Address,
                           GeoLocation,
                           _ => Tags,
                           IsDisabled,

                           notifications,

                           _User2Organization_Edges,
                           _Organization2Organization_InEdges,
                           _Organization2Organization_OutEdges,

                           CustomData,
                           AttachedFiles,
                           JSONLDContext,
                           DataSource,
                           LastChangeDate);

        #endregion

        #region (class) Builder

        /// <summary>
        /// An organization builder.
        /// </summary>
        public new class Builder : AEntity<Organization_Id, Organization>.Builder
        {

            #region Properties

            /// <summary>
            /// The website of the organization.
            /// </summary>
            [Optional]
            public String?                Website              { get; set; }

            /// <summary>
            /// The primary E-Mail address of the organization.
            /// </summary>
            [Optional]
            public EMailAddress?          EMail                { get; set; }

            /// <summary>
            /// The telephone number of the organization.
            /// </summary>
            [Optional]
            public PhoneNumber?           Telephone            { get; set; }

            /// <summary>
            /// The optional address of the organization.
            /// </summary>
            [Optional]
            public Address?               Address              { get; set; }

            /// <summary>
            /// The geographical location of this organization.
            /// </summary>
            public GeoCoordinate?         GeoLocation          { get; set; }

            /// <summary>
            /// An collection of multi-language tags and their relevance.
            /// </summary>
            [Optional]
            public Tags?                  Tags                 { get; set; }

            /// <summary>
            /// The user will be shown in organization listings.
            /// </summary>
            [Mandatory]
            public Boolean                IsDisabled           { get; set; }

            /// <summary>
            /// An enumeration of attached files.
            /// </summary>
            [Optional]
            public HashSet<AttachedFile>  AttachedFiles        { get; }

            #endregion

            #region Edges

            #region User          -> Organization edges

            protected readonly List<User2OrganizationEdge> _User2Organization_Edges;

            public IEnumerable<User2OrganizationEdge> User2OrganizationEdges
                => _User2Organization_Edges;


            #region LinkUser(Edge)

            public User2OrganizationEdge

                LinkUser(User2OrganizationEdge Edge)

                => _User2Organization_Edges.AddAndReturnElement(Edge);

            #endregion

            #region LinkUser(Source, EdgeLabel, PrivacyLevel = PrivacyLevel.World)

            public User2OrganizationEdge

                LinkUser(User Source,
                         User2OrganizationEdgeLabel EdgeLabel,
                         PrivacyLevel PrivacyLevel = PrivacyLevel.World)

                => _User2Organization_Edges.
                       AddAndReturnElement(new User2OrganizationEdge(Source,
                                                                     EdgeLabel,
                                                                     this.ToImmutable,
                                                                     PrivacyLevel));

            #endregion


            #region User2OrganizationInEdges     (User)

            /// <summary>
            /// The edge labels of all (incoming) edges between the given user and this organization.
            /// </summary>
            public IEnumerable<User2OrganizationEdge> User2OrganizationInEdges(User User)

                => _User2Organization_Edges.
                       Where(edge => edge.Source == User);

            #endregion

            #region User2OrganizationInEdgeLabels(User)

            /// <summary>
            /// The edge labels of all (incoming) edges between the given user and this organization.
            /// </summary>
            public IEnumerable<User2OrganizationEdgeLabel> User2OrganizationInEdgeLabels(User User)

                => _User2Organization_Edges.
                       Where(edge => edge.Source == User).
                       Select(edge => edge.EdgeLabel);

            #endregion

            public IEnumerable<User2OrganizationEdge>

                Add(IEnumerable<User2OrganizationEdge> Edges)

                    => _User2Organization_Edges.AddAndReturnList(Edges);


            #region UnlinkUser(EdgeLabel, User)

            public void UnlinkUser(User2OrganizationEdgeLabel EdgeLabel,
                                   User User)
            {

                var edges = _User2Organization_Edges.
                                Where(edge => edge.EdgeLabel == EdgeLabel &&
                                              edge.Source == User).
                                ToArray();

                foreach (var edge in edges)
                    _User2Organization_Edges.Remove(edge);

            }

            #endregion

            public Boolean RemoveInEdge(User2OrganizationEdge Edge)
                => _User2Organization_Edges.Remove(Edge);

            #endregion

            #region Organization <-> Organization edges

            protected readonly List<Organization2OrganizationEdge> _Organization2Organization_InEdges;

            public IEnumerable<Organization2OrganizationEdge> Organization2OrganizationInEdges
                => _Organization2Organization_InEdges;

            #region AddInEdge (Edge)

            public Organization2OrganizationEdge

                AddInEdge(Organization2OrganizationEdge Edge)

                => _Organization2Organization_InEdges.AddAndReturnElement(Edge);

            #endregion

            #region AddInEdge (EdgeLabel, SourceOrganization, PrivacyLevel = PrivacyLevel.World)

            public Organization2OrganizationEdge

                AddInEdge(Organization2OrganizationEdgeLabel  EdgeLabel,
                          Organization                        SourceOrganization,
                          PrivacyLevel                        PrivacyLevel = PrivacyLevel.World)

                => _Organization2Organization_InEdges.AddAndReturnElement(new Organization2OrganizationEdge(SourceOrganization,
                                                                                                            EdgeLabel,
                                                                                                            this.ToImmutable,
                                                                                                            PrivacyLevel));

            #endregion

            public IEnumerable<Organization2OrganizationEdge>

                AddInEdges(IEnumerable<Organization2OrganizationEdge> Edges)

                    => _Organization2Organization_InEdges.AddAndReturnList(Edges);

            #region RemoveInEdges(EdgeLabel, TargetOrganization)

            public Boolean RemoveInEdge(Organization2OrganizationEdge Edge)
                => _Organization2Organization_InEdges.Remove(Edge);

            #endregion

            #region RemoveInEdges (EdgeLabel, SourceOrganization)

            public void RemoveInEdges(Organization2OrganizationEdgeLabel EdgeLabel,
                                      Organization SourceOrganization)
            {

                var edges = _Organization2Organization_OutEdges.
                                Where(edge => edge.EdgeLabel == EdgeLabel &&
                                              edge.Source == SourceOrganization).
                                ToArray();

                foreach (var edge in edges)
                    _Organization2Organization_InEdges.Remove(edge);

            }

            #endregion



            protected readonly List<Organization2OrganizationEdge> _Organization2Organization_OutEdges;

            public IEnumerable<Organization2OrganizationEdge> Organization2OrganizationOutEdges
                => _Organization2Organization_OutEdges;

            #region AddOutEdge(Edge)

            public Organization2OrganizationEdge

                AddOutEdge(Organization2OrganizationEdge Edge)

                => _Organization2Organization_OutEdges.AddAndReturnElement(Edge);

            #endregion

            #region AddOutEdge(EdgeLabel, TargetOrganization, PrivacyLevel = PrivacyLevel.World)

            public Organization2OrganizationEdge

                AddOutEdge(Organization2OrganizationEdgeLabel EdgeLabel,
                           Organization TargetOrganization,
                           PrivacyLevel PrivacyLevel = PrivacyLevel.World)

                => _Organization2Organization_OutEdges.AddAndReturnElement(new Organization2OrganizationEdge(this.ToImmutable,
                                                                                                             EdgeLabel,
                                                                                                             TargetOrganization,
                                                                                                             PrivacyLevel));

            #endregion

            public IEnumerable<Organization2OrganizationEdge>

                AddOutEdges(IEnumerable<Organization2OrganizationEdge> Edges)

                    => _Organization2Organization_OutEdges.AddAndReturnList(Edges);

            #region RemoveOutEdges(EdgeLabel, TargetOrganization)

            public Boolean RemoveOutEdge(Organization2OrganizationEdge Edge)
                => _Organization2Organization_OutEdges.Remove(Edge);

            #endregion

            #region RemoveOutEdges(EdgeLabel, TargetOrganization)

            public void RemoveOutEdges(Organization2OrganizationEdgeLabel EdgeLabel,
                                       Organization TargetOrganization)
            {

                var edges = _Organization2Organization_OutEdges.
                                Where(edge => edge.EdgeLabel == EdgeLabel &&
                                              edge.Target == TargetOrganization).
                                ToArray();

                foreach (var edge in edges)
                    _Organization2Organization_OutEdges.Remove(edge);

            }

            #endregion

            #endregion

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new organization builder.
            /// </summary>
            /// <param name="Id">The unique identification of the organization.</param>
            /// <param name="Name">The offical (multi-language) name of the organization.</param>
            /// <param name="Description">An optional (multi-language) description of the organization.</param>
            /// <param name="Website">The website of the organization.</param>
            /// <param name="EMail">The primary e-mail of the organisation.</param>
            /// <param name="Telephone">An optional telephone number of the organisation.</param>
            /// <param name="GeoLocation">An optional geographical location of the organisation.</param>
            /// <param name="Address">An optional address of the organisation.</param>
            /// <param name="IsDisabled">The organization is disabled.</param>
            /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
            public Builder(Organization_Id                              Id,
                           I18NString?                                  Name                                = null,
                           I18NString?                                  Description                         = null,
                           String?                                      Website                             = null,
                           EMailAddress?                                EMail                               = null,
                           PhoneNumber?                                 Telephone                           = null,
                           Address?                                     Address                             = null,
                           GeoCoordinate?                               GeoLocation                         = null,
                           Func<Tags.Builder, Tags>?                    Tags                                = null,
                           Boolean                                      IsDisabled                          = false,

                           IEnumerable<ANotification>?                  Notifications                       = null,

                           IEnumerable<User2OrganizationEdge>?          User2OrganizationEdges              = null,
                           IEnumerable<Organization2OrganizationEdge>?  Organization2OrganizationInEdges    = null,
                           IEnumerable<Organization2OrganizationEdge>?  Organization2OrganizationOutEdges   = null,

                           JObject?                                     CustomData                          = default,
                           IEnumerable<AttachedFile>?                   AttachedFiles                       = default,
                           JSONLDContext?                               JSONLDContext                       = default,
                           String?                                      DataSource                          = default,
                           DateTime?                                    LastChange                          = default)

                : base(Id,
                       JSONLDContext ?? DefaultJSONLDContext,
                       LastChange,
                       null,
                       CustomData,
                       null,
                       DataSource)

            {

                this.Name = Name ?? new I18NString();
                this.Description = Description ?? new I18NString();
                this.Website = Website;
                this.EMail = EMail;
                this.Telephone = Telephone;
                this.Address = Address;
                this.GeoLocation = GeoLocation;
                var _TagsBuilder = new Tags.Builder();
                this.Tags = Tags != null ? Tags(_TagsBuilder) : _TagsBuilder;
                this.IsDisabled = IsDisabled;
                this.AttachedFiles = AttachedFiles.SafeAny() ? new HashSet<AttachedFile>(AttachedFiles) : new HashSet<AttachedFile>();

                this.notifications = new NotificationStore(Notifications);

                this._User2Organization_Edges = User2OrganizationEdges.IsNeitherNullNorEmpty() ? new List<User2OrganizationEdge>(User2OrganizationEdges) : new List<User2OrganizationEdge>();
                this._Organization2Organization_InEdges = Organization2OrganizationInEdges.IsNeitherNullNorEmpty() ? new List<Organization2OrganizationEdge>(Organization2OrganizationInEdges) : new List<Organization2OrganizationEdge>();
                this._Organization2Organization_OutEdges = Organization2OrganizationOutEdges.IsNeitherNullNorEmpty() ? new List<Organization2OrganizationEdge>(Organization2OrganizationOutEdges) : new List<Organization2OrganizationEdge>();

            }

            #endregion


            #region Notifications

            private readonly NotificationStore notifications;

            #region (internal) AddNotification(Notification,                           OnUpdate = null)

            internal T AddNotification<T>(T          Notification,
                                          Action<T>  OnUpdate  = null)

                where T : ANotification

                => notifications.Add(Notification,
                                      OnUpdate);

            #endregion

            #region (internal) AddNotification(Notification, NotificationMessageType,  OnUpdate = null)

            internal T AddNotification<T>(T                        Notification,
                                          NotificationMessageType  NotificationMessageType,
                                          Action<T>                OnUpdate  = null)

                where T : ANotification

                => notifications.Add(Notification,
                                      NotificationMessageType,
                                      OnUpdate);

            #endregion

            #region (internal) AddNotification(Notification, NotificationMessageTypes, OnUpdate = null)

            internal T AddNotification<T>(T                                     Notification,
                                          IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                          Action<T>                             OnUpdate  = null)

                where T : ANotification

                => notifications.Add(Notification,
                                      NotificationMessageTypes,
                                      OnUpdate);

            #endregion


            #region GetNotifications  (NotificationMessageType = null)

            public IEnumerable<ANotification> GetNotifications(NotificationMessageType?  NotificationMessageType = null)
            {
                lock (notifications)
                {
                    return notifications.GetNotifications(NotificationMessageType);
                }
            }

            #endregion

            #region GetNotificationsOf(params NotificationMessageTypes)

            public IEnumerable<T> GetNotificationsOf<T>(params NotificationMessageType[] NotificationMessageTypes)

                where T : ANotification

            {

                lock (notifications)
                {
                    return notifications.GetNotificationsOf<T>(NotificationMessageTypes);
                }

            }

            #endregion

            #region GetNotifications  (NotificationMessageTypeFilter)

            public IEnumerable<ANotification> GetNotifications(Func<NotificationMessageType, Boolean> NotificationMessageTypeFilter)
            {
                lock (notifications)
                {
                    return notifications.GetNotifications(NotificationMessageTypeFilter);
                }
            }

            #endregion

            #region GetNotificationsOf(NotificationMessageTypeFilter)

            public IEnumerable<T> GetNotificationsOf<T>(Func<NotificationMessageType, Boolean> NotificationMessageTypeFilter)

                where T : ANotification

            {

                lock (notifications)
                {
                    return notifications.GetNotificationsOf<T>(NotificationMessageTypeFilter);
                }

            }

            #endregion


            #region GetNotificationInfos()

            public JObject GetNotificationInfos()

                => JSONObject.Create(new JProperty("user", JSONObject.Create(

                                         new JProperty("name",               EMail.OwnerName),
                                         new JProperty("email",              EMail.Address.ToString())

                                         //MobilePhone.HasValue
                                         //    ? new JProperty("phoneNumber",  MobilePhone.Value.ToString())
                                         //    : null

                                     )),
                                     new JProperty("notifications",  notifications.ToJSON()));

            #endregion


            #region (internal) RemoveNotification(NotificationType,                           OnRemoval = null)

            internal Task RemoveNotification<T>(T          NotificationType,
                                                Action<T>  OnRemoval  = null)

                where T : ANotification

                => notifications.Remove(NotificationType,
                                         OnRemoval);

            #endregion

            #endregion


            #region ToImmutable

            /// <summary>
            /// Return an immutable version of the organization.
            /// </summary>
            /// <param name="Builder">An organization builder.</param>
            public static implicit operator Organization(Builder Builder)

                => Builder?.ToImmutable;


            /// <summary>
            /// Return an immutable version of the organization.
            /// </summary>
            public Organization ToImmutable
            {
                get
                {

                    //if (!Branch.HasValue || Branch.Value.IsNullOrEmpty)
                    //    throw new ArgumentNullException(nameof(Branch), "The given branch must not be null or empty!");

                    return new Organization(Id,
                                            Name,
                                            Description,
                                            Website,
                                            EMail,
                                            Telephone,
                                            Address,
                                            GeoLocation,
                                            _ => Tags,
                                            IsDisabled,

                                            notifications,

                                            _User2Organization_Edges,
                                            _Organization2Organization_InEdges,
                                            _Organization2Organization_OutEdges,

                                            CustomData,
                                            AttachedFiles,
                                            JSONLDContext,
                                            DataSource,
                                            LastChangeDate);

                }
            }

            #endregion


            #region CopyAllLinkedDataFrom(OldOrganization)

            public override void CopyAllLinkedDataFromBase(Organization OldOrganization)
            {

                if (OldOrganization._User2Organization_Edges.Any() && !_User2Organization_Edges.Any())
                {

                    Add(OldOrganization._User2Organization_Edges);

                    foreach (var edge in _User2Organization_Edges)
                        edge.Target = this.ToImmutable;

                }

                if (OldOrganization._Organization2Organization_InEdges.Any() && !_Organization2Organization_InEdges.Any())
                {

                    AddInEdges(OldOrganization._Organization2Organization_InEdges);

                    foreach (var edge in _Organization2Organization_InEdges)
                        edge.Target = this.ToImmutable;

                }

                if (OldOrganization._Organization2Organization_OutEdges.Any() && !_Organization2Organization_OutEdges.Any())
                {

                    AddOutEdges(OldOrganization._Organization2Organization_OutEdges);

                    foreach (var edge in _Organization2Organization_OutEdges)
                        edge.Source = this.ToImmutable;

                }

                if (OldOrganization.notifications.SafeAny() && !notifications.SafeAny())
                    notifications.Add(OldOrganization.notifications);

            }

            #endregion


            #region Operator overloading

            #region Operator == (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization builder.</param>
            /// <param name="Builder2">Another organization builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator == (Builder? Builder1,
                                               Builder? Builder2)
            {

                // If both are null, or both are same instance, return true.
                if (Object.ReferenceEquals(Builder1, Builder2))
                    return true;

                // If one is null, but not both, return false.
                if ((Builder1 is null) || (Builder2 is null))
                    return false;

                return Builder1.Equals(Builder2);

            }

            #endregion

            #region Operator != (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization builder.</param>
            /// <param name="Builder2">Another organization builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator != (Builder? Builder1,
                                               Builder? Builder2)

                => !(Builder1 == Builder2);

            #endregion

            #region Operator <  (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization builder.</param>
            /// <param name="Builder2">Another organization builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator < (Builder? Builder1,
                                              Builder? Builder2)
            {

                if (Builder1 is null)
                    throw new ArgumentNullException(nameof(Builder1), "The given Builder1 must not be null!");

                return Builder1.CompareTo(Builder2) < 0;

            }

            #endregion

            #region Operator <= (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization builder.</param>
            /// <param name="Builder2">Another organization builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator <= (Builder? Builder1,
                                               Builder? Builder2)

                => !(Builder1 > Builder2);

            #endregion

            #region Operator >  (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization builder.</param>
            /// <param name="Builder2">Another organization builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator > (Builder? Builder1,
                                              Builder? Builder2)
            {

                if (Builder1 is null)
                    throw new ArgumentNullException(nameof(Builder1), "The given Builder1 must not be null!");

                return Builder1.CompareTo(Builder2) > 0;

            }

            #endregion

            #region Operator >= (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization builder.</param>
            /// <param name="Builder2">Another organization builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator >= (Builder? Builder1,
                                               Builder? Builder2)

                => !(Builder1 < Builder2);

            #endregion

            #endregion

            #region IComparable<Builder> Members

            #region CompareTo(Object)

            /// <summary>
            /// Compares two organizations.
            /// </summary>
            /// <param name="Object">An organization to compare with.</param>
            public override Int32 CompareTo(Object? Object)

                => Object is Builder builder
                       ? CompareTo(builder)
                       : throw new ArgumentException("The given object is not an organization!");

            #endregion

            #region CompareTo(Builder)

            /// <summary>
            /// Compares two organizations.
            /// </summary>
            /// <param name="Organization">An organization to compare with.</param>
            public override Int32 CompareTo(Organization? Organization)

                => Organization is null
                       ? throw new ArgumentNullException(nameof(Organization), "The given organization must not be null!")
                       : Id.CompareTo(Organization.Id);

            #endregion

            #region CompareTo(Builder)

            /// <summary>
            /// Compares two organizations.
            /// </summary>
            /// <param name="Organization">An organization to compare with.</param>
            public Int32 CompareTo(Builder? Builder)

                => Builder is null
                       ? throw new ArgumentNullException(nameof(Builder), "The given organization must not be null!")
                       : Id.CompareTo(Builder.Id);

            #endregion

            #endregion

            #region IEquatable<Builder> Members

            #region Equals(Object)

            /// <summary>
            /// Compares two organizations for equality.
            /// </summary>
            /// <param name="Object">An organization to compare with.</param>
            public override Boolean Equals(Object? Object)

                => Object is Builder builder &&
                      Equals(builder);

            #endregion

            #region Equals(Builder)

            /// <summary>
            /// Compares two organizations for equality.
            /// </summary>
            /// <param name="Organization">An organization to compare with.</param>
            public override Boolean Equals(Organization? Organization)

                => Organization is not null &&
                       Id.Equals(Organization.Id);

            #endregion

            #region Equals(Builder)

            /// <summary>
            /// Compares two organizations for equality.
            /// </summary>
            /// <param name="Builder">An organization to compare with.</param>
            public Boolean Equals(Builder? Builder)

                => Builder is not null &&
                       Id.Equals(Builder.Id);

            #endregion

            #endregion

            #region (override) GetHashCode()

            /// <summary>
            /// Get the hashcode of this object.
            /// </summary>
            public override Int32 GetHashCode()
                => Id.GetHashCode();

            #endregion

            #region (override) ToString()

            /// <summary>
            /// Return a text representation of this object.
            /// </summary>
            public override String ToString()
                => Id.ToString();

            #endregion

        }

        #endregion

    }

}
