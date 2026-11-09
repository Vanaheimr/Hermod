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

using System.Net.Http;
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The one place Hermod turns a host into the addresses to connect to - and never with the resolver
    /// of the operating system.
    /// </summary>
    /// <remarks>
    /// An application that resolves its names securely - its own name servers, DNS over TLS or HTTPS,
    /// DNSSEC - must not be dialled around them: a name resolved by the operating system is one an
    /// attacker on the local network, or a name server nobody chose, may have answered, and nothing
    /// would say so. So an IP address is itself, "localhost" and every name below it the loopback
    /// addresses, an "invalid" name nothing, a host name what the DNS client given finds for it - and a
    /// host name without a DNS client is refused rather than looked up some other way. The two special-use
    /// names are answered here, without asking the DNS client, as RFC 6761 §6.3 and §6.4 ask of a name
    /// resolution API: a DNS client may be one that carries every query to its server.
    /// </remarks>
    public static class NameResolution
    {

        #region TryLiteral(Host, out Addresses)

        /// <summary>
        /// The addresses a host is without asking anybody: an IPv4 address, an IPv6 address - in brackets
        /// as a URL has it, and with its zone, percent-encoded or not - or "localhost" and every name
        /// below it (RFC 6761 §6.3).
        /// </summary>
        /// <param name="Host">A host.</param>
        /// <param name="Addresses">Its addresses, when it is one of these.</param>
        public static Boolean TryLiteral(String                                     Host,
                                         out IReadOnlyList<System.Net.IPAddress>    Addresses)
        {

            var host = Host.Trim();

            if (IPAddress.IsIPv4(host))
            {
                Addresses = [ IPv4Address.Parse(host).ToDotNet() ];
                return true;
            }

            // As it comes out of a URL, with its brackets, and its zone percent-encoded.
            if (IPv6Address.TryParseURIHost(host, out var ipv6))
            {
                Addresses = [ ipv6.ToDotNet() ];
                return true;
            }

            // An IPv6 address with its zone as written by hand: fe80::1%eth0, fe80::1%14. Nothing with a
            // colon is a host name, so asking .NET's parser here never reaches a name server.
            var unbracketed = host.StartsWith('[') && host.EndsWith(']')
                                  ? host[1..^1]
                                  : host;

            if (unbracketed.Contains(':') &&
                System.Net.IPAddress.TryParse(unbracketed, out var dotNetIPv6) &&
                dotNetIPv6.AddressFamily == AddressFamily.InterNetworkV6)
            {
                Addresses = [ dotNetIPv6 ];
                return true;
            }

            if (IPAddress.IsIPv4Localhost(host))
            {
                Addresses = [ System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback ];
                return true;
            }

            if (IPAddress.IsIPv6Localhost(host))
            {
                Addresses = [ System.Net.IPAddress.IPv6Loopback ];
                return true;
            }

            // RFC 6761 §6.3: "localhost." and every name below it, with the root's dot or without, are
            // the loopback addresses - not only the text "localhost" above.
            if (DNSServiceName.TryParse(host, out var name, out _) &&
                SpecialUseNames.IsLocalhost(name))
            {
                Addresses = [ System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback ];
                return true;
            }

            Addresses = [];
            return false;

        }

        #endregion

        #region ResolveAsync(Host, DNSClient, CancellationToken = default)

        /// <summary>
        /// The addresses of a host to connect to: an IP address is itself, "localhost" the loopback addresses,
        /// and a host name what the given DNS client finds for it - IPv6 first, with the Resolution Delay of
        /// RFC 8305 (see IDNSClient.Query_IPAddressesToConnect). The resolver of the operating system is
        /// never asked.
        /// </summary>
        /// <param name="Host">A host name, an IP address (an IPv6 address in brackets, or with its zone, too), or "localhost".</param>
        /// <param name="DNSClient">The DNS client a host name is resolved with.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <exception cref="NameResolutionException">A host name without a DNS client, no host name at all, an "invalid" name, or one it found no address for.</exception>
        public static async Task<IReadOnlyList<System.Net.IPAddress>> ResolveAsync(String             Host,
                                                                                   IDNSClient?        DNSClient,
                                                                                   CancellationToken  CancellationToken = default)
        {

            if (TryLiteral(Host, out var literal))
                return literal;

            var host = Host.Trim();

            // RFC 6761 §6.4: an "invalid" name has no address, and the DNS client is not asked for one.
            if (DNSServiceName.TryParse(host, out var name, out _) &&
                SpecialUseNames.IsInvalid(name))
                throw new NameResolutionException(Host, $"'{Host}' is an \"invalid\" name, which resolves to no address (RFC 6761 §6.4); no DNS client is asked.");

            if (DNSClient is null)
                throw new NameResolutionException(Host, $"'{Host}' is a host name, and no DNS client was given to resolve it; the resolver of the operating system is not asked.");

            if (!DomainName.TryParse(host, out var domainName, out var notAName))
                throw new NameResolutionException(Host, $"'{Host}' is neither an IP address nor a host name: {notAName}");

            var addresses = (await DNSClient.Query_IPAddressesToConnect(
                                       domainName,
                                       RecursionDesired:   true,
                                       CancellationToken:  CancellationToken
                                   ).ConfigureAwait(false)).
                                Select(address => address.ToDotNet()).
                                ToArray();

            if (addresses.Length == 0)
                throw new NameResolutionException(Host, $"'{Host}' could not be resolved: its name servers named no address for it.");

            return addresses;

        }

        #endregion

        #region ConnectAsync(Socket, Host, Port, DNSClient, CancellationToken = default)

        /// <summary>
        /// Connect a socket to a host, its addresses resolved as <see cref="ResolveAsync"/> does, and tried in turn.
        /// </summary>
        /// <param name="Socket">A socket: for a host of either family, one made with SocketType and ProtocolType only, which is dual-mode.</param>
        /// <param name="Host">A host name, an IP address, or "localhost".</param>
        /// <param name="Port">The port.</param>
        /// <param name="DNSClient">The DNS client a host name is resolved with; an IP address and "localhost" need none.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <exception cref="NameResolutionException">A host name without a DNS client, or one it found no address for.</exception>
        public static async Task ConnectAsync(Socket             Socket,
                                              String             Host,
                                              Int32              Port,
                                              IDNSClient?        DNSClient,
                                              CancellationToken  CancellationToken = default)
        {

            var addresses = await ResolveAsync(Host, DNSClient, CancellationToken).ConfigureAwait(false);

            await Socket.ConnectAsync([.. addresses], Port, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region ConnectAsync(TcpClient, Host, Port, DNSClient, CancellationToken = default)

        /// <summary>
        /// Connect a TCP client to a host, its addresses resolved as <see cref="ResolveAsync"/> does, and tried in turn.
        /// </summary>
        /// <param name="TCPClient">A TCP client.</param>
        /// <param name="Host">A host name, an IP address, or "localhost".</param>
        /// <param name="Port">The port.</param>
        /// <param name="DNSClient">The DNS client a host name is resolved with; an IP address and "localhost" need none.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <exception cref="NameResolutionException">A host name without a DNS client, or one it found no address for.</exception>
        public static async Task ConnectAsync(TcpClient          TCPClient,
                                              String             Host,
                                              Int32              Port,
                                              IDNSClient?        DNSClient,
                                              CancellationToken  CancellationToken = default)
        {

            var addresses = await ResolveAsync(Host, DNSClient, CancellationToken).ConfigureAwait(false);

            await TCPClient.ConnectAsync([.. addresses], Port, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region ConnectCallback(DNSClient, AllowAddress = null)

        /// <summary>
        /// A ConnectCallback for a SocketsHttpHandler that resolves as <see cref="ResolveAsync"/> does: without
        /// one, a SocketsHttpHandler hands the host name to the operating system.
        /// </summary>
        /// <param name="DNSClient">The DNS client host names are resolved with.</param>
        /// <param name="AllowAddress">Which of the addresses may be connected to, or null for any.</param>
        public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>

            ConnectCallback(IDNSClient?                           DNSClient,
                            Func<System.Net.IPAddress, Boolean>?  AllowAddress   = null)

                => async (context, cancellationToken) => {

                       var addresses = await ResolveAsync(context.DnsEndPoint.Host, DNSClient, cancellationToken).ConfigureAwait(false);
                       var allowed   = AllowAddress is null
                                           ? [.. addresses]
                                           : addresses.Where(AllowAddress).ToArray();

                       if (allowed.Length == 0)
                           throw new HttpRequestException($"{context.DnsEndPoint.Host} has no public address ({String.Join(", ", addresses.Select(address => address.ToString()))})");

                       // Either family: a dual-mode socket where there is IPv6.
                       var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

                       try
                       {
                           await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                           return new NetworkStream(socket, ownsSocket: true);
                       }
                       catch
                       {
                           socket.Dispose();
                           throw;
                       }

                   };

        #endregion

        #region HTTPHandler(DNSClient, AllowAddress = null)

        /// <summary>
        /// A SocketsHttpHandler that resolves as <see cref="ResolveAsync"/> does, and goes through no proxy -
        /// which one, and how its own name would be looked up, would be the system's to say.
        /// </summary>
        /// <param name="DNSClient">The DNS client host names are resolved with.</param>
        /// <param name="AllowAddress">Which of the addresses may be connected to, or null for any.</param>
        public static SocketsHttpHandler HTTPHandler(IDNSClient?                           DNSClient,
                                                     Func<System.Net.IPAddress, Boolean>?  AllowAddress   = null)

            => new() {
                   UseProxy         = false,
                   ConnectCallback  = ConnectCallback(DNSClient, AllowAddress)
               };

        #endregion

    }


    /// <summary>
    /// A host that could not be resolved to an address to connect to: a host name without a DNS client to
    /// resolve it with, or one its name servers named no address for.
    /// </summary>
    /// <param name="Host">The host, as it was given.</param>
    /// <param name="Message">What is wrong, as a sentence.</param>
    public class NameResolutionException(String  Host,
                                         String  Message) : IOException(Message)
    {

        /// <summary>
        /// The host, as it was given.
        /// </summary>
        public String Host { get; } = Host;

    }

}
