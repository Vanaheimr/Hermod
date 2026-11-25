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

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The parts an HTML mail refers to, and what a part is for: the Content-ID, a msg-id
    /// (RFC 2045 §7, RFC 5322 §3.6.4) a cid: URL names (RFC 2392), multipart/related and its
    /// type and start (RFC 2387), Content-Disposition (RFC 2183) - written and read. The
    /// findings E-17 to E-21 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class MimeRelatedPartsTests
    {

        #region Helpers

        private static readonly Byte[] Png = [ 0x89, 0x50, 0x4E, 0x47 ];

        private static MimeMessage Parse(String Text)
            => MimeMessage.Parse(Encoding.UTF8.GetBytes(Text.Replace("\r\n", "\n").Replace("\n", "\r\n")));

        private static MimeMessage Parse(MimeEntity Entity)
            => MimeMessage.Parse(Entity.ToBytes());

        private static String Related(String ImageId, String RootId, String Start) => $"""
            From: author@example.org
            Subject: A picture
            MIME-Version: 1.0
            Content-Type: multipart/related; type="text/html"; start="{Start}"; boundary="b1"

            --b1
            Content-Type: image/png
            Content-Transfer-Encoding: base64
            Content-ID: {ImageId}

            iVBORw0KGgo=
            --b1
            Content-Type: text/html; charset=us-ascii
            Content-ID: {RootId}

            <p><img src="cid:logo@example.org"></p>
            --b1--

            """;

        private static String Mixed(String SecondDisposition) => $"""
            From: author@example.org
            Subject: Two texts
            MIME-Version: 1.0
            Content-Type: multipart/mixed; boundary="b1"

            --b1
            Content-Type: text/plain; charset=us-ascii

            The text.
            --b1
            Content-Type: text/plain; charset=us-ascii
            Content-Disposition: {SecondDisposition}

            The second text.
            --b1--

            """;

        private static String Single(String Disposition) => $"""
            From: author@example.org
            Subject: Notes
            MIME-Version: 1.0
            Content-Type: text/plain; charset=us-ascii
            Content-Disposition: {Disposition}

            The notes.

            """;

        #endregion


        #region E-17: a Content-ID read as a msg-id

        [TestCase("<logo@example.org>",                     TestName = "Content-ID: a msg-id")]
        [TestCase("<logo@example.org> (the logo)",          TestName = "Content-ID: a comment after it")]
        [TestCase("(the logo) <logo@example.org>",          TestName = "Content-ID: a comment before it")]
        [TestCase(" (a (nested) one)\t<logo@example.org> ", TestName = "Content-ID: a nested comment, white space around it")]
        public void A_Content_ID_is_read_as_a_msg_id(String ContentId)
        {

            var message = Parse(Related(ContentId, "<root@example.org>", "<root@example.org>"));

            Assert.Multiple(() => {
                Assert.That(message.Root.Children[0].ContentId,                 Is.EqualTo("logo@example.org"));
                Assert.That(message.FindByContentId("logo@example.org"),        Is.SameAs(message.Root.Children[0]));
                Assert.That(message.FindByContentId("<logo@example.org>"),      Is.SameAs(message.Root.Children[0]));
                Assert.That(message.Root.Children[0].IsInlineResource,          Is.True);
            });

        }

        [TestCase("<root@example.org> (the root)", "<root@example.org>",            TestName = "start: the root's Content-ID with a comment")]
        [TestCase("<root@example.org>",            "<root@example.org> (the root)", TestName = "start: with a comment of its own")]
        [TestCase("<root@example.org>",            "<root@example.org>",            TestName = "start: as it usually is")]
        public void The_root_start_names_is_the_HTML_of_the_message(String RootId, String Start)
        {

            var message = Parse(Related("<logo@example.org>", RootId, Start));

            Assert.That(message.HtmlBody, Is.SameAs(message.Root.Children[1]));

        }

        [Test]
        public void A_Content_ID_that_is_no_msg_id_is_read_as_before()
        {

            var message = Parse(Related("<logo>", "<root@example.org>", "<root@example.org>"));

            Assert.Multiple(() => {
                Assert.That(message.Root.Children[0].ContentId,   Is.EqualTo("logo"));
                Assert.That(message.FindByContentId("logo"),      Is.SameAs(message.Root.Children[0]));
            });

        }

        #endregion

        #region E-18 and E-19: what Content-Disposition says a part is for

        [TestCase("x-archive",                       TestName = "An unknown disposition type is attachment")]
        [TestCase("X-Archive; filename=\"a.txt\"",   TestName = "An unknown disposition type with a file name is attachment")]
        [TestCase("attachment",                      TestName = "attachment is attachment")]
        [TestCase("Attachment",                      TestName = "Attachment in upper case is attachment")]
        public void A_part_the_sender_did_not_mark_inline_is_an_attachment(String Disposition)
        {

            var message = Parse(Mixed(Disposition));

            Assert.Multiple(() => {
                Assert.That(message.TextBody,                                     Is.SameAs(message.Root.Children[0]));
                Assert.That(message.Attachments,                                  Is.EqualTo(new[] { message.Root.Children[1] }));
                Assert.That(message.Root.Children[1].ContentDisposition?.IsAttachment, Is.True);
                Assert.That(message.Root.Children[1].ContentDisposition?.IsInline,     Is.False);
            });

        }

        [Test]
        public void An_image_of_an_unknown_disposition_type_is_no_inline_resource()
        {

            var message = Parse(Related("<logo@example.org>", "<root@example.org>", "<root@example.org>").
                                    Replace("Content-ID: <logo@example.org>", "Content-ID: <logo@example.org>\nContent-Disposition: x-archive"));

            Assert.Multiple(() => {
                Assert.That(message.Root.Children[0].IsInlineResource,  Is.False);
                Assert.That(message.Attachments,                         Is.EqualTo(new[] { message.Root.Children[0] }));
            });

        }

        [TestCase("inline; filename=\"notes.txt\"",  true,  TestName = "A text marked inline with a file name is the body")]
        [TestCase("INLINE; filename=notes.txt",      true,  TestName = "A text marked INLINE with a file name is the body")]
        [TestCase("inline",                          true,  TestName = "A text marked inline is the body")]
        [TestCase("attachment; filename=\"notes.txt\"", false, TestName = "A text marked attachment with a file name is no body")]
        public void A_text_marked_inline_is_the_body(String Disposition, Boolean IsBody)
        {

            var message = Parse(Single(Disposition));

            Assert.Multiple(() => {
                Assert.That(message.TextBody,      IsBody ? Is.SameAs(message.Root) : Is.Null);
                Assert.That(message.Attachments,   IsBody ? Is.Empty : Is.EqualTo(new[] { message.Root }));
            });

        }

        [Test]
        public void A_second_text_marked_inline_with_a_file_name_is_listed_beside_the_body()
        {

            var message = Parse(Mixed("inline; filename=\"second.txt\""));

            Assert.Multiple(() => {
                Assert.That(message.TextBody,      Is.SameAs(message.Root.Children[0]));
                Assert.That(message.Attachments,   Is.EqualTo(new[] { message.Root.Children[1] }));
            });

        }

        #endregion

        #region E-20: a Content-ID written is a msg-id

        [TestCase("logo",                        TestName = "No Content-ID: no @")]
        [TestCase("",                            TestName = "No Content-ID: empty")]
        [TestCase("my logo@example.org",         TestName = "No Content-ID: a space")]
        [TestCase("bild-\u00E4@example.org",     TestName = "No Content-ID: a non-ASCII character")]
        [TestCase("logo@",                       TestName = "No Content-ID: no id-right")]
        [TestCase("a..b@example.org",            TestName = "No Content-ID: two dots")]
        [TestCase("a@b@example.org",             TestName = "No Content-ID: two @")]
        [TestCase("logo@example.org\r\nBcc: x",  TestName = "No Content-ID: a line break")]
        public void A_Content_ID_that_is_no_msg_id_is_refused(String ContentId)

            => Assert.Throws<ArgumentException>(() => new MimeAttachment(Png, "image/png", "logo.png", Inline: true, ContentId: ContentId));

        [TestCase("logo@example.org",            "logo@example.org",          TestName = "Content-ID written: a msg-id")]
        [TestCase("<logo@example.org>",          "logo@example.org",          TestName = "Content-ID written: one given in angle brackets")]
        [TestCase("part1.06090408@example.org",  "part1.06090408@example.org", TestName = "Content-ID written: dots in id-left")]
        [TestCase("logo@[192.0.2.1]",            "logo@[192.0.2.1]",          TestName = "Content-ID written: a no-fold-literal")]
        public void A_Content_ID_that_is_a_msg_id_is_written_and_names_its_part(String ContentId, String Id)
        {

            var message = Parse(new MimeMultipart("related", [ new MimeTextPart($"<img src=\"cid:{Id}\">", "html"),
                                                               new MimeAttachment(Png, "image/png", "logo.png", Inline: true, ContentId: ContentId) ]));

            Assert.Multiple(() => {
                Assert.That(message.Root.Children[1].Headers.Get("Content-ID"),  Is.EqualTo($"<{Id}>"));
                Assert.That(message.FindByContentId(Id),                         Is.SameAs(message.Root.Children[1]));
                Assert.That(message.Root.Children[1].IsInlineResource,           Is.True);
            });

        }

        #endregion

        #region E-21: a multipart/related written has its type

        [Test]
        public void A_multipart_related_has_the_type_of_its_first_part()
        {

            var message = Parse(new MimeMultipart("related", [ new MimeTextPart("<p>x</p>", "html"),
                                                               new MimeAttachment(Png, "image/png", null, Inline: true, ContentId: "logo@example.org") ]));

            Assert.Multiple(() => {
                Assert.That(message.Root.ContentType.Parameters.GetValueOrDefault("type"),  Is.EqualTo("text/html"));
                Assert.That(message.HtmlBody,                                               Is.SameAs(message.Root.Children[0]));
            });

        }

        [Test]
        public void A_multipart_related_has_the_type_of_the_part_start_names()
        {

            var html = new MimeTextPart("<p>x</p>", "html");
            html.Headers.Add(("Content-ID", "<root@example.org>"));

            var message = Parse(new MimeMultipart("related", [ new MimeAttachment(Png, "image/png", null, Inline: true, ContentId: "logo@example.org"), html ],
                                                  [ ("start", "<root@example.org>") ]));

            Assert.Multiple(() => {
                Assert.That(message.Root.ContentType.Parameters.GetValueOrDefault("type"),  Is.EqualTo("text/html"));
                Assert.That(message.HtmlBody,                                               Is.SameAs(message.Root.Children[1]));
            });

        }

        [Test]
        public void A_multipart_related_of_an_alternative_has_its_type()
        {

            var message = Parse(new MimeMultipart("related", [ new MimeMultipart("alternative", [ new MimeTextPart("x"), new MimeTextPart("<p>x</p>", "html") ]),
                                                               new MimeAttachment(Png, "image/png", null, Inline: true, ContentId: "logo@example.org") ]));

            Assert.That(message.Root.ContentType.Parameters.GetValueOrDefault("type"), Is.EqualTo("multipart/alternative"));

        }

        [Test]
        public void A_multipart_related_keeps_the_type_it_is_given()
        {

            var message = Parse(new MimeMultipart("related", [ new MimeTextPart("<p>x</p>", "html") ], [ ("Type", "text/html") ]));

            Assert.That(message.Root.Headers.Get("Content-Type"), Does.Contain("Type=").And.Not.Contain("type="));

        }

        [Test]
        public void A_multipart_mixed_has_no_type()
        {

            var message = Parse(new MimeMultipart("mixed", [ new MimeTextPart("x") ]));

            Assert.That(message.Root.ContentType.Parameters.ContainsKey("type"), Is.False);

        }

        #endregion

    }

}
