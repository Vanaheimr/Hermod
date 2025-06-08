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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// 
    /// </summary>
    public class TOTPFileMapper : FileMapper
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
      //  public readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/group");

        #endregion

        #region Properties

        public String    SharedSecret      { get; }
        public TimeSpan  ValidityTime      { get; }
        public UInt32    TOTPLength        { get; }
        public String    Alphabet          { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new file mapper.
        /// </summary>
        /// <param name="API">The HTTP API of the file mapper.</param>
        /// 
        /// <param name="Id">An optional file mapper identification. If omitted, a random identification will be assigned.</param>
        /// <param name="Name">An optional human-readable name of the file mapper. If omitted, the Id will be used.</param>
        /// <param name="Description">An optional multi-language description of the file mapper.</param>
        public TOTPFileMapper(HTTPExtAPI      API,

                              HTTPHostname    Hostname,
                              HTTPPath        URLTemplate,
                              String          FileSystemPath,
                              String          SharedSecret,

                              FileMapper_Id?  Id             = null,
                              String?         Name           = null,
                              I18NString?     Description    = default,

                              TimeSpan?       ValidityTime   = null,
                              UInt32?         TOTPLength     = 12,
                              String?         Alphabet       = null)

            : base(API,
                   Hostname,
                   URLTemplate,
                   FileSystemPath,
                   Id,
                   Name,
                   Description)

        {


            this.SharedSecret    = SharedSecret ?? throw new ArgumentNullException(nameof(SharedSecret), "The shared secret must not be null!");
            this.ValidityTime    = ValidityTime ?? TimeSpan.FromMinutes(15);
            this.TOTPLength      = TOTPLength   ?? 12;
            this.Alphabet        = Alphabet     ?? "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        }

        #endregion


        public  (String  Current,
                 Int64   EndTime)

            Create(DateTimeOffset  ExpiryDate,
                   String          FilePath)

        {

            var endTime       = ExpiryDate.ToUnixTimestamp();

            var (totp, _, _)  = TOTPGenerator.GenerateTOTP(
                                    ExpiryDate,
                                    FilePath + SharedSecret,
                                    null,
                                    TOTPLength,
                                    Alphabet
                                );

            return (totp, endTime);

        }


        #region (override) GetFile(Request)

        public override async Task<HTTPResponse> GetFile(HTTPRequest Request)
        {

            try
            {

                var numberOfTemplateParameters  = URLTemplate.ToString().Count(c => c == '{');

                var filePath    = Request.ParsedURLParameters.Length > numberOfTemplateParameters
                                    ? Request.ParsedURLParameters.Last().URLDecode()
                                    : "DefaultFilename";

                // https://example.com/photos/photo.jpg?expiry=1717614500&token=abcdef
                var expiry      = Request.QueryString.GetInt64("expiry");
                var expiryDate  = expiry.HasValue
                                      ? DateTimeExtensions.FromUnixTimestamp(expiry.Value)
                                      : new DateTime?();
                var token       = Request.QueryString.GetString("token")?.URLDecode();


                if (!expiryDate.HasValue || token.IsNullOrEmpty() || expiryDate.Value < Timestamp.Now)
                    return new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.Unauthorized,
                               Server          = API.HTTPServer.DefaultServerName,
                               Date            = Timestamp.Now,
                               CacheControl    = "public, max-age=300",
                               //Expires         = "Mon, 25 Jun 2015 21:31:12 GMT",
                               KeepAlive       = new KeepAliveType(
                                                     TimeSpan.FromMinutes(15),
                                                     500
                                                 ),
                               Connection      = ConnectionType.KeepAlive,
                           }.AsImmutable;

                var (prev,
                     curr,
                     next,
                     rem,
                     end) = TOTPGenerator.GenerateTOTPs(
                                expiryDate.Value,
                                filePath + SharedSecret,
                                null,
                                TOTPLength,
                                Alphabet
                            );

                if (curr != token && prev != token && next != token)
                    return new HTTPResponse.Builder(Request) {
                               HTTPStatusCode  = HTTPStatusCode.Unauthorized,
                               Server          = API.HTTPServer.DefaultServerName,
                               Date            = Timestamp.Now,
                               CacheControl    = "public, max-age=300",
                               //Expires         = "Mon, 25 Jun 2015 21:31:12 GMT",
                               KeepAlive       = new KeepAliveType(
                                                     TimeSpan.FromMinutes(15),
                                                     500
                                                 ),
                               Connection      = ConnectionType.KeepAlive,
                           }.AsImmutable;


                //var fileStream                  = //FileStreamProvider(filePath);

                // Resolve full path to avoid directory/path traversal attacks!
                var fullPath    = Path.GetFullPath(Path.Combine(FileSystemPath, filePath.Replace('/', Path.DirectorySeparatorChar)));

                var fileStream  = fullPath.StartsWith(FileSystemPath, StringComparison.OrdinalIgnoreCase)
                                     ? File.OpenRead(fullPath)
                                     : null;

                return fileStream is null

                           ? new HTTPResponse.Builder(Request) {
                                 HTTPStatusCode   = HTTPStatusCode.NotFound,
                                 Server           = API.HTTPServer.DefaultServerName,
                                 Date             = Timestamp.Now,
                                 CacheControl     = "no-cache",
                                 Connection       = ConnectionType.Close,
                             }.AsImmutable

                           : new HTTPResponse.Builder(Request) {
                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                 Server          = API.HTTPServer.DefaultServerName,
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
                            Server          = API.HTTPServer.DefaultServerName,
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
