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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// SMTPServerConfig.VerifySpf, VerifyDkim and VerifyDmarc: a check that is switched
    /// off neither rejects a message nor appears in its Authentication-Results.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// TXT records by owner name; every other query has no answer.
        /// </summary>
        private sealed class TxtDNS(params (String Name, String Text)[] Records) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private Task<DNSInfo> Answer(String Name, IEnumerable<DNSResourceRecordTypes> Types)
            {

                var name    = Name.TrimEnd('.').ToLowerInvariant();
                var answers = Types.Contains(DNSResourceRecordTypes.TXT)
                                  ? Records.Where (record => record.Name == name).
                                            Select(record => (IDNSResourceRecord) new TXT(DNSServiceName.Parse(name), DNSQueryClasses.IN, TimeSpan.FromMinutes(5), record.Text)).
                                            ToArray()
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

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

            public override String ToString()
                => "TXT records";

        }

        private static readonly (String, String) SpfHardFail  = ("client.example",        "v=spf1 -all");
        private static readonly (String, String) DmarcReject  = ("_dmarc.client.example", "v=DMARC1; p=reject");


        /// <summary>
        /// Send one message from sender@client.example; the reply to its end of data.
        /// </summary>
        private static async Task<(Int32 Code, String Text)> SendFromClientExample(Server Server)
        {

            using var wire = await Server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("From: sender@client.example\r\nSubject: checked\r\n\r\nbody\r\n.\r\n");

            return await wire.ReplyAsync();

        }

        private static String AuthenticationResults(Server Server)
        {
            var message = Server.Storage.Messages.Single();
            return message[..message.IndexOf("Received:", StringComparison.Ordinal)];
        }


        [Test]
        public async Task An_SPF_hard_fail_is_rejected_while_VerifySpf_is_on()
        {

            await using var server = new Server(DNS: new TxtDNS(SpfHardFail));

            var reply = await SendFromClientExample(server);

            Assert.That(reply.Code, Is.EqualTo(550));
            Assert.That(reply.Text, Does.StartWith("5.7.23 "));

        }


        [Test]
        public async Task VerifySpf_off_neither_rejects_nor_reports_SPF()
        {

            await using var server = new Server(DNS: new TxtDNS(SpfHardFail), Configure: config => config with { VerifySpf = false });

            Assert.That((await SendFromClientExample(server)).Code, Is.EqualTo(250));

            var results = AuthenticationResults(server);
            Assert.That(results, Does.Not.Contain("spf="));
            Assert.That(results, Does.Contain("dkim=none").And.Contain("dmarc=none"), "the other checks still ran");

        }


        [Test]
        public async Task A_DMARC_reject_policy_is_enforced_while_VerifyDmarc_is_on()
        {

            await using var server = new Server(DNS: new TxtDNS(DmarcReject));

            var reply = await SendFromClientExample(server);

            Assert.That(reply.Code, Is.EqualTo(550));
            Assert.That(reply.Text, Does.Contain("DMARC"));

        }


        [Test]
        public async Task VerifyDmarc_off_neither_enforces_nor_reports_DMARC()
        {

            await using var server = new Server(DNS: new TxtDNS(DmarcReject), Configure: config => config with { VerifyDmarc = false });

            Assert.That((await SendFromClientExample(server)).Code, Is.EqualTo(250));

            var results = AuthenticationResults(server);
            Assert.That(results, Does.Not.Contain("dmarc="));
            Assert.That(results, Does.Contain("spf=none").And.Contain("dkim=none"), "the other checks still ran");

        }


        /// <summary>
        /// Send one message with the given envelope sender and header; the reply to its end of data.
        /// </summary>
        private static async Task<(Int32 Code, String Text)> Send(Server Server, String MailFrom, String Header, String Helo = "client.example")
        {

            using var wire = await Server.ConnectAsync();

            Assert.That(await wire.CommandAsync($"EHLO {Helo}"),                Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"MAIL FROM:<{MailFrom}>"),     Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                        Is.EqualTo(354));
            await wire.SendAsync(Header + "Subject: checked\r\n\r\nbody\r\n.\r\n");

            return await wire.ReplyAsync();

        }


        [Test]
        public async Task A_message_naming_more_than_one_author_is_rejected()
        {

            // DMARC passes on whichever From: it reads; the reader is shown the other.
            await using var server = new Server(DNS: new TxtDNS());

            var twoFields     = await Send(server, "sender@client.example", "From: Chef <ceo@victim.example>\r\nFrom: sender@client.example\r\n");
            var twoAddresses  = await Send(server, "sender@client.example", "From: ceo@victim.example, sender@client.example\r\n");
            var one           = await Send(server, "sender@client.example", "From: Sender <sender@client.example>\r\n");
            var none          = await Send(server, "sender@client.example", "");

            Assert.Multiple(() => {
                Assert.That(twoFields.Code,    Is.EqualTo(550));
                Assert.That(twoFields.Text,    Does.StartWith("5.7.1 ").And.Contain("one From: address"));
                Assert.That(twoAddresses.Code, Is.EqualTo(550));
                Assert.That(one.Code,          Is.EqualTo(250));
                Assert.That(none.Code,         Is.EqualTo(250), "no author names nobody: nothing for DMARC to pass on");
            });

        }


        [Test]
        public async Task A_null_sender_is_checked_on_its_From_and_its_HELO()
        {

            // A "bounce" in the name of a domain with p=reject; another from a HELO whose SPF says -all.
            await using var rejecting = new Server(DNS: new TxtDNS(DmarcReject));
            await using var hardFail  = new Server(DNS: new TxtDNS(SpfHardFail));
            await using var watching  = new Server(DNS: new TxtDNS(("_dmarc.client.example", "v=DMARC1; p=none")));

            var forged   = await Send(rejecting, "", "From: sender@client.example\r\n");
            var helo     = await Send(hardFail,  "", "From: someone@elsewhere.example\r\n");
            var bare     = await Send(watching,  "", "From: sender@client.example\r\n", Helo: "[192.0.2.1]");

            Assert.Multiple(() => {

                Assert.That(forged.Code,  Is.EqualTo(550), "DMARC of the From: domain, as for any other message");
                Assert.That(forged.Text,  Does.Contain("DMARC"));

                Assert.That(helo.Code,    Is.EqualTo(550), "SPF of the HELO identity (RFC 7208 §2.4)");
                Assert.That(helo.Text,    Does.StartWith("5.7.23 "));

                Assert.That(bare.Code,    Is.EqualTo(250));
                Assert.That(AuthenticationResults(watching), Does.Contain("spf=none").And.Contain("dmarc=fail"), "checked, and said so - not left without a word");

            });

        }


        [Test]
        public async Task With_every_check_off_Authentication_Results_says_none()
        {

            await using var server = new Server(DNS:       new TxtDNS(SpfHardFail, DmarcReject),
                                                Configure: config => config with { VerifySpf = false, VerifyDkim = false, VerifyDmarc = false });

            Assert.That((await SendFromClientExample(server)).Code, Is.EqualTo(250));

            Assert.That(AuthenticationResults(server), Is.EqualTo("Authentication-Results: mx.hermod.test; none\r\n"));

        }


        #region Forwarded mail: SPF fails, the author's DKIM signature holds

        /// <summary>
        /// A DKIM key of a domain - its signer and its record in DNS.
        /// </summary>
        private sealed record DkimKey(DkimSigner Signer, (String Name, String Text) Record)
        {

            public static DkimKey Make(String Domain)
            {

                using var rsa = RSA.Create(2048);

                var config = new DkimConfig {
                                 Domain         = Domain,
                                 Selector       = "test",
                                 PrivateKeyPem  = rsa.ExportPkcs8PrivateKeyPem(),
                                 SignedHeaders  = "from:to:subject:date:message-id"
                             };

                return new DkimKey(new DkimSigner(config, new QuietLogger()),
                                   ($"test._domainkey.{Domain}", $"v=DKIM1; k=rsa; p={Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())}"));

            }

        }

        private static String Letter()

            => "From: Sender <sender@client.example>\r\n" +
               "To: alice@hermod.test\r\n" +
               "Subject: forwarded\r\n" +
               "Date: Sat, 17 Oct 2026 20:00:00 +0200\r\n" +
               $"Message-ID: <{Guid.NewGuid():N}@client.example>\r\n" +
               "\r\n" +
               "body\r\n";

        /// <summary>
        /// Send a message as it is, from sender@client.example as before it was forwarded;
        /// the reply to its end of data.
        /// </summary>
        private static async Task<(Int32 Code, String Text)> SendAsIs(Server Server, String Message)
        {

            using var wire = await Server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO forwarder.example"),           Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"),       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                              Is.EqualTo(354));
            await wire.SendAsync(Message + ".\r\n");

            return await wire.ReplyAsync();

        }


        [Test]
        public async Task A_forwarded_message_its_author_signed_is_not_rejected_for_failing_SPF()
        {

            // Another server forwards it: SPF of client.example says -all for that server,
            // but client.example's own signature holds - with p=reject and without DMARC.
            var key            = DkimKey.Make("client.example");
            await using var withDmarc     = new Server(DNS: new TxtDNS(SpfHardFail, DmarcReject, key.Record));
            await using var withoutDmarc  = new Server(DNS: new TxtDNS(SpfHardFail, key.Record));

            var dmarc          = await SendAsIs(withDmarc,    key.Signer.SignMessage(Letter()));
            var noDmarc        = await SendAsIs(withoutDmarc, key.Signer.SignMessage(Letter()));

            Assert.Multiple(() => {

                Assert.That(dmarc.Code,                            Is.EqualTo(250), dmarc.Text);
                Assert.That(AuthenticationResults(withDmarc),      Does.Contain("spf=fail").And.Contain("dkim=pass header.d=client.example").And.Contain("dmarc=pass"),
                                                                   "SPF failed, and says so");

                Assert.That(noDmarc.Code,                          Is.EqualTo(250), noDmarc.Text);
                Assert.That(AuthenticationResults(withoutDmarc),   Does.Contain("spf=fail").And.Contain("dkim=pass header.d=client.example").And.Contain("dmarc=none"));

            });

        }


        [Test]
        public async Task An_SPF_hard_fail_is_still_rejected_when_the_author_did_not_sign()
        {

            // Signed by a mail service alone, or by the author before somebody changed it.
            var author         = DkimKey.Make("client.example");
            var mailer         = DkimKey.Make("mailer.example");
            await using var server = new Server(DNS: new TxtDNS(SpfHardFail, author.Record, mailer.Record));

            var otherDomain    = await SendAsIs(server, mailer.Signer.SignMessage(Letter()));
            var changed        = await SendAsIs(server, author.Signer.SignMessage(Letter()).Replace("body", "bodY"));

            Assert.Multiple(() => {
                Assert.That(otherDomain.Code,  Is.EqualTo(550));
                Assert.That(otherDomain.Text,  Does.StartWith("5.7.23 "));
                Assert.That(changed.Code,      Is.EqualTo(550));
                Assert.That(changed.Text,      Does.StartWith("5.7.23 "));
                Assert.That(server.Storage.Messages, Is.Empty);
            });

        }


        [Test]
        public async Task DMARC_passes_on_the_authors_signature_behind_a_mail_services()
        {

            // The mail service signs last, so its signature comes first; DMARC passes on
            // any signature that holds and is aligned (RFC 7489 §4.2), not on the first alone.
            var author         = DkimKey.Make("client.example");
            var mailer         = DkimKey.Make("mailer.example");
            await using var server = new Server(DNS: new TxtDNS(DmarcReject, author.Record, mailer.Record));

            var reply          = await SendAsIs(server, mailer.Signer.SignMessage(author.Signer.SignMessage(Letter())));

            Assert.Multiple(() => {
                Assert.That(reply.Code,                     Is.EqualTo(250), reply.Text);
                Assert.That(AuthenticationResults(server),  Does.Contain("dkim=pass header.d=client.example").And.Contain("dmarc=pass"),
                                                            "the author's signature is the one named");
            });

        }

        #endregion

    }

}
