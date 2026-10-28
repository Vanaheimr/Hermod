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

using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 7838 over HTTP/1.1: the Alt-Svc and Alt-Used fields. H-7 in
    /// HTTP1ConformanceTests.
    /// </summary>
    /// <remarks>
    /// The grammar had been written for HTTP/2's ALTSVC frame, in HTTP2/Core,
    /// where the HTTP/1.x stack could not reach it - although over HTTP/1.1
    /// the field is the only way to announce an alternative at all. Moving it
    /// found three places where it read or wrote something RFC 7838 Section 3
    /// does not say: "clear" beside alternatives was skipped, "Clear" was not
    /// "clear", and an ALPN name outside the token alphabet went onto the wire
    /// unencoded. The HTTP/2 tests of the same type are in
    /// Tests.HTTP2.AlternativeServiceTests.
    /// </remarks>
    [TestFixture]
    public class AlternativeServicesTests
    {

        #region Data

        private HTTPServer?  httpServer;

        private URL          URL
            => URL.Parse($"http://127.0.0.1:{httpServer!.TCPPort}");

        private static readonly AlternativeServices announced
            = AlternativeServices.Of(
                  new HTTPAlternativeService("h3", "",            443,  TimeSpan.FromHours(1), false),
                  new HTTPAlternativeService("h2", "alt.example", 8443, HTTPAlternativeService.DefaultMaxAge, true)
              );

        #endregion

        #region Setup / Teardown

        [OneTimeSetUp]
        public void Init()
        {

            httpServer = new HTTPServer(
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );

            var httpAPI = new HTTPAPI(httpServer);

            httpAPI.AddHandler(HTTPPath.Root + "announce",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = "announced".ToUTF8Bytes(),
                                                      AltSvc          = announced,
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            httpAPI.AddHandler(HTTPPath.Root + "clear",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.NotFound,
                                                      ContentLength   = 0,
                                                      AltSvc          = AlternativeServices.Clear,
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            httpAPI.AddHandler(HTTPPath.Root + "used",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = (request.AltUsed ?? "(none)").ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {
            if (httpServer is not null)
                await httpServer.DisposeAsync();
        }

        #endregion

        #region (private) RawHeader(Target)

        /// <summary>
        /// The response's header section as it went over the wire.
        /// </summary>
        private async Task<String> RawHeader(String Target)
        {

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, httpServer!.TCPPort.ToInt32());

            await using var stream = tcpClient.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET {Target} HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"));

            using var timeout  = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var buffer   = new MemoryStream();
            await stream.CopyToAsync(buffer, timeout.Token);

            var response = Encoding.ASCII.GetString(buffer.ToArray());

            return response[..response.IndexOf("\r\n\r\n", StringComparison.Ordinal)];

        }

        #endregion


        #region ClearBesideAlternativesStillClears()

        /// <summary>
        /// Section 3: "clear" invalidates every alternative, "including those
        /// specified in the same response, in case of an invalid reply
        /// containing both". The parser skipped "clear" as an entry without
        /// "=", and returned the alternative beside it.
        /// </summary>
        [TestCase("h3=\":443\", clear")]
        [TestCase("clear, h3=\":443\"")]
        public void ClearBesideAlternativesStillClears(String FieldValue)
        {

            var services = HTTPAlternativeService.Parse(FieldValue, out var clear);

            Assert.Multiple(() => {
                Assert.That(clear,     Is.True);
                Assert.That(services,  Is.Empty, "none of the alternatives beside it");
                Assert.That(AlternativeServices.TryParse(FieldValue, out var value), Is.True);
                Assert.That(value!.IsClear, Is.True);
            });

        }

        #endregion

        #region ClearIsCaseInsensitive()

        /// <summary>
        /// "clear" is a literal in the ABNF of Section 3, and RFC 5234 makes
        /// literals case-insensitive. The parser compared it ordinally.
        /// </summary>
        [TestCase("Clear")]
        [TestCase("CLEAR")]
        public void ClearIsCaseInsensitive(String FieldValue)
        {

            HTTPAlternativeService.Parse(FieldValue, out var clear);

            Assert.That(clear, Is.True);

        }

        #endregion

        #region AnALPNNameOutsideTheTokenAlphabetIsPercentEncoded()

        /// <summary>
        /// Section 3: octets that are not token characters MUST be
        /// percent-encoded, so MUST "%", and nothing else may be. The first
        /// two are the RFC's own examples. The serializer wrote every name as
        /// it was, so "http/1.1" went onto the wire as an invalid token.
        /// </summary>
        [TestCase("w=x:y#z",  "w%3Dx%3Ay#z")]
        [TestCase("x%y",      "x%25y")]
        [TestCase("http/1.1", "http%2F1.1")]
        [TestCase("h3",       "h3")]
        public void AnALPNNameOutsideTheTokenAlphabetIsPercentEncoded(String ALPN, String Encoded)
        {

            var service   = new HTTPAlternativeService(ALPN, "", 443, HTTPAlternativeService.DefaultMaxAge, false);
            var reparsed  = HTTPAlternativeService.Parse(service.ToFieldValue());

            Assert.Multiple(() => {
                Assert.That(service.ToFieldValue(), Is.EqualTo($"{Encoded}=\":443\""));
                Assert.That(reparsed.Single(),      Is.EqualTo(service), "and decoded back to the same name");
            });

        }

        #endregion

        #region AValueWithNothingUsableIsRefused()

        /// <summary>
        /// The grammar has no empty list, and an empty list would read as
        /// neither "nothing new" nor "clear" without guessing which.
        /// </summary>
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("nonsense")]
        [TestCase("h3=\":0\"")]
        public void AValueWithNothingUsableIsRefused(String FieldValue)
        {

            Assert.That(AlternativeServices.TryParse(FieldValue, out var value), Is.False);
            Assert.That(value,                                                   Is.Null);

        }

        #endregion

        #region TheFieldValueRoundTrips()

        [Test]
        public void TheFieldValueRoundTrips()
        {

            var text = announced.ToString();

            Assert.Multiple(() => {
                Assert.That(text, Is.EqualTo("h3=\":443\"; ma=3600, h2=\"alt.example:8443\"; persist=1"));
                Assert.That(AlternativeServices.TryParse(text, out var reparsed), Is.True);
                Assert.That(reparsed!.Services, Is.EqualTo(announced.Services));
                Assert.That(AlternativeServices.Clear.ToString(), Is.EqualTo("clear"));
            });

        }

        #endregion

        #region RepeatedFieldLinesAreOneList()

        /// <summary>
        /// The field is a list, so two lines are one value (RFC 9110, Section
        /// 5.3). The header section keeps them as an array, which the generic
        /// typed lookup returns as null.
        /// </summary>
        [Test]
        public void RepeatedFieldLinesAreOneList()
        {

            var response = HTTPResponse.Parse(
                               "HTTP/1.1 200 OK\r\n" +
                               "Alt-Svc: h3=\":443\"\r\n" +
                               "Alt-Svc: h2=\"alt.example:8443\"\r\n" +
                               "Content-Length: 0\r\n" +
                               "\r\n"
                           );

            var services = response.AltSvc?.Services;

            Assert.That(services?.Select(service => service.ProtocolId), Is.EqualTo(new[] { "h3", "h2" }));

        }

        #endregion

        #region TheServerAnnouncesThem()

        /// <summary>
        /// On the wire, from a handler that sets HTTPResponse.Builder.AltSvc.
        /// </summary>
        [Test]
        public async Task TheServerAnnouncesThem()
        {

            // A line break added at the end, so that the last field line has one too.
            var header = await RawHeader("/announce") + "\r\n";
            var clear  = await RawHeader("/clear")    + "\r\n";

            Assert.Multiple(() => {
                Assert.That(header, Does.Contain("\r\nAlt-Svc: h3=\":443\"; ma=3600, h2=\"alt.example:8443\"; persist=1\r\n"));
                Assert.That(clear,  Does.StartWith("HTTP/1.1 404"), "any status code may carry it");
                Assert.That(clear,  Does.Contain("\r\nAlt-Svc: clear\r\n"));
            });

        }

        #endregion

        #region TheClientReadsThem()

        [Test]
        public async Task TheClientReadsThem()
        {

            using var client = new HTTPClient(URL);

            var announcedResponse  = await client.GET(HTTPPath.Root + "announce", RequestTimeout: TimeSpan.FromSeconds(10));
            var clearResponse      = await client.GET(HTTPPath.Root + "clear",    RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(announcedResponse.AltSvc?.Services, Is.EqualTo(announced.Services));
                Assert.That(announcedResponse.AltSvc?.IsClear,  Is.False);
                Assert.That(clearResponse.    AltSvc?.IsClear,  Is.True);
            });

        }

        #endregion

        #region AltUsedReachesTheHandler()

        /// <summary>
        /// Section 5: a client using an alternative SHOULD send Alt-Used with
        /// every request.
        /// </summary>
        [Test]
        public async Task AltUsedReachesTheHandler()
        {

            using var client = new HTTPClient(URL);

            var used     = await client.RunRequest(
                                     HTTPMethod.GET,
                                     HTTPPath.Root + "used",
                                     RequestBuilder:  builder => builder.AltUsed = "alt.example:8443",
                                     RequestTimeout:  TimeSpan.FromSeconds(10)
                                 );

            var notUsed  = await client.GET(HTTPPath.Root + "used", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(used.   HTTPBodyAsUTF8String, Is.EqualTo("alt.example:8443"));
                Assert.That(notUsed.HTTPBodyAsUTF8String, Is.EqualTo("(none)"));
            });

        }

        #endregion

    }

}
