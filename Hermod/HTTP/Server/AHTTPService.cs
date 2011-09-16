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

        #endregion

        #region Constructor(s)

        #region AHTTPService()

        /// <summary>
        /// Creates a new AHTTPService.
        /// </summary>
        public AHTTPService()
        { }

        #endregion

        #region AHTTPService(IHTTPConnection)

        /// <summary>
        /// Creates a new abstract HTTPService.
        /// </summary>
        /// <param name="IHTTPConnection">The http connection for this request.</param>
        public AHTTPService(IHTTPConnection IHTTPConnection, String ResourcePath)
        {
            this.IHTTPConnection = IHTTPConnection;
            this.ResourcePath    = ResourcePath;
        }

        #endregion

        #endregion


        #region GetRAWRequestHeader()

        public HTTPResponseHeader GetRAWRequestHeader()
        {

            return new HTTPResponseBuilder()
                {
                    HTTPStatusCode = HTTPStatusCode.OK,
                    CacheControl   = "no-cache",
                    Connection     = "close",
                    ContentType    = HTTPContentType.TEXT_UTF8,
                    Content        = ("Incoming http connection from '" + IHTTPConnection.RemoteSocket + "'" +
                                       Environment.NewLine + Environment.NewLine +
                                       IHTTPConnection.RequestHeader.RawHTTPHeader +
                                       Environment.NewLine + Environment.NewLine +
                                       "Method => " + IHTTPConnection.RequestHeader.HTTPMethod + Environment.NewLine +
                                       "URL => " + IHTTPConnection.RequestHeader.Url + Environment.NewLine +
                                       "QueryString => " + IHTTPConnection.RequestHeader.QueryString + Environment.NewLine +
                                       "Protocol => " + IHTTPConnection.RequestHeader.ProtocolName + Environment.NewLine +
                                       "Version => " + IHTTPConnection.RequestHeader.ProtocolVersion + Environment.NewLine +
                                       Environment.NewLine + Environment.NewLine +
                                       IHTTPConnection.ResponseHeader.HTTPStatusCode).ToUTF8Bytes()
                };

        }

        #endregion


        #region Error406_NotAcceptable()

        protected HTTPResponseHeader Error406_NotAcceptable()
        {

            return new HTTPResponseBuilder()
                    {
                        HTTPStatusCode = HTTPStatusCode.NotAcceptable,
                        ContentType    = HTTPContentType.TEXT_UTF8,
                        ContentLength  = 0,
                        CacheControl   = "no-cache",
                        Connection     = "close",
                        Content        = new Byte[0]
                    };

        }

        #endregion


        #region GetResources(myResource)

        /// <summary>
        /// Returns internal resources embedded within the assembly.
        /// </summary>
        /// <param name="myResource">The path and name of the resource.</param>
        public HTTPResponseHeader GetResources(String myResource)
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
                    _ResourceContent = new MemoryStream(UTF8Encoding.UTF8.GetBytes("Error 404 - File not found!"));

                return new HTTPResponseBuilder()
                        {
                            HTTPStatusCode = HTTPStatusCode.NotFound,
                            ContentType    = HTTPContentType.HTML_UTF8,
                            ContentLength  = (UInt64) _ResourceContent.Length,
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
        public HTTPResponseHeader GetFavicon()
        {
            return GetResources("favicon.ico");
        }

        #endregion

        #region GetRobotsTxt()

        /// <summary>
        /// Get /robots.txt
        /// </summary>
        /// <returns>Some search engine info.</returns>
        public HTTPResponseHeader GetRobotsTxt()
        {
            return GetResources("robots.txt");
        }

        #endregion

        #region GetHumansTxt()

        /// <summary>
        /// Get /humans.txt
        /// </summary>
        /// <returns>Some search engine info.</returns>
        public HTTPResponseHeader GetHumansTxt()
        {
            return GetResources("humans.txt");
        }

        #endregion

    }

}
