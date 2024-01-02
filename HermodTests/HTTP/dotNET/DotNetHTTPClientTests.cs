/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Net;
using System.Text;

using NUnit.Framework;
using NUnit.Framework.Legacy;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Tests between .NET HTTP clients and Hermod HTTP servers.
    /// </summary>
    [TestFixture]
    public class DotNetHTTPClientTests : AHTTPServerTests
    {

        #region Data

        public static readonly IPPort HTTPPort = IPPort.Parse(83);

        #endregion

        #region Constructor(s)

        public DotNetHTTPClientTests()
            : base(HTTPPort)
        { }

        #endregion


        #region Test_001()

        [Test]
        public async Task Test_001()
        {

            var httpClient    = new HttpClient();
            var httpResponse  = await httpClient.GetAsync($"http://127.0.0.1:{HTTPPort}");
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            ClassicAssert.AreEqual(HttpStatusCode.OK,      httpResponse.StatusCode);
            ClassicAssert.AreEqual("Hermod Test Server",   httpResponse.Headers.Server.ToString());

            ClassicAssert.AreEqual("Hello World!",         responseBody);

        }

        #endregion


        #region POST_MirrorRandomString_in_QueryString()

        [Test]
        public async Task POST_MirrorRandomString_in_QueryString()
        {

            var randomString    = RandomExtensions.RandomString(50);
            var httpClient      = new HttpClient();
            var httpResponse    = await httpClient.PostAsync($"http://127.0.0.1:{HTTPPort}/mirror/queryString?q={randomString}", null);
            var responseBody    = await httpResponse.Content.ReadAsStringAsync();
            var mirroredString  = randomString.Reverse();

            ClassicAssert.AreEqual(HttpStatusCode.OK,      httpResponse.StatusCode);
            ClassicAssert.AreEqual("Hermod Test Server",   httpResponse.Headers.Server.ToString());

            ClassicAssert.AreEqual(mirroredString,         responseBody);

        }

        #endregion

        #region POST_MirrorRandomString_in_HTTPBody()

        [Test]
        public async Task POST_MirrorRandomString_in_HTTPBody()
        {

            var randomString    = RandomExtensions.RandomString(50);
            var httpClient      = new HttpClient();
            var httpResponse    = await httpClient.PostAsync($"http://127.0.0.1:{HTTPPort}/mirror/httpBody",
                                                             new StringContent(
                                                                 randomString,
                                                                 Encoding.UTF8,
                                                                 "text/plain"
                                                             ));
            var responseBody    = await httpResponse.Content.ReadAsStringAsync();
            var mirroredString  = randomString.Reverse();


            ClassicAssert.AreEqual(HttpStatusCode.OK,      httpResponse.StatusCode);
            ClassicAssert.AreEqual("Hermod Test Server",   httpResponse.Headers.Server.ToString());

            ClassicAssert.AreEqual(mirroredString,         responseBody);

        }

        #endregion

        #region MIRROR_RandomString_in_HTTPBody()

        [Test]
        public async Task MIRROR_RandomString_in_HTTPBody()
        {

            var randomString        = RandomExtensions.RandomString(50);
            var httpClient          = new HttpClient();
            var httpRequest         = new HttpRequestMessage {
                                          Method      = new HttpMethod("MIRROR"),
                                          RequestUri  = new Uri       ($"http://127.0.0.1:{HTTPPort}/mirror/httpBody"),
                                          Content     = new StringContent(
                                                            randomString,
                                                            Encoding.UTF8,
                                                            "text/plain"
                                                        )
                                      };
            var httpResponse        = await httpClient.SendAsync(httpRequest);
            var responseBody        = await httpResponse.Content.ReadAsStringAsync();

            ClassicAssert.AreEqual(HttpStatusCode.OK,      httpResponse.StatusCode);
            ClassicAssert.AreEqual("Hermod Test Server",   httpResponse.Headers.Server.ToString());


            var mirroredString  = randomString.Reverse();

            ClassicAssert.AreEqual(mirroredString,          responseBody);

        }

        #endregion


    }

}
