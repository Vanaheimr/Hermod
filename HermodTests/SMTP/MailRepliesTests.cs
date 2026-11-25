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
    /// What an answer takes from the message it answers, and the list it came through: the
    /// subject of a reply (RFC 5322 §3.6.5), List-Post as MailReplies reads it and the older
    /// builder writes it (RFC 2369 §2, §3.4), and its mailto URL (RFC 6068 §2). The findings
    /// E-22 to E-25 of SMTPConformanceTests.
    /// </summary>
    [TestFixture]
    public class MailRepliesTests
    {

        #region Helpers

        private static MimeMessage WithListPost(String ListPost)
            => MimeMessage.Parse(Encoding.UTF8.GetBytes($"From: author@example.org\r\nTo: list@example.org\r\nList-Post: {ListPost}\r\nSubject: A post\r\n\r\nText.\r\n"));

        private static IReadOnlyList<String> Written(String ListPost)
        {

            var builder = new TextEMailBuilder {
                              From     = EMailAddress.Parse("Mary Smith <mary@example.net>"),
                              Subject  = "A post",
                              Text     = "Hello.\r\n"
                          };

            builder.To       = EMailAddressListBuilder.Create(EMailAddress.Parse("John Doe <john@example.org>")!);
            builder.ListPost = SimpleEMailAddress.Parse(ListPost);

            return [ .. ((EMail) builder).ToText(false) ];

        }

        #endregion


        #region E-22: one "Re: " in the subject of a reply

        [TestCase("[dev] Re: Hello",              "Re: [dev] Hello",       TestName = "Reply subject: a list's tag before Re:")]
        [TestCase("Re: [dev] Re: Hello",          "Re: [dev] Hello",       TestName = "Reply subject: Re: before and after a list's tag")]
        [TestCase("AW: [dev] WG: Hello",          "Re: [dev] Hello",       TestName = "Reply subject: other languages' prefixes around a list's tag")]
        [TestCase("[dev] Re: [DEV] Re: Hello",    "Re: [dev] Hello",       TestName = "Reply subject: a list's tag twice is kept once")]
        [TestCase("[dev] [announce] Re: Hello",   "Re: [dev] [announce] Hello", TestName = "Reply subject: two lists' tags are kept")]
        [TestCase("[dev] Hello",                  "Re: [dev] Hello",       TestName = "Reply subject: a list's tag, no Re:")]
        [TestCase("[PATCH 1/3] Fix the queue",    "Re: [PATCH 1/3] Fix the queue", TestName = "Reply subject: brackets that are no prefix stay")]
        [TestCase("Re: Re: Hello",                "Re: Hello",             TestName = "Reply subject: Re: twice")]
        [TestCase("Hello [dev] Re: x",            "Re: Hello [dev] Re: x", TestName = "Reply subject: what is inside the subject stays")]
        public void The_subject_of_a_reply_has_one_Re(String Subject, String Reply)

            => Assert.That(MailReplies.ReplySubject(Subject), Is.EqualTo(Reply));

        [Test]
        public void The_subject_of_a_forward_keeps_a_lists_tag_once()

            => Assert.That(MailReplies.ForwardSubject("[dev] Re: Hello"), Is.EqualTo("Fwd: [dev] Hello"));

        #endregion

        #region E-23 and E-24: List-Post as RFC 2369 has it

        [TestCase("<mailto:list@example.org>",                                         "list@example.org",     TestName = "List-Post: a mailto URL")]
        [TestCase("<mailto:list@\r\n example.org>",                                    "list@example.org",     TestName = "List-Post: folded inside the URL")]
        [TestCase("< mailto:list@example.org >",                                       "list@example.org",     TestName = "List-Post: white space around the URL")]
        [TestCase("<mailto:list@exam ple.org>",                                        "list@example.org",     TestName = "List-Post: white space inside the URL")]
        [TestCase("(not <mailto:wrong@example.org>) <mailto:list@example.org>",        "list@example.org",     TestName = "List-Post: a URL in a comment before is none")]
        [TestCase("(a (nested \\) one)) <mailto:list@example.org>",                    "list@example.org",     TestName = "List-Post: a nested comment with a quoted-pair before")]
        [TestCase("<https://example.org/post> (moderated) <mailto:other@example.org>", null,                   TestName = "List-Post: a URL after another and no comma is ignored")]
        [TestCase("<https://example.org/post>, by mail: <mailto:other@example.org>",   null,                   TestName = "List-Post: a URL after a sub-item that is no URL is ignored")]
        [TestCase("<https://example.org/post>,\r\n <mailto:list@example.org>",         "list@example.org",     TestName = "List-Post: a mailto URL after another, a comma between")]
        [TestCase("<https://example.org/post> (web) , <mailto:list@example.org>",      "list@example.org",     TestName = "List-Post: a comment before the comma")]
        [TestCase("<mailto:list@example.org> (moderated)",                             "list@example.org",     TestName = "List-Post: a comment after the URL")]
        [TestCase("<MAILTO:list@example.org>",                                         "list@example.org",     TestName = "List-Post: the scheme in upper case")]
        [TestCase("<mailto:list@example.org?subject=post>",                            "list@example.org",     TestName = "List-Post: header fields of the URL")]
        [TestCase("<mailto:dev%2Fannounce@example.org>",                               "dev/announce@example.org", TestName = "List-Post: a percent-encoded address")]
        [TestCase("<https://example.org/post>",                                        null,                   TestName = "List-Post: no mailto URL")]
        [TestCase("NO (posting not allowed on this list)",                             null,                   TestName = "List-Post: NO")]
        [TestCase("mailto:list@example.org",                                           null,                   TestName = "List-Post: a URL without angle brackets is none")]
        [TestCase("<mailto:list@example.org",                                          null,                   TestName = "List-Post: no closing angle bracket")]
        public void List_Post_names_the_list(String ListPost, String? Address)

            => Assert.That(MailReplies.ListPost(WithListPost(ListPost)), Is.EqualTo(Address));

        [Test]
        public void A_reply_to_the_list_goes_to_the_address_List_Post_names()
        {

            var addressing = MailReplies.Addressing(WithListPost("(post here) <mailto:list@\r\n example.org>"), ReplyKind.List, [ "me@example.net" ]);

            Assert.That(addressing.To.Select(mailbox => mailbox.Address), Is.EqualTo(new[] { "list@example.org" }));

        }

        #endregion

        #region E-25: the List-Post the older builder writes

        [TestCase("dev-announce@lists.example.org", "<mailto:dev-announce@lists.example.org>",     TestName = "List-Post written: nothing to encode")]
        [TestCase("dev/announce@lists.example.org", "<mailto:dev%2Fannounce@lists.example.org>",   TestName = "List-Post written: a slash")]
        [TestCase("dev?announce@lists.example.org", "<mailto:dev%3Fannounce@lists.example.org>",   TestName = "List-Post written: a question mark")]
        [TestCase("list#1@lists.example.org",       "<mailto:list%231@lists.example.org>",         TestName = "List-Post written: a number sign")]
        [TestCase("list%1@lists.example.org",       "<mailto:list%251@lists.example.org>",         TestName = "List-Post written: a percent sign")]
        public void List_Post_is_written_as_a_mailto_URL_of_the_address(String Address, String ListPost)
        {

            var lines   = Written(Address);
            var message = MimeMessage.Parse(Encoding.UTF8.GetBytes(String.Join("\r\n", lines) + "\r\n"));

            Assert.Multiple(() => {
                Assert.That(lines,                         Has.Member("List-Post: " + ListPost));
                Assert.That(MailReplies.ListPost(message), Is.EqualTo(Address));
            });

        }

        #endregion

    }

}
