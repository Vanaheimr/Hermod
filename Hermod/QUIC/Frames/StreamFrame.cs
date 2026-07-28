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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

/// <summary>
/// STREAM frame (type 0x08..0x0f, RFC 9000 §19.8): carries application stream data. The lower three
/// bits of the type are flags: OFF (0x04) = offset field present, LEN (0x02) = length field present,
/// FIN (0x01) = end of stream.
/// </summary>
public sealed record StreamFrame(ulong StreamId, ulong Offset, ReadOnlyMemory<byte> Data, bool Fin) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        // We always write with the LEN bit (explicit length) – robust even when coalescing with following frames.
        byte type = (byte)FrameType.StreamBase;
        if (Offset != 0) type |= FrameType.StreamOffBit;
        type |= FrameType.StreamLenBit;
        if (Fin) type |= FrameType.StreamFinBit;

        writer.WriteVarInt(type);
        writer.WriteVarInt(StreamId);
        if (Offset != 0)
            writer.WriteVarInt(Offset);
        writer.WriteVarInt((ulong)Data.Length);
        writer.WriteBytes(Data.Span);
    }

    /// <summary>
    /// Parses the frame body. <paramref name="type"/> is the already-read type byte (with flags).
    /// </summary>
    public static bool TryReadBody(ref BufferReader reader, byte type, out StreamFrame? frame)
    {
        frame = null;
        bool hasOffset = (type & FrameType.StreamOffBit) != 0;
        bool hasLength = (type & FrameType.StreamLenBit) != 0;
        bool fin = (type & FrameType.StreamFinBit) != 0;

        if (!reader.TryReadVarInt(out ulong streamId))
            return false;

        ulong offset = 0;
        if (hasOffset && !reader.TryReadVarInt(out offset))
            return false;

        int length;
        if (hasLength)
        {
            if (!reader.TryReadVarInt(out ulong len) || len > (ulong)reader.Remaining)
                return false;
            length = (int)len;
        }
        else
        {
            // Without the LEN bit, the stream data extends to the end of the packet.
            length = reader.Remaining;
        }

        if (!reader.TryReadBytes(length, out ReadOnlySpan<byte> data))
            return false;

        frame = new StreamFrame(streamId, offset, data.ToArray(), fin);
        return true;
    }
}
