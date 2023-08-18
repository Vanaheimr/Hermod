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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The result of an update user request.
    /// </summary>
    public class UpdateUserResult : AEnitityResult<IUser, User_Id>
    {

        #region Properties

        public IUser?          User
            => Object;

        public IOrganization?  Organization    { get; internal set; }

        #endregion

        #region Constructor(s)

        public UpdateUserResult(IUser                  User,
                                CommandResult          Result,
                                EventTracking_Id?      EventTrackingId   = null,
                                IId?                   SenderId          = null,
                                Object?                Sender            = null,
                                IOrganization?         Organization      = null,
                                I18NString?            Description       = null,
                                IEnumerable<Warning>?  Warnings          = null,
                                TimeSpan?              Runtime           = null)

            : base(User,
                   Result,
                   EventTrackingId,
                   SenderId,
                   Sender,
                   Description,
                   Warnings,
                   Runtime)

        {

            this.Organization = Organization;

        }


        public UpdateUserResult(User_Id                UserId,
                                CommandResult          Result,
                                EventTracking_Id?      EventTrackingId   = null,
                                IId?                   SenderId          = null,
                                Object?                Sender            = null,
                                IOrganization?         Organization      = null,
                                I18NString?            Description       = null,
                                IEnumerable<Warning>?  Warnings          = null,
                                TimeSpan?              Runtime           = null)

            : base(UserId,
                   Result,
                   EventTrackingId,
                   SenderId,
                   Sender,
                   Description,
                   Warnings,
                   Runtime)

        {

            this.Organization = Organization;

        }

        #endregion


        #region (static) AdminDown    (User, ...)

        public static UpdateUserResult

            AdminDown(IUser                  User,
                      EventTracking_Id?      EventTrackingId      = null,
                      IId?                   SenderId             = null,
                      Object?                Sender               = null,
                      IOrganization?         Organization         = null,
                      I18NString?            Description          = null,
                      IEnumerable<Warning>?  Warnings             = null,
                      TimeSpan?              Runtime              = null)

                => new (User,
                        CommandResult.AdminDown,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) NoOperation  (User, ...)

        public static UpdateUserResult

            NoOperation(IUser                  User,
                        EventTracking_Id?      EventTrackingId      = null,
                        IId?                   SenderId             = null,
                        Object?                Sender               = null,
                        IOrganization?         Organization         = null,
                        I18NString?            Description          = null,
                        IEnumerable<Warning>?  Warnings             = null,
                        TimeSpan?              Runtime              = null)

                => new (User,
                        CommandResult.NoOperation,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) Enqueued     (User, ...)

        public static UpdateUserResult

            Enqueued(IUser                  User,
                     EventTracking_Id?      EventTrackingId      = null,
                     IId?                   SenderId             = null,
                     Object?                Sender               = null,
                     IOrganization?         Organization         = null,
                     I18NString?            Description          = null,
                     IEnumerable<Warning>?  Warnings             = null,
                     TimeSpan?              Runtime              = null)

                => new (User,
                        CommandResult.Enqueued,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Success      (User, ...)

        public static UpdateUserResult

            Success(IUser                  User,
                    EventTracking_Id?      EventTrackingId      = null,
                    IId?                   SenderId             = null,
                    Object?                Sender               = null,
                    IOrganization?         Organization         = null,
                    I18NString?            Description          = null,
                    IEnumerable<Warning>?  Warnings             = null,
                    TimeSpan?              Runtime              = null)

                => new (User,
                        CommandResult.Success,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) ArgumentError(User,   Description, ...)

        public static UpdateUserResult

            ArgumentError(IUser                  User,
                          I18NString             Description,
                          EventTracking_Id?      EventTrackingId      = null,
                          IId?                   SenderId             = null,
                          Object?                Sender               = null,
                          IOrganization?         Organization         = null,
                          IEnumerable<Warning>?  Warnings             = null,
                          TimeSpan?              Runtime              = null)

                => new (User,
                        CommandResult.ArgumentError,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) ArgumentError(UserId, Description, ...)

        public static UpdateUserResult

            ArgumentError(User_Id                UserId,
                          I18NString             Description,
                          EventTracking_Id?      EventTrackingId      = null,
                          IId?                   SenderId             = null,
                          Object?                Sender               = null,
                          IOrganization?         Organization         = null,
                          IEnumerable<Warning>?  Warnings             = null,
                          TimeSpan?              Runtime              = null)

                => new (UserId,
                        CommandResult.ArgumentError,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error        (User,   Description, ...)

        public static UpdateUserResult

            Error(IUser                  User,
                  I18NString             Description,
                  EventTracking_Id?      EventTrackingId      = null,
                  IId?                   SenderId             = null,
                  Object?                Sender               = null,
                  IOrganization?         Organization         = null,
                  IEnumerable<Warning>?  Warnings             = null,
                  TimeSpan?              Runtime              = null)

                => new (User,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error        (User,   Exception,   ...)

        public static UpdateUserResult

            Error(IUser                  User,
                  Exception              Exception,
                  EventTracking_Id?      EventTrackingId      = null,
                  IId?                   SenderId             = null,
                  Object?                Sender               = null,
                  IOrganization?         Organization         = null,
                  IEnumerable<Warning>?  Warnings             = null,
                  TimeSpan?              Runtime              = null)

                => new (User,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Exception.Message.ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Timeout      (User,   Timeout,     ...)

        public static UpdateUserResult

            Timeout(IUser                  User,
                    TimeSpan               Timeout,
                    EventTracking_Id?      EventTrackingId      = null,
                    IId?                   SenderId             = null,
                    Object?                Sender               = null,
                    IOrganization?         Organization         = null,
                    IEnumerable<Warning>?  Warnings             = null,
                    TimeSpan?              Runtime              = null)

                => new (User,
                        CommandResult.Timeout,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        $"Timeout after {Timeout.TotalSeconds} seconds!".ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) LockTimeout  (User,   Timeout,     ...)

        public static UpdateUserResult

            LockTimeout(IUser                  User,
                        TimeSpan               Timeout,
                        EventTracking_Id?      EventTrackingId      = null,
                        IId?                   SenderId             = null,
                        Object?                Sender               = null,
                        IOrganization?         Organization         = null,
                        IEnumerable<Warning>?  Warnings             = null,
                        TimeSpan?              Runtime              = null)

                => new (User,
                        CommandResult.LockTimeout,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        $"Lock timeout after {Timeout.TotalSeconds} seconds!".ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion


    }

}
