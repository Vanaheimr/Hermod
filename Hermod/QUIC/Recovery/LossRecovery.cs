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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;

/// <summary>
/// A sent packet tracked for loss detection and retransmission.
/// </summary>
public sealed class SentPacket
{
    public required ulong PacketNumber { get; init; }
    public required long TimeSentTicks { get; init; }
    public required bool AckEliciting { get; init; }
    public required int Size { get; init; }

    /// <summary>
    /// Frames to resend on loss (CRYPTO, STREAM). ACK/flow control are re-derived.
    /// </summary>
    public required IReadOnlyList<Frame> RetransmittableFrames { get; init; }

    /// <summary>
    /// This packet is a PMTU probe (RFC 9000 §14.4): deliberately larger than the current maximum
    /// datagram size, so its loss says something about the path MTU rather than about congestion.
    /// </summary>
    public bool IsPmtuProbe { get; init; }
}

/// <summary>
/// Loss detection and retransmission bookkeeping per RFC 9002 §6 – separate per packet-number space
/// (Initial/Handshake/Application). Detects losses via packet and time thresholds, updates the RTT
/// estimator and the congestion controller, and returns the frames of lost packets.
/// Timestamps are <see cref="TimeSpan.Ticks"/> (100 ns) of a monotonic clock.
/// </summary>
public sealed class LossRecovery
{
    private const int PacketThreshold = 3;

    /// <summary>
    /// PTO multiplier for the persistent-congestion duration (RFC 9002 §7.6, recommended: 3).
    /// </summary>
    private const int PersistentCongestionThreshold = 3;

    private sealed class SpaceState
    {
        public readonly SortedDictionary<ulong, SentPacket> Sent = [];
        public long LargestAcked = -1;
        public long LossTimeTicks = -1;
        public ulong LargestCeCount; // highest ECN-CE counter reported so far (RFC 9002 §A.7)
    }

    private readonly SpaceState[] _spaces = [new(), new(), new()];

    /// <summary>
    /// Time of the first RTT sample; before it, persistent congestion does not apply (RFC 9002 §7.6.2).
    /// </summary>
    private long _firstRttSampleTicks = -1;

    public RttEstimator Rtt { get; } = new();
    public NewRenoCongestionControl Congestion { get; } = new();
    public TimeSpan MaxAckDelay { get; set; } = TimeSpan.FromMilliseconds(25);

    /// <summary>
    /// Time of the most recently sent ack-eliciting packet (for PTO); -1 = none in flight.
    /// </summary>
    public long LastAckElicitingSentTicks { get; private set; } = -1;

    /// <summary>
    /// Time of the last packet sent at all (also non-ack-eliciting) – the PTO base for a client
    /// whose peer has not yet completed address validation.
    /// </summary>
    private long _lastSentTicks = -1;

    /// <summary>
    /// Whether the peer has completed address validation (RFC 9002 §A.6
    /// <c>PeerCompletedAddressValidation</c>). For the server always <c>true</c>; the client sets it
    /// once it has received a Handshake ACK or the handshake is complete.
    /// <para>While it is <c>false</c>, the client MUST keep a PTO armed EVEN WITHOUT packets in
    /// flight (RFC 9002 §6.2.2.1) — otherwise a lost server flight deadlocks the handshake: the
    /// server waits (address not yet validated, anti-amplification limit) for more data from the
    /// client, and the client waits for the server.</para>
    /// </summary>
    public bool PeerCompletedAddressValidation { get; set; } = true;

    public int PtoCount { get; private set; }

    /// <summary>
    /// Optional observer for declared losses: (packet-number space, packet number, trigger) —
    /// "reordering_threshold" or "time_threshold" per RFC 9002 §6.1. Used for the qlog
    /// (<c>quic:packet_lost</c>); <c>null</c> = nothing is reported.
    /// </summary>
    public Action<int, ulong, string>? OnPacketLost { get; set; }

    /// <summary>
    /// A PMTU probe of this datagram size was acknowledged — the path carries it (RFC 9000 §14.3).
    /// </summary>
    public Action<int>? OnPmtuProbeAcked { get; set; }

    /// <summary>
    /// A PMTU probe of this datagram size was declared lost. Says nothing about congestion, only
    /// about the path (§14.4).
    /// </summary>
    public Action<int>? OnPmtuProbeLost { get; set; }

    /// <summary>
    /// Discards the entire loss-recovery state of a packet-number space (RFC 9002 §6.4) when its
    /// protection keys are discarded (Initial/Handshake after the handshake): the not-yet-acknowledged
    /// packets no longer count, their bytes are removed from <c>bytes_in_flight</c>.
    /// </summary>
    public void DiscardSpace(int space)
    {
        SpaceState st = _spaces[space];
        foreach (SentPacket sp in st.Sent.Values)
            if (sp.AckEliciting)
                Congestion.OnPacketDiscarded(sp.Size);
        st.Sent.Clear();
        st.LargestAcked = -1;
        st.LossTimeTicks = -1;
        UpdateInFlightMarker();
    }

