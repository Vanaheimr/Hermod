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
    /// The byte-channel seam the SFTP client and server frame their packets over. Implemented by both the
    /// single-channel <see cref="SshChannelDuplex"/> (the classic subsystem path) and by a plain duplex
    /// <see cref="Stream"/> via <see cref="StreamSftpDuplex"/> — so SFTP can equally run over a channel opened
    /// on the connection multiplexer (concurrent with exec and forwards).
    /// </summary>
    public interface ISftpDuplex
    {
        /// <summary>
        /// Read exactly <paramref name="Count"/> bytes; null at a clean end-of-stream.
        /// </summary>
        ValueTask<Byte[]?> TryReadExactAsync(Int32 Count, CancellationToken CancellationToken = default);

        /// <summary>
        /// Read exactly <paramref name="Count"/> bytes (throws at end-of-stream).
        /// </summary>
        ValueTask<Byte[]> ReadExactAsync(Int32 Count, CancellationToken CancellationToken = default);

        /// <summary>
        /// Send bytes.
        /// </summary>
        ValueTask SendAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default);

        /// <summary>
        /// Close the channel.
        /// </summary>
        ValueTask CloseAsync(CancellationToken CancellationToken = default);
    }


    /// <summary>
    /// Adapts a bidirectional <see cref="Stream"/> (e.g. a multiplexed channel's <c>AsStream()</c>) to <see cref="ISftpDuplex"/>.
    /// </summary>
    public sealed class StreamSftpDuplex : ISftpDuplex
    {

        private readonly Stream stream;

        /// <summary>
        /// Wrap a duplex stream.
        /// </summary>
        public StreamSftpDuplex(Stream Stream)
        {
            this.stream = Stream;
        }

        public async ValueTask<Byte[]?> TryReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
        {
            var buffer = new Byte[Count];
            var total  = 0;
            while (total < Count)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(total), CancellationToken).ConfigureAwait(false);
                if (read == 0)
                    return null;   // end of stream (clean close or a truncated final frame)
                total += read;
            }
            return buffer;
        }

        public async ValueTask<Byte[]> ReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
            => await TryReadExactAsync(Count, CancellationToken).ConfigureAwait(false)
               ?? throw new SshChannelClosedException();

        public async ValueTask SendAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
        {
            await stream.WriteAsync(Data, CancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(CancellationToken).ConfigureAwait(false);
        }

        public async ValueTask CloseAsync(CancellationToken CancellationToken = default)
            => await stream.DisposeAsync().ConfigureAwait(false);

    }

}
