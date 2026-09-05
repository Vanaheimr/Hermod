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

    #region Options

    /// <summary>
    /// Options of the Multicast DNS responder. The defaults are the values of RFC 6762;
    /// tests shorten the intervals.
    /// </summary>
    public sealed class MulticastDNSResponderOptions
    {

        /// <summary>
        /// Whether unique records are probed before they are announced (RFC 6762 §8.1, default: true).
        /// </summary>
        public Boolean   ProbeUniqueRecords          { get; init; } = true;

        /// <summary>
        /// The number of probes (default: 3).
        /// </summary>
        public Int32     ProbeCount                  { get; init; } = MulticastDNS.DefaultProbeCount;

        /// <summary>
        /// The interval between two probes (default: 250 ms).
        /// </summary>
        public TimeSpan  ProbeInterval               { get; init; } = MulticastDNS.DefaultProbeInterval;

        /// <summary>
        /// The maximum random delay before the first probe (default: 250 ms).
        /// </summary>
        public TimeSpan  MaxInitialProbeDelay        { get; init; } = MulticastDNS.DefaultProbeInterval;

        /// <summary>
        /// The number of announcements (default: 2).
        /// </summary>
        public Int32     AnnouncementCount           { get; init; } = MulticastDNS.DefaultAnnouncementCount;

        /// <summary>
        /// The interval between two announcements (default: 1 s).
        /// </summary>
        public TimeSpan  AnnouncementInterval        { get; init; } = MulticastDNS.DefaultAnnouncementInterval;

        /// <summary>
        /// The minimum delay of a response containing shared records (default: 20 ms).
        /// </summary>
        public TimeSpan  MinSharedResponseDelay      { get; init; } = MulticastDNS.MinSharedResponseDelay;

        /// <summary>
        /// The maximum delay of a response containing shared records (default: 120 ms).
        /// </summary>
        public TimeSpan  MaxSharedResponseDelay      { get; init; } = MulticastDNS.MaxSharedResponseDelay;

        /// <summary>
        /// The minimum delay before answering a query with a truncated known-answer list (default: 400 ms).
        /// </summary>
        public TimeSpan  MinTruncatedQueryDelay      { get; init; } = MulticastDNS.MinTruncatedQueryDelay;

        /// <summary>
        /// The maximum delay before answering a query with a truncated known-answer list (default: 500 ms).
        /// </summary>
        public TimeSpan  MaxTruncatedQueryDelay      { get; init; } = MulticastDNS.MaxTruncatedQueryDelay;

        /// <summary>
        /// The minimum interval between two multicasts of the same record (default: 1 s).
        /// </summary>
        public TimeSpan  MinRecordMulticastInterval  { get; init; } = MulticastDNS.MinRecordMulticastInterval;

        /// <summary>
        /// Whether legacy unicast queries of conventional resolvers are answered (default: true).
        /// </summary>
        public Boolean   RespondToLegacyUnicast      { get; init; } = true;

        /// <summary>
        /// Whether a query for a type this responder does not have at a name it owns is
        /// answered with an NSEC record (RFC 6762 §6.1, default: true).
        /// </summary>
        public Boolean   SendNegativeResponses       { get; init; } = true;

        /// <summary>
        /// The preferred maximum size of a response packet (default: 1472 bytes).
        /// </summary>
        public Int32     MaxResponseSize             { get; init; } = MulticastDNS.PreferredMaxPacketSize;

    }

    #endregion

    #region Publication

    /// <summary>
    /// The state of a Multicast DNS publication.
    /// </summary>
    public enum MulticastDNSPublicationState
    {

        /// <summary>
        /// The publication was created but not yet probed or announced.
        /// </summary>
        Pending,

        /// <summary>
        /// The unique names of the publication are being probed (RFC 6762 §8.1).
        /// </summary>
        Probing,

        /// <summary>
        /// The records are being announced (RFC 6762 §8.3) and already answered.
        /// </summary>
        Announcing,

        /// <summary>
        /// The records are published and answered.
        /// </summary>
        Published,

        /// <summary>
        /// Another host claimed one of the unique names (RFC 6762 §9); the records are no longer answered.
        /// </summary>
        Conflict,

        /// <summary>
        /// The publication was withdrawn (goodbye packet sent, RFC 6762 §10.1).
        /// </summary>
        Withdrawn

    }


    /// <summary>
    /// A set of resource records published by a Multicast DNS responder.
    /// </summary>
    public sealed class MulticastDNSPublication
    {

        #region Data

        private readonly MulticastDNSResponder responder;
        private          Int32                 probeStarted;

        #endregion

        #region Properties

        /// <summary>
        /// The identification of this publication.
        /// </summary>
        public Guid                               Id                     { get; }

        /// <summary>
        /// The published resource records.
        /// </summary>
        public IReadOnlyList<IDNSResourceRecord>  Records                { get; internal set; }

        /// <summary>
        /// The state of this publication.
        /// </summary>
        public MulticastDNSPublicationState       State                  { get; internal set; }

        /// <summary>
        /// Whether this publication has started sending its probe packets.
        /// </summary>
        internal Boolean                          ProbeStarted
        {
            get => Volatile.Read(ref probeStarted) != 0;
            set => Volatile.Write(ref probeStarted, value ? 1 : 0);
        }

        /// <summary>
        /// Whether the records are currently answered (announcing or published).
        /// </summary>
        public Boolean                            IsActive
            => State is MulticastDNSPublicationState.Announcing or
                        MulticastDNSPublicationState.Published;

        /// <summary>
        /// The unique records of this publication (every record that is not a PTR or NSEC record).
        /// </summary>
        public IEnumerable<IDNSResourceRecord>    UniqueRecords
            => Records.Where(record => MulticastDNS.IsUniqueRecordType(record.Type));

        /// <summary>
        /// The own record another host conflicted with, when in state Conflict.
        /// </summary>
        public IDNSResourceRecord?                OwnConflictedRecord    { get; internal set; }

        /// <summary>
        /// The record of the other host that caused the conflict, when in state Conflict.
        /// </summary>
        public IDNSResourceRecord?                ConflictingRecord      { get; internal set; }

        /// <summary>
        /// The source of the conflicting record, when in state Conflict.
        /// </summary>
        public IPSocket?                          ConflictSource         { get; internal set; }

        #endregion

        #region Constructor(s)

        internal MulticastDNSPublication(MulticastDNSResponder            Responder,
                                         IReadOnlyList<IDNSResourceRecord>  Records)
        {

            this.responder  = Responder;
            this.Id         = Guid.NewGuid();
            this.Records    = Records;
            this.State      = MulticastDNSPublicationState.Pending;

        }

        #endregion


        #region WithdrawAsync(CancellationToken = default)

        /// <summary>
        /// Withdraw this publication: send a goodbye packet and stop answering.
        /// </summary>
        /// <param name="CancellationToken">A token to cancel the withdrawal.</param>
        public Task WithdrawAsync(CancellationToken CancellationToken = default)
            => responder.WithdrawAsync(this, CancellationToken);

        #endregion

        #region UpdateAsync(Records, CancellationToken = default)

        /// <summary>
        /// Replace the records of this publication and announce the new ones (RFC 6762 §8.4).
        /// </summary>
        /// <param name="Records">The new resource records.</param>
        /// <param name="CancellationToken">A token to cancel the update.</param>
        public Task UpdateAsync(IEnumerable<IDNSResourceRecord>  Records,
                                CancellationToken                CancellationToken   = default)
            => responder.UpdateAsync(this, Records, CancellationToken);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this publication.
        /// </summary>
        public override String ToString()

            => $"{Records.Count} record(s) ({String.Join(", ", Records.Select(record => record.DomainName.FullName).Distinct())}): {State}";

        #endregion

    }

    #endregion

    #region Delegates

    /// <summary>
    /// A delegate called whenever a query was received.
    /// </summary>
    public delegate Task OnMulticastDNSQueryReceivedDelegate           (DateTimeOffset                Timestamp,
                                                                        MulticastDNSResponder         Sender,
                                                                        MulticastDNSMessage           Query,
                                                                        MulticastDNSDatagram          Datagram,
                                                                        CancellationToken             CancellationToken);

    /// <summary>
    /// A delegate called whenever a response (or announcement, or goodbye) was sent.
    /// </summary>
    public delegate Task OnMulticastDNSResponseSentDelegate            (DateTimeOffset                Timestamp,
                                                                        MulticastDNSResponder         Sender,
                                                                        MulticastDNSMessage           Response,
                                                                        IPSocket?                     Destination,
                                                                        CancellationToken             CancellationToken);

    /// <summary>
    /// A delegate called whenever the state of a publication changed.
    /// </summary>
    public delegate Task OnMulticastDNSPublicationStateChangedDelegate (DateTimeOffset                Timestamp,
                                                                        MulticastDNSResponder         Sender,
                                                                        MulticastDNSPublication       Publication,
                                                                        MulticastDNSPublicationState  OldState,
                                                                        MulticastDNSPublicationState  NewState,
                                                                        CancellationToken             CancellationToken);

    /// <summary>
    /// A delegate called whenever another host claimed a unique name of a publication.
    /// </summary>
    public delegate Task OnMulticastDNSNameConflictDelegate            (DateTimeOffset                Timestamp,
                                                                        MulticastDNSResponder         Sender,
                                                                        MulticastDNSPublication       Publication,
                                                                        IDNSResourceRecord            OwnRecord,
                                                                        IDNSResourceRecord            ConflictingRecord,
                                                                        IPSocket                      Source,
                                                                        CancellationToken             CancellationToken);

    #endregion


    /// <summary>
    /// A Multicast DNS responder (RFC 6762 §6 to §10): probes and announces published records,
    /// answers queries (multicast, unicast for QU questions, legacy unicast for conventional
    /// resolvers) with known-answer suppression, additional records (RFC 6763 §12) and the
    /// per-record rate limit, sends goodbye packets on withdrawal and detects name conflicts.
    /// A conflict is reported (state <see cref="MulticastDNSPublicationState.Conflict"/>,
    /// <see cref="OnNameConflict"/>) and the records are no longer answered; choosing a new
    /// name is left to the publisher, because a renamed host also needs new URLs and certificates.
    /// </summary>
    public sealed class MulticastDNSResponder : IAsyncDisposable
    {

        #region Data

        private readonly Lock                                  stateLock         = new();
        private readonly List<MulticastDNSPublication>         publications      = [];
        private readonly Dictionary<String, DateTimeOffset>    lastMulticast     = [];
        private readonly HashSet<Task>                         backgroundTasks   = [];
        private readonly ILogger?                              logger;
        private          CancellationTokenSource?              cancellationTokenSource;
        private          Boolean                               isDisposed;

        #endregion

        #region Properties

        /// <summary>
        /// The transport of this responder.
        /// </summary>
        public IMulticastDNSTransport                  Transport       { get; }

        /// <summary>
        /// The options of this responder.
        /// </summary>
        public MulticastDNSResponderOptions            Options         { get; }

        /// <summary>
        /// The time provider of this responder.
        /// </summary>
        public TimeProvider                            TimeProvider    { get; }

        /// <summary>
        /// Whether this responder is running.
        /// </summary>
        public Boolean                                 IsRunning
            => cancellationTokenSource is not null && !cancellationTokenSource.IsCancellationRequested;

        /// <summary>
        /// The publications of this responder that were not withdrawn.
        /// </summary>
        public IReadOnlyList<MulticastDNSPublication>  Publications
        {
            get
            {
                lock (stateLock)
                    return [.. publications];
            }
        }

        /// <summary>
        /// The records currently answered by this responder.
        /// </summary>
        public IReadOnlyList<IDNSResourceRecord>       ActiveRecords
        {
            get
            {
                lock (stateLock)
                    return [.. publications.Where(publication => publication.IsActive).
                                            SelectMany(publication => publication.Records)];
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever a query was received.
        /// </summary>
        public event OnMulticastDNSQueryReceivedDelegate?            OnQueryReceived;

        /// <summary>
        /// An event fired whenever a response, announcement or goodbye was sent.
        /// </summary>
        public event OnMulticastDNSResponseSentDelegate?             OnResponseSent;

        /// <summary>
        /// An event fired whenever the state of a publication changed.
        /// </summary>
        public event OnMulticastDNSPublicationStateChangedDelegate?  OnPublicationStateChanged;

        /// <summary>
        /// An event fired whenever another host claimed a unique name of a publication.
        /// </summary>
        public event OnMulticastDNSNameConflictDelegate?             OnNameConflict;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new Multicast DNS responder.
        /// </summary>
        /// <param name="Transport">The transport (shared with the Multicast DNS client of the process).</param>
        /// <param name="Options">Optional responder options.</param>
        /// <param name="TimeProvider">An optional time provider.</param>
        /// <param name="LoggerFactory">An optional logger factory.</param>
        public MulticastDNSResponder(IMulticastDNSTransport         Transport,
                                     MulticastDNSResponderOptions?  Options         = null,
                                     TimeProvider?                  TimeProvider    = null,
                                     ILoggerFactory?                LoggerFactory   = null)
        {

            ArgumentNullException.ThrowIfNull(Transport);

            this.Transport     = Transport;
            this.Options       = Options      ?? new MulticastDNSResponderOptions();
            this.TimeProvider  = TimeProvider ?? System.TimeProvider.System;
            this.logger        = LoggerFactory?.CreateLogger<MulticastDNSResponder>();

            if (this.Options.ProbeCount < 1)
                throw new ArgumentException("The probe count must be at least one!", nameof(Options));

            if (this.Options.AnnouncementCount < 1)
                throw new ArgumentException("The announcement count must be at least one!", nameof(Options));

        }

        #endregion


        #region StartAsync(CancellationToken = default)

        /// <summary>
        /// Start answering queries (and start the transport when it is not running yet).
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

        }

        #endregion

        #region StopAsync(CancellationToken = default)

        /// <summary>
        /// Withdraw every publication (goodbye packets) and stop answering.
        /// </summary>
        public async Task StopAsync(CancellationToken CancellationToken = default)
        {

            var cts = cancellationTokenSource;
            if (cts is null)
                return;

            foreach (var publication in Publications)
            {
                try
                {
                    await WithdrawAsync(publication, CancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger?.LogWarning(e, "Multicast DNS responder: withdrawing {Publication} failed", publication);
                }
            }

            cancellationTokenSource       = null;
            Transport.OnDatagramReceived -= OnDatagramReceivedAsync;

            await cts.CancelAsync().ConfigureAwait(false);

            Task[] pending;
            lock (stateLock)
                pending = [.. backgroundTasks];

            try
            {
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
            catch
            { }

            cts.Dispose();

        }

        #endregion


        #region PublishAsync(Records, Probe = true, CancellationToken = default)

        /// <summary>
        /// Publish the given records: probe the unique ones (RFC 6762 §8.1), announce all of them
        /// (§8.3) and answer queries for them from then on. The returned publication is in state
        /// Published, or Conflict when another host claimed one of the unique names.
        /// </summary>
        /// <param name="Records">The resource records to publish (class IN).</param>
        /// <param name="Probe">Whether to probe the unique records (default: true; the options may disable probing altogether).</param>
        /// <param name="CancellationToken">A token to cancel the publication (the records are withdrawn silently).</param>
        public async Task<MulticastDNSPublication> PublishAsync(IEnumerable<IDNSResourceRecord>  Records,
                                                                Boolean                          Probe               = true,
                                                                CancellationToken                CancellationToken   = default)
        {

            ArgumentNullException.ThrowIfNull(Records);

            var records = Records.ToArray();

            if (records.Length == 0)
                throw new ArgumentException("At least one record must be published!", nameof(Records));

            if (records.Any(record => ((UInt16) record.Class & MulticastDNS.ClassMask) != (UInt16) DNSQueryClasses.IN))
                throw new ArgumentException("Multicast DNS records must be of class IN!", nameof(Records));

            var cts = cancellationTokenSource ?? throw new InvalidOperationException("The Multicast DNS responder is not running!");

            var publication = new MulticastDNSPublication(this, records);

            lock (stateLock)
                publications.Add(publication);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, cts.Token);

            try
            {

                #region Probing (RFC 6762 §8.1)

                if (Probe && Options.ProbeUniqueRecords && publication.UniqueRecords.Any())
                {

                    await SetStateAsync(publication, MulticastDNSPublicationState.Probing, linked.Token).ConfigureAwait(false);

                    var initialDelay = RandomDelay(TimeSpan.Zero, Options.MaxInitialProbeDelay);
                    if (initialDelay > TimeSpan.Zero)
                        await Task.Delay(initialDelay, TimeProvider, linked.Token).ConfigureAwait(false);

                    var uniqueRecords  = publication.UniqueRecords.ToArray();
                    var uniqueNames    = uniqueRecords.Select(record => record.DomainName).
                                                       DistinctBy(name => name.FullName.ToLowerInvariant()).
                                                       ToArray();

                    for (var i = 0; i < Options.ProbeCount; i++)
                    {

                        if (publication.State != MulticastDNSPublicationState.Probing)
                            return publication;

                        var probe = MulticastDNSMessage.Query(
                                        uniqueNames.Select(name => new MulticastDNSQuestion(name, DNSResourceRecordTypes.Any, UnicastResponseRequested: true)),
                                        Authorities: uniqueRecords.Select(record => new MulticastDNSRecord(record))
                                    );

                        if (i == 0)
                            publication.ProbeStarted = true;

                        await SendAsync(probe, null, null, linked.Token).ConfigureAwait(false);

                        await Task.Delay(Options.ProbeInterval, TimeProvider, linked.Token).ConfigureAwait(false);

                    }

                    if (publication.State != MulticastDNSPublicationState.Probing)
                        return publication;

                }

                #endregion

                #region Announcing (RFC 6762 §8.3)

                await SetStateAsync(publication, MulticastDNSPublicationState.Announcing, linked.Token).ConfigureAwait(false);

                for (var i = 0; i < Options.AnnouncementCount; i++)
                {

                    if (publication.State != MulticastDNSPublicationState.Announcing)
                        return publication;

                    if (i > 0)
                        await Task.Delay(Options.AnnouncementInterval, TimeProvider, linked.Token).ConfigureAwait(false);

                    if (publication.State != MulticastDNSPublicationState.Announcing)
                        return publication;

                    await AnnounceAsync(publication.Records, linked.Token).ConfigureAwait(false);

                }

                if (publication.State == MulticastDNSPublicationState.Announcing)
                    await SetStateAsync(publication, MulticastDNSPublicationState.Published, linked.Token).ConfigureAwait(false);

                #endregion

                return publication;

            }
            catch (OperationCanceledException)
            {

                lock (stateLock)
                {
                    publication.State = MulticastDNSPublicationState.Withdrawn;
                    publications.Remove(publication);
                }

                throw;

            }

        }

        #endregion

        #region WithdrawAsync(Publication, CancellationToken = default)

        /// <summary>
        /// Withdraw the given publication: send a goodbye packet for announced records (RFC 6762 §10.1)
        /// and stop answering them.
        /// </summary>
        /// <param name="Publication">The publication.</param>
        /// <param name="CancellationToken">A token to cancel the withdrawal.</param>
        public async Task WithdrawAsync(MulticastDNSPublication  Publication,
                                        CancellationToken        CancellationToken   = default)
        {

            ArgumentNullException.ThrowIfNull(Publication);

            MulticastDNSPublicationState oldState;

            lock (stateLock)
            {

                if (Publication.State == MulticastDNSPublicationState.Withdrawn)
                    return;

                oldState = Publication.State;
                Publication.State = MulticastDNSPublicationState.Withdrawn;
                publications.Remove(Publication);

            }

            await RaiseStateChangedAsync(Publication, oldState, MulticastDNSPublicationState.Withdrawn, CancellationToken).ConfigureAwait(false);

            if (oldState is MulticastDNSPublicationState.Announcing or
                            MulticastDNSPublicationState.Published &&
                IsRunning)
            {
                await SendGoodbyeAsync(Publication.Records, CancellationToken).ConfigureAwait(false);
            }

        }

        #endregion

        #region UpdateAsync(Publication, Records, CancellationToken = default)

        /// <summary>
        /// Replace the records of the given publication and announce the new ones with the
        /// cache-flush bit (RFC 6762 §8.4); records that disappeared get a goodbye packet.
        /// The owner names should stay the same, as the new records are not probed.
        /// </summary>
        /// <param name="Publication">The publication.</param>
        /// <param name="Records">The new resource records.</param>
        /// <param name="CancellationToken">A token to cancel the update.</param>
        public async Task UpdateAsync(MulticastDNSPublication          Publication,
                                      IEnumerable<IDNSResourceRecord>  Records,
                                      CancellationToken                CancellationToken   = default)
        {

            ArgumentNullException.ThrowIfNull(Publication);
            ArgumentNullException.ThrowIfNull(Records);

            var records = Records.ToArray();

            if (records.Length == 0)
                throw new ArgumentException("At least one record must be published!", nameof(Records));

            IDNSResourceRecord[] removed;

            lock (stateLock)
            {

                if (!Publication.IsActive)
                    throw new InvalidOperationException($"The publication is not active ({Publication.State})!");

                removed = [.. Publication.Records.Where(old => !records.Any(record => record.IsIdenticalTo(old)))];

                Publication.Records = records;

            }

            var cts = cancellationTokenSource;
            if (cts is null)
                return;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, cts.Token);

            // Unique records in an RRSet that still exists are replaced by the cache-flush
            // announcement. Shared records never carry that bit, so every removed shared
            // RDATA needs an explicit goodbye even when other members of its RRSet remain.
            var goodbyeRecords = removed.Where(old => !MulticastDNS.IsUniqueRecordType(old.Type) ||
                                                       !records.Any(record => record.IsSameRRSet(old))).
                                         ToArray();

            if (goodbyeRecords.Length > 0)
                await SendGoodbyeAsync(goodbyeRecords, linked.Token).ConfigureAwait(false);

            for (var i = 0; i < Options.AnnouncementCount; i++)
            {

                if (i > 0)
                    await Task.Delay(Options.AnnouncementInterval, TimeProvider, linked.Token).ConfigureAwait(false);

                if (!Publication.IsActive)
                    return;

                await AnnounceAsync(Publication.Records, linked.Token).ConfigureAwait(false);

            }

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

            if (!MulticastDNSMessage.TryParse(Datagram.Payload, out var message, out var error))
            {
                logger?.LogDebug("Multicast DNS responder: ignoring an unparsable packet from {Source}: {Error}", Datagram.RemoteSocket, error);
                return;
            }

            // RFC 6762 §11, §18.3 and §18.11: only on-link standard messages
            // with a zero response code participate in Multicast DNS.
            if (!Datagram.SourceIsOnLocalLink ||
                 message.Opcode       != 0 ||
                 message.ResponseCode != DNSResponseCodes.NoError)
            {
                return;
            }

            if (message.IsQuery)
                await HandleQueryAsync(message, Datagram, CancellationToken).ConfigureAwait(false);
            else
                await HandleResponseAsync(message, Datagram, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region (private) HandleQueryAsync(Query, Datagram, CancellationToken)

        private async Task HandleQueryAsync(MulticastDNSMessage   Query,
                                            MulticastDNSDatagram  Datagram,
                                            CancellationToken     CancellationToken)
        {

            await OnQueryReceived.InvokeAllAsync(
                      handler => handler(Datagram.Timestamp, this, Query, Datagram, CancellationToken),
                      logger
                  ).ConfigureAwait(false);

            var legacy = Transport.IsLegacyUnicast(Datagram.RemoteSocket);

            if (legacy && !Options.RespondToLegacyUnicast)
                return;

            #region Simultaneous probe tie-breaking (RFC 6762 §8.2)

            if (Query.Authorities.Count > 0)
                await HandleProbeAsync(Query, Datagram, CancellationToken).ConfigureAwait(false);

            #endregion

            #region Collect the answers

            var activeRecords  = ActiveRecords;
            var answers        = new List<IDNSResourceRecord>();
            var answerKeys     = new HashSet<String>();
            var unicastOnly    = true;
            var shared         = false;

            foreach (var question in Query.Questions)
            {

                if (question.Class != DNSQueryClasses.IN && question.Class != DNSQueryClasses.ANY)
                    continue;

                foreach (var record in activeRecords.Where(question.Matches))
                {

                    // RFC 6762 §7.1: known-answer suppression.
                    if (Query.Answers.Any(known => known.Record.IsIdenticalTo(record) &&
                                                   known.TimeToLive >= record.TimeToLive / 2))
                        continue;

                    if (!answerKeys.Add(record.RecordKey()))
                        continue;

                    answers.Add(record);

                    if (!question.UnicastResponseRequested)
                        unicastOnly = false;

                    if (!MulticastDNS.IsUniqueRecordType(record.Type))
                        shared = true;

                }

            }

            #endregion

            #region Negative responses (RFC 6762 §6.1)

            // A query for a type we do not have at a name we own is answered with an NSEC
            // record listing the types we do have, so that a querier waiting for an AAAA
            // record of an IPv4-only host learns immediately that there is none.
            if (Options.SendNegativeResponses)
            {

                foreach (var question in Query.Questions)
                {

                    if (question.Type == DNSResourceRecordTypes.Any ||
                        question.Type == DNSResourceRecordTypes.NSEC ||
                        (question.Class != DNSQueryClasses.IN && question.Class != DNSQueryClasses.ANY))
                        continue;

                    var atName = activeRecords.Where(record => record.DomainName.Equals(question.Name)).ToArray();

                    if (atName.Length == 0 || atName.Any(record => record.Type == question.Type))
                        continue;

                    if (!DomainName.TryParse(question.Name.FullName, out var ownerName, out _))
                        continue;

                    var nsec = new NSEC(
                                   ownerName,
                                   DNSQueryClasses.IN,
                                   atName.Min(record => record.TimeToLive),
                                   ownerName,
                                   MulticastDNSTypeBitmap.Encode(atName.Select(record => record.Type))
                               );

                    if (answerKeys.Add(nsec.RecordKey()))
                        answers.Add(nsec);

                    if (!question.UnicastResponseRequested)
                        unicastOnly = false;

                }

            }

            #endregion

            if (answers.Count == 0)
                return;

            var additionals  = AdditionalRecords(answers, activeRecords, answerKeys);
            var unicast      = legacy || unicastOnly;

            var delay        = legacy          ? TimeSpan.Zero :
                               Query.Truncated ? RandomDelay(Options.MinTruncatedQueryDelay, Options.MaxTruncatedQueryDelay) :
                               shared          ? RandomDelay(Options.MinSharedResponseDelay, Options.MaxSharedResponseDelay) :
                                                 TimeSpan.Zero;

            if (delay > TimeSpan.Zero)
            {

                TrackBackgroundTask(async token => {

                    await Task.Delay(delay, TimeProvider, token).ConfigureAwait(false);

                    await SendResponseAsync(answers, additionals, unicast, legacy, Query, Datagram, token).ConfigureAwait(false);

                });

                return;

            }

            await SendResponseAsync(answers, additionals, unicast, legacy, Query, Datagram, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region (private) HandleProbeAsync(Probe, Datagram, CancellationToken)

        private async Task HandleProbeAsync(MulticastDNSMessage   Probe,
                                            MulticastDNSDatagram  Datagram,
                                            CancellationToken     CancellationToken)
        {

            MulticastDNSPublication[] probing;

            lock (stateLock)
                probing = [.. publications.Where(publication => publication.State == MulticastDNSPublicationState.Probing)];

            foreach (var publication in probing)
            {

                foreach (var nameGroup in publication.UniqueRecords.GroupBy(record => record.DomainName.FullName.ToLowerInvariant()))
                {

                    var theirs = Probe.Authorities.
                                     Where (authority => authority.Name.FullName.Equals(nameGroup.Key, StringComparison.OrdinalIgnoreCase)).
                                     Select(authority => authority.Record).
                                     ToArray();

                    if (theirs.Length == 0)
                        continue;

                    var ours = nameGroup.ToArray();

                    // Identical proposals (our own probe looped back, or a second instance
                    // of the same host) are no conflict.
                    if (theirs.All(their => ours.Any(our => our.IsIdenticalTo(their))))
                        continue;

                    // RFC 6762 §8.2.1: the lexicographically later record set wins.
                    if (ours.CompareLexicographically(theirs) < 0)
                    {

                        var conflicting = theirs.First(their => !ours.Any(our => our.IsIdenticalTo(their)));
                        var own         = ours.FirstOrDefault(our => our.IsSameRRSet(conflicting)) ?? ours[0];

                        await SetConflictAsync(publication, own, conflicting, Datagram.RemoteSocket, CancellationToken).ConfigureAwait(false);
                        break;

                    }

                }

            }

        }

        #endregion

        #region (private) HandleResponseAsync(Response, Datagram, CancellationToken)

        private async Task HandleResponseAsync(MulticastDNSMessage   Response,
                                               MulticastDNSDatagram  Datagram,
                                               CancellationToken     CancellationToken)
        {

            // A goodbye record (TTL 0) only removes data and claims nothing.
            var receivedRecords = Response.AllRecords.
                                           Where (record => !record.IsGoodbye).
                                           Select(record => record.Record).
                                           ToArray();

            if (receivedRecords.Length == 0)
                return;

            // RFC 6762 §9 uses RRsets for conflicts after probing has completed.
            var receivedByRRSet = receivedRecords.
                                      GroupBy(record => record.RRSetKey()).
                                      ToDictionary(group => group.Key, group => group.ToArray());

            MulticastDNSPublication[] candidates;

            lock (stateLock)
                candidates = [.. publications.Where(publication => publication.IsActive ||
                                                                   publication.State == MulticastDNSPublicationState.Probing)];

            foreach (var publication in candidates)
            {

                // RFC 6762 §8.1: an ANY probe claims the complete owner name.  While
                // probing, any live record with that name is therefore a conflict,
                // regardless of its record type or data.
                if (publication.State        == MulticastDNSPublicationState.Probing &&
                    publication.ProbeStarted)
                {

                    foreach (var nameGroup in publication.UniqueRecords.GroupBy(record => record.DomainName.FullName.ToLowerInvariant()))
                    {

                        var conflicting = receivedRecords.FirstOrDefault(record => record.DomainName.FullName.Equals(nameGroup.Key, StringComparison.OrdinalIgnoreCase));

                        if (conflicting is null)
                            continue;

                        var ours = nameGroup.ToArray();
                        var own  = ours.FirstOrDefault(record => record.IsSameRRSet(conflicting)) ?? ours[0];

                        await SetConflictAsync(publication, own, conflicting, Datagram.RemoteSocket, CancellationToken).ConfigureAwait(false);
                        break;

                    }

                    continue;

                }

                foreach (var rrset in publication.UniqueRecords.GroupBy(record => record.RRSetKey()))
                {

                    if (!receivedByRRSet.TryGetValue(rrset.Key, out var theirs))
                        continue;

                    var ours         = rrset.ToArray();
                    var conflicting  = theirs.FirstOrDefault(their => !ours.Any(our => our.IsIdenticalTo(their)));

                    if (conflicting is null)
                        continue;

                    await SetConflictAsync(publication, ours[0], conflicting, Datagram.RemoteSocket, CancellationToken).ConfigureAwait(false);
                    break;

                }

            }

        }

        #endregion

        #region (private) SendResponseAsync(Answers, Additionals, Unicast, Legacy, Query, Datagram, CancellationToken)

        private async Task SendResponseAsync(List<IDNSResourceRecord>  Answers,
                                             List<IDNSResourceRecord>  Additionals,
                                             Boolean                   Unicast,
                                             Boolean                   Legacy,
                                             MulticastDNSMessage       Query,
                                             MulticastDNSDatagram      Datagram,
                                             CancellationToken         CancellationToken)
        {

            var answers      = Answers;
            var additionals  = Additionals;

            #region Rate limit (RFC 6762 §6)

            if (!Unicast)
            {

                var now = TimeProvider.GetUtcNow();

                lock (stateLock)
                {

                    answers = [.. answers.Where(record => !lastMulticast.TryGetValue(record.RecordKey(), out var last) ||
                                                          now - last >= Options.MinRecordMulticastInterval)];

                    if (answers.Count == 0)
                        return;

                    additionals = [.. additionals.Where(record => !lastMulticast.TryGetValue(record.RecordKey(), out var last) ||
                                                                  now - last >= Options.MinRecordMulticastInterval)];

                    foreach (var record in answers.Concat(additionals))
                        lastMulticast[record.RecordKey()] = now;

                }

            }

            #endregion

            var ttlOverride  = Legacy ? MulticastDNS.MaxLegacyUnicastTimeToLive : (TimeSpan?) null;

            // RFC 6762 §6.1: the NSEC record of a negative answer is unique to this responder as well.
            MulticastDNSRecord Wrap(IDNSResourceRecord Record)
                => new (Record,
                        CacheFlush:          !Legacy && (MulticastDNS.IsUniqueRecordType(Record.Type) || Record.Type == DNSResourceRecordTypes.NSEC),
                        TimeToLiveOverride:  ttlOverride.HasValue && Record.TimeToLive > ttlOverride.Value
                                                 ? ttlOverride
                                                 : null);

            foreach (var (packetAnswers, packetAdditionals) in Split(answers, additionals, Legacy ? Query.Questions.Count * 64 : 0))
            {

                var response = MulticastDNSMessage.Response(
                                   packetAnswers.    Select(Wrap),
                                   packetAdditionals.Select(Wrap),
                                   Questions:      Legacy ? Query.Questions : null,
                                   TransactionId:  Legacy ? Query.TransactionId : (UInt16) 0
                               );

                await SendAsync(
                          response,
                          Unicast ? Datagram.RemoteSocket : null,
                          Unicast ? null : Datagram.InterfaceIndex,
                          CancellationToken
                      ).ConfigureAwait(false);

            }

        }

        #endregion

        #region (private) AnnounceAsync(Records, CancellationToken)

        private async Task AnnounceAsync(IReadOnlyList<IDNSResourceRecord>  Records,
                                         CancellationToken                  CancellationToken)
        {

            var now = TimeProvider.GetUtcNow();

            lock (stateLock)
            {
                foreach (var record in Records)
                    lastMulticast[record.RecordKey()] = now;
            }

            foreach (var (packetAnswers, _) in Split([.. Records], [], 0))
            {

                var announcement = MulticastDNSMessage.Response(
                                       packetAnswers.Select(record => new MulticastDNSRecord(record, MulticastDNS.IsUniqueRecordType(record.Type)))
                                   );

                await SendAsync(announcement, null, null, CancellationToken).ConfigureAwait(false);

            }

        }

        #endregion

        #region (private) SendGoodbyeAsync(Records, CancellationToken)

        private async Task SendGoodbyeAsync(IReadOnlyList<IDNSResourceRecord>  Records,
                                            CancellationToken                  CancellationToken)
        {

            foreach (var (packetAnswers, _) in Split([.. Records], [], 0))
            {

                var goodbye = MulticastDNSMessage.Response(
                                  packetAnswers.Select(record => new MulticastDNSRecord(
                                                                      record,
                                                                      MulticastDNS.IsUniqueRecordType(record.Type),
                                                                      TimeSpan.Zero
                                                                  ))
                              );

                await SendAsync(goodbye, null, null, CancellationToken).ConfigureAwait(false);

            }

        }

        #endregion

        #region (private) SendAsync(Message, Destination, InterfaceIndex, CancellationToken)

        private async Task SendAsync(MulticastDNSMessage  Message,
                                     IPSocket?            Destination,
                                     Int32?               InterfaceIndex,
                                     CancellationToken    CancellationToken)
        {

            if (!Transport.IsRunning)
                return;

            await Transport.SendAsync(Message.Serialize(), Destination, InterfaceIndex, CancellationToken).ConfigureAwait(false);

            if (Message.IsResponse)
                await OnResponseSent.InvokeAllAsync(
                          handler => handler(TimeProvider.GetUtcNow(), this, Message, Destination, CancellationToken),
                          logger
                      ).ConfigureAwait(false);

        }

        #endregion


        #region (private) AdditionalRecords(Answers, ActiveRecords, AnswerKeys)

        /// <summary>
        /// The additional records of a response (RFC 6763 §12, RFC 6762 §6.2): for PTR answers the
        /// SRV and TXT records of the instance, for SRV answers the address records of the target,
        /// and for address records the other address family of the same name.
        /// </summary>
        private static List<IDNSResourceRecord> AdditionalRecords(List<IDNSResourceRecord>           Answers,
                                                                  IReadOnlyList<IDNSResourceRecord>  ActiveRecords,
                                                                  HashSet<String>                    AnswerKeys)
        {

            var additionals  = new List<IDNSResourceRecord>();
            var keys         = new HashSet<String>(AnswerKeys);

            void Add(IEnumerable<IDNSResourceRecord> Records)
            {
                foreach (var record in Records)
                    if (keys.Add(record.RecordKey()))
                        additionals.Add(record);
            }

            IEnumerable<IDNSResourceRecord> AtName(String Name, params DNSResourceRecordTypes[] Types)
                => ActiveRecords.Where(record => Types.Contains(record.Type) &&
                                                 SameName(record.DomainName.FullName, Name));

            foreach (var answer in Answers)
            {

                switch (answer)
                {

                    case PTR ptr:
                        foreach (var service in AtName(ptr.Target.FullName, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT))
                        {
                            Add([ service ]);
                            if (service is SRV srv)
                                Add(AtName(srv.Target.FullName, DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA));
                        }
                        break;

                    case SRV srv:
                        Add(AtName(srv.Target.FullName, DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA));
                        break;

                    case A a:
                        Add(AtName(a.DomainName.FullName, DNSResourceRecordTypes.AAAA));
                        break;

                    case AAAA aaaa:
                        Add(AtName(aaaa.DomainName.FullName, DNSResourceRecordTypes.A));
                        break;

                }

            }

            return additionals;

        }

        #endregion

        #region (private) Split(Answers, Additionals, Overhead)

        /// <summary>
        /// Split the records into packets of at most MaxResponseSize bytes (RFC 6762 §17).
        /// </summary>
        private IEnumerable<(List<IDNSResourceRecord> Answers, List<IDNSResourceRecord> Additionals)> Split(List<IDNSResourceRecord>  Answers,
                                                                                                             List<IDNSResourceRecord>  Additionals,
                                                                                                             Int32                     Overhead)
        {

            var maxSize             = Options.MaxResponseSize;
            var size                = 12 + Overhead;
            var packetAnswers       = new List<IDNSResourceRecord>();
            var packetAdditionals   = new List<IDNSResourceRecord>();

            foreach (var (record, isAnswer) in Answers.    Select(record => (record, true)).
                                               Concat(Additionals.Select(record => (record, false))))
            {

                var recordSize = record.ToWireFormat().Length;

                if (size + recordSize > maxSize && (packetAnswers.Count > 0 || packetAdditionals.Count > 0))
                {

                    // Additional records are optional (RFC 6762 §7.2 lets a receiver re-query them).
                    if (!isAnswer)
                        break;

                    yield return (packetAnswers, packetAdditionals);

                    packetAnswers      = [];
                    packetAdditionals  = [];
                    size               = 12 + Overhead;

                }

                if (isAnswer)
                    packetAnswers.Add(record);
                else
                    packetAdditionals.Add(record);

                size += recordSize;

            }

            if (packetAnswers.Count > 0 || packetAdditionals.Count > 0)
                yield return (packetAnswers, packetAdditionals);

        }

        #endregion

        #region (private) SetStateAsync / SetConflictAsync / RaiseStateChangedAsync

        private async Task SetStateAsync(MulticastDNSPublication       Publication,
                                         MulticastDNSPublicationState  NewState,
                                         CancellationToken             CancellationToken)
        {

            MulticastDNSPublicationState oldState;

            lock (stateLock)
            {

                if (Publication.State == NewState)
                    return;

                oldState           = Publication.State;
                Publication.State  = NewState;

            }

            await RaiseStateChangedAsync(Publication, oldState, NewState, CancellationToken).ConfigureAwait(false);

        }

        private async Task SetConflictAsync(MulticastDNSPublication  Publication,
                                            IDNSResourceRecord       OwnRecord,
                                            IDNSResourceRecord       ConflictingRecord,
                                            IPSocket                 Source,
                                            CancellationToken        CancellationToken)
        {

            MulticastDNSPublicationState oldState;

            lock (stateLock)
            {

                if (Publication.State is MulticastDNSPublicationState.Conflict or
                                         MulticastDNSPublicationState.Withdrawn)
                    return;

                oldState                         = Publication.State;
                Publication.State                = MulticastDNSPublicationState.Conflict;
                Publication.OwnConflictedRecord  = OwnRecord;
                Publication.ConflictingRecord    = ConflictingRecord;
                Publication.ConflictSource       = Source;

            }

            logger?.LogWarning("Multicast DNS responder: name conflict for '{Name}' with {Source}: {Conflicting}",
                               OwnRecord.DomainName, Source, ConflictingRecord);

            await RaiseStateChangedAsync(Publication, oldState, MulticastDNSPublicationState.Conflict, CancellationToken).ConfigureAwait(false);

            await OnNameConflict.InvokeAllAsync(
                      handler => handler(TimeProvider.GetUtcNow(), this, Publication, OwnRecord, ConflictingRecord, Source, CancellationToken),
                      logger
                  ).ConfigureAwait(false);

        }

        private Task RaiseStateChangedAsync(MulticastDNSPublication       Publication,
                                            MulticastDNSPublicationState  OldState,
                                            MulticastDNSPublicationState  NewState,
                                            CancellationToken             CancellationToken)

            => OnPublicationStateChanged.InvokeAllAsync(
                   handler => handler(TimeProvider.GetUtcNow(), this, Publication, OldState, NewState, CancellationToken),
                   logger
               );

        #endregion

        #region (private) TrackBackgroundTask(Work)

        private void TrackBackgroundTask(Func<CancellationToken, Task> Work)
        {

            var cts = cancellationTokenSource;
            if (cts is null)
                return;

            var token = cts.Token;

            Task? task = null;

            task = Task.Run(async () => {

                try
                {
                    await Work(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                { }
                catch (Exception e)
                {
                    logger?.LogWarning(e, "Multicast DNS responder: a delayed response failed");
                }
                finally
                {
                    lock (stateLock)
                        if (task is not null)
                            backgroundTasks.Remove(task);
                }

            }, CancellationToken.None);

            lock (stateLock)
                if (!task.IsCompleted)
                    backgroundTasks.Add(task);

        }

        #endregion

        #region (private static) RandomDelay(Min, Max) / SameName(Name1, Name2)

        private static TimeSpan RandomDelay(TimeSpan Min, TimeSpan Max)
        {

            if (Max <= Min)
                return Min;

            return Min + TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * (Max - Min).TotalMilliseconds);

        }

        private static Boolean SameName(String Name1, String Name2)

            => Name1.TrimEnd('.').Equals(Name2.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Stop the responder (withdrawing every publication).
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
        /// Return a text representation of this responder.
        /// </summary>
        public override String ToString()

            => $"mDNS responder ({Publications.Count} publication(s)) on {Transport}";

        #endregion

    }

}
