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
    /// So Stop() waits for the ones still on their way onto the list and then
    /// looks at the list a second time, once the loop has ended and no more can
    /// come. The old Stop() did neither, and while it waited for its event
    /// streams, one after another, for up to fifteen seconds each, the loop went
    /// on taking connections in.
    ///
    /// The window is narrow, so this test holds it open. A connection's logger is
    /// made just before the connection is listed - on the thread pool, which is
    /// where the accept loop hands each connection it has accepted, so that no
    /// part of setting one up happens on the one thread that has to stay free to
    /// accept - and the logger factory here holds whoever asks for it until this
    /// test lets go. So what is held open is the gap between a connection being
    /// accepted and it being listed, whichever thread is crossing it.
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

            // Released only once Stop() has had its chance to come back without
            // us, and the asymmetry is the point: a Stop() that waits for this
            // connection cannot return before the release, so correct code always
            // spends the two seconds and always passes, while a Stop() that does
            // not wait returns on its own and is caught below. The two seconds are
            // the cost of the test, paid on every run.
            var returnedOnItsOwn = stopping == await Task.WhenAny(
                                                         stopping,
                                                         Task.Delay(TimeSpan.FromSeconds(2))
                                                     );

            gate.Release.Set();

            await stopping.WaitAsync(TimeSpan.FromSeconds(10));

            // The late connection's handler may not have got going at all: once
            // it is listed, Stop() looks at the list a second time, on another
            // thread, and may close the connection before its handler's first
            // line touches it - which then ends where a closed connection does.
            // Either way no handler is left running, and that is what is asked:
            // that every handler that started has finished. Asking for two
            // failed the nightly now and then, on a busy runner.
            Assert.Multiple(() => {
                Assert.That(returnedOnItsOwn,                 Is.False,                                "Stop() returned without waiting for the connection held between its accept and its listing");
                Assert.That(server.HandlersStarted,           Is.InRange(1, 2),                        "the first handler had started, and the late one where its connection was not closed first");
                Assert.That(server.HandlersFinished,          Is.EqualTo(server.HandlersStarted),      "every handler that started had finished when Stop() returned");
                Assert.That(server.NumberOfConnectedClients,  Is.Zero,                                 "no connection left on the server's books");
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
