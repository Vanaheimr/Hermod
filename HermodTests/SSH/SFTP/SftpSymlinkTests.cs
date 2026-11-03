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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Symbolic links over SFTP - SYMLINK, READLINK, LSTAT - against the root-jailed local file system,
    /// whose jail they must not open; and truncation through an open file stream.
    /// </summary>
    [TestFixture]
    public class SftpSymlinkTests
    {

        #region Data

        private String  sandbox  = "";
        private String  root     = "";
        private String  outside  = "";

        private static readonly Byte[] Content = Encoding.UTF8.GetBytes("what the link leads to\n");
        private static readonly Byte[] Secret  = Encoding.UTF8.GetBytes("beyond the root\n");

        #endregion

        #region Setup / Teardown

        [SetUp]
        public void MakeSandbox()
        {
            sandbox = Path.Combine(Path.GetTempPath(), "hermod_sftp_links_" + Guid.NewGuid().ToString("N"));
            root    = Path.Combine(sandbox, "root");
            outside = Path.Combine(sandbox, "outside");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            File.WriteAllBytes(Path.Combine(outside, "secret.txt"), Secret);
        }

        [TearDown]
        public void RemoveSandbox()
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }

        #endregion


        #region Symlink_IsMadeReadAndFollowed

        /// <summary>
        /// A relative and an absolute link to a file: LSTAT shows the link and STAT the file, a download
        /// through either reads the file, and a listing shows them as links. On disk both are relative,
        /// holding nothing of the server's own paths - and READLINK reads back what is on disk.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Symlink_IsMadeReadAndFollowed(CancellationToken CancellationToken)
        {

            await using var sftp = await ConnectAsync(new LocalSftpFileSystem(root), CancellationToken);

            await sftp.MakeDirectoryAsync("/data", CancellationToken);
            await sftp.UploadAsync("/data/a.txt", Content, CancellationToken);

            await sftp.CreateSymbolicLinkAsync("/relative.txt", "data/a.txt",  CancellationToken);
            await sftp.CreateSymbolicLinkAsync("/absolute.txt", "/data/a.txt", CancellationToken);

            var listing = await sftp.ListDirectoryAsync("/", CancellationToken);

            Assert.Multiple(async () => {

                Assert.That(await sftp.ReadLinkAsync("/relative.txt", CancellationToken), Is.EqualTo("data/a.txt"));
                Assert.That(await sftp.ReadLinkAsync("/absolute.txt", CancellationToken), Is.EqualTo("data/a.txt"), "stored relative to the link");

                Assert.That((await sftp.LStatAsync("/relative.txt", CancellationToken)).IsSymbolicLink, Is.True,  "LSTAT shows the link");
                Assert.That((await sftp.StatAsync ("/relative.txt", CancellationToken)).IsSymbolicLink, Is.False, "STAT shows the file");
                Assert.That((await sftp.StatAsync ("/relative.txt", CancellationToken)).Size,           Is.EqualTo(Content.Length));

                Assert.That(await sftp.DownloadAsync("/relative.txt", CancellationToken), Is.EqualTo(Content));
                Assert.That(await sftp.DownloadAsync("/absolute.txt", CancellationToken), Is.EqualTo(Content));

                Assert.That(listing.Single(e => e.Name == "relative.txt").Attributes.IsSymbolicLink, Is.True, "a listing shows a link as a link");
                Assert.That(listing.Single(e => e.Name == "data").Attributes.IsDirectory,             Is.True);

                foreach (var link in new[] { "relative.txt", "absolute.txt" })
                {
                    var stored = new FileInfo(Path.Combine(root, link)).LinkTarget!;
                    Assert.That(Path.IsPathRooted(stored), Is.False,                       $"{link} is stored relative");
                    Assert.That(stored,                    Does.Not.Contain(sandbox),       $"{link} holds no server path");
                }

            });

        }

        #endregion

        #region Symlink_ToADirectory_IsWalkedThrough

        /// <summary>
        /// A link to a directory is walked through, REALPATH names where it really leads, RMDIR refuses
        /// the link, and REMOVE takes the link away and leaves the directory.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Symlink_ToADirectory_IsWalkedThrough(CancellationToken CancellationToken)
        {

            await using var sftp = await ConnectAsync(new LocalSftpFileSystem(root), CancellationToken);

            await sftp.MakeDirectoryAsync("/logs", CancellationToken);
            await sftp.UploadAsync("/logs/boot.log", Content, CancellationToken);
            await sftp.CreateSymbolicLinkAsync("/current", "logs", CancellationToken);

            Assert.That(await sftp.DownloadAsync("/current/boot.log", CancellationToken), Is.EqualTo(Content));
            Assert.That(await sftp.RealPathAsync("/current/boot.log", CancellationToken), Is.EqualTo("/logs/boot.log"));

            var rmdir = await Assert.CatchAsync<SftpException>(async () => await sftp.RemoveDirectoryAsync("/current", CancellationToken));
            Assert.That(rmdir!.Code, Is.EqualTo(SftpStatusCode.Failure), "RMDIR does not go through a link");

            await sftp.RemoveAsync("/current", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(Directory.Exists(Path.Combine(root, "current")),        Is.False, "the link is gone");
                Assert.That(File.Exists(Path.Combine(root, "logs", "boot.log")),    Is.True,  "the directory stayed");
            });

        }

        #endregion

        #region Symlink_LeadingOutOfTheRoot_IsNotMade

        /// <summary>
        /// A client cannot make a link that leads out of the root, however it spells the way out.
        /// </summary>
        [TestCase("/../outside/secret.txt")]
        [TestCase("../outside/secret.txt")]
        [TestCase("../../../../../../../../etc/passwd")]
        [CancelAfter(20000)]
        public async Task Symlink_LeadingOutOfTheRoot_IsNotMade(String Target, CancellationToken CancellationToken)
        {

            await using var sftp = await ConnectAsync(new LocalSftpFileSystem(root), CancellationToken);

            var refused = await Assert.CatchAsync<SftpException>(async () => await sftp.CreateSymbolicLinkAsync("/escape", Target, CancellationToken));

            Assert.Multiple(() => {
                Assert.That(refused!.Code,                             Is.EqualTo(SftpStatusCode.PermissionDenied));
                Assert.That(Directory.EnumerateFileSystemEntries(root), Is.Empty, "nothing was made");
            });

        }

        #endregion

        #region ALinkLeadingOutOfTheRoot_ThatWasThereBefore_IsNotFollowed

        /// <summary>
        /// A link placed into the root by someone else, leading out of it - absolute or relative, to the
        /// directory or to the file: nothing goes through it, neither reading nor writing nor listing, and
        /// READLINK does not show where it leads. The link itself can still be looked at and removed.
        /// Before links were followed here, the operating system followed this one.
        /// </summary>
        [TestCase(true,  true)]
        [TestCase(true,  false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        [CancelAfter(20000)]
        public async Task ALinkLeadingOutOfTheRoot_ThatWasThereBefore_IsNotFollowed(Boolean ToTheDirectory, Boolean Absolute, CancellationToken CancellationToken)
        {

            var target = ToTheDirectory ? outside : Path.Combine(outside, "secret.txt");
            var stored = Absolute ? target : Path.GetRelativePath(root, target);

            if (ToTheDirectory)
                Directory.CreateSymbolicLink(Path.Combine(root, "escape"), stored);
            else
                File.CreateSymbolicLink(Path.Combine(root, "escape"), stored);

            await using var sftp = await ConnectAsync(new LocalSftpFileSystem(root), CancellationToken);

            var read     = ToTheDirectory ? "/escape/secret.txt" : "/escape";
            var write    = ToTheDirectory ? "/escape/planted.txt" : "/escape";

            var download = await Assert.CatchAsync<SftpException>(async () => await sftp.DownloadAsync(read, CancellationToken));
            var stat     = await Assert.CatchAsync<SftpException>(async () => await sftp.StatAsync(read, CancellationToken));
            var upload   = await Assert.CatchAsync<SftpException>(async () => await sftp.UploadAsync(write, Content, CancellationToken));
            var readLink = await Assert.CatchAsync<SftpException>(async () => await sftp.ReadLinkAsync("/escape", CancellationToken));

            Assert.Multiple(() => {
                Assert.That(download!.Code,  Is.EqualTo(SftpStatusCode.PermissionDenied), "no reading through it");
                Assert.That(stat!.Code,      Is.EqualTo(SftpStatusCode.PermissionDenied), "no looking through it");
                Assert.That(upload!.Code,    Is.EqualTo(SftpStatusCode.PermissionDenied), "no writing through it");
                Assert.That(readLink!.Code,  Is.EqualTo(SftpStatusCode.PermissionDenied), "where it leads is not shown");
                Assert.That(readLink.Message, Does.Not.Contain(sandbox));
                Assert.That(File.ReadAllBytes(Path.Combine(outside, "secret.txt")), Is.EqualTo(Secret), "the file beyond the root is untouched");
                Assert.That(File.Exists(Path.Combine(outside, "planted.txt")),      Is.False);
            });

            if (ToTheDirectory)
            {
                var list = await Assert.CatchAsync<SftpException>(async () => await sftp.ListDirectoryAsync("/escape", CancellationToken));
                Assert.That(list!.Code, Is.EqualTo(SftpStatusCode.PermissionDenied), "no listing through it");
            }

            Assert.That((await sftp.LStatAsync("/escape", CancellationToken)).IsSymbolicLink, Is.True, "the link itself can be looked at");

            await sftp.RemoveAsync("/escape", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(Directory.EnumerateFileSystemEntries(root),          Is.Empty, "the link is gone");
                Assert.That(File.Exists(Path.Combine(outside, "secret.txt")),    Is.True,  "what it led to is not");
            });

        }

        #endregion

        #region Symlinks_InALoop_AreGivenUp

        [Test]
        [CancelAfter(20000)]
        public async Task Symlinks_InALoop_AreGivenUp(CancellationToken CancellationToken)
        {

            await using var sftp = await ConnectAsync(new LocalSftpFileSystem(root), CancellationToken);

            await sftp.CreateSymbolicLinkAsync("/a", "b", CancellationToken);
            await sftp.CreateSymbolicLinkAsync("/b", "a", CancellationToken);

            var loop = await Assert.CatchAsync<SftpException>(async () => await sftp.StatAsync("/a", CancellationToken));

            Assert.Multiple(() => {
                Assert.That(loop!.Code,    Is.EqualTo(SftpStatusCode.Failure));
                Assert.That(loop.Message,  Does.Contain("symbolic links"));
            });

        }

        #endregion

        #region RenameAndRemove_ActOnTheLinkItself

        /// <summary>
        /// RENAME moves the link and REMOVE deletes it; the file it points to stays where it was.
        /// READLINK of a file, and a link over an existing name, are refused.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task RenameAndRemove_ActOnTheLinkItself(CancellationToken CancellationToken)
        {

            await using var sftp = await ConnectAsync(new LocalSftpFileSystem(root), CancellationToken);

            await sftp.UploadAsync("/file.txt", Content, CancellationToken);
            await sftp.CreateSymbolicLinkAsync("/link", "file.txt", CancellationToken);

            var notALink = await Assert.CatchAsync<SftpException>(async () => await sftp.ReadLinkAsync("/file.txt", CancellationToken));
            var taken    = await Assert.CatchAsync<SftpException>(async () => await sftp.CreateSymbolicLinkAsync("/file.txt", "link", CancellationToken));

            await sftp.RenameAsync("/link", "/moved", CancellationToken);

            Assert.That(await sftp.ReadLinkAsync("/moved", CancellationToken), Is.EqualTo("file.txt"));

            await sftp.RemoveAsync("/moved", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(notALink!.Code,                                          Is.EqualTo(SftpStatusCode.Failure));
                Assert.That(taken!.Code,                                             Is.EqualTo(SftpStatusCode.Failure));
                Assert.That(Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName), Is.EqualTo(new[] { "file.txt" }));
                Assert.That(File.ReadAllBytes(Path.Combine(root, "file.txt")),        Is.EqualTo(Content));
            });

        }

        #endregion

        #region Symlink_NeedsTheRightToCreate_AndCountsAgainstTheQuota

        /// <summary>
        /// A download-only session may read a link but not make one; a link counts against the file-count
        /// quota like a file.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Symlink_NeedsTheRightToCreate_AndCountsAgainstTheQuota(CancellationToken CancellationToken)
        {

            var fileSystem = new LocalSftpFileSystem(root);

            await using (var setup = await ConnectAsync(fileSystem, CancellationToken))
            {
                await setup.UploadAsync("/firmware.bin", Content, CancellationToken);
                await setup.CreateSymbolicLinkAsync("/latest", "firmware.bin", CancellationToken);
            }

            await using (var downloadOnly = await ConnectAsync(fileSystem, CancellationToken, SshAccessProfile.SftpDownloadOnly))
            {

                var make = await Assert.CatchAsync<SftpException>(async () => await downloadOnly.CreateSymbolicLinkAsync("/other", "firmware.bin", CancellationToken));

                Assert.Multiple(async () => {
                    Assert.That(await downloadOnly.ReadLinkAsync("/latest", CancellationToken), Is.EqualTo("firmware.bin"));
                    Assert.That(make!.Code, Is.EqualTo(SftpStatusCode.PermissionDenied));
                });

            }

            await using (var quota = await ConnectAsync(fileSystem, CancellationToken, Limits: new SftpLimits { MaxFileCount = 1 }))
            {

                await quota.CreateSymbolicLinkAsync("/one", "firmware.bin", CancellationToken);

                var two = await Assert.CatchAsync<SftpException>(async () => await quota.CreateSymbolicLinkAsync("/two", "firmware.bin", CancellationToken));

                Assert.That(two!.Code, Is.EqualTo(SftpStatusCode.Failure).Or.EqualTo(SftpStatusCode.PermissionDenied), two.Message);
                Assert.That(File.Exists(Path.Combine(root, "two")), Is.False);

            }

        }

        #endregion

        #region AFileSystemWithoutLinks_SaysSo

        /// <summary>
        /// The in-memory file system has no links: SYMLINK and READLINK are unsupported, and LSTAT answers
        /// as STAT, which is right where nothing can be a link.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task AFileSystemWithoutLinks_SaysSo(CancellationToken CancellationToken)
        {

            await using var sftp = await ConnectAsync(new InMemorySftpFileSystem(), CancellationToken);

            await sftp.UploadAsync("/file.txt", Content, CancellationToken);

            var make = await Assert.CatchAsync<SftpException>(async () => await sftp.CreateSymbolicLinkAsync("/link", "file.txt", CancellationToken));
            var read = await Assert.CatchAsync<SftpException>(async () => await sftp.ReadLinkAsync("/file.txt", CancellationToken));

            Assert.Multiple(async () => {
                Assert.That(make!.Code, Is.EqualTo(SftpStatusCode.OpUnsupported));
                Assert.That(read!.Code, Is.EqualTo(SftpStatusCode.OpUnsupported));
                Assert.That(await sftp.LStatAsync("/file.txt", CancellationToken), Is.EqualTo(await sftp.StatAsync("/file.txt", CancellationToken)));
            });

        }

        #endregion

        #region FileStream_SetLength_TruncatesAndExtends

        /// <summary>
        /// <see cref="SftpFileStream.SetLength"/> shortens and lengthens the remote file through its handle,
        /// and the position stays where it was.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task FileStream_SetLength_TruncatesAndExtends(CancellationToken CancellationToken)
        {

            await using var sftp = await ConnectAsync(new LocalSftpFileSystem(root), CancellationToken);

            await sftp.UploadAsync("/log.txt", Content, CancellationToken);

            await using (var stream = await sftp.OpenFileStreamAsync("/log.txt", SftpOpenFlags.Read | SftpOpenFlags.Write, CancellationToken))
            {

                stream.Position = 3;

                await stream.SetLengthAsync(4, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(stream.Length,                                          Is.EqualTo(4));
                    Assert.That(stream.Position,                                        Is.EqualTo(3));
                    Assert.That(ReadShared(Path.Combine(root, "log.txt")),              Is.EqualTo(Content[..4]));
                });

                stream.SetLength(10);

                Assert.Multiple(() => {
                    Assert.That(stream.Length,                                          Is.EqualTo(10));
                    Assert.That(ReadShared(Path.Combine(root, "log.txt")),              Is.EqualTo(Content[..4].Concat(new Byte[6]).ToArray()));
                });

            }

            await using (var readOnly = await sftp.OpenFileStreamAsync("/log.txt", SftpOpenFlags.Read, CancellationToken))
                Assert.Throws<NotSupportedException>(() => readOnly.SetLength(1), "a stream opened for reading does not truncate");

        }

        #endregion


        // The server still holds the file open for writing, so it is read sharing that.
        private static Byte[] ReadShared(String Physical)
        {
            using var file   = new FileStream(Physical, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var buffer = new MemoryStream();
            file.CopyTo(buffer);
            return buffer.ToArray();
        }

        #region (private) ConnectAsync(FileSystem, CancellationToken, Profile = null, Limits = null)

        private static async Task<SftpClient> ConnectAsync(ISftpFileSystem    FileSystem,
                                                           CancellationToken  CancellationToken,
                                                           SshAccessProfile?  Profile  = null,
                                                           SftpLimits?        Limits   = null)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey       = Ed25519KeyPair.Generate();
            var userKey       = SshHostKey.GenerateEd25519();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            _ = Task.Run(async () =>
            {
                try
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                    var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                    await SftpServer.ServeAsync(duplex, FileSystem, Profile, Limits, CancellationToken);
                }
                catch { /* torn down with the client */ }
            }, CancellationToken);

            var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);
            return await SftpClient.OpenAsync(client, CancellationToken);

        }

        #endregion

    }

}
