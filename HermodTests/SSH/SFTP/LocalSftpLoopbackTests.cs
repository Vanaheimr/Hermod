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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M7 SFTP: our client transfers files to/from a root-jailed local file system over the wire.
    /// </summary>
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
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, User_Id.Parse("achim"), userKey, CancellationToken: CancellationToken);

                var sftp = await SFTPClient.OpenAsync(client, CancellationToken);

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

        #region Sftp_AbruptClientDisconnect_ClosesAllOpenServerHandles

        /// <summary>
        /// When a client disconnects abruptly while holding open SFTP handles (without sending
        /// <c>SSH_FXP_CLOSE</c>), <see cref="SftpServer"/> must close every remaining handle on the backing
        /// file system so no OS file descriptors are leaked.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Sftp_AbruptClientDisconnect_ClosesAllOpenServerHandles(CancellationToken CancellationToken)
        {

            var root = Path.Combine(Path.GetTempPath(), "hermod_sftp_leak_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {

                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                var hostKey = Ed25519KeyPair.Generate();
                var userKey = SshHostKey.GenerateEd25519();

                using var fileSystem = new LocalSftpFileSystem(root);
                var authenticator    = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

                var serverRun = Task.Run(async () =>
                {
                    try
                    {
                        using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                        await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                        var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                        await SftpServer.ServeAsync(duplex, fileSystem, CancellationToken: CancellationToken);
                    }
                    catch { }
                }, CancellationToken);

                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, User_Id.Parse("achim"), userKey, CancellationToken: CancellationToken);

                var sftp = await SFTPClient.OpenAsync(client, CancellationToken);

                // Open three files and deliberately leave them all unclosed.
                var h1 = await sftp.OpenFileAsync("/leaked1.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken);
                var h2 = await sftp.OpenFileAsync("/leaked2.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken);
                var h3 = await sftp.OpenFileAsync("/leaked3.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken);
                await sftp.WriteAsync(h1, 0, new Byte[] { 1, 2, 3 }, CancellationToken);
                await sftp.WriteAsync(h2, 0, new Byte[] { 4, 5, 6 }, CancellationToken);
                await sftp.WriteAsync(h3, 0, new Byte[] { 7, 8, 9 }, CancellationToken);

                Assert.That(fileSystem.OpenHandleCount, Is.EqualTo(3));

                // Drop the transport abruptly without closing any SFTP handle.
                await clientPipe.Output.CompleteAsync();
                await clientPipe.Input.CompleteAsync();
                await serverRun;

                Assert.That(fileSystem.OpenHandleCount, Is.EqualTo(0), "server must close all open handles when the session ends");

                // On Windows, deleting a file fails with IOException if any SafeFileHandle is still open.
                Assert.DoesNotThrow(() => File.Delete(Path.Combine(root, "leaked1.bin")));
                Assert.DoesNotThrow(() => File.Delete(Path.Combine(root, "leaked2.bin")));
                Assert.DoesNotThrow(() => File.Delete(Path.Combine(root, "leaked3.bin")));

                await sftp.DisposeAsync();

            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }

        }

        #endregion

        #region Sftp_MaxOpenHandles_RejectsExcessiveOpens

        /// <summary>
        /// Per-session open-handle cap: opening more handles than <see cref="SftpLimits.MaxOpenHandles"/>
        /// allows is refused with <see cref="SftpStatusCode.Failure"/> until an existing handle is closed.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Sftp_MaxOpenHandles_RejectsExcessiveOpens(CancellationToken CancellationToken)
        {

            var root = Path.Combine(Path.GetTempPath(), "hermod_sftp_cap_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {

                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                var hostKey = Ed25519KeyPair.Generate();
                var userKey = SshHostKey.GenerateEd25519();

                using var fileSystem = new LocalSftpFileSystem(root);
                var authenticator    = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

                var serverRun = Task.Run(async () =>
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                    var duplex = await SshConnection.AcceptSubsystemAsync(t, "sftp", CancellationToken);
                    await SftpServer.ServeAsync(duplex, fileSystem, Limits: new SftpLimits { MaxOpenHandles = 3 }, CancellationToken: CancellationToken);
                }, CancellationToken);

                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, User_Id.Parse("achim"), userKey, CancellationToken: CancellationToken);

                var sftp   = await SFTPClient.OpenAsync(client, CancellationToken);
                var limits = await sftp.LimitsAsync(CancellationToken);
                Assert.That(limits.MaxOpenHandles, Is.EqualTo(3UL));

                var h1 = await sftp.OpenFileAsync("/f1.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken);
                var h2 = await sftp.OpenFileAsync("/f2.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken);
                var h3 = await sftp.OpenFileAsync("/f3.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken);

                var ex = await Assert.ThrowsAsync<SftpException>(async () =>
                    await sftp.OpenFileAsync("/f4.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken));
                Assert.That(ex!.Code, Is.EqualTo(SftpStatusCode.Failure));

                // Closing one handle frees a slot.
                await sftp.CloseAsync(h1, CancellationToken);
                var h4 = await sftp.OpenFileAsync("/f4.bin", SftpOpenFlags.Create | SftpOpenFlags.Write, CancellationToken);
                await sftp.CloseAsync(h2, CancellationToken);
                await sftp.CloseAsync(h3, CancellationToken);
                await sftp.CloseAsync(h4, CancellationToken);

                await sftp.DisposeAsync();
                await serverRun;

                Assert.That(fileSystem.OpenHandleCount, Is.EqualTo(0));

            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }

        }

        #endregion

    }

}
