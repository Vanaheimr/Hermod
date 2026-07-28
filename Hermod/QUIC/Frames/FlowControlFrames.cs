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
/// MAX_DATA frame (type 0x10, RFC 9000 §19.9): raises the connection flow-control limit.
/// </summary>
public sealed record MaxDataFrame(ulong MaximumData) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.MaxData);
        writer.WriteVarInt(MaximumData);
    }

    public static bool TryReadBody(ref BufferReader reader, out MaxDataFrame? frame)
    {
        frame = reader.TryReadVarInt(out ulong max) ? new MaxDataFrame(max) : null;
        return frame is not null;
    }
}

/// <summary>
/// MAX_STREAM_DATA frame (type 0x11, RFC 9000 §19.10): raises the limit of a stream.
/// </summary>
public sealed record MaxStreamDataFrame(ulong StreamId, ulong MaximumStreamData) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.MaxStreamData);
        writer.WriteVarInt(StreamId);
        writer.WriteVarInt(MaximumStreamData);
    }

    public static bool TryReadBody(ref BufferReader reader, out MaxStreamDataFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong id) || !reader.TryReadVarInt(out ulong max))
            return false;
        frame = new MaxStreamDataFrame(id, max);
        return true;
    }
}

/// <summary>
/// MAX_STREAMS frame (type 0x12 bidi / 0x13 uni, RFC 9000 §19.11).
/// </summary>
public sealed record MaxStreamsFrame(bool Bidirectional, ulong MaximumStreams) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(Bidirectional ? FrameType.MaxStreamsBidi : FrameType.MaxStreamsUni);
        writer.WriteVarInt(MaximumStreams);
    }

    public static bool TryReadBody(ref BufferReader reader, bool bidirectional, out MaxStreamsFrame? frame)
    {
        frame = reader.TryReadVarInt(out ulong max) ? new MaxStreamsFrame(bidirectional, max) : null;
        return frame is not null;
    }
}

/// <summary>
/// DATA_BLOCKED frame (type 0x14, RFC 9000 §19.12): the sender is blocked at the connection limit.
/// </summary>
public sealed record DataBlockedFrame(ulong MaximumData) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.DataBlocked);
        writer.WriteVarInt(MaximumData);
    }

    public static bool TryReadBody(ref BufferReader reader, out DataBlockedFrame? frame)
    {
        frame = reader.TryReadVarInt(out ulong max) ? new DataBlockedFrame(max) : null;
        return frame is not null;
    }
}

/// <summary>
/// STREAM_DATA_BLOCKED frame (type 0x15, RFC 9000 §19.13): blocked at the stream limit.
/// </summary>
public sealed record StreamDataBlockedFrame(ulong StreamId, ulong MaximumStreamData) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.StreamDataBlocked);
        writer.WriteVarInt(StreamId);
        writer.WriteVarInt(MaximumStreamData);
    }

    public static bool TryReadBody(ref BufferReader reader, out StreamDataBlockedFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong id) || !reader.TryReadVarInt(out ulong max))
            return false;
        frame = new StreamDataBlockedFrame(id, max);
        return true;
    }
}

/// <summary>
/// STREAMS_BLOCKED frame (type 0x16 bidi / 0x17 uni, RFC 9000 §19.14).
/// </summary>
public sealed record StreamsBlockedFrame(bool Bidirectional, ulong MaximumStreams) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(Bidirectional ? FrameType.StreamsBlockedBidi : FrameType.StreamsBlockedUni);
        writer.WriteVarInt(MaximumStreams);
    }

    public static bool TryReadBody(ref BufferReader reader, bool bidirectional, out StreamsBlockedFrame? frame)
    {
        frame = reader.TryReadVarInt(out ulong max) ? new StreamsBlockedFrame(bidirectional, max) : null;
        return frame is not null;
    }
}

/// <summary>
/// RESET_STREAM frame (type 0x04, RFC 9000 §19.4): aborts the send side of a stream.
/// </summary>
public sealed record ResetStreamFrame(ulong StreamId, ulong ApplicationErrorCode, ulong FinalSize) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.ResetStream);
        writer.WriteVarInt(StreamId);
        writer.WriteVarInt(ApplicationErrorCode);
        writer.WriteVarInt(FinalSize);
    }

    public static bool TryReadBody(ref BufferReader reader, out ResetStreamFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong id) || !reader.TryReadVarInt(out ulong error) || !reader.TryReadVarInt(out ulong finalSize))
            return false;
        frame = new ResetStreamFrame(id, error, finalSize);
        return true;
    }
}

/// <summary>
/// RESET_STREAM_AT frame (type 0x24, draft-ietf-quic-reliable-stream-reset §4): like RESET_STREAM,
/// but with an additional <paramref name="ReliableSize"/> — the sender guarantees reliable delivery
/// of the first <paramref name="ReliableSize"/> bytes although the stream is being reset. Reliable
/// size 0 is equivalent to an ordinary RESET_STREAM.
/// </summary>
public sealed record ResetStreamAtFrame(ulong StreamId, ulong ApplicationErrorCode, ulong FinalSize, ulong ReliableSize) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.ResetStreamAt);
        writer.WriteVarInt(StreamId);
        writer.WriteVarInt(ApplicationErrorCode);
        writer.WriteVarInt(FinalSize);
        writer.WriteVarInt(ReliableSize);
    }

    public static bool TryReadBody(ref BufferReader reader, out ResetStreamAtFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong id) || !reader.TryReadVarInt(out ulong error) ||
            !reader.TryReadVarInt(out ulong finalSize) || !reader.TryReadVarInt(out ulong reliableSize))
            return false;
        frame = new ResetStreamAtFrame(id, error, finalSize, reliableSize);
        return true;
    }
}

/// <summary>
/// DATAGRAM frame (type 0x30/0x31, RFC 9221 §4): unreliable application data — not retransmitted,
/// not flow-controlled, but ack-eliciting and congestion-controlled. We always send the variant
/// WITH the length field (0x31); both are parsed (for 0x30 the payload extends to the end of the packet).
/// </summary>
public sealed record DatagramFrame(ReadOnlyMemory<byte> Data) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.DatagramWithLength);
        writer.WriteVarInt((ulong)Data.Length);
        writer.WriteBytes(Data.Span);
    }

    public static bool TryReadBody(ref BufferReader reader, bool hasLength, out DatagramFrame? frame)
    {
        frame = null;
        int count;
        if (hasLength)
        {
            if (!reader.TryReadVarInt(out ulong length) || length > (ulong)reader.Remaining)
                return false;
            count = (int)length;
        }
        else
            count = reader.Remaining; // LEN bit 0: data up to the end of the packet (RFC 9221 §4)
        if (!reader.TryReadBytes(count, out ReadOnlySpan<byte> data))
            return false;
        frame = new DatagramFrame(data.ToArray());
        return true;
    }
}

/// <summary>
/// STOP_SENDING frame (type 0x05, RFC 9000 §19.5): asks the peer to stop sending.
/// </summary>
public sealed record StopSendingFrame(ulong StreamId, ulong ApplicationErrorCode) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.StopSending);
        writer.WriteVarInt(StreamId);
        writer.WriteVarInt(ApplicationErrorCode);
    }

    public static bool TryReadBody(ref BufferReader reader, out StopSendingFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong id) || !reader.TryReadVarInt(out ulong error))
            return false;
        frame = new StopSendingFrame(id, error);
        return true;
    }
}
