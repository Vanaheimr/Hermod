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

using Org.BouncyCastle.Asn1.Ocsp;

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public static class DNSPacketExtensions
    {

        public static DNSResponse CreateResponse(this DNSPacket                   Request,

                                                 Byte                             Opcode,
                                                 Boolean                          AuthoritativeAnswer,
                                                 Boolean                          Truncation,
                                                 Boolean                          RecursionDesired,
                                                 Boolean                          RecursionAvailable,
                                                 DNSResponseCodes                 ResponseCode,

                                                 IEnumerable<IDNSResourceRecord>  AnswerRRs,
                                                 IEnumerable<IDNSResourceRecord>  AuthorityRRs,
                                                 IEnumerable<IDNSResourceRecord>  AdditionalRRs)

            => new (

                   Request:               Request,

                   TransactionId:         Request.TransactionId,
                   QueryOrResponse:       DNSQueryResponse.Response,
                   Opcode:                Opcode,
                   AuthoritativeAnswer:   AuthoritativeAnswer,
                   Truncation:            Truncation,
                   RecursionDesired:      RecursionDesired,
                   RecursionAvailable:    RecursionAvailable,
                   ResponseCode:          ResponseCode,

                   Questions:             Request.Questions,
                   AnswerRRs:             AnswerRRs,
                   AuthorityRRs:          AuthorityRRs,
                   AdditionalRRs:         AdditionalRRs

               );

    }


    public class DNSResponse : DNSPacket
    {

        #region Properties

        public DNSPacket?                       Request                { get; }

        #endregion

        #region Constructor(s)

        #region DNSPacket(LocalSocket, RemoteSocket, TransactionId, ... )

        /// <summary>
        /// Create a new DNS query.
        /// </summary>
        public DNSResponse(DNSPacket?                       Request,

                           UInt16                           TransactionId,
                           DNSQueryResponse                 QueryOrResponse,
                           Byte                             Opcode,
                           Boolean                          AuthoritativeAnswer,
                           Boolean                          Truncation,
                           Boolean                          RecursionDesired,
                           Boolean                          RecursionAvailable,
                           DNSResponseCodes                 ResponseCode,

                           IEnumerable<DNSQuestion>         Questions,
                           IEnumerable<IDNSResourceRecord>  AnswerRRs,
                           IEnumerable<IDNSResourceRecord>  AuthorityRRs,
                           IEnumerable<IDNSResourceRecord>  AdditionalRRs)

            : base(TransactionId,
                   QueryOrResponse,
                   Opcode,
                   AuthoritativeAnswer,
                   Truncation,
                   RecursionDesired,
                   RecursionAvailable,
                   ResponseCode,

                   Questions,
                   AnswerRRs,
                   AuthorityRRs,
                   AdditionalRRs,

                   Request?.LocalSocket  ?? IPSocket.Zero,
                   Request?.RemoteSocket ?? IPSocket.Zero)

        {

            this.Request = Request;

        }

        #endregion

        #endregion

    }


    /// <summary>
    /// A DNS query.
    /// </summary>
    public class DNSPacket
    {

        #region Properties

        public IPSocket                         LocalSocket            { get; }
        public IPSocket                         RemoteSocket           { get; }


        /// <summary>
        /// The transaction identifier of this DNS query.
        /// </summary>
        public UInt16                           TransactionId          { get; }

        public DNSQueryResponse                 QueryOrResponse        { get; }
        public Byte                             Opcode                 { get; }
        public Boolean                          AuthoritativeAnswer    { get; }
        public Boolean                          Truncation             { get; }

        /// <summary>
        /// Whether recursion is desired or not.
        /// </summary>
        public Boolean                          RecursionDesired       { get; }
        public Boolean                          RecursionAvailable     { get; }
        public DNSResponseCodes                 ResponseCode           { get; }

        public IEnumerable<DNSQuestion>         Questions              { get; } = [];
        public IEnumerable<IDNSResourceRecord>  AnswerRRs              { get; } = [];
        public IEnumerable<IDNSResourceRecord>  AuthorityRRs           { get; } = [];
        public IEnumerable<IDNSResourceRecord>  AdditionalRRs          { get; } = [];

        #endregion

        #region Constructor(s)

        #region DNSPacket(LocalSocket, RemoteSocket, TransactionId, ... )

        /// <summary>
        /// Create a new DNS query.
        /// </summary>
        public DNSPacket(UInt16                           TransactionId,
                         DNSQueryResponse                 QueryOrResponse,
                         Byte                             Opcode,
                         Boolean                          AuthoritativeAnswer,
                         Boolean                          Truncation,
                         Boolean                          RecursionDesired,
                         Boolean                          RecursionAvailable,
                         DNSResponseCodes                 ResponseCode,

                         IEnumerable<DNSQuestion>         Questions,
                         IEnumerable<IDNSResourceRecord>  AnswerRRs,
                         IEnumerable<IDNSResourceRecord>  AuthorityRRs,
                         IEnumerable<IDNSResourceRecord>  AdditionalRRs,

                         IPSocket?                        LocalSocket    = null,
                         IPSocket?                        RemoteSocket   = null)

        {

            this.LocalSocket          = LocalSocket  ?? IPSocket.Zero;
            this.RemoteSocket         = RemoteSocket ?? IPSocket.Zero;

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
                     DNSResourceRecordTypes.Any);

        #endregion

        #region Query(DomainName,                   params DNSResourceRecordTypes)

        /// <summary>
        /// Create a new DNS request.
        /// </summary>
        /// <param name="DomainName">The domain name to query.</param>
        /// <param name="DNSResourceRecordTypes">The DNS resource record types to query.</param>
        public static DNSPacket Query(DNSServiceName                   DomainName,
                                      params DNSResourceRecordTypes[]  DNSResourceRecordTypes)

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
                                      Boolean                          RecursionDesired,
                                      //IEnumerable<IDNSResourceRecord>  AdditionalRRs,
                                      params DNSResourceRecordTypes[]  ResourceRecordTypes)
        {

            var questions = new List<DNSQuestion>();

            if (ResourceRecordTypes is null || ResourceRecordTypes.Length == 0)
                questions = [ new DNSQuestion(DomainName, DNSResourceRecordTypes.Any, DNSQueryClasses.IN) ];

            else
                foreach (var resourceRecordType in ResourceRecordTypes)
                    questions.Add(new DNSQuestion(DomainName, resourceRecordType, DNSQueryClasses.IN));


            return new DNSPacket(
                       TransactionId:         (UInt16) new Random().Next(UInt16.MaxValue),
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






        //private static List<IDNSResourceRecord> ParseResourceRecords(Byte[] packet, ref Int32 position, UInt16 count)
        private static List<IDNSResourceRecord> ParseResourceRecords(Stream stream, UInt16 count)
        {

            var records = new List<IDNSResourceRecord>();

            for (var i = 0; i < count; i++)
            {

                var name         = DNSTools.ExtractName(stream);

                var recordType   = (DNSResourceRecordTypes) stream.ReadUInt16BE(); //((packet[position++] <<  8) |  packet[position++]);
                //var queryClass   = (DNSQueryClasses)        stream.ReadUInt16BE(); //((packet[position++] <<  8) |  packet[position++]);
                //var rawTTL       =                          stream.ReadUInt32BE(); //((packet[position++] << 24) | (packet[position++] << 16) | (packet[position++] << 8) | packet[position++]);
                //var timeToLive   = TimeSpan.FromSeconds     (rawTTL);
                //var rdataLength  =                          stream.ReadUInt32BE(); //(UInt16) ((packet[position++] <<  8) |  packet[position++]);
                //var rdataOffset  = position;

                IDNSResourceRecord record = recordType switch {
                    DNSResourceRecordTypes.A     => new A      (DNSServiceName.Parse(name), stream),
                //    DNSResourceRecordTypes.A     => new ARecord      (DNS.DNSServiceName.Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                //    DNSResourceRecordTypes.AAAA  => new AAAARecord   (DNS.DNSServiceName.Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                //    DNSResourceRecordTypes.SRV   => new SRVRecord    (DNS.DomainName.    Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                //    DNSResourceRecordTypes.URI   => new URIRecord    (DNS.DomainName.    Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                //    DNSResourceRecordTypes.NAPTR => new NAPTRRecord  (DNS.DomainName.    Parse(name),             queryClass, timeToLive, rdataLength, packet, rdataOffset),
                //    DNSResourceRecordTypes.OPT   => new OPTRecord    (DNS.DomainName.    Parse(name),             queryClass, rawTTL,     rdataLength, packet, rdataOffset),
                    DNSResourceRecordTypes.OPT   => new OPT    (DNSServiceName.Parse(name), stream),
                //    _                            => new GenericRecord(DNS.DomainName.    Parse(name), recordType, queryClass, timeToLive, rdataLength, packet, rdataOffset)
                };

                records.Add(record);
                //position += rdataLength;

            }

            return records;

        }

        #region Parse(LocalSocket, RemoteSocket, Stream)

        // ARSoft.Tools.Net
        public static DNSPacket Parse(IPSocket  LocalSocket,
                                      IPSocket  RemoteSocket,
                                      Stream    Stream)
        {

            // host -p 63 -t A heise.de  172.23.32.1

           // if (Packet.Length < 12)
           //     return null;

            var position               = 0;

            var transactionId          = Stream.ReadUInt16BE(); // (UInt16) ((Packet[position++] << 8) | Packet[position++]);
            var flags1                 = Stream.ReadByte();     // Packet[position++];
            var flags2                 = Stream.ReadByte();     // Packet[position++];

            var queryOrResponse        = (flags1 & 0x80) != 0 ? DNSQueryResponse.Response : DNSQueryResponse.Query;
            var opcode                 = (Byte)   ((flags1 >> 3) & 0x0F);
            var authoritativeAnswer    = (flags1 & 0x04) != 0;
            var truncation             = (flags1 & 0x02) != 0;
            var recursionDesired       = (flags1 & 0x01) != 0;
            var recursionAvailable     = (flags2 & 0x80) != 0;
            var responseCode           = (DNSResponseCodes) (flags2 & 0x0F);

            var numberOfQuestions      = Stream.ReadUInt16BE(); // (UInt16) ((Packet[position++] << 8) | Packet[position++]);
            var numberOfAnswerRRs      = Stream.ReadUInt16BE(); // (UInt16) ((Packet[position++] << 8) | Packet[position++]);
            var numberOfAuthorityRRs   = Stream.ReadUInt16BE(); // (UInt16) ((Packet[position++] << 8) | Packet[position++]);
            var numberOfAdditionalRRs  = Stream.ReadUInt16BE(); // (UInt16) ((Packet[position++] << 8) | Packet[position++]);

            var questions              = new List<DNSQuestion>();

            for (var i = 0; i < numberOfQuestions; i++)
                questions.Add(DNSQuestion.Parse(Stream));// Packet, ref position));

            var answerRRs              = ParseResourceRecords(Stream, numberOfAnswerRRs);
            var authorityRRs           = ParseResourceRecords(Stream, numberOfAuthorityRRs);
            var additionalRRs          = ParseResourceRecords(Stream, numberOfAdditionalRRs);


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
                       additionalRRs,

                       LocalSocket,
                       RemoteSocket

                   );

        }

        #endregion

        #region Serialize()

        public void Serialize(Stream                      Stream,
                              Boolean                     UseCompression       = false,
                              Dictionary<String, Int32>?  CompressionOffsets   = null)
        {

            //var dnsPacket = new Byte[512];
            var packetPositionStart = Stream.Position;

            #region DNS Query Packet Header

            // TransactionId (2 Bytes)
            //dnsPacket[ 0] = (Byte) (TransactionId >> 8);
            //dnsPacket[ 1] = (Byte) (TransactionId & Byte.MaxValue);
            Stream.WriteUInt16BE(TransactionId);

            // Flags (2 Bytes)
            //dnsPacket[ 2] = 0x00;
            //dnsPacket[ 3] = 0x00;
            var flags1 = (Byte) 0x00;
            var flags2 = (Byte) 0x00;

            if (QueryOrResponse == DNSQueryResponse.Response)
                flags1 |= 0x80;

            // Set Opcode
            flags1 |= (Byte) ((Byte) Opcode << 3);

            // Set Authoritative Answer (AA)
            if (AuthoritativeAnswer)
                flags1 |= 0x04;

            // Set Truncation (TC)
            if (Truncation)
                flags1 |= 0x02;

            // Set Recursion Desired (RD)
            if (RecursionDesired)
                flags1 |= 0x01;

            // Set Recursion Available (RA)
            if (RecursionAvailable)
                flags2 |= 0x80;

            // Set Response Code (RCODE)
            flags2 |= (Byte) ((Byte) ResponseCode & 0x0F);

            Stream.WriteByte(flags1);
            Stream.WriteByte(flags2);



            //var numberOfQuestions      = Questions.    Count();
            //dnsPacket[ 4] = (Byte) (numberOfQuestions      >> 8);
            //dnsPacket[ 5] = (Byte) (numberOfQuestions     & Byte.MaxValue);
            //
            //var numberOfAnswerRRs      = AnswerRRs.    Count();
            //dnsPacket[ 6] = (Byte) (numberOfAnswerRRs     >> 8);
            //dnsPacket[ 7] = (Byte) (numberOfAnswerRRs     & Byte.MaxValue);
            //
            //var numberOfAuthorityRRs   = AuthorityRRs. Count();
            //dnsPacket[ 8] = (Byte) (numberOfAuthorityRRs  >> 8);
            //dnsPacket[ 9] = (Byte) (numberOfAuthorityRRs  & Byte.MaxValue);
            //
            //var numberOfAdditionalRRs  = AdditionalRRs.Count();
            //dnsPacket[10] = (Byte) (numberOfAdditionalRRs >> 8);
            //dnsPacket[11] = (Byte) (numberOfAdditionalRRs & Byte.MaxValue);

            Stream.WriteUInt16BE((UInt16) Questions.    Count());
            Stream.WriteUInt16BE((UInt16) AnswerRRs.    Count());
            Stream.WriteUInt16BE((UInt16) AuthorityRRs. Count());
            Stream.WriteUInt16BE((UInt16) AdditionalRRs.Count());


            //var packetPosition = 12;

            #endregion

            #region Fill Question Section

            foreach (var question in Questions)
            {

                question.Serialize(
                    Stream,
                    (Int32) (Stream.Position - packetPositionStart),
                    UseCompression,
                    CompressionOffsets
                );

                //foreach (var domainNameLabel in question.DomainName.Labels)
                //{

                //    // Set Length label for domain name segment
                //    dnsPacket[packetPosition++] = (Byte) (domainNameLabel.Length & Byte.MaxValue);

                //    foreach (var character in Encoding.ASCII.GetBytes(domainNameLabel))
                //        dnsPacket[packetPosition++] = character;

                //}

                //// End-of-DomainName marker
                //dnsPacket[packetPosition++] = 0x00;

                //var queryType  = (UInt16) question.QueryType;
                //dnsPacket[packetPosition++] = (Byte) (queryType  >> 8);
                //dnsPacket[packetPosition++] = (Byte) (queryType   & 0xFF);

                //var queryClass = (UInt16) question.QueryClass;
                //dnsPacket[packetPosition++] = (Byte) (queryClass >> 8);
                //dnsPacket[packetPosition++] = (Byte) (queryClass  & 0xFF);

            }

            #endregion



            foreach (var answerRR     in AnswerRRs)
                answerRR.    Serialize(Stream, UseCompression, CompressionOffsets);

            foreach (var authorityRR  in AuthorityRRs)
                authorityRR. Serialize(Stream, UseCompression, CompressionOffsets);

            foreach (var additionalRR in AdditionalRRs)
                additionalRR.Serialize(Stream, UseCompression, CompressionOffsets);



            //var byteArray = Stream.ToByteArray();
            //Array.Copy(byteArray, 0, dnsPacket, packetPosition, byteArray.Length);
            //packetPosition += byteArray.Length;

            //Array.Resize(ref dnsPacket, packetPosition);

            //return dnsPacket;

        }

        #endregion


        public Byte[] ToByteArray()
        {

            var ms = new MemoryStream();
            Serialize(
                ms,
                UseCompression:      false,
                CompressionOffsets:  []
            );

            return ms.ToArray();

        }


    }

}
