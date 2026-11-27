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
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    #region SessionRecordingMetadataJson

    /// <summary>
    /// Serializes a <see cref="SessionRecordingMetadata"/> sidecar to JSON.
    /// </summary>
    public static class SessionRecordingMetadataJson
    {

        /// <summary>
        /// Render the metadata sidecar as indented JSON.
        /// </summary>
        public static String ToJson(this SessionRecordingMetadata Metadata)
        {

            var buffer = new ArrayBufferWriter<Byte>();
            using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();
                json.WriteString("recordingId", Metadata.RecordingId);
                WriteIfSet(json, "seriesId",         Metadata.SeriesId);
                if (Metadata.Part is { } part)
                    json.WriteNumber("part", part);
                WriteIfSet(json, "previousRecordingId", Metadata.PreviousRecordingId);
                WriteIfSet(json, "sessionId",        Metadata.SessionId);
                WriteIfSet(json, "username",         Metadata.Username);
                WriteIfSet(json, "keyFingerprint",   Metadata.KeyFingerprint);
                WriteIfSet(json, "certificateKeyId", Metadata.CertificateKeyId);
                WriteIfSet(json, "principal",        Metadata.Principal);
                WriteIfSet(json, "peerEndpoint",     Metadata.PeerEndpoint);
                WriteIfSet(json, "accessProfile",    Metadata.AccessProfile);
                WriteIfSet(json, "command",          Metadata.Command);
                json.WriteString("startedAt", Metadata.StartedAt);
                if (Metadata.EndedAt is { } ended)
                    json.WriteString("endedAt", ended);
                if (Metadata.ExitStatus is { } exit)
                    json.WriteNumber("exitStatus", exit);
                WriteIfSet(json, "disconnectReason", Metadata.DisconnectReason);
                json.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);

        }

        private static void WriteIfSet(Utf8JsonWriter Json, String Name, String? Value)
        {
            if (Value is not null)
                Json.WriteString(Name, Value);
        }

    }

    #endregion


    #region InMemoryRecordingSink

    /// <summary>
    /// An <see cref="ISessionRecordingSink"/> that keeps recordings in memory — for tests and diagnostics.
    /// </summary>
    public sealed class InMemoryRecordingSink : ISessionRecordingSink
    {

        private readonly ConcurrentBag<InMemoryRecording> recordings = [];

        /// <summary>
        /// All recordings begun on this sink.
        /// </summary>
        public IReadOnlyList<InMemoryRecording> Recordings => recordings.ToArray();

        public ValueTask<ISessionRecording> BeginAsync(SessionRecordingMetadata Metadata, RecordingFormat Format, CancellationToken CancellationToken = default)
        {
            var recording = new InMemoryRecording(Metadata, Format);
            recordings.Add(recording);
            return ValueTask.FromResult<ISessionRecording>(recording);
        }


        /// <summary>
        /// An in-memory recording: the JSON-lines body plus the final metadata sidecar.
        /// </summary>
        public sealed class InMemoryRecording : ISessionRecording
        {

            private readonly StringWriter body = new ();

            /// <summary>
            /// The recording format.
            /// </summary>
            public RecordingFormat            Format         { get; }

            /// <summary>
            /// The metadata at completion (or the initial metadata if still open).
            /// </summary>
            public SessionRecordingMetadata   FinalMetadata  { get; private set; }

            /// <summary>
            /// The recorded JSON-lines body.
            /// </summary>
            public String                     Text           => body.ToString();

            internal InMemoryRecording(SessionRecordingMetadata Metadata, RecordingFormat Format)
            {
                this.FinalMetadata  = Metadata;
                this.Format         = Format;
            }

            public TextWriter Writer => body;

            public ValueTask CompleteAsync(SessionRecordingMetadata FinalMetadata, CancellationToken CancellationToken = default)
            {
                this.FinalMetadata = FinalMetadata;
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

        }

    }

    #endregion


    #region DirectoryRecordingSink

    /// <summary>
    /// An <see cref="ISessionRecordingSink"/> that writes each recording to a rotating directory: a
    /// <c>&lt;id&gt;.cast</c> / <c>&lt;id&gt;.jsonl</c> body streamed incrementally, plus a
    /// <c>&lt;id&gt;.json</c> metadata sidecar written on completion. With a <see cref="RecordingRetention"/>,
    /// the sink removes the recordings written to longest ago whenever it begins a new one, and on
    /// <see cref="PruneAsync"/>, until the directory is within every limit again.
    /// </summary>
    public sealed class DirectoryRecordingSink : ISessionRecordingSink
    {

        #region Data

        private static readonly String[]                            bodyExtensions  = [ ".cast", ".jsonl" ];

        private readonly        String                              directory;
        private readonly        TimeProvider                        timeProvider;
        private readonly        ConcurrentDictionary<String, Byte>  open            = new (StringComparer.OrdinalIgnoreCase);
        private readonly        Lock                                pruneLock       = new ();

        #endregion

        #region Properties

        /// <summary>
        /// How much this sink keeps; <c>null</c>: everything.
        /// </summary>
        public RecordingRetention?  Retention  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a sink writing into the given directory (created if missing).
        /// </summary>
        /// <param name="Directory">The directory of the recordings.</param>
        /// <param name="Retention">How much to keep; <c>null</c>: everything.</param>
        /// <param name="TimeProvider">The clock the age of a recording is measured by; defaults to <see cref="TimeProvider.System"/>.</param>
        public DirectoryRecordingSink(String               Directory,
                                      RecordingRetention?  Retention     = null,
                                      TimeProvider?        TimeProvider  = null)
        {
            this.directory     = Directory;
            this.Retention     = Retention;
            this.timeProvider  = TimeProvider ?? System.TimeProvider.System;
            System.IO.Directory.CreateDirectory(Directory);
        }

        #endregion


        #region BeginAsync(Metadata, Format, CancellationToken)

        public ValueTask<ISessionRecording> BeginAsync(SessionRecordingMetadata Metadata, RecordingFormat Format, CancellationToken CancellationToken = default)
        {

            // Room for the one about to begin: a limit on the count holds once it exists.
            if (Retention is not null)
                Prune(Reserve: 1, CancellationToken);

            var id        = Metadata.RecordingId;
            var extension = Format == RecordingFormat.AsciicastV2 ? ".cast" : ".jsonl";
            var bodyPath  = Path.Combine(directory, id + extension);
            var metaPath  = Path.Combine(directory, id + ".json");

            open.TryAdd(id, 0);

            try
            {
                return ValueTask.FromResult<ISessionRecording>(new FileRecording(bodyPath, metaPath, () => open.TryRemove(id, out _)));
            }
            catch
            {
                open.TryRemove(id, out _);
                throw;
            }

        }

        #endregion

        #region PruneAsync(CancellationToken)

        /// <summary>
        /// Remove the recordings the <see cref="Retention"/> no longer keeps, those written to longest ago
        /// first, and return their ids. A recording still open on this sink is never removed, nor one whose
        /// files cannot be deleted just now (another process reading it, say); the next prune tries again.
        /// </summary>
        public ValueTask<IReadOnlyList<String>> PruneAsync(CancellationToken CancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<String>>(Retention is null ? [] : Prune(Reserve: 0, CancellationToken));

        #endregion

        #region (private) Prune(Reserve, CancellationToken)

        private IReadOnlyList<String> Prune(Int32 Reserve, CancellationToken CancellationToken)
        {

            var retention = Retention!;
            var removed   = new List<String>();

            lock (pruneLock)
            {

                var now        = timeProvider.GetUtcNow();
                var recordings = Scan();
                var totalBytes = recordings.Sum(recording => recording.Bytes);
                var count      = recordings.Count;

                foreach (var recording in recordings.Where(recording => !open.ContainsKey(recording.Id))
                                                    .OrderBy(recording => recording.LastWritten)
                                                    .ThenBy (recording => recording.Id, StringComparer.Ordinal))
                {

                    CancellationToken.ThrowIfCancellationRequested();

                    var tooOld   = retention.MaxAge        is { } maxAge        && now - recording.LastWritten > maxAge;
                    var tooBig   = retention.MaxTotalBytes is { } maxTotalBytes && totalBytes                 > maxTotalBytes;
                    var tooMany  = retention.MaxRecordings is { } maxRecordings && count + Reserve             > maxRecordings;

                    if (!tooOld && !tooBig && !tooMany)
                        continue;

                    if (TryDelete(recording))
                    {
                        removed.Add(recording.Id);
                        totalBytes -= recording.Bytes;
                        count--;
                    }

                }

            }

            return removed;

        }

        #endregion

        #region (private) Scan()

        private sealed record StoredRecording(String          Id,
                                              FileInfo        Body,
                                              FileInfo        Sidecar,
                                              Int64           Bytes,
                                              DateTimeOffset  LastWritten);

        /// <summary>
        /// Every recording in the directory: each body, with the sidecar beside it if there is one. A
        /// sidecar without a body is not a recording this sink knows, and stays.
        /// </summary>
        private List<StoredRecording> Scan()
        {

            var recordings = new List<StoredRecording>();

            foreach (var body in new DirectoryInfo(directory).EnumerateFiles())
            {

                if (!bodyExtensions.Contains(body.Extension, StringComparer.OrdinalIgnoreCase))
                    continue;

                var id          = Path.GetFileNameWithoutExtension(body.Name);
                var sidecar     = new FileInfo(Path.Combine(directory, id + ".json"));
                var lastWritten = sidecar.Exists && sidecar.LastWriteTimeUtc > body.LastWriteTimeUtc
                                      ? sidecar.LastWriteTimeUtc
                                      : body.LastWriteTimeUtc;

                recordings.Add(new StoredRecording(id,
                                                   body,
                                                   sidecar,
                                                   body.Length + (sidecar.Exists ? sidecar.Length : 0),
                                                   new DateTimeOffset(DateTime.SpecifyKind(lastWritten, DateTimeKind.Utc))));

            }

            return recordings;

        }

        #endregion

        #region (private) TryDelete(Recording)

        /// <summary>
        /// Delete the sidecar first and the body last: a prune cut short leaves a body, which the next
        /// prune finds again, rather than a sidecar no prune would ever look at.
        /// </summary>
        private static Boolean TryDelete(StoredRecording Recording)
        {
            try
            {
                Recording.Sidecar.Delete();
                Recording.Body.Delete();
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        #endregion


        #region (private class) FileRecording

        private sealed class FileRecording : ISessionRecording
        {

            private readonly StreamWriter  body;
            private readonly String        metaPath;
            private readonly Action        closed;
            private          Int32         disposed;

            public FileRecording(String BodyPath, String MetaPath, Action Closed)
            {
                this.body      = new StreamWriter(new FileStream(BodyPath, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = false };
                this.metaPath  = MetaPath;
                this.closed    = Closed;
            }

            public TextWriter Writer => body;

            public async ValueTask CompleteAsync(SessionRecordingMetadata FinalMetadata, CancellationToken CancellationToken = default)
            {
                await body.FlushAsync(CancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(metaPath, FinalMetadata.ToJson(), CancellationToken).ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync()
            {

                if (Interlocked.Exchange(ref disposed, 1) == 1)
                    return;

                try
                {
                    await body.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    closed();
                }

            }

        }

        #endregion

    }

    #endregion

}
