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
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using de.ahzf.Illias.Commons;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    public static class HTTPErrors
    {

        #region HTTPErrorResponse(StatusCode, Reasons = null)

        /// <summary>
        /// Return a HTTP error response using the best-matching content type.
        /// </summary>
        /// <param name="HTTPRequest">The HTTP request.</param>
        /// <param name="StatusCode">A HTTP status code.</param>
        /// <param name="Reasons">Optional application side reasons for this error.</param>
        public static HTTPResponse HTTPErrorResponse(HTTPRequest HTTPRequest, HTTPStatusCode StatusCode, String Reasons = null)
        {

            #region Initial checks

            if (StatusCode == null)
                return HTTPErrorResponse(HTTPRequest, HTTPStatusCode.InternalServerError, "Calling the HTTPError lead to an error!");

            var Content     = String.Empty;
            var ContentType = HTTPRequest.Accept.BestMatchingContentType(HTTPContentType.JSON_UTF8,
                                                                         HTTPContentType.HTML_UTF8,
                                                                         HTTPContentType.TEXT_UTF8,
                                                                         HTTPContentType.XML_UTF8);

            #endregion

            #region JSON_UTF8

            // {
            //     "error": {
            //         "code"    : 400
            //         "message" : "Bad Request"
            //         "reason"  : "The first paramter is not a valid number!"
            //     }
            // }
            if (ContentType == HTTPContentType.JSON_UTF8)
                Content = (Reasons == null) ? "{ \"error\": { \"code\" : " + StatusCode.Code + ", \"message\" : \"" + StatusCode.Name + "\" } }" :
                                              "{ \"error\": { \"code\" : " + StatusCode.Code + ", \"message\" : \"" + StatusCode.Name + "\", \"reasons\" : \"" + Reasons + "\" } }";

            #endregion

            #region HTML_UTF8

            //<!doctype html>
            //<html>
            //  <head>
            //    <meta charset="UTF-8">
            //    <title>Error 400 - Bad Request</title>
            //  </head>
            //  <body>
            //    <h1>Error 400 - Bad Request</h1>
            //    The first paramter is not a valid number!
            //  </body>
            //</html>
            else if (ContentType == HTTPContentType.HTML_UTF8)
                Content = (Reasons == null) ? "<!doctype html><html><head><meta charset=\"UTF-8\"><title>Error " + StatusCode.Code + " - " + StatusCode.Name + "</title></head><body><h1>Error " + StatusCode.Code + " - " + StatusCode.Name + "</h1></body></html>" :
                                              "<!doctype html><html><head><meta charset=\"UTF-8\"><title>Error " + StatusCode.Code + " - " + StatusCode.Name + "</title></head><body><h1>Error " + StatusCode.Code + " - " + StatusCode.Name + "</h1>" + Reasons + "</body></html>";

            #endregion

            #region TEXT_UTF8

            // Error 400 - Bad Request
            // The first paramter is not a valid number!
            else if (ContentType == HTTPContentType.TEXT_UTF8)
                Content = (Reasons == null) ? "Error " + StatusCode.Code + " - " + StatusCode.Name :
                                              "Error " + StatusCode.Code + " - " + StatusCode.Name + Environment.NewLine + Reasons;

            #endregion

            #region XML_UTF8

            // <?xml version="1.0" encoding="UTF-8"?>
            // <error>
            //     <code>400</code>
            //     <message>Bad Request</message>
            //     <reason>The first paramter is not a valid number!</message>
            // </error>
            else if (ContentType == HTTPContentType.XML_UTF8)
                Content = (Reasons == null) ? "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><code>" + StatusCode.Code + "</code><message>" + StatusCode.Name + "</message></error></xml>" :
                                              "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><code>" + StatusCode.Code + "</code><message>" + StatusCode.Name + "</message><reasons>" + Reasons + "</reasons></error></xml>";

            #endregion

            return new HTTPResponseBuilder()
            {
                HTTPStatusCode = StatusCode,
                CacheControl   = "no-cache",
                Connection     = "close",
                Content        = Content.ToUTF8Bytes()
            };

        }

        #endregion

    }

    public static class HTTPTools
    {

        #region MovedPermanently(Location)

        /// <summary>
        /// Return a HTTP response redirecting to the given location permanently.
        /// </summary>
        /// <param name="Location">The location of the redirect.</param>
        public static HTTPResponse MovedPermanently(String Location)
        {

            #region Initial checks

            if (Location == null || Location == "")
                throw new ArgumentNullException("Location", "The parameter 'Location' must not be null or empty!");

            #endregion

            return new HTTPResponseBuilder()
            {
                HTTPStatusCode = HTTPStatusCode.MovedPermanently,
                CacheControl   = "no-cache",
                Location       = Location
            };

        }

        #endregion

        #region MovedTemporarily(Location)

        /// <summary>
        /// Return a HTTP response redirecting to the given location temporarily.
        /// </summary>
        /// <param name="Location">The location of the redirect.</param>
        public static HTTPResponse MovedTemporarily(String Location)
        {

            #region Initial checks

            if (Location == null || Location == "")
                throw new ArgumentNullException("Location", "The parameter 'Location' must not be null or empty!");

            #endregion

            return new HTTPResponseBuilder()
            {
                HTTPStatusCode = HTTPStatusCode.TemporaryRedirect,
                CacheControl   = "no-cache",
                Location       = Location
            };

        }

        #endregion

    }


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

            if (IHTTPConnection.InHTTPRequest.QueryString != null)
                if (IHTTPConnection.InHTTPRequest.QueryString.TryGetValue(Name, out _StringValues))
                {

                    UInt64 _Value;

                    if (UInt64.TryParse(_StringValues[0], out _Value))
                    {
                        HTTPResult = new HTTPResult<UInt64>(_Value);
                        return true;
                    }

                    else
                    {
                        HTTPResult = new HTTPResult<UInt64>(IHTTPConnection.InHTTPRequest, HTTPStatusCode.BadRequest, "The given optional parameter '" + Name + "' is invalid!");
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

            if (IHTTPConnection.InHTTPRequest.QueryString != null)
                if (IHTTPConnection.InHTTPRequest.QueryString.TryGetValue(Name, out _StringValues))
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

            if (IHTTPConnection.InHTTPRequest.QueryString != null)
                if (IHTTPConnection.InHTTPRequest.QueryString.TryGetValue(Name, out _StringValues))
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
                    case "jpg":  _ResponseContentType = HTTPContentType.JPG;             break;
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
                    return HTTPErrors.HTTPErrorResponse(IHTTPConnection.InHTTPRequest, HTTPStatusCode.NotFound);

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
