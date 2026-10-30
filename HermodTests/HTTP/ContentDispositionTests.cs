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
    /// Content-Disposition (RFC 6266): the grammar of Section 4.1, the file
    /// name rules of Section 4.3, and what Appendix D advises a sender.
    /// </summary>
    [TestFixture]
    public class ContentDispositionTests
    {

        #region (private static) Parsed(Text)

        private static HTTPContentDisposition Parsed(String Text)
        {
            Assert.That(HTTPContentDisposition.TryParse(Text, out var contentDisposition), Is.True, Text);
            return contentDisposition!;
        }

        #endregion


        #region RFC 6266, Section 5

        [Test]
        public void AnAttachmentWithAFileName()
        {

            var value = Parsed("Attachment; filename=example.html");

            Assert.Multiple(() => {
                Assert.That(value.Type,          Is.EqualTo("attachment"));
                Assert.That(value.IsAttachment,  Is.True);
                Assert.That(value.FileName,      Is.EqualTo("example.html"));
            });

        }

        [Test]
        public void InlineWithAQuotedFileName()
        {

            var value = Parsed("INLINE; FILENAME= \"an example.html\"");

            Assert.Multiple(() => {
                Assert.That(value.IsInline,  Is.True);
                Assert.That(value.FileName,  Is.EqualTo("an example.html"));
            });

        }

        [Test]
        public void AFileNameOutsideISO88591()
        {
            Assert.That(Parsed("attachment; filename*= UTF-8''%e2%82%ac%20rates").FileName, Is.EqualTo("€ rates"));
        }

        [Test]
        public void TheSameWithAFallback()
        {
            Assert.That(Parsed("attachment; filename=\"EURO rates\"; filename*=utf-8''%e2%82%ac%20rates").FileName, Is.EqualTo("€ rates"));
        }

        #endregion

        #region Parsing

        /// <summary>
        /// Section 4.3: "recipients SHOULD pick filename* and ignore filename",
        /// whichever comes first.
        /// </summary>
        [TestCase("attachment; filename=\"plain.txt\"; filename*=UTF-8''st%C3%A4r.txt")]
        [TestCase("attachment; filename*=UTF-8''st%C3%A4r.txt; filename=\"plain.txt\"")]
        public void FileNameStarWins(String Text)
        {
            Assert.That(Parsed(Text).FileName, Is.EqualTo("stär.txt"));
        }

        /// <summary>
        /// A filename* that does not decode is ignored (RFC 8187, Section 3.2.1),
        /// and filename stands.
        /// </summary>
        [TestCase("attachment; filename=\"plain.txt\"; filename*=KOI8-R''x")]
        [TestCase("attachment; filename=\"plain.txt\"; filename*=UTF-8''%ZZ")]
        [TestCase("attachment; filename=\"plain.txt\"; filename*=UTF-8''%C3")]
        public void AFileNameStarThatDoesNotDecodeLeavesTheFileName(String Text)
        {
            Assert.That(Parsed(Text).FileName, Is.EqualTo("plain.txt"));
        }

        /// <summary>
        /// Section 4.2: unknown types are handled the same way as attachment.
        /// </summary>
        [Test]
        public void AnUnknownTypeIsAnAttachment()
        {

            var value = Parsed("x-preview; filename=a.txt");

            Assert.Multiple(() => {
                Assert.That(value.Type,          Is.EqualTo("x-preview"));
                Assert.That(value.IsAttachment,  Is.True);
                Assert.That(value.IsInline,      Is.False);
            });

        }

        /// <summary>
        /// Section 4.4: unknown parameters are kept, and a name* that decodes
        /// replaces its name where that stood.
        /// </summary>
        [Test]
        public void ExtensionParametersAreKeptInOrder()
        {

            var value = Parsed("attachment; foo=plain; filename=a.txt; bar=\"q x\"; foo*=UTF-8''%C3%BC; baz*=UTF-8''%ZZ; baz=kept");

            Assert.That(value.Parameters.Select(parameter => $"{parameter.Key}={parameter.Value}"),
                        Is.EqualTo(new[] { "foo=ü", "bar=q x", "baz=kept" }));

        }

        /// <summary>
        /// Section 4.1: "values with multiple instances of the same parameter
        /// name are invalid", and so is what does not fit the grammar.
        /// </summary>
        [TestCase("attachment; filename=a.txt; filename=b.txt")]
        [TestCase("attachment; FILENAME=a.txt; filename=b.txt")]
        [TestCase("attachment; foo=1; foo=2")]
        [TestCase("")]
        [TestCase("; filename=a.txt")]
        [TestCase("attach ment")]
        [TestCase("attachment filename=a.txt")]
        [TestCase("attachment; filename")]
        [TestCase("attachment; filename=")]
        [TestCase("attachment; filename=\"unterminated")]
        [TestCase("attachment; filename=a b.txt")]
        [TestCase("attachment; filename*=\"UTF-8''quoted\"")]
        [TestCase("attachment; filename=\"a\u0007b\"")]
        public void WhatDoesNotFitTheGrammarIsRefused(String Text)
        {
            Assert.That(HTTPContentDisposition.TryParse(Text, out _), Is.False, Text);
        }

        [Test]
        public void QuotedPairsUnescape()
        {
            Assert.That(Parsed("attachment; filename=\"a \\\"b\\\" c.txt\"").FileName, Is.EqualTo("a \"b\" c.txt"));
        }

        #endregion

        #region Serialisation, Appendix D

        [TestCase("report.pdf",        "attachment; filename=report.pdf")]
        [TestCase("annual report.pdf", "attachment; filename=\"annual report.pdf\"")]
        [TestCase("Grüße.pdf",         "attachment; filename=\"Gruesse.pdf\"; filename*=UTF-8''Gr%C3%BC%C3%9Fe.pdf")]
        [TestCase("€ rates.txt",       "attachment; filename=\"_ rates.txt\"; filename*=UTF-8''%E2%82%AC%20rates.txt")]
        [TestCase("café.txt",          "attachment; filename=\"cafe.txt\"; filename*=UTF-8''caf%C3%A9.txt")]
        [TestCase("100%A9.txt",        "attachment; filename=\"100_A9.txt\"; filename*=UTF-8''100%25A9.txt")]
        [TestCase("a\"b.txt",          "attachment; filename=\"a_b.txt\"; filename*=UTF-8''a%22b.txt")]
        [TestCase("a\\b.txt",          "attachment; filename=\"a_b.txt\"; filename*=UTF-8''a%5Cb.txt")]
        public void AFileNameGoesOutInTheFormItNeeds(String FileName, String Expected)
        {

            var value = HTTPContentDisposition.Attachment(FileName);

            Assert.That(value.ToString(),                  Is.EqualTo(Expected));
            Assert.That(Parsed(value.ToString()).FileName, Is.EqualTo(FileName));

        }

        [Test]
        public void InlineAndParameters()
        {

            var value = new HTTPContentDisposition("inline", "a.txt", [ new ("Size", "123"), new ("note", "two words"), new ("motto", "Grüße") ]);

            Assert.That(value.ToString(), Is.EqualTo("inline; filename=a.txt; size=123; note=\"two words\"; motto*=UTF-8''Gr%C3%BC%C3%9Fe"));

        }

        [Test]
        public void WhatCannotBeSentIsRefused()
        {

            Assert.Multiple(() => {
                Assert.That(() => new HTTPContentDisposition("attach ment"),                                     Throws.ArgumentException);
                Assert.That(() => HTTPContentDisposition.Attachment(""),                                         Throws.ArgumentException);
                Assert.That(() => HTTPContentDisposition.Attachment("a\r\nSet-Cookie: x=y"),                     Throws.ArgumentException);
                Assert.That(() => new HTTPContentDisposition("attachment", null, [ new ("filename",  "x") ]),   Throws.ArgumentException);
                Assert.That(() => new HTTPContentDisposition("attachment", null, [ new ("x*",        "y") ]),   Throws.ArgumentException);
                Assert.That(() => new HTTPContentDisposition("attachment", null, [ new ("a", "1"), new ("A", "2") ]), Throws.ArgumentException);
            });

        }

        #endregion

        #region Section 4.3: the file name is advisory

        [TestCase("report.pdf",               "report.pdf")]
        [TestCase("../../etc/passwd",         "passwd")]
        [TestCase("..\\..\\boot.ini",         "boot.ini")]
        [TestCase("C:\\Windows\\evil.exe",    "evil.exe")]
        [TestCase("C:evil.exe",               "C_evil.exe")]
        [TestCase("  padded.txt  ",           "padded.txt")]
        [TestCase("a<b>c|d?e*f.txt",          "a_b_c_d_e_f.txt")]
        [TestCase("Grüße.pdf",                "Grüße.pdf")]
        [TestCase("..",                       null)]
        [TestCase(".",                        null)]
        [TestCase("~",                        null)]
        [TestCase("dir/",                     null)]
        [TestCase("nul",                      null)]
        [TestCase("NUL.txt",                  null)]
        [TestCase("com1.tar.gz",              null)]
        [TestCase("console.log",              "console.log")]
        public void ASafeFileName(String FileName, String? Expected)
        {
            Assert.That(HTTPContentDisposition.Attachment(FileName).SafeFileName, Is.EqualTo(Expected));
        }

        [Test]
        public void ControlCharactersThatArriveAreDropped()
        {
            Assert.That(Parsed("attachment; filename*=UTF-8''a%07b%0D%0A.txt").SafeFileName, Is.EqualTo("ab.txt"));
        }

        #endregion

        #region End to end

        /// <summary>
        /// A handler sets the typed field, and the client reads it back.
        /// </summary>
        [Test]
        public async Task AHandlersContentDispositionReachesTheClient()
        {

            await using var httpServer = await HTTPServer.StartNew();

            httpServer.AddHTTPAPI().AddHandler(
                HTTPPath.Root + "download",
                HTTPMethod:   HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                             new HTTPResponse.Builder(request) {
                                                 HTTPStatusCode      = HTTPStatusCode.OK,
                                                 ContentType         = HTTPContentType.Text.PLAIN,
                                                 ContentDisposition  = HTTPContentDisposition.Attachment("Grüße.txt"),
                                                 Content             = "Hallo".ToUTF8Bytes()
                                             }.AsImmutable
                                         )
            );

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{httpServer.TCPPort}"));

            var response = await client.GET(HTTPPath.Root + "download", RequestTimeout: TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(response.RawHTTPHeader,                     Does.Contain("Content-Disposition: attachment; filename=\"Gruesse.txt\"; filename*=UTF-8''Gr%C3%BC%C3%9Fe.txt"));
                Assert.That(response.ContentDisposition?.IsAttachment,  Is.True);
                Assert.That(response.ContentDisposition?.FileName,      Is.EqualTo("Grüße.txt"));
            });

        }

        /// <summary>
        /// The field is a singleton. Two field lines are not one value, and the
        /// client does not pick one of them.
        /// </summary>
        [Test]
        public async Task TwoFieldLinesAreNoValue()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var server = Task.Run(async () => {

                using var accepted = await listener.AcceptTcpClientAsync();
                var stream         = accepted.GetStream();
                var buffer         = new Byte[4096];
                var request        = new StringBuilder();

                while (!request.ToString().Contains("\r\n\r\n"))
                {
                    var read = await stream.ReadAsync(buffer);
                    if (read == 0) break;
                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes(
                          "HTTP/1.1 200 OK\r\n" +
                          "Content-Length: 0\r\n" +
                          "Content-Disposition: attachment; filename=a.txt\r\n" +
                          "Content-Disposition: attachment; filename=b.txt\r\n" +
                          "Connection: close\r\n" +
                          "\r\n"));

            });

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"));

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(10));

            await server;

            Assert.That(response.ContentDisposition, Is.Null);

        }

        #endregion

    }

}
