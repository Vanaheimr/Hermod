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

using System.Diagnostics;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS client.
    /// </summary>
    public partial class DNSClient : IDNSClient
    {

        #region Data

        /// <summary>
        /// The default DNS query timeout.
        /// </summary>
        public static readonly  TimeSpan  DefaultQueryTimeout    = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The default maximum number of CNAME redirects to follow
        /// before giving up and returning the last response.
        /// RFC 1034 does not mandate a limit, but common practice
        /// is 8-16 to prevent infinite loops.
        /// </summary>
        public const            Byte                DefaultMaxCNAMEFollows = 8;

        /// <summary>
        /// Per-server EDNS COOKIE store (RFC 7873).
        /// After each response the server cookie is extracted and stored,
        /// then sent back in subsequent queries to that server.
        /// </summary>
        private readonly        ConcurrentDictionary<String, Byte[]>            cookieStore = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Where the client half of every DNS Cookie comes from (RFC 7873 §4.1).
        /// </summary>
        /// <remarks>
        /// The store above holds only the *server* half now. The client half is
        /// derived per server rather than remembered, which is what §4.1 asks for
        /// and what keeps it from becoming a value that follows this client
        /// across changes of address.
        /// </remarks>
        private readonly        DNSClientCookies                                clientCookies = new();

        /// <summary>
        /// Pooled transport clients for connection-oriented transports (TCP, TLS, HTTPS).
        /// UDP clients are stateless and created per-query, so they are not pooled here.
        /// </summary>
        private readonly        ConcurrentDictionary<DNSServerConfig, IDNSClientWithTransport>  transportClients = new();

        private                 Boolean disposedValue;


        private readonly        ILogger<IDNSClient>  logger;
        private readonly        ILoggerFactory       loggerFactory;

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
        /// Whether to set the EDNS0 "DNSSEC OK" (DO) bit on every query, so the
        /// resolver returns the RRSIG/DNSKEY/DS records needed for DNSSEC validation
        /// (RFC 4035 §3.2.1, RFC 6891 §6.1.3). Required for DANE (RFC 7672) and any
        /// use of <see cref="DNSSECValidator"/>. Default false.
        /// </summary>
        public Boolean                        DnssecOK            { get; set; }

        /// <summary>
        /// Optional EDNS Client Subnet option (RFC 7871).
        /// When set, the truncated client IP address is automatically included
        /// in every DNS query to enable geo-aware / CDN-optimized responses.
        /// Set to null to disable (default).
        /// </summary>
        public EDNSClientSubnetOption?        ClientSubnet        { get; set; }

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

        #region DNSClient(DNSServer,  Port = null, QueryTimeout = null, UseQueryCache = true, ...)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="DNSServer">The DNS server to query.</param>
        /// <param name="Port">The optional IP port of the DNS server.</param>
        /// <param name="QueryTimeout">The optional DNS query timeout.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(IIPAddress            DNSServer,
                         IPPort?               Port            = null,
                         TimeSpan?             QueryTimeout    = null,
                         Boolean?              UseQueryCache   = true,
                         ILogger<IDNSClient>?  Logger          = null,
                         ILoggerFactory?       LoggerFactory   = null)

            : this (
                  [
                      new DNSServerConfig(
                          DNSServer,
                          Port
                      )
                  ],
                  QueryTimeout:   QueryTimeout,
                  UseQueryCache:  UseQueryCache,
                  Logger:         Logger,
                  LoggerFactory:  LoggerFactory
              )

        { }

        #endregion

        #region DNSClient(DNSServers, Port = null, QueryTimeout = null, UseQueryCache = true, ...)

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
                         Boolean?                 UseQueryCache   = true,
                         ILogger<IDNSClient>?     Logger          = null,
                         ILoggerFactory?          LoggerFactory   = null)

            : this(
                  DNSServers.Select(ipAddress => new DNSServerConfig(
                                                     ipAddress,
                                                     Port
                                                 )),
                  QueryTimeout:   QueryTimeout,
                  UseQueryCache:  UseQueryCache,
                  Logger:         Logger,
                  LoggerFactory:  LoggerFactory
              )

        { }

        #endregion


        #region DNSClient(                  QueryTimeout = null, SearchForIPv4DNSServers = true, SearchForIPv6DNSServers = true, UseQueryCache = true, ...)

        /// <summary>
        /// Create a new DNS resolver client.
        /// </summary>
        /// <param name="QueryTimeout">The optional DNS query timeout.</param>
        /// <param name="SearchForIPv4DNSServers">Whether the DNS client will query a list of DNS servers from the IPv4 network configuration.</param>
        /// <param name="SearchForIPv6DNSServers">Whether the DNS client will query a list of DNS servers from the IPv6 network configuration.</param>
        /// <param name="UseQueryCache">Whether to use the DNS query cache.</param>
        public DNSClient(TimeSpan?             QueryTimeout              = null,
                         Boolean?              SearchForIPv4DNSServers   = true,
                         Boolean?              SearchForIPv6DNSServers   = true,
                         Boolean?              UseQueryCache             = true,
                         ILogger<IDNSClient>?  Logger                    = null,
                         ILoggerFactory?       LoggerFactory             = null)

            : this([],
                   QueryTimeout,
                   SearchForIPv4DNSServers,
                   SearchForIPv6DNSServers,
                   UseQueryCache,
                   Logger,
                   LoggerFactory)

        { }

        #endregion

        #region DNSClient(ManualDNSServers, QueryTimeout = null, SearchForIPv4DNSServers = true, SearchForIPv6DNSServers = true, UseQueryCache = true, ...)

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
                         Boolean?                      UseQueryCache             = true,
                         ILogger<IDNSClient>?          Logger                    = null,
                         ILoggerFactory?               LoggerFactory             = null)

        {

            this.loggerFactory     = LoggerFactory ?? NullLoggerFactory.Instance;
            this.logger            = Logger        ?? loggerFactory.CreateLogger<IDNSClient>();
            this.QueryTimeout      = QueryTimeout  ?? DefaultQueryTimeout;
            this.UseCache          = UseQueryCache ?? true;
            this.DNSCache          = new DNSCache(LoggerFactory: this.loggerFactory);
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



        [LoggerMessage(Level   = LogLevel.Debug,
                       Message = "Querying DNS for '{DNSServiceName}' with record types '{RecordTypes}' and a timeout of {Timeout}ms")]
        public partial void LogDNSResolution(String DNSServiceName, String RecordTypes, Double Timeout);

        [LoggerMessage(Level   = LogLevel.Debug,
                       Message = "Querying DNS for '{DNSServiceName}' with record types '{RecordTypes}' => '{Answers}', runtime: {Runtime}ms")]
        public partial void LogDNSResponse(String DNSServiceName, String RecordTypes, String Answers, Double Runtime);


        #region Query (DomainName,     ResourceRecordTypes, Timeout = null, RecursionDesired = true, ForceUpdate = false, ...)

        /// <summary>
        /// Query the configured DNS server(s) for the specified domain name and resource record types.
        /// </summary>
        /// <param name="DomainName">The domain name to query.</param>
        /// <param name="ResourceRecordTypes">An enumeration of DNS resource record types to query for (e.g. A, AAAA, CNAME). Use 'Any' to query for all types.</param>
        /// <param name="Timeout">An optional timeout for this query. If not specified, the client's default QueryTimeout will be used.</param>
        /// <param name="RecursionDesired">Whether to set the Recursion Desired flag in the DNS query. If not specified, the client's default RecursionDesired setting will be used (default: true).</param>
        /// <param name="ForceUpdate">Whether to force an upstream DNS query and update the DNS cache with the response. Default: false.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel the query.</param>
        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Query(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   Timeout,
                   RecursionDesired,
                   ForceUpdate,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, Timeout = null, RecursionDesired = true, ForceUpdate = false, ...)

        /// <summary>
        /// Query the configured DNS server(s) for the specified DNS service name and resource record types.
        /// </summary>
        /// <param name="DNSServiceName">The DNS service name to query.</param>
        /// <param name="ResourceRecordTypes">An enumeration of DNS resource record types to query for (e.g. SRV, SVCB, HTTPS). Use 'Any' to query for all types.</param>
        /// <param name="Timeout">An optional timeout for this query. If not specified, the client's default QueryTimeout will be used.</param>
        /// <param name="RecursionDesired">Whether to set the Recursion Desired flag in the DNS query. If not specified, the client's default RecursionDesired setting will be used (default: true).</param>
        /// <param name="ForceUpdate">Whether to force an upstream DNS query and update the DNS cache with the response. Default: false.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel the query.</param>
        public async Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                         IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                         TimeSpan?                            Timeout             = null,
                                         Boolean?                             RecursionDesired    = true,
                                         Boolean?                             ForceUpdate         = false,
                                         CancellationToken                    CancellationToken   = default)
        {

            var effectiveTimeout = Timeout ?? QueryTimeout;

            #region Initial checks

            var stopWatch = Stopwatch.StartNew();

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
                           Timeout:                effectiveTimeout,

                           Runtime:                TimeSpan.Zero

                       );

            var resourceRecordTypes = ResourceRecordTypes.ToList();

            if (resourceRecordTypes.Count == 0)
                resourceRecordTypes = [ DNSResourceRecordTypes.Any ];

            #endregion

            logger.LogDebug(
                "Querying DNS for '{DNSServiceName}' with record types '{RecordTypes}' and a timeout of {Timeout}ms",
                DNSServiceName,
                resourceRecordTypes.AggregateWith(", "),
                effectiveTimeout.TotalMilliseconds
            );

            //LogDNSResolution(
            //    DNSServiceName.     ToString(),
            //    resourceRecordTypes.AggregateWith(", "),
            //    Math.Round(effectiveTimeout.TotalMilliseconds, 2)
            //);

            #region Try to get answers from the DNS cache

            if (UseCache && !(ForceUpdate ?? false) &&
                DNSCache.TryGetDNSInfo(DNSServiceName, out var cachedResults))
            {

                // Return cached negative responses (NXDOMAIN, Refused)
                if (cachedResults.ResponseCode is DNSResponseCodes.NameError or
                                                    DNSResponseCodes.Refused)
                {
                    logger.LogDebug(
                        "DNS cache hit for '{DNSServiceName}' with negative response {ResponseCode}",
                        DNSServiceName,
                        cachedResults.ResponseCode
                    );

                    return cachedResults;
                }

                // Check per-type NODATA cache: if all requested types are cached as NODATA,
                // return the cached result without hitting the network.
                if (resourceRecordTypes.All(type => DNSCache.IsNoData(DNSServiceName, type)))
                {
                    logger.LogDebug(
                        "DNS cache hit for '{DNSServiceName}' with NODATA for record types '{RecordTypes}'",
                        DNSServiceName,
                        resourceRecordTypes.AggregateWith(", ")
                    );

                    return cachedResults;
                }

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
                {
                    logger.LogDebug(
                        "DNS cache hit for '{DNSServiceName}' with {AnswerCount} matching answer(s)",
                        DNSServiceName,
                        resourceRecords.Length
                    );

                    return cachedResults;
                }

                logger.LogDebug(
                    "DNS cache entry for '{DNSServiceName}' did not contain fresh answers for record types '{RecordTypes}'",
                    DNSServiceName,
                    resourceRecordTypes.AggregateWith(", ")
                );

            }

            #endregion

            // RFC 8198: Aggressive NSEC caching — check if the queried name
            // falls within a known NSEC range, proving non-existence.
            //
            // The zone is not guessed from the shape of the name. It used to be
            // taken as "the last three labels", which is right for a.example.com
            // and wrong for everything under a longer or shorter zone cut; the
            // cache then answered from the wrong zone's ranges, or missed. The
            // lookup now tries every ancestor of the name as a zone, which is
            // what "which zone holds this name" actually means when you do not
            // yet know the answer.
            if (DNSCache.IsNameNegativelyCachedByNSEC(DNSServiceName.ToString()))
            {
                logger.LogDebug(
                    "DNS NSEC cache proves non-existence for '{DNSServiceName}'",
                    DNSServiceName
                );

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
                           Timeout:                effectiveTimeout,

                            Runtime:                stopWatch.Elapsed

                        );
            }


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

            logger.LogTrace(
                "Dispatching DNS query for '{DNSServiceName}' to {ServerCount} server(s)",
                DNSServiceName,
                DNSServers.Count
            );

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

                        // RFC 8198: cache the NSEC records from the authority
                        // section, so one proven gap can answer for every name
                        // inside it.
                        //
                        // §3 permits this only for records that have been
                        // DNSSEC-validated, and that condition is the whole
                        // safety argument. An NSEC taken on trust is a licence
                        // to deny a range of names: an off-path attacker who
                        // lands a single forged response suppresses everything
                        // in that range for the TTL, without having to win a
                        // race against any particular query. Caching first and
                        // validating never is strictly worse than not caching.
                        //
                        // Hermod's client does not validate inline — that is the
                        // caller's job, via DNSSECValidator — so DNSSECStatus is
                        // normally null here and this path stays dormant. It is
                        // meant to: dormant is the correct state for a feature
                        // whose precondition is not met, and the alternative was
                        // an unauthenticated denial cache.
                        if (firstResponse?.DNSSECStatus == DNSSECValidationResult.Secure)
                        {

                            var nsecRecords = firstResponse.Authorities.OfType<NSEC>().ToList();

                            if (nsecRecords.Count > 0)
                            {

                                // The zone comes from the SOA the responder put
                                // in the authority section — it is the zone that
                                // answered — rather than from counting labels of
                                // the query name, which cannot find a zone cut.
                                var soa      = firstResponse.Authorities.OfType<SOA>().FirstOrDefault();
                                var respZone = soa?.DomainName.ToString();

                                if (respZone is not null)
                                {

                                    var nsecTTL = soa?.TimeToLive ?? DNSCache.DefaultNegativeCacheTTL;

                                    foreach (var nsec in nsecRecords)
                                        DNSCache.AddNSECRange(respZone, nsec, nsecTTL);

                                }

                            }

                        }

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
                                // RFC 2308 §4 sets the lifetime from min(SOA MINIMUM, SOA TTL);
                                // reading only the record's TTL ignores the MINIMUM field that
                                // RFC 2308 repurposed for precisely this.
                                var noDataTTL = DNSCache.ComputeNegativeCacheTTL(firstResponse);

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
                        logger.LogWarning(
                            e,
                            "DNS query for '{DNSServiceName}' failed while awaiting a DNS server response",
                            DNSServiceName
                        );
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

                            // RFC 6672: If no CNAME, check for DNAME and synthesize the rewritten name.
                            //
                            // The substitution is the same one the authoritative
                            // side performs, and it is shared rather than repeated:
                            // it is defined on *labels* (§2.2, "the suffix labels
                            // of the name being sought"), and a second
                            // implementation of it here is a second chance to
                            // compare the two names as strings instead.
                            //
                            // Which is what this did. A string suffix match finds
                            // boundaries inside a label, so a DNAME at
                            // "old.example." matched "notold.example." — a
                            // different name, quite possibly somebody else's — and
                            // rewrote it to "notnew.example."; and it matched the
                            // owner name itself with an empty prefix, redirecting
                            // the one name §2.3 exempts. Neither had any length
                            // check behind it, so an over-long result left the
                            // resolver throwing out of the chase rather than
                            // answering.
                            if (cnameTarget is null)
                            {

                                var dname = currentResponse.Answers.
                                                OfType<DNAME>().
                                                LastOrDefault();

                                if (dname is not null &&
                                    DNSServiceName.TryParse(currentName, out var currentServiceName, out _) &&
                                    DNAME.TrySubstitute(
                                        currentServiceName,
                                        dname.DomainName,
                                        dname.Target,
                                        out var rewritten
                                    ) == DNAMESubstitution.Redirected)
                                {
                                    cnameTarget = rewritten.FullName;
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
                                                             ForceUpdate,
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
                                            Timeout:                currentResponse.Timeout,

                                            Runtime:                stopWatch.Elapsed

                                        );

                        // Cache the merged response under the original name
                        AddToCache(
                            DNSServiceName,
                            firstResponse
                        );

                    }

                }

            }

            #endregion


            var response = firstResponse ?? new DNSInfo(

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
                                                Timeout:                effectiveTimeout,

                                                Runtime:                stopWatch.Elapsed

                                            );

            //LogDNSResponse(
            //    DNSServiceName.     ToString(),
            //    resourceRecordTypes.AggregateWith(", "),
            //    response.Answers.   AggregateWith(", "),
            //    Math.Round(effectiveTimeout.TotalMilliseconds, 2)
            //);

            logger.LogDebug(
                "Querying DNS for '{DNSServiceName}' with record types '{RecordTypes}' => '{Answers}', runtime: {Runtime}ms",
                DNSServiceName,
                resourceRecordTypes.AggregateWith(", "),
                response.Answers.   AggregateWith(", "),
                stopWatch.Elapsed.TotalMilliseconds
            );

            return response;

        }

        #endregion



        #region (private static) AddressOf(DNSServer)

        /// <summary>
        /// The address to dial for the given DNS server, or a refusal naming the
        /// configuration that has none.
        /// </summary>
        /// <remarks>
        /// DNSServerConfig serves two purposes: a server to query, and the origin
        /// of an answer. Only the first needs an address. A DNS-over-HTTPS or
        /// DNS-over-TLS client created from a URL produces the second without
        /// one, so this says which configuration was unusable rather than leaving
        /// a null reference to surface inside whichever client was about to be
        /// built - or, before the field admitted it could be null, inside
        /// DNSServerConfig.ToString().
        /// </remarks>
        private static IIPAddress AddressOf(DNSServerConfig DNSServer)

            => DNSServer.IPAddress
                   ?? throw new ArgumentException(
                          $"The DNS server configuration '{DNSServer}' has no IP address to connect to!",
                          nameof(DNSServer)
                      );

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

            var ipAddress = AddressOf(DNSServer);

            return DNSServer.Transport switch {

                DNSTransport.UDP =>
                    new DNSUDPClient(
                        ipAddress,
                        DNSServer.Port,
                        QueryTimeout:   Timeout,
                        LoggerFactory:  loggerFactory
                    ),

                DNSTransport.TCP =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSTCPClient(
                            ipAddress,
                            DNSServer.Port,
                            QueryTimeout:   Timeout,
                            LoggerFactory:  loggerFactory
                        )
                    ),

                DNSTransport.TLS =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSTLSClient(
                            ipAddress,
                            DNSServer.Port,
                            QueryTimeout:   Timeout,
                            LoggerFactory:  loggerFactory
                        )
                    ),

                DNSTransport.HTTPS or DNSTransport.HTTPS_Binary =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            ipAddress,
                            DNSServer.Port,
                            Mode:           DNSHTTPSMode.POST,
                            QueryTimeout:   Timeout,
                            LoggerFactory:  loggerFactory
                        )
                    ),

                DNSTransport.HTTPS_JSON =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            ipAddress,
                            DNSServer.Port,
                            Mode:           DNSHTTPSMode.JSON,
                            QueryTimeout:   Timeout,
                            LoggerFactory:  loggerFactory
                        )
                    ),

                DNSTransport.HTTPS_GET =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            ipAddress,
                            DNSServer.Port,
                            Mode:           DNSHTTPSMode.GET,
                            QueryTimeout:   Timeout,
                            LoggerFactory:  loggerFactory
                        )
                    ),

                // HTTP variants (unencrypted) — treat like HTTPS for now
                DNSTransport.HTTP or DNSTransport.HTTP_Binary =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            ipAddress,
                            DNSServer.Port,
                            Mode:           DNSHTTPSMode.POST,
                            QueryTimeout:   Timeout,
                            LoggerFactory:  loggerFactory
                        )
                    ),

                DNSTransport.HTTP_JSON =>
                    transportClients.GetOrAdd(DNSServer, _ =>
                        new DNSHTTPSClient(
                            ipAddress,
                            DNSServer.Port,
                            Mode:           DNSHTTPSMode.JSON,
                            QueryTimeout:   Timeout,
                            LoggerFactory:  loggerFactory
                        )
                    ),

                _ => new DNSUDPClient(
                          ipAddress,
                          DNSServer.Port,
                          QueryTimeout:     Timeout,
                          LoggerFactory:    loggerFactory
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
            var serverIPAddress = AddressOf(DNSServer);
            var serverKey       = serverIPAddress.ToString();
            var effectiveQuery  = DNSQuery;

            // RFC 7873 §5.1: always offer a cookie. The client half is derived
            // from this server's address (§4.1), so it is the same on every query
            // to it — which is what makes a server cookie worth keeping, since a
            // server cookie is bound to the client cookie it was issued for. The
            // server half is included once there is one.
            cookieStore.TryGetValue(serverKey, out var storedServerCookie);

            var storedCookie = clientCookies.OptionFor(
                                   System.Net.IPAddress.Parse(AddressOf(DNSServer).ToString()),
                                   storedServerCookie
                               );

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

            // Propagate the DNSSEC OK (DO) bit so the transport requests RRSIG/DNSKEY/DS records.
            transportClient.DnssecOK = this.DnssecOK;

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
                var attempts      = 0;

                // RFC 7873 §5.3 allows exactly one more try after a BADCOOKIE,
                // and one is enough: the response that carried it also carried a
                // valid server cookie, so a second BADCOOKIE means something is
                // wrong that asking again will not fix.
                var cookieRetried = false;

                do
                {

                    logger.LogTrace(
                        "Querying DNS server {DNSServer} via {Transport} for '{DNSServiceName}' ({RecordTypes}), attempt {Attempt}",
                        DNSServer,
                        DNSServer.Transport,
                        DNSQuery.Questions.First().DomainName,
                        DNSQuery.Questions.Select(q => q.QueryType).AggregateWith(", "),
                        attempts + 1
                    );

                    response = await transportClient.Query(
                                         DNSQuery.Questions.First().DomainName,
                                         DNSQuery.Questions.Select(q => q.QueryType),
                                         Timeout,
                                         DNSQuery.RecursionDesired,
                                         false,
                                         CancellationToken
                                     ).ConfigureAwait(false);

                    logger.LogTrace(
                        "DNS server {DNSServer} via {Transport} returned {ResponseCode} with {AnswerCount} answer(s) in {Runtime}ms",
                        DNSServer,
                        DNSServer.Transport,
                        response.ResponseCode,
                        response.Answers.Count(),
                        response.Runtime.TotalMilliseconds
                    );

                    // RFC 7873 §5.3: the COOKIE decides whether this response may
                    // be used at all, so it is checked before the RCODE is read.
                    if (!AcceptCookie(serverKey, storedCookie, response))
                    {

                        response = DNSInfo.Invalid(DNSServer, response.QueryId);

                        attempts++;

                        if (attempts <= MaxRetries)
                            continue;

                        break;

                    }

                    // §5.3: BADCOOKIE is not a refusal to answer, it is a request
                    // to ask again — the response that carried it also carried a
                    // valid server cookie, which AcceptCookie has just stored.
                    if (response.ResponseCode == DNSResponseCodes.BadCookie &&
                        !cookieRetried &&
                        cookieStore.TryGetValue(serverKey, out var refreshedCookie))
                    {

                        cookieRetried  = true;

                        // The client half is derived again rather than carried
                        // over, which changes nothing — it is the same eight
                        // octets — and keeps one rule about where it comes from.
                        storedCookie   = clientCookies.OptionFor(
                                             System.Net.IPAddress.Parse(AddressOf(DNSServer).ToString()),
                                             refreshedCookie
                                         );

                        var cookieIndex = transportClient.EDNSOptions.FindIndex(option => option.Code == (UInt16) EDNSOptionCode.Cookie);

                        if (cookieIndex >= 0)
                            transportClient.EDNSOptions[cookieIndex] = storedCookie;
                        else
                            transportClient.EDNSOptions.Add(storedCookie);

                        logger.LogTrace(
                            "DNS server {DNSServer} returned BADCOOKIE; retrying with the server cookie it supplied",
                            DNSServer
                        );

                        continue;

                    }

                    if (response.ResponseCode != DNSResponseCodes.ServerFailure)
                        break;

                    attempts++;

                    if (attempts <= MaxRetries)
                    {
                        logger.LogWarning(
                            "DNS server {DNSServer} via {Transport} returned SERVFAIL; retry {Attempt} of {MaxRetries}",
                            DNSServer,
                            DNSServer.Transport,
                            attempts,
                            MaxRetries
                        );

                        await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken).ConfigureAwait(false);
                    }

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

        #region (private) AcceptCookie(ServerKey, Sent, Response)

        /// <summary>
        /// Check the COOKIE option of a response and, if it is the answer to the
        /// cookie that was sent, remember its server half (RFC 7873 §5.3).
        /// </summary>
        /// <param name="ServerKey">The server the query went to.</param>
        /// <param name="Sent">The COOKIE option that was sent with the query.</param>
        /// <param name="Response">The response.</param>
        /// <returns>False when §5.3 requires the response to be discarded.</returns>
        /// <remarks>
        /// <para>
        /// §5.3: a client "MUST discard the response if it contains an illegal
        /// COOKIE option length or an incorrect Client Cookie value". The client
        /// cookie is the whole mechanism — an unpredictable value that comes back
        /// only from someone who saw the query — so a response echoing a
        /// different one is, by construction, from someone who did not.
        /// </para>
        /// <para>
        /// The other half of the fix is what gets stored. This used to keep the
        /// entire option from the response, client cookie included, and use it
        /// for the next query. One spoofed response was therefore enough to
        /// *replace* the client's own cookie with the attacker's for as long as
        /// the entry lived: every later query carried a value the attacker had
        /// chosen, so every later spoof was trivially able to echo it. The
        /// mechanism defeated itself, permanently, from a single packet. Only the
        /// server half is a value the server gets to choose.
        /// </para>
        /// </remarks>
        private Boolean AcceptCookie(String            ServerKey,
                                     EDNSCookieOption  Sent,
                                     DNSInfo           Response)
        {

            var responseCookie = Response.EDNSOptions.
                                          OfType<EDNSCookieOption>().
                                          FirstOrDefault();

            // No cookie coming back is not an error: §5.2.1 lets a server that
            // does not implement cookies answer normally, and there is nothing
            // to check or store.
            if (responseCookie is null)
                return true;

            if (!responseCookie.ClientCookie.SequenceEqual(Sent.ClientCookie))
            {

                logger.LogWarning(
                    "DNS server {ServerKey} returned a COOKIE echoing a client cookie that was never sent; discarding the response (RFC 7873 §5.3)",
                    ServerKey
                );

                return false;

            }

            // Only the server half is kept. The client half is derived from the
            // server's address and this client's secret every time it is needed,
            // so there is nothing here to store and nothing to go stale.
            if (responseCookie.HasServerCookie)
                cookieStore[ServerKey] = responseCookie.ServerCookie!;

            return true;

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
