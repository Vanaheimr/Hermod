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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

/// <summary>
/// Result of taking a STREAM fragment into the receive buffer.
/// </summary>
public enum StreamReceiveResult
{
    Ok,
    /// <summary>
    /// The highest offset exceeds the granted flow-control window ⇒ FLOW_CONTROL_ERROR.
    /// </summary>
    FlowControlError,
    /// <summary>
    /// Contradictory final size (FIN) ⇒ FINAL_SIZE_ERROR.
    /// </summary>
    FinalSizeError,
    /// <summary>
    /// RESET_STREAM_AT with reliable size &gt; final size (draft §4) ⇒ FRAME_ENCODING_ERROR.
    /// </summary>
    FrameEncodingError,
    /// <summary>
    /// Another RESET_STREAM_AT/RESET_STREAM changes the error code (draft §5.2) ⇒ STREAM_STATE_ERROR.
    /// </summary>
    StreamStateError,
}

/// <summary>
/// Receive side of a stream (RFC 9000 §2.2, §3.2): reassembles (also unordered, overlapping)
/// STREAM data into an ordered byte stream, tracks the FIN/final size and enforces the
/// flow-control window. Consumed bytes are discarded; <see cref="ReadAvailable"/> returns the
/// next contiguous section.
/// </summary>
public sealed class StreamReceiveBuffer
{
    private readonly SortedDictionary<ulong, byte[]> _fragments = new();
    private ulong _readOffset;
    private ulong? _finalSize;
    private ulong _flowControlConsumed; // final size adopted at RESET(_AT) (credit accounting, decoupled from the read offset)
    private ulong? _reliableSize;       // smallest reliable size of a RESET_STREAM_AT (draft §5.2)

    /// <summary>
    /// Highest received offset (end of the farthest-reaching fragment).
    /// </summary>
    public ulong HighestReceivedOffset { get; private set; }

    /// <summary>
    /// Granted flow-control limit for this stream (max_stream_data). Grows via MAX_STREAM_DATA.
    /// </summary>
    public ulong MaxData { get; set; } = ulong.MaxValue;

    /// <summary>
    /// Auto-tuning of the receive-side stream window (phase 9). <c>null</c> = fixed window (e.g. for
    /// streams without a set limit); populated by the endpoint on open with the matching starting
    /// value and growth limit.
    /// </summary>
    public ReceiveWindowTuner? WindowTuner { get; set; }

    /// <summary>
    /// Offset "consumed" for flow-control accounting. After a RESET(_AT) the final size counts fully
    /// as consumed (§4.5), even if the read offset (with RESET_STREAM_AT) still lingers at the
    /// reliable size while the application collects the reliably delivered bytes.
    /// </summary>
    public ulong BytesConsumed => Math.Max(_readOffset, _flowControlConsumed);

    /// <summary>
    /// The peer aborted the receive side via RESET_STREAM_AT with guaranteed partial delivery; this
    /// is the (smallest) reliable size up to which bytes are still delivered to the application
    /// (draft §5). <c>null</c> = no RESET_STREAM_AT.
    /// </summary>
    public ulong? ReliableSize => _reliableSize;

    /// <summary>
    /// FIN received (final size known).
    /// </summary>
    public bool FinReceived => _finalSize.HasValue;

    /// <summary>
    /// The peer aborted this receive side via RESET_STREAM (RFC 9000 §19.4).
    /// </summary>
    public bool ResetReceived { get; private set; }

    /// <summary>
    /// The error code from the peer's RESET_STREAM (valid when <see cref="ResetReceived"/>).
    /// </summary>
    public ulong ResetErrorCode { get; private set; }

    /// <summary>
    /// We aborted reading (RFC 9000 §3.5); a STOP_SENDING may be waiting for emission.
    /// </summary>
    public bool ReadingAborted { get; private set; }

    /// <summary>
    /// The error code of our read abort (valid when <see cref="ReadingAborted"/>).
    /// </summary>
    public ulong AbortErrorCode { get; private set; }

    /// <summary>
    /// A STOP_SENDING frame is waiting to be emitted (picked up by the endpoint).
    /// </summary>
    public bool StopSendingPending { get; private set; }

