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

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The write side of a connection (<see cref="HTTP2FrameWriter"/>): one
    /// writer at a time, its frames buffered, and the flush left to the last of
    /// the writers waiting. What reaches the transport is checked per write, so
    /// a flush the writer should have left to another shows up as a write of
    /// its own.
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

            /// <summary>
            /// Fail the next write with this, as a broken transport would, and
            /// take none of it.
            /// </summary>
            public Exception?              FailNextWrite { get; set; }

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

            public override ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
            {

                if (FailNextWrite is { } failure)
                {
                    FailNextWrite = null;
                    return ValueTask.FromException(failure);
                }

                lock (sync)
                    writes.Add(Buffer.ToArray());

                return ValueTask.CompletedTask;

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


        #region ALoneWriter_FlushesAsItLeaves()

        /// <summary>
        /// With nobody waiting, the writer leaving the lock flushes: what it
        /// wrote is buffered until then, and on the transport after.
        /// </summary>
        [Test]
        public async Task ALoneWriter_FlushesAsItLeaves()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport);

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

        #region AForcedFlush_GoesOutWithAWriterWaiting()

        /// <summary>
        /// A frame that must be on its way at once, a GOAWAY ahead of the
        /// connection's end, is flushed even with another writer waiting.
        /// </summary>
        [Test]
        public async Task AForcedFlush_GoesOutWithAWriterWaiting()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport);

            await writer.EnterAsync(CancellationToken.None);

            var second = writer.EnterAsync(CancellationToken.None);

            await writer.WriteAsync(Text("goaway"), CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None, Flush: true);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "goaway" }));

            await second;
            await writer.ExitAsync(CancellationToken.None);

        }

        #endregion


        #region AnOpenBatch_HoldsTheFlushUntilItEnds()

        /// <summary>
        /// While a batch is open, no writer flushes; its end flushes everything
        /// written meanwhile, in one write.
        /// </summary>
        [Test]
        public async Task AnOpenBatch_HoldsTheFlushUntilItEnds()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport);

            writer.BeginBatch();

            await WriteAsync(writer, "a");
            await WriteAsync(writer, "b");

            Assert.That(transport.Writes, Is.Empty, "nobody flushes while the batch is open");

            await writer.EndBatchAsync(CancellationToken.None);

            Assert.That(transport.Writes, Is.EqualTo(new[] { "ab" }));

        }

        #endregion

        #region AnAbandonedBatch_LetsTheWritersAfterItFlush()

        /// <summary>
        /// A batch given up without its flush stops holding the flushes back:
        /// the next writer flushes, what the batch left included.
        /// </summary>
        [Test]
        public async Task AnAbandonedBatch_LetsTheWritersAfterItFlush()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport);

            writer.BeginBatch();

            await WriteAsync(writer, "a");

            writer.AbandonBatch();

            Assert.That(transport.Writes, Is.Empty, "abandoning the batch flushes nothing");

            await WriteAsync(writer, "b");

            Assert.That(transport.Writes, Is.EqualTo(new[] { "ab" }));

        }

        #endregion

        #region CloseAsync_FlushesWhatWasLeft()

        /// <summary>
        /// What a batch wrote and then left unflushed, because the connection
        /// ended first, goes out with CloseAsync.
        /// </summary>
        [Test]
        public async Task CloseAsync_FlushesWhatWasLeft()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport);

            writer.BeginBatch();

            await WriteAsync(writer, "last words");

            writer.AbandonBatch();

            await writer.CloseAsync(TimeSpan.FromSeconds(10));

            Assert.That(transport.Writes, Is.EqualTo(new[] { "last words" }));

        }

        #endregion


        #region AFailedFlush_DropsWhatItCarried()

        /// <summary>
        /// A flush the transport fails takes its bytes with it: they do not go
        /// out a second time with the next flush, behind a GOAWAY say. After a
        /// failed write the transport's state is unknown, and a frame sent twice,
        /// or half of one again, would garble everything after it.
        /// </summary>
        [Test]
        public async Task AFailedFlush_DropsWhatItCarried()
        {

            var transport = new RecordingStream { FailNextWrite = new IOException("The write failed on purpose") };
            var writer    = new HTTP2FrameWriter(transport);

            Assert.That(async () => await WriteAsync(writer, "lost"), Throws.InstanceOf<IOException>());

            await WriteAsync(writer, "goaway");

            Assert.That(transport.Writes, Is.EqualTo(new[] { "goaway" }));

        }

        #endregion

        #region AWriteLargerThanTheBuffer_FollowsWhatWasBuffered()

        /// <summary>
        /// A write larger than the buffer, a full DATA frame, still goes out
        /// after what was buffered before it, and nothing is lost or reordered.
        /// </summary>
        [Test]
        public async Task AWriteLargerThanTheBuffer_FollowsWhatWasBuffered()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, BufferSize: 16);
            var large     = new String('c', 40);

            await writer.EnterAsync(CancellationToken.None);
            await writer.WriteAsync(Text("ab"),  CancellationToken.None);
            await writer.WriteAsync(Text(large), CancellationToken.None);
            await writer.WriteAsync(Text("d"),   CancellationToken.None);
            await writer.ExitAsync(CancellationToken.None);

            Assert.That(String.Concat(transport.Writes), Is.EqualTo("ab" + large + "d"));

        }

        #endregion

        #region WithoutABuffer_EveryWriteGoesThroughAtOnce()

        /// <summary>
        /// With a buffer size of 0, each write reaches the transport as it is
        /// made, as it did before there was a buffer.
        /// </summary>
        [Test]
        public async Task WithoutABuffer_EveryWriteGoesThroughAtOnce()
        {

            var transport = new RecordingStream();
            var writer    = new HTTP2FrameWriter(transport, BufferSize: 0);

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
