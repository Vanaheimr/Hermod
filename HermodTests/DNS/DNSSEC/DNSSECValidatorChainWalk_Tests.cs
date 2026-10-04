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
    /// The step of a chain walk from a zone to its parent, through a zone that
    /// publishes more than one key-signing key.
    /// </summary>
    /// <remarks>
    /// Three zones, all signed for real: the root, <c>test.</c> and <c>leaf.test.</c>.
    /// The anchor is the root's key, so the walk has to cross <c>test.</c> by its DS
    /// rather than by an anchor.
    /// </remarks>
    [TestFixture]
    public class DNSSECValidatorChainWalk_Tests
    {

        #region (private) CannedDNSClient

        /// <summary>
        /// Answers each (name, type) with the records registered for it, and with an
        /// empty NOERROR otherwise.
        /// </summary>
        private sealed class CannedDNSClient : IDNSClient
        {

            private readonly Dictionary<(String, DNSResourceRecordTypes), IDNSResourceRecord[]> answers = [];

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.DNS);

            private static String Key(String Name)
                => Name.TrimEnd('.').ToLowerInvariant();

            public CannedDNSClient Answer(String Name, DNSResourceRecordTypes Type, params IDNSResourceRecord[] Records)
            {
                answers[(Key(Name), Type)] = Records;
                return this;
            }

            public Task<DNSInfo> Query(DomainName                           DomainName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout             = null,
                                       Boolean?                             RecursionDesired    = null,
                                       Boolean?                             ForceUpdate         = false,
                                       CancellationToken                    CancellationToken   = default)

                => Task.FromResult(Build(DomainName.FullName, ResourceRecordTypes));

            public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                       IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                       TimeSpan?                            Timeout             = null,
                                       Boolean?                             RecursionDesired    = null,
                                       Boolean?                             ForceUpdate         = false,
                                       CancellationToken                    CancellationToken   = default)

                => Task.FromResult(Build(DNSServiceName.FullName, ResourceRecordTypes));

            private DNSInfo Build(String Name, IEnumerable<DNSResourceRecordTypes> Types)

                => new (
                       Origin:                 origin,
                       QueryId:                0,
                       IsAuthoritativeAnswer:  true,
                       IsTruncated:            false,
                       RecursionDesired:       true,
                       RecursionAvailable:     true,
                       ResponseCode:           DNSResponseCodes.NoError,
                       Answers:                Types.SelectMany(type => answers.TryGetValue((Key(Name), type), out var records) ? records : []).ToArray(),
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

        #region (private) Sign(RRset, Key)

        private static RRSIG Sign(IEnumerable<IDNSResourceRecord> RRset, DNSSECSigningKey Key)

            => DNSSECZoneSigner.SignRRSet(
                   RRset,
                   Key,
                   DateTime.UtcNow.AddDays(-1),
                   DateTime.UtcNow.AddDays(14)
               );

        #endregion

        #region (private) Validate(TestKSKs, TestDS, RootKey)

        /// <summary>
        /// Build the three zones and validate an A record of the leaf.
        /// </summary>
        /// <param name="TestKSKs">The key-signing keys test. publishes, in this order. The last one signs its DNSKEY RRset.</param>
        /// <param name="TestDS">The DS the root publishes for test.</param>
        /// <param name="RootKey">The root's only key, and the trust anchor.</param>
        private static async Task<DNSSECValidationResult> Validate(DNSSECSigningKey[]  TestKSKs,
                                                                   DS                  TestDS,
                                                                   DNSSECSigningKey    RootKey)
        {

            using var leafKey   = DNSSECSigningKey.Generate(DomainName.Parse("leaf.test"), 13, KeySigningKey: true);
            using var testZSK   = DNSSECSigningKey.Generate(DomainName.Parse("test"),      13);

            var a               = new A(DomainName.Parse("www.leaf.test"), DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.1"));

            IDNSResourceRecord[] leafKeys  = [ leafKey.DNSKEY ];
            IDNSResourceRecord[] testKeys  = [ .. TestKSKs.Select(key => key.DNSKEY), testZSK.DNSKEY ];
            IDNSResourceRecord[] rootKeys  = [ RootKey.DNSKEY ];
            IDNSResourceRecord[] leafDS    = [ leafKey.DelegationSigner() ];
            IDNSResourceRecord[] testDS    = [ TestDS ];

            var resolver        = new CannedDNSClient().
                                      Answer("leaf.test", DNSResourceRecordTypes.DNSKEY, [ .. leafKeys, Sign(leafKeys, leafKey)        ]).
                                      Answer("leaf.test", DNSResourceRecordTypes.DS,     [ .. leafDS,   Sign(leafDS,   testZSK)        ]).
                                      Answer("test",      DNSResourceRecordTypes.DNSKEY, [ .. testKeys, Sign(testKeys, TestKSKs[^1])   ]).
                                      Answer("test",      DNSResourceRecordTypes.DS,     [ .. testDS,   Sign(testDS,   RootKey)        ]).
                                      Answer(".",         DNSResourceRecordTypes.DNSKEY, [ .. rootKeys, Sign(rootKeys, RootKey)        ]);

            var response        = new DNSInfo(
                                      Origin:                 new DNSServerConfig(IPv4Address.Localhost, IPPort.DNS),
                                      QueryId:                0,
                                      IsAuthoritativeAnswer:  true,
                                      IsTruncated:            false,
                                      RecursionDesired:       true,
                                      RecursionAvailable:     true,
                                      ResponseCode:           DNSResponseCodes.NoError,
                                      Answers:                [ a, Sign([ a ], leafKey) ],
                                      Authorities:            [],
                                      AdditionalRecords:      [],
                                      IsValid:                true,
                                      IsTimeout:              false,
                                      Timeout:                TimeSpan.FromSeconds(5),
                                      Runtime:                TimeSpan.Zero
                                  );

            return await new DNSSECValidator(resolver, [ RootKey.DelegationSigner() ]).
                             ValidateAsync(response);

        }

        #endregion


        #region A_Standby_Key_Signing_Key_Listed_First_Does_Not_Break_The_Chain()

        /// <summary>
        /// A zone in the middle of a KSK rollover publishes two keys with the SEP bit,
        /// and the DS above it names the one that signs. The walk used to check the DS
        /// against the first published SEP key only, and answered Bogus whenever the
        /// standby key came first — org. did, live, with keys 725 and 26974.
        /// RFC 4034 §2.1.1: "validators MUST NOT alter their behavior during the
        /// signature validation process in any way based on the setting of this bit."
        /// </summary>
        [Test]
        public async Task A_Standby_Key_Signing_Key_Listed_First_Does_Not_Break_The_Chain()
        {

            using var rootKey  = DNSSECSigningKey.Generate(DomainName.Parse("."),    13, KeySigningKey: true);
            using var standby  = DNSSECSigningKey.Generate(DomainName.Parse("test"), 13, KeySigningKey: true);
            using var active   = DNSSECSigningKey.Generate(DomainName.Parse("test"), 13, KeySigningKey: true);

            var result = await Validate([ standby, active ], active.DelegationSigner(), rootKey);

            Assert.That(result, Is.EqualTo(DNSSECValidationResult.Secure));

        }

        #endregion

        #region A_DS_That_Names_No_Published_Key_Is_Still_Bogus()

        /// <summary>
        /// The other half: looking at every key does not mean accepting any DS. A DS
        /// naming a key test. does not publish still breaks the chain.
        /// </summary>
        [Test]
        public async Task A_DS_That_Names_No_Published_Key_Is_Still_Bogus()
        {

            using var rootKey  = DNSSECSigningKey.Generate(DomainName.Parse("."),    13, KeySigningKey: true);
            using var standby  = DNSSECSigningKey.Generate(DomainName.Parse("test"), 13, KeySigningKey: true);
            using var active   = DNSSECSigningKey.Generate(DomainName.Parse("test"), 13, KeySigningKey: true);
            using var stranger = DNSSECSigningKey.Generate(DomainName.Parse("test"), 13, KeySigningKey: true);

            var result = await Validate([ standby, active ], stranger.DelegationSigner(), rootKey);

            Assert.That(result, Is.EqualTo(DNSSECValidationResult.Bogus));

        }

        #endregion

    }

}
