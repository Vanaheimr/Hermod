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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;

/// <summary>
/// A packet-number space (RFC 9000 §12.3): separate for Initial, Handshake and Application. Assigns
/// ascending packet numbers when sending and remembers received numbers for ACK generation.
/// </summary>
public sealed class PacketNumberSpace
{
    /// <summary>
    /// Upper bound for the ACK frames of our own we keep track of (RFC 9000 §13.2.4). More than a
    /// handful is never needed: the reported Largest Acknowledged only ever grows, so an older entry
    /// is never more informative than a newer one — dropping the oldest costs at most one pruning
    /// opportunity, never correctness.
    /// </summary>
    private const int MaxTrackedAckFrames = 64;

    /// <summary>
    /// Upper bound for the ACK ranges of one frame (RFC 9000 §13.2.4 "Limiting Ranges"). Pruning
    /// keeps the count small in normal operation, but a pathological loss pattern could otherwise
    /// still produce an ACK frame that no longer fits into a packet. The ranges are descending, so
    /// the cap keeps the NEWEST ones — dropping the oldest merely leaves those packets
    /// unacknowledged for now (at worst a spurious retransmission), which the RFC explicitly allows.
    /// </summary>
    private const int MaxAckRanges = 32;

    private ulong _nextToSend;
    private readonly SortedSet<ulong> _received = [];

    // Cumulative ECN counters of the received packets of this space (RFC 9000 §13.4.2), reported in the ACK frame.
    private ulong _ect0Count;
    private ulong _ect1Count;
    private ulong _ceCount;

    // Packet numbers of our own packets that carried an ACK frame, together with the Largest
    // Acknowledged reported in it (RFC 9000 §13.2.4). Ascending by packet number.
    private readonly List<(ulong PacketNumber, ulong LargestAcked)> _ackFramesSent = [];

    // Everything below this bound has been dropped from _received after the peer confirmed our
    // acknowledgment; _prunedCount counts how many distinct numbers that were.
    private ulong _prunedBelow;
    private ulong _prunedCount;

    // Tracked separately from _received, which pruning may empty out entirely.
    private long _largestReceived = -1;

    /// <summary>
    /// Number of received packets with a CE mark (diagnostics/test).
    /// </summary>
    public ulong ReceivedCeCount => _ceCount;

    /// <summary>
    /// Largest packet number acknowledged by the peer (for choosing the PN encoding length); -1 = none.
    /// </summary>
    public long LargestAckedByPeer { get; private set; } = -1;

    /// <summary>
    /// Largest packet number received so far (for PN reconstruction on receive); -1 = none.
    /// </summary>
    public long LargestReceived => _largestReceived;

    /// <summary>
    /// There are received packets not yet acknowledged via ACK.
    /// </summary>
    public bool AckPending { get; private set; }

    /// <summary>
    /// Number of packet numbers currently held for ACK generation (diagnostics/test): shows that
    /// the state does not grow without bound over the lifetime of a connection.
    /// </summary>
    public int TrackedReceivedCount => _received.Count;

    /// <summary>
    /// <c>true</c> when reception from packet number 0 is gap-free (i.e. exactly {0,1,…,Max}, no
    /// missing numbers). Packet numbers start at 0 per space (RFC 9000 §12.3), so this holds exactly
    /// when the number of received packets is <c>Max+1</c>. Usage: the server thereby detects that it
    /// has received all 0-RTT packets (RFC 9001 §4.9.3, "keeping track of missing packet numbers").
    /// <para>Numbers already pruned per §13.2.4 count as received — they were acknowledged, and the
    /// peer confirmed that acknowledgment.</para>
    /// </summary>
    public bool IsContiguousFromZero =>
        _largestReceived >= 0 && (ulong)_received.Count + _prunedCount == (ulong)_largestReceived + 1;

    /// <summary>
    /// Assigns the next packet number to send.
    /// </summary>
    public ulong NextPacketNumber() => _nextToSend++;