    /// <summary>
    /// All data up to the FIN has been read. A stream aborted via RESET NEVER counts as complete –
    /// the application recognises the abort via <see cref="ResetReceived"/>.
    /// </summary>
    public bool IsComplete => !ResetReceived && _finalSize == _readOffset && _fragments.Count == 0;

    /// <summary>
    /// Aborts reading (RFC 9000 §3.5): the endpoint sends a STOP_SENDING with
    /// <paramref name="errorCode"/>; already-buffered data is discarded. Idempotent.
    /// </summary>
    public void AbortReading(ulong errorCode)
    {
        if (ReadingAborted || ResetReceived)
            return; // §3.5: STOP_SENDING only for streams the peer has not already reset
        ReadingAborted = true;
        StopSendingPending = true;
        AbortErrorCode = errorCode;
        _fragments.Clear();
    }

    /// <summary>
    /// Fetches the STOP_SENDING frame to send (once; loss repetition is handled by loss recovery).
    /// Does not include the stream ID – the caller knows it.
    /// </summary>
    public ulong? TakeStopSendingErrorCode()
    {
        if (!StopSendingPending)
            return null;
        StopSendingPending = false;
        return AbortErrorCode;
    }

    /// <summary>
    /// Processes a received RESET_STREAM (RFC 9000 §19.4/§4.5): checks the final size against flow
    /// control and what was already seen, adopts it (binding flow-control accounting) and discards
    /// buffered data (§19.4: "can discard any data").
    /// </summary>
    public StreamReceiveResult Reset(ulong errorCode, ulong finalSize)
    {
        if (finalSize > MaxData)
            return StreamReceiveResult.FlowControlError;   // §4.1: the final size consumes credit
        if (_finalSize is { } known && known != finalSize)
            return StreamReceiveResult.FinalSizeError;     // §4.5: a known final size is immutable
        if (HighestReceivedOffset > finalSize)
            return StreamReceiveResult.FinalSizeError;     // §4.5: data seen beyond the final size

        if (ResetReceived)
            return StreamReceiveResult.Ok; // idempotent (retransmission of the RESET_STREAM)

        ResetReceived = true;
        ResetErrorCode = errorCode;
        _finalSize = finalSize;
        HighestReceivedOffset = finalSize;
        // §4.5: the final size counts fully as consumed flow-control credit — book it as "consumed"
        // so the connection's MAX_DATA window arithmetic stays consistent.
        _flowControlConsumed = finalSize;
        _readOffset = finalSize;
        _fragments.Clear();
        return StreamReceiveResult.Ok;
    }

    /// <summary>
    /// Processes a received RESET_STREAM_AT (draft-ietf-quic-reliable-stream-reset §4/§5): like
    /// <see cref="Reset"/>, but the first <paramref name="reliableSize"/> bytes are still delivered
    /// to the application. Repeated frames may only lower the reliable size (§5.2); increases (from
    /// reordering) are ignored, a changed error code is a STREAM_STATE_ERROR.
    /// </summary>
    public StreamReceiveResult ResetAt(ulong errorCode, ulong finalSize, ulong reliableSize)
    {
        if (reliableSize > finalSize)
            return StreamReceiveResult.FrameEncodingError; // draft §4
        if (finalSize > MaxData)
            return StreamReceiveResult.FlowControlError;    // §4.1: the final size consumes credit
        if (_finalSize is { } known && known != finalSize)
            return StreamReceiveResult.FinalSizeError;      // §4.5: a known final size is immutable
        if (!ResetReceived && HighestReceivedOffset > finalSize)
            return StreamReceiveResult.FinalSizeError;      // §4.5: data seen beyond the final size
        if (ResetReceived && errorCode != ResetErrorCode)
            return StreamReceiveResult.StreamStateError;    // draft §5.2: the error code must not change

        if (ResetReceived)
        {
            // §5.2: the reliable size may only decrease; ignore increases (reordering).
            if (_reliableSize is { } prev && reliableSize < prev)
            {
                _reliableSize = reliableSize;
                TrimAboveReliable(reliableSize);
            }
            return StreamReceiveResult.Ok;
        }

        ResetReceived = true;
        ResetErrorCode = errorCode;
        _finalSize = finalSize;
        _reliableSize = reliableSize;
        HighestReceivedOffset = finalSize;
        // §4.5: the full final size counts as consumed credit; the read offset however lingers at the
        // reliable size until the application has collected the reliably delivered bytes.
        _flowControlConsumed = finalSize;
        TrimAboveReliable(reliableSize); // data beyond the reliable size is no longer delivered
        return StreamReceiveResult.Ok;
    }

