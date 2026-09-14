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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A small, deterministic DNS zone store useful for tests and simple authoritative deployments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The store becomes a *zone* the moment it holds an SOA: that record's owner
    /// name is the apex, and from there the lookup follows RFC 1034 §4.3.2 —
    /// delegations end the search, a name with descendants but no records of its
    /// own is NODATA rather than NXDOMAIN, wildcards synthesize, and negative
    /// answers cite the SOA so a resolver can cache them (RFC 2308).
    /// </para>
    /// <para>
    /// If the zone data also carries NSEC or NSEC3 and RRSIG records — the output
    /// of <c>dnssec-signzone</c> or an equivalent — then a querier that sets the
    /// DO bit gets the signatures with its answer and the denial records with its
    /// "no". Such a zone is signed offline and this serves what the signer
    /// produced, byte for byte.
    /// </para>
    /// <para>
    /// <see cref="Sign"/> is the other way round: hand it keys and it signs what
    /// is already here, in process. The records it produces are the same records
    /// an offline signer would have written, so everything downstream — the
    /// denial index, the DO-bit selection, the referral logic — is unchanged and
    /// cannot tell the two apart. What changes is only who wrote them.
    /// </para>
    /// <para>
    /// Records outside the apex are still answered by exact name, which is why
    /// this can hold, say, a forward zone and a matching in-addr.arpa name at the
    /// same time. They get no SOA and no denial records, because there is no zone
    /// to cite for them.
    /// </para>
    /// </remarks>
    public sealed class InMemoryDNSZone : IDNSZoneStore
    {

        #region Data

        private readonly ConcurrentDictionary<DNSServiceName, List<IDNSResourceRecord>>  records   = [];

        private readonly Lock                                                            indexLock = new();

        private ZoneIndex?                                                               index;

        /// <summary>
        /// Bumped by every change to the records. <see cref="Sign"/> remembers the
        /// value it signed at, which is the whole of how staleness is detected.
        /// </summary>
        private Int64                                                                    revision;

        private Int64                                                                    signedRevision = -1;

        /// <summary>
        /// What the last <see cref="Sign"/> was given, so that it can be repeated
        /// without the caller having to hold on to any of it.
        /// </summary>
        /// <remarks>
        /// The keys are kept by reference. That is not a new exposure — the
        /// caller already holds them and <see cref="DNSSECSigningKey"/> already
        /// holds the private half — but it is worth saying out loud that a signed
        /// zone now keeps its signing keys for as long as it lives.
        /// </remarks>
        private DNSSECSigningKey[]?                                                      signingKeys;

        private NSEC3Parameters?                                                         signingNSEC3;

        private TimeSpan?                                                                signingValidity;

        #endregion

        #region (class) ZoneIndex

        /// <summary>
        /// Everything about the zone that is derived from its records rather than
        /// stored: the apex, which names exist, and how it denies.
        /// </summary>
        private sealed class ZoneIndex
        {

            public required DomainName?             Origin        { get; init; }
            public required SOA?                    StartOfAuthority { get; init; }
            public required HashSet<String>         Names         { get; init; }
            public required ZoneDenialOfExistence?  Denial        { get; init; }

        }

        #endregion

        #region Properties

        /// <summary>
        /// The zone apex — the owner name of the SOA record — or null while the
        /// store holds no SOA and is therefore just a set of records.
        /// </summary>
        public DomainName? Origin
            => Index().Origin;

        /// <summary>
        /// Whether the zone carries the NSEC/NSEC3 records needed to deny authenticated.
        /// </summary>
        public Boolean IsSigned
            => Index().Denial?.IsSigned == true;

        /// <summary>
        /// When <see cref="Sign"/> last ran, or null if it never has. A zone whose
        /// records came from an offline signer reports null here and true from
        /// <see cref="IsSigned"/>, which is the correct pair: it is signed, and
        /// not by this.
        /// </summary>
        public DateTime? SignedAt              { get; private set; }

        /// <summary>
        /// When the signatures <see cref="Sign"/> produced stop being valid.
        /// </summary>
        /// <remarks>
        /// Worth having in reach rather than buried in the RRSIGs, because this is
        /// the failure that arrives without anyone doing anything: a server signs
        /// once at startup and serves the result for longer than the signatures
        /// last, and every validating resolver in the world starts calling the
        /// zone Bogus on the same day.
        /// </remarks>
        public DateTime? SignaturesExpireAt    { get; private set; }

        /// <summary>
        /// Whether the records have changed since <see cref="Sign"/> last ran.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the dangerous state, and it is dangerous in a way that looks
        /// fine: adding a record to a signed zone leaves that RRset without an
        /// RRSIG and its owner name outside the chain of denial. A validating
        /// resolver asking for it does not get "unsigned" — it gets an answer the
        /// zone's own NSEC records say should not exist, which is the signature of
        /// an attack rather than of a mistake.
        /// </para>
        /// <para>
        /// Nothing here refuses to serve such a zone: an authoritative server that
        /// silently stops answering is its own kind of outage, and the decision is
        /// the operator's. What it does is make the state visible instead of
        /// leaving it to be inferred from a resolver's complaint.
        /// </para>
        /// </remarks>
        public Boolean SignaturesAreStale
            => SignedAt.HasValue &&
               Interlocked.Read(ref revision) != Interlocked.Read(ref signedRevision);

        #endregion


        #region Add / Set / Remove / AddZoneFileString

        public InMemoryDNSZone Add(params IDNSResourceRecord[] ResourceRecords)
            => Add((IEnumerable<IDNSResourceRecord>) ResourceRecords);


        public InMemoryDNSZone Add(IEnumerable<IDNSResourceRecord> ResourceRecords)
        {

            foreach (var resourceRecord in ResourceRecords)
            {

                records.AddOrUpdate(
                    resourceRecord.DomainName,
                    _ => [ resourceRecord ],
                    (_, existingRecords) => {

                        lock (existingRecords)
                        {
                            existingRecords.Add(resourceRecord);
                            return existingRecords;
                        }

                    }
                );

            }

            Invalidate();

            return this;

        }


        public InMemoryDNSZone Set(params IDNSResourceRecord[] ResourceRecords)
            => Set((IEnumerable<IDNSResourceRecord>) ResourceRecords);


        public InMemoryDNSZone Set(IEnumerable<IDNSResourceRecord> ResourceRecords)
        {

            foreach (var resourceRecordGroup in ResourceRecords.GroupBy(resourceRecord => resourceRecord.DomainName))
            {

                var replacementRecords = resourceRecordGroup.ToArray();
                var replacementKeys    = replacementRecords.
                                             Select(resourceRecord => (resourceRecord.Type, resourceRecord.Class)).
                                             ToHashSet();

                records.AddOrUpdate(
                    resourceRecordGroup.Key,
                    _ => [.. replacementRecords],
                    (_, existingRecords) => {

                        lock (existingRecords)
                        {
                            existingRecords.RemoveAll(resourceRecord => replacementKeys.Contains((resourceRecord.Type, resourceRecord.Class)));
                            existingRecords.AddRange(replacementRecords);
                            return existingRecords;
                        }

                    }
                );

            }

            Invalidate();

            return this;

        }


        public InMemoryDNSZone Remove(DNSServiceName          DomainName,
                                      DNSResourceRecordTypes? ResourceRecordType = null,
                                      DNSQueryClasses?        QueryClass         = null)
        {

            if (!records.TryGetValue(DomainName, out var existingRecords))
                return this;

            lock (existingRecords)
            {

                existingRecords.RemoveAll(resourceRecord =>
                    (!ResourceRecordType.HasValue || resourceRecord.Type  == ResourceRecordType.Value) &&
                    (!QueryClass.        HasValue || resourceRecord.Class == QueryClass.        Value)
                );

                if (existingRecords.Count == 0)
                    records.TryRemove(DomainName, out _);

            }

            Invalidate();

            return this;

        }


        /// <summary>
        /// Add one resource record, written as a single zone-file line.
        /// </summary>
        /// <remarks>
        /// A line on its own carries no origin, so every name in it is taken as
        /// complete whether or not it ends in a dot. Use <see cref="AddZoneFile"/>
        /// for an actual zone file: RFC 1035 §5.1 completes a relative name against
        /// the current origin, and a line has no current origin to offer.
        /// </remarks>
        /// <param name="ZoneFileString">A BIND-style zone-file resource record line.</param>
        /// <param name="DefaultTimeToLive">An optional TTL used when the line omits one.</param>
        /// <param name="Origin">An optional origin, against which a relative name is completed.</param>
        public InMemoryDNSZone AddZoneFileString(String       ZoneFileString,
                                                 TimeSpan?    DefaultTimeToLive   = null,
                                                 DomainName?  Origin              = null)
        {

            Add(ADNSResourceRecord.ParseZoneFileString(
                    ZoneFileString,
                    DefaultTimeToLive,
                    Origin
                ));

            return this;

        }


        /// <summary>
        /// Add every resource record of a zone file (RFC 1035 §5.1).
        /// </summary>
        /// <remarks>
        /// The origin matters more than it looks. Relative names are the ordinary
        /// way to write a zone file — <c>ns1</c>, not <c>ns1.example.com.</c> — and
        /// without an origin to complete them against they are taken as complete,
        /// which quietly puts every record at the top level instead of in the zone.
        /// Passing none here falls back to the zone's own origin, which is what a
        /// file's <c>$ORIGIN</c> would have said anyway; a file that names its own
        /// overrides both.
        /// </remarks>
        /// <param name="ZoneFile">The text of a zone file.</param>
        /// <param name="Origin">The origin relative names are completed against; the zone's own by default.</param>
        /// <param name="DefaultTimeToLive">A TTL for records that state none and are not covered by a $TTL.</param>
        public InMemoryDNSZone AddZoneFile(String       ZoneFile,
                                           DomainName?  Origin              = null,
                                           TimeSpan?    DefaultTimeToLive   = null)
        {

            Add(DNSZoneFile.Parse(
                    ZoneFile,
                    Origin ?? this.Origin,
                    DefaultTimeToLive
                ));

            return this;

        }

        #endregion


        #region Sign(Keys, Inception = null, Expiration = null, NSEC3 = null)

        /// <summary>
        /// The record types a signer produces, which are therefore not part of
        /// the zone's own data and are replaced rather than added to.
        /// </summary>
        private static readonly HashSet<DNSResourceRecordTypes> signerOutput = [
            DNSResourceRecordTypes.RRSIG,
            DNSResourceRecordTypes.NSEC,
            DNSResourceRecordTypes.NSEC3,
            DNSResourceRecordTypes.NSEC3PARAM,
            DNSResourceRecordTypes.DNSKEY
        ];

        /// <summary>
        /// Sign this zone in process: every authoritative RRset gets an RRSIG and
        /// every name its place in the chain of denial (RFC 4035 §2).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Signing replaces rather than adds. Everything a signer produces —
        /// RRSIG, NSEC, NSEC3, NSEC3PARAM and the DNSKEY RRset — is dropped first,
        /// and the DNSKEY RRset is rebuilt from <paramref name="Keys"/>. Without
        /// that, signing twice would leave the first run's signatures in place
        /// beside the second's and publish each key twice: a zone that grows every
        /// time it is signed, and whose stale RRSIGs a resolver is entitled to try
        /// and entitled to fail on.
        /// </para>
        /// <para>
        /// The consequence worth stating plainly is that a DNSKEY added by hand
        /// does not survive signing. A key that should be published has to be
        /// passed in here, because that is the only place the two lists are
        /// reconciled.
        /// </para>
        /// </remarks>
        /// <param name="Keys">One or more keys; those with the Secure Entry Point flag sign the DNSKEY RRset.</param>
        /// <param name="Inception">When the signatures become valid; an hour ago by default, for clock skew.</param>
        /// <param name="Expiration">When they stop; thirty days by default, which is what dnssec-signzone uses.</param>
        /// <param name="NSEC3">Hash the names instead of listing them (RFC 5155); NSEC by default.</param>
        public InMemoryDNSZone Sign(IEnumerable<DNSSECSigningKey>  Keys,
                                    DateTime?                      Inception    = null,
                                    DateTime?                      Expiration   = null,
                                    NSEC3Parameters?               NSEC3        = null)
        {

            var origin = Origin
                             ?? throw new InvalidOperationException(
                                    "This zone has no SOA, so it has no apex to sign at. " +
                                    "A signer needs to know where the zone begins: RFC 4035 §2.2 " +
                                    "makes the apex the boundary between what is signed and what " +
                                    "is a delegation.");

            var expiration = Expiration ?? DateTime.UtcNow.AddDays(30);
            var keys       = Keys.ToArray();

            var unsigned   = records.Values.
                                 SelectMany(list => { lock (list) { return list.ToArray(); } }).
                                 Where     (record => !signerOutput.Contains(record.Type)).
                                 ToArray();

            var signed     = DNSSECZoneSigner.Sign(
                                 unsigned,
                                 origin,
                                 keys,
                                 Inception,
                                 expiration,
                                 NSEC3
                             );

            records.Clear();
            Add(signed);

            SignedAt            = DateTime.UtcNow;
            SignaturesExpireAt  = expiration;

            // The validity is remembered as a duration rather than as the
            // absolute moment it ends, because that is what a repeat needs: a
            // re-signing that reused the old expiration would produce signatures
            // that are already as stale as the ones it replaced.
            signingKeys         = keys;
            signingNSEC3        = NSEC3;
            signingValidity     = expiration - SignedAt.Value;

            // After Add's own bump, so the zone is not born stale.
            Interlocked.Exchange(ref signedRevision, Interlocked.Read(ref revision));

            return this;

        }

        #endregion

        #region Resign() / ResignIfDue(Before)

        /// <summary>
        /// Sign again with the keys and parameters of the last <see cref="Sign"/>,
        /// giving the signatures a fresh validity of the same length.
        /// </summary>
        /// <remarks>
        /// Nothing about this is required by a specification. RFC 6781 is
        /// Informational and states no interval; a validating resolver, not an
        /// authoritative server, is what decides an expired signature is expired.
        /// This exists because a zone that signs itself at start-up and is then
        /// served for longer than its signatures last will be called Bogus by
        /// every validator on the same day, and the server will have done nothing
        /// wrong to deserve it.
        /// </remarks>
        public InMemoryDNSZone Resign()
        {

            var keys = signingKeys
                           ?? throw new InvalidOperationException(
                                  "This zone has never been signed, so there is nothing to repeat. " +
                                  "Call Sign with keys first — a re-signing deliberately does not " +
                                  "invent parameters it was never given.");

            return Sign(
                       keys,
                       Inception:  null,
                       Expiration: DateTime.UtcNow.Add(signingValidity ?? TimeSpan.FromDays(30)),
                       NSEC3:      signingNSEC3
                   );

        }

        /// <summary>
        /// Sign again if the signatures expire within <paramref name="Before"/>,
        /// or if the records have moved on from them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Driven by the caller rather than by a timer inside this object, on
        /// purpose. A timer here would need disposing, would fire on a thread
        /// nobody owns, and would make every test of this behaviour wait for wall
        /// clock time. An operator — or a server's own housekeeping — knows when
        /// it is convenient to spend the CPU that signing a zone costs.
        /// </para>
        /// <para>
        /// The window matters more than it looks: signatures have to be replaced
        /// before the old ones leave caches, not before they expire, which is why
        /// <paramref name="Before"/> is a parameter and not a constant.
        /// </para>
        /// </remarks>
        /// <param name="Before">How long before expiry to act.</param>
        /// <returns>Whether it signed.</returns>
        public Boolean ResignIfDue(TimeSpan Before)
        {

            // A zone nobody has signed is not due for anything.
            if (signingKeys is null || !SignaturesExpireAt.HasValue)
                return false;

            if (!SignaturesAreStale &&
                 SignaturesExpireAt.Value - DateTime.UtcNow > Before)
            {
                return false;
            }

            Resign();

            return true;

        }

        #endregion


        #region Lookup(Question, DNSSECOK = false, CancellationToken = default)

        public Task<DNSZoneLookupResult> Lookup(DNSQuestion        Question,
                                                Boolean            DNSSECOK            = false,
                                                CancellationToken  CancellationToken   = default)
        {

            CancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(Resolve(Question, DNSSECOK));

        }

        #endregion

        #region (private) Resolve(Question, DNSSECOK)

        private DNSZoneLookupResult Resolve(DNSQuestion  Question,
                                            Boolean      DNSSECOK)
        {

            var zone   = Index();
            var qname  = ZoneDenialOfExistence.Normalize(Question.DomainName.FullName);

            // A store with no SOA is a bag of records rather than a zone: there is
            // no apex to measure the name against, so answer by exact name and
            // cite nothing.
            if (zone.Origin is null)
                return ExactMatchOnly(Question, DNSSECOK);

            // A name outside the apex is still served when this store actually
            // holds it — the fixtures rely on that to keep a reverse-lookup PTR
            // beside a forward zone — but only then. Falling through to an exact
            // match and reporting NXDOMAIN when there is nothing is the part that
            // is wrong: that is an authoritative claim that a name does not
            // exist, cacheable for its whole subtree under RFC 8020, about a zone
            // this server never served.
            if (!IsAtOrBelow(qname, ZoneDenialOfExistence.Normalize(zone.Origin.FullName)))
            {

                var outside = ExactMatchOnly(Question, DNSSECOK);

                return outside.Status == DNSZoneLookupStatus.NameError
                           ? DNSZoneLookupResult.NotAuthoritative()
                           : outside;

            }

            // RFC 1034 §4.3.2 step 3b: a zone cut between the apex and QNAME ends
            // the search here. The child's NS records are the answer, and this
            // server is not authoritative for what lies below them.
            if (FindDelegation(qname, Question.QueryType, zone) is { } delegationName)
                return Refer(delegationName, zone, DNSSECOK);

            // RFC 6672 §3.2 step 3c: a DNAME *above* QNAME redirects it. This
            // runs before the exact match on purpose — the DNAME owns its whole
            // subtree, so anything found at a name below it is occluded (§2.4:
            // "Resource records MUST NOT exist at any subdomain of the owner of a
            // DNAME RR"), and answering from an occluded record would hide a
            // malformed zone rather than redirect as the zone says.
            //
            // Only strict ancestors are searched, which is what keeps the owner
            // name itself out of it (§2.3) — a query for the DNAME's own name
            // falls through to the exact match below and is answered from
            // whatever else lives there.
            if (FindRedirection(qname, zone) is { } dnameName &&
                TryGetRecords(dnameName, out var atDName))
            {
                return DNSZoneLookupResult.Redirect(
                           WithSignatures(
                               [.. atDName.Where(resourceRecord => resourceRecord.Type == DNSResourceRecordTypes.DNAME)],
                               atDName,
                               DNSSECOK
                           )
                       );
            }

            // Step 3a: an exact match on the name.
            if (TryGetRecords(Question.DomainName, out var atName))
            {

                var answers = SelectByType(atName, Question);

                if (answers.Length > 0)
                    return DNSZoneLookupResult.Found(
                               WithSignatures(answers, atName, DNSSECOK)
                           );

                // A node holding a CNAME is never NODATA: the caller restarts the
                // query at the canonical name, and inventing a denial here would
                // contradict the answer it is about to build.
                if (atName.Any(resourceRecord => resourceRecord.Type == DNSResourceRecordTypes.CNAME))
                    return DNSZoneLookupResult.NoData();

                return NoData(qname, Question, zone, DNSSECOK);

            }

            // An empty non-terminal: no records of its own, but names below it.
            // RFC 4592 §2.2.2 — it exists, so this is NODATA, and no wildcard may
            // be applied to it.
            if (zone.Names.Contains(qname))
                return NoData(qname, Question, zone, DNSSECOK);

            return Synthesize(qname, Question, zone, DNSSECOK);

        }

        #endregion

        #region (private) Synthesize(QName, Question, Zone, DNSSECOK)

        /// <summary>
        /// RFC 4592 §3.3.1 — the closest encloser of QNAME, then exactly one
        /// wildcard lookup at it.
        /// </summary>
        /// <remarks>
        /// The "exactly one" is the whole subtlety. It is tempting to walk up from
        /// QNAME taking the first wildcard found anywhere above it, and that is
        /// wrong: with <c>*.example.</c> and <c>sub.example.</c> both in the zone,
        /// a query for <c>x.sub.example.</c> has closest encloser
        /// <c>sub.example.</c>, so the only wildcard that may answer is
        /// <c>*.sub.example.</c> — which does not exist. The name error is the
        /// correct answer, and a server that reaches past the closest encloser
        /// answers with data the zone never authorized.
        /// </remarks>
        private DNSZoneLookupResult Synthesize(String       QName,
                                               DNSQuestion  Question,
                                               ZoneIndex    Zone,
                                               Boolean      DNSSECOK)
        {

            var closestEncloser = ClosestEncloser(QName, Zone);

            var wildcard        = closestEncloser is null
                                      ? null
                                      : closestEncloser == "."
                                            ? "*."
                                            : $"*.{closestEncloser}";

            // TryParse rather than Parse: a wildcard directly under the root is
            // "*.", which the name parser refuses, and a zone that cannot hold a
            // wildcard simply has none to apply.
            if (wildcard is not null &&
                DNSServiceName.TryParse(wildcard) is { } wildcardName)
            {

                if (TryGetRecords(wildcardName, out var atWildcard))
                {

                    var matches = SelectByType(atWildcard, Question);

                    if (matches.Length > 0)
                    {

                        // §3.3.1: the answer carries the queried name, never the
                        // wildcard label — including on the RRSIG, whose own
                        // "labels" field is what still reveals the synthesis
                        // (RFC 4035 §3.1.3.3).
                        var synthesized = WithSignatures(matches, atWildcard, DNSSECOK).
                                              Select(resourceRecord => ADNSResourceRecord.CloneWithOwner(resourceRecord, Question.DomainName)).
                                              ToArray();

                        var authority   = new List<IDNSResourceRecord>();

                        if (DNSSECOK && Zone.Denial is not null)
                            authority.AddRange(Zone.Denial.ForWildcardAnswer(QName, wildcard));

                        return DNSZoneLookupResult.Found(synthesized, authority);

                    }

                    // The wildcard matched the name but holds no such type.
                    var wildcardNoData = new List<IDNSResourceRecord>();

                    AddStartOfAuthority(wildcardNoData, Zone, DNSSECOK);

                    if (DNSSECOK && Zone.Denial is not null)
                        wildcardNoData.AddRange(Zone.Denial.ForWildcardNoData(QName, wildcard, Question.QueryType));

                    return DNSZoneLookupResult.NoData(wildcardNoData);

                }

            }

            var nameError = new List<IDNSResourceRecord>();

            AddStartOfAuthority(nameError, Zone, DNSSECOK);

            if (DNSSECOK && Zone.Denial is not null)
                nameError.AddRange(Zone.Denial.ForNameError(QName));

            return DNSZoneLookupResult.NameError(nameError);

        }

        #endregion

        #region (private) NoData(QName, Question, Zone, DNSSECOK)

        private DNSZoneLookupResult NoData(String       QName,
                                           DNSQuestion  Question,
                                           ZoneIndex    Zone,
                                           Boolean      DNSSECOK)
        {

            var authority = new List<IDNSResourceRecord>();

            AddStartOfAuthority(authority, Zone, DNSSECOK);

            if (DNSSECOK && Zone.Denial is not null)
                authority.AddRange(Zone.Denial.ForNoData(QName, Question.QueryType));

            return DNSZoneLookupResult.NoData(authority);

        }

        #endregion

        #region (private) Refer(DelegationName, Zone, DNSSECOK)

        /// <summary>
        /// Build the referral for a name below a zone cut: the child's NS records,
        /// whatever glue this zone holds for them, and — for a DO querier — either
        /// the DS that makes the delegation secure or the NSEC/NSEC3 that proves
        /// there is none (RFC 4035 §3.1.4.1, RFC 5155 §7.2.7).
        /// </summary>
        private DNSZoneLookupResult Refer(DNSServiceName  DelegationName,
                                          ZoneIndex       Zone,
                                          Boolean         DNSSECOK)
        {

            TryGetRecords(DelegationName, out var atDelegation);

            var authority   = new List<IDNSResourceRecord>(
                                  atDelegation.Where(resourceRecord => resourceRecord.Type == DNSResourceRecordTypes.NS)
                              );

            var delegated   = ZoneDenialOfExistence.Normalize(DelegationName.FullName);

            if (DNSSECOK)
            {

                var delegationSigners = atDelegation.Where(resourceRecord => resourceRecord.Type == DNSResourceRecordTypes.DS).ToArray();

                if (delegationSigners.Length > 0)
                    authority.AddRange(WithSignatures(delegationSigners, atDelegation, true));

                // No DS: the delegation is insecure, and *that* is what has to be
                // proven. Without it a validator cannot tell an unsigned child
                // from a signed one whose DS was stripped in flight.
                else if (Zone.Denial is not null)
                    authority.AddRange(Zone.Denial.ForNoData(delegated, DNSResourceRecordTypes.DS));

            }

            // Glue: the addresses of name servers that live inside the delegated
            // subtree, which nobody else can resolve (RFC 1034 §4.2.1).
            var glue = new List<IDNSResourceRecord>();

            foreach (var nameServer in authority.OfType<NS>())
            {

                var target = ZoneDenialOfExistence.Normalize(nameServer.NameServer.FullName);

                if (!IsAtOrBelow(target, delegated))
                    continue;

                if (TryGetRecords(DNSServiceName.Parse(target), out var atNameServer))
                    glue.AddRange(atNameServer.Where(resourceRecord => resourceRecord.Type is DNSResourceRecordTypes.A
                                                                                          or DNSResourceRecordTypes.AAAA));

            }

            return DNSZoneLookupResult.Referral(authority, glue);

        }

        #endregion

        #region (private) ExactMatchOnly(Question, DNSSECOK)

        /// <summary>
        /// The lookup this store did before it knew what a zone was: match the
        /// name exactly or answer NXDOMAIN, with nothing in the authority section.
        /// </summary>
        private DNSZoneLookupResult ExactMatchOnly(DNSQuestion  Question,
                                                   Boolean      DNSSECOK)
        {

            if (!TryGetRecords(Question.DomainName, out var atName))
                return DNSZoneLookupResult.NameError();

            var answers = SelectByType(atName, Question);

            return answers.Length > 0
                       ? DNSZoneLookupResult.Found(WithSignatures(answers, atName, DNSSECOK))
                       : DNSZoneLookupResult.NoData();

        }

        #endregion


        #region (private) SelectByType(Records, Question) / WithSignatures(...) / AddStartOfAuthority(...)

        private static IDNSResourceRecord[] SelectByType(IDNSResourceRecord[]  Records,
                                                         DNSQuestion           Question)

            => [.. Records.Where(resourceRecord =>
                       (Question.QueryClass == DNSQueryClasses.ANY ||
                        resourceRecord.Class == Question.QueryClass) &&
                       (Question.QueryType  == DNSResourceRecordTypes.Any ||
                        resourceRecord.Type  == Question.QueryType))];


        /// <summary>
        /// The answer plus, when the querier asked for them, the RRSIGs covering
        /// each type in it (RFC 4035 §3.1.1).
        /// </summary>
        private static IDNSResourceRecord[] WithSignatures(IDNSResourceRecord[]  Answers,
                                                           IDNSResourceRecord[]  AtName,
                                                           Boolean               DNSSECOK)
        {

            if (!DNSSECOK || Answers.Length == 0)
                return Answers;

            var covered     = Answers.Select(resourceRecord => resourceRecord.Type).
                                      Where (type => type != DNSResourceRecordTypes.RRSIG).
                                      ToHashSet();

            var signatures  = AtName.OfType<RRSIG>().
                                     Where(signature => covered.Contains(signature.TypeCovered)).
                                     ToArray();

            return signatures.Length == 0
                       ? Answers
                       : [.. Answers, .. signatures];

        }


        /// <summary>
        /// RFC 2308 §3 — a negative answer carries the zone's SOA so the resolver
        /// knows how long it may remember the "no".
        /// </summary>
        private void AddStartOfAuthority(List<IDNSResourceRecord>  Authority,
                                         ZoneIndex                 Zone,
                                         Boolean                   DNSSECOK)
        {

            if (Zone.StartOfAuthority is null)
                return;

            Authority.Add(Zone.StartOfAuthority);

            if (DNSSECOK)
                Authority.AddRange(SignaturesOf(Zone.StartOfAuthority));

        }

        #endregion

        #region (private) SignaturesOf(Record) / TryGetRecords(Name, out Records)

        private IEnumerable<IDNSResourceRecord> SignaturesOf(IDNSResourceRecord ResourceRecord)

            => TryGetRecords(ResourceRecord.DomainName, out var atName)
                   ? atName.OfType<RRSIG>().Where(signature => signature.TypeCovered == ResourceRecord.Type)
                   : [];


        private Boolean TryGetRecords(DNSServiceName            Name,
                                      out IDNSResourceRecord[]  Records)
        {

            if (!records.TryGetValue(Name, out var atName))
            {
                Records = [];
                return false;
            }

            lock (atName)
                Records = [.. atName];

            return Records.Length > 0;

        }

        #endregion

        #region (private) FindDelegation(QName, Zone) / ClosestEncloser(QName, Zone) / IsAtOrBelow(...)

        /// <summary>
        /// The nearest zone cut strictly between the apex and QNAME, or null when
        /// QNAME is served from this zone directly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The apex is excluded on purpose: every zone has NS records at its own
        /// apex, and treating those as a delegation would turn the zone into a
        /// referral to itself. QNAME itself is normally included — a query for
        /// the delegated name is answered with the referral, not from the parent.
        /// </para>
        /// <para>
        /// DS is the exception, and RFC 4035 §3.1.4.1 is explicit about it: "The
        /// DS RRset and its associated RRSIG RRs are authoritative data in the
        /// parent zone." So a DS query *at* a zone cut is the one question about
        /// that name the parent must answer itself. Referring it downwards would
        /// send a validator to ask the child whether the child is signed — the
        /// one party whose answer cannot be trusted for it — and the chain of
        /// trust would stall at every delegation.
        /// </para>
        /// </remarks>
        private DNSServiceName? FindDelegation(String                  QName,
                                               DNSResourceRecordTypes  QueryType,
                                               ZoneIndex               Zone)
        {

            if (Zone.Origin is null)
                return null;

            var labels    = ZoneDenialOfExistence.LabelsOf(QName);
            var apexDepth = ZoneDenialOfExistence.LabelsOf(Zone.Origin.FullName).Length;

            // A delegation *above* QNAME still ends the search, even for DS: that
            // name is genuinely in the child's half of the tree.
            var firstLabel = QueryType == DNSResourceRecordTypes.DS ? 1 : 0;

            for (var skip = firstLabel; labels.Length - skip > apexDepth; skip++)
            {

                var candidate = DNSServiceName.Parse(ZoneDenialOfExistence.Join(labels, skip));

                if (TryGetRecords(candidate, out var atCandidate) &&
                    atCandidate.Any(resourceRecord => resourceRecord.Type == DNSResourceRecordTypes.NS))
                {
                    return candidate;
                }

            }

            return null;

        }


        /// <summary>
        /// The closest strict ancestor of QNAME holding a DNAME, or null.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Closest rather than highest, because that is where RFC 1034's descent
        /// stops: labels are matched downward from the apex until one cannot be,
        /// and RFC 6672 §3.2 step 3c then looks for a DNAME at the last name that
        /// did match. In a zone that obeys §2.4 there is only ever one candidate
        /// anyway — nothing may exist below a DNAME owner, so DNAMEs cannot nest
        /// within one zone — and picking the closest is what makes a zone that
        /// breaks the rule behave predictably rather than by dictionary order.
        /// </para>
        /// <para>
        /// The search starts one label below QNAME, never at QNAME: §2.3 leaves
        /// the owner name itself unredirected.
        /// </para>
        /// </remarks>
        private DNSServiceName? FindRedirection(String     QName,
                                                ZoneIndex  Zone)
        {

            if (Zone.Origin is null)
                return null;

            var labels    = ZoneDenialOfExistence.LabelsOf(QName);
            var apexDepth = ZoneDenialOfExistence.LabelsOf(Zone.Origin.FullName).Length;

            for (var skip = 1; labels.Length - skip >= apexDepth; skip++)
            {

                var candidate = DNSServiceName.Parse(ZoneDenialOfExistence.Join(labels, skip));

                if (TryGetRecords(candidate, out var atCandidate) &&
                    atCandidate.Any(resourceRecord => resourceRecord.Type == DNSResourceRecordTypes.DNAME))
                {
                    return candidate;
                }

            }

            return null;

        }


        private static String? ClosestEncloser(String     QName,
                                               ZoneIndex  Zone)
        {

            if (Zone.Origin is null)
                return null;

            var labels    = ZoneDenialOfExistence.LabelsOf(QName);
            var apexDepth = ZoneDenialOfExistence.LabelsOf(Zone.Origin.FullName).Length;

            for (var skip = 1; labels.Length - skip >= apexDepth; skip++)
            {

                var candidate = ZoneDenialOfExistence.Join(labels, skip);

                if (Zone.Names.Contains(candidate))
                    return candidate;

            }

            return null;

        }


        private static Boolean IsAtOrBelow(String Name, String Ancestor)

            => Name.Equals(Ancestor, StringComparison.OrdinalIgnoreCase) ||
               Name.EndsWith("." + Ancestor.TrimStart('.'), StringComparison.OrdinalIgnoreCase) ||
               Ancestor == ".";

        #endregion

        #region (private) Index() / Invalidate()

        private void Invalidate()
        {

            // Every change bumps the revision, which is what lets Sign tell
            // afterwards whether the records it signed are still the records
            // being served.
            Interlocked.Increment(ref revision);

            lock (indexLock)
                index = null;

        }


        private ZoneIndex Index()
        {

            var current = index;

            if (current is not null)
                return current;

            lock (indexLock)
            {

                if (index is not null)
                    return index;

                var all = records.Values.SelectMany(list => {
                                             lock (list)
                                                 return list.ToArray();
                                         }).
                                         ToArray();

                // The apex is the SOA's owner name. A store holding more than one
                // — which is not a zone, but is a thing tests do — is anchored at
                // the shortest, so the others are simply names inside it.
                var startOfAuthority = all.OfType<SOA>().
                                           OrderBy(soa => soa.DomainName.FullName.Length).
                                           FirstOrDefault();

                var origin           = startOfAuthority is not null
                                           ? DomainName.ParseLenient(startOfAuthority.DomainName.FullName)
                                           : null;

                // Every owner name, plus every name between it and the apex:
                // those in-between names hold no records but they exist, and the
                // difference between "exists with nothing" and "does not exist" is
                // the difference between NODATA and NXDOMAIN.
                var names            = new HashSet<String>(StringComparer.Ordinal);

                if (origin is not null)
                {

                    var apex      = ZoneDenialOfExistence.Normalize(origin.FullName);
                    var apexDepth = ZoneDenialOfExistence.LabelsOf(apex).Length;

                    foreach (var owner in records.Keys)
                    {

                        var name = ZoneDenialOfExistence.Normalize(owner.FullName);

                        // Names this store happens to hold outside the zone are
                        // not part of it, and their ancestors are not names here.
                        if (!IsAtOrBelow(name, apex))
                            continue;

                        var labels = ZoneDenialOfExistence.LabelsOf(name);

                        for (var skip = 0; labels.Length - skip >= apexDepth; skip++)
                            names.Add(ZoneDenialOfExistence.Join(labels, skip));

                    }

                }

                index = new ZoneIndex {
                            Origin            = origin,
                            StartOfAuthority  = startOfAuthority,
                            Names             = names,
                            Denial            = origin is not null
                                                    ? new ZoneDenialOfExistence(
                                                          origin,
                                                          all,
                                                          names.Contains,
                                                          SignaturesOf
                                                      )
                                                    : null
                        };

                return index;

            }

        }

        #endregion


        #region (static) CreateDemoZone()

        public static InMemoryDNSZone CreateDemoZone()
        {

            var zone = new InMemoryDNSZone();

            zone.Add(
                new A(
                    DomainName.Parse("api1.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    IPv4Address.Parse("141.24.12.2")
                ),
                new AAAA(
                    DomainName.Parse("api2.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    IPv6Address.Parse("::2")
                ),
                new SRV(
                    DNSServiceName.Parse("_ocpp._tls.api2.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    10,
                    20,
                    IPPort.Parse(443),
                    DomainName.Parse("api2.example.org.")
                ),
                new SSHFP(
                    DomainName.Parse("api2.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    SSHFP_Algorithm.ECDSA,
                    SSHFP_FingerprintType.SHA256,
                    "0095d7637f456888505741e952a1e7ff635e018f9a95c9b3b38af4bb9fdb0c36".FromHEX()
                ),
                new TXT(
                    DomainName.Parse("api2.example.org."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(30),
                    "Hello world!"
                )
            );

            return zone;

        }

        #endregion

    }

}
