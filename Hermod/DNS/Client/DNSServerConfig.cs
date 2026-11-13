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
    /// A DNS server configuration.
    /// </summary>
    public class DNSServerConfig : IEquatable<DNSServerConfig>
    {

        #region Properties

        /// <summary>
        /// The domain name of the DNS server.
        /// </summary>
        public DomainName?   DomainName      { get; }

        /// <summary>
        /// The DNS server IP address, or null when this server is known only by
        /// name.
        /// </summary>
        /// <remarks>
        /// Nullable because it genuinely is. A DNS-over-HTTPS or DNS-over-TLS
        /// client created from a URL learns its address when the socket connects
        /// and knows none at all when it never did, and it still has to say where
        /// an answer came from. Those clients used to write RemoteIPAddress! into
        /// this field, so a null sat in a non-nullable property and ToString()
        /// below would have dereferenced it.
        ///
        /// A configuration used to *reach* a server is a different matter: that
        /// one needs an address, and DNSClient.GetOrCreateTransportClient says so
        /// rather than failing somewhere further down.
        /// </remarks>
        public IIPAddress?   IPAddress       { get; }

        /// <summary>
        /// The DNS server port.
        /// </summary>
        public IPPort        Port            { get; }

        /// <summary>
        /// The DNS transport protocol to use (UDP, TCP, ...).
        /// </summary>
        public DNSTransport  Transport       { get; }

        /// <summary>
        /// The query timeout for this DNS server: how long a client gives it,
        /// unless a query brings a timeout of its own. Null leaves it to the
        /// client's QueryTimeout.
        /// </summary>
        public TimeSpan?     QueryTimeout    { get; set; }

        /// <summary>
        /// How the SSH connection of a server asked over <see cref="DNSTransport.SSH"/>
        /// is had: see <see cref="OverSSH(IIPAddress, SSH.Client.SshClientOptions, IPPort?, TimeSpan?, String?)"/>.
        /// Null for every other transport.
        /// </summary>
        public DNSServerSSH? SSH             { get; private init; }

        #endregion

        #region Constructor(s)

        #region DNSServerConfig (            IPAddress, Port = null, Transport = UDP, ...)

        /// <summary>
        /// Create a new DNS server configuration.
        /// </summary>
        /// <param name="IPAddress">The IP address of the DNS server.</param>
        /// <param name="Port">The optional port of the DNS server.</param>
        /// <param name="Transport">The DNS transport protocol to use (UDP, TCP, ...). Default is UDP.</param>
        /// <param name="QueryTimeout">The optional query timeout for this DNS server.</param>
        public DNSServerConfig(IIPAddress     IPAddress,
                               IPPort?        Port           = null,
                               DNSTransport?  Transport      = null,
                               TimeSpan?      QueryTimeout   = null)
        {

            this.IPAddress     = IPAddress;
            this.Transport     = Transport ?? DNSTransport.UDP;
            this.QueryTimeout  = QueryTimeout;

            this.Port          = Port ?? DefaultPortFor(this.Transport);

        }

        #endregion

        #region DNSServerConfig (DomainName, IPAddress, Port = null, Transport = UDP, ...)

        /// <summary>
        /// Create a new DNS server configuration.
        /// </summary>
        /// <param name="DomainName">The domain name of the DNS server.</param>
        /// <param name="IPAddress">The IP address of the DNS server.</param>
        /// <param name="Port">The optional port of the DNS server.</param>
        /// <param name="Transport">The DNS transport protocol to use (UDP, TCP, ...). Default is UDP.</param>
        /// <param name="QueryTimeout">The optional query timeout for this DNS server.</param>
        public DNSServerConfig(DomainName     DomainName,
                               IIPAddress     IPAddress,
                               IPPort?        Port           = null,
                               DNSTransport?  Transport      = null,
                               TimeSpan?      QueryTimeout   = null)
        {

            this.DomainName    = DomainName;
            this.IPAddress     = IPAddress;
            this.Transport     = Transport ?? DNSTransport.UDP;
            this.QueryTimeout  = QueryTimeout;

            this.Port          = Port ?? DefaultPortFor(this.Transport);

            if (Hermod.IPAddress.TryParse(DomainName.ToString(), out var ipAddress))
                this.IPAddress  = ipAddress;

        }

        #endregion

        #region DNSServerConfig (DomainName, Port = null, Transport = UDP, ...)

        /// <summary>
        /// Create a new DNS server configuration for a server known by name
        /// rather than by address.
        /// </summary>
        /// <remarks>
        /// This is what a DNS-over-HTTPS or DNS-over-TLS endpoint is before its
        /// socket connects, and what it stays if the connection never succeeds.
        /// Such a configuration names the origin of an answer; it cannot be used
        /// to open a connection, because there is no address in it.
        /// </remarks>
        /// <param name="DomainName">The domain name of the DNS server.</param>
        /// <param name="Port">The optional port of the DNS server.</param>
        /// <param name="Transport">The DNS transport protocol to use (UDP, TCP, ...). Default is UDP.</param>
        /// <param name="QueryTimeout">The optional query timeout for this DNS server.</param>
        public DNSServerConfig(DomainName     DomainName,
                               IPPort?        Port           = null,
                               DNSTransport?  Transport      = null,
                               TimeSpan?      QueryTimeout   = null)
        {

            this.DomainName    = DomainName;
            this.Transport     = Transport ?? DNSTransport.UDP;
            this.QueryTimeout  = QueryTimeout;
            this.Port          = Port ?? DefaultPortFor(this.Transport);

            if (Hermod.IPAddress.TryParse(DomainName.ToString(), out var ipAddress))
                this.IPAddress  = ipAddress;

        }

        #endregion

        #endregion


        #region (static) OverSSH(...)

        /// <summary>
        /// A DNS server asked over SSH - the subsystem "dns@open.charging.cloud" of the white paper
        /// "DNS over SSH" - on an SSH connection of its own: a server among the others of a
        /// <see cref="DNSClient"/>, as one over UDP, TCP or TLS is.
        /// </summary>
        /// <param name="IPAddress">The IP address of the SSH server.</param>
        /// <param name="SSHOptions">Who signs in, with what, and what the server's host key is held to.</param>
        /// <param name="Port">The SSH port; 22 by default.</param>
        /// <param name="QueryTimeout">An optional timeout of a query to this server.</param>
        /// <param name="Subsystem">The subsystem, where it is not "dns@open.charging.cloud".</param>
        public static DNSServerConfig OverSSH(IIPAddress                    IPAddress,
                                              SSH.Client.SshClientOptions   SSHOptions,
                                              IPPort?                       Port           = null,
                                              TimeSpan?                     QueryTimeout   = null,
                                              String?                       Subsystem      = null)

            => new (IPAddress, Port, DNSTransport.SSH, QueryTimeout) {
                   SSH = new DNSServerSSH(SSHOptions, null, Subsystem)
               };

        /// <summary>
        /// A DNS server asked over SSH on an SSH connection of its own, dialled by its name.
        /// </summary>
        /// <param name="DomainName">The name of the SSH server - which another server has to resolve.</param>
        /// <param name="SSHOptions">Who signs in, with what, and what the server's host key is held to.</param>
        /// <param name="Port">The SSH port; 22 by default.</param>
        /// <param name="QueryTimeout">An optional timeout of a query to this server.</param>
        /// <param name="Subsystem">The subsystem, where it is not "dns@open.charging.cloud".</param>
        public static DNSServerConfig OverSSH(DomainName                    DomainName,
                                              SSH.Client.SshClientOptions   SSHOptions,
                                              IPPort?                       Port           = null,
                                              TimeSpan?                     QueryTimeout   = null,
                                              String?                       Subsystem      = null)

            => new (DomainName, Port, DNSTransport.SSH, QueryTimeout) {
                   SSH = new DNSServerSSH(SSHOptions, null, Subsystem)
               };

        /// <summary>
        /// A DNS server asked over SSH on a connection somebody else keeps: the queries are one more
        /// channel of it, and it is never closed for them. What
        /// <see cref="SSH.Client.SshSharedConnection.GetAsync"/> hands out, say.
        /// </summary>
        /// <param name="IPAddress">The IP address of the SSH server, which names the server among the others.</param>
        /// <param name="Connection">Hands out the connection: asked at the first query, and again once the connection it handed out has ended.</param>
        /// <param name="Port">The SSH port of that connection; 22 by default.</param>
        /// <param name="QueryTimeout">An optional timeout of a query to this server.</param>
        /// <param name="Subsystem">The subsystem, where it is not "dns@open.charging.cloud".</param>
        public static DNSServerConfig OverSSH(IIPAddress                                                IPAddress,
                                              Func<CancellationToken, ValueTask<SSH.Client.SshClient>>  Connection,
                                              IPPort?                                                   Port           = null,
                                              TimeSpan?                                                 QueryTimeout   = null,
                                              String?                                                   Subsystem      = null)

            => new (IPAddress, Port, DNSTransport.SSH, QueryTimeout) {
                   SSH = new DNSServerSSH(null, Connection, Subsystem)
               };

        /// <summary>
        /// A DNS server asked over SSH on a connection somebody else keeps, named by the name of its host.
        /// </summary>
        /// <param name="DomainName">The name of the SSH server, which names the server among the others. It is not resolved: the connection is there, or is made by whoever keeps it.</param>
        /// <param name="Connection">Hands out the connection: asked at the first query, and again once the connection it handed out has ended.</param>
        /// <param name="Port">The SSH port of that connection; 22 by default.</param>
        /// <param name="QueryTimeout">An optional timeout of a query to this server.</param>
        /// <param name="Subsystem">The subsystem, where it is not "dns@open.charging.cloud".</param>
        public static DNSServerConfig OverSSH(DomainName                                                DomainName,
                                              Func<CancellationToken, ValueTask<SSH.Client.SshClient>>  Connection,
                                              IPPort?                                                   Port           = null,
                                              TimeSpan?                                                 QueryTimeout   = null,
                                              String?                                                   Subsystem      = null)

            => new (DomainName, Port, DNSTransport.SSH, QueryTimeout) {
                   SSH = new DNSServerSSH(null, Connection, Subsystem)
               };

        #endregion

        #region (private static) DefaultPortFor(Transport)

        /// <summary>
        /// The port a transport uses when none was given.
        /// </summary>
        /// <remarks>
        /// All eleven of them. The constructors used to spell this out twice and
        /// each time covered six, so HTTP, HTTP_Binary, HTTP_JSON and HTTPS_GET
        /// fell through with Port left at its default - port 0 - and HTTPS_GET is
        /// one DNSClient.GetOrCreateTransportClient dispatches on.
        /// </remarks>
        private static IPPort DefaultPortFor(DNSTransport Transport)

            => Transport switch {

                   DNSTransport.UDP           => IPPort.DNS,
                   DNSTransport.TCP           => IPPort.DNS,

                   DNSTransport.TLS           => IPPort.DNS_TLS,

                   DNSTransport.HTTP          => IPPort.HTTP,
                   DNSTransport.HTTP_Binary   => IPPort.HTTP,
                   DNSTransport.HTTP_JSON     => IPPort.HTTP,

                   DNSTransport.HTTPS         => IPPort.HTTPS,
                   DNSTransport.HTTPS_Binary  => IPPort.HTTPS,
                   DNSTransport.HTTPS_JSON    => IPPort.HTTPS,
                   DNSTransport.HTTPS_GET     => IPPort.HTTPS,

                   DNSTransport.SSH           => IPPort.SSH,

                   _                          => IPPort.DNS

               };

        #endregion

        #region Equals(DNSServerConfig) / Equals(Object) / GetHashCode()

        /// <summary>
        /// The same server: the same transport to the same address, or name, on
        /// the same port.
        /// </summary>
        /// <remarks>
        /// DNSClient keeps its servers in a set and fills it from every network
        /// interface, where one server is listed once per interface. Compared by
        /// reference, as a class is unless it says otherwise, each of those was
        /// a server of its own: a Windows machine listed its router's address
        /// twice and fec0:0:0:ffff::1 to ::3 three times each, and asked each of
        /// them again whenever a query went unanswered. An address with another
        /// scope is the same server here, because IPv6Address compares without
        /// one.
        ///
        /// The timeout is left out. It says how long to wait for a server rather
        /// than which one, and it can be set after the config has gone into a
        /// set - which a hash code must never follow.
        /// </remarks>
        /// <param name="DNSServerConfig">A DNS server configuration to compare with.</param>
        public Boolean Equals(DNSServerConfig? DNSServerConfig)

            => DNSServerConfig is not null &&
               Transport == DNSServerConfig.Transport &&
               Port      == DNSServerConfig.Port      &&
               Equals(IPAddress,  DNSServerConfig.IPAddress) &&
               Equals(DomainName, DNSServerConfig.DomainName);

        /// <summary>
        /// Whether the given object is a configuration of the same DNS server.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DNSServerConfig dnsServerConfig &&
               Equals(dnsServerConfig);

        /// <summary>
        /// The hash code of the server this configures; the timeout is not part
        /// of it, see <see cref="Equals(DNSServerConfig)"/>.
        /// </summary>
        public override Int32 GetHashCode()

            => HashCode.Combine(
                   Transport,
                   Port,
                   IPAddress,
                   DomainName
               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"{Transport.ToString().ToLower()}://{Host}:{Port}",

                   QueryTimeout.HasValue
                       ? $", timeout: {Math.Round(QueryTimeout.Value.TotalSeconds)} sec."
                       : ""

               );

        /// <summary>
        /// Whatever is known about where this server is: its name, or its
        /// address, or - for a client whose connection never came up - neither.
        /// </summary>
        private String Host
        {
            get
            {

                if (DomainName is not null)
                    return DomainName.ToString();

                if (IPAddress is null)
                    return "<unknown>";

                // RFC 3986 §3.2.2: brackets, or the colon before the port is
                // indistinguishable from the ones inside the address.
                return IPAddress.ToIPLiteral();

            }
        }

        #endregion

    }


    /// <summary>
    /// How the SSH connection of a DNS server asked over SSH is had: dialled with the given options,
    /// or handed out by whoever keeps it.
    /// </summary>
    /// <param name="Options">Who signs in, with what, and what the server's host key is held to - for a connection of the server's own.</param>
    /// <param name="Connection">Hands out a connection somebody else keeps, which is never closed for DNS.</param>
    /// <param name="Subsystem">The subsystem, where it is not "dns@open.charging.cloud".</param>
    public sealed record DNSServerSSH(SSH.Client.SshClientOptions?                               Options,
                                      Func<CancellationToken, ValueTask<SSH.Client.SshClient>>?  Connection,
                                      String?                                                    Subsystem = null);

}
