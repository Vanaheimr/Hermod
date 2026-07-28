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

using System.Buffers.Binary;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

/// <summary>
/// Forward reader over a <see cref="ReadOnlySpan{Byte}"/> (big-endian, as in the QUIC wire format).
/// <para>
/// Designed as a <c>ref struct</c> – lives only on the stack, no allocation, no copying of the
/// underlying buffer. The <c>Try*</c> methods never throw; they return <c>false</c> when not
/// enough bytes are present (incomplete packet). The non-<c>Try</c> variants throw
/// <see cref="EndOfBufferException"/> and are meant for places where the length was checked beforehand.
/// </para>
/// </summary>
public ref struct BufferReader
{
    private readonly ReadOnlySpan<byte> _buffer;

    public BufferReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        Position = 0;
    }

    /// <summary>
    /// Current read position (bytes from the beginning).
    /// </summary>
    public int Position { get; private set; }

    /// <summary>
    /// Number of bytes not yet read.
    /// </summary>
    public readonly int Remaining => _buffer.Length - Position;

    /// <summary>
    /// <c>true</c> when all bytes have been read.
    /// </summary>
    public readonly bool IsEmpty => Remaining == 0;

    /// <summary>
    /// The not-yet-read remainder, without changing the position.
    /// </summary>
    public readonly ReadOnlySpan<byte> RemainingSpan => _buffer[Position..];

    public bool TryReadByte(out byte value)
    {
        if (Remaining < 1)
        {
            value = 0;
            return false;
        }
        value = _buffer[Position];
        Position += 1;
        return true;
    }

    public byte ReadByte()
        => TryReadByte(out byte v) ? v : throw EndOfBuffer(1);

    public bool TryReadUInt16(out ushort value)
    {
        if (Remaining < 2)
        {
            value = 0;
            return false;
        }
        value = BinaryPrimitives.ReadUInt16BigEndian(_buffer[Position..]);
        Position += 2;
        return true;
    }

    public ushort ReadUInt16()
        => TryReadUInt16(out ushort v) ? v : throw EndOfBuffer(2);

    public bool TryReadUInt32(out uint value)
    {
        if (Remaining < 4)
        {
            value = 0;
            return false;
        }
        value = BinaryPrimitives.ReadUInt32BigEndian(_buffer[Position..]);
        Position += 4;
        return true;
    }

    public uint ReadUInt32()
        => TryReadUInt32(out uint v) ? v : throw EndOfBuffer(4);

    public bool TryReadUInt64(out ulong value)
    {
        if (Remaining < 8)
        {
            value = 0;
            return false;
        }
        value = BinaryPrimitives.ReadUInt64BigEndian(_buffer[Position..]);
        Position += 8;
        return true;
    }

    public ulong ReadUInt64()
        => TryReadUInt64(out ulong v) ? v : throw EndOfBuffer(8);

    /// <summary>
    /// Reads a QUIC VarInt (RFC 9000 §16).
    /// </summary>
    public bool TryReadVarInt(out ulong value)
    {
        if (VarInt.TryRead(RemainingSpan, out value, out int read))
        {
            Position += read;
            return true;
        }
        return false;
    }

    public ulong ReadVarInt()
        => TryReadVarInt(out ulong v) ? v : throw EndOfBuffer(1);

    /// <summary>
    /// Reads exactly <paramref name="length"/> bytes as a slice of the underlying buffer
    /// (no copying).
    /// </summary>
    public bool TryReadBytes(int length, out ReadOnlySpan<byte> value)
    {
        if (length < 0 || Remaining < length)
        {
            value = default;
            return false;
        }
        value = _buffer.Slice(Position, length);
        Position += length;
        return true;
    }

    public ReadOnlySpan<byte> ReadBytes(int length)
        => TryReadBytes(length, out ReadOnlySpan<byte> v) ? v : throw EndOfBuffer(length);

    /// <summary>
    /// Skips <paramref name="length"/> bytes.
    /// </summary>
    public bool TrySkip(int length)
    {
        if (length < 0 || Remaining < length)
            return false;
        Position += length;
        return true;
    }

    private readonly EndOfBufferException EndOfBuffer(int needed)
        => new(needed, Remaining);
}

/// <summary>
/// Thrown when a read operation would go past the end of the buffer.
/// </summary>
public sealed class EndOfBufferException(int needed, int available)
    : Exception($"End of buffer reached: needed {needed} byte(s), available {available}.")
{
    public int Needed { get; } = needed;
    public int Available { get; } = available;
}
