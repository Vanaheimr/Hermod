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
    /// RFC 5321 §4.5.4.1: when the server "to which a copy of the message is to be sent is the
    /// same for multiple recipients, then only one copy of the message SHOULD be transmitted";
    /// §2.4: "Mailbox domains follow normal DNS rules and are hence not case sensitive", and a
    /// U-label is its A-label (RFC 5890 §2.3.2.1). The server queues one copy for the relayed
    /// recipients of one domain, however its name is written.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// Relay one message to the given recipients, and return what was queued.
        /// </summary>
        private static async Task<IReadOnlyList<org.GraphDefined.Vanaheimr.Hermod.SMTP.QueuedMail>> RelayTo(Boolean SmtpUtf8, params String[] Recipients)
        {

            await using var server = RelayingServer();
            using var       wire   = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>" + (SmtpUtf8 ? " SMTPUTF8" : "")), Is.EqualTo(250));

            // In UTF-8: the wire's own commands are ASCII.
            foreach (var recipient in Recipients)
            {
                await wire.SendAsync(System.Text.Encoding.UTF8.GetBytes($"RCPT TO:<{recipient}>\r\n"));
                Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250), recipient);
            }

            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("Subject: copies\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            return await server.Queue!.GetWaitingAsync();

        }


        [TestCase(false, "a@remote.example",  "b@REMOTE.example",          "remote.example",     TestName = "Recipients at remote.example and REMOTE.example get one copy")]
        [TestCase(true,  "a@bücher.test",     "b@xn--bcher-kva.test",      "xn--bcher-kva.test", TestName = "Recipients at bücher.test and xn--bcher-kva.test get one copy")]
        [TestCase(false, "a@remote.example",  "b@remote.example",          "remote.example",     TestName = "Recipients at remote.example get one copy")]
        public async Task Recipients_at_one_domain_get_one_copy(Boolean SmtpUtf8, String First, String Second, String Domain)
        {

            var queued = await RelayTo(SmtpUtf8, First, Second);

            Assert.That(queued, Has.Count.EqualTo(1), String.Join("; ", queued.Select(mail => mail.TargetDomain)));
            Assert.Multiple(() => {
                Assert.That(queued[0].TargetDomain, Is.EqualTo(Domain));
                Assert.That(queued[0].EnvelopeTo,   Is.EqualTo(new[] { First, Second }));
            });

        }


        [Test]
        public async Task Recipients_at_two_domains_get_two_copies()
        {

            var queued = await RelayTo(false, "a@remote.example", "b@other.example");

            Assert.That(queued.Select(mail => mail.TargetDomain), Is.EquivalentTo(new[] { "remote.example", "other.example" }));

        }

    }

}
