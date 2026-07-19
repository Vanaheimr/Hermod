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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// Delivery Status Notification (RFC 3461) tests: the outbound MAIL FROM / RCPT TO command
    /// construction, the sender facade threading the request into the queue, and the success-DSN
    /// emission on delivery.
    /// </summary>
    [TestFixture]
    public class DsnTests
    {

        private sealed class NullSmtpLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private static readonly ILogger Logger = new NullSmtpLogger();


        // The QueuedMail constructor is internal; build one the supported way (through MailSender)
        // and capture what it enqueued.
        private static async Task<QueuedMail> QueueOneAsync(DsnNotify notify)
        {

            var queue  = new CapturingQueue();
            var sender = new MailSender(queue, Logger);

            var mail = new TextEMailBuilder { Text = "body" };
            mail.From    = (EMailAddress) SimpleEMailAddress.Parse("me@example.com");
            mail.To      = (EMailAddress) SimpleEMailAddress.Parse("you@example.org");
            mail.Subject = "hi";

            await sender.SendAsync(new EMailEnvelop((EMail) mail) { Dsn = new DsnParameters(notify) });

            return queue.Enqueued[0];

        }


        #region A minimal capturing mail queue

        private sealed class CapturingQueue : IMailQueue
        {
            public readonly List<QueuedMail> Enqueued = [];

            public Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default)
            {
                Enqueued.Add(mail);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<QueuedMail>> GetPendingAsync(int maxItems = 50, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<QueuedMail>>(Enqueued);
            public Task<QueuedMail?> GetByIdAsync(string id, CancellationToken ct = default)
                => Task.FromResult(Enqueued.FirstOrDefault(m => m.Id == id));
            public Task UpdateAsync(QueuedMail mail, CancellationToken ct = default) => Task.CompletedTask;
            public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
            public Task<int> GetQueueLengthAsync(CancellationToken ct = default) => Task.FromResult(Enqueued.Count);
            public Task<IReadOnlyList<QueuedMail>> GetFailedAsync(int maxItems = 100, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public ChannelReader<QueuedMail> NewMailReader { get; } = Channel.CreateUnbounded<QueuedMail>().Reader;
            public void SignalRetryCheck() { }
            public ChannelReader<bool> RetryCheckReader { get; } = Channel.CreateUnbounded<bool>().Reader;
        }

        #endregion


        #region DsnCommands — MAIL FROM / RCPT TO construction (RFC 3461)

        [Test]
        public void No_DSN_params_when_remote_does_not_support_dsn()
        {
            var dsn = new DsnParameters(DsnNotify.Success | DsnNotify.Failure);
            Assert.That(DsnCommands.MailFrom("me@example.com", dsn, remoteSupportsDsn: false), Is.EqualTo("MAIL FROM:<me@example.com>"));
            Assert.That(DsnCommands.RcptTo("you@example.org", dsn, remoteSupportsDsn: false), Is.EqualTo("RCPT TO:<you@example.org>"));
        }

        [Test]
        public void No_DSN_params_when_nothing_requested()
        {
            Assert.That(DsnCommands.MailFrom("me@example.com", DsnParameters.None, remoteSupportsDsn: true), Is.EqualTo("MAIL FROM:<me@example.com>"));
            Assert.That(DsnCommands.RcptTo("you@example.org", DsnParameters.None, remoteSupportsDsn: true), Is.EqualTo("RCPT TO:<you@example.org>"));
        }

        [Test]
        public void MailFrom_emits_RET_and_ENVID_when_supported()
        {
            var dsn = new DsnParameters(DsnNotify.Success, DsnRet.Hdrs, EnvId: "abc123");
            Assert.That(DsnCommands.MailFrom("me@example.com", dsn, remoteSupportsDsn: true),
                        Is.EqualTo("MAIL FROM:<me@example.com> RET=HDRS ENVID=abc123"));
        }

        [Test]
        public void RcptTo_emits_NOTIFY_and_ORCPT_when_supported()
        {
            var dsn = new DsnParameters(DsnNotify.Success | DsnNotify.Failure | DsnNotify.Delay);
            Assert.That(DsnCommands.RcptTo("you@example.org", dsn, remoteSupportsDsn: true),
                        Is.EqualTo("RCPT TO:<you@example.org> NOTIFY=SUCCESS,FAILURE,DELAY ORCPT=rfc822;you@example.org"));
        }

        [Test]
        public void NOTIFY_formatting_covers_never_and_combinations()
        {
            Assert.That(DsnCommands.FormatNotify(DsnNotify.Never), Is.EqualTo("NEVER"));
            Assert.That(DsnCommands.FormatNotify(DsnNotify.Success), Is.EqualTo("SUCCESS"));
            Assert.That(DsnCommands.FormatNotify(DsnNotify.Failure | DsnNotify.Delay), Is.EqualTo("FAILURE,DELAY"));
        }

        #endregion


        #region Sender facade threads the DSN request into the queue

        [Test]
        public async Task MailSender_carries_the_DSN_request_onto_the_queued_mail()
        {

            var queue  = new CapturingQueue();
            var sender = new MailSender(queue, Logger);

            var mail = new TextEMailBuilder { Text = "hi" };
            mail.From    = (EMailAddress) SimpleEMailAddress.Parse("me@example.com");
            mail.To      = (EMailAddress) SimpleEMailAddress.Parse("you@example.org");
            mail.Subject = "test";

            await sender.SendAsync(new EMailEnvelop((EMail) mail) {
                                       Dsn = new DsnParameters(DsnNotify.Success | DsnNotify.Failure, DsnRet.Full, "env-42")
                                   });

            Assert.That(queue.Enqueued, Has.Count.EqualTo(1));
            var queued = queue.Enqueued[0];
            Assert.That(queued.Notify, Is.EqualTo(DsnNotify.Success | DsnNotify.Failure));
            Assert.That(queued.Ret,    Is.EqualTo(DsnRet.Full));
            Assert.That(queued.EnvId,  Is.EqualTo("env-42"));

        }

        [Test]
        public async Task Envelope_carries_all_transaction_parameters_onto_the_queued_mail()
        {

            var queue  = new CapturingQueue();
            var sender = new MailSender(queue, Logger);

            var mail = new TextEMailBuilder { Text = "hi" };
            mail.From    = (EMailAddress) SimpleEMailAddress.Parse("me@example.com");
            mail.To      = (EMailAddress) SimpleEMailAddress.Parse("you@example.org");
            mail.Subject = "test";

            // DSN, MT-PRIORITY and REQUIRETLS are envelope (transaction) parameters — they live on
            // the EMailEnvelop, not on the EMail and not on the send method.
            await sender.SendAsync(new EMailEnvelop((EMail) mail) {
                                       Dsn        = new DsnParameters(DsnNotify.Success, DsnRet.Hdrs, "env-77"),
                                       Priority   = 7,
                                       RequireTls = true
                                   });

            var queued = queue.Enqueued[0];
            Assert.That(queued.Notify,     Is.EqualTo(DsnNotify.Success));
            Assert.That(queued.Ret,        Is.EqualTo(DsnRet.Hdrs));
            Assert.That(queued.EnvId,      Is.EqualTo("env-77"));
            Assert.That(queued.Priority,   Is.EqualTo((SByte) 7));
            Assert.That(queued.RequireTls, Is.True);

        }

        [Test]
        public async Task MailSender_defaults_to_no_DSN_request()
        {

            var queue  = new CapturingQueue();
            var sender = new MailSender(queue, Logger);

            var mail = new TextEMailBuilder { Text = "hi" };
            mail.From    = (EMailAddress) SimpleEMailAddress.Parse("me@example.com");
            mail.To      = (EMailAddress) SimpleEMailAddress.Parse("you@example.org");
            mail.Subject = "test";

            await sender.SendAsync((EMail) mail);

            Assert.That(queue.Enqueued[0].Notify, Is.EqualTo(DsnNotify.Never));

        }

        #endregion


        #region Success DSN — relay ("relayed") and local acceptance ("delivered")

        private static SMTPServerConfig Config
            => new () { Hostname = "mx.example.com" };

        [Test]
        public async Task Relay_of_a_NOTIFY_SUCCESS_mail_queues_a_relayed_DSN_when_next_hop_has_no_dsn()
        {

            var queue   = new CapturingQueue();
            var handler = new BounceHandler(Config, queue, Logger);

            // remoteSupportsDsn = false → we own the notification.
            await handler.SendRelayNotificationAsync(await QueueOneAsync(DsnNotify.Success | DsnNotify.Failure), remoteSupportsDsn: false);

            Assert.That(queue.Enqueued, Has.Count.EqualTo(1), "a relayed DSN must be queued");
            var dsn = queue.Enqueued[0];
            Assert.That(dsn.EnvelopeFrom,   Is.EqualTo(""), "a report has a null sender (loop prevention)");
            Assert.That(dsn.EnvelopeTo,     Is.EqualTo(new[] { "me@example.com" }), "addressed back to the original sender");
            Assert.That(dsn.MessageContent, Does.Contain("multipart/report; report-type=delivery-status"));
            Assert.That(dsn.MessageContent, Does.Contain("Action: relayed"), "relaying is not final delivery (RFC 3461)");

        }

        [Test]
        public async Task No_relay_DSN_when_the_next_hop_supports_dsn()
        {

            var queue   = new CapturingQueue();
            var handler = new BounceHandler(Config, queue, Logger);

            // remoteSupportsDsn = true → the next hop owns the delivered notification; we must not duplicate.
            await handler.SendRelayNotificationAsync(await QueueOneAsync(DsnNotify.Success), remoteSupportsDsn: true);

            Assert.That(queue.Enqueued, Is.Empty, "the next hop issues the delivered DSN, so we don't");

        }

        [Test]
        public async Task No_relay_DSN_without_NOTIFY_SUCCESS()
        {

            var queue   = new CapturingQueue();
            var handler = new BounceHandler(Config, queue, Logger);

            await handler.SendRelayNotificationAsync(await QueueOneAsync(DsnNotify.Failure), remoteSupportsDsn: false);

            Assert.That(queue.Enqueued, Is.Empty, "no success notification unless SUCCESS was requested");

        }

        [Test]
        public async Task Local_delivery_queues_a_delivered_DSN_for_NOTIFY_SUCCESS_recipients()
        {

            var queue   = new CapturingQueue();
            var handler = new BounceHandler(Config, queue, Logger);

            var recipients = new[]
            {
                new RecipientDsn { Recipient = "you@example.com", Notify = DsnNotify.Success | DsnNotify.Failure },
                new RecipientDsn { Recipient = "quiet@example.com", Notify = DsnNotify.Failure }   // no success → no DSN
            };

            await handler.SendLocalDeliveryNotificationAsync(
                "sender@remote.org", recipients, "Subject: hi\r\n\r\nbody", envId: null, ret: DsnRet.Full);

            Assert.That(queue.Enqueued, Has.Count.EqualTo(1), "exactly one recipient asked for success");
            var dsn = queue.Enqueued[0];
            Assert.That(dsn.EnvelopeTo,     Is.EqualTo(new[] { "sender@remote.org" }));
            Assert.That(dsn.MessageContent, Does.Contain("Action: delivered"), "local mailbox delivery is final");
            Assert.That(dsn.MessageContent, Does.Contain("Final-Recipient: rfc822; you@example.com"));

        }

        #endregion

    }

}
