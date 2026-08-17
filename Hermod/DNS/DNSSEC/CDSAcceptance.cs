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
    /// Why a parent refused to act on a child's CDS RRset (RFC 7344 §4.1).
    /// </summary>
    public enum CDSAcceptanceResult
    {

        /// <summary>
        /// The RRset passed every rule and names the DS RRset the parent should publish.
        /// </summary>
        Accepted,

        /// <summary>
        /// The RRset asks for the DS RRset to be removed (RFC 8078 §4).
        /// </summary>
        AcceptedAsDelete,

        /// <summary>
        /// There was nothing to act on.
        /// </summary>
        NoRecords,

        /// <summary>
        /// The records are not at the child zone's apex.
        /// </summary>
        NotAtApex,

        /// <summary>
        /// Nothing signed the RRset, or nothing that signed it is trusted.
        /// </summary>
        NotSignedByATrustedKey,

        /// <summary>
        /// Applying it would leave the delegation with no usable DS.
        /// </summary>
        WouldBreakTheDelegation,

        /// <summary>
        /// A delete sentinel was mixed with ordinary records (RFC 8078 §4).
        /// </summary>
        InconsistentDeleteSignal

    }


    /// <summary>
    /// The rules a parent applies before it will change a delegation on a
    /// child's say-so (RFC 7344 §4.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// CDS turns the DS RRset into something the child controls, which is the
    /// point — key rollovers stop needing the registrar — and also the danger.
    /// The record is published in the child's own zone, so anyone who can write
    /// there can ask for the parent's DS to be replaced or, with RFC 8078 §4's
    /// sentinel, removed altogether. Removing it turns DNSSEC off for the zone,
    /// quietly and legitimately as far as every validator is concerned.
    /// </para>
    /// <para>
    /// §4.1 is what keeps that from being a takeover: the RRset must be signed
    /// by a key the parent already trusts. So the authority to change the
    /// delegation comes from the current delegation, and an attacker who can
    /// merely write records into the child zone — without holding its keys —
    /// cannot use it.
    /// </para>
    /// <para>
    /// Nothing here fetches anything. The caller supplies what it has already
    /// validated, and this decides; that separation is deliberate, because the
    /// interesting failures are decisions rather than lookups.
    /// </para>
    /// </remarks>
    public static class CDSAcceptance
    {

        #region (static) Evaluate(ChildApex, CDSRecords, Signatures, ChildDNSKEYs, CurrentDS)

        /// <summary>
        /// Decide whether a parent may act on this CDS RRset.
        /// </summary>
        /// <param name="ChildApex">The apex of the child zone the delegation points at.</param>
        /// <param name="CDSRecords">The CDS RRset as published by the child.</param>
        /// <param name="Signatures">The RRSIGs covering that RRset.</param>
        /// <param name="ChildDNSKEYs">The child's current DNSKEY RRset.</param>
        /// <param name="CurrentDS">The DS RRset the parent publishes today.</param>
        public static CDSAcceptanceResult Evaluate(DomainName            ChildApex,
                                                   IEnumerable<CDS>      CDSRecords,
                                                   IEnumerable<RRSIG>    Signatures,
                                                   IEnumerable<DNSKEY>   ChildDNSKEYs,
                                                   IEnumerable<DS>       CurrentDS)
        {

            var records   = CDSRecords. ToArray();
            var keys      = ChildDNSKEYs.ToArray();
            var currentDS = CurrentDS.  ToArray();

            if (records.Length == 0)
                return CDSAcceptanceResult.NoRecords;

            // §4.1: "MUST be at the Child zone apex." A CDS below the apex is not
            // a statement about this delegation, and treating it as one would let
            // any subdomain speak for the zone.
            if (records.Any(record => !String.Equals(record.DomainName.FullName.TrimEnd('.'),
                                                     ChildApex.FullName.TrimEnd('.'),
                                                     StringComparison.OrdinalIgnoreCase)))
            {
                return CDSAcceptanceResult.NotAtApex;
            }

            // RFC 8078 §4: the delete signal is one record and nothing else. A
            // sentinel standing beside ordinary CDS records is a contradiction —
            // install this DS, and also remove them all — and picking one of the
            // two would be arbitrary.
            var sentinels = records.Count(record => record.IsDeleteSentinel);

            if (sentinels > 0 && records.Length != 1)
                return CDSAcceptanceResult.InconsistentDeleteSignal;

            // §4.1: "MUST be signed with a key that is represented in both the
            // current DNSKEY and DS RRsets."
            //
            // Both, and that is the load-bearing word. A key in the DNSKEY RRset
            // alone proves only that whoever published the zone published it —
            // which an attacker who has taken over the zone has also done. The
            // parent's own DS RRset is the part they cannot have written, so it
            // is what the permission has to come from.
            if (!IsSignedByATrustedKey(Signatures, keys, currentDS))
                return CDSAcceptanceResult.NotSignedByATrustedKey;

            if (sentinels == 1)
                return CDSAcceptanceResult.AcceptedAsDelete;

            // §4.1: "MUST NOT break the current delegation if applied to DS
            // RRset." A CDS RRset naming only algorithms or digests nobody can
            // follow would leave the delegation looking signed and being
            // unverifiable — worse than either signed or unsigned, because
            // validators answer SERVFAIL rather than resolving.
            if (!records.Any(record => DNSSECValidator.IsUsableDelegationSigner(record.Algorithm,
                                                                                record.DigestType)))
            {
                return CDSAcceptanceResult.WouldBreakTheDelegation;
            }

            return CDSAcceptanceResult.Accepted;

        }

        #endregion

        #region (private static) IsSignedByATrustedKey(Signatures, ChildDNSKEYs, CurrentDS)

        /// <summary>
        /// Whether one of the signatures was made by a key that is both in the
        /// child's DNSKEY RRset and named by the parent's current DS RRset.
        /// </summary>
        private static Boolean IsSignedByATrustedKey(IEnumerable<RRSIG>  Signatures,
                                                     DNSKEY[]            ChildDNSKEYs,
                                                     DS[]                CurrentDS)
        {

            foreach (var signature in Signatures.Where(signature => signature.TypeCovered == DNSResourceRecordTypes.CDS))
            {

                var signingKey = ChildDNSKEYs.FirstOrDefault(
                                     key => key.Algorithm == signature.Algorithm &&
                                            DNSSECValidator.ComputeKeyTag(key) == signature.KeyTag
                                 );

                if (signingKey is null)
                    continue;

                // The DS is matched by recomputing its digest rather than by
                // comparing key tags: a key tag is a checksum, not an identifier,
                // and two different keys sharing one is ordinary enough that
                // RFC 4034 App. B warns about it.
                if (CurrentDS.Any(ds => DNSSECValidator.VerifyDS(signingKey, ds)))
                    return true;

            }

            return false;

        }

        #endregion

    }

}
