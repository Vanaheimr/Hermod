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
    /// RFC 1870 §5: the size of a message is "the number of octets, including CR-LF pairs, but not
    /// the SMTP DATA command's terminating dot or doubled quoting dots, to be transmitted by the
    /// SMTP client after receiving reply code 354 to the DATA command". The client declares it on
    /// MAIL and checks it against the server's SIZE limit.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientMessageSizeTests
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


        /// <summary>
        /// The octets the server received after 354, as RFC 1870 counts them: every line with its
        /// CR LF, a doubled quoting dot once.
        /// </summary>
        private static UInt64 ReceivedSize(Server Server)

            => Server.DataLines.Aggregate(0UL, (size, line) => size + (UInt64) (line is [ (Byte) '.', _, .. ] ? line.Length - 1 : line.Length) + 2);


        [Test]
        public async Task The_declared_SIZE_is_the_size_sent()
        {

            using var server = new Server("SIZE 1000000");
            using var client = ClientFor(server);

            var result = await client.SendWithResult(Message("line one", ".stuffed", "line three"), NumberOfRetries: 0);
            await Task.Delay(100);

            var mail     = server.Commands.Single(command => command.StartsWith("MAIL"));
            var declared = UInt64.Parse(Regex.Match(mail, @" SIZE=(\d+)").Groups[1].Value);

            Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok));
            Assert.That(declared,      Is.EqualTo(ReceivedSize(server)));

        }


        /// <summary>
        /// A limit one octet below the message refuses it before MAIL; a limit of exactly its size
        /// lets it through.
        /// </summary>
        [Test]
        public async Task The_SIZE_limit_holds_to_the_octet()
        {

            UInt64 size;
            using (var probe = new Server("SIZE 1000000"))
            using (var client = ClientFor(probe))
            {
                await client.SendWithResult(Message("hello", "world"), NumberOfRetries: 0);
                await Task.Delay(100);
                size = ReceivedSize(probe);
            }

            using (var tooSmall = new Server($"SIZE {size - 1}"))
            using (var client   = ClientFor(tooSmall))
            {
                var result = await client.SendWithResult(Message("hello", "world"), NumberOfRetries: 0);
                await Task.Delay(100);
                Assert.That(result.Status,     Is.EqualTo(MailSentStatus.MessageSizeExceeded), $"{size} octets against a limit of {size - 1}");
                Assert.That(tooSmall.Commands, Has.None.StartsWith("MAIL"));
            }

            using (var exact  = new Server($"SIZE {size}"))
            using (var client = ClientFor(exact))
            {
                var result = await client.SendWithResult(Message("hello", "world"), NumberOfRetries: 0);
                await Task.Delay(100);
                Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok), $"{size} octets against a limit of {size}");
            }

        }


        [TestCase(new String[0],                 0UL)]
        [TestCase(new[] { "" },                  2UL)]
        [TestCase(new[] { "abc", "" },           7UL)]
        [TestCase(new[] { "Grüße" },             9UL)]
        public void MessageSize_counts_every_CRLF_and_UTF8_octets(String[] Lines, UInt64 Size)

            => Assert.That(SMTPSubmissionClient.MessageSize(Lines), Is.EqualTo(Size));

    }

}
