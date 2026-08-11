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
    /// Interoperability against <b>Paramiko</b> (Python) — the most widely deployed SSH library in the
    /// world and, deliberately, our most <i>conservative</i> peer: through 5.0 it has no post-quantum key
    /// exchange at all, and it still offers CBC and 3DES ciphers we refuse.
    ///
    /// <para>
    /// That makes it the counterpart to <see cref="AsyncSshInteropTests"/>: where AsyncSSH proves the
    /// modern path, Paramiko proves the classical one still works, that we do <b>not</b> fall back to a
    /// legacy cipher merely because a peer offers one, and that a genuinely empty algorithm intersection
    /// fails cleanly instead of hanging.
    /// </para>
    ///
    /// <para>
    /// Paramiko runs inside WSL while our server runs on Windows — see <see cref="WslInterop"/> for the
    /// addressing rules that follow from that.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.WSL")]
    [Category("Interop.Paramiko")]
    public class ParamikoInteropTests
    {

        #region (private) harness

        private sealed record Fixture(SshServer          Server,
                                      String             Host,
                                      Int32              Port,
                                      String             KeyPathWsl,
                                      String             KnownHostsWsl,
                                      ISshHostKey        HostKey,
                                      String[]           TempFiles,
                                      RecordingAuditSink Audit)
        {

            public Dictionary<String, Object?> BaseConfiguration(String Action)
                => new () {
                       ["action"]        = Action,
                       ["host"]          = Host,
                       ["port"]          = Port,
                       ["username"]      = "hermoduser",
                       ["key_path"]      = KeyPathWsl,
                       ["known_hosts"]   = KnownHostsWsl,
                       ["host_key_type"] = "ssh-ed25519",
                       ["host_key_b64"]  = Convert.ToBase64String(HostKey.PublicKeyBlob)
                   };

        }


        private static async Task<Fixture> StartAsync(CancellationToken  CancellationToken,
                                                      ISftpFileSystem?   FileSystem      = null,
                                                      ISshHostKey?       TrustedHostKey  = null)
        {

            WslInterop.SkipIfUnavailable();

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var keyPath    = Path.Combine(Path.GetTempPath(), "hermod_paramiko_" + Guid.NewGuid().ToString("N"));
            var knownHosts = keyPath + ".known_hosts";

            await SshKeyGenerator.WriteKeyPairAsync(userKey, keyPath, "paramiko-interop", CancellationToken);

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
            => WslInterop.RunPeerDriverAsync("paramiko_driver.py", Configuration, CancellationToken);

        #endregion


        #region Paramiko_RunsCommand_OnOurServer

        /// <summary>
        /// The everyday case for the most deployed SSH library there is: connect, authenticate with a key,
        /// run a command, read our output and our exit status.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        [TestCase("hello", 0)]
        [TestCase("fail", 42)]
        public async Task Paramiko_RunsCommand_OnOurServer(String Command, Int32 ExpectedExit, CancellationToken CancellationToken)
        {

            var fixture = await StartAsync(CancellationToken);

            try
            {

                var configuration = fixture.BaseConfiguration("exec");
                configuration["command"] = Command;

                var result = await RunAsync(configuration, CancellationToken);

                TestContext.Out.WriteLine($"Paramiko {result.PeerVersion} saw '{result.ServerVersion}' — {result.AlgorithmSummary}");

                Assert.Multiple(() => {
                    Assert.That(result.Ok,         Is.True, $"Paramiko could not run the command.\n{result.FailureReport}\n--- our server's audit ---\n{fixture.Audit.Report}");
                    Assert.That(result.StdOut,     Is.EqualTo($"hermod ran: {Command}\n"), "our command output must reach Paramiko");
                    Assert.That(result.ExitStatus, Is.EqualTo(ExpectedExit),               "Paramiko must see our exit status");
                });

            }
            finally
            {
                await StopAsync(fixture);
            }

        }

        #endregion

        #region Paramiko_NegotiatesModernAlgorithms_WithoutPostQuantum

        /// <summary>
        /// The conservative-peer path, and the one place our algorithm policy is visible from outside:
        /// Paramiko offers no post-quantum key exchange at all, yet also offers <c>aes256-cbc</c> and
        /// <c>3des-cbc</c>, which we do not implement. A successful session must therefore land on a
        /// modern classical key exchange <b>and</b> a modern cipher — never on the legacy ones on offer.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        public async Task Paramiko_NegotiatesModernAlgorithms_WithoutPostQuantum(CancellationToken CancellationToken)
        {

            var fixture = await StartAsync(CancellationToken);

            try
            {

                var result = await RunAsync(fixture.BaseConfiguration("connect"), CancellationToken);

                TestContext.Out.WriteLine($"Paramiko {result.PeerVersion} negotiated: {result.AlgorithmSummary}");

                Assert.That(result.Ok, Is.True, $"Paramiko must complete a session with us: {result.ErrorType}: {result.Error}");

                var cipher   = result.Algorithms?.GetValueOrDefault("cipher");
                var hostKey  = result.Algorithms?.GetValueOrDefault("host_key");

                Assert.Multiple(() => {

                    Assert.That(hostKey, Is.EqualTo("ssh-ed25519"), "our Ed25519 host key must be what Paramiko verified");

                    // Paramiko offers CBC and 3DES; we must not have agreed to either.
                    Assert.That(cipher, Is.Not.Null.And.Not.Empty);
                    Assert.That(cipher, Does.Not.Contain("cbc").IgnoreCase,
                                "a CBC cipher was on offer and must not have been chosen");
                    Assert.That(cipher, Does.Not.Contain("3des").IgnoreCase);

                });

            }
            finally
            {
                await StopAsync(fixture);
            }

        }

        #endregion

        #region Paramiko_CompletesClassicalTransport

        /// <summary>
        /// The classical fallback pinned down: Paramiko may offer only <c>curve25519-sha256@libssh.org</c>
        /// — the <i>alias</i>, which is the name it actually uses — so a completed session proves we
        /// accept that spelling and interoperate on the non-post-quantum path a peer like this needs.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        public async Task Paramiko_CompletesClassicalTransport(CancellationToken CancellationToken)
        {

            var fixture = await StartAsync(CancellationToken);

            try
            {

                var configuration = fixture.BaseConfiguration("exec");
                configuration["command"]  = "classical";
                configuration["kex_algs"] = new[] { "curve25519-sha256@libssh.org" };

                var result = await RunAsync(configuration, CancellationToken);

                TestContext.Out.WriteLine($"Paramiko over curve25519-sha256@libssh.org — {result.AlgorithmSummary}");

                Assert.Multiple(() => {
                    Assert.That(result.Ok,     Is.True,
                                $"Paramiko must complete the classical key exchange with us: {result.ErrorType}: {result.Error}");
                    Assert.That(result.StdOut, Is.EqualTo("hermod ran: classical\n"));
                });

            }
            finally
            {
                await StopAsync(fixture);
            }

        }

        #endregion

        #region Paramiko_FailsCleanly_WhenNoKeyExchangeIsShared

        /// <summary>
        /// The negative case the interop program asks for: a genuinely empty algorithm intersection must
        /// fail <i>cleanly</i>. Paramiko is restricted to <c>diffie-hellman-group-exchange-sha256</c>,
        /// which it supports and we deliberately do not, so there is nothing to agree on. The peer must
        /// come back with an error rather than hang, and our server must survive it — proven by serving a
        /// second, ordinary session afterwards on the same listener.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        public async Task Paramiko_FailsCleanly_WhenNoKeyExchangeIsShared(CancellationToken CancellationToken)
        {

            var fixture = await StartAsync(CancellationToken);

            try
            {

                var configuration = fixture.BaseConfiguration("exec");
                configuration["command"]  = "unreachable";
                configuration["kex_algs"] = new[] { "diffie-hellman-group-exchange-sha256" };

                var rejected = await RunAsync(configuration, CancellationToken);

                TestContext.Out.WriteLine($"Paramiko failed as expected with: {rejected.ErrorType}: {rejected.Error}");

                Assert.Multiple(() => {
                    Assert.That(rejected.Ok,     Is.False, "there is no shared key exchange, so this must not succeed");
                    Assert.That(rejected.Error,  Is.Not.Null.And.Not.Empty, "the failure must be reported, not silent");
                    Assert.That(rejected.StdOut, Is.Null.Or.Empty);
                });

                // The listener must still be healthy for the next client.
                var afterwards = fixture.BaseConfiguration("exec");
                afterwards["command"] = "still-alive";

                var survivor = await RunAsync(afterwards, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(survivor.Ok,     Is.True,
                                $"our server must survive a failed negotiation: {survivor.ErrorType}: {survivor.Error}");
                    Assert.That(survivor.StdOut, Is.EqualTo("hermod ran: still-alive\n"));
                });

            }
            finally
            {
                await StopAsync(fixture);
            }

        }

        #endregion

        #region Paramiko_TransfersFiles_OverOurSftpSubsystem

        /// <summary>
        /// Paramiko's SFTP client against our subsystem — a third implementation of SFTP v3 (after the
        /// OpenSSH CLI and SSH.NET) driving our server end, upload and download compared byte-for-byte.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        public async Task Paramiko_TransfersFiles_OverOurSftpSubsystem(CancellationToken CancellationToken)
        {

            var fileSystem = new InMemorySftpFileSystem();
            var fixture    = await StartAsync(CancellationToken, fileSystem);

            var payload      = RandomNumberGenerator.GetBytes(40_000);
            var uploadPath   = Path.Combine(Path.GetTempPath(), "hermod_paramiko_up_"   + Guid.NewGuid().ToString("N"));
            var downloadPath = Path.Combine(Path.GetTempPath(), "hermod_paramiko_down_" + Guid.NewGuid().ToString("N"));

            try
            {

                await File.WriteAllBytesAsync(uploadPath, payload, CancellationToken);

                var configuration = fixture.BaseConfiguration("sftp");
                configuration["upload_path"]   = WslInterop.ToWslPath(uploadPath);
                configuration["download_path"] = WslInterop.ToWslPath(downloadPath);
                configuration["remote_path"]   = "/telemetry.bin";

                var result = await RunAsync(configuration, CancellationToken);

                Assert.That(result.Ok, Is.True, $"Paramiko could not transfer over SFTP: {result.ErrorType}: {result.Error}");

                var downloaded = await File.ReadAllBytesAsync(downloadPath, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(downloaded,     Is.EqualTo(payload), "the round-trip through Paramiko must be byte-for-byte");
                    Assert.That(result.Listing, Does.Contain("telemetry.bin"));
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

        #region Paramiko_RejectsAWrongHostKey

        /// <summary>
        /// With a foreign key in known_hosts and a <c>RejectPolicy</c>, Paramiko must refuse the
        /// connection — the same promise as <see cref="AsyncSshInteropTests.AsyncSsh_RejectsAWrongHostKey"/>,
        /// checked against a second independent implementation.
        /// </summary>
        [Test]
        [CancelAfter(120000)]
        public async Task Paramiko_RejectsAWrongHostKey(CancellationToken CancellationToken)
        {

            var somebodyElse = SshHostKey.GenerateEd25519();
            var fixture      = await StartAsync(CancellationToken, TrustedHostKey: somebodyElse);

            try
            {

                var result = await RunAsync(fixture.BaseConfiguration("connect"), CancellationToken);

                TestContext.Out.WriteLine($"Paramiko rejected us with: {result.ErrorType}: {result.Error}");

                Assert.Multiple(() => {
                    Assert.That(result.Ok,    Is.False, "Paramiko must not accept a host key it does not trust");
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
