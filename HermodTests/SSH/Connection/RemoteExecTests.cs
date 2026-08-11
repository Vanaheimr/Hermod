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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>M6 connection layer: remote command execution (open session, exec, capture, exit-status).</summary>
    [TestFixture]
    public class RemoteExecTests
    {

        #region Exec_CapturesStdoutStderrAndExitCode

        [Test]
        [CancelAfter(15000)]
        public async Task Exec_CapturesStdoutStderrAndExitCode(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                await SshConnection.ServeExecAsync(t, "achim", async (context, ct) =>
                {
                    await context.WriteLineAsync($"you ran: {context.Command}", ct);
                    await context.WriteErrorAsync(Encoding.UTF8.GetBytes("a warning\n"), ct);
                    return 7;
                }, CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                Assert.That(await UserAuthentication.ClientPublicKeyAuthenticateAsync(t, "achim", userKey, CancellationToken: CancellationToken), Is.True);
                return await SshConnection.ExecuteAsync(t, "uname -a", CancellationToken);
            }, CancellationToken);

            var result = await clientRun;
            await serverRun;

            Assert.Multiple(() => {
                Assert.That(result.ExitCode,       Is.EqualTo(7));
                Assert.That(result.StandardOutput, Is.EqualTo("you ran: uname -a\n"));
                Assert.That(result.StandardError,  Does.Contain("a warning"));
                Assert.That(result.Success,        Is.False);
            });

        }

        #endregion

        #region Exec_LargeOutput_IsCapturedFully

        [Test]
        [CancelAfter(15000)]
        public async Task Exec_LargeOutput_IsCapturedFully(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            // ~200 KiB of output crosses several 32 KiB channel packets.
            var line = new String('x', 1000) + "\n";
            const Int32 lineCount = 200;

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                await SshConnection.ServeExecAsync(t, "achim", async (context, ct) =>
                {
                    for (var i = 0; i < lineCount; i++)
                        await context.WriteAsync(line, ct);
                    return 0;
                }, CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(t, "achim", userKey, CancellationToken: CancellationToken);
                return await SshConnection.ExecuteAsync(t, "generate", CancellationToken);
            }, CancellationToken);

            var result = await clientRun;
            await serverRun;

            Assert.Multiple(() => {
                Assert.That(result.ExitCode,              Is.EqualTo(0));
                Assert.That(result.StandardOutputBytes.Length, Is.EqualTo(line.Length * lineCount));
                Assert.That(result.StandardOutput,        Is.EqualTo(String.Concat(Enumerable.Repeat(line, lineCount))));
            });

        }

        #endregion

    }

}
