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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The relay side, <see cref="SMTPOutboundClient"/>, against a scripted next hop set as its
    /// smart host: replies of more than one line (RFC 5321 §4.2.1), timeouts (§4.5.3.2).
    /// </summary>
    [TestFixture]
    public partial class SMTPOutboundClientTests
    {

        #region Test doubles

        private sealed class NoDNS : IDNSClient
        {

            private static readonly DNSServerConfig origin = new (IPv4Address.Localhost, IPPort.Parse(53));

            private static Task<DNSInfo> Empty()
                => Task.FromResult(new DNSInfo(origin, 0, true, false, true, false, DNSResponseCodes.NoError,
                                               [], [], [], true, false, TimeSpan.FromSeconds(1), TimeSpan.Zero));

            public Task<DNSInfo> Query(DomainName DomainName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Empty();

            public Task<DNSInfo> Query(DNSServiceName DNSServiceName, IEnumerable<DNSResourceRecordTypes> ResourceRecordTypes, TimeSpan? Timeout = null,
                                       Boolean? RecursionDesired = true, Boolean? ForceUpdate = false, CancellationToken CancellationToken = default)
                => Empty();

            public void Dispose() { }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }


        /// <summary>
        /// A next hop with scripted replies. A reply may have several lines ("\r\n" inside it).
        /// It does not close after QUIT, so that it can see whether the client does.
        /// </summary>
        private sealed class NextHop : IDisposable
        {

            private readonly TcpListener              listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly Func<String, String?>    reply;
            private readonly String                   greeting;
            private readonly String[]                 extensions;
            private readonly Boolean                  silent;

            public readonly ConcurrentQueue<String>   Commands  = new();
            public readonly ConcurrentQueue<String>   DataLines = new();
            public volatile Boolean                   ClosedByClient;

            public UInt16 Port
                => (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            /// <param name="Reply">The reply to a command line (or "." for the end of data), or null for the default.</param>
            /// <param name="Greeting">The greeting.</param>
            /// <param name="Extensions">The EHLO keywords.</param>
            /// <param name="Silent">Accept the connection and say nothing at all.</param>
            public NextHop(Func<String, String?>?  Reply       = null,
                           String                  Greeting    = "220 next.hop ESMTP",
                           String[]?               Extensions  = null,
                           Boolean                 Silent      = false)
            {
                reply       = line => Reply?.Invoke(line) ?? Default(line);
                greeting    = Greeting;
                extensions  = Extensions ?? [ "PIPELINING", "8BITMIME", "SIZE 10485760", "DSN", "SMTPUTF8", "ENHANCEDSTATUSCODES" ];
                silent      = Silent;
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private String Default(String Line)
                => Line.Split(' ')[0].ToUpperInvariant() switch {
                       "EHLO"  => String.Join("\r\n", new[] { "next.hop" }.Concat(extensions).Select((line, i) => (i == extensions.Length ? "250 " : "250-") + line)),
                       "HELO"  => "250 next.hop",
                       "MAIL"  => "250 2.1.0 ok",
                       "RCPT"  => "250 2.1.5 ok",
                       "DATA"  => "354 go ahead",
                       "."     => "250 2.0.0 queued",
                       "QUIT"  => "221 2.0.0 bye",
                       _       => "250 2.0.0 ok"
                   };

            private async Task ServeAsync()
            {
                try
                {

                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    if (silent)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        return;
                    }

                    Task Write(String Text)
                        => stream.WriteAsync(Encoding.UTF8.GetBytes(Text + "\r\n")).AsTask();

                    await Write(greeting);

                    var inData = false;

                    while (true)
                    {

                        var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                        if (line is null)
                        {
                            ClosedByClient = true;
                            return;
                        }

                        if (inData)
                        {
                            if (line == ".")
                            {
                                inData = false;
                                await Write(reply("."));
                            }
                            else
                                DataLines.Enqueue(line);
                            continue;
                        }

                        Commands.Enqueue(line);

                        var answer = reply(line);
                        if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase) && answer.StartsWith('3'))
                            inData = true;

                        await Write(answer);

                    }

                }
                catch
                {
                    // the client hung up
                }
            }

            public void Dispose()
                => listener.Stop();

        }


        private static SMTPOutboundClient ClientFor(NextHop NextHop, UInt32 ReadTimeoutMs = 5_000)

            => new (new SmtpOutboundConfig {
                        LocalHostname     = "relay.hermod.test",
                        SmartHost         = "127.0.0.1",
                        SmartHostPort     = NextHop.Port,
                        ConnectTimeoutMs  = 3_000,
                        ReadTimeoutMs     = ReadTimeoutMs,
                        WriteTimeoutMs    = 3_000
                    },
                    null,
                    new NoDNS(),
                    new QuietLogger());

        private static async Task<SendResult> Send(NextHop         NextHop,
                                                   String[]?       To              = null,
                                                   String          Body            = "hello",
                                                   UInt32          ReadTimeoutMs   = 5_000,
                                                   DsnParameters?  Dsn             = null)
        {

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            var result = await ClientFor(NextHop, ReadTimeoutMs).SendAsync("next.example",
                                                                           "sender@client.example",
                                                                           To ?? [ "you@next.example" ],
                                                                           $"Subject: relay\r\n\r\n{Body}\r\n",
                                                                           dsn: Dsn,
                                                                           ct: cts.Token);

            await Task.Delay(100);
            return result;

        }

        #endregion


        #region Replies of more than one line

        public static IEnumerable<TestCaseData> MultiLineReplies()
        {

            TestCaseData Case(String Name, String Greeting = "220 next.hop ESMTP", Func<String, String?>? Reply = null)
                => new TestCaseData(Greeting, Reply).SetName($"A multi-line reply is read whole: {Name}");

            yield return Case("the greeting",     Greeting: "220-next.hop ESMTP\r\n220 at your service");
            yield return Case("MAIL",             Reply: line => line.StartsWith("MAIL") ? "250-2.1.0 sender\r\n250 2.1.0 ok"                   : null);
            yield return Case("RCPT",             Reply: line => line.StartsWith("RCPT") ? "250-2.1.5 recipient\r\n250 2.1.5 ok"                : null);
            yield return Case("DATA",             Reply: line => line.StartsWith("DATA") ? "354-go ahead\r\n354 end with <CRLF>.<CRLF>"         : null);
            yield return Case("the end of data",  Reply: line => line == "."             ? "250-2.0.0 queued\r\n250 2.0.0 as 4711"              : null);

        }


        [TestCaseSource(nameof(MultiLineReplies))]
        public async Task A_multi_line_reply_is_read_whole(String Greeting, Func<String, String?>? Reply)
        {

            using var nextHop = new NextHop(Reply, Greeting);

            var result = await Send(nextHop);

            Assert.That(result.Status,     Is.EqualTo(SendStatus.Success), result.ResponseText);
            Assert.That(nextHop.DataLines, Does.Contain("hello"));
            Assert.That(nextHop.Commands.Select(command => command.Split(' ')[0]),
                        Is.EqualTo(new[] { "EHLO", "MAIL", "RCPT", "DATA", "QUIT" }),
                        "each command after the whole reply to the one before");

        }


        /// <summary>
        /// A refusal of several lines, as large providers send them, is one reply: the next RCPT
        /// is answered by its own reply, not by the refusal's second line.
        /// </summary>
        [Test]
        public async Task A_multi_line_refusal_does_not_shift_the_replies()
        {

            using var nextHop = new NextHop(line => line.Contains("<first@")
                                                        ? "550-5.1.1 The email account that you tried to reach does not exist.\r\n550 5.1.1 Please try again."
                                                        : null);

            var result = await Send(nextHop, [ "first@next.example", "second@next.example" ]);

            Assert.That(result.Status,     Is.EqualTo(SendStatus.Success), result.ResponseText);
            Assert.That(nextHop.DataLines, Does.Contain("hello"), "second@ was accepted and gets the message");

        }


        [Test]
        public async Task A_reply_text_of_several_lines_is_kept_whole()
        {

            using var nextHop = new NextHop(line => line.StartsWith("MAIL") ? "550-5.7.1 Sender rejected:\r\n550 5.7.1 see https://next.hop/policy" : null);

            var result = await Send(nextHop);

            Assert.That(result.Status,       Is.EqualTo(SendStatus.PermFail));
            Assert.That(result.ResponseCode, Is.EqualTo(550));
            Assert.That(result.ResponseText, Does.Contain("Sender rejected:").And.Contain("next.hop/policy"));

        }

        #endregion

        #region Timeouts

        [Test]
        public async Task A_silent_next_hop_is_given_up_on_after_the_read_timeout()
        {

            using var nextHop = new NextHop(Silent: true);

            var stopwatch = Stopwatch.StartNew();
            var result    = await Send(nextHop, ReadTimeoutMs: 1_000);

            Assert.That(result.Status,     Is.EqualTo(SendStatus.TempFail));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "the 1 s read timeout, not the 20 s bound of the test");

        }

        #endregion

    }

}
