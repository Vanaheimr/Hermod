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
        /// DATA bytes waiting in <see cref="RequestBodyChannel"/> or
        /// <see cref="TunnelInbound"/> for the handler to read them, whose receive
        /// window the server withholds until it does. Once nothing will read them —
        /// the stream was reset, or its handler has ended, or none was started —
        /// the connection's share of that window is returned all at once (see
        /// <see cref="UnreadWindowReturned"/>). Kept by the server's connection,
        /// under the lock of its receive windows.
        /// </summary>
        internal Int64            UnreadRecvBytes   { get; set; }

        /// <summary>
        /// True once the connection window of every byte in
        /// <see cref="UnreadRecvBytes"/> has been returned, because nothing will
        /// read them: after a reset, and once the stream's handler has ended, or
        /// none was started. Reading them returns nothing a second time, and
        /// nothing is withheld on the stream any more: DATA that arrives afterwards
        /// is dropped, and its window returned at once. Kept like
        /// <see cref="UnreadRecvBytes"/>.
        /// </summary>
        internal bool             UnreadWindowReturned { get; set; }

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
        /// declared one. Used to enforce RFC 9113, Section 8.1.1: the declared
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
        /// A reset, or the end of the tunnel's handler, returns the connection's
        /// share of the window for what is still unread at once (see
        /// <see cref="UnreadRecvBytes"/>): after either, nobody has to read it. The
        /// handler's end completes the channel as a reset does.
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
        /// <see cref="IHTTP2RequestStream.ReadAsync"/>. Completed at END_STREAM, or,
        /// if the stream is reset first, with an <see cref="OperationCanceledException"/>
        /// that carries its <see cref="CancellationToken"/> (see <see cref="Reset"/>),
        /// or, if its handler ends first, with an <see cref="InvalidOperationException"/>:
        /// nothing is added once nothing reads it. Flow-controlled like
        /// <see cref="TunnelInbound"/>.
        /// </summary>
        public Channel<byte[]>?    RequestBodyChannel { get; set; }

        /// <summary>
        /// Running total of DATA payload bytes received on this stream — used to
        /// validate <see cref="ExpectedContentLength"/> (RFC 9113, Section 8.1.1)
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
        /// Cancelled when the stream is forcibly closed (<see cref="Reset"/>) — by an
        /// RST_STREAM, sent or received, or by the end of its connection — so a
        /// running <c>HTTP2RequestHandler</c> invocation for this stream can be
        /// told to stop instead of running to completion for a peer that already
        /// walked away. Never disposed — its lifetime is tied to this stream
        /// object, it holds no timer/unmanaged resources, and disposing it would
        /// risk an ObjectDisposedException if a handler read the token concurrently.
        /// </summary>
        private readonly CancellationTokenSource  requestCancellation  = new();

        /// <summary>
        /// Signaled when this stream is reset — see <see cref="requestCancellation"/>.
        /// </summary>
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

            bool closed;

            lock (stateLock)
            {
                State = State switch {
                    HTTP2StreamState.Open            => HTTP2StreamState.HalfClosedRemote,
                    HTTP2StreamState.HalfClosedLocal => HTTP2StreamState.Closed,
                    _                                => throw new HTTP2StreamException(
                                                            HTTP2ErrorCode.STREAM_CLOSED, StreamId,
                                                            $"Cannot close remote on stream {StreamId} in state {State}")
                };
                closed = State == HTTP2StreamState.Closed;
            }

            if (closed)
                AbandonOutboundOnClose();

        }

        /// <summary>
        /// Transition when we send END_STREAM.
        /// </summary>
        public void CloseLocal()
        {

            bool closed;

            lock (stateLock)
            {
                State = State switch {
                    HTTP2StreamState.Open             => HTTP2StreamState.HalfClosedLocal,
                    HTTP2StreamState.HalfClosedRemote => HTTP2StreamState.Closed,
                    _                                 => throw new HTTP2StreamException(
                                                             HTTP2ErrorCode.STREAM_CLOSED, StreamId,
                                                             $"Cannot close local on stream {StreamId} in state {State}")
                };
                closed = State == HTTP2StreamState.Closed;
            }

            if (closed)
                AbandonOutboundOnClose();

        }

        /// <summary>
        /// Transition when we send END_STREAM, if our side is still open — and
        /// return false instead of throwing when it is not. Checked and changed
        /// under one lock, because the usual reason it is not open is a reset that
        /// raced the END_STREAM: once the frame is on the wire the peer may answer
        /// it with RST_STREAM, and the read loop may handle that before the sender
        /// gets round to this transition. Testing <see cref="State"/> first and
        /// then calling <see cref="CloseLocal"/> leaves that gap open.
        /// </summary>
        public bool TryCloseLocal()
        {

            lock (stateLock)
            {

                switch (State)
                {

                    case HTTP2StreamState.Open:
                        State = HTTP2StreamState.HalfClosedLocal;
                        return true;

                    case HTTP2StreamState.HalfClosedRemote:
                        State = HTTP2StreamState.Closed;
                        break;

                    default:
                        return false;

                }

            }

            AbandonOutboundOnClose();

            return true;

        }

        /// <summary>
        /// Reset this stream as <see cref="Reset()"/> does, but only while our side
        /// has ended and the peer's has not — half-closed (local) — and return
        /// whether it was reset. Checked and changed under one lock, as in
        /// <see cref="TryCloseLocal"/>: the read loop may handle the peer's
        /// END_STREAM at any moment, and a stream both sides have ended is closed,
        /// and gets no RST_STREAM. For a server that has sent a complete response
        /// and asks the client to stop sending a request nothing reads, with
        /// RST_STREAM NO_ERROR (RFC 9113, Section 8.1). What the client sent before
        /// it read that is discarded, as after any reset of ours while it could
        /// still send (<see cref="DiscardsPeerFrames"/>).
        /// </summary>
        public bool TryResetHalfClosedLocal()
        {

            lock (stateLock)
            {

                if (State != HTTP2StreamState.HalfClosedLocal)
                    return false;

                DiscardsPeerFrames  = true;
                State               = HTTP2StreamState.Closed;
                WasReset            = true;

            }

            // Outside the lock, as always: Reset finds the stream closed and reset
            // already, and releases what a reset releases.
            Reset();

            return true;

        }

        /// <summary>
        /// Once both sides have ended this stream it is closed, and the writer
        /// loop's picker skips it: whatever is still queued on it, or is queued
        /// later, would never be sent, and its producer would wait for the whole
        /// connection to end. Only a write after the end of our own side can be
        /// in that position, but it is released at once all the same — outside
        /// <see cref="stateLock"/>, as <see cref="Reset"/> releases its producers.
        /// A close is no reset: the stream's own token stays as it is.
        /// </summary>
        private void AbandonOutboundOnClose()

            => OutboundQueue.AbandonAll();

        /// <summary>
        /// True once this stream was closed by an RST_STREAM (sent or received),
        /// as opposed to a clean END_STREAM close. RFC 9113, Section 5.1 treats a
        /// later frame differently in the two cases: after RST_STREAM it's a stream
        /// error, after END_STREAM it's a connection error — unless the RST_STREAM
        /// was ours, see <see cref="DiscardsPeerFrames"/>.
        /// </summary>
        public bool WasReset { get; private set; }

        /// <summary>
        /// True once we reset this stream while the peer could still send on it:
        /// while it was open, or closed on our side only. What the peer sent
        /// before it read our RST_STREAM is still to come, and RFC 9113, Section
        /// 5.1 has it minimally processed and then discarded, not answered. False
        /// after the peer's own reset (<see cref="ResetByPeer()"/>), and after ours
        /// once the peer had ended its side: it has nothing left to send then,
        /// and a frame it sends all the same is an error.
        /// </summary>
        public bool DiscardsPeerFrames { get; private set; }

        /// <summary>
        /// The error code of the peer's RST_STREAM that closed this stream, as
        /// <see cref="ResetByPeer(HTTP2ErrorCode)"/> keeps it; null before that,
        /// and after a reset that was not given one. The client passes it, so
        /// that a header block it refuses on the reset stream says why, as the
        /// response side of the stream does. Read under the lock of the
        /// transitions, under which the reset sets it.
        /// </summary>
        public HTTP2ErrorCode? PeerResetCode
        {
            get
            {
                lock (stateLock)
                    return peerResetCode;
            }
        }

        private HTTP2ErrorCode? peerResetCode;

        /// <summary>
        /// Forcibly close: we send RST_STREAM, or the connection ends.
        /// </summary>
        public void Reset()

            => Reset(ByPeer: false);

        /// <summary>
        /// Forcibly close, as the peer's RST_STREAM does. Everything else is as
        /// for <see cref="Reset()"/>.
        /// </summary>
        public void ResetByPeer()

            => Reset(ByPeer: true);

        /// <summary>
        /// Forcibly close, as the peer's RST_STREAM does, and keep the error code
        /// it carried (see <see cref="PeerResetCode"/>). Everything else is as for
        /// <see cref="Reset()"/>.
        /// </summary>
        public void ResetByPeer(HTTP2ErrorCode ErrorCode)

            => Reset(ByPeer: true, ErrorCode);

        private void Reset(bool ByPeer, HTTP2ErrorCode? PeerErrorCode = null)
        {
            lock (stateLock)
            {

                // Under the lock of the transitions, so that the state tested is
                // the one the reset ends.
                if (!ByPeer && State is (HTTP2StreamState.Open or HTTP2StreamState.HalfClosedLocal))
                    DiscardsPeerFrames = true;

                // The first code stays: a later reset changes nothing on a stream
                // that is closed already.
                peerResetCode ??= PeerErrorCode;

                State    = HTTP2StreamState.Closed;
                WasReset = true;

            }

            // The token is cancelled at once, and what is registered on it runs on
            // the thread pool: not on this thread, which is the writer loop's for
            // a write that failed, and the read loop's for the peer's RST_STREAM.
            // Cancel() ran the callbacks here, and with them the continuation of
            // every handler awaiting with this token: a tunnel's handler went on
            // inside the writer loop's reset, down to the end of its reading, and
            // waited there for the receive-window lock before the writer loop had
            // sent its RST_STREAM (found by the CI of 470081d9). And a callback's
            // exception ended up in whichever loop made the reset. Outside the
            // lock all the same: a callback re-entering this stream must not find
            // stateLock held. Safe to call repeatedly (e.g. RST_STREAM sent by us
            // and then also received from the peer): CancelAsync is idempotent.
            _ = requestCancellation.CancelAsync();

            // Unblock a tunnel handler possibly waiting in HTTP2Tunnel.ReadAsync —
            // without this, a reset mid-tunnel would leave it awaiting forever
            // (the cancellation token covers *sending*, not this channel read).
            TunnelInbound?.Writer.TryComplete();

            // Likewise a streaming handler possibly waiting in
            // HTTP2RequestStream.ReadAsync — the token covers that read only if the
            // handler passes it — and fail every read it makes from now on, once
            // the chunks that did arrive are read: with this stream's token, the
            // handler's, as a write on a reset stream fails. Not completed without
            // an error, as the tunnel's channel is: a body read to its end is a
            // whole one, and a truncated upload must not look like one. A body the
            // peer had already ended stays whole; TryComplete leaves a completed
            // channel as it is.
            RequestBodyChannel?.Writer.TryComplete(new OperationCanceledException($"Stream {StreamId} was reset", requestCancellation.Token));

            // Unblock a producer possibly awaiting HTTP2OutboundQueue.EnqueueAsync
            // for bytes that will now never be sent — the writer loop's picker
            // skips Closed streams, so without this the data would sit queued
            // forever and the producer would never come back. The queue refuses
            // later writes at once as well, and every one of them fails with this
            // stream's token: the handler's, so a write on a reset stream ends
            // the handler as the handler's own check of its token would.
            OutboundQueue.AbandonAll(requestCancellation.Token);
        }

        #endregion

    }

}
