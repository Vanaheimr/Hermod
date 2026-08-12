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
    /// What a set of NSEC or NSEC3 records proves about a name that was not
    /// answered.
    /// </summary>
    public enum DenialOfExistence
    {

        /// <summary>The name provably does not exist (RFC 4035 §5.4, RFC 5155 §8.4).</summary>
        NameDoesNotExist,

        /// <summary>The name exists, and provably holds no record of the queried type.</summary>
        NoDataForType,

        /// <summary>
        /// An opt-out span covers the name (RFC 5155 §6). Nothing is proven either
        /// way — the zone declined to sign this part of the namespace, so an
        /// unsigned delegation may or may not be there.
        /// </summary>
        OptedOut,

        /// <summary>
        /// The records do not prove the denial. A validator must treat the answer
        /// as Bogus rather than believe it.
        /// </summary>
        NotProven

    }


    /// <summary>
    /// Authenticated denial of existence — checking that an NXDOMAIN or NODATA
    /// answer is backed by the records that prove it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the half of DNSSEC that matters for answers which contain nothing.
    /// A signature proves that data came from the zone; only these records prove
    /// that data is *absent*, and without checking them a validator cannot tell a
    /// genuine "no such name" from an attacker who removed the answer.
    /// </para>
    /// <para>
    /// The records are assumed to have been signature-checked already. This
    /// answers a different question: granting that they are authentic, do they
    /// actually prove what the response claims?
    /// </para>
    /// </remarks>
    public static class DenialOfExistenceValidator
    {

        #region (static) Verify(QName, QType, Records)

        /// <summary>
        /// Check whether the given records prove that <paramref name="QName"/>
        /// does not exist, or holds no <paramref name="QType"/>.
        /// </summary>
        /// <param name="QName">The name that was queried.</param>
        /// <param name="QType">The type that was queried.</param>
        /// <param name="Records">The authority-section records of the response.</param>
        public static DenialOfExistence Verify(DomainName                       QName,
                                               DNSResourceRecordTypes           QType,
                                               IEnumerable<IDNSResourceRecord>  Records)
        {

            var records = Records.ToArray();
            var nsec3s  = records.OfType<NSEC3>().ToArray();
            var nsecs   = records.OfType<NSEC>(). ToArray();

            // RFC 5155 §8.2: NSEC3 takes precedence where present. A zone signs
            // with one or the other, never both.
            if (nsec3s.Length > 0)
                return VerifyNSEC3(QName, QType, nsec3s);

            if (nsecs.Length > 0)
                return VerifyNSEC (QName, QType, nsecs);

            return DenialOfExistence.NotProven;

        }

        #endregion


        #region (private static) VerifyNSEC3(QName, QType, Records)

        private static DenialOfExistence VerifyNSEC3(DomainName              QName,
                                                     DNSResourceRecordTypes  QType,
                                                     NSEC3[]                 Records)
        {

            // §8.2: every NSEC3 considered must share one parameter set. Records
            // with different iterations or salt belong to a different chain — mid
            // re-signing, both may be present — and mixing them proves nothing.
            var reference  = Records[0];
            var usable     = Records.Where(nsec3 => nsec3.HashAlgorithm == reference.HashAlgorithm &&
                                                    nsec3.Iterations    == reference.Iterations    &&
                                                    nsec3.Salt.SequenceEqual(reference.Salt)).
                                     ToArray();

            if (!IsSupportedHashAlgorithm(reference.HashAlgorithm))
                return DenialOfExistence.NotProven;

            // §8.5 — NODATA: an NSEC3 that *matches* QNAME, with the queried type
            // absent from its bitmap. This comes first because a matching record
            // settles the question without any closest-encloser reasoning.
            var match = FindMatch(QName, usable, reference);

            if (match is not null)
                return ADNSResourceRecord.TypeBitMapContains(match.TypeBitMaps, QType) ||
                       ADNSResourceRecord.TypeBitMapContains(match.TypeBitMaps, DNSResourceRecordTypes.CNAME)
                           ? DenialOfExistence.NotProven      // the type is there; nothing is being denied
                           : DenialOfExistence.NoDataForType;

            // §8.3 — the closest encloser proof. Walk up from QNAME to find the
            // longest ancestor that has a matching NSEC3; the name one label
            // longer than that ("next closer") must be covered.
            var labels = QName.FullName.TrimEnd('.').Split('.');

            for (var skip = 1; skip <= labels.Length; skip++)
            {

                var candidate = DomainName.ParseLenient(String.Join('.', labels.Skip(skip)) + ".");

                if (FindMatch(candidate, usable, reference) is null)
                    continue;

                // Found the closest encloser.
                var nextCloser  = DomainName.ParseLenient(String.Join('.', labels.Skip(skip - 1)) + ".");
                var covering    = FindCover(nextCloser, usable, reference);

                if (covering is null)
                    return DenialOfExistence.NotProven;

                // §6: an opt-out span makes no promise about the names inside it.
                if ((covering.Flags & 0x01) != 0)
                    return DenialOfExistence.OptedOut;

                // §8.7 — a wildcard at the closest encloser may answer the query,
                // in which case this is NODATA rather than NXDOMAIN.
                var wildcard      = DomainName.ParseLenient("*." + candidate.FullName.TrimStart('.'));
                var wildcardMatch = FindMatch(wildcard, usable, reference);

                if (wildcardMatch is not null)
                    return ADNSResourceRecord.TypeBitMapContains(wildcardMatch.TypeBitMaps, QType)
                               ? DenialOfExistence.NotProven
                               : DenialOfExistence.NoDataForType;

                // §8.4 — NXDOMAIN needs the wildcard proven absent too, otherwise
                // the name could still have been synthesised.
                return FindCover(wildcard, usable, reference) is not null
                           ? DenialOfExistence.NameDoesNotExist
                           : DenialOfExistence.NotProven;

            }

            return DenialOfExistence.NotProven;

        }

        #endregion

        #region (private static) VerifyNSEC (QName, QType, Records)

        private static DenialOfExistence VerifyNSEC(DomainName              QName,
                                                    DNSResourceRecordTypes  QType,
                                                    NSEC[]                  Records)
        {

            // RFC 4035 §5.4 — NODATA: an NSEC whose owner *is* QNAME, with the
            // queried type absent from its bitmap.
            foreach (var nsec in Records)
            {
                if (CompareCanonical(nsec.DomainName.ToString(), QName.FullName) == 0)
                    return ADNSResourceRecord.TypeBitMapContains(nsec.TypeBitMaps, QType) ||
                           ADNSResourceRecord.TypeBitMapContains(nsec.TypeBitMaps, DNSResourceRecordTypes.CNAME)
                               ? DenialOfExistence.NotProven
                               : DenialOfExistence.NoDataForType;
            }

            // §5.4 — NXDOMAIN: one NSEC covering QNAME, and one covering the
            // wildcard that could otherwise have synthesised it.
            var coversName = Records.Any(nsec => Covers(nsec, QName.FullName));

            if (!coversName)
                return DenialOfExistence.NotProven;

            var labels = QName.FullName.TrimEnd('.').Split('.');

            for (var skip = 1; skip <= labels.Length; skip++)
            {

                var wildcard = "*." + String.Join('.', labels.Skip(skip)) + ".";

                if (Records.Any(nsec => Covers(nsec, wildcard)) ||
                    Records.Any(nsec => CompareCanonical(nsec.DomainName.ToString(), wildcard) == 0))
                    return DenialOfExistence.NameDoesNotExist;

            }

            return DenialOfExistence.NotProven;

        }

        #endregion


        #region (private static) FindMatch(Name, Records, Parameters)

        /// <summary>
        /// The NSEC3 whose owner name is the hash of <paramref name="Name"/>
        /// (RFC 5155 §8.3, "matches").
        /// </summary>
        private static NSEC3? FindMatch(DomainName  Name,
                                        NSEC3[]     Records,
                                        NSEC3       Parameters)
        {

            var hash = NSEC3.ComputeHash(Name,
                                         Parameters.Iterations,
                                         Parameters.Salt,
                                         Parameters.HashAlgorithm);

            return Records.FirstOrDefault(nsec3 => OwnerHashOf(nsec3) is Byte[] owner &&
                                                   owner.SequenceEqual(hash));

        }

        #endregion

        #region (private static) FindCover(Name, Records, Parameters)

        /// <summary>
        /// The NSEC3 whose span strictly contains the hash of
        /// <paramref name="Name"/> (RFC 5155 §8.3, "covers").
        /// </summary>
        private static NSEC3? FindCover(DomainName  Name,
                                        NSEC3[]     Records,
                                        NSEC3       Parameters)
        {

            var hash = NSEC3.ComputeHash(Name,
                                         Parameters.Iterations,
                                         Parameters.Salt,
                                         Parameters.HashAlgorithm);

            foreach (var nsec3 in Records)
            {

                var owner = OwnerHashOf(nsec3);

                if (owner is null)
                    continue;

                var next     = nsec3.NextHashedOwnerName;
                var afterLow = CompareHashes(hash, owner) >  0;
                var beforeHi = CompareHashes(hash, next)  <  0;

                // The chain is a ring: its last record wraps past the highest
                // hash back to the lowest, and for that one record the span is
                // everything above the owner *or* below the next.
                var wraps    = CompareHashes(owner, next) >= 0;

                if (wraps ? (afterLow || beforeHi)
                          : (afterLow && beforeHi))
                    return nsec3;

            }

            return null;

        }

        #endregion

        #region (private static) OwnerHashOf(Record)

        /// <summary>
        /// The hash an NSEC3 record's owner name carries in its leftmost label.
        /// </summary>
        private static Byte[]? OwnerHashOf(NSEC3 Record)
        {

            var label = Record.DomainName.ToString().TrimEnd('.').Split('.').FirstOrDefault();

            if (label is null || label.Length == 0)
                return null;

            try
            {
                return NSEC3.Base32HexDecode(label);
            }
            catch (FormatException)
            {
                // Not a hashed owner name at all; it cannot be part of the chain.
                return null;
            }

        }

        #endregion

        #region (private static) CompareHashes(Left, Right)

        private static Int32 CompareHashes(Byte[] Left, Byte[] Right)
        {

            var shared = Math.Min(Left.Length, Right.Length);

            for (var i = 0; i < shared; i++)
                if (Left[i] != Right[i])
                    return Left[i] < Right[i] ? -1 : 1;

            return Left.Length.CompareTo(Right.Length);

        }

        #endregion

        #region (private static) Covers(Record, Name)

        /// <summary>
        /// Whether an NSEC record's span strictly contains the given name.
        /// </summary>
        private static Boolean Covers(NSEC Record, String Name)
        {

            var owner  = Record.DomainName.ToString();
            var next   = Record.NextDomainName.FullName;

            var above  = CompareCanonical(Name, owner) > 0;
            var below  = CompareCanonical(Name, next)  < 0;

            // The last NSEC of a zone points back at the apex, so its span wraps.
            return CompareCanonical(owner, next) >= 0
                       ? above || below
                       : above && below;

        }

        #endregion

        #region (private static) CompareCanonical(Left, Right)

        /// <summary>
        /// Canonical DNS name ordering, RFC 4034 §6.1: compare label by label
        /// from the rightmost, each label as a case-insensitive octet string.
        /// </summary>
        /// <remarks>
        /// This is not string ordering. "z.example." sorts before "a.b.example."
        /// because the comparison starts at the far end, and getting it wrong
        /// makes NSEC spans include names they do not cover — which would let a
        /// forged denial through.
        /// </remarks>
        public static Int32 CompareCanonical(String Left, String Right)
        {

            var leftLabels   = Left. ToLowerInvariant().TrimEnd('.').Split('.');
            var rightLabels  = Right.ToLowerInvariant().TrimEnd('.').Split('.');

            if (Left.TrimEnd('.').Length  == 0) leftLabels  = [];
            if (Right.TrimEnd('.').Length == 0) rightLabels = [];

            var index = 0;

            while (true)
            {

                var leftIndex   = leftLabels. Length - 1 - index;
                var rightIndex  = rightLabels.Length - 1 - index;

                if (leftIndex < 0 && rightIndex < 0)  return 0;
                if (leftIndex < 0)                    return -1;   // a prefix sorts first
                if (rightIndex < 0)                   return  1;

                var comparison = String.CompareOrdinal(leftLabels[leftIndex], rightLabels[rightIndex]);

                if (comparison != 0)
                    return comparison < 0 ? -1 : 1;

                index++;

            }

        }

        #endregion

        #region (private static) IsSupportedHashAlgorithm(HashAlgorithm)

        /// <summary>
        /// Whether the NSEC3 hash algorithm is one this implementation computes.
        /// An unknown algorithm means the chain cannot be walked, and RFC 5155
        /// §8.1 says such records are simply not usable as proof.
        /// </summary>
        private static Boolean IsSupportedHashAlgorithm(Byte HashAlgorithm)

            => HashAlgorithm == NSEC3.HashAlgorithmSHA1;

        #endregion

    }

}
