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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// Streams still open when their connection ends. A handler is given its
    /// stream's token, not the connection's, and the connection cancelled only
    /// its own token when it ended: no stream was reset. So once the client had
    /// gone away, a streaming handler waiting for request-body data waited for
    /// good, even with its own token, and a handler that checks its token ran on.
    ///
    /// The end of a connection now resets every stream still open on it, as the
    /// peer's RST_STREAM would: the handler's token is cancelled, and a body read
    /// fails with it.
    /// </summary>
    [TestFixture]
    public class ServerConnectionEndTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        /// <summary>
        /// End the connection, as the client does that goes away without a word,
        /// or the server that stops, and wait until it has ended.
        /// </summary>
        private static async Task EndAsync(PipedH2ServerConnection Peer, Boolean ClientGoesAway)
        {

            if (ClientGoesAway)
                await Peer.DisconnectAsync();
            else
                await Peer.DisposeAsync();

            await Peer.Ended.WaitAsync(PipedH2ServerConnection.StepTimeout);

        }

        /// <summary>
        /// How a task ended: whether it did within the step timeout, and with
        /// which exception, if any.
        /// </summary>
        private static async Task<(Boolean Ended, Exception? Failure)> EndOf(Task Awaited)
        {

            if (await Task.WhenAny(Awaited, Task.Delay(PipedH2ServerConnection.StepTimeout)) != Awaited)
                return (false, null);

            try
            {
                await Awaited;
                return (true, null);
            }
            catch (Exception e)
            {
                return (true, e);
            }

        }

        /// <summary>
        /// The token a cancellation carries, or null for any other outcome.
        /// </summary>
        private static CancellationToken? TokenOf(Exception? Failure)

            => (Failure as OperationCanceledException)?.CancellationToken;

        #endregion


        #region ReadWaitingAtConnectionEnd_FailsWithTheStreamsToken(PassHandlerToken, ClientGoesAway)

        /// <summary>
        /// A streaming handler waits for request-body data when the connection
        /// ends: the client goes away, or the server stops. Whether the handler
        /// passed its token to the read or not, the read fails, with that token,
        /// once the connection has ended.
        /// </summary>
        [TestCase(false, true)]
        [TestCase(true,  true)]
        [TestCase(false, false)]
        [TestCase(true,  false)]
        public async Task ReadWaitingAtConnectionEnd_FailsWithTheStreamsToken(Boolean PassHandlerToken, Boolean ClientGoesAway)
        {

            var readWaiting = new TaskCompletionSource<(Task<Byte[]?> Read, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    var read = request.ReadAsync(PassHandlerToken ? cancellationToken : default).AsTask();

                    readWaiting.TrySetResult((read, cancellationToken));

                    await read;

                });

            await peer.RequestAsync(1, "/upload", EndStream: false);

            var (read, handlerToken) = await readWaiting.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            Assert.That(read.IsCompleted, Is.False, "the read, while the client sends nothing");

            await EndAsync(peer, ClientGoesAway);

            var (ended, failure) = await EndOf(read);

            Assert.Multiple(() =>
            {

                Assert.That(ended,                                 Is.True,                                     "the read returned once the connection had ended");
                Assert.That(failure,                               Is.InstanceOf<OperationCanceledException>(), "how the read ended");
                Assert.That(TokenOf(failure),                      Is.EqualTo(handlerToken),                   "the token it carries: the handler's, the stream's own");
                Assert.That(handlerToken.IsCancellationRequested,  Is.True,                                    "the handler's token");

            });

        }

        #endregion

        #region HandlerToken_CancelledWhenTheConnectionEnds(ClientGoesAway)

        /// <summary>
        /// A request handler still at work when the connection ends, one that
        /// observes its token as it should, is told to stop: its token is
        /// cancelled. It used to run on for a client that was gone.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task HandlerToken_CancelledWhenTheConnectionEnds(Boolean ClientGoesAway)
        {

            var working  = new TaskCompletionSource<CancellationToken>(Async);
            var stopped  = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                async (streamId, headers, body, cancellationToken) => {

                    working.TrySetResult(cancellationToken);

                    try
                    {
                        await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
                    }
                    finally
                    {
                        stopped.TrySetResult();
                    }

                    return ([(":status", "200")], Encoding.ASCII.GetBytes("never"));

                });

            await peer.RequestAsync(1, "/slow");

            var handlerToken = await working.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            await EndAsync(peer, ClientGoesAway);

            var (ended, _) = await EndOf(stopped.Task);

            Assert.Multiple(() =>
            {

                Assert.That(handlerToken.IsCancellationRequested,  Is.True, "the handler's token, once the connection had ended");
                Assert.That(ended,                                 Is.True, "the handler, stopped by its token");

            });

        }

        #endregion

        #region CallbackThrowsOnOneStream_OthersReleasedAnyway()

        /// <summary>
        /// A callback on one handler's token throws when the connection ends and
        /// the stream is reset: Cancel() passes that on. The streams after it are
        /// reset all the same, and a read waiting on one of them fails.
        /// </summary>
        [Test]
        public async Task CallbackThrowsOnOneStream_OthersReleasedAnyway()
        {

            var callbackRegistered  = new TaskCompletionSource(Async);
            var readWaiting         = new TaskCompletionSource<(Task<Byte[]?> Read, CancellationToken HandlerToken)>(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    if (request.Headers.First(header => header.Name == ":path").Value == "/throwing")
                    {

                        cancellationToken.Register(() => throw new InvalidOperationException("The callback failed on purpose"));
                        callbackRegistered.TrySetResult();

                        // Released by its own token, which the reset still cancels.
                        await request.ReadAsync(cancellationToken);
                        return;

                    }

                    var read = request.ReadAsync().AsTask();

                    readWaiting.TrySetResult((read, cancellationToken));

                    await read;

                });

            // Opened first, and reset first.
            await peer.RequestAsync(1, "/throwing", EndStream: false);
            await callbackRegistered.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            await peer.RequestAsync(3, "/waiting",  EndStream: false);

            var (read, handlerToken) = await readWaiting.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

            await EndAsync(peer, ClientGoesAway: true);

            var (ended, failure) = await EndOf(read);

            Assert.Multiple(() =>
            {

                Assert.That(ended,             Is.True,                                     "the read on the other stream returned once the connection had ended");
                Assert.That(failure,           Is.InstanceOf<OperationCanceledException>(), "how that read ended");
                Assert.That(TokenOf(failure),  Is.EqualTo(handlerToken),                   "the token it carries: its handler's");

            });

        }

        #endregion

    }

}
