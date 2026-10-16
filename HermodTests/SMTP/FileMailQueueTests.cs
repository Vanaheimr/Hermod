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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The file queue: entries written whole or not at all, what a crash leaves tidied,
    /// unreadable entries set aside and said so, delivered mail kept without its content
    /// and only for a while.
    /// </summary>
    [TestFixture]
    public class FileMailQueueTests
    {

        #region Test doubles

        private sealed class RecordingLogger : ILogger
        {
            public readonly List<(LogLevel Level, String Message)> Lines = [];
            public void Log(LogLevel level, String message) { lock (Lines) Lines.Add((level, message)); }
        }

        #endregion

        private String  dir      = "";
        private String  pending  => Path.Combine(dir, "queue", "pending");
        private String  failed   => Path.Combine(dir, "queue", "failed");
        private String  delivered => Path.Combine(dir, "queue", "delivered");
        private String  corrupt  => Path.Combine(dir, "queue", "corrupt");

        [SetUp]
        public void SetUp()
            => dir = Path.Combine(Path.GetTempPath(), "hermod-queue-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private static QueuedMail Mail(String Id = "m1", String Body = "Das geheime Angebot")

            => new () {
                   Id              = Id,
                   EnvelopeFrom    = "anna@example.com",
                   EnvelopeTo      = [ "ben@example.org", "blind@example.org" ],
                   MessageContent  = $"Subject: Angebot\r\n\r\n{Body}\r\n",
                   MessageOctets   = Encoding.UTF8.GetBytes($"Subject: Angebot\r\n\r\n{Body}\r\n"),
                   TargetDomain    = "example.org"
               };


        #region Atomic writes

        [Test]
        public async Task Entries_are_written_whole_and_leave_no_temporary_file()
        {

            using var queue = new FileMailQueue(dir, new RecordingLogger());

            var mail = Mail();
            await queue.EnqueueAsync(mail);

            mail.Status    = QueueItemStatus.Deferred;
            mail.NextRetry = Timestamp.Now.AddMinutes(5);
            await queue.UpdateAsync(mail);

            Assert.That(Directory.GetFiles(pending).Select(Path.GetFileName), Is.EqualTo(new[] { "m1.json" }), "only the entry itself, no temporary file beside it");

            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(pending, "m1.json")));
            Assert.That(json.RootElement.GetProperty("Status").GetString(), Is.EqualTo("Deferred"));

        }

        [Test]
        public void Temporary_files_a_crash_left_are_removed_when_the_queue_opens()
        {

            Directory.CreateDirectory(pending);
            Directory.CreateDirectory(delivered);
            File.WriteAllText(Path.Combine(pending,   ".m1.json.0123456789abcdef.tmp"), "{ \"Id\": \"m1\", \"Envelo");
            File.WriteAllText(Path.Combine(delivered, ".m2.json.0123456789abcdef.tmp"), "");

            using var queue = new FileMailQueue(dir, new RecordingLogger());

            Assert.That(Directory.GetFiles(pending),   Is.Empty);
            Assert.That(Directory.GetFiles(delivered), Is.Empty);
            Assert.That(queue.GetSetAside(),           Is.Empty, "a temporary file is no entry - nothing to set aside");

        }

        #endregion

        #region Unreadable entries

        [Test]
        public async Task An_unreadable_entry_is_set_aside_and_said_loudly_when_the_queue_opens()
        {

            using (var first = new FileMailQueue(dir, new RecordingLogger()))
                await first.EnqueueAsync(Mail("good"));

            var whole = File.ReadAllText(Path.Combine(pending, "good.json"));
            File.WriteAllText(Path.Combine(pending, "half.json"),  whole[..(whole.Length / 2)]);
            File.WriteAllText(Path.Combine(pending, "null.json"),  "null");
            File.WriteAllText(Path.Combine(pending, "empty.json"), "");

            var logger = new RecordingLogger();
            using var queue = new FileMailQueue(dir, logger);

            Assert.That(Directory.GetFiles(pending).Select(Path.GetFileName), Is.EqualTo(new[] { "good.json" }));
            Assert.That(queue.GetSetAside().Select(Path.GetFileName).ToArray(),
                        Has.Length.EqualTo(3).And.All.Contains("-pending-"),
                        "set aside, with where they came from in the name");
            Assert.That(logger.Lines.Count(line => line.Level == LogLevel.Error && line.Message.Contains("set aside")), Is.EqualTo(3),
                        "each one said as an error, not skipped without a word");

            Assert.That((await queue.GetPendingAsync()).Select(mail => mail.Id), Is.EqualTo(new[] { "good" }));
            Assert.That(queue.NewMailReader.TryRead(out var loaded) ? loaded.Id : null, Is.EqualTo("good"));

        }

        [Test]
        public async Task An_entry_broken_while_the_queue_runs_is_set_aside_once()
        {

            var logger = new RecordingLogger();
            using var queue = new FileMailQueue(dir, logger);

            await queue.EnqueueAsync(Mail("good"));
            File.WriteAllText(Path.Combine(pending, "broken.json"), "{ \"Id\": \"broken\", ");

            Assert.That((await queue.GetPendingAsync()).Select(mail => mail.Id), Is.EqualTo(new[] { "good" }));
            Assert.That((await queue.GetWaitingAsync()).Select(mail => mail.Id), Is.EqualTo(new[] { "good" }));
            Assert.That(await queue.GetByIdAsync("broken"), Is.Null);

            Assert.That(queue.GetSetAside(), Has.Count.EqualTo(1));
            Assert.That(logger.Lines.Count(line => line.Level == LogLevel.Error), Is.EqualTo(1), "said once - then it is out of the way");
            Assert.That(await queue.GetQueueLengthAsync(), Is.EqualTo(1));

        }

        [Test]
        public async Task An_unreadable_failed_entry_is_set_aside_too()
        {

            using var queue = new FileMailQueue(dir, new RecordingLogger());

            File.WriteAllText(Path.Combine(failed, "f.json"), "kein JSON");

            Assert.That(await queue.GetFailedAsync(), Is.Empty);
            Assert.That(queue.GetSetAside().Single(), Does.Contain("-failed-f.json"));

        }

        #endregion

        #region After a crash

        [Test]
        public async Task A_mail_that_was_being_delivered_when_the_process_ended_is_tried_again()
        {

            using (var first = new FileMailQueue(dir, new RecordingLogger()))
            {
                var mail = Mail();
                await first.EnqueueAsync(mail);
                mail.Status = QueueItemStatus.Processing;
                await first.UpdateAsync(mail);
            }

            var logger = new RecordingLogger();
            using var queue = new FileMailQueue(dir, logger);

            var again = await queue.GetPendingAsync();

            Assert.That(again.Select(mail => (mail.Id, mail.Status)), Is.EqualTo(new[] { ("m1", QueueItemStatus.Pending) }));
            Assert.That(queue.NewMailReader.TryRead(out var loaded) ? loaded.Id : null, Is.EqualTo("m1"));
            Assert.That(logger.Lines.Any(line => line.Level == LogLevel.Warning && line.Message.Contains("tried again")));

        }

        [Test]
        public async Task A_mail_moved_to_delivered_but_not_yet_deleted_is_not_delivered_twice()
        {

            using (var first = new FileMailQueue(dir, new RecordingLogger()))
            {
                var mail = Mail();
                await first.EnqueueAsync(mail);

                var copy = File.ReadAllText(Path.Combine(pending, "m1.json"));

                mail.Status      = QueueItemStatus.Delivered;
                mail.DeliveredAt = Timestamp.Now;
                await first.UpdateAsync(mail);

                // The crash came between writing the delivered entry and deleting the pending one.
                File.WriteAllText(Path.Combine(pending, "m1.json"), copy);
            }

            using var queue = new FileMailQueue(dir, new RecordingLogger());

            Assert.That(Directory.GetFiles(pending), Is.Empty);
            Assert.That(await queue.GetPendingAsync(), Is.Empty);
            Assert.That(queue.NewMailReader.TryRead(out _), Is.False);
            Assert.That((await queue.GetByIdAsync("m1"))?.Status, Is.EqualTo(QueueItemStatus.Delivered));

        }

        #endregion

        #region Delivered and failed

        [Test]
        public async Task A_delivered_mail_is_remembered_without_its_content()
        {

            using var queue = new FileMailQueue(dir, new RecordingLogger());

            var mail = Mail();
            await queue.EnqueueAsync(mail);

            mail.Status         = QueueItemStatus.Delivered;
            mail.DeliveredAt    = Timestamp.Now;
            mail.RemoteMx       = "mx.example.org";
            mail.RemoteResponse = "250 2.0.0 OK";
            await queue.UpdateAsync(mail);

            Assert.That(mail.MessageContent, Does.Contain("Das geheime Angebot"), "the caller's mail stays as it was - a DSN may still need it");

            var raw = File.ReadAllText(Path.Combine(delivered, "m1.json"));
            Assert.That(raw, Does.Not.Contain("geheime").And.Not.Contain("Angebot").And.Not.Contain("MessageOctets"));
            Assert.That(Directory.GetFiles(pending), Is.Empty);

            var remembered = await queue.GetByIdAsync("m1");
            Assert.That(remembered,                 Is.Not.Null);
            Assert.That(remembered!.Status,         Is.EqualTo(QueueItemStatus.Delivered));
            Assert.That(remembered.EnvelopeTo,      Is.EqualTo(new[] { "ben@example.org", "blind@example.org" }));
            Assert.That(remembered.RemoteMx,        Is.EqualTo("mx.example.org"));
            Assert.That(remembered.DeliveredAt,     Is.Not.Null);
            Assert.That(remembered.MessageContent,  Is.Empty);
            Assert.That(remembered.MessageOctets,   Is.Null);

        }

        [Test]
        public async Task A_failed_mail_is_kept_as_it_was()
        {

            using var queue = new FileMailQueue(dir, new RecordingLogger());

            var mail = Mail();
            await queue.EnqueueAsync(mail);

            mail.Status    = QueueItemStatus.Failed;
            mail.LastError = "550 5.1.1 unknown";
            await queue.UpdateAsync(mail);

            var kept = (await queue.GetFailedAsync()).Single();
            Assert.That(kept.MessageContent, Does.Contain("Das geheime Angebot"));
            Assert.That(kept.MessageOctets,  Is.Not.Null);

        }

        [Test]
        public async Task Delivered_and_failed_mail_is_forgotten_after_its_time()
        {

            using var queue = new FileMailQueue(dir, new RecordingLogger());

            Assert.That(queue.DeliveredRetention, Is.EqualTo(TimeSpan.FromDays(7)));
            Assert.That(queue.FailedRetention,    Is.EqualTo(TimeSpan.FromDays(30)));

            var sent = Mail("sent");
            var lost = Mail("lost");
            var wait = Mail("wait");
            await queue.EnqueueAsync(sent);
            await queue.EnqueueAsync(lost);
            await queue.EnqueueAsync(wait);

            sent.Status = QueueItemStatus.Delivered;
            lost.Status = QueueItemStatus.Failed;
            await queue.UpdateAsync(sent);
            await queue.UpdateAsync(lost);

            File.WriteAllText(Path.Combine(corrupt, "20260101T000000Z-pending-old.json"), "kaputt");
            File.SetLastWriteTimeUtc(Path.Combine(corrupt, "20260101T000000Z-pending-old.json"), DateTime.UtcNow.AddYears(-1));

            var now = Timestamp.Now;

            Assert.That(queue.Purge(now.AddDays(6)),  Is.EqualTo(0));
            Assert.That(queue.Purge(now.AddDays(8)),  Is.EqualTo(1));
            Assert.That(await queue.GetByIdAsync("sent"), Is.Null, "delivered: a week");
            Assert.That(await queue.GetByIdAsync("lost"), Is.Not.Null);

            Assert.That(queue.Purge(now.AddDays(31)), Is.EqualTo(1));
            Assert.That(await queue.GetByIdAsync("lost"), Is.Null, "failed: thirty days");

            Assert.That(await queue.GetByIdAsync("wait"), Is.Not.Null, "what still waits is never forgotten");
            Assert.That(queue.GetSetAside(),              Has.Count.EqualTo(1), "what was set aside waits for an administrator");

        }

        [Test]
        public async Task How_long_is_said_by_whoever_opens_the_queue_and_counted_when_it_opens()
        {

            using (var first = new FileMailQueue(dir, new RecordingLogger(), deliveredRetention: TimeSpan.FromDays(14)))
            {
                var mail = Mail();
                await first.EnqueueAsync(mail);
                mail.Status = QueueItemStatus.Delivered;
                await first.UpdateAsync(mail);
            }

            File.SetLastWriteTimeUtc(Path.Combine(delivered, "m1.json"), DateTime.UtcNow.AddDays(-10));

            using (var fortnight = new FileMailQueue(dir, new RecordingLogger(), deliveredRetention: TimeSpan.FromDays(14)))
                Assert.That(await fortnight.GetByIdAsync("m1"), Is.Not.Null, "ten days old, fourteen allowed");

            using var week = new FileMailQueue(dir, new RecordingLogger());
            Assert.That(await week.GetByIdAsync("m1"), Is.Null, "ten days old, seven allowed - gone when the queue opened");

        }

        #endregion

    }

}
