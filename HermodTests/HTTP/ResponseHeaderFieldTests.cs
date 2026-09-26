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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Response header fields a handler has to be able to set through the
    /// response builder, rather than by spelling the field name out by hand.
    ///
    /// Both cases here were found the same way: a handler wanted a field, the
    /// builder did not have it or had it wired to another one, and the fallback
    /// worked well enough that nothing said so.
    /// </summary>
    [TestFixture]
    public class ResponseHeaderFieldTests
    {

        #region Data

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
                         );

            httpAPI    = new HTTPAPI(httpServer);

            #region GET /ranges     - a resource that accepts byte ranges

            httpAPI.AddHandler(HTTPPath.Root + "ranges",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      AcceptRanges    = "bytes",
                                                      Content         = "a representation with ranges".ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            #endregion

            #region GET /noranges   - a resource that accepts none

            httpAPI.AddHandler(HTTPPath.Root + "noranges",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      AcceptRanges    = "none",
                                                      Content         = "a representation without".ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

            #endregion

            #region GET /patchable  - a resource that advertises patch formats

            httpAPI.AddHandler(HTTPPath.Root + "patchable",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      AcceptPatch     = [ HTTPContentType.Application.JSON_UTF8 ],
                                                      Content         = "a patchable representation".ToUTF8Bytes(),
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

        #region (private) Get(Path)

        private async Task<HTTPResponse> Get(String Path)
        {

            using var client = new HTTPClient(URL);

            return await client.RunRequest(
                             HTTPMethod.GET,
                             HTTPPath.Parse(Path),
                             RequestTimeout:  TimeSpan.FromSeconds(10)
                         );

        }

        #endregion


        #region AcceptRangesIsAResponseField()

        /// <summary>
        /// RFC 9110, Section 14.3: Accept-Ranges is what a server says about a
        /// resource, so the response builder is where it belongs. It used to
        /// exist only on the request side - where its own summary already
        /// called it a response-header field - and a handler that wanted it had
        /// to fall back to SetHeaderField with the name spelled out.
        /// </summary>
        [Test]
        public async Task AcceptRangesIsAResponseField()
        {

            var response = await Get("/ranges");

            Assert.Multiple(() => {
                Assert.That(response.AcceptRanges,   Is.EqualTo("bytes"),  response.EntirePDU);
                Assert.That(response.RawHTTPHeader,  Does.Contain("Accept-Ranges: bytes"));
            });

        }

        #endregion

        #region AResourceThatAcceptsNoRangesSaysSo()

        /// <summary>
        /// The other half of Section 14.3: "none" is a thing a server may say,
        /// and it is not the same as saying nothing.
        /// </summary>
        [Test]
        public async Task AResourceThatAcceptsNoRangesSaysSo()
        {

            var response = await Get("/noranges");

            Assert.That(response.AcceptRanges, Is.EqualTo("none"), response.EntirePDU);

        }

        #endregion

        #region AcceptPatchDoesNotWriteAllow()

        /// <summary>
        /// The AcceptPatch setter on the builder wrote the Allow field: a
        /// handler advertising patch formats silently replaced the set of
        /// methods it claims to support, with media types. Both fields are
        /// lists and SetHeaderField takes an Object, so nothing complained -
        /// not the compiler, and not the wire.
        /// </summary>
        [Test]
        public async Task AcceptPatchDoesNotWriteAllow()
        {

            var response = await Get("/patchable");

            Assert.Multiple(() => {
                Assert.That(response.RawHTTPHeader,  Does.Contain("Accept-Patch: application/json"), response.EntirePDU);
                Assert.That(response.RawHTTPHeader,  Does.Not.Contain("Allow:"),                     response.EntirePDU);
                Assert.That(response.Allow,          Is.Empty);
            });

        }

        #endregion

    }

}
