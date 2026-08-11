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

using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>M7 SFTP: our client transfers and manages files against our server over a loopback pipe.</summary>
    [TestFixture]
    public class SftpLoopbackTests
    {

        #region Sftp_UploadDownloadListManage

        [Test]
        [CancelAfter(20000)]
        public async Task Sftp_UploadDownloadListManage(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            var fileSystem    = new InMemorySftpFileSystem();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                await SftpServer.ServeAsync(duplex, fileSystem, CancellationToken: CancellationToken);
            }, CancellationToken);

            // ~100 KiB crosses several transfer chunks / channel packets.
            var content = RandomNumberGenerator.GetBytes(100_000);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            Assert.That(await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken), Is.True);

            var sftp = await SftpClient.OpenAsync(client, CancellationToken);

            await sftp.UploadAsync("/hello.bin", content, CancellationToken);
            var downloaded = await sftp.DownloadAsync("/hello.bin", CancellationToken);
            var stat       = await sftp.StatAsync("/hello.bin", CancellationToken);

            await sftp.MakeDirectoryAsync("/sub", CancellationToken);
            var listingBefore = await sftp.ListDirectoryAsync("/", CancellationToken);

            await sftp.RenameAsync("/hello.bin", "/world.bin", CancellationToken);
            var afterRename   = await sftp.DownloadAsync("/world.bin", CancellationToken);

            await sftp.RemoveAsync("/world.bin", CancellationToken);
            var listingAfter  = await sftp.ListDirectoryAsync("/", CancellationToken);

            await sftp.DisposeAsync();
            await serverRun;

            Assert.Multiple(() => {
                Assert.That(downloaded,       Is.EqualTo(content), "download must match upload byte-for-byte");
                Assert.That(stat.Size,        Is.EqualTo(content.Length));
                Assert.That(listingBefore.Select(e => e.Name), Does.Contain("hello.bin").And.Contains("sub"));
                Assert.That(afterRename,      Is.EqualTo(content), "renamed file keeps its content");
                Assert.That(listingAfter.Select(e => e.Name), Does.Not.Contain("world.bin"));
                Assert.That(listingAfter.Select(e => e.Name), Does.Contain("sub"));
            });

        }

        #endregion

        #region Sftp_DownloadMissingFile_Throws

        [Test]
        [CancelAfter(20000)]
        public async Task Sftp_DownloadMissingFile_Throws(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                await SftpServer.ServeAsync(duplex, new InMemorySftpFileSystem(), CancellationToken: CancellationToken);
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);
            var sftp = await SftpClient.OpenAsync(client, CancellationToken);

            var error = Assert.CatchAsync<SftpException>(async () => await sftp.DownloadAsync("/does-not-exist", CancellationToken));
            Assert.That(error!.Code, Is.EqualTo(SftpStatusCode.NoSuchFile));

            await sftp.DisposeAsync();
            await serverRun;

        }

        #endregion

    }

}
