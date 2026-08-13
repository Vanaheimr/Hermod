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

#region Usings

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

/// <summary>
/// Send side of a stream (RFC 9000 §3.1): buffers bytes to send and produces STREAM frames from
/// them, limited by the peer's flow-control window (max_stream_data) and the maximum frame size.
/// Tracks the send offset and the FIN.
/// </summary>
public sealed class StreamSendBuffer(ulong streamId)
{
    private readonly ByteQueue _pending = new(); // zero-alloc path: O(1) consume instead of List<byte> shifting
    private ulong _sentOffset;
    private bool _finQueued;
    private bool _finSent;

    public StreamId StreamId { get; } = new(streamId);

    /// <summary>
    /// Send limit granted by the peer (max_stream_data). Grows via MAX_STREAM_DATA.
    /// </summary>
    public ulong MaxData { get; set; }

    /// <summary>
    /// Amount of bytes already emitted in frames (send offset).
    /// </summary>
    public ulong SentOffset => _sentOffset;

    /// <summary>
    /// The send side was aborted via RESET_STREAM (RFC 9000 §19.4).
    /// </summary>
    public bool IsReset { get; private set; }

    /// <summary>
    /// The error code of the abort (valid when <see cref="IsReset"/>).
    /// </summary>
    public ulong ResetErrorCode { get; private set; }

    /// <summary>
    /// The final size communicated in the RESET_STREAM (RFC 9000 §4.5: number of bytes already sent).
    /// </summary>
    public ulong ResetFinalSize { get; private set; }

    /// <summary>
    /// A RESET_STREAM frame is waiting to be emitted (picked up by the endpoint).
    /// </summary>
    public bool ResetPending { get; private set; }

    /// <summary>
    /// The abort happened via RESET_STREAM_AT (draft-ietf-quic-reliable-stream-reset) with guaranteed
    /// partial delivery up to <see cref="ReliableSize"/>.
    /// </summary>
    public bool IsResetAt { get; private set; }

    /// <summary>
    /// With a RESET_STREAM_AT, the amount of bytes to deliver reliably; STREAM frames below this
    /// offset keep being retransmitted on loss (draft §5).
    /// </summary>
    public ulong ReliableSize { get; private set; }

    /// <summary>
    /// Bytes written but not yet emitted in frames. The backpressure signal for a streaming
    /// producer: keep filling only while this stays below a watermark, otherwise a fast producer
    /// would buffer the whole body in memory and defeat the point of streaming.
    /// </summary>
    public int PendingBytes => _pending.Count;

    /// <summary>
    /// There is still unemitted data or a pending FIN.
    /// </summary>
    public bool HasPending => !IsReset && (_pending.Count > 0 || (_finQueued && !_finSent));

    /// <summary>
    /// The sender is blocked by the flow-control window (data present, but no credit).
    /// </summary>
    public bool IsBlocked => !IsReset && _pending.Count > 0 && _sentOffset >= MaxData;

    /// <summary>
    /// Nothing more will ever be sent on this stream: either it was reset, or the FIN has gone out
    /// and no data is waiting behind it.
    /// </summary>
    /// <remarks>
    /// Deliberately not "and everything was acknowledged". This drives stream-credit accounting
    /// (RFC 9000 §4.6), where being early is harmless — the credit is handed to the peer, not taken
    /// from it — while being late stalls the peer for a round trip it did not need to wait.
    /// Note the asymmetry with <see cref="HasPending"/>: a stream on which the application has
    /// written nothing and called nothing has no pending data either, but it is not complete.
    /// </remarks>
    public bool IsComplete => IsReset || (_finQueued && _finSent && _pending.Count == 0);

    public void Write(ReadOnlySpan<byte> data)
    {
        if (IsReset)
            return; // no more data is accepted after the reset
        _pending.Append(data);
    }

    /// <summary>
    /// Marks the end of the stream; the next frame that drains the remaining data carries the FIN.
    /// </summary>
    public void Finish() => _finQueued = true;

    /// <summary>
    /// Aborts the send side abruptly (RFC 9000 §19.4): discards unsent data, records the final size
    /// (= bytes sent, §4.5) and lets the endpoint emit a RESET_STREAM. After the abort, STREAM frames
    /// of this stream are neither sent nor retransmitted. Idempotent.
    /// </summary>
    public void Reset(ulong errorCode)
    {
        if (IsReset)
            return;
        IsReset = true;
        ResetPending = true;
        ResetErrorCode = errorCode;
        ResetFinalSize = _sentOffset;
        _pending.Clear();
    }

    /// <summary>
    /// Aborts the send side but guarantees reliable delivery of the first
    /// <paramref name="reliableSize"/> bytes (draft-ietf-quic-reliable-stream-reset §5). Only
    /// already-sent bytes can be guaranteed — the reliable size is clamped to the current send
    /// offset; unsent data is discarded as with an ordinary reset. Idempotent.
    /// </summary>
    public void ResetAt(ulong errorCode, ulong reliableSize)
    {
        if (IsReset)
            return;
        IsReset = true;
        IsResetAt = true;
        ResetPending = true;
        ResetErrorCode = errorCode;
        ResetFinalSize = _sentOffset;
        ReliableSize = Math.Min(reliableSize, _sentOffset); // only what was already sent can be guaranteed
        _pending.Clear();
    }

    /// <summary>
    /// Fetches the RESET_STREAM or RESET_STREAM_AT frame to send (once; loss repetition is handled by
    /// loss recovery). A RESET_STREAM_AT is only produced when the peer announced the extension
    /// (<paramref name="peerSupportsResetAt"/>) — otherwise the abort degrades to an ordinary
    /// RESET_STREAM (without a delivery guarantee).
    /// </summary>
    public Frame? TakeResetFrame(bool peerSupportsResetAt)
    {
        if (!ResetPending)
            return null;
        ResetPending = false;
        return IsResetAt && peerSupportsResetAt
            ? new ResetStreamAtFrame(StreamId.Value, ResetErrorCode, ResetFinalSize, ReliableSize)
            : new ResetStreamFrame(StreamId.Value, ResetErrorCode, ResetFinalSize);
    }

    /// <summary>
    /// Produces the next STREAM frame (up to <paramref name="maxPayload"/> bytes, within the
    /// flow-control window) or <c>null</c> when there is nothing to send.
    /// </summary>
    public StreamFrame? NextFrame(int maxPayload)
    {
        if (_finSent || IsReset)
            return null;

        ulong window = _sentOffset < MaxData ? MaxData - _sentOffset : 0;
        int count = (int)Math.Min(Math.Min((ulong)_pending.Count, (ulong)maxPayload), window);

        // Send a pure FIN frame (without data) only when no data remains outstanding.
        bool fin = _finQueued && _pending.Count == count;
        if (count == 0 && !fin)
            return null;

        // ONE copy per frame is necessary: the frame keeps the bytes for possible retransmissions.
        byte[] data = count > 0 ? _pending.Span[..count].ToArray() : [];
        _pending.Consume(count);

        var frame = new StreamFrame(StreamId.Value, _sentOffset, data, fin);
        _sentOffset += (ulong)count;
        if (fin)
            _finSent = true;
        return frame;
    }
}
