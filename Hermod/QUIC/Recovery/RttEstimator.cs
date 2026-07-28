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
/// RTT estimation per RFC 9002 §5: min_rtt, smoothed_rtt and rttvar from RTT samples, plus the
/// computation of the probe timeout (PTO, §6.2.1). Values as <see cref="TimeSpan"/>.
/// </summary>
public sealed class RttEstimator
{
    /// <summary>
    /// Timer granularity (RFC 9002: kGranularity = 1 ms).
    /// </summary>
    public static readonly TimeSpan Granularity = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// Initial RTT before the first sample (RFC 9002: kInitialRtt = 333 ms).
    /// </summary>
    public static readonly TimeSpan InitialRtt = TimeSpan.FromMilliseconds(333);

    private bool _hasSample;

    public TimeSpan LatestRtt { get; private set; }
    public TimeSpan MinRtt { get; private set; }
    public TimeSpan SmoothedRtt { get; private set; } = InitialRtt;
    public TimeSpan RttVar { get; private set; } = InitialRtt / 2;

    /// <summary>
    /// Processes an RTT sample (RFC 9002 §5.3). <paramref name="ackDelay"/> is the ACK delay reported
    /// by the peer, capped by <paramref name="maxAckDelay"/> (after the handshake).
    /// </summary>
    public void AddSample(TimeSpan rttSample, TimeSpan ackDelay, TimeSpan maxAckDelay)
    {
        LatestRtt = rttSample;

        if (!_hasSample)
        {
            MinRtt = rttSample;
            SmoothedRtt = rttSample;
            RttVar = rttSample / 2;
            _hasSample = true;
            return;
        }

        MinRtt = rttSample < MinRtt ? rttSample : MinRtt;

        // Cap ack_delay and only subtract it when the sample does not fall below min_rtt as a result.
        TimeSpan adjustedAckDelay = ackDelay < maxAckDelay ? ackDelay : maxAckDelay;
        TimeSpan adjustedRtt = rttSample;
        if (rttSample >= MinRtt + adjustedAckDelay)
            adjustedRtt = rttSample - adjustedAckDelay;

        TimeSpan diff = SmoothedRtt > adjustedRtt ? SmoothedRtt - adjustedRtt : adjustedRtt - SmoothedRtt;
        RttVar = (3 * RttVar + diff) / 4;                  // 3/4 rttvar + 1/4 |smoothed - adjusted|
        SmoothedRtt = (7 * SmoothedRtt + adjustedRtt) / 8; // 7/8 smoothed + 1/8 adjusted
    }

    /// <summary>
    /// Probe timeout (RFC 9002 §6.2.1): smoothed_rtt + max(4·rttvar, kGranularity) + max_ack_delay.
    /// </summary>
    public TimeSpan GetProbeTimeout(TimeSpan maxAckDelay)
    {
        if (!_hasSample)
            return 2 * InitialRtt; // before the first sample

        TimeSpan variation = 4 * RttVar;
        if (variation < Granularity)
            variation = Granularity;
        return SmoothedRtt + variation + maxAckDelay;
    }

    /// <summary>
    /// Delay after which a packet counts as lost via the time threshold (RFC 9002 §6.1.2).
    /// </summary>
    public TimeSpan LossDelay()
    {
        // kTimeThreshold = 9/8 · max(latest_rtt, smoothed_rtt), at least kGranularity.
        TimeSpan baseRtt = LatestRtt > SmoothedRtt ? LatestRtt : SmoothedRtt;
        TimeSpan delay = 9 * baseRtt / 8;
        return delay > Granularity ? delay : Granularity;
    }
}
