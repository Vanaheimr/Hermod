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
using System.Diagnostics;
using System.Threading.Channels;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 5321 §6.1: a relay that accepted a message "MUST NOT lose" it. When the next hop accepts
    /// some recipients and refuses others, those accepted have the message, each refused one is
    /// bounced to the sender, and those refused for now are tried again.
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        private static Func<String, String?> Refusing(String Refused, String Reply)
            => line => line.StartsWith("RCPT") && line.Contains($"<{Refused}@") ? Reply : null;


        [Test]
        public async Task Each_recipient_answer_is_in_the_result()
        {

            using var nextHop = new NextHop(line => line.StartsWith("RCPT") && line.Contains("<gone@")  ? "550 5.1.1 No such user"
                                                  : line.StartsWith("RCPT") && line.Contains("<later@") ? "450 4.2.1 Mailbox busy"
                                                  : null);

            var result = await Send(nextHop, [ "here@next.example", "gone@next.example", "later@next.example" ]);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Success));
            Assert.That(result.Recipients.Select(outcome => (outcome.Recipient, outcome.Status)),
                        Is.EqualTo(new[] { ("here@next.example",  SendStatus.Success),
                                           ("gone@next.example",  SendStatus.PermFail),
                                           ("later@next.example", SendStatus.TempFail) }));

        }


        [Test]
        public async Task Nobody_accepted_but_one_only_for_now_is_a_temporary_failure()
        {

            using var nextHop = new NextHop(line => line.StartsWith("RCPT") && line.Contains("<gone@")  ? "550 5.1.1 No such user"
                                                  : line.StartsWith("RCPT") && line.Contains("<later@") ? "450 4.2.1 Mailbox busy"
                                                  : null);

            var result = await Send(nextHop, [ "gone@next.example", "later@next.example" ]);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.TempFail), "later@ is to be tried again");
            Assert.That(result.Recipients,   Has.Count.EqualTo(2));
            Assert.That(nextHop.Commands,    Has.None.EqualTo("DATA"));

        }


        #region Through the queue

        private sealed class MemoryQueue : IMailQueue
        {

            public readonly ConcurrentDictionary<String, QueuedMail> Mails = new();

            private readonly Channel<QueuedMail> newMail = Channel.CreateUnbounded<QueuedMail>();
            private readonly Channel<Boolean>    retry   = Channel.CreateUnbounded<Boolean>();

            public Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default)
            {
                Mails[mail.Id] = mail;
                newMail.Writer.TryWrite(mail);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<QueuedMail>> GetPendingAsync(Int32 maxItems = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public Task<QueuedMail?> GetByIdAsync(String id, CancellationToken ct = default)                          => Task.FromResult(Mails.TryGetValue(id, out var mail) ? mail : null);
            public Task UpdateAsync(QueuedMail mail, CancellationToken ct = default)                                   { Mails[mail.Id] = mail; return Task.CompletedTask; }
            public Task RemoveAsync(String id, CancellationToken ct = default)                                         { Mails.TryRemove(id, out _); return Task.CompletedTask; }
            public Task<Int32> GetQueueLengthAsync(CancellationToken ct = default)                                     => Task.FromResult(Mails.Count);
            public Task<IReadOnlyList<QueuedMail>> GetFailedAsync(Int32 maxItems = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public ChannelReader<QueuedMail> NewMailReader    => newMail.Reader;
            public void SignalRetryCheck()                    => retry.Writer.TryWrite(true);
            public ChannelReader<Boolean>    RetryCheckReader => retry.Reader;

        }


        /// <summary>
        /// Queue one message for the given recipients, let the QueueProcessor deliver it to the next
        /// hop, and return the queue and the message afterwards.
        /// </summary>
        private static async Task<(MemoryQueue Queue, QueuedMail Original)> Relay(NextHop NextHop, params String[] To)
        {

            var queue     = new MemoryQueue();
            var logger    = new QuietLogger();
            var processor = new QueueProcessor(queue,
                                               ClientFor(NextHop),
                                               new BounceHandler(new SMTPServerConfig { Hostname = "relay.hermod.test" }, queue, logger),
                                               new QueueProcessorConfig { DomainCooldownSeconds = 0 },
                                               logger);

            var original  = new QueuedMail {
                                Id              = $"relay-{Guid.NewGuid():N}",
                                EnvelopeFrom    = "sender@client.example",
                                EnvelopeTo      = To,
                                MessageContent  = "From: sender@client.example\r\nSubject: relay\r\n\r\nhello\r\n",
                                TargetDomain    = "next.example"
                            };

            await processor.StartAsync();
            await queue.EnqueueAsync(original);

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10) &&
                   original.Status is QueueItemStatus.Pending or QueueItemStatus.Processing)
                await Task.Delay(50);

            await Task.Delay(300);
            await processor.StopAsync();
            await processor.DisposeAsync();

            return (queue, original);

        }


        [Test]
        public async Task A_recipient_refused_by_the_next_hop_is_bounced_and_the_others_get_the_message()
        {

            using var nextHop = new NextHop(Refusing("gone", "550 5.1.1 No such user"));

            var (queue, original) = await Relay(nextHop, "here@next.example", "gone@next.example");

            var bounces = queue.Mails.Values.Where(mail => mail.EnvelopeFrom.Length == 0 && mail.EnvelopeTo.SequenceEqual([ "sender@client.example" ])).ToArray();

            Assert.That(original.Status,               Is.EqualTo(QueueItemStatus.Delivered));
            Assert.That(nextHop.DataLines,             Does.Contain("hello"));
            Assert.That(bounces,                       Has.Length.EqualTo(1));
            Assert.That(bounces[0].MessageContent,     Does.Contain("Final-Recipient: rfc822; gone@next.example")
                                                           .And.Not.Contain("Final-Recipient: rfc822; here@next.example"));
            Assert.That(bounces[0].MessageContent,     Does.Contain("5.1.1 No such user"));

        }


        [Test]
        public async Task A_recipient_refused_for_now_stays_in_the_queue_on_its_own()
        {

            using var nextHop = new NextHop(Refusing("later", "450 4.2.1 Mailbox busy"));

            var (queue, original) = await Relay(nextHop, "now@next.example", "later@next.example");

            var retry = queue.Mails.Values.SingleOrDefault(mail => mail.Id != original.Id && mail.EnvelopeFrom.Length > 0);

            Assert.That(original.Status,    Is.EqualTo(QueueItemStatus.Delivered));
            Assert.That(retry,              Is.Not.Null, "later@ is to be tried again");
            Assert.That(retry!.EnvelopeTo,  Is.EqualTo(new[] { "later@next.example" }), "and only later@: now@ has the message");
            Assert.That(retry.Status,       Is.EqualTo(QueueItemStatus.Deferred));
            Assert.That(retry.RetryCount,   Is.GreaterThanOrEqualTo(1));
            Assert.That(queue.Mails.Values, Has.None.Matches<QueuedMail>(mail => mail.EnvelopeFrom.Length == 0), "nobody is bounced");

        }

        #endregion

    }

}
