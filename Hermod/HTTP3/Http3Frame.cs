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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// A raw HTTP/3 frame: type + payload (RFC 9114 §7.1: Type(i) ‖ Length(i) ‖ Payload).
/// </summary>
public readonly record struct Http3Frame(ulong Type, ReadOnlyMemory<byte> Payload);

/// <summary>
/// Serialising and (incremental) parsing of HTTP/3 frames.
/// </summary>
public static class Http3Frames
{
    /// <summary>
    /// Writes an HTTP/3 frame (type, length, payload) into <paramref name="writer"/>.
    /// </summary>
    public static void Write(ref BufferWriter writer, ulong type, ReadOnlySpan<byte> payload)
    {
        writer.WriteVarInt(type);
        writer.WriteVarInt((ulong)payload.Length);
        writer.WriteBytes(payload);
    }

    /// <summary>
    /// Builds a single HTTP/3 frame as a byte array.
    /// </summary>
    public static byte[] Build(ulong type, ReadOnlySpan<byte> payload)
    {
        var writer = new BufferWriter(payload.Length + 8);
        try
        {
            Write(ref writer, type, payload);
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    /// <summary>
    /// Reads as many complete frames as possible from <paramref name="buffer"/>.
    /// <paramref name="consumed"/> indicates how many bytes were consumed – the rest is a
    /// still-incomplete frame (wait for more stream data).
    /// </summary>
    /// <summary>
    /// Reads only the HEADER of the next frame (type + length) without waiting for its payload.
    /// Needed for streaming request bodies: a DATA frame can carry the entire upload, so waiting for
    /// it to be complete would buffer exactly what streaming is meant to avoid.
    /// </summary>
    /// <param name="headerLength">Bytes consumed by type + length.</param>
    public static bool TryReadFrameHeader(ReadOnlySpan<byte> buffer, out ulong type, out ulong length,
                                          out int headerLength)
    {
        var reader = new BufferReader(buffer);
        type = 0;
        length = 0;
        headerLength = 0;
        if (!reader.TryReadVarInt(out type) || !reader.TryReadVarInt(out length))
            return false; // header not yet complete
        headerLength = reader.Position;
        return true;
    }

    public static bool TryReadAll(ReadOnlyMemory<byte> buffer, out List<Http3Frame> frames, out int consumed)
    {
        frames = [];
        consumed = 0;
        var reader = new BufferReader(buffer.Span);

        while (!reader.IsEmpty)
        {
            if (!reader.TryReadVarInt(out ulong type) || !reader.TryReadVarInt(out ulong length))
                break; // frame header incomplete
            if (length > (ulong)reader.Remaining)
                break; // payload not yet complete

            int payloadStart = reader.Position;
            if (!reader.TrySkip((int)length))
                break;

            frames.Add(new Http3Frame(type, buffer.Slice(payloadStart, (int)length)));
            consumed = reader.Position;
        }

        return true;
    }
}
