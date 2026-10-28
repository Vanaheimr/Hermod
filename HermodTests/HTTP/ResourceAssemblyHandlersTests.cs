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
using System.Reflection;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The embedded-resource half of HTTPStandardHandlers, over the wire:
    /// MapResourceAssemblyFolder on HTTPAPI and on HTTPExtAPI,
    /// MapResourceAssembliesFolder on HTTPExtAPI, and RegisterResourcesFile.
    /// H-34 in HTTP1ConformanceTests.
    /// </summary>
    /// <remarks>
    /// None of the four had a caller in this suite, though OCPI, WWCP and the
    /// UsersAPI serve their web front ends through the first. Unlike their
    /// file-system neighbours, nothing here was wrong: every request below
    /// was answered as it is asserted the first time it was sent. What this
    /// fixture adds is that it would now be noticed if that changed.
    ///
    /// One behaviour is pinned that a reader would not guess. A manifest
    /// resource has a flat, dot-separated name, and the handler turns every
    /// "/" of the URL into a "." to find it, so "assets/app.js" and
    /// "assets.app.js" are the same resource. That is the nature of embedded
    /// resources rather than a defect, and no file outside the assembly can
    /// be reached through it.
    ///
    /// The Hostname every one of them takes is not used. An HTTPAPI is bound
    /// to its hostnames when it is created, and AddHandler takes none; the
    /// parameter is left over from the HTTPServer-based API these methods
    /// were written for. The last test pins that, because the doc comments
    /// now say so.
    /// </remarks>
    [TestFixture]
    public class ResourceAssemblyHandlersTests
    {

        #region Data

        private const           String    prefix    = "org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.HTTPRoot.";
        private static readonly Assembly  assembly  = typeof(ResourceAssemblyHandlersTests).Assembly;

        #endregion

        #region (private) Get(Server, Target, Host = "localhost")

        /// <summary>
        /// A raw GET, so that the request-target and the Host arrive exactly
        /// as written.
        /// </summary>
        private static async Task<(Int32 Status, String ContentType, String Body)> Get(HTTPServer  Server,
                                                                                       String      Target,
                                                                                       String      Host   = "localhost")
        {

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, Server.TCPPort.ToInt32());

            await using var stream = tcpClient.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET {Target} HTTP/1.1\r\nHost: {Host}\r\nConnection: close\r\n\r\n"));

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

        #region (private) WithExtAPI(Test)

        private static async Task WithExtAPI(Func<HTTPServer, HTTPExtAPI, Task> Test)
        {

            var loggingPath  = Path.Combine(Path.GetTempPath(), "hermod-h34-" + Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
            var extServer    = await HTTPServer.StartNew();

            try
            {

                await Test(
                          extServer,
                          new HTTPExtAPI(
                              extServer,
                              SkipURLTemplates:      true,
                              DisableNotifications:  true,
                              LoggingPath:           loggingPath
                          )
                      );

            }
            finally
            {

                await extServer.DisposeAsync();

                try
                {
                    Directory.Delete(loggingPath, recursive: true);
                }
                catch
                { }

            }

        }

        #endregion


        #region TheFolderOnHTTPAPIServesItsFiles()

        [Test]
        public async Task TheFolderOnHTTPAPIServesItsFiles()
        {

            await using var server = await HTTPServer.StartNew();

            server.AddHTTPAPI().MapResourceAssemblyFolder(HTTPHostname.Any, HTTPPath.Root + "spa", prefix + "SPA.", assembly);

            var folder       = await Get(server, "/spa");
            var folderSlash  = await Get(server, "/spa/");
            var icon         = await Get(server, "/spa/favicon.svg");
            var nested       = await Get(server, "/spa/assets/app.0123456789abcdef.js");
            var missing      = await Get(server, "/spa/missing.txt");
            var directory    = await Get(server, "/spa/assets");

            Assert.Multiple(() => {

                Assert.That(folder.Status,            Is.EqualTo(200));
                Assert.That(folder.ContentType,       Does.StartWith("text/html"));
                Assert.That(folder.Body,              Does.StartWith("<!doctype html>"));
                Assert.That(folderSlash.Status,       Is.EqualTo(200));
                Assert.That(folderSlash.Body,         Is.EqualTo(folder.Body));

                Assert.That(icon.Status,              Is.EqualTo(200));
                Assert.That(icon.ContentType,         Does.StartWith("image/svg+xml"));
                Assert.That(nested.Status,            Is.EqualTo(200));
                Assert.That(nested.ContentType,       Does.StartWith("text/javascript"));

                Assert.That(missing.Status,           Is.EqualTo(404));
                Assert.That(directory.Status,         Is.EqualTo(404));

            });

        }

        #endregion

        #region ASlashAndADotNameTheSameResource()

        /// <summary>
        /// Pinned rather than fixed: see the remarks on this fixture.
        /// </summary>
        [Test]
        public async Task ASlashAndADotNameTheSameResource()
        {

            await using var server = await HTTPServer.StartNew();

            server.AddHTTPAPI().MapResourceAssemblyFolder(HTTPHostname.Any, HTTPPath.Root + "spa", prefix + "SPA.", assembly);

            var slash  = await Get(server, "/spa/assets/app.0123456789abcdef.js");
            var dot    = await Get(server, "/spa/assets.app.0123456789abcdef.js");

            Assert.Multiple(() => {
                Assert.That(dot.Status,  Is.EqualTo(200));
                Assert.That(dot.Body,    Is.EqualTo(slash.Body));
            });

        }

        #endregion

        #region TheTemplateHandlerSeesHTMLAndNothingElse()

        [Test]
        public async Task TheTemplateHandlerSeesHTMLAndNothingElse()
        {

            await using var server = await HTTPServer.StartNew();

            server.AddHTTPAPI().MapResourceAssemblyFolder(HTTPHostname.Any, HTTPPath.Root + "tpl", prefix + "SPA.", assembly,
                                                          HTMLTemplateHandler: (fileName, text) => "TEMPLATED " + fileName + ":" + text);

            var page  = await Get(server, "/tpl");
            var icon  = await Get(server, "/tpl/favicon.svg");

            Assert.Multiple(() => {
                Assert.That(page.Body,  Does.StartWith("TEMPLATED index.html:<!doctype html>"));
                Assert.That(icon.Body,  Does.StartWith("<svg"));
            });

        }

        #endregion

        #region TheFolderOnHTTPExtAPIAsksForAuthenticationWhenTold()

        [Test]
        public async Task TheFolderOnHTTPExtAPIAsksForAuthenticationWhenTold()

            => await WithExtAPI(async (server, extAPI) => {

                   extAPI.MapResourceAssemblyFolder(HTTPHostname.Any, HTTPPath.Root + "open",   prefix + "SPA.", assembly, RequireAuthentication: false);
                   extAPI.MapResourceAssemblyFolder(HTTPHostname.Any, HTTPPath.Root + "closed", prefix + "SPA.", assembly, RequireAuthentication: true);

                   var open        = await Get(server, "/open");
                   var openIcon    = await Get(server, "/open/favicon.svg");
                   var closed      = await Get(server, "/closed");
                   var closedIcon  = await Get(server, "/closed/favicon.svg");

                   Assert.Multiple(() => {

                       Assert.That(open.Status,        Is.EqualTo(200));
                       Assert.That(open.Body,          Does.StartWith("<!doctype html>"));
                       Assert.That(openIcon.Status,    Is.EqualTo(200));

                       Assert.That(closed.Status,      Is.EqualTo(401));
                       Assert.That(closed.Body,        Does.Not.Contain("<!doctype html>"));
                       Assert.That(closedIcon.Status,  Is.EqualTo(401));
                       Assert.That(closedIcon.Body,    Does.Not.Contain("<svg"));

                   });

               });

        #endregion

        #region SeveralAssembliesOnHTTPExtAPIAreSearchedInOrder()

        /// <summary>
        /// The first prefix has index.html and favicon.svg, only the second has
        /// helloWorld.txt: a name the first cannot answer falls through to the
        /// second, and one neither has is a 404.
        /// </summary>
        [Test]
        public async Task SeveralAssembliesOnHTTPExtAPIAreSearchedInOrder()

            => await WithExtAPI(async (server, extAPI) => {

                   extAPI.MapResourceAssembliesFolder(HTTPHostname.Any,
                                                      HTTPPath.Root + "many",
                                                      [
                                                          new Tuple<String, Assembly>(prefix + "SPA.", assembly),
                                                          new Tuple<String, Assembly>(prefix,          assembly)
                                                      ],
                                                      RequireAuthentication: false);

                   extAPI.MapResourceAssembliesFolder(HTTPHostname.Any,
                                                      HTTPPath.Root + "closed",
                                                      [ new Tuple<String, Assembly>(prefix + "SPA.", assembly) ],
                                                      RequireAuthentication: true);

                   var folder   = await Get(server, "/many");
                   var first    = await Get(server, "/many/favicon.svg");
                   var second   = await Get(server, "/many/helloWorld.txt");
                   var neither  = await Get(server, "/many/nope.txt");
                   var closed   = await Get(server, "/closed");

                   Assert.Multiple(() => {

                       Assert.That(folder.Status,   Is.EqualTo(200));
                       Assert.That(folder.Body,     Does.StartWith("<!doctype html>"));
                       Assert.That(first.Status,    Is.EqualTo(200));
                       Assert.That(first.Body,      Does.StartWith("<svg"));
                       Assert.That(second.Status,   Is.EqualTo(200));
                       Assert.That(second.Body,     Does.StartWith("Hello World!"));
                       Assert.That(neither.Status,  Is.EqualTo(404));

                       Assert.That(closed.Status,   Is.EqualTo(401));

                   });

               });

        #endregion

        #region ASingleResourceFileIsServedWithTheTypeOfItsName()

        [Test]
        public async Task ASingleResourceFileIsServedWithTheTypeOfItsName()
        {

            await using var server = await HTTPServer.StartNew();

            var httpAPI = server.AddHTTPAPI();

            httpAPI.RegisterResourcesFile(HTTPHostname.Any, HTTPPath.Root + "hello", assembly, prefix + "helloWorld.txt");
            httpAPI.RegisterResourcesFile(HTTPHostname.Any, HTTPPath.Root + "icon",  assembly, prefix + "SPA.favicon.svg");
            httpAPI.RegisterResourcesFile(HTTPHostname.Any, HTTPPath.Root + "gone",  assembly, prefix + "missing.txt");

            var hello  = await Get(server, "/hello");
            var icon   = await Get(server, "/icon");
            var gone   = await Get(server, "/gone");

            Assert.Multiple(() => {

                Assert.That(hello.Status,       Is.EqualTo(200));
                Assert.That(hello.ContentType,  Does.StartWith("text/plain"));
                Assert.That(hello.Body,         Does.StartWith("Hello World!"));

                Assert.That(icon.Status,        Is.EqualTo(200));
                Assert.That(icon.ContentType,   Does.StartWith("image/svg+xml"));

                Assert.That(gone.Status,        Is.EqualTo(404));

            });

        }

        #endregion

        #region TheHostnameParameterDoesNotScopeTheRoute()

        /// <summary>
        /// Registered for "a.example" on an API for any host, and served to a
        /// request for "b.example": the hostnames of the HTTPAPI decide, the
        /// parameter does not. Pinned because the doc comments say so now.
        /// </summary>
        [Test]
        public async Task TheHostnameParameterDoesNotScopeTheRoute()
        {

            await using var server = await HTTPServer.StartNew();

            server.AddHTTPAPI().RegisterResourcesFile(HTTPHostname.Parse("a.example"), HTTPPath.Root + "hello", assembly, prefix + "helloWorld.txt");

            var other = await Get(server, "/hello", Host: "b.example");

            Assert.That(other.Status, Is.EqualTo(200));

        }

        #endregion

    }

}
