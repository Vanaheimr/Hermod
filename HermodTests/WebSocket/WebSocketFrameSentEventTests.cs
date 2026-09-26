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

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// Every subscriber of OnWebSocketFrameSent is called and waited for, one
    /// after another, as the subscribers of the events raised next to it are.
    /// </summary>
    /// <remarks>
    /// The event used to be raised by calling it and waiting for what came
    /// back. A multicast delegate that returns a Task returns the task of its
    /// last subscriber, and only that one: the subscribers before it were
    /// started and never waited for. The send returned while they were still at
    /// work, and whatever they threw afterwards nobody ever saw. And a
    /// subscriber that threw before it had a task to return ended the call right
    /// there, so that the subscribers behind it were never called at all.
    ///
    /// Each test filters on text frames: the close frame at the end of a test
    /// raises the same event, and is none of its business.
    ///
    /// None of these run anywhere near the ten seconds the event used to be
    /// waited for. They do not have to: even inside those ten seconds, only the
    /// last subscriber was waited for at all, and the wait ended the moment
    /// the sender's token was cancelled. A bound of ten seconds alone, without
    /// the token, is the one thing they would not notice; that would take a
    /// test that waits the ten seconds out.
    /// </remarks>
    [TestFixture]
    public class WebSocketFrameSentEventTests
    {

        #region Data

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
                await server.Shutdown(Wait: true);

            client  = null;
            server  = null;

        }

        #endregion


        #region (private) Connect(LoggerFactory = null)

        /// <summary>
        /// Start a server, connect a client to it, and return the connection
        /// as the server sees it.
        /// </summary>
        /// <param name="LoggerFactory">An optional logger factory for the server.</param>
        private async Task<WebSocketServerConnection> Connect(ILoggerFactory? LoggerFactory = null)
        {

            server         = new WebSocketMirrorServer(HTTPPort:               IPPort.Zero,
                                                       RequireAuthentication:  false,
                                                       LoggerFactory:          LoggerFactory,
                                                       AutoStart:              true);

            var connected  = new TaskCompletionSource<WebSocketServerConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnNewWebSocketConnection += (timestamp, webSocketServer, connection, sharedSubprotocols, selectedSubprotocol, eventTrackingId, cancellationToken) => {
                connected.TrySetResult(connection);
                return Task.CompletedTask;
            };

            client         = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));

            await client.Connect().WaitAsync(TimeSpan.FromSeconds(10));

            // Raised once the 101 is out, which the client may notice first.
            return await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        }

        #endregion


        #region TheSendWaitsForEverySubscriberNotOnlyTheLast()

        /// <summary>
        /// A slow subscriber, and a fast one behind it: the send returns once
        /// both have finished, and the second is called once the first has.
        /// </summary>
        /// <remarks>
        /// Calling the event handed back the fast subscriber's task, finished
        /// before it was even looked at. So the send returned while the slow
        /// one had only just begun, and with it whatever the sender did next -
        /// the next frame, and the next round of the same subscribers, running
        /// alongside the one still at work.
        /// </remarks>
        [Test]
        public async Task TheSendWaitsForEverySubscriberNotOnlyTheLast()
        {

            var connection  = await Connect();
            var order       = new ConcurrentQueue<String>();

            server!.OnWebSocketFrameSent += async (timestamp, webSocketServer, webSocketConnection, eventTrackingId, frame, cancellationToken) => {
                if (frame.Opcode == WebSocketFrame.Opcodes.Text)
                {
                    order.Enqueue("first began");
                    // Long enough for a send that did not wait for this to have returned.
                    await Task.Delay(300, cancellationToken);
                    order.Enqueue("first finished");
                }
            };

            server.OnWebSocketFrameSent  += (timestamp, webSocketServer, webSocketConnection, eventTrackingId, frame, cancellationToken) => {
                if (frame.Opcode == WebSocketFrame.Opcodes.Text)
                    order.Enqueue("second");
                return Task.CompletedTask;
            };

            var sentStatus = await server.SendTextMessage(connection, "x");

            order.Enqueue("sent");

            Assert.Multiple(() => {

                Assert.That(sentStatus,  Is.EqualTo(SentStatus.Success));

                Assert.That(order,       Is.EqualTo(new[] { "first began", "first finished", "second", "sent" }),
                            "The send returned, or the second subscriber was called, before the first one had finished.");

            });

        }

        #endregion

        #region ASubscriberThatThrowsAtOnceDoesNotSilenceTheOnesBehindIt()

        /// <summary>
        /// A subscriber that throws before it has returned a task, and one
        /// behind it that is called all the same.
        /// </summary>
        /// <remarks>
        /// Not async, and that is the point: its exception comes out of the
        /// call itself rather than out of a task, while the invocation list is
        /// still being worked through - and ended it there.
        /// </remarks>
        [Test]
        public async Task ASubscriberThatThrowsAtOnceDoesNotSilenceTheOnesBehindIt()
        {

            var connection    = await Connect();
            var secondCalled  = false;

            server!.OnWebSocketFrameSent += (timestamp, webSocketServer, webSocketConnection, eventTrackingId, frame, cancellationToken)

                => frame.Opcode == WebSocketFrame.Opcodes.Text
                       ? throw new InvalidOperationException("This subscriber is broken.")
                       : Task.CompletedTask;

            server.OnWebSocketFrameSent  += (timestamp, webSocketServer, webSocketConnection, eventTrackingId, frame, cancellationToken) => {
                if (frame.Opcode == WebSocketFrame.Opcodes.Text)
                    secondCalled = true;
                return Task.CompletedTask;
            };

            var sentStatus = await server.SendTextMessage(connection, "x");

            Assert.Multiple(() => {

                Assert.That(sentStatus,    Is.EqualTo(SentStatus.Success));

                Assert.That(secondCalled,  Is.True,
                            "A subscriber that threw kept the one behind it from being called.");

            });

        }

        #endregion

        #region WhatASubscriberThrowsLaterIsReportedNotLost()

        /// <summary>
        /// A subscriber that fails after its first await, with another one
        /// behind it: what it threw is in the server's log, and the one behind
        /// it is called all the same.
        /// </summary>
        /// <remarks>
        /// With another one behind it, because it was the last subscriber's
        /// task that calling the event handed back, and the only one anybody
        /// looked at. How the ones before it ended, nobody ever asked.
        ///
        /// And called all the same, because waiting for each subscriber in turn
        /// inside a single try would report this failure - and end the round
        /// there, one subscriber deciding for the ones behind it again.
        /// </remarks>
        [Test]
        public async Task WhatASubscriberThrowsLaterIsReportedNotLost()
        {

            var log           = new ErrorLog();
            var connection    = await Connect(log);
            var failed        = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondCalled  = false;

            server!.OnWebSocketFrameSent += async (timestamp, webSocketServer, webSocketConnection, eventTrackingId, frame, cancellationToken) => {
                if (frame.Opcode == WebSocketFrame.Opcodes.Text)
                {
                    try
                    {
                        await Task.Delay(100, cancellationToken);
                        throw new InvalidOperationException("This subscriber failed after its first await.");
                    }
                    finally
                    {
                        failed.TrySetResult();
                    }
                }
            };

            server.OnWebSocketFrameSent  += (timestamp, webSocketServer, webSocketConnection, eventTrackingId, frame, cancellationToken) => {
                if (frame.Opcode == WebSocketFrame.Opcodes.Text)
                    secondCalled = true;
                return Task.CompletedTask;
            };

            await server.SendTextMessage(connection, "x");

            // Whether or not the send waited for it: by now it has failed.
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {

                Assert.That(log.Errors,    Has.Some.Contains("This subscriber failed after its first await."),
                            "What a subscriber threw after its first await was never looked at.");

                Assert.That(secondCalled,  Is.True,
                            "A subscriber that failed after its first await kept the one behind it from being called.");

            });

        }

        #endregion

        #region ASendWhoseTokenIsCancelledStillWaitsForItsSubscribers()

        /// <summary>
        /// A send whose token is cancelled while a subscriber is still at work
        /// returns once that subscriber has finished, and not before.
        /// </summary>
        /// <remarks>
        /// The ten seconds the event used to be waited for came with the
        /// sender's token, and the wait gave up the moment that token was
        /// cancelled - the wait, not the subscriber, which went on. For a
        /// keep-alive ping that token is the connection loop's own, and it is
        /// cancelled when the connection ends: the loop was on its way out,
        /// and the subscriber still at work with a token whose owner was done
        /// with it.
        ///
        /// This subscriber does not stop when the token tells it to, which a
        /// subscriber is free not to do: it may be halfway through writing
        /// down what it was told, and finishing that is the right thing to do.
        /// A bound of ten seconds per subscriber, with the same token, would
        /// fail this just the same.
        /// </remarks>
        [Test]
        public async Task ASendWhoseTokenIsCancelledStillWaitsForItsSubscribers()
        {

            var connection  = await Connect();
            var order       = new ConcurrentQueue<String>();
            var began       = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cancellationTokenSource = new CancellationTokenSource();

            server!.OnWebSocketFrameSent += async (timestamp, webSocketServer, webSocketConnection, eventTrackingId, frame, cancellationToken) => {
                if (frame.Opcode == WebSocketFrame.Opcodes.Text)
                {
                    order.Enqueue("began");
                    began.TrySetResult();
                    // Finishes what it has begun, whatever becomes of the token.
                    await Task.Delay(300, CancellationToken.None);
                    order.Enqueue("finished");
                }
            };

            var sending = server.SendTextMessage(connection, "x", CancellationToken: cancellationTokenSource.Token);

            // The sender gives up while the subscriber is at work.
            await began.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellationTokenSource.Cancel();

            await sending;

            order.Enqueue("returned");

            Assert.That(order,  Is.EqualTo(new[] { "began", "finished", "returned" }),
                        "The send returned while a subscriber was still at work.");

        }

        #endregion


        #region (private class) ErrorLog

        /// <summary>
        /// Keeps what was logged at error level, with the message of the
        /// exception that came with it.
        /// </summary>
        private sealed class ErrorLog : ILoggerFactory
        {

            private readonly ConcurrentQueue<String> errors = new();

            /// <summary>
            /// Everything logged at error level or above so far.
            /// </summary>
            public IEnumerable<String> Errors
                => errors.ToArray();

            public ILogger CreateLogger(String CategoryName)
                => new ErrorLogger(errors);

            public void AddProvider(ILoggerProvider Provider)
            { }

            public void Dispose()
            { }


            private sealed class ErrorLogger(ConcurrentQueue<String> Errors) : ILogger
            {

                public IDisposable? BeginScope<TState>(TState state)
                    where TState : notnull

                    => null;

                public Boolean IsEnabled(LogLevel logLevel)
                    => logLevel >= LogLevel.Error;

                public void Log<TState>(LogLevel                          logLevel,
                                        EventId                           eventId,
                                        TState                            state,
                                        Exception?                        exception,
                                        Func<TState, Exception?, String>  formatter)
                {
                    if (logLevel >= LogLevel.Error)
                        Errors.Enqueue($"{formatter(state, exception)} <<{exception?.GetType().Name}: {exception?.Message}>>");
                }

            }

        }

        #endregion

    }

}
