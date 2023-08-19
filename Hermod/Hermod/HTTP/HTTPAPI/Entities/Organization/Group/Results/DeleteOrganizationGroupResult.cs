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
    /// The result of a delete organization group request.
    /// </summary>
    public class DeleteOrganizationGroupResult : AEnitityResult<IOrganizationGroup, OrganizationGroup_Id>
    {

        #region Properties

        public IOrganizationGroup? OrganizationGroup
            => Entity;

        #endregion

        #region Constructor(s)

        public DeleteOrganizationGroupResult(IOrganizationGroup     OrganizationGroup,
                                             CommandResult          Result,
                                             EventTracking_Id?      EventTrackingId   = null,
                                             IId?                   SenderId          = null,
                                             Object?                Sender            = null,
                                             I18NString?            Description       = null,
                                             IEnumerable<Warning>?  Warnings          = null,
                                             TimeSpan?              Runtime           = null)

            : base(OrganizationGroup,
                   Result,
                   EventTrackingId,
                   SenderId,
                   Sender,
                   Description,
                   Warnings,
                   Runtime)

        { }


        public DeleteOrganizationGroupResult(OrganizationGroup_Id   OrganizationGroupId,
                                             CommandResult          Result,
                                             EventTracking_Id?      EventTrackingId   = null,
                                             IId?                   SenderId          = null,
                                             Object?                Sender            = null,
                                             I18NString?            Description       = null,
                                             IEnumerable<Warning>?  Warnings          = null,
                                             TimeSpan?              Runtime           = null)

            : base(OrganizationGroupId,
                   Result,
                   EventTrackingId,
                   SenderId,
                   Sender,
                   Description,
                   Warnings,
                   Runtime)

        { }

        #endregion


        #region (static) AdminDown      (OrganizationGroup, ...)

        public static DeleteOrganizationGroupResult

            AdminDown(IOrganizationGroup     OrganizationGroup,
                      EventTracking_Id?      EventTrackingId   = null,
                      IId?                   SenderId          = null,
                      Object?                Sender            = null,
                      I18NString?            Description       = null,
                      IEnumerable<Warning>?  Warnings          = null,
                      TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.AdminDown,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) NoOperation    (OrganizationGroup, ...)

        public static DeleteOrganizationGroupResult

            NoOperation(IOrganizationGroup     OrganizationGroup,
                        EventTracking_Id?      EventTrackingId   = null,
                        IId?                   SenderId          = null,
                        Object?                Sender            = null,
                        I18NString?            Description       = null,
                        IEnumerable<Warning>?  Warnings          = null,
                        TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.NoOperation,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) Enqueued       (OrganizationGroup, ...)

        public static DeleteOrganizationGroupResult

            Enqueued(IOrganizationGroup     OrganizationGroup,
                     EventTracking_Id?      EventTrackingId   = null,
                     IId?                   SenderId          = null,
                     Object?                Sender            = null,
                     I18NString?            Description       = null,
                     IEnumerable<Warning>?  Warnings          = null,
                     TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.Enqueued,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Success        (OrganizationGroup, ...)

        public static DeleteOrganizationGroupResult

            Success(IOrganizationGroup     OrganizationGroup,
                    EventTracking_Id?      EventTrackingId   = null,
                    IId?                   SenderId          = null,
                    Object?                Sender            = null,
                    I18NString?            Description       = null,
                    IEnumerable<Warning>?  Warnings          = null,
                    TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.Success,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) CanNotBeRemoved(OrganizationGroup, ...)

        public static DeleteOrganizationGroupResult

            CanNotBeRemoved(IOrganizationGroup     OrganizationGroup,
                            EventTracking_Id?      EventTrackingId   = null,
                            IId?                   SenderId          = null,
                            Object?                Sender            = null,
                            I18NString?            Description       = null,
                            IEnumerable<Warning>?  Warnings          = null,
                            TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.CanNotBeRemoved,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) ArgumentError  (OrganizationGroup,   Description, ...)

        public static DeleteOrganizationGroupResult

            ArgumentError(IOrganizationGroup     OrganizationGroup,
                          I18NString             Description,
                          EventTracking_Id?      EventTrackingId   = null,
                          IId?                   SenderId          = null,
                          Object?                Sender            = null,
                          IEnumerable<Warning>?  Warnings          = null,
                          TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.ArgumentError,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) ArgumentError  (OrganizationGroupId, Description, ...)

        public static DeleteOrganizationGroupResult

            ArgumentError(OrganizationGroup_Id           OrganizationGroupId,
                          I18NString             Description,
                          EventTracking_Id?      EventTrackingId   = null,
                          IId?                   SenderId          = null,
                          Object?                Sender            = null,
                          IEnumerable<Warning>?  Warnings          = null,
                          TimeSpan?              Runtime           = null)

                => new (OrganizationGroupId,
                        CommandResult.ArgumentError,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error          (OrganizationGroup,   Description, ...)

        public static DeleteOrganizationGroupResult

            Error(IOrganizationGroup     OrganizationGroup,
                  I18NString             Description,
                  EventTracking_Id?      EventTrackingId   = null,
                  IId?                   SenderId          = null,
                  Object?                Sender            = null,
                  IEnumerable<Warning>?  Warnings          = null,
                  TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error          (OrganizationGroup,   Exception,   ...)

        public static DeleteOrganizationGroupResult

            Error(IOrganizationGroup     OrganizationGroup,
                  Exception              Exception,
                  EventTracking_Id?      EventTrackingId   = null,
                  IId?                   SenderId          = null,
                  Object?                Sender            = null,
                  IEnumerable<Warning>?  Warnings          = null,
                  TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Exception.Message.ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Timeout        (OrganizationGroup,   Timeout,     ...)

        public static DeleteOrganizationGroupResult

            Timeout(IOrganizationGroup     OrganizationGroup,
                    TimeSpan               Timeout,
                    EventTracking_Id?      EventTrackingId   = null,
                    IId?                   SenderId          = null,
                    Object?                Sender            = null,
                    IEnumerable<Warning>?  Warnings          = null,
                    TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.Timeout,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        $"Timeout after {Timeout.TotalSeconds} seconds!".ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) LockTimeout    (OrganizationGroup,   Timeout,     ...)

        public static DeleteOrganizationGroupResult

            LockTimeout(IOrganizationGroup     OrganizationGroup,
                        TimeSpan               Timeout,
                        EventTracking_Id?      EventTrackingId   = null,
                        IId?                   SenderId          = null,
                        Object?                Sender            = null,
                        IEnumerable<Warning>?  Warnings          = null,
                        TimeSpan?              Runtime           = null)

                => new (OrganizationGroup,
                        CommandResult.LockTimeout,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        $"Lock timeout after {Timeout.TotalSeconds} seconds!".ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion


    }

}
