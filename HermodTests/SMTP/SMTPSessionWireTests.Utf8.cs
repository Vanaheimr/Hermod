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
    /// SMTPUTF8 envelopes (RFC 6531 §3.3): MAIL and RCPT addresses are UTF-8 on the wire,
    /// and must arrive in storage as the strings they are.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [Test]
        public async Task UTF8_envelope_addresses_reach_storage_intact()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            await wire.SendAsync(Encoding.UTF8.GetBytes("MAIL FROM:<jöran@bücher.example> SMTPUTF8\r\n"));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            await wire.SendAsync(Encoding.UTF8.GetBytes("RCPT TO:<ümlaut@hermod.test>\r\n"));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(await wire.CommandAsync("DATA"), Is.EqualTo(354));
            await wire.SendAsync(Encoding.UTF8.GetBytes("Subject: Grüße\r\n\r\nHallo\r\n.\r\n"));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            var (from, to) = server.Storage.Envelopes.Single();

            Assert.That(from, Is.EqualTo("jöran@bücher.example"));
            Assert.That(to,   Is.EqualTo(new[] { "ümlaut@hermod.test" }));

            // The trace field names the recipient as well (RFC 5321 §4.4 "for" clause).
            Assert.That(server.Storage.Messages.Single(), Does.Contain("for <ümlaut@hermod.test>"));

        }


        [Test]
        public async Task A_command_that_is_not_UTF8_is_an_invalid_command()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            // 0xFF never occurs in UTF-8.
            await wire.SendAsync([ .. "MAIL FROM:<"u8.ToArray(), 0xFF, .. "@client.example>\r\n"u8.ToArray() ]);

            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(500));
            Assert.That(await wire.CommandAsync("NOOP"), Is.EqualTo(250), "the session goes on");

        }

    }

}
