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
    /// Which stream's queued DATA goes on the wire next (RFC 9218): the one
    /// decision a connection's single DATA writer loop makes over and over. The
    /// server's (<see cref="HTTP2Connection"/>) sends response bodies and tunnel
    /// bytes by it, the client's (<see cref="HTTP2ClientConnection"/>) request
    /// bodies and tunnel bytes, so that both directions of a connection order
    /// their streams alike, by the priority the client signalled.
    /// </summary>
    internal static class HTTP2SendOrder
    {

        /// <summary>
        /// Pick the best of Candidates to send from next, per <see cref="Compare"/>.
        /// Skips streams with nothing queued, and streams whose only queued
        /// bytes need flow-control window that isn't currently available — either
        /// the stream's own send window or, since it's shared, the connection's
        /// (an end-of-stream-only marker needs no window at all, so such a
        /// stream is still a candidate regardless of either window).
        ///
        /// Filtering window-blocked streams out of candidacy here — rather than
        /// picking the single best candidate first and discarding the whole turn
        /// if only *it* turns out window-blocked — matters for correctness, not
        /// just efficiency: without it, a lower-priority stream that needs no
        /// window (e.g. a tunnel's closing marker) could starve indefinitely
        /// behind a higher-priority stream that's permanently connection-window-
        /// blocked, even though the lower-priority one is otherwise immediately
        /// sendable.
        /// </summary>
        public static HTTP2Stream? PickNext(IReadOnlyList<HTTP2Stream> Candidates, long ConnectionSendWindow, out bool NeedsWindow)
        {

            HTTP2Stream? best            = null;
            var          bestNeedsWindow = false;

            foreach (var stream in Candidates)
            {

                if (!stream.OutboundQueue.HasPending)
                    continue;

                var needsWindow = stream.OutboundQueue.HeadNeedsWindow;

                if (needsWindow && (stream.SendWindow <= 0 || ConnectionSendWindow <= 0))
                    continue;   // Flow control blocked; retry once WINDOW_UPDATE arrives

                if (best is null || Compare(stream, best) < 0)
                {
                    best            = stream;
                    bestNeedsWindow = needsWindow;
                }

            }

            NeedsWindow = bestNeedsWindow;
            return best;

        }

        /// <summary>
        /// RFC 9218 send ordering: lower urgency number sends first; within the
        /// same urgency, a non-incremental stream ("send as a single unit") is
        /// preferred over an incremental one ("fine to interleave"); ties beyond
        /// that are broken round-robin-fairly by recency of last service, and
        /// between streams never served yet by stream ID, lowest first: the
        /// order the client asked in, which RFC 9218, Section 10 has a server
        /// serve non-incremental responses of one urgency by. This
        /// is a deliberate simplification of the RFC's non-incremental guidance
        /// — a strict reading favors draining one non-incremental stream to
        /// completion before starting the next at the same urgency, whereas this
        /// still round-robins fairly among several concurrent non-incremental
        /// streams at that urgency, rather than head-of-line-blocking one behind
        /// another. Reasonable for a learning implementation, and arguably a
        /// fairer outcome for concurrent equal-urgency responses either way.
        /// </summary>
        public static int Compare(HTTP2Stream A, HTTP2Stream B)
        {

            if (A.Priority.Urgency != B.Priority.Urgency)
                return A.Priority.Urgency.CompareTo(B.Priority.Urgency);

            if (A.Priority.Incremental != B.Priority.Incremental)
                return A.Priority.Incremental ? 1 : -1;   // Non-incremental drained preferentially

            if (A.LastServedSequence != B.LastServedSequence)
                return A.LastServedSequence.CompareTo(B.LastServedSequence);   // Least-recently-served first

            // Both never served: each service hands its stream a sequence of
            // its own, so only the initial -1 ties. Left to the order of the
            // candidates, the tie went to whichever stream the stream manager's
            // dictionary enumerated first — and a dictionary refills the slots
            // of removed entries first, so under load a new stream was
            // enumerated ahead of an older one, on every pick, for as long as
            // new streams kept coming. h2bench saw single responses wait 1.5 s
            // that way at 64 concurrent requests, while the connection was busy
            // all along.
            return A.StreamId.CompareTo(B.StreamId);

        }

    }

}
