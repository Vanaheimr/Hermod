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
    /// RFC 5321 §2.4 / RFC 6152 §3: no octet with the high bit set goes to a server that
    /// did not advertise 8BITMIME. The client does not convert to 7-bit MIME, so such a
    /// message is a permanent failure, decided before MAIL.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClient8BitTests
    {

        private sealed class Server : IDisposable
        {

            private readonly TcpListener              listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly Boolean                  eightBitMime;

            public readonly ConcurrentQueue<String>   Commands  = new();
            public readonly ConcurrentQueue<Byte[]>   DataLines = new();

            public UInt16 Port
                => (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            public Server(Boolean EightBitMime)
            {
                eightBitMime = EightBitMime;
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                try
                {

                    using var client = await listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.Latin1);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

                    await writer.WriteLineAsync("220 seven.test ESMTP");

                    var inData = false;
                    String? line;

                    while ((line = await reader.ReadLineAsync()) is not null)
                    {

                        if (inData)
                        {
                            if (line == ".") { await writer.WriteLineAsync("250 queued"); inData = false; }
                            else               DataLines.Enqueue(Encoding.Latin1.GetBytes(line));
                            continue;
                        }

                        Commands.Enqueue(line);
                        var upper = line.ToUpperInvariant();

                        if (upper.StartsWith("EHLO"))
                        {
                            await writer.WriteLineAsync("250-seven.test");
                            if (eightBitMime)
                                await writer.WriteLineAsync("250-8BITMIME");
                            await writer.WriteLineAsync("250 SIZE 1000000");
                        }
                        else if (upper == "DATA")  { await writer.WriteLineAsync("354 go ahead"); inData = true; }
                        else if (upper == "QUIT")  { await writer.WriteLineAsync("221 bye"); return; }
                        else                         await writer.WriteLineAsync("250 ok");

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


        private static async Task<(SMTPSendResult Result, String[] Commands, Byte[][] Data)> Send(Boolean EightBitMime, String Body)
        {

            using var server = new Server(EightBitMime);
            using var client = new SMTPSubmissionClient(DomainName.Parse("127.0.0.1"),
                                                        IPPort.Parse(server.Port),
                                                        LocalDomain:                 "client.example",
                                                        UseTLS:                      TLSUsage.NoTLS,
                                                        RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                                        ConnectionTimeout:           TimeSpan.FromSeconds(3),
                                                        CommandTimeout:              TimeSpan.FromSeconds(3));

            var result = await client.SendWithResult(new EMailEnvelop(EMail.Parse([
                                                         "From: app@client.example",
                                                         "To: you@seven.test",
                                                         "Subject: encoding",
                                                         "Content-Type: text/plain; charset=utf-8",
                                                         "",
                                                         Body
                                                     ])),
                                                     NumberOfRetries: 2);

            await Task.Delay(100);

            return (result, server.Commands.ToArray(), server.DataLines.ToArray());

        }


        [Test]
        public async Task Eight_bit_content_is_not_sent_without_8BITMIME()
        {

            var (result, commands, data) = await Send(EightBitMime: false, "Grüße aus Köln");

            Assert.That(result.Status,   Is.EqualTo(MailSentStatus.EightBitNotSupported));
            Assert.That(result.Attempts, Is.EqualTo(1), "a permanent failure is not retried");
            Assert.That(commands,        Has.None.StartsWith("MAIL"));
            Assert.That(data,            Is.Empty);

        }


        [Test]
        public async Task Seven_bit_content_is_sent_without_8BITMIME()
        {

            var (result, commands, data) = await Send(EightBitMime: false, "Hello from Cologne");

            Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok));
            Assert.That(commands,      Has.Some.StartsWith("MAIL").And.None.Contains("BODY="));
            Assert.That(data.SelectMany(line => line).Any(octet => octet > 0x7F), Is.False);

        }


        [Test]
        public async Task Eight_bit_content_is_sent_with_8BITMIME()
        {

            var (result, commands, data) = await Send(EightBitMime: true, "Grüße aus Köln");

            Assert.That(result.Status, Is.EqualTo(MailSentStatus.ok));
            Assert.That(commands,      Has.Some.Contains("BODY=8BITMIME"));
            Assert.That(data.SelectMany(line => line).Any(octet => octet > 0x7F), Is.True, "the content goes out as UTF-8, unchanged");

        }

    }

}
