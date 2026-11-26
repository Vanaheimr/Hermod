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
using System.Buffers.Binary;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Server
{

    /// <summary>
    /// What a request handler is told of how a request came: over a byte stream or
    /// not (DNSPacket.OverStream), and with which TSIG key it was signed and verified
    /// (DNSPacket.TSIGKeyName) - what a zone transfer is granted by.
    /// </summary>
    [TestFixture]
    public class DNSServer_RequestTransport_Tests
    {

        /// <summary>
        /// Answers as the zone does, and keeps every request it was handed.
        /// </summary>
        private sealed class Keeping(IDNSRequestHandler Inner) : IDNSRequestHandler
        {

            public List<DNSPacket> Requests { get; } = [];

            public Task<DNSResponse?> ProcessDNSRequest(DNSPacket          Request,
                                                        CancellationToken  CancellationToken = default)
            {
                lock (Requests)
                    Requests.Add(Request);
                return Inner.ProcessDNSRequest(Request, CancellationToken);
            }

        }

        private static readonly TSIGKey Key = TSIGKey.ParseBase64(DomainName.Parse("transfer.example.test."),
                                                                  Convert.ToBase64String(new Byte[32]));

        /// <summary>
        /// An A query for api.example.test., as it goes on the wire.
        /// </summary>
        private static Byte[] Query(UInt16 Id)
        {

            var query = new List<Byte> { (Byte) (Id >> 8), (Byte) Id, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            foreach (var label in new[] { "api", "example", "test" })
            {
                query.Add((Byte) label.Length);
                query.AddRange(System.Text.Encoding.ASCII.GetBytes(label));
            }

            query.AddRange([ 0x00, 0x00, 0x01, 0x00, 0x01 ]);

            return [ .. query ];

        }

        private static async Task OverUDP(IPPort Port, Byte[] Message)
        {
            using var udp     = new UdpClient(AddressFamily.InterNetwork);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await udp.SendAsync(Message, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, Port.ToUInt16()), timeout.Token);
            await udp.ReceiveAsync(timeout.Token);
        }

        private static async Task OverTCP(IPPort Port, Byte[] Message)
        {

            using var tcp     = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await tcp.ConnectAsync("127.0.0.1", Port.ToUInt16(), timeout.Token);

            var stream = tcp.GetStream();
            var length = new Byte[2];

            BinaryPrimitives.WriteUInt16BigEndian(length, (UInt16) Message.Length);
            await stream.WriteAsync(length,  timeout.Token);
            await stream.WriteAsync(Message, timeout.Token);

            await stream.ReadExactlyAsync(length, timeout.Token);
            await stream.ReadExactlyAsync(new Byte[BinaryPrimitives.ReadUInt16BigEndian(length)], timeout.Token);

        }


        [Test]
        public async Task TheHandler_IsToldTheTransport_AndTheKeyARequestWasSignedWith()
        {

            var keeping = new Keeping(new AuthoritativeDNSRequestHandler(
                                          new InMemoryDNSZone().Add(new A(DomainName. Parse("api.example.test."),
                                                                          DNSQueryClasses.IN,
                                                                          TimeSpan.   FromMinutes(5),
                                                                          IPv4Address.Parse("127.0.0.42")))));

            var server  = new DNSServer(keeping,
                                        new DNSServerOptions {
                                            UDPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Zero),
                                            TCPUnicastSocket    = new IPSocket(IPv4Address.Localhost, IPPort.Zero),
                                            EnableUDPMulticast  = false,
                                            TSIGKeys            = [ Key ]
                                        });

            try
            {

                await server.Start();

                await OverUDP(server.ActiveUDPUnicastSocket!.Value.Port, Query(1));
                await OverUDP(server.ActiveUDPUnicastSocket!.Value.Port, TSIGSigner.Sign(Query(2), Key));
                await OverTCP(server.ActiveTCPUnicastSocket!.Value.Port, Query(3));
                await OverTCP(server.ActiveTCPUnicastSocket!.Value.Port, TSIGSigner.Sign(Query(4), Key));

            }
            finally
            {
                await server.Stop();
            }

            var byId = keeping.Requests.ToDictionary(request => request.TransactionId);

            Assert.Multiple(() => {

                Assert.That(byId.Keys.Order(),                    Is.EqualTo(new UInt16[] { 1, 2, 3, 4 }));

                Assert.That(byId[1].OverStream,                   Is.False);
                Assert.That(byId[1].TSIGKeyName,                  Is.Null);

                Assert.That(byId[2].OverStream,                   Is.False);
                Assert.That(byId[2].TSIGKeyName?.FullName,        Is.EqualTo("transfer.example.test."));

                Assert.That(byId[3].OverStream,                   Is.True);
                Assert.That(byId[3].TSIGKeyName,                  Is.Null);

                Assert.That(byId[4].OverStream,                   Is.True);
                Assert.That(byId[4].TSIGKeyName?.FullName,        Is.EqualTo("transfer.example.test."));

            });

        }

    }

}
