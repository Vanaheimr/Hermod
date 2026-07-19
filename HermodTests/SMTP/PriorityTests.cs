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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// Header-level message importance (RFC 2156 + de-facto X-Priority) build/parse tests.
    /// </summary>
    [TestFixture]
    public class PriorityTests
    {

        private static EMail Build(MailImportance importance)
        {
            var b = new TextEMailBuilder { Text = "hi", Importance = importance };
            b.From    = (EMailAddress) SimpleEMailAddress.Parse("me@example.com");
            b.To      = (EMailAddress) SimpleEMailAddress.Parse("you@example.org");
            b.Subject = "test";
            return b;
        }


        [Test]
        public void High_importance_emits_the_expected_headers()
        {
            var text = String.Join("\r\n", Build(MailImportance.High).ToText());
            Assert.That(text, Does.Contain("Importance: High"));
            Assert.That(text, Does.Contain("Priority: urgent"));
            Assert.That(text, Does.Contain("X-Priority: 1 (Highest)"));
            Assert.That(text, Does.Contain("X-MSMail-Priority: High"));
        }

        [Test]
        public void Low_importance_emits_the_expected_headers()
        {
            var text = String.Join("\r\n", Build(MailImportance.Low).ToText());
            Assert.That(text, Does.Contain("Importance: Low"));
            Assert.That(text, Does.Contain("X-Priority: 5 (Lowest)"));
        }

        [Test]
        public void Normal_importance_emits_no_priority_headers()
        {
            var text = String.Join("\r\n", Build(MailImportance.Normal).ToText());
            Assert.That(text, Does.Not.Contain("Importance:"));
            Assert.That(text, Does.Not.Contain("X-Priority:"));
        }


        [TestCase(MailImportance.High)]
        [TestCase(MailImportance.Normal)]
        [TestCase(MailImportance.Low)]
        public void Importance_round_trips_through_parse(MailImportance importance)
        {
            var parsed = EMail.Parse(Build(importance).ToText());
            Assert.That(parsed.Importance, Is.EqualTo(importance));
        }


        [Test]
        public void Importance_is_parsed_from_X_Priority_alone()
        {
            var high = EMail.Parse(new[]
            {
                "From: a@example.com", "To: b@example.org", "Subject: s",
                "X-Priority: 2", "Content-Type: text/plain; charset=utf-8", "", "body"
            });
            Assert.That(high.Importance, Is.EqualTo(MailImportance.High));

            var low = EMail.Parse(new[]
            {
                "From: a@example.com", "To: b@example.org", "Subject: s",
                "X-Priority: 4", "Content-Type: text/plain; charset=utf-8", "", "body"
            });
            Assert.That(low.Importance, Is.EqualTo(MailImportance.Low));
        }

        [Test]
        public void Importance_header_wins_over_X_Priority()
        {
            var mail = EMail.Parse(new[]
            {
                "From: a@example.com", "To: b@example.org", "Subject: s",
                "Importance: High", "X-Priority: 5 (Lowest)",
                "Content-Type: text/plain; charset=utf-8", "", "body"
            });
            Assert.That(mail.Importance, Is.EqualTo(MailImportance.High));
        }

        [Test]
        public void Unmarked_mail_is_normal_importance()
        {
            var mail = EMail.Parse(new[]
            {
                "From: a@example.com", "To: b@example.org", "Subject: s",
                "Content-Type: text/plain; charset=utf-8", "", "body"
            });
            Assert.That(mail.Importance, Is.EqualTo(MailImportance.Normal));
        }


        #region MT-PRIORITY (RFC 6710) transport priority

        private sealed class NullSmtpLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        [Test]
        public void MtPriority_clamps_and_parses()
        {
            Assert.That(MtPriority.Clamp(100),  Is.EqualTo((SByte) 9));
            Assert.That(MtPriority.Clamp(-100), Is.EqualTo((SByte) (-9)));
            Assert.That(MtPriority.Parse("4"),  Is.EqualTo((SByte) 4));
            Assert.That(MtPriority.Parse("x"),  Is.EqualTo((SByte) 0), "invalid → default");
        }

        [Test]
        public void MtPriority_parses_the_MAIL_FROM_parameter()
        {
            Assert.That(MtPriority.ParseFromMailParams("SIZE=100 MT-PRIORITY=6 BODY=8BITMIME"), Is.EqualTo((SByte) 6));
            Assert.That(MtPriority.ParseFromMailParams("SIZE=100"), Is.EqualTo((SByte) 0));
        }

        [Test]
        public void MtPriority_appends_to_MAIL_FROM_only_when_supported_and_non_default()
        {
            Assert.That(MtPriority.AppendMailFromParam("MAIL FROM:<a@b.c>", 5, remoteSupportsMtPriority: true),
                        Is.EqualTo("MAIL FROM:<a@b.c> MT-PRIORITY=5"));
            Assert.That(MtPriority.AppendMailFromParam("MAIL FROM:<a@b.c>", 5, remoteSupportsMtPriority: false),
                        Is.EqualTo("MAIL FROM:<a@b.c>"));
            Assert.That(MtPriority.AppendMailFromParam("MAIL FROM:<a@b.c>", 0, remoteSupportsMtPriority: true),
                        Is.EqualTo("MAIL FROM:<a@b.c>"));
        }

        [Test]
        public async Task Queue_returns_higher_priority_mail_first()
        {

            var dir   = Path.Combine(Path.GetTempPath(), "hermod-prio-" + UUIDv7.Generate().ToString("N"));
            var queue = new FileMailQueue(dir, new NullSmtpLogger());

            try
            {

                var sender = new MailSender(queue, new NullSmtpLogger());

                // Enqueue in a deliberately unsorted order.
                foreach (var p in new SByte[] { 0, 5, -3, 9, 2 })
                {
                    var mail = new TextEMailBuilder { Text = "b" };
                    mail.From    = (EMailAddress) SimpleEMailAddress.Parse("me@example.com");
                    mail.To      = (EMailAddress) SimpleEMailAddress.Parse($"p{p}@example.org");
                    mail.Subject = "s";
                    await sender.SendAsync(new EMailEnvelop((EMail) mail) { Priority = p });
                }

                var pending = await queue.GetPendingAsync(50);

                Assert.That(pending.Select(m => m.Priority).ToArray(),
                            Is.EqualTo(new SByte[] { 9, 5, 2, 0, -3 }),
                            "pending mail must be ordered by MT-PRIORITY, highest first");

            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }

        }

        #endregion

    }

}
