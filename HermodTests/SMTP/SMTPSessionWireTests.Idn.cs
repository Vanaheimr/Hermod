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

using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// Internationalized domain names at the server (RFC 6531). §3.7: a client writes a domain in
    /// U-labels in an SMTPUTF8 transaction and in A-labels outside one - both are the same local
    /// domain. §3.7.1: the server's own name in its greeting and its reply to EHLO is in LDH
    /// labels or A-labels.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        private static Func<SMTPServerConfig, SMTPServerConfig> LocalDomain(String Domain)
            => config => config with { LocalDomains = [ Domain ] };


        [TestCase("xn--bcher-kva.test", "alice@bücher.test",        true)]
        [TestCase("xn--bcher-kva.test", "alice@xn--bcher-kva.test", false)]
        [TestCase("bücher.test",        "alice@xn--bcher-kva.test", false)]
        [TestCase("bücher.test",        "alice@bücher.test",        true)]
        [TestCase("Bücher.TEST",        "alice@XN--BCHER-KVA.test", false)]
        public async Task A_recipient_of_a_local_domain_in_either_form_is_local(String Local, String Recipient, Boolean SmtpUtf8)
        {

            await using var server = new Server(Configure: LocalDomain(Local));
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            await wire.SendAsync(Encoding.UTF8.GetBytes($"MAIL FROM:<sender@client.example>{(SmtpUtf8 ? " SMTPUTF8" : "")}\r\n"));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            await wire.SendAsync(Encoding.UTF8.GetBytes($"RCPT TO:<{Recipient}>\r\n"));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250), "a local recipient, not one to relay");

        }


        [Test]
        public async Task A_recipient_of_another_domain_is_still_no_local_one()
        {

            await using var server = new Server(Configure: LocalDomain("xn--bcher-kva.test"));
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            await wire.SendAsync(Encoding.UTF8.GetBytes("MAIL FROM:<sender@client.example> SMTPUTF8\r\n"));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

            await wire.SendAsync(Encoding.UTF8.GetBytes("RCPT TO:<alice@bucher.test>\r\n"));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(550));

        }


        [Test]
        public async Task A_server_named_in_U_labels_names_itself_in_A_labels()
        {

            await using var server = new Server(Configure: config => config with { Hostname = "mx.bücher.test" });

            // Once the server answers, a connection of our own: its greeting is what is tested.
            using (await server.ConnectAsync()) { }
            using var wire  = new Wire(new TcpClient("127.0.0.1", server.Port.ToInt32()));

            var greeting    = await wire.ReplyAsync();
            var ehlo        = await wire.CommandLinesAsync("EHLO client.example");

            Assert.That(greeting.Code,  Is.EqualTo(220));
            Assert.That(greeting.Text,  Does.StartWith("mx.xn--bcher-kva.test "));
            Assert.That(ehlo.Code,      Is.EqualTo(250));
            Assert.That(ehlo.Lines[0],  Does.StartWith("mx.xn--bcher-kva.test "));

        }

    }

}
