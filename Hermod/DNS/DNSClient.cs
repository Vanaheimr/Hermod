/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS client.
    /// </summary>
    public class DNSClient
    {

        #region Data

        private readonly Dictionary<UInt16, ConstructorInfo>  _RRLookup;
        private readonly DNSCache                             _DNSCache;

        #endregion

        #region Properties

        #region DNSServers

        private readonly List<IPSocket> _DNSServers;

        /// <summary>
        /// The DNS servers used by this DNS client.
        /// </summary>
        public IEnumerable<IPSocket> DNSServers
        {
            get
            {
                return _DNSServers;
            }
        }

        #endregion

        #region QueryTimeout

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan  QueryTimeout  { get; set; }

        #endregion

        #region RecursionDesired

        /// <summary>
        /// Wether DNS recursion is desired.
        /// </summary>
        public Boolean  RecursionDesired  { get; set; }

        #endregion

        #endregion

        #region Constructor(s)

        #region DNSClient(DNSServer)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        public DNSClient(IIPAddress DNSServer)

            : this(new IPSocket[1] { new IPSocket(DNSServer, new IPPort(53)) })

        { }

        #endregion

        #region DNSClient(DNSServer, Port)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        /// <param name="Port">The IP port of the DNS server to query.</param>
        public DNSClient(IIPAddress DNSServer, IPPort Port)

            : this(new IPSocket[1] { new IPSocket(DNSServer, Port) })

        { }

        #endregion

        #region DNSClient(DNSServers)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServers">A list of DNS servers to query.</param>
        public DNSClient(IEnumerable<IIPAddress> DNSServers)

            : this(DNSServers.Select(IPAddress => new IPSocket(IPAddress, new IPPort(53))))

        { }

        #endregion

        #region DNSClient(DNSServers, Port)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServers">A list of DNS servers to query.</param></param>
        /// <param name="Port">The common IP port of the DNS servers to query.</param>
        public DNSClient(IEnumerable<IIPAddress> DNSServers, IPPort Port)

            : this(DNSServers.Select(IPAddress => new IPSocket(IPAddress, Port)))

        { }

        #endregion

        #region DNSClient(SearchForIPv4DNSServers = true, SearchForIPv6DNSServers = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="SearchForIPv4DNSServers">If yes, the DNS client will query a list of DNS servers from the IPv4 network configuration.</param>
        /// <param name="SearchForIPv6DNSServers">If yes, the DNS client will query a list of DNS servers from the IPv6 network configuration.</param>
        public DNSClient(Boolean SearchForIPv4DNSServers = true,
                         Boolean SearchForIPv6DNSServers = true)

            : this(new IPSocket[0], SearchForIPv4DNSServers, SearchForIPv6DNSServers)

        { }

        #endregion

        #region DNSClient(ManualDNSServers, SearchForIPv4DNSServers = true, SearchForIPv6DNSServers = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="ManualDNSServers">A list of manually configured DNS servers to query.</param>
        /// <param name="SearchForIPv4DNSServers">If yes, the DNS client will query a list of DNS servers from the IPv4 network configuration.</param>
        /// <param name="SearchForIPv6DNSServers">If yes, the DNS client will query a list of DNS servers from the IPv6 network configuration.</param>
        public DNSClient(IEnumerable<IPSocket>  ManualDNSServers,
                         Boolean                SearchForIPv4DNSServers = true,
                         Boolean                SearchForIPv6DNSServers = true)

        {

            this._DNSCache         = new DNSCache();
            this.RecursionDesired  = true;
            this.QueryTimeout      = TimeSpan.FromSeconds(23.5);

            _DNSServers = new List<IPSocket>(ManualDNSServers);

            #region Search for IPv4/IPv6 DNS Servers...

            if (SearchForIPv4DNSServers)
                _DNSServers.AddRange(NetworkInterface.
                                         GetAllNetworkInterfaces().
                                         Where     (NI        => NI.OperationalStatus == OperationalStatus.Up).
                                         SelectMany(NI        => NI.GetIPProperties().DnsAddresses).
                                         Where     (IPAddress => IPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).
                                         Select    (IPAddress => new IPSocket(new IPv4Address(IPAddress), new IPPort(53))));

            if (SearchForIPv6DNSServers)
                _DNSServers.AddRange(NetworkInterface.
                                         GetAllNetworkInterfaces().
                                         Where     (NI        => NI.OperationalStatus == OperationalStatus.Up).
                                         SelectMany(NI        => NI.GetIPProperties().DnsAddresses).
                                         Where     (IPAddress => IPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6).
                                         Select    (IPAddress => new IPSocket(new IPv6Address(IPAddress), new IPPort(53))));

            #endregion

            #region Reflect ResourceRecordTypes

            this._RRLookup          = new Dictionary<UInt16, ConstructorInfo>();

            FieldInfo        TypeIdField;
            ConstructorInfo  Constructor;

            foreach (var _ActualType in typeof(ADNSResourceRecord).
                                            Assembly.GetTypes().
                                            Where(type => type.IsClass &&
                                                 !type.IsAbstract &&
                                                  type.IsSubclassOf(typeof(ADNSResourceRecord))))
            {

                TypeIdField = _ActualType.GetField("TypeId");

                if (TypeIdField == null)
                    throw new ArgumentException("Constant field 'TypeId' of type '" + _ActualType.Name + "' was not found!");

                Constructor = _ActualType.GetConstructor(new Type[2] { typeof(String), typeof(Stream) });

                if (Constructor == null)
                    throw new ArgumentException("Constructor<String, Stream> of type '" + _ActualType.Name + "' was not found!");

                _RRLookup.Add((UInt16) TypeIdField.GetValue(_ActualType), Constructor);

            }

            #endregion

        }

        #endregion

        #endregion


        #region (private) ReadResponse(Origin, ExpectedTransactionId, DNSBuffer)

        private DNSInfo ReadResponse(IPSocket Origin, Int32 ExpectedTransactionId, Stream DNSBuffer)
        {

            #region DNS Header

            var RequestId       = ((DNSBuffer.ReadByte() & Byte.MaxValue) << 8) + (DNSBuffer.ReadByte() & Byte.MaxValue);

            if (ExpectedTransactionId != RequestId)
                throw new Exception("Security Alert: Mallory might send us faked DNS replies! [" + ExpectedTransactionId + " != " + RequestId + "]");

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

            var QuestionCount   = ((DNSBuffer.ReadByte() & Byte.MaxValue) << 8) | (DNSBuffer.ReadByte() & Byte.MaxValue);
            var AnswerCount     = ((DNSBuffer.ReadByte() & Byte.MaxValue) << 8) | (DNSBuffer.ReadByte() & Byte.MaxValue);
            var AuthorityCount  = ((DNSBuffer.ReadByte() & Byte.MaxValue) << 8) | (DNSBuffer.ReadByte() & Byte.MaxValue);
            var AdditionalCount = ((DNSBuffer.ReadByte() & Byte.MaxValue) << 8) | (DNSBuffer.ReadByte() & Byte.MaxValue);

            #endregion

            //ToDo: Does this make sense?
            #region Process Questions

            DNSBuffer.Seek(12, SeekOrigin.Begin);

            for (var i = 0; i < QuestionCount; ++i) {
                var QuestionName  = DNSTools.ExtractName(DNSBuffer);
                var TypeId        = (UInt16)          ((DNSBuffer.ReadByte() & Byte.MaxValue) << 8 | DNSBuffer.ReadByte() & Byte.MaxValue);
                var ClassId       = (DNSQueryClasses) ((DNSBuffer.ReadByte() & Byte.MaxValue) << 8 | DNSBuffer.ReadByte() & Byte.MaxValue);
            }

            #endregion

            var Answers            = new List<ADNSResourceRecord>();
            var Authorities        = new List<ADNSResourceRecord>();
            var AdditionalRecords  = new List<ADNSResourceRecord>();

            for (var i = 0; i < AnswerCount; ++i)
                Answers.Add(ReadResourceRecord(DNSBuffer));

            for (var i = 0; i < AuthorityCount; ++i)
                Authorities.Add(ReadResourceRecord(DNSBuffer));

            for (var i = 0; i < AdditionalCount; ++i)
                AdditionalRecords.Add(ReadResourceRecord(DNSBuffer));

            return new DNSInfo(Origin, RequestId, AA, TC, RD, RA, ResponseCode, Answers, Authorities, AdditionalRecords);

        }

        #endregion

        #region (private) ReadResourceRecord(DNSStream)

        private ADNSResourceRecord ReadResourceRecord(Stream DNSStream)
        {

            var ResourceName  = DNSTools.ExtractName(DNSStream);
            var TypeId        = (UInt16) ((DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);

            ConstructorInfo Constructor;

            if (_RRLookup.TryGetValue(TypeId, out Constructor))
                return (ADNSResourceRecord) Constructor.Invoke(new Object[2] {
                                                                   ResourceName,
                                                                   DNSStream
                                                               });

            Debug.WriteLine("Unknown DNS resource record '" + TypeId + "' for '" + ResourceName + "' received!");

            return null;

        }

        #endregion

        #region (private) AddToCache(DomainName, DNSInformation)

        /// <summary>
        /// Add a DNS cache entry.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        /// <param name="DNSInformation">The DNS information</param>
        private void AddToCache(String    DomainName,
                                DNSInfo   DNSInformation)
        {

            if (DNSInformation.Answers != null)
                DNSInformation.
                    Answers.
                    ForEach(ResourceRecord => _DNSCache.Add(DomainName,
                                                            DNSInformation.Origin,
                                                            ResourceRecord));

        }

        #endregion

        #region AddToCache(DomainName, ARecord)

        /// <summary>
        /// Add a DNS cache entry.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        /// <param name="ARecord">A DNS record</param>
        public void AddToCache(String  DomainName,
                               A       ARecord)
        {

            _DNSCache.Add(DomainName, IPSocket.Localhost(new IPPort(53)), ARecord);

        }

        #endregion


        #region Query(DomainName, params ResourceRecordTypes)

        public async Task<DNSInfo> Query(String           DomainName,
                                         params UInt16[]  ResourceRecordTypes)
        {

            if (ResourceRecordTypes.Length == 0)
                ResourceRecordTypes = new UInt16[1] { 255 };

            #region Try to get an answer from the DNS cache

            var DNSInfo = _DNSCache.GetDNSInfo(DomainName);

            if (DNSInfo != null)
            {
                //var tcs = new TaskCompletionSource<DNSInfo>();
                //tcs.SetResult(DNSInfo);
                //return tcs.Task;
                return DNSInfo;
            }

            #endregion

            #region Query all DNS server(s) in parallel...

            // Preparing the DNS query packet
            var QueryPacket = new DNSQuery(DomainName, RecursionDesired, ResourceRecordTypes);

            var AllDNSServerRequests = DNSServers.Select(DNSServer => {

                return Task<DNSInfo>.Factory.StartNew(() => {

                    var data = new Byte[512];
                    Int32  length;
                    Socket socket = null;

                    try
                    {

                        var serverAddress  = IPAddress.Parse(DNSServer.IPAddress.ToString());
                        var endPoint       = (EndPoint) new IPEndPoint(serverAddress, DNSServer.Port.ToInt32());
                        socket             = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout,    (Int32) QueryTimeout.TotalMilliseconds);
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (Int32) QueryTimeout.TotalMilliseconds);
                        socket.Connect(endPoint);
                        socket.SendTo(QueryPacket.Serialize(), endPoint);

                        length = socket.ReceiveFrom(data, ref endPoint);

                    }
                    catch (Exception e)
                    {
                        // A SocketException might be thrown after the timeout was reached!
                        throw new Exception("DNS server '" + DNSServer.ToString() + "' did not respond within " + QueryTimeout.TotalSeconds + " seconds!");
                    }
                    finally
                    {
                        if (socket != null)
                            socket.Shutdown(SocketShutdown.Both);
                    }

                    return ReadResponse(DNSServer, QueryPacket.TransactionId, new MemoryStream(data));

                },
                TaskCreationOptions.AttachedToParent);

            }).ToArray();

            #endregion

            #region Cache all replies...

            AllDNSServerRequests.
                ForEach(DNSServerTask => DNSServerTask.ContinueWith(x => {
                                                           try
                                                           {
                                                               if (x.Result != null)
                                                                   AddToCache(DomainName, x.Result);
                                                           }
                                                           catch (Exception e)
                                                           {
                                                               Debug.WriteLine("[" + DateTime.Now + "] DNS exception " + e.Message);
                                                           }
                                                       }));

            #endregion

            // Return first/fastest reply
            var FirstReply = Task.WhenAny(AllDNSServerRequests);

            return FirstReply.Result.Result;

        }

        #endregion

        #region Query<T>(DomainName)

        public async Task<IEnumerable<T>> Query<T>(String DomainName)
            where T : ADNSResourceRecord
        {

            var TypeIdField = typeof(T).GetField("TypeId");

            if (TypeIdField == null)
                throw new ArgumentException("Constant field 'TypeId' of type '" + typeof(T).Name + "' was not found!");

            return Query(DomainName,
                         new UInt16[1] { (UInt16) TypeIdField.GetValue(typeof(T)) }).

                       ContinueWith(QueryTask => QueryTask.Result.
                                                     Answers.
                                                     Where(v => v.GetType() == typeof(T)).
                                                     Cast<T>()).Result;

        }

        #endregion

        #region QueryFirst<T>(DomainName)

        public Task<T> QueryFirst<T>(String DomainName)
            where T : ADNSResourceRecord
        {

            return Query<T>(DomainName).
                       ContinueWith(QueryTask => QueryTask.Result.
                                                     FirstOrDefault());

        }

        #endregion

        #region Query<T1, T2>(DomainName, Mapper)

        public Task<IEnumerable<T2>> Query<T1, T2>(String DomainName, Func<T1, T2> Mapper)
            where T1 : ADNSResourceRecord
        {

            return Query<T1>(DomainName).
                       ContinueWith(QueryTask => QueryTask.Result.
                                                     Select(v => Mapper(v)));

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Get a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return "Using DNS servers: " + _DNSServers.Select(v => v.ToString()).AggregateWith(", ");
        }

        #endregion

    }

}
