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
    /// RFC 4954 §4: "An AUTH command issued during a mail transaction MUST be rejected with a
    /// 503 reply." The transaction is not touched by the refused AUTH.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [TestCase(false, TestName = "AUTH after MAIL is 503")]
        [TestCase(true,  TestName = "AUTH after MAIL and RCPT is 503")]
        public async Task AUTH_during_a_transaction_is_503(Boolean WithRecipient)
        {

            await using var server = ServerWithSubmissionUser();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            if (WithRecipient)
                Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"),   Is.EqualTo(250));

            Assert.That(await wire.CommandAsync("AUTH SCRAM-SHA-256"), Is.EqualTo(503));

            // Not inside an AUTH exchange now: the next line is a command, and the transaction goes on.
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                      Is.EqualTo(354));
            await wire.SendAsync("Subject: test\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

        }


        /// <summary>
        /// Between transactions AUTH is fine - after RSET as after a delivered message.
        /// </summary>
        [TestCase("RSET")]
        [TestCase("DATA")]
        public async Task AUTH_after_the_transaction_ended_is_fine(String End)
        {

            await using var server = ServerWithSubmissionUser();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            Assert.That(await wire.CommandAsync(End), Is.EqualTo(End == "DATA" ? 354 : 250));
            if (End == "DATA")
            {
                await wire.SendAsync("Subject: test\r\n\r\nbody\r\n.\r\n");
                Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));
            }

            await AuthenticateWithScram(wire);

        }

    }

}
