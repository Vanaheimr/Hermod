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

using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M6 session recording: asciicast v2 round-trips and plays back, exec recordings carry the command and
    /// exit status, SFTP transcripts reconstruct the operation sequence, credentials/input are redacted by
    /// default, and a crash-truncated recording is still a valid prefix.
    /// </summary>
    [TestFixture]
    public class RecordingTests
    {

        #region (helper) ManualClock

        private sealed class ManualClock : TimeProvider
        {
            private DateTimeOffset now;
            public ManualClock(DateTimeOffset Start) { now = Start; }
            public override DateTimeOffset GetUtcNow() => now;
            public void Advance(TimeSpan By) => now += By;
        }

        private static ManualClock NewClock()
            => new (new DateTimeOffset(2026, 07, 24, 12, 00, 00, TimeSpan.Zero));

        private static Byte[] Utf8(String s) => Encoding.UTF8.GetBytes(s);

        #endregion


        #region Asciicast_RoundTrips_HeaderAndEvents

        [Test]
        [CancelAfter(15000)]
        public async Task Asciicast_RoundTrips_HeaderAndEvents(CancellationToken CancellationToken)
        {

            var writerText = new StringWriter();
            var writer     = new AsciicastWriter(writerText);

            await writer.WriteHeaderAsync(new AsciicastHeader {
                Width      = 120,
                Height     = 40,
                Command    = "uname -a",
                Timestamp  = new DateTimeOffset(2026, 07, 24, 12, 00, 00, TimeSpan.Zero)
            }, CancellationToken);

            await writer.WriteEventAsync(new AsciicastEvent(0.5, AsciicastEventCode.Output, "Linux hermod\n"), CancellationToken);
            await writer.WriteEventAsync(new AsciicastEvent(1.0, AsciicastEventCode.Resize, "132x50"),        CancellationToken);
            await writer.WriteEventAsync(new AsciicastEvent(1.5, AsciicastEventCode.Output, "done\r\n\t\"q\""), CancellationToken);

            var parsed = AsciicastReader.Parse(writerText.ToString());

            Assert.Multiple(() => {
                Assert.That(parsed.Header.Version,   Is.EqualTo(2));
                Assert.That(parsed.Header.Width,     Is.EqualTo(120));
                Assert.That(parsed.Header.Height,    Is.EqualTo(40));
                Assert.That(parsed.Header.Command,   Is.EqualTo("uname -a"));
                Assert.That(parsed.Events,           Has.Count.EqualTo(3));
                Assert.That(parsed.Events[0].Code,   Is.EqualTo(AsciicastEventCode.Output));
                Assert.That(parsed.Events[1].Code,   Is.EqualTo(AsciicastEventCode.Resize));
                Assert.That(parsed.Events[1].Data,   Is.EqualTo("132x50"));
                // Control characters and quotes must survive JSON escaping intact.
                Assert.That(parsed.OutputText,       Is.EqualTo("Linux hermod\ndone\r\n\t\"q\""));
            });

        }

        #endregion

        #region Asciicast_TruncatedFinalLine_IsStillValid

        [Test]
        [CancelAfter(15000)]
        public async Task Asciicast_TruncatedFinalLine_IsStillValid(CancellationToken CancellationToken)
        {

            var writerText = new StringWriter();
            var writer     = new AsciicastWriter(writerText);

            await writer.WriteHeaderAsync(new AsciicastHeader { Command = "run" }, CancellationToken);
            await writer.WriteEventAsync(new AsciicastEvent(0.1, AsciicastEventCode.Output, "first\n"), CancellationToken);
            await writer.WriteEventAsync(new AsciicastEvent(0.2, AsciicastEventCode.Output, "second\n"), CancellationToken);

            // Simulate a crash mid-write: a dangling, unterminated last line.
            var crashed = writerText.ToString() + "[0.3,\"o\",\"thir";

            var parsed = AsciicastReader.Parse(crashed);

            Assert.Multiple(() => {
                Assert.That(parsed.Header.Command, Is.EqualTo("run"));
                Assert.That(parsed.Events,         Has.Count.EqualTo(2), "the broken final line is skipped, complete events survive");
                Assert.That(parsed.OutputText,     Is.EqualTo("first\nsecond\n"));
            });

        }

        #endregion

        #region SessionRecorder_Exec_CarriesCommandAndExitStatus

        [Test]
        [CancelAfter(15000)]
        public async Task SessionRecorder_Exec_CarriesCommandAndExitStatus(CancellationToken CancellationToken)
        {

            var clock = NewClock();
            var sink  = new InMemoryRecordingSink();
            var meta  = new SessionRecordingMetadata { Username = "achim", SessionId = "abcd" };

            var handle   = await sink.BeginAsync(meta, RecordingFormat.AsciicastV2, CancellationToken);
            var recorder = new SessionRecorder(handle, meta, clock);

            await recorder.StartAsync(Command: "uname -a", CancellationToken: CancellationToken);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            await recorder.RecordOutputAsync(Utf8("Linux hermod 6.9\n"), CancellationToken);
            clock.Advance(TimeSpan.FromMilliseconds(250));
            await recorder.RecordExitAsync(0, CancellationToken);
            await recorder.DisposeAsync();

            var rec    = sink.Recordings.Single();
            var parsed = AsciicastReader.Parse(rec.Text);

            Assert.Multiple(() => {
                Assert.That(parsed.Header.Command,        Is.EqualTo("uname -a"));
                Assert.That(parsed.OutputText,            Is.EqualTo("Linux hermod 6.9\n"));
                Assert.That(parsed.ExitStatus,            Is.EqualTo(0));
                Assert.That(parsed.Events[0].ElapsedSeconds, Is.EqualTo(0.5).Within(0.0001));
                // The sidecar carries who / command / exit / end time.
                Assert.That(rec.FinalMetadata.Username,   Is.EqualTo("achim"));
                Assert.That(rec.FinalMetadata.Command,    Is.EqualTo("uname -a"));
                Assert.That(rec.FinalMetadata.ExitStatus, Is.EqualTo(0));
                Assert.That(rec.FinalMetadata.EndedAt,    Is.Not.Null);
            });

        }

        #endregion

        #region SessionRecorder_InputRedactedByDefault_OptInCaptures

        [Test]
        [CancelAfter(15000)]
        public async Task SessionRecorder_InputRedactedByDefault_OptInCaptures(CancellationToken CancellationToken)
        {

            var clock = NewClock();
            var sink  = new InMemoryRecordingSink();
            var meta  = new SessionRecordingMetadata { Username = "achim" };

            // Output-only recorder (the default): input — including anything sensitive — is never written.
            var handle   = await sink.BeginAsync(meta, RecordingFormat.AsciicastV2, CancellationToken);
            var recorder = new SessionRecorder(handle, meta, clock);   // CaptureInput = false
            await recorder.StartAsync(CancellationToken: CancellationToken);
            await recorder.RecordInputAsync(Utf8("hunter2-secret"), CancellationToken);
            await recorder.RecordOutputAsync(Utf8("welcome\n"), CancellationToken);
            await recorder.DisposeAsync();

            var defaultRaw    = sink.Recordings.First(r => r.FinalMetadata.Username == "achim").Text;
            var defaultParsed = AsciicastReader.Parse(defaultRaw);

            // Explicit opt-in recorder: input is captured.
            var sink2      = new InMemoryRecordingSink();
            var handle2    = await sink2.BeginAsync(meta, RecordingFormat.AsciicastV2, CancellationToken);
            var recorder2  = new SessionRecorder(handle2, meta, clock, CaptureInput: true);
            await recorder2.StartAsync(CancellationToken: CancellationToken);
            await recorder2.RecordInputAsync(Utf8("ls -la"), CancellationToken);
            await recorder2.DisposeAsync();

            var optInParsed = AsciicastReader.Parse(sink2.Recordings.Single().Text);

            Assert.Multiple(() => {
                Assert.That(defaultParsed.Events.Any(e => e.Code == AsciicastEventCode.Input), Is.False, "no input events in an output-only recording");
                Assert.That(defaultRaw.Contains("hunter2"), Is.False, "the secret must not appear anywhere in the recording");
                Assert.That(optInParsed.Events.Any(e => e.Code == AsciicastEventCode.Input),   Is.True);
                Assert.That(optInParsed.Events.Single(e => e.Code == AsciicastEventCode.Input).Data, Is.EqualTo("ls -la"));
            });

        }

        #endregion

        #region SftpTranscript_ReconstructsOperationSequence

        [Test]
        [CancelAfter(15000)]
        public async Task SftpTranscript_ReconstructsOperationSequence(CancellationToken CancellationToken)
        {

            var clock = NewClock();
            var sink  = new InMemoryRecordingSink();
            var meta  = new SessionRecordingMetadata { Username = "device-07", AccessProfile = "SftpDownloadOnly" };

            var handle = await sink.BeginAsync(meta, RecordingFormat.SftpTranscript, CancellationToken);
            var trx    = new SftpTranscriptRecorder(handle, meta, clock);

            await trx.RecordAsync("open",  "/firmware.bin", "Ok",              CancellationToken: CancellationToken);
            await trx.RecordAsync("read",  "/firmware.bin", "Ok", 0,    32768, CancellationToken);
            await trx.RecordAsync("read",  "/firmware.bin", "Ok", 32768, 4096, CancellationToken);
            await trx.RecordAsync("write", "/evil.bin",     "PermissionDenied", CancellationToken: CancellationToken);
            await trx.RecordAsync("close", "/firmware.bin", "Ok",              CancellationToken: CancellationToken);
            await trx.DisposeAsync();

            var entries = SftpTranscriptReader.Parse(sink.Recordings.Single().Text);

            Assert.Multiple(() => {
                Assert.That(entries.Select(e => e.Operation), Is.EqualTo(new[] { "open", "read", "read", "write", "close" }));
                Assert.That(entries[1].Length,  Is.EqualTo(32768));
                Assert.That(entries[2].Offset,  Is.EqualTo(32768));
                Assert.That(entries[3].Result,  Is.EqualTo("PermissionDenied"), "a denied upload attempt is on the record");
                Assert.That(entries.All(e => e.Path.Length > 0 || e.Operation == "close"), Is.True);
            });

        }

        #endregion

        #region Metadata_Sidecar_CarriesWhoAndWhen

        [Test]
        public void Metadata_Sidecar_CarriesWhoAndWhen()
        {

            var meta = new SessionRecordingMetadata {
                RecordingId    = "rec1",
                Username       = "achim",
                KeyFingerprint = "SHA256:abc",
                PeerEndpoint   = "203.0.113.7:52344",
                AccessProfile  = "FullSftp",
                Command        = "uname -a",
                StartedAt      = new DateTimeOffset(2026, 07, 24, 12, 00, 00, TimeSpan.Zero),
                EndedAt        = new DateTimeOffset(2026, 07, 24, 12, 00, 05, TimeSpan.Zero),
                ExitStatus     = 0,
                DisconnectReason = "normal"
            };

            var json = meta.ToJson();

            Assert.Multiple(() => {
                Assert.That(json, Does.Contain("\"username\": \"achim\""));
                Assert.That(json, Does.Contain("\"keyFingerprint\": \"SHA256:abc\""));
                Assert.That(json, Does.Contain("\"peerEndpoint\": \"203.0.113.7:52344\""));
                Assert.That(json, Does.Contain("\"exitStatus\": 0"));
                Assert.That(json, Does.Contain("\"disconnectReason\": \"normal\""));
            });

        }

        #endregion

    }

}
