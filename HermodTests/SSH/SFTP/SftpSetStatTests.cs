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
using System.IO.Pipelines;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// SETSTAT / FSETSTAT: the attributes a client asks for are applied, or refused with a status that
    /// says why.
    ///
    /// <para>
    /// The server used to answer every SETSTAT with OK and change nothing. A <c>chmod</c>, a
    /// <c>sftp -p</c> upload that preserves its modification time, a truncation through the size
    /// attribute — all reported success to a client that then relied on it. And because the request
    /// classified as needing every permission, an upload-only session that does preserve times was
    /// refused outright.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SftpSetStatTests
    {

        #region (private) a loopback ISftpDuplex pair and a client on it

        private sealed class PipeDuplex : ISftpDuplex
        {

            private readonly PipeReader reader;
            private readonly PipeWriter writer;

            private PipeDuplex(PipeReader Reader, PipeWriter Writer)
            {
                this.reader = Reader;
                this.writer = Writer;
            }

            public static (ISftpDuplex Client, ISftpDuplex Server) CreatePair()
            {
                var clientToServer = new Pipe();
                var serverToClient = new Pipe();
                return (new PipeDuplex(serverToClient.Reader, clientToServer.Writer),
                        new PipeDuplex(clientToServer.Reader, serverToClient.Writer));
            }

            public async ValueTask<Byte[]?> TryReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
            {

                var buffer = new Byte[Count];
                var filled = 0;

                while (filled < Count)
                {

                    var result = await reader.ReadAsync(CancellationToken).ConfigureAwait(false);

                    if (result.Buffer.IsEmpty && result.IsCompleted)
                    {
                        reader.AdvanceTo(result.Buffer.Start);
                        return filled == 0 ? null : throw new EndOfStreamException();
                    }

                    var take = (Int32) Math.Min(Count - filled, result.Buffer.Length);
                    result.Buffer.Slice(0, take).CopyTo(buffer.AsSpan(filled));
                    filled += take;
                    reader.AdvanceTo(result.Buffer.GetPosition(take));

                }

                return buffer;

            }

            public async ValueTask<Byte[]> ReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
                => await TryReadExactAsync(Count, CancellationToken).ConfigureAwait(false)
                       ?? throw new EndOfStreamException();

            public async ValueTask SendAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
                => await writer.WriteAsync(Data, CancellationToken).ConfigureAwait(false);

            public ValueTask CloseAsync(CancellationToken CancellationToken = default)
            {
                writer.Complete();
                return ValueTask.CompletedTask;
            }

        }

        private static async Task<SftpClient> OpenAsync(ISftpFileSystem FileSystem, SshAccessProfile? Profile, CancellationToken CancellationToken)
        {

            var (client, server) = PipeDuplex.CreatePair();

            _ = Task.Run(async () => {
                try { await SftpServer.ServeAsync(server, FileSystem, Profile, CancellationToken: CancellationToken); }
                catch { /* torn down with the test */ }
            }, CancellationToken);

            return await SftpClient.OpenAsync(client, CancellationToken);

        }

        private static String NewRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "hermod_setstat_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(String Root)
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            try { Directory.Delete(Root, recursive: true); } catch { }
        }

        private static readonly DateTimeOffset Then = new (2001, 2, 3, 4, 5, 6, TimeSpan.Zero);

        #endregion


        #region SetStat_SetsTheModificationTimeAndTheSize

        /// <summary>
        /// What <c>sftp -p</c> sends after an upload, plus a truncation: both land on disk, and a STAT
        /// reports them back.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task SetStat_SetsTheModificationTimeAndTheSize(CancellationToken CancellationToken)
        {

            var root = NewRoot();

            try
            {

                await using var sftp = await OpenAsync(new LocalSftpFileSystem(root), null, CancellationToken);

                await sftp.UploadAsync("/a.bin", new Byte[10], CancellationToken);
                await sftp.SetAttributesAsync("/a.bin", new SftpFileAttributes { Size = 4, AccessTime = Then, ModifyTime = Then }, CancellationToken);

                var physical = new FileInfo(Path.Combine(root, "a.bin"));
                var stat     = await sftp.StatAsync("/a.bin", CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(physical.Length,                                       Is.EqualTo(4),                "truncated on disk");
                    Assert.That(new DateTimeOffset(physical.LastWriteTimeUtc, TimeSpan.Zero), Is.EqualTo(Then),   "mtime set on disk");
                    Assert.That(stat.Size,                                             Is.EqualTo(4));
                    Assert.That(stat.ModifyTime,                                       Is.EqualTo(Then),             "and STAT says so");
                });

            }
            finally
            {
                DeleteRoot(root);
            }

        }

        #endregion

        #region SetStat_AppliesPermissions_ButNoSpecialBits

        /// <summary>
        /// A <c>chmod</c> lands on disk and STAT reports it. On a POSIX system the rwx bits are applied and
        /// setuid / setgid / sticky are not — a client does not get to make the server's files setuid. On
        /// Windows the owner-write bit is the read-only attribute.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task SetStat_AppliesPermissions_ButNoSpecialBits(CancellationToken CancellationToken)
        {

            var root = NewRoot();

            try
            {

                await using var sftp = await OpenAsync(new LocalSftpFileSystem(root), null, CancellationToken);

                await sftp.UploadAsync("/a.bin", new Byte[10], CancellationToken);
                var physical = Path.Combine(root, "a.bin");

                if (OperatingSystem.IsWindows())
                {

                    await sftp.SetAttributesAsync("/a.bin", new SftpFileAttributes { Permissions = 0x124 /* 0444 */ }, CancellationToken);
                    var readOnly = new FileInfo(physical).IsReadOnly;
                    var stat     = await sftp.StatAsync("/a.bin", CancellationToken);

                    await sftp.SetAttributesAsync("/a.bin", new SftpFileAttributes { Permissions = 0x1A4 /* 0644 */ }, CancellationToken);
                    var writable = !new FileInfo(physical).IsReadOnly;

                    Assert.Multiple(() => {
                        Assert.That(readOnly,                  Is.True,           "0444 makes the file read-only");
                        Assert.That(stat.Permissions & 0x1FF,  Is.EqualTo(0x124), "and STAT reports 0444");
                        Assert.That(writable,                  Is.True,           "0644 makes it writable again");
                    });

                }
                else
                {

                    await sftp.SetAttributesAsync("/a.bin", new SftpFileAttributes { Permissions = 0x9ED /* 04755 */ }, CancellationToken);
                    var mode = (UInt32) File.GetUnixFileMode(physical);
                    var stat = await sftp.StatAsync("/a.bin", CancellationToken);

                    Assert.Multiple(() => {
                        Assert.That(mode,                      Is.EqualTo(0x1ED), "0755 applied, the setuid bit dropped");
                        Assert.That(stat.Permissions & 0xFFF,  Is.EqualTo(0x1ED), "and STAT reports what is on disk");
                    });

                }

            }
            finally
            {
                DeleteRoot(root);
            }

        }

        #endregion

        #region SetStat_RefusesOwnership_AndThenChangesNothing

        /// <summary>
        /// Ownership is not this server's to change. A request that asks for it together with a time is
        /// refused as a whole, and the time it also asked for is not applied either.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task SetStat_RefusesOwnership_AndThenChangesNothing(CancellationToken CancellationToken)
        {

            var root = NewRoot();

            try
            {

                await using var sftp = await OpenAsync(new LocalSftpFileSystem(root), null, CancellationToken);

                await sftp.UploadAsync("/a.bin", new Byte[10], CancellationToken);
                var before = File.GetLastWriteTimeUtc(Path.Combine(root, "a.bin"));

                var refused = await Assert.CatchAsync<SftpException>(async () =>
                                  await sftp.SetAttributesAsync("/a.bin", new SftpFileAttributes { UserId = 0, GroupId = 0, ModifyTime = Then }, CancellationToken));

                Assert.Multiple(() => {
                    Assert.That(refused?.Code,                                         Is.EqualTo(SftpStatusCode.OpUnsupported));
                    Assert.That(File.GetLastWriteTimeUtc(Path.Combine(root, "a.bin")), Is.EqualTo(before), "nothing of a refused request is applied");
                });

            }
            finally
            {
                DeleteRoot(root);
            }

        }

        #endregion

        #region SetStat_OnAFileSystemWithoutAttributes_IsRefused

        /// <summary>
        /// A file system that models no attributes says so, instead of the OK it used to give.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task SetStat_OnAFileSystemWithoutAttributes_IsRefused(CancellationToken CancellationToken)
        {

            await using var sftp = await OpenAsync(new InMemorySftpFileSystem(), null, CancellationToken);

            await sftp.UploadAsync("/a.bin", new Byte[10], CancellationToken);

            var refused = await Assert.CatchAsync<SftpException>(async () =>
                              await sftp.SetAttributesAsync("/a.bin", new SftpFileAttributes { ModifyTime = Then }, CancellationToken));

            Assert.That(refused?.Code, Is.EqualTo(SftpStatusCode.OpUnsupported));

        }

        #endregion

        #region SetStat_FollowsTheAccessProfile

        /// <summary>
        /// Setting attributes is writing. An upload-only session may set the time of what it uploaded —
        /// which <c>sftp -p</c> does, and which used to be refused because SETSTAT demanded every
        /// permission — and a download-only session may not touch anything.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task SetStat_FollowsTheAccessProfile(CancellationToken CancellationToken)
        {

            var root = NewRoot();

            try
            {

                await using (var upload = await OpenAsync(new LocalSftpFileSystem(root), SshAccessProfile.SftpUploadOnly, CancellationToken))
                {
                    await upload.UploadAsync("/a.bin", new Byte[10], CancellationToken);
                    await upload.SetAttributesAsync("/a.bin", new SftpFileAttributes { ModifyTime = Then }, CancellationToken);
                }

                Assert.That(new DateTimeOffset(File.GetLastWriteTimeUtc(Path.Combine(root, "a.bin")), TimeSpan.Zero), Is.EqualTo(Then),
                            "an upload-only session may preserve the time of its upload");

                await using var download = await OpenAsync(new LocalSftpFileSystem(root), SshAccessProfile.SftpDownloadOnly, CancellationToken);

                var refused = await Assert.CatchAsync<SftpException>(async () =>
                                  await download.SetAttributesAsync("/a.bin", new SftpFileAttributes { Size = 0 }, CancellationToken));

                Assert.Multiple(() => {
                    Assert.That(refused?.Code,                                   Is.EqualTo(SftpStatusCode.PermissionDenied));
                    Assert.That(new FileInfo(Path.Combine(root, "a.bin")).Length, Is.EqualTo(10), "a download-only session truncates nothing");
                });

            }
            finally
            {
                DeleteRoot(root);
            }

        }

        #endregion

    }

}
