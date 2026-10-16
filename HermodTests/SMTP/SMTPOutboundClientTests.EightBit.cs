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

using System.Diagnostics;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// A message whose 8-bit body is not UTF-8 - ISO-8859-1 here - goes through as
    /// the octets it is: signed over them, sent as them, verified over them, and
    /// returned as them in a report. Decoded as UTF-8 on the way, every umlaut
    /// would have become U+FFFD, and the DKIM signature would have failed.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region (private) An 8-bit message in ISO-8859-1

        private static Byte[] Latin1Message(String From = "anna@example.org")

            => Encoding.Latin1.GetBytes($"From: Anna <{From}>\r\n" +
                                        "To: you@next.example\r\n" +
                                        "Subject: Gruesse\r\n" +
                                        "Date: Fri, 16 Oct 2026 09:30:00 +0200\r\n" +
                                        $"Message-ID: <{Guid.NewGuid():N}@example.org>\r\n" +
                                        "MIME-Version: 1.0\r\n" +
                                        "Content-Type: text/plain; charset=iso-8859-1\r\n" +
                                        "Content-Transfer-Encoding: 8bit\r\n" +
                                        "\r\n" +
                                        "Grüße aus Köln, ½ Maß Kölsch.\r\n" +
                                        ".Eine Zeile, die mit einem Punkt beginnt.\r\n");

        private static Boolean EndsWith(Byte[] Octets, Byte[] End)
            => Octets.AsSpan().EndsWith(End);

        #endregion


        [Test]
        public async Task An_8bit_message_in_Latin1_is_signed_and_verified_over_its_octets()
        {

            var key       = Key.Make("example.org");
            var verifier  = new DNSVerifier(new DkimDNS(key), new QuietLogger());
            var message   = Latin1Message();
            var signed    = key.Signer.SignOctets(message);

            var changed   = (Byte[]) signed.Clone();
            changed[Array.LastIndexOf(changed, (Byte) 0xF6)] = 0xE4;     // ö becomes ä

            var good      = await verifier.VerifyDkimAsync(signed);
            var bad       = await verifier.VerifyDkimAsync(changed);

            // What signing the UTF-8 decoding had made of it: the umlauts are U+FFFD now, and
            // the octets that go out are no longer those of the message.
            var decoded   = Encoding.UTF8.GetBytes(key.Signer.SignMessage(Encoding.UTF8.GetString(message)));

            Assert.Multiple(() => {

                Assert.That(EndsWith(signed, message),  Is.True, "the message is left as it is - a header in front of it");
                Assert.That(good.Result,                Is.EqualTo(DkimResult.Pass), good.Details);
                Assert.That(bad.Result,                 Is.EqualTo(DkimResult.Fail), "one octet changed");

                Assert.That(EndsWith(decoded, message), Is.False, "decoded as UTF-8, the message was another one");

            });

        }

        [Test]
        public async Task An_8bit_message_is_queued_signed_and_sent_as_its_octets()
        {

            var key       = Key.Make("example.org");
            var message   = Latin1Message();

            using var nextHop = new NextHop(Octets: true);

            var queue     = new MemoryQueue();
            var logger    = new QuietLogger();
            var client    = new SMTPOutboundClient(new SmtpOutboundConfig {
                                                       LocalHostname     = "relay.hermod.test",
                                                       SmartHost         = "127.0.0.1",
                                                       SmartHostPort     = nextHop.Port,
                                                       ConnectTimeoutMs  = 3_000,
                                                       ReadTimeoutMs     = 5_000,
                                                       WriteTimeoutMs    = 3_000
                                                   },
                                                   null,
                                                   new NoDNS(),
                                                   logger,
                                                   dkimSignerFor: domain => domain == "example.org" ? key.Signer : null);

            var processor = new QueueProcessor(queue,
                                               client,
                                               new BounceHandler(new SMTPServerConfig { Hostname = "relay.hermod.test" }, queue, logger),
                                               new QueueProcessorConfig { DomainCooldownSeconds = 0 },
                                               logger);

            var ids       = await new MailSender(queue, logger).SendAsync("anna@example.org", [ "you@next.example" ], message);
            var queued    = queue.Mails[ids[0]];

            await processor.StartAsync();

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10) && queued.Status is QueueItemStatus.Pending or QueueItemStatus.Processing)
                await Task.Delay(50);

            await Task.Delay(100);
            await processor.StopAsync();
            await processor.DisposeAsync();

            // What the next hop got - the dot-stuffing undone.
            var arrived   = Encoding.Latin1.GetBytes(String.Join("\r\n", nextHop.DataLines.Select(line => line.StartsWith("..") ? line[1..] : line)) + "\r\n");
            var verified  = await new DNSVerifier(new DkimDNS(key), logger).VerifyDkimAsync(arrived);

            Assert.Multiple(() => {

                Assert.That(queued.MessageOctets,     Is.EqualTo(message), "queued as the octets it is");
                Assert.That(queued.Status,            Is.EqualTo(QueueItemStatus.Delivered));
                Assert.That(nextHop.Commands.Single(command => command.StartsWith("MAIL")), Does.Contain("BODY=8BITMIME"));
                Assert.That(nextHop.DataLines,        Does.Contain("..Eine Zeile, die mit einem Punkt beginnt."), "a leading dot doubled (RFC 5321 §4.5.2)");

                Assert.That(EndsWith(arrived, message), Is.True,                     "the octets that were queued");
                Assert.That(verified.Result,          Is.EqualTo(DkimResult.Pass),  verified.Details);

            });

        }

        [Test]
        public async Task A_bounce_returns_the_octets_of_the_message()
        {

            var message = Encoding.Latin1.GetBytes("From: sender@client.example\r\n" +
                                                   "Subject: relay\r\n" +
                                                   "Content-Type: text/plain; charset=iso-8859-1\r\n" +
                                                   "\r\n" +
                                                   "Grüße aus Köln\r\n");

            using var nextHop = new NextHop(Refusing("gone", "550 5.1.1 No such user"), Octets: true);

            var (queue, original) = await RelayOctets(nextHop, message, "here@next.example", "gone@next.example");

            var bounce  = queue.Mails.Values.Single(mail => mail.EnvelopeFrom.Length == 0);
            var octets  = bounce.MessageOctets!;
            var at      = octets.AsSpan().IndexOf(message);

            Assert.Multiple(() => {

                Assert.That(original.Status,          Is.EqualTo(QueueItemStatus.Delivered));
                Assert.That(nextHop.DataLines,        Does.Contain("Grüße aus Köln"), "here@ got it as it is");

                Assert.That(bounce.MessageOctets,     Is.Not.Null,                    "the report is octets too");
                Assert.That(at,                       Is.GreaterThan(0),              "with the message as it was sent - not as U+FFFD");
                Assert.That(bounce.MessageContent,    Does.Contain("Final-Recipient: rfc822; gone@next.example"));
                Assert.That(bounce.MessageContent,    Does.Contain("Content-Type: message/rfc822\r\nContent-Transfer-Encoding: 8bit"));

            });

        }

    }

}