    /// <summary>
    /// Discards buffered fragments beyond the reliable size and trims a fragment crossing the
    /// boundary (draft §5.2: nothing beyond the reliable size is delivered anymore).
    /// </summary>
    private void TrimAboveReliable(ulong reliableSize)
    {
        foreach (ulong start in _fragments.Keys.ToArray())
        {
            if (start >= reliableSize)
            {
                _fragments.Remove(start);
                continue;
            }
            byte[] data = _fragments[start];
            if (start + (ulong)data.Length > reliableSize)
                _fragments[start] = data[..(int)(reliableSize - start)];
        }
    }

    /// <summary>
    /// Takes in a STREAM fragment.
    /// </summary>
    public StreamReceiveResult Receive(ulong offset, ReadOnlySpan<byte> data, bool fin)
    {
        ulong end = offset + (ulong)data.Length;
        if (end > MaxData)
            return StreamReceiveResult.FlowControlError;

        if (fin)
        {
            if (_finalSize is { } existing && existing != end)
                return StreamReceiveResult.FinalSizeError;
            // Data must not extend past the final size.
            if (HighestReceivedOffset > end)
                return StreamReceiveResult.FinalSizeError;
            _finalSize = end;
        }
        else if (_finalSize is { } fs && end > fs)
        {
            return StreamReceiveResult.FinalSizeError;
        }

        if (end > HighestReceivedOffset)
            HighestReceivedOffset = end;

        // Skip data already consumed.
        if (!data.IsEmpty && end > _readOffset)
        {
            ulong start = offset;
            ReadOnlySpan<byte> slice = data;
            if (start < _readOffset)
            {
                slice = data[(int)(_readOffset - start)..];
                start = _readOffset;
            }
            // After RESET_STREAM_AT, buffer nothing beyond the reliable size (draft §5.2).
            if (_reliableSize is { } rel)
            {
                if (start >= rel)
                    return StreamReceiveResult.Ok;
                if (start + (ulong)slice.Length > rel)
                    slice = slice[..(int)(rel - start)];
            }
            _fragments[start] = slice.ToArray();
        }

        return StreamReceiveResult.Ok;
    }

    /// <summary>
    /// Returns the next contiguous, not-yet-read section and advances the read offset. An empty
    /// array when no contiguous data is (yet) available. Zero-alloc path: the frequent empty case
    /// (every pump pass queries every stream) costs nothing; otherwise the total length is
    /// determined up front and exactly ONE result array is filled.
    /// </summary>
    public byte[] ReadAvailable()
    {
        if (_fragments.Count == 0)
            return [];

        // First pass: determine the contiguous length from the read offset (without copying).
        ulong cursor = _readOffset;
        int total = 0;
        foreach ((ulong start, byte[] data) in _fragments)
        {
            if (start > cursor)
                break; // gap
            ulong end = start + (ulong)data.Length;
            if (end > cursor)
                total += (int)(end - cursor);
            cursor = Math.Max(cursor, end);
        }
        if (total == 0)
            return [];

        // Second pass: fill exactly one result array, remove consumed fragments.
        byte[] result = new byte[total];
        int written = 0;
        while (_fragments.Count > 0)
        {
            (ulong start, byte[] data) = First();
            if (start > _readOffset)
                break;
            int skip = (int)(_readOffset - start);
            if (skip < data.Length)
            {
                data.AsSpan(skip).CopyTo(result.AsSpan(written));
                written += data.Length - skip;
                _readOffset = start + (ulong)data.Length;
            }
            _fragments.Remove(start);
        }
        return result;
    }

    private (ulong, byte[]) First()
    {
        foreach (KeyValuePair<ulong, byte[]> kv in _fragments)
            return (kv.Key, kv.Value);
        return (0, []);
    }
}
