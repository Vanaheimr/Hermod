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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Tunnels;

/// <summary>
/// The receive side of <see cref="Http3Tunnel"/> under concurrent access. The tunnel used to assume
/// its consumer never leaves the pump thread — true only as long as that consumer is the RFC 6455
/// framing, which never awaits real I/O. A consumer that awaits anything else resumes on a
/// thread-pool thread and reads while the pump delivers.
/// </summary>
[TestFixture]
public class Http3TunnelConcurrencyTests
{

    private static Http3Tunnel NewTunnel()
        => new(new QuicStream(new StreamId(0)));

    #region Deterministic behaviour

    [Test]
    public void DeliverBeforeRead_IsQueuedAndReturnedInOrder()
    {
        Http3Tunnel tunnel = NewTunnel();
        tunnel.Deliver([1]);
        tunnel.Deliver([2]);

        Assert.That(tunnel.ReadAsync(CancellationToken.None).Result, Is.EqualTo(new byte[] { 1 }));
        Assert.That(tunnel.ReadAsync(CancellationToken.None).Result, Is.EqualTo(new byte[] { 2 }));
    }

    [Test]
    public void DeliverAfterRead_CompletesTheWaitingRead()
    {
        Http3Tunnel tunnel = NewTunnel();
        Task<byte[]?> read = tunnel.ReadAsync(CancellationToken.None);
        Assert.That(read.IsCompleted, Is.False);

        tunnel.Deliver([7]);
        Assert.That(read.Result, Is.EqualTo(new byte[] { 7 }));
    }

    [Test]
    public void End_CompletesAWaitingReadWithNull_AndEveryReadAfterThat()
    {
        Http3Tunnel tunnel = NewTunnel();
        Task<byte[]?> read = tunnel.ReadAsync(CancellationToken.None);

        tunnel.End();

        Assert.That(read.Result, Is.Null);
        Assert.That(tunnel.ReadAsync(CancellationToken.None).Result, Is.Null);
    }

    [Test]
    public void QueuedDataSurvivesEnd_AndOnlyThenComesNull()
    {
        Http3Tunnel tunnel = NewTunnel();
        tunnel.Deliver([1]);
        tunnel.End();

        Assert.That(tunnel.ReadAsync(CancellationToken.None).Result, Is.EqualTo(new byte[] { 1 }));
        Assert.That(tunnel.ReadAsync(CancellationToken.None).Result, Is.Null);
    }

    [Test]
    public void TwoOverlappingReads_AreRejected()
    {
        Http3Tunnel tunnel = NewTunnel();
        _ = tunnel.ReadAsync(CancellationToken.None);

        // Previously the second reader silently got the SAME task as the first, so one delivered
        // chunk would have satisfied both — a lost chunk rather than a visible error.
        Assert.Throws<InvalidOperationException>(() => tunnel.ReadAsync(CancellationToken.None));
    }

    #endregion

    #region Send side (marshalled to the pump)

    [Test]
    public void WritesReachTheStreamOnlyOnceThePumpDrains()
    {
        var stream = new QuicStream(new StreamId(0));
        var tunnel = new Http3Tunnel(stream);

        tunnel.WriteAsync([1, 2, 3], CancellationToken.None);

        // The whole point of the marshalling: the consumer's thread does not touch the send buffer.
        Assert.That(stream.Send.PendingBytes, Is.Zero, "The write must not reach the stream directly.");

        tunnel.PumpOutbound();
        Assert.That(stream.Send.PendingBytes, Is.GreaterThan(0), "The pump must put the write on the stream.");
    }

    [Test]
    public void TheFinCannotOvertakeWritesQueuedBeforeIt()
    {
        var stream = new QuicStream(new StreamId(0));
        var tunnel = new Http3Tunnel(stream);

        tunnel.WriteAsync([1, 2, 3], CancellationToken.None);
        tunnel.Complete();
        tunnel.PumpOutbound();

        // Both happened, and the bytes are on the stream rather than lost behind the FIN.
        Assert.That(stream.Send.PendingBytes, Is.GreaterThan(0));
    }

    [Test]
    public void AbortDropsWhatWasQueuedButNotYetSent()
    {
        var stream = new QuicStream(new StreamId(0));
        var tunnel = new Http3Tunnel(stream);

        tunnel.WriteAsync([1, 2, 3], CancellationToken.None);
        tunnel.Abort();   // a reset, not a close: queued data is not delivered (RFC 9000 §2.4)
        tunnel.PumpOutbound();

        Assert.That(stream.Send.IsReset, Is.True);
        Assert.That(stream.Send.PendingBytes, Is.Zero, "A reset must not still emit the queued chunk.");
    }

