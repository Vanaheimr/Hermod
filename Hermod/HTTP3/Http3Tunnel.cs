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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// A bidirectional byte tunnel over an Extended-CONNECT stream (RFC 9114 §4.4, RFC 8441/9220):
/// payload travels in DATA frames of the request stream; a FIN corresponds to the orderly TCP
/// close, a reset to the RST (H3_REQUEST_CANCELLED, RFC 9220 §3). Implements the transport-agnostic
/// <see cref="IHTTP2Tunnel"/> interface, so the RFC 6455 <see cref="WebSocketConnection"/>
/// runs over it unchanged.
///
/// <para>
/// <b>Concurrency — receive side:</b> guarded, like <see cref="Http3RequestBody"/>. A consumer that
/// awaits real I/O between two reads resumes on a thread-pool thread and then reads concurrently
/// with the pump delivering into this tunnel, so the queue, the pending read and the datagram buffer
/// are all under a lock. A waiting read is always completed OUTSIDE that lock: its continuation runs
/// inline on the completing thread and may read again immediately, which would otherwise re-enter
/// mid-operation.
/// </para>
/// <para>
/// <b>Concurrency — send side:</b> marshalled, not locked. <see cref="WriteAsync"/>,
/// <see cref="Complete"/> and <see cref="Abort"/> only append to an outbound queue; the pump drains
/// it on its own thread via <see cref="PumpOutbound"/>. A lock would not do here: the race is with
/// the pump's use of the QUIC stream's send buffer, not between two tunnel callers, and that buffer
/// has no synchronisation of its own. The drain runs after the pump has processed incoming streams,
/// so an answer written by a continuation running inline still leaves on the same pass.
/// </para>
/// <para>
/// The one exception is <see cref="TrySendDatagram"/>, which stays pump-affine: it answers
/// synchronously whether the datagram fit into a packet, and that answer cannot survive being
/// deferred.
/// </para>
/// </summary>
public sealed class Http3Tunnel : IHTTP2Tunnel
{
    private readonly QuicStream _stream;
    private readonly Lock _lock = new();
    private readonly Queue<byte[]> _received = new();
    private TaskCompletionSource<byte[]?>? _pendingRead;
    private bool _ended;

    internal Http3Tunnel(QuicStream stream)
        => _stream = stream;

    /// <summary>
    /// Reads the next chunk tunnelled from the peer; <c>null</c> once the peer has ended its side
    /// (FIN or reset).
    /// </summary>
    public Task<byte[]?> ReadAsync(CancellationToken CancellationToken)
    {
        if (CancellationToken.IsCancellationRequested)
            return Task.FromCanceled<byte[]?>(CancellationToken);

        lock (_lock)
        {
            if (_received.Count > 0)
                return Task.FromResult<byte[]?>(_received.Dequeue());
            if (_ended)
                return Task.FromResult<byte[]?>(null);
            if (_pendingRead is not null)
                throw new InvalidOperationException("Only one read at a time is supported.");

            // Nothing there yet — the pump completes this task as soon as a DATA frame arrives.
            _pendingRead = new TaskCompletionSource<byte[]?>();
            return _pendingRead.Task;
        }
    }

