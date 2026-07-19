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
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// Tests for the MdnGeneratingMailStorage decorator: an automatic read receipt (RFC 8098) is
    /// produced on local delivery of a message that requested one, and not otherwise.
    /// </summary>
    [TestFixture]
    public class MdnStorageTests
    {

        #region Test doubles

        private sealed class RecordingStorage : IMailStorage
        {
            public Int32 Calls;
            public Task<String> StoreAsync(EMailMessage m, String from, IEnumerable<String> to, CancellationToken ct = default)
            {
                Calls++;
                return Task.FromResult("/mailstore/stored.eml");
            }
        }

        private sealed class CapturingQueue : IMailQueue
        {
            public readonly List<QueuedMail> Enqueued = [];
            public Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default) { Enqueued.Add(mail); return Task.CompletedTask; }
            public Task<IReadOnlyList<QueuedMail>> GetPendingAsync(int maxItems = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>(Enqueued);
            public Task<QueuedMail?> GetByIdAsync(string id, CancellationToken ct = default) => Task.FromResult(Enqueued.FirstOrDefault(m => m.Id == id));
            public Task UpdateAsync(QueuedMail mail, CancellationToken ct = default) => Task.CompletedTask;
            public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
            public Task<int> GetQueueLengthAsync(CancellationToken ct = default) => Task.FromResult(Enqueued.Count);
            public Task<IReadOnlyList<QueuedMail>> GetFailedAsync(int maxItems = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public ChannelReader<QueuedMail> NewMailReader { get; } = Channel.CreateUnbounded<QueuedMail>().Reader;
            public void SignalRetryCheck() { }
            public ChannelReader<bool> RetryCheckReader { get; } = Channel.CreateUnbounded<bool>().Reader;
        }

        private sealed class NullSmtpLogger : ILogger { public void Log(LogLevel level, String message) { } }

        #endregion


        private static String RawMessage(Boolean requestReceipt)
        {
            var b = new TextEMailBuilder { Text = "Please read this." };
            b.From    = (EMailAddress) SimpleEMailAddress.Parse("alice@example.com");
            b.To      = (EMailAddress) SimpleEMailAddress.Parse("bob@example.org");
            b.Subject = "Contract";
            if (requestReceipt)
                b.DispositionNotificationTo = (EMailAddress) SimpleEMailAddress.Parse("alice@example.com");
            EMail mail = b;
            return String.Join("\r\n", mail.ToText());
        }


        [Test]
        public async Task Storing_a_message_that_requested_a_receipt_queues_an_MDN()
        {

            var inner   = new RecordingStorage();
            var queue   = new CapturingQueue();
            var storage = new MdnGeneratingMailStorage(inner, queue, new NullSmtpLogger());

            var path = await storage.StoreAsync(
                           new EMailMessage { RawMessage = RawMessage(requestReceipt: true) },
                           "alice@example.com",
                           ["bob@example.org"]);

            // The inner store still ran and owns the result.
            Assert.That(inner.Calls, Is.EqualTo(1));
            Assert.That(path,        Is.EqualTo("/mailstore/stored.eml"));

            // ...and an MDN was queued, from the local recipient back to the requester.
            Assert.That(queue.Enqueued, Has.Count.EqualTo(1), "one MDN per local recipient");
            var mdn = queue.Enqueued[0];
            Assert.That(mdn.EnvelopeFrom, Is.EqualTo("bob@example.org"), "the reporting recipient");
            Assert.That(mdn.EnvelopeTo,   Is.EqualTo(new[] { "alice@example.com" }), "back to Disposition-Notification-To");
            Assert.That(mdn.MessageContent, Does.Contain("report-type=disposition-notification"));
            Assert.That(mdn.MessageContent, Does.Contain("Disposition: automatic-action/MDN-sent-automatically; processed"),
                        "a delivery agent processed it automatically — not 'displayed'");
            Assert.That(mdn.MessageContent, Does.Contain("Auto-Submitted: auto-replied"));

        }

        [Test]
        public async Task Storing_a_message_without_a_receipt_request_queues_nothing()
        {

            var inner   = new RecordingStorage();
            var queue   = new CapturingQueue();
            var storage = new MdnGeneratingMailStorage(inner, queue, new NullSmtpLogger());

            await storage.StoreAsync(
                new EMailMessage { RawMessage = RawMessage(requestReceipt: false) },
                "alice@example.com",
                ["bob@example.org"]);

            Assert.That(inner.Calls,    Is.EqualTo(1), "the message is still stored");
            Assert.That(queue.Enqueued, Is.Empty,      "no MDN when none was requested");

        }

        [Test]
        public async Task One_MDN_is_generated_per_local_recipient()
        {

            var inner   = new RecordingStorage();
            var queue   = new CapturingQueue();
            var storage = new MdnGeneratingMailStorage(inner, queue, new NullSmtpLogger());

            await storage.StoreAsync(
                new EMailMessage { RawMessage = RawMessage(requestReceipt: true) },
                "alice@example.com",
                ["bob@example.org", "carol@example.org"]);

            Assert.That(queue.Enqueued.Select(m => m.EnvelopeFrom),
                        Is.EquivalentTo(new[] { "bob@example.org", "carol@example.org" }));

        }

    }

}
