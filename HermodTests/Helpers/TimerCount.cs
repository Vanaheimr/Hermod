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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests
{

    /// <summary>
    /// The timers that instances of a class leave running once they were
    /// disposed of.
    /// </summary>
    /// <remarks>
    /// A running System.Threading.Timer keeps what it calls alive, and queues
    /// that call on the thread pool every period, for as long as the process
    /// lives. What forgets one of its timers on the way out therefore never
    /// goes away.
    ///
    /// Timer.ActiveCount counts the timers of the whole process, and the rest
    /// of a test run goes on ticking beside the instances being counted. So
    /// many instances are made at once: what each of them leaves behind then
    /// comes to a multiple of <see cref="Instances"/>, and stands out from the
    /// few that anything else starts or stops meanwhile.
    /// </remarks>
    public static class TimerCount
    {

        /// <summary>
        /// How many instances are made at once.
        /// </summary>
        public const Int32 Instances = 20;


        #region Of(Make, DisposeOf)

        /// <summary>
        /// Make <see cref="Instances"/> instances, and count the timers they
        /// add while they are alive, and those still running once all of them
        /// were disposed of.
        /// </summary>
        /// <param name="Make">Make one instance.</param>
        /// <param name="DisposeOf">Dispose of one instance.</param>
        public static Task<(Int64 Alive, Int64 Left)> Of<T>(Func<T>             Make,
                                                            Func<T, ValueTask>  DisposeOf)

            => Of(() => Task.FromResult(Make()), DisposeOf);


        /// <summary>
        /// Make <see cref="Instances"/> instances, one after another, and count
        /// the timers they add while they are alive, and those still running
        /// once all of them were disposed of.
        /// </summary>
        /// <param name="Make">Make one instance, a started one for example.</param>
        /// <param name="DisposeOf">Dispose of one instance, or stop it.</param>
        public static async Task<(Int64 Alive, Int64 Left)> Of<T>(Func<Task<T>>       Make,
                                                                  Func<T, ValueTask>  DisposeOf)
        {

            // One made and disposed of beforehand: what the first instance of a
            // kind starts once for the whole process is not what this counts.
            await DisposeOf(await Make());

            var before     = Timer.ActiveCount;
            var instances  = new List<T>(Instances);

            for (var i = 0; i < Instances; i++)
                instances.Add(await Make());

            var alive      = Timer.ActiveCount - before;

            foreach (var instance in instances)
                await DisposeOf(instance);

            return (alive, Timer.ActiveCount - before);

        }

        #endregion

        #region AssertNoneLeft(Timers, What)

        /// <summary>
        /// Assert that the instances counted ran timers while they were alive,
        /// and left none of them running.
        /// </summary>
        /// <param name="Timers">What <see cref="Of"/> counted.</param>
        /// <param name="What">What the instances are, for the messages.</param>
        public static void AssertNoneLeft((Int64 Alive, Int64 Left)  Timers,
                                          String                     What)
        {
            Assert.Multiple(() => {

                // Without a timer of their own while alive, there would be
                // nothing to leave behind, and the second check would pass
                // without having looked at anything. Both checks part at half
                // the instances: a timer of something else that fires while
                // they are made, or one that is started, moves either count
                // by one, and a class with one timer each still stands out.
                Assert.That(
                    Timers.Alive,
                    Is.GreaterThanOrEqualTo(Instances / 2),
                    $"timers running while {Instances} {What} were alive"
                );

                Assert.That(
                    Timers.Left,
                    Is.LessThan(Instances / 2),
                    $"timers still running after {Instances} {What} were disposed of"
                );

            });
        }

        #endregion

    }

}
