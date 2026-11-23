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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// SPF, DKIM and DMARC of internationalized domain names (RFC 8616): a domain in U-labels is
    /// looked up by its A-labels - "bücher.test" is "xn--bcher-kva.test" - in the record of
    /// SPF and its macros (§4), the key of a signature whose d= is in U-labels (§5, §3), and the
    /// DMARC record and alignment of an author domain in U-labels (§6). The findings V-23 to
    /// V-25 of SMTPConformanceTests. Every query is noted: none of them is in U-labels.
    /// </summary>
    [TestFixture]
    public class InternationalizedAuthenticationTests
    {

        #region Setup

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        /// <summary>
        /// TXT and A records by owner name; every query noted.
        /// </summary>
        private sealed class ZoneDNS : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private readonly Dictionary<(String, DNSResourceRecordTypes), List<IDNSResourceRecord>> records = [];

            public ConcurrentQueue<String> Queries { get; } = new();

            private ZoneDNS Add(String Name, DNSResourceRecordTypes Type, IDNSResourceRecord Record)
            {
                var key = (Name, Type);
                if (!records.TryGetValue(key, out var list))
                    records[key] = list = [];
                list.Add(Record);
                return this;
            }

            public ZoneDNS Txt(String Name, String Text)
                => Add(Name, DNSResourceRecordTypes.TXT, new TXT(DNSServiceName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), Text));

            public ZoneDNS A(String Name, String Address)
                => Add(Name, DNSResourceRecordTypes.A, new A(DNSServiceName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), IPv4Address.Parse(Address)));

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.').ToLowerInvariant();
                var answers = new List<IDNSResourceRecord>();

                foreach (var type in Types)
                {

                    Queries.Enqueue($"{name} {type}");

                    if (records.TryGetValue((name, type), out var list))
                        answers.AddRange(list);

                }

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
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

        private const String Client  = "192.0.2.25";
        private const String UDomain = "bücher.test";
        private const String ADomain = "xn--bcher-kva.test";

        private static String Letter(String FromDomain)

            => $"From: <news@{FromDomain}>\r\n" +
               "To: <alice@hermod.test>\r\n" +
               "Subject: Grüße\r\n" +
               $"Message-ID: <{Guid.NewGuid():N}@{Idna.ToAscii(FromDomain)}>\r\n" +
               "Date: Sun, 22 Nov 2026 10:00:00 +0000\r\n" +
               "\r\n" +
               "Hallo.\r\n";

        /// <summary>
        /// The letter signed by hand, relaxed/relaxed with rsa-sha256 over From, To and Subject,
        /// the tags before b= as given - "{bh}" stands for the body hash. The tags are UTF-8, as
        /// in an EAI message.
        /// </summary>
        private static String Sign(RSA Key, String Letter, String Tags)
        {

            String[] names    = [ "from", "to", "subject" ];

            var (head, body)  = DkimCanonicalization.Split(Letter);
            var fields        = DkimCanonicalization.ParseFields(head);
            var bodyHash      = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(DkimCanonicalization.CanonicalizeBody(body, "relaxed"))));
            var tags          = Tags.Replace("{bh}", bodyHash) + $"; h={String.Join(':', names)}; b=";

            var input         = new StringBuilder();

            foreach (var name in names)
                input.Append(DkimCanonicalization.CanonicalizeHeader(fields.Last(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase)), "relaxed")).Append("\r\n");

            input.Append(DkimCanonicalization.CanonicalizeHeader(new DkimHeaderField("DKIM-Signature", " " + tags, "DKIM-Signature: " + tags), "relaxed"));

            var signature     = Convert.ToBase64String(Key.SignData(Encoding.UTF8.GetBytes(input.ToString()), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            return $"DKIM-Signature: {tags}{signature}\r\n{Letter}";

        }

        private static String KeyRecord(RSA Key)
            => $"v=DKIM1; k=rsa; p={Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo())}";

        private static Task<DnsVerificationResult> Verify(ZoneDNS DNS, String Message, String MailFrom, String ClientAddress = Client)

            => new DNSVerifier(DNS, new QuietLogger()).
                   VerifyAsync(MailFrom[(MailFrom.IndexOf('@') + 1)..],
                               System.Net.IPAddress.Parse(ClientAddress),
                               MailFrom,
                               "mx.unrelated.test",
                               EMailMessage.Parse(Encoding.UTF8.GetBytes(Message)));

        private static void AllAscii(ZoneDNS DNS)
            => Assert.That(DNS.Queries.All(query => query.All(c => c < 0x80)), Is.True, String.Join(", ", DNS.Queries));

        #endregion


        #region SPF (V-23)

        [Test(Description = "RFC 8616 §4: \"All U-labels MUST be converted to A-labels before being used for an SPF validation\" - MAIL FROM in U-labels finds the record of its A-labels")]
        public async Task SPF_of_a_sender_domain_in_U_labels_is_the_record_of_its_A_labels()
        {

            var dns    = new ZoneDNS().Txt(ADomain, $"v=spf1 ip4:{Client} -all");

            var result = await Verify(dns, Letter(UDomain), $"news@{UDomain}");

            Assert.That(result.Spf, Is.EqualTo(SPFResult.Pass), String.Join(", ", dns.Queries));
            AllAscii(dns);

        }


        [Test(Description = "RFC 8616 §4: a client the record of the A-labels does not list fails - it is not \"none\"")]
        public async Task SPF_fail_of_a_sender_domain_in_U_labels_is_not_lost()
        {

            var dns    = new ZoneDNS().Txt(ADomain, $"v=spf1 ip4:{Client} -all");

            var result = await Verify(dns, Letter(UDomain), $"news@{UDomain}", ClientAddress: "198.51.100.7");

            Assert.That(result.Spf, Is.EqualTo(SPFResult.Fail));

        }


        [TestCase("a:macro.%{d}")]
        [TestCase("a:%{o}")]
        [TestCase("exists:%{l}.%{o}")]
        public async Task The_macros_of_a_sender_domain_in_U_labels_expand_to_A_labels(String Mechanism)
        {

            var dns    = new ZoneDNS().Txt(ADomain,                        $"v=spf1 {Mechanism} -all").
                                       A  ($"macro.{ADomain}",             Client).
                                       A  (ADomain,                        Client).
                                       A  ($"news.{ADomain}",              Client);

            var result = await Verify(dns, Letter(UDomain), $"news@{UDomain}");

            Assert.That(result.Spf, Is.EqualTo(SPFResult.Pass), String.Join(", ", dns.Queries));
            AllAscii(dns);

        }


        [Test(Description = "RFC 7208 §4: MAIL FROM in A-labels finds the same record (a guard)")]
        public async Task SPF_of_a_sender_domain_in_A_labels_passes()
        {

            var dns    = new ZoneDNS().Txt(ADomain, $"v=spf1 ip4:{Client} -all");

            var result = await Verify(dns, Letter(ADomain), $"news@{ADomain}");

            Assert.That(result.Spf, Is.EqualTo(SPFResult.Pass));

        }

        #endregion

        #region DKIM (V-24)

        [TestCase("d=sig.bücher.test; s=test")]
        [TestCase("d=sig.bücher.test; i=@sig.xn--bcher-kva.test; s=test")]
        [TestCase("d=sig.xn--bcher-kva.test; i=news@sig.bücher.test; s=test")]
        [TestCase("d=sig.xn--bcher-kva.test; s=test")]
        public async Task A_signature_with_d_in_U_labels_is_verified_with_the_key_of_its_A_labels(String Tags)
        {

            using var key = RSA.Create(2048);

            var dns     = new ZoneDNS().Txt($"test._domainkey.sig.{ADomain}", KeyRecord(key));
            var message = Sign(key, Letter(UDomain), $"v=1; a=rsa-sha256; c=relaxed/relaxed; {Tags}; bh={{bh}}");

            var (result, details, domain) = await new DNSVerifier(dns, new QuietLogger()).VerifyDkimAsync(Encoding.UTF8.GetBytes(message));

            Assert.That(result, Is.EqualTo(DkimResult.Pass), $"{details}; " + String.Join(", ", dns.Queries));
            Assert.That(Idna.ToAscii(domain!), Is.EqualTo($"sig.{ADomain}"), "d= as written, in UTF-8 - not read as Latin-1");
            AllAscii(dns);

        }

        #endregion

        #region DMARC (V-25)

        [Test(Description = "RFC 8616 §6: the record of an author domain in U-labels is the one of its A-labels - its p=reject applies, not the p=none of the organizational domain")]
        public async Task The_policy_of_an_author_domain_in_U_labels_is_the_record_of_its_A_labels()
        {

            var dns    = new ZoneDNS().Txt($"_dmarc.sub.{ADomain}", "v=DMARC1; p=reject").
                                       Txt($"_dmarc.{ADomain}",     "v=DMARC1; p=none");

            var result = await Verify(dns, Letter($"sub.{UDomain}"), "bounce@bounces.unrelated.test");

            Assert.That(result.Dmarc,                     Is.EqualTo(DmarcResult.Fail), String.Join(", ", dns.Queries));
            Assert.That(result.DmarcDetail?.PolicyDomain, Is.EqualTo($"sub.{ADomain}"));
            Assert.That(result.DmarcDetail?.Disposition,  Is.EqualTo(DmarcDisposition.Reject));
            AllAscii(dns);

        }


        [Test(Description = "RFC 8616 §6, RFC 7489 §3.1.1: strict alignment compares the author domain by its A-labels - a signature of the A-labels is aligned")]
        public async Task Strict_alignment_compares_an_author_domain_in_U_labels_by_its_A_labels()
        {

            using var key = RSA.Create(2048);

            var dns     = new ZoneDNS().Txt($"_dmarc.{ADomain}",           "v=DMARC1; p=reject; adkim=s; aspf=s").
                                        Txt($"test._domainkey.{ADomain}", KeyRecord(key));
            var message = Sign(key, Letter(UDomain), $"v=1; a=rsa-sha256; c=relaxed/relaxed; d={ADomain}; s=test; bh={{bh}}");

            var result  = await Verify(dns, message, "bounce@bounces.unrelated.test");

            Assert.That(result.Dkim,                      Is.EqualTo(DkimResult.Pass), String.Join(", ", dns.Queries));
            Assert.That(result.Dmarc,                     Is.EqualTo(DmarcResult.Pass));
            Assert.That(result.DmarcDetail?.DkimAligned,  Is.True);

        }


        [Test(Description = "RFC 8616 §4, §6: SPF of MAIL FROM in U-labels is aligned with the author domain in A-labels")]
        public async Task SPF_of_a_sender_domain_in_U_labels_is_aligned_with_its_A_labels()
        {

            var dns    = new ZoneDNS().Txt($"_dmarc.{ADomain}", "v=DMARC1; p=reject; aspf=s").
                                       Txt(ADomain,             $"v=spf1 ip4:{Client} -all");

            var result = await Verify(dns, Letter(ADomain), $"news@{UDomain}");

            Assert.That(result.Spf,                      Is.EqualTo(SPFResult.Pass));
            Assert.That(result.Dmarc,                    Is.EqualTo(DmarcResult.Pass), String.Join(", ", dns.Queries));
            Assert.That(result.DmarcDetail?.SpfAligned,  Is.True);

        }


        [Test(Description = "RFC 7489 §6.6.3: the policy of the organizational domain applies to an author domain in U-labels without its own (a guard)")]
        public async Task The_organizational_policy_applies_to_an_author_domain_in_U_labels()
        {

            var dns    = new ZoneDNS().Txt($"_dmarc.{ADomain}", "v=DMARC1; p=reject");

            var result = await Verify(dns, Letter($"sub.{UDomain}"), "bounce@bounces.unrelated.test");

            Assert.That(result.DmarcDetail?.PolicyDomain, Is.EqualTo(ADomain));
            Assert.That(result.DmarcDetail?.Disposition,  Is.EqualTo(DmarcDisposition.Reject));

        }

        #endregion

    }

}
