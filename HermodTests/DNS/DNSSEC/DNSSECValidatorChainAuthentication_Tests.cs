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
    /// Every link of a chain of trust is a signature, and every one of them has to
    /// verify: the DNSKEY RRset of each zone by a key a DS or the trust anchor
    /// names, and each DS RRset by a key of the parent (RFC 4035 §5.2).
    /// </summary>
    /// <remarks>
    /// Three zones, signed for real: the root, <c>test.</c> and <c>leaf.test.</c>,
    /// one key each. The anchor is the root's key. Each test breaks one link.
    /// </remarks>
    [TestFixture]
    public class DNSSECValidatorChainAuthentication_Tests
    {

        #region (private) Chain

        /// <summary>
        /// The three signed zones, and a resolver that serves them.
        /// </summary>
        private sealed class Chain : IDisposable
        {

            public DNSSECSigningKey  RootKey   { get; } = DNSSECSigningKey.Generate(DomainName.Parse("."),         13, KeySigningKey: true);
            public DNSSECSigningKey  TestKey   { get; } = DNSSECSigningKey.Generate(DomainName.Parse("test"),      13, KeySigningKey: true);
            public DNSSECSigningKey  LeafKey   { get; } = DNSSECSigningKey.Generate(DomainName.Parse("leaf.test"), 13, KeySigningKey: true);

            public A                 Address   { get; } = new (DomainName.Parse("www.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.1"));

            public CannedDNSClient   Resolver  { get; } = new ();

            private readonly DateTime inception;
            private readonly DateTime expiration;

            /// <param name="Inception">The start of every signature's validity window; yesterday when omitted.</param>
            /// <param name="Expiration">The end of every signature's validity window; in two weeks when omitted.</param>
            public Chain(DateTime? Inception = null, DateTime? Expiration = null)
            {

                inception   = Inception  ?? DateTime.UtcNow.AddDays(-1);
                expiration  = Expiration ?? DateTime.UtcNow.AddDays(14);

                PublishSigned(".",         DNSResourceRecordTypes.DNSKEY, [ RootKey.DNSKEY             ], RootKey);
                PublishSigned("test",      DNSResourceRecordTypes.DNSKEY, [ TestKey.DNSKEY             ], TestKey);
                PublishSigned("test",      DNSResourceRecordTypes.DS,     [ TestKey.DelegationSigner() ], RootKey);
                PublishSigned("leaf.test", DNSResourceRecordTypes.DNSKEY, [ LeafKey.DNSKEY             ], LeafKey);
                PublishSigned("leaf.test", DNSResourceRecordTypes.DS,     [ LeafKey.DelegationSigner() ], TestKey);

            }

            public RRSIG Sign(IEnumerable<IDNSResourceRecord> RRSet, DNSSECSigningKey Key, DateTime? Inception = null, DateTime? Expiration = null)

                => DNSSECZoneSigner.SignRRSet(RRSet, Key, Inception ?? inception, Expiration ?? expiration);

            public Chain Publish(String Name, DNSResourceRecordTypes Type, IDNSResourceRecord[] RRSet, params RRSIG[] Signatures)
            {
                Resolver.Answer(Name, Type, [ .. RRSet, .. Signatures ]);
                return this;
            }

            public Chain PublishSigned(String Name, DNSResourceRecordTypes Type, IDNSResourceRecord[] RRSet, DNSSECSigningKey Key)

                => Publish(Name, Type, RRSet, Sign(RRSet, Key));

            /// <summary>
            /// Validate the answer — by default the A record of www.leaf.test., signed by the leaf's key.
            /// </summary>
            public Task<DNSSECValidationResult> Validate(IDNSResourceRecord[]?  Answers  = null,
                                                         DateTimeOffset?        Now      = null)

                => new DNSSECValidator(Resolver, [ RootKey.DelegationSigner() ]).
                       ValidateAsync(Response(Answers ?? [ Address, Sign([ Address ], LeafKey) ]), Now);

            /// <summary>
            /// Validate a negative answer: an empty answer section and the given authority section.
            /// </summary>
            public Task<DNSSECValidationResult> ValidateDenial(IDNSResourceRecord[]    Authorities,
                                                               String                  QName,
                                                               DNSResourceRecordTypes  QType)

                => new DNSSECValidator(Resolver, [ RootKey.DelegationSigner() ]).
                       ValidateAsync(Response([], Authorities), (DomainName.Parse(QName), QType));

            public void Dispose()
            {
                RootKey.Dispose();
                TestKey.Dispose();
                LeafKey.Dispose();
            }

        }

        #endregion

        #region (private static) Response(Answers, Authorities) / Forged(Signature) / UnusableDS(Key)

        private static DNSInfo Response(IEnumerable<IDNSResourceRecord>   Answers,
                                        IEnumerable<IDNSResourceRecord>?  Authorities = null)

            => new (
                   Origin:                 new DNSServerConfig(IPv4Address.Localhost, IPPort.DNS),
                   QueryId:                0,
                   IsAuthoritativeAnswer:  true,
                   IsTruncated:            false,
                   RecursionDesired:       true,
                   RecursionAvailable:     true,
                   ResponseCode:           DNSResponseCodes.NoError,
                   Answers:                [ .. Answers ],
                   Authorities:            [ .. Authorities ?? [] ],
                   AdditionalRecords:      [],
                   IsValid:                true,
                   IsTimeout:              false,
                   Timeout:                TimeSpan.FromSeconds(5),
                   Runtime:                TimeSpan.Zero
               );

        /// <summary>
        /// The signature an attacker without the private key can produce: every
        /// field the genuine one has, key tag included, and zeros where the
        /// signature goes.
        /// </summary>
        private static RRSIG Forged(RRSIG Genuine)

            => new (DomainName.ParseLenient(Genuine.DomainName.FullName),
                    DNSQueryClasses.IN,
                    Genuine.TimeToLive,
                    Genuine.TypeCovered,
                    Genuine.Algorithm,
                    Genuine.Labels,
                    Genuine.OriginalTTL,
                    Genuine.SignatureExpiration,
                    Genuine.SignatureInception,
                    Genuine.KeyTag,
                    Genuine.SignerName,
                    new Byte[Genuine.Signature.Length]);

        /// <summary>
        /// A DS for the key that names an algorithm no validator implements
        /// (253 is reserved for private use), which RFC 6840 §5.2 says to disregard.
        /// </summary>
        private static DS UnusableDS(DNSSECSigningKey Key)
        {
            var ds = Key.DelegationSigner();
            return new DS(DomainName.ParseLenient(ds.DomainName.FullName), DNSQueryClasses.IN, ds.TimeToLive, ds.KeyTag, 253, ds.DigestType, ds.Digest);
        }

        #endregion


        #region An_Intact_Chain_Is_Secure()

        /// <summary>
        /// The baseline every other test breaks one link of.
        /// </summary>
        [Test]
        public async Task An_Intact_Chain_Is_Secure()
        {

            using var chain = new Chain();

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Secure));

        }

        #endregion

        #region A_Delegation_Chain_Made_Up_Below_The_Anchor_Is_Bogus()

        /// <summary>
        /// The attack in full: the attacker makes up a key for every zone below the
        /// anchor, publishes unsigned DS records naming them and a signature of
        /// zeros over every DNSKEY RRset, and signs the answer with their own leaf
        /// key. The root's public key is copied, which anyone can. This used to be
        /// Secure, because no signature above the answer was ever verified.
        /// </summary>
        [Test]
        public async Task A_Delegation_Chain_Made_Up_Below_The_Anchor_Is_Bogus()
        {

            using var rootKey   = DNSSECSigningKey.Generate(DomainName.Parse("."),         13, KeySigningKey: true);
            using var testKey   = DNSSECSigningKey.Generate(DomainName.Parse("test"),      13, KeySigningKey: true);
            using var leafKey   = DNSSECSigningKey.Generate(DomainName.Parse("leaf.test"), 13, KeySigningKey: true);

            static RRSIG Sign(IDNSResourceRecord[] RRSet, DNSSECSigningKey Key)
                => DNSSECZoneSigner.SignRRSet(RRSet, Key, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(14));

            IDNSResourceRecord[] rootKeys  = [ rootKey.DNSKEY ];
            IDNSResourceRecord[] testKeys  = [ testKey.DNSKEY ];
            IDNSResourceRecord[] leafKeys  = [ leafKey.DNSKEY ];
            var address                    = new A(DomainName.Parse("www.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.66"));

            var resolver = new CannedDNSClient().
                               Answer(".",         DNSResourceRecordTypes.DNSKEY, [ .. rootKeys, Forged(Sign(rootKeys, rootKey)) ]).
                               Answer("test",      DNSResourceRecordTypes.DNSKEY, [ .. testKeys, Forged(Sign(testKeys, testKey)) ]).
                               Answer("test",      DNSResourceRecordTypes.DS,     testKey.DelegationSigner()).
                               Answer("leaf.test", DNSResourceRecordTypes.DNSKEY, leafKeys).
                               Answer("leaf.test", DNSResourceRecordTypes.DS,     leafKey.DelegationSigner());

            var result   = await new DNSSECValidator(resolver, [ rootKey.DelegationSigner() ]).
                                     ValidateAsync(Response([ address, Sign([ address ], leafKey) ]));

            Assert.That(result, Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region A_Replayed_Root_Key_Set_Does_Not_Carry_A_Made_Up_Delegation()

        /// <summary>
        /// The same against a real root: the root's DNSKEY response, signature and
        /// all, is public and can be replayed as it is. What the attacker cannot
        /// produce is the root's signature over a DS for their key.
        /// </summary>
        [Test]
        public async Task A_Replayed_Root_Key_Set_Does_Not_Carry_A_Made_Up_Delegation()
        {

            using var chain     = new Chain();
            using var testKey   = DNSSECSigningKey.Generate(DomainName.Parse("test"),      13, KeySigningKey: true);
            using var leafKey   = DNSSECSigningKey.Generate(DomainName.Parse("leaf.test"), 13, KeySigningKey: true);

            chain.Publish      ("test",      DNSResourceRecordTypes.DS,     [ testKey.DelegationSigner() ]).
                  PublishSigned("test",      DNSResourceRecordTypes.DNSKEY, [ testKey.DNSKEY             ], testKey).
                  PublishSigned("leaf.test", DNSResourceRecordTypes.DS,     [ leafKey.DelegationSigner() ], testKey).
                  PublishSigned("leaf.test", DNSResourceRecordTypes.DNSKEY, [ leafKey.DNSKEY             ], leafKey);

            var result = await chain.Validate([ chain.Address, chain.Sign([ chain.Address ], leafKey) ]);

            Assert.That(result, Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region A_Forged_Signature_Over_A_DNSKEY_RRset_Is_Bogus()

        /// <summary>
        /// The DS names the key and the RRSIG names it too; neither says the key
        /// signed anything. Only verifying the signature does.
        /// </summary>
        [Test]
        public async Task A_Forged_Signature_Over_A_DNSKEY_RRset_Is_Bogus()
        {

            using var chain = new Chain();

            IDNSResourceRecord[] testKeys = [ chain.TestKey.DNSKEY ];
            chain.Publish("test", DNSResourceRecordTypes.DNSKEY, testKeys, Forged(chain.Sign(testKeys, chain.TestKey)));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region An_Unsigned_DS_RRset_Is_Bogus()

        /// <summary>
        /// The DS RRset is the parent's statement about the child, and without the
        /// parent's signature it is nobody's (RFC 4035 §5.2).
        /// </summary>
        [Test]
        public async Task An_Unsigned_DS_RRset_Is_Bogus()
        {

            using var chain = new Chain();

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, [ chain.LeafKey.DelegationSigner() ]);

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region A_DS_RRset_Signed_By_A_Key_The_Parent_Does_Not_Publish_Is_Bogus()

        /// <summary>
        /// A valid signature is not enough; it has to be one by a key of the
        /// parent's own DNSKEY RRset.
        /// </summary>
        [Test]
        public async Task A_DS_RRset_Signed_By_A_Key_The_Parent_Does_Not_Publish_Is_Bogus()
        {

            using var chain     = new Chain();
            using var stranger  = DNSSECSigningKey.Generate(DomainName.Parse("test"), 13, KeySigningKey: true);

            chain.PublishSigned("leaf.test", DNSResourceRecordTypes.DS, [ chain.LeafKey.DelegationSigner() ], stranger);

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region A_DNSKEY_RRset_Signed_Only_By_A_Key_The_DS_Does_Not_Name_Is_Bogus()

        /// <summary>
        /// The DS names a key the zone publishes, and that key signed the answer —
        /// but the DNSKEY RRset is signed only by another key, which nothing above
        /// vouches for. Then nothing vouches for the RRset either.
        /// </summary>
        [Test]
        public async Task A_DNSKEY_RRset_Signed_Only_By_A_Key_The_DS_Does_Not_Name_Is_Bogus()
        {

            using var chain  = new Chain();
            using var other  = DNSSECSigningKey.Generate(DomainName.Parse("leaf.test"), 13, KeySigningKey: true);

            chain.PublishSigned("leaf.test", DNSResourceRecordTypes.DNSKEY, [ chain.LeafKey.DNSKEY, other.DNSKEY ], other);

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region A_Trust_Anchor_Key_In_An_Unsigned_Key_Set_Is_Bogus()

        /// <summary>
        /// The anchor names the root's key, and the key is in the set. But a public
        /// key can be copied into any response; the set is the root's only if the
        /// key signed it.
        /// </summary>
        [Test]
        public async Task A_Trust_Anchor_Key_In_An_Unsigned_Key_Set_Is_Bogus()
        {

            using var chain = new Chain();

            chain.Publish(".", DNSResourceRecordTypes.DNSKEY, [ chain.RootKey.DNSKEY ]);

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region One_Valid_Signature_By_The_Named_Key_Is_Enough()

        /// <summary>
        /// RFC 4035 §5.3.1: every signature over the DNSKEY RRset is a candidate.
        /// A forged one and one by an unnamed key do not spoil the one that the
        /// named key really made — the shape of a KSK rollover, seen through an
        /// attacker who adds noise.
        /// </summary>
        [Test]
        public async Task One_Valid_Signature_By_The_Named_Key_Is_Enough()
        {

            using var chain  = new Chain();
            using var other  = DNSSECSigningKey.Generate(DomainName.Parse("leaf.test"), 13, KeySigningKey: true);

            IDNSResourceRecord[] leafKeys = [ other.DNSKEY, chain.LeafKey.DNSKEY ];

            chain.Publish("leaf.test",
                          DNSResourceRecordTypes.DNSKEY,
                          leafKeys,
                          Forged(chain.Sign(leafKeys, chain.LeafKey)),
                          chain.Sign(leafKeys, other),
                          chain.Sign(leafKeys, chain.LeafKey));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Secure));

        }

        #endregion

        #region An_Expired_Signature_Over_A_DNSKEY_RRset_Is_Bogus()

        /// <summary>
        /// RFC 4034 §3.1.5 applies to the chain's signatures as much as to the
        /// answer's: an expired RRSIG over a DNSKEY RRset authenticates nothing.
        /// </summary>
        [Test]
        public async Task An_Expired_Signature_Over_A_DNSKEY_RRset_Is_Bogus()
        {

            using var chain = new Chain();

            IDNSResourceRecord[] testKeys = [ chain.TestKey.DNSKEY ];
            chain.Publish("test", DNSResourceRecordTypes.DNSKEY, testKeys,
                          chain.Sign(testKeys, chain.TestKey, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-1)));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region An_Expired_Signature_Over_A_DS_RRset_Is_Bogus()

        [Test]
        public async Task An_Expired_Signature_Over_A_DS_RRset_Is_Bogus()
        {

            using var chain = new Chain();

            IDNSResourceRecord[] leafDS = [ chain.LeafKey.DelegationSigner() ];
            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, leafDS,
                          chain.Sign(leafDS, chain.TestKey, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-1)));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region Every_Signature_Of_The_Chain_Is_Checked_At_The_Same_Now()

        /// <summary>
        /// The caller's Now is the one instant every window is checked against, the
        /// anchor's end of the chain included. Every signature here is valid from
        /// ten to twenty days from now, except the root's over its own keys, whose
        /// window closes after twelve: on day eleven the chain holds, on day
        /// fifteen it breaks at the top, and today none of it is valid yet.
        /// </summary>
        [Test]
        public async Task Every_Signature_Of_The_Chain_Is_Checked_At_The_Same_Now()
        {

            var today = DateTime.UtcNow;

            using var chain = new Chain(today.AddDays(10), today.AddDays(20));

            IDNSResourceRecord[] rootKeys = [ chain.RootKey.DNSKEY ];
            chain.Publish(".", DNSResourceRecordTypes.DNSKEY, rootKeys,
                          chain.Sign(rootKeys, chain.RootKey, today.AddDays(10), today.AddDays(12)));

            var onDayEleven   = await chain.Validate(Now: new DateTimeOffset(today.AddDays(11)));
            var onDayFifteen  = await chain.Validate(Now: new DateTimeOffset(today.AddDays(15)));
            var beforeWindow  = await chain.Validate(Now: new DateTimeOffset(today));

            Assert.Multiple(() => {
                Assert.That(onDayEleven,   Is.EqualTo(DNSSECValidationResult.Secure));
                Assert.That(onDayFifteen,  Is.EqualTo(DNSSECValidationResult.Bogus));
                Assert.That(beforeWindow,  Is.EqualTo(DNSSECValidationResult.Bogus));
            });

        }

        #endregion

        #region A_Signed_DS_RRset_With_Only_Unusable_Records_Is_Insecure()

        /// <summary>
        /// RFC 6840 §5.2: an authenticated DS RRset that names nothing this build
        /// can use leaves the zone unsigned rather than broken.
        /// </summary>
        [Test]
        public async Task A_Signed_DS_RRset_With_Only_Unusable_Records_Is_Insecure()
        {

            using var chain = new Chain();

            chain.PublishSigned("leaf.test", DNSResourceRecordTypes.DS, [ UnusableDS(chain.LeafKey) ], chain.TestKey);

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Insecure));

        }

        #endregion

        #region An_Unsigned_DS_RRset_With_Only_Unusable_Records_Is_Bogus()

        /// <summary>
        /// The other half: §5.2 speaks of authenticated DS records. A forged DS
        /// naming an unknown algorithm must not be a way to downgrade a signed
        /// zone to Insecure.
        /// </summary>
        [Test]
        public async Task An_Unsigned_DS_RRset_With_Only_Unusable_Records_Is_Bogus()
        {

            using var chain = new Chain();

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, [ UnusableDS(chain.LeafKey) ]);

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion


        #region A_Zone_Cannot_Sign_For_A_Name_Outside_It()

        /// <summary>
        /// leaf.test.'s chain is intact and its key really made this signature —
        /// over an A record for www.bank.example. A zone speaks only for the names
        /// inside it: RFC 4035 §5.3.1, "the RRSIG RR's Signer's Name field MUST be
        /// the name of the zone that contains the RRset". Otherwise the owner of
        /// any signed zone could vouch for every name there is.
        /// </summary>
        [Test]
        public async Task A_Zone_Cannot_Sign_For_A_Name_Outside_It()
        {

            using var chain   = new Chain();
            var       address = new A(DomainName.Parse("www.bank.example"), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.66"));

            Assert.That(await chain.Validate([ address, chain.Sign([ address ], chain.LeafKey) ]),
                        Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion


        #region A_Signature_Over_Records_The_Answer_Does_Not_Hold_Signs_Nothing_In_It()

        /// <summary>
        /// The answer holds a forged A record and, beside it, a genuine signature
        /// over a TXT record of the zone that the answer does not hold. A signature
        /// with nothing to cover was skipped, and an answer whose every signature
        /// was skipped was Secure.
        /// </summary>
        /// <remarks>
        /// Insecure rather than Bogus: an RRset without a signature is what an
        /// unsigned zone sends, and whether www.leaf.test. lies in one is the same
        /// question as an answer with no RRSIG at all, which is Insecure as well.
        /// What it must not be is Secure.
        /// </remarks>
        [Test]
        public async Task A_Signature_Over_Records_The_Answer_Does_Not_Hold_Signs_Nothing_In_It()
        {

            using var chain   = new Chain();
            var       text    = new TXT(DomainName.Parse("www.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1), "v=genuine");
            var       forged  = new A  (DomainName.Parse("www.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.66"));

            Assert.That(await chain.Validate([ forged, chain.Sign([ text ], chain.LeafKey) ]),
                        Is.EqualTo(DNSSECValidationResult.Insecure));

        }

        #endregion

        #region An_Unsigned_RRset_Beside_A_Signed_One_Is_Not_Secure()

        /// <summary>
        /// One signed RRset does not vouch for the others in the same answer.
        /// </summary>
        [Test]
        public async Task An_Unsigned_RRset_Beside_A_Signed_One_Is_Not_Secure()
        {

            using var chain   = new Chain();
            var       forged  = new A(DomainName.Parse("mail.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.66"));

            Assert.That(await chain.Validate([ chain.Address, chain.Sign([ chain.Address ], chain.LeafKey), forged ]),
                        Is.EqualTo(DNSSECValidationResult.Insecure));

        }

        #endregion

        #region A_CNAME_Into_An_Unsigned_Zone_Is_Insecure()

        /// <summary>
        /// The legitimate shape of the same thing: a signed CNAME pointing into a
        /// zone that is not signed. The target's records come without signatures,
        /// and the answer is Insecure — not Bogus, which would make every signed
        /// name that points at an unsigned CDN unresolvable.
        /// </summary>
        [Test]
        public async Task A_CNAME_Into_An_Unsigned_Zone_Is_Insecure()
        {

            using var chain   = new Chain();
            var       alias   = new CNAME(DomainName.Parse("www.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1), DomainName.Parse("edge.cdn.example"));
            var       target  = new A    (DomainName.Parse("edge.cdn.example"), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.80"));

            Assert.That(await chain.Validate([ alias, chain.Sign([ alias ], chain.LeafKey), target ]),
                        Is.EqualTo(DNSSECValidationResult.Insecure));

        }

        #endregion

        #region A_CNAME_Synthesized_From_A_Signed_DNAME_Is_Secure()

        /// <summary>
        /// The one unsigned RRset a signed answer legitimately holds: the CNAME a
        /// server synthesizes from a DNAME (RFC 6672 §5.3.1). It is not signed and
        /// need not be, because the DNAME is, and the CNAME follows from it — when
        /// it does.
        /// </summary>
        [Test]
        public async Task A_CNAME_Synthesized_From_A_Signed_DNAME_Is_Secure()
        {

            using var chain   = new Chain();
            var       dname   = new DNAME(DomainName.Parse("old.leaf.test"),     DNSQueryClasses.IN, TimeSpan.FromHours(1), DomainName.Parse("leaf.test"));
            var       cname   = new CNAME(DomainName.Parse("www.old.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1), DomainName.Parse("www.leaf.test"));

            Assert.That(await chain.Validate([ dname, chain.Sign([ dname ], chain.LeafKey),
                                               cname,
                                               chain.Address, chain.Sign([ chain.Address ], chain.LeafKey) ]),
                        Is.EqualTo(DNSSECValidationResult.Secure));

        }

        #endregion

        #region A_CNAME_That_Does_Not_Follow_From_The_DNAME_Is_Not_Secure()

        /// <summary>
        /// The same DNAME, and an unsigned CNAME beside it that points somewhere
        /// the DNAME does not lead.
        /// </summary>
        [Test]
        public async Task A_CNAME_That_Does_Not_Follow_From_The_DNAME_Is_Not_Secure()
        {

            using var chain   = new Chain();
            var       dname   = new DNAME(DomainName.Parse("old.leaf.test"),     DNSQueryClasses.IN, TimeSpan.FromHours(1), DomainName.Parse("leaf.test"));
            var       cname   = new CNAME(DomainName.Parse("www.old.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1), DomainName.Parse("evil.example"));

            Assert.That(await chain.Validate([ dname, chain.Sign([ dname ], chain.LeafKey), cname ]),
                        Is.EqualTo(DNSSECValidationResult.Insecure));

        }

        #endregion


        #region A_Signed_NODATA_Proof_Is_Secure()

        /// <summary>
        /// The baseline for the denial path: www.leaf.test. exists with a TXT record
        /// only, and the zone's signed NSEC there says so.
        /// </summary>
        [Test]
        public async Task A_Signed_NODATA_Proof_Is_Secure()
        {

            using var chain  = new Chain();
            var       nsec   = new NSEC(DomainName.Parse("www.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1),
                                        DomainName.Parse("leaf.test"), ADNSResourceRecord.EncodeTypeBitMaps([ "TXT", "RRSIG", "NSEC" ]));

            Assert.That(await chain.ValidateDenial([ nsec, chain.Sign([ nsec ], chain.LeafKey) ], "www.leaf.test", DNSResourceRecordTypes.A),
                        Is.EqualTo(DNSSECValidationResult.Secure));

        }

        #endregion

        #region An_Unsigned_NSEC_Beside_A_Signed_One_Proves_Nothing()

        /// <summary>
        /// A genuine signed NSEC of the zone — replayed, it proves nothing about
        /// www.leaf.test. — and beside it an unsigned one that does. Only the first
        /// was checked, and the proof was then read from all of them.
        /// </summary>
        [Test]
        public async Task An_Unsigned_NSEC_Beside_A_Signed_One_Proves_Nothing()
        {

            using var chain    = new Chain();
            var       genuine  = new NSEC(DomainName.Parse("leaf.test"),     DNSQueryClasses.IN, TimeSpan.FromHours(1),
                                          DomainName.Parse("www.leaf.test"), ADNSResourceRecord.EncodeTypeBitMaps([ "SOA", "NS", "DNSKEY", "RRSIG", "NSEC" ]));
            var       forged   = new NSEC(DomainName.Parse("www.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1),
                                          DomainName.Parse("leaf.test"),     ADNSResourceRecord.EncodeTypeBitMaps([ "TXT", "RRSIG", "NSEC" ]));

            Assert.That(await chain.ValidateDenial([ genuine, chain.Sign([ genuine ], chain.LeafKey), forged ], "www.leaf.test", DNSResourceRecordTypes.A),
                        Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion


        #region (private static) DelegationNSEC(Child, BitMap) / NSEC3At(Name, Zone, BitMap) / NSEC3Covering(Name, Zone, OptOut)

        /// <summary>
        /// The parent's NSEC at the child's name, with the given types.
        /// </summary>
        private static NSEC DelegationNSEC(String Child, params String[] BitMap)

            => new (DomainName.Parse(Child), DNSQueryClasses.IN, TimeSpan.FromHours(1),
                    DomainName.Parse("zz." + Child), ADNSResourceRecord.EncodeTypeBitMaps(BitMap));

        /// <summary>
        /// The NSEC3 of the zone that matches the name: no salt, no extra iterations, opt-out set, as RFC 9276 has every chain look now.
        /// </summary>
        private static NSEC3 NSEC3At(String Name, String Zone, params String[] BitMap)

            => new (NSEC3.ComputeHashedOwnerName(DomainName.Parse(Name), DomainName.Parse(Zone), 0, []),
                    DNSQueryClasses.IN, TimeSpan.FromHours(1),
                    NSEC3.HashAlgorithmSHA1, 0x01, 0, [],
                    Step(NSEC3.ComputeHash(DomainName.Parse(Name), 0, []), +1),
                    ADNSResourceRecord.EncodeTypeBitMaps(BitMap));

        /// <summary>
        /// The NSEC3 of the zone whose span holds the hash of the name and nothing much else.
        /// </summary>
        private static NSEC3 NSEC3Covering(String Name, String Zone, Boolean OptOut)
        {

            var hash = NSEC3.ComputeHash(DomainName.Parse(Name), 0, []);
            var zone = DomainName.Parse(Zone).FullName.TrimStart('.');

            return new (DomainName.Parse($"{NSEC3.Base32HexEncode(Step(hash, -1))}.{zone}"),
                        DNSQueryClasses.IN, TimeSpan.FromHours(1),
                        NSEC3.HashAlgorithmSHA1, (Byte) (OptOut ? 0x01 : 0x00), 0, [],
                        Step(hash, +1),
                        ADNSResourceRecord.EncodeTypeBitMaps([ "NS", "DS", "RRSIG" ]));

        }

        /// <summary>
        /// The hash one above or below, as a 20-byte big-endian number.
        /// </summary>
        private static Byte[] Step(Byte[] Hash, Int32 Delta)
        {

            var value  = new System.Numerics.BigInteger(Hash, isUnsigned: true, isBigEndian: true) + Delta;
            var bytes  = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            var result = new Byte[Hash.Length];

            Array.Copy(bytes, 0, result, result.Length - bytes.Length, bytes.Length);

            return result;

        }

        #endregion


        #region A_Stripped_DS_Is_Bogus()

        /// <summary>
        /// The downgrade: test. holds a DS for leaf.test., and an attacker on the
        /// path answers the DS query with an empty NOERROR. Nothing proves the DS
        /// absent, and leaf.test. lies under the anchor. This used to be Insecure —
        /// for DANE, that is the TLSA records ignored.
        /// </summary>
        [Test]
        public async Task A_Stripped_DS_Is_Bogus()
        {

            using var chain = new Chain();

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region A_Signed_NSEC_Proving_No_DS_Is_Insecure()

        /// <summary>
        /// The legitimate unsigned delegation: test.'s signed NSEC at leaf.test.
        /// lists NS and no DS. That leaf.test. signs its own records anyway makes
        /// it an island the chain does not reach — Insecure, not Bogus.
        /// </summary>
        [Test]
        public async Task A_Signed_NSEC_Proving_No_DS_Is_Insecure()
        {

            using var chain = new Chain();
            var       nsec  = DelegationNSEC("leaf.test", "NS", "RRSIG", "NSEC");

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);
            chain.Resolver.Authority("leaf.test", DNSResourceRecordTypes.DS, nsec, chain.Sign([ nsec ], chain.TestKey));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Insecure));

        }

        #endregion

        #region An_OptOut_NSEC3_Span_Is_Insecure()

        /// <summary>
        /// RFC 5155 §8.6: test. is signed with NSEC3 and opt-out, and leaf.test.
        /// falls in an opt-out span — the closest encloser is the apex, and the
        /// next closer name is covered by a span with the Opt-Out flag. The zone
        /// declined to say whether a delegation is there; if one is, it is
        /// unsigned.
        /// </summary>
        [Test]
        public async Task An_OptOut_NSEC3_Span_Is_Insecure()
        {

            using var chain    = new Chain();
            var       apex     = NSEC3At      ("test", "test", "SOA", "NS", "DNSKEY", "RRSIG", "NSEC3PARAM");
            var       covering = NSEC3Covering("leaf.test", "test", OptOut: true);

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);
            chain.Resolver.Authority("leaf.test", DNSResourceRecordTypes.DS,
                                     apex,     chain.Sign([ apex     ], chain.TestKey),
                                     covering, chain.Sign([ covering ], chain.TestKey));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Insecure));

        }

        #endregion

        #region A_Span_Without_OptOut_Is_Bogus()

        /// <summary>
        /// The same span without the flag says leaf.test. does not exist in test.
        /// at all — which a zone whose keys were just fetched contradicts.
        /// </summary>
        [Test]
        public async Task A_Span_Without_OptOut_Is_Bogus()
        {

            using var chain    = new Chain();
            var       apex     = NSEC3At      ("test", "test", "SOA", "NS", "DNSKEY", "RRSIG", "NSEC3PARAM");
            var       covering = NSEC3Covering("leaf.test", "test", OptOut: false);

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);
            chain.Resolver.Authority("leaf.test", DNSResourceRecordTypes.DS,
                                     apex,     chain.Sign([ apex     ], chain.TestKey),
                                     covering, chain.Sign([ covering ], chain.TestKey));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region An_Unsigned_NSEC_Proving_No_DS_Is_Bogus()

        /// <summary>
        /// The proof an attacker can write: the right NSEC, without test.'s signature.
        /// </summary>
        [Test]
        public async Task An_Unsigned_NSEC_Proving_No_DS_Is_Bogus()
        {

            using var chain = new Chain();

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);
            chain.Resolver.Authority("leaf.test", DNSResourceRecordTypes.DS, DelegationNSEC("leaf.test", "NS", "RRSIG", "NSEC"));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region A_Proof_Signed_With_A_Key_Made_Up_For_The_Parent_Is_Bogus()

        /// <summary>
        /// The NSEC is signed, by a key the attacker made up for test. and serves
        /// as test.'s DNSKEY RRset. The signature verifies against that set; the
        /// set does not verify against the DS the root holds for test.
        /// </summary>
        [Test]
        public async Task A_Proof_Signed_With_A_Key_Made_Up_For_The_Parent_Is_Bogus()
        {

            using var chain     = new Chain();
            using var madeUp    = DNSSECSigningKey.Generate(DomainName.Parse("test"), 13, KeySigningKey: true);
            var       nsec      = DelegationNSEC("leaf.test", "NS", "RRSIG", "NSEC");

            chain.PublishSigned("test", DNSResourceRecordTypes.DNSKEY, [ madeUp.DNSKEY ], madeUp).
                  Publish      ("leaf.test", DNSResourceRecordTypes.DS, []);
            chain.Resolver.Authority("leaf.test", DNSResourceRecordTypes.DS, nsec, chain.Sign([ nsec ], madeUp));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region An_Unusable_DS_Signed_With_A_Key_Made_Up_For_The_Parent_Is_Bogus()

        /// <summary>
        /// The same against RFC 6840 §5.2: a DS naming algorithm 253 is disregarded
        /// only once it is authenticated, and a signature by a key made up for the
        /// parent authenticates nothing. This returned Insecure as soon as that
        /// signature verified against the made-up key, before the parent's keys
        /// were looked at.
        /// </summary>
        [Test]
        public async Task An_Unusable_DS_Signed_With_A_Key_Made_Up_For_The_Parent_Is_Bogus()
        {

            using var chain     = new Chain();
            using var madeUp    = DNSSECSigningKey.Generate(DomainName.Parse("test"), 13, KeySigningKey: true);

            chain.PublishSigned("test",      DNSResourceRecordTypes.DNSKEY, [ madeUp.DNSKEY ],              madeUp).
                  PublishSigned("leaf.test", DNSResourceRecordTypes.DS,     [ UnusableDS(chain.LeafKey) ], madeUp);

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region The_Child_Cannot_Deny_Its_Own_DS()

        /// <summary>
        /// leaf.test.'s apex NSEC never lists DS — DS lives in the parent — and the
        /// child's own key signed it. It is not the parent's word, and it is not a
        /// delegation either: SOA is set.
        /// </summary>
        [Test]
        public async Task The_Child_Cannot_Deny_Its_Own_DS()
        {

            using var chain = new Chain();
            var       nsec  = DelegationNSEC("leaf.test", "SOA", "NS", "DNSKEY", "RRSIG", "NSEC");

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);
            chain.Resolver.Authority("leaf.test", DNSResourceRecordTypes.DS, nsec, chain.Sign([ nsec ], chain.LeafKey));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region An_NSEC_Without_NS_Proves_No_Delegation()

        /// <summary>
        /// RFC 6840 §4.4: the matching NSEC must show NS, proving there is a
        /// delegation at all. The genuine NSEC of an ordinary name lists no DS
        /// either; replayed for a zone cut the attacker invented there, it would
        /// make their own key's signatures Insecure instead of Bogus.
        /// </summary>
        [Test]
        public async Task An_NSEC_Without_NS_Proves_No_Delegation()
        {

            using var chain = new Chain();
            var       nsec  = DelegationNSEC("leaf.test", "A", "RRSIG", "NSEC");

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);
            chain.Resolver.Authority("leaf.test", DNSResourceRecordTypes.DS, nsec, chain.Sign([ nsec ], chain.TestKey));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region An_OptOut_Span_Beneath_A_Delegation_Proves_Nothing()

        /// <summary>
        /// RFC 6840 §4.1: the root's NSEC3 records — genuinely signed by the root,
        /// which is above leaf.test. — with test. as the closest encloser and an
        /// opt-out span over leaf.test. But test. is a delegation in the root, and
        /// the root has nothing to say about names below it. Otherwise com.'s
        /// opt-out spans would make every zone under every signed .com zone
        /// Insecure.
        /// </summary>
        [Test]
        public async Task An_OptOut_Span_Beneath_A_Delegation_Proves_Nothing()
        {

            using var chain    = new Chain();
            var       cut      = NSEC3At      ("test", ".", "NS", "DS", "RRSIG");
            var       covering = NSEC3Covering("leaf.test", ".", OptOut: true);

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);
            chain.Resolver.Authority("leaf.test", DNSResourceRecordTypes.DS,
                                     cut,      chain.Sign([ cut      ], chain.RootKey),
                                     covering, chain.Sign([ covering ], chain.RootKey));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region NSEC3_Records_Of_Two_Zones_Do_Not_Add_Up_To_A_Proof()

        /// <summary>
        /// test.'s genuine apex NSEC3 as the closest encloser, and the root's
        /// genuine opt-out span over the hash of leaf.test. — the same parameters,
        /// as every chain without salt and extra iterations has. Each is signed by
        /// its zone and each zone is above leaf.test., and together they read as an
        /// opt-out proof neither zone ever published.
        /// </summary>
        [Test]
        public async Task NSEC3_Records_Of_Two_Zones_Do_Not_Add_Up_To_A_Proof()
        {

            using var chain    = new Chain();
            var       apex     = NSEC3At      ("test", "test", "SOA", "NS", "DNSKEY", "RRSIG", "NSEC3PARAM");
            var       covering = NSEC3Covering("leaf.test", ".", OptOut: true);

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);
            chain.Resolver.Authority("leaf.test", DNSResourceRecordTypes.DS,
                                     apex,     chain.Sign([ apex     ], chain.TestKey),
                                     covering, chain.Sign([ covering ], chain.RootKey));

            Assert.That(await chain.Validate(), Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

        #region A_Missing_DS_Outside_Every_Anchor_Is_Insecure()

        /// <summary>
        /// With an anchor for some other zone only, nothing said leaf.test. should
        /// be signed, and no proof could chain to anything: an empty DS answer is
        /// Insecure there, as it always was.
        /// </summary>
        [Test]
        public async Task A_Missing_DS_Outside_Every_Anchor_Is_Insecure()
        {

            using var chain     = new Chain();
            using var elsewhere = DNSSECSigningKey.Generate(DomainName.Parse("example"), 13, KeySigningKey: true);

            chain.Publish("leaf.test", DNSResourceRecordTypes.DS, []);

            var result = await new DNSSECValidator(chain.Resolver, [ elsewhere.DelegationSigner() ]).
                                   ValidateAsync(Response([ chain.Address, chain.Sign([ chain.Address ], chain.LeafKey) ]));

            Assert.That(result, Is.EqualTo(DNSSECValidationResult.Insecure));

        }

        #endregion

        #region An_Unusable_DS_Outside_Every_Anchor_Is_Insecure()

        /// <summary>
        /// The same for RFC 6840 §5.2: an unusable DS sends the walk on up to the
        /// anchor, and where there is none to reach, it must not end at the root
        /// as Bogus.
        /// </summary>
        [Test]
        public async Task An_Unusable_DS_Outside_Every_Anchor_Is_Insecure()
        {

            using var chain     = new Chain();
            using var elsewhere = DNSSECSigningKey.Generate(DomainName.Parse("example"), 13, KeySigningKey: true);

            chain.PublishSigned("leaf.test", DNSResourceRecordTypes.DS, [ UnusableDS(chain.LeafKey) ], chain.TestKey);

            var result = await new DNSSECValidator(chain.Resolver, [ elsewhere.DelegationSigner() ]).
                                   ValidateAsync(Response([ chain.Address, chain.Sign([ chain.Address ], chain.LeafKey) ]));

            Assert.That(result, Is.EqualTo(DNSSECValidationResult.Insecure));

        }

        #endregion

    }

}
