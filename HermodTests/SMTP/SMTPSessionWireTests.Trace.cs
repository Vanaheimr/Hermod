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
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// What the server writes on top of a message (RFC 5321 §4.4) - the Return-Path at final
    /// delivery, the Received field from what the connection says - and the loops it stops
    /// (§6.3).
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        #region Helpers

        /// <summary>
        /// One message from sender@client.example (or the given reverse-path) to the recipient,
        /// after the given EHLO; the reply to the end of data.
        /// </summary>
        private static async Task<(Int32 Code, String Text)> SendTraced(Server   Server,
                                                                         String   Message,
                                                                         String   Recipient  = "alice@hermod.test",
                                                                         String   From       = "sender@client.example",
                                                                         String   Ehlo       = "client.example")
        {

            using var wire = await Server.ConnectAsync();

            Assert.That(await wire.CommandAsync($"EHLO {Ehlo}"),           Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"MAIL FROM:<{From}>"),    Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"RCPT TO:<{Recipient}>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                   Is.EqualTo(354));

            await wire.SendAsync(Message + ".\r\n");

            return await wire.ReplyAsync();

        }

        /// <summary>
        /// A message that says it has been through so many hops.
        /// </summary>
        private static String WithHops(Int32 Hops)
        {

            var message = new StringBuilder();

            for (var hop = Hops; hop > 0; hop--)
                message.Append($"Received: from hop{hop - 1}.example by hop{hop}.example; Sun, 22 Nov 2026 06:00:00 +0000\r\n");

            return message + "From: <sender@client.example>\r\nSubject: hops\r\n\r\naround\r\n";

        }

        #endregion


        #region Loops (RFC 5321 §6.3)

        [Test(Description = "RFC 5321 §6.3: \"servers MUST contain provisions for detecting and stopping trivial loops\" - a message with more Received fields than MaxReceivedFields is refused with 5.4.6, and neither stored nor queued")]
        public async Task A_message_with_more_than_100_Received_fields_is_a_loop()
        {

            await using var server = RelayingServer();

            var local   = await SendTraced(server, WithHops(101));
            var relay   = await SendTraced(server, WithHops(101), Recipient: "bob@remote.example");
            var waiting = await server.Queue!.GetWaitingAsync();

            Assert.Multiple(() => {
                Assert.That(local.Code,               Is.EqualTo(554));
                Assert.That(local.Text,               Does.StartWith("5.4.6 "));
                Assert.That(relay.Code,               Is.EqualTo(554));
                Assert.That(server.Storage.Messages,  Is.Empty);
                Assert.That(waiting,                  Is.Empty);
            });

        }


        [Test(Description = "RFC 5321 §6.3: \"a large rejection threshold, normally at least 100 Received entries\" - 100 are delivered, and the threshold is the configuration's")]
        public async Task A_message_with_100_Received_fields_is_delivered_and_the_limit_is_configurable()
        {

            await using var server  = new Server();
            await using var strict  = new Server(Configure: config => config with { MaxReceivedFields = 10 });

            var hundred = await SendTraced(server, WithHops(100));
            var ten     = await SendTraced(strict, WithHops(10));
            var eleven  = await SendTraced(strict, WithHops(11));

            Assert.Multiple(() => {
                Assert.That(hundred.Code, Is.EqualTo(250));
                Assert.That(ten.Code,     Is.EqualTo(250));
                Assert.That(eleven.Code,  Is.EqualTo(554));
            });

        }

        #endregion


        #region Return-Path (RFC 5321 §4.4)

        [TestCase("sender@client.example", "Return-Path: <sender@client.example>\r\n")]
        [TestCase("",                      "Return-Path: <>\r\n")]
        [Test(Description = "RFC 5321 §4.4: \"When the delivery SMTP server makes the \"final delivery\" of a message, it inserts a return-path line at the beginning of the mail data. This use of return-path is required\" - Return-path-line = \"Return-Path:\" FWS Reverse-path")]
        public async Task A_delivered_message_begins_with_its_Return_Path(String From, String ReturnPath)
        {

            await using var server = new Server();

            Assert.That((await SendTraced(server, "Subject: test\r\n\r\nbody\r\n", From: From)).Code, Is.EqualTo(250));

            var stored = server.Storage.Messages.Single();

            Assert.Multiple(() => {
                Assert.That(stored, Does.StartWith(ReturnPath),                       "the first line");
                Assert.That(stored, Does.Contain("\r\nReceived: from client.example "), "the trace of this hop below it");
            });

        }


        [Test(Description = "RFC 5321 §4.4: \"exactly one return path SHOULD be present when the message is delivered\"; \"SMTP servers making final delivery MAY remove Return-path header fields before adding their own\" - the client's go, folded or not, and nothing else does")]
        public async Task The_Return_Path_fields_of_the_message_are_replaced()
        {

            await using var server = new Server();

            var message = "Return-Path: <forged@evil.example>\r\n" +
                          "Subject: test\r\n" +
                          "return-path :\r\n <second@evil.example>\r\n" +
                          "X-Kept: Return-Path: <in a value>\r\n" +
                          "\r\n" +
                          "Return-Path: <in the body>\r\n";

            Assert.That((await SendTraced(server, message)).Code, Is.EqualTo(250));

            var stored = server.Storage.Messages.Single();

            Assert.Multiple(() => {
                Assert.That(Regex.Count(stored, "^return-path", RegexOptions.Multiline | RegexOptions.IgnoreCase),
                            Is.EqualTo(2), "the one of the delivery, and the one in the body");
                Assert.That(stored, Does.StartWith("Return-Path: <sender@client.example>\r\n"));
                Assert.That(stored, Does.Not.Contain("evil.example"));
                Assert.That(stored, Does.Contain("\r\nSubject: test\r\nX-Kept: Return-Path: <in a value>\r\n\r\nReturn-Path: <in the body>\r\n"));
            });

        }


        [Test(Description = "RFC 5321 §4.4: \"SMTP servers performing a relay function MUST NOT inspect the message data, and especially not to the extent needed to determine if Return-path header fields are present\" - a relayed message gets no Return-Path, and keeps what it had")]
        public async Task A_relayed_message_gets_no_Return_Path_and_keeps_its_own()
        {

            await using var server = RelayingServer();

            Assert.That((await SendTraced(server, "Return-Path: <kept@client.example>\r\nSubject: test\r\n\r\nbody\r\n", Recipient: "bob@remote.example")).Code, Is.EqualTo(250));

            var queued = (await server.Queue!.GetWaitingAsync()).Single();

            Assert.Multiple(() => {
                Assert.That(queued.MessageContent, Does.Not.StartWith("Return-Path"));
                Assert.That(queued.MessageContent, Does.Contain("\r\nReturn-Path: <kept@client.example>\r\n"));
            });

        }

        #endregion


        #region Received from what the connection says (RFC 5321 §4.4, §4.1.1.1)

        [TestCase("EHLO client.example (mail.trusted.example [203.0.113.5])")]
        [TestCase("EHLO mail.trusted.example by mx.trusted.example")]
        [TestCase("EHLO client.example;")]
        [TestCase("EHLO (comment)")]
        [TestCase("HELO client.example (mail.trusted.example [203.0.113.5])")]
        [TestCase("EHLO [192.0.2.1")]
        [Test(Description = "RFC 5321 §4.1.1.1: ehlo = \"EHLO\" SP ( Domain / address-literal ) CRLF; §4.1.4: \"If the EHLO command is not acceptable to the SMTP server, 501, 500, 502, or 550 failure replies MUST be returned\" - what would write Received syntax of the client's (§4.4) is refused, and the session goes on")]
        public async Task A_HELO_or_EHLO_argument_with_syntax_of_its_own_is_refused(String Command)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync(Command),                            Is.EqualTo(501));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(503), "no greeting was taken");
            Assert.That(await wire.CommandAsync("EHLO client.example"),               Is.EqualTo(250));

        }


        [TestCase("client.example",      "Received: from client.example (unknown [127.0.0.1])")]
        [TestCase("client.example.",     "Received: from [127.0.0.1] (unknown [127.0.0.1]) (helo=client.example.)")]
        [TestCase("localhost",           "Received: from localhost (unknown [127.0.0.1])")]
        [TestCase("[127.0.0.1]",         "Received: from [127.0.0.1] (unknown [127.0.0.1])")]
        [TestCase("[IPv6:2001:db8::1]",  "Received: from [IPv6:2001:db8::1] (unknown [127.0.0.1])")]
        [TestCase("DESKTOP_7",           "Received: from [127.0.0.1] (unknown [127.0.0.1]) (helo=DESKTOP_7)")]
        [TestCase(" client.example ",    "Received: from client.example (unknown [127.0.0.1])")]
        [Test(Description = "RFC 5321 §4.4: Extended-Domain = Domain / ( Domain FWS \"(\" TCP-info \")\" ) / ( address-literal FWS \"(\" TCP-info \")\" ), TCP-info \"derived by server from TCP connection not client EHLO\" - a name or literal goes in front, a name that is no Domain (an underscore, a final dot) is taken and kept as a comment")]
        public async Task The_Received_field_begins_with_a_Domain_or_an_address_literal(String Ehlo, String Received)
        {

            await using var server = new Server();

            Assert.That((await SendTraced(server, "Subject: test\r\n\r\nbody\r\n", Ehlo: Ehlo)).Code, Is.EqualTo(250));

            var stored = server.Storage.Messages.Single();

            Assert.That(stored, Does.Contain("\r\n" + Received + "\r\n"));

        }

        #endregion

    }

}
