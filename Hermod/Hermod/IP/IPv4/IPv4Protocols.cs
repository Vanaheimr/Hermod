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

namespace org.GraphDefined.Vanaheimr.Hermod.IPv4
{

    /// <summary>
    /// IPv4 protocol numbers.
    /// </summary>
    public enum IPv4Protocols : Byte
    {

        /// <summary>
        /// Internet Control Message Protocol
        /// </summary>
        ICMP     =  1,

        /// <summary>
        /// Internet Group Management Protocol
        /// </summary>
        IGMP     =  2,

        /// <summary>
        /// Transmission Control Protocol
        /// </summary>
        TCP      =  6,

        /// <summary>
        /// User Datagram Protocol
        /// </summary>
        UDP      = 17,

        /// <summary>
        /// IPv6 encapsulation
        /// </summary>
        IPv6     = 41,

        /// <summary>
        /// Generic Routing Encapsulation
        /// </summary>
        GRE      = 47,

        /// <summary>
        /// Encapsulating Security Payload
        /// </summary>
        ESP      = 50,

        /// <summary>
        /// Authentication Header
        /// </summary>
        AH       = 51,

        /// <summary>
        /// Open Shortest Path First
        /// </summary>
        OSPF     = 89,

        /// <summary>
        /// Stream Control Transmission Protocol
        /// </summary>
        SCTP     = 132

    }

}
