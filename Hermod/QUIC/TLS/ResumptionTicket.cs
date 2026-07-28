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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

/// <summary>
/// A client-side stored session ticket (RFC 8446 §2.2/§4.6.1) for later resumption (PSK).
/// Contains the resumption PSK derived from the NewSessionTicket, the ticket identity,
/// the obfuscation offset for the ticket age, the cipher suite (the PSK is bound to its hash)
/// and the time of receipt. <see cref="MaxEarlyDataSize"/> and <see cref="PeerTransportParameters"/>
/// are only needed for 0-RTT (phase B).
/// </summary>
public sealed class ResumptionTicket
{
    public byte[] Psk { get; }
    public byte[] Identity { get; }
    public uint AgeAdd { get; }
    public CipherSuite CipherSuite { get; }
    public DateTimeOffset ReceivedAt { get; }
    public uint LifetimeSeconds { get; }
    public string ServerName { get; }
    public uint MaxEarlyDataSize { get; }
    public byte[] PeerTransportParameters { get; }

    public ResumptionTicket(
        byte[] psk,
        byte[] identity,
        uint ageAdd,
        CipherSuite cipherSuite,
        string serverName,
        uint lifetimeSeconds,
        uint maxEarlyDataSize,
        byte[] peerTransportParameters,
        DateTimeOffset? receivedAt = null)
    {
        Psk = psk;
        Identity = identity;
        AgeAdd = ageAdd;
        CipherSuite = cipherSuite;
        ServerName = serverName;
        LifetimeSeconds = lifetimeSeconds;
        MaxEarlyDataSize = maxEarlyDataSize;
        PeerTransportParameters = peerTransportParameters;
        ReceivedAt = receivedAt ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// <c>true</c> when 0-RTT data is permitted with this ticket (the server announced max_early_data_size).
    /// </summary>
    public bool AllowsEarlyData => MaxEarlyDataSize > 0;

    /// <summary>
    /// The obfuscated ticket age (RFC 8446 §4.2.11.1): <c>(elapsed ms + age_add) mod 2^32</c>.
    /// The server subtracts the <see cref="AgeAdd"/> again and checks the age against the lifetime.
    /// </summary>
    public uint ObfuscatedTicketAge(DateTimeOffset now)
    {
        long elapsedMs = (long)(now - ReceivedAt).TotalMilliseconds;
        if (elapsedMs < 0)
            elapsedMs = 0;
        return unchecked((uint)elapsedMs + AgeAdd);
    }
}
