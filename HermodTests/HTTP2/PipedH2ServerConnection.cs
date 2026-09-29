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
    /// see <see cref="HoldDataAfterStateCheckAsync"/>.
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


        #region StartAsync(RequestHandler, StreamingHandler = null, ConnectHandler = null)

        /// <summary>
        /// Start a server connection with these handlers, and return once the
        /// client side has completed the connection preface.
        /// </summary>
        public static async Task<PipedH2ServerConnection> StartAsync(HTTP2RequestHandler     RequestHandler,
                                                                     HTTP2StreamingHandler?  StreamingHandler   = null,
                                                                     HTTP2ConnectHandler?    ConnectHandler     = null)
        {

            var peer = new PipedH2ServerConnection();

            peer.connection = new HTTP2Connection(peer.Server,
                                                  RequestHandler,
                                                  ConnectHandler:     ConnectHandler,
                                                  CancellationToken:  peer.cancellation.Token,
                                                  StreamingHandler:   StreamingHandler);

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

            var target  = (Int64) typeof(HTTP2Connection).GetField("ConnectionRecvWindowTarget",  BindingFlags.NonPublic | BindingFlags.Static)!.
                                                          GetValue(null)!;

            var owed    = (Int64) typeof(HTTP2Connection).GetField("connectionPendingRecvUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.
                                                          GetValue(connection)!;

            return target - StreamManager.ConnectionRecvWindow - owed;

        }

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
        /// <see cref="HTTP2Stream.Reset"/> is called by the read loop for an
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

            holder.LetGo(UnderLock: stream.Reset);

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
        /// connection.
        ///
        /// The connection's receive-window lock, which the read loop takes right
        /// after that check, is held on a thread of its own. The frame's payload
        /// is read at once, as soon as its header is in, so the thread that reads
        /// it goes on from that read to the lock, and has to wait there. With that
        /// thread seen waiting, Meanwhile runs; then the lock is let go. Nothing
        /// else may take the lock meanwhile: a handler reading its body, say,
        /// would wait there as well.
        /// </summary>
        public async Task HoldDataAfterStateCheckAsync(HTTP2Frame Data, Func<Task> Meanwhile)
        {

            // Everything sent before has been handled, and the read loop waits for
            // the next frame: no earlier frame can be the one that waits.
            await PingAsync();

            using var holder = MonitorHolder.Hold(ReceiveWindowLockOf(Connection));

            await SendAsync(Data);

            await WaitUntilReaderBlockedAsync();

            await Meanwhile();

            holder.LetGo();

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
                if (frame is null && headerFill == 0 && Buffer.Length >= HTTP2Frame.HeaderSize &&
                    Peer.WriteFailureFor(HTTP2Frame.ParseHeader(Buffer.Span[..HTTP2Frame.HeaderSize])) is { } failure)
                    return ValueTask.FromException(failure);

                var ended = StreamsEndedBy(Buffer.Span);

                toClient.Write(Buffer.Span);

                var flush = toClient.FlushAsync(CancellationToken);

                if (!flush.IsCompleted)
                    throw new InvalidOperationException("A write to the client did not complete at once");

                flush.GetAwaiter().GetResult();

                foreach (var streamId in ended)
                    Peer.EndOfStreamWrittenBy(streamId, Thread.CurrentThread);

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

        private readonly Thread                thread;
        private readonly ManualResetEventSlim  held   = new();
        private readonly ManualResetEventSlim  letGo  = new();
        private          Action?               underLock;
        private          Exception?            failure;

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
                                 underLock?.Invoke();
                             }
                             catch (Exception e)
                             {
                                 failure = e;
                             }

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

        public void Dispose()
        {
            letGo.Set();
            thread.Join(PipedH2ServerConnection.StepTimeout);
        }

    }

}
