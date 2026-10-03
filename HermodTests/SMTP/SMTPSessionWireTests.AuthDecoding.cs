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
    /// RFC 4954 §4: "If the server cannot [BASE64] decode any client response, it MUST reject
    /// the AUTH command with a 501 reply (and an enhanced status code of 5.5.2)." A response
    /// that decodes but authenticates nobody stays a 535.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// Each case: the lines to send after EHLO inside TLS, and the reply expected to the last.
        /// "Zm9v" is "foo" in base64; "!!!" is not base64 at all.
        /// </summary>
        public static IEnumerable<TestCaseData> AuthResponses()
        {
            TestCaseData Case(String name, Int32 code, String enhanced, params String[] lines)
                => new TestCaseData(lines, code, enhanced).SetName(name);

            yield return Case("PLAIN with an undecodable initial response is 501 5.5.2",   501, "5.5.2", "AUTH PLAIN !!!");
            yield return Case("PLAIN with an undecodable response is 501 5.5.2",           501, "5.5.2", "AUTH PLAIN", "!!!");
            yield return Case("LOGIN with an undecodable user name is 501 5.5.2",          501, "5.5.2", "AUTH LOGIN", "!!!");
            yield return Case("LOGIN with an undecodable password is 501 5.5.2",           501, "5.5.2", "AUTH LOGIN", "Zm9v", "!!!");
            yield return Case("SCRAM with an undecodable client-first is 501 5.5.2",       501, "5.5.2", "AUTH SCRAM-SHA-256 !!!");

            // "=" is the empty response (RFC 4954 §4), not an undecodable one: an empty PLAIN
            // message authenticates nobody, so it is 535.
            yield return Case("PLAIN with \"=\" is the empty response and 535 5.7.8",     535, "5.7.8", "AUTH PLAIN =");
            yield return Case("PLAIN that decodes to no valid user is 535 5.7.8",          535, "5.7.8", "AUTH PLAIN " + Convert.ToBase64String("\0nobody\0nothing"u8.ToArray()));
        }


        [TestCaseSource(nameof(AuthResponses))]
        public async Task An_AUTH_response_that_does_not_decode_is_501(String[] Lines, Int32 Code, String EnhancedCode)
        {

            await using var server = new Server(Configure: WithServerCertificate);
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("STARTTLS"),            Is.EqualTo(220));
            await wire.StartTlsAsync();
            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            foreach (var line in Lines[..^1])
                Assert.That(await wire.CommandAsync(line), Is.EqualTo(334), $"\"{line}\" continues the exchange");

            await wire.SendAsync(Lines[^1] + "\r\n");
            var reply = await wire.ReplyAsync();

            Assert.That(reply.Code, Is.EqualTo(Code));
            Assert.That(reply.Text, Does.StartWith(EnhancedCode + " "), $"reply text was \"{reply.Text}\"");

            // The exchange is over and the session goes on.
            Assert.That(await wire.CommandAsync("NOOP"), Is.EqualTo(250));

        }

    }

}
