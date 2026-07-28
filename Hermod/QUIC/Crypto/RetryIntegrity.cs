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

using System.Security.Cryptography;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;

/// <summary>
/// Retry integrity tag (RFC 9001, §5.8): a 128-bit AEAD tag over the Retry pseudo-packet.
/// Proves that the sender saw the original Initial packet and protects against accidental
/// corruption. Key and nonce are fixed for QUIC v1.
/// </summary>
public static class RetryIntegrity
{
    // K = 0xbe0c690b9f66575a1d766b54e368c84e
    private static ReadOnlySpan<byte> Key =>
    [
        0xbe, 0x0c, 0x69, 0x0b, 0x9f, 0x66, 0x57, 0x5a,
        0x1d, 0x76, 0x6b, 0x54, 0xe3, 0x68, 0xc8, 0x4e
    ];

    // N = 0x461599d35d632bf2239825bb
    private static ReadOnlySpan<byte> Nonce =>
    [
        0x46, 0x15, 0x99, 0xd3, 0x5d, 0x63, 0x2b, 0xf2, 0x23, 0x98, 0x25, 0xbb
    ];

    /// <summary>
    /// Computes the 16-byte integrity tag over the Retry pseudo-packet:
    /// <c>ODCID length (1 byte) || original DCID || Retry packet without the tag</c>.
    /// </summary>
    /// <param name="originalDestinationConnectionId">The DCID from the client Initial that triggered the Retry.</param>
    /// <param name="retryWithoutTag">The Retry packet from the first byte up to and including the Retry token, without the tag.</param>
    public static byte[] ComputeTag(ReadOnlySpan<byte> originalDestinationConnectionId, ReadOnlySpan<byte> retryWithoutTag)
    {
        using var writer = new BufferWriter(1 + originalDestinationConnectionId.Length + retryWithoutTag.Length);
        writer.WriteByte((byte)originalDestinationConnectionId.Length);
        writer.WriteBytes(originalDestinationConnectionId);
        writer.WriteBytes(retryWithoutTag);

        byte[] tag = new byte[16];
        using var aead = new AesGcm(Key, 16);
        aead.Encrypt(Nonce, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, tag, writer.WrittenSpan);
        return tag;
    }

    /// <summary>
    /// Verifies the tag of a received Retry packet (constant-time comparison).
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> originalDestinationConnectionId, ReadOnlySpan<byte> retryWithoutTag, ReadOnlySpan<byte> tag)
    {
        byte[] expected = ComputeTag(originalDestinationConnectionId, retryWithoutTag);
        return CryptographicOperations.FixedTimeEquals(expected, tag);
    }
}
