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
using System.Text.Json;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Rotation of session recordings: a long session split into parts by size or by time, each part a
    /// valid recording of its own that names its place in the series, and a recording directory kept within
    /// an age, a total size and a count, without ever touching a recording still open or a file that is
    /// not a recording.
    /// </summary>
    [TestFixture]
    public class RecordingRotationTests
    {

        #region (helpers)

        private sealed class ManualClock : TimeProvider
        {
            private DateTimeOffset now;
            public ManualClock(DateTimeOffset Start) { now = Start; }
            public override DateTimeOffset GetUtcNow() => now;
            public void Advance(TimeSpan By) => now += By;
        }

        private static readonly DateTimeOffset start = new (2026, 11, 27, 12, 00, 00, TimeSpan.Zero);

        private static ManualClock NewClock()
            => new (start);

        private static Byte[] Utf8(String s) => Encoding.UTF8.GetBytes(s);

        private static InMemoryRecordingSink.InMemoryRecording[] Parts(InMemoryRecordingSink Sink)
            => Sink.Recordings.OrderBy(recording => recording.FinalMetadata.Part).ToArray();

        private String directory = "";

        [SetUp]
        public void CreateDirectory()
        {
            directory = Path.Combine(Path.GetTempPath(), "hermod-recording-rotation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void RemoveDirectory()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        /// <summary>
        /// Leave a finished recording in the directory, last written to at <paramref name="WrittenAt"/>.
        /// </summary>
        private void Store(String Id, DateTimeOffset WrittenAt, Int32 BodyBytes = 100)
        {

            var body    = Path.Combine(directory, Id + ".cast");
            var sidecar = Path.Combine(directory, Id + ".json");

            File.WriteAllText(body,    new String('x', BodyBytes));
            File.WriteAllText(sidecar, "{}");
            File.SetLastWriteTimeUtc(body,    WrittenAt.UtcDateTime);
            File.SetLastWriteTimeUtc(sidecar, WrittenAt.UtcDateTime);

        }

        private String[] StoredIds()
            => Directory.EnumerateFiles(directory, "*.cast")
                        .Select(Path.GetFileNameWithoutExtension)
                        .OfType<String>()
                        .Order(StringComparer.Ordinal)
                        .ToArray();

        #endregion


        #region SessionRecorder_RotatesBySize_EveryPartPlaysAlone

        [Test]
        [CancelAfter(15000)]
        public async Task SessionRecorder_RotatesBySize_EveryPartPlaysAlone(CancellationToken CancellationToken)
        {

            var clock    = NewClock();
            var sink     = new InMemoryRecordingSink();
            var meta     = new SessionRecordingMetadata { RecordingId = "s1", Username = "achim" };

            var recorder = await SessionRecorder.BeginAsync(sink, meta, new RecordingRotation { MaxBytes = 10 }, clock, CancellationToken: CancellationToken);
            await recorder.StartAsync(120, 40, Command: "tail -f log", CancellationToken: CancellationToken);

            foreach (var line in new[] { "alpha\n", "bravo\n", "charl\n" })
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                await recorder.RecordOutputAsync(Utf8(line), CancellationToken);
            }

            clock.Advance(TimeSpan.FromSeconds(1));
            await recorder.RecordExitAsync(3, CancellationToken);
            await recorder.DisposeAsync();

            var parts  = Parts(sink);
            var parsed = parts.Select(part => AsciicastReader.Parse(part.Text)).ToArray();
            var meta1  = parts[0].FinalMetadata;
            var meta3  = parts[2].FinalMetadata;

            Assert.Multiple(() => {

                // 6 + 6 bytes would pass 10, so every line opens a part of its own; the exit marker carries no bytes.
                Assert.That(parts,                                    Has.Length.EqualTo(3));
                Assert.That(parts.Select(part => part.FinalMetadata.Part),        Is.EqualTo(new Int32?[] { 1, 2, 3 }));
                Assert.That(parts.Select(part => part.FinalMetadata.RecordingId), Is.EqualTo(new[] { "s1", "s1-2", "s1-3" }));
                Assert.That(parts.Select(part => part.FinalMetadata.SeriesId),    Is.All.EqualTo("s1"));
                Assert.That(parts.Select(part => part.FinalMetadata.PreviousRecordingId), Is.EqualTo(new String?[] { null, "s1", "s1-2" }));
                Assert.That(parts.Select(part => part.FinalMetadata.DisconnectReason),    Is.EqualTo(new[] { "rotated", "rotated", "normal" }));
                Assert.That(recorder.CompletedParts.Select(part => part.RecordingId),     Is.EqualTo(new[] { "s1", "s1-2" }));

                // Each part is an asciicast of its own: a header with the session's command and size, times from its own start.
                Assert.That(parsed.Select(cast => cast.Header.Command),  Is.All.EqualTo("tail -f log"));
                Assert.That(parsed.Select(cast => cast.Header.Width),    Is.All.EqualTo(120));
                Assert.That(parsed.Select(cast => cast.Header.Timestamp), Is.EqualTo(new DateTimeOffset?[] { start, start.AddSeconds(2), start.AddSeconds(3) }));
                Assert.That(parsed[0].Events[0].ElapsedSeconds,           Is.EqualTo(1.0).Within(0.0001));
                Assert.That(parsed[1].Events[0].ElapsedSeconds,           Is.EqualTo(0.0).Within(0.0001));
                Assert.That(String.Concat(parsed.Select(cast => cast.OutputText)), Is.EqualTo("alpha\nbravo\ncharl\n"));

                // The exit status belongs to the end of the session, the last part alone.
                Assert.That(parsed[2].ExitStatus,  Is.EqualTo(3));
                Assert.That(meta3.ExitStatus,      Is.EqualTo(3));
                Assert.That(meta1.ExitStatus,      Is.Null);
                Assert.That(meta1.Username,        Is.EqualTo("achim"), "every part keeps who the session was");
                Assert.That(meta1.EndedAt,         Is.EqualTo(start.AddSeconds(2)), "a part ends where the next begins");

            });

        }

        #endregion

        #region SessionRecorder_AnEventLargerThanAPart_GoesWholeIntoOne

        [Test]
        [CancelAfter(15000)]
        public async Task SessionRecorder_AnEventLargerThanAPart_GoesWholeIntoOne(CancellationToken CancellationToken)
        {

            var sink     = new InMemoryRecordingSink();
            var recorder = await SessionRecorder.BeginAsync(sink, new SessionRecordingMetadata(), new RecordingRotation { MaxBytes = 4 }, NewClock(), CancellationToken: CancellationToken);

            await recorder.StartAsync(CancellationToken: CancellationToken);
            await recorder.RecordOutputAsync(Utf8("0123456789"), CancellationToken);
            await recorder.RecordOutputAsync(Utf8("abcdefghij"), CancellationToken);
            await recorder.DisposeAsync();

            var outputs = Parts(sink).Select(part => AsciicastReader.Parse(part.Text).OutputText).ToArray();

            Assert.That(outputs, Is.EqualTo(new[] { "0123456789", "abcdefghij" }), "no event is split, and no part is empty");

        }

        #endregion

        #region SessionRecorder_RotatesByDuration_WithTheCurrentTerminalSize

        [Test]
        [CancelAfter(15000)]
        public async Task SessionRecorder_RotatesByDuration_WithTheCurrentTerminalSize(CancellationToken CancellationToken)
        {

            var clock    = NewClock();
            var sink     = new InMemoryRecordingSink();
            var recorder = await SessionRecorder.BeginAsync(sink, new SessionRecordingMetadata { RecordingId = "d" }, new RecordingRotation { MaxDuration = TimeSpan.FromMinutes(1) }, clock, CancellationToken: CancellationToken);

            await recorder.StartAsync(80, 24, CancellationToken: CancellationToken);
            await recorder.RecordOutputAsync(Utf8("one\n"), CancellationToken);

            clock.Advance(TimeSpan.FromSeconds(30));
            await recorder.RecordResizeAsync(132, 50, CancellationToken);

            // A silent hour opens no part on its own: the next event does, a single one.
            clock.Advance(TimeSpan.FromHours(1));
            await recorder.RecordOutputAsync(Utf8("two\n"), CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(1));
            await recorder.RecordOutputAsync(Utf8("three\n"), CancellationToken);
            await recorder.DisposeAsync();

            var parsed = Parts(sink).Select(part => AsciicastReader.Parse(part.Text)).ToArray();

            Assert.Multiple(() => {
                Assert.That(parsed,                  Has.Length.EqualTo(2));
                Assert.That(parsed[0].OutputText,    Is.EqualTo("one\n"));
                Assert.That(parsed[1].OutputText,    Is.EqualTo("two\nthree\n"));
                Assert.That(parsed[1].Header.Width,  Is.EqualTo(132), "the new part starts at the size the terminal has now");
                Assert.That(parsed[1].Header.Height, Is.EqualTo(50));
                Assert.That(parsed[1].Events[1].ElapsedSeconds, Is.EqualTo(1.0).Within(0.0001));
            });

        }

        #endregion

        #region SessionRecorder_TheByteCapBoundsTheWholeSession

        [Test]
        [CancelAfter(15000)]
        public async Task SessionRecorder_TheByteCapBoundsTheWholeSession(CancellationToken CancellationToken)
        {

            var sink     = new InMemoryRecordingSink();
            var recorder = await SessionRecorder.BeginAsync(sink, new SessionRecordingMetadata(), new RecordingRotation { MaxBytes = 5 }, NewClock(), MaxBytes: 12, CancellationToken: CancellationToken);

            await recorder.StartAsync(CancellationToken: CancellationToken);
            foreach (var chunk in new[] { "aaaa", "bbbb", "cccc", "dddd" })
                await recorder.RecordOutputAsync(Utf8(chunk), CancellationToken);
            await recorder.RecordExitAsync(0, CancellationToken);
            await recorder.DisposeAsync();

            var parts = Parts(sink);

            Assert.Multiple(() => {
                Assert.That(recorder.Truncated, Is.True);
                Assert.That(String.Concat(parts.Select(part => AsciicastReader.Parse(part.Text).OutputText)), Is.EqualTo("aaaabbbbcccc"));
                Assert.That(parts[^1].FinalMetadata.DisconnectReason, Is.EqualTo("recording-size-cap"));
                Assert.That(parts[^1].FinalMetadata.ExitStatus,       Is.EqualTo(0), "the sidecar still learns the exit status the cap kept out of the body");
            });

        }

        #endregion

        #region SftpTranscript_RotatesBySizeAndDuration

        [Test]
        [CancelAfter(15000)]
        public async Task SftpTranscript_RotatesBySizeAndDuration(CancellationToken CancellationToken)
        {

            var clock    = NewClock();
            var sink     = new InMemoryRecordingSink();
            var rotation = new RecordingRotation { MaxBytes = 350, MaxDuration = TimeSpan.FromMinutes(5) };
            var trx      = await SftpTranscriptRecorder.BeginAsync(sink, new SessionRecordingMetadata { RecordingId = "t" }, rotation, clock, CancellationToken);

            // Three entries of about 110 bytes fit in one part of 350, the fourth does not ...
            for (var i = 0; i < 4; i++)
                await trx.RecordAsync("read", "/firmware.bin", "Ok", i * 4096, 4096, CancellationToken);

            // ... and after six minutes the next entry opens a part however small the open one is.
            clock.Advance(TimeSpan.FromMinutes(6));
            await trx.RecordAsync("close", "/firmware.bin", "Ok", CancellationToken: CancellationToken);
            await trx.DisposeAsync();

            var parts   = Parts(sink);
            var entries = parts.Select(part => SftpTranscriptReader.Parse(part.Text)).ToArray();

            Assert.Multiple(() => {
                Assert.That(parts.Select(part => part.Format),                Is.All.EqualTo(RecordingFormat.SftpTranscript));
                Assert.That(entries.Select(part => part.Count),               Is.EqualTo(new[] { 3, 1, 1 }));
                Assert.That(parts.Select(part => Encoding.UTF8.GetByteCount(part.Text)), Is.All.LessThanOrEqualTo(350 + 3), "within the limit, give or take the CR of a Windows line break");
                Assert.That(entries.SelectMany(part => part).Select(entry => entry.Offset), Is.EqualTo(new Int64[] { 0, 4096, 8192, 12288, 0 }));
                Assert.That(parts.Select(part => part.FinalMetadata.RecordingId),           Is.EqualTo(new[] { "t", "t-2", "t-3" }));
                Assert.That(parts[0].FinalMetadata.StartedAt,                 Is.EqualTo(start));
                Assert.That(parts[2].FinalMetadata.StartedAt,                 Is.EqualTo(start.AddMinutes(6)));
            });

        }

        #endregion

        #region Sidecar_NamesThePartAndTheSeries

        [Test]
        public void Sidecar_NamesThePartAndTheSeries()
        {

            var json = new SessionRecordingMetadata {
                           RecordingId          = "abc-2",
                           SeriesId             = "abc",
                           Part                 = 2,
                           PreviousRecordingId  = "abc",
                           StartedAt            = start
                       }.ToJson();

            var plain = new SessionRecordingMetadata { RecordingId = "plain", StartedAt = start }.ToJson();

            using var doc      = JsonDocument.Parse(json);
            using var plainDoc = JsonDocument.Parse(plain);

            Assert.Multiple(() => {
                Assert.That(doc.RootElement.GetProperty("seriesId").GetString(),            Is.EqualTo("abc"));
                Assert.That(doc.RootElement.GetProperty("part").GetInt32(),                 Is.EqualTo(2));
                Assert.That(doc.RootElement.GetProperty("previousRecordingId").GetString(), Is.EqualTo("abc"));
                Assert.That(plainDoc.RootElement.TryGetProperty("part", out _),             Is.False, "a recording that is not rotated says nothing of parts");
                Assert.That(plainDoc.RootElement.TryGetProperty("seriesId", out _),         Is.False);
            });

        }

        #endregion


        #region DirectorySink_WritesEveryPartWithItsSidecar

        [Test]
        [CancelAfter(15000)]
        public async Task DirectorySink_WritesEveryPartWithItsSidecar(CancellationToken CancellationToken)
        {

            var sink     = new DirectoryRecordingSink(directory);
            var recorder = await SessionRecorder.BeginAsync(sink, new SessionRecordingMetadata { RecordingId = "dev7" }, new RecordingRotation { MaxBytes = 3 }, NewClock(), CancellationToken: CancellationToken);

            await recorder.StartAsync(CancellationToken: CancellationToken);
            await recorder.RecordOutputAsync(Utf8("abc"), CancellationToken);
            await recorder.RecordOutputAsync(Utf8("def"), CancellationToken);
            await recorder.DisposeAsync();

            var second = File.ReadAllText(Path.Combine(directory, "dev7-2.cast"));
            using var sidecar = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "dev7-2.json")));

            Assert.Multiple(() => {
                Assert.That(StoredIds(),                            Is.EqualTo(new[] { "dev7", "dev7-2" }));
                Assert.That(File.Exists(Path.Combine(directory, "dev7.json")), Is.True);
                Assert.That(AsciicastReader.Parse(second).OutputText, Is.EqualTo("def"));
                Assert.That(sidecar.RootElement.GetProperty("part").GetInt32(),                 Is.EqualTo(2));
                Assert.That(sidecar.RootElement.GetProperty("previousRecordingId").GetString(), Is.EqualTo("dev7"));
            });

        }

        #endregion

        #region DirectorySink_KeepsAtMostMaxRecordings_TheNewestOnes

        [Test]
        [CancelAfter(15000)]
        public async Task DirectorySink_KeepsAtMostMaxRecordings_TheNewestOnes(CancellationToken CancellationToken)
        {

            Store("old",    start.AddHours(-3));
            Store("middle", start.AddHours(-2));
            Store("recent", start.AddHours(-1));

            var sink   = new DirectoryRecordingSink(directory, new RecordingRetention { MaxRecordings = 3 }, NewClock());
            var handle = await sink.BeginAsync(new SessionRecordingMetadata { RecordingId = "new" }, RecordingFormat.AsciicastV2, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(StoredIds(), Is.EqualTo(new[] { "middle", "new", "recent" }), "beginning the fourth made room by removing the oldest");
                Assert.That(File.Exists(Path.Combine(directory, "old.json")), Is.False, "the sidecar goes with its body");
            });

            await handle.CompleteAsync(new SessionRecordingMetadata { RecordingId = "new" }, CancellationToken);
            await handle.DisposeAsync();

        }

        #endregion

        #region DirectorySink_RemovesRecordingsOlderThanMaxAge

        [Test]
        [CancelAfter(15000)]
        public async Task DirectorySink_RemovesRecordingsOlderThanMaxAge(CancellationToken CancellationToken)
        {

            Store("eight-days", start.AddDays(-8));
            Store("six-days",   start.AddDays(-6));
            Store("today",      start.AddHours(-1));

            var sink    = new DirectoryRecordingSink(directory, new RecordingRetention { MaxAge = TimeSpan.FromDays(7) }, NewClock());
            var removed = await sink.PruneAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(removed,     Is.EqualTo(new[] { "eight-days" }));
                Assert.That(StoredIds(), Is.EqualTo(new[] { "six-days", "today" }));
            });

        }

        #endregion

        #region DirectorySink_KeepsTheTotalWithinMaxTotalBytes

        [Test]
        [CancelAfter(15000)]
        public async Task DirectorySink_KeepsTheTotalWithinMaxTotalBytes(CancellationToken CancellationToken)
        {

            // 100 bytes of body and 2 of sidecar each: 408 bytes in all.
            Store("a", start.AddMinutes(-4));
            Store("b", start.AddMinutes(-3));
            Store("c", start.AddMinutes(-2));
            Store("d", start.AddMinutes(-1));

            var sink    = new DirectoryRecordingSink(directory, new RecordingRetention { MaxTotalBytes = 250 }, NewClock());
            var removed = await sink.PruneAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(removed,     Is.EqualTo(new[] { "a", "b" }));
                Assert.That(StoredIds(), Is.EqualTo(new[] { "c", "d" }));
            });

        }

        #endregion

        #region DirectorySink_NeverRemovesAnOpenRecording_NorAFileOfItsOwn

        [Test]
        [CancelAfter(15000)]
        public async Task DirectorySink_NeverRemovesAnOpenRecording_NorAFileOfItsOwn(CancellationToken CancellationToken)
        {

            var notes   = Path.Combine(directory, "notes.txt");
            var config  = Path.Combine(directory, "config.json");
            File.WriteAllText(notes,  "keep me");
            File.WriteAllText(config, "{ \"keep\": true }");
            File.SetLastWriteTimeUtc(notes,  start.AddYears(-1).UtcDateTime);
            File.SetLastWriteTimeUtc(config, start.AddYears(-1).UtcDateTime);

            var sink    = new DirectoryRecordingSink(directory, new RecordingRetention { MaxAge = TimeSpan.FromMinutes(1), MaxRecordings = 1 }, NewClock());
            var handle  = await sink.BeginAsync(new SessionRecordingMetadata { RecordingId = "live" }, RecordingFormat.AsciicastV2, CancellationToken);
            await handle.Writer.WriteLineAsync("{\"version\":2}");
            await handle.Writer.FlushAsync(CancellationToken);
            File.SetLastWriteTimeUtc(Path.Combine(directory, "live.cast"), start.AddDays(-1).UtcDateTime);

            Store("finished", start.AddDays(-1));

            var whileOpen = await sink.PruneAsync(CancellationToken);

            await handle.CompleteAsync(new SessionRecordingMetadata { RecordingId = "live" }, CancellationToken);
            await handle.DisposeAsync();
            File.SetLastWriteTimeUtc(Path.Combine(directory, "live.cast"), start.AddDays(-1).UtcDateTime);
            File.SetLastWriteTimeUtc(Path.Combine(directory, "live.json"), start.AddDays(-1).UtcDateTime);

            var afterClose = await sink.PruneAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(whileOpen,  Is.EqualTo(new[] { "finished" }), "the open recording stays, however old and however many");
                Assert.That(afterClose, Is.EqualTo(new[] { "live" }),     "once closed, it is a recording like any other");
                Assert.That(File.Exists(notes),  Is.True, "a file that is not a recording stays");
                Assert.That(File.Exists(config), Is.True, "and so does a .json without a body beside it");
            });

        }

        #endregion

        #region DirectorySink_WithoutRetention_KeepsEverything

        [Test]
        [CancelAfter(15000)]
        public async Task DirectorySink_WithoutRetention_KeepsEverything(CancellationToken CancellationToken)
        {

            Store("ancient", start.AddYears(-10));

            var sink    = new DirectoryRecordingSink(directory);
            var handle  = await sink.BeginAsync(new SessionRecordingMetadata { RecordingId = "next" }, RecordingFormat.SftpTranscript, CancellationToken);
            await handle.DisposeAsync();

            var removed = await sink.PruneAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(removed, Is.Empty);
                Assert.That(File.Exists(Path.Combine(directory, "ancient.cast")), Is.True);
                Assert.That(File.Exists(Path.Combine(directory, "next.jsonl")),   Is.True);
            });

        }

        #endregion

    }

}
