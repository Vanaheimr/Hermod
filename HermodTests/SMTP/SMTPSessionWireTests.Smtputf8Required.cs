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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 6531 §3.5: a non-ASCII address in a transaction that did not ask for SMTPUTF8 is
    /// refused - 550 at MAIL, 553 at RCPT, with X.6.7 "Non-ASCII addresses not permitted for
    /// that sender/recipient" (RFC 6533).
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private static async Task<(Int32 Code, String Text)> Utf8CommandAsync(Wire Wire, String Line)
        {
            await Wire.SendAsync(Encoding.UTF8.GetBytes(Line + "\r\n"));
            return await Wire.ReplyAsync();
        }


        [Test]
        public async Task A_non_ASCII_sender_without_SMTPUTF8_is_550_5_6_7()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            var reply = await Utf8CommandAsync(wire, "MAIL FROM:<jöran@bücher.example>");

            Assert.That(reply.Code, Is.EqualTo(550));
            Assert.That(reply.Text, Does.StartWith("5.6.7 "));

            // No transaction was opened.
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test>"), Is.EqualTo(503));

        }


        [Test]
        public async Task A_non_ASCII_recipient_without_SMTPUTF8_is_553_5_6_7()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);

            var reply = await Utf8CommandAsync(wire, "RCPT TO:<ümlaut@hermod.test>");

            Assert.That(reply.Code, Is.EqualTo(553));
            Assert.That(reply.Text, Does.StartWith("5.6.7 "));

            // The transaction goes on with the recipients it has.
            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync("Subject: test\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));
            Assert.That(server.Storage.Envelopes.Single().To, Is.EqualTo(new[] { "alice@hermod.test" }));

        }


        /// <summary>
        /// SMTPUTF8 is asked for per transaction: the next MAIL without it is ASCII-only again.
        /// </summary>
        [Test]
        public async Task SMTPUTF8_lasts_for_its_transaction_only()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            Assert.That((await Utf8CommandAsync(wire, "MAIL FROM:<sender@client.example> SMTPUTF8")).Code, Is.EqualTo(250));
            Assert.That((await Utf8CommandAsync(wire, "RCPT TO:<ümlaut@hermod.test>")).Code,           Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RSET"),                                              Is.EqualTo(250));

            Assert.That((await Utf8CommandAsync(wire, "MAIL FROM:<sender@client.example>")).Code,      Is.EqualTo(250));
            Assert.That((await Utf8CommandAsync(wire, "RCPT TO:<ümlaut@hermod.test>")).Code,           Is.EqualTo(553));

        }

    }

}
