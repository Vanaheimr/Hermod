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
            Assert.That(text, Does.Contain("Disposition: manual-action/MDN-sent-manually; displayed"), "RFC 8098 §3.2.6.1: the default for MUAs");

        }


        [Test(Description = "RFC 8098 §3.2.3: the Original-Recipient field \"MUST be included\" \"if information about the original recipient is reliably available some other way\" - §5: the ORCPT of RFC 3461")]
        public void CreateReadReceipt_returns_an_Original_Recipient_it_is_given()
        {

            var text = String.Join("\r\n", Incoming(RequestReceipt: true).CreateReadReceipt(Bob, OriginalRecipient: "rfc822;team@example.org")!.ToText());

            Assert.That(text, Does.Contain("Original-Recipient: rfc822;team@example.org\r\nFinal-Recipient: rfc822; bob@example.org\r\n"));

        }


        [Test(Description = "RFC 8098 §3.2.6.1: manual-action - \"in order to preserve the user's privacy, this MUST be the default for MUAs\"; MDN-sent-manually - \"this MUST be the default for MUAs\"")]
        public void CreateReadReceipt_defaults_to_manual_action_and_MDN_sent_manually()
        {

            var text = String.Join("\r\n", Incoming(RequestReceipt: true).CreateReadReceipt(Bob)!.ToText());

            Assert.Multiple(() => {
                Assert.That(text, Does.Contain("Disposition: manual-action/MDN-sent-manually; displayed"));
                Assert.That(text, Does.Not.Contain("automatic"));
            });

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


        [Test(Description = "RFC 5322 §2.2.3: \"Unfolding is accomplished by simply removing any CRLF that is immediately followed by WSP\" - the white space stays")]
        public void A_folded_field_is_unfolded_with_its_white_space()
        {

            var mail = EMail.Parse([ "From: Alice <alice@example.com>",
                                     "Subject: Read",
                                     " receipt",
                                     "Comments: x first",
                                     "\tsecond",
                                     "",
                                     "Hello." ]);

            Assert.Multiple(() => {
                Assert.That(mail.Subject,                     Is.EqualTo("Read receipt"));
                Assert.That(mail.GetEMailHeader("Comments"),  Is.EqualTo("x first\tsecond"));
            });

        }


        /// <summary>
        /// The lines of the text/rfc822-headers part of an MDN.
        /// </summary>
        private static String[] ReturnedHeader(EMail Mdn)
            => [ .. Mdn.Body.NestedBodyparts.ElementAt(2).MailBody.Where(line => line.Length > 0) ];

        private static readonly String[] Traced = [ "Received: from mail.example.com by mx.example.org;",
                                                    "\tFri, 20 Nov 2026 10:00:02 +0100",
                                                    "Received: from client.example.com by mail.example.com; Fri, 20 Nov 2026 10:00:01 +0100",
                                                    "From: Alice <alice@example.com>",
                                                    "To: Bob <bob@example.org>",
                                                    "Subject: Please confirm",
                                                    "Comments: x first",
                                                    " second",
                                                    "Message-ID: <orig-123@example.com>",
                                                    "Disposition-Notification-To: Alice <alice@example.com>",
                                                    "",
                                                    "Hello Bob, did you read this?" ];


        [Test(Description = "RFC 6522 §4: \"The text/rfc822-headers body part SHOULD contain all the mail header fields from the message that caused the report\" - every Received field, \"for diagnostics based on the Received header fields\"")]
        public void The_returned_header_has_every_Received_field()
        {

            var returned = ReturnedHeader(EMail.Parse(Traced).CreateReadReceipt(Bob)!);

            Assert.That(returned.Where(line => line.StartsWith("Received:")), Has.Exactly(2).Items, String.Join("\n", returned));

        }


        [Test(Description = "RFC 6522 §4: all the header fields of the message - RFC 5322 §2.2.3: a folded field keeps the white space unfolding leaves, here even its line breaks")]
        public void The_returned_header_is_the_header_section_as_it_was_written()
        {

            var returned = ReturnedHeader(EMail.Parse(Traced).CreateReadReceipt(Bob)!);

            Assert.That(returned, Is.EqualTo(Traced.TakeWhile(line => line.Length > 0)));

        }


        [Test(Description = "RFC 5322 §2.2.3: a field may be folded onto any number of lines - one folded onto 240000 is parsed in time, within 10 seconds")]
        public async Task A_field_folded_onto_240000_lines_is_parsed_in_time()
        {

            var lines = new[] { "From: Alice <alice@example.com>", "X-Filler: a" }.
                            Concat(Enumerable.Repeat(" x", 240_000)).
                            Concat([ "", "Hello." ]).
                            ToArray();

            var mail  = await Task.Run(() => EMail.Parse(lines)).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(mail.GetEMailHeader("X-Filler"), Has.Length.EqualTo(1 + 2 * 240_000));

        }

    }

}
