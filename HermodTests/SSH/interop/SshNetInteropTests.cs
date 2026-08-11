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

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Interoperability against <b>SSH.NET</b> — an independent .NET SSH implementation — exercising our
    /// <b>server</b> role from a completely different code lineage.
    ///
    /// <para>
    /// Unlike the OpenSSH tests these need no external binaries, containers or WSL: SSH.NET is a NuGet
    /// package and runs inside the NUnit process, so this is real third-party interop that works on any
    /// machine and in any CI, with zero orchestration. That makes it the one Tier-2 peer that can gate
    /// every commit rather than a nightly matrix (PLAN §11.1).
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.InProcess")]
    public class SshNetInteropTests
    {

        #region (private) server + client helpers

        private static async Task<(SshServer Server, Int32 Port, ISshHostKey HostKey, String KeyPath)>
            StartServerAsync(CancellationToken CancellationToken, ISftpFileSystem? FileSystem = null)
        {

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            // SSH.NET reads the private key from a file, so write it in openssh-key-v1.
            var keyPath = Path.Combine(Path.GetTempPath(), "hermod_sshnet_" + Guid.NewGuid().ToString("N"));
            await SshKeyGenerator.WriteKeyPairAsync(userKey, keyPath, "sshnet-interop", CancellationToken);

            var server = new SshServer(new SshServerOptions {
                HostKeys        = [ hostKey ],
                Authenticator   = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                SftpFileSystem  = FileSystem,
                ExecHandler     = async (context, ct) => {
                                      await context.WriteAsync($"hermod ran: {context.Command}\n", ct);
                                      if (context.Command == "fail")
                                          return 42;
                                      return 0;
                                  }
            });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return (server, server.LocalEndPoint.Port.ToInt32(), hostKey, keyPath);

        }

        /// <summary>Build an SSH.NET connection that pins our host key.</summary>
        private static Renci.SshNet.ConnectionInfo ConnectionFor(Int32 Port, String KeyPath)
        {

            var privateKey = new Renci.SshNet.PrivateKeyFile(KeyPath);

            return new Renci.SshNet.ConnectionInfo(
                       "127.0.0.1", Port, "hermoduser",
                       new Renci.SshNet.PrivateKeyAuthenticationMethod("hermoduser", privateKey));

        }

        private static void PinHostKey(Renci.SshNet.BaseClient Client, ISshHostKey HostKey)
            => Client.HostKeyReceived += (_, e) =>
                   e.CanTrust = e.HostKey.AsSpan().SequenceEqual(HostKey.PublicKeyBlob);

        private static void Cleanup(String KeyPath)
        {
            try { File.Delete(KeyPath);          } catch { }
            try { File.Delete(KeyPath + ".pub"); } catch { }
        }

        #endregion


        #region SshNet_RunsCommand_OnOurServer

        /// <summary>
        /// The whole stack from another lineage's point of view: version exchange, KEX, publickey auth
        /// against a key our own generator wrote, a session channel, command output and the exit status.
        /// </summary>
        [Test]
        [CancelAfter(60000)]
        [TestCase("uname -a", 0)]
        [TestCase("fail",    42)]
        public async Task SshNet_RunsCommand_OnOurServer(String Command, Int32 ExpectedExit, CancellationToken CancellationToken)
        {

            var (server, port, hostKey, keyPath) = await StartServerAsync(CancellationToken);

            try
            {

                var (output, exit) = await Task.Run(() => {

                    using var client = new Renci.SshNet.SshClient(ConnectionFor(port, keyPath));
                    PinHostKey(client, hostKey);

                    client.Connect();
                    using var command = client.RunCommand(Command);
                    return (command.Result, command.ExitStatus);

                }, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(output, Is.EqualTo($"hermod ran: {Command}\n"), "SSH.NET must receive our command output");
                    Assert.That(exit,   Is.EqualTo(ExpectedExit),               "SSH.NET must see our exit status");
                });

            }
            finally
            {
                await server.DisposeAsync();
                Cleanup(keyPath);
            }

        }

        #endregion

        #region SshNet_RejectsAWrongHostKey

        /// <summary>
        /// Host-key verification seen from the other side: SSH.NET must refuse when the key it pinned is
        /// not the one we present — proof our host key really is what reaches the peer.
        /// </summary>
        [Test]
        [CancelAfter(60000)]
        public async Task SshNet_RejectsAWrongHostKey(CancellationToken CancellationToken)
        {

            var (server, port, _, keyPath) = await StartServerAsync(CancellationToken);
            var somebodyElse = SshHostKey.GenerateEd25519();

            try
            {

                Assert.CatchAsync(async () => await Task.Run(() => {

                    using var client = new Renci.SshNet.SshClient(ConnectionFor(port, keyPath));
                    PinHostKey(client, somebodyElse);
                    client.Connect();

                }, CancellationToken));

            }
            finally
            {
                await server.DisposeAsync();
                Cleanup(keyPath);
            }

        }

        #endregion

        #region SshNet_TransfersFiles_OverOurSftpSubsystem

        /// <summary>
        /// SSH.NET's SFTP client against our subsystem: upload, list, download, delete — a third-party
        /// implementation of SFTP v3 driving our server end.
        /// </summary>
        [Test]
        [CancelAfter(60000)]
        public async Task SshNet_TransfersFiles_OverOurSftpSubsystem(CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            var (server, port, hostKey, keyPath) = await StartServerAsync(CancellationToken, fileSystem);

            var payload = RandomNumberGenerator.GetBytes(40_000);   // multi-chunk

            try
            {

                var (downloaded, listing) = await Task.Run(() => {

                    using var sftp = new Renci.SshNet.SftpClient(ConnectionFor(port, keyPath));
                    PinHostKey(sftp, hostKey);
                    sftp.Connect();

                    using (var upload = new MemoryStream(payload))
                        sftp.UploadFile(upload, "/device.bin");

                    using var download = new MemoryStream();
                    sftp.DownloadFile("/device.bin", download);

                    var names = sftp.ListDirectory("/").Select(entry => entry.Name).ToArray();

                    return (download.ToArray(), names);

                }, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(downloaded, Is.EqualTo(payload), "the round-trip through SSH.NET must be byte-for-byte");
                    Assert.That(listing,    Does.Contain("device.bin"));
                });

            }
            finally
            {
                await server.DisposeAsync();
                Cleanup(keyPath);
            }

        }

        #endregion

        #region SshNet_NegotiatesModernAlgorithms

        /// <summary>
        /// Records what the two implementations actually agree on, so the interop claim is specific
        /// rather than "it connected": an independent lineage must land on the modern primitives, not on
        /// some legacy fallback that happens to work.
        /// </summary>
        [Test]
        [CancelAfter(60000)]
        public async Task SshNet_NegotiatesModernAlgorithms(CancellationToken CancellationToken)
        {

            var (server, port, hostKey, keyPath) = await StartServerAsync(CancellationToken);

            try
            {

                var (kex, cipher, hostKeyAlg, mac) = await Task.Run(() => {

                    using var client = new Renci.SshNet.SshClient(ConnectionFor(port, keyPath));
                    PinHostKey(client, hostKey);
                    client.Connect();

                    var info = client.ConnectionInfo;
                    return (info.CurrentKeyExchangeAlgorithm,
                            info.CurrentServerEncryption,
                            info.CurrentHostKeyAlgorithm,
                            info.CurrentServerHmacAlgorithm);

                }, CancellationToken);

                TestContext.Out.WriteLine($"SSH.NET negotiated: kex={kex}, cipher={cipher}, hostkey={hostKeyAlg}, mac={mac ?? "(aead)"}");

                Assert.Multiple(() => {

                    Assert.That(kex,         Is.Not.Null.And.Not.Empty);
                    Assert.That(cipher,      Is.Not.Null.And.Not.Empty);
                    Assert.That(hostKeyAlg,  Is.EqualTo("ssh-ed25519"), "our Ed25519 host key must be what it verified");

                    // No SHA-1 key exchange and no legacy cipher may be reachable.
                    Assert.That(kex,     Does.Not.Contain("sha1").IgnoreCase);
                    Assert.That(cipher,  Does.Not.Contain("3des").IgnoreCase);
                    Assert.That(cipher,  Does.Not.Contain("arcfour").IgnoreCase);

                });

            }
            finally
            {
                await server.DisposeAsync();
                Cleanup(keyPath);
            }

        }

        #endregion

        #region SshNet_RunsSeveralCommandsOnOneConnection

        /// <summary>Our multiplexer seen from another implementation: several sessions on one connection.</summary>
        [Test]
        [CancelAfter(60000)]
        public async Task SshNet_RunsSeveralCommandsOnOneConnection(CancellationToken CancellationToken)
        {

            var (server, port, hostKey, keyPath) = await StartServerAsync(CancellationToken);

            try
            {

                var results = await Task.Run(() => {

                    using var client = new Renci.SshNet.SshClient(ConnectionFor(port, keyPath));
                    PinHostKey(client, hostKey);
                    client.Connect();

                    return Enumerable.Range(1, 4)
                                     .Select(i => { using var c = client.RunCommand($"step-{i}"); return c.Result; })
                                     .ToArray();

                }, CancellationToken);

                Assert.That(results, Is.EqualTo(new[] {
                    "hermod ran: step-1\n", "hermod ran: step-2\n", "hermod ran: step-3\n", "hermod ran: step-4\n"
                }));

            }
            finally
            {
                await server.DisposeAsync();
                Cleanup(keyPath);
            }

        }

        #endregion

    }

}
