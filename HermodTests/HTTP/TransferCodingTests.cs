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

using System.Net;
using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// How many transfer codings a message declares, and which one is last.
    ///
    /// RFC 9112 Section 6.3 item 4 frames a body by the chunked coding only
    /// when chunked is the final coding; Section 6.1 additionally forbids a
    /// sender to apply chunked more than once. The second rule has no matching
    /// recipient requirement, which is exactly why it needs tests: a recipient
    /// is free to accept "chunked, chunked", and a recipient that does
    /// disagrees about where the body ends with every recipient that does not.
    /// That disagreement is what request smuggling is made of.
    ///
    /// Found by the A6 differential in the HTTP/1.1 conformance repository,
    /// which put this stack next to Go's net/http and Node's node:http and
    /// found the three of them reading the same octets three different ways.
    /// Filed there as H-29.
    ///
    /// The fixture deliberately walks all three parse paths, because they were
    /// not the same code and did not agree:
    ///
    ///   - HTTPRequest.TryParse(timestamp, source, …)  the server's own path
    ///   - HTTPRequest.TryParse(text, out request)     the public convenience
    ///   - HTTPResponse.TryParse(lines, out response)  what the client reads
    /// </summary>
    [TestFixture]
    public class TransferCodingTests
    {

        #region Data

        private static readonly HTTPSource  source  = new (IPSocket.LocalhostV4(IPPort.HTTP));
        private static readonly IPSocket    socket  = IPSocket.LocalhostV4(IPPort.HTTP);

        private static Boolean ParseRequestAsServer(String                 HeaderFields,
                                                    out HTTPRequest?       Request,
                                                    out HTTPResponse?      Failure)

            => HTTPRequest.TryParse(
                   Timestamp.Now,
                   source,
                   socket,
                   socket,
                   $"POST /echo HTTP/1.1\r\nHost: example.org\r\n{HeaderFields}\r\n",
                   out Request,
                   out Failure
               );

        #endregion


        #region The server refuses a chunked coding applied more than once

        /// <summary>
        /// All three spellings are the same message. RFC 9110 Section 5.3:
        /// repeated field lines of the same name are equivalent to one field
        /// line with the values joined by commas - so two "Transfer-Encoding:
        /// chunked" lines ARE "chunked, chunked", and a parser that treats
        /// them differently from each other has a second bug on top of the
        /// first.
        /// </summary>
        [TestCase("Transfer-Encoding: chunked, chunked",                        TestName = "one field line, the coding twice")]
        [TestCase("Transfer-Encoding: chunked\r\nTransfer-Encoding: chunked",   TestName = "two field lines")]
        [TestCase("Transfer-Encoding: chunked\r\nTransfer-Encoding:  chunked",  TestName = "two field lines, extra OWS")]
        [TestCase("Transfer-Encoding: chunked, chunked, chunked",               TestName = "three times")]
        public void Server_Rejects_Chunked_Applied_More_Than_Once(String headerFields)
        {

            var parsed = ParseRequestAsServer(headerFields, out var request, out var failure);

            Assert.Multiple(() => {
                Assert.That(parsed,                     Is.False, "the request was accepted");
                Assert.That(request,                    Is.Null);
                Assert.That(failure?.HTTPStatusCode,    Is.EqualTo(HTTPStatusCode.BadRequest));
            });

        }

        #endregion

        #region …and still accepts everything that is legal

        /// <summary>
        /// The counterweight. A rule that rejects too much is not an
        /// improvement on one that accepts too much, and a fixture without
        /// this half would score a parser that refuses all chunked messages
        /// as perfect.
        /// </summary>
        [TestCase("Transfer-Encoding: chunked",         TestName = "the ordinary case")]
        [TestCase("Transfer-Encoding: gzip, chunked",   TestName = "another coding first, chunked last")]
        [TestCase("Transfer-Encoding: ChUnKeD",         TestName = "transfer codings are case-insensitive")]
        [TestCase("Transfer-Encoding:\tchunked",        TestName = "HTAB is OWS")]
        [TestCase("Transfer-Encoding:   chunked",       TestName = "so are several spaces")]
        public void Server_Accepts_A_Single_Final_Chunked(String headerFields)
        {

            var parsed = ParseRequestAsServer(headerFields, out var request, out var failure);

            Assert.Multiple(() => {
                Assert.That(parsed,                                Is.True, failure?.HTTPStatusCode.ToString());
                Assert.That(request?.IsChunkedTransferEncoding,    Is.True);
                Assert.That(request?.HasUnframeableTransferEncoding, Is.False);
            });

        }

        /// <summary>
        /// RFC 9112 Section 6.3 item 4, the half that was already right: in a
        /// request, a Transfer-Encoding whose final coding is not chunked
        /// carries a MUST and a status code.
        /// </summary>
        [TestCase("Transfer-Encoding: chunked, gzip",   TestName = "chunked present but not last")]
        [TestCase("Transfer-Encoding: identity",        TestName = "a coding that is not chunked at all")]
        [TestCase("Transfer-Encoding: gzip",            TestName = "a known coding, used alone")]
        public void Server_Rejects_A_Final_Coding_That_Is_Not_Chunked(String headerFields)
        {

            var parsed = ParseRequestAsServer(headerFields, out _, out var failure);

            Assert.Multiple(() => {
                Assert.That(parsed,                  Is.False);
                Assert.That(failure?.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.BadRequest));
            });

        }

        #endregion

        #region Repeated field lines must not make the field disappear

        /// <summary>
        /// The defect underneath the defect, and the more dangerous of the two.
        ///
        /// Outside the server's own parse path, repeated field lines were kept
        /// as a String[]. GetHeaderField&lt;String&gt; cannot cast a String[] to
        /// a String, so it returned null - and a message carrying
        /// "Transfer-Encoding: chunked" twice was read as declaring no transfer
        /// coding whatsoever. Not "an odd one": none. Its chunk framing was then
        /// body octets, or worse, was left in the stream for the next read.
        /// </summary>
        [Test]
        public void Repeated_TransferEncoding_Lines_Combine_On_The_Request_Convenience_Path()
        {

            var parsed = HTTPRequest.TryParse(
                             "POST /echo HTTP/1.1\r\nHost: example.org\r\nTransfer-Encoding: chunked\r\nTransfer-Encoding: chunked\r\n\r\n",
                             out var request
                         );

            Assert.Multiple(() => {
                Assert.That(parsed,                                    Is.True);
                Assert.That(request?.TransferEncoding,                 Is.EqualTo("chunked, chunked"));
                Assert.That(request?.TransferCodings,                  Is.EqualTo(new[] { "chunked", "chunked" }));
                Assert.That(request?.IsChunkedTransferEncoding,        Is.False);
                Assert.That(request?.HasUnframeableTransferEncoding,   Is.True);
            });

        }

        [Test]
        public void Repeated_TransferEncoding_Lines_Combine_On_The_Response_Path()
        {

            var parsed = HTTPResponse.TryParse(
                             "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nTransfer-Encoding: chunked\r\n\r\n".Split("\r\n"),
                             out var response,
                             ConsumeChunkedTransferEncodingImmediately: false
                         );

            Assert.Multiple(() => {
                Assert.That(parsed,                                     Is.True);
                Assert.That(response?.TransferEncoding,                 Is.EqualTo("chunked, chunked"));
                Assert.That(response?.IsChunkedTransferEncoding,        Is.False);
                Assert.That(response?.HasUnframeableTransferEncoding,   Is.True);
            });

        }

        /// <summary>
        /// A single Transfer-Encoding line is unaffected by the combining, on
        /// every path. Worth its own test because the combining code is a new
        /// branch in a loop that every other field also goes through.
        /// </summary>
        [Test]
        public void A_Single_TransferEncoding_Line_Is_Unchanged_Everywhere()
        {

            HTTPRequest. TryParse("POST / HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: chunked\r\n\r\n", out var request);
            HTTPResponse.TryParse("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n".Split("\r\n"), out var response, ConsumeChunkedTransferEncodingImmediately: false);
            ParseRequestAsServer ("Transfer-Encoding: chunked", out var serverRequest, out _);

            Assert.Multiple(() => {
                Assert.That(request?.TransferEncoding,               Is.EqualTo("chunked"));
                Assert.That(response?.TransferEncoding,              Is.EqualTo("chunked"));
                Assert.That(serverRequest?.TransferEncoding,         Is.EqualTo("chunked"));
                Assert.That(request?.IsChunkedTransferEncoding,      Is.True);
                Assert.That(response?.IsChunkedTransferEncoding,     Is.True);
                Assert.That(serverRequest?.IsChunkedTransferEncoding, Is.True);
            });

        }

        /// <summary>
        /// Repeated lines of an unrelated field keep the behaviour they had -
        /// the new branch is for Transfer-Encoding only, and a change that
        /// leaked into every field would be a much larger one than this.
        /// </summary>
        [Test]
        public void Repeated_Lines_Of_Other_Fields_Are_Not_Affected()
        {

            var parsed = HTTPRequest.TryParse(
                             "GET / HTTP/1.1\r\nHost: a\r\nX-Custom: one\r\nX-Custom: two\r\n\r\n",
                             out var request
                         );

            Assert.Multiple(() => {
                Assert.That(parsed,                                  Is.True);
                Assert.That(request?.TransferEncoding,               Is.Null);
                Assert.That(request?.TransferCodings,                Is.Empty);
                Assert.That(request?.HasUnframeableTransferEncoding, Is.False);
            });

        }

        #endregion

    }

}
