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
    /// M7 SFTP least-privilege access profiles: the upload-only (log collection) and download-only
    /// (firmware distribution) presets deny the operations outside their remit.
    /// </summary>
    [TestFixture]
    public class SftpAccessProfileTests
    {

        private static async Task<SftpClient> ConnectAsync(SshAccessProfile Profile, InMemorySftpFileSystem FileSystem, CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            _ = Task.Run(async () =>
            {
                try
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                    var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                    await SftpServer.ServeAsync(duplex, FileSystem, Profile, CancellationToken: CancellationToken);
                }
                catch { /* torn down with the client */ }
            }, CancellationToken);

            var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);
            return await SftpClient.OpenAsync(client, CancellationToken);

        }


        #region UploadOnly_AllowsUpload_DeniesDownloadAndList

        [Test]
        [CancelAfter(20000)]
        public async Task UploadOnly_AllowsUpload_DeniesDownloadAndList(CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            var sftp       = await ConnectAsync(SshAccessProfile.SftpUploadOnly, fileSystem, CancellationToken);

            // Uploading a log file is allowed …
            await sftp.UploadAsync("/device.log", Encoding.UTF8.GetBytes("boot ok\n"), CancellationToken);

            // … but reading anything back or listing is denied.
            var download = Assert.CatchAsync<SftpException>(async () => await sftp.DownloadAsync("/device.log", CancellationToken));
            var list     = Assert.CatchAsync<SftpException>(async () => await sftp.ListDirectoryAsync("/", CancellationToken));

            Assert.Multiple(() => {
                Assert.That(download!.Code, Is.EqualTo(SftpStatusCode.PermissionDenied));
                Assert.That(list!.Code,     Is.EqualTo(SftpStatusCode.PermissionDenied));
            });

            await sftp.DisposeAsync();

        }

        #endregion

        #region DownloadOnly_AllowsDownload_DeniesUploadAndDelete

        [Test]
        [CancelAfter(20000)]
        public async Task DownloadOnly_AllowsDownload_DeniesUploadAndDelete(CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            fileSystem.AddFile("/firmware.bin", Encoding.UTF8.GetBytes("firmware-image-v2"));

            var sftp = await ConnectAsync(SshAccessProfile.SftpDownloadOnly, fileSystem, CancellationToken);

            // Downloading the firmware is allowed …
            var image = await sftp.DownloadAsync("/firmware.bin", CancellationToken);

            // … but uploading or deleting is denied.
            var upload = Assert.CatchAsync<SftpException>(async () => await sftp.UploadAsync("/evil.bin", [ 1, 2, 3 ], CancellationToken));
            var delete = Assert.CatchAsync<SftpException>(async () => await sftp.RemoveAsync("/firmware.bin", CancellationToken));

            Assert.Multiple(() => {
                Assert.That(Encoding.UTF8.GetString(image), Is.EqualTo("firmware-image-v2"));
                Assert.That(upload!.Code, Is.EqualTo(SftpStatusCode.PermissionDenied));
                Assert.That(delete!.Code, Is.EqualTo(SftpStatusCode.PermissionDenied));
            });

            await sftp.DisposeAsync();

        }

        #endregion

    }

}
