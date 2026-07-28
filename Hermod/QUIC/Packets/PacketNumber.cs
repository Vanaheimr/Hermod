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
/// Encoding and reconstruction of QUIC packet numbers (RFC 9000, §17.1 and appendix A).
/// <para>
/// Only the low-order 1–4 bytes of the packet number are transmitted on the wire. The receiver
/// reconstructs the full 62-bit number relative to the largest packet number acknowledged so far.
/// </para>
/// </summary>
public static class PacketNumber
{
    /// <summary>
    /// Upper bound of the packet-number space (2^62).
    /// </summary>
    private const ulong PacketNumberLimit = 1UL << 62;

    /// <summary>
    /// Determines the minimum number of bytes (1–4) with which <paramref name="packetNumber"/> can be
    /// encoded such that the receiver reconstructs it unambiguously relative to
    /// <paramref name="largestAcked"/>. Before the first acknowledgment
    /// (<paramref name="largestAcked"/> = -1) the full needed length is chosen.
    /// </summary>
    public static int EncodeLength(ulong packetNumber, long largestAcked)
    {
        // Number of still-unacknowledged consecutive packet numbers incl. the new one (num_unacked).
        ulong range = largestAcked < 0 ? packetNumber + 1 : packetNumber - (ulong)largestAcked;

        // RFC 9000 A.2: min_bits = log2(range) + 1, num_bytes = ceil(min_bits / 8).
        // That yields the thresholds range <= 2^7, 2^15, 2^23 (bounds inclusive).
        if (range <= (1UL << 7)) return 1;
        if (range <= (1UL << 15)) return 2;
        if (range <= (1UL << 23)) return 3;
        return 4;
    }

    /// <summary>
    /// Writes the low-order <paramref name="length"/> bytes of <paramref name="packetNumber"/> big-endian.
    /// </summary>
    public static void Encode(Span<byte> destination, ulong packetNumber, int length)
    {
        if (length is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(length));
        for (int i = 0; i < length; i++)
            destination[i] = (byte)(packetNumber >> (8 * (length - 1 - i)));
    }

    /// <summary>
    /// Reconstructs the full packet number from the truncated wire encoding
    /// (RFC 9000, appendix A.3 – <c>DecodePacketNumber</c>).
    /// </summary>
    /// <param name="truncated">The number received truncated to <paramref name="length"/> bytes.</param>
    /// <param name="length">Number of bytes received (1–4).</param>
    /// <param name="largestAcked">Largest successfully processed packet number so far (-1 = none yet).</param>
    public static ulong Decode(uint truncated, int length, long largestAcked)
    {
        int pnBits = length * 8;
        ulong pnWin = 1UL << pnBits;
        ulong pnHalfWin = pnWin / 2;
        ulong pnMask = pnWin - 1;

        // "expected" is the next expected packet number.
        ulong expected = (ulong)(largestAcked + 1);
        ulong candidate = (expected & ~pnMask) | truncated;

        if (candidate + pnHalfWin <= expected && candidate + pnWin < PacketNumberLimit)
            return candidate + pnWin;
        if (candidate > expected + pnHalfWin && candidate >= pnWin)
            return candidate - pnWin;
        return candidate;
    }
}
