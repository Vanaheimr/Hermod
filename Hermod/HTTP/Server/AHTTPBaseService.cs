///*
// * Copyright (c) 2011-2013 Achim 'ahzf' Friedland <achim@ahzf.de>
// * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
// * 
// * This program is free software; you can redistribute it and/or modify
// * it under the terms of the GNU General Public License as published by
// * the Free Software Foundation; either version 3 of the License, or
// * (at your option) any later version.
// * 
// * You may obtain a copy of the License at
// *   http://www.gnu.org/licenses/gpl.html
// * 
// * This program is distributed in the hope that it will be useful, but
// * WITHOUT ANY WARRANTY; without even the implied warranty of
// * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// * General Public License for more details.
// */

//#region Usings

//using System;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using System.Collections.Generic;

//using eu.Vanaheimr.Illias.Commons;
//using System.Text.RegularExpressions;
//using System.Threading;
//using Newtonsoft.Json.Linq;
//using System.Xml.Linq;
//using eu.Vanaheimr.Hermod.Services.DNS;

//#endregion

//namespace eu.Vanaheimr.Hermod.HTTP
//{

//    /// <summary>
//    /// An abstract HTTP service implementation for common base services.
//    /// </summary>
//    public abstract class AHTTPBaseService : AHTTPService,
//                                             IHTTPBaseService
//    {

//        #region Constructor(s)

//        #region AHTTPBaseService()

//        /// <summary>
//        /// Creates a new AHTTPBaseService.
//        /// </summary>
//        public AHTTPBaseService()
//            : base()
//        { }

//        #endregion

//        #region AHTTPBaseService(HTTPContentType)

//        /// <summary>
//        /// Creates a new abstract HTTPBaseService.
//        /// </summary>
//        /// <param name="HTTPContentType">A content type.</param>
//        public AHTTPBaseService(HTTPContentType HTTPContentType)
//            : base(HTTPContentType)
//        { }

//        #endregion

//        #region AHTTPBaseService(HTTPContentTypes)

//        /// <summary>
//        /// Creates a new abstract HTTPBaseService.
//        /// </summary>
//        /// <param name="HTTPContentTypes">A content type.</param>
//        public AHTTPBaseService(IEnumerable<HTTPContentType> HTTPContentTypes)
//            : base(HTTPContentTypes)
//        { }

//        #endregion

//        #region AHTTPBaseService(IHTTPConnection, HTTPContentType)

//        /// <summary>
//        /// Creates a new abstract HTTPBaseService.
//        /// </summary>
//        /// <param name="IHTTPConnection">The http connection for this request.</param>
//        /// <param name="HTTPContentType">A http content type.</param>
//        public AHTTPBaseService(IHTTPConnection IHTTPConnection, HTTPContentType HTTPContentType)
//            : base(IHTTPConnection, HTTPContentType)
//        { }

//        #endregion

//        #region AHTTPBaseService(IHTTPConnection, HTTPContentTypes)

//        /// <summary>
//        /// Creates a new abstract HTTPBaseService.
//        /// </summary>
//        /// <param name="IHTTPConnection">The http connection for this request.</param>
//        /// <param name="HTTPContentTypes">An enumeration of http content types.</param>
//        public AHTTPBaseService(IHTTPConnection IHTTPConnection, IEnumerable<HTTPContentType> HTTPContentTypes)
//            : base(IHTTPConnection, HTTPContentTypes)
//        { }

//        #endregion

//        #endregion



//        #region GetRAWRequestHeader()

//        /// <summary>
//        /// Return the RAW request header.
//        /// </summary>
//        public HTTPResponse GetRAWRequestHeader()
//        {

//            return new HTTPResponseBuilder() {

