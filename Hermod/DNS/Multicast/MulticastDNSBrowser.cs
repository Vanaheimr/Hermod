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
    /// the instance name (from the PTR record), host and port (SRV), the TXT record and the
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
        /// Whether host, port and TXT record are known.
        /// </summary>
        public Boolean                    IsResolved
            => HostName is not null && Port.HasValue && TXT is not null;

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
    /// (SRV, TXT, A/AAAA), refreshes records towards the end of their lifetime and reports
    /// appeared, updated and disappeared instances. An instance is reported as appeared once
    /// host, port and TXT record are known.
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
        public Task StartAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (IsRunning)
                return Task.CompletedTask;

            cancellationTokenSource   = new CancellationTokenSource();
            var token                 = cancellationTokenSource.Token;

            Client.OnRecordReceived  += OnRecordReceivedAsync;
            Client.OnRecordExpired   += OnRecordExpiredAsync;

            #region Seed from the cache

            var now = Client.TimeProvider.GetUtcNow();

            lock (stateLock)
            {

                foreach (var entry in Client.CachedRecords(ServiceType, DNSResourceRecordTypes.PTR))
                {

                    if (entry.Record is not PTR ptr)
                        continue;

                    var instance = GetOrAddInstance(ptr.Target, now);

                    foreach (var detail in Client.CachedRecords(ptr.Target))
                        Apply(instance, detail.Record);

                    if (instance.HostName is not null)
                        foreach (var address in Client.CachedRecords().Where(address => SameName(address.Record.DomainName.FullName, instance.HostName.FullName)))
                            Apply(instance, address.Record);

                    if (instance.IsResolved)
                        instance.Announced = true;

                }

            }

            #endregion

            queryLoop         = Task.Run(() => QueryLoopAsync(token), CancellationToken.None);

            maintenanceTimer  = Client.TimeProvider.CreateTimer(
                                    _ => Maintenance(),
                                    null,
                                    Client.Options.BrowseMaintenanceInterval,
                                    Client.Options.BrowseMaintenanceInterval
                                );

            return Task.CompletedTask;

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

            var knownAnswers = Client.CachedRecords(ServiceType, DNSResourceRecordTypes.PTR).
                                   Where (entry => entry.ExpiresAt > now + TimeSpan.FromSeconds(1)).
                                   Select(entry => new MulticastDNSRecord(entry.Record, false, entry.RemainingTimeToLive(now)));

            return Client.SendQueryAsync(
                       [ new MulticastDNSQuestion(ServiceType, DNSResourceRecordTypes.PTR) ],
                       knownAnswers,
                       CancellationToken
                   );

        }

        #endregion


        #region (private) QueryLoopAsync(CancellationToken)

        private async Task QueryLoopAsync(CancellationToken CancellationToken)
        {

            var interval = Client.Options.InitialBrowseInterval;

            while (!CancellationToken.IsCancellationRequested)
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
                        {

                            foreach (var detail in Client.CachedRecords(ptr.Target))
                                Apply(instance, detail.Record);

                            if (instance.HostName is not null)
                                foreach (var address in Client.CachedRecords().Where(address => SameName(address.Record.DomainName.FullName, instance.HostName.FullName)))
                                    Apply(instance, address.Record);

                        }

                        if (!instance.IsResolved || instance.Addresses.Count == 0)
                            toQuery.Add(instance);

                        Announce(instance, added, updated, false);

                        break;


                    case SRV or TXT:

                        if (instances.TryGetValue(Key(Record.Record.DomainName.FullName), out var detailInstance))
                        {

                            var changed = Apply(detailInstance, Record.Record);

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
                            toQuery.Add(instance);

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
