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

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Diagnostics;

/// <summary>
/// Structured events of a running QUIC connection, for anyone attaching an
/// <see cref="EventListener"/>, <c>dotnet-trace</c> or an ETW/EventPipe session.
/// <para>
/// This is deliberately NOT qlog. qlog answers "what exactly happened on this one connection" and
/// writes a file per connection; these events answer "what is this process doing right now" and cost
/// nothing when nobody is listening. Every method checks <see cref="EventSource.IsEnabled()"/>, so an
/// unobserved connection neither formats a string nor allocates.
/// </para>
/// </summary>
[EventSource(Name = "Vanaheimr-Hermod-Quic")]
public sealed class QuicEventSource : EventSource
{

    /// <summary>
    /// The single instance — EventSource is a process-wide facility, not a per-connection object.
    /// </summary>
    public static readonly QuicEventSource Log = new();

    private QuicEventSource() { }

    /// <summary>
    /// A connection object was created: from here on it has state a peer can address.
    /// </summary>
    /// <param name="Role">"client" or "server".</param>
    /// <param name="SourceConnectionId">Our own connection ID, hex.</param>
    [Event(1, Level = EventLevel.Informational, Message = "QUIC {0} connection started (scid {1})")]
    public void ConnectionStarted(string Role, string SourceConnectionId)
    {
        if (IsEnabled())
            WriteEvent(1, Role, SourceConnectionId);
    }

    /// <summary>
    /// The 1-RTT keys are installed — the handshake is through and application data can flow.
    /// </summary>
    /// <param name="Role">"client" or "server".</param>
    /// <param name="SourceConnectionId">Our own connection ID, hex.</param>
    /// <param name="DurationMs">Time from construction to this point.</param>
    [Event(2, Level = EventLevel.Informational, Message = "QUIC {0} handshake completed after {2} ms (scid {1})")]
    public void HandshakeCompleted(string Role, string SourceConnectionId, double DurationMs)
    {
        if (IsEnabled())
            WriteEvent(2, Role, SourceConnectionId, DurationMs);
    }

    /// <summary>
    /// The connection is closing, either from our side or because the peer sent CONNECTION_CLOSE.
    /// </summary>
    /// <param name="Role">"client" or "server".</param>
    /// <param name="SourceConnectionId">Our own connection ID, hex.</param>
    /// <param name="Origin">"local" or "remote" — who ended it.</param>
    /// <param name="ErrorCode">The transport or application error code.</param>
    /// <param name="Reason">The reason phrase, possibly empty.</param>
    [Event(3, Level = EventLevel.Informational, Message = "QUIC {0} connection closed by {2}: 0x{3:X} {4} (scid {1})")]
    public void ConnectionClosed(string Role, string SourceConnectionId, string Origin, long ErrorCode, string Reason)
    {
        if (IsEnabled())
            WriteEvent(3, Role, SourceConnectionId, Origin, ErrorCode, Reason);
    }

    /// <summary>
    /// A sent packet was declared lost (RFC 9002 §6). Verbose: on a bad path this fires often.
    /// </summary>
    /// <param name="Role">"client" or "server".</param>
    /// <param name="Space">Packet-number space: 0 Initial, 1 Handshake, 2 Application.</param>
    /// <param name="PacketNumber">The lost packet number.</param>
    /// <param name="Trigger">What declared it lost — reordering threshold or time threshold.</param>
    [Event(4, Level = EventLevel.Verbose, Message = "QUIC {0} lost packet {2} in space {1} ({3})")]
    public void PacketLost(string Role, int Space, long PacketNumber, string Trigger)
    {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.None))
            WriteEvent(4, Role, Space, PacketNumber, Trigger);
    }

}

/// <summary>
/// Metrics of the QUIC layer, published through <see cref="System.Diagnostics.Metrics"/> — readable
/// with <c>dotnet-counters</c>, an OpenTelemetry exporter or a plain <see cref="MeterListener"/>.
/// <para>
/// Everything on a hot path is guarded by <see cref="Instrument.Enabled"/>, which is false while no
/// listener has subscribed to that instrument. Counting therefore costs a field read when nobody is
/// watching.
/// </para>
/// </summary>
public static class QuicMetrics
{

