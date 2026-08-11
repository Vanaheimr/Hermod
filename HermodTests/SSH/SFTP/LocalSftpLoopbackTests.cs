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

    /// <summary>M7 SFTP: our client transfers files to/from a root-jailed local file system over the wire.</summary>
    [TestFixture]
    public class LocalSftpLoopbackTests
    {

        #region Sftp_OverLocalFileSystem_LandsBytesOnDisk

        [Test]
        [CancelAfter(20000)]
        public async Task Sftp_OverLocalFileSystem_LandsBytesOnDisk(CancellationToken CancellationToken)
        {

            var root = Path.Combine(Path.GetTempPath(), "hermod_sftp_lb_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {

                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                var hostKey = Ed25519KeyPair.Generate();
                var userKey = SshHostKey.GenerateEd25519();

                var fileSystem    = new LocalSftpFileSystem(root);
                var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

                var serverRun = Task.Run(async () =>
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                    var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                    await SftpServer.ServeAsync(duplex, fileSystem, CancellationToken: CancellationToken);
                }, CancellationToken);

                var content = RandomNumberGenerator.GetBytes(80_000);   // multi-chunk

                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);

                var sftp = await SftpClient.OpenAsync(client, CancellationToken);

                await sftp.MakeDirectoryAsync("/incoming", CancellationToken);
                await sftp.UploadAsync("/incoming/device.bin", content, CancellationToken);

                // The upload really hit the physical disk under the jail root.
                var physical = Path.Combine(root, "incoming", "device.bin");
                Assert.That(File.Exists(physical), Is.True);
                Assert.That(await File.ReadAllBytesAsync(physical, CancellationToken), Is.EqualTo(content));

                // … and a download reads it straight back.
                var downloaded = await sftp.DownloadAsync("/incoming/device.bin", CancellationToken);
                var listing    = await sftp.ListDirectoryAsync("/incoming", CancellationToken);

                await sftp.DisposeAsync();
                await serverRun;

                Assert.Multiple(() => {
                    Assert.That(downloaded, Is.EqualTo(content));
                    Assert.That(listing.Select(e => e.Name), Does.Contain("device.bin"));
                });

            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }

        }

        #endregion

    }

}
