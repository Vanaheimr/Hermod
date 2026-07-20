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
    /// A full-duplex (bidirectional) streaming exchange over one HTTP/2 stream,
    /// returned by <see cref="HTTP2ClientConnection.StartStreamingRequestAsync"/>.
    /// The request body is written incrementally (<see cref="WriteAsync"/> any number
    /// of chunks, then <see cref="CompleteRequestAsync"/> to half-close), and the
    /// response is read incrementally (<see cref="GetResponseAsync"/> for the head,
    /// then <see cref="ReadAsync"/> until it returns null, then
    /// <see cref="GetTrailersAsync"/>). Both directions flow concurrently — the
    /// enabler for client-streaming and bidirectional gRPC, whose request and
    /// response messages interleave over the same stream.
    ///
    /// Unlike a buffered <see cref="HTTP2ClientConnection.SendRequestAsync"/>, a
    /// streaming exchange is never auto-retried on REFUSED_STREAM: its outbound
    /// chunks aren't buffered for replay, so a reset surfaces to the caller (same as
    /// a CONNECT tunnel). Mirrors the server's <c>IHTTP2RequestStream</c> /
    /// <c>IHTTP2ResponseStream</c> seam from the client side.
    /// </summary>
    public sealed class HTTP2ClientStream
    {

        private readonly HTTP2ClientConnection                    connection;
        private readonly HTTP2Stream                              stream;
        private readonly Task<HTTP2ResponseHead>                  responseHead;
        private readonly ChannelReader<byte[]>                    responseChunks;
        private readonly Task<List<(string Name, string Value)>>  responseTrailers;

        internal HTTP2ClientStream(
            HTTP2ClientConnection                   Connection,
            HTTP2Stream                             Stream,
            Task<HTTP2ResponseHead>                 ResponseHead,
            ChannelReader<byte[]>                   ResponseChunks,
            Task<List<(string Name, string Value)>> ResponseTrailers)
        {
            connection       = Connection;
            stream           = Stream;
            responseHead     = ResponseHead;
            responseChunks   = ResponseChunks;
            responseTrailers = ResponseTrailers;
        }

        /// <summary>The stream ID this exchange runs on.</summary>
        public UInt32 StreamId => stream.StreamId;

        /// <summary>Send a chunk of request body as flow-controlled DATA frame(s) — never END_STREAM.</summary>
        public Task WriteAsync(byte[] Data, CancellationToken CancellationToken = default)
            => connection.SendStreamDataAsync(stream, Data, CancellationToken);

        /// <summary>Finish the request body: a zero-length END_STREAM DATA frame, half-closing our side.</summary>
        public Task CompleteRequestAsync(CancellationToken CancellationToken = default)
            => connection.EndTunnelAsync(stream);

        /// <summary>Await the response head (status + headers) — completes when the response HEADERS arrive.</summary>
        public Task<HTTP2ResponseHead> GetResponseAsync(CancellationToken CancellationToken = default)
            => responseHead.WaitAsync(CancellationToken);

        /// <summary>Read the next response body chunk as it arrives, or null once the response ends (END_STREAM / reset).</summary>
        public async Task<byte[]?> ReadAsync(CancellationToken CancellationToken = default)
        {
            if (await responseChunks.WaitToReadAsync(CancellationToken) && responseChunks.TryRead(out var chunk))
                return chunk;
            return null;
        }

        /// <summary>The response trailer fields (RFC 9113 §8.1) — completes when the response ends. Empty if none.</summary>
        public Task<List<(string Name, string Value)>> GetTrailersAsync()
            => responseTrailers;

    }

}
