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
    ///
    /// The response body is held for <see cref="ReadAsync"/> up to the stream's
    /// window, 1 MiB: its window goes back to the server only as it is read, so
    /// the server can send no more than that ahead of the reader. A response
    /// that will not be read to its end is given up with <see cref="DisposeAsync"/>,
    /// or it keeps its stream, and the server, waiting.
    /// </summary>
    public sealed class HTTP2ClientStream : IAsyncDisposable
    {

        private readonly HTTP2ClientConnection                    connection;
        private readonly HTTP2Stream                              stream;
        private readonly Task<HTTP2ResponseHead>                  responseHead;
        private readonly ChannelReader<byte[]>                    responseChunks;
        private readonly Task<List<(string Name, string Value)>>  responseTrailers;
        private          Int32                                    disposed;

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

        /// <summary>
        /// The stream ID this exchange runs on.
        /// </summary>
        public UInt32 StreamId => stream.StreamId;

        /// <summary>
        /// Send a chunk of request body as flow-controlled DATA frame(s) — never
        /// END_STREAM. The connection's writer loop sends them by the priority the
        /// request asked for, between the DATA of its other streams; the task
        /// completes once the last frame of them is the next to go out.
        ///
        /// Nothing more goes out on a stream the server has reset, and the task
        /// fails then, as the response side of a reset stream does, with an
        /// <see cref="HTTP2StreamException"/> that carries the reset's error code:
        /// before the response, or after a complete one, which a server may follow
        /// with RST_STREAM NO_ERROR to stop the rest of an upload it no longer
        /// needs (RFC 9113, Section 8.1). That response stands. Once the request is
        /// complete on our side (<see cref="CompleteRequestAsync"/>), a write fails
        /// with an <see cref="InvalidOperationException"/>.
        /// <paramref name="CancellationToken"/> ends the wait, not the write: a
        /// chunk already queued still goes out, in order.
        ///
        /// On a stream still open when the connection ends, a write still
        /// waiting then, and every one after, fails with an
        /// <see cref="OperationCanceledException"/> whose token is not the
        /// caller's, as a tunnel's does (<see cref="HTTP2ClientTunnel.WriteAsync"/>).
        /// </summary>
        public Task WriteAsync(byte[] Data, CancellationToken CancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            return connection.SendStreamDataAsync(stream, Data, CancellationToken);
        }

        /// <summary>
        /// Finish the request, half-closing our side: a zero-length END_STREAM DATA
        /// frame, or — when <paramref name="Trailers"/> are supplied — a trailing
        /// HEADERS block carrying END_STREAM instead (RFC 9113, Section 8.1). The
        /// mirror of the server's <c>IHTTP2ResponseStream.CompleteAsync</c>, and what
        /// lets a client-streaming call say something after its last message.
        ///
        /// Trailers must carry no pseudo-header fields and their names must be
        /// lowercase; a list that breaks either rule throws rather than reaching the
        /// wire, since the peer would be entitled to reset the stream over it.
        ///
        /// Nor does anything go out on a stream the server has reset, trailers or
        /// the END_STREAM DATA frame: before its response, or after a complete
        /// one, as RFC 9113, Section 8.1 lets a server stop the rest of an upload
        /// it no longer needs (with NO_ERROR). The task fails then, as the
        /// response side of a reset stream does and as <see cref="WriteAsync"/>
        /// does, with an <see cref="HTTP2StreamException"/> that carries the
        /// reset's error code. A response that was complete stands.
        ///
        /// Either end goes out behind the chunks written before it. Once the
        /// request is complete, ending it again sends nothing — without trailers
        /// it returns, with them it fails with an <see cref="InvalidOperationException"/>.
        /// </summary>
        public Task CompleteRequestAsync(IEnumerable<(string Name, string Value)>? Trailers = null,
                                         CancellationToken                          CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

            var trailerList = Trailers?.ToList();

            return trailerList is { Count: > 0 }
                       ? connection.EndRequestWithTrailersAsync(stream, trailerList, CancellationToken)
                       : connection.EndTunnelAsync(stream);

        }

        /// <summary>
        /// Await the response head (status + headers) — completes when the response HEADERS arrive.
        /// </summary>
        public Task<HTTP2ResponseHead> GetResponseAsync(CancellationToken CancellationToken = default)
            => responseHead.WaitAsync(CancellationToken);

        /// <summary>
        /// Read the next response body chunk as it arrives, or null once the response ends (END_STREAM / reset).
        ///
        /// Its stream window goes back to the server as it is read
        /// (consumption-driven backpressure): a chunk read gives the server room
        /// for as much more.
        /// </summary>
        public async Task<byte[]?> ReadAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

            if (await responseChunks.WaitToReadAsync(CancellationToken) && responseChunks.TryRead(out var chunk))
            {
                await connection.ReplenishConsumedAsync(stream, chunk.Length);
                return chunk;
            }

            return null;

        }

        /// <summary>
        /// The response trailer fields (RFC 9113 §8.1) — completes when the response ends. Empty if none.
        /// </summary>
        public Task<List<(string Name, string Value)>> GetTrailersAsync()
            => responseTrailers;

        /// <summary>
        /// Give the exchange up: unless both sides have ended the stream, or it
        /// was reset, it is reset with RST_STREAM CANCEL — so that the server
        /// stops sending a response nobody reads to its end, and a request that
        /// was not ended is not taken for a whole one — and its stream slot is
        /// free for the next request. What was received and not read is dropped.
        /// A read that waits for the response then fails with an
        /// <see cref="ObjectDisposedException"/>, as does a write, and every call
        /// to <see cref="ReadAsync"/>, <see cref="WriteAsync"/> or
        /// <see cref="CompleteRequestAsync"/> after this one. The response head
        /// and trailers stay as they arrived; whichever had not, fails the same
        /// way. Never throws.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            await connection.AbandonAsync(stream, new ObjectDisposedException(GetType().FullName, $"Stream {StreamId} was disposed."));

        }

    }

}
