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
    public void RecordReceived(ulong packetNumber, EcnCodepoint ecn = EcnCodepoint.NotEct, long nowTicks = 0)
    {
        if ((long)packetNumber > _largestReceived)
            _largestReceived = (long)packetNumber;

        // Below the pruning bound the peer has already confirmed our acknowledgment (§13.2.4):
        // a packet arriving there is a duplicate or extremely late reordering and needs no new ACK.
        if (packetNumber < _prunedBelow)
            return;

        _received.Add(packetNumber);
        AckPending = true;

        // §13.2.5 measures the delay from the arrival of the LARGEST-numbered packet to the moment
        // the ACK goes out, so only that arrival time matters.
        if ((long)packetNumber >= _largestReceived)
            _largestReceivedTicks = nowTicks;

        // §13.2.1: "packets marked with the ECN Congestion Experienced (CE) codepoint … SHOULD be
        // acknowledged immediately, to reduce the peer's response time to congestion events."
        if (ecn == EcnCodepoint.Ce)
            ImmediateAckNeeded = true;
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
        AckElicitingSinceLastAck = 0;
        ImmediateAckNeeded = false;
        _firstUnackedElicitingTicks = -1;

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

    // ---- Acknowledgment timing (RFC 9000 §13.2) ------------------------------------------------

    private long _largestReceivedTicks;      // arrival of the largest-numbered packet (§13.2.5)
    private long _firstUnackedElicitingTicks = -1;
    private long _largestAckEliciting = -1;

    /// <summary>
    /// Ack-eliciting packets received since the last ACK went out. §13.2.2: "A receiver SHOULD send
    /// an ACK frame after receiving at least two ack-eliciting packets."
    /// </summary>
    public int AckElicitingSinceLastAck { get; private set; }

    /// <summary>
    /// An ACK must go out now rather than on the timer — §13.2.1 asks for this on reordering (which
    /// helps the peer's loss detection) and on an ECN-CE mark.
    /// </summary>
    public bool ImmediateAckNeeded { get; private set; }

    /// <summary>
    /// Reports that a received packet carried at least one ack-eliciting frame. Only these start the
    /// acknowledgment clock: §13.2.1 forbids answering a non-ack-eliciting packet with another one,
    /// "to avoid an infinite feedback loop of acknowledgments".
    /// </summary>
    public void OnAckElicitingReceived(ulong packetNumber, long nowTicks)
    {
        AckElicitingSinceLastAck++;
        if (_firstUnackedElicitingTicks < 0)
            _firstUnackedElicitingTicks = nowTicks;

        // §13.2.1, the two cases that call for an immediate ACK: a packet number below one already
        // received, or one above the highest with a gap in between. Both mean the peer is looking at
        // a hole and would otherwise wait out its loss timer.
        if (_largestAckEliciting >= 0 &&
            ((long)packetNumber < _largestAckEliciting || (long)packetNumber > _largestAckEliciting + 1))
            ImmediateAckNeeded = true;

        if ((long)packetNumber > _largestAckEliciting)
            _largestAckEliciting = (long)packetNumber;
    }

    /// <summary>
    /// Whether an ACK is due (RFC 9000 §13.2.1/§13.2.2). <paramref name="immediateSpace"/> is set for
    /// Initial and Handshake, which "MUST" be acknowledged immediately; the application space may
    /// wait for a second ack-eliciting packet or for <paramref name="maxAckDelay"/> to elapse.
    /// </summary>
    public bool IsAckDue(long nowTicks, TimeSpan maxAckDelay, bool immediateSpace)
    {
        if (!AckPending)
            return false;
        if (immediateSpace || ImmediateAckNeeded)
            return true;
        if (AckElicitingSinceLastAck >= 2)
            return true;
        return _firstUnackedElicitingTicks >= 0 &&
               nowTicks - _firstUnackedElicitingTicks >= maxAckDelay.Ticks;
    }

    /// <summary>
    /// When the pending ACK is due at the latest, or -1 when nothing is waiting. Drives the timer
    /// that keeps the max_ack_delay promise of §13.2.1.
    /// </summary>
    public long AckDeadlineTicks(TimeSpan maxAckDelay)
        => AckPending && _firstUnackedElicitingTicks >= 0
               ? _firstUnackedElicitingTicks + maxAckDelay.Ticks
               : -1;

    /// <summary>
    /// The delay to report in the ACK Delay field: the time between the arrival of the largest
    /// packet and now (§13.2.5), encoded as microseconds divided by 2^<paramref name="exponent"/>.
    /// </summary>
    public ulong EncodeAckDelay(long nowTicks, ulong exponent)
    {
        long delayTicks = nowTicks - _largestReceivedTicks;
        if (delayTicks <= 0)
            return 0;
        ulong microseconds = (ulong)(delayTicks / (TimeSpan.TicksPerMillisecond / 1000));
        return microseconds >> (int)Math.Min(exponent, 62);
    }
}
