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

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets
{

    /// <summary>
    /// A stream that hands out some bytes before the ones that are still coming.
    /// </summary>
    /// <remarks>
    /// <b>For giving a connection away after something has already been read off
    /// it.</b> An HTTP server that reads a request and then finds it is a
    /// WebSocket upgrade cannot put those bytes back, and the code it hands the
    /// connection to wants to read the request itself - so the request goes in
    /// front of the stream and everything downstream reads normally.
    ///
    /// The alternative is to teach the downstream reader about a starting
    /// buffer, and that turns out to be the worse one: its loop would then have
    /// two ways of getting its first bytes, and only one of them would be
    /// exercised by the tests that existed before.
    ///
    /// <b>Only reading is intercepted.</b> Writing, flushing and the timeouts go
    /// straight to the stream underneath; <see cref="CanTimeout"/> and the two
    /// timeout properties are forwarded rather than defaulted, because a caller
    /// that sets a read timeout on this expects the socket to get it.
    /// </remarks>
    public sealed class PrefixedStream : Stream
    {

        #region Data

        private readonly Byte[]  prefix;
        private readonly Stream  inner;
        private readonly Boolean leaveOpen;

        private Int32 offset;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a stream that yields the prefix and then the inner stream.
        /// </summary>
        /// <param name="Prefix">The bytes that were already read.</param>
        /// <param name="Inner">Where the rest comes from.</param>
        /// <param name="LeaveOpen">Whether closing this leaves the inner stream open.</param>
        public PrefixedStream(Byte[]   Prefix,
                              Stream   Inner,
                              Boolean  LeaveOpen = false)
        {
            prefix     = Prefix;
            inner      = Inner;
            leaveOpen  = LeaveOpen;
        }

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
        /// How much of the prefix has not been handed out yet.
        /// </summary>
        public Int32 Pending => prefix.Length - offset;

        #endregion

        #region Reading

        /// <summary>
        /// Reads from the prefix while there is any, and from the stream after.
        /// </summary>
        /// <remarks>
        /// <b>Never both in one call</b>, even when the buffer would hold more.
        /// A read that returned the last of the prefix plus whatever the socket
        /// happened to have would block for the second half - and the caller
        /// asked for what is there, not for the buffer to be filled.
        /// </remarks>
        public override Int32 Read(Byte[] buffer, Int32 offsetInBuffer, Int32 count)
        {

            if (offset < prefix.Length)
            {

                var take = Math.Min(count, prefix.Length - offset);

                Array.Copy(prefix, offset, buffer, offsetInBuffer, take);
                offset += take;

                return take;

            }

            return inner.Read(buffer, offsetInBuffer, count);

        }

        public override ValueTask<Int32> ReadAsync(Memory<Byte>       buffer,
                                                   CancellationToken  cancellationToken = default)
        {

            if (offset < prefix.Length)
            {

                var take = Math.Min(buffer.Length, prefix.Length - offset);

                prefix.AsSpan(offset, take).CopyTo(buffer.Span);
                offset += take;

                return ValueTask.FromResult(take);

            }

            return inner.ReadAsync(buffer, cancellationToken);

        }

        public override Task<Int32> ReadAsync(Byte[]             buffer,
                                              Int32              offsetInBuffer,
                                              Int32              count,
                                              CancellationToken  cancellationToken)

            => ReadAsync(buffer.AsMemory(offsetInBuffer, count), cancellationToken).AsTask();

        #endregion

        #region Writing and the rest

        public override void Write(Byte[] buffer, Int32 offsetInBuffer, Int32 count)
            => inner.Write(buffer, offsetInBuffer, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<Byte>  buffer,
                                             CancellationToken     cancellationToken = default)
            => inner.WriteAsync(buffer, cancellationToken);

        public override Task WriteAsync(Byte[]             buffer,
                                        Int32              offsetInBuffer,
                                        Int32              count,
                                        CancellationToken  cancellationToken)
            => inner.WriteAsync(buffer.AsMemory(offsetInBuffer, count), cancellationToken).AsTask();

        public override void Flush()
            => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken)
            => inner.FlushAsync(cancellationToken);

        public override Int64 Seek(Int64 offsetInStream, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(Int64 value)
            => throw new NotSupportedException();

        public override void Close()
        {

            if (!leaveOpen)
                inner.Close();

            base.Close();

        }

        protected override void Dispose(Boolean disposing)
        {

            if (disposing && !leaveOpen)
                inner.Dispose();

            base.Dispose(disposing);

        }

        #endregion

    }

}
