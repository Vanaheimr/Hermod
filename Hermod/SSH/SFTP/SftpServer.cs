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
using System.Buffers;
using System.Buffers.Binary;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP
{

    /// <summary>
    /// Serves the SFTP protocol (version 3) over a channel duplex, dispatching requests to an
    /// <see cref="ISftpFileSystem"/> and returning the appropriate STATUS / HANDLE / DATA / NAME / ATTRS
    /// responses. One request is processed at a time (no pipelining), which is sufficient for correctness.
    /// </summary>
    public static class SftpServer
    {

        #region ServeAsync(Channel, FileSystem, CancellationToken)

        /// <summary>
        /// Run the SFTP server loop over an established subsystem channel. When <paramref name="Profile"/>
        /// is given, each operation is gated against it (least privilege): a denied operation returns
        /// SSH_FX_PERMISSION_DENIED without touching the file system. When <paramref name="Limits"/> is given,
        /// per-session size quotas are enforced (a quota overrun discards the partial upload) and upload/
        /// download throughput is throttled via a token bucket.
        /// </summary>
        public static async ValueTask ServeAsync(ISftpDuplex        Channel,
                                                 ISftpFileSystem    FileSystem,
                                                 SshAccessProfile?  Profile            = null,
                                                 SftpLimits?        Limits             = null,
                                                 CancellationToken  CancellationToken  = default)
        {

            var session = new SftpSession(
                               Limits is { HasSizeQuota: true } ? new SftpQuotaTracker(Limits) : null,
                               Limits is { UploadBytesPerSecond: > 0 }   ? new TokenBucketRateLimiter(Limits.UploadBytesPerSecond!.Value,   Limits.TimeProvider, Limits.BurstBytes) : null,
                               Limits is { DownloadBytesPerSecond: > 0 } ? new TokenBucketRateLimiter(Limits.DownloadBytesPerSecond!.Value, Limits.TimeProvider, Limits.BurstBytes) : null,
                               Limits);

            // 1. INIT → VERSION.
            var init = await ReadPacketAsync(Channel, CancellationToken).ConfigureAwait(false)
                       ?? throw new SshWireException("The SFTP client closed the channel before SSH_FXP_INIT.");

            if ((SftpPacketType) init[0] != SftpPacketType.Init)
                throw new SshWireException("Expected SSH_FXP_INIT.");

            await SendAsync(Channel, BuildVersion(), CancellationToken).ConfigureAwait(false);

            // 2. Request loop.
            while (true)
            {

                var packet = await ReadPacketAsync(Channel, CancellationToken).ConfigureAwait(false);
                if (packet is null)
                {
                    // The client closed the SFTP channel — complete the close handshake so the peer can exit.
                    try { await Channel.CloseAsync(CancellationToken).ConfigureAwait(false); } catch { }
                    return;
                }

                var request = ParseRequest(packet);
                Byte[] response;

                try
                {
                    if (Profile is not null && !Profile.AllowsSftp(RequiredPermission(request)))
                        response = BuildStatus(request.RequestId, SftpStatusCode.PermissionDenied, "Operation not permitted by the access profile.");
                    else
                        response = await DispatchAsync(FileSystem, session, request, CancellationToken).ConfigureAwait(false);
                }
                catch (SftpQuotaExceededException e)
                {
                    // Mid-write overrun: discard the partial upload and keep the session healthy.
                    await CleanupPartialAsync(FileSystem, request.Handle, e.PathToCleanup, CancellationToken).ConfigureAwait(false);
                    response = BuildStatus(request.RequestId, e.Code, e.Message);
                }
                catch (SftpException e)
                {
                    response = BuildStatus(request.RequestId, e.Code, e.Message);
                }
                catch (Exception e)
                {
                    response = BuildStatus(request.RequestId, SftpStatusCode.Failure, e.Message);
                }

                await SendAsync(Channel, response, CancellationToken).ConfigureAwait(false);

            }

        }

        private sealed record SftpSession(SftpQuotaTracker? Quota, TokenBucketRateLimiter? Upload, TokenBucketRateLimiter? Download, SftpLimits? Limits)
        {
            // Maps open handles → their path, so handle-based requests (FSTAT) can resolve a path.
            public Dictionary<String, String> HandlePaths { get; } = [];
        }

        private static async ValueTask CleanupPartialAsync(ISftpFileSystem FileSystem, String Handle, String? Path, CancellationToken CancellationToken)
        {
            if (Handle.Length > 0)
                try { await FileSystem.CloseAsync(Handle, CancellationToken).ConfigureAwait(false); } catch { }
            if (Path is not null)
                try { await FileSystem.RemoveAsync(Path, CancellationToken).ConfigureAwait(false); } catch { }
        }

        #endregion


        #region (private) DispatchAsync(FileSystem, Request, CancellationToken)

        private static async ValueTask<Byte[]> DispatchAsync(ISftpFileSystem FileSystem, SftpSession Session, SftpRequest Request, CancellationToken CancellationToken)
        {

            switch (Request.Type)
            {

                case SftpPacketType.RealPath:
                    return BuildName(Request.RequestId, [ new SftpDirectoryEntry(await FileSystem.RealPathAsync(Request.Path, CancellationToken).ConfigureAwait(false), SftpFileAttributes.Directory()) ]);

                case SftpPacketType.Open:
                {
                    var writable = (Request.OpenFlags & (SftpOpenFlags.Write | SftpOpenFlags.Append | SftpOpenFlags.Create | SftpOpenFlags.Truncate)) != 0;
                    var creating = Request.OpenFlags.HasFlag(SftpOpenFlags.Create);

                    // Enforce the file-count quota before anything is created on the backing store.
                    if (creating)
                        Session.Quota?.CheckCanCreate(Request.Path);

                    var handle = await FileSystem.OpenAsync(Request.Path, Request.OpenFlags, CancellationToken).ConfigureAwait(false);

                    if (writable)
                        Session.Quota?.RegisterWritable(handle, Request.Path, creating);
                    Session.HandlePaths[handle] = Request.Path;

                    return BuildHandle(Request.RequestId, handle);
                }

                case SftpPacketType.OpenDir:
                {
                    var handle = await FileSystem.OpenDirectoryAsync(Request.Path, CancellationToken).ConfigureAwait(false);
                    Session.HandlePaths[handle] = Request.Path;
                    return BuildHandle(Request.RequestId, handle);
                }

                case SftpPacketType.Close:
                    Session.Quota?.OnClose(Request.Handle);
                    Session.HandlePaths.Remove(Request.Handle);
                    await FileSystem.CloseAsync(Request.Handle, CancellationToken).ConfigureAwait(false);
                    return BuildStatus(Request.RequestId, SftpStatusCode.Ok, "OK");

                case SftpPacketType.Read:
                {
                    var data = await FileSystem.ReadAsync(Request.Handle, Request.Offset, (Int32) Request.Length, CancellationToken).ConfigureAwait(false);
                    if (data.Length == 0)
                        return BuildStatus(Request.RequestId, SftpStatusCode.Eof, "EOF");
                    if (Session.Download is not null)
                        await Session.Download.ThrottleAsync(data.Length, CancellationToken).ConfigureAwait(false);
                    return BuildData(Request.RequestId, data);
                }

                case SftpPacketType.Write:
                    // Meter first (throws on overrun → the partial file is discarded upstream), then write.
                    Session.Quota?.OnWrite(Request.Handle, Request.Offset, Request.Data.Length);
                    await FileSystem.WriteAsync(Request.Handle, Request.Offset, Request.Data, CancellationToken).ConfigureAwait(false);
                    if (Session.Upload is not null)
                        await Session.Upload.ThrottleAsync(Request.Data.Length, CancellationToken).ConfigureAwait(false);
                    return BuildStatus(Request.RequestId, SftpStatusCode.Ok, "OK");

                case SftpPacketType.ReadDir:
                {
                    var entries = await FileSystem.ReadDirectoryAsync(Request.Handle, CancellationToken).ConfigureAwait(false);
                    return entries.Count == 0
                               ? BuildStatus(Request.RequestId, SftpStatusCode.Eof, "EOF")
                               : BuildName(Request.RequestId, entries);
                }

                case SftpPacketType.Stat or SftpPacketType.LStat:
                    return BuildAttrs(Request.RequestId, await FileSystem.StatAsync(Request.Path, CancellationToken).ConfigureAwait(false));

                case SftpPacketType.FStat:
                {
                    if (!Session.HandlePaths.TryGetValue(Request.Handle, out var fstatPath))
                        return BuildStatus(Request.RequestId, SftpStatusCode.Failure, "Invalid handle.");
                    return BuildAttrs(Request.RequestId, await FileSystem.StatAsync(fstatPath, CancellationToken).ConfigureAwait(false));
                }

                // Attribute-setting (permissions/mtime) is accepted and ignored — we do not model POSIX attrs.
                case SftpPacketType.SetStat or SftpPacketType.FSetStat:
                    return BuildStatus(Request.RequestId, SftpStatusCode.Ok, "OK");

                case SftpPacketType.MkDir:
                    await FileSystem.MakeDirectoryAsync(Request.Path, CancellationToken).ConfigureAwait(false);
                    return BuildStatus(Request.RequestId, SftpStatusCode.Ok, "OK");

                case SftpPacketType.Remove:
                    await FileSystem.RemoveAsync(Request.Path, CancellationToken).ConfigureAwait(false);
                    return BuildStatus(Request.RequestId, SftpStatusCode.Ok, "OK");

                case SftpPacketType.RmDir:
                    await FileSystem.RemoveDirectoryAsync(Request.Path, CancellationToken).ConfigureAwait(false);
                    return BuildStatus(Request.RequestId, SftpStatusCode.Ok, "OK");

                case SftpPacketType.Rename:
                    await FileSystem.RenameAsync(Request.Path, Request.TargetPath, CancellationToken).ConfigureAwait(false);
                    return BuildStatus(Request.RequestId, SftpStatusCode.Ok, "OK");

                case SftpPacketType.Extended:
                    return await DispatchExtendedAsync(FileSystem, Session, Request, CancellationToken).ConfigureAwait(false);

                default:
                    return BuildStatus(Request.RequestId, SftpStatusCode.OpUnsupported, $"Unsupported request {Request.Type}.");

            }

        }

        #endregion

        #region (private) DispatchExtendedAsync — OpenSSH SFTP extensions

        // Handle the OpenSSH SFTP extensions advertised in our VERSION packet.
        private static async ValueTask<Byte[]> DispatchExtendedAsync(ISftpFileSystem FileSystem, SftpSession Session, SftpRequest Request, CancellationToken CancellationToken)
        {

            switch (Request.ExtendedName)
            {

                // Atomic rename with replace semantics (Path = old, TargetPath = new).
                case "posix-rename@openssh.com":
                    try   { await FileSystem.RemoveAsync(Request.TargetPath, CancellationToken).ConfigureAwait(false); }
                    catch (SftpException) { /* target may not exist — fine */ }
                    await FileSystem.RenameAsync(Request.Path, Request.TargetPath, CancellationToken).ConfigureAwait(false);
                    return BuildStatus(Request.RequestId, SftpStatusCode.Ok, "OK");

                // Flush a handle to stable storage. This must reach the file system: a client sends fsync
                // precisely because it does not trust that a successful WRITE is durable, and answering OK
                // without acting would leave it believing a guarantee it never got.
                case "fsync@openssh.com":
                    await FileSystem.FlushAsync(Request.Handle, CancellationToken).ConfigureAwait(false);
                    return BuildStatus(Request.RequestId, SftpStatusCode.Ok, "OK");

                // Report file-system stats — we surface the session quota as free space, deliberately:
                // the real numbers of the host file system are none of a jailed session's business.
                // Both variants therefore answer identically and neither consults its argument (a path
                // for statvfs, an open handle for fstatvfs) — the figures are per-session, not per-file.
                case "statvfs@openssh.com":
                case "fstatvfs@openssh.com":
                    return BuildStatVfs(Request.RequestId, Session);

                // Report our protocol limits (max packet / read / write / open handles).
                case "limits@openssh.com":
                    return BuildLimits(Request.RequestId, Session);

                default:
                    return BuildStatus(Request.RequestId, SftpStatusCode.OpUnsupported, $"Unsupported extension '{Request.ExtendedName}'.");

            }

        }

        private static Byte[] BuildStatVfs(UInt32 RequestId, SftpSession Session)
        {

            const UInt64 blockSize = 1;   // byte-granular blocks → the byte quota maps to space exactly

            // Total/free blocks reflect the per-session byte quota, if any (otherwise a large nominal space).
            UInt64 totalBytes = Session.Limits?.MaxBytesPerSession is { } max ? (UInt64) max : 1UL << 44;   // ~16 TiB nominal
            UInt64 usedBytes  = Session.Quota is { } q ? (UInt64) q.SessionBytesWritten : 0;
            UInt64 freeBytes  = usedBytes >= totalBytes ? 0 : totalBytes - usedBytes;

            UInt64 totalFiles = Session.Limits?.MaxFileCount is { } fc ? (UInt64) fc : 1UL << 32;
            UInt64 usedFiles  = Session.Quota is { } q2 ? (UInt64) q2.FilesCreated : 0;
            UInt64 freeFiles  = usedFiles >= totalFiles ? 0 : totalFiles - usedFiles;

            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.ExtendedReply);
            w.WriteUInt32(RequestId);
            w.WriteUInt64(blockSize);                 // f_bsize
            w.WriteUInt64(blockSize);                 // f_frsize
            w.WriteUInt64(totalBytes / blockSize);    // f_blocks
            w.WriteUInt64(freeBytes  / blockSize);    // f_bfree
            w.WriteUInt64(freeBytes  / blockSize);    // f_bavail
            w.WriteUInt64(totalFiles);                // f_files
            w.WriteUInt64(freeFiles);                 // f_ffree
            w.WriteUInt64(freeFiles);                 // f_favail
            w.WriteUInt64(0);                         // f_sid
            w.WriteUInt64(0);                         // f_flag
            w.WriteUInt64(255);                       // f_namemax
            return abw.WrittenSpan.ToArray();

        }

        private static Byte[] BuildLimits(UInt32 RequestId, SftpSession Session)
        {

            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.ExtendedReply);
            w.WriteUInt32(RequestId);
            w.WriteUInt64(34_000);                    // max-packet-length
            w.WriteUInt64(32_768);                    // max-read-length
            w.WriteUInt64(32_768);                    // max-write-length
            w.WriteUInt64(Session.Limits?.MaxFileCount is { } fc ? (UInt64) fc : 0);   // max-open-handles (0 = no limit)
            return abw.WrittenSpan.ToArray();

        }

        #endregion

        #region (private) request parsing

        private readonly record struct SftpRequest(SftpPacketType Type, UInt32 RequestId, String Path, String TargetPath,
                                                   String Handle, Int64 Offset, UInt32 Length, SftpOpenFlags OpenFlags, Byte[] Data,
                                                   String ExtendedName);

        private static SftpRequest ParseRequest(Byte[] Packet)
        {

            var reader     = new SshPacketReader(Packet);
            var type       = (SftpPacketType) reader.ReadByte();
            var requestId  = reader.ReadUInt32();

            String path = "", target = "", handle = "", extendedName = "";
            Int64 offset = 0; UInt32 length = 0; SftpOpenFlags flags = 0; Byte[] data = [];

            switch (type)
            {

                case SftpPacketType.Extended:
                    extendedName = reader.ReadString();
                    switch (extendedName)
                    {
                        case "posix-rename@openssh.com":
                            path   = reader.ReadString();
                            target = reader.ReadString();
                            break;
                        // Handle-based: fstatvfs is statvfs' f-variant, so its argument is an open
                        // handle, not a path — reading it into `path` happened to work only because
                        // both are wire strings and the reply consults neither.
                        case "fsync@openssh.com" or "fstatvfs@openssh.com":
                            handle = reader.ReadString();
                            break;
                        case "statvfs@openssh.com":
                            path = reader.ReadString();
                            break;
                    }
                    break;
                case SftpPacketType.Open:
                    path  = reader.ReadString();
                    flags = (SftpOpenFlags) reader.ReadUInt32();
                    SftpFileAttributes.Decode(ref reader);
                    break;

                case SftpPacketType.OpenDir or SftpPacketType.RealPath or SftpPacketType.Stat or
                     SftpPacketType.LStat or SftpPacketType.MkDir or SftpPacketType.Remove or SftpPacketType.RmDir:
                    path = reader.ReadString();
                    break;

                case SftpPacketType.SetStat:
                    path = reader.ReadString();   // (attrs follow, ignored)
                    break;

                case SftpPacketType.FStat or SftpPacketType.FSetStat:
                    handle = reader.ReadString();   // (attrs follow for FSetStat, ignored)
                    break;

                case SftpPacketType.Rename:
                    path   = reader.ReadString();
                    target = reader.ReadString();
                    break;

                case SftpPacketType.Close:
                    handle = reader.ReadString();
                    break;

                case SftpPacketType.Read:
                    handle = reader.ReadString();
                    offset = (Int64) reader.ReadUInt64();
                    length = reader.ReadUInt32();
                    break;

                case SftpPacketType.Write:
                    handle = reader.ReadString();
                    offset = (Int64) reader.ReadUInt64();
                    data   = reader.ReadBinaryString();
                    break;

                case SftpPacketType.ReadDir:
                    handle = reader.ReadString();
                    break;
            }

            return new SftpRequest(type, requestId, path, target, handle, offset, length, flags, data, extendedName);

        }

        /// <summary>
        /// Any open flag that ends up writing to the file. <c>Truncate</c> and <c>Append</c> modify the
        /// file just as surely as <c>Write</c> does — <see cref="LocalSftpFileSystem"/> opens for write
        /// whenever any of these is set, so the permission gate must use the very same mask or a
        /// read-only session could truncate (or create) files it may only read.
        /// </summary>
        private const SftpOpenFlags WritingOpenFlags = SftpOpenFlags.Write     |
                                                       SftpOpenFlags.Append    |
                                                       SftpOpenFlags.Create    |
                                                       SftpOpenFlags.Truncate;

        // The SFTP permission an operation requires, for access-profile gating.
        private static SftpPermissions RequiredPermission(SftpRequest Request)
            => Request.Type switch {
                   SftpPacketType.Open      => (Request.OpenFlags & WritingOpenFlags) != 0
                                                   ? (Request.OpenFlags.HasFlag(SftpOpenFlags.Create) ? SftpPermissions.Create : SftpPermissions.Write)
                                                   : SftpPermissions.Read,
                   SftpPacketType.Read      => SftpPermissions.Read,
                   SftpPacketType.Write     => SftpPermissions.Write,
                   SftpPacketType.OpenDir   => SftpPermissions.List,
                   SftpPacketType.ReadDir   => SftpPermissions.List,
                   SftpPacketType.MkDir     => SftpPermissions.MakeDirectory,
                   SftpPacketType.Remove    => SftpPermissions.Delete,
                   SftpPacketType.RmDir     => SftpPermissions.Delete,
                   SftpPacketType.Rename    => SftpPermissions.Rename,
                   SftpPacketType.Stat      => SftpPermissions.Stat,
                   SftpPacketType.LStat     => SftpPermissions.Stat,
                   SftpPacketType.FStat     => SftpPermissions.Stat,

                   // An extension is only as harmless as what it does: posix-rename mutates, so it must
                   // demand the same permissions as the plain operations it is built from.
                   SftpPacketType.Extended  => RequiredExtendedPermission(Request.ExtendedName),

                   // Deny by default. Only the three stateless, non-privileged packets are free; anything
                   // added later must classify itself here rather than slip through a permissive fallback.
                   SftpPacketType.Close     or
                   SftpPacketType.RealPath  or
                   SftpPacketType.Init      => SftpPermissions.None,
                   _                        => SftpPermissions.All
               };

        // The permission an extended request requires. Unknown extensions demand everything, so an
        // unimplemented name can never be a way around the profile.
        private static SftpPermissions RequiredExtendedPermission(String? ExtendedName)
            => ExtendedName switch {
                   "posix-rename@openssh.com"  => SftpPermissions.Delete | SftpPermissions.Rename,
                   "fsync@openssh.com"         => SftpPermissions.Write,
                   "statvfs@openssh.com"       => SftpPermissions.Stat,
                   "fstatvfs@openssh.com"      => SftpPermissions.Stat,
                   "limits@openssh.com"        => SftpPermissions.None,
                   _                           => SftpPermissions.All
               };

        #endregion

        #region (private) response builders

        private static Byte[] BuildVersion()
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Version);
            w.WriteUInt32(SftpVersion.Three);
            // Advertise the OpenSSH extensions we support (name/data pairs). This list is the only thing
            // a peer may go by, so every name handled in DispatchExtendedAsync must appear here —
            // fstatvfs was answered but not advertised, which made a correct client never ask for it.
            w.WriteString("posix-rename@openssh.com"); w.WriteString("1");
            w.WriteString("fsync@openssh.com");        w.WriteString("1");
            w.WriteString("statvfs@openssh.com");      w.WriteString("2");
            w.WriteString("fstatvfs@openssh.com");     w.WriteString("2");
            w.WriteString("limits@openssh.com");       w.WriteString("1");
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildStatus(UInt32 RequestId, SftpStatusCode Code, String Message)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Status);
            w.WriteUInt32(RequestId); w.WriteUInt32((UInt32) Code); w.WriteString(Message); w.WriteString("");
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildHandle(UInt32 RequestId, String Handle)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Handle);
            w.WriteUInt32(RequestId); w.WriteString(Handle);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildData(UInt32 RequestId, Byte[] Data)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Data);
            w.WriteUInt32(RequestId); w.WriteBinaryString(Data);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildAttrs(UInt32 RequestId, SftpFileAttributes Attributes)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Attrs);
            w.WriteUInt32(RequestId);
            Attributes.Encode(ref w);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildName(UInt32 RequestId, IReadOnlyList<SftpDirectoryEntry> Entries)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Name);
            w.WriteUInt32(RequestId);
            w.WriteUInt32((UInt32) Entries.Count);
            foreach (var entry in Entries)
            {
                w.WriteString(entry.Name);
                w.WriteString(LongName(entry));
                entry.Attributes.Encode(ref w);
            }
            return abw.WrittenSpan.ToArray();
        }

        private static String LongName(SftpDirectoryEntry Entry)
        {
            var type = Entry.Attributes.IsDirectory ? 'd' : '-';
            var size = Entry.Attributes.Size ?? 0;
            return $"{type}rw-r--r-- 1 owner group {size,10} Jan  1 00:00 {Entry.Name}";
        }

        #endregion

        #region (private) framing

        internal static async ValueTask<Byte[]?> ReadPacketAsync(ISftpDuplex Channel, CancellationToken CancellationToken)
        {
            var lengthBytes = await Channel.TryReadExactAsync(4, CancellationToken).ConfigureAwait(false);
            if (lengthBytes is null)
                return null;
            var length = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
            return await Channel.ReadExactAsync((Int32) length, CancellationToken).ConfigureAwait(false);
        }

        internal static async ValueTask SendAsync(ISftpDuplex Channel, ReadOnlyMemory<Byte> Body, CancellationToken CancellationToken)
        {

            // Rent rather than allocate: on a bulk upload this runs once per 30 KiB chunk, and a fresh
            // array each time was a full copy of the transfer's worth of garbage.
            var framed = ArrayPool<Byte>.Shared.Rent(4 + Body.Length);
            try
            {
                BinaryPrimitives.WriteUInt32BigEndian(framed, (UInt32) Body.Length);
                Body.CopyTo(framed.AsMemory(4));
                await Channel.SendAsync(framed.AsMemory(0, 4 + Body.Length), CancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(framed);
            }
        }

        #endregion

    }

}
