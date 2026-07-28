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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

/// <summary>
/// Ensures that packet number and payload together are long enough for the header-protection sample
/// (RFC 9001 §5.4.2). The 16-byte sample starts 4 bytes past the beginning of the packet-number
/// field; for it to lie inside the packet (together with the 16-byte AEAD tag),
/// <c>packet-number length + payload ≥ 4</c> must hold. Missing bytes are filled with PADDING (0x00).
/// </summary>
public static class PacketPadding
{
    public static byte[] ForSampling(ReadOnlySpan<byte> payload, int packetNumberLength)
    {
        int minPayload = 4 - packetNumberLength;
        int length = Math.Max(payload.Length, minPayload);
        byte[] result = new byte[length];
        payload.CopyTo(result);
        // The remainder stays 0 ⇒ PADDING frames.
        return result;
    }
}
