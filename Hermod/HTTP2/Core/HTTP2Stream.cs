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
    /// Represents a single HTTP/2 stream with its state machine (RFC 9113, Section 5.1)
    /// and per-stream flow control window.
    /// </summary>
    public sealed class HTTP2Stream
    {

        public UInt32             StreamId        { get; }
        public HTTP2StreamState   State           { get; private set; } = HTTP2StreamState.Idle;

        /// <summary>
        /// Outbound flow control window (how many bytes WE can still send to the peer).
        /// Initialized to the peer's INITIAL_WINDOW_SIZE setting.
        /// </summary>
        public Int64              SendWindow      { get; set; }

        /// <summary>
        /// Inbound flow control window (how many bytes the peer can still send to us).
        /// Initialized to our INITIAL_WINDOW_SIZE setting.
        /// </summary>
        public Int64              RecvWindow      { get; set; }

        /// <summary>
        /// Bytes consumed on this stream since the last stream-level WINDOW_UPDATE we
        /// sent — accumulated so we can replenish in batches (once it crosses a
        /// fraction of the window) instead of one WINDOW_UPDATE per DATA frame.
        /// </summary>
        public Int64              PendingRecvUpdate { get; set; }

        /// <summary>
        /// Tracks whether END_STREAM was set on the HEADERS frame
        /// while waiting for CONTINUATION frames to complete the header block.
        /// </summary>
        public bool               EndStreamPending  { get; set; }

        /// <summary>
        /// Accumulates HEADERS/CONTINUATION fragments until END_HEADERS is received.
        /// </summary>
        public MemoryStream?      HeaderBuffer    { get; set; }

        /// <summary>
        /// The decoded request headers once the full header block is received.
        /// </summary>
        public List<(string Name, string Value)>?  RequestHeaders  { get; set; }

        /// <summary>
        /// The decoded trailer fields, if the request sent a second HEADERS block
        /// after DATA (RFC 9113, Section 8.1). Null unless trailers were sent.
        /// Set once <see cref="RequestHeaders"/> is already populated — that's how
        /// a second header block on the same stream is recognized as trailers.
        /// </summary>
        public List<(string Name, string Value)>?  Trailers        { get; set; }

        /// <summary>
        /// Accumulates DATA frame payloads for the request body.
        /// </summary>
        public MemoryStream?      RequestBody     { get; set; }

        /// <summary>
        /// The value of the request's <c>content-length</c> header field, if it
        /// declared one. Used to enforce RFC 9113, Section 8.1.2.6: the declared
        /// length MUST equal the sum of the DATA frame payload lengths, else the
        /// request is malformed. Null when no (valid) content-length was sent.
        /// </summary>
        public long?              ExpectedContentLength { get; set; }

        /// <summary>
        /// True once this stream has been recognized as an accepted CONNECT
        /// tunnel (RFC 9113, Section 8.5; RFC 8441 extended CONNECT). Once set,
        /// DATA frames are routed to <see cref="TunnelInbound"/> instead of
        /// buffering into <see cref="RequestBody"/>, and the stream is dispatched
        /// to the connect handler rather than the ordinary request handler — a
        /// CONNECT tunnel has no "complete body, single response" request/response
        /// cycle, just a bidirectional byte stream for as long as it's open.
        /// </summary>
        public bool                IsConnectTunnel { get; set; }

        /// <summary>
        /// Inbound side of an accepted CONNECT tunnel: DATA frame payloads the
        /// peer sends are written here by the frame read loop as they arrive
        /// (HandleDataAsync) and read by the application's tunnel handler
        /// (HTTP2Tunnel.ReadAsync). The channel is unbounded, but it is bounded in
        /// practice by flow control: the receive window for these bytes is returned
        /// only as the consumer reads them (consumption-driven backpressure — see
        /// HandleDataAsync / ReplenishConsumedAsync), so the peer can never have
        /// more than a window's worth in flight, and a slow consumer simply leaves
        /// the peer's window depleted rather than growing this queue without bound.
        /// </summary>
        public Channel<byte[]>?    TunnelInbound   { get; set; }

        /// <summary>
        /// True once this stream is being handled by a streaming request handler
        /// (<see cref="HTTP2StreamingHandler"/>). Like a CONNECT tunnel, its DATA
        /// frames are routed to <see cref="RequestBodyChannel"/> as they arrive
        /// (rather than buffered into <see cref="RequestBody"/>), and the handler is
        /// dispatched at HEADERS-complete rather than at END_STREAM — but unlike a
        /// tunnel it keeps ordinary request/response + trailer semantics.
        /// </summary>
        public bool                IsStreamingRequest { get; set; }

        /// <summary>
        /// Inbound request-body chunks for a streaming request, written by the frame
        /// read loop as DATA arrives and read by the handler via
        /// <see cref="IHTTP2RequestStream.ReadAsync"/>. Completed at END_STREAM.
        /// </summary>
        public Channel<byte[]>?    RequestBodyChannel { get; set; }

        /// <summary>
        /// Running total of DATA payload bytes received on this stream — used to
        /// validate <see cref="ExpectedContentLength"/> (RFC 9113, Section 8.1.2.6)
        /// on the streaming path, where there is no buffered <see cref="RequestBody"/>
        /// whose length could be checked instead.
        /// </summary>
        public long                ReceivedBodyLength { get; set; }

        /// <summary>
        /// RFC 9218 priority (urgency + incremental). Set from the request's
        /// "priority" header field if present (default otherwise), and
        /// updatable afterwards via a PRIORITY_UPDATE frame or a "priority"
        /// response header — read by the connection's writer loop to decide send
        /// order among concurrent streams.
        /// </summary>
        public HTTP2Priority       Priority        { get; set; } = HTTP2Priority.Default;

        /// <summary>
        /// Queued response/tunnel body bytes not yet handed to the wire — see
        /// <see cref="HTTP2OutboundQueue"/> and the connection's writer loop.
        /// </summary>
        public HTTP2OutboundQueue  OutboundQueue   { get; } = new();

        /// <summary>
        /// Set by the writer loop each time this stream is chosen to send;
        /// breaks priority ties round-robin-fairly (least-recently-served goes
        /// first) instead of always favoring whichever stream happens to be
        /// enumerated first.
        /// </summary>
        public long                LastServedSequence  { get; set; } = -1;

        /// <summary>
        /// Cancelled when the stream is forcibly closed (<see cref="Reset"/>), so a
        /// running <c>HTTP2RequestHandler</c> invocation for this stream can be
        /// told to stop instead of running to completion for a peer that already
        /// walked away. Never disposed — its lifetime is tied to this stream
        /// object, it holds no timer/unmanaged resources, and disposing it would
        /// risk an ObjectDisposedException if a handler read the token concurrently.
        /// </summary>
        private readonly CancellationTokenSource  requestCancellation  = new();

        /// <summary>Signaled when this stream is reset — see <see cref="requestCancellation"/>.</summary>
        public CancellationToken  CancellationToken  => requestCancellation.Token;


        public HTTP2Stream(UInt32 StreamId, Int64 InitialSendWindow, Int64 InitialRecvWindow)
        {
            this.StreamId   = StreamId;
            this.SendWindow = InitialSendWindow;
            this.RecvWindow = InitialRecvWindow;
        }


        #region State transitions (RFC 9113, Section 5.1)

        /// <summary>
        /// Makes state transitions atomic — the connection's read loop and the
        /// per-stream response task may transition concurrently.
        /// </summary>
        private readonly object stateLock = new();

        /// <summary>
        /// Transition to Open state (receiving HEADERS on an idle stream).
        /// </summary>
        public void Open()
        {

            lock (stateLock)
            {

                if (State != HTTP2StreamState.Idle)
                    throw new HTTP2StreamException(HTTP2ErrorCode.PROTOCOL_ERROR, StreamId,
                                                   $"Cannot open stream {StreamId} in state {State}");

                State = HTTP2StreamState.Open;

            }

        }

        /// <summary>
        /// Transition when the remote peer sends END_STREAM.
        /// </summary>
        public void CloseRemote()
        {

            lock (stateLock)
            {
                State = State switch {
                    HTTP2StreamState.Open            => HTTP2StreamState.HalfClosedRemote,
                    HTTP2StreamState.HalfClosedLocal => HTTP2StreamState.Closed,
                    _                                => throw new HTTP2StreamException(
                                                            HTTP2ErrorCode.STREAM_CLOSED, StreamId,
                                                            $"Cannot close remote on stream {StreamId} in state {State}")
                };
            }

        }

        /// <summary>
        /// Transition when we send END_STREAM.
        /// </summary>
        public void CloseLocal()
        {

            lock (stateLock)
            {
                State = State switch {
                    HTTP2StreamState.Open             => HTTP2StreamState.HalfClosedLocal,
                    HTTP2StreamState.HalfClosedRemote => HTTP2StreamState.Closed,
                    _                                 => throw new HTTP2StreamException(
                                                             HTTP2ErrorCode.STREAM_CLOSED, StreamId,
                                                             $"Cannot close local on stream {StreamId} in state {State}")
                };
            }

        }

        /// <summary>
        /// True once this stream was closed by an RST_STREAM (sent or received),
        /// as opposed to a clean END_STREAM close. RFC 9113, Section 5.1 treats a
        /// later frame differently in the two cases: after RST_STREAM it's a stream
        /// error, after END_STREAM it's a connection error.
        /// </summary>
        public bool WasReset { get; private set; }

        /// <summary>
        /// Forcibly close (RST_STREAM received or sent).
        /// </summary>
        public void Reset()
        {
            lock (stateLock)
            {
                State    = HTTP2StreamState.Closed;
                WasReset = true;
            }

            // Outside the lock: Cancel() runs registered callbacks synchronously,
            // and a handler's callback re-entering this stream while stateLock is
            // held would deadlock. Safe to call repeatedly (e.g. RST_STREAM sent
            // by us and then also received from the peer) — Cancel() is idempotent.
            requestCancellation.Cancel();

            // Unblock a tunnel handler possibly waiting in HTTP2Tunnel.ReadAsync —
            // without this, a reset mid-tunnel would leave it awaiting forever
            // (the cancellation token covers *sending*, not this channel read).
            TunnelInbound?.Writer.TryComplete();

            // Unblock a producer possibly awaiting HTTP2OutboundQueue.EnqueueAsync
            // for bytes that will now never be sent — the writer loop's picker
            // skips Closed streams, so without this the data would sit queued
            // forever and the producer would never come back.
            OutboundQueue.AbandonAll();
        }

        #endregion

    }

}
