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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The end-to-end acceptance test for the connection layer: the real OpenSSH client logs in to our
    /// server with a key, runs a command, and receives our output and exit status.
    /// </summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.OpenSSH")]
    public class OpenSshExecInteropTests
    {

        private static String? FindOnPathOrSystem(String Tool)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                foreach (var name in new[] { Tool, Tool + ".exe" })
                    try { var c = Path.Combine(dir.Trim(), name); if (File.Exists(c)) return c; } catch { }
            var system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", Tool + ".exe");
            return File.Exists(system) ? system : null;
        }


        #region RealOpenSshClient_RunsCommand_OnOurServer

        [Test]
        [CancelAfter(30000)]
        [TestCase("hello",  0)]
        [TestCase("fail",  42)]
        public async Task RealOpenSshClient_RunsCommand_OnOurServer(String Command, Int32 ExpectedExit, CancellationToken CancellationToken)
        {

            var ssh        = FindOnPathOrSystem("ssh");
            var sshKeygen  = FindOnPathOrSystem("ssh-keygen");
            if (ssh is null || sshKeygen is null)
                Assert.Ignore("No 'ssh'/'ssh-keygen' found.");

            var keyPath = Path.Combine(Path.GetTempPath(), "hermod_exec_" + Guid.NewGuid().ToString("N"));

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

                var serverTask = Task.Run(async () =>
                {
                    var pipe = await listener.AcceptAsync(CancellationToken);
                    using var transport = await SshTransport.ServerHandshakeAsync(pipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(transport, authenticator, CancellationToken: CancellationToken);
                    await SshConnection.ServeExecAsync(transport, "hermoduser", async (context, ct) =>
                    {
                        await context.WriteAsync($"hermod ran: {context.Command}\n", ct);
                        return context.Command == "fail" ? 42 : 0;
                    }, CancellationToken);
                }, CancellationToken);

                var knownHosts = Path.GetTempFileName();
                var emptyConf  = Path.GetTempFileName();

                using var client = new Process { StartInfo = new ProcessStartInfo(ssh!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
                foreach (var arg in new[]
                {
                    "-F", emptyConf, "-p", port.ToString(),
                    "-o", "StrictHostKeyChecking=no", "-o", $"UserKnownHostsFile={knownHosts}",
                    "-i", keyPath, "-o", "IdentitiesOnly=yes",
                    "-o", "PreferredAuthentications=publickey", "-o", "PasswordAuthentication=no",
                    "-o", "BatchMode=yes", "-o", "ConnectTimeout=10",
                    "hermoduser@127.0.0.1", Command
                })
                    client.StartInfo.ArgumentList.Add(arg);

                try
                {

                    client.Start();
                    var stdoutTask = client.StandardOutput.ReadToEndAsync(CancellationToken);
                    var stderrTask = client.StandardError.ReadToEndAsync(CancellationToken);

                    await serverTask;
                    await client.WaitForExitAsync(CancellationToken);

                    var stdout = await stdoutTask;

                    Assert.Multiple(() => {
                        Assert.That(stdout,           Is.EqualTo($"hermod ran: {Command}\n"), "our command output must reach the ssh client's stdout");
                        Assert.That(client.ExitCode,  Is.EqualTo(ExpectedExit),               "ssh must propagate our exit-status");
                    });

                }
                catch (Exception e)
                {
                    try { if (!client.HasExited) client.Kill(true); } catch { }
                    var err = await client.StandardError.ReadToEndAsync(CancellationToken);
                    throw new AssertionException($"OpenSSH exec interop failed:\n{e.Message}\nssh stderr:\n" + err, e);
                }
                finally
                {
                    try { File.Delete(knownHosts); } catch { }
                    try { File.Delete(emptyConf);  } catch { }
                }

            }
            finally
            {
                try { File.Delete(keyPath);          } catch { }
                try { File.Delete(keyPath + ".pub"); } catch { }
            }

        }

        #endregion

    }

}
