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

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A server that takes the connection and the upgrade request and then
    /// says nothing at all does not hold the client for ever.
    /// </summary>
    /// <remarks>
    /// A hung process, a load balancer with nothing behind it: the socket is
    /// accepted, and no answer ever comes. The client's deadline for the
    /// answer to its upgrade - five seconds, where the request names none -
    /// was looked at after each byte it read, and a server that sends no byte
    /// never let it be looked at. So the attempt waited for as long as the
    /// socket stayed open, Connect() held its caller until its own request
    /// timeout - ten minutes by default - and a client with a reconnect policy
    /// never tried again, because the attempt it would have tried again after
    /// never ended.
    /// </remarks>
    [TestFixture]
    public class WebSocketClientSilentServerTests
    {

        #region Data

        private TcpListener?            listener;
        private readonly List<Socket>   taken      = [];
        private WebSocketClient?        client;

        #endregion

        #region SetUp / TearDown

        /// <summary>
        /// A server that accepts every connection, reads what it is sent, and
        /// never writes a byte.
        /// </summary>
        [SetUp]
        public void Silence()
        {

            listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            _ = Task.Run(async () => {
                while (true)
                {

                    Socket socket;

                    try
                    {
                        socket = await listener.AcceptSocketAsync();
                    }
                    catch
                    {
                        return;
                    }

                    lock (taken)
                        taken.Add(socket);

                    _ = Task.Run(async () => {
                        var buffer = new Byte[4096];
                        try
                        {
                            while (await socket.ReceiveAsync(buffer) > 0) { }
                        }
                        catch
                        { }
                    });

                }
            });

        }

        [TearDown]
        public async Task TearDown()
        {

            if (client is not null)
                await client.Close();

            client = null;

            listener?.Stop();

            lock (taken)
            {
                foreach (var socket in taken)
                {
                    try { socket.Close(); } catch { }
                }
                taken.Clear();
            }

        }

        #endregion


        #region AnUpgradeNobodyAnswersEndsAndIsTriedAgain()

        /// <summary>
        /// With a reconnect policy: Connect() answers when the deadline for the
        /// upgrade has passed, and the client tries again by itself.
        /// </summary>
        [Test]
        public async Task AnUpgradeNobodyAnswersEndsAndIsTriedAgain()
        {

            client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{Port}/silent"),
                                         RequestTimeout: TimeSpan.FromSeconds(20)) {
                         ReconnectPolicy = new WebSocketClientReconnectPolicy(
                                               InitialDelay:  TimeSpan.FromMilliseconds(200),
                                               MaxDelay:      TimeSpan.FromMilliseconds(500)
                                           )
                     };

            var took = Stopwatch.StartNew();
            var (_, response) = await client.Connect();
            took.Stop();

            Assert.Multiple(() => {
                Assert.That(took.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)),
                            $"Connect() held its caller for {took.Elapsed.TotalSeconds:F1} s for a server that never answered.");
                Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.RequestTimeout),
                            "The first attempt did not end with its deadline.");
            });

            Assert.That(await Taken(2, TimeSpan.FromSeconds(10)), Is.True,
                        "The client never tried again after an upgrade nobody answered.");

            Assert.That(client.KeepsTrying, Is.True);

        }

        #endregion

        #region AnUpgradeNobodyAnswersEndsWithoutAPolicyToo()

        /// <summary>
        /// Without one: Connect() answers when the deadline has passed, and
        /// that is the end of it.
        /// </summary>
        [Test]
        public async Task AnUpgradeNobodyAnswersEndsWithoutAPolicyToo()
        {

            client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{Port}/silent"),
                                         RequestTimeout: TimeSpan.FromSeconds(20));

            var took = Stopwatch.StartNew();
            var (_, response) = await client.Connect();
            took.Stop();

            Assert.Multiple(() => {
                Assert.That(took.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)),
                            $"Connect() held its caller for {took.Elapsed.TotalSeconds:F1} s for a server that never answered.");
                Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.RequestTimeout),
                            "The attempt did not end with its deadline.");
                Assert.That(client.KeepsTrying, Is.False,
                            "A client without a policy said it went on trying.");
            });

        }

        #endregion


        #region (private) Port

        private UInt16 Port

            => (UInt16) ((IPEndPoint) listener!.LocalEndpoint).Port;

        #endregion

        #region (private) Taken(Count, Within)

        /// <summary>
        /// Whether the server has been asked for that many connections, within
        /// the time given.
        /// </summary>
        private async Task<Boolean> Taken(Int32 Count, TimeSpan Within)
        {

            var giveUp = DateTimeOffset.UtcNow + Within;

            while (DateTimeOffset.UtcNow < giveUp)
            {

                lock (taken)
                    if (taken.Count >= Count)
                        return true;

                await Task.Delay(50);

            }

            return false;

        }

        #endregion

    }

}
