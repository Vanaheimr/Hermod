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
    /// TCP is a stream: where a server's reply is cut into segments means nothing. The client
    /// collects octets until a reply is complete and decodes it whole - a UTF-8 character split
    /// across two segments is one character.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientReplyReaderTests
    {

        /// <summary>
        /// A server that writes every reply in pieces, each its own segment.
        /// </summary>
        private sealed class Server : IDisposable
        {

            private readonly TcpListener                          listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly Func<Byte[], IEnumerable<Byte[]>>    split;

            public UInt16 Port
                => (UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;

            public Server(Func<Byte[], IEnumerable<Byte[]>> Split)
            {
                split = Split;
                listener.Start();
                _ = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                try
                {

                    using var client = await listener.AcceptTcpClientAsync();
                    client.NoDelay   = true;
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    async Task Write(String Text)
                    {
                        foreach (var piece in split(Encoding.UTF8.GetBytes(Text + "\r\n")))
                        {
                            await stream.WriteAsync(piece);
                            await stream.FlushAsync();
                            await Task.Delay(15);
                        }
                    }

                    await Write("220 split.test ESMTP");

                    var inData = false;

                    while (await reader.ReadLineAsync() is { } line)
                    {

                        if (inData)
                        {
                            if (line == ".")
                            {
                                inData = false;
                                await Write("250 2.0.0 Grüße aus Köln");
                            }
                            continue;
                        }

                        switch (line.Split(' ')[0].ToUpperInvariant())
                        {
                            case "EHLO":  await Write("250-split.test Hello\r\n250-8BITMIME\r\n250-SIZE 1000000\r\n250 ENHANCEDSTATUSCODES"); break;
                            case "DATA":  inData = true; await Write("354 go ahead");                                                         break;
                            case "QUIT":  await Write("221 2.0.0 bye"); return;
                            default:      await Write("250 2.0.0 ok");                                                                        break;
                        }

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


        private static async Task<SMTPSendResult> Send(Server Server)
        {

            using var client = new SMTPSubmissionClient(DomainName.Parse("127.0.0.1"),
                                                        IPPort.Parse(Server.Port),
                                                        LocalDomain:                 "client.example",
                                                        UseTLS:                      TLSUsage.NoTLS,
                                                        RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                                        ConnectionTimeout:           TimeSpan.FromSeconds(3),
                                                        CommandTimeout:              TimeSpan.FromSeconds(5));

            return await client.SendWithResult(new EMailEnvelop(EMail.Parse([
                                                   "From: app@client.example",
                                                   "To: you@split.test",
                                                   "Subject: split",
                                                   "",
                                                   "hello"
                                               ])),
                                               NumberOfRetries: 0);

        }


        [Test]
        public async Task A_reply_split_inside_a_UTF8_character_is_decoded_whole()
        {

            // Cut every reply right after the first octet of its first non-ASCII character.
            using var server = new Server(octets => {
                                              var cut = Array.FindIndex(octets, octet => octet > 0x7F);
                                              return cut < 0 ? [ octets ] : [ octets[..(cut + 1)], octets[(cut + 1)..] ];
                                          });

            var result = await Send(server);

            Assert.That(result.Status,   Is.EqualTo(MailSentStatus.ok));
            Assert.That(result.Response, Is.EqualTo("2.0.0 Grüße aus Köln"));

        }


        [Test]
        public async Task Replies_written_one_octet_at_a_time_are_read_whole()
        {

            using var server = new Server(octets => octets.Select(octet => new[] { octet }));

            var result = await Send(server);

            Assert.That(result.Status,   Is.EqualTo(MailSentStatus.ok));
            Assert.That(result.Response, Is.EqualTo("2.0.0 Grüße aus Köln"));

        }


        [TestCase("250 ok\r\n",                    8)]
        [TestCase("250-a\r\n250 b\r\n",           14)]
        [TestCase("250-a\r\n250-b\r\n",           -1)]
        [TestCase("250 ok",                       -1)]
        [TestCase("220 ready\r\nleftover",        11)]
        [TestCase("250\r\n",                       5)]
        [TestCase("250 Grüße\r\n",                13)]
        public void EndOfFirstReply_is_just_behind_the_first_final_line(String Text, Int32 End)

            => Assert.That(SMTPSubmissionClient.EndOfFirstReply(Encoding.UTF8.GetBytes(Text)), Is.EqualTo(End));

    }

}
