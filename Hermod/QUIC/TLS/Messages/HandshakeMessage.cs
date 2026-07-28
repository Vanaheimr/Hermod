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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

/// <summary>
/// A single TLS handshake message: type, body and the complete bytes (for the transcript).
/// </summary>
public readonly record struct HandshakeMessage(HandshakeType Type, ReadOnlyMemory<byte> Body, ReadOnlyMemory<byte> Full);

/// <summary>
/// Splits a byte stream (possibly reassembled from multiple CRYPTO frames) into individual
/// handshake messages. Each message is <c>type (1) ‖ length (3) ‖ body</c> (RFC 8446 §4).
/// </summary>
public static class HandshakeMessages
{
    /// <summary>
    /// Reads as many complete messages as possible. <paramref name="consumed"/> indicates how many
    /// bytes were consumed – the rest is a still-incomplete message (more CRYPTO data needed).
    /// Returns <c>false</c> only for structurally impossible lengths.
    /// </summary>
    public static bool TryReadAll(ReadOnlyMemory<byte> buffer, out List<HandshakeMessage> messages, out int consumed)
    {
        messages = [];
        consumed = 0;
        var reader = new BufferReader(buffer.Span);

        while (reader.Remaining >= 4)
        {
            int start = reader.Position;
            byte type = reader.ReadByte();
            int length = (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte();

            if (reader.Remaining < length)
                break; // message not yet complete -> wait for more CRYPTO data

            reader.TrySkip(length);
            int end = reader.Position;
            messages.Add(new HandshakeMessage(
                (HandshakeType)type,
                buffer.Slice(start + 4, length),
                buffer.Slice(start, end - start)));
            consumed = end;
        }

        return true;
    }
}
