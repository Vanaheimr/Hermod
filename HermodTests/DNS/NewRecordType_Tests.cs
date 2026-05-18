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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    /// <summary>
    /// Integration tests for the new DNS resource record types.
    /// Uses Google Public DNS (UDP) to query well-known domains with stable records.
    /// </summary>
    [TestFixture]
    public class NewRecordType_GoogleUDP_Tests
    {

        private IDNSClientWithTransport? client;

        [OneTimeSetUp]
        public void InitTests()
        {
            client = DNSUDPClient.Google_IPv4_1(QueryTimeout: TimeSpan.FromSeconds(10));
        }

        [OneTimeTearDown]
        public void ShutdownTests()
        {
            client?.Dispose();
        }


        // ───────────────── CAA (RFC 8659) ─────────────────

        #region Test_cloudflare_com__CAA()

        [Test]
        public async Task Test_cloudflare_com__CAA()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<CAA>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,            Is.Not.Null);
            Assert.That(response.IsValid,    Is.True);
            Assert.That(response.Answers.Count, Is.GreaterThanOrEqualTo(1));

            var caaRecords = response.Answers.OfType<CAA>().ToList();
            Assert.That(caaRecords.Count, Is.GreaterThanOrEqualTo(1));

            foreach (var caa in caaRecords)
            {
                Assert.That(caa.Type,  Is.EqualTo(DNSResourceRecordTypes.CAA));
                Assert.That(caa.Class, Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(caa.Tag,   Is.Not.Null.And.Not.Empty);
                Assert.That(caa.Value, Is.Not.Null.And.Not.Empty);

                // CAA flags should be 0 or 128 (critical)
                Assert.That(caa.Flags, Is.LessThanOrEqualTo((Byte) 128));
            }

            // Cloudflare should have at least an "issue" or "issuewild" tag
            var tags = caaRecords.Select(c => c.Tag.ToLower()).ToHashSet();
            Assert.That(tags.Contains("issue") || tags.Contains("issuewild"), Is.True,
                        "Expected at least an 'issue' or 'issuewild' CAA record for cloudflare.com");

        }

        #endregion


        // ───────────────── DNSKEY (RFC 4034) ─────────────────

        #region Test_cloudflare_com__DNSKEY()

        [Test]
        public async Task Test_cloudflare_com__DNSKEY()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<DNSKEY>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,            Is.Not.Null);
            Assert.That(response.IsValid,    Is.True);
            Assert.That(response.Answers.Count, Is.GreaterThanOrEqualTo(1));

            var dnskeyRecords = response.Answers.OfType<DNSKEY>().ToList();
            Assert.That(dnskeyRecords.Count, Is.GreaterThanOrEqualTo(1));

            foreach (var key in dnskeyRecords)
            {
                Assert.That(key.Type,      Is.EqualTo(DNSResourceRecordTypes.DNSKEY));
                Assert.That(key.Class,     Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(key.Protocol,  Is.EqualTo((Byte) 3), "DNSKEY protocol must always be 3");
                Assert.That(key.PublicKey,  Is.Not.Null);
                Assert.That(key.PublicKey.Length, Is.GreaterThan(0));

                // Algorithm should be a known DNSSEC algorithm (8, 10, 13, 14, 15, 16)
                Assert.That(new Byte[] { 8, 10, 13, 14, 15, 16 }, Does.Contain(key.Algorithm));

                // Flags: 256 (ZSK) or 257 (KSK/SEP)
                Assert.That(key.Flags == 256 || key.Flags == 257, Is.True,
                            $"Expected DNSKEY flags 256 (ZSK) or 257 (KSK), got {key.Flags}");
            }

            // There should be at least one KSK (Flags == 257)
            Assert.That(dnskeyRecords.Any(k => k.Flags == 257), Is.True,
                        "Expected at least one KSK (flags 257) for cloudflare.com");

        }

        #endregion


        // ───────────────── DS (RFC 4034) ─────────────────

        #region Test_cloudflare_com__DS()

        [Test]
        public async Task Test_cloudflare_com__DS()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<DS>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,            Is.Not.Null);
            Assert.That(response.IsValid,    Is.True);
            Assert.That(response.Answers.Count, Is.GreaterThanOrEqualTo(1));

            var dsRecords = response.Answers.OfType<DS>().ToList();
            Assert.That(dsRecords.Count, Is.GreaterThanOrEqualTo(1));

            foreach (var ds in dsRecords)
            {
                Assert.That(ds.Type,       Is.EqualTo(DNSResourceRecordTypes.DS));
                Assert.That(ds.Class,      Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(ds.KeyTag,     Is.GreaterThan((UInt16) 0));
                Assert.That(ds.Digest,     Is.Not.Null);
                Assert.That(ds.Digest.Length, Is.GreaterThan(0));

                // DigestType: 1 (SHA-1), 2 (SHA-256), 4 (SHA-384)
                Assert.That(new Byte[] { 1, 2, 4 }, Does.Contain(ds.DigestType));
            }

        }

        #endregion


        // ───────────────── TLSA (RFC 6698 / DANE) ─────────────────

        #region Test_dane_enabled_domain__TLSA()

        [Test]
        public async Task Test_dane_enabled_domain__TLSA()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            // _443._tcp.good.dane.verisignlabs.com is a well-known DANE test domain
            var response = await client.Query<TLSA>(DNSServiceName.Parse("_443._tcp.good.dane.verisignlabs.com"));

            // TLSA records are less common; just check the query round-trips without errors
            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

            var tlsaRecords = response.Answers.OfType<TLSA>().ToList();

            if (tlsaRecords.Count > 0)
            {
                foreach (var tlsa in tlsaRecords)
                {
                    Assert.That(tlsa.Type, Is.EqualTo(DNSResourceRecordTypes.TLSA));
                    Assert.That(tlsa.CertificateUsage, Is.LessThanOrEqualTo((Byte) 3));
                    Assert.That(tlsa.Selector,         Is.LessThanOrEqualTo((Byte) 1));
                    Assert.That(tlsa.MatchingType,     Is.LessThanOrEqualTo((Byte) 2));
                    Assert.That(tlsa.CertificateAssociationData, Is.Not.Null);
                    Assert.That(tlsa.CertificateAssociationData.Length, Is.GreaterThan(0));
                }
            }

        }

        #endregion


        // ───────────────── SSHFP (RFC 4255) ─────────────────

        #region Test_sshfp_domain__SSHFP()

        [Test]
        public async Task Test_sshfp_domain__SSHFP()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            // sshfp.dns-oarc.net has SSHFP records
            var response = await client.Query<SSHFP>(DomainName.Parse("sshfp.dns-oarc.net"));

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

            var sshfpRecords = response.Answers.OfType<SSHFP>().ToList();

            if (sshfpRecords.Count > 0)
            {
                foreach (var sshfp in sshfpRecords)
                {
                    Assert.That(sshfp.Type, Is.EqualTo(DNSResourceRecordTypes.SSHFP));
                    Assert.That(sshfp.FingerprintAlgorithm, Is.Not.EqualTo(default(SSHFP_Algorithm)));
                    Assert.That(sshfp.FingerprintType,      Is.Not.EqualTo(default(SSHFP_FingerprintType)));
                    Assert.That(sshfp.Fingerprint,          Is.Not.Null);
                    Assert.That(sshfp.Fingerprint.Length,    Is.GreaterThan(0));
                }
            }

        }

        #endregion


        // ───────────────── HINFO (RFC 1035) ─────────────────

        #region Test_hinfo_query_roundtrip()

        [Test]
        public async Task Test_hinfo_query_roundtrip()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            // HINFO is rare; just verify the query round-trips cleanly without error
            var response = await client.Query(
                               DomainName.Parse("cloudflare.com"),
                               [DNSResourceRecordTypes.HINFO]
                           );

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

            // We expect either an empty answer or valid HINFO records
            var hinfoRecords = response.Answers.OfType<HINFO>().ToList();
            foreach (var hinfo in hinfoRecords)
            {
                Assert.That(hinfo.Type, Is.EqualTo(DNSResourceRecordTypes.HINFO));
                Assert.That(hinfo.CPU,  Is.Not.Null);
                Assert.That(hinfo.OS,   Is.Not.Null);
            }

        }

        #endregion


        // ───────────────── LOC (RFC 1876) ─────────────────

        #region Test_loc_query_roundtrip()

        [Test]
        public async Task Test_loc_query_roundtrip()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            // LOC is also rare; verify the query mechanism works
            var response = await client.Query(
                               DomainName.Parse("cloudflare.com"),
                               [DNSResourceRecordTypes.LOC]
                           );

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

        }

        #endregion


        // ───────────────── NSEC (RFC 4034) ─────────────────

        #region Test_nsec_in_authority_section()

        [Test]
        public async Task Test_nsec_in_authority_section()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            // Query for a type we know doesn't exist, which should trigger an NSEC/NSEC3
            // response in the authority section (authenticated denial of existence)
            var response = await client.Query(
                               DomainName.Parse("cloudflare.com"),
                               [DNSResourceRecordTypes.LOC]
                           );

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

            // NSEC or NSEC3 records may appear in the authority section
            var nsecRecords  = response.Authorities.OfType<NSEC>().ToList();
            var nsec3Records = response.Authorities.OfType<NSEC3>().ToList();

            // At least one should be present for DNSSEC-signed zones
            if (nsecRecords.Count > 0)
            {
                foreach (var nsec in nsecRecords)
                {
                    Assert.That(nsec.Type, Is.EqualTo(DNSResourceRecordTypes.NSEC));
                    Assert.That(nsec.NextDomainName, Is.Not.Null);
                    Assert.That(nsec.TypeBitMaps,    Is.Not.Null);
                }
            }

            if (nsec3Records.Count > 0)
            {
                foreach (var nsec3 in nsec3Records)
                {
                    Assert.That(nsec3.Type, Is.EqualTo(DNSResourceRecordTypes.NSEC3));
                    Assert.That(nsec3.HashAlgorithm, Is.EqualTo((Byte) 1), "NSEC3 hash should be SHA-1 (1)");
                    Assert.That(nsec3.NextHashedOwnerName, Is.Not.Null);
                    Assert.That(nsec3.NextHashedOwnerName.Length, Is.GreaterThan(0));
                }
            }

        }

        #endregion


        // ───────────────── RRSIG (RFC 4034) ─────────────────

        #region Test_cloudflare_com__RRSIG_with_DNSKEY()

        [Test]
        public async Task Test_cloudflare_com__RRSIG_with_DNSKEY()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            // DNSSEC-signed zones return RRSIG alongside DNSKEY when queried with DO flag.
            // Even without DO, recursive resolvers often include RRSIG in their responses.
            var response = await client.Query(
                               DomainName.Parse("cloudflare.com"),
                               [DNSResourceRecordTypes.DNSKEY]
                           );

            Assert.That(response,            Is.Not.Null);
            Assert.That(response.IsValid,    Is.True);

            var rrsigRecords = response.Answers.OfType<RRSIG>().ToList();

            if (rrsigRecords.Count > 0)
            {
                foreach (var rrsig in rrsigRecords)
                {
                    Assert.That(rrsig.Type,               Is.EqualTo(DNSResourceRecordTypes.RRSIG));
                    Assert.That(rrsig.TypeCovered,         Is.EqualTo(DNSResourceRecordTypes.DNSKEY));
                    Assert.That(rrsig.Labels,              Is.GreaterThan((Byte) 0));
                    Assert.That(rrsig.OriginalTTL,         Is.GreaterThan((UInt32) 0));
                    Assert.That(rrsig.SignatureExpiration,  Is.GreaterThan(rrsig.SignatureInception));
                    Assert.That(rrsig.KeyTag,              Is.GreaterThan((UInt16) 0));
                    Assert.That(rrsig.SignerName,           Is.Not.Null);
                    Assert.That(rrsig.Signature,            Is.Not.Null);
                    Assert.That(rrsig.Signature.Length,     Is.GreaterThan(0));

                    // Algorithm should be a known DNSSEC algorithm
                    Assert.That(new Byte[] { 8, 10, 13, 14, 15, 16 }, Does.Contain(rrsig.Algorithm));
                }
            }

        }

        #endregion


        // ───────────────── DNSKEY Key Tag computation ─────────────────

        #region Test_DNSKEY_KeyTag_computation()

        [Test]
        public async Task Test_DNSKEY_KeyTag_computation()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<DNSKEY>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

            var dnskeys = response.Answers.OfType<DNSKEY>().ToList();
            var rrsigs  = response.Answers.OfType<RRSIG>()
                                           .Where(rr => rr.TypeCovered == DNSResourceRecordTypes.DNSKEY)
                                           .ToList();

            if (dnskeys.Count > 0 && rrsigs.Count > 0)
            {
                // The RRSIG's KeyTag should match the computed KeyTag of one of the DNSKEY records
                foreach (var rrsig in rrsigs)
                {
                    var matchingKey = dnskeys.FirstOrDefault(
                                         k => DNSSECValidator.ComputeKeyTag(k) == rrsig.KeyTag
                                     );

                    // This verifies our KeyTag computation is correct
                    Assert.That(matchingKey, Is.Not.Null,
                                $"No DNSKEY found with computed KeyTag {rrsig.KeyTag}");
                }
            }

        }

        #endregion


        // ───────────────── DS ↔ DNSKEY verification ─────────────────

        #region Test_DS_DNSKEY_verification()

        [Test]
        public async Task Test_DS_DNSKEY_verification()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            // Fetch the DS for cloudflare.com (from .com zone)
            var dsResponse = await client.Query<DS>(DomainName.Parse("cloudflare.com"));

            // Fetch the DNSKEY for cloudflare.com
            var dnskeyResponse = await client.Query<DNSKEY>(DomainName.Parse("cloudflare.com"));

            if (!dsResponse.IsValid || !dnskeyResponse.IsValid)
            {
                Assert.Inconclusive("Could not fetch DS or DNSKEY records");
                return;
            }

            var dsRecords     = dsResponse.Answers.OfType<DS>().ToList();
            var dnskeyRecords = dnskeyResponse.Answers.OfType<DNSKEY>().ToList();

            if (dsRecords.Count == 0 || dnskeyRecords.Count == 0)
            {
                Assert.Inconclusive("No DS or DNSKEY records returned");
                return;
            }

            // Find the KSK (Flags 257) whose KeyTag matches a DS record
            var ksks = dnskeyRecords.Where(k => k.Flags == 257).ToList();

            var verified = false;

            foreach (var ksk in ksks)
            {
                var keyTag = DNSSECValidator.ComputeKeyTag(ksk);

                var matchingDS = dsRecords.FirstOrDefault(
                                     ds => ds.KeyTag    == keyTag &&
                                           ds.Algorithm == ksk.Algorithm
                                 );

                if (matchingDS is not null)
                {
                    var result = DNSSECValidator.VerifyDS(ksk, matchingDS);

                    if (result)
                    {
                        verified = true;
                        break;
                    }
                }
            }

            Assert.That(verified, Is.True,
                        "Expected at least one KSK to match a DS record for cloudflare.com");

        }

        #endregion


        // ───────────────── ToZoneFileString() ─────────────────

        #region Test_ToZoneFileString__A()

        [Test]
        public async Task Test_ToZoneFileString__A()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<A>(DomainName.Parse("charging.cloud"));

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);
            Assert.That(response.Answers.Count, Is.GreaterThanOrEqualTo(1));

            var a = response.Answers.OfType<A>().First();
            var zoneStr = a.ToZoneFileString();

            Assert.That(zoneStr, Is.Not.Null.And.Not.Empty);
            Assert.That(zoneStr, Does.Contain("charging.cloud."));
            Assert.That(zoneStr, Does.Contain("IN"));
            Assert.That(zoneStr, Does.Contain("A"));
            Assert.That(zoneStr, Does.Contain("23.88.66.160"));

        }

        #endregion

        #region Test_ToZoneFileString__CAA()

        [Test]
        public async Task Test_ToZoneFileString__CAA()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<CAA>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

            var caaRecords = response.Answers.OfType<CAA>().ToList();
            if (caaRecords.Count == 0) { Assert.Inconclusive("No CAA records returned"); return; }

            var caa = caaRecords.First();
            var zoneStr = caa.ToZoneFileString();

            Assert.That(zoneStr, Is.Not.Null.And.Not.Empty);
            Assert.That(zoneStr, Does.Contain("cloudflare.com."));
            Assert.That(zoneStr, Does.Contain("IN"));
            Assert.That(zoneStr, Does.Contain("CAA"));
            // Should contain either "issue" or "issuewild"
            Assert.That(zoneStr, Does.Contain("issue").IgnoreCase);

        }

        #endregion

        #region Test_ToZoneFileString__DNSKEY()

        [Test]
        public async Task Test_ToZoneFileString__DNSKEY()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<DNSKEY>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

            var keys = response.Answers.OfType<DNSKEY>().ToList();
            if (keys.Count == 0) { Assert.Inconclusive("No DNSKEY records returned"); return; }

            var key = keys.First();
            var zoneStr = key.ToZoneFileString();

            Assert.That(zoneStr, Is.Not.Null.And.Not.Empty);
            Assert.That(zoneStr, Does.Contain("cloudflare.com."));
            Assert.That(zoneStr, Does.Contain("IN"));
            Assert.That(zoneStr, Does.Contain("DNSKEY"));
            // Should contain the protocol "3"
            Assert.That(zoneStr, Does.Contain(" 3 "));

        }

        #endregion

        #region Test_ToZoneFileString__DS()

        [Test]
        public async Task Test_ToZoneFileString__DS()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<DS>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

            var dsRecords = response.Answers.OfType<DS>().ToList();
            if (dsRecords.Count == 0) { Assert.Inconclusive("No DS records returned"); return; }

            var ds = dsRecords.First();
            var zoneStr = ds.ToZoneFileString();

            Assert.That(zoneStr, Is.Not.Null.And.Not.Empty);
            Assert.That(zoneStr, Does.Contain("cloudflare.com."));
            Assert.That(zoneStr, Does.Contain("IN"));
            Assert.That(zoneStr, Does.Contain("DS"));
            // Should contain the hex digest
            Assert.That(zoneStr.Length, Is.GreaterThan(60));

        }

        #endregion

    }


    /// <summary>
    /// The same tests but via Google DNS-over-HTTPS (JSON) to test JSON parsing of new types.
    /// </summary>
    [TestFixture]
    public class NewRecordType_GoogleJSON_Tests
    {

        private IDNSClientWithTransport? client;

        [OneTimeSetUp]
        public void InitTests()
        {
            client = DNSHTTPSClient.Google(
                         Mode:                         DNSHTTPSMode.JSON,
                         RemoteCertificateValidator:   TLSValidationExtensions.AskTheOS,
                         DNSClient:                    new DNSClient(
                                                           SearchForIPv4DNSServers: true,
                                                           SearchForIPv6DNSServers: false
                                                       )
                     );
        }

        [OneTimeTearDown]
        public void ShutdownTests()
        {
            client?.Dispose();
        }


        // ───────────────── CAA via JSON ─────────────────

        #region Test_cloudflare_com__CAA__JSON()

        [Test]
        public async Task Test_cloudflare_com__CAA__JSON()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<CAA>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,            Is.Not.Null);
            Assert.That(response.IsValid,    Is.True);
            Assert.That(response.Answers.Count, Is.GreaterThanOrEqualTo(1));

            var caaRecords = response.Answers.OfType<CAA>().ToList();
            Assert.That(caaRecords.Count, Is.GreaterThanOrEqualTo(1));

            var tags = caaRecords.Select(c => c.Tag.ToLower()).ToHashSet();
            Assert.That(tags.Contains("issue") || tags.Contains("issuewild"), Is.True);

        }

        #endregion


        // ───────────────── DNSKEY via JSON ─────────────────

        #region Test_cloudflare_com__DNSKEY__JSON()

        [Test]
        public async Task Test_cloudflare_com__DNSKEY__JSON()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<DNSKEY>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,            Is.Not.Null);
            Assert.That(response.IsValid,    Is.True);
            Assert.That(response.Answers.Count, Is.GreaterThanOrEqualTo(1));

            var keys = response.Answers.OfType<DNSKEY>().ToList();
            Assert.That(keys.Count, Is.GreaterThanOrEqualTo(1));

            foreach (var key in keys)
            {
                Assert.That(key.Protocol,  Is.EqualTo((Byte) 3));
                Assert.That(key.PublicKey.Length, Is.GreaterThan(0));
            }

        }

        #endregion


        // ───────────────── DS via JSON ─────────────────

        #region Test_cloudflare_com__DS__JSON()

        [Test]
        public async Task Test_cloudflare_com__DS__JSON()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query<DS>(DomainName.Parse("cloudflare.com"));

            Assert.That(response,            Is.Not.Null);
            Assert.That(response.IsValid,    Is.True);
            Assert.That(response.Answers.Count, Is.GreaterThanOrEqualTo(1));

            var dsRecords = response.Answers.OfType<DS>().ToList();
            Assert.That(dsRecords.Count, Is.GreaterThanOrEqualTo(1));

            foreach (var ds in dsRecords)
            {
                Assert.That(ds.KeyTag, Is.GreaterThan((UInt16) 0));
                Assert.That(ds.Digest.Length, Is.GreaterThan(0));
            }

        }

        #endregion


        // ───────────────── HINFO via JSON ─────────────────

        #region Test_hinfo_query_roundtrip__JSON()

        [Test]
        public async Task Test_hinfo_query_roundtrip__JSON()
        {

            if (client is null) { Assert.Fail("DNS client is null!"); return; }

            var response = await client.Query(
                               DomainName.Parse("cloudflare.com"),
                               [DNSResourceRecordTypes.HINFO]
                           );

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.IsValid, Is.True);

        }

        #endregion

    }

}
