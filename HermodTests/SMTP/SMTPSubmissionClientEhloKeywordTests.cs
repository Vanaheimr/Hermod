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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.TLS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// RFC 5321 §2.4: "Verbs and argument values (e.g., "TO:" or "to:" in the RCPT command and
    /// extension name keywords) are not case sensitive". A server that advertises "starttls"
    /// offers STARTTLS.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientEhloKeywordTests
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


        [Test]
        public async Task A_lower_case_starttls_is_STARTTLS()
        {

            using var server = new Server("8bitmime", "starttls");
            using var client = ClientFor(server, TLSUsage.STARTTLS);

            var result = await client.SendWithResult(Message("hello"), NumberOfRetries: 0);
            await Task.Delay(100);

            // The server refuses it with 454, so the message is not sent - but it was asked.
            Assert.That(server.Commands, Does.Contain("STARTTLS"), "the client must try STARTTLS, not report it missing");
            Assert.That(result.Status,   Is.EqualTo(MailSentStatus.TLSUnavailable));

        }


        [Test]
        public async Task Lower_case_8bitmime_and_size_are_used_on_MAIL()
        {

            using var server = new Server("8bitmime", "size 1000000");
            using var client = ClientFor(server);

            var result = await client.SendWithResult(Message("hello"), NumberOfRetries: 0);
            await Task.Delay(100);

            var mail = server.Commands.Single(command => command.StartsWith("MAIL"));

            Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok));
            Assert.That(mail,          Does.Contain(" BODY=8BITMIME"));
            Assert.That(mail,          Does.Match(@" SIZE=\d+"));

        }


        [Test]
        public async Task A_lower_case_size_limit_is_honoured()
        {

            using var server = new Server("size 100");
            using var client = ClientFor(server);

            var result = await client.SendWithResult(Message(new String('x', 500)), NumberOfRetries: 0);
            await Task.Delay(100);

            Assert.That(result.Status,   Is.EqualTo(MailSentStatus.MessageSizeExceeded));
            Assert.That(server.Commands, Has.None.StartsWith("MAIL"));

        }


        [TestCase("starttls",           "STARTTLS")]
        [TestCase("Size 1000",          "SIZE 1000")]
        [TestCase("auth plain Login",   "AUTH plain Login")]
        [TestCase("AUTH=login",         "AUTH=LOGIN")]
        [TestCase("PIPELINING",         "PIPELINING")]
        public void The_keyword_is_upper_case_and_the_parameters_are_unchanged(String Line, String Expected)

            => Assert.That(SMTPSubmissionClient.EhloLine(Line), Is.EqualTo(Expected));

    }

}
