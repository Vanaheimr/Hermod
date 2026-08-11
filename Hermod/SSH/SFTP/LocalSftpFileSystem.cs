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

        private static readonly StringComparison PathComparison =
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        #endregion

        #region Constructor(s)

        /// <summary>Create a root-jailed local SFTP file system.</summary>
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

            return Guard(() =>
            {
                foreach (var dir in Directory.EnumerateDirectories(physical))
                    entries.Add(new SftpDirectoryEntry(System.IO.Path.GetFileName(dir), DirectoryAttributes(dir)));

                foreach (var file in Directory.EnumerateFiles(physical))
                    entries.Add(new SftpDirectoryEntry(System.IO.Path.GetFileName(file), FileAttributesFor(file)));

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

        public ValueTask MakeDirectoryAsync(String Path, CancellationToken CancellationToken = default)
        {
            RequireWritable();
            var physical = Resolve(Path);
            return Guard(() => { Directory.CreateDirectory(physical); return ValueTask.CompletedTask; });
        }

        public ValueTask RemoveAsync(String Path, CancellationToken CancellationToken = default)
        {
            RequireWritable();
            var physical = Resolve(Path);
            if (!System.IO.File.Exists(physical))
                throw new SftpException(SftpStatusCode.NoSuchFile, $"No such file: {Path}");
            return Guard(() => { System.IO.File.Delete(physical); return ValueTask.CompletedTask; });
        }

        public ValueTask RemoveDirectoryAsync(String Path, CancellationToken CancellationToken = default)
        {
            RequireWritable();
            var physical = Resolve(Path);
            if (!Directory.Exists(physical))
                throw new SftpException(SftpStatusCode.NoSuchFile, $"No such directory: {Path}");
            return Guard(() => { Directory.Delete(physical, recursive: false); return ValueTask.CompletedTask; });
        }

        public ValueTask RenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default)
        {
            RequireWritable();
            var oldPhysical = Resolve(OldPath);
            var newPhysical = Resolve(NewPath);
            return Guard(() =>
            {
                if (System.IO.File.Exists(oldPhysical))
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

        // Map a client POSIX path to a physical path, guaranteeing it stays within the root jail.
        private String Resolve(String VirtualPath)
        {

            var relative  = VirtualPath.Replace('\\', '/').TrimStart('/');
            var combined  = Path.GetFullPath(Path.Combine(root, relative));

            // Canonicalized path must be the root itself or live strictly beneath it.
            if (!combined.Equals(root, PathComparison) &&
                !combined.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
                throw new SftpException(SftpStatusCode.PermissionDenied, "Path escapes the root jail.");

            return combined;

        }

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

        private void RequireWritable()
        {
            if (readOnly)
                throw new SftpException(SftpStatusCode.PermissionDenied, "The file system is read-only.");
        }

        private static SftpFileAttributes FileAttributesFor(String Physical)
        {
            var info = new FileInfo(Physical);
            return SftpFileAttributes.File(info.Length) with { ModifyTime = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) };
        }

        private static SftpFileAttributes DirectoryAttributes(String Physical)
        {
            var info = new DirectoryInfo(Physical);
            return SftpFileAttributes.Directory() with { ModifyTime = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) };
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
