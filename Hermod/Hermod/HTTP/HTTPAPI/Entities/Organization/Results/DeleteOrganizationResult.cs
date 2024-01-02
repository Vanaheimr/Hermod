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

    /// <summary>
    /// The result of a delete organization request.
    /// </summary>
    public class DeleteOrganizationResult : AEnitityResult<IOrganization, Organization_Id>
    {

        #region Properties

        public IOrganization?  Organization
            => Entity;

        public IOrganization?  ParentOrganization    { get; internal set; }

        #endregion

        #region Constructor(s)

        public DeleteOrganizationResult(IOrganization          Organization,
                                        CommandResult          Result,
                                        EventTracking_Id?      EventTrackingId      = null,
                                        IId?                   SenderId             = null,
                                        Object?                Sender               = null,
                                        IOrganization?         ParentOrganization   = null,
                                        I18NString?            Description          = null,
                                        IEnumerable<Warning>?  Warnings             = null,
                                        TimeSpan?              Runtime              = null)

            : base(Organization,
                   Result,
                   EventTrackingId,
                   SenderId,
                   Sender,
                   Description,
                   Warnings,
                   Runtime)

        {

            this.ParentOrganization = ParentOrganization;

        }


        public DeleteOrganizationResult(Organization_Id        Id,
                                        CommandResult          Result,
                                        EventTracking_Id?      EventTrackingId      = null,
                                        IId?                   SenderId             = null,
                                        Object?                Sender               = null,
                                        IOrganization?         ParentOrganization   = null,
                                        I18NString?            Description          = null,
                                        IEnumerable<Warning>?  Warnings             = null,
                                        TimeSpan?              Runtime              = null)

            : base(Id,
                   Result,
                   EventTrackingId,
                   SenderId,
                   Sender,
                   Description,
                   Warnings,
                   Runtime)

        {

            this.ParentOrganization = ParentOrganization;

        }

        #endregion


        #region (static) AdminDown      (Organization, ...)

        public static DeleteOrganizationResult

            AdminDown(IOrganization          Organization,
                      EventTracking_Id?      EventTrackingId      = null,
                      IId?                   SenderId             = null,
                      Object?                Sender               = null,
                      IOrganization?         ParentOrganization   = null,
                      I18NString?            Description          = null,
                      IEnumerable<Warning>?  Warnings             = null,
                      TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.AdminDown,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) NoOperation    (Organization, ...)

        public static DeleteOrganizationResult

            NoOperation(IOrganization          Organization,
                        EventTracking_Id?      EventTrackingId      = null,
                        IId?                   SenderId             = null,
                        Object?                Sender               = null,
                        IOrganization?         ParentOrganization   = null,
                        I18NString?            Description          = null,
                        IEnumerable<Warning>?  Warnings             = null,
                        TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.NoOperation,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) Enqueued       (Organization, ...)

        public static DeleteOrganizationResult

            Enqueued(IOrganization          Organization,
                     EventTracking_Id?      EventTrackingId      = null,
                     IId?                   SenderId             = null,
                     Object?                Sender               = null,
                     IOrganization?         ParentOrganization   = null,
                     I18NString?            Description          = null,
                     IEnumerable<Warning>?  Warnings             = null,
                     TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.Enqueued,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Success        (Organization, ...)

        public static DeleteOrganizationResult

            Success(IOrganization          Organization,
                    EventTracking_Id?      EventTrackingId      = null,
                    IId?                   SenderId             = null,
                    Object?                Sender               = null,
                    IOrganization?         ParentOrganization   = null,
                    I18NString?            Description          = null,
                    IEnumerable<Warning>?  Warnings             = null,
                    TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.Success,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) CanNotBeRemoved(Organization, ...)

        public static DeleteOrganizationResult

            CanNotBeRemoved(IOrganization          Organization,
                            EventTracking_Id?      EventTrackingId      = null,
                            IId?                   SenderId             = null,
                            Object?                Sender               = null,
                            IOrganization?         ParentOrganization   = null,
                            I18NString?            Description          = null,
                            IEnumerable<Warning>?  Warnings             = null,
                            TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.CanNotBeRemoved,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) ArgumentError  (Organization,   Description, ...)

        public static DeleteOrganizationResult

            ArgumentError(IOrganization          Organization,
                          I18NString             Description,
                          EventTracking_Id?      EventTrackingId      = null,
                          IId?                   SenderId             = null,
                          Object?                Sender               = null,
                          IOrganization?         ParentOrganization   = null,
                          IEnumerable<Warning>?  Warnings             = null,
                          TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.ArgumentError,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) ArgumentError  (OrganizationId, Description, ...)

        public static DeleteOrganizationResult

            ArgumentError(Organization_Id        OrganizationId,
                          I18NString             Description,
                          EventTracking_Id?      EventTrackingId      = null,
                          IId?                   SenderId             = null,
                          Object?                Sender               = null,
                          IOrganization?         ParentOrganization   = null,
                          IEnumerable<Warning>?  Warnings             = null,
                          TimeSpan?              Runtime              = null)

                => new (OrganizationId,
                        CommandResult.ArgumentError,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error          (Organization,   Description, ...)

        public static DeleteOrganizationResult

            Error(IOrganization          Organization,
                  I18NString             Description,
                  EventTracking_Id?      EventTrackingId      = null,
                  IId?                   SenderId             = null,
                  Object?                Sender               = null,
                  IOrganization?         ParentOrganization   = null,
                  IEnumerable<Warning>?  Warnings             = null,
                  TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error          (Organization,   Exception,   ...)

        public static DeleteOrganizationResult

            Error(IOrganization          Organization,
                  Exception              Exception,
                  EventTracking_Id?      EventTrackingId      = null,
                  IId?                   SenderId             = null,
                  Object?                Sender               = null,
                  IOrganization?         ParentOrganization   = null,
                  IEnumerable<Warning>?  Warnings             = null,
                  TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        Exception.Message.ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Timeout        (Organization,   Timeout,     ...)

        public static DeleteOrganizationResult

            Timeout(IOrganization          Organization,
                    TimeSpan               Timeout,
                    EventTracking_Id?      EventTrackingId      = null,
                    IId?                   SenderId             = null,
                    Object?                Sender               = null,
                    IOrganization?         ParentOrganization   = null,
                    IEnumerable<Warning>?  Warnings             = null,
                    TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.Timeout,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        $"Timeout after {Timeout.TotalSeconds} seconds!".ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) LockTimeout    (Organization,   Timeout,     ...)

        public static DeleteOrganizationResult

            LockTimeout(IOrganization          Organization,
                        TimeSpan               Timeout,
                        EventTracking_Id?      EventTrackingId      = null,
                        IId?                   SenderId             = null,
                        Object?                Sender               = null,
                        IOrganization?         ParentOrganization   = null,
                        IEnumerable<Warning>?  Warnings             = null,
                        TimeSpan?              Runtime              = null)

                => new (Organization,
                        CommandResult.LockTimeout,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        ParentOrganization,
                        $"Lock timeout after {Timeout.TotalSeconds} seconds!".ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion


    }

}
