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

using System.Buffers;
using System.Text.Json;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Qlog;

/// <summary>
/// Structured connection log in the qlog format — the event stream that qvis
/// (<see href="https://qvis.quictools.info"/>) turns into packet/congestion diagrams.
/// <para>
/// Serialised as JSON Text Sequences (<c>application/qlog+json-seq</c>, RFC 7464): one record per
/// line, each introduced by a record separator (0x1E). That makes the log streamable and keeps a
/// recording usable even if the process is killed mid-connection — unlike the array-based JSON
/// variant, which would need a closing bracket.
/// </para>
/// <para>
/// One writer = one trace = ONE connection (qlog "Trace" semantics). Events carry the time in
/// milliseconds relative to the start of the connection (<c>relative_to_epoch</c> on a monotonic
/// clock), i.e. exactly the endpoint's own clock.
/// </para>
/// <para>
/// Field and event names follow draft-ietf-quic-qlog-main-schema-14 and
/// draft-ietf-quic-qlog-quic-events-13. Since these are still Internet-Drafts, the event schema URI
/// carries the draft number as the drafts require (<see cref="EventSchemaUri"/>).
/// </para>
/// </summary>
public sealed class QlogWriter
{
    /// <summary>
    /// Event schema of the QUIC events. As long as no RFC exists, implementations MUST append the
    /// draft number (draft-ietf-quic-qlog-quic-events §"Draft Event Schema Identification").
    /// </summary>
    public const string EventSchemaUri = "urn:ietf:params:qlog:events:quic-13";

    /// <summary>
    /// File schema of the streamable JSON-SEQ variant (main schema §"QlogFileSeq schema").
    /// </summary>
    public const string FileSchemaUri = "urn:ietf:params:qlog:file:sequential";

    private const byte RecordSeparator = 0x1E; // RFC 7464 §2.2

    private readonly Action<string> _writeRecord;
    private readonly Lock _lock = new();

    /// <summary>
    /// A qlog writer with an arbitrary sink. Each record arrives WITHOUT the record separator and
    /// without a trailing newline — <see cref="ToFile"/> adds both. Used by the tests to inspect
    /// the JSON without touching the file system.
    /// </summary>
    public QlogWriter(Action<string> recordSink, bool isServer, string? connectionId = null, string? title = null)
    {
        _writeRecord = recordSink;
        WriteHeader(isServer, connectionId, title);
    }

    /// <summary>
    /// Writes to a file in JSON-SEQ format (RFC 7464: record separator, JSON, newline). Convention
    /// for the file extension is <c>.sqlog</c> — qvis and other tools recognise it.
    /// </summary>
    public static QlogWriter ToFile(string path, bool isServer, string? connectionId = null, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new QlogWriter(
            record => File.AppendAllText(path, (char)RecordSeparator + record + "\n"),
            isServer, connectionId, title);
    }

