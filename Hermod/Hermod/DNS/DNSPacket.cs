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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS query.
    /// </summary>
    public class DNSPacket
    {

        #region Properties

        /// <summary>
        /// The transaction identifier of this DNS query.
        /// </summary>
        public Int32                           TransactionId          { get; }

        public DNSQueryResponse                QueryOrResponse        { get; }
        public Byte                            Opcode                 { get; }
        public Boolean                         AuthoritativeAnswer    { get; }
        public Boolean                         Truncation             { get; }

        /// <summary>
        /// Whether recursion is desired or not.
        /// </summary>
        public Boolean                         RecursionDesired       { get; }
        public Boolean                         RecursionAvailable     { get; }
        public DNSResponseCodes                ResponseCode           { get; }

        public IEnumerable<DNSQuestion>        Questions              { get; } = [];
        public IEnumerable<DNSResourceRecord>  AnswerRRs              { get; } = [];
        public IEnumerable<DNSResourceRecord>  AuthorityRRs           { get; } = [];
        public IEnumerable<DNSResourceRecord>  AdditionalRRs          { get; } = [];

        #endregion

        #region Constructor(s)



        #region DNSPacket(TransactionId, Flags1, Flags2, )

        /// <summary>
        /// Create a new DNS query.
        /// </summary>
        public DNSPacket(Int32                           TransactionId,
                        DNSQueryResponse                QueryOrResponse,
                        Byte                            Opcode,
                        Boolean                         AuthoritativeAnswer,
                        Boolean                         Truncation,
                        Boolean                         RecursionDesired,
                        Boolean                         RecursionAvailable,
                        DNSResponseCodes                ResponseCode,

                        IEnumerable<DNSQuestion>        Questions,
                        IEnumerable<DNSResourceRecord>  AnswerRRs,
                        IEnumerable<DNSResourceRecord>  AuthorityRRs,
                        IEnumerable<DNSResourceRecord>  AdditionalRRs)

        {

            this.TransactionId        = TransactionId;
            this.QueryOrResponse      = QueryOrResponse;
            this.Opcode               = Opcode;
            this.AuthoritativeAnswer  = AuthoritativeAnswer;
            this.Truncation           = Truncation;
            this.RecursionDesired     = RecursionDesired;
            this.RecursionAvailable   = RecursionAvailable;
            this.ResponseCode         = ResponseCode;

            this.Questions            = Questions;
            this.AnswerRRs            = AnswerRRs;
            this.AuthorityRRs         = AuthorityRRs;
            this.AdditionalRRs        = AdditionalRRs;

        }

        #endregion

        #endregion


        #region Query(DomainName)

        /// <summary>
        /// Create a new DNS request.
        /// </summary>
        /// <param name="DomainName">The domain name to query.</param>
        public static DNSPacket Query(DNSServiceName DomainName)

            => Query(DomainName,
                     true,
                     DNSResourceRecordType.Any);

        #endregion

        #region Query(DomainName,                   params DNSResourceRecordTypes)

        /// <summary>
        /// Create a new DNS request.
        /// </summary>
        /// <param name="DomainName">The domain name to query.</param>
        /// <param name="DNSResourceRecordTypes">The DNS resource record types to query.</param>
        public static DNSPacket Query(DNSServiceName                   DomainName,
                                      params DNSResourceRecordType[]  DNSResourceRecordTypes)

            => Query(DomainName,
                     true,
                     DNSResourceRecordTypes);

        #endregion

        #region Query(DomainName, RecursionDesired, params ResourceRecordTypes)

        /// <summary>
        /// Create a new DNS request.
        /// </summary>
        /// <param name="DomainName">The domain name to query.</param>
        /// <param name="RecursionDesired">Whether recursion is desired or not.</param>
        /// <param name="ResourceRecordTypes">The DNS resource record types to query.</param>
        public static DNSPacket Query(DNSServiceName                   DomainName,
                                      Boolean                      RecursionDesired,
                                      params DNSResourceRecordType[]  ResourceRecordTypes)
        {

            var questions = new List<DNSQuestion>();

            if (ResourceRecordTypes is null || ResourceRecordTypes.Length == 0)
                questions = [ new DNSQuestion(DomainName, DNSResourceRecordType.Any, DNSQueryClasses.IN) ];

            else
                foreach (var resourceRecordType in ResourceRecordTypes)
                    questions.Add(new DNSQuestion(DomainName, resourceRecordType, DNSQueryClasses.IN));


            return new DNSPacket(
                       TransactionId:         new Random().Next(65535),
                       QueryOrResponse:       DNSQueryResponse.Query,
                       Opcode:                0x00,
                       AuthoritativeAnswer:   false,
                       Truncation:            false,
                       RecursionDesired:      RecursionDesired,
                       RecursionAvailable:    false,
                       ResponseCode:          DNSResponseCodes.NoError,
                       Questions:             questions,
                       AnswerRRs:             [],
                       AuthorityRRs:          [],
                       AdditionalRRs:         []
                   );

        }

        #endregion






        private static List<DNSResourceRecord> ParseResourceRecords(Byte[] packet, ref Int32 position, UInt16 count)
        {

            var records = new List<DNSResourceRecord>();

            for (var i = 0; i < count; i++)
            {

                var name     = DNSTools.ReadDomainName(packet, ref position);

                if (name == "")
                    name = ".";

                var recordType   = (DNSResourceRecordType) ((packet[position++] <<  8) |  packet[position++]);
                var queryClass   = (DNSQueryClasses)    ((packet[position++] <<  8) |  packet[position++]);
                var rawTTL       = (UInt32)             ((packet[position++] << 24) | (packet[position++] << 16) | (packet[position++] << 8) | packet[position++]);
                var timeToLive   = TimeSpan.FromSeconds (rawTTL);
                var rdataLength  = (UInt16) ((packet[position++] <<  8) |  packet[position++]);
                var rdataOffset  = position;

                DNSResourceRecord record = recordType switch {
                    DNSResourceRecordType.A     => new ARecord      (DNS.DNSServiceName.Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                    DNSResourceRecordType.AAAA  => new AAAARecord   (DNS.DNSServiceName.Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                    DNSResourceRecordType.SRV   => new SRVRecord    (DNS.DomainName.Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                    DNSResourceRecordType.URI   => new URIRecord    (DNS.DomainName.Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                    DNSResourceRecordType.NAPTR => new NAPTRRecord  (DNS.DomainName.Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                    DNSResourceRecordType.OPT   => new OPTRecord    (DNS.DomainName.Parse(name),             queryClass, rawTTL,     rdataLength, packet, rdataOffset),
                    _                        => new GenericRecord(DNS.DomainName.Parse(name), recordType, queryClass, timeToLive, rdataLength, packet, rdataOffset)
                };

                records.Add(record);
                position += rdataLength;

            }

            return records;

        }

        #region Parse(Packet)

        // ARSoft.Tools.Net
        public static DNSPacket Parse(Byte[] Packet)
        {

            // host -p 63 -t A heise.de  172.23.32.1

            if (Packet.Length < 12)
                return null;

            var position               = 0;

            var transactionId          = (UInt16) ((Packet[position++] << 8) | Packet[position++]);
            var flags1                 = Packet[position++];
            var flags2                 = Packet[position++];

            var queryOrResponse        = (flags1 & 0x80) != 0 ? DNSQueryResponse.Response : DNSQueryResponse.Query;
            var opcode                 = (Byte)   ((flags1 >> 3) & 0x0F);
            var authoritativeAnswer    = (flags1 & 0x04) != 0;
            var truncation             = (flags1 & 0x02) != 0;
            var recursionDesired       = (flags1 & 0x01) != 0;
            var recursionAvailable     = (flags2 & 0x80) != 0;
            var responseCode           = (DNSResponseCodes) (flags2 & 0x0F);

            var numberOfQuestions      = (UInt16) ((Packet[position++] << 8) | Packet[position++]);
            var numberOfAnswerRRs      = (UInt16) ((Packet[position++] << 8) | Packet[position++]);
            var numberOfAuthorityRRs   = (UInt16) ((Packet[position++] << 8) | Packet[position++]);
            var numberOfAdditionalRRs  = (UInt16) ((Packet[position++] << 8) | Packet[position++]);

            var questions              = new List<DNSQuestion>();

            for (var i = 0; i < numberOfQuestions; i++)
                questions.Add(DNSQuestion.Parse(Packet, ref position));

            var answerRRs              = ParseResourceRecords(Packet, ref position, numberOfAnswerRRs);
            var authorityRRs           = ParseResourceRecords(Packet, ref position, numberOfAuthorityRRs);
            var additionalRRs          = ParseResourceRecords(Packet, ref position, numberOfAdditionalRRs);


            return new DNSPacket(

                       transactionId,
                       queryOrResponse,
                       opcode,
                       authoritativeAnswer,
                       truncation,
                       recursionDesired,
                       recursionAvailable,
                       responseCode,

                       questions,
                       answerRRs,
                       authorityRRs,
                       additionalRRs

                   );

        }

        #endregion

        #region Serialize()

        public Byte[] Serialize()
        {

            var dnsPacket = new Byte[512];

            #region DNS Query Packet Header

            // TransactionId (2 Bytes)
            dnsPacket[ 0] = (Byte) (TransactionId >> 8);
            dnsPacket[ 1] = (Byte) (TransactionId & Byte.MaxValue);

            // Flags (2 Bytes)
            dnsPacket[ 2] = 0x00;
            dnsPacket[ 3] = 0x00;

            if (QueryOrResponse == DNSQueryResponse.Response)
                dnsPacket[2] |= 0x80;

            // Set Opcode
            dnsPacket[2]     |= (Byte) ((Byte) Opcode << 3);

            // Set Authoritative Answer (AA)
            if (AuthoritativeAnswer)
                dnsPacket[2] |= 0x04;

            // Set Truncation (TC)
            if (Truncation)
                dnsPacket[2] |= 0x02;

            // Set Recursion Desired (RD)
            if (RecursionDesired)
                dnsPacket[2] |= 0x01;

            // Set Recursion Available (RA)
            if (RecursionAvailable)
                dnsPacket[3] |= 0x80;

            // Set Response Code (RCODE)
            dnsPacket[3]     |= (Byte) ((Byte) ResponseCode & 0x0F);


            var numberOfQuestions      = Questions.    Count();
            dnsPacket[ 4] = (Byte) (numberOfQuestions      >> 8);
            dnsPacket[ 5] = (Byte) (numberOfQuestions     & Byte.MaxValue);

            var numberOfAnswerRRs      = AnswerRRs.    Count();
            dnsPacket[ 6] = (Byte) (numberOfAnswerRRs     >> 8);
            dnsPacket[ 7] = (Byte) (numberOfAnswerRRs     & Byte.MaxValue);

            var numberOfAuthorityRRs   = AuthorityRRs. Count();
            dnsPacket[ 8] = (Byte) (numberOfAuthorityRRs  >> 8);
            dnsPacket[ 9] = (Byte) (numberOfAuthorityRRs  & Byte.MaxValue);

            var numberOfAdditionalRRs  = AdditionalRRs.Count();
            dnsPacket[10] = (Byte) (numberOfAdditionalRRs >> 8);
            dnsPacket[11] = (Byte) (numberOfAdditionalRRs & Byte.MaxValue);

            var packetPosition = 12;

            #endregion

            #region Fill Question Section

            foreach (var question in Questions)
            {

                foreach (var domainNameLabel in question.DomainName.Labels)
                {

                    // Set Length label for domain name segment
                    dnsPacket[packetPosition++] = (Byte) (domainNameLabel.Length & Byte.MaxValue);

                    foreach (var character in Encoding.ASCII.GetBytes(domainNameLabel))
                        dnsPacket[packetPosition++] = character;

                }

                // End-of-DomainName marker
                dnsPacket[packetPosition++] = 0x00;

                var queryType  = (UInt16) question.QueryType;
                dnsPacket[packetPosition++] = (Byte) (queryType  >> 8);
                dnsPacket[packetPosition++] = (Byte) (queryType   & 0xFF);

                var queryClass = (UInt16) question.QueryClass;
                dnsPacket[packetPosition++] = (Byte) (queryClass >> 8);
                dnsPacket[packetPosition++] = (Byte) (queryClass  & 0xFF);

            }

            #endregion

            Array.Resize(ref dnsPacket, packetPosition);

            return dnsPacket;

        }

        #endregion


    }

}
