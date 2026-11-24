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
    /// The reports on a message that requires TLS (RFC 8689 §5): "If a REQUIRETLS message is
    /// bounced, the server MUST behave as if RET=HDRS was present", and "All non-delivery messages
    /// resulting from messages with the REQUIRETLS SMTP option ... MUST also specify the REQUIRETLS
    /// SMTP option". The findings D-14 and D-15 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class DsnRequireTlsTests
    {

        #region Setup

        private const String Body = "The body of this message stays home.";

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

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

        /// <summary>
        /// A message queued the supported way, through MailSender: with the RET given, and
        /// REQUIRETLS or not.
        /// </summary>
        private static async Task<QueuedMail> QueuedAsync(Boolean RequireTls, DsnRet? Ret, DsnNotify Notify = DsnNotify.Failure)
        {

            var queue  = new CapturingQueue();
            var sender = new MailSender(queue, new QuietLogger());

            var mail = new TextEMailBuilder { Text = Body };
            mail.From    = (EMailAddress) SimpleEMailAddress.Parse("me@example.com");
            mail.To      = (EMailAddress) SimpleEMailAddress.Parse("you@example.org");
            mail.Subject = "for TLS only";

            await sender.SendAsync(new EMailEnvelop((EMail) mail) {
                                       Dsn         = new DsnParameters(Notify, Ret),
                                       RequireTls  = RequireTls
                                   });

            return queue.Enqueued.Single();

        }

        private static async Task<QueuedMail> BounceOf(QueuedMail Mail)
        {

            var reports = new CapturingQueue();

            await new BounceHandler(new SMTPServerConfig { Hostname = "mx.example.com" }, reports, new QuietLogger()).
                      SendBounceAsync(Mail, SendResult.PermFail(550, "5.1.1 No such user"));

            return reports.Enqueued.Single();

        }

        #endregion


        [TestCase(DsnRet.Full)]
        [TestCase(null)]
        public async Task The_bounce_of_a_REQUIRETLS_message_returns_its_header_only(DsnRet? Ret)
        {

            var bounce = await BounceOf(await QueuedAsync(RequireTls: true, Ret));

            Assert.That(bounce.MessageContent, Does.Not.Contain(Body),                          "RFC 8689 §5: as if RET=HDRS");
            Assert.That(bounce.MessageContent, Does.Contain("Content-Type: text/rfc822-headers"));
            Assert.That(bounce.MessageContent, Does.Contain("for TLS only"),                   "the header is returned");

        }

        [Test]
        public async Task The_bounce_of_a_message_without_REQUIRETLS_returns_it_whole_with_RET_FULL()
        {

            var bounce = await BounceOf(await QueuedAsync(RequireTls: false, DsnRet.Full));

            Assert.That(bounce.MessageContent, Does.Contain(Body));
            Assert.That(bounce.RequireTls,     Is.False);

        }

        [Test]
        public async Task The_bounce_of_a_REQUIRETLS_message_requires_TLS()
        {

            var bounce = await BounceOf(await QueuedAsync(RequireTls: true, Ret: null));

            Assert.That(bounce.RequireTls,   Is.True, "RFC 8689 §5: \"MUST also specify the REQUIRETLS SMTP option\"");
            Assert.That(bounce.EnvelopeFrom, Is.EqualTo(""));

        }

        [Test]
        public async Task The_delay_notice_of_a_REQUIRETLS_message_requires_TLS()
        {

            var reports = new CapturingQueue();

            await new BounceHandler(new SMTPServerConfig { Hostname = "mx.example.com" }, reports, new QuietLogger()).
                      SendDelayNotificationAsync(await QueuedAsync(RequireTls: true, Ret: DsnRet.Full, Notify: DsnNotify.Delay | DsnNotify.Failure),
                                                 SendResult.TempFail(451, "4.4.1 try again later"));

            var notice = reports.Enqueued.Single();

            Assert.That(notice.RequireTls,     Is.True, "protected as the message is");
            Assert.That(notice.MessageContent, Does.Not.Contain(Body));

        }

    }

}
