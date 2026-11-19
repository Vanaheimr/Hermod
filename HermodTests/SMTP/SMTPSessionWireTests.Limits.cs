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
using System.Net.Sockets;
using System.Text;

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


        /// <summary>
        /// True when the server closes the connection within the window, without another
        /// reply - a read that times out is a connection still open.
        /// </summary>
        private static async Task<Boolean> ClosedAsync(Wire Wire, TimeSpan? Window = null)
        {

            var window    = Window ?? TimeSpan.FromSeconds(5);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                return await Wire.TryReplyAsync(window) is null && stopwatch.Elapsed < window;
            }
            catch (IOException)
            {
                return true;    // reset by the server
            }

        }


        [Test(Description = "RFC 5321 §4.5.3.1.8: \"The minimum total number of recipients that MUST be buffered is 100 recipients\" - for every message, the second in a session too")]
        public async Task A_second_message_in_the_session_takes_its_own_recipients()
        {

            // The server's default limits.
            await using var server = new Server();
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));

            foreach (var message in new[] { "b", "c" })
            {

                Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(250), $"MAIL of message {message}");

                for (var i = 1; i <= 60; i++)
                    Assert.That(await wire.CommandAsync($"RCPT TO:<alice+{message}{i}@hermod.test>"), Is.EqualTo(250), $"recipient {i} of message {message}");

                Assert.That(await wire.CommandAsync("RSET"), Is.EqualTo(250));

            }

        }


        [Test(Description = "RFC 5321 §4.5.3.1.8: a client sends a larger message \"in 100-recipient chunks\" - the session's recipient limit ends the session at the next MAIL with 421, it never cuts a message short")]
        public async Task MaxRcptPerSession_ends_the_session_at_the_next_MAIL()
        {

            await using var server = new Server(RateLimits: new RateLimitConfig { AuthFailDelayMs = 0, MaxRcptPerSession = 2 });
            using var wire         = await server.ConnectAsync();

            await OpenTransaction(wire);                                                  // one recipient
            Assert.That(await wire.CommandAsync("RCPT TO:<bob@hermod.test>"),   Is.EqualTo(250));

            // Past the session's limit - but this message is not cut short.
            Assert.That(await wire.CommandAsync("RCPT TO:<carol@hermod.test>"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync("DATA"),                         Is.EqualTo(354));
            Assert.That(await wire.CommandAsync("Subject: one\r\n\r\nbody\r\n."), Is.EqualTo(250));

            // The next transaction is not started.
            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(421));
            Assert.That(await ClosedAsync(wire),                                      Is.True, "421 closes the connection");
            Assert.That(server.Storage.Envelopes.Single().To,                        Has.Count.EqualTo(3));

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


        [Test(Description = "RFC 5321 §4.2.2: 421 is \"Service not available, closing transmission channel\" - after a 421 to AUTH the server closes the connection")]
        public async Task After_421_to_AUTH_the_connection_is_closed()
        {

            await using var server = new Server(RateLimits: new RateLimitConfig {
                                                                AuthFailDelayMs              = 0,
                                                                MaxAuthAttemptsPerIpPerHour  = 1,
                                                                WhitelistedIps               = []
                                                            });
            using var wire         = await server.ConnectAsync();

            Assert.That(await wire.CommandAsync("EHLO client.example"), Is.EqualTo(250));
            Assert.That(await wire.CommandAsync(FailingAuth),           Is.EqualTo(535));
            Assert.That(await wire.CommandAsync(FailingAuth),           Is.EqualTo(421));
            Assert.That(await ClosedAsync(wire),                        Is.True, "421 closes the connection");

        }


        #region Connections refused at accept

        /// <summary>
        /// Every octet a fresh connection to the server receives until the server closes it,
        /// or 5 s pass.
        /// </summary>
        private static async Task<Byte[]> RefusalAsync(Server Server)
        {

            using var tcp  = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", Server.Port);

            var stream     = tcp.GetStream();
            var received   = new MemoryStream();
            var buffer     = new Byte[1024];
            using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                Int32 read;
                while ((read = await stream.ReadAsync(buffer, cts.Token)) > 0)
                    received.Write(buffer, 0, read);
            }
            catch (Exception e) when (e is OperationCanceledException or IOException)
            { }

            return received.ToArray();

        }


        [Test(Description = "RFC 5321 §2.3.8: lines are \"terminated by the sequence ASCII character \"CR\" (hex 0D) followed immediately by ASCII character \"LF\" (hex 0A)\" - the 421 of a connection refused at accept too, on every platform")]
        public async Task The_421_of_a_refused_connection_ends_in_CRLF()
        {

            await using var server = new Server(RateLimits: new RateLimitConfig {
                                                                AuthFailDelayMs              = 0,
                                                                MaxConnectionsPerIp          = 1,
                                                                WhitelistedIps               = []
                                                            });
            using var first        = await server.ConnectAsync();

            var refusal            = await RefusalAsync(server);
            var text               = Encoding.ASCII.GetString(refusal);

            Assert.That(text,                                    Does.StartWith("421 4.7.0 "));
            Assert.That(refusal[^2..],                           Is.EqualTo("\r\n"u8.ToArray()), text.Replace("\r", "<CR>").Replace("\n", "<LF>"));
            Assert.That(text.Replace("\r\n", "").Contains('\n'), Is.False,                       "a bare LF");

        }


        private static Server BlacklistingServer()

            => new (RateLimits: new RateLimitConfig {
                                    AuthFailDelayMs  = 0,
                                    WhitelistedIps   = [],
                                    BlacklistedIps   = [ "127.0.0.1" ]
                                });


        [Test(Description = "RFC 5321 §3.1: after a 554 greeting the server \"MUST still wait for the client to send a QUIT ... before closing the connection and SHOULD respond to any intervening commands with \"503 bad sequence of commands\"\"")]
        public async Task After_554_the_server_waits_for_QUIT()
        {

            await using var server = BlacklistingServer();
            using var wire         = new Wire(new TcpClient("127.0.0.1", server.Port));

            var greeting           = await wire.ReplyAsync();
            Assert.That(greeting.Code,                                  Is.EqualTo(554));

            await wire.SendAsync("EHLO client.example\r\n");
            var ehlo               = await wire.ReplyAsync();
            Assert.That(ehlo.Code,                                      Is.EqualTo(503));
            Assert.That(ehlo.Text,                                      Does.StartWith("5.5.1 "));

            Assert.That(await wire.CommandAsync("MAIL FROM:<sender@client.example>"), Is.EqualTo(503));
            Assert.That(await wire.CommandAsync("QUIT"),                Is.EqualTo(221));
            Assert.That(await ClosedAsync(wire),                        Is.True, "QUIT closes the connection");

        }


        [Test(Description = "RFC 5321 §3.1: a client refused with 554 is waited for - but not forever: ten commands other than QUIT, then the connection is closed")]
        public async Task A_client_refused_with_554_is_answered_ten_commands_only()
        {

            await using var server = BlacklistingServer();
            using var wire         = new Wire(new TcpClient("127.0.0.1", server.Port));

            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(554));

            for (var i = 1; i <= 10; i++)
                Assert.That(await wire.CommandAsync("NOOP"), Is.EqualTo(503), $"command {i}");

            Assert.That(await ClosedAsync(wire), Is.True, "closed after the tenth");

        }


        [Test(Description = "RFC 5321 §4.2.2: 421 is \"closing transmission channel\" - a connection refused at accept with 421 is closed at once, not held like one refused with 554")]
        public async Task A_connection_refused_with_421_is_closed_at_once()
        {

            await using var server = new Server(RateLimits: new RateLimitConfig {
                                                                AuthFailDelayMs              = 0,
                                                                MaxConnectionsPerIp          = 1,
                                                                WhitelistedIps               = []
                                                            });
            using var first        = await server.ConnectAsync();
            using var wire         = new Wire(new TcpClient("127.0.0.1", server.Port));

            Assert.That((await wire.ReplyAsync()).Code, Is.EqualTo(421));
            Assert.That(await ClosedAsync(wire),        Is.True, "421 closes the connection");

        }

        #endregion

    }

}
