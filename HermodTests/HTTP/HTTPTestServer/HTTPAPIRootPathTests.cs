/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// An HTTP API below the root must receive the same API-relative path
    /// whether its root path was given with or without a trailing slash.
    /// </summary>
    [TestFixture]
    public class HTTPAPIRootPathTests
    {

        #region SubAPI_RootPathWithOrWithoutTrailingSlash()

        [Test]
        [TestCase("/api")]
        [TestCase("/api/")]
        public async Task SubAPI_RootPathWithOrWithoutTrailingSlash(String RootPath)
        {

            var httpServer  = await HTTPServer.StartNew();

            try
            {

                var api = httpServer.AddHTTPAPI(HTTPPath.Parse(RootPath));

                api.AddHandler(
                    HTTPPath.Root + "v1/status",
                    HTTPMethod:    HTTPMethod.GET,
                    HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = "status".ToUTF8Bytes()
                                                  }.AsImmutable
                                              )
                );

                api.AddHandler(
                    HTTPPath.Root + "{path..}",
                    HTTPMethod:    HTTPMethod.GET,
                    HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = $"unknown: {request.TryGetURLParameter("path")}".ToUTF8Bytes()
                                                  }.AsImmutable
                                              )
                );

                var httpClient  = await HTTPClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);
                var client      = httpClient.Item1!;

                var status      = await client.SendRequest(client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api/v1/status")));

                Assert.That(status.HTTPStatusCode,          Is.EqualTo(HTTPStatusCode.OK),   RootPath);
                Assert.That(status.HTTPBodyAsUTF8String,    Is.EqualTo("status"),            RootPath);

                var unknown     = await client.SendRequest(client.CreateRequest(HTTPMethod.GET, HTTPPath.Parse("/api/v1/nope")));

                Assert.That(unknown.HTTPStatusCode,         Is.EqualTo(HTTPStatusCode.NotFound),  RootPath);
                Assert.That(unknown.HTTPBodyAsUTF8String,   Is.EqualTo("unknown: v1/nope"),       RootPath);

            }
            finally
            {
                await httpServer.Stop();
            }

        }

        #endregion

    }

}
