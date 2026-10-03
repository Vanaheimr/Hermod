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
    /// RFC 2034 §4: once ENHANCEDSTATUSCODES is advertised, "the text part of all 2xx, 4xx,
    /// and 5xx SMTP responses other than the initial greeting and any response to HELO or EHLO
    /// are prefaced with a status code" - of the reply's class (RFC 3463 §2).
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// Each case: the lines that set the scene, the line whose reply is checked, and that
        /// reply's code and enhanced code. A "STARTTLS" answered 220 in the scene is followed by
        /// the TLS handshake.
        /// </summary>
        public static IEnumerable<TestCaseData> RepliesWithEnhancedCodes()
        {
            TestCaseData Case(String name, String[] setup, String command, Int32 code, String enhanced)
                => new TestCaseData(setup, command, code, enhanced).SetName(name);

            String[] ehlo    = [ "EHLO client.example" ];
            String[] inTls   = [ "EHLO client.example", "STARTTLS", "EHLO client.example" ];

            yield return Case("NOOP is 250 2.0.0",                          ehlo,              "NOOP",                  250, "2.0.0");
            yield return Case("RSET is 250 2.0.0",                          ehlo,              "RSET",                  250, "2.0.0");
            yield return Case("VRFY is 252 2.0.0",                          ehlo,              "VRFY alice",            252, "2.0.0");
            yield return Case("An unknown command is 500 5.5.1",            ehlo,              "FROBNICATE",            500, "5.5.1");
            yield return Case("QUIT is 221 2.0.0",                          ehlo,              "QUIT",                  221, "2.0.0");
            yield return Case("MAIL before EHLO is 503 5.5.1",              [],                "MAIL FROM:<s@c.example>", 503, "5.5.1");
            yield return Case("AUTH before EHLO is 503 5.5.1",              [],                "AUTH PLAIN",            503, "5.5.1");
            yield return Case("AUTH without a mechanism is 501 5.5.4",      ehlo,              "AUTH",                  501, "5.5.4");
            yield return Case("A cancelled AUTH is 501 5.7.0",              [ ..inTls, "AUTH LOGIN" ], "*",             501, "5.7.0");
            yield return Case("STARTTLS is 220 2.0.0",                      ehlo,              "STARTTLS",              220, "2.0.0");
            yield return Case("STARTTLS inside TLS is 503 5.5.1",           inTls,             "STARTTLS",              503, "5.5.1");
        }


        [TestCaseSource(nameof(RepliesWithEnhancedCodes))]
        public async Task Every_reply_carries_an_enhanced_code(String[] Setup, String Command, Int32 Code, String EnhancedCode)
        {

            await using var server = new Server(Configure: WithServerCertificate);
            using var wire         = await server.ConnectAsync();

            foreach (var line in Setup)
            {
                var code = await wire.CommandAsync(line);
                if (line == "STARTTLS" && code == 220)
                    await wire.StartTlsAsync();
            }

            await wire.SendAsync(Command + "\r\n");
            var reply = await wire.ReplyAsync();

            Assert.That(reply.Code, Is.EqualTo(Code));
            Assert.That(reply.Text, Does.StartWith(EnhancedCode + " "), $"reply text was \"{reply.Text}\"");

        }


        [Test]
        public async Task A_second_AUTH_is_503_5_5_1()
        {

            await using var server = ServerWithSubmissionUser();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            await AuthenticateWithScram(wire);

            await wire.SendAsync("AUTH SCRAM-SHA-256\r\n");
            var reply = await wire.ReplyAsync();

            Assert.That(reply.Code, Is.EqualTo(503));
            Assert.That(reply.Text, Does.StartWith("5.5.1 "), $"reply text was \"{reply.Text}\"");

        }


        [Test]
        public async Task STARTTLS_without_a_certificate_is_454_4_7_0()
        {

            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            await wire.SendAsync("STARTTLS\r\n");
            var reply = await wire.ReplyAsync();

            Assert.That(reply.Code, Is.EqualTo(454));
            Assert.That(reply.Text, Does.StartWith("4.7.0 "), $"reply text was \"{reply.Text}\"");

        }

    }

}
