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

using Microsoft.Win32.SafeHandles;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP
{

    /// <summary>
    /// An SFTP file system backed by a real local directory, confined to a <b>root jail</b>: every client
    /// path is a POSIX-style absolute path resolved beneath the configured root, and any attempt to escape
    /// it (via <c>..</c>, an absolute path, or a drive letter) is canonicalized and rejected with
    /// <see cref="SftpStatusCode.PermissionDenied"/>. Offset-based reads/writes use
    /// <see cref="RandomAccess"/> so concurrent operations on one handle are safe. This is the workhorse for
    /// real device-fleet use (paired with the upload-only / download-only access profiles).
    /// </summary>
    /// <remarks>
    /// Symbolic links are followed here rather than by the operating system: every link on a path is read
    /// and the path it leads to checked against the root, so a link that leads out of the root - one a
    /// client made, or one that was there before - is refused rather than followed. A client can only make
    /// links that lead into the root, and they are stored relative to the link, so they still do when the
    /// root moves. What this cannot guard against is another process swapping a directory on the path for
    /// a link between the check and the open.
    /// </remarks>
    public sealed class LocalSftpFileSystem : ISftpFileSystem
    {

        #region Data

        private sealed class FileEntry { public required SafeFileHandle Handle; public required String VirtualPath; }
        private sealed class DirEntry  { public required List<SftpDirectoryEntry> Entries; public Int32 Index; }

        private readonly String                       root;
        private readonly Boolean                      readOnly;
        private readonly Dictionary<String, Object>   handles = [];
        private readonly Lock                         gate    = new ();
        private Int64                                 handleCounter;

        /// <summary>
        /// The links followed on one path before it is given up as a loop, as Linux's limit.
        /// </summary>
        private const Int32 MaxLinkHops = 40;

        private static readonly StringComparison PathComparison =
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a root-jailed local SFTP file system.
        /// </summary>
        /// <param name="RootDirectory">The directory that becomes the SFTP root <c>/</c> (created if missing).</param>
        /// <param name="ReadOnly">When true, all mutating operations are refused with permission denied.</param>
        public LocalSftpFileSystem(String RootDirectory, Boolean ReadOnly = false)
        {
            this.root      = Path.TrimEndingDirectorySeparator(Path.GetFullPath(RootDirectory));
            this.readOnly  = ReadOnly;
            Directory.CreateDirectory(this.root);
        }

        #endregion


        #region ISftpFileSystem

        public ValueTask<String> OpenAsync(String Path, SftpOpenFlags Flags, CancellationToken CancellationToken = default)
        {

            var physical = Resolve(Path);
            var writing  = (Flags & (SftpOpenFlags.Write | SftpOpenFlags.Append | SftpOpenFlags.Create | SftpOpenFlags.Truncate)) != 0;

            if (writing && readOnly)
                throw new SftpException(SftpStatusCode.PermissionDenied, "The file system is read-only.");

            var mode =
                Flags.HasFlag(SftpOpenFlags.Create) && Flags.HasFlag(SftpOpenFlags.Exclusive) ? FileMode.CreateNew   :
                Flags.HasFlag(SftpOpenFlags.Create) && Flags.HasFlag(SftpOpenFlags.Truncate)  ? FileMode.Create      :
                Flags.HasFlag(SftpOpenFlags.Create)                                           ? FileMode.OpenOrCreate :
                Flags.HasFlag(SftpOpenFlags.Truncate)                                         ? FileMode.Truncate     :
                Flags.HasFlag(SftpOpenFlags.Append)                                           ? FileMode.OpenOrCreate :
                                                                                                FileMode.Open;

            var access = writing
                             ? (Flags.HasFlag(SftpOpenFlags.Read) ? FileAccess.ReadWrite : FileAccess.Write)
                             : FileAccess.Read;

            return Guard(() =>
            {
                var handle = System.IO.File.OpenHandle(physical, mode, access, FileShare.ReadWrite, FileOptions.Asynchronous);
                return ValueTask.FromResult(Register(new FileEntry { Handle = handle, VirtualPath = ToVirtual(physical) }));
            });

        }

        public async ValueTask<Byte[]> ReadAsync(String Handle, Int64 Offset, Int32 Length, CancellationToken CancellationToken = default)
        {

            var entry  = File(Handle);
            var buffer = new Byte[Length];

            var read   = await RandomAccess.ReadAsync(entry.Handle, buffer.AsMemory(0, Length), Offset, CancellationToken).ConfigureAwait(false);

            return read == Length ? buffer : buffer[..read];

        }

        public async ValueTask WriteAsync(String Handle, Int64 Offset, ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
        {
            if (readOnly)
                throw new SftpException(SftpStatusCode.PermissionDenied, "The file system is read-only.");
            var entry = File(Handle);
            await RandomAccess.WriteAsync(entry.Handle, Data, Offset, CancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Flush the handle to stable storage. <see cref="RandomAccess"/> writes land in the operating
        /// system's page cache, so a successful WRITE is not yet a durable one — this is the call that
        /// makes it so, and the only reason <c>fsync@openssh.com</c> is worth answering.
        /// </summary>
        public ValueTask FlushAsync(String Handle, CancellationToken CancellationToken = default)
        {
            RandomAccess.FlushToDisk(File(Handle).Handle);
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync(String Handle, CancellationToken CancellationToken = default)
        {
            lock (gate)
            {
                if (handles.Remove(Handle, out var h) && h is FileEntry f)
                    f.Handle.Dispose();
                return ValueTask.CompletedTask;
            }
        }

        public ValueTask<String> OpenDirectoryAsync(String Path, CancellationToken CancellationToken = default)
        {

            var physical = Resolve(Path);

            if (!Directory.Exists(physical))
                throw new SftpException(SftpStatusCode.NoSuchFile, $"No such directory: {Path}");

            var entries = new List<SftpDirectoryEntry> {
                new (".",  SftpFileAttributes.Directory()),
                new ("..", SftpFileAttributes.Directory())
            };

            // A listing shows a link as a link, as LSTAT does, not what it points to.
            return Guard(() =>
            {
                foreach (var entry in new DirectoryInfo(physical).EnumerateFileSystemInfos())
                    entries.Add(new SftpDirectoryEntry(entry.Name, LStatAttributes(entry.FullName)));

                return ValueTask.FromResult(Register(new DirEntry { Entries = entries }));
            });

        }

        public ValueTask<IReadOnlyList<SftpDirectoryEntry>> ReadDirectoryAsync(String Handle, CancellationToken CancellationToken = default)
        {
            lock (gate)
            {
                var dir = Dir(Handle);
                if (dir.Index >= dir.Entries.Count)
                    return ValueTask.FromResult<IReadOnlyList<SftpDirectoryEntry>>([]);
                var batch = dir.Entries.Skip(dir.Index).Take(64).ToList();
                dir.Index += batch.Count;
                return ValueTask.FromResult<IReadOnlyList<SftpDirectoryEntry>>(batch);
            }
        }

        public ValueTask<SftpFileAttributes> StatAsync(String Path, CancellationToken CancellationToken = default)
        {

            var physical = Resolve(Path);

            if (System.IO.File.Exists(physical))
                return ValueTask.FromResult(FileAttributesFor(physical));

            if (Directory.Exists(physical))
                return ValueTask.FromResult(DirectoryAttributes(physical));

            throw new SftpException(SftpStatusCode.NoSuchFile, $"No such path: {Path}");

        }

        public ValueTask<SftpFileAttributes> LStatAsync(String Path, CancellationToken CancellationToken = default)
        {

            var physical = Resolve(Path, FollowFinal: false);

            if (!Exists(physical))
                throw new SftpException(SftpStatusCode.NoSuchFile, $"No such path: {Path}");

            return Guard(() => ValueTask.FromResult(LStatAttributes(physical)));

        }

        public ValueTask<String> ReadLinkAsync(String Path, CancellationToken CancellationToken = default)
        {

            var physical = Resolve(Path, FollowFinal: false);

            if (!Exists(physical))
                throw new SftpException(SftpStatusCode.NoSuchFile, $"No such path: {Path}");

            var target = LinkTarget(physical)
                             ?? throw new SftpException(SftpStatusCode.Failure, $"Not a symbolic link: {Path}");

            // Never the server's own paths: a relative target is shown as it stands, an absolute one (a link
            // a client did not make) is mapped back into the root, and one that leads out of it not at all.
            var landed = Landing(physical, target);

            if (!InRoot(landed))
                throw new SftpException(SftpStatusCode.PermissionDenied, "The symbolic link leads out of the root jail.");

            return ValueTask.FromResult(System.IO.Path.IsPathRooted(target)
                                            ? ToVirtual(landed)
                                            : target.Replace('\\', '/'));

        }

        public ValueTask CreateSymbolicLinkAsync(String LinkPath, String TargetPath, CancellationToken CancellationToken = default)
        {

            RequireWritable();

            var link = Resolve(LinkPath, FollowFinal: false);

            if (Exists(link))
                throw new SftpException(SftpStatusCode.Failure, $"Already exists: {LinkPath}");

            // The target is a client path, absolute or relative to the link's directory, and has to lie
            // within the root. It is stored relative to the link - an absolute one too, which on disk would
            // name the server's own root - and that is what READLINK reads back.
            var directory = System.IO.Path.GetDirectoryName(link)!;
            var target    = Lexical(TargetPath.StartsWith('/')
                                        ? TargetPath
                                        : ToVirtual(directory).TrimEnd('/') + "/" + TargetPath);
            var relative  = System.IO.Path.GetRelativePath(directory, target);

            return Guard(() =>
            {
                if (Directory.Exists(target))
                    Directory.CreateSymbolicLink(link, relative);
                else
                    System.IO.File.CreateSymbolicLink(link, relative);
                return ValueTask.CompletedTask;
            });

        }

        public ValueTask SetAttributesAsync(String Path, SftpFileAttributes Attributes, CancellationToken CancellationToken = default)
        {

            RequireWritable();

            var physical    = Resolve(Path);
            var isFile      = System.IO.File.Exists(physical);
            var isDirectory = !isFile && Directory.Exists(physical);

            if (!isFile && !isDirectory)
                throw new SftpException(SftpStatusCode.NoSuchFile, $"No such path: {Path}");

            // Everything is checked before anything changes: a request that cannot be applied in full
            // changes nothing, rather than half of what it asked for.
            if (Attributes.UserId is not null || Attributes.GroupId is not null)
                throw new SftpException(SftpStatusCode.OpUnsupported, "Ownership cannot be changed on this server.");

            if (Attributes.Size is { } requestedSize && (!isFile || requestedSize < 0))
                throw new SftpException(SftpStatusCode.Failure, isFile ? $"Invalid size {requestedSize}." : $"Only a file has a size to set: {Path}");

            if (Attributes.Permissions is { } requested && OperatingSystem.IsWindows() && isDirectory && (requested & OwnerWrite) == 0)
                throw new SftpException(SftpStatusCode.OpUnsupported, "A directory cannot be made read-only on this server.");

            return Guard(() =>
            {

                // Permissions that grant writing go first and ones that take it away go last, so that a
                // request to make a read-only file writable and truncate it works, and one to set its time
                // and make it read-only does too.
                var grantsWrite = Attributes.Permissions is { } p && (p & OwnerWrite) != 0;

                if (grantsWrite)
                    ApplyPermissions(physical, isFile, Attributes.Permissions!.Value);

                if (Attributes.Size is { } size)
                {
                    using var handle = System.IO.File.OpenHandle(physical, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                    RandomAccess.SetLength(handle, size);
                }

                if (Attributes.AccessTime is { } atime)
                {
                    if (isFile) System.IO.File.SetLastAccessTimeUtc(physical, atime.UtcDateTime);
                    else        Directory.SetLastAccessTimeUtc     (physical, atime.UtcDateTime);
                }

                if (Attributes.ModifyTime is { } mtime)
                {
                    if (isFile) System.IO.File.SetLastWriteTimeUtc(physical, mtime.UtcDateTime);
                    else        Directory.SetLastWriteTimeUtc     (physical, mtime.UtcDateTime);
                }

                if (Attributes.Permissions is { } permissions && !grantsWrite)
                    ApplyPermissions(physical, isFile, permissions);

                return ValueTask.CompletedTask;

            });

        }

        public ValueTask MakeDirectoryAsync(String Path, CancellationToken CancellationToken = default)
        {
            RequireWritable();
            var physical = Resolve(Path, FollowFinal: false);
            if (LinkTarget(physical) is not null)
                throw new SftpException(SftpStatusCode.Failure, $"Already exists: {Path}");
            return Guard(() => { Directory.CreateDirectory(physical); return ValueTask.CompletedTask; });
        }

        // A link is removed itself, never what it points to - also a link to a directory, which REMOVE
        // takes as POSIX unlink() does.
        public ValueTask RemoveAsync(String Path, CancellationToken CancellationToken = default)
        {
            RequireWritable();
            var physical = Resolve(Path, FollowFinal: false);
            if (LinkTarget(physical) is not null)
                return Guard(() => { DeleteLink(physical); return ValueTask.CompletedTask; });
            if (!System.IO.File.Exists(physical))
                throw new SftpException(SftpStatusCode.NoSuchFile, $"No such file: {Path}");
            return Guard(() => { System.IO.File.Delete(physical); return ValueTask.CompletedTask; });
        }

        public ValueTask RemoveDirectoryAsync(String Path, CancellationToken CancellationToken = default)
        {
            RequireWritable();
            var physical = Resolve(Path, FollowFinal: false);
            if (LinkTarget(physical) is not null)
                throw new SftpException(SftpStatusCode.Failure, $"Not a directory: {Path}");
            if (!Directory.Exists(physical))
                throw new SftpException(SftpStatusCode.NoSuchFile, $"No such directory: {Path}");
            return Guard(() => { Directory.Delete(physical, recursive: false); return ValueTask.CompletedTask; });
        }

        public ValueTask RenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default)
        {
            RequireWritable();
            var oldPhysical = Resolve(OldPath, FollowFinal: false);
            var newPhysical = Resolve(NewPath, FollowFinal: false);
            return Guard(() =>
            {
                // A link moves itself. A relative one that now leads out of the root is refused on use.
                if (LinkTarget(oldPhysical) is not null)
                {
                    if (Exists(newPhysical))
                        throw new SftpException(SftpStatusCode.Failure, $"Already exists: {NewPath}");
                    if (System.IO.File.GetAttributes(oldPhysical).HasFlag(System.IO.FileAttributes.Directory))
                        Directory.Move(oldPhysical, newPhysical);
                    else
                        System.IO.File.Move(oldPhysical, newPhysical, overwrite: false);
                }
                else if (System.IO.File.Exists(oldPhysical))
                    System.IO.File.Move(oldPhysical, newPhysical, overwrite: false);
                else if (Directory.Exists(oldPhysical))
                    Directory.Move(oldPhysical, newPhysical);
                else
                    throw new SftpException(SftpStatusCode.NoSuchFile, $"No such path: {OldPath}");
                return ValueTask.CompletedTask;
            });
        }

        public ValueTask<String> RealPathAsync(String Path, CancellationToken CancellationToken = default)
            => ValueTask.FromResult(ToVirtual(Resolve(Path)));

        #endregion


        #region (private) path jail

        // Map a client POSIX path to a physical path within the root jail, following its symbolic links -
        // the last one too, unless FollowFinal is false (LSTAT, READLINK, and what acts on a link itself).
        private String Resolve(String VirtualPath, Boolean FollowFinal = true)
            => FollowLinks(Lexical(VirtualPath), FollowFinal);

        // The path as written, canonicalized: it must be the root itself or live strictly beneath it.
        private String Lexical(String VirtualPath)
        {

            var relative  = VirtualPath.Replace('\\', '/').TrimStart('/');
            var combined  = Path.GetFullPath(Path.Combine(root, relative));

            if (!InRoot(combined))
                throw new SftpException(SftpStatusCode.PermissionDenied, "Path escapes the root jail.");

            return combined;

        }

        // Walk the path below the root one name at a time. A link is read and replaced by where it leads,
        // which must again be within the root, and the walk starts over from there - so the path handed to
        // the operating system holds no link it would follow on its own.
        private String FollowLinks(String Physical, Boolean FollowFinal)
        {

            var hops = 0;

            while (true)
            {

                var relative = Path.GetRelativePath(root, Physical);
                var names    = relative == "." ? [] : relative.Split(Path.DirectorySeparatorChar);
                var current  = root;
                var restart  = false;

                for (var i = 0; i < names.Length && !restart; i++)
                {

                    var next = Path.Combine(current, names[i]);

                    if ((i < names.Length - 1 || FollowFinal) && LinkTarget(next) is String target)
                    {

                        if (++hops > MaxLinkHops)
                            throw new SftpException(SftpStatusCode.Failure, "Too many levels of symbolic links.");

                        var landed = Landing(next, target);

                        if (!InRoot(landed))
                            throw new SftpException(SftpStatusCode.PermissionDenied, "A symbolic link leads out of the root jail.");

                        Physical = Path.Combine([ landed, .. names[(i + 1)..] ]);
                        restart  = true;

                    }
                    else
                        current = next;

                }

                if (!restart)
                    return current;

            }

        }

        // Where a link's target leads: an absolute one as it stands, a relative one from the link's directory.
        private static String Landing(String Link, String Target)
            => Path.GetFullPath(Path.IsPathRooted(Target)
                                    ? Target
                                    : Path.Combine(Path.GetDirectoryName(Link)!, Target));

        private Boolean InRoot(String Physical)
            => Physical.Equals(root, PathComparison) ||
               Physical.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);

        // Map a physical path back to its POSIX virtual path (never leaking the physical root).
        private String ToVirtual(String Physical)
        {
            if (Physical.Equals(root, PathComparison))
                return "/";
            var relative = Path.GetRelativePath(root, Physical).Replace('\\', '/');
            return "/" + relative;
        }

        #endregion

        #region (private) helpers

        /// <summary>
        /// The POSIX owner-write bit (0200): on Windows the one permission bit with a meaning — the
        /// read-only attribute.
        /// </summary>
        private const UInt32 OwnerWrite = 0x80;

        // The path a symbolic link holds, or null for anything else - a missing path, or one below a file,
        // included: the operation that follows reports those. Junctions count as links; other reparse
        // points (deduplicated or cloud files) do not. Nothing here may carry a physical path to the client.
        private static String? LinkTarget(String Physical)
        {
            try
            {
                var attributes = System.IO.File.GetAttributes(Physical);
                if (!attributes.HasFlag(System.IO.FileAttributes.ReparsePoint))
                    return null;
                FileSystemInfo info = attributes.HasFlag(System.IO.FileAttributes.Directory)
                                          ? new DirectoryInfo(Physical)
                                          : new FileInfo(Physical);
                return info.LinkTarget;
            }
            catch (IOException)                 { return null; }
            catch (UnauthorizedAccessException) { throw new SftpException(SftpStatusCode.PermissionDenied, "Permission denied."); }
        }

        // Whether anything is there, a link that leads nowhere included.
        private static Boolean Exists(String Physical)
            => LinkTarget(Physical) is not null || System.IO.File.Exists(Physical) || Directory.Exists(Physical);

        // A link goes as itself: a link to a directory on Windows is a directory entry of its own.
        private static void DeleteLink(String Physical)
        {
            if (System.IO.File.GetAttributes(Physical).HasFlag(System.IO.FileAttributes.Directory))
                Directory.Delete(Physical);
            else
                System.IO.File.Delete(Physical);
        }

        // What LSTAT reports: a link as a link, anything else as STAT would.
        private static SftpFileAttributes LStatAttributes(String Physical)
        {

            if (LinkTarget(Physical) is String target)
            {
                var info = new FileInfo(Physical);
                return SftpFileAttributes.SymbolicLink(System.Text.Encoding.UTF8.GetByteCount(target)) with {
                           AccessTime = new DateTimeOffset(info.LastAccessTimeUtc, TimeSpan.Zero),
                           ModifyTime = new DateTimeOffset(info.LastWriteTimeUtc,  TimeSpan.Zero)
                       };
            }

            return Directory.Exists(Physical)
                       ? DirectoryAttributes(Physical)
                       : FileAttributesFor(Physical);

        }

        private void RequireWritable()
        {
            if (readOnly)
                throw new SftpException(SftpStatusCode.PermissionDenied, "The file system is read-only.");
        }

        // What a STAT reports is what is on disk: the real mode where the platform has one, and on Windows
        // the read-only attribute as the absent owner-write bit — not a fixed 0644 that no chmod could move.
        private static SftpFileAttributes FileAttributesFor(String Physical)
        {
            var info = new FileInfo(Physical);
            var mode = OperatingSystem.IsWindows()
                           ? (info.IsReadOnly ? 0x124u /* 0444 */ : 0x1A4u /* 0644 */)
                           : (UInt32) System.IO.File.GetUnixFileMode(Physical);
            return SftpFileAttributes.File(info.Length, 0x8000 | mode) with {
                       AccessTime = new DateTimeOffset(info.LastAccessTimeUtc, TimeSpan.Zero),
                       ModifyTime = new DateTimeOffset(info.LastWriteTimeUtc,  TimeSpan.Zero)
                   };
        }

        private static SftpFileAttributes DirectoryAttributes(String Physical)
        {
            var info = new DirectoryInfo(Physical);
            var mode = OperatingSystem.IsWindows()
                           ? 0x1EDu /* 0755 */
                           : (UInt32) System.IO.File.GetUnixFileMode(Physical);
            return SftpFileAttributes.Directory(0x4000 | mode) with {
                       AccessTime = new DateTimeOffset(info.LastAccessTimeUtc, TimeSpan.Zero),
                       ModifyTime = new DateTimeOffset(info.LastWriteTimeUtc,  TimeSpan.Zero)
                   };
        }

        // Only the rwx bits for owner, group and other: a client does not get to set setuid, setgid or the
        // sticky bit on the server's files. On Windows only the owner-write bit has a counterpart.
        private static void ApplyPermissions(String Physical, Boolean IsFile, UInt32 Permissions)
        {

            if (!OperatingSystem.IsWindows())
            {
                System.IO.File.SetUnixFileMode(Physical, (UnixFileMode) (Permissions & 0x1FF));
                return;
            }

            // A directory stays writable: making it read-only was refused before anything was changed.
            if (!IsFile)
                return;

            var attributes = System.IO.File.GetAttributes(Physical);
            System.IO.File.SetAttributes(Physical, (Permissions & OwnerWrite) == 0
                                                       ? attributes |  System.IO.FileAttributes.ReadOnly
                                                       : attributes & ~System.IO.FileAttributes.ReadOnly);

        }

        private String Register(Object Handle)
        {
            lock (gate)
            {
                var id = "h" + (++handleCounter);
                handles[id] = Handle;
                return id;
            }
        }

        private FileEntry File(String Handle)
        {
            lock (gate)
                return handles.TryGetValue(Handle, out var h) && h is FileEntry f ? f : throw new SftpException(SftpStatusCode.Failure, "Invalid handle.");
        }

        private DirEntry Dir(String Handle)
        {
            lock (gate)
                return handles.TryGetValue(Handle, out var h) && h is DirEntry d ? d : throw new SftpException(SftpStatusCode.Failure, "Invalid handle.");
        }

        // Run a file-system operation, translating IO exceptions into SFTP status codes.
        private static T Guard<T>(Func<T> Operation)
        {
            try
            {
                return Operation();
            }
            catch (SftpException)                    { throw; }
            catch (FileNotFoundException)            { throw new SftpException(SftpStatusCode.NoSuchFile,      "No such file."); }
            catch (DirectoryNotFoundException)       { throw new SftpException(SftpStatusCode.NoSuchFile,      "No such directory."); }
            catch (UnauthorizedAccessException)      { throw new SftpException(SftpStatusCode.PermissionDenied, "Permission denied."); }
            catch (IOException exception)            { throw new SftpException(SftpStatusCode.Failure,          exception.Message); }
        }

        #endregion

    }

}
