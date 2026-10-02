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

    using System.Threading.Channels;

    /// <summary>
    /// The client side of an accepted CONNECT tunnel (RFC 9113 §8.5 / RFC 8441) —
    /// a raw bidirectional byte stream over one HTTP/2 stream, the mirror of the
    /// server's <c>HTTP2Tunnel</c>. Implements <see cref="IHTTP2Tunnel"/>, so a
    /// protocol layered on top (e.g. RFC 6455 WebSocket framing via
    /// <see cref="WebSocketConnection"/>) works over it unchanged.
    ///
    /// What the server sends is held for <see cref="ReadAsync"/>, up to the
    /// tunnel's stream window, 1 MiB: its window goes back to the server only as
    /// it is read, so the server can send no more than that ahead of the reader.
    /// A tunnel that will not be read to its end is given up with
    /// <see cref="DisposeAsync"/>.
    /// </summary>
    public sealed class HTTP2ClientTunnel : IHTTP2Tunnel, IAsyncDisposable
    {

        private readonly HTTP2ClientConnection connection;
        private readonly HTTP2Stream           stream;
        private          Int32                 disposed;

        internal HTTP2ClientTunnel(HTTP2ClientConnection Connection, HTTP2Stream Stream, IReadOnlyList<(string Name, string Value)> ResponseHeaders)
        {
            connection           = Connection;
            stream               = Stream;
            this.ResponseHeaders = ResponseHeaders;
        }

        /// <summary>
        /// The tunnel's stream ID.
        /// </summary>
        public UInt32 StreamId => stream.StreamId;

        /// <summary>
        /// The headers the server sent on the accepting (2xx) CONNECT response —
        /// e.g. <c>sec-websocket-extensions</c> echoing back a negotiated
        /// permessage-deflate (RFC 7692), or any sub-protocol/extension the server
        /// selected.
        /// </summary>
        public IReadOnlyList<(string Name, string Value)> ResponseHeaders { get; }

        /// <summary>
        /// Read the next chunk the peer sent, or null once the tunnel ends (END_STREAM / reset).
        ///
        /// Its stream window goes back to the server as it is read
        /// (consumption-driven backpressure): a chunk read gives the server room
        /// for as much more.
        /// </summary>
        public async Task<byte[]?> ReadAsync(CancellationToken CancellationToken)
        {

            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

            var reader = stream.TunnelInbound!.Reader;

            if (await reader.WaitToReadAsync(CancellationToken) && reader.TryRead(out var chunk))
            {
                await connection.ReplenishConsumedAsync(stream, chunk.Length);
                return chunk;
            }

            return null;

        }

        /// <summary>
        /// Send a chunk of bytes to the peer as flow-controlled DATA frame(s).
        ///
        /// Nothing more goes out on a tunnel the server has reset, and the task
        /// fails then with an <see cref="HTTP2StreamException"/> that carries the
        /// reset's error code. <see cref="ReadAsync"/> returns null after a reset
        /// as after an orderly end; this is where the two differ.
        /// </summary>
        public Task WriteAsync(byte[] Data, CancellationToken CancellationToken)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            return connection.SendTunnelDataAsync(stream, Data, CancellationToken);
        }

        /// <summary>
        /// End our side of the tunnel (a zero-length END_STREAM DATA frame). On a
        /// tunnel the server has reset nothing is sent, and the task fails as
        /// <see cref="WriteAsync"/> does.
        /// </summary>
        public Task CloseAsync()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            return connection.EndTunnelAsync(stream);
        }

        /// <summary>
        /// Give the tunnel up: unless both sides have ended it, or it was reset,
        /// it is reset with RST_STREAM CANCEL, so that the server stops sending,
        /// and its stream slot is free for the next request. What was received
        /// and not read is dropped. A read or a write that waits on the tunnel
        /// then fails with an <see cref="ObjectDisposedException"/>, and so does
        /// every call after this one but this one. Never throws.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            await connection.AbandonAsync(stream, new ObjectDisposedException(GetType().FullName, $"Tunnel {StreamId} was disposed."));

        }

    }

}
