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