    public void OnPacketSent(int space, SentPacket packet)
    {
        _spaces[space].Sent[packet.PacketNumber] = packet;
        if (packet.TimeSentTicks > _lastSentTicks)
            _lastSentTicks = packet.TimeSentTicks;
        if (packet.AckEliciting)
        {
            Congestion.OnPacketSent(packet.Size);
            LastAckElicitingSentTicks = packet.TimeSentTicks;
        }
    }

    /// <summary>
    /// Processes a received ACK: removes acknowledged packets, updates RTT/congestion window and
    /// detects lost packets. Returns the frames of lost packets to resend.
    /// </summary>
    public List<Frame> OnAckReceived(int space, AckFrame ack, TimeSpan ackDelay, long nowTicks)
    {
        SpaceState st = _spaces[space];
        if ((long)ack.LargestAcknowledged > st.LargestAcked)
            st.LargestAcked = (long)ack.LargestAcknowledged;

        SentPacket? largestNewlyAcked = null;
        foreach (PacketNumberRange range in ack.Ranges)
        {
            for (ulong pn = range.Smallest; pn <= range.Largest; pn++)
            {
                if (!st.Sent.Remove(pn, out SentPacket? sp))
                    continue;
                Congestion.OnPacketAcked(sp.Size, sp.TimeSentTicks);
                if (sp.IsPmtuProbe)
                    OnPmtuProbeAcked?.Invoke(sp.Size); // the path carries a datagram of this size
                if (largestNewlyAcked is null || sp.PacketNumber > largestNewlyAcked.PacketNumber)
                    largestNewlyAcked = sp;
            }
        }

        // Derive the RTT only from the largest, newly acknowledged and ack-eliciting packet.
        if (largestNewlyAcked is { AckEliciting: true } &&
            largestNewlyAcked.PacketNumber == ack.LargestAcknowledged)
        {
            var rttSample = TimeSpan.FromTicks(Math.Max(0, nowTicks - largestNewlyAcked.TimeSentTicks));
            Rtt.AddSample(rttSample, ackDelay, MaxAckDelay);
            if (_firstRttSampleTicks < 0)
                _firstRttSampleTicks = nowTicks; // RFC 9002 §B.8: first_rtt_sample
        }

        // ECN (RFC 9002 §A.7 ProcessECN): if the ACK reports an increased CE counter, that counts as congestion.
        if (ack.Ecn is { } ecn && ecn.CongestionExperienced > st.LargestCeCount)
        {
            st.LargestCeCount = ecn.CongestionExperienced;
            long sentTime = largestNewlyAcked?.TimeSentTicks ?? nowTicks;
            Congestion.OnEcnCongestionEvent(sentTime, nowTicks);
        }

        PtoCount = 0;
        UpdateInFlightMarker();
        return DetectLostPackets(space, nowTicks);
    }

    private List<Frame> DetectLostPackets(int space, long nowTicks)
    {
        SpaceState st = _spaces[space];
        var lostFrames = new List<Frame>();
        var lostAckEliciting = new List<SentPacket>();
        long lossDelayTicks = Rtt.LossDelay().Ticks;
        long lostSendThreshold = nowTicks - lossDelayTicks;
        st.LossTimeTicks = -1;
        long largestLostTime = -1;
        int lostBytes = 0;

        foreach (SentPacket sp in st.Sent.Values.ToList())
        {
            if ((long)sp.PacketNumber >= st.LargestAcked)
                continue; // only packets older than the largest acknowledged one

            bool lostByThreshold = (long)sp.PacketNumber <= st.LargestAcked - PacketThreshold;
            bool lostByTime = sp.TimeSentTicks <= lostSendThreshold;

            if (lostByThreshold || lostByTime)
            {
                OnPacketLost?.Invoke(space, sp.PacketNumber,
                                     lostByThreshold ? "reordering_threshold" : "time_threshold");
                st.Sent.Remove(sp.PacketNumber);

                if (sp.IsPmtuProbe)
                {
                    // RFC 9000 §14.4: a probe is deliberately too big, so "Loss of a QUIC packet that
                    // is carried in a PMTU probe is therefore not a reliable indication of congestion
                    // and SHOULD NOT trigger a congestion control reaction". The bytes still have to
                    // leave the in-flight count — they consumed window on the way out — which is what
                    // OnPacketDiscarded does WITHOUT the congestion event OnPacketsLost would raise.
                    // Its PING/PADDING are not requeued either: the discovery decides whether to
                    // probe again, and at which size.
                    if (sp.AckEliciting)
                        Congestion.OnPacketDiscarded(sp.Size);
                    OnPmtuProbeLost?.Invoke(sp.Size);
                    continue;
                }

                lostFrames.AddRange(sp.RetransmittableFrames);
                if (sp.AckEliciting)
                {
                    lostBytes += sp.Size;
                    largestLostTime = Math.Max(largestLostTime, sp.TimeSentTicks);
                    lostAckEliciting.Add(sp);
                }
            }
            else
            {
                long t = sp.TimeSentTicks + lossDelayTicks;
                st.LossTimeTicks = st.LossTimeTicks < 0 ? t : Math.Min(st.LossTimeTicks, t);
            }
        }

        if (lostBytes > 0)
            Congestion.OnPacketsLost(lostBytes, largestLostTime, nowTicks);
        DetectPersistentCongestion(lostAckEliciting);
        return lostFrames;
    }

