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
using System.IO.Pipelines;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// An in-memory transport for one <see cref="HTTP2ClientConnection"/>, whose
    /// other end the test plays by hand, frame by frame. It puts in order two
    /// things that otherwise only the thread scheduler orders: the client
    /// returning from the write of a request's HEADERS, and the connection's
    /// read loop handling the server's answer to that very request.
    ///
    /// For every stream <see cref="HoldHeadersOf"/> selects, the client's write
    /// of the HEADERS frame hands the bytes over and then does not return until
    /// the test calls <see cref="Release"/>. Meanwhile the test can read the
    /// request, answer it, and learn from <see cref="SendAsync"/> when the read
    /// loop has handled the answer — all while the client is still inside that
    /// write, which on a real socket is a window of microseconds.
    /// </summary>
    internal sealed class HoldingH2Transport : IAsyncDisposable
    {

        /// <summary>
        /// How long any single step may take before the test gives up — every
        /// step is in memory, so this only bounds a test that went wrong.
        /// </summary>
        public static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

        private readonly Pipe          clientToServer  = new();
        private readonly Pipe          serverToClient  = new();
        private readonly Stream        fromClient;
        private readonly Stream        toClient;
        private readonly HPACKEncoder  encoder         = new();

        private readonly Lock                                      sync  = new();
        private readonly Dictionary<UInt32, TaskCompletionSource>  held  = [];

        private Int64                  sent;
        private Int64                  delivered;
        private TaskCompletionSource?  caughtUp;
        private Int64                  caughtUpAt;
        private Boolean                prefaceRead;

        /// <summary>
        /// The stream to hand to the <see cref="HTTP2ClientConnection"/>.
        /// </summary>
        public Stream                  Client          { get; }

        /// <summary>
        /// Which streams' HEADERS the client is held in until <see cref="Release"/>.
        /// </summary>
        public Func<UInt32, Boolean>   HoldHeadersOf   { get; }

        public HoldingH2Transport(Func<UInt32, Boolean> HoldHeadersOf)
        {
            this.HoldHeadersOf  = HoldHeadersOf;
            this.fromClient     = clientToServer.Reader.AsStream();
            this.toClient       = serverToClient.Writer.AsStream();
            this.Client         = new ClientSide(this);
        }


        #region Server side

        /// <summary>
        /// Answer the connection preface with an empty SETTINGS frame, and return
        /// once the client has taken it — which is when its StartAsync returns.
        /// </summary>
        public async Task<HTTP2ClientConnection> ConnectAsync(HTTP2ClientOptions Options)
        {

            var connection = new HTTP2ClientConnection(Client, Options);
            var starting   = connection.StartAsync();

            await SendAsync(HTTP2Frame.CreateSettings());
            await starting.WaitAsync(StepTimeout);

            return connection;

        }

        /// <summary>
        /// The next HEADERS frame the client sent, skipping everything else —
        /// into <paramref name="Skipped"/>, if given.
        /// </summary>
        public async Task<HTTP2Frame> NextHeadersAsync(ICollection<HTTP2Frame>? Skipped = null)
        {

            using var timeout = new CancellationTokenSource(StepTimeout);

            while (true)
            {

                var frame = await NextFrameAsync(timeout.Token)
                                ?? throw new EndOfStreamException("The client closed before sending HEADERS");

                if (frame.Type == HTTP2FrameType.HEADERS)
                    return frame;

                Skipped?.Add(frame);

            }

        }

        /// <summary>
        /// The next frame the client sent, of whatever type, or null once the
        /// client's side has ended. Reads the connection preface first, the first
        /// time.
        /// </summary>
        public async Task<HTTP2Frame?> NextFrameAsync(CancellationToken CancellationToken)
        {

            if (!prefaceRead)
            {
                if (!await H2Raw.ReadExactAsync(fromClient, new Byte[H2Raw.Preface.Length], CancellationToken))
                    throw new EndOfStreamException("The client closed before its connection preface");
                prefaceRead = true;
            }

            return await H2Raw.ReadFrameAsync(fromClient, CancellationToken);

        }

        /// <summary>
        /// The next frame the client sent on <paramref name="StreamId"/>, skipping
        /// those on other streams — into <paramref name="Skipped"/>, if given.
        /// </summary>
        public async Task<HTTP2Frame> NextFrameOnAsync(UInt32 StreamId, ICollection<HTTP2Frame>? Skipped = null)
        {

            using var timeout = new CancellationTokenSource(StepTimeout);

            while (true)
            {

                var frame = await NextFrameAsync(timeout.Token)
                                ?? throw new EndOfStreamException($"The client closed before sending on stream {StreamId}");

                if (frame.StreamId == StreamId)
                    return frame;

                Skipped?.Add(frame);

            }

        }

        /// <summary>
        /// Send a frame to the client and return once the connection's read loop
        /// has handled it. The loop handles one frame at a time and reads the
        /// next only after that, so a read that begins with every byte sent so
        /// far taken means everything sent so far has been handled.
        /// </summary>
        public async Task SendAsync(HTTP2Frame Frame)
        {

            var bytes = Frame.Serialize();

            // Unlike a release, this must not resume the test inline: that would
            // run the test on the connection's read loop, which could not read
            // until the test gave its thread back.
            var done  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Armed before the bytes go out, so the read that takes them cannot
            // slip past unobserved.
            lock (sync)
            {
                sent       += bytes.Length;
                caughtUp    = done;
                caughtUpAt  = sent;
            }

            await toClient.WriteAsync(bytes);
            await done.Task.WaitAsync(StepTimeout);

        }

        /// <summary>
        /// Refuse a stream (RFC 9113, Section 8.7), waiting until the read loop has handled it.
        /// </summary>
        public Task RefuseAsync(UInt32 StreamId)
            => SendAsync(HTTP2Frame.CreateRstStream(StreamId, HTTP2ErrorCode.REFUSED_STREAM));

        /// <summary>
        /// Answer a stream 200 with a short body, waiting until the read loop has handled it.
        /// </summary>
        public async Task RespondAsync(UInt32 StreamId, String Body)
        {

            var content = Encoding.ASCII.GetBytes(Body);

            await SendAsync(HTTP2Frame.CreateHeaders(StreamId,
                                                     encoder.EncodeHeaderBlock([(":status",        "200"),
                                                                                ("content-length", content.Length.ToString())]),
                                                     EndStream:  false,
                                                     EndHeaders: true));

            await SendAsync(HTTP2Frame.CreateData(StreamId, content, EndStream: true));

        }

        /// <summary>
        /// Send the last frame the client is to read: one it must answer with a
        /// connection error. Returns once the frame is on its way, not once the
        /// read loop has handled it as <see cref="SendAsync"/> does — a loop that
        /// ends over it never starts the read that would tell. Whether it ended,
        /// the test learns from <see cref="HTTP2ClientConnection.Closed"/>.
        /// </summary>
        public async Task SendLastAsync(HTTP2Frame Frame)
        {

            var bytes = Frame.Serialize();

            // Counted all the same, so that a SendAsync after it, where the
            // client read on after all, waits for its own frame.
            lock (sync)
                sent += bytes.Length;

            await toClient.WriteAsync(bytes);

        }

        /// <summary>
        /// Let the client return from the write of the HEADERS of this stream.
        ///
        /// A write the read loop started — a retry — carries on inline, on the
        /// calling thread and as far as it gets without waiting, before this
        /// returns. Whatever the retry does right after its write is then done,
        /// and cannot race the test's next step. (A write started from the test
        /// itself resumes on the test's SynchronizationContext instead, as it
        /// always does.)
        /// </summary>
        public void Release(UInt32 StreamId)
        {

            TaskCompletionSource? release;

            lock (sync)
                held.Remove(StreamId, out release);

            // A hold that never engaged would leave the test ordering nothing,
            // and passing for the wrong reason.
            if (release is null)
                throw new InvalidOperationException($"The client is not held in the HEADERS of stream {StreamId}");

            // The TPL never runs a continuation inline on a thread with a
            // SynchronizationContext of its own, and NUnit gives its test threads
            // one. Without it, the retry's continuations — which the read loop
            // registered, and so captured none — run right here.
            var context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                release.TrySetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }

        }

        #endregion


        #region Client side

        private void ReadStarting()
        {

            TaskCompletionSource? done = null;

            lock (sync)
            {
                if (caughtUp is not null && delivered >= caughtUpAt)
                {
                    done      = caughtUp;
                    caughtUp  = null;
                }
            }

            done?.TrySetResult();

        }

        private void ReadDone(Int32 Count)
        {
            lock (sync)
                delivered += Count;
        }

        private Task Hold(UInt32 StreamId)
        {

            // Continuations run synchronously, and deliberately so: see Release.
            var release = new TaskCompletionSource();

            lock (sync)
                held.Add(StreamId, release);

            return release.Task;

        }

        /// <summary>
        /// The connection's end: reads what the test sends, and writes to the
        /// test — holding a write that completes a selected HEADERS frame until
        /// it is released. Outgoing bytes are parsed into frames as they pass,
        /// so where the connection splits its writes does not matter.
        /// </summary>
        private sealed class ClientSide(HoldingH2Transport Transport) : Stream
        {

            private readonly Stream  fromServer      = Transport.serverToClient.Reader.AsStream();
            private readonly Stream  toServer        = Transport.clientToServer.Writer.AsStream();

            // Frame parser state for the outgoing bytes. The connection
            // serializes its writes, so no lock is needed.
            private readonly Byte[]  header          = new Byte[HTTP2Frame.HeaderSize];
            private          Int32   prefaceLeft     = H2Raw.Preface.Length;
            private          Int32   headerFill;
            private          Int64   payloadLeft;
            private          HTTP2Frame? frame;

            public override async ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
            {
                Transport.ReadStarting();
                var count = await fromServer.ReadAsync(Buffer, CancellationToken);
                Transport.ReadDone(count);
                return count;
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
            {

                // Holds exist before the bytes go out, so the test cannot read
                // these HEADERS and then find nothing to release.
                var holds = HeadersCompletedBy(Buffer.Span).
                                Where (Transport.HoldHeadersOf).
                                Select(Transport.Hold).
                                ToList();

                await toServer.WriteAsync(Buffer, CancellationToken);

                foreach (var hold in holds)
                    await hold.WaitAsync(CancellationToken);

            }

            /// <summary>
            /// The stream IDs of the HEADERS frames whose last byte is in <paramref name="Data"/>.
            /// </summary>
            private List<UInt32> HeadersCompletedBy(ReadOnlySpan<Byte> Data)
            {

                var completed = new List<UInt32>();

                while (!Data.IsEmpty)
                {

                    if (prefaceLeft > 0)
                    {
                        var skip     = Math.Min(prefaceLeft, Data.Length);
                        prefaceLeft -= skip;
                        Data         = Data[skip..];
                        continue;
                    }

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

                    if (payloadLeft == 0)
                    {

                        if (frame.Type == HTTP2FrameType.HEADERS)
                            completed.Add(frame.StreamId);

                        frame = null;

                    }

                }

                return completed;

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
        /// Let go of every held write and end both directions, so that a test
        /// which failed half-way leaves nothing waiting behind it.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            List<TaskCompletionSource> stillHeld;

            lock (sync)
            {
                stillHeld = [.. held.Values];
                held.Clear();
            }

            foreach (var release in stillHeld)
                release.TrySetResult();

            await serverToClient.Writer.CompleteAsync();
            await clientToServer.Writer.CompleteAsync();

        }

    }

}
