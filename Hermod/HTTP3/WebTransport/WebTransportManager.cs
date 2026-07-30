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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;

/// <summary>
/// Routes WebTransport streams, datagrams and capsules between the HTTP/3 connection and the
/// <see cref="WebTransportSession"/>s (draft-ietf-webtrans-http3-13). Peer-initiated uni streams
/// with type 0x54 (§4.1) and bidi streams with the WT_STREAM signal 0x41 (§4.2) are matched by
/// their session ID; sessions still unknown are buffered to a limit (§4.5).
/// </summary>
internal sealed class WebTransportManager
{
    private const int MaxBufferedStreams = 16; // §4.5: limit the buffer ⇒ WT_BUFFERED_STREAM_REJECTED

    private readonly bool _weAreClient;
    private readonly Dictionary<ulong, WebTransportSession> _sessions = [];   // session ID (CONNECT stream) → session
    private readonly List<BufferedStream> _bufferedForUnknownSession = [];    // §4.5: waiting for their session

    public WebTransportManager(bool weAreClient) => _weAreClient = weAreClient;

    public void RegisterSession(WebTransportSession session)
    {
        _sessions[session.SessionId] = session;
        DrainBufferedStreams(); // §4.5: match early-arrived streams now
    }

    public bool TryGetSession(ulong sessionId, out WebTransportSession? session) => _sessions.TryGetValue(sessionId, out session);

    /// <summary>
    /// Matches a recognised WebTransport data stream to its session (§4.1/§4.2). The caller has
    /// already parsed the header (uni type 0x54 or WT_STREAM 0x41 ‖ session ID) and hands over the
    /// payload already read along as <paramref name="leftover"/>. When the session is not yet there,
    /// the stream is buffered to a limit (§4.5) or, on overflow, discarded with WT_BUFFERED_STREAM_REJECTED.
    /// </summary>
    public void ClaimStream(QuicStream stream, ulong sessionId, byte[] leftover, bool bidirectional)
    {
        if (_sessions.TryGetValue(sessionId, out WebTransportSession? session))
        {
            Deliver(session, stream, bidirectional, leftover);
            return;
        }
        if (_bufferedForUnknownSession.Count >= MaxBufferedStreams)
        {
            stream.Reset(WebTransportConstants.BufferedStreamRejected);
            stream.AbortRead(WebTransportConstants.BufferedStreamRejected);
            return;
        }
        _bufferedForUnknownSession.Add(new BufferedStream(stream, sessionId, leftover, bidirectional));
    }

    private void DrainBufferedStreams()
    {
        for (int i = _bufferedForUnknownSession.Count - 1; i >= 0; i--)
        {
            BufferedStream buffered = _bufferedForUnknownSession[i];
            if (!_sessions.TryGetValue(buffered.SessionId, out WebTransportSession? session))
                continue;
            _bufferedForUnknownSession.RemoveAt(i);
            Deliver(session, buffered.Stream, buffered.Bidirectional, buffered.Leftover);
        }
    }

    private static void Deliver(WebTransportSession session, QuicStream stream, bool bidi, byte[] leftover)
    {
        if (bidi)
            session.OnIncomingBidiStream(stream, leftover);
        else
            session.OnIncomingUniStream(stream, leftover);
    }

    /// <summary>
    /// Delivers a WebTransport datagram to its session (the quarter stream ID addresses the
    /// CONNECT stream, §4.4). <c>true</c> when it belonged to a known session.
    /// </summary>
    public bool TryDeliverDatagram(ulong sessionId, byte[] payload)
    {
        if (!_sessions.TryGetValue(sessionId, out WebTransportSession? session))
            return false;
        session.OnDatagram(payload);
        return true;
    }

    private sealed record BufferedStream(QuicStream Stream, ulong SessionId, byte[] Leftover, bool Bidirectional);
}
