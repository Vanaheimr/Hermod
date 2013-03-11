/*
 * Copyright (c) 2011-2013 Achim 'ahzf' Friedland <achim@ahzf.de>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using eu.Vanaheimr.Illias.Commons;
using System.Text.RegularExpressions;
using System.Threading;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An abstract HTTP service implementation.
    /// </summary>
    public abstract class AHTTPService
    {

        #region Data

        /// <summary>
        /// The 'Callback' parameter within the query string
        /// </summary>
        protected ThreadLocal<String> Callback;

        /// <summary>
        /// The 'Skip' parameter within the query string
        /// </summary>
        protected ThreadLocal<UInt64> Skip;

        /// <summary>
        /// The 'Take' parameter within the query string
        /// </summary>
        protected ThreadLocal<UInt64> Take;

        #endregion

        #region Properties

        /// <summary>
        /// The HTTP connection.
        /// </summary>
        public IHTTPConnection               IHTTPConnection    { get; set; }

        /// <summary>
        /// The calling assembly.
        /// </summary>
        public Assembly                      CallingAssembly    { get; protected set; }

        /// <summary>
        /// The resource path where to find the internal resources to be exported via HTTP '/resources'.
        /// </summary>
        public String                        ResourcePath       { get; private   set; }

        /// <summary>
        /// An enumeration of all associated content types.
        /// </summary>
        public IEnumerable<HTTPContentType>  HTTPContentTypes   { get; private   set; }

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
        /// <param name="HTTPContentType">A http content type.</param>
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
        /// <param name="HTTPContentTypes">An enumeration of http content types.</param>
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

        /// <summary>
        /// Return the RAW request header.
        /// </summary>
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
                                       IHTTPConnection.RequestHeader.RawHTTPHeader +
                                       Environment.NewLine + Environment.NewLine +
                                       "Method => " + IHTTPConnection.RequestHeader.HTTPMethod + Environment.NewLine +
                                       "URL => " + IHTTPConnection.RequestHeader.UrlPath + Environment.NewLine +
                                       "QueryString => " + IHTTPConnection.RequestHeader.QueryString + Environment.NewLine +
                                       "Protocol => " + IHTTPConnection.RequestHeader.ProtocolName + Environment.NewLine +
                                       "Version => " + IHTTPConnection.RequestHeader.ProtocolVersion + Environment.NewLine +
                                       Environment.NewLine + Environment.NewLine +
                                       IHTTPConnection.ResponseHeader.HTTPStatusCode).ToUTF8Bytes()
                };

        }

        #endregion

        #region (protected) TryGetParameter_UInt64(Name, out HTTPResult)

        /// <summary>
        /// Try to return a single optional HTTP parameter as UInt64.
        /// </summary>
        /// <param name="Name">The name of the parameter.</param>
        /// <param name="HTTPResult">The HTTPResult.</param>
        /// <returns>True if the parameter exist; False otherwise.</returns>
        protected Boolean TryGetParameter_UInt64_(String Name, out HTTPResult<UInt64> HTTPResult)
        {

            List<String> _StringValues = null;

            if (IHTTPConnection.RequestHeader.QueryString != null)
                if (IHTTPConnection.RequestHeader.QueryString.TryGetValue(Name, out _StringValues))
                {

                    UInt64 _Value;

                    if (UInt64.TryParse(_StringValues[0], out _Value))
                    {
                        HTTPResult = new HTTPResult<UInt64>(_Value);
                        return true;
                    }

                    else
                    {
                        HTTPResult = new HTTPResult<UInt64>(IHTTPConnection.RequestHeader, HTTPStatusCode.BadRequest, "The given optional parameter '" + Name + "' is invalid!");
                        return true;
                    }

                }

            HTTPResult = new HTTPResult<UInt64>(default(UInt64));
            return false;

        }

        #endregion

        #region (protected) TryGetParameter_UInt64(Name, out HTTPResult)

        /// <summary>
        /// Try to return a single optional HTTP parameter as UInt64.
        /// </summary>
        /// <param name="Name">The name of the parameter.</param>
        /// <param name="HTTPResult">The HTTPResult.</param>
        /// <returns>True if the parameter exist; False otherwise.</returns>
        protected Boolean TryGetParameter_UInt64(String Name, out UInt64 Result)
        {

            List<String> _StringValues = null;

            if (IHTTPConnection.RequestHeader.QueryString != null)
                if (IHTTPConnection.RequestHeader.QueryString.TryGetValue(Name, out _StringValues))
                    if (_StringValues.Any())
                    {

                        UInt64 _Value;

                        if (UInt64.TryParse(_StringValues[0], out _Value))
                        {
                            Result = _Value;
                            return true;
                        }

                    }

            Result = default(UInt64);
            return false;

        }

        #endregion

        #region (protected) TryGetParameter_String(Name, out HTTPResult)

        /// <summary>
        /// Try to return a single optional HTTP parameter as UInt64.
        /// </summary>
        /// <param name="Name">The name of the parameter.</param>
        /// <param name="HTTPResult">The HTTPResult.</param>
        /// <returns>True if the parameter exist; False otherwise.</returns>
        protected Boolean TryGetParameter_String(String Name, out String Result)
        {

            List<String> _StringValues = null;

            if (IHTTPConnection.RequestHeader.QueryString != null)
                if (IHTTPConnection.RequestHeader.QueryString.TryGetValue(Name.ToLower(), out _StringValues))
                    if (_StringValues.Any())
                        if (_StringValues[0] != null && _StringValues[0] != "")
                        {
                            Result = _StringValues[0];
                            return true;
                        }

            Result = default(String);
            return false;

        }

        #endregion

        #region (protected) GetRequestBodyString(HTTPContentType)

        protected HTTPResult<String> GetRequestBodyAsUTF8String(HTTPContentType HTTPContentType)
        {

            if (IHTTPConnection.RequestHeader.ContentType != HTTPContentType)
                return new HTTPResult<String>(IHTTPConnection.RequestHeader, HTTPStatusCode.BadRequest);

            if (IHTTPConnection.RequestBody == null || IHTTPConnection.RequestBody.Length == 0)
                return new HTTPResult<String>(IHTTPConnection.RequestHeader, HTTPStatusCode.BadRequest);

            var RequestBodyString = IHTTPConnection.RequestBody.ToUTF8String();

            if (RequestBodyString.IsNullOrEmpty())
                return new HTTPResult<String>(IHTTPConnection.RequestHeader, HTTPStatusCode.BadRequest);

            return new HTTPResult<String>(RequestBodyString);

        }

        #endregion


        #region (protected) ParseCallbackParameter()

        /// <summary>
        /// Parse and check the parameter CALLBACK.
        /// </summary>
        protected void ParseCallbackParameter()
        {

            String _Callback;

            if (TryGetParameter_String(Tokens.CALLBACK, out _Callback))
                Callback.Value = new Regex("[^a-zA-Z0-9_]").Replace(_Callback, "");

        }

        #endregion

        #region (protected) ParseSkipParameter()

        /// <summary>
        /// Parse and check the parameter SKIP.
        /// </summary>
        protected void ParseSkipParameter()
        {

            UInt64 _Skip;

            if (TryGetParameter_UInt64(Tokens.SKIP, out _Skip))
                Skip.Value = _Skip;

        }

        #endregion

        #region (protected) ParseTakeParameter()

        /// <summary>
        /// Parse and check the parameter TAKE.
        /// </summary>
        protected void ParseTakeParameter()
        {

            UInt64 _Take;

            if (TryGetParameter_UInt64(Tokens.TAKE, out _Take))
                Take.Value = _Take;

            if (Take.Value == 0)
                Take.Value = 25;

        }

        #endregion


        #region GetResources(ResourceName)

        /// <summary>
        /// Returns internal resources embedded within the assembly.
        /// </summary>
        /// <param name="ResourceName">The path and name of the resource.</param>
        public HTTPResponse GetResources(String ResourceName)
        {

            #region Initial checks

            if (CallingAssembly == null)
                throw new ArgumentNullException("The calling assembly must not be null! Please add the line 'this.CallingAssembly = Assembly.GetExecutingAssembly();' to your constructor(s)!");

            #endregion

            #region Data

            var _AllResources = CallingAssembly.GetManifestResourceNames();

            ResourceName = ResourceName.Replace('/', '.');

            #endregion

            #region Return internal assembly resources...

            if (_AllResources.Contains(this.ResourcePath + ResourceName))
            {

                var _ResourceContent = CallingAssembly.GetManifestResourceStream(this.ResourcePath + ResourceName);

                HTTPContentType _ResponseContentType = null;

                // Get the apropriate content type based on the suffix of the requested resource
                switch (ResourceName.Remove(0, ResourceName.LastIndexOf(".") + 1))
                {
                    case "htm":  _ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "html": _ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "css":  _ResponseContentType = HTTPContentType.CSS_UTF8;        break;
                    case "gif":  _ResponseContentType = HTTPContentType.GIF;             break;
                    case "jpg":  _ResponseContentType = HTTPContentType.JPEG;            break;
                    case "jpeg": _ResponseContentType = HTTPContentType.JPEG;            break;
                    case "png":  _ResponseContentType = HTTPContentType.PNG;             break;
                    case "ico":  _ResponseContentType = HTTPContentType.ICO;             break;
                    case "swf":  _ResponseContentType = HTTPContentType.SWF;             break;
                    case "js":   _ResponseContentType = HTTPContentType.JAVASCRIPT_UTF8; break;
                    case "txt":  _ResponseContentType = HTTPContentType.TEXT_UTF8;       break;
                    default:     _ResponseContentType = HTTPContentType.OCTETSTREAM;     break;
                }

                return
                    new HTTPResponseBuilder()
                        {
                            HTTPStatusCode = HTTPStatusCode.OK,
                            ContentType    = _ResponseContentType,
                            ContentLength  = (UInt64) _ResourceContent.Length,
                            CacheControl   = "max-age=600",
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
                {

                    _ResourceContent = this.CallingAssembly.GetManifestResourceStream(this.ResourcePath + ".errorpages.Error404.html");

                    return new HTTPResponseBuilder()
                        {
                            HTTPStatusCode = HTTPStatusCode.NotFound,
                            ContentType    = HTTPContentType.HTML_UTF8,
                            CacheControl   = "no-cache",
                            Connection     = "close",
                            ContentStream  = _ResourceContent
                        };

                }

                else
                    return new HTTPResult<Object>(IHTTPConnection.RequestHeader, HTTPStatusCode.NotFound).Error;

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
