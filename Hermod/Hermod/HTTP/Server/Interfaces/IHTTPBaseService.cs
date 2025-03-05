///*
// * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
// * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
// *
// * Licensed under the Apache License, Version 2.0 (the "License");
// * you may not use this file except in compliance with the License.
// * You may obtain a copy of the License at
// *
// *     http://www.apache.org/licenses/LICENSE-2.0
// *
// * Unless required by applicable law or agreed to in writing, software
// * distributed under the License is distributed on an "AS IS" BASIS,
// * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// * See the License for the specific language governing permissions and
// * limitations under the License.
// */

//#region Usings

//using System;

//#endregion

//namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
//{

//    /// <summary>
//    /// An interface for common HTTP services.
//    /// </summary>
//    public interface IHTTPBaseService : IHTTPService
//    {

//        #region HTTPRoot

//        /// <summary>
//        /// Get Landingpage
//        /// </summary>
//        /// <returns>Some HTML and JavaScript</returns>
//        [HTTPMapping(HTTPMethods.GET, "/"), NoAuthentication]
//        HTTPResponse GET_Root();

//        String HTTPRoot { get; set; }

//        #endregion

//        #region Resources

//        /// <summary>
//        /// Will return internal libs
//        /// </summary>
//        /// <returns>internal libs</returns>
//        [NoAuthentication]
//        [HTTPMapping(HTTPMethods.GET, "/libs/{Resource}")]
//        HTTPResponse GetLibs(String Resource);

//        /// <summary>
//        /// Will return internal resources
//        /// </summary>
//        /// <returns>internal resources</returns>
//        [NoAuthentication]
//        [HTTPMapping(HTTPMethods.GET, "/resources/{Resource}")]
//        HTTPResponse GetResources(String Resource);

//        #endregion

//        #region Utilities

//        /// <summary>
//        /// Get /favicon.ico
//        /// </summary>
//        /// <returns>Some HTML and JavaScript.</returns>
//        [NoAuthentication]
//        [HTTPMapping(HTTPMethods.GET, "/favicon.ico")]
//        HTTPResponse GetFavicon();

//        /// <summary>
//        /// Get /robots.txt
//        /// </summary>
//        /// <returns>Some search engine info.</returns>
//        [NoAuthentication]
//        [HTTPMapping(HTTPMethods.GET, "/robots.txt")]
//        HTTPResponse GetRobotsTxt();

//        /// <summary>
//        /// Get /humans.txt
//        /// </summary>
//        /// <returns>Some search engine info.</returns>
//        [NoAuthentication]
//        [HTTPMapping(HTTPMethods.GET, "/humans.txt")]
//        HTTPResponse GetHumansTxt();

//        #endregion

//        #region Logging events

//        [NoAuthentication]
//        [HTTPEventMapping(EventIdentification: HTTPSemantics.LogEvents, UriTemplate: HTTPSemantics.LogEvents, HTTPMethod: HTTPMethods.GET, MaxNumberOfCachedEvents: 500)]
//        HTTPResponse GET_LogEvents();

//        [NoAuthentication]
//        [HTTPEventMapping(EventIdentification: HTTPSemantics.LogEvents, UriTemplate: HTTPSemantics.LogEvents, HTTPMethod: HTTPMethods.POST, MaxNumberOfCachedEvents: 500)]
//        HTTPResponse POST_LogEvents();

//        [NoAuthentication]
//        [HTTPEventMapping(EventIdentification: HTTPSemantics.LogEvents, UriTemplate: HTTPSemantics.LogEvents, HTTPMethod: HTTPMethods.MONITOR, MaxNumberOfCachedEvents: 500)]
//        HTTPResponse MONITOR_LogEvents();

//        #endregion

//    }

//}
