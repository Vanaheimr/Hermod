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
    /// A request whose content is converted - an HTTPRequest&lt;TContent&gt; made
    /// from an HTTPRequest - is the same request: its method, path and version,
    /// and what the server set on it.
    /// </summary>
    /// <remarks>
    /// The copy took the protocol name, the request-target and the query string,
    /// but not the method, the path or the version: a converted PUT /items/42
    /// HTTP/1.0 was a GET of no path in a version of none, and it had lost the
    /// URL parameters, the user and the connection the server had given it.
    /// </remarks>
    [TestFixture]
    public class HTTPRequestCopyTests
    {

        #region A_Converted_Request_Is_The_Same_Request()

        [Test]
        public void A_Converted_Request_Is_The_Same_Request()
        {

            var parsed   = HTTPRequest.TryParse("PUT /items/42?x=1 HTTP/1.0\r\nHost: example.test\r\nContent-Length: 5\r\n\r\nhello",
                                                out var parsedRequest);

            Assert.That(parsed, Is.True);

            var request  = parsedRequest!;

            request.FakeURLPrefix         = "/prefix";
            request.ParsedURLParameters   = [ "42" ];
            request.ParsedURLParametersX  = new () { ["itemId"] = "42" };
            request.SubprotocolRequest    = "subprotocol";
            request.UseChunkWorker        = true;

            var converted = request.ConvertContent<String?>((String? text, OnExceptionDelegate? _) => text?.ToUpperInvariant());

            Assert.Multiple(() => {

                Assert.That(converted.Content,                       Is.EqualTo("HELLO"));

                Assert.That(converted.HTTPMethod,                    Is.EqualTo(HTTPMethod.PUT));
                Assert.That(converted.Path,                          Is.EqualTo(HTTPPath.Parse("/items/42")));
                Assert.That(converted.QueryString.ToString(),        Is.EqualTo(request.QueryString.ToString()));
                Assert.That(converted.ProtocolName,                  Is.EqualTo("HTTP"));
                Assert.That(converted.ProtocolVersion,               Is.EqualTo(HTTPVersion.HTTP_1_0));
                Assert.That(converted.RequestTarget,                 Is.EqualTo("/items/42?x=1"));
                Assert.That(converted.EntireRequestHeader,           Is.EqualTo(request.EntireRequestHeader));

                Assert.That(converted.FakeURLPrefix,                 Is.EqualTo("/prefix"));
                Assert.That(converted.ParsedURLParameters,           Is.EqualTo(new[] { "42" }));
                Assert.That(converted.TryGetURLParameter("itemId"),  Is.EqualTo("42"));
                Assert.That(converted.SubprotocolRequest,            Is.EqualTo("subprotocol"));
                Assert.That(converted.UseChunkWorker,                Is.True);
                Assert.That(converted.Host,                          Is.EqualTo(request.Host));

            });

        }

        #endregion

    }

}
