/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// DKIM on the way out (H5): every domain signs with its own key, a message
    /// signed before it was queued is not signed again, and the public verifier
    /// finds the signature good - and bad, once the message was changed. And the
    /// smart host's password, which goes nowhere without TLS; a message queued as
    /// it is; the changes of a queued message, told.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region (private) DKIM keys and a DNS that has their records

        private sealed record Key(String Domain, String Selector, String PrivatePem, String PublicBase64)
        {

            public DkimSigner Signer
                => new (new DkimConfig {
                            Domain          = Domain,
                            Selector        = Selector,
                            PrivateKeyPem   = PrivatePem,
                            SignedHeaders   = "from:to:subject:date:message-id"
                        },
                        new QuietLogger());

            public static Key Make(String Domain)
            {
                using var rsa = RSA.Create(2048);
                return new Key(Domain, "test", rsa.ExportPkcs8PrivateKeyPem(), Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
            }

        }

        private sealed class DkimDNS(params Key[] Keys) : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private static DNSInfo Answer(IEnumerable<IDNSResourceRecord> Records)
                => new (origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                        [ .. Records ], [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero);

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Task.FromResult(Answer([]));

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Task.FromResult(Answer(Keys.Where (key => DNSServiceName.ToString().TrimEnd('.').Equals($"{key.Selector}._domainkey.{key.Domain}", StringComparison.OrdinalIgnoreCase)).
                                                Select(key => new TXT(DNSServiceName, DNSQueryClasses.IN, TimeSpan.FromHours(1), $"v=DKIM1; k=rsa; p={key.PublicBase64}"))));

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private static String Message(String From = "anna@example.org", String Body = "hello")

            => $"From: Anna <{From}>\r\n" +
               "To: you@next.example\r\n" +
               "Subject: signed\r\n" +
               "Date: Fri, 16 Oct 2026 09:30:00 +0200\r\n" +
               $"Message-ID: <{Guid.NewGuid():N}@example.org>\r\n" +
               "\r\n" +
               $"{Body}\r\n";

        #endregion


        [Test]
        public async Task A_DKIM_signature_is_verified_and_a_changed_message_fails()
        {

            var key       = Key.Make("example.org");
            var verifier  = new DNSVerifier(new DkimDNS(key), new QuietLogger());
            var signed    = key.Signer.SignMessage(Message());
            var changed   = signed.Replace("hello", "hallo");

            var good      = await verifier.VerifyDkimAsync(Encoding.ASCII.GetBytes(signed));
            var bad       = await verifier.VerifyDkimAsync(Encoding.ASCII.GetBytes(changed));
            var none      = await verifier.VerifyDkimAsync(Encoding.ASCII.GetBytes(Message()));

            Assert.Multiple(() => {

                Assert.That(good.Result,  Is.EqualTo(DkimResult.Pass), good.Details);
                Assert.That(good.Domain,  Is.EqualTo("example.org"));
                Assert.That(bad.Result,   Is.EqualTo(DkimResult.Fail));
                Assert.That(none.Result,  Is.EqualTo(DkimResult.None));

                Assert.That(DkimSigning.FromDomain(signed),                Is.EqualTo("example.org"));
                Assert.That(DkimSigning.IsSignedBy(signed, "example.org"), Is.True);
                Assert.That(DkimSigning.IsSignedBy(signed, "example.com"), Is.False);
                Assert.That(DkimSigning.FromDomain("From: a@x.example, b@y.example\r\n\r\nx\r\n"), Is.Null, "two authors align with nobody");

            });

        }

        [Test]
        public async Task Every_domain_signs_with_its_own_key_and_nothing_is_signed_twice()
        {

            var org       = Key.Make("example.org");
            var net       = Key.Make("example.net");
            var signers   = new Dictionary<String, DkimSigner> { ["example.org"] = org.Signer, ["example.net"] = net.Signer };

            using var nextHop = new NextHop();

            var client = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                    LocalHostname     = "relay.hermod.test",
                                                    SmartHost         = "127.0.0.1",
                                                    SmartHostPort     = nextHop.Port,
                                                    ConnectTimeoutMs  = 3_000,
                                                    ReadTimeoutMs     = 5_000,
                                                    WriteTimeoutMs    = 3_000
                                                },
                                                null,
                                                new NoDNS(),
                                                new QuietLogger(),
                                                dkimSignerFor: domain => signers.GetValueOrDefault(domain));

            async Task<String> Sent(String Message)
            {
                nextHop.DataLines.Clear();
                var result = await client.SendAsync("next.example", "anna@example.org", [ "you@next.example" ], Message);
                Assert.That(result.Status, Is.EqualTo(SendStatus.Success), result.ResponseText);
                await Task.Delay(100);
                return String.Join("\r\n", nextHop.DataLines) + "\r\n";
            }

            var fromOrg       = await Sent(Message("anna@example.org"));
            var fromNet       = await Sent(Message("ben@example.net"));
            var fromElsewhere = await Sent(Message("carol@example.com"));
            var signedBefore  = await Sent(org.Signer.SignMessage(Message("anna@example.org", "signed before it was queued")));

            var verifier      = new DNSVerifier(new DkimDNS(org, net), new QuietLogger());
            var orgResult     = (await verifier.VerifyDkimAsync(Encoding.ASCII.GetBytes(fromOrg))).Result;
            var netResult     = (await verifier.VerifyDkimAsync(Encoding.ASCII.GetBytes(fromNet))).Result;
            var beforeResult  = (await verifier.VerifyDkimAsync(Encoding.ASCII.GetBytes(signedBefore))).Result;

            Assert.Multiple(() => {

                Assert.That(fromOrg,        Does.Contain("d=example.org"));
                Assert.That(fromNet,        Does.Contain("d=example.net"));
                Assert.That(fromElsewhere,  Does.Not.Contain("DKIM-Signature"), "a domain this server does not sign for");

                Assert.That(orgResult,      Is.EqualTo(DkimResult.Pass), "as the next hop got it");
                Assert.That(netResult,      Is.EqualTo(DkimResult.Pass));

                Assert.That(signedBefore.Split("DKIM-Signature").Length - 1, Is.EqualTo(1), "signed once, before it was queued");
                Assert.That(beforeResult,   Is.EqualTo(DkimResult.Pass));

            });

        }

        [Test]
        public async Task The_smart_host_gets_no_password_without_TLS()
        {

            using var nextHop = new NextHop(line => line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase) ? "235 2.7.0 ok" : null,
                                            Extensions: [ "8BITMIME", "AUTH PLAIN LOGIN" ]);

            SMTPOutboundClient Client(Boolean AllowCleartext)

                => new (new SmtpOutboundConfig {
                            LocalHostname            = "relay.hermod.test",
                            SmartHost                = "127.0.0.1",
                            SmartHostPort            = nextHop.Port,
                            SmartHostUsername        = "relay",
                            SmartHostPassword        = "relay-secret",
                            SmartHostAuthWithoutTls  = AllowCleartext,
                            ConnectTimeoutMs         = 3_000,
                            ReadTimeoutMs            = 5_000,
                            WriteTimeoutMs           = 3_000
                        },
                        null, new NoDNS(), new QuietLogger());

            var refused = await Client(AllowCleartext: false).SendAsync("next.example", "anna@example.org", [ "you@next.example" ], Message());
            await Task.Delay(100);
            var sentAuth = nextHop.Commands.Any(command => command.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase));

            var allowed = await Client(AllowCleartext: true).SendAsync("next.example", "anna@example.org", [ "you@next.example" ], Message());

            Assert.Multiple(() => {
                Assert.That(refused.Status,  Is.EqualTo(SendStatus.TempFail), "the message waits");
                Assert.That(refused.ResponseCode, Is.EqualTo(454));
                Assert.That(sentAuth,        Is.False, "no AUTH in cleartext");
                Assert.That(allowed.Status,  Is.EqualTo(SendStatus.Success), "for a relay on the loopback, when asked for");
            });

        }

        [Test]
        public async Task A_message_is_queued_as_it_is_and_its_changes_are_told()
        {

            var message = Message();

            // Queued as it is: one item per recipient domain, the octets untouched.
            var queue   = new MemoryQueue();
            var ids     = await new MailSender(queue, new QuietLogger()).SendAsync("anna@example.org",
                                                                                   [ "a@next.example", "b@next.example", "c@other.example" ],
                                                                                   message);

            Assert.Multiple(() => {
                Assert.That(ids,                                                              Has.Count.EqualTo(2));
                Assert.That(queue.Mails.Values.Select(mail => mail.TargetDomain).Order(),     Is.EqualTo(new[] { "next.example", "other.example" }));
                Assert.That(queue.Mails.Values.Single(mail => mail.TargetDomain == "next.example").EnvelopeTo, Is.EqualTo(new[] { "a@next.example", "b@next.example" }));
                Assert.That(queue.Mails.Values.All(mail => mail.MessageContent == message),   Is.True);
            });

            // Delivered, and every step told.
            using var nextHop  = new NextHop();
            var       relay    = new MemoryQueue();
            var       logger   = new QuietLogger();
            var       told     = new ConcurrentQueue<QueueItemStatus>();
            var       processor = new QueueProcessor(relay,
                                                     ClientFor(nextHop),
                                                     new BounceHandler(new SMTPServerConfig { Hostname = "relay.hermod.test" }, relay, logger),
                                                     new QueueProcessorConfig { DomainCooldownSeconds = 0 },
                                                     logger);

            processor.OnStatusChanged += mail => told.Enqueue(mail.Status);

            var sent = await new MailSender(relay, logger).SendAsync("anna@example.org", [ "you@next.example" ], message);

            await processor.StartAsync();

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10) && !told.Contains(QueueItemStatus.Delivered))
                await Task.Delay(50);

            await processor.StopAsync();
            await processor.DisposeAsync();

            Assert.That(told, Is.EqualTo(new[] { QueueItemStatus.Processing, QueueItemStatus.Delivered }));

        }

    }

}
