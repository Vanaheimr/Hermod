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
using System.Security.Cryptography;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// AHTTPClient.DownloadAsync: a body written to its destination as it
    /// arrives, and the rest asked for with Range and If-Range when it breaks off
    /// (RFC 9110, Sections 14 and 13.1.5), which the HTTP/2 client did alone
    /// until H-38.
    ///
    /// The server is a raw listener answering each request from a script,
    /// because the point is a body that stops in the middle, which no
    /// well-behaved server sends on purpose.
    /// </summary>
    [TestFixture]
    public class HTTPClientDownloadTests
    {

        #region Data

        private static readonly Byte[] Body    = [.. Enumerable.Range(0, 1000).Select(i => (Byte) (i % 251))];
        private static readonly Byte[] Changed = [.. Enumerable.Range(0,  800).Select(i => (Byte) (i % 13))];

        #endregion

        #region (private) ScriptedServer

        /// <summary>
        /// Answers the n-th request with what the script says: the octets to
        /// write, and whether to close the connection after them, which with a
        /// body shorter than its framing promised is a body that breaks off.
        /// </summary>
        private sealed class ScriptedServer : IDisposable
        {

            private readonly TcpListener                                     listener  = new (System.Net.IPAddress.Loopback, 0);
            private readonly CancellationTokenSource                         cts       = new ();
            private readonly Func<Int32, String, (Byte[] Answer, Boolean Close)>  script;

            public List<String> Requests { get; } = [];

            public URL URL
                => URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}");

            public ScriptedServer(Func<Int32, String, (Byte[] Answer, Boolean Close)> Script)
            {
                script = Script;
                listener.Start();
                _ = Task.Run(Serve);
            }

            private async Task Serve()
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {

                        using var tcp    = await listener.AcceptTcpClientAsync(cts.Token);
                        var       stream = tcp.GetStream();

                        while (true)
                        {

                            var head = new StringBuilder();
                            var one  = new Byte[1];

                            while (!head.ToString().EndsWith("\r\n\r\n") &&
                                   await stream.ReadAsync(one, cts.Token) == 1)
                            {
                                head.Append((Char) one[0]);
                            }

                            if (!head.ToString().EndsWith("\r\n\r\n"))
                                break;

                            Int32 index;
                            lock (Requests)
                            {
                                index = Requests.Count;
                                Requests.Add(head.ToString());
                            }

                            var (answer, close) = script(index, head.ToString());

                            await stream.WriteAsync(answer, cts.Token);
                            await stream.FlushAsync(cts.Token);

                            if (close)
                                break;

                        }

                    }
                }
                catch (OperationCanceledException)
                { }
                catch (IOException)
                { }
                catch (SocketException)
                { }
            }

            public void Dispose()
            {
                cts.Cancel();
                listener.Stop();
            }

        }

        private static Byte[] Answer(String Head, Byte[] Body, Int32 Start = 0, Int32? Count = null)

            => [.. Encoding.ASCII.GetBytes(Head + "\r\n"), .. Body.AsSpan(Start, Count ?? Body.Length - Start).ToArray()];

        private static String Header(String Request, String Name)

            => Request.Split("\r\n").
                       FirstOrDefault(line => line.StartsWith(Name + ":", StringComparison.OrdinalIgnoreCase))?[(Name.Length + 1)..].Trim()
               ?? "-";

        private static String ReprDigest(Byte[] Octets)

            => $"sha-256=:{Convert.ToBase64String(SHA256.HashData(Octets))}:";

        #endregion


        #region An uninterrupted download is one request

        [Test]
        public async Task AnUninterruptedDownloadIsOneRequest()
        {

            using var server = new ScriptedServer((i, request) =>
                                   (Answer("HTTP/1.1 200 OK\r\nETag: \"v1\"\r\nContent-Length: 1000\r\n", Body), false));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.True, result.Error);
                Assert.That(result.WasUninterrupted,                               Is.True);
                Assert.That(result.BytesWritten,                                   Is.EqualTo(1000));
                Assert.That(destination.ToArray(),                                 Is.EqualTo(Body));
                Assert.That(Header(server.Requests[0], "Accept-Encoding"),         Is.EqualTo("identity"));
                Assert.That(Header(server.Requests[0], "Range"),                   Is.EqualTo("-"));
            });

        }

        #endregion

        #region A body that breaks off is resumed with Range and If-Range

        [Test]
        public async Task ABodyThatBreaksOffIsResumedWithRangeAndIfRange()
        {

            using var server = new ScriptedServer((i, request) => i == 0
                                   ? (Answer("HTTP/1.1 200 OK\r\nETag: \"v1\"\r\nContent-Length: 1000\r\n", Body, 0, 400), true)
                                   : (Answer("HTTP/1.1 206 Partial Content\r\nETag: \"v1\"\r\nContent-Range: bytes 400-999/1000\r\nContent-Length: 600\r\n", Body, 400), false));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.True, result.Error);
                Assert.That(result.HTTPStatusCode,                                 Is.EqualTo(HTTPStatusCode.PartialContent));
                Assert.That(result.Attempts,                                       Is.EqualTo(2));
                Assert.That(result.Resumes,                                        Is.EqualTo(1));
                Assert.That(result.Restarts,                                       Is.EqualTo(0));
                Assert.That(result.Validator,                                      Is.EqualTo("\"v1\""));
                Assert.That(destination.ToArray(),                                 Is.EqualTo(Body));
                Assert.That(Header(server.Requests[1], "Range"),                   Is.EqualTo("bytes=400-"));
                Assert.That(Header(server.Requests[1], "If-Range"),                Is.EqualTo("\"v1\""));
            });

        }

        #endregion

        #region A chunked body that breaks off is resumed too

        [Test]
        public async Task AChunkedBodyThatBreaksOffIsResumedToo()
        {

            using var server = new ScriptedServer((i, request) => i == 0
                                   ? ([.. Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nETag: \"v1\"\r\nTransfer-Encoding: chunked\r\n\r\n3e8\r\n"), .. Body.AsSpan(0, 700).ToArray()], true)
                                   : (Answer("HTTP/1.1 206 Partial Content\r\nETag: \"v1\"\r\nContent-Range: bytes 700-999/1000\r\nContent-Length: 300\r\n", Body, 700), false));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.True, result.Error);
                Assert.That(destination.ToArray(),                                 Is.EqualTo(Body));
                Assert.That(Header(server.Requests[1], "Range"),                   Is.EqualTo("bytes=700-"));
            });

        }

        #endregion

        #region Last-Modified guards a resume when the entity-tag is weak

        /// <summary>
        /// A weak entity-tag promises an equivalent representation, not the same
        /// octets, and splicing needs the same octets.
        /// </summary>
        [Test]
        public async Task LastModifiedGuardsAResumeWhenTheEntityTagIsWeak()
        {

            const String date = "Sun, 06 Nov 1994 08:49:37 GMT";

            using var server = new ScriptedServer((i, request) => i == 0
                                   ? (Answer($"HTTP/1.1 200 OK\r\nETag: W/\"v1\"\r\nLast-Modified: {date}\r\nContent-Length: 1000\r\n", Body, 0, 250), true)
                                   : (Answer($"HTTP/1.1 206 Partial Content\r\nLast-Modified: {date}\r\nContent-Range: bytes 250-999/1000\r\nContent-Length: 750\r\n", Body, 250), false));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.True, result.Error);
                Assert.That(Header(server.Requests[1], "If-Range"),                Is.EqualTo(date));
                Assert.That(destination.ToArray(),                                 Is.EqualTo(Body));
            });

        }

        #endregion

        #region A changed representation starts over

        [Test]
        public async Task ARepresentationThatChangedStartsOver()
        {

            using var server = new ScriptedServer((i, request) => i == 0
                                   ? (Answer("HTTP/1.1 200 OK\r\nETag: \"v1\"\r\nContent-Length: 1000\r\n", Body, 0, 400), true)
                                   : (Answer("HTTP/1.1 200 OK\r\nETag: \"v2\"\r\nContent-Length: 800\r\n", Changed), false));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.True, result.Error);
                Assert.That(result.Restarts,                                       Is.EqualTo(1));
                Assert.That(result.BytesWritten,                                   Is.EqualTo(800));
                Assert.That(destination.ToArray(),                                 Is.EqualTo(Changed));
                Assert.That(result.Validator,                                      Is.EqualTo("\"v2\""));
            });

        }

        #endregion

        #region A 206 that starts anywhere else starts over

        [Test]
        public async Task A206ThatStartsAnywhereElseStartsOver()
        {

            using var server = new ScriptedServer((i, request) => i == 0
                                   ? (Answer("HTTP/1.1 200 OK\r\nETag: \"v1\"\r\nContent-Length: 1000\r\n", Body, 0, 400), true)
                                   : (Answer("HTTP/1.1 206 Partial Content\r\nETag: \"v1\"\r\nContent-Range: bytes 0-999/1000\r\nContent-Length: 1000\r\n", Body), false));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.True, result.Error);
                Assert.That(result.Restarts,                                       Is.EqualTo(1));
                Assert.That(destination.ToArray(),                                 Is.EqualTo(Body));
            });

        }

        #endregion

        #region Without a validator a broken body is not resumed

        [Test]
        public async Task WithoutAValidatorABrokenBodyIsNotResumed()
        {

            using var server = new ScriptedServer((i, request) =>
                                   (Answer("HTTP/1.1 200 OK\r\nContent-Length: 1000\r\n", Body, 0, 400), true));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.False);
                Assert.That(result.HTTPStatusCode,                                 Is.EqualTo(HTTPStatusCode.ClientError));
                Assert.That(result.Error,                                          Does.Contain("no validator"));
                Assert.That(result.Attempts,                                       Is.EqualTo(1));
                Assert.That(server.Requests,                                       Has.Count.EqualTo(1), "not sent again from its first octet");
                Assert.That(result.BytesWritten,                                   Is.EqualTo(400));
            });

        }

        #endregion

        #region The attempts are bounded

        [Test]
        public async Task TheAttemptsAreBounded()
        {

            using var server = new ScriptedServer((i, request) => i == 0
                                   ? (Answer("HTTP/1.1 200 OK\r\nETag: \"v1\"\r\nContent-Length: 1000\r\n", Body, 0, 100), true)
                                   : (Answer($"HTTP/1.1 206 Partial Content\r\nETag: \"v1\"\r\nContent-Range: bytes {i * 100}-999/1000\r\nContent-Length: {1000 - i * 100}\r\n", Body, i * 100, 100), true));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, MaxAttempts: 3, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.False);
                Assert.That(result.Attempts,                                       Is.EqualTo(3));
                Assert.That(result.Resumes,                                        Is.EqualTo(2));
                Assert.That(result.BytesWritten,                                   Is.EqualTo(300));
                Assert.That(destination.ToArray(),                                 Is.EqualTo(Body[..300]));
                Assert.That(server.Requests,                                       Has.Count.EqualTo(3));
            });

        }

        #endregion

        #region An error status writes nothing

        [Test]
        public async Task AnErrorStatusWritesNothing()
        {

            using var server = new ScriptedServer((i, request) =>
                                   (Answer("HTTP/1.1 404 Not Found\r\nContent-Length: 9\r\n", "not found"u8.ToArray()), false));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.False);
                Assert.That(result.HTTPStatusCode,                                 Is.EqualTo(HTTPStatusCode.NotFound));
                Assert.That(destination.Length,                                    Is.EqualTo(0));
                Assert.That(result.Response?.HTTPBodyAsUTF8String,                 Is.EqualTo("not found"));
            });

        }

        #endregion

        #region A 416 for exactly what is held finishes the download

        /// <summary>
        /// The chunked body carries all 1000 octets and breaks off before its
        /// last chunk, so the client cannot know the body was whole until it asks
        /// for the rest and is told there is none.
        /// </summary>
        [Test]
        public async Task A416ForExactlyWhatIsHeldFinishesTheDownload()
        {

            using var server = new ScriptedServer((i, request) => i == 0
                                   ? ([.. Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nETag: \"v1\"\r\nTransfer-Encoding: chunked\r\n\r\n3e8\r\n"), .. Body, .. "\r\n"u8.ToArray()], true)
                                   : (Answer("HTTP/1.1 416 Range Not Satisfiable\r\nContent-Range: bytes */1000\r\nContent-Length: 0\r\n", []), false));

            using var client      = new HTTPClient(server.URL);
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.True, result.Error);
                Assert.That(result.HTTPStatusCode,                                 Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(destination.ToArray(),                                 Is.EqualTo(Body));
                Assert.That(Header(server.Requests[1], "Range"),                   Is.EqualTo("bytes=1000-"));
            });

        }

        #endregion

        #region The Repr-Digest is checked over the resumed whole

        [Test]
        public async Task TheReprDigestIsCheckedOverTheResumedWhole()
        {

            using var server = new ScriptedServer((i, request) => i == 0
                                   ? (Answer($"HTTP/1.1 200 OK\r\nETag: \"v1\"\r\nRepr-Digest: {ReprDigest(Body)}\r\nContent-Length: 1000\r\n", Body, 0, 400), true)
                                   : (Answer($"HTTP/1.1 206 Partial Content\r\nETag: \"v1\"\r\nRepr-Digest: {ReprDigest(Body)}\r\nContent-Range: bytes 400-999/1000\r\nContent-Length: 600\r\n", Body, 400), false));

            using var client      = new HTTPClient(server.URL) { VerifyDigests = true };
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.True, result.Error);
                Assert.That(result.DigestVerification,                             Is.EqualTo(HTTPDigestVerification.Match));
                Assert.That(Header(server.Requests[0], "Want-Repr-Digest"),        Is.EqualTo(HTTPDigest.Want));
                Assert.That(Header(server.Requests[0], "Want-Content-Digest"),     Is.EqualTo("-"));
            });

        }

        [Test]
        public async Task AReprDigestThatDoesNotMatchFailsTheDownload()
        {

            using var server = new ScriptedServer((i, request) =>
                                   (Answer($"HTTP/1.1 200 OK\r\nETag: \"v1\"\r\nRepr-Digest: {ReprDigest(Changed)}\r\nContent-Length: 1000\r\n", Body), false));

            using var client      = new HTTPClient(server.URL) { VerifyDigests = true };
            using var destination = new MemoryStream();

            var result = await client.DownloadAsync(HTTPPath.Root + "file", destination, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(result.IsComplete,                                     Is.False);
                Assert.That(result.HTTPStatusCode,                                 Is.EqualTo(HTTPStatusCode.ClientError));
                Assert.That(result.DigestVerification,                             Is.EqualTo(HTTPDigestVerification.Mismatch));
            });

        }

        #endregion

    }

}
