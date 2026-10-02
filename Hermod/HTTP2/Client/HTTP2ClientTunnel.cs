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
        /// Read the next chunk the peer sent, or null once the tunnel ends: at
        /// the server's END_STREAM or RST_STREAM, or at the end of the
        /// connection, however it ends — the server goes away, the keepalive
        /// gets no answer, the client closes it, or its writer loop fails.
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
        /// The tunnel's RFC 9218 priority: the one it was opened with
        /// (<see cref="HTTP2ClientConnection.OpenTunnelAsync"/>), or the last one
        /// <see cref="UpdatePriorityAsync"/> set. The connection's writer loop sends
        /// what is written to the tunnel by it, among the DATA of the connection's
        /// other streams.
        /// </summary>
        public HTTP2Priority Priority
            => stream.Priority;

        /// <summary>
        /// Reprioritize the tunnel: what is still queued, and what is written from
        /// now on, goes out by the new priority, and the server is told with a
        /// PRIORITY_UPDATE frame, for what it sends back (RFC 9218, Section 7.1) —
        /// see <see cref="HTTP2ClientConnection.UpdatePriorityAsync"/>.
        /// </summary>
        public Task UpdatePriorityAsync(HTTP2Priority Priority, CancellationToken CancellationToken = default)
            => connection.UpdatePriorityAsync(stream.StreamId, Priority, CancellationToken);

        /// <summary>
        /// Send a chunk of bytes to the peer as flow-controlled DATA frame(s): the
        /// connection's writer loop sends them by the tunnel's
        /// <see cref="Priority"/>, between the DATA of its other streams. Returns
        /// once the last frame of them is the next to go out.
        ///
        /// Nothing more goes out on a tunnel the server has reset, and the task
        /// fails then with an <see cref="HTTP2StreamException"/> that carries the
        /// reset's error code. <see cref="ReadAsync"/> returns null after a reset
        /// as after an orderly end; this is where the two differ. Once the tunnel
        /// is closed on our side (<see cref="CloseAsync"/>), a write fails with an
        /// <see cref="InvalidOperationException"/>. <paramref name="CancellationToken"/>
        /// ends the wait, not the write: a chunk already queued still goes out, in
        /// order.
        ///
        /// A tunnel still open when the connection ends reads its end there too
        /// (<see cref="ReadAsync"/> returns null), and a write fails then with an
        /// <see cref="OperationCanceledException"/> whose token is not the
        /// caller's: one still waiting, for window or for its turn, and every one
        /// after.
        /// </summary>
        public Task WriteAsync(byte[] Data, CancellationToken CancellationToken)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            return connection.SendTunnelDataAsync(stream, Data, CancellationToken);
        }

        /// <summary>
        /// End our side of the tunnel (a zero-length END_STREAM DATA frame, behind
        /// what was written before it). On a tunnel the server has reset nothing
        /// is sent, and the task fails as <see cref="WriteAsync"/> does. A second
        /// close sends nothing, and returns; so does a close on a connection that
        /// has ended, or that ends while the close waits.
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
