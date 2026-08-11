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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP
{

    /// <summary>
    /// Per-session SFTP quotas and bandwidth caps. Size quotas bound how much a session may write; bandwidth
    /// caps throttle upload/download throughput via a token bucket. A null field means that dimension is
    /// unlimited.
    /// </summary>
    public sealed record SftpLimits
    {

        /// <summary>The maximum size of any single uploaded file, in bytes.</summary>
        public Int64?         MaxFileSize             { get; init; }

        /// <summary>The maximum total number of bytes a session may write.</summary>
        public Int64?         MaxBytesPerSession      { get; init; }

        /// <summary>The maximum number of files a session may create.</summary>
        public Int32?         MaxFileCount            { get; init; }

        /// <summary>The upload (client→server write) throughput cap, in bytes per second.</summary>
        public Int64?         UploadBytesPerSecond    { get; init; }

        /// <summary>The download (server→client read) throughput cap, in bytes per second.</summary>
        public Int64?         DownloadBytesPerSecond  { get; init; }

        /// <summary>The burst capacity for the bandwidth caps; defaults to one second's worth of the rate.</summary>
        public Int64?         BurstBytes              { get; init; }

        /// <summary>The clock used for bandwidth pacing; defaults to <see cref="TimeProvider.System"/>.</summary>
        public TimeProvider   TimeProvider            { get; init; } = TimeProvider.System;


        /// <summary>Whether any size/count quota is configured.</summary>
        public Boolean HasSizeQuota
            => MaxFileSize is not null || MaxBytesPerSession is not null || MaxFileCount is not null;

        /// <summary>Whether any bandwidth cap is configured.</summary>
        public Boolean HasBandwidthCap
            => UploadBytesPerSecond is > 0 || DownloadBytesPerSecond is > 0;

    }


    /// <summary>
    /// An SFTP quota violation. Maps to <see cref="SftpStatusCode.Failure"/> on the wire (SFTP v3 has no
    /// dedicated quota code) and carries the path of a partial upload that should be cleaned up.
    /// </summary>
    public sealed class SftpQuotaExceededException : SftpException
    {

        /// <summary>The path of the partially-written file to remove after the failed write, if any.</summary>
        public String? PathToCleanup { get; }

        /// <summary>Create a quota-exceeded exception.</summary>
        public SftpQuotaExceededException(String Message, String? PathToCleanup = null)
            : base(SftpStatusCode.Failure, Message)
        {
            this.PathToCleanup = PathToCleanup;
        }

    }


    /// <summary>
    /// Tracks a single SFTP session against its <see cref="SftpLimits"/>: files created, total bytes written
    /// and per-file high-water marks. Throws <see cref="SftpQuotaExceededException"/> the moment a quota
    /// would be exceeded, naming the offending file so the server can discard a partial upload.
    /// </summary>
    public sealed class SftpQuotaTracker
    {

        #region Data

        private readonly SftpLimits                                limits;
        private readonly Lock                                      gate          = new ();
        private readonly Dictionary<String, (String Path, Int64 HighWater)> writeHandles = [];

        private Int64  sessionBytes;
        private Int32  filesCreated;

        #endregion

        #region Properties

        /// <summary>The total bytes written so far this session.</summary>
        public Int64  SessionBytesWritten  { get { lock (gate) return sessionBytes; } }

        /// <summary>The number of files created so far this session.</summary>
        public Int32  FilesCreated         { get { lock (gate) return filesCreated; } }

        #endregion

        #region Constructor(s)

        /// <summary>Create a quota tracker for the given limits.</summary>
        public SftpQuotaTracker(SftpLimits Limits)
        {
            this.limits = Limits;
        }

        #endregion


        #region CheckCanCreate(Path)

        /// <summary>Verify a new file may be created (file-count quota) — throws before anything is created.</summary>
        public void CheckCanCreate(String Path)
        {
            lock (gate)
                if (limits.MaxFileCount is { } max && filesCreated + 1 > max)
                    throw new SftpQuotaExceededException($"File-count quota ({max}) exceeded.", Path);
        }

        #endregion

        #region RegisterWritable(Handle, Path, WasCreated)

        /// <summary>Register a writable handle (counting a creation, if any) so later writes can be metered.</summary>
        public void RegisterWritable(String Handle, String Path, Boolean WasCreated)
        {
            lock (gate)
            {
                if (WasCreated)
                    filesCreated++;
                writeHandles[Handle] = (Path, 0);
            }
        }

        #endregion

        #region OnWrite(Handle, Offset, Length)

        /// <summary>Meter a write against the per-file and per-session size quotas.</summary>
        public void OnWrite(String Handle, Int64 Offset, Int32 Length)
        {
            lock (gate)
            {

                var path = writeHandles.TryGetValue(Handle, out var e) ? e.Path : null;
                var end  = Offset + Length;

                if (limits.MaxFileSize is { } maxFile && end > maxFile)
                    throw new SftpQuotaExceededException($"Per-file size quota ({maxFile} bytes) exceeded.", path);

                if (limits.MaxBytesPerSession is { } maxSession && sessionBytes + Length > maxSession)
                    throw new SftpQuotaExceededException($"Per-session byte quota ({maxSession} bytes) exceeded.", path);

                sessionBytes += Length;

                if (writeHandles.TryGetValue(Handle, out var cur) && end > cur.HighWater)
                    writeHandles[Handle] = (cur.Path, end);

            }
        }

        #endregion

        #region OnClose(Handle)

        /// <summary>Forget a handle when it closes.</summary>
        public void OnClose(String Handle)
        {
            lock (gate)
                writeHandles.Remove(Handle);
        }

        #endregion

    }

}
