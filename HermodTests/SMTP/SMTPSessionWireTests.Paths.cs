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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The paths of MAIL and RCPT as RFC 5321 §4.1.2 has them - source routes, quoted local
    /// parts - and HELP, EXPN and VRFY (§4.1.1.6 to §4.1.1.8).
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        #region Helpers

        /// <summary>
        /// MAIL and RCPT as given, then a message: the replies, the end of data included when
        /// both were taken.
        /// </summary>
        private static async Task<(Int32 Mail, Int32 Rcpt, Int32? Final)> SendWithPaths(Server Server, String MailFrom, String RcptTo)
        {

            using var wire = await Server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            var mail = await wire.CommandAsync(MailFrom);
            var rcpt = await wire.CommandAsync(RcptTo);

            if (mail != 250 || rcpt != 250)
                return (mail, rcpt, null);

            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("Subject: paths\r\n\r\nbody\r\n.\r\n");

            return (mail, rcpt, (await wire.ReplyAsync()).Code);

        }

        #endregion


        #region Source routes

        [TestCase("RCPT TO:<@relay.example:alice@hermod.test>")]
        [TestCase("RCPT TO:<@one.example,@two.example:alice@hermod.test>")]
        [TestCase("RCPT TO:<@[192.0.2.1]:alice@hermod.test>")]
        [Test(Description = "RFC 5321 §4.1.2: A-d-l \"MUST BE accepted, SHOULD NOT be generated, and SHOULD be ignored\"; §4.1.1.3: \"utilize the domain name associated with the mailbox as if the source route had not been provided\" - a local mailbox behind a route is a local recipient")]
        public async Task A_source_route_before_a_local_mailbox_is_ignored(String RcptTo)
        {

            await using var server = new Server();

            var (mail, rcpt, final) = await SendWithPaths(server, "MAIL FROM:<sender@client.example>", RcptTo);

            Assert.Multiple(() => {
                Assert.That(rcpt,                                    Is.EqualTo(250));
                Assert.That(final,                                   Is.EqualTo(250));
                Assert.That(server.Storage.Envelopes.Single().To,    Is.EqualTo(new[] { "alice@hermod.test" }));
            });

        }


        [Test(Description = "RFC 5321 §4.1.2: the reverse-path is a Path too, and its route is ignored - the envelope sender is the mailbox")]
        public async Task A_source_route_in_the_reverse_path_is_ignored()
        {

            await using var server = new Server();

            var (_, _, final) = await SendWithPaths(server, "MAIL FROM:<@relay.example:sender@client.example>", "RCPT TO:<alice@hermod.test>");

            Assert.Multiple(() => {
                Assert.That(final,                                   Is.EqualTo(250));
                Assert.That(server.Storage.Envelopes.Single().From,  Is.EqualTo("sender@client.example"));
            });

        }


        [Test(Description = "RFC 5321 Appendix C: \"The mailbox (here, JOE@THREE) is an absolute address, and the route is information about how to get there\" - a relayed mailbox behind a route is queued for its own domain, without the route")]
        public async Task A_relayed_mailbox_behind_a_source_route_is_queued_for_its_domain()
        {

            await using var server = RelayingServer();

            var (_, rcpt, final) = await SendWithPaths(server, "MAIL FROM:<sender@client.example>", "RCPT TO:<@hop.example:bob@remote.example>");
            var queued           = (await server.Queue!.GetWaitingAsync()).Single();

            Assert.Multiple(() => {
                Assert.That(final,               Is.EqualTo(250));
                Assert.That(queued.TargetDomain, Is.EqualTo("remote.example"));
                Assert.That(queued.EnvelopeTo,   Is.EqualTo(new[] { "bob@remote.example" }));
            });

        }

        #endregion


        #region Quoted local parts

        [TestCase("\"alice@home\"@hermod.test")]
        [TestCase("\"a>b\"@hermod.test")]
        [TestCase("\"john doe\"@hermod.test")]
        [TestCase("\"a\\\"b\"@hermod.test")]
        [TestCase("\"a\\\\\"@hermod.test")]
        [Test(Description = "RFC 5321 §4.1.2: Local-part = Dot-string / Quoted-string; qtextSMTP: \"any ASCII graphic or space is permitted without blackslash-quoting except double-quote and the backslash itself\", quoted-pairSMTP for those - the domain follows the quoted local part, and the path ends after it")]
        public async Task A_quoted_local_part_is_one_local_part(String Mailbox)
        {

            await using var server = new Server();

            var (_, rcpt, final) = await SendWithPaths(server, "MAIL FROM:<sender@client.example>", $"RCPT TO:<{Mailbox}> NOTIFY=NEVER");

            Assert.Multiple(() => {
                Assert.That(rcpt,                                  Is.EqualTo(250));
                Assert.That(final,                                 Is.EqualTo(250));
                Assert.That(server.Storage.Envelopes.Single().To,  Is.EqualTo(new[] { Mailbox }));
            });

        }


        [Test(Description = "RFC 5321 §4.1.2: a reverse-path with a \">\" in its quoted local part, and Mail-parameters after it")]
        public async Task A_quoted_local_part_with_an_angle_bracket_is_a_reverse_path()
        {

            await using var server = new Server();

            var (mail, _, final) = await SendWithPaths(server, "MAIL FROM:<\"x>y\"@client.example> BODY=8BITMIME", "RCPT TO:<alice@hermod.test>");

            Assert.Multiple(() => {
                Assert.That(mail,                                    Is.EqualTo(250));
                Assert.That(final,                                   Is.EqualTo(250));
                Assert.That(server.Storage.Envelopes.Single().From,  Is.EqualTo("\"x>y\"@client.example"));
            });

        }


        [Test(Description = "RFC 5321 §4.1.2: a relayed recipient with an \"@\" in its quoted local part is queued for the domain after it")]
        public async Task A_relayed_quoted_local_part_is_queued_for_its_domain()
        {

            await using var server = RelayingServer();

            var (_, _, final) = await SendWithPaths(server, "MAIL FROM:<sender@client.example>", "RCPT TO:<\"bob@inside\"@remote.example>");
            var queued        = (await server.Queue!.GetWaitingAsync()).Single();

            Assert.Multiple(() => {
                Assert.That(final,               Is.EqualTo(250));
                Assert.That(queued.TargetDomain, Is.EqualTo("remote.example"));
                Assert.That(queued.EnvelopeTo,   Is.EqualTo(new[] { "\"bob@inside\"@remote.example" }));
            });

        }


        [TestCase("RCPT TO:<>")]
        [TestCase("RCPT TO:alice@hermod.test")]
        [TestCase("RCPT TO:<alice@hermod.test")]
        [TestCase("RCPT TO:<\"alice@hermod.test>")]
        [TestCase("RCPT TO:<@relay.example alice@hermod.test>")]
        [TestCase("RCPT TO:<@relay example:alice@hermod.test>")]
        [Test(Description = "RFC 5321 §4.1.2: what is no Forward-path - none at all, no closing \">\", an unclosed quoted string, a route without its \":\" - is a syntax error, 501")]
        public async Task What_is_no_forward_path_is_501(String RcptTo)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync(RcptTo),                              Is.EqualTo(501));

        }

        #endregion


        #region HELP, EXPN, VRFY

        [TestCase("HELP")]
        [TestCase("HELP MAIL")]
        [Test(Description = "RFC 5321 §4.1.1.8: \"SMTP servers SHOULD support HELP without arguments and MAY support it with arguments\" - 214, and the transaction it was given in goes on")]
        public async Task HELP_is_214_and_changes_nothing(String Help)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"),       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync(Help),                                Is.EqualTo(214));
            Assert.That(await wire.CommandAsync("DATA"),                              Is.EqualTo(354));

        }


        [TestCase("EXPN staff",       502)]
        [TestCase("VRFY",             501)]
        [TestCase("VRFY   ",          501)]
        [TestCase("VRFY alice",       252)]
        [Test(Description = "RFC 5321 §4.2.4: \"502 SHOULD be used when the command is actually recognized by the SMTP server, but not implemented\"; §4.1.1.6: vrfy = \"VRFY\" SP String CRLF; §3.5.3: never 250 for what was not verified")]
        public async Task EXPN_is_502_and_VRFY_needs_its_string(String Command, Int32 Code)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync(Command),               Is.EqualTo(Code));

        }

        #endregion

    }

}
