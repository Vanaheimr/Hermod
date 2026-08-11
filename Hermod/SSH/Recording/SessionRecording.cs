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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>The on-disk format of a session recording.</summary>
    public enum RecordingFormat
    {
        /// <summary>An interactive/exec recording as asciicast v2 (<c>.cast</c>).</summary>
        AsciicastV2,
        /// <summary>An SFTP operation transcript as JSON-lines (<c>.jsonl</c>).</summary>
        SftpTranscript
    }


    /// <summary>
    /// The "who / from where / when / under what policy" sidecar for a recorded session — everything a
    /// compliance reviewer needs to place a recording in context. Immutable; a fresh copy with the closing
    /// details is written when the recording completes.
    /// </summary>
    public sealed record SessionRecordingMetadata
    {

        /// <summary>A stable identifier for this recording (also the base file name).</summary>
        public String           RecordingId      { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>The session id of the SSH transport (hex), correlating with the audit stream.</summary>
        public String?          SessionId        { get; init; }

        /// <summary>The authenticated user name.</summary>
        public String?          Username         { get; init; }

        /// <summary>The public-key fingerprint the user authenticated with (SHA256:…), if any.</summary>
        public String?          KeyFingerprint   { get; init; }

        /// <summary>The certificate key-id, if the user authenticated with a certificate.</summary>
        public String?          CertificateKeyId { get; init; }

        /// <summary>The certificate principal that matched, if any.</summary>
        public String?          Principal        { get; init; }

        /// <summary>The peer endpoint (host:port) the session came from.</summary>
        public String?          PeerEndpoint     { get; init; }

        /// <summary>The name of the access profile in force.</summary>
        public String?          AccessProfile    { get; init; }

        /// <summary>The command that was run (for an <c>exec</c> session).</summary>
        public String?          Command          { get; init; }

        /// <summary>When the session started.</summary>
        public DateTimeOffset   StartedAt        { get; init; }

        /// <summary>When the session ended (set on completion).</summary>
        public DateTimeOffset?  EndedAt          { get; init; }

        /// <summary>The command's exit status, once known.</summary>
        public Int32?           ExitStatus       { get; init; }

        /// <summary>Why the session ended (normal, idle timeout, dead peer, size cap, …).</summary>
        public String?          DisconnectReason { get; init; }

    }


    /// <summary>A pluggable target for session recordings (files, blob storage, a SIEM, Hermod logging).</summary>
    public interface ISessionRecordingSink
    {
        /// <summary>Begin a recording; the returned handle appends JSON-lines and finalizes the sidecar.</summary>
        ValueTask<ISessionRecording> BeginAsync(SessionRecordingMetadata Metadata,
                                                RecordingFormat          Format,
                                                CancellationToken        CancellationToken = default);
    }


    /// <summary>
    /// One in-progress recording: append JSON-lines to <see cref="Writer"/>; call
    /// <see cref="CompleteAsync"/> to flush and persist the closing metadata sidecar.
    /// </summary>
    public interface ISessionRecording : IAsyncDisposable
    {

        /// <summary>The JSON-lines sink for this recording (one record per line).</summary>
        TextWriter Writer { get; }

        /// <summary>Flush and write the final metadata sidecar (end time, exit status, disconnect reason).</summary>
        ValueTask CompleteAsync(SessionRecordingMetadata FinalMetadata, CancellationToken CancellationToken = default);

    }

}
