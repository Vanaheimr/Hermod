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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public enum User2UserEdgeTypes
    {
        gemini,
        follows,
        IsFollowedBy
    }


    public class User2UserEdge : MiniEdge<IUser, User2UserEdgeTypes, IUser>
    {

        /// <summary>
        /// Create a new miniedge.
        /// </summary>
        /// <param name="UserA">The source of the edge.</param>
        /// <param name="EdgeLabel">The label of the edge.</param>
        /// <param name="UserB">The target of the edge</param>
        /// <param name="PrivacyLevel">The level of privacy of this edge.</param>
        /// <param name="Created">The creation timestamp of the miniedge.</param>
        public User2UserEdge(IUser               UserA,
                             User2UserEdgeTypes  EdgeLabel,
                             IUser               UserB,
                             PrivacyLevel        PrivacyLevel  = PrivacyLevel.Private,
                             DateTime?           Created       = null)

            : base(UserA ?? throw new ArgumentNullException(nameof(UserA), "The given user must not be null!"),
                   EdgeLabel,
                   UserB ?? throw new ArgumentNullException(nameof(UserB), "The given user must not be null!"),
                   PrivacyLevel,
                   Created)

        { }

    }

}
