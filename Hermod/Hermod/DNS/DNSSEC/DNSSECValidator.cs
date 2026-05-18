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

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// DNSSEC validation results.
    /// </summary>
    public enum DNSSECValidationResult
    {

        /// <summary>Signature verified, chain of trust intact.</summary>
        Secure,

        /// <summary>No DNSSEC signatures present.</summary>
        Insecure,

        /// <summary>Signature verification failed.</summary>
        Bogus,

        /// <summary>Validation could not be completed (e.g. network error fetching keys).</summary>
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
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <returns>True if the trust anchor set was modified.</returns>
        public async Task<Boolean> ProbeForTrustAnchorUpdatesAsync(CancellationToken CancellationToken = default)
        {

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
                    var isRevoked = (key.Flags & 0x0080) != 0;    // REVOKE flag (RFC 5011 Section 2.1)

                    // Process revocations
                    if (isRevoked && isSEP)
                    {

                        // Remove from active trust anchors
                        var removed = trustAnchors.RemoveAll(
                                          a => a.KeyTag    == keyTag &&
                                               a.Algorithm == key.Algorithm
                                      );

                        if (removed > 0)
                        {
                            revokedAnchors.Add(keyId);
                            modified = true;
                        }

                        // Also remove from pending
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
                                if (DateTimeOffset.UtcNow - pending.FirstSeen >= AddHoldDownTime)
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
                                pendingAnchors[keyId] = (ds, DateTimeOffset.UtcNow);
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
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<DNSSECValidationResult> ValidateAsync(DNSInfo            Response,
                                                                CancellationToken  CancellationToken = default)
        {

            try
            {

                // Extract RRSIG records from the answer section
                var rrsigRecords = Response.Answers.OfType<RRSIG>().ToList();

                if (rrsigRecords.Count == 0)
                    return DNSSECValidationResult.Insecure;

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
                    var now = (UInt32) DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (now < rrsig.SignatureInception || now > rrsig.SignatureExpiration)
                        return DNSSECValidationResult.Bogus;

                    // Fetch the DNSKEY for the signer zone
                    var dnskeyResponse = await dnsClient.Query(
                                                  DomainName.Parse(rrsig.SignerName.FullName),
                                                  [DNSResourceRecordTypes.DNSKEY],
                                                  CancellationToken: CancellationToken
                                              ).ConfigureAwait(false);

                    if (!dnskeyResponse.IsValid)
                        return DNSSECValidationResult.Indeterminate;

                    var dnskeys = dnskeyResponse.Answers.OfType<DNSKEY>().ToList();

                    // Find the DNSKEY matching the RRSIG KeyTag
                    var matchingKey = dnskeys.FirstOrDefault(
                                         key => ComputeKeyTag(key) == rrsig.KeyTag &&
                                                key.Algorithm      == rrsig.Algorithm
                                     );

                    if (matchingKey is null)
                        return DNSSECValidationResult.Bogus;

                    // Verify the RRSIG signature
                    var sigResult = ValidateRRSig(rrSet, rrsig, matchingKey);
                    if (sigResult != DNSSECValidationResult.Secure)
                        return sigResult;

                    // Walk the chain of trust upward
                    var chainResult = await WalkChainOfTrust(
                                               rrsig.SignerName,
                                               dnskeys,
                                               matchingKey,
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
                var signedData = BuildSignedData(RRSet, Signature);

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
        {

            // DNSKEY RDATA: Flags (2 bytes) + Protocol (1 byte) + Algorithm (1 byte) + PublicKey
            var rdataLength = 4 + Key.PublicKey.Length;
            var rdata       = new Byte[rdataLength];

            BinaryPrimitives.WriteUInt16BigEndian(rdata.AsSpan(0), Key.Flags);
            rdata[2] = Key.Protocol;
            rdata[3] = Key.Algorithm;
            Array.Copy(Key.PublicKey, 0, rdata, 4, Key.PublicKey.Length);

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


        #region (private) WalkChainOfTrust(SignerName, DNSKeys, SigningKey, CancellationToken)

        /// <summary>
        /// Walk the chain of trust from the signer zone up to a trust anchor.
        /// </summary>
        private async Task<DNSSECValidationResult> WalkChainOfTrust(DomainName         SignerName,
                                                                    List<DNSKEY>       DNSKeys,
                                                                    DNSKEY             SigningKey,
                                                                    CancellationToken  CancellationToken)
        {

            var currentZone     = SignerName.FullName;
            var currentDNSKeys  = DNSKeys;
            var currentKey      = SigningKey;

            // Limit chain walk depth to prevent infinite loops
            for (var depth = 0; depth < 20; depth++)
            {

                // Check if the current key is a trust anchor
                var keyTag = ComputeKeyTag(currentKey);

                foreach (var anchor in trustAnchors)
                {
                    if (anchor.KeyTag    == keyTag &&
                        anchor.Algorithm == currentKey.Algorithm)
                    {
                        // Verify the trust anchor DS against the key
                        if (VerifyDS(currentKey, anchor))
                            return DNSSECValidationResult.Secure;
                    }
                }

                // If the signing key is a ZSK (Zone Signing Key), we need to find the KSK
                // that signed the DNSKEY RRSet and verify the DS for the KSK.
                // A KSK has bit 8 (Secure Entry Point) set: Flags & 0x0001 == 1
                var ksk = currentDNSKeys.FirstOrDefault(
                              key => (key.Flags & 0x0001) == 1 &&
                                     key.Algorithm == currentKey.Algorithm
                          );

                if (ksk is null)
                    ksk = currentKey;

                var kskKeyTag = ComputeKeyTag(ksk);

                // Check if the KSK is a trust anchor
                foreach (var anchor in trustAnchors)
                {
                    if (anchor.KeyTag    == kskKeyTag &&
                        anchor.Algorithm == ksk.Algorithm)
                    {
                        if (VerifyDS(ksk, anchor))
                            return DNSSECValidationResult.Secure;
                    }
                }

                // Walk to the parent zone and fetch the DS record
                var parentZone = GetParentZone(currentZone);
                if (parentZone is null)
                    return DNSSECValidationResult.Bogus;

                // Fetch the DS record for the current zone from the parent
                var dsResponse = await dnsClient.Query(
                                          DomainName.Parse(currentZone),
                                          [DNSResourceRecordTypes.DS],
                                          CancellationToken: CancellationToken
                                      ).ConfigureAwait(false);

                if (!dsResponse.IsValid)
                    return DNSSECValidationResult.Indeterminate;

                var dsRecords = dsResponse.Answers.OfType<DS>().ToList();

                if (dsRecords.Count == 0)
                    return DNSSECValidationResult.Insecure;

                // Verify the KSK against at least one DS record
                var dsVerified = dsRecords.Any(ds => VerifyDS(ksk, ds));

                if (!dsVerified)
                    return DNSSECValidationResult.Bogus;

                // Now move up: fetch the parent zone's DNSKEY records
                var parentDnskeyResponse = await dnsClient.Query(
                                                    DomainName.Parse(parentZone),
                                                    [DNSResourceRecordTypes.DNSKEY],
                                                    CancellationToken: CancellationToken
                                                ).ConfigureAwait(false);

                if (!parentDnskeyResponse.IsValid)
                    return DNSSECValidationResult.Indeterminate;

                var parentDnskeys = parentDnskeyResponse.Answers.OfType<DNSKEY>().ToList();

                if (parentDnskeys.Count == 0)
                    return DNSSECValidationResult.Indeterminate;

                // Verify the DNSKEY RRSet in the parent is signed
                var parentRrsigs = parentDnskeyResponse.Answers.OfType<RRSIG>()
                                       .Where(rr => rr.TypeCovered == DNSResourceRecordTypes.DNSKEY)
                                       .ToList();

                if (parentRrsigs.Count == 0)
                    return DNSSECValidationResult.Insecure;

                // Find the DNSKEY that signed the parent DNSKEY RRSet
                DNSKEY? parentSigningKey = null;
                foreach (var parentRrsig in parentRrsigs)
                {
                    parentSigningKey = parentDnskeys.FirstOrDefault(
                                          key => ComputeKeyTag(key) == parentRrsig.KeyTag &&
                                                 key.Algorithm      == parentRrsig.Algorithm
                                      );

                    if (parentSigningKey is not null)
                        break;
                }

                if (parentSigningKey is null)
                    return DNSSECValidationResult.Bogus;

                // Move up the chain
                currentZone    = parentZone;
                currentDNSKeys = parentDnskeys;
                currentKey     = parentSigningKey;

            }

            // Chain walk depth exceeded
            return DNSSECValidationResult.Indeterminate;

        }

        #endregion

        #region (private) BuildSignedData(RRSet, Signature)

        /// <summary>
        /// Build the signed data for RRSIG verification (RFC 4034 Section 3.1.8.1):
        /// RRSIG RDATA (without signature) + canonical sorted RRSet in wire format.
        /// </summary>
        private static Byte[] BuildSignedData(IEnumerable<IDNSResourceRecord>  RRSet,
                                              RRSIG                            Signature)
        {

            using var stream = new MemoryStream();

            // Write RRSIG RDATA fields (without the signature itself)
            stream.WriteUInt16BE((UInt16) Signature.TypeCovered);
            stream.WriteByte    (Signature.Algorithm);
            stream.WriteByte    (Signature.Labels);
            stream.WriteUInt32BE(Signature.OriginalTTL);
            stream.WriteUInt32BE(Signature.SignatureExpiration);
            stream.WriteUInt32BE(Signature.SignatureInception);
            stream.WriteUInt16BE(Signature.KeyTag);

            // Signer name in canonical (lowercased) wire format without compression
            var signerNameWire = SerializeCanonicalName(Signature.SignerName.FullName);
            stream.Write(signerNameWire, 0, signerNameWire.Length);

            // Write the RRSet in canonical order
            // Each RR: name | type | class | original TTL | RDLENGTH | RDATA
            // Names must be lowercased (canonical form)
            var canonicalRRs = new List<Byte[]>();

            foreach (var rr in RRSet)
            {
                using var rrStream = new MemoryStream();

                // Owner name in canonical wire format
                var ownerWire = SerializeCanonicalName(rr.DomainName.FullName);
                rrStream.Write(ownerWire, 0, ownerWire.Length);

                // Type (2 bytes)
                rrStream.WriteUInt16BE((UInt16) Signature.TypeCovered);

                // Class (2 bytes)
                rrStream.WriteUInt16BE((UInt16) rr.Class);

                // Original TTL from RRSIG (not the actual TTL)
                rrStream.WriteUInt32BE(Signature.OriginalTTL);

                // RDLENGTH + RDATA: serialize the full RR and extract RDATA
                var rdataBytes = ExtractRData(rr);
                rrStream.WriteUInt16BE((UInt16) rdataBytes.Length);
                rrStream.Write(rdataBytes, 0, rdataBytes.Length);

                canonicalRRs.Add(rrStream.ToArray());
            }

            // Sort the RRs in canonical order (byte-by-byte comparison)
            canonicalRRs.Sort(CompareByteArrays);

            // Write sorted RRs to the signed data stream
            foreach (var rr in canonicalRRs)
                stream.Write(rr, 0, rr.Length);

            return stream.ToArray();

        }

        #endregion

        #region (private static) ExtractRData(ResourceRecord)

        /// <summary>
        /// Extract the RDATA portion of a resource record by serializing it
        /// and stripping the header (name + type + class + TTL + RDLENGTH).
        /// </summary>
        private static Byte[] ExtractRData(IDNSResourceRecord ResourceRecord)
        {

            using var fullStream = new MemoryStream();

            // Serialize the full resource record without compression
            ResourceRecord.Serialize(fullStream, UseCompression: false);

            var fullBytes = fullStream.ToArray();

            // Parse past the owner name to find where RDATA begins
            var offset = 0;

            // Skip the wire-format owner name
            while (offset < fullBytes.Length)
            {
                var labelLen = fullBytes[offset];

                if (labelLen == 0)
                {
                    offset++; // skip the null terminator
                    break;
                }

                if ((labelLen & 0xC0) == 0xC0)
                {
                    offset += 2; // compression pointer
                    break;
                }

                offset += 1 + labelLen;
            }

            // Skip Type (2) + Class (2) + TTL (4) = 8 bytes
            offset += 8;

            // Read RDLENGTH
            if (offset + 2 > fullBytes.Length)
                return [];

            var rdLength = (UInt16) ((fullBytes[offset] << 8) | fullBytes[offset + 1]);
            offset += 2;

            // Extract RDATA
            if (offset + rdLength > fullBytes.Length)
                return fullBytes[offset..];

            return fullBytes[offset..(offset + rdLength)];

        }

        #endregion

        #region (private static) VerifySignature(Algorithm, PublicKey, Data, Signature)

        /// <summary>
        /// Verify a cryptographic signature using the appropriate algorithm.
        /// </summary>
        private static Boolean VerifySignature(Byte    Algorithm,
                                               Byte[]  PublicKey,
                                               Byte[]  Data,
                                               Byte[]  Signature)
        {

            return Algorithm switch {

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
        private static Byte[] SerializeCanonicalName(String Name)
        {

            using var stream = new MemoryStream();

            // Normalize: lowercase, ensure trailing dot is handled
            var normalized = Name.ToLowerInvariant().TrimEnd('.');

            if (String.IsNullOrEmpty(normalized) || normalized == ".")
            {
                stream.WriteByte(0x00);
                return stream.ToArray();
            }

            var labels = normalized.Split('.');

            foreach (var label in labels)
            {
                var labelBytes = Encoding.ASCII.GetBytes(label);
                stream.WriteByte((Byte) labelBytes.Length);
                stream.Write(labelBytes, 0, labelBytes.Length);
            }

            // Null terminator (root label)
            stream.WriteByte(0x00);

            return stream.ToArray();

        }

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

        #region (private static) CompareByteArrays(A, B)

        /// <summary>
        /// Compare two byte arrays lexicographically for canonical RRSet ordering.
        /// </summary>
        private static Int32 CompareByteArrays(Byte[]  A,
                                               Byte[]  B)
        {

            var minLength = Math.Min(A.Length, B.Length);

            for (var i = 0; i < minLength; i++)
            {
                if (A[i] < B[i]) return -1;
                if (A[i] > B[i]) return  1;
            }

            return A.Length.CompareTo(B.Length);

        }

        #endregion

    }

}