    /// <summary>
    /// The case the marshalling exists for: a consumer resuming on a thread-pool thread writes while
    /// the pump drains. Nothing may be lost, torn or reordered.
    /// </summary>
    [Test]
    public void WritesFromAnotherThread_ArriveCompleteAndInOrder()
    {
        const int writes = 20_000;

        var stream = new QuicStream(new StreamId(0));
        var tunnel = new Http3Tunnel(stream);
        bool writerDone = false;

        Task writer = Task.Run(() =>
        {
            for (int i = 0; i < writes; i++)
                tunnel.WriteAsync(BitConverter.GetBytes(i), CancellationToken.None);
            Volatile.Write(ref writerDone, true);
        });

        Task pump = Task.Run(() =>
        {
            while (!Volatile.Read(ref writerDone))
                tunnel.PumpOutbound();
            tunnel.PumpOutbound(); // final drain
        });

        Assert.That(Task.WhenAll(writer, pump).Wait(TimeSpan.FromSeconds(30)), Is.True);

        // Every write becomes one DATA frame: type 0x00, length 4, four payload bytes = 6 bytes each.
        Assert.That(stream.Send.PendingBytes, Is.EqualTo(writes * 6), "Writes were lost or duplicated.");
    }

    #endregion

    #region Under concurrency

    /// <summary>
    /// The pump delivers on one thread while a consumer reads on another, awaiting each read so the
    /// continuation genuinely lands on a thread-pool thread. Every chunk must arrive exactly once
    /// and in order.
    /// </summary>
    [Test]
    public void PumpAndConsumerOnDifferentThreads_LoseNoChunksAndKeepOrder()
    {
        const int chunks = 20_000;

        Http3Tunnel tunnel = NewTunnel();
        var received = new List<int>(chunks);

        Task consumer = Task.Run(async () =>
        {
            while (true)
            {
                byte[]? chunk = await tunnel.ReadAsync(CancellationToken.None).ConfigureAwait(false);
                if (chunk is null)
                    return;
                received.Add(BitConverter.ToInt32(chunk));
            }
        });

        Task producer = Task.Run(() =>
        {
            for (int i = 0; i < chunks; i++)
                tunnel.Deliver(BitConverter.GetBytes(i));
            tunnel.End();
        });

        Assert.That(Task.WhenAll(consumer, producer).Wait(TimeSpan.FromSeconds(30)), Is.True,
                    "Producer and consumer did not finish — a read was left hanging.");

        Assert.That(received, Has.Count.EqualTo(chunks), "Chunks were lost.");
        Assert.That(received, Is.EqualTo(Enumerable.Range(0, chunks).ToList()), "Chunks arrived out of order.");
    }

    /// <summary>
    /// Datagrams (RFC 9297) have their own queue with its own producer/consumer split: the pump
    /// delivers, the application drains. Nothing may be handed out twice or torn.
    /// </summary>
    [Test]
    public void DatagramQueue_SurvivesConcurrentDeliverAndReceive()
    {
        const int datagrams = 20_000;

        Http3Tunnel tunnel = NewTunnel();
        int taken = 0;
        var seen = new HashSet<int>();
        bool producerDone = false;

        Task producer = Task.Run(() =>
        {
            for (int i = 0; i < datagrams; i++)
                tunnel.DeliverDatagram(BitConverter.GetBytes(i));
            Volatile.Write(ref producerDone, true);
        });

        Task consumer = Task.Run(() =>
        {
            while (!Volatile.Read(ref producerDone) || tunnel.TryReceiveDatagram(out _))
            {
                if (!tunnel.TryReceiveDatagram(out byte[]? payload) || payload is null)
                    continue;
                taken++;
                // The buffer is bounded and drops the OLDEST on overflow, so not every datagram has
                // to arrive — but none may arrive twice, and none may be a torn or short array.
                Assert.That(payload, Has.Length.EqualTo(4));
                Assert.That(seen.Add(BitConverter.ToInt32(payload)), Is.True, "A datagram was handed out twice.");
            }
        });

        Assert.That(Task.WhenAll(producer, consumer).Wait(TimeSpan.FromSeconds(30)), Is.True);
        Assert.That(taken, Is.GreaterThan(0));
    }

    #endregion

}
