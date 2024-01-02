/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The result of an update API key request.
    /// </summary>
    public class UpdateAPIKeyResult : AEnitityResult<APIKey, APIKey_Id>
    {

        #region Properties

        public APIKey?         APIKey
            => Entity;

        public IOrganization?  Organization    { get; internal set; }

        #endregion

        #region Constructor(s)

        public UpdateAPIKeyResult(APIKey                 APIKey,
                                  CommandResult          Result,
                                  EventTracking_Id?      EventTrackingId   = null,
                                  IId?                   SenderId          = null,
                                  Object?                Sender            = null,
                                  IOrganization?         Organization      = null,
                                  I18NString?            Description       = null,
                                  IEnumerable<Warning>?  Warnings          = null,
                                  TimeSpan?              Runtime           = null)

            : base(APIKey,
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


        public UpdateAPIKeyResult(APIKey_Id              APIKeyId,
                                  CommandResult          Result,
                                  EventTracking_Id?      EventTrackingId   = null,
                                  IId?                   SenderId          = null,
                                  Object?                Sender            = null,
                                  IOrganization?         Organization      = null,
                                  I18NString?            Description       = null,
                                  IEnumerable<Warning>?  Warnings          = null,
                                  TimeSpan?              Runtime           = null)

            : base(APIKeyId,
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


        #region (static) AdminDown    (APIKey, ...)

        public static UpdateAPIKeyResult

            AdminDown(APIKey                 APIKey,
                      EventTracking_Id?      EventTrackingId   = null,
                      IId?                   SenderId          = null,
                      Object?                Sender            = null,
                      IOrganization?         Organization      = null,
                      I18NString?            Description       = null,
                      IEnumerable<Warning>?  Warnings          = null,
                      TimeSpan?              Runtime           = null)

                => new (APIKey,
                        CommandResult.AdminDown,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) NoOperation  (APIKey, ...)

        public static UpdateAPIKeyResult

            NoOperation(APIKey                 APIKey,
                        EventTracking_Id?      EventTrackingId   = null,
                        IId?                   SenderId          = null,
                        Object?                Sender            = null,
                        IOrganization?         Organization      = null,
                        I18NString?            Description       = null,
                        IEnumerable<Warning>?  Warnings          = null,
                        TimeSpan?              Runtime           = null)

                => new (APIKey,
                        CommandResult.NoOperation,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) Enqueued     (APIKey, ...)

        public static UpdateAPIKeyResult

            Enqueued(APIKey                 APIKey,
                     EventTracking_Id?      EventTrackingId   = null,
                     IId?                   SenderId          = null,
                     Object?                Sender            = null,
                     IOrganization?         Organization      = null,
                     I18NString?            Description       = null,
                     IEnumerable<Warning>?  Warnings          = null,
                     TimeSpan?              Runtime           = null)

                => new (APIKey,
                        CommandResult.Enqueued,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Success      (APIKey, ...)

        public static UpdateAPIKeyResult

            Success(APIKey                 APIKey,
                    EventTracking_Id?      EventTrackingId   = null,
                    IId?                   SenderId          = null,
                    Object?                Sender            = null,
                    IOrganization?         Organization      = null,
                    I18NString?            Description       = null,
                    IEnumerable<Warning>?  Warnings          = null,
                    TimeSpan?              Runtime           = null)

                => new (APIKey,
                        CommandResult.Success,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) ArgumentError(APIKey,   Description, ...)

        public static UpdateAPIKeyResult

            ArgumentError(APIKey                 APIKey,
                          I18NString             Description,
                          EventTracking_Id?      EventTrackingId   = null,
                          IId?                   SenderId          = null,
                          Object?                Sender            = null,
                          IOrganization?         Organization      = null,
                          IEnumerable<Warning>?  Warnings          = null,
                          TimeSpan?              Runtime           = null)

                => new (APIKey,
                        CommandResult.ArgumentError,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) ArgumentError(APIKeyId, Description, ...)

        public static UpdateAPIKeyResult

            ArgumentError(APIKey_Id              APIKeyId,
                          I18NString             Description,
                          EventTracking_Id?      EventTrackingId   = null,
                          IId?                   SenderId          = null,
                          Object?                Sender            = null,
                          IOrganization?         Organization      = null,
                          IEnumerable<Warning>?  Warnings          = null,
                          TimeSpan?              Runtime           = null)

                => new (APIKeyId,
                        CommandResult.ArgumentError,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error        (APIKey,   Description, ...)

        public static UpdateAPIKeyResult

            Error(APIKey                 APIKey,
                  I18NString             Description,
                  EventTracking_Id?      EventTrackingId   = null,
                  IId?                   SenderId          = null,
                  Object?                Sender            = null,
                  IOrganization?         Organization      = null,
                  IEnumerable<Warning>?  Warnings          = null,
                  TimeSpan?              Runtime           = null)

                => new (APIKey,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error        (APIKey,   Exception,   ...)

        public static UpdateAPIKeyResult

            Error(APIKey                 APIKey,
                  Exception              Exception,
                  EventTracking_Id?      EventTrackingId   = null,
                  IId?                   SenderId          = null,
                  Object?                Sender            = null,
                  IOrganization?         Organization      = null,
                  IEnumerable<Warning>?  Warnings          = null,
                  TimeSpan?              Runtime           = null)

                => new (APIKey,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        Exception.Message.ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Timeout      (APIKey,   Timeout,     ...)

        public static UpdateAPIKeyResult

            Timeout(APIKey                 APIKey,
                    TimeSpan               Timeout,
                    EventTracking_Id?      EventTrackingId   = null,
                    IId?                   SenderId          = null,
                    Object?                Sender            = null,
                    IOrganization?         Organization      = null,
                    IEnumerable<Warning>?  Warnings          = null,
                    TimeSpan?              Runtime           = null)

                => new (APIKey,
                        CommandResult.Timeout,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        Organization,
                        $"Timeout after {Timeout.TotalSeconds} seconds!".ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) LockTimeout  (APIKey,   Timeout,     ...)

        public static UpdateAPIKeyResult

            LockTimeout(APIKey                 APIKey,
                        TimeSpan               Timeout,
                        EventTracking_Id?      EventTrackingId   = null,
                        IId?                   SenderId          = null,
                        Object?                Sender            = null,
                        IOrganization?         Organization      = null,
                        IEnumerable<Warning>?  Warnings          = null,
                        TimeSpan?              Runtime           = null)

                => new (APIKey,
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
