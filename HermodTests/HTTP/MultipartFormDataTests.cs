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
    /// multipart/form-data (RFC 7578), framed as RFC 2046, Section 5.1.1 has it.
    ///
    /// The bodies of the first tests are what real senders sent, recorded on
    /// 2026-11-27 and copied octet for octet: Chromium 153 and Firefox 155
    /// through Playwright, Windows curl 8.23, and Python 3.13's requests 2.32
    /// and urllib3 2.3. The two browsers sent the same body but for its
    /// boundary.
    ///
    /// The parser it replaces, Hermod.MIME's Multipart, measured on the same
    /// kind of bodies: it kept the CRLF in front of every delimiter as the
    /// end of the content before it; threw on a preamble, on "--boundary" in
    /// the middle of a line of content, on a header section without its blank
    /// line, and on two field names that differ only in case; read
    /// name=token as "oke"; lost a file name with a ";" in it; dropped a part
    /// without a Content-Disposition, and a body cut off, without a word; and
    /// had no limits.
    /// </summary>
    [TestFixture]
    public class MultipartFormDataTests
    {

        #region Data

        private HTTPServer?  httpServer;
        private HTTPAPI?     httpAPI;

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

            // Lists what the server read of the form, one part per line.
            httpAPI.AddHandler(HTTPPath.Root + "form",
                               HTTPMethod:    HTTPMethod.POST,
                               HTTPDelegate:  request => {

                                   if (!request.TryParseMultipartFormDataRequestBody(out var formData, out var errorResponse, MaxParts: 4))
                                       return Task.FromResult(errorResponse);

                                   return Task.FromResult(
                                              new HTTPResponse.Builder(request) {
                                                  HTTPStatusCode  = HTTPStatusCode.OK,
                                                  ContentType     = HTTPContentType.Text.PLAIN,
                                                  Content         = String.Join(
                                                                        "\n",
                                                                        formData.Select(part => $"{part.Name}|{part.FileName ?? "-"}|{part.Text}")
                                                                    ).ToUTF8Bytes(),
                                                  Connection      = ConnectionType.Close
                                              }.AsImmutable);

                               });

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {
            if (httpServer is not null)
                await httpServer.DisposeAsync();
        }

        #endregion

        #region (private static) Parse(Body, Boundary, ...) / Refuse(Body, Boundary, ...)

        private static HTTPMultipartFormData Parse(String  Body,
                                                   String  Boundary)

            => Parse(Encoding.UTF8.GetBytes(Body), Boundary);

        private static HTTPMultipartFormData Parse(Byte[]  Body,
                                                   String  Boundary,
                                                   Int32   MaxParts            = HTTPMultipartFormData.DefaultMaxParts,
                                                   Int32   MaxPartHeaderSize   = HTTPMultipartFormData.DefaultMaxPartHeaderSize)
        {

            Assert.That(HTTPMultipartFormData.TryParse(Body, Boundary, out var formData, out var error, MaxParts, MaxPartHeaderSize),
                        Is.True,
                        error);

            return formData!;

        }

        private static String Refuse(String  Body,
                                     String  Boundary,
                                     Int32   MaxParts            = HTTPMultipartFormData.DefaultMaxParts,
                                     Int32   MaxPartHeaderSize   = HTTPMultipartFormData.DefaultMaxPartHeaderSize)
        {

            Assert.That(HTTPMultipartFormData.TryParse(Encoding.UTF8.GetBytes(Body), Boundary, out var formData, out var error, MaxParts, MaxPartHeaderSize),
                        Is.False,
                        $"Read {formData?.Parts.Count} parts from: {Body}");

            return error!;

        }

        #endregion

        #region (private static) BrowserForm(Boundary)

        /// <summary>
        /// The form Chromium 153 and Firefox 155 sent: a hidden "_charset_", a
        /// text field, a field whose name holds a quotation mark and a line
        /// break, a textarea, a file whose name holds quotation marks, and a
        /// file input left empty.
        /// </summary>
        private static String BrowserForm(String Boundary)

            => $"--{Boundary}\r\n" +
               "Content-Disposition: form-data; name=\"_charset_\"\r\n" +
               "\r\n" +
               "UTF-8\r\n" +
               $"--{Boundary}\r\n" +
               "Content-Disposition: form-data; name=\"comment\"\r\n" +
               "\r\n" +
               "Grüße, €\r\n" +
               $"--{Boundary}\r\n" +
               "Content-Disposition: form-data; name=\"a%22b%0D%0Ac\"\r\n" +
               "\r\n" +
               "v\r\n" +
               $"--{Boundary}\r\n" +
               "Content-Disposition: form-data; name=\"text\"\r\n" +
               "\r\n" +
               "a\r\n" +
               "b\r\n" +
               $"--{Boundary}\r\n" +
               "Content-Disposition: form-data; name=\"file\"; filename=\"notes %221%22.txt\"\r\n" +
               "Content-Type: text/plain\r\n" +
               "\r\n" +
               "line 1\r\n" +
               "line 2\r\n" +
               "\r\n" +
               $"--{Boundary}\r\n" +
               "Content-Disposition: form-data; name=\"empty\"; filename=\"\"\r\n" +
               "Content-Type: application/octet-stream\r\n" +
               "\r\n" +
               "\r\n" +
               $"--{Boundary}--\r\n";

        #endregion

        #region (private) Post(ContentType, Body, Chunked = false)

        /// <summary>
        /// A POST of the given body to /form, on the wire, and the response.
        /// </summary>
        private async Task<String> Post(String   ContentType,
                                        Byte[]   Body,
                                        Boolean  Chunked          = false,
                                        String?  ContentEncoding  = null)
        {

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, httpServer!.TCPPort.ToUInt16());

            var stream = tcp.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes("POST /form HTTP/1.1\r\n" +
                                                            "Host: 127.0.0.1\r\n" +
                                                            $"Content-Type: {ContentType}\r\n" +
                                                            (ContentEncoding is not null
                                                                 ? $"Content-Encoding: {ContentEncoding}\r\n"
                                                                 : "") +
                                                            (Chunked
                                                                 ? "Transfer-Encoding: chunked\r\n"
                                                                 : $"Content-Length: {Body.Length}\r\n") +
                                                            "Connection: close\r\n" +
                                                            "\r\n"));

            if (Chunked)
            {
                // Two chunks, cut inside a delimiter.
                var half = Body.Length / 2;
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"{half:x}\r\n"));
                await stream.WriteAsync(Body.AsMemory(0, half));
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"\r\n{Body.Length - half:x}\r\n"));
                await stream.WriteAsync(Body.AsMemory(half));
                await stream.WriteAsync("\r\n0\r\n\r\n"u8.ToArray());
            }
            else
                await stream.WriteAsync(Body);

            using var reader   = new StreamReader(stream, Encoding.UTF8);
            var       readTask = reader.ReadToEndAsync();

            if (await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(10))) != readTask)
                Assert.Fail("No response within ten seconds.");

            return await readTask;

        }

        private static String ContentOf(String Response)

            => Response[(Response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];

        #endregion


        #region ABrowsersForm_IsReadAsSent(Boundary)

        /// <summary>
        /// Every part of a browser's form: the field names and file names as
        /// sent, the content without the CRLF that belongs to the next
        /// delimiter, the form's charset, and a file input left empty, which a
        /// browser sends as a file with an empty name.
        /// </summary>
        [TestCase("----WebKitFormBoundaryhxReYqp6VzVUJYjU",               TestName = "ABrowsersForm_IsReadAsSent(Chromium)")]
        [TestCase("----geckoformboundarya213fc523f38284df6912498eb9d816", TestName = "ABrowsersForm_IsReadAsSent(Firefox)")]
        public void ABrowsersForm_IsReadAsSent(String Boundary)
        {

            var form = Parse(BrowserForm(Boundary), Boundary);

            Assert.Multiple(() => {

                Assert.That(form.Parts.Select(part => part.Name),  Is.EqualTo(new[] { "_charset_", "comment", "a%22b%0D%0Ac", "text", "file", "empty" }));
                Assert.That(form.Boundary,                         Is.EqualTo(Boundary));
                Assert.That(form.Charset,                          Is.EqualTo("UTF-8"));

                Assert.That(form["comment"]!.Text,                 Is.EqualTo("Grüße, €"));
                Assert.That(form["comment"]!.IsFile,               Is.False);
                Assert.That(form["comment"]!.ContentType,          Is.Null);
                Assert.That(form["a%22b%0D%0Ac"]!.Text,            Is.EqualTo("v"));
                Assert.That(form["text"]!.Text,                    Is.EqualTo("a\r\nb"));

                var file = form["file"]!;
                Assert.That(file.IsFile,                           Is.True);
                Assert.That(file.FileName,                         Is.EqualTo("notes %221%22.txt"));
                Assert.That(file.SafeFileName,                     Is.EqualTo("notes %221%22.txt"));
                Assert.That(file.ContentType,                      Is.EqualTo(HTTPContentType.Text.PLAIN));
                Assert.That(file.Content,                          Is.EqualTo("line 1\r\nline 2\r\n"u8.ToArray()));
                Assert.That(file.Headers.Select(header => header.Key),
                                                                   Is.EqualTo(new[] { "Content-Disposition", "Content-Type" }));

                var empty = form["empty"]!;
                Assert.That(empty.IsFile,                          Is.True);
                Assert.That(empty.FileName,                        Is.Empty);
                Assert.That(empty.SafeFileName,                    Is.Null);
                Assert.That(empty.Content,                         Is.Empty);
                Assert.That(empty.ContentType!.MediaType,          Is.EqualTo("application/octet-stream"));

            });

        }

        #endregion

        #region CurlsForm_IsReadAsSent()

        /// <summary>
        /// curl -F "comment=Hello" -F "file=@notes.txt" -F 'name with "quote"=x',
        /// with curl's boundary of 24 dashes and 22 random characters, and the
        /// quotation marks of the last name percent-encoded as browsers do.
        /// </summary>
        [Test]
        public void CurlsForm_IsReadAsSent()
        {

            var boundary = "------------------------Adwtp9ZfcFWKczPuNt4NpE";

            var form     = Parse("--------------------------Adwtp9ZfcFWKczPuNt4NpE\r\n" +
                                 "Content-Disposition: form-data; name=\"comment\"\r\n" +
                                 "\r\n" +
                                 "Hello\r\n" +
                                 "--------------------------Adwtp9ZfcFWKczPuNt4NpE\r\n" +
                                 "Content-Disposition: form-data; name=\"file\"; filename=\"notes.txt\"\r\n" +
                                 "Content-Type: text/plain\r\n" +
                                 "\r\n" +
                                 "line 1\r\n" +
                                 "line 2\r\n" +
                                 "\r\n" +
                                 "--------------------------Adwtp9ZfcFWKczPuNt4NpE\r\n" +
                                 "Content-Disposition: form-data; name=\"name with %22quote%22\"\r\n" +
                                 "\r\n" +
                                 "x\r\n" +
                                 "--------------------------Adwtp9ZfcFWKczPuNt4NpE--\r\n",
                                 boundary);

            Assert.Multiple(() => {
                Assert.That(form.Parts.Select(part => part.Name),  Is.EqualTo(new[] { "comment", "file", "name with %22quote%22" }));
                Assert.That(form["comment"]!.Text,                 Is.EqualTo("Hello"));
                Assert.That(form["file"]!.FileName,                Is.EqualTo("notes.txt"));
                Assert.That(form["file"]!.Content,                 Is.EqualTo("line 1\r\nline 2\r\n"u8.ToArray()));
                Assert.That(form["name with %22quote%22"]!.Text,   Is.EqualTo("x"));
                Assert.That(form.Charset,                          Is.Null);
            });

        }

        #endregion

        #region PythonsForms_AreReadAsSent()

        /// <summary>
        /// Python's requests and urllib3, whose boundaries are 32 hexadecimal
        /// digits without a dash. requests sends a file without a Content-Type,
        /// which makes it text/plain (RFC 7578, Section 4.4).
        /// </summary>
        [Test]
        public void PythonsForms_AreReadAsSent()
        {

            var requests = Parse("--6b8382dcb29259ce31e9960a69bdc8c3\r\n" +
                                 "Content-Disposition: form-data; name=\"comment\"\r\n" +
                                 "\r\n" +
                                 "Hello\r\n" +
                                 "--6b8382dcb29259ce31e9960a69bdc8c3\r\n" +
                                 "Content-Disposition: form-data; name=\"file\"; filename=\"notes.txt\"\r\n" +
                                 "\r\n" +
                                 "line 1\r\n" +
                                 "line 2\r\n" +
                                 "\r\n" +
                                 "--6b8382dcb29259ce31e9960a69bdc8c3--\r\n",
                                 "6b8382dcb29259ce31e9960a69bdc8c3");

            var urllib3  = Parse("--8225f6d64ac0f16cec00609e64aa8e63\r\n" +
                                 "Content-Disposition: form-data; name=\"comment\"\r\n" +
                                 "\r\n" +
                                 "Hello\r\n" +
                                 "--8225f6d64ac0f16cec00609e64aa8e63\r\n" +
                                 "Content-Disposition: form-data; name=\"file\"; filename=\"notes.txt\"\r\n" +
                                 "Content-Type: text/plain\r\n" +
                                 "\r\n" +
                                 "line 1\r\n" +
                                 "line 2\r\n" +
                                 "\r\n" +
                                 "--8225f6d64ac0f16cec00609e64aa8e63--\r\n",
                                 "8225f6d64ac0f16cec00609e64aa8e63");

            Assert.Multiple(() => {
                foreach (var form in new[] { requests, urllib3 })
                {
                    Assert.That(form["comment"]!.Text,   Is.EqualTo("Hello"));
                    Assert.That(form["file"]!.FileName,  Is.EqualTo("notes.txt"));
                    Assert.That(form["file"]!.Text,      Is.EqualTo("line 1\r\nline 2\r\n"));
                    Assert.That(form["file"]!.Charset,   Is.EqualTo("utf-8"));
                }
                Assert.That(requests["file"]!.ContentType,  Is.Null);
                Assert.That(urllib3 ["file"]!.ContentType,  Is.EqualTo(HTTPContentType.Text.PLAIN));
            });

        }

        #endregion

        #region TheRFCsExample_IsRead()

        /// <summary>
        /// RFC 7578, Section 4.5's example: header names in lower case, the
        /// part's charset in its Content-Type, and a
        /// Content-Transfer-Encoding, which is kept and not undone.
        /// </summary>
        [Test]
        public void TheRFCsExample_IsRead()
        {

            var form = Parse("--AaB03x\r\n" +
                             "content-disposition: form-data; name=\"field1\"\r\n" +
                             "content-type: text/plain;charset=UTF-8\r\n" +
                             "content-transfer-encoding: quoted-printable\r\n" +
                             "\r\n" +
                             "Joe owes =E2=82=AC10.00\r\n" +
                             "--AaB03x--\r\n",
                             "AaB03x");

            var part = form["field1"]!;

            Assert.Multiple(() => {
                Assert.That(form.Parts,              Has.Count.EqualTo(1));
                Assert.That(part.Charset,            Is.EqualTo("UTF-8").IgnoreCase);
                Assert.That(part.Text,               Is.EqualTo("Joe owes =E2=82=AC10.00"));
                Assert.That(part.Headers[2],         Is.EqualTo(new KeyValuePair<String, String>("content-transfer-encoding", "quoted-printable")));
            });

        }

        #endregion


        #region PreambleAndEpilogue_AreIgnored()

        /// <summary>
        /// Text before the first delimiter and after the close delimiter is
        /// ignored (RFC 2046, Section 5.1.1), even when it looks like a
        /// delimiter that is none.
        /// </summary>
        [Test]
        public void PreambleAndEpilogue_AreIgnored()
        {

            var form = Parse("This is the preamble. x--AaB03x\r\n" +
                             "--AaB03xY\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"a\"\r\n" +
                             "\r\n" +
                             "1\r\n" +
                             "--AaB03x--\r\n" +
                             "This is the epilogue.\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"b\"\r\n" +
                             "\r\n" +
                             "2\r\n",
                             "AaB03x");

            Assert.Multiple(() => {
                Assert.That(form.Parts.Select(part => part.Name),  Is.EqualTo(new[] { "a" }));
                Assert.That(form["a"]!.Text,                       Is.EqualTo("1"));
            });

        }

        #endregion

        #region TransportPadding_IsAccepted()

        /// <summary>
        /// Spaces and tabs after a delimiter, which receivers MUST handle, and a
        /// close delimiter that ends the body without a CRLF.
        /// </summary>
        [Test]
        public void TransportPadding_IsAccepted()
        {

            var form = Parse("--AaB03x \t\r\n" +
                             "Content-Disposition: form-data; name=\"a\"\r\n" +
                             "\r\n" +
                             "1\r\n" +
                             "--AaB03x\t\r\n" +
                             "Content-Disposition: form-data; name=\"b\"\r\n" +
                             "\r\n" +
                             "2\r\n" +
                             "--AaB03x--  ",
                             "AaB03x");

            Assert.Multiple(() => {
                Assert.That(form.Parts.Select(part => part.Text),  Is.EqualTo(new[] { "1", "2" }));
            });

        }

        #endregion

        #region ADelimiterIsOneOnlyAtTheStartOfALine()

        /// <summary>
        /// "--AaB03xY", "x--AaB03x" and a delimiter followed by anything but
        /// padding, "--" and a CRLF are content; the CRLF in front of the real
        /// delimiter is the delimiter's, and every CRLF before it is the
        /// content's.
        /// </summary>
        [Test]
        public void ADelimiterIsOneOnlyAtTheStartOfALine()
        {

            var content = "--AaB03xY\r\n" +
                          "x--AaB03x\r\n" +
                          "--AaB03x x\r\n" +
                          "--AaB03x--x\r\n" +
                          "\r\n";

            var form    = Parse("--AaB03x\r\n" +
                                "Content-Disposition: form-data; name=\"a\"\r\n" +
                                "\r\n" +
                                content +
                                "\r\n" +
                                "--AaB03x--\r\n",
                                "AaB03x");

            Assert.That(form["a"]!.Text, Is.EqualTo(content));

        }

        #endregion

        #region ABodyOfNoParts_HasNoParts()

        /// <summary>
        /// A close delimiter alone: a form with no field that has a value,
        /// which is how a browser sends one.
        /// </summary>
        [Test]
        public void ABodyOfNoParts_HasNoParts()
        {

            Assert.Multiple(() => {
                Assert.That(Parse("--AaB03x--\r\n", "AaB03x").Parts,  Is.Empty);
                Assert.That(Parse("--AaB03x--",     "AaB03x").Parts,  Is.Empty);
            });

        }

        #endregion

        #region AFieldNamedTwice_KeepsBothInOrder()

        /// <summary>
        /// Several files of one field (RFC 7578, Section 4.3), and a field the
        /// form repeats: the indexer has the first, GetAll every one in order,
        /// and names are compared as sent.
        /// </summary>
        [Test]
        public void AFieldNamedTwice_KeepsBothInOrder()
        {

            var form = Parse("--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"files\"; filename=\"a.txt\"\r\n" +
                             "\r\n" +
                             "A\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"Files\"\r\n" +
                             "\r\n" +
                             "other\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"files\"; filename=\"b.txt\"\r\n" +
                             "\r\n" +
                             "B\r\n" +
                             "--AaB03x--\r\n",
                             "AaB03x");

            Assert.Multiple(() => {
                Assert.That(form["files"]!.FileName,                                Is.EqualTo("a.txt"));
                Assert.That(form.GetAll("files").Select(part => part.FileName),     Is.EqualTo(new[] { "a.txt", "b.txt" }));
                Assert.That(form["Files"]!.Text,                                    Is.EqualTo("other"));
                Assert.That(form["FILES"],                                          Is.Null);
            });

        }

        #endregion

        #region ADispositionIsReadByItsGrammar()

        /// <summary>
        /// Content-Disposition's type and parameter names in any case, a token
        /// for a value, whitespace around ";" and "=", a parameter not known,
        /// "filename*" ignored as RFC 7578, Section 4.2 has a sender never use
        /// it, and an obs-fold unfolded.
        /// </summary>
        [Test]
        public void ADispositionIsReadByItsGrammar()
        {

            var form = Parse("--AaB03x\r\n" +
                             "Content-Disposition: Form-Data ; NAME = token ; x-other=\"1\"\r\n" +
                             "\r\n" +
                             "1\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"star\"; filename*=UTF-8''r%C3%A9sum%C3%A9.txt\r\n" +
                             "\r\n" +
                             "2\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data;\r\n" +
                             "\tname=\"folded\"\r\n" +
                             "\r\n" +
                             "3\r\n" +
                             "--AaB03x--\r\n",
                             "AaB03x");

            Assert.Multiple(() => {
                Assert.That(form.Parts.Select(part => part.Name),  Is.EqualTo(new[] { "token", "star", "folded" }));
                Assert.That(form["star"]!.IsFile,                  Is.False);
                Assert.That(form["star"]!.FileName,                Is.Null);
            });

        }

        #endregion

        #region AFileNameKeepsItsBackslashes()

        /// <summary>
        /// Browsers write a backslash in a file name as it is (WHATWG HTML), so
        /// a backslash quotes the character after it only when that is a
        /// quotation mark or another backslash. SafeFileName then keeps the
        /// last path segment, of a Windows path and of a relative one alike.
        /// </summary>
        [Test]
        public void AFileNameKeepsItsBackslashes()
        {

            var form = Parse("--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"windows\"; filename=\"C:\\Users\\x\\notes.txt\"\r\n" +
                             "\r\n" +
                             "1\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"escaped\"; filename=\"a\\\"b\\\\c\"\r\n" +
                             "\r\n" +
                             "2\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"relative\"; filename=\"../../etc/passwd\"\r\n" +
                             "\r\n" +
                             "3\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"tab\"; filename=\"a\tb.txt\"\r\n" +
                             "\r\n" +
                             "4\r\n" +
                             "--AaB03x\r\n" +
                             "Content-Disposition: form-data; name=\"device\"; filename=\"C:\\CON.txt\"\r\n" +
                             "\r\n" +
                             "5\r\n" +
                             "--AaB03x--\r\n",
                             "AaB03x");

            Assert.Multiple(() => {
                Assert.That(form["windows"]!.FileName,       Is.EqualTo("C:\\Users\\x\\notes.txt"));
                Assert.That(form["windows"]!.SafeFileName,   Is.EqualTo("notes.txt"));
                Assert.That(form["escaped"]!.FileName,       Is.EqualTo("a\"b\\c"));
                Assert.That(form["escaped"]!.SafeFileName,   Is.EqualTo("c"));
                Assert.That(form["relative"]!.SafeFileName,  Is.EqualTo("passwd"));
                Assert.That(form["tab"]!.FileName,           Is.EqualTo("a\tb.txt"));
                Assert.That(form["tab"]!.SafeFileName,       Is.EqualTo("ab.txt"));
                Assert.That(form["device"]!.SafeFileName,    Is.Null);
            });

        }

        #endregion

        #region TheFormsCharset_IsTheDefaultOfItsTextParts()

        /// <summary>
        /// "_charset_" names the charset of the parts that name none (RFC 7578,
        /// Section 4.6); a part's own charset comes first, and one .NET does not
        /// know leaves the part without a text.
        /// </summary>
        [Test]
        public void TheFormsCharset_IsTheDefaultOfItsTextParts()
        {

            var form = Parse(Encoding.Latin1.GetBytes("--AaB03x\r\n" +
                                                      "Content-Disposition: form-data; name=\"_charset_\"\r\n" +
                                                      "\r\n" +
                                                      "iso-8859-1\r\n" +
                                                      "--AaB03x\r\n" +
                                                      "Content-Disposition: form-data; name=\"latin\"\r\n" +
                                                      "\r\n" +
                                                      "Grüße\r\n" +
                                                      "--AaB03x\r\n" +
                                                      "Content-Disposition: form-data; name=\"own\"\r\n" +
                                                      "Content-Type: text/plain; charset=utf-8\r\n" +
                                                      "\r\n" +
                                                      "GrÃ¼Ã\u009fe\r\n" +
                                                      "--AaB03x\r\n" +
                                                      "Content-Disposition: form-data; name=\"unknown\"\r\n" +
                                                      "Content-Type: text/plain; charset=x-no-such-charset\r\n" +
                                                      "\r\n" +
                                                      "x\r\n" +
                                                      "--AaB03x--\r\n"),
                             "AaB03x");

            Assert.Multiple(() => {
                Assert.That(form.Charset,              Is.EqualTo("iso-8859-1"));
                Assert.That(form["latin"]!.Charset,    Is.EqualTo("iso-8859-1"));
                Assert.That(form["latin"]!.Text,       Is.EqualTo("Grüße"));
                Assert.That(form["own"]!.Charset,      Is.EqualTo("utf-8"));
                Assert.That(form["own"]!.Text,         Is.EqualTo("Grüße"));
                Assert.That(form["unknown"]!.Text,     Is.Null);
                Assert.That(form["unknown"]!.Content,  Is.EqualTo("x"u8.ToArray()));
            });

        }

        #endregion

        #region AHeaderNotInUTF8_IsReadOctetByOctet()

        /// <summary>
        /// A field name in Latin-1, which some old senders write: not UTF-8, so
        /// one character per octet, as HTTP reads a field.
        /// </summary>
        [Test]
        public void AHeaderNotInUTF8_IsReadOctetByOctet()
        {

            var form = Parse(Encoding.Latin1.GetBytes("--AaB03x\r\n" +
                                                      "Content-Disposition: form-data; name=\"café\"\r\n" +
                                                      "\r\n" +
                                                      "1\r\n" +
                                                      "--AaB03x--\r\n"),
                             "AaB03x");

            Assert.That(form.Parts[0].Name, Is.EqualTo("café"));

        }

        #endregion


        #region AMalformedBody_IsRefused(Body, Reason)

        /// <summary>
        /// Each a body RFC 7578 or RFC 2046 does not allow, with the reason the
        /// parser gives.
        /// </summary>
        [TestCase("",
                  "no delimiter",                                  TestName = "AMalformedBody_IsRefused(empty)")]
        [TestCase("Content-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n",
                  "no delimiter",                                  TestName = "AMalformedBody_IsRefused(NoDelimiter)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1",
                  "ends before its close delimiter",               TestName = "AMalformedBody_IsRefused(CutOffInAPart)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n--AaB03x\r\n",
                  "ends before its close delimiter",               TestName = "AMalformedBody_IsRefused(CutOffAfterADelimiter)")]
        [TestCase("--AaB03x\r\n\r\n1\r\n--AaB03x--",
                  "Part 1: it has no Content-Disposition",         TestName = "AMalformedBody_IsRefused(NoHeaders)")]
        [TestCase("--AaB03x\r\nContent-Type: text/plain\r\n\r\n1\r\n--AaB03x--",
                  "Part 1: it has no Content-Disposition",         TestName = "AMalformedBody_IsRefused(NoDisposition)")]
        [TestCase("--AaB03x\r\nContent-Disposition: attachment; name=\"a\"\r\n\r\n1\r\n--AaB03x--",
                  "not form-data with a name",                     TestName = "AMalformedBody_IsRefused(NotFormData)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; filename=\"a.txt\"\r\n\r\n1\r\n--AaB03x--",
                  "not form-data with a name",                     TestName = "AMalformedBody_IsRefused(NoName)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"; name=\"b\"\r\n\r\n1\r\n--AaB03x--",
                  "not form-data with a name",                     TestName = "AMalformedBody_IsRefused(NameTwice)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\r\n\r\n1\r\n--AaB03x--",
                  "not form-data with a name",                     TestName = "AMalformedBody_IsRefused(UnterminatedQuotedString)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=a b\r\n\r\n1\r\n--AaB03x--",
                  "not form-data with a name",                     TestName = "AMalformedBody_IsRefused(GarbageAfterAParameter)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Disposition: form-data; name=\"b\"\r\n\r\n1\r\n--AaB03x--",
                  "two Content-Disposition fields",                TestName = "AMalformedBody_IsRefused(TwoDispositions)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: text/plain\r\nContent-Type: text/html\r\n\r\n1\r\n--AaB03x--",
                  "two Content-Type fields",                       TestName = "AMalformedBody_IsRefused(TwoContentTypes)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: text\r\n\r\n1\r\n--AaB03x--",
                  "not a media type",                              TestName = "AMalformedBody_IsRefused(NoMediaType)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\nnot a field\r\n\r\n1\r\n--AaB03x--",
                  "a header line is not a field",                  TestName = "AMalformedBody_IsRefused(NotAField)")]
        [TestCase("--AaB03x\r\nBad Name: x\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n--AaB03x--",
                  "a header line is not a field",                  TestName = "AMalformedBody_IsRefused(NotATokenName)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\nX: y\r\n\r\n1\r\n--AaB03x--",
                  "bare CR or LF",                                 TestName = "AMalformedBody_IsRefused(BareLF)")]
        [TestCase("--AaB03x\r\n continued: x\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n--AaB03x--",
                  "begins with a continuation line",               TestName = "AMalformedBody_IsRefused(ContinuationFirst)")]
        [TestCase("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\n1\r\n--AaB03x--",
                  "its header section has no end",                 TestName = "AMalformedBody_IsRefused(NoBlankLine)")]
        public void AMalformedBody_IsRefused(String Body, String Reason)
        {
            Assert.That(Refuse(Body, "AaB03x"), Does.Contain(Reason));
        }

        #endregion

        #region ABoundaryRFC2046DoesNotAllow_IsRefused(Boundary)

        /// <summary>
        /// One to seventy bchars, the last not a space (RFC 2046,
        /// Section 5.1.1); the body is not even looked at.
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("AaB03x ")]
        [TestCase("AaB03x;")]
        [TestCase("AaB\"03x")]
        [TestCase("Grüße")]
        public void ABoundaryRFC2046DoesNotAllow_IsRefused(String? Boundary)
        {

            Assert.Multiple(() => {
                Assert.That(HTTPMultipartFormData.IsValidBoundary(Boundary),  Is.False);
                Assert.That(HTTPMultipartFormData.TryParse(Encoding.UTF8.GetBytes($"--{Boundary}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n--{Boundary}--"),
                                                           Boundary,
                                                           out _,
                                                           out var error),
                            Is.False);
                Assert.That(error, Does.Contain("boundary"));
            });

        }

        #endregion

        #region EveryBoundaryRFC2046Allows_IsAccepted()

        /// <summary>
        /// Every bchar, a space inside, one character, and seventy; and not
        /// seventy-one.
        /// </summary>
        [Test]
        public void EveryBoundaryRFC2046Allows_IsAccepted()
        {

            Assert.Multiple(() => {

                foreach (var boundary in new[] { "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
                                                 "'()+_,-./:=?",
                                                 "a b",
                                                 "x",
                                                 new String('-', 70) })
                {
                    Assert.That(HTTPMultipartFormData.IsValidBoundary(boundary), Is.True, boundary);
                    Assert.That(Parse($"--{boundary}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n--{boundary}--", boundary)["a"]!.Text,
                                Is.EqualTo("1"),
                                boundary);
                }

                Assert.That(HTTPMultipartFormData.IsValidBoundary(new String('-', 71)), Is.False);

            });

        }

        #endregion

        #region TheLimits_AreTheGivenOnes()

        /// <summary>
        /// A body of exactly MaxParts parts is read and one of one more is
        /// refused; so is a part header section one octet longer than
        /// MaxPartHeaderSize, with or without its end.
        /// </summary>
        [Test]
        public void TheLimits_AreTheGivenOnes()
        {

            var header = "Content-Disposition: form-data; name=\"a\"";

            String Body(Int32 Parts)
                => String.Concat(Enumerable.Repeat($"--AaB03x\r\n{header}\r\n\r\n1\r\n", Parts)) + "--AaB03x--";

            Assert.Multiple(() => {

                Assert.That(Parse(Encoding.UTF8.GetBytes(Body(3)), "AaB03x", MaxParts: 3).Parts,          Has.Count.EqualTo(3));
                Assert.That(Refuse(Body(4), "AaB03x", MaxParts: 3),                                       Does.Contain("more than 3 parts"));

                Assert.That(Parse(Encoding.UTF8.GetBytes(Body(1)), "AaB03x", MaxPartHeaderSize: header.Length).Parts,
                                                                                                          Has.Count.EqualTo(1));
                Assert.That(Refuse(Body(1), "AaB03x", MaxPartHeaderSize: header.Length - 1),              Does.Contain($"longer than {header.Length - 1} octets"));
                Assert.That(Refuse($"--AaB03x\r\n{header}\r\n1\r\n--AaB03x--", "AaB03x", MaxPartHeaderSize: 10),
                                                                                                          Does.Contain("longer than 10 octets"));

                Assert.That(HTTPMultipartFormData.DefaultMaxParts,           Is.EqualTo(1000));
                Assert.That(HTTPMultipartFormData.DefaultMaxPartHeaderSize,  Is.EqualTo(8192));

            });

        }

        #endregion

        #region ManyParts_AreReadInLinearTime()

        /// <summary>
        /// A thousand parts of eleven kilobytes of almost-delimiters each, which
        /// a search that started over from each part's start would choke on.
        /// </summary>
        [Test]
        public void ManyParts_AreReadInLinearTime()
        {

            var content  = String.Concat(Enumerable.Repeat("--AaB03\r\n-", 1_000));
            var body     = Encoding.ASCII.GetBytes(String.Concat(Enumerable.Repeat($"--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n{content}\r\n", 1000)) + "--AaB03x--");

            var watch    = System.Diagnostics.Stopwatch.StartNew();
            var form     = Parse(body, "AaB03x");

            Assert.Multiple(() => {
                Assert.That(form.Parts,                       Has.Count.EqualTo(1000));
                Assert.That(form.Parts[999].Content.Length,   Is.EqualTo(content.Length));
                Assert.That(watch.Elapsed,                    Is.LessThan(TimeSpan.FromSeconds(10)));
            });

        }

        #endregion


        #region ABodyBuilt_IsReadBackAsBuilt()

        /// <summary>
        /// ToBytes and TryParse agree: a field whose name holds a quotation
        /// mark, a CR and an LF, which go percent-encoded as browsers send them,
        /// a file with a delimiter-like line in it, and a file of no known type,
        /// which goes as application/octet-stream without a charset.
        /// </summary>
        [Test]
        public void ABodyBuilt_IsReadBackAsBuilt()
        {

            var binary = new Byte[] { 0x00, 0xFF, 0x0D, 0x0A, 0x2D, 0x2D, 0x0D, 0x0A, 0x0D, 0x0A };

            var built  = new HTTPMultipartFormData([
                             new HTTPFormDataPart("a\"b\r\nc",  "Grüße"),
                             new HTTPFormDataPart("upload",     "C:\\x\\image.png", binary, HTTPContentType.Image.PNG),
                             new HTTPFormDataPart("raw",        "data.bin",         [])
                         ]);

            var body   = built.ToBytes();
            var read   = Parse(body, built.Boundary);

            Assert.Multiple(() => {

                Assert.That(HTTPMultipartFormData.IsValidBoundary(built.Boundary),  Is.True);
                Assert.That(built.ContentType.MediaType,                            Is.EqualTo("multipart/form-data"));
                Assert.That(built.ContentType.MIMEBoundary,                         Is.EqualTo(built.Boundary));
                Assert.That(built.ContentType.ToString(),                           Does.Contain($"boundary=\"{built.Boundary}\""));

                Assert.That(Encoding.UTF8.GetString(body),                          Does.Contain("name=\"a%22b%0D%0Ac\""));
                Assert.That(read.Parts.Select(part => part.Name),                   Is.EqualTo(new[] { "a%22b%0D%0Ac", "upload", "raw" }));
                Assert.That(read.Parts[0].Text,                                     Is.EqualTo("Grüße"));
                Assert.That(read["upload"]!.FileName,                               Is.EqualTo("C:\\x\\image.png"));
                Assert.That(read["upload"]!.ContentType,                            Is.EqualTo(HTTPContentType.Image.PNG));
                Assert.That(read["upload"]!.Content,                                Is.EqualTo(binary));
                Assert.That(read["raw"]!.Content,                                   Is.Empty);
                Assert.That(read["raw"]!.ContentType!.ToString(),                   Is.EqualTo("application/octet-stream"));

            });

        }

        #endregion

        #region ABoundaryForABody_IsOneItsPartsDoNotHold()

        /// <summary>
        /// A boundary given that is not one, or that occurs in a part, which
        /// RFC 7578, Section 4.1 forbids, is refused; a new one is 24 dashes
        /// and 24 random letters and digits.
        /// </summary>
        [Test]
        public void ABoundaryForABody_IsOneItsPartsDoNotHold()
        {

            var newBoundary = HTTPMultipartFormData.NewBoundary();

            Assert.Multiple(() => {

                Assert.That(() => new HTTPMultipartFormData([ new HTTPFormDataPart("a", "x--AaB03x") ], "AaB03x"),
                            Throws.ArgumentException);

                Assert.That(() => new HTTPMultipartFormData([ new HTTPFormDataPart("a", "f.bin", "\r\n--AaB03x--"u8.ToArray()) ], "AaB03x"),
                            Throws.ArgumentException);

                Assert.That(() => new HTTPMultipartFormData([ new HTTPFormDataPart("AaB03x", "1") ], "AaB03x"),
                            Throws.ArgumentException);

                Assert.That(() => new HTTPMultipartFormData([ new HTTPFormDataPart("a", "AaB03x.txt", []) ], "AaB03x"),
                            Throws.ArgumentException);

                Assert.That(() => new HTTPMultipartFormData([ new HTTPFormDataPart("a", "1") ], "AaB03x "),
                            Throws.ArgumentException);

                Assert.That(new HTTPMultipartFormData([ new HTTPFormDataPart("a", "1") ], "AaB03x").Boundary,
                            Is.EqualTo("AaB03x"));

                Assert.That(newBoundary,                   Has.Length.EqualTo(48));
                Assert.That(newBoundary[..24],             Is.EqualTo(new String('-', 24)));
                Assert.That(newBoundary[24..].All(Char.IsAsciiLetterOrDigit),
                                                           Is.True);
                Assert.That(HTTPMultipartFormData.NewBoundary(),
                                                           Is.Not.EqualTo(newBoundary));

            });

        }

        #endregion


        #region TheServer_ReadsAFormFromTheWire(Chunked)

        /// <summary>
        /// A handler with TryParseMultipartFormDataRequestBody, the body sent
        /// with a Content-Length and chunked, cut inside a delimiter.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task TheServer_ReadsAFormFromTheWire(Boolean Chunked)
        {

            var boundary = "----WebKitFormBoundaryhxReYqp6VzVUJYjU";

            var response = await Post($"multipart/form-data; boundary={boundary}",
                                      Encoding.UTF8.GetBytes(BrowserForm(boundary).Replace("--" + boundary + "\r\nContent-Disposition: form-data; name=\"a%22b%0D%0Ac\"\r\n\r\nv\r\n", "").
                                                                                   Replace("--" + boundary + "\r\nContent-Disposition: form-data; name=\"text\"\r\n\r\na\r\nb\r\n", "")),
                                      Chunked);

            Assert.Multiple(() => {
                Assert.That(response,             Does.StartWith("HTTP/1.1 200"));
                Assert.That(ContentOf(response),  Is.EqualTo("_charset_|-|UTF-8\n" +
                                                             "comment|-|Grüße, €\n" +
                                                             "file|notes %221%22.txt|line 1\r\nline 2\r\n\n" +
                                                             "empty||"));
            });

        }

        #endregion

        #region TheServer_ReadsACompressedForm()

        /// <summary>
        /// A form in a content coding is read as what it encodes (RFC 9110,
        /// Section 8.4). One in a coding the stack cannot undo is 415 (Section
        /// 15.5.16), and one whose octets do not decode is 400.
        /// </summary>
        [Test]
        public async Task TheServer_ReadsACompressedForm()
        {

            var form = Encoding.UTF8.GetBytes("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\nGrüße\r\n--AaB03x--\r\n");

            using var compressed = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(form);

            var gzipped  = await Post("multipart/form-data; boundary=AaB03x", compressed.ToArray(), ContentEncoding: "gzip");
            var unknown  = await Post("multipart/form-data; boundary=AaB03x", form,                 ContentEncoding: "x-unknown");
            var corrupt  = await Post("multipart/form-data; boundary=AaB03x", form,                 ContentEncoding: "gzip");

            Assert.Multiple(() => {
                Assert.That(gzipped,             Does.StartWith("HTTP/1.1 200"));
                Assert.That(ContentOf(gzipped),  Is.EqualTo("a|-|Grüße"));
                Assert.That(unknown,             Does.StartWith("HTTP/1.1 415"));
                Assert.That(ContentOf(unknown),  Does.Contain("x-unknown"));
                Assert.That(corrupt,             Does.StartWith("HTTP/1.1 400"));
            });

        }

        #endregion

        #region TheServer_RefusesWhatIsNoForm()

        /// <summary>
        /// Another media type is 415 (RFC 9110, Section 15.5.16); a form
        /// without a boundary, a malformed one and one of more parts than the
        /// handler allows are 400, with the reason in the response.
        /// </summary>
        [Test]
        public async Task TheServer_RefusesWhatIsNoForm()
        {

            var form = Encoding.UTF8.GetBytes(BrowserForm("AaB03x"));

            var json            = await Post("application/json",                          "{}"u8.ToArray());
            var noBoundary      = await Post("multipart/form-data",                       form);
            var otherBoundary   = await Post("multipart/form-data; boundary=AaB03y",      form);
            var tooManyParts    = await Post("multipart/form-data; boundary=AaB03x",      form);
            var quotedBoundary  = await Post("multipart/form-data; boundary=\"AaB03x\"",  Encoding.UTF8.GetBytes("--AaB03x\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n--AaB03x--\r\n"));

            Assert.Multiple(() => {
                Assert.That(json,                       Does.StartWith("HTTP/1.1 415"));
                Assert.That(noBoundary,                 Does.StartWith("HTTP/1.1 400"));
                Assert.That(ContentOf(noBoundary),      Does.Contain("boundary"));
                Assert.That(otherBoundary,              Does.StartWith("HTTP/1.1 400"));
                Assert.That(ContentOf(otherBoundary),   Does.Contain("no delimiter"));
                Assert.That(tooManyParts,               Does.StartWith("HTTP/1.1 400"));
                Assert.That(ContentOf(tooManyParts),    Does.Contain("more than 4 parts"));
                Assert.That(quotedBoundary,             Does.StartWith("HTTP/1.1 200"));
                Assert.That(ContentOf(quotedBoundary),  Is.EqualTo("a|-|1"));
            });

        }

        #endregion

    }

}
