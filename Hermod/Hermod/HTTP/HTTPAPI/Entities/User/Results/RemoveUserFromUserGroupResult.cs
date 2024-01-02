/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public class RemoveUserFromUserGroupResult : AResult<User, UserGroup>
    {

        public User                      User
            => Object1;

        public User2UserGroupEdgeLabel?  EdgeLabel    { get; }

        public UserGroup                 UserGroup
            => Object2;


        public RemoveUserFromUserGroupResult(User                      User,
                                             User2UserGroupEdgeLabel?  EdgeLabel,
                                             UserGroup                 UserGroup,
                                             EventTracking_Id          EventTrackingId,
                                             Boolean                   IsSuccess,
                                             String?                   Argument           = null,
                                             I18NString?               ErrorDescription   = null)

            : base(User,
                   UserGroup,
                   EventTrackingId,
                   IsSuccess,
                   Argument,
                   ErrorDescription)

        {

            this.EdgeLabel = EdgeLabel;

        }


        public static RemoveUserFromUserGroupResult Success(User              User,
                                                            UserGroup         UserGroup,
                                                            EventTracking_Id  EventTrackingId)

            => new (User,
                    null,
                    UserGroup,
                    EventTrackingId,
                    true);

        public static RemoveUserFromUserGroupResult Success(User                     User,
                                                            User2UserGroupEdgeLabel  EdgeLabel,
                                                            UserGroup                UserGroup,
                                                            EventTracking_Id         EventTrackingId)

            => new (User,
                    EdgeLabel,
                    UserGroup,
                    EventTrackingId,
                    true);


        public static RemoveUserFromUserGroupResult ArgumentError(User              User,
                                                                  UserGroup         UserGroup,
                                                                  EventTracking_Id  EventTrackingId,
                                                                  String            Argument,
                                                                  String            Description)

            => new (User,
                    null,
                    UserGroup,
                    EventTrackingId,
                    false,
                    Argument,
                    I18NString.Create(
                        Languages.en,
                        Description
                    ));

        public static RemoveUserFromUserGroupResult ArgumentError(User                     User,
                                                                  User2UserGroupEdgeLabel  EdgeLabel,
                                                                  UserGroup                UserGroup,
                                                                  EventTracking_Id         EventTrackingId,
                                                                  String                   Argument,
                                                                  String                   Description)

            => new (User,
                    EdgeLabel,
                    UserGroup,
                    EventTrackingId,
                    false,
                    Argument,
                    I18NString.Create(
                        Languages.en,
                        Description
                    ));

        public static RemoveUserFromUserGroupResult ArgumentError(User              User,
                                                                  UserGroup         UserGroup,
                                                                  EventTracking_Id  EventTrackingId,
                                                                  String            Argument,
                                                                  I18NString        Description)

            => new (User,
                    null,
                    UserGroup,
                    EventTrackingId,
                    false,
                    Argument,
                    Description);

        public static RemoveUserFromUserGroupResult ArgumentError(User                     User,
                                                                  User2UserGroupEdgeLabel  EdgeLabel,
                                                                  UserGroup                UserGroup,
                                                                  EventTracking_Id         EventTrackingId,
                                                                  String                   Argument,
                                                                  I18NString               Description)

            => new (User,
                    EdgeLabel,
                    UserGroup,
                    EventTrackingId,
                    false,
                    Argument,
                    Description);


        public static RemoveUserFromUserGroupResult Failed(User              User,
                                                           UserGroup         UserGroup,
                                                           EventTracking_Id  EventTrackingId,
                                                           String            Description)

            => new (User,
                    null,
                    UserGroup,
                    EventTrackingId,
                    false,
                    null,
                    I18NString.Create(
                        Languages.en,
                        Description
                    ));

        public static RemoveUserFromUserGroupResult Failed(User                     User,
                                                           User2UserGroupEdgeLabel  EdgeLabel,
                                                           UserGroup                UserGroup,
                                                           EventTracking_Id         EventTrackingId,
                                                           String                   Description)

            => new (User,
                    EdgeLabel,
                    UserGroup,
                    EventTrackingId,
                    false,
                    null,
                    I18NString.Create(
                        Languages.en,
                        Description
                    ));

        public static RemoveUserFromUserGroupResult Failed(User              User,
                                                           UserGroup         UserGroup,
                                                           EventTracking_Id  EventTrackingId,
                                                           I18NString        Description)

            => new (User,
                    null,
                    UserGroup,
                    EventTrackingId,
                    false,
                    null,
                    Description);

        public static RemoveUserFromUserGroupResult Failed(User                     User,
                                                           User2UserGroupEdgeLabel  EdgeLabel,
                                                           UserGroup                UserGroup,
                                                           EventTracking_Id         EventTrackingId,
                                                           I18NString               Description)

            => new (User,
                    EdgeLabel,
                    UserGroup,
                    EventTrackingId,
                    false,
                    null,
                    Description);

        public static RemoveUserFromUserGroupResult Failed(User              User,
                                                           UserGroup         UserGroup,
                                                           EventTracking_Id  EventTrackingId,
                                                           Exception         Exception)

            => new (User,
                    null,
                    UserGroup,
                    EventTrackingId,
                    false,
                    null,
                    I18NString.Create(
                        Languages.en,
                        Exception.Message
                    ));

        public static RemoveUserFromUserGroupResult Failed(User                     User,
                                                           User2UserGroupEdgeLabel  EdgeLabel,
                                                           UserGroup                UserGroup,
                                                           EventTracking_Id         EventTrackingId,
                                                           Exception                Exception)

            => new (User,
                    EdgeLabel,
                    UserGroup,
                    EventTrackingId,
                    false,
                    null,
                    I18NString.Create(
                        Languages.en,
                        Exception.Message
                    ));

    }

}
