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

using System.Globalization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// When one recording of a long session is closed and the next part begun, instead of one file growing
    /// without end. Every part is a recording of its own: a valid asciicast (or SFTP transcript) that plays
    /// or parses alone, with its own sidecar that names its part number, the series it belongs to and the
    /// part before it. A part is closed only once it holds at least one event, so no part is ever empty.
    /// </summary>
    public sealed record RecordingRotation
    {

        /// <summary>
        /// Begin a new part before an event would take the current one past this many bytes: the output
        /// bytes of an asciicast, as <c>MaxBytes</c> of <see cref="SessionRecorder"/> counts them, or the
        /// UTF-8 bytes of the lines of an SFTP transcript. An event larger than this goes whole into a
        /// part of its own. <c>null</c>: no limit.
        /// </summary>
        public Int64?     MaxBytes     { get; init; }

        /// <summary>
        /// Begin a new part with the first event after the current one has been open this long. Checked
        /// when an event arrives, so a session that stays silent does not produce empty parts.
        /// <c>null</c>: no limit.
        /// </summary>
        public TimeSpan?  MaxDuration  { get; init; }


        #region (internal) IsDue(PartBytes, PartEvents, NextBytes, PartStartedAt, Now)

        /// <summary>
        /// Whether the next event, of <paramref name="NextBytes"/>, belongs in a new part.
        /// </summary>
        internal Boolean IsDue(Int64           PartBytes,
                               Int64           PartEvents,
                               Int64           NextBytes,
                               DateTimeOffset  PartStartedAt,
                               DateTimeOffset  Now)

            => PartEvents > 0 &&
               ((MaxBytes    is { } maxBytes    && PartBytes + NextBytes > maxBytes) ||
                (MaxDuration is { } maxDuration && Now - PartStartedAt   >= maxDuration));

        #endregion

        #region (static) FirstPart(Metadata)

        /// <summary>
        /// The metadata of the first part of a rotated recording: part 1 of the series named by its own id.
        /// </summary>
        public static SessionRecordingMetadata FirstPart(SessionRecordingMetadata Metadata)

            => Metadata with {
                   Part      = 1,
                   SeriesId  = Metadata.SeriesId ?? Metadata.RecordingId
               };

        #endregion

        #region (static) NextPart(Closed, StartedAt)

        /// <summary>
        /// The metadata of the part after <paramref name="Closed"/>: the same session, the next number, an
        /// id of <c>&lt;series&gt;-&lt;part&gt;</c>, and nothing yet of how it ends.
        /// </summary>
        public static SessionRecordingMetadata NextPart(SessionRecordingMetadata  Closed,
                                                        DateTimeOffset            StartedAt)
        {

            var series = Closed.SeriesId ?? Closed.RecordingId;
            var part   = (Closed.Part ?? 1) + 1;

            return Closed with {
                       RecordingId          = series + "-" + part.ToString(CultureInfo.InvariantCulture),
                       SeriesId             = series,
                       Part                 = part,
                       PreviousRecordingId  = Closed.RecordingId,
                       StartedAt            = StartedAt,
                       EndedAt              = null,
                       ExitStatus           = null,
                       DisconnectReason     = null
                   };

        }

        #endregion

    }


    /// <summary>
    /// How much a <see cref="DirectoryRecordingSink"/> keeps. Once a limit is exceeded, the recordings
    /// written to longest ago are removed, body and sidecar together, until every limit holds again.
    /// A recording the sink still has open is never removed, and neither is any file that is not a
    /// recording body (<c>.cast</c>, <c>.jsonl</c>) or the sidecar beside one.
    /// </summary>
    public sealed record RecordingRetention
    {

        /// <summary>
        /// Remove a recording last written to longer ago than this. <c>null</c>: no limit.
        /// </summary>
        public TimeSpan?  MaxAge         { get; init; }

        /// <summary>
        /// Keep the bodies and sidecars of all recordings within this many bytes. <c>null</c>: no limit.
        /// </summary>
        public Int64?     MaxTotalBytes  { get; init; }

        /// <summary>
        /// Keep at most this many recordings; a part of a rotated recording counts as one.
        /// <c>null</c>: no limit.
        /// </summary>
        public Int32?     MaxRecordings  { get; init; }

    }

}
