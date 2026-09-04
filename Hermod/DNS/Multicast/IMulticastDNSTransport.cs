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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A datagram received by a Multicast DNS transport.
    /// </summary>
    /// <param name="Payload">The payload.</param>
    /// <param name="RemoteSocket">The source of the datagram.</param>
    /// <param name="InterfaceIndex">The index of the network interface that received the datagram, when known.</param>
    /// <param name="Timestamp">The time of reception.</param>
    public sealed record MulticastDNSDatagram(ReadOnlyMemory<Byte>  Payload,
                                              IPSocket              RemoteSocket,
                                              Int32?                InterfaceIndex,
                                              DateTimeOffset        Timestamp)
    {

        /// <summary>
        /// Whether the datagram was received via IPv6.
        /// </summary>
        public Boolean ViaIPv6
            => RemoteSocket.IPAddress.IsIPv6;

    }


    /// <summary>
    /// A delegate called whenever a Multicast DNS transport received a datagram.
    /// </summary>
    /// <param name="Timestamp">The time of reception.</param>
    /// <param name="Sender">The transport.</param>
    /// <param name="Datagram">The datagram.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task OnMulticastDNSDatagramReceivedDelegate(DateTimeOffset          Timestamp,
                                                                 IMulticastDNSTransport  Sender,
                                                                 MulticastDNSDatagram    Datagram,
                                                                 CancellationToken       CancellationToken);


    /// <summary>
    /// The transport of Multicast DNS: sends datagrams to the multicast groups or to a
    /// single socket and delivers every received datagram to all subscribers. One
    /// transport is shared by the responder and the client of a process, so that
    /// the operating system sees one Multicast DNS socket per address family.
    /// </summary>
    public interface IMulticastDNSTransport : IAsyncDisposable
    {

        /// <summary>
        /// The Multicast DNS port of this transport (5353 unless a test chose another one).
        /// </summary>
        IPPort                     Port              { get; }

        /// <summary>
        /// Whether this transport is running.
        /// </summary>
        Boolean                    IsRunning         { get; }

        /// <summary>
        /// The unicast addresses of this host that may be advertised in A and AAAA records.
        /// </summary>
        IReadOnlyList<IIPAddress>  LocalAddresses    { get; }

        /// <summary>
        /// An event fired whenever a datagram was received.
        /// </summary>
        event OnMulticastDNSDatagramReceivedDelegate?  OnDatagramReceived;

        /// <summary>
        /// Start the transport.
        /// </summary>
        /// <param name="CancellationToken">A token to cancel the start.</param>
        Task StartAsync(CancellationToken CancellationToken = default);

        /// <summary>
        /// Stop the transport.
        /// </summary>
        /// <param name="CancellationToken">A token to cancel the stop.</param>
        Task StopAsync(CancellationToken CancellationToken = default);

        /// <summary>
        /// Send the given payload to the multicast groups (when no destination is given)
        /// or to the given socket (unicast responses, RFC 6762 §5.4 and §6.7).
        /// </summary>
        /// <param name="Payload">The payload.</param>
        /// <param name="Destination">An optional unicast destination.</param>
        /// <param name="InterfaceIndex">An optional network interface to send a multicast on (default: all).</param>
        /// <param name="CancellationToken">A token to cancel the sending.</param>
        Task SendAsync(ReadOnlyMemory<Byte>  Payload,
                       IPSocket?             Destination         = null,
                       Int32?                InterfaceIndex      = null,
                       CancellationToken     CancellationToken   = default);


        /// <summary>
        /// Whether a query from the given socket is a "legacy unicast" query of a
        /// conventional resolver, recognisable by a source port other than the
        /// Multicast DNS port (RFC 6762 §6.7).
        /// </summary>
        /// <param name="RemoteSocket">The source of a query.</param>
        Boolean IsLegacyUnicast(IPSocket RemoteSocket)
            => RemoteSocket.Port != Port;

    }

}
