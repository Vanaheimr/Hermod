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
    /// RFC 5321 §4.5.1: "the special case of 'RCPT TO:&lt;Postmaster&gt;' (with no domain
    /// specification), MUST be supported" - the local part case-insensitively (§2.4).
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        [TestCase("Postmaster")]
        [TestCase("postmaster")]
        [TestCase("POSTMASTER")]
        public async Task RCPT_TO_Postmaster_without_a_domain_is_this_servers_postmaster(String Postmaster)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"RCPT TO:<{Postmaster}>"),           Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                              Is.EqualTo(354));

            await wire.SendAsync("Subject: test\r\n\r\nbody\r\n.\r\n");
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            Assert.That(server.Storage.Envelopes.Single().To, Is.EqualTo(new[] { "postmaster@mx.hermod.test" }));

        }


        /// <summary>
        /// Only the domainless form is the special case: another domain's postmaster is a relay
        /// recipient, and another domainless name is nobody.
        /// </summary>
        [TestCase("postmaster@elsewhere.example")]
        [TestCase("hostmaster")]
        public async Task Other_recipients_stay_refused_without_authentication(String Recipient)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"RCPT TO:<{Recipient}>"),            Is.EqualTo(550));

        }

    }

}
