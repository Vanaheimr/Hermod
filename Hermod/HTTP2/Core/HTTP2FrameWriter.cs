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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// The write side of one connection: one writer at a time, its frames
    /// buffered, and the buffer flushed only once no other writer is waiting.
    ///
    /// Each frame used to be written and flushed on its own, and every flush is
    /// a write to the transport: a TLS record and a send. Under load that made
    /// the flush the cost of a response. The writers queue for the lock anyway,
    /// so the one leaving it can tell whether another is about to follow, and
    /// leave the flush to that one. The last in line flushes for all of them.
    /// Nobody hands a write to another thread, which is what made an earlier
    /// writer queue slower than no queue at all: the thread that holds the lock
    /// writes, as before, and only the flushes become fewer.
    ///
    /// A batch (<see cref="BeginBatch"/>) counts as a writer that is waiting
    /// for as long as it is open, so nobody flushes meanwhile. A loop about to
    /// write many frames in a row opens one, and its end flushes them all.
    ///
    /// The order of the frames is the order of the writers, so nothing that
    /// relied on one frame preceding another changes. What does change is that
    /// a frame may still be in the buffer when its writer has left the lock;
    /// it goes out with the flush of whoever came last. A writer that gives up
    /// waiting, as all of them do when the connection ends, may have been that
    /// one, so <see cref="CloseAsync"/> flushes once more at the end.
    ///
    /// What a flush carries is dropped from the buffer whether or not the
    /// transport takes it. After a failed write the transport's state is
    /// unknown, and those bytes must not go out a second time, behind whatever
    /// is written next, a GOAWAY say.
    /// </summary>
    internal sealed class HTTP2FrameWriter
    {

        #region Data

        /// <summary>
        /// The default size of the buffer: one TLS record's worth of plaintext.
        /// A larger write, such as a full DATA frame, goes past the buffer to
        /// the transport, after what was buffered before it.
        /// </summary>
        public const Int32 DefaultBufferSize = 16 * 1024;

        private readonly Stream         transport;
        private readonly Int32          bufferSize;
        private readonly SemaphoreSlim  writeLock = new(1, 1);

        /// <summary>
        /// Allocated with the first write that is buffered.
        /// </summary>
        private Byte[]?  buffer;
        private Int32    buffered;

        /// <summary>
        /// Writers holding the lock or waiting for it, and open batches.
        /// </summary>
        private Int32    pending;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// The write side of the given transport.
        /// </summary>
        /// <param name="Transport">The connection's byte transport.</param>
        /// <param name="BufferSize">The size of the buffer, or 0 to write every frame through to the transport at once, as each write is made.</param>
        public HTTP2FrameWriter(Stream  Transport,
                                Int32   BufferSize = DefaultBufferSize)
        {

            ArgumentOutOfRangeException.ThrowIfNegative(BufferSize);

            this.transport   = Transport;
            this.bufferSize  = BufferSize;

        }

        #endregion


        #region EnterAsync(CancellationToken)

        /// <summary>
        /// Wait for the lock, as one more writer.
        /// </summary>
        public async Task EnterAsync(CancellationToken CancellationToken)
        {

            Interlocked.Increment(ref pending);

            try
            {
                await writeLock.WaitAsync(CancellationToken);
            }
            catch
            {
                // Whoever left the lock last may have left its flush to this
                // writer; with it gone, CloseAsync makes up for it.
                Interlocked.Decrement(ref pending);
                throw;
            }

        }

        #endregion

        #region WriteAsync(Bytes, CancellationToken)

        /// <summary>
        /// Write, under the lock, into the buffer. What does not fit goes out
        /// first: the buffer as it is, then the write itself if it is larger
        /// than the whole buffer, or else into the emptied buffer.
        /// </summary>
        public async ValueTask WriteAsync(ReadOnlyMemory<Byte> Bytes, CancellationToken CancellationToken)
        {

            if (bufferSize == 0)
            {
                await transport.WriteAsync(Bytes, CancellationToken);
                return;
            }

            if (buffered + Bytes.Length > bufferSize)
            {

                await WriteBufferAsync(CancellationToken);

                if (Bytes.Length >= bufferSize)
                {
                    await transport.WriteAsync(Bytes, CancellationToken);
                    return;
                }

            }

            buffer ??= new Byte[bufferSize];

            Bytes.Span.CopyTo(buffer.AsSpan(buffered));
            buffered += Bytes.Length;

        }

        #endregion

        #region ExitAsync(CancellationToken, Flush = false)

        /// <summary>
        /// Leave the lock, and flush the buffer unless another writer is waiting
        /// and will flush it, or <paramref name="Flush"/> asks for it regardless.
        /// </summary>
        /// <param name="Flush">Flush even if another writer is waiting: for a frame that must be on its way when the call returns, such as a GOAWAY ahead of the connection's end.</param>
        public async Task ExitAsync(CancellationToken  CancellationToken,
                                    Boolean            Flush = false)
        {

            try
            {
                if (Interlocked.Decrement(ref pending) == 0 || Flush)
                    await FlushAsync(CancellationToken);
            }
            finally
            {
                writeLock.Release();
            }

        }

        #endregion

        #region FlushAsync(CancellationToken)

        /// <summary>
        /// Flush now, under the lock: what ExitAsync does for the last writer,
        /// and what a last frame needs after which nothing else may be written,
        /// whose writer keeps the lock.
        /// </summary>
        public async Task FlushAsync(CancellationToken CancellationToken)
        {

            await WriteBufferAsync(CancellationToken);

            await transport.FlushAsync(CancellationToken);

        }

        #endregion

        #region (private) WriteBufferAsync(CancellationToken)

        /// <summary>
        /// Hand the buffer to the transport, and empty it — before the write, so
        /// that a failed write leaves nothing behind to go out again.
        /// </summary>
        private async ValueTask WriteBufferAsync(CancellationToken CancellationToken)
        {

            if (buffered == 0)
                return;

            var count = buffered;
            buffered  = 0;

            await transport.WriteAsync(buffer.AsMemory(0, count), CancellationToken);

        }

        #endregion


        #region BeginBatch()

        /// <summary>
        /// Open a batch: until it ends, nobody flushes, unless asked to.
        /// </summary>
        public void BeginBatch()

            => Interlocked.Increment(ref pending);

        #endregion

        #region EndBatchAsync(CancellationToken)

        /// <summary>
        /// End a batch, and flush unless another writer is waiting and will.
        /// </summary>
        public async Task EndBatchAsync(CancellationToken CancellationToken)
        {

            try
            {
                await writeLock.WaitAsync(CancellationToken);
            }
            catch
            {
                Interlocked.Decrement(ref pending);
                throw;
            }

            await ExitAsync(CancellationToken);

        }

        #endregion

        #region AbandonBatch()

        /// <summary>
        /// End a batch without flushing, for a writer that stops altogether: the
        /// writers after it flush again, and CloseAsync flushes what it left.
        /// </summary>
        public void AbandonBatch()

            => Interlocked.Decrement(ref pending);

        #endregion


        #region CloseAsync(Timeout)

        /// <summary>
        /// Flush what is still in the buffer, once the connection ends, before
        /// the transport is closed. Best effort, and bounded by
        /// <paramref name="Timeout"/>: a transport that is gone, or a peer that
        /// reads nothing more, gets nothing.
        /// </summary>
        public async Task CloseAsync(TimeSpan Timeout)
        {

            if (bufferSize == 0)
                return;

            try
            {

                using var timeout = new CancellationTokenSource(Timeout);

                await writeLock.WaitAsync(timeout.Token);

                try
                {
                    await FlushAsync(timeout.Token);
                }
                finally
                {
                    writeLock.Release();
                }

            }
            catch
            {
                // Timeout, cancellation, or a closed transport — best effort
            }

        }

        #endregion

    }

}
