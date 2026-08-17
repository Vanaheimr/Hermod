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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M9: keystroke-timing obfuscation — a constant cadence of real+chaff packets while typing, stopping after the idle window.
    /// </summary>
    [TestFixture]
    public class KeystrokeTimingObfuscatorTests
    {

        private sealed class ManualClock : TimeProvider
        {
            private DateTimeOffset now;
            public ManualClock(DateTimeOffset Start) { now = Start; }
            public override DateTimeOffset GetUtcNow() => now;
            public void Advance(TimeSpan By) => now += By;
        }

        private static Byte[] K(String s) => Encoding.UTF8.GetBytes(s);


        #region Cadence_IsConstant_RealThenChaff_ThenIdle

        [Test]
        public void Cadence_IsConstant_RealThenChaff_ThenIdle()
        {

            var clock = new ManualClock(new DateTimeOffset(2026, 07, 24, 12, 00, 00, TimeSpan.Zero));
            var obf   = new KeystrokeTimingObfuscator(
                            new KeystrokeTimingObfuscation { Interval = TimeSpan.FromMilliseconds(20), IdleStop = TimeSpan.FromSeconds(1) },
                            clock);

            // Keystrokes arrive at irregular times, but leave on the fixed 20 ms grid.
            obf.Enqueue(K("h"));
            clock.Advance(TimeSpan.FromMilliseconds(20));
            var t1 = obf.Poll();

            obf.Enqueue(K("i"));   // burst of two while the previous tick just fired
            obf.Enqueue(K("!"));
            clock.Advance(TimeSpan.FromMilliseconds(20));
            var t2 = obf.Poll();
            clock.Advance(TimeSpan.FromMilliseconds(20));
            var t3 = obf.Poll();

            // Queue now empty but still within the idle window ⇒ chaff keeps the cadence constant.
            clock.Advance(TimeSpan.FromMilliseconds(20));
            var t4 = obf.Poll();
            clock.Advance(TimeSpan.FromMilliseconds(20));
            var t5 = obf.Poll();

            // Long pause past the idle window ⇒ the cadence goes idle (chaff stops).
            clock.Advance(TimeSpan.FromSeconds(2));
            var t6 = obf.Poll();

            Assert.Multiple(() => {
                Assert.That(t1.Kind, Is.EqualTo(KeystrokeEmit.Real));
                Assert.That(t1.Payload, Is.EqualTo(K("h")));
                Assert.That(t2.Kind, Is.EqualTo(KeystrokeEmit.Real));
                Assert.That(t2.Payload, Is.EqualTo(K("i")));
                Assert.That(t3.Kind, Is.EqualTo(KeystrokeEmit.Real));
                Assert.That(t3.Payload, Is.EqualTo(K("!")));
                Assert.That(t4.Kind, Is.EqualTo(KeystrokeEmit.Chaff), "empty queue but active ⇒ chaff, not a gap");
                Assert.That(t5.Kind, Is.EqualTo(KeystrokeEmit.Chaff));
                Assert.That(t6.Kind, Is.EqualTo(KeystrokeEmit.Idle),  "cadence stops after the idle window");
            });

        }

        #endregion

        #region NoKeystrokes_StaysIdle

        [Test]
        public void NoKeystrokes_StaysIdle()
        {
            var clock = new ManualClock(new DateTimeOffset(2026, 07, 24, 12, 00, 00, TimeSpan.Zero));
            var obf   = new KeystrokeTimingObfuscator(TimeProvider: clock);

            clock.Advance(TimeSpan.FromMilliseconds(20));
            Assert.That(obf.Poll().Kind, Is.EqualTo(KeystrokeEmit.Idle), "no typing ⇒ no chaff at all");
        }

        #endregion

        #region NewKeystroke_ReactivatesCadence

        [Test]
        public void NewKeystroke_ReactivatesCadence()
        {

            var clock = new ManualClock(new DateTimeOffset(2026, 07, 24, 12, 00, 00, TimeSpan.Zero));
            var obf   = new KeystrokeTimingObfuscator(
                            new KeystrokeTimingObfuscation { Interval = TimeSpan.FromMilliseconds(20), IdleStop = TimeSpan.FromSeconds(1) },
                            clock);

            obf.Enqueue(K("a"));
            clock.Advance(TimeSpan.FromMilliseconds(20));
            Assert.That(obf.Poll().Kind, Is.EqualTo(KeystrokeEmit.Real));

            // Go idle …
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.That(obf.Poll().Kind, Is.EqualTo(KeystrokeEmit.Idle));

            // … a fresh keystroke reactivates the cadence.
            obf.Enqueue(K("b"));
            Assert.That(obf.Poll().Kind, Is.EqualTo(KeystrokeEmit.Real));
            clock.Advance(TimeSpan.FromMilliseconds(20));
            Assert.That(obf.Poll().Kind, Is.EqualTo(KeystrokeEmit.Chaff), "still active right after typing resumed");

        }

        #endregion

    }

}
