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
    /// </remarks>
    [TestFixture]
    public class DNSSECRootTrustAnchor_Tests
    {

        #region Data

        private const String KSK2017Digest = "E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D";
        private const String KSK2024Digest = "683D2D0ACB8C9B712A1948B27F741219298D0A450D612C483AF444A4C0FB2B16";

        private const String KSK2017PublicKey = "AwEAAaz/tAm8yTn4Mfeh5eyI96WSVexTBAvkMgJzkKTOiW1vkIbzxeF3+/4RgWOq7HrxRixHlFlExOLAJr5emLvN7SWXgnLh4+B5xQlNVz8Og8kvArMtNROxVQuCaSnIDdD5LKyWbRd2n9WGe2R8PzgCmr3EgVLrjyBxWezF0jLHwVN8efS3rCj/EWgvIWgb9tarpVUDK/b58Da+sqqls3eNbuv7pr+eoZG+SrDK6nWeL3c6H5Apxz7LjVc1uTIdsIXxuOLYA4/ilBmSVIzuDWfdRUfhHdY6+cn8HFRm+2hM8AnXGXws9555KrUB5qihylGa8subX2Nn6UwNR1AkUTV74bU=";
        private const String KSK2024PublicKey = "AwEAAa96jeuknZlaeSrvyAJj6ZHv28hhOKkx3rLGXVaC6rXTsDc449/cidltpkyGwCJNnOAlFNKF2jBosZBU5eeHspaQWOmOElZsjICMQMC3aeHbGiShvZsx4wMYSjH8e7Vrhbu6irwCzVBApESjbUdpWWmEnhathWu1jo+siFUiRAAxm9qyJNg/wOZqqzL/dL/q8PkcRU5oUKEpUge71M3ej2/7CPqpdVwuMoTvoB+ZOT4YeGyxMvHmbrxlFzGOHOijtzN+u1TQNatX2XBuzZNQ1K+s2CXkPIZo7s6JgZyvaBevYtxPvYLw4z9mR7K2vaF18UYH9Z9GNUUeayffKC73PYc=";

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
        /// Answers every query with the given root DNSKEY RRset.
        /// </summary>
        private sealed class RootDNSKEYClient(params DNSKEY[] Keys) : IDNSClient
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
                       Answers:                Keys,
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
        [Test]
        public async Task KSK2024_Is_Not_Held_Down_As_A_New_Key()
        {

            var validator = DNSSECValidator.WithRootTrustAnchor(new RootDNSKEYClient(
                                                                    RootKey(257, KSK2017PublicKey),
                                                                    RootKey(257, KSK2024PublicKey)
                                                                ));

            var modified  = await validator.ProbeForTrustAnchorUpdatesAsync();

            Assert.That(modified,                                         Is.False);
            Assert.That(validator.PendingAnchors,                         Is.Empty);
            Assert.That(validator.TrustAnchors.Select(a => a.KeyTag),     Is.EquivalentTo(new UInt16[] { 20326, 38696 }));

        }

        #endregion

        #region Revoking_KSK2017_Leaves_KSK2024()

        /// <summary>
        /// RFC 5011 §2.1: when the root publishes KSK-2017 with the REVOKE bit, the probe
        /// removes that anchor — and only that one.
        /// </summary>
        [Test]
        public async Task Revoking_KSK2017_Leaves_KSK2024()
        {

            var validator = DNSSECValidator.WithRootTrustAnchor(new RootDNSKEYClient(
                                                                    RootKey(257 | 0x0080, KSK2017PublicKey),
                                                                    RootKey(257,          KSK2024PublicKey)
                                                                ));

            var modified  = await validator.ProbeForTrustAnchorUpdatesAsync();

            Assert.That(modified,                                         Is.True);
            Assert.That(validator.PendingAnchors,                         Is.Empty);
            Assert.That(validator.TrustAnchors.Select(a => a.KeyTag),     Is.EquivalentTo(new UInt16[] { 38696 }));

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
