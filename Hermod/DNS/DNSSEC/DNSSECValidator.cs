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
        /// The IANA root trust anchors still in force, as published in
        /// https://data.iana.org/root-anchors/root-anchors.xml (Algorithm 8, SHA-256):
        /// KSK-2017 (Key Tag 20326) and KSK-2024 (Key Tag 38696). KSK-2024 signs the
        /// root DNSKEY RRset alone from 2026-10-11 on, and KSK-2017 is revoked and
        /// withdrawn after that — a validator anchored on KSK-2017 only would then
        /// answer Bogus for everything, unless it had picked KSK-2024 up by RFC 5011.
        /// </summary>
        private static readonly DS[] RootTrustAnchors = [

            new (DomainName.Parse("."),
                 DNSQueryClasses.IN,
                 TimeSpan.FromDays(36500),
                 20326,
                 8,
                 2,
                 Convert.FromHexString("E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D")),

            new (DomainName.Parse("."),
                 DNSQueryClasses.IN,
                 TimeSpan.FromDays(36500),
                 38696,
                 8,
                 2,
                 Convert.FromHexString("683D2D0ACB8C9B712A1948B27F741219298D0A450D612C483AF444A4C0FB2B16"))

        ];

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
        /// Create a new DNSSEC validator with the IANA root trust anchors pre-configured
        /// (KSK-2017 and KSK-2024).
        /// </summary>
        /// <param name="DNSClient">A DNS client for fetching DNSKEY/DS records.</param>
        public static DNSSECValidator WithRootTrustAnchor(IDNSClient DNSClient)

            => new(DNSClient, RootTrustAnchors);

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
        /// <remarks>
        /// <para>
        /// The RRset is authenticated before any key in it counts for anything.
        /// RFC 5011 adds a new key only "when that RRSet is validated by an
        /// existing trust anchor" (§2), and starts its hold-down when it is seen
        /// "in a validated trust point DNSKEY RRSet" (§2.2). §2.1 accepts a
        /// revocation only from "a self-signed RRSet": the revoked key itself has
        /// to have signed it.
        /// </para>
        /// <para>
        /// This used to say it validated the RRset and verified no signature at
        /// all. Every SEP key in whatever answer came back started a hold-down,
        /// and every REVOKE flag was believed: an attacker on the path for thirty
        /// days planted a trust anchor of their own, and one forged answer removed
        /// a genuine one.
        /// </para>
        /// <para>
        /// A set nothing authenticates changes nothing — no hold-down starts, none
        /// runs out, and none is cut short by a key that seems to be missing from
        /// it. A set authenticated only by a revoked anchor's own signature is
        /// believed about that revocation and about nothing else in it: a revoked
        /// key "MUST NOT" be used "for any other purpose except to validate the
        /// RRSIG it signed over the DNSKEY RRSet specifically for the purpose of
        /// validating the revocation".
        /// </para>
        /// </remarks>
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

                // Only the keys at the root's apex make up the RRset, and only the
                // root's own signatures over it can authenticate it.
                var dnskeys        = ApexKeys(response, String.Empty);
                var signatures     = Signatures(response, String.Empty, DNSResourceRecordTypes.DNSKEY).
                                         Where(rrsig => Normalize(rrsig.SignerName.FullName).Length == 0).
                                         ToList();

                // RFC 5011 §2.2: signed by a key an existing trust anchor names.
                var authenticated  = KeySetSignedByAKeyNamedBy(dnskeys, signatures, trustAnchors, now);

                // RFC 5011 §2.1: a revocation counts only if the revoked key signed
                // the RRset itself — only its holder can revoke it. In a set that is
                // not otherwise authenticated, that signature still speaks for a key
                // that is an anchor here, and for none other: a stranger's revoked
                // key would otherwise bar a genuine key that shares its tag.
                var revocations    = dnskeys.Where(key => IsSecureEntryPoint(key)                       &&
                                                          (key.Flags & RevokeFlag) != 0                 &&
                                                          SelfSigned(dnskeys, signatures, key, now)     &&
                                                          (authenticated ||
                                                           trustAnchors.Any(anchor => Names(anchor, Unrevoked(key))))).
                                             ToList();

                if (!authenticated && revocations.Count == 0)
                    return false;

                var modified       = false;

                foreach (var key in revocations)
                {

                    // This anchor was stored while the REVOKE bit was clear. The key
                    // tag is a checksum over the whole DNSKEY RDATA, Flags included,
                    // so setting REVOKE changes it — matching on the revoked key's
                    // current tag can never find the stored anchor, and the revocation
                    // would be silently ignored while the compromised key stayed
                    // trusted. Compare against the key as it was before it was revoked,
                    // by digest: a tag is a checksum anyone can collide with.
                    var liveKey    = Unrevoked(key);
                    var keyId      = (ComputeKeyTag(key),     key.Algorithm);
                    var liveKeyId  = (ComputeKeyTag(liveKey), key.Algorithm);

                    if (trustAnchors.RemoveAll(anchor => Names(anchor, liveKey) || Names(anchor, key)) > 0)
                        modified = true;

                    // Remember it under both identities, so the key cannot be
                    // re-admitted when it is next published without the REVOKE bit.
                    revokedAnchors.Add(liveKeyId);
                    revokedAnchors.Add(keyId);

                    // Also remove from pending
                    pendingAnchors.Remove(liveKeyId);
                    pendingAnchors.Remove(keyId);

                }

                // A revocation vouches for the revoked key and for nothing else in
                // the RRset it came in.
                if (!authenticated)
                    return modified;

                foreach (var key in dnskeys)
                {

                    var keyTag    = ComputeKeyTag(key);
                    var keyId     = (keyTag, key.Algorithm);

                    // Process new KSKs (SEP bit set, not revoked)
                    if (IsSecureEntryPoint(key) && (key.Flags & RevokeFlag) == 0)
                    {

                        // Check if this key is already a trust anchor
                        var isExisting = trustAnchors.Any(anchor => Names(anchor, key));

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
        /// Whether the key carries the Secure Entry Point flag (RFC 4034 §2.1.1),
        /// which RFC 5011 takes to mark the keys that are trust anchor candidates.
        /// </summary>
        private static Boolean IsSecureEntryPoint(DNSKEY Key)
            => (Key.Flags & 0x0001) != 0;

        /// <summary>
        /// The key as it was published before its REVOKE bit was set — the form
        /// a trust anchor for it was stored in.
        /// </summary>
        private static DNSKEY Unrevoked(DNSKEY Key)

            => new (DomainName.ParseLenient(Key.DomainName.FullName),
                    Key.Class,
                    Key.TimeToLive,
                    (UInt16) (Key.Flags & ~RevokeFlag),
                    Key.Protocol,
                    Key.Algorithm,
                    Key.PublicKey);

        /// <summary>
        /// Whether the key itself made one of the valid signatures over the RRset
        /// it is part of — RFC 5011 §2.1's "self-signed RRSet".
        /// </summary>
        private Boolean SelfSigned(List<DNSKEY>    Keys,
                                   List<RRSIG>     Signatures,
                                   DNSKEY          Key,
                                   DateTimeOffset  Now)
        {

            var rrSet = Keys.Cast<IDNSResourceRecord>().ToList();

            return Signatures.Any(rrsig => WithinValidityWindow(rrsig, Now) &&
                                           VerifiedByOneOf(rrSet, rrsig, [ Key ]));

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

                    // No signature at all. That is what an unsigned zone sends,
                    // and what stripping the signatures of a signed one produces;
                    // only a proof that the zone is unsigned tells them apart.
                    // It is owed for every owner the answer holds, and for a
                    // negative answer for the name asked about. Without either,
                    // there is no name a proof could be about.
                    var owners = Response.Answers.Where(rr => rr is not RRSIG).
                                                  Select(rr => rr.DomainName.FullName).
                                                  ToList();

                    if (owners.Count == 0 && Question is not null)
                        owners.Add(Question.Value.QName.FullName);

                    return await ProveAllUnsigned(owners, now, CancellationToken).ConfigureAwait(false);

                }

                // Group answers by type (excluding RRSIG itself) to form RRSets
                var answerRecords = Response.Answers
                                            .OfType<ADNSResourceRecord>()
                                            .Where(rr => rr.Type != DNSResourceRecordTypes.RRSIG)
                                            .ToList();

                // The RRsets a signature was verified for, by owner and type
                var verified = new HashSet<(String Owner, DNSResourceRecordTypes Type)>();

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

                    // A zone speaks only for the names inside it. RFC 4035 §5.3.1:
                    // "The RRSIG RR's Signer's Name field MUST be the name of the
                    // zone that contains the RRset." Without this, whoever holds the
                    // key of any properly delegated zone could sign an RRset for any
                    // name at all under their own signer name, and the chain walk —
                    // which authenticates the signer's keys, not the signer's
                    // authority over the owner — would call it Secure.
                    if (!IsAtOrBelow(rrsig.DomainName.FullName, rrsig.SignerName.FullName))
                        return DNSSECValidationResult.Bogus;

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

                    verified.Add((Normalize(rrsig.DomainName.FullName), rrsig.TypeCovered));

                }

                // A signature vouches for the RRset it covers and for nothing else
                // in the answer. This used to skip an RRSIG with nothing to cover
                // and then report Secure for whatever was left, so a forged A record
                // beside any genuine RRSIG — over a TXT the answer did not even
                // hold — was Secure; and one signed RRset made its unsigned
                // neighbours Secure too.
                //
                // An unsigned RRset is what a signed CNAME into an unsigned zone
                // legitimately brings along — and what stripping one signature
                // from an answer that held two produces. It is the question an
                // answer without any RRSIG raises, and is answered the same way:
                // Insecure where a proof shows the owner's zone unsigned, Bogus
                // where the owner lies in a signed zone. Answered Insecure
                // without asking, it let an attacker keep any genuinely signed
                // RRset beside a stripped one and have the rest ignored. The
                // exception is the CNAME a server synthesizes from a DNAME
                // (RFC 6672 §5.3.1), which is never signed and needs no signature
                // of its own when the DNAME is verified and the CNAME follows
                // from it.
                var unsigned = answerRecords.Where(rr => !verified.Contains((Normalize(rr.DomainName.FullName), rr.Type)) &&
                                                         !SynthesizedFromAVerifiedDNAME(rr, answerRecords, verified)).
                                             Select(rr => rr.DomainName.FullName).
                                             ToList();

                if (unsigned.Count > 0)
                    return await ProveAllUnsigned(unsigned, now, CancellationToken).ConfigureAwait(false);

                return DNSSECValidationResult.Secure;

            }
            catch (Exception)
            {
                return DNSSECValidationResult.Indeterminate;
            }

        }

        #endregion

        #region (private static) SynthesizedFromAVerifiedDNAME(Record, Answers, Verified)

        /// <summary>
        /// Whether the record is a CNAME that follows from a verified DNAME of the
        /// same answer (RFC 6672 §2.2): its owner lies strictly below the DNAME's,
        /// and its target is the same leading labels in front of the DNAME's target.
        /// </summary>
        private static Boolean SynthesizedFromAVerifiedDNAME(ADNSResourceRecord                                Record,
                                                             IEnumerable<ADNSResourceRecord>                   Answers,
                                                             HashSet<(String Owner, DNSResourceRecordTypes Type)>  Verified)
        {

            if (Record is not CNAME cname)
                return false;

            var owner   = Normalize(cname.DomainName.FullName);
            var target  = Normalize(cname.CName.FullName);

            return Answers.OfType<DNAME>().Any(dname => {

                var dnameOwner  = Normalize(dname.DomainName.FullName);
                var dnameTarget = Normalize(dname.Target.FullName);

                if (!Verified.Contains((dnameOwner, DNSResourceRecordTypes.DNAME)) ||
                    owner == dnameOwner                                            ||
                    !IsAtOrBelow(owner, dnameOwner))
                    return false;

                var prefix = dnameOwner.Length == 0
                                 ? owner
                                 : owner[..^(dnameOwner.Length + 1)];

                return target == (dnameTarget.Length == 0
                                      ? prefix
                                      : prefix + "." + dnameTarget);

            });

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

            // The records whose signatures verified — the only ones the proof may
            // be read from.
            var verified = new List<IDNSResourceRecord>();

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

                // As for an answer: a zone can deny only names inside it.
                if (!IsAtOrBelow(rrsig.DomainName.FullName, rrsig.SignerName.FullName))
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

                verified.AddRange(rrSet);

            }

            // The records are authentic. Now: do they prove the claim?
            //
            // Only the verified ones. This used to hand the whole authority
            // section to the proof check, so an unsigned NSEC or NSEC3 placed
            // beside a genuine signed one — replayed from anywhere in the zone —
            // was never checked and still counted: a forged NODATA or NXDOMAIN
            // for any name, Secure.
            return DenialOfExistenceValidator.Verify(QName, QType, verified) switch {

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

            // What an intact chain up to the anchor amounts to: Secure, unless a
            // link on the way was a proven unsigned delegation.
            var outcome             = DNSSECValidationResult.Secure;

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
                               ? outcome
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

                // No DS is a claim like any other, and it needs proof like any
                // other: RFC 4035 §5.2 lets a delegation be insecure only when
                // the parent's authenticated NSEC or NSEC3 says so. This used to
                // return Insecure on an empty answer alone, so an attacker on the
                // path stripped the DS of any signed zone under the anchor and
                // got Insecure for it — and for DANE, Insecure means the TLSA
                // records are ignored.
                //
                // The proof is the parent's, so it is accepted on the same terms
                // as a DS would be: conditionally on the parent's keys, which the
                // walk goes on to authenticate. What it then ends in is Insecure
                // rather than Secure.
                if (dsRecords.Count == 0)
                {

                    // Outside every anchor there is nothing the proof could chain
                    // to, and nothing that said the zone should be signed.
                    if (!CoveredByATrustAnchor(DomainName.Parse(currentZone + ".")))
                        return DNSSECValidationResult.Insecure;

                    var (proof, provenBy) = await ProveNoDelegationSigner(currentZone,
                                                                          dsResponse,
                                                                          keyResponses,
                                                                          Now,
                                                                          CancellationToken).ConfigureAwait(false);

                    if (proof != DNSSECValidationResult.Insecure || provenBy is null)
                        return proof;

                    outcome             = DNSSECValidationResult.Insecure;
                    currentZone         = provenBy;
                    currentKeyResponse  = keyResponses[provenBy];

                    continue;

                }

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
                // algorithm 253 downgrades any zone to Insecure. And the check
                // so far is only half of the authentication — the parent's keys
                // are still the ones the response offered — so the walk goes on
                // up to the anchor rather than returning here, where a DS signed
                // with a key made up for the parent was reason enough. Outside
                // every anchor there is nothing to go on up to, and the answer
                // stays what it was.
                if (!HasUsableDelegationSigner(dsRecords))
                {

                    if (!CoveredByATrustAnchor(DomainName.Parse(currentZone + ".")))
                        return DNSSECValidationResult.Insecure;

                    outcome             = DNSSECValidationResult.Insecure;
                    currentZone         = parentZone;
                    currentKeyResponse  = keyResponses[parentZone];
                    continue;
                }

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

        #region (private) ProveNoDelegationSigner(Zone, DSResponse, KeyResponses, Now, CancellationToken)

        /// <summary>
        /// Check the proof that came with an answer holding no DS for the zone:
        /// NSEC or NSEC3 records, signed by the parent, that show an unsigned
        /// delegation (RFC 4035 §5.2, RFC 5155 §8.6, RFC 6840 §4.4).
        /// </summary>
        /// <param name="Zone">The zone whose DS was asked for.</param>
        /// <param name="DSResponse">The response to the DS query.</param>
        /// <param name="KeyResponses">The DNSKEY responses fetched so far during this walk, by zone; the parent's is added.</param>
        /// <param name="Now">The time the signatures are checked against.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <returns>
        /// Insecure and the parent zone whose keys signed the proof, when the
        /// proof holds — conditionally on those keys, which the caller has yet
        /// to authenticate. Bogus when it does not, Indeterminate when the
        /// parent's keys could not be fetched.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The proof has to come from one zone, and from one strictly above the
        /// child. A proof assembled from two zones proves nothing about either —
        /// the closest encloser matched in one, and an opt-out span of another
        /// covering the next closer name, add up to an opt-out the parent never
        /// published. Every record is therefore verified against the keys of
        /// the one zone that signed them all, and a record no key of that zone
        /// signed is not read. And the child cannot speak for its own
        /// delegation: its apex NSEC never lists DS, because DS does not live
        /// there.
        /// </para>
        /// <para>
        /// An NSEC or NSEC3 RRset counts once one of its signatures verifies
        /// (RFC 4035 §5.3.3); the proof is read from those alone, as in
        /// <see cref="ValidateDenialAsync"/>.
        /// </para>
        /// </remarks>
        private async Task<(DNSSECValidationResult Result, String? Parent)> ProveNoDelegationSigner(String                       Zone,
                                                                                                    DNSInfo                      DSResponse,
                                                                                                    Dictionary<String, DNSInfo>  KeyResponses,
                                                                                                    DateTimeOffset               Now,
                                                                                                    CancellationToken            CancellationToken)
        {

            var authorities  = DSResponse.Authorities.ToList();
            var denialSigs   = authorities.OfType<RRSIG>().
                                           Where(rrsig => rrsig.TypeCovered is DNSResourceRecordTypes.NSEC
                                                                            or DNSResourceRecordTypes.NSEC3).
                                           ToList();

            var signers      = denialSigs.Select(rrsig => Normalize(rrsig.SignerName.FullName)).
                                          Distinct().
                                          ToList();

            // Nothing signed, or signed by more than one zone — and then there
            // is no telling whose keys the proof is to be read with.
            if (signers.Count != 1)
                return (DNSSECValidationResult.Bogus, null);

            var parent = signers[0];

            if (parent == Zone || !IsAtOrBelow(Zone, parent))
                return (DNSSECValidationResult.Bogus, null);

            if (!KeyResponses.TryGetValue(parent, out var parentKeyResponse))
            {

                parentKeyResponse = await dnsClient.Query(
                                              DomainName.Parse(parent + "."),
                                              [DNSResourceRecordTypes.DNSKEY],
                                              CancellationToken: CancellationToken
                                          ).ConfigureAwait(false);

                if (!parentKeyResponse.IsValid)
                    return (DNSSECValidationResult.Indeterminate, null);

                KeyResponses[parent] = parentKeyResponse;

            }

            var verified = DenialSignedBy(DSResponse, parent, ApexKeys(parentKeyResponse, parent), Now);

            return DenialOfExistenceValidator.VerifyNoDelegationSigner(DomainName.Parse(Zone + "."), verified) switch {

                DenialOfExistence.NoDataForType  => (DNSSECValidationResult.Insecure, parent),

                // RFC 5155 §8.6: an opt-out span says the delegation may be
                // unsigned, and an unsigned one is what the child then is.
                DenialOfExistence.OptedOut       => (DNSSECValidationResult.Insecure, parent),

                _                                => (DNSSECValidationResult.Bogus,    null)

            };

        }

        #endregion

        #region (private) DenialSignedBy(Response, Zone, Keys, Now)

        /// <summary>
        /// The NSEC and NSEC3 records of the response's authority section that
        /// the zone signed with one of the given keys — the only ones a proof
        /// may be read from (RFC 4035 §5.3.3: an RRset counts once one of its
        /// signatures verifies).
        /// </summary>
        private List<IDNSResourceRecord> DenialSignedBy(DNSInfo         Response,
                                                        String          Zone,
                                                        List<DNSKEY>    Keys,
                                                        DateTimeOffset  Now)
        {

            var authorities  = Response.Authorities.ToList();
            var verified     = new List<IDNSResourceRecord>();

            var denialSigs   = authorities.OfType<RRSIG>().
                                           Where(rrsig => (rrsig.TypeCovered is DNSResourceRecordTypes.NSEC
                                                                             or DNSResourceRecordTypes.NSEC3) &&
                                                          Normalize(rrsig.SignerName.FullName) == Zone);

            foreach (var rrSetSigs in denialSigs.GroupBy(rrsig => (Owner: Normalize(rrsig.DomainName.FullName), rrsig.TypeCovered)))
            {

                // A zone can deny only names inside it.
                if (!IsAtOrBelow(rrSetSigs.Key.Owner, Zone))
                    continue;

                var rrSet = authorities.OfType<ADNSResourceRecord>().
                                        Where(rr => rr.Type == rrSetSigs.Key.TypeCovered &&
                                                    Normalize(rr.DomainName.FullName) == rrSetSigs.Key.Owner).
                                        Cast<IDNSResourceRecord>().
                                        ToList();

                if (rrSet.Count > 0 &&
                    rrSetSigs.Any(rrsig => WithinValidityWindow(rrsig, Now) &&
                                           VerifiedByOneOf(rrSet, rrsig, Keys)))
                    verified.AddRange(rrSet);

            }

            return verified;

        }

        #endregion


        #region (private) ProveAllUnsigned(Owners, Now, CancellationToken)

        /// <summary>
        /// Insecure if every one of the owner names is proven to lie outside the
        /// signed part of the namespace (see <see cref="ProveUnsigned"/>), and
        /// the first verdict that is not Insecure otherwise. No owners at all
        /// is Insecure: there is nothing a proof could be about.
        /// </summary>
        private async Task<DNSSECValidationResult> ProveAllUnsigned(IEnumerable<String>  Owners,
                                                                    DateTimeOffset       Now,
                                                                    CancellationToken    CancellationToken)
        {

            // The names between an anchor and the owners are mostly the same
            // ones — a CNAME and its target in one zone, an A and an AAAA — and
            // each is asked about once.
            var names = new Dictionary<String, (NameBelowAnchor Kind, List<DNSKEY> Keys)>();

            foreach (var owner in Owners.Select(Normalize).Distinct())
            {

                var result = await ProveUnsigned(owner, names, Now, CancellationToken).ConfigureAwait(false);

                if (result != DNSSECValidationResult.Insecure)
                    return result;

            }

            return DNSSECValidationResult.Insecure;

        }

        #endregion

        #region (private) ProveUnsigned(Owner, Names, Now, CancellationToken)

        /// <summary>
        /// What a name between an anchor and the owner of an unsigned RRset
        /// turned out to be, seen from the zone above it.
        /// </summary>
        private enum NameBelowAnchor
        {

            /// <summary>A name of the zone above that is no zone cut — an empty non-terminal included.</summary>
            InTheZone,

            /// <summary>The apex of a signed zone, whose DNSKEY RRset is authenticated.</summary>
            SignedZone,

            /// <summary>A delegation the zone above proves unsigned.</summary>
            UnsignedDelegation,

            /// <summary>Nothing proves what it is, or the proof is broken.</summary>
            Bogus,

            /// <summary>What it is could not be found out.</summary>
            Indeterminate

        }


        /// <summary>
        /// What an RRset that came without any signature amounts to: Insecure
        /// when its owner lies outside every trust anchor, or below a delegation
        /// that is proven unsigned; Bogus when its owner lies in a signed zone.
        /// </summary>
        /// <param name="Owner">The owner name of the unsigned RRset, or the name a negative answer was asked for.</param>
        /// <param name="Names">What the names below an anchor turned out to be, shared by every owner of one answer.</param>
        /// <param name="Now">The time every signature on the way down is checked against.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <remarks>
        /// <para>
        /// RFC 4035 §4.3: data is Insecure when the resolver has a trust anchor
        /// and a proof that some delegation on the way to it is unsigned, and
        /// §5.2 says how that proof looks. An unsigned answer under an anchor
        /// without one is what stripping the signatures produces. This used to
        /// be decided by the anchors alone: without a question an unsigned
        /// answer was Insecure — so whoever removed the RRSIGs of a DANE
        /// lookup's TLSA records had them ignored — and with a question it was
        /// Bogus under every anchor, which made every unsigned zone on the
        /// internet Bogus under the root's.
        /// </para>
        /// <para>
        /// The proof is found from the top. From the closest anchor down, one
        /// label at a time, the DS RRset of each name is asked for and read
        /// with the keys of the zone the name lies in, which the step above
        /// authenticated. A signed DS leads into a signed zone, whose keys
        /// then read the next step. An empty answer is read from that zone's
        /// signed NSEC or NSEC3 records: a delegation without DS, or an
        /// opt-out span, ends the walk Insecure (with the rules of
        /// <see cref="DenialOfExistenceValidator.VerifyNoDelegationSigner"/>:
        /// NS set and SOA clear, so the NSEC of a name that is no zone cut
        /// proves nothing); a name that exists in the zone without being a
        /// delegation, an empty non-terminal included, sends the walk one label
        /// further within the same zone. Anything else is no proof, and Bogus.
        /// A walk that reaches the owner without leaving signed zones has
        /// shown the owner's zone to be signed: the RRset should have been too.
        /// </para>
        /// </remarks>
        private async Task<DNSSECValidationResult> ProveUnsigned(String                                                          Owner,
                                                                 Dictionary<String, (NameBelowAnchor Kind, List<DNSKEY> Keys)>  Names,
                                                                 DateTimeOffset                                                  Now,
                                                                 CancellationToken                                               CancellationToken)
        {

            var owner   = Normalize(Owner);

            // RFC 4035 §4.3 starts from the closest anchor: one deeper than the
            // root is a zone the root's chain may not reach.
            var anchor  = trustAnchors.Select (anchor => Normalize(anchor.DomainName.FullName)).
                                       Where  (name   => IsAtOrBelow(owner, name)).
                                       OrderByDescending(name => name.Length).
                                       FirstOrDefault();

            // Outside every anchor nothing said the owner should be signed, and
            // there is nothing a proof could chain to.
            if (anchor is null)
                return DNSSECValidationResult.Insecure;

            if (!Names.TryGetValue(anchor, out var step))
            {
                step           = await AnchoredZone(anchor, Now, CancellationToken).ConfigureAwait(false);
                Names[anchor]  = step;
            }

            if (step.Kind == NameBelowAnchor.Indeterminate)
                return DNSSECValidationResult.Indeterminate;

            if (step.Kind != NameBelowAnchor.SignedZone)
                return DNSSECValidationResult.Bogus;

            var zone    = anchor;
            var keys    = step.Keys;
            var labels  = owner. Length == 0 ? [] : owner. Split('.');
            var depth   = anchor.Length == 0 ? 0  : anchor.Split('.').Length;

            for (var index = labels.Length - depth - 1; index >= 0; index--)
            {

                var name = String.Join('.', labels[index..]);

                if (!Names.TryGetValue(name, out step))
                {
                    step         = await NameBelow(name, zone, keys, Now, CancellationToken).ConfigureAwait(false);
                    Names[name]  = step;
                }

                switch (step.Kind)
                {

                    case NameBelowAnchor.SignedZone:
                        zone = name;
                        keys = step.Keys;
                        break;

                    case NameBelowAnchor.InTheZone:
                        break;

                    case NameBelowAnchor.UnsignedDelegation:
                        return DNSSECValidationResult.Insecure;

                    case NameBelowAnchor.Indeterminate:
                        return DNSSECValidationResult.Indeterminate;

                    default:
                        return DNSSECValidationResult.Bogus;

                }

            }

            // The walk reached the owner without leaving signed zones: its
            // RRset should have been signed, and the signatures were stripped.
            return DNSSECValidationResult.Bogus;

        }

        #endregion

        #region (private) AnchoredZone(Zone, Now, CancellationToken)

        /// <summary>
        /// The anchored zone's DNSKEY RRset, once a key a trust anchor names has
        /// signed it.
        /// </summary>
        private async Task<(NameBelowAnchor Kind, List<DNSKEY> Keys)> AnchoredZone(String             Zone,
                                                                                   DateTimeOffset     Now,
                                                                                   CancellationToken  CancellationToken)
        {

            var keyResponse = await dnsClient.Query(
                                        DomainName.ParseLenient(Zone + "."),
                                        [DNSResourceRecordTypes.DNSKEY],
                                        CancellationToken: CancellationToken
                                    ).ConfigureAwait(false);

            if (!keyResponse.IsValid)
                return (NameBelowAnchor.Indeterminate, []);

            var keys = ApexKeys(keyResponse, Zone);
            var sigs = Signatures(keyResponse, Zone, DNSResourceRecordTypes.DNSKEY).
                           Where(rrsig => Normalize(rrsig.SignerName.FullName) == Zone).
                           ToList();

            return KeySetSignedByAKeyNamedBy(keys,
                                             sigs,
                                             trustAnchors.Where(anchor => Normalize(anchor.DomainName.FullName) == Zone),
                                             Now)
                       ? (NameBelowAnchor.SignedZone, keys)
                       : (NameBelowAnchor.Bogus,      []);

        }

        #endregion

        #region (private) NameBelow(Name, Zone, ZoneKeys, Now, CancellationToken)

        /// <summary>
        /// Ask for the DS RRset of a name one label below a signed zone, and
        /// read the answer with the zone's authenticated keys.
        /// </summary>
        /// <param name="Name">The name, one label below the zone or deeper inside it.</param>
        /// <param name="Zone">The signed zone the name lies in.</param>
        /// <param name="ZoneKeys">The zone's authenticated DNSKEY RRset.</param>
        /// <param name="Now">The time the signatures are checked against.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        private async Task<(NameBelowAnchor Kind, List<DNSKEY> Keys)> NameBelow(String             Name,
                                                                                String             Zone,
                                                                                List<DNSKEY>       ZoneKeys,
                                                                                DateTimeOffset     Now,
                                                                                CancellationToken  CancellationToken)
        {

            var name        = DomainName.ParseLenient(Name + ".");

            var dsResponse  = await dnsClient.Query(
                                        name,
                                        [DNSResourceRecordTypes.DS],
                                        CancellationToken: CancellationToken
                                    ).ConfigureAwait(false);

            if (!dsResponse.IsValid)
                return (NameBelowAnchor.Indeterminate, []);

            var dsRecords   = dsResponse.Answers.OfType<DS>().
                                  Where(ds => Normalize(ds.DomainName.FullName) == Name).
                                  ToList();

            if (dsRecords.Count > 0)
            {

                // The DS RRset is the zone's word, signed with the zone's keys
                // (RFC 4035 §5.2) — and with no other zone's.
                var dsRRSet = dsRecords.Cast<IDNSResourceRecord>().ToList();

                if (!Signatures(dsResponse, Name, DNSResourceRecordTypes.DS).
                         Any(rrsig => Normalize(rrsig.SignerName.FullName) == Zone &&
                                      WithinValidityWindow(rrsig, Now)              &&
                                      VerifiedByOneOf(dsRRSet, rrsig, ZoneKeys)))
                    return (NameBelowAnchor.Bogus, []);

                // RFC 6840 §5.2: an authenticated DS RRset naming nothing this
                // build can use leaves the delegation unsigned.
                if (!HasUsableDelegationSigner(dsRecords))
                    return (NameBelowAnchor.UnsignedDelegation, []);

                var keyResponse = await dnsClient.Query(
                                            name,
                                            [DNSResourceRecordTypes.DNSKEY],
                                            CancellationToken: CancellationToken
                                        ).ConfigureAwait(false);

                if (!keyResponse.IsValid)
                    return (NameBelowAnchor.Indeterminate, []);

                var keys = ApexKeys(keyResponse, Name);
                var sigs = Signatures(keyResponse, Name, DNSResourceRecordTypes.DNSKEY).
                               Where(rrsig => Normalize(rrsig.SignerName.FullName) == Name).
                               ToList();

                return KeySetSignedByAKeyNamedBy(keys, sigs, dsRecords.Where(IsUsableDelegationSigner), Now)
                           ? (NameBelowAnchor.SignedZone, keys)
                           : (NameBelowAnchor.Bogus,      []);

            }

            // No DS: the zone has to say why, in records it signed.
            var proof = DenialSignedBy(dsResponse, Zone, ZoneKeys, Now);

            if (DenialOfExistenceValidator.VerifyNoDelegationSigner(name, proof) is DenialOfExistence.NoDataForType
                                                                                  or DenialOfExistence.OptedOut)
                return (NameBelowAnchor.UnsignedDelegation, []);

            // Not a delegation without DS. If the name exists and holds no DS,
            // it is no zone cut — VerifyNoDelegationSigner would have taken the
            // NSEC of one — and the zone goes on below it. A name the zone says
            // does not exist cannot have the owner below it.
            return DenialOfExistenceValidator.Verify(name, DNSResourceRecordTypes.DS, proof) == DenialOfExistence.NoDataForType
                       ? (NameBelowAnchor.InTheZone, [])
                       : (NameBelowAnchor.Bogus,     []);

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
