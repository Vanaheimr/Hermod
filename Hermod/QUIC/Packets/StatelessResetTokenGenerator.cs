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
/// Derives stateless-reset tokens deterministically from a connection ID (RFC 9000 §10.3.1):
/// <c>token = HMAC-SHA256(secret, connection_id)[0..16]</c>. This lets an endpoint that has lost a
/// connection's state (restart) recompute the token belonging to the DCID and send a stateless
/// reset — without ever having held state. The secret lives process-wide; all instances share it.
/// </summary>
public sealed class StatelessResetTokenGenerator
{
    private readonly byte[] _secret;

    /// <summary>
    /// <paramref name="secret"/> = the (persistent) server secret; one is generated when omitted.
    /// </summary>
    public StatelessResetTokenGenerator(byte[]? secret = null)
        => _secret = secret is { Length: > 0 } ? secret : RandomNumberGenerator.GetBytes(32);

    /// <summary>
    /// The 16-byte stateless-reset token belonging to <paramref name="connectionId"/>.
    /// </summary>
    public byte[] ComputeToken(ReadOnlySpan<byte> connectionId)
    {
        Span<byte> mac = stackalloc byte[32];
        HMACSHA256.HashData(_secret, connectionId, mac);
        return mac[..StatelessReset.TokenLength].ToArray();
    }
}
