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

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    #region Options

    /// <summary>
    /// Options of the Multicast DNS client.
    /// </summary>
    public sealed class MulticastDNSClientOptions
    {

        /// <summary>
        /// The maximum time a one-shot query waits for answers (default: 2 s).
        /// </summary>
        public TimeSpan  QueryTimeout               { get; init; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// The time a one-shot query keeps collecting answers after the first one arrived (default: 100 ms).
        /// </summary>
        public TimeSpan  ResponseGracePeriod        { get; init; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// The interval after which an unanswered one-shot query is sent again for the first
        /// time; every further interval is twice the one before (RFC 6762 §5.2: "the interval
        /// between the first two queries MUST be at least one second, the intervals between
        /// successive queries MUST increase by at least a factor of two", default: 1 s).
        /// </summary>
        public TimeSpan  RetransmissionInterval     { get; init; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The maximum size of a query packet (RFC 6762 §7.2 and §17, default: 1472 bytes,
        /// an Ethernet MTU less the IPv4 and UDP headers). Known answers that do not fit
        /// continue in further packets, each but the last with the TC bit set.
        /// </summary>
        public Int32     MaxQuerySize               { get; init; } = MulticastDNS.PreferredMaxPacketSize;

        /// <summary>
        /// Whether cached records answer queries (default: true).
        /// </summary>
        public Boolean   UseCache                   { get; init; } = true;

        /// <summary>
        /// Whether one-shot queries set the unicast-response (QU) bit (RFC 6762 §5.4, default: true):
        /// a unicast answer is not subject to the responder's one-per-second multicast limit,
        /// so a name announced a moment ago still resolves at once.
        /// </summary>
        public Boolean   RequestUnicastResponses    { get; init; } = true;

        /// <summary>
        /// The interval of the cache sweep removing expired records (default: 1 s).
        /// </summary>
        public TimeSpan  CacheSweepInterval         { get; init; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The time a record announced with a time-to-live of zero stays cached (RFC 6762 §10.1, default: 1 s).
        /// </summary>
        public TimeSpan  GoodbyeDelay               { get; init; } = MulticastDNS.GoodbyeDelay;

        /// <summary>
        /// Whether IPv6 link-local addresses are returned by address queries (default: false,
        /// because they need an interface scope to be useful).
        /// </summary>
        public Boolean   IncludeLinkLocalIPv6       { get; init; } = false;

        /// <summary>
        /// The minimum random delay of a browser's first query (RFC 6762 §5.2, default: 20 ms).
        /// </summary>
        public TimeSpan  MinInitialBrowseDelay      { get; init; } = TimeSpan.FromMilliseconds(20);

        /// <summary>
        /// The maximum random delay of a browser's first query (RFC 6762 §5.2: "a Multicast DNS
        /// querier SHOULD also delay the first query of the series by a randomly chosen amount
        /// in the range 20-120 ms", default: 120 ms).
        /// </summary>
        public TimeSpan  MaxInitialBrowseDelay      { get; init; } = TimeSpan.FromMilliseconds(120);

        /// <summary>
        /// The initial interval between the repeated queries of a browser (RFC 6762 §5.2, default: 1 s).
        /// </summary>
        public TimeSpan  InitialBrowseInterval      { get; init; } = MulticastDNS.InitialContinuousQueryInterval;

        /// <summary>
        /// The maximum interval between the repeated queries of a browser (RFC 6762 §5.2, default: 60 min).
        /// </summary>
        public TimeSpan  MaxBrowseInterval          { get; init; } = MulticastDNS.MaxContinuousQueryInterval;

        /// <summary>
        /// The interval of the browser maintenance (expiry checks and refresh queries, default: 1 s).
        /// </summary>
        public TimeSpan  BrowseMaintenanceInterval  { get; init; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The fraction of a record's time-to-live after which a browser re-queries it (RFC 6762 §5.2, default: 0.8).
        /// </summary>
        public Double    RefreshFraction            { get; init; } = 0.8;

    }

    #endregion

    #region Cache entry

    /// <summary>
    /// A cached Multicast DNS resource record.
    /// </summary>
    public sealed class MulticastDNSCacheEntry
    {

        /// <summary>
        /// The resource record.
        /// </summary>
        public IDNSResourceRecord  Record        { get; }

        /// <summary>
        /// The time of reception.
        /// </summary>
        public DateTimeOffset      ReceivedAt    { get; internal set; }

        /// <summary>
        /// The time of expiry.
        /// </summary>
        public DateTimeOffset      ExpiresAt     { get; internal set; }

        /// <summary>
        /// The source of the record.
        /// </summary>
        public IPSocket            Source        { get; internal set; }

        internal MulticastDNSCacheEntry(IDNSResourceRecord  Record,
                                        DateTimeOffset      ReceivedAt,
                                        DateTimeOffset      ExpiresAt,
                                        IPSocket            Source)
        {
            this.Record      = Record;
            this.ReceivedAt  = ReceivedAt;
            this.ExpiresAt   = ExpiresAt;
            this.Source      = Source;
        }

        /// <summary>
        /// The remaining time-to-live at the given time.
        /// </summary>
        /// <param name="Now">The current time.</param>
        public TimeSpan RemainingTimeToLive(DateTimeOffset Now)
            => ExpiresAt > Now ? ExpiresAt - Now : TimeSpan.Zero;

        /// <summary>
        /// Whether the record is worth listing as a known answer at the given time (RFC 6762 §7.1:
        /// "a Multicast DNS querier SHOULD NOT include records in the Known-Answer list whose
        /// remaining TTL is less than half of their original TTL"). The original TTL is the one
        /// the record was last received with, i.e. the time from reception to expiry.
        /// </summary>
        /// <param name="Now">The current time.</param>
        internal Boolean IsKnownAnswerAt(DateTimeOffset Now)
        {
            var remaining = RemainingTimeToLive(Now);
            return remaining > TimeSpan.Zero &&
                   remaining + remaining >= ExpiresAt - ReceivedAt;
        }

        /// <summary>
        /// Return a text representation of this cache entry.
        /// </summary>
        public override String ToString()
            => $"{Record} (expires {ExpiresAt:O})";

    }

    #endregion

    #region Delegates

    /// <summary>
    /// A delegate called whenever a response was received.
    /// </summary>
    public delegate Task OnMulticastDNSResponseReceivedDelegate(DateTimeOffset         Timestamp,
                                                                MulticastDNSClient     Sender,
                                                                MulticastDNSMessage    Response,
                                                                MulticastDNSDatagram   Datagram,
                                                                CancellationToken      CancellationToken);

    /// <summary>
    /// A delegate called for every record of a received response.
    /// </summary>
    public delegate Task OnMulticastDNSRecordReceivedDelegate  (DateTimeOffset         Timestamp,
                                                                MulticastDNSClient     Sender,
                                                                MulticastDNSRecord     Record,
                                                                IPSocket               Source,
                                                                CancellationToken      CancellationToken);

    /// <summary>
    /// A delegate called whenever a cached record expired.
    /// </summary>
    public delegate Task OnMulticastDNSRecordExpiredDelegate   (DateTimeOffset         Timestamp,
                                                                MulticastDNSClient     Sender,
                                                                MulticastDNSCacheEntry Entry,
                                                                CancellationToken      CancellationToken);

    #endregion


    /// <summary>
    /// A Multicast DNS client (RFC 6762 §5): one-shot queries with cache, known-answer
    /// lists, retransmission and negative answers (NSEC), a cache honouring cache-flush
    /// and goodbye records (§10), and continuous browsing of DNS-SD services
    /// (RFC 6763, <see cref="MulticastDNSBrowser"/>). It implements <see cref="IDNSClient"/>,
    /// so that "hostname.local" resolves like any other name.
    /// </summary>
    public sealed class MulticastDNSClient : IDNSClient
    {

        #region Data

        private sealed class PendingQuery(DNSServiceName            Name,
                                          DNSResourceRecordTypes[]  Types)
        {

            public DNSServiceName                  Name          { get; } = Name;
            public DNSResourceRecordTypes[]        Types         { get; } = Types;
            public List<IDNSResourceRecord>        Answers       { get; } = [];
            public List<IDNSResourceRecord>        Additionals   { get; } = [];
            public Boolean                         IsNegative    { get; set; }
            public TaskCompletionSource            Signal        { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Boolean Matches(IDNSResourceRecord Record)
                => Name.Equals(Record.DomainName) &&
                   (Types.Contains(DNSResourceRecordTypes.Any) || Types.Contains(Record.Type));

        }

        private sealed record RecentUnicastQuestion(DNSServiceName            Name,
                                                    DNSResourceRecordTypes    Type,
                                                    DateTimeOffset            ExpiresAt);


        private readonly Lock                                          stateLock   = new();
        private readonly Dictionary<String, List<MulticastDNSCacheEntry>>  cache   = [];
        private readonly List<PendingQuery>                            pending     = [];
        private readonly List<RecentUnicastQuestion>                   recentUnicastQuestions = [];
        private readonly List<MulticastDNSBrowser>                     browsers    = [];
        private readonly ILogger?                                      logger;
        private          CancellationTokenSource?                      cancellationTokenSource;
        private          ITimer?                                       sweepTimer;
        private          Boolean                                       isDisposed;

        #endregion

        #region Properties

        /// <summary>
        /// The transport of this client.
        /// </summary>
        public IMulticastDNSTransport    Transport       { get; }

        /// <summary>
        /// The options of this client.
        /// </summary>
        public MulticastDNSClientOptions  Options        { get; }

        /// <summary>
        /// The time provider of this client.
        /// </summary>
        public TimeProvider              TimeProvider    { get; }

        /// <summary>
        /// Whether this client is running.
        /// </summary>
        public Boolean                   IsRunning
            => cancellationTokenSource is not null && !cancellationTokenSource.IsCancellationRequested;

        /// <summary>
        /// The number of cached records.
        /// </summary>
        public Int32                     CacheSize
        {
            get
            {
                lock (stateLock)
                    return cache.Values.Sum(entries => entries.Count);
            }
        }

        /// <summary>
        /// The browsers of this client.
        /// </summary>
        public IReadOnlyList<MulticastDNSBrowser>  Browsers
        {
            get
            {
                lock (stateLock)
                    return [.. browsers];
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever a response was received.
        /// </summary>
        public event OnMulticastDNSResponseReceivedDelegate?  OnResponseReceived;

        /// <summary>
        /// An event fired for every record of a received response.
        /// </summary>
        public event OnMulticastDNSRecordReceivedDelegate?    OnRecordReceived;

        /// <summary>
        /// An event fired whenever a cached record expired.
        /// </summary>
        public event OnMulticastDNSRecordExpiredDelegate?     OnRecordExpired;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new Multicast DNS client.
        /// </summary>
        /// <param name="Transport">The transport (shared with the Multicast DNS responder of the process).</param>
        /// <param name="Options">Optional client options.</param>
        /// <param name="TimeProvider">An optional time provider.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        public MulticastDNSClient(IMulticastDNSTransport      Transport,
                                  MulticastDNSClientOptions?  Options         = null,
                                  TimeProvider?               TimeProvider    = null,
                                  ILoggerFactory?             LoggerFactory   = null)
        {

            ArgumentNullException.ThrowIfNull(Transport);

            this.Transport     = Transport;
            this.Options       = Options      ?? new MulticastDNSClientOptions();
            this.TimeProvider  = TimeProvider ?? System.TimeProvider.System;
            this.logger        = LoggerFactory?.CreateLogger<MulticastDNSClient>();

        }

        #endregion


        #region StartAsync(CancellationToken = default)

        /// <summary>
        /// Start receiving responses (and start the transport when it is not running yet).
        /// </summary>
        public async Task StartAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (IsRunning)
                return;

            cancellationTokenSource       = new CancellationTokenSource();
            Transport.OnDatagramReceived += OnDatagramReceivedAsync;

            if (!Transport.IsRunning)
                await Transport.StartAsync(CancellationToken).ConfigureAwait(false);

            sweepTimer = TimeProvider.CreateTimer(
                             _ => Sweep(),
                             null,
                             Options.CacheSweepInterval,
                             Options.CacheSweepInterval
                         );

        }

        #endregion

        #region StopAsync(CancellationToken = default)

        /// <summary>
        /// Stop the browsers and stop receiving responses.
        /// </summary>
        public async Task StopAsync(CancellationToken CancellationToken = default)
        {

            var cts = cancellationTokenSource;
            if (cts is null)
                return;

            foreach (var browser in Browsers)
                await browser.StopAsync(CancellationToken).ConfigureAwait(false);

            lock (stateLock)
                recentUnicastQuestions.Clear();

            cancellationTokenSource       = null;
            Transport.OnDatagramReceived -= OnDatagramReceivedAsync;

            sweepTimer?.Dispose();
            sweepTimer = null;

            await cts.CancelAsync().ConfigureAwait(false);
            cts.Dispose();

        }

        #endregion


        #region Query(DomainName,     ResourceRecordTypes, Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        /// <summary>
        /// Resolve the given domain name via Multicast DNS.
        /// </summary>
        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = null,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Query(DNSServiceName.Parse(DomainName.FullName),
                     ResourceRecordTypes,
                     Timeout,
                     RecursionDesired,
                     ForceUpdate,
                     CancellationToken);

        #endregion

        #region Query(DNSServiceName, ResourceRecordTypes, Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        /// <summary>
        /// Resolve the given name via Multicast DNS: cached records answer at once, otherwise a
        /// one-shot query is multicast (RFC 6762 §5.1) and its answers are collected until the
        /// grace period after the first answer, a negative answer (NSEC) or the timeout.
        /// </summary>
        /// <param name="DNSServiceName">The name to resolve.</param>
        /// <param name="ResourceRecordTypes">The resource record types to query (empty: ANY).</param>
        /// <param name="Timeout">An optional timeout (default: the QueryTimeout option).</param>
        /// <param name="RecursionDesired">Ignored by Multicast DNS.</param>
        /// <param name="ForceUpdate">Whether to bypass the cache.</param>
        /// <param name="CancellationToken">A token to cancel the query.</param>
        public async Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                         IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                         TimeSpan?                            Timeout             = null,
                                         Boolean?                             RecursionDesired    = null,
                                         Boolean?                             ForceUpdate         = false,
                                         CancellationToken                    CancellationToken   = default)
        {

            ArgumentNullException.ThrowIfNull(DNSServiceName);
            ArgumentNullException.ThrowIfNull(ResourceRecordTypes);

            var types      = ResourceRecordTypes.Distinct().ToArray();
            if (types.Length == 0)
                types = [ DNSResourceRecordTypes.Any ];

            var timeout    = Timeout ?? Options.QueryTimeout;
            var origin     = new DNSServerConfig(MulticastDNS.IPv4Group, Transport.Port, DNSTransport.UDP, timeout);
            var stopwatch  = Stopwatch.StartNew();

            var cts = cancellationTokenSource;
            if (cts is null)
                throw new InvalidOperationException("The Multicast DNS client is not running!");

            #region Cache

            if (Options.UseCache && ForceUpdate != true)
            {

                var cached = CachedRecords(DNSServiceName).
                                 Where(entry => types.Contains(DNSResourceRecordTypes.Any) || types.Contains(entry.Record.Type)).
                                 Select(entry => entry.Record).
                                 ToArray();

                if (cached.Length > 0)
                    return BuildInfo(origin, FilterAddresses(cached), RelatedRecords(cached), timeout, stopwatch.Elapsed);

            }

            #endregion

            var query = new PendingQuery(DNSServiceName, types);

            lock (stateLock)
                pending.Add(query);

            try
            {

                using var linked   = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, cts.Token);

                var deadline       = TimeProvider.GetUtcNow() + timeout;
                var questions      = types.Select(type => new MulticastDNSQuestion(DNSServiceName, type, Options.RequestUnicastResponses)).ToArray();

                // RFC 6762 §5.4: "the unicast-response bit SHOULD NOT be set in subsequent
                // retransmissions of that question. Subsequent retransmissions SHOULD be usual
                // "QM" questions."
                var retransmitted  = types.Select(type => new MulticastDNSQuestion(DNSServiceName, type)).ToArray();
                var interval       = Options.RetransmissionInterval;

                await SendQueryAsync(questions, KnownAnswers(DNSServiceName, types), linked.Token).ConfigureAwait(false);

                while (true)
                {

                    var remaining = deadline - TimeProvider.GetUtcNow();
                    if (remaining <= TimeSpan.Zero)
                        break;

                    var wait = remaining < interval ? remaining : interval;

                    using var delayCTS  = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                    var       delayTask = Task.Delay(wait, TimeProvider, delayCTS.Token);
                    var       completed = await Task.WhenAny(query.Signal.Task, delayTask).ConfigureAwait(false);

                    if (completed == query.Signal.Task)
                    {

                        await delayCTS.CancelAsync().ConfigureAwait(false);

                        if (!query.IsNegative)
                        {
                            var grace = deadline - TimeProvider.GetUtcNow();
                            if (grace > Options.ResponseGracePeriod)
                                grace = Options.ResponseGracePeriod;
                            if (grace > TimeSpan.Zero)
                                await Task.Delay(grace, TimeProvider, linked.Token).ConfigureAwait(false);
                        }

                        break;

                    }

                    linked.Token.ThrowIfCancellationRequested();

                    // A wait the deadline cut short ends the query: a retransmission at the
                    // deadline could not be answered in time, and would come sooner than §5.2 allows.
                    if (wait < interval)
                        break;

                    await SendQueryAsync(retransmitted, KnownAnswers(DNSServiceName, types), linked.Token).ConfigureAwait(false);

                    // RFC 6762 §5.2: "the intervals between successive queries MUST increase by
                    // at least a factor of two".
                    interval += interval;

                }

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {
                return DNSInfo.Failed(origin, 0, timeout);
            }
            finally
            {
                lock (stateLock)
                    pending.Remove(query);
            }

            IDNSResourceRecord[] answers;
            IDNSResourceRecord[] additionals;

            lock (stateLock)
            {
                answers      = [.. query.Answers.    DistinctBy(record => record.RecordKey())];
                additionals  = [.. query.Additionals.DistinctBy(record => record.RecordKey())];
            }

            if (answers.Length == 0 && !query.IsNegative)
                return DNSInfo.TimedOut(origin, 0, timeout);

            return BuildInfo(origin, FilterAddresses(answers), additionals, timeout, stopwatch.Elapsed);

        }

        #endregion

        #region SendQueryAsync(Questions, KnownAnswers = null, CancellationToken = default)

        /// <summary>
        /// Multicast a query with the given questions and known answers (RFC 6762 §7.1).
        /// </summary>
        /// <param name="Questions">The questions.</param>
        /// <param name="KnownAnswers">The known answers (with their remaining time-to-live).</param>
        /// <param name="CancellationToken">A token to cancel the sending.</param>
        public async Task SendQueryAsync(IEnumerable<MulticastDNSQuestion>  Questions,
                                         IEnumerable<MulticastDNSRecord>?   KnownAnswers        = null,
                                         CancellationToken                  CancellationToken   = default)
        {

            if (!Transport.IsRunning)
                throw new InvalidOperationException("The Multicast DNS transport is not running!");

            var questions     = Questions.ToArray();
            var now           = TimeProvider.GetUtcNow();
            var registrations = questions.Where(question => question.UnicastResponseRequested).
                                         Select(question => new RecentUnicastQuestion(
                                                                question.Name,
                                                                question.Type,
                                                                now + Options.QueryTimeout
                                                            )).
                                         ToArray();

            lock (stateLock)
            {
                recentUnicastQuestions.RemoveAll(question => question.ExpiresAt <= now);
                recentUnicastQuestions.AddRange(registrations);
            }

            try
            {
                foreach (var packet in QueryPackets(questions, [.. KnownAnswers ?? []]))
                    await Transport.SendAsync(packet, null, null, CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (stateLock)
                    foreach (var registration in registrations)
                        recentUnicastQuestions.Remove(registration);

                throw;
            }

        }

        #endregion

        #region (private) QueryPackets(Questions, KnownAnswers)

        /// <summary>
        /// The packets of one query (RFC 6762 §7.2): the questions with "as many Known-Answer
        /// records as will fit", then packets "containing no questions and as many more
        /// Known-Answer records as will fit", every packet but the last with the TC bit set.
        /// A known answer too large for a packet of the maximum size goes in a packet of its
        /// own (§17: a packet larger than the MTU "MUST NOT contain more than one resource record").
        /// </summary>
        /// <param name="Questions">The questions.</param>
        /// <param name="KnownAnswers">The known answers.</param>
        private List<Byte[]> QueryPackets(MulticastDNSQuestion[]  Questions,
                                          MulticastDNSRecord[]    KnownAnswers)
        {

            var groups   = new List<List<MulticastDNSRecord>>();
            var current  = new List<MulticastDNSRecord>();

            // The first group carries the questions. The size is measured with the TC bit set,
            // which costs nothing: the header has the same twelve bytes either way.
            Int32 SizeOf(List<MulticastDNSRecord> Answers)
                => MulticastDNSMessage.Query(groups.Count == 0 ? Questions : [], Answers, null, true).Serialize().Length;

            foreach (var knownAnswer in KnownAnswers)
            {

                current.Add(knownAnswer);

                if (SizeOf(current) <= Options.MaxQuerySize)
                    continue;

                current.RemoveAt(current.Count - 1);

                if (current.Count > 0 || groups.Count == 0)
                {
                    groups.Add(current);
                    current = [];
                }

                current.Add(knownAnswer);

                if (SizeOf(current) > Options.MaxQuerySize)
                {
                    groups.Add(current);
                    current = [];
                }

            }

            if (current.Count > 0 || groups.Count == 0)
                groups.Add(current);

            return [.. groups.Select((answers, index) => MulticastDNSMessage.Query(index == 0 ? Questions : [],
                                                                                  answers,
                                                                                  null,
                                                                                  index < groups.Count - 1).Serialize())];

        }

        #endregion

        #region BrowseAsync(ServiceType, CancellationToken = default)

        /// <summary>
        /// Start browsing for instances of the given DNS-SD service type (e.g. "_http._tcp.local.").
        /// </summary>
        /// <param name="ServiceType">The service type name.</param>
        /// <param name="CancellationToken">A token to cancel the start.</param>
        public async Task<MulticastDNSBrowser> BrowseAsync(DNSServiceName     ServiceType,
                                                           CancellationToken  CancellationToken   = default)
        {

            ArgumentNullException.ThrowIfNull(ServiceType);

            if (!IsRunning)
                throw new InvalidOperationException("The Multicast DNS client is not running!");

            var browser = new MulticastDNSBrowser(this, ServiceType, logger);

            lock (stateLock)
                browsers.Add(browser);

            await browser.StartAsync(CancellationToken).ConfigureAwait(false);

            return browser;

        }

        internal void RemoveBrowser(MulticastDNSBrowser Browser)
        {
            lock (stateLock)
                browsers.Remove(Browser);
        }

        #endregion


        #region CachedRecords(Name = null, Type = null)

        /// <summary>
        /// The non-expired cache entries, optionally only those of the given name and type.
        /// </summary>
        /// <param name="Name">An optional owner name.</param>
        /// <param name="Type">An optional resource record type.</param>
        public IReadOnlyList<MulticastDNSCacheEntry> CachedRecords(DNSServiceName?          Name   = null,
                                                                   DNSResourceRecordTypes?  Type   = null)
        {

            var now = TimeProvider.GetUtcNow();

            lock (stateLock)
                return [.. cache.Values.
                               SelectMany(entries => entries).
                               Where(entry => entry.ExpiresAt > now &&
                                              (Name is null || entry.Record.DomainName.Equals(Name)) &&
                                              (!Type.HasValue || entry.Record.Type == Type.Value))];

        }

        #endregion

        #region ClearCache()

        /// <summary>
        /// Remove every cached record.
        /// </summary>
        public void ClearCache()
        {
            lock (stateLock)
                cache.Clear();
        }

        #endregion


        #region (private) OnDatagramReceivedAsync(Timestamp, Sender, Datagram, CancellationToken)

        private async Task OnDatagramReceivedAsync(DateTimeOffset          Timestamp,
                                                   IMulticastDNSTransport  Sender,
                                                   MulticastDNSDatagram    Datagram,
                                                   CancellationToken       CancellationToken)
        {

            if (!IsRunning)
                return;

            // RFC 6762 §6 and §11: mDNS responses originate at the mDNS port and
            // are accepted only when the transport can establish local-link delivery.
            if (Datagram.RemoteSocket.Port != Transport.Port || !Datagram.SourceIsOnLocalLink)
                return;

            if (!MulticastDNSMessage.TryParse(Datagram.Payload, out var message, out var error))
            {
                logger?.LogDebug("Multicast DNS client: ignoring an unparsable packet from {Source}: {Error}", Datagram.RemoteSocket, error);
                return;
            }

            // RFC 6762 §18.3 and §18.11: only standard, successful messages are mDNS.
            if (message.Opcode       != 0 ||
                message.ResponseCode != DNSResponseCodes.NoError)
            {
                return;
            }

            // RFC 6762 §7.3: another host's multicast query tells the browsers which of their
            // questions have just been asked. This host's own queries come back as well, and
            // so do the probes of a responder sharing the transport; neither counts.
            if (!message.IsResponse)
            {

                if (Datagram.WasReceivedViaMulticast &&
                    !Transport.LocalAddresses.Any(address => address.Equals(Datagram.RemoteSocket.IPAddress)))
                {
                    foreach (var browser in Browsers)
                        browser.ObserveQuery(message);
                }

                return;

            }

            // RFC 6762 §6: a unicast response is accepted only after a recent query
            // explicitly requested one via the QU bit. Unknown delivery metadata is
            // treated like unicast and therefore also fails closed without correlation.
            if (!Datagram.WasReceivedViaMulticast && !IsExpectedUnicastResponse(message))
                return;

            await HandleResponseAsync(message, Datagram, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region (private) IsExpectedUnicastResponse(Response)

        private Boolean IsExpectedUnicastResponse(MulticastDNSMessage Response)
        {

            var now      = TimeProvider.GetUtcNow();
            var answers  = Response.AllRecords.Select(answer => answer.Record).ToArray();

            lock (stateLock)
            {

                recentUnicastQuestions.RemoveAll(question => question.ExpiresAt <= now);

                return recentUnicastQuestions.Any(
                           question => answers.Any(
                                           answer => question.Name.Equals(answer.DomainName) &&
                                                     (question.Type == DNSResourceRecordTypes.Any ||
                                                      question.Type == answer.Type ||
                                                      answer.Type   == DNSResourceRecordTypes.NSEC)
                                       )
                       );

            }

        }

        #endregion

        #region (private) HandleResponseAsync(Response, Datagram, CancellationToken)

        private async Task HandleResponseAsync(MulticastDNSMessage   Response,
                                               MulticastDNSDatagram  Datagram,
                                               CancellationToken     CancellationToken)
        {

            var now      = TimeProvider.GetUtcNow();
            var records  = Response.Answers.Concat(Response.Additionals).ToArray();

            #region Cache (RFC 6762 §10)

            lock (stateLock)
            {

                foreach (var record in records)
                {

                    if (record.Record is OPT)
                        continue;

                    var key = record.Record.RRSetKey();

                    if (!cache.TryGetValue(key, out var entries))
                    {
                        entries = [];
                        cache[key] = entries;
                    }

                    // §10.2: the cache-flush bit invalidates every record of the RRSet received
                    // more than one second ago; they get one more second to live.
                    if (record.CacheFlush)
                    {
                        foreach (var entry in entries.Where(entry => now - entry.ReceivedAt > TimeSpan.FromSeconds(1) &&
                                                                     entry.ExpiresAt > now + TimeSpan.FromSeconds(1)))
                            entry.ExpiresAt = now + TimeSpan.FromSeconds(1);
                    }

                    var existing = entries.FirstOrDefault(entry => entry.Record.IsIdenticalTo(record.Record));

                    // §10.1: a goodbye record expires after one second instead of at once.
                    if (record.IsGoodbye)
                    {
                        if (existing is not null && existing.ExpiresAt > now + Options.GoodbyeDelay)
                            existing.ExpiresAt = now + Options.GoodbyeDelay;
                        continue;
                    }

                    if (existing is not null)
                    {
                        existing.ReceivedAt  = now;
                        existing.ExpiresAt   = now + record.TimeToLive;
                        existing.Source      = Datagram.RemoteSocket;
                    }
                    else
                        entries.Add(new MulticastDNSCacheEntry(record.Record, now, now + record.TimeToLive, Datagram.RemoteSocket));

                }

                #region Pending one-shot queries

                foreach (var query in pending)
                {

                    var matches = Response.Answers.Where(record => !record.IsGoodbye && query.Matches(record.Record)).ToArray();

                    var negative = Response.Answers.
                                       Select(record => record.Record).
                                       OfType<NSEC>().
                                       Where (nsec => query.Name.Equals(nsec.DomainName)).
                                       ToArray();

                    if (negative.Length > 0 &&
                        !query.Types.Contains(DNSResourceRecordTypes.Any) &&
                        query.Types.All(type => !negative.Any(nsec => MulticastDNSTypeBitmap.Contains(nsec.TypeBitMaps, type))))
                    {
                        query.IsNegative = true;
                        query.Signal.TrySetResult();
                        continue;
                    }

                    if (matches.Length == 0)
                        continue;

                    query.Answers.    AddRange(matches.Select(record => record.Record));
                    query.Additionals.AddRange(records.Where(record => !record.IsGoodbye && !matches.Contains(record)).
                                                       Select(record => record.Record));
                    query.Signal.TrySetResult();

                }

                #endregion

            }

            #endregion

            await OnResponseReceived.InvokeAllAsync(
                      handler => handler(now, this, Response, Datagram, CancellationToken),
                      logger
                  ).ConfigureAwait(false);

            foreach (var record in records)
            {
                await OnRecordReceived.InvokeAllAsync(
                          handler => handler(now, this, record, Datagram.RemoteSocket, CancellationToken),
                          logger
                      ).ConfigureAwait(false);
            }

        }

        #endregion

        #region (private) Sweep()

        private void Sweep()
        {

            var now      = TimeProvider.GetUtcNow();
            var expired  = new List<MulticastDNSCacheEntry>();

            lock (stateLock)
            {

                foreach (var (key, entries) in cache.ToArray())
                {

                    expired.AddRange(entries.Where(entry => entry.ExpiresAt <= now));
                    entries.RemoveAll(entry => entry.ExpiresAt <= now);

                    if (entries.Count == 0)
                        cache.Remove(key);

                }

            }

            if (expired.Count == 0)
                return;

            var cts = cancellationTokenSource;
            if (cts is null)
                return;

            _ = Task.Run(async () => {
                foreach (var entry in expired)
                {
                    await OnRecordExpired.InvokeAllAsync(
                              handler => handler(now, this, entry, cts.Token),
                              logger
                          ).ConfigureAwait(false);
                }
            }, CancellationToken.None);

        }

        #endregion

        #region (private) KnownAnswers(Name) / RelatedRecords(Records) / FilterAddresses(Records) / BuildInfo(...)

        /// <summary>
        /// The known answers of a query (RFC 6762 §7.1): the cached records of the name that are not
        /// of a queried type. A record of a queried type is never listed, because a responder would
        /// suppress it (§7.1) and the query is sent precisely because the querier wants that type
        /// answered (a cache miss, or a forced update).
        /// </summary>
        private IEnumerable<MulticastDNSRecord> KnownAnswers(DNSServiceName            Name,
                                                             DNSResourceRecordTypes[]  QueriedTypes)
        {

            var now = TimeProvider.GetUtcNow();

            if (QueriedTypes.Contains(DNSResourceRecordTypes.Any))
                return [];

            return CachedRecords(Name).
                       Where (entry => entry.IsKnownAnswerAt(now) &&
                                       !QueriedTypes.Contains(entry.Record.Type)).
                       Select(entry => new MulticastDNSRecord(entry.Record, false, entry.RemainingTimeToLive(now)));

        }

        private IDNSResourceRecord[] RelatedRecords(IEnumerable<IDNSResourceRecord> Records)
        {

            var related = new List<IDNSResourceRecord>();

            foreach (var record in Records)
            {

                switch (record)
                {

                    case PTR ptr:
                        foreach (var entry in CachedRecords(ptr.Target).Where(entry => entry.Record.Type is DNSResourceRecordTypes.SRV or DNSResourceRecordTypes.TXT))
                        {
                            related.Add(entry.Record);
                            if (entry.Record is SRV srv)
                                related.AddRange(AddressRecords(srv.Target));
                        }
                        break;

                    case SRV srv:
                        related.AddRange(AddressRecords(srv.Target));
                        break;

                }

            }

            return [.. related.DistinctBy(record => record.RecordKey())];

        }

        private IEnumerable<IDNSResourceRecord> AddressRecords(DomainName Target)
        {

            if (!DNSServiceName.TryParse(Target.FullName, out var name, out _))
                return [];

            return CachedRecords(name).
                       Where (entry => entry.Record.Type is DNSResourceRecordTypes.A or DNSResourceRecordTypes.AAAA).
                       Select(entry => entry.Record);

        }

        private IDNSResourceRecord[] FilterAddresses(IEnumerable<IDNSResourceRecord> Records)

            => Options.IncludeLinkLocalIPv6
                   ? [.. Records]
                   : [.. Records.Where(record => record is not AAAA aaaa ||
                                                 !((System.Net.IPAddress) aaaa.IPv6Address).IsIPv6LinkLocal)];

        private static DNSInfo BuildInfo(DNSServerConfig                  Origin,
                                         IEnumerable<IDNSResourceRecord>  Answers,
                                         IEnumerable<IDNSResourceRecord>  Additionals,
                                         TimeSpan                         Timeout,
                                         TimeSpan                         Runtime)

            => new (Origin,
                    0,
                    true,
                    false,
                    false,
                    false,
                    DNSResponseCodes.NoError,
                    Answers,
                    [],
                    Additionals,
                    true,
                    false,
                    Timeout,
                    Runtime);

        #endregion


        #region Dispose() / DisposeAsync()

        /// <summary>
        /// Stop the client.
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Stop the client.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (isDisposed)
                return;

            isDisposed = true;

            await StopAsync().ConfigureAwait(false);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this client.
        /// </summary>
        public override String ToString()

            => $"mDNS client ({CacheSize} cached record(s)) on {Transport}";

        #endregion

    }

}
