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
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// When a DMARC policy is applied at all (RFC 7489 §6.6.2, §6.6.3): after a temporary error
    /// of SPF, with a DNS error at the From domain's record, with two records, with a version
    /// that is not precisely "DMARC1", and with a record without a valid p= or sp=. The
    /// findings V-18 to V-22 of SMTPConformanceTests. The messages come from news@author.test
    /// (or a subdomain), unsigned, through a client no SPF record names: DMARC fails on them
    /// wherever it is applied.
    /// </summary>
    [TestFixture]
    public class DmarcPolicyDiscoveryTests
    {

        #region Setup

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        /// <summary>
        /// TXT and A records by owner name, answers with an error code, and lookups that throw.
        /// </summary>
        private sealed class ZoneDNS : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private readonly Dictionary<(String, DNSResourceRecordTypes), List<IDNSResourceRecord>>  records  = [];
            private readonly Dictionary<(String, DNSResourceRecordTypes), DNSResponseCodes>          errors   = [];
            private readonly HashSet<(String, DNSResourceRecordTypes)>                                timeouts = [];

            public ZoneDNS Txt(String Name, String Text)
            {
                var key = (Name, DNSResourceRecordTypes.TXT);
                if (!records.TryGetValue(key, out var list))
                    records[key] = list = [];
                list.Add(new TXT(DNSServiceName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), Text));
                return this;
            }

            public ZoneDNS Error(String Name, DNSResourceRecordTypes Type, DNSResponseCodes Code = DNSResponseCodes.ServerFailure)
            {
                errors[(Name, Type)] = Code;
                return this;
            }

            public ZoneDNS Timeout(String Name, DNSResourceRecordTypes Type)
            {
                timeouts.Add((Name, Type));
                return this;
            }

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.').ToLowerInvariant();
                var answers = new List<IDNSResourceRecord>();
                var code    = DNSResponseCodes.NoError;

                foreach (var type in Types)
                {

                    if (timeouts.Contains((name, type)))
                        throw new TimeoutException($"{name} {type}");

                    if (errors.TryGetValue((name, type), out var error))
                        code = error;

                    else if (records.TryGetValue((name, type), out var list))
                        answers.AddRange(list);

                }

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, code,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private const String Author = "author.test";

        private static String Message(String FromDomain = Author)

            => $"From: <news@{FromDomain}>\r\n" +
               "To: <alice@hermod.test>\r\n" +
               "Subject: DMARC policy\r\n" +
               $"Message-ID: <{Guid.NewGuid():N}@{FromDomain}>\r\n" +
               "Date: Sat, 21 Nov 2026 10:00:00 +0000\r\n" +
               "\r\n" +
               "Hello.\r\n";

        /// <summary>
        /// The message from the given MAIL FROM, through 192.0.2.25.
        /// </summary>
        private static Task<DnsVerificationResult> Verify(ZoneDNS DNS, String Message, String MailFrom = "bounce@bounces.unrelated.test")

            => new DNSVerifier(DNS, new QuietLogger()).
                   VerifyAsync(MailFrom[(MailFrom.IndexOf('@') + 1)..], System.Net.IPAddress.Parse("192.0.2.25"), MailFrom, "mx.unrelated.test", EMailMessage.Parse(Message));

        #endregion


        #region A temporary error of SPF (V-18)

        /// <summary>
        /// SPF of author.test names mail.author.test, whose address lookup fails: "temperror".
        /// </summary>
        private static ZoneDNS SpfTemporaryError(String Dmarc)

            => new ZoneDNS().
                   Txt  ("_dmarc." + Author, Dmarc).
                   Txt  (Author, "v=spf1 a:mail.author.test -all").
                   Error("mail.author.test", DNSResourceRecordTypes.A);


        [Test(Description = "RFC 7489 §6.6.2: \"If neither passes and one or both of them fail due to a temporary error, the Receiver evaluating the message is unable to conclude that the DMARC mechanism had a permanent failure; they therefore cannot apply the advertised DMARC policy\"")]
        public async Task A_temporary_error_of_aligned_SPF_does_not_apply_the_policy()
        {

            var result = await Verify(SpfTemporaryError("v=DMARC1; p=reject"), Message(), MailFrom: $"news@{Author}");

            Assert.Multiple(() => {
                Assert.That(result.Spf,                       Is.EqualTo(SPFResult.TempError));
                Assert.That(result.Dmarc,                     Is.EqualTo(DmarcResult.TempError));
                Assert.That(result.DmarcDetail?.Disposition,  Is.EqualTo(DmarcDisposition.None));
            });

        }


        [Test(Description = "RFC 7489 §6.6.2: a temporary error of SPF for a domain not aligned with the From domain could not have passed DMARC - it fails, and the policy applies")]
        public async Task A_temporary_error_of_unaligned_SPF_still_fails()
        {

            var dns = new ZoneDNS().
                          Txt  ("_dmarc." + Author, "v=DMARC1; p=reject").
                          Txt  ("bounces.unrelated.test", "v=spf1 a:mail.unrelated.test -all").
                          Error("mail.unrelated.test", DNSResourceRecordTypes.A);

            var result = await Verify(dns, Message(), MailFrom: "bounce@bounces.unrelated.test");

            Assert.Multiple(() => {
                Assert.That(result.Spf,                       Is.EqualTo(SPFResult.TempError));
                Assert.That(result.Dmarc,                     Is.EqualTo(DmarcResult.Fail));
                Assert.That(result.DmarcDetail?.Disposition,  Is.EqualTo(DmarcDisposition.Reject));
            });

        }

        #endregion

        #region A DNS error at the From domain's record (V-19)

        [Test(Description = "RFC 7489 §6.6.3: the Organizational Domain is asked \"If the set is now empty\" - a lookup that failed with SERVFAIL answered nothing, and the Organizational Domain's record is not applied in its place")]
        public async Task A_DNS_error_at_the_From_domain_does_not_fall_back()
        {

            var dns = new ZoneDNS().
                          Txt  ("_dmarc." + Author, "v=DMARC1; p=none; sp=none").
                          Error("_dmarc.news." + Author, DNSResourceRecordTypes.TXT);

            var result = await Verify(dns, Message(FromDomain: "news." + Author));

            Assert.Multiple(() => {
                Assert.That(result.Dmarc,        Is.EqualTo(DmarcResult.TempError));
                Assert.That(result.DmarcDetail,  Is.Null);
            });

        }


        [Test(Description = "RFC 7489 §6.6.3: \"a transient DNS error\" - a lookup that timed out does not fall back either")]
        public async Task A_timeout_at_the_From_domain_does_not_fall_back()
        {

            var dns = new ZoneDNS().
                          Txt    ("_dmarc." + Author, "v=DMARC1; p=none; sp=reject").
                          Timeout("_dmarc.news." + Author, DNSResourceRecordTypes.TXT);

            var result = await Verify(dns, Message(FromDomain: "news." + Author));

            Assert.That(result.Dmarc, Is.EqualTo(DmarcResult.TempError));

        }


        [TestCase(DNSResponseCodes.NoError,   Description = "no TXT record")]
        [TestCase(DNSResponseCodes.NameError, Description = "NXDOMAIN")]
        [Test(Description = "RFC 7489 §6.6.3: a From domain that has no record of its own - no records, or no such name - falls back to the Organizational Domain's, whose sp= applies")]
        public async Task A_From_domain_without_a_record_falls_back(DNSResponseCodes Code)
        {

            var dns = new ZoneDNS().Txt("_dmarc." + Author, "v=DMARC1; p=none; sp=reject");

            if (Code != DNSResponseCodes.NoError)
                dns.Error("_dmarc.news." + Author, DNSResourceRecordTypes.TXT, Code);

            var result = await Verify(dns, Message(FromDomain: "news." + Author));

            Assert.Multiple(() => {
                Assert.That(result.DmarcDetail?.PolicyDomain,  Is.EqualTo(Author));
                Assert.That(result.DmarcDetail?.Disposition,   Is.EqualTo(DmarcDisposition.Reject));
            });

        }


        [Test(Description = "RFC 7489 §6.6.3: a DNS error at the Organizational Domain's record - no policy is applied")]
        public async Task A_DNS_error_at_the_organizational_domain_applies_no_policy()
        {

            var dns = new ZoneDNS().Error("_dmarc." + Author, DNSResourceRecordTypes.TXT);

            var result = await Verify(dns, Message(FromDomain: "news." + Author));

            Assert.Multiple(() => {
                Assert.That(result.Dmarc,        Is.EqualTo(DmarcResult.TempError));
                Assert.That(result.DmarcDetail,  Is.Null);
            });

        }

        #endregion

        #region Which record (V-20, V-21)

        [TestCase(false, new[] { "v=DMARC1; p=reject", "v=DMARC1; p=none" },        Description = "two records")]
        [TestCase(false, new[] { "v=dmarc1; p=reject" },                            Description = "v=dmarc1")]
        [TestCase(false, new[] { "v=DMARC1x; p=reject" },                           Description = "v=DMARC1x")]
        [TestCase(false, new[] { "v=DMARC10; p=reject" },                           Description = "v=DMARC10")]
        [TestCase(false, new[] { "p=reject; v=DMARC1" },                            Description = "v= not first")]
        [TestCase(true,  new[] { "v = DMARC1; p=reject" },                          Description = "white space around the =")]
        [TestCase(true,  new[] { "v=DMARC1 ; p=reject" },                           Description = "white space before the separator")]
        [TestCase(true,  new[] { "v=DMARC1;p=reject" },                             Description = "no white space")]
        [TestCase(true,  new[] { "site-verification=4711", "v=DMARC1; p=reject" },  Description = "one record among other TXT records")]
        [TestCase(true,  new[] { "v=dmarc1; p=none", "v=DMARC1; p=reject" },        Description = "one record beside one of another version")]
        [Test(Description = "RFC 7489 §6.6.3: \"Records that do not start with a \"v=\" tag that identifies the current version of DMARC are discarded\"; \"If the remaining set contains multiple records or no records, policy discovery terminates and DMARC processing is not applied to this message\"; §6.3: the value of v= \"MUST match precisely\"; §6.4: dmarc-version = \"v\" *WSP \"=\" *WSP %x44 %x4d %x41 %x52 %x43 %x31")]
        public async Task Only_one_record_of_version_DMARC1_is_applied(Boolean Applied, String[] Records)
        {

            var dns = new ZoneDNS();
            foreach (var record in Records)
                dns.Txt("_dmarc." + Author, record);

            var result = await Verify(dns, Message());

            if (Applied)
                Assert.That(result.DmarcDetail?.Disposition, Is.EqualTo(DmarcDisposition.Reject));
            else
                Assert.Multiple(() => {
                    Assert.That(result.Dmarc,        Is.EqualTo(DmarcResult.None));
                    Assert.That(result.DmarcDetail,  Is.Null);
                });

        }


        [Test(Description = "RFC 7489 §6.6.3: two records at the From domain end policy discovery - the Organizational Domain is not asked, its set was not empty")]
        public async Task Two_records_at_the_From_domain_do_not_fall_back()
        {

            var dns = new ZoneDNS().
                          Txt("_dmarc.news." + Author, "v=DMARC1; p=none").
                          Txt("_dmarc.news." + Author, "v=DMARC1; p=quarantine").
                          Txt("_dmarc."      + Author, "v=DMARC1; p=reject; sp=reject");

            var result = await Verify(dns, Message(FromDomain: "news." + Author));

            Assert.That(result.Dmarc, Is.EqualTo(DmarcResult.None));

        }

        #endregion

        #region Records without a valid p= or sp= (V-22)

        [TestCase("v=DMARC1; sp=reject",           Description = "no p=")]
        [TestCase("v=DMARC1; p=bogus",             Description = "p= of no policy")]
        [TestCase("v=DMARC1; p=",                  Description = "an empty p=")]
        [TestCase("v=DMARC1; p=reject; sp=bogus",  Description = "sp= of no policy")]
        [TestCase("v=DMARC1; p=bogus; rua=bogus",  Description = "a rua that is no URI")]
        [Test(Description = "RFC 7489 §6.6.3: \"If a retrieved policy record does not contain a valid \"p\" tag, or contains an \"sp\" tag that is not valid, then: ... 2. otherwise, the Mail Receiver applies no DMARC processing to this message\"")]
        public async Task A_record_without_a_valid_policy_and_without_rua_is_not_applied(String Record)
        {

            var result = await Verify(new ZoneDNS().Txt("_dmarc." + Author, Record), Message());

            Assert.That(result.Dmarc, Is.EqualTo(DmarcResult.None));

        }


        [TestCase("v=DMARC1; p=bogus; rua=mailto:dmarc@author.test",                 Description = "p= of no policy")]
        [TestCase("v=DMARC1; rua=mailto:dmarc@author.test!10m",                      Description = "no p=, a rua with a size limit")]
        [TestCase("v=DMARC1; p=reject; sp=bogus; rua=bogus,mailto:dmarc@author.test", Description = "sp= of no policy, one valid rua of two")]
        [Test(Description = "RFC 7489 §6.6.3: \"1. if a \"rua\" tag is present and contains at least one syntactically valid reporting URI, the Mail Receiver SHOULD act as if a record containing a valid \"v\" tag and \"p=none\" was retrieved, and continue processing\"")]
        public async Task A_record_without_a_valid_policy_but_with_rua_is_p_none(String Record)
        {

            var result = await Verify(new ZoneDNS().Txt("_dmarc." + Author, Record), Message());

            Assert.Multiple(() => {
                Assert.That(result.Dmarc,                         Is.EqualTo(DmarcResult.Fail));
                Assert.That(result.DmarcDetail?.Disposition,      Is.EqualTo(DmarcDisposition.None));
                Assert.That(result.DmarcDetail?.RequestedPolicy,  Is.EqualTo("none"));
                Assert.That(result.DmarcDetail?.Rua,              Is.Not.Null);
            });

        }


        [TestCase("v=DMARC1; p=REJECT",                Description = "upper case")]
        [TestCase("v=DMARC1; p=quarantine; sp=reject", Description = "with sp=")]
        [Test(Description = "RFC 7489 §6.4: dmarc-request = \"p\" *WSP \"=\" *WSP ( \"none\" / \"quarantine\" / \"reject\" ) - quoted strings of ABNF, which do not care about case")]
        public async Task A_valid_policy_is_applied(String Record)
        {

            var result = await Verify(new ZoneDNS().Txt("_dmarc." + Author, Record), Message());

            Assert.Multiple(() => {
                Assert.That(result.Dmarc,                     Is.EqualTo(DmarcResult.Fail));
                Assert.That(result.DmarcDetail?.Disposition,  Is.Not.EqualTo(DmarcDisposition.None));
            });

        }

        #endregion

        #region IsDmarcRecord

        [TestCase("v=DMARC1",              true)]
        [TestCase("v=DMARC1;",             true)]
        [TestCase("v=DMARC1; p=none",      true)]
        [TestCase("V=DMARC1; p=none",      true)]
        [TestCase("v\t=\tDMARC1\t;p=none", true)]
        [TestCase("v=dmarc1; p=none",      false)]
        [TestCase("v=DMARC1x; p=none",     false)]
        [TestCase("v=DMARC1 p=none",       false)]
        [TestCase(" v=DMARC1; p=none",     false)]
        [TestCase("p=none; v=DMARC1",      false)]
        [TestCase("v=spf1 -all",           false)]
        [Test(Description = "RFC 7489 §6.4: dmarc-record = dmarc-version dmarc-sep ..., dmarc-version = \"v\" *WSP \"=\" *WSP %x44 %x4d %x41 %x52 %x43 %x31, dmarc-sep = *WSP %x3b *WSP")]
        public void IsDmarcRecord(String Record, Boolean IsDmarc)
        {
            Assert.That(DNSVerifier.IsDmarcRecord(Record), Is.EqualTo(IsDmarc));
        }

        #endregion

    }

}
