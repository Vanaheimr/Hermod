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
    /// RFC 6533 §3: the UTF-8 address type in ORCPT, over the wire.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private static async Task<Int32> CommandUtf8Async(Wire Wire, String Line)
        {
            await Wire.SendAsync(Encoding.UTF8.GetBytes(Line + "\r\n"));
            return (await Wire.ReplyAsync()).Code;
        }


        [Test(Description = "RFC 6533 §3: a server that advertises SMTPUTF8 and DSN \"MUST accept a UTF-8 address type in the ORCPT parameter including 8-bit UTF-8 characters\" - in an SMTPUTF8 transaction, and the Original-Recipient of the delivered message names it (RFC 8098 §2.3)")]
        public async Task An_ORCPT_of_the_UTF_8_address_type_in_UTF_8_is_taken()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),                                          Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example> SMTPUTF8"),                   Is.EqualTo(250));
            Assert.That(await CommandUtf8Async(wire, "RCPT TO:<alice@hermod.test> ORCPT=utf-8;jörg@hermod.test"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                                                         Is.EqualTo(354));

            await wire.SendAsync("Subject: test\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(server.Storage.Messages.Single(), Does.Contain("Original-Recipient: utf-8;jörg@hermod.test\r\n"));

        }


        [Test(Description = "RFC 6533 §3: without SMTPUTF8 on MAIL the ORCPT is utf-8-addr-xtext, ASCII - one with UTF-8 in it is 501")]
        public async Task An_ORCPT_in_UTF_8_outside_an_SMTPUTF8_transaction_is_501()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),                                           Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"),                             Is.EqualTo(250));
            Assert.That(await CommandUtf8Async(wire, "RCPT TO:<alice@hermod.test> ORCPT=utf-8;jörg@hermod.test"),  Is.EqualTo(501));
            Assert.That(await wire.CommandAsync("RCPT TO:<alice@hermod.test> ORCPT=utf-8;j\\x{F6}rg@hermod.test"),  Is.EqualTo(250));

        }

    }

}
