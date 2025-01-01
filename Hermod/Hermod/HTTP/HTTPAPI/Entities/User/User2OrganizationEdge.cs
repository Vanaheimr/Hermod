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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    [Flags]
    public enum User2OrganizationEdgeLabel
    {
        IsAdmin,
        IsAdminReadOnly,
        IsMember,
        IsGuest,
        follows,
        IsFollowedBy
    }


    public class User2OrganizationEdge : MiniEdge<IUser, User2OrganizationEdgeLabel, IOrganization>
    {

        /// <summary>
        /// Create a new miniedge.
        /// </summary>
        /// <param name="User">The source of the edge.</param>
        /// <param name="EdgeLabel">The label of the edge.</param>
        /// <param name="Organization">The target of the edge</param>
        /// <param name="PrivacyLevel">The level of privacy of this edge.</param>
        /// <param name="Created">The creation timestamp of the miniedge.</param>
        public User2OrganizationEdge(IUser                       User,
                                     User2OrganizationEdgeLabel  EdgeLabel,
                                     IOrganization               Organization,
                                     PrivacyLevel                PrivacyLevel  = PrivacyLevel.Private,
                                     DateTime?                   Created       = null)

            : base(User         ?? throw new ArgumentNullException(nameof(User),         "The given user must not be null!"),
                   EdgeLabel,
                   Organization ?? throw new ArgumentNullException(nameof(Organization), "The given organization must not be null!"),
                   PrivacyLevel,
                   Created)

        { }

    }



}
