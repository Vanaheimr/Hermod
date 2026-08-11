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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M6 connection liveness, end-to-end over a loopback pipe: keepalive probing and the idle timeout wired
    /// into a live <see cref="SshCommandProcess"/>. The exact "N probes, not one short" counting is proven
    /// deterministically in <see cref="SshLivenessMonitorTests"/>; here we verify the real integration —
    /// probes get answered, the idle timer still fires on a responsive-but-silent peer, and ordinary traffic
    /// keeps a session healthy.
    /// </summary>
    [TestFixture]
    [Category("Slow")]
    public class KeepAliveTests
    {

        #region Idle_ResponsivePeer_StillDisconnectsOnTimeout

        [Test]
        [CancelAfter(30000)]
        public async Task Idle_ResponsivePeer_StillDisconnectsOnTimeout(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            using var serverStop = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            // The server answers keepalives (via its receive loop) but the command produces no output —
            // a healthy but silent peer.
            var server = Task.Run(async () =>
            {
                try
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob), CancellationToken: CancellationToken);
                    await SshConnection.ServeCommandAsync(t, "achim", async (context, ct) =>
                    {
                        try { await Task.Delay(Timeout.InfiniteTimeSpan, serverStop.Token); } catch { }
                        return 0;
                    }, CancellationToken: CancellationToken);
                }
                catch { }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);

            var options = new SshConnectionOptions
            {
                KeepAliveInterval  = TimeSpan.FromMilliseconds(200),
                KeepAliveCountMax  = 10,                          // high, so keepalives alone never trip it
                IdleTimeout        = TimeSpan.FromSeconds(2)
            };

            await using var cmd = await SshConnection.StartCommandAsync(client, new SshCommand("sleep"), options, CancellationToken);

            var lost = Assert.CatchAsync<SshConnectionLostException>(async () => await cmd.WaitForExitAsync(CancellationToken));
            Assert.That(lost!.WasIdleTimeout, Is.True, "the disconnect must be attributed to the idle timeout");

            serverStop.Cancel();

        }

        #endregion

        #region KeepAlive_ActiveCommand_StaysHealthy

        [Test]
        [CancelAfter(30000)]
        public async Task KeepAlive_ActiveCommand_StaysHealthy(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            // The command dribbles output every 100 ms for ~1 s, then exits cleanly. With keepalives on and
            // a modest idle timeout, the steady traffic must keep the session alive to a normal exit.
            var server = Task.Run(async () =>
            {
                try
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob), CancellationToken: CancellationToken);
                    await SshConnection.ServeCommandAsync(t, "achim", async (context, ct) =>
                    {
                        for (var i = 0; i < 10; i++)
                        {
                            await context.WriteLineAsync($"tick {i}", ct);
                            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
                        }
                        return 0;
                    }, CancellationToken: CancellationToken);
                }
                catch { }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);

            var options = new SshConnectionOptions
            {
                KeepAliveInterval  = TimeSpan.FromMilliseconds(150),
                KeepAliveCountMax  = 5,
                IdleTimeout        = TimeSpan.FromSeconds(3)      // longer than any single inter-tick gap
            };

            await using var cmd = await SshConnection.StartCommandAsync(client, new SshCommand("ticker"), options, CancellationToken);

            using var sink = new MemoryStream();
            await cmd.StandardOutput.CopyToAsync(sink, CancellationToken);
            var exit = await cmd.WaitForExitAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(exit, Is.EqualTo(0));
                Assert.That(System.Text.Encoding.UTF8.GetString(sink.ToArray()), Does.Contain("tick 9"));
            });

            await server;

        }

        #endregion

    }

}
