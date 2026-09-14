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

using NUnit.Framework;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TOTP
{

    /// <summary>
    /// When a time-based one-time password stops being valid, and how much of
    /// it is left.
    /// </summary>
    /// <remarks>
    /// The end of a window is a fixed instant, the same for everybody who asks
    /// during it. That is not decoration: it is what lets two parties compare
    /// notes, what lets an answer be cached or held against a previous one, and
    /// what makes "how long is left" mean anything.
    ///
    /// It used to be worked out as "now, plus what is left", with what is left
    /// counted from whole seconds and now carrying its fraction - so two
    /// callers a few hundred milliseconds apart were told two different ends of
    /// one window, and the time left was over-stated by up to a second. The
    /// over-statement is the half that bites: whoever asks "is this still worth
    /// handing out" is told it has longer than it has.
    /// </remarks>
    [TestFixture]
    public class TOTPWindowBoundaryTests
    {

        private const  String    SharedSecret  = "a-shared-secret-for-tests";

        private static readonly TimeSpan  ValidityTime = TimeSpan.FromSeconds(30);


        #region EndTime_IsTheSameForEverybodyInTheWindow()

        [Test]
        public void EndTime_IsTheSameForEverybodyInTheWindow()
        {

            // Deliberately not on a whole second, and deliberately not on a
            // window boundary: the fractions are what used to leak out.
            var windowStart = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);   // a multiple of 30

            var asked       = new[] { 0.0, 0.113, 1.5, 7.777, 17.25, 29.001, 29.999 }.
                                  Select(offset => TOTPGenerator.GenerateTOTP(
                                                       SharedSecret,
                                                       ValidityTime,
                                                       TOTPTimestamp: windowStart + TimeSpan.FromSeconds(offset)
                                                   )).
                                  ToArray();

            Assert.Multiple(() => {

                // One window, one password, one end.
                Assert.That(asked.Select(answer => answer.Current).Distinct().Count(),  Is.EqualTo(1));
                Assert.That(asked.Select(answer => answer.EndTime).Distinct().Count(),  Is.EqualTo(1));

                // And that end is the start of the next window, to the tick.
                Assert.That(asked[0].EndTime,  Is.EqualTo(windowStart + ValidityTime));

            });

        }

        #endregion

        #region RemainingTime_IsNeverMoreThanIsLeft()

        [Test]
        public void RemainingTime_IsNeverMoreThanIsLeft()
        {

            var windowStart = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

            for (var milliseconds = 0; milliseconds < 30_000; milliseconds += 137)
            {

                var asked     = windowStart + TimeSpan.FromMilliseconds(milliseconds);
                var answer    = TOTPGenerator.GenerateTOTP(SharedSecret, ValidityTime, TOTPTimestamp: asked);

                var actually  = windowStart + ValidityTime - asked;

                Assert.That(
                    answer.RemainingTime,
                    Is.EqualTo(actually),
                    $"asked {milliseconds} ms into the window"
                );

                // The two have to agree with each other as well as with the clock.
                Assert.That(asked + answer.RemainingTime, Is.EqualTo(answer.EndTime));

            }

        }

        #endregion

        #region TheNextWindow_StartsWhereThisOneEnds()

        [Test]
        public void TheNextWindow_StartsWhereThisOneEnds()
        {

            var windowStart  = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

            var thisOne      = TOTPGenerator.GenerateTOTP(SharedSecret, ValidityTime, TOTPTimestamp: windowStart + TimeSpan.FromSeconds(29.9));
            var nextOne      = TOTPGenerator.GenerateTOTP(SharedSecret, ValidityTime, TOTPTimestamp: thisOne.EndTime);

            Assert.Multiple(() => {

                // A different window is a different password, and the moment
                // one ends is the moment the next is already the answer - not a
                // tick of nobody's land in between.
                Assert.That(nextOne.Current,  Is.Not.EqualTo(thisOne.Current));
                Assert.That(nextOne.EndTime,  Is.EqualTo(thisOne.EndTime + ValidityTime));
                Assert.That(nextOne.RemainingTime, Is.EqualTo(ValidityTime));

            });

        }

        #endregion

        #region EndTime_KeepsTheOffsetItWasAskedWith()

        [Test]
        public void EndTime_KeepsTheOffsetItWasAskedWith()
        {

            var asked   = new DateTimeOffset(2026, 9, 14, 22, 11, 36, 488, TimeSpan.FromHours(2));

            var answer  = TOTPGenerator.GenerateTOTP(SharedSecret, ValidityTime, TOTPTimestamp: asked);

            // The instant is what matters, but handing back a timestamp in some
            // other zone than the one it was asked in makes every log and every
            // screen that shows it read oddly for no reason.
            Assert.That(answer.EndTime.Offset, Is.EqualTo(asked.Offset));

        }

        #endregion

    }

}
