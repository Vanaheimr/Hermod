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
    /// RFC 5321 §4.1.4: "MAIL [...] MUST NOT be sent if a mail transaction is already open";
    /// §4.3.2 lists 503 for it. The open transaction is not touched by the refused MAIL.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [Test]
        public async Task MAIL_inside_a_transaction_is_503_and_the_transaction_stays()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            Assert.That(await wire.CommandAsync("MAIL FROM:<other@client.example>"), Is.EqualTo(503));

            // The first transaction goes on: its sender and its recipient.
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("Subject: test\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            var envelope = server.Storage.Envelopes.Single();
            Assert.That(envelope.From, Is.EqualTo("sender@client.example"));
            Assert.That(envelope.To,   Is.EqualTo(new[] { "alice@hermod.test" }));

        }


        [Test]
        public async Task MAIL_after_MAIL_without_recipients_is_503_as_well()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<other@client.example>"),  Is.EqualTo(503));

        }


        /// <summary>
        /// Whatever ends the transaction - RSET, a new EHLO, a delivered message - opens
        /// the way for the next MAIL.
        /// </summary>
        [TestCase("RSET")]
        [TestCase("EHLO client.example")]
        [TestCase("DATA")]
        public async Task MAIL_after_the_transaction_ended_is_fine(String End)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            Assert.That(await wire.CommandAsync(End), Is.EqualTo(End == "DATA" ? 354 : 250));
            if (End == "DATA")
            {
                await wire.SendAsync("Subject: test\r\n\r\nbody\r\n.\r\n");
                Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));
            }

            Assert.That(await wire.CommandAsync("MAIL FROM:<other@client.example>"), Is.EqualTo(250));

        }

    }

}