//                HTTPStatusCode = HTTPStatusCode.OK,
//                CacheControl   = "no-cache",
//                Connection     = "close",
//                ContentType    = HTTPContentType.TEXT_UTF8,
//                Content        = ("Incoming http connection from '" + IHTTPConnection.RemoteSocket + "'" +
//                                   Environment.NewLine + Environment.NewLine +
//                                   IHTTPConnection.RequestHeader.RawHTTPHeader +
//                                   Environment.NewLine + Environment.NewLine +
//                                   "Method => "         + IHTTPConnection.RequestHeader.HTTPMethod      + Environment.NewLine +
//                                   "URL => "            + IHTTPConnection.RequestHeader.UrlPath         + Environment.NewLine +
//                                   "QueryString => "    + IHTTPConnection.RequestHeader.QueryString     + Environment.NewLine +
//                                   "Protocol => "       + IHTTPConnection.RequestHeader.ProtocolName    + Environment.NewLine +
//                                   "Version => "        + IHTTPConnection.RequestHeader.ProtocolVersion + Environment.NewLine +
//                                   Environment.NewLine + Environment.NewLine +
//                                   IHTTPConnection.ResponseHeader.HTTPStatusCode).ToUTF8Bytes()

//            };

//        }

//        #endregion


//        #region GET /

//        /// <summary>
//        /// Get the landing page.
//        /// </summary>
//        public virtual HTTPResponse GET_Root()
//        {

//            var path = IHTTPConnection.RequestHeader.UrlPath.Remove(0, 1);

//            return (path != "")
//                       ? GetResources(path)
//                       : GetResources("index.html");

//            //return HTTPTools.MovedTemporarily("/combinedlog");

//        }

//        #endregion

//        #region Resources

//        #region GetLibs(ResourceName)

//        /// <summary>
//        /// Returns internal resources embedded within the assembly.
//        /// </summary>
//        /// <param name="ResourceName">The path and name of the resource.</param>
//        public HTTPResponse GetLibs(String ResourceName)
//        {
//            return base._GetResources(HTTPRoot + ".libs.", ResourceName);
//        }

//        #endregion

//        #region GetResources(ResourceName)

//        public HTTPResponse GetResources(String ResourceName)
//        {
//            return _GetResources(HTTPRoot + ".resources.", ResourceName);
//        }

//        #endregion

//        #endregion

//        #region Utilities

//        #region GetFavicon()

//        /// <summary>
//        /// Get /favicon.ico
//        /// </summary>
//        /// <returns>Some HTML and JavaScript.</returns>
//        public HTTPResponse GetFavicon()
//        {
//            return _GetResources("favicon.ico");
//        }

//        #endregion

//        #region GetRobotsTxt()

//        /// <summary>
//        /// Get /robots.txt
//        /// </summary>
//        /// <returns>Some search engine info.</returns>
//        public HTTPResponse GetRobotsTxt()
//        {
//            return _GetResources("robots.txt");
//        }

//        #endregion

//        #region GetHumansTxt()

//        /// <summary>
//        /// Get /humans.txt
//        /// </summary>
//        /// <returns>Some search engine info.</returns>
//        public HTTPResponse GetHumansTxt()
//        {
//            return _GetResources("humans.txt");
//        }

//        #endregion

//        #endregion

//        #region /LogEvents

//        #region GET     /LogEvents

//        /// <summary>
//        /// Get a list of all job events encoded as an eventstream.
//        /// </summary>
//        /// <example>GET /LogEvents</example>
//        public virtual HTTPResponse GET_LogEvents()
//        {

//            if (IHTTPConnection.RequestHeader.Accept.BestMatchingContentType(HTTPContentType.EVENTSTREAM) == HTTPContentType.EVENTSTREAM)
//                return _GetEventStream(HTTPSemantics.LogEvents);

//            return new HTTPResult<Object>(IHTTPConnection.RequestHeader, HTTPStatusCode.NotAcceptable).Error;

//        }

//        #endregion

//        #region POST    /LogEvents

//        /// <summary>
//        /// Get a list of all job events encoded as an eventstream.
//        /// </summary>
//        /// <example>POST /LogEvents</example>
//        public virtual HTTPResponse POST_LogEvents()
//        {
//            return GET_LogEvents();
//        }

//        #endregion

//        #region MONITOR /LogEvents

//        /// <summary>
//        /// Get a list of all job events encoded as an eventstream.
//        /// </summary>
//        /// <example>GET /LogEvents</example>
//        public virtual HTTPResponse MONITOR_LogEvents()
//        {
//            return GET_LogEvents();
//        }

//        #endregion

//        #endregion

//    }

//}
