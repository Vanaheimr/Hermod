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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RegisterFileSystemFile, over the wire. H-34 in HTTP1ConformanceTests.
    /// </summary>
    /// <remarks>
    /// Nothing had called it, here or in any repository using today's HTTPAPI.
    /// Four things were wrong with it, found by sending requests:
    ///
    /// A directory was a 500 whose body was the exception message, the
    /// absolute path on the server - the leak H-33 closed in
    /// MapFileSystemFolder, still open next door.
    ///
    /// File.Exists was consulted only when a DefaultFile was given, so a
    /// device name went straight to File.OpenRead, and on Windows "nul" was
    /// opened and answered 200.
    ///
    /// The content type was decided once, at registration, from the
    /// builder's answer to empty parameters. For the usual builder that is a
    /// directory, without an extension, so every file was
    /// application/octet-stream.
    ///
    /// And the doc comment, corrected only weeks ago, said the parameters
    /// arrive "not URL-decoded". They arrive decoded, which one test here pins
    /// so that the comment cannot drift from the behaviour again.
    ///
    /// What it still does not do is keep the builder's result inside a root.
    /// That is the builder's job, and the doc comment says so.
    /// </remarks>
    [TestFixture]
    public class RegisterFileSystemFileTests
    {

        #region Data

        private String       dir     = "";
        private HTTPServer?  server;

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {

            dir     = Path.Combine(Path.GetTempPath(), "hermod-h34-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path.Combine(dir, "sub"));

            File.WriteAllText(Path.Combine(dir, "a.txt"),        "file a");
            File.WriteAllText(Path.Combine(dir, "a b.txt"),      "file a b");
            File.WriteAllText(Path.Combine(dir, "page.html"),    "<p>page</p>");
            File.WriteAllText(Path.Combine(dir, "default.txt"),  "the default");

            server  = new HTTPServer(
                          IPv4Address.Localhost,
                          IPPort.Parse(0),
                          AutoStart: true
                      );

            var httpAPI = server.AddHTTPAPI();

            httpAPI.RegisterFileSystemFile(HTTPHostname.Any, HTTPPath.Root + "f"     + "{file}", parameters => Path.Combine(dir, parameters[0]));
            httpAPI.RegisterFileSystemFile(HTTPHostname.Any, HTTPPath.Root + "d"     + "{file}", parameters => Path.Combine(dir, parameters[0]), DefaultFile: Path.Combine(dir, "default.txt"));
            httpAPI.RegisterFileSystemFile(HTTPHostname.Any, HTTPPath.Root + "gone"  + "{file}", parameters => Path.Combine(dir, parameters[0]), DefaultFile: Path.Combine(dir, "no-default.txt"));
            httpAPI.RegisterFileSystemFile(HTTPHostname.Any, HTTPPath.Root + "typed" + "{file}", parameters => Path.Combine(dir, parameters[0]), ResponseContentType: HTTPContentType.Text.PLAIN);

        }

        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
                await server.DisposeAsync();

            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            { }

        }

        #endregion

        #region (private) Get(Target)

        /// <summary>
        /// A raw GET, so that the request-target arrives exactly as written.
        /// </summary>
        private async Task<(Int32 Status, String ContentType, String Body)> Get(String Target)
        {

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, server!.TCPPort.ToInt32());

            await using var stream = tcpClient.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET {Target} HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"));

            using var timeout  = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var buffer   = new MemoryStream();
            await stream.CopyToAsync(buffer, timeout.Token);

            var response     = Encoding.UTF8.GetString(buffer.ToArray());
            var headerEnd    = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            var header       = headerEnd < 0 ? response : response[..headerEnd];
            var contentType  = header.Split("\r\n").
                                      FirstOrDefault(line => line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))?.
                                      Split(':', 2)[1].Trim() ?? "";

            return (Int32.Parse(response.Split(' ')[1]),
                    contentType,
                    headerEnd < 0 ? "" : response[(headerEnd + 4)..]);

        }

        #endregion


        #region AFileIsServedWithTheTypeOfItsOwnExtension(Target, ContentType, Body)

        /// <summary>
        /// Both were application/octet-stream: the type of the directory the
        /// builder returns for empty parameters, decided at registration.
        /// </summary>
        [TestCase("/f/page.html",  "text/html",   "<p>page</p>")]
        [TestCase("/f/a.txt",      "text/plain",  "file a")]
        public async Task AFileIsServedWithTheTypeOfItsOwnExtension(String Target, String ContentType, String Body)
        {

            var (status, contentType, body) = await Get(Target);

            Assert.Multiple(() => {
                Assert.That(status,       Is.EqualTo(200));
                Assert.That(contentType,  Does.StartWith(ContentType));
                Assert.That(body,         Is.EqualTo(Body));
            });

        }

        #endregion

        #region AGivenContentTypeIsKept()

        /// <summary>
        /// Green before as well: a caller that names the type gets it, for
        /// whatever file the builder chose.
        /// </summary>
        [Test]
        public async Task AGivenContentTypeIsKept()
        {

            var (status, contentType, body) = await Get("/typed/page.html");

            Assert.Multiple(() => {
                Assert.That(status,       Is.EqualTo(200));
                Assert.That(contentType,  Does.StartWith("text/plain"));
                Assert.That(body,         Is.EqualTo("<p>page</p>"));
            });

        }

        #endregion

        #region ADirectoryIsA404ThatNamesNoPath()

        /// <summary>
        /// Was a 500 with the body "Access to the path 'C:\...\sub' is denied."
        /// </summary>
        [Test]
        public async Task ADirectoryIsA404ThatNamesNoPath()
        {

            var (status, _, body) = await Get("/f/sub");

            Assert.Multiple(() => {
                Assert.That(status,  Is.EqualTo(404));
                Assert.That(body,    Does.Not.Contain(Path.GetFileName(dir)), "not even the name of the directory");
            });

        }

        #endregion

        #region ADeviceNameIsA404(Name)

        /// <summary>
        /// "nul" was opened as the NUL device and answered 200 with an empty
        /// body, and was the only one of the three that was red before:
        /// "NUL.txt", a device in older Windows, and "con" were already 404s.
        /// Elsewhere all three are ordinary names of files that do not exist,
        /// and the test pins that they stay a 404 there too.
        /// </summary>
        [TestCase("nul")]
        [TestCase("NUL.txt")]
        [TestCase("con")]
        public async Task ADeviceNameIsA404(String Name)
        {

            var (status, _, _) = await Get("/f/" + Name);

            Assert.That(status, Is.EqualTo(404));

        }

        #endregion

        #region AMissingFileIsA404()

        [Test]
        public async Task AMissingFileIsA404()
        {

            var (status, _, body) = await Get("/f/missing.txt");

            Assert.Multiple(() => {
                Assert.That(status,  Is.EqualTo(404));
                Assert.That(body,    Is.Empty);
            });

        }

        #endregion

        #region TheDefaultFileStandsInForAMissingOne()

        /// <summary>
        /// Red before for its type alone: the default file came back as
        /// application/octet-stream like every other.
        /// </summary>
        [Test]
        public async Task TheDefaultFileStandsInForAMissingOne()
        {

            var (status, contentType, body) = await Get("/d/missing.txt");

            Assert.Multiple(() => {
                Assert.That(status,       Is.EqualTo(200));
                Assert.That(contentType,  Does.StartWith("text/plain"));
                Assert.That(body,         Is.EqualTo("the default"));
            });

        }

        #endregion

        #region AMissingDefaultFileIsA404()

        [Test]
        public async Task AMissingDefaultFileIsA404()
        {

            var (status, _, body) = await Get("/gone/missing.txt");

            Assert.Multiple(() => {
                Assert.That(status,  Is.EqualTo(404));
                Assert.That(body,    Does.Not.Contain(Path.GetFileName(dir)));
            });

        }

        #endregion

        #region TheBuilderReceivesTheParametersURLDecoded()

        /// <summary>
        /// The doc comment said "not URL-decoded" from 2026-10-04 until this
        /// test. "a%20b.txt" reaches the builder as "a b.txt".
        /// </summary>
        [Test]
        public async Task TheBuilderReceivesTheParametersURLDecoded()
        {

            var (status, _, body) = await Get("/f/a%20b.txt");

            Assert.Multiple(() => {
                Assert.That(status,  Is.EqualTo(200));
                Assert.That(body,    Is.EqualTo("file a b"));
            });

        }

        #endregion

        #region AFileThatCannotBeOpenedIsA500ThatNamesNoPath()

        /// <summary>
        /// File.Exists says yes and File.OpenRead throws: a file another
        /// process holds without sharing. The message of that exception is the
        /// absolute path, and it was the body of the 500. Windows only, because
        /// that is where FileShare.None keeps a reader out.
        /// </summary>
        [Test]
        [Platform("Win")]
        public async Task AFileThatCannotBeOpenedIsA500ThatNamesNoPath()
        {

            using var locked = new FileStream(Path.Combine(dir, "a.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var (status, _, body) = await Get("/f/a.txt");

            Assert.Multiple(() => {
                Assert.That(status,  Is.EqualTo(500));
                Assert.That(body,    Does.Not.Contain(Path.GetFileName(dir)));
            });

        }

        #endregion

    }

}
