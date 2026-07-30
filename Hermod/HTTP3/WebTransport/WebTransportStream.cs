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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;

/// <summary>
/// A WebTransport data stream (draft-webtrans-http3 §4.1/§4.2): a native QUIC stream whose header
/// (uni type 0x54 or WT_STREAM signal 0x41, each followed by the session ID) has already been
/// stripped — <see cref="Read"/>/<see cref="Write"/> operate on the pure payload.
/// Reset/StopSending map 32-bit app error codes into the WT_APPLICATION_ERROR range (§4.3).
/// </summary>
public sealed class WebTransportStream
{
    private readonly QuicStream _stream;
    private readonly bool _canSend;    // send direction present (bidi or locally opened uni)
    private readonly bool _canReceive; // receive direction present (bidi or incoming uni)
    private byte[] _leftover; // payload read together with the header, returned first

    internal WebTransportStream(QuicStream stream, bool bidirectional, bool canSend, bool canReceive, byte[]? leftover = null)
    {
        _stream = stream;
        IsBidirectional = bidirectional;
        _canSend = canSend;
        _canReceive = canReceive;
        _leftover = leftover ?? [];
    }

    /// <summary>
    /// <c>true</c> = bidirectional WebTransport stream, <c>false</c> = unidirectional.
    /// </summary>
    public bool IsBidirectional { get; }

    /// <summary>
    /// The underlying QUIC stream ID.
    /// </summary>
    public ulong StreamId => _stream.Id.Value;

    /// <summary>
    /// Reads the next contiguous payload section (header already stripped).
    /// </summary>
    public byte[] Read()
    {
        byte[] fresh = _stream.Read();
        if (_leftover.Length == 0)
            return fresh;
        byte[] combined = [.. _leftover, .. fresh]; // prepend the header remainder once
        _leftover = [];
        return combined;
    }

    /// <summary>
    /// Writes payload (only meaningful on bidi or locally initiated uni streams).
    /// </summary>
    public void Write(ReadOnlySpan<byte> data) => _stream.Write(data);

    /// <summary>
    /// Ends the send direction (FIN).
    /// </summary>
    public void Finish() => _stream.Finish();

    /// <summary>
    /// The peer has ended its send direction (FIN) and everything has been read.
    /// </summary>
    public bool IsReceiveComplete => _stream.IsReceiveComplete;

    /// <summary>
    /// Aborts the send direction (RESET_STREAM); <paramref name="applicationErrorCode"/> is a
    /// 32-bit WebTransport error and is mapped into the WT_APPLICATION_ERROR range (§4.3).
    /// </summary>
    public void Reset(uint applicationErrorCode)
    {
        if (_canSend)
            _stream.Reset(WebTransportConstants.ApplicationErrorToHttp(applicationErrorCode));
    }

    /// <summary>
    /// Aborts reading (STOP_SENDING), with the code mapped into the WT_APPLICATION_ERROR range.
    /// </summary>
    public void StopSending(uint applicationErrorCode)
    {
        if (_canReceive)
            _stream.AbortRead(WebTransportConstants.ApplicationErrorToHttp(applicationErrorCode));
    }

    /// <summary>
    /// The peer reset this stream side (RESET_STREAM).
    /// </summary>
    public bool IsResetByPeer => _stream.IsResetByPeer;

    /// <summary>
    /// The (back-computed) WebTransport application error code of a peer reset, when in the
    /// WT_APPLICATION_ERROR range; otherwise <c>null</c>.
    /// </summary>
    public uint? PeerResetErrorCode
        => _stream.PeerResetErrorCode is { } http ? WebTransportConstants.HttpToApplicationError(http) : null;

    /// <summary>
    /// Called by the session management when the session ends (§6): abort both directions with
    /// WT_SESSION_GONE.
    /// </summary>
    internal void AbortForSessionGone()
    {
        // Abort only the direction(s) actually present — otherwise STREAM_STATE_ERROR
        // (STOP_SENDING on a send-only uni or RESET_STREAM on a receive-only uni).
        if (_canSend)
            _stream.Reset(WebTransportConstants.SessionGone);
        if (_canReceive)
            _stream.AbortRead(WebTransportConstants.SessionGone);
    }

    internal QuicStream Underlying => _stream;
}
