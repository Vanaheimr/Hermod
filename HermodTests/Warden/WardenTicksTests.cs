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

using System.Diagnostics;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Warden
{

    using Warden = org.GraphDefined.Vanaheimr.Warden.Warden;

    /// <summary>
    /// A Warden's tick starts its checks and holds no thread while they are at
    /// work, and leaves a tick out while the checks of the tick before are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every TCP server has a Warden, and a server that was stopped but never
    /// disposed keeps it ticking. Its tick used to wait on a thread of the
    /// pool - with GetResult() on a Task.Run - for the checks, and they waited
    /// once more with Task.WaitAll for a check whose result somebody consumes,
    /// while what they waited for needed the pool to get done. A charging
    /// station's tests leave a few hundred such servers behind, and on GitHub's
    /// runners, with four cores, their ticks took every thread of the pool:
    /// tests that take a second took fifty, and three ran out of time.
    /// </para>
    /// <para>
    /// The checks here have a consumer of their result, because only those
    /// are waited for: a check without one is started and left to itself.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class WardenTicksTests
    {

        #region Data

        /// <summary>
        /// How often the Wardens of these tests tick: often enough for many
        /// ticks to come and go while a test waits.
        /// </summary>
        private static readonly TimeSpan  Often     = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// How long a test waits for what should happen at the next tick or
        /// two before it says it did not.
        /// </summary>
        private static readonly TimeSpan  Patience  = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Name servers written down rather than looked for: a Warden handed
        /// none makes itself a resolver, which asks the operating system for
        /// its network adapters, and these tests make many Wardens. Nothing is
        /// resolved.
        /// </summary>
        private static readonly DNSClient Resolver  = new ([
                                                          new DNSServerConfig(IPv4Address.Parse("192.0.2.53"), IPPort.DNS)
                                                      ]);

        private readonly List<Warden>                  wardens  = [];
        private          TaskCompletionSource<Object>  gate     = default!;

        #endregion

        #region SetUp / TearDown

        /// <summary>
        /// A gate of its own for each test: what the checks of a test wait for
        /// until the test or its end opens it.
        /// </summary>
        [SetUp]
        public void MakeAGate()
        {
            gate = new (TaskCreationOptions.RunContinuationsAsynchronously);
        }

        [TearDown]
        public async Task DisposeTheWardens()
        {

            gate.TrySetResult(new Object());

            foreach (var warden in wardens)
                await warden.DisposeAsync();

            wardens.Clear();

        }

        #endregion

        #region (private) NewWarden(Id, Check)

        /// <summary>
        /// A Warden ticking often, with one check that counts itself and then
        /// does what the test gives it to do, and a consumer of its result, so
        /// that the Warden waits for it.
        /// </summary>
        /// <param name="Id">The Warden's id.</param>
        /// <param name="Check">What the check does once it has counted itself.</param>
        /// <param name="Started">How often the check was started.</param>
        private Warden NewWarden(String           Id,
                                 Func<Task>       Check,
                                 out Func<Int32>  Started)
        {

            var started  = new Int32[1];
            var warden   = new Warden(Id, InitialDelay: Often, CheckEvery: Often, DNSClient: Resolver);

            warden.Check<Boolean>(
                (timestamp, properties) => true,
                TimeSpan.Zero,
                async (timestamp, cancellationToken) => {
                    Interlocked.Increment(ref started[0]);
                    await Check();
                    return true;
                },
                result => { }
            );

            wardens.Add(warden);

            Started = () => Volatile.Read(ref started[0]);

            return warden;

        }

        #endregion

        #region (private static) Within(Patience, Condition)

        /// <summary>
        /// Whether the condition comes true within the given time.
        /// </summary>
        private static async Task<Boolean> Within(TimeSpan       Patience,
                                                  Func<Boolean>  Condition)
        {

            var watch = Stopwatch.StartNew();

            while (!Condition())
            {

                if (watch.Elapsed > Patience)
                    return false;

                await Task.Delay(10);

            }

            return true;

        }

        #endregion


        #region ATickIsLeftOutWhileTheChecksBeforeAreStillAtWork()

        /// <summary>
        /// While the checks of one tick are still at work, the ticks after it
        /// are left out, and the one after they are done starts them again.
        /// </summary>
        [Test]
        public async Task ATickIsLeftOutWhileTheChecksBeforeAreStillAtWork()
        {

            NewWarden("waiting", () => gate.Task, out var started);

            Assert.That(await Within(Patience, () => started() == 1), Is.True, "the first tick started the check");

            await Task.Delay(Often * 15);

            Assert.That(started(), Is.EqualTo(1), "the ticks while it was at work were left out");

            gate.TrySetResult(new Object());

            Assert.That(await Within(Patience, () => started() > 1), Is.True, "a tick after it was done started it again");

        }

        #endregion

        #region ACheckThatThrowsKeepsNoTickFromComing()

        /// <summary>
        /// A check that fails is written down, and the ticks after it run the
        /// checks again: nothing is left holding the Warden's lock.
        /// </summary>
        [Test]
        public async Task ACheckThatThrowsKeepsNoTickFromComing()
        {

            NewWarden("throwing", () => throw new InvalidOperationException("this check always fails"), out var started);

            Assert.That(await Within(Patience, () => started() > 2), Is.True,
                        "the ticks after a check that threw did not run it again");

        }

        #endregion

        #region DisposingWaitsForTheChecksAtWork()

        /// <summary>
        /// A Warden disposed of while its checks are at work is done once they
        /// are, and not before: the tick that waited for them used to make it
        /// so, and what it disposes of - its lock, its DNS client - is what
        /// they are still using.
        /// </summary>
        [Test]
        public async Task DisposingWaitsForTheChecksAtWork()
        {

            var warden    = NewWarden("disposed", () => gate.Task, out var started);

            Assert.That(await Within(Patience, () => started() == 1), Is.True, "the first tick started the check");

            var disposing = warden.DisposeAsync().AsTask();

            await Task.Delay(Often * 5);

            Assert.That(disposing.IsCompleted, Is.False, "disposed of while its check was still at work");

            gate.TrySetResult(new Object());

            Assert.That(await Task.WhenAny(disposing, Task.Delay(Patience)), Is.SameAs(disposing),
                        "not disposed of after its check was done");

            await disposing;

        }

        #endregion

        #region ManyWardensWaitingInTheirChecksHoldNoThread()

        /// <summary>
        /// Many Wardens whose checks are all waiting leave the thread pool
        /// free: what is given to it meanwhile is done at once, and it needs
        /// hardly a thread more than before.
        /// </summary>
        [Test]
        public async Task ManyWardensWaitingInTheirChecksHoldNoThread()
        {

            const Int32 count = 200;

            var threadsBefore = ThreadPool.ThreadCount;
            var startedOf     = new List<Func<Int32>>();

            for (var number = 0; number < count; number++)
            {
                NewWarden($"waiting{number}", () => gate.Task, out var started);
                startedOf.Add(started);
            }

            Assert.That(await Within(Patience, () => startedOf.All(started => started() == 1)), Is.True,
                        "every Warden started its check");

            // Some ticks of each Warden come and go meanwhile.
            await Task.Delay(Often * 10);

            var watch  = Stopwatch.StartNew();
            var done   = await Task.Run(() => true).WaitAsync(Patience);

            // Half of them, not a handful: the pool is the process's, and what
            // the tests before this one left at work grows it by a few dozen
            // threads now and then. A tick that held a thread grew it by one
            // for nearly every Warden - 198 to 434 of these 200.

            Assert.Multiple(() => {
                Assert.That(done,                                          Is.True);
                Assert.That(watch.Elapsed,                                 Is.LessThan(TimeSpan.FromSeconds(1)),  "something given to the thread pool waited for a thread");
                Assert.That(ThreadPool.ThreadCount - threadsBefore,        Is.LessThan(count / 2),                "the thread pool grew by a thread for many of the Wardens");
                Assert.That(startedOf.All(started => started() == 1),      Is.True,                               "a tick was not left out");
            });

        }

        #endregion

    }

}
