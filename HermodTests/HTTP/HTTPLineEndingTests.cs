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

using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The request and status lines the builders produce end with CRLF on every platform
    /// (RFC 9112, section 2.2). They used to be joined with Environment.NewLine, so that on
    /// Linux a request started with "GET / HTTP/1.1\n" and strict peers (Python's websockets,
    /// for one) closed the connection.
    /// </summary>
    [TestFixture]
    public class HTTPLineEndingTests
    {

        #region Data

        private static readonly Regex bareLineFeed = new (@"(?<!\r)\n", RegexOptions.Compiled);

        #endregion


        #region RequestBuilder_EndsTheRequestLineWithCRLF()

        [Test]
        public void RequestBuilder_EndsTheRequestLineWithCRLF()
        {

            using var httpClient = new HTTPClient(URL.Parse("http://127.0.0.1:1/"));

            var builder = new HTTPRequest.Builder(httpClient) {
                              Path  = HTTPPath.Parse("/hello"),
                              Host  = HTTPHostname.Parse("example.org")
                          };

            var header  = builder.EntireRequestHeader;

            Assert.Multiple(() => {
                Assert.That(header,                             Does.StartWith("GET /hello HTTP/1.1\r\n"));
                Assert.That(bareLineFeed.IsMatch(header),       Is.False, "no bare line feed in the request header: " + header.Replace("\r", "\\r").Replace("\n", "\\n"));
                Assert.That(builder.AsImmutable.EntirePDU,      Does.StartWith("GET /hello HTTP/1.1\r\n"));
                Assert.That(builder.AsImmutable.EntirePDU,      Does.Contain("\r\nHost: example.org"));
            });

        }

        #endregion

        #region ResponseBuilder_EndsTheStatusLineWithCRLF()

        [Test]
        public void ResponseBuilder_EndsTheStatusLineWithCRLF()
        {

            var builder = new HTTPResponse.Builder(
                              Timestamp.Now,
                              EventTracking_Id.New,
                              TimeSpan.Zero,
                              new HTTPSource(),
                              IPSocket.Zero,
                              IPSocket.Zero,
                              ConnectionType.Close,
                              HTTPStatusCode.OK,
                              "hello"
                          );

            var header  = builder.HTTPHeader;

            Assert.Multiple(() => {
                Assert.That(header,                             Does.StartWith("HTTP/1.1 200 OK\r\n"));
                Assert.That(header,                             Does.EndWith("\r\n\r\n"), "the header ends with exactly one empty line");
                Assert.That(header,                             Does.Not.EndWith("\r\n\r\n\r\n"));
                Assert.That(bareLineFeed.IsMatch(header),       Is.False, "no bare line feed in the response header: " + header.Replace("\r", "\\r").Replace("\n", "\\n"));
                Assert.That(builder.AsImmutable.EntirePDU,      Does.StartWith("HTTP/1.1 200 OK\r\n"));
            });

        }

        #endregion

    }

}
