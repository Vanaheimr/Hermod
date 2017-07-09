/*
 * Copyright (c) 2011-2014 Achim 'ahzf' Friedland <achim@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Illias;
using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Standard handlers for HTTP servers.
    /// </summary>
    public static class HTTPStandardHandlers
    {

        #region RegisterRAWRequestHandler(this HTTPServer, URITemplate)

        /// <summary>
        /// Return the RAW request header.
        /// </summary>
        public static void RegisterRAWRequestHandler(this HTTPServer  HTTPServer,
                                                     HTTPHostname     Hostname,
                                                     String           URITemplate,
                                                     HTTPMethod       HTTPMethod = null)
        {

            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod:    HTTPMethod ?? HTTPMethod.GET,
                                         URITemplate:   URITemplate,
                                         HTTPDelegate:  async Request => {

                                             return new HTTPResponseBuilder(Request) {

                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                 Server          = HTTPServer.DefaultServerName,
                                                 Date            = DateTime.Now,
                                                 CacheControl    = "no-cache",
                                                 Connection      = "close",
                                                 ContentType     = HTTPContentType.TEXT_UTF8,
                                                 Content         = ("Incoming http connection from '" + Request.RemoteSocket + "'" +
                                                                     Environment.NewLine + Environment.NewLine +
                                                                     Request.RawHTTPHeader +
                                                                     Environment.NewLine + Environment.NewLine +
                                                                     "Method => "         + Request.HTTPMethod      + Environment.NewLine +
                                                                     "URL => "            + Request.URI         + Environment.NewLine +
                                                                     "QueryString => "    + Request.QueryString     + Environment.NewLine +
                                                                     "Protocol => "       + Request.ProtocolName    + Environment.NewLine +
                                                                     "Version => "        + Request.ProtocolVersion + Environment.NewLine).ToUTF8Bytes()

                                             };

            });

        }

        #endregion

        #region RegisterMovedTemporarilyHandler(this HTTPServer, URITemplate, URITarget)

        /// <summary>
        /// Register a MovedTemporarily handler.
        /// </summary>
        public static void RegisterMovedTemporarilyHandler(this HTTPServer  HTTPServer,
                                                           HTTPHostname     Hostname,
                                                           String           URITemplate,
                                                           String           URITarget)
        {

            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod.GET,
                                         URITemplate,
                                         HTTPDelegate: async Request => HTTPTools.MovedTemporarily(Request, URITarget));

        }

        #endregion

        #region RegisterEventStreamHandler(this HTTPServer, URITemplate, EventSource)

        /// <summary>
        /// Register a EventStream handler.
        /// </summary>
        public static void RegisterEventStreamHandler(this HTTPServer  HTTPServer,
                                                      HTTPHostname     Hostname,
                                                      String           URITemplate,
                                                      String           EventSource)
        {

            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod.GET,
                                         URITemplate,
                                         HTTPDelegate: async Request => {

                                             var _LastEventId        = 0UL;
                                             var _Client_LastEventId = 0UL;
                                             var _EventSource        = HTTPServer.GetEventSource(EventSource);

                                             if (Request.TryGet<UInt64>("Last-Event-Id", out _Client_LastEventId))
                                                 _LastEventId = _Client_LastEventId;

                                             //_LastEventId = 0;

                                             var _HTTPEvents      = (from   _HTTPEvent
                                                                     in     _EventSource.GetAllEventsGreater(_LastEventId)
                                                                     where  _HTTPEvent != null
                                                                     select _HTTPEvent.ToString())
                                                                    .ToArray(); // For thread safety!


                                             // Transform HTTP events into an UTF8 string
                                             var _ResourceContent = String.Empty;

                                             if (_HTTPEvents.Length > 0)
                                                 _ResourceContent = Environment.NewLine + _HTTPEvents.Aggregate((a, b) => a + Environment.NewLine + b);

                                             _ResourceContent += Environment.NewLine + "retry: " + _EventSource.RetryIntervall.TotalMilliseconds + Environment.NewLine + Environment.NewLine;

                                             return new HTTPResponseBuilder(Request) {
                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                 Server          = HTTPServer.DefaultServerName,
                                                 Date            = DateTime.Now,
                                                 ContentType     = HTTPContentType.EVENTSTREAM,
                                                 CacheControl    = "no-cache",
                                                 Connection      = "keep-alive",
                                                 Content         = _ResourceContent.ToUTF8Bytes()
                                             };

                                         });

        }

        #endregion


        #region RegisterResourcesFile(this HTTPServer, URITemplate, ResourceAssembly, ResourceFilename, ResponseContentType = null, CacheControl = "no-cache")

        /// <summary>
        /// Returns internal resources embedded within the given assembly.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URITemplate">An URI template.</param>
        /// <param name="ResourceAssembly">The assembly where the resources are located.</param>
        /// <param name="ResourceFilename">The path to the file within the assembly.</param>
        /// <param name="ResponseContentType">Set the HTTP MIME content-type of the file. If null try to autodetect the content type based on the filename extention.</param>
        /// <param name="CacheControl">Set the HTTP cache control response header.</param>
        public static void RegisterResourcesFile(this IHTTPServer  HTTPServer,
                                                 HTTPHostname      Hostname,
                                                 String            URITemplate,
                                                 Assembly          ResourceAssembly,
                                                 String            ResourceFilename,
                                                 HTTPContentType   ResponseContentType  = null,
                                                 String            CacheControl         = "no-cache")
        {

            #region Get the appropriate content type based on the suffix of the requested resource

            if (ResponseContentType == null)
                switch (ResourceFilename.Remove(0, ResourceFilename.LastIndexOf(".") + 1))
                {
                    case "htm":  ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "html": ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "css":  ResponseContentType = HTTPContentType.CSS_UTF8;        break;
                    case "gif":  ResponseContentType = HTTPContentType.GIF;             break;
                    case "jpg":  ResponseContentType = HTTPContentType.JPEG;            break;
                    case "jpeg": ResponseContentType = HTTPContentType.JPEG;            break;
                    case "svg":  ResponseContentType = HTTPContentType.SVG;             break;
                    case "png":  ResponseContentType = HTTPContentType.PNG;             break;
                    case "ico":  ResponseContentType = HTTPContentType.ICO;             break;
                    case "swf":  ResponseContentType = HTTPContentType.SWF;             break;
                    case "js":   ResponseContentType = HTTPContentType.JAVASCRIPT_UTF8; break;
                    case "txt":  ResponseContentType = HTTPContentType.TEXT_UTF8;       break;
                    case "xml":  ResponseContentType = HTTPContentType.XML_UTF8;        break;
                    default:     ResponseContentType = HTTPContentType.OCTETSTREAM;     break;
                }

            #endregion

            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod.GET,
                                         URITemplate,
                                         HTTPContentType: ResponseContentType,
                                         HTTPDelegate: async Request => {

                                             var FileStream = ResourceAssembly.GetManifestResourceStream(ResourceFilename);

                                             if (FileStream != null)
                                                 return new HTTPResponseBuilder(Request) {
                                                     HTTPStatusCode  = HTTPStatusCode.OK,
                                                     Server          = HTTPServer.DefaultServerName,
                                                     Date            = DateTime.Now,
                                                     ContentType     = ResponseContentType,
                                                     ContentStream   = FileStream,
                                                     CacheControl    = CacheControl,
                                                     Connection      = "close",
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
                                                     return new HTTPResponseBuilder(Request) {
                                                         HTTPStatusCode = HTTPStatusCode.NotFound,
                                                         ContentType    = ResponseContentType,
                                                         ContentStream  = ErrorStream,
                                                         CacheControl   = "no-cache",
                                                         Connection     = "close",
                                                     };

                                                 #endregion

                                                 #region ...or send a default error page!

                                                 else
                                                     return new HTTPResponseBuilder(Request) {
                                                         HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                         Server          = HTTPServer.DefaultServerName,
                                                         Date            = DateTime.Now,
                                                         CacheControl    = "no-cache",
                                                         Connection      = "close",
                                                     };

                                                 #endregion

                                             }

                                         }, AllowReplacement: URIReplacement.Fail);

            return;

        }

        #endregion

        #region RegisterResourcesFolder(this HTTPServer, Hostname, URITemplate, ResourcePath, ResourceAssembly = null, DefaultFilename = "index.html", HTTPRealm = null, HTTPLogin = null, HTTPPassword = null)

        /// <summary>
        /// Returns internal resources embedded within the given assembly.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URITemplate">An URI template.</param>
        /// <param name="ResourcePath">The path to the file within the assembly.</param>
        /// <param name="ResourceAssembly">Optionally the assembly where the resources are located (default: the calling assembly).</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        /// <param name="HTTPRealm">An optional realm for HTTP basic authentication.</param>
        /// <param name="HTTPLogin">An optional login for HTTP basic authentication.</param>
        /// <param name="HTTPPassword">An optional password for HTTP basic authentication.</param>
        public static void RegisterResourcesFolder(this IHTTPServer  HTTPServer,
                                                   HTTPHostname      Hostname,
                                                   String            URITemplate,
                                                   String            ResourcePath,
                                                   Assembly          ResourceAssembly  = null,
                                                   String            DefaultFilename   = "index.html",
                                                   String            HTTPRealm         = null,
                                                   String            HTTPLogin         = null,
                                                   String            HTTPPassword      = null)
        {

            if (ResourceAssembly == null)
                ResourceAssembly = Assembly.GetCallingAssembly();


            HTTPDelegate GetEmbeddedResources = async Request => {

                #region Check HTTP Basic Authentication

                if (HTTPLogin.IsNotNullOrEmpty() && HTTPPassword.IsNotNullOrEmpty())
                {

                    if (Request.Authorization          == null        ||
                        Request.Authorization.Username != HTTPLogin   ||
                        Request.Authorization.Password != HTTPPassword)
                        return new HTTPResponseBuilder(Request) {
                            HTTPStatusCode   = HTTPStatusCode.Unauthorized,
                            Server           = HTTPServer.DefaultServerName,
                            Date             = DateTime.Now,
                            WWWAuthenticate  = @"Basic realm=""" + HTTPRealm + @"""",
                            ContentType      = HTTPContentType.TEXT_UTF8,
                            Content          = "Unauthorized Access!".ToUTF8Bytes(),
                            Connection       = "close"
                        };

                }

                #endregion


                HTTPContentType ResponseContentType = null;

                var FilePath    = (Request.ParsedURIParameters != null && Request.ParsedURIParameters.Length > 0)
                                      ? Request.ParsedURIParameters.Last().Replace("/", ".")
                                      : DefaultFilename.Replace("/", ".");

                var FileStream  = ResourceAssembly.GetManifestResourceStream(ResourcePath + "." + FilePath);

                if (FileStream != null)
                {

                    #region Choose HTTP Content Type based on the file name extention...

                    var FileName = FilePath.Substring(FilePath.LastIndexOf("/") + 1);

                    // Get the appropriate content type based on the suffix of the requested resource
                    switch (FileName.Remove(0, FileName.LastIndexOf(".") + 1))
                    {
                        case "htm" : ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                        case "html": ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                        case "css" : ResponseContentType = HTTPContentType.CSS_UTF8;        break;
                        case "gif" : ResponseContentType = HTTPContentType.GIF;             break;
                        case "jpg" : ResponseContentType = HTTPContentType.JPEG;            break;
                        case "jpeg": ResponseContentType = HTTPContentType.JPEG;            break;
                        case "svg" : ResponseContentType = HTTPContentType.SVG;             break;
                        case "png" : ResponseContentType = HTTPContentType.PNG;             break;
                        case "ico" : ResponseContentType = HTTPContentType.ICO;             break;
                        case "swf" : ResponseContentType = HTTPContentType.SWF;             break;
                        case "js"  : ResponseContentType = HTTPContentType.JAVASCRIPT_UTF8; break;
                        case "txt" : ResponseContentType = HTTPContentType.TEXT_UTF8;       break;
                        case "xml":  ResponseContentType = HTTPContentType.XML_UTF8;        break;
                        default:     ResponseContentType = HTTPContentType.OCTETSTREAM;     break;
                    }

                    #endregion

                    #region Create HTTP Response

                    return new HTTPResponseBuilder(Request) {
                        HTTPStatusCode  = HTTPStatusCode.OK,
                        Server          = HTTPServer.DefaultServerName,
                        Date            = DateTime.Now,
                        ContentType     = ResponseContentType,
                        ContentStream   = FileStream,
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

                    Stream ErrorStream = null;

                    Request.BestMatchingAcceptType = Request.Accept.BestMatchingContentType(new HTTPContentType[] { HTTPContentType.HTML_UTF8, HTTPContentType.TEXT_UTF8 });

                    if (Request.BestMatchingAcceptType == HTTPContentType.HTML_UTF8)
                    {
                        ResponseContentType = HTTPContentType.HTML_UTF8;
                        ErrorStream = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                    }

                    else if (Request.BestMatchingAcceptType == HTTPContentType.TEXT_UTF8)
                    {
                        ResponseContentType = HTTPContentType.TEXT_UTF8;
                        ErrorStream = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.txt");
                    }

                    else if (Request.BestMatchingAcceptType == HTTPContentType.JSON_UTF8)
                    {
                        ResponseContentType = HTTPContentType.JSON_UTF8;
                        ErrorStream = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.js");
                    }

                    else if (Request.BestMatchingAcceptType == HTTPContentType.XML_UTF8)
                    {
                        ResponseContentType = HTTPContentType.XML_UTF8;
                        ErrorStream = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.xml");
                    }

                    else if (Request.BestMatchingAcceptType == HTTPContentType.ALL)
                    {
                        ResponseContentType = HTTPContentType.HTML_UTF8;
                        ErrorStream = ResourceAssembly.GetManifestResourceStream(ResourcePath.Substring(0, ResourcePath.LastIndexOf(".")) + ".ErrorPages." + "404.html");
                    }

                    if (ErrorStream != null)
                        return new HTTPResponseBuilder(Request) {
                            HTTPStatusCode  = HTTPStatusCode.NotFound,
                            Server          = HTTPServer.DefaultServerName,
                            Date            = DateTime.Now,
                            ContentType     = ResponseContentType,
                            ContentStream   = ErrorStream,
                            CacheControl    = "no-cache",
                            Connection      = "close",
                        };

                    #endregion

                    #region ...or send a default error page!

                    else
                        return new HTTPResponseBuilder(Request) {
                            HTTPStatusCode  = HTTPStatusCode.NotFound,
                            Server          = HTTPServer.DefaultServerName,
                            Date            = DateTime.Now,
                            CacheControl    = "no-cache",
                            Connection      = "close",
                        };

                    #endregion

                }

            };


            // ~/map
            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod.GET,
                                         URITemplate.EndsWith("/", StringComparison.InvariantCulture) ? URITemplate.Substring(0, URITemplate.Length) : URITemplate,
                                         HTTPDelegate: GetEmbeddedResources);

            // ~/map/
            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod.GET,
                                         URITemplate + (URITemplate.EndsWith("/", StringComparison.InvariantCulture) ? "" : "/"),
                                         HTTPDelegate: GetEmbeddedResources);

            // ~/map/file.name
            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod.GET,
                                         URITemplate + (URITemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),
                                         HTTPDelegate: GetEmbeddedResources);

        }

        #endregion


        #region RegisterFilesystemFile(this HTTPServer, URITemplate, ResourceFilenameBuilder, DefaultFile = null, ResponseContentType = null, CacheControl = "no-cache")

        /// <summary>
        /// Returns a resource from the given file system location.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URITemplate">An URI template.</param>
        /// <param name="ResourceFilenameBuilder">The path to the file within the assembly.</param>
        /// <param name="DefaultFile">If an error occures, return this file.</param>
        /// <param name="ResponseContentType">Set the HTTP MIME content-type of the file. If null try to autodetect the content type based on the filename extention.</param>
        /// <param name="CacheControl">Set the HTTP cache control response header.</param>
        public static void RegisterFilesystemFile(this IHTTPServer         HTTPServer,
                                                  HTTPHostname             Hostname,
                                                  String                   URITemplate,
                                                  Func<String[], String>   ResourceFilenameBuilder,
                                                  String                   DefaultFile          = null,
                                                  HTTPContentType          ResponseContentType  = null,
                                                  String                   CacheControl         = "no-cache")
        {

            #region Get the appropriate content type based on the suffix returned by the ResourceFilenameBuilder

                                                                                  // NumberOfTemplateParameters
            var _ResourceFilename = ResourceFilenameBuilder(Enumerable.Repeat("", URITemplate.Count(c => c == '{')).ToArray());

            if (ResponseContentType == null)
                switch (_ResourceFilename.Remove(0, _ResourceFilename.LastIndexOf(".") + 1))
                {
                    case "htm":  ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "html": ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                    case "css":  ResponseContentType = HTTPContentType.CSS_UTF8;        break;
                    case "gif":  ResponseContentType = HTTPContentType.GIF;             break;
                    case "jpg":  ResponseContentType = HTTPContentType.JPEG;            break;
                    case "jpeg": ResponseContentType = HTTPContentType.JPEG;            break;
                    case "svg":  ResponseContentType = HTTPContentType.SVG;             break;
                    case "png":  ResponseContentType = HTTPContentType.PNG;             break;
                    case "ico":  ResponseContentType = HTTPContentType.ICO;             break;
                    case "swf":  ResponseContentType = HTTPContentType.SWF;             break;
                    case "js":   ResponseContentType = HTTPContentType.JAVASCRIPT_UTF8; break;
                    case "txt":  ResponseContentType = HTTPContentType.TEXT_UTF8;       break;
                    default:     ResponseContentType = HTTPContentType.OCTETSTREAM;     break;
                }

            #endregion

            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod.GET,
                                         URITemplate,
                                         HTTPContentType: ResponseContentType,
                                         HTTPDelegate: async Request => {

                                             var ResourceFilename = ResourceFilenameBuilder(Request.ParsedURIParameters);

                                             if (!File.Exists(ResourceFilename) && DefaultFile != null)
                                                 ResourceFilename = DefaultFile;

                                             if (File.Exists(ResourceFilename))
                                             {

                                                 var FileStream = File.OpenRead(ResourceFilename);
                                                 if (FileStream != null)
                                                 {

                                                     return new HTTPResponseBuilder(Request)
                                                     {
                                                         HTTPStatusCode  = HTTPStatusCode.OK,
                                                         Server          = HTTPServer.DefaultServerName,
                                                         Date            = DateTime.Now,
                                                         ContentType     = ResponseContentType,
                                                         ContentStream   = FileStream,
                                                         CacheControl    = CacheControl,
                                                         Connection      = "close",
                                                     };

                                                 }

                                             }

                                             return new HTTPResponseBuilder(Request)
                                             {
                                                 HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                 Server          = HTTPServer.DefaultServerName,
                                                 Date            = DateTime.Now,
                                                 CacheControl    = "no-cache",
                                                 Connection      = "close",
                                             };

                                         }, AllowReplacement: URIReplacement.Fail);

            return;

        }

        #endregion

        #region RegisterFilesystemFolder(this HTTPServer, URITemplate, ResourcePath, DefaultFilename = "index.html")

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="Hostname">The HTTP hostname.</param>
        /// <param name="URITemplate">An URI template.</param>
        /// <param name="ResourcePath">The path to the file within the assembly.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterFilesystemFolder(this IHTTPServer         HTTPServer,
                                                    HTTPHostname             Hostname,
                                                    String                   URITemplate,
                                                    Func<String[], String>   ResourcePath,
                                                    String                   DefaultFilename  = "index.html")
        {

            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod.GET,
                                         URITemplate + (URITemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),
                                         HTTPContentType: HTTPContentType.PNG,
                                         HTTPDelegate: Request => {

                                             try
                                             {

                                                HTTPContentType ResponseContentType = null;

                                                var NumberOfTemplateParameters = URITemplate.Count(c => c == '{');

                                                var FilePath    = (Request.ParsedURIParameters != null && Request.ParsedURIParameters.Length > NumberOfTemplateParameters)
                                                                      ? Request.ParsedURIParameters.Last().Replace('/', Path.DirectorySeparatorChar)
                                                                      : DefaultFilename.Replace('/', Path.DirectorySeparatorChar);

                                                var FileStream  = File.OpenRead(ResourcePath(Request.ParsedURIParameters) +
                                                                                Path.DirectorySeparatorChar +
                                                                                FilePath);

                                                if (FileStream == null)
                                                    return Task.FromResult(
                                                        new HTTPResponseBuilder(Request) {
                                                            HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                            Server          = HTTPServer.DefaultServerName,
                                                            Date            = DateTime.Now,
                                                            CacheControl    = "no-cache",
                                                            Connection      = "close",
                                                        }.AsImmutable());


                                                #region Choose HTTP Content Type based on the file name extention...

                                                var FileName = FilePath.Substring(FilePath.LastIndexOf("/") + 1);

                                                // Get the appropriate content type based on the suffix of the requested resource
                                                switch (FileName.Remove(0, FileName.LastIndexOf(".") + 1))
                                                {
                                                    case "htm":   ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                                                    case "html":  ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                                                    case "shtml": ResponseContentType = HTTPContentType.HTML_UTF8;       break;
                                                    case "css":   ResponseContentType = HTTPContentType.CSS_UTF8;        break;
                                                    case "gif":   ResponseContentType = HTTPContentType.GIF;             break;
                                                    case "jpg":   ResponseContentType = HTTPContentType.JPEG;            break;
                                                    case "jpeg":  ResponseContentType = HTTPContentType.JPEG;            break;
                                                    case "svg":   ResponseContentType = HTTPContentType.SVG;             break;
                                                    case "png":   ResponseContentType = HTTPContentType.PNG;             break;
                                                    case "ico":   ResponseContentType = HTTPContentType.ICO;             break;
                                                    case "swf":   ResponseContentType = HTTPContentType.SWF;             break;
                                                    case "js":    ResponseContentType = HTTPContentType.JAVASCRIPT_UTF8; break;
                                                    case "txt":   ResponseContentType = HTTPContentType.TEXT_UTF8;       break;
                                                    default:      ResponseContentType = HTTPContentType.OCTETSTREAM;     break;
                                                }

                                                #endregion

                                                #region Create HTTP Response

                                                return Task.FromResult(
                                                    new HTTPResponseBuilder(Request) {
                                                        HTTPStatusCode  = HTTPStatusCode.OK,
                                                        Server          = HTTPServer.DefaultServerName,
                                                        Date            = DateTime.Now,
                                                        ContentType     = ResponseContentType,
                                                        ContentStream   = FileStream,
                                                        CacheControl    = "public, max-age=300",
                                                        //Expires         = "Mon, 25 Jun 2015 21:31:12 GMT",
                                                        KeepAlive       = new KeepAliveType(TimeSpan.FromMinutes(15),
                                                                                            500),
                                                        Connection      = "Keep-Alive",
                                                    }.AsImmutable());

                                                #endregion


                                            }
                                            catch (Exception e)
                                            {

                                                return Task.FromResult(
                                                    new HTTPResponseBuilder(Request) {
                                                        HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                                        Server          = HTTPServer.DefaultServerName,
                                                        Date            = DateTime.Now,
                                                        ContentType     = HTTPContentType.JSON_UTF8,
                                                        Content         = JSONObject.Create(new JProperty("message", e.Message)).ToUTF8Bytes(),
                                                        CacheControl    = "no-cache",
                                                        Connection      = "close",
                                                    }.AsImmutable());

                                            }

                                        }, AllowReplacement: URIReplacement.Fail);

            return;

        }

        #endregion

        #region RegisterWatchedFilesystemFolder(this HTTPServer, URITemplate, ResourcePath, DefaultFilename = "index.html")

        private static void FileWasChanged(IHTTPServer source, String HTTPSSE_EventIdentification, String ChangeType, String FileName)
        {
            source.
                GetEventSource(HTTPSSE_EventIdentification).
                SubmitSubEvent(ChangeType,
                               @"{ ""timestamp"": """ + DateTime.Now.ToIso8601() +  @""", ""filename"": """ + FileName + @""" }");
        }

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
        /// <param name="URITemplate">An URI template.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterWatchedFileSystemFolder(this IHTTPServer        HTTPServer,
                                                           String                  URITemplate,
                                                           String                  FileSystemLocation,
                                                           String                  HTTPSSE_EventIdentification,
                                                           String                  HTTPSSE_URITemplate,
                                                           String                  DefaultFilename  = "index.html")
        {

            RegisterWatchedFileSystemFolder(HTTPServer,
                                            HTTPHostname.Any,
                                            URITemplate,
                                            FileSystemLocation,
                                            HTTPSSE_EventIdentification,
                                            HTTPSSE_URITemplate,
                                            DefaultFilename);

        }

        /// <summary>
        /// Returns resources from the given file system location.
        /// </summary>
        /// <param name="HTTPServer">A HTTP server.</param>
        /// <param name="URITemplate">An URI template.</param>
        /// <param name="DefaultFilename">The default file to load.</param>
        public static void RegisterWatchedFileSystemFolder(this IHTTPServer        HTTPServer,
                                                           HTTPHostname            Hostname,
                                                           String                  URITemplate,
                                                           String                  FileSystemLocation,
                                                           String                  HTTPSSE_EventIdentification,
                                                           String                  HTTPSSE_URITemplate,
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

            HTTPServer.AddEventSource(HTTPSSE_EventIdentification, URITemplate: HTTPSSE_URITemplate);

            HTTPServer.AddMethodCallback(Hostname,
                                         HTTPMethod.GET,
                                         URITemplate + (URITemplate.EndsWith("/", StringComparison.InvariantCulture) ? "{ResourceName}" : "/{ResourceName}"),
                                         HTTPContentType.PNG,
                                         HTTPDelegate: async Request => {

                                             HTTPContentType ResponseContentType = null;

                                             var NumberOfTemplateParameters = URITemplate.Count(c => c == '{');

                                             var FilePath    = (Request.ParsedURIParameters != null && Request.ParsedURIParameters.Length > NumberOfTemplateParameters)
                                                                   ? Request.ParsedURIParameters.Last().Replace('/', Path.DirectorySeparatorChar)
                                                                   : DefaultFilename.Replace('/', Path.DirectorySeparatorChar);

                                             try
                                             {

                                                 var FileStream = File.OpenRead(FileSystemLocation + Path.DirectorySeparatorChar + FilePath);

                                                 if (FileStream != null)
                                                 {

                                                     #region Choose HTTP Content Type based on the file name extention...

                                                     ResponseContentType = HTTPContentType.ForFileExtention(FilePath.Remove(0, FilePath.LastIndexOf(".", StringComparison.InvariantCulture) + 1),
                                                                                                            () => HTTPContentType.OCTETSTREAM).FirstOrDefault();

                                                     #endregion

                                                     #region Create HTTP Response

                                                     return new HTTPResponseBuilder(Request) {
                                                         HTTPStatusCode  = HTTPStatusCode.OK,
                                                         Server          = HTTPServer.DefaultServerName,
                                                         Date            = DateTime.Now,
                                                         ContentType     = ResponseContentType,
                                                         ContentStream   = FileStream,
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

                                             return new HTTPResponseBuilder(Request) {
                                                 HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                 Server          = HTTPServer.DefaultServerName,
                                                 Date            = DateTime.Now,
                                                 ContentType     = HTTPContentType.TEXT_UTF8,
                                                 Content         = "Error 404 - Not found!".ToUTF8Bytes(),
                                                 CacheControl    = "no-cache",
                                                 Connection      = "close",
                                             };

                                         }, AllowReplacement: URIReplacement.Fail);


            // And now my watch begins...
            watcher.EnableRaisingEvents = true;

        }

        #endregion

    }

}
