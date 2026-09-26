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
    /// RFC 9112, Section 7.1: a message that announces the chunked transfer
    /// coding has to carry chunk framing, and it ends with the zero-length
    /// chunk and with nothing else.
    ///
    /// These tests read the raw octets rather than a parsed message, because
    /// every defect here is invisible to a lenient parser and fatal to a strict
    /// one. What used to happen: a handler that announced the coding and gave a
    /// ChunkWorker, but no ChunkedTransferEncodingStream, produced a response
    /// with no body and no terminator at all. The last test goes the other way
    /// down the wire - the client had the same hole in its request path.
    ///
    /// What is *not* under test here, because it is the existing contract
    /// rather than a defect: a chunked response carrying a byte array or an
    /// ordinary stream is taken to be framed already, and is copied through
    /// untouched. AutomaticallyChunkContent is how a handler says the opposite.
    /// Chunked_Response_Content_And_Stream_Must_Be_Sent in
    /// HTTP11AuditRegressionTests pins that, and it is what says no to the
    /// tempting idea of framing everything that says "chunked".
    /// </summary>
    [TestFixture]
    public class ChunkedFramingTests
    {

        #region (private) RecordingServer

        /// <summary>
        /// A bare TCP listener that records one request and answers it, so that
        /// what the *client* framed can be read as octets. It answers as soon as
        /// it has seen the terminating chunk, or when it gives up waiting for
        /// one - the second case is what a client that never ends its body does
        /// to a server, and the point of measuring it here.
        /// </summary>
        private sealed class RecordingServer : IDisposable
        {

            private readonly TcpListener listener = new (System.Net.IPAddress.Loopback, 0);

            public Task<String>  Recorded  { get; }

            public URL           URL
                => URL.Parse($"http://127.0.0.1:{((System.Net.IPEndPoint) listener.LocalEndpoint).Port}");

            public RecordingServer()
            {
                listener.Start();
                Recorded = Task.Run(Record);
            }

            private async Task<String> Record()
            {

                using var tcpClient  = await listener.AcceptTcpClientAsync();
                using var giveUp     = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                var stream  = tcpClient.GetStream();
                var seen    = new List<Byte>();
                var buffer  = new Byte[4096];

                try
                {
                    while (true)
                    {

                        var read = await stream.ReadAsync(buffer, giveUp.Token);

                        if (read == 0)
                            break;

                        seen.AddRange(buffer[..read]);

                        if (Encoding.Latin1.GetString([.. seen]).Contains("\r\n0\r\n\r\n", StringComparison.Ordinal))
                            break;

                    }
                }
                catch (OperationCanceledException)
                { }

                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\n\r\n"));
                await stream.FlushAsync();

                return Encoding.Latin1.GetString([.. seen]);

            }

            public void Dispose()
                => listener.Stop();

        }

        #endregion

        #region Data

        private HTTPServer?  httpServer;
        private HTTPAPI?     httpAPI;

        private IPPort       Port
            => httpServer!.TCPPort;

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

            #region GET /worker      - Transfer-Encoding and a ChunkWorker, and nothing else

            // The shape the finding is about: no ContentStream, so nothing here
            // knows about request.NetworkStream.
            httpAPI.AddHandler(HTTPPath.Root + "worker",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode    = HTTPStatusCode.OK,
                                                      ContentType       = HTTPContentType.Text.PLAIN,
                                                      TransferEncoding  = "chunked",
                                                      ChunkWorker       = async (response, stream) => {
                                                          await stream.WriteAsync("one\n".ToUTF8Bytes(), null);
                                                          await stream.WriteAsync("two\n".ToUTF8Bytes(), null);
                                                          await stream.Finish();
                                                      }
                                                  }.AsImmutable));

            #endregion

            #region GET /declared    - chunked, and then nothing at all

            httpAPI.AddHandler(HTTPPath.Root + "declared",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode    = HTTPStatusCode.OK,
                                                      ContentType       = HTTPContentType.Text.PLAIN,
                                                      TransferEncoding  = "chunked"
                                                  }.AsImmutable));

            #endregion

            #region GET /forgetful   - a worker that never finishes what it started

            httpAPI.AddHandler(HTTPPath.Root + "forgetful",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode    = HTTPStatusCode.OK,
                                                      ContentType       = HTTPContentType.Text.PLAIN,
                                                      TransferEncoding  = "chunked",
                                                      ChunkWorker       = async (response, stream) => {
                                                          await stream.WriteAsync("half a message\n".ToUTF8Bytes(), null);
                                                      }
                                                  }.AsImmutable));

            #endregion

            #region GET /ownstream   - the handler builds the stream itself, as it always could

            httpAPI.AddHandler(HTTPPath.Root + "ownstream",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode    = HTTPStatusCode.OK,
                                                      ContentType       = HTTPContentType.Text.PLAIN,
                                                      TransferEncoding  = "chunked",
                                                      Trailer           = "X-Checksum",
                                                      ContentStream     = new ChunkedTransferEncodingStream(request.NetworkStream!, true),
                                                      ChunkWorker       = async (response, stream) => {
                                                          await stream.WriteAsync("from my own stream\n".ToUTF8Bytes(), null);
                                                          await stream.Finish(
                                                              new Dictionary<String, String> {
                                                                  { "X-Checksum", "deadbeef" }
                                                              }
                                                          );
                                                      }
                                                  }.AsImmutable));

            #endregion

            #region GET /plain       - an ordinary response, to ask a connection whether it still works

            httpAPI.AddHandler(HTTPPath.Root + "plain",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = "still here".ToUTF8Bytes()
                                                  }.AsImmutable));

            #endregion

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {
            if (httpServer is not null)
                await httpServer.Stop();
        }

        #endregion

        #region (private) WireOf(Path)

        /// <summary>
        /// Everything the server puts on the connection for one request, as
        /// octets. The requests below ask for the connection to be closed, so
        /// the close is the end of the transfer and no framing has to be
        /// trusted in order to read it - which is the point, since the framing
        /// is what is under test.
        /// </summary>
        private async Task<String> WireOf(String Path)
        {

            using var client = new TcpClient();

            await client.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToUInt16());

            var stream  = client.GetStream();
            var request = $"GET {Path} HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n";

            await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
            await stream.FlushAsync();

            using var buffer   = new MemoryStream();
            using var giveUp   = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await stream.CopyToAsync(buffer, giveUp.Token);

            return Encoding.Latin1.GetString(buffer.ToArray());

        }

        #endregion

        #region (private) BodyOf(Wire)

        private static String BodyOf(String Wire)
        {

            var separator = Wire.IndexOf("\r\n\r\n", StringComparison.Ordinal);

            Assert.That(separator, Is.GreaterThan(0), "The response has no header/body separator.");

            return Wire[(separator + 4)..];

        }

        #endregion


        #region AChunkWorkerWithoutItsOwnStreamStillWritesTheBody()

        /// <summary>
        /// The finding. A handler that sets Transfer-Encoding and a ChunkWorker
        /// has said everything a reader of the API would think is needed; the
        /// dispatch, however, keyed on the body being a
        /// ChunkedTransferEncodingStream, so this used to send the header and
        /// then nothing - not even the terminating chunk.
        /// </summary>
        [Test]
        public async Task AChunkWorkerWithoutItsOwnStreamStillWritesTheBody()
        {

            var wire = await WireOf("/worker");

            Assert.Multiple(() => {
                Assert.That(wire,          Does.Contain("Transfer-Encoding: chunked"));
                Assert.That(BodyOf(wire),  Is.EqualTo("4\r\none\n\r\n4\r\ntwo\n\r\n0\r\n\r\n"), wire);
            });

        }

        #endregion

        #region AChunkedResponseAlwaysEndsWithTheTerminalChunk()

        /// <summary>
        /// Nothing to write is still something to say: an empty chunked body is
        /// the zero-length chunk, and a response that stops before it is a
        /// truncated message rather than an empty one. The recipient cannot
        /// tell the difference except by waiting.
        /// </summary>
        [Test]
        public async Task AChunkedResponseAlwaysEndsWithTheTerminalChunk()
        {

            var wire = await WireOf("/declared");

            Assert.That(BodyOf(wire), Is.EqualTo("0\r\n\r\n"), wire);

        }

        #endregion

        #region AWorkerThatDoesNotFinishIsFinishedForIt()

        /// <summary>
        /// A worker that returns without writing the terminal chunk - or that
        /// throws halfway through - leaves a message that never ends. The
        /// server closes it, once.
        /// </summary>
        [Test]
        public async Task AWorkerThatDoesNotFinishIsFinishedForIt()
        {

            var wire = await WireOf("/forgetful");
            var body = BodyOf(wire);

            Assert.Multiple(() => {
                Assert.That(body,                                Is.EqualTo("F\r\nhalf a message\n\r\n0\r\n\r\n"), wire);
                Assert.That(body.Split("0\r\n\r\n").Length - 1,  Is.EqualTo(1), "The terminal chunk was written more than once.");
            });

        }

        #endregion

        #region AHandlerSuppliedStreamIsFinishedExactlyOnce()

        /// <summary>
        /// The pattern that always worked has to keep working, and must not now
        /// be finished a second time by the server: two terminal chunks would
        /// start a second message on this connection. Finish being idempotent
        /// is what makes the server's unconditional call safe - including for
        /// the trailer section, which belongs to the worker's Finish and not to
        /// the server's.
        /// </summary>
        [Test]
        public async Task AHandlerSuppliedStreamIsFinishedExactlyOnce()
        {

            var wire = await WireOf("/ownstream");
            var body = BodyOf(wire);

            Assert.Multiple(() => {
                Assert.That(body,                               Is.EqualTo("13\r\nfrom my own stream\n\r\n0\r\nX-Checksum: deadbeef\r\n\r\n"), wire);
                Assert.That(body.Split("\r\n0\r\n").Length - 1, Is.EqualTo(1), "The terminal chunk was written more than once.");
            });

        }

        #endregion

        #region AChunkedResponseLeavesTheConnectionUsable()

        /// <summary>
        /// What a missing terminator actually costs: the next request on the
        /// same connection. This one keeps the connection alive across a
        /// chunked response and then asks it a second, ordinary question.
        /// </summary>
        [Test]
        public async Task AChunkedResponseLeavesTheConnectionUsable()
        {

            await using var client = await HTTPRawSocketClient.ConnectAsync(
                                               System.Net.IPAddress.Loopback,
                                               Port
                                           );

            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await client.SendAsync("GET /worker HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n", giveUp.Token);

            var first  = await client.ReadResponseAsync(CancellationToken: giveUp.Token);

            await client.SendAsync("GET /plain HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n", giveUp.Token);

            var second = await client.ReadResponseAsync(CancellationToken: giveUp.Token);

            Assert.Multiple(() => {
                Assert.That(first.StatusCode,                       Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(first.Body!),   Is.EqualTo("one\ntwo\n"));
                Assert.That(second.StatusCode,                      Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(second.Body!),  Is.EqualTo("still here"));
            });

        }

        #endregion

        #region TheClientEndsAChunkedRequestBodyToo()

        /// <summary>
        /// The mirror image, found while fixing the response side: the client
        /// runs the request's ChunkWorker and then sends whatever the worker
        /// left behind. A worker that did not call Finish produced a request
        /// body that never ends, and a server with nothing to answer - the same
        /// silence, one direction over.
        /// </summary>
        [Test]
        public async Task TheClientEndsAChunkedRequestBodyToo()
        {

            using var server = new RecordingServer();
            using var client = new HTTPClient(server.URL);

            await client.RunRequest(
                      HTTPMethod.POST,
                      HTTPPath.Root + "upload",
                      RequestBuilder:  builder => {
                          builder.TransferEncoding  = "chunked";
                          builder.UseChunkWorker    = true;
                          builder.ChunkWorker       = async (request, stream) => {
                              await stream.WriteAsync("first\n".ToUTF8Bytes(),  null);
                              await stream.WriteAsync("second\n".ToUTF8Bytes(), null);
                              // and deliberately no Finish()
                          };
                      },
                      RequestTimeout:  TimeSpan.FromSeconds(10)
                  );

            var recorded = await server.Recorded;
            var body     = recorded[(recorded.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];

            Assert.That(body, Is.EqualTo("6\r\nfirst\n\r\n7\r\nsecond\n\r\n0\r\n\r\n"), recorded);

        }

        #endregion

    }

}
