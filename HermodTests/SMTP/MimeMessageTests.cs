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
    /// The reading half of the mail model: MimeMessage as mail programs actually
    /// send mail - encoded words, parameters in RFC 2231, quoted-printable and
    /// base64, charsets declared, missing and wrong, multiparts nested, related,
    /// signed and never closed - and the old model's List-Id and Reply-To.
    /// </summary>
    [TestFixture]
    public class MimeMessageTests
    {

        #region Helpers

        private static MimeMessage Parse(String Text)
            => MimeMessage.Parse(Encoding.UTF8.GetBytes(Text.Replace("\r\n", "\n").Replace("\n", "\r\n")));

        private static MimeMessage ParseLatin1(String Text)
            => MimeMessage.Parse(Encoding.Latin1.GetBytes(Text.Replace("\r\n", "\n").Replace("\n", "\r\n")));

        #endregion


        #region RFC 2047 encoded words

        [TestCase("=?UTF-8?B?R3LDvMOfZQ==?=",                          "Grüße")]
        [TestCase("=?iso-8859-1?Q?Gr=FC=DFe?=",                         "Grüße")]
        [TestCase("=?UTF-8?Q?Sch=C3=B6ne_Gr=C3=BC=C3=9Fe?=",            "Schöne Grüße")]
        [TestCase("Re: =?UTF-8?Q?Gr=C3=BC=C3=9Fe?= aus Berlin",         "Re: Grüße aus Berlin")]
        [TestCase("=?UTF-8?Q?a?= =?UTF-8?Q?b?=",                        "ab")]
        [TestCase("=?UTF-8?Q?a?=\t  =?UTF-8?Q?_b?=",                    "a b")]
        [TestCase("=?UTF-8?Q?a?= x =?UTF-8?Q?b?=",                      "a x b")]
        [TestCase("=?UTF-8*de?Q?Gr=C3=BC=C3=9Fe?=",                     "Grüße")]
        [TestCase("=?windows-1252?Q?=93quoted=94?=",                    "“quoted”")]
        [TestCase("=?x-unknown?B?SGFsbG8=?=",                           "Hallo")]
        [TestCase("plain ASCII",                                        "plain ASCII")]
        [TestCase("=?UTF-8?X?broken?=",                                 "=?UTF-8?X?broken?=")]
        public void Encoded_words_are_decoded(String Encoded, String Decoded)
        {
            Assert.That(EncodedWords.Decode(Encoded), Is.EqualTo(Decoded));
        }

        [Test]
        public void A_character_split_across_two_encoded_words_is_whole_again()
        {
            // "ü" is C3 BC in UTF-8; here its two octets are in two words.
            Assert.That(EncodedWords.Decode("=?UTF-8?B?R3LD?= =?UTF-8?B?vMOfZQ==?="), Is.EqualTo("Grüße"));
        }

        #endregion

        #region Header fields

        [Test]
        public void Folded_header_fields_are_unfolded_and_repeated_ones_kept_in_order()
        {

            var message = Parse("Received: from a by b\n" +
                                "Received: from c by d\n" +
                                "Subject: a very\n" +
                                "  long subject\n" +
                                "X-Garbage-Without-Colon\n" +
                                "\n" +
                                "body\n");

            Assert.Multiple(() =>
            {
                Assert.That(message.Headers.GetAll("received"), Is.EqualTo(new[] { "from a by b", "from c by d" }));
                Assert.That(message.Subject,                    Is.EqualTo("a very  long subject"));
                Assert.That(message.Headers.Count,              Is.EqualTo(3), "a line without a colon is no field");
            });

        }

        [Test]
        public void Header_fields_in_8bit_are_UTF8_when_they_are_and_Windows1252_otherwise()
        {
            Assert.That(Parse      ("Subject: Grüße\n\nx\n").Subject, Is.EqualTo("Grüße"), "RFC 6532");
            Assert.That(ParseLatin1("Subject: Grüße\n\nx\n").Subject, Is.EqualTo("Grüße"), "8-bit Latin-1, as old mail programs send it");
        }

        #endregion

        #region Addresses

        [Test]
        public void Addresses_keep_their_names_decoded_and_commas_inside_quotes_separate_nothing()
        {

            var message = Parse("From: =?UTF-8?Q?J=C3=B6rg_M=C3=BCller?= <joerg@example.org>\n" +
                                "To: \"Müller, Anna\" <anna@example.org>, ben@example.org,\n" +
                                "    Team: carl@example.org, \"Dora \\\"D\\\"\" <dora@example.org>;\n" +
                                "Cc: old@example.org (Old Style), undisclosed-recipients:;\n" +
                                "Reply-To: <reply@example.org>\n" +
                                "\n" +
                                "x\n");

            Assert.Multiple(() =>
            {
                Assert.That(message.From.Single(),    Is.EqualTo(new MailboxAddress("Jörg Müller", "joerg@example.org")));
                Assert.That(message.To,               Is.EqualTo(new[] {
                                                                     new MailboxAddress("Müller, Anna", "anna@example.org"),
                                                                     new MailboxAddress(null,           "ben@example.org"),
                                                                     new MailboxAddress(null,           "carl@example.org"),
                                                                     new MailboxAddress("Dora \"D\"",   "dora@example.org")
                                                                 }));
                Assert.That(message.Cc,               Is.EqualTo(new[] { new MailboxAddress("Old Style", "old@example.org") }), "an empty group is nobody");
                Assert.That(message.ReplyTo.Single(), Is.EqualTo(new MailboxAddress(null, "reply@example.org")));
            });

        }

        #endregion

        #region Dates

        [TestCase("Tue, 14 Oct 2026 09:30:00 +0200",          "2026-10-14T07:30:00Z")]
        [TestCase("Tue, 14 Oct 2026 09:30:00 +0200 (CEST)",   "2026-10-14T07:30:00Z")]
        [TestCase("14 Oct 2026 09:30 GMT",                    "2026-10-14T09:30:00Z")]
        [TestCase("Wed, 14 Oct 26 09:30:00 EDT",              "2026-10-14T13:30:00Z")]
        [TestCase("Sun, 4 Oct 99 23:59:59 -0800",             "1999-10-05T07:59:59Z")]
        [TestCase("Wed Oct 14 09:30:00 2026",                 "2026-10-14T09:30:00Z")]
        [TestCase("Mon,  4 Oct 2026  9:05:00 +01:00",         "2026-10-04T08:05:00Z")]
        [TestCase("Thursday, 15 October 2026 17:12:03 Z",     "2026-10-15T17:12:03Z")]
        public void Dates_as_mail_programs_write_them(String Text, String Expected)
        {

            Assert.That(MimeDate.TryParse(Text, out var date), Is.True);
            Assert.That(date.UtcDateTime, Is.EqualTo(DateTime.Parse(Expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal)));

        }

        [TestCase("")]
        [TestCase("yesterday")]
        [TestCase("31 Feb 2026 10:00:00 +0000")]
        public void Dates_that_are_none_are_none(String Text)
        {
            Assert.That(MimeDate.TryParse(Text, out _), Is.False);
        }

        #endregion

        #region Content-Type and RFC 2231

        [Test]
        public void RFC2231_continuations_and_charsets_make_one_file_name()
        {

            var type = MimeContentType.Parse("application/pdf; name*0*=UTF-8''Gr%C3%BC%C3%9Fe; name*1=\" aus \"; name*2*=Berlin.pdf", MimeContentType.TextPlain);

            Assert.That(type.MimeType, Is.EqualTo("application/pdf"));
            Assert.That(type.Name,     Is.EqualTo("Grüße aus Berlin.pdf"));

        }

        [TestCase("attachment; filename*=UTF-8''Gr%C3%BC%C3%9Fe.pdf",                      "Grüße.pdf")]
        [TestCase("attachment; filename*=iso-8859-1'de'Gr%FC%DFe.pdf",                     "Grüße.pdf")]
        [TestCase("attachment; filename=\"=?UTF-8?B?R3LDvMOfZS5wZGY=?=\"",                  "Grüße.pdf")]
        [TestCase("attachment; filename=plain.pdf; filename*=UTF-8''better.pdf",            "better.pdf")]
        [TestCase("attachment; filename=\"semi;colon.pdf\" (a comment; here)",              "semi;colon.pdf")]
        [TestCase("ATTACHMENT; FILENAME=\"a\\\"quote.pdf\"",                                "a\"quote.pdf")]
        public void File_names_as_mail_programs_write_them(String Disposition, String FileName)
        {

            var disposition = MimeContentDisposition.Parse(Disposition)!;

            Assert.That(disposition.IsAttachment, Is.True);
            Assert.That(disposition.FileName,     Is.EqualTo(FileName));

        }

        [Test]
        public void A_content_type_that_cannot_be_read_is_text_plain()
        {
            Assert.That(MimeContentType.Parse("garbage", MimeContentType.TextPlain).MimeType, Is.EqualTo("text/plain"));
            Assert.That(MimeContentType.Parse("Text/HTML; Charset=UTF-8", MimeContentType.TextPlain).MimeType, Is.EqualTo("text/html"), "type and subtype do not care about case");
        }

        #endregion

        #region Transfer encodings and charsets

        [Test]
        public void Quoted_printable_with_soft_line_breaks_in_iso_8859_1()
        {

            var message = Parse("Content-Type: text/plain; charset=iso-8859-1\n" +
                                "Content-Transfer-Encoding: quoted-printable\n" +
                                "\n" +
                                "Sch=F6ne Gr=FC=DFe, das ist eine sehr lange Zeile, die umbrochen=\n" +
                                " wurde. 1+1=3D2 und ein loses = bleibt.\n");

            Assert.That(message.TextBody!.GetText(), Is.EqualTo("Schöne Grüße, das ist eine sehr lange Zeile, die umbrochen wurde. 1+1=2 und ein loses = bleibt.\r\n"));

        }

        [Test]
        public void Base64_with_line_breaks_and_stray_characters()
        {

            var message = Parse("Content-Type: text/plain; charset=utf-8\n" +
                                "Content-Transfer-Encoding: BASE64\n" +
                                "\n" +
                                "U2Now7Zu\n" +
                                "ZSBH\r\n" +
                                "csO8w5*9l\n" +
                                "\n");

            Assert.That(message.TextBody!.GetText(), Is.EqualTo("Schöne Grüße"));

        }

        [Test]
        public void Text_that_says_UTF8_and_is_not_is_read_as_Windows1252()
        {

            var message = ParseLatin1("Content-Type: text/plain; charset=utf-8\n" +
                                      "Content-Transfer-Encoding: 8bit\n" +
                                      "\n" +
                                      "Grüße\n");

            Assert.That(message.TextBody!.GetText(), Is.EqualTo("Grüße\r\n"));

        }

        #endregion

        #region Multiparts

        private const String Newsletter =
            "From: Shop <news@shop.example>\n" +
            "To: anna@example.org\n" +
            "Subject: Angebote\n" +
            "MIME-Version: 1.0\n" +
            "Content-Type: multipart/mixed; boundary=\"outer\"\n" +
            "\n" +
            "This is a preamble.\n" +
            "--outer\n" +
            "Content-Type: multipart/alternative; boundary=inner\n" +
            "\n" +
            "--inner\n" +
            "Content-Type: text/plain; charset=utf-8\n" +
            "\n" +
            "Nur Text\n" +
            "--inner\n" +
            "Content-Type: multipart/related; boundary=\"rel\"; type=\"text/html\"\n" +
            "\n" +
            "--rel\n" +
            "Content-Type: text/html; charset=utf-8\n" +
            "\n" +
            "<p>HTML mit <img src=\"cid:logo@shop\"></p>\n" +
            "--rel\n" +
            "Content-Type: image/png\n" +
            "Content-ID: <logo@shop>\n" +
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            "iVBORw0KGgo=\n" +
            "--rel--\n" +
            "--inner--\n" +
            "--outer\n" +
            "Content-Type: application/pdf; name=\"preise.pdf\"\n" +
            "Content-Disposition: attachment; filename=\"preise.pdf\"\n" +
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            "JVBERi0=\n" +
            "--outer--\n" +
            "This is an epilogue.\n";

        [Test]
        public void A_newsletter_with_alternative_related_and_an_attachment()
        {

            var message = Parse(Newsletter);

            Assert.Multiple(() =>
            {

                Assert.That(message.Root.PartId,                    Is.EqualTo(""), "the top of a multipart is a container");
                Assert.That(message.Root.Children.Count,            Is.EqualTo(2),  "preamble and epilogue are no parts");

                Assert.That(message.TextBody?.PartId,                Is.EqualTo("1.1"));
                Assert.That(message.TextBody?.GetText(),             Is.EqualTo("Nur Text"));
                Assert.That(message.HtmlBody?.PartId,                Is.EqualTo("1.2.1"), "the root of the related");
                Assert.That(message.HtmlBody?.GetText(),             Does.Contain("cid:logo@shop"));

                var logo = message.FindByContentId("<logo@shop>");
                Assert.That(logo?.PartId,                             Is.EqualTo("1.2.2"));
                Assert.That(logo?.IsInlineResource,                   Is.True);
                Assert.That(logo?.GetContent().Take(4),               Is.EqualTo(new Byte[] { 0x89, 0x50, 0x4E, 0x47 }));

                Assert.That(message.Attachments.Select(part => part.FileName), Is.EqualTo(new[] { "preise.pdf" }), "the logo is shown, not listed");
                Assert.That(message.FindPart("2")?.GetContent(),     Is.EqualTo(Encoding.ASCII.GetBytes("%PDF-")));

            });

        }

        [Test]
        public void Every_part_keeps_the_octets_it_arrived_as()
        {

            var raw     = Encoding.UTF8.GetBytes(Newsletter.Replace("\n", "\r\n"));
            var message = MimeMessage.Parse(raw);
            var text    = message.FindPart("1.1")!;

            // What a multipart/signed signature is made over: the part from its first
            // header line to the end of its body, without the line break before the
            // next delimiter (RFC 3156 §5).
            Assert.That(Encoding.UTF8.GetString(text.Raw.Span), Is.EqualTo("Content-Type: text/plain; charset=utf-8\r\n\r\nNur Text"));
            Assert.That(message.Root.Raw.Length,                 Is.EqualTo(raw.Length));

        }

        [Test]
        public void A_multipart_whose_end_never_comes_ends_at_the_end()
        {

            var message = Parse("Content-Type: multipart/mixed; boundary=b\n" +
                                "\n" +
                                "--b\n" +
                                "Content-Type: text/plain\n" +
                                "\n" +
                                "first\n" +
                                "--b\n" +
                                "Content-Type: text/plain; name=second.txt\n" +
                                "\n" +
                                "second\n");

            Assert.That(message.Root.Children.Count,        Is.EqualTo(2));
            Assert.That(message.TextBody?.GetText(),         Is.EqualTo("first"));
            Assert.That(message.Attachments.Single().GetText(), Is.EqualTo("second\r\n"));

        }

        [Test]
        public void A_line_that_only_begins_like_the_boundary_is_content()
        {

            var message = Parse("Content-Type: multipart/mixed; boundary=b\n" +
                                "\n" +
                                "--b\n" +
                                "\n" +
                                "--bx is not the boundary\n" +
                                "--b--\n");

            Assert.That(message.TextBody?.GetText(), Is.EqualTo("--bx is not the boundary"));

        }

        [Test]
        public void A_signed_message_shows_its_content_and_lists_no_signature()
        {

            var message = Parse("Content-Type: multipart/signed; micalg=pgp-sha512; protocol=\"application/pgp-signature\"; boundary=s\n" +
                                "\n" +
                                "--s\n" +
                                "Content-Type: text/plain; charset=utf-8\n" +
                                "\n" +
                                "signed text\n" +
                                "--s\n" +
                                "Content-Type: application/pgp-signature; name=signature.asc\n" +
                                "\n" +
                                "-----BEGIN PGP SIGNATURE-----\n" +
                                "-----END PGP SIGNATURE-----\n" +
                                "--s--\n");

            Assert.That(message.Pgp,                  Is.EqualTo("signed"));
            Assert.That(message.TextBody?.GetText(),  Is.EqualTo("signed text"));
            Assert.That(message.Attachments,          Is.Empty);

        }

        [Test]
        public void An_encrypted_message_has_no_body_to_show()
        {

            var message = Parse("Content-Type: multipart/encrypted; protocol=\"application/pgp-encrypted\"; boundary=e\n" +
                                "\n" +
                                "--e\n" +
                                "Content-Type: application/pgp-encrypted\n" +
                                "\n" +
                                "Version: 1\n" +
                                "--e\n" +
                                "Content-Type: application/octet-stream; name=encrypted.asc\n" +
                                "\n" +
                                "-----BEGIN PGP MESSAGE-----\n" +
                                "--e--\n");

            Assert.That(message.Pgp,          Is.EqualTo("encrypted"));
            Assert.That(message.TextBody,     Is.Null);
            Assert.That(message.HtmlBody,     Is.Null);
            Assert.That(message.Attachments,  Is.Empty, "the ciphertext is the message, not an attachment of it");

        }

        [Test]
        public void A_forwarded_message_is_an_attachment_and_a_message_of_its_own()
        {

            var message = Parse("Content-Type: multipart/mixed; boundary=f\n" +
                                "\n" +
                                "--f\n" +
                                "\n" +
                                "see below\n" +
                                "--f\n" +
                                "Content-Type: message/rfc822\n" +
                                "\n" +
                                "Subject: =?UTF-8?Q?Urspr=C3=BCnglich?=\n" +
                                "Content-Type: multipart/mixed; boundary=g\n" +
                                "\n" +
                                "--g\n" +
                                "\n" +
                                "inner text\n" +
                                "--g\n" +
                                "Content-Type: image/jpeg\n" +
                                "\n" +
                                "xyz\n" +
                                "--g--\n" +
                                "--f--\n");

            var forwarded = message.Attachments.Single();

            Assert.Multiple(() =>
            {
                Assert.That(forwarded.PartId,                             Is.EqualTo("2"));
                Assert.That(forwarded.EmbeddedMessage?.Subject,          Is.EqualTo("Ursprünglich"));
                Assert.That(forwarded.EmbeddedMessage?.TextBody?.PartId, Is.EqualTo("2.1"));
                Assert.That(message.FindPart("2.2")?.ContentType.MimeType, Is.EqualTo("image/jpeg"), "IMAP numbering into the forwarded message");
            });

        }

        [Test]
        public void A_message_that_is_not_a_multipart_is_part_1()
        {

            var message = Parse("Subject: x\n\nhello\n");

            Assert.That(message.Root.PartId,       Is.EqualTo("1"));
            Assert.That(message.TextBody,          Is.SameAs(message.Root));
            Assert.That(message.FindPart("1"),     Is.SameAs(message.Root));

        }

        [Test]
        public void Nesting_beyond_the_limit_is_kept_but_not_looked_into()
        {

            var text = new StringBuilder();

            for (var i = 0; i < MimeMessage.MaxDepth + 10; i++)
                text.Append($"Content-Type: multipart/mixed; boundary=b{i}\n\n--b{i}\n");

            text.Append("\nbottom\n");

            Assert.DoesNotThrow(() => Parse(text.ToString()));

        }

        [Test]
        public void Bare_LF_line_ends_are_read_like_CRLF()
        {

            var message = MimeMessage.Parse(Encoding.ASCII.GetBytes("Subject: lf\nContent-Type: multipart/alternative; boundary=x\n\n--x\nContent-Type: text/plain\n\nlf only\n--x--\n"));

            Assert.That(message.Subject,              Is.EqualTo("lf"));
            Assert.That(message.TextBody?.GetText(),  Is.EqualTo("lf only"));

        }

        #endregion

        #region Message ids and List-Id

        [Test]
        public void Message_ids_without_their_brackets()
        {

            var message = Parse("Message-ID: <a@x>\n" +
                                "In-Reply-To: <b@x>\n" +
                                "References: <c@x>\n <b@x>\n" +
                                "\n" +
                                "x\n");

            Assert.That(message.MessageId,   Is.EqualTo("a@x"));
            Assert.That(message.InReplyTo,   Is.EqualTo(new[] { "b@x" }));
            Assert.That(message.References,  Is.EqualTo(new[] { "c@x", "b@x" }));

        }

        [TestCase("Hermod developers <hermod-dev.lists.example.org>",  "Hermod developers",  "hermod-dev.lists.example.org")]
        [TestCase("\"Hermod, dev\" <hermod-dev.lists.example.org>",    "Hermod, dev",        "hermod-dev.lists.example.org")]
        [TestCase("<announce.example.org>",                             "",                   "announce.example.org")]
        [TestCase("=?UTF-8?Q?Gr=C3=BC=C3=9Fe?= <gruesse.example.org>",  "Grüße",              "gruesse.example.org")]
        [TestCase("bare.list.example.org",                              "",                   "bare.list.example.org")]
        public void List_ids_as_RFC2919_writes_them(String Header, String Name, String Id)
        {

            Assert.That(ListId.TryParse(Header, out var listId), Is.True);
            Assert.That(listId!.Name,            Is.EqualTo(Name));
            Assert.That(listId.Identification,   Is.EqualTo(Id));

        }

        [TestCase("anna@example.org")]
        [TestCase("<anna@example.org>")]
        [TestCase("")]
        public void A_mailbox_is_no_list_id(String Header)
        {
            Assert.That(ListId.TryParse(Header, out _), Is.False);
        }

        [Test]
        public void The_old_model_reads_Reply_To()
        {

            var email = EMail.Parse([
                            "From: Alice <alice@example.com>",
                            "To: Bob <bob@example.org>",
                            "Reply-To: replies@example.com",
                            "Subject: hi",
                            "",
                            "x"
                        ]);

            Assert.That(email.ReplyTo, Is.Not.Null);
            Assert.That(email.ReplyTo.ToString(), Does.Contain("replies@example.com"));

        }

        #endregion

    }

}
