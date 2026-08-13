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
    /// "no". Nothing here signs anything: a zone is signed offline, and this
    /// serves what the signer produced.
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

        /// <summary>Whether the zone carries the NSEC/NSEC3 records needed to deny authenticated.</summary>
        public Boolean IsSigned
            => Index().Denial?.IsSigned == true;

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


        public InMemoryDNSZone AddZoneFileString(String     ZoneFileString,
                                                 TimeSpan?  DefaultTimeToLive = null)
        {

            Add(ADNSResourceRecord.ParseZoneFileString(
                    ZoneFileString,
                    DefaultTimeToLive
                ));

            return this;

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

            // No SOA, or a name this store holds outside any zone it is
            // authoritative for: answer by exact name and cite nothing.
            if (zone.Origin is null ||
                !IsAtOrBelow(qname, ZoneDenialOfExistence.Normalize(zone.Origin.FullName)))
            {
                return ExactMatchOnly(Question, DNSSECOK);
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
