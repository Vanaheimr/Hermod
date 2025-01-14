﻿/*
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

using System;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS query.
    /// </summary>
    public class DNSQuery
    {

        #region Data

        /// <summary>
        /// The query types (resource record types).
        /// </summary>
        public UInt16[]         QueryTypes    { get; }

        /// <summary>
        /// The query class.
        /// </summary>
        public DNSQueryClasses  QueryClass    { get; }

        #endregion

        #region Properties

        public String   DomainName          { get; }

        public Int32    TransactionId       { get; }

        public Boolean  RecursionDesired    { get; }

        #endregion

        #region Constructor(s)

        #region DNSQuery(DomainName)

        /// <summary>
        /// Create a new DNS query.
        /// </summary>
        /// <param name="DomainName">The domain name to query.</param>
        public DNSQuery(String DomainName)

            : this(DomainName,
                   true,
                   255)

        { }

        #endregion

        #region DNSQuery(DomainName,                   params DNSResourceRecordTypes)

        /// <summary>
        /// Create a new DNS query.
        /// </summary>
        /// <param name="DomainName">The domain name to query.</param>
        /// <param name="DNSResourceRecordTypes">The DNS resource record types to query.</param>
        public DNSQuery(String           DomainName,
                        params UInt16[]  DNSResourceRecordTypes)

            : this(DomainName,
                   true,
                   DNSResourceRecordTypes)

        { }

        #endregion

        #region DNSQuery(DomainName, RecursionDesired, params ResourceRecordTypes)

        /// <summary>
        /// Create a new DNS query.
        /// </summary>
        /// <param name="DomainName">The domain name to query.</param>
        /// <param name="RecursionDesired">Whether recursion is desired or not.</param>
        /// <param name="ResourceRecordTypes">The DNS resource record types to query.</param>
        public DNSQuery(String           DomainName,
                        Boolean          RecursionDesired,
                        params UInt16[]  ResourceRecordTypes)
        {

            if (ResourceRecordTypes == null || ResourceRecordTypes.Length == 0)
                QueryTypes = new UInt16[1] { 255 };

            else
                QueryTypes = ResourceRecordTypes;

            if (QueryTypes.Length > 2305) // Just because of the number ;)
                throw new ArgumentException("Too many DNSResourceRecordTypes!");

            this.DomainName        = DomainName;
            this.TransactionId     = new Random().Next(55555);
            this.RecursionDesired  = RecursionDesired;
            this.QueryClass        = DNSQueryClasses.IN;

        }

        #endregion

        #endregion


        #region Serialize()

        public Byte[] Serialize()
        {

            var DNSPacket = new Byte[512];

            #region DNS Query Packet Header

            // TransactionId (2 Bytes)
            DNSPacket[ 0] = (Byte) (TransactionId >> 8);
            DNSPacket[ 1] = (Byte) (TransactionId & Byte.MaxValue);

            // Flags (2 Bytes)
            DNSPacket[ 2] = 0x00; // Set OpCode to Regular Query

            if (RecursionDesired)
                DNSPacket[ 2] |= 1;

            DNSPacket[ 3] = 0x00;

            // Number of queries (2 Bytes)
            DNSPacket[ 4] = (Byte) (QueryTypes.Length >> 8);
            DNSPacket[ 5] = (Byte) (QueryTypes.Length & Byte.MaxValue);

            // Number of answer resource records (2 Bytes)
            DNSPacket[ 6] = 0x00;
            DNSPacket[ 7] = 0x00;

            // Number of authority resource records (2 Bytes)
            DNSPacket[ 8] = 0x00;
            DNSPacket[ 9] = 0x00;

            // Number of additional resource records (2 Bytes)
            DNSPacket[10] = 0x00;
            DNSPacket[11] = 0x00;

            var PacketPosition = 12;

            #endregion

            #region Fill Question Section

            foreach (var QueryType in QueryTypes)
            {

                foreach (var DomainNameTokens in DomainName.Split(new Char[] { '.' }))
                {

                    // Set Length label for domainname segment
                    DNSPacket[PacketPosition++] = (Byte) (DomainNameTokens.Length & Byte.MaxValue);

                    foreach (var Char in Encoding.ASCII.GetBytes(DomainNameTokens))
                        DNSPacket[PacketPosition++] = Char;

                }

                // End-of-DomainName marker
                DNSPacket[PacketPosition++] = 0x00;

                // Set Query type
                DNSPacket[PacketPosition++] = (Byte) 0;
                DNSPacket[PacketPosition++] = (Byte) QueryType;

                // Set Query class
                DNSPacket[PacketPosition++] = (Byte) 0;
                DNSPacket[PacketPosition++] = (Byte) QueryClass;

            }

            #endregion

            Array.Resize(ref DNSPacket, PacketPosition);

            return DNSPacket;

        }

        #endregion

    }

}
