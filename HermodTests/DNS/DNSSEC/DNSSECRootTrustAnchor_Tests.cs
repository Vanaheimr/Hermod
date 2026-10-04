/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.DNSSEC
{

    /// <summary>
    /// The built-in IANA root trust anchors of WithRootTrustAnchor.
    /// </summary>
    /// <remarks>
    /// The root zone publishes KSK-2024 (key tag 38696) since 2025-01-11 and signs its
    /// DNSKEY RRset with it alone from 2026-10-11 on; KSK-2017 (key tag 20326) is then
    /// revoked and withdrawn. A validator anchored on KSK-2017 only answers Bogus for
    /// everything from that day, unless RFC 5011 probing has run for thirty days.
    /// The anchors are those of https://data.iana.org/root-anchors/root-anchors.xml.
    /// <para>
    /// The RFC 5011 probe believes a root DNSKEY RRset only once it is signed: by a
    /// key an anchor names, and for a revocation by the revoked key itself (§2.1).
    /// Nobody but the root holds the private halves of KSK-2017 and KSK-2024, so the
    /// probe tests here sign with keys of their own, generated in the root's shape:
    /// RSA/SHA-256 and anchored by their real DS. A set they do not sign is what a
    /// forged answer looks like, and changes nothing.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class DNSSECRootTrustAnchor_Tests
    {

        #region Data

        private const String KSK2017Digest = "E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D";
        private const String KSK2024Digest = "683D2D0ACB8C9B712A1948B27F741219298D0A450D612C483AF444A4C0FB2B16";

        private const String KSK2017PublicKey = "AwEAAaz/tAm8yTn4Mfeh5eyI96WSVexTBAvkMgJzkKTOiW1vkIbzxeF3+/4RgWOq7HrxRixHlFlExOLAJr5emLvN7SWXgnLh4+B5xQlNVz8Og8kvArMtNROxVQuCaSnIDdD5LKyWbRd2n9WGe2R8PzgCmr3EgVLrjyBxWezF0jLHwVN8efS3rCj/EWgvIWgb9tarpVUDK/b58Da+sqqls3eNbuv7pr+eoZG+SrDK6nWeL3c6H5Apxz7LjVc1uTIdsIXxuOLYA4/ilBmSVIzuDWfdRUfhHdY6+cn8HFRm+2hM8AnXGXws9555KrUB5qihylGa8subX2Nn6UwNR1AkUTV74bU=";
        private const String KSK2024PublicKey = "AwEAAa96jeuknZlaeSrvyAJj6ZHv28hhOKkx3rLGXVaC6rXTsDc449/cidltpkyGwCJNnOAlFNKF2jBosZBU5eeHspaQWOmOElZsjICMQMC3aeHbGiShvZsx4wMYSjH8e7Vrhbu6irwCzVBApESjbUdpWWmEnhathWu1jo+siFUiRAAxm9qyJNg/wOZqqzL/dL/q8PkcRU5oUKEpUge71M3ej2/7CPqpdVwuMoTvoB+ZOT4YeGyxMvHmbrxlFzGOHOijtzN+u1TQNatX2XBuzZNQ1K+s2CXkPIZo7s6JgZyvaBevYtxPvYLw4z9mR7K2vaF18UYH9Z9GNUUeayffKC73PYc=";

        private const UInt16 RevokeFlag = 0x0080;

        private static readonly DomainName Root = DomainName.Parse(".");

        /// <summary>
        /// The instant every probe here is made at, and the signatures' window around it.
        /// </summary>
        private static readonly DateTimeOffset Now         = DateTimeOffset.UtcNow;
        private static readonly DateTime       Inception   = Now.UtcDateTime.AddDays(-1);
        private static readonly DateTime       Expiration  = Now.UtcDateTime.AddDays(14);

        #endregion

        #region (private static) Signing helpers

        /// <summary>
        /// A key of the root, generated for the test: RSA/SHA-256 like KSK-2017 and
        /// KSK-2024, and a KSK unless asked for a ZSK.
        /// </summary>
        private static DNSSECSigningKey NewRootKey(Boolean KeySigningKey = true)
            => DNSSECSigningKey.Generate(Root, 8, KeySigningKey);

        private static RRSIG Sign(IDNSResourceRecord[] RRSet, DNSSECSigningKey Key)
            => DNSSECZoneSigner.SignRRSet(RRSet, Key, Inception, Expiration);

        /// <summary>
        /// The key as its owner publishes it to revoke it: the REVOKE bit set,
        /// which changes its key tag (RFC 5011 §2.1).
        /// </summary>
        private static DNSKEY Revoked(DNSSECSigningKey Key)

            => new (Root,
                    DNSQueryClasses.IN,
                    Key.DNSKEY.TimeToLive,
                    (UInt16) (Key.DNSKEY.Flags | RevokeFlag),
                    Key.DNSKEY.Protocol,
                    Key.DNSKEY.Algorithm,
                    Key.DNSKEY.PublicKey);

        /// <summary>
        /// The signature a revoked key makes over the RRset it is revoked in: the
        /// same private key, and the key tag of the revoked form.
        /// </summary>
        private static RRSIG SignAsRevoked(IDNSResourceRecord[] RRSet, DNSSECSigningKey Key)
        {

            var genuine  = Sign(RRSet, Key);
            var keyTag   = DNSSECValidator.ComputeKeyTag(Revoked(Key));

            RRSIG With(Byte[] Signature)
                => new (Root, DNSQueryClasses.IN, genuine.TimeToLive, genuine.TypeCovered, genuine.Algorithm, genuine.Labels,
                        genuine.OriginalTTL, genuine.SignatureExpiration, genuine.SignatureInception, keyTag, genuine.SignerName, Signature);

            return With(Key.Sign(DNSSECCanonical.SignedData(RRSet, With([]))));

        }

        #endregion

        #region (private) RootKey(Flags, PublicKey)

        private static DNSKEY RootKey(UInt16 Flags, String PublicKey)

            => new (DomainName.Parse("."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromDays(2),
                    Flags,
                    3,
                    8,
                    Convert.FromBase64String(PublicKey));

        #endregion

        #region (private) RootDNSKEYClient

        /// <summary>
        /// Answers every query with the given root DNSKEY RRset and its signatures.
        /// </summary>
        private sealed class RootDNSKEYClient(params IDNSResourceRecord[] Records) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.DNS);

            public Task<DNSInfo> Query(DomainName                           DomainName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout             = null,
                                       Boolean?                             RecursionDesired    = null,
                                       Boolean?                             ForceUpdate         = false,
                                       CancellationToken                    CancellationToken   = default)

                => Task.FromResult(Build());

            public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout             = null,
                                       Boolean?                             RecursionDesired    = null,
                                       Boolean?                             ForceUpdate         = false,
                                       CancellationToken                    CancellationToken   = default)

                => Task.FromResult(Build());

            private DNSInfo Build()

                => new (
                       Origin:                 origin,
                       QueryId:                0,
                       IsAuthoritativeAnswer:  true,
                       IsTruncated:            false,
                       RecursionDesired:       true,
                       RecursionAvailable:     true,
                       ResponseCode:           DNSResponseCodes.NoError,
                       Answers:                Records,
                       Authorities:            [],
                       AdditionalRecords:      [],
                       IsValid:                true,
                       IsTimeout:              false,
                       Timeout:                TimeSpan.FromSeconds(5),
                       Runtime:                TimeSpan.Zero
                   );

            public void Dispose()
            { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        #endregion


        #region WithRootTrustAnchor_Holds_KSK2017_And_KSK2024()

        /// <summary>
        /// Both anchors of IANA's root-anchors.xml, with their published digests.
        /// </summary>
        [Test]
        public void WithRootTrustAnchor_Holds_KSK2017_And_KSK2024()
        {

            var anchors = DNSSECValidator.WithRootTrustAnchor(new RootDNSKEYClient()).TrustAnchors;

            Assert.That(anchors.Select(anchor => (anchor.KeyTag, anchor.Algorithm, anchor.DigestType, Convert.ToHexString(anchor.Digest))),
                        Is.EquivalentTo(new[] {
                            ((UInt16) 20326, (Byte) 8, (Byte) 2, KSK2017Digest),
                            ((UInt16) 38696, (Byte) 8, (Byte) 2, KSK2024Digest)
                        }));

            Assert.That(anchors.All(anchor => anchor.DomainName.FullName.TrimEnd('.') == ""), Is.True);

        }

        #endregion

        #region The_KSK2024_Anchor_Matches_The_Published_KSK2024()

        /// <summary>
        /// The anchor is the digest of the key the root actually publishes — so the walk
        /// ends at it when KSK-2024 is the root's only key-signing key.
        /// </summary>
        [Test]
        public void The_KSK2024_Anchor_Matches_The_Published_KSK2024()
        {

            var ksk2024 = RootKey(257, KSK2024PublicKey);
            var anchor  = DNSSECValidator.WithRootTrustAnchor(new RootDNSKEYClient()).TrustAnchors.Single(anchor => anchor.KeyTag == 38696);

            Assert.That(DNSSECValidator.ComputeKeyTag(ksk2024), Is.EqualTo((UInt16) 38696));
            Assert.That(DNSSECValidator.VerifyDS(ksk2024, anchor), Is.True);

        }

        #endregion

        #region The_KSK2017_Anchor_Matches_The_Published_KSK2017()

        [Test]
        public void The_KSK2017_Anchor_Matches_The_Published_KSK2017()
        {

            var ksk2017 = RootKey(257, KSK2017PublicKey);
            var anchor  = DNSSECValidator.WithRootTrustAnchor(new RootDNSKEYClient()).TrustAnchors.Single(anchor => anchor.KeyTag == 20326);

            Assert.That(DNSSECValidator.ComputeKeyTag(ksk2017), Is.EqualTo((UInt16) 20326));
            Assert.That(DNSSECValidator.VerifyDS(ksk2017, anchor), Is.True);

        }

        #endregion

        #region KSK2024_Is_Not_Held_Down_As_A_New_Key()

        /// <summary>
        /// KSK-2024 is an anchor from the start, so an RFC 5011 probe of today's root
        /// DNSKEY RRset neither changes the anchors nor starts a hold-down for it.
        /// </summary>
        /// <remarks>
        /// The set holds the real KSK-2017 and KSK-2024, and is signed by a key the
        /// test generates and anchors beside the built-in two: the probe believes the
        /// set, and what is left to see is whether it knows KSK-2024 by the built-in
        /// anchor. An unsigned set would pass this test whatever the probe made of it.
        /// </remarks>
        [Test]
        public async Task KSK2024_Is_Not_Held_Down_As_A_New_Key()
        {

            using var signer = NewRootKey();

            IDNSResourceRecord[] keys = [ RootKey(257, KSK2017PublicKey), RootKey(257, KSK2024PublicKey), signer.DNSKEY ];

            var validator = DNSSECValidator.WithRootTrustAnchor(new RootDNSKEYClient([ .. keys, Sign(keys, signer) ]));
            validator.AddTrustAnchor(signer.DelegationSigner());

            var modified  = await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            Assert.Multiple(() => {
                Assert.That(modified,                                     Is.False);
                Assert.That(validator.PendingAnchors,                     Is.Empty, "neither KSK-2017 nor KSK-2024 is a newcomer");
                Assert.That(validator.TrustAnchors.Select(a => a.KeyTag), Is.EquivalentTo(new UInt16[] { 20326, 38696, signer.KeyTag }));
            });

        }

        #endregion

        #region Revoking_KSK2017_Leaves_KSK2024()

        /// <summary>
        /// RFC 5011 §2.1: when the root publishes KSK-2017 with the REVOKE bit, the probe
        /// removes that anchor — and only that one.
        /// </summary>
        /// <remarks>
        /// The set is the one the root publishes at the end of the rollover, the way
        /// it revoked KSK-2010 in 2018: the revoked old KSK, the new KSK and the ZSK,
        /// signed by the new KSK and by the old one under its revoked key tag. Both
        /// KSKs are generated stand-ins, since only the revoked key's own signature
        /// makes the revocation count.
        /// </remarks>
        [Test]
        public async Task Revoking_KSK2017_Leaves_KSK2024()
        {

            using var ksk2017  = NewRootKey();
            using var ksk2024  = NewRootKey();
            using var zsk      = NewRootKey(KeySigningKey: false);

            IDNSResourceRecord[] keys = [ Revoked(ksk2017), ksk2024.DNSKEY, zsk.DNSKEY ];

            var validator = new DNSSECValidator(new RootDNSKEYClient([ .. keys, Sign(keys, ksk2024), SignAsRevoked(keys, ksk2017) ]),
                                                [ ksk2017.DelegationSigner(), ksk2024.DelegationSigner() ]);

            var modified  = await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            Assert.Multiple(() => {
                Assert.That(modified,                                     Is.True);
                Assert.That(validator.PendingAnchors,                     Is.Empty);
                Assert.That(validator.TrustAnchors.Select(a => a.KeyTag), Is.EquivalentTo(new[] { ksk2024.KeyTag }));
            });

        }

        #endregion

        #region An_Unsigned_Revocation_Of_KSK2017_Removes_Nothing()

        /// <summary>
        /// The real KSK-2017 with the REVOKE bit and the real KSK-2024, signed by no one:
        /// anybody can send this, so the built-in anchors stay as they are.
        /// </summary>
        [Test]
        public async Task An_Unsigned_Revocation_Of_KSK2017_Removes_Nothing()
        {

            var validator = DNSSECValidator.WithRootTrustAnchor(new RootDNSKEYClient(
                                                                    RootKey(257 | RevokeFlag, KSK2017PublicKey),
                                                                    RootKey(257,              KSK2024PublicKey)
                                                                ));

            var modified  = await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            Assert.Multiple(() => {
                Assert.That(modified,                                     Is.False);
                Assert.That(validator.TrustAnchors.Select(a => a.KeyTag), Is.EquivalentTo(new UInt16[] { 20326, 38696 }));
            });

        }

        #endregion

        #region Removing_An_Anchor_Does_Not_Touch_The_Next_Validator()

        /// <summary>
        /// The built-in anchors are shared by every validator WithRootTrustAnchor creates;
        /// removing one from a validator must not remove it from the next.
        /// </summary>
        [Test]
        public void Removing_An_Anchor_Does_Not_Touch_The_Next_Validator()
        {

            var first = DNSSECValidator.WithRootTrustAnchor(new RootDNSKEYClient());

            Assert.That(first.RemoveTrustAnchor(20326, 8), Is.True);
            Assert.That(first.TrustAnchors.Select(a => a.KeyTag), Is.EquivalentTo(new UInt16[] { 38696 }));

            Assert.That(DNSSECValidator.WithRootTrustAnchor(new RootDNSKEYClient()).TrustAnchors.Select(a => a.KeyTag),
                        Is.EquivalentTo(new UInt16[] { 20326, 38696 }));

        }

        #endregion

    }

}
