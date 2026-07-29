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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;

/// <summary>
/// Datagram Packetization Layer PMTU Discovery (RFC 8899, applied to QUIC by RFC 9000 §14.3): finds
/// how large a datagram the path actually carries, by sending deliberately oversized probes and
/// watching whether they are acknowledged.
/// <para>
/// The whole point is what we would otherwise leave on the table. RFC 9000 §14.1 guarantees only
/// 1200 bytes on every path, so without discovery every datagram is sized for the worst case — on an
/// ordinary 1500-byte Ethernet path, where 1472 bytes of IPv4/UDP payload fit, that wastes about 18 %
/// of every packet forever.
/// </para>
/// <para>
/// No ICMP is involved. §14.3 builds on "Probing using padding data" (RFC 8899 §4.1): a probe is an
/// ordinary ack-eliciting QUIC packet padded to the size under test, so an acknowledgment is proof
/// the path carried it and silence is evidence it did not. That makes this work behind middleboxes
/// that swallow ICMP, which is most of them.
/// </para>
/// </summary>
public sealed class PathMtuDiscovery
{

    /// <summary>
    /// Smallest datagram size QUIC may use at all (RFC 9000 §14.1). DPLPMTUD calls this BASE_PLPMTU,
    /// and §14.3 requires MIN_PLPMTU to equal it: the search never goes below this, and a path that
    /// cannot carry it is not usable for QUIC.
    /// </summary>
    public const int BasePlpmtu = 1200;

    /// <summary>
    /// Default upper end of the search. 1452 = 1500-byte Ethernet MTU minus 40 bytes of IPv6 and
    /// 8 bytes of UDP header — the largest datagram that survives the common path without
    /// fragmentation, and still correct on IPv4, where it simply leaves 20 bytes unused.
    /// <para>
    /// Deliberately not 9000: jumbo frames only survive when every hop carries them, which is a
    /// property of a datacentre, not of the internet. Probing above the real MTU is not harmful —
    /// the probes are simply lost and the search backs off — but it costs probes and time, so paths
    /// known to be larger have to say so via the ceiling.
    /// </para>
    /// </summary>
    public const int DefaultSearchCeiling = 1452;

    /// <summary>
    /// Probes per size before the size counts as unreachable. RFC 8899 §5.1.2 calls this MAX_PROBES
    /// and suggests 3: a single loss is far more likely to be ordinary loss than a too-large packet.
    /// </summary>
    public const int MaxProbesPerSize = 3;

    private readonly int _ceiling;
    private int _confirmed = BasePlpmtu;  // largest size known to work
    private int _upperBound;              // largest size not yet ruled out
    private int _probeSize;               // size currently under test, 0 = none
    private int _probesSent;              // attempts made at that size
    private bool _probeOutstanding;       // one is in flight: wait for the verdict before sending more
    private bool _started;

    /// <param name="searchCeiling">
    /// Largest datagram size to look for. Clamped to at least <see cref="BasePlpmtu"/>; passing that
    /// value disables the search entirely, which is the honest way to turn the feature off.
    /// </param>
    public PathMtuDiscovery(int searchCeiling = DefaultSearchCeiling)
    {
        _ceiling    = Math.Max(BasePlpmtu, searchCeiling);
        _upperBound = _ceiling;
    }

    /// <summary>
    /// Largest datagram size currently proven to work — what the send path may use.
    /// </summary>
    public int MaxDatagramSize => _confirmed;

    /// <summary>
    /// Upper end of this search, after any clamping.
    /// </summary>
    public int SearchCeiling => _ceiling;

    /// <summary>
    /// <c>true</c> once nothing is left to try: either the ceiling is confirmed or every larger size
    /// has been ruled out. DPLPMTUD calls this SEARCH_COMPLETE; §14.3.2 notes that QUIC, being an
    /// acknowledged packetization layer, needs no confirmation timer afterwards.
    /// </summary>
    public bool SearchComplete => _started && _probeSize == 0 && _confirmed >= _upperBound;

    /// <summary>
    /// Size of the probe currently in flight, or 0 when none is.
    /// </summary>
    public int OutstandingProbeSize => _probeOutstanding ? _probeSize : 0;

    /// <summary>
    /// Number of probes sent so far (diagnostics/test).
    /// </summary>
    public int ProbesSent { get; private set; }

    /// <summary>
    /// Enters the search. §14.3.1: a QUIC sender may enter the DPLPMTUD BASE state "when the QUIC
    /// connection handshake has been completed" — before that the path is not even validated.
    /// <paramref name="peerMaxUdpPayloadSize"/> caps the search: §14 makes the peer's
    /// max_udp_payload_size "an additional limit on the maximum datagram size", and a datagram the
    /// peer refuses to receive is not worth discovering.
    /// </summary>
    public void Start(ulong peerMaxUdpPayloadSize)
    {
        if (_started)
            return;
        _started = true;

        int peerLimit = peerMaxUdpPayloadSize > int.MaxValue ? int.MaxValue : (int)peerMaxUdpPayloadSize;
        _upperBound = Math.Max(BasePlpmtu, Math.Min(_ceiling, peerLimit));
    }

    /// <summary>
    /// The size to probe next, or 0 when nothing should be probed right now — either the search has
    /// not started, is complete, or a probe is still outstanding.
    /// </summary>
    public int NextProbeSize()
    {
        if (!_started || _probeOutstanding || _confirmed >= _upperBound)
            return 0;

        // A size that was tried and lost, but not yet given up on, is retried at the SAME size —
        // without this the search would stall after the first ordinary loss and never resume.
        if (_probeSize != 0)
            return _probeSize;

        // Binary search between what is proven and what is not yet ruled out. Each answer halves the
        // remaining range, so an ordinary path is settled in a handful of probes.
        _probeSize  = _confirmed + ((_upperBound - _confirmed + 1) / 2);
        _probesSent = 0;
        return _probeSize;
    }

    /// <summary>
    /// Records that a probe of the current size went out.
    /// </summary>
    public void OnProbeSent()
    {
        _probeOutstanding = true;
        _probesSent++;
        ProbesSent++;
    }

    /// <summary>
    /// A probe was acknowledged: the path carries this size, so it becomes the new floor and the
    /// send path may use it immediately.
    /// </summary>
    public void OnProbeAcknowledged(int size)
    {
        if (size <= _confirmed)
            return;
        _confirmed = size;
        if (_probeSize == size)
        {
            _probeSize        = 0;
            _probeOutstanding = false;
        }
    }

    /// <summary>
    /// A probe was declared lost. Only after <see cref="MaxProbesPerSize"/> attempts does that count
    /// as "the path will not carry this size" — before that it is far more likely to have been
    /// ordinary loss, and giving up on the first one would leave most of the gain undiscovered.
    /// </summary>
    public void OnProbeLost(int size)
    {
        if (size != _probeSize)
            return; // a stale probe from a size we have already moved past

        _probeOutstanding = false;
        if (_probesSent < MaxProbesPerSize)
            return; // try this size again

        _upperBound = size - 1;
        _probeSize  = 0;
    }

}
