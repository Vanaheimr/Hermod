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
using System.Numerics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;

/// <summary>
/// The ChaCha20 block function (RFC 8439 §2.3). Needed for the header protection of the
/// ChaCha20-Poly1305 suite (RFC 9001 §5.4.4); the BCL provides no raw ChaCha20 keystream. ChaCha20
/// consists only of additions, XOR and rotations – no data-dependent branches or table accesses and
/// thus constant-time by nature (unlike AES with its S-box tables).
/// </summary>
public static class ChaCha20
{
    /// <summary>
    /// Computes the 5-byte header-protection mask (RFC 9001 §5.4.4): the counter is the first 4 bytes
    /// of the sample (little-endian), the nonce the following 12 bytes; the mask is the first 5 bytes
    /// of the ChaCha20 keystream block over the HP key.
    /// </summary>
    public static void HeaderProtectionMask(ReadOnlySpan<byte> headerProtectionKey, ReadOnlySpan<byte> sample, Span<byte> mask)
    {
        uint counter = BinaryPrimitives.ReadUInt32LittleEndian(sample);
        Span<byte> keystream = stackalloc byte[64];
        Block(headerProtectionKey, counter, sample.Slice(4, 12), keystream);
        keystream[..5].CopyTo(mask);
    }

    /// <summary>
    /// Produces a 64-byte keystream block (RFC 8439 §2.3): <paramref name="output"/> must hold 64 bytes.
    /// </summary>
    public static void Block(ReadOnlySpan<byte> key, uint counter, ReadOnlySpan<byte> nonce, Span<byte> output)
    {
        Span<uint> state =
        [
            0x61707865, 0x3320646e, 0x79622d32, 0x6b206574, // "expand 32-byte k"
            U32(key, 0), U32(key, 4), U32(key, 8), U32(key, 12),
            U32(key, 16), U32(key, 20), U32(key, 24), U32(key, 28),
            counter,
            U32(nonce, 0), U32(nonce, 4), U32(nonce, 8),
        ];

        Span<uint> working = stackalloc uint[16];
        state.CopyTo(working);

        for (int i = 0; i < 10; i++) // 20 rounds = 10 double rounds
        {
            QuarterRound(working, 0, 4, 8, 12);
            QuarterRound(working, 1, 5, 9, 13);
            QuarterRound(working, 2, 6, 10, 14);
            QuarterRound(working, 3, 7, 11, 15);
            QuarterRound(working, 0, 5, 10, 15);
            QuarterRound(working, 1, 6, 11, 12);
            QuarterRound(working, 2, 7, 8, 13);
            QuarterRound(working, 3, 4, 9, 14);
        }

        for (int i = 0; i < 16; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(i * 4, 4), working[i] + state[i]);
    }

    private static void QuarterRound(Span<uint> s, int a, int b, int c, int d)
    {
        s[a] += s[b]; s[d] = BitOperations.RotateLeft(s[d] ^ s[a], 16);
        s[c] += s[d]; s[b] = BitOperations.RotateLeft(s[b] ^ s[c], 12);
        s[a] += s[b]; s[d] = BitOperations.RotateLeft(s[d] ^ s[a], 8);
        s[c] += s[d]; s[b] = BitOperations.RotateLeft(s[b] ^ s[c], 7);
    }

    private static uint U32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
}
