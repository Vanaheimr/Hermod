/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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
        private readonly DNSCache                             dnsCache;

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

            this.dnsCache         = new DNSCache();
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


        #region (private) ReadResponse(Origin, ExpectedTransactionId, DNSResponseStream)

        private DNSInfo ReadResponse(IPSocket  Origin,
                                     Int32     ExpectedTransactionId,
                                     Stream    DNSResponseStream)
        {

            #region DNS Header

            var requestId       = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) + (DNSResponseStream.ReadByte() & Byte.MaxValue);

            if (ExpectedTransactionId != requestId)
                //throw new Exception("Security Alert: Mallory might send us faked DNS replies! [" + ExpectedTransactionId + " != " + requestId + "]");
                return DNSInfo.Invalid(Origin,
                                       requestId);

            var Byte2           = DNSResponseStream.ReadByte();
            var IS              = (Byte2 & 128) == 128;
            var OpCode          = (Byte2 >> 3 & 15);
            var AA              = (Byte2 & 4) == 4;
            var TC              = (Byte2 & 2) == 2;
            var RD              = (Byte2 & 1) == 1;

            var Byte3           = DNSResponseStream.ReadByte();
            var RA              = (Byte3 & 128) == 128;
            var Z               = (Byte3 & 1);    //reserved, not used
            var ResponseCode    = (DNSResponseCodes) (Byte3 & 15);

            var QuestionCount   = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);
            var AnswerCount     = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);
            var AuthorityCount  = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);
            var AdditionalCount = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);

            #endregion

            //ToDo: Does this make sense?
            #region Process Questions

            DNSResponseStream.Seek(12, SeekOrigin.Begin);

            for (var i = 0; i < QuestionCount; ++i) {
                var questionName  = DNSTools.ExtractName(DNSResponseStream);
                var typeId        = (UInt16)          ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8 | DNSResponseStream.ReadByte() & Byte.MaxValue);
                var classId       = (DNSQueryClasses) ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8 | DNSResponseStream.ReadByte() & Byte.MaxValue);
            }

            #endregion

            var answers            = new List<ADNSResourceRecord>();
            var authorities        = new List<ADNSResourceRecord>();
            var additionalRecords  = new List<ADNSResourceRecord>();

            for (var i = 0; i < AnswerCount; ++i)
                answers.          Add(ReadResourceRecord(DNSResponseStream));

            for (var i = 0; i < AuthorityCount; ++i)
                authorities.      Add(ReadResourceRecord(DNSResponseStream));

            for (var i = 0; i < AdditionalCount; ++i)
                additionalRecords.Add(ReadResourceRecord(DNSResponseStream));

            return new DNSInfo(Origin,
                               requestId,
                               AA,
                               TC,
                               RD,
                               RA,
                               ResponseCode,

                               answers,
                               authorities,
                               additionalRecords,

                               true,
                               false,
                               TimeSpan.Zero);

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

            dnsCache.Add(DomainName,
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

            dnsCache.Add(DomainName,
                          IPSocket.LocalhostV4(IPPort.DNS),
                          ARecord);

        }

        #endregion


        #region Query(DomainName, params ResourceRecordTypes)

        public Task<DNSInfo> Query(String           DomainName,
                                   params UInt16[]  ResourceRecordTypes)
        {

            #region Initial checks

            if (DomainName.IsNullOrEmpty() || !DNSServers.Any())
                return Task.FromResult(new DNSInfo(Origin:               new IPSocket(IPv4Address.Localhost,
                                                                                      IPPort.DNS),
                                                   QueryId:              0,
                                                   IsAuthorativeAnswer:  false,
                                                   IsTruncated:          false,
                                                   RecursionDesired:     true,
                                                   RecursionAvailable:   false,
                                                   ResponseCode:         DNSResponseCodes.NameError,
                                                   Answers:              Array.Empty<ADNSResourceRecord>(),
                                                   Authorities:          Array.Empty<ADNSResourceRecord>(),
                                                   AdditionalRecords:    Array.Empty<ADNSResourceRecord>(),
                                                   IsValid:              true,
                                                   IsTimeout:            false,
                                                   Timeout:              QueryTimeout));

            if (ResourceRecordTypes.Length == 0)
                ResourceRecordTypes = new UInt16[1] { 255 };

            #endregion

            #region Try to get answers from the DNS cache

            var cachedResults = dnsCache.GetDNSInfo(DomainName);

            if (cachedResults is not null)
            {

                var resourceRecords = cachedResults.Answers.
                                          Where  (resourceRecord => ResourceRecordTypes.Contains(resourceRecord.Type)).
                                          ToArray();

                if (resourceRecords.Any())
                    return Task.FromResult(cachedResults);

            }

            #endregion


            var dnsQuery = new DNSQuery(DomainName,
                                        RecursionDesired,
                                        ResourceRecordTypes);

            #region Query all DNS server(s) in parallel...

            var allDNSServerRequests = DNSServers.Select(dnsServer =>

                Task<DNSInfo>.Factory.StartNew(() => {

                    var data = new Byte[512];
                    Int32  length;
                    Socket? socket = null;

                    try
                    {

                        var serverAddress  = System.Net.IPAddress.Parse(dnsServer.IPAddress.ToString());
                        var endPoint       = (EndPoint) new IPEndPoint(serverAddress, dnsServer.Port.ToInt32());
                        socket             = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout,    (Int32) QueryTimeout.TotalMilliseconds);
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (Int32) QueryTimeout.TotalMilliseconds);
                        socket.Connect(endPoint);
                        socket.SendTo(dnsQuery.Serialize(), endPoint);

                        length = socket.ReceiveFrom(data, ref endPoint);

                    }
                    catch (SocketException se)
                    {

                        if (se.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                            return new DNSInfo(Origin:               new IPSocket(dnsServer.IPAddress,
                                                                                  dnsServer.Port),
                                               QueryId:              dnsQuery.TransactionId,
                                               IsAuthorativeAnswer:  false,
                                               IsTruncated:          false,
                                               RecursionDesired:     false,
                                               RecursionAvailable:   false,
                                               ResponseCode:         DNSResponseCodes.ServerFailure,
                                               Answers:              Array.Empty<ADNSResourceRecord>(),
                                               Authorities:          Array.Empty<ADNSResourceRecord>(),
                                               AdditionalRecords:    Array.Empty<ADNSResourceRecord>(),
                                               IsValid:              true,
                                               IsTimeout:            false,
                                               Timeout:              QueryTimeout);

                        // A SocketException might be thrown after the timeout was reached!
                        //throw new Exception("DNS server '" + DNSServer + "' did not respond within " + QueryTimeout.TotalSeconds + " seconds!");
                        return DNSInfo.TimedOut(new IPSocket(dnsServer.IPAddress,
                                                             dnsServer.Port),
                                                dnsQuery.TransactionId,
                                                QueryTimeout);

                    }
                    catch
                    {
                        // A SocketException might be thrown after the timeout was reached!
                        //throw new Exception("DNS server '" + DNSServer + "' did not respond within " + QueryTimeout.TotalSeconds + " seconds!");
                        return DNSInfo.TimedOut(new IPSocket(dnsServer.IPAddress,
                                                             dnsServer.Port),
                                                dnsQuery.TransactionId,
                                                QueryTimeout);
                    }
                    finally
                    {
                        if (socket is not null)
                            socket.Shutdown(SocketShutdown.Both);
                    }

                    return ReadResponse(dnsServer,
                                        dnsQuery.TransactionId,
                                        new MemoryStream(data));

                },
                TaskCreationOptions.AttachedToParent)

            ).ToList();

            #endregion


            Task<DNSInfo>? firstResponse = null;

            if (allDNSServerRequests.Any())
            {

                do
                {

                    try
                    {

                        // Return first/fastest reply
                        firstResponse = Task.WhenAny(allDNSServerRequests).Result;

                        allDNSServerRequests.Remove(firstResponse);

                        // Cache first useful response...
                        if (firstResponse.Result?.ResponseCode == DNSResponseCodes.NoError)
                        {

                            AddToCache(DomainName,
                                       firstResponse.Result);

                            break;

                        }

                    }
                    catch
                    { }

                }
                while (allDNSServerRequests.Count > 0);

            }

            return Task.FromResult(firstResponse?.Result ??
                                       new DNSInfo(Origin:               new IPSocket(IPv4Address.Localhost, IPPort.DNS),
                                                   QueryId:              0,
                                                   IsAuthorativeAnswer:  false,
                                                   IsTruncated:          false,
                                                   RecursionDesired:     true,
                                                   RecursionAvailable:   false,
                                                   ResponseCode:         DNSResponseCodes.NameError,
                                                   Answers:              Array.Empty<ADNSResourceRecord>(),
                                                   Authorities:          Array.Empty<ADNSResourceRecord>(),
                                                   AdditionalRecords:    Array.Empty<ADNSResourceRecord>(),
                                                   IsValid:              true,
                                                   IsTimeout:            false,
                                                   Timeout:              QueryTimeout));

        }

        #endregion

        #region Query<T>(DomainName)

        public async Task<IEnumerable<T>> Query<T>(String DomainName)
            where T : ADNSResourceRecord
        {

            var typeIdField = typeof(T).GetField("TypeId");

            if (typeIdField is null)
                throw new ArgumentException("Constant field 'TypeId' of type '" + typeof(T).Name + "' was not found!");

            var dnsQuery = await Query(DomainName,
                                       new UInt16[] {
                                           (UInt16) typeIdField.GetValue(typeof(T))
                                       }).ConfigureAwait(false);

            return dnsQuery.Answers.
                       Where(resourceRecord => resourceRecord is T).
                       Cast<T>();

        }

        #endregion

        #region Query<T1, T2>(DomainName, Mapper)

        public async Task<IEnumerable<T2>> Query<T1, T2>(String        DomainName,
                                                         Func<T1, T2>  Mapper)
            where T1 : ADNSResourceRecord

            => (await Query<T1>(DomainName)).Select(v => Mapper(v));

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
