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

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    #region Service instance

    /// <summary>
    /// A DNS-SD service instance discovered by a <see cref="MulticastDNSBrowser"/> (RFC 6763 §4 to §6):
    /// the instance name (from the PTR record), host and port (SRV), the optional TXT record and the
    /// addresses of the host (A/AAAA).
    /// </summary>
    public sealed class MulticastDNSServiceInstance
    {

        #region Properties

        /// <summary>
        /// The service instance name (e.g. "myhost._http._tcp.local.").
        /// </summary>
        public DNSServiceName             InstanceName    { get; }

        /// <summary>
        /// The service type (e.g. "_http._tcp.local.").
        /// </summary>
        public DNSServiceName             ServiceType     { get; }

        /// <summary>
        /// The host name of the instance (the SRV target), when known.
        /// </summary>
        public DomainName?                HostName        { get; internal set; }

        /// <summary>
        /// The port of the instance (from the SRV record), when known.
        /// </summary>
        public IPPort?                    Port            { get; internal set; }

        /// <summary>
        /// The priority of the instance (from the SRV record).
        /// </summary>
        public UInt16                     Priority        { get; internal set; }

        /// <summary>
        /// The weight of the instance (from the SRV record).
        /// </summary>
        public UInt16                     Weight          { get; internal set; }

        /// <summary>
        /// The TXT record of the instance, when known.
        /// </summary>
        public TXT?                       TXT             { get; internal set; }

        /// <summary>
        /// The addresses of the host, when known.
        /// </summary>
        public IReadOnlyList<IIPAddress>  Addresses       { get; internal set; } = [];

        /// <summary>
        /// When the instance was first seen.
        /// </summary>
        public DateTimeOffset             FirstSeen       { get; }

        /// <summary>
        /// When the instance was last seen (PTR record received).
        /// </summary>
        public DateTimeOffset             LastSeen        { get; internal set; }

        /// <summary>
        /// Whether the host and port needed to contact the service are known.
        /// RFC 6763 section 6.1 requires clients to treat a missing TXT record like an empty one.
        /// </summary>
        public Boolean                    IsResolved
            => HostName is not null && Port.HasValue;

        internal Boolean                  Announced          { get; set; }
        internal Boolean                  RefreshRequested   { get; set; }

        #endregion

        #region Constructor(s)

        internal MulticastDNSServiceInstance(DNSServiceName  InstanceName,
                                             DNSServiceName  ServiceType,
                                             DateTimeOffset  Now)
        {

            this.InstanceName  = InstanceName;
            this.ServiceType   = ServiceType;
            this.FirstSeen     = Now;
            this.LastSeen      = Now;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this service instance.
        /// </summary>
        public override String ToString()

            => String.Concat(
                   InstanceName.FullName,
                   HostName is not null ? $" at {HostName.FullName}:{Port}" : " (unresolved)",
                   Addresses.Count > 0  ? $" [{String.Join(", ", Addresses)}]" : ""
               );

        #endregion

    }

    #endregion

    #region Delegates

    /// <summary>
    /// A delegate called whenever a service instance appeared, changed or disappeared.
    /// </summary>
    public delegate Task OnMulticastDNSServiceInstanceDelegate(DateTimeOffset               Timestamp,
                                                               MulticastDNSBrowser          Sender,
                                                               MulticastDNSServiceInstance  Instance,
                                                               CancellationToken            CancellationToken);

    #endregion


    /// <summary>
    /// A continuous DNS-SD browser (RFC 6763 §4, RFC 6762 §5.2): it repeats the PTR query for a
    /// service type with a growing interval and known-answer lists, resolves every instance
    /// (SRV, optional TXT, A/AAAA), refreshes records towards the end of their lifetime and reports
    /// appeared, updated and disappeared instances. An instance is reported as appeared once
    /// host and port are known.
    /// </summary>
    public sealed class MulticastDNSBrowser : IAsyncDisposable
    {

        #region Data

        private readonly Lock                                              stateLock   = new();
        private readonly Dictionary<String, MulticastDNSServiceInstance>   instances   = [];
        private readonly ILogger?                                          logger;
        private          CancellationTokenSource?                          cancellationTokenSource;
        private          Task?                                             queryLoop;
        private          ITimer?                                           maintenanceTimer;
        private          Boolean                                           isDisposed;
        private          Int32                                             duplicateQuestionSeen;

        #endregion

        #region Properties

        /// <summary>
        /// The Multicast DNS client of this browser.
        /// </summary>
        public MulticastDNSClient  Client         { get; }

        /// <summary>
        /// The browsed service type (e.g. "_http._tcp.local.").
        /// </summary>
        public DNSServiceName      ServiceType    { get; }

        /// <summary>
        /// Whether this browser is running.
        /// </summary>
        public Boolean             IsRunning
            => cancellationTokenSource is not null && !cancellationTokenSource.IsCancellationRequested;

        /// <summary>
        /// The resolved service instances.
        /// </summary>
        public IReadOnlyList<MulticastDNSServiceInstance>  Instances
        {
            get
            {
                lock (stateLock)
                    return [.. instances.Values.Where(instance => instance.IsResolved)];
            }
        }

        /// <summary>
        /// All service instances, including the ones not resolved yet.
        /// </summary>
        public IReadOnlyList<MulticastDNSServiceInstance>  AllInstances
        {
            get
            {
                lock (stateLock)
                    return [.. instances.Values];
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever a resolved service instance appeared.
        /// </summary>
        public event OnMulticastDNSServiceInstanceDelegate?  OnInstanceAdded;

        /// <summary>
        /// An event fired whenever a reported service instance changed (host, port, TXT record or addresses).
        /// </summary>
        public event OnMulticastDNSServiceInstanceDelegate?  OnInstanceUpdated;

        /// <summary>
        /// An event fired whenever a reported service instance disappeared (goodbye or expiry).
        /// </summary>
        public event OnMulticastDNSServiceInstanceDelegate?  OnInstanceRemoved;

        #endregion

        #region Constructor(s)

        internal MulticastDNSBrowser(MulticastDNSClient  Client,
                                     DNSServiceName      ServiceType,
                                     ILogger?            Logger)
        {

            this.Client       = Client;
            this.ServiceType  = ServiceType;
            this.logger       = Logger;

        }

        #endregion


        #region StartAsync(CancellationToken = default)

        /// <summary>
        /// Start browsing: seed the instances from the cache, subscribe to the client and
        /// start the query loop and the maintenance timer.
        /// </summary>
        public async Task StartAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (IsRunning)
                return;

            cancellationTokenSource   = new CancellationTokenSource();
            var token                 = cancellationTokenSource.Token;

            Client.OnRecordReceived  += OnRecordReceivedAsync;
            Client.OnRecordExpired   += OnRecordExpiredAsync;

            #region Seed from the cache

            var now      = Client.TimeProvider.GetUtcNow();
            var toQuery  = new List<MulticastDNSServiceInstance>();

            lock (stateLock)
            {

                foreach (var entry in Client.CachedRecords(ServiceType, DNSResourceRecordTypes.PTR))
                {

                    if (entry.Record is not PTR ptr)
                        continue;

                    var instance = GetOrAddInstance(ptr.Target, now);

                    ApplyCached(instance);

                    if (instance.IsResolved)
                        instance.Announced = true;

                    if (NeedsDetails(instance))
                        toQuery.Add(instance);

                }

            }

            #endregion

            foreach (var instance in toQuery.Distinct())
                await QueryInstanceAsync(instance, CancellationToken).ConfigureAwait(false);

            queryLoop         = Task.Run(() => QueryLoopAsync(token), CancellationToken.None);

            maintenanceTimer  = Client.TimeProvider.CreateTimer(
                                    _ => Maintenance(),
                                    null,
                                    Client.Options.BrowseMaintenanceInterval,
                                    Client.Options.BrowseMaintenanceInterval
                                );

        }

        #endregion

        #region StopAsync(CancellationToken = default)

        /// <summary>
        /// Stop browsing.
        /// </summary>
        public async Task StopAsync(CancellationToken CancellationToken = default)
        {

            var cts = cancellationTokenSource;
            if (cts is null)
                return;

            cancellationTokenSource   = null;

            Client.OnRecordReceived  -= OnRecordReceivedAsync;
            Client.OnRecordExpired   -= OnRecordExpiredAsync;

            maintenanceTimer?.Dispose();
            maintenanceTimer = null;

            await cts.CancelAsync().ConfigureAwait(false);

            if (queryLoop is not null)
            {
                try
                {
                    await queryLoop.ConfigureAwait(false);
                }
                catch
                { }
                queryLoop = null;
            }

            cts.Dispose();

            Client.RemoveBrowser(this);

        }

        #endregion

        #region QueryNowAsync(CancellationToken = default)

        /// <summary>
        /// Send the PTR query for the service type now (with the known answers).
        /// </summary>
        public Task QueryNowAsync(CancellationToken CancellationToken = default)
        {

            var now = Client.TimeProvider.GetUtcNow();

            var knownAnswers = KnownAnswers(now).
                                   Select(entry => new MulticastDNSRecord(entry.Record, false, entry.RemainingTimeToLive(now)));

            return Client.SendQueryAsync(
                       [ new MulticastDNSQuestion(ServiceType, DNSResourceRecordTypes.PTR) ],
                       knownAnswers,
                       CancellationToken
                   );

        }

        #endregion

        #region (private)  KnownAnswers(Now)

        /// <summary>
        /// The cached pointers worth listing as known answers (RFC 6762 §7.1: not those whose
        /// remaining TTL is less than half of their original TTL).
        /// </summary>
        private IEnumerable<MulticastDNSCacheEntry> KnownAnswers(DateTimeOffset Now)

            => Client.CachedRecords(ServiceType, DNSResourceRecordTypes.PTR).
                      Where(entry => entry.IsKnownAnswerAt(Now));

        #endregion

        #region (internal) ObserveQuery(Query)

        /// <summary>
        /// Another host's query (RFC 6762 §7.3): "If a host is planning to transmit (or
        /// retransmit) a query, and it sees another host on the network send a query containing
        /// the same "QM" question, and the Known-Answer Section of that query does not contain
        /// any records that this host would not also put in its own Known-Answer Section, then
        /// this host SHOULD treat its own query as having been sent."
        /// </summary>
        /// <remarks>
        /// A truncated query is not enough: its other known answers are still on their way, and
        /// may be ones this browser does not have.
        /// </remarks>
        /// <param name="Query">A query of another host.</param>
        internal void ObserveQuery(MulticastDNSMessage Query)
        {

            if (!IsRunning || Query.Truncated)
                return;

            if (!Query.Questions.Any(question => !question.UnicastResponseRequested                &&
                                                  question.Type  == DNSResourceRecordTypes.PTR      &&
                                                  question.Class == DNSQueryClasses.IN              &&
                                                  question.Name.Equals(ServiceType)))
                return;

            var ours = KnownAnswers(Client.TimeProvider.GetUtcNow()).Select(entry => entry.Record).ToArray();

            if (Query.Answers.All(theirs => ours.Any(record => record.IsIdenticalTo(theirs.Record))))
                Interlocked.Exchange(ref duplicateQuestionSeen, 1);

        }

        #endregion


        #region (private) QueryLoopAsync(CancellationToken)

        private async Task QueryLoopAsync(CancellationToken CancellationToken)
        {

            var interval = Client.Options.InitialBrowseInterval;

            // RFC 6762 §5.2: "a Multicast DNS querier SHOULD also delay the first query of the
            // series by a randomly chosen amount in the range 20-120 ms".
            var minDelay  = Client.Options.MinInitialBrowseDelay;
            var maxDelay  = Client.Options.MaxInitialBrowseDelay;
            var delay     = maxDelay > minDelay
                                ? minDelay + TimeSpan.FromTicks((Int64) (Random.Shared.NextDouble() * (maxDelay - minDelay).Ticks))
                                : minDelay;

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, Client.TimeProvider, CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            while (!CancellationToken.IsCancellationRequested)
            {

                // RFC 6762 §7.3: a question another host has asked since this browser last
                // asked it counts as asked; the series goes on from there.
                if (Interlocked.Exchange(ref duplicateQuestionSeen, 0) == 0)
                {
                    try
                    {
                        await QueryNowAsync(CancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        logger?.LogWarning(e, "Multicast DNS browser: querying {ServiceType} failed", ServiceType);
                    }
                }

                try
                {
                    await Task.Delay(interval, Client.TimeProvider, CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // RFC 6762 §5.2: double the interval up to the maximum.
                interval = interval + interval;
                if (interval > Client.Options.MaxBrowseInterval)
                    interval = Client.Options.MaxBrowseInterval;

            }

        }

        #endregion

        #region (private) OnRecordReceivedAsync(Timestamp, Sender, Record, Source, CancellationToken)

        private async Task OnRecordReceivedAsync(DateTimeOffset      Timestamp,
                                                 MulticastDNSClient  Sender,
                                                 MulticastDNSRecord  Record,
                                                 IPSocket            Source,
                                                 CancellationToken   CancellationToken)
        {

            if (!IsRunning)
                return;

            var added    = new List<MulticastDNSServiceInstance>();
            var updated  = new List<MulticastDNSServiceInstance>();
            var removed  = new List<MulticastDNSServiceInstance>();
            var toQuery  = new List<MulticastDNSServiceInstance>();

            lock (stateLock)
            {

                switch (Record.Record)
                {

                    case PTR ptr when ptr.DomainName.Equals(ServiceType):

                        if (Record.IsGoodbye)
                        {

                            // Normally the cache expiry (one second later) removes the instance;
                            // without a goodbye delay it goes at once.
                            if (Client.Options.GoodbyeDelay <= TimeSpan.Zero &&
                                instances.Remove(Key(ptr.Target.FullName), out var gone) &&
                                gone.Announced)
                            {
                                removed.Add(gone);
                            }

                            break;

                        }

                        var isNew     = !instances.ContainsKey(Key(ptr.Target.FullName));
                        var instance  = GetOrAddInstance(ptr.Target, Timestamp);

                        instance.LastSeen          = Timestamp;
                        instance.RefreshRequested  = false;

                        if (isNew)
                            ApplyCached(instance);

                        if (NeedsDetails(instance))
                            toQuery.Add(instance);

                        Announce(instance, added, updated, false);

                        break;


                    case SRV or TXT:

                        if (instances.TryGetValue(Key(Record.Record.DomainName.FullName), out var detailInstance))
                        {

                            // An SRV record is one of possibly several: the instance takes the
                            // preferred one of those cached, which this record has just joined.
                            var changed = Record.Record is SRV
                                              ? PreferredService(detailInstance) is SRV preferred && Apply(detailInstance, preferred)
                                              : Apply(detailInstance, Record.Record);

                            // A host name learnt from the SRV record may already have cached addresses
                            // (announcements list them before the browser asks).
                            if (changed && Record.Record is SRV && detailInstance.HostName is not null)
                                foreach (var address in Client.CachedRecords().Where(address => SameName(address.Record.DomainName.FullName, detailInstance.HostName.FullName)))
                                    Apply(detailInstance, address.Record);

                            if (detailInstance.HostName is not null && detailInstance.Addresses.Count == 0)
                                toQuery.Add(detailInstance);

                            Announce(detailInstance, added, updated, changed);

                        }

                        break;


                    case A or AAAA:

                        foreach (var hostInstance in instances.Values.Where(instance => instance.HostName is not null &&
                                                                                        SameName(instance.HostName.FullName, Record.Record.DomainName.FullName)))
                        {
                            var changed = Apply(hostInstance, Record.Record);
                            Announce(hostInstance, added, updated, changed);
                        }

                        break;

                }

            }

            foreach (var instance in toQuery)
                await QueryInstanceAsync(instance, CancellationToken).ConfigureAwait(false);

            await RaiseAsync(added, updated, removed, Timestamp, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region (private) OnRecordExpiredAsync(Timestamp, Sender, Entry, CancellationToken)

        private async Task OnRecordExpiredAsync(DateTimeOffset          Timestamp,
                                                MulticastDNSClient      Sender,
                                                MulticastDNSCacheEntry  Entry,
                                                CancellationToken       CancellationToken)
        {

            if (!IsRunning)
                return;

            var removed  = new List<MulticastDNSServiceInstance>();
            var updated  = new List<MulticastDNSServiceInstance>();
            var toQuery  = new List<MulticastDNSServiceInstance>();

            lock (stateLock)
            {

                switch (Entry.Record)
                {

                    case PTR ptr when ptr.DomainName.Equals(ServiceType):

                        // Another PTR for the same instance may still be cached (e.g. from another source).
                        if (Client.CachedRecords(ServiceType, DNSResourceRecordTypes.PTR).Any(other => other.Record is PTR otherPtr && otherPtr.Target.Equals(ptr.Target)))
                            break;

                        if (instances.Remove(Key(ptr.Target.FullName), out var gone) && gone.Announced)
                            removed.Add(gone);

                        break;


                    case SRV or TXT:

                        if (instances.TryGetValue(Key(Entry.Record.DomainName.FullName), out var instance))
                        {

                            // Another SRV record of the instance may still be cached, and now be
                            // the preferred one.
                            if (Entry.Record is SRV && PreferredService(instance) is SRV next && Apply(instance, next))
                            {
                                ApplyCachedAddresses(instance);
                                if (instance.Announced)
                                    updated.Add(instance);
                            }

                            toQuery.Add(instance);

                        }

                        break;


                    case A a:

                        foreach (var hostInstance in instances.Values.Where(instance => instance.HostName is not null &&
                                                                                        SameName(instance.HostName.FullName, a.DomainName.FullName)))
                        {
                            hostInstance.Addresses = [.. hostInstance.Addresses.Where(address => !address.Equals(a.IPv4Address))];
                            if (hostInstance.Announced)
                                updated.Add(hostInstance);
                            toQuery.Add(hostInstance);
                        }

                        break;


                    case AAAA aaaa:

                        foreach (var hostInstance in instances.Values.Where(instance => instance.HostName is not null &&
                                                                                        SameName(instance.HostName.FullName, aaaa.DomainName.FullName)))
                        {
                            hostInstance.Addresses = [.. hostInstance.Addresses.Where(address => !address.Equals(aaaa.IPv6Address))];
                            if (hostInstance.Announced)
                                updated.Add(hostInstance);
                            toQuery.Add(hostInstance);
                        }

                        break;

                }

            }

            foreach (var instance in toQuery.Distinct())
                await QueryInstanceAsync(instance, CancellationToken).ConfigureAwait(false);

            await RaiseAsync([], updated, removed, Timestamp, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region (private) Maintenance()

        private void Maintenance()
        {

            var cts = cancellationTokenSource;
            if (cts is null)
                return;

            var now       = Client.TimeProvider.GetUtcNow();
            var refresh   = new List<MulticastDNSServiceInstance>();
            var expired   = new List<MulticastDNSServiceInstance>();

            lock (stateLock)
            {

                var pointers = Client.CachedRecords(ServiceType, DNSResourceRecordTypes.PTR);

                foreach (var instance in instances.Values)
                {

                    var pointer = pointers.FirstOrDefault(entry => entry.Record is PTR ptr && ptr.Target.Equals(instance.InstanceName));

                    if (pointer is null)
                    {
                        // The PTR expired while the browser was not listening (or never existed in the cache).
                        if (now - instance.LastSeen > MulticastDNS.SharedRecordTimeToLive)
                            expired.Add(instance);
                        continue;
                    }

                    var lifetime   = pointer.ExpiresAt - pointer.ReceivedAt;
                    var remaining  = pointer.RemainingTimeToLive(now);

                    // RFC 6762 §5.2: re-query at 80 % of the lifetime.
                    if (!instance.RefreshRequested &&
                        lifetime > TimeSpan.Zero &&
                        remaining < lifetime * (1 - Client.Options.RefreshFraction))
                    {
                        instance.RefreshRequested = true;
                        refresh.Add(instance);
                    }

                }

                foreach (var instance in expired)
                    instances.Remove(Key(instance.InstanceName.FullName));

            }

            if (refresh.Count == 0 && expired.Count == 0)
                return;

            _ = Task.Run(async () => {

                try
                {

                    if (refresh.Count > 0)
                    {

                        await QueryNowAsync(cts.Token).ConfigureAwait(false);

                        foreach (var instance in refresh)
                            await QueryInstanceAsync(instance, cts.Token).ConfigureAwait(false);

                    }

                    await RaiseAsync([], [], expired.Where(instance => instance.Announced).ToList(), now, cts.Token).ConfigureAwait(false);

                }
                catch (OperationCanceledException)
                { }
                catch (Exception e)
                {
                    logger?.LogWarning(e, "Multicast DNS browser: maintenance of {ServiceType} failed", ServiceType);
                }

            }, CancellationToken.None);

        }

        #endregion

        #region (private) QueryInstanceAsync(Instance, CancellationToken)

        private async Task QueryInstanceAsync(MulticastDNSServiceInstance  Instance,
                                              CancellationToken            CancellationToken)
        {

            if (!Client.Transport.IsRunning)
                return;

            var questions = new List<MulticastDNSQuestion> {
                                new (Instance.InstanceName, DNSResourceRecordTypes.SRV),
                                new (Instance.InstanceName, DNSResourceRecordTypes.TXT)
                            };

            if (Instance.HostName is not null &&
                DNSServiceName.TryParse(Instance.HostName.FullName, out var hostName, out _))
            {
                questions.Add(new MulticastDNSQuestion(hostName, DNSResourceRecordTypes.A));
                questions.Add(new MulticastDNSQuestion(hostName, DNSResourceRecordTypes.AAAA));
            }

            try
            {
                await Client.SendQueryAsync(questions, null, CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            { }
            catch (Exception e)
            {
                logger?.LogWarning(e, "Multicast DNS browser: querying {Instance} failed", Instance.InstanceName);
            }

        }

        #endregion

        #region (private) ApplyCached(Instance) / ApplyCachedAddresses(Instance) / PreferredService(Instance)

        /// <summary>
        /// Apply what the cache knows about the instance: its TXT record, its preferred SRV
        /// record and the addresses of the host that one names.
        /// </summary>
        private void ApplyCached(MulticastDNSServiceInstance Instance)
        {

            foreach (var detail in Client.CachedRecords(Instance.InstanceName).Where(entry => entry.Record is not SRV))
                Apply(Instance, detail.Record);

            if (PreferredService(Instance) is SRV srv)
                Apply(Instance, srv);

            ApplyCachedAddresses(Instance);

        }

        private void ApplyCachedAddresses(MulticastDNSServiceInstance Instance)
        {

            if (Instance.HostName is null)
                return;

            foreach (var address in Client.CachedRecords().Where(address => SameName(address.Record.DomainName.FullName, Instance.HostName.FullName)))
                Apply(Instance, address.Record);

        }

        /// <summary>
        /// The SRV record that describes the instance, of those cached (RFC 6763 §5: "In the
        /// event that more than one SRV is returned, clients MUST correctly interpret the
        /// priority and weight fields -- i.e., lower-numbered priority servers should be used
        /// in preference to higher-numbered priority servers"). Of several with the lowest
        /// priority the one heard last wins, so that a changed port or host is taken at once.
        /// </summary>
        private SRV? PreferredService(MulticastDNSServiceInstance Instance)
        {

            var services = Client.CachedRecords(Instance.InstanceName, DNSResourceRecordTypes.SRV).
                                  Where(entry => entry.Record is SRV).
                                  ToArray();

            if (services.Length == 0)
                return null;

            var priority = services.Min(entry => ((SRV) entry.Record).Priority);

            return (SRV) services.Where  (entry => ((SRV) entry.Record).Priority == priority).
                                  OrderBy(entry => entry.ReceivedAt).
                                  Last().
                                  Record;

        }

        #endregion

        #region (private) GetOrAddInstance / Apply / Announce / RaiseAsync / Key / SameName

        private MulticastDNSServiceInstance GetOrAddInstance(DNSServiceName  InstanceName,
                                                             DateTimeOffset  Now)
        {

            var key = Key(InstanceName.FullName);

            if (!instances.TryGetValue(key, out var instance))
            {
                instance = new MulticastDNSServiceInstance(InstanceName, ServiceType, Now);
                instances.Add(key, instance);
            }

            return instance;

        }

        /// <summary>
        /// Apply a record to the instance; returns whether anything changed.
        /// </summary>
        private static Boolean Apply(MulticastDNSServiceInstance  Instance,
                                     IDNSResourceRecord           Record)
        {

            switch (Record)
            {

                case SRV srv when srv.DomainName.Equals(Instance.InstanceName):

                    var hostChanged = Instance.HostName is null || !Instance.HostName.Equals(srv.Target);

                    if (!hostChanged &&
                        Instance.Port == srv.Port &&
                        Instance.Priority == srv.Priority &&
                        Instance.Weight == srv.Weight)
                        return false;

                    if (hostChanged)
                        Instance.Addresses = [];

                    Instance.HostName  = srv.Target;
                    Instance.Port      = srv.Port;
                    Instance.Priority  = srv.Priority;
                    Instance.Weight    = srv.Weight;
                    return true;


                case TXT txt when txt.DomainName.Equals(Instance.InstanceName):

                    // RFC 6763 §6.1: "DNS-SD clients MUST treat the following as equivalent":
                    // a TXT record holding a single empty string, an empty TXT record, and no TXT
                    // record. Learning which of them it is changes nothing.
                    if (IsEmpty(Instance.TXT) && IsEmpty(txt))
                    {
                        Instance.TXT ??= txt;
                        return false;
                    }

                    if (Instance.TXT is not null && Instance.TXT.Strings.SequenceEqual(txt.Strings, StringComparer.Ordinal))
                        return false;

                    Instance.TXT = txt;
                    return true;


                case A a when Instance.HostName is not null && SameName(a.DomainName.FullName, Instance.HostName.FullName):

                    if (Instance.Addresses.Any(address => address.Equals(a.IPv4Address)))
                        return false;

                    Instance.Addresses = [.. Instance.Addresses, a.IPv4Address];
                    return true;


                case AAAA aaaa when Instance.HostName is not null && SameName(aaaa.DomainName.FullName, Instance.HostName.FullName):

                    if (Instance.Addresses.Any(address => address.Equals(aaaa.IPv6Address)))
                        return false;

                    Instance.Addresses = [.. Instance.Addresses, aaaa.IPv6Address];
                    return true;

            }

            return false;

        }

        private static Boolean IsEmpty(TXT? TXT)

            => TXT is null ||
               TXT.Strings.Count == 0 ||
              (TXT.Strings.Count == 1 && TXT.Strings[0].Length == 0);

        /// <summary>
        /// TXT data is optional for resolution, but keep asking for it so that metadata omitted
        /// from an earlier response can still arrive as an update.
        /// </summary>
        private static Boolean NeedsDetails(MulticastDNSServiceInstance Instance)

            => !Instance.IsResolved ||
                Instance.TXT is null ||
                Instance.Addresses.Count == 0;

        private static void Announce(MulticastDNSServiceInstance        Instance,
                                     List<MulticastDNSServiceInstance>  Added,
                                     List<MulticastDNSServiceInstance>  Updated,
                                     Boolean                            Changed)
        {

            if (!Instance.Announced)
            {

                if (Instance.IsResolved)
                {
                    Instance.Announced = true;
                    Added.Add(Instance);
                }

                return;

            }

            if (Changed && !Updated.Contains(Instance))
                Updated.Add(Instance);

        }

        private async Task RaiseAsync(List<MulticastDNSServiceInstance>  Added,
                                      List<MulticastDNSServiceInstance>  Updated,
                                      List<MulticastDNSServiceInstance>  Removed,
                                      DateTimeOffset                     Timestamp,
                                      CancellationToken                  CancellationToken)
        {

            foreach (var instance in Added)
                await OnInstanceAdded.InvokeAllAsync(
                          handler => handler(Timestamp, this, instance, CancellationToken),
                          logger
                      ).ConfigureAwait(false);

            foreach (var instance in Updated.Where(instance => !Added.Contains(instance)))
                await OnInstanceUpdated.InvokeAllAsync(
                          handler => handler(Timestamp, this, instance, CancellationToken),
                          logger
                      ).ConfigureAwait(false);

            foreach (var instance in Removed)
                await OnInstanceRemoved.InvokeAllAsync(
                          handler => handler(Timestamp, this, instance, CancellationToken),
                          logger
                      ).ConfigureAwait(false);

        }

        private static String Key(String Name)
            => Name.TrimEnd('.').ToLowerInvariant();

        private static Boolean SameName(String Name1, String Name2)
            => Name1.TrimEnd('.').Equals(Name2.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Stop browsing.
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
        /// Return a text representation of this browser.
        /// </summary>
        public override String ToString()

            => $"mDNS browser for {ServiceType} ({Instances.Count} instance(s))";

        #endregion

    }

}
