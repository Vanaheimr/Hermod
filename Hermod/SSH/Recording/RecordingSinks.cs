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
    /// <c>&lt;id&gt;.json</c> metadata sidecar written on completion.
    /// </summary>
    public sealed class DirectoryRecordingSink : ISessionRecordingSink
    {

        private readonly String directory;

        /// <summary>
        /// Create a sink writing into the given directory (created if missing).
        /// </summary>
        public DirectoryRecordingSink(String Directory)
        {
            this.directory = Directory;
            System.IO.Directory.CreateDirectory(Directory);
        }

        public ValueTask<ISessionRecording> BeginAsync(SessionRecordingMetadata Metadata, RecordingFormat Format, CancellationToken CancellationToken = default)
        {
            var extension = Format == RecordingFormat.AsciicastV2 ? ".cast" : ".jsonl";
            var bodyPath  = Path.Combine(directory, Metadata.RecordingId + extension);
            var metaPath  = Path.Combine(directory, Metadata.RecordingId + ".json");
            return ValueTask.FromResult<ISessionRecording>(new FileRecording(bodyPath, metaPath));
        }


        private sealed class FileRecording : ISessionRecording
        {

            private readonly StreamWriter  body;
            private readonly String        metaPath;

            public FileRecording(String BodyPath, String MetaPath)
            {
                this.body      = new StreamWriter(new FileStream(BodyPath, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = false };
                this.metaPath  = MetaPath;
            }

            public TextWriter Writer => body;

            public async ValueTask CompleteAsync(SessionRecordingMetadata FinalMetadata, CancellationToken CancellationToken = default)
            {
                await body.FlushAsync(CancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(metaPath, FinalMetadata.ToJson(), CancellationToken).ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync()
                => await body.DisposeAsync().ConfigureAwait(false);

        }

    }

    #endregion

}
