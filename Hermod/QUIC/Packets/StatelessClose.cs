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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

/// <summary>
/// Builds an Initial packet carrying only a CONNECTION_CLOSE — a refusal a server can send
/// <b>without holding any connection state</b>. The Initial keys come from the DCID of the packet
/// being answered (RFC 9001 §5.2), which every endpoint can derive, so no handshake is needed.
/// <para>
/// The case that motivates this is an Initial with an unusable Retry token (RFC 9000 §8.1.2): the
/// client will not accept a second Retry, so the server "SHOULD immediately close the connection
/// with an INVALID_TOKEN error" — and the RFC notes explicitly that the server has established no
/// state at that point and therefore does not enter the closing period. Dropping the packet instead
/// would be legal but would cost the client a full handshake timeout.
/// </para>
/// </summary>
public static class StatelessClose
{
    /// <summary>
    /// Builds the refusal for a received client Initial.
    /// </summary>
    /// <param name="version">The QUIC version of the packet being answered.</param>
    /// <param name="clientDestinationConnectionId">
    /// Its DCID — the Initial keys are derived from it, and it becomes our source connection ID.
    /// </param>
    /// <param name="clientSourceConnectionId">Its SCID — our destination connection ID.</param>
    public static byte[] Build(uint version,
                               ConnectionId clientDestinationConnectionId,
                               ConnectionId clientSourceConnectionId,
                               TransportError error,
                               string reason = "")
    {
        InitialSecrets secrets = InitialSecrets.DeriveV1(clientDestinationConnectionId.Span);
        var protection = new PacketProtection(secrets.Server);

        byte[] payload = FrameParser.Serialize([ConnectionCloseFrame.Transport(error, reason)]);

        // Deliberately NOT padded: this is a server packet, so the 1200-byte minimum of RFC 9000
        // §14.1 does not apply, and staying small keeps us far inside the anti-amplification limit
        // of three times what the client sent (§8).
        return LongHeader.Build(protection, LongPacketType.Initial, version,
                                destinationConnectionId: clientSourceConnectionId,
                                sourceConnectionId: clientDestinationConnectionId,
                                token: default, packetNumber: 0, packetNumberLength: 1, payload);
    }
}
