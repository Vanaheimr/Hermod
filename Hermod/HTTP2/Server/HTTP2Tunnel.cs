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
    /// A bidirectional byte tunnel over a single accepted CONNECT stream
    /// (RFC 9113, Section 8.5; RFC 8441 extended CONNECT). Reads surface DATA
    /// frame payloads the peer sends on this stream, in order, as they arrive;
    /// writes go out as DATA frame(s) on the same stream, split and flow-controlled
    /// exactly like an ordinary response body. There is no framing of its own —
    /// callers layer whatever protocol they're tunneling (e.g. RFC 6455 WebSocket
    /// framing, see <see cref="WebSocketConnection"/>) on top of these raw bytes.
    ///
    /// Implements the shared-library <see cref="IHTTP2Tunnel"/> so transport-agnostic
    /// consumers (WebSocketConnection) don't depend on this server-coupled type.
    /// </summary>
    public sealed class HTTP2Tunnel : IHTTP2Tunnel
    {

        private readonly HTTP2Connection connection;
        private readonly HTTP2Stream     stream;

        internal HTTP2Tunnel(HTTP2Connection Connection, HTTP2Stream Stream)
        {
            connection = Connection;
            stream     = Stream;
        }

        /// <summary>
        /// Read the next chunk the peer sent. Returns null once the peer has
        /// ended its side of the tunnel — END_STREAM on a DATA frame, or the
        /// stream was reset.
        /// </summary>
        public async Task<byte[]?> ReadAsync(CancellationToken CancellationToken)
        {

            var reader = stream.TunnelInbound!.Reader;

            if (await reader.WaitToReadAsync(CancellationToken) && reader.TryRead(out var chunk))
            {
                // Consumption-driven backpressure: the window for these bytes was
                // deliberately withheld on receipt (HandleDataAsync) and is returned
                // only now, as the tunnel consumer actually takes them.
                await connection.ReplenishConsumedAsync(stream, chunk.Length);
                return chunk;
            }

            return null;

        }

        /// <summary>
        /// Send a chunk of data to the peer as DATA frame(s) on this stream.
        /// </summary>
        public Task WriteAsync(byte[] Data, CancellationToken CancellationToken)
            => connection.SendTunnelDataAsync(stream, Data, CancellationToken);

    }

}
