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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public class DNSQuestion
    {

        public DNSServiceName          DomainName    { get; }
        public DNSResourceRecordTypes  QueryType     { get; }
        public DNSQueryClasses         QueryClass    { get; }



        public static DNSQuestion Parse(Byte[] packet, ref Int32 position)

            => new (
                   DNSServiceName.Parse(DNSTools.ReadDomainName(packet, ref position)),
                   (DNSResourceRecordTypes) ((packet[position++] << 8) | packet[position++]),
                   (DNSQueryClasses)    ((packet[position++] << 8) | packet[position++])
               );


        public static DNSQuestion Parse(Stream Stream)

            => new (
                   DNSTools.ExtractDNSServiceName(Stream),
                   (DNSResourceRecordTypes) Stream.ReadUInt16BE(),
                   (DNSQueryClasses)        Stream.ReadUInt16BE()
               );



        public void Serialize(Stream                      Stream,
                              Int32                       CurrentOffset,
                              Boolean                     UseCompression       = true,
                              Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            DomainName.Serialize(
                Stream,
                CurrentOffset,
                UseCompression,
                CompressionOffsets
            );

            Stream.WriteUInt16BE((UInt16) QueryType);
            Stream.WriteUInt16BE((UInt16) QueryClass);

        }




        public DNSQuestion(DNSServiceName          DomainName,
                           DNSResourceRecordTypes  QueryType,
                           DNSQueryClasses         QueryClass)
        {

            this.DomainName  = DomainName;
            this.QueryType   = QueryType;
            this.QueryClass  = QueryClass;

        }

    }

}
