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

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Interoperability against <b>AsyncSSH</b> (Python) — the most feature-complete of the third-party
    /// peers and a lineage sharing no code with ours: pure Python on top of PyCA <c>cryptography</c>.
    ///
    /// <para>
    /// AsyncSSH runs inside WSL while our server runs on Windows, so these tests bind to
    /// <c>IPv4Address.Any</c> and let <see cref="WslInterop.ResolveWindowsHostAsync"/> work out which
    /// address the peer must dial (see the note there on NAT vs. mirrored networking). A machine without
    /// the provisioned harness skips rather than fails — no evidence either way.
    /// </para>
    ///
    /// <para>
    /// Where a test needs to prove that a *specific* algorithm was negotiated it constrains what AsyncSSH
    /// is allowed to offer, rather than trusting what the peer reports afterwards: if the client may only
    /// propose one key exchange, a completed handshake is the proof.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.WSL")]
    [Category("Interop.AsyncSSH")]
    public class AsyncSshInteropTests
    {

        #region (private) harness

        /// <summary>Everything a driver run needs, plus the temporary files to clean up afterwards.</summary>
        private sealed record Fixture(SshServer   Server,
                                      String      Host,
                                      Int32       Port,
                                      String      KeyPathWsl,
                                      String      KnownHostsWsl,
                                      ISshHostKey HostKey,
                                      String[]    TempFiles,
                                      RecordingAuditSink Audit)
        {

            public Dictionary<String, Object?> BaseConfiguration(String Action)
                => new () {
                       ["action"]      = Action,
                       ["host"]        = Host,
                       ["port"]        = Port,
                       ["username"]    = "hermoduser",
                       ["key_path"]    = KeyPathWsl,
                       ["known_hosts"] = KnownHostsWsl
                   };

        }


        /// <summary>
        /// Start our server bound on every interface, write the client key and a known_hosts file, and
        /// determine the address AsyncSSH has to dial from inside WSL.
        /// </summary>
        /// <param name="TrustedHostKey">
        /// The host key to write into known_hosts — pass a foreign key to test rejection.
        /// </param>
        private static async Task<Fixture> StartAsync(CancellationToken   CancellationToken,
                                                      ISftpFileSystem?    FileSystem      = null,
                                                      ISshHostKey?        TrustedHostKey  = null)
        {

            WslInterop.SkipIfUnavailable();

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var keyPath    = Path.Combine(Path.GetTempPath(), "hermod_asyncssh_" + Guid.NewGuid().ToString("N"));
            var knownHosts = keyPath + ".known_hosts";

            await SshKeyGenerator.WriteKeyPairAsync(userKey, keyPath, "asyncssh-interop", CancellationToken);

            var audit  = new RecordingAuditSink();

            var server = new SshServer(new SshServerOptions {
                             HostKeys        = [ hostKey ],
                             Authenticator   = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                             SftpFileSystem  = FileSystem,
                             AuditSink       = audit,
                             ExecHandler     = async (context, ct) => {
                                                   await context.WriteAsync($"hermod ran: {context.Command}\n", ct);
                                                   return context.Command == "fail" ? 42 : 0;
                                               }
                         });

            // Bound on Any: a peer inside WSL cannot reach a loopback-only listener on the Windows host.
            await server.StartAsync(new IPSocket(IPv4Address.Any, IPPort.Auto), CancellationToken);
            var port = server.LocalEndPoint.Port.ToInt32();

            var host = await WslInterop.ResolveWindowsHostAsync(port, CancellationToken);
            if (host is null)
            {
                await server.DisposeAsync();
                Assert.Ignore($"WSL cannot reach the test listener on port {port} — check the Windows firewall for the test host process.");
            }

            var trusted = TrustedHostKey ?? hostKey;
            await File.WriteAllTextAsync(knownHosts,
                                         $"[{host}]:{port} ssh-ed25519 {Convert.ToBase64String(trusted.PublicKeyBlob)}\n",
                                         CancellationToken);

            return new Fixture(server,
                               host!,
                               port,
                               WslInterop.ToWslPath(keyPath),
                               WslInterop.ToWslPath(knownHosts),
                               hostKey,
                               [ keyPath, keyPath + ".pub", knownHosts ],
                               audit);

        }

        private static async Task StopAsync(Fixture Fixture)
        {
            await Fixture.Server.DisposeAsync();
            foreach (var file in Fixture.TempFiles)
                try { File.Delete(file); } catch { }
        }

        private static Task<PeerRunResult> RunAsync(Dictionary<String, Object?> Configuration, CancellationToken CancellationToken)
            => WslInterop.RunPeerDriverAsync("asyncssh_driver.py", Configuration, CancellationToken);

        #endregion


        #region AsyncSsh_RunsCommand_OnOurServer

        /// <summary>
        /// The whole stack as a Python implementation sees it: version exchange, key exchange, publickey
        /// authentication against a key our own generator wrote, a session channel, our command output
        /// and our exit status.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        [TestCase("hello", 0)]
        [TestCase("fail", 42)]
        public async Task AsyncSsh_RunsCommand_OnOurServer(String Command, Int32 ExpectedExit, CancellationToken CancellationToken)
        {

            var fixture = await StartAsync(CancellationToken);

            try
            {

                var configuration = fixture.BaseConfiguration("exec");
                configuration["command"] = Command;

                var result = await RunAsync(configuration, CancellationToken);

                TestContext.Out.WriteLine($"AsyncSSH {result.PeerVersion} saw '{result.ServerVersion}' — {result.AlgorithmSummary}");

                Assert.Multiple(() => {
                    Assert.That(result.Ok,         Is.True, $"AsyncSSH could not run the command.\n{result.FailureReport}\n--- our server's audit ---\n{fixture.Audit.Report}");
                    Assert.That(result.StdOut,     Is.EqualTo($"hermod ran: {Command}\n"), "our command output must reach AsyncSSH");
                    Assert.That(result.ExitStatus, Is.EqualTo(ExpectedExit),               "AsyncSSH must see our exit status");
                });

            }
            finally
            {
                await StopAsync(fixture);
            }

        }

        #endregion

        #region AsyncSsh_CompletesPostQuantumTransport

        /// <summary>
        /// A third-party implementation completing our <b>post-quantum hybrid key exchange</b>: AsyncSSH is
        /// allowed to offer <c>mlkem768x25519-sha256</c> and nothing else, so a session that reaches the
        /// command stage proves both sides derived the same ML-KEM-768 + X25519 secret — across .NET's
        /// <c>MLKem</c> on our side and PyCA <c>cryptography</c> on theirs.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        public async Task AsyncSsh_CompletesPostQuantumTransport(CancellationToken CancellationToken)
        {

            var fixture = await StartAsync(CancellationToken);

            try
            {

                var configuration = fixture.BaseConfiguration("exec");
                configuration["command"]  = "pq";
                configuration["kex_algs"] = new[] { "mlkem768x25519-sha256" };

                var result = await RunAsync(configuration, CancellationToken);

                TestContext.Out.WriteLine($"AsyncSSH {result.PeerVersion} over mlkem768x25519-sha256 — {result.AlgorithmSummary}");

                Assert.Multiple(() => {
                    Assert.That(result.Ok,     Is.True,
                                $"AsyncSSH must complete the PQ hybrid key exchange with us: {result.ErrorType}: {result.Error}");
                    Assert.That(result.StdOut, Is.EqualTo("hermod ran: pq\n"),
                                "traffic after NEWKEYS must decrypt, which only holds if the shared secret matches");
                });

            }
            finally
            {
                await StopAsync(fixture);
            }

        }

        #endregion

        #region AsyncSsh_TransfersFiles_OverOurSftpSubsystem

        /// <summary>
        /// AsyncSSH's SFTP client against our subsystem: upload, list and download a multi-chunk payload,
        /// compared byte-for-byte.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        public async Task AsyncSsh_TransfersFiles_OverOurSftpSubsystem(CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            var fixture    = await StartAsync(CancellationToken, fileSystem);

            var payload      = RandomNumberGenerator.GetBytes(40_000);
            var uploadPath   = Path.Combine(Path.GetTempPath(), "hermod_asyncssh_up_"   + Guid.NewGuid().ToString("N"));
            var downloadPath = Path.Combine(Path.GetTempPath(), "hermod_asyncssh_down_" + Guid.NewGuid().ToString("N"));

            try
            {

                await File.WriteAllBytesAsync(uploadPath, payload, CancellationToken);

                var configuration = fixture.BaseConfiguration("sftp");
                configuration["upload_path"]   = WslInterop.ToWslPath(uploadPath);
                configuration["download_path"] = WslInterop.ToWslPath(downloadPath);
                configuration["remote_path"]   = "/device.bin";

                var result = await RunAsync(configuration, CancellationToken);

                Assert.That(result.Ok, Is.True, $"AsyncSSH could not transfer over SFTP: {result.ErrorType}: {result.Error}");

                var downloaded = await File.ReadAllBytesAsync(downloadPath, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(downloaded,     Is.EqualTo(payload), "the round-trip through AsyncSSH must be byte-for-byte");
                    Assert.That(result.Listing, Does.Contain("device.bin"));
                });

            }
            finally
            {
                try { File.Delete(uploadPath);   } catch { }
                try { File.Delete(downloadPath); } catch { }
                await StopAsync(fixture);
            }

        }

        #endregion

        #region AsyncSsh_RejectsAWrongHostKey

        /// <summary>
        /// Host-key verification seen from the other side: with a foreign key in known_hosts AsyncSSH must
        /// refuse to continue — proof that the key we present is really the one reaching the peer, and
        /// that a mismatch is fatal rather than merely noted.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        public async Task AsyncSsh_RejectsAWrongHostKey(CancellationToken CancellationToken)
        {

            var somebodyElse = SshHostKey.GenerateEd25519();
            var fixture      = await StartAsync(CancellationToken, TrustedHostKey: somebodyElse);

            try
            {

                var result = await RunAsync(fixture.BaseConfiguration("connect"), CancellationToken);

                TestContext.Out.WriteLine($"AsyncSSH rejected us with: {result.ErrorType}: {result.Error}");

                Assert.Multiple(() => {
                    Assert.That(result.Ok,    Is.False, "AsyncSSH must not accept a host key it does not trust");
                    Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
                });

            }
            finally
            {
                await StopAsync(fixture);
            }

        }

        #endregion

    }

}
