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
/// <b>Backpressure</b>, both ways, at <see cref="HighWatermark"/> bytes, as for
/// <see cref="Http3RequestBody"/>. While that much sits here unread, the connection leaves what
/// arrives on the QUIC stream: its flow-control window stays shut and the peer stops sending. And
/// while that much waits to go out — queued here or on the stream, unsent because the peer gives no
/// credit or the path no room — <see cref="WriteAsync"/> waits. Until 2026-10 both queues were
/// unbounded: a consumer that did not read, or a peer that did not, let a tunnel grow in memory for
/// as long as the other side kept sending, and every write completed at once.
/// </para>
/// <para>
/// The one exception is <see cref="TrySendDatagram"/>, which stays pump-affine: it answers
/// synchronously whether the datagram fit into a packet, and that answer cannot survive being
/// deferred.
/// </para>
/// </summary>
public sealed class Http3Tunnel : IHTTP2Tunnel
{
    /// <summary>
    /// Bytes above which the tunnel pushes back: received bytes not read yet, above which the
    /// connection stops taking data off the QUIC stream, and bytes not sent yet, above which
    /// <see cref="WriteAsync"/> waits. The same as <see cref="Http3RequestBody.HighWatermark"/>.
    /// </summary>
    public const int HighWatermark = 64 * 1024;

    private readonly QuicStream _stream;
    private readonly Lock _lock = new();
    private readonly Queue<byte[]> _received = new();
    private int _receivedBytes;
    private TaskCompletionSource<byte[]?>? _pendingRead;
    private bool _ended;

    internal Http3Tunnel(QuicStream stream)
        => _stream = stream;

    /// <summary>
    /// Unread bytes received — the connection's signal to stop reading the QUIC stream.
    /// </summary>
    internal int Buffered { get { lock (_lock) return _receivedBytes; } }

    /// <summary>
    /// <c>true</c> while <see cref="HighWatermark"/> bytes or more sit here unread: the connection
    /// then leaves the data on the QUIC stream, which keeps its flow-control window shut.
    /// </summary>
    internal bool IsSaturated { get { lock (_lock) return _receivedBytes >= HighWatermark; } }

