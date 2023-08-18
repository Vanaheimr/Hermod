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
    /// The result of a reset password request.
    /// </summary>
    public class ResetPasswordResult : AResult<IEnumerable<IUser>>
    {

        #region Properties

        public IEnumerable<IUser>  Users
            => Object ?? Array.Empty<IUser>();

        public PasswordReset?      PasswordReset    { get; internal set; }

        #endregion

        #region Constructor(s)

        public ResetPasswordResult(IEnumerable<IUser>     Users,
                                   CommandResult          Result,
                                   EventTracking_Id?      EventTrackingId   = null,
                                   IId?                   SenderId          = null,
                                   Object?                Sender            = null,
                                   PasswordReset?         PasswordReset     = null,
                                   I18NString?            Description       = null,
                                   IEnumerable<Warning>?  Warnings          = null,
                                   TimeSpan?              Runtime           = null)

            : base(Users,
                   Result,
                   EventTrackingId,
                   SenderId,
                   Sender,
                   Description,
                   Warnings,
                   Runtime)

        {

            this.PasswordReset = PasswordReset;

        }

        #endregion


        #region (static) AdminDown    (User, ...)

        public static ResetPasswordResult

            AdminDown(IEnumerable<IUser>     Users,
                      EventTracking_Id?      EventTrackingId      = null,
                      IId?                   SenderId             = null,
                      Object?                Sender               = null,
                      PasswordReset?         PasswordReset        = null,
                      I18NString?            Description          = null,
                      IEnumerable<Warning>?  Warnings             = null,
                      TimeSpan?              Runtime              = null)

                => new (Users,
                        CommandResult.AdminDown,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        PasswordReset,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) NoOperation  (User, ...)

        public static ResetPasswordResult

            NoOperation(IEnumerable<IUser>     Users,
                        EventTracking_Id?      EventTrackingId      = null,
                        IId?                   SenderId             = null,
                        Object?                Sender               = null,
                        PasswordReset?         PasswordReset        = null,
                        I18NString?            Description          = null,
                        IEnumerable<Warning>?  Warnings             = null,
                        TimeSpan?              Runtime              = null)

                => new (Users,
                        CommandResult.NoOperation,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        PasswordReset,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) Enqueued     (User, ...)

        public static ResetPasswordResult

            Enqueued(IEnumerable<IUser>     Users,
                     EventTracking_Id?      EventTrackingId      = null,
                     IId?                   SenderId             = null,
                     Object?                Sender               = null,
                     PasswordReset?         PasswordReset        = null,
                     I18NString?            Description          = null,
                     IEnumerable<Warning>?  Warnings             = null,
                     TimeSpan?              Runtime              = null)

                => new (Users,
                        CommandResult.Enqueued,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        PasswordReset,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Success      (User, ...)

        public static ResetPasswordResult

            Success(IEnumerable<IUser>     Users,
                    EventTracking_Id?      EventTrackingId      = null,
                    IId?                   SenderId             = null,
                    Object?                Sender               = null,
                    PasswordReset?         PasswordReset        = null,
                    I18NString?            Description          = null,
                    IEnumerable<Warning>?  Warnings             = null,
                    TimeSpan?              Runtime              = null)

                => new (Users,
                        CommandResult.Success,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        PasswordReset,
                        Description,
                        Warnings,
                        Runtime);

        #endregion


        #region (static) ArgumentError(User, Description, ...)

        public static ResetPasswordResult

            ArgumentError(IEnumerable<IUser>     Users,
                          I18NString             Description,
                          EventTracking_Id?      EventTrackingId      = null,
                          IId?                   SenderId             = null,
                          Object?                Sender               = null,
                          PasswordReset?         PasswordReset        = null,
                          IEnumerable<Warning>?  Warnings             = null,
                          TimeSpan?              Runtime              = null)

                => new (Users,
                        CommandResult.ArgumentError,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        PasswordReset,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error        (User, Description, ...)

        public static ResetPasswordResult

            Error(IEnumerable<IUser>     Users,
                  I18NString             Description,
                  EventTracking_Id?      EventTrackingId      = null,
                  IId?                   SenderId             = null,
                  Object?                Sender               = null,
                  PasswordReset?         PasswordReset        = null,
                  IEnumerable<Warning>?  Warnings             = null,
                  TimeSpan?              Runtime              = null)

                => new (Users,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        PasswordReset,
                        Description,
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Error        (User, Exception,   ...)

        public static ResetPasswordResult

            Error(IEnumerable<IUser>     Users,
                  Exception              Exception,
                  EventTracking_Id?      EventTrackingId      = null,
                  IId?                   SenderId             = null,
                  Object?                Sender               = null,
                  PasswordReset?         PasswordReset        = null,
                  IEnumerable<Warning>?  Warnings             = null,
                  TimeSpan?              Runtime              = null)

                => new (Users,
                        CommandResult.Error,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        PasswordReset,
                        Exception.Message.ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) Timeout      (User, Timeout,     ...)

        public static ResetPasswordResult

            Timeout(IEnumerable<IUser>     Users,
                    TimeSpan               Timeout,
                    EventTracking_Id?      EventTrackingId      = null,
                    IId?                   SenderId             = null,
                    Object?                Sender               = null,
                    PasswordReset?         PasswordReset        = null,
                    IEnumerable<Warning>?  Warnings             = null,
                    TimeSpan?              Runtime              = null)

                => new (Users,
                        CommandResult.Timeout,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        PasswordReset,
                        $"Timeout after {Timeout.TotalSeconds} seconds!".ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion

        #region (static) LockTimeout  (User, Timeout,     ...)

        public static ResetPasswordResult

            LockTimeout(IEnumerable<IUser>     Users,
                        TimeSpan               Timeout,
                        EventTracking_Id?      EventTrackingId      = null,
                        IId?                   SenderId             = null,
                        Object?                Sender               = null,
                        PasswordReset?         PasswordReset        = null,
                        IEnumerable<Warning>?  Warnings             = null,
                        TimeSpan?              Runtime              = null)

                => new (Users,
                        CommandResult.LockTimeout,
                        EventTrackingId,
                        SenderId,
                        Sender,
                        PasswordReset,
                        $"Lock timeout after {Timeout.TotalSeconds} seconds!".ToI18NString(),
                        Warnings,
                        Runtime);

        #endregion


    }

}
