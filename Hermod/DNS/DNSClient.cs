/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

        /// <summary>
        /// The DNS servers used by this DNS client.
        /// </summary>
        public IEnumerable<IPSocket>  DNSServers          { get; }

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan               QueryTimeout        { get; set; }

        /// <summary>
        /// Whether DNS recursion is desired.
        /// </summary>
        public Boolean                RecursionDesired    { get; set; }

        #endregion

        #region Constructor(s)

        #region DNSClient(DNSServer)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        public DNSClient(IIPAddress DNSServer)

            : this(new IPSocket[] { new IPSocket(DNSServer, IPPort.DNS) })

        { }

        #endregion

        #region DNSClient(DNSServer, Port)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        /// <param name="Port">The IP port of the DNS server to query.</param>
        public DNSClient(IIPAddress DNSServer, IPPort Port)

            : this(new IPSocket[] { new IPSocket(DNSServer, Port) })

        { }

        #endregion

        #region DNSClient(DNSServers)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServers">A list of DNS servers to query.</param>
        public DNSClient(IEnumerable<IIPAddress> DNSServers)

            : this(DNSServers.Select(IPAddress => new IPSocket(IPAddress, IPPort.DNS)))

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

            : this(new IPSocket[0],
                   SearchForIPv4DNSServers,
                   SearchForIPv6DNSServers)

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
                         Boolean                SearchForIPv4DNSServers = false,
                         Boolean                SearchForIPv6DNSServers = false)

        {

            this._DNSCache         = new DNSCache();
            this.RecursionDesired  = true;
            this.QueryTimeout      = TimeSpan.FromSeconds(23.5);

            var _DNSServers        = new List<IPSocket>(ManualDNSServers);

            #region Search for IPv4/IPv6 DNS Servers...

            if (SearchForIPv4DNSServers)
                _DNSServers.AddRange(NetworkInterface.
                                         GetAllNetworkInterfaces().
                                         Where     (NI        => NI.OperationalStatus == OperationalStatus.Up).
                                         SelectMany(NI        => NI.GetIPProperties().DnsAddresses).
                                         Where     (IPAddress => IPAddress.AddressFamily == AddressFamily.InterNetwork).
                                         Select    (IPAddress => new IPSocket(new IPv4Address(IPAddress), IPPort.DNS)));

            if (SearchForIPv6DNSServers)
                _DNSServers.AddRange(NetworkInterface.
                                         GetAllNetworkInterfaces().
                                         Where     (NI        => NI.OperationalStatus == OperationalStatus.Up).
                                         SelectMany(NI        => NI.GetIPProperties().DnsAddresses).
                                         Where     (IPAddress => IPAddress.AddressFamily == AddressFamily.InterNetworkV6).
                                         Select    (IPAddress => new IPSocket(new IPv6Address(IPAddress), IPPort.DNS)));

            #endregion

            this.DNSServers        = _DNSServers;

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

            return new DNSInfo(Origin,
                               RequestId,
                               AA,
                               TC,
                               RD,
                               RA,
                               ResponseCode,
                               Answers,
                               Authorities,
                               AdditionalRecords);

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

            if (DomainName.IsNullOrEmpty() || DNSInformation == null)
                return;

            _DNSCache.Add(DomainName,
                          DNSInformation);

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

            if (DomainName.IsNullOrEmpty() || ARecord == null)
                return;

            _DNSCache.Add(DomainName,
                          IPSocket.LocalhostV4(IPPort.DNS),
                          ARecord);

        }

        #endregion


        #region Query(DomainName, params ResourceRecordTypes)

        public async Task<DNSInfo> Query(String           DomainName,
                                         params UInt16[]  ResourceRecordTypes)
        {

            #region Initial checks

            if (DomainName.IsNullOrEmpty() || !DNSServers.Any())
                return new DNSInfo(Origin:               new IPSocket(IPv4Address.Localhost, IPPort.DNS),
                                   QueryId:              0,
                                   IsAuthorativeAnswer:  false,
                                   IsTruncated:          false,
                                   RecursionDesired:     true,
                                   RecursionAvailable:   false,
                                   ResponseCode:         DNSResponseCodes.NameError,
                                   Answers:              new ADNSResourceRecord[0],
                                   Authorities:          new ADNSResourceRecord[0],
                                   AdditionalRecords:    new ADNSResourceRecord[0]);

            if (ResourceRecordTypes.Length == 0)
                ResourceRecordTypes = new UInt16[1] { 255 };

            Task<DNSInfo> FirstReply = null;

            #endregion

            #region Try to get answers from the DNS cache

            var dnsInfo = _DNSCache.GetDNSInfo(DomainName);

            if (dnsInfo != null)
            {

                var resourceRecords = dnsInfo.Answers.
                                              Where  (resourceRecord => ResourceRecordTypes.Contains(resourceRecord.Type)).
                                              ToArray();

                if (resourceRecords.Any())
                    return dnsInfo;

            }

            #endregion


            #region Prepare DNS query packet

            var QueryPacket = new DNSQuery(DomainName,
                                           RecursionDesired,
                                           ResourceRecordTypes);

            #endregion

            #region Query all DNS server(s) in parallel...

            var AllDNSServerRequests = DNSServers.Select(DNSServer => {

                return Task<DNSInfo>.Factory.StartNew(() => {

                    var data = new Byte[512];
                    Int32  length;
                    Socket socket = null;

                    try
                    {

                        var serverAddress  = System.Net.IPAddress.Parse(DNSServer.IPAddress.ToString());
                        var endPoint       = (EndPoint) new IPEndPoint(serverAddress, DNSServer.Port.ToInt32());
                        socket             = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout,    (Int32) QueryTimeout.TotalMilliseconds);
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (Int32) QueryTimeout.TotalMilliseconds);
                        socket.Connect(endPoint);
                        socket.SendTo(QueryPacket.Serialize(), endPoint);

                        length = socket.ReceiveFrom(data, ref endPoint);

                    }
                    catch (SocketException se)
                    {

                        if (se.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                            return new DNSInfo(new IPSocket(DNSServer.IPAddress, DNSServer.Port),
                                               QueryPacket.TransactionId,
                                               false,
                                               false,
                                               false,
                                               false,
                                               DNSResponseCodes.ServerFailure,
                                               new ADNSResourceRecord[0],
                                               new ADNSResourceRecord[0],
                                               new ADNSResourceRecord[0]);

                        // A SocketException might be thrown after the timeout was reached!
                        throw new Exception("DNS server '" + DNSServer + "' did not respond within " + QueryTimeout.TotalSeconds + " seconds!");

                    }
                    catch (Exception e)
                    {
                        // A SocketException might be thrown after the timeout was reached!
                        throw new Exception("DNS server '" + DNSServer + "' did not respond within " + QueryTimeout.TotalSeconds + " seconds!");
                    }
                    finally
                    {
                        if (socket != null)
                            socket.Shutdown(SocketShutdown.Both);
                    }

                    return ReadResponse(DNSServer, QueryPacket.TransactionId, new MemoryStream(data));

                },
                TaskCreationOptions.AttachedToParent);

            }).ToList();

            #endregion


            if (AllDNSServerRequests.Count > 0)
            {

                try
                {

                    do
                    {

                        // Return first/fastest reply
                        FirstReply = Task.WhenAny(AllDNSServerRequests).Result;

                        AllDNSServerRequests.Remove(FirstReply);

                        // Cache first good response...
                        if (FirstReply.Result?.ResponseCode == DNSResponseCodes.NoError)
                        {
                            AddToCache(DomainName, FirstReply.Result);
                            break;
                        }

                    }
                    while (AllDNSServerRequests.Count > 0);

                }
                catch (Exception e)
                {

                    while (e.InnerException != null)
                        e = e.InnerException;

                    Debug.WriteLine("[" + DateTime.UtcNow + "] DNS exception " + e.Message);

                }

            }

            return FirstReply?.Result ??
                       new DNSInfo(Origin:               new IPSocket(IPv4Address.Localhost, IPPort.DNS),
                                   QueryId:              0,
                                   IsAuthorativeAnswer:  false,
                                   IsTruncated:          false,
                                   RecursionDesired:     true,
                                   RecursionAvailable:   false,
                                   ResponseCode:         DNSResponseCodes.NameError,
                                   Answers:              new ADNSResourceRecord[0],
                                   Authorities:          new ADNSResourceRecord[0],
                                   AdditionalRecords:    new ADNSResourceRecord[0]);

        }

        #endregion

        #region Query<T>(DomainName)

        public async Task<IEnumerable<T>> Query<T>(String DomainName)
            where T : ADNSResourceRecord
        {

            var TypeIdField = typeof(T).GetField("TypeId");

            if (TypeIdField == null)
                throw new ArgumentException("Constant field 'TypeId' of type '" + typeof(T).Name + "' was not found!");

            var QueryTask = await Query(
                                      DomainName,
                                      new UInt16[1] {
                                          (UInt16) TypeIdField.GetValue(typeof(T))
                                      }
                                  ).ConfigureAwait(false);

            return QueryTask.Answers.
                             Where(v => v.GetType() == typeof(T)).
                             Cast<T>().
                             Where(v => v != null);

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
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => "Using DNS servers: " +
               DNSServers.SafeSelect(socket => socket.ToString()).AggregateWith(", ");

        #endregion

    }

}
