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

using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The server-wide content-coding filter (RFC 9110, Section 8.4): every
    /// handler's response compressed when the client said it could decompress
    /// one, and left alone in the rather longer list of cases where compressing
    /// it would be wrong rather than merely pointless.
    ///
    /// The client here does *not* decode, on purpose. What is under test is
    /// what goes out on the wire, so the tests read the encoded octets and undo
    /// them by hand — except the last one, which is the two halves of this
    /// meeting in the middle.
    /// </summary>
    [TestFixture]
    public class ServerContentCompressionTests
    {

        #region Data

        private static readonly String  compressibleText  = String.Join("\n", Enumerable.Range(0, 400).Select(i => $"line {i} of a very repetitive document"));

        private HTTPServer?  httpServer;
        private HTTPAPI?     httpAPI;

        private URL          URL
            => URL.Parse($"http://127.0.0.1:{httpServer!.TCPPort}");

        #endregion

        #region Setup / Teardown

        [OneTimeSetUp]
        public void Init()
        {

            httpServer = new HTTPServer(
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         ) {
                             AutomaticContentCompression = true
                         };

            httpAPI    = new HTTPAPI(httpServer);

            #region GET /text — the ordinary compressible case

            httpAPI.AddHandler(HTTPPath.Root + "text",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      Server          = "Hermod Test Server",
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = compressibleText.ToUTF8Bytes(),
                                                      ETag            = "\"v1\"",
                                                      Connection      = ConnectionType.Close
                                                  }.SetHeaderField("X-Handler-Said", "this must survive").
                                                    AsImmutable));

            #endregion

            #region HEAD /text — same headers, no body (RFC 9110, Section 9.3.2)

            httpAPI.AddHandler(HTTPPath.Root + "text",
                               HTTPMethod:    HTTPMethod.HEAD,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      Server          = "Hermod Test Server",
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = compressibleText.ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            #endregion

            #region GET /tiny — below the minimum

            httpAPI.AddHandler(HTTPPath.Root + "tiny",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = "short".ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            #endregion

            #region GET /image — a type that is already compressed

            httpAPI.AddHandler(HTTPPath.Root + "image",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Image.PNG,
                                                      Content         = compressibleText.ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            #endregion

            #region GET /already — the handler chose its own coding

            httpAPI.AddHandler(HTTPPath.Root + "already",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      ContentEncoding = [ "br" ],
                                                      Content         = HTTPContentCoding.Encode(compressibleText.ToUTF8Bytes(), "br"),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            #endregion

            #region GET /partial — a range of the selected representation

            httpAPI.AddHandler(HTTPPath.Root + "partial",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.PartialContent,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      ContentRange    = $"bytes 0-{compressibleText.Length - 1}/{compressibleText.Length * 2}",
                                                      Content         = compressibleText.ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            #endregion

            #region GET /varies — the handler already varies on something

            httpAPI.AddHandler(HTTPPath.Root + "varies",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = compressibleText.ToUTF8Bytes(),
                                                      Vary            = "Accept-Language",
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            #endregion

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {
            if (httpServer is not null)
                await httpServer.Stop();
        }

        #endregion

        #region (private) Get(Path, AcceptEncoding = null)

        /// <summary>
        /// A client that asks for a coding but does not undo it — so the assertions
        /// below are about the wire and not about the client.
        /// </summary>
        private async Task<HTTPResponse> Get(String   Path,
                                             String?  AcceptEncoding   = null,
                                             Boolean  Head             = false)
        {

            using var client = new HTTPClient(URL);

            return await client.RunRequest(
                             Head ? HTTPMethod.HEAD : HTTPMethod.GET,
                             HTTPPath.Parse(Path),
                             RequestBuilder:  builder => { if (AcceptEncoding is not null) builder.AcceptEncoding = AcceptEncoding; },
                             RequestTimeout:  TimeSpan.FromSeconds(10)
                         );

        }

        #endregion


        #region ACompressibleResponseIsCompressedWhenAsked()

        [Test]
        public async Task ACompressibleResponseIsCompressedWhenAsked()
        {

            var response = await Get("/text", "gzip");

            Assert.Multiple(() => {
                Assert.That(response.ContentEncoding,  Is.EqualTo(new[] { "gzip" }), response.EntirePDU);
                Assert.That(response.Vary,             Is.EqualTo("Accept-Encoding"));
                Assert.That(response.ContentLength,    Is.LessThan((UInt64) compressibleText.Length));
                Assert.That(response.ContentLength,    Is.EqualTo((UInt64) response.HTTPBody!.LongLength));
            });

            Assert.That(
                Encoding.UTF8.GetString(HTTPContentCoding.Decode(response.HTTPBody!, "gzip", 1024 * 1024)),
                Is.EqualTo(compressibleText)
            );

        }

        #endregion

        #region BrotliIsPreferredOverGzip()

        /// <summary>
        /// Both are offered, and the server's own order decides when the client
        /// expresses no preference of its own.
        /// </summary>
        [Test]
        public async Task BrotliIsPreferredOverGzip()
        {

            var response = await Get("/text", "gzip, br");

            Assert.That(response.ContentEncoding, Is.EqualTo(new[] { "br" }));

        }

        #endregion

        #region AQualityOfZeroIsARefusal()

        /// <summary>
        /// RFC 9110, Section 12.4.2: "gzip;q=0" does not ask for gzip, it rules it
        /// out. Reading the q-value as mere decoration is the easy mistake here.
        /// </summary>
        [Test]
        public async Task AQualityOfZeroIsARefusal()
        {

            var response = await Get("/text", "br;q=0, gzip;q=0");

            Assert.That(response.ContentEncoding,       Is.Empty, response.EntirePDU);
            Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo(compressibleText));

        }

        #endregion

        #region WithoutAnAcceptEncodingNothingIsCompressed()

        [Test]
        public async Task WithoutAnAcceptEncodingNothingIsCompressed()
        {

            var response = await Get("/text");

            Assert.Multiple(() => {
                Assert.That(response.ContentEncoding,       Is.Empty);
                Assert.That(response.Vary,                  Is.Null);
                Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo(compressibleText));
            });

        }

        #endregion

        #region ASmallBodyIsLeftAlone()

        [Test]
        public async Task ASmallBodyIsLeftAlone()
        {

            var response = await Get("/tiny", "gzip");

            Assert.That(response.ContentEncoding,       Is.Empty);
            Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo("short"));

        }

        #endregion

        #region AnAlreadyCompressedTypeIsLeftAlone()

        /// <summary>
        /// The content here would compress beautifully; image/png says it should
        /// not have to, and the type is what the decision is made on.
        /// </summary>
        [Test]
        public async Task AnAlreadyCompressedTypeIsLeftAlone()
        {

            var response = await Get("/image", "gzip");

            Assert.That(response.ContentEncoding, Is.Empty);
            Assert.That(response.HTTPBody,        Is.EqualTo(compressibleText.ToUTF8Bytes()));

        }

        #endregion

        #region AHandlersOwnCodingIsNotTouched()

        /// <summary>
        /// SinglePageAppHandler compresses static files once at startup and serves
        /// the result. A filter that compressed that again would be both wasteful
        /// and wrong.
        /// </summary>
        [Test]
        public async Task AHandlersOwnCodingIsNotTouched()
        {

            var response = await Get("/already", "gzip");

            Assert.That(response.ContentEncoding, Is.EqualTo(new[] { "br" }));

            Assert.That(
                Encoding.UTF8.GetString(HTTPContentCoding.Decode(response.HTTPBody!, "br", 1024 * 1024)),
                Is.EqualTo(compressibleText)
            );

        }

        #endregion

        #region APartialResponseIsNotCompressed()

        /// <summary>
        /// A 206 carries part of the selected representation; a content coding
        /// applies to the representation as a whole. Compressing after ranging
        /// describes neither, and the Content-Range would be measuring the wrong
        /// thing.
        /// </summary>
        [Test]
        public async Task APartialResponseIsNotCompressed()
        {

            var response = await Get("/partial", "gzip");

            Assert.That(response.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.PartialContent));
            Assert.That(response.ContentEncoding, Is.Empty);

        }

        #endregion

        #region VaryIsExtendedRatherThanReplaced()

        [Test]
        public async Task VaryIsExtendedRatherThanReplaced()
        {

            var response = await Get("/varies", "gzip");

            Assert.That(response.ContentEncoding, Is.EqualTo(new[] { "gzip" }));
            Assert.That(response.Vary,            Is.EqualTo("Accept-Language, Accept-Encoding"));

        }

        #endregion

        #region EverythingElseTheHandlerSaidSurvives()

        /// <summary>
        /// Compressing rebuilds the response rather than editing it, because an
        /// HTTPResponse serialises from the header text it was built with — a field
        /// changed afterwards would reach the log and not the wire. Which makes
        /// "did every other field come along" the thing most likely to go wrong,
        /// and worth asserting on a header nothing else cares about.
        /// </summary>
        [Test]
        public async Task EverythingElseTheHandlerSaidSurvives()
        {

            var response = await Get("/text", "gzip");

            Assert.Multiple(() => {
                Assert.That(response.ContentEncoding,                       Is.EqualTo(new[] { "gzip" }));
                Assert.That(response.GetHeaderField("X-Handler-Said"),      Is.EqualTo("this must survive"));
                Assert.That(response.Server,                                Is.EqualTo("Hermod Test Server"));
                Assert.That(response.ContentType?.ToString(),               Does.StartWith("text/plain"));
                Assert.That(response.HTTPStatusCode,                        Is.EqualTo(HTTPStatusCode.OK));
            });

        }

        #endregion

        #region AStrongETagDistinguishesTheEncodedRepresentation()

        /// <summary>
        /// RFC 9110, Section 8.8.3: gzip and identity are different
        /// representations, so a strong validator has to tell them apart — or a
        /// range request against a cached identity copy gets answered out of the
        /// compressed one.
        /// </summary>
        [Test]
        public async Task AStrongETagDistinguishesTheEncodedRepresentation()
        {

            var identity  = await Get("/text");
            var encoded   = await Get("/text", "gzip");

            Assert.Multiple(() => {
                Assert.That(identity.ETag,  Is.EqualTo("\"v1\""));
                Assert.That(encoded.ETag,   Is.EqualTo("\"v1-gzip\""));
            });

        }

        #endregion

        #region HeadAndGetAgreeOnTheLength()

        /// <summary>
        /// RFC 9110, Section 9.3.2: the header fields of a HEAD response should be
        /// the ones a GET would have sent. A filter that skipped HEAD because there
        /// is no body to compress would have them disagree about Content-Length,
        /// which is the one field a HEAD is usually asked for.
        /// </summary>
        [Test]
        public async Task HeadAndGetAgreeOnTheLength()
        {

            var get   = await Get("/text", "gzip");
            var head  = await Get("/text", "gzip", Head: true);

            Assert.Multiple(() => {
                Assert.That(head.ContentEncoding,  Is.EqualTo(new[] { "gzip" }));
                Assert.That(head.ContentLength,    Is.EqualTo(get.ContentLength));
                Assert.That(head.HTTPBody,         Is.Empty);
            });

        }

        #endregion

        #region TheTwoHalvesMeet()

        /// <summary>
        /// The whole of H-2 in one exchange: a Hermod client that asks, a Hermod
        /// server that compresses, and a caller that sees the text — while the wire
        /// carried a good deal less of it.
        /// </summary>
        [Test]
        public async Task TheTwoHalvesMeet()
        {

            using var client = new HTTPClient(URL) { AutomaticDecompression = true };

            var response = await client.GET(HTTPPath.Parse("/text"), RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo(compressibleText));
                Assert.That(response.DecodedContentEncoding,  Is.EqualTo("br"));
                Assert.That(response.ContentEncoding,         Is.Empty);
                Assert.That(response.RawHTTPHeader,           Does.Contain("Content-Encoding: br"));
            });

        }

        #endregion

        #region (unit) TheVaryFieldIsMergedRatherThanOverwritten(...)

        [Test]
        [TestCase(null,                            "Accept-Encoding")]
        [TestCase("",                              "Accept-Encoding")]
        [TestCase("Accept-Language",               "Accept-Language, Accept-Encoding")]
        [TestCase("Accept-Encoding",               "Accept-Encoding")]
        [TestCase("accept-encoding, Origin",       "accept-encoding, Origin")]
        [TestCase("*",                             "*")]
        public void TheVaryFieldIsMergedRatherThanOverwritten(String? Existing, String Expected)
        {
            Assert.That(HTTPContentCompression.WithAcceptEncoding(Existing), Is.EqualTo(Expected));
        }

        #endregion

    }

}
