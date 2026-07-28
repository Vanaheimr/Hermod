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

using System.Buffers;
using System.Buffers.Binary;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

/// <summary>
/// Growing write buffer (big-endian) for assembling outgoing QUIC packets.
/// <para>
/// The backing store is rented from the <see cref="ArrayPool{Byte}"/> and doubled on demand.
/// <see cref="Dispose"/> returns it – therefore always use with <c>using</c>.
/// Designed as a <c>struct</c>; pass by <c>ref</c> to avoid copies (and double dispose).
/// </para>
/// </summary>
public struct BufferWriter : IDisposable
{
    private const int DefaultCapacity = 1500;

    private byte[]? _buffer;
    private int _written;
    private bool _disposed;

    public BufferWriter(int initialCapacity = DefaultCapacity)
    {
        if (initialCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _written = 0;
        _disposed = false;
    }

    /// <summary>
    /// Number of bytes written so far.
    /// </summary>
    public readonly int Length => _written;

    /// <summary>
    /// The bytes written so far as a slice (valid until the next write/dispose operation).
    /// </summary>
    public readonly ReadOnlySpan<byte> WrittenSpan
        => _buffer is null ? ReadOnlySpan<byte>.Empty : _buffer.AsSpan(0, _written);

    public void WriteByte(byte value)
    {
        byte[] buf = EnsureCapacity(1);
        buf[_written] = value;
        _written += 1;
    }

    public void WriteUInt16(ushort value)
    {
        byte[] buf = EnsureCapacity(2);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(_written), value);
        _written += 2;
    }

    public void WriteUInt32(uint value)
    {
        byte[] buf = EnsureCapacity(4);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(_written), value);
        _written += 4;
    }

    public void WriteUInt64(ulong value)
    {
        byte[] buf = EnsureCapacity(8);
        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(_written), value);
        _written += 8;
    }

    /// <summary>
    /// Writes a QUIC VarInt (RFC 9000 §16).
    /// </summary>
    public void WriteVarInt(ulong value)
    {
        byte[] buf = EnsureCapacity(VarInt.GetLength(value));
        _written += VarInt.Write(buf.AsSpan(_written), value);
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
            return;
        byte[] buf = EnsureCapacity(value.Length);
        value.CopyTo(buf.AsSpan(_written));
        _written += value.Length;
    }

    /// <summary>
    /// Writes <paramref name="count"/> bytes with the value <paramref name="value"/> (e.g. PADDING).
    /// </summary>
    public void WriteRepeated(byte value, int count)
    {
        if (count <= 0)
            return;
        byte[] buf = EnsureCapacity(count);
        buf.AsSpan(_written, count).Fill(value);
        _written += count;
    }

    /// <summary>
    /// Reserves <paramref name="count"/> bytes and returns a writable span onto them.
    /// Useful e.g. for filling in a length afterwards. The span is only valid until the next
    /// write operation (possible reallocation).
    /// </summary>
    public Span<byte> GetSpan(int count)
    {
        byte[] buf = EnsureCapacity(count);
        Span<byte> span = buf.AsSpan(_written, count);
        _written += count;
        return span;
    }

    /// <summary>
    /// Returns a writable span onto already-written bytes for patching afterwards
    /// (e.g. filling in a length field whose value is only known after writing the content).
    /// Only valid immediately before the next write operation.
    /// </summary>
    public readonly Span<byte> PatchSpan(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > _written)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return _buffer!.AsSpan(offset, count);
    }

    /// <summary>
    /// Discards everything beyond <paramref name="length"/> bytes — the buffer itself is retained.
    /// Used to undo a speculative write: write, measure, and on overshoot rewind to the previous
    /// length (e.g. when filling a packet up to its size budget).
    /// </summary>
    public void Truncate(int length)
    {
        if (length < 0 || length > _written)
            throw new ArgumentOutOfRangeException(nameof(length));
        _written = length;
    }

    /// <summary>
    /// Ensures space for <paramref name="additional"/> more bytes and returns the backing store.
    /// </summary>
    private byte[] EnsureCapacity(int additional)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(BufferWriter));

        // Lazy init: a writer created via 'new BufferWriter()' (implicit struct default ctor)
        // has no rented buffer yet.
        _buffer ??= ArrayPool<byte>.Shared.Rent(Math.Max(DefaultCapacity, additional));

        int required = _written + additional;
        if (required <= _buffer.Length)
            return _buffer;

        int newCapacity = _buffer.Length * 2;
        while (newCapacity < required)
            newCapacity *= 2;

        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = newBuffer;
        return _buffer;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (_buffer is not null)
            ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = null;
        _disposed = true;
    }
}