    /// <summary>
    /// The meter name to subscribe to.
    /// </summary>
    public const String MeterName = "Vanaheimr.Hermod.Quic";

    private static readonly Meter meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Connections currently alive — raised on construction, lowered on disposal.
    /// </summary>
    public static readonly UpDownCounter<Int64> ActiveConnections =
        meter.CreateUpDownCounter<Int64>("quic.connections.active", "{connection}",
                                         "QUIC connections currently alive.");

    /// <summary>
    /// Completed handshakes, tagged with the role.
    /// </summary>
    public static readonly Counter<Int64> Handshakes =
        meter.CreateCounter<Int64>("quic.handshakes", "{handshake}",
                                   "QUIC handshakes that reached the 1-RTT keys.");

    /// <summary>
    /// How long a handshake took, from connection object to 1-RTT keys.
    /// </summary>
    public static readonly Histogram<Double> HandshakeDuration =
        meter.CreateHistogram<Double>("quic.handshake.duration", "ms",
                                      "Time from connection creation to installed 1-RTT keys.");

    /// <summary>
    /// Streams opened. Not a gauge of live streams: QUIC stream state stays for the lifetime of the
    /// connection (a peer may still reference a finished stream), so there is no close to count down.
    /// </summary>
    public static readonly Counter<Int64> StreamsOpened =
        meter.CreateCounter<Int64>("quic.streams.opened", "{stream}",
                                   "QUIC streams opened, locally or by the peer.");

    /// <summary>
    /// UDP payload bytes handed to us, before any decryption.
    /// </summary>
    public static readonly Counter<Int64> BytesReceived =
        meter.CreateCounter<Int64>("quic.bytes.received", "By",
                                   "UDP payload bytes received.");

    /// <summary>
    /// UDP payload bytes we produced for sending.
    /// </summary>
    public static readonly Counter<Int64> BytesSent =
        meter.CreateCounter<Int64>("quic.bytes.sent", "By",
                                   "UDP payload bytes produced for sending.");

    /// <summary>
    /// Protected packets sent, tagged with the packet-number space.
    /// </summary>
    public static readonly Counter<Int64> PacketsSent =
        meter.CreateCounter<Int64>("quic.packets.sent", "{packet}",
                                   "Protected QUIC packets sent.");

    /// <summary>
    /// Packets declared lost by loss detection (RFC 9002 §6).
    /// </summary>
    public static readonly Counter<Int64> PacketsLost =
        meter.CreateCounter<Int64>("quic.packets.lost", "{packet}",
                                   "Packets declared lost by loss detection.");

    /// <summary>
    /// Frames queued again after a loss or a probe timeout — the retransmission volume.
    /// </summary>
    public static readonly Counter<Int64> FramesRetransmitted =
        meter.CreateCounter<Int64>("quic.frames.retransmitted", "{frame}",
                                   "Frames re-queued after loss or probe timeout.");

    /// <summary>
    /// Smoothed RTT at each acknowledgment (RFC 9002 §5).
    /// </summary>
    public static readonly Histogram<Double> SmoothedRtt =
        meter.CreateHistogram<Double>("quic.rtt.smoothed", "ms",
                                      "Smoothed round-trip time.");

    /// <summary>
    /// Congestion window at each acknowledgment (RFC 9002 §7).
    /// </summary>
    public static readonly Histogram<Int64> CongestionWindow =
        meter.CreateHistogram<Int64>("quic.congestion_window", "By",
                                     "Congestion window in bytes.");

    /// <summary>
    /// Pre-built role tags, so the hot paths do not allocate one per measurement.
    /// </summary>
    internal static KeyValuePair<String, Object?> RoleTag(Boolean IsServer)
        => IsServer ? serverTag : clientTag;

    private static readonly KeyValuePair<String, Object?> clientTag = new("role", "client");
    private static readonly KeyValuePair<String, Object?> serverTag = new("role", "server");

}
