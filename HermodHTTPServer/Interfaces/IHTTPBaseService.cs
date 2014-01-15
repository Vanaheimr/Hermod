/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// HTTP base services interface.
    /// </summary>
    public interface IHTTPBaseService : IHTTPService
    {

        #region Landingpage

        /// <summary>
        /// Get Landingpage
        /// </summary>
        /// <returns>Some HTML and JavaScript</returns>
        [HTTPMapping(HTTPMethods.GET, "/"), NoAuthentication]
        HTTPResponse GET_Root();

        String HTTPRoot { get; set; }

        #endregion

        #region Utilities

        /// <summary>
        /// Will return internal resources
        /// </summary>
        /// <returns>internal resources</returns>
        [NoAuthentication]
        [HTTPMapping(HTTPMethods.GET, "/resources/{Resource}")]
        HTTPResponse GetResources(String Resource);

        /// <summary>
        /// Get /favicon.ico
        /// </summary>
        /// <returns>Some HTML and JavaScript.</returns>
        [NoAuthentication]
        [HTTPMapping(HTTPMethods.GET, "/favicon.ico")]
        HTTPResponse GetFavicon();

        /// <summary>
        /// Get /robots.txt
        /// </summary>
        /// <returns>Some search engine info.</returns>
        [NoAuthentication]
        [HTTPMapping(HTTPMethods.GET, "/robots.txt")]
        HTTPResponse GetRobotsTxt();

        /// <summary>
        /// Get /humans.txt
        /// </summary>
        /// <returns>Some search engine info.</returns>
        [NoAuthentication]
        [HTTPMapping(HTTPMethods.GET, "/humans.txt")]
        HTTPResponse GetHumansTxt();

        #endregion

    }

}
