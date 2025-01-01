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


#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    [Flags]
    public enum Organization2OrganizationEdgeLabel
    {
        IsSubsidary,
        IsParent,
        IsChildOf,
    }


    public class Organization2OrganizationEdge : MiniEdge<IOrganization, Organization2OrganizationEdgeLabel, IOrganization>
    {

        /// <summary>
        /// Create a new miniedge.
        /// </summary>
        /// <param name="OrganizationA">The source of the edge.</param>
        /// <param name="EdgeLabel">The label of the edge.</param>
        /// <param name="OrganizationB">The target of the edge</param>
        /// <param name="PrivacyLevel">The level of privacy of this edge.</param>
        /// <param name="Created">The creation timestamp of the miniedge.</param>
        public Organization2OrganizationEdge(IOrganization                       OrganizationA,
                                             Organization2OrganizationEdgeLabel  EdgeLabel,
                                             IOrganization                       OrganizationB,
                                             PrivacyLevel                        PrivacyLevel  = PrivacyLevel.Private,
                                             DateTime?                           Created       = null)

            : base(OrganizationA ?? throw new ArgumentNullException(nameof(OrganizationA), "The given user must not be null!"),
                   EdgeLabel,
                   OrganizationB ?? throw new ArgumentNullException(nameof(OrganizationB), "The given user must not be null!"),
                   PrivacyLevel,
                   Created)

        { }

    }

}
