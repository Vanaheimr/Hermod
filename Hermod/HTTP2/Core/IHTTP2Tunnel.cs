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
    /// A transport-agnostic bidirectional byte tunnel — the minimal surface a
    /// protocol layered on top of a CONNECT stream (e.g. RFC 6455 WebSocket
    /// framing, see <see cref="WebSocketConnection"/>) needs: read the next chunk
    /// the peer sent, write a chunk back. Lives in the shared library so those
    /// consumers don't depend on the server-coupled concrete implementation
    /// (the server's <c>HTTP2Tunnel</c>, which implements this). A client-side
    /// tunnel could implement the same interface and reuse the framing unchanged.
    /// </summary>
    public interface IHTTP2Tunnel
    {
        /// <summary>Read the next chunk from the peer, or null once the peer has ended its side.</summary>
        Task<byte[]?> ReadAsync(CancellationToken CancellationToken);

        /// <summary>Send a chunk of bytes to the peer.</summary>
        Task WriteAsync(byte[] Data, CancellationToken CancellationToken);
    }

}
