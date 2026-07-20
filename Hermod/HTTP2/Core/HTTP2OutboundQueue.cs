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
    /// Per-stream FIFO of outbound body bytes, drained by the connection's single
    /// priority-aware writer loop (<see cref="HTTP2Connection"/>'s writer loop)
    /// rather than written directly by whichever response/tunnel task produced
    /// them. That indirection is what makes RFC 9218 prioritization possible:
    /// only the writer loop decides whose bytes actually go on the wire next,
    /// instead of producers racing each other for the connection's shared send
    /// window first-come-first-served.
    ///
    /// Single-consumer (the writer loop) / multi-producer (response and tunnel
    /// tasks) — only <see cref="TakeChunk"/> and <see cref="AbandonAll"/> ever
    /// remove items, and both only ever run on the writer loop's own thread of
    /// execution, so they never race each other.
    ///
    /// Public only because it's exposed as the type of <see cref="HTTP2Stream.OutboundQueue"/>;
    /// there's no reason for anything outside this assembly to construct or call
    /// it directly.
    /// </summary>
    public sealed class HTTP2OutboundQueue
    {

        private readonly object                   gate  = new();
        private readonly Queue<HTTP2OutboundItem>  items = new();

        /// <summary>True if at least one item is queued (possibly just a zero-length end-of-stream marker).</summary>
        public bool HasPending
        {
            get { lock (gate) return items.Count > 0; }
        }

        /// <summary>
        /// True if the head item still has actual payload bytes left to send.
        /// False for an item that's only carrying a pending End-Stream marker —
        /// an empty final DATA frame needs no flow-control window, so the writer
        /// loop's picker must not treat such a stream as blocked just because its
        /// send window happens to be exhausted.
        /// </summary>
        public bool HeadNeedsWindow
        {
            get
            {
                lock (gate)
                {
                    if (items.Count == 0)
                        return false;

                    var head = items.Peek();
                    return head.Data.Length - head.Offset > 0;
                }
            }
        }

        /// <summary>
        /// Queue Data (and, if EndStream, a pending End-Stream marker) for the
        /// writer loop to send. Returns a task that completes once this exact
        /// item has been fully handed to the wire, or abandoned (see
        /// <see cref="AbandonAll"/>) — callers that want write-completion
        /// backpressure (e.g. a slow tunnel peer) should await it.
        /// </summary>
        public Task EnqueueAsync(byte[] Data, bool EndStream, List<(string Name, string Value)>? Trailers = null)
        {

            var item = new HTTP2OutboundItem {
                Data       = Data,
                EndStream  = EndStream,
                Trailers   = Trailers,
                Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };

            lock (gate)
                items.Enqueue(item);

            return item.Completion.Task;

        }

        /// <summary>
        /// Called only by the writer loop. Takes up to MaxBytes from the head
        /// item; if that exhausts the item, it is dequeued and its Completion is
        /// signalled. Returns null if nothing is queued.
        /// </summary>
        public (byte[] Chunk, bool EndStream, List<(string Name, string Value)>? Trailers)? TakeChunk(int MaxBytes)
        {

            lock (gate)
            {

                if (items.Count == 0)
                    return null;

                var item      = items.Peek();
                var remaining = item.Data.Length - item.Offset;
                var take      = Math.Max(0, Math.Min(remaining, MaxBytes));

                var chunk = take > 0
                                ? item.Data.AsSpan(item.Offset, take).ToArray()
                                : [];

                item.Offset += take;

                if (item.Offset < item.Data.Length)
                    return (chunk, false, null);

                items.Dequeue();
                item.Completion.TrySetResult();

                return (chunk, item.EndStream, item.Trailers);

            }

        }

        /// <summary>
        /// Drop everything still queued and unblock any producer awaiting
        /// <see cref="EnqueueAsync"/> — called when the stream is reset, so a
        /// producer isn't left waiting forever for bytes that will now never be
        /// sent.
        /// </summary>
        public void AbandonAll()
        {
            lock (gate)
            {
                while (items.Count > 0)
                    items.Dequeue().Completion.TrySetResult();
            }
        }

    }

}
