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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 5321 §4.5.3.1: a domain name of 255 octets (§4.5.3.1.2), a path of 256 (§4.5.3.1.3) -
    /// taken, and a longer one refused with 501 (§4.5.3.1.9: "501 Path too long"), before it goes
    /// whole into a reply line of more than 512 octets (§4.5.3.1.5) or a header line of more than
    /// 998 characters (RFC 5322 §2.1.1). Findings S-51 and S-52 of SMTPConformanceTests.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// A domain name of that many octets: labels of 63 octets, ".example" at the end.
        /// </summary>
        private static String DomainOfLength(Int32 Length)
        {

            var labels = Length - ".example".Length;
            var domain = String.Concat(Enumerable.Repeat(new String('d', 63) + ".", labels / 64)) + new String('d', labels % 64) + ".example";

            Assert.That(domain.Length, Is.EqualTo(Length), "the domain the test meant to write");

            return domain;

        }


        [TestCase("EHLO", 255, 250)]
        [TestCase("EHLO", 256, 501)]
        [TestCase("EHLO", 1000, 501)]
        [TestCase("HELO", 255, 250)]
        [TestCase("HELO", 256, 501)]
        [TestCase("HELO", 1000, 501)]
        [Description("S-52 - RFC 5321 §4.5.3.1.2: \"The maximum total length of a domain name or number is 255 octets\" - taken; a longer argument is none, refused with 501, and no reply line is longer than 512 octets (§4.5.3.1.5)")]
        public async Task An_EHLO_name_of_more_than_255_octets_is_refused(String Command, Int32 Length, Int32 Code)
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            var (code, lines)      = await wire.CommandLinesAsync($"{Command} {DomainOfLength(Length)}");

            Assert.Multiple(() => {
                Assert.That(code,                                    Is.EqualTo(Code));
                Assert.That(lines.Select(line => line.Length + 2),   Has.All.LessThanOrEqualTo(512), "RFC 5321 §4.5.3.1.5");
            });

        }


        [Test(Description = "S-52 - RFC 5321 §4.1.4: a refused EHLO greets nobody - MAIL after it is still out of sequence, and an EHLO of the right size goes on")]
        public async Task A_refused_EHLO_greets_nobody()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync($"EHLO {DomainOfLength(256)}"),      Is.EqualTo(501));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(503));
            Assert.That(await wire.CommandAsync("EHLO client.example"),              Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));

        }


        [TestCase("MAIL FROM:", 256, 250)]
        [TestCase("MAIL FROM:", 257, 501)]
        [TestCase("MAIL FROM:", 1000, 501)]
        [TestCase("RCPT TO:",   256, 250)]
        [TestCase("RCPT TO:",   257, 501)]
        [TestCase("RCPT TO:",   1000, 501)]
        [Description("S-51 - RFC 5321 §4.5.3.1.3: \"The maximum total length of a reverse-path or forward-path is 256 octets (including the punctuation and element separators)\" - taken; a longer one refused: §4.5.3.1.9, \"501 Path too long\"")]
        public async Task A_path_of_more_than_256_octets_is_refused(String Command, Int32 Length, Int32 Code)
        {

            await using var server = new Server(MaxMessageSize: 64 * 1024);
            using var wire         = await server.ConnectAsync();

            // RCPT to a local domain: the local part makes the length.
            var path = Command.StartsWith("MAIL")
                           ? $"<sender@{DomainOfLength(Length - "<sender@>".Length)}>"
                           : $"<{new String('r', Length - "<@hermod.test>".Length)}@hermod.test>";

            Assert.That(path.Length, Is.EqualTo(Length), "the path the test meant to write");

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            if (Command.StartsWith("RCPT"))
                Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250));

            var (code, lines) = await wire.CommandLinesAsync(Command + path);

            Assert.That(code, Is.EqualTo(Code), String.Join(" / ", lines));

            if (Code == 501)
                Assert.That(lines[^1], Does.Contain(Command.StartsWith("MAIL") ? "5.1.7" : "5.1.3").And.Contain("Path too long"));

        }


        [Test(Description = "S-51 - RFC 5321 §4.5.3.1: \"specifies lengths in octets\" - a path of 137 characters, 257 octets in UTF-8 (RFC 6531), is longer than 256; one of 77 characters, 137 octets, is taken")]
        public async Task A_path_is_measured_in_octets()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            // UTF-8 on the wire (RFC 6531 §3.3) - the wire's commands are ASCII.
            await wire.SendAsync(Encoding.UTF8.GetBytes($"MAIL FROM:<{new String('ä', 120)}@client.example> SMTPUTF8\r\n"));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(501));

            await wire.SendAsync(Encoding.UTF8.GetBytes($"MAIL FROM:<{new String('ä', 60)}@client.example> SMTPUTF8\r\n"));
            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(250));

        }


        [Test(Description = "S-51 - RFC 5322 §2.1.1: \"Each line of characters MUST be no more than 998 characters\" - an EHLO name of 255 octets and paths of 256 make a message with no longer line, the Received field and the Return-Path included")]
        public async Task The_longest_names_and_paths_make_no_header_line_of_more_than_998_characters()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            var ehlo     = DomainOfLength(255);
            var mailFrom = $"<sender@{DomainOfLength(256 - "<sender@>".Length)}>";
            var rcptTo   = $"<{new String('r', 256 - "<@hermod.test>".Length)}@hermod.test>";

            Assert.That(await wire.CommandAsync($"EHLO {ehlo}"),           Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"MAIL FROM:{mailFrom}"),  Is.EqualTo(250));
            Assert.That(await wire.CommandAsync($"RCPT TO:{rcptTo}"),      Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                    Is.EqualTo(354));
            Assert.That(await wire.CommandAsync("Subject: sizes\r\n\r\nbody\r\n."), Is.EqualTo(250));

            var message = server.Storage.Messages.Single();

            Assert.Multiple(() => {
                Assert.That(message,                                         Does.Contain($"from {ehlo} ").And.Contain($"for {rcptTo}"));
                Assert.That(message.Split("\r\n").Max(line => line.Length),  Is.LessThanOrEqualTo(998));
            });

        }

    }

}
