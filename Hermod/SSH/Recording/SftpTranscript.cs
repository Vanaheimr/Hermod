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
using System.Text;
using System.Text.Json;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// One entry in an SFTP session transcript: which operation touched which path, at what offset/length,
    /// and with what result. A stream of these reconstructs exactly which bytes and paths a device touched —
    /// the audit trail that pairs with the upload-only / download-only access profiles.
    /// </summary>
    /// <param name="Timestamp">When the operation was handled.</param>
    /// <param name="Operation">The SFTP operation name (e.g. <c>open</c>, <c>write</c>, <c>read</c>, <c>remove</c>).</param>
    /// <param name="Path">The path acted upon (may be empty for handle-only operations).</param>
    /// <param name="Offset">The file offset for read/write operations.</param>
    /// <param name="Length">The number of bytes for read/write operations.</param>
    /// <param name="Result">The SFTP status result (e.g. <c>Ok</c>, <c>PermissionDenied</c>).</param>
    public readonly record struct SftpTranscriptEntry(DateTimeOffset  Timestamp,
                                                      String          Operation,
                                                      String          Path,
                                                      Int64           Offset,
                                                      Int64           Length,
                                                      String          Result);


    /// <summary>Writes an SFTP transcript as JSON-lines, one <see cref="SftpTranscriptEntry"/> per line, flushed incrementally.</summary>
    public sealed class SftpTranscriptRecorder : IAsyncDisposable
    {

        #region Data

        private readonly ISessionRecording  recording;
        private readonly TimeProvider       timeProvider;
        private SessionRecordingMetadata    metadata;
        private Boolean                     completed;

        #endregion

        #region Properties

        /// <summary>The current metadata for this transcript.</summary>
        public SessionRecordingMetadata Metadata => metadata;

        #endregion

        #region Constructor(s)

        /// <summary>Create an SFTP transcript recorder over an open recording target.</summary>
        public SftpTranscriptRecorder(ISessionRecording         Recording,
                                      SessionRecordingMetadata  Metadata,
                                      TimeProvider?             TimeProvider = null)
        {
            this.recording     = Recording;
            this.metadata      = Metadata;
            this.timeProvider  = TimeProvider ?? System.TimeProvider.System;
        }

        #endregion


        #region RecordAsync(Operation, Path, Result, Offset, Length, CancellationToken)

        /// <summary>Append one transcript entry (timestamped via the recorder's clock).</summary>
        public async ValueTask RecordAsync(String             Operation,
                                           String             Path,
                                           String             Result,
                                           Int64              Offset             = 0,
                                           Int64              Length             = 0,
                                           CancellationToken  CancellationToken  = default)
        {

            if (completed)
                return;

            var entry = new SftpTranscriptEntry(timeProvider.GetUtcNow(), Operation, Path, Offset, Length, Result);

            var buffer = new ArrayBufferWriter<Byte>();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteString("t",      entry.Timestamp);
                json.WriteString("op",     entry.Operation);
                json.WriteString("path",   entry.Path);
                if (entry.Offset != 0 || entry.Length != 0)
                {
                    json.WriteNumber("offset", entry.Offset);
                    json.WriteNumber("length", entry.Length);
                }
                json.WriteString("result", entry.Result);
                json.WriteEndObject();
            }

            await recording.Writer.WriteLineAsync(Encoding.UTF8.GetString(buffer.WrittenSpan)).ConfigureAwait(false);
            await recording.Writer.FlushAsync(CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region DisposeAsync()

        /// <summary>Finalize the transcript and write the closing metadata sidecar.</summary>
        public async ValueTask DisposeAsync()
        {

            if (completed)
                return;

            completed = true;
            metadata  = metadata with {
                            EndedAt          = timeProvider.GetUtcNow(),
                            DisconnectReason = metadata.DisconnectReason ?? "normal"
                        };

            await recording.CompleteAsync(metadata).ConfigureAwait(false);
            await recording.DisposeAsync().ConfigureAwait(false);

        }

        #endregion

    }


    /// <summary>Parses an SFTP transcript (JSON-lines) back into entries, tolerant of a truncated final line.</summary>
    public static class SftpTranscriptReader
    {

        /// <summary>Parse a complete or partially-written SFTP transcript.</summary>
        public static IReadOnlyList<SftpTranscriptEntry> Parse(String Text)
        {

            var entries = new List<SftpTranscriptEntry>();

            foreach (var raw in Text.Split('\n'))
            {

                var line = raw.TrimEnd('\r');
                if (line.Length == 0)
                    continue;

                JsonDocument doc;
                try     { doc = JsonDocument.Parse(line); }
                catch   { continue; }   // truncated last line — skip

                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                        continue;

                    entries.Add(new SftpTranscriptEntry(
                        root.TryGetProperty("t",      out var t)  ? t.GetDateTimeOffset() : default,
                        root.TryGetProperty("op",     out var op) ? op.GetString() ?? ""  : "",
                        root.TryGetProperty("path",   out var p)  ? p.GetString() ?? ""   : "",
                        root.TryGetProperty("offset", out var o)  ? o.GetInt64()          : 0,
                        root.TryGetProperty("length", out var l)  ? l.GetInt64()          : 0,
                        root.TryGetProperty("result", out var r)  ? r.GetString() ?? ""   : ""
                    ));
                }

            }

            return entries;

        }

    }

}
