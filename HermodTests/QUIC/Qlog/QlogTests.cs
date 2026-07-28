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

using System.Text.Json;

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Qlog;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Qlog;

/// <summary>
/// qlog (<see cref="QlogWriter"/>) — the structured connection log that qvis turns into
/// packet/congestion diagrams. Verified: the JSON-SEQ container, the event and field names of
/// draft-ietf-quic-qlog-quic-events-13, and that a real connection produces a complete, parseable
/// trace.
/// </summary>
[TestFixture]
public class QlogTests
{
    private static (QlogWriter Writer, List<string> Records) Writer(bool isServer = false)
    {
        var records = new List<string>();
        return (new QlogWriter(records.Add, isServer, connectionId: "abcdef"), records);
    }

    private static JsonElement Parse(string record) => JsonDocument.Parse(record).RootElement;

    [Test]
    public void Header_IsAQlogFileSeq_WithTraceAndEventSchema()
    {
        (QlogWriter _, List<string> records) = Writer();

        Assert.That(records, Has.Count.EqualTo(1), "The header is written on construction.");
        JsonElement header = Parse(records[0]);
        Assert.That(header.GetProperty("file_schema").GetString(), Is.EqualTo("urn:ietf:params:qlog:file:sequential"));
        Assert.That(header.GetProperty("serialization_format").GetString(), Is.EqualTo("application/qlog+json-seq"));

        JsonElement trace = header.GetProperty("trace");
        Assert.That(trace.GetProperty("vantage_point").GetProperty("type").GetString(), Is.EqualTo("client"));
        Assert.That(trace.GetProperty("event_schemas")[0].GetString(), Is.EqualTo("urn:ietf:params:qlog:events:quic-13"),
                    "Drafts require the draft number in the event schema URI.");
        Assert.That(trace.GetProperty("common_fields").GetProperty("time_format").GetString(),
                    Is.EqualTo("relative_to_epoch"));
        Assert.That(trace.GetProperty("common_fields").GetProperty("group_id").GetString(), Is.EqualTo("abcdef"));
    }

    [Test]
    public void ServerVantagePoint_IsLoggedAsServer()
    {
        (QlogWriter _, List<string> records) = Writer(isServer: true);
        Assert.That(Parse(records[0]).GetProperty("trace").GetProperty("vantage_point").GetProperty("type").GetString(),
                    Is.EqualTo("server"));
    }

    [Test]
    public void PacketSent_HasTimeNameAndData()
    {
        (QlogWriter writer, List<string> records) = Writer();

        writer.PacketSent(12.5, "1RTT", packetNumber: 7, datagramLength: 1200,
                          [PingFrame.Instance], trigger: "pto_probe");

        JsonElement e = Parse(records[1]);
        Assert.That(e.GetProperty("time").GetDouble(), Is.EqualTo(12.5));
        Assert.That(e.GetProperty("name").GetString(), Is.EqualTo("quic:packet_sent"));

        JsonElement data = e.GetProperty("data");
        Assert.That(data.GetProperty("header").GetProperty("packet_type").GetString(), Is.EqualTo("1RTT"));
        Assert.That(data.GetProperty("header").GetProperty("packet_number").GetUInt64(), Is.EqualTo(7UL));
        Assert.That(data.GetProperty("raw").GetProperty("length").GetInt32(), Is.EqualTo(1200));
        Assert.That(data.GetProperty("trigger").GetString(), Is.EqualTo("pto_probe"));
        Assert.That(data.GetProperty("frames")[0].GetProperty("frame_type").GetString(), Is.EqualTo("ping"));
    }

    [Test]
    public void AckFrame_UsesTheAckedRangesNotation()
    {
        (QlogWriter writer, List<string> records) = Writer();

        // Draft: a range is [from, to] — but a single packet SHOULD be logged as [n].
        writer.PacketSent(1, "1RTT", 0, 100, [AckFrame.FromAscendingPacketNumbers([0, 1, 2, 5])]);

        JsonElement frame = Parse(records[1]).GetProperty("data").GetProperty("frames")[0];
        Assert.That(frame.GetProperty("frame_type").GetString(), Is.EqualTo("ack"));
        JsonElement ranges = frame.GetProperty("acked_ranges");
        Assert.That(ranges.GetArrayLength(), Is.EqualTo(2));
        Assert.That(ranges[0].GetArrayLength(), Is.EqualTo(1), "Single packet 5 ⇒ [5].");
        Assert.That(ranges[0][0].GetUInt64(), Is.EqualTo(5UL));
        Assert.That(ranges[1].GetArrayLength(), Is.EqualTo(2), "0..2 ⇒ [0,2].");
        Assert.That(ranges[1][0].GetUInt64(), Is.EqualTo(0UL));
        Assert.That(ranges[1][1].GetUInt64(), Is.EqualTo(2UL));
    }