    /// <summary>
    /// Records a successfully unprotected, received packet number along with its ECN codepoint.
    /// </summary>
    public void RecordReceived(ulong packetNumber, EcnCodepoint ecn = EcnCodepoint.NotEct)
    {
        if ((long)packetNumber > _largestReceived)
            _largestReceived = (long)packetNumber;

        // Below the pruning bound the peer has already confirmed our acknowledgment (§13.2.4):
        // a packet arriving there is a duplicate or extremely late reordering and needs no new ACK.
        if (packetNumber < _prunedBelow)
            return;

        _received.Add(packetNumber);
        AckPending = true;
        switch (ecn)
        {
            case EcnCodepoint.Ect0: _ect0Count++; break;
            case EcnCodepoint.Ect1: _ect1Count++; break;
            case EcnCodepoint.Ce: _ceCount++; break;
        }
    }

    /// <summary>
    /// Processes a received ACK frame (updates the largest acknowledged number).
    /// </summary>
    public void OnAckReceived(ulong largestAcknowledged)
    {
        if ((long)largestAcknowledged > LargestAckedByPeer)
            LargestAckedByPeer = (long)largestAcknowledged;
    }

    /// <summary>
    /// Processes a received ACK frame and additionally limits the ACK state per RFC 9000 §13.2.4:
    /// when the peer acknowledges one of our packets that carried an ACK frame, we may stop
    /// acknowledging everything up to that frame's Largest Acknowledged — those packet numbers are
    /// dropped. Without this the received set (and with it every ACK frame we build) would grow for
    /// the entire lifetime of the connection.
    /// </summary>
    public void OnAckReceived(AckFrame ack)
    {
        OnAckReceived(ack.LargestAcknowledged);

        // From the newest entry backwards: the first one the peer acknowledges carries the highest
        // Largest Acknowledged of all confirmed entries, since that value only ever grows. Everything
        // up to it is then superfluous.
        for (int i = _ackFramesSent.Count - 1; i >= 0; i--)
        {
            if (!ack.Covers(_ackFramesSent[i].PacketNumber))
                continue;
            PruneUpTo(_ackFramesSent[i].LargestAcked);
            _ackFramesSent.RemoveRange(0, i + 1);
            break;
        }
    }

    /// <summary>
    /// Records that we sent an ACK frame with <paramref name="largestAcknowledged"/> in our packet
    /// <paramref name="packetNumber"/> (RFC 9000 §13.2.4). Acknowledgment of that packet later
    /// releases the corresponding ACK state.
    /// </summary>
    public void OnAckFrameSent(ulong packetNumber, ulong largestAcknowledged)
    {
        _ackFramesSent.Add((packetNumber, largestAcknowledged));
        if (_ackFramesSent.Count > MaxTrackedAckFrames)
            _ackFramesSent.RemoveAt(0);
    }

    /// <summary>
    /// Drops all received packet numbers up to and including <paramref name="largest"/>.
    /// </summary>
    private void PruneUpTo(ulong largest)
    {
        if (_prunedBelow > largest)
            return; // already pruned further

        while (_received.Count > 0 && _received.Min <= largest)
        {
            _received.Remove(_received.Min);
            _prunedCount++;
        }
        _prunedBelow = largest + 1;
    }

    /// <summary>
    /// Builds an ACK frame over all packets received so far and marks the ACKs as sent.
    /// Returns <c>null</c> when there is nothing to acknowledge.
    /// </summary>
    public AckFrame? BuildAck(ulong ackDelay = 0)
    {
        if (_received.Count == 0)
            return null;
        AckPending = false;

        // Once ECN-marked packets have been received, every ACK MUST carry the cumulative counters
        // (type 0x03, RFC 9000 §13.4.2). Without ECN marks, the simple ACK (0x02) remains.
        EcnCounts? ecn = (_ect0Count | _ect1Count | _ceCount) != 0
            ? new EcnCounts(_ect0Count, _ect1Count, _ceCount)
            : null;
        // _received is a SortedSet ⇒ already ascending and duplicate-free: one walk, no copy.
        AckFrame ack = AckFrame.FromAscendingPacketNumbers(_received, ackDelay, MaxAckRanges);
        return ack with { Ecn = ecn };
    }

    /// <summary>
    /// Re-arms the ACK: to be called when an already-built ACK frame could not be sent after all
    /// (e.g. deferred by the anti-amplification budget), so the next send builds a fresh one.
    /// </summary>
    public void MarkAckPending() => AckPending = _received.Count > 0;
}
