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

using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M6 streaming remote commands: <see cref="SshConnection.StartCommandAsync"/> yields a live
    /// <see cref="SshCommandProcess"/> whose stdout streams incrementally, whose stdin is piped to the
    /// remote command, and whose exit status is awaited — all against our own concurrent streaming server.
    /// </summary>
    [TestFixture]
    public class StreamingCommandTests
    {

        #region (helper) StartServer / ClientLogin

        private static Task StartServer(IDuplexPipe        ServerPipe,
                                        Ed25519KeyPair     HostKey,
                                        Byte[]             UserPublicKey,
                                        SshExecHandler     Handler,
                                        CancellationToken  CancellationToken)

            => Task.Run(async () =>
               {
                   try
                   {
                       using var t = await SshTransport.ServerHandshakeAsync(ServerPipe, HostKey, CancellationToken: CancellationToken);
                       await UserAuthentication.ServerAuthenticateAsync(t, SshUserAuthenticator.ForAuthorizedKeys(UserPublicKey), CancellationToken: CancellationToken);
                       await SshConnection.ServeCommandAsync(t, "achim", Handler, CancellationToken: CancellationToken);
                   }
                   catch { /* torn down with the client */ }
               }, CancellationToken);

        private static async Task<SshTransport> ClientLogin(IDuplexPipe ClientPipe, ISshHostKey UserKey, CancellationToken CancellationToken)
        {
            var t = await SshTransport.ClientHandshakeAsync(ClientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(t, "achim", UserKey, CancellationToken: CancellationToken);
            return t;
        }

        #endregion


        #region Streaming_StdoutArrivesIncrementally_ThenExitStatus

        [Test]
        [CancelAfter(20000)]
        public async Task Streaming_StdoutArrivesIncrementally_ThenExitStatus(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            var release = new SemaphoreSlim(0);   // lets the test gate the server between chunks

            var server = StartServer(serverPipe, hostKey, userKey.PublicKeyBlob, async (context, ct) =>
            {
                for (var i = 1; i <= 3; i++)
                {
                    await release.WaitAsync(ct);
                    await context.WriteAsync($"chunk{i}\n", ct);
                }
                return 5;
            }, CancellationToken);

            using var client = await ClientLogin(clientPipe, userKey, CancellationToken);
            await using var cmd = await SshConnection.StartCommandAsync(client, new SshCommand("stream"), CancellationToken: CancellationToken);

            var reader = new StreamReader(cmd.StandardOutput, Encoding.UTF8);

            // Each line only appears after we release the corresponding server chunk — proving it streams.
            for (var i = 1; i <= 3; i++)
            {
                release.Release();
                var line = await reader.ReadLineAsync(CancellationToken);
                Assert.That(line, Is.EqualTo($"chunk{i}"));
            }

            var exit = await cmd.WaitForExitAsync(CancellationToken);
            Assert.That(exit, Is.EqualTo(5));

            await server;

        }

        #endregion

        #region Streaming_StdinIsPipedToRemote_AndEchoed

        [Test]
        [CancelAfter(20000)]
        public async Task Streaming_StdinIsPipedToRemote_AndEchoed(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            // The "command" is a byte-count: read all of stdin, echo the count, exit with it (mod 256).
            var payload = RandomNumberGenerator.GetBytes(50_000);

            var server = StartServer(serverPipe, hostKey, userKey.PublicKeyBlob, async (context, ct) =>
            {
                using var buffer = new MemoryStream();
                await context.StandardInput.CopyToAsync(buffer, ct);
                await context.WriteAsync($"{buffer.Length}", ct);
                return (Int32) (buffer.Length % 256);
            }, CancellationToken);

            using var client = await ClientLogin(clientPipe, userKey, CancellationToken);
            await using var cmd = await SshConnection.StartCommandAsync(
                                      client,
                                      new SshCommand("wc -c") { Input = new MemoryStream(payload) },
                                      CancellationToken: CancellationToken);

            var echoed = await new StreamReader(cmd.StandardOutput).ReadToEndAsync(CancellationToken);
            var exit   = await cmd.WaitForExitAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(echoed, Is.EqualTo(payload.Length.ToString()));
                Assert.That(exit,   Is.EqualTo(payload.Length % 256));
            });

            await server;

        }

        #endregion

        #region Streaming_LargeOutput_FlowsThroughWindow

        [Test]
        [CancelAfter(20000)]
        public async Task Streaming_LargeOutput_FlowsThroughWindow(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            var chunk      = RandomNumberGenerator.GetBytes(64 * 1024);
            const Int32 n  = 32;   // 2 MiB — exceeds a single window, exercising CHANNEL_WINDOW_ADJUST

            var server = StartServer(serverPipe, hostKey, userKey.PublicKeyBlob, async (context, ct) =>
            {
                for (var i = 0; i < n; i++)
                    await context.WriteAsync(chunk, ct);
                return 0;
            }, CancellationToken);

            using var client = await ClientLogin(clientPipe, userKey, CancellationToken);
            await using var cmd = await SshConnection.StartCommandAsync(client, new SshCommand("blast"), CancellationToken: CancellationToken);

            using var sink = new MemoryStream();
            await cmd.StandardOutput.CopyToAsync(sink, CancellationToken);
            var exit = await cmd.WaitForExitAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(sink.Length, Is.EqualTo((Int64) n * chunk.Length));
                Assert.That(exit,        Is.EqualTo(0));
            });

            await server;

        }

        #endregion

    }

}
