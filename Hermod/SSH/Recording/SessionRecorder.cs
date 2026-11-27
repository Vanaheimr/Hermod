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
    /// A recorder begun on a sink with a <see cref="RecordingRotation"/> splits a long session into parts,
    /// each a valid asciicast with a header of its own and times counted from its own start, so every part
    /// plays alone; the byte cap then bounds the session across all its parts.
    /// </summary>
    public sealed class SessionRecorder : IAsyncDisposable
    {

        #region Data

        private readonly ISessionRecordingSink?          sink;
        private readonly RecordingRotation?              rotation;
        private readonly TimeProvider                    timeProvider;
        private readonly Boolean                         captureInput;
        private readonly Int64                           maxBytes;
        private readonly SemaphoreSlim                   gate            = new (1, 1);
        private readonly List<SessionRecordingMetadata>  completedParts  = [];

        private ISessionRecording                        recording;
        private AsciicastWriter                          writer;
        private DateTimeOffset                           startedAt;
        private Int64                                    bytesWritten;
        private Int64                                    partBytes;
        private Int64                                    partEvents;
        private Boolean                                  started;
        private Boolean                                  completed;
        private Boolean                                  truncated;
        private Int32                                    width  = 80;
        private Int32                                    height = 24;
        private String?                                  command;
        private IReadOnlyDictionary<String, String>?     env;

        private SessionRecordingMetadata                 metadata;

        #endregion

        #region Properties

        /// <summary>
        /// The current metadata for this recording - of the part now open, when the recording is rotated.
        /// </summary>
        public SessionRecordingMetadata                 Metadata        => metadata;

        /// <summary>
        /// Whether the byte cap was hit and recording stopped early (the file is still valid).
        /// </summary>
        public Boolean                                  Truncated       => truncated;

        /// <summary>
        /// The final metadata of every part closed by the rotation so far, the first part first.
        /// </summary>
        public IReadOnlyList<SessionRecordingMetadata>  CompletedParts  => completedParts.ToArray();

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a recorder over an open recording target.
        /// </summary>
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

            : this(Recording, Metadata, TimeProvider, CaptureInput, MaxBytes, null, null)

        { }

        private SessionRecorder(ISessionRecording         Recording,
                                SessionRecordingMetadata  Metadata,
                                TimeProvider?             TimeProvider,
                                Boolean                   CaptureInput,
                                Int64                     MaxBytes,
                                ISessionRecordingSink?    Sink,
                                RecordingRotation?        Rotation)
        {
            this.recording     = Recording;
            this.writer        = new AsciicastWriter(Recording.Writer);
            this.metadata      = Metadata;
            this.timeProvider  = TimeProvider ?? System.TimeProvider.System;
            this.captureInput  = CaptureInput;
            this.maxBytes      = MaxBytes;
            this.sink          = Sink;
            this.rotation      = Rotation;
        }

        #endregion


        #region (static) BeginAsync(Sink, Metadata, Rotation = null, TimeProvider = null, CaptureInput = false, MaxBytes = 0, CancellationToken = default)

        /// <summary>
        /// Begin a recording on <paramref name="Sink"/> and return its recorder. With a
        /// <paramref name="Rotation"/>, the recorder begins each further part on the same sink itself.
        /// </summary>
        /// <param name="Sink">Where the recording, and every part of it, is written.</param>
        /// <param name="Metadata">The session metadata (updated on completion).</param>
        /// <param name="Rotation">When to close a part and begin the next; <c>null</c>: one recording.</param>
        /// <param name="TimeProvider">The clock for event timing; defaults to <see cref="TimeProvider.System"/>.</param>
        /// <param name="CaptureInput">Whether to also record terminal input (default false — output only).</param>
        /// <param name="MaxBytes">A cap on recorded output bytes, across all parts; 0 means unbounded.</param>
        /// <param name="CancellationToken">A token to cancel beginning the recording.</param>
        public static async ValueTask<SessionRecorder> BeginAsync(ISessionRecordingSink     Sink,
                                                                  SessionRecordingMetadata  Metadata,
                                                                  RecordingRotation?        Rotation           = null,
                                                                  TimeProvider?             TimeProvider       = null,
                                                                  Boolean                   CaptureInput       = false,
                                                                  Int64                     MaxBytes           = 0,
                                                                  CancellationToken         CancellationToken  = default)
        {

            var first     = Rotation is null ? Metadata : RecordingRotation.FirstPart(Metadata);
            var recording = await Sink.BeginAsync(first, RecordingFormat.AsciicastV2, CancellationToken).ConfigureAwait(false);

            return new SessionRecorder(recording, first, TimeProvider, CaptureInput, MaxBytes, Sink, Rotation);

        }

        #endregion


        #region StartAsync(Width, Height, Command, Env, CancellationToken)

        /// <summary>
        /// Write the asciicast header. Call once before recording any events.
        /// </summary>
        public async ValueTask StartAsync(Int32                                Width         = 80,
                                          Int32                                Height        = 24,
                                          String?                              Command       = null,
                                          IReadOnlyDictionary<String, String>?  Env          = null,
                                          CancellationToken                    CancellationToken = default)
        {

            await gate.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {

                if (started)
                    return;

                started        = true;
                this.width     = Width;
                this.height    = Height;
                this.command   = Command ?? metadata.Command;
                this.env       = Env;
                this.startedAt = timeProvider.GetUtcNow();

                metadata = metadata with {
                               StartedAt = startedAt,
                               Command   = command
                           };

                await WriteHeaderAsync(CancellationToken).ConfigureAwait(false);

            }
            finally
            {
                gate.Release();
            }

        }

        #endregion

        #region RecordOutputAsync(Data, CancellationToken)

        /// <summary>
        /// Record channel output (stdout/stderr as seen on a terminal replay).
        /// </summary>
        public ValueTask RecordOutputAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            => RecordAsync(AsciicastEventCode.Output, Encoding.UTF8.GetString(Data.Span), Data.Length, null, CancellationToken);

        #endregion

        #region RecordInputAsync(Data, CancellationToken)

        /// <summary>
        /// Record terminal input — a no-op unless input capture was explicitly enabled. Even when enabled,
        /// this only ever sees post-authentication channel keystrokes; credentials are never in scope.
        /// </summary>
        public ValueTask RecordInputAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            => captureInput
                   ? RecordAsync(AsciicastEventCode.Input, Encoding.UTF8.GetString(Data.Span), Data.Length, null, CancellationToken)
                   : ValueTask.CompletedTask;

        #endregion

        #region RecordResizeAsync(Columns, Rows, CancellationToken)

        /// <summary>
        /// Record a terminal resize. A part begun after it carries the new size in its header.
        /// </summary>
        public ValueTask RecordResizeAsync(Int32 Columns, Int32 Rows, CancellationToken CancellationToken = default)
            => RecordAsync(AsciicastEventCode.Resize,
                           $"{Columns}x{Rows}",
                           0,
                           () => {
                               width  = Columns;
                               height = Rows;
                           },
                           CancellationToken);

        #endregion

        #region RecordExitAsync(ExitCode, CancellationToken)

        /// <summary>
        /// Record the command's exit status as a terminal marker and remember it for the sidecar - of the
        /// last part, when the recording is rotated.
        /// </summary>
        public ValueTask RecordExitAsync(Int32 ExitCode, CancellationToken CancellationToken = default)
            => RecordAsync(AsciicastEventCode.Marker,
                           $"exit-status={ExitCode}",
                           0,
                           () => metadata = metadata with { ExitStatus = ExitCode },
                           CancellationToken);

        #endregion


        #region (private) RecordAsync(Code, Data, ByteCount, Recorded, CancellationToken)

        /// <summary>
        /// Write one event, into a new part if the rotation says so, and then run <paramref name="Recorded"/>
        /// - whether the event was written or kept out, by the byte cap or because the header is not yet
        /// written. Only a finished recording ignores it altogether.
        /// </summary>
        private async ValueTask RecordAsync(AsciicastEventCode  Code,
                                            String              Data,
                                            Int32               ByteCount,
                                            Action?             Recorded,
                                            CancellationToken   CancellationToken)
        {

            await gate.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {

                if (completed)
                    return;

                if (started && !truncated && maxBytes > 0 && bytesWritten + ByteCount > maxBytes)
                {
                    truncated = true;
                    metadata  = metadata with { DisconnectReason = "recording-size-cap" };
                }

                if (started && !truncated)
                {

                    var now = timeProvider.GetUtcNow();

                    if (sink is not null &&
                        rotation is not null &&
                        rotation.IsDue(partBytes, partEvents, ByteCount, startedAt, now))
                    {
                        await RotateAsync(now, CancellationToken).ConfigureAwait(false);
                    }

                    bytesWritten += ByteCount;
                    partBytes    += ByteCount;
                    partEvents++;

                    await writer.WriteEventAsync(new AsciicastEvent((now - startedAt).TotalSeconds, Code, Data),
                                                 CancellationToken).ConfigureAwait(false);

                }

                Recorded?.Invoke();

            }
            finally
            {
                gate.Release();
            }

        }

        #endregion

        #region (private) RotateAsync(Now, CancellationToken)

        /// <summary>
        /// Close the part now open, its sidecar saying it was rotated, and begin the next with a header of
        /// its own.
        /// </summary>
        private async ValueTask RotateAsync(DateTimeOffset Now, CancellationToken CancellationToken)
        {

            var closed = metadata with {
                             EndedAt          = Now,
                             DisconnectReason = "rotated"
                         };

            await recording.CompleteAsync(closed, CancellationToken).ConfigureAwait(false);
            await recording.DisposeAsync().ConfigureAwait(false);
            completedParts.Add(closed);

            var next   = RecordingRotation.NextPart(closed, Now);

            recording  = await sink!.BeginAsync(next, RecordingFormat.AsciicastV2, CancellationToken).ConfigureAwait(false);
            writer     = new AsciicastWriter(recording.Writer);
            metadata   = next;
            startedAt  = Now;
            partBytes  = 0;
            partEvents = 0;

            await WriteHeaderAsync(CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region (private) WriteHeaderAsync(CancellationToken)

        private ValueTask WriteHeaderAsync(CancellationToken CancellationToken)

            => writer.WriteHeaderAsync(new AsciicastHeader {
                   Width      = width,
                   Height     = height,
                   Timestamp  = startedAt,
                   Command    = command,
                   Env        = env
               }, CancellationToken);

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Finalize the recording, writing the closing metadata sidecar.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            await gate.WaitAsync().ConfigureAwait(false);
            try
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
            finally
            {
                gate.Release();
            }

        }

        #endregion

    }

}
