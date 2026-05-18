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
                    Assert.That(sshfp.FingerprintAlgorithm,   Is.Not.EqualTo(default(SSHFP_Algorithm)));
                    Assert.That(sshfp.FingerprintType,        Is.Not.EqualTo(default(SSHFP_FingerprintType)));
                    Assert.That(sshfp.Fingerprint,            Is.Not.Null);
                    Assert.That(sshfp.Fingerprint.Length,     Is.GreaterThan(0));
                }
            }

        }

        #endregion

        #region Test_janus1_graphdefined_com__SSHFP()

        [Test]
        public void Test_janus1_graphdefined_com__SSHFP()
        {

            var sshfpRecords  = new List<String> {

                                    "janus1.graphdefined.com IN SSHFP 1 1 bd35fbb31c3aaadeb9a5dc887f4e8f4510332a5f",
                                    "janus1.graphdefined.com IN SSHFP 1 2 2a39072bb1af2a255e9e8e2456f2a31c097a8b6edaa709a610c2993a7f463c07",
                                    "janus1.graphdefined.com IN SSHFP 3 1 f7e3d0ddeb5b765df327d7589d04f6cfc54d093a",
                                    "janus1.graphdefined.com IN SSHFP 3 2 38c80a5b37d2c5db664dfabbb3e14d60d2ed9ce854d334c4ce349c14c89f0544",
                                    "janus1.graphdefined.com IN SSHFP 4 1 1213a08d727469ac8e8d1ce3e3708831d7f4c4a7",
                                    "janus1.graphdefined.com IN SSHFP 4 2 ef330ab694d823bbda98fdb738919d85b1f8321f6abe1e84dc3cb7800ceb92ca",

                                    "janus2.graphdefined.com IN SSHFP 1 1 3dc114f332324dea702c03b4480b0767ad4d8f8c",
                                    "janus2.graphdefined.com IN SSHFP 1 2 99fbf0f159a0e808f78a882c5fe16aae54b1a694798f756a4693d75129ac3790",
                                    "janus2.graphdefined.com IN SSHFP 3 1 0e3d59626f5276b6524f1ea81adbe83fdd783e6c",
                                    "janus2.graphdefined.com IN SSHFP 3 2 a07f5c117167eff1def2106115c621e870705489a746c8749057e48a81f4af02",
                                    "janus2.graphdefined.com IN SSHFP 4 1 2afc5ba8a8a73b734607c77ee106445b6cfadc4f",
                                    "janus2.graphdefined.com IN SSHFP 4 2 6b267fd4bdb10fe3c976ce5cbda424d8467f419dc3dac1c0e32f0179a2a5f543",

                                    "janus3.graphdefined.com IN SSHFP 1 1 7747434d4324ddfdb0e5a91a137d1b5454ecd52d",
                                    "janus3.graphdefined.com IN SSHFP 1 2 b487f1b7d8c98e6a548b2e75d1a228bf6d7fa9c4e6765198ac2d3b9ce2a18568",
                                    "janus3.graphdefined.com IN SSHFP 3 1 bdfc5143dd6d9ed2f3cda5961eec4946830d1c52",
                                    "janus3.graphdefined.com IN SSHFP 3 2 f7b0a7686e77e5f0d15c89ce0bb985fb1acb7d577eb94b23e6afa9693be34907",
                                    "janus3.graphdefined.com IN SSHFP 4 1 2304031462f93552d3e54616082457751196d7e8",
                                    "janus3.graphdefined.com IN SSHFP 4 2 c70035df927fc3c76a0da74cb994b5a18f77d268c02f91b6058321e3f23a7551"

                                };

            foreach (var sshfpRecord in sshfpRecords)
            {

                Assert.That(
                    ADNSResourceRecord.TryParseZoneFileString(
                        sshfpRecord,
                        out var resourceRecord,
                        out var errorResponse
                    ),
                    Is.True,
                    errorResponse
                );

                if (resourceRecord is null)
                {
                    Assert.Fail($"Failed to parse the SSHFP record: {sshfpRecord}!");
                    continue;
                }

                Assert.That(resourceRecord,                         Is.TypeOf<SSHFP>());
                //Assert.That(resourceRecord.DomainName.ToString(),   Is.EqualTo("janus1.graphdefined.com."));
                Assert.That(resourceRecord.Class,                   Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(resourceRecord.TimeToLive,              Is.EqualTo(TimeSpan.Zero));

                var sshfp = (SSHFP) resourceRecord;
                Assert.That(sshfp.FingerprintAlgorithm,             Is.Not.EqualTo(default(SSHFP_Algorithm)));
                Assert.That(sshfp.FingerprintType,                  Is.Not.EqualTo(default(SSHFP_FingerprintType)));
                Assert.That(sshfp.Fingerprint,                      Is.Not.Null.And.Not.Empty);

            }

        }

        #endregion

        #region Test_TryParseZoneFileString__representative_records()

        [Test]
        public void Test_TryParseZoneFileString__representative_records()
        {

            var zoneFileRecords = new (String Text, DNSResourceRecordTypes Type)[] {
                                      ("example.com. 3600 IN A 192.0.2.1",                                                            DNSResourceRecordTypes.A),
                                      ("example.com. 3600 IN AAAA 2001:db8::1",                                                       DNSResourceRecordTypes.AAAA),
                                      ("example.com. IN NS ns1.example.com.",                                                         DNSResourceRecordTypes.NS),
                                      ("www.example.com. IN CNAME example.com.",                                                      DNSResourceRecordTypes.CNAME),
                                      ("example.com. IN SOA ns.example.com. hostmaster.example.com. 2026051801 3600 600 86400 3600",  DNSResourceRecordTypes.SOA),
                                      ("1.2.0.192.in-addr.arpa. IN PTR example.com.",                                                 DNSResourceRecordTypes.PTR),
                                      ("example.com. IN HINFO \"INTEL\" \"Linux\"",                                                   DNSResourceRecordTypes.HINFO),
                                      ("example.com. IN MX 10 mail.example.com.",                                                     DNSResourceRecordTypes.MX),
                                      ("example.com. IN TXT \"hello world\"",                                                         DNSResourceRecordTypes.TXT),
                                      ("example.com. IN RP admin.example.com. txt.example.com.",                                      DNSResourceRecordTypes.RP),
                                      ("example.com. IN AFSDB 1 afsdb.example.com.",                                                  DNSResourceRecordTypes.AFSDB),
                                      ("example.com. IN LOC 52 22 23.000 N 4 53 32.000 E -2.00m 0.00m 10000.00m 10.00m",              DNSResourceRecordTypes.LOC),
                                      ("example.com. IN NAPTR 100 10 \"u\" \"E2U+sip\" \"!^.*$!sip:info@example.com!\" .",            DNSResourceRecordTypes.NAPTR),
                                      ("example.com. IN CERT 1 0 5 AQID",                                                             DNSResourceRecordTypes.CERT),
                                      ("alias.example.com. IN DNAME example.com.",                                                    DNSResourceRecordTypes.DNAME),
                                      ("example.com. IN DS 12345 13 2 00112233445566778899aabbccddeeff",                              DNSResourceRecordTypes.DS),
                                      ("example.com. IN SSHFP 1 1 1469679466a193364f3928b7f3b6a15180244ec1",                          DNSResourceRecordTypes.SSHFP),
                                      ("example.com. IN RRSIG A 13 2 3600 20260518000000 20260418000000 12345 example.com. AQID",     DNSResourceRecordTypes.RRSIG),
                                      ("example.com. IN NSEC next.example.com. A NS SOA MX TXT AAAA RRSIG NSEC DNSKEY",               DNSResourceRecordTypes.NSEC),
                                      ("example.com. IN DNSKEY 257 3 13 AQID",                                                        DNSResourceRecordTypes.DNSKEY),
                                      ("example.com. IN NSEC3 1 0 12 aabb ccdd A NS SOA MX TXT AAAA RRSIG DNSKEY NSEC3PARAM",         DNSResourceRecordTypes.NSEC3),
                                      ("example.com. IN NSEC3PARAM 1 0 12 aabb",                                                      DNSResourceRecordTypes.NSEC3PARAM),
                                      ("example.com. IN TLSA 3 1 1 00112233445566778899aabbccddeeff",                                 DNSResourceRecordTypes.TLSA),
                                      ("example.com. IN SMIMEA 3 1 1 00112233445566778899aabbccddeeff",                               DNSResourceRecordTypes.SMIMEA),
                                      ("example.com. IN CDS 12345 13 2 00112233445566778899aabbccddeeff",                             DNSResourceRecordTypes.CDS),
                                      ("example.com. IN CDNSKEY 257 3 13 AQID",                                                       DNSResourceRecordTypes.CDNSKEY),
                                      ("example.com. IN OPENPGPKEY AQID",                                                             DNSResourceRecordTypes.OPENPGPKEY),
                                      ("example.com. IN CSYNC 2026051801 3 A NS SOA MX",                                              DNSResourceRecordTypes.CSYNC),
                                      ("example.com. IN ZONEMD 2026051801 1 1 00112233445566778899aabbccddeeff",                      DNSResourceRecordTypes.ZONEMD),
                                      ("example.com. IN SVCB 1 svc.example.com. alpn=\"h2,h3\" port=\"443\"",                         DNSResourceRecordTypes.SVCB),
                                      ("example.com. IN HTTPS 1 . alpn=\"h2,h3\"",                                                    DNSResourceRecordTypes.HTTPS),
                                      ("example.com. IN SPF \"v=spf1 -all\"",                                                         DNSResourceRecordTypes.SPF),
                                      ("example.com. IN EUI48 00-11-22-33-44-55",                                                     DNSResourceRecordTypes.EUI48),
                                      ("example.com. IN EUI64 00-11-22-33-44-55-66-77",                                               DNSResourceRecordTypes.EUI64),
                                      ("example.com. IN CAA 0 issue \"letsencrypt.org\"",                                             DNSResourceRecordTypes.CAA),
                                      ("_sip._tcp.example.com. IN SRV 10 5 5060 sip.example.com.",                                    DNSResourceRecordTypes.SRV),
                                      ("_service._tcp.example.com. IN URI 10 1 \"https://example.com/\"",                             DNSResourceRecordTypes.URI),
                                      ("key.example.com. IN TKEY hmac-sha256.example.com. 20260518120000 20260518130000 3 0 AQID",    DNSResourceRecordTypes.TKEY),
                                      ("key.example.com. 0 ANY TSIG hmac-sha256.example.com. 20260518120000 300 AQID 12345 0",        DNSResourceRecordTypes.TSIG)
                                  };

            foreach (var zoneFileRecord in zoneFileRecords)
            {

                Assert.That(
                    ADNSResourceRecord.TryParseZoneFileString(
                        zoneFileRecord.Text,
                        out var resourceRecord,
                        out var errorResponse
                    ),
                    Is.True,
                    $"{zoneFileRecord.Text}: {errorResponse}"
                );

                Assert.That(resourceRecord,         Is.Not.Null);
                Assert.That(resourceRecord!.Type,   Is.EqualTo(zoneFileRecord.Type));

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

            // DNS record order is not guaranteed, so check all CAA records
            foreach (var caa in caaRecords)
            {
                var zoneStr = caa.ToZoneFileString();

                Assert.That(zoneStr, Is.Not.Null.And.Not.Empty);
                Assert.That(zoneStr, Does.Contain("cloudflare.com."));
                Assert.That(zoneStr, Does.Contain("IN"));
                Assert.That(zoneStr, Does.Contain("CAA"));
            }

            // At least one CAA record should contain "issue" or "issuewild"
            Assert.That(caaRecords.Any(c => c.ToZoneFileString().Contains("issue", StringComparison.OrdinalIgnoreCase)), Is.True);

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
