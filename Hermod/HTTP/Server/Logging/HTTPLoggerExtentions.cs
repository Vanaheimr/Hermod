/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extentions methods for the HTTP logger.
    /// </summary>
    public static class HTTPLoggerExtentions
    {

        #region RegisterDefaultConsoleLogTarget(this HTTPRequestLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        public static HTTPLogger.HTTPRequestLogger RegisterDefaultConsoleLogTarget(this HTTPLogger.HTTPRequestLogger  HTTPRequestLogger)
        {

            return HTTPRequestLogger.RegisterLogTarget(LogTargets.Console,
                                                      (Context, LogEventName, Request) => HTTPLogger.Default_LogHTTPRequest_toConsole(Context, LogEventName, Request));

        }

        #endregion

        #region RegisterDefaultConsoleLogTarget(this HTTPRequestLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        public static HTTPLogger.HTTPResponseLogger RegisterDefaultConsoleLogTarget(this HTTPLogger.HTTPResponseLogger  HTTPResponseLogger)
        {

            return HTTPResponseLogger.RegisterLogTarget(LogTargets.Console,
                                                       (Context, LogEventName, Request, Response) => HTTPLogger.Default_LogHTTPResponse_toConsole(Context, LogEventName, Request, Response));

        }

        #endregion


        #region RegisterDefaultDiscLogTarget(this HTTPRequestLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        public static HTTPLogger.HTTPRequestLogger RegisterDefaultDiscLogTarget(this HTTPLogger.HTTPRequestLogger  HTTPRequestLogger)
        {

            return HTTPRequestLogger.RegisterLogTarget(LogTargets.Disc,
                                                      (Context, LogEventName, Request) => HTTPLogger.Default_LogHTTPRequest_toDisc(Context, LogEventName, Request));

        }

        #endregion

        #region RegisterDefaultDiscLogTarget(this HTTPRequestLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        public static HTTPLogger.HTTPResponseLogger RegisterDefaultDiscLogTarget(this HTTPLogger.HTTPResponseLogger  HTTPResponseLogger)
        {

            return HTTPResponseLogger.RegisterLogTarget(LogTargets.Disc,
                                                       (Context, LogEventName, Request, Response) => HTTPLogger.Default_LogHTTPResponse_toDisc(Context, LogEventName, Request, Response));

        }

        #endregion

    }

}
