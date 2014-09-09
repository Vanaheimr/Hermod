/*
 * Copyright (c) 2011-2014 Achim 'ahzf' Friedland <achim@graphdefined.com>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 * 
 * You may obtain a copy of the License at
 *   http://www.gnu.org/licenses/gpl.html
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 */

#region Usings

using System;
using System.Linq;
using System.Reflection;

using eu.Vanaheimr.Illias.Commons;
using System.IO;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Standard handlers for HTTP servers.
    /// </summary>
    public static partial class HTTPStandardHandlers
    {

        #region RegisterRAWRequestHandler(this HTTPServer, URITemplate)

        /// <summary>
        /// Return the RAW request header.
        /// </summary>
        public static void RegisterRAWRequestHandler(this HTTPServer  HTTPServer,
                                                     String           URITemplate,
                                                     HTTPMethod       HTTPMethod = null)
        {

            HTTPServer.AddMethodCallback(HTTPMethod:    HTTPMethod != null ? HTTPMethod : HTTPMethod.GET,
                                         URITemplate:   URITemplate,
                                         HTTPDelegate:  Request => {

                                             return new HTTPResponseBuilder() {

                                                 HTTPStatusCode = HTTPStatusCode.OK,
                                                 CacheControl   = "no-cache",
                                                 Connection     = "close",
                                                 ContentType    = HTTPContentType.TEXT_UTF8,
                                                 Content        = ("Incoming http connection from '" + Request.RemoteSocket + "'" +
                                                                    Environment.NewLine + Environment.NewLine +
                                                                    Request.RawHTTPHeader +
                                                                    Environment.NewLine + Environment.NewLine +
                                                                    "Method => "         + Request.HTTPMethod      + Environment.NewLine +
                                                                    "URL => "            + Request.UrlPath         + Environment.NewLine +
                                                                    "QueryString => "    + Request.QueryString     + Environment.NewLine +
                                                                    "Protocol => "       + Request.ProtocolName    + Environment.NewLine +
                                                                    "Version => "        + Request.ProtocolVersion + Environment.NewLine).ToUTF8Bytes()

                                             };

            });

        }

        #endregion

        #region RegisterResourcesFile(this HTTPServer, URITemplate, ResourceAssembly, ResourceFilename, ResponseContentType = null, CacheControl = "no-cache")

        /// <summary>
        /// Returns internal resources embedded within the given assembly.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="URITemplate">An URI template.</param>
        /// <param name="ResourceAssembly">The assembly where the resources are located.</param>
        /// <param name="ResourceFilename">The path to the file within the assembly.</param>
        /// <param name="ContentType">Set the HTTP MIME content-type of the file. If null try to autodetect the content type based on the filename extention.</param>
        public static void RegisterResourcesFile(this HTTPServer  HTTPServer,
                                                 String           URITemplate,
                                                 Assembly         ResourceAssembly,
                                                 String           ResourceFilename,
                                                 HTTPContentType  ResponseContentType  = null,
                                                 String           CacheControl         = "no-cache")
        {

            #region Get the apropriate content type based on the suffix of the requested resource

            if (ResponseContentType == null)
                switch (ResourceFilename.Remove(0, ResourceFilename.LastIndexOf(".") + 1))
                {
                    case "htm":  ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "html": ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "css":  ResponseContentType = HTTPContentType.CSS_UTF8;        break;
                    case "gif":  ResponseContentType = HTTPContentType.GIF;             break;
                    case "jpg":  ResponseContentType = HTTPContentType.JPEG;            break;
                    case "jpeg": ResponseContentType = HTTPContentType.JPEG;            break;
                    case "png":  ResponseContentType = HTTPContentType.PNG;             break;
                    case "ico":  ResponseContentType = HTTPContentType.ICO;             break;
                    case "swf":  ResponseContentType = HTTPContentType.SWF;             break;
                    case "js":   ResponseContentType = HTTPContentType.JAVASCRIPT_UTF8; break;
                    case "txt":  ResponseContentType = HTTPContentType.TEXT_UTF8;       break;
                    default:     ResponseContentType = HTTPContentType.OCTETSTREAM;     break;
                }

            #endregion

            HTTPServer.AddMethodCallback(HTTPMethod.GET,
                                         URITemplate + (URITemplate.EndsWith("/") ? "{ResourceName}" : "/{ResourceName}"),
                                         HTTPContentType: ResponseContentType,
                                         HTTPDelegate: Request => {

                                             var FileStream = ResourceAssembly.GetManifestResourceStream(ResourceFilename);

                                             if (FileStream != null)
                                                 return new HTTPResponseBuilder() {
                                                     HTTPStatusCode = HTTPStatusCode.OK,
                                                     ContentType    = ResponseContentType,
                                                     ContentStream  = FileStream,
                                                     CacheControl   = CacheControl,
                                                     Connection     = "close",
                                                 };

                                             else
                                             {

                                                 #region Try to find a appropriate customized errorpage...

                                                 Stream ErrorStream = null;

                                                 Request.BestMatchingAcceptType = Request.Accept.BestMatchingContentType(new HTTPContentType[] { HTTPContentType.HTML_UTF8, HTTPContentType.TEXT_UTF8 });

                                                 if (Request.BestMatchingAcceptType == HTTPContentType.HTML_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.HTML_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.TEXT_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.TEXT_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.txt");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.JSON_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.JSON_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.js");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.XML_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.XML_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.xml");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.ALL)
                                                 {
                                                     ResponseContentType = HTTPContentType.HTML_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                                                 }

                                                 if (ErrorStream != null)
                                                     return new HTTPResponseBuilder() {
                                                         HTTPStatusCode = HTTPStatusCode.NotFound,
                                                         ContentType    = ResponseContentType,
                                                         ContentStream  = ErrorStream,
                                                         CacheControl   = "no-cache",
                                                         Connection     = "close",
                                                     };

                                                 #endregion

                                                 #region ...or send a default error page!

                                                 else
                                                     return new HTTPResponseBuilder() {
                                                         HTTPStatusCode = HTTPStatusCode.NotFound,
                                                         CacheControl   = "no-cache",
                                                         Connection     = "close",
                                                     };

                                                 #endregion

                                             }

                                         });

            return;

        }

        #endregion

        #region RegisterResourcesFolder(this HTTPServer, URITemplate, ResourcePath)

        /// <summary>
        /// Returns internal resources embedded within the given assembly.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="URITemplate">An URI template.</param>
        /// <param name="ResourceAssembly">The assembly where the resources are located.</param>
        /// <param name="ResourcePath">The path to the file within the assembly.</param>
        public static void RegisterResourcesFolder(this HTTPServer  HTTPServer,
                                                   String           URITemplate,
                                                   Assembly         ResourceAssembly,
                                                   String           ResourcePath,
                                                   String           DefaultFilename = "index.html")
        {

            HTTPServer.AddMethodCallback(HTTPMethod.GET,
                                         URITemplate + (URITemplate.EndsWith("/") ? "{ResourceName}" : "/{ResourceName}"),
                                         HTTPDelegate: Request => {

                                             HTTPContentType ResponseContentType = null;

                                             var FilePath = (Request.ParsedQueryParameters != null)
                                                                ? Request.ParsedQueryParameters.First().Replace("/", ".")
                                                                : DefaultFilename.Replace("/", ".");

                                             var FileStream = ResourceAssembly.GetManifestResourceStream(ResourcePath + "." + FilePath);

                                             if (FileStream != null)
                                             {

                                                 #region Choose HTTP Content Type based on the file name extention...

                                                 var FileName = FilePath.Substring(FilePath.LastIndexOf("/") + 1);

                                                 // Get the apropriate content type based on the suffix of the requested resource
                                                 switch (FileName.Remove(0, FileName.LastIndexOf(".") + 1))
                                                 {
                                                     case "htm":  ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                                                     case "html": ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                                                     case "css":  ResponseContentType = HTTPContentType.CSS_UTF8;        break;
                                                     case "gif":  ResponseContentType = HTTPContentType.GIF;             break;
                                                     case "jpg":  ResponseContentType = HTTPContentType.JPEG;            break;
                                                     case "jpeg": ResponseContentType = HTTPContentType.JPEG;            break;
                                                     case "png":  ResponseContentType = HTTPContentType.PNG;             break;
                                                     case "ico":  ResponseContentType = HTTPContentType.ICO;             break;
                                                     case "swf":  ResponseContentType = HTTPContentType.SWF;             break;
                                                     case "js":   ResponseContentType = HTTPContentType.JAVASCRIPT_UTF8; break;
                                                     case "txt":  ResponseContentType = HTTPContentType.TEXT_UTF8;       break;
                                                     default:     ResponseContentType = HTTPContentType.OCTETSTREAM;     break;
                                                 }

                                                 #endregion

                                                 #region Create HTTP Response

                                                 return new HTTPResponseBuilder() {
                                                     HTTPStatusCode  = HTTPStatusCode.OK,
                                                     ContentType     = ResponseContentType,
                                                     ContentStream   = FileStream,
                                                     CacheControl    = "no-cache",
                                                     Connection      = "close",
                                                 };

                                                 #endregion

                                             }

                                             else
                                             {

                                                 #region Try to find a appropriate customized errorpage...

                                                 Stream ErrorStream = null;

                                                 Request.BestMatchingAcceptType = Request.Accept.BestMatchingContentType(new HTTPContentType[] { HTTPContentType.HTML_UTF8, HTTPContentType.TEXT_UTF8 });

                                                 if (Request.BestMatchingAcceptType == HTTPContentType.HTML_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.HTML_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.TEXT_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.TEXT_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.txt");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.JSON_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.JSON_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.js");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.XML_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.XML_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.xml");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.ALL)
                                                 {
                                                     ResponseContentType = HTTPContentType.HTML_UTF8;
                                                     ErrorStream         = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                                                 }

                                                 if (ErrorStream != null)
                                                     return new HTTPResponseBuilder() {
                                                         HTTPStatusCode = HTTPStatusCode.NotFound,
                                                         ContentType    = ResponseContentType,
                                                         ContentStream  = ErrorStream,
                                                         CacheControl   = "no-cache",
                                                         Connection     = "close",
                                                     };

                                                 #endregion

                                                 #region ...or send a default error page!

                                                 else
                                                     return new HTTPResponseBuilder() {
                                                         HTTPStatusCode = HTTPStatusCode.NotFound,
                                                         CacheControl   = "no-cache",
                                                         Connection     = "close",
                                                     };

                                                 #endregion

                                             }

                                         });

            return;

        }

        #endregion

        #region RegisterMovedTemporarilyHandler(this HTTPServer, URITemplate, URITarget)

        /// <summary>
        /// Register a MovedTemporarily handler.
        /// </summary>
        public static void RegisterMovedTemporarilyHandler(this HTTPServer  HTTPServer,
                                                           String           URITemplate,
                                                           String           URITarget)
        {

            HTTPServer.AddMethodCallback(HTTPMethod.GET,
                                         URITemplate,
                                         HTTPDelegate: Request => HTTPTools.MovedTemporarily(URITarget));

        }

        #endregion

        //egion GET /CombinedLog

        //        /// <summary>
        //        /// Get a combined list of all log events.
        //        /// </summary>
        //        /// <httpparam name="SKIP">Skip the given number of entries from the beginning of the result set.</httpparam>
        //        /// <httpparam name="TAKE">Return only the given number of entries from the result set.</httpparam>
        //        /// <example>curl -H "Accept: application/json" http://127.0.0.1:2002/CombinedLog</example>
        //        public override HTTPResponse GET_CombinedLog()
        //        {
        //            return HTTPTools.MovedTemporarily("/CombinedLog/index.html");
        //        }

        //        #endregion

    }

}
