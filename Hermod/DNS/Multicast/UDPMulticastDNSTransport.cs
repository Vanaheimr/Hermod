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
using System.Net.NetworkInformation;

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Options of the UDP Multicast DNS transport.
    /// </summary>
    public sealed class UDPMulticastDNSTransportOptions
    {

        /// <summary>
        /// Whether to join the IPv4 group 224.0.0.251 (default: true).
        /// </summary>
        public Boolean               EnableIPv4                  { get; init; } = true;

        /// <summary>
        /// Whether to join the IPv6 group ff02::fb (default: true; interfaces refusing the join are skipped).
        /// </summary>
        public Boolean               EnableIPv6                  { get; init; } = true;

        /// <summary>
        /// The UDP port (default: 5353). Tests may pick another port to stay invisible to the host's own responder.
        /// </summary>
        public IPPort                Port                        { get; init; } = MulticastDNS.Port;

        /// <summary>
        /// An optional list of network interface names (or ids) to use (default: every interface that is up and supports multicast).
        /// </summary>
        public IEnumerable<String>?  InterfaceNames              { get; init; }

        /// <summary>
        /// Whether to use the loopback interface as well (default: false).
        /// </summary>
        public Boolean               IncludeLoopbackInterface    { get; init; } = false;

        /// <summary>
        /// Whether multicasts of this host are delivered to its own sockets (default: true;
        /// required when responder and client of one host shall see each other).
        /// </summary>
        public Boolean               MulticastLoopback           { get; init; } = true;

        /// <summary>
        /// Whether IPv6 link-local addresses are listed within LocalAddresses (default: false,
        /// because they need an interface scope to be useful to a client).
        /// </summary>
        public Boolean               IncludeLinkLocalIPv6        { get; init; } = false;

        /// <summary>
        /// Whether an explicit list of local addresses replaces the interface addresses (default: null).
        /// </summary>
        public IEnumerable<IIPAddress>?  LocalAddresses          { get; init; }

    }


    /// <summary>
    /// The UDP transport of Multicast DNS (RFC 6762 §2, §3, §11): one socket per address
    /// family bound to the Multicast DNS port with address reuse, joined to the groups on
    /// every selected interface, multicast loopback enabled, IP time-to-live 255.
    /// </summary>
    public sealed class UDPMulticastDNSTransport : IMulticastDNSTransport
    {

        #region Data

        private sealed record InterfaceInfo(Int32                Index,
                                            String               Name,
                                            IReadOnlyList<System.Net.IPAddress>  IPv4Addresses,
                                            IReadOnlyList<System.Net.IPAddress>  IPv6Addresses);

        private readonly ILogger?                  logger;
        private readonly TimeProvider              timeProvider;
        private readonly SemaphoreSlim             sendLock          = new(1, 1);
        private readonly List<InterfaceInfo>       interfaces        = [];
        private readonly List<Task>                receiveLoops      = [];
        private          Socket?                   socket4;
        private          Socket?                   socket6;
        private          CancellationTokenSource?  cancellationTokenSource;
        private          IReadOnlyList<IIPAddress> localAddresses    = [];
        private          Boolean                   isDisposed;

        private static readonly IPEndPoint anyV4 = new(System.Net.IPAddress.Any,     0);
        private static readonly IPEndPoint anyV6 = new(System.Net.IPAddress.IPv6Any, 0);

        #endregion

        #region Properties

        /// <summary>
        /// The options of this transport.
        /// </summary>
        public UDPMulticastDNSTransportOptions  Options           { get; }

        /// <summary>
        /// The Multicast DNS port of this transport.
        /// </summary>
        public IPPort                           Port
            => Options.Port;

        /// <summary>
        /// Whether this transport is running.
        /// </summary>
        public Boolean                          IsRunning
            => cancellationTokenSource is not null && !cancellationTokenSource.IsCancellationRequested;

        /// <summary>
        /// The unicast addresses of the selected interfaces (or the configured local addresses).
        /// </summary>
        public IReadOnlyList<IIPAddress>        LocalAddresses
            => localAddresses;

        /// <summary>
        /// The indexes of the selected network interfaces.
        /// </summary>
        public IReadOnlyList<Int32>             InterfaceIndexes
            => [.. interfaces.Select(info => info.Index)];

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever a datagram was received.
        /// </summary>
        public event OnMulticastDNSDatagramReceivedDelegate?  OnDatagramReceived;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new UDP Multicast DNS transport.
        /// </summary>
        /// <param name="Options">Optional transport options.</param>
        /// <param name="TimeProvider">An optional time provider.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        public UDPMulticastDNSTransport(UDPMulticastDNSTransportOptions?  Options         = null,
                                        TimeProvider?                     TimeProvider    = null,
                                        ILoggerFactory?                   LoggerFactory   = null)
        {

            this.Options       = Options      ?? new UDPMulticastDNSTransportOptions();
            this.timeProvider  = TimeProvider ?? System.TimeProvider.System;
            this.logger        = LoggerFactory?.CreateLogger<UDPMulticastDNSTransport>();

        }

        #endregion


        #region StartAsync(CancellationToken = default)

        /// <summary>
        /// Open the sockets, join the multicast groups and start receiving.
        /// </summary>
        public Task StartAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (IsRunning)
                return Task.CompletedTask;

            interfaces.Clear();
            interfaces.AddRange(SelectInterfaces());

            if (interfaces.Count == 0)
                throw new InvalidOperationException("No network interface supporting multicast was found!");

            localAddresses = Options.LocalAddresses is not null
                                 ? [.. Options.LocalAddresses]
                                 : [.. interfaces.SelectMany(info => info.IPv4Addresses.Select(ToHermod)).
                                        Concat(interfaces.SelectMany(info => info.IPv6Addresses.
                                                                                  Where(address => Options.IncludeLinkLocalIPv6 || !address.IsIPv6LinkLocal).
                                                                                  Select(ToHermod)))];

            var port = Options.Port.ToInt32();

            if (Options.EnableIPv4)
            {

                socket4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
                              ExclusiveAddressUse = false
                          };

                socket4.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,        true);
                socket4.Bind(new IPEndPoint(System.Net.IPAddress.Any, port));
                socket4.SetSocketOption(SocketOptionLevel.IP,     SocketOptionName.MulticastTimeToLive, MulticastDNS.MulticastTimeToLive);
                socket4.SetSocketOption(SocketOptionLevel.IP,     SocketOptionName.MulticastLoopback,   Options.MulticastLoopback);
                socket4.SetSocketOption(SocketOptionLevel.IP,     SocketOptionName.PacketInformation,   true);
                DisableConnectionReset(socket4);

                var joined = 0;

                foreach (var info in interfaces)
                {
                    foreach (var address in info.IPv4Addresses)
                    {
                        try
                        {
                            socket4.SetSocketOption(SocketOptionLevel.IP,
                                                    SocketOptionName.AddMembership,
                                                    new MulticastOption(MulticastDNS.IPv4Group, address));
                            joined++;
                        }
                        catch (SocketException e)
                        {
                            logger?.LogWarning("Multicast DNS: joining {Group} on interface {Interface} ({Address}) failed: {Error}",
                                               MulticastDNS.IPv4Group, info.Name, address, e.SocketErrorCode);
                        }
                    }
                }

                if (joined == 0)
                    logger?.LogWarning("Multicast DNS: the IPv4 group could not be joined on any interface!");

            }

            if (Options.EnableIPv6)
            {

                try
                {

                    socket6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp) {
                                  ExclusiveAddressUse = false,
                                  DualMode            = false
                              };

                    socket6.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,        true);
                    socket6.Bind(new IPEndPoint(System.Net.IPAddress.IPv6Any, port));
                    socket6.SetSocketOption(SocketOptionLevel.IPv6,   SocketOptionName.MulticastTimeToLive, MulticastDNS.MulticastTimeToLive);
                    socket6.SetSocketOption(SocketOptionLevel.IPv6,   SocketOptionName.MulticastLoopback,   Options.MulticastLoopback);
                    socket6.SetSocketOption(SocketOptionLevel.IPv6,   SocketOptionName.PacketInformation,   true);
                    DisableConnectionReset(socket6);

                    var joined = 0;

                    foreach (var info in interfaces.Where(info => info.IPv6Addresses.Count > 0))
                    {
                        try
                        {
                            socket6.SetSocketOption(SocketOptionLevel.IPv6,
                                                    SocketOptionName.AddMembership,
                                                    new IPv6MulticastOption(MulticastDNS.IPv6Group, info.Index));
                            joined++;
                        }
                        catch (SocketException e)
                        {
                            logger?.LogWarning("Multicast DNS: joining {Group} on interface {Interface} failed: {Error}",
                                               MulticastDNS.IPv6Group, info.Name, e.SocketErrorCode);
                        }
                    }

                    if (joined == 0)
                    {
                        logger?.LogWarning("Multicast DNS: the IPv6 group could not be joined on any interface, IPv6 is disabled!");
                        socket6.Dispose();
                        socket6 = null;
                    }

                }
                catch (SocketException e)
                {
                    logger?.LogWarning("Multicast DNS: IPv6 is not available ({Error}), IPv6 is disabled!", e.SocketErrorCode);
                    socket6?.Dispose();
                    socket6 = null;
                }

            }

            if (socket4 is null && socket6 is null)
                throw new InvalidOperationException("No Multicast DNS socket could be opened!");

            cancellationTokenSource = new CancellationTokenSource();

            if (socket4 is not null)
                receiveLoops.Add(Task.Run(() => ReceiveLoopAsync(socket4, anyV4, cancellationTokenSource.Token), CancellationToken.None));

            if (socket6 is not null)
                receiveLoops.Add(Task.Run(() => ReceiveLoopAsync(socket6, anyV6, cancellationTokenSource.Token), CancellationToken.None));

            logger?.LogInformation("Multicast DNS transport started on port {Port} using {Interfaces}",
                                   Options.Port,
                                   String.Join(", ", interfaces.Select(info => info.Name)));

            return Task.CompletedTask;

        }

        #endregion

        #region StopAsync(CancellationToken = default)

        /// <summary>
        /// Stop receiving and close the sockets.
        /// </summary>
        public async Task StopAsync(CancellationToken CancellationToken = default)
        {

            var cts = cancellationTokenSource;
            if (cts is null)
                return;

            cancellationTokenSource = null;

            await cts.CancelAsync().ConfigureAwait(false);

            socket4?.Dispose();
            socket6?.Dispose();

            try
            {
                await Task.WhenAll(receiveLoops).ConfigureAwait(false);
            }
            catch
            { }

            receiveLoops.Clear();
            socket4 = null;
            socket6 = null;
            cts.Dispose();

        }

        #endregion

        #region SendAsync(Payload, Destination = null, InterfaceIndex = null, CancellationToken = default)

        /// <summary>
        /// Send the given payload to the multicast groups on every (or the given) interface,
        /// or to the given unicast destination.
        /// </summary>
        public async Task SendAsync(ReadOnlyMemory<Byte>  Payload,
                                    IPSocket?             Destination         = null,
                                    Int32?                InterfaceIndex      = null,
                                    CancellationToken     CancellationToken   = default)
        {

            if (!IsRunning)
                throw new InvalidOperationException("The UDP Multicast DNS transport is not running!");

            await sendLock.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                if (Destination.HasValue)
                {

                    var endPoint = Destination.Value.ToIPEndPoint();
                    var socket   = endPoint.AddressFamily == AddressFamily.InterNetworkV6 ? socket6 : socket4;

                    if (socket is null)
                    {
                        logger?.LogWarning("Multicast DNS: no socket for a unicast to {Destination}", Destination);
                        return;
                    }

                    await socket.SendToAsync(Payload, SocketFlags.None, endPoint, CancellationToken).ConfigureAwait(false);
                    return;

                }

                var port = Options.Port.ToInt32();

                foreach (var info in interfaces.Where(info => !InterfaceIndex.HasValue || info.Index == InterfaceIndex.Value))
                {

                    if (socket4 is not null)
                    {
                        foreach (var address in info.IPv4Addresses)
                        {
                            try
                            {
                                socket4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, address.GetAddressBytes());
                                await socket4.SendToAsync(Payload, SocketFlags.None, new IPEndPoint(MulticastDNS.IPv4Group, port), CancellationToken).ConfigureAwait(false);
                            }
                            catch (SocketException e)
                            {
                                logger?.LogDebug("Multicast DNS: sending via {Interface} ({Address}) failed: {Error}", info.Name, address, e.SocketErrorCode);
                            }
                        }
                    }

                    if (socket6 is not null && info.IPv6Addresses.Count > 0)
                    {
                        try
                        {
                            socket6.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface, info.Index);
                            await socket6.SendToAsync(Payload,
                                                      SocketFlags.None,
                                                      new IPEndPoint(new System.Net.IPAddress(((System.Net.IPAddress) MulticastDNS.IPv6Group).GetAddressBytes(), info.Index), port),
                                                      CancellationToken).ConfigureAwait(false);
                        }
                        catch (SocketException e)
                        {
                            logger?.LogDebug("Multicast DNS: sending via {Interface} (IPv6) failed: {Error}", info.Name, e.SocketErrorCode);
                        }
                    }

                }

            }
            finally
            {
                sendLock.Release();
            }

        }

        #endregion


        #region (private) ReceiveLoopAsync(Socket, AnyEndPoint, CancellationToken)

        private async Task ReceiveLoopAsync(Socket             Socket,
                                            IPEndPoint         AnyEndPoint,
                                            CancellationToken  CancellationToken)
        {

            var buffer = new Byte[MulticastDNS.MaxPacketSize];

            while (!CancellationToken.IsCancellationRequested)
            {

                try
                {

                    var result     = await Socket.ReceiveMessageFromAsync(buffer, SocketFlags.None, AnyEndPoint, CancellationToken).ConfigureAwait(false);

                    if (result.ReceivedBytes == 0)
                        continue;

                    var payload    = buffer.AsMemory(0, result.ReceivedBytes).ToArray();
                    var remote     = IPSocket.FromIPEndPoint((IPEndPoint) result.RemoteEndPoint);
                    var timestamp  = timeProvider.GetUtcNow();

                    await OnDatagramReceived.InvokeAllAsync(
                              handler => handler(
                                             timestamp,
                                             this,
                                             new MulticastDNSDatagram(
                                                 payload,
                                                 remote,
                                                 result.PacketInformation.Interface,
                                                 timestamp
                                             ),
                                             CancellationToken
                                         ),
                              logger
                          ).ConfigureAwait(false);

                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException e) when (e.SocketErrorCode is SocketError.ConnectionReset or
                                                                     SocketError.MessageSize)
                {
                    // An ICMP port unreachable of an earlier unicast, or an oversized datagram: keep listening.
                    logger?.LogDebug("Multicast DNS: ignoring socket error {Error}", e.SocketErrorCode);
                }
                catch (SocketException e)
                {
                    if (CancellationToken.IsCancellationRequested)
                        break;
                    logger?.LogWarning("Multicast DNS: receive failed with {Error}", e.SocketErrorCode);
                }
                catch (Exception e)
                {
                    logger?.LogError(e, "Multicast DNS: receive loop failed");
                }

            }

        }

        #endregion

        #region (private) SelectInterfaces()

        private List<InterfaceInfo> SelectInterfaces()
        {

            var selected = new List<InterfaceInfo>();
            var wanted   = Options.InterfaceNames?.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {

                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (!networkInterface.SupportsMulticast)
                    continue;

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback && !Options.IncludeLoopbackInterface)
                    continue;

                if (wanted is not null &&
                    !wanted.Contains(networkInterface.Name) &&
                    !wanted.Contains(networkInterface.Id))
                {
                    continue;
                }

                var properties     = networkInterface.GetIPProperties();
                var ipv4Addresses  = new List<System.Net.IPAddress>();
                var ipv6Addresses  = new List<System.Net.IPAddress>();

                foreach (var unicast in properties.UnicastAddresses)
                {

                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        ipv4Addresses.Add(unicast.Address);

                    else if (unicast.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        ipv6Addresses.Add(unicast.Address);

                }

                if (ipv4Addresses.Count == 0 && ipv6Addresses.Count == 0)
                    continue;

                var index = -1;

                try
                {
                    index = ipv4Addresses.Count > 0
                                ? properties.GetIPv4Properties().Index
                                : properties.GetIPv6Properties().Index;
                }
                catch
                {
                    try { index = properties.GetIPv6Properties().Index; } catch { }
                }

                if (index < 0)
                    continue;

                selected.Add(new InterfaceInfo(index, networkInterface.Name, ipv4Addresses, ipv6Addresses));

            }

            return selected;

        }

        #endregion

        #region (private static) ToHermod(Address)

        private static IIPAddress ToHermod(System.Net.IPAddress Address)

            => Address.AddressFamily == AddressFamily.InterNetworkV6
                   ? IPv6Address.From(Address)
                   : IPv4Address.From(Address);

        #endregion

        #region (private static) DisableConnectionReset(Socket)

        private static void DisableConnectionReset(Socket Socket)
        {

            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                // SIO_UDP_CONNRESET: an ICMP port unreachable must not close the socket.
                Socket.IOControl(-1744830452, [ 0, 0, 0, 0 ], null);
            }
            catch
            { }

        }

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Stop the transport and release the sockets.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (isDisposed)
                return;

            isDisposed = true;

            await StopAsync().ConfigureAwait(false);

            sendLock.Dispose();

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this transport.
        /// </summary>
        public override String ToString()

            => $"UDP mDNS transport on port {Options.Port}{(IsRunning ? $" ({interfaces.Count} interface(s))" : " (stopped)")}";

        #endregion

    }

}
