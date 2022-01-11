/*
 * Copyright (c) 2010-2022, Achim Friedland <achim.friedland@graphdefined.com>
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

using static org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPClientLogger;
using static org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPServerLogger;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extensions methods for HTTP loggers.
    /// </summary>
    public static class HTTPLoggerExtensions
    {

        #region RegisterDefaultConsoleLogTarget(this HTTPRequestLogger, HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPServerRequestLogger RegisterDefaultConsoleLogTarget(this HTTPServerRequestLogger  HTTPRequestLogger,
                                                                              HTTPServerLogger              HTTPLogger)

            => HTTPRequestLogger.RegisterLogTarget(LogTargets.Console,
                                                   HTTPLogger.Default_LogHTTPRequest_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTarget(this HTTPResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPServerResponseLogger RegisterDefaultConsoleLogTarget(this HTTPServerResponseLogger  HTTPResponseLogger,
                                                                               HTTPServerLogger               HTTPLogger)

            => HTTPResponseLogger.RegisterLogTarget(LogTargets.Console,
                                                    HTTPLogger.Default_LogHTTPResponse_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTarget(this HTTPClientRequestLogger, HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPClientRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPClientRequestLogger RegisterDefaultConsoleLogTarget(this HTTPClientRequestLogger  HTTPClientRequestLogger,
                                                                              HTTPClientLogger              HTTPLogger)

            => HTTPClientRequestLogger.RegisterLogTarget(LogTargets.Console,
                                                         HTTPLogger.Default_LogHTTPRequest_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTarget(this HTTPClientResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPClientResponseLogger">A HTTP response logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPClientResponseLogger RegisterDefaultConsoleLogTarget(this HTTPClientResponseLogger  HTTPClientResponseLogger,
                                                                               HTTPClientLogger               HTTPLogger)

            => HTTPClientResponseLogger.RegisterLogTarget(LogTargets.Console,
                                                          HTTPLogger.Default_LogHTTPResponse_toConsole);

        #endregion


        #region RegisterDefaultDiscLogTarget(this HTTPRequestLogger,  HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPServerRequestLogger RegisterDefaultDiscLogTarget(this HTTPServerRequestLogger  HTTPRequestLogger,
                                                                           HTTPServerLogger              HTTPLogger)

            => HTTPRequestLogger.RegisterLogTarget(LogTargets.Disc,
                                                   HTTPLogger.Default_LogHTTPRequest_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTarget(this HTTPResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPServerResponseLogger RegisterDefaultDiscLogTarget(this HTTPServerResponseLogger  HTTPResponseLogger,
                                                                            HTTPServerLogger               HTTPLogger)

            => HTTPResponseLogger.RegisterLogTarget(LogTargets.Disc,
                                                    HTTPLogger.Default_LogHTTPResponse_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTarget(this HTTPClientRequestLogger,  HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPClientRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPClientRequestLogger RegisterDefaultDiscLogTarget(this HTTPClientRequestLogger  HTTPClientRequestLogger,
                                                                           HTTPClientLogger              HTTPLogger)

            => HTTPClientRequestLogger.RegisterLogTarget(LogTargets.Disc,
                                                         HTTPLogger.Default_LogHTTPRequest_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTarget(this HTTPClientResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPClientResponseLogger">A HTTP response logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPClientResponseLogger RegisterDefaultDiscLogTarget(this HTTPClientResponseLogger  HTTPClientResponseLogger,
                                                                            HTTPClientLogger               HTTPLogger)

            => HTTPClientResponseLogger.RegisterLogTarget(LogTargets.Disc,
                                                          HTTPLogger.Default_LogHTTPResponse_toDisc);

        #endregion



        #region RegisterDefaultConsoleLogTarget(this HTTPRequestLogger, HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPServerRequestLogger2 RegisterDefaultConsoleLogTarget(this HTTPServerRequestLogger2  HTTPRequestLogger,
                                                                              HTTPServerLogger              HTTPLogger)

            => HTTPRequestLogger.RegisterLogTarget(LogTargets.Console,
                                                   HTTPLogger.Default_LogHTTPRequest_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTarget(this HTTPResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPServerResponseLogger2 RegisterDefaultConsoleLogTarget(this HTTPServerResponseLogger2  HTTPResponseLogger,
                                                                               HTTPServerLogger               HTTPLogger)

            => HTTPResponseLogger.RegisterLogTarget(LogTargets.Console,
                                                    HTTPLogger.Default_LogHTTPResponse_toConsole);

        #endregion

        //#region RegisterDefaultConsoleLogTarget(this HTTPClientRequestLogger, HTTPLogger)

        ///// <summary>
        ///// Register the default console logger.
        ///// </summary>
        ///// <param name="HTTPClientRequestLogger">A HTTP request logger.</param>
        ///// <param name="HTTPLogger">A HTTP logger.</param>
        //public static HTTPClientRequestLogger RegisterDefaultConsoleLogTarget(this HTTPClientRequestLogger  HTTPClientRequestLogger,
        //                                                                      HTTPClientLogger              HTTPLogger)

        //    => HTTPClientRequestLogger.RegisterLogTarget(LogTargets.Console,
        //                                                 HTTPLogger.Default_LogHTTPRequest_toConsole);

        //#endregion

        //#region RegisterDefaultConsoleLogTarget(this HTTPClientResponseLogger, HTTPLogger)

        ///// <summary>
        ///// Register the default console logger.
        ///// </summary>
        ///// <param name="HTTPClientResponseLogger">A HTTP response logger.</param>
        ///// <param name="HTTPLogger">A HTTP logger.</param>
        //public static HTTPClientResponseLogger RegisterDefaultConsoleLogTarget(this HTTPClientResponseLogger  HTTPClientResponseLogger,
        //                                                                       HTTPClientLogger               HTTPLogger)

        //    => HTTPClientResponseLogger.RegisterLogTarget(LogTargets.Console,
        //                                                  HTTPLogger.Default_LogHTTPResponse_toConsole);

        //#endregion


        #region RegisterDefaultDiscLogTarget(this HTTPRequestLogger,  HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">A HTTP request logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPServerRequestLogger2 RegisterDefaultDiscLogTarget(this HTTPServerRequestLogger2  HTTPRequestLogger,
                                                                           HTTPServerLogger              HTTPLogger)

            => HTTPRequestLogger.RegisterLogTarget(LogTargets.Disc,
                                                   HTTPLogger.Default_LogHTTPRequest_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTarget(this HTTPResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">A HTTP response logger.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        public static HTTPServerResponseLogger2 RegisterDefaultDiscLogTarget(this HTTPServerResponseLogger2  HTTPResponseLogger,
                                                                            HTTPServerLogger               HTTPLogger)

            => HTTPResponseLogger.RegisterLogTarget(LogTargets.Disc,
                                                    HTTPLogger.Default_LogHTTPResponse_toDisc);

        #endregion

        //#region RegisterDefaultDiscLogTarget(this HTTPClientRequestLogger,  HTTPLogger)

        ///// <summary>
        ///// Register the default disc logger.
        ///// </summary>
        ///// <param name="HTTPClientRequestLogger">A HTTP request logger.</param>
        ///// <param name="HTTPLogger">A HTTP logger.</param>
        //public static HTTPClientRequestLogger RegisterDefaultDiscLogTarget(this HTTPClientRequestLogger  HTTPClientRequestLogger,
        //                                                                   HTTPClientLogger              HTTPLogger)

        //    => HTTPClientRequestLogger.RegisterLogTarget(LogTargets.Disc,
        //                                                 HTTPLogger.Default_LogHTTPRequest_toDisc);

        //#endregion

        //#region RegisterDefaultDiscLogTarget(this HTTPClientResponseLogger, HTTPLogger)

        ///// <summary>
        ///// Register the default disc logger.
        ///// </summary>
        ///// <param name="HTTPClientResponseLogger">A HTTP response logger.</param>
        ///// <param name="HTTPLogger">A HTTP logger.</param>
        //public static HTTPClientResponseLogger RegisterDefaultDiscLogTarget(this HTTPClientResponseLogger  HTTPClientResponseLogger,
        //                                                                    HTTPClientLogger               HTTPLogger)

        //    => HTTPClientResponseLogger.RegisterLogTarget(LogTargets.Disc,
        //                                                  HTTPLogger.Default_LogHTTPResponse_toDisc);

        //#endregion

    }

}
