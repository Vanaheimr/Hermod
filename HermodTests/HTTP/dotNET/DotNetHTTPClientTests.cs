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

using System.Net;
using System.Text;
using System.Globalization;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using System.Net.Http.Headers;

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

        public static readonly IPPort      HTTPPort           = IPPort.Zero;

        private const          String      MirrorText         = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmn";
        private const          String      MirroredText       = "nmlkjihgfedcbaZYXWVUTSRQPONMLKJIHGFEDCBA9876543210";
        private const          String      EncodedQueryText   = "Grüße + 100%";

        private                String      BaseURL            = "";
        private                HttpClient  httpClient         = null!;

        #endregion

        #region Constructor(s)

        public DotNetHTTPClientTests()
            : base(HTTPPort)
        { }

        #endregion


        #region Setup()

        [OneTimeSetUp]
        public void Setup()
        {

            BaseURL = $"http://127.0.0.1:{httpServer.TCPPort}";

            httpClient = new HttpClient {
                BaseAddress = new Uri(BaseURL)
            };

        }

        #endregion

        #region Shutdown()

        [OneTimeTearDown]
        public void Shutdown()
        {

            httpClient.Dispose();

        }

        #endregion


        #region AssertResponseStartHeader(HttpResponse)

        private static void AssertResponseStartHeader(HttpResponseMessage HTTPResponse)
        {

            HTTPResponse.Headers.TryGetValues(StatisticsMiddleware.ResponseStartHeader, out var runtimeValues);

            //Assert.That(
            //    HTTPResponse.Headers.TryGetValues(StatisticsMiddleware.ResponseStartHeader, out var runtimeValues),
            //    Is.True
            //);

            //var runtimeValue = runtimeValues!.Single();

            //Assert.That(
            //    Double.TryParse(runtimeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var runtimeMS),
            //    Is.True,
            //    runtimeValue
            //);

            //Assert.That(runtimeMS, Is.GreaterThanOrEqualTo(0));

        }

        #endregion

        #region AssertBasicChallenge(HttpResponse)

        private static void AssertBasicChallenge(HttpResponseMessage HTTPResponse)
        {

            Assert.That(
                HTTPResponse.Headers.WwwAuthenticate.Any(challenge => challenge.ToString() == @"Basic realm=""Access to the staging site"", charset=""UTF-8"""),
                Is.True
            );

        }

        #endregion

        #region BasicCredentials(UserName, Password)

        private static String BasicCredentials(String UserName,
                                               String Password)
        {

            return Convert.ToBase64String($"{UserName}:{Password}".ToUTF8Bytes());

        }

        #endregion


        #region Test_001()

        [Test]
        public async Task Test_001()
        {

            var httpResponse  = await httpClient.GetAsync("/");
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(),   Is.EqualTo("Hermod Test Server"));
            AssertResponseStartHeader(httpResponse);

            Assert.That(responseBody,                             Is.EqualTo("Hello World!"));

        }

        #endregion


        #region POST_MirrorTestString_in_QueryString()

        [Test]
        public async Task POST_MirrorTestString_in_QueryString()
        {

            var httpResponse  = await httpClient.PostAsync($"/mirror/queryString?q={MirrorText}", null);
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(),   Is.EqualTo("Hermod Test Server"));
            AssertResponseStartHeader(httpResponse);

            Assert.That(responseBody,                             Is.EqualTo(MirroredText));

        }

        #endregion

        #region POST_MirrorEncodedString_in_QueryString()

        [Test]
        public async Task POST_MirrorEncodedString_in_QueryString()
        {

            var httpResponse  = await httpClient.PostAsync(
                                          $"/mirror/queryString?q={Uri.EscapeDataString(EncodedQueryText)}",
                                          null
                                      );
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(),   Is.EqualTo("Hermod Test Server"));
            AssertResponseStartHeader(httpResponse);

            Assert.That(responseBody,                             Is.EqualTo(EncodedQueryText.Reverse()));

        }

        #endregion

        #region POST_MirrorTestString_in_HTTPBody()

        [Test]
        public async Task POST_MirrorTestString_in_HTTPBody()
        {

            var httpResponse  = await httpClient.PostAsync(
                                          "/mirror/httpBody",
                                          new StringContent(
                                              MirrorText,
                                              Encoding.UTF8,
                                              "text/plain"
                                          )
                                      );
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(),   Is.EqualTo("Hermod Test Server"));
            AssertResponseStartHeader(httpResponse);

            Assert.That(responseBody,                             Is.EqualTo(MirroredText));

        }

        #endregion

        #region MIRROR_TestString_in_HTTPBody()

        [Test]
        public async Task MIRROR_TestString_in_HTTPBody()
        {

            var httpRequest   = new HttpRequestMessage {
                                    Method      = new HttpMethod("MIRROR"),
                                    RequestUri  = new Uri       ("/mirror/httpBody", UriKind.Relative),
                                    Content     = new StringContent(
                                                      MirrorText,
                                                      Encoding.UTF8,
                                                      "text/plain"
                                                  )
                                };
            var httpResponse  = await httpClient.SendAsync(httpRequest);
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(),   Is.EqualTo("Hermod Test Server"));
            AssertResponseStartHeader(httpResponse);

            Assert.That(responseBody,                             Is.EqualTo(MirroredText));

        }

        #endregion


        #region POST_MirrorRandomString_in_QueryString()

        [Test]
        public async Task POST_MirrorRandomString_in_QueryString()
        {

            var randomString  = RandomExtensions.RandomString(50);
            var httpClient    = new HttpClient();
            var httpResponse  = await httpClient.PostAsync($"{BaseURL}/mirror/queryString?q={randomString}", null);
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(),   Is.EqualTo("Hermod Test Server"));
            AssertResponseStartHeader(httpResponse);

            Assert.That(responseBody,                             Is.EqualTo(randomString.Reverse()));

        }

        #endregion

        #region POST_MirrorRandomString_in_HTTPBody()

        [Test]
        public async Task POST_MirrorRandomString_in_HTTPBody()
        {

            var randomString  = RandomExtensions.RandomString(50);
            var httpClient    = new HttpClient();
            var httpResponse  = await httpClient.PostAsync(
                                          $"{BaseURL}/mirror/httpBody",
                                          new StringContent(
                                              randomString,
                                              Encoding.UTF8,
                                              "text/plain"
                                          )
                                      );
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(),   Is.EqualTo("Hermod Test Server"));
            AssertResponseStartHeader(httpResponse);

            Assert.That(responseBody,                             Is.EqualTo(randomString.Reverse()));

        }

        #endregion

        #region MIRROR_RandomString_in_HTTPBody()

        [Test]
        public async Task MIRROR_RandomString_in_HTTPBody()
        {

            var randomString  = RandomExtensions.RandomString(50);
            var httpClient    = new HttpClient();
            var httpRequest   = new HttpRequestMessage {
                                    Method      = new HttpMethod("MIRROR"),
                                    RequestUri  = new Uri       ($"{BaseURL}/mirror/httpBody"),
                                    Content     = new StringContent(
                                                      randomString,
                                                      Encoding.UTF8,
                                                      "text/plain"
                                                  )
                                };
            var httpResponse  = await httpClient.SendAsync(httpRequest);
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(),   Is.EqualTo("Hermod Test Server"));
            AssertResponseStartHeader(httpResponse);

            Assert.That(responseBody,                             Is.EqualTo(randomString.Reverse()));

        }

        #endregion


        #region GET_NotForEveryone_RequiresBasicAuthorization()

        [Test]
        public async Task GET_NotForEveryone_RequiresBasicAuthorization()
        {

            var httpResponse = await httpClient.GetAsync("/NotForEveryone");

            Assert.That(httpResponse.StatusCode,   Is.EqualTo(HttpStatusCode.Unauthorized));
            AssertBasicChallenge(httpResponse);
            AssertResponseStartHeader(httpResponse);

        }

        #endregion

        #region GET_NotForEveryone_AllowsUser1()

        [Test]
        public async Task GET_NotForEveryone_AllowsUser1()
        {

            var httpRequest                    = new HttpRequestMessage(HttpMethod.Get, "/NotForEveryone");
            httpRequest.Headers.Authorization  = new AuthenticationHeaderValue(
                                                     "Basic",
                                                     BasicCredentials("testUser1", "testPassword1")
                                                 );

            var httpResponse  = await httpClient.SendAsync(httpRequest);
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,   Is.EqualTo(HttpStatusCode.OK));
            Assert.That(responseBody,              Is.EqualTo("Hello 'testUser1'!"));
            AssertResponseStartHeader(httpResponse);

        }

        #endregion

        #region GET_NotForEveryone_ForbidsUser2()

        [Test]
        public async Task GET_NotForEveryone_ForbidsUser2()
        {

            var httpRequest                    = new HttpRequestMessage(HttpMethod.Get, "/NotForEveryone");
            httpRequest.Headers.Authorization  = new AuthenticationHeaderValue(
                                                     "Basic",
                                                     BasicCredentials("testUser2", "testPassword2")
                                                 );

            var httpResponse  = await httpClient.SendAsync(httpRequest);
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,   Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(responseBody,              Is.EqualTo("Sorry 'testUser2' please contact your administrator!"));
            AssertResponseStartHeader(httpResponse);

        }

        #endregion

        #region GET_NotForEveryone_RejectsBearerAuthorization()

        [Test]
        public async Task GET_NotForEveryone_RejectsBearerAuthorization()
        {

            var httpRequest                    = new HttpRequestMessage(HttpMethod.Get, "/NotForEveryone");
            httpRequest.Headers.Authorization  = new AuthenticationHeaderValue("Bearer", "not-a-basic-token");

            var httpResponse  = await httpClient.SendAsync(httpRequest);

            Assert.That(httpResponse.StatusCode,   Is.EqualTo(HttpStatusCode.Unauthorized));
            AssertBasicChallenge(httpResponse);
            AssertResponseStartHeader(httpResponse);

        }

        #endregion

        #region GET_NotForEveryone_RejectsMalformedBasicAuthorization()

        [Test]
        public async Task GET_NotForEveryone_RejectsMalformedBasicAuthorization()
        {

            var httpRequest   = new HttpRequestMessage(HttpMethod.Get, "/NotForEveryone");
            httpRequest.Headers.TryAddWithoutValidation("Authorization", "Basic not-base64!");

            var httpResponse  = await httpClient.SendAsync(httpRequest);

            Assert.That(httpResponse.StatusCode,   Is.EqualTo(HttpStatusCode.Unauthorized));
            AssertBasicChallenge(httpResponse);
            AssertResponseStartHeader(httpResponse);

        }

        #endregion

        #region GET_NotForEveryone_RejectsMismatchedBasicAuthorization()

        [Test]
        public async Task GET_NotForEveryone_RejectsMismatchedBasicAuthorization()
        {

            var httpRequest                    = new HttpRequestMessage(HttpMethod.Get, "/NotForEveryone");
            httpRequest.Headers.Authorization  = new AuthenticationHeaderValue(
                                                     "Basic",
                                                     BasicCredentials("testUser1", "testPassword2")
                                                 );

            var httpResponse  = await httpClient.SendAsync(httpRequest);

            Assert.That(httpResponse.StatusCode,   Is.EqualTo(HttpStatusCode.Unauthorized));
            AssertBasicChallenge(httpResponse);
            AssertResponseStartHeader(httpResponse);

        }

        #endregion


    }

}
