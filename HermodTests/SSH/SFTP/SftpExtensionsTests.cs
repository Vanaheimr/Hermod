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

    }

}
