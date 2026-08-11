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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Interop for the host-key rotation extension: the real OpenSSH client, with
    /// <c>UpdateHostKeys=yes</c>, must accept our <c>hostkeys-00@openssh.com</c> advertisement, challenge
    /// the key it does not know, verify our <c>hostkeys-prove</c> signature and write the new key into
    /// its <c>known_hosts</c>. That end state is only reachable if our wire format <i>and</i> our
    /// signatures are byte-for-byte what OpenSSH expects.
    /// </summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.OpenSSH")]
    public class OpenSshHostKeyRotationInteropTests
    {

        private static String? FindOnPathOrSystem(String Tool)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                foreach (var name in new[] { Tool, Tool + ".exe" })
                    try { var c = Path.Combine(dir.Trim(), name); if (File.Exists(c)) return c; } catch { }
            var system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", Tool + ".exe");
            return File.Exists(system) ? system : null;
        }


        #region RealOpenSshClient_LearnsOurRotatedInHostKey

        [Test]
        [CancelAfter(40000)]
        public async Task RealOpenSshClient_LearnsOurRotatedInHostKey(CancellationToken CancellationToken)
        {

            var ssh       = FindOnPathOrSystem("ssh");
            var sshKeygen = FindOnPathOrSystem("ssh-keygen");
            if (ssh is null || sshKeygen is null)
                Assert.Ignore("No 'ssh'/'ssh-keygen' found.");

            var keyPath    = Path.Combine(Path.GetTempPath(), "hermod_hkr_" + Guid.NewGuid().ToString("N"));
            var knownHosts = Path.Combine(Path.GetTempPath(), "hermod_kh_"  + Guid.NewGuid().ToString("N"));

            // The key the client already trusts, and the one being rotated in.
            var currentKey = SshHostKey.GenerateEd25519();
            var rotatedIn  = SshHostKey.GenerateEcdsa(SshAlgorithmNames.HostKey.EcdsaNistP256);

            SshServer? server = null;

            try
            {

                using (var keygen = Process.Start(new ProcessStartInfo(sshKeygen!) { ArgumentList = { "-t", "ed25519", "-f", keyPath, "-N", "", "-q" }, UseShellExecute = false, CreateNoWindow = true })!)
                    await keygen.WaitForExitAsync(CancellationToken);

                var publicLine = (await File.ReadAllTextAsync(keyPath + ".pub", CancellationToken)).Split(' ');
                var publicBlob = Convert.FromBase64String(publicLine[1]);

                server = new SshServer(new SshServerOptions {
                    HostKeys          = [ currentKey, rotatedIn ],
                    Authenticator     = SshUserAuthenticator.ForAuthorizedKeys(publicBlob),
                    ExecHandler       = async (context, ct) => { await context.WriteAsync("rotated\n", ct); return 0; },
                    AdvertiseHostKeys = true
                });

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                var port = server.LocalEndPoint.Port.ToInt32();

                // Pre-trust only the current key, so ssh connects strictly and has a base entry to update.
                await File.WriteAllTextAsync(knownHosts,
                                             $"[127.0.0.1]:{port} ssh-ed25519 {Convert.ToBase64String(currentKey.PublicKeyBlob)}\n",
                                             CancellationToken);

                var emptyConf = Path.GetTempFileName();

                using var client = new Process { StartInfo = new ProcessStartInfo(ssh!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
                foreach (var arg in new[]
                {
                    "-F", emptyConf, "-p", port.ToString(),
                    "-o", "StrictHostKeyChecking=yes", "-o", $"UserKnownHostsFile={knownHosts}",
                    "-o", "UpdateHostKeys=yes",
                    "-i", keyPath, "-o", "IdentitiesOnly=yes",
                    "-o", "PreferredAuthentications=publickey", "-o", "PasswordAuthentication=no",
                    "-o", "BatchMode=yes", "-o", "ConnectTimeout=10",
                    "hermoduser@127.0.0.1", "hello"
                })
                    client.StartInfo.ArgumentList.Add(arg);

                try
                {

                    client.Start();
                    var stdoutTask = client.StandardOutput.ReadToEndAsync(CancellationToken);
                    var stderrTask = client.StandardError.ReadToEndAsync(CancellationToken);

                    await client.WaitForExitAsync(CancellationToken);

                    var stdout = await stdoutTask;
                    var stderr = await stderrTask;

                    // ssh writes the updated known_hosts as the connection winds down.
                    var rotatedBase64 = Convert.ToBase64String(rotatedIn.PublicKeyBlob);
                    var learned       = false;
                    for (var attempt = 0; attempt < 20 && !learned; attempt++)
                    {
                        learned = (await File.ReadAllTextAsync(knownHosts, CancellationToken)).Contains(rotatedBase64, StringComparison.Ordinal);
                        if (!learned)
                            await Task.Delay(100, CancellationToken);
                    }

                    var finalKnownHosts = await File.ReadAllTextAsync(knownHosts, CancellationToken);

                    Assert.Multiple(() => {

                        Assert.That(client.ExitCode, Is.EqualTo(0), $"ssh must succeed. stderr:\n{stderr}");
                        Assert.That(stdout,          Is.EqualTo("rotated\n"));

                        Assert.That(learned, Is.True,
                                    "real ssh must accept our hostkeys-prove signature and learn the rotated-in key.\n"
                                    + $"known_hosts:\n{finalKnownHosts}\nssh stderr:\n{stderr}");

                        Assert.That(finalKnownHosts, Does.Contain(Convert.ToBase64String(currentKey.PublicKeyBlob)),
                                    "the still-advertised current key must be retained");

                    });

                }
                catch (Exception e)
                {
                    try { if (!client.HasExited) client.Kill(true); } catch { }
                    throw new AssertionException("OpenSSH host-key rotation interop failed.", e);
                }
                finally
                {
                    try { File.Delete(emptyConf); } catch { }
                }

            }
            finally
            {
                if (server is not null)
                    await server.DisposeAsync();
                try { File.Delete(keyPath);          } catch { }
                try { File.Delete(keyPath + ".pub"); } catch { }
                try { File.Delete(knownHosts);       } catch { }
            }

        }

        #endregion

    }

}
