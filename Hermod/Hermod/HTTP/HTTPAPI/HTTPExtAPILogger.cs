/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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

using System;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The Users API logger.
    /// </summary>
    public class HTTPExtAPILogger : HTTPServerLogger
    {

        #region Data

        /// <summary>
        /// The default context of this logger.
        /// </summary>
        public const String DefaultContext = "HTTPExtAPI";

        #endregion

        #region Properties

        /// <summary>
        /// The linked HTTPExtAPI.
        /// </summary>
        public HTTPExtAPI  HTTPExtAPI  { get; }

        #endregion

        #region Constructor(s)

        #region HTTPExtAPILogger(HTTPExtAPI, Context = DefaultContext, LogfileCreator = null)

        /// <summary>
        /// Create a new HTTPExtAPI logger using the default logging delegates.
        /// </summary>
        /// <param name="HTTPExtAPI">A HTTPExtAPI.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="Context">A context of this API.</param>
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        public HTTPExtAPILogger(HTTPExtAPI                HTTPExtAPI,
                              String                  LoggingPath,
                              String                  Context         = DefaultContext,
                              LogfileCreatorDelegate  LogfileCreator  = null)

            : this(HTTPExtAPI,
                   LoggingPath,
                   Context,
                   null,
                   null,
                   null,
                   null,
                   LogfileCreator: LogfileCreator)

        { }

        #endregion

        #region HTTPExtAPILogger(HTTPExtAPI, Context, ... Logging delegates ...)

        /// <summary>
        /// Create a new HTTPExtAPI logger using the given logging delegates.
        /// </summary>
        /// <param name="HTTPExtAPI">A HTTPExtAPI.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="Context">A context of this API.</param>
        /// 
        /// <param name="LogHTTPRequest_toConsole">A delegate to log incoming HTTP requests to console.</param>
        /// <param name="LogHTTPResponse_toConsole">A delegate to log HTTP requests/responses to console.</param>
        /// <param name="LogHTTPRequest_toDisc">A delegate to log incoming HTTP requests to disc.</param>
        /// <param name="LogHTTPResponse_toDisc">A delegate to log HTTP requests/responses to disc.</param>
        /// 
        /// <param name="LogHTTPRequest_toNetwork">A delegate to log incoming HTTP requests to a network target.</param>
        /// <param name="LogHTTPResponse_toNetwork">A delegate to log HTTP requests/responses to a network target.</param>
        /// <param name="LogHTTPRequest_toHTTPSSE">A delegate to log incoming HTTP requests to a HTTP server sent events source.</param>
        /// <param name="LogHTTPResponse_toHTTPSSE">A delegate to log HTTP requests/responses to a HTTP server sent events source.</param>
        /// 
        /// <param name="LogHTTPError_toConsole">A delegate to log HTTP errors to console.</param>
        /// <param name="LogHTTPError_toDisc">A delegate to log HTTP errors to disc.</param>
        /// <param name="LogHTTPError_toNetwork">A delegate to log HTTP errors to a network target.</param>
        /// <param name="LogHTTPError_toHTTPSSE">A delegate to log HTTP errors to a HTTP server sent events source.</param>
        /// 
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        public HTTPExtAPILogger(HTTPExtAPI                     HTTPExtAPI,
                              String                       LoggingPath,
                              String                       Context,

                              HTTPRequestLoggerDelegate?   LogHTTPRequest_toConsole    = null,
                              HTTPResponseLoggerDelegate?  LogHTTPResponse_toConsole   = null,
                              HTTPRequestLoggerDelegate?   LogHTTPRequest_toDisc       = null,
                              HTTPResponseLoggerDelegate?  LogHTTPResponse_toDisc      = null,

                              HTTPRequestLoggerDelegate?   LogHTTPRequest_toNetwork    = null,
                              HTTPResponseLoggerDelegate?  LogHTTPResponse_toNetwork   = null,
                              HTTPRequestLoggerDelegate?   LogHTTPRequest_toHTTPSSE    = null,
                              HTTPResponseLoggerDelegate?  LogHTTPResponse_toHTTPSSE   = null,

                              HTTPResponseLoggerDelegate?  LogHTTPError_toConsole      = null,
                              HTTPResponseLoggerDelegate?  LogHTTPError_toDisc         = null,
                              HTTPResponseLoggerDelegate?  LogHTTPError_toNetwork      = null,
                              HTTPResponseLoggerDelegate?  LogHTTPError_toHTTPSSE      = null,

                              LogfileCreatorDelegate?      LogfileCreator              = null)

            : base(HTTPExtAPI.HTTPServer,
                   LoggingPath,
                   Context,

                   LogHTTPRequest_toConsole,
                   LogHTTPResponse_toConsole,
                   LogHTTPRequest_toDisc,
                   LogHTTPResponse_toDisc,

                   LogHTTPRequest_toNetwork,
                   LogHTTPResponse_toNetwork,
                   LogHTTPRequest_toHTTPSSE,
                   LogHTTPResponse_toHTTPSSE,

                   LogHTTPError_toConsole,
                   LogHTTPError_toDisc,
                   LogHTTPError_toNetwork,
                   LogHTTPError_toHTTPSSE,

                   LogfileCreator)

        {

            this.HTTPExtAPI = HTTPExtAPI ?? throw new ArgumentNullException(nameof(HTTPExtAPI), "The given HTTPExtAPI must not be null!");

            #region Users

            RegisterEvent2("AddUserRequest",
                           handler => HTTPExtAPI.OnAddUserHTTPRequest += handler,
                           handler => HTTPExtAPI.OnAddUserHTTPRequest -= handler,
                           "User", "Request",  "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("AddUserResponse",
                           handler => HTTPExtAPI.OnAddUserHTTPResponse += handler,
                           handler => HTTPExtAPI.OnAddUserHTTPResponse -= handler,
                           "User", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("SetUserRequest",
                           handler => HTTPExtAPI.OnSetUserHTTPRequest += handler,
                           handler => HTTPExtAPI.OnSetUserHTTPRequest -= handler,
                           "User", "Request",  "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("SetUserResponse",
                           handler => HTTPExtAPI.OnSetUserHTTPResponse += handler,
                           handler => HTTPExtAPI.OnSetUserHTTPResponse -= handler,
                           "User", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("ChangePasswordRequest",
                           handler => HTTPExtAPI.OnChangePasswordRequest += handler,
                           handler => HTTPExtAPI.OnChangePasswordRequest -= handler,
                           "User", "ChangePassword", "Password", "Request", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("ChangePasswordResponse",
                           handler => HTTPExtAPI.OnChangePasswordResponse += handler,
                           handler => HTTPExtAPI.OnChangePasswordResponse -= handler,
                           "User", "ChangePassword", "Password", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("ImpersonateUserRequest",
                           handler => HTTPExtAPI.OnImpersonateUserRequest += handler,
                           handler => HTTPExtAPI.OnImpersonateUserRequest -= handler,
                           "User", "Impersonate", "Request", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("ImpersonateUserResponse",
                           handler => HTTPExtAPI.OnImpersonateUserResponse += handler,
                           handler => HTTPExtAPI.OnImpersonateUserResponse -= handler,
                           "User", "Impersonate", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("SetUserNotificationsRequest",
                           handler => HTTPExtAPI.OnSetUserNotificationsRequest  += handler,
                           handler => HTTPExtAPI.OnSetUserNotificationsRequest  -= handler,
                           "SetUserNotifications", "Notifications", "Request",  "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("SetUserNotificationsResponse",
                           handler => HTTPExtAPI.OnSetUserNotificationsResponse += handler,
                           handler => HTTPExtAPI.OnSetUserNotificationsResponse -= handler,
                           "SetUserNotifications", "Notifications", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("DeleteUserNotificationsRequest",
                           handler => HTTPExtAPI.OnDeleteUserNotificationsRequest += handler,
                           handler => HTTPExtAPI.OnDeleteUserNotificationsRequest -= handler,
                           "DeleteUserNotifications", "Notifications", "Request", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("DeleteUserNotificationsResponse",
                           handler => HTTPExtAPI.OnDeleteUserNotificationsResponse += handler,
                           handler => HTTPExtAPI.OnDeleteUserNotificationsResponse -= handler,
                           "DeleteUserNotifications", "Notifications", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            #endregion

            #region Organizations

            RegisterEvent2("AddOrganizationRequest",
                           handler => HTTPExtAPI.OnAddOrganizationHTTPRequest += handler,
                           handler => HTTPExtAPI.OnAddOrganizationHTTPRequest -= handler,
                           "Organization", "Request",  "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("AddOrganizationResponse",
                           handler => HTTPExtAPI.OnAddOrganizationHTTPResponse += handler,
                           handler => HTTPExtAPI.OnAddOrganizationHTTPResponse -= handler,
                           "Organization", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("SetOrganizationRequest",
                           handler => HTTPExtAPI.OnSetOrganizationHTTPRequest += handler,
                           handler => HTTPExtAPI.OnSetOrganizationHTTPRequest -= handler,
                           "Organization", "Request",  "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("SetOrganizationResponse",
                           handler => HTTPExtAPI.OnSetOrganizationHTTPResponse += handler,
                           handler => HTTPExtAPI.OnSetOrganizationHTTPResponse -= handler,
                           "Organization", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("SetOrganizationNotificationsRequest",
                           handler => HTTPExtAPI.OnSetOrganizationNotificationsRequest  += handler,
                           handler => HTTPExtAPI.OnSetOrganizationNotificationsRequest  -= handler,
                           "SetOrganizationNotifications", "Organization", "Notifications", "Request",  "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("SetOrganizationNotificationsResponse",
                           handler => HTTPExtAPI.OnSetOrganizationNotificationsResponse += handler,
                           handler => HTTPExtAPI.OnSetOrganizationNotificationsResponse -= handler,
                           "SetOrganizationNotifications", "Organization", "Notifications", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("DeleteOrganizationNotificationsRequest",
                           handler => HTTPExtAPI.OnDeleteOrganizationNotificationsRequest += handler,
                           handler => HTTPExtAPI.OnDeleteOrganizationNotificationsRequest -= handler,
                           "DeleteOrganizationNotifications", "Organization", "Notifications", "Request", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("DeleteOrganizationNotificationsResponse",
                           handler => HTTPExtAPI.OnDeleteOrganizationNotificationsResponse += handler,
                           handler => HTTPExtAPI.OnDeleteOrganizationNotificationsResponse -= handler,
                           "DeleteOrganizationNotifications", "Organization", "Notifications", "Response", "All").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("DeleteOrganizationRequest",
                           handler => HTTPExtAPI.OnDeleteOrganizationHTTPRequest += handler,
                           handler => HTTPExtAPI.OnDeleteOrganizationHTTPRequest -= handler,
                           "DeleteOrganizations", "Organization", "Request", "All").
                     RegisterDefaultConsoleLogTarget(this).
                     RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("DeleteOrganizationResponse",
                           handler => HTTPExtAPI.OnDeleteOrganizationHTTPResponse += handler,
                           handler => HTTPExtAPI.OnDeleteOrganizationHTTPResponse -= handler,
                           "DeleteOrganizations", "Organization", "Response", "All").
                     RegisterDefaultConsoleLogTarget(this).
                     RegisterDefaultDiscLogTarget(this);

            #endregion

            #region API

            RegisterEvent2("RestartRequest",
                           handler => HTTPExtAPI.OnRestartHTTPRequest += handler,
                           handler => HTTPExtAPI.OnRestartHTTPRequest -= handler,
                           "api", "restart", "request",  "all").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("RestartResponse",
                           handler => HTTPExtAPI.OnRestartHTTPResponse += handler,
                           handler => HTTPExtAPI.OnRestartHTTPResponse -= handler,
                           "api", "restart", "response", "all").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);


            RegisterEvent2("StopRequest",
                           handler => HTTPExtAPI.OnStopHTTPRequest += handler,
                           handler => HTTPExtAPI.OnStopHTTPRequest -= handler,
                           "api", "stop", "request", "all").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            RegisterEvent2("StopResponse",
                           handler => HTTPExtAPI.OnStopHTTPResponse += handler,
                           handler => HTTPExtAPI.OnStopHTTPResponse -= handler,
                           "api", "stop", "response", "all").
                RegisterDefaultConsoleLogTarget(this).
                RegisterDefaultDiscLogTarget(this);

            #endregion

        }

        #endregion

        #endregion

    }

}
