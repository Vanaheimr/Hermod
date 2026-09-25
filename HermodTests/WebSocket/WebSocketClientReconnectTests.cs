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
    /// A client with a reconnect policy comes back when its server goes away
    /// and comes back - and a client without one, or one its application
    /// closed, does not.
    /// </summary>
    /// <remarks>
    /// The policy was only ever consulted for a connection that failed on its
    /// way up. One that was established and then ended by the other side - a
    /// close frame, a socket closed underneath, pings no longer answered - left
    /// a ClientCloseMessage behind, and the loop took that for a close its own
    /// application had asked for: the client stopped, whatever its policy said.
    /// A charging station whose CSMS restarted stayed off the network until
    /// somebody restarted the station too.
    /// </remarks>
    [TestFixture]
    public class WebSocketClientReconnectTests
    {

        #region Data

        /// <summary>
        /// Reconnect attempts close together, so that a test does not wait on
        /// the default second.
        /// </summary>
        private static WebSocketClientReconnectPolicy Quickly
            => new (InitialDelay: TimeSpan.FromMilliseconds(200),
                    MaxDelay:     TimeSpan.FromMilliseconds(500));

        private WebSocketMirrorServer?  server;
        private WebSocketClient?        client;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (client is not null)
                await client.Close();

            if (server is not null)
                await server.Shutdown();

            client  = null;
            server  = null;

        }

        #endregion


        #region AClientWithAPolicyComesBackWhenItsServerIsShutDownAndStartedAgain()

        /// <summary>
        /// A server shut down tells its clients with a close frame; the client
        /// answers it, and comes back once the server is there again.
        /// </summary>
        [Test]
        public async Task AClientWithAPolicyComesBackWhenItsServerIsShutDownAndStartedAgain()
        {

            var port = FreePort();

            await Connect(port, Quickly);

            await server!.Shutdown("Restarting.");

            Assert.That(await ComesBack(port, TimeSpan.FromSeconds(10)), Is.True,
                        "The server was shut down and started again, and the client did not come back.");

        }

        #endregion

        #region AClientWithAPolicyComesBackWhenItsServerIsStoppedAndStartedAgain()

        /// <summary>
        /// A server stopped closes the sockets and says nothing; the client
        /// comes back all the same.
        /// </summary>
        [Test]
        public async Task AClientWithAPolicyComesBackWhenItsServerIsStoppedAndStartedAgain()
        {

            var port = FreePort();

            await Connect(port, Quickly);

            await server!.Stop();

            Assert.That(await ComesBack(port, TimeSpan.FromSeconds(10)), Is.True,
                        "The server was stopped and started again, and the client did not come back.");

        }

        #endregion

        #region AClientWithoutAPolicyStaysAway()

        /// <summary>
        /// No policy, no reconnect: that is what leaving it null says.
        /// </summary>
        [Test]
        public async Task AClientWithoutAPolicyStaysAway()
        {

            var port = FreePort();

            await Connect(port, null);

            await server!.Shutdown();

            Assert.That(await ComesBack(port, TimeSpan.FromSeconds(3)), Is.False,
                        "A client without a reconnect policy reconnected.");

        }

        #endregion

        #region AClientItsApplicationClosedStaysAway()

        /// <summary>
        /// A close the application asked for is not a loss, whatever the
        /// policy says - and the server is still there to be reconnected to.
        /// </summary>
        [Test]
        public async Task AClientItsApplicationClosedStaysAway()
        {

            var port = FreePort();

            await Connect(port, Quickly);

            await client!.Close();

            var giveUp = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(3);

            while (DateTimeOffset.UtcNow < giveUp && server!.WebSocketConnections.Any())
                await Task.Delay(50);

            Assert.That(server!.WebSocketConnections.Any(), Is.False,
                        "The client closed by its application was still connected.");

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.That(server.WebSocketConnections.Any(), Is.False,
                        "The client closed by its application came back.");

        }

        #endregion


        #region AClientWithAPolicyComesBackAfterItsServerStayedAwayForAWhile()

        /// <summary>
        /// The server is gone for a while - long enough for several attempts
        /// to find nothing listening - and the client comes back all the same.
        /// </summary>
        /// <remarks>
        /// The first attempt that found nothing listening ended the client: the
        /// answer to it was built from a request there was not yet, and the
        /// exception that made escaped the catch it was built in. A server that
        /// was back before the first attempt was reached; one that took longer,
        /// which is every real restart, was not.
        /// </remarks>
        [Test]
        public async Task AClientWithAPolicyComesBackAfterItsServerStayedAwayForAWhile()
        {

            var port      = FreePort();
            var attempts  = 0;

            await Connect(port, Quickly);

            client!.OnReconnecting += (timestamp, sender, attempt, delay, cancellationToken) => {
                Interlocked.Increment(ref attempts);
                return Task.CompletedTask;
            };

            await server!.Stop();

            // At least three attempts into the silence, however the backoff
            // falls.
            var giveUp = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);

            while (DateTimeOffset.UtcNow < giveUp && Volatile.Read(ref attempts) < 3)
                await Task.Delay(50);

            Assert.That(attempts, Is.GreaterThanOrEqualTo(3),
                        $"The client made {attempts} attempt(s) while its server was away, and stopped trying.");

            Assert.That(await ComesBack(port, TimeSpan.FromSeconds(10)), Is.True,
                        "The server came back after a while, and the client did not.");

        }

        #endregion

        #region AClientWithAPolicyIsToldOfItsFirstAttemptAndGoesOnTrying()

        /// <summary>
        /// A client with a policy whose first attempt finds nothing listening
        /// is told so at once, and connects by itself once there is something
        /// to connect to.
        /// </summary>
        /// <remarks>
        /// Connect() waited for an answer for as long as the policy kept trying,
        /// up to the request timeout - ten minutes by default. A caller that
        /// sets a policy before the first attempt, so that a server that is not
        /// there yet is reached when it is, was held for all of that: a
        /// charging station started while its CSMS was down would not have
        /// finished starting.
        /// </remarks>
        [Test]
        public async Task AClientWithAPolicyIsToldOfItsFirstAttemptAndGoesOnTrying()
        {

            var port   = FreePort();

            client     = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{port}"),
                                             RequestTimeout: TimeSpan.FromSeconds(20)) {
                             ReconnectPolicy = Quickly
                         };

            var took   = System.Diagnostics.Stopwatch.StartNew();
            var (_, response) = await client.Connect();
            took.Stop();

            Assert.Multiple(() => {
                Assert.That(took.Elapsed,                    Is.LessThan(TimeSpan.FromSeconds(5)),
                            "Connect() held its caller while the policy went on trying.");
                Assert.That(response.HTTPStatusCode.Code,    Is.Not.EqualTo(101),
                            "There was nothing to connect to, and the first attempt said it had connected.");
            });

            Assert.That(await ComesBack(port, TimeSpan.FromSeconds(10)), Is.True,
                        "The client did not connect by itself once the server was there.");

        }

        #endregion

        #region AClientSaysWhetherItKeepsTrying()

        /// <summary>
        /// After a first attempt that failed, a client says whether it goes on:
        /// with a policy, it does; without one, it does not; and closed, it
        /// does not either.
        /// </summary>
        [Test]
        public async Task AClientSaysWhetherItKeepsTrying()
        {

            var withPolicy     = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{FreePort()}")) { ReconnectPolicy = Quickly };
            var withoutPolicy  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{FreePort()}"));

            try
            {

                await withPolicy.   Connect();
                await withoutPolicy.Connect();

                Assert.Multiple(() => {
                    Assert.That(withPolicy.   KeepsTrying, Is.True,  "A client with a policy whose first attempt failed said it had given up.");
                    Assert.That(withoutPolicy.KeepsTrying, Is.False, "A client without a policy whose first attempt failed said it kept trying.");
                });

                await withPolicy.Close();

                Assert.That(withPolicy.KeepsTrying, Is.False, "A client that was closed said it kept trying.");

            }
            finally
            {
                await withPolicy.   Close();
                await withoutPolicy.Close();
            }

        }

        #endregion


        #region (private) Connect(Port, Policy)

        /// <summary>
        /// A server on the port, and the client connected to it.
        /// </summary>
        private async Task Connect(IPPort                           Port,
                                   WebSocketClientReconnectPolicy?  Policy)
        {

            server  = new WebSocketMirrorServer(HTTPPort: Port, AutoStart: true);

            client  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{Port}")) {
                          ReconnectPolicy = Policy
                      };

            await client.Connect();

            var giveUp = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);

            while (DateTimeOffset.UtcNow < giveUp && !server.WebSocketConnections.Any())
                await Task.Delay(50);

            Assert.That(server.WebSocketConnections.Any(), Is.True,
                        "The client did not get connected to begin with.");

        }

        #endregion

        #region (private) ComesBack(Port, Within)

        /// <summary>
        /// Whether the client connects to a server started again on the port,
        /// within the given time. The server is the test's from then on.
        /// </summary>
        private async Task<Boolean> ComesBack(IPPort    Port,
                                              TimeSpan  Within)
        {

            server = new WebSocketMirrorServer(HTTPPort: Port, AutoStart: true);

            var giveUp = DateTimeOffset.UtcNow + Within;

            while (DateTimeOffset.UtcNow < giveUp)
            {

                if (server.WebSocketConnections.Any())
                    return true;

                await Task.Delay(50);

            }

            return false;

        }

        #endregion

        #region (private static) FreePort()

        /// <summary>
        /// A TCP port nobody was listening on a moment ago.
        /// </summary>
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
