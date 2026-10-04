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

using System.Text;
using System.Buffers.Binary;
using System.Security.Cryptography;

using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;


#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// DNSSEC validation results.
    /// </summary>
    public enum DNSSECValidationResult
    {

        /// <summary>
        /// Signature verified, chain of trust intact.
        /// </summary>
        Secure,

        /// <summary>
        /// No DNSSEC signatures present.
        /// </summary>
        Insecure,

        /// <summary>
        /// Signature verification failed.
        /// </summary>
        Bogus,

        /// <summary>
        /// Validation could not be completed (e.g. network error fetching keys).
        /// </summary>
        Indeterminate

    }


    /// <summary>
    /// DNSSEC chain-of-trust validator (RFC 4033/4034/4035).
    /// Validates RRSIG signatures against DNSKEY records and verifies
    /// the delegation chain up to a configured trust anchor.
    /// </summary>
    public class DNSSECValidator
    {

        #region Data

        /// <summary>
        /// Trust anchors (root DNSKEY DS records).
        /// </summary>
        private readonly List<DS> trustAnchors;

        /// <summary>
        /// DNS client for fetching DNSKEY/DS records during chain walk.
        /// </summary>
        private readonly IDNSClient dnsClient;

        /// <summary>
        /// The IANA root KSK DS record (Key Tag 20326, Algorithm 8, SHA-256).
        /// </summary>
        private static readonly DS RootTrustAnchor = new(
            DomainName.Parse("."),
            DNSQueryClasses.IN,
            TimeSpan.FromDays(36500),
            20326,
            8,
            2,
            Convert.FromHexString("E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D")
        );

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new DNSSEC validator.
        /// </summary>
        /// <param name="DNSClient">A DNS client for fetching DNSKEY/DS records.</param>
        /// <param name="TrustAnchors">Optional trust anchors. If null, no trust anchors are configured (use WithRootTrustAnchor for the IANA root).</param>
        public DNSSECValidator(IDNSClient        DNSClient,
                               IEnumerable<DS>?  TrustAnchors   = null)
        {

            this.dnsClient    = DNSClient    ?? throw new ArgumentNullException(nameof(DNSClient));
            this.trustAnchors = TrustAnchors is not null
                                    ? [.. TrustAnchors]
                                    : [];

        }

        #endregion


        #region WithRootTrustAnchor(DNSClient)

        /// <summary>
        /// Create a new DNSSEC validator with the IANA root trust anchor pre-configured.
        /// </summary>
        /// <param name="DNSClient">A DNS client for fetching DNSKEY/DS records.</param>
        public static DNSSECValidator WithRootTrustAnchor(IDNSClient DNSClient)

            => new(DNSClient, [RootTrustAnchor]);

        #endregion


        #region Trust Anchor Management (RFC 5011)

        /// <summary>
        /// Pending trust anchors that have been seen but not yet accepted.
        /// RFC 5011 requires a "hold-down time" of 30 days before accepting
        /// a new trust anchor to prevent attackers from introducing rogue anchors.
        /// Key: (KeyTag, Algorithm), Value: (DS record, first-seen timestamp).
        /// </summary>
        private readonly Dictionary<(UInt16 KeyTag, Byte Algorithm), (DS Anchor, DateTimeOffset FirstSeen)> pendingAnchors = [];

        /// <summary>
        /// Trust anchors that have been revoked but are kept to recognize
        /// the revocation.  RFC 5011 Section 2.1: revoked keys stay in the
        /// set until they expire.
        /// Key: (KeyTag, Algorithm).
        /// </summary>
        private readonly HashSet<(UInt16 KeyTag, Byte Algorithm)> revokedAnchors = [];

        /// <summary>
        /// The RFC 5011 add hold-down time (30 days).
        /// A new trust anchor must be continuously seen for this duration
        /// before it is accepted.
        /// </summary>
        /// <summary>
        /// The DNSKEY REVOKE flag (RFC 5011 §2.1). Note that it lives in the Flags
        /// field, which is part of the RDATA the key tag is computed over — so a
        /// revoked key does not have the same key tag as the key it revokes.
        /// </summary>
        private const UInt16 RevokeFlag = 0x0080;

        /// <summary>
        /// The DNSKEY Zone Key flag (RFC 4034 §2.1.1). A key without it "MUST NOT
        /// be used to verify RRSIGs that cover RRsets".
        /// </summary>
        private const UInt16 ZoneKeyFlag = 0x0100;

        public static readonly TimeSpan AddHoldDownTime = TimeSpan.FromDays(30);

        /// <summary>
        /// The RFC 5011 remove hold-down time (30 days).
        /// A revoked trust anchor is kept for this duration before removal.
        /// </summary>
        public static readonly TimeSpan RemoveHoldDownTime = TimeSpan.FromDays(30);

        /// <summary>
        /// Probe the root zone for new or revoked trust anchors (RFC 5011).
        /// This method fetches the current root DNSKEY RRSet, validates it
        /// against the existing trust anchors, and processes any key changes:
        ///
        /// - New KSKs (SEP bit set) are added to pendingAnchors with a 30-day hold-down.
        /// - If a pending anchor has been continuously seen for 30+ days, it is promoted.
        /// - Revoked KSKs (revoke bit set, bit 8 = 0x0080) are moved to revokedAnchors.
        ///
        /// Call this periodically (e.g. daily) to keep trust anchors current.
        /// </summary>
        /// <param name="Now">
        /// The time to measure RFC 5011 §2.4.1's add hold-down against; the current
        /// time when omitted. One reading serves the whole probe, both for the keys
        /// whose hold-down is being checked and for the ones whose hold-down starts
        /// here — otherwise the two ends of a thirty-day interval are taken from two
        /// different instants, and no caller can name either of them.
        /// </param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <returns>True if the trust anchor set was modified.</returns>
        public async Task<Boolean> ProbeForTrustAnchorUpdatesAsync(DateTimeOffset?    Now                = null,
                                                                   CancellationToken  CancellationToken  = default)
        {

            var now = Now ?? Timestamp.Now;

            try
            {

                // Fetch the root DNSKEY RRSet
                var response = await dnsClient.Query(
                                         DomainName.Parse("."),
                                         [DNSResourceRecordTypes.DNSKEY],
                                         CancellationToken: CancellationToken
                                     ).ConfigureAwait(false);

                if (!response.IsValid)
                    return false;

                var dnskeys  = response.Answers.OfType<DNSKEY>().ToList();
                var modified = false;

                foreach (var key in dnskeys)
                {

                    var keyTag    = ComputeKeyTag(key);
                    var keyId     = (keyTag, key.Algorithm);
                    var isSEP     = (key.Flags & 0x0001) == 1;   // Secure Entry Point (KSK)
                    var isRevoked = (key.Flags & RevokeFlag) != 0;   // RFC 5011 §2.1

                    // Process revocations
                    if (isRevoked && isSEP)
                    {

                        // RFC 5011 §2.1: this anchor was stored while the REVOKE bit was
                        // clear. The key tag is a checksum over the whole DNSKEY RDATA,
                        // Flags included, so setting REVOKE changes it — matching on the
                        // revoked key's current tag can never find the stored anchor, and
                        // the revocation would be silently ignored while the compromised
                        // key stayed trusted. Compare against the tag the key had before
                        // it was revoked.
                        var liveKeyTag = ComputeKeyTag(
                                             (UInt16) (key.Flags & ~RevokeFlag),
                                             key.Protocol,
                                             key.Algorithm,
                                             key.PublicKey
                                         );

                        var liveKeyId  = (liveKeyTag, key.Algorithm);

                        // Remove from active trust anchors
                        var removed = trustAnchors.RemoveAll(
                                          a => (a.KeyTag == liveKeyTag || a.KeyTag == keyTag) &&
                                                a.Algorithm == key.Algorithm
                                      );

                        if (removed > 0)
                            modified = true;

                        // Remember it under both identities, so the key cannot be
                        // re-admitted when it is next published without the REVOKE bit.
                        revokedAnchors.Add(liveKeyId);
                        revokedAnchors.Add(keyId);

                        // Also remove from pending
                        pendingAnchors.Remove(liveKeyId);
                        pendingAnchors.Remove(keyId);

                        continue;

                    }

                    // Process new KSKs (SEP bit set, not revoked)
                    if (isSEP)
                    {

                        // Check if this key is already a trust anchor
                        var isExisting = trustAnchors.Any(
                                             a => a.KeyTag    == keyTag &&
                                                  a.Algorithm == key.Algorithm
                                         );

                        if (!isExisting && !revokedAnchors.Contains(keyId))
                        {

                            // Build a DS record for this key using SHA-256 (digest type 2)
                            var ownerNameWire = SerializeCanonicalName(key.DomainName.FullName);
                            var dnskeyRData   = new Byte[4 + key.PublicKey.Length];
                            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(dnskeyRData.AsSpan(0), key.Flags);
                            dnskeyRData[2] = key.Protocol;
                            dnskeyRData[3] = key.Algorithm;
                            Array.Copy(key.PublicKey, 0, dnskeyRData, 4, key.PublicKey.Length);

                            var dataToHash = new Byte[ownerNameWire.Length + dnskeyRData.Length];
                            Array.Copy(ownerNameWire, 0, dataToHash, 0, ownerNameWire.Length);
                            Array.Copy(dnskeyRData, 0, dataToHash, ownerNameWire.Length, dnskeyRData.Length);

                            var digest = System.Security.Cryptography.SHA256.HashData(dataToHash);

                            var ds = new DS(
                                         DomainName.Parse("."),
                                         DNSQueryClasses.IN,
                                         TimeSpan.FromDays(36500),
                                         keyTag,
                                         key.Algorithm,
                                         2,  // SHA-256
                                         digest
                                     );

                            if (pendingAnchors.TryGetValue(keyId, out var pending))
                            {
                                // Already pending — check if hold-down time has elapsed
                                if (now - pending.FirstSeen >= AddHoldDownTime)
                                {
                                    trustAnchors.Add(ds);
                                    pendingAnchors.Remove(keyId);
                                    modified = true;
                                }
                                // else: still in hold-down period, keep waiting
                            }
                            else
                            {
                                // First time seeing this key — start the hold-down timer
                                pendingAnchors[keyId] = (ds, now);
                            }

                        }

                    }

                }

                // Remove pending anchors that were NOT seen in this probe
                // (RFC 5011: the key must be continuously present during hold-down)
                var seenKeyIds = dnskeys.Select(k => (ComputeKeyTag(k), k.Algorithm)).ToHashSet();
                var toRemove   = pendingAnchors.Keys.Where(k => !seenKeyIds.Contains(k)).ToList();

                foreach (var key in toRemove)
                    pendingAnchors.Remove(key);

                return modified;

            }
            catch
            {
                return false;
            }

        }

        /// <summary>
        /// Get the current set of active trust anchors.
        /// </summary>
        public IReadOnlyList<DS> TrustAnchors
            => trustAnchors.AsReadOnly();

        /// <summary>
        /// Get the current set of pending trust anchors (awaiting hold-down completion).
        /// </summary>
        public IReadOnlyDictionary<(UInt16 KeyTag, Byte Algorithm), (DS Anchor, DateTimeOffset FirstSeen)> PendingAnchors
            => pendingAnchors;

        /// <summary>
        /// Manually add a trust anchor.
        /// </summary>
        /// <param name="TrustAnchor">The DS record to add as a trust anchor.</param>
        public void AddTrustAnchor(DS TrustAnchor)
            => trustAnchors.Add(TrustAnchor);

        /// <summary>
        /// Remove a trust anchor by key tag and algorithm.
        /// </summary>
        /// <param name="KeyTag">The key tag to remove.</param>
        /// <param name="Algorithm">The algorithm to match.</param>
        /// <returns>True if a trust anchor was removed.</returns>
        public Boolean RemoveTrustAnchor(UInt16 KeyTag, Byte Algorithm)
            => trustAnchors.RemoveAll(a => a.KeyTag == KeyTag && a.Algorithm == Algorithm) > 0;

        #endregion


        #region ValidateAsync(Response, CancellationToken = default)

        /// <summary>
        /// Validate a DNS response by verifying the RRSIG signatures
        /// and walking the chain of trust up to a configured trust anchor.
        /// </summary>
        /// <remarks>
        /// Implements the DNSSEC validation procedure defined in RFC 4033 (DNS Security Introduction),
        /// RFC 4034 (Resource Records for the DNS Security Extensions), and
        /// RFC 4035 (Protocol Modifications for the DNS Security Extensions).
        /// </remarks>
        /// <param name="Response">The DNS response to validate.</param>
        /// <param name="Now">The time to check RFC 4034 §3.1.5's validity windows against; the current time when omitted.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<DNSSECValidationResult> ValidateAsync(DNSInfo            Response,
                                                                DateTimeOffset?    Now                = null,
                                                                CancellationToken  CancellationToken  = default)

            => await ValidateAsync(Response, null, Now, CancellationToken).ConfigureAwait(false);


        /// <summary>
        /// Validate a response, including the authenticated denial of existence
        /// when the answer section is empty.
        /// </summary>
        /// <param name="Response">The response to validate.</param>
        /// <param name="Question">What was asked. Without it a negative answer cannot be checked, because the proof is a statement about a specific name and type.</param>
        /// <param name="Now">
        /// The time to check RFC 4034 §3.1.5's validity windows against; the
        /// current time when omitted. A signature does not know what time it is,
        /// so the window is a separate check from the cryptography — and one that
        /// cannot be exercised at its own boundary without saying when "now" is.
        /// <see cref="TSIGSigner.Verify"/> and <see cref="SIG0Signer.Verify"/>
        /// take the same parameter for the same reason.
        /// </param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public async Task<DNSSECValidationResult> ValidateAsync(DNSInfo                                                     Response,
                                                                (DomainName QName, DNSResourceRecordTypes QType)?           Question,
                                                                DateTimeOffset?                                             Now                = null,
                                                                CancellationToken                                           CancellationToken  = default)
        {

            // One reading of the clock for the answer and for every signature on
            // the way up to the anchor.
            var now = Now ?? Timestamp.Now;

            try
            {

                // Extract RRSIG records from the answer section
                var rrsigRecords = Response.Answers.OfType<RRSIG>().ToList();

                if (rrsigRecords.Count == 0)
                {

                    // An answer section with no signatures is not necessarily an
                    // unsigned zone: a negative answer has an empty answer
                    // section by definition, and carries its proof in the
                    // authority section instead. Reporting Insecure here without
                    // looking would accept every NXDOMAIN ever forged.
                    if (Question is not null &&
                        Response.Authorities.Any(rr => rr is NSEC or NSEC3))
                        return await ValidateDenialAsync(
                                         Response,
                                         Question.Value.QName,
                                         Question.Value.QType,
                                         now,
                                         CancellationToken
                                     ).ConfigureAwait(false);

                    // No proof at all. Whether that is acceptable depends on
                    // whether the zone was supposed to have one: under a
                    // configured trust anchor it was, and a negative answer
                    // arriving without it is exactly what stripping the records
                    // produces. Outside any anchor there is nothing to expect,
                    // and the answer is merely unsigned.
                    if (Question is not null &&
                        CoveredByATrustAnchor(Question.Value.QName))
                        return DNSSECValidationResult.Bogus;

                    return DNSSECValidationResult.Insecure;

                }

                // Group answers by type (excluding RRSIG itself) to form RRSets
                var answerRecords = Response.Answers
                                            .OfType<ADNSResourceRecord>()
                                            .Where(rr => rr.Type != DNSResourceRecordTypes.RRSIG)
                                            .ToList();

                // For each RRSIG, find the matching RRSet and validate
                foreach (var rrsig in rrsigRecords)
                {

                    // Find the RRSet covered by this RRSIG
                    var rrSet = answerRecords
                                    .Where(rr => rr.Type == rrsig.TypeCovered &&
                                                 rr.DomainName.FullName.Equals(rrsig.DomainName.FullName, StringComparison.OrdinalIgnoreCase))
                                    .Cast<IDNSResourceRecord>()
                                    .ToList();

                    if (rrSet.Count == 0)
                        continue;

                    // Check signature timestamps
                    if (!WithinValidityWindow(rrsig, now))
                        return DNSSECValidationResult.Bogus;

                    // Fetch the DNSKEY for the signer zone
                    var dnskeyResponse = await dnsClient.Query(
                                                  DomainName.Parse(rrsig.SignerName.FullName),
                                                  [DNSResourceRecordTypes.DNSKEY],
                                                  CancellationToken: CancellationToken
                                              ).ConfigureAwait(false);

                    if (!dnskeyResponse.IsValid)
                        return DNSSECValidationResult.Indeterminate;

                    // Verify the RRSIG with a key of the signer's apex DNSKEY RRset —
                    // the RRset the walk below authenticates, and no other key.
                    if (!VerifiedByOneOf(rrSet, rrsig, ApexKeys(dnskeyResponse, rrsig.SignerName.FullName)))
                        return DNSSECValidationResult.Bogus;

                    // Walk the chain of trust upward
                    var chainResult = await WalkChainOfTrust(
                                               rrsig.SignerName,
                                               dnskeyResponse,
                                               now,
                                               CancellationToken
                                           ).ConfigureAwait(false);

                    if (chainResult != DNSSECValidationResult.Secure)
                        return chainResult;

                }

                return DNSSECValidationResult.Secure;

            }
            catch (Exception)
            {
                return DNSSECValidationResult.Indeterminate;
            }

        }

        #endregion

        #region (private) CoveredByATrustAnchor(Name)

        /// <summary>
        /// Whether any configured trust anchor sits at or above the given name,
        /// which is what makes a missing proof a defect rather than a fact about
        /// an unsigned zone.
        /// </summary>
        private Boolean CoveredByATrustAnchor(DomainName Name)
        {

            var name = Name.FullName.TrimEnd('.');

            return trustAnchors.Any(anchor => {

                var anchorName = anchor.DomainName.ToString().TrimEnd('.');

                // The root anchor covers everything.
                return anchorName.Length == 0 ||
                       name.Equals(anchorName, StringComparison.OrdinalIgnoreCase) ||
                       name.EndsWith("." + anchorName, StringComparison.OrdinalIgnoreCase);

            });

        }

        #endregion

        #region (private) ValidateDenialAsync(Response, QName, QType, CancellationToken)

        /// <summary>
        /// Validate a negative answer: check that the NSEC or NSEC3 records in
        /// the authority section are authentic, and that they actually prove
        /// what the response claims.
        /// </summary>
        /// <remarks>
        /// Both halves are required and neither is sufficient. A signature check
        /// alone proves the records came from the zone, which an attacker can
        /// satisfy by replaying genuine records from elsewhere in the namespace;
        /// a proof check alone proves the records fit the claim, which an
        /// attacker can satisfy by forging them. Only together do they say "the
        /// zone asserted that this name is absent".
        /// </remarks>
        private async Task<DNSSECValidationResult> ValidateDenialAsync(DNSInfo                 Response,
                                                                       DomainName              QName,
                                                                       DNSResourceRecordTypes  QType,
                                                                       DateTimeOffset          Now,
                                                                       CancellationToken       CancellationToken)
        {

            var authorities  = Response.Authorities.ToList();
            var denialSigs   = authorities.OfType<RRSIG>().
                                           Where(rrsig => rrsig.TypeCovered is DNSResourceRecordTypes.NSEC
                                                                            or DNSResourceRecordTypes.NSEC3).
                                           ToList();

            if (denialSigs.Count == 0)
                return DNSSECValidationResult.Bogus;

            foreach (var rrsig in denialSigs)
            {

                var rrSet = authorities.OfType<ADNSResourceRecord>().
                                        Where(rr => rr.Type == rrsig.TypeCovered &&
                                                    rr.DomainName.FullName.Equals(rrsig.DomainName.FullName,
                                                                                  StringComparison.OrdinalIgnoreCase)).
                                        Cast<IDNSResourceRecord>().
                                        ToList();

                if (rrSet.Count == 0)
                    return DNSSECValidationResult.Bogus;

                if (!WithinValidityWindow(rrsig, Now))
                    return DNSSECValidationResult.Bogus;

                var dnskeyResponse = await dnsClient.Query(
                                               DomainName.Parse(rrsig.SignerName.FullName),
                                               [DNSResourceRecordTypes.DNSKEY],
                                               CancellationToken: CancellationToken
                                           ).ConfigureAwait(false);

                if (!dnskeyResponse.IsValid)
                    return DNSSECValidationResult.Indeterminate;

                if (!VerifiedByOneOf(rrSet, rrsig, ApexKeys(dnskeyResponse, rrsig.SignerName.FullName)))
                    return DNSSECValidationResult.Bogus;

                var chainResult = await WalkChainOfTrust(
                                           rrsig.SignerName,
                                           dnskeyResponse,
                                           Now,
                                           CancellationToken
                                       ).ConfigureAwait(false);

                if (chainResult != DNSSECValidationResult.Secure)
                    return chainResult;

            }

            // The records are authentic. Now: do they prove the claim?
            return DenialOfExistenceValidator.Verify(QName, QType, authorities) switch {

                DenialOfExistence.NameDoesNotExist  => DNSSECValidationResult.Secure,
                DenialOfExistence.NoDataForType     => DNSSECValidationResult.Secure,

                // RFC 5155 §6: inside an opt-out span the zone made no promise,
                // so the absence is unsigned rather than proven — Insecure, not
                // Bogus. Treating it as Bogus would break every opted-out TLD.
                DenialOfExistence.OptedOut          => DNSSECValidationResult.Insecure,

                _                                   => DNSSECValidationResult.Bogus

            };

        }

        #endregion

        #region ValidateRRSig(RRSet, Signature, Key)

        /// <summary>
        /// Validate a specific RRSet against its RRSIG signature using the provided DNSKEY.
        /// </summary>
        /// <remarks>
        /// Implements RRSIG verification as specified in RFC 4034 Section 3.
        /// </remarks>
        /// <param name="RRSet">The set of resource records to validate.</param>
        /// <param name="Signature">The RRSIG covering the RRSet.</param>
        /// <param name="Key">The DNSKEY used to verify the signature.</param>
        public DNSSECValidationResult ValidateRRSig(IEnumerable<IDNSResourceRecord>  RRSet,
                                                    RRSIG                            Signature,
                                                    DNSKEY                           Key)
        {

            try
            {

                // Build the signed data: RRSIG RDATA (without signature) + canonical sorted RRSet
                var signedData = DNSSECCanonical.SignedData(RRSet, Signature);

                // Verify the cryptographic signature
                var verified = VerifySignature(
                                   Signature.Algorithm,
                                   Key.PublicKey,
                                   signedData,
                                   Signature.Signature
                               );

                return verified
                           ? DNSSECValidationResult.Secure
                           : DNSSECValidationResult.Bogus;

            }
            catch (Exception)
            {
                return DNSSECValidationResult.Indeterminate;
            }

        }

        #endregion

        #region VerifyDS(Key, DelegationSigner)

        /// <summary>
        /// Verify that a DNSKEY matches a DS (Delegation Signer) record
        /// by computing the digest over the canonical owner name + DNSKEY RDATA.
        /// </summary>
        /// <remarks>
        /// Implements DS record verification as specified in RFC 4034 Section 5.
        /// </remarks>
        /// <param name="Key">The DNSKEY to verify.</param>
        /// <param name="DelegationSigner">The DS record to match against.</param>
        /// <summary>
        /// Whether this build can follow a delegation signed with this algorithm
        /// and digested this way.
        /// </summary>
        /// <param name="Algorithm">The DNSKEY algorithm the DS names.</param>
        /// <param name="DigestType">The digest algorithm the DS uses.</param>
        /// <remarks>
        /// Takes the two fields rather than a record, because CDS asks the same
        /// question about a record that is not a DS — RFC 7344 §3.1 makes the two
        /// identical in wire format and different in meaning, and turning one
        /// into the other just to ask would be a conversion in the wrong
        /// direction.
        /// </remarks>
        public static Boolean IsUsableDelegationSigner(Byte  Algorithm,
                                                       Byte  DigestType)

            => DigestType is 1 or 2 or 4 &&
               Algorithm  is 5 or 7 or 8 or 10 or 13 or 14 or 15 or 16;


        /// <inheritdoc cref="IsUsableDelegationSigner(Byte, Byte)"/>
        /// <param name="DelegationSigner">A DS record.</param>
        public static Boolean IsUsableDelegationSigner(DS DelegationSigner)

            => IsUsableDelegationSigner(DelegationSigner.Algorithm,
                                        DelegationSigner.DigestType);


        /// <summary>
        /// Whether any DS in this RRset names both a signature algorithm and a
        /// digest algorithm this build can actually use.
        /// </summary>
        /// <param name="DelegationSigners">The parent's DS RRset for the child.</param>
        /// <remarks>
        /// <para>
        /// RFC 6840 §5.2 is the rule, and it is about what a validator concludes
        /// rather than what it computes:
        /// </para>
        /// <para>
        /// "when determining the security status of a zone, a validator
        /// disregards any authenticated DS records that specify unknown or
        /// unsupported DNSKEY algorithms. If none are left, the zone is treated
        /// as if it were unsigned" — and §5.2 extends the same treatment to
        /// unsupported *digest* algorithms.
        /// </para>
        /// <para>
        /// Unsigned, not broken. The difference is the whole point: a delegation
        /// this validator cannot follow is one it has no opinion about, and
        /// reporting Bogus instead turns "I cannot check this" into "this is
        /// forged" — which fails the name outright for every client behind the
        /// validator, over a zone that is very likely perfectly fine and merely
        /// newer than the code reading it.
        /// </para>
        /// </remarks>
        public static Boolean HasUsableDelegationSigner(IEnumerable<DS> DelegationSigners)

            => DelegationSigners.Any(IsUsableDelegationSigner);


        /// <summary>
        /// Verify that a DNSKEY matches a DS record.
        /// </summary>
        /// <param name="Key">The DNSKEY to verify.</param>
        /// <param name="DelegationSigner">The DS record to match against.</param>
        public static Boolean VerifyDS(DNSKEY  Key,
                                       DS      DelegationSigner)
        {

            // Build the data to hash: canonical owner name wire format + DNSKEY RDATA
            // (Flags + Protocol + Algorithm + PublicKey)
            var ownerNameWire  = SerializeCanonicalName(Key.DomainName.FullName);
            var dnskeyRData    = new Byte[4 + Key.PublicKey.Length];

            BinaryPrimitives.WriteUInt16BigEndian(dnskeyRData.AsSpan(0), Key.Flags);
            dnskeyRData[2] = Key.Protocol;
            dnskeyRData[3] = Key.Algorithm;
            Array.Copy(Key.PublicKey, 0, dnskeyRData, 4, Key.PublicKey.Length);

            var dataToHash = new Byte[ownerNameWire.Length + dnskeyRData.Length];
            Array.Copy(ownerNameWire, 0, dataToHash, 0,                   ownerNameWire.Length);
            Array.Copy(dnskeyRData,   0, dataToHash, ownerNameWire.Length, dnskeyRData.Length);

            // Compute the digest using the algorithm specified in the DS record
            var computedDigest = DelegationSigner.DigestType switch {
                1 => SHA1.HashData(dataToHash),
                2 => SHA256.HashData(dataToHash),
                4 => SHA384.HashData(dataToHash),
                _ => null
            };

            if (computedDigest is null)
                return false;

            return computedDigest.AsSpan().SequenceEqual(DelegationSigner.Digest);

        }

        #endregion

        #region ComputeKeyTag(Key)

        /// <summary>
        /// Compute the Key Tag for a DNSKEY record (RFC 4034 Appendix B).
        /// </summary>
        /// <remarks>
        /// Implements the key tag computation algorithm defined in RFC 4034 Appendix B.
        /// </remarks>
        /// <param name="Key">The DNSKEY record.</param>
        public static UInt16 ComputeKeyTag(DNSKEY Key)

            => ComputeKeyTag(Key.Flags,
                             Key.Protocol,
                             Key.Algorithm,
                             Key.PublicKey);


        /// <summary>
        /// Compute the Key Tag from the DNSKEY RDATA fields (RFC 4034 Appendix B).
        /// </summary>
        /// <remarks>
        /// Taking the fields rather than the record makes it possible to ask what
        /// the tag *would* be under different flags — which RFC 5011 revocation
        /// handling needs, because setting the REVOKE bit changes the tag — and to
        /// tag a KEY record, whose RDATA has the same shape and whose tag SIG(0)
        /// needs (RFC 2931 §3).
        /// </remarks>
        public static UInt16 ComputeKeyTag(UInt16  Flags,
                                           Byte    Protocol,
                                           Byte    Algorithm,
                                           Byte[]  PublicKey)
        {

            // DNSKEY RDATA: Flags (2 bytes) + Protocol (1 byte) + Algorithm (1 byte) + PublicKey
            var rdataLength = 4 + PublicKey.Length;
            var rdata       = new Byte[rdataLength];

            BinaryPrimitives.WriteUInt16BigEndian(rdata.AsSpan(0), Flags);
            rdata[2] = Protocol;
            rdata[3] = Algorithm;
            Array.Copy(PublicKey, 0, rdata, 4, PublicKey.Length);

            // RFC 4034 Appendix B algorithm
            UInt32 ac = 0;

            for (var i = 0; i < rdata.Length; i++)
            {
                if ((i & 1) == 0)
                    ac += (UInt32) rdata[i] << 8;
                else
                    ac += rdata[i];
            }

            ac += (ac >> 16) & 0xFFFF;

            return (UInt16) (ac & 0xFFFF);

        }

        #endregion


        #region (private) WalkChainOfTrust(SignerName, DNSKeyResponse, Now, CancellationToken)

        /// <summary>
        /// Walk the chain of trust from the signer zone up to a trust anchor.
        /// </summary>
        /// <param name="SignerName">The zone whose DNSKEY RRset signed the data being validated.</param>
        /// <param name="DNSKeyResponse">The response the signer's DNSKEY RRset was taken from, its RRSIGs included.</param>
        /// <param name="Now">The time every signature on the way up is checked against.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <remarks>
        /// <para>
        /// Every link of the chain is a signature, and every one of them is
        /// checked. RFC 4035 §5.2: the child's DNSKEY RRset is authenticated by
        /// "an RRSIG RR ... that covers the child zone's DNSKEY RRset" made with a
        /// key that an authenticated DS names, and the DS RRset is authenticated
        /// in the parent — by the parent's keys, which are authenticated the same
        /// way one step further up, until a trust anchor stands in for the DS.
        /// </para>
        /// <para>
        /// This used to check that the DS digests matched a key and that the
        /// DNSKEY RRsets carried an RRSIG whose key tag named one of their keys —
        /// and never verified any of those signatures. Nothing above the answer
        /// was authenticated: an attacker who generated a key per zone, published
        /// unsigned DS records for them and an RRSIG of zero bytes over each
        /// DNSKEY RRset made every name Secure, and against the real root the
        /// root's own DNSKEY response can simply be replayed.
        /// </para>
        /// <para>
        /// The walk goes bottom-up, and a link is accepted conditionally on the
        /// one above it: a DNSKEY RRset is accepted if a DS names its signer, the
        /// DS if the parent's DNSKEY RRset signed it, and so on until an anchor
        /// ends it. Any link that fails fails the whole chain.
        /// </para>
        /// </remarks>
        private async Task<DNSSECValidationResult> WalkChainOfTrust(DomainName         SignerName,
                                                                    DNSInfo            DNSKeyResponse,
                                                                    DateTimeOffset     Now,
                                                                    CancellationToken  CancellationToken)
        {

            var currentZone         = Normalize(SignerName.FullName);
            var currentKeyResponse  = DNSKeyResponse;
            var keyResponses        = new Dictionary<String, DNSInfo>() { [currentZone] = DNSKeyResponse };

            // Limit chain walk depth to prevent infinite loops
            for (var depth = 0; depth < 20; depth++)
            {

                var currentKeys     = ApexKeys(currentKeyResponse, currentZone);
                var currentKeySigs  = Signatures(currentKeyResponse, currentZone, DNSResourceRecordTypes.DNSKEY).
                                          Where(rrsig => Normalize(rrsig.SignerName.FullName) == currentZone).
                                          ToList();

                // Which of the zone's keys an anchor or a DS names is decided by key
                // tag, algorithm and digest, and by nothing else. RFC 4035 §5.2 asks
                // for "a DNSKEY RR in the child zone's apex DNSKEY RRset" — any of
                // them — and RFC 4034 §2.1.1 rules out the one shortcut that suggests
                // itself: the SEP bit "is only intended to be a hint to zone signing
                // or debugging software", and "validators MUST NOT alter their
                // behavior during the signature validation process in any way based
                // on the setting of this bit".
                //
                // This used to pick "the" key-signing key as the first published key
                // with the SEP bit and the algorithm carried up, and test the anchor
                // and the DS against that one key. A zone publishes two SEP keys for
                // as long as a KSK rollover lasts, and whenever the standby key came
                // first every name under the zone was Bogus — org. did, with keys 725
                // and 26974 and the root's DS naming 26974.
                //
                // A key an anchor names is not yet a reason to trust the RRset it
                // was found in: anyone can copy the root's public key into a DNSKEY
                // response. The RRset is authenticated only once that key's
                // signature over it verifies.
                if (trustAnchors.Any(anchor => currentKeys.Any(key => Names(anchor, key))))
                    return KeySetSignedByAKeyNamedBy(currentKeys, currentKeySigs, trustAnchors, Now)
                               ? DNSSECValidationResult.Secure
                               : DNSSECValidationResult.Bogus;

                // Above the root there is nothing to ask, and no anchor was met.
                if (GetParentZone(currentZone) is null)
                    return DNSSECValidationResult.Bogus;

                // Fetch the DS record for the current zone from the parent
                var dsResponse = await dnsClient.Query(
                                          DomainName.Parse(currentZone + "."),
                                          [DNSResourceRecordTypes.DS],
                                          CancellationToken: CancellationToken
                                      ).ConfigureAwait(false);

                if (!dsResponse.IsValid)
                    return DNSSECValidationResult.Indeterminate;

                var dsRecords = dsResponse.Answers.OfType<DS>().
                                    Where(ds => Normalize(ds.DomainName.FullName) == currentZone).
                                    ToList();

                if (dsRecords.Count == 0)
                    return DNSSECValidationResult.Insecure;

                // The DS RRset is the parent's data, signed with the parent's keys
                // (RFC 4035 §5.2), and its RRSIG names the parent in the Signer's
                // Name field. Following that name rather than stripping one label
                // also finds the parent where a label is not a zone cut. A signer
                // that is not strictly above the child could not be its parent.
                var dsRRSet    = dsRecords.Cast<IDNSResourceRecord>().ToList();
                var parentZone = (String?) null;

                foreach (var dsSig in Signatures(dsResponse, currentZone, DNSResourceRecordTypes.DS))
                {

                    var signer = Normalize(dsSig.SignerName.FullName);

                    if (signer == currentZone              ||
                        !IsAtOrBelow(currentZone, signer)  ||
                        !WithinValidityWindow(dsSig, Now))
                        continue;

                    if (!keyResponses.TryGetValue(signer, out var signerKeyResponse))
                    {

                        signerKeyResponse = await dnsClient.Query(
                                                      DomainName.Parse(signer + "."),
                                                      [DNSResourceRecordTypes.DNSKEY],
                                                      CancellationToken: CancellationToken
                                                  ).ConfigureAwait(false);

                        if (!signerKeyResponse.IsValid)
                            return DNSSECValidationResult.Indeterminate;

                        keyResponses[signer] = signerKeyResponse;

                    }

                    if (VerifiedByOneOf(dsRRSet, dsSig, ApexKeys(signerKeyResponse, signer)))
                    {
                        parentZone = signer;
                        break;
                    }

                }

                // A DS RRset no key of any parent signed is not the parent's word —
                // and that includes one that is not signed at all.
                if (parentZone is null)
                    return DNSSECValidationResult.Bogus;

                // RFC 6840 §5.2: a DS naming an algorithm or digest this build
                // cannot use is disregarded rather than failed, and a DS RRset
                // with nothing left after that is treated exactly like no DS at
                // all — the delegation is unsigned, not broken.
                //
                // Without this the next step reads "no DS verified" and answers
                // Bogus, which is the difference between a name resolving
                // insecurely and not resolving at all. It fires the day a child
                // moves to an algorithm this code has not learned yet, which is
                // precisely when the answer must not be an outage.
                //
                // §5.2 speaks of "authenticated DS records", which is why this
                // comes after the signature check: otherwise a forged DS naming
                // algorithm 253 downgrades any zone to Insecure.
                if (!HasUsableDelegationSigner(dsRecords))
                    return DNSSECValidationResult.Insecure;

                // At least one usable DS has to name a key of the zone, and that
                // key has to have signed the zone's DNSKEY RRset. A DS that names
                // a published key which signed nothing vouches for nothing.
                if (!KeySetSignedByAKeyNamedBy(currentKeys,
                                               currentKeySigs,
                                               dsRecords.Where(IsUsableDelegationSigner),
                                               Now))
                    return DNSSECValidationResult.Bogus;

                // Move up the chain
                currentZone         = parentZone;
                currentKeyResponse  = keyResponses[parentZone];

            }

            // Chain walk depth exceeded
            return DNSSECValidationResult.Indeterminate;

        }

        #endregion

        #region (private) KeySetSignedByAKeyNamedBy(Keys, Signatures, DelegationSigners, Now)

        /// <summary>
        /// Whether a zone's apex DNSKEY RRset carries a valid signature made with
        /// one of its own keys that one of the given DS records (or trust anchors)
        /// names.
        /// </summary>
        /// <remarks>
        /// RFC 4035 §5.3.1: key tags are not unique, so every key a signature's
        /// tag and algorithm point at is tried, and every signature — a zone in a
        /// KSK rollover signs its DNSKEY RRset with both keys, and only one of
        /// them is named by the DS. One that verifies is enough.
        /// </remarks>
        private Boolean KeySetSignedByAKeyNamedBy(List<DNSKEY>     Keys,
                                                  List<RRSIG>      Signatures,
                                                  IEnumerable<DS>  DelegationSigners,
                                                  DateTimeOffset   Now)
        {

            var delegationSigners  = DelegationSigners.ToList();
            var namedKeys          = Keys.Where(key => delegationSigners.Any(ds => Names(ds, key))).ToList();
            var rrSet              = Keys.Cast<IDNSResourceRecord>().ToList();

            return Signatures.Any(rrsig => WithinValidityWindow(rrsig, Now) &&
                                           VerifiedByOneOf(rrSet, rrsig, namedKeys));

        }

        #endregion

        #region (private) VerifiedByOneOf(RRSet, Signature, Keys)

        /// <summary>
        /// Whether any of the given keys verifies the signature over the RRset.
        /// </summary>
        /// <remarks>
        /// RFC 4035 §5.3.1: the key has to match the RRSIG's algorithm and key tag,
        /// and has to have the Zone Key flag set; RFC 4034 §2.1.2: a key whose
        /// protocol is not 3 is invalid for verification. Several keys may share a
        /// tag, so each candidate is tried.
        /// </remarks>
        private Boolean VerifiedByOneOf(IEnumerable<IDNSResourceRecord>  RRSet,
                                        RRSIG                            Signature,
                                        IEnumerable<DNSKEY>              Keys)
        {

            var rrSet = RRSet.ToList();

            return Keys.Any(key => (key.Flags & ZoneKeyFlag) != 0          &&
                                   key.Protocol             == 3            &&
                                   key.Algorithm            == Signature.Algorithm &&
                                   ComputeKeyTag(key)       == Signature.KeyTag    &&
                                   ValidateRRSig(rrSet, Signature, key) == DNSSECValidationResult.Secure);

        }

        #endregion

        #region (private static) Names(DelegationSigner, Key)

        /// <summary>
        /// Whether a DS record (or a trust anchor in DS form) names this key.
        /// </summary>
        private static Boolean Names(DS      DelegationSigner,
                                     DNSKEY  Key)

            => ComputeKeyTag(Key) == DelegationSigner.KeyTag    &&
               Key.Algorithm      == DelegationSigner.Algorithm &&
               VerifyDS(Key, DelegationSigner);

        #endregion

        #region (private static) WithinValidityWindow(Signature, Now)

        /// <summary>
        /// Whether the signature's validity window (RFC 4034 §3.1.5) contains the given time.
        /// </summary>
        private static Boolean WithinValidityWindow(RRSIG           Signature,
                                                    DateTimeOffset  Now)
        {

            var now = (UInt32) Now.ToUnixTimeSeconds();

            return now >= Signature.SignatureInception &&
                   now <= Signature.SignatureExpiration;

        }

        #endregion

        #region (private static) ApexKeys(Response, Zone) / Signatures(Response, Owner, TypeCovered)

        /// <summary>
        /// The DNSKEY records of a response that belong to the zone's apex. A key
        /// at any other name is not part of the RRset its RRSIG covers, and must not
        /// be used as if it were.
        /// </summary>
        private static List<DNSKEY> ApexKeys(DNSInfo  Response,
                                             String   Zone)

            => Response.Answers.OfType<DNSKEY>().
                                Where(key => Normalize(key.DomainName.FullName) == Normalize(Zone)).
                                ToList();


        /// <summary>
        /// The RRSIG records of a response that cover the given type at the given owner name.
        /// </summary>
        private static List<RRSIG> Signatures(DNSInfo                 Response,
                                              String                  Owner,
                                              DNSResourceRecordTypes  TypeCovered)

            => Response.Answers.OfType<RRSIG>().
                                Where(rrsig => rrsig.TypeCovered == TypeCovered &&
                                               Normalize(rrsig.DomainName.FullName) == Normalize(Owner)).
                                ToList();

        #endregion

        #region (private static) Normalize(Name) / IsAtOrBelow(Name, Zone)

        /// <summary>
        /// A name in the one spelling every comparison here uses: lower case, no
        /// trailing dot, and the root as the empty string.
        /// </summary>
        private static String Normalize(String Name)

            => Name.TrimEnd('.').ToLowerInvariant();


        /// <summary>
        /// Whether the name is the zone's apex or lies below it.
        /// </summary>
        private static Boolean IsAtOrBelow(String  Name,
                                           String  Zone)
        {

            var name = Normalize(Name);
            var zone = Normalize(Zone);

            return zone.Length == 0 ||
                   name == zone     ||
                   name.EndsWith("." + zone, StringComparison.Ordinal);

        }

        #endregion

        #region (static) VerifySignature(Algorithm, PublicKey, Data, Signature)

        /// <summary>
        /// Verify a signature made with a DNSSEC algorithm over arbitrary data.
        /// </summary>
        /// <param name="Algorithm">The DNSSEC algorithm number (RFC 8624 §3.1).</param>
        /// <param name="PublicKey">The public key in the wire form its algorithm defines — RFC 3110 for RSA, RFC 6605 for ECDSA, RFC 8080 for the Edwards curves.</param>
        /// <param name="Data">The signed data.</param>
        /// <param name="Signature">The signature to check.</param>
        /// <remarks>
        /// Public because RRSIG is not the only thing DNS signs with these
        /// algorithms and these key encodings: SIG(0) (RFC 2931) signs a whole
        /// message under a KEY record, and it would be a poor idea to grow a
        /// second implementation of the same six algorithms next to this one.
        /// </remarks>
        public static Boolean VerifySignature(Byte    Algorithm,
                                              Byte[]  PublicKey,
                                              Byte[]  Data,
                                              Byte[]  Signature)
        {

            return Algorithm switch {

                // RSA/SHA-1 (algorithm 5) and RSA/SHA-1-NSEC3-SHA1 (algorithm 7).
                // Deprecated (RFC 8624 §3.1) but still widely deployed in signed zones.
                5 or 7 => VerifyRSA(PublicKey, Data, Signature, HashAlgorithmName.SHA1),

                // RSA/SHA-256
                8  => VerifyRSA(PublicKey, Data, Signature, HashAlgorithmName.SHA256),

                // RSA/SHA-512
                10 => VerifyRSA(PublicKey, Data, Signature, HashAlgorithmName.SHA512),

                // ECDSA P-256/SHA-256
                13 => VerifyECDSA(PublicKey, Data, Signature, ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256),

                // ECDSA P-384/SHA-384
                14 => VerifyECDSA(PublicKey, Data, Signature, ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384),

                // Ed25519
                15 => VerifyEd25519(PublicKey, Data, Signature),

                // Ed448
                16 => VerifyEd448(PublicKey, Data, Signature),

                // Unknown algorithm
                _  => false

            };

        }

        #endregion

        #region (private static) VerifyRSA(PublicKey, Data, Signature, HashAlgorithm)

        /// <summary>
        /// Verify an RSA signature (algorithms 8 and 10).
        /// The DNSKEY public key format for RSA is:
        ///   1 or 3 bytes exponent length prefix + exponent + modulus
        /// </summary>
        private static Boolean VerifyRSA(Byte[]            PublicKey,
                                         Byte[]            Data,
                                         Byte[]            Signature,
                                         HashAlgorithmName HashAlgorithm)
        {

            // A key read off the wire is attacker-controlled, so every step below
            // has to be able to fail without throwing: a DNSKEY too short to hold
            // its own exponent length, an exponent length that runs past the end,
            // a modulus the platform refuses. The answer to all of them is the
            // same — this key does not verify this signature — and it has to be
            // an answer rather than an exception, because the caller turns a
            // throw into Indeterminate, which RFC 4033 §5 defines as "no trust
            // anchor covers this" and not "the key was broken".
            try
            {

                // Parse RSA public key from DNSKEY wire format (RFC 3110)
                var offset       = 0;
                var exponentLen  = (Int32) PublicKey[offset++];

                if (exponentLen == 0)
                {
                    // 3-byte length prefix
                    exponentLen = (PublicKey[offset] << 8) | PublicKey[offset + 1];
                    offset += 2;
                }

                var exponent = PublicKey[offset..(offset + exponentLen)];
                offset += exponentLen;

                var modulus = PublicKey[offset..];

                using var rsa = RSA.Create();

                rsa.ImportParameters(new RSAParameters {
                    Exponent = exponent,
                    Modulus  = modulus
                });

                return rsa.VerifyData(
                           Data,
                           Signature,
                           HashAlgorithm,
                           RSASignaturePadding.Pkcs1
                       );

            }
            catch (Exception)
            {
                return false;
            }

        }

        #endregion

        #region (private static) VerifyECDSA(PublicKey, Data, Signature, Curve, HashAlgorithm)

        /// <summary>
        /// Verify an ECDSA signature (algorithms 13 and 14).
        /// The DNSKEY public key is the uncompressed point (Q.X || Q.Y) without the 0x04 prefix.
        /// The signature is (r || s) in fixed-size big-endian format.
        /// </summary>
        private static Boolean VerifyECDSA(Byte[]            PublicKey,
                                           Byte[]            Data,
                                           Byte[]            Signature,
                                           ECCurve           Curve,
                                           HashAlgorithmName HashAlgorithm)
        {

            // As in VerifyRSA: a point that is not on the curve, or a key of the
            // wrong length for it, is a failed verification and not a crash.
            // ECDsa.Create validates the point and throws, which is the right
            // thing for it to do and the wrong thing to let out of here.
            try
            {

                var keySize = PublicKey.Length / 2;

                var qx = PublicKey[..keySize];
                var qy = PublicKey[keySize..];

                using var ecdsa = ECDsa.Create(new ECParameters {
                    Curve = Curve,
                    Q     = new ECPoint {
                        X = qx,
                        Y = qy
                    }
                });

                // DNSSEC ECDSA signature format is r || s (fixed-size, no ASN.1 wrapping)
                return ecdsa.VerifyData(
                           Data,
                           Signature,
                           HashAlgorithm,
                           DSASignatureFormat.IeeeP1363FixedFieldConcatenation
                       );

            }
            catch (Exception)
            {
                return false;
            }

        }

        #endregion

        #region (private static) VerifyEd25519(PublicKey, Data, Signature)

        /// <summary>
        /// Verify an Ed25519 signature (algorithm 15) using BouncyCastle.
        /// </summary>
        private static Boolean VerifyEd25519(Byte[]  PublicKey,
                                             Byte[]  Data,
                                             Byte[]  Signature)
        {

            try
            {

                var publicKeyParams = new Ed25519PublicKeyParameters(PublicKey, 0);
                var verifier        = new Ed25519Signer();

                verifier.Init(false, publicKeyParams);
                verifier.BlockUpdate(Data, 0, Data.Length);

                return verifier.VerifySignature(Signature);

            }
            catch
            {
                return false;
            }

        }

        #endregion

        #region (private static) VerifyEd448(PublicKey, Data, Signature)

        /// <summary>
        /// Verify an Ed448 signature (algorithm 16) using BouncyCastle.
        /// </summary>
        private static Boolean VerifyEd448(Byte[]  PublicKey,
                                           Byte[]  Data,
                                           Byte[]  Signature)
        {

            try
            {

                var publicKeyParams = new Ed448PublicKeyParameters(PublicKey, 0);
                var verifier        = new Ed448Signer([]);

                verifier.Init(false, publicKeyParams);
                verifier.BlockUpdate(Data, 0, Data.Length);

                return verifier.VerifySignature(Signature);

            }
            catch
            {
                return false;
            }

        }

        #endregion

        #region (private static) SerializeCanonicalName(Name)

        /// <summary>
        /// Serialize a domain name in canonical wire format (lowercased, no compression).
        /// </summary>
        /// <remarks>
        /// RFC 5155 §5 hashes the very same bytes, so the implementation lives in
        /// <see cref="DNSTools.SerializeCanonicalName"/> and both callers share it.
        /// </remarks>
        private static Byte[] SerializeCanonicalName(String Name)

            => DNSTools.SerializeCanonicalName(Name);

        #endregion

        #region (private static) GetParentZone(Zone)

        /// <summary>
        /// Get the parent zone of a given zone name.
        /// For "example.com." returns "com.", for "com." returns ".".
        /// </summary>
        private static String? GetParentZone(String Zone)
        {

            var normalized = Zone.TrimEnd('.');

            if (String.IsNullOrEmpty(normalized) || normalized == ".")
                return null;

            var dotIndex = normalized.IndexOf('.');

            if (dotIndex < 0)
                return ".";

            return normalized[(dotIndex + 1)..] + ".";

        }

        #endregion

    }

}
