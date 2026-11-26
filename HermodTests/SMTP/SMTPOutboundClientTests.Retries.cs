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

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 5321 §4.5.4.1, the sending strategy: a destination that failed is not tried again at
    /// once, the retry interval is at least what it is set to, the parameters of the retry
    /// algorithm are the operator's, and the recipients of one domain get one copy.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        #region Setup

        /// <summary>
        /// A host that counts the connections made to it, greets each with the given line - or
        /// with nothing - and closes it.
        /// </summary>
        private sealed class CountingHost : IDisposable
        {

            private readonly TcpListener listener = new (System.Net.IPAddress.Loopback, 0);
            private          Int32       connections;

            public Int32  Connections
                => Volatile.Read(ref connections);

            public IPPort Port
                => IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);

            public CountingHost(String? Greeting)
            {

                listener.Start();

                _ = Task.Run(async () => {
                    while (true)
                    {

                        TcpClient client;

                        try
                        {
                            client = await listener.AcceptTcpClientAsync();
                        }
                        catch
                        {
                            return;
                        }

                        Interlocked.Increment(ref connections);

                        using (client)
                        {
                            try
                            {
                                if (Greeting is not null)
                                {
                                    await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(Greeting + "\r\n"));
                                    await Task.Delay(200);
                                }
                            }
                            catch
                            { }
                        }

                    }
                });

            }

            public void Dispose()
                => listener.Stop();

        }

        private static SMTPOutboundClient ClientForPort(IPPort Port)

            => new (new SmtpOutboundConfig {
                        LocalHostname     = "relay.hermod.test",
                        SmartHost         = "127.0.0.1",
                        SmartHostPort     = Port,
                        ConnectTimeoutMs  = 3_000,
                        ReadTimeoutMs     = 3_000,
                        WriteTimeoutMs    = 3_000
                    },
                    null,
                    new NoDNS(),
                    new QuietLogger());

        /// <summary>
        /// A QueueProcessor with the given configuration - no cooldown, no limit per domain, no delay
        /// notices unless it says otherwise - and its queue.
        /// </summary>
        private sealed class Relaying : IAsyncDisposable
        {

            public  readonly MemoryQueue     Queue = new();
            private readonly QueueProcessor  processor;

            public Relaying(SMTPOutboundClient Client, QueueProcessorConfig? Config = null)
            {
                var logger = new QuietLogger();
                processor  = new QueueProcessor(Queue,
                                                Client,
                                                new BounceHandler(new SMTPServerConfig { Hostname = "relay.hermod.test" }, Queue, logger),
                                                Config ?? Quick(),
                                                logger);
                processor.StartAsync().GetAwaiter().GetResult();
            }

            public static QueueProcessorConfig Quick()
                => new () { DomainCooldownSeconds = 0, MaxDeliveriesPerDomain = 1000, SendDelayNotifications = false };

            /// <summary>
            /// Queue a message for the given recipient at next.example, and wait until the relay has
            /// done with it.
            /// </summary>
            public async Task<QueuedMail> SendAsync(String To, DateTimeOffset? QueuedAt = null)
            {

                var mail = new QueuedMail {
                               Id              = $"retry-{Guid.NewGuid():N}",
                               EnvelopeFrom    = "sender@client.example",
                               EnvelopeTo      = [ To ],
                               MessageContent  = "From: sender@client.example\r\nSubject: retry\r\n\r\nhello\r\n",
                               TargetDomain    = "next.example",
                               QueuedAt        = QueuedAt ?? Timestamp.Now
                           };

                await Queue.EnqueueAsync(mail);

                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed < TimeSpan.FromSeconds(10) &&
                       mail.Status is QueueItemStatus.Pending or QueueItemStatus.Processing)
                    await Task.Delay(50);

                await Task.Delay(200);
                return mail;

            }

            public QueuedMail[] Bounces
                => [.. Queue.Mails.Values.Where(mail => mail.EnvelopeFrom.Length == 0)];

            public async ValueTask DisposeAsync()
            {
                await processor.StopAsync();
                await processor.DisposeAsync();
            }

        }

        #endregion


        #region A destination that failed

        [TestCase("421 4.3.2 Service not available",  TestName = "A next hop that greeted with 421 is not tried again at once for the next message")]
        [TestCase(null,                                TestName = "A next hop that closed the connection unasked is not tried again at once for the next message")]
        public async Task A_destination_that_failed_is_not_tried_again_at_once(String? Greeting)
        {

            using var       host   = new CountingHost(Greeting);
            await using var relay  = new Relaying(ClientForPort(host.Port));

            var first       = await relay.SendAsync("first@next.example");
            var connections = host.Connections;

            Assert.That(first.Status, Is.EqualTo(QueueItemStatus.Deferred));
            Assert.That(connections,  Is.GreaterThan(0));

            var second      = await relay.SendAsync("second@next.example");

            Assert.Multiple(() => {
                Assert.That(host.Connections,  Is.EqualTo(connections),                "not tried again");
                Assert.That(second.Status,     Is.EqualTo(QueueItemStatus.Deferred),   "waits");
                Assert.That(second.RetryCount, Is.Zero,                                "it was not tried");
                Assert.That(second.NextRetry,  Is.EqualTo(first.NextRetry),            "until the first is due");
            });

        }


        [Test]
        public async Task A_port_nobody_listens_on_is_not_tried_again_at_once()
        {

            // A port that was free a moment ago: connecting to it is refused.
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);
            listener.Stop();

            await using var relay = new Relaying(ClientForPort(port));

            var first  = await relay.SendAsync("first@next.example");
            var second = await relay.SendAsync("second@next.example");

            Assert.Multiple(() => {
                Assert.That(first.Status,      Is.EqualTo(QueueItemStatus.Deferred));
                Assert.That(first.RetryCount,  Is.EqualTo(1));
                Assert.That(second.Status,     Is.EqualTo(QueueItemStatus.Deferred));
                Assert.That(second.RetryCount, Is.Zero, "held back, not tried");
            });

        }


        [Test]
        public async Task Its_time_come_a_destination_that_failed_is_tried_again()
        {

            using var       host  = new CountingHost("421 4.3.2 Service not available");
            await using var relay = new Relaying(ClientForPort(host.Port), Relaying.Quick() with { RetryIntervals = [ TimeSpan.FromSeconds(1) ] });

            await relay.SendAsync("first@next.example");
            var connections = host.Connections;

            await Task.Delay(1_500);

            var second = await relay.SendAsync("second@next.example");

            Assert.Multiple(() => {
                Assert.That(host.Connections,  Is.GreaterThan(connections), "tried again");
                Assert.That(second.RetryCount, Is.EqualTo(1));
            });

        }


        [Test]
        public async Task A_recipient_refused_for_now_does_not_hold_back_the_next_message()
        {

            using var       nextHop = new NextHop(Refusing("busy", "450 4.2.1 Mailbox busy"));
            await using var relay   = new Relaying(ClientFor(nextHop));

            var first  = await relay.SendAsync("busy@next.example");
            var second = await relay.SendAsync("here@next.example");

            Assert.Multiple(() => {
                Assert.That(first.Status,  Is.EqualTo(QueueItemStatus.Deferred));
                Assert.That(second.Status, Is.EqualTo(QueueItemStatus.Delivered));
            });

        }


        [Test]
        public async Task A_message_held_back_past_its_give_up_time_is_bounced()
        {

            using var       host  = new CountingHost("421 4.3.2 Service not available");
            await using var relay = new Relaying(ClientForPort(host.Port), Relaying.Quick() with { GiveUpAfter = TimeSpan.FromHours(1) });

            var first       = await relay.SendAsync("first@next.example");
            var old         = await relay.SendAsync("old@next.example", Timestamp.Now - TimeSpan.FromHours(2));

            Assert.Multiple(() => {
                Assert.That(first.Status,                                         Is.EqualTo(QueueItemStatus.Deferred));
                Assert.That(old.Status,                                           Is.EqualTo(QueueItemStatus.Failed));
                Assert.That(old.RetryCount,                                       Is.Zero, "not tried - its bounce goes through the same host");
                Assert.That(relay.Bounces.Select(bounce => bounce.MessageContent), Has.One.Matches<String>(content => content.Contains("old@next.example")));
            });

        }

        #endregion

        #region The parameters of the retry algorithm

        [Test]
        public async Task With_no_time_left_a_message_refused_for_now_is_given_up_on()
        {

            using var       nextHop = new NextHop(Refusing("later", "451 4.3.0 Try again later"));
            await using var relay   = new Relaying(ClientFor(nextHop), Relaying.Quick() with { GiveUpAfter = TimeSpan.Zero });

            var mail = await relay.SendAsync("later@next.example");

            Assert.Multiple(() => {
                Assert.That(mail.Status,   Is.EqualTo(QueueItemStatus.Failed));
                Assert.That(relay.Bounces, Has.Length.EqualTo(1));
            });

        }


        [Test]
        public async Task With_one_attempt_allowed_a_message_refused_for_now_is_given_up_on()
        {

            using var       nextHop = new NextHop(Refusing("later", "451 4.3.0 Try again later"));
            await using var relay   = new Relaying(ClientFor(nextHop), Relaying.Quick() with { MaxRetries = 1 });

            var mail = await relay.SendAsync("later@next.example");

            Assert.That(mail.Status, Is.EqualTo(QueueItemStatus.Failed));

        }


        [Test]
        public async Task A_message_refused_for_now_is_tried_again_after_the_configured_interval()
        {

            using var       nextHop = new NextHop(Refusing("later", "451 4.3.0 Try again later"));
            await using var relay   = new Relaying(ClientFor(nextHop), Relaying.Quick() with { RetryIntervals = [ TimeSpan.FromHours(3) ] });

            var start = Timestamp.Now;
            var mail  = await relay.SendAsync("later@next.example");
            var end   = Timestamp.Now;

            Assert.Multiple(() => {
                Assert.That(mail.Status,    Is.EqualTo(QueueItemStatus.Deferred));
                Assert.That(mail.NextRetry, Is.InRange(start + TimeSpan.FromHours(3), end + TimeSpan.FromHours(3.3)));
            });

        }


        [Test]
        public void By_default_a_message_is_kept_for_five_days_and_twelve_attempts()
        {

            var config = new QueueProcessorConfig();

            Assert.Multiple(() => {
                Assert.That(config.GiveUpAfter,    Is.EqualTo(TimeSpan.FromDays(5)));
                Assert.That(config.MaxRetries,     Is.EqualTo(12));
                Assert.That(config.RetryIntervals, Is.EqualTo(RetryCalculator.DefaultRetryIntervals));
                Assert.That(config.RetryIntervals, Has.All.GreaterThanOrEqualTo(TimeSpan.FromMinutes(30)));
            });

        }


        [Test]
        public void No_retry_comes_sooner_than_its_interval_nor_much_later()
        {

            for (UInt16 retry = 1; retry <= 14; retry++)
            {

                var interval = RetryCalculator.DefaultRetryIntervals[Math.Min(retry - 1, RetryCalculator.DefaultRetryIntervals.Count - 1)];

                for (var i = 0; i < 200; i++)
                {

                    var before = Timestamp.Now;
                    var next   = RetryCalculator.GetNextRetryTime(retry);
                    var after  = Timestamp.Now;

                    Assert.That(next, Is.InRange(before + interval, after + interval * 1.1), $"retry {retry}");

                }

            }

        }


        [TestCase((UInt16) 1, 5)]
        [TestCase((UInt16) 2, 60)]
        [TestCase((UInt16) 9, 60)]
        public void The_intervals_given_are_used_and_the_last_one_after(UInt16 RetryCount, Int32 Minutes)
        {

            var before = Timestamp.Now;
            var next   = RetryCalculator.GetNextRetryTime(RetryCount, [ TimeSpan.FromMinutes(5), TimeSpan.FromHours(1) ]);

            Assert.That(next - before, Is.InRange(TimeSpan.FromMinutes(Minutes), TimeSpan.FromMinutes(Minutes * 1.1 + 1)));

        }


        [Test]
        public void No_intervals_are_the_default_ones()
        {

            var before = Timestamp.Now;
            var next   = RetryCalculator.GetNextRetryTime(1, []);

            Assert.That(next - before, Is.InRange(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(34)));

        }

        #endregion

        #region One copy for one domain

        [Test]
        public async Task MailSender_queues_one_copy_for_one_domain_however_it_is_written()
        {

            var queue = new MemoryQueue();

            var ids   = await new MailSender(queue, new QuietLogger()).SendAsync(
                            new EMailEnvelop(EMail.Parse([
                                "From: app@client.example",
                                "To: a@next.example, b@NEXT.example, c@bücher.test, d@xn--bcher-kva.test",
                                "Subject: copies",
                                "",
                                "hello"
                            ])));

            var copies = ids.Select(id => queue.Mails[id]).ToArray();

            Assert.Multiple(() => {
                Assert.That(copies.Select(copy => copy.TargetDomain), Is.EquivalentTo(new[] { "next.example", "xn--bcher-kva.test" }));
                Assert.That(copies.Single(copy => copy.TargetDomain == "next.example").EnvelopeTo,       Has.Length.EqualTo(2));
                Assert.That(copies.Single(copy => copy.TargetDomain == "xn--bcher-kva.test").EnvelopeTo, Has.Length.EqualTo(2));
            });

        }


        [TestCase("next.example",         "next.example")]
        [TestCase("NEXT.Example.",        "next.example")]
        [TestCase("bücher.test",          "xn--bcher-kva.test")]
        [TestCase("Bücher.TEST",          "xn--bcher-kva.test")]
        [TestCase("XN--BCHER-KVA.test",   "xn--bcher-kva.test")]
        [TestCase("[192.0.2.1]",          "[192.0.2.1]")]
        public void A_domain_has_one_key_however_it_is_written(String Domain, String Key)
        {
            Assert.That(Idna.Key(Domain), Is.EqualTo(Key));
        }

        #endregion

    }

}
