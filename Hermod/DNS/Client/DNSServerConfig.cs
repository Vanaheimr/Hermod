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
    public class DNSServerConfig
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
        /// The query timeout for this DNS server.
        /// </summary>
        public TimeSpan?     QueryTimeout    { get; set; }

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


        #region (private static) DefaultPortFor(Transport)

        /// <summary>
        /// The port a transport uses when none was given.
        /// </summary>
        /// <remarks>
        /// All ten of them. The constructors used to spell this out twice and
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

                   _                          => IPPort.DNS

               };

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

}
