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

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic;

/// <summary>
/// The server's preferred address (RFC 9000 §9.6, transport parameter 0x0d, wire format §18.2
/// Figure 22): an address the server would rather serve this connection from, offered during the
/// handshake so the client can move there once the handshake is confirmed.
/// <para>
/// The typical use is anycast: clients reach a shared address first, and the instance that answers
/// hands out its own unicast address so the connection survives routing changes that would otherwise
/// send packets to a different instance.
/// </para>
/// <para>
/// The connection ID travelling with it has sequence number 1 (§18.2). It is there so the client is
/// guaranteed an unused active connection ID for the migration — using the handshake CID on the new
/// path would let an observer link the two paths.
/// </para>
/// </summary>
/// <param name="IPv4">The IPv4 address, or <c>null</c> when the server offers none.</param>
/// <param name="IPv4Port">Port for <paramref name="IPv4"/>.</param>
/// <param name="IPv6">The IPv6 address, or <c>null</c> when the server offers none.</param>
/// <param name="IPv6Port">Port for <paramref name="IPv6"/>.</param>
/// <param name="ConnectionId">Alternative connection ID with sequence number 1.</param>
/// <param name="StatelessResetToken">The 16-byte token belonging to that connection ID.</param>
public sealed record PreferredAddress(System.Net.IPAddress?          IPv4,
                                      UInt16              IPv4Port,
                                      System.Net.IPAddress?          IPv6,
                                      UInt16              IPv6Port,
                                      ConnectionId        ConnectionId,
                                      ReadOnlyMemory<Byte> StatelessResetToken)
{

    /// <summary>
    /// Fixed part of the encoding: 4 + 2 + 16 + 2 bytes of addresses and ports, one length byte,
    /// and the 16-byte token.
    /// </summary>
    private const Int32 FixedLength = 4 + 2 + 16 + 2 + 1 + 16;

    /// <summary>
    /// Value equality, including the token. The compiler-generated version would compare the
    /// <see cref="ReadOnlyMemory{T}"/> by reference, so two parameters carrying the same bytes —
    /// one built here, one parsed off the wire — would come out unequal.
    /// </summary>
    public Boolean Equals(PreferredAddress? other)

        => other is not null                                                     &&
           Equals(IPv4, other.IPv4)                                              &&
           IPv4Port == other.IPv4Port                                            &&
           Equals(IPv6, other.IPv6)                                              &&
           IPv6Port == other.IPv6Port                                            &&
           ConnectionId.Equals(other.ConnectionId)                               &&
           StatelessResetToken.Span.SequenceEqual(other.StatelessResetToken.Span);

    public override Int32 GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IPv4);
        hash.Add(IPv4Port);
        hash.Add(IPv6);
        hash.Add(IPv6Port);
        hash.Add(ConnectionId);
        hash.AddBytes(StatelessResetToken.Span);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Builds the parameter for a single IPv4 endpoint. §18.2: a server offering only one family
    /// sends an all-zero address and port for the other.
    /// </summary>
    public static PreferredAddress ForIPv4(IPEndPoint endpoint, ConnectionId connectionId, ReadOnlyMemory<Byte> token)
        => new(endpoint.Address, (UInt16)endpoint.Port, null, 0, connectionId, token);

    /// <summary>
    /// Builds the parameter for a single IPv6 endpoint.
    /// </summary>
    public static PreferredAddress ForIPv6(IPEndPoint endpoint, ConnectionId connectionId, ReadOnlyMemory<Byte> token)
        => new(null, 0, endpoint.Address, (UInt16)endpoint.Port, connectionId, token);

    /// <summary>
    /// The endpoint of the given family, or <c>null</c> when the server did not offer that family.
    /// </summary>
    public IPEndPoint? EndPointFor(AddressFamily family)
        => family switch
        {
            AddressFamily.InterNetwork   when IPv4 is { } v4 => new IPEndPoint(v4, IPv4Port),
            AddressFamily.InterNetworkV6 when IPv6 is { } v6 => new IPEndPoint(v6, IPv6Port),
            _ => null,
        };

    /// <summary>
    /// Serialises the parameter value (RFC 9000 §18.2 Figure 22). Addresses go out in network byte
    /// order; a family the server does not offer is written as all zeros.
    /// </summary>
    public Byte[] Encode()
    {
        Byte[] value = new Byte[FixedLength + ConnectionId.Length];
        Span<Byte> span = value;

        if (IPv4 is { } v4 && v4.AddressFamily == AddressFamily.InterNetwork)
            v4.TryWriteBytes(span[..4], out _);
        span[4] = (Byte)(IPv4Port >> 8);
        span[5] = (Byte)IPv4Port;

        if (IPv6 is { } v6 && v6.AddressFamily == AddressFamily.InterNetworkV6)
            v6.TryWriteBytes(span.Slice(6, 16), out _);
        span[22] = (Byte)(IPv6Port >> 8);
        span[23] = (Byte)IPv6Port;

        span[24] = (Byte)ConnectionId.Length;
        ConnectionId.Span.CopyTo(span.Slice(25, ConnectionId.Length));
        StatelessResetToken.Span.CopyTo(span.Slice(25 + ConnectionId.Length, 16));
        return value;
    }

    /// <summary>
    /// Parses the parameter value. <c>false</c> for anything malformed, which the caller must turn
    /// into TRANSPORT_PARAMETER_ERROR — including the two cases §18.2 calls out by name: a
    /// zero-length connection ID, and a connection ID longer than the 20 bytes §17.2 permits.
    /// </summary>
    public static Boolean TryParse(ReadOnlySpan<Byte> value, out PreferredAddress? address)
    {
        address = null;
        if (value.Length < FixedLength)
            return false;

        Int32 cidLength = value[24];
        // §18.2: "a server MUST NOT include a zero-length connection ID in this transport
        // parameter. A client MUST treat a violation of these requirements as a connection error
        // of type TRANSPORT_PARAMETER_ERROR."
        if (cidLength == 0 || cidLength > ConnectionId.MaxLength)
            return false;
        if (value.Length != FixedLength + cidLength)
            return false;

        var v4 = new System.Net.IPAddress(value[..4]);
        UInt16 v4Port = (UInt16)((value[4] << 8) | value[5]);
        var v6 = new System.Net.IPAddress(value.Slice(6, 16));
        UInt16 v6Port = (UInt16)((value[22] << 8) | value[23]);

        // An all-zero address and port means "this family is not offered" (§18.2) — reported as
        // null rather than as 0.0.0.0:0, so a caller cannot accidentally migrate to nowhere.
        Boolean hasV4 = v4Port != 0 && !v4.Equals(System.Net.IPAddress.Any);
        Boolean hasV6 = v6Port != 0 && !v6.Equals(System.Net.IPAddress.IPv6Any);

        address = new PreferredAddress(hasV4 ? v4 : null, hasV4 ? v4Port : (UInt16)0,
                                       hasV6 ? v6 : null, hasV6 ? v6Port : (UInt16)0,
                                       new ConnectionId(value.Slice(25, cidLength).ToArray()),
                                       value.Slice(25 + cidLength, 16).ToArray());
        return true;
    }

}
