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

using System;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Aegir;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for OrganizationInfos.
    /// </summary>
    public static class Organization2InfoExtensions
    {

        public static JArray ToJSON(this IEnumerable<OrganizationInfo2> OrganizationInfos)
        {

            if (OrganizationInfos?.Any() == false)
                return new JArray();

            return JSONArray.Create(OrganizationInfos.Select(orgInfo => orgInfo.ToJSON()));

        }

    }

    public class OrganizationInfo2 : Organization
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
        public new readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/organizationInfo");

        #endregion

        #region Properties

        public   IUser                   You                               { get; }
        //public   IEnumerable<User>       Admins                            { get; }
        //public   IEnumerable<User>       Members                           { get; }

        //public   IEnumerable<User>       Guests                            { get; }

        internal List<OrganizationInfo>  internalChilds;
        public IEnumerable<OrganizationInfo> Childs
                => internalChilds;

        public   Boolean                 YouAreMember                      { get; }
        public   Boolean                 YouCanAddMembers                  { get; }
        public   Boolean                 YouCanCreateChildOrganizations    { get; }
        public   Boolean                 MemberOfChild                     { get; set; }

        #endregion

        #region Constructor(s)

        public OrganizationInfo2(IOrganization  organization,
                                 IUser          You)

            : base(organization.Id,
                   organization.Name,
                   organization.Description,
                   organization.Website,
                   organization.EMail,
                   organization.Telephone,
                   organization.Address,
                   organization.GeoLocation,
                   _ => organization.Tags,
                   organization.IsDisabled,

                   null,

                   organization.User2OrganizationEdges,
                   organization.Organization2OrganizationInEdges,
                   organization.Organization2OrganizationOutEdges,

                   organization.CustomData,
                   organization.AttachedFiles,
                   organization.JSONLDContext,
                   organization.DataSource,
                   organization.LastChangeDate)

        {

            this.You                             = You;
            //this.Admins                          = _User2Organization_InEdges.Where(_ => _.EdgeLabel == User2OrganizationEdgeTypes.IsAdmin). SafeSelect(edge => edge.Source).ToArray();
            //this.Members                         = _User2Organization_InEdges.Where(_ => _.EdgeLabel == User2OrganizationEdgeTypes.IsMember).SafeSelect(edge => edge.Source).ToArray();
            //this.Guests                          = _User2Organization_InEdges.Where(_ => _.EdgeLabel == User2OrganizationEdgeTypes.IsGuest). SafeSelect(edge => edge.Source).ToArray();

            void CheckAccessRights(IOrganization  OOORg,
                                   ref Boolean    _YouAreMemberRecursion,
                                   ref Boolean    _YouCanAddMembersRecursion,
                                   ref Boolean    _YouCanCreateChildOrganizationsRecursion)
            {

                foreach (var parent in OOORg.ParentOrganizations)
                {
                    CheckAccessRights(parent,
                                      ref _YouAreMemberRecursion,
                                      ref _YouCanAddMembersRecursion,
                                      ref _YouCanCreateChildOrganizationsRecursion);
                }

                if (_YouAreMemberRecursion == false && (OOORg.Members.Contains(You) || OOORg.Admins.Contains(You)))
                    _YouAreMemberRecursion = true;

                if (_YouCanAddMembersRecursion == false && OOORg.Admins.Contains(You))
                    _YouCanAddMembersRecursion = true;

                if (_YouCanCreateChildOrganizationsRecursion == false && OOORg.Admins.Contains(You))
                    _YouCanCreateChildOrganizationsRecursion = true;

            }


            var __YouAreMemberRecursion                    = false;
            var __YouCanAddMembersRecursion                = false;
            var __YouCanCreateChildOrganizationsRecursion  = false;

            CheckAccessRights(this,
                              ref __YouAreMemberRecursion,
                              ref __YouCanAddMembersRecursion,
                              ref __YouCanCreateChildOrganizationsRecursion);

            this.YouAreMember                    = __YouAreMemberRecursion                   || Admins.Contains(You) || Members.Contains(You);
            this.YouCanAddMembers                = __YouCanAddMembersRecursion               || Admins.Contains(You);
            this.YouCanCreateChildOrganizations  = __YouCanCreateChildOrganizationsRecursion || Admins.Contains(You);

            #region GetChilds(Org, ...)

            IEnumerable<OrganizationInfo> GetChilds(IOrganization  Org,
                                                    Boolean        YouAreMemberRecursion,
                                                    Boolean        YouCanAddMembersRecursion,
                                                    Boolean        YouCanCreateChildOrganizationsRecursion)

                => Org.Organization2OrganizationInEdges.
                       Where     (edge => edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf).
                       SafeSelect(edge => new OrganizationInfo(edge.Source,
                                                               You,
                                                               YouAreMemberRecursion,
                                                               YouCanAddMembersRecursion,
                                                               YouCanCreateChildOrganizationsRecursion));

            #endregion

            #region CheckYouMembership(OrgInfo)

            Boolean CheckYouMembership(OrganizationInfo OrgInfo)
            {

                if (OrgInfo.internalChilds == null || OrgInfo.internalChilds.Count == 0)
                    return OrgInfo.YouAreMember;

                var res = OrgInfo.YouAreMember;

                foreach (var ChildOrg in OrgInfo.Childs.ToArray())
                {

                    var resOfChild = CheckYouMembership(ChildOrg);

                    // Remove all child organizations if the given user is no direct member!
                    if (!resOfChild)
                        OrgInfo.internalChilds.Remove(ChildOrg);

                    res |= resOfChild;

                }

                return res;

            }

            #endregion

            var childInfos                       = GetChilds(organization,
                                                             this.YouAreMember,
                                                             this.YouCanAddMembers,
                                                             this.YouCanCreateChildOrganizations).ToList();

            foreach (var childInfo in childInfos)
                CheckYouMembership(childInfo);

            this.internalChilds = childInfos.Where(org => org.YouAreMember || org.internalChilds.Count > 0).ToList();

        }

        #endregion


        public JObject ToJSON(Boolean     Embedded                = false,
                              InfoStatus  ExpandMembers           = InfoStatus.ShowIdOnly,
                              InfoStatus  ExpandParents           = InfoStatus.ShowIdOnly,
                              InfoStatus  ExpandSubOrganizations  = InfoStatus.ShowIdOnly,
                              InfoStatus  ExpandTags              = InfoStatus.ShowIdOnly,
                              Boolean     IncludeCryptoHash       = true)
        {

            var org      = base.ToJSON(Embedded,
                                       ExpandMembers,
                                       ExpandParents,
                                       ExpandSubOrganizations,
                                       ExpandTags,
                                       IncludeCryptoHash);

            org["@context"] = JSONLDContext.ToString();

            org.Add("youAreMember",                    YouAreMember);
            org.Add("youCanAddMembers",                YouCanAddMembers);
            org.Add("youCanCreateChildOrganizations",  YouCanCreateChildOrganizations);
            //org.Add("admins",                          JSONArray.Create(Admins. SafeSelect(user => user.ToJSON())));

            //if (YouAreMember)
            //    org.Add("members",                     JSONArray.Create(Members.SafeSelect(user => user.ToJSON())));

            //org.Add("_childs",                         new JArray(Childs.OrderBy(child => child.Id).Select(child => child.ToJSON())));

            return org;

        }

    }

}
