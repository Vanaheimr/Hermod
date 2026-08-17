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
    /// An in-memory SFTP file system, mainly for tests and demos. Paths are POSIX-style absolute paths.
    /// </summary>
    public sealed class InMemorySftpFileSystem : ISftpFileSystem
    {

        #region Data

        private sealed class FileHandle    { public required String Path; }
        private sealed class DirHandle     { public required List<SftpDirectoryEntry> Entries; public Int32 Index; }

        private readonly Dictionary<String, List<Byte>>  files    = [];
        private readonly HashSet<String>                 dirs     = new () { "/" };
        private readonly Dictionary<String, Object>      handles  = [];
        private Int64                                    handleCounter;
        private readonly Lock                            gate     = new ();

        #endregion


        #region Seeding helpers

        /// <summary>
        /// Seed a file with content (for tests/demos).
        /// </summary>
        public void AddFile(String Path, Byte[] Content)
        {
            var normalized = Normalize(Path);
            files[normalized] = [.. Content];
            dirs.Add(ParentOf(normalized));
        }

        #endregion


        #region ISftpFileSystem

        public ValueTask<String> OpenAsync(String Path, SftpOpenFlags Flags, CancellationToken CancellationToken = default)
        {
            lock (gate)
            {
                var path = Normalize(Path);

                if (!files.ContainsKey(path))
                {
                    if (!Flags.HasFlag(SftpOpenFlags.Create))
                        throw new SftpException(SftpStatusCode.NoSuchFile, $"No such file: {path}");
                    files[path] = [];
                }
                else if (Flags.HasFlag(SftpOpenFlags.Truncate))
                    files[path].Clear();

                return ValueTask.FromResult(Register(new FileHandle { Path = path }));
            }
        }

        public ValueTask<Byte[]> ReadAsync(String Handle, Int64 Offset, Int32 Length, CancellationToken CancellationToken = default)
        {
            lock (gate)
            {
                var content = files[File(Handle).Path];
                if (Offset >= content.Count)
                    return ValueTask.FromResult(Array.Empty<Byte>());
                var count = (Int32) Math.Min(Length, content.Count - Offset);
                return ValueTask.FromResult(content.GetRange((Int32) Offset, count).ToArray());
            }
        }

        public ValueTask WriteAsync(String Handle, Int64 Offset, ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
        {
            lock (gate)
            {
                var content = files[File(Handle).Path];
                while (content.Count < Offset + Data.Length)
                    content.Add(0);
                for (var i = 0; i < Data.Length; i++)
                    content[(Int32) Offset + i] = Data.Span[i];
                return ValueTask.CompletedTask;
            }
        }

        public ValueTask CloseAsync(String Handle, CancellationToken CancellationToken = default)
        {
            lock (gate) { handles.Remove(Handle); return ValueTask.CompletedTask; }
        }

        public ValueTask<String> OpenDirectoryAsync(String Path, CancellationToken CancellationToken = default)
        {
            lock (gate)
            {
                var path = Normalize(Path);
                if (!dirs.Contains(path))
                    throw new SftpException(SftpStatusCode.NoSuchFile, $"No such directory: {path}");

                var entries = new List<SftpDirectoryEntry> {
                    new (".",  SftpFileAttributes.Directory()),
                    new ("..", SftpFileAttributes.Directory())
                };

                foreach (var dir in dirs.Where(d => d != "/" && ParentOf(d) == path))
                    entries.Add(new SftpDirectoryEntry(NameOf(dir), SftpFileAttributes.Directory()));

                foreach (var (filePath, content) in files.Where(f => ParentOf(f.Key) == path))
                    entries.Add(new SftpDirectoryEntry(NameOf(filePath), SftpFileAttributes.File(content.Count)));

                return ValueTask.FromResult(Register(new DirHandle { Entries = entries }));
            }
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
            lock (gate)
            {
                var path = Normalize(Path);
                if (files.TryGetValue(path, out var content))
                    return ValueTask.FromResult(SftpFileAttributes.File(content.Count));
                if (dirs.Contains(path))
                    return ValueTask.FromResult(SftpFileAttributes.Directory());
                throw new SftpException(SftpStatusCode.NoSuchFile, $"No such path: {path}");
            }
        }

        public ValueTask MakeDirectoryAsync(String Path, CancellationToken CancellationToken = default)
        {
            lock (gate) { dirs.Add(Normalize(Path)); return ValueTask.CompletedTask; }
        }

        public ValueTask RemoveAsync(String Path, CancellationToken CancellationToken = default)
        {
            lock (gate)
            {
                if (!files.Remove(Normalize(Path)))
                    throw new SftpException(SftpStatusCode.NoSuchFile, $"No such file: {Path}");
                return ValueTask.CompletedTask;
            }
        }

        public ValueTask RemoveDirectoryAsync(String Path, CancellationToken CancellationToken = default)
        {
            lock (gate) { dirs.Remove(Normalize(Path)); return ValueTask.CompletedTask; }
        }

        public ValueTask RenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default)
        {
            lock (gate)
            {
                var oldPath = Normalize(OldPath);
                var newPath = Normalize(NewPath);
                if (!files.Remove(oldPath, out var content))
                    throw new SftpException(SftpStatusCode.NoSuchFile, $"No such file: {oldPath}");
                files[newPath] = content;
                return ValueTask.CompletedTask;
            }
        }

        public ValueTask<String> RealPathAsync(String Path, CancellationToken CancellationToken = default)
            => ValueTask.FromResult(Normalize(Path));

        #endregion


        #region (private) helpers

        private String Register(Object Handle)
        {
            var id = "h" + (++handleCounter);
            handles[id] = Handle;
            return id;
        }

        private FileHandle File(String Handle) => handles.TryGetValue(Handle, out var h) && h is FileHandle f ? f : throw new SftpException(SftpStatusCode.Failure, "Invalid handle.");
        private DirHandle  Dir (String Handle) => handles.TryGetValue(Handle, out var h) && h is DirHandle  d ? d : throw new SftpException(SftpStatusCode.Failure, "Invalid handle.");

        private static String Normalize(String Path)
        {
            var p = Path.Replace('\\', '/').Trim();
            if (!p.StartsWith('/')) p = "/" + p;
            while (p.Length > 1 && p.EndsWith('/')) p = p[..^1];
            return p;
        }

        private static String ParentOf(String Path)
        {
            var i = Path.LastIndexOf('/');
            return i <= 0 ? "/" : Path[..i];
        }

        private static String NameOf(String Path)
            => Path[(Path.LastIndexOf('/') + 1)..];

        #endregion

    }

}
