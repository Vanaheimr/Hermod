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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;
using org.GraphDefined.Vanaheimr.Hermod.IPv4.ICMP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Timers
{

    /// <summary>
    /// An ICMP client, a notification sender and a network service node each
    /// make a DNS client for themselves when they were handed none, and the
    /// node an HTTP server as well. Disposing of them stops the timers of what
    /// they made, and leaves what they were lent alone.
    /// </summary>
    /// <remarks>
    /// None of the three could be disposed of. A DNS client's cache cleans up
    /// on a timer every ten seconds, and a running timer keeps what it calls
    /// alive: every instance made left that timer behind, and the sender and
    /// the node more of their own, each putting its callback on the thread
    /// pool for the rest of the process.
    /// </remarks>
    [TestFixture]
    public class DNSClientOwnersDisposeTests
    {

        #region (private) NewHTTPAPI()

        /// <summary>
        /// An HTTP API on an HTTP server that is never started, to be lent to
        /// what needs one.
        /// </summary>
        private static HTTPExtAPI NewHTTPAPI()

            => new (
                   new HTTPServer(
                       IPAddress:  IPv4Address.Localhost,
                       TCPPort:    IPPort.Zero,
                       AutoStart:  false
                   ),
                   SkipURLTemplates:      true,
                   DisableNotifications:  true,
                   DisableLogging:        true
               );

        #endregion


        #region AnICMPClientStopsTheTimerOfItsDNSClient_DisposeAsync()

        [Test]
        public async Task AnICMPClientStopsTheTimerOfItsDNSClient_DisposeAsync()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new ICMPClient(),
                             client => client.DisposeAsync()
                         ),
                   "ICMP clients"
               );

        #endregion

        #region AnICMPClientStopsTheTimerOfItsDNSClient_Dispose()

        [Test]
        public async Task AnICMPClientStopsTheTimerOfItsDNSClient_Dispose()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new ICMPClient(),
                             client => new ValueTask(Task.Run(client.Dispose))
                         ),
                   "ICMP clients"
               );

        #endregion

        #region AnICMPClientLeavesALentDNSClientAlone()

        [Test]
        public async Task AnICMPClientLeavesALentDNSClientAlone()
        {

            var dnsClient = new FakeDNSClient();

            await new ICMPClient(DNSClient: dnsClient).DisposeAsync();
            await Task.Run(new ICMPClient(DNSClient: dnsClient).Dispose);

            Assert.That(dnsClient.DisposeCount, Is.Zero, "the lent DNS client was disposed of");

        }

        #endregion


        #region ANotificationSenderStopsItsTimerAndThatOfItsDNSClient_DisposeAsync()

        /// <summary>
        /// The sender's own timer calls one of its methods, and so kept the
        /// sender alive, and ticking, for as long as the process ran.
        /// </summary>
        [Test]
        public async Task ANotificationSenderStopsItsTimerAndThatOfItsDNSClient_DisposeAsync()
        {

            var httpAPI = NewHTTPAPI();

            try
            {

                TimerCount.AssertNoneLeft(
                    await TimerCount.Of(
                              () => new HTTPNotificationSender(httpAPI, HTTPHostname.Localhost),
                              sender => sender.DisposeAsync()
                          ),
                    "notification senders"
                );

            }
            finally
            {
                await httpAPI.HTTPServer.DisposeAsync();
            }

        }

        #endregion

        #region ANotificationSenderStopsItsTimerAndThatOfItsDNSClient_Dispose()

        [Test]
        public async Task ANotificationSenderStopsItsTimerAndThatOfItsDNSClient_Dispose()
        {

            var httpAPI = NewHTTPAPI();

            try
            {

                TimerCount.AssertNoneLeft(
                    await TimerCount.Of(
                              () => new HTTPNotificationSender(httpAPI, HTTPHostname.Localhost),
                              sender => new ValueTask(Task.Run(sender.Dispose))
                          ),
                    "notification senders"
                );

            }
            finally
            {
                await httpAPI.HTTPServer.DisposeAsync();
            }

        }

        #endregion

        #region ANotificationSenderLeavesALentDNSClientAlone()

        [Test]
        public async Task ANotificationSenderLeavesALentDNSClientAlone()
        {

            var httpAPI    = NewHTTPAPI();
            var dnsClient  = new FakeDNSClient();

            try
            {

                await new HTTPNotificationSender(httpAPI, HTTPHostname.Localhost, DNSClient: dnsClient).DisposeAsync();
                await Task.Run(new HTTPNotificationSender(httpAPI, HTTPHostname.Localhost, DNSClient: dnsClient).Dispose);

                Assert.That(dnsClient.DisposeCount, Is.Zero, "the lent DNS client was disposed of");

            }
            finally
            {
                await httpAPI.HTTPServer.DisposeAsync();
            }

        }

        #endregion


        #region ANetworkServiceNodeStopsTheTimersOfItsHTTPServerAndDNSClient_DisposeAsync()

        /// <summary>
        /// The maintenance and Warden timers of the HTTP server the node made,
        /// and the cache timer of the DNS client it made for that server.
        /// </summary>
        [Test]
        public async Task ANetworkServiceNodeStopsTheTimersOfItsHTTPServerAndDNSClient_DisposeAsync()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new NetworkServiceNode(),
                             node => node.DisposeAsync()
                         ),
                   "network service nodes"
               );

        #endregion

        #region ANetworkServiceNodeStopsTheTimersOfItsHTTPServerAndDNSClient_Dispose()

        [Test]
        public async Task ANetworkServiceNodeStopsTheTimersOfItsHTTPServerAndDNSClient_Dispose()

            => TimerCount.AssertNoneLeft(
                   await TimerCount.Of(
                             () => new NetworkServiceNode(),
                             node => new ValueTask(Task.Run(node.Dispose))
                         ),
                   "network service nodes"
               );

        #endregion

        #region ANetworkServiceNodeLeavesALentDNSClientAlone()

        /// <summary>
        /// The node lends the DNS client it was lent on to the HTTP server it
        /// makes, and disposes of that server.
        /// </summary>
        [Test]
        public async Task ANetworkServiceNodeLeavesALentDNSClientAlone()
        {

            var dnsClient = new FakeDNSClient();

            await new NetworkServiceNode(DNSClient: dnsClient).DisposeAsync();
            await Task.Run(new NetworkServiceNode(DNSClient: dnsClient).Dispose);

            Assert.That(dnsClient.DisposeCount, Is.Zero, "the lent DNS client was disposed of");

        }

        #endregion

        #region ANetworkServiceNodeLeavesALentHTTPServerRunning()

        /// <summary>
        /// A node made on a running HTTP server, with an HTTP API of its own
        /// making or with a lent one, leaves the server running when disposed
        /// of.
        /// </summary>
        [Test]
        public async Task ANetworkServiceNodeLeavesALentHTTPServerRunning()
        {

            var httpServer = new HTTPServer(
                                 IPAddress:  IPv4Address.Localhost,
                                 TCPPort:    IPPort.Zero,
                                 AutoStart:  false
                             );

            try
            {

                await httpServer.Start();

                var node = new NetworkServiceNode(HTTPServer: httpServer);
                await node.DisposeAsync();

                await Task.Run(new NetworkServiceNode(HTTPServer: httpServer, DefaultHTTPAPI: node.DefaultHTTPAPI).Dispose);

                Assert.That(httpServer.IsRunning, Is.True, "the lent HTTP server is no longer running");

            }
            finally
            {
                await httpServer.DisposeAsync();
            }

        }

        #endregion

        #region ANetworkServiceNodeOnALentHTTPServerRunsNoTimer()

        /// <summary>
        /// A node made on a lent HTTP server, with a lent HTTP API, makes no
        /// timer at all. It used to make a DNS client beside the server, which
        /// nothing ever asked and nothing could dispose of: the DNS client of a
        /// node is that of its HTTP server.
        /// </summary>
        [Test]
        public async Task ANetworkServiceNodeOnALentHTTPServerRunsNoTimer()
        {

            var httpAPI = NewHTTPAPI();

            try
            {

                var timers = await TimerCount.Of(
                                       () => new NetworkServiceNode(
                                                 HTTPServer:      httpAPI.HTTPServer,
                                                 DefaultHTTPAPI:  httpAPI
                                             ),
                                       node => node.DisposeAsync()
                                   );

                // Parted at half the instances, as in TimerCount.AssertNoneLeft():
                // one timer per node stands out from what anything else starts
                // or stops meanwhile.
                Assert.Multiple(() => {

                    Assert.That(
                        timers.Alive,
                        Is.LessThan(TimerCount.Instances / 2),
                        $"timers running while {TimerCount.Instances} network service nodes on a lent HTTP server were alive"
                    );

                    Assert.That(
                        timers.Left,
                        Is.LessThan(TimerCount.Instances / 2),
                        $"timers still running after {TimerCount.Instances} network service nodes on a lent HTTP server were disposed of"
                    );

                });

            }
            finally
            {
                await httpAPI.HTTPServer.DisposeAsync();
            }

        }

        #endregion

    }

}
