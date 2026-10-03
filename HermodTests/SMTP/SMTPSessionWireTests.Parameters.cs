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
    /// MAIL/RCPT parameters on the wire (RFC 5321 §4.1.1.11): a refused parameter refuses
    /// the command, and nothing about the transaction changes.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [Test]
        public async Task An_unknown_MAIL_parameter_is_555_and_opens_no_transaction()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),                                 Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example> X-NO-SUCH-PARAM=1"),  Is.EqualTo(555));

            // No transaction was opened by the refused MAIL.
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"),                         Is.EqualTo(503));

            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example> SIZE=100"),           Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test> NOTIFY=NEVER,SUCCESS"),     Is.EqualTo(501));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test> NOTIFY=SUCCESS"),           Is.EqualTo(250));

        }


        [Test]
        public async Task After_HELO_a_MAIL_parameter_is_555()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("HELO client.example"),                         Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example> SIZE=100"),   Is.EqualTo(555));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"),            Is.EqualTo(250));

        }

    }

}
