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
using System.Reflection;
using System.IO;

#endregion

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    public static class DNSTools
    {

        public static String ExtractName(Stream DNSStream)
        {

            var DNSName          = new StringBuilder();
            var LengthOfSegment  = 0;
            var OldPosition      = 0L;
            var Alias            = String.Empty;
            var buffer           = new Byte[512];

            do
            {

                LengthOfSegment = (DNSStream.ReadByte() & Byte.MaxValue);

                if (LengthOfSegment > 0)
                {

                    if (DNSName.Length > 0)
                        DNSName.Append(".");

                    // RDATA Compression
                    if ((LengthOfSegment & 0xC0) == 0xC0)
                    {

                        OldPosition         = DNSStream.Position;
                        DNSStream.Position  = ((LengthOfSegment & 0x3F) << 8) | (DNSStream.ReadByte() & Byte.MaxValue);
                        Alias               = ExtractName(DNSStream);
                        DNSStream.Position  = OldPosition + 1;

                        return Alias;

                    }

                    else
                    {
                        DNSStream.Read(buffer, 0, LengthOfSegment);
                        DNSName.Append(Encoding.ASCII.GetString(buffer, 0, LengthOfSegment));
                    }

                }

            }
            while (LengthOfSegment > 0);

            return DNSName.ToString();

        }

    }

    public class DNSClient
    {

        #region Data

        private readonly Dictionary<Byte, Type> RRLookup;
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

            //this.RRLookup          = new Dictionary<DNSResourceRecordTypes, Type>();

            //foreach (var _ActualType in typeof(ADNSResourceRecord).
            //                                Assembly.GetTypes().
            //                                Where(type => type.IsClass &&
            //                                     !type.IsAbstract &&
            //                                      type.IsSubclassOf(typeof(ADNSResourceRecord))))
            //{
            //    RRLookup.Add((Byte)((ADNSResourceRecord) Activator.CreateInstance(_ActualType, null)).Type, _ActualType);
            //}

        }

        #endregion


        #region Query(DomainName, params ResourceRecordTypes)

        public DNSResponse Query(String                           DomainName,
                                 params DNSResourceRecordTypes[]  ResourceRecordTypes)
        {

            if (ResourceRecordTypes.Length == 0)
                ResourceRecordTypes = new DNSResourceRecordTypes[1] { DNSResourceRecordTypes.ANY };

            // Preparing the DNS query packet
            var QueryPacket = new DNSQuery(DomainName, ResourceRecordTypes) {
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

            return ReadResponse(new MemoryStream(data));

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

            else if (typeof(T) == typeof(CNAME))
                DNSResourceRecordType = DNSResourceRecordTypes.CNAME;

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


        #region ReadResponse(DNSBuffer)

        private DNSResponse ReadResponse(Stream DNSBuffer)
        {

            #region DNS Header

            var RequestId       = ((DNSBuffer.ReadByte() & byte.MaxValue) << 8) + (DNSBuffer.ReadByte() & byte.MaxValue);

            var Byte2           = DNSBuffer.ReadByte();
            var IS              = (Byte2 & 128) == 128;
            var OpCode          = (Byte2 >> 3 & 15);
            var AA              = (Byte2 & 4) == 4;
            var TC              = (Byte2 & 2) == 2;
            var RD              = (Byte2 & 1) == 1;

            var Byte3           = DNSBuffer.ReadByte();
            var RA              = (Byte3 & 128) == 128;
            var Z               = (Byte3 & 1);    //reserved, not used
            var ResponseCode    = (DNSResponseCodes) (Byte3 & 15);

            var QuestionCount   = ((DNSBuffer.ReadByte() & byte.MaxValue) << 8) | (DNSBuffer.ReadByte() & byte.MaxValue);
            var AnswerCount     = ((DNSBuffer.ReadByte() & byte.MaxValue) << 8) | (DNSBuffer.ReadByte() & byte.MaxValue);
            var AuthorityCount  = ((DNSBuffer.ReadByte() & byte.MaxValue) << 8) | (DNSBuffer.ReadByte() & byte.MaxValue);
            var AdditionalCount = ((DNSBuffer.ReadByte() & byte.MaxValue) << 8) | (DNSBuffer.ReadByte() & byte.MaxValue);

            #endregion

            #region Process Questions

            DNSBuffer.Seek(12, SeekOrigin.Begin);

            for (var i = 0; i < QuestionCount; ++i) {
                var QuestionName  = DNSTools.ExtractName(DNSBuffer);
                var TypeId        = (DNSResourceRecordTypes) ((DNSBuffer.ReadByte() & byte.MaxValue) << 8 | DNSBuffer.ReadByte() & byte.MaxValue);
                var ClassId       = (DNSQueryClasses)        ((DNSBuffer.ReadByte() & byte.MaxValue) << 8 | DNSBuffer.ReadByte() & byte.MaxValue);
            }

            #endregion

            var Answers            = new List<ADNSResourceRecord>();
            var Authorities        = new List<ADNSResourceRecord>();
            var AdditionalRecords  = new List<ADNSResourceRecord>();

            for (var i = 0; i < AnswerCount; ++i)
                Answers.Add(GetResourceRecord(DNSBuffer));

            for (var i = 0; i < AuthorityCount; ++i)
                Authorities.Add(GetResourceRecord(DNSBuffer));

            for (var i = 0; i < AdditionalCount; ++i)
                AdditionalRecords.Add(GetResourceRecord(DNSBuffer));

            return new DNSResponse(RequestId, AA, TC, RD, RA, ResponseCode, Answers, Authorities, AdditionalRecords);

        }

        #endregion



        private ADNSResourceRecord GetResourceRecord(Stream DNSStream)
        {

            var ResourceName  = DNSTools.ExtractName(DNSStream);
            var TypeId        = (DNSResourceRecordTypes) ((DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);

            switch (TypeId)
            {

                case DNSResourceRecordTypes.A:
                    return new A(ResourceName, DNSStream);

                case DNSResourceRecordTypes.AAAA:
                    return new AAAA(ResourceName, DNSStream);

                //case DNSResourceRecordTypes.SOA:
                //    var Server   = DNSTools.ExtractName(DNSStream);
                //    var Email    = DNSTools.ExtractName(DNSStream);
                //    var Serial   = (DNSStream.ReadByte() & byte.MaxValue) << 24 | (DNSStream.ReadByte() & byte.MaxValue) << 16 | (DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue;
                //    var Refresh  = (DNSStream.ReadByte() & byte.MaxValue) << 24 | (DNSStream.ReadByte() & byte.MaxValue) << 16 | (DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue;
                //    var Retry    = (DNSStream.ReadByte() & byte.MaxValue) << 24 | (DNSStream.ReadByte() & byte.MaxValue) << 16 | (DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue;
                //    var Expire   = (DNSStream.ReadByte() & byte.MaxValue) << 24 | (DNSStream.ReadByte() & byte.MaxValue) << 16 | (DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue;
                //    var Minimum  = (DNSStream.ReadByte() & byte.MaxValue) << 24 | (DNSStream.ReadByte() & byte.MaxValue) << 16 | (DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue;
                //    return new SOA(ResourceName, ClassId, TTL_Seconds, Server, Email, Serial, Refresh, Retry, Expire, Minimum);

                case DNSResourceRecordTypes.CNAME:
                    return new CNAME(ResourceName, DNSStream);

                case DNSResourceRecordTypes.MINFO:
                    return new MINFO(ResourceName, DNSStream);

                case DNSResourceRecordTypes.NS:
                    return new NS(ResourceName, DNSStream);

                case DNSResourceRecordTypes.PTR:
                    return new PTR(ResourceName, DNSStream);

                case DNSResourceRecordTypes.TXT:
                    return new TXT(ResourceName, DNSStream);

                //case DNSResourceRecordTypes.MX:
                //    int Rank = (DNSStream.ReadByte() << 8) | (DNSStream.ReadByte() & byte.MaxValue);
                //    return new MX(ResourceName, TypeId, ClassId, TTL_Seconds, Rank, DNSTools.ExtractName(DNSStream));

                default:
                    Trace.WriteLine("Resource type did not match: " + TypeId.ToString(), "RUY QDNS");
                    break;

            }

            return null;

        }



    }

}
