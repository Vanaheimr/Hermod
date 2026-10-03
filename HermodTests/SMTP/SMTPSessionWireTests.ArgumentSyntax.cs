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
    /// The argument rules of RFC 5321 §4.1.1: EHLO and HELO need a domain; DATA and RSET take
    /// nothing, and neither does STARTTLS (RFC 3207 §4: "501 Syntax error (no parameters
    /// allowed)"). A command refused with 501 has no effect.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [TestCase("EHLO")]
        [TestCase("EHLO ")]
        [TestCase("HELO")]
        public async Task A_greeting_without_a_domain_is_501_and_greets_nobody(String Greeting)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync(Greeting),                          Is.EqualTo(501));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(503), "the session is not greeted");
            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));

        }


        [Test]
        public async Task DATA_with_an_argument_is_501_and_opens_no_DATA_phase()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            Assert.That(await wire.CommandAsync("DATA please"), Is.EqualTo(501));

            // The transaction is intact: DATA proper still works.
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("Subject: test\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(server.Storage.Messages, Has.Count.EqualTo(1));

        }


        [Test]
        public async Task RSET_with_an_argument_is_501_and_keeps_the_transaction()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            Assert.That(await wire.CommandAsync("RSET now"), Is.EqualTo(501));
            Assert.That(await wire.CommandAsync("DATA"),     Is.EqualTo(354), "the recipient is still there");

        }


        /// <summary>
        /// quit = "QUIT" CRLF (§4.1.1.10), but a client that says QUIT is leaving either way:
        /// refusing it would only keep a connection open that both sides are done with.
        /// </summary>
        [Test]
        public async Task QUIT_with_an_argument_still_ends_the_session()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("QUIT now"),            Is.EqualTo(221));
            Assert.That(await wire.TryReplyAsync(),                     Is.Null, "the server closes the connection");

        }


        [Test]
        public async Task STARTTLS_with_an_argument_is_501_and_starts_no_TLS()
        {

            await using var server = new Server(Configure: WithServerCertificate);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("STARTTLS now"),        Is.EqualTo(501));

            // Still cleartext: a plain STARTTLS then works.
            Assert.That(await wire.CommandAsync("STARTTLS"),            Is.EqualTo(220));
            await wire.StartTlsAsync();
            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

        }


        /// <summary>
        /// RFC 5321 §4.1.1.9: noop = "NOOP" [ SP String ] CRLF - an argument is allowed.
        /// </summary>
        [Test]
        public async Task NOOP_with_an_argument_is_fine()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("NOOP anything"),       Is.EqualTo(250));

        }

    }

}
