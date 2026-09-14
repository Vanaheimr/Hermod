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
    public interface IDNSResourceRecord : IDNSPseudoResourceRecord
    {

        DNSQueryClasses  Class         { get; }
        DNSServiceName   DomainName    { get; }
        DateTimeOffset   EndOfLife     { get; }
        String?          RText         { get; }
        IIPAddress?      Source        { get; }
        TimeSpan         TimeToLive    { get; }


        void Serialize(Stream                      Stream,
                       Boolean                     UseCompression       = true,
                       Dictionary<String, Int32>?  CompressionOffsets   = null);

        /// <summary>
        /// The zone-file presentation form of this resource record
        /// (RFC 1035 §5.1): name, TTL, class, type and RDATA.
        /// </summary>
        /// <remarks>
        /// The counterpart of <see cref="Serialize"/>, which is the other way a
        /// record is written down. Both belong here for the same reason: the
        /// zone-file reader hands back this interface, so without it, writing
        /// back what was just read needed a cast to the abstract base in every
        /// caller, for a method every one of them had.
        /// <para>
        /// One implementation refuses. RFC 6891 §6.1.1 says an OPT "MUST NOT be
        /// cached, forwarded, or stored in or loaded from master files": it
        /// belongs to a single message and has no presentation form at all. That
        /// is the specification speaking rather than a gap, so OPT throws and
        /// says which section.
        /// </para>
        /// </remarks>
        String ToZoneFileString();

    }

}
