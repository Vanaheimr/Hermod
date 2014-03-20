/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Net;
using System.Text;
using System.Linq;
using System.Net.Sockets;
using System.Diagnostics;
using System.Collections.Generic;

#endregion

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    public class DNSClient
    {

        #region Data

        private int position, length;

        #endregion

        #region Properties

        public TimeSpan  QueryTimeout       { get; set; }
        public String    DNSServer          { get; set; }
        public IPPort    Port               { get; set; }
        public Boolean   RecursionDesired   { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Default Constructor with QueryType: A 
        /// </summary>
        public DNSClient()
            : this("8.8.8.8", new IPPort(53))
        { }

        public DNSClient(String DNSServer)
            : this(DNSServer, new IPPort(53))
        { }

        public DNSClient(String DNSServer, IPPort Port)
        {
            this.Port              = new IPPort(53);
            this.DNSServer         = DNSServer;
            this.QueryTimeout      = TimeSpan.FromSeconds(10);
            this.RecursionDesired  = true;
        }

        #endregion


        #region Query(DomainName, params DNSResourceRecordTypes)

        public DNSResponse Query(String                           DomainName,
                                 params DNSResourceRecordTypes[]  DNSResourceRecordTypes)
        {

            // Preparing the DNS query packet
            var QueryPacket = new DNSQuery(DomainName, DNSResourceRecordTypes) {
                                      RecursionDesired = RecursionDesired
                                  };

            // Query DNS server
            var serverAddress  = IPAddress.Parse(DNSServer);
            var endPoint       = (EndPoint) new IPEndPoint(serverAddress, this.Port.ToInt32());
            var socket         = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout,    (Int32) QueryTimeout.TotalMilliseconds);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (Int32) QueryTimeout.TotalMilliseconds);
            socket.Connect(endPoint);
            socket.SendTo(QueryPacket.Serialize(), endPoint);

            var data = new Byte[512];
            length = socket.ReceiveFrom(data, ref endPoint);

            socket.Shutdown(SocketShutdown.Both);

            return ReadResponse(data);

        }

        #endregion

        #region Query<T>(DomainName)

        public IEnumerable<T> Query<T>(String DomainName)
            where T : ADNSResourceRecord
        {

            DNSResourceRecordTypes DNSResourceRecordType;

            if (typeof(T) == typeof(A))
                DNSResourceRecordType = DNSResourceRecordTypes.A;

            else if (typeof(T) == typeof(TXT))
                DNSResourceRecordType = DNSResourceRecordTypes.TXT;

            else
                throw new ArgumentException("");

            return Query(DomainName, DNSResourceRecordType).
                       Answers.
                       Where(v => v.GetType() == typeof(T)).
                       Select(v => v as T);

        }

        #endregion

        #region QueryFirst<T>(DomainName)

        public T QueryFirst<T>(String DomainName)
            where T : ADNSResourceRecord
        {
            return Query<T>(DomainName).FirstOrDefault();
        }

        #endregion

        #region Query<T1, T2>(DomainName, Mapper)

        public IEnumerable<T2> Query<T1, T2>(String DomainName, Func<T1, T2> Mapper)
            where T1 : ADNSResourceRecord
        {
            return Query<T1>(DomainName).Select(v => Mapper(v));
        }

        #endregion
        


        #region ReadResponse(data)

        private DNSResponse ReadResponse(Byte[] data)
        {

            #region DNS Header

            var ID      = ((data[0] & byte.MaxValue) << 8) + (data[1] & byte.MaxValue);
            var IS      = (data[2] & 128) == 128;                
            var OpCode  = (data[2] >> 3 & 15);
            var AA      = (data[2] & 4) == 4;
            var TC      = (data[2] & 2) == 2;
            var RD      = (data[2] & 1) == 1;
            var RA      = (data[3] & 128) == 128;
            var Z       = (data[3] & 1);//reserved, not used
            var RC      = (data[3] & 15);

            #endregion

            //Counts
            int QuestionCount   = ((data[4] & byte.MaxValue) << 8) | (data[5] & byte.MaxValue);
            int AnswerCount     = ((data[6] & byte.MaxValue) << 8) | (data[7] & byte.MaxValue);
            //Trace.WriteLine("Answer count: " + AnswerCount); 
            int AuthorityCount  = ((data[8] & byte.MaxValue) << 8) | (data[9] & byte.MaxValue);
            int AdditionalCount = ((data[10] & byte.MaxValue) << 8) | (data[11] & byte.MaxValue);

            //Create Response Object
            var _Response = new DNSResponse(ID, AA, TC, RD, RA, RC); 

            //FINISHED HEADER

            //GET QUESTIONS
            position=12;

            for(int i=0; i<QuestionCount; ++i)
            {

                string QuestionName = GetName(data);

                //two octec field
                int TypeID = (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;
                DNSResourceRecordTypes QuestionType = (DNSResourceRecordTypes)TypeID; 

                //two octec field
                int ClassID = (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;
                DNSQueryClasses QuestionClass = (DNSQueryClasses)ClassID;

            }

            for (int i = 0; i < AnswerCount; ++i)
                GetResourceRecord(data, i, _Response.Answers);

            for (int i = 0; i < AuthorityCount; ++i)
                GetResourceRecord(data, i, _Response.Authorities);

            for (int i = 0; i < AdditionalCount; ++i)
                GetResourceRecord(data, i, _Response.AdditionalRecords);

            return _Response;

        }

        #endregion



        private void GetResourceRecord(Byte[] data, int i, List<ADNSResourceRecord> Container)
        {

            //get resource (answer) name
            string ResourceName = GetName(data);

            //get resource type and class, usefull when using the ANY query
            //type: two octec field
            int TypeID = (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;
            DNSResourceRecordTypes ResourceType = (DNSResourceRecordTypes)TypeID;

            //type: two octec field
            int ClassID = (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;
            DNSQueryClasses ResourceClass = (DNSQueryClasses)ClassID;

            //ttl: unsigned integer
            var TTL_Seconds = TimeSpan.FromSeconds((data[position++] & byte.MaxValue) << 24 | (data[position++] & byte.MaxValue) << 16 | (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue);

            //Get Resource Data Length
            int RDLength = (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;

            //Parse Resource Data: 4 possible formats: A, Text, SOA and MX
            switch(ResourceType) 
            {

                case DNSResourceRecordTypes.A:
                    Container.Add(new A(ResourceName, ResourceClass, TTL_Seconds, new IPv4Address(new Byte[] { data[position++], data[position++], data[position++], data[position++] })));
                    break;

                case DNSResourceRecordTypes.AAAA:
                    Container.Add(new AAAA(ResourceName, ResourceClass, TTL_Seconds, new IPv6Address(new Byte[] { data[position++], data[position++], data[position++], data[position++],
                                                                                                                  data[position++], data[position++], data[position++], data[position++],
                                                                                                                  data[position++], data[position++], data[position++], data[position++], 
                                                                                                                  data[position++], data[position++], data[position++], data[position++] })));
                    break;

                case DNSResourceRecordTypes.SOA:
                    //Extract Text Fields
                    string Server = GetName(data);
                    string Email  = GetName(data);

                    //32 bit fields
                    long Serial  = (data[position++] & byte.MaxValue) << 24 | (data[position++] & byte.MaxValue) << 16 | (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;
                    long Refresh = (data[position++] & byte.MaxValue) << 24 | (data[position++] & byte.MaxValue) << 16 | (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;
                    long Retry   = (data[position++] & byte.MaxValue) << 24 | (data[position++] & byte.MaxValue) << 16 | (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;
                    long Expire  = (data[position++] & byte.MaxValue) << 24 | (data[position++] & byte.MaxValue) << 16 | (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;
                    long Minimum = (data[position++] & byte.MaxValue) << 24 | (data[position++] & byte.MaxValue) << 16 | (data[position++] & byte.MaxValue) << 8 | data[position++] & byte.MaxValue;

                    Container.Add(new SOA(ResourceName, ResourceType, ResourceClass, TTL_Seconds, Server, Email, Serial, Refresh, Retry, Expire, Minimum));

                    break;

                case DNSResourceRecordTypes.CNAME:
                    Container.Add(new CNAME(ResourceName, ResourceClass, TTL_Seconds, GetName(data)));
                    break; 

                case DNSResourceRecordTypes.MINFO:
                    Container.Add(new MINFO(ResourceName, ResourceClass, TTL_Seconds, GetName(data)));
                    break;

                case DNSResourceRecordTypes.NS:
                    Container.Add(new NS(ResourceName, ResourceClass, TTL_Seconds, GetName(data)));
                    break;

                case DNSResourceRecordTypes.PTR:
                    Container.Add(new PTR(ResourceName, ResourceClass, TTL_Seconds, GetName(data)));
                    break;

                case DNSResourceRecordTypes.TXT:
                    Container.Add(new TXT(ResourceName, ResourceClass, TTL_Seconds, GetName(data)));
                    break;

                case DNSResourceRecordTypes.MX:
                    int Rank = (data[position++] << 8) | (data[position++] & byte.MaxValue);
                    Container.Add(new MX(ResourceName, ResourceType, ResourceClass, TTL_Seconds, Rank, GetName(data)));
                    break;

                default:
                    Trace.WriteLine("Resource type did not match: " + ResourceType.ToString(), "RUY QDNS");
                    break;

            }

        }

        /// <summary>
        /// Wrapper for not so pretty code =) 
        /// </summary>
        /// <returns></returns>
        private string GetName(Byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            position = ExtractName(data, position, sb);
            return sb.ToString(); 
        }

        /// <summary>
        /// Gets name string segments from byte array. 
        /// Uses the DNS "compression" support 
        /// that gives a pointer to a previous 
        /// occurrence of repeat names. 
        /// -- not so pretty, consider killing
        /// </summary>
        /// <param name="position">Current Byte Array Reading Position</param>
        /// <returns>New Global Cursor Position</returns>
        private int ExtractName(Byte[] data, int ResourceDataCursor, StringBuilder Name)
        {
            //Get label for how many characters to extract in this segment
            int LengthLabel = (data[ResourceDataCursor++] & byte.MaxValue);

            if(LengthLabel == 0) 
            {
                return ResourceDataCursor;
            }

            do
            {

                if ((LengthLabel & 0xC0) == 0xC0)
                {
                    if (ResourceDataCursor >= length)
                    {
                        return -1;
                    }

                    //Compression OffsetID for RDATA Compression
                    int CompressionOffsetID = ((LengthLabel & 0x3F) << 8) | (data[ResourceDataCursor++] & byte.MaxValue);
                    ExtractName(data, CompressionOffsetID, Name);
                    return ResourceDataCursor;
                }
                else
                {

                    if ((ResourceDataCursor + LengthLabel) > length)
                    {
                        return -1;
                    }

                    Name.Append(Encoding.ASCII.GetString(data, ResourceDataCursor, LengthLabel));
                    ResourceDataCursor += LengthLabel;
                }

                if (ResourceDataCursor > length) 
                {
                    return -1;
                }

                LengthLabel = data[ResourceDataCursor++] & byte.MaxValue;

                //if new length label is larger than 0, we have another segment
                //so append dot. 
                if (LengthLabel != 0) 
                {
                    Name.Append(".");
                }

            }
            while (LengthLabel != 0);

            return ResourceDataCursor;
        }

    }

}
