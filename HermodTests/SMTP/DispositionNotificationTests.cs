/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// Read receipts (RFC 8098): who asks for one, whether it would go somewhere
    /// else than the mail came from, and the one a reader sends - a
    /// multipart/report a program can read, about the message it names.
    /// </summary>
    [TestFixture]
    public class DispositionNotificationTests
    {

        private static MimeMessage Message(String DispositionNotificationTo, String From = "Bernd <bernd@example.net>")

            => MimeMessage.Parse(Encoding.UTF8.GetBytes($"From: {From}\r\n" +
                                                        "To: anna@example.test\r\n" +
                                                        "Subject: Grüße\r\n" +
                                                        "Message-ID: <original@example.net>\r\n" +
                                                        (DispositionNotificationTo.Length > 0 ? $"Disposition-Notification-To: {DispositionNotificationTo}\r\n" : "") +
                                                        "\r\nHallo\r\n"));


        [Test]
        public void Who_asks_for_a_receipt_and_whether_it_goes_elsewhere()
        {

            var asked     = Message("Bernd <bernd@example.net>");
            var elsewhere = Message("tracker@collect.example");

            Assert.Multiple(() => {

                Assert.That(DispositionNotification.RequestedBy(Message("")),            Is.Null, "nobody asked");
                Assert.That(DispositionNotification.RequestedBy(asked)!.Address,         Is.EqualTo("bernd@example.net"));

                Assert.That(DispositionNotification.GoesElsewhere(asked,     DispositionNotification.RequestedBy(asked)!,     "bernd@example.net"), Is.False);
                Assert.That(DispositionNotification.GoesElsewhere(asked,     DispositionNotification.RequestedBy(asked)!,     "bounces@lists.example"), Is.True, "another reverse-path");
                Assert.That(DispositionNotification.GoesElsewhere(elsewhere, DispositionNotification.RequestedBy(elsewhere)!, "bernd@example.net"), Is.True, "another address than From:");

            });

        }


        [TestCase("Bernd@example.net",                           "bernd@example.net",  Description = "the local-part in another case")]
        [TestCase("bernd@example.net",                           null,                 Description = "no Return-Path")]
        [TestCase("bernd@example.net",                           "",                   Description = "the null reverse-path")]
        [TestCase("bernd@example.net",                           "<>",                 Description = "the null reverse-path in brackets")]
        [TestCase("bernd@example.net, tracker@collect.example",  "bernd@example.net",  Description = "two distinct addresses")]
        [Test(Description = "RFC 8098 §2.1: the local-part compared \"case sensitive\"; \"Confirmation from the user MUST be obtained (or no MDN sent) if there is no Return-Path header field in the message or if there is more than one distinct address in the Disposition-Notification-To header field\"")]
        public void A_request_the_RFC_wants_confirmed_goes_elsewhere(String DispositionNotificationTo, String? EnvelopeFrom)
        {

            var message = Message(DispositionNotificationTo);

            Assert.That(DispositionNotification.GoesElsewhere(message, DispositionNotification.RequestedBy(message)!, EnvelopeFrom), Is.True);

        }


        [TestCase("bernd@EXAMPLE.NET",                           "bernd@example.net",    Description = "the domain in capitals")]
        [TestCase("bernd@example.net",                           "<bernd@example.net>",  Description = "the reverse-path in brackets")]
        [TestCase("bernd@example.net, Bernd <bernd@example.net>", "bernd@example.net",   Description = "one address written twice")]
        [Test(Description = "RFC 8098 §2.1: \"The comparison of the addresses is done using only the addr-spec ... excluding any angle brackets, phrase\", \"case insensitive for the domain part\" - the same address: nothing to confirm")]
        public void The_reverse_paths_own_address_does_not_go_elsewhere(String DispositionNotificationTo, String EnvelopeFrom)
        {

            var message = Message(DispositionNotificationTo);

            Assert.That(DispositionNotification.GoesElsewhere(message, DispositionNotification.RequestedBy(message)!, EnvelopeFrom), Is.False);

        }


        [Test(Description = "RFC 8098 §3: \"The MDN MUST be addressed (in both the message header field and the transport envelope) to the address(es) from the Disposition-Notification-To header field\" - every one of them, once the reader confirmed")]
        public void A_confirmed_receipt_goes_to_every_address_that_asked()
        {

            var displayed = new DateTimeOffset(2026, 11, 20, 9, 30, 0, TimeSpan.FromHours(1));
            var receipt   = MimeMessage.Parse(DispositionNotification.Build(Message("bernd@example.net, Carla <carla@example.org>, Bernd <bernd@example.net>"),
                                                                            new MailboxAddress(null, "anna@example.test"),
                                                                            "EMailWebApp",
                                                                            displayed).ToBytes());

            Assert.That(receipt.To.Select(mailbox => mailbox.Address), Is.EqualTo(new[] { "bernd@example.net", "carla@example.org" }));

        }


        /// <summary>
        /// The message/disposition-notification of the receipt Build writes for the message.
        /// </summary>
        private static String Report(MimeMessage Message, String? OriginalRecipient = null)

            => Encoding.ASCII.GetString(MimeMessage.Parse(DispositionNotification.Build(Message,
                                                                                         new MailboxAddress(null, "anna@example.test"),
                                                                                         "EMailWebApp",
                                                                                         new DateTimeOffset(2026, 11, 20, 9, 30, 0, TimeSpan.FromHours(1)),
                                                                                         OriginalRecipient).ToBytes()).
                                                   Root.Children[1].GetContent());


        [Test(Description = "RFC 8098 §3.2.3: \"If there is an Original-Recipient header field in the message ... then the Original-Recipient field MUST be included\"")]
        public void A_confirmed_receipt_returns_the_Original_Recipient()
        {

            var message = MimeMessage.Parse(Encoding.UTF8.GetBytes("Original-Recipient: rfc822;team@example.test\r\n" +
                                                                   "From: Bernd <bernd@example.net>\r\n" +
                                                                   "To: team@example.test\r\n" +
                                                                   "Subject: Termin\r\n" +
                                                                   "Message-ID: <original@example.net>\r\n" +
                                                                   "Disposition-Notification-To: bernd@example.net\r\n" +
                                                                   "\r\nHallo\r\n"));

            Assert.Multiple(() => {
                Assert.That(Report(message),                                Does.Contain("Original-Recipient: rfc822;team@example.test\r\nFinal-Recipient: rfc822; anna@example.test\r\n"));
                Assert.That(Report(message, "rfc822;sales@example.test"),   Does.Contain("Original-Recipient: rfc822;sales@example.test\r\n"), "one it is given");
                Assert.That(Report(Message("bernd@example.net")),           Does.Not.Contain("Original-Recipient"), "§3.2.3: \"Otherwise ... MUST NOT be included\"");
            });

        }

        [Test]
        public void A_receipt_is_a_report_about_the_message_it_names()
        {

            var displayed = new DateTimeOffset(2026, 10, 16, 9, 30, 0, TimeSpan.FromHours(2));
            var octets    = DispositionNotification.Build(Message("Bernd <bernd@example.net>"), new MailboxAddress(null, "anna@example.test"), "EMailWebApp", displayed).ToBytes();
            var receipt   = MimeMessage.Parse(octets);
            var parts     = receipt.Root.Children;
            var report    = Encoding.ASCII.GetString(parts[1].GetContent());

            Assert.Multiple(() => {

                Assert.That(receipt.To.Single().Address,                         Is.EqualTo("bernd@example.net"));
                Assert.That(receipt.From.Single().Address,                       Is.EqualTo("anna@example.test"));
                Assert.That(receipt.Subject,                                     Is.EqualTo("Gelesen: Grüße"));
                Assert.That(receipt.InReplyTo,                                   Is.EqualTo(new[] { "original@example.net" }));

                Assert.That(receipt.Root.ContentType.MimeType,                   Is.EqualTo("multipart/report"));
                Assert.That(receipt.Root.ContentType.Parameters["report-type"],  Is.EqualTo("disposition-notification"));
                Assert.That(parts[0].ContentType.MimeType,                       Is.EqualTo("text/plain"));
                Assert.That(parts[1].ContentType.MimeType,                       Is.EqualTo("message/disposition-notification"));

                Assert.That(report, Does.Contain("Reporting-UA: example.test; EMailWebApp\r\n"));
                Assert.That(report, Does.Contain("Final-Recipient: rfc822; anna@example.test\r\n"));
                Assert.That(report, Does.Contain("Original-Message-ID: <original@example.net>\r\n"));
                Assert.That(report, Does.Contain("Disposition: manual-action/MDN-sent-manually; displayed\r\n"), "sent because the reader said so");

                Assert.That(octets.All(octet => octet < 0x80),                   Is.True, "7-bit");

            });

            Assert.That(() => DispositionNotification.Build(Message(""), new MailboxAddress(null, "anna@example.test"), "x", displayed), Throws.ArgumentException);

        }

    }

}
