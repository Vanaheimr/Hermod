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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>M8: ProxyJump / SSH-over-SSH — reach a TCP target through one (and two) in-process bastions, with end-to-end target host-key verification and auth.</summary>
    [TestFixture]
    public class ProxyJumpTests
    {

        // A TCP-listening SSH server that runs an exec handler echoing the command.
        private static (SshTcpListener Listener, Int32 Port, Task Run) StartTargetSshServer(ISshHostKey HostKey, Byte[] UserPublicKey, String Label, CancellationToken CancellationToken)
        {
            var listener = SshTcpListener.Start(new IPSocket(IPv4Address.Localhost, IPPort.Auto));
            var port     = listener.LocalEndPoint.Port.ToInt32();
            var run = Task.Run(async () =>
            {
                try
                {
                    var pipe = await listener.AcceptAsync(CancellationToken);
                    using var t = await SshTransport.ServerHandshakeAsync(pipe, HostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, SshUserAuthenticator.ForAuthorizedKeys(UserPublicKey), CancellationToken: CancellationToken);
                    await SshConnection.ServeExecAsync(t, "achim", async (ctx, ct) =>
                    {
                        await ctx.WriteAsync($"{Label} ran: {ctx.Command}\n", ct);
                        return 0;
                    }, CancellationToken);
                }
                catch { }
            }, CancellationToken);
            return (listener, port, run);
        }

        // A bastion SSH server (over a pipe) that serves a single direct-tcpip forward under the given policy.
        private static Task StartBastion(IDuplexPipe ServerPipe, ISshHostKey HostKey, Byte[] UserPublicKey, ForwardingPolicy Policy, CancellationToken CancellationToken)
            => Task.Run(async () =>
               {
                   try
                   {
                       using var t = await SshTransport.ServerHandshakeAsync(ServerPipe, HostKey, CancellationToken: CancellationToken);
                       await UserAuthentication.ServerAuthenticateAsync(t, SshUserAuthenticator.ForAuthorizedKeys(UserPublicKey), CancellationToken: CancellationToken);
                       await SshForwarding.ServeDirectTcpIpAsync(t, Policy, CancellationToken: CancellationToken);
                   }
                   catch { }
               }, CancellationToken);


        #region ProxyJump_OneBastion_ExecOnTarget

        [Test]
        [CancelAfter(25000)]
        public async Task ProxyJump_OneBastion_ExecOnTarget(CancellationToken CancellationToken)
        {

            var bastionHostKey = Ed25519KeyPair.Generate();
            var bastionUserKey = SshHostKey.GenerateEd25519();
            var targetHostKey  = Ed25519KeyPair.Generate();
            var targetUserKey  = SshHostKey.GenerateEd25519();

            var (targetListener, targetPort, targetRun) = StartTargetSshServer(targetHostKey, targetUserKey.PublicKeyBlob, "target", CancellationToken);

            try
            {

                var (clientPipe, bastionPipe) = DuplexPipe.CreateConnectedPair();
                var policy   = ForwardingPolicy.Custom(NetworkAcl.DenyByDefault().Allow(Cidr: "127.0.0.1/32", Ports: targetPort.ToString()));
                var bastion  = StartBastion(bastionPipe, bastionHostKey, bastionUserKey.PublicKeyBlob, policy, CancellationToken);

                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", bastionUserKey, CancellationToken: CancellationToken);

                // Tunnel to the target through the bastion; verify the TARGET's host key end-to-end.
                await using var hop = await SshProxyJump.ConnectThroughAsync(
                                          client, "127.0.0.1", (UInt16) targetPort,
                                          VerifyHostKey: blob => blob.AsSpan().SequenceEqual(targetHostKey.PublicKeyBlob),
                                          CancellationToken: CancellationToken);

                await UserAuthentication.ClientPublicKeyAuthenticateAsync(hop.Transport, "achim", targetUserKey, CancellationToken: CancellationToken);
                var result = await SshConnection.ExecuteAsync(hop.Transport, "whoami", CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(result.ExitCode,       Is.EqualTo(0));
                    Assert.That(result.StandardOutput, Is.EqualTo("target ran: whoami\n"), "exec ran on the target, reached through the bastion tunnel");
                });

            }
            finally
            {
                targetListener.Dispose();
            }

        }

        #endregion

        #region ProxyJump_WrongTargetHostKey_IsRejected

        [Test]
        [CancelAfter(25000)]
        public async Task ProxyJump_WrongTargetHostKey_IsRejected(CancellationToken CancellationToken)
        {

            var bastionHostKey = Ed25519KeyPair.Generate();
            var bastionUserKey = SshHostKey.GenerateEd25519();
            var targetHostKey  = Ed25519KeyPair.Generate();
            var targetUserKey  = SshHostKey.GenerateEd25519();

            var (targetListener, targetPort, _) = StartTargetSshServer(targetHostKey, targetUserKey.PublicKeyBlob, "target", CancellationToken);

            try
            {

                var (clientPipe, bastionPipe) = DuplexPipe.CreateConnectedPair();
                var policy  = ForwardingPolicy.Custom(NetworkAcl.DenyByDefault().Allow(Cidr: "127.0.0.1/32", Ports: targetPort.ToString()));
                _ = StartBastion(bastionPipe, bastionHostKey, bastionUserKey.PublicKeyBlob, policy, CancellationToken);

                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", bastionUserKey, CancellationToken: CancellationToken);

                // The target presents a key we do NOT trust → the tunneled handshake must fail.
                Assert.CatchAsync(async () => await SshProxyJump.ConnectThroughAsync(
                                                  client, "127.0.0.1", (UInt16) targetPort,
                                                  VerifyHostKey: _ => false,
                                                  CancellationToken: CancellationToken));

            }
            finally
            {
                targetListener.Dispose();
            }

        }

        #endregion

        #region JumpHost_Parse

        [Test]
        public void JumpHost_Parse()
        {
            Assert.Multiple(() => {
                var a = SshJumpHost.Parse("achim@bastion:2222");
                Assert.That(a.Username, Is.EqualTo("achim"));
                Assert.That(a.Host,     Is.EqualTo("bastion"));
                Assert.That(a.Port,     Is.EqualTo(2222));

                var b = SshJumpHost.Parse("gateway");
                Assert.That(b.Username, Is.Null);
                Assert.That(b.Host,     Is.EqualTo("gateway"));
                Assert.That(b.Port,     Is.EqualTo(22));

                var chain = SshJumpHost.ParseChain("achim@b1:22, b2");
                Assert.That(chain.Select(h => h.Host), Is.EqualTo(new[] { "b1", "b2" }));
            });
        }

        #endregion

    }

}
