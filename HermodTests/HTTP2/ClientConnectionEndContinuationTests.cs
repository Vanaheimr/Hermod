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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The end of a client connection cancels its token, and whatever waited
    /// with that token goes on — a write waiting for window, say, and the
    /// caller's code after it. Not on the thread that ends the connection: the
    /// read loop, once the server has gone away, which still has to tell
    /// everyone waiting for the connection's end, a pool among them. Cancel()
    /// ran every continuation waiting with the token right there, inside the
    /// read loop's end, and the connection was not over until the caller's code
    /// was done. The client's loops now cancel with CancelAsync(), as
    /// HTTP2Stream.Reset does for a stream's token since 513797ae (see
    /// StreamResetContinuationTests).
    /// </summary>
    [TestFixture]
    public class ClientConnectionEndContinuationTests
    {

        #region (helpers)

        /// <summary>
        /// A caller's code: a write, and once it fails, something that takes its
        /// time — waits on a gate here.
        /// </summary>
        private static async Task Writer(Task                  Write,
                                         ManualResetEventSlim  Gate,
                                         ManualResetEventSlim  WentOn,
                                         ManualResetEventSlim  Returned)
        {

            try
            {
                await Write.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                WentOn.Set();
                Gate.Wait(HoldingH2Transport.StepTimeout);
            }
            finally
            {
                Returned.Set();
            }

        }

        #endregion


        #region AWriteWaitingWhenTheConnectionEnds_GoesOnOffTheReadLoop()

        /// <summary>
        /// A write waits for window, which the server never grants, and then the
        /// server goes away. The read loop finds the end of the stream and ends
        /// the connection: <see cref="HTTP2ClientConnection.Closed"/> completes
        /// while the writer's code, which goes on from the write's failure, still
        /// runs, elsewhere. It used to complete only once that code was done, which
        /// ran on the read loop's thread, inside the end of the connection.
        /// </summary>
        [Test]
        public async Task AWriteWaitingWhenTheConnectionEnds_GoesOnOffTheReadLoop()
        {

            await using var transport = new HoldingH2Transport(HoldHeadersOf: streamId => false);

            var connection = await transport.ConnectAsync(new HTTP2ClientOptions());

            // No window for any new stream: a write waits for it.
            await transport.SendAsync(HTTP2Frame.CreateSettings((HTTP2SettingsParameter.INITIAL_WINDOW_SIZE, 0)));

            var upload = await connection.StartStreamingRequestAsync(HTTPMethod.POST, URIScheme.http, "localhost", "/upload").
                                          WaitAsync(HoldingH2Transport.StepTimeout);

            using var gate      = new ManualResetEventSlim(false);
            using var wentOn    = new ManualResetEventSlim(false);
            using var returned  = new ManualResetEventSlim(false);

            // Started on the thread pool, as a handler's code runs, with no context
            // to post continuations to; and left waiting: by the time this returns,
            // the write waits, and the writer waits for it.
            var writer = await Task.Factory.StartNew(() => Writer(upload.WriteAsync(Encoding.ASCII.GetBytes("waits for window")), gate, wentOn, returned),
                                                     CancellationToken.None,
                                                     TaskCreationOptions.None,
                                                     TaskScheduler.Default);

            await transport.EndServerSideAsync();

            // Long enough for a writer on the read loop's thread to give up on the
            // gate first, and so to tell the two apart without a race.
            var closed          = await Task.WhenAny(connection.Closed, Task.Delay(2 * HoldingH2Transport.StepTimeout)) == connection.Closed;
            var stillRunning    = !returned.IsSet;

            var wentOnInTime    = wentOn.Wait(HoldingH2Transport.StepTimeout);

            gate.Set();

            var writerDone      = await Task.WhenAny(writer, Task.Delay(HoldingH2Transport.StepTimeout)) == writer;

            Assert.Multiple(() =>
            {
                Assert.That(closed,        Is.True,  "the connection ended");
                Assert.That(stillRunning,  Is.True,  "the writer's code, still running when the connection had ended");
                Assert.That(wentOnInTime,  Is.True,  "the writer went on from the write's failure");
                Assert.That(writerDone,    Is.True,  "the writer, done once the gate opened");
            });

        }

        #endregion

    }

}
