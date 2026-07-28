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
/// Base type of all QUIC frames (RFC 9000, §19). A packet payload is a plain concatenation of
/// frames without any framing – the frames are not self-describing, the receiver must know every
/// type (unknown types ⇒ FRAME_ENCODING_ERROR).
/// </summary>
public abstract record Frame
{
    /// <summary>
    /// Serialises the frame (including the frame type) into <paramref name="writer"/>.
    /// </summary>
    public abstract void Write(ref BufferWriter writer);
}

/// <summary>
/// One or more consecutive PADDING frames (type 0x00), combined as a run length.
/// Used to bring a packet up to a minimum size (e.g. client Initial ≥ 1200 bytes).
/// </summary>
public sealed record PaddingFrame(int Length) : Frame
{
    public override void Write(ref BufferWriter writer) => writer.WriteRepeated(0x00, Length);
}

/// <summary>
/// PING frame (type 0x01): elicits an ACK from the receiver; serves as keep-alive/path probe.
/// </summary>
public sealed record PingFrame : Frame
{
    public static readonly PingFrame Instance = new();
    public override void Write(ref BufferWriter writer) => writer.WriteVarInt(FrameType.Ping);
}

/// <summary>
/// HANDSHAKE_DONE frame (type 0x1e): the server confirms the completed handshake.
/// </summary>
public sealed record HandshakeDoneFrame : Frame
{
    public static readonly HandshakeDoneFrame Instance = new();
    public override void Write(ref BufferWriter writer) => writer.WriteVarInt(FrameType.HandshakeDone);
}
