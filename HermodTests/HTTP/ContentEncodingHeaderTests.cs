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
using System.IO.Compression;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The Content-Encoding header field carries content codings such as
    /// "gzip" or "br" (RFC 9110, section 8.4), not a character encoding.
    /// </summary>
    [TestFixture]
    public class ContentEncodingHeaderTests
    {

        #region (private) Request()

        private static HTTPRequest Request()

            => HTTPRequest.TryParse("GET /hello HTTP/1.1\r\nHost: example.test\r\n\r\n", out var request)
                   ? request
                   : throw new InvalidOperationException("The HTTP request could not be parsed!");

        #endregion


        #region Response_Lists_The_Content_Codings_In_Order()

        [Test]
        public void Response_Lists_The_Content_Codings_In_Order()
        {

            var response = HTTPResponse.Parse("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip, br\r\nContent-Length: 0\r\n\r\n");

            Assert.That(response.ContentEncoding,  Is.EqualTo(new[] { "gzip", "br" }));

        }

        #endregion

        #region Without_The_Header_Field_The_Body_Is_Not_Encoded()

        [Test]
        public void Without_The_Header_Field_The_Body_Is_Not_Encoded()
        {

            var response = HTTPResponse.Parse("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");

            Assert.That(response.ContentEncoding,  Is.Empty);

        }

        #endregion

        #region Request_Lists_The_Content_Coding()

        [Test]
        public void Request_Lists_The_Content_Coding()
        {

            var parsed = HTTPRequest.TryParse("POST /upload HTTP/1.1\r\nHost: example.test\r\nContent-Encoding: gzip\r\nContent-Length: 0\r\n\r\n", out var request);

            Assert.That(parsed,                     Is.True);
            Assert.That(request?.ContentEncoding,   Is.EqualTo(new[] { "gzip" }));

        }

        #endregion

        #region Builder_Serializes_The_Codings_As_One_List()

        [Test]
        public void Builder_Serializes_The_Codings_As_One_List()
        {

            var response = new HTTPResponse.Builder(Request()) {
                               HTTPStatusCode   = HTTPStatusCode.OK,
                               ContentEncoding  = [ "gzip", "br" ]
                           }.AsImmutable;

            Assert.That(response.RawHTTPHeader,                       Does.Contain("Content-Encoding: gzip, br"));
            Assert.That(response.ContentEncoding,                     Is.EqualTo(new[] { "gzip", "br" }));
            Assert.That(response.GetHeaderField("Content-Encoding"),  Is.EqualTo("gzip, br"));

        }

        #endregion

        #region An_Empty_List_Removes_The_Header_Field()

        [Test]
        public void An_Empty_List_Removes_The_Header_Field()
        {

            var builder = new HTTPResponse.Builder(Request()) {
                              HTTPStatusCode = HTTPStatusCode.OK
                          }.SetContentEncoding("gzip");

            Assert.That(builder.ContentEncoding,                Is.EqualTo(new[] { "gzip" }));

            builder.ContentEncoding = [];

            Assert.That(builder.ContentEncoding,                Is.Empty);
            Assert.That(builder.AsImmutable.RawHTTPHeader,      Does.Not.Contain("Content-Encoding"));

        }

        #endregion


        // Decoding — AHTTPPDU.DecodeBody(...) and friends, which is where the
        // header field above stops being an assertion about a string and starts
        // deciding what the body actually says.

        #region (private) Encoded(Coding, Text)

        private static readonly String payload = String.Concat(Enumerable.Repeat("Hermod speaks HTTP/1.1. ", 40));

        /// <summary>
        /// A response carrying <paramref name="Body"/> and declaring
        /// <paramref name="ContentEncoding"/>, as it would arrive from the wire.
        /// </summary>
        private static HTTPResponse ResponseWith(String? ContentEncoding, Byte[] Body)

            => HTTPResponse.Parse(
                   "HTTP/1.1 200 OK\r\n" +
                   "Content-Type: text/plain\r\n" +
                   (ContentEncoding is null ? "" : $"Content-Encoding: {ContentEncoding}\r\n") +
                   $"Content-Length: {Body.Length}\r\n\r\n",
                   Body
               );

        #endregion


        #region EverySupportedCodingRoundTrips()

        /// <summary>
        /// br, gzip and deflate, through the same seam a received message uses.
        /// </summary>
        [Test]
        public void EverySupportedCodingRoundTrips()
        {

            var identity = Encoding.UTF8.GetBytes(payload);

            Assert.Multiple(() => {

                foreach (var coding in HTTPContentCoding.Supported)
                {

                    var encoded  = HTTPContentCoding.Encode(identity, coding);
                    var response = ResponseWith(coding, encoded);

                    Assert.That(response.IsContentEncoded,             Is.True,                   coding);
                    Assert.That(response.ContentCodings,               Is.EqualTo(new[] { coding }));

                    // What arrived is not what was meant...
                    Assert.That(response.HTTPBody,                     Is.Not.EqualTo(identity),  coding);
                    Assert.That(encoded.Length,                        Is.LessThan(identity.Length), coding);

                    // ...and this is what was meant.
                    Assert.That(response.DecodeBody(),                 Is.EqualTo(identity),      coding);
                    Assert.That(response.DecodedBodyAsUTF8String(),    Is.EqualTo(payload),       coding);

                }

            });

        }

        #endregion

        #region StackedCodingsAreUndoneInReverseOrder()

        /// <summary>
        /// "Content-Encoding: gzip, br" means gzip was applied first and Brotli
        /// to the result, so undoing them runs the list backwards (RFC 9110,
        /// Section 8.4). With a single coding — every case in practice — the two
        /// orders are indistinguishable, which is exactly why this is a test and
        /// not a comment.
        /// </summary>
        [Test]
        public void StackedCodingsAreUndoneInReverseOrder()
        {

            var identity  = Encoding.UTF8.GetBytes(payload);
            var onTheWire = HTTPContentCoding.Encode(
                                HTTPContentCoding.Encode(identity, "gzip"),
                                "br"
                            );

            Assert.Multiple(() => {

                var right = ResponseWith("gzip, br", onTheWire);

                Assert.That(right.ContentCodings, Is.EqualTo(new[] { "gzip", "br" }));
                Assert.That(right.DecodeBody(),   Is.EqualTo(identity));

                // The same octets, claimed in the other order: Brotli output is
                // not gzip input, so the first step already fails. If the order
                // were ignored this would pass, and the test would be worthless.
                var wrong = ResponseWith("br, gzip", onTheWire);

                Assert.That(wrong.TryDecodeBody(out var body, out var errorResponse), Is.False);
                Assert.That(body,                                                     Is.Empty);
                Assert.That(errorResponse,                                            Is.Not.Null);

            });

        }

        #endregion

        #region IdentityIsTheAbsenceOfACoding()

        /// <summary>
        /// "identity" names the absence of a coding, so it must not send the
        /// body through a decoder — there is nothing to undo, and every decoder
        /// would reject the plain octets.
        /// </summary>
        [Test]
        public void IdentityIsTheAbsenceOfACoding()
        {

            var identity  = Encoding.UTF8.GetBytes(payload);
            var response  = ResponseWith("identity", identity);

            Assert.Multiple(() => {
                Assert.That(response.ContentEncoding,  Is.EqualTo(new[] { "identity" }));
                Assert.That(response.ContentCodings,   Is.Empty);
                Assert.That(response.IsContentEncoded, Is.False);
                Assert.That(response.DecodeBody(),     Is.EqualTo(identity));
            });

        }

        #endregion

        #region AMessageWithoutACodingReturnsItsBodyUnchanged()

        [Test]
        public void AMessageWithoutACodingReturnsItsBodyUnchanged()
        {

            var identity  = Encoding.UTF8.GetBytes(payload);
            var response  = ResponseWith(null, identity);

            Assert.Multiple(() => {
                Assert.That(response.IsContentEncoded, Is.False);
                Assert.That(response.DecodeBody(),     Is.EqualTo(identity));
                Assert.That(response.DecodeBody(),     Is.SameAs(response.HTTPBody), "the identity case must not copy the body");
            });

        }

        #endregion

        #region AnUnknownCodingIsRefusedRatherThanIgnored()

        /// <summary>
        /// "compress" (LZW) is registered and we cannot undo it. Returning the
        /// encoded octets as if they were the representation would be the one
        /// genuinely dangerous answer, so the coding has to be refused.
        /// </summary>
        [Test]
        public void AnUnknownCodingIsRefusedRatherThanIgnored()
        {

            var response = ResponseWith("compress", Encoding.UTF8.GetBytes(payload));

            Assert.Multiple(() => {

                Assert.That(response.IsContentEncoded, Is.True);
                Assert.Throws<NotSupportedException>(() => response.DecodeBody());

                Assert.That(response.TryDecodeBody(out var body, out var errorResponse), Is.False);
                Assert.That(body,                                                       Is.Empty);
                Assert.That(errorResponse,                                              Does.Contain("compress"));

            });

        }

        #endregion

        #region ADecompressionBombIsRefusedAtTheCeiling()

        /// <summary>
        /// 4 MiB of zeros gzip down to a few kilobytes. Decoding them is fine;
        /// decoding them under a smaller ceiling has to fail, and fail *during*
        /// decompression rather than after it, or the memory is already gone by
        /// the time anyone looks at the size.
        /// </summary>
        [Test]
        public void ADecompressionBombIsRefusedAtTheCeiling()
        {

            var bomb     = HTTPContentCoding.Encode(new Byte[4 * 1024 * 1024], "gzip");
            var response = ResponseWith("gzip", bomb);

            Assert.Multiple(() => {

                Assert.That(bomb.Length, Is.LessThan(64 * 1024), "the point of the test is that it is small on the wire");

                // Under a ceiling it cannot fit.
                Assert.That(response.TryDecodeBody(out var refused, out var errorResponse, 1024 * 1024), Is.False);
                Assert.That(refused,                                                                     Is.Empty);
                Assert.That(errorResponse,                                                               Does.Contain("limit"));

                // ...and the same octets under one it can.
                Assert.That(response.DecodeBody(16 * 1024 * 1024).Length, Is.EqualTo(4 * 1024 * 1024));

            });

        }

        #endregion

        #region DeflateIsAcceptedBothZLibWrappedAndRaw()

        /// <summary>
        /// RFC 9110 names RFC 1950 (zlib-wrapped) for "deflate", plenty of
        /// servers send RFC 1951 (raw), and .NET's DeflateStream reads only the
        /// latter. Both have to work, the way browsers cope with it.
        /// </summary>
        [Test]
        public void DeflateIsAcceptedBothZLibWrappedAndRaw()
        {

            var identity = Encoding.UTF8.GetBytes(payload);

            var raw      = HTTPContentCoding.Encode(identity, "deflate");

            var zlib     = new Func<Byte[]>(() => {
                               using var output = new MemoryStream();
                               using (var compressor = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
                                   compressor.Write(identity, 0, identity.Length);
                               return output.ToArray();
                           })();

            Assert.Multiple(() => {

                // The two really are different octets, so the sniffer has work to do.
                Assert.That(zlib, Is.Not.EqualTo(raw));

                Assert.That(ResponseWith("deflate", raw). DecodeBody(), Is.EqualTo(identity), "raw deflate (RFC 1951)");
                Assert.That(ResponseWith("deflate", zlib).DecodeBody(), Is.EqualTo(identity), "zlib-wrapped (RFC 1950)");

            });

        }

        #endregion

        #region RequestBodiesAreDecodedByTheSameSeam()

        /// <summary>
        /// The wiring sits on AHTTPPDU rather than on the response, so a request
        /// that arrives gzipped decodes through exactly the same code — which is
        /// the half a server needs and the half that did not exist before.
        /// </summary>
        [Test]
        public void RequestBodiesAreDecodedByTheSameSeam()
        {

            var identity = Encoding.UTF8.GetBytes(payload);
            var encoded  = HTTPContentCoding.Encode(identity, "gzip");

            Assert.That(
                HTTPRequest.TryParse(
                    "POST /echo HTTP/1.1\r\n" +
                    "Host: example.test\r\n" +
                    "Content-Type: text/plain\r\n" +
                    "Content-Encoding: gzip\r\n" +
                    $"Content-Length: {encoded.Length}\r\n\r\n",
                    encoded,
                    out var request
                ),
                Is.True,
                "The HTTP request could not be parsed!"
            );

            Assert.Multiple(() => {
                Assert.That(request!.IsContentEncoded,          Is.True);
                Assert.That(request. DecodeBody(),              Is.EqualTo(identity));
                Assert.That(request. DecodedBodyAsUTF8String(), Is.EqualTo(payload));
            });

        }

        #endregion

    }

}