    [Test]
    public void Frames_AreMappedToTheDraftFrameTypes()
    {
        (QlogWriter writer, List<string> records) = Writer();

        writer.PacketSent(1, "1RTT", 0, 100,
        [
            new StreamFrame(4, 100, new byte[7], Fin: true),
            new CryptoFrame(0, new byte[9]),
            new ResetStreamFrame(8, 0x10c, 42),
            new StopSendingFrame(8, 0x10c),
            new MaxDataFrame(65536),
            HandshakeDoneFrame.Instance,
            new PaddingFrame(23),
        ]);

        JsonElement frames = Parse(records[1]).GetProperty("data").GetProperty("frames");
        Assert.That(frames[0].GetProperty("frame_type").GetString(), Is.EqualTo("stream"));
        Assert.That(frames[0].GetProperty("stream_id").GetUInt64(), Is.EqualTo(4UL));
        Assert.That(frames[0].GetProperty("offset").GetUInt64(), Is.EqualTo(100UL));
        Assert.That(frames[0].GetProperty("fin").GetBoolean(), Is.True);
        Assert.That(frames[0].GetProperty("raw").GetProperty("length").GetInt32(), Is.EqualTo(7));

        Assert.That(frames[1].GetProperty("frame_type").GetString(), Is.EqualTo("crypto"));
        Assert.That(frames[2].GetProperty("frame_type").GetString(), Is.EqualTo("reset_stream"));
        Assert.That(frames[2].GetProperty("final_size").GetUInt64(), Is.EqualTo(42UL));
        Assert.That(frames[3].GetProperty("frame_type").GetString(), Is.EqualTo("stop_sending"));
        Assert.That(frames[4].GetProperty("frame_type").GetString(), Is.EqualTo("max_data"));
        Assert.That(frames[5].GetProperty("frame_type").GetString(), Is.EqualTo("handshake_done"));
        Assert.That(frames[6].GetProperty("frame_type").GetString(), Is.EqualTo("padding"));
        Assert.That(frames[6].GetProperty("raw").GetProperty("payload_length").GetInt32(), Is.EqualTo(23),
                    "PADDING is logged as ONE frame with the byte count (draft SHOULD).");
    }

    [Test]
    public void RecoveryMetrics_CarryTheFieldsQvisPlots()
    {
        (QlogWriter writer, List<string> records) = Writer();

        writer.RecoveryMetricsUpdated(5, smoothedRttMs: 33.3, latestRttMs: 30, minRttMs: 29,
                                      rttVarianceMs: 2.5, ptoCount: 1, congestionWindow: 14720, bytesInFlight: 1200);

        JsonElement data = Parse(records[1]).GetProperty("data");
        Assert.That(Parse(records[1]).GetProperty("name").GetString(), Is.EqualTo("quic:recovery_metrics_updated"));
        Assert.That(data.GetProperty("smoothed_rtt").GetDouble(), Is.EqualTo(33.3));
        Assert.That(data.GetProperty("congestion_window").GetInt64(), Is.EqualTo(14720));
        Assert.That(data.GetProperty("bytes_in_flight").GetInt64(), Is.EqualTo(1200));
        Assert.That(data.GetProperty("pto_count").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public void FileFormat_IsJsonSeq_RecordSeparatorAndNewlinePerRecord()
    {
        string path = Path.Combine(Path.GetTempPath(), $"qlog-test-{Guid.NewGuid():N}.sqlog");
        try
        {
            QlogWriter writer = QlogWriter.ToFile(path, isServer: false);
            writer.PacketSent(1, "1RTT", 0, 100, []);

            string content = File.ReadAllText(path);
            Assert.That(content[0], Is.EqualTo(''), "RFC 7464: every record starts with 0x1E.");
            string[] records = content.Split('', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(records, Has.Length.EqualTo(2), "Header + one event.");
            foreach (string record in records)
                Assert.DoesNotThrow(() => JsonDocument.Parse(record), "Every record is standalone JSON.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- Over a real connection ---------------------------------------------------------------

    [Test]
    public void RealConnection_ProducesACompleteParseableTrace()
    {
        var clientRecords = new List<string>();
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation,
                                                    qlog: new QlogWriter(clientRecords.Add, isServer: false));
        using var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }
        Assert.That(client.HandshakeConfirmed, Is.True);

        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write([1, 2, 3, 4]);
        for (int round = 0; round < 5; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }

        // Every record is valid JSON and every event carries time/name/data.
        string[] eventNames = [.. clientRecords.Skip(1).Select(r => Parse(r).GetProperty("name").GetString()!)];
        foreach (string record in clientRecords)
            Assert.DoesNotThrow(() => JsonDocument.Parse(record));

        Assert.That(eventNames, Has.Some.EqualTo("quic:packet_sent"));
        Assert.That(eventNames, Has.Some.EqualTo("quic:packet_received"));
        Assert.That(eventNames, Has.Some.EqualTo("quic:recovery_metrics_updated"));
        Assert.That(eventNames, Has.Some.EqualTo("quic:key_discarded"), "Initial/Handshake keys are discarded.");

        // The times are monotonically non-decreasing — otherwise qvis draws the timeline wrongly.
        double[] times = [.. clientRecords.Skip(1).Select(r => Parse(r).GetProperty("time").GetDouble())];
        Assert.That(times, Is.Ordered, "Event times must not go backwards.");

        // The handshake must show up as CRYPTO frames in the first packet.
        JsonElement firstSent = Parse(clientRecords.Skip(1).First(r => Parse(r).GetProperty("name").GetString() == "quic:packet_sent"));
        Assert.That(firstSent.GetProperty("data").GetProperty("header").GetProperty("packet_type").GetString(),
                    Is.EqualTo("initial"));
        Assert.That(firstSent.GetProperty("data").GetProperty("frames").EnumerateArray()
                        .Any(f => f.GetProperty("frame_type").GetString() == "crypto"), Is.True);
    }

    [Test]
    public void WithoutAQlog_NothingHappens()
    {
        // The default must stay silent and must not change behaviour.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }

        Assert.That(client.HandshakeConfirmed, Is.True);
    }
}
