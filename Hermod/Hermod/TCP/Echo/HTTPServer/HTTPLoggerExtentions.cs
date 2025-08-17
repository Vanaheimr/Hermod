/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.Logging;

using static org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPClientLogger;
using static org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPServerLoggerX;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extensions methods for HTTP loggers.
    /// </summary>
    public static class HTTPLoggerXExtensions
    {

        #region RegisterDefaultConsoleLogTargetX(this HTTPRequestLogger,        HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPServerRequestLogger RegisterDefaultConsoleLogTargetX(this HTTPServerRequestLogger  HTTPRequestLogger,
                                                                              HTTPServerLoggerX              HTTPLogger)

            => HTTPRequestLogger.RegisterLogTarget(LogTargets.Console,
                                                   HTTPLogger.Default_LogHTTPRequest_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTargetX(this HTTPResponseLogger,       HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPServerResponseLogger RegisterDefaultConsoleLogTargetX(this HTTPServerResponseLogger  HTTPResponseLogger,
                                                                               HTTPServerLoggerX               HTTPLogger)

            => HTTPResponseLogger.RegisterLogTarget(LogTargets.Console,
                                                    HTTPLogger.Default_LogHTTPResponse_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTargetX(this HTTPClientRequestLogger,  HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPClientRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPClientRequestLogger RegisterDefaultConsoleLogTargetX(this HTTPClientRequestLogger  HTTPClientRequestLogger,
                                                                              HTTPClientLogger              HTTPLogger)

            => HTTPClientRequestLogger.RegisterLogTarget(LogTargets.Console,
                                                         HTTPLogger.Default_LogHTTPRequest_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTargetX(this HTTPClientResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPClientResponseLogger">An HTTP response logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPClientResponseLogger RegisterDefaultConsoleLogTargetX(this HTTPClientResponseLogger  HTTPClientResponseLogger,
                                                                               HTTPClientLogger               HTTPLogger)

            => HTTPClientResponseLogger.RegisterLogTarget(LogTargets.Console,
                                                          HTTPLogger.Default_LogHTTPResponse_toConsole);

        #endregion


        #region RegisterDefaultDiscLogTargetX(this HTTPRequestLogger,        HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPServerRequestLogger RegisterDefaultDiscLogTargetX(this HTTPServerRequestLogger  HTTPRequestLogger,
                                                                           HTTPServerLoggerX              HTTPLogger)

            => HTTPRequestLogger.RegisterLogTarget(LogTargets.Disc,
                                                   HTTPLogger.Default_LogHTTPRequest_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTargetX(this HTTPResponseLogger,       HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPServerResponseLogger RegisterDefaultDiscLogTargetX(this HTTPServerResponseLogger  HTTPResponseLogger,
                                                                            HTTPServerLoggerX               HTTPLogger)

            => HTTPResponseLogger.RegisterLogTarget(LogTargets.Disc,
                                                    HTTPLogger.Default_LogHTTPResponse_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTargetX(this HTTPClientRequestLogger,  HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPClientRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPClientRequestLogger RegisterDefaultDiscLogTargetX(this HTTPClientRequestLogger  HTTPClientRequestLogger,
                                                                           HTTPClientLogger              HTTPLogger)

            => HTTPClientRequestLogger.RegisterLogTarget(LogTargets.Disc,
                                                         HTTPLogger.Default_LogHTTPRequest_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTargetX(this HTTPClientResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPClientResponseLogger">An HTTP response logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPClientResponseLogger RegisterDefaultDiscLogTargetX(this HTTPClientResponseLogger  HTTPClientResponseLogger,
                                                                            HTTPClientLogger               HTTPLogger)

            => HTTPClientResponseLogger.RegisterLogTarget(LogTargets.Disc,
                                                          HTTPLogger.Default_LogHTTPResponse_toDisc);

        #endregion



        #region RegisterDefaultConsoleLogTargetX(this HTTPRequestLogger,  HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPServerRequestLogger2 RegisterDefaultConsoleLogTargetX(this HTTPServerRequestLogger2  HTTPRequestLogger,
                                                                               HTTPServerLoggerX               HTTPLogger)

            => HTTPRequestLogger.RegisterLogTarget(LogTargets.Console,
                                                   HTTPLogger.Default_LogHTTPRequest_toConsole);

        #endregion

        #region RegisterDefaultConsoleLogTargetX(this HTTPResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default console logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPServerResponseLogger2 RegisterDefaultConsoleLogTargetX(this HTTPServerResponseLogger2  HTTPResponseLogger,
                                                                                HTTPServerLoggerX                HTTPLogger)

            => HTTPResponseLogger.RegisterLogTarget(LogTargets.Console,
                                                    HTTPLogger.Default_LogHTTPResponse_toConsole);

        #endregion

        //#region RegisterDefaultConsoleLogTargetX(this HTTPClientRequestLogger, HTTPLogger)

        ///// <summary>
        ///// Register the default console logger.
        ///// </summary>
        ///// <param name="HTTPClientRequestLogger">An HTTP request logger.</param>
        ///// <param name="HTTPLogger">An HTTP logger.</param>
        //public static HTTPClientRequestLogger RegisterDefaultConsoleLogTargetX(this HTTPClientRequestLogger  HTTPClientRequestLogger,
        //                                                                      HTTPClientLogger              HTTPLogger)

        //    => HTTPClientRequestLogger.RegisterLogTarget(LogTargets.Console,
        //                                                 HTTPLogger.Default_LogHTTPRequest_toConsole);

        //#endregion

        //#region RegisterDefaultConsoleLogTargetX(this HTTPClientResponseLogger, HTTPLogger)

        ///// <summary>
        ///// Register the default console logger.
        ///// </summary>
        ///// <param name="HTTPClientResponseLogger">An HTTP response logger.</param>
        ///// <param name="HTTPLogger">An HTTP logger.</param>
        //public static HTTPClientResponseLogger RegisterDefaultConsoleLogTargetX(this HTTPClientResponseLogger  HTTPClientResponseLogger,
        //                                                                       HTTPClientLogger               HTTPLogger)

        //    => HTTPClientResponseLogger.RegisterLogTarget(LogTargets.Console,
        //                                                  HTTPLogger.Default_LogHTTPResponse_toConsole);

        //#endregion


        #region RegisterDefaultDiscLogTargetX(this HTTPRequestLogger,  HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPRequestLogger">An HTTP request logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPServerRequestLogger2 RegisterDefaultDiscLogTargetX(this HTTPServerRequestLogger2  HTTPRequestLogger,
                                                                            HTTPServerLoggerX               HTTPLogger)

            => HTTPRequestLogger.RegisterLogTarget(LogTargets.Disc,
                                                   HTTPLogger.Default_LogHTTPRequest_toDisc);

        #endregion

        #region RegisterDefaultDiscLogTargetX(this HTTPResponseLogger, HTTPLogger)

        /// <summary>
        /// Register the default disc logger.
        /// </summary>
        /// <param name="HTTPResponseLogger">An HTTP response logger.</param>
        /// <param name="HTTPLogger">An HTTP logger.</param>
        public static HTTPServerResponseLogger2 RegisterDefaultDiscLogTargetX(this HTTPServerResponseLogger2  HTTPResponseLogger,
                                                                             HTTPServerLoggerX                HTTPLogger)

            => HTTPResponseLogger.RegisterLogTarget(LogTargets.Disc,
                                                    HTTPLogger.Default_LogHTTPResponse_toDisc);

        #endregion

        //#region RegisterDefaultDiscLogTargetX(this HTTPClientRequestLogger,  HTTPLogger)

        ///// <summary>
        ///// Register the default disc logger.
        ///// </summary>
        ///// <param name="HTTPClientRequestLogger">An HTTP request logger.</param>
        ///// <param name="HTTPLogger">An HTTP logger.</param>
        //public static HTTPClientRequestLogger RegisterDefaultDiscLogTargetX(this HTTPClientRequestLogger  HTTPClientRequestLogger,
        //                                                                   HTTPClientLogger              HTTPLogger)

        //    => HTTPClientRequestLogger.RegisterLogTarget(LogTargets.Disc,
        //                                                 HTTPLogger.Default_LogHTTPRequest_toDisc);

        //#endregion

        //#region RegisterDefaultDiscLogTargetX(this HTTPClientResponseLogger, HTTPLogger)

        ///// <summary>
        ///// Register the default disc logger.
        ///// </summary>
        ///// <param name="HTTPClientResponseLogger">An HTTP response logger.</param>
        ///// <param name="HTTPLogger">An HTTP logger.</param>
        //public static HTTPClientResponseLogger RegisterDefaultDiscLogTargetX(this HTTPClientResponseLogger  HTTPClientResponseLogger,
        //                                                                    HTTPClientLogger               HTTPLogger)

        //    => HTTPClientResponseLogger.RegisterLogTarget(LogTargets.Disc,
        //                                                  HTTPLogger.Default_LogHTTPResponse_toDisc);

        //#endregion

    }

}
