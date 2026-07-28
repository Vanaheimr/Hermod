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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Buffers;

/// <summary>
/// The zero-alloc/UDP-batching building blocks of phase 9: <see cref="ByteQueue"/> (append/consume
/// without O(n) shifting) and <see cref="GsoBatcher"/> (datagram grouping for UDP_SEGMENT).
/// </summary>
[TestFixture]
public class ZeroAllocBufferTests
{
    // ---- ByteQueue -------------------------------------------------------------------------

    [Test]
    public void ByteQueue_AppendConsume_PreservesFifoOrder()
    {
        var q = new ByteQueue();
        Assert.That(q.Count, Is.EqualTo(0));

        q.Append([1, 2, 3]);
        q.Append([4, 5]);
        Assert.That(q.Count, Is.EqualTo(5));
        Assert.That(q.Span.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));

        q.Consume(2); // 1,2 out
        Assert.That(q.Span.ToArray(), Is.EqualTo(new byte[] { 3, 4, 5 }));

        q.Append([6, 7]); // keep appending after a partial consume (internal head > 0)
        Assert.That(q.Span.ToArray(), Is.EqualTo(new byte[] { 3, 4, 5, 6, 7 }));

        q.Consume(5);
        Assert.That(q.Count, Is.EqualTo(0));
        Assert.That(q.Span.IsEmpty, Is.True);
    }

    [Test]
    public void ByteQueue_ManyAppendConsumeCycles_MatchReferenceQueue()
    {
        // Fuzz against a trivial reference (System queue) — deterministic seed.
        var q = new ByteQueue();
        var reference = new Queue<byte>();
        var rng = new Random(4242);

        for (int step = 0; step < 5000; step++)
        {
            if (rng.Next(2) == 0)
            {
                byte[] chunk = new byte[rng.Next(0, 40)];
                rng.NextBytes(chunk);
                q.Append(chunk);
                foreach (byte b in chunk)
                    reference.Enqueue(b);
            }
            else
            {
                int take = rng.Next(0, Math.Min(reference.Count, 30) + 1);
                Assert.That(q.Span[..take].ToArray(), Is.EqualTo(Peek(reference, take)));
                q.Consume(take);
                for (int k = 0; k < take; k++)
                    reference.Dequeue();
            }
            Assert.That(q.Count, Is.EqualTo(reference.Count));
        }
    }

    [Test]
    public void ByteQueue_Consume_OutOfRange_Throws()
    {
        var q = new ByteQueue();
        q.Append([1, 2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => q.Consume(3));
    }

    // ---- GsoBatcher ------------------------------------------------------------------------

    [Test]
    public void GsoBatcher_GroupsEqualSizedRuns_WithSmallerTrailingSegment()
    {
        // Three equally sized (1200) + one smaller (500) ⇒ ONE batch, segment size 1200, 4 segments.
        var datagrams = new[] { Bytes(1200, 1), Bytes(1200, 2), Bytes(1200, 3), Bytes(500, 4) };
        List<GsoBatch> batches = Collect(datagrams, out List<byte[]> reconstructed);

        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(batches[0].SegmentSize, Is.EqualTo(1200));
        Assert.That(batches[0].SegmentCount, Is.EqualTo(4));
        Assert.That(reconstructed, Is.EqualTo(datagrams)); // reconstructible byte for byte
    }

    [Test]
    public void GsoBatcher_StartsNewBatch_OnLargerDatagram()
    {
        // A smaller one MUST end a segment run; a LARGER one starts a new batch.
        var datagrams = new[] { Bytes(800, 1), Bytes(1200, 2), Bytes(1200, 3) };
        List<GsoBatch> batches = Collect(datagrams, out List<byte[]> reconstructed);

        // 800 is smaller ⇒ its own 1-segment batch; then 1200,1200 as a batch of two.
        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.That(batches[0].SegmentCount, Is.EqualTo(1));
        Assert.That(batches[1].SegmentCount, Is.EqualTo(2));
        Assert.That(batches[1].SegmentSize, Is.EqualTo(1200));
        Assert.That(reconstructed, Is.EqualTo(datagrams));
    }

    [Test]
    public void GsoBatcher_RespectsSegmentAndByteCaps()
    {
        // 200 equally sized 20-byte datagrams ⇒ batches of at most MaxSegments (64) segments each.
        var datagrams = Enumerable.Range(0, 200).Select(k => Bytes(20, (byte)k)).ToArray();
        List<GsoBatch> batches = Collect(datagrams, out List<byte[]> reconstructed);

        Assert.That(batches, Has.All.Matches<GsoBatch>(b => b.SegmentCount <= GsoBatcher.MaxSegments));
        Assert.That(batches.Sum(b => b.SegmentCount), Is.EqualTo(200));
        Assert.That(reconstructed, Is.EqualTo(datagrams));
    }

    [Test]
    public void GsoBatcher_SingleDatagram_IsOneSegmentBatch()
    {
        List<GsoBatch> batches = Collect([Bytes(1200, 9)], out List<byte[]> reconstructed);
        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(batches[0].SegmentCount, Is.EqualTo(1));
        Assert.That(reconstructed, Is.EqualTo(new[] { Bytes(1200, 9) }));
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static byte[] Bytes(int length, byte fill)
    {
        byte[] b = new byte[length];
        Array.Fill(b, fill);
        return b;
    }

    /// <summary>
    /// Collects the batches AND reconstructs the original datagram sequence from them — each batch
    /// is cut up again according to the segment size (exactly what the kernel does for UDP_SEGMENT).
    /// </summary>
    private static List<GsoBatch> Collect(IReadOnlyList<byte[]> datagrams, out List<byte[]> reconstructed)
    {
        var batches = new List<GsoBatch>();
        var recon = new List<byte[]>();
        new GsoBatcher().Batch(datagrams, batch =>
        {
            batches.Add(batch);
            for (int offset = 0; offset < batch.Length; offset += batch.SegmentSize)
            {
                int len = Math.Min(batch.SegmentSize, batch.Length - offset);
                recon.Add(batch.Buffer.AsSpan(offset, len).ToArray());
            }
        });
        reconstructed = recon;
        return batches;
    }

    private static byte[] Peek(Queue<byte> queue, int count) => queue.Take(count).ToArray();
}
