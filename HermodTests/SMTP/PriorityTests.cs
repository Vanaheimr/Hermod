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
using System.Linq;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.Mail;

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

    }

}
