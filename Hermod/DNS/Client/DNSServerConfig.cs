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
        /// The DNS server IP address.
        /// </summary>
        public IIPAddress    IPAddress       { get; }

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

            if (this.Transport == DNSTransport.UDP ||
                this.Transport == DNSTransport.TCP)
            {
                this.Port      = Port ?? IPPort.DNS;
            }

            else if (this.Transport == DNSTransport.TLS)
            {
                this.Port      = Port ?? IPPort.DNS_TLS;
            }

            else if (this.Transport == DNSTransport.HTTPS        ||
                     this.Transport == DNSTransport.HTTPS_Binary ||
                     this.Transport == DNSTransport.HTTPS_JSON)
            {
                this.Port      = Port ?? IPPort.HTTPS;
            }

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

            if (this.Transport == DNSTransport.UDP ||
                this.Transport == DNSTransport.TCP)
            {
                this.Port      = Port ?? IPPort.DNS;
            }

            else if (this.Transport == DNSTransport.TLS)
            {
                this.Port      = Port ?? IPPort.DNS_TLS;
            }

            else if (this.Transport == DNSTransport.HTTPS        ||
                     this.Transport == DNSTransport.HTTPS_Binary ||
                     this.Transport == DNSTransport.HTTPS_JSON)
            {
                this.Port      = Port ?? IPPort.HTTPS;
            }

            if (Hermod.IPAddress.TryParse(DomainName.ToString(), out var ipAddress))
                this.IPAddress  = ipAddress;

        }

        #endregion

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"{Transport.ToString().ToLower()}://{DomainName?.ToString() ?? IPAddress.ToString()}:{Port}",

                   QueryTimeout.HasValue
                       ? $", timeout: {Math.Round(QueryTimeout.Value.TotalSeconds)} sec."
                       : ""

               );

        #endregion

    }

}
