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

using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public interface IGroup
    {

        JObject ToJSON();

    }

    public interface IGroup<TGroupId> : IGroup,
                                        IHasId<TGroupId>

        where TGroupId : IId

    {

        JObject ToJSON();

    }

    /// <summary>
    /// An abstract group.
    /// </summary>
    /// <typeparam name="TGroupId">The type of the group identification.</typeparam>
    /// <typeparam name="TGroup">The type of the group.</typeparam>
    /// <typeparam name="TMembersId">The type of the members of the group.</typeparam>
    /// <typeparam name="TMembers">The type of the group members.</typeparam>
    public abstract class AGroup<TGroupId,
                                 TGroup,
                                 TMembersId,
                                 TMembers> : AEntity<TGroupId,
                                                     TGroup>,
                                             IGroup<TGroupId>

        where TGroupId   : IId
        where TGroup     : class, IHasId<TGroupId>, IGroup
        where TMembersId : IId
        where TMembers   : class, IHasId<TMembersId>

    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
        public readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/group");

        #endregion

        #region Properties

        #region API

        private Object _API;

        /// <summary>
        /// The API of this object.
        /// </summary>
        public Object API
        {

            get
            {
                return _API;
            }

            set
            {

                if (_API != null)
                    throw new ArgumentException("Illegal attempt to change the API!");

                _API = value ?? throw new ArgumentException("Illegal attempt to delete the API!");

            }

        }

        #endregion


        /// <summary>
        /// The members of this group.
        /// </summary>
        public IEnumerable<TMembers>      Members           { get; }

        /// <summary>
        /// An optional parent group.
        /// </summary>
        public TGroup?                    ParentGroup       { get; }

        /// <summary>
        /// Optional subgroups.
        /// </summary>
        public IEnumerable<TGroup>        Subgroups         { get; }

        /// <summary>
        /// Optional files attached to this group.
        /// </summary>
        public IEnumerable<AttachedFile>  AttachedFiles     { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract group.
        /// </summary>
        /// <param name="Id">The unique identification of the group.</param>
        /// 
        /// <param name="Name">A multi-language name of the group.</param>
        /// <param name="Description">A multi-language description of the group.</param>
        /// <param name="Members">The members of the group.</param>
        /// <param name="ParentGroup">An optional parent group.</param>
        /// <param name="Subgroups">Optional subgroups.</param>
        /// <param name="CustomData">Custom data to be stored with this group.</param>
        /// <param name="AttachedFiles">Optional files attached to this group.</param>
        /// 
        /// <param name="JSONLDContext">The JSON-LD context of this group.</param>
        /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
        /// <param name="LastChange">The timestamp of the last changes within this group. Can e.g. be used as a HTTP ETag.</param>
        public AGroup(TGroupId                    Id,

                      I18NString?                 Name,
                      I18NString?                 Description     = default,
                      IEnumerable<TMembers>?      Members         = default,
                      TGroup?                     ParentGroup     = default,
                      IEnumerable<TGroup>?        Subgroups       = default,
                      JObject?                    CustomData      = default,
                      IEnumerable<AttachedFile>?  AttachedFiles   = default,

                      JSONLDContext?              JSONLDContext   = default,
                      String?                     DataSource      = default,
                      DateTime?                   LastChange      = default)

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

            this.Members        = Members       ?? Array.Empty<TMembers>();
            this.ParentGroup    = ParentGroup;
            this.Subgroups      = Subgroups     ?? Array.Empty<TGroup>();
            this.AttachedFiles  = AttachedFiles ?? Array.Empty<AttachedFile>();

        }

        #endregion


        #region (protected) ToJSON(JSONLDContext, ...)

        public JObject ToJSON()
            => new JObject();

        protected JObject ToJSON(String                   JSONLDContext,
                                 Boolean                  Embedded                        = false,
                                 InfoStatus               ExpandAttachedFiles             = InfoStatus.ShowIdOnly,
                                 InfoStatus               IncludeAttachedFileSignatures   = InfoStatus.ShowIdOnly,
                                 Func<JObject, JObject>?  CustomAGroupSerializer          = null)
        {

            var JSON = JSONObject.Create(

                    new JProperty("@id",                    Id.           ToString()),

                    Embedded
                        ? null
                        : new JProperty("@context",         JSONLDContext.ToString()),

                    new JProperty("name",                   Name.         ToJSON()),

                    Description.IsNotNullOrEmpty()
                        ? new JProperty("description",      Description.  ToJSON())
                        : null,

                    AttachedFiles.SafeAny() && ExpandAttachedFiles != InfoStatus.Hidden
                        ? ExpandAttachedFiles.Switch(
                                () => new JProperty("attachedFileIds",  new JArray(AttachedFiles.SafeSelect(attachedFile => attachedFile.Id.ToString()))),
                                () => new JProperty("attachedFiles",    new JArray(AttachedFiles.SafeSelect(attachedFile => attachedFile.   ToJSON(Embedded:           true,
                                                                                                                                                   IncludeSignatures:  IncludeAttachedFileSignatures,
                                                                                                                                                   IncludeCryptoHash:  true)))))
                        : null

                    //DataSource?. ToJSON("dataSource")

                );

            return CustomAGroupSerializer is not null
                       ? CustomAGroupSerializer(JSON)
                       : JSON ;

        }

        #endregion



        #region (class) Builder

        /// <summary>
        /// An abstract group builder.
        /// </summary>
        public new abstract class Builder : AEntity<TGroupId, TGroup>.Builder
        {

            #region Properties

            /// <summary>
            /// The members of this group.
            /// </summary>
            public HashSet<TMembers>      Members           { get; }

            /// <summary>
            /// An optional parent group.
            /// </summary>
            public TGroup                 ParentGroup       { get; set; }

            /// <summary>
            /// Optional subgroups.
            /// </summary>
            public HashSet<TGroup>        Subgroups         { get; }

            /// <summary>
            /// Optional files attached to this group.
            /// </summary>
            public HashSet<AttachedFile>  AttachedFiles     { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new user group builder.
            /// </summary>
            /// <param name="Id">The unique identification of the group.</param>
            /// <param name="JSONLDContext">The JSON-LD context of this group.</param>
            /// 
            /// <param name="Name">A multi-language name of the group.</param>
            /// <param name="Description">A multi-language description of the group.</param>
            /// <param name="Members">The members of the group.</param>
            /// <param name="ParentGroup">An optional parent group.</param>
            /// <param name="Subgroups">Optional subgroups.</param>
            /// <param name="CustomData">Custom data to be stored with this group.</param>
            /// <param name="AttachedFiles">Optional files attached to this group.</param>
            /// 
            /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
            /// <param name="LastChange">The timestamp of the last changes within this group. Can e.g. be used as a HTTP ETag.</param>
            public Builder(TGroupId                    Id,
                           JSONLDContext               JSONLDContext,

                           I18NString                  Name,
                           I18NString?                 Description     = null,
                           IEnumerable<TMembers>?      Members         = null,
                           TGroup?                     ParentGroup     = null,
                           IEnumerable<TGroup>?        Subgroups       = null,
                           JObject?                    CustomData      = null,
                           IEnumerable<AttachedFile>?  AttachedFiles   = null,

                           String?                     DataSource      = null,
                           DateTime?                   LastChange      = null)

                : base(Id,
                       JSONLDContext,
                       LastChange,
                       null,
                       CustomData,
                       null,
                       DataSource)

            {

                this.Name           = Name;
                this.Description    = Description  ?? I18NString.Empty;
                this.Members        = Members.      IsNeitherNullNorEmpty() ? new HashSet<TMembers>    (Members)       : new HashSet<TMembers>();
                this.ParentGroup    = ParentGroup;
                this.Subgroups      = Subgroups.    IsNeitherNullNorEmpty() ? new HashSet<TGroup>      (Subgroups)     : new HashSet<TGroup>();
                this.AttachedFiles  = AttachedFiles.IsNeitherNullNorEmpty() ? new HashSet<AttachedFile>(AttachedFiles) : new HashSet<AttachedFile>();

            }

            #endregion

        }

        #endregion

    }

}
