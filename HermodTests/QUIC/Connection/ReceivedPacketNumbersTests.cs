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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Received packet numbers held as ranges, so an ACK frame costs what it reports.
/// </summary>
[TestFixture]
public class ReceivedPacketNumbersTests
{
    [Test]
    public void AgreesWithASetOfSingleNumbers_UnderReorderingDuplicatesAndPruning(
        [Values(1, 2, 3, 4, 5, 6, 7, 8)] int seed)
    {
        // The set of single numbers is what this replaced; it is obviously right, so it is the
        // reference. Arrivals run mostly in order, with reordering, duplicates and gaps, and the
        // pruning bound moves up now and then, as the peer's acknowledgments of our ACKs would move it.
        var random    = new Random(seed);
        var ranges    = new ReceivedPacketNumbers();
        var reference = new SortedSet<ulong>();
        ulong next = 0, prunedBelow = 0;

        for (int step = 0; step < 3000; step++)
        {
            ulong pn = random.Next(10) switch
            {
                0     => next + (ulong) random.Next(1, 6),                                 // a gap
                1     => next > 20 ? next - (ulong) random.Next(1, 20) : next,             // late or duplicate
                _     => next,                                                             // in order
            };
            if (pn >= next)
                next = pn + 1;
            if (pn < prunedBelow)
                continue; // the space never hands pruned numbers on

            Assert.That(ranges.Add(pn), Is.EqualTo(reference.Add(pn)), $"step {step}: Add({pn})");

            if (random.Next(50) == 0 && next > 10)
            {
                ulong bound = prunedBelow + (ulong) random.Next(0, (int) (next - prunedBelow));
                ulong expected = (ulong) reference.Count(n => n <= bound);
                Assert.That(ranges.PruneUpTo(bound), Is.EqualTo(expected), $"step {step}: PruneUpTo({bound})");
                reference.RemoveWhere(n => n <= bound);
                prunedBelow = Math.Max(prunedBelow, bound + 1);
            }

            Assert.That(ranges.Count, Is.EqualTo((ulong) reference.Count), $"step {step}: Count");
            if (reference.Count > 0)
                Assert.That(ranges.NewestDescending(32),
                            Is.EqualTo(AckFrame.FromAscendingPacketNumbers(reference, maxRanges: 32).Ranges),
                            $"step {step}: the ACK ranges");
        }

        for (ulong pn = prunedBelow; pn <= next + 2; pn++)
        {
            Assert.That(ranges.Contains(pn), Is.EqualTo(reference.Contains(pn)), $"Contains({pn})");
            ulong missing = pn;
            while (reference.Contains(missing))
                missing++;
            Assert.That(ranges.SmallestMissingFrom(pn), Is.EqualTo(missing), $"SmallestMissingFrom({pn})");
        }
    }

    [Test]
    public void BuildingAnAck_CostsItsRanges_NotThePacketsHeld()
    {
        // A server streaming a response receives an ACK-only packet for about every other packet it
        // sends, and piggybacks an ACK on nearly every one of its own. Nothing is pruned for a round
        // trip, so the space holds the peer's whole flight. Building each ACK used to walk all of it:
        // 4000 packets in, an ACK after every second one, came to four million steps.
        var space = new PacketNumberSpace();
        int builds = 0;
        for (ulong pn = 0; pn < 4000; pn++)
        {
            space.RecordReceived(pn);
            if (pn % 2 == 1)
            {
                Assert.That(space.BuildAck(), Is.Not.Null);
                builds++;
            }
        }

        Assert.That(space.TrackedReceivedCount, Is.EqualTo(4000), "precondition: nothing was pruned");
        Assert.That(space.AckBuildWorkForTest, Is.EqualTo(builds), "one range per ACK, whatever the space holds");
    }
}
