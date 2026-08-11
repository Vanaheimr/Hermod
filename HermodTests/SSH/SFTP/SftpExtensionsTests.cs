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
using System.Text;
using System.Threading.Channels;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M7 SFTP OpenSSH extensions, end-to-end: the server advertises them in VERSION and answers
    /// <c>limits@</c>, <c>statvfs@</c> (surfacing the session quota as free space) and
    /// <c>posix-rename@openssh.com</c>.
    /// </summary>
    [TestFixture]
    public class SftpExtensionsTests
    {

        private static async Task<(SftpClient Sftp, SshTransport Client, Task Server)> ConnectAsync(ISftpFileSystem FileSystem, SftpLimits? Limits, CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var server = Task.Run(async () =>
            {
                try
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                    var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                    await SftpServer.ServeAsync(duplex, FileSystem, Limits: Limits, CancellationToken: CancellationToken);
                }
                catch { }
            }, CancellationToken);

            var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);
            var sftp = await SftpClient.OpenAsync(client, CancellationToken);
            return (sftp, client, server);

        }


        #region Extensions_AreAdvertised_AndLimitsReported

        [Test]
        [CancelAfter(20000)]
        public async Task Extensions_AreAdvertised_AndLimitsReported(CancellationToken CancellationToken)
        {

            var (sftp, client, server) = await ConnectAsync(new InMemorySftpFileSystem(), new SftpLimits { MaxFileCount = 42 }, CancellationToken);

            var limits = await sftp.LimitsAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(sftp.Supports("posix-rename@openssh.com"), Is.True);
                Assert.That(sftp.Supports("statvfs@openssh.com"),      Is.True);
                Assert.That(sftp.Supports("limits@openssh.com"),       Is.True);
                Assert.That(sftp.Supports("fsync@openssh.com"),        Is.True);
                // fstatvfs was answered by the dispatcher but missing from VERSION, so a peer that
                // honours the advertisement — the only thing it can go by — would never have asked.
                Assert.That(sftp.Supports("fstatvfs@openssh.com"),     Is.True);
                Assert.That(limits.MaxWriteLength,  Is.GreaterThan(0));
                Assert.That(limits.MaxOpenHandles,  Is.EqualTo(42), "open-handle limit reflects the file-count quota");
            });

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion

        #region StatVfs_ReportsQuotaAsFreeSpace

        [Test]
        [CancelAfter(20000)]
        public async Task StatVfs_ReportsQuotaAsFreeSpace(CancellationToken CancellationToken)
        {

            var (sftp, client, server) = await ConnectAsync(new InMemorySftpFileSystem(), new SftpLimits { MaxBytesPerSession = 1_000_000 }, CancellationToken);

            var before = await sftp.StatVfsAsync("/", CancellationToken);
            await sftp.UploadAsync("/a.bin", new Byte[400_000], CancellationToken);
            var after  = await sftp.StatVfsAsync("/", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(before.TotalBytes,     Is.EqualTo(1_000_000UL));
                Assert.That(before.AvailableBytes, Is.EqualTo(1_000_000UL));
                // After a 400 KB upload, ~600 KB of quota remains.
                Assert.That(after.AvailableBytes,  Is.EqualTo(600_000UL));
                Assert.That(after.MaxNameLength,   Is.EqualTo(255UL));
            });

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion

        #region PosixRename_ReplacesExistingTarget

        [Test]
        [CancelAfter(20000)]
        public async Task PosixRename_ReplacesExistingTarget(CancellationToken CancellationToken)
        {

            var fs = new InMemorySftpFileSystem();
            var (sftp, client, server) = await ConnectAsync(fs, null, CancellationToken);

            await sftp.UploadAsync("/new.txt", Encoding.UTF8.GetBytes("fresh"), CancellationToken);
            await sftp.UploadAsync("/live.txt", Encoding.UTF8.GetBytes("stale"), CancellationToken);

            // posix-rename replaces the existing target atomically (plain rename would fail on a busy store).
            await sftp.PosixRenameAsync("/new.txt", "/live.txt", CancellationToken);

            var content = await sftp.DownloadAsync("/live.txt", CancellationToken);
            var listing = await sftp.ListDirectoryAsync("/", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(Encoding.UTF8.GetString(content), Is.EqualTo("fresh"));
                Assert.That(listing.Select(e => e.Name), Does.Not.Contain("new.txt"));
            });

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion

        #region Fsync_ReachesTheFileSystem

        /// <summary>
        /// An <c>fsync@openssh.com</c> request must arrive at the file system, on the handle it names.
        ///
        /// <para>
        /// No test can prove durability — that needs the power to go out — so this proves the thing that
        /// was actually broken: the server used to answer OK without asking the store to flush anything,
        /// which is the failure mode fsync exists to prevent. The spy records what the store was told.
        /// </para>
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Fsync_ReachesTheFileSystem(CancellationToken CancellationToken)
        {

            var spy = new FlushRecordingFileSystem(new InMemorySftpFileSystem());
            var (sftp, client, server) = await ConnectAsync(spy, null, CancellationToken);

            await using (var stream = await sftp.OpenFileStreamAsync("/firmware.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("payload"), CancellationToken);
                Assert.That(spy.FlushedHandles, Is.Empty, "writing alone must not flush — that is what makes the explicit call meaningful");

                await stream.SyncToDiskAsync(CancellationToken);
            }

            Assert.That(spy.FlushedHandles, Has.Count.EqualTo(1), "the fsync request reached the file system exactly once");

            // And the same through the upload convenience, which flushes before it closes the handle.
            await sftp.UploadAsync("/second.bin", [1, 2, 3], CancellationToken, SyncToDisk: true);
            Assert.That(spy.FlushedHandles, Has.Count.EqualTo(2));

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion

        #region Fsync_AgainstAServerWithoutTheExtension_Throws

        /// <summary>
        /// A server that never advertised <c>fsync@openssh.com</c> must produce an error, not a silent
        /// success: a durability request that cannot be honoured is the one case where returning normally
        /// is worse than throwing, because the caller would believe a guarantee it never received.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Fsync_AgainstAServerWithoutTheExtension_Throws(CancellationToken CancellationToken)
        {

            // A minimal peer that answers INIT with a VERSION carrying no extensions at all.
            var (ours, theirs) = BarePipe.CreatePair();
            var peer = Task.Run(async () =>
            {
                var length  = await theirs.ReadExactAsync(4, CancellationToken);
                var payload = await theirs.ReadExactAsync((Int32) BinaryPrimitives.ReadUInt32BigEndian(length), CancellationToken);
                Assert.That((SftpPacketType) payload[0], Is.EqualTo(SftpPacketType.Init));

                var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                w.WriteByte((Byte) SftpPacketType.Version);
                w.WriteUInt32(SftpVersion.Three);          // …and not a single extension pair
                await SftpServer.SendAsync(theirs, abw.WrittenSpan.ToArray(), CancellationToken);
            }, CancellationToken);

            var sftp = await SftpClient.OpenAsync(ours, CancellationToken);
            await peer;

            Assert.That(sftp.Supports("fsync@openssh.com"), Is.False);

            var direct = Assert.ThrowsAsync<SftpException>(async () => await sftp.FsyncAsync("handle-1", CancellationToken));
            Assert.That(direct!.Code, Is.EqualTo(SftpStatusCode.OpUnsupported));

            // The upload path refuses up front — before a single byte is written, not after.
            var upload = Assert.ThrowsAsync<SftpException>(async () => await sftp.UploadAsync("/x.bin", [1], CancellationToken, SyncToDisk: true));
            Assert.That(upload!.Code, Is.EqualTo(SftpStatusCode.OpUnsupported));

            await sftp.DisposeAsync();

        }

        #endregion


        #region CopyData_CopiesServerSide_WithoutTheBytesCrossingTheWire

        /// <summary>
        /// <c>copy-data</c> end to end: a whole file, and a byte range placed at an offset in an existing
        /// destination. The interesting part is what is <i>not</i> in the test — no download and no upload,
        /// so the payload never travels; the client only ever sends two handles and three numbers.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task CopyData_CopiesServerSide_WithoutTheBytesCrossingTheWire(CancellationToken CancellationToken)
        {

            var fs = new InMemorySftpFileSystem();
            var (sftp, client, server) = await ConnectAsync(fs, null, CancellationToken);

            var payload = Encoding.UTF8.GetBytes("firmware-image-v7-contents");
            await sftp.UploadAsync("/source.bin", payload, CancellationToken);

            Assert.That(sftp.Supports("copy-data"), Is.True);

            // Whole file: length 0 means "to the end".
            await sftp.CopyAsync("/source.bin", "/copy.bin", CancellationToken);
            Assert.That(await sftp.DownloadAsync("/copy.bin", CancellationToken), Is.EqualTo(payload));

            // A range: 8 bytes from offset 9 ("image-v7"[..] → "image-v7" starts at 9).
            await sftp.CopyAsync("/source.bin", "/range.bin", CancellationToken, Length: 8, SourceOffset: 9);
            Assert.That(Encoding.UTF8.GetString(await sftp.DownloadAsync("/range.bin", CancellationToken)),
                        Is.EqualTo("image-v7"));

            // The source is untouched, and both copies are independent files.
            Assert.That(await sftp.DownloadAsync("/source.bin", CancellationToken), Is.EqualTo(payload));

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion

        #region CopyData_IsMeteredByTheQuota

        /// <summary>
        /// A server-side copy must be charged to the session quota exactly like the upload it replaces.
        ///
        /// <para>
        /// This is the one operation that could otherwise walk around every limit a session has: the
        /// client pays no bandwidth for it, so an unmetered copy would let a 1 KB upload become a
        /// gigabyte on the server for free.
        /// </para>
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task CopyData_IsMeteredByTheQuota(CancellationToken CancellationToken)
        {

            var fs = new InMemorySftpFileSystem();
            // Room for the 10 KB upload and one copy of it, but not a second copy.
            var (sftp, client, server) = await ConnectAsync(fs, new SftpLimits { MaxBytesPerSession = 25_000 }, CancellationToken);

            await sftp.UploadAsync("/source.bin", new Byte[10_000], CancellationToken);
            await sftp.CopyAsync("/source.bin", "/first.bin", CancellationToken);

            // Driven through raw handles, so the state *after* the refusal can be inspected — which is
            // where the interesting mistake lives: the cleanup must undo the handle being written to,
            // and copy-data is the only request whose destination is not in the usual handle position.
            var source      = await sftp.OpenFileAsync("/source.bin", SftpOpenFlags.Read, CancellationToken);
            var destination = await sftp.OpenFileAsync("/second.bin", SftpOpenFlags.Create | SftpOpenFlags.Write | SftpOpenFlags.Truncate, CancellationToken);

            var refused = Assert.ThrowsAsync<SftpException>(async () =>
                await sftp.CopyDataAsync(source, 0, 0, destination, 0, CancellationToken));

            Assert.That(refused!.Code, Is.EqualTo(SftpStatusCode.Failure).Or.EqualTo(SftpStatusCode.PermissionDenied),
                        "the copy that would exceed the session quota must be refused");

            // The source survived: it was being read, not written, so the cleanup must not have touched it.
            var stillReadable = await sftp.ReadAsync(source, 0, 100, CancellationToken);
            Assert.That(stillReadable, Has.Length.EqualTo(100), "the read handle must survive a failed copy");
            await sftp.CloseAsync(source, CancellationToken);

            // What the quota already accounted for is intact, and the refused copy left nothing behind.
            Assert.That((await sftp.DownloadAsync("/first.bin", CancellationToken)).Length, Is.EqualTo(10_000));
            Assert.That((await sftp.ListDirectoryAsync("/", CancellationToken)).Select(e => e.Name),
                        Does.Not.Contain("second.bin"), "the half-created destination must not survive the overrun");

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion

        #region CopyData_RefusesTheSameHandleOnBothEnds

        /// <summary>
        /// Source and destination being the same handle is refused. OpenSSH permits it for non-overlapping
        /// ranges, but telling those apart needs a length that may be "until EOF" — and nothing in a file
        /// copy needs it, so the honest answer is a clean refusal rather than a best guess.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task CopyData_RefusesTheSameHandleOnBothEnds(CancellationToken CancellationToken)
        {

            var fs = new InMemorySftpFileSystem();
            var (sftp, client, server) = await ConnectAsync(fs, null, CancellationToken);

            await sftp.UploadAsync("/f.bin", Encoding.UTF8.GetBytes("0123456789"), CancellationToken);

            var handle = await sftp.OpenFileAsync("/f.bin", SftpOpenFlags.Read | SftpOpenFlags.Write, CancellationToken);

            var refused = Assert.ThrowsAsync<SftpException>(async () =>
                await sftp.CopyDataAsync(handle, 0, 4, handle, 6, CancellationToken));

            Assert.That(refused!.Code, Is.EqualTo(SftpStatusCode.OpUnsupported));

            await sftp.DisposeAsync();
            using (client) { }
            await server;

        }

        #endregion


        #region (private) test doubles

        /// <summary>Passes everything through, but records which handles were asked to flush.</summary>
        private sealed class FlushRecordingFileSystem(ISftpFileSystem Inner) : ISftpFileSystem
        {

            public List<String> FlushedHandles { get; } = [];

            public ValueTask FlushAsync(String Handle, CancellationToken CancellationToken = default)
            {
                FlushedHandles.Add(Handle);
                return Inner.FlushAsync(Handle, CancellationToken);
            }

            public ValueTask<String> OpenAsync(String Path, SftpOpenFlags Flags, CancellationToken CancellationToken = default) => Inner.OpenAsync(Path, Flags, CancellationToken);
            public ValueTask<Byte[]> ReadAsync(String Handle, Int64 Offset, Int32 Length, CancellationToken CancellationToken = default) => Inner.ReadAsync(Handle, Offset, Length, CancellationToken);
            public ValueTask WriteAsync(String Handle, Int64 Offset, ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default) => Inner.WriteAsync(Handle, Offset, Data, CancellationToken);
            public ValueTask CloseAsync(String Handle, CancellationToken CancellationToken = default) => Inner.CloseAsync(Handle, CancellationToken);
            public ValueTask<String> OpenDirectoryAsync(String Path, CancellationToken CancellationToken = default) => Inner.OpenDirectoryAsync(Path, CancellationToken);
            public ValueTask<IReadOnlyList<SftpDirectoryEntry>> ReadDirectoryAsync(String Handle, CancellationToken CancellationToken = default) => Inner.ReadDirectoryAsync(Handle, CancellationToken);
            public ValueTask<SftpFileAttributes> StatAsync(String Path, CancellationToken CancellationToken = default) => Inner.StatAsync(Path, CancellationToken);
            public ValueTask MakeDirectoryAsync(String Path, CancellationToken CancellationToken = default) => Inner.MakeDirectoryAsync(Path, CancellationToken);
            public ValueTask RemoveAsync(String Path, CancellationToken CancellationToken = default) => Inner.RemoveAsync(Path, CancellationToken);
            public ValueTask RemoveDirectoryAsync(String Path, CancellationToken CancellationToken = default) => Inner.RemoveDirectoryAsync(Path, CancellationToken);
            public ValueTask RenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default) => Inner.RenameAsync(OldPath, NewPath, CancellationToken);
            public ValueTask<String> RealPathAsync(String Path, CancellationToken CancellationToken = default) => Inner.RealPathAsync(Path, CancellationToken);

        }


        /// <summary>Two <see cref="ISftpDuplex"/> ends wired to each other in memory — no transport, no crypto.</summary>
        private sealed class BarePipe : ISftpDuplex
        {

            private readonly Channel<Byte[]>  inbox = Channel.CreateUnbounded<Byte[]>();
            private BarePipe                  peer  = default!;
            private Byte[]                    rest  = [];

            public static (BarePipe A, BarePipe B) CreatePair()
            {
                var a = new BarePipe(); var b = new BarePipe();
                a.peer = b; b.peer = a;
                return (a, b);
            }

            public async ValueTask<Byte[]?> TryReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
            {
                while (rest.Length < Count)
                {
                    if (!await inbox.Reader.WaitToReadAsync(CancellationToken))
                        return null;
                    rest = [.. rest, .. await inbox.Reader.ReadAsync(CancellationToken)];
                }
                var head = rest[..Count];
                rest = rest[Count..];
                return head;
            }

            public async ValueTask<Byte[]> ReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
                => await TryReadExactAsync(Count, CancellationToken) ?? throw new EndOfStreamException();

            public ValueTask SendAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            {
                peer.inbox.Writer.TryWrite(Data.ToArray());
                return ValueTask.CompletedTask;
            }

            public ValueTask CloseAsync(CancellationToken CancellationToken = default)
            {
                inbox.Writer.TryComplete();
                peer.inbox.Writer.TryComplete();
                return ValueTask.CompletedTask;
            }

        }

        #endregion

    }

}