    /// <summary>
    /// Sends a chunk to the peer — as a DATA frame on the CONNECT stream (RFC 9114 §4.4). Queued for
    /// the pump; the DATA frame is built here, so <paramref name="Data"/> may be reused on return.
    /// </summary>
    public Task WriteAsync(byte[] Data, CancellationToken CancellationToken)
    {
        if (CancellationToken.IsCancellationRequested)
            return Task.FromCanceled(CancellationToken);

        byte[] frame = Http3Frames.Build(Http3FrameType.Data, Data);
        lock (_lock)
            _outbound.Enqueue((OutboundKind.Data, frame));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ends our own send direction in an orderly fashion (FIN ≙ TCP close, RFC 9220 §3). Queued
    /// behind everything already written, so the FIN cannot overtake it.
    /// </summary>
    public void Complete()
    {
        lock (_lock)
            _outbound.Enqueue((OutboundKind.Finish, null));
    }

    /// <summary>
    /// Aborts the tunnel abruptly (≙ TCP RST): RESET_STREAM/STOP_SENDING with H3_REQUEST_CANCELLED.
    /// </summary>
    public void Abort()
    {
        lock (_lock)
        {
            // Queued but unsent data is dropped — that is what distinguishes a reset from a close.
            _outbound.Clear();
            _outbound.Enqueue((OutboundKind.Abort, null));
        }
    }

    // ---- Outbound queue --------------------------------------------------------------------------

    private enum OutboundKind { Data, Finish, Abort }

    private readonly Queue<(OutboundKind Kind, byte[]? Frame)> _outbound = new();

    /// <summary>
    /// Called by the pump: puts everything the consumer has queued onto the QUIC stream. Runs after
    /// incoming streams have been processed, so an answer written by an inline continuation of a
    /// read still goes out on the same pass.
    /// </summary>
    internal void PumpOutbound()
    {
        while (true)
        {
            (OutboundKind Kind, byte[]? Frame) item;
            lock (_lock)
            {
                if (_outbound.Count == 0)
                    return;
                item = _outbound.Dequeue();
            }

            // Outside the lock: these reach into the QUIC stream, which is the pump's own territory
            // and must never be entered while holding a lock a consumer thread can be waiting on.
            switch (item.Kind)
            {
                case OutboundKind.Data:
                    _stream.Write(item.Frame!);
                    break;

                case OutboundKind.Finish:
                    _stream.Finish();
                    break;

                case OutboundKind.Abort:
                    _stream.Reset(Http3Error.RequestCancelled);
                    _stream.AbortRead(Http3Error.RequestCancelled);
                    break;
            }
        }
    }

    /// <summary>
    /// Called by the pump: delivers the payload of a received DATA frame into the tunnel.
    /// </summary>
    internal void Deliver(byte[] chunk)
    {
        TaskCompletionSource<byte[]?>? toComplete = null;
        lock (_lock)
        {
            if (_ended)
                return; // the peer already finished — anything after that is not ours to hand on
            if (_pendingRead is { } pending)
            {
                _pendingRead = null;
                toComplete = pending;
            }
            else
                _received.Enqueue(chunk);
        }
        toComplete?.TrySetResult(chunk); // outside the lock — the continuation may read again at once
    }

    /// <summary>
    /// Called by the pump: the peer side has ended (FIN or reset) — outstanding and future reads
    /// return <c>null</c> once the queue has drained.
    /// </summary>
    internal void End()
    {
        TaskCompletionSource<byte[]?>? toComplete;
        lock (_lock)
        {
            if (_ended)
                return;
            _ended = true;
            toComplete = _pendingRead;
            _pendingRead = null;
        }
        toComplete?.TrySetResult(null); // outside the lock, for the same reason as in Deliver
    }

    // ---- HTTP datagrams (RFC 9297) — unreliable messages alongside the byte stream ----------

    private const int MaxBufferedDatagrams = 64; // unreliable ⇒ overflow MAY be discarded (RFC 9221 §5.3)
    private readonly Queue<byte[]> _datagrams = new();
    internal Func<byte[], bool>? DatagramSender { get; set; }

    /// <summary>
    /// Sends an HTTP datagram for this request stream (RFC 9297 §2.1: quarter stream ID + payload in
    /// a QUIC DATAGRAM frame). <c>false</c> when datagrams are not negotiated
    /// (SETTINGS_H3_DATAGRAM/max_datagram_frame_size) or the datagram does not fit into a packet.
    /// </summary>
    public bool TrySendDatagram(byte[] payload) => DatagramSender?.Invoke(payload) ?? false;

    /// <summary>
    /// Fetches the next HTTP datagram received for this stream, if any.
    /// </summary>
    public bool TryReceiveDatagram(out byte[]? payload)
    {
        lock (_lock)
        {
            if (_datagrams.Count > 0)
            {
                payload = _datagrams.Dequeue();
                return true;
            }
            payload = null;
            return false;
        }
    }

    /// <summary>
    /// Called by the pump: delivers a received HTTP datagram (when the buffer is full, the oldest
    /// one is discarded — datagrams are unreliable by definition).
    /// </summary>
    internal void DeliverDatagram(byte[] payload)
    {
        lock (_lock)
        {
            if (_datagrams.Count >= MaxBufferedDatagrams)
                _datagrams.Dequeue();
            _datagrams.Enqueue(payload);
        }
    }
}

/// <summary>
/// A server's answer to an Extended CONNECT (RFC 8441/9220): status code, additional headers
/// (e.g. <c>sec-websocket-protocol</c>) and — on acceptance (2xx) — a callback receiving the
/// finished <see cref="Http3Tunnel"/> (analogous to Hermod's <c>HTTP2ConnectResult.RunAsync</c>).
/// </summary>
public sealed class Http3ConnectResult
{
    public required int Status { get; init; }
    public IReadOnlyList<HeaderField> Headers { get; init; } = [];

    /// <summary>
    /// Called with the tunnel on 2xx, as soon as the response HEADERS are sent.
    /// </summary>
    public Action<Http3Tunnel>? OnTunnel { get; init; }
}
