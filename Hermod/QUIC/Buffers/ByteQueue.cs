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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

/// <summary>
/// Byte queue for the zero-alloc path (phase 9): append at the back, consume at the front —
/// both amortised O(1) and without allocation in the steady state. Replaces the former
/// <c>List&lt;byte&gt;</c> buffers, whose <c>RemoveRange(0, n)</c> shifted ALL remaining bytes on
/// every consume (O(n²) over a transfer) and whose read-out (<c>ToArray</c>) copied the entire
/// content per pump pass. The backing store grows on demand (doubling) and is reused;
/// compaction (moving the remaining bytes to the front) only happens when there is no more
/// room at the back — amortised O(1) per byte.
/// </summary>
public sealed class ByteQueue
{
    private byte[] _buffer = [];
    private int _head; // index of the first unread byte
    private int _tail; // index of the first free position

    /// <summary>
    /// Number of buffered (not yet consumed) bytes.
    /// </summary>
    public int Count => _tail - _head;

    /// <summary>
    /// The buffered bytes as a span — valid until the next <see cref="Append"/>/<see cref="Consume"/>.
    /// </summary>
    public ReadOnlySpan<byte> Span => _buffer.AsSpan(_head, _tail - _head);

    /// <summary>
    /// The buffered bytes as memory (for parsers with a <c>ReadOnlyMemory</c> signature) — valid
    /// until the next mutation.
    /// </summary>
    public ReadOnlyMemory<byte> Memory => _buffer.AsMemory(_head, _tail - _head);

    /// <summary>
    /// Appends bytes at the back.
    /// </summary>
    public void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;
        EnsureSpace(data.Length);
        data.CopyTo(_buffer.AsSpan(_tail));
        _tail += data.Length;
    }

    /// <summary>
    /// Consumes the frontmost <paramref name="count"/> bytes (only advances the read index).
    /// </summary>
    public void Consume(int count)
    {
        if (count < 0 || count > Count)
            throw new ArgumentOutOfRangeException(nameof(count));
        _head += count;
        if (_head == _tail)
            _head = _tail = 0; // empty ⇒ reset the indices (best case: never compact)
    }

    /// <summary>
    /// Discards the entire content (the backing store is kept for reuse).
    /// </summary>
    public void Clear() => _head = _tail = 0;

    /// <summary>
    /// Copies the content into a fresh array (only for hand-offs that must take ownership).
    /// </summary>
    public byte[] ToArray() => Span.ToArray();

    private void EnsureSpace(int needed)
    {
        if (_tail + needed <= _buffer.Length)
            return;
        int count = Count;
        if (count + needed <= _buffer.Length)
        {
            // Back is full, but there is free room at the front ⇒ compact instead of growing
            // (Buffer.BlockCopy is overlap-safe, like memmove).
            Buffer.BlockCopy(_buffer, _head, _buffer, 0, count);
        }
        else
        {
            int capacity = Math.Max(256, _buffer.Length);
            while (capacity < count + needed)
                capacity *= 2;
            byte[] grown = new byte[capacity];
            Buffer.BlockCopy(_buffer, _head, grown, 0, count);
            _buffer = grown;
        }
        _head = 0;
        _tail = count;
    }
}
