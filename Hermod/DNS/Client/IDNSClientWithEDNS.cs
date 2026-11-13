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
    /// The interface for all DNS clients of one DNS server that take EDNS(0)
    /// options for their queries: what <see cref="DNSClient"/> needs of the
    /// client of one of its servers, whatever it reaches that server over.
    /// </summary>
    /// <remarks>
    /// Apart from <see cref="IDNSClientWithTransport"/>, which adds the socket
    /// end points and timeouts of a connection of the client's own: a client
    /// on a channel of somebody else's connection - DNS over SSH on the SSH
    /// connection a node keeps to a peer - has none of them to show.
    /// </remarks>
    public interface IDNSClientWithEDNS : IDNSClientWithDNSSEC
    {

        /// <summary>
        /// Optional EDNS0 options to include in every DNS query
        /// </summary>
        List<EDNSOption>  EDNSOptions    { get; }

    }

}
