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

using System.Net.Sockets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// When Stop() returns, the handler of every connection has finished - that
    /// of a connection it found listed, and that of one the accept loop took in
    /// while Stop() was closing the others.
    /// </summary>
    /// <remarks>
    /// Stop() closes the connections it finds listed, then cancels the server's
    /// token and waits for the accept loop to end. A connection the loop had
    /// accepted by then, but not yet listed, is listed afterwards, and its
    /// handler gets going with nobody to close its connection or to wait for it.
    /// So Stop() looks at the list a second time once the loop has ended, when no
    /// more can come. The old Stop() did not, and while it waited for its event
    /// streams, one after another, for up to fifteen seconds each, the loop went
    /// on taking connections in.
    ///
    /// The window is narrow, so this test holds it open. The accept loop makes
    /// each connection's logger before it lists the connection, and the logger
    /// factory here holds it there until Stop() has looked.
    ///
    /// The handlers take a moment to finish once their connections have closed,
    /// as handlers that log or tidy up do. A Stop() that closed the connections
    /// and returned without waiting would otherwise pass now and then: its
    /// handlers often finished first.
    /// </remarks>
    [TestFixture]
    public class StopWaitsForEveryHandlerTests
    {

        #region EveryHandlerHasFinishedWhenStopReturns()

        [Test]
        public async Task EveryHandlerHasFinishedWhenStopReturns()
        {

            using var gate          = new AcceptGate();

            await using var server  = new ReadUntilClosedServer(gate);

            await server.Start();

            // One connection listed as usual, with its handler reading.
            using var listed = new TcpClient();
            await listed.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToUInt16());

            Assert.That(await Eventually(() => server.HandlersStarted == 1, TimeSpan.FromSeconds(10)),
                        Is.True,
                        "the first connection's handler started");

            // And one that the accept loop has, but holds before it lists it.
            gate.Armed = true;

            using var late = new TcpClient();
            await late.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToUInt16());

            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Up to its wait for the accept loop: it has closed the first
            // connection, found nothing else to close, and cancelled the token.
            var stopping = server.Stop();

            gate.Release.Set();

            await stopping.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Multiple(() => {
                Assert.That(server.HandlersStarted,           Is.EqualTo(2),  "both handlers had started");
                Assert.That(server.HandlersFinished,          Is.EqualTo(2),  "and had finished when Stop() returned");
                Assert.That(server.NumberOfConnectedClients,  Is.Zero,        "no connection left on the server's books");
            });

        }

        #endregion


        #region (private static) Eventually(Condition, Within)

        private static async Task<Boolean> Eventually(Func<Boolean>  Condition,
                                                      TimeSpan       Within)
        {

            using var timeout = new CancellationTokenSource(Within);

            while (!Condition())
            {

                if (timeout.IsCancellationRequested)
                    return false;

                await Task.Delay(20);

            }

            return true;

        }

        #endregion

        #region (class) ReadUntilClosedServer

        /// <summary>
        /// A server whose handler reads its connection until it is closed, and
        /// does not look at the server's token: only closing the connection ends
        /// it. It then takes a fifth of a second to finish.
        /// </summary>
        private sealed class ReadUntilClosedServer(ILoggerFactory LoggerFactory)

            : ATCPServer(IPAddress:                IPv4Address.Localhost,
                         TCPPort:                  IPPort.Zero,
                         DisableMaintenanceTasks:  true,
                         DisableWardenTasks:       true,
                         LoggerFactory:            LoggerFactory)

        {

            private Int32 handlersStarted;
            private Int32 handlersFinished;

            public Int32 HandlersStarted
                => Volatile.Read(ref handlersStarted);

            public Int32 HandlersFinished
                => Volatile.Read(ref handlersFinished);

            protected override async Task HandleConnection(TCPConnection      Connection,
                                                           CancellationToken  Token)
            {

                Interlocked.Increment(ref handlersStarted);

                try
                {

                    var stream = Connection.TCPClient.GetStream();
                    var buffer = new Byte[64];

                    while (await stream.ReadAsync(buffer) > 0)
                    { }

                }
                catch
                { }

                await Task.Delay(TimeSpan.FromMilliseconds(200));

                Interlocked.Increment(ref handlersFinished);

            }

        }

        #endregion

        #region (class) AcceptGate

        /// <summary>
        /// A logger factory that, once armed, holds whoever asks it for the logger
        /// of a TCP connection until it is released: the accept loop, right before
        /// it lists a connection it has accepted.
        /// </summary>
        private sealed class AcceptGate : ILoggerFactory
        {

            public volatile Boolean     Armed;
            public TaskCompletionSource Reached  { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);
            public ManualResetEventSlim Release  { get; } = new (false);

            public ILogger CreateLogger(String CategoryName)
            {

                if (Armed && CategoryName.EndsWith(nameof(TCPConnection), StringComparison.Ordinal))
                {
                    Reached.TrySetResult();
                    Release.Wait(TimeSpan.FromSeconds(10));
                }

                return NullLogger.Instance;

            }

            public void AddProvider(ILoggerProvider Provider)
            { }

            public void Dispose()
            {
                Release.Set();
                Release.Dispose();
            }

        }

        #endregion

    }

}
