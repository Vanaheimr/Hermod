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

using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The pool closes a connection the server has sent a GOAWAY on, once its
    /// streams in flight have finished, and on its disposal at the latest.
    ///
    /// It closed it never. The pool took the connection out of its routable
    /// set on the GOAWAY and opened another, and left the old one to finish its
    /// streams, but nothing closed it then, and DisposeAsync closed only the
    /// connections still routable. The server need not close a connection it
    /// has sent a GOAWAY on (RFC 9113, Section 6.8): one that did not kept the
    /// connection, and its TCP connection, open for as long as the pool's
    /// process ran.
    ///
    /// Each test runs a raw HTTP/2 origin on a loopback socket in cleartext that
    /// keeps every TCP connection open, and reads what the client sends on the
    /// first until the end of the stream: a FIN, a reset, or nothing within
    /// <see cref="EndTimeout"/>.
    /// </summary>
    [TestFixture]
    public class ClientPoolDrainTests
    {

        #region (helpers)

        /// <summary>
        /// How long the origin waits for a connection, or for the client to close
        /// its side of one.
        /// </summary>
        private static readonly TimeSpan EndTimeout   = TimeSpan.FromSeconds(5);

        /// <summary>
        /// How long a connection that must stay open is watched for an end.
        /// </summary>
        private static readonly TimeSpan StaysOpenFor = TimeSpan.FromMilliseconds(300);

        /// <summary>
        /// How the origin's reading of a connection ended.
        /// </summary>
        public enum End
        {
            EndOfStream,
            Reset,
            StillOpen
        }

        /// <summary>
        /// A raw HTTP/2 origin: it accepts every connection the pool opens,
        /// answers its preface with the given SETTINGS, and hands it to the test,
        /// which does the rest. Nothing is closed until the origin is disposed.
        /// </summary>
        private sealed class Origin : IAsyncDisposable
        {

            private readonly TcpListener                 listener;
            private readonly HTTP2Frame                  settings;
            private readonly CancellationTokenSource     cts       = new();
            private readonly Channel<Connection>         accepted  = Channel.CreateUnbounded<Connection>();
            private readonly List<Connection>            all       = [];

            public Int32 Port
                => ((IPEndPoint) listener.LocalEndpoint).Port;

            public String Authority
                => $"127.0.0.1:{Port}";

            private Origin(HTTP2Frame Settings)
            {
                settings  = Settings;
                listener  = new TcpListener(System.Net.IPAddress.Loopback, 0);
                listener.Start();
                _ = AcceptLoopAsync();
            }

            /// <summary>
            /// Start an origin that answers each preface with <paramref name="Settings"/>.
            /// </summary>
            public static Origin Start(params (HTTP2SettingsParameter Id, UInt32 Value)[] Settings)
                => new (HTTP2Frame.CreateSettings(Settings));

            private async Task AcceptLoopAsync()
            {
                while (!cts.IsCancellationRequested)
                {

                    TcpClient tcp;

                    try
                    {
                        tcp = await listener.AcceptTcpClientAsync(cts.Token);
                    }
                    catch
                    {
                        break;
                    }

                    var connection = new Connection(tcp);
                    lock (all)
                        all.Add(connection);

                    _ = AnswerPrefaceAsync(connection);

                }
            }

            private async Task AnswerPrefaceAsync(Connection Connection)
            {
                try
                {

                    var preface = new Byte[H2Raw.Preface.Length];
                    if (!await H2Raw.ReadExactAsync(Connection.Stream, preface, cts.Token))
                        return;

                    await Connection.SendAsync(settings);

                    accepted.Writer.TryWrite(Connection);

                }
                catch
                { }
            }

            /// <summary>
            /// The next connection the pool has opened, once its preface is answered.
            /// </summary>
            public Task<Connection> AcceptAsync()
                => accepted.Reader.ReadAsync(cts.Token).AsTask().WaitAsync(EndTimeout);

            public ValueTask DisposeAsync()
            {

                cts.Cancel();

                try { listener.Stop(); } catch { }

                lock (all)
                    foreach (var connection in all)
                        connection.Dispose();

                return ValueTask.CompletedTask;

            }

        }

        /// <summary>
        /// The origin's side of one connection.
        /// </summary>
        private sealed class Connection(TcpClient Tcp) : IDisposable
        {

            public NetworkStream Stream { get; } = Tcp.GetStream();

            private readonly HPACKEncoder encoder = new ();

            /// <summary>
            /// Send <paramref name="Frames"/> in one write.
            /// </summary>
            public async Task SendAsync(params HTTP2Frame[] Frames)
            {
                await Stream.WriteAsync(Frames.SelectMany(frame => frame.Serialize()).ToArray());
                await Stream.FlushAsync();
            }

            /// <summary>
            /// A 200 response with the body "ok" on <paramref name="StreamId"/>, which ends the stream.
            /// </summary>
            public Task RespondAsync(UInt32 StreamId)

                => SendAsync(HTTP2Frame.CreateHeaders(StreamId,
                                                      encoder.EncodeHeaderBlock([(":status", "200"), ("content-length", "2")]),
                                                      EndStream: false),
                             HTTP2Frame.CreateData(StreamId, "ok"u8.ToArray(), EndStream: true));

            /// <summary>
            /// Read the frames the client sends up to its first HEADERS, and
            /// return the stream that HEADERS opens.
            /// </summary>
            public async Task<UInt32> ReadRequestAsync()
            {

                using var timeout = new CancellationTokenSource(EndTimeout);

                while (true)
                {

                    var frame = await H2Raw.ReadFrameAsync(Stream, timeout.Token);

                    Assert.That(frame, Is.Not.Null, "the client's request");

                    if (frame!.Type == HTTP2FrameType.HEADERS)
                        return frame.StreamId;

                }

            }

            /// <summary>
            /// Read the frames the client sends, until the end of the stream, a
            /// reset, or nothing more within <paramref name="Timeout"/>.
            /// </summary>
            public async Task<End> ReadToEndAsync(TimeSpan? Timeout = null)
            {

                using var timeout = new CancellationTokenSource(Timeout ?? EndTimeout);

                try
                {
                    while (true)
                    {
                        if (await H2Raw.ReadFrameAsync(Stream, timeout.Token) is null)
                            return End.EndOfStream;
                    }
                }
                catch (OperationCanceledException)
                {
                    return End.StillOpen;
                }
                catch (IOException)
                {
                    return End.Reset;
                }

            }

            public void Dispose()
                => Tcp.Dispose();

        }

        #endregion


        #region DrainedConnection_IsClosed_OnceItsRequestHasFinished

        /// <summary>
        /// A GOAWAY comes with a request in flight, which the server goes on to
        /// answer, and the server leaves the TCP connection open. The pool opens
        /// another connection at once, and leaves the old one to its request;
        /// once the response is in, it closes it.
        /// </summary>
        [Test]
        public async Task DrainedConnection_IsClosed_OnceItsRequestHasFinished()
        {

            await using var origin  = Origin.Start();
            await using var pool    = await HTTP2ClientPool.ConnectAsync("127.0.0.1", origin.Port, Cleartext: true, MaxConnections: 1);

            var first     = await origin.AcceptAsync();

            var response  = pool.SendRequestAsync(HTTPMethod.GET, URIScheme.http, origin.Authority, "/");
            var streamId  = await first.ReadRequestAsync();

            await first.SendAsync(HTTP2Frame.CreateGoAway(streamId, HTTP2ErrorCode.NO_ERROR, "going away"));

            // The replacement comes, and the old connection still serves its request.
            await origin.AcceptAsync();

            var whileInFlight  = await first.ReadToEndAsync(StaysOpenFor);
            var draining       = pool.DrainingConnectionCount;

            await first.RespondAsync(streamId);

            var status         = (await response.WaitAsync(EndTimeout)).Status;
            var end            = await first.ReadToEndAsync();

            Assert.Multiple(() => {
                Assert.That(whileInFlight,                                             Is.EqualTo(End.StillOpen),    "the connection stays open while its request is in flight");
                Assert.That(draining,                                                  Is.EqualTo(1),                "the connection drains meanwhile");
                Assert.That(status,                                                    Is.EqualTo(200),              "the request in flight is answered");
                Assert.That(end,                                                       Is.EqualTo(End.EndOfStream),  "the pool closes the connection once the request has finished");
                Assert.That(pool.DrainingConnectionCount,                              Is.EqualTo(0),                "nothing left draining");
                Assert.That(pool.ConnectionCount,                                      Is.EqualTo(1),                "the replacement serves on");
            });

        }

        #endregion

        #region DrainedConnection_WithNothingInFlight_IsClosedAtOnce

        /// <summary>
        /// A GOAWAY comes with no request in flight: there is nothing for the
        /// connection to finish, and the pool closes it at once.
        /// </summary>
        [Test]
        public async Task DrainedConnection_WithNothingInFlight_IsClosedAtOnce()
        {

            await using var origin  = Origin.Start();
            await using var pool    = await HTTP2ClientPool.ConnectAsync("127.0.0.1", origin.Port, Cleartext: true, MaxConnections: 1);

            var first   = await origin.AcceptAsync();

            await first.SendAsync(HTTP2Frame.CreateGoAway(0, HTTP2ErrorCode.NO_ERROR, "going away"));

            var second  = await origin.AcceptAsync();
            var end     = await first.ReadToEndAsync();

            // The replacement serves.
            var response  = pool.SendRequestAsync(HTTPMethod.GET, URIScheme.http, origin.Authority, "/");
            await second.RespondAsync(await second.ReadRequestAsync());

            var status    = (await response.WaitAsync(EndTimeout)).Status;

            Assert.Multiple(() => {
                Assert.That(end,                                                       Is.EqualTo(End.EndOfStream),  "the pool closes the connection");
                Assert.That(status,                                                    Is.EqualTo(200),              "the replacement serves");
                Assert.That(pool.DrainingConnectionCount,                              Is.EqualTo(0),                "nothing left draining");
            });

        }

        #endregion

        #region DisposeAsync_ClosesADrainingConnection

        /// <summary>
        /// A GOAWAY comes with a request in flight whose body is not all sent
        /// yet: the server has granted no send window. The server answers the
        /// request in full all the same (RFC 9113, Section 8.1), and the request
        /// is done, but its stream stays open for the rest of the body, which the
        /// server never asks for: the connection never drains. Disposing of the
        /// pool closes it.
        /// </summary>
        [Test]
        public async Task DisposeAsync_ClosesADrainingConnection()
        {

            await using var origin  = Origin.Start((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0));
            var pool                = await HTTP2ClientPool.ConnectAsync("127.0.0.1", origin.Port, Cleartext: true, MaxConnections: 1);

            try
            {

                var first     = await origin.AcceptAsync();

                var response  = pool.SendRequestAsync(HTTPMethod.POST, URIScheme.http, origin.Authority, "/", Body: "hello"u8.ToArray());
                var streamId  = await first.ReadRequestAsync();

                await first.SendAsync(HTTP2Frame.CreateGoAway(streamId, HTTP2ErrorCode.NO_ERROR, "going away"));
                await first.RespondAsync(streamId);

                var status         = (await response.WaitAsync(EndTimeout)).Status;

                await origin.AcceptAsync();

                var beforeDispose  = await first.ReadToEndAsync(StaysOpenFor);
                var draining       = pool.DrainingConnectionCount;

                await pool.DisposeAsync().AsTask().WaitAsync(EndTimeout);

                var end            = await first.ReadToEndAsync();

                Assert.Multiple(() => {
                    Assert.That(status,                                                Is.EqualTo(200),              "the request is answered");
                    Assert.That(beforeDispose,                                         Is.EqualTo(End.StillOpen),    "the connection stays open while the stream is");
                    Assert.That(draining,                                              Is.EqualTo(1),                "the connection drains meanwhile");
                    Assert.That(end,                                                   Is.EqualTo(End.EndOfStream),  "disposing of the pool closes the connection");
                    Assert.That(pool.DrainingConnectionCount,                          Is.EqualTo(0),                "nothing left draining");
                });

            }
            finally
            {
                await pool.DisposeAsync();
            }

        }

        #endregion

    }

}
