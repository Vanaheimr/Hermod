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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// What the EHLO reply offers is read keyword by keyword (RFC 5321 §4.1.1.1), not as
    /// substrings; and HELO is the fallback only for a server that does not know EHLO (§3.2).
    /// </summary>
    public partial class SMTPOutboundClientTests
    {

        private static readonly DsnParameters SomeDsn = new (DsnNotify.Failure | DsnNotify.Delay, DsnRet.Hdrs, "env-4711");


        [Test]
        public async Task A_next_hop_named_dsn_does_not_get_DSN_parameters()
        {

            using var nextHop = new NextHop(line => line.StartsWith("EHLO")
                                                        ? "250-dsn.next.hop Hello\r\n250-PIPELINING\r\n250-8BITMIME\r\n250 ENHANCEDSTATUSCODES"
                                                        : null);

            var result = await Send(nextHop, Dsn: SomeDsn);

            Assert.That(result.Status,    Is.EqualTo(SendStatus.Success), result.ResponseText);
            Assert.That(nextHop.Commands, Has.None.Contains("RET=").And.None.Contains("ENVID=").And.None.Contains("NOTIFY="));

        }


        [Test]
        public async Task A_lower_case_dsn_keyword_is_DSN()
        {

            using var nextHop = new NextHop(line => line.StartsWith("EHLO")
                                                        ? "250-next.hop Hello\r\n250-8bitmime\r\n250 dsn"
                                                        : null);

            await Send(nextHop, Dsn: SomeDsn);

            Assert.That(nextHop.Commands, Has.Some.StartsWith("MAIL").And.Contains("ENVID=env-4711"));

        }


        [Test]
        public async Task A_421_to_EHLO_ends_the_attempt_without_HELO()
        {

            using var nextHop = new NextHop(line => line.StartsWith("EHLO") ? "421 4.3.2 Service shutting down" : null);

            var result = await Send(nextHop);

            Assert.That(result.Status,    Is.EqualTo(SendStatus.TempFail));
            Assert.That(nextHop.Commands, Has.None.StartsWith("HELO").And.None.StartsWith("MAIL"));

        }


        [TestCase(500)]
        [TestCase(502)]
        [TestCase(550)]
        public async Task A_server_without_EHLO_gets_HELO(Int32 Code)
        {

            using var nextHop = new NextHop(line => line.StartsWith("EHLO") ? $"{Code} 5.5.1 What?" : null);

            var result = await Send(nextHop);

            Assert.That(result.Status,    Is.EqualTo(SendStatus.Success), result.ResponseText);
            Assert.That(nextHop.Commands, Has.Some.StartsWith("HELO"));

        }


        [Test]
        public void Extensions_are_keywords_of_the_lines_after_the_first()
        {

            var extensions = SMTPOutboundClient.Extensions(new SMTPReply([
                                 "250-dsn.starttls.example Hello",
                                 "250-SIZE 10485760",
                                 "250-auth PLAIN LOGIN",
                                 "250-AUTH=CRAM-MD5",
                                 "250 X-MENTIONS-DSN"
                             ]));

            Assert.That(extensions.Keys,     Is.EquivalentTo(new[] { "SIZE", "AUTH", "X-MENTIONS-DSN" }));
            Assert.That(extensions["SIZE"],  Is.EqualTo("10485760"));
            Assert.That(extensions["AUTH"],  Is.EqualTo("PLAIN LOGIN CRAM-MD5"));

        }

    }

}
