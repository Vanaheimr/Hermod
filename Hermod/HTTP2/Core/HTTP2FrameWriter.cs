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
    /// buffered, and the buffer flushed only once no other writer is waiting;
    /// or, with <c>SendOutsideTheLock</c>, sent outside the lock as soon as the
    /// transport is free.
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
    /// The server sends outside the lock. A send costs about as long as the
    /// writes of dozens of frames, 30 to 50 µs on loopback with TLS. Made under
    /// the lock, every writer behind it waited for it, and only the last of
    /// them sent: the first response of a burst went out with the last. Now a
    /// writer leaving the lock starts a send if none is running, and the next
    /// writer takes the lock meanwhile. What the writers write during a send
    /// gathers in the buffer, and the sender takes it once its send is done,
    /// until nothing more has gathered. It looks for more and stops in one
    /// step, under the gate the writers write under, so that a writer leaving
    /// during a send either has its frames taken by that send, or finds it
    /// ended and starts its own. The writer that starts a send makes it,
    /// so still nobody hands a write to another thread. A write too large for
    /// the buffer, and a flush that must be done when it returns, wait for the
    /// send that runs: the transport has one writer at a time.
    ///
    /// The client keeps the flush under the lock. Its request starts in line
    /// share one batch already, which the last of them flushes; its tests build
    /// their races on a write that holds the lock while the transport holds the
    /// write; and sending outside the lock has not been measured for it.
    ///
    /// A batch (<see cref="BeginBatch"/>) holds the flushes and the sends back
    /// for as long as it is open. A loop about to write many frames in a row
    /// opens one, and its end sends them all.
    ///
    /// The order of the frames is the order of the writers, so nothing that
    /// relied on one frame preceding another changes. What does change is that
    /// a frame may still be in the buffer when its writer has left the lock;
    /// it goes out with the flush of whoever came last, or with the next send.
    /// A writer that gives up waiting, as all of them do when the connection
    /// ends, may have been that one, or may leave a batch's frames behind, so
    /// <see cref="CloseAsync"/> flushes once more at the end.
    ///
    /// What a flush or a send carries is dropped from the buffer whether or not
    /// the transport takes it. After a failed write the transport's state is
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
        private readonly Boolean        sendOutsideTheLock;
        private readonly SemaphoreSlim  writeLock = new(1, 1);

        /// <summary>
        /// Allocated with the first write that is buffered.
        /// </summary>
        private Byte[]?  buffer;
        private Int32    buffered;

        /// <summary>
        /// Writers holding the lock or waiting for it, and open batches. Only
        /// while the flush is made under the lock.
        /// </summary>
        private Int32    pending;

        #region Sending outside the lock

        /// <summary>
        /// Guards the buffer between the writers and the sender, and the state
        /// below. Held for no more than a copy, never across a write.
        /// </summary>
        private readonly Lock  gate = new();

        /// <summary>
        /// The buffer the last send carried, for the writers to use next.
        /// </summary>
        private Byte[]?  spare;

        /// <summary>
        /// Open batches.
        /// </summary>
        private Int32    batches;

        /// <summary>
        /// Whether the transport is being written to: by a send, or by a writer
        /// that holds the lock and waited for the send to end.
        /// </summary>
        private Boolean  sending;

        /// <summary>
        /// Completes once the transport is free again. Made by the first writer
        /// to wait for that, so that a send nobody waits for allocates nothing.
        /// </summary>
        private TaskCompletionSource?  sendEnded;

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// The write side of the given transport.
        /// </summary>
        /// <param name="Transport">The connection's byte transport.</param>
        /// <param name="BufferSize">The size of the buffer, or 0 to write every frame through to the transport at once, as each write is made.</param>
        /// <param name="SendOutsideTheLock">Whether the buffer is sent outside the lock, as soon as the transport is free, rather than flushed under it by the last writer waiting.</param>
        public HTTP2FrameWriter(Stream   Transport,
                                Int32    BufferSize           = DefaultBufferSize,
                                Boolean  SendOutsideTheLock   = false)
        {

            ArgumentOutOfRangeException.ThrowIfNegative(BufferSize);

            this.transport           = Transport;
            this.bufferSize          = BufferSize;
            this.sendOutsideTheLock  = SendOutsideTheLock;

        }

        #endregion


        #region EnterAsync(CancellationToken)

        /// <summary>
        /// Wait for the lock, as one more writer.
        /// </summary>
        public async Task EnterAsync(CancellationToken CancellationToken)
        {

            if (sendOutsideTheLock)
            {
                await writeLock.WaitAsync(CancellationToken);
                return;
            }

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
        /// than the whole buffer, or else into the emptied buffer. Outside the
        /// lock, that waits for the send that runs.
        /// </summary>
        public async ValueTask WriteAsync(ReadOnlyMemory<Byte> Bytes, CancellationToken CancellationToken)
        {

            if (bufferSize == 0)
            {
                await transport.WriteAsync(Bytes, CancellationToken);
                return;
            }

            if (sendOutsideTheLock)
            {

                lock (gate)
                {
                    if (buffered + Bytes.Length <= bufferSize)
                    {
                        Append(Bytes.Span);
                        return;
                    }
                }

                await WriteThroughAsync(Bytes, CancellationToken);
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

            Append(Bytes.Span);

        }

        #endregion

        #region ExitAsync(CancellationToken, Flush = false)

        /// <summary>
        /// Leave the lock, and flush the buffer unless another writer is waiting
        /// and will flush it, or <paramref name="Flush"/> asks for it regardless.
        /// Outside the lock: send the buffer, unless a send is running, which
        /// takes it next, or a batch is open, whose end sends it.
        /// </summary>
        /// <param name="Flush">Flush even if another writer is waiting, or a batch is open: for a frame that must be on its way when the call returns, such as a GOAWAY ahead of the connection's end.</param>
        public async Task ExitAsync(CancellationToken  CancellationToken,
                                    Boolean            Flush = false)
        {

            if (sendOutsideTheLock && !Flush)
            {

                var send = TakeIfIdle();

                writeLock.Release();

                if (send is { } taken)
                    await SendAsync(taken.Bytes, taken.Count, CancellationToken);

                return;

            }

            try
            {
                if (sendOutsideTheLock || Interlocked.Decrement(ref pending) == 0 || Flush)
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
        /// whose writer keeps the lock. Outside the lock, once the send that
        /// runs is done.
        /// </summary>
        public async Task FlushAsync(CancellationToken CancellationToken)
        {

            if (sendOutsideTheLock)
            {
                await FlushOnceSentAsync(CancellationToken);
                return;
            }

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

        #region (private) Append(Bytes)

        /// <summary>
        /// Copy into the buffer, which has room: under the lock, and outside it
        /// also under the gate.
        /// </summary>
        private void Append(ReadOnlySpan<Byte> Bytes)
        {

            buffer ??= new Byte[bufferSize];

            Bytes.CopyTo(buffer.AsSpan(buffered));
            buffered += Bytes.Length;

        }

        #endregion


        #region BeginBatch()

        /// <summary>
        /// Open a batch: until it ends, nobody flushes, unless asked to.
        /// </summary>
        public void BeginBatch()
        {

            if (!sendOutsideTheLock)
            {
                Interlocked.Increment(ref pending);
                return;
            }

            lock (gate)
                batches++;

        }

        #endregion

        #region EndBatchAsync(CancellationToken)

        /// <summary>
        /// End a batch, and flush unless another writer is waiting and will.
        /// Outside the lock: send what it held back, unless a send is running,
        /// which takes it next, or another batch is open.
        /// </summary>
        public async Task EndBatchAsync(CancellationToken CancellationToken)
        {

            try
            {
                await writeLock.WaitAsync(CancellationToken);
            }
            catch
            {
                AbandonBatch();
                throw;
            }

            // Under the lock, the batch's count in pending stands in for this
            // writer's, and ExitAsync takes it off.
            if (sendOutsideTheLock)
                lock (gate)
                    batches--;

            await ExitAsync(CancellationToken);

        }

        #endregion

        #region AbandonBatch()

        /// <summary>
        /// End a batch without flushing, for a writer that stops altogether: the
        /// writers after it flush again, and CloseAsync flushes what it left.
        /// </summary>
        public void AbandonBatch()
        {

            if (!sendOutsideTheLock)
            {
                Interlocked.Decrement(ref pending);
                return;
            }

            lock (gate)
                batches--;

        }

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


        #region (private) Take()

        /// <summary>
        /// Under the gate: take what has gathered, and leave the writers an
        /// empty buffer, the one the last send carried if there is one.
        /// </summary>
        private (Byte[] Bytes, Int32 Count) Take()
        {

            var bytes = buffer ?? [];
            var count = buffered;

            if (count > 0)
            {
                buffer = spare;
                spare  = null;
            }

            buffered = 0;

            return (bytes, count);

        }

        #endregion

        #region (private) TakeIfIdle()

        /// <summary>
        /// Start a send, if there is something to send, no send is running, and
        /// no batch is open: the transport is then the caller's to write to.
        /// </summary>
        private (Byte[] Bytes, Int32 Count)? TakeIfIdle()
        {

            lock (gate)
            {

                if (buffered == 0 || sending || batches > 0)
                    return null;

                sending = true;

                return Take();

            }

        }

        #endregion

        #region (private) SendAsync(Bytes, Count, CancellationToken)

        /// <summary>
        /// Outside the lock: send what was taken, then what gathered meanwhile,
        /// until nothing has, or a batch holds it back.
        /// </summary>
        private async Task SendAsync(Byte[] Bytes, Int32 Count, CancellationToken CancellationToken)
        {

            while (true)
            {

                TaskCompletionSource? ended;

                try
                {
                    await transport.WriteAsync(Bytes.AsMemory(0, Count), CancellationToken);
                    await transport.FlushAsync(CancellationToken);
                }
                catch
                {

                    lock (gate)
                    {
                        spare ??= Bytes;
                        ended   = EndSend();
                    }

                    ended?.TrySetResult();
                    throw;

                }

                lock (gate)
                {

                    spare ??= Bytes;

                    if (buffered > 0 && batches == 0)
                    {
                        (Bytes, Count) = Take();
                        continue;
                    }

                    // Stop under the same gate as the look above. A writer that
                    // leaves after it finds no send running, and starts its own.
                    // In a gate of its own, a writer leaving in between found
                    // this send still running and started none, and what it had
                    // written waited for the next writer, if one came. With 1
                    // concurrent request none did, until the idle timeout.
                    ended = EndSend();

                }

                ended?.TrySetResult();
                return;

            }

        }

        #endregion

        #region (private) WriteThroughAsync(Bytes, CancellationToken)

        /// <summary>
        /// Under the lock, for a write that does not fit: once the send that
        /// runs is done, write the buffer, and then the write itself if it is
        /// larger than the whole buffer, or else into the emptied buffer.
        /// </summary>
        private async ValueTask WriteThroughAsync(ReadOnlyMemory<Byte> Bytes, CancellationToken CancellationToken)
        {

            await AcquireTransportAsync(CancellationToken);

            try
            {

                await WriteTakenAsync(CancellationToken);

                if (Bytes.Length >= bufferSize)
                {
                    await transport.WriteAsync(Bytes, CancellationToken);
                    await transport.FlushAsync(CancellationToken);
                }
                else
                    lock (gate)
                        Append(Bytes.Span);

            }
            finally
            {
                ReleaseTransport();
            }

        }

        #endregion

        #region (private) FlushOnceSentAsync(CancellationToken)

        /// <summary>
        /// Under the lock: once the send that runs is done, write the buffer,
        /// and flush.
        /// </summary>
        private async Task FlushOnceSentAsync(CancellationToken CancellationToken)
        {

            await AcquireTransportAsync(CancellationToken);

            try
            {
                await WriteTakenAsync(CancellationToken);
                await transport.FlushAsync(CancellationToken);
            }
            finally
            {
                ReleaseTransport();
            }

        }

        #endregion

        #region (private) WriteTakenAsync(CancellationToken)

        /// <summary>
        /// With the transport acquired: take the buffer, and write it.
        /// </summary>
        private async Task WriteTakenAsync(CancellationToken CancellationToken)
        {

            Byte[] bytes;
            Int32  count;

            lock (gate)
                (bytes, count) = Take();

            if (count == 0)
                return;

            try
            {
                await transport.WriteAsync(bytes.AsMemory(0, count), CancellationToken);
            }
            finally
            {
                lock (gate)
                    spare ??= bytes;
            }

        }

        #endregion

        #region (private) AcquireTransportAsync(CancellationToken) / ReleaseTransport() / EndSend()

        /// <summary>
        /// Under the lock: wait for the send that runs to end, and take the
        /// transport. Nobody starts another meanwhile: a send is started by a
        /// writer leaving the lock, or continued by the sender, which ends as
        /// soon as nothing new has been written, and nothing is while the lock
        /// is held.
        /// </summary>
        private async Task AcquireTransportAsync(CancellationToken CancellationToken)
        {

            while (true)
            {

                Task ended;

                lock (gate)
                {

                    if (!sending)
                    {
                        sending = true;
                        return;
                    }

                    sendEnded ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    ended       = sendEnded.Task;

                }

                // A send that failed failed its own sender; what comes next may
                // still be written, as it was when the flush was under the lock.
                await ended.WaitAsync(CancellationToken);

            }

        }

        /// <summary>
        /// Give the transport back, and wake whoever waits for it.
        /// </summary>
        private void ReleaseTransport()
        {

            TaskCompletionSource? ended;

            lock (gate)
                ended = EndSend();

            ended?.TrySetResult();

        }

        /// <summary>
        /// Under the gate: mark the transport free, and hand back the signal for
        /// whoever waits for that, to be set once the gate is left.
        /// </summary>
        private TaskCompletionSource? EndSend()
        {

            var ended = sendEnded;

            sending   = false;
            sendEnded = null;

            return ended;

        }

        #endregion

    }

}
