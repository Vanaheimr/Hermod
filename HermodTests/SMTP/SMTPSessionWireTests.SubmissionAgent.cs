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

using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The server as the Message Submission Agent (RFC 6409) - a client signed in, no application
    /// to hand its message to: §4.2, "The MSA MUST ensure that all domains in the SMTP envelope
    /// are fully qualified"; §8, "Any message forwarded or delivered by the MSA MUST conform to
    /// the requirements of [SMTP-MTA] and [MESSAGE-FORMAT]"; §8.3, a Message-ID for the message
    /// that lacks one. And RFC 5321 §6.4: none of it for mail from other servers.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private const String SubmittedDate = "Date: Thu, 26 Nov 2026 10:00:00 +0000";

        private static String[] FieldsOf(String Message, String Name)
            => [ .. Message[..Message.IndexOf("\r\n\r\n", StringComparison.Ordinal)].
                    Split("\r\n").
                    Where (line => line.StartsWith(Name + ":", StringComparison.OrdinalIgnoreCase)).
                    Select(line => line[(Name.Length + 1)..].Trim()) ];

        /// <summary>
        /// Signed in as alice on the submission port, no application: the server stores what is
        /// for its own domain. The reply to the end of data of one message with this header.
        /// </summary>
        private static async Task<(Int32 Code, String Text)> SubmitAsync(Server Server, String Header, String RcptTo = "bob@hermod.test")
        {

            var (wire, _) = await SecureAsync(Server, Server.SubmissionPort);

            using (wire)
            {
                Assert.That(await wire.CommandAsync("AUTH PLAIN " + Plain("", "alice", "secret")), Is.EqualTo(235));
                Assert.That(await wire.CommandAsync("MAIL FROM:<alice@hermod.test>"),             Is.EqualTo(250));
                Assert.That(await wire.CommandAsync($"RCPT TO:<{RcptTo}>"),                       Is.EqualTo(250));
                Assert.That(await wire.CommandAsync("DATA"),                                      Is.EqualTo(354));
                await wire.SendAsync(Header + "Subject: submitted\r\n\r\nbody\r\n.\r\n");
                return await wire.ReplyAsync();
            }

        }

        private static Server SubmissionAgent()
            => new (Configure: SubmissionOnly, UserStore: new Passwords(("alice", "secret")));


        [Test]
        public async Task A_submitted_message_without_Date_and_Message_ID_gets_both()
        {

            await using var server = SubmissionAgent();

            Assert.That((await SubmitAsync(server, "From: <alice@hermod.test>\r\n")).Code, Is.EqualTo(250));

            var stored = server.Storage.Messages.Single();

            Assert.That(FieldsOf(stored, "Date"),        Has.Length.EqualTo(1).And.All.Match(@"^\w{3}, \d{2} \w{3} \d{4} \d{2}:\d{2}:\d{2} \+0000$"));
            Assert.That(FieldsOf(stored, "Message-ID"),  Has.Length.EqualTo(1).And.All.Match(@"^<[0-9a-f]{32}@mx\.hermod\.test>$"));

            // The trace fields stay on top (RFC 5321 §4.4), the two added below them.
            Assert.That(stored.IndexOf("\r\nReceived:", StringComparison.Ordinal), Is.LessThan(stored.IndexOf("\r\nDate:",       StringComparison.Ordinal)));
            Assert.That(stored.IndexOf("\r\nReceived:", StringComparison.Ordinal), Is.LessThan(stored.IndexOf("\r\nMessage-ID:", StringComparison.Ordinal)));

        }


        [Test]
        public async Task The_Date_and_Message_ID_a_client_gave_are_kept()
        {

            await using var server = SubmissionAgent();

            Assert.That((await SubmitAsync(server, $"From: <alice@hermod.test>\r\n{SubmittedDate}\r\nMessage-ID: <given@client.example>\r\n")).Code, Is.EqualTo(250));

            var stored = server.Storage.Messages.Single();

            Assert.That(FieldsOf(stored, "Date"),        Is.EqualTo(new[] { SubmittedDate[6..] }));
            Assert.That(FieldsOf(stored, "Message-ID"),  Is.EqualTo(new[] { "<given@client.example>" }));

        }


        [TestCase("",                                    TestName = "A submitted message without From is 554")]
        [TestCase("From: alice\r\n",                     TestName = "A submitted message with a From without a domain is 554")]
        [TestCase("From: <alice@intranet>\r\n",          TestName = "A submitted message with a From at a single-label domain is 554")]
        [TestCase("From: <a@hermod.test>, <b@x>\r\n",    TestName = "A submitted message with one From address at a single-label domain is 554")]
        public async Task A_submitted_message_without_a_fully_qualified_author_is_554(String From)
        {

            await using var server = SubmissionAgent();

            Assert.That((await SubmitAsync(server, From + SubmittedDate + "\r\n")).Code, Is.EqualTo(554));
            Assert.That(server.Storage.Messages, Is.Empty);

        }


        [TestCase("MAIL FROM:<alice@intranet>",  "",                                 TestName = "A submission from a single-label domain is 554")]
        [TestCase("MAIL FROM:<alice@hermod.test>", "RCPT TO:<bob@intranet>",          TestName = "A submission to a single-label domain is 554")]
        [TestCase("MAIL FROM:<alice@hermod.test>", "RCPT TO:<bob@intranet.>",         TestName = "A submission to a single-label domain with a final dot is 554")]
        public async Task A_single_label_domain_in_the_envelope_of_a_submission_is_554(String Mail, String Rcpt)
        {

            await using var server = SubmissionAgent();

            var (wire, _) = await SecureAsync(server, server.SubmissionPort);

            using (wire)
            {

                Assert.That(await wire.CommandAsync("AUTH PLAIN " + Plain("", "alice", "secret")), Is.EqualTo(235));

                if (Rcpt.Length == 0)
                    Assert.That(await wire.CommandAsync(Mail), Is.EqualTo(554));

                else
                {
                    Assert.That(await wire.CommandAsync(Mail), Is.EqualTo(250));
                    Assert.That(await wire.CommandAsync(Rcpt), Is.EqualTo(554));
                }

            }

        }


        [Test]
        public async Task The_postmaster_without_a_domain_is_still_a_recipient_of_a_submission()
        {

            await using var server = SubmissionAgent();

            Assert.That((await SubmitAsync(server, $"From: <alice@hermod.test>\r\n{SubmittedDate}\r\n", RcptTo: "postmaster")).Code, Is.EqualTo(250));
            Assert.That(server.Storage.Envelopes.Single().To, Is.EqualTo(new[] { "postmaster@mx.hermod.test" }));

        }


        [Test]
        public async Task A_message_from_another_server_gets_no_Date_and_no_Message_ID()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("From: <sender@client.example>\r\nSubject: relayed\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            var stored = server.Storage.Messages.Single();

            Assert.That(FieldsOf(stored, "Date"),        Is.Empty, "RFC 5321 §6.4: not for mail from other servers");
            Assert.That(FieldsOf(stored, "Message-ID"),  Is.Empty, "RFC 5321 §6.4: not for mail from other servers");

        }


        [Test]
        public async Task A_message_for_the_application_is_handed_over_as_it_came()
        {

            var application        = new Application();
            await using var server = new Server(Configure: SubmissionOnly, UserStore: new Passwords(("alice", "secret")), Submission: application);

            Assert.That((await SubmitAsync(server, "From: <alice@hermod.test>\r\n")).Code, Is.EqualTo(250));

            var handed = application.Submitted.Single().Message;

            Assert.That(FieldsOf(handed, "Date"),        Is.Empty, "the application's to complete");
            Assert.That(FieldsOf(handed, "Message-ID"),  Is.Empty, "the application's to complete");

        }

    }

}
