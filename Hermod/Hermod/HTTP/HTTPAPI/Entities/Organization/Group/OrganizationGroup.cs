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
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public delegate Boolean OrganizationGroupProviderDelegate(OrganizationGroup_Id OrganizationGroupId, out OrganizationGroup OrganizationGroup);

    public delegate JObject OrganizationGroupToJSONDelegate(OrganizationGroup  OrganizationGroup,
                                                            Boolean            Embedded                        = false,
                                                            InfoStatus         ExpandOrganizations             = InfoStatus.ShowIdOnly,
                                                            InfoStatus         ExpandParentGroup               = InfoStatus.ShowIdOnly,
                                                            InfoStatus         ExpandSubgroups                 = InfoStatus.ShowIdOnly,
                                                            InfoStatus         ExpandAttachedFiles             = InfoStatus.ShowIdOnly,
                                                            InfoStatus         IncludeAttachedFileSignatures   = InfoStatus.ShowIdOnly);


    /// <summary>
    /// Extension methods for the organization groups.
    /// </summary>
    public static partial class OrganizationGroupExtensions
    {

        #region ToJSON(this OrganizationGroups, Skip = null, Take = null, Embedded = false, ...)

        /// <summary>
        /// Return a JSON representation for the given enumeration of organization groups.
        /// </summary>
        /// <param name="OrganizationGroups">An enumeration of organization groups.</param>
        /// <param name="Skip">The optional number of organization groups to skip.</param>
        /// <param name="Take">The optional number of organization groups to return.</param>
        /// <param name="Embedded">Whether this data is embedded into another data structure, e.g. into a organization group.</param>
        public static JArray ToJSON(this IEnumerable<OrganizationGroup>  OrganizationGroups,
                                    UInt64?                              Skip                            = null,
                                    UInt64?                              Take                            = null,
                                    Boolean                              Embedded                        = false,
                                    InfoStatus                           ExpandOrganizations             = InfoStatus.ShowIdOnly,
                                    InfoStatus                           ExpandParentGroup               = InfoStatus.ShowIdOnly,
                                    InfoStatus                           ExpandSubgroups                 = InfoStatus.ShowIdOnly,
                                    InfoStatus                           ExpandAttachedFiles             = InfoStatus.ShowIdOnly,
                                    InfoStatus                           IncludeAttachedFileSignatures   = InfoStatus.ShowIdOnly,
                                    OrganizationGroupToJSONDelegate?     OrganizationGroupToJSON         = null)


            => OrganizationGroups?.Any() != true

                   ? new JArray()

                   : new JArray(OrganizationGroups.
                                    OrderBy       (organizationGroup => organizationGroup.Id).
                                    SkipTakeFilter(Skip, Take).
                                    SafeSelect    (organizationGroup => OrganizationGroupToJSON != null
                                                                            ? OrganizationGroupToJSON (organizationGroup,
                                                                                                       Embedded,
                                                                                                       ExpandOrganizations,
                                                                                                       ExpandParentGroup,
                                                                                                       ExpandSubgroups,
                                                                                                       ExpandAttachedFiles,
                                                                                                       IncludeAttachedFileSignatures)

                                                                            : organizationGroup.ToJSON(Embedded,
                                                                                                       ExpandOrganizations,
                                                                                                       ExpandParentGroup,
                                                                                                       ExpandSubgroups,
                                                                                                       ExpandAttachedFiles,
                                                                                                       IncludeAttachedFileSignatures)));

        #endregion

    }


    /// <summary>
    /// A organization group.
    /// </summary>
    public class OrganizationGroup : AGroup<OrganizationGroup_Id,
                                            IOrganizationGroup,
                                            Organization_Id,
                                            IOrganization>,
                                     IOrganizationGroup
    {

        #region Data

        /// <summary>
        /// The default JSON-LD context of organization groups.
        /// </summary>
        public new readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/HTTPExtAPI/organizationGroup");

        #endregion

        HTTPExtAPI? IOrganizationGroup.API { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        #region Constructor(s)

        /// <summary>
        /// Create a new organization group.
        /// </summary>
        /// <param name="Id">The unique identification of the organization group.</param>
        /// 
        /// <param name="Name">A multi-language name of the organization group.</param>
        /// <param name="Description">A multi-language description of the organization group.</param>
        /// <param name="Organizations">An enumeration of organizations.</param>
        /// <param name="ParentGroup">An optional parent organization group.</param>
        /// <param name="Subgroups">Optional organization subgroups.</param>
        /// 
        /// <param name="CustomData">Custom data to be stored with this organization group.</param>
        /// <param name="AttachedFiles">Optional files attached to this organization group.</param>
        /// <param name="JSONLDContext">The JSON-LD context of this organization group.</param>
        /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
        /// <param name="LastChange">The timestamp of the last changes within this organization group. Can e.g. be used as a HTTP ETag.</param>
        public OrganizationGroup(OrganizationGroup_Id              Id,

                                 I18NString                        Name,
                                 I18NString?                       Description     = default,
                                 IEnumerable<IOrganization>?       Organizations   = default,
                                 IOrganizationGroup?               ParentGroup     = default,
                                 IEnumerable<IOrganizationGroup>?  Subgroups       = default,

                                 JObject?                          CustomData      = default,
                                 IEnumerable<AttachedFile>?        AttachedFiles   = default,
                                 JSONLDContext?                    JSONLDContext   = default,
                                 String?                           DataSource      = default,
                                 DateTime?                         LastChange      = default)

            : base(Id,

                   Name,
                   Description,
                   Organizations,
                   ParentGroup,
                   Subgroups,

                   CustomData,
                   AttachedFiles,
                   JSONLDContext ?? DefaultJSONLDContext,
                   DataSource,
                   LastChange)

        { }

        #endregion


        #region ToJSON(...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        public override JObject ToJSON(Boolean Embedded = false)

            => ToJSON(Embedded: false,
                      ExpandOrganizations: InfoStatus.ShowIdOnly,
                      ExpandParentGroup: InfoStatus.ShowIdOnly,
                      ExpandSubgroups: InfoStatus.ShowIdOnly,
                      ExpandAttachedFiles: InfoStatus.ShowIdOnly,
                      IncludeAttachedFileSignatures: InfoStatus.ShowIdOnly);


        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data is embedded into another data structure, e.g. into a OrganizationGroup.</param>
        public virtual JObject ToJSON(Boolean Embedded = false,
                                      InfoStatus ExpandOrganizations = InfoStatus.ShowIdOnly,
                                      InfoStatus ExpandParentGroup = InfoStatus.ShowIdOnly,
                                      InfoStatus ExpandSubgroups = InfoStatus.ShowIdOnly,
                                      InfoStatus ExpandAttachedFiles = InfoStatus.ShowIdOnly,
                                      InfoStatus IncludeAttachedFileSignatures = InfoStatus.ShowIdOnly)
        {

            var JSON = JSONObject.Create(

                           Members.SafeAny() && ExpandOrganizations != InfoStatus.Hidden
                               ? ExpandSubgroups.Switch(
                                       () => new JProperty("memberIds", new JArray(Members.SafeSelect(organization => organization.Id.ToString()))),
                                       () => new JProperty("members", new JArray(Members.SafeSelect(organization => organization.ToJSON(Embedded: true)))))
                               //ExpandParentGroup:  InfoStatus.Hidden,
                               //ExpandSubgroups:    InfoStatus.Expand)))))
                               : null,

                           ParentGroup is not null && ExpandParentGroup != InfoStatus.Hidden
                               ? ExpandParentGroup.Switch(
                                       () => new JProperty("parentGroupId", ParentGroup.Id.ToString()),
                                       () => new JProperty("parentGroup",   ParentGroup.ToJSON(true)))
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

        #region (static) TryParseJSON(JSONObject, ..., out OrganizationGroup, out ErrorResponse)

        /// <summary>
        /// Try to parse the given organization group JSON.
        /// </summary>
        /// <param name="JSONObject">A JSON object.</param>
        /// <param name="OrganizationGroupProvider">A delegate resolving organization groups.</param>
        /// <param name="OrganizationProvider">A delegate resolving organizations.</param>
        /// <param name="OrganizationGroup">The parsed organization group.</param>
        /// <param name="ErrorResponse">An error message.</param>
        /// <param name="OrganizationGroupIdURL">An optional OrganizationGroup identification, e.g. from the HTTP URL.</param>
        public static Boolean TryParseJSON(JObject JSONObject,
                                           OrganizationGroupProviderDelegate OrganizationGroupProvider,
                                           OrganizationProviderDelegate OrganizationProvider,
                                           out OrganizationGroup? OrganizationGroup,
                                           out String? ErrorResponse,
                                           OrganizationGroup_Id? OrganizationGroupIdURL = null)
        {

            try
            {

                OrganizationGroup = null;

                if (JSONObject?.HasValues != true)
                {
                    ErrorResponse = "The given JSON object must not be null or empty!";
                    return false;
                }

                #region Parse OrganizationGroupId  [optional]

                // Verify that a given OrganizationGroup identification
                //   is at least valid.
                if (JSONObject.ParseOptional("@id",
                                             "OrganizationGroup identification",
                                             OrganizationGroup_Id.TryParse,
                                             out OrganizationGroup_Id? OrganizationGroupIdBody,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                if (!OrganizationGroupIdURL.HasValue && !OrganizationGroupIdBody.HasValue)
                {
                    ErrorResponse = "The OrganizationGroup identification is missing!";
                    return false;
                }

                if (OrganizationGroupIdURL.HasValue && OrganizationGroupIdBody.HasValue && OrganizationGroupIdURL.Value != OrganizationGroupIdBody.Value)
                {
                    ErrorResponse = "The optional OrganizationGroup identification given within the JSON body does not match the one given in the URI!";
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

                if (Context != OrganizationGroup.JSONLDContext)
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


                #region Parse ParentGroup identification      [optional]

                if (JSONObject.ParseOptional("parentGroupId",
                                             "parentgroup identification",
                                             OrganizationGroup_Id.TryParse,
                                             out OrganizationGroup_Id? ParentGroupId,
                                             out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                OrganizationGroup? ParentGroup = null;

                if (ParentGroupId.HasValue)
                    OrganizationGroupProvider(ParentGroupId.Value, out ParentGroup);

                #endregion

                #region Parse Subgroup identifications        [optional]

                if (JSONObject.ParseOptional("SubgroupIds",
                                             "subgroup identifications",
                                             OrganizationGroup_Id.TryParse,
                                             out IEnumerable<OrganizationGroup_Id> SubgroupIds,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                List<OrganizationGroup> Subgroups = null;

                if (SubgroupIds?.Any() == true)
                {

                    Subgroups = new List<OrganizationGroup>();

                    foreach (var organizationGroupId in SubgroupIds)
                    {
                        if (OrganizationGroupProvider(organizationGroupId, out OrganizationGroup organizationGroup))
                            Subgroups.Add(organizationGroup);
                    }

                }

                #endregion

                #region Parse Organization identifications    [optional]

                if (JSONObject.ParseOptional("organizationIds",
                                             "organization identifications",
                                             Organization_Id.TryParse,
                                             out IEnumerable<Organization_Id> OrganizationIds,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                List<IOrganization>? Organizations = null;

                if (OrganizationIds?.Any() == true)
                {

                    Organizations = new List<IOrganization>();

                    foreach (var organizationId in OrganizationIds)
                    {
                        if (OrganizationProvider(organizationId, out var organization))
                            Organizations.Add(organization);
                    }

                }

                #endregion


                #region Get   DataSource       [optional]

                var DataSource = JSONObject.GetOptional("dataSource");

                #endregion


                #region Parse CryptoHash       [optional]

                var CryptoHash = JSONObject.GetOptional("cryptoHash");

                #endregion


                OrganizationGroup = new OrganizationGroup(OrganizationGroupIdBody ?? OrganizationGroupIdURL.Value,

                                                          Name,
                                                          Description,
                                                          Organizations,
                                                          ParentGroup,
                                                          Subgroups,

                                                          null,
                                                          null,
                                                          Context,
                                                          DataSource,
                                                          null);

                ErrorResponse = null;
                return true;

            }
            catch (Exception e)
            {
                ErrorResponse = e.Message;
                OrganizationGroup = null;
                return false;
            }

        }

        #endregion


        #region CopyAllLinkedDataFrom(OldOrganizationGroup)

        public override void CopyAllLinkedDataFrom(IOrganizationGroup OldOrganizationGroup)
        {

        }

        #endregion


        #region Operator overloading

        #region Operator == (OrganizationGroup1, OrganizationGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroup1">A organization group.</param>
        /// <param name="OrganizationGroup2">Another organization group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator ==(OrganizationGroup OrganizationGroup1,
                                           OrganizationGroup OrganizationGroup2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(OrganizationGroup1, OrganizationGroup2))
                return true;

            // If one is null, but not both, return false.
            if ((OrganizationGroup1 is null) || (OrganizationGroup2 is null))
                return false;

            return OrganizationGroup1.Equals(OrganizationGroup2);

        }

        #endregion

        #region Operator != (OrganizationGroup1, OrganizationGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroup1">A organization group.</param>
        /// <param name="OrganizationGroup2">Another organization group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator !=(OrganizationGroup OrganizationGroup1,
                                           OrganizationGroup OrganizationGroup2)

            => !(OrganizationGroup1 == OrganizationGroup2);

        #endregion

        #region Operator <  (OrganizationGroup1, OrganizationGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroup1">A organization group.</param>
        /// <param name="OrganizationGroup2">Another organization group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <(OrganizationGroup OrganizationGroup1,
                                          OrganizationGroup OrganizationGroup2)
        {

            if (OrganizationGroup1 is null)
                throw new ArgumentNullException(nameof(OrganizationGroup1), "The given organization group must not be null!");

            return OrganizationGroup1.CompareTo(OrganizationGroup2) < 0;

        }

        #endregion

        #region Operator <= (OrganizationGroup1, OrganizationGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroup1">A organization group.</param>
        /// <param name="OrganizationGroup2">Another organization group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <=(OrganizationGroup OrganizationGroup1,
                                           OrganizationGroup OrganizationGroup2)

            => !(OrganizationGroup1 > OrganizationGroup2);

        #endregion

        #region Operator >  (OrganizationGroup1, OrganizationGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroup1">A organization group.</param>
        /// <param name="OrganizationGroup2">Another organization group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >(OrganizationGroup OrganizationGroup1,
                                          OrganizationGroup OrganizationGroup2)
        {

            if (OrganizationGroup1 is null)
                throw new ArgumentNullException(nameof(OrganizationGroup1), "The given organization group must not be null!");

            return OrganizationGroup1.CompareTo(OrganizationGroup2) > 0;

        }

        #endregion

        #region Operator >= (OrganizationGroup1, OrganizationGroup2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OrganizationGroup1">A organization group.</param>
        /// <param name="OrganizationGroup2">Another organization group.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >=(OrganizationGroup OrganizationGroup1,
                                           OrganizationGroup OrganizationGroup2)

            => !(OrganizationGroup1 < OrganizationGroup2);

        #endregion

        #endregion

        #region IComparable<OrganizationGroup> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public override Int32 CompareTo(Object Object)
        {

            if (Object is OrganizationGroup OrganizationGroup)
                CompareTo(OrganizationGroup);

            throw new ArgumentException("The given object is not a organization group!");

        }

        #endregion

        #region CompareTo(OrganizationGroup)

        /// <summary>
        /// Compares two organization groups.
        /// </summary>
        /// <param name="OrganizationGroup">A organization group to compare with.</param>
        public override Int32 CompareTo(IOrganizationGroup? OrganizationGroup)
        {

            if (OrganizationGroup is null)
                throw new ArgumentNullException(nameof(OrganizationGroup), "The given organization group must not be null!");

            return Id.CompareTo(OrganizationGroup.Id);

        }

        #endregion

        #endregion

        #region IEquatable<OrganizationGroup> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object is OrganizationGroup OrganizationGroup)
                return Equals(OrganizationGroup);

            return false;

        }

        #endregion

        #region Equals(OrganizationGroup)

        /// <summary>
        /// Compares two organization groups for equality.
        /// </summary>
        /// <param name="OrganizationGroup">A organization group to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public override Boolean Equals(IOrganizationGroup? OrganizationGroup)
        {

            if (OrganizationGroup is null)
                return false;

            return Id.Equals(OrganizationGroup.Id);

        }

        #endregion

        #endregion

        #region GetHashCode()

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


        #region ToBuilder(NewOrganizationGroupId = null)

        /// <summary>
        /// Return a builder for this organization group.
        /// </summary>
        /// <param name="NewOrganizationGroupId">An optional new organization group identification.</param>
        public Builder ToBuilder(OrganizationGroup_Id? NewOrganizationGroupId = null)

            => new (NewOrganizationGroupId ?? Id,

                    Name,
                    Description,
                    Members,
                    ParentGroup,
                    Subgroups,

                    CustomData,
                    AttachedFiles,
                    JSONLDContext,
                    DataSource,
                    LastChangeDate);

        #endregion

        #region (class) Builder

        /// <summary>
        /// A organization group builder.
        /// </summary>
        public new class Builder : AGroup<OrganizationGroup_Id,
                                          IOrganizationGroup,
                                          Organization_Id,
                                          IOrganization>.Builder
        {

            #region Constructor(s)

            /// <summary>
            /// Create a new organization group builder.
            /// </summary>
            /// <param name="Id">The unique identification of the organization group.</param>
            /// 
            /// <param name="Name">A multi-language name of the organization group.</param>
            /// <param name="Description">A multi-language description of the organization group.</param>
            /// <param name="Organizations">An enumeration of organizations.</param>
            /// <param name="ParentGroup">An optional parent organization group.</param>
            /// <param name="Subgroups">Optional organization subgroups.</param>
            /// 
            /// <param name="CustomData">Custom data to be stored with this organization group.</param>
            /// <param name="AttachedFiles">Optional files attached to this organization group.</param>
            /// <param name="JSONLDContext">The JSON-LD context of this organization group.</param>
            /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
            /// <param name="LastChange">The timestamp of the last changes within this organization group. Can e.g. be used as a HTTP ETag.</param>
            public Builder(OrganizationGroup_Id?             Id              = default,

                           I18NString?                       Name            = default,
                           I18NString?                       Description     = default,
                           IEnumerable<IOrganization>?       Organizations   = default,
                           IOrganizationGroup?               ParentGroup     = default,
                           IEnumerable<IOrganizationGroup>?  Subgroups       = default,

                           JObject?                          CustomData      = default,
                           IEnumerable<AttachedFile>?        AttachedFiles   = default,
                           JSONLDContext?                    JSONLDContext   = default,
                           String?                           DataSource      = default,
                           DateTime?                         LastChange      = default)

                : base(Id ?? OrganizationGroup_Id.Random(),
                       JSONLDContext ?? DefaultJSONLDContext,

                       Name,
                       Description,
                       Organizations,
                       ParentGroup,
                       Subgroups,

                       CustomData,
                       AttachedFiles,
                       DataSource,
                       LastChange)

            { }

            #endregion


            #region CopyAllLinkedDataFrom(OldOrganizationGroup)

            public override void CopyAllLinkedDataFrom(IOrganizationGroup OldOrganizationGroup)
            {

            }

            #endregion


            #region ToImmutable

            /// <summary>
            /// Return an immutable version of the OrganizationGroup.
            /// </summary>
            public static implicit operator OrganizationGroup(Builder Builder)

                => Builder?.ToImmutable;


            /// <summary>
            /// Return an immutable version of the OrganizationGroup.
            /// </summary>
            public OrganizationGroup ToImmutable

                => new (Id,

                        Name,
                        Description,
                        Members,
                        ParentGroup,
                        Subgroups,

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
            /// <param name="Builder1">A organization group builder.</param>
            /// <param name="Builder2">Another organization group builder.</param>
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
            /// <param name="Builder1">A organization group builder.</param>
            /// <param name="Builder2">Another organization group builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator !=(Builder Builder1,
                                               Builder Builder2)

                => !(Builder1 == Builder2);

            #endregion

            #region Operator <  (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization group builder.</param>
            /// <param name="Builder2">Another organization group builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator <(Builder Builder1,
                                              Builder Builder2)
            {

                if (Builder1 is null)
                    throw new ArgumentNullException(nameof(Builder1), "The given organization group must not be null!");

                return Builder1.CompareTo(Builder2) < 0;

            }

            #endregion

            #region Operator <= (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization group builder.</param>
            /// <param name="Builder2">Another organization group builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator <=(Builder Builder1,
                                               Builder Builder2)

                => !(Builder1 > Builder2);

            #endregion

            #region Operator >  (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization group builder.</param>
            /// <param name="Builder2">Another organization group builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator >(Builder Builder1,
                                              Builder Builder2)
            {

                if (Builder1 is null)
                    throw new ArgumentNullException(nameof(Builder1), "The given organization group must not be null!");

                return Builder1.CompareTo(Builder2) > 0;

            }

            #endregion

            #region Operator >= (Builder1, Builder2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder1">A organization group builder.</param>
            /// <param name="Builder2">Another organization group builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator >=(Builder Builder1,
                                               Builder Builder2)

                => !(Builder1 < Builder2);

            #endregion

            #endregion

            #region IComparable<OrganizationGroup> Members

            #region CompareTo(Object)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Object">An object to compare with.</param>
            public override Int32 CompareTo(Object Object)
            {

                if (Object is OrganizationGroup OrganizationGroup)
                    CompareTo(OrganizationGroup);

                throw new ArgumentException("The given object is not a organization group!");

            }

            #endregion

            #region CompareTo(OrganizationGroup)

            /// <summary>
            /// Compares two organization groups.
            /// </summary>
            /// <param name="OrganizationGroup">A organization group to compare with.</param>
            public override Int32 CompareTo(IOrganizationGroup? OrganizationGroup)
            {

                if (OrganizationGroup is null)
                    throw new ArgumentNullException(nameof(OrganizationGroup), "The given organization group must not be null!");

                return Id.CompareTo(OrganizationGroup.Id);

            }

            #endregion

            #endregion

            #region IEquatable<OrganizationGroup> Members

            #region Equals(Object)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Object">An object to compare with.</param>
            /// <returns>true|false</returns>
            public override Boolean Equals(Object Object)
            {

                if (Object is OrganizationGroup OrganizationGroup)
                    return Equals(OrganizationGroup);

                return false;

            }

            #endregion

            #region Equals(OrganizationGroup)

            /// <summary>
            /// Compares two organization groups for equality.
            /// </summary>
            /// <param name="OrganizationGroup">A organization group to compare with.</param>
            /// <returns>True if both match; False otherwise.</returns>
            public override Boolean Equals(IOrganizationGroup? OrganizationGroup)
            {

                if (OrganizationGroup is null)
                    return false;

                return Id.Equals(OrganizationGroup.Id);

            }

            #endregion

            #endregion

            #region GetHashCode()

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
