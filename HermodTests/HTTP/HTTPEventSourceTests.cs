/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Affero GPL license, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/agpl.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// What a client of an event source is entitled to: everything the source
    /// still remembers, everything from now on, each of them once, in order.
    /// </summary>
    /// <remarks>
    /// The interesting part is the seam between those two. A client asks for
    /// the remembered events and for the live ones in a single call, and
    /// whatever happens while it is being served has to end up on one side of
    /// that seam or the other - never in between and never on both.
    /// </remarks>
    public class HTTPEventSourceTests
    {

        #region Data

        private HTTPServer  httpServer   = default!;
        private HTTPAPI     httpAPI      = default!;

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void CreateAnAPI()
        {

            // Not started: an event source needs an API to belong to and never
            // asks it anything, so nothing here has to listen on a port.
            httpServer  = new HTTPServer(TCPPort: IPPort.Parse(0));
            httpAPI     = new HTTPAPI(httpServer);

        }

        [TearDown]
        public async Task StopTheServer()
        {
            await httpServer.Stop();
        }

        #endregion


        #region AnEventSubmittedWhileTheHistoryIsReplayedStillArrives()

        /// <summary>
        /// The seam, and the reason this file exists.
        /// </summary>
        /// <remarks>
        /// A client is served its remembered events one at a time, as it reads
        /// them, and the queue they come from hands out a moment-in-time
        /// snapshot - so an event submitted while that replay is running cannot
        /// arrive that way. It has to arrive over the subscription, which means
        /// the subscription has to exist before the replay starts.
        ///
        /// Driving that deterministically takes one more turn of the screw than
        /// it looks. Submitting an event only writes it to a channel; a
        /// background fan-out picks it up a moment later and writes it to the
        /// clients that exist *then*. Releasing the replay straight after
        /// submitting therefore races the fan-out, and a client that subscribes
        /// afterwards often still wins - which is how this test passed against
        /// the broken order the first time it was written.
        ///
        /// So a second client is kept subscribed throughout and used as the
        /// starting gun: once it has the event, the fan-out has demonstrably
        /// been past, and a subscription made after that point is too late by
        /// construction rather than by luck.
        /// </remarks>
        [Test]
        public async Task AnEventSubmittedWhileTheHistoryIsReplayedStillArrives()
        {

            var source = httpAPI.AddEventSource<String>(
                             HTTPEventSource_Id.Parse("seam"),
                             MaxNumberOfCachedEvents:  100,
                             EnableLogging:            false
                         );

            for (var i = 1; i <= 10; i++)
                await source.SubmitEvent("test", $"remembered {i}");

            const String duringTheReplay = "submitted while the history was being replayed";

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            #region The witness: a client that is already subscribed and reading

            var witnessSawIt = new TaskCompletionSource();

            var witness = Task.Run(async () => {
                await foreach (var httpEvent in source.GetAllEventsGreater("the-witness", 0, cancellation.Token))
                {
                    if (httpEvent.Data == duringTheReplay)
                    {
                        witnessSawIt.TrySetResult();
                        break;
                    }
                }
            }, cancellation.Token);

            #endregion

            var delivered     = new List<String>();
            var insideReplay  = new TaskCompletionSource();
            var goOn          = new TaskCompletionSource();

            var consumer = Task.Run(async () => {

                await foreach (var httpEvent in source.GetAllEventsGreater("a-client", 0, cancellation.Token))
                {

                    delivered.Add(httpEvent.Data);

                    // Held here, in the middle of the replay, until the event
                    // has been submitted *and* the fan-out has been past it.
                    if (delivered.Count == 1)
                    {
                        insideReplay.TrySetResult();
                        await goOn.Task;
                    }

                    if (httpEvent.Data == duringTheReplay)
                        break;

                }

            }, cancellation.Token);

            await insideReplay.Task.WaitAsync(TimeSpan.FromSeconds(30));

            await source.SubmitEvent("test", duringTheReplay);

            // The starting gun: the fan-out has now written this event to every
            // client that was subscribed. Ours is either among them or has lost
            // it for good.
            await witnessSawIt.Task.WaitAsync(TimeSpan.FromSeconds(30));

            goOn.TrySetResult();

            var finished = await Task.WhenAny(consumer, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.That(finished, Is.SameAs(consumer),
                        "The client never saw the event submitted while its history was being replayed, and waited for it for ever.");

            await consumer;
            await witness;

            Assert.Multiple(() => {

                Assert.That(delivered, Does.Contain(duringTheReplay));

                // And the ten it was promised, still in order and still once
                // each - closing the window must not have opened a second one.
                Assert.That(delivered.Take(10),
                            Is.EqualTo(Enumerable.Range(1, 10).Select(i => $"remembered {i}")));

                Assert.That(delivered.Distinct().Count(), Is.EqualTo(delivered.Count),
                            "Something was delivered twice.");

            });

        }

        #endregion

        #region AClientIsNotToldWhatItSaysItAlreadyHas()

        /// <summary>
        /// Last-Event-Id is a browser saying how far it got; everything up to
        /// and including it is not sent again.
        /// </summary>
        [Test]
        public async Task AClientIsNotToldWhatItSaysItAlreadyHas()
        {

            var source = httpAPI.AddEventSource<String>(
                             HTTPEventSource_Id.Parse("resume"),
                             MaxNumberOfCachedEvents:  100,
                             EnableLogging:            false
                         );

            for (var i = 1; i <= 5; i++)
                await source.SubmitEvent("test", $"remembered {i}");

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // A first look, to find out what the source called them: the ids
            // are its own and nothing outside it is entitled to guess them.
            var remembered = new List<(UInt64 Id, String Data)>();

            await foreach (var httpEvent in source.GetAllEventsGreater("first-look", 0, cancellation.Token))
            {
                remembered.Add((httpEvent.Id, httpEvent.Data));
                if (remembered.Count == 5)
                    break;
            }

            Assert.That(remembered, Has.Count.EqualTo(5), "The source did not remember what it was given.");

            var resumeAfter = remembered[2].Id;
            var delivered   = new List<String>();

            var consumer = Task.Run(async () => {
                await foreach (var httpEvent in source.GetAllEventsGreater("a-client", resumeAfter, cancellation.Token))
                {
                    delivered.Add(httpEvent.Data);
                    if (delivered.Count == 2)
                        break;
                }
            }, cancellation.Token);

            await consumer.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.That(delivered, Is.EqualTo(new[] { "remembered 4", "remembered 5" }),
                        "A client that said where it got to was sent the wrong part of the history.");

        }

        #endregion

        #region AClientThatGivesUpDuringTheReplayLeavesNothingBehind()

        /// <summary>
        /// Subscribing before the replay means there is a channel to let go of
        /// even when the client never gets as far as the live events.
        /// </summary>
        [Test]
        public async Task AClientThatGivesUpDuringTheReplayLeavesNothingBehind()
        {

            var source = httpAPI.AddEventSource<String>(
                             HTTPEventSource_Id.Parse("giveup"),
                             MaxNumberOfCachedEvents:  100,
                             EnableLogging:            false
                         );

            for (var i = 1; i <= 10; i++)
                await source.SubmitEvent("test", $"remembered {i}");

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var _ in source.GetAllEventsGreater("a-client", 0, cancellation.Token))
            {
                // One, and then away - the enumerator is disposed by leaving
                // the loop, which is what has to take the subscription with it.
                break;
            }

            Assert.That(source.NumberOfConnectedClients, Is.EqualTo(0),
                        "A client that stopped reading during the replay is still counted as connected.");

        }

        #endregion

    }

}