    /// <summary>
    /// Checks for persistent congestion (RFC 9002 §7.6.2): collapses the window when two ack-eliciting
    /// packets are lost, nothing was acknowledged in between and their send spacing exceeds the
    /// persistent-congestion duration – and both were sent after the first RTT sample.
    /// <para>Conservative approximation: "nothing acknowledged in between" is checked via consecutive
    /// packet numbers of the lost (always ack-eliciting) packets. A gap (an acknowledged or still
    /// in-flight packet) breaks the run; an intervening pure ACK packet leads at most to a missed –
    /// never a false – detection.</para>
    /// </summary>
    private void DetectPersistentCongestion(List<SentPacket> lostAckEliciting)
    {
        if (_firstRttSampleTicks < 0)
            return; // only after the first RTT sample

        // Consider only packets sent after the first RTT sample (RFC 9002 §B.8).
        List<SentPacket> candidates = lostAckEliciting
            .Where(p => p.TimeSentTicks > _firstRttSampleTicks)
            .OrderBy(p => p.PacketNumber)
            .ToList();
        if (candidates.Count < 2)
            return;

        long pcDuration = Rtt.GetProbeTimeout(MaxAckDelay).Ticks * PersistentCongestionThreshold;

        // Find the longest run of consecutive packet numbers and check its time span.
        int runStart = 0;
        for (int i = 1; i <= candidates.Count; i++)
        {
            bool endOfRun = i == candidates.Count ||
                            candidates[i].PacketNumber != candidates[i - 1].PacketNumber + 1;
            if (!endOfRun)
                continue;

            SentPacket first = candidates[runStart];
            SentPacket last = candidates[i - 1];
            if (last != first && last.TimeSentTicks - first.TimeSentTicks > pcDuration)
            {
                Congestion.OnPersistentCongestion();
                return;
            }
            runStart = i;
        }
    }

    /// <summary>
    /// PTO deadline (RFC 9002 §6.2): last ack-eliciting send time + PTO·2^ptoCount. -1 = no timer.
    /// <para>Special case RFC 9002 §6.2.2.1: with nothing ack-eliciting in flight the timer is
    /// normally cancelled — but NOT at a client whose peer has not yet completed address validation.
    /// It has to keep probing to unblock the server; otherwise a lost handshake flight leaves both
    /// sides waiting for each other forever.</para>
    /// </summary>
    public long GetProbeTimeoutDeadline()
    {
        long baseTicks = LastAckElicitingSentTicks;
        if (baseTicks < 0)
        {
            if (PeerCompletedAddressValidation || _lastSentTicks < 0)
                return -1;
            baseTicks = _lastSentTicks;
        }
        long pto = Rtt.GetProbeTimeout(MaxAckDelay).Ticks << PtoCount;
        return baseTicks + pto;
    }

    /// <summary>
    /// Increments the PTO backoff counter (to be called when a PTO expires).
    /// </summary>
    public void OnProbeTimeoutFired() => PtoCount++;

    /// <summary>
    /// Returns the frames of the oldest still-unacknowledged packet of a space for retransmission
    /// (probe against tail loss). Empty when nothing is outstanding.
    /// </summary>
    public List<Frame> GetProbeFrames(int space)
    {
        foreach (SentPacket sp in _spaces[space].Sent.Values)
            if (sp.RetransmittableFrames.Count > 0)
                return [.. sp.RetransmittableFrames];
        return [];
    }

    /// <summary>
    /// Called when 0-RTT is rejected (RFC 9001 §4.6.2): removes all packets sent as 0-RTT (never
    /// acknowledged) – identifiable via <paramref name="maxZeroRttPacketNumber"/>, since 0-RTT packet
    /// numbers precede 1-RTT ones – from loss tracking and returns their frames for immediate
    /// retransmission over 1-RTT. This removes the wait for time threshold/PTO, and because the
    /// packets vanish from the sent list, they are not additionally double-sent as "lost".
    /// </summary>
    public List<Frame> OnZeroRttRejected(int space, ulong maxZeroRttPacketNumber)
    {
        SpaceState st = _spaces[space];
        var frames = new List<Frame>();
        foreach (ulong pn in st.Sent.Keys.Where(pn => pn <= maxZeroRttPacketNumber).ToList())
            if (st.Sent.Remove(pn, out SentPacket? sp))
                frames.AddRange(sp.RetransmittableFrames);
        UpdateInFlightMarker();
        return frames;
    }

    private void UpdateInFlightMarker()
    {
        foreach (SpaceState st in _spaces)
            foreach (SentPacket sp in st.Sent.Values)
                if (sp.AckEliciting)
                    return; // there are still ack-eliciting packets in flight
        LastAckElicitingSentTicks = -1;
    }
}
