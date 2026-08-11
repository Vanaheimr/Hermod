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

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP
{

    /// <summary>
    /// An SFTP (version 3) client over a channel duplex: upload, download, directory listing and the common
    /// file-management operations. A background reader correlates replies to requests by request-id, so many
    /// requests may be outstanding at once — <see cref="UploadAsync"/> and <see cref="DownloadAsync"/>
    /// pipeline their WRITE/READ requests for throughput, and <see cref="SftpFileStream"/> can stream freely.
    /// </summary>
    public sealed class SftpClient : IAsyncDisposable
    {

        #region Data

        private const Int32 TransferChunk         = 30 * 1024;   // stay under the 32 KiB channel packet
        private const Int32 MaxOutstanding        = 16;          // pipelining window (requests in flight)

        private readonly ISftpDuplex                                         channel;
        private readonly CancellationTokenSource                             cts       = new ();
        private readonly SemaphoreSlim                                       sendGate  = new (1, 1);
        private readonly ConcurrentDictionary<UInt32, TaskCompletionSource<Byte[]>>  pending = new ();
        private readonly Task                                                receiveLoop;
        private Int32                                                        requestIdSeed;

        #endregion

        #region Properties

        /// <summary>The extensions the server advertised in its SSH_FXP_VERSION (name → data).</summary>
        public IReadOnlyDictionary<String, String> ServerExtensions { get; }

        /// <summary>Whether the server advertised the named extension.</summary>
        public Boolean Supports(String Extension) => ServerExtensions.ContainsKey(Extension);

        #endregion

        #region Constructor(s)

        private SftpClient(ISftpDuplex Channel, IReadOnlyDictionary<String, String> ServerExtensions)
        {
            this.channel           = Channel;
            this.ServerExtensions  = ServerExtensions;
            this.receiveLoop       = Task.Run(ReceiveLoopAsync);
        }

        #endregion


        #region (static) OpenAsync(Transport, CancellationToken)

        /// <summary>Open the <c>sftp</c> subsystem on a single-channel transport and negotiate the protocol version.</summary>
        public static async ValueTask<SftpClient> OpenAsync(SshTransport Transport, CancellationToken CancellationToken = default)
            => await OpenAsync(await SshConnection.OpenSubsystemAsync(Transport, "sftp", CancellationToken).ConfigureAwait(false), CancellationToken).ConfigureAwait(false);

        /// <summary>Run the SFTP client over an already-established duplex channel (e.g. a multiplexed <c>sftp</c> subsystem channel).</summary>
        public static async ValueTask<SftpClient> OpenAsync(ISftpDuplex channel, CancellationToken CancellationToken = default)
        {

            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SftpPacketType.Init);
            w.WriteUInt32(SftpVersion.Three);
            await SftpServer.SendAsync(channel, abw.WrittenSpan.ToArray(), CancellationToken).ConfigureAwait(false);

            // INIT → VERSION is handled synchronously, before the background reader takes over the channel.
            var version = await SftpServer.ReadPacketAsync(channel, CancellationToken).ConfigureAwait(false)
                          ?? throw new SshWireException("The SFTP server closed the channel before SSH_FXP_VERSION.");
            if ((SftpPacketType) version[0] != SftpPacketType.Version)
                throw new SshWireException("Expected SSH_FXP_VERSION.");

            // Parse the advertised extension name/data pairs that follow the version word.
            var extensions = new Dictionary<String, String>(StringComparer.Ordinal);
            var vReader    = new SshPacketReader(version);
            vReader.ReadByte(); vReader.ReadUInt32();   // type + version
            while (vReader.Position < version.Length)
            {
                var name = vReader.ReadString();
                var data = vReader.ReadString();
                extensions[name] = data;
            }

            return new SftpClient(channel, extensions);

        }

        #endregion


        #region UploadAsync / DownloadAsync (pipelined)

        /// <summary>
        /// Upload bytes to a remote path (creating/truncating it), pipelining the WRITE requests.
        /// </summary>
        /// <param name="SyncToDisk">
        /// Ask the server to flush the file to stable storage before the handle is closed
        /// (<c>fsync@openssh.com</c>). Off by default because it costs a round trip and a real disk
        /// flush; worth it when the upload must survive the power going out a second later — writing
        /// firmware to a device is the case this exists for. Throws if the server does not offer the
        /// extension, rather than returning as if it had.
        /// </param>
        public async ValueTask UploadAsync(String             RemotePath,
                                           Byte[]             Content,
                                           CancellationToken  CancellationToken = default,
                                           Boolean            SyncToDisk        = false)
        {

            // Checked before the first byte goes out: a caller who demanded durability is better served by
            // an upload that never happened than by one that finished and cannot be confirmed.
            if (SyncToDisk && !Supports("fsync@openssh.com"))
                throw new SftpException(SftpStatusCode.OpUnsupported,
                                        "SyncToDisk was requested, but the server does not offer 'fsync@openssh.com'.");

            var handle   = await OpenFileAsync(RemotePath, SftpOpenFlags.Create | SftpOpenFlags.Write | SftpOpenFlags.Truncate, CancellationToken).ConfigureAwait(false);
            var inflight = new Queue<Task>();

            try
            {
                for (var offset = 0; offset < Content.Length; offset += TransferChunk)
                {
                    var chunk = Content.AsMemory(offset, Math.Min(TransferChunk, Content.Length - offset));
                    inflight.Enqueue(WriteAsync(handle, offset, chunk, CancellationToken).AsTask());
                    if (inflight.Count >= MaxOutstanding)
                        await inflight.Dequeue().ConfigureAwait(false);
                }

                while (inflight.Count > 0)
                    await inflight.Dequeue().ConfigureAwait(false);

                // After the last write is acknowledged, and before the close: a flush of a handle the
                // server has already closed would be answered by nothing at all.
                if (SyncToDisk)
                    await FsyncAsync(handle, CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await ObserveAsync(inflight).ConfigureAwait(false);   // don't leak faults from in-flight writes
                throw;
            }
            finally
            {
                await CloseAsync(handle, CancellationToken).ConfigureAwait(false);
            }

        }

        /// <summary>Download a remote file's contents, pipelining the READ requests across its length.</summary>
        public async ValueTask<Byte[]> DownloadAsync(String RemotePath, CancellationToken CancellationToken = default)
        {

            var size   = (await StatAsync(RemotePath, CancellationToken).ConfigureAwait(false)).Size ?? -1;
            var handle = await OpenFileAsync(RemotePath, SftpOpenFlags.Read, CancellationToken).ConfigureAwait(false);

            try
            {

                if (size < 0)
                    return await SequentialDownloadAsync(handle, CancellationToken).ConfigureAwait(false);

                var output    = new Byte[size];
                var inflight  = new Queue<(Int64 Offset, Int32 Length, Task<Byte[]> Task)>();
                var nextIssue = 0L;

                void IssueMore()
                {
                    while (inflight.Count < MaxOutstanding && nextIssue < size)
                    {
                        var len = (Int32) Math.Min(TransferChunk, size - nextIssue);
                        var off = nextIssue;
                        inflight.Enqueue((off, len, ReadAsync(handle, off, len, CancellationToken).AsTask()));
                        nextIssue += len;
                    }
                }

                IssueMore();

                while (inflight.Count > 0)
                {
                    var (offset, length, task) = inflight.Dequeue();
                    var data = await task.ConfigureAwait(false);

                    if (data.Length == 0)
                        break;   // EOF earlier than the stat size (file shrank) — stop

                    data.CopyTo(output, offset);

                    // A short read leaves a gap — re-issue the remainder (offset-based reads are independent).
                    if (data.Length < length)
                        inflight.Enqueue((offset + data.Length, length - data.Length,
                                          ReadAsync(handle, offset + data.Length, length - data.Length, CancellationToken).AsTask()));

                    IssueMore();
                }

                return output;

            }
            finally
            {
                await CloseAsync(handle, CancellationToken).ConfigureAwait(false);
            }

        }

        private async ValueTask<Byte[]> SequentialDownloadAsync(String Handle, CancellationToken CancellationToken)
        {
            var output = new ArrayBufferWriter<Byte>();
            var offset = 0L;
            while (true)
            {
                var data = await ReadAsync(Handle, offset, TransferChunk, CancellationToken).ConfigureAwait(false);
                if (data.Length == 0)
                    break;
                output.Write(data);
                offset += data.Length;
            }
            return output.WrittenSpan.ToArray();
        }

        private static async ValueTask ObserveAsync(Queue<Task> Inflight)
        {
            while (Inflight.Count > 0)
                try { await Inflight.Dequeue().ConfigureAwait(false); } catch { }
        }

        #endregion

        #region ListDirectoryAsync(RemotePath)

        /// <summary>List a remote directory (excluding <c>.</c> and <c>..</c>).</summary>
        public async ValueTask<IReadOnlyList<SftpDirectoryEntry>> ListDirectoryAsync(String RemotePath, CancellationToken CancellationToken = default)
        {

            var handle   = await OpenDirectoryAsync(RemotePath, CancellationToken).ConfigureAwait(false);
            var entries  = new List<SftpDirectoryEntry>();

            try
            {
                while (true)
                {
                    var batch = await ReadDirectoryAsync(handle, CancellationToken).ConfigureAwait(false);
                    if (batch.Count == 0)
                        break;
                    entries.AddRange(batch);
                }
            }
            finally
            {
                await CloseAsync(handle, CancellationToken).ConfigureAwait(false);
            }

            return entries.Where(e => e.Name is not "." and not "..").ToList();

        }

        #endregion


        #region file-management operations

        /// <summary>Get the attributes of a remote path.</summary>
        public async ValueTask<SftpFileAttributes> StatAsync(String RemotePath, CancellationToken CancellationToken = default)
        {
            var response = await RoundtripAsync(SftpPacketType.Stat, (ref SshPacketWriter w) => w.WriteString(RemotePath), CancellationToken).ConfigureAwait(false);
            EnsureNotStatusError(response);
            var reader = new SshPacketReader(response); reader.ReadByte(); reader.ReadUInt32();
            return SftpFileAttributes.Decode(ref reader);
        }

        /// <summary>Create a remote directory.</summary>
        public ValueTask MakeDirectoryAsync(String RemotePath, CancellationToken CancellationToken = default)
            => ExpectOkAsync(SftpPacketType.MkDir, (ref SshPacketWriter w) => { w.WriteString(RemotePath); SftpFileAttributes.Directory().Encode(ref w); }, CancellationToken);

        /// <summary>Remove a remote file.</summary>
        public ValueTask RemoveAsync(String RemotePath, CancellationToken CancellationToken = default)
            => ExpectOkAsync(SftpPacketType.Remove, (ref SshPacketWriter w) => w.WriteString(RemotePath), CancellationToken);

        /// <summary>Remove a remote directory.</summary>
        public ValueTask RemoveDirectoryAsync(String RemotePath, CancellationToken CancellationToken = default)
            => ExpectOkAsync(SftpPacketType.RmDir, (ref SshPacketWriter w) => w.WriteString(RemotePath), CancellationToken);

        /// <summary>Rename a remote file or directory.</summary>
        public ValueTask RenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default)
            => ExpectOkAsync(SftpPacketType.Rename, (ref SshPacketWriter w) => { w.WriteString(OldPath); w.WriteString(NewPath); }, CancellationToken);

        /// <summary>Atomically rename with replace semantics via <c>posix-rename@openssh.com</c>.</summary>
        public ValueTask PosixRenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default)
            => ExpectOkAsync(SftpPacketType.Extended, (ref SshPacketWriter w) => { w.WriteString("posix-rename@openssh.com"); w.WriteString(OldPath); w.WriteString(NewPath); }, CancellationToken);

        /// <summary>
        /// Ask the server to flush an open handle to stable storage via <c>fsync@openssh.com</c>.
        ///
        /// <para>
        /// A server that never advertised the extension is refused here rather than asked: silently
        /// skipping the request would hand the caller the appearance of a durability guarantee, which is
        /// the one thing an fsync must never do. Check <see cref="Supports"/> when the server is unknown.
        /// </para>
        /// </summary>
        internal ValueTask FsyncAsync(String Handle, CancellationToken CancellationToken = default)
        {

            if (!Supports("fsync@openssh.com"))
                throw new SftpException(SftpStatusCode.OpUnsupported,
                                        "The server does not offer 'fsync@openssh.com', so it cannot confirm the data reached stable storage.");

            return ExpectOkAsync(SftpPacketType.Extended,
                                 (ref SshPacketWriter w) => { w.WriteString("fsync@openssh.com"); w.WriteString(Handle); },
                                 CancellationToken);

        }

        /// <summary>Query the server's protocol limits via <c>limits@openssh.com</c>.</summary>
        public async ValueTask<SftpProtocolLimits> LimitsAsync(CancellationToken CancellationToken = default)
        {
            var response = await RoundtripAsync(SftpPacketType.Extended, (ref SshPacketWriter w) => w.WriteString("limits@openssh.com"), CancellationToken).ConfigureAwait(false);
            EnsureNotStatusError(response);
            var reader = new SshPacketReader(response); reader.ReadByte(); reader.ReadUInt32();
            return new SftpProtocolLimits(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
        }

        /// <summary>Query file-system statistics via <c>statvfs@openssh.com</c> (we surface the session quota as free space).</summary>
        public async ValueTask<SftpFileSystemStats> StatVfsAsync(String RemotePath, CancellationToken CancellationToken = default)
        {
            var response = await RoundtripAsync(SftpPacketType.Extended, (ref SshPacketWriter w) => { w.WriteString("statvfs@openssh.com"); w.WriteString(RemotePath); }, CancellationToken).ConfigureAwait(false);
            EnsureNotStatusError(response);
            var reader = new SshPacketReader(response); reader.ReadByte(); reader.ReadUInt32();
            return new SftpFileSystemStats(
                reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(),
                reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(),
                reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
        }

        #endregion

        #region OpenFileStreamAsync(RemotePath, Flags, CancellationToken)

        /// <summary>
        /// Open a remote file as a seekable <see cref="SftpFileStream"/> — read and/or write bytes at
        /// arbitrary offsets without loading the whole file into memory.
        /// </summary>
        public async ValueTask<SftpFileStream> OpenFileStreamAsync(String             RemotePath,
                                                                   SftpOpenFlags      Flags,
                                                                   CancellationToken  CancellationToken = default)
        {
            var writable = (Flags & (SftpOpenFlags.Write | SftpOpenFlags.Append | SftpOpenFlags.Create | SftpOpenFlags.Truncate)) != 0;
            var readable = Flags.HasFlag(SftpOpenFlags.Read) || !writable;
            var initial  = Flags.HasFlag(SftpOpenFlags.Create) || Flags.HasFlag(SftpOpenFlags.Truncate)
                               ? 0L
                               : (await StatAsync(RemotePath, CancellationToken).ConfigureAwait(false)).Size ?? 0L;

            var handle = await OpenFileAsync(RemotePath, Flags, CancellationToken).ConfigureAwait(false);
            return new SftpFileStream(this, handle, readable, writable, initial);
        }

        #endregion


        #region (internal) file primitives — also used by SftpFileStream

        internal async ValueTask<String> OpenFileAsync(String Path, SftpOpenFlags Flags, CancellationToken CancellationToken)
        {
            var response = await RoundtripAsync(SftpPacketType.Open, (ref SshPacketWriter w) => { w.WriteString(Path); w.WriteUInt32((UInt32) Flags); SftpFileAttributes.File(0).Encode(ref w); }, CancellationToken).ConfigureAwait(false);
            return ReadHandle(response);
        }

        internal async ValueTask<Byte[]> ReadAsync(String Handle, Int64 Offset, Int32 Length, CancellationToken CancellationToken)
        {
            var response = await RoundtripAsync(SftpPacketType.Read, (ref SshPacketWriter w) => { w.WriteString(Handle); w.WriteUInt64((UInt64) Offset); w.WriteUInt32((UInt32) Length); }, CancellationToken).ConfigureAwait(false);
            if ((SftpPacketType) response[0] == SftpPacketType.Status)
                return [];   // EOF (or an error surfaced as empty here)
            var reader = new SshPacketReader(response); reader.ReadByte(); reader.ReadUInt32();
            return reader.ReadBinaryString();
        }

        internal ValueTask WriteAsync(String Handle, Int64 Offset, ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken)
            // The body-writer runs synchronously inside RoundtripAsync, before the first await, so the
            // caller's buffer can be written straight through — copying it here cost a full chunk per
            // WRITE and was the largest single overhead on the upload path.
            => ExpectOkAsync(SftpPacketType.Write,
                             (ref SshPacketWriter w) => { w.WriteString(Handle); w.WriteUInt64((UInt64) Offset); w.WriteBinaryString(Data.Span); },
                             CancellationToken);

        internal ValueTask CloseAsync(String Handle, CancellationToken CancellationToken)
            => ExpectOkAsync(SftpPacketType.Close, (ref SshPacketWriter w) => w.WriteString(Handle), CancellationToken);

        private async ValueTask<String> OpenDirectoryAsync(String Path, CancellationToken CancellationToken)
        {
            var response = await RoundtripAsync(SftpPacketType.OpenDir, (ref SshPacketWriter w) => w.WriteString(Path), CancellationToken).ConfigureAwait(false);
            return ReadHandle(response);
        }

        private async ValueTask<IReadOnlyList<SftpDirectoryEntry>> ReadDirectoryAsync(String Handle, CancellationToken CancellationToken)
        {
            var response = await RoundtripAsync(SftpPacketType.ReadDir, (ref SshPacketWriter w) => w.WriteString(Handle), CancellationToken).ConfigureAwait(false);
            if ((SftpPacketType) response[0] == SftpPacketType.Status)
                return [];   // EOF

            var reader = new SshPacketReader(response); reader.ReadByte(); reader.ReadUInt32();
            var count  = reader.ReadUInt32();
            var result = new List<SftpDirectoryEntry>((Int32) count);
            for (var i = 0U; i < count; i++)
            {
                var name = reader.ReadString();
                reader.ReadString();   // longname
                var attrs = SftpFileAttributes.Decode(ref reader);
                result.Add(new SftpDirectoryEntry(name, attrs));
            }
            return result;
        }

        #endregion

        #region (private) request/response correlation

        private delegate void WriteBody(ref SshPacketWriter Writer);

        // Send a request (a body-writer that must not await) and await the reply matched by request-id.
        private async ValueTask<Byte[]> RoundtripAsync(SftpPacketType Type, WriteBody Write, CancellationToken CancellationToken)
        {

            var id  = unchecked((UInt32) System.Threading.Interlocked.Increment(ref requestIdSeed));
            var tcs = new TaskCompletionSource<Byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = tcs;

            var abw = new ArrayBufferWriter<Byte>();
            var w   = new SshPacketWriter(abw);
            w.WriteByte((Byte) Type);
            w.WriteUInt32(id);
            Write(ref w);

            await sendGate.WaitAsync(CancellationToken).ConfigureAwait(false);
            try     { await SftpServer.SendAsync(channel, abw.WrittenMemory, CancellationToken).ConfigureAwait(false); }
            finally { sendGate.Release(); }

            await using (CancellationToken.Register(static state => ((TaskCompletionSource<Byte[]>) state!).TrySetCanceled(), tcs).ConfigureAwait(false))
                return await tcs.Task.ConfigureAwait(false);

        }

        private async ValueTask ExpectOkAsync(SftpPacketType Type, WriteBody Write, CancellationToken CancellationToken)
        {
            var response = await RoundtripAsync(Type, Write, CancellationToken).ConfigureAwait(false);
            EnsureNotStatusError(response);
        }

        // The single background reader: dispatch every reply to its waiting request by id.
        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (true)
                {
                    var packet = await SftpServer.ReadPacketAsync(channel, cts.Token).ConfigureAwait(false);
                    if (packet is null)
                    {
                        FailAllPending(new SshChannelClosedException());
                        return;
                    }

                    var id = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(1, 4));
                    if (pending.TryRemove(id, out var tcs))
                        tcs.TrySetResult(packet);
                    // else: an unsolicited or already-cancelled reply — ignore.
                }
            }
            catch (Exception exception)
            {
                FailAllPending(exception);
            }
        }

        private void FailAllPending(Exception Exception)
        {
            foreach (var id in pending.Keys)
                if (pending.TryRemove(id, out var tcs))
                    tcs.TrySetException(Exception);
        }

        private static String ReadHandle(Byte[] Response)
        {
            EnsureNotStatusError(Response);
            var reader = new SshPacketReader(Response); reader.ReadByte(); reader.ReadUInt32();
            return reader.ReadString();
        }

        private static void EnsureNotStatusError(Byte[] Response)
        {
            if ((SftpPacketType) Response[0] != SftpPacketType.Status)
                return;
            var reader = new SshPacketReader(Response); reader.ReadByte(); reader.ReadUInt32();
            var code   = (SftpStatusCode) reader.ReadUInt32();
            if (code != SftpStatusCode.Ok)
                throw new SftpException(code, reader.ReadString());
        }

        #endregion

        #region DisposeAsync()

        /// <summary>Close the SFTP channel and stop the background reader.</summary>
        public async ValueTask DisposeAsync()
        {
            try { await channel.CloseAsync().ConfigureAwait(false); } catch { }
            await cts.CancelAsync().ConfigureAwait(false);
            try { await receiveLoop.ConfigureAwait(false); } catch { }
            FailAllPending(new SshChannelClosedException());
            sendGate.Dispose();
            cts.Dispose();
        }

        #endregion

    }

}
