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

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS clients.
    /// </summary>
    public static class DNSClientExtensions
    {

        // ...

    }


    /// <summary>
    /// A DNS client.
    /// </summary>
    public class DNSClient : IDNSClient
    {

        #region Data

        private Boolean disposedValue;

        #endregion

        #region Properties

        /// <summary>
        /// The DNS servers used by this DNS client.
        /// </summary>
        public IEnumerable<DNSServerConfig>  DNSServers          { get; }

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan                      QueryTimeout        { get; set; }

        /// <summary>
        /// Whether DNS recursion is desired.
        /// </summary>
        public Boolean?                      RecursionDesired    { get; set; }

        /// <summary>
        /// The DNS cache used by this DNS client.
        /// </summary>
        public DNSCache                      DNSCache            { get; }

        #endregion

        #region Constructor(s)

        #region DNSClient(DNSServer)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        public DNSClient(IIPAddress DNSServer)

            : this([ new DNSServerConfig(DNSServer, IPPort.DNS) ])

        { }

        #endregion

        #region DNSClient(DNSServer, Port)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        /// <param name="Port">The IP port of the DNS server to query.</param>
        public DNSClient(IIPAddress DNSServer, IPPort Port)

            : this([ new DNSServerConfig(DNSServer, Port) ])

        { }

        #endregion

        #region DNSClient(DNSServers)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServers">A list of DNS servers to query.</param>
        public DNSClient(IEnumerable<IIPAddress> DNSServers)

            : this(DNSServers.Select(IPAddress => new DNSServerConfig(IPAddress, IPPort.DNS)))

        { }

        #endregion

        #region DNSClient(DNSServers, Port)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServers">A list of DNS servers to query.</param></param>
        /// <param name="Port">The common IP port of the DNS servers to query.</param>
        public DNSClient(IEnumerable<IIPAddress> DNSServers, IPPort Port)

            : this(DNSServers.Select(IPAddress => new DNSServerConfig(IPAddress, Port)))

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
        public DNSClient(IEnumerable<DNSServerConfig>  ManualDNSServers,
                         Boolean                       SearchForIPv4DNSServers = false,
                         Boolean                       SearchForIPv6DNSServers = false)

        {

            this.DNSCache          = new DNSCache();
            this.RecursionDesired  = true;
            this.QueryTimeout      = TimeSpan.FromSeconds(23.5);

            var dnsServers         = new List<DNSServerConfig>(ManualDNSServers);

            #region Search for IPv4/IPv6 DNS Servers...

            if (SearchForIPv4DNSServers)
                dnsServers.AddRange(NetworkInterface.
                                        GetAllNetworkInterfaces().
                                        Where     (NI        => NI.OperationalStatus == OperationalStatus.Up).
                                        SelectMany(NI        => NI.GetIPProperties().DnsAddresses).
                                        Where     (IPAddress => IPAddress.AddressFamily == AddressFamily.InterNetwork).
                                        Select    (IPAddress =>  new DNSServerConfig(
                                                                     new IPv4Address(IPAddress),
                                                                     IPPort.DNS
                                                                 )));

            if (SearchForIPv6DNSServers)
                dnsServers.AddRange(NetworkInterface.
                                        GetAllNetworkInterfaces().
                                        Where     (NI        => NI.OperationalStatus == OperationalStatus.Up).
                                        SelectMany(NI        => NI.GetIPProperties().DnsAddresses).
                                        Where     (IPAddress => IPAddress.AddressFamily == AddressFamily.InterNetworkV6).
                                        Select    (IPAddress => new DNSServerConfig(
                                                                    new IPv6Address(IPAddress),
                                                                    IPPort.DNS
                                                                )));

            #endregion

            this.DNSServers        = dnsServers;

        }

        #endregion

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


        #region Query (DomainName,     ResourceRecordTypes, RecursionDesired = true, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   Boolean                              RecursionDesired    = true,
                                   CancellationToken                    CancellationToken   = default)

            => Query(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   RecursionDesired,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, RecursionDesired = true, ...)

        public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   Boolean                              RecursionDesired    = true,
                                   CancellationToken                    CancellationToken   = default)
        {

            #region Initial checks

            if (DNSServiceName.IsNullOrEmpty() || !DNSServers.Any())
                return Task.FromResult(
                           new DNSInfo(
                               Origin:                 new DNSServerConfig(
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

            var cachedResults = DNSCache.GetDNSInfo(DNSServiceName);

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
                               DNSServiceName,
                               this.RecursionDesired ?? RecursionDesired,
                               [.. resourceRecordTypes]
                           );

            #region Query all DNS server(s) in parallel...

            var allDNSServerRequests = DNSServers.Select(dnsServer =>

                Task<DNSInfo>.Factory.StartNew((token) => {

                    var data = new Byte[512];
                    Int32  length;
                    Socket? socket = null;

                    try
                    {

                        var serverAddress  = System.Net.IPAddress.Parse(dnsServer.IPAddress.ToString());
                        var endPoint       = (EndPoint) new IPEndPoint(serverAddress, dnsServer.Port.ToInt32());

                        socket             = dnsServer.IPAddress.IsIPv4
                                                 ? new Socket(AddressFamily.InterNetwork,   SocketType.Dgram, ProtocolType.Udp)
                                                 : new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

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
                                       Origin:                 new DNSServerConfig(
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
                                   new DNSServerConfig(
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
                                   new DNSServerConfig(
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

                    return DNSInfo.ReadResponse(
                               dnsServer,
                               dnsQuery.TransactionId,
                               new MemoryStream(data)
                           );

                },
                TaskCreationOptions.AttachedToParent,
                CancellationToken)

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
                                DNSServiceName,
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
                               Origin:                 new DNSServerConfig(
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

        }

        #endregion



        #region Google DNS

        public static DNSClient Google()

            => new ([
                   IPv4Address.Parse("8.8.8.8"),
                   IPv4Address.Parse("8.8.4.4"),
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPv6Address.Parse("2001:4860:4860::8844")
               ]);

        #endregion

        #region Google DNS IPv4

        public static DNSClient GoogleIPV4()

            => new ([
                   IPv4Address.Parse("8.8.8.8"),
                   IPv4Address.Parse("8.8.4.4")
               ]);

        #endregion

        #region Google DNS IPv6

        public static DNSClient GoogleIPv6()

            => new ([
                   IPv6Address.Parse("2001:4860:4860::8888"),
                   IPv6Address.Parse("2001:4860:4860::8844")
               ]);

        #endregion


        #region Cloudflare DNS

        public static DNSClient Cloudflare()

            => new ([
                   IPv4Address.Parse("1.1.1.1"),
                   IPv4Address.Parse("1.0.0.1"),
                   IPv6Address.Parse("2606:4700:4700::1111"),
                   IPv6Address.Parse("2606:4700:4700::1001")
               ]);

        #endregion

        #region Cloudflare DNS IPv4

        public static DNSClient CloudflareIPV4()

            => new ([
                   IPv4Address.Parse("1.1.1.1"),
                   IPv4Address.Parse("1.0.0.1")
               ]);

        #endregion

        #region Cloudflare DNS IPv6

        public static DNSClient CloudflareIPv6()

            => new ([
                   IPv6Address.Parse("2606:4700:4700::1111"),
                   IPv6Address.Parse("2606:4700:4700::1001")
               ]);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => "Using DNS servers: " +
               DNSServers.SafeSelect(socket => socket.ToString()).AggregateCSV();

        #endregion


        protected virtual void Dispose(Boolean Disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(Disposing: true);
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose(Disposing: false);
            GC.SuppressFinalize(this);
            return default;
        }


    }

}
