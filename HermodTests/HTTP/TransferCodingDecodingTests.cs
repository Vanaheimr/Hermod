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
using System.IO.Compression;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The transfer codings other than chunked (RFC 9112, Section 7), undone
    /// by both ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Transfer-Encoding: gzip, chunked" means the content was gzipped and
    /// the result chunked. Measured before the fix, on the demo host of
    /// HTTP1ConformanceTests: a POST to /echo with "gzip, chunked",
    /// "deflate, chunked" or "bogus, chunked" was answered 200, and what it
    /// echoed was the gzip octets. The server removed the chunks and handed on
    /// the rest as the content, whatever the coding before chunked was called.
    /// RFC 9112, Section 6.1: "A server that receives a request message with a
    /// transfer coding it does not understand SHOULD respond with 501 (Not
    /// Implemented)."
    /// </para>
    /// <para>
    /// The client refused every response whose Transfer-Encoding was not a
    /// single chunked, safely: it dropped the connection with the body unread.
    /// It now undoes gzip and deflate as the server does, and reads a response
    /// whose final coding is not chunked to the end of the connection
    /// (Section 6.3, item 4), and still refuses a coding it cannot undo.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class TransferCodingDecodingTests
    {

        #region Data

        private const String Text = "The content, which the transfer codings are not part of.";

        private static readonly HTTPSource  source  = new (IPSocket.LocalhostV4(IPPort.HTTP));
        private static readonly IPSocket    socket  = IPSocket.LocalhostV4(IPPort.HTTP);

        private HTTPServer? server;

        /// <summary>
        /// Whether the handler of POST /record got past reading the content.
        /// </summary>
        private volatile Boolean recordedAContent;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
                await server.DisposeAsync();

            server = null;

        }

        #endregion


        #region (private static) Codings

        private static Byte[] GZip(Byte[] Data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(Data);
            return output.ToArray();
        }

        /// <summary>
        /// "deflate" as RFC 9110, Section 8.4.1.2 defines it: the zlib format.
        /// </summary>
        private static Byte[] ZLib(Byte[] Data)
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(Data);
            return output.ToArray();
        }

        private static Byte[] Chunked(Byte[] Data)
        {
            using var output = new MemoryStream();
            output.Write(Encoding.ASCII.GetBytes($"{Data.Length:x}\r\n"));
            output.Write(Data);
            output.Write(Encoding.ASCII.GetBytes("\r\n0\r\n\r\n"));
            return output.ToArray();
        }

        private static Byte[] Concat(String Head, Byte[] Body)
            => [.. Encoding.ASCII.GetBytes(Head), .. Body];

        #endregion

        #region (private) StartEchoServer(MaxHTTPBodySize = null)

        /// <summary>
        /// A server whose POST /echo answers with the content it received.
        /// </summary>
        private async Task<HTTPServer> StartEchoServer(UInt64? MaxHTTPBodySize = null)
        {

            server = new HTTPServer(
                         IPAddress:        IPv4Address.Localhost,
                         TCPPort:          IPPort.Zero,
                         AutoStart:        true,
                         MaxHTTPBodySize:  MaxHTTPBodySize
                     );

            var api = server.AddHTTPAPI();

            // A handler that never reads the content: the server reads it then,
            // to keep the connection in step, and has to tell what it finds.
            api.AddHandler(
                HTTPMethod.POST,
                HTTPPath.Root + "ignore",
                HTTPDelegate: request => Task.FromResult(
                                  new HTTPResponse.Builder(request) {
                                      HTTPStatusCode  = HTTPStatusCode.NoContent,
                                      Connection      = ConnectionType.Close
                                  }.AsImmutable
                              )
            );

            // A handler that acts on what it reads. It must not get to act on
            // octets that did not decode.
            api.AddHandler(
                HTTPMethod.POST,
                HTTPPath.Root + "record",
                HTTPDelegate: request => {
                                  var content = request.HTTPBody;
                                  recordedAContent = true;
                                  return Task.FromResult(
                                             new HTTPResponse.Builder(request) {
                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                 Content         = $"{content?.Length ?? 0} octets recorded".ToUTF8Bytes(),
                                                 Connection      = ConnectionType.Close
                                             }.AsImmutable
                                         );
                              }
            );

            api.AddHandler(
                HTTPMethod.POST,
                HTTPPath.Root + "echo",
                HTTPDelegate: request => Task.FromResult(
                                  new HTTPResponse.Builder(request) {
                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                      ContentType     = HTTPContentType.Application.OCTETSTREAM,
                                      Content         = request.HTTPBody ?? [],
                                      Connection      = ConnectionType.Close
                                  }.AsImmutable
                              )
            );

            await Task.CompletedTask;

            return server;

        }

        #endregion

        #region (private static) SendRaw(Port, Request)

        /// <summary>
        /// Send the given octets and return the response that comes back, its
        /// head as text and its body as octets.
        /// </summary>
        private static async Task<(String Head, Byte[] Body)> SendRaw(IPPort  Port,
                                                                       Byte[]  Request)
        {

            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToInt32());

            await using var stream = client.GetStream();
            await stream.WriteAsync(Request);

            using var received = new MemoryStream();
            using var timeout  = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var buffer = new Byte[8192];

            try
            {
                Int32 read;
                while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
                    received.Write(buffer, 0, read);
            }
            catch (OperationCanceledException)
            { }
            catch (IOException)
            { }

            var all  = received.ToArray();
            var end  = all.AsSpan().IndexOf("\r\n\r\n"u8);

            return end < 0
                       ? (Encoding.ASCII.GetString(all), [])
                       : (Encoding.ASCII.GetString(all, 0, end), all[(end + 4)..]);

        }

        #endregion


        #region The parse: 501 for a coding the server cannot undo

        /// <summary>
        /// Chunked is final in each, so the framing is sound; what is under it
        /// the server cannot undo. compress is LZW, which the BCL has no
        /// decoder for; br and zstd are content codings, and the Transfer
        /// Coding Registry does not have them.
        /// </summary>
        [TestCase("bogus, chunked")]
        [TestCase("compress, chunked")]
        [TestCase("x-compress, chunked")]
        [TestCase("identity, chunked")]
        [TestCase("br, chunked")]
        [TestCase("zstd, chunked")]
        [TestCase("gzip, bogus, chunked")]
        public void A_Coding_The_Server_Cannot_Undo_Is_501(String TransferEncoding)
        {

            var parsed = HTTPRequest.TryParse(
                             Timestamp.Now, source, socket, socket,
                             $"POST /echo HTTP/1.1\r\nHost: example.org\r\nTransfer-Encoding: {TransferEncoding}\r\n",
                             out var request,
                             out var failure
                         );

            Assert.Multiple(() => {
                Assert.That(parsed,                       Is.False);
                Assert.That(request,                      Is.Null);
                Assert.That(failure?.HTTPStatusCode,      Is.EqualTo(HTTPStatusCode.NotImplemented));
                Assert.That(failure?.Connection,          Is.EqualTo(ConnectionType.Close));
            });

        }

        [TestCase("gzip, chunked")]
        [TestCase("x-gzip, chunked")]
        [TestCase("deflate, chunked")]
        [TestCase("GZip, chunked")]
        [TestCase("gzip, deflate, chunked")]
        public void A_Coding_The_Server_Undoes_Is_Accepted(String TransferEncoding)
        {

            var parsed = HTTPRequest.TryParse(
                             Timestamp.Now, source, socket, socket,
                             $"POST /echo HTTP/1.1\r\nHost: example.org\r\nTransfer-Encoding: {TransferEncoding}\r\n",
                             out var request,
                             out var failure
                         );

            Assert.That(parsed, Is.True, failure?.HTTPStatusCode.ToString());

        }

        #endregion

        #region The server: the content is what the handler gets

        [Test]
        public async Task The_Server_Undoes_GZip_Under_Chunked()
        {

            var echo     = await StartEchoServer();
            var response = await SendRaw(echo.TCPPort,
                                         Concat("POST /echo HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: gzip, chunked\r\n\r\n",
                                                Chunked(GZip(Text.ToUTF8Bytes()))));

            Assert.Multiple(() => {
                Assert.That(response.Head,                               Does.StartWith("HTTP/1.1 200"), response.Head);
                Assert.That(Encoding.UTF8.GetString(response.Body),      Is.EqualTo(Text));
            });

        }

        [Test]
        public async Task The_Server_Undoes_XGZip_And_Deflate_Too()
        {

            var echo     = await StartEchoServer();

            var xgzip    = await SendRaw(echo.TCPPort,
                                         Concat("POST /echo HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: x-gzip, chunked\r\n\r\n",
                                                Chunked(GZip(Text.ToUTF8Bytes()))));

            var deflate  = await SendRaw(echo.TCPPort,
                                         Concat("POST /echo HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: deflate, chunked\r\n\r\n",
                                                Chunked(ZLib(Text.ToUTF8Bytes()))));

            Assert.Multiple(() => {
                Assert.That(Encoding.UTF8.GetString(xgzip.Body),    Is.EqualTo(Text), xgzip.Head);
                Assert.That(Encoding.UTF8.GetString(deflate.Body),  Is.EqualTo(Text), deflate.Head);
            });

        }

        /// <summary>
        /// "deflate, gzip, chunked": deflate first, gzip on top, so gzip is
        /// undone first.
        /// </summary>
        [Test]
        public async Task The_Server_Undoes_Codings_In_Reverse_Order()
        {

            var echo     = await StartEchoServer();
            var response = await SendRaw(echo.TCPPort,
                                         Concat("POST /echo HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: deflate, gzip, chunked\r\n\r\n",
                                                Chunked(GZip(ZLib(Text.ToUTF8Bytes())))));

            Assert.That(Encoding.UTF8.GetString(response.Body), Is.EqualTo(Text), response.Head);

        }

        [Test]
        public async Task Octets_That_Are_Not_GZip_Are_400()
        {

            var echo     = await StartEchoServer();
            var response = await SendRaw(echo.TCPPort,
                                         Concat("POST /echo HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: gzip, chunked\r\n\r\n",
                                                Chunked(Text.ToUTF8Bytes())));

            Assert.That(response.Head, Does.StartWith("HTTP/1.1 400"), response.Head);

        }

        /// <summary>
        /// A handler that reads octets that do not decode gets the failure, not
        /// an empty content to act on.
        /// </summary>
        [Test]
        public async Task A_Handler_Does_Not_Get_To_Act_On_Octets_That_Do_Not_Decode()
        {

            var echo     = await StartEchoServer();
            var response = await SendRaw(echo.TCPPort,
                                         Concat("POST /record HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: gzip, chunked\r\n\r\n",
                                                Chunked(Text.ToUTF8Bytes())));

            Assert.Multiple(() => {
                Assert.That(response.Head,     Does.StartWith("HTTP/1.1 400"), response.Head);
                Assert.That(recordedAContent,  Is.False, "the handler went on with a content that did not decode");
            });

        }

        /// <summary>
        /// The same octets to a handler that does not read them: the server
        /// reads them after it, and they are no gzip either.
        /// </summary>
        [Test]
        public async Task Octets_That_Are_Not_GZip_Are_400_When_The_Handler_Does_Not_Read_Them()
        {

            var echo     = await StartEchoServer();
            var response = await SendRaw(echo.TCPPort,
                                         Concat("POST /ignore HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: gzip, chunked\r\n\r\n",
                                                Chunked(Text.ToUTF8Bytes())));

            Assert.That(response.Head, Does.StartWith("HTTP/1.1 400"), response.Head);

        }

        /// <summary>
        /// The body limit holds for what the coding expands to, not only for
        /// what arrives: 64 KiB of zeros are a few hundred octets of gzip.
        /// </summary>
        [Test]
        public async Task The_Body_Limit_Holds_For_The_Decoded_Content()
        {

            var bomb     = GZip(new Byte[64 * 1024]);

            Assert.That(bomb.Length, Is.LessThan(1024), "the fixture proves nothing unless the content really expands");

            var echo     = await StartEchoServer(MaxHTTPBodySize: 4096);
            var response = await SendRaw(echo.TCPPort,
                                         Concat("POST /echo HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: gzip, chunked\r\n\r\n",
                                                Chunked(bomb)));

            Assert.That(response.Head, Does.StartWith("HTTP/1.1 413"), response.Head);

        }

        [Test]
        public async Task A_Coding_The_Server_Cannot_Undo_Is_501_On_The_Wire()
        {

            var echo     = await StartEchoServer();
            var response = await SendRaw(echo.TCPPort,
                                         Concat("POST /echo HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: bogus, chunked\r\n\r\n",
                                                Chunked(GZip(Text.ToUTF8Bytes()))));

            Assert.That(response.Head, Does.StartWith("HTTP/1.1 501"), response.Head);

        }

        #endregion

        #region The client: the content is what the caller gets

        /// <summary>
        /// A server that answers one request with the given octets, then closes.
        /// </summary>
        private static async Task<(TcpListener Listener, Task Served)> ServeOnce(Byte[] Response)
        {

            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var served   = Task.Run(async () => {

                using var connection = await listener.AcceptTcpClientAsync();
                await using var stream = connection.GetStream();

                var buffer = new Byte[4096];
                var length = 0;

                while (length < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(length));
                    if (read == 0)
                        break;
                    length += read;
                    if (Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n"))
                        break;
                }

                await stream.WriteAsync(Response);
                await stream.FlushAsync();

            });

            await Task.CompletedTask;

            return (listener, served);

        }

        private static HTTPClient ClientFor(TcpListener Listener)

            => new (URL.Parse($"http://127.0.0.1:{((IPEndPoint) Listener.LocalEndpoint).Port}"),
                    MaxNumberOfRetries: 0);

        [Test]
        public async Task The_Client_Undoes_GZip_Under_Chunked()
        {

            var (listener, served) = await ServeOnce(Concat("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n",
                                                            Chunked(GZip(Text.ToUTF8Bytes()))));

            try
            {

                using var client = ClientFor(listener);

                var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

                Assert.Multiple(() => {
                    Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK), response.HTTPBodyAsUTF8String);
                    Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo(Text));
                });

                await served.WaitAsync(TimeSpan.FromSeconds(5));

            }
            finally
            {
                listener.Stop();
            }

        }

        /// <summary>
        /// RFC 9112, Section 6.3, item 4: a response whose final transfer
        /// coding is not chunked ends where the server closes the connection.
        /// No "Connection: close" says so here, and the connection is not used
        /// again.
        /// </summary>
        [Test]
        public async Task The_Client_Reads_A_GZip_Response_To_The_End_Of_The_Connection()
        {

            var (listener, served) = await ServeOnce(Concat("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\n\r\n",
                                                            GZip(Text.ToUTF8Bytes())));

            try
            {

                using var client = ClientFor(listener);

                var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

                Assert.Multiple(() => {
                    Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK), response.HTTPBodyAsUTF8String);
                    Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo(Text));
                    Assert.That(client.IsHTTPConnected,         Is.False, "the connection was kept after a body the server ended by closing it");
                });

                await served.WaitAsync(TimeSpan.FromSeconds(5));

            }
            finally
            {
                listener.Stop();
            }

        }

        /// <summary>
        /// The transfer coding is below the content coding: deflate is undone
        /// first, and then the gzip the content was stored in.
        /// </summary>
        [Test]
        public async Task The_Client_Undoes_The_Transfer_Coding_Before_The_Content_Coding()
        {

            var (listener, served) = await ServeOnce(Concat("HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nTransfer-Encoding: deflate, chunked\r\n\r\n",
                                                            Chunked(ZLib(GZip(Text.ToUTF8Bytes())))));

            try
            {

                using var client = ClientFor(listener);
                client.AutomaticDecompression = true;

                var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

                Assert.Multiple(() => {
                    Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo(Text));
                    Assert.That(response.DecodedContentEncoding,  Is.EqualTo("gzip"));
                });

                await served.WaitAsync(TimeSpan.FromSeconds(5));

            }
            finally
            {
                listener.Stop();
            }

        }

        /// <summary>
        /// Chunked twice, or not last: a sender must not (Section 6.1), and the
        /// client says which rule it was.
        /// </summary>
        [TestCase("chunked, chunked")]
        [TestCase("chunked, gzip")]
        public async Task The_Client_Refuses_Chunked_Twice_Or_Not_Last(String TransferEncoding)
        {

            var (listener, served) = await ServeOnce(Concat($"HTTP/1.1 200 OK\r\nTransfer-Encoding: {TransferEncoding}\r\n\r\n",
                                                            Chunked(Text.ToUTF8Bytes())));

            try
            {

                using var client = ClientFor(listener);

                var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

                Assert.Multiple(() => {
                    Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.ClientError));
                    Assert.That(response.HTTPBodyAsUTF8String,  Does.Contain("chunked once at most, and only last"));
                });

                await served.WaitAsync(TimeSpan.FromSeconds(5));

            }
            finally
            {
                listener.Stop();
            }

        }

        [TestCase("br, chunked")]
        [TestCase("compress, chunked")]
        [TestCase("bogus")]
        public async Task The_Client_Refuses_A_Coding_It_Cannot_Undo(String TransferEncoding)
        {

            var (listener, served) = await ServeOnce(Concat($"HTTP/1.1 200 OK\r\nTransfer-Encoding: {TransferEncoding}\r\n\r\n",
                                                            Chunked(Text.ToUTF8Bytes())));

            try
            {

                using var client = ClientFor(listener);

                var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

                Assert.Multiple(() => {
                    Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.ClientError));
                    Assert.That(response.HTTPBodyAsUTF8String,  Does.Contain("transfer coding"));
                    Assert.That(client.IsHTTPConnected,         Is.False);
                });

                await served.WaitAsync(TimeSpan.FromSeconds(5));

            }
            finally
            {
                listener.Stop();
            }

        }

        #endregion

    }

}
