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

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A delegate called whenever a datagram was sent within an in-memory Multicast DNS network.
    /// </summary>
    /// <param name="Timestamp">The time of sending.</param>
    /// <param name="Sender">The sending transport.</param>
    /// <param name="Payload">The payload.</param>
    /// <param name="Destination">The unicast destination, or null for a multicast.</param>
    public delegate Task OnInMemoryMulticastDNSDatagramSentDelegate(DateTimeOffset                   Timestamp,
                                                                     InMemoryMulticastDNSTransport    Sender,
                                                                     ReadOnlyMemory<Byte>             Payload,
                                                                     IPSocket?                        Destination);


    /// <summary>
    /// An in-memory link for Multicast DNS transports: every multicast reaches every
    /// attached transport (including the sender, like a socket with multicast loopback),
    /// every unicast reaches the transport owning the destination address. Delivery is
    /// synchronous and ordered, which makes responder and querier behaviour testable
    /// without sockets, timers or a network.
    /// </summary>
    public sealed class InMemoryMulticastDNSNetwork
    {

        #region Data

        private readonly Lock                                 transportsLock   = new();
        private readonly List<InMemoryMulticastDNSTransport>  transports       = [];
        private          Int32                                nextHost         = 1;

        #endregion

        #region Properties

        /// <summary>
        /// The Multicast DNS port of this network.
        /// </summary>
        public IPPort        Port                { get; }

        /// <summary>
        /// The time provider of this network.
        /// </summary>
        public TimeProvider  TimeProvider        { get; }

        /// <summary>
        /// Whether a multicast is also delivered to its sender (default: true, like multicast loopback).
        /// </summary>
        public Boolean       DeliverToSender     { get; set; } = true;

        /// <summary>
        /// An optional filter deciding whether a datagram from the first transport reaches
        /// the second one (default: everything is delivered); useful to simulate packet loss.
        /// </summary>
        public Func<InMemoryMulticastDNSTransport, InMemoryMulticastDNSTransport, Boolean>?  Filter   { get; set; }

        /// <summary>
        /// The attached transports.
        /// </summary>
        public IReadOnlyList<InMemoryMulticastDNSTransport>  Transports
        {
            get
            {
                lock (transportsLock)
                    return [.. transports];
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever a datagram was sent within this network.
        /// </summary>
        public event OnInMemoryMulticastDNSDatagramSentDelegate?  OnDatagramSent;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new in-memory Multicast DNS network.
        /// </summary>
        /// <param name="Port">The Multicast DNS port of the network (default: 5353).</param>
        /// <param name="TimeProvider">An optional time provider.</param>
        public InMemoryMulticastDNSNetwork(IPPort?        Port           = null,
                                           TimeProvider?  TimeProvider   = null)
        {

            this.Port          = Port         ?? MulticastDNS.Port;
            this.TimeProvider  = TimeProvider ?? System.TimeProvider.System;

        }

        #endregion


        #region CreateTransport(Address = null, LoggerFactory = null)

        /// <summary>
        /// Create and attach a transport with the given (or the next free) address.
        /// </summary>
        /// <param name="Address">An optional host address (default: 10.53.0.n).</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        public InMemoryMulticastDNSTransport CreateTransport(IIPAddress?      Address         = null,
                                                             ILoggerFactory?  LoggerFactory   = null)
        {

            IIPAddress address;

            lock (transportsLock)
            {

                address = Address ?? IPv4Address.Parse($"10.53.{(nextHost >> 8) & 0xFF}.{nextHost & 0xFF}");
                nextHost++;

                var transport = new InMemoryMulticastDNSTransport(this, address, LoggerFactory);
                transports.Add(transport);
                return transport;

            }

        }

        #endregion

        #region (internal) Detach(Transport)

        internal void Detach(InMemoryMulticastDNSTransport Transport)
        {
            lock (transportsLock)
                transports.Remove(Transport);
        }

        #endregion

        #region (internal) DeliverAsync(Sender, Payload, Destination, CancellationToken)

        internal async Task DeliverAsync(InMemoryMulticastDNSTransport  Sender,
                                         ReadOnlyMemory<Byte>           Payload,
                                         IPSocket?                      Destination,
                                         CancellationToken              CancellationToken)
        {

            var timestamp = TimeProvider.GetUtcNow();

            var onDatagramSent = OnDatagramSent;
            if (onDatagramSent is not null)
            {
                foreach (var handler in onDatagramSent.GetInvocationList().Cast<OnInMemoryMulticastDNSDatagramSentDelegate>())
                    await handler(timestamp, Sender, Payload, Destination).ConfigureAwait(false);
            }

            var remoteSocket = new IPSocket(Sender.Address, Port);

            foreach (var transport in Transports)
            {

                if (Destination.HasValue)
                {
                    if (!transport.Address.Equals(Destination.Value.IPAddress))
                        continue;
                }

                else if (transport == Sender && !DeliverToSender)
                    continue;

                if (Filter is not null && !Filter(Sender, transport))
                    continue;

                await transport.ReceiveAsync(
                          new MulticastDNSDatagram(
                              Payload,
                              remoteSocket,
                              null,
                              timestamp
                          ),
                          CancellationToken
                      ).ConfigureAwait(false);

            }

        }

        #endregion

    }


    /// <summary>
    /// A Multicast DNS transport attached to an <see cref="InMemoryMulticastDNSNetwork"/>.
    /// </summary>
    public sealed class InMemoryMulticastDNSTransport : IMulticastDNSTransport
    {

        #region Data

        private readonly ILogger?  logger;
        private          Boolean   isRunning;
        private          Boolean   isDisposed;

        #endregion

        #region Properties

        /// <summary>
        /// The network of this transport.
        /// </summary>
        public InMemoryMulticastDNSNetwork  Network           { get; }

        /// <summary>
        /// The host address of this transport.
        /// </summary>
        public IIPAddress                   Address           { get; }

        /// <summary>
        /// The Multicast DNS port of the network.
        /// </summary>
        public IPPort                       Port
            => Network.Port;

        /// <summary>
        /// Whether this transport is running.
        /// </summary>
        public Boolean                      IsRunning
            => isRunning && !isDisposed;

        /// <summary>
        /// The host address as the only local address.
        /// </summary>
        public IReadOnlyList<IIPAddress>    LocalAddresses    { get; }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever a datagram was received.
        /// </summary>
        public event OnMulticastDNSDatagramReceivedDelegate?  OnDatagramReceived;

        #endregion

        #region Constructor(s)

        internal InMemoryMulticastDNSTransport(InMemoryMulticastDNSNetwork  Network,
                                               IIPAddress                   Address,
                                               ILoggerFactory?              LoggerFactory)
        {

            this.Network         = Network;
            this.Address         = Address;
            this.LocalAddresses  = [ Address ];
            this.logger          = LoggerFactory?.CreateLogger<InMemoryMulticastDNSTransport>();

        }

        #endregion


        #region StartAsync(CancellationToken = default)

        /// <summary>
        /// Start the transport.
        /// </summary>
        public Task StartAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(isDisposed, this);

            isRunning = true;
            return Task.CompletedTask;

        }

        #endregion

        #region StopAsync(CancellationToken = default)

        /// <summary>
        /// Stop the transport.
        /// </summary>
        public Task StopAsync(CancellationToken CancellationToken = default)
        {
            isRunning = false;
            return Task.CompletedTask;
        }

        #endregion

        #region SendAsync(Payload, Destination = null, InterfaceIndex = null, CancellationToken = default)

        /// <summary>
        /// Send the given payload to every transport of the network (multicast) or to the
        /// transport owning the destination address (unicast).
        /// </summary>
        public Task SendAsync(ReadOnlyMemory<Byte>  Payload,
                              IPSocket?             Destination         = null,
                              Int32?                InterfaceIndex      = null,
                              CancellationToken     CancellationToken   = default)
        {

            if (!IsRunning)
                throw new InvalidOperationException("The in-memory Multicast DNS transport is not running!");

            return Network.DeliverAsync(this, Payload, Destination, CancellationToken);

        }

        #endregion

        #region InjectAsync(Payload, RemoteSocket, CancellationToken = default)

        /// <summary>
        /// Deliver a datagram to this transport as if it had been received from the given
        /// socket, e.g. a legacy unicast query from an ephemeral port.
        /// </summary>
        /// <param name="Payload">The payload.</param>
        /// <param name="RemoteSocket">The apparent source of the datagram.</param>
        /// <param name="CancellationToken">A token to cancel the processing.</param>
        public Task InjectAsync(ReadOnlyMemory<Byte>  Payload,
                                IPSocket              RemoteSocket,
                                CancellationToken     CancellationToken   = default)

            => ReceiveAsync(
                   new MulticastDNSDatagram(
                       Payload,
                       RemoteSocket,
                       null,
                       Network.TimeProvider.GetUtcNow()
                   ),
                   CancellationToken
               );

        #endregion

        #region (internal) ReceiveAsync(Datagram, CancellationToken)

        internal Task ReceiveAsync(MulticastDNSDatagram  Datagram,
                                   CancellationToken     CancellationToken)
        {

            if (!IsRunning)
                return Task.CompletedTask;

            return OnDatagramReceived.InvokeAllAsync(
                       handler => handler(Datagram.Timestamp, this, Datagram, CancellationToken),
                       logger
                   );

        }

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// Stop the transport and detach it from the network.
        /// </summary>
        public ValueTask DisposeAsync()
        {

            if (isDisposed)
                return ValueTask.CompletedTask;

            isDisposed  = true;
            isRunning   = false;

            Network.Detach(this);

            return ValueTask.CompletedTask;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this transport.
        /// </summary>
        public override String ToString()

            => $"in-memory mDNS transport {Address}:{Port}{(IsRunning ? "" : " (stopped)")}";

        #endregion

    }

}
