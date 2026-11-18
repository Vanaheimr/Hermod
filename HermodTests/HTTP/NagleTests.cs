/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Neither end of an HTTP/1.1 connection holds back what it writes for
    /// Nagle's algorithm.
    /// </summary>
    /// <remarks>
    /// The server writes a response's header and its body one after the
    /// other, and the client a request's header and its body. With Nagle's
    /// algorithm on, the second write waits until the first is acknowledged,
    /// and the other end - waiting for the rest of the message, with nothing
    /// to send - delays its acknowledgement: by 40 ms on Linux, every message
    /// with a body. Windows acknowledges at once on loopback and does not show
    /// it. A charging station's tests, which ask its JSON API one request
    /// after another, took 16 s on Linux and 1 s on Windows, each request
    /// 41 ms apart.
    ///
    /// The NoDelay tests are what fails everywhere; the round trips are what
    /// a user waited for, and fail on Linux only.
    /// </remarks>
    [TestFixture]
    public class NagleTests
    {

        #region Data

        /// <summary>
        /// How many round trips are timed on one connection: enough for the
        /// median to be one of those after the opening quick acknowledgements
        /// of a Linux connection.
        /// </summary>
        private const           Int32     RoundTrips    = 40;

        /// <summary>
        /// Half of a delayed acknowledgement of Linux, and some hundred times a
        /// round trip on loopback.
        /// </summary>
        private static readonly TimeSpan  MedianWithin  = TimeSpan.FromMilliseconds(20);

        #endregion


        #region AnAcceptedConnectionSaysNoDelay()

        /// <summary>
        /// The connection a server accepts has Nagle's algorithm off.
        /// </summary>
        [Test]
        public async Task AnAcceptedConnectionSaysNoDelay()
        {

            var noDelay = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

            var server  = AServer();

            server.OnNewTCPConnection += (tcpServer, timestamp, eventTrackingId, remoteSocket, connectionId, tcpConnection) => {
                noDelay.TrySetResult(tcpConnection.NoDelay);
                return Task.CompletedTask;
            };

            try
            {

                using var client = new TcpClient(AddressFamily.InterNetwork);

                await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                Assert.That(await Task.WhenAny(noDelay.Task, Task.Delay(TimeSpan.FromSeconds(10))) == noDelay.Task,
                            Is.True,
                            "the server never told of the connection");

                Assert.That(noDelay.Task.Result, Is.True, "the server holds back what it writes for Nagle's algorithm");

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region AClientsConnectionSaysNoDelay()

        /// <summary>
        /// The connection an HTTP client makes has Nagle's algorithm off.
        /// </summary>
        [Test]
        public async Task AClientsConnectionSaysNoDelay()
        {

            var server = AServer();

            try
            {

                using var client = new SeeingHTTPClient(URL.Parse($"http://127.0.0.1:{server.TCPPort}"));

                var response = await client.GET(HTTPPath.Root,
                                                Connection:      ConnectionType.KeepAlive,
                                                RequestTimeout:  TimeSpan.FromSeconds(10));

                Assert.Multiple(() => {
                    Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.OK));
                    Assert.That(client.NoDelay,          Is.Not.Null, "the client kept no connection to look at");
                    Assert.That(client.NoDelay,          Is.True,     "the client holds back what it writes for Nagle's algorithm");
                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region ResponsesComeWithoutADelayedAcknowledgement()

        /// <summary>
        /// One request after another on a connection kept open, each written at
        /// once by a client that does not hold back: the server's response, a
        /// header and a body, is not held back either.
        /// </summary>
        [Test]
        public async Task ResponsesComeWithoutADelayedAcknowledgement()
        {

            var server = AServer();

            try
            {

                using var client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };

                await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                var stream  = client.GetStream();
                var request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n");
                var took    = new List<TimeSpan>();

                for (var i = 0; i < RoundTrips; i++)
                {

                    var watch = Stopwatch.StartNew();

                    await stream.WriteAsync(request);
                    await ReadOneResponse(stream);

                    took.Add(watch.Elapsed);

                }

                Assert.That(Median(took), Is.LessThan(MedianWithin), Said(took));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region RequestsWithABodyComeWithoutADelayedAcknowledgement()

        /// <summary>
        /// One request with a body after another, from Hermod's own HTTP client
        /// on a connection kept open: the request, a header and a body, is not
        /// held back on its way.
        /// </summary>
        [Test]
        public async Task RequestsWithABodyComeWithoutADelayedAcknowledgement()
        {

            var server = AServer();

            try
            {

                using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{server.TCPPort}"));

                var took = new List<TimeSpan>();

                for (var i = 0; i < RoundTrips; i++)
                {

                    var watch    = Stopwatch.StartNew();

                    var response = await client.POST(HTTPPath.Root,
                                                     "Hello world!".ToUTF8Bytes(),
                                                     HTTPContentType.Text.PLAIN,
                                                     Connection:      ConnectionType.KeepAlive,
                                                     RequestTimeout:  TimeSpan.FromSeconds(10));

                    took.Add(watch.Elapsed);

                    Assert.That(response.HTTPBodyAsUTF8String, Is.EqualTo("!dlrow olleH"));

                }

                Assert.That(Median(took), Is.LessThan(MedianWithin), Said(took));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion


        #region (private) AServer()

        /// <summary>
        /// A server answering GET with a body and POST with the body turned
        /// round, keeping the connection open.
        /// </summary>
        private static HTTPServer AServer()
        {

            var server = new HTTPServer(IPAddress:  IPv4Address.Localhost,
                                        TCPPort:    IPPort.Zero,
                                        AutoStart:  true);

            var api    = new HTTPAPI(server);

            api.AddHandler(HTTPPath.Root,
                           HTTPMethod:    HTTPMethod.GET,
                           HTTPDelegate:  request => Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode  = HTTPStatusCode.OK,
                                                             Date            = Timestamp.Now,
                                                             ContentType     = HTTPContentType.Text.PLAIN,
                                                             Content         = "Hello world!".ToUTF8Bytes(),
                                                             Connection      = ConnectionType.KeepAlive
                                                         }.AsImmutable));

            api.AddHandler(HTTPPath.Root,
                           HTTPMethod:    HTTPMethod.POST,
                           HTTPDelegate:  request => Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode  = HTTPStatusCode.OK,
                                                             Date            = Timestamp.Now,
                                                             ContentType     = HTTPContentType.Text.PLAIN,
                                                             Content         = new String((request.HTTPBodyAsUTF8String ?? "").Reverse().ToArray()).ToUTF8Bytes(),
                                                             Connection      = ConnectionType.KeepAlive
                                                         }.AsImmutable));

            return server;

        }

        #endregion

        #region (private) ReadOneResponse(Stream)

        /// <summary>
        /// Read one response with a Content-Length, and nothing after it.
        /// </summary>
        private static async Task ReadOneResponse(NetworkStream Stream)
        {

            var header  = new List<Byte>();
            var one     = new Byte[1];

            while (header.Count < 4 ||
                   header[^4] != '\r' || header[^3] != '\n' || header[^2] != '\r' || header[^1] != '\n')
            {
                if (await Stream.ReadAsync(one) == 0)
                    throw new IOException("the server closed the connection");
                header.Add(one[0]);
            }

            var text    = Encoding.ASCII.GetString(header.ToArray());
            var length  = text.Split("\r\n").
                               Where (line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).
                               Select(line => Int32.Parse(line["Content-Length:".Length..].Trim())).
                               Single();

            var body    = new Byte[length];
            var read    = 0;

            while (read < length)
            {
                var got = await Stream.ReadAsync(body.AsMemory(read));
                if (got == 0)
                    throw new IOException("the server closed the connection");
                read += got;
            }

        }

        #endregion

        #region (private) Median(Took) / Said(Took)

        private static TimeSpan Median(List<TimeSpan> Took)
            => Took.OrderBy(took => took).ElementAt(Took.Count / 2);

        private static String Said(List<TimeSpan> Took)
            => $"round trips in ms: {String.Join(", ", Took.Select(took => took.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)))}";

        #endregion

        #region (private class) SeeingHTTPClient

        /// <summary>
        /// An HTTP client that says how its connection is set.
        /// </summary>
        private sealed class SeeingHTTPClient(URL RemoteURL) : HTTPClient(RemoteURL)
        {
            public Boolean? NoDelay
                => tcpClient?.Client is not null ? tcpClient.NoDelay : null;
        }

        #endregion

    }

}
