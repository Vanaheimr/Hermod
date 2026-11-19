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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// MT-PRIORITY on the server (RFC 6710 §4.1): one parameter, as the grammar writes it;
    /// raised only by a client that signed in; written into the Received field; and carried
    /// on to the relay queue as determined (§4.2).
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// A server that relays for clients that did not sign in - as between MTAs on port 25.
        /// </summary>
        private static Server RelayingServer()
            => new (WithQueue: true, Configure: config => config with { RequireAuthForRelay = false });

        /// <summary>
        /// MAIL with the given parameters, one remote recipient and a message: the MAIL reply,
        /// and the queued mail.
        /// </summary>
        private static async Task<((Int32 Code, String Text) Mail, QueuedMail Queued)> RelayAsync(Server Server, Wire Wire, String Sender, String Parameters)
        {

            await Wire.SendAsync($"MAIL FROM:<{Sender}> {Parameters}\r\n");
            var mail = await Wire.ReplyAsync();

            Assert.That(mail.Code,                                                Is.EqualTo(250), mail.Text);
            Assert.That(await Wire.CommandAsync("RCPT TO:<bob@remote.example>"),  Is.EqualTo(250));
            Assert.That(await Wire.CommandAsync("DATA"),                          Is.EqualTo(354));
            await Wire.SendAsync($"From: <{Sender}>\r\nSubject: priority\r\n\r\nbody\r\n.\r\n");
            Assert.That((await Wire.ReplyAsync()).Code,                           Is.EqualTo(250));

            return (mail, (await Server.Queue!.GetWaitingAsync()).Single());

        }

        /// <summary>
        /// The Received field the server put on top of the one stored message.
        /// </summary>
        private static String ReceivedField(Server Server)
            => Regex.Match(Server.Storage.Messages.Single(), @"^Received:[^\r\n]*(?:\r\n[ \t][^\r\n]*)*", RegexOptions.Multiline).Value;


        [TestCase("MT-PRIORITY=+3")]
        [TestCase("MT-PRIORITY=-0")]
        [TestCase("MT-PRIORITY=05")]
        [TestCase("MT-PRIORITY=3 MT-PRIORITY=3")]
        [TestCase("MT-PRIORITY=3 mt-priority=-3")]
        [Test(Description = "RFC 6710 §4.1: \"If any of the associated <esmtp-value>s [...] are not syntactically valid, or if there is more than one MT-PRIORITY parameter in a particular MAIL FROM command, the server MUST return an error\"")]
        public async Task MT_PRIORITY_outside_the_grammar_or_given_twice_is_501(String Parameters)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),                               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"MAIL FROM:<sender@client.example> {Parameters}"),  Is.EqualTo(501));

            // Nothing was opened: a correct MAIL follows.
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example> MT-PRIORITY=-3"),  Is.EqualTo(250));

        }


        [Test(Description = "RFC 6710 §4.1: the server \"MUST NOT allow \"upgraded\" (positive) priorities from untrusted (e.g., unauthenticated) or unauthorized sources\"; a change it \"SHOULD\" report with X.3.6, the new priority first")]
        public async Task A_client_that_did_not_sign_in_relays_at_priority_0_at_most()
        {

            await using var server = RelayingServer();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO mx.client.example"), Is.EqualTo(250));

            var (mail, queued) = await RelayAsync(server, wire, "sender@client.example", "MT-PRIORITY=7");

            Assert.Multiple(() => {
                Assert.That(mail.Text,       Does.StartWith("2.3.6 0 "), "X.3.6, then the new priority");
                Assert.That(queued.Priority, Is.EqualTo((SByte) 0));
            });

        }


        [Test(Description = "RFC 6710 §4.1: \"The server MAY, however, allow an untrusted source to lower its own message's priorities\"")]
        public async Task A_client_that_did_not_sign_in_may_lower_its_priority()
        {

            await using var server = RelayingServer();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO mx.client.example"), Is.EqualTo(250));

            var (mail, queued) = await RelayAsync(server, wire, "sender@client.example", "MT-PRIORITY=-4");

            Assert.Multiple(() => {
                Assert.That(mail.Text,       Is.EqualTo("2.1.0 OK"), "nothing changed, nothing to report");
                Assert.That(queued.Priority, Is.EqualTo((SByte) (-4)));
            });

        }


        [Test(Description = "RFC 6710 §4.2: the relayed MAIL carries the MT-PRIORITY \"with the value determined by the procedure from Section 4.1\" - for a client that signed in, the one it asked for")]
        public async Task A_client_that_signed_in_relays_at_the_priority_it_asked_for()
        {

            await using var server = ServerWithSubmissionUser();
            using var wire         = await server.ConnectAsync(server.SubmissionPort);

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            await AuthenticateWithScram(wire);

            var (mail, queued) = await RelayAsync(server, wire, "alice@hermod.test", "MT-PRIORITY=7");

            Assert.Multiple(() => {
                Assert.That(mail.Text,       Is.EqualTo("2.1.0 OK"));
                Assert.That(queued.Priority, Is.EqualTo((SByte) 7));
            });

        }


        [TestCase("MT-PRIORITY=-3", "-3")]
        [TestCase("MT-PRIORITY=0",  "0")]
        [TestCase("MT-PRIORITY=7",  "0",  Description = "not signed in: the priority the server determined")]
        [Test(Description = "RFC 6710 §4.1: the MTA \"SHOULD include the \"PRIORITY\" clause\" in its Received field - Pri = CFWS \"PRIORITY\" FWS priority-value (§7), after For (RFC 5321 §4.4)")]
        public async Task The_Received_field_names_the_priority(String Parameters, String Priority)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO mx.client.example"),                            Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"MAIL FROM:<sender@client.example> {Parameters}"),  Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"),                       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                                              Is.EqualTo(354));
            await wire.SendAsync("Subject: no hurry\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code,                                               Is.EqualTo(250));

            Assert.That(ReceivedField(server), Does.Match(@"\sfor <alice@hermod\.test>\r\n\tPRIORITY " + Regex.Escape(Priority) + ";"));

        }


        [Test(Description = "RFC 6710 §10: the PRIORITY clause \"Records the value of the MT-PRIORITY parameter specified in the MAIL FROM command\" - without one, there is none")]
        public async Task Without_MT_PRIORITY_the_Received_field_names_no_priority()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"),  Is.EqualTo(354));
            await wire.SendAsync("Subject: plain\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code,   Is.EqualTo(250));

            Assert.That(ReceivedField(server), Does.Not.Contain("PRIORITY"));

        }

    }

}
