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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Records one interactive/exec channel as asciicast v2. Output events are timestamped through a
    /// <see cref="TimeProvider"/> (deterministic under a fake clock) and flushed incrementally, so a
    /// crash-truncated recording remains a valid prefix. The recording is <b>output-only by default</b>:
    /// input capture is an explicit opt-in, and even then credentials never appear — password,
    /// keyboard-interactive and TOTP inputs belong to the pre-channel authentication phase, which no channel
    /// recorder ever sees. A byte cap bounds the recording; once exceeded it stops cleanly (still valid).
    /// </summary>
    public sealed class SessionRecorder : IAsyncDisposable
    {

        #region Data

        private readonly ISessionRecording  recording;
        private readonly AsciicastWriter    writer;
        private readonly TimeProvider       timeProvider;
        private readonly Boolean            captureInput;
        private readonly Int64              maxBytes;

        private DateTimeOffset              startedAt;
        private Int64                       bytesWritten;
        private Boolean                     started;
        private Boolean                     completed;
        private Boolean                     truncated;
        private Int32                       width  = 80;
        private Int32                       height = 24;

        private SessionRecordingMetadata    metadata;

        #endregion

        #region Properties

        /// <summary>The current metadata for this recording.</summary>
        public SessionRecordingMetadata  Metadata   => metadata;

        /// <summary>Whether the byte cap was hit and recording stopped early (the file is still valid).</summary>
        public Boolean                   Truncated  => truncated;

        #endregion

        #region Constructor(s)

        /// <summary>Create a recorder over an open recording target.</summary>
        /// <param name="Recording">The recording sink handle.</param>
        /// <param name="Metadata">The session metadata (updated on completion).</param>
        /// <param name="TimeProvider">The clock for event timing; defaults to <see cref="TimeProvider.System"/>.</param>
        /// <param name="CaptureInput">Whether to also record terminal input (default false — output only).</param>
        /// <param name="MaxBytes">A cap on recorded output bytes; 0 means unbounded.</param>
        public SessionRecorder(ISessionRecording         Recording,
                               SessionRecordingMetadata  Metadata,
                               TimeProvider?             TimeProvider  = null,
                               Boolean                   CaptureInput  = false,
                               Int64                     MaxBytes      = 0)
        {
            this.recording     = Recording;
            this.writer        = new AsciicastWriter(Recording.Writer);
            this.metadata      = Metadata;
            this.timeProvider  = TimeProvider ?? System.TimeProvider.System;
            this.captureInput  = CaptureInput;
            this.maxBytes      = MaxBytes;
        }

        #endregion


        #region StartAsync(Width, Height, Command, Env, CancellationToken)

        /// <summary>Write the asciicast header. Call once before recording any events.</summary>
        public async ValueTask StartAsync(Int32                                Width         = 80,
                                          Int32                                Height        = 24,
                                          String?                              Command       = null,
                                          IReadOnlyDictionary<String, String>?  Env          = null,
                                          CancellationToken                    CancellationToken = default)
        {

            if (started)
                return;

            started        = true;
            this.width     = Width;
            this.height    = Height;
            this.startedAt = timeProvider.GetUtcNow();

            metadata = metadata with {
                           StartedAt = startedAt,
                           Command   = Command ?? metadata.Command
                       };

            await writer.WriteHeaderAsync(new AsciicastHeader {
                Width      = Width,
                Height     = Height,
                Timestamp  = startedAt,
                Command    = Command ?? metadata.Command,
                Env        = Env
            }, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region RecordOutputAsync(Data, CancellationToken)

        /// <summary>Record channel output (stdout/stderr as seen on a terminal replay).</summary>
        public ValueTask RecordOutputAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            => RecordAsync(AsciicastEventCode.Output, Encoding.UTF8.GetString(Data.Span), Data.Length, CancellationToken);

        #endregion

        #region RecordInputAsync(Data, CancellationToken)

        /// <summary>
        /// Record terminal input — a no-op unless input capture was explicitly enabled. Even when enabled,
        /// this only ever sees post-authentication channel keystrokes; credentials are never in scope.
        /// </summary>
        public ValueTask RecordInputAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            => captureInput
                   ? RecordAsync(AsciicastEventCode.Input, Encoding.UTF8.GetString(Data.Span), Data.Length, CancellationToken)
                   : ValueTask.CompletedTask;

        #endregion

        #region RecordResizeAsync(Columns, Rows, CancellationToken)

        /// <summary>Record a terminal resize.</summary>
        public ValueTask RecordResizeAsync(Int32 Columns, Int32 Rows, CancellationToken CancellationToken = default)
        {
            width  = Columns;
            height = Rows;
            return RecordAsync(AsciicastEventCode.Resize, $"{Columns}x{Rows}", 0, CancellationToken);
        }

        #endregion

        #region RecordExitAsync(ExitCode, CancellationToken)

        /// <summary>Record the command's exit status as a terminal marker and remember it for the sidecar.</summary>
        public async ValueTask RecordExitAsync(Int32 ExitCode, CancellationToken CancellationToken = default)
        {
            metadata = metadata with { ExitStatus = ExitCode };
            await RecordAsync(AsciicastEventCode.Marker, $"exit-status={ExitCode}", 0, CancellationToken).ConfigureAwait(false);
        }

        #endregion


        #region (private) RecordAsync(Code, Data, ByteCount, CancellationToken)

        private async ValueTask RecordAsync(AsciicastEventCode Code, String Data, Int32 ByteCount, CancellationToken CancellationToken)
        {

            if (!started || completed || truncated)
                return;

            if (maxBytes > 0 && bytesWritten + ByteCount > maxBytes)
            {
                truncated = true;
                metadata  = metadata with { DisconnectReason = "recording-size-cap" };
                return;
            }

            bytesWritten += ByteCount;

            var elapsed = (timeProvider.GetUtcNow() - startedAt).TotalSeconds;
            await writer.WriteEventAsync(new AsciicastEvent(elapsed, Code, Data), CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region DisposeAsync()

        /// <summary>Finalize the recording, writing the closing metadata sidecar.</summary>
        public async ValueTask DisposeAsync()
        {

            if (completed)
                return;

            completed = true;

            metadata = metadata with {
                           EndedAt          = timeProvider.GetUtcNow(),
                           DisconnectReason = metadata.DisconnectReason ?? "normal"
                       };

            await recording.CompleteAsync(metadata).ConfigureAwait(false);
            await recording.DisposeAsync().ConfigureAwait(false);

        }

        #endregion

    }

}
