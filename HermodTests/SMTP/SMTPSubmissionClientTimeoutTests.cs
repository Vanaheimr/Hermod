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
    /// RFC 5321 §4.5.3.2.6, DATA Termination: 10 minutes - "A spurious timeout at this point
    /// would be very wasteful and would typically result in delivery of multiple copies of the
    /// message". The reply to the end of data has a timeout of its own.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientTimeoutTests
    {

        /// <summary>
        /// A server that takes its time to answer the end of data, on every connection, and
        /// counts the messages it took.
        /// </summary>
        private sealed class SlowServer : IDisposable
        {

            private readonly TcpListener  listener = new (System.Net.IPAddress.Loopback, 0);
            private readonly TimeSpan     delay;
            private          Int32        messages;

            public Int32 Messages
                => Volatile.Read(ref messages);

            public IPPort Port
                => IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);

            public SlowServer(TimeSpan Delay)
            {
                delay = Delay;
                listener.Start();
                _ = Task.Run(AcceptAsync);
            }

            private async Task AcceptAsync()
            {
                try
                {
                    while (true)
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        _ = Task.Run(() => ServeAsync(client));
                    }
                }
                catch
                {
                    // stopped
                }
            }

            private async Task ServeAsync(TcpClient Client)
            {
                try
                {

                    using var client = Client;
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    Task Write(String Text)
                        => stream.WriteAsync(Encoding.ASCII.GetBytes(Text + "\r\n")).AsTask();

                    await Write("220 slow.test ESMTP");

                    var inData = false;

                    while (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) is { } line)
                    {

                        if (inData)
                        {
                            if (line == ".")
                            {
                                inData = false;
                                Interlocked.Increment(ref messages);    // the message is the server's from here on
                                await Task.Delay(delay);
                                await Write("250 2.0.0 delivered");
                            }
                            continue;
                        }

                        switch (line.Split(' ')[0].ToUpperInvariant())
                        {
                            case "EHLO":  await Write("250-slow.test\r\n250 8BITMIME"); break;
                            case "DATA":  inData = true; await Write("354 go ahead");  break;
                            case "QUIT":  await Write("221 2.0.0 bye");                 return;
                            default:      await Write("250 2.0.0 ok");                  break;
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

        private static async Task<MailSentStatus> Send(SlowServer Server, TimeSpan? CommandTimeout, TimeSpan? DataTerminationTimeout)
        {

            using var client = new SMTPSubmissionClient(DomainName.Parse("127.0.0.1"),
                                                        Server.Port,
                                                        LocalDomain:                 "client.example",
                                                        UseTLS:                      TLSUsage.NoTLS,
                                                        RemoteCertificateValidator:  (_, _, _, _, _) => TLSValidationResult.Success(),
                                                        ConnectionTimeout:           TimeSpan.FromSeconds(3),
                                                        CommandTimeout:              CommandTimeout,
                                                        DataTerminationTimeout:      DataTerminationTimeout);

            return await client.Send(new EMailEnvelop(EMail.Parse([
                                         "From: app@client.example",
                                         "To: you@slow.test",
                                         "Subject: slow",
                                         "",
                                         "hello"
                                     ])),
                                     NumberOfRetries: 1);

        }


        [Test(Description = "RFC 5321 §4.5.3.2.6: the reply to the end of data is waited for longer than that of a command - a server that answers after the command timeout, within the DATA termination timeout, has the message once, and the client counts it sent")]
        public async Task The_reply_to_the_end_of_data_is_waited_for_longer_than_a_command()
        {

            using var server = new SlowServer(TimeSpan.FromMilliseconds(1500));

            var result = await Send(server, CommandTimeout: TimeSpan.FromMilliseconds(500), DataTerminationTimeout: TimeSpan.FromSeconds(6));

            Assert.Multiple(() => {
                Assert.That(result,          Is.EqualTo(MailSentStatus.ok));
                Assert.That(server.Messages, Is.EqualTo(1), "one message, once");
            });

        }


        [Test(Description = "RFC 5321 §4.5.3.2: \"An SMTP client MUST provide a timeout mechanism\" - past the DATA termination timeout the client gives up")]
        public async Task Past_the_data_termination_timeout_the_client_gives_up()
        {

            using var server = new SlowServer(TimeSpan.FromSeconds(3));

            var result = await Send(server, CommandTimeout: TimeSpan.FromSeconds(5), DataTerminationTimeout: TimeSpan.FromMilliseconds(500));

            Assert.That(result, Is.EqualTo(MailSentStatus.Timeout));

        }

    }

}
