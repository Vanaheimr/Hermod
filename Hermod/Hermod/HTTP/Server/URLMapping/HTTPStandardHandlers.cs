/*
 * Copyright (c) 2011-2023 Achim Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Reflection;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Standard handlers for HTTP servers.
    /// </summary>
    public static class HTTPStandardHandlers
    {

        #region RegisterRAWRequestHandler      (this HTTPServer, HTTPAPI, Hostname, URLTemplate, Method = null)

        /// <summary>
        /// Return the RAW request header.
        /// </summary>
        /// <param name="HTTPServer">The HTTP server.</param>
        /// <param name="HTTPAPI">The HTTP API.</param>
        /// <param name="Hostname">A HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="Method">An optional HTTP method, default is "GET".</param>
        public static void RegisterRAWRequestHandler(this HTTPServer  HTTPServer,
                                                     HTTPAPI          HTTPAPI,
                                                     HTTPHostname     Hostname,
                                                     HTTPPath         URLTemplate,
                                                     HTTPMethod?      Method   = null)

            => HTTPServer?.AddMethodCallback(
                   HTTPAPI,
                   Hostname,
                   HTTPMethod:    Method ?? HTTPMethod.GET,
                   URLTemplate:   URLTemplate,
                   HTTPDelegate:  Request => {

                       return Task.FromResult(
                           new HTTPResponse.Builder(Request) {

                               HTTPStatusCode  = HTTPStatusCode.OK,
                               Server          = HTTPServer.DefaultServerName,
                               Date            = Timestamp.Now,
                               CacheControl    = "no-cache",
                               Connection      = "close",
                               ContentType     = HTTPContentType.TEXT_UTF8,
                               Content         = ("Incoming http connection from '" + Request.HTTPSource + "'" +
                                                   Environment.NewLine + Environment.NewLine +
                                                   Request.RawHTTPHeader +
                                                   Environment.NewLine + Environment.NewLine +
                                                   "Method => "         + Request.HTTPMethod      + Environment.NewLine +
                                                   "URL => "            + Request.Path.ToString()  + Environment.NewLine +
                                                   "QueryString => "    + Request.QueryString     + Environment.NewLine +
                                                   "Protocol => "       + Request.ProtocolName    + Environment.NewLine +
                                                   "Version => "        + Request.ProtocolVersion + Environment.NewLine).ToUTF8Bytes()

                           }.AsImmutable
                       );

                });

        #endregion

        #region RegisterMovedTemporarilyHandler(this HTTPServer, HTTPAPI, Hostname, URLTemplate, Location)

        /// <summary>
        /// Register a MovedTemporarily handler.
        /// </summary>
        /// <param name="HTTPServer">The HTTP server.</param>
        /// <param name="HTTPAPI">The HTTP API.</param>
        /// <param name="Hostname">A HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="Location">The HTTP URL to redirect to.</param>
        public static void RegisterMovedTemporarilyHandler(this HTTPServer  HTTPServer,
                                                           HTTPAPI          HTTPAPI,
                                                           HTTPHostname     Hostname,
                                                           HTTPPath         URLTemplate,
                                                           Location         Location)
        {

            HTTPServer.AddMethodCallback(
                HTTPAPI,
                Hostname,
                HTTPMethod.GET,
                URLTemplate,
                HTTPDelegate: Request => Task.FromResult(
                                             HTTPTools.MovedTemporarily(
                                                 Request,
                                                 Location
                                             )
                                         )
            );

        }

        #endregion


        #region RegisterResourcesFile  (this HTTPServer,           URLTemplate, ResourceAssembly, ResourceFilename, ResponseContentType = null, CacheControl = "no-cache")

        /// <summary>
        /// Returns internal resources embedded within the given assembly.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="ResourceAssembly">The assembly where the resources are located.</param>
        /// <param name="ResourceFilename">The path to the file within the assembly.</param>
        /// <param name="ResponseContentType">Set the HTTP MIME content-type of the file. If null try to autodetect the content type based on the filename extension.</param>
        /// <param name="CacheControl">Set the HTTP cache control response header.</param>
        public static void RegisterResourcesFile(this IHTTPServer  HTTPServer,
                                                 HTTPAPI           HTTPAPI,
                                                 HTTPHostname      Hostname,
                                                 HTTPPath          URLTemplate,
                                                 Assembly          ResourceAssembly,
                                                 String            ResourceFilename,
                                                 HTTPContentType?  ResponseContentType   = null,
                                                 String            CacheControl          = "no-cache")
        {

            // Get the appropriate content type based on the suffix of the requested resource
            ResponseContentType ??= ResourceFilename.Remove(0, ResourceFilename.LastIndexOf(".") + 1) switch {
                                        "htm"  => HTTPContentType.HTML_UTF8,
                                        "html" => HTTPContentType.HTML_UTF8,
                                        "css"  => HTTPContentType.CSS_UTF8,
                                        "gif"  => HTTPContentType.GIF,
                                        "jpg"  => HTTPContentType.JPEG,
                                        "jpeg" => HTTPContentType.JPEG,
                                        "svg"  => HTTPContentType.SVG,
                                        "png"  => HTTPContentType.PNG,
                                        "ico"  => HTTPContentType.ICO,
                                        "swf"  => HTTPContentType.SWF,
                                        "js"   => HTTPContentType.JAVASCRIPT_UTF8,
                                        "txt"  => HTTPContentType.TEXT_UTF8,
                                        "xml"  => HTTPContentType.XML_UTF8,
                                        _      => HTTPContentType.OCTETSTREAM,
                                    };


            HTTPServer.AddMethodCallback(HTTPAPI,
                                         Hostname,
                                         HTTPMethod.GET,
                                         URLTemplate,
                                         HTTPContentType: ResponseContentType,
                                         HTTPDelegate: async Request => {

                                             var fileStream = ResourceAssembly.GetManifestResourceStream(ResourceFilename);

                                             if (fileStream != null)
                                                 return new HTTPResponse.Builder(Request) {
                                                     HTTPStatusCode  = HTTPStatusCode.OK,
                                                     Server          = HTTPServer.DefaultServerName,
                                                     Date            = Timestamp.Now,
                                                     ContentType     = ResponseContentType,
                                                     ContentStream   = fileStream,
                                                     CacheControl    = CacheControl,
                                                     Connection      = "close",
                                                 };

                                             else
                                             {

                                                 #region Try to find a appropriate customized errorpage...

                                                 Stream? errorStream = null;

                                                 Request.BestMatchingAcceptType = Request.Accept.BestMatchingContentType(new HTTPContentType[] { HTTPContentType.HTML_UTF8, HTTPContentType.TEXT_UTF8 });

                                                 if (Request.BestMatchingAcceptType == HTTPContentType.HTML_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.HTML_UTF8;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.TEXT_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.TEXT_UTF8;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.txt");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.JSON_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.JSON_UTF8;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.js");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.XML_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.XML_UTF8;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.xml");
                                                 }

                                                 else if (Request.BestMatchingAcceptType == HTTPContentType.ALL)
                                                 {
                                                     ResponseContentType = HTTPContentType.HTML_UTF8;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                                                 }

                                                 if (errorStream is not null)
                                                     return new HTTPResponse.Builder(Request) {
                                                         HTTPStatusCode = HTTPStatusCode.NotFound,
                                                         ContentType    = ResponseContentType,
                                                         ContentStream  = errorStream,
                                                         CacheControl   = "no-cache",
                                                         Connection     = "close",
                                                     };

                                                 #endregion

                                                 #region ...or send a default error page!

                                                 else
                                                     return new HTTPResponse.Builder(Request) {
                                                         HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                         Server          = HTTPServer.DefaultServerName,
                                                         Date            = Timestamp.Now,
                                                         CacheControl    = "no-cache",
                                                         Connection      = "close",
                                                     };

                                                 #endregion

                                             }

                                         }, AllowReplacement: URLReplacement.Fail);

        }

        #endregion

        #region RegisterResourcesFolder(this HTTPServer, Hostname, URLTemplate, ResourcePath, ResourceAssembly = null, DefaultFilename = "index.html", HTTPRealm = null, HTTPLogin = null, HTTPPassword = null)

        /// <summary>
        /// Returns internal resources embedded within the given assembly.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="ResourcePath">The path to the file within the assembly.</param>
        /// <param name="ResourceAssembly">Optionally the assembly where the resources are located (default: the calling assembly).</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        /// <param name="HTTPRealm">An optional realm for HTTP basic authentication.</param>
        /// <param name="HTTPLogin">An optional login for HTTP basic authentication.</param>
        /// <param name="HTTPPassword">An optional password for HTTP basic authentication.</param>
        public static void RegisterResourcesFolder(this IHTTPServer  HTTPServer,
                                                   HTTPAPI           HTTPAPI,
                                                   HTTPHostname      Hostname,
                                                   HTTPPath          URLTemplate,
                                                   String            ResourcePath,
                                                   Assembly?         ResourceAssembly  = null,
                                                   String            DefaultFilename   = "index.html",
                                                   String?           HTTPRealm         = null,
                                                   String?           HTTPLogin         = null,
                                                   String?           HTTPPassword      = null)
        {

            ResourceAssembly ??= Assembly.GetCallingAssembly();


            HTTPDelegate GetEmbeddedResources = async Request => {

                #region Check HTTP Basic Authentication

                if (HTTPLogin.IsNotNullOrEmpty() && HTTPPassword.IsNotNullOrEmpty())
                {

                    if (Request.Authorization is null                               ||
                      !(Request.Authorization is HTTPBasicAuthentication basicAuth) ||
                        basicAuth.Username    != HTTPLogin                          ||
                        basicAuth.Password    != HTTPPassword)
                    {
                        return new HTTPResponse.Builder(Request) {
                            HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                            Server           = HTTPServer.DefaultServerName,
                            Date             = Timestamp.Now,
                            WWWAuthenticate  = @"Basic realm=""" + HTTPRealm + @"""",
                            ContentType      = HTTPContentType.TEXT_UTF8,
                            Content          = "Unauthorized Access!".ToUTF8Bytes(),
                            Connection       = "close"
                        };
                    }

                }

                #endregion

                HTTPContentType? ResponseContentType = null;

                var filePath    = (Request.ParsedURLParameters != null && Request.ParsedURLParameters.Length > 0)
                                      ? Request.ParsedURLParameters.Last().Replace("/", ".")
                                      : DefaultFilename.Replace("/", ".");

                var fileStream  = ResourceAssembly.GetManifestResourceStream(ResourcePath + "." + filePath);

                if (fileStream is not null)
                {

                    #region Choose HTTP Content Type based on the file name extension...

                    var fileName = filePath.Substring(filePath.LastIndexOf("/") + 1);

                    // Get the appropriate content type based on the suffix of the requested resource
                    ResponseContentType = fileName.Remove(0, fileName.LastIndexOf(".") + 1) switch {
                                              "htm"  => HTTPContentType.HTML_UTF8,
                                              "html" => HTTPContentType.HTML_UTF8,
                                              "css"  => HTTPContentType.CSS_UTF8,
                                              "gif"  => HTTPContentType.GIF,
                                              "jpg"  => HTTPContentType.JPEG,
                                              "jpeg" => HTTPContentType.JPEG,
                                              "svg"  => HTTPContentType.SVG,
                                              "png"  => HTTPContentType.PNG,
                                              "ico"  => HTTPContentType.ICO,
                                              "swf"  => HTTPContentType.SWF,
                                              "js"   => HTTPContentType.JAVASCRIPT_UTF8,
                                              "txt"  => HTTPContentType.TEXT_UTF8,
                                              "xml"  => HTTPContentType.XML_UTF8,
                                              _      => HTTPContentType.OCTETSTREAM,
                                          };

                    #endregion

                    #region Create HTTP Response

                    return new HTTPResponse.Builder(Request) {
                        HTTPStatusCode  = HTTPStatusCode.OK,
                        Server          = HTTPServer.DefaultServerName,
                        Date            = Timestamp.Now,
                        ContentType     = ResponseContentType,
                        ContentStream   = fileStream,
                        CacheControl    = "public, max-age=300",
                        //Expires          = "Mon, 25 Jun 2015 21:31:12 GMT",
                        KeepAlive       = new KeepAliveType(TimeSpan.FromMinutes(5), 500),
                        Connection      = "Keep-Alive",
                    };

                    #endregion

                }

                else
                {

                    #region Try to find a appropriate customized errorpage...

                    Stream? errorStream = null;

                    Request.BestMatchingAcceptType = Request.Accept.BestMatchingContentType(new[] {
                                                                                                HTTPContentType.HTML_UTF8,
                                                                                                HTTPContentType.TEXT_UTF8
                                                                                            });

                    if (Request.BestMatchingAcceptType == HTTPContentType.HTML_UTF8) {
                        ResponseContentType  = HTTPContentType.HTML_UTF8;
                        errorStream          = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                    }

                    else if (Request.BestMatchingAcceptType == HTTPContentType.TEXT_UTF8) {
                        ResponseContentType  = HTTPContentType.TEXT_UTF8;
                        errorStream          = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.txt");
                    }

                    else if (Request.BestMatchingAcceptType == HTTPContentType.JSON_UTF8) {
                        ResponseContentType  = HTTPContentType.JSON_UTF8;
                        errorStream          = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.js");
                    }

                    else if (Request.BestMatchingAcceptType == HTTPContentType.XML_UTF8) {
                        ResponseContentType  = HTTPContentType.XML_UTF8;
                        errorStream          = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.xml");
                    }

                    else if (Request.BestMatchingAcceptType == HTTPContentType.ALL) {
                        ResponseContentType  = HTTPContentType.HTML_UTF8;
                        errorStream          = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                    }

                    if (errorStream is not null)
                        return new HTTPResponse.Builder(Request) {
                            HTTPStatusCode  = HTTPStatusCode.NotFound,
                            Server          = HTTPServer.DefaultServerName,
                            Date            = Timestamp.Now,
                            ContentType     = ResponseContentType,
                            ContentStream   = errorStream,
                            CacheControl    = "no-cache",
                            Connection      = "close",
                        };

                    #endregion

                    #region ...or send a default error page!

                    else
                        return new HTTPResponse.Builder(Request) {
                            HTTPStatusCode  = HTTPStatusCode.NotFound,
                            Server          = HTTPServer.DefaultServerName,
                            Date            = Timestamp.Now,
                            CacheControl    = "no-cache",
                            Connection      = "close",
                        };

                    #endregion

                }

            };


            // ~/map
            HTTPServer.AddMethodCallback(HTTPAPI,
                                         Hostname,
                                         HTTPMethod.GET,
                                         URLTemplate.EndsWith("/", StringComparison.InvariantCulture)
                                             ? URLTemplate.Substring(0, (Int32) URLTemplate.Length)
                                             : URLTemplate,
                                         HTTPContentType.ALL,
                                         HTTPDelegate: GetEmbeddedResources);

            //// ~/map/
            //HTTPServer.AddMethodCallback(Hostname,
            //                             HTTPMethod.GET,
            //                             URLTemplate + (URLTemplate.EndsWith("/", StringComparison.InvariantCulture) ? "" : "/"),
            //                             HTTPContentType.ALL,
            //                             HTTPDelegate: GetEmbeddedResources);

            // ~/map/file.name
            HTTPServer.AddMethodCallback(HTTPAPI,
                                         Hostname,
                                         HTTPMethod.GET,
                                         URLTemplate + (URLTemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),
                                         HTTPContentType.ALL,
                                         HTTPDelegate: GetEmbeddedResources);

        }

        #endregion



        #region RegisterFileSystemFile  (this HTTPAPI,             Hostname, URLTemplate, ResourceFilenameBuilder, DefaultFile = null, ResponseContentType = null, CacheControl = "no-cache")

        /// <summary>
        /// Returns a resource from the given file system location.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="ResourceFilenameBuilder">The path to the file within the assembly.</param>
        /// <param name="DefaultFile">If an error occures, return this file.</param>
        /// <param name="ResponseContentType">Set the HTTP MIME content-type of the file. If null try to autodetect the content type based on the filename extension.</param>
        /// <param name="CacheControl">Set the HTTP cache control response header.</param>
        public static void RegisterFileSystemFile(this HTTPAPI            HTTPAPI,
                                                  HTTPHostname            Hostname,
                                                  HTTPPath                URLTemplate,
                                                  Func<String[], String>  ResourceFilenameBuilder,
                                                  String?                 DefaultFile           = null,
                                                  HTTPContentType?        ResponseContentType   = null,
                                                  String                  CacheControl          = "no-cache")

            => HTTPAPI.HTTPServer.RegisterFileSystemFile(
                                      HTTPAPI,
                                      Hostname,
                                      URLTemplate,
                                      ResourceFilenameBuilder,
                                      DefaultFile,
                                      ResponseContentType,
                                      CacheControl
                                  );

        #endregion

        #region RegisterFileSystemFile  (this HTTPServer, HTTPAPI, Hostname, URLTemplate, ResourceFilenameBuilder, DefaultFile = null, ResponseContentType = null, CacheControl = "no-cache")

        /// <summary>
        /// Returns a resource from the given file system location.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="ResourceFilenameBuilder">The path to the file within the assembly.</param>
        /// <param name="DefaultFile">If an error occures, return this file.</param>
        /// <param name="ResponseContentType">Set the HTTP MIME content-type of the file. If null try to autodetect the content type based on the filename extension.</param>
        /// <param name="CacheControl">Set the HTTP cache control response header.</param>
        public static void RegisterFileSystemFile(this IHTTPServer        HTTPServer,
                                                  HTTPAPI                 HTTPAPI,
                                                  HTTPHostname            Hostname,
                                                  HTTPPath                URLTemplate,
                                                  Func<String[], String>  ResourceFilenameBuilder,
                                                  String?                 DefaultFile           = null,
                                                  HTTPContentType?        ResponseContentType   = null,
                                                  String                  CacheControl          = "no-cache")
        {

            #region Get the appropriate content type based on the suffix returned by the ResourceFilenameBuilder

                                                                                 // NumberOfTemplateParameters
            var resourceFilename = ResourceFilenameBuilder(Enumerable.Repeat("", URLTemplate.ToString().Count(c => c == '{')).ToArray());

            ResponseContentType ??= resourceFilename.Remove(0, resourceFilename.LastIndexOf(".") + 1) switch {
                                        "htm"  => HTTPContentType.HTML_UTF8,
                                        "html" => HTTPContentType.HTML_UTF8,
                                        "css"  => HTTPContentType.CSS_UTF8,
                                        "gif"  => HTTPContentType.GIF,
                                        "jpg"  => HTTPContentType.JPEG,
                                        "jpeg" => HTTPContentType.JPEG,
                                        "svg"  => HTTPContentType.SVG,
                                        "png"  => HTTPContentType.PNG,
                                        "ico"  => HTTPContentType.ICO,
                                        "swf"  => HTTPContentType.SWF,
                                        "js"   => HTTPContentType.JAVASCRIPT_UTF8,
                                        "txt"  => HTTPContentType.TEXT_UTF8,
                                        _      => HTTPContentType.OCTETSTREAM,
                                    };

            #endregion

            HTTPServer.AddMethodCallback(HTTPAPI,
                                         Hostname,
                                         HTTPMethod.GET,
                                         URLTemplate,
                                         HTTPContentType: ResponseContentType,
                                         HTTPDelegate: async Request => {

                                             var ResourceFilename = ResourceFilenameBuilder(Request.ParsedURLParameters);

                                             if (!File.Exists(ResourceFilename) && DefaultFile != null)
                                                 ResourceFilename = DefaultFile;

                                             if (File.Exists(ResourceFilename))
                                             {

                                                 var fileStream = File.OpenRead(ResourceFilename);

                                                 if (fileStream is not null)
                                                     return new HTTPResponse.Builder(Request) {
                                                         HTTPStatusCode  = HTTPStatusCode.OK,
                                                         Server          = HTTPServer.DefaultServerName,
                                                         Date            = Timestamp.Now,
                                                         ContentType     = ResponseContentType,
                                                         ContentStream   = fileStream,
                                                         CacheControl    = CacheControl,
                                                         Connection      = "close",
                                                     };

                                             }

                                             return new HTTPResponse.Builder(Request) {
                                                 HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                 Server          = HTTPServer.DefaultServerName,
                                                 Date            = Timestamp.Now,
                                                 CacheControl    = "no-cache",
                                                 Connection      = "close",
                                             };

                                         }, AllowReplacement: URLReplacement.Fail);

            return;

        }

        #endregion


        #region RegisterFileSystemFolder(this HTTPAPI,                Hostname, URLTemplate, ResourcePath, DefaultFilename = "index.html")

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPAPI">The HTTP API.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="ResourcePath">The path to the file within the local file system.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterFileSystemFolder(this HTTPAPI            HTTPAPI,
                                                    HTTPHostname            Hostname,
                                                    HTTPPath                URLTemplate,
                                                    Func<String[], String>  ResourcePath,
                                                    String                  DefaultFilename  = "index.html")

            => HTTPAPI.HTTPServer.RegisterFileSystemFolder(
                                      HTTPAPI,
                                      Hostname,
                                      URLTemplate,
                                      ResourcePath,
                                      DefaultFilename
                                  );

        #endregion

        #region RegisterFileSystemFolder(this HTTPExtAPI,             Hostname, URLTemplate, ResourcePath, DefaultFilename = "index.html")

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPExtAPI">The HTTP API.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="ResourcePath">The path to the file within the local file system.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterFileSystemFolder(this HTTPExtAPI         HTTPExtAPI,
                                                    HTTPHostname            Hostname,
                                                    HTTPPath                URLTemplate,
                                                    Func<String[], String>  ResourcePath,
                                                    String                  DefaultFilename  = "index.html")

            => HTTPExtAPI.HTTPServer.RegisterFileSystemFolder(
                                         HTTPExtAPI,
                                         Hostname,
                                         URLTemplate,
                                         ResourcePath,
                                         DefaultFilename
                                     );

        #endregion

        #region RegisterFileSystemFolder(this HTTPServer, HTTPAPI,    Hostname, URLTemplate, ResourcePath, DefaultFilename = "index.html")

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPServer">The HTTP server.</param>
        /// <param name="HTTPAPI">The HTTP API.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="ResourcePath">The path to the file within the local file system.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterFileSystemFolder(this IHTTPServer        HTTPServer,
                                                    HTTPAPI                 HTTPAPI,
                                                    HTTPHostname            Hostname,
                                                    HTTPPath                URLTemplate,
                                                    Func<String[], String>  ResourcePath,
                                                    String                  DefaultFilename  = "index.html")
        {

            HTTPServer.AddMethodCallback(
                HTTPAPI,
                Hostname,
                HTTPMethod.GET,
                URLTemplate + (URLTemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),
                HTTPDelegate: Request => {

                    try
                    {

                        HTTPContentType? ResponseContentType = null;

                        var numberOfTemplateParameters = URLTemplate.ToString().Count(c => c == '{');

                        var filePath    = (Request.ParsedURLParameters is not null && Request.ParsedURLParameters.Length > numberOfTemplateParameters)
                                              ? Request.ParsedURLParameters.Last().Replace('/', Path.DirectorySeparatorChar)
                                              : DefaultFilename.Replace('/', Path.DirectorySeparatorChar);

                        var fileStream  = File.OpenRead(ResourcePath(Request.ParsedURLParameters) +
                                                        Path.DirectorySeparatorChar +
                                                        filePath);

                        if (fileStream is null)
                            return Task.FromResult(
                                new HTTPResponse.Builder(Request) {
                                    HTTPStatusCode  = HTTPStatusCode.NotFound,
                                    Server          = HTTPServer.DefaultServerName,
                                    Date            = Timestamp.Now,
                                    CacheControl    = "no-cache",
                                    Connection      = "close",
                                }.AsImmutable);


                        #region Choose HTTP Content Type based on the file name extension...

                        var fileName = filePath.Substring(filePath.LastIndexOf("/") + 1);

                        // Get the appropriate content type based on the suffix of the requested resource
                        ResponseContentType = fileName.Remove(0, fileName.LastIndexOf(".") + 1) switch {
                                                  "htm"   => HTTPContentType.HTML_UTF8,
                                                  "html"  => HTTPContentType.HTML_UTF8,
                                                  "shtml" => HTTPContentType.HTML_UTF8,
                                                  "css"   => HTTPContentType.CSS_UTF8,
                                                  "gif"   => HTTPContentType.GIF,
                                                  "jpg"   => HTTPContentType.JPEG,
                                                  "jpeg"  => HTTPContentType.JPEG,
                                                  "svg"   => HTTPContentType.SVG,
                                                  "png"   => HTTPContentType.PNG,
                                                  "ico"   => HTTPContentType.ICO,
                                                  "swf"   => HTTPContentType.SWF,
                                                  "js"    => HTTPContentType.JAVASCRIPT_UTF8,
                                                  "txt"   => HTTPContentType.TEXT_UTF8,
                                                  _       => HTTPContentType.OCTETSTREAM,
                                              };

                        #endregion

                        #region Create HTTP Response

                        return Task.FromResult(
                           new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               Server          = HTTPServer.DefaultServerName,
                               Date            = Timestamp.Now,
                               ContentType     = ResponseContentType,
                               ContentStream   = fileStream,
                               CacheControl    = "public, max-age=300",
                               //Expires         = "Mon, 25 Jun 2015 21:31:12 GMT",
                               KeepAlive       = new KeepAliveType(TimeSpan.FromMinutes(15),
                                                                   500),
                               Connection      = "Keep-Alive",
                           }.AsImmutable);

                        #endregion

                    }
                    catch (Exception e)
                    {

                        return Task.FromResult(
                                   new HTTPResponse.Builder(Request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       Server          = HTTPServer.DefaultServerName,
                                       Date            = Timestamp.Now,
                                       ContentType     = HTTPContentType.JSON_UTF8,
                                       Content         = JSONObject.Create(new JProperty("message", e.Message)).ToUTF8Bytes(),
                                       CacheControl    = "no-cache",
                                       Connection      = "close",
                                   }.AsImmutable
                               );

                    }

               }, AllowReplacement: URLReplacement.Fail);

            return;

        }

        #endregion

        #region RegisterFileSystemFolder(this HTTPServer, HTTPExtAPI, Hostname, URLTemplate, ResourcePath, DefaultFilename = "index.html")

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPServer">The HTTP server.</param>
        /// <param name="HTTPExtAPI">The HTTP API.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="ResourcePath">The path to the file within the local file system.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterFileSystemFolder(this IHTTPServer        HTTPServer,
                                                    HTTPExtAPI              HTTPExtAPI,
                                                    HTTPHostname            Hostname,
                                                    HTTPPath                URLTemplate,
                                                    Func<String[], String>  ResourcePath,
                                                    String                  DefaultFilename  = "index.html")
        {

            HTTPServer.AddMethodCallback(
                HTTPExtAPI,
                Hostname,
                HTTPMethod.GET,
                URLTemplate + (URLTemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),
                HTTPDelegate: Request => {

                    #region Get HTTP user and its organizations

                    // Will return HTTP 401 Unauthorized, when the HTTP user is unknown!
                    if (!HTTPExtAPI.TryGetHTTPUser(
                                        Request,
                                        out var httpUser,
                                        out var httpOrganizations,
                                        out var response,
                                        Recursive: true
                                    ))
                    {
                        return Task.FromResult(response!.AsImmutable);
                    }

                    #endregion


                    try
                    {

                        HTTPContentType? ResponseContentType = null;

                        var numberOfTemplateParameters = URLTemplate.ToString().Count(c => c == '{');

                        var filePath    = (Request.ParsedURLParameters is not null && Request.ParsedURLParameters.Length > numberOfTemplateParameters)
                                              ? Request.ParsedURLParameters.Last().Replace('/', Path.DirectorySeparatorChar)
                                              : DefaultFilename.Replace('/', Path.DirectorySeparatorChar);

                        var fileStream  = File.OpenRead(ResourcePath(Request.ParsedURLParameters) +
                                                        Path.DirectorySeparatorChar +
                                                        filePath);

                        if (fileStream is null)
                            return Task.FromResult(
                                new HTTPResponse.Builder(Request) {
                                    HTTPStatusCode  = HTTPStatusCode.NotFound,
                                    Server          = HTTPServer.DefaultServerName,
                                    Date            = Timestamp.Now,
                                    CacheControl    = "no-cache",
                                    Connection      = "close",
                                }.AsImmutable);


                        #region Choose HTTP Content Type based on the file name extension...

                        var fileName = filePath.Substring(filePath.LastIndexOf("/") + 1);

                        // Get the appropriate content type based on the suffix of the requested resource
                        ResponseContentType = fileName.Remove(0, fileName.LastIndexOf(".") + 1) switch {
                                                  "htm"   => HTTPContentType.HTML_UTF8,
                                                  "html"  => HTTPContentType.HTML_UTF8,
                                                  "shtml" => HTTPContentType.HTML_UTF8,
                                                  "css"   => HTTPContentType.CSS_UTF8,
                                                  "gif"   => HTTPContentType.GIF,
                                                  "jpg"   => HTTPContentType.JPEG,
                                                  "jpeg"  => HTTPContentType.JPEG,
                                                  "svg"   => HTTPContentType.SVG,
                                                  "png"   => HTTPContentType.PNG,
                                                  "ico"   => HTTPContentType.ICO,
                                                  "swf"   => HTTPContentType.SWF,
                                                  "js"    => HTTPContentType.JAVASCRIPT_UTF8,
                                                  "txt"   => HTTPContentType.TEXT_UTF8,
                                                  _       => HTTPContentType.OCTETSTREAM,
                                              };

                        #endregion

                        #region Create HTTP Response

                        return Task.FromResult(
                           new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               Server          = HTTPServer.DefaultServerName,
                               Date            = Timestamp.Now,
                               ContentType     = ResponseContentType,
                               ContentStream   = fileStream,
                               CacheControl    = "public, max-age=300",
                               //Expires         = "Mon, 25 Jun 2015 21:31:12 GMT",
                               KeepAlive       = new KeepAliveType(TimeSpan.FromMinutes(15),
                                                                   500),
                               Connection      = "Keep-Alive",
                           }.AsImmutable);

                        #endregion

                    }
                    catch (Exception e)
                    {

                        return Task.FromResult(
                                   new HTTPResponse.Builder(Request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       Server          = HTTPServer.DefaultServerName,
                                       Date            = Timestamp.Now,
                                       ContentType     = HTTPContentType.JSON_UTF8,
                                       Content         = JSONObject.Create(new JProperty("message", e.Message)).ToUTF8Bytes(),
                                       CacheControl    = "no-cache",
                                       Connection      = "close",
                                   }.AsImmutable
                               );

                    }

               }, AllowReplacement: URLReplacement.Fail);

            return;

        }

        #endregion


        #region RegisterWatchedFileSystemFolder(this HTTPAPI,             URLTemplate, FileSystemLocation, HTTPSSE_EventIdentification, HTTPSSE_URLTemplate, DefaultFilename = "index.html")

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPAPI">A HTTP API.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterWatchedFileSystemFolder(this HTTPAPI        HTTPAPI,
                                                           HTTPPath            URLTemplate,
                                                           String              FileSystemLocation,
                                                           HTTPEventSource_Id  HTTPSSE_EventIdentification,
                                                           HTTPPath            HTTPSSE_URLTemplate,
                                                           String              DefaultFilename  = "index.html")

            => HTTPAPI.HTTPServer.RegisterWatchedFileSystemFolder(
                                      HTTPAPI,
                                      URLTemplate,
                                      FileSystemLocation,
                                      HTTPSSE_EventIdentification,
                                      HTTPSSE_URLTemplate,
                                      DefaultFilename
                                  );


        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterWatchedFileSystemFolder(this HTTPAPI            HTTPAPI,
                                                           HTTPHostname            Hostname,
                                                           HTTPPath                URLTemplate,
                                                           String                  FileSystemLocation,
                                                           HTTPEventSource_Id      HTTPSSE_EventIdentification,
                                                           HTTPPath                HTTPSSE_URLTemplate,
                                                 //          Func<String[], String>  ResourcePath,
                                                           String                  DefaultFilename  = "index.html")

            => HTTPAPI.HTTPServer.RegisterWatchedFileSystemFolder(
                                      HTTPAPI,
                                      Hostname,
                                      URLTemplate,
                                      FileSystemLocation,
                                      HTTPSSE_EventIdentification,
                                      HTTPSSE_URLTemplate,
                                      DefaultFilename
                                  );

        #endregion

        #region RegisterWatchedFileSystemFolder(this HTTPServer, HTTPAPI, URLTemplate, FileSystemLocation, HTTPSSE_EventIdentification, HTTPSSE_URLTemplate, DefaultFilename = "index.html")

        private static Task FileWasChanged(IHTTPServer source, HTTPEventSource_Id HTTPSSE_EventIdentification, String ChangeType, String FileName)

            => source.Get<String>(HTTPSSE_EventIdentification).
                      SubmitEvent(ChangeType,
                                  @"{ ""timestamp"": """ + Timestamp.Now.ToIso8601() +  @""", ""filename"": """ + FileName + @""" }");

        private static void FileWasRenamed(object source, RenamedEventArgs e)
        {
            Console.WriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
        }

        private static void FileWatcherError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine("File watcher exception: " + e.GetException().Message);
        }


        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterWatchedFileSystemFolder(this IHTTPServer    HTTPServer,
                                                           HTTPAPI             HTTPAPI,
                                                           HTTPPath            URLTemplate,
                                                           String              FileSystemLocation,
                                                           HTTPEventSource_Id  HTTPSSE_EventIdentification,
                                                           HTTPPath            HTTPSSE_URLTemplate,
                                                           String              DefaultFilename  = "index.html")
        {

            RegisterWatchedFileSystemFolder(HTTPServer,
                                            HTTPAPI,
                                            HTTPHostname.Any,
                                            URLTemplate,
                                            FileSystemLocation,
                                            HTTPSSE_EventIdentification,
                                            HTTPSSE_URLTemplate,
                                            DefaultFilename);

        }

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterWatchedFileSystemFolder(this IHTTPServer        HTTPServer,
                                                           HTTPAPI                 HTTPAPI,
                                                           HTTPHostname            Hostname,
                                                           HTTPPath                URLTemplate,
                                                           String                  FileSystemLocation,
                                                           HTTPEventSource_Id      HTTPSSE_EventIdentification,
                                                           HTTPPath                HTTPSSE_URLTemplate,
                                                 //          Func<String[], String>  ResourcePath,
                                                           String                  DefaultFilename  = "index.html")
        {

            #region Setup file system watcher

            var watcher = new FileSystemWatcher() {
                              Path                   = FileSystemLocation,
                              NotifyFilter           = NotifyFilters.FileName |
                                                       NotifyFilters.DirectoryName |
                                                       NotifyFilters.LastWrite,
                              //Filter                 = "*.html",//|*.css|*.js|*.json",
                              IncludeSubdirectories  = true,
                              InternalBufferSize     = 4 * 4096
                          };

            watcher.Created += (s, e) => FileWasChanged(HTTPServer, HTTPSSE_EventIdentification, e.ChangeType.ToString(), e.FullPath.Remove(0, FileSystemLocation.Length).Replace(Path.DirectorySeparatorChar, '/'));
            watcher.Changed += (s, e) => FileWasChanged(HTTPServer, HTTPSSE_EventIdentification, e.ChangeType.ToString(), e.FullPath.Remove(0, FileSystemLocation.Length).Replace(Path.DirectorySeparatorChar, '/'));
            watcher.Renamed += FileWasRenamed;
            watcher.Deleted += (s, e) => FileWasChanged(HTTPServer, HTTPSSE_EventIdentification, e.ChangeType.ToString(), e.FullPath.Remove(0, FileSystemLocation.Length).Replace(Path.DirectorySeparatorChar, '/'));
            watcher.Error   += FileWatcherError;

            #endregion

            HTTPServer.AddEventSource<String>(HTTPSSE_EventIdentification,
                                              HTTPAPI,
                                              URLTemplate: HTTPSSE_URLTemplate);

            HTTPServer.AddMethodCallback(HTTPAPI,
                                         Hostname,
                                         HTTPMethod.GET,
                                         URLTemplate + (URLTemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),
                                         HTTPContentType.PNG,
                                         HTTPDelegate: async Request => {

                                             HTTPContentType? ResponseContentType = null;

                                             var numberOfTemplateParameters = URLTemplate.ToString().Count(c => c == '{');

                                             var filePath    = (Request.ParsedURLParameters != null && Request.ParsedURLParameters.Length > numberOfTemplateParameters)
                                                                   ? Request.ParsedURLParameters.Last().Replace('/', Path.DirectorySeparatorChar)
                                                                   : DefaultFilename.Replace('/', Path.DirectorySeparatorChar);

                                             try
                                             {

                                                 var fileStream = File.OpenRead(FileSystemLocation + Path.DirectorySeparatorChar + filePath);

                                                 if (fileStream is not null)
                                                 {

                                                     #region Choose HTTP Content Type based on the file name extension...

                                                     ResponseContentType = HTTPContentType.ForFileExtension(filePath.Remove(0, filePath.LastIndexOf(".", StringComparison.InvariantCulture) + 1),
                                                                                                            () => HTTPContentType.OCTETSTREAM).FirstOrDefault();

                                                     #endregion

                                                     #region Create HTTP Response

                                                     return new HTTPResponse.Builder(Request) {
                                                         HTTPStatusCode  = HTTPStatusCode.OK,
                                                         Server          = HTTPServer.DefaultServerName,
                                                         Date            = Timestamp.Now,
                                                         ContentType     = ResponseContentType,
                                                         ContentStream   = fileStream,
                                                         CacheControl    = "public, max-age=300",
                                                         //Expires         = "Mon, 25 Jun 2015 21:31:12 GMT",
                                                         KeepAlive       = new KeepAliveType(TimeSpan.FromMinutes(5), 500),
                                                         Connection      = "Keep-Alive"
                                                     };

                                                     #endregion

                                                 }

                                             }
                                             catch (FileNotFoundException e)
                                             {
                                             }

                                             return new HTTPResponse.Builder(Request) {
                                                 HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                 Server          = HTTPServer.DefaultServerName,
                                                 Date            = Timestamp.Now,
                                                 ContentType     = HTTPContentType.TEXT_UTF8,
                                                 Content         = "Error 404 - Not found!".ToUTF8Bytes(),
                                                 CacheControl    = "no-cache",
                                                 Connection      = "close",
                                             };

                                         }, AllowReplacement: URLReplacement.Fail);


            // And now my watch begins...
            watcher.EnableRaisingEvents = true;

        }

        #endregion

    }

}
