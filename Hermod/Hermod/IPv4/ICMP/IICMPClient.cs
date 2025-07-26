/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.RawIP.ICMP
{

    /// <summary>
    /// The common interface of all ICMP clients.
    /// </summary>
    public interface IICMPClient
    {

        /// <summary>
        /// Ping the given DNS hostname.
        /// </summary>
        /// <param name="Hostname">A DNS hostname.</param>
        /// <param name="NumberOfTests">The number of pings.</param>
        /// <param name="Timeout">The timeout of each ping.</param>
        /// <param name="ResultHandler">A delegate called for each ping result.</param>
        /// <param name="Identifier">The ICMP identifier.</param>
        /// <param name="SequenceStartValue">The ICMP echo request start value.</param>
        /// <param name="TestData">The ICMP echo request test data.</param>
        /// <param name="TTL">The time-to-live of the underlying IP packet.</param>
        /// <param name="DNSClient">An optional DNS client to use.</param>
        Task<PingResults> Ping(DomainName              Hostname,
                               UInt32                  NumberOfTests        = 3,
                               TimeSpan?               Timeout              = null,
                               TestRunResultDelegate?  ResultHandler        = null,
                               UInt16?                 Identifier           = null,
                               UInt16                  SequenceStartValue   = 0,
                               String?                 TestData             = null,
                               Byte                    TTL                  = 64,
                               DNSClient?              DNSClient            = null);


        /// <summary>
        /// Ping the given IPv4 address.
        /// </summary>
        /// <param name="IPv4Address">An IPv4 address.</param>
        /// <param name="NumberOfTests">The number of pings.</param>
        /// <param name="Timeout">The timeout of each ping.</param>
        /// <param name="ResultHandler">A delegate called for each ping result.</param>
        /// <param name="Identifier">The ICMP identifier.</param>
        /// <param name="SequenceStartValue">The ICMP echo request start value.</param>
        /// <param name="TestData">The ICMP echo request test data.</param>
        /// <param name="TTL">The time-to-live of the underlying IP packet.</param>
        Task<PingResults> Ping(IPv4Address             IPv4Address,
                               UInt32                  NumberOfTests        = 3,
                               TimeSpan?               Timeout              = null,
                               TestRunResultDelegate?  ResultHandler        = null,
                               UInt16?                 Identifier           = null,
                               UInt16                  SequenceStartValue   = 0,
                               String?                 TestData             = null,
                               Byte                    TTL                  = 64);


        /// <summary>
        /// Ping the given IPv6 address.
        /// </summary>
        /// <param name="IPv6Address">An IPv6 address.</param>
        /// <param name="NumberOfTests">The number of pings.</param>
        /// <param name="Timeout">The timeout of each ping.</param>
        /// <param name="ResultHandler">A delegate called for each ping result.</param>
        /// <param name="Identifier">The ICMP identifier.</param>
        /// <param name="SequenceStartValue">The ICMP echo request start value.</param>
        /// <param name="TestData">The ICMP echo request test data.</param>
        /// <param name="TTL">The time-to-live of the underlying IP packet.</param>
        Task<PingResults> Ping(IPv6Address             IPv6Address,
                               UInt32                  NumberOfTests        = 3,
                               TimeSpan?               Timeout              = null,
                               TestRunResultDelegate?  ResultHandler        = null,
                               UInt16?                 Identifier           = null,
                               UInt16                  SequenceStartValue   = 0,
                               String?                 TestData             = null,
                               Byte                    TTL                  = 64);

    }

}
