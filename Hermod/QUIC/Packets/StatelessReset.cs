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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

/// <summary>
/// Stateless reset (RFC 9000 §10.3): an endpoint that has lost the state needed for a connection
/// (e.g. after a restart) cannot send a regular CONNECTION_CLOSE (the keys are gone). Instead it
/// sends a packet that outwardly looks like a 1-RTT packet (short-header form, otherwise
/// unpredictable bytes) and carries, in the <b>last 16 bytes</b>, the stateless-reset token belonging
/// to the connection ID. The receiver recognises it by the known token and ends the connection.
/// </summary>
public static class StatelessReset
{
    /// <summary>
    /// Length of the stateless-reset token (RFC 9000 §10.3).
    /// </summary>
    public const int TokenLength = 16;

    /// <summary>
    /// Minimum length of a stateless-reset packet (RFC 9000 §10.3): at least 5 unpredictable bytes +
    /// the 16-byte token. In practice, larger packets are sent so it looks like a real packet.
    /// </summary>
    public const int MinLength = 5 + TokenLength;

    /// <summary>
    /// Builds a stateless-reset packet with <paramref name="token"/> in the last 16 bytes. The rest
    /// is random; the first byte carries header form 0 and fixed bit 1 (looks like a short header).
    /// </summary>
    public static byte[] Build(ReadOnlySpan<byte> token, int totalLength = 41)
    {
        if (token.Length != TokenLength)
            throw new ArgumentException($"Token must be {TokenLength} bytes long.", nameof(token));

        int length = Math.Max(totalLength, MinLength);
        byte[] packet = RandomNumberGenerator.GetBytes(length);
        packet[0] = (byte)(0x40 | (packet[0] & 0x3f)); // header form 0 (short), fixed bit 1
        token.CopyTo(packet.AsSpan(length - TokenLength));
        return packet;
    }

    /// <summary>
    /// Checks in constant time whether <paramref name="datagram"/> ends with <paramref name="token"/>.
    /// </summary>
    public static bool EndsWith(ReadOnlySpan<byte> datagram, ReadOnlySpan<byte> token)
        => token.Length == TokenLength
           && datagram.Length >= TokenLength
           && CryptographicOperations.FixedTimeEquals(datagram[^TokenLength..], token);

    /// <summary>
    /// Builds the response to a packet that could not be matched to a connection (RFC 9000 §10.3):
    /// the token is computed from the destination connection ID (fixed length
    /// <paramref name="localCidLength"/>) and a stateless reset is sent. Only for <b>short-header</b>
    /// packets (1-RTT form; long headers would be a new connection) above a minimum size. Loop
    /// avoidance (§10.3.3): the reset is always <b>smaller</b> than the triggering packet. Returns
    /// <c>null</c> when no reset should be sent.
    /// </summary>
    public static byte[]? BuildResponse(ReadOnlySpan<byte> incomingDatagram, int localCidLength, StatelessResetTokenGenerator generator)
    {
        // Only reset 1-RTT-shaped packets and only when a genuinely smaller, valid reset (≥ MinLength) fits.
        if (incomingDatagram.Length <= MinLength ||
            !PacketFormat.IsShortHeader(incomingDatagram[0]) ||
            incomingDatagram.Length < 1 + localCidLength)
            return null;

        byte[] token = generator.ComputeToken(incomingDatagram.Slice(1, localCidLength));
        int resetLength = Math.Min(incomingDatagram.Length - 1, 41); // smaller than the trigger, but compact
        return Build(token, resetLength);
    }
}
