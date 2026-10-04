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
    /// RFC 5011 believes a root DNSKEY RRset only once it is authenticated: a new
    /// key is added only "when that RRSet is validated by an existing trust
    /// anchor" (§2), and a revocation counts only when the revoked key signed the
    /// set itself (§2.1).
    /// </summary>
    /// <remarks>
    /// The probe used to verify no signature at all. An attacker on the path for
    /// thirty days planted a trust anchor of their own, and a single forged REVOKE
    /// removed a genuine one.
    /// </remarks>
    [TestFixture]
    public class DNSSECValidatorTrustAnchorProbe_Tests
    {

        #region Data

        private const UInt16 RevokeFlag = 0x0080;

        private static readonly DomainName Root = DomainName.Parse(".");

        /// <summary>
        /// The instant every probe here is made at, and the signatures' window
        /// around it: wide enough to still hold when the add hold-down has run.
        /// </summary>
        private static readonly DateTimeOffset Now         = DateTimeOffset.UtcNow;
        private static readonly DateTime       Inception   = Now.UtcDateTime.AddDays(-1);
        private static readonly DateTime       Expiration  = Now.UtcDateTime.AddDays(60);

        #endregion

        #region (private static) Helpers

        private static DNSSECSigningKey NewKSK()
            => DNSSECSigningKey.Generate(Root, 13, KeySigningKey: true);

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

        /// <summary>
        /// The signature an attacker without the private key can produce: every
        /// field the genuine one has, and zeros where the signature goes.
        /// </summary>
        private static RRSIG Forged(RRSIG Genuine)

            => new (Root, DNSQueryClasses.IN, Genuine.TimeToLive, Genuine.TypeCovered, Genuine.Algorithm, Genuine.Labels,
                    Genuine.OriginalTTL, Genuine.SignatureExpiration, Genuine.SignatureInception, Genuine.KeyTag, Genuine.SignerName,
                    new Byte[Genuine.Signature.Length]);

        private static CannedDNSClient Serving(IDNSResourceRecord[] RRSet, params RRSIG[] Signatures)
            => new CannedDNSClient().Answer(".", DNSResourceRecordTypes.DNSKEY, [ .. RRSet, .. Signatures ]);

        private static IEnumerable<UInt16> KeyTags(IEnumerable<DS> Anchors)
            => Anchors.Select(anchor => anchor.KeyTag);

        #endregion


        #region An_Anchor_Signed_Set_Starts_The_Hold_Down()

        /// <summary>
        /// The baseline every other add test takes something away from.
        /// </summary>
        [Test]
        public async Task An_Anchor_Signed_Set_Starts_The_Hold_Down()
        {

            using var anchor    = NewKSK();
            using var incoming  = NewKSK();

            IDNSResourceRecord[] keys = [ anchor.DNSKEY, incoming.DNSKEY ];

            var validator = new DNSSECValidator(Serving(keys, Sign(keys, anchor)), [ anchor.DelegationSigner() ]);

            var modified  = await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            Assert.Multiple(() => {

                Assert.That(modified, Is.False, "nothing is trusted yet");

                Assert.That(validator.PendingAnchors.Keys.Select(id => id.KeyTag), Is.EqualTo(new[] { incoming.KeyTag }),
                            "the newcomer, and only the newcomer, starts its hold-down");

                Assert.That(KeyTags(validator.TrustAnchors), Is.EqualTo(new[] { anchor.KeyTag }));

            });

        }

        #endregion

        #region The_Hold_Down_Runs_Out_On_Anchor_Signed_Sets()

        [Test]
        public async Task The_Hold_Down_Runs_Out_On_Anchor_Signed_Sets()
        {

            using var anchor    = NewKSK();
            using var incoming  = NewKSK();

            IDNSResourceRecord[] keys = [ anchor.DNSKEY, incoming.DNSKEY ];

            var validator = new DNSSECValidator(Serving(keys, Sign(keys, anchor)), [ anchor.DelegationSigner() ]);

            await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            Assert.That(await validator.ProbeForTrustAnchorUpdatesAsync(Now + DNSSECValidator.AddHoldDownTime), Is.True,
                        "thirty days of authenticated sightings admit the key");

            Assert.That(KeyTags(validator.TrustAnchors), Is.EquivalentTo(new[] { anchor.KeyTag, incoming.KeyTag }));

        }

        #endregion

        #region An_Unsigned_Set_Starts_No_Hold_Down()

        /// <summary>
        /// The attack in its simplest form: an answer with the attacker's key in
        /// it and no signature at all.
        /// </summary>
        [Test]
        public async Task An_Unsigned_Set_Starts_No_Hold_Down()
        {

            using var anchor    = NewKSK();
            using var attacker  = NewKSK();

            var validator = new DNSSECValidator(Serving([ anchor.DNSKEY, attacker.DNSKEY ]), [ anchor.DelegationSigner() ]);

            var modified  = await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            Assert.Multiple(() => {
                Assert.That(modified,                 Is.False);
                Assert.That(validator.PendingAnchors, Is.Empty, "a set nothing signed vouches for no key in it");
            });

        }

        #endregion

        #region A_Forged_Signature_Starts_No_Hold_Down()

        /// <summary>
        /// An RRSIG that names the anchor's key by tag, algorithm and signer, and
        /// does not verify. Finding one is not the same as checking it.
        /// </summary>
        [Test]
        public async Task A_Forged_Signature_Starts_No_Hold_Down()
        {

            using var anchor    = NewKSK();
            using var attacker  = NewKSK();

            IDNSResourceRecord[] keys = [ anchor.DNSKEY, attacker.DNSKEY ];

            var validator = new DNSSECValidator(Serving(keys, Forged(Sign(keys, anchor))), [ anchor.DelegationSigner() ]);

            await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            Assert.That(validator.PendingAnchors, Is.Empty);

        }

        #endregion

        #region A_Set_Signed_Only_By_The_Newcomer_Starts_No_Hold_Down()

        /// <summary>
        /// A genuine signature by the key that wants to be trusted proves only that
        /// whoever published it holds its private key — which the attacker does.
        /// </summary>
        [Test]
        public async Task A_Set_Signed_Only_By_The_Newcomer_Starts_No_Hold_Down()
        {

            using var anchor    = NewKSK();
            using var attacker  = NewKSK();

            IDNSResourceRecord[] keys = [ anchor.DNSKEY, attacker.DNSKEY ];

            var validator = new DNSSECValidator(Serving(keys, Sign(keys, attacker)), [ anchor.DelegationSigner() ]);

            await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            Assert.That(validator.PendingAnchors, Is.Empty);

        }

        #endregion

        #region An_Expired_Signature_Starts_No_Hold_Down()

        /// <summary>
        /// The validity window is checked at the probe's own Now: a set signed long
        /// ago and replayed is not authenticated today.
        /// </summary>
        [Test]
        public async Task An_Expired_Signature_Starts_No_Hold_Down()
        {

            using var anchor    = NewKSK();
            using var incoming  = NewKSK();

            IDNSResourceRecord[] keys = [ anchor.DNSKEY, incoming.DNSKEY ];

            var validator = new DNSSECValidator(Serving(keys, Sign(keys, anchor)), [ anchor.DelegationSigner() ]);

            await validator.ProbeForTrustAnchorUpdatesAsync(new DateTimeOffset(Expiration.AddDays(1)));

            Assert.That(validator.PendingAnchors, Is.Empty);

        }

        #endregion

        #region An_Unauthenticated_Set_Does_Not_Interrupt_A_Hold_Down()

        /// <summary>
        /// A set nothing authenticates changes nothing — including the pending key
        /// it leaves out. Otherwise one forged answer a month resets every rollover
        /// for good.
        /// </summary>
        [Test]
        public async Task An_Unauthenticated_Set_Does_Not_Interrupt_A_Hold_Down()
        {

            using var anchor    = NewKSK();
            using var incoming  = NewKSK();

            IDNSResourceRecord[] keys = [ anchor.DNSKEY, incoming.DNSKEY ];

            var resolver  = Serving(keys, Sign(keys, anchor));
            var validator = new DNSSECValidator(resolver, [ anchor.DelegationSigner() ]);

            await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            resolver.Answer(".", DNSResourceRecordTypes.DNSKEY, anchor.DNSKEY);

            Assert.That(await validator.ProbeForTrustAnchorUpdatesAsync(Now + TimeSpan.FromDays(1)), Is.False);

            Assert.That(validator.PendingAnchors.Keys.Select(id => id.KeyTag), Is.EqualTo(new[] { incoming.KeyTag }),
                        "the newcomer's hold-down is still running");

        }

        #endregion


        #region A_Self_Signed_Revocation_Removes_The_Anchor()

        /// <summary>
        /// A rollover's last step, as the root performs it: the new key is already
        /// an anchor and signs the set, and the old one signs it with its REVOKE
        /// bit set.
        /// </summary>
        [Test]
        public async Task A_Self_Signed_Revocation_Removes_The_Anchor()
        {

            using var old       = NewKSK();
            using var current   = NewKSK();

            IDNSResourceRecord[] keys = [ Revoked(old), current.DNSKEY ];

            var validator = new DNSSECValidator(Serving(keys, Sign(keys, current), SignAsRevoked(keys, old)),
                                                [ old.DelegationSigner(), current.DelegationSigner() ]);

            Assert.That(await validator.ProbeForTrustAnchorUpdatesAsync(Now), Is.True);
            Assert.That(KeyTags(validator.TrustAnchors), Is.EqualTo(new[] { current.KeyTag }));

        }

        #endregion

        #region A_Revocation_Not_Signed_By_The_Revoked_Key_Is_Ignored()

        /// <summary>
        /// RFC 5011 §2.1 exists so that one compromised anchor cannot remove the
        /// others: the set here is genuinely signed by an anchor, and still says
        /// nothing about another anchor's revocation unless that key signed it.
        /// </summary>
        [Test]
        public async Task A_Revocation_Not_Signed_By_The_Revoked_Key_Is_Ignored()
        {

            using var victim       = NewKSK();
            using var compromised  = NewKSK();

            IDNSResourceRecord[] keys = [ Revoked(victim), compromised.DNSKEY ];

            var validator = new DNSSECValidator(Serving(keys, Sign(keys, compromised)),
                                                [ victim.DelegationSigner(), compromised.DelegationSigner() ]);

            Assert.That(await validator.ProbeForTrustAnchorUpdatesAsync(Now), Is.False);
            Assert.That(KeyTags(validator.TrustAnchors), Is.EquivalentTo(new[] { victim.KeyTag, compromised.KeyTag }));

        }

        #endregion

        #region A_Revocation_In_An_Unsigned_Set_Is_Ignored()

        /// <summary>
        /// The other attack: one forged answer, the anchor's key with the REVOKE
        /// bit set, and the resolver is left without a trust anchor.
        /// </summary>
        [Test]
        public async Task A_Revocation_In_An_Unsigned_Set_Is_Ignored()
        {

            using var anchor = NewKSK();

            var validator = new DNSSECValidator(Serving([ Revoked(anchor) ]), [ anchor.DelegationSigner() ]);

            Assert.That(await validator.ProbeForTrustAnchorUpdatesAsync(Now), Is.False);
            Assert.That(KeyTags(validator.TrustAnchors), Is.EqualTo(new[] { anchor.KeyTag }));

        }

        #endregion

        #region A_Revocation_With_A_Forged_Self_Signature_Is_Ignored()

        [Test]
        public async Task A_Revocation_With_A_Forged_Self_Signature_Is_Ignored()
        {

            using var anchor = NewKSK();

            IDNSResourceRecord[] keys = [ Revoked(anchor) ];

            var validator = new DNSSECValidator(Serving(keys, Forged(SignAsRevoked(keys, anchor))), [ anchor.DelegationSigner() ]);

            Assert.That(await validator.ProbeForTrustAnchorUpdatesAsync(Now), Is.False);
            Assert.That(KeyTags(validator.TrustAnchors), Is.EqualTo(new[] { anchor.KeyTag }));

        }

        #endregion

        #region A_Self_Signed_Revocation_Vouches_For_Nothing_Else()

        /// <summary>
        /// §2.1 makes a revocation valid on the revoked key's own signature, so it
        /// takes effect even where no other anchor signed the set. But the revoked
        /// key may be used for nothing except that revocation: the other key in
        /// the set starts no hold-down on its word.
        /// </summary>
        [Test]
        public async Task A_Self_Signed_Revocation_Vouches_For_Nothing_Else()
        {

            using var anchor    = NewKSK();
            using var incoming  = NewKSK();

            IDNSResourceRecord[] keys = [ Revoked(anchor), incoming.DNSKEY ];

            var validator = new DNSSECValidator(Serving(keys, SignAsRevoked(keys, anchor)), [ anchor.DelegationSigner() ]);

            var modified  = await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            Assert.Multiple(() => {
                Assert.That(modified,                 Is.True, "the revocation took effect");
                Assert.That(validator.TrustAnchors,   Is.Empty);
                Assert.That(validator.PendingAnchors, Is.Empty, "and nothing else in the set did");
            });

        }

        #endregion

        #region A_Revoked_Key_Cannot_Come_Back()

        /// <summary>
        /// Revocation is permanent: the same key republished with REVOKE cleared,
        /// in a set an anchor signs, starts no new hold-down.
        /// </summary>
        [Test]
        public async Task A_Revoked_Key_Cannot_Come_Back()
        {

            using var old       = NewKSK();
            using var current   = NewKSK();

            IDNSResourceRecord[] revoking  = [ Revoked(old), current.DNSKEY ];
            IDNSResourceRecord[] returning = [ old.DNSKEY,   current.DNSKEY ];

            var resolver  = Serving(revoking, Sign(revoking, current), SignAsRevoked(revoking, old));
            var validator = new DNSSECValidator(resolver, [ old.DelegationSigner(), current.DelegationSigner() ]);

            await validator.ProbeForTrustAnchorUpdatesAsync(Now);

            resolver.Answer(".", DNSResourceRecordTypes.DNSKEY, [ .. returning, Sign(returning, current) ]);

            await validator.ProbeForTrustAnchorUpdatesAsync(Now + TimeSpan.FromDays(1));

            Assert.Multiple(() => {
                Assert.That(KeyTags(validator.TrustAnchors), Is.EqualTo(new[] { current.KeyTag }));
                Assert.That(validator.PendingAnchors,        Is.Empty);
            });

        }

        #endregion

    }

}
