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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The zone half of authenticated denial of existence: picking the NSEC or
    /// NSEC3 records that prove a negative answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DenialOfExistenceValidator"/> is the other half — it asks
    /// whether a set of records proves what a response claims. This asks the
    /// dual question: given a zone and a query it cannot answer, which of the
    /// zone's records have to travel with the "no" for a validator to believe it.
    /// </para>
    /// <para>
    /// Nothing here computes a signature or invents a record. A zone is signed
    /// offline, by <c>dnssec-signzone</c> or an equivalent, and every NSEC,
    /// NSEC3 and RRSIG this returns was already in the zone data. The work is
    /// entirely one of selection, and getting the selection wrong is a real
    /// failure mode: a response missing one of the three NSEC3 records of a
    /// closest-encloser proof is not "slightly less secure", it is Bogus, and a
    /// validating resolver will discard it.
    /// </para>
    /// <para>
    /// RFC 7129 is the readable account of why these particular records, and in
    /// these combinations.
    /// </para>
    /// </remarks>
    public sealed class ZoneDenialOfExistence
    {

        #region Data

        private readonly DomainName                                       origin;
        private readonly NSEC[]                                           nsecRecords;
        private readonly NSEC3[]                                          nsec3Records;
        private readonly NSEC3?                                           nsec3Parameters;
        private readonly Func<String, Boolean>                            nameExists;
        private readonly Func<IDNSResourceRecord, IEnumerable<IDNSResourceRecord>> signaturesOf;

        #endregion

        #region Properties

        /// <summary>
        /// Whether this zone denies with NSEC3 rather than NSEC. RFC 5155 §7.1:
        /// a zone uses one or the other, never both, so the presence of a single
        /// NSEC3 settles it.
        /// </summary>
        public Boolean UsesNSEC3
            => nsec3Records.Length > 0;

        /// <summary>Whether this zone can prove anything at all — an unsigned zone cannot.</summary>
        public Boolean IsSigned
            => nsec3Records.Length > 0 || nsecRecords.Length > 0;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the denial machinery for one zone.
        /// </summary>
        /// <param name="Origin">The zone apex.</param>
        /// <param name="Records">Every record in the zone.</param>
        /// <param name="NameExists">Whether a name exists in the zone — including empty non-terminals, which have no records of their own but are still names (RFC 4592 §2.2.2).</param>
        /// <param name="SignaturesOf">The RRSIGs covering a given record's owner and type.</param>
        public ZoneDenialOfExistence(DomainName                                                 Origin,
                                     IEnumerable<IDNSResourceRecord>                            Records,
                                     Func<String, Boolean>                                      NameExists,
                                     Func<IDNSResourceRecord, IEnumerable<IDNSResourceRecord>>  SignaturesOf)
        {

            var records           = Records.ToArray();

            this.origin           = Origin;
            this.nsecRecords      = [.. records.OfType<NSEC>()];
            this.nsec3Records     = [.. records.OfType<NSEC3>()];
            this.nsec3Parameters  = nsec3Records.FirstOrDefault();
            this.nameExists       = NameExists;
            this.signaturesOf     = SignaturesOf;

        }

        #endregion


        #region ForNameError      (QName)

        /// <summary>
        /// The records proving that <paramref name="QName"/> is not in this zone.
        /// </summary>
        /// <remarks>
        /// Two things have to be proven, not one. That the name itself is absent,
        /// and that no wildcard could have synthesized it — otherwise a resolver
        /// cannot tell "no such name" from "an answer was stripped out".
        /// RFC 4035 §3.1.3.2 for NSEC, RFC 5155 §7.2.2 for NSEC3.
        /// </remarks>
        public IEnumerable<IDNSResourceRecord> ForNameError(String QName)
        {

            var proof = new List<IDNSResourceRecord>();

            if (UsesNSEC3)
            {

                // §7.2.2 — three records: the closest encloser matched, the next
                // closer name covered, and the wildcard at the closest encloser
                // covered. They may coincide; duplicates are dropped below.
                var closestEncloser = ClosestProvableEncloser(QName);

                if (closestEncloser is null)
                    return proof;

                Collect(proof, MatchingNSEC3(closestEncloser));
                Collect(proof, CoveringNSEC3(NextCloser(QName, closestEncloser)));
                Collect(proof, CoveringNSEC3(Wildcard(closestEncloser)));

            }

            else
            {

                // §3.1.3.2 — the NSEC covering the name, and the NSEC covering
                // the wildcard that would otherwise have answered. One record
                // often covers both.
                Collect(proof, CoveringNSEC(QName));

                var closestEncloser = ClosestEncloser(QName);

                if (closestEncloser is not null)
                    Collect(proof, CoveringNSEC(Wildcard(closestEncloser)));

            }

            return proof;

        }

        #endregion

        #region ForNoData         (QName, QType)

        /// <summary>
        /// The records proving that <paramref name="QName"/> exists but holds no
        /// <paramref name="QType"/> (RFC 4035 §3.1.3.1, RFC 5155 §7.2.3).
        /// </summary>
        public IEnumerable<IDNSResourceRecord> ForNoData(String                  QName,
                                                         DNSResourceRecordTypes  QType)
        {

            var proof = new List<IDNSResourceRecord>();

            if (UsesNSEC3)
            {

                var match = MatchingNSEC3(QName);

                if (match is not null)
                {
                    Collect(proof, match);
                    return proof;
                }

                // §7.2.4 — no NSEC3 matches. In a correctly signed zone that
                // happens for exactly one query: a DS at a name inside an opt-out
                // span, which the zone deliberately did not hash. The proof is
                // then the closest encloser and the covering record, and the
                // opt-out flag is what tells the validator to expect nothing more.
                var closestEncloser = ClosestProvableEncloser(QName);

                if (closestEncloser is null)
                    return proof;

                Collect(proof, MatchingNSEC3(closestEncloser));
                Collect(proof, CoveringNSEC3(NextCloser(QName, closestEncloser)));

            }

            else
                Collect(proof, MatchingNSEC(QName));

            return proof;

        }

        #endregion

        #region ForWildcardAnswer (QName, Wildcard)

        /// <summary>
        /// The record proving that a wildcard-synthesized answer was legitimate:
        /// that <paramref name="QName"/> itself does not exist, so the wildcard
        /// really was the best match (RFC 4035 §3.1.3.3, RFC 5155 §7.2.5).
        /// </summary>
        /// <remarks>
        /// Without this a resolver has no way to distinguish an answer the zone
        /// synthesized from one an attacker synthesized, because the RRSIG that
        /// travels with a wildcard answer validates equally well under any name
        /// the wildcard could have covered. The <c>labels</c> field of that RRSIG
        /// is what reveals a wildcard was involved; this record is what bounds
        /// which names it was allowed to cover.
        /// </remarks>
        public IEnumerable<IDNSResourceRecord> ForWildcardAnswer(String  QName,
                                                                 String  Wildcard)
        {

            var proof           = new List<IDNSResourceRecord>();
            var closestEncloser = ParentOf(Wildcard);

            if (closestEncloser is null)
                return proof;

            if (UsesNSEC3)
                Collect(proof, CoveringNSEC3(NextCloser(QName, closestEncloser)));
            else
                Collect(proof, CoveringNSEC(QName));

            return proof;

        }

        #endregion

        #region ForWildcardNoData (QName, Wildcard, QType)

        /// <summary>
        /// The records proving that a wildcard matched <paramref name="QName"/>
        /// but holds no <paramref name="QType"/> (RFC 4035 §3.1.3.4,
        /// RFC 5155 §7.2.6).
        /// </summary>
        public IEnumerable<IDNSResourceRecord> ForWildcardNoData(String                  QName,
                                                                 String                  Wildcard,
                                                                 DNSResourceRecordTypes  QType)
        {

            var proof           = new List<IDNSResourceRecord>();
            var closestEncloser = ParentOf(Wildcard);

            if (closestEncloser is null)
                return proof;

            if (UsesNSEC3)
            {
                Collect(proof, MatchingNSEC3(closestEncloser));
                Collect(proof, CoveringNSEC3(NextCloser(QName, closestEncloser)));
                Collect(proof, MatchingNSEC3(Wildcard));
            }

            else
            {
                Collect(proof, MatchingNSEC(Wildcard));
                Collect(proof, CoveringNSEC(QName));
            }

            return proof;

        }

        #endregion


        #region (private) MatchingNSEC (Name) / CoveringNSEC (Name)

        /// <summary>The NSEC whose owner name *is* the given name.</summary>
        private NSEC? MatchingNSEC(String Name)

            => nsecRecords.FirstOrDefault(nsec => DenialOfExistenceValidator.CompareCanonical(
                                                      nsec.DomainName.FullName,
                                                      Name
                                                  ) == 0);

        /// <summary>The NSEC whose span strictly contains the given name.</summary>
        private NSEC? CoveringNSEC(String Name)
        {

            foreach (var nsec in nsecRecords)
            {

                var owner = nsec.DomainName.FullName;
                var next  = nsec.NextDomainName.FullName;

                var above = DenialOfExistenceValidator.CompareCanonical(Name, owner) > 0;
                var below = DenialOfExistenceValidator.CompareCanonical(Name, next)  < 0;

                // The last NSEC of a zone points back at the apex, so its span
                // wraps around the end of the chain.
                var wraps = DenialOfExistenceValidator.CompareCanonical(owner, next) >= 0;

                if (wraps ? (above || below)
                          : (above && below))
                    return nsec;

            }

            return null;

        }

        #endregion

        #region (private) MatchingNSEC3(Name) / CoveringNSEC3(Name)

        /// <summary>The NSEC3 whose owner hash is the hash of the given name.</summary>
        private NSEC3? MatchingNSEC3(String? Name)
        {

            if (Name is null || nsec3Parameters is null)
                return null;

            var hash = HashOf(Name);

            if (hash is null)
                return null;

            return nsec3Records.FirstOrDefault(nsec3 => OwnerHashOf(nsec3) is Byte[] owner &&
                                                        owner.SequenceEqual(hash));

        }

        /// <summary>The NSEC3 whose hash span strictly contains the hash of the given name.</summary>
        private NSEC3? CoveringNSEC3(String? Name)
        {

            if (Name is null || nsec3Parameters is null)
                return null;

            var hash = HashOf(Name);

            if (hash is null)
                return null;

            foreach (var nsec3 in nsec3Records)
            {

                var owner = OwnerHashOf(nsec3);

                if (owner is null)
                    continue;

                var next  = nsec3.NextHashedOwnerName;
                var above = CompareHashes(hash, owner) > 0;
                var below = CompareHashes(hash, next)  < 0;
                var wraps = CompareHashes(owner, next) >= 0;

                if (wraps ? (above || below)
                          : (above && below))
                    return nsec3;

            }

            return null;

        }

        #endregion

        #region (private) HashOf(Name) / OwnerHashOf(Record) / CompareHashes(Left, Right)

        private Byte[]? HashOf(String Name)
        {

            if (nsec3Parameters is null)
                return null;

            try
            {
                return NSEC3.ComputeHash(
                           DomainName.ParseLenient(Name),
                           nsec3Parameters.Iterations,
                           nsec3Parameters.Salt,
                           nsec3Parameters.HashAlgorithm
                       );
            }
            catch (NotSupportedException)
            {
                // A hash algorithm this build cannot compute. RFC 5155 §8.1: such
                // records simply cannot serve as proof, and answering with them
                // anyway would be worse than answering with none.
                return null;
            }
            catch (ArgumentException)
            {
                // A name the parser will not take — "*." under a root zone is the
                // one that occurs in practice. Same conclusion: no proof rather
                // than a wrong one.
                return null;
            }

        }

        private static Byte[]? OwnerHashOf(NSEC3 Record)
        {

            var label = Record.DomainName.FullName.TrimEnd('.').Split('.').FirstOrDefault();

            if (label is null || label.Length == 0)
                return null;

            try
            {
                return NSEC3.Base32HexDecode(label);
            }
            catch (FormatException)
            {
                return null;
            }

        }

        private static Int32 CompareHashes(Byte[] Left, Byte[] Right)
        {

            var shared = Math.Min(Left.Length, Right.Length);

            for (var i = 0; i < shared; i++)
                if (Left[i] != Right[i])
                    return Left[i] < Right[i] ? -1 : 1;

            return Left.Length.CompareTo(Right.Length);

        }

        #endregion

        #region (private) ClosestEncloser(QName) / ClosestProvableEncloser(QName)

        /// <summary>
        /// The longest ancestor of <paramref name="QName"/> that exists in the
        /// zone (RFC 4592 §3.3.1). Empty non-terminals count: they hold no
        /// records but they are names.
        /// </summary>
        private String? ClosestEncloser(String QName)
        {

            var labels = LabelsOf(QName);
            var apex   = LabelsOf(origin.FullName).Length;

            for (var skip = 1; labels.Length - skip >= apex; skip++)
            {

                var candidate = Join(labels, skip);

                if (nameExists(candidate))
                    return candidate;

            }

            return nameExists(Normalize(origin.FullName))
                       ? Normalize(origin.FullName)
                       : null;

        }

        /// <summary>
        /// The longest ancestor of <paramref name="QName"/> with a matching
        /// NSEC3 — the "closest provable encloser" of RFC 5155 §7.2.1.
        /// </summary>
        /// <remarks>
        /// Deliberately not the same as <see cref="ClosestEncloser"/>. Opt-out
        /// leaves parts of the namespace unhashed, so the closest encloser a zone
        /// can *prove* may be higher up than the one it actually has. Deriving
        /// this from the NSEC3 chain rather than from the name index is what makes
        /// the proof match what the zone signed.
        /// </remarks>
        private String? ClosestProvableEncloser(String QName)
        {

            var labels = LabelsOf(QName);
            var apex   = LabelsOf(origin.FullName).Length;

            for (var skip = 0; labels.Length - skip >= apex; skip++)
            {

                var candidate = Join(labels, skip);

                if (MatchingNSEC3(candidate) is not null)
                    return candidate;

            }

            return null;

        }

        #endregion

        #region (private static) Name helpers

        /// <summary>
        /// The "next closer" name of RFC 5155 §1.3: one label longer than the
        /// closest encloser, on the way down to QNAME.
        /// </summary>
        private static String? NextCloser(String QName, String ClosestEncloser)
        {

            var qnameLabels    = LabelsOf(QName);
            var encloserLabels = LabelsOf(ClosestEncloser);

            // QNAME is the encloser: there is no name between them.
            if (qnameLabels.Length <= encloserLabels.Length)
                return null;

            return Join(qnameLabels, qnameLabels.Length - encloserLabels.Length - 1);

        }

        /// <summary>The wildcard name at a given owner: <c>*.owner</c>.</summary>
        private static String Wildcard(String Owner)
            => Owner == "." ? "*." : $"*.{Owner}";

        private static String? ParentOf(String Name)
        {

            var labels = LabelsOf(Name);

            return labels.Length == 0
                       ? null
                       : Join(labels, 1);

        }

        internal static String[] LabelsOf(String Name)
        {

            var trimmed = Name.TrimEnd('.');

            return trimmed.Length == 0
                       ? []
                       : trimmed.Split('.');

        }

        internal static String Join(String[] Labels, Int32 Skip)

            => Skip >= Labels.Length
                   ? "."
                   : String.Join('.', Labels.Skip(Skip)) + ".";

        internal static String Normalize(String Name)
        {

            var lower = Name.ToLowerInvariant();

            return lower.EndsWith('.')
                       ? lower
                       : lower + ".";

        }

        #endregion

        #region (private) Collect(Proof, Record)

        /// <summary>
        /// Add a record and its signatures to the proof, skipping records already
        /// there. The three parts of a closest-encloser proof frequently resolve
        /// to the same NSEC3, and sending it twice is a malformed response rather
        /// than a stronger one.
        /// </summary>
        private void Collect(List<IDNSResourceRecord>  Proof,
                             IDNSResourceRecord?       Record)
        {

            if (Record is null)
                return;

            if (Proof.Any(existing => existing.Type == Record.Type &&
                                      existing.DomainName.Equals(Record.DomainName)))
                return;

            Proof.Add(Record);
            Proof.AddRange(signaturesOf(Record));

        }

        #endregion

    }

}
