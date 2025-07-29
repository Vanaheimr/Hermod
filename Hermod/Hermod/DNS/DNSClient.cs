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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS client.
    /// </summary>
    public class DNSClient
    {

        #region Data

        private readonly ConcurrentDictionary<DNSResourceRecordTypes, ConstructorInfo>  rrLookup_DomainName = [];
        private readonly ConcurrentDictionary<DNSResourceRecordTypes, ConstructorInfo>  rrLookup_DNSServiceName = [];

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

        /// <summary>
        /// The DNS cache used by this DNS client.
        /// </summary>
        public DNSCache               DNSCache            { get; }

        #endregion

        #region Constructor(s)

        #region DNSClient(DNSServer)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        public DNSClient(IIPAddress DNSServer)

            : this([ new IPSocket(DNSServer, IPPort.DNS) ])

        { }

        #endregion

        #region DNSClient(DNSServer, Port)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        /// <param name="Port">The IP port of the DNS server to query.</param>
        public DNSClient(IIPAddress DNSServer, IPPort Port)

            : this([ new IPSocket(DNSServer, Port) ])

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

            : this([],
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

            this.DNSCache          = new DNSCache();
            this.RecursionDesired  = true;
            this.QueryTimeout      = TimeSpan.FromSeconds(23.5);

            var dnsServers         = new List<IPSocket>(ManualDNSServers);

            #region Search for IPv4/IPv6 DNS Servers...

            if (SearchForIPv4DNSServers)
                dnsServers.AddRange(NetworkInterface.
                                        GetAllNetworkInterfaces().
                                        Where     (NI        => NI.OperationalStatus == OperationalStatus.Up).
                                        SelectMany(NI        => NI.GetIPProperties().DnsAddresses).
                                        Where     (IPAddress => IPAddress.AddressFamily == AddressFamily.InterNetwork).
                                        Select    (IPAddress => new IPSocket(new IPv4Address(IPAddress), IPPort.DNS)));

            if (SearchForIPv6DNSServers)
                dnsServers.AddRange(NetworkInterface.
                                        GetAllNetworkInterfaces().
                                        Where     (NI        => NI.OperationalStatus == OperationalStatus.Up).
                                        SelectMany(NI        => NI.GetIPProperties().DnsAddresses).
                                        Where     (IPAddress => IPAddress.AddressFamily == AddressFamily.InterNetworkV6).
                                        Select    (IPAddress => new IPSocket(new IPv6Address(IPAddress), IPPort.DNS)));

            #endregion

            this.DNSServers        = dnsServers;

            #region Reflect ResourceRecordTypes

            foreach (var actualType in typeof(ADNSResourceRecord).
                                           Assembly.GetTypes().
                                           Where(type => type.IsClass &&
                                                !type.IsAbstract &&
                                                 type.IsSubclassOf(typeof(ADNSResourceRecord))))
            {

                var constructor_DomainName      = actualType.GetConstructor([ typeof(DomainName),     typeof(Stream) ]);
                var constructor_DNSServiceName  = actualType.GetConstructor([ typeof(DNSServiceName), typeof(Stream) ]);

                var typeIdField                 = actualType.GetField("TypeId") ?? throw new ArgumentException($"Constant field 'TypeId' of type '{actualType.Name}' was not found!");
                var actualTypeId                = typeIdField.GetValue(actualType);

                if (actualTypeId is DNSResourceRecordTypes id)
                {

                    if (constructor_DomainName is not null)
                        rrLookup_DomainName.    TryAdd(id, constructor_DomainName);

                    if (constructor_DNSServiceName is not null)
                        rrLookup_DNSServiceName.TryAdd(id, constructor_DNSServiceName);

                }

                else
                    throw new ArgumentException($"Constant field 'TypeId' of type '{actualType.Name}' was null!");

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

            return new DNSInfo(
                       Origin,
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
                       TimeSpan.Zero
                   );

        }

        #endregion

        #region (private) ReadResourceRecord(DNSStream)

        private ADNSResourceRecord? ReadResourceRecord(Stream DNSStream)
        {

            var resourceName  = DNSTools.ExtractName(DNSStream);
            var typeId        = (DNSResourceRecordTypes) ((DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);

            if (resourceName == "")
                resourceName = ".";

            if (rrLookup_DNSServiceName. TryGetValue(typeId, out var constructor_DNSServiceName))
                return (ADNSResourceRecord) constructor_DNSServiceName.Invoke([
                                                DNSServiceName.Parse(resourceName),
                                                DNSStream
                                            ]);

            else if (rrLookup_DomainName.TryGetValue(typeId, out var constructor_DomainName))
                return (ADNSResourceRecord) constructor_DomainName.Invoke([
                                                DomainName.    Parse(resourceName),
                                                DNSStream
                                            ]);

            Debug.WriteLine($"Unknown DNS resource record '{typeId}' for '{resourceName}' received!");

            return null;

        }

        #endregion

        #region (private) AddToCache(DomainName, DNSInformation)

        /// <summary>
        /// Add a DNS cache entry.
        /// </summary>
        /// <param name="DomainName">The domain name.</param>
        /// <param name="DNSInformation">The DNS information</param>
        private void AddToCache(DNSServiceName  DomainName,
                                DNSInfo     DNSInformation)
        {

            if (DomainName.IsNullOrEmpty() || DNSInformation == null)
                return;

            DNSCache.Add(
                DomainName,
                DNSInformation
            );

        }

        #endregion


        #region Query   (DomainName, ResourceRecordTypes, CancellationToken = default)

        public Task<DNSInfo> Query(DNSServiceName                       DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   CancellationToken                CancellationToken   = default)
        {

            #region Initial checks

            if (DomainName.IsNullOrEmpty() || !DNSServers.Any())
                return Task.FromResult(
                           new DNSInfo(
                               Origin:                 new IPSocket(
                                                           IPv4Address.Localhost,
                                                           IPPort.DNS
                                                       ),
                               QueryId:                0,
                               IsAuthoritativeAnswer:  false,
                               IsTruncated:            false,
                               RecursionDesired:       true,
                               RecursionAvailable:     false,
                               ResponseCode:           DNSResponseCodes.NameError,
                               Answers:                [],
                               Authorities:            [],
                               AdditionalRecords:      [],
                               IsValid:                true,
                               IsTimeout:              false,
                               Timeout:                QueryTimeout
                           )
                      );

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion

            #region Try to get answers from the DNS cache

            var cachedResults = DNSCache.GetDNSInfo(DomainName);

            if (cachedResults is not null)
            {

                var resourceRecords = cachedResults.Answers.
                                          Where  (resourceRecord => resourceRecordTypes.Contains(resourceRecord.Type)).
                                          ToArray();

                if (resourceRecords.Length != 0)
                    return Task.FromResult(cachedResults);

            }

            #endregion


            var dnsQuery = DNSPacket.Query(
                               DomainName,
                               RecursionDesired,
                               [.. resourceRecordTypes]
                           );

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

                        var ms = new MemoryStream();
                        dnsQuery.Serialize(ms, false, []);

                        socket.SendTo(ms.ToArray(), endPoint);

                        length = socket.ReceiveFrom(data, ref endPoint);

                    }
                    catch (SocketException se)
                    {

                        if (se.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                            return new DNSInfo(
                                       Origin:                 new IPSocket(
                                                                   dnsServer.IPAddress,
                                                                   dnsServer.Port
                                                               ),
                                       QueryId:                dnsQuery.TransactionId,
                                       IsAuthoritativeAnswer:  false,
                                       IsTruncated:            false,
                                       RecursionDesired:       false,
                                       RecursionAvailable:     false,
                                       ResponseCode:           DNSResponseCodes.ServerFailure,
                                       Answers:                [],
                                       Authorities:            [],
                                       AdditionalRecords:      [],
                                       IsValid:                true,
                                       IsTimeout:              false,
                                       Timeout:                QueryTimeout
                                   );

                        // A SocketException might be thrown after the timeout was reached!
                        //throw new Exception("DNS server '" + DNSServer + "' did not respond within " + QueryTimeout.TotalSeconds + " seconds!");
                        return DNSInfo.TimedOut(
                                   new IPSocket(
                                       dnsServer.IPAddress,
                                       dnsServer.Port
                                   ),
                                   dnsQuery.TransactionId,
                                   QueryTimeout
                               );

                    }
                    catch
                    {
                        // A SocketException might be thrown after the timeout was reached!
                        //throw new Exception("DNS server '" + DNSServer + "' did not respond within " + QueryTimeout.TotalSeconds + " seconds!");
                        return DNSInfo.TimedOut(
                                   new IPSocket(
                                       dnsServer.IPAddress,
                                       dnsServer.Port
                                   ),
                                   dnsQuery.TransactionId,
                                   QueryTimeout
                               );
                    }
                    finally
                    {
                        socket?.Shutdown(SocketShutdown.Both);
                    }

                    return ReadResponse(
                               dnsServer,
                               dnsQuery.TransactionId,
                               new MemoryStream(data)
                           );

                },
                TaskCreationOptions.AttachedToParent)

            ).ToList();

            #endregion


            Task<DNSInfo>? firstResponse = null;

            if (allDNSServerRequests.Count != 0)
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

                            AddToCache(
                                DomainName,
                                firstResponse.Result
                            );

                            break;

                        }

                    }
                    catch
                    { }

                }
                while (allDNSServerRequests.Count > 0);

            }

            return Task.FromResult(
                       firstResponse?.Result ??
                           new DNSInfo(
                               Origin:               new IPSocket(
                                                          IPv4Address.Localhost,
                                                          IPPort.DNS
                                                     ),
                               QueryId:              0,
                               IsAuthoritativeAnswer:  false,
                               IsTruncated:          false,
                               RecursionDesired:     true,
                               RecursionAvailable:   false,
                               ResponseCode:         DNSResponseCodes.NameError,
                               Answers:              [],
                               Authorities:          [],
                               AdditionalRecords:    [],
                               IsValid:              true,
                               IsTimeout:            false,
                               Timeout:              QueryTimeout
                           )
                   );

        }

        #endregion

        #region Query<T>(DomainName, CancellationToken = default)

        public async Task<DNSInfo<T>> Query<T>(DomainName         DomainName,
                                               CancellationToken  CancellationToken   = default)

            where T : ADNSResourceRecord

            => await Query<T>(
                         DNSServiceName.Parse(DomainName.FullName),
                         CancellationToken
                     ).ConfigureAwait(false);


        public async Task<DNSInfo<T>> Query<T>(DNSServiceName         DNSService,
                                               CancellationToken  CancellationToken   = default)

            where T : ADNSResourceRecord

        {

            var typeIdField  = typeof(T).GetField("TypeId")
                                   ?? throw new ArgumentException($"Constant field 'TypeId' of type '{typeof(T).Name}' was not found!");

            var dnsInfo      = await Query(
                                         DNSService,
                                         [ (DNSResourceRecordTypes) typeIdField.GetValue(typeof(T)) ],
                                         CancellationToken
                                     ).ConfigureAwait(false);

            //return dnsInfo.Answers.
            //           Where(resourceRecord => resourceRecord is T).
            //           Cast<T>();

            return new DNSInfo<T>(dnsInfo);

        }

        #endregion

        #region Query<T1, T2>(DomainName, Mapper)

        public async Task<IEnumerable<T2>> Query<TRR, T2>(DomainName     DomainName,
                                                          Func<TRR, T2>  Mapper)

            where TRR : ADNSResourceRecord

            => (await Query<TRR>(DomainName)).FilteredAnswers.Select(v => Mapper(v));

        #endregion


        #region Query_IPv4Addresses (DomainName, CancellationToken = default)

        public async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(DomainName         DomainName,
                                CancellationToken  CancellationToken = default)

                => await Query<A>(
                             DNSServiceName.Parse(DomainName.FullName),
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers.Select(ARecord => ARecord.IPv4Address)).
                           ConfigureAwait(false);

        #endregion

        #region Query_IPv6Addresses (DomainName, CancellationToken = default)

        public async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(DomainName         DomainName,
                                CancellationToken  CancellationToken = default)

                => await Query<AAAA>(
                             DNSServiceName.Parse(DomainName.FullName),
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address)).
                           ConfigureAwait(false);

        #endregion

        #region Query_IPAddresses   (DomainName, CancellationToken = default)

        public async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(DomainName         DomainName,
                              CancellationToken  CancellationToken = default)

        {

            var ipv4AddressLookupTask = Query_IPv4Addresses(DomainName, CancellationToken);
            var ipv6AddressLookupTask = Query_IPv6Addresses(DomainName, CancellationToken);

            await Task.WhenAll(
                      ipv4AddressLookupTask,
                      ipv6AddressLookupTask
                  ).ConfigureAwait(false);

            return       ipv4AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress).
                   Union(ipv6AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress));

        }

        #endregion

        #region Query_IPAddresses   (RemoteURL,  CancellationToken = default)

        public async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(URL                RemoteURL,
                              CancellationToken  CancellationToken = default)

        {

            var ipv4AddressLookupTask = Query_IPv4Addresses(DomainName.Parse(RemoteURL.Hostname.Name), CancellationToken);
            var ipv6AddressLookupTask = Query_IPv6Addresses(DomainName.Parse(RemoteURL.Hostname.Name), CancellationToken);

            await Task.WhenAll(
                      ipv4AddressLookupTask,
                      ipv6AddressLookupTask
                  ).ConfigureAwait(false);

            return       ipv4AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress).
                   Union(ipv6AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress));

        }

        #endregion


        #region Query_DNSService    (DNSService, CancellationToken = default)

        public async Task<IEnumerable<SRV>>

            Query_DNSService(DNSServiceName         DNSService,
                             CancellationToken  CancellationToken = default)

                => await Query<SRV>(
                             DNSService,
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers).
                           ConfigureAwait(false);

        #endregion

        #region Query_DNSService    (DNSService, CancellationToken = default)

        public async Task<IEnumerable<SRV>>

            Query_DNSService(DomainName         DomainName,
                             SRV_Spec           DNSServiceSpec,
                             CancellationToken  CancellationToken = default)

                => await Query<SRV>(
                             DNSServiceName.From(
                                 DomainName,
                                 DNSServiceSpec
                             ),
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers).
                           ConfigureAwait(false);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => "Using DNS servers: " +
               DNSServers.SafeSelect(socket => socket.ToString()).AggregateCSV();

        #endregion


    }

}
