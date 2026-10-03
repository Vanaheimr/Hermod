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

using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The per-session and per-failure limits of <see cref="RateLimitConfig"/>.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        // "not a scram message": a SCRAM exchange that fails at once (see the AUTH refusals).
        private const String FailingAuth = "AUTH SCRAM-SHA-256 bm90IGEgc2NyYW0gbWVzc2FnZQ==";


        [Test]
        public async Task MaxRcptPerSession_counts_the_recipients_of_every_transaction()
        {

            await using var server = new Server(RateLimits: new RateLimitConfig { AuthFailDelayMs = 0, MaxRcptPerSession = 2 });
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);                                                  // one recipient
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"),   Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<carol@hermod.test>"), Is.EqualTo(452));

            // A new transaction does not start the count again.
            Assert.That(await wire.CommandAsync("RSET"),                                    Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"),       Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("RCPT TO:<carol@hermod.test>"),             Is.EqualTo(452));

        }


        [Test]
        public async Task MaxMessagesPerSession_ends_the_session_at_the_next_MAIL()
        {

            await using var server = new Server(RateLimits: new RateLimitConfig { AuthFailDelayMs = 0, MaxMessagesPerSession = 1 });
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);
            Assert.That(await wire.CommandAsync("DATA"),                         Is.EqualTo(354));
            Assert.That(await wire.CommandAsync("Subject: one\r\n\r\nbody\r\n."), Is.EqualTo(250));

            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(421));
            Assert.That(await wire.TryReplyAsync(TimeSpan.FromSeconds(2)),           Is.Null, "421 closes the connection");
            Assert.That(server.Storage.Messages,                                     Has.Count.EqualTo(1));

        }


        [Test]
        public async Task A_failed_AUTH_is_answered_after_AuthFailDelayMs()
        {

            await using var server = new Server(RateLimits: new RateLimitConfig { AuthFailDelayMs = 1000 });
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            var stopwatch = Stopwatch.StartNew();
            Assert.That(await wire.CommandAsync(FailingAuth), Is.EqualTo(535));
            stopwatch.Stop();

            Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900)));

            // An unknown mechanism is no failed guess: it is answered at once.
            stopwatch.Restart();
            Assert.That(await wire.CommandAsync("AUTH NO-SUCH-MECHANISM"), Is.EqualTo(504));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(900)));

        }


        [Test]
        public async Task Failed_AUTHs_count_towards_MaxAuthAttemptsPerIpPerHour()
        {

            // 127.0.0.1 is whitelisted by default, which skips this limit.
            await using var server = new Server(RateLimits: new RateLimitConfig {
                                                                AuthFailDelayMs              = 0,
                                                                MaxAuthAttemptsPerIpPerHour  = 2,
                                                                WhitelistedIps               = []
                                                            });
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync(FailingAuth),           Is.EqualTo(535));
            Assert.That(await wire.CommandAsync(FailingAuth),           Is.EqualTo(535));
            Assert.That(await wire.CommandAsync(FailingAuth),           Is.EqualTo(421));

        }

    }

}
