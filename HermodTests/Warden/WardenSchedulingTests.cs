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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Warden;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Warden
{

    using Warden = org.GraphDefined.Vanaheimr.Warden.Warden;

    /// <summary>
    /// When a Warden check actually runs.
    ///
    /// Two things decide that, and neither is enough on its own: the check's
    /// <c>RunCheck</c> predicate, which the Warden evaluates on every tick of its
    /// own timer, and the check's <c>SleepTime</c>, which is a debounce on top.
    /// The tests below take them apart, because the interesting failures live in
    /// the gap — a predicate that is true for a whole minute is not "once a
    /// minute", and a slot narrower than the tick is a slot that can be missed
    /// entirely.
    /// </summary>
    [TestFixture]
    public class WardenSchedulingTests
    {

        #region (private) NewWarden()

        /// <summary>
        /// A Warden whose own timer will not fire during a test: everything here
        /// drives the check by hand, so that "when would this run" is a question
        /// about the schedule and not about how long the test waited.
        /// </summary>
        private static Warden NewWarden()

            => new (
                   "scheduling-tests",
                   InitialDelay:  TimeSpan.FromHours(24),
                   CheckEvery:    TimeSpan.FromHours(24)
               );

        #endregion


        #region (private) SlotsIn(Check, Properties, Start, Step, Count)

        /// <summary>
        /// The offsets, in units of <paramref name="Step"/>, at which the check's
        /// predicate says yes — i.e. exactly what the Warden would see if it ticked
        /// at that rate.
        /// </summary>
        private static Int32[] SlotsIn(IWardenCheck      Check,
                                       WardenProperties  Properties,
                                       DateTimeOffset    Start,
                                       TimeSpan          Step,
                                       Int32             Count)

            => [.. Enumerable.Range(0, Count).
                              Where(i => Check.RunCheck(Start + Step * i, Properties))];

        private static readonly DateTimeOffset midnight = new (2026, 9, 25, 0, 0, 0, TimeSpan.Zero);

        #endregion


        #region EverySecondsCountsSeconds()

        /// <summary>
        /// Six of the eight EverySeconds overloads tested <c>timestamp.Minute %
        /// Seconds</c>. The two that did not are in the last region of the file and
        /// show what was meant, which is what makes this a copy-and-paste slip
        /// rather than a design.
        /// </summary>
        [Test]
        public void EverySecondsCountsSeconds()
        {

            using var warden = NewWarden();

            warden.EverySeconds(5, (timestamp, ct) => Task.CompletedTask);

            Assert.That(
                SlotsIn(warden.AllWardenChecks.Single(), warden.WardenCheckProperties, midnight, TimeSpan.FromSeconds(1), 60),
                Is.EqualTo(new[] { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55 })
            );

        }

        #endregion

        #region EverySecondsDoesNotDependOnWhichMinuteItIs()

        /// <summary>
        /// The sharper statement of the same bug, and the one that cannot be read
        /// two ways: whether second 7 is a slot must be the same answer in every
        /// minute of the hour. Under <c>Minute % Seconds</c> it was "yes" for twelve
        /// minutes out of sixty and "no" for the rest — a schedule that changed with
        /// the wall clock.
        /// </summary>
        [Test]
        public void EverySecondsDoesNotDependOnWhichMinuteItIs()
        {

            using var warden = NewWarden();

            warden.EverySeconds(5, (timestamp, ct) => Task.CompletedTask);

            var check       = warden.AllWardenChecks.Single();
            var properties  = warden.WardenCheckProperties;

            var answers     = Enumerable.Range(0, 60).
                                         Select(minute => check.RunCheck(midnight.AddMinutes(minute).AddSeconds(7), properties)).
                                         Distinct().
                                         ToArray();

            Assert.That(answers, Is.EqualTo(new[] { false }));

        }

        #endregion

        #region EveryMinutesCountsMinutes()

        [Test]
        public void EveryMinutesCountsMinutes()
        {

            using var warden = NewWarden();

            warden.EveryMinutes(15, (timestamp, ct) => Task.CompletedTask);

            Assert.That(
                SlotsIn(warden.AllWardenChecks.Single(), warden.WardenCheckProperties, midnight, TimeSpan.FromMinutes(1), 60),
                Is.EqualTo(new[] { 0, 15, 30, 45 })
            );

        }

        #endregion

        #region EveryHoursCountsHours()

        [Test]
        public void EveryHoursCountsHours()
        {

            using var warden = NewWarden();

            warden.EveryHours(6, (timestamp, ct) => Task.CompletedTask);

            Assert.That(
                SlotsIn(warden.AllWardenChecks.Single(), warden.WardenCheckProperties, midnight, TimeSpan.FromHours(1), 24),
                Is.EqualTo(new[] { 0, 6, 12, 18 })
            );

        }

        #endregion

        #region TheOffsetShiftsTheSlots()

        /// <summary>
        /// The schedule is an alignment rather than an interval — "at seconds
        /// divisible by five", not "five seconds after the last run" — and the
        /// offset is what moves the alignment.
        /// </summary>
        [Test]
        public void TheOffsetShiftsTheSlots()
        {

            using var warden = NewWarden();

            warden.EverySeconds(5, 2, (timestamp, ct) => Task.CompletedTask);

            Assert.That(
                SlotsIn(warden.AllWardenChecks.Single(), warden.WardenCheckProperties, midnight, TimeSpan.FromSeconds(1), 60),
                Is.EqualTo(new[] { 2, 7, 12, 17, 22, 27, 32, 37, 42, 47, 52, 57 })
            );

        }

        #endregion

        #region TheSleepTimeIsWhatTurnsASlotIntoOneRun()

        /// <summary>
        /// The predicate alone would run the check on *every* tick inside a
        /// matching slot: "minute 0" is sixty seconds wide, and a Warden ticking
        /// every ten seconds is in it six times. SleepTime is the debounce that
        /// makes "every fifteen minutes" mean four runs an hour and not
        /// twenty-four.
        ///
        /// Worth a test of its own because the two halves live in different files
        /// and neither is correct without the other.
        /// </summary>
        [Test]
        public async Task TheSleepTimeIsWhatTurnsASlotIntoOneRun()
        {

            using var warden = NewWarden();

            var runs = 0;

            warden.EveryMinutes(15, (timestamp, ct) => { runs++; return Task.CompletedTask; });

            var check        = warden.AllWardenChecks.Single();
            var properties   = warden.WardenCheckProperties;
            var opportunities = 0;

            // One hour of ten-second ticks.
            for (var tick = 0; tick < 360; tick++)
            {

                var now = midnight.AddSeconds(tick * 10);

                if (check.RunCheck(now, properties))
                {
                    opportunities++;
                    await check.Run(now, warden.DNSClient, default);
                }

            }

            Assert.Multiple(() => {
                Assert.That(opportunities,  Is.EqualTo(24), "four matching minutes, six ticks in each");
                Assert.That(runs,           Is.EqualTo(4),  "and SleepTime turns each of those six into one");
            });

        }

        #endregion

        #region ASlotNarrowerThanTheTickIsASlotThatCanBeMissed()

        /// <summary>
        /// The precondition nothing states anywhere: the predicate is *sampled*, so
        /// a schedule finer than the Warden's own CheckEvery is not a schedule. At
        /// seconds divisible by three there are twenty slots in a minute; a Warden
        /// ticking every ten seconds visits two of them.
        ///
        /// This is not a defect to fix — it is what "run the checks every
        /// CheckEvery" means — but it is the reason EverySeconds is only usable on a
        /// Warden configured to tick at least that often, and the reason the
        /// minute- and hour-aligned schedules are safe: their slots are wider than
        /// any sane tick.
        /// </summary>
        [Test]
        public void ASlotNarrowerThanTheTickIsASlotThatCanBeMissed()
        {

            using var warden = NewWarden();

            warden.EverySeconds(3, (timestamp, ct) => Task.CompletedTask);

            var check       = warden.AllWardenChecks.Single();
            var properties  = warden.WardenCheckProperties;

            Assert.Multiple(() => {

                Assert.That(SlotsIn(check, properties, midnight, TimeSpan.FromSeconds(1),  60).Length,
                            Is.EqualTo(20), "the schedule itself");

                Assert.That(SlotsIn(check, properties, midnight, TimeSpan.FromSeconds(10),  6).Length,
                            Is.EqualTo(2),  "what a Warden ticking every ten seconds sees of it");

            });

        }

        #endregion

        #region TheRegisteredChecksCanBeEnumerated()

        /// <summary>
        /// <c>AllWardenChecks</c> returned <c>AllWardenChecks</c> — itself — so
        /// reading it recursed until the stack ran out. Nothing called it, which is
        /// the only reason it never took a process down.
        ///
        /// It is first here because everything else in this file reads the
        /// registered checks back in order to ask when they would run.
        /// </summary>
        [Test]
        public void TheRegisteredChecksCanBeEnumerated()
        {

            using var warden = NewWarden();

            warden.EveryMinutes(1, (timestamp, ct) => Task.CompletedTask);
            warden.EveryHours  (1, (timestamp, ct) => Task.CompletedTask);

            Assert.That(warden.AllWardenChecks.Count(), Is.EqualTo(2));

        }

        #endregion

    }

}
