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
    /// MapFileSystemFolder, over the wire. H-33 in HTTP1ConformanceTests.
    /// </summary>
    /// <remarks>
    /// Until this fixture nothing in this suite had ever called it. The guard
    /// against leaving the root was Path.Combine followed by
    /// GetFullPath(...).StartsWith(ResourcePath), and three things were wrong
    /// with it, all three found by sending requests rather than by reading:
    ///
    /// Path.Combine discards its first argument when the second is rooted, and
    /// on Windows "C:secret.txt" is rooted - drive-relative, resolved against
    /// the current directory of drive C:. The request parser lets a colon
    /// through. StartsWith without a trailing separator then accepted
    /// "...\site-secrets\secret.txt" as being inside "...\site". Together: a
    /// file outside the root, served with 200, whenever the current directory
    /// shares the root's prefix.
    ///
    /// A missing file, a directory or a device name was a 500 whose body was
    /// the exception message - the absolute path on the server, for any typo
    /// in a URL.
    ///
    /// A relative ResourcePath was compared as written against absolute paths
    /// and refused every file.
    ///
    /// What the guard did not have to stop is "../": the request parser
    /// refuses dot-segments, backslashes and encoded separators before routing,
    /// which the last test pins, because everything above relies on it.
    /// </remarks>
    [TestFixture]
    public class MapFileSystemFolderTests
    {

        #region Data

        private String       root     = "";
        private String       site     = "";
        private String       sibling  = "";
        private HTTPServer?  server;

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {

            root     = Path.Combine(Path.GetTempPath(), "hermod-h33-" + Guid.NewGuid().ToString("N"));
            site     = Path.Combine(root, "site");
            sibling  = Path.Combine(root, "site-secrets");

            Directory.CreateDirectory(Path.Combine(site, "sub"));
            Directory.CreateDirectory(sibling);

            File.WriteAllText(Path.Combine(site,    "sub", "a.txt"),  "inside");
            File.WriteAllText(Path.Combine(sibling, "secret.txt"),    "sibling secret");
            File.WriteAllText(Path.Combine(root,    "outside.txt"),   "parent secret");

            server   = new HTTPServer(
                           IPv4Address.Localhost,
                           IPPort.Parse(0),
                           AutoStart: true
                       );

            var httpAPI = server.AddHTTPAPI();

            httpAPI.MapFileSystemFolder(HTTPHostname.Any, HTTPPath.Root + "files",    site);
            httpAPI.MapFileSystemFolder(HTTPHostname.Any, HTTPPath.Root + "relative", Path.GetRelativePath(Directory.GetCurrentDirectory(), site));

        }

        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
                await server.DisposeAsync();

            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            { }

        }

        #endregion

        #region (private) Get(Target)

        /// <summary>
        /// A raw GET, so that the request-target arrives exactly as written:
        /// a client would normalise or refuse half of what is sent here.
        /// </summary>
        private async Task<(Int32 Status, String Body)> Get(String Target)
        {

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, server!.TCPPort.ToInt32());

            await using var stream = tcpClient.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET {Target} HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"));

            using var timeout  = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var buffer   = new MemoryStream();
            await stream.CopyToAsync(buffer, timeout.Token);

            var response   = Encoding.UTF8.GetString(buffer.ToArray());
            var headerEnd  = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);

            return (Int32.Parse(response.Split(' ')[1]),
                    headerEnd < 0 ? "" : response[(headerEnd + 4)..]);

        }

        #endregion


        #region AFileBelowTheRootIsServed()

        [Test]
        public async Task AFileBelowTheRootIsServed()
        {

            var (status, body) = await Get("/files/sub/a.txt");

            Assert.Multiple(() => {
                Assert.That(status, Is.EqualTo(200));
                Assert.That(body,   Is.EqualTo("inside"));
            });

        }

        #endregion

        #region AMissingFileIsA404ThatNamesNoPath()

        /// <summary>
        /// Was a 500 with the body {"message":"Could not find file 'C:\...\site\missing.txt'."}.
        /// </summary>
        [Test]
        public async Task AMissingFileIsA404ThatNamesNoPath()
        {

            var (status, body) = await Get("/files/missing.txt");

            Assert.Multiple(() => {
                Assert.That(status, Is.EqualTo(404));
                Assert.That(body,   Does.Not.Contain(Path.GetFileName(root)), "not even the name of the directory");
            });

        }

        #endregion

        #region ADirectoryIsA404ThatNamesNoPath()

        /// <summary>
        /// Was a 500 with the body {"message":"Access to the path '...\site\sub' is denied."}.
        /// </summary>
        [Test]
        public async Task ADirectoryIsA404ThatNamesNoPath()
        {

            var (status, body) = await Get("/files/sub");

            Assert.Multiple(() => {
                Assert.That(status, Is.EqualTo(404));
                Assert.That(body,   Does.Not.Contain(Path.GetFileName(root)));
            });

        }

        #endregion

        #region ADriveRelativePathDoesNotLeaveTheRoot()

        /// <summary>
        /// Was 200 "sibling secret" for both spellings. The current directory
        /// is process-wide, hence NonParallelizable, and put back afterwards.
        /// </summary>
        [Test, NonParallelizable]
        public async Task ADriveRelativePathDoesNotLeaveTheRoot()
        {

            if (!OperatingSystem.IsWindows())
                Assert.Ignore("Drive-relative paths exist only on Windows; elsewhere \"C:secret.txt\" is an ordinary file name.");

            var drive             = site[0];
            var currentDirectory  = Directory.GetCurrentDirectory();

            try
            {

                Directory.SetCurrentDirectory(sibling);

                var plain    = await Get($"/files/{drive}:secret.txt");
                var encoded  = await Get($"/files/{drive}%3Asecret.txt");
                var bare     = await Get($"/files/{drive}:");

                Assert.Multiple(() => {

                    Assert.That(plain.Status,    Is.EqualTo(404));
                    Assert.That(plain.Body,      Does.Not.Contain("sibling secret"));

                    Assert.That(encoded.Status,  Is.EqualTo(404));
                    Assert.That(encoded.Body,    Does.Not.Contain("sibling secret"));

                    // "C:" alone named the current directory itself, and the
                    // 500 it caused printed it.
                    Assert.That(bare.Status,     Is.EqualTo(404));
                    Assert.That(bare.Body,       Does.Not.Contain(Path.GetFileName(root)));

                });

            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }

        }

        #endregion

        #region AnAlternateDataStreamIsNotAnotherNameForTheFile()

        /// <summary>
        /// Was 200 "inside": "a.txt::$DATA" is the default stream of a.txt on
        /// NTFS. Inside the root, but under a name whose extension is not
        /// ".txt" - and the extension is what both the content type and the
        /// HTMLTemplateHandler decide by.
        /// </summary>
        [Test]
        public async Task AnAlternateDataStreamIsNotAnotherNameForTheFile()
        {

            if (!OperatingSystem.IsWindows())
                Assert.Ignore("Alternate data streams are an NTFS feature.");

            var (status, body) = await Get("/files/sub/a.txt::$DATA");

            Assert.Multiple(() => {
                Assert.That(status, Is.EqualTo(404));
                Assert.That(body,   Does.Not.Contain("inside"));
            });

        }

        #endregion

        #region ARelativeRootServesItsFiles()

        /// <summary>
        /// Was 404: the relative root was compared as written against the
        /// absolute path of every file, and no absolute path starts with "..".
        /// </summary>
        [Test]
        public async Task ARelativeRootServesItsFiles()
        {

            var (status, body) = await Get("/relative/sub/a.txt");

            Assert.Multiple(() => {
                Assert.That(status, Is.EqualTo(200));
                Assert.That(body,   Is.EqualTo("inside"));
            });

        }

        #endregion

        #region DotSegmentsNeverReachTheHandler(Target)

        /// <summary>
        /// Green before the fix as well: this pins the precondition the rest
        /// relies on, not the fix. It goes red if the request parser ever
        /// starts passing these on to routing.
        /// </summary>
        [TestCase("/files/../outside.txt")]
        [TestCase("/files/../site-secrets/secret.txt")]
        [TestCase("/files/sub/../../outside.txt")]
        [TestCase("/files/%2e%2e/outside.txt")]
        [TestCase("/files/..%2foutside.txt")]
        [TestCase("/files/..%5coutside.txt")]
        [TestCase("/files/%252e%252e%252foutside.txt")]
        public async Task DotSegmentsNeverReachTheHandler(String Target)
        {

            var (status, body) = await Get(Target);

            Assert.Multiple(() => {
                Assert.That(status, Is.EqualTo(400));
                Assert.That(body,   Does.Not.Contain("secret"));
            });

        }

        #endregion

    }

}
