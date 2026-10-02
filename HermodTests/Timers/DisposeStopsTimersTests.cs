/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Timers
{

    using Warden = org.GraphDefined.Vanaheimr.Warden.Warden;

    /// <summary>
    /// What is disposed of stops its timers, and those of the DNS client it
    /// made for itself, and leaves a DNS client that it was lent alone.
    /// </summary>
    /// <remarks>
    /// A dump of the whole test suite, four minutes in, held 2,537 timers,
    /// most of them running. Most of those were the caches of DNS clients
    /// that TCP clients had made for themselves and never disposed of, even
    /// when the TCP clients were disposed of. Then TCP servers with their
    /// Wardens, and WebSocket clients that <c>await using</c> had disposed of.
    /// Their callbacks reached the thread pool at about 150 a second, while a
    /// test waited for a thread.
    /// </remarks>
    [TestFixture]
    public class DisposeStopsTimersTests
    {

        #region (class) IdleTCPServer

        /// <summary>
        /// A TCP server that is never started, with a DNS client of its own
        /// or one that it is lent.
        /// </summary>
        private sealed class IdleTCPServer(IDNSClient? DNSClient = null)

            : ATCPServer(TCPPort:    IPPort.Zero,
                         DNSClient:  DNSClient)

        {

            protected override Task HandleConnection(TCPConnection      Connection,
                                                     CancellationToken  Token)

                => Task.CompletedTask;

        }

        #endregion


        #region ATCPServerStopsItsTimersAndThoseOfItsDNSClient()

        /// <summary>
        /// Its maintenance timer, its Warden's, and that of its DNS client's
        /// cache. The Warden used to dispose of the DNS client, whoever's it
        /// was; the server now disposes of the one it made.
        /// </summary>
        [Test]
        public async Task ATCPServerStopsItsTimersAndThoseOfItsDNSClient()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new IdleTCPServer(),
                             server => server.DisposeAsync()
                         ),
                   "TCP servers"
               );

        #endregion

        #region ATCPServerLeavesALentDNSClientAlone()

        /// <summary>
        /// Disposing of a TCP server used to dispose of the DNS client it was
        /// lent, through its Warden, while the lender went on using it.
        /// </summary>
        [Test]
        public async Task ATCPServerLeavesALentDNSClientAlone()
        {

            var dnsClient = new FakeDNSClient();

            await new IdleTCPServer(dnsClient).DisposeAsync();

            Assert.That(dnsClient.DisposeCount, Is.Zero, "the lent DNS client was disposed of");

        }

        #endregion

        #region ATCPServerMayBeDisposedOfTwice()

        /// <summary>
        /// The second DisposeAsync() stopped the server again, and cancelled the
        /// token source the first one had disposed of - an
        /// ObjectDisposedException for a test that disposes of a server both in
        /// a finally block and by <c>await using</c>.
        /// </summary>
        [Test]
        public async Task ATCPServerMayBeDisposedOfTwice()
        {

            var server = new IdleTCPServer();

            await server.DisposeAsync();

            Assert.That(async () => await server.DisposeAsync(), Throws.Nothing);

        }

        #endregion


        #region ATCPClientStopsTheTimerOfItsDNSClient()

        /// <summary>
        /// A TCP client never disposed of the DNS client it made for itself,
        /// whose cache then went on cleaning up every ten seconds.
        /// </summary>
        [Test]
        public async Task ATCPClientStopsTheTimerOfItsDNSClient()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new TCPClient(IPv4Address.Localhost, IPPort.Parse(9)),
                             client => client.DisposeAsync()
                         ),
                   "TCP clients"
               );

        #endregion

        #region AnHTTPClientStopsTheTimerOfItsDNSClient()

        /// <summary>
        /// The same for every client built on the TCP client, an HTTP client
        /// for one.
        /// </summary>
        [Test]
        public async Task AnHTTPClientStopsTheTimerOfItsDNSClient()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new HTTPClient(URL.Parse("http://127.0.0.1:9")),
                             client => client.DisposeAsync()
                         ),
                   "HTTP clients"
               );

        #endregion

        #region AnHTTPClientLeavesALentDNSClientAlone()

        [Test]
        public async Task AnHTTPClientLeavesALentDNSClientAlone()
        {

            var dnsClient = new FakeDNSClient();

            await new HTTPClient(URL.Parse("http://127.0.0.1:9"), DNSClient: dnsClient).DisposeAsync();
            await Task.Run(new HTTPClient(URL.Parse("http://127.0.0.1:9"), DNSClient: dnsClient).Dispose);

            Assert.That(dnsClient.DisposeCount, Is.Zero, "the lent DNS client was disposed of");

        }

        #endregion


        #region AWebSocketClientStopsItsTimers_DisposeAsync()

        /// <summary>
        /// The DisposeAsync() it inherited from the TCP client closed only the
        /// TCP connection; its ping and maintenance timers ran on.
        /// </summary>
        [Test]
        public async Task AWebSocketClientStopsItsTimers_DisposeAsync()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new WebSocketClient(URL.Parse("ws://127.0.0.1:9")),
                             client => client.DisposeAsync()
                         ),
                   "WebSocket clients"
               );

        #endregion

        #region AWebSocketClientStopsItsTimers_Dispose()

        /// <summary>
        /// Dispose() closed it, timers included, but never got to what every
        /// TCP client lets go of: the DNS client it made for itself.
        /// </summary>
        [Test]
        public async Task AWebSocketClientStopsItsTimers_Dispose()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new WebSocketClient(URL.Parse("ws://127.0.0.1:9")),
                             client => new ValueTask(Task.Run(client.Dispose))
                         ),
                   "WebSocket clients"
               );

        #endregion


        #region AWebSocketProxyStopsTheTimersOfItsUpstreamClient()

        /// <summary>
        /// A proxy is a server with a WebSocket client of its own for the
        /// upstream server, and disposing of the server left that client
        /// running: its ping and maintenance timers, and its DNS client's.
        /// </summary>
        [Test]
        public async Task AWebSocketProxyStopsTheTimersOfItsUpstreamClient()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new WebSocketProxy(
                                       URL.Parse("ws://127.0.0.1:9"),
                                       AutoConnect:  false,
                                       HTTPPort:     IPPort.Zero
                                   ),
                             proxy => proxy.DisposeAsync()
                         ),
                   "WebSocket proxies"
               );

        #endregion


        #region AWardenStopsItsTimerAndThatOfItsDNSClient_Dispose()

        /// <summary>
        /// Dispose() stopped the Warden's own timer, but not that of the DNS
        /// client it made for itself.
        /// </summary>
        [Test]
        public async Task AWardenStopsItsTimerAndThatOfItsDNSClient_Dispose()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new Warden("timers"),
                             warden => new ValueTask(Task.Run(warden.Dispose))
                         ),
                   "Wardens"
               );

        #endregion

        #region AWardenStopsItsTimerAndThatOfItsDNSClient_DisposeAsync()

        [Test]
        public async Task AWardenStopsItsTimerAndThatOfItsDNSClient_DisposeAsync()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new Warden("timers"),
                             warden => warden.DisposeAsync()
                         ),
                   "Wardens"
               );

        #endregion

        #region AWardenLeavesALentDNSClientAlone()

        /// <summary>
        /// DisposeAsync() disposed of the DNS client, whoever's it was.
        /// </summary>
        [Test]
        public async Task AWardenLeavesALentDNSClientAlone()
        {

            var dnsClient = new FakeDNSClient();

            await new Warden("lent", DNSClient: dnsClient).DisposeAsync();
            await Task.Run(new Warden("lent", DNSClient: dnsClient).Dispose);

            Assert.That(dnsClient.DisposeCount, Is.Zero, "the lent DNS client was disposed of");

        }

        #endregion


        #region ADNSClientStopsTheTimerOfItsCache()

        /// <summary>
        /// What all of the above come down to.
        /// </summary>
        [Test]
        public async Task ADNSClientStopsTheTimerOfItsCache()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new DNSClient(ManualDNSServers: []),
                             client => client.DisposeAsync()
                         ),
                   "DNS clients"
               );

        #endregion

    }

}
