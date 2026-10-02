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
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Buffers.Binary;
using System.Diagnostics.Tracing;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The client closes its TCP connection when the HTTP/2 connection ends.
    ///
    /// It never did. HTTP2Client.ConnectAsync handed the stream over its socket
    /// to the connection, which nobody else held, and the connection only
    /// cancelled its token at its end. A cancelled read does not close a socket:
    /// the TCP connection stayed open until the GC finalized it, after
    /// CloseAsync, after a connection error — whose GOAWAY RFC 9113, Section
    /// 5.4.1 has the TCP connection closed after —, after the keepalive's
    /// teardown, and after the server closed its side, where our side sat in
    /// CLOSE_WAIT. A connect whose TLS handshake or start failed left its socket
    /// open as well.
    ///
    /// Now the connection closes a transport it owns at its end: one that
    /// HTTP2Client.ConnectAsync made, or one passed in with OwnsTransport. A
    /// stream the caller passed in and still owns stays open. After the GOAWAY
    /// of a connection error it reads what the server still sends for a moment
    /// before it closes, as the server does, so that the close goes out as a FIN
    /// behind the GOAWAY, not as a reset that may discard it.
    ///
    /// Each test runs a raw HTTP/2 peer on a loopback socket and reads what the
    /// client sends until the end of the stream: a FIN, a reset, or nothing
    /// within <see cref="EndTimeout"/>.
    /// </summary>
    [TestFixture]
    public class ClientTransportCloseTests
    {

        #region (helpers)

        /// <summary>
        /// How long the peer waits for the client to close its side, and the test
        /// for the client's connection to end.
        /// </summary>
        private static readonly TimeSpan EndTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The peer's certificate over TLS, made once: making one takes a while.
        /// </summary>
        private static readonly Lazy<X509Certificate2> Certificate = new (() => H2.MakeCert());

        /// <summary>
        /// How the client gets its transport: from HTTP2Client.ConnectAsync, in
        /// cleartext or over TLS, or as a stream over a socket the test made and
        /// passed in with OwnsTransport.
        /// </summary>
        public enum Transport
        {
            H2c,
            TLS,
            OwnedStream
        }

        /// <summary>
        /// How the peer's reading ended.
        /// </summary>
        public enum End
        {
            EndOfStream,
            Reset,
            StillOpen
        }

        /// <summary>
        /// A stream over another that notes whether it was disposed, and holds
        /// writes when told: as a socket does whose peer no longer reads.
        /// </summary>
        private sealed class RecordingStream(Stream Inner) : Stream
        {

            public Stream               Inner       { get; } = Inner;
            public Boolean              Disposed    { get; private set; }

            /// <summary>
            /// Whether a write waits until it is cancelled, rather than go out.
            /// </summary>
            public Boolean              HoldWrites  { get; set; }

            /// <summary>
            /// Whether a write waits until the stream is disposed, and then fails
            /// with an ObjectDisposedException, whatever its token says: as a
            /// send on a socket does that is closed under it.
            /// </summary>
            public Boolean              HoldWritesUntilDisposed { get; set; }

            /// <summary>
            /// Completes when a write is held.
            /// </summary>
            public TaskCompletionSource WriteHeld   { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource disposal = new (TaskCreationOptions.RunContinuationsAsynchronously);

            public override ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
                => Inner.ReadAsync(Buffer, CancellationToken);

            public override async ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
            {

                if (HoldWrites)
                {
                    WriteHeld.TrySetResult();
                    await Task.Delay(Timeout.Infinite, CancellationToken);
                }

                if (HoldWritesUntilDisposed)
                {
                    WriteHeld.TrySetResult();
                    await disposal.Task;
                }

                await Inner.WriteAsync(Buffer, CancellationToken);

            }

            public override Task<Int32> ReadAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
                => Inner.ReadAsync(Buffer, Offset, Count, CancellationToken);

            public override Task WriteAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
                => WriteAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

            public override Task FlushAsync(CancellationToken CancellationToken)
                => Inner.FlushAsync(CancellationToken);

            public override Int32 Read (Byte[] Buffer, Int32 Offset, Int32 Count) => Inner.Read (Buffer, Offset, Count);
            public override void  Write(Byte[] Buffer, Int32 Offset, Int32 Count) => Inner.Write(Buffer, Offset, Count);
            public override void  Flush() => Inner.Flush();

            public override Boolean  CanRead   => true;
            public override Boolean  CanWrite  => true;
            public override Boolean  CanSeek   => false;
            public override Int64    Length    => throw new NotSupportedException();
            public override Int64    Position  { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override Int64    Seek(Int64 Offset, SeekOrigin Origin) => throw new NotSupportedException();
            public override void     SetLength(Int64 Value)                => throw new NotSupportedException();

            protected override void Dispose(Boolean Disposing)
            {
                if (Disposing)
                {
                    Disposed = true;
                    disposal.TrySetException(new ObjectDisposedException(nameof(RecordingStream)));
                    Inner.Dispose();
                }
                base.Dispose(Disposing);
            }

        }

        /// <summary>
        /// The errors the stack's EventSource reports while this listener lives.
        /// </summary>
        private sealed class ConnectionErrors : EventListener
        {

            // Initialised with the declaration: the base constructor may already
            // call OnEventSourceCreated, and events may follow at once.
            private readonly List<String> payloads = [];

            protected override void OnEventSourceCreated(EventSource Source)
            {
                if (Source.Name == "Vanaheimr-Hermod-HTTP2")
                    EnableEvents(Source, EventLevel.Verbose);
            }

            protected override void OnEventWritten(EventWrittenEventArgs Event)
            {
                if (Event.EventName == "ConnectionError")
                    lock (payloads)
                        payloads.Add(String.Join(" ", Event.Payload ?? []));
            }

            /// <summary>
            /// The payload of every ConnectionError so far, joined by spaces.
            /// </summary>
            public List<String> All
            {
                get
                {
                    lock (payloads)
                        return [.. payloads];
                }
            }

        }

        /// <summary>
        /// The server side of one connection: a raw HTTP/2 peer on a loopback
        /// socket that has sent its SETTINGS and does nothing else unless told.
        /// </summary>
        private sealed class Peer : IAsyncDisposable
        {

            private readonly TcpListener  listener;
            private readonly TcpClient    tcp;

            /// <summary>
            /// The peer's side of the connection.
            /// </summary>
            public Stream                 Stream     { get; }

            /// <summary>
            /// The client's stream, for <see cref="Transport.OwnedStream"/> and
            /// for a stream the caller owns.
            /// </summary>
            public RecordingStream?       Recorded   { get; }

            private Peer(TcpListener Listener, TcpClient Tcp, Stream Stream, RecordingStream? Recorded)
            {
                this.listener  = Listener;
                this.tcp       = Tcp;
                this.Stream    = Stream;
                this.Recorded  = Recorded;
            }

            /// <summary>
            /// Accept one connection on a loopback port and hand it to
            /// <paramref name="Server"/>, while <paramref name="Connect"/> connects
            /// the client to that port.
            /// </summary>
            private static async Task<(T Client, Peer Peer)> RunAsync<T>(Boolean                                 Tls,
                                                                         Func<Int32, Task<(T, RecordingStream?)>>  Connect,
                                                                         Func<Stream, Task>                      Server,
                                                                         Boolean                                 Alpn = true)
            {

                var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
                listener.Start();

                var port      = ((IPEndPoint) listener.LocalEndpoint).Port;
                var accepted  = listener.AcceptTcpClientAsync();
                var client    = Connect(port);

                var tcp       = await accepted.WaitAsync(EndTimeout);
                Stream stream = tcp.GetStream();

                if (Tls)
                {

                    var ssl = new SslStream(stream, false);

                    await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                              ServerCertificate     = Certificate.Value,
                              ApplicationProtocols  = Alpn ? [SslApplicationProtocol.Http2] : null
                          }).WaitAsync(EndTimeout);

                    stream = ssl;

                }

                await Server(stream);

                var (connection, recorded) = await client.WaitAsync(EndTimeout);

                return (connection, new Peer(listener, tcp, stream, recorded));

            }

            /// <summary>
            /// Connect a client by <paramref name="How"/> to a peer that answers
            /// its preface with an empty SETTINGS.
            /// </summary>
            public static Task<(HTTP2ClientConnection Client, Peer Peer)> ConnectAsync(Transport            How,
                                                                                       HTTP2ClientOptions?  Options = null)

                => RunAsync(How == Transport.TLS,
                            async port => How switch {

                                Transport.H2c          => (await HTTP2Client.ConnectAsync("127.0.0.1", port, Options: Options, Cleartext: true), null),

                                Transport.TLS          => (await HTTP2Client.ConnectAsync("127.0.0.1", port, H2.AcceptAnyServerCert, Options: Options), null),

                                Transport.OwnedStream  => await StartOnStreamAsync(port, Options, OwnsTransport: true),

                                _                      => throw new ArgumentException($"No such transport: {How}", nameof(How))

                            },
                            AnswerPrefaceAsync);

            /// <summary>
            /// Connect a client over a stream the test owns, as callers of the
            /// constructor have so far: without OwnsTransport.
            /// </summary>
            public static Task<(HTTP2ClientConnection Client, Peer Peer)> ConnectCallerOwnedAsync()

                => RunAsync(false,
                            port => StartOnStreamAsync(port, null, OwnsTransport: false),
                            AnswerPrefaceAsync);

            /// <summary>
            /// Connect by HTTP2Client.ConnectAsync to a peer that does what
            /// <paramref name="Server"/> does, and return how the connect failed.
            /// </summary>
            public static async Task<(Exception? Failure, Peer Peer)> FailToConnectAsync(Boolean                                       Tls,
                                                                                         Func<Stream, Task>                            Server,
                                                                                         Func<Int32, Task<HTTP2ClientConnection>>     Connect,
                                                                                         Boolean                                       Alpn = true)
            {

                var (failure, peer) = await RunAsync(Tls,
                                                     async port => {
                                                         try
                                                         {
                                                             await Connect(port);
                                                             return ((Exception?) null, (RecordingStream?) null);
                                                         }
                                                         catch (Exception e)
                                                         {
                                                             return (e, null);
                                                         }
                                                     },
                                                     Server,
                                                     Alpn);

                return (failure, peer);

            }

            private static async Task<(HTTP2ClientConnection, RecordingStream?)> StartOnStreamAsync(Int32 Port, HTTP2ClientOptions? Options, Boolean OwnsTransport)
            {

                var tcp = new TcpClient { NoDelay = true };
                await tcp.ConnectAsync(System.Net.IPAddress.Loopback, Port);

                var recorded    = new RecordingStream(tcp.GetStream());
                var connection  = new HTTP2ClientConnection(recorded, Options, OwnsTransport: OwnsTransport);

                await connection.StartAsync();

                return (connection, recorded);

            }

            /// <summary>
            /// Read the client's preface, and answer it with an empty SETTINGS.
            /// </summary>
            public static async Task AnswerPrefaceAsync(Stream Stream)
            {
                await ReadPrefaceAsync(Stream);
                await Stream.WriteAsync(HTTP2Frame.CreateSettings().Serialize());
                await Stream.FlushAsync();
            }

            /// <summary>
            /// Read the client's preface, and answer nothing.
            /// </summary>
            public static async Task ReadPrefaceAsync(Stream Stream)
            {
                using var timeout = new CancellationTokenSource(EndTimeout);
                var preface = new Byte[H2Raw.Preface.Length];
                Assert.That(await H2Raw.ReadExactAsync(Stream, preface, timeout.Token), Is.True, "the client's preface");
            }

            /// <summary>
            /// Send <paramref name="Frames"/> in one write.
            /// </summary>
            public async Task SendAsync(params HTTP2Frame[] Frames)
            {
                await Stream.WriteAsync(Frames.SelectMany(frame => frame.Serialize()).ToArray());
                await Stream.FlushAsync();
            }

            /// <summary>
            /// Close the peer's sending side: a close_notify over TLS, and a FIN.
            /// </summary>
            public async Task EndSendingAsync()
            {
                if (Stream is SslStream ssl)
                    await ssl.ShutdownAsync();
                tcp.Client.Shutdown(SocketShutdown.Send);
            }

            /// <summary>
            /// Read the frames the client sends up to the first of type
            /// <paramref name="Type"/>, and return them; null if the stream ends,
            /// or none comes within <see cref="EndTimeout"/>.
            /// </summary>
            public async Task<List<HTTP2Frame>?> ReadUpToAsync(HTTP2FrameType Type)
            {

                var frames = new List<HTTP2Frame>();

                using var timeout = new CancellationTokenSource(EndTimeout);

                try
                {
                    while (true)
                    {

                        var frame = await H2Raw.ReadFrameAsync(Stream, timeout.Token);
                        if (frame is null)
                            return null;

                        frames.Add(frame);

                        if (frame.Type == Type)
                            return frames;

                    }
                }
                catch (Exception e) when (e is OperationCanceledException or IOException)
                {
                    return null;
                }

            }

            /// <summary>
            /// Read the frames the client sends, until the end of the stream, a
            /// reset, or nothing more within <paramref name="Timeout"/>.
            /// </summary>
            public async Task<(List<HTTP2Frame> Frames, End End)> ReadToEndAsync(TimeSpan? Timeout = null)
            {

                var frames = new List<HTTP2Frame>();

                using var timeout = new CancellationTokenSource(Timeout ?? EndTimeout);

                try
                {
                    while (true)
                    {

                        var frame = await H2Raw.ReadFrameAsync(Stream, timeout.Token);
                        if (frame is null)
                            return (frames, End.EndOfStream);

                        frames.Add(frame);

                    }
                }
                catch (OperationCanceledException)
                {
                    return (frames, End.StillOpen);
                }
                catch (IOException)
                {
                    return (frames, End.Reset);
                }

            }

            public async ValueTask DisposeAsync()
            {
                try { await Stream.DisposeAsync(); } catch { }
                tcp.Dispose();
                listener.Stop();
            }

        }

        /// <summary>
        /// A frame as the peer reads it: its type, and for a GOAWAY its error code.
        /// </summary>
        private static String Describe(HTTP2Frame Frame)

            => Frame.Type == HTTP2FrameType.GOAWAY
                   ? $"GOAWAY {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload.AsSpan(4, 4))}"
                   : Frame.Type.ToString();

        /// <summary>
        /// Whether <paramref name="Task"/> completes within <see cref="EndTimeout"/>.
        /// </summary>
        private static async Task<Boolean> EndsAsync(Task Task)
            => await System.Threading.Tasks.Task.WhenAny(Task, System.Threading.Tasks.Task.Delay(EndTimeout)) == Task;

        /// <summary>
        /// A PUSH_PROMISE, which the client must answer with a connection error
        /// of type PROTOCOL_ERROR: it advertised ENABLE_PUSH=0.
        /// </summary>
        private static HTTP2Frame PushPromise

            => new () {
                   Type      = HTTP2FrameType.PUSH_PROMISE,
                   Flags     = HTTP2FrameFlags.END_HEADERS,
                   StreamId  = 1,
                   Payload   = [0, 0, 0, 2]
               };

        #endregion


        #region CloseAsync_ClosesTheConnection

        /// <summary>
        /// CloseAsync sends a GOAWAY NO_ERROR and closes the TCP connection, and
        /// the connection has ended by the time it returns.
        /// </summary>
        [Test]
        public async Task CloseAsync_ClosesTheConnection([Values] Transport How)
        {

            var (client, peer) = await Peer.ConnectAsync(How);
            await using var _ = peer;

            var closing = client.CloseAsync();
            Assert.That(await EndsAsync(closing), Is.True, "CloseAsync returned");

            var closedOnReturn    = client.Closed.IsCompleted;
            var disposedOnReturn  = peer.Recorded?.Disposed ?? true;

            var (frames, end) = await peer.ReadToEndAsync();

            Assert.Multiple(() => {
                Assert.That(frames.Select(Describe).LastOrDefault(),  Is.EqualTo("GOAWAY NO_ERROR"),  "the client's last frame");
                Assert.That(end,                                      Is.EqualTo(End.EndOfStream),    "the TCP connection");
                Assert.That(closedOnReturn,                           Is.True,                        "Closed, once CloseAsync returned");
                Assert.That(disposedOnReturn,                         Is.True,                        "the client's stream disposed, once CloseAsync returned");
            });

        }

        #endregion

        #region CloseAsync_BehindAWriteThatDoesNotEnd_Returns

        /// <summary>
        /// A write holds the write lock and does not end — the server no longer
        /// reads, say. CloseAsync's GOAWAY waited for the lock with the
        /// connection's token, which it cancels only after the GOAWAY: it never
        /// returned. Now the GOAWAY is given up after a second (see
        /// SendGoAwayAsync), and the connection ends and closes its transport.
        /// </summary>
        [Test]
        public async Task CloseAsync_BehindAWriteThatDoesNotEnd_Returns()
        {

            var (client, peer) = await Peer.ConnectAsync(Transport.OwnedStream);
            await using var _ = peer;

            var recorded = peer.Recorded!;
            recorded.HoldWrites = true;

            var request = client.SendRequestAsync(HTTPMethod.GET, URIScheme.http, "127.0.0.1", "/");

            Assert.That(await EndsAsync(recorded.WriteHeld.Task), Is.True, "the request's HEADERS held");

            var closing         = client.CloseAsync();
            var closingReturns  = await EndsAsync(closing);

            var (frames, end)   = await peer.ReadToEndAsync();
            var requestEnds     = await EndsAsync(request);

            Assert.Multiple(() => {
                Assert.That(closingReturns,                           Is.True,                        "CloseAsync returned");
                Assert.That(frames.Select(Describe),                  Does.Not.Contain("HEADERS"),    "the held write went out");
                Assert.That(end,                                      Is.EqualTo(End.EndOfStream),    "the TCP connection");
                Assert.That(recorded.Disposed,                        Is.True,                        "the client's stream disposed");
                Assert.That(requestEnds,                              Is.True,                        "the request ended");
            });

        }

        #endregion

        #region CloseAsync_DuringAnUpload_IsNoWriterLoopFailure

        /// <summary>
        /// The writer loop is sending a request body's DATA when CloseAsync
        /// closes the transport under it. The write fails with what the transport
        /// says once closed — an ObjectDisposedException here — rather than
        /// with the cancellation, and the writer loop took that for a failure of
        /// its own: it reported a connection error WRITER_LOOP for a connection
        /// that was only being closed. Once the connection is cancelled, a
        /// failed write is its end, as a cancelled one is.
        /// </summary>
        [Test]
        public async Task CloseAsync_DuringAnUpload_IsNoWriterLoopFailure()
        {

            using var errors = new ConnectionErrors();

            var (client, peer) = await Peer.ConnectAsync(Transport.OwnedStream);
            await using var _ = peer;

            var recorded = peer.Recorded!;

            var upload = await client.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "127.0.0.1", "/upload").
                                      WaitAsync(EndTimeout);

            recorded.HoldWritesUntilDisposed = true;

            var writing = upload.WriteAsync(Encoding.ASCII.GetBytes("a body the server will not get"));

            Assert.That(await EndsAsync(recorded.WriteHeld.Task), Is.True, "the DATA write held");

            var closingReturns  = await EndsAsync(client.CloseAsync());
            var writingEnds     = await EndsAsync(writing);

            Assert.Multiple(() => {
                Assert.That(closingReturns,                           Is.True,                        "CloseAsync returned");
                Assert.That(recorded.Disposed,                        Is.True,                        "the client's stream disposed");
                Assert.That(writingEnds,                              Is.True,                        "the body's write ended");
                Assert.That(errors.All,                               Has.None.StartsWith("WRITER_LOOP"),
                                                                                                      "a connection error of the writer loop reported");
            });

        }

        #endregion

        #region ConnectionError_ClosesTheConnectionAfterItsGoAway

        /// <summary>
        /// RFC 9113, Section 5.4.1: after the GOAWAY of a connection error, the
        /// TCP connection is closed.
        /// </summary>
        [Test]
        public async Task ConnectionError_ClosesTheConnectionAfterItsGoAway([Values] Transport How)
        {

            var (client, peer) = await Peer.ConnectAsync(How);
            await using var _ = peer;

            await peer.SendAsync(PushPromise);

            var (frames, end)  = await peer.ReadToEndAsync();
            var closed         = await EndsAsync(client.Closed);

            Assert.Multiple(() => {
                Assert.That(frames.Select(Describe).LastOrDefault(),  Is.EqualTo("GOAWAY PROTOCOL_ERROR"),  "the client's last frame");
                Assert.That(end,                                      Is.EqualTo(End.EndOfStream),          "the TCP connection");
                Assert.That(closed,                                   Is.True,                              "Closed");
                Assert.That(peer.Recorded?.Disposed ?? true,          Is.True,                              "the client's stream disposed");
            });

        }

        #endregion

        #region ConnectionError_FramesStillComing_CloseIsNotAReset

        /// <summary>
        /// The server goes on sending after the frame that ends the connection,
        /// unaware: here once the client's GOAWAY is out. A socket closed with
        /// that unread, or with it still coming, sends a reset, and a reset may
        /// discard the GOAWAY on the server's side before it is read. The client
        /// reads what comes for a moment before it closes, as the server does, so
        /// that its close is a FIN behind the GOAWAY. The peer reads on only once
        /// the client's connection has ended, so that a reset would be there
        /// first.
        /// </summary>
        [Test]
        public async Task ConnectionError_FramesStillComing_CloseIsNotAReset([Values] Transport How)
        {

            var (client, peer) = await Peer.ConnectAsync(How);
            await using var _ = peer;

            await peer.SendAsync(PushPromise);

            var upToGoAway = await peer.ReadUpToAsync(HTTP2FrameType.GOAWAY);

            await peer.SendAsync([ .. Enumerable.Range(0, 64).Select(i => HTTP2Frame.CreatePing(new Byte[8])) ]);

            var closed     = await EndsAsync(client.Closed);

            var (_, end)   = await peer.ReadToEndAsync();

            Assert.Multiple(() => {
                Assert.That(upToGoAway?.Select(Describe).Last(),      Is.EqualTo("GOAWAY PROTOCOL_ERROR"),  "the client's GOAWAY");
                Assert.That(closed,                                   Is.True,                              "Closed");
                Assert.That(end,                                      Is.EqualTo(End.EndOfStream),          "the TCP connection");
            });

        }

        #endregion

        #region ServerEndOfStream_ClosesTheConnection

        /// <summary>
        /// The server closes its side: the client's connection ends, and closes
        /// its side too, rather than sit in CLOSE_WAIT.
        /// </summary>
        [Test]
        public async Task ServerEndOfStream_ClosesTheConnection([Values] Transport How)
        {

            var (client, peer) = await Peer.ConnectAsync(How);
            await using var _ = peer;

            await peer.EndSendingAsync();

            var (_, end)  = await peer.ReadToEndAsync();
            var closed    = await EndsAsync(client.Closed);

            Assert.Multiple(() => {
                Assert.That(end,                                      Is.EqualTo(End.EndOfStream),  "the TCP connection");
                Assert.That(closed,                                   Is.True,                      "Closed");
                Assert.That(peer.Recorded?.Disposed ?? true,          Is.True,                      "the client's stream disposed");
            });

        }

        #endregion

        #region KeepAliveTeardown_ClosesTheConnection

        /// <summary>
        /// The server does not answer the keepalive's PING: the client tears the
        /// connection down, and closes the TCP connection.
        /// </summary>
        [Test]
        public async Task KeepAliveTeardown_ClosesTheConnection([Values] Transport How)
        {

            var (client, peer) = await Peer.ConnectAsync(How, new HTTP2ClientOptions {
                                                                  KeepAliveInterval  = TimeSpan.FromMilliseconds(100),
                                                                  KeepAliveTimeout   = TimeSpan.FromMilliseconds(200)
                                                              });
            await using var _ = peer;

            var (frames, end)  = await peer.ReadToEndAsync();
            var closed         = await EndsAsync(client.Closed);

            Assert.Multiple(() => {
                Assert.That(frames.Select(Describe),                  Does.Contain("PING"),         "the keepalive's PING");
                Assert.That(end,                                      Is.EqualTo(End.EndOfStream),  "the TCP connection");
                Assert.That(closed,                                   Is.True,                      "Closed");
                Assert.That(peer.Recorded?.Disposed ?? true,          Is.True,                      "the client's stream disposed");
            });

        }

        #endregion

        #region FailedConnect_ClosesTheSocket

        /// <summary>
        /// HTTP2Client.ConnectAsync fails after it opened the socket: the server
        /// sends no SETTINGS until the caller gives up — once the peer has read
        /// the client's preface, SETTINGS and WINDOW_UPDATE, so that the client
        /// waits for the server's SETTINGS by then —, speaks no TLS, or
        /// negotiates no h2. The socket is closed, not left to the GC: with a FIN,
        /// or with a reset where the client leaves the server's answer unread.
        /// </summary>
        [Test]
        public async Task FailedConnect_ClosesTheSocket([Values("no SETTINGS (h2c)", "no SETTINGS (TLS)", "no TLS", "no ALPN h2")] String How)
        {

            using var giveUp = new CancellationTokenSource();

            async Task ReadStartThenGiveUp(Stream Stream)
            {

                await Peer.ReadPrefaceAsync(Stream);

                using var timeout = new CancellationTokenSource(EndTimeout);
                await H2Raw.ReadFrameAsync(Stream, timeout.Token);
                await H2Raw.ReadFrameAsync(Stream, timeout.Token);

                giveUp.Cancel();

            }

            var (failure, peer) = How switch {

                "no SETTINGS (h2c)"  => await Peer.FailToConnectAsync(false,
                                                                       ReadStartThenGiveUp,
                                                                       port => HTTP2Client.ConnectAsync("127.0.0.1", port, Cleartext: true, CancellationToken: giveUp.Token)),

                "no SETTINGS (TLS)"  => await Peer.FailToConnectAsync(true,
                                                                       ReadStartThenGiveUp,
                                                                       port => HTTP2Client.ConnectAsync("127.0.0.1", port, H2.AcceptAnyServerCert, CancellationToken: giveUp.Token)),

                // An HTTP/1.1 server's answer to a ClientHello.
                "no TLS"             => await Peer.FailToConnectAsync(false,
                                                                       async stream => {
                                                                           await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"));
                                                                           await stream.FlushAsync();
                                                                       },
                                                                       port => HTTP2Client.ConnectAsync("127.0.0.1", port, H2.AcceptAnyServerCert)),

                "no ALPN h2"         => await Peer.FailToConnectAsync(true,
                                                                       _ => Task.CompletedTask,
                                                                       port => HTTP2Client.ConnectAsync("127.0.0.1", port, H2.AcceptAnyServerCert),
                                                                       Alpn: false),

                _                    => throw new ArgumentException($"No such failure: {How}", nameof(How))

            };
            await using var _ = peer;

            Assert.That(failure, Is.Not.Null, "the connect failed");

            var (_, end) = await peer.ReadToEndAsync();

            Assert.That(end, Is.Not.EqualTo(End.StillOpen), "the TCP connection");

        }

        #endregion

        #region CallerOwnedStream_StaysOpen

        /// <summary>
        /// A stream passed in without OwnsTransport stays open, as it always did,
        /// however the connection ends: the caller owns it. The client writes a
        /// PING through it after the connection ended, and the peer reads it.
        /// </summary>
        [Test]
        public async Task CallerOwnedStream_StaysOpen([Values("CloseAsync", "connection error", "server's end of stream")] String How)
        {

            var (client, peer) = await Peer.ConnectCallerOwnedAsync();
            await using var _ = peer;

            switch (How)
            {

                case "CloseAsync":
                    await client.CloseAsync();
                    break;

                case "connection error":
                    await peer.SendAsync(PushPromise);
                    break;

                case "server's end of stream":
                    await peer.EndSendingAsync();
                    break;

            }

            Assert.That(await EndsAsync(client.Closed), Is.True, "Closed");

            var recorded = peer.Recorded!;

            await recorded.Inner.WriteAsync(HTTP2Frame.CreatePing(new Byte[8]).Serialize());
            await recorded.Inner.FlushAsync();

            var (frames, end) = await peer.ReadToEndAsync(TimeSpan.FromMilliseconds(500));

            Assert.Multiple(() => {
                Assert.That(frames.Select(Describe).LastOrDefault(),  Is.EqualTo("PING"),           "the frame written after the end");
                Assert.That(end,                                      Is.EqualTo(End.StillOpen),    "the TCP connection");
                Assert.That(recorded.Disposed,                        Is.False,                     "the caller's stream disposed");
            });

        }

        #endregion

    }

}
