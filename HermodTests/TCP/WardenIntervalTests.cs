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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// What a TCP server's Warden settings mean.
    ///
    /// <c>WardenCheckEvery</c> is public, documented as "the warden check
    /// interval", and was not the interval at which anything checked. These tests
    /// hold the three places that have an opinion about it — the property, the
    /// Warden, and the registered check's own debounce — against each other,
    /// because each of them was individually plausible and no two of them agreed.
    ///
    /// Nothing is started and nothing is waited for: the question is what the
    /// schedule *is*, not how long it takes to observe it.
    /// </summary>
    [TestFixture]
    public class WardenIntervalTests
    {

        #region TheWardenTicksAtTheIntervalTheServerAdvertises()

        /// <summary>
        /// The defaults resolved twice, against different numbers: the properties
        /// took <c>DefaultWardenCheckEvery</c> (30 s) and the Warden took the
        /// literals three minutes and one minute that sat in the constructor call.
        /// </summary>
        [Test]
        public async Task TheWardenTicksAtTheIntervalTheServerAdvertises()
        {

            await using var server = new TCPEchoTestServer();

            Assert.Multiple(() => {

                Assert.That(server.Warden.CheckEvery,    Is.EqualTo(server.WardenCheckEvery));
                Assert.That(server.Warden.InitialDelay,  Is.EqualTo(server.WardenInitialDelay));

                // And the documented default is the one that wins, rather than a
                // number visible only in a constructor call.
                Assert.That(server.WardenCheckEvery,     Is.EqualTo(TimeSpan.FromSeconds(30)));

            });

        }

        #endregion

        #region TheConnectionCheckHasNoIntervalOfItsOwn()

        /// <summary>
        /// The reaper was registered as <c>EveryMinutes(1, …)</c>, which reads like
        /// "once a minute" and is two separate things: a predicate that is true on
        /// every tick, and a one-minute SleepTime. The SleepTime was the schedule,
        /// and no constructor argument could reach it — so asking for a faster
        /// Warden sped up the timer and not the check. Reproducing H-25 needed a
        /// source edit for exactly this reason.
        ///
        /// A debounce longer than the tick is the shape of that bug, whatever the
        /// numbers are, so that is what is asserted.
        /// </summary>
        [Test]
        public async Task TheConnectionCheckHasNoIntervalOfItsOwn()
        {

            await using var server = new TCPEchoTestServer();

            var checks = server.Warden.AllWardenChecks.ToArray();

            Assert.That(checks, Has.Length.EqualTo(1), "the connection reaper, and nothing else");

            Assert.That(
                checks[0].SleepTime,
                Is.LessThanOrEqualTo(server.WardenCheckEvery),
                "a check that debounces for longer than the Warden ticks has an interval the Warden cannot set"
            );

        }

        #endregion

    }

}
