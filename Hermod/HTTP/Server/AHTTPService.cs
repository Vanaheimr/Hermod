/*
 * Copyright (c) 2011 Achim 'ahzf' Friedland <achim@ahzf.de>
 * This file is part of Loki <http://www.github.com/ahzf/Loki>
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 * 
 * You may obtain a copy of the License at
 *     http://www.gnu.org/licenses/gpl.html
 *     
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 */

#region Usings

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using de.ahzf.Hermod;
using de.ahzf.Hermod.HTTP;
using System.Threading;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// Default abstract HTTP service implementation.
    /// </summary>
    public abstract class AHTTPService
    {

        #region Properties

        public IHTTPConnection IHTTPConnection { get;           set; }
        public Assembly        CallingAssembly { get; protected set; }
        public String          ResourcePath    { get; private   set; }

        /// <summary>
        /// Returns an enumeration of all associated content types.
        /// </summary>
        public IEnumerable<HTTPContentType> HTTPContentTypes { get; private set; }

        #endregion

        #region Constructor(s)

        #region AHTTPService()

        /// <summary>
        /// Creates a new AHTTPService.
        /// </summary>
        public AHTTPService()
        { }

        #endregion

        #region AHTTPService(HTTPContentType)

        /// <summary>
        /// Creates a new abstract HTTPService.
        /// </summary>
        /// <param name="HTTPContentType">A content type.</param>
        public AHTTPService(HTTPContentType HTTPContentType)
        {

            #region Initial checks

            if (HTTPContentType == null)
                throw new ArgumentNullException("The given HTTPContentType must not be null!");

            #endregion

            this.HTTPContentTypes = new HTTPContentType[1] { HTTPContentType };

        }

        #endregion

        #region AHTTPService(HTTPContentTypes)

        /// <summary>
        /// Creates a new abstract HTTPService.
        /// </summary>
        /// <param name="HTTPContentTypes">A content type.</param>
        public AHTTPService(IEnumerable<HTTPContentType> HTTPContentTypes)
        {

            #region Initial checks

            if (HTTPContentTypes == null)
                throw new ArgumentNullException("The given HTTPContentTypes must not be null!");

            #endregion

            this.HTTPContentTypes = HTTPContentTypes;

        }

        #endregion

        #region AHTTPService(IHTTPConnection, HTTPContentType, ResourcePath)

        /// <summary>
        /// Creates a new abstract HTTPService.
        /// </summary>
        /// <param name="IHTTPConnection">The http connection for this request.</param>
        /// <param name="HTTPContentType">A content type.</param>
        /// <param name="ResourcePath">The path to internal resources.</param>
        public AHTTPService(IHTTPConnection IHTTPConnection, HTTPContentType HTTPContentType, String ResourcePath)
        {

            #region Initial checks

            if (IHTTPConnection == null)
                throw new ArgumentNullException("The given IHTTPConnection must not be null!");

            if (HTTPContentType == null)
                throw new ArgumentNullException("The given HTTPContentType must not be null!");

            if (ResourcePath.IsNullOrEmpty())
                throw new ArgumentNullException("The given ResourcePath must not be null or empty!");

            #endregion

            this.IHTTPConnection  = IHTTPConnection;
            this.HTTPContentTypes = new HTTPContentType[1] { HTTPContentType };
            this.ResourcePath     = ResourcePath;

        }

        #endregion

        #region AHTTPService(IHTTPConnection, HTTPContentTypes, ResourcePath)

        /// <summary>
        /// Creates a new abstract HTTPService.
        /// </summary>
        /// <param name="IHTTPConnection">The http connection for this request.</param>
        /// <param name="HTTPContentTypes">An enumeration of content types.</param>
        /// <param name="ResourcePath">The path to internal resources.</param>
        public AHTTPService(IHTTPConnection IHTTPConnection, IEnumerable<HTTPContentType> HTTPContentTypes, String ResourcePath)
        {

            #region Initial checks

            if (IHTTPConnection == null)
                throw new ArgumentNullException("The given IHTTPConnection must not be null!");

            if (HTTPContentTypes.IsNullOrEmpty())
                throw new ArgumentNullException("The given HTTPContentTypes must not be null or empty!");

            if (ResourcePath.IsNullOrEmpty())
                throw new ArgumentNullException("The given ResourcePath must not be null or empty!");

            #endregion

            this.IHTTPConnection  = IHTTPConnection;
            this.HTTPContentTypes = HTTPContentTypes;
            this.ResourcePath     = ResourcePath;

        }

        #endregion

        #endregion


        #region GetRAWRequestHeader()

        public HTTPResponse GetRAWRequestHeader()
        {

            return new HTTPResponseBuilder()
                {
                    HTTPStatusCode = HTTPStatusCode.OK,
                    CacheControl   = "no-cache",
                    Connection     = "close",
                    ContentType    = HTTPContentType.TEXT_UTF8,
                    Content        = ("Incoming http connection from '" + IHTTPConnection.RemoteSocket + "'" +
                                       Environment.NewLine + Environment.NewLine +
                                       IHTTPConnection.InHTTPRequest.RawHTTPHeader +
                                       Environment.NewLine + Environment.NewLine +
                                       "Method => " + IHTTPConnection.InHTTPRequest.HTTPMethod + Environment.NewLine +
                                       "URL => " + IHTTPConnection.InHTTPRequest.UrlPath + Environment.NewLine +
                                       "QueryString => " + IHTTPConnection.InHTTPRequest.QueryString + Environment.NewLine +
                                       "Protocol => " + IHTTPConnection.InHTTPRequest.ProtocolName + Environment.NewLine +
                                       "Version => " + IHTTPConnection.InHTTPRequest.ProtocolVersion + Environment.NewLine +
                                       Environment.NewLine + Environment.NewLine +
                                       IHTTPConnection.ResponseHeader.HTTPStatusCode).ToUTF8Bytes()
                };

        }

        #endregion


        #region Error400_BadRequest()

        protected HTTPResponse Error400_BadRequest()
        {

            return new HTTPResponseBuilder()
            {
                HTTPStatusCode = HTTPStatusCode.BadRequest,
                CacheControl   = "no-cache",
                Connection     = "close",
            };

        }

        #endregion

        #region Error404_NotFound()

        protected HTTPResponse Error404_NotFound()
        {

            return new HTTPResponseBuilder()
                    {
                        HTTPStatusCode = HTTPStatusCode.NotFound,
                        CacheControl   = "no-cache",
                        Connection     = "close",
                    };

        }

        #endregion

        #region Error406_NotAcceptable()

        protected HTTPResponse Error406_NotAcceptable()
        {

            return new HTTPResponseBuilder()
                    {
                        HTTPStatusCode = HTTPStatusCode.NotAcceptable,
                        CacheControl   = "no-cache",
                        Connection     = "close",
                    };

        }

        #endregion

        #region Error409_Conflict()

        protected HTTPResponse Error409_Conflict()
        {

            return new HTTPResponseBuilder()
            {
                HTTPStatusCode = HTTPStatusCode.Conflict,
                CacheControl   = "no-cache",
                Connection     = "close",
            };

        }

        #endregion


        #region GetResources(myResource)

        /// <summary>
        /// Returns internal resources embedded within the assembly.
        /// </summary>
        /// <param name="myResource">The path and name of the resource.</param>
        public HTTPResponse GetResources(String myResource)
        {

            #region Initial checks

            if (CallingAssembly == null)
                throw new ArgumentNullException("The calling assembly must not be null! Please add the line 'this.CallingAssembly = Assembly.GetExecutingAssembly();' to your constructor(s)!");

            #endregion

            #region Data

            var _AllResources = CallingAssembly.GetManifestResourceNames();

            myResource = myResource.Replace('/', '.');

            #endregion

            #region Return internal assembly resources...

            if (_AllResources.Contains(this.ResourcePath + myResource))
            {

                var _ResourceContent = CallingAssembly.GetManifestResourceStream(this.ResourcePath + myResource);

                HTTPContentType _ResponseContentType = null;

                // Get the apropriate content type based on the suffix of the requested resource
                switch (myResource.Remove(0, myResource.LastIndexOf(".") + 1))
                {
                    case "htm":  _ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "html": _ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "css":  _ResponseContentType = HTTPContentType.CSS_UTF8;        break;
                    case "gif":  _ResponseContentType = HTTPContentType.GIF;             break;
                    case "ico":  _ResponseContentType = HTTPContentType.ICO;             break;
                    case "swf":  _ResponseContentType = HTTPContentType.SWF;             break;
                    case "js":   _ResponseContentType = HTTPContentType.JAVASCRIPT_UTF8; break;
                    case "txt":  _ResponseContentType = HTTPContentType.TEXT_UTF8;       break;
                    default:     _ResponseContentType = HTTPContentType.OCTETSTREAM;     break;
                }

                return new HTTPResponseBuilder()
                        {
                            HTTPStatusCode = HTTPStatusCode.OK,
                            ContentType    = _ResponseContentType,
                            ContentLength  = (UInt64) _ResourceContent.Length,
                            CacheControl   = "no-cache",
                            Connection     = "close",
                            ContentStream  = _ResourceContent
                        };

            }

            #endregion

            #region ...or send an (custom) error 404!

            else
            {
                
                Stream _ResourceContent = null;

                if (_AllResources.Contains(this.ResourcePath + ".errorpages.Error404.html"))
                    _ResourceContent = this.CallingAssembly.GetManifestResourceStream(this.ResourcePath + ".errorpages.Error404.html");
                else
                    _ResourceContent = new MemoryStream("Error 404 - File not found!".ToUTF8Bytes());

                return new HTTPResponseBuilder()
                        {
                            HTTPStatusCode = HTTPStatusCode.NotFound,
                            ContentType    = HTTPContentType.HTML_UTF8,
                            CacheControl   = "no-cache",
                            Connection     = "close",
                            ContentStream  = _ResourceContent
                        };

            }

            #endregion

        }

        #endregion

        #region GetFavicon()

        /// <summary>
        /// Get /favicon.ico
        /// </summary>
        /// <returns>Some HTML and JavaScript.</returns>
        public HTTPResponse GetFavicon()
        {
            return GetResources("favicon.ico");
        }

        #endregion

        #region GetRobotsTxt()

        /// <summary>
        /// Get /robots.txt
        /// </summary>
        /// <returns>Some search engine info.</returns>
        public HTTPResponse GetRobotsTxt()
        {
            return GetResources("robots.txt");
        }

        #endregion

        #region GetHumansTxt()

        /// <summary>
        /// Get /humans.txt
        /// </summary>
        /// <returns>Some search engine info.</returns>
        public HTTPResponse GetHumansTxt()
        {
            return GetResources("humans.txt");
        }

        #endregion

    }

}
