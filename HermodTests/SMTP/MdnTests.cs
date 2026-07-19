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
    /// Message Disposition Notification (read receipt, RFC 8098) generation tests.
    /// </summary>
    [TestFixture]
    public class MdnTests
    {

        private static EMail Incoming(Boolean RequestReceipt)

            => EMail.Parse(
                   RequestReceipt
                       ? new[]
                         {
                             "From: Alice <alice@example.com>",
                             "To: Bob <bob@example.org>",
                             "Subject: Please confirm",
                             "Message-ID: <orig-123@example.com>",
                             "Disposition-Notification-To: Alice <alice@example.com>",
                             "Content-Type: text/plain; charset=utf-8",
                             "",
                             "Hello Bob, did you read this?"
                         }
                       : new[]
                         {
                             "From: Alice <alice@example.com>",
                             "To: Bob <bob@example.org>",
                             "Subject: No receipt please",
                             "Content-Type: text/plain; charset=utf-8",
                             "",
                             "Hello Bob."
                         }
               );

        private static EMailAddress Bob
            => new ("Bob", SimpleEMailAddress.Parse("bob@example.org"));


        [Test]
        public void No_read_receipt_requested_returns_null()
        {
            var incoming = Incoming(RequestReceipt: false);
            Assert.That(incoming.IsReadReceiptRequested,      Is.False);
            Assert.That(incoming.DispositionNotificationTo,   Is.Null);
            Assert.That(incoming.CreateReadReceipt(Bob),      Is.Null, "no MDN when none was requested");
        }

        [Test]
        public void Requested_read_receipt_is_detected()
        {
            var incoming = Incoming(RequestReceipt: true);
            Assert.That(incoming.IsReadReceiptRequested,    Is.True);
            Assert.That(incoming.DispositionNotificationTo, Does.Contain("alice@example.com"));
        }

        [Test]
        public void Generated_MDN_has_the_RFC_8098_structure()
        {

            var mdn = Incoming(RequestReceipt: true).CreateReadReceipt(Bob);
            Assert.That(mdn, Is.Not.Null);

            var text = String.Join("\r\n", mdn!.ToText());

            // Envelope: from the reader, to the requester.
            Assert.That(mdn.From?.Address.ToString(), Is.EqualTo("bob@example.org"));
            Assert.That(mdn.To.Any(),                 Is.True);
            Assert.That(text, Does.Contain("Auto-Submitted: auto-replied"), "MDNs must not trigger auto-replies");

            // multipart/report with the disposition-notification report type.
            Assert.That(text, Does.Contain("multipart/report"));
            Assert.That(text, Does.Contain("report-type=disposition-notification"));

            // The machine-readable part and its required fields.
            Assert.That(text, Does.Contain("message/disposition-notification"));
            Assert.That(text, Does.Contain("Final-Recipient: rfc822; bob@example.org"));
            Assert.That(text, Does.Contain("Original-Message-ID: <orig-123@example.com>"));
            Assert.That(text, Does.Contain("Disposition: automatic-action/MDN-sent-automatically; displayed"));

        }

        [Test]
        public void Generated_MDN_re_parses_into_the_report_parts()
        {

            var mdn    = Incoming(RequestReceipt: true).CreateReadReceipt(Bob)!;
            var parsed = EMail.Parse(mdn.ToText());

            // multipart/report → human-readable + disposition-notification + original headers.
            Assert.That(parsed.Body.NestedBodyparts.Count(), Is.EqualTo(3),
                        "multipart/report must re-parse into its three parts");

        }

        [Test]
        public void Disposition_can_be_manual_and_a_different_type()
        {

            var mdn = Incoming(RequestReceipt: true).CreateReadReceipt(
                          Bob,
                          new MessageDisposition(DispositionActionMode.ManualAction,
                                                 DispositionSendingMode.SentManually,
                                                 MessageDispositionType.Deleted));

            var text = String.Join("\r\n", mdn!.ToText());
            Assert.That(text, Does.Contain("Disposition: manual-action/MDN-sent-manually; deleted"));

        }

        [Test]
        public void Builder_can_request_a_read_receipt()
        {

            var b = new TextEMailBuilder { Text = "hi" };
            b.From                      = (EMailAddress) SimpleEMailAddress.Parse("alice@example.com");
            b.To                        = (EMailAddress) SimpleEMailAddress.Parse("bob@example.org");
            b.Subject                   = "Confirm";
            b.DispositionNotificationTo = (EMailAddress) SimpleEMailAddress.Parse("alice@example.com");

            EMail mail   = b;
            var   parsed = EMail.Parse(mail.ToText());

            Assert.That(parsed.IsReadReceiptRequested, Is.True);
            Assert.That(parsed.DispositionNotificationTo, Does.Contain("alice@example.com"));

        }

    }

}
