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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A bidirectional <see cref="Stream"/> over an open SSH channel — the plain-stream view of a
    /// <c>direct-tcpip</c> tunnel (or any channel). Reads and writes map to channel data with the channel's
    /// flow control; closing the stream sends EOF + CLOSE.
    /// </summary>
    public sealed class SshChannelStream : Stream
    {

        #region Data

        private readonly SshChannelDuplex  channel;
        private Boolean                    closed;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Wrap an established channel as a stream.
        /// </summary>
        public SshChannelStream(SshChannelDuplex Channel)
        {
            this.channel = Channel;
        }

        #endregion

        #region Stream

        public override Boolean CanRead   => !closed;
        public override Boolean CanWrite  => !closed;
        public override Boolean CanSeek   => false;
        public override Int64   Length            => throw new NotSupportedException();
        public override Int64   Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
            => channel.ReadSomeAsync(Buffer, CancellationToken);

        public override Task<Int32> ReadAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
            => channel.ReadSomeAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

        public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count)
            => channel.ReadSomeAsync(Buffer.AsMemory(Offset, Count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override ValueTask WriteAsync(ReadOnlyMemory<Byte> Buffer, CancellationToken CancellationToken = default)
            => channel.SendAsync(Buffer, CancellationToken);

        public override Task WriteAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)
            => channel.SendAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

        public override void Write(Byte[] Buffer, Int32 Offset, Int32 Count)
            => channel.SendAsync(Buffer.AsMemory(Offset, Count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken CancellationToken) => Task.CompletedTask;

        public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void  SetLength(Int64 value)                => throw new NotSupportedException();

        public override async ValueTask DisposeAsync()
        {
            if (closed) return;
            closed = true;
            try { await channel.CloseAsync().ConfigureAwait(false); } catch { }
            await base.DisposeAsync().ConfigureAwait(false);
        }

        protected override void Dispose(Boolean disposing)
        {
            if (closed) return;
            closed = true;
            if (disposing)
                try { channel.CloseAsync().AsTask().GetAwaiter().GetResult(); } catch { }
            base.Dispose(disposing);
        }

        #endregion

    }

}
