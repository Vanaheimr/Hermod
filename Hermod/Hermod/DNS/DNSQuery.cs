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

            if (ResourceRecordTypes is null || ResourceRecordTypes.Length == 0)
                QueryTypes = [ 255 ];

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

        private static readonly Char[] splitter = [ '.' ];

        #region Serialize()

        public Byte[] Serialize()
        {

            var dnsPacket = new Byte[512];

            #region DNS Query Packet Header

            // TransactionId (2 Bytes)
            dnsPacket[ 0] = (Byte) (TransactionId >> 8);
            dnsPacket[ 1] = (Byte) (TransactionId & Byte.MaxValue);

            // Flags (2 Bytes)
            dnsPacket[ 2] = 0x00; // Set OpCode to Regular Query

            if (RecursionDesired)
                dnsPacket[ 2] |= 1;

            dnsPacket[ 3] = 0x00;

            // Number of queries (2 Bytes)
            dnsPacket[ 4] = (Byte) (QueryTypes.Length >> 8);
            dnsPacket[ 5] = (Byte) (QueryTypes.Length & Byte.MaxValue);

            // Number of answer resource records (2 Bytes)
            dnsPacket[ 6] = 0x00;
            dnsPacket[ 7] = 0x00;

            // Number of authority resource records (2 Bytes)
            dnsPacket[ 8] = 0x00;
            dnsPacket[ 9] = 0x00;

            // Number of additional resource records (2 Bytes)
            dnsPacket[10] = 0x00;
            dnsPacket[11] = 0x00;

            var packetPosition = 12;

            #endregion

            #region Fill Question Section

            foreach (var queryType in QueryTypes)
            {

                foreach (var DomainNameTokens in DomainName.Split(splitter))
                {

                    // Set Length label for domain name segment
                    dnsPacket[packetPosition++] = (Byte) (DomainNameTokens.Length & Byte.MaxValue);

                    foreach (var character in Encoding.ASCII.GetBytes(DomainNameTokens))
                        dnsPacket[packetPosition++] = character;

                }

                // End-of-DomainName marker
                dnsPacket[packetPosition++] = 0x00;

                dnsPacket[packetPosition++] = (Byte) 0;
                dnsPacket[packetPosition++] = (Byte) queryType;

                dnsPacket[packetPosition++] = (Byte) 0;
                dnsPacket[packetPosition++] = (Byte) QueryClass;

            }

            #endregion

            Array.Resize(ref dnsPacket, packetPosition);

            return dnsPacket;

        }

        #endregion

    }

}