    private void WriteHeader(bool isServer, string? connectionId, string? title)
        => WriteRecord(writer =>
        {
            writer.WriteString("file_schema", FileSchemaUri);
            writer.WriteString("serialization_format", "application/qlog+json-seq");
            writer.WriteString("title", title ?? "HTTP/3 from Scratch");

            writer.WriteStartObject("trace");
            {
                writer.WriteStartObject("vantage_point");
                writer.WriteString("name", "HTTP3FromScratch");
                writer.WriteString("type", isServer ? "server" : "client");
                writer.WriteEndObject();

                writer.WriteStartObject("common_fields");
                writer.WriteString("time_format", "relative_to_epoch");
                writer.WriteStartObject("reference_time");
                writer.WriteString("clock_type", "monotonic");
                writer.WriteString("epoch", "unknown"); // monotonic ⇒ no wall-clock epoch
                writer.WriteEndObject();
                if (connectionId is not null)
                    writer.WriteString("group_id", connectionId);
                writer.WriteEndObject();

                writer.WriteStartArray("event_schemas");
                writer.WriteStringValue(EventSchemaUri);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        });

    // ---- Events ------------------------------------------------------------------------------

    /// <summary>
    /// <c>quic:packet_sent</c>. <paramref name="trigger"/> per the draft, e.g. "pto_probe".
    /// </summary>
    public void PacketSent(double timeMs, string packetType, ulong packetNumber, int datagramLength,
                           IReadOnlyList<Frame> frames, string? trigger = null)
        => WriteEvent(timeMs, "quic:packet_sent", writer =>
        {
            WritePacketHeader(writer, packetType, packetNumber);
            WriteFrames(writer, frames);
            WriteRaw(writer, datagramLength);
            if (trigger is not null)
                writer.WriteString("trigger", trigger);
        });

    /// <summary>
    /// <c>quic:packet_received</c>.
    /// </summary>
    public void PacketReceived(double timeMs, string packetType, ulong packetNumber, int datagramLength,
                               IReadOnlyList<Frame> frames)
        => WriteEvent(timeMs, "quic:packet_received", writer =>
        {
            WritePacketHeader(writer, packetType, packetNumber);
            WriteFrames(writer, frames);
            WriteRaw(writer, datagramLength);
        });

    /// <summary>
    /// <c>quic:packet_dropped</c> — e.g. a packet that could not be unprotected.
    /// </summary>
    public void PacketDropped(double timeMs, string packetType, int datagramLength, string trigger)
        => WriteEvent(timeMs, "quic:packet_dropped", writer =>
        {
            writer.WriteStartObject("header");
            writer.WriteString("packet_type", packetType);
            writer.WriteEndObject();
            WriteRaw(writer, datagramLength);
            writer.WriteString("trigger", trigger);
        });

    /// <summary>
    /// <c>quic:packet_lost</c> — loss detection declared the packet lost (RFC 9002 §6).
    /// </summary>
    public void PacketLost(double timeMs, string packetType, ulong packetNumber, string trigger)
        => WriteEvent(timeMs, "quic:packet_lost", writer =>
        {
            WritePacketHeader(writer, packetType, packetNumber);
            writer.WriteString("trigger", trigger);
        });

    /// <summary>
    /// <c>quic:recovery_metrics_updated</c> — the values qvis plots as the congestion diagram.
    /// RTTs in milliseconds, windows in bytes (RFC 9002 App. A.3/B.2).
    /// </summary>
    public void RecoveryMetricsUpdated(double timeMs, double smoothedRttMs, double latestRttMs, double minRttMs,
                                       double rttVarianceMs, int ptoCount, long congestionWindow, long bytesInFlight)
        => WriteEvent(timeMs, "quic:recovery_metrics_updated", writer =>
        {
            writer.WriteNumber("smoothed_rtt", smoothedRttMs);
            writer.WriteNumber("latest_rtt", latestRttMs);
            writer.WriteNumber("min_rtt", minRttMs);
            writer.WriteNumber("rtt_variance", rttVarianceMs);
            writer.WriteNumber("pto_count", ptoCount);
            writer.WriteNumber("congestion_window", congestionWindow);
            writer.WriteNumber("bytes_in_flight", bytesInFlight);
        });

    /// <summary>
    /// <c>quic:key_updated</c>. <paramref name="keyType"/> per the draft's KeyType, e.g.
    /// "client_1rtt_secret"; <paramref name="trigger"/> "tls", "local_update" or "remote_update".
    /// </summary>
    public void KeyUpdated(double timeMs, string keyType, string trigger, ulong? keyPhase = null)
        => WriteEvent(timeMs, "quic:key_updated", writer =>
        {
            writer.WriteString("key_type", keyType);
            if (keyPhase is { } phase)
                writer.WriteNumber("key_phase", phase);
            writer.WriteString("trigger", trigger);
        });

    /// <summary>
    /// <c>quic:key_discarded</c> (RFC 9001 §4.9).
    /// </summary>
    public void KeyDiscarded(double timeMs, string keyType)
        => WriteEvent(timeMs, "quic:key_discarded", writer => writer.WriteString("key_type", keyType));

    /// <summary>
    /// <c>quic:connection_closed</c>. <paramref name="owner"/> is "local" or "remote".
    /// </summary>
    public void ConnectionClosed(double timeMs, string owner, ulong? errorCode, string? reason)
        => WriteEvent(timeMs, "quic:connection_closed", writer =>
        {
            writer.WriteString("owner", owner);
            if (errorCode is { } code)
                writer.WriteNumber("error_code", code);
            if (!string.IsNullOrEmpty(reason))
                writer.WriteString("reason", reason);
        });

    // ---- Serialisation -----------------------------------------------------------------------

    private static void WritePacketHeader(Utf8JsonWriter writer, string packetType, ulong packetNumber)
    {
        writer.WriteStartObject("header");
        writer.WriteString("packet_type", packetType);
        writer.WriteNumber("packet_number", packetNumber);
        writer.WriteEndObject();
    }

    private static void WriteRaw(Utf8JsonWriter writer, int length)
    {
        writer.WriteStartObject("raw");
        writer.WriteNumber("length", length);
        writer.WriteEndObject();
    }

    private static void WriteFrames(Utf8JsonWriter writer, IReadOnlyList<Frame> frames)
    {
        writer.WriteStartArray("frames");
        foreach (Frame frame in frames)
        {
            writer.WriteStartObject();
            WriteFrame(writer, frame);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    /// <summary>
    /// Maps our frame records to the qlog frame definitions (quic-events §"QUIC Frames").
    /// </summary>
    private static void WriteFrame(Utf8JsonWriter writer, Frame frame)
    {
        switch (frame)
        {
            case PaddingFrame padding:
                writer.WriteString("frame_type", "padding");
                // The draft asks for the number of PADDING bytes in raw.payload_length instead of
                // one frame per byte.
                writer.WriteStartObject("raw");
                writer.WriteNumber("payload_length", padding.Length);
                writer.WriteEndObject();
                break;

            case PingFrame:
                writer.WriteString("frame_type", "ping");
                break;

            case AckFrame ack:
                writer.WriteString("frame_type", "ack");
                writer.WriteNumber("ack_delay", ack.AckDelay);
                writer.WriteStartArray("acked_ranges");
                foreach (PacketNumberRange range in ack.Ranges)
                {
                    writer.WriteStartArray();
                    writer.WriteNumberValue(range.Smallest);
                    if (range.Largest != range.Smallest)
                        writer.WriteNumberValue(range.Largest); // single packet ⇒ one number (draft SHOULD)
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
                if (ack.Ecn is { } ecn)
                {
                    writer.WriteNumber("ect0", ecn.Ect0);
                    writer.WriteNumber("ect1", ecn.Ect1);
                    writer.WriteNumber("ce", ecn.CongestionExperienced);
                }
                break;

            case CryptoFrame crypto:
                writer.WriteString("frame_type", "crypto");
                writer.WriteNumber("offset", crypto.Offset);
                writer.WriteStartObject("raw");
                writer.WriteNumber("length", crypto.Data.Length);
                writer.WriteEndObject();
                break;

            case StreamFrame stream:
                writer.WriteString("frame_type", "stream");
                writer.WriteNumber("stream_id", stream.StreamId);
                writer.WriteNumber("offset", stream.Offset);
                writer.WriteBoolean("fin", stream.Fin);
                writer.WriteStartObject("raw");
                writer.WriteNumber("length", stream.Data.Length);
                writer.WriteEndObject();
                break;

            case ResetStreamFrame reset:
                writer.WriteString("frame_type", "reset_stream");
                writer.WriteNumber("stream_id", reset.StreamId);
                writer.WriteString("error", "unknown"); // application error codes are opaque to us
                writer.WriteNumber("error_code", reset.ApplicationErrorCode);
                writer.WriteNumber("final_size", reset.FinalSize);
                break;

            case ResetStreamAtFrame resetAt:
                writer.WriteString("frame_type", "reset_stream_at");
                writer.WriteNumber("stream_id", resetAt.StreamId);
                writer.WriteString("error", "unknown");
                writer.WriteNumber("error_code", resetAt.ApplicationErrorCode);
                writer.WriteNumber("final_size", resetAt.FinalSize);
                writer.WriteNumber("reliable_size", resetAt.ReliableSize);
                break;

            case StopSendingFrame stop:
                writer.WriteString("frame_type", "stop_sending");
                writer.WriteNumber("stream_id", stop.StreamId);
                writer.WriteString("error", "unknown");
                writer.WriteNumber("error_code", stop.ApplicationErrorCode);
                break;

            case MaxDataFrame maxData:
                writer.WriteString("frame_type", "max_data");
                writer.WriteNumber("maximum", maxData.MaximumData);
                break;

            case MaxStreamDataFrame maxStreamData:
                writer.WriteString("frame_type", "max_stream_data");
                writer.WriteNumber("stream_id", maxStreamData.StreamId);
                writer.WriteNumber("maximum", maxStreamData.MaximumStreamData);
                break;

            case MaxStreamsFrame maxStreams:
                writer.WriteString("frame_type", "max_streams");
                writer.WriteString("stream_type", maxStreams.Bidirectional ? "bidirectional" : "unidirectional");
                writer.WriteNumber("maximum", maxStreams.MaximumStreams);
                break;

            case DataBlockedFrame dataBlocked:
                writer.WriteString("frame_type", "data_blocked");
                writer.WriteNumber("limit", dataBlocked.MaximumData);
                break;

            case StreamDataBlockedFrame streamDataBlocked:
                writer.WriteString("frame_type", "stream_data_blocked");
                writer.WriteNumber("stream_id", streamDataBlocked.StreamId);
                writer.WriteNumber("limit", streamDataBlocked.MaximumStreamData);
                break;

            case StreamsBlockedFrame streamsBlocked:
                writer.WriteString("frame_type", "streams_blocked");
                writer.WriteString("stream_type", streamsBlocked.Bidirectional ? "bidirectional" : "unidirectional");
                writer.WriteNumber("limit", streamsBlocked.MaximumStreams);
                break;

            case NewConnectionIdFrame newCid:
                writer.WriteString("frame_type", "new_connection_id");
                writer.WriteNumber("sequence_number", newCid.SequenceNumber);
                writer.WriteNumber("retire_prior_to", newCid.RetirePriorTo);
                writer.WriteString("connection_id", Convert.ToHexStringLower(newCid.ConnectionId.Span));
                break;

            case RetireConnectionIdFrame retireCid:
                writer.WriteString("frame_type", "retire_connection_id");
                writer.WriteNumber("sequence_number", retireCid.SequenceNumber);
                break;

            case NewTokenFrame newToken:
                writer.WriteString("frame_type", "new_token");
                writer.WriteStartObject("token");
                writer.WriteString("raw", Convert.ToHexStringLower(newToken.Token.Span));
                writer.WriteEndObject();
                break;

            case PathChallengeFrame:
                writer.WriteString("frame_type", "path_challenge");
                break;

            case PathResponseFrame:
                writer.WriteString("frame_type", "path_response");
                break;

            case ConnectionCloseFrame close:
                writer.WriteString("frame_type", "connection_close");
                writer.WriteString("error_space", close.IsApplicationError ? "application" : "transport");
                writer.WriteString("error", "unknown");
                writer.WriteNumber("error_code", close.ErrorCode);
                if (!string.IsNullOrEmpty(close.ReasonPhrase))
                    writer.WriteString("reason", close.ReasonPhrase);
                break;

            case HandshakeDoneFrame:
                writer.WriteString("frame_type", "handshake_done");
                break;

            case DatagramFrame datagram:
                writer.WriteString("frame_type", "datagram");
                writer.WriteStartObject("raw");
                writer.WriteNumber("length", datagram.Data.Length);
                writer.WriteEndObject();
                break;

            default:
                writer.WriteString("frame_type", "unknown");
                break;
        }
    }

    private void WriteEvent(double timeMs, string name, Action<Utf8JsonWriter> writeData)
        => WriteRecord(writer =>
        {
            writer.WriteNumber("time", timeMs);
            writer.WriteString("name", name);
            writer.WriteStartObject("data");
            writeData(writer);
            writer.WriteEndObject();
        });

    private void WriteRecord(Action<Utf8JsonWriter> writeBody)
    {
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeBody(writer);
            writer.WriteEndObject();
        }

        string record = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        lock (_lock)
            _writeRecord(record);
    }
}
