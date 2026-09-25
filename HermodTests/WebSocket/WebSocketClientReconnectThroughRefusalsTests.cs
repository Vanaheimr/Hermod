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
    ///
    /// And a server that says when to come back, in a Retry-After, is not
    /// asked again before then - up to what the policy allows.
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


        #region AClientWaitsAsLongAsARetryAfterAsksInSeconds()

        /// <summary>
        /// A 503 that says "Retry-After: 2" is not asked again for two seconds -
        /// by a client whose own backoff would have asked again after a fifth of
        /// one.
        /// </summary>
        /// <remarks>
        /// The client read the status and not the header, so a server that said
        /// when it would be ready was asked again at the client's own pace -
        /// the thing it was asking not to happen, times every client it had.
        /// </remarks>
        [Test]
        public async Task AClientWaitsAsLongAsARetryAfterAsksInSeconds()
        {

            var port   = FreePort();
            var asked  = new List<DateTimeOffset>();

            await ConnectAndLoseIt(port);

            StartAgain(port, request => {
                lock (asked)
                {

                    asked.Add(DateTimeOffset.UtcNow);

                    return asked.Count == 1
                               ? new HTTPResponse.Builder(request) {
                                     HTTPStatusCode  = HTTPStatusCode.ServiceUnavailable,
                                     RetryAfter      = "2",
                                     Connection      = ConnectionType.Close
                                 }
                               : null;

                }
            });

            Assert.That(await Upgraded(TimeSpan.FromSeconds(10)), Is.True,
                        "The client did not come back after the server had asked for two seconds.");

            var gap = Gap(asked);

            Assert.That(gap, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)),
                        $"Asked to come back in two seconds, the client came back after {gap.TotalSeconds:F2} s.");

        }

        #endregion

        #region AClientWaitsAsLongAsARetryAfterAsksAsADate()

        /// <summary>
        /// The same, said as a date - counted from the server's own Date, so
        /// that a client whose clock is off still waits as long as it was asked
        /// to.
        /// </summary>
        [Test]
        public async Task AClientWaitsAsLongAsARetryAfterAsksAsADate()
        {

            var port   = FreePort();
            var asked  = new List<DateTimeOffset>();

            await ConnectAndLoseIt(port);

            StartAgain(port, request => {
                lock (asked)
                {

                    asked.Add(DateTimeOffset.UtcNow);

                    if (asked.Count > 1)
                        return null;

                    // Whole seconds, because that is all an HTTP date can say.
                    var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                    return new HTTPResponse.Builder(request) {
                               HTTPStatusCode  = HTTPStatusCode.ServiceUnavailable,
                               Date            = now,
                               RetryAfter      = now.AddSeconds(3).ToString("r", System.Globalization.CultureInfo.InvariantCulture),
                               Connection      = ConnectionType.Close
                           };

                }
            });

            Assert.That(await Upgraded(TimeSpan.FromSeconds(10)), Is.True,
                        "The client did not come back after the server had named a time three seconds on.");

            var gap = Gap(asked);

            Assert.That(gap, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2.9)),
                        $"Asked to come back three seconds on, the client came back after {gap.TotalSeconds:F2} s.");

        }

        #endregion

        #region AClientDoesNotWaitLongerThanItsPolicyAllows()

        /// <summary>
        /// A Retry-After of an hour is waited for as long as the policy allows,
        /// and then the server is asked again.
        /// </summary>
        [Test]
        public async Task AClientDoesNotWaitLongerThanItsPolicyAllows()
        {

            var port   = FreePort();
            var asked  = new List<DateTimeOffset>();

            await ConnectAndLoseIt(port, new WebSocketClientReconnectPolicy(
                                             InitialDelay:   TimeSpan.FromMilliseconds(200),
                                             MaxDelay:       TimeSpan.FromMilliseconds(500),
                                             JitterRatio:    0.0,
                                             MaxRetryAfter:  TimeSpan.FromSeconds(1)
                                         ));

            StartAgain(port, request => {
                lock (asked)
                {

                    asked.Add(DateTimeOffset.UtcNow);

                    return asked.Count == 1
                               ? new HTTPResponse.Builder(request) {
                                     HTTPStatusCode  = HTTPStatusCode.ServiceUnavailable,
                                     RetryAfter      = "3600",
                                     Connection      = ConnectionType.Close
                                 }
                               : null;

                }
            });

            Assert.That(await Upgraded(TimeSpan.FromSeconds(10)), Is.True,
                        "The client stayed away for as long as the server asked, rather than as long as its policy allows.");

            var gap = Gap(asked);

            Assert.That(gap, Is.InRange(TimeSpan.FromSeconds(0.9), TimeSpan.FromSeconds(3)),
                        $"With Retry-After allowed up to one second, the client came back after {gap.TotalSeconds:F2} s.");

        }

        #endregion


        #region (private) ConnectAndLoseIt(Port)

        /// <summary>
        /// The client, with a quick policy or the one given, connected to a
        /// server on the port - and the server stopped again.
        /// </summary>
        private async Task ConnectAndLoseIt(IPPort                           Port,
                                            WebSocketClientReconnectPolicy?  Policy   = null)
        {

            first   = new WebSocketMirrorServer(HTTPPort: Port, AutoStart: true);

            client  = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{Port}")) {
                          ReconnectPolicy = Policy ?? Quickly
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

            => StartAgain(Port, request => Refusal(request) is HTTPStatusCode status
                                               ? new HTTPResponse.Builder(request) {
                                                     HTTPStatusCode  = status,
                                                     Connection      = ConnectionType.Close
                                                 }
                                               : null);

        /// <summary>
        /// The same, with the whole answer to the upgrade named by the function,
        /// or the upgrade made where it names none.
        /// </summary>
        private void StartAgain(IPPort                                    Port,
                                Func<HTTPRequest, HTTPResponse.Builder?>  Answer)
        {

            lent        = new WebSocketMirrorServer(AutoStart: false);
            httpServer  = new HTTPServer(TCPPort: Port);

            var upgrade = WebSocketUpgrade.For(lent);

            httpServer.AddHTTPAPI().AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/"),
                HTTPDelegate: request => Answer(request) is HTTPResponse.Builder answer
                                             ? Task.FromResult(answer.AsImmutable)
                                             : upgrade(request)
            );

            httpServer.Start().GetAwaiter().GetResult();

        }

        #endregion

        #region (private static) Gap(Asked)

        /// <summary>
        /// How long the client left between the attempt that was turned away and
        /// the one after it, as the server saw them arrive.
        /// </summary>
        private static TimeSpan Gap(List<DateTimeOffset> Asked)
        {

            lock (Asked)
            {

                Assert.That(Asked, Has.Count.GreaterThanOrEqualTo(2),
                            "The client was not turned away and then let in, so this test tested nothing.");

                return Asked[1] - Asked[0];

            }

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
