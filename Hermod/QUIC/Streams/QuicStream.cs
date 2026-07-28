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
/// A QUIC stream: bundles the send and receive side (RFC 9000 §2). Bidirectional streams use both,
/// unidirectional ones only one direction. The actual frame production/intake is handled by
/// <see cref="StreamSendBuffer"/> and <see cref="StreamReceiveBuffer"/>; this class offers the
/// application-facing view.
/// </summary>
public sealed class QuicStream
{
    public StreamId Id { get; }
    public StreamSendBuffer Send { get; }
    public StreamReceiveBuffer Receive { get; }

    public QuicStream(StreamId id, ulong initialSendLimit = 0, ulong initialReceiveLimit = ulong.MaxValue)
    {
        Id = id;
        Send = new StreamSendBuffer(id.Value) { MaxData = initialSendLimit };
        Receive = new StreamReceiveBuffer { MaxData = initialReceiveLimit };
    }

    /// <summary>
    /// Writes application data into the send buffer.
    /// </summary>
    public void Write(ReadOnlySpan<byte> data) => Send.Write(data);

    /// <summary>
    /// Marks the end of the sent stream (FIN).
    /// </summary>
    public void Finish() => Send.Finish();

    /// <summary>
    /// Reads the next contiguous received section.
    /// </summary>
    public byte[] Read() => Receive.ReadAvailable();

    /// <summary>
    /// The received stream has been read completely (FIN reached). Never <c>true</c> after a peer reset.
    /// </summary>
    public bool IsReceiveComplete => Receive.IsComplete;

    /// <summary>
    /// Aborts the send side abruptly (RFC 9000 §2.4/§19.4): unsent data is discarded, the endpoint
    /// sends a RESET_STREAM with <paramref name="errorCode"/> (reliably, via loss recovery).
    /// </summary>
    public void Reset(ulong errorCode) => Send.Reset(errorCode);

    /// <summary>
    /// Aborts the send side but guarantees reliable delivery of the first
    /// <paramref name="reliableSize"/> already-sent bytes (draft-ietf-quic-reliable-stream-reset §5):
    /// the endpoint sends a RESET_STREAM_AT (when the peer supports the extension, otherwise an
    /// ordinary RESET_STREAM). Useful when the receiver must see a critical prefix (e.g. the
    /// WebTransport stream header) despite the abort.
    /// </summary>
    public void ResetAt(ulong errorCode, ulong reliableSize) => Send.ResetAt(errorCode, reliableSize);

    /// <summary>
    /// Aborts reading (RFC 9000 §2.4/§3.5): the endpoint sends a STOP_SENDING with
    /// <paramref name="errorCode"/>, thereby asking the peer for a RESET_STREAM of its send side.
    /// </summary>
    public void AbortRead(ulong errorCode) => Receive.AbortReading(errorCode);

    /// <summary>
    /// The peer aborted its send side via RESET_STREAM (error code in
    /// <see cref="PeerResetErrorCode"/>).
    /// </summary>
    public bool IsResetByPeer => Receive.ResetReceived;

    /// <summary>
    /// The error code from the peer's RESET_STREAM, if received.
    /// </summary>
    public ulong? PeerResetErrorCode => Receive.ResetReceived ? Receive.ResetErrorCode : null;

    /// <summary>
    /// For a receive stream aborted via RESET_STREAM_AT, the (smallest) reliable size up to which the
    /// peer still delivers the bytes reliably (draft-ietf-quic-reliable-stream-reset §5); <c>null</c>
    /// for an ordinary RESET_STREAM or no reset at all.
    /// </summary>
    public ulong? PeerReliableSize => Receive.ReliableSize;

    /// <summary>
    /// The error code from a received peer STOP_SENDING, if received (our send side was thereupon
    /// reset automatically, RFC 9000 §3.5).
    /// </summary>
    public ulong? PeerStopSendingErrorCode { get; internal set; }

    /// <summary>
    /// Send urgency per RFC 9218 §4.1: 0 (highest) … 7 (background), default 3.
    /// The send scheduler serves streams in ascending urgency; the application layer (HTTP/3)
    /// sets the value from the `priority` header or PRIORITY_UPDATE frames.
    /// </summary>
    public int SendUrgency { get; set; } = 3;

    /// <summary>
    /// Incremental per RFC 9218 §4.2: <c>true</c> ⇒ equally urgent incremental streams share the
    /// bandwidth (round-robin); <c>false</c> (default) ⇒ one after another in stream-ID order.
    /// </summary>
    public bool SendIncremental { get; set; }
}
