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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// <c>TCPConnection.IsConnectionClosed()</c> and what it can and cannot tell you.
    ///
    /// This is a characterization test: it asserts a DEFECT, deliberately, the way
    /// the curl matrix asserts an expected failure. The predicate is
    ///
    ///     socket.Poll(0, SelectMode.SelectRead) &amp;&amp; socket.Available == 0
    ///
    /// which is the textbook "has the peer vanished" idiom and a race against any
    /// concurrent reader: Poll is true when the socket is readable — data arrived
    /// OR the peer closed — and Available is what separates the two. A reader that
    /// takes the bytes in between turns a live connection into a false "closed".
    ///
    /// That is HTTP1ConformanceTests finding H-25. The TCP server's Warden used
    /// this to decide what to reap and closed connections in the middle of a
    /// transfer, about one Autobahn run in four; it now reaps on the handler task
    /// instead. This test is why, and it is here because the end-to-end evidence
    /// could not carry the argument on its own: with the Warden forced to a
    /// one-second period the defect still only showed in one forced run out of
    /// four, which is too close to the base rate to prove anything either way.
    ///
    /// Sampled directly, it usually takes milliseconds - but only usually.
    /// Whether a reader lands in the few instructions between Poll and Available
    /// is up to the scheduler, and on a busy runner it can take longer than the
    /// time given: EVCLI's nightly run 36115581323 sampled a Debian container
    /// for twenty seconds without one false reading, and a test that documents
    /// a defect was read as a regression. So a run that catches no lie is
    /// inconclusive rather than failed; it proves nothing either way.
    ///
    /// Only a change to TCPConnection could make the predicate stop lying, and
    /// that would be news rather than breakage: the Warden could go back to
    /// asking it.
    /// </summary>
    [TestFixture]
    public class ConnectionLivenessTests
    {

        #region IsConnectionClosedLiesWhileAnotherReaderIsDraining()

        [Test]
        public async Task IsConnectionClosedLiesWhileAnotherReaderIsDraining()
        {

            // A real server with a real handler, rather than a hand-made
            // TCPConnection: the point is a connection somebody OWNS and is
            // reading, which is the only situation in which the predicate is
            // wrong. The echo server's handler reads in a loop, which is exactly
            // the shape of AWebSocketServer's and AHTTPServer's.
            var server = new TCPEchoTestServer(
                             TCPPort:  IPPort.Parse(0)
                         );

            await server.Start();

            var port = server.TCPPort;

            using var peer = new TcpClient();
            await peer.ConnectAsync(System.Net.IPAddress.Loopback, port.ToUInt16());

            using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            // Keep the peer talking, so the handler keeps reading.
            var sender = Task.Run(async () => {
                var payload = new Byte[16 * 1024];
                var stream  = peer.GetStream();
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        await stream.WriteAsync(payload, cts.Token);
                        // Drain the echo, or the peer's own receive buffer fills
                        // and the server's writes block — which would stop the
                        // reading this test is about.
                        _ = stream.ReadAsync(payload, cts.Token);
                    }
                }
                catch { }
            });

            // Wait for the server to have accepted it.
            TCPConnection? connection = null;
            while (!cts.IsCancellationRequested && connection is null)
            {
                connection = server.ClientConnections.FirstOrDefault();
                if (connection is null)
                    await Task.Delay(10, CancellationToken.None);
            }

            Assert.That(connection, Is.Not.Null, "the server never accepted the connection");

            var samples         = 0L;
            var falsePositives  = 0L;

            while (!cts.IsCancellationRequested && falsePositives == 0)
            {
                samples++;
                if (connection!.IsConnectionClosed())
                    falsePositives++;
            }

            var stillConnected = peer.Client.Connected;

            await cts.CancelAsync();
            try { await sender; } catch { }
            await server.Stop();

            // The connection really was alive throughout — otherwise a
            // "closed" reading would simply have been correct and this test
            // would prove nothing at all.
            Assert.That(stillConnected, Is.True, "the peer's end was still connected");

            // Not catching the lie within the time given says nothing about the
            // predicate, only about where the scheduler happened to put the two
            // readers: a skip in the results rather than a red run.
            if (falsePositives == 0)
                Assert.Inconclusive(
                    $"IsConnectionClosed() did not lie in {samples} samples against a live, " +
                     "actively-read connection within the time given. That proves nothing either way: " +
                     "see this fixture's summary before changing anything."
                );

        }

        #endregion

    }

}
