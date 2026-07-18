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


        #region GET_ConnectionClose_Reconnects_Before_The_Next_Request()

        [Test]
        public async Task GET_ConnectionClose_Reconnects_Before_The_Next_Request()
        {

            using var closeClient = new HttpClient {
                                        BaseAddress = new Uri(BaseURL)
                                    };

            var firstResponse  = await closeClient.GetAsync("/close");
            var secondResponse = await closeClient.GetAsync("/close");

            Assert.That(firstResponse.StatusCode,   Is.EqualTo(HttpStatusCode.OK));
            Assert.That(secondResponse.StatusCode,  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await firstResponse.Content.ReadAsStringAsync(),  Is.EqualTo("1"));
            Assert.That(await secondResponse.Content.ReadAsStringAsync(), Is.EqualTo("1"));

        }

        #endregion


        #region GET_KeepAlive_Reuses_Connection()

        [Test]
        public async Task GET_KeepAlive_Reuses_Connection()
        {

            using var keepAliveClient = new HttpClient {
                                            BaseAddress = new Uri(BaseURL)
                                        };

            var firstResponse  = await keepAliveClient.GetAsync("/keepalive");
            var secondResponse = await keepAliveClient.GetAsync("/keepalive");

            Assert.That(firstResponse.StatusCode,   Is.EqualTo(HttpStatusCode.OK));
            Assert.That(secondResponse.StatusCode,  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await firstResponse.Content.ReadAsStringAsync(),  Is.EqualTo("1"));
            Assert.That(await secondResponse.Content.ReadAsStringAsync(), Is.EqualTo("2"));

        }

        #endregion


        #region HEAD_Root_Has_No_Body()

        [Test]
        public async Task HEAD_Root_Has_No_Body()
        {

            using var request       = new HttpRequestMessage(HttpMethod.Head, "/");
            var       httpResponse  = await httpClient.SendAsync(request);
            var       responseBody  = await httpResponse.Content.ReadAsByteArrayAsync();

            Assert.That(httpResponse.StatusCode,                     Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Content.Headers.ContentLength,  Is.EqualTo((Int64) "Hello World!".Length));
            Assert.That(responseBody,                                 Is.Empty);

        }

        #endregion

        #region OPTIONS_Root_Has_No_Body()

        [Test]
        public async Task OPTIONS_Root_Has_No_Body()
        {

            using var request       = new HttpRequestMessage(HttpMethod.Options, "/");
            var       httpResponse  = await httpClient.SendAsync(request);
            var       responseBody  = await httpResponse.Content.ReadAsByteArrayAsync();

            Assert.That(httpResponse.StatusCode,  Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(responseBody,             Is.Empty);

        }

        #endregion

        #region GET_NotModified_Has_No_Body()

        [Test]
        public async Task GET_NotModified_Has_No_Body()
        {

            var httpResponse = await httpClient.GetAsync("/notmodified");
            var responseBody = await httpResponse.Content.ReadAsByteArrayAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
            Assert.That(responseBody,            Is.Empty);

        }

        #endregion


        #region GET_ResetContent_Has_No_Body()

        [Test]
        public async Task GET_ResetContent_Has_No_Body()
        {

            var httpResponse = await httpClient.GetAsync("/resetcontent");
            var responseBody = await httpResponse.Content.ReadAsByteArrayAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.ResetContent));
            Assert.That(responseBody,            Is.Empty);

        }

        #endregion


        #region PUT_Root_Is_MethodNotAllowed()

        [Test]
        public async Task PUT_Root_Is_MethodNotAllowed()
        {

            var httpResponse = await httpClient.PutAsync("/", new ByteArrayContent([]));

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(httpResponse.Content.Headers.TryGetValues("Allow", out var allowedMethods),
                        Is.True,
                        String.Join("; ", httpResponse.Content.Headers.Select(header => $"{header.Key}: {String.Join(",", header.Value)}")));
            Assert.That(String.Join(",", allowedMethods ?? []), Does.Contain("GET"));
            Assert.That(String.Join(",", allowedMethods ?? []), Does.Contain("HEAD"));
            Assert.That(String.Join(",", allowedMethods ?? []), Does.Contain("OPTIONS"));

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

        #region POST_MirrorHTTPBody_With_Expect100Continue()

        [Test]
        public async Task POST_MirrorHTTPBody_With_Expect100Continue()
        {

            var httpRequest  = new HttpRequestMessage(HttpMethod.Post, "/mirror/httpBody") {
                                   Content = new StringContent(
                                                 MirrorText,
                                                 Encoding.UTF8,
                                                 "text/plain"
                                             )
                               };
            httpRequest.Headers.ExpectContinue = true;

            var httpResponse = await httpClient.SendAsync(httpRequest);
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(), Is.EqualTo("Hermod Test Server"));
            Assert.That(responseBody,                           Is.EqualTo(MirroredText));

        }

        #endregion

        #region POST_UnsupportedExpectation_Returns417()

        [Test]
        public async Task POST_UnsupportedExpectation_Returns417()
        {

            var httpRequest  = new HttpRequestMessage(HttpMethod.Post, "/mirror/httpBody") {
                                   Content = new StringContent(
                                                 MirrorText,
                                                 Encoding.UTF8,
                                                 "text/plain"
                                             )
                               };
            Assert.That(httpRequest.Headers.TryAddWithoutValidation("Expect", "unsupported-expectation"), Is.True);

            var httpResponse = await httpClient.SendAsync(httpRequest);

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.ExpectationFailed));

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


        #region QUERY_TestString_in_HTTPBody()

        [Test]
        public async Task QUERY_TestString_in_HTTPBody()
        {

            using var httpRequest = new HttpRequestMessage {
                                        Method      = new HttpMethod("QUERY"),
                                        RequestUri  = new Uri("/query", UriKind.Relative),
                                        Content     = new StringContent(
                                                          MirrorText,
                                                          Encoding.UTF8,
                                                          "text/plain"
                                                      )
                                    };

            var httpResponse = await httpClient.SendAsync(httpRequest);
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.Server.ToString(), Is.EqualTo("Hermod Test Server"));
            Assert.That(responseBody,                           Is.EqualTo(MirrorText.Reverse()));

        }

        #endregion


        #region QUERY_ChunkedTestString_in_HTTPBody()

        [Test]
        public async Task QUERY_ChunkedTestString_in_HTTPBody()
        {

            using var httpRequest = new HttpRequestMessage(new HttpMethod("QUERY"), "/query") {
                                        Content = new StreamContent(new MemoryStream(MirrorText.ToUTF8Bytes()))
                                    };
            httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") {
                                                        CharSet = "utf-8"
                                                    };
            httpRequest.Headers.TransferEncodingChunked = true;

            var httpResponse = await httpClient.SendAsync(httpRequest);
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(responseBody,            Is.EqualTo(MirrorText.Reverse()));

        }

        #endregion


        #region POST_ChunkedMirrorTestString_in_HTTPBody()

        [Test]
        public async Task POST_ChunkedMirrorTestString_in_HTTPBody()
        {

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/mirror/httpBody") {
                                  Content = new StreamContent(new MemoryStream(MirrorText.ToUTF8Bytes()))
                              };
            httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") {
                                                        CharSet = "utf-8"
                                                    };
            httpRequest.Headers.TransferEncodingChunked = true;

            var httpResponse = await httpClient.SendAsync(httpRequest);
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(responseBody,            Is.EqualTo(MirroredText));

        }

        #endregion

        #region GET_ChunkedResponse_Trailers_Are_Available()

        [Test]
        public async Task GET_ChunkedResponse_Trailers_Are_Available()
        {

            var httpResponse = await httpClient.GetAsync("/chunkedTrailerHeaders");
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(responseBody,            Is.EqualTo("Hello World!"));
            Assert.That(httpResponse.TrailingHeaders.TryGetValues("X-Message-Length", out var messageLengths), Is.True);
            Assert.That(messageLengths, Is.EquivalentTo([ "13" ]));
            Assert.That(httpResponse.TrailingHeaders.TryGetValues("X-Protocol-Version", out var protocolVersions), Is.True);
            Assert.That(protocolVersions, Is.EquivalentTo([ "1.0" ]));

        }

        #endregion


        #region GET_AutomaticallyChunkedResponse_Trailers_Are_Available()

        [Test]
        public async Task GET_AutomaticallyChunkedResponse_Trailers_Are_Available()
        {

            var httpResponse = await httpClient.GetAsync("/chunkedAutomaticTrailerHeaders");
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(responseBody,            Is.EqualTo("Hello World!"));
            Assert.That(httpResponse.TrailingHeaders.GetValues("X-Message-Length"),   Is.EquivalentTo([ "13" ]));
            Assert.That(httpResponse.TrailingHeaders.GetValues("X-Protocol-Version"), Is.EquivalentTo([ "1.0" ]));

        }

        #endregion


        #region GET_ChunkedLiveResponse_Trailers_Are_Available()

        [Test]
        public async Task GET_ChunkedLiveResponse_Trailers_Are_Available()
        {

            var httpResponse = await httpClient.GetAsync("/chunkedLiveTrailerHeaders");
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(responseBody,            Is.EqualTo("Hello World!"));
            Assert.That(httpResponse.TrailingHeaders.TryGetValues("X-Message-Length", out var messageLengths), Is.True);
            Assert.That(messageLengths, Is.EquivalentTo([ "13" ]));
            Assert.That(httpResponse.TrailingHeaders.TryGetValues("X-Protocol-Version", out var protocolVersions), Is.True);
            Assert.That(protocolVersions, Is.EquivalentTo([ "1.0" ]));

        }

        #endregion


        #region GET_ChunkedLiveResponse_Extensions_Are_Interoperable()

        [Test]
        public async Task GET_ChunkedLiveResponse_Extensions_Are_Interoperable()
        {

            var httpResponse = await httpClient.GetAsync("/chunkedLiveExtensions");
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(responseBody,            Is.EqualTo("Hello World!"));

        }

        #endregion


        #region GET_EventStream_With_Multiple_Data_Lines()

        [Test]
        public async Task GET_EventStream_With_Multiple_Data_Lines()
        {

            var httpResponse = await httpClient.GetAsync("/events/multiline");
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
            Assert.That(responseBody, Does.Contain("data: {\n").And.Contain("data:   \"message\": \"multiline\"\n").And.Contain("data: }\n"));

        }

        #endregion


        #region GET_EventStream()

        [Test]
        public async Task GET_EventStream()
        {

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/events");
            using var httpResponse = await httpClient.SendAsync(
                                         httpRequest,
                                         HttpCompletionOption.ResponseHeadersRead
                                     );
            using var streamReader = new StreamReader(await httpResponse.Content.ReadAsStreamAsync());

            var lines = new List<String>();
            while (lines.Count < 6)
            {
                var line = await streamReader.ReadLineAsync();
                if (line is null)
                    break;

                lines.Add(line);
            }

            Assert.That(httpResponse.StatusCode,                  Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
            Assert.That(lines, Does.Contain("event: status"));
            Assert.That(lines, Does.Contain("data: {\"message\":\"from Hermod server\"}"));

        }

        #endregion

        #region GET_LiveEventStreamWorker()

        [Test]
        public async Task GET_LiveEventStreamWorker()
        {

            var httpResponse = await httpClient.GetAsync("/events/live");
            var responseBody = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
            Assert.That(responseBody, Does.Contain(": live-worker").And.Contain("event: status").And.Contain("data: {\"message\":\"from live SSE worker\"}"));

        }

        #endregion


        #region GET_LiveEventStreamWorker_Supports_Parallel_Clients()

        [Test]
        public async Task GET_LiveEventStreamWorker_Supports_Parallel_Clients()
        {

            var responseBodies = await Task.WhenAll(
                                     Enumerable.Range(0, 4).
                                                Select(_ => httpClient.GetStringAsync("/events/live"))
                                 );

            Assert.That(responseBodies, Has.All.Contain(": live-worker"));
            Assert.That(responseBodies, Has.All.Contain("data: {\"message\":\"from live SSE worker\"}"));

        }

        #endregion


        #region GET_EventStream_Reconnects_From_Last_Event_Id()

        [Test]
        public async Task GET_EventStream_Reconnects_From_Last_Event_Id()
        {

            var initialResponse = await httpClient.GetAsync("/events/reconnect");
            var initialBody     = await initialResponse.Content.ReadAsStringAsync();

            using var reconnectRequest = new HttpRequestMessage(HttpMethod.Get, "/events/reconnect");
            reconnectRequest.Headers.TryAddWithoutValidation("Last-Event-ID", "1");

            var reconnectResponse = await httpClient.SendAsync(reconnectRequest);
            var reconnectBody     = await reconnectResponse.Content.ReadAsStringAsync();

            Assert.That(initialResponse.StatusCode,   Is.EqualTo(HttpStatusCode.OK));
            Assert.That(initialBody,                  Does.Contain("id: 1").And.Contain("id: 2"));
            Assert.That(reconnectResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reconnectBody,                Does.Not.Contain("id: 1").And.Contain("id: 2"));

        }

        #endregion


        #region GET_ChunkedResponse()

        [Test]
        public async Task GET_ChunkedResponse()
        {

            var httpResponse = await httpClient.GetAsync("/chunked");
            var responseBody  = await httpResponse.Content.ReadAsStringAsync();

            Assert.That(httpResponse.StatusCode,                     Is.EqualTo(HttpStatusCode.OK));
            Assert.That(httpResponse.Headers.TransferEncodingChunked, Is.True);
            Assert.That(responseBody,                                 Is.EqualTo("Hello World!"));

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
