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
using System.Net;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP
{

    /// <summary>
    /// Tests between .NET HTTP clients and Hermod HTTP servers.
    /// </summary>
    [TestFixture]
    public class DotNetHTTPClientTests : AHTTPServerTests
    {

        #region DotNetHTTPClientTest_001()

        [Test]
        public async Task DotNetHTTPClientTest_001()
        {

            var httpClient    = new HttpClient();
            var httpResponse  = await httpClient.GetAsync("http://127.0.0.1:82");
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.OK,  httpResponse.StatusCode);
            Assert.AreEqual("Hello World!",     responseBody);

        }

        #endregion


        #region DotNetHTTPClientTest_002()

        [Test]
        public async Task DotNetHTTPClientTest_002()
        {

            var httpClient    = new HttpClient();
            var httpResponse  = await httpClient.PostAsync("http://127.0.0.1:82/mirror/queryString?q=abcdefgh", null);
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.OK,  httpResponse.StatusCode);
            Assert.AreEqual("hgfedcba",         responseBody);

        }

        #endregion

        #region DotNetHTTPClientTest_003()

        [Test]
        public async Task DotNetHTTPClientTest_003()
        {

            var httpClient    = new HttpClient();
            var httpResponse  = await httpClient.PostAsync("http://127.0.0.1:82/mirror/httpBody",
                                                           new StringContent(
                                                               "123456789",
                                                               Encoding.UTF8,
                                                               "text/plain"
                                                           ));
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.OK,  httpResponse.StatusCode);
            Assert.AreEqual("987654321",        responseBody);

        }

        #endregion


    }

}