    /// <summary>
    /// Reads the next chunk tunnelled from the peer; <c>null</c> once the peer has ended its side
    /// (FIN or reset), or the connection has ended — closed by either side or timed out. Reading is
    /// what lets the peer send on: past <see cref="HighWatermark"/> unread bytes it is held back.
    /// </summary>
    public Task<byte[]?> ReadAsync(CancellationToken CancellationToken)
    {
        if (CancellationToken.IsCancellationRequested)
            return Task.FromCanceled<byte[]?>(CancellationToken);

        lock (_lock)
        {
            if (_received.TryDequeue(out byte[]? chunk))
            {
                _receivedBytes -= chunk.Length;
                return Task.FromResult<byte[]?>(chunk);
            }
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
    ///
    /// Completes once the frame is queued, which is at once while fewer than
    /// <see cref="HighWatermark"/> bytes wait to go out, and otherwise when the pump has sent enough
    /// of them. Writes that wait keep their order, the order of their calls, awaited or not; one whose
    /// token is cancelled while it waits is never sent. Fails with an
    /// <see cref="OperationCanceledException"/>, waiting or not, once the tunnel can send no more: the
    /// peer reset it, the connection ended, or it was aborted. A FIN from the peer leaves it open.
    /// </summary>
    public Task WriteAsync(byte[] Data, CancellationToken CancellationToken)
    {
        if (CancellationToken.IsCancellationRequested)
            return Task.FromCanceled(CancellationToken);

        byte[] frame = Http3Frames.Build(Http3FrameType.Data, Data);
        Waiter waiter;

        lock (_lock)
        {
            if (_sendingEnded is { } why)
                return Task.FromException(new OperationCanceledException(why));

            // Room, and nobody waiting ahead of us, whom we must not overtake.
            if (_waiting.Count == 0 && _queuedBytes + _streamPending < HighWatermark)
            {
                Enqueue(frame);
                return Task.CompletedTask;
            }

            waiter = new Waiter(frame);
            waiter.Node = _waiting.AddLast(waiter);
        }

        // Outside the lock: a token cancelled meanwhile runs the callback right here.
        if (CancellationToken.CanBeCanceled)
            waiter.Registration = CancellationToken.Register(() => Withdraw(waiter, CancellationToken));

        return waiter.Done.Task;
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
    /// Writes waiting, and writes after this, fail.
    /// </summary>
    public void Abort()
    {
        lock (_lock)
        {
            // Queued but unsent data is dropped — that is what distinguishes a reset from a close.
            _outbound.Clear();
            _queuedBytes = 0;
            _outbound.Enqueue((OutboundKind.Abort, null));
        }
        EndSending("The tunnel was aborted.");
    }

    // ---- Outbound queue --------------------------------------------------------------------------

    private enum OutboundKind { Data, Finish, Abort }

    private readonly Queue<(OutboundKind Kind, byte[]? Frame)> _outbound = new();

    /// <summary>
    /// Bytes of the DATA frames in <see cref="_outbound"/>, not on the stream yet.
    /// </summary>
    private int _queuedBytes;

    /// <summary>
    /// Bytes on the stream not sent yet, as the pump last saw them — the stream's send buffer is the
    /// pump's, so a writer reads this instead.
    /// </summary>
    private int _streamPending;

    /// <summary>
    /// Writes waiting for room, in the order of their calls.
    /// </summary>
    private readonly LinkedList<Waiter> _waiting = new();

    /// <summary>
    /// Why nothing more can be sent, once that is so.
    /// </summary>
    private string? _sendingEnded;

    private sealed class Waiter(byte[] frame)
    {
        public byte[] Frame { get; } = frame;
        public TaskCompletionSource Done { get; } = new();
        public LinkedListNode<Waiter>? Node { get; set; }
        public CancellationTokenRegistration Registration { get; set; }
    }

    /// <summary>
    /// Queue a DATA frame for the pump. Under the lock.
    /// </summary>
    private void Enqueue(byte[] frame)
    {
        _outbound.Enqueue((OutboundKind.Data, frame));
        _queuedBytes += frame.Length;
    }

    /// <summary>
    /// A waiting write whose token was cancelled: out of the line, if it is still in it.
    /// </summary>
    private void Withdraw(Waiter waiter, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (waiter.Node?.List is null)
                return; // admitted or failed already
            _waiting.Remove(waiter.Node);
        }
        waiter.Done.TrySetCanceled(cancellationToken);
    }

    /// <summary>
    /// Nothing more can be sent: writes waiting fail, and so do writes after this.
    /// </summary>
    private void EndSending(string why)
    {
        List<Waiter> failed;
        lock (_lock)
        {
            _sendingEnded ??= why;
            failed = [.. _waiting];
            _waiting.Clear();
        }
        foreach (Waiter waiter in failed)
        {
            waiter.Registration.Dispose();
            waiter.Done.TrySetException(new OperationCanceledException(why));
        }
    }

    /// <summary>
    /// Called by the pump: puts everything the consumer has queued onto the QUIC stream, and lets
    /// waiting writes in as far as what the stream has sent makes room. Runs after incoming streams
    /// have been processed, so an answer written by an inline continuation of a read still goes out
    /// on the same pass — and so does a write let in here.
    /// </summary>
    internal void PumpOutbound()
    {
        while (true)
        {
            while (true)
            {
                (OutboundKind Kind, byte[]? Frame) item;
                lock (_lock)
                {
                    if (!_outbound.TryDequeue(out item))
                        break;
                    if (item.Kind == OutboundKind.Data)
                        _queuedBytes -= item.Frame!.Length;
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

            int streamPending = _stream.Send.PendingBytes;
            List<Waiter>? admitted = null;

            lock (_lock)
            {
                _streamPending = streamPending;

                while (_waiting.First is { } first && _queuedBytes + _streamPending < HighWatermark)
                {
                    _waiting.RemoveFirst();
                    Enqueue(first.Value.Frame);
                    (admitted ??= []).Add(first.Value);
                }
            }

            if (admitted is null)
                return;

            // Outside the lock, as for a read: a continuation may write again at once. The frames let
            // in go onto the stream in the next round of this loop.
            foreach (Waiter waiter in admitted)
            {
                waiter.Registration.Dispose();
                waiter.Done.TrySetResult();
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
            {
                _received.Enqueue(chunk);
                _receivedBytes += chunk.Length;
            }
        }
        toComplete?.TrySetResult(chunk); // outside the lock — the continuation may read again at once
    }

    /// <summary>
    /// Called by the connection: the peer has ended its side with a FIN — outstanding and future
    /// reads return <c>null</c> once the queue has drained. Our side stays open (RFC 9220 §3: a
    /// FIN is the orderly TCP close of one direction).
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

    /// <summary>
    /// Called by the connection: the peer reset the stream (≙ TCP RST, RFC 9220 §3), or the
    /// connection has ended — closed by either side or timed out. Reads end as after a FIN, and
    /// writes, waiting or not, fail: nothing will make room for them, or take them, any more.
    /// </summary>
    internal void EndAbruptly()
    {
        End();
        EndSending("The tunnel was reset, or its connection ended.");
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
