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
    /// The entity a Warden check is registered with, and what its checker is
    /// handed when the check runs.
    ///
    /// The Warden keeps entities as <c>Object</c> and casts them back per
    /// check, so the typed entity a caller passes and the one its checker
    /// receives are only the same thing as long as nothing between them lets
    /// go of it. These tests say that they are, and that an entity which is
    /// not there is refused where it is given rather than minutes later: a
    /// check runs on a timer, and what is left of a failure inside the Warden
    /// loop is a DebugX line and a check that quietly does nothing.
    /// </summary>
    [TestFixture]
    public class WardenEntityTests
    {

        #region (private) NewWarden()

        /// <summary>
        /// A Warden whose own timer will not fire during a test: what runs
        /// here is driven by hand.
        /// </summary>
        private static Warden NewWarden()

            => new (
                   "entity-tests",
                   InitialDelay:  TimeSpan.FromHours(24),
                   CheckEvery:    TimeSpan.FromHours(24)
               );

        #endregion


        #region ACheckWithoutItsEntityIsRefusedWhereItIsRegistered()

        /// <summary>
        /// All four overloads that take one. A null used to be accepted here
        /// and passed on as null to a checker whose parameter says it never
        /// is - so the mistake surfaced in somebody else's code, on a timer,
        /// as a logged line.
        /// </summary>
        [Test]
        public void ACheckWithoutItsEntityIsRefusedWhereItIsRegistered()
        {

            using var warden = NewWarden();

            // The parameter name as well as the type: an ArgumentNullException
            // from somewhere else in registration would satisfy the type alone
            // and prove nothing about the entity.
            void Refuses(Action Register, String What)
            {

                var thrown = Assert.Throws<ArgumentNullException>(() => Register(), $"{What} was registered.");

                Assert.That(thrown!.ParamName, Is.EqualTo("Entity"),
                            $"{What} was refused, but for something other than its entity.");

            }

            Assert.Multiple(() => {

                Refuses(() => warden.Check<String>(
                                  (timestamp, properties) => true,
                                  TimeSpan.Zero,
                                  null!,
                                  (timestamp, entity, ct) => Task.CompletedTask
                              ),
                        "An entity-less check");

                Refuses(() => warden.Check<String, Boolean>(
                                  (timestamp, properties) => true,
                                  TimeSpan.Zero,
                                  null!,
                                  (timestamp, entity, ct) => Task.FromResult(true)
                              ),
                        "An entity-less check with a result");

                Refuses(() => warden.Check<String>(
                                  (timestamp, properties) => true,
                                  TimeSpan.Zero,
                                  null!,
                                  (timestamp, dns, entity, ct) => Task.CompletedTask
                              ),
                        "An entity-less check with a DNS client");

                Refuses(() => warden.Check<String, Boolean>(
                                  (timestamp, properties) => true,
                                  TimeSpan.Zero,
                                  null!,
                                  (timestamp, dns, entity, ct) => Task.FromResult(true)
                              ),
                        "An entity-less check with a DNS client and a result");

            });

            Assert.That(warden.AllWardenChecks, Is.Empty,
                        "A refused check was added to the Warden all the same.");

        }

        #endregion

        #region TheCheckerIsHandedTheEntityItWasRegisteredWith()

        /// <summary>
        /// The invariant the cast inside Check rests on: what comes back is
        /// that very object, not a copy and not something else of its type.
        /// </summary>
        [Test]
        public async Task TheCheckerIsHandedTheEntityItWasRegisteredWith()
        {

            using var warden = NewWarden();

            var           entity  = new List<Int32> { 1, 2, 3 };
            List<Int32>?  given   = null;

            warden.Check<List<Int32>>(
                (timestamp, properties) => true,
                TimeSpan.Zero,
                entity,
                (timestamp, got, ct) => {
                    given = got;
                    return Task.CompletedTask;
                }
            );

            // No DNS client: this check does not ask for one, and a test
            // about the entity should not need a name server to say so.
            await warden.AllWardenChecks.Single().Run(
                      DateTimeOffset.UtcNow,
                      null!,
                      CancellationToken.None
                  );

            Assert.That(given, Is.SameAs(entity),
                        "The checker was handed something other than the entity it was registered with.");

        }

        #endregion

    }

}
