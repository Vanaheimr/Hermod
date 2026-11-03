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

using System.Text;
using System.Reflection;
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The write side of a connection (<see cref="HTTP2FrameWriter"/>): one
    /// writer at a time, its frames buffered, and the flush left to the last of
    /// the writers waiting; or, as the server has it, the buffer sent outside
    /// the lock, by one sender at a time, which takes what gathered meanwhile.
    /// What reaches the transport is checked per write, so a flush the writer
    /// should have left to another shows up as a write of its own; and a write
    /// can be held on the transport, to see what the writers do during a send.
    /// What holds either way is tested either way.
    /// </summary>
    [TestFixture]
    public class FrameWriterTests
    {

        #region (helpers)

        /// <summary>
        /// A transport that keeps each write it is given, in order.
        /// </summary>
        private sealed class RecordingStream : Stream
        {

            private readonly Lock          sync    = new();
            private readonly List<Byte[]>  writes  = [];
            private (TaskCompletionSource Started, TaskCompletionSource Release)? hold;
            private Int32                  inFlight;

            /// <summary>
            /// Fail the next write with this, as a broken transport would, and
            /// take none of it.
            /// </summary>
            public Exception?              FailNextWrite { get; set; }

            /// <summary>
            /// Whether two writes were ever made at once.
            /// </summary>
            public Boolean                 Overlapped    { get; private set; }

            /// <summary>
            /// Hold the next write on the transport, as a peer that reads slowly
            /// would, until Release is called. Started completes once that
            /// write has begun, and waits.
            /// </summary>
            public (Task Started, Action Release) HoldNextWrite()
            {

                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                lock (sync)
                    hold = (started, release);

                return (started.Task, () => release.TrySetResult());

            }

            /// <summary>
            /// The writes so far, each as the text it carried.
            /// </summary>
            public IReadOnlyList<String> Writes
            {
                get
                {
                    lock (sync)
                        return [.. writes.Select(write => Encoding.ASCII.GetString(write))];
                }
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
            {

                if (Interlocked.Increment(ref inFlight) > 1)
                    Overlapped = true;

                try
                {

                    if (FailNextWrite is { } failure)
                    {
                        FailNextWrite = null;
                        throw failure;
                    }

                    (TaskCompletionSource Started, TaskCompletionSource Release)? held;

                    lock (sync)
                    {
                        held = hold;
                        hold = null;
                    }

                    if (held is { } h)
                    {
                        h.Started.TrySetResult();
                        await h.Release.Task;
                    }

                    lock (sync)
                        writes.Add(Buffer.ToArray());

                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }

            }

            public override Task WriteAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
                => WriteAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

            public override void Write(Byte[] Buffer, Int32 Offset, Int32 Count)
            {
                lock (sync)
                    writes.Add(Buffer.AsSpan(Offset, Count).ToArray());
            }

            public override Task FlushAsync(CancellationToken CancellationToken) => Task.CompletedTask;
            public override void Flush() { }

            public override Boolean  CanRead   => false;
            public override Boolean  CanWrite  => true;
            public override Boolean  CanSeek   => false;
            public override Int64    Length    => throw new NotSupportedException();
            public override Int64    Position  { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override Int32    Read(Byte[] Buffer, Int32 Offset, Int32 Count) => throw new NotSupportedException();
            public override Int64    Seek(Int64 Offset, SeekOrigin Origin)          => throw new NotSupportedException();
            public override void     SetLength(Int64 Value)                         => throw new NotSupportedException();

        }

        /// <summary>
        /// How long a step may take before the test gives up: every step is in
        /// memory, so this only bounds a test that went wrong.
        /// </summary>
        private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

        private static ReadOnlyMemory<Byte> Text(String Text)
            => Encoding.ASCII.GetBytes(Text);

        /// <summary>
        /// Enter, write, and leave, as one writer.
        /// </summary>
        private static async Task WriteAsync(HTTP2FrameWriter Writer, String Frame)
        {
            await Writer.EnterAsync(CancellationToken.None);
            await Writer.WriteAsync(Text(Frame), CancellationToken.None);
            await Writer.ExitAsync(CancellationToken.None);
        }

        #endregion


        #region ALoneWriter_FlushesAsItLeaves(SendOutsideTheLock)

        /// <summary>
        /// With nobody waiting, the writer leaving the lock flushes: what it
        /// wrote is buffered until then, and on the transport after.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task ALoneWriter_FlushesAsItLeaves(Boolean SendOutsideTheLock)
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, SendOutsideTheLock: SendOutsideTheLock);

            await writer.EnterAsync(CancellationToken.None);
            await writer.WriteAsync(Text("a"), CancellationToken.None);

            Assert.That(transport.Writes, Is.Empty, "the frame waits in the buffer while its writer holds the lock");

            await writer.ExitAsync(CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a" }));

        }

        #endregion

        #region AWriterWithAnotherWaiting_LeavesTheFlushToIt()

        /// <summary>
        /// A writer that leaves while another waits for the lock does not flush,
        /// and the one after it flushes both frames, in their order, in one write.
        /// </summary>
        [Test]
        public async Task AWriterWithAnotherWaiting_LeavesTheFlushToIt()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport);

            await writer.EnterAsync(CancellationToken.None);

            var second = writer.EnterAsync(CancellationToken.None);

            Assert.That(second.IsCompleted, Is.False, "the second writer waits for the first");

            await writer.WriteAsync(Text("a"), CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None);
            await second;

            Assert.That(transport.Writes, Is.Empty, "the first writer left its flush to the second");

            await writer.WriteAsync(Text("b"), CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "ab" }));

        }

        #endregion

        #region AWaiterThatGivesUp_NoLongerCounts()

        /// <summary>
        /// A writer that stops waiting is no longer one to leave the flush to:
        /// the writer holding the lock then flushes as it leaves.
        /// </summary>
        [Test]
        public async Task AWaiterThatGivesUp_NoLongerCounts()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport);

            await writer.EnterAsync(CancellationToken.None);

            using var giveUp = new CancellationTokenSource();

            var second = writer.EnterAsync(giveUp.Token);

            giveUp.Cancel();

            Assert.That(async () => await second, Throws.InstanceOf<OperationCanceledException>());

            await writer.WriteAsync(Text("a"), CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a" }));

        }

        #endregion

        #region AWriterWithAnotherWaiting_SendsAsItLeaves()

        /// <summary>
        /// Outside the lock, a writer that leaves while another waits for the
        /// lock sends what it wrote at once, rather than leave it to the one
        /// after it: the next writer does not wait for that send.
        /// </summary>
        [Test]
        public async Task AWriterWithAnotherWaiting_SendsAsItLeaves()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, SendOutsideTheLock: true);

            await writer.EnterAsync(CancellationToken.None);

            var second = writer.EnterAsync(CancellationToken.None);
            Assert.That(second.IsCompleted, Is.False, "the second writer waits for the first");

            await writer.WriteAsync(Text("a"), CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a" }), "the first writer sent as it left");

            await second;
            await writer.WriteAsync(Text("b"), CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a", "b" }));

        }

        #endregion

        #region AForcedFlush_GoesOutWithAWriterWaiting(SendOutsideTheLock)

        /// <summary>
        /// A frame that must be on its way at once, a GOAWAY ahead of the
        /// connection's end, is flushed even with another writer waiting.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AForcedFlush_GoesOutWithAWriterWaiting(Boolean SendOutsideTheLock)
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, SendOutsideTheLock: SendOutsideTheLock);

            await writer.EnterAsync(CancellationToken.None);

            var second = writer.EnterAsync(CancellationToken.None);

            await writer.WriteAsync(Text("goaway"), CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None, Flush: true);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "goaway" }));

            await second;
            await writer.ExitAsync(CancellationToken.None);

        }

        #endregion

        #region ASendRunsOutsideTheLock()

        /// <summary>
        /// While a send waits on the transport, the next writer takes the lock,
        /// writes, and leaves without waiting for it. What it wrote goes out
        /// once that send is done, from the sender, not from its writer.
        /// </summary>
        [Test]
        public async Task ASendRunsOutsideTheLock()
        {

            var transport          = new RecordingStream();
            var writer             = new HTTP2FrameWriter(transport, SendOutsideTheLock: true);
            var (started, release) = transport.HoldNextWrite();

            var first = WriteAsync(writer, "a");
            await started.WaitAsync(StepTimeout);

            var second = WriteAsync(writer, "b");

            Assert.That(await Task.WhenAny(second, Task.Delay(StepTimeout)), Is.SameAs(second),
                        "the second writer took the lock, wrote, and left, while the first one's send waited");
            Assert.That(first.IsCompleted,    Is.False, "the first writer is still sending");
            Assert.That(transport.Writes,     Is.Empty);

            release();
            await first.WaitAsync(StepTimeout);

            Assert.That(transport.Writes,     Is.EqualTo(new[] { "a", "b" }), "the sender sent what was written meanwhile, after its own");
            Assert.That(transport.Overlapped, Is.False);

        }

        #endregion

        #region WritesDuringASend_GoOutTogetherAfterIt()

        /// <summary>
        /// What several writers write during one send goes out after it, in one
        /// write: the sender takes everything that gathered.
        /// </summary>
        [Test]
        public async Task WritesDuringASend_GoOutTogetherAfterIt()
        {

            var transport          = new RecordingStream();
            var writer             = new HTTP2FrameWriter(transport, SendOutsideTheLock: true);
            var (started, release) = transport.HoldNextWrite();

            var first = WriteAsync(writer, "a");
            await started.WaitAsync(StepTimeout);

            await WriteAsync(writer, "b").WaitAsync(StepTimeout);
            await WriteAsync(writer, "c").WaitAsync(StepTimeout);
            await WriteAsync(writer, "d").WaitAsync(StepTimeout);

            release();
            await first.WaitAsync(StepTimeout);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a", "bcd" }));

        }

        #endregion

        #region AWriterLeavingAsASendEnds_IsNeverLeftBehind()

        /// <summary>
        /// A writer that leaves while a send runs either has its frames taken by
        /// that send, or finds it ended and sends them itself. The sender looks
        /// for more and stops in one step, under the gate the writers write
        /// under. It used to stop in a gate of its own, after the look, and a
        /// writer leaving in between found the send still running, started none,
        /// and its frames waited for the next writer: with one request at a
        /// time, until the idle timeout ended the connection. To land in that
        /// gap, the second writer writes and leaves holding the gate, the whole
        /// of it at once, as soon as the sender has looked, round after round.
        /// </summary>
        [Test]
        public async Task AWriterLeavingAsASendEnds_IsNeverLeftBehind()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, SendOutsideTheLock: true);

            Object? Field(String Name)
                => typeof(HTTP2FrameWriter).GetField(Name, BindingFlags.NonPublic | BindingFlags.Instance)!.
                                            GetValue(writer);

            var gate = (Lock) Field("gate")!;

            for (var round = 1; round <= 2_000; round++)
            {

                await writer.EnterAsync(CancellationToken.None);
                await writer.WriteAsync(Text("a"), CancellationToken.None);

                // The first writer starts a send as it leaves, and makes it, on a
                // thread of its own. The second takes the lock meanwhile.
                var first = Task.Run(() => writer.ExitAsync(CancellationToken.None));

                await writer.EnterAsync(CancellationToken.None).WaitAsync(StepTimeout);

                var waiting = Stopwatch.StartNew();

                while (true)
                {

                    gate.Enter();

                    // The sender has not looked yet: it puts the buffer it sent
                    // back as the spare when it does. Try again, to write as soon
                    // after its look as can be.
                    if ((Boolean) Field("sending")! && Field("spare") is null && waiting.Elapsed < StepTimeout)
                    {
                        gate.Exit();
                        continue;
                    }

                    // The transport completes every write at once, so the write
                    // and the leaving, and a send it starts, are done before the
                    // gate is left, on this thread, as the gate requires.
                    var wrote   = writer.WriteAsync(Text("b"), CancellationToken.None);
                    var leaving = writer.ExitAsync(CancellationToken.None);

                    gate.Exit();

                    Assert.That(wrote.IsCompletedSuccessfully && leaving.IsCompletedSuccessfully, Is.True, "the second writer wrote and left at once");

                    break;

                }

                await first.WaitAsync(StepTimeout);

                Assert.That(String.Concat(transport.Writes), Has.Length.EqualTo(2 * round),
                            $"in round {round}, a send ran as the second writer left, and what it wrote is still in the buffer");

            }

        }

        #endregion

        #region AForcedFlush_WaitsForTheSendThatRuns()

        /// <summary>
        /// A frame that must be on its way when its writer leaves, a GOAWAY,
        /// waits for the send that runs, and is out when the call returns, after
        /// what that send carried.
        /// </summary>
        [Test]
        public async Task AForcedFlush_WaitsForTheSendThatRuns()
        {

            var transport          = new RecordingStream();
            var writer             = new HTTP2FrameWriter(transport, SendOutsideTheLock: true);
            var (started, release) = transport.HoldNextWrite();

            var first = WriteAsync(writer, "a");
            await started.WaitAsync(StepTimeout);

            await writer.EnterAsync(CancellationToken.None).WaitAsync(StepTimeout);
            await writer.WriteAsync(Text("goaway"), CancellationToken.None);

            var flushed = writer.ExitAsync(CancellationToken.None, Flush: true);
            Assert.That(flushed.IsCompleted, Is.False, "the forced flush waits for the send that runs");

            release();
            await flushed.WaitAsync(StepTimeout);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a", "goaway" }));

            await first.WaitAsync(StepTimeout);
            Assert.That(transport.Overlapped, Is.False);

        }

        #endregion


        #region AnOpenBatch_HoldsTheFlushUntilItEnds(SendOutsideTheLock)

        /// <summary>
        /// While a batch is open, no writer flushes; its end flushes everything
        /// written meanwhile, in one write.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AnOpenBatch_HoldsTheFlushUntilItEnds(Boolean SendOutsideTheLock)
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, SendOutsideTheLock: SendOutsideTheLock);

            writer.BeginBatch();

            await WriteAsync(writer, "a");
            await WriteAsync(writer, "b");

            Assert.That(transport.Writes, Is.Empty, "nobody flushes while the batch is open");

            await writer.EndBatchAsync(CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "ab" }));

        }

        #endregion

        #region AnAbandonedBatch_LetsTheWritersAfterItFlush(SendOutsideTheLock)

        /// <summary>
        /// A batch given up without its flush stops holding the flushes back:
        /// the next writer flushes, what the batch left included.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AnAbandonedBatch_LetsTheWritersAfterItFlush(Boolean SendOutsideTheLock)
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, SendOutsideTheLock: SendOutsideTheLock);

            writer.BeginBatch();

            await WriteAsync(writer, "a");

            writer.AbandonBatch();

            Assert.That(transport.Writes, Is.Empty, "abandoning the batch flushes nothing");

            await WriteAsync(writer, "b");

            Assert.That(transport.Writes, Is.EqualTo(new[] { "ab" }));

        }

        #endregion

        #region AnOpenBatch_StopsTheSendThatRuns()

        /// <summary>
        /// A batch opened while a send runs holds back what is written after it:
        /// the sender stops once its send is done, and the batch's end sends the
        /// rest.
        /// </summary>
        [Test]
        public async Task AnOpenBatch_StopsTheSendThatRuns()
        {

            var transport          = new RecordingStream();
            var writer             = new HTTP2FrameWriter(transport, SendOutsideTheLock: true);
            var (started, release) = transport.HoldNextWrite();

            var first = WriteAsync(writer, "a");
            await started.WaitAsync(StepTimeout);

            writer.BeginBatch();
            await WriteAsync(writer, "b").WaitAsync(StepTimeout);

            release();
            await first.WaitAsync(StepTimeout);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a" }), "the sender left what the batch holds back");

            await writer.EndBatchAsync(CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a", "b" }));

        }

        #endregion

        #region CloseAsync_FlushesWhatWasLeft(SendOutsideTheLock)

        /// <summary>
        /// What a batch wrote and then left unflushed, because the connection
        /// ended first, goes out with CloseAsync.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task CloseAsync_FlushesWhatWasLeft(Boolean SendOutsideTheLock)
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, SendOutsideTheLock: SendOutsideTheLock);

            writer.BeginBatch();

            await WriteAsync(writer, "last words");

            writer.AbandonBatch();

            await writer.CloseAsync(TimeSpan.FromSeconds(10));

            Assert.That(transport.Writes, Is.EqualTo(new[] { "last words" }));

        }

        #endregion


        #region AFailedFlush_DropsWhatItCarried(SendOutsideTheLock)

        /// <summary>
        /// A flush the transport fails takes its bytes with it: they do not go
        /// out a second time with the next flush, behind a GOAWAY say. After a
        /// failed write the transport's state is unknown, and a frame sent twice,
        /// or half of one again, would garble everything after it.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AFailedFlush_DropsWhatItCarried(Boolean SendOutsideTheLock)
        {

            var transport = new RecordingStream { FailNextWrite = new IOException("The write failed on purpose") };
            var writer    = new HTTP2FrameWriter(transport, SendOutsideTheLock: SendOutsideTheLock);

            Assert.That(async () => await WriteAsync(writer, "lost"), Throws.InstanceOf<IOException>());

            await WriteAsync(writer, "goaway");

            Assert.That(transport.Writes, Is.EqualTo(new[] { "goaway" }));

        }

        #endregion

        #region AWriteLargerThanTheBuffer_FollowsWhatWasBuffered(SendOutsideTheLock)

        /// <summary>
        /// A write larger than the buffer, a full DATA frame, still goes out
        /// after what was buffered before it, and nothing is lost or reordered.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AWriteLargerThanTheBuffer_FollowsWhatWasBuffered(Boolean SendOutsideTheLock)
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, BufferSize: 16, SendOutsideTheLock: SendOutsideTheLock);
            var large     = new String('c', 40);

            await writer.EnterAsync(CancellationToken.None);
            await writer.WriteAsync(Text("ab"),  CancellationToken.None);
            await writer.WriteAsync(Text(large), CancellationToken.None);
            await writer.WriteAsync(Text("d"),   CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None);

            Assert.That(String.Concat(transport.Writes), Is.EqualTo("ab" + large + "d"));

        }

        #endregion

        #region AWriteLargerThanTheBuffer_WaitsForTheSendThatRuns()

        /// <summary>
        /// A write too large for the buffer goes to the transport itself, and so
        /// waits for the send that runs: the transport never has two writes at
        /// once, and nothing is reordered.
        /// </summary>
        [Test]
        public async Task AWriteLargerThanTheBuffer_WaitsForTheSendThatRuns()
        {

            var transport          = new RecordingStream();
            var writer             = new HTTP2FrameWriter(transport, BufferSize: 16, SendOutsideTheLock: true);
            var large              = new String('c', 40);
            var (started, release) = transport.HoldNextWrite();

            var first = WriteAsync(writer, "a");
            await started.WaitAsync(StepTimeout);

            var second = WriteAsync(writer, large);
            Assert.That(second.IsCompleted, Is.False, "the large write waits for the send that runs");

            release();
            await Task.WhenAll(first, second).WaitAsync(StepTimeout);

            Assert.That(String.Concat(transport.Writes), Is.EqualTo("a" + large));
            Assert.That(transport.Overlapped,            Is.False, "the transport never had two writes at once");

        }

        #endregion

        #region WithoutABuffer_EveryWriteGoesThroughAtOnce(SendOutsideTheLock)

        /// <summary>
        /// With a buffer size of 0, each write reaches the transport as it is
        /// made, as it did before there was a buffer.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task WithoutABuffer_EveryWriteGoesThroughAtOnce(Boolean SendOutsideTheLock)
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, BufferSize: 0, SendOutsideTheLock: SendOutsideTheLock);

            await writer.EnterAsync(CancellationToken.None);
            await writer.WriteAsync(Text("a"), CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a" }), "written through before the writer leaves");

            await writer.WriteAsync(Text("b"), CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "a", "b" }));

        }

        #endregion

    }

}
