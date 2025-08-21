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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{
    public interface IUserGroup : IGroup,
                                  IEntity<UserGroup_Id>,
                                  IEquatable<IUserGroup>,
                                  IComparable<IUserGroup>,
                                  IComparable
    {

        HTTPExtAPI? API { get; set; }
        HTTPExtAPIX? APIX { get; set; }
        IEnumerable<UserGroup> ParentUserGroups { get; }
        IEnumerable<UserGroup> SubUserGroups { get; }
        IEnumerable<User2UserGroupEdge> User2UserGroupEdges { get; }
        IEnumerable<UserGroup2UserGroupEdge> UserGroup2UserGroupInEdges { get; }
        IEnumerable<UserGroup2UserGroupEdge> UserGroup2UserGroupOutEdges { get; }

        UserGroup2UserGroupEdge AddEdge(UserGroup2UserGroupEdge Edge);
        IEnumerable<UserGroup2UserGroupEdge> AddEdges(IEnumerable<UserGroup2UserGroupEdge> Edges);
        UserGroup2UserGroupEdge AddInEdge(UserGroup2UserGroupEdgeLabel EdgeLabel, UserGroup SourceUserGroup, PrivacyLevel PrivacyLevel = PrivacyLevel.World);
        UserGroup2UserGroupEdge AddOutEdge(UserGroup2UserGroupEdgeLabel EdgeLabel, UserGroup TargetUserGroup, PrivacyLevel PrivacyLevel = PrivacyLevel.World);
        User2UserGroupEdge AddUser(User2UserGroupEdge Edge);
        User2UserGroupEdge AddUser(User2UserGroupEdgeLabel EdgeLabel, IUser Source, PrivacyLevel PrivacyLevel = PrivacyLevel.Private);
        UserGroup AddUsers(IEnumerable<User2UserGroupEdge> Edges);
        //int CompareTo(UserGroup UserGroup);
        void CopyAllLinkedDataFromBase(IUserGroup OldGroup);
        IEnumerable<User2UserGroupEdgeLabel> EdgeLabels(IUser User);
        IEnumerable<UserGroup2UserGroupEdgeLabel> EdgeLabels(UserGroup UserGroup);
        IEnumerable<User2UserGroupEdge> Edges(IUser User);
        IEnumerable<User2UserGroupEdge> Edges(User2UserGroupEdgeLabel EdgeLabel, IUser User);
        //bool Equals(object Object);
        //bool Equals(UserGroup UserGroup);
        IEnumerable<UserGroup> GetAllChilds(Func<UserGroup, bool> Include = null);
        IEnumerable<UserGroup> GetAllParents(Func<UserGroup, bool> Include = null);
        //int GetHashCode();
        IEnumerable<UserGroup> GetMeAndAllMyChilds(Func<UserGroup, bool> Include = null);
        IEnumerable<UserGroup> GetMeAndAllMyParents(Func<UserGroup, bool> Include = null);
        bool HasEdge(User2UserGroupEdgeLabel EdgeLabel, IUser User);
        bool RemoveInEdge(UserGroup2UserGroupEdge Edge);
        void RemoveInEdges(UserGroup2UserGroupEdgeLabel EdgeLabel, UserGroup SourceUserGroup);
        bool RemoveOutEdge(UserGroup2UserGroupEdge Edge);
        void RemoveOutEdges(UserGroup2UserGroupEdgeLabel EdgeLabel, UserGroup TargetUserGroup);
        UserGroup.Builder ToBuilder(UserGroup_Id? NewUserGroupId = null);
        JObject ToJSON(bool Embedded = false);
        JObject ToJSON(bool Embedded = false, InfoStatus ExpandUsers = InfoStatus.ShowIdOnly, InfoStatus ExpandParentGroup = InfoStatus.ShowIdOnly, InfoStatus ExpandSubgroups = InfoStatus.ShowIdOnly, InfoStatus ExpandAttachedFiles = InfoStatus.ShowIdOnly, InfoStatus IncludeAttachedFileSignatures = InfoStatus.ShowIdOnly);
        string ToString();
        IEnumerable<User2UserGroupEdge> User2GroupInEdges(Func<User2UserGroupEdgeLabel, bool>? User2GroupEdgeFilter = null);

    }

}
