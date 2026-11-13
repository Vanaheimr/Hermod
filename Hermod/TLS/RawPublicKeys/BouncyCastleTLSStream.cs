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

using System.Buffers;

using Org.BouncyCastle.Tls;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A stream over a TLS connection that BouncyCastle's TLS protocol
    /// encrypts: what is written is turned into records and sent over the
    /// stream underneath, and what arrives there is decrypted and read.
    /// </summary>
    /// <remarks>
    /// BouncyCastle's own stream blocks, and what a blocking stream gives
    /// for ReadAsync and WriteAsync is one operation at a time: a WebSocket
    /// that waits for a frame could not send one. So the protocol is run
    /// here in its non-blocking mode, in which it takes and gives bytes and
    /// does no I/O of its own, and this stream does the I/O: a read and a
    /// write may be under way at the same time, as on an SslStream, and no
    /// two reads and no two writes.
    ///
    /// The protocol object is not thread-safe. It is only touched under the
    /// protocol lock, and never across an await. Whatever records it has to
    /// send - what was written, but also its answer to something it read,
    /// an alert or a key update - go out under the write semaphore, in the
    /// order in which they were made.
    /// </remarks>
    public sealed class BouncyCastleTLSStream : Stream
    {

        #region Data

        // A TLS record is at most 2^14 bytes of plaintext and 256 more of what encrypts it, behind 5 bytes of header.
        private const    Int32          NetworkBufferSize  = 5 + 16384 + 256;

        private readonly Stream         inner;
        private readonly TlsProtocol    protocol;
        private readonly Boolean        leaveInnerStreamOpen;

        private readonly Lock           protocolLock       = new();
        private readonly SemaphoreSlim  readSemaphore      = new (1, 1);
        private readonly SemaphoreSlim  writeSemaphore     = new (1, 1);
        private readonly Byte[]         networkBuffer      = new Byte[NetworkBufferSize];

        private          Int32          disposed;

        #endregion

        #region Properties

        public override Boolean  CanRead      => inner.CanRead;
        public override Boolean  CanWrite     => inner.CanWrite;
        public override Boolean  CanSeek      => false;
        public override Boolean  CanTimeout   => inner.CanTimeout;
        public override Int64    Length       => throw new NotSupportedException();

        public override Int64    Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override Int32 ReadTimeout
        {
            get => inner.ReadTimeout;
            set => inner.ReadTimeout = value;
        }

        public override Int32 WriteTimeout
        {
            get => inner.WriteTimeout;
            set => inner.WriteTimeout = value;
        }

        /// <summary>
        /// Whether the handshake is done and the connection is open.
        /// </summary>
        public Boolean IsAuthenticated
        {
            get
            {
                lock (protocolLock)
                    return protocol.IsConnected;
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a stream over the given TLS protocol, which must be in its
        /// non-blocking mode and have its handshake begun.
        /// </summary>
        /// <param name="Inner">The stream the TLS records go over.</param>
        /// <param name="Protocol">The TLS protocol.</param>
        /// <param name="LeaveInnerStreamOpen">Whether closing this leaves the stream underneath open.</param>
        internal BouncyCastleTLSStream(Stream       Inner,
                                       TlsProtocol  Protocol,
                                       Boolean      LeaveInnerStreamOpen)
        {

            this.inner                 = Inner;
            this.protocol              = Protocol;
            this.leaveInnerStreamOpen  = LeaveInnerStreamOpen;

        }

        #endregion


        #region (internal) HandshakeAsync(IsComplete, CancellationToken)

        /// <summary>
        /// Run the handshake to its end: send what the protocol has to say,
        /// give it what the peer answers, until it is done or fails.
        /// </summary>
        /// <param name="IsComplete">Whether the handshake is complete: asked under the protocol lock.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        internal async Task HandshakeAsync(Func<Boolean>      IsComplete,
                                           CancellationToken  CancellationToken)
        {

            try
            {

                while (true)
                {

                    await FlushProtocolOutputAsync(CancellationToken).ConfigureAwait(false);

                    lock (protocolLock)
                    {
                        if (IsComplete())
                            return;
                    }

                    var read = await inner.ReadAsync(networkBuffer, CancellationToken).ConfigureAwait(false);

                    if (read == 0)
                        throw new IOException("The connection was closed during the TLS handshake.");

                    lock (protocolLock)
                        protocol.OfferInput(networkBuffer, 0, read);

                }

            }
            catch
            {

                // What ended the handshake on this side made an alert: the peer is told, where it can still be.
                try
                {
                    using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await FlushProtocolOutputAsync(bounded.Token).ConfigureAwait(false);
                }
                catch
                { }

                throw;

            }

        }

        #endregion

        #region (private) TakeProtocolOutput()

        /// <summary>
        /// The records the protocol has to send, taken from it. Called under
        /// the write semaphore, so that what is taken first is sent first.
        /// </summary>
        private Byte[]? TakeProtocolOutput()
        {
            lock (protocolLock)
            {

                var available = protocol.GetAvailableOutputBytes();

                if (available <= 0)
                    return null;

                var output = new Byte[available];
                var taken  = protocol.ReadOutput(output, 0, available);

                return taken == available
                           ? output
                           : output.AsSpan(0, taken).ToArray();

            }
        }

        #endregion

        #region (private) FlushProtocolOutput(Async)(...)

        private async ValueTask FlushProtocolOutputAsync(CancellationToken CancellationToken)
        {

            await writeSemaphore.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {
                if (TakeProtocolOutput() is { } output)
                    await inner.WriteAsync(output, CancellationToken).ConfigureAwait(false);
            }
            finally
            {
                writeSemaphore.Release();
            }

        }

        private void FlushProtocolOutput()
        {

            writeSemaphore.Wait();

            try
            {
                if (TakeProtocolOutput() is { } output)
                    inner.Write(output, 0, output.Length);
            }
            finally
            {
                writeSemaphore.Release();
            }

        }

        #endregion


        #region Reading

        /// <summary>
        /// What is decrypted and waiting, if anything: -1 where the protocol
        /// has nothing and more has to be read from the network, 0 at the end.
        /// </summary>
        private Int32 TakePlaintext(Span<Byte> Buffer)
        {
            lock (protocolLock)
            {

                var available = protocol.GetAvailableInputBytes();

                if (available > 0)
                {

                    var take   = Math.Min(available, Buffer.Length);
                    var rented = ArrayPool<Byte>.Shared.Rent(take);

                    try
                    {
                        var taken = protocol.ReadInput(rented, 0, take);
                        rented.AsSpan(0, taken).CopyTo(Buffer);
                        return taken;
                    }
                    finally
                    {
                        ArrayPool<Byte>.Shared.Return(rented, clearArray: true);
                    }

                }

                // The peer closed the connection, or this side did.
                return protocol.IsClosed
                           ? 0
                           : -1;

            }
        }

        /// <summary>
        /// Give the protocol what came from the network. Whether it has
        /// something to send in answer.
        /// </summary>
        private Boolean OfferNetworkInput(Int32 Count)
        {
            lock (protocolLock)
            {

                if (protocol.IsClosed)
                    return false;

                protocol.OfferInput(networkBuffer, 0, Count);

                return protocol.GetAvailableOutputBytes() > 0;

            }
        }

        public override async ValueTask<Int32> ReadAsync(Memory<Byte>       Buffer,
                                                         CancellationToken  CancellationToken = default)
        {

            if (Buffer.IsEmpty)
                return 0;

            await readSemaphore.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                while (true)
                {

                    var taken = TakePlaintext(Buffer.Span);

                    if (taken >= 0)
                        return taken;

                    var read = await inner.ReadAsync(networkBuffer, CancellationToken).ConfigureAwait(false);

                    // The end of the stream underneath is the end of this one.
                    if (read == 0)
                        return 0;

                    if (OfferNetworkInput(read))
                        await FlushProtocolOutputAsync(CancellationToken).ConfigureAwait(false);

                }

            }
            finally
            {
                readSemaphore.Release();
            }

        }

        public override Task<Int32> ReadAsync(Byte[]             Buffer,
                                              Int32              Offset,
                                              Int32              Count,
                                              CancellationToken  CancellationToken)

            => ReadAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

        public override Int32 Read(Span<Byte> Buffer)
        {

            if (Buffer.IsEmpty)
                return 0;

            readSemaphore.Wait();

            try
            {

                while (true)
                {

                    var taken = TakePlaintext(Buffer);

                    if (taken >= 0)
                        return taken;

                    var read = inner.Read(networkBuffer, 0, networkBuffer.Length);

                    if (read == 0)
                        return 0;

                    if (OfferNetworkInput(read))
                        FlushProtocolOutput();

                }

            }
            finally
            {
                readSemaphore.Release();
            }

        }

        public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count)

            => Read(Buffer.AsSpan(Offset, Count));

        #endregion

        #region Writing

        private void EncryptForSending(ReadOnlySpan<Byte> Buffer)
        {

            var rented = ArrayPool<Byte>.Shared.Rent(Buffer.Length);

            try
            {

                Buffer.CopyTo(rented);

                lock (protocolLock)
                    protocol.WriteApplicationData(rented, 0, Buffer.Length);

            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(rented, clearArray: true);
            }

        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<Byte>  Buffer,
                                                   CancellationToken     CancellationToken = default)
        {

            if (Buffer.IsEmpty)
                return;

            await writeSemaphore.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                EncryptForSending(Buffer.Span);

                if (TakeProtocolOutput() is { } output)
                    await inner.WriteAsync(output, CancellationToken).ConfigureAwait(false);

            }
            finally
            {
                writeSemaphore.Release();
            }

        }

        public override Task WriteAsync(Byte[]             Buffer,
                                        Int32              Offset,
                                        Int32              Count,
                                        CancellationToken  CancellationToken)

            => WriteAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

        public override void Write(ReadOnlySpan<Byte> Buffer)
        {

            if (Buffer.IsEmpty)
                return;

            writeSemaphore.Wait();

            try
            {

                EncryptForSending(Buffer);

                if (TakeProtocolOutput() is { } output)
                    inner.Write(output, 0, output.Length);

            }
            finally
            {
                writeSemaphore.Release();
            }

        }

        public override void Write(Byte[] Buffer, Int32 Offset, Int32 Count)

            => Write(Buffer.AsSpan(Offset, Count));

        public override void Flush()

            => inner.Flush();

        public override Task FlushAsync(CancellationToken CancellationToken)

            => inner.FlushAsync(CancellationToken);

        #endregion

        #region Not supported

        public override Int64 Seek(Int64 Offset, SeekOrigin Origin)
            => throw new NotSupportedException();

        public override void SetLength(Int64 Value)
            => throw new NotSupportedException();

        #endregion

        #region Closing

        /// <summary>
        /// The close_notify of this side, where the connection is still open.
        /// </summary>
        private Byte[]? TakeClosingOutput()
        {
            try
            {
                lock (protocolLock)
                {

                    if (!protocol.IsClosed)
                        protocol.Close();

                }

                return TakeProtocolOutput();

            }
            catch
            {
                return null;
            }
        }

        protected override void Dispose(Boolean Disposing)
        {

            if (Disposing && Interlocked.Exchange(ref disposed, 1) == 0)
            {

                // Told to the peer where that takes no waiting: a writer that is
                // stuck on a peer that does not read must not hold a close up.
                if (writeSemaphore.Wait(0))
                {
                    try
                    {
                        if (TakeClosingOutput() is { } output)
                            inner.Write(output, 0, output.Length);
                    }
                    catch
                    { }
                    finally
                    {
                        writeSemaphore.Release();
                    }
                }

                if (!leaveInnerStreamOpen)
                    inner.Dispose();

            }

            base.Dispose(Disposing);

        }

        public override async ValueTask DisposeAsync()
        {

            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {

                if (writeSemaphore.Wait(0))
                {
                    try
                    {
                        if (TakeClosingOutput() is { } output)
                        {
                            using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                            await inner.WriteAsync(output, bounded.Token).ConfigureAwait(false);
                        }
                    }
                    catch
                    { }
                    finally
                    {
                        writeSemaphore.Release();
                    }
                }

                if (!leaveInnerStreamOpen)
                    await inner.DisposeAsync().ConfigureAwait(false);

            }

            GC.SuppressFinalize(this);

        }

        #endregion

    }

}
