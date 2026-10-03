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
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.TLS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 5321 §2.3.8: "SMTP client implementations MUST NOT transmit these characters [bare CR
    /// or LF] except when they are intended as line terminators and then MUST transmit them only
    /// as a &lt;CRLF&gt; sequence." A body text with "\n" in it goes out as separate lines.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientBareLineEndingTests
    {

        /// <summary>
        /// A server that answers EHLO with the given extensions, refuses STARTTLS with 454 and
        /// accepts everything else. Lines end at CR LF and nowhere else: a bare CR or LF stays
        /// inside the line, where a test can see it.
        /// </summary>
        private sealed class Server : IDisposable
        {

            private readonly TcpListener              listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly String[]                 extensions;

            public readonly ConcurrentQueue<String>   Commands  = new();
            public readonly ConcurrentQueue<Byte[]>   DataLines = new();

            public UInt16 Port
                => (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            public Server(params String[] Extensions)
            {
                extensions = Extensions;
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                try
                {

                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();

                    Task Reply(String Text)
                        => stream.WriteAsync(Encoding.ASCII.GetBytes(Text + "\r\n")).AsTask();

                    await Reply("220 wire.test ESMTP");

                    var inData = false;

                    while (await ReadLineAsync(stream) is { } bytes)
                    {

                        if (inData)
                        {
                            if (bytes is [ (Byte) '.' ]) { inData = false; await Reply("250 2.0.0 queued"); }
                            else                           DataLines.Enqueue(bytes);
                            continue;
                        }

                        var line  = Encoding.UTF8.GetString(bytes);
                        var upper = line.ToUpperInvariant();
                        Commands.Enqueue(line);

                        if (upper.StartsWith("EHLO"))
                        {
                            await Reply(extensions.Length == 0 ? "250 wire.test" : "250-wire.test");
                            for (var i = 0; i < extensions.Length; i++)
                                await Reply((i == extensions.Length - 1 ? "250 " : "250-") + extensions[i]);
                        }
                        else if (upper == "STARTTLS")  await Reply("454 4.7.0 TLS not available");
                        else if (upper == "DATA")    { inData = true; await Reply("354 go ahead"); }
                        else if (upper == "QUIT")    { await Reply("221 2.0.0 bye"); return; }
                        else                           await Reply("250 2.0.0 ok");

                    }

                }
                catch
                {
                    // the client hung up
                }
            }

            private static async Task<Byte[]?> ReadLineAsync(Stream Stream)
            {

                var line   = new List<Byte>();
                var octet  = new Byte[1];

                while (await Stream.ReadAsync(octet) == 1)
                {
                    if (octet[0] == '\n' && line.Count > 0 && line[^1] == '\r')
                    {
                        line.RemoveAt(line.Count - 1);
                        return [.. line];
                    }
                    line.Add(octet[0]);
                }

                return null;

            }

            public void Dispose()
                => listener.Stop();

        }


        private static SMTPSubmissionClient ClientFor(Server    Server,
                                                      TLSUsage  UseTLS       = TLSUsage.NoTLS,
                                                      String?   LocalDomain  = "client.example")

            => new (DomainName.Parse("127.0.0.1"),
                    IPPort.Parse(Server.Port),
                    LocalDomain:                 LocalDomain,
                    UseTLS:                      UseTLS,
                    RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                    ConnectionTimeout:           TimeSpan.FromSeconds(3),
                    CommandTimeout:              TimeSpan.FromSeconds(3));


        private static EMailEnvelop Message(params String[] BodyLines)

            => new (EMail.Parse([
                        "From: app@client.example",
                        "To: you@wire.test",
                        "Subject: wire format",
                        "Content-Type: text/plain; charset=utf-8",
                        "",
                        .. BodyLines
                    ]));


        private static String[] Body(Server Server)

            => [.. Server.DataLines.Select(line => Encoding.UTF8.GetString(line)).SkipWhile(line => line.Length > 0).Skip(1)];


        [Test]
        public async Task Bare_LF_and_bare_CR_in_the_body_go_out_as_CRLF()
        {

            using var server = new Server("8BITMIME", "SIZE 1000000");
            using var client = ClientFor(server);

            var result = await client.SendWithResult(Message("first\nsecond", "third\rfourth", "fifth\r\nsixth"), NumberOfRetries: 0);
            await Task.Delay(100);

            Assert.That(result.Status,     Is.EqualTo(MailSentStatus.ok));
            Assert.That(server.DataLines,  Has.None.Matches<Byte[]>(line => line.Contains((Byte) '\r') || line.Contains((Byte) '\n')),
                        "every line ends at CR LF, and none holds a CR or LF of its own");
            Assert.That(Body(server),      Is.EqualTo(new[] { "first", "second", "third", "fourth", "fifth", "sixth" }));

        }


        /// <summary>
        /// The smuggling primitive: "\n.\n" inside the body. Split into lines, the "." is a line
        /// of its own and dot-stuffed - the message stays one message.
        /// </summary>
        [Test]
        public async Task A_dot_between_bare_LFs_is_dot_stuffed()
        {

            using var server = new Server("8BITMIME", "SIZE 1000000");
            using var client = ClientFor(server);

            var result = await client.SendWithResult(Message("before\n.\nMAIL FROM:<smuggled@evil.example>"), NumberOfRetries: 0);
            await Task.Delay(100);

            Assert.That(result.Status,   Is.EqualTo(MailSentStatus.ok));
            Assert.That(Body(server),    Is.EqualTo(new[] { "before", "..", "MAIL FROM:<smuggled@evil.example>" }));
            Assert.That(server.Commands.Count(command => command.StartsWith("MAIL")), Is.EqualTo(1));

        }


        /// <summary>
        /// The size declared on MAIL is that of the lines sent - a bare LF turned into CR LF
        /// is one octet more.
        /// </summary>
        [Test]
        public async Task The_declared_SIZE_counts_the_lines_as_sent()
        {

            using var server = new Server("SIZE 1000000");
            using var client = ClientFor(server);

            await client.SendWithResult(Message("a\nb\nc"), NumberOfRetries: 0);
            await Task.Delay(100);

            var mail     = server.Commands.Single(command => command.StartsWith("MAIL"));
            var declared = UInt64.Parse(Regex.Match(mail, @" SIZE=(\d+)").Groups[1].Value);
            var received = server.DataLines.Aggregate(0UL, (size, line) => size + (UInt64) line.Length + 2);

            Assert.That(declared, Is.EqualTo(received));

        }


        [TestCase("plain",          new[] { "plain" })]
        [TestCase("",               new[] { "" })]
        [TestCase("a\nb",           new[] { "a", "b" })]
        [TestCase("a\rb",           new[] { "a", "b" })]
        [TestCase("a\r\nb",         new[] { "a", "b" })]
        [TestCase("a\n\rb",         new[] { "a", "", "b" })]
        [TestCase("a\n",            new[] { "a", "" })]
        public void WireLines_ends_a_line_at_every_CR_LF_bare_CR_and_bare_LF(String Line, String[] Expected)

            => Assert.That(SMTPSubmissionClient.WireLines([ Line ]), Is.EqualTo(Expected));

    }

}
