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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests;

/// <summary>
/// One direction of an impaired in-process link: drops, delays, reorders and duplicates datagrams —
/// seeded, and therefore reproducible down to the individual datagram.
/// <para>
/// Slots into the usual pump idiom of the tests, between producing and delivering:
/// <code>
/// foreach (byte[] dg in link.Transfer(client.GetDatagramsToSend()))
///     server.ProcessDatagram(dg);
/// </code>
/// Delayed datagrams stay in the link and surface in a later round — which is exactly what produces
/// reordering: a datagram sent later but not delayed overtakes one that was held back.
/// </para>
/// <para>
/// Datagrams are always handed over as COPIES: <c>ProcessDatagram</c> unprotects the buffer in place
/// (RFC 9001 §5.4), so a duplicate has to be its own buffer — just as on a real network.
/// </para>
/// </summary>
internal sealed class LossyLink(Random random)
{
    private readonly record struct Pending(byte[] Datagram, long DeliverAtRound, long Sequence);

    private readonly List<Pending> _pending = [];
    private long _round;
    private long _sequence;
    private long _highestDelivered = -1;

    /// <summary>
    /// Probability (0..1) that a datagram is lost.
    /// </summary>
    public double DropRate { get; init; }

    /// <summary>
    /// Probability (0..1) that a datagram arrives a second time.
    /// </summary>
    public double DuplicateRate { get; init; }

    /// <summary>
    /// Probability (0..1) that a datagram is held back by a few rounds (⇒ reordering).
    /// </summary>
    public double ReorderRate { get; init; }

    /// <summary>
    /// Maximum hold time in pump rounds for delayed/duplicated datagrams.
    /// </summary>
    public int MaxDelayRounds { get; init; } = 3;

    public int Sent { get; private set; }
    public int Dropped { get; private set; }
    public int Duplicated { get; private set; }
    public int Delayed { get; private set; }
    public int Delivered { get; private set; }

    /// <summary>
    /// Number of datagrams that reached the peer AFTER a datagram sent later (genuine reordering).
    /// </summary>
    public int DeliveredOutOfOrder { get; private set; }

    /// <summary>
    /// Datagrams still held in the link.
    /// </summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Takes the datagrams of one send pass, applies the impairments, and returns everything due
    /// this round (which may include datagrams from earlier rounds).
    /// </summary>
    public List<byte[]> Transfer(IReadOnlyList<byte[]> sent)
    {
        _round++;

        foreach (byte[] datagram in sent)
        {
            Sent++;

            if (random.NextDouble() < DropRate)
            {
                Dropped++;
                continue;
            }

            int delay = random.NextDouble() < ReorderRate ? random.Next(1, MaxDelayRounds + 1) : 0;
            if (delay > 0)
                Delayed++;
            Enqueue(datagram, delay);

            if (random.NextDouble() < DuplicateRate)
            {
                Duplicated++;
                Enqueue(datagram, delay + random.Next(0, MaxDelayRounds + 1));
            }
        }

        return TakeDue();
    }

    private void Enqueue(byte[] datagram, int delay)
        => _pending.Add(new Pending([.. datagram], _round + delay, _sequence++));

    private List<byte[]> TakeDue()
    {
        // Within a round the send order is preserved — reordering comes solely from the hold-back,
        // never from an artefact of this loop.
        var dueNow = new List<Pending>();
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].DeliverAtRound > _round)
                continue;
            dueNow.Add(_pending[i]);
            _pending.RemoveAt(i);
        }
        dueNow.Reverse();

        var due = new List<byte[]>(dueNow.Count);
        foreach (Pending pending in dueNow)
        {
            if (pending.Sequence < _highestDelivered)
                DeliveredOutOfOrder++; // a datagram sent earlier arrives after a later one
            else
                _highestDelivered = pending.Sequence;
            Delivered++;
            due.Add(pending.Datagram);
        }
        return due;
    }

    public override string ToString()
        => $"sent {Sent}, dropped {Dropped}, delayed {Delayed}, duplicated {Duplicated}, " +
           $"delivered {Delivered} (thereof {DeliveredOutOfOrder} out of order)";
}

/// <summary>
/// An impaired link in both directions with ONE seed — the whole scenario is thus reproducible.
/// </summary>
internal sealed class LossyNetwork
{
    public LossyNetwork(int seed, double dropRate = 0, double duplicateRate = 0, double reorderRate = 0,
                        int maxDelayRounds = 3)
    {
        // Separate Random instances per direction (derived from the one seed) so an extra datagram
        // in one direction does not shift the impairment sequence of the other.
        var seedSource = new Random(seed);
        ClientToServer = new LossyLink(new Random(seedSource.Next()))
        {
            DropRate = dropRate, DuplicateRate = duplicateRate, ReorderRate = reorderRate, MaxDelayRounds = maxDelayRounds,
        };
        ServerToClient = new LossyLink(new Random(seedSource.Next()))
        {
            DropRate = dropRate, DuplicateRate = duplicateRate, ReorderRate = reorderRate, MaxDelayRounds = maxDelayRounds,
        };
    }

    public LossyLink ClientToServer { get; }
    public LossyLink ServerToClient { get; }

    /// <summary>
    /// Datagrams still held in either direction.
    /// </summary>
    public int PendingCount => ClientToServer.PendingCount + ServerToClient.PendingCount;

    public override string ToString() => $"client→server: {ClientToServer} | server→client: {ServerToClient}";
}
