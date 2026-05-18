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

using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS client.
    /// </summary>
    public class DNSClient : IDNSClient
    {

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly TimeSpan  DefaultQueryTimeout    = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The default maximum number of CNAME redirects to follow
        /// before giving up and returning the last response.
        /// RFC 1034 does not mandate a limit, but common practice
        /// is 8-16 to prevent infinite loops.
        /// </summary>
        public const Byte                DefaultMaxCNAMEFollows = 8;

        /// <summary>
        /// Per-server EDNS COOKIE store (RFC 7873).
        /// After each response the server cookie is extracted and stored,
        /// then sent back in subsequent queries to that server.
        /// </summary>
        private readonly ConcurrentDictionary<String, EDNSCookieOption>  cookieStore = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Pooled transport clients for connection-oriented transports (TCP, TLS, HTTPS).
        /// UDP clients are stateless and created per-query, so they are not pooled here.
        /// </summary>
        private readonly ConcurrentDictionary<DNSServerConfig, IDNSClientWithTransport>  transportClients = new();

        private Boolean disposedValue;

        #endregion

        #region Properties

        /// <summary>
        /// The DNS servers used by this DNS client.
        /// </summary>
        public IReadOnlySet<DNSServerConfig>  DNSServers          { get; }

        /// <summary>
        /// The DNS query timeout.
        /// </summary>
        public TimeSpan                       QueryTimeout        { get; }

        /// <summary>
        /// Whether DNS recursion is desired as a default.
        /// </summary>
        public Boolean?                       RecursionDesired    { get; set; }

        /// <summary>
        /// Whether to use the DNS cache.
        /// </summary>
        public Boolean                        UseCache            { get; set; }

        /// <summary>
        /// The DNS cache used by this DNS client.
        /// </summary>
        public DNSCache                       DNSCache            { get; }

        /// <summary>
        /// The default EDNS0 UDP payload size to advertise in DNS queries.
        /// </summary>
        public UInt16                         UDPPayloadSize      { get; } = DNSPacket.DefaultUDPPayloadSize;

        /// <summary>
        /// Optional EDNS0 options to include in every DNS query
        /// (e.g. Cookie, Client Subnet, Padding, Keepalive, Extended DNS Error).
        /// </summary>
        public List<EDNSOption>               EDNSOptions         { get; } = [];

        /// <summary>
        /// Optional EDNS Client Subnet option (RFC 7871).
        /// When set, the truncated client IP address is automatically included
        /// in every DNS query to enable geo-aware / CDN-optimized responses.
        /// Set to null to disable (default).
        /// </summary>
        public EDNSClientSubnetOption?  ClientSubnet    { get; set; }

        /// <summary>
        /// Whether to automatically follow CNAME redirects.
        /// When enabled, the DNSClient will chase CNAME chains until
        /// it receives a response containing the originally requested
        /// record type(s), or until MaxCNAMEFollows is reached.
        /// Default: true.
        /// </summary>
        public Boolean                        FollowCNAMEs        { get; set; } = true;

        /// <summary>
        /// The maximum number of CNAME redirects to follow before
        /// giving up and returning the last response as-is.
        /// Default: 8.
        /// </summary>
        public Byte                           MaxCNAMEFollows     { get; set; } = DefaultMaxCNAMEFollows;

        /// <summary>
        /// The maximum number of retries when a DNS server responds with SERVFAIL.
        /// Default: 1 (1 initial attempt + 1 retry = 2 total attempts per server).
        /// </summary>
        public Byte                           MaxRetries          { get; set; } = 1;

        #endregion

        #region Constructor(s)

        #region DNSClient(DNSServer,  Port = null, QueryTimeout = null, UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        /// <param name="Port">The optional IP port of the DNS server.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(IIPAddress  DNSServer,
                         IPPort?     Port            = null,
                         TimeSpan?   QueryTimeout    = null,
                         Boolean?    UseQueryCache   = true)

            : this (
                  [
                      new DNSServerConfig(
                          DNSServer,
                          Port
                      )
                  ],
                  QueryTimeout:   QueryTimeout,
                  UseQueryCache:  UseQueryCache
              )

        { }

        #endregion

        #region DNSClient(DNSServers, Port = null, QueryTimeout = null, UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServers">A list of DNS servers to query.</param>
        /// <param name="Port">The optional common IP port of the DNS servers.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(IEnumerable<IIPAddress>  DNSServers,
                         IPPort?                  Port            = null,
                         TimeSpan?                QueryTimeout    = null,
                         Boolean?                 UseQueryCache   = true)

            : this(
                  DNSServers.Select(ipAddress => new DNSServerConfig(
                                                     ipAddress,
                                                     Port
                                                 )),
                  QueryTimeout:   QueryTimeout,
                  UseQueryCache:  UseQueryCache
              )

        { }

        #endregion


        #region DNSClient(                  QueryTimeout = null, SearchForIPv4DNSServers = true, SearchForIPv6DNSServers = true, UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="QueryTimeout">The optional DNS query timeout.</param>
        /// <param name="SearchForIPv4DNSServers">Whether the DNS client will query a list of DNS servers from the IPv4 network configuration.</param>
        /// <param name="SearchForIPv6DNSServers">Whether the DNS client will query a list of DNS servers from the IPv6 network configuration.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(TimeSpan?  QueryTimeout              = null,
                         Boolean?   SearchForIPv4DNSServers   = true,
                         Boolean?   SearchForIPv6DNSServers   = true,
                         Boolean?   UseQueryCache             = true)

            : this([],
                   QueryTimeout,
                   SearchForIPv4DNSServers,
                   SearchForIPv6DNSServers,
                   UseQueryCache)

        { }

        #endregion

        #region DNSClient(ManualDNSServers, QueryTimeout = null, SearchForIPv4DNSServers = true, SearchForIPv6DNSServers = true, UseQueryCache = true)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="ManualDNSServers">A list of manually configured DNS servers to query.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout.</param>
        /// <param name="SearchForIPv4DNSServers">Whether the DNS client will query a list of DNS servers from the IPv4 network configuration.</param>
        /// <param name="SearchForIPv6DNSServers">Whether the DNS client will query a list of DNS servers from the IPv6 network configuration.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(IEnumerable<DNSServerConfig>  ManualDNSServers,
                         TimeSpan?                     QueryTimeout              = null,
                         Boolean?                      SearchForIPv4DNSServers   = false,
                         Boolean?                      SearchForIPv6DNSServers   = false,
                         Boolean?                      UseQueryCache             = true)

        {

            this.QueryTimeout      = QueryTimeout  ?? DefaultQueryTimeout;
            this.UseCache          = UseQueryCache ?? true;
            this.DNSCache          = new DNSCache();
            this.RecursionDesired  = true;

            var dnsServers         = new HashSet<DNSServerConfig>(ManualDNSServers);

            #region Search for IPv4/IPv6 DNS Servers...

            if (SearchForIPv4DNSServers ?? true)
                NetworkInterface.GetAllNetworkInterfaces().
                    Where     (networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up).
                    SelectMany(networkInterface => networkInterface.GetIPProperties().DnsAddresses).
                    Where     (ipAddress        => ipAddress.AddressFamily == AddressFamily.InterNetwork).
                    Select    (ipAddress        => new DNSServerConfig(
                                                       IPv4Address.From(ipAddress),
                                                       IPPort.DNS
                                                   )).
                    ForEach   (dnsServerConfig  => dnsServers.Add(dnsServerConfig));

            if (SearchForIPv6DNSServers ?? true)
                NetworkInterface.GetAllNetworkInterfaces().
                    Where     (networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up).
                    SelectMany(networkInterface => networkInterface.GetIPProperties().DnsAddresses).
                    Where     (ipAddress        => ipAddress.AddressFamily == AddressFamily.InterNetworkV6).
                    Select    (ipAddress        => new DNSServerConfig(
                                                       IPv6Address.From(ipAddress),
                                                       IPPort.DNS
                                                   )).
                    ForEach   (dnsServerConfig  => dnsServers.Add(dnsServerConfig));

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

            var effectiveTimeout = Timeout ?? QueryTimeout;

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
                            Timeout:                effectiveTimeout
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

                // Check per-type NODATA cache: if all requested types are cached as NODATA,
                // return the cached result without hitting the network.
                if (resourceRecordTypes.All(type => DNSCache.IsNoData(DNSServiceName, type)))
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

            // RFC 8198: Aggressive NSEC caching — check if the queried name
            // falls within a known NSEC range, proving non-existence.
            var zoneName = DNSServiceName.ToString();
            // Extract zone from name (use last 2+ labels as approximation)
            var labels = zoneName.Split('.');
            if (labels.Length > 2)
                zoneName = String.Join(".", labels[^3..]);

            if (DNSCache.IsNameNegativelyCachedByNSEC(DNSServiceName.ToString(), zoneName))
                return new DNSInfo(
                           Origin:                 DNSServers.First(),
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
                           Timeout:                effectiveTimeout
                       );


            // Build the EDNS options list.
            // Per-server cookies are injected in QueryDNSServerAsync()
            // because they differ per server endpoint.
            var dnsQuery = DNSPacket.Query(
                               DNSServiceName,
                               UDPPayloadSize,
                               this.RecursionDesired ?? RecursionDesired ?? true,
                               EDNSOptions.Count > 0 ? EDNSOptions : null,
                               [.. resourceRecordTypes]
                           );

            #region Query all DNS server(s) in parallel...

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

                        if (CancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException(CancellationToken);

                        if (firstResponse?.ResponseCode == DNSResponseCodes.NoError)
                        {

                            if (firstResponse.Answers.Any())
                            {

                                foreach (var domainNameGroup in firstResponse.Answers.GroupBy(group => group.DomainName))
                                {
                                    AddToCache(
                                        domainNameGroup.Key,
                                        firstResponse
                                    );
                                }

                            }
                            else
                            {

                                // NODATA: NoError but zero answers — cache per (domain, type)
                                // so that a NODATA for AAAA does not suppress valid A records.
                                // Uses the SOA minimum TTL from the authority section (RFC 2308),
                                // falling back to the configured negative cache TTL.
                                var noDataTTL = firstResponse.Authorities.
                                                    OfType<SOA>().
                                                    Select(soa => soa.TimeToLive).
                                                    FirstOrDefault(DNSCache.DefaultNegativeCacheTTL);

                                foreach (var recordType in resourceRecordTypes)
                                {
                                    DNSCache.AddNoData(
                                        DNSServiceName,
                                        recordType,
                                        noDataTTL
                                    );
                                }

                                // Also cache the response itself so the cache lookup
                                // has something to return for NODATA hits.
                                AddToCache(
                                    DNSServiceName,
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

                        // RFC 8198: Cache NSEC records from the authority section for aggressive negative caching
                        if (firstResponse is not null)
                        {
                            var nsecRecords = firstResponse.Authorities.OfType<NSEC>().ToList();
                            if (nsecRecords.Count > 0)
                            {
                                var respZone = DNSServiceName.ToString();
                                var respLabels = respZone.Split('.');
                                if (respLabels.Length > 2)
                                    respZone = String.Join(".", respLabels[^3..]);

                                var nsecTTL = firstResponse.Authorities.OfType<SOA>()
                                                   .Select(soa => soa.TimeToLive)
                                                   .FirstOrDefault(DNSCache.DefaultNegativeCacheTTL);

                                foreach (var nsec in nsecRecords)
                                    DNSCache.AddNSECRange(respZone, nsec, nsecTTL);
                            }
                        }

                    }
                    catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (OperationCanceledException)
                    {
                        // Race-cancel from the linked raceCTS — expected when
                        // another DNS server already returned a valid response.
                        // Silently ignore; nobody is awaiting this task anymore.
                    }
                    catch (Exception e)
                    {
                        DebugX.LogT($"DNS query for '{DNSServiceName}' failed: {e.Message}");
                    }

                }
                while (allDNSServerRequests.Count > 0);

                // Cancel remaining in-flight requests
                await raceCTS.CancelAsync();

            }

            #region Follow CNAME / DNAME chains

            // Many authoritative DNS servers return only a CNAME record when
            // the queried name is an alias, without including the final A/AAAA
            // records in the answer section.  When FollowCNAMEs is enabled,
            // we iteratively resolve the CNAME target until we either receive
            // the originally requested record types or hit the depth limit.
            //
            // RFC 6672 (DNAME): A DNAME record provides redirection for an
            // entire subtree.  When the answer contains a DNAME but no CNAME,
            // we synthesize the rewritten name and continue the chase.

            if (FollowCNAMEs && firstResponse is not null &&
                firstResponse.ResponseCode == DNSResponseCodes.NoError &&
                firstResponse.Answers.Any())
            {

                // Only chase if:
                //  1) The answer section contains CNAME(s) or DNAME(s)
                //  2) But does NOT contain any record of the originally requested type(s)
                //     (i.e. the resolver didn't already inline the final answer)
                var requestedTypesSet  = new HashSet<DNSResourceRecordTypes>(resourceRecordTypes);

                // "Any" matches everything — no chase needed
                if (!requestedTypesSet.Contains(DNSResourceRecordTypes.Any) &&
                    !requestedTypesSet.Contains(DNSResourceRecordTypes.CNAME))
                {

                    var hasRequestedType  = firstResponse.Answers.Any(rr => requestedTypesSet.Contains(rr.Type));

                    if (!hasRequestedType)
                    {

                        var allAnswers        = new List<IDNSResourceRecord>(firstResponse.Answers);
                        var currentResponse   = firstResponse;
                        var currentName       = DNSServiceName.ToString();
                        var visited           = new HashSet<String>(StringComparer.OrdinalIgnoreCase) {
                                                    currentName
                                                };

                        for (var hop = 0; hop < MaxCNAMEFollows; hop++)
                        {

                            // First check for CNAME
                            var cnameTarget = currentResponse.Answers.
                                                  OfType<CNAME>().
                                                  Select(cname => cname.CName.FullName).
                                                  LastOrDefault();

                            // RFC 6672: If no CNAME, check for DNAME and synthesize the rewritten name
                            if (cnameTarget is null)
                            {
                                var dname = currentResponse.Answers.
                                                OfType<DNAME>().
                                                LastOrDefault();

                                if (dname is not null)
                                {
                                    // Synthesize: replace the DNAME owner suffix in the queried name
                                    // with the DNAME target.  E.g. if querying "x.old.example." and
                                    // DNAME owner is "old.example." with target "new.example.",
                                    // the rewritten name becomes "x.new.example."
                                    var ownerSuffix = dname.DomainName.ToString();
                                    if (currentName.EndsWith(ownerSuffix, StringComparison.OrdinalIgnoreCase))
                                    {
                                        var prefix = currentName[..^ownerSuffix.Length];
                                        cnameTarget = prefix + dname.Target.FullName;
                                    }
                                }
                            }

                            if (cnameTarget is null || !visited.Add(cnameTarget))
                                break;   // No CNAME/DNAME or loop detected

                            currentName = cnameTarget;

                            var followUpResponse = await Query(
                                                             DNSServiceName.Parse(cnameTarget),
                                                             resourceRecordTypes,
                                                             Timeout,
                                                             RecursionDesired,
                                                             BypassCache,
                                                             CancellationToken
                                                         ).ConfigureAwait(false);

                            if (followUpResponse.ResponseCode != DNSResponseCodes.NoError ||
                                !followUpResponse.Answers.Any())
                            {
                                // NXDOMAIN / error on the target — stop chasing
                                currentResponse = followUpResponse;
                                break;
                            }

                            allAnswers.AddRange(followUpResponse.Answers);
                            currentResponse = followUpResponse;

                            // Check whether we now have the requested record type(s)
                            if (followUpResponse.Answers.Any(rr => requestedTypesSet.Contains(rr.Type)))
                                break;

                        }

                        // Build a merged response with the full CNAME/DNAME chain + final records
                        firstResponse = new DNSInfo(
                                            Origin:                 currentResponse.Origin,
                                            QueryId:                currentResponse.QueryId,
                                            IsAuthoritativeAnswer:  currentResponse.AuthoritativeAnswer,
                                            IsTruncated:            currentResponse.IsTruncated,
                                            RecursionDesired:       currentResponse.RecursionRequested,
                                            RecursionAvailable:     currentResponse.RecursionAvailable,
                                            ResponseCode:           currentResponse.ResponseCode,
                                            Answers:                allAnswers,
                                            Authorities:            currentResponse.Authorities,
                                            AdditionalRecords:      currentResponse.AdditionalRecords,
                                            IsValid:                currentResponse.IsValid,
                                            IsTimeout:              currentResponse.IsTimeout,
                                            Timeout:                currentResponse.Timeout
                                        );

                        // Cache the merged response under the original name
                        AddToCache(DNSServiceName, firstResponse);

                    }

                }

            }

            #endregion


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
                            Timeout:                effectiveTimeout
                        );

        }

        #endregion



        #region (private) GetOrCreateTransportClient(DNSServer, Timeout)

        /// <summary>
        /// Get or create the appropriate transport client for the given DNS server configuration.
        /// UDP clients are stateless and created per-query (caller is responsible for disposal).
        /// TCP, TLS, and HTTPS clients are connection-oriented and pooled for reuse.
        /// </summary>
        private IDNSClientWithTransport GetOrCreateTransportClient(DNSServerConfig  DNSServer,
                                                                   TimeSpan         Timeout)
        {

            return DNSServer.Transport switch
            {

                DNSTransport.UDP =>
                    new DNSUDPClient(
                        DNSServer.IPAddress,
                        DNSServer.Port,
                        QueryTimeout: Timeout
                    ),

                DNSTransport.TCP =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSTCPClient(
                            DNSServer.IPAddress,
                            DNSServer.Port,
                            QueryTimeout: Timeout
                        )
                    ),

                DNSTransport.TLS =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSTLSClient(
                            DNSServer.IPAddress,
                            DNSServer.Port,
                            QueryTimeout: Timeout
                        )
                    ),

                DNSTransport.HTTPS or DNSTransport.HTTPS_Binary =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            DNSServer.IPAddress,
                            DNSServer.Port,
                            Mode:         DNSHTTPSMode.POST,
                            QueryTimeout: Timeout
                        )
                    ),

                DNSTransport.HTTPS_JSON =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            DNSServer.IPAddress,
                            DNSServer.Port,
                            Mode:         DNSHTTPSMode.JSON,
                            QueryTimeout: Timeout
                        )
                    ),

                DNSTransport.HTTPS_GET =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            DNSServer.IPAddress,
                            DNSServer.Port,
                            Mode:         DNSHTTPSMode.GET,
                            QueryTimeout: Timeout
                        )
                    ),

                // HTTP variants (unencrypted) — treat like HTTPS for now
                DNSTransport.HTTP or DNSTransport.HTTP_Binary =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            DNSServer.IPAddress,
                            DNSServer.Port,
                            Mode:         DNSHTTPSMode.POST,
                            QueryTimeout: Timeout
                        )
                    ),

                DNSTransport.HTTP_JSON =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            DNSServer.IPAddress,
                            DNSServer.Port,
                            Mode:         DNSHTTPSMode.JSON,
                            QueryTimeout: Timeout
                        )
                    ),

                _ => new DNSUDPClient(
                         DNSServer.IPAddress,
                         DNSServer.Port,
                         QueryTimeout: Timeout
                     )

            };

        }

        #endregion

        #region (private) QueryDNSServerAsync(DNSServer, DNSQuery, Timeout, CancellationToken)

        private async Task<DNSInfo> QueryDNSServerAsync(DNSServerConfig    DNSServer,
                                                        DNSPacket          DNSQuery,
                                                        TimeSpan           Timeout,
                                                        CancellationToken  CancellationToken)
        {

            // RFC 7873: Inject stored per-server COOKIE into the query.
            var serverKey       = DNSServer.IPAddress.ToString();
            var effectiveQuery  = DNSQuery;

            // RFC 7873: Always send a COOKIE option.
            // If we already have a server cookie, include it; otherwise send a fresh client cookie.
            if (!cookieStore.TryGetValue(serverKey, out var storedCookie))
                storedCookie = EDNSCookieOption.CreateInitial();

            var optionsWithCookie = EDNSOptions
                                        .Where (o => o.Code != (UInt16) EDNSOptionCode.Cookie)
                                        .Append(storedCookie)
                                        .ToList();

            // RFC 7871: Include Client Subnet if configured
            if (ClientSubnet is not null)
            {
                optionsWithCookie.RemoveAll(o => o.Code == (UInt16) EDNSOptionCode.ClientSubnet);
                optionsWithCookie.Add(ClientSubnet);
            }

            effectiveQuery = DNSPacket.Query(
                                 DNSQuery.Questions.First().DomainName,
                                 UDPPayloadSize,
                                 DNSQuery.RecursionDesired,
                                 optionsWithCookie,
                                 DNSQuery.Questions.Select(q => q.QueryType).ToArray()
                             );

            // Get or create the appropriate transport client based on the server's Transport setting.
            var transportClient = GetOrCreateTransportClient(DNSServer, Timeout);
            var isUDP           = DNSServer.Transport == DNSTransport.UDP;

            try
            {

                // Transfer EDNS options (including cookie) to the transport client.
                var optionsToSet = optionsWithCookie;

                if (isUDP)
                {
                    transportClient.EDNSOptions.AddRange(optionsToSet);
                }
                else
                {
                    foreach (var option in optionsToSet)
                    {
                        var idx = transportClient.EDNSOptions.FindIndex(o => o.Code == option.Code);
                        if (idx >= 0)
                            transportClient.EDNSOptions[idx] = option;
                        else
                            transportClient.EDNSOptions.Add(option);
                    }
                }

                DNSInfo response;

                // Retry logic for SERVFAIL responses
                var attempts = 0;
                do
                {

                    response = await transportClient.Query(
                                         DNSQuery.Questions.First().DomainName,
                                         DNSQuery.Questions.Select(q => q.QueryType),
                                         Timeout,
                                         DNSQuery.RecursionDesired,
                                         false,
                                         CancellationToken
                                     ).ConfigureAwait(false);

                    // RFC 7873: Extract and store server cookie from response OPT record
                    ExtractAndStoreCookie(serverKey, response);

                    if (response.ResponseCode != DNSResponseCodes.ServerFailure)
                        break;

                    attempts++;

                    if (attempts <= MaxRetries)
                        await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken).ConfigureAwait(false);

                }
                while (attempts <= MaxRetries);

                return response;

            }
            finally
            {
                // Only dispose UDP clients (they are created per-query and stateless).
                // Pooled connection-oriented clients (TCP/TLS/HTTPS) are reused.
                if (isUDP && transportClient is IDisposable disposableClient)
                    disposableClient.Dispose();
            }

        }

        #endregion

        #region (private) ExtractAndStoreCookie(ServerKey, Response)

        /// <summary>
        /// RFC 7873: Extract the server cookie from the response OPT record
        /// and store it so that subsequent queries to the same server include it.
        /// </summary>
        private void ExtractAndStoreCookie(String   ServerKey,
                                           DNSInfo  Response)
        {

            var responseCookie = Response.EDNSOptions
                                         .OfType<EDNSCookieOption>()
                                         .FirstOrDefault();

            if (responseCookie?.HasServerCookie == true)
                cookieStore[ServerKey] = responseCookie;

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
                {

                    DNSCache.Dispose();

                    // Dispose all pooled transport clients (TCP, TLS, HTTPS)
                    foreach (var kvp in transportClients)
                    {
                        if (kvp.Value is IDisposable disposableClient)
                            disposableClient.Dispose();
                    }

                    transportClients.Clear();

                }

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
