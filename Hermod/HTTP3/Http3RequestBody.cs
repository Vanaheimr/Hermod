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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// The request body as a readable stream (RFC 9114 §4.1 item 2). Handed to a streaming handler
/// straight after the header section, so an upload can be processed while it is still arriving
/// instead of being buffered in full.
/// <para>
/// <b>Concurrency:</b> unlike <see cref="Http3Tunnel"/> this reader IS thread-safe — and it has to
/// be. A handler that awaits anything else (a database, a file, another request) resumes on a
/// thread-pool thread and reads from there, concurrently with the pump filling this buffer. The
/// shared state is therefore guarded, and a waiting read is always completed OUTSIDE the lock so
/// that a continuation which immediately reads again cannot deadlock.
/// </para>
/// <para>
/// Backpressure comes from QUIC: while more than <see cref="HighWatermark"/> bytes sit here unread,
/// the connection stops taking data off the stream, the flow-control window closes and the peer
/// stops sending. A handler that reads slowly therefore throttles the upload instead of filling
/// memory.
/// </para>
/// </summary>
public sealed class Http3RequestBody : Stream
{
    /// <summary>
    /// Unread bytes above which the connection stops reading from the QUIC stream.
    /// </summary>
    public const int HighWatermark = 64 * 1024;

    private readonly Lock _lock = new();
    private readonly Queue<byte[]> _chunks = new();
    private byte[]? _current;
    private int _currentOffset;
    private int _buffered;
    private bool _ended;
    private Exception? _failure;

    private TaskCompletionSource<int>? _pendingRead;
    private Memory<byte> _pendingBuffer;

    /// <summary>
    /// Unread bytes currently buffered — the connection's backpressure signal.
    /// </summary>
    internal int Buffered { get { lock (_lock) return _buffered; } }

    /// <summary>
    /// <c>true</c> while the reader is above the watermark: the connection then leaves the data on
    /// the QUIC stream, which closes the flow-control window.
    /// </summary>
    internal bool IsSaturated { get { lock (_lock) return _buffered >= HighWatermark; } }

    /// <summary>
    /// Total number of body bytes handed over so far — the basis of the content-length check
    /// (RFC 9114 §4.1.2).
    /// </summary>
    internal ulong TotalReceived { get { lock (_lock) return _totalReceived; } }

    private ulong _totalReceived;

    // ---- Producer side (pump) ------------------------------------------------------------------

    /// <summary>
    /// Delivers body bytes and hands them straight to a waiting reader where possible.
    /// </summary>
    internal void Deliver(ReadOnlySpan<byte> data)
    {
        TaskCompletionSource<int>? toComplete = null;
        int served = 0;

        lock (_lock)
        {
            if (data.IsEmpty || _ended)
                return;
            _totalReceived += (ulong)data.Length;

            if (_pendingRead is { } pending && _current is null && _chunks.Count == 0)
            {
                // A reader is waiting: serve it directly and buffer only the remainder.
                served = Math.Min(_pendingBuffer.Length, data.Length);
                data[..served].CopyTo(_pendingBuffer.Span);
                if (served < data.Length)
                    Enqueue(data[served..]);
                _pendingRead = null;
                _pendingBuffer = default;
                toComplete = pending;
            }
            else
                Enqueue(data);
        }

        toComplete?.TrySetResult(served); // outside the lock — the continuation may read again at once
    }

    /// <summary>
    /// The peer has ended the body (FIN); a waiting reader gets 0 = end of stream.
    /// </summary>
    internal void Complete()
    {
        TaskCompletionSource<int>? toComplete;
        lock (_lock)
        {
            if (_ended)
                return;
            _ended = true;
            toComplete = _pendingRead;
            _pendingRead = null;
            _pendingBuffer = default;
        }
        toComplete?.TrySetResult(0);
    }

    /// <summary>
    /// The request was aborted (RESET_STREAM, malformed, connection error); a waiting reader sees
    /// the error instead of hanging.
    /// </summary>
    internal void Fail(Exception error)
    {
        TaskCompletionSource<int>? toComplete;
        lock (_lock)
        {
            if (_ended)
                return;
            _ended = true;
            _failure = error;
            toComplete = _pendingRead;
            _pendingRead = null;
            _pendingBuffer = default;
        }
        toComplete?.TrySetException(error);
    }

    private void Enqueue(ReadOnlySpan<byte> data)
    {
        _chunks.Enqueue(data.ToArray());
        _buffered += data.Length;
    }

    // ---- Consumer side (handler) ---------------------------------------------------------------

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<int>(cancellationToken);

        lock (_lock)
        {
            int copied = TakeBuffered(buffer.Span);
            if (copied > 0)
                return ValueTask.FromResult(copied);
            if (_failure is { } error)
                return ValueTask.FromException<int>(error);
            if (_ended)
                return ValueTask.FromResult(0);
            if (_pendingRead is not null)
                throw new InvalidOperationException("Only one read at a time is supported.");

            // Nothing there yet — the pump completes this task as soon as body bytes arrive.
            _pendingRead = new TaskCompletionSource<int>();
            _pendingBuffer = buffer;
            return new ValueTask<int>(_pendingRead.Task);
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int copied;
        Exception? failure;
        bool ended;
        lock (_lock)
        {
            copied = TakeBuffered(buffer.AsSpan(offset, count));
            failure = _failure;
            ended = _ended;
        }
        if (copied > 0)
            return copied;
        if (failure is { } error)
            throw error;
        if (ended)
            return 0;
        // Blocking here would stall the very pump that is supposed to supply the data.
        throw new InvalidOperationException("The request body is only available asynchronously — use ReadAsync.");
    }

    /// <summary>
    /// Copies buffered bytes into <paramref name="destination"/>. Caller holds the lock.
    /// </summary>
    private int TakeBuffered(Span<byte> destination)
    {
        if (destination.IsEmpty)
            return 0;
        if (_current is null && _chunks.Count > 0)
        {
            _current = _chunks.Dequeue();
            _currentOffset = 0;
        }
        if (_current is null)
            return 0;

        int available = _current.Length - _currentOffset;
        int copied = Math.Min(available, destination.Length);
        _current.AsSpan(_currentOffset, copied).CopyTo(destination);
        _currentOffset += copied;
        _buffered -= copied;
        if (_currentOffset >= _current.Length)
            _current = null;
        return copied;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
