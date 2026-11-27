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

using System.Security.Cryptography;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The results of a DKIM signature that neither passes nor fails its verification (RFC 6376
    /// §6.1.1, §6.1.2, RFC 8601 §2.7.1): "temperror" for a key that could not be retrieved, and
    /// "permerror" for a signature that could not be evaluated - and DMARC on a temporary error
    /// (RFC 7489 §6.6.2). The findings V-31 and V-32 of SMTPConformanceTests.
    /// </summary>
    public partial class DkimVerifierTests
    {

        /// <summary>
        /// TXT records by owner name, answers with an error code, and lookups that throw.
        /// </summary>
        private sealed class ResultDNS : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private readonly Dictionary<String, String>            texts    = [];
            private readonly Dictionary<String, DNSResponseCodes>  errors   = [];
            private readonly HashSet<String>                       timeouts = [];

            public ResultDNS Txt(String Name, String Text)
            {
                texts[Name] = Text;
                return this;
            }

            public ResultDNS Error(String Name, DNSResponseCodes Code)
            {
                errors[Name] = Code;
                return this;
            }

            public ResultDNS Timeout(String Name)
            {
                timeouts.Add(Name);
                return this;
            }

            private Task<DNSInfo> Answer(String Owner, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Owner.TrimEnd('.').ToLowerInvariant();
                var isTxt   = Types.Contains(DNSResourceRecordTypes.TXT);

                if (isTxt && timeouts.Contains(name))
                    throw new TimeoutException($"{name} TXT");

                var code    = isTxt && errors.TryGetValue(name, out var error) ? error : DNSResponseCodes.NoError;
                var answers = isTxt && code == DNSResponseCodes.NoError && texts.TryGetValue(name, out var text)
                                  ? new IDNSResourceRecord[] { new TXT(DNSServiceName.Parse(name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), text) }
                                  : [];

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, code,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public override String ToString()
                => nameof(ResultDNS);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private const String KeyName = "test._domainkey.client.example";

        private static async Task<(DkimResult Result, String? Details)> Verify(ResultDNS DNS, String Message)
        {
            var (result, details, _) = await new DNSVerifier(DNS, new QuietLogger()).VerifyDkimAsync(Encoding.UTF8.GetBytes(Message));
            return (result, details);
        }


        #region V-31: a DNS error at the key is a temporary error

        [TestCase(false, TestName = "A SERVFAIL at the key is a temporary error")]
        [TestCase(true,  TestName = "A timeout at the key is a temporary error")]
        public async Task A_DNS_error_at_the_key_is_a_temporary_error(Boolean Timeout)
        {

            using var key = RSA.Create(2048);

            var dns = Timeout ? new ResultDNS().Timeout(KeyName)
                              : new ResultDNS().Error  (KeyName, DNSResponseCodes.ServerFailure);

            var (result, details) = await Verify(dns, Sign(key, Usual));

            Assert.That(result, Is.EqualTo(DkimResult.TempError), details);

        }


        [TestCase(true,  TestName = "A key record that does not exist is a permanent error: NXDOMAIN")]
        [TestCase(false, TestName = "A key record that does not exist is a permanent error: no TXT record")]
        public async Task A_key_record_that_does_not_exist_is_a_permanent_error(Boolean NameError)
        {

            using var key = RSA.Create(2048);

            var dns = NameError ? new ResultDNS().Error(KeyName, DNSResponseCodes.NameError)
                                : new ResultDNS();

            var (result, details) = await Verify(dns, Sign(key, Usual));

            Assert.That(result, Is.EqualTo(DkimResult.PermError), details);

        }


        [TestCase(false, TestName = "DMARC on a SERVFAIL at the author's key is a temporary error: p=reject is not applied")]
        [TestCase(true,  TestName = "DMARC on a timeout at the author's key is a temporary error: p=reject is not applied")]
        public async Task DMARC_on_a_DNS_error_at_the_authors_key(Boolean Timeout)
        {

            using var key = RSA.Create(2048);

            var dns = new ResultDNS().Txt("_dmarc.client.example", "v=DMARC1; p=reject");

            if (Timeout) dns.Timeout(KeyName);
            else         dns.Error  (KeyName, DNSResponseCodes.ServerFailure);

            var result = await new DNSVerifier(dns, new QuietLogger()).
                                   VerifyAsync("bounces.unrelated.test", System.Net.IPAddress.Parse("192.0.2.25"), "bounce@bounces.unrelated.test",
                                               "mx.unrelated.test", EMailMessage.Parse(Sign(key, Usual)));

            Assert.Multiple(() => {
                Assert.That(result.Dkim,                      Is.EqualTo(DkimResult.TempError));
                Assert.That(result.Dmarc,                     Is.EqualTo(DmarcResult.TempError));
                Assert.That(result.DmarcDetail?.Disposition,  Is.EqualTo(DmarcDisposition.None));
            });

        }


        [Test(Description = "RFC 6376 §6.1.2: a key record that does not exist is PERMFAIL - with nothing else for the author, DMARC fails, and p=reject applies")]
        public async Task DMARC_on_a_key_record_that_does_not_exist_applies_p_reject()
        {

            using var key = RSA.Create(2048);

            var dns    = new ResultDNS().Txt  ("_dmarc.client.example", "v=DMARC1; p=reject").
                                         Error(KeyName, DNSResponseCodes.NameError);

            var result = await new DNSVerifier(dns, new QuietLogger()).
                                   VerifyAsync("bounces.unrelated.test", System.Net.IPAddress.Parse("192.0.2.25"), "bounce@bounces.unrelated.test",
                                               "mx.unrelated.test", EMailMessage.Parse(Sign(key, Usual)));

            Assert.Multiple(() => {
                Assert.That(result.Dmarc,                     Is.EqualTo(DmarcResult.Fail));
                Assert.That(result.DmarcDetail?.Disposition,  Is.EqualTo(DmarcDisposition.Reject));
            });

        }

        #endregion

        #region V-32: a signature that cannot be evaluated is a permanent error

        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}",                                   "v=DKIM1; k=rsa; ",          TestName = "A signature without bh= is a permanent error")]
        [TestCase("v=2; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}",                          "v=DKIM1; k=rsa; ",          TestName = "A signature of version 2 is a permanent error")]
        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; d=client.example; s=test; h={h}; bh={bh}",        "v=DKIM1; k=rsa; ",          TestName = "A signature with a tag named twice is a permanent error")]
        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}",                          "v=DKIM1; k=rsa; k=rsa; ",   TestName = "A key record with a tag named twice is a permanent error")]
        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}",                          "k=rsa; v=DKIM1; ",          TestName = "A key record whose v= is not its first tag is a permanent error")]
        public async Task A_signature_that_cannot_be_evaluated_is_a_permanent_error(String Tags, String TagsBeforeKey)
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(new ResultDNS().Txt(KeyName, Record(key, TagsBeforeKey)), Sign(key, Tags));

            Assert.That(result, Is.EqualTo(DkimResult.PermError), details);

        }


        [Test(Description = "RFC 6376 §6.1.2: a key record without p= cannot be used - a permanent error")]
        public async Task A_key_record_without_p_is_a_permanent_error()
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(new ResultDNS().Txt(KeyName, "v=DKIM1; k=rsa"), Sign(key, Usual));

            Assert.That(result, Is.EqualTo(DkimResult.PermError), details);

        }


        [Test(Description = "RFC 6376 §6.1.2: \"If the \"p=\" tag is empty, the key has been revoked; the Verifier MUST treat this as a failed signature check\" - fail")]
        public async Task A_signature_of_a_revoked_key_fails()
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(new ResultDNS().Txt(KeyName, "v=DKIM1; k=rsa; p="), Sign(key, Usual));

            Assert.That(result, Is.EqualTo(DkimResult.Fail), details);

        }


        [Test(Description = "RFC 8601 §2.7.1: a signature that was verified and did not hold - fail")]
        public async Task A_signature_over_a_changed_body_fails()
        {

            using var key = RSA.Create(2048);

            var (result, details) = await Verify(new ResultDNS().Txt(KeyName, Record(key)), Sign(key, Usual).Replace("Hello.", "Hallo."));

            Assert.That(result, Is.EqualTo(DkimResult.Fail), details);

        }

        #endregion

    }

}
