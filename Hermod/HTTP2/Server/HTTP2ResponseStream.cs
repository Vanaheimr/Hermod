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
    /// Server-side <see cref="IHTTP2ResponseStream"/> — writes an incrementally
    /// produced response over a single <see cref="HTTP2Stream"/>, reusing the
    /// connection's HEADERS/DATA/trailer machinery (flow-controlled, prioritized,
    /// HPACK-encoded under the write lock) so a streamed response is byte-identical
    /// on the wire to a buffered one, just produced piece by piece.
    /// </summary>
    internal sealed class HTTP2ResponseStream : IHTTP2ResponseStream
    {

        private readonly HTTP2Connection connection;
        private readonly HTTP2Stream     stream;

        private bool headersSent;
        private bool completed;

        internal HTTP2ResponseStream(HTTP2Connection Connection, HTTP2Stream Stream)
        {
            connection = Connection;
            stream     = Stream;
        }

        /// <summary>Whether the response HEADERS have gone out yet (drives error fallback).</summary>
        internal bool HeadersSent => headersSent;

        /// <summary>Whether the response has been completed (END_STREAM sent).</summary>
        internal bool Completed   => completed;

        public async Task WriteInterimResponseAsync(int Status, IEnumerable<(string Name, string Value)> Headers, CancellationToken CancellationToken = default)
        {
            if (Status is < 100 or >= 200)
                throw new ArgumentOutOfRangeException(nameof(Status), Status, "An interim response status must be in the 1xx range");
            if (headersSent)
                throw new InvalidOperationException("Interim responses must be sent before the final response headers");

            var list = new List<(string Name, string Value)> { (":status", Status.ToString()) };
            list.AddRange(Headers);

            connection.EnforceOutboundHeaderListSize(stream.StreamId, list);

            // An interim (1xx) response is a HEADERS block that does NOT end the
            // stream — the final response follows (RFC 9110, Section 15.2).
            await connection.SendHeaderListAsync(stream.StreamId, list, EndStream: false);
        }

        public async Task WriteHeadersAsync(IEnumerable<(string Name, string Value)> Headers, CancellationToken CancellationToken = default)
        {
            if (headersSent)
                throw new InvalidOperationException("Response headers have already been sent");

            var list = Headers.ToList();
            connection.EnforceOutboundHeaderListSize(stream.StreamId, list);
            HTTP2Connection.ApplyResponsePriorityOverride(stream, list);

            headersSent = true;
            await connection.SendHeaderListAsync(stream.StreamId, list, EndStream: false);
        }

        public Task WriteAsync(byte[] Data, CancellationToken CancellationToken = default)
        {
            if (!headersSent)
                throw new InvalidOperationException("WriteHeadersAsync must be called before WriteAsync");
            if (completed)
                throw new InvalidOperationException("The response has already been completed");
            if (Data.Length == 0)
                return Task.CompletedTask;

            return connection.EnqueueOutboundAsync(stream, Data, EndStream: false);
        }

        public async Task CompleteAsync(IEnumerable<(string Name, string Value)>? Trailers = null, CancellationToken CancellationToken = default)
        {
            if (completed)
                return;

            // A handler that produced nothing still needs a valid response.
            if (!headersSent)
                await WriteHeadersAsync([(":status", "200")], CancellationToken);

            completed = true;

            var trailerList = Trailers?.ToList();

            if (trailerList is { Count: > 0 })
            {
                HTTP2Connection.ValidateOutboundTrailers(stream.StreamId, trailerList);
                await connection.EnqueueOutboundAsync(stream, [], EndStream: true, trailerList);
            }
            else
                await connection.EnqueueOutboundAsync(stream, [], EndStream: true);
        }

        /// <summary>Auto-complete when a handler returns without ending the response itself.</summary>
        internal Task EnsureCompletedAsync()
            => completed ? Task.CompletedTask : CompleteAsync();

    }

}
