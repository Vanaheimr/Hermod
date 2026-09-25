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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A client with a reconnect policy keeps trying through the answers a
    /// server gives while it is on its way back - and stops at one that says
    /// it will not be let in.
    /// </summary>
    /// <remarks>
    /// A back end that restarts behind a reverse proxy answers the first
    /// attempts with 502 or 503: the proxy is there, the service behind it is
    /// not yet. The client took any answer but 101 as the end - it cancelled
    /// its own networking token - so a station that reconnected a moment too
    /// early stayed away for good. 408, 429 and the 5xx are what a server says
    /// while it cannot yet; with a policy they are now a reason to try again
    /// after the backoff. Anything else - a 401, a 404 - still ends it: a
    /// client that is not wanted there is not helped by asking again every
    /// thirty seconds.
    /// </remarks>
    [TestFixture]
    public class WebSocketClientReconnectThroughRefusalsTests
    {

        #region Data

        private static WebSocketClientReconnectPolicy Quickly
            => new (InitialDelay: TimeSpan.FromMilliseconds(200),
                    MaxDelay:     TimeSpan.FromMilliseconds(500));

        private WebSocketMirrorServer?  first;
        private WebSocketMirrorServer?  lent;
        private HTTPServer?             httpServer;
        private WebSocketClient?        client;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (client is not null)
                await client.Close();

            if (first is not null)
                await first.Shutdown();

            if (lent is not null)
                await lent.Shutdown();

            if (httpServer is not null)
                await httpServer.Stop();

            client      = null;
            first       = null;
            lent        = null;
            httpServer  = null;

        }

        #endregion


        #region AClientComesBackThroughABackEndThatIsStillStarting()

        /// <summary>
        /// Two 503s on the way back, and then the upgrade.
        /// </summary>
        [Test]
        public async Task AClientComesBackThroughABackEndThatIsStillStarting()
        {

            var port     = FreePort();
            var refused  = 0;

            await ConnectAndLoseIt(port);

            StartAgain(port, request => Interlocked.Increment(ref refused) <= 2
                                            ? HTTPStatusCode.ServiceUnavailable
                                            : null);

            Assert.That(await Upgraded(TimeSpan.FromSeconds(10)), Is.True,
                        $"The client was refused {Math.Min(refused, 2)} time(s) with 503 on its way back and did not come back.");

            Assert.That(refused, Is.GreaterThanOrEqualTo(3),
                        "The client came back without being refused first, so this test tested nothing.");

        }

        #endregion

        #region AClientStopsAtABackEndThatWillNotHaveIt()

        /// <summary>
        /// A 404 on the way back is an answer, and the client stops asking.
        /// </summary>
        [Test]
        public async Task AClientStopsAtABackEndThatWillNotHaveIt()
        {

            var port   = FreePort();
            var asked  = 0;

            await ConnectAndLoseIt(port);

            StartAgain(port, request => { Interlocked.Increment(ref asked); return HTTPStatusCode.NotFound; });

            var giveUp = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);

            while (DateTimeOffset.UtcNow < giveUp && Volatile.Read(ref asked) == 0)
                await Task.Delay(50);

            Assert.That(asked, Is.EqualTo(1), "The client did not ask again at all, so this test tested nothing.");

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.That(asked, Is.EqualTo(1), "The client kept asking a back end that answered 404.");

        }

        #endregion


        #region (private) ConnectAndLoseIt(Port)

        /// <summary>
        /// The client, with a quick policy, connected to a server on the port -
        /// and the server stopped again.
        /// </summary>
        private async Task ConnectAndLoseIt(IPPort Port)
        {

            first   = new WebSocketMirrorServer(HTTPPort: Port, AutoStart: true);

            client  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{Port}")) {
                          ReconnectPolicy = Quickly
                      };

            await client.Connect();

            var giveUp = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);

            while (DateTimeOffset.UtcNow < giveUp && !first.WebSocketConnections.Any())
                await Task.Delay(50);

            Assert.That(first.WebSocketConnections.Any(), Is.True,
                        "The client did not get connected to begin with.");

            await first.Stop();

            first = null;

        }

        #endregion

        #region (private) StartAgain(Port, Refusal)

        /// <summary>
        /// A server on the port again: an HTTP server that answers the upgrade
        /// with the status the given function names, or upgrades it where the
        /// function names none.
        /// </summary>
        private void StartAgain(IPPort                             Port,
                                Func<HTTPRequest, HTTPStatusCode?>  Refusal)
        {

            lent        = new WebSocketMirrorServer(AutoStart: false);
            httpServer  = new HTTPServer(TCPPort: Port);

            var upgrade = WebSocketUpgrade.For(lent);

            httpServer.AddHTTPAPI().AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/"),
                HTTPDelegate: request => Refusal(request) is HTTPStatusCode status
                                             ? Task.FromResult(new HTTPResponse.Builder(request) {
                                                                   HTTPStatusCode  = status,
                                                                   Connection      = ConnectionType.Close
                                                               }.AsImmutable)
                                             : upgrade(request)
            );

            httpServer.Start().GetAwaiter().GetResult();

        }

        #endregion

        #region (private) Upgraded(Within)

        private async Task<Boolean> Upgraded(TimeSpan Within)
        {

            var giveUp = DateTimeOffset.UtcNow + Within;

            while (DateTimeOffset.UtcNow < giveUp)
            {

                if (lent!.WebSocketConnections.Any())
                    return true;

                await Task.Delay(50);

            }

            return false;

        }

        #endregion

        #region (private static) FreePort()

        private static IPPort FreePort()
        {

            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);

            listener.Start();

            try
            {
                return IPPort.Parse((UInt16) ((IPEndPoint) listener.LocalEndpoint).Port);
            }
            finally
            {
                listener.Stop();
            }

        }

        #endregion

    }

}
