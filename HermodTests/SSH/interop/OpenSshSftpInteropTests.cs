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

using System.Diagnostics;
using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The real OpenSSH <c>sftp</c> client transfers files to/from our server over a root-jailed
    /// <see cref="LocalSftpFileSystem"/> — an end-to-end proof our SFTP server interoperates with the
    /// reference client (INIT/VERSION, realpath, open/read/write/close, fstat/fsetstat, limits@openssh.com).
    /// </summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.OpenSSH")]
    public class OpenSshSftpInteropTests
    {

        private static String? FindOnPathOrSystem(String Tool)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                foreach (var name in new[] { Tool, Tool + ".exe" })
                    try { var c = Path.Combine(dir.Trim(), name); if (File.Exists(c)) return c; } catch { }
            var system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", Tool + ".exe");
            return File.Exists(system) ? system : null;
        }


        #region RealSftpClient_PutsAndGets_AgainstOurServer

        [Test]
        [CancelAfter(40000)]
        public async Task RealSftpClient_PutsAndGets_AgainstOurServer(CancellationToken CancellationToken)
        {

            var sftpCli    = FindOnPathOrSystem("sftp");
            var sshKeygen  = FindOnPathOrSystem("ssh-keygen");
            if (sftpCli is null || sshKeygen is null)
                Assert.Ignore("No 'sftp'/'ssh-keygen' found.");

            var workDir  = Path.Combine(Path.GetTempPath(), "hermod_sftpi_" + Guid.NewGuid().ToString("N"));
            var root     = Path.Combine(workDir, "root");
            Directory.CreateDirectory(root);

            var keyPath   = Path.Combine(workDir, "id");
            var localSrc  = Path.Combine(workDir, "src.bin");
            var localDst  = Path.Combine(workDir, "dst.bin");
            var batch     = Path.Combine(workDir, "batch.txt");
            var knownHosts = Path.Combine(workDir, "known_hosts");
            var emptyConf  = Path.Combine(workDir, "empty_conf");
            File.WriteAllText(emptyConf, "");

            var payload = RandomNumberGenerator.GetBytes(40_000);
            await File.WriteAllBytesAsync(localSrc, payload, CancellationToken);

            try
            {

                using (var keygen = Process.Start(new ProcessStartInfo(sshKeygen!) { ArgumentList = { "-t", "ed25519", "-f", keyPath, "-N", "", "-q" }, UseShellExecute = false, CreateNoWindow = true })!)
                    await keygen.WaitForExitAsync(CancellationToken);

                var publicLine = (await File.ReadAllTextAsync(keyPath + ".pub", CancellationToken)).Split(' ');
                var publicBlob = Convert.FromBase64String(publicLine[1]);

                var authenticator = SshUserAuthenticator.ForAuthorizedKeys(publicBlob);
                var hostKey       = SshHostKey.GenerateEd25519();

                using var listener = SshTcpListener.Start(new IPSocket(IPv4Address.Localhost, IPPort.Auto));
                var port = listener.LocalEndPoint.Port.ToInt32();

                Exception? serverError = null;
                var serverTask = Task.Run(async () =>
                {
                    try
                    {
                        var pipe = await listener.AcceptAsync(CancellationToken);
                        using var transport = await SshTransport.ServerHandshakeAsync(pipe, hostKey, CancellationToken: CancellationToken);
                        await UserAuthentication.ServerAuthenticateAsync(transport, authenticator, CancellationToken: CancellationToken);
                        var duplex = await SshConnection.AcceptSubsystemAsync(transport, "sftp", CancellationToken);
                        await SftpServer.ServeAsync(duplex, new LocalSftpFileSystem(root), CancellationToken: CancellationToken);
                    }
                    catch (Exception e) { serverError = e; throw; }
                }, CancellationToken);

                // Batch script: upload then download (forward slashes work for local paths on Windows too).
                await File.WriteAllTextAsync(batch,
                    $"put {localSrc.Replace('\\', '/')} /uploaded.bin\n" +
                    $"get /uploaded.bin {localDst.Replace('\\', '/')}\n",
                    CancellationToken);

                using var client = new Process { StartInfo = new ProcessStartInfo(sftpCli!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
                foreach (var arg in new[]
                {
                    "-F", emptyConf, "-P", port.ToString(),
                    "-o", "StrictHostKeyChecking=no", "-o", $"UserKnownHostsFile={knownHosts}",
                    "-i", keyPath, "-o", "IdentitiesOnly=yes",
                    "-o", "PreferredAuthentications=publickey", "-o", "PasswordAuthentication=no",
                    "-o", "BatchMode=yes", "-o", "ConnectTimeout=10", "-v",
                    "-b", batch, "hermoduser@127.0.0.1"
                })
                    client.StartInfo.ArgumentList.Add(arg);

                // Read the pipes without the test token so we still get the log if the run is cancelled.
                client.Start();
                var stdoutTask = client.StandardOutput.ReadToEndAsync();
                var stderrTask = client.StandardError.ReadToEndAsync();

                try
                {

                    var exited = await Task.WhenAny(client.WaitForExitAsync(CancellationToken), Task.Delay(TimeSpan.FromSeconds(25), CancellationToken)).ConfigureAwait(false);
                    if (!client.HasExited)
                    {
                        try { client.Kill(true); } catch { }
                        var log = await stderrTask;
                        Assert.Fail($"sftp did not finish in time — stalled.\nserver error: {serverError}\nsftp -v log:\n{log}");
                    }

                    try { await serverTask; } catch { }
                    var stderr = await stderrTask;

                    Assert.Multiple(() => {
                        Assert.That(client.ExitCode, Is.EqualTo(0), $"sftp should succeed. stderr:\n{stderr}");
                        Assert.That(File.Exists(Path.Combine(root, "uploaded.bin")), Is.True, "the put landed under our jail root");
                        Assert.That(File.ReadAllBytes(Path.Combine(root, "uploaded.bin")), Is.EqualTo(payload), "uploaded bytes match");
                        Assert.That(File.Exists(localDst), Is.True, "the get produced a local file");
                        Assert.That(File.ReadAllBytes(localDst), Is.EqualTo(payload), "downloaded bytes match");
                    });

                }
                catch (AssertionException) { throw; }
                catch (Exception e)
                {
                    try { if (!client.HasExited) client.Kill(true); } catch { }
                    var log = await stderrTask;
                    throw new AssertionException($"OpenSSH sftp interop failed:\n{e.Message}\nsftp -v log:\n" + log, e);
                }

            }
            finally
            {
                try { Directory.Delete(workDir, recursive: true); } catch { }
            }

        }

        #endregion

    }

}
