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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// Where the server listens: the configured addresses, IPv4 and IPv6, and port 0 for "any
    /// free port" - with the end points actually bound readable from the server.
    /// </summary>
    public partial class SMTPSessionWireTests
    {

        /// <summary>
        /// A server on the given addresses, every port 0, started; disposed with the result.
        /// </summary>
        private static async Task<(SMTPServer Server, IAsyncDisposable Stop)> ListeningOn(params System.Net.IPAddress[] Addresses)
        {

            var directory  = Directory.CreateTempSubdirectory("hermod-smtp-").FullName;
            var server     = new SMTPServer(new SMTPServerConfig {
                                                Hostname           = "mx.hermod.test",
                                                ListenAddresses    = Addresses,
                                                Port               = 0,
                                                SubmissionPort     = 0,
                                                EnableImplicitTls  = false,
                                                MailStoragePath    = directory,
                                                LocalDomains       = [ "hermod.test" ]
                                            },
                                            new NoDNS(),
                                            new QuietLogger(),
                                            mailStorage: new MemoryStorage());

            var running    = server.Start();

            return (server, new Stopper(async () => {
                try { await server.DisposeAsync(); } catch { }
                try { await running.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
                try { Directory.Delete(directory, recursive: true); } catch { }
            }));

        }

        private sealed class Stopper(Func<Task> Stop) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync() => await Stop();
        }

        private static async Task<String> GreetingFrom(System.Net.IPEndPoint EndPoint)
        {
            using var tcp    = new TcpClient(EndPoint.AddressFamily);
            await tcp.ConnectAsync(EndPoint);
            var buffer       = new Byte[512];
            var read         = await tcp.GetStream().ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            return Encoding.ASCII.GetString(buffer, 0, read);
        }


        [Test]
        public async Task Port_0_binds_a_free_port_and_the_server_tells_which()
        {

            var (server, stop) = await ListeningOn(System.Net.IPAddress.Loopback);
            await using var _  = stop;

            Assert.That(server.MtaEndPoints,        Has.Count.EqualTo(1));
            Assert.That(server.SubmissionEndPoints, Has.Count.EqualTo(1));
            Assert.That(server.MtaEndPoints[0].Address, Is.EqualTo(System.Net.IPAddress.Loopback));
            Assert.That(server.MtaEndPoints[0].Port,    Is.Not.Zero);
            Assert.That(server.SubmissionEndPoints[0].Port, Is.Not.EqualTo(server.MtaEndPoints[0].Port));
            Assert.That(server.ImplicitTlsEndPoints, Is.Empty, "no certificate, no implicit TLS");

            Assert.That(await GreetingFrom(server.MtaEndPoints[0]),        Does.StartWith("220 "));
            Assert.That(await GreetingFrom(server.SubmissionEndPoints[0]), Does.StartWith("220 "));

        }


        [Test]
        public async Task The_server_listens_on_IPv6()
        {

            Assume.That(Socket.OSSupportsIPv6, "no IPv6 on this host");

            var (server, stop) = await ListeningOn(System.Net.IPAddress.IPv6Loopback);
            await using var _  = stop;

            Assert.That(server.MtaEndPoints.Single().AddressFamily, Is.EqualTo(AddressFamily.InterNetworkV6));
            Assert.That(await GreetingFrom(server.MtaEndPoints[0]), Does.StartWith("220 "));

        }


        /// <summary>
        /// IPv4 and IPv6 side by side: each listener keeps to its own family, so both bind.
        /// </summary>
        [Test]
        public async Task IPv4_and_IPv6_loopback_together()
        {

            Assume.That(Socket.OSSupportsIPv6, "no IPv6 on this host");

            var (server, stop) = await ListeningOn(System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback);
            await using var _  = stop;

            Assert.That(server.MtaEndPoints.Select(endPoint => endPoint.AddressFamily),
                        Is.EquivalentTo(new[] { AddressFamily.InterNetwork, AddressFamily.InterNetworkV6 }));

            foreach (var endPoint in server.MtaEndPoints)
                Assert.That(await GreetingFrom(endPoint), Does.StartWith("220 "), endPoint.ToString());

        }


        [Test]
        public void By_default_the_server_listens_on_every_IPv4_address()

            => Assert.That(new SMTPServerConfig { Hostname = "mx.hermod.test" }.ListenAddresses,
                           Is.EqualTo(new[] { System.Net.IPAddress.Any }));

    }

}
