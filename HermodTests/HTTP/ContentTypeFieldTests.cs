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
    /// H-51, Content-Type as RFC 9110, Section 8.3 has it: a media type whose
    /// type and subtype are case-insensitive, and parameters whose values are a
    /// token or a quoted string.
    ///
    /// The parser it replaces split the value on every ";", read the boundary
    /// from its first "----" on, and registered every type a peer named in a
    /// static table. So a boundary without four dashes in a row made the whole
    /// field unreadable, a quoted one kept its quotes, "Text/Plain" was another
    /// type than "text/plain", a registered type lost the charset the field gave
    /// it, and any peer could grow the table without bound, from every
    /// connection at once and without a lock.
    /// </summary>
    [TestFixture]
    public class ContentTypeFieldTests
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

            // Reports what the server made of the request's Content-Type.
            httpAPI.AddHandler(HTTPPath.Root + "type",
                               HTTPMethod:    HTTPMethod.POST,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = String.Join(
                                                                            "\n",
                                                                            $"type={request.ContentType?.MediaType ?? "-"}",
                                                                            $"charset={request.ContentType?.CharSet ?? "-"}",
                                                                            $"boundary={request.ContentType?.MIMEBoundary ?? "-"}",
                                                                            $"formdata={request.ContentType == HTTPContentType.Multipart.FORMDATA}"
                                                                        ).ToUTF8Bytes(),
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

        #region (private static) Parse(Text)

        private static HTTPContentType Parse(String Text)
        {

            Assert.That(HTTPContentType.TryParse(Text, out var contentType), Is.True, Text);

            return contentType!;

        }

        #endregion

        #region (private) Post(ContentType)

        /// <summary>
        /// A POST on the wire with the given Content-Type, as the server read it.
        /// </summary>
        private async Task<Dictionary<String, String>> Post(String ContentType)
        {

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, httpServer!.TCPPort.ToUInt16());

            var stream = tcp.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes("POST /type HTTP/1.1\r\n" +
                                                            "Host: 127.0.0.1\r\n" +
                                                            $"Content-Type: {ContentType}\r\n" +
                                                            "Content-Length: 2\r\n" +
                                                            "Connection: close\r\n" +
                                                            "\r\n" +
                                                            "ok"));

            using var reader   = new StreamReader(stream, Encoding.UTF8);
            var       readTask = reader.ReadToEndAsync();

            if (await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(10))) != readTask)
                Assert.Fail("No response within ten seconds.");

            var response = await readTask;

            Assert.That(response, Does.StartWith("HTTP/1.1 200"), response);

            return response[(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..].
                       Split('\n').
                       Select(line => line.Split('=', 2)).
                       ToDictionary(parts => parts[0], parts => parts[1]);

        }

        #endregion


        #region ARegisteredType_IsTheRegisteredOne()

        /// <summary>
        /// A field that says what the registry says comes back as the registered
        /// instance, file extensions and all.
        /// </summary>
        [Test]
        public void ARegisteredType_IsTheRegisteredOne()
        {

            Assert.Multiple(() => {
                Assert.That(Parse("application/json; charset=utf-8"),  Is.SameAs(HTTPContentType.Application.JSON_UTF8));
                Assert.That(Parse("text/html;charset=UTF-8"),          Is.SameAs(HTTPContentType.Text.HTML_UTF8));
                Assert.That(Parse("image/png"),                        Is.SameAs(HTTPContentType.Image.PNG));
            });

        }

        #endregion

        #region TypeAndSubtype_AreCaseInsensitive()

        /// <summary>
        /// "The type and subtype tokens are case-insensitive" (RFC 9110,
        /// Section 8.3.1), and so are parameter names (Section 5.6.6).
        /// </summary>
        [Test]
        public void TypeAndSubtype_AreCaseInsensitive()
        {

            var json = Parse("Application/JSON; CHARSET=utf-8");

            Assert.Multiple(() => {
                Assert.That(json,                                                    Is.EqualTo(HTTPContentType.Application.JSON_UTF8));
                Assert.That(json.GetHashCode(),                                      Is.EqualTo(HTTPContentType.Application.JSON_UTF8.GetHashCode()));
                Assert.That(json.MediaType,                                          Is.EqualTo("application/json"));
                Assert.That(json.CharSet,                                            Is.EqualTo("utf-8"));
                Assert.That(Parse("TEXT/PLAIN") == HTTPContentType.Text.PLAIN,       Is.True);
                Assert.That(Parse("X-Test/Thing"),                                   Is.EqualTo(Parse("x-test/thing")));
                Assert.That(Parse("X-Test/Thing").MediaType,                         Is.EqualTo("x-test/thing"));
                Assert.That(Parse("TEXT/PLAIN").CompareTo(HTTPContentType.Text.PLAIN), Is.Zero);
                Assert.That(HTTPContentType.ForMediaType("Application/JSON"),        Is.SameAs(HTTPContentType.Application.JSON_UTF8));
            });

        }

        #endregion

        #region ATypeBuiltInAnyCase_IsTheSameType()

        /// <summary>
        /// A type an application builds itself, in whatever case, equals, hashes
        /// and sorts as the registered one: the parser is not the only source of
        /// types.
        /// </summary>
        [Test]
        public void ATypeBuiltInAnyCase_IsTheSameType()
        {

            var plain = new HTTPContentType("Text", "PLAIN", null, null, null);

            Assert.Multiple(() => {
                Assert.That(plain == HTTPContentType.Text.PLAIN,                    Is.True);
                Assert.That(plain.Equals(HTTPContentType.Text.PLAIN),               Is.True);
                Assert.That(plain.GetHashCode(),                                    Is.EqualTo(HTTPContentType.Text.PLAIN.GetHashCode()));
                Assert.That(plain.CompareTo(HTTPContentType.Text.PLAIN),            Is.Zero);
                Assert.That(new HashSet<HTTPContentType> { HTTPContentType.Text.PLAIN }.Contains(plain), Is.True);
            });

        }

        #endregion

        #region TheFieldsCharset_IsKept()

        /// <summary>
        /// The charset is the one the field names, not the one the type was
        /// registered with; and none when it names none.
        /// </summary>
        [Test]
        public void TheFieldsCharset_IsKept()
        {

            Assert.Multiple(() => {
                Assert.That(Parse("text/plain; charset=iso-8859-1").CharSet,    Is.EqualTo("iso-8859-1"));
                Assert.That(Parse("text/plain; charset=\"iso-8859-1\"").CharSet, Is.EqualTo("iso-8859-1"));
                Assert.That(Parse("text/plain").CharSet,                         Is.Null);
                Assert.That(Parse("text/plain; charset=iso-8859-1"),             Is.EqualTo(HTTPContentType.Text.PLAIN), "still text/plain");
                Assert.That(Parse("text/plain; charset=iso-8859-1").FileExtensions, Is.EqualTo(HTTPContentType.Text.PLAIN.FileExtensions));
            });

        }

        #endregion

        #region TheBoundary_IsTheParameter(Field, Boundary)

        /// <summary>
        /// The boundary is the parameter's value, whatever characters RFC 2046
        /// allows it: the examples of RFC 2046 and RFC 7578, those of browsers,
        /// curl and Python, and quoted ones, whose quotes are not part of it.
        /// </summary>
        [TestCase("multipart/form-data; boundary=AaB03x",                                  "AaB03x")]
        [TestCase("multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW",   "----WebKitFormBoundary7MA4YWxkTrZu0gW")]
        [TestCase("multipart/form-data; boundary=------------------------d74496d66958873e", "------------------------d74496d66958873e")]
        [TestCase("multipart/form-data; boundary=8c2fb3ea33a1e8ed2b4a6a7c8f0d1e2b",        "8c2fb3ea33a1e8ed2b4a6a7c8f0d1e2b")]
        [TestCase("multipart/form-data; boundary=\"simple boundary\"",                    "simple boundary")]
        [TestCase("multipart/form-data; boundary=\"a;b=c,d\"",                            "a;b=c,d")]
        [TestCase("multipart/form-data;boundary=x;charset=utf-8",                         "x")]
        [TestCase("multipart/form-data; charset=utf-8; BOUNDARY=x",                       "x")]
        public void TheBoundary_IsTheParameter(String Field, String Boundary)
        {

            var contentType = Parse(Field);

            Assert.Multiple(() => {
                Assert.That(contentType.MIMEBoundary,                               Is.EqualTo(Boundary));
                Assert.That(contentType == HTTPContentType.Multipart.FORMDATA,      Is.True);
            });

        }

        #endregion

        #region AnInvalidField_IsNoMediaType(Field)

        /// <summary>
        /// What does not fit Section 8.3 is no media type, rather than the part of
        /// it that could be read; a parameter named twice is two readings.
        /// </summary>
        [TestCase("")]
        [TestCase("text")]
        [TestCase("text/")]
        [TestCase("/plain")]
        [TestCase("text/plain/html")]
        [TestCase("text /plain")]
        [TestCase("text/pl ain")]
        [TestCase("text/plain charset=utf-8")]
        [TestCase("text/plain; charset")]
        [TestCase("text/plain; charset=")]
        [TestCase("text/plain; charset = utf-8")]
        [TestCase("text/plain; charset =utf-8")]
        [TestCase("text/plain; charset= utf-8")]
        [TestCase("text/plain; charset=\"utf-8")]
        [TestCase("text/plain; charset=utf 8")]
        [TestCase("multipart/form-data; boundary=a; boundary=b")]
        [TestCase("multipart/form-data; boundary=a; Boundary=a")]
        public void AnInvalidField_IsNoMediaType(String Field)
        {

            Assert.That(HTTPContentType.TryParse(Field, out var contentType), Is.False, Field);
            Assert.That(contentType, Is.Null);

        }

        #endregion

        #region AControlCharacterInAQuotedString_IsNoMediaType()

        /// <summary>
        /// qdtext and quoted-pair hold HTAB, SP, visible characters and obs-text,
        /// and no other control character, quoted or not (RFC 9110,
        /// Section 5.6.4). In a loop rather than as test cases: NUnit does not run
        /// a case whose name holds a control character.
        /// </summary>
        [Test]
        public void AControlCharacterInAQuotedString_IsNoMediaType()
        {

            var accepted = new List<Int32>();

            foreach (var code in Enumerable.Range(0, 0x20).Append(0x7F))
            {

                if (code == '\t')
                    continue;

                var character = (Char) code;

                if (HTTPContentType.TryParse($"text/plain; charset=\"utf{character}8\"",  out _) ||
                    HTTPContentType.TryParse($"text/plain; charset=\"utf\\{character}8\"", out _))
                {
                    accepted.Add(code);
                }

            }

            Assert.Multiple(() => {
                Assert.That(accepted,                                                                 Is.Empty);
                Assert.That(HTTPContentType.TryParse("text/plain; charset=\"utf\t8\"", out var tab),  Is.True, "HTAB is qdtext");
                Assert.That(tab!.CharSet,                                                             Is.EqualTo("utf\t8"));
            });

        }

        #endregion

        #region EmptyParameters_AreNothing()

        /// <summary>
        /// "parameters = *( OWS ";" OWS [ parameter ] )" (Section 5.6.6): an empty
        /// one is allowed, and is nothing.
        /// </summary>
        [Test]
        public void EmptyParameters_AreNothing()
        {

            Assert.Multiple(() => {
                Assert.That(Parse("text/plain;"),                         Is.EqualTo(HTTPContentType.Text.PLAIN));
                Assert.That(Parse("text/plain;").CharSet,                 Is.Null);
                Assert.That(Parse("text/plain; charset=utf-8;"),          Is.SameAs(HTTPContentType.Text.PLAIN));
                Assert.That(Parse("text/plain;;charset=utf-8").CharSet,   Is.EqualTo("utf-8"));
                Assert.That(Parse(" text/plain ; charset=utf-8 ").CharSet, Is.EqualTo("utf-8"));
            });

        }

        #endregion

        #region AQuotedPair_IsTheCharacterItQuotes()

        [Test]
        public void AQuotedPair_IsTheCharacterItQuotes()

            => Assert.That(Parse("multipart/form-data; boundary=\"a\\\"b\\\\c\"").MIMEBoundary, Is.EqualTo("a\"b\\c"));

        #endregion

        #region AnUnknownType_IsNotRegistered()

        /// <summary>
        /// Reading a type a peer named leaves the registry as it was: no table
        /// grows by what peers send.
        /// </summary>
        [Test]
        public void AnUnknownType_IsNotRegistered()
        {

            var mediaType = $"x-h51/{Guid.NewGuid():N}";

            Assert.That(HTTPContentType.TryParse(mediaType, out var contentType), Is.True);

            Assert.Multiple(() => {
                Assert.That(contentType!.MediaType,                                  Is.EqualTo(mediaType));
                Assert.That(HTTPContentType.ForMediaType(mediaType),                 Is.SameAs(HTTPContentType.ALL), "not registered");
                Assert.That(HTTPContentType.TryParse(mediaType, out var again),      Is.True);
                Assert.That(again,                                                   Is.EqualTo(contentType));
            });

        }

        #endregion

        #region ManyUnknownTypesAtOnce_AreRead()

        /// <summary>
        /// Parsing from many connections at once: the old parser wrote into one
        /// Dictionary from all of them, which is undefined, and threw when two
        /// registered the same new type.
        /// </summary>
        [Test]
        public void ManyUnknownTypesAtOnce_AreRead()
        {

            var failures = 0;

            Parallel.For(0, 20_000, i => {
                if (!HTTPContentType.TryParse($"x-h51-race/t{i % 50}-{i}", out _) ||
                    !HTTPContentType.TryParse($"x-h51-race/same", out _))
                {
                    Interlocked.Increment(ref failures);
                }
            });

            Assert.That(failures, Is.Zero);

        }

        #endregion


        #region TheServer_ReadsTheFieldAsItIs(Field, Type, Charset, Boundary)

        /// <summary>
        /// The request's Content-Type, as a handler sees it.
        /// </summary>
        [TestCase("multipart/form-data; boundary=AaB03x",       "multipart/form-data", "-",          "AaB03x")]
        [TestCase("Multipart/Form-Data; boundary=\"a b\"",      "multipart/form-data", "-",          "a b")]
        [TestCase("text/plain; charset=iso-8859-1",              "text/plain",          "iso-8859-1", "-")]
        [TestCase("text/plain",                                  "text/plain",          "-",          "-")]
        public async Task TheServer_ReadsTheFieldAsItIs(String Field, String Type, String Charset, String Boundary)
        {

            var report = await Post(Field);

            Assert.Multiple(() => {
                Assert.That(report["type"],      Is.EqualTo(Type));
                Assert.That(report["charset"],   Is.EqualTo(Charset));
                Assert.That(report["boundary"],  Is.EqualTo(Boundary));
                Assert.That(report["formdata"],  Is.EqualTo((Type == "multipart/form-data").ToString()));
            });

        }

        #endregion

    }

}
