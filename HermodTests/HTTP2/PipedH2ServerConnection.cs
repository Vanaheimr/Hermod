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

using System.Text;
using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using System.Diagnostics;
using System.IO.Pipelines;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// A server <see cref="HTTP2Connection"/> on in-memory pipes, whose client the
    /// test plays by hand, frame by frame. It puts in order two things that
    /// otherwise only the thread scheduler orders: the server's half-close of a
    /// stream, right after it wrote the frame that ends its side, and a reset of
    /// that stream — see <see cref="ResetBeforeHalfCloseAsync"/>. Likewise the
    /// read loop's handling of a DATA frame, and a reset made on another task —
    /// see <see cref="HoldDataAfterStateCheckAsync"/> and
    /// <see cref="ResetAfterStateCheckAsync"/> — and a frame the server has
    /// decided to send, and a reset that comes before it goes out — see
    /// <see cref="HoldWritesAsync"/>. And a chunk the writer loop has taken, and
    /// a reset whose RST_STREAM goes out before it — see
    /// <see cref="HoldTakenDataAsync"/>.
    ///
    /// Every write of the server completes here synchronously, so the thread that
    /// wrote the frame ending a stream goes on from that write to the half-close
    /// without a hop, and <see cref="EndOfStreamWritten"/> names that thread.
    /// </summary>
    internal sealed class PipedH2ServerConnection : IAsyncDisposable
    {

        /// <summary>
        /// How long any single step may take before the test gives up — every
        /// step is in memory, so this only bounds a test that went wrong.
        /// </summary>
        public static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

        private readonly Pipe                     clientToServer  = new(new PipeOptions(useSynchronizationContext: false));

        // Never full: a write that had to wait for room would finish on another
        // thread, and the thread named to the test would not be the one that
        // goes on to the half-close.
        private readonly Pipe                     serverToClient  = new(new PipeOptions(pauseWriterThreshold:      0,
                                                                                        resumeWriterThreshold:     0,
                                                                                        useSynchronizationContext: false));
        private readonly Stream                   toServer;
        private readonly Stream                   fromServer;
        private readonly HPACKEncoder             encoder         = new();
        private readonly HPACKDecoder             decoder         = new();
        private readonly CancellationTokenSource  cancellation    = new();

        private readonly Lock                                              sync     = new();
        private readonly Dictionary<UInt32, TaskCompletionSource<Thread>>  watched  = [];
        private readonly List<ReceivedFrame>                               received = [];

        private HTTP2Connection?  connection;
        private Task?             running;
        private MemoryStream?     headerBlock;

        private (UInt32 StreamId, Exception Failure)?  dataWriteFailure;

        /// <summary>
        /// The thread that last read from the client — see
        /// <see cref="HoldDataAfterStateCheckAsync"/>.
        /// </summary>
        private volatile Thread?  lastReader;

        /// <summary>
        /// The stream the <see cref="HTTP2Connection"/> runs on.
        /// </summary>
        private Stream            Server          { get; }

        /// <summary>
        /// The server's connection.
        /// </summary>
        public HTTP2Connection    Connection
            => connection!;

        /// <summary>
        /// Completes once the connection has ended by itself, or by the test's
        /// end, and the server has closed its end.
        /// </summary>
        public Task               Ended
            => running!;


        /// <summary>
        /// A frame the server sent, with its header block decoded if it ends one.
        /// </summary>
        public sealed record ReceivedFrame(HTTP2Frame Frame, List<(String Name, String Value)>? Headers)
        {

            public override String ToString()

                => Frame.Type + (Headers is null ? "" : " " + String.Join(", ", Headers.Select(header => header.Name + " " + header.Value))) +
                                 (Frame.Type == HTTP2FrameType.DATA ? $" \"{Encoding.ASCII.GetString(Frame.Payload)}\"" : "") +
                                 (Frame.Type is HTTP2FrameType.DATA or HTTP2FrameType.HEADERS && Frame.EndStream ? " END_STREAM" : "") +
                                 (Frame.Type == HTTP2FrameType.RST_STREAM ? $" {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload)}" : "") +
                                 (Frame.Type == HTTP2FrameType.GOAWAY     ? $" {(HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload.AsSpan(4))}" +
                                                                            $" last {BinaryPrimitives.ReadUInt32BigEndian(Frame.Payload) & 0x7FFFFFFF}" : "");

        }

        /// <summary>
        /// A response as the client read it: its header fields, its body and its
        /// trailer fields, if it had any.
        /// </summary>
        public sealed record Response(List<(String Name, String Value)>   Headers,
                                      String                              Body,
                                      List<(String Name, String Value)>?  Trailers)
        {
            public String? Status
                => Headers.FirstOrDefault(header => header.Name == ":status").Value;
        }


        private PipedH2ServerConnection()
        {
            this.toServer    = clientToServer.Writer.AsStream();
            this.fromServer  = serverToClient.Reader.AsStream();
            this.Server      = new ServerSide(this);
        }


        #region StartAsync(RequestHandler, StreamingHandler = null, ConnectHandler = null, IsAuthorityServed = null, ConnectionWindowSize = null)

        /// <summary>
        /// Start a server connection with these handlers, and return once the
        /// client side has completed the connection preface. With
        /// <paramref name="IsAuthorityServed"/>, the server answers a request for
        /// an origin it refuses with 421. With <paramref name="ConnectionWindowSize"/>,
        /// it grants the client that connection window instead of its default.
        /// </summary>
        public static async Task<PipedH2ServerConnection> StartAsync(HTTP2RequestHandler     RequestHandler,
                                                                     HTTP2StreamingHandler?  StreamingHandler      = null,
                                                                     HTTP2ConnectHandler?    ConnectHandler        = null,
                                                                     Func<String, Boolean>?  IsAuthorityServed     = null,
                                                                     Int32?                  ConnectionWindowSize  = null)
        {

            var peer = new PipedH2ServerConnection();

            peer.connection = new HTTP2Connection(peer.Server,
                                                  RequestHandler,
                                                  ConnectHandler:        ConnectHandler,
                                                  CancellationToken:     peer.cancellation.Token,
                                                  StreamingHandler:      StreamingHandler,
                                                  IsAuthorityServed:     IsAuthorityServed,
                                                  ConnectionWindowSize:  ConnectionWindowSize ?? HTTP2FlowControl.DefaultConnectionWindowSize);

            // On the thread pool, not the test's thread: the connection's loops
            // would otherwise capture NUnit's SynchronizationContext. Once the
            // connection has ended, the server closes its end, as a server
            // closes the socket then, and the client reads the end of the stream.
            peer.running = Task.Run(async () => {

                               try
                               {
                                   await peer.connection.RunAsync();
                               }
                               finally
                               {
                                   await peer.serverToClient.Writer.CompleteAsync();
                               }

                           });

            await peer.toServer.WriteAsync(H2Raw.Preface);
            await peer.SendAsync(HTTP2Frame.CreateSettings());

            var settings     = false;
            var settingsAck  = false;

            while (!settings || !settingsAck)
            {

                var frame = (await peer.NextFrameAsync()).Frame;

                if (frame.Type == HTTP2FrameType.SETTINGS && !frame.IsAck)
                {
                    settings = true;
                    await peer.SendAsync(HTTP2Frame.CreateSettingsAck());
                }

                if (frame.Type == HTTP2FrameType.SETTINGS && frame.IsAck)
                    settingsAck = true;

            }

            return peer;

        }

        #endregion


        #region Client side

        /// <summary>
        /// Send a frame to the server.
        /// </summary>
        public async Task SendAsync(HTTP2Frame Frame)
        {
            await toServer.WriteAsync(Frame.Serialize());
            await toServer.FlushAsync();
        }

        /// <summary>
        /// Send a GET for <paramref name="Path"/> that ends the stream, or,
        /// unless <paramref name="EndStream"/>, leaves the client's side open.
        /// </summary>
        public Task RequestAsync(UInt32 StreamId, String Path, Boolean EndStream = true)

            => SendAsync(HTTP2Frame.CreateHeaders(StreamId,
                                                  encoder.EncodeHeaderBlock([(":method",    "GET"),
                                                                             (":scheme",    "http"),
                                                                             (":authority", "localhost"),
                                                                             (":path",      Path)]),
                                                  EndStream:  EndStream,
                                                  EndHeaders: true));

        /// <summary>
        /// Send a CONNECT for a tunnel to <paramref name="Authority"/>, a host and
        /// port (RFC 9113, Section 8.5). It leaves the client's side open, as a
        /// tunnel's stays until one side ends it.
        /// </summary>
        public Task RequestTunnelAsync(UInt32 StreamId, String Authority)

            => SendAsync(HTTP2Frame.CreateHeaders(StreamId,
                                                  encoder.EncodeHeaderBlock([(":method",    "CONNECT"),
                                                                             (":authority", Authority)]),
                                                  EndStream:  false,
                                                  EndHeaders: true));

        /// <summary>
        /// Send a header block of these fields, a request's or trailers, encoded
        /// as every other block the client sends here: in one HEADERS frame, or,
        /// given <paramref name="FragmentSize"/>, split into a HEADERS frame and
        /// CONTINUATION frames that carry at most that many bytes of it each.
        /// </summary>
        public async Task SendHeadersAsync(UInt32                             StreamId,
                                           List<(String Name, String Value)>  Fields,
                                           Boolean                            EndStream,
                                           Int32?                             FragmentSize   = null)
        {

            var block  = encoder.EncodeHeaderBlock(Fields);
            var size   = FragmentSize ?? Math.Max(block.Length, 1);

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size, nameof(FragmentSize));

            var first  = block[..Math.Min(size, block.Length)];

            await SendAsync(HTTP2Frame.CreateHeaders(StreamId,
                                                     first,
                                                     EndStream:  EndStream,
                                                     EndHeaders: first.Length == block.Length));

            for (var offset = first.Length; offset < block.Length; offset += size)
            {

                var fragment = block[offset..Math.Min(offset + size, block.Length)];

                await SendAsync(new HTTP2Frame {
                                    Type      = HTTP2FrameType.CONTINUATION,
                                    Flags     = offset + fragment.Length == block.Length
                                                    ? HTTP2FrameFlags.END_HEADERS
                                                    : HTTP2FrameFlags.NONE,
                                    StreamId  = StreamId,
                                    Length    = (UInt32) fragment.Length,
                                    Payload   = fragment
                                });

            }

        }

        /// <summary>
        /// Ping the server and read up to its answer. The read loop handles one
        /// frame at a time, so once the answer is read, the server has handled
        /// every frame sent before the ping, and everything it wrote before
        /// answering has been read.
        /// </summary>
        public async Task PingAsync()
        {

            var opaque = Guid.NewGuid().ToByteArray()[..8];

            await SendAsync(HTTP2Frame.CreatePing(opaque));

            while (true)
            {

                var frame = (await NextFrameAsync()).Frame;

                if (frame.Type == HTTP2FrameType.PING && frame.IsAck && frame.Payload.AsSpan().SequenceEqual(opaque))
                    return;

            }

        }

        /// <summary>
        /// Read on until the server has ended its side of the stream, and return
        /// the response it sent there — or null if it did not end it in time.
        /// </summary>
        public async Task<Response?> TryResponseAsync(UInt32 StreamId, TimeSpan Timeout)
        {

            using var timeout = new CancellationTokenSource(Timeout);

            try
            {

                while (true)
                {

                    var frame = (await NextFrameAsync(timeout.Token)).Frame;

                    if (frame.StreamId != StreamId)
                        continue;

                    if (EndsStream(frame))
                        break;

                    if (frame.Type == HTTP2FrameType.RST_STREAM)
                        return null;

                }

            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return null;
            }

            var blocks  = FramesOn(StreamId).Where(frame => frame.Headers is not null).ToList();
            var body    = String.Concat(FramesOn(StreamId).Where (frame => frame.Frame.Type == HTTP2FrameType.DATA).
                                                           Select(frame => Encoding.ASCII.GetString(frame.Frame.Payload)));

            return new Response(blocks[0].Headers!,
                                body,
                                blocks.Count > 1 ? blocks[^1].Headers : null);

        }

        /// <summary>
        /// Read on until the server resets the stream, past any end of its side
        /// it sent first, and return the error code of its RST_STREAM.
        /// </summary>
        public async Task<HTTP2ErrorCode> ReadToResetAsync(UInt32 StreamId)
        {

            while (true)
            {

                var frame = (await NextFrameAsync()).Frame;

                if (frame.StreamId == StreamId && frame.Type == HTTP2FrameType.RST_STREAM)
                    return (HTTP2ErrorCode) BinaryPrimitives.ReadUInt32BigEndian(frame.Payload);

            }

        }

        /// <summary>
        /// Read on until the server has closed its end of the connection.
        /// </summary>
        public async Task ReadToEndAsync()
        {
            try
            {
                while (true)
                    await NextFrameAsync();
            }
            catch (EndOfStreamException)
            { }
        }

        /// <summary>
        /// Close the client's end, as a client that goes away without a word: the
        /// server reads the end of its stream, and ends the connection.
        /// </summary>
        public ValueTask DisconnectAsync()

            => clientToServer.Writer.CompleteAsync();

        /// <summary>
        /// Every frame read so far on this stream, in order.
        /// </summary>
        public List<ReceivedFrame> FramesOn(UInt32 StreamId)
        {
            lock (sync)
                return [.. received.Where(frame => frame.Frame.StreamId == StreamId)];
        }

        /// <summary>
        /// Read the next frame the server sent, decoding each header block it
        /// ends: the server's HPACK encoder has already put its fields into the
        /// dynamic table, and skipping a block would put ours out of step.
        /// </summary>
        private async Task<ReceivedFrame> NextFrameAsync(CancellationToken CancellationToken = default)
        {

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            timeout.CancelAfter(StepTimeout);

            var frame = await H2Raw.ReadFrameAsync(fromServer, timeout.Token)
                            ?? throw new EndOfStreamException("The server closed the connection");

            List<(String Name, String Value)>? headers = null;

            if (frame.Type is HTTP2FrameType.HEADERS or HTTP2FrameType.CONTINUATION)
            {

                if (frame.Type == HTTP2FrameType.HEADERS)
                    headerBlock = new MemoryStream();

                headerBlock!.Write(frame.Payload);

                if (frame.EndHeaders)
                {
                    headers      = decoder.DecodeHeaderBlock(headerBlock.ToArray());
                    headerBlock  = null;
                }

            }

            var receivedFrame = new ReceivedFrame(frame, headers);

            lock (sync)
                received.Add(receivedFrame);

            return receivedFrame;

        }

        /// <summary>
        /// Whether this frame completes the end of its stream: a DATA frame with
        /// END_STREAM, or the last frame of a header block whose HEADERS carried it.
        /// </summary>
        private Boolean EndsStream(HTTP2Frame Frame)
        {

            switch (Frame.Type)
            {

                case HTTP2FrameType.DATA:
                    return Frame.EndStream;

                case HTTP2FrameType.HEADERS:
                    return Frame.EndHeaders && Frame.EndStream;

                case HTTP2FrameType.CONTINUATION when Frame.EndHeaders:
                    lock (sync)
                        return received.LastOrDefault(frame => frame.Frame.StreamId == Frame.StreamId &&
                                                               frame.Frame.Type     == HTTP2FrameType.HEADERS)?.Frame.EndStream ?? false;

                default:
                    return false;

            }

        }

        #endregion


        #region Ordering the race

        /// <summary>
        /// The server's own stream object for this stream ID.
        /// </summary>
        public HTTP2Stream ServerStream(UInt32 StreamId)

            => StreamManager.TryGetStream(StreamId)
                   ?? throw new InvalidOperationException($"The server has no stream {StreamId}");

        /// <summary>
        /// The server connection's streams and its connection-level windows.
        /// </summary>
        private HTTP2StreamManager StreamManager

            => (HTTP2StreamManager) typeof(HTTP2Connection).
                                        GetField("streamManager", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                        GetValue(connection)!;

        /// <summary>
        /// The bytes of DATA the connection has taken in and neither given back to
        /// the client's window nor set aside to give back with a later
        /// WINDOW_UPDATE: bytes it holds for a reader that has not read them yet.
        /// Read while the connection is quiet, after a ping.
        /// </summary>
        public Int64 ConnectionWindowHeldBack()
        {

            var target  = (Int32) typeof(HTTP2Connection).GetField("connectionWindowSize",        BindingFlags.NonPublic | BindingFlags.Instance)!.
                                                          GetValue(connection)!;

            var owed    = (Int64) typeof(HTTP2Connection).GetField("connectionPendingRecvUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                                          GetValue(connection)!;

            return target - StreamManager.ConnectionRecvWindow - owed;

        }

        /// <summary>
        /// The bytes of DATA the connection may still send before the client
        /// grants more, as the connection counts them: less what it has sent, and
        /// less what its writer loop has taken off a stream's queue to send. Read
        /// while the connection is quiet, after a ping.
        /// </summary>
        public Int64 ConnectionSendWindow()

            => StreamManager.ConnectionSendWindow;

        /// <summary>
        /// Complete once the server has written the frame that ends its side of
        /// this stream, naming the thread that wrote it. Watch before the server
        /// can get there.
        /// </summary>
        private Task<Thread> EndOfStreamWritten(UInt32 StreamId)
        {

            // Must not resume the test on the server's thread, which is about to
            // go on to the half-close.
            var written = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (sync)
                watched.Add(StreamId, written);

            return written.Task;

        }

        /// <summary>
        /// Reset a stream while the server is between writing the frame that
        /// ends its side and the half-close that follows: the moment the read
        /// loop may handle a client's RST_STREAM on any real connection, and a
        /// window of a few instructions there.
        ///
        /// The stream's state lock is held on a thread of its own, and
        /// <paramref name="Answer"/> lets the server answer. The thread that
        /// writes the end of the stream comes on to the half-close and has to
        /// wait for that lock — past any test of the state made outside it. With
        /// the thread seen waiting, the stream is reset under the lock, as
        /// <see cref="HTTP2Stream.ResetByPeer"/> is called by the read loop for an
        /// RST_STREAM. Then the lock is let go, and the half-close finds the
        /// stream reset.
        /// </summary>
        public async Task ResetBeforeHalfCloseAsync(UInt32 StreamId, Action Answer)
        {

            // Anything the read loop still has to do for this stream (the
            // streaming path closes the remote side only after dispatching the
            // handler) must be done, or it would wait for the lock as well.
            await PingAsync();

            var stream      = ServerStream(StreamId);
            var endWritten  = EndOfStreamWritten(StreamId);

            using var holder = MonitorHolder.Hold(StateLockOf(stream));

            Answer();

            var writer = await endWritten.WaitAsync(StepTimeout);

            await WaitUntilBlockedAsync(writer);

            holder.LetGo(UnderLock: stream.ResetByPeer);

        }

        /// <summary>
        /// The monitor a stream's state transitions are made under.
        /// </summary>
        private static Object StateLockOf(HTTP2Stream Stream)

            => typeof(HTTP2Stream).GetField("stateLock", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                   GetValue(Stream)!;

        /// <summary>
        /// Wait until the thread is blocked. A thread waiting to enter a monitor
        /// shows WaitSleepJoin; a running, spinning or idle pool thread does not.
        /// </summary>
        private static async Task WaitUntilBlockedAsync(Thread Writer)
        {

            var waited = Stopwatch.StartNew();

            while ((Writer.ThreadState & System.Threading.ThreadState.WaitSleepJoin) == 0)
            {

                if (waited.Elapsed > StepTimeout)
                    throw new TimeoutException($"The server's thread never blocked after writing the end of the stream; it is {Writer.ThreadState}");

                await Task.Delay(1);

            }

        }

        /// <summary>
        /// Send a DATA frame, and run <paramref name="Meanwhile"/> while the read
        /// loop holds that frame between its check of the stream's state and
        /// anything it does with the frame's bytes: the moment a reset made on
        /// another task, a handler's or the writer loop's, may land on any real
        /// connection. Meanwhile has that reset made, and the read loop is held
        /// until the server has sent its RST_STREAM.
        ///
        /// The connection's receive-window lock, which the read loop takes right
        /// after that check, is held on a thread of its own. The frame's payload
        /// is read at once, as soon as its header is in, so the thread that reads
        /// it goes on from that read to the lock, and has to wait there. With that
        /// thread seen waiting, Meanwhile runs. The lock is let go by the thread
        /// that holds it, as soon as the RST_STREAM is written, whether Meanwhile
        /// is done by then or not; Meanwhile may wait for anything that follows.
        ///
        /// It was let go once Meanwhile was done, and Meanwhile needed a thread of
        /// the pool's to get there, where the pool had none to spare. Whatever
        /// resets the stream goes on to the lock right after its RST_STREAM, to
        /// give back the window of what was left unread, and waits there on its
        /// pool thread, beside the read loop: the writer loop or a failing
        /// handler's task, and a tunnel's handler too, once it has read the end of
        /// its tunnel. The reset completes what Meanwhile waits for on the thread
        /// that makes it, so Meanwhile's continuation is queued on that thread,
        /// just before it blocks, behind the tunnel handler's. Another thread takes
        /// them only once it finds nothing else to do, and the handler's takes the
        /// first such thread to the lock as well. On a CI runner with four cores,
        /// whose pool starts with four threads, one of them the test host's for
        /// good, a reset of the writer loop's left Meanwhile to the pool's
        /// starvation heuristics, and the threads they added went to the timers
        /// of what earlier tests had left running first:
        /// TunnelWriteFailsAsAChunkArrives took up to 13 s, and timed out in three
        /// nightlies of 2026-10-01.
        /// </summary>
        public async Task HoldDataAfterStateCheckAsync(HTTP2Frame Data, Func<Task> Meanwhile)
        {

            // Everything sent before has been handled, and the read loop waits for
            // the next frame: no earlier frame can be the one that waits.
            await PingAsync();

            var resetWritten = WatchForResetWritten(Data.StreamId);

            try
            {

                using var holder = MonitorHolder.Hold(ReceiveWindowLockOf(Connection));

                await SendAsync(Data);

                await WaitUntilReaderBlockedAsync();

                var letGo = holder.LetGoAsync(When:  resetWritten,
                                              What:  $"the server has sent RST_STREAM on stream {Data.StreamId}");

                await Meanwhile();

                await letGo;

            }
            finally
            {
                UnwatchResetWritten(resetWritten);
            }

        }

        /// <summary>
        /// The stream whose next RST_STREAM sets the event, once the server has
        /// written it — see <see cref="HoldDataAfterStateCheckAsync"/>.
        /// </summary>
        private (UInt32 StreamId, ManualResetEventSlim Written)? resetWatched;

        /// <summary>
        /// An event set once the server has written its next RST_STREAM on this
        /// stream, by the thread that wrote it, before it goes on: nothing of the
        /// test runs on that thread.
        /// </summary>
        private ManualResetEventSlim WatchForResetWritten(UInt32 StreamId)
        {

            var written = new ManualResetEventSlim();

            lock (sync)
                resetWatched = (StreamId, written);

            return written;

        }

        /// <summary>
        /// Stop watching for the RST_STREAM this event was set up for, if it is
        /// still watched for.
        /// </summary>
        private void UnwatchResetWritten(ManualResetEventSlim Written)
        {
            lock (sync)
            {
                if (resetWatched?.Written == Written)
                    resetWatched = null;
            }
        }

        /// <summary>
        /// A task that completes once a thread of its own has taken the
        /// receive-window lock and let it go again: while
        /// <see cref="HoldDataAfterStateCheckAsync"/> holds the lock, only once it
        /// is let go, as for every task of the server's that waits for the lock
        /// meanwhile. A Meanwhile that waits for this waits as Meanwhile did on a
        /// CI runner whose pool had no thread to spare, every thread it would run
        /// waiting for the lock: it can go on only once the lock is let go.
        /// </summary>
        public Task ReceiveWindowLockTakenAsync()
        {

            var receiveWindowLock  = ReceiveWindowLockOf(Connection);
            var taken              = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            new Thread(() => {

                lock (receiveWindowLock)
                { }

                taken.TrySetResult();

            }) {
                IsBackground = true,
                Name         = "ReceiveWindowLockTaker"
            }.Start();

            return taken.Task;

        }

        /// <summary>
        /// The server has written an RST_STREAM on this stream.
        /// </summary>
        private void ResetWrittenBy(UInt32 StreamId)
        {

            ManualResetEventSlim? written = null;

            lock (sync)
            {
                if (resetWatched is { } watched && watched.StreamId == StreamId)
                {
                    written        = watched.Written;
                    resetWatched   = null;
                }
            }

            written?.Set();

        }

        /// <summary>
        /// Send a DATA frame, and reset its stream while the read loop holds that
        /// frame past its check of the stream's state, as
        /// <see cref="HoldDataAfterStateCheckAsync"/> holds it: completely, before
        /// the read loop counts the frame's bytes. The stream is reset as every
        /// reset the server makes resets it, <see cref="HTTP2Stream.Reset"/>
        /// followed by the connection's return of the window of what the stream's
        /// reader left unread, both under the receive-window lock the read loop
        /// waits for. No RST_STREAM goes out. Then the lock is let go.
        /// </summary>
        public async Task ResetAfterStateCheckAsync(HTTP2Frame Data)
        {

            await PingAsync();

            var stream = ServerStream(Data.StreamId);

            using var holder = MonitorHolder.Hold(ReceiveWindowLockOf(Connection));

            await SendAsync(Data);

            await WaitUntilReaderBlockedAsync();

            holder.LetGo(UnderLock: () => {

                stream.Reset();

                // It takes the lock held here, re-entered, and for less than half
                // the connection window it sends nothing: done when it returns.
                ((Task) typeof(HTTP2Connection).GetMethod("ReturnUnreadWindowAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                                Invoke(Connection, [stream])!).GetAwaiter().GetResult();

            });

        }

        /// <summary>
        /// Send a header block that ends the client's side of a stream, with
        /// <paramref name="Send"/>, and reset the stream while the read loop holds
        /// that block between its check of the stream's state and the transition
        /// that ends the client's side: the moment a reset made on another task,
        /// a handler's or the writer loop's, may land on any real connection.
        ///
        /// The stream's state lock is held on a thread of its own. The thread that
        /// reads the block goes on, past that check and the block's decoding, to
        /// the transition, and has to wait for the lock there. With that thread
        /// seen waiting, the stream is reset under the lock, as
        /// <see cref="HTTP2Stream.Reset"/> is called for a reset of the server's
        /// own, but without its RST_STREAM. Then the lock is let go.
        /// </summary>
        public async Task ResetBeforeRemoteCloseAsync(UInt32 StreamId, Func<Task> Send)
        {

            // Everything sent before has been handled, and the read loop waits for
            // the next frame: no earlier frame can be the one that waits.
            await PingAsync();

            var stream = ServerStream(StreamId);

            using var holder = MonitorHolder.Hold(StateLockOf(stream));

            await Send();

            await WaitUntilReaderBlockedAsync();

            holder.LetGo(UnderLock: stream.Reset);

        }

        /// <summary>
        /// The monitor the connection's receive windows are counted under.
        /// </summary>
        private static Object ReceiveWindowLockOf(HTTP2Connection Connection)

            => typeof(HTTP2Connection).GetField("recvLock", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                       GetValue(Connection)!;

        /// <summary>
        /// Wait until the thread that last read from the client is blocked, as
        /// <see cref="WaitUntilBlockedAsync"/> waits for a writer.
        /// </summary>
        private async Task WaitUntilReaderBlockedAsync()
        {

            var waited = Stopwatch.StartNew();

            // Looked up anew each time: the read that completes the frame may come
            // after the wait has begun.
            while (lastReader is not { } reader || (reader.ThreadState & System.Threading.ThreadState.WaitSleepJoin) == 0)
            {

                if (waited.Elapsed > StepTimeout)
                    throw new TimeoutException($"The read loop never blocked after reading the frame; its thread is {lastReader?.ThreadState}");

                await Task.Delay(1);

            }

        }

        /// <summary>
        /// Hold the connection's write lock until the returned handle is disposed:
        /// every frame the server sends meanwhile waits for the lock, and the
        /// waits end in the order they began. So a frame the server has decided
        /// to send can be kept from the wire while the test changes what it was
        /// decided on. Nothing the test waits for may have to be sent meanwhile,
        /// not even the answer to a ping.
        /// </summary>
        public async Task<IDisposable> HoldWritesAsync()
        {

            var writeLock = WriteLockOf(Connection);

            if (!await writeLock.WaitAsync(StepTimeout))
                throw new TimeoutException("The connection's write lock could not be taken");

            return new HeldWrites(writeLock);

        }

        /// <summary>
        /// The semaphore the connection's writes are made under.
        /// </summary>
        private static SemaphoreSlim WriteLockOf(HTTP2Connection Connection)

            => (SemaphoreSlim) typeof(HTTP2Connection).GetField("writeLock", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                                       GetValue(Connection)!;

        /// <summary>
        /// Lets go of the write lock once, when disposed.
        /// </summary>
        private sealed class HeldWrites(SemaphoreSlim WriteLock) : IDisposable
        {

            private Int32 released;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref released, 1) == 0)
                    WriteLock.Release();
            }

        }

        /// <summary>
        /// The stream whose next DATA frame, once the server has written it, runs
        /// the action on the thread that wrote it, before the write returns — see
        /// <see cref="HoldTakenDataAsync"/>.
        /// </summary>
        private (UInt32 StreamId, Action<Thread> Then)? dataWritten;

        /// <summary>
        /// Let the writer loop take the second of two chunks queued on a stream,
        /// and hold it between its take of the chunk and its wait for the
        /// connection's write lock until the returned handle lets it go on: the
        /// moment a reset made on another task, the read loop's or a handler's,
        /// may land on any real connection, and send its RST_STREAM ahead of the
        /// DATA. The write lock is free meanwhile, so that RST_STREAM does go out,
        /// and the test may read it.
        ///
        /// After it has taken a chunk, the writer loop enters the connection's
        /// flow-control lock once more, to give back what it reserved of the send
        /// windows beyond the chunk, and that is where the chunk is held: the
        /// second chunk must be smaller than what the loop reserves, as much of
        /// both windows as one frame may carry. But the loop enters that lock to
        /// pick a stream as well, right before the take, and a lock held in
        /// advance would hold the loop there. So the loop is walked into place
        /// with the locks of two streams' queues, each held on a thread of its own:
        /// - <paramref name="Write"/> queues both chunks while the test holds the
        ///   connection's writes, and returns once they are queued: the second is
        ///   queued before the first goes out.
        /// - As the writer loop writes the first chunk, the lock of the queue of
        ///   <paramref name="ScannedAfter"/>, an open stream with nothing queued
        ///   that the loop scans after this one, is taken before the write
        ///   returns. The loop goes on from the write, on that thread, to its next
        ///   pick. It finds the second chunk on this stream, and then waits for the
        ///   other stream's queue, holding the flow-control lock.
        /// - This stream's queue lock is taken, and the other's let go. The loop
        ///   picks this stream, lets go of the flow-control lock, and has to wait
        ///   for this stream's queue to take the chunk.
        /// - The flow-control lock is taken, and this stream's queue let go. The
        ///   loop takes the chunk, and waits for the flow-control lock, to give
        ///   back what it reserved beyond the chunk.
        /// </summary>
        public async Task<HeldTakenData> HoldTakenDataAsync(UInt32 StreamId, UInt32 ScannedAfter, Func<Task> Write)
        {

            // Everything sent before has been handled: both streams are open.
            await PingAsync();

            var stream     = ServerStream(StreamId);
            var neighbour  = ServerStream(ScannedAfter);
            var scanned    = StreamManager.GetSendableStreams().Select(sendable => sendable.StreamId).ToList();

            if (scanned.IndexOf(StreamId) < 0 || scanned.IndexOf(ScannedAfter) < scanned.IndexOf(StreamId))
                throw new InvalidOperationException($"The writer loop does not scan stream {ScannedAfter} after stream {StreamId}, but {String.Join(", ", scanned)}");

            // The holder is taken on the writer's thread, and a failure to take it
            // goes to the test: the server's write itself must not fail.
            var firstWritten = new TaskCompletionSource<(Thread Writer, MonitorHolder Neighbour)>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (sync)
                dataWritten = (StreamId, writer => {

                                  try
                                  {
                                      firstWritten.TrySetResult((writer, MonitorHolder.Hold(QueueLockOf(neighbour))));
                                  }
                                  catch (Exception e)
                                  {
                                      firstWritten.TrySetException(e);
                                  }

                              });

            Thread         writer;
            MonitorHolder  neighbourQueue;

            try
            {

                using (await HoldWritesAsync())
                    await Write();

                (writer, neighbourQueue) = await firstWritten.Task.WaitAsync(StepTimeout);

            }
            catch
            {
                // A holder taken after all must not keep the loop from the end of
                // the connection.
                _ = firstWritten.Task.ContinueWith(written => written.Result.Neighbour.Dispose(), TaskContinuationOptions.OnlyOnRanToCompletion);
                throw;
            }
            finally
            {
                lock (sync)
                    dataWritten = null;
            }

            MonitorHolder? streamQueue  = null;
            MonitorHolder? flow         = null;

            try
            {

                await WaitUntilPickBlockedAsync(writer, "at the queue of the stream it scans after this one");

                streamQueue = MonitorHolder.Hold(QueueLockOf(stream));

                neighbourQueue.LetGo();

                // Entered once the loop has picked this stream and let go of the
                // lock, which it does not want again before it has taken the chunk.
                flow = MonitorHolder.Hold(FlowLockOf(Connection));

                streamQueue.LetGo();

                // Once the chunk is off the queue, the loop can wait for nothing
                // but the flow-control lock.
                await WaitUntilAsync(() => !stream.OutboundQueue.HasPending && IsBlocked(writer),
                                     "the writer loop has taken the chunk, and waits to give back what it reserved beyond it");

                return new HeldTakenData(this, stream, writer, flow, QueueLockOf(neighbour));

            }
            catch
            {
                neighbourQueue.Dispose();
                streamQueue?.Dispose();
                flow?.Dispose();
                throw;
            }

        }

        /// <summary>
        /// The server has written a DATA frame on this stream, on this thread.
        /// </summary>
        private void DataWrittenBy(UInt32 StreamId, Thread Writer)
        {

            Action<Thread>? then = null;

            lock (sync)
            {
                if (dataWritten is { } armed && armed.StreamId == StreamId)
                {
                    then         = armed.Then;
                    dataWritten  = null;
                }
            }

            then?.Invoke(Writer);

        }

        /// <summary>
        /// Wait until the writer loop waits for the lock of a stream's queue in the
        /// midst of a pick: its thread is blocked, and the flow-control lock that
        /// it picks under is held.
        /// </summary>
        private Task WaitUntilPickBlockedAsync(Thread Writer, String Where)

            => WaitUntilAsync(() => IsBlocked(Writer) && IsHeld(FlowLockOf(Connection)),
                              $"the writer loop waits {Where}");

        /// <summary>
        /// Wait until the condition holds, or fail once the step timeout is over.
        /// </summary>
        private static async Task WaitUntilAsync(Func<Boolean> Condition, String What)
        {

            var waited = Stopwatch.StartNew();

            while (!Condition())
            {

                if (waited.Elapsed > StepTimeout)
                    throw new TimeoutException($"Timed out waiting until {What}");

                await Task.Delay(1);

            }

        }

        /// <summary>
        /// Whether the thread is blocked — see <see cref="WaitUntilBlockedAsync"/>.
        /// </summary>
        private static Boolean IsBlocked(Thread Writer)

            => (Writer.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0;

        /// <summary>
        /// Whether another thread holds the monitor: it cannot be entered at once.
        /// </summary>
        private static Boolean IsHeld(Object Gate)
        {

            if (!Monitor.TryEnter(Gate))
                return true;

            Monitor.Exit(Gate);

            return false;

        }

        /// <summary>
        /// The monitor the connection's send windows are counted under, and that
        /// its writer loop picks the next stream to send from under.
        /// </summary>
        private static Object FlowLockOf(HTTP2Connection Connection)

            => typeof(HTTP2Connection).GetField("flowLock", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                       GetValue(Connection)!;

        /// <summary>
        /// The monitor a stream's outbound queue is kept under: the writer loop
        /// takes it to see what is queued, and to take a chunk.
        /// </summary>
        private static Object QueueLockOf(HTTP2Stream Stream)

            => typeof(HTTP2OutboundQueue).GetField("gate", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                          GetValue(Stream.OutboundQueue)!;

        /// <summary>
        /// A chunk the writer loop has taken, and is held from going on with — see
        /// <see cref="HoldTakenDataAsync"/>. Disposed, it lets the loop go on.
        /// </summary>
        public sealed class HeldTakenData : IDisposable
        {

            private readonly PipedH2ServerConnection  peer;
            private readonly HTTP2Stream              stream;
            private readonly Thread                   writer;
            private readonly MonitorHolder            flow;
            private readonly Object                   neighbourQueue;

            internal HeldTakenData(PipedH2ServerConnection  Peer,
                                   HTTP2Stream              Stream,
                                   Thread                   Writer,
                                   MonitorHolder            Flow,
                                   Object                   NeighbourQueue)
            {
                this.peer            = Peer;
                this.stream          = Stream;
                this.writer          = Writer;
                this.flow            = Flow;
                this.neighbourQueue  = NeighbourQueue;
            }

            /// <summary>
            /// Let the writer loop go on with the chunk, and return once it is done
            /// with it, whether it wrote it or not. The loop's next pick is held as
            /// its first one was, at the queue of the stream it scans after this
            /// one, and let go once the loop waits there, past the chunk: whatever
            /// it wrote of it is with the client then. The loop must go on on the
            /// same thread, so nothing may hold the connection's writes meanwhile.
            /// </summary>
            public async Task LetGoAsync()
            {

                var served = stream.LastServedSequence;

                using var neighbour = MonitorHolder.Hold(neighbourQueue);

                flow.LetGo();

                // The loop counts the chunk as served once it has given back what
                // it reserved beyond it, right before it goes on to the write lock.
                await WaitUntilAsync(() => stream.LastServedSequence != served && IsBlocked(writer) && IsHeld(FlowLockOf(peer.Connection)),
                                     "the writer loop is done with the chunk, and waits at its next pick");

                neighbour.LetGo();

            }

            public void Dispose()

                => flow.Dispose();

        }

        #endregion


        #region Failing a write

        /// <summary>
        /// Fail the server's next write of a DATA frame on this stream with
        /// <paramref name="Failure"/>, as a broken transport would, and write
        /// none of it. The writes after it go through again.
        /// </summary>
        public void FailNextDataWrite(UInt32 StreamId, Exception Failure)
        {
            lock (sync)
                dataWriteFailure = (StreamId, Failure);
        }

        /// <summary>
        /// The failure armed for a write of this frame, if any — only once.
        /// </summary>
        private Exception? WriteFailureFor(HTTP2Frame Frame)
        {

            lock (sync)
            {

                if (dataWriteFailure is not { } armed ||
                    Frame.Type     != HTTP2FrameType.DATA ||
                    Frame.StreamId != armed.StreamId)
                    return null;

                dataWriteFailure = null;

                return armed.Failure;

            }

        }

        #endregion


        #region Server side

        private void EndOfStreamWrittenBy(UInt32 StreamId, Thread Writer)
        {

            TaskCompletionSource<Thread>? written;

            lock (sync)
                watched.Remove(StreamId, out written);

            written?.TrySetResult(Writer);

        }

        /// <summary>
        /// The connection's end: reads what the test sends, and writes to the
        /// test, noting which writes end a stream. Every write completes before
        /// it returns.
        /// </summary>
        private sealed class ServerSide(PipedH2ServerConnection Peer) : Stream
        {

            private readonly Stream      fromClient      = Peer.clientToServer.Reader.AsStream();
            private readonly PipeWriter  toClient        = Peer.serverToClient.Writer;

            // Frame parser state for the outgoing bytes. The connection
            // serializes its writes, so no lock is needed.
            private readonly Byte[]      header          = new Byte[HTTP2Frame.HeaderSize];
            private          Int32       headerFill;
            private          Int64       payloadLeft;
            private          HTTP2Frame? frame;
            private          UInt32?     blockEndsStream;

            public override ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
            {

                // A read of bytes already sent completes at once, on this thread,
                // and the read loop goes on here to the frame it completes.
                Peer.lastReader = Thread.CurrentThread;

                return fromClient.ReadAsync(Buffer, CancellationToken);

            }

            public override ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
            {

                // The connection writes one whole frame at a time, so a write
                // that starts between frames starts with a frame header.
                var starts = frame is null && headerFill == 0 && Buffer.Length >= HTTP2Frame.HeaderSize
                                 ? HTTP2Frame.ParseHeader(Buffer.Span[..HTTP2Frame.HeaderSize])
                                 : null;

                if (starts is not null && Peer.WriteFailureFor(starts) is { } failure)
                    return ValueTask.FromException(failure);

                var ended = StreamsEndedBy(Buffer.Span);

                toClient.Write(Buffer.Span);

                var flush = toClient.FlushAsync(CancellationToken);

                if (!flush.IsCompleted)
                    throw new InvalidOperationException("A write to the client did not complete at once");

                flush.GetAwaiter().GetResult();

                foreach (var streamId in ended)
                    Peer.EndOfStreamWrittenBy(streamId, Thread.CurrentThread);

                if (starts?.Type == HTTP2FrameType.DATA)
                    Peer.DataWrittenBy(starts.StreamId, Thread.CurrentThread);

                if (starts?.Type == HTTP2FrameType.RST_STREAM)
                    Peer.ResetWrittenBy(starts.StreamId);

                return ValueTask.CompletedTask;

            }

            /// <summary>
            /// The IDs of the streams whose end is completed by a frame whose last
            /// byte is in <paramref name="Data"/>: a DATA frame with END_STREAM, or
            /// the last frame of a header block whose HEADERS carried it.
            /// </summary>
            private List<UInt32> StreamsEndedBy(ReadOnlySpan<Byte> Data)
            {

                var ended = new List<UInt32>();

                while (!Data.IsEmpty)
                {

                    if (frame is null)
                    {

                        var take     = Math.Min(header.Length - headerFill, Data.Length);
                        Data[..take].CopyTo(header.AsSpan(headerFill));
                        headerFill  += take;
                        Data         = Data[take..];

                        if (headerFill < header.Length)
                            continue;

                        frame        = HTTP2Frame.ParseHeader(header);
                        payloadLeft  = frame.Length;
                        headerFill   = 0;

                    }
                    else
                    {
                        var skip     = (Int32) Math.Min(payloadLeft, Data.Length);
                        payloadLeft -= skip;
                        Data         = Data[skip..];
                    }

                    if (payloadLeft > 0)
                        continue;

                    switch (frame.Type)
                    {

                        case HTTP2FrameType.DATA when frame.EndStream:
                            ended.Add(frame.StreamId);
                            break;

                        case HTTP2FrameType.HEADERS when frame.EndStream:
                            if (frame.EndHeaders)
                                ended.Add(frame.StreamId);
                            else
                                blockEndsStream = frame.StreamId;
                            break;

                        case HTTP2FrameType.CONTINUATION when frame.EndHeaders && blockEndsStream == frame.StreamId:
                            ended.Add(frame.StreamId);
                            blockEndsStream = null;
                            break;

                    }

                    frame = null;

                }

                return ended;

            }

            public override Task<Int32> ReadAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
                => ReadAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

            public override Task WriteAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
                => WriteAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

            public override Task FlushAsync(CancellationToken CancellationToken)
                => Task.CompletedTask;

            // The connection only ever uses the asynchronous API.
            public override Int32 Read (Byte[] Buffer, Int32 Offset, Int32 Count) => throw new NotSupportedException();
            public override void  Write(Byte[] Buffer, Int32 Offset, Int32 Count) => throw new NotSupportedException();
            public override void  Flush() { }

            public override Boolean  CanRead   => true;
            public override Boolean  CanWrite  => true;
            public override Boolean  CanSeek   => false;
            public override Int64    Length    => throw new NotSupportedException();
            public override Int64    Position  { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override Int64    Seek(Int64 Offset, SeekOrigin Origin) => throw new NotSupportedException();
            public override void     SetLength(Int64 Value)                => throw new NotSupportedException();

        }

        #endregion


        /// <summary>
        /// End the connection and both directions, so that a test which failed
        /// half-way leaves nothing running behind it.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            cancellation.Cancel();

            if (running is not null)
            {
                try
                {
                    await running.WaitAsync(StepTimeout);
                }
                catch
                { }
            }

            // The server's own tasks may still be finishing a write; the test is
            // over either way.
            try
            {
                await clientToServer.Writer.CompleteAsync();
                await serverToClient.Writer.CompleteAsync();
            }
            catch
            { }

        }

    }


    /// <summary>
    /// Holds a monitor on a thread of its own — a monitor belongs to the thread
    /// that entered it, and a test's awaits may resume on any — until told to
    /// let go.
    /// </summary>
    internal sealed class MonitorHolder : IDisposable
    {

        private readonly Thread                   thread;
        private readonly ManualResetEventSlim     held       = new();
        private readonly ManualResetEventSlim     letGo      = new();
        private readonly CancellationTokenSource  disposed   = new();
        private readonly TaskCompletionSource     letGone    = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private          (ManualResetEventSlim Event, String What)?  letGoWhen;
        private          Action?                  underLock;
        private          Exception?               failure;

        private MonitorHolder(Object Monitor)
        {

            thread = new Thread(() => {

                         lock (Monitor)
                         {

                             held.Set();
                             letGo.Wait();

                             // Thrown on this thread, it would end the test run.
                             try
                             {

                                 // Waited for here, on the thread that holds the monitor:
                                 // a wait of the caller's would need a thread of the pool's
                                 // to go on, and those may all be waiting for the monitor.
                                 if (letGoWhen is { } when && !when.Event.Wait(PipedH2ServerConnection.StepTimeout, disposed.Token))
                                     throw new TimeoutException($"Timed out waiting until {when.What}");

                                 underLock?.Invoke();

                             }
                             catch (OperationCanceledException) when (disposed.IsCancellationRequested)
                             { }
                             catch (Exception e)
                             {
                                 failure = e;
                             }

                         }

                         // Only for LetGoAsync, whose caller awaits it.
                         if (letGoWhen is not null)
                         {
                             if (failure is null)
                                 letGone.TrySetResult();
                             else
                                 letGone.TrySetException(failure);
                         }

                     }) {
                         IsBackground = true,
                         Name         = nameof(MonitorHolder)
                     };

        }

        /// <summary>
        /// Enter the monitor on a thread of its own, and return once it is held.
        /// </summary>
        public static MonitorHolder Hold(Object Monitor)
        {

            var holder = new MonitorHolder(Monitor);

            holder.thread.Start();

            if (!holder.held.Wait(PipedH2ServerConnection.StepTimeout))
                throw new TimeoutException("The monitor could not be entered");

            return holder;

        }

        /// <summary>
        /// Run <paramref name="UnderLock"/> while still holding the monitor, then
        /// let go of it, and return once it is let go.
        /// </summary>
        public void LetGo(Action? UnderLock = null)
        {

            underLock = UnderLock;
            letGo.Set();

            if (!thread.Join(PipedH2ServerConnection.StepTimeout))
                throw new TimeoutException("The monitor was not let go");

            if (failure is not null)
                throw new InvalidOperationException("The action under the monitor failed", failure);

        }

        /// <summary>
        /// Let go of the monitor once <paramref name="When"/> is set, on the thread
        /// that holds it, as soon as it is set: nothing has to run on the pool for
        /// that. The task completes once the monitor is let go. It fails if When
        /// is not set within the step timeout, with <paramref name="What"/> in
        /// its message; the monitor is let go then all the same.
        /// </summary>
        public Task LetGoAsync(ManualResetEventSlim When, String What)
        {

            letGoWhen = (When, What);
            letGo.Set();

            return letGone.Task;

        }

        public void Dispose()
        {
            disposed.Cancel();
            letGo.Set();
            thread.Join(PipedH2ServerConnection.StepTimeout);
        }

    }

}
