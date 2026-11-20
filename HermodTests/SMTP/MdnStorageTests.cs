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
            Assert.That(mdn.EnvelopeFrom, Is.Empty, "RFC 8098 §3: MAIL FROM:<> - the reporting recipient is the From: of the MDN");
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

            // Each MDN is from its recipient - in From: and Final-Recipient; the envelope's is <> (RFC 8098 §3).
            Assert.That(queue.Enqueued.Select(m => System.Text.RegularExpressions.Regex.Match(m.MessageContent, @"Final-Recipient: rfc822; (\S+)").Groups[1].Value),
                        Is.EquivalentTo(new[] { "bob@example.org", "carol@example.org" }));

        }


        [Test(Description = "RFC 8098 §3: \"The envelope sender address (i.e., SMTP \"MAIL FROM\") of the MDN MUST be null (<>)\"")]
        public async Task The_MDN_is_sent_from_the_null_reverse_path()
        {

            var queued = await Store(Raw("alice@example.com"), "alice@example.com", "bob@example.org", "carol@example.org");

            Assert.That(queued.Select(mdn => mdn.EnvelopeFrom), Is.EqualTo(new[] { "", "" }));

        }


        #region When an MDN may be sent automatically (RFC 8098 §2.1)

        /// <summary>
        /// A message from alice@example.com to bob@example.org, with the given
        /// Disposition-Notification-To (none for null) and more header fields.
        /// </summary>
        private static String Raw(String?  DispositionNotificationTo,
                                  String   MoreHeaders  = "",
                                  String   Body         = "Please read this.\r\n")

            => "From: Alice <alice@example.com>\r\n" +
               "To: Bob <bob@example.org>\r\n" +
               "Subject: Contract\r\n" +
               "Date: Fri, 20 Nov 2026 10:00:00 +0100\r\n" +
               "Message-ID: <contract@example.com>\r\n" +
               (DispositionNotificationTo is not null ? $"Disposition-Notification-To: {DispositionNotificationTo}\r\n" : "") +
               MoreHeaders +
               "\r\n" +
               Body;

        /// <summary>
        /// Store the message for the envelope; what was queued in answer.
        /// </summary>
        private static async Task<List<QueuedMail>> Store(String           Raw,
                                                          String           EnvelopeFrom  = "alice@example.com",
                                                          params String[]  EnvelopeTo)
        {

            var queue   = new CapturingQueue();
            var storage = new MdnGeneratingMailStorage(new RecordingStorage(), queue, new NullSmtpLogger());

            await storage.StoreAsync(new EMailMessage { RawMessage = Raw },
                                     EnvelopeFrom,
                                     EnvelopeTo.Length > 0 ? EnvelopeTo : [ "bob@example.org" ]);

            return queue.Enqueued;

        }


        [TestCase("alice@example.com",  "victim@elsewhere.example",                     Description = "a third party")]
        [TestCase("",                   "alice@example.com",                            Description = "the null reverse-path")]
        [TestCase("<>",                 "alice@example.com",                            Description = "the null reverse-path in brackets")]
        [TestCase("alice@example.com",  "Alice@example.com",                            Description = "the local-part in another case")]
        [TestCase("alice@example.com",  "alice@example.com, victim@elsewhere.example",  Description = "two distinct addresses")]
        [Test(Description = "RFC 8098 §2.1: \"MDNs MUST NOT be sent automatically if the address in the Disposition-Notification-To header field differs from the address in the Return-Path header field\" - \"case sensitive for the local-part\" - and \"Confirmation from the user MUST be obtained (or no MDN sent) if there is no Return-Path ... or if there is more than one distinct address\"")]
        public async Task No_MDN_is_sent_automatically_to_an_address_the_reverse_path_does_not_vouch_for(String EnvelopeFrom, String DispositionNotificationTo)
        {

            var queued = await Store(Raw(DispositionNotificationTo), EnvelopeFrom);

            Assert.That(queued.Select(mdn => $"MAIL FROM:<{mdn.EnvelopeFrom}> to {String.Join(", ", mdn.EnvelopeTo)}"), Is.Empty);

        }


        [TestCase("Alice <alice@example.com>",                       Description = "with a display name")]
        [TestCase("alice@EXAMPLE.COM",                               Description = "the domain in capitals")]
        [TestCase("alice@example.com, Alice <alice@Example.com>",    Description = "the same address twice")]
        [Test(Description = "RFC 8098 §2.1: \"The comparison of the addresses is done using only the addr-spec ... excluding any angle brackets, phrase\", \"case insensitive for the domain part\" - the reverse-path's own address is answered")]
        public async Task The_reverse_paths_own_address_in_another_spelling_is_answered(String DispositionNotificationTo)
        {

            var queued = await Store(Raw(DispositionNotificationTo), "alice@example.com");

            Assert.That(queued,                           Has.Count.EqualTo(1));
            Assert.That(queued.Single().EnvelopeTo,       Has.Length.EqualTo(1), "one MDN, once to the address");
            Assert.That(queued.Single().EnvelopeTo[0],    Is.EqualTo("alice@example.com").IgnoreCase);

        }


        [TestCase("multipart/report; report-type=disposition-notification; boundary=\"mdn\"",           Description = "as Hermod writes it")]
        [TestCase("multipart/report;\r\n report-type=\"Disposition-Notification\";\r\n boundary=\"mdn\"", Description = "folded, quoted, in capitals")]
        [Test(Description = "RFC 8098 §2.1: \"An MDN MUST NOT be generated in response to an MDN\" - not even to one that, against §3, asks for one itself")]
        public async Task No_MDN_is_sent_in_answer_to_an_MDN(String ContentType)
        {

            var queued = await Store(Raw("alice@example.com",
                                         MoreHeaders:  "MIME-Version: 1.0\r\n" +
                                                       $"Content-Type: {ContentType}\r\n",
                                         Body:         "--mdn\r\n" +
                                                       "Content-Type: text/plain\r\n\r\n" +
                                                       "Your message was displayed.\r\n" +
                                                       "--mdn\r\n" +
                                                       "Content-Type: message/disposition-notification\r\n\r\n" +
                                                       "Reporting-UA: example.com; Foomail 97.1\r\n" +
                                                       "Final-Recipient: rfc822; alice@example.com\r\n" +
                                                       "Original-Message-ID: <first@example.org>\r\n" +
                                                       "Disposition: manual-action/MDN-sent-manually; displayed\r\n" +
                                                       "\r\n--mdn--\r\n"));

            Assert.That(queued, Is.Empty);

        }


        [Test(Description = "RFC 8098 §2.1 forbids answering an MDN, not any multipart/report - a delivery status notification that asks for a receipt is answered")]
        public async Task A_report_of_another_type_is_answered()
        {

            var queued = await Store(Raw("alice@example.com",
                                         MoreHeaders:  "MIME-Version: 1.0\r\n" +
                                                       "Content-Type: multipart/report; report-type=delivery-status; boundary=\"dsn\"\r\n",
                                         Body:         "--dsn\r\n" +
                                                       "Content-Type: text/plain\r\n\r\n" +
                                                       "Delivered.\r\n" +
                                                       "--dsn--\r\n"));

            Assert.That(queued, Has.Count.EqualTo(1));

        }

        #endregion

        #region Original-Recipient (RFC 8098 §3.2.3)

        [Test(Description = "RFC 8098 §3.2.3: \"If there is an Original-Recipient header field in the message ... then the Original-Recipient field MUST be included\" - the one the delivering server wrote at the top (§2.3) from ORCPT")]
        public async Task Original_Recipient_is_taken_from_the_message()
        {

            var queued = await Store("Original-Recipient: rfc822;team@example.org\r\n" + Raw("alice@example.com"));

            Assert.That(queued.Single().MessageContent, Does.Contain("Original-Recipient: rfc822;team@example.org\r\nFinal-Recipient: rfc822; bob@example.org\r\n"));

        }


        [Test(Description = "RFC 8098 §3.2.3: \"Otherwise, the Original-Recipient field MUST NOT be included\"")]
        public async Task Without_one_there_is_no_Original_Recipient_field()
        {

            var queued = await Store(Raw("alice@example.com"));

            Assert.That(queued.Single().MessageContent, Does.Not.Contain("Original-Recipient"));

        }

        #endregion

        #region Hostile messages

        [TestCase(false, Description = "asking for no receipt")]
        [TestCase(true,  Description = "asking for one")]
        [Test(Description = "RFC 5322 §2.2.3: a field may be folded onto any number of lines - one folded onto 240000 (under 1 MB) is read in time, within 10 seconds, whether the message asks for a receipt or not")]
        public async Task A_field_folded_onto_240000_lines_does_not_hold_up_delivery(Boolean AskForReceipt)
        {

            var folded = "X-Filler: a" + String.Concat(Enumerable.Repeat("\r\n x", 240_000)) + "\r\n";

            var queued = await Task.Run(() => Store(Raw(AskForReceipt ? "alice@example.com" : null, MoreHeaders: folded))).
                                    WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(queued, Has.Count.EqualTo(AskForReceipt ? 1 : 0));

        }

        #endregion

    }

}
