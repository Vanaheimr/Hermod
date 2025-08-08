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

using System;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate Boolean UserGroupProviderDelegate(UserGroup_Id UserGroupId, out IUserGroup UserGroup);

    public delegate JObject UserGroupToJSONDelegate(IUserGroup  UserGroup,
                                                    Boolean     Embedded                        = false,
                                                    InfoStatus  ExpandUsers                     = InfoStatus.ShowIdOnly,
                                                    InfoStatus  ExpandParentGroup               = InfoStatus.ShowIdOnly,
                                                    InfoStatus  ExpandSubgroups                 = InfoStatus.ShowIdOnly,
                                                    InfoStatus  ExpandAttachedFiles             = InfoStatus.ShowIdOnly,
                                                    InfoStatus  IncludeAttachedFileSignatures   = InfoStatus.ShowIdOnly);


    /// <summary>
    /// Extension methods for the user groups.
    /// </summary>
    public static partial class UserGroupExtensions
    {

        #region ToJSON(this UserGroups, Skip = null, Take = null, Embedded = false, ...)

        /// <summary>
        /// Return a JSON representation for the given enumeration of user groups.
        /// </summary>
        /// <param name="UserGroups">An enumeration of user groups.</param>
        /// <param name="Skip">The optional number of user groups to skip.</param>
        /// <param name="Take">The optional number of user groups to return.</param>
        /// <param name="Embedded">Whether this data is embedded into another data structure, e.g. into a user group.</param>
        public static JArray ToJSON(this IEnumerable<IUserGroup>  UserGroups,
                                    UInt64?                       Skip                            = null,
                                    UInt64?                       Take                            = null,
                                    Boolean                       Embedded                        = false,
                                    InfoStatus                    ExpandUsers                     = InfoStatus.ShowIdOnly,
                                    InfoStatus                    ExpandParentGroup               = InfoStatus.ShowIdOnly,
                                    InfoStatus                    ExpandSubgroups                 = InfoStatus.ShowIdOnly,
                                    InfoStatus                    ExpandAttachedFiles             = InfoStatus.ShowIdOnly,
                                    InfoStatus                    IncludeAttachedFileSignatures   = InfoStatus.ShowIdOnly,
                                    UserGroupToJSONDelegate?      UserGroupToJSON                 = null)


            => UserGroups?.Any() != true

                   ? new JArray()

                   : new JArray(UserGroups.
                                    OrderBy       (userGroup => userGroup.Id).
                                    SkipTakeFilter(Skip, Take).
                                    SafeSelect    (userGroup => UserGroupToJSON != null
                                                                            ? UserGroupToJSON (userGroup,
                                                                                                       Embedded,
                                                                                                       ExpandUsers,
                                                                                                       ExpandParentGroup,
                                                                                                       ExpandSubgroups,
                                                                                                       ExpandAttachedFiles,
                                                                                                       IncludeAttachedFileSignatures)

                                                                            : userGroup.ToJSON(Embedded,
                                                                                                       ExpandUsers,
                                                                                                       ExpandParentGroup,
                                                                                                       ExpandSubgroups,
                                                                                                       ExpandAttachedFiles,
                                                                                                       IncludeAttachedFileSignatures)));

        #endregion

    }


    /// <summary>
    /// A user group.
    /// </summary>
    public class UserGroup : AGroup<UserGroup_Id,
                                    IUserGroup,
                                    User_Id,
                                    IUser>,
                             IUserGroup
    {

        #region Data

        /// <summary>
        /// The default JSON-LD context of user groups.
        /// </summary>
        public new readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/userGroup");

        #endregion

        #region User      -> UserGroup edges

        protected readonly List<User2UserGroupEdge> _User2UserGroup_Edges;

        public IEnumerable<User2UserGroupEdge> User2UserGroupEdges
            => _User2UserGroup_Edges;


        public User2UserGroupEdge AddUser(User2UserGroupEdgeLabel EdgeLabel,
                                          IUser Source,
                                          PrivacyLevel PrivacyLevel = PrivacyLevel.Private)

            => _User2UserGroup_Edges.
                   AddAndReturnElement(new User2UserGroupEdge(Source,
                                                              EdgeLabel,
                                                              this,
                                                              PrivacyLevel));


        public User2UserGroupEdge AddUser(User2UserGroupEdge Edge)

            => _User2UserGroup_Edges.
                   AddAndReturnElement(Edge);

        public UserGroup AddUsers(IEnumerable<User2UserGroupEdge> Edges)
        {

            foreach (var edge in Edges)
                _User2UserGroup_Edges.Add(edge);

            return this;

        }



        public IEnumerable<User2UserGroupEdge> User2GroupInEdges(Func<User2UserGroupEdgeLabel, Boolean>? User2GroupEdgeFilter = null)
            => _User2UserGroup_Edges.
                   Where(edge => User2GroupEdgeFilter != null ? User2GroupEdgeFilter(edge.EdgeLabel) : true);


        /// <summary>
        /// All organizations this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        /// <param name="User">Just return edges with the given user.</param>
        public IEnumerable<User2UserGroupEdge> Edges(IUser User)

            => _User2UserGroup_Edges.
                   Where(edge => edge.Source == User);


        /// <summary>
        /// All organizations this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        /// <param name="User">Just return edges with the given user.</param>
        public IEnumerable<User2UserGroupEdge> Edges(User2UserGroupEdgeLabel EdgeLabel,
                                                     IUser User)

            => _User2UserGroup_Edges.
                   Where(edge => edge.Source == User && edge.EdgeLabel == EdgeLabel);


        /// <summary>
        /// All organizations this user belongs to,
        /// filtered by the given edge label.
        /// </summary>
        /// <param name="User">Just return edges with the given user.</param>
        public IEnumerable<User2UserGroupEdgeLabel> EdgeLabels(IUser User)

            => _User2UserGroup_Edges.
                   Where(edge => edge.Source == User).
                   Select(edge => edge.EdgeLabel);


        public Boolean HasEdge(User2UserGroupEdgeLabel EdgeLabel,
                               IUser User)

            => _User2UserGroup_Edges.
                   Any(edge => edge.EdgeLabel == EdgeLabel && edge.Source == User);

        #endregion

        #region UserGroup -> UserGroup edges

        protected readonly List<UserGroup2UserGroupEdge> _UserGroup2UserGroup_InEdges;
        protected readonly List<UserGroup2UserGroupEdge> _UserGroup2UserGroup_OutEdges;

        public IEnumerable<UserGroup2UserGroupEdge> UserGroup2UserGroupInEdges
            => _UserGroup2UserGroup_InEdges;

        public IEnumerable<UserGroup2UserGroupEdge> UserGroup2UserGroupOutEdges
            => _UserGroup2UserGroup_OutEdges;


        #region AddEdge (Edge)

        public UserGroup2UserGroupEdge AddEdge(UserGroup2UserGroupEdge Edge)

            => Edge.Target == this
                   ? _UserGroup2UserGroup_InEdges.AddAndReturnElement(Edge)
                   : _UserGroup2UserGroup_OutEdges.AddAndReturnElement(Edge);

        #endregion

        #region AddEdges(Edges)

        public IEnumerable<UserGroup2UserGroupEdge> AddEdges(IEnumerable<UserGroup2UserGroupEdge> Edges)
        {

            foreach (var edge in Edges)
                AddEdge(edge);

            return Edges;

        }

        #endregion

        #region AddInEdge (EdgeLabel, SourceUserGroup, PrivacyLevel = PrivacyLevel.World)

        public UserGroup2UserGroupEdge AddInEdge(UserGroup2UserGroupEdgeLabel EdgeLabel,
                                                 UserGroup SourceUserGroup,
                                                 PrivacyLevel PrivacyLevel = PrivacyLevel.World)

            => _UserGroup2UserGroup_InEdges.AddAndReturnElement(new UserGroup2UserGroupEdge(SourceUserGroup,
                                                                                            EdgeLabel,
                                                                                            this,
                                                                                            PrivacyLevel));

        #endregion

        #region AddOutEdge(EdgeLabel, TargetUserGroup, PrivacyLevel = PrivacyLevel.World)

        public UserGroup2UserGroupEdge AddOutEdge(UserGroup2UserGroupEdgeLabel EdgeLabel,
                                                  UserGroup TargetUserGroup,
                                                  PrivacyLevel PrivacyLevel = PrivacyLevel.World)

            => _UserGroup2UserGroup_OutEdges.AddAndReturnElement(new UserGroup2UserGroupEdge(this,
                                                                                             EdgeLabel,
                                                                                             TargetUserGroup,
                                                                                             PrivacyLevel));

        #endregion


        #region (private) _GetAllParents(ref Parents)

        private void _GetAllParents(ref HashSet<UserGroup> Parents)
        {

            var parents = _UserGroup2UserGroup_OutEdges.
                              Where(edge => edge.Source == this && edge.EdgeLabel == UserGroup2UserGroupEdgeLabel.IsSubgroupOf).
                              Select(edge => edge.Target).
                              ToArray();

            foreach (var parent in parents)
            {
                // Detect loops!
                if (Parents.Add(parent))
                    parent._GetAllParents(ref Parents);
            }

        }

        #endregion

        #region GetAllParents(Filter = null)

        public IEnumerable<UserGroup> GetAllParents(Func<UserGroup, Boolean> Include = null)
        {

            var parents = new HashSet<UserGroup>();
            _GetAllParents(ref parents);

            return Include != null
                       ? parents.Where(Include)
                       : parents;

        }

        #endregion

        #region GetMeAndAllMyParents(Filter = null)

        public IEnumerable<UserGroup> GetMeAndAllMyParents(Func<UserGroup, Boolean> Include = null)
        {

            var parentsAndMe = new HashSet<UserGroup>();
            parentsAndMe.Add(this);
            _GetAllParents(ref parentsAndMe);

            return Include != null
                       ? parentsAndMe.Where(Include)
                       : parentsAndMe;

        }

        #endregion

        #region ParentUserGroups

        public IEnumerable<UserGroup> ParentUserGroups

            => _UserGroup2UserGroup_OutEdges.
                   Where(edge => edge.Source == this && edge.EdgeLabel == UserGroup2UserGroupEdgeLabel.IsSubgroupOf).
                   Select(edge => edge.Target).
                   ToArray();

        #endregion


        #region (private) _GetAllChilds(ref Childs)

        private void _GetAllChilds(ref HashSet<UserGroup> Childs)
        {

            var childs = _UserGroup2UserGroup_InEdges.
                             Where(edge => edge.Target == this && edge.EdgeLabel == UserGroup2UserGroupEdgeLabel.IsSubgroupOf).
                             Select(edge => edge.Source).
                             ToArray();

            foreach (var child in childs)
            {
                // Detect loops!
                if (Childs.Add(child))
                    child._GetAllChilds(ref Childs);
            }

        }

        #endregion

        #region GetAllChilds(Filter = null)

        public IEnumerable<UserGroup> GetAllChilds(Func<UserGroup, Boolean> Include = null)
        {

            var childs = new HashSet<UserGroup>();
            _GetAllChilds(ref childs);

            return Include != null
                       ? childs.Where(Include)
                       : childs;

        }

        #endregion

        #region GetMeAndAllMyChilds(Filter = null)

        public IEnumerable<UserGroup> GetMeAndAllMyChilds(Func<UserGroup, Boolean> Include = null)
        {

            var childAndMe = new HashSet<UserGroup>();
            childAndMe.Add(this);
            _GetAllChilds(ref childAndMe);

            return Include != null
                       ? childAndMe.Where(Include)
                       : childAndMe;

        }

        #endregion

        #region SubUserGroups

        /// <summary>
        /// A relationship between two organizations where the first includes the second, e.g., as a subsidiary. See also: the more specific 'department' property.
        /// </summary>
        public IEnumerable<UserGroup> SubUserGroups

            => _UserGroup2UserGroup_InEdges.
                   Where(edge => edge.Target == this && edge.EdgeLabel == UserGroup2UserGroupEdgeLabel.IsSubgroupOf).
                   Select(edge => edge.Source).
                   ToArray();

        #endregion


        #region EdgeLabels(UserGroup UserGroup)

        /// <summary>
        /// All edge labels between this and the given user group.
        /// </summary>
        public IEnumerable<UserGroup2UserGroupEdgeLabel> EdgeLabels(UserGroup UserGroup)

            => UserGroup is null

                   ? new UserGroup2UserGroupEdgeLabel[0]

                   : _UserGroup2UserGroup_InEdges.
                         Where(edge => edge.Source == UserGroup).
                         Select(edge => edge.EdgeLabel).Concat(

                     _UserGroup2UserGroup_OutEdges.
                         Where(edge => edge.Target == UserGroup).
                         Select(edge => edge.EdgeLabel));

        #endregion


        #region RemoveInEdge  (EdgeLabel)

        public Boolean RemoveInEdge(UserGroup2UserGroupEdge Edge)

            => _UserGroup2UserGroup_InEdges.Remove(Edge);

        #endregion

        #region RemoveInEdges (EdgeLabel, SourceUserGroup)

        public void RemoveInEdges(UserGroup2UserGroupEdgeLabel EdgeLabel,
                                  UserGroup SourceUserGroup)
        {

            var edges = _UserGroup2UserGroup_InEdges.
                            Where(edge => edge.EdgeLabel == EdgeLabel &&
                                          edge.Source == SourceUserGroup).
                            ToArray();

            foreach (var edge in edges)
                _UserGroup2UserGroup_InEdges.Remove(edge);

        }

        #endregion

        #region RemoveOutEdges(EdgeLabel, TargetUserGroup)

        public Boolean RemoveOutEdge(UserGroup2UserGroupEdge Edge)

            => _UserGroup2UserGroup_OutEdges.Remove(Edge);

        #endregion

        #region RemoveOutEdges(EdgeLabel, TargetUserGroup)

        public void RemoveOutEdges(UserGroup2UserGroupEdgeLabel EdgeLabel,
                                   UserGroup TargetUserGroup)
        {

            var edges = _UserGroup2UserGroup_OutEdges.
                            Where(edge => edge.EdgeLabel == EdgeLabel &&
                                          edge.Target == TargetUserGroup).
                            ToArray();

            foreach (var edge in edges)
                _UserGroup2UserGroup_OutEdges.Remove(edge);

        }

        #endregion

        #endregion

        HTTPExtAPI? IUserGroup.API { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        #region Constructor(s)

        /// <summary>
        /// Create a new user group.
        /// </summary>
        /// <param name="Id">The unique identification of the user group.</param>
        /// 
        /// <param name="Name">A multi-language name of the user group.</param>
        /// <param name="Description">A multi-language description of the user group.</param>
        /// <param name="Users">An enumeration of users.</param>
        /// <param name="ParentGroup">An optional parent user group.</param>
        /// <param name="Subgroups">Optional user subgroups.</param>
        /// 
        /// <param name="CustomData">Custom data to be stored with this user group.</param>
        /// <param name="AttachedFiles">Optional files attached to this user group.</param>
        /// <param name="JSONLDContext">The JSON-LD context of this user group.</param>
        /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
        /// <param name="LastChange">The timestamp of the last changes within this user group. Can e.g. be used as a HTTP ETag.</param>
        public UserGroup(UserGroup_Id Id,

                         I18NString                             Name,
                         I18NString?                            Description                   = null,
                         IEnumerable<IUser>?                    Users                         = null,
                         IUserGroup?                            ParentGroup                   = null,
                         IEnumerable<IUserGroup>?               Subgroups                     = null,

                         IEnumerable<User2UserGroupEdge>?       User2GroupInEdges             = null,
                         IEnumerable<UserGroup2UserGroupEdge>?  UserGroup2UserGroupInEdges    = null,
                         IEnumerable<UserGroup2UserGroupEdge>?  UserGroup2UserGroupOutEdges   = null,

                         JObject?                               CustomData                    = default,
                         IEnumerable<AttachedFile>?             AttachedFiles                 = default,
                         JSONLDContext?                         JSONLDContext                 = default,
                         String?                                DataSource                    = default,
                         DateTimeOffset?                        LastChange                    = default)

            : base(Id,

                   Name,
                   Description,
                   Users,
                   ParentGroup,
                   Subgroups,

                   CustomData,
                   AttachedFiles,
                   JSONLDContext ?? DefaultJSONLDContext,
                   DataSource,
                   LastChange)

        {

            this._User2UserGroup_Edges          = User2GroupInEdges != null ? new List<User2UserGroupEdge>     (User2GroupInEdges)           : new List<User2UserGroupEdge>();
            this._UserGroup2UserGroup_InEdges   = User2GroupInEdges != null ? new List<UserGroup2UserGroupEdge>(UserGroup2UserGroupInEdges)  : new List<UserGroup2UserGroupEdge>();
            this._UserGroup2UserGroup_OutEdges  = User2GroupInEdges != null ? new List<UserGroup2UserGroupEdge>(UserGroup2UserGroupOutEdges) : new List<UserGroup2UserGroupEdge>();

        }

        #endregion


        #region ToJSON(...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        public override JObject ToJSON(Boolean Embedded = false)

            => ToJSON(Embedded: false,
                      ExpandUsers: InfoStatus.ShowIdOnly,
                      ExpandParentGroup: InfoStatus.ShowIdOnly,
                      ExpandSubgroups: InfoStatus.ShowIdOnly,
                      ExpandAttachedFiles: InfoStatus.ShowIdOnly,
                      IncludeAttachedFileSignatures: InfoStatus.ShowIdOnly);


        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data is embedded into another data structure, e.g. into a UserGroup.</param>
        /// <param name="IncludeCryptoHash">Whether to include the cryptograhical hash value of this object.</param>
        public virtual JObject ToJSON(Boolean Embedded = false,
                                      InfoStatus ExpandUsers = InfoStatus.ShowIdOnly,
                                      InfoStatus ExpandParentGroup = InfoStatus.ShowIdOnly,
                                      InfoStatus ExpandSubgroups = InfoStatus.ShowIdOnly,
                                      InfoStatus ExpandAttachedFiles = InfoStatus.ShowIdOnly,
                                      InfoStatus IncludeAttachedFileSignatures = InfoStatus.ShowIdOnly)
        {


            var JSON = base.ToJSON(Embedded,
                                   false, //IncludeLastChange,
                                   null,

                                   new JProperty("name", Name.ToJSON()),

                                   Description.IsNotNullOrEmpty()
                                       ? new JProperty("description", Description.ToJSON())
                                       : null,

                                   _User2UserGroup_Edges.Where(edge => edge.EdgeLabel == User2UserGroupEdgeLabel.IsMember).SafeAny()
                                       ? new JProperty("isMember", new JArray(_User2UserGroup_Edges.Where(edge => edge.EdgeLabel == User2UserGroupEdgeLabel.IsMember).Select(edge => edge.Source.Id.ToString())))
                                       : null,

                                   Members.SafeAny() && ExpandUsers != InfoStatus.Hidden
                                       ? ExpandSubgroups.Switch(
                                               () => new JProperty("memberIds", new JArray(Members.SafeSelect(user => user.Id.ToString()))),
                                               () => new JProperty("members", new JArray(Members.SafeSelect(user => user.ToJSON(Embedded: true)))))
                                       //ExpandParentGroup:  InfoStatus.Hidden,
                                       //ExpandSubgroups:    InfoStatus.Expand)))))
                                       : null,

                                   ParentGroup is not null && ExpandParentGroup != InfoStatus.Hidden
                                       ? ExpandParentGroup.Switch(
                                               () => new JProperty("parentGroupId", ParentGroup.Id.ToString()),
                                               () => new JProperty("parentGroup", ParentGroup.ToJSON(true)))
                                       : null,

                                   Subgroups.SafeAny() && ExpandSubgroups != InfoStatus.Hidden
                                       ? ExpandSubgroups.Switch(
                                               () => new JProperty("subgroupsIds", new JArray(Subgroups.SafeSelect(subgroup => subgroup.Id.ToString()))),
                                               () => new JProperty("subgroups", new JArray(Subgroups.SafeSelect(subgroup => subgroup.ToJSON(Embedded: true,
                                                                                                                                                    ExpandParentGroup: InfoStatus.Hidden,
                                                                                                                                                    ExpandSubgroups: InfoStatus.Expanded)))))
                                       : null

                       );



            return JSON;

        }

        #endregion

        #region (static) TryParseJSON(JSONObject, ..., out UserGroup, out ErrorResponse)

        /// <summary>
        /// Try to parse the given user group JSON.
        /// </summary>
        /// <param name="JSONObject">A JSON object.</param>
        /// <param name="UserGroupProvider">A delegate resolving user groups.</param>
        /// <param name="UserProvider">A delegate resolving users.</param>
        /// <param name="UserGroup">The parsed user group.</param>
        /// <param name="ErrorResponse">An error message.</param>
        /// <param name="UserGroupIdURL">An optional UserGroup identification, e.g. from the HTTP URL.</param>
        public static Boolean TryParseJSON(JObject                    JSONObject,
                                           UserGroupProviderDelegate  UserGroupProvider,
                                           UserProviderDelegate       UserProvider,
                                           out UserGroup?             UserGroup,
                                           out String?                ErrorResponse,
                                           UserGroup_Id?              UserGroupIdURL = null)
        {

            try
            {

                UserGroup = null;

                if (JSONObject?.HasValues != true)
                {
                    ErrorResponse = "The given JSON object must not be null or empty!";
                    return false;
                }

                #region Parse UserGroupId  [optional]

                // Verify that a given UserGroup identification
                //   is at least valid.
                if (JSONObject.ParseOptional("@id",
                                             "UserGroup identification",
                                             UserGroup_Id.TryParse,
                                             out UserGroup_Id? UserGroupIdBody,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                if (!UserGroupIdURL.HasValue && !UserGroupIdBody.HasValue)
                {
                    ErrorResponse = "The UserGroup identification is missing!";
                    return false;
                }

                if (UserGroupIdURL.HasValue && UserGroupIdBody.HasValue && UserGroupIdURL.Value != UserGroupIdBody.Value)
                {
                    ErrorResponse = "The optional UserGroup identification given within the JSON body does not match the one given in the URI!";
                    return false;
                }

                #endregion

                #region Parse Context                       [mandatory]

                if (!JSONObject.ParseMandatory("@context",
                                               "JSON-LD context",
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

                #region Parse Name                          [mandatory]

                if (!JSONObject.ParseMandatory("name",
                                               "name",
                                               out I18NString Name,
                                               out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Description                   [optional]

                if (JSONObject.ParseOptional("description",
                                             "description",
                                             out I18NString Description,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion


                #region Parse ParentGroup identification    [optional]

                if (JSONObject.ParseOptional("parentGroupId",
                                             "parentgroup identification",
                                             UserGroup_Id.TryParse,
                                             out UserGroup_Id? ParentGroupId,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                IUserGroup? ParentGroup = null;

                if (ParentGroupId.HasValue)
                    UserGroupProvider(ParentGroupId.Value, out ParentGroup);

                #endregion

                #region Parse Subgroup identifications      [optional]

                if (JSONObject.ParseOptional("SubgroupIds",
                                             "subgroup identifications",
                                             UserGroup_Id.TryParse,
                                             out IEnumerable<UserGroup_Id> SubgroupIds,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                List<IUserGroup>? Subgroups = null;

                if (SubgroupIds?.Any() == true)
                {

                    Subgroups = new List<IUserGroup>();

                    foreach (var userGroupId in SubgroupIds)
                    {
                        if (UserGroupProvider(userGroupId, out var userGroup))
                            Subgroups.Add(userGroup);
                    }

                }

                #endregion

                #region Parse User identifications          [optional]

                if (JSONObject.ParseOptional("userIds",
                                             "user identifications",
                                             User_Id.TryParse,
                                             out IEnumerable<User_Id> UserIds,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                List<IUser>? Users = null;

                if (UserIds?.Any() == true)
                {

                    Users = new List<IUser>();

                    foreach (var userId in UserIds)
                    {
                        if (UserProvider(userId, out var user))
                            Users.Add(user);
                    }

                }

                #endregion


                #region Get   DataSource       [optional]

                var DataSource = JSONObject.GetOptional("dataSource");

                #endregion


                #region Parse CryptoHash       [optional]

                var CryptoHash = JSONObject.GetOptional("cryptoHash");

                #endregion


                UserGroup = new UserGroup(

                                    UserGroupIdBody ?? UserGroupIdURL.Value,

                                    Name,
                                    Description,
                                    Users,
                                    ParentGroup,
                                    Subgroups,

                                    null,
                                    null,
                                    null,

                                    null,
                                    null,
                                    Context,
                                    DataSource,
                                    null

                                );

                ErrorResponse = null;
                return true;

            }
            catch (Exception e)
            {
                ErrorResponse  = e.Message;
                UserGroup      = null;
                return false;
            }

        }

        #endregion


        #region CopyAllLinkedDataFrom(OldGroup)

        public override void CopyAllLinkedDataFromBase(IUserGroup OldGroup)
        {

            if (OldGroup.User2UserGroupEdges.Any() && !User2UserGroupEdges.Any())
            {

                AddUsers(OldGroup.User2UserGroupEdges);

                foreach (var edge in User2UserGroupEdges)
                    edge.Target = this;

            }

        }

        #endregion


        #region Operator overloading

        #region Operator == (UserGroup1, UserGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroup1">A user group.</param>
        /// <param name="UserGroup2">Another user group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator ==(UserGroup UserGroup1,
                                           UserGroup UserGroup2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(UserGroup1, UserGroup2))
                return true;

            // If one is null, but not both, return false.
            if ((UserGroup1 is null) || (UserGroup2 is null))
                return false;

            return UserGroup1.Equals(UserGroup2);

        }

        #endregion

        #region Operator != (UserGroup1, UserGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroup1">A user group.</param>
        /// <param name="UserGroup2">Another user group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator !=(UserGroup UserGroup1,
                                           UserGroup UserGroup2)

            => !(UserGroup1 == UserGroup2);

        #endregion

        #region Operator <  (UserGroup1, UserGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroup1">A user group.</param>
        /// <param name="UserGroup2">Another user group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <(UserGroup UserGroup1,
                                          UserGroup UserGroup2)
        {

            if (UserGroup1 is null)
                throw new ArgumentNullException(nameof(UserGroup1), "The given user group must not be null!");

            return UserGroup1.CompareTo(UserGroup2) < 0;

        }

        #endregion

        #region Operator <= (UserGroup1, UserGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroup1">A user group.</param>
        /// <param name="UserGroup2">Another user group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <=(UserGroup UserGroup1,
                                           UserGroup UserGroup2)

            => !(UserGroup1 > UserGroup2);

        #endregion

        #region Operator >  (UserGroup1, UserGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroup1">A user group.</param>
        /// <param name="UserGroup2">Another user group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >(UserGroup UserGroup1,
                                          UserGroup UserGroup2)
        {

            if (UserGroup1 is null)
                throw new ArgumentNullException(nameof(UserGroup1), "The given user group must not be null!");

            return UserGroup1.CompareTo(UserGroup2) > 0;

        }

        #endregion

        #region Operator >= (UserGroup1, UserGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="UserGroup1">A user group.</param>
        /// <param name="UserGroup2">Another user group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >=(UserGroup UserGroup1,
                                           UserGroup UserGroup2)

            => !(UserGroup1 < UserGroup2);

        #endregion

        #endregion

        #region IComparable<UserGroup> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public override Int32 CompareTo(Object Object)
        {

            if (Object is UserGroup UserGroup)
                CompareTo(UserGroup);

            throw new ArgumentException("The given object is not a user group!");

        }

        #endregion

        #region CompareTo(UserGroup)

        /// <summary>
        /// Compares two user groups.
        /// </summary>
        /// <param name="UserGroup">A user group to compare with.</param>
        public override Int32 CompareTo(IUserGroup? UserGroup)
        {

            if (UserGroup is null)
                throw new ArgumentNullException(nameof(UserGroup), "The given user group must not be null!");

            return Id.CompareTo(UserGroup.Id);

        }

        #endregion

        #endregion

        #region IEquatable<UserGroup> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object is UserGroup UserGroup)
                return Equals(UserGroup);

            return false;

        }

        #endregion

        #region Equals(UserGroup)

        /// <summary>
        /// Compares two user groups for equality.
        /// </summary>
        /// <param name="UserGroup">A user group to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public override Boolean Equals(IUserGroup? UserGroup)
        {

            if (UserGroup is null)
                return false;

            return Id.Equals(UserGroup.Id);

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Get the hash code of this object.
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


        #region ToBuilder(NewUserGroupId = null)

        /// <summary>
        /// Return a builder for this user group.
        /// </summary>
        /// <param name="NewUserGroupId">An optional new user group identification.</param>
        public Builder ToBuilder(UserGroup_Id? NewUserGroupId = null)

            => new (NewUserGroupId ?? Id,

                    Description,
                    Name,
                    Members,
                    ParentGroup,
                    Subgroups,

                    _User2UserGroup_Edges,
                    _UserGroup2UserGroup_InEdges,
                    _UserGroup2UserGroup_OutEdges,

                    CustomData,
                    AttachedFiles,
                    JSONLDContext,
                    DataSource,
                    LastChangeDate);

        #endregion

        #region (class) Builder

        /// <summary>
        /// A user group builder.
        /// </summary>
        public new class Builder : AGroup<UserGroup_Id,
                                          IUserGroup,
                                          User_Id,
                                          IUser>.Builder
        {

            #region User      -> UserGroup edges

            protected readonly List<User2UserGroupEdge> _User2UserGroup_Edges;

            #endregion

            #region UserGroup -> UserGroup edges

            protected readonly List<UserGroup2UserGroupEdge> _UserGroup2UserGroup_InEdges;
            protected readonly List<UserGroup2UserGroupEdge> _UserGroup2UserGroup_OutEdges;

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new user group builder.
            /// </summary>
            /// <param name="Id">The unique identification of the user group.</param>
            /// 
            /// <param name="Name">A multi-language name of the user group.</param>
            /// <param name="Description">A multi-language description of the user group.</param>
            /// <param name="Users">An enumeration of users.</param>
            /// <param name="ParentGroup">An optional parent user group.</param>
            /// <param name="Subgroups">Optional user subgroups.</param>
            /// 
            /// <param name="CustomData">Custom data to be stored with this user group.</param>
            /// <param name="AttachedFiles">Optional files attached to this user group.</param>
            /// <param name="JSONLDContext">The JSON-LD context of this user group.</param>
            /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
            /// <param name="LastChange">The timestamp of the last changes within this user group. Can e.g. be used as a HTTP ETag.</param>
            public Builder(UserGroup_Id?                          Id                            = null,

                           I18NString?                            Name                          = null,
                           I18NString?                            Description                   = null,
                           IEnumerable<IUser>?                    Users                         = null,
                           IUserGroup?                            ParentGroup                   = null,
                           IEnumerable<IUserGroup>?               Subgroups                     = null,

                           IEnumerable<User2UserGroupEdge>?       User2UserGroupEdges           = null,
                           IEnumerable<UserGroup2UserGroupEdge>?  UserGroup2UserGroupInEdges    = null,
                           IEnumerable<UserGroup2UserGroupEdge>?  UserGroup2UserGroupOutEdges   = null,

                           JObject?                               CustomData                    = default,
                           IEnumerable<AttachedFile>?             AttachedFiles                 = default,
                           JSONLDContext?                         JSONLDContext                 = default,
                           String?                                DataSource                    = default,
                           DateTimeOffset?                        LastChange                    = default)

                : base(Id ?? UserGroup_Id.Random(),
                       JSONLDContext ?? DefaultJSONLDContext,
                       Name,
                       Description,
                       Users,
                       ParentGroup,
                       Subgroups,
                       CustomData,
                       AttachedFiles,
                       DataSource,
                       LastChange)

            {

                this._User2UserGroup_Edges          = User2UserGroupEdges.        IsNeitherNullNorEmpty() ? new List<User2UserGroupEdge>     (User2UserGroupEdges)         : new List<User2UserGroupEdge>();
                this._UserGroup2UserGroup_InEdges   = UserGroup2UserGroupInEdges. IsNeitherNullNorEmpty() ? new List<UserGroup2UserGroupEdge>(UserGroup2UserGroupInEdges)  : new List<UserGroup2UserGroupEdge>();
                this._UserGroup2UserGroup_OutEdges  = UserGroup2UserGroupOutEdges.IsNeitherNullNorEmpty() ? new List<UserGroup2UserGroupEdge>(UserGroup2UserGroupOutEdges) : new List<UserGroup2UserGroupEdge>();

            }

            #endregion


            #region CopyAllLinkedDataFrom(OldGroup)

            public override void CopyAllLinkedDataFromBase(IUserGroup OldGroup)
            {

                //if (OldGroup._User2GroupEdges.Any() && !_User2GroupEdges.Any())
                //{

                //    Add(OldGroup._User2GroupEdges);

                //    foreach (var edge in _User2GroupEdges)
                //        edge.Target = this;

                //}

            }

            #endregion

            #region ToImmutable

            /// <summary>
            /// Return an immutable version of the UserGroup.
            /// </summary>
            public static implicit operator UserGroup(Builder Builder)

                => Builder?.ToImmutable;


            /// <summary>
            /// Return an immutable version of the UserGroup.
            /// </summary>
            public UserGroup ToImmutable

                => new (Id,

                        Name,
                        Description,
                        Members,
                        ParentGroup,
                        Subgroups,

                        _User2UserGroup_Edges,
                        _UserGroup2UserGroup_InEdges,
                        _UserGroup2UserGroup_OutEdges,

                        CustomData,
                        AttachedFiles,
                        JSONLDContext,
                        DataSource,
                        LastChangeDate);

            #endregion


            #region Operator overloading

            #region Operator == (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A user group builder.</param>
            /// <param name="Builder2">Another user group identification.</param>
            /// <returns>true|false</returns>
            public static Boolean operator ==(Builder Builder1,
                                               Builder Builder2)
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
            /// <param name="Builder1">A user group builder.</param>
            /// <param name="Builder2">Another user group identification.</param>
            /// <returns>true|false</returns>
            public static Boolean operator !=(Builder Builder1,
                                               Builder Builder2)

                => !(Builder1 == Builder2);

            #endregion

            #region Operator <  (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A user group builder.</param>
            /// <param name="Builder2">Another user group identification.</param>
            /// <returns>true|false</returns>
            public static Boolean operator <(Builder Builder1,
                                              Builder Builder2)
            {

                if (Builder1 is null)
                    throw new ArgumentNullException(nameof(Builder1), "The given user group must not be null!");

                return Builder1.CompareTo(Builder2) < 0;

            }

            #endregion

            #region Operator <= (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A user group builder.</param>
            /// <param name="Builder2">Another user group identification.</param>
            /// <returns>true|false</returns>
            public static Boolean operator <=(Builder Builder1,
                                               Builder Builder2)

                => !(Builder1 > Builder2);

            #endregion

            #region Operator >  (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A user group builder.</param>
            /// <param name="Builder2">Another user group identification.</param>
            /// <returns>true|false</returns>
            public static Boolean operator >(Builder Builder1,
                                              Builder Builder2)
            {

                if (Builder1 is null)
                    throw new ArgumentNullException(nameof(Builder1), "The given user group must not be null!");

                return Builder1.CompareTo(Builder2) > 0;

            }

            #endregion

            #region Operator >= (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A user group builder.</param>
            /// <param name="Builder2">Another user group identification.</param>
            /// <returns>true|false</returns>
            public static Boolean operator >=(Builder Builder1,
                                               Builder Builder2)

                => !(Builder1 < Builder2);

            #endregion

            #endregion

            #region IComparable<UserGroup> Members

            #region CompareTo(Object)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Object">An object to compare with.</param>
            public override Int32 CompareTo(Object Object)
            {

                if (Object is UserGroup UserGroup)
                    CompareTo(UserGroup);

                throw new ArgumentException("The given object is not a user group!");

            }

            #endregion

            #region CompareTo(UserGroup)

            /// <summary>
            /// Compares two user groups.
            /// </summary>
            /// <param name="UserGroup">A user group to compare with.</param>
            public override Int32 CompareTo(IUserGroup? UserGroup)

                => UserGroup is UserGroup
                       ? Id.CompareTo(UserGroup.Id)
                       : throw new ArgumentException("The given object is not an user group!", nameof(UserGroup));

            #endregion

            #endregion

            #region IEquatable<UserGroup> Members

            #region Equals(Object)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Object">An object to compare with.</param>
            /// <returns>true|false</returns>
            public override Boolean Equals(Object Object)
            {

                if (Object is UserGroup UserGroup)
                    return Equals(UserGroup);

                return false;

            }

            #endregion

            #region Equals(UserGroup)

            /// <summary>
            /// Compares two user groups for equality.
            /// </summary>
            /// <param name="UserGroup">A user group to compare with.</param>
            /// <returns>True if both match; False otherwise.</returns>
            public override Boolean Equals(IUserGroup? UserGroup)
            {

                if (UserGroup is null)
                    return false;

                return Id.Equals(UserGroup.Id);

            }

            #endregion

            #endregion

            #region (override) GetHashCode()

            /// <summary>
            /// Get the hash code of this object.
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
