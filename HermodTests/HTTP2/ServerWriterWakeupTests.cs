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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The DATA writer loop's wakeup. The loop scans the open streams, then
    /// picks one with something queued under the flow-control lock, and if
    /// there is none it waits for the next signal. It used to take that signal
    /// under the lock, after the scan. A stream that opened and queued its DATA
    /// in between had signalled already: SignalWriterWakeup completes the
    /// current signal and arms a fresh one, and the fresh one is what the loop
    /// then took, while the new stream was not among those it had scanned. It
    /// slept with that DATA queued until something unrelated woke it. On a
    /// connection with nothing else going on, nothing did, and the response
    /// waited for the idle timeout to end the connection: h2bench, one request
    /// at a time, hung that way in about one run in six.
    ///
    /// The loop takes the signal before the scan now. A stream that queues
    /// after the scan completes that very signal, and the loop goes on at once.
    /// </summary>
    [TestFixture]
    public class ServerWriterWakeupTests
    {

        #region (helpers)

        private static readonly TaskCreationOptions Async = TaskCreationOptions.RunContinuationsAsynchronously;

        #endregion


        #region DataQueuedBetweenScanAndPick_GoesOut()

        /// <summary>
        /// Stream 1 has written its first chunk, and the writer loop goes on to
        /// its next pick, which the test holds at the flow-control lock. Then
        /// stream 3 opens, and its handler's write is queued under that lock,
        /// before the loop gets it: past the scan in the old order of things,
        /// before it in the new. Its DATA goes out.
        /// </summary>
        [Test]
        public async Task DataQueuedBetweenScanAndPick_GoesOut()
        {

            var lateResponse  = new TaskCompletionSource<IHTTP2ResponseStream>(Async);
            var handlersDone  = new TaskCompletionSource(Async);

            await using var peer = await PipedH2ServerConnection.StartAsync(

                (streamId, headers, body, cancellationToken) => throw new InvalidOperationException("Every request is streamed here"),

                StreamingHandler: async (request, response, cancellationToken) => {

                    await response.WriteHeadersAsync([(":status", "200")]);

                    if (request.Headers.First(header => header.Name == ":path").Value == "/first")
                        await response.WriteAsync(Encoding.ASCII.GetBytes("chunk 1"));
                    else
                        lateResponse.TrySetResult(response);

                    await handlersDone.Task;

                });

            try
            {

                using var flow = await peer.HoldWriterAfterDataAsync(1, () => peer.RequestAsync(1, "/first"));

                await peer.RequestAsync(3, "/late");

                var response = await lateResponse.Task.WaitAsync(PipedH2ServerConnection.StepTimeout);

                // Queued, and its signal given, while the loop waits for the lock.
                Task? late = null;

                flow.LetGo(() => late = response.WriteAsync(Encoding.ASCII.GetBytes("late")));

                Assert.That(await Task.WhenAny(late!, Task.Delay(PipedH2ServerConnection.StepTimeout)), Is.SameAs(late),
                            "the DATA stream 3 queued while the writer loop was on its way from the scan to the pick went out");

            }
            finally
            {
                handlersDone.TrySetResult();
            }

        }

        #endregion

    }

}
