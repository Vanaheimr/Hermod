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
    /// Manages all streams for a single HTTP/2 connection.
    /// Handles stream creation, lookup, and connection-level flow control.
    /// </summary>
    public sealed class HTTP2StreamManager
    {

        private readonly Dictionary<UInt32, HTTP2Stream> streams = [];

        /// <summary>
        /// Guards <see cref="streams"/> itself (adds/removes/enumeration) —
        /// previously unnecessary, since only the connection's frame read loop
        /// ever touched the dictionary. That invariant no longer holds once the
        /// priority-aware writer loop (a separate, concurrently-running task)
        /// needs to enumerate live streams via <see cref="GetSendableStreams"/>
        /// on every send decision. Per-stream state (State/SendWindow/etc.) is
        /// still guarded separately (stateLock / the connection's flowLock) —
        /// this lock is only about the dictionary's own shape.
        /// </summary>
        private readonly object dictLock = new();

        /// <summary>
        /// Whether this endpoint is the server or the client (RFC 9113, Section
        /// 5.1.1). Defaults to Server so every existing server call site
        /// (`new HTTP2StreamManager()`) keeps its exact behavior; the Track E
        /// client passes Client.
        /// </summary>
        public HTTP2Role Role { get; }

        /// <summary>
        /// The low bit of stream IDs the *peer* initiates: a server's peer (the
        /// client) uses odd IDs (1); a client's peer (the server) would use even
        /// ones (0, server push — which this stack disables, so in practice a
        /// client sees none). Streams with this parity are validated/tracked as
        /// peer-initiated in <see cref="GetOrCreateStream"/> and <see cref="IsIdle"/>.
        /// </summary>
        private uint PeerInitiatedParity => Role == HTTP2Role.Server ? 1u : 0u;

        /// <summary>The low bit of stream IDs *we* initiate — the mirror of <see cref="PeerInitiatedParity"/>.</summary>
        private uint LocalInitiatedParity => Role == HTTP2Role.Server ? 0u : 1u;

        public HTTP2StreamManager(HTTP2Role Role = HTTP2Role.Server)
        {
            this.Role = Role;
        }

        /// <summary>
        /// The highest peer-initiated stream ID seen so far (for a server, the
        /// highest odd stream the client opened).
        /// </summary>
        public UInt32  LastPeerStreamId  { get; private set; }

        /// <summary>
        /// The highest stream ID *we* have locally initiated (client role: the last
        /// odd request stream we allocated via <see cref="CreateLocalStream"/>).
        /// Stays 0 for a server, which never initiates streams here (no push).
        /// </summary>
        public UInt32  LastLocalStreamId { get; private set; }

        /// <summary>
        /// Connection-level send flow control window (how many DATA bytes we can send).
        /// </summary>
        public Int64   ConnectionSendWindow  { get; set; } = 65535;

        /// <summary>
        /// Connection-level receive flow control window (how many DATA bytes the peer can send).
        /// </summary>
        public Int64   ConnectionRecvWindow  { get; set; } = 65535;

        /// <summary>
        /// The peer's INITIAL_WINDOW_SIZE setting (applied to new streams).
        /// </summary>
        public Int64   PeerInitialWindowSize  { get; set; } = 65535;

        /// <summary>
        /// Our INITIAL_WINDOW_SIZE setting (applied to new streams).
        /// </summary>
        public Int64   LocalInitialWindowSize { get; set; } = 65535;

        /// <summary>
        /// Maximum number of concurrent streams allowed (from our SETTINGS).
        /// </summary>
        public UInt32  MaxConcurrentStreams { get; set; } = 100;

        /// <summary>
        /// The highest valid client-initiated stream ID: stream identifiers are a
        /// 31-bit field (RFC 9113, Section 4.1 — the top bit is reserved and
        /// always masked off during parsing), so this is the last one a client
        /// can ever legally send on a given connection.
        /// </summary>
        public const UInt32  MaxStreamId  = 0x7FFFFFFF;

        /// <summary>
        /// How far below MaxStreamId to start warning (RFC 9113, Section 5.1.1)
        /// instead of running the connection all the way to the hard boundary.
        /// </summary>
        private const UInt32 StreamIdExhaustionMargin = 1000;

        /// <summary>
        /// True once LastPeerStreamId is close enough to MaxStreamId that new
        /// streams should be refused and the peer told to migrate to a fresh
        /// connection, rather than let it run to the point where the next stream
        /// ID would fail the "must be greater than the last one" check as an
        /// abrupt connection-level error.
        /// </summary>
        public bool IsNearStreamIdExhaustion
            => LastPeerStreamId >= MaxStreamId - StreamIdExhaustionMargin;


        /// <summary>
        /// Get or create a stream the *peer* initiated (a server's client-opened
        /// odd stream). Validates proper ordering and concurrency limits for
        /// peer-parity IDs. A client never receives peer-initiated streams here
        /// (push is disabled), so for the client role the parity branch is simply
        /// never taken.
        /// </summary>
        public HTTP2Stream GetOrCreateStream(UInt32 StreamId)
        {

            lock (dictLock)
            {

                if (streams.TryGetValue(StreamId, out var existing))
                    return existing;

                // Peer-initiated streams must carry the peer's parity and be
                // monotonically increasing.
                if (StreamId % 2 == PeerInitiatedParity)
                {
                    if (StreamId <= LastPeerStreamId)
                        throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                            $"Stream ID {StreamId} is not greater than last peer stream ID {LastPeerStreamId}");

                    var openCount = 0;
                    foreach (var s in streams.Values)
                    {
                        if (s.State is HTTP2StreamState.Open
                                    or HTTP2StreamState.HalfClosedLocal
                                    or HTTP2StreamState.HalfClosedRemote)
                            openCount++;
                    }

                    if (openCount >= MaxConcurrentStreams)
                        throw new HTTP2StreamException(HTTP2ErrorCode.REFUSED_STREAM, StreamId,
                            $"Maximum concurrent streams ({MaxConcurrentStreams}) exceeded");

                    LastPeerStreamId = StreamId;
                }

                var stream = new HTTP2Stream(StreamId, PeerInitialWindowSize, LocalInitialWindowSize);
                streams[StreamId] = stream;

                return stream;

            }

        }

        /// <summary>
        /// Allocate and register the next locally-initiated stream (client role:
        /// the next odd request stream). Stream IDs are assigned sequentially in
        /// increments of 2 starting from the local parity, monotonically for the
        /// life of the connection (RFC 9113, Section 5.1.1). Enforces
        /// <see cref="MaxConcurrentStreams"/> (for a client, that field carries the
        /// peer server's advertised limit) and refuses once the 31-bit ID space is
        /// exhausted.
        /// </summary>
        public HTTP2Stream CreateLocalStream()
        {

            lock (dictLock)
            {

                var next = LastLocalStreamId == 0
                               ? (LocalInitiatedParity == 1 ? 1u : 2u)
                               : LastLocalStreamId + 2;

                if (next > MaxStreamId)
                    throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                        "Local stream IDs exhausted; open a new connection");

                var openCount = 0;
                foreach (var s in streams.Values)
                {
                    if (s.State is HTTP2StreamState.Open
                                or HTTP2StreamState.HalfClosedLocal
                                or HTTP2StreamState.HalfClosedRemote)
                        openCount++;
                }

                if (openCount >= MaxConcurrentStreams)
                    throw new HTTP2StreamException(HTTP2ErrorCode.REFUSED_STREAM, next,
                        $"Maximum concurrent streams ({MaxConcurrentStreams}) exceeded");

                var stream = new HTTP2Stream(next, PeerInitialWindowSize, LocalInitialWindowSize);
                streams[next]     = stream;
                LastLocalStreamId = next;

                return stream;

            }

        }

        /// <summary>
        /// Try to get an existing stream (returns null if not found).
        /// </summary>
        public HTTP2Stream? TryGetStream(UInt32 StreamId)
        {
            lock (dictLock)
                return streams.TryGetValue(StreamId, out var stream) ? stream : null;
        }

        /// <summary>
        /// Snapshot of every stream that hasn't reached the Closed state, for the
        /// connection's priority-aware writer loop to scan on each send decision.
        /// A snapshot (rather than exposing live enumeration) keeps dictLock's
        /// critical section short and lets the writer loop's priority comparison
        /// run outside of it entirely.
        /// </summary>
        public List<HTTP2Stream> GetSendableStreams()
        {
            lock (dictLock)
                return [.. streams.Values.Where(static s => s.State != HTTP2StreamState.Closed)];
        }

        /// <summary>
        /// True if StreamId has never been touched and is not implicitly closed by
        /// a later stream (RFC 9113, Section 5.1.1) — i.e. it is genuinely "idle".
        /// Only HEADERS and PRIORITY are legal on such a stream; any other frame
        /// referencing it is a connection error, unlike a stream that is merely
        /// closed (where most frame types are tolerated as stragglers).
        /// </summary>
        public bool IsIdle(UInt32 StreamId)
        {

            lock (dictLock)
            {

                if (streams.ContainsKey(StreamId))
                    return false;

                // Peer-initiated streams below the highest one the peer ever opened
                // were implicitly closed by that later HEADERS, not idle.
                if (StreamId % 2 == PeerInitiatedParity)
                    return StreamId > LastPeerStreamId;

                // Locally-initiated parity: idle until we've allocated up to it. For
                // a server (local parity = even, push disabled) LastLocalStreamId
                // stays 0, so every even ID is idle for the whole connection —
                // identical to the previous hardcoded behavior.
                return StreamId > LastLocalStreamId;

            }

        }

        /// <summary>
        /// Remove closed streams to avoid unbounded memory growth.
        /// Call periodically or after processing.
        /// </summary>
        public void PruneClosedStreams()
        {

            lock (dictLock)
            {

                var toRemove = new List<UInt32>();

                foreach (var (id, stream) in streams)
                {
                    if (stream.State == HTTP2StreamState.Closed)
                        toRemove.Add(id);
                }

                foreach (var id in toRemove)
                    streams.Remove(id);

            }

        }

        /// <summary>
        /// When the peer changes INITIAL_WINDOW_SIZE via SETTINGS, we must adjust
        /// the send window of all open/half-closed streams by the delta.
        /// </summary>
        public void AdjustAllStreamWindows(Int64 Delta)
        {

            lock (dictLock)
            {

                foreach (var stream in streams.Values)
                {
                    if (stream.State is HTTP2StreamState.Open
                                     or HTTP2StreamState.HalfClosedLocal
                                     or HTTP2StreamState.HalfClosedRemote)
                    {
                        stream.SendWindow += Delta;

                        if (stream.SendWindow > Int32.MaxValue)
                            throw new HTTP2ConnectionException(HTTP2ErrorCode.FLOW_CONTROL_ERROR,
                                $"Flow control window overflow on stream {stream.StreamId}");
                    }
                }

            }

        }

    }

}
