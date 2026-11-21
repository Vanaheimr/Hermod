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

using System.Globalization;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Validators, conditional requests and ranges for static files: the
    /// handlers in HTTPStandardHandlers and SinglePageAppHandler, since H-43
    /// answered by HTTPResourceSemantics.
    /// </summary>
    /// <remarks>
    /// Until 2026-11-21 MapFileSystemFolder, MapResourceAssemblyFolder,
    /// RegisterFileSystemFile and RegisterResourcesFile sent a 200 with neither
    /// ETag nor Last-Modified, so no request for a static file could be answered
    /// 304 or 206. RFC 9110, Sections 8.8.2.1 and 8.8.3: an origin server SHOULD
    /// send both where it can determine them. SinglePageAppHandler had an ETag
    /// and a 304 for If-None-Match, and nothing else.
    ///
    /// Raw sockets, as in HTTPResourceSemanticsTests: which fields go over the
    /// wire is what is under test. NonParallelizable, because one test lowers
    /// HTTPStandardHandlers.MaxBufferedFileSize, which is process-wide.
    /// </remarks>
    [TestFixture, NonParallelizable]
    public class StaticFileValidatorsTests
    {

        #region Data

        private static readonly DateTimeOffset  Written         = new (2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        private const           String          WrittenText     = "Wed, 01 Jul 2026 12:00:00 GMT";
        private const           String          Text            = "Hello from a static file on disk.";
        private const           String          ResourcePrefix  = "org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.HTTPRoot.";

        private String       root    = "";
        private HTTPServer?  server;

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public async Task SetUp()
        {

            root = Path.Combine(Path.GetTempPath(), "hermod-h43-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path.Combine(root, "site"));
            Directory.CreateDirectory(Path.Combine(root, "spa", "assets"));

            Write(Path.Combine(root, "site", "a.txt"),           Text);
            Write(Path.Combine(root, "site", "page.html"),       "<p>{{name}}</p>");
            Write(Path.Combine(root, "single.txt"),              Text);
            Write(Path.Combine(root, "spa",  "index.html"),      "<!doctype html><p>app</p>");
            Write(Path.Combine(root, "spa",  "assets", "a.js"),  "console.log('a static asset');");

            server = await HTTPServer.StartNew();

            var api = server.AddHTTPAPI();

            api.MapFileSystemFolder      (HTTPHostname.Any, HTTPPath.Root + "files", Path.Combine(root, "site"));
            api.MapFileSystemFolder      (HTTPHostname.Any, HTTPPath.Root + "tpl",   Path.Combine(root, "site"),
                                          HTMLTemplateHandler: (file, html) => html.Replace("{{name}}", "templated"));
            api.MapResourceAssemblyFolder(HTTPHostname.Any, HTTPPath.Root + "res",   ResourcePrefix, typeof(StaticFileValidatorsTests).Assembly);
            api.RegisterFileSystemFile   (HTTPHostname.Any, HTTPPath.Root + "single", parameters => Path.Combine(root, "single.txt"));
            api.RegisterResourcesFile    (HTTPHostname.Any, HTTPPath.Root + "hello",  typeof(StaticFileValidatorsTests).Assembly, ResourcePrefix + "helloWorld.txt");

            server.AddHTTPAPI(HTTPPath.Root + "spa").
                   MapSinglePageApplication(new FileSystemContentSource(Path.Combine(root, "spa")));

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

        #region (private) Write(Path, Text, Modified = null)

        private static void Write(String           FilePath,
                                  String           Content,
                                  DateTimeOffset?  Modified   = null)
        {
            File.WriteAllText(FilePath, Content);
            File.SetLastWriteTimeUtc(FilePath, (Modified ?? Written).UtcDateTime);
        }

        #endregion

        #region (private) Exchange(Method, Target, Fields)

        private async Task<HTTPRawSocketResponse> Exchange(String           Method,
                                                           String           Target,
                                                           params String[]  Fields)
        {

            await using var client = await HTTPRawSocketClient.ConnectAsync(
                                               System.Net.IPAddress.Loopback,
                                               server!.TCPPort
                                           );

            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await client.SendAsync(
                $"{Method} {Target} HTTP/1.1\r\nHost: localhost\r\n{String.Concat(Fields.Select(field => field + "\r\n"))}\r\n",
                giveUp.Token
            );

            return await client.ReadResponseAsync(Method == "HEAD", giveUp.Token);

        }

        private Task<HTTPRawSocketResponse> Get(String Target, params String[] Fields)
            => Exchange("GET", Target, Fields);

        private static String? Field(HTTPRawSocketResponse Response, String Name)

            => Response.Headers.TryGetValue(Name, out var values)
                   ? String.Join(", ", values)
                   : null;

        private static String BodyOf(HTTPRawSocketResponse Response)
            => Encoding.UTF8.GetString(Response.Body);

        #endregion


        #region AFileOnDiskCarriesItsValidators(Target)

        /// <summary>
        /// MapFileSystemFolder and RegisterFileSystemFile: Last-Modified is the
        /// time of the last write, and the entity tag is made of that time and the
        /// length, so that no request hashes the file.
        /// </summary>
        [TestCase("/files/a.txt")]
        [TestCase("/single")]
        public async Task AFileOnDiskCarriesItsValidators(String Target)
        {

            var response = await Get(Target);

            Assert.That(response.StatusCode,                 Is.EqualTo(200));
            Assert.That(BodyOf(response),                    Is.EqualTo(Text));
            Assert.That(Field(response, "Last-Modified"),    Is.EqualTo(WrittenText));
            Assert.That(Field(response, "ETag"),             Is.EqualTo($"\"{Written.UtcTicks:x}-{Text.Length:x}\""));
            Assert.That(Field(response, "Accept-Ranges"),    Is.EqualTo("bytes"));

        }

        #endregion

        #region AFileOnDiskIsRevalidated(Target)

        /// <summary>
        /// A 304 for the entity tag and one for the date, each with the
        /// Cache-Control a 200 would have had (RFC 9110, Section 15.4.5).
        /// </summary>
        [TestCase("/files/a.txt", "public, max-age=300")]
        [TestCase("/single",      "no-cache")]
        public async Task AFileOnDiskIsRevalidated(String Target, String CacheControl)
        {

            var etag          = Field(await Get(Target), "ETag")!;

            var byTag         = await Get(Target, $"If-None-Match: {etag}");
            var byDate        = await Get(Target, $"If-Modified-Since: {WrittenText}");
            var byEarlierDate = await Get(Target, "If-Modified-Since: Tue, 30 Jun 2026 12:00:00 GMT");

            Assert.That(byTag.StatusCode,                    Is.EqualTo(304));
            Assert.That(Field(byTag, "ETag"),                Is.EqualTo(etag));
            Assert.That(Field(byTag, "Cache-Control"),       Is.EqualTo(CacheControl));
            Assert.That(byDate.StatusCode,                   Is.EqualTo(304));
            Assert.That(byEarlierDate.StatusCode,            Is.EqualTo(200));

        }

        #endregion

        #region AFailedPreconditionIs412()

        [Test]
        public async Task AFailedPreconditionIs412()
        {

            var wrongTag   = await Get("/files/a.txt", "If-Match: \"not-this-one\"");
            var anyTag     = await Get("/files/a.txt", "If-Match: *");
            var unchanged  = await Get("/files/a.txt", "If-Unmodified-Since: Tue, 30 Jun 2026 12:00:00 GMT");

            Assert.That(wrongTag.StatusCode,   Is.EqualTo(412));
            Assert.That(anyTag.StatusCode,     Is.EqualTo(200));
            Assert.That(unchanged.StatusCode,  Is.EqualTo(412));

        }

        #endregion

        #region ARangeOfAFileIs206()

        [Test]
        public async Task ARangeOfAFileIs206()
        {

            var etag        = Field(await Get("/files/a.txt"), "ETag")!;

            var partial     = await Get("/files/a.txt", "Range: bytes=0-4");
            var resumed     = await Get("/files/a.txt", "Range: bytes=6-9",   $"If-Range: {etag}");
            var stale       = await Get("/files/a.txt", "Range: bytes=6-9",   "If-Range: \"an-older-one\"");
            var beyond      = await Get("/files/a.txt", "Range: bytes=1000-");

            Assert.That(partial.StatusCode,                    Is.EqualTo(206));
            Assert.That(BodyOf(partial),                       Is.EqualTo("Hello"));
            Assert.That(Field(partial, "Content-Range"),       Is.EqualTo($"bytes 0-4/{Text.Length}"));
            Assert.That(resumed.StatusCode,                    Is.EqualTo(206));
            Assert.That(BodyOf(resumed),                       Is.EqualTo("from"));
            Assert.That(stale.StatusCode,                      Is.EqualTo(200));
            Assert.That(BodyOf(stale),                         Is.EqualTo(Text));
            Assert.That(beyond.StatusCode,                     Is.EqualTo(416));

        }

        #endregion

        #region AChangedFileHasANewEntityTag()

        /// <summary>
        /// A write of the same length one second later: the time is part of the
        /// tag, so the old tag no longer matches.
        /// </summary>
        [Test]
        public async Task AChangedFileHasANewEntityTag()
        {

            var before  = Field(await Get("/files/a.txt"), "ETag")!;

            Write(Path.Combine(root, "site", "a.txt"), Text.ToUpperInvariant(), Written.AddSeconds(1));

            var after   = await Get("/files/a.txt", $"If-None-Match: {before}");

            Assert.That(after.StatusCode,          Is.EqualTo(200));
            Assert.That(BodyOf(after),             Is.EqualTo(Text.ToUpperInvariant()));
            Assert.That(Field(after, "ETag"),      Is.Not.EqualTo(before));

        }

        #endregion

        #region AFileFromTheFutureIsNotDatedThere()

        /// <summary>
        /// RFC 9110, Section 8.8.2.1: a Last-Modified later than the message's
        /// Date is sent as that Date.
        /// </summary>
        [Test]
        public async Task AFileFromTheFutureIsNotDatedThere()
        {

            Write(Path.Combine(root, "site", "a.txt"), Text, DateTimeOffset.UtcNow.AddYears(1));

            var response      = await Get("/files/a.txt");
            var lastModified  = DateTimeOffset.ParseExact(Field(response, "Last-Modified")!, "r", CultureInfo.InvariantCulture);
            var date          = DateTimeOffset.ParseExact(Field(response, "Date")!,          "r", CultureInfo.InvariantCulture);

            Assert.That(lastModified, Is.LessThanOrEqualTo(date));

        }

        #endregion

        #region AHEADCarriesTheValidatorsAndNoBody()

        [Test]
        public async Task AHEADCarriesTheValidatorsAndNoBody()
        {

            var response = await Exchange("HEAD", "/files/a.txt");

            Assert.That(response.StatusCode,                 Is.EqualTo(200));
            Assert.That(Field(response, "Content-Length"),   Is.EqualTo(Text.Length.ToString()));
            Assert.That(Field(response, "ETag"),             Is.Not.Null);
            Assert.That(Field(response, "Last-Modified"),    Is.EqualTo(WrittenText));

        }

        #endregion

        #region ATemplatedPageHasTheTagOfItsContent()

        /// <summary>
        /// A page through an HTMLTemplateHandler is what the template made of it,
        /// which neither the file's time nor its length describes.
        /// </summary>
        [Test]
        public async Task ATemplatedPageHasTheTagOfItsContent()
        {

            var response = await Get("/tpl/page.html");

            Assert.That(BodyOf(response),                    Is.EqualTo("<p>templated</p>"));
            Assert.That(Field(response, "ETag"),             Is.EqualTo(HTTPValidators.ComputeETag(Encoding.UTF8.GetBytes("<p>templated</p>"))));
            Assert.That(Field(response, "Last-Modified"),    Is.Null);

        }

        #endregion

        #region AnEmbeddedResourceHasTheTagOfItsContent(Target)

        /// <summary>
        /// MapResourceAssemblyFolder and RegisterResourcesFile: an embedded
        /// resource has no time of its own, so an entity tag and no Last-Modified.
        /// </summary>
        [TestCase("/res/helloWorld.txt")]
        [TestCase("/hello")]
        public async Task AnEmbeddedResourceHasTheTagOfItsContent(String Target)
        {

            var response     = await Get(Target);
            var etag         = Field(response, "ETag");
            var revalidated  = await Get(Target, $"If-None-Match: {etag}");

            Assert.That(response.StatusCode,                 Is.EqualTo(200));
            Assert.That(etag,                                Is.EqualTo(HTTPValidators.ComputeETag(response.Body)));
            Assert.That(Field(response, "Last-Modified"),    Is.Null);
            Assert.That(revalidated.StatusCode,              Is.EqualTo(304));

        }

        #endregion

        #region AFileTooLargeToHoldIsStreamedWithItsValidators()

        /// <summary>
        /// Above MaxBufferedFileSize the file is streamed as before. It keeps its
        /// validators and the 304 and 412 of its preconditions, and gives up its
        /// ranges, which need the representation in hand: a Range is answered with
        /// the whole file, as Section 14.2 permits, and no Accept-Ranges says
        /// otherwise.
        /// </summary>
        [Test]
        public async Task AFileTooLargeToHoldIsStreamedWithItsValidators()
        {

            var before = HTTPStandardHandlers.MaxBufferedFileSize;

            try
            {

                HTTPStandardHandlers.MaxBufferedFileSize = 8;

                var response   = await Get("/files/a.txt");
                var etag       = Field(response, "ETag")!;
                var byTag      = await Get("/files/a.txt", $"If-None-Match: {etag}");
                var byDate     = await Get("/files/a.txt", $"If-Modified-Since: {WrittenText}");
                var wrongTag   = await Get("/files/a.txt", "If-Match: \"not-this-one\"");
                var range      = await Get("/files/a.txt", "Range: bytes=0-4");

                Assert.That(response.StatusCode,                 Is.EqualTo(200));
                Assert.That(BodyOf(response),                    Is.EqualTo(Text));
                Assert.That(etag,                                Is.EqualTo($"\"{Written.UtcTicks:x}-{Text.Length:x}\""));
                Assert.That(Field(response, "Last-Modified"),    Is.EqualTo(WrittenText));
                Assert.That(Field(response, "Accept-Ranges"),    Is.Null);
                Assert.That(byTag.StatusCode,                    Is.EqualTo(304));
                Assert.That(Field(byTag, "Cache-Control"),       Is.EqualTo("public, max-age=300"));
                Assert.That(byDate.StatusCode,                   Is.EqualTo(304));
                Assert.That(wrongTag.StatusCode,                 Is.EqualTo(412));
                Assert.That(range.StatusCode,                    Is.EqualTo(200));
                Assert.That(BodyOf(range),                       Is.EqualTo(Text));

            }
            finally
            {
                HTTPStandardHandlers.MaxBufferedFileSize = before;
            }

        }

        #endregion

        #region AnEmbeddedResourceTooLargeToHoldIsHashedAsItStreams()

        [Test]
        public async Task AnEmbeddedResourceTooLargeToHoldIsHashedAsItStreams()
        {

            var before = HTTPStandardHandlers.MaxBufferedFileSize;

            try
            {

                HTTPStandardHandlers.MaxBufferedFileSize = 1;

                var response     = await Get("/res/helloWorld.txt");
                var revalidated  = await Get("/res/helloWorld.txt", $"If-None-Match: {Field(response, "ETag")}");

                Assert.That(response.StatusCode,                 Is.EqualTo(200));
                Assert.That(Field(response, "ETag"),             Is.EqualTo(HTTPValidators.ComputeETag(response.Body)));
                Assert.That(revalidated.StatusCode,              Is.EqualTo(304));

            }
            finally
            {
                HTTPStandardHandlers.MaxBufferedFileSize = before;
            }

        }

        #endregion


        #region ASinglePageAppAssetIsDatedRangedAndGuarded()

        /// <summary>
        /// SinglePageAppHandler from a FileSystemContentSource: Last-Modified, the
        /// 304 for If-Modified-Since, the 412 for If-Match and a 206, none of which
        /// it had before.
        /// </summary>
        [Test]
        public async Task ASinglePageAppAssetIsDatedRangedAndGuarded()
        {

            var response   = await Get("/spa/assets/a.js");
            var byDate     = await Get("/spa/assets/a.js", $"If-Modified-Since: {WrittenText}");
            var byTag      = await Get("/spa/assets/a.js", $"If-None-Match: {Field(response, "ETag")}");
            var wrongTag   = await Get("/spa/assets/a.js", "If-Match: \"not-this-one\"");
            var partial    = await Get("/spa/assets/a.js", "Range: bytes=0-6");

            Assert.That(response.StatusCode,                   Is.EqualTo(200));
            Assert.That(Field(response, "Last-Modified"),      Is.EqualTo(WrittenText));
            Assert.That(Field(response, "X-Content-Type-Options"), Is.EqualTo("nosniff"));
            Assert.That(byDate.StatusCode,                     Is.EqualTo(304));
            Assert.That(Field(byDate, "Cache-Control"),        Is.EqualTo(Field(response, "Cache-Control")));
            Assert.That(byTag.StatusCode,                      Is.EqualTo(304));
            Assert.That(wrongTag.StatusCode,                   Is.EqualTo(412));
            Assert.That(partial.StatusCode,                    Is.EqualTo(206));
            Assert.That(BodyOf(partial),                       Is.EqualTo("console"));

        }

        #endregion

        #region ASinglePageAppKeepsItsCodingPerRepresentation()

        /// <summary>
        /// The coding is still chosen by the handler, from its cache, and the
        /// entity tag names it: a gzip representation is not the identity one.
        /// </summary>
        [Test]
        public async Task ASinglePageAppKeepsItsCodingPerRepresentation()
        {

            Write(Path.Combine(root, "spa", "assets", "big.js"), String.Concat(Enumerable.Repeat("console.log('compressible');\n", 200)));

            var identity  = await Get("/spa/assets/big.js");
            var gzip      = await Get("/spa/assets/big.js", "Accept-Encoding: gzip");
            var again     = await Get("/spa/assets/big.js", "Accept-Encoding: gzip", $"If-None-Match: {Field(gzip, "ETag")}");

            Assert.That(Field(identity, "Content-Encoding"),   Is.Null);
            Assert.That(Field(gzip, "Content-Encoding"),       Is.EqualTo("gzip"));
            Assert.That(Field(gzip, "ETag"),                   Is.Not.EqualTo(Field(identity, "ETag")));
            Assert.That(Field(gzip, "Vary"),                   Is.EqualTo("Accept-Encoding"));
            Assert.That(again.StatusCode,                      Is.EqualTo(304));
            Assert.That(Field(again, "Vary"),                  Is.EqualTo("Accept-Encoding"));

        }

        #endregion

    }

}
