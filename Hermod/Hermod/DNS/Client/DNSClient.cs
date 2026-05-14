/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
        /// Whether DNS recursion is desired as a default.
        /// </summary>
        public Boolean?                      RecursionDesired    { get; set; }

        /// <summary>
        /// Whether to use the DNS cache.
        /// </summary>
        public Boolean                       UseCache            { get; set; }

        /// <summary>
        /// The DNS cache used by this DNS client.
        /// </summary>
        public DNSCache                      DNSCache            { get; }

        /// <summary>
        /// The default EDNS0 UDP payload size to advertise in DNS queries.
        /// </summary>
        public UInt16                        UDPPayloadSize      { get; } = DNSPacket.DefaultUDPPayloadSize;

        #endregion

        #region Constructor(s)

        #region DNSClient(DNSServer,        UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(IIPAddress  DNSServer,
                         Boolean?    UseQueryCache   = true)

            : this(
                  [
                      new DNSServerConfig(
                          DNSServer,
                          IPPort.DNS
                      )
                  ],
                  null,
                  null,
                  UseQueryCache
              )

        { }

        #endregion

        #region DNSClient(DNSServer, Port,  UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        /// <param name="Port">The IP port of the DNS server to query.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(IIPAddress  DNSServer,
                         IPPort      Port,
                         Boolean?    UseQueryCache   = true)

            : this(
                  [
                      new DNSServerConfig(
                          DNSServer,
                          Port
                      )
                  ],
                  null,
                  null,
                  UseQueryCache
                  )

        { }

        #endregion

        #region DNSClient(DNSServers,       UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServers">A list of DNS servers to query.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(IEnumerable<IIPAddress>  DNSServers,
                         Boolean?                 UseQueryCache   = true)

            : this(
                  DNSServers.Select(IPAddress => new DNSServerConfig(
                                                     IPAddress,
                                                     IPPort.DNS
                                                 )),
                  null,
                  null,
                  UseQueryCache
              )

        { }

        #endregion

        #region DNSClient(DNSServers, Port, UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServers">A list of DNS servers to query.</param>
        /// <param name="Port">The common IP port of the DNS servers to query.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(IEnumerable<IIPAddress>  DNSServers,
                         IPPort                   Port,
                         Boolean?                 UseQueryCache   = true)

            : this(
                  DNSServers.Select(IPAddress => new DNSServerConfig(
                                                     IPAddress,
                                                     Port)),
                  null,
                  null,
                  UseQueryCache
              )

        { }

        #endregion


        #region DNSClient(                  SearchForIPv4DNSServers = true, SearchForIPv6DNSServers = true, UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="SearchForIPv4DNSServers">Whether the DNS client will query a list of DNS servers from the IPv4 network configuration.</param>
        /// <param name="SearchForIPv6DNSServers">Whether the DNS client will query a list of DNS servers from the IPv6 network configuration.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(Boolean?  SearchForIPv4DNSServers   = true,
                         Boolean?  SearchForIPv6DNSServers   = true,
                         Boolean?  UseQueryCache             = true)

            : this([],
                   SearchForIPv4DNSServers,
                   SearchForIPv6DNSServers,
                   UseQueryCache)

        { }

        #endregion

        #region DNSClient(ManualDNSServers, SearchForIPv4DNSServers = true, SearchForIPv6DNSServers = true, UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="ManualDNSServers">A list of manually configured DNS servers to query.</param>
        /// <param name="SearchForIPv4DNSServers">Whether the DNS client will query a list of DNS servers from the IPv4 network configuration.</param>
        /// <param name="SearchForIPv6DNSServers">Whether the DNS client will query a list of DNS servers from the IPv6 network configuration.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(IEnumerable<DNSServerConfig>  ManualDNSServers,
                         Boolean?                      SearchForIPv4DNSServers   = false,
                         Boolean?                      SearchForIPv6DNSServers   = false,
                         Boolean?                      UseQueryCache             = true)

        {

            this.DNSCache          = new DNSCache();
            this.RecursionDesired  = true;
            this.UseCache          = UseQueryCache ?? true;
            this.QueryTimeout      = TimeSpan.FromSeconds(23.5);

            var dnsServers         = new List<DNSServerConfig>(ManualDNSServers);

            #region Search for IPv4/IPv6 DNS Servers...

            if (SearchForIPv4DNSServers ?? true)
                dnsServers.AddRange(NetworkInterface.
                                        GetAllNetworkInterfaces().
                                        Where     (NI        => NI.OperationalStatus == OperationalStatus.Up).
                                        SelectMany(NI        => NI.GetIPProperties().DnsAddresses).
                                        Where     (IPAddress => IPAddress.AddressFamily == AddressFamily.InterNetwork).
                                        Select    (IPAddress =>  new DNSServerConfig(
                                                                     new IPv4Address(IPAddress),
                                                                     IPPort.DNS
                                                                 )));

            if (SearchForIPv6DNSServers ?? true)
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
                                DNSInfo         DNSInformation)
        {

            if (DomainName.IsNullOrEmpty() || DNSInformation is null)
                return;

            DNSCache.Add(
                DomainName,
                DNSInformation
            );

        }

        #endregion

        #region RemoveFromCache(DomainName)

        /// <summary>
        /// Remove a cached DNS entry by its domain name.
        /// Useful when HTTP clients encounter errors indicating
        /// stale DNS entries (e.g. AWS endpoint changes).
        /// </summary>
        /// <param name="DomainName">The domain name to remove from cache.</param>
        public Boolean RemoveFromCache(DomainName DomainName)

            => DNSCache.Remove(DomainName);

        /// <summary>
        /// Remove a cached DNS entry by its DNS service name.
        /// Useful when HTTP clients encounter errors indicating
        /// stale DNS entries (e.g. AWS endpoint changes).
        /// </summary>
        /// <param name="DNSServiceName">The DNS service name to remove from cache.</param>
        public Boolean RemoveFromCache(DNSServiceName DNSServiceName)

            => DNSCache.Remove(DNSServiceName);

        #endregion


        #region Query (DomainName,     ResourceRecordTypes, Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             BypassCache         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Query(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   Timeout,
                   RecursionDesired,
                   BypassCache,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public async Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                         IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                         TimeSpan?                            Timeout             = null,
                                         Boolean?                             RecursionDesired    = true,
                                         Boolean?                             BypassCache         = false,
                                         CancellationToken                    CancellationToken   = default)
        {

            #region Initial checks

            if (DNSServiceName.IsNullOrEmpty() || !DNSServers.Any())
                return new DNSInfo(
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
                       );

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion

            #region Try to get answers from the DNS cache

            if (UseCache && !(BypassCache ?? false) &&
                DNSCache.TryGetDNSInfo(DNSServiceName, out var cachedResults))
            {

                // Return cached negative responses (NXDOMAIN, Refused)
                if (cachedResults.ResponseCode is DNSResponseCodes.NameError or
                                                   DNSResponseCodes.Refused)
                    return cachedResults;

                var now              = Timestamp.Now;

                // Some load balancers have shorter timeouts for CNAME records than for A/AAAA records!
                // Yet CNAME records must be valid in order to use A/AAAA records!
                var cnameRecord      = cachedResults.Answers.
                                           FirstOrDefault(resourceRecord => resourceRecord.Type == DNSResourceRecordTypes.CNAME);

                var resourceRecords  = cachedResults.Answers.
                                           Where         (resourceRecord => resourceRecordTypes.Contains(resourceRecord.Type) &&
                                                                            resourceRecord.EndOfLife > now &&
                                                                            ((cnameRecord is null) || (cnameRecord.EndOfLife > now))).
                                           ToArray();

                if (resourceRecords.Length != 0)
                    return cachedResults;

            }

            #endregion


            var dnsQuery = DNSPacket.Query(
                               DNSServiceName,
                               UDPPayloadSize,
                               this.RecursionDesired ?? RecursionDesired ?? true,
                               [.. resourceRecordTypes]
                           );

            #region Query all DNS server(s) in parallel...

            var effectiveTimeout = Timeout ?? QueryTimeout;

            using var raceCTS = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            var allDNSServerRequests = DNSServers.Select(dnsServer =>

                QueryDNSServerAsync(dnsServer, dnsQuery, effectiveTimeout, raceCTS.Token)

            ).ToList();

            #endregion


            DNSInfo? firstResponse = null;

            if (allDNSServerRequests.Count != 0)
            {

                do
                {

                    try
                    {

                        var firstResponseTask = await Task.WhenAny(allDNSServerRequests).
                                                          ConfigureAwait(false);

                        allDNSServerRequests.Remove(firstResponseTask);

                        firstResponse = await firstResponseTask.
                                                ConfigureAwait(false);

                        if (firstResponse?.ResponseCode == DNSResponseCodes.NoError)
                        {

                            foreach (var domainNameGroup in firstResponse.Answers.GroupBy(group => group.DomainName))
                            {
                                AddToCache(
                                    domainNameGroup.Key,
                                    firstResponse
                                );
                            }

                            break;

                        }

                        if (firstResponse?.ResponseCode is DNSResponseCodes.NameError or
                                                            DNSResponseCodes.Refused)
                        {

                            AddToCache(
                                DNSServiceName,
                                firstResponse
                            );

                            break;

                        }

                    }
                    catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    { }

                }
                while (allDNSServerRequests.Count > 0);

                // Cancel remaining in-flight requests
                await raceCTS.CancelAsync();

            }

            return firstResponse ??
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
                       );

        }

        #endregion



        #region (private) QueryDNSServerAsync(DNSServer, DNSQuery, Timeout, CancellationToken)

        private async Task<DNSInfo> QueryDNSServerAsync(DNSServerConfig    DNSServer,
                                                        DNSPacket          DNSQuery,
                                                        TimeSpan           Timeout,
                                                        CancellationToken  CancellationToken)
        {

            Socket? socket = null;

            try
            {

                var serverAddress  = System.Net.IPAddress.Parse(DNSServer.IPAddress.ToString());
                var endPoint       = new IPEndPoint(serverAddress, DNSServer.Port.ToInt32());

                socket             = DNSServer.IPAddress.IsIPv4
                                         ? new Socket(AddressFamily.InterNetwork,   SocketType.Dgram, ProtocolType.Udp)
                                         : new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

                using var timeoutCTS  = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                timeoutCTS.CancelAfter(Timeout);

                await socket.ConnectAsync(endPoint, timeoutCTS.Token).
                             ConfigureAwait(false);

                using var ms = new MemoryStream();
                DNSQuery.Serialize(ms, false, []);

                await socket.SendToAsync(ms.ToArray(), SocketFlags.None, endPoint, timeoutCTS.Token).
                             ConfigureAwait(false);

                var data      = new Byte[Math.Max(4096, (Int32) UDPPayloadSize)];
                var received  = await socket.ReceiveAsync(data, SocketFlags.None, timeoutCTS.Token).
                                             ConfigureAwait(false);

                var response = DNSInfo.ReadResponse(
                                  DNSServer,
                                  DNSQuery.TransactionId,
                                  new MemoryStream(data, 0, received)
                              );

                // RFC 5966: If the UDP response is truncated, retry via TCP
                if (response.IsTruncated)
                    return await QueryDNSServerViaTCPAsync(DNSServer, DNSQuery, Timeout, CancellationToken).
                                     ConfigureAwait(false);

                return response;

            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressFamilyNotSupported)
            {

                return new DNSInfo(
                           Origin:                 new DNSServerConfig(
                                                       DNSServer.IPAddress,
                                                       DNSServer.Port
                                                   ),
                           QueryId:                DNSQuery.TransactionId,
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
                           Timeout:                Timeout
                       );

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               DNSServer.IPAddress,
                               DNSServer.Port
                           ),
                           DNSQuery.TransactionId,
                           Timeout
                       );

            }
            catch
            {

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               DNSServer.IPAddress,
                               DNSServer.Port
                           ),
                           DNSQuery.TransactionId,
                           Timeout
                       );

            }
            finally
            {
                socket?.Dispose();
            }

        }

        #endregion

        #region (private) QueryDNSServerViaTCPAsync(DNSServer, DNSQuery, Timeout, CancellationToken)

        /// <summary>
        /// TCP fallback for truncated UDP responses (RFC 5966).
        /// </summary>
        private async Task<DNSInfo> QueryDNSServerViaTCPAsync(DNSServerConfig    DNSServer,
                                                               DNSPacket          DNSQuery,
                                                               TimeSpan           Timeout,
                                                               CancellationToken  CancellationToken)
        {

            Socket? socket = null;

            try
            {

                var serverAddress  = System.Net.IPAddress.Parse(DNSServer.IPAddress.ToString());
                var endPoint       = new IPEndPoint(serverAddress, DNSServer.Port.ToInt32());

                socket             = DNSServer.IPAddress.IsIPv4
                                         ? new Socket(AddressFamily.InterNetwork,   SocketType.Stream, ProtocolType.Tcp)
                                         : new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                using var timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                timeoutCTS.CancelAfter(Timeout);

                await socket.ConnectAsync(endPoint, timeoutCTS.Token).
                             ConfigureAwait(false);

                using var networkStream = new NetworkStream(socket, ownsSocket: false);

                // TCP DNS: 2-byte length prefix (RFC 1035 Section 4.2.2)
                using var ms = new MemoryStream();
                ms.WriteByte(0);
                ms.WriteByte(0);
                DNSQuery.Serialize(ms, false, []);
                var data       = ms.ToArray();
                var dataLength = data.Length - 2;
                data[0] = (Byte) (dataLength >> 8);
                data[1] = (Byte) (dataLength & 0xFF);

                await networkStream.WriteAsync(data, timeoutCTS.Token).ConfigureAwait(false);
                await networkStream.FlushAsync(timeoutCTS.Token).      ConfigureAwait(false);

                var responseLength = await networkStream.ReadUInt16BEAsync(timeoutCTS.Token).
                                                         ConfigureAwait(false);

                var buffer    = new Byte[responseLength];
                var totalRead = 0;

                while (totalRead < responseLength)
                {

                    var bytesRead = await networkStream.ReadAsync(
                                              buffer.AsMemory(totalRead, responseLength - totalRead),
                                              timeoutCTS.Token
                                          ).ConfigureAwait(false);

                    if (bytesRead == 0)
                        break;

                    totalRead += bytesRead;

                }

                return DNSInfo.ReadResponse(
                           new DNSServerConfig(
                               DNSServer.IPAddress,
                               DNSServer.Port,
                               DNSTransport.TCP,
                               Timeout
                           ),
                           DNSQuery.TransactionId,
                           new MemoryStream(buffer, 0, totalRead)
                       );

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               DNSServer.IPAddress,
                               DNSServer.Port
                           ),
                           DNSQuery.TransactionId,
                           Timeout
                       );

            }
            catch
            {

                return DNSInfo.TimedOut(
                           new DNSServerConfig(
                               DNSServer.IPAddress,
                               DNSServer.Port
                           ),
                           DNSQuery.TransactionId,
                           Timeout
                       );

            }
            finally
            {
                socket?.Dispose();
            }

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
                if (Disposing)
                    DNSCache.Dispose();

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
            Dispose(Disposing: true);
            GC.SuppressFinalize(this);
            return default;
        }


    }

}
