/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A file mapper is a component that maps HTTP files to files e.g. within a file system.
    /// </summary>
    public class FileMapper : IFileMapper
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
      //  public readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/group");

        #endregion

        #region Properties

        #region API

        private HTTPExtAPIX api;

        /// <summary>
        /// The HTTP API of the file mapper.
        /// </summary>
        public HTTPExtAPIX API
        {

            get
            {
                return api;
            }

            set
            {

                if (api is not null)
                    throw new ArgumentException("Illegal attempt to change the HTTP API!");

                api = value ?? throw new ArgumentException("Illegal attempt to delete the HTTP API!");

            }

        }

        #endregion


        public HTTPHostname   Hostname          { get; }

        public HTTPPath       URLTemplate       { get; }

        /// <summary>
        /// The file system path of the file mapper.
        /// </summary>
        public String         FileSystemPath    { get; }

        /// <summary>
        /// The unique identification of the file mapper.
        /// </summary>
        public FileMapper_Id  Id                { get; }

        /// <summary>
        /// A human-readable name of the file mapper.
        /// </summary>
        public String         Name              { get; }

        /// <summary>
        /// The optional description of the file mapper.
        /// </summary>
        public I18NString     Description       { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new file mapper.
        /// </summary>
        /// <param name="API">The HTTP API of the file mapper.</param>
        /// <param name="FileSystemPath">The file system path of the file mapper.</param>
        /// 
        /// <param name="Id">An optional file mapper identification. If omitted, a random identification will be assigned.</param>
        /// <param name="Name">An optional human-readable name of the file mapper. If omitted, the Id will be used.</param>
        /// <param name="Description">An optional multi-language description of the file mapper.</param>
        public FileMapper(HTTPExtAPIX     API,
                          HTTPHostname    Hostname,
                          HTTPPath        URLTemplate,
                          String          FileSystemPath,

                          FileMapper_Id?  Id            = null,
                          String?         Name          = null,
                          I18NString?     Description   = default)
        {

            this.api             = API;
            this.Hostname        = Hostname;
            this.URLTemplate     = URLTemplate;
            this.FileSystemPath  = FileSystemPath;

            this.Id              = Id             ?? FileMapper_Id.Random();
            this.Name            = Name           ?? this.Id.ToString();
            this.Description     = Description    ?? I18NString.Empty;

        }

        #endregion


        #region (virtual) GetFile(Request)

        public virtual async Task<HTTPResponse> GetFile(HTTPRequest Request)
        {

            try
            {

                var numberOfTemplateParameters  = URLTemplate.ToString().Count(c => c == '{');

                var filePath    = Request.ParsedURLParameters.Length > numberOfTemplateParameters
                                    ? Request.ParsedURLParameters.Last().URLDecode()
                                    : "DefaultFilename";

                //var fileStream                  = //FileStreamProvider(filePath);

                // Resolve full path to avoid directory/path traversal attacks!
                var fullPath    = Path.GetFullPath(Path.Combine(FileSystemPath, filePath.Replace('/', Path.DirectorySeparatorChar)));

                var fileStream  = fullPath.StartsWith(FileSystemPath, StringComparison.OrdinalIgnoreCase)
                                     ? File.OpenRead(fullPath)
                                     : null;

                return fileStream is null

                           ? new HTTPResponse.Builder(Request) {
                                 HTTPStatusCode   = HTTPStatusCode.NotFound,
                                 Server           = API.HTTPTestServer.HTTPServerName,
                                 Date             = Timestamp.Now,
                                 CacheControl     = "no-cache",
                                 Connection       = ConnectionType.Close,
                             }.AsImmutable

                           : new HTTPResponse.Builder(Request) {
                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                 Server          = API.HTTPTestServer.HTTPServerName,
                                 Date            = Timestamp.Now,
                                 ContentType     = HTTPContentType.ForFileExtension(
                                                       filePath[(filePath.LastIndexOf('.') + 1)..],
                                                       () => HTTPContentType.Application.OCTETSTREAM
                                                   ).FirstOrDefault(),
                                 ContentStream   = fileStream,
                                 CacheControl    = "public, max-age=300",
                                 //Expires         = "Mon, 25 Jun 2015 21:31:12 GMT",
                                 KeepAlive       = new KeepAliveType(
                                                       TimeSpan.FromMinutes(15),
                                                       500
                                                   ),
                                 Connection      = ConnectionType.KeepAlive,
                             }.AsImmutable;

            }

            #region Handle exceptions

            catch (Exception e)
            {

                return new HTTPResponse.Builder(Request) {
                            HTTPStatusCode  = HTTPStatusCode.InternalServerError,
                            Server          = API.HTTPTestServer.HTTPServerName,
                            Date            = Timestamp.Now,
                            ContentType     = HTTPContentType.Application.JSON_UTF8,
                            Content         = JSONObject.Create(
                                                new JProperty("message", e.Message)
                                            ).ToUTF8Bytes(),
                            CacheControl    = "no-cache",
                            Connection      = ConnectionType.Close,
                        }.AsImmutable;

            }

            #endregion

        }

        #endregion

    }

}
