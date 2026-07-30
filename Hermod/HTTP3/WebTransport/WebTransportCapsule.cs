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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;

/// <summary>
/// A capsule of the capsule protocol (RFC 9297 §3.2): type ‖ length ‖ value. On a WebTransport
/// session's CONNECT stream, capsules carry session control (WT_CLOSE_SESSION) and flow control
/// (WT_MAX_STREAMS/WT_MAX_DATA/…). Unknown types MUST be skipped silently (§3.2).
/// </summary>
public readonly record struct WebTransportCapsule(ulong Type, ReadOnlyMemory<byte> Value)
{
    /// <summary>
    /// Serialises a capsule (type ‖ varint length ‖ value).
    /// </summary>
    public static byte[] Build(ulong type, ReadOnlySpan<byte> value)
    {
        var writer = new BufferWriter(value.Length + 16);
        try
        {
            writer.WriteVarInt(type);
            writer.WriteVarInt((ulong)value.Length);
            writer.WriteBytes(value);
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    /// <summary>
    /// A capsule with a single varint value (WT_MAX_STREAMS/WT_MAX_DATA/…).
    /// </summary>
    public static byte[] BuildVarIntCapsule(ulong type, ulong value)
    {
        var inner = new BufferWriter(8);
        try
        {
            inner.WriteVarInt(value);
            return Build(type, inner.WrittenSpan);
        }
        finally { inner.Dispose(); }
    }

    /// <summary>
    /// The WT_CLOSE_SESSION capsule (§6): 32-bit error code (big-endian) ‖ UTF-8 message (≤ 1024 B).
    /// </summary>
    public static byte[] BuildCloseSession(uint errorCode, string reason)
    {
        byte[] reasonBytes = System.Text.Encoding.UTF8.GetBytes(reason);
        if (reasonBytes.Length > 1024)
            reasonBytes = reasonBytes[..1024];
        var value = new byte[4 + reasonBytes.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(value, errorCode);
        reasonBytes.CopyTo(value, 4);
        return Build(WebTransportConstants.CapsuleCloseSession, value);
    }

    /// <summary>
    /// Reads as many complete capsules as possible from <paramref name="buffer"/>;
    /// <paramref name="consumed"/> reports the consumed bytes (the rest is an incomplete
    /// capsule – wait for more stream data).
    /// </summary>
    public static List<WebTransportCapsule> ReadAll(ReadOnlyMemory<byte> buffer, out int consumed)
    {
        var capsules = new List<WebTransportCapsule>();
        consumed = 0;
        var reader = new BufferReader(buffer.Span);
        while (!reader.IsEmpty)
        {
            if (!reader.TryReadVarInt(out ulong type) || !reader.TryReadVarInt(out ulong length))
                break; // capsule header incomplete
            if (length > (ulong)reader.Remaining)
                break; // value not yet complete
            int valueStart = reader.Position;
            if (!reader.TrySkip((int)length))
                break;
            capsules.Add(new WebTransportCapsule(type, buffer.Slice(valueStart, (int)length)));
            consumed = reader.Position;
        }
        return capsules;
    }
}
