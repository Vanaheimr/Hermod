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
/// NEW_TOKEN frame (type 0x07, RFC 9000 §19.7): the server delivers a token for future Initials.
/// </summary>
public sealed record NewTokenFrame(ReadOnlyMemory<byte> Token) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.NewToken);
        writer.WriteVarInt((ulong)Token.Length);
        writer.WriteBytes(Token.Span);
    }

    public static bool TryReadBody(ref BufferReader reader, out NewTokenFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong length) || length > (ulong)reader.Remaining ||
            !reader.TryReadBytes((int)length, out ReadOnlySpan<byte> token))
            return false;
        frame = new NewTokenFrame(token.ToArray());
        return true;
    }
}

/// <summary>
/// NEW_CONNECTION_ID frame (type 0x18, RFC 9000 §19.15): the server offers another connection ID
/// along with a stateless-reset token.
/// </summary>
public sealed record NewConnectionIdFrame(
    ulong SequenceNumber,
    ulong RetirePriorTo,
    ReadOnlyMemory<byte> ConnectionId,
    ReadOnlyMemory<byte> StatelessResetToken) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(0x18);
        writer.WriteVarInt(SequenceNumber);
        writer.WriteVarInt(RetirePriorTo);
        writer.WriteByte((byte)ConnectionId.Length);
        writer.WriteBytes(ConnectionId.Span);
        writer.WriteBytes(StatelessResetToken.Span);
    }

    public static bool TryReadBody(ref BufferReader reader, out NewConnectionIdFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong seq) ||
            !reader.TryReadVarInt(out ulong retirePriorTo) ||
            !reader.TryReadByte(out byte cidLen) || cidLen > 20 ||
            !reader.TryReadBytes(cidLen, out ReadOnlySpan<byte> cid) ||
            !reader.TryReadBytes(16, out ReadOnlySpan<byte> token))
            return false;
        frame = new NewConnectionIdFrame(seq, retirePriorTo, cid.ToArray(), token.ToArray());
        return true;
    }
}

/// <summary>
/// RETIRE_CONNECTION_ID frame (type 0x19, RFC 9000 §19.16).
/// </summary>
public sealed record RetireConnectionIdFrame(ulong SequenceNumber) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(0x19);
        writer.WriteVarInt(SequenceNumber);
    }

    public static bool TryReadBody(ref BufferReader reader, out RetireConnectionIdFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong seq))
            return false;
        frame = new RetireConnectionIdFrame(seq);
        return true;
    }
}

/// <summary>
/// PATH_CHALLENGE frame (type 0x1a, RFC 9000 §19.17): 8 random bytes for path validation. The
/// receiver mirrors them unchanged in a PATH_RESPONSE.
/// </summary>
public sealed record PathChallengeFrame(ulong Data) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.PathChallenge);
        writer.WriteUInt64(Data);
    }

    public static bool TryReadBody(ref BufferReader reader, out PathChallengeFrame? frame)
    {
        frame = null;
        if (!reader.TryReadUInt64(out ulong data))
            return false;
        frame = new PathChallengeFrame(data);
        return true;
    }
}

/// <summary>
/// PATH_RESPONSE frame (type 0x1b, RFC 9000 §19.18): mirrors the 8 bytes of a PATH_CHALLENGE.
/// </summary>
public sealed record PathResponseFrame(ulong Data) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.PathResponse);
        writer.WriteUInt64(Data);
    }

    public static bool TryReadBody(ref BufferReader reader, out PathResponseFrame? frame)
    {
        frame = null;
        if (!reader.TryReadUInt64(out ulong data))
            return false;
        frame = new PathResponseFrame(data);
        return true;
    }
}
