/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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

namespace de.ahzf.Hermod.HTTP
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
        HTTPResponseBuilder GetRoot();

        #endregion

        #region Utilities

        /// <summary>
        /// Will return internal resources
        /// </summary>
        /// <returns>internal resources</returns>
        [NoAuthentication]
        [HTTPMapping(HTTPMethods.GET, "/resources/{myResource}")]
        HTTPResponseBuilder GetResources(String myResource);

        /// <summary>
        /// Get /favicon.ico
        /// </summary>
        /// <returns>Some HTML and JavaScript.</returns>
        [NoAuthentication]
        [HTTPMapping(HTTPMethods.GET, "/favicon.ico")]
        HTTPResponseBuilder GetFavicon();

        /// <summary>
        /// Get /robots.txt
        /// </summary>
        /// <returns>Some search engine info.</returns>
        [NoAuthentication]
        [HTTPMapping(HTTPMethods.GET, "/robots.txt")]
        HTTPResponseBuilder GetRobotsTxt();

        /// <summary>
        /// Get /humans.txt
        /// </summary>
        /// <returns>Some search engine info.</returns>
        [NoAuthentication]
        [HTTPMapping(HTTPMethods.GET, "/humans.txt")]
        HTTPResponseBuilder GetHumansTxt();

        #endregion

    }

}
