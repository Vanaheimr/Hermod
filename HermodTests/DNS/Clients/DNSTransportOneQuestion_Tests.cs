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
using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// Whether the transport clients ask one question per query.
    /// </summary>
    /// <remarks>
    /// RFC 9619 §4: "A DNS message with OPCODE = 0 MUST NOT include a QDCOUNT
    /// parameter whose value is greater than 1." DNSClient split several types
    /// into several queries from the start; the UDP, TCP, TLS and HTTPS clients
    /// made one message of every type they were given, which a server following
    /// the RFC answers with FORMERR for all of them.
    /// </remarks>
    [TestFixture]
    public class DNSTransportOneQuestion_Tests
    {

        private static readonly TimeSpan                  Timeout   = TimeSpan.FromMilliseconds(600);
        private static readonly DNSResourceRecordTypes[]  TwoTypes  = [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ];


        /// <summary>QDCOUNT and the type of the first question, read off the wire.</summary>
        private static (Int32 QuestionCount, Int32 FirstType) Read(Byte[] Message)
        {

            var questionCount = (Message[4] << 8) | Message[5];

            // The question name starts at 12 and is uncompressed in a query.
            var offset = 12;
            while (Message[offset] != 0)
                offset += 1 + Message[offset];

            return (questionCount, (Message[offset + 1] << 8) | Message[offset + 2]);

        }


        #region TwoTypesOverUDP_AreTwoQueriesOfOneQuestion()

        [Test]
        public async Task TwoTypesOverUDP_AreTwoQueriesOfOneQuestion()
        {

            using var server      = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
            var       serverPort  = IPPort.Parse(((IPEndPoint) server.Client.LocalEndPoint!).Port);

            var       messages    = new ConcurrentQueue<Byte[]>();
            using var listening   = new CancellationTokenSource();

            var listener = Task.Run(async () => {
                try
                {
                    while (!listening.IsCancellationRequested)
                        messages.Enqueue((await server.ReceiveAsync(listening.Token)).Buffer);
                }
                catch (OperationCanceledException)
                { }
            });

            await using var client = new DNSUDPClient(IPv4Address.Localhost,
                                                      Port:          serverPort,
                                                      QueryTimeout:  Timeout);

            await client.Query(DNSServiceName.Parse("two.example."), TwoTypes, Timeout);

            listening.Cancel();
            await listener;

            var read = messages.Select(Read).ToArray();

            Assert.Multiple(() => {
                Assert.That(read.Select(message => message.QuestionCount), Is.All.EqualTo(1),
                            "every datagram, retransmissions included, asks one question");
                Assert.That(read.Select(message => message.FirstType).Distinct(),
                            Is.EquivalentTo(new[] { (Int32) DNSResourceRecordTypes.A, (Int32) DNSResourceRecordTypes.AAAA }),
                            "and between them they ask for both types");
            });

        }

        #endregion

        #region TwoTypesOverTCP_AreTwoQueriesOfOneQuestion()

        [Test]
        public async Task TwoTypesOverTCP_AreTwoQueriesOfOneQuestion()
        {

            var listener   = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var serverPort = IPPort.Parse(((IPEndPoint) listener.LocalEndpoint).Port);

            var messages   = new ConcurrentQueue<Byte[]>();
            using var listening = new CancellationTokenSource();

            var accepting  = Task.Run(async () => {
                try
                {
                    using var connection = await listener.AcceptTcpClientAsync(listening.Token);
                    var stream = connection.GetStream();
                    var length = new Byte[2];
                    while (!listening.IsCancellationRequested)
                    {

                        await stream.ReadExactlyAsync(length, listening.Token);
                        var message = new Byte[(length[0] << 8) | length[1]];
                        await stream.ReadExactlyAsync(message, listening.Token);
                        messages.Enqueue(message);

                        // Answered, with the query turned round: QR set, NOERROR,
                        // nothing in the answer section. This server reads one query,
                        // answers it and only then reads the next, so it sees both
                        // whether the client pipelines them (DNSTCPPipelining_Tests)
                        // or waits for each answer as it used to.
                        var response = (Byte[]) message.Clone();
                        response[2] |= 0x80;
                        response[3]  = 0x80;
                        await stream.WriteAsync(length,   listening.Token);
                        await stream.WriteAsync(response, listening.Token);

                    }
                }
                catch (OperationCanceledException) { }
                catch (EndOfStreamException)       { }
                catch (IOException)                { }
            });

            await using (var client = new DNSTCPClient(IPv4Address.Localhost,
                                                       Port:          serverPort,
                                                       QueryTimeout:  Timeout))
            {
                await client.Query(DNSServiceName.Parse("two.example."), TwoTypes, Timeout);
            }

            listening.Cancel();
            listener.Stop();
            await accepting;

            var read = messages.Select(Read).ToArray();

            Assert.Multiple(() => {
                Assert.That(read, Has.Length.EqualTo(2), "two types, two messages");
                Assert.That(read.Select(message => message.QuestionCount), Is.All.EqualTo(1),
                            "each of one question");
                Assert.That(read.Select(message => message.FirstType),
                            Is.EquivalentTo(new[] { (Int32) DNSResourceRecordTypes.A, (Int32) DNSResourceRecordTypes.AAAA }));
            });

        }

        #endregion

    }

}
