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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP
{

    /// <summary>
    /// A seekable <see cref="Stream"/> over a remote SFTP file. Reads and writes are mapped to offset-based
    /// SFTP READ/WRITE requests at the current position, so a caller can stream a large file (e.g.
    /// <c>CopyToAsync</c>) without materializing it in memory. Obtained from
    /// <see cref="SftpClient.OpenFileStreamAsync"/>.
    /// </summary>
    public sealed class SftpFileStream : Stream
    {

        #region Data

        private const Int32 TransferChunk = 30 * 1024;

        private readonly SftpClient  client;
        private readonly String      handle;
        private readonly Boolean     readable;
        private readonly Boolean     writable;

        private Int64                position;
        private Int64                length;
        private Boolean              closed;

        #endregion

        #region Constructor(s)

        internal SftpFileStream(SftpClient Client, String Handle, Boolean Readable, Boolean Writable, Int64 InitialLength)
        {
            this.client    = Client;
            this.handle    = Handle;
            this.readable  = Readable;
            this.writable  = Writable;
            this.length    = InitialLength;
        }

        #endregion

        #region Stream capabilities

        public override Boolean CanRead   => readable && !closed;
        public override Boolean CanWrite  => writable && !closed;
        public override Boolean CanSeek   => !closed;
        public override Int64   Length    => length;

        public override Int64 Position
        {
            get => position;
            set => position = value;
        }

        #endregion

        #region Read

        public override async ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
        {

            if (!readable)
                throw new NotSupportedException("The stream is not readable.");

            var want = Math.Min(Buffer.Length, TransferChunk);
            var data = await client.ReadAsync(handle, position, want, CancellationToken).ConfigureAwait(false);
            if (data.Length == 0)
                return 0;

            data.CopyTo(Buffer.Span);
            position += data.Length;
            if (position > length)
                length = position;

            return data.Length;

        }

        public override Task<Int32> ReadAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
            => ReadAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

        public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count)
            => ReadAsync(Buffer.AsMemory(Offset, Count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        #endregion

        #region Write

        public override async ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
        {

            if (!writable)
                throw new NotSupportedException("The stream is not writable.");

            var offset = 0;
            while (offset < Buffer.Length)
            {
                var count = Math.Min(TransferChunk, Buffer.Length - offset);
                await client.WriteAsync(handle, position, Buffer.Slice(offset, count), CancellationToken).ConfigureAwait(false);
                position += count;
                offset   += count;
                if (position > length)
                    length = position;
            }

        }

        public override Task WriteAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
            => WriteAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

        public override void Write(Byte[] Buffer, Int32 Offset, Int32 Count)
            => WriteAsync(Buffer.AsMemory(Offset, Count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        #endregion

        #region Seek / Flush / SetLength

        public override Int64 Seek(Int64 Offset, SeekOrigin Origin)
        {
            position = Origin switch {
                           SeekOrigin.Begin    => Offset,
                           SeekOrigin.Current  => position + Offset,
                           SeekOrigin.End      => length + Offset,
                           _                   => position
                       };
            return position;
        }

        // Nothing is buffered on this side — every write already went out as a WRITE request — so Flush
        // has nothing to do. It deliberately does *not* send fsync: callers flush routinely and would be
        // paying a round trip plus a disk sync for it, and a server without the extension would start
        // throwing from a method nobody expects to fail. Durability is asked for by name, below.
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken CancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Ask the server to flush this file to stable storage (<c>fsync@openssh.com</c>) — the bytes are
        /// on the disk, not merely in the server's page cache, once this returns.
        ///
        /// <para>
        /// Call it before disposing the stream, not after: the flush needs the handle to still be open.
        /// Throws <see cref="SftpException"/> with <see cref="SftpStatusCode.OpUnsupported"/> if the
        /// server never offered the extension — an unanswerable durability request is reported, never
        /// quietly skipped.
        /// </para>
        /// </summary>
        public ValueTask SyncToDiskAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(closed, this);

            return client.FsyncAsync(handle, CancellationToken);

        }

        /// <summary>
        /// Not supported: SFTP v3 has no in-place truncation on this stream.
        /// </summary>
        public override void SetLength(Int64 value)
            => throw new NotSupportedException("SFTP v3 file streams do not support SetLength.");

        #endregion

        #region Dispose

        public override async ValueTask DisposeAsync()
        {
            if (closed)
                return;
            closed = true;
            try { await client.CloseAsync(handle, CancellationToken.None).ConfigureAwait(false); } catch { }
            await base.DisposeAsync().ConfigureAwait(false);
        }

        protected override void Dispose(Boolean disposing)
        {
            if (closed)
                return;
            closed = true;
            if (disposing)
                try { client.CloseAsync(handle, CancellationToken.None).AsTask().GetAwaiter().GetResult(); } catch { }
            base.Dispose(disposing);
        }

        #endregion

    }

}
