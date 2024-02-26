/*
 * Copyright (c) 2011-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
                               ContentType     = HTTPContentType.Text.PLAIN,
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


        // Map folders

        #region (private static) GetFromResourceAssembly(URLTemplate, DefaultServerName, ResourceAssembly, ResourcePath, ...)

        private static HTTPDelegate GetFromResourceAssembly(HTTPPath  URLTemplate,
                                                            String    DefaultServerName,
                                                            Assembly  ResourceAssembly,
                                                            String    ResourcePath,
                                                            String    DefaultFilename   = "index.html")

            => ExportFolderDelegate(
                   URLTemplate,
                   DefaultServerName,
                   ResourcePath,
                   (filePath, fileName) => {

                       // Avoid directory/path traversal attacks!
                       if (fileName.Contains("../"))
                           return null;

                       return ResourceAssembly.GetManifestResourceStream($"{filePath}{fileName.Replace('/', '.')}");

                   },
                   DefaultFilename
               );

        #endregion

        #region (private static) GetFromFileSystem      (URLTemplate, DefaultServerName,                   ResourcePath, ...)

        private static HTTPDelegate GetFromFileSystem(HTTPPath  URLTemplate,
                                                      String    DefaultServerName,
                                                      String    ResourcePath,
                                                      String    DefaultFilename   = "index.html")

            => ExportFolderDelegate(
                   URLTemplate,
                   DefaultServerName,
                   ResourcePath,
                   (filePath, fileName) => {

                       // Resolve full path to avoid directory/path traversal attacks!
                       var fullPath = Path.GetFullPath(Path.Combine(filePath, fileName.Replace('/', Path.DirectorySeparatorChar)));

                       return fullPath.StartsWith(ResourcePath, StringComparison.OrdinalIgnoreCase)
                                           ? File.OpenRead(fullPath)
                                           : null;

                   },
                   DefaultFilename
               );

        #endregion

        #region (private static) ExportFolderDelegate   (URLTemplate, DefaultServerName,                   ResourcePath, FileStreamProvider, ...)

        private static HTTPDelegate ExportFolderDelegate(HTTPPath                       URLTemplate,
                                                         String                         DefaultServerName,
                                                         String                         ResourcePath,
                                                         Func<String, String, Stream?>  FileStreamProvider,
                                                         String                         DefaultFilename   = "index.html")

            => (httpRequest) => {

                   try
                   {

                       var numberOfTemplateParameters  = URLTemplate.ToString().Count(c => c == '{');

                       var filePath                    = httpRequest.ParsedURLParameters.Length > numberOfTemplateParameters
                                                             ? httpRequest.ParsedURLParameters.Last()
                                                             : DefaultFilename;

                       var fileStream                  = FileStreamProvider(ResourcePath, filePath);

                       return Task.FromResult(
                                  fileStream is null

                                      ? new HTTPResponse.Builder(httpRequest) {
                                            HTTPStatusCode   = HTTPStatusCode.NotFound,
                                            Server           = DefaultServerName,
                                            Date             = Timestamp.Now,
                                            CacheControl     = "no-cache",
                                            Connection       = "close",
                                        }.AsImmutable

                                      : new HTTPResponse.Builder(httpRequest) {
                                            HTTPStatusCode  = HTTPStatusCode.OK,
                                            Server          = DefaultServerName,
                                            Date            = Timestamp.Now,
                                            ContentType     = HTTPContentType.ForFileExtension(
                                                                  filePath.Remove(0, filePath.LastIndexOf('.') + 1),
                                                                  () => HTTPContentType.Application.OCTETSTREAM
                                                              ).FirstOrDefault(),
                                            ContentStream   = fileStream,
                                            CacheControl    = "public, max-age=300",
                                            //Expires         = "Mon, 25 Jun 2015 21:31:12 GMT",
                                            KeepAlive       = new KeepAliveType(
                                                                  TimeSpan.FromMinutes(15),
                                                                  500
                                                              ),
                                            Connection      = "Keep-Alive",
                                        }.AsImmutable

                              );

                   }

                   #region Handle exceptions

                   catch (Exception e)
                   {

                       return Task.FromResult(
                                  new HTTPResponse.Builder(httpRequest) {
                                      HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                      Server          = DefaultServerName,
                                      Date            = Timestamp.Now,
                                      ContentType     = HTTPContentType.Application.JSON_UTF8,
                                      Content         = JSONObject.Create(
                                                            new JProperty("message", e.Message)
                                                        ).ToUTF8Bytes(),
                                      CacheControl    = "no-cache",
                                      Connection      = "close",
                                  }.AsImmutable
                              );

                   }

                   #endregion

            };

        #endregion


        #region (private static) FileWasChanged  (HTTPAPI, EventSourceId, ChangeType, FileName)

        private static async Task FileWasChanged(HTTPAPI             HTTPAPI,
                                                 HTTPEventSource_Id  EventSourceId,
                                                 String              ChangeType,
                                                 String              FileName)

        {

            var httpEventSource = HTTPAPI.Get<String>(EventSourceId);

            if (httpEventSource is not null)
                await httpEventSource.SubmitEvent(
                                          ChangeType,
                                          @"{ ""timestamp"": """ + Timestamp.Now.ToIso8601() + @""", ""fileName"": """ + FileName + @""" }"
                                      );

        }

        #endregion

        #region (private static) FileWasRenamed  (HTTPAPI, EventSourceId, ChangeType, FileName)

        private static async Task FileWasRenamed(HTTPAPI             HTTPAPI,
                                                 HTTPEventSource_Id  EventSourceId,
                                                 String              NewFileName,
                                                 String              OldFileName)

        {

            var httpEventSource = HTTPAPI.Get<String>(EventSourceId);

            if (httpEventSource is not null)
                await httpEventSource.SubmitEvent(
                                          "Renamed",
                                          @"{ ""timestamp"": """ + Timestamp.Now.ToIso8601() + @""", ""newFileName"": """ + NewFileName + @""", ""oldFileName"": """ + OldFileName + @""" }"
                                      );

        }

        #endregion

        #region (private static) FileWatcherError(HTTPAPI, EventSourceId, Error)

        private static async Task FileWatcherError(HTTPAPI             HTTPAPI,
                                                   HTTPEventSource_Id  EventSourceId,
                                                   ErrorEventArgs      Error)
        {

            DebugX.LogException(Error.GetException(), $"HTTP SSE Event Source: '{EventSourceId}'");

            var httpEventSource = HTTPAPI.Get<String>(EventSourceId);

            if (httpEventSource is not null)
                await httpEventSource.SubmitEvent(
                                          "Error",
                                          @"{ ""timestamp"": """ + Timestamp.Now.ToIso8601() + @""", ""message"": """ + Error.GetException().Message + @""" }"
                                      );

        }

        #endregion

        #region (private static) WatchFileSystemFolder(this HTTPAPI,                           ResourcePath, EventSourceId)

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPAPI">The HTTP API.</param>
        /// <param name="ResourcePath">The path to the file within the local file system.</param>
        private static void WatchFileSystemFolder(this HTTPAPI        HTTPAPI,
                                                  String              ResourcePath,
                                                  HTTPEventSource_Id  EventSourceId)
        {

            var watcher = new FileSystemWatcher() {
                              Path                   = ResourcePath,
                              NotifyFilter           = NotifyFilters.FileName |
                                                       NotifyFilters.DirectoryName |
                                                       NotifyFilters.LastWrite,
                              //Filter                 = "*.html",//|*.css|*.js|*.json",
                              IncludeSubdirectories  = true,
                              InternalBufferSize     = 4 * 4096
                          };

            watcher.Created += async (s, e) => await FileWasChanged  (HTTPAPI, EventSourceId, e.ChangeType.ToString(), e.FullPath.Remove(0, ResourcePath.Length).Replace(Path.DirectorySeparatorChar, '/'));
            watcher.Changed += async (s, e) => await FileWasChanged  (HTTPAPI, EventSourceId, e.ChangeType.ToString(), e.FullPath.Remove(0, ResourcePath.Length).Replace(Path.DirectorySeparatorChar, '/'));
            watcher.Renamed += async (s, e) => await FileWasRenamed  (HTTPAPI, EventSourceId,                          e.FullPath.Remove(0, ResourcePath.Length).Replace(Path.DirectorySeparatorChar, '/'), e.OldFullPath.Remove(0, ResourcePath.Length).Replace(Path.DirectorySeparatorChar, '/'));
            watcher.Deleted += async (s, e) => await FileWasChanged  (HTTPAPI, EventSourceId, e.ChangeType.ToString(), e.FullPath.Remove(0, ResourcePath.Length).Replace(Path.DirectorySeparatorChar, '/'));
            watcher.Error   += async (s, e) => await FileWatcherError(HTTPAPI, EventSourceId, e);

            // And now my watch begins...
            watcher.EnableRaisingEvents = true;

        }

        #endregion

        #region (private static) WatchFileSystemFolder(this HTTPAPI,    Hostname, URLTemplate, ResourcePath, EventSourceId)

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPAPI">The HTTP API.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="ResourcePath">The path to the file within the local file system.</param>
        private static void WatchFileSystemFolder(this HTTPAPI        HTTPAPI,
                                                  HTTPHostname        Hostname,
                                                  HTTPPath            URLTemplate,
                                                  String              ResourcePath,
                                                  HTTPEventSource_Id  EventSourceId)
        {

            HTTPAPI.WatchFileSystemFolder(
                        ResourcePath,
                        EventSourceId
                    );

            HTTPAPI.AddEventSource<String>(
                EventSourceId,
                URLTemplate,
                Hostname: Hostname
            );

        }

        #endregion

        #region (private static) WatchFileSystemFolder(this HTTPExtAPI, Hostname, URLTemplate, ResourcePath, EventSourceId, RequireAuthentication = true)

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPExtAPI">The HTTP API.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="ResourcePath">The path to the file within the local file system.</param>
        /// <param name="RequireAuthentication">Whether a HTTP authentication is required for downloading the files.</param>
        private static void WatchFileSystemFolder(this HTTPExtAPI     HTTPExtAPI,
                                                  HTTPHostname        Hostname,
                                                  HTTPPath            URLTemplate,
                                                  String              ResourcePath,
                                                  HTTPEventSource_Id  EventSourceId,
                                                  Boolean             RequireAuthentication   = true)
        {

            HTTPExtAPI.WatchFileSystemFolder(
                           ResourcePath,
                           EventSourceId
                       );

            HTTPExtAPI.AddEventSource<String>(
                EventSourceId,
                URLTemplate,
                Hostname:               Hostname,
                RequireAuthentication:  RequireAuthentication
            );

        }

        #endregion


        #region MapResourceAssemblyFolder(this HTTPAPI,    Hostname, URLTemplate, ResourcePath, ResourceAssembly = null, ...)

        /// <summary>
        /// Returns internal resources embedded within the given assembly.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="ResourcePath">The path to the file within the assembly.</param>
        /// <param name="ResourceAssembly">Optionally the assembly where the resources are located (default: the calling assembly).</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void MapResourceAssemblyFolder(this HTTPAPI      HTTPAPI,
                                                     HTTPHostname      Hostname,
                                                     HTTPPath          URLTemplate,
                                                     String            ResourcePath,
                                                     Assembly?         ResourceAssembly   = null,
                                                     String            DefaultFilename    = "index.html")
        {

            HTTPAPI.AddMethodCallback(

                        Hostname,
                        HTTPMethod.GET,
                        URLTemplate + (URLTemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),

                        HTTPDelegate:       GetFromResourceAssembly(
                                                URLTemplate,
                                                HTTPAPI.HTTPServer.DefaultServerName,
                                                ResourceAssembly ??= Assembly.GetCallingAssembly(),
                                                ResourcePath,
                                                DefaultFilename
                                            ),

                        AllowReplacement:   URLReplacement.Fail

                    );

        }

        #endregion

        #region MapResourceAssemblyFolder(this HTTPExtAPI, Hostname, URLTemplate, ResourcePath, ResourceAssembly = null, ...)

        /// <summary>
        /// Returns internal resources embedded within the given assembly.
        /// </summary>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">An URL template.</param>
        /// <param name="ResourcePath">The path to the file within the assembly.</param>
        /// <param name="ResourceAssembly">Optionally the assembly where the resources are located (default: the calling assembly).</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void MapResourceAssemblyFolder(this HTTPExtAPI   HTTPExtAPI,
                                                     HTTPHostname      Hostname,
                                                     HTTPPath          URLTemplate,
                                                     String            ResourcePath,
                                                     Assembly?         ResourceAssembly        = null,
                                                     String            DefaultFilename         = "index.html",
                                                     Boolean           RequireAuthentication   = true)
        {

            HTTPExtAPI.AddMethodCallback(

                           Hostname,
                           HTTPMethod.GET,
                           URLTemplate + (URLTemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),

                           HTTPDelegate:       RequireAuthentication

                                                   ? httpRequest => {

                                                         #region Get HTTP user and its organizations

                                                         // Will return HTTP 401 Unauthorized, when the HTTP user is unknown!
                                                         if (!HTTPExtAPI.TryGetHTTPUser(
                                                                             httpRequest,
                                                                             out var httpUser,
                                                                             out var httpOrganizations,
                                                                             out var response,
                                                                             Recursive: true
                                                                         ))
                                                         {
                                                             return Task.FromResult(response!.AsImmutable);
                                                         }

                                                         #endregion

                                                         return GetFromResourceAssembly(
                                                                    URLTemplate,
                                                                    HTTPExtAPI.HTTPServer.DefaultServerName,
                                                                    ResourceAssembly ??= Assembly.GetCallingAssembly(),
                                                                    ResourcePath,
                                                                    DefaultFilename
                                                                )(httpRequest);

                                                     }

                                                   : GetFromResourceAssembly(
                                                         URLTemplate,
                                                         HTTPExtAPI.HTTPServer.DefaultServerName,
                                                         ResourceAssembly ??= Assembly.GetCallingAssembly(),
                                                         ResourcePath,
                                                         DefaultFilename
                                                     ),

                           AllowReplacement:   URLReplacement.Fail

                       );

        }

        #endregion

        #region MapFileSystemFolder      (this HTTPAPI,    Hostname, URLTemplate, ResourcePath, ...)

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPAPI">The HTTP API.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="ResourcePath">The path to the file within the local file system.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void MapFileSystemFolder(this HTTPAPI         HTTPAPI,
                                               HTTPHostname         Hostname,
                                               HTTPPath             URLTemplate,
                                               String               ResourcePath,
                                               String               DefaultFilename          = "index.html",
                                               HTTPEventSource_Id?  EventSourceId            = null,
                                               HTTPPath?            EventSourceURLTemplate   = null)
        {

            #region Setup file system watcher

            if (EventSourceId.         HasValue &&
                EventSourceURLTemplate.HasValue)
            {
                HTTPAPI.WatchFileSystemFolder(
                    Hostname,
                    EventSourceURLTemplate.        Value,
                    ResourcePath,
                    EventSourceId.Value
                );
            }

            #endregion

            HTTPAPI.AddMethodCallback(

                        Hostname,
                        HTTPMethod.GET,
                        URLTemplate + (URLTemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),

                        HTTPDelegate:       GetFromFileSystem(
                                                URLTemplate,
                                                HTTPAPI.HTTPServer.DefaultServerName,
                                                ResourcePath,
                                                //fileName => GetFromFileSystem(ResourcePath, fileName),
                                                DefaultFilename
                                            ),

                        AllowReplacement:   URLReplacement.Fail

                    );

        }

        #endregion

        #region MapFileSystemFolder      (this HTTPExtAPI, Hostname, URLTemplate, ResourcePath, ...)

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPExtAPI">The HTTP API.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URLTemplate">A HTTP URL template.</param>
        /// <param name="ResourcePath">The path to the file within the local file system.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        /// <param name="RequireAuthentication">Whether a HTTP authentication is required for downloading the files.</param>
        public static void MapFileSystemFolder(this HTTPExtAPI      HTTPExtAPI,
                                               HTTPHostname         Hostname,
                                               HTTPPath             URLTemplate,
                                               String               ResourcePath,
                                               String               DefaultFilename          = "index.html",
                                               HTTPEventSource_Id?  EventSourceId            = null,
                                               HTTPPath?            EventSourceURLTemplate   = null,
                                               Boolean              RequireAuthentication    = true)
        {

            #region Setup file system watcher

            if (EventSourceId.         HasValue &&
                EventSourceURLTemplate.HasValue)
            {
                HTTPExtAPI.WatchFileSystemFolder(
                    Hostname,
                    EventSourceURLTemplate.        Value,
                    ResourcePath,
                    EventSourceId.Value,
                    RequireAuthentication
                );
            }

            #endregion

            HTTPExtAPI.AddMethodCallback(

                           Hostname,
                           HTTPMethod.GET,
                           URLTemplate + (URLTemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),

                           HTTPDelegate:       RequireAuthentication

                                                   ? httpRequest => {

                                                         #region Get HTTP user and its organizations

                                                         // Will return HTTP 401 Unauthorized, when the HTTP user is unknown!
                                                         if (!HTTPExtAPI.TryGetHTTPUser(
                                                                             httpRequest,
                                                                             out var httpUser,
                                                                             out var httpOrganizations,
                                                                             out var response,
                                                                             Recursive: true
                                                                         ))
                                                         {
                                                             return Task.FromResult(response!.AsImmutable);
                                                         }

                                                         #endregion

                                                         return GetFromFileSystem(
                                                                    URLTemplate,
                                                                    HTTPExtAPI.HTTPServer.DefaultServerName,
                                                                    ResourcePath,
                                                                    //fileName => GetFromFileSystem(ResourcePath, fileName),
                                                                    DefaultFilename
                                                                )(httpRequest);

                                                     }

                                                   : GetFromFileSystem(
                                                         URLTemplate,
                                                         HTTPExtAPI.HTTPServer.DefaultServerName,
                                                         ResourcePath,
                                                         //fileName => GetFromFileSystem(ResourcePath, fileName),
                                                         DefaultFilename
                                                     ),

                           AllowReplacement:   URLReplacement.Fail

                       );

        }

        #endregion



        // Map a single file

        #region RegisterResourcesFile   (this HTTPServer,           URLTemplate, ResourceAssembly, ResourceFilename, ResponseContentType = null, CacheControl = "no-cache")

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

            ResponseContentType ??= HTTPContentType.ForFileExtension(
                ResourceFilename.Remove(0, ResourceFilename.LastIndexOf('.') + 1),
                HTTPContentType.Application.OCTETSTREAM
            ).First();

            HTTPServer.AddMethodCallback(HTTPAPI,
                                         Hostname,
                                         HTTPMethod.GET,
                                         URLTemplate,
                                         HTTPContentType: ResponseContentType,
                                         HTTPDelegate: async request => {

                                             var fileStream = ResourceAssembly.GetManifestResourceStream(ResourceFilename);

                                             if (fileStream is not null)
                                                 return new HTTPResponse.Builder(request) {
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

                                                 request.BestMatchingAcceptType = request.Accept.BestMatchingContentType(new HTTPContentType[] { HTTPContentType.Text.HTML_UTF8, HTTPContentType.Text.PLAIN });

                                                 if (request.BestMatchingAcceptType == HTTPContentType.Text.HTML_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.Text.HTML_UTF8;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                                                 }

                                                 else if (request.BestMatchingAcceptType == HTTPContentType.Text.PLAIN)
                                                 {
                                                     ResponseContentType = HTTPContentType.Text.PLAIN;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.txt");
                                                 }

                                                 else if (request.BestMatchingAcceptType == HTTPContentType.Application.JSON_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.Application.JSON_UTF8;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.js");
                                                 }

                                                 else if (request.BestMatchingAcceptType == HTTPContentType.Application.XML_UTF8)
                                                 {
                                                     ResponseContentType = HTTPContentType.Application.XML_UTF8;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.xml");
                                                 }

                                                 else if (request.BestMatchingAcceptType == HTTPContentType.ALL)
                                                 {
                                                     ResponseContentType = HTTPContentType.Text.HTML_UTF8;
                                                     errorStream         = ResourceAssembly.GetManifestResourceStream(ResourceFilename.Substring(0, ResourceFilename.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                                                 }

                                                 if (errorStream is not null)
                                                     return new HTTPResponse.Builder(request) {
                                                         HTTPStatusCode = HTTPStatusCode.NotFound,
                                                         ContentType    = ResponseContentType,
                                                         ContentStream  = errorStream,
                                                         CacheControl   = "no-cache",
                                                         Connection     = "close",
                                                     };

                                                 #endregion

                                                 #region ...or send a default error page!

                                                 else
                                                     return new HTTPResponse.Builder(request) {
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

        #region RegisterFileSystemFile  (this HTTPAPI,                Hostname, URLTemplate, ResourceFilenameBuilder, DefaultFile = null, ResponseContentType = null, CacheControl = "no-cache")

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

        #region RegisterFileSystemFile  (this HTTPServer, HTTPAPI,    Hostname, URLTemplate, ResourceFilenameBuilder, DefaultFile = null, ResponseContentType = null, CacheControl = "no-cache")

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
            var resourceFilename  = ResourceFilenameBuilder(Enumerable.Repeat("", URLTemplate.ToString().Count(c => c == '{')).ToArray());

            ResponseContentType ??= HTTPContentType.ForFileExtension(
                                        resourceFilename.Remove(0, resourceFilename.LastIndexOf('.') + 1),
                                        HTTPContentType.Application.OCTETSTREAM
                                    ).First();

            #endregion

            HTTPServer.AddMethodCallback(HTTPAPI,
                                         Hostname,
                                         HTTPMethod.GET,
                                         URLTemplate,
                                         HTTPContentType: ResponseContentType,
                                         HTTPDelegate: request => {

                                             var fileName = ResourceFilenameBuilder(request.ParsedURLParameters);

                                             if (!File.Exists(fileName) && DefaultFile is not null)
                                                 fileName = DefaultFile;

                                             try
                                             {

                                                 var fileStream = File.OpenRead(fileName);

                                                 return fileStream is not null

                                                            ? Task.FromResult(
                                                                  new HTTPResponse.Builder(request) {
                                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                                      Server          = HTTPServer.DefaultServerName,
                                                                      Date            = Timestamp.Now,
                                                                      ContentType     = ResponseContentType,
                                                                      ContentStream   = fileStream,
                                                                      CacheControl    = CacheControl,
                                                                      Connection      = "close",
                                                                  }.AsImmutable
                                                              )

                                                            : Task.FromResult(
                                                                  new HTTPResponse.Builder(request) {
                                                                      HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                                      Server          = HTTPServer.DefaultServerName,
                                                                      Date            = Timestamp.Now,
                                                                      CacheControl    = "no-cache",
                                                                      Connection      = "close",
                                                                  }.AsImmutable
                                                              );

                                             } catch (FileNotFoundException)
                                             {

                                                 return Task.FromResult(
                                                            new HTTPResponse.Builder(request) {
                                                                HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                                Server          = HTTPServer.DefaultServerName,
                                                                Date            = Timestamp.Now,
                                                                CacheControl    = "no-cache",
                                                                Connection      = "close",
                                                            }.AsImmutable
                                                        );

                                             }

                                             catch (Exception e)
                                             {

                                                 return Task.FromResult(
                                                            new HTTPResponse.Builder(request) {
                                                                HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                                                                Server          = HTTPServer.DefaultServerName,
                                                                Date            = Timestamp.Now,
                                                                ContentType     = HTTPContentType.Text.PLAIN,
                                                                Content         = e.Message.ToUTF8Bytes(),
                                                                CacheControl    = "no-cache",
                                                                Connection      = "close",
                                                            }.AsImmutable
                                                        );

                                             }

                                         }, AllowReplacement: URLReplacement.Fail);

            return;

        }

        #endregion


    }

}
