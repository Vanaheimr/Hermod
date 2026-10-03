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
    /// RFC 5321 §4.1.4: "The domain name given in the EHLO command MUST be either a primary host
    /// name (a domain name that resolves to an address RR) or, if the host has no name, an
    /// address literal". Without a configured local domain the client names itself by the
    /// address its connection leaves from.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientEhloArgumentTests
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
        public async Task Without_a_local_domain_EHLO_names_the_local_address()
        {

            using var server = new Server("8BITMIME");
            using var client = ClientFor(server, LocalDomain: null);

            var result = await client.SendWithResult(Message("hello"), NumberOfRetries: 0);
            await Task.Delay(100);

            Assert.That(result.Status,   Is.EqualTo(MailSentStatus.ok));
            Assert.That(server.Commands, Does.Contain("EHLO [127.0.0.1]"), "the connection to 127.0.0.1 leaves from 127.0.0.1");

        }


        [Test]
        public async Task A_configured_local_domain_is_used_as_it_is()
        {

            using var server = new Server("8BITMIME");
            using var client = ClientFor(server, LocalDomain: "client.example");

            await client.SendWithResult(Message("hello"), NumberOfRetries: 0);
            await Task.Delay(100);

            Assert.That(server.Commands, Does.Contain("EHLO client.example"));

        }


        [TestCase("192.0.2.1",          "[192.0.2.1]")]
        [TestCase("2001:db8::1",        "[IPv6:2001:db8::1]")]
        [TestCase("::ffff:192.0.2.1",   "[192.0.2.1]")]
        [TestCase("fe80::1%3",          "[IPv6:fe80::1]")]
        public void Address_literals_follow_RFC_5321_section_4_1_3(String Address, String Literal)

            => Assert.That(SMTPSubmissionClient.AddressLiteral(System.Net.IPAddress.Parse(Address)), Is.EqualTo(Literal));

    }

}
