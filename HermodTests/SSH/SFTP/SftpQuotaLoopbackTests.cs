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
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M7 SFTP quotas, end-to-end: an upload exceeding the per-file size quota fails cleanly, the partial
    /// upload is discarded from disk, and the session stays healthy for further transfers.
    /// </summary>
    [TestFixture]
    public class SftpQuotaLoopbackTests
    {

        #region Upload_ExceedingFileSizeQuota_FailsCleanly_AndRemovesPartial

        [Test]
        [CancelAfter(20000)]
        public async Task Upload_ExceedingFileSizeQuota_FailsCleanly_AndRemovesPartial(CancellationToken CancellationToken)
        {

            var root = Path.Combine(Path.GetTempPath(), "hermod_sftp_q_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {

                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                var hostKey = Ed25519KeyPair.Generate();
                var userKey = SshHostKey.GenerateEd25519();

                var fileSystem    = new LocalSftpFileSystem(root);
                var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);
                var limits        = new SftpLimits { MaxFileSize = 50_000 };   // below the 100 KiB we try to send

                var serverRun = Task.Run(async () =>
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                    var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                    await SftpServer.ServeAsync(duplex, fileSystem, Limits: limits, CancellationToken: CancellationToken);
                }, CancellationToken);

                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);
                var sftp = await SftpClient.OpenAsync(client, CancellationToken);

                // The oversized upload is rejected …
                var tooBig = RandomNumberGenerator.GetBytes(100_000);
                var error  = Assert.CatchAsync<SftpException>(async () => await sftp.UploadAsync("/big.bin", tooBig, CancellationToken));

                // … and no partial file is left behind on disk.
                var physical = Path.Combine(root, "big.bin");

                // The session is still healthy: a within-quota upload succeeds and round-trips.
                var small = Encoding.UTF8.GetBytes("small-and-legal");
                await sftp.UploadAsync("/ok.bin", small, CancellationToken);
                var back  = await sftp.DownloadAsync("/ok.bin", CancellationToken);

                await sftp.DisposeAsync();
                await serverRun;

                Assert.Multiple(() => {
                    Assert.That(error!.Code,          Is.EqualTo(SftpStatusCode.Failure));
                    Assert.That(File.Exists(physical), Is.False, "the partial upload must be discarded");
                    Assert.That(back,                 Is.EqualTo(small), "the session survives a quota rejection");
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
