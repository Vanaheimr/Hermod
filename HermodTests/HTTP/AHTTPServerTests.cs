/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Hermod HTTP server tests endpoints.
    /// </summary>
    public abstract class AHTTPServerTests
    {

        #region Data

        protected readonly HTTPServer httpServer;

        #endregion

        #region Constructor(s)

        public AHTTPServerTests(IPPort HTTPPort)
        {

            httpServer = new HTTPServer(
                             HTTPPort,
                             AutoStart: true
                         );

        }

        #endregion


        #region Init_HTTPServer()

        [OneTimeSetUp]
        public void Init_HTTPServer()
        {

            #region GET     /

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root,
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.Text.PLAIN,
                                                                           Content                    = "Hello World!".ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                         AsImmutable));

            #endregion

            #region GET     /NotForEveryone

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root + "NotForEveryone",
                                         HTTPDelegate: request => {

                                             if (request.Authorization is HTTPBasicAuthentication httpBasicAuthentication)
                                             {

                                                 //return new HTTPResponse.Builder(request) {
                                                 //           HTTPStatusCode             = HTTPStatusCode.Unauthorized,
                                                 //           Server                     = "Hermod Test Server",
                                                 //           Date                       = Timestamp.Now,
                                                 //           AccessControlAllowOrigin   = "*",
                                                 //           AccessControlAllowMethods  = new[] { "GET" },
                                                 //           AccessControlAllowHeaders  = new[] { "Authorization" },
                                                 //           WWWAuthenticate            = @"Basic realm=""Access to the staging site"", charset =""UTF-8""",
                                                 //           Connection                 = "close"
                                                 //       }.AsImmutable;

                                                 if (httpBasicAuthentication.Username == "testUser1" ||
                                                     httpBasicAuthentication.Password == "testPassword1")
                                                 {
                                                     return Task.FromResult(
                                                                new HTTPResponse.Builder(request) {
                                                                    HTTPStatusCode             = HTTPStatusCode.OK,
                                                                    Server                     = "Hermod Test Server",
                                                                    Date                       = Timestamp.Now,
                                                                    AccessControlAllowOrigin   = "*",
                                                                    AccessControlAllowMethods  = new[] { "GET" },
                                                                    AccessControlAllowHeaders  = new[] { "Authorization" },
                                                                    ContentType                = HTTPContentType.Text.PLAIN,
                                                                    Content                    = $"Hello '{httpBasicAuthentication.Username}'!".ToUTF8Bytes(),
                                                                    Connection                 = "close"
                                                                }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                  AsImmutable
                                                            );
                                                 }

                                                 // HTTP 403 Forbidden for authentication is ok, but authorization is still not given!
                                                 if (httpBasicAuthentication.Username == "testUser2" ||
                                                     httpBasicAuthentication.Password == "testPassword2")
                                                 {
                                                     return Task.FromResult(
                                                                new HTTPResponse.Builder(request) {
                                                                    HTTPStatusCode             = HTTPStatusCode.Forbidden,
                                                                    Server                     = "Hermod Test Server",
                                                                    Date                       = Timestamp.Now,
                                                                    AccessControlAllowOrigin   = "*",
                                                                    AccessControlAllowMethods  = new[] { "GET" },
                                                                    AccessControlAllowHeaders  = new[] { "Authorization" },
                                                                    ContentType                = HTTPContentType.Text.PLAIN,
                                                                    Content                    = $"Sorry '{httpBasicAuthentication.Username}' please contact your administrator!".ToUTF8Bytes(),
                                                                    WWWAuthenticate            = @"Basic realm=""Access to the staging site"", charset =""UTF-8""",
                                                                    Connection                 = "close"
                                                                }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                  AsImmutable
                                                            );
                                                 }

                                             }

                                             return Task.FromResult(
                                                        new HTTPResponse.Builder(request) {
                                                            HTTPStatusCode             = HTTPStatusCode.Unauthorized,
                                                            Server                     = "Hermod Test Server",
                                                            Date                       = Timestamp.Now,
                                                            AccessControlAllowOrigin   = "*",
                                                            AccessControlAllowMethods  = new[] { "GET" },
                                                            AccessControlAllowHeaders  = new[] { "Authorization" },
                                                            WWWAuthenticate            = @"Basic realm=""Access to the staging site"", charset =""UTF-8""",
                                                            Connection                 = "close"
                                                        }.AsImmutable
                                                    );

                                         });

            #endregion


            #region POST    /mirror/queryString

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirror" + "queryString",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.Text.PLAIN,
                                                                           Content                    = request.QueryString.GetString("q", "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion

            #region POST    /mirror/httpBody

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirror" + "httpBody",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.Text.PLAIN,
                                                                           Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion

            #region MIRROR  /mirror/httpBody

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.MIRROR,
                                         HTTPPath.Root + "mirror" + "httpBody",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "MIRROR" },
                                                                           ContentType                = HTTPContentType.Text.PLAIN,
                                                                           Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion


            #region GET     /chunked

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root + "chunked",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           TransferEncoding           = "chunked",
                                                                           ContentType                = HTTPContentType.Text.PLAIN,
                                                                           Content                    = (new[] { "5", "Hello", "1", " ", "6", "World!", "0" }.AggregateWith("\r\n") + "\r\n\r\n").ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                         AsImmutable));

            #endregion

            #region GET     /chunkedSlow

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root + "chunkedSlow",
                                         HTTPDelegate: request => {

                                             var responseStream  = new MemoryStream();
                                             responseStream.Write((new[] { "5", "Hello", "1", " ", "6", "World!", "0" }.AggregateWith("\r\n") + "\r\n\r\n").ToUTF8Bytes());

                                             return Task.FromResult(
                                                        new HTTPResponse.Builder(request) {
                                                            HTTPStatusCode             = HTTPStatusCode.OK,
                                                            Server                     = "Hermod Test Server",
                                                            Date                       = Timestamp.Now,
                                                            AccessControlAllowOrigin   = "*",
                                                            AccessControlAllowMethods  = new[] { "GET" },
                                                            TransferEncoding           = "chunked",
                                                            ContentType                = HTTPContentType.Text.PLAIN,
                                                            ContentStream              = responseStream,
                                                            Connection                 = "close"
                                                        }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                          AsImmutable
                                                    );

                                         });

            #endregion

            #region GET     /chunkedTrailerHeaders

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root + "chunkedTrailerHeaders",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           TransferEncoding           = "chunked",
                                                                           Trailer                    = "X-Message-Length, X-Protocol-Version",
                                                                           ContentType                = HTTPContentType.Text.PLAIN,
                                                                           Content                    = (new[] { "5", "Hello", "1", " ", "6", "World!", "0" }.AggregateWith("\r\n") + "\r\nX-Message-Length: 13\r\nX-Protocol-Version: 1.0\r\n\r\n").ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                         AsImmutable));

            #endregion


            #region POST    /mirrorBody2

            httpServer.AddMethodCallback(HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirrorBody2",
                                         HTTPDelegate: request => {

                                             var queryParameter = request.HTTPBodyAsUTF8String ?? "";

                                             return Task.FromResult(
                                                        new HTTPResponse.Builder(request) {
                                                            HTTPStatusCode             = HTTPStatusCode.OK,
                                                            Server                     = "Hermod Test Server",
                                                            Date                       = Timestamp.Now,
                                                            AccessControlAllowOrigin   = "*",
                                                            AccessControlAllowMethods  = new[] { "GET" },
                                                            AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                            ContentType                = HTTPContentType.Text.PLAIN,
                                                            Content                    = queryParameter.Reverse().ToUTF8Bytes(),
                                                            Connection                 = "close"
                                                        }.AsImmutable
                                                    );

                                         });

            #endregion

        }

        #endregion

        #region Shutdown_HTTPServer()

        [OneTimeTearDown]
        public void Shutdown_HTTPServer()
        {
            httpServer?.Shutdown();
        }

        #endregion


    }

}
