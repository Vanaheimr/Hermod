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

using Org.BouncyCastle.Bcpg.OpenPgp;

using org.GraphDefined.Vanaheimr.Aegir;
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.BouncyCastle;

using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate Boolean UserProviderDelegate(User_Id UserId, out IUser? User);

    public delegate JObject CustomUserSerializerDelegate(IUser    User,
                                                         Boolean  Embedded   = false);



    public delegate User OverwriteUserDelegate(IUser User);


    /// <summary>
    /// Extension methods for Users.
    /// </summary>
    public static class UserExtensions
    {

        #region ToJSON(this Users, Skip = null, Take = null, Embedded = false, ...)

        /// <summary>
        /// Return a JSON representation for the given enumeration of Users.
        /// </summary>
        /// <param name="Users">An enumeration of Users.</param>
        /// <param name="Skip">The optional number of Users to skip.</param>
        /// <param name="Take">The optional number of Users to return.</param>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        public static JArray ToJSON(this IEnumerable<IUser>        Users,
                                    UInt64?                        Skip         = null,
                                    UInt64?                        Take         = null,
                                    Boolean                        Embedded     = false,
                                    CustomUserSerializerDelegate?  UserToJSON   = null)


            => Users?.Any() != true

                   ? new JArray()

                   : new JArray(Users.
                                    Where     (dataSet =>  dataSet is not null).
                                    OrderBy   (dataSet => dataSet.Id).
                                    SkipTakeFilter(Skip, Take).
                                    SafeSelect(User => UserToJSON is not null
                                                                    ? UserToJSON (User,
                                                                                  Embedded)
                                                                    : User.ToJSON(Embedded)));

        #endregion

    }

    public enum Use2AuthFactor
    {

        /// <summary>
        /// Do not use any second authentication factor.
        /// </summary>
        None,

        /// <summary>
        /// Use a SMS via the user's mobile phone as second authentication factor.
        /// </summary>
        MobilePhoneSMS

    }



    /// <summary>
    /// A user.
    /// </summary>
    public class User : AEntity<User_Id, User>,
                        IUser
    {

        #region Data

        /// <summary>
        /// The default max size of the aggregated EVSE operator status history.
        /// </summary>
        public const UInt16 DefaultUserStatusHistorySize = 50;

        /// <summary>
        /// The default JSON-LD context of users.
        /// </summary>
        public readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/user");

        #endregion

        #region Properties

        #region API

        private HTTPExtAPI? api;

        /// <summary>
        /// The HTTPExtAPI of this user.
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
                    throw new ArgumentException("Illegal attempt to change the API of this user!");

                api = value ?? throw new ArgumentException("Illegal attempt to delete the API reference of this user!");

            }

        }

        #endregion


        /// <summary>
        /// The primary E-Mail address of the user.
        /// </summary>
        [Mandatory]
        public EMailAddress               EMail                { get; }

        /// <summary>
        /// The PGP/GPG public keyring of the user.
        /// </summary>
        [Optional]
        public PgpPublicKeyRing?          PublicKeyRing        { get; }

        /// <summary>
        /// The PGP/GPG secret keyring of the user.
        /// </summary>
        [Optional]
        public PgpSecretKeyRing?          SecretKeyRing        { get; }

        /// <summary>
        /// The language setting of the user.
        /// </summary>
        [Mandatory]
        public Languages                  UserLanguage         { get; }

        /// <summary>
        /// The telephone number of the user.
        /// </summary>
        [Optional]
        public PhoneNumber?               Telephone            { get; }

        /// <summary>
        /// The mobile telephone number of the user.
        /// </summary>
        [Optional]
        public PhoneNumber?               MobilePhone          { get; }

        /// <summary>
        /// Whether to use a second authentication factor.
        /// </summary>
        public Use2AuthFactor             Use2AuthFactor       { get; }

        /// <summary>
        /// The telegram user name.
        /// </summary>
        [Optional]
        public String?                    Telegram             { get; }

        /// <summary>
        /// The homepage of the user.
        /// </summary>
        [Optional]
        public String?                    Homepage             { get; }

        /// <summary>
        /// The geographical location of this organization.
        /// </summary>
        public GeoCoordinate?             GeoLocation          { get; }

        /// <summary>
        /// The optional address of the organization.
        /// </summary>
        [Optional]
        public Address?                   Address              { get; }

        /// <summary>
        /// Whether the user will be shown in user listings, or not.
        /// </summary>
        [Mandatory]
        public PrivacyLevel               PrivacyLevel         { get; }

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

        /// <summary>
        /// An enumeration of attached files.
        /// </summary>
        [Optional]
        public IEnumerable<AttachedFile>  AttachedFiles        { get; }

        #endregion

        #region User <-> User         edges

        private readonly List<User2UserEdge> _User2User_Edges;

        public IEnumerable<User2UserEdge> __User2UserEdges
            => _User2User_Edges;


        public User2UserEdge

            Add(User2UserEdge Edge)

                => _User2User_Edges.AddAndReturnElement(Edge);


        public IEnumerable<User2UserEdge>

            Add(IEnumerable<User2UserEdge> Edges)

                => _User2User_Edges.AddAndReturnList(Edges);



        #region AddIncomingEdge(Edge)

        public User2UserEdge

            AddIncomingEdge(User2UserEdge  Edge)

            => _User2User_Edges.AddAndReturnElement(Edge);

        #endregion

        #region AddIncomingEdge(SourceUser, EdgeLabel, PrivacyLevel = PrivacyLevel.World)

        public User2UserEdge

            AddIncomingEdge(IUser               SourceUser,
                            User2UserEdgeTypes  EdgeLabel,
                            PrivacyLevel        PrivacyLevel = PrivacyLevel.World)

            => _User2User_Edges.AddAndReturnElement(new User2UserEdge(SourceUser, EdgeLabel, this, PrivacyLevel));

        #endregion

        #region AddOutgoingEdge(Edge)

        public User2UserEdge

            AddOutgoingEdge(User2UserEdge  Edge)

            => _User2User_Edges.AddAndReturnElement(Edge);

        #endregion

        #region AddOutgoingEdge(SourceUser, EdgeLabel, PrivacyLevel = PrivacyLevel.World)

        public User2UserEdge

            AddOutgoingEdge(User2UserEdgeTypes  EdgeLabel,
                            IUser               Target,
                            PrivacyLevel        PrivacyLevel = PrivacyLevel.World)

            => _User2User_Edges.AddAndReturnElement(new User2UserEdge(this, EdgeLabel, Target, PrivacyLevel));

        #endregion


        #region Genimi

        /// <summary>
        /// The gemini of this user.
        /// </summary>
        public IEnumerable<IUser> Genimi
        {
            get
            {
                return _User2User_Edges.
                           Where (edge => edge.EdgeLabel == User2UserEdgeTypes.gemini).
                           Select(edge => edge.Target);
            }
        }

        #endregion

        #region FollowsUsers

        /// <summary>
        /// This user follows this other users.
        /// </summary>
        public IEnumerable<IUser> FollowsUsers
        {
            get
            {
                return _User2User_Edges.
                           Where(edge => edge.EdgeLabel == User2UserEdgeTypes.follows).
                           Select(edge => edge.Target);
            }
        }

        #endregion

        #region IsFollowedBy

        /// <summary>
        /// This user is followed by this other users.
        /// </summary>
        public IEnumerable<IUser> IsFollowedBy
        {
            get
            {
                return _User2User_Edges.
                           Where(edge => edge.EdgeLabel == User2UserEdgeTypes.IsFollowedBy).
                           Select(edge => edge.Target);
            }
        }

        #endregion

        #endregion

        #region User  -> Organization edges

        private readonly List<User2OrganizationEdge> _User2Organization_Edges;

        public IEnumerable<User2OrganizationEdge> User2Organization_OutEdges
            => _User2Organization_Edges;


        public User2OrganizationEdge

            Add(User2OrganizationEdge Edge)

                => _User2Organization_Edges.AddAndReturnElement(Edge);


        public IEnumerable<User2OrganizationEdge>

            Add(IEnumerable<User2OrganizationEdge> Edges)

                => _User2Organization_Edges.AddAndReturnList(Edges);




        public User2OrganizationEdge

            AddOutgoingEdge(User2OrganizationEdgeLabel  EdgeLabel,
                            IOrganization               Target,
                            PrivacyLevel                PrivacyLevel = PrivacyLevel.World)

            => _User2Organization_Edges.AddAndReturnElement(new User2OrganizationEdge(this, EdgeLabel, Target, PrivacyLevel));



        public User2UserGroupEdge

            AddToUserGroup(User2UserGroupEdgeLabel  EdgeLabel,
                           IUserGroup               Target,
                           PrivacyLevel             PrivacyLevel = PrivacyLevel.World)

            => _User2UserGroup_Edges.AddAndReturnElement(new User2UserGroupEdge(this, EdgeLabel, Target, PrivacyLevel));


        public IEnumerable<User2UserGroupEdge> User2GroupOutEdges(Func<User2UserGroupEdgeLabel, Boolean> User2GroupEdgeFilter)
            => _User2UserGroup_Edges.Where(edge => User2GroupEdgeFilter(edge.EdgeLabel));


        #region EdgeLabels(Organization)

        /// <summary>
        /// All organizations this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        public IEnumerable<User2OrganizationEdgeLabel> EdgeLabels(IOrganization Organization)

            => _User2Organization_Edges.
                   Where (edge => edge.Target == Organization).
                   Select(edge => edge.EdgeLabel);

        #endregion

        #region Edges     (Organization)

        /// <summary>
        /// All organizations this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        public IEnumerable<User2OrganizationEdge> Edges(IOrganization Organization)

            => _User2Organization_Edges.
                   Where(edge => edge.Target == Organization);

        #endregion

        #region Edges     (EdgeLabel, Organization)

        /// <summary>
        /// All organizations this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        public IEnumerable<User2OrganizationEdge> Edges(IOrganization               Organization,
                                                        User2OrganizationEdgeLabel  EdgeLabel)

            => _User2Organization_Edges.
                   Where(edge => edge.Target == Organization && edge.EdgeLabel == EdgeLabel);

        #endregion



        #region ParentOrganizations()

        public IEnumerable<IOrganization> ParentOrganizations()

            => _User2Organization_Edges.
                   Where(edge  => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdmin  ||
                                  edge.EdgeLabel == User2OrganizationEdgeLabel.IsMember ||
                                  edge.EdgeLabel == User2OrganizationEdgeLabel.IsGuest).
                   Select(edge => edge.Target).
                   ToArray();

        #endregion

        #region Organizations(RequireAdminAccess, RequireReadWriteAccess, Recursive = true)

        public IEnumerable<IOrganization> Organizations(Access_Levels  AccessLevel,
                                                        Boolean        Recursive = true)
        {

            var allMyOrganizations = new HashSet<IOrganization>();

            switch (AccessLevel)
            {

                case Access_Levels.Admin:
                    foreach (var organization in _User2Organization_Edges.
                                                     Where (edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdmin).
                                                     Select(edge => edge.Target))
                    {
                        allMyOrganizations.Add(organization);
                    }
                    break;

                case Access_Levels.AdminReadOnly:
                    foreach (var organization in _User2Organization_Edges.
                                                     Where (edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdminReadOnly).
                                                     Select(edge => edge.Target))
                    {
                        allMyOrganizations.Add(organization);
                    }
                    break;

                case Access_Levels.ReadWrite:
                    foreach (var organization in _User2Organization_Edges.
                                                     Where (edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdmin ||
                                                                    edge.EdgeLabel == User2OrganizationEdgeLabel.IsMember).
                                                     Select(edge => edge.Target))
                    {
                        allMyOrganizations.Add(organization);
                    }
                    break;

                default:
                    foreach (var organization in _User2Organization_Edges.
                                                     Where (edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdmin  ||
                                                                    edge.EdgeLabel == User2OrganizationEdgeLabel.IsMember ||
                                                                    edge.EdgeLabel == User2OrganizationEdgeLabel.IsGuest).
                                                     Select(edge => edge.Target))
                    {
                        allMyOrganizations.Add(organization);
                    }
                    break;

            }


            if (Recursive)
            {

                IOrganization[]? Level2 = null;

                do
                {

                    Level2 = allMyOrganizations.SelectMany(organization => organization.
                                                                               Organization2OrganizationInEdges.
                                                                               Where(edge => edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf)).
                                                Select    (edge         => edge.Source).
                                                Where     (organization => !allMyOrganizations.Contains(organization)).
                                                ToArray();

                    foreach (var organization in Level2)
                        allMyOrganizations.Add(organization);

                } while (Level2.Length > 0);

            }

            return allMyOrganizations;

        }

        #endregion

        #region HasAccessToOrganization(AccessLevel, Organization,   Recursive = true)

        public Boolean HasAccessToOrganization(Access_Levels  AccessLevel,
                                               IOrganization  Organization,
                                               Boolean        Recursive = true)

            => !(Organization is null) &&
                 Organizations(AccessLevel, Recursive).Contains(Organization);

        #endregion

        #region HasAccessToOrganization(AccessLevel, OrganizationId, Recursive = true)

        public Boolean HasAccessToOrganization(Access_Levels    AccessLevel,
                                               Organization_Id  OrganizationId,
                                               Boolean          Recursive = true)

            => !OrganizationId.IsNullOrEmpty &&
                Organizations(AccessLevel, Recursive).Any(org => org.Id == OrganizationId);

        #endregion


        public Boolean RemoveOutEdge(User2OrganizationEdge Edge)
            => _User2Organization_Edges.Remove(Edge);

        #endregion

        #region User  -> UserGroup    edges

        private readonly List<User2UserGroupEdge> _User2UserGroup_Edges;

        public IEnumerable<User2UserGroupEdge> User2Group_OutEdges
            => _User2UserGroup_Edges;



        public User2UserGroupEdge

            Add(User2UserGroupEdge Edge)

                => _User2UserGroup_Edges.AddAndReturnElement(Edge);


        public IEnumerable<User2UserGroupEdge>

            Add(IEnumerable<User2UserGroupEdge> Edges)

                => _User2UserGroup_Edges.AddAndReturnList(Edges);


        #region UserGroups(EdgeFilter)

        /// <summary>
        /// All groups this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        public IEnumerable<IUserGroup> UserGroups(User2UserGroupEdgeLabel EdgeFilter)

            => _User2UserGroup_Edges.
                   Where(edge => edge.EdgeLabel == EdgeFilter).
                   Select(edge => edge.Target);

        #endregion

        #region UserGroups(RequireReadWriteAccess = false, Recursive = false)

        public IEnumerable<IUserGroup> UserGroups(Boolean RequireReadWriteAccess = false,
                                                  Boolean Recursive = false)
        {

            var _Groups = RequireReadWriteAccess

                                     ? _User2UserGroup_Edges.
                                           Where(edge => edge.EdgeLabel == User2UserGroupEdgeLabel.IsAdmin ||
                                                         edge.EdgeLabel == User2UserGroupEdgeLabel.IsMember).
                                           Select(edge => edge.Target).
                                           ToList()

                                     : _User2UserGroup_Edges.
                                           Where(edge => edge.EdgeLabel == User2UserGroupEdgeLabel.IsAdmin ||
                                                         edge.EdgeLabel == User2UserGroupEdgeLabel.IsMember ||
                                                         edge.EdgeLabel == User2UserGroupEdgeLabel.IsGuest).
                                           Select(edge => edge.Target).
                                           ToList();

            //if (Recursive)
            //{

            //    Group[] Level2 = null;

            //    do
            //    {

            //        Level2 = _Groups.SelectMany(group => group.
            //                                                 Group2GroupInEdges.
            //                                                 Where(edge => edge.EdgeLabel == Group2GroupEdges.IsChildOf)).
            //                         Select    (edge  => edge.Target).
            //                         Where     (group => !_Groups.Contains(group)).
            //                         ToArray();

            //        foreach (var organization in Level2)
            //            _Groups.Add(organization);

            //    } while (Level2.Length > 0);

            //}

            return new HashSet<IUserGroup>(_Groups);

        }

        #endregion

        #region EdgeLabels(UserGroup)

        /// <summary>
        /// All user groups this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        public IEnumerable<User2UserGroupEdgeLabel> EdgeLabels(UserGroup UserGroup)

            => _User2UserGroup_Edges.
                   Where(edge => edge.Target == UserGroup).
                   Select(edge => edge.EdgeLabel);

        #endregion

        #region Edges     (UserGroup)

        /// <summary>
        /// All organizations this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        public IEnumerable<User2UserGroupEdge> Edges(UserGroup UserGroup)

            => _User2UserGroup_Edges.
                   Where(edge => edge.Target == UserGroup);

        #endregion

        #region Edges     (EdgeLabel, UserGroup)

        /// <summary>
        /// All organizations this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        public IEnumerable<User2UserGroupEdge> Edges(User2UserGroupEdgeLabel  EdgeLabel,
                                                     UserGroup                UserGroup)

            => _User2UserGroup_Edges.
                   Where(edge => edge.Target == UserGroup && edge.EdgeLabel == EdgeLabel);

        #endregion

        public Boolean HasEdge(User2UserGroupEdgeLabel  EdgeLabel,
                               UserGroup                UserGroup)

            => _User2UserGroup_Edges.
                   Any(edge => edge.Target == UserGroup && edge.EdgeLabel == EdgeLabel);


        public Boolean RemoveOutEdge(User2UserGroupEdge Edge)
            => _User2UserGroup_Edges.Remove(Edge);

        #endregion

        #region Events

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new user.
        /// </summary>
        /// <param name="Id">The unique identification of the user.</param>
        /// <param name="Name">An offical (multi-language) name of the user.</param>
        /// <param name="EMail">The primary e-mail of the user.</param>
        /// 
        /// <param name="Description">An optional (multi-language) description of the user.</param>
        /// <param name="PublicKeyRing">An optional PGP/GPG public keyring of the user.</param>
        /// <param name="SecretKeyRing">An optional PGP/GPG secret keyring of the user.</param>
        /// <param name="UserLanguage">The language setting of the user.</param>
        /// <param name="Telephone">An optional telephone number of the user.</param>
        /// <param name="MobilePhone">An optional mobile telephone number of the user.</param>
        /// <param name="Use2AuthFactor">Whether to use a second authentication factor.</param>
        /// <param name="Telegram">An optional telegram account name of the user.</param>
        /// <param name="Homepage">The homepage of the user.</param>
        /// <param name="GeoLocation">An optional geographical location of the user.</param>
        /// <param name="Address">An optional address of the user.</param>
        /// <param name="AcceptedEULA">Timestamp when the user accepted the End-User-License-Agreement.</param>
        /// <param name="IsDisabled">The user is disabled.</param>
        /// <param name="IsAuthenticated">The user will not be shown in user listings, as its primary e-mail address is not yet authenticated.</param>
        /// 
        /// <param name="CustomData">Custom data to be stored with this user.</param>
        /// <param name="AttachedFiles">Optional files attached to this user.</param>
        /// <param name="JSONLDContext">The JSON-LD context of this user.</param>
        /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
        /// <param name="LastChange">The timestamp of the last changes within this user. Can e.g. be used as a HTTP ETag.</param>
        public User(User_Id                              Id,
                    I18NString                           Name,
                    SimpleEMailAddress                   EMail,

                    I18NString?                          Description              = null,
                    PgpPublicKeyRing?                    PublicKeyRing            = null,
                    PgpSecretKeyRing?                    SecretKeyRing            = null,
                    Languages                            UserLanguage             = Languages.en,
                    PhoneNumber?                         Telephone                = null,
                    PhoneNumber?                         MobilePhone              = null,
                    Use2AuthFactor                       Use2AuthFactor           = Use2AuthFactor.None,
                    String?                              Telegram                 = null,
                    String?                              Homepage                 = null,
                    GeoCoordinate?                       GeoLocation              = null,
                    Address?                             Address                  = null,
                    DateTime?                            AcceptedEULA             = null,
                    Boolean                              IsDisabled               = false,
                    Boolean                              IsAuthenticated          = false,

                    IEnumerable<ANotification>?          Notifications            = null,

                    IEnumerable<User2UserEdge>?          User2UserEdges           = null,
                    IEnumerable<User2UserGroupEdge>?     User2UserGroupEdges      = null,
                    IEnumerable<User2OrganizationEdge>?  User2OrganizationEdges   = null,

                    JObject?                             CustomData               = default,
                    IEnumerable<AttachedFile>?           AttachedFiles            = default,
                    JSONLDContext?                       JSONLDContext            = default,
                    String?                              DataSource               = default,
                    DateTime?                            LastChange               = default)

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

            #region Initial checks

            if (Name.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Name), "The given username must not be null or empty!");

            #endregion

            this.EMail                     = new EMailAddress(
                                                 EMail,
                                                 Name.FirstText(),
                                                 SecretKeyRing,
                                                 PublicKeyRing
                                             );
            this.PublicKeyRing             = PublicKeyRing;
            this.SecretKeyRing             = SecretKeyRing;
            this.UserLanguage              = UserLanguage;
            this.Telephone                 = Telephone;
            this.MobilePhone               = MobilePhone;
            this.Use2AuthFactor            = Use2AuthFactor;
            this.Telegram                  = Telegram;
            this.Homepage                  = Homepage;
            this.GeoLocation               = GeoLocation;
            this.Address                   = Address;
            this.AcceptedEULA              = AcceptedEULA;
            this.IsAuthenticated           = IsAuthenticated;
            this.IsDisabled                = IsDisabled;
            this.AttachedFiles             = AttachedFiles ?? Array.Empty<AttachedFile>();

            this.notificationStore         = new NotificationStore(Notifications);

            this._User2User_Edges          = User2UserEdges         is not null && User2UserEdges.        IsNeitherNullNorEmpty() ? new List<User2UserEdge>        (User2UserEdges)         : new List<User2UserEdge>();
            this._User2UserGroup_Edges     = User2UserGroupEdges    is not null && User2UserGroupEdges.   IsNeitherNullNorEmpty() ? new List<User2UserGroupEdge>   (User2UserGroupEdges)    : new List<User2UserGroupEdge>();
            this._User2Organization_Edges  = User2OrganizationEdges is not null && User2OrganizationEdges.IsNeitherNullNorEmpty() ? new List<User2OrganizationEdge>(User2OrganizationEdges) : new List<User2OrganizationEdge>();

        }

        #endregion


        #region Notifications

        private readonly NotificationStore notificationStore;

        #region AddNotification(Notification,                           OnUpdate = null)

        public T AddNotification<T>(T           Notification,
                                    Action<T>?  OnUpdate   = null)

            where T : ANotification

            => notificationStore.Add(Notification,
                                     OnUpdate);

        #endregion

        #region AddNotification(Notification, NotificationMessageType,  OnUpdate = null)

        public T AddNotification<T>(T                        Notification,
                                    NotificationMessageType  NotificationMessageType,
                                    Action<T>?               OnUpdate   = null)

            where T : ANotification

            => notificationStore.Add(Notification,
                                     NotificationMessageType,
                                     OnUpdate);

        #endregion

        #region (internal) AddNotification(Notification, NotificationMessageTypes, OnUpdate = null)

        public T AddNotification<T>(T                                     Notification,
                                    IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                    Action<T>?                            OnUpdate = null)

            where T : ANotification

            => notificationStore.Add(Notification,
                                     NotificationMessageTypes,
                                     OnUpdate);

        #endregion

        #region RemoveNotification(NotificationType,                           OnRemoval = null)

        public Task RemoveNotification<T>(T           NotificationType,
                                          Action<T>?  OnRemoval   = null)

            where T : ANotification

            => notificationStore.Remove(NotificationType,
                                        OnRemoval);

        #endregion


        #region GetNotifications  (NotificationMessageType = null)

        public IEnumerable<ANotification> GetNotifications(NotificationMessageType? NotificationMessageType = null)
        {
            lock (notificationStore)
            {
                return notificationStore.GetNotifications(NotificationMessageType);
            }
        }

        #endregion

        #region GetNotificationsOf(params NotificationMessageTypes)

        public IEnumerable<T> GetNotificationsOf<T>(params NotificationMessageType[] NotificationMessageTypes)

            where T : ANotification

        {

            lock (notificationStore)
            {
                return notificationStore.GetNotificationsOf<T>(NotificationMessageTypes);
            }

        }

        #endregion

        #region GetNotifications  (NotificationMessageTypeFilter)

        public IEnumerable<ANotification> GetNotifications(Func<NotificationMessageType, Boolean> NotificationMessageTypeFilter)
        {
            lock (notificationStore)
            {
                return notificationStore.GetNotifications(NotificationMessageTypeFilter);
            }
        }

        #endregion

        #region GetNotificationsOf(NotificationMessageTypeFilter)

        public IEnumerable<T> GetNotificationsOf<T>(Func<NotificationMessageType, Boolean> NotificationMessageTypeFilter)

            where T : ANotification

        {

            lock (notificationStore)
            {
                return notificationStore.GetNotificationsOf<T>(NotificationMessageTypeFilter);
            }

        }

        #endregion


        #region GetNotificationInfo(NotificationId)

        public JObject GetNotificationInfo(UInt32 NotificationId)
        {

            var notification = notificationStore.ToJSON(NotificationId);

            notification.Add(new JProperty("user", JSONObject.Create(

                                     new JProperty("name", EMail.OwnerName),
                                     new JProperty("email", EMail.Address.ToString()),

                                     MobilePhone.HasValue
                                         ? new JProperty("phoneNumber", MobilePhone.Value.ToString())
                                         : null

                                 )));

            return notification;

        }

        #endregion

        #region GetNotificationInfos()

        public JObject GetNotificationInfos()

            => JSONObject.Create(new JProperty("user", JSONObject.Create(

                                     new JProperty("name", EMail.OwnerName),
                                     new JProperty("email", EMail.Address.ToString()),

                                     MobilePhone.HasValue
                                         ? new JProperty("phoneNumber", MobilePhone.Value.ToString())
                                         : null

                                 )),
                                 new JProperty("notifications", notificationStore.ToJSON()));

        #endregion

        #endregion


        #region (static) TryParseJSON(JSONObject, ..., out User, out ErrorResponse, ...)

        public static Boolean TryParseJSON(JObject      JSONObject,
                                           out User?    User,
                                           out String?  ErrorResponse,
                                           User_Id?     UserIdURL           = null,
                                           Byte?        MinUserIdLength     = 0,
                                           Byte?        MinUserNameLength   = 0)
        {

            try
            {

                User = null;

                #region Parse UserId           [optional]

                if (JSONObject.ParseOptional("@id",
                                             "user identification",
                                             User_Id.TryParse,
                                             out User_Id? UserIdBody,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                if (!UserIdURL.HasValue && !UserIdBody.HasValue)
                {
                    ErrorResponse = "The user identification is missing!";
                    return false;
                }

                if (UserIdURL.HasValue && UserIdBody.HasValue && UserIdURL.Value != UserIdBody.Value)
                {
                    ErrorResponse = "The optional user identification given within the JSON body does not match the one given in the URI!";
                    return false;
                }

                var userId = UserIdBody ?? UserIdURL.Value;

                if (userId.Length < MinUserIdLength)
                {
                    ErrorResponse = "The given user identification '" + userId + "' is too short!";
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

                if (Context != DefaultJSONLDContext)
                {
                    ErrorResponse = @"The given JSON-LD ""@context"" information '" + Context + "' is not supported!";
                    return false;
                }

                #endregion

                #region Parse Name             [mandatory]

                if (!JSONObject.ParseMandatory("name",
                                               "Username",
                                               out I18NString Name,
                                               out ErrorResponse))
                {
                    return false;
                }

                if (Name.FirstText().Length < MinUserNameLength)
                {
                    ErrorResponse = "The given user name '" + Name.FirstText() + "' is too short!";
                    return false;
                }

                #endregion

                #region Parse E-Mail           [mandatory]

                if (!JSONObject.ParseMandatory("email",
                                               "E-Mail",
                                               SimpleEMailAddress.TryParse,
                                               out SimpleEMailAddress EMail,
                                               out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Description      [optional]

                if (JSONObject.ParseOptional("description",
                                             "user description",
                                             out I18NString? Description,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse PublicKeyRing    [optional]

                if (JSONObject.ParseOptional("publicKeyRing",
                                             "GPG/PGP public key ring",
                                             txt => OpenPGP.ReadPublicKeyRing(txt.HexStringToByteArray()),
                                             out PgpPublicKeyRing? PublicKeyRing,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse SecretKeyRing    [optional]

                if (JSONObject.ParseOptional("secretKeyRing",
                                             "GPG/PGP secret key ring",
                                             txt => OpenPGP.ReadSecretKeyRing(txt.HexStringToByteArray()),
                                             out PgpSecretKeyRing? SecretKeyRing,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse Language         [optional]

                if (!JSONObject.ParseOptional("language",
                                              "user language",
                                              LanguagesExtensions.TryParse,
                                              out Languages? UserLanguage,
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

                #region Parse MobilePhone      [optional]

                if (JSONObject.ParseOptional("mobilePhone",
                                             "mobile phone number",
                                             PhoneNumber.TryParse,
                                             out PhoneNumber? MobilePhone,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse Use2AuthFactor   [optional]

                if (JSONObject.ParseOptionalEnum("use2AuthFactor",
                                                 "use a second authentication factor",
                                                 out Use2AuthFactor? Use2AuthFactor,
                                                 out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse Telegram         [optional]

                if (JSONObject.ParseOptional("telegram",
                                             "telegram user name",
                                             out String? Telegram,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse Homepage         [optional]

                if (JSONObject.ParseOptional("homepage",
                                             "homepage",
                                             out String? Homepage,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse GeoLocation      [optional]

                if (JSONObject.ParseOptionalStruct("geoLocation",
                                                   "Geo location",
                                                   GeoCoordinate.TryParseJSON,
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

                #region Parse AcceptedEULA     [optional]

                if (JSONObject.ParseOptional("acceptedEULA",
                                             "accepted EULA",
                                             out DateTime? AcceptedEULA,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                var IsAuthenticated  = JSONObject["isAuthenticated"]?.Value<Boolean>();

                var IsDisabled       = JSONObject["isDisabled"]?.     Value<Boolean>();

                #region Get   DataSource       [optional]

                var DataSource = JSONObject.GetOptional("dataSource");

                #endregion

                #region Parse CryptoHash       [optional]

                var CryptoHash    = JSONObject.GetOptional("cryptoHash");

                #endregion


                User = new User(
                           userId,
                           Name,
                           EMail,
                           Description,
                           PublicKeyRing,
                           SecretKeyRing,
                           UserLanguage   ?? Languages.en,
                           Telephone,
                           MobilePhone,
                           Use2AuthFactor ?? HTTP.Use2AuthFactor.None,
                           Telegram,
                           Homepage,
                           GeoLocation,
                           Address,
                           AcceptedEULA,
                           IsAuthenticated ?? false,
                           IsDisabled ?? false,

                           null,
                           null,
                           null,
                           null,

                           null, //CustomData,
                           null, //AttachedFiles,
                           null, //JSONLDContext,
                           DataSource,
                           null
                       ); //LastChange

                ErrorResponse = null;
                return true;

            }
            catch (Exception e)
            {
                ErrorResponse  = e.Message;
                User           = null;
                return false;
            }

        }

        #endregion

        #region ToJSON(Embedded = false)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        public override JObject ToJSON(Boolean Embedded = false)

            => ToJSON(Embedded,
                      InfoStatus.Hidden,
                      InfoStatus.Hidden,
                      true);


        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        /// <param name="ExpandOrganizations">Whether to expand the organizations this user is a member of.</param>
        /// <param name="ExpandGroups">Whether to expand the groups this user is a member of.</param>
        /// <param name="IncludeLastChange">Whether to include the lastChange timestamp of this object.</param>
        /// <param name="CustomUserSerializer">A delegate to serialize custom user JSON objects.</param>
        public JObject ToJSON(Boolean                                 Embedded               = false,
                              InfoStatus                              ExpandOrganizations    = InfoStatus.Hidden,
                              InfoStatus                              ExpandGroups           = InfoStatus.Hidden,
                              Boolean                                 IncludeLastChange      = true,
                              CustomJObjectSerializerDelegate<User>?  CustomUserSerializer   = null)

        {

            var JSON = base.ToJSON(Embedded,
                                   IncludeLastChange,
                                   null,
                                   new JProperty?[] {

                                             new JProperty("name",             Name.                      ToJSON()),

                                       Description.IsNotNullOrEmpty()
                                           ? new JProperty("description",      Description.               ToJSON())
                                           : null,

                                       new JProperty("email",                  EMail.Address.             ToString()),

                                       PublicKeyRing is not null
                                           ? new JProperty("publicKeyRing",    PublicKeyRing.GetEncoded().ToHexString())
                                           : null,

                                       SecretKeyRing is not null
                                           ? new JProperty("secretKeyRing",    SecretKeyRing.GetEncoded().ToHexString())
                                           : null,

                                       new JProperty("language",               UserLanguage.              AsText()),

                                       Telephone.HasValue
                                           ? new JProperty("telephone",        Telephone.                 ToString())
                                           : null,

                                       MobilePhone.HasValue
                                           ? new JProperty("mobilePhone",      MobilePhone.               ToString())
                                           : null,

                                       Use2AuthFactor != Use2AuthFactor.None
                                           ? new JProperty("use2AuthFactor",   Use2AuthFactor.            ToString())
                                           : null,

                                       Telegram.IsNotNullOrEmpty()
                                           ? new JProperty("telegram",         Telegram)
                                           : null,

                                       Homepage is not null && Homepage.IsNotNullOrEmpty()
                                           ? new JProperty("homepage",         Homepage.                  ToString())
                                           : null,

                                       PrivacyLevel.                                                      ToJSON(),

                                       AcceptedEULA.HasValue
                                           ? new JProperty("acceptedEULA",     AcceptedEULA.Value.        ToIso8601())
                                           : null,

                                       new JProperty("isAuthenticated",        IsAuthenticated),
                                       new JProperty("isDisabled",             IsDisabled)

                                       //new JProperty("signatures",           new JArray()),

                                       //ExpandOrganizations.Switch(
                                       //    () => new JProperty("organizationIds",   Owner.Id.ToString()),
                                       //    () => new JProperty("organizations",     Owner.ToJSON())),

                                   });

            return CustomUserSerializer is not null
                       ? CustomUserSerializer(this, JSON)
                       : JSON;

        }

        #endregion

        #region Clone(NewUserId = null)

        /// <summary>
        /// Clone this object.
        /// </summary>
        /// <param name="NewUserId">An optional new user identification.</param>
        public User Clone(User_Id? NewUserId = null)

            => new (NewUserId ?? Id.Clone,
                    Name,
                    EMail.Address,
                    Description?.Clone,
                    PublicKeyRing,
                    SecretKeyRing,
                    UserLanguage,
                    Telephone?.Clone,
                    MobilePhone,
                    Use2AuthFactor,
                    Telegram,
                    Homepage,
                    GeoLocation,
                    Address,
                    AcceptedEULA,
                    IsDisabled,
                    IsAuthenticated,

                    notificationStore,

                    _User2User_Edges,
                    _User2UserGroup_Edges,
                    _User2Organization_Edges,

                    CustomData,
                    AttachedFiles,
                    JSONLDContext,
                    DataSource,
                    LastChangeDate);

        #endregion


        #region CopyAllLinkedDataFrom(OldUser)

        public void CopyAllLinkedDataFrom(IUser OldUser)
            => CopyAllLinkedDataFromBase(OldUser as User);

        public override void CopyAllLinkedDataFromBase(User OldUser)
        {

            if (OldUser.__User2UserEdges.Any() && !__User2UserEdges.Any())
            {

                Add(OldUser.__User2UserEdges);

                foreach (var edge in __User2UserEdges)
                    edge.Source = this;

            }

            if (OldUser.User2Organization_OutEdges.Any() && !User2Organization_OutEdges.Any())
            {

                Add(OldUser.User2Organization_OutEdges);

                foreach (var edge in User2Organization_OutEdges)
                    edge.Source = this;

            }

            if (OldUser.User2Group_OutEdges.Any() && !User2Group_OutEdges.Any())
            {

                Add(OldUser.User2Group_OutEdges);

                foreach (var edge in User2Group_OutEdges)
                    edge.Source = this;

            }

            if (OldUser.notificationStore.SafeAny() && !notificationStore.SafeAny())
                notificationStore.Add(OldUser.notificationStore);

        }

        #endregion


        #region Operator overloading

        #region Operator == (User1, User2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="User1">A user.</param>
        /// <param name="User2">Another user.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (User? User1,
                                           User? User2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(User1, User2))
                return true;

            // If one is null, but not both, return false.
            if (User1 is null || User2 is null)
                return false;

            return User1.Equals(User2);

        }

        #endregion

        #region Operator != (User1, User2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="User1">A user.</param>
        /// <param name="User2">Another user.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (User? User1,
                                           User? User2)

            => !(User1 == User2);

        #endregion

        #region Operator <  (User1, User2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="User1">A user.</param>
        /// <param name="User2">Another user.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (User? User1,
                                          User? User2)
        {

            if (User1 is null)
                throw new ArgumentNullException(nameof(User1), "The given User1 must not be null!");

            return User1.CompareTo(User2) < 0;

        }

        #endregion

        #region Operator <= (User1, User2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="User1">A user.</param>
        /// <param name="User2">Another user.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (User? User1,
                                           User? User2)

            => !(User1 > User2);

        #endregion

        #region Operator >  (User1, User2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="User1">A user.</param>
        /// <param name="User2">Another user.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (User? User1,
                                          User? User2)
        {

            if (User1 is null)
                throw new ArgumentNullException(nameof(User1), "The given User1 must not be null!");

            return User1.CompareTo(User2) > 0;

        }

        #endregion

        #region Operator >= (User1, User2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="User1">A user.</param>
        /// <param name="User2">Another user.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (User? User1,
                                           User? User2)

            => !(User1 < User2);

        #endregion

        #endregion

        #region IComparable<User> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two users.
        /// </summary>
        /// <param name="Object">An user to compare with.</param>
        public override Int32 CompareTo(Object? Object)

            => Object is User user
                   ? CompareTo(user)
                   : throw new ArgumentException("The given object is not a user!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(User)

        /// <summary>
        /// Compares two users.
        /// </summary>
        /// <param name="User">An user to compare with.</param>
        public override Int32 CompareTo(User? User)

            => CompareTo(User as IUser);


        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="User">An user object to compare with.</param>
        public Int32 CompareTo(IUser? User)
        {

            if (User is null)
                throw new ArgumentNullException(nameof(User),
                                                "The given user must not be null!");

            return Id.CompareTo(User.Id);

            //ToDo: Compare more properties!

        }

        #endregion

        #endregion

        #region IEquatable<User> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two users for equality.
        /// </summary>
        /// <param name="Object">An user to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is User user &&
                  Equals(user);

        #endregion

        #region Equals(User)

        /// <summary>
        /// Compares two users for equality.
        /// </summary>
        /// <param name="User">An user to compare with.</param>
        public override Boolean Equals(User? User)

            => Equals(User as IUser);


        /// <summary>
        /// Compares two users for equality.
        /// </summary>
        /// <param name="User">An user to compare with.</param>
        public Boolean Equals(IUser? User)

            => User is not null &&
                   Id.Equals(User.Id);

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


        #region ToBuilder(NewUserId = null)

        /// <summary>
        /// Return a builder for this user.
        /// </summary>
        /// <param name="NewUserId">An optional new user identification.</param>
        public Builder ToBuilder(User_Id? NewUserId = null)

            => new (NewUserId ?? Id.Clone,
                    EMail.Address,
                    Name,
                    Description,
                    PublicKeyRing,
                    SecretKeyRing,
                    UserLanguage,
                    Telephone,
                    MobilePhone,
                    Use2AuthFactor,
                    Telegram,
                    Homepage,
                    GeoLocation,
                    Address,
                    AcceptedEULA,
                    IsDisabled,
                    IsAuthenticated,

                    notificationStore,

                    _User2User_Edges,
                    _User2UserGroup_Edges,
                    _User2Organization_Edges,

                    CustomData,
                    AttachedFiles,
                    JSONLDContext,
                    DataSource,
                    LastChangeDate);

        #endregion

        #region (class) Builder

        /// <summary>
        /// An user builder.
        /// </summary>
        public new class Builder : AEntity<User_Id, User>.Builder
        {

            #region Properties

            /// <summary>
            /// The primary E-Mail address of the user.
            /// </summary>
            [Mandatory]
            public EMailAddress           EMail                { get; set; }

            /// <summary>
            /// The PGP/GPG public keyring of the user.
            /// </summary>
            [Optional]
            public PgpPublicKeyRing?      PublicKeyRing        { get; set; }

            /// <summary>
            /// The PGP/GPG secret keyring of the user.
            /// </summary>
            [Optional]
            public PgpSecretKeyRing?      SecretKeyRing        { get; set; }

            /// <summary>
            /// The language setting of the user.
            /// </summary>
            [Mandatory]
            public Languages              UserLanguage         { get; set; }

            /// <summary>
            /// An optional telephone number of the user.
            /// </summary>
            [Optional]
            public PhoneNumber?           Telephone            { get; set; }

            /// <summary>
            /// An optional mobile telephone number of the user.
            /// </summary>
            [Optional]
            public PhoneNumber?           MobilePhone          { get; set; }

            /// <summary>
            /// Whether to use a second authentication factor.
            /// </summary>
            public Use2AuthFactor?        Use2AuthFactor       { get; set; }

            /// <summary>
            /// The telegram user name.
            /// </summary>
            [Optional]
            public String?                Telegram             { get; set; }

            /// <summary>
            /// An optional homepage of the user.
            /// </summary>
            [Optional]
            public String?                Homepage             { get; set; }

            /// <summary>
            /// The geographical location of this organization.
            /// </summary>
            public GeoCoordinate?         GeoLocation          { get; set; }

            /// <summary>
            /// The optional address of the organization.
            /// </summary>
            [Optional]
            public Address?               Address              { get; set; }

            /// <summary>
            /// Timestamp when the user accepted the End-User-License-Agreement.
            /// </summary>
            [Mandatory]
            public DateTime?              AcceptedEULA         { get; set; }

            /// <summary>
            /// The user is disabled.
            /// </summary>
            [Mandatory]
            public Boolean                IsDisabled           { get; set; }

            /// <summary>
            /// The user will not be shown in user listings, as its
            /// primary e-mail address is not yet authenticated.
            /// </summary>
            [Mandatory]
            public Boolean                IsAuthenticated      { get; set; }

            /// <summary>
            /// An enumeration of attached files.
            /// </summary>
            [Optional]
            public HashSet<AttachedFile>  AttachedFiles        { get; }

            #endregion

            #region Edges

            #region User <-> User         edges

            private readonly List<User2UserEdge> _User2UserEdges;

            public IEnumerable<User2UserEdge> __User2UserEdges
                => _User2UserEdges;


            public User2UserEdge

                Add(User2UserEdge Edge)

                    => _User2UserEdges.AddAndReturnElement(Edge);


            public IEnumerable<User2UserEdge>

                Add(IEnumerable<User2UserEdge> Edges)

                    => _User2UserEdges.AddAndReturnList(Edges);



            #region AddIncomingEdge(Edge)

            public User2UserEdge

                AddIncomingEdge(User2UserEdge  Edge)

                => _User2UserEdges.AddAndReturnElement(Edge);

            #endregion

            #region AddIncomingEdge(SourceUser, EdgeLabel, PrivacyLevel = PrivacyLevel.World)

            public User2UserEdge

                AddIncomingEdge(IUser               SourceUser,
                                User2UserEdgeTypes  EdgeLabel,
                                PrivacyLevel        PrivacyLevel = PrivacyLevel.World)

                => _User2UserEdges.AddAndReturnElement(new User2UserEdge(SourceUser, EdgeLabel, this.ToImmutable, PrivacyLevel));

            #endregion

            #region AddOutgoingEdge(Edge)

            public User2UserEdge

                AddOutgoingEdge(User2UserEdge  Edge)

                => _User2UserEdges.AddAndReturnElement(Edge);

            #endregion

            #region AddOutgoingEdge(SourceUser, EdgeLabel, PrivacyLevel = PrivacyLevel.World)

            public User2UserEdge

                AddOutgoingEdge(User2UserEdgeTypes  EdgeLabel,
                                IUser               Target,
                                PrivacyLevel        PrivacyLevel = PrivacyLevel.World)

                => _User2UserEdges.AddAndReturnElement(new User2UserEdge(this.ToImmutable, EdgeLabel, Target, PrivacyLevel));

            #endregion

            #endregion

            #region User  -> Organization edges

            private readonly List<User2OrganizationEdge> _User2Organization_OutEdges;

            public IEnumerable<User2OrganizationEdge> User2Organization_OutEdges
                => _User2Organization_OutEdges;


            public User2OrganizationEdge

                Add(User2OrganizationEdge Edge)

                    => _User2Organization_OutEdges.AddAndReturnElement(Edge);


            public IEnumerable<User2OrganizationEdge>

                Add(IEnumerable<User2OrganizationEdge> Edges)

                    => _User2Organization_OutEdges.AddAndReturnList(Edges);




            public User2OrganizationEdge

                AddOutgoingEdge(User2OrganizationEdgeLabel  EdgeLabel,
                                IOrganization               Target,
                                PrivacyLevel                PrivacyLevel = PrivacyLevel.World)

                => _User2Organization_OutEdges.AddAndReturnElement(new User2OrganizationEdge(this.ToImmutable, EdgeLabel, Target, PrivacyLevel));



            public User2UserGroupEdge

                AddOutgoingEdge(User2UserGroupEdgeLabel  EdgeLabel,
                                UserGroup                Target,
                                PrivacyLevel             PrivacyLevel = PrivacyLevel.World)

                => _User2Group_OutEdges.AddAndReturnElement(new User2UserGroupEdge(this.ToImmutable, EdgeLabel, Target, PrivacyLevel));


            public IEnumerable<User2UserGroupEdge> User2GroupOutEdges(Func<User2UserGroupEdgeLabel, Boolean> User2GroupEdgeFilter)
                => _User2Group_OutEdges.Where(edge => User2GroupEdgeFilter(edge.EdgeLabel));


            #region Organizations(RequireAdminAccess, RequireReadWriteAccess, Recursive)

            public IEnumerable<IOrganization> Organizations(Access_Levels  AccessLevel,
                                                            Boolean        Recursive)
            {

                var AllMyOrganizations = new HashSet<IOrganization>();

                switch (AccessLevel)
                {

                    case Access_Levels.Admin:
                        foreach (var organization in _User2Organization_OutEdges.
                                                         Where (edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdmin).
                                                         Select(edge => edge.Target))
                        {
                            AllMyOrganizations.Add(organization);
                        }
                        break;

                    case Access_Levels.ReadWrite:
                        foreach (var organization in _User2Organization_OutEdges.
                                                         Where (edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdmin ||
                                                                        edge.EdgeLabel == User2OrganizationEdgeLabel.IsMember).
                                                         Select(edge => edge.Target))
                        {
                            AllMyOrganizations.Add(organization);
                        }
                        break;

                    default:
                        foreach (var organization in _User2Organization_OutEdges.
                                                         Where (edge => edge.EdgeLabel == User2OrganizationEdgeLabel.IsAdmin  ||
                                                                        edge.EdgeLabel == User2OrganizationEdgeLabel.IsMember ||
                                                                        edge.EdgeLabel == User2OrganizationEdgeLabel.IsGuest).
                                                         Select(edge => edge.Target))
                        {
                            AllMyOrganizations.Add(organization);
                        }
                        break;

                }


                if (Recursive)
                {

                    IOrganization[]? Level2 = null;

                    do
                    {

                        Level2 = AllMyOrganizations.SelectMany(organization => organization.
                                                                                   Organization2OrganizationInEdges.
                                                                                   Where(edge => edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf)).
                                                    Select    (edge         => edge.Source).
                                                    Where     (organization => !AllMyOrganizations.Contains(organization)).
                                                    ToArray();

                        foreach (var organization in Level2)
                            AllMyOrganizations.Add(organization);

                    } while (Level2.Length > 0);

                }

                return AllMyOrganizations;

            }

            #endregion

            public Boolean RemoveOutEdge(User2OrganizationEdge Edge)
                => _User2Organization_OutEdges.Remove(Edge);

            #endregion

            #region User  -> Group        edges

            private readonly List<User2UserGroupEdge> _User2Group_OutEdges;

            public IEnumerable<User2UserGroupEdge> User2Group_OutEdges
                => _User2Group_OutEdges;



            public User2UserGroupEdge

                Add(User2UserGroupEdge Edge)

                    => _User2Group_OutEdges.AddAndReturnElement(Edge);


            public IEnumerable<User2UserGroupEdge>

                Add(IEnumerable<User2UserGroupEdge> Edges)

                    => _User2Group_OutEdges.AddAndReturnList(Edges);



            #region Groups(RequireReadWriteAccess = false, Recursive = false)

            public IEnumerable<IUserGroup> Groups(Boolean  RequireReadWriteAccess   = false,
                                                  Boolean  Recursive                = false)
            {

                var _Groups = RequireReadWriteAccess

                                         ? _User2Group_OutEdges.
                                               Where (edge => edge.EdgeLabel == User2UserGroupEdgeLabel.IsAdmin ||
                                                              edge.EdgeLabel == User2UserGroupEdgeLabel.IsMember).
                                               Select(edge => edge.Target).
                                               ToList()

                                         : _User2Group_OutEdges.
                                               Where (edge => edge.EdgeLabel == User2UserGroupEdgeLabel.IsAdmin  ||
                                                              edge.EdgeLabel == User2UserGroupEdgeLabel.IsMember ||
                                                              edge.EdgeLabel == User2UserGroupEdgeLabel.IsGuest).
                                               Select(edge => edge.Target).
                                               ToList();

                //if (Recursive)
                //{

                //    Group[] Level2 = null;

                //    do
                //    {

                //        Level2 = _Groups.SelectMany(group => group.
                //                                                 Group2GroupInEdges.
                //                                                 Where(edge => edge.EdgeLabel == Group2GroupEdges.IsChildOf)).
                //                         Select    (edge  => edge.Target).
                //                         Where     (group => !_Groups.Contains(group)).
                //                         ToArray();

                //        foreach (var organization in Level2)
                //            _Groups.Add(organization);

                //    } while (Level2.Length > 0);

                //}

                return new HashSet<IUserGroup>(_Groups);

            }

            #endregion

            public Boolean RemoveOutEdge(User2UserGroupEdge Edge)
                => _User2Group_OutEdges.Remove(Edge);

            #endregion


            #region Genimi

            /// <summary>
            /// The gemini of this user.
            /// </summary>
            public IEnumerable<IUser> Genimi
            {
                get
                {
                    return _User2UserEdges.
                               Where (edge => edge.EdgeLabel == User2UserEdgeTypes.gemini).
                               Select(edge => edge.Target);
                }
            }

            #endregion

            #region FollowsUsers

            /// <summary>
            /// This user follows this other users.
            /// </summary>
            public IEnumerable<IUser> FollowsUsers
            {
                get
                {
                    return _User2UserEdges.
                               Where(edge => edge.EdgeLabel == User2UserEdgeTypes.follows).
                               Select(edge => edge.Target);
                }
            }

            #endregion

            #region IsFollowedBy

            /// <summary>
            /// This user is followed by this other users.
            /// </summary>
            public IEnumerable<IUser> IsFollowedBy
            {
                get
                {
                    return _User2UserEdges.
                               Where(edge => edge.EdgeLabel == User2UserEdgeTypes.IsFollowedBy).
                               Select(edge => edge.Target);
                }
            }

            #endregion

            #region Groups()

            /// <summary>
            /// All groups this user belongs to.
            /// </summary>
            public IEnumerable<IUserGroup> Groups()
                => _User2Group_OutEdges.
                       Select(edge => edge.Target);

            #endregion

            #region Groups(EdgeFilter)

            /// <summary>
            /// All groups this user belongs to,
            /// filtered by the given edge label.
            /// </summary>
            public IEnumerable<IUserGroup> Groups(User2UserGroupEdgeLabel EdgeFilter)
                => _User2Group_OutEdges.
                       Where (edge => edge.EdgeLabel == EdgeFilter).
                       Select(edge => edge.Target);

            #endregion

            #region Edges(Group)

            /// <summary>
            /// All groups this user belongs to,
            /// filtered by the given edge label.
            /// </summary>
            public IEnumerable<User2UserGroupEdgeLabel> OutEdges(UserGroup Group)
                => _User2Group_OutEdges.
                       Where (edge => edge.Target == Group).
                       Select(edge => edge.EdgeLabel);

            #endregion

            #region Edges(Organization)

            /// <summary>
            /// All organizations this user belongs to,
            /// filtered by the given edge label.
            /// </summary>
            public IEnumerable<User2OrganizationEdgeLabel> Edges(Organization Organization)
                => _User2Organization_OutEdges.
                       Where (edge => edge.Target == Organization).
                       Select(edge => edge.EdgeLabel);

            #endregion

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new user builder.
            /// </summary>
            /// <param name="Id">The unique identification of the user.</param>
            /// <param name="EMail">The primary e-mail of the user.</param>
            /// <param name="Name">An offical (multi-language) name of the user.</param>
            /// <param name="Description">An optional (multi-language) description of the user.</param>
            /// <param name="PublicKeyRing">An optional PGP/GPG public keyring of the user.</param>
            /// <param name="SecretKeyRing">An optional PGP/GPG secret keyring of the user.</param>
            /// <param name="UserLanguage">The language setting of the user.</param>
            /// <param name="Telephone">An optional telephone number of the user.</param>
            /// <param name="MobilePhone">An optional telephone number of the user.</param>
            /// <param name="Use2AuthFactor">Whether to use a second authentication factor.</param>
            /// <param name="Telegram">An optional telegram account name of the user.</param>
            /// <param name="Homepage">The homepage of the user.</param>
            /// <param name="GeoLocation">An optional geographical location of the user.</param>
            /// <param name="Address">An optional address of the user.</param>
            /// <param name="AcceptedEULA">Timestamp when the user accepted the End-User-License-Agreement.</param>
            /// <param name="IsDisabled">The user is disabled.</param>
            /// <param name="IsAuthenticated">The user will not be shown in user listings, as its primary e-mail address is not yet authenticated.</param>
            /// 
            /// <param name="CustomData">Custom data to be stored with this user.</param>
            /// <param name="AttachedFiles">Optional files attached to this user.</param>
            /// <param name="JSONLDContext">The JSON-LD context of this user.</param>
            /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
            /// <param name="LastChange">The timestamp of the last changes within this user. Can e.g. be used as a HTTP ETag.</param>
            public Builder(User_Id                              Id,
                           SimpleEMailAddress                   EMail,
                           I18NString?                          Name                     = null,
                           I18NString?                          Description              = null,
                           PgpPublicKeyRing?                    PublicKeyRing            = null,
                           PgpSecretKeyRing?                    SecretKeyRing            = null,
                           Languages                            UserLanguage             = Languages.en,
                           PhoneNumber?                         Telephone                = null,
                           PhoneNumber?                         MobilePhone              = null,
                           Use2AuthFactor?                      Use2AuthFactor           = null,
                           String?                              Telegram                 = null,
                           String?                              Homepage                 = null,
                           GeoCoordinate?                       GeoLocation              = null,
                           Address?                             Address                  = null,
                           DateTime?                            AcceptedEULA             = null,
                           Boolean                              IsDisabled               = false,
                           Boolean                              IsAuthenticated          = false,

                           IEnumerable<ANotification>?          Notifications            = null,

                           IEnumerable<User2UserEdge>?          User2UserEdges           = null,
                           IEnumerable<User2UserGroupEdge>?     User2GroupEdges          = null,
                           IEnumerable<User2OrganizationEdge>?  User2OrganizationEdges   = null,

                           JObject?                             CustomData               = default,
                           IEnumerable<AttachedFile>?           AttachedFiles            = default,
                           JSONLDContext?                       JSONLDContext            = default,
                           String?                              DataSource               = default,
                           DateTime?                            LastChange               = default)

                : base(Id,
                       JSONLDContext ?? DefaultJSONLDContext,
                       LastChange,
                       null,
                       CustomData,
                       null,
                       DataSource)

            {

                this.EMail                        = Name is not null && Name.IsNotNullOrEmpty()
                                                        ? new EMailAddress(Name.FirstText(), EMail, null, null)
                                                        : new EMailAddress(                  EMail, null, null);
                this.Name                         = Name;
                this.Description                  = Description ?? new I18NString();
                this.PublicKeyRing                = PublicKeyRing;
                this.SecretKeyRing                = SecretKeyRing;
                this.UserLanguage                 = UserLanguage;
                this.Telephone                    = Telephone;
                this.MobilePhone                  = MobilePhone;
                this.Use2AuthFactor               = Use2AuthFactor;
                this.Telegram                     = Telegram;
                this.Homepage                     = Homepage;
                this.GeoLocation                  = GeoLocation;
                this.Address                      = Address;
                this.AcceptedEULA                 = AcceptedEULA;
                this.IsDisabled                   = IsDisabled;
                this.IsAuthenticated              = IsAuthenticated;
                this.AttachedFiles                = AttachedFiles is not null && AttachedFiles.Any()
                                                        ? new HashSet<AttachedFile>(AttachedFiles)
                                                        : new HashSet<AttachedFile>();

                this.notificationStore            = new NotificationStore(Notifications);

                this._User2UserEdges              = User2UserEdges         is not null && User2UserEdges.        IsNeitherNullNorEmpty() ? new List<User2UserEdge>        (User2UserEdges)         : new List<User2UserEdge>();
                this._User2Group_OutEdges         = User2GroupEdges        is not null && User2GroupEdges.       IsNeitherNullNorEmpty() ? new List<User2UserGroupEdge>   (User2GroupEdges)        : new List<User2UserGroupEdge>();
                this._User2Organization_OutEdges  = User2OrganizationEdges is not null && User2OrganizationEdges.IsNeitherNullNorEmpty() ? new List<User2OrganizationEdge>(User2OrganizationEdges) : new List<User2OrganizationEdge>();

            }

            #endregion


            #region Notifications

            private readonly NotificationStore notificationStore;

            #region (internal) AddNotification(Notification,                           OnUpdate = null)

            internal T AddNotification<T>(T          Notification,
                                          Action<T>  OnUpdate  = null)

                where T : ANotification

                => notificationStore.Add(Notification,
                                      OnUpdate);

            #endregion

            #region (internal) AddNotification(Notification, NotificationMessageType,  OnUpdate = null)

            internal T AddNotification<T>(T                        Notification,
                                          NotificationMessageType  NotificationMessageType,
                                          Action<T>                OnUpdate  = null)

                where T : ANotification

                => notificationStore.Add(Notification,
                                      NotificationMessageType,
                                      OnUpdate);

            #endregion

            #region (internal) AddNotification(Notification, NotificationMessageTypes, OnUpdate = null)

            internal T AddNotification<T>(T                                     Notification,
                                          IEnumerable<NotificationMessageType>  NotificationMessageTypes,
                                          Action<T>                             OnUpdate  = null)

                where T : ANotification

                => notificationStore.Add(Notification,
                                      NotificationMessageTypes,
                                      OnUpdate);

            #endregion


            #region GetNotifications  (NotificationMessageType = null)

            public IEnumerable<ANotification> GetNotifications(NotificationMessageType?  NotificationMessageType = null)
            {
                lock (notificationStore)
                {
                    return notificationStore.GetNotifications(NotificationMessageType);
                }
            }

            #endregion

            #region GetNotificationsOf(params NotificationMessageTypes)

            public IEnumerable<T> GetNotificationsOf<T>(params NotificationMessageType[] NotificationMessageTypes)

                where T : ANotification

            {

                lock (notificationStore)
                {
                    return notificationStore.GetNotificationsOf<T>(NotificationMessageTypes);
                }

            }

            #endregion

            #region GetNotifications  (NotificationMessageTypeFilter)

            public IEnumerable<ANotification> GetNotifications(Func<NotificationMessageType, Boolean> NotificationMessageTypeFilter)
            {
                lock (notificationStore)
                {
                    return notificationStore.GetNotifications(NotificationMessageTypeFilter);
                }
            }

            #endregion

            #region GetNotificationsOf(NotificationMessageTypeFilter)

            public IEnumerable<T> GetNotificationsOf<T>(Func<NotificationMessageType, Boolean> NotificationMessageTypeFilter)

                where T : ANotification

            {

                lock (notificationStore)
                {
                    return notificationStore.GetNotificationsOf<T>(NotificationMessageTypeFilter);
                }

            }

            #endregion


            #region GetNotificationInfo(NotificationId)

            public JObject GetNotificationInfo(UInt32 NotificationId)
            {

                var notification = notificationStore.ToJSON(NotificationId);

                notification.Add(new JProperty("user", JSONObject.Create(

                                         new JProperty("name",  EMail.OwnerName),
                                         new JProperty("email", EMail.Address.ToString()),

                                         MobilePhone.HasValue
                                             ? new JProperty("phoneNumber", MobilePhone.Value.ToString())
                                             : null

                                     )));

                return notification;

            }

            #endregion

            #region GetNotificationInfos()

            public JObject GetNotificationInfos()

                => JSONObject.Create(new JProperty("user", JSONObject.Create(

                                         new JProperty("name",               EMail.OwnerName),
                                         new JProperty("email",              EMail.Address.ToString()),

                                         MobilePhone.HasValue
                                             ? new JProperty("phoneNumber",  MobilePhone.Value.ToString())
                                             : null

                                     )),
                                     new JProperty("notifications",  notificationStore.ToJSON()));

            #endregion


            #region (internal) RemoveNotification(NotificationType,                           OnRemoval = null)

            internal Task RemoveNotification<T>(T          NotificationType,
                                                Action<T>  OnRemoval  = null)

                where T : ANotification

                => notificationStore.Remove(NotificationType,
                                             OnRemoval);

            #endregion

            #endregion


            #region CopyAllLinkedDataFrom(OldUser)

            public void CopyAllLinkedDataFrom(IUser OldUser)
                => CopyAllLinkedDataFromBase(OldUser as User);

            public override void CopyAllLinkedDataFromBase(User OldUser)
            {

                if (OldUser.__User2UserEdges.Any() && !__User2UserEdges.Any())
                {

                    Add(OldUser.__User2UserEdges);

                    foreach (var edge in __User2UserEdges)
                        edge.Source = this.ToImmutable;

                }

                if (OldUser.User2Organization_OutEdges.Any() && !User2Organization_OutEdges.Any())
                {

                    Add(OldUser.User2Organization_OutEdges);

                    foreach (var edge in User2Organization_OutEdges)
                        edge.Source = this.ToImmutable;

                }

                if (OldUser.User2Group_OutEdges.Any() && !User2Group_OutEdges.Any())
                {

                    Add(OldUser.User2Group_OutEdges);

                    foreach (var edge in User2Group_OutEdges)
                        edge.Source = this.ToImmutable;

                }

                if (OldUser.notificationStore.SafeAny() && !notificationStore.SafeAny())
                    notificationStore.Add(OldUser.notificationStore);

            }

            #endregion


            #region ToImmutable

            /// <summary>
            /// Return an immutable version of the user.
            /// </summary>
            /// <param name="Builder">A user builder.</param>
            public static implicit operator User(Builder Builder)

                => Builder?.ToImmutable;


            /// <summary>
            /// Return an immutable version of the user.
            /// </summary>
            public User ToImmutable
            {
                get
                {

                    //if (!Branch.HasValue || Branch.Value.IsNullOrEmpty)
                    //    throw new ArgumentNullException(nameof(Branch), "The given branch must not be null or empty!");

                    return new User(Id,
                                    Name,
                                    EMail.Address,
                                    Description,
                                    PublicKeyRing,
                                    SecretKeyRing,
                                    UserLanguage,
                                    Telephone,
                                    MobilePhone,
                                    Use2AuthFactor ?? HTTP.Use2AuthFactor.None,
                                    Telegram,
                                    Homepage,
                                    GeoLocation,
                                    Address,
                                    AcceptedEULA,
                                    IsDisabled,
                                    IsAuthenticated,

                                    notificationStore,

                                    _User2UserEdges,
                                    _User2Group_OutEdges,
                                    _User2Organization_OutEdges,

                                    CustomData,
                                    AttachedFiles,
                                    JSONLDContext,
                                    DataSource,
                                    LastChangeDate);
                }
            }

            #endregion


            #region Operator overloading

            #region Operator == (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A user builder.</param>
            /// <param name="Builder2">Another user builder.</param>
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
            /// <param name="Builder1">A user builder.</param>
            /// <param name="Builder2">Another user builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator != (Builder? Builder1,
                                               Builder? Builder2)

                => !(Builder1 == Builder2);

            #endregion

            #region Operator <  (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A user builder.</param>
            /// <param name="Builder2">Another user builder.</param>
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
            /// <param name="Builder1">A user builder.</param>
            /// <param name="Builder2">Another user builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator <= (Builder? Builder1,
                                               Builder? Builder2)

                => !(Builder1 > Builder2);

            #endregion

            #region Operator >  (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A user builder.</param>
            /// <param name="Builder2">Another user builder.</param>
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
            /// <param name="Builder1">A user builder.</param>
            /// <param name="Builder2">Another user builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator >= (Builder? Builder1,
                                               Builder? Builder2)

                => !(Builder1 < Builder2);

            #endregion

            #endregion

            #region IComparable<Builder> Members

            #region CompareTo(Object)

            /// <summary>
            /// Compares two users.
            /// </summary>
            /// <param name="Object">An user to compare with.</param>
            public override Int32 CompareTo(Object? Object)

                => Object is Builder builder
                       ? CompareTo(builder)
                       : throw new ArgumentException("The given object is not an user!");

            #endregion

            #region CompareTo(User)

            /// <summary>
            /// Compares two users.
            /// </summary>
            /// <param name="User">An user to compare with.</param>
            public override Int32 CompareTo(User? User)

                => User is not null
                       ? Id.CompareTo(User.Id)
                       : throw new ArgumentNullException(nameof(User), "The given user must not be null!");


            /// <summary>
            /// Compares two users.
            /// </summary>
            /// <param name="Builder">An user to compare with.</param>
            public Int32 CompareTo(Builder Builder)

                => Builder is not null
                       ? Id.CompareTo(Builder.Id)
                       : throw new ArgumentNullException(nameof(Builder), "The given user must not be null!");

            #endregion

            #endregion

            #region IEquatable<Builder> Members

            #region Equals(Object)

            /// <summary>
            /// Compares two users for equality.
            /// </summary>
            /// <param name="Object">An user to compare with.</param>
            public override Boolean Equals(Object? Object)

                => Object is Builder builder &&
                      Equals(builder);

            #endregion

            #region Equals(User)

            /// <summary>
            /// Compares two users for equality.
            /// </summary>
            /// <param name="User">An user to compare with.</param>
            public override Boolean Equals(User? User)

                => User is not null &&
                       Id.Equals(User.Id);


            /// <summary>
            /// Compares two users for equality.
            /// </summary>
            /// <param name="Object">An user to compare with.</param>
            public Boolean Equals(Builder Builder)

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
