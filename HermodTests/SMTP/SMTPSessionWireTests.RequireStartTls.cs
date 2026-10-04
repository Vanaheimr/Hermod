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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 3207 §4: a server that requires TLS "SHOULD return the reply code: 530 Must issue a
    /// STARTTLS command first to every command other than NOOP, EHLO, STARTTLS, or QUIT."
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private static SMTPServerConfig RequiringStartTls(SMTPServerConfig config)
            => WithServerCertificate(config) with { RequireStartTls = true };


        [TestCase("HELO client.example")]
        [TestCase("AUTH SCRAM-SHA-256")]
        [TestCase("VRFY alice")]
        [TestCase("RSET")]
        [TestCase("MAIL FROM:<sender@client.example>")]
        [TestCase("RCPT TO:<alice@hermod.test>")]
        [TestCase("DATA")]
        [TestCase("FROBNICATE")]
        public async Task Before_STARTTLS_a_command_is_530_5_7_0(String Command)
        {

            await using var server = new Server(Configure: RequiringStartTls);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            await wire.SendAsync(Command + "\r\n");
            var reply = await wire.ReplyAsync();

            Assert.That(reply.Code, Is.EqualTo(530));
            Assert.That(reply.Text, Does.StartWith("5.7.0 "));

            // Nothing was started: the session is still in step.
            Assert.That(await wire.CommandAsync("NOOP"), Is.EqualTo(250));

        }


        [TestCase("NOOP",                 250)]
        [TestCase("EHLO client.example",  250)]
        [TestCase("STARTTLS",             220)]
        [TestCase("QUIT",                 221)]
        public async Task Before_STARTTLS_NOOP_EHLO_STARTTLS_and_QUIT_are_answered(String Command, Int32 Code)
        {

            await using var server = new Server(Configure: RequiringStartTls);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync(Command),               Is.EqualTo(Code));

        }


        /// <summary>
        /// AUTH is not offered where it would be 530; after STARTTLS it is offered and works, and so
        /// does the transaction. AUTH is the last EHLO keyword when there is one.
        /// </summary>
        [Test]
        public async Task AUTH_is_offered_and_works_only_after_STARTTLS()
        {

            await using var server = ServerWithSubmissionUser(Configure: RequiringStartTls);
            using var wire         = await server.ConnectAsync();

            await wire.SendAsync("EHLO client.example\r\n");
            var beforeTls = await wire.ReplyAsync();
            Assert.That(beforeTls.Text, Does.Not.StartWith("AUTH"), "no AUTH before TLS");

            Assert.That(await wire.CommandAsync("STARTTLS"), Is.EqualTo(220));
            await wire.StartTlsAsync();

            await wire.SendAsync("EHLO client.example\r\n");
            var afterTls = await wire.ReplyAsync();
            Assert.That(afterTls.Text, Does.StartWith("AUTH "), "AUTH inside TLS");

            await AuthenticateWithScram(wire);

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("Subject: test\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

        }

    }

}
