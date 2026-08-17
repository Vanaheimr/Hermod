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
    /// One entry returned by a directory listing: the file name and its attributes.
    /// </summary>
    public sealed record SftpDirectoryEntry(String Name, SftpFileAttributes Attributes);


    /// <summary>
    /// An SFTP operation error carrying the status code to return on the wire.
    /// </summary>
    public class SftpException : Exception
    {
        /// <summary>
        /// The SFTP status code.
        /// </summary>
        public SftpStatusCode Code { get; }

        /// <summary>
        /// Create an SFTP exception with a status code and message.
        /// </summary>
        public SftpException(SftpStatusCode Code, String Message) : base(Message)
        {
            this.Code = Code;
        }
    }


    /// <summary>
    /// The backing store an SFTP server exposes. Handles are opaque strings the file system manages;
    /// operations throw <see cref="SftpException"/> to map to an SFTP status. A concrete implementation
    /// (local directory with a root jail, in-memory, …) plugs in behind this seam.
    /// </summary>
    public interface ISftpFileSystem
    {

        /// <summary>
        /// Open (and, with the Create flag, create) a file, returning a handle.
        /// </summary>
        ValueTask<String> OpenAsync(String Path, SftpOpenFlags Flags, CancellationToken CancellationToken = default);

        /// <summary>
        /// Read up to <paramref name="Length"/> bytes at an offset; an empty result means end of file.
        /// </summary>
        ValueTask<Byte[]> ReadAsync(String Handle, Int64 Offset, Int32 Length, CancellationToken CancellationToken = default);

        /// <summary>
        /// Write bytes at an offset.
        /// </summary>
        ValueTask WriteAsync(String Handle, Int64 Offset, ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default);

        /// <summary>
        /// Close a file or directory handle.
        /// </summary>
        ValueTask CloseAsync(String Handle, CancellationToken CancellationToken = default);

        /// <summary>
        /// Open a directory for listing, returning a handle.
        /// </summary>
        ValueTask<String> OpenDirectoryAsync(String Path, CancellationToken CancellationToken = default);

        /// <summary>
        /// Read the next batch of directory entries; an empty result means end of listing.
        /// </summary>
        ValueTask<IReadOnlyList<SftpDirectoryEntry>> ReadDirectoryAsync(String Handle, CancellationToken CancellationToken = default);

        /// <summary>
        /// Get the attributes of a path.
        /// </summary>
        ValueTask<SftpFileAttributes> StatAsync(String Path, CancellationToken CancellationToken = default);

        /// <summary>
        /// Create a directory.
        /// </summary>
        ValueTask MakeDirectoryAsync(String Path, CancellationToken CancellationToken = default);

        /// <summary>
        /// Remove a file.
        /// </summary>
        ValueTask RemoveAsync(String Path, CancellationToken CancellationToken = default);

        /// <summary>
        /// Remove an (empty) directory.
        /// </summary>
        ValueTask RemoveDirectoryAsync(String Path, CancellationToken CancellationToken = default);

        /// <summary>
        /// Rename a file or directory.
        /// </summary>
        ValueTask RenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default);

        /// <summary>
        /// Canonicalize a path to an absolute one.
        /// </summary>
        ValueTask<String> RealPathAsync(String Path, CancellationToken CancellationToken = default);

        /// <summary>
        /// Flush everything written through a handle to stable storage — what the client asked for with
        /// <c>fsync@openssh.com</c>.
        ///
        /// <para>
        /// The default does nothing, which is the honest answer for a store that has no cache between
        /// the write and its durability (an in-memory file system has nowhere to flush *to*). An
        /// implementation backed by an operating-system file **must** override this: a buffered write
        /// that returned successfully is not yet on the disk, and answering the request without acting
        /// on it turns the one call whose entire purpose is a durability guarantee into a lie.
        /// </para>
        /// </summary>
        ValueTask FlushAsync(String Handle, CancellationToken CancellationToken = default)
            => ValueTask.CompletedTask;

    }

}
