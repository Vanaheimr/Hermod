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

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// An HTTPS client begins a TLS handshake whichever way it was told where
    /// to connect to.
    /// </summary>
    /// <remarks>
    /// Only an https:// URL used to begin one. An HTTPSClient made from an IP
    /// address, from a bare TCP port or from a DNS name and DNS service began
    /// none unless its caller passed EnforceTLS: true. Its RemoteURL was
    /// tcp://, or there was none, and ATLSClient made a missing EnforceTLS
    /// false. ConnectNew reported success, and the requests that followed
    /// went out in plain text.
    ///
    /// The server here speaks no TLS. It keeps the first bytes it is sent and
    /// hangs up: whether they are the header of a TLS handshake record says
    /// whether the client began a handshake, with no certificate on either
    /// side.
    /// </remarks>
    [TestFixture]
    public class HTTPSClientTLSByDefaultTests
    {

        #region Data

        /// <summary>
        /// The first two bytes of a TLS record carrying a handshake message: its
        /// content type 22, and 3, the major number of every TLS version.
        /// </summary>
        private static readonly Byte[]  TLSHandshakeRecord  = [ 0x16, 0x03 ];

        private static readonly TimeSpan  Patience  = TimeSpan.FromSeconds(10);

        private static readonly RemoteTLSServerCertificateValidationHandler<IHTTPClient> AnyCertificate

            = (sender, certificate, certificateChain, client, policyErrors) => TLSValidationResult.Success();

        #endregion


        #region FromAnIPAddress()

        /// <summary>
        /// HTTPSClient.ConnectNew(IPAddress, TCPPort, ...) without EnforceTLS.
        /// </summary>
        [Test]
        public async Task FromAnIPAddress()
        {

            var server            = FirstBytesServer.Start(System.Net.IPAddress.Loopback);

            var (client, result)  = await HTTPSClient.ConnectNew(
                                              IPv4Address.Localhost,
                                              server.Port,
                                              AnyCertificate
                                          );

            if (client is not null)
                await client.DisposeAsync();

            var received          = await server.Received.WaitAsync(Patience);

            Assert.Multiple(() => {
                Assert.That(received.Take(2),   Is.EqualTo(TLSHandshakeRecord),  BitConverter.ToString(received));
                Assert.That(result.IsSuccess,   Is.False,                        "A server that speaks no TLS is no HTTPS connection.");
            });

        }

        #endregion

        #region FromATCPPortAlone()

        /// <summary>
        /// HTTPSClient.ConnectNew(TCPPort, ...) without EnforceTLS, which connects
        /// to [::1].
        /// </summary>
        [Test]
        public async Task FromATCPPortAlone()
        {

            FirstBytesServer server;

            try
            {
                server = FirstBytesServer.Start(System.Net.IPAddress.IPv6Loopback);
            }
            catch (SocketException e)
            {
                Assert.Ignore($"No [::1] to listen on: {e.SocketErrorCode}");
                return;
            }

            var (client, result)  = await HTTPSClient.ConnectNew(
                                              server.Port,
                                              AnyCertificate
                                          );

            if (client is not null)
                await client.DisposeAsync();

            var received          = await server.Received.WaitAsync(Patience);

            Assert.Multiple(() => {
                Assert.That(received.Take(2),   Is.EqualTo(TLSHandshakeRecord),  BitConverter.ToString(received));
                Assert.That(result.IsSuccess,   Is.False,                        "A server that speaks no TLS is no HTTPS connection.");
            });

        }

        #endregion

        #region FromADNSNameAndService()

        /// <summary>
        /// HTTPSClient.ConnectNew(DNSName, DNSService, ...) without EnforceTLS,
        /// resolved from the cache of a DNS client.
        /// </summary>
        /// <remarks>
        /// A DNS client without a server answers nothing, not even from its
        /// cache. This one's server is a closed port, which only the AAAA
        /// query nothing was cached for is sent to.
        /// </remarks>
        [Test]
        public async Task FromADNSNameAndService()
        {

            var server           = FirstBytesServer.Start(System.Net.IPAddress.Loopback);

            using var closedPort = new ClosedPort();

            await using var dnsClient = new DNSClient([
                                            new DNSServerConfig(
                                                IPv4Address.Localhost,
                                                closedPort.Number,
                                                QueryTimeout: TimeSpan.FromSeconds(1)
                                            )
                                        ]);

            dnsClient.CacheSRV(DNSServiceName.Parse("_https._tcp.api.example.test"), 10, 0, server.Port, DomainName.Parse("host.example.test"));
            dnsClient.CacheA  (DomainName.    Parse("host.example.test"),           IPv4Address.Localhost);

            var client           = await HTTPSClient.ConnectNew(
                                             DomainName.Parse("api.example.test"),
                                             SRV_Spec.TCP("https"),
                                             AnyCertificate,
                                             DNSClient: dnsClient
                                         );

            var isHTTPConnected  = client.IsHTTPConnected;

            await client.DisposeAsync();

            var received         = await server.Received.WaitAsync(Patience);

            Assert.Multiple(() => {
                Assert.That(received.Take(2),   Is.EqualTo(TLSHandshakeRecord),  BitConverter.ToString(received));
                Assert.That(isHTTPConnected,    Is.False,                        "A server that speaks no TLS is no HTTPS connection.");
            });

        }

        #endregion

        #region FromAnHTTPSURL()

        /// <summary>
        /// HTTPSClient.ConnectNew(URL, ...) with an https:// URL, which has
        /// always begun a handshake: what the server sees then.
        /// </summary>
        [Test]
        public async Task FromAnHTTPSURL()
        {

            var server    = FirstBytesServer.Start(System.Net.IPAddress.Loopback);

            var client    = await HTTPSClient.ConnectNew(
                                      URL.Parse($"https://127.0.0.1:{server.Port}"),
                                      AnyCertificate
                                  );

            await client.DisposeAsync();

            var received  = await server.Received.WaitAsync(Patience);

            Assert.That(received.Take(2), Is.EqualTo(TLSHandshakeRecord), BitConverter.ToString(received));

        }

        #endregion

        #region NotWhenEnforceTLSIsFalse()

        /// <summary>
        /// A caller who passes EnforceTLS: false still gets a client without
        /// TLS: the server is sent nothing, and the connect succeeds.
        /// </summary>
        [Test]
        public async Task NotWhenEnforceTLSIsFalse()
        {

            var server            = FirstBytesServer.Start(System.Net.IPAddress.Loopback);

            var (client, result)  = await HTTPSClient.ConnectNew(
                                              IPv4Address.Localhost,
                                              server.Port,
                                              AnyCertificate,
                                              EnforceTLS: false
                                          );

            if (client is not null)
                await client.DisposeAsync();

            // The client has hung up, so whatever it sent has arrived.
            var received          = await server.Received.WaitAsync(Patience);

            Assert.Multiple(() => {
                Assert.That(received,           Is.Empty,  BitConverter.ToString(received));
                Assert.That(result.IsSuccess,   Is.True,   result.ToString());
            });

        }

        #endregion


        #region (private class) FirstBytesServer

        /// <summary>
        /// A server for one connection, which keeps the first bytes it is sent
        /// and then hangs up.
        /// </summary>
        private sealed class FirstBytesServer
        {

            /// <summary>
            /// The TCP port it listens on.
            /// </summary>
            public IPPort        Port        { get; }

            /// <summary>
            /// The first five bytes or more, as many as the length of a TLS
            /// record header - or fewer, if the client hung up before.
            /// </summary>
            public Task<Byte[]>  Received    { get; }

            private FirstBytesServer(IPPort        Port,
                                     Task<Byte[]>  Received)
            {
                this.Port      = Port;
                this.Received  = Received;
            }

            public static FirstBytesServer Start(System.Net.IPAddress Address)
            {

                var listener  = new TcpListener(Address, 0);
                listener.Start();

                var port      = IPPort.Parse((UInt16) ((IPEndPoint) listener.LocalEndpoint).Port);

                var received  = Task.Run(async () => {

                    using var socket = await listener.AcceptSocketAsync();

                    // A second attempt is refused, not left in the backlog
                    // waiting for an answer that never comes.
                    listener.Stop();

                    var bytes   = new List<Byte>();
                    var buffer  = new Byte[4096];

                    // A client that began a handshake waits for the server's
                    // answer, so it is not waited for in turn.
                    while (bytes.Count < 5)
                    {

                        Int32 read;

                        try
                        {
                            read = await socket.ReceiveAsync(buffer);
                        }
                        catch (SocketException)
                        {
                            break;
                        }

                        if (read == 0)
                            break;

                        bytes.AddRange(buffer.Take(read));

                    }

                    return bytes.ToArray();

                });

                return new FirstBytesServer(
                           port,
                           received
                       );

            }

        }

        #endregion

    }

}
