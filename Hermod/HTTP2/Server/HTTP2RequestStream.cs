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
    /// Server-side <see cref="IHTTP2RequestStream"/> over a single streaming
    /// request's <see cref="HTTP2Stream"/>: request headers, the body pulled from
    /// the stream's inbound channel as DATA frames arrive, and the trailers (if any)
    /// once the body ends.
    /// </summary>
    internal sealed class HTTP2RequestStream : IHTTP2RequestStream
    {

        private static readonly List<(string Name, string Value)> EmptyFields = [];

        private readonly HTTP2Connection                         connection;
        private readonly HTTP2Stream                             stream;
        private readonly IReadOnlyList<(string Name, string Value)> headers;

        internal HTTP2RequestStream(HTTP2Connection Connection, HTTP2Stream Stream, IReadOnlyList<(string Name, string Value)> Headers)
        {
            connection = Connection;
            stream     = Stream;
            headers    = Headers;
        }

        public IReadOnlyList<(string Name, string Value)> Headers  => headers;

        public IReadOnlyList<(string Name, string Value)> Trailers => stream.Trailers ?? EmptyFields;

        public async ValueTask<byte[]?> ReadAsync(CancellationToken CancellationToken = default)
        {
            var reader = stream.RequestBodyChannel!.Reader;

            if (await reader.WaitToReadAsync(CancellationToken) && reader.TryRead(out var chunk))
            {
                // Consumption-driven backpressure: the window for these bytes was
                // deliberately withheld on receipt (HandleDataAsync) and is returned
                // only now, as the handler actually consumes them — so an unread body
                // leaves the peer's window depleted instead of buffering unbounded.
                await connection.ReplenishConsumedAsync(stream, chunk.Length);
                return chunk;
            }

            return null;   // body ended (channel completed at END_STREAM)
        }

    }

}
