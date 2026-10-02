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

using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// A connect that is closed while it is still pending says that it was
    /// closed, and leaves a connect started after the close alone.
    /// </summary>
    /// <remarks>
    /// Close() and CloseConnection() take the TcpClient out of the field - set
    /// it to null - and then close its socket. A connect still pending then
    /// failed, and its finally read the field again to dispose of the client.
    /// It found null, and the NullReferenceException of that read replaced
    /// whatever the connect had failed with: "Error connecting ATCPClient:
    /// Object reference not set to an instance of an object.", in every run,
    /// on Windows and on Linux alike.
    ///
    /// A connect started right after the close fared worse. By the time the
    /// closed one got to its finally, the field held the new connect's client,
    /// which that finally then disposed, and the new connect failed with it.
    /// The closed connect reported anything but the close: "Connection
    /// timeout!" most often, the NullReferenceException above, or the
    /// cancellation itself. And where the closed connect's catch cleared the
    /// resolved addresses just as the new one was choosing from them, the new
    /// one failed with "The given enumeration must not be null or empty!".
    ///
    /// A connect stays pending here because nobody makes room for it: the
    /// listener never accepts, and one connection fills its backlog of none.
    /// Linux drops the SYN of every connect after that one, and the connect
    /// waits for an answer until it gives up. Windows answers it
    /// with a reset, and the connect tries again for about two seconds before
    /// it reports the refusal - time enough, since every close here comes a
    /// fifth of a second in. No address outside the machine is needed, and
    /// none is reached. Where a connect has ended before its close all the
    /// same, the test is inconclusive rather than passed: what it could say
    /// then is about the platform, not about the close.
    /// </remarks>
    [TestFixture]
    public class CloseWhileConnectingTests
    {

        #region Data

        /// <summary>
        /// How long a connect is left pending before it is closed.
        /// </summary>
        private static readonly TimeSpan  WhilePending  = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// The timeout of the connects that are closed: far beyond the close.
        /// </summary>
        private static readonly TimeSpan  LongTimeout   = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How long a test waits for a connect to report, once it may.
        /// </summary>
        private static readonly TimeSpan  Patience      = TimeSpan.FromSeconds(10);

        #endregion

        #region (private) FullListener

        /// <summary>
        /// A listener on 127.0.0.1 that never accepts, with a backlog of none
        /// that one connection fills. Every connect after that one stays
        /// pending - see the remarks above.
        /// </summary>
        private sealed class FullListener : IDisposable
        {

            private readonly Socket  listener;
            private readonly Socket  filler;

            public URL  URL  { get; }

            private FullListener(Socket  Listener,
                                 Socket  Filler)
            {

                this.listener  = Listener;
                this.filler    = Filler;
                this.URL       = URL.Parse($"tcp://127.0.0.1:{((IPEndPoint) Listener.LocalEndPoint!).Port}");

            }

            public static async Task<FullListener> Start()
            {

                var listener  = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var filler    = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {

                    listener.Bind(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
                    listener.Listen(0);

                    // There is room for this one, on Windows and on Linux alike,
                    // and none for any after it.
                    await filler.ConnectAsync(listener.LocalEndPoint!).WaitAsync(Patience);

                    return new FullListener(listener, filler);

                }
                catch
                {
                    filler.  Dispose();
                    listener.Dispose();
                    throw;
                }

            }

            public void Dispose()
            {
                filler.  Dispose();
                listener.Dispose();
            }

        }

        #endregion

        #region (private static) Describe(Result)

        private static String Describe(TCPConnectionResult Result)

            => Result.IsSuccess
                   ? "success"
                   : Result.Errors.Select(error => error.ToString()).AggregateWith(" | ");

        #endregion

        #region (private static) LeavePending(Connect, What)

        /// <summary>
        /// Leave the connect pending for a while, and make sure that it still is.
        /// </summary>
        private static async Task LeavePending(Task<TCPConnectionResult>  Connect,
                                               String                     What)
        {

            await Task.Delay(WhilePending);

            if (Connect.IsCompleted)
                Assert.Inconclusive($"{What} ended before it could be closed, with: {Describe(await Connect)}");

        }

        #endregion


        #region AConnectClosedWhilePendingSaysSo()

        /// <summary>
        /// Close(), which also cancels the client's token. Which of the two the
        /// connect fails with - the socket closed under it, or the token - is up
        /// to timing: in a probe of 20 closes on each, the token came first 14
        /// times on Windows and 15 times on Linux.
        /// </summary>
        [Test]
        public async Task AConnectClosedWhilePendingSaysSo()
        {

            using       var listener   = await FullListener.Start();

            // Lent to the client: one it makes itself outlives it.
            await using var dnsClient  = new DNSClient(ManualDNSServers: []);

            await using var client     = new TCPClient(listener.URL,
                                                       ConnectTimeout:  LongTimeout,
                                                       DNSClient:       dnsClient);

            var connect = client.ConnectAsync();

            await LeavePending(connect, "The connect");

            await client.Close();

            Assert.That(Describe(await connect.WaitAsync(Patience)), Is.EqualTo("Closed while connecting!"));

        }

        #endregion

        #region AConnectWhoseConnectionIsClosedWhilePendingSaysSo()

        /// <summary>
        /// CloseConnection(), which leaves the client's token alone: the connect
        /// fails with the socket closed under it, every time.
        /// </summary>
        [Test]
        public async Task AConnectWhoseConnectionIsClosedWhilePendingSaysSo()
        {

            using       var listener   = await FullListener.Start();
            await using var dnsClient  = new DNSClient(ManualDNSServers: []);
            await using var client     = new TCPClient(listener.URL,
                                                       ConnectTimeout:  LongTimeout,
                                                       DNSClient:       dnsClient);

            var connect = client.ConnectAsync();

            await LeavePending(connect, "The connect");

            await client.CloseConnection();

            Assert.That(Describe(await connect.WaitAsync(Patience)), Is.EqualTo("Closed while connecting!"));

        }

        #endregion

        #region AConnectRightAfterACloseIsLeftAloneByTheOneClosed()

        /// <summary>
        /// A caller that closes a client to connect it anew, at once. The connect
        /// that was closed says so, and the new one stays pending until it is
        /// closed itself - rather than failing with a client that the one closed
        /// disposed under it.
        /// </summary>
        [Test]
        public async Task AConnectRightAfterACloseIsLeftAloneByTheOneClosed()
        {

            using       var listener   = await FullListener.Start();
            await using var dnsClient  = new DNSClient(ManualDNSServers: []);
            await using var client     = new TCPClient(listener.URL,
                                                       ConnectTimeout:  LongTimeout,
                                                       DNSClient:       dnsClient);

            var first        = client.ConnectAsync();

            await LeavePending(first, "The first connect");

            await client.Close();

            var second       = client.ConnectAsync();

            var firstResult  = await first.WaitAsync(Patience);

            // Time for whatever the first connect did on its way out to show:
            // the dispose of a client that was not its own ended the second
            // connect within milliseconds.
            await Task.Delay(WhilePending);

            var secondEnded  = second.IsCompleted
                                   ? Describe(await second)
                                   : null;

            await client.Close();

            var secondResult = await second.WaitAsync(Patience);

            Assert.Multiple(() => {

                Assert.That(Describe(firstResult),   Is.EqualTo("Closed while connecting!"),  "what the first connect reported");
                Assert.That(secondEnded,             Is.Null,                                 "how the second connect ended before it was closed itself");
                Assert.That(Describe(secondResult),  Is.EqualTo("Closed while connecting!"),  "what the second connect reported");

            });

        }

        #endregion

        #region AConnectThatTimesOutStillSaysSo()

        /// <summary>
        /// What a close is told apart from: the connect's own timeout, which
        /// cancels the connect as Close() does.
        /// </summary>
        [Test]
        public async Task AConnectThatTimesOutStillSaysSo()
        {

            var connectTimeout = TimeSpan.FromMilliseconds(200);

            using       var listener   = await FullListener.Start();
            await using var dnsClient  = new DNSClient(ManualDNSServers: []);
            await using var client     = new TCPClient(listener.URL,
                                                       ConnectTimeout:  connectTimeout,
                                                       DNSClient:       dnsClient);

            var stopwatch  = Stopwatch.StartNew();
            var result     = await client.ConnectAsync().WaitAsync(Patience);

            Assert.That(Describe(result), Is.EqualTo("Connection timeout!"), $"after {stopwatch.ElapsedMilliseconds} ms, with a timeout of {connectTimeout.TotalMilliseconds} ms");

        }

        #endregion

    }

}
