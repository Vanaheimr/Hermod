/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The anonymous access pipeline replaced HTTPServer.AddAuth(...) when Hermod deprecated
    /// its old HTTP implementation. These tests pin the behaviour the old auth chain had:
    /// assign the anonymous user to the exempted paths, leave everything else alone.
    /// </summary>
    [TestFixture]
    public class AnonymousAccessPipelineTests
    {

        #region (private) ParseGETRequest(Path)

        private static HTTPRequest ParseGETRequest(String Path)
        {

            var localSocket   = IPSocket.LocalhostV4(IPPort.Parse(6801));
            var remoteSocket  = IPSocket.LocalhostV4(IPPort.Parse(43123));

            var parsed        = HTTPRequest.TryParse(
                                    Timestamp.Now,
                                    new HTTPSource(remoteSocket),
                                    localSocket,
                                    remoteSocket,
                                    $"GET {Path} HTTP/1.1\r\n" +
                                    "Host: example.test\r\n" +
                                    "Connection: close\r\n",
                                    out var request,
                                    out var errorResponse,
                                    CancellationToken: CancellationToken.None
                                );

            Assert.That(parsed,   Is.True, errorResponse?.ToString());
            Assert.That(request,  Is.Not.Null);

            return request!;

        }

        #endregion


        #region AnExemptedPath_BecomesAnonymous()

        [Test]
        public async Task AnExemptedPath_BecomesAnonymous()
        {

            var pipeline            = new AnonymousAccessPipeline(
                                          request => request.Path.ToString() == "/systemInfo"
                                      );

            var (request, response)  = await pipeline.ProcessHTTPRequest(ParseGETRequest("/systemInfo"));

            Assert.Multiple(() => {
                Assert.That(request.User, Is.EqualTo(HTTPExtAPI.Anonymous));
                // A pipeline that returns a response would stop the request from ever
                // reaching its handler, so this one must not return one.
                Assert.That(response,     Is.Null);
            });

        }

        #endregion

        #region AnyOtherPath_StaysWithoutAUser()

        [Test]
        public async Task AnyOtherPath_StaysWithoutAUser()
        {

            var pipeline             = new AnonymousAccessPipeline(
                                           request => request.Path.ToString() == "/systemInfo"
                                       );

            var (request, response)  = await pipeline.ProcessHTTPRequest(ParseGETRequest("/programs"));

            Assert.Multiple(() => {
                Assert.That(request.User, Is.Null);
                Assert.That(response,     Is.Null);
            });

        }

        #endregion

        #region AnAlreadyIdentifiedUser_IsNotOverwritten()

        [Test]
        public async Task AnAlreadyIdentifiedUser_IsNotOverwritten()
        {

            var pipeline      = new AnonymousAccessPipeline(request => true);

            var request       = ParseGETRequest("/systemInfo");
            var existingUser  = new User(
                                    User_Id.Parse("alice"),
                                    "Alice".ToI18NString(),
                                    SimpleEMailAddress.Parse("alice@example.test")
                                );

            request.User      = existingUser;

            var (processed, _)  = await pipeline.ProcessHTTPRequest(request);

            // A cookie, an API key or HTTP Basic Auth knows more about the request
            // than a path list does.
            Assert.That(processed.User, Is.EqualTo(existingUser));

        }

        #endregion

    }

}
