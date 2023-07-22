/*
 * Copyright (c) 2010-2023, Achim Friedland <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP
{

    /// <summary>
    /// Hermod HTTP server tests endpoints.
    /// </summary>
    public abstract class AHTTPServerTests
    {

        #region Data

        protected readonly HTTPServer httpServer;

        public AHTTPServerTests(IPPort HTTPPort)
        {

            httpServer = new HTTPServer(
                             HTTPPort,
                             Autostart: true
                         );

        }

        #endregion

        #region Start/Stop HTTPServer

        [OneTimeSetUp]
        public void Init_HTTPServer()
        {

            #region GET     /

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.GET,
                                         HTTPPath.Root,
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = "Hello World!".ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.SetHeaderField("X-Environment-ManagedThreadId", Environment.CurrentManagedThreadId).
                                                                         AsImmutable));

            #endregion

            #region POST    /mirror/queryString

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirror" + "queryString",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = request.QueryString.GetString("q", "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion

            #region POST    /mirror/httpBody

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirror" + "httpBody",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion

            #region MIRROR  /mirror/httpBody

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.MIRROR,
                                         HTTPPath.Root + "mirror" + "httpBody",
                                         HTTPDelegate: request => Task.FromResult(
                                                                       new HTTPResponse.Builder(request) {
                                                                           HTTPStatusCode             = HTTPStatusCode.OK,
                                                                           Server                     = "Hermod Test Server",
                                                                           Date                       = Timestamp.Now,
                                                                           AccessControlAllowOrigin   = "*",
                                                                           AccessControlAllowMethods  = new[] { "GET" },
                                                                           ContentType                = HTTPContentType.TEXT_UTF8,
                                                                           Content                    = (request.HTTPBodyAsUTF8String ?? "").Reverse().ToUTF8Bytes(),
                                                                           Connection                 = "close"
                                                                       }.AsImmutable));

            #endregion


            #region POST    /mirrorBody2

            httpServer.AddMethodCallback(null,
                                         HTTPHostname.Any,
                                         HTTPMethod.POST,
                                         HTTPPath.Root + "mirrorBody2",
                                         HTTPDelegate: async request => {

                                             var queryParameter = request.HTTPBodyAsUTF8String ?? "";

                                             return new HTTPResponse.Builder(request) {
                                                        HTTPStatusCode             = HTTPStatusCode.OK,
                                                        Server                     = "Hermod Test Server",
                                                        Date                       = Timestamp.Now,
                                                        AccessControlAllowOrigin   = "*",
                                                        AccessControlAllowMethods  = new[] { "GET" },
                                                        AccessControlAllowHeaders  = new[] { "Content-Type", "Accept", "Authorization" },
                                                        ContentType                = HTTPContentType.TEXT_UTF8,
                                                        Content                    = queryParameter.Reverse().ToUTF8Bytes(),
                                                        Connection                 = "close"
                                                    }.AsImmutable;

                                         });

            #endregion

        }

        [OneTimeTearDown]
        public void Shutdown_HTTPServer()
        {
            httpServer?.Shutdown();
        }

        #endregion

    }

}
