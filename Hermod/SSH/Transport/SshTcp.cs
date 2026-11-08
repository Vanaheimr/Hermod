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
using System.IO.Pipelines;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Adapts TCP sockets to the <see cref="IDuplexPipe"/> the SSH transport is written against.
    /// The public surface uses Hermod's networking types (<see cref="IPSocket"/>, <see cref="IPPort"/>,
    /// <see cref="IIPAddress"/>); the conversion to the <c>System.Net</c> socket types happens here at
    /// the lowest layer. IPv6 is first-class: an IPv6 endpoint yields a dual-stack listener.
    /// </summary>
    public static class SshTcp
    {

        #region AsDuplexPipe(Socket)

        /// <summary>
        /// Wrap a connected socket as a duplex pipe. The pipe owns the socket and closes it on completion.
        /// </summary>
        public static IDuplexPipe AsDuplexPipe(Socket Socket)
        {
            var stream = new NetworkStream(Socket, ownsSocket: true);
            return new DuplexPipe(PipeReader.Create(stream), PipeWriter.Create(stream)) {
                       Abort         = stream.Dispose,
                       LocalSocket   = Socket.LocalEndPoint  is IPEndPoint local  ? IPSocket.FromIPEndPoint(local)  : null,
                       RemoteSocket  = Socket.RemoteEndPoint is IPEndPoint remote ? IPSocket.FromIPEndPoint(remote) : null
                   };
        }

        #endregion

        #region ConnectAsync(RemoteSocket, CancellationToken = default)

        /// <summary>
        /// Connect to a remote IP socket (IPv4 or IPv6) and return the connection as a duplex pipe.
        /// </summary>
        public static async Task<IDuplexPipe> ConnectAsync(IPSocket           RemoteSocket,
                                                           CancellationToken  CancellationToken = default)
        {

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(RemoteSocket.IPAddress.ToDotNet(), RemoteSocket.Port.ToInt32(), CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            return AsDuplexPipe(socket);

        }

        #endregion

        #region ConnectAsync(Host, Port, DNSClient, CancellationToken = default)

        /// <summary>
        /// Connect to a host name or IP address and port and return the connection as a duplex pipe. A host
        /// name is resolved with the given DNS client, and never with the resolver of the operating system:
        /// see <see cref="ResolveAsync"/>. Its addresses are tried in turn, IPv6 first.
        /// </summary>
        /// <param name="Host">A host name, an IP address, or "localhost".</param>
        /// <param name="Port">The port.</param>
        /// <param name="DNSClient">The DNS client a host name is resolved with; an IP address and "localhost" need none.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <exception cref="SshNameResolutionException">A host name without a DNS client, or one it found no address for.</exception>
        public static async Task<IDuplexPipe> ConnectAsync(String             Host,
                                                           IPPort             Port,
                                                           IDNSClient?        DNSClient,
                                                           CancellationToken  CancellationToken = default)
        {

            var addresses  = await ResolveAsync(Host, DNSClient, CancellationToken).ConfigureAwait(false);

            var socket     = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync([.. addresses], Port.ToInt32(), CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            return AsDuplexPipe(socket);

        }

        #endregion

        #region ResolveAsync(Host, DNSClient, CancellationToken = default)

        /// <summary>
        /// The addresses of a host to connect to: an IP address is itself, "localhost" the loopback addresses,
        /// and a host name what the given DNS client finds for it - IPv6 first, with the Resolution Delay of
        /// RFC 8305 (see IDNSClient.Query_IPAddressesToConnect).
        /// </summary>
        /// <remarks>
        /// The resolver of the operating system is never asked. A node whose names are resolved securely - its
        /// own name servers, DNS over TLS or HTTPS, DNSSEC - must not be dialled around them: a name resolved
        /// by the operating system is one an attacker on the local network, or a name server nobody chose, may
        /// have answered. So a host name without a DNS client is refused rather than looked up some other way.
        /// </remarks>
        /// <param name="Host">A host name, an IP address (an IPv6 address in brackets, too), or "localhost".</param>
        /// <param name="DNSClient">The DNS client a host name is resolved with.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <exception cref="SshNameResolutionException">A host name without a DNS client, no host name at all, or one it found no address for.</exception>
        public static async Task<IReadOnlyList<System.Net.IPAddress>> ResolveAsync(String             Host,
                                                                                   IDNSClient?        DNSClient,
                                                                                   CancellationToken  CancellationToken = default)
        {

            var host = Host.Trim();

            if (IPAddress.IsIPv4(host))
                return [ IPv4Address.Parse(host).ToDotNet() ];

            // As it comes out of a URL, with its brackets, and its zone percent-encoded.
            if (IPv6Address.TryParseURIHost(host, out var ipv6))
                return [ ipv6.ToDotNet() ];

            if (IPAddress.IsIPv4Localhost(host))
                return [ System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback ];

            if (IPAddress.IsIPv6Localhost(host))
                return [ System.Net.IPAddress.IPv6Loopback ];

            if (DNSClient is null)
                throw new SshNameResolutionException(Host, $"'{Host}' is a host name, and no DNS client was given to resolve it; the resolver of the operating system is not asked.");

            if (!DomainName.TryParse(host, out var domainName, out var notAName))
                throw new SshNameResolutionException(Host, $"'{Host}' is neither an IP address nor a host name: {notAName}");

            var addresses = (await DNSClient.Query_IPAddressesToConnect(
                                       domainName,
                                       RecursionDesired:   true,
                                       CancellationToken:  CancellationToken
                                   ).ConfigureAwait(false)).
                                Select(address => address.ToDotNet()).
                                ToArray();

            if (addresses.Length == 0)
                throw new SshNameResolutionException(Host, $"'{Host}' could not be resolved: its name servers named no address for it.");

            return addresses;

        }

        #endregion

        #region ResolverOf(DNSClient)

        /// <summary>
        /// A resolver of forwarding targets that resolves as <see cref="ResolveAsync"/> does: host names with
        /// the given DNS client - or, without one, none at all.
        /// </summary>
        /// <param name="DNSClient">The DNS client host names are resolved with.</param>
        public static SshAddressResolver ResolverOf(IDNSClient? DNSClient)

            => async (host, cancellationToken) => await ResolveAsync(host, DNSClient, cancellationToken).ConfigureAwait(false);

        #endregion

    }


    /// <summary>
    /// A host that could not be resolved to an address to connect to: a host name without a DNS client to
    /// resolve it with, or one its name servers named no address for.
    /// </summary>
    /// <param name="Host">The host, as it was given.</param>
    /// <param name="Message">What is wrong, as a sentence.</param>
    public sealed class SshNameResolutionException(String  Host,
                                                   String  Message) : IOException(Message)
    {

        /// <summary>
        /// The host, as it was given.
        /// </summary>
        public String Host { get; } = Host;

    }


    /// <summary>
    /// A minimal TCP listener that hands each accepted connection to the SSH transport as an
    /// <see cref="IDuplexPipe"/>. Bind to an IPv6 endpoint to get a dual-stack listener.
    /// </summary>
    public sealed class SshTcpListener : IDisposable
    {

        #region Data

        private readonly Socket socket;

        #endregion

        #region Properties

        /// <summary>
        /// The local socket the listener is bound to (with the actual port when 0 was requested).
        /// </summary>
        public IPSocket LocalEndPoint
            => IPSocket.FromIPEndPoint((IPEndPoint) socket.LocalEndPoint!);

        #endregion

        #region Constructor(s)

        private SshTcpListener(Socket Socket)
        {
            this.socket = Socket;
        }

        #endregion


        #region (static) Start(EndPoint, Backlog = 32)

        /// <summary>
        /// Start listening on the given IP socket. An IPv6 address enables dual-stack mode (IPv4 and IPv6).
        /// Use <see cref="IPPort.Auto"/> (port 0) to let the OS choose a free port (read it back from
        /// <see cref="LocalEndPoint"/>).
        /// </summary>
        /// <param name="EndPoint">The IP socket to bind to.</param>
        /// <param name="Backlog">The listen backlog.</param>
        public static SshTcpListener Start(IPSocket EndPoint, Int32 Backlog = 32)
        {

            var dotNetAddress  = EndPoint.IPAddress.ToDotNet();
            var socket         = new Socket(dotNetAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            if (dotNetAddress.AddressFamily == AddressFamily.InterNetworkV6)
                socket.DualMode = true;   // accept IPv4-mapped connections too

            socket.Bind(new IPEndPoint(dotNetAddress, EndPoint.Port.ToInt32()));
            socket.Listen(Backlog);

            return new SshTcpListener(socket);

        }

        #endregion

        #region AcceptAsync(CancellationToken = default)

        /// <summary>
        /// Accept the next incoming connection as a duplex pipe.
        /// </summary>
        public async Task<IDuplexPipe> AcceptAsync(CancellationToken CancellationToken = default)
            => (await AcceptWithPeerAsync(CancellationToken).ConfigureAwait(false)).Pipe;

        /// <summary>
        /// Accept the next incoming connection, keeping the peer's address alongside the pipe. Needed
        /// wherever an authorization decision depends on <i>where</i> the client connected from — a
        /// certificate's <c>source-address</c> restriction, or address-based auditing.
        /// </summary>
        /// <param name="CancellationToken">An optional token to cancel the accept.</param>
        public async Task<(IDuplexPipe Pipe, IPSocket? Peer)> AcceptWithPeerAsync(CancellationToken CancellationToken = default)
        {

            var (pipe, peer, _) = await AcceptWithEndpointsAsync(CancellationToken).ConfigureAwait(false);

            return (pipe, peer);

        }

        /// <summary>
        /// Accept the next incoming connection, keeping both of its ends alongside the pipe: where the
        /// client connected from, and where it connected to. A listener on every address
        /// (<c>0.0.0.0</c>, <c>::</c>) knows only that; the connection knows which of the machine's
        /// addresses - which network - it came in on.
        /// </summary>
        /// <param name="CancellationToken">An optional token to cancel the accept.</param>
        public async Task<(IDuplexPipe Pipe, IPSocket? Peer, IPSocket? Local)> AcceptWithEndpointsAsync(CancellationToken CancellationToken = default)
        {

            var client = await socket.AcceptAsync(CancellationToken).ConfigureAwait(false);
            client.NoDelay = true;

            var peer  = client.RemoteEndPoint is IPEndPoint remote
                            ? IPSocket.FromIPEndPoint(remote)
                            : (IPSocket?) null;

            var local = client.LocalEndPoint  is IPEndPoint here
                            ? IPSocket.FromIPEndPoint(here)
                            : (IPSocket?) null;

            return (SshTcp.AsDuplexPipe(client), peer, local);

        }

        #endregion

        #region Dispose()

        /// <summary>
        /// Stop listening.
        /// </summary>
        public void Dispose()
            => socket.Dispose();

        #endregion

    }

}
