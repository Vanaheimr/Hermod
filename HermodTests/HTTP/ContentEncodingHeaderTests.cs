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

    }

}
