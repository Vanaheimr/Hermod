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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// Whether DNSUDPClient asks again when nothing comes back.
    /// </summary>
    /// <remarks>
    /// It used to send one datagram and then wait out the whole query timeout.
    /// UDP loses datagrams — measured from this network at roughly one query in
    /// a hundred and twenty, to Google and to Cloudflare alike — and every one of
    /// those losses became a 23.5 second failure that a second question would
    /// have answered. Five losses in six hundred queries, five recovered by
    /// simply asking again, and not one stray datagram among them: nothing was
    /// arriving and being rejected, it was not arriving at all.
    /// </remarks>
    [TestFixture]
    public class DNSUDPRetransmissionTests
    {

        #region ASilentServer_IsAskedAgain()

        /// <summary>
        /// A server that never answers. Within a budget of three and a half
        /// seconds and a first wait of one, the client should ask at zero, one
        /// and three seconds — and in any case more than once.
        /// </summary>
        [Test]
        public async Task ASilentServer_IsAskedAgain()
        {

            using var server      = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
            var       serverPort  = IPPort.Parse(((IPEndPoint) server.Client.LocalEndPoint!).Port);

            var       datagrams   = 0;
            using var listening   = new CancellationTokenSource();

            var listener = Task.Run(async () => {
                try
                {
                    while (!listening.IsCancellationRequested)
                    {
                        await server.ReceiveAsync(listening.Token);
                        Interlocked.Increment(ref datagrams);
                    }
                }
                catch (OperationCanceledException)
                { }
            });

            using var client = new DNSUDPClient(
                                   IPv4Address.Localhost,
                                   Port:          serverPort,
                                   QueryTimeout:  TimeSpan.FromSeconds(3.5)
                               ) {
                                   RetransmissionInterval = TimeSpan.FromSeconds(1)
                               };

            var response = await client.Query<A>(DomainName.Parse("silent.example"));

            await listening.CancelAsync();
            await listener;

            Assert.Multiple(() => {

                Assert.That(response.IsTimeout,  Is.True,
                            "the server never answers, so the query must still end as a timeout");

                Assert.That(datagrams,           Is.GreaterThan(1),
                            $"the question was asked {datagrams} time(s); one means it was never repeated");

            });

        }

        #endregion

        #region AQueryShorterThanTheInterval_IsAskedOnce()

        /// <summary>
        /// A budget too short for a second attempt must not produce one. This is
        /// what keeps a caller who asked for a 200 ms answer from being given
        /// three datagrams' worth of load instead.
        /// </summary>
        [Test]
        public async Task AQueryShorterThanTheInterval_IsAskedOnce()
        {

            using var server      = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
            var       serverPort  = IPPort.Parse(((IPEndPoint) server.Client.LocalEndPoint!).Port);

            var       datagrams   = 0;
            using var listening   = new CancellationTokenSource();

            var listener = Task.Run(async () => {
                try
                {
                    while (!listening.IsCancellationRequested)
                    {
                        await server.ReceiveAsync(listening.Token);
                        Interlocked.Increment(ref datagrams);
                    }
                }
                catch (OperationCanceledException)
                { }
            });

            using var client = new DNSUDPClient(
                                   IPv4Address.Localhost,
                                   Port:          serverPort,
                                   QueryTimeout:  TimeSpan.FromMilliseconds(200)
                               ) {
                                   RetransmissionInterval = TimeSpan.FromSeconds(1)
                               };

            var response = await client.Query<A>(DomainName.Parse("silent.example"));

            await listening.CancelAsync();
            await listener;

            Assert.Multiple(() => {
                Assert.That(response.IsTimeout,  Is.True);
                Assert.That(datagrams,           Is.EqualTo(1),  $"asked {datagrams} time(s)");
            });

        }

        #endregion

    }

}
