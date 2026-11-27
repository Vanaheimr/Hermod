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
    /// What the DKIM verifier checks of a signature and its key record before it hashes
    /// anything (RFC 6376 §6.1.1, §6.1.2, RFC 8301 §3.2): the required tags and the version,
    /// From among the signed fields, i= within d=, the key size, and the key record's k=, h=,
    /// s= and t=s. Every signature is a valid one over what it says, made by hand. The
    /// findings V-13 to V-17 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public partial class DkimVerifierTests
    {

        #region Setup

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        /// <summary>
        /// The TXT records of one owner name; every other query has no answer.
        /// </summary>
        private sealed class KeyDNS(String Name, String Text) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private Task<DNSInfo> Answer(String Owner, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var answers = Owner.TrimEnd('.').Equals(Name, StringComparison.OrdinalIgnoreCase) && Types.Contains(DNSResourceRecordTypes.TXT)
                                  ? new IDNSResourceRecord[] { new TXT(DNSServiceName.Parse(Name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), Text) }
                                  : [];

                return Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                                   answers, [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            }

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DomainName.FullName, ResourceRecordTypes);

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Answer(DNSServiceName.FullName, ResourceRecordTypes);

            public override String ToString()
                => nameof(KeyDNS);

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private const String Letter = "From: <news@client.example>\r\n" +
                                      "To: <alice@hermod.test>\r\n" +
                                      "Subject: DKIM verifier\r\n" +
                                      "\r\n" +
                                      "Hello.\r\n";

        /// <summary>
        /// The letter signed by hand, relaxed/relaxed with rsa-sha256 over the given fields, the
        /// tags before b= as given - "{bh}" stands for the body hash and "{h}" for the fields.
        /// </summary>
        private static String Sign(RSA Key, String Tags, params String[] Fields)
        {

            if (Fields.Length == 0)
                Fields = [ "from", "to", "subject" ];

            var (head, body)  = DkimCanonicalization.Split(Letter);
            var fields        = DkimCanonicalization.ParseFields(head);
            var bodyHash      = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(DkimCanonicalization.CanonicalizeBody(body, "relaxed"))));
            var tags          = Tags.Replace("{bh}", bodyHash).Replace("{h}", String.Join(':', Fields)) + "; b=";

            var input         = new StringBuilder();

            foreach (var name in Fields)
                input.Append(DkimCanonicalization.CanonicalizeHeader(fields.Last(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase)), "relaxed")).Append("\r\n");

            input.Append(DkimCanonicalization.CanonicalizeHeader(new DkimHeaderField("DKIM-Signature", " " + tags, "DKIM-Signature: " + tags), "relaxed"));

            var signature     = Convert.ToBase64String(Key.SignData(Encoding.UTF8.GetBytes(input.ToString()), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            return $"DKIM-Signature: {tags}{signature}\r\n{Letter}";

        }

        private const String Usual = "v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}";

        private static String Record(RSA Key, String TagsBeforeKey = "v=DKIM1; k=rsa; ")
            => TagsBeforeKey + $"p={Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo())}";

        private static async Task<(DkimResult Result, String? Details)> Verify(String Message, String KeyRecord)
        {
            var (result, details, _) = await new DNSVerifier(new KeyDNS("test._domainkey.client.example", KeyRecord), new QuietLogger()).
                                                 VerifyDkimAsync(Encoding.UTF8.GetBytes(Message));
            return (result, details);
        }

        #endregion


        [Test(Description = "RFC 6376 §6.1: a signature made by hand over From, To and Subject verifies - a guard")]
        public async Task A_signature_made_by_hand_verifies()
        {
            using var key = RSA.Create(2048);
            var (result, details) = await Verify(Sign(key, Usual), Record(key));
            Assert.That(result, Is.EqualTo(DkimResult.Pass), details);
        }

        [Test(Description = "RFC 6376 §6.1.1: \"If the \"h=\" tag does not include the From header field, the Verifier MUST ignore the DKIM-Signature header field\"")]
        public async Task A_signature_that_does_not_sign_From_does_not_verify()
        {
            using var key = RSA.Create(2048);
            var (result, details) = await Verify(Sign(key, Usual, "to", "subject"), Record(key));
            Assert.That(result, Is.Not.EqualTo(DkimResult.Pass), details);
        }

        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}",              true,  Description = "all")]
        [TestCase("a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}",                   false, Description = "no v=")]
        [TestCase("v=2; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}",              false, Description = "v=2")]
        [TestCase("v=1; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}",                            false, Description = "no a=")]
        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}; i=@client.example",       true,  Description = "i= the domain")]
        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}; i=news@lists.client.example", true, Description = "i= a subdomain")]
        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}; i=news@evil.example",     false, Description = "i= another domain")]
        [TestCase("v=1; a=rsa-sha256; c=relaxed/relaxed; d=client.example; s=test; h={h}; bh={bh}; i=news@xclient.example",  false, Description = "i= a domain ending in d= without a dot")]
        public async Task The_tags_of_the_signature(String Tags, Boolean Verifies)
        {
            using var key = RSA.Create(2048);
            var (result, details) = await Verify(Sign(key, Tags), Record(key));
            Assert.That(result == DkimResult.Pass, Is.EqualTo(Verifies), details);
        }

        [TestCase( 512, false)]
        [TestCase(1024, true)]
        [TestCase(2048, true)]
        public async Task The_size_of_an_RSA_key(Int32 Bits, Boolean Verifies)
        {
            using var key = RSA.Create(Bits);
            var (result, details) = await Verify(Sign(key, Usual), Record(key));
            Assert.That(result == DkimResult.Pass, Is.EqualTo(Verifies), details);
        }

        [TestCase("v=DKIM1; k=rsa; h=sha1; ",          null,                              false)]
        [TestCase("v=DKIM1; k=rsa; h=sha1:sha256; ",   null,                              true)]
        [TestCase("v=DKIM1; k=rsa; s=other; ",         null,                              false)]
        [TestCase("v=DKIM1; k=rsa; s=email; ",         null,                              true)]
        [TestCase("v=DKIM1; k=rsa; s=*; ",             null,                              true)]
        [TestCase("v=DKIM1; k=rsa; s=other:email; ",   null,                              true)]
        [TestCase("v=DKIM1; k=rsa; t=s; ",             "news@lists.client.example",       false)]
        [TestCase("v=DKIM1; k=rsa; t=s; ",             "news@client.example",             true)]
        [TestCase("v=DKIM1; k=rsa; t=y; ",             "news@lists.client.example",       true)]
        [TestCase("v=DKIM1; k=dsa; ",                  null,                              false)]
        [TestCase("v=DKIM1; ",                         null,                              true,  Description = "k=rsa by default")]
        public async Task The_tags_of_the_key_record(String KeyTags, String? Identity, Boolean Verifies)
        {
            using var key = RSA.Create(2048);
            var signed = Sign(key, Identity is null ? Usual : Usual + $"; i={Identity}");
            var (result, details) = await Verify(signed, Record(key, KeyTags));
            Assert.That(result == DkimResult.Pass, Is.EqualTo(Verifies), details);
        }

    }

}
