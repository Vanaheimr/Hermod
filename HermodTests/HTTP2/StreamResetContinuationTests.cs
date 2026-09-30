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

using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// A reset cancels its stream's token, and whatever waited with that token
    /// goes on. Not on the thread that made the reset: the writer loop resets a
    /// stream whose write failed, and the read loop one the client resets with
    /// RST_STREAM, and each of them sends or reads for every stream of the
    /// connection. A handler going on there held them up for as long as it ran.
    ///
    /// Found by the CI of 470081d9, red on both runners since: a tunnel's
    /// handler, whose write the writer loop had failed and reset, went on inside
    /// that reset, down to the end of its tunnel's reading, and waited there for
    /// the receive-window lock, before the writer loop had sent its RST_STREAM.
    /// ServerTunnelDataAfterResetTests.TunnelWriteFailsAsAChunkArrives holds that
    /// lock while it waits for the RST_STREAM, and waited 10 seconds for it; a
    /// dump of a run that hung had the handler's code on the writer loop's stack,
    /// under HTTP2Stream.Reset and the token's callbacks.
    /// </summary>
    [TestFixture]
    public class StreamResetContinuationTests
    {

        #region (helpers)

        /// <summary>
        /// A handler waiting for something with its stream's token - its next
        /// write, as HTTP2Tunnel.WriteAsync waits for one - which, once that is
        /// cancelled, goes on to wait for what the resetting thread may hold: the
        /// receive-window lock of the writer loop, here a gate.
        /// </summary>
        private static async Task Handler(Task                  Wait,
                                          ManualResetEventSlim  Gate,
                                          StrongBox<Int32>      WentOnOn)
        {

            try
            {
                await Wait.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                WentOnOn.Value = Environment.CurrentManagedThreadId;
                Gate.Wait(PipedH2ServerConnection.StepTimeout);
            }

        }

        #endregion


        #region AHandlerCancelledByAResetGoesOnOffTheResettingThread(ByPeer)

        /// <summary>
        /// A handler waiting with its stream's token goes on elsewhere once the
        /// stream is reset, by us or by the client, and the reset returns without
        /// waiting for it. The token is cancelled when it does.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void AHandlerCancelledByAResetGoesOnOffTheResettingThread(Boolean ByPeer)
        {

            var stream = new HTTP2Stream(1, 65535, 65535);
            stream.Open();

            using var gate      = new ManualResetEventSlim(false);
            var       wentOnOn  = new StrongBox<Int32>(0);

            // Called here, the handler runs up to its wait and leaves its
            // continuation on the wait: nothing of it is left to happen before
            // the reset.
            var handler         = Handler(new TaskCompletionSource().Task.WaitAsync(stream.CancellationToken), gate, wentOnOn);

            var resetOn         = 0;
            var resetter        = new Thread(() => {

                                      resetOn = Environment.CurrentManagedThreadId;

                                      if (ByPeer)
                                          stream.ResetByPeer();
                                      else
                                          stream.Reset();

                                  }) { IsBackground = true };

            resetter.Start();

            var returned        = resetter.Join(TimeSpan.FromSeconds(5));
            var cancelled       = stream.CancellationToken.IsCancellationRequested;

            gate.Set();
            handler.Wait(PipedH2ServerConnection.StepTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(returned,        Is.True,                "the reset, which waited for the handler it had cancelled");
                Assert.That(cancelled,       Is.True,                "the stream's token, once the reset returned");
                Assert.That(wentOnOn.Value,  Is.Not.EqualTo(resetOn), "the thread the handler went on on: the one that reset its stream");
            });

        }

        #endregion

    }

}
