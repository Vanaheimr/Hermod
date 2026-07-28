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

using System.Text;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

/// <summary>
/// CONNECTION_CLOSE frame (type 0x1c/0x1d, RFC 9000 §19.19). Type 0x1c reports a QUIC transport
/// error (with the triggering frame type), type 0x1d an application error (without the frame-type field).
/// </summary>
public sealed record ConnectionCloseFrame(
    ulong ErrorCode,
    bool IsApplicationError,
    ulong TriggeringFrameType,
    string ReasonPhrase) : Frame
{
    /// <summary>
    /// Creates a transport CONNECTION_CLOSE (type 0x1c).
    /// </summary>
    public static ConnectionCloseFrame Transport(TransportError error, string reason = "", ulong triggeringFrameType = 0)
        => new((ulong)error, IsApplicationError: false, triggeringFrameType, reason);

    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(IsApplicationError ? FrameType.ConnectionCloseApp : FrameType.ConnectionCloseQuic);
        writer.WriteVarInt(ErrorCode);
        if (!IsApplicationError)
            writer.WriteVarInt(TriggeringFrameType);

        byte[] reason = Encoding.UTF8.GetBytes(ReasonPhrase);
        writer.WriteVarInt((ulong)reason.Length);
        writer.WriteBytes(reason);
    }

    /// <summary>
    /// Parses the frame body. <paramref name="isApplication"/> follows from the type (0x1d).
    /// </summary>
    public static bool TryReadBody(ref BufferReader reader, bool isApplication, out ConnectionCloseFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong errorCode))
            return false;

        ulong triggeringFrameType = 0;
        if (!isApplication && !reader.TryReadVarInt(out triggeringFrameType))
            return false;

        if (!reader.TryReadVarInt(out ulong reasonLength) ||
            reasonLength > (ulong)reader.Remaining ||
            !reader.TryReadBytes((int)reasonLength, out ReadOnlySpan<byte> reasonBytes))
            return false;

        frame = new ConnectionCloseFrame(errorCode, isApplication, triggeringFrameType, Encoding.UTF8.GetString(reasonBytes));
        return true;
    }
}
