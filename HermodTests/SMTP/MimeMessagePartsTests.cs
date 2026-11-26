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

using System.Text;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// What a part is when the reader does not know its subtype - a text (RFC 2046 §4.1.4),
    /// a message (§5.2.4) - and the message a message carries: message/rfc822 or
    /// message/global (RFC 6532 §3.7), 7bit, 8bit or binary (RFC 2045 §2.7 to §2.9). The
    /// findings E-26 to E-29 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class MimeMessagePartsTests
    {

        #region Helpers

        private static MimeMessage Parse(String Message)
            => MimeMessage.Parse(Encoding.UTF8.GetBytes(Message.Replace("\r\n", "\n").Replace("\n", "\r\n")));

        private static String Single(String ContentType, String Disposition = "")
            => $"From: author@example.org\nTo: recipient@example.net\nSubject: A text\nMIME-Version: 1.0\nContent-Type: {ContentType}\n{Disposition}\nThe text.\n";

        private static String Mixed(params (String Header, String Body)[] Parts)
            => "From: author@example.org\nTo: recipient@example.net\nSubject: Parts\nMIME-Version: 1.0\nContent-Type: multipart/mixed; boundary=\"b1\"\n\n" +
               String.Concat(Parts.Select(part => $"--b1\n{part.Header}\n\n{part.Body}\n")) +
               "--b1--\n";

        private static String Field(MimeEntity Entity, String Name)
        {
            var octets = Encoding.UTF8.GetString(Entity.ToBytes());
            var header = Regex.Replace(octets[..octets.IndexOf("\r\n\r\n", StringComparison.Ordinal)], "\r\n(?=[ \t])", "");
            return header.Split("\r\n").First(line => line.StartsWith(Name + ":", StringComparison.OrdinalIgnoreCase))[(Name.Length + 1)..].Trim();
        }

        #endregion


        #region E-26: a text of a subtype the reader does not know

        [TestCase("text/markdown; charset=utf-8",   TestName = "A text/markdown message is its text")]
        [TestCase("text/x-log; charset=us-ascii",   TestName = "A text/x-log message is its text")]
        [TestCase("text/enriched",                  TestName = "A text/enriched message without a charset is its text")]
        [TestCase("text/markdown; charset=iso-8859-1", TestName = "A text/markdown message in Latin-1 is its text")]
        public void A_text_of_an_unknown_subtype_is_the_body(String ContentType)
        {

            var message = Parse(Single(ContentType));

            Assert.Multiple(() => {
                Assert.That(message.TextBody,             Is.SameAs(message.Root));
                Assert.That(message.TextBody!.GetText(),  Is.EqualTo("The text.\r\n"));
                Assert.That(message.Attachments,          Is.Empty);
            });

        }

        [Test]
        public void A_text_of_an_unknown_subtype_first_in_a_multipart_mixed_is_the_body()
        {

            var message = Parse(Mixed(("Content-Type: text/markdown; charset=utf-8", "# Notes"),
                                      ("Content-Type: image/png\nContent-Transfer-Encoding: base64\nContent-Disposition: attachment; filename=\"a.png\"", "iVBORw0KGgo=")));

            Assert.Multiple(() => {
                Assert.That(message.TextBody,     Is.SameAs(message.Root.Children[0]));
                Assert.That(message.Attachments,  Is.EqualTo(new[] { message.Root.Children[1] }));
            });

        }

        [Test]
        public void A_text_of_an_unknown_subtype_beside_HTML_is_the_text()
        {

            var message = Parse("From: author@example.org\nSubject: Two forms\nMIME-Version: 1.0\nContent-Type: multipart/alternative; boundary=\"b1\"\n\n" +
                                "--b1\nContent-Type: text/markdown; charset=utf-8\n\n*Hello*\n" +
                                "--b1\nContent-Type: text/html; charset=utf-8\n\n<em>Hello</em>\n--b1--\n");

            Assert.Multiple(() => {
                Assert.That(message.TextBody,     Is.SameAs(message.Root.Children[0]));
                Assert.That(message.HtmlBody,     Is.SameAs(message.Root.Children[1]));
                Assert.That(message.Attachments,  Is.Empty);
            });

        }

        [Test]
        public void A_text_of_an_unknown_subtype_beside_text_plain_stays_an_attachment()
        {

            var message = Parse(Mixed(("Content-Type: text/plain; charset=us-ascii",                  "You are invited."),
                                      ("Content-Type: text/calendar; charset=utf-8; method=REQUEST",  "BEGIN:VCALENDAR\nEND:VCALENDAR")));

            Assert.Multiple(() => {
                Assert.That(message.TextBody,     Is.SameAs(message.Root.Children[0]));
                Assert.That(message.Attachments,  Is.EqualTo(new[] { message.Root.Children[1] }));
            });

        }

        [TestCase("text/markdown; charset=x-unheard-of",  "",                                              TestName = "A text of an unknown subtype in an unknown charset is an attachment")]
        [TestCase("text/markdown; charset=utf-8",         "Content-Disposition: attachment\n",             TestName = "A text of an unknown subtype marked attachment is an attachment")]
        [TestCase("text/markdown; name=\"notes.md\"",     "",                                              TestName = "A text of an unknown subtype with a file name is an attachment")]
        public void A_text_of_an_unknown_subtype_that_is_no_text_to_show_is_an_attachment(String ContentType, String Disposition)
        {

            var message = Parse(Single(ContentType, Disposition));

            Assert.Multiple(() => {
                Assert.That(message.TextBody,     Is.Null);
                Assert.That(message.Attachments,  Is.EqualTo(new[] { message.Root }));
            });

        }

        [Test]
        public void A_text_of_an_unknown_subtype_with_a_file_name_marked_inline_is_the_body()
        {

            var message = Parse(Single("text/markdown; charset=utf-8", "Content-Disposition: inline; filename=\"notes.md\"\n"));

            Assert.That(message.TextBody, Is.SameAs(message.Root));

        }

        [TestCase("unknown-8bit",   false, TestName = "unknown-8bit is no charset the reader knows")]
        [TestCase("us-ascii",       true,  TestName = "us-ascii is a charset the reader knows")]
        [TestCase("\"UTF-8\"",      true,  TestName = "\"UTF-8\" is a charset the reader knows")]
        [TestCase(null,             true,  TestName = "No charset is US-ASCII")]
        public void Which_charsets_the_reader_knows(String? Charset, Boolean Known)

            => Assert.That(MimeCharsets.IsKnown(Charset), Is.EqualTo(Known));

        #endregion

        #region E-27: a message of a subtype the reader does not know

        [TestCase("message/http",       "GET / HTTP/1.1\nHost: example.org", TestName = "A message/http part is an attachment")]
        [TestCase("message/sipfrag",    "SIP/2.0 200 OK",                    TestName = "A message/sipfrag part is an attachment")]
        [TestCase("message/x-example",  "Anything.",                         TestName = "A message/x-example part is an attachment")]
        [TestCase("message/partial; id=\"abc@example.org\"; number=1; total=2", "From: a@example.org\n\nPart one.", TestName = "A message/partial part is an attachment")]
        public void A_message_part_of_an_unknown_subtype_is_an_attachment(String ContentType, String Body)
        {

            var message = Parse(Mixed(("Content-Type: text/plain; charset=us-ascii", "See below."),
                                      ($"Content-Type: {ContentType}",              Body)));

            Assert.Multiple(() => {
                Assert.That(message.TextBody,     Is.SameAs(message.Root.Children[0]));
                Assert.That(message.Attachments,  Is.EqualTo(new[] { message.Root.Children[1] }));
            });

        }

        [TestCase("delivery-status",          "message/delivery-status",          "Reporting-MTA: dns; example.org\n\nFinal-Recipient: rfc822; a@example.net\nAction: failed\nStatus: 5.1.1", TestName = "The delivery status of a bounce is no attachment")]
        [TestCase("global-delivery-status",   "message/global-delivery-status",   "Reporting-MTA: dns; example.org\n\nFinal-Recipient: utf-8; a@example.net\nAction: failed\nStatus: 5.1.1", TestName = "The global delivery status of a bounce is no attachment")]
        [TestCase("disposition-notification", "message/disposition-notification", "Final-Recipient: rfc822; a@example.net\nDisposition: manual-action/MDN-sent-manually; displayed",       TestName = "The disposition of a read receipt is no attachment")]
        [TestCase("feedback-report",          "message/feedback-report",          "Feedback-Type: abuse\nVersion: 1",                                                                     TestName = "The feedback of an abuse report is no attachment")]
        public void The_report_parts_of_a_report_are_no_attachments(String ReportType, String ContentType, String Body)
        {

            var message = Parse("From: MAILER-DAEMON@example.org\nSubject: A report\nMIME-Version: 1.0\n" +
                                $"Content-Type: multipart/report; report-type={ReportType}; boundary=\"b1\"\n\n" +
                                "--b1\nContent-Type: text/plain; charset=us-ascii\n\nThe report.\n" +
                                $"--b1\nContent-Type: {ContentType}\n\n{Body}\n" +
                                "--b1\nContent-Type: text/rfc822-headers\n\nFrom: a@example.net\nSubject: Hello\n--b1--\n");

            Assert.Multiple(() => {
                Assert.That(message.TextBody,     Is.SameAs(message.Root.Children[0]));
                Assert.That(message.Attachments,  Is.Empty);
            });

        }

        #endregion

        #region E-28: message/global

        [TestCase("From: other@example.net\r\nSubject: Grüße aus Jena\r\n\r\nThe text.\r\n",                   TestName = "UTF-8 in the subject of an attached message makes it message/global")]
        [TestCase("From: Jürgen Müller <other@example.net>\r\nSubject: Hello\r\n\r\nThe text.\r\n",            TestName = "UTF-8 in the name of From makes it message/global")]
        [TestCase("From: jürgen@bücher.example\r\nSubject: Hello\r\n\r\nThe text.\r\n",                        TestName = "UTF-8 in the address of From makes it message/global")]
        [TestCase("From: other@example.net\r\nSubject: Hello\r\nMIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=\"x\"\r\n\r\n--x\r\nContent-Type: text/plain\r\n\r\nText.\r\n--x\r\nContent-Type: application/pdf\r\nContent-Disposition: attachment; filename=\"Rechnung März.pdf\"\r\nContent-Transfer-Encoding: base64\r\n\r\nJVBERg==\r\n--x--\r\n",
                                                                                                                 TestName = "UTF-8 in the header of a part makes it message/global")]
        public void An_attached_message_with_UTF_8_in_a_header_is_message_global(String Message)
        {

            var attached = new MimeAttachedMessage(Encoding.UTF8.GetBytes(Message));

            Assert.Multiple(() => {
                Assert.That(attached.IsGlobal,                        Is.True);
                Assert.That(Field(attached, "Content-Type"),           Is.EqualTo("message/global"));
                Assert.That(Field(attached, "Content-Transfer-Encoding"), Is.EqualTo("8bit"));
            });

        }

        [TestCase("From: other@example.net\r\nSubject: =?utf-8?q?Gr=C3=BC=C3=9Fe?=\r\n\r\nGrüße.\r\n",       TestName = "Encoded words and a UTF-8 body leave an attached message message/rfc822")]
        [TestCase("From: other@example.net\r\nSubject: Hello\r\n\r\nThe text.\r\n",                          TestName = "An attached message of US-ASCII is message/rfc822")]
        public void An_attached_message_with_an_ASCII_header_is_message_rfc822(String Message)
        {

            var attached = new MimeAttachedMessage(Encoding.UTF8.GetBytes(Message));

            Assert.Multiple(() => {
                Assert.That(attached.IsGlobal,               Is.False);
                Assert.That(Field(attached, "Content-Type"),  Is.EqualTo("message/rfc822"));
            });

        }

        [Test]
        public void A_message_global_is_read_back_as_the_message_it_carries()
        {

            var original = Encoding.UTF8.GetBytes("From: Jürgen <other@example.net>\r\nSubject: Grüße\r\n\r\nThe text.\r\n");
            var builder  = new MimeMessageBuilder {
                               From     = new MailboxAddress(null, "anna@example.test"),
                               Subject  = "Fwd: Grüße",
                               Body     = new MimeMultipart("mixed", [ new MimeTextPart("See below."), new MimeAttachedMessage(original) ])
                           };

            builder.To.Add(new MailboxAddress(null, "ben@example.test"));

            var mail     = MimeMessage.Parse(builder.ToBytes());
            var attached = mail.Root.Children[1];

            Assert.Multiple(() => {
                Assert.That(attached.ContentType.MimeType,          Is.EqualTo("message/global"));
                Assert.That(attached.GetContent(),                  Is.EqualTo(original), "octet for octet");
                Assert.That(attached.EmbeddedMessage?.Subject,      Is.EqualTo("Grüße"));
                Assert.That(mail.Attachments,                       Is.EqualTo(new[] { attached }));
            });

        }

        #endregion

        #region E-29: 7bit, 8bit, binary

        private static Byte[] WithBody(String Body)
            => Encoding.UTF8.GetBytes("From: other@example.net\r\nSubject: Hello\r\n\r\n" + Body + "\r\n");

        [TestCase(998,  "",        "7bit",   TestName = "A line of 998 US-ASCII octets is 7bit")]
        [TestCase(999,  "",        "binary", TestName = "A line of 999 US-ASCII octets is binary")]
        [TestCase(1000, "",        "binary", TestName = "A line of 1000 US-ASCII octets is binary")]
        [TestCase(10,   "Grüße ",  "8bit",   TestName = "A short line with octets above 127 is 8bit")]
        [TestCase(992,  "Grüße ",  "binary", TestName = "A line of 1000 octets, some above 127, is binary")]
        [TestCase(990,  "Grüße ",  "8bit",   TestName = "A line of 998 octets, some above 127, is 8bit")]
        public void The_encoding_of_an_attached_message_is_what_its_lines_are(Int32 Length, String Start, String Encoding)
        {

            var attached = new MimeAttachedMessage(WithBody(Start + new String('a', Length)));

            Assert.Multiple(() => {
                Assert.That(attached.TransferEncoding,                    Is.EqualTo(Encoding));
                Assert.That(Field(attached, "Content-Transfer-Encoding"), Is.EqualTo(Encoding));
                Assert.That(attached.IsSevenBit,                          Is.EqualTo(Encoding == "7bit"));
            });

        }

        [Test]
        public void A_NUL_makes_an_attached_message_binary()
        {

            Byte[] message  = [ .. Encoding.ASCII.GetBytes("From: other@example.net\r\nSubject: Hello\r\n\r\na"), 0, .. Encoding.ASCII.GetBytes("b\r\n") ];
            var    attached = new MimeAttachedMessage(message);

            Assert.That(Field(attached, "Content-Transfer-Encoding"), Is.EqualTo("binary"));

        }

        [Test]
        public void Bare_line_ends_are_written_as_CRLF_and_measured_so()
        {

            var attached = new MimeAttachedMessage(System.Text.Encoding.ASCII.GetBytes("From: other@example.net\nSubject: Hello\n\nOne line.\rAnother.\n"));

            Assert.That(Field(attached, "Content-Transfer-Encoding"), Is.EqualTo("7bit"));

        }

        #endregion

    }

}
