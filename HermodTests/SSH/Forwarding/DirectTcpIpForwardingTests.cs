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

using System.Net;
using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>M8: <c>direct-tcpip</c> port forwarding — an allowed tunnel reaches an in-process echo server, a denied destination is refused, and the session survives the denial.</summary>
    [TestFixture]
    public class DirectTcpIpForwardingTests
    {

        // A tiny in-process TCP echo server on loopback; returns its port.
        private static (TcpListener Listener, Int32 Port) StartEchoServer(CancellationToken CancellationToken)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint) listener.LocalEndpoint).Port;

            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var conn = await listener.AcceptTcpClientAsync(CancellationToken);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var s = conn.GetStream();
                                var buffer = new Byte[4096];
                                int n;
                                while ((n = await s.ReadAsync(buffer, CancellationToken)) > 0)
                                    await s.WriteAsync(buffer.AsMemory(0, n), CancellationToken);
                            }
                            catch { }
                        }, CancellationToken);
                    }
                }
                catch { }
            }, CancellationToken);

            return (listener, port);
        }


        #region DirectTcpIp_Allowed_ReachesEcho_Denied_IsRefused

        [Test]
        [CancelAfter(20000)]
        public async Task DirectTcpIp_Allowed_ReachesEcho_Denied_IsRefused(CancellationToken CancellationToken)
        {

            var (echo, echoPort) = StartEchoServer(CancellationToken);

            try
            {

                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                var hostKey = Ed25519KeyPair.Generate();
                var userKey = SshHostKey.GenerateEd25519();
                var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

                // Permit only loopback on exactly the echo port.
                var policy = ForwardingPolicy.Custom(NetworkAcl.DenyByDefault().Allow(Cidr: "127.0.0.1/32", Ports: echoPort.ToString()));

                var server = Task.Run(async () =>
                {
                    try
                    {
                        using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                        await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                        await SshForwarding.ServeDirectTcpIpAsync(t, policy, CancellationToken: CancellationToken);
                    }
                    catch { }
                }, CancellationToken);

                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);

                // A destination outside the ACL is refused — and the session stays usable.
                var refused = Assert.CatchAsync<SshForwardingException>(async () =>
                    await SshForwarding.OpenTcpStreamAsync(client, "127.0.0.1", (UInt16) (echoPort + 1), CancellationToken));

                // The permitted destination tunnels through to the echo server.
                var stream  = await SshForwarding.OpenTcpStreamAsync(client, "127.0.0.1", (UInt16) echoPort, CancellationToken);
                var sent    = Encoding.UTF8.GetBytes("ping through the tunnel");
                await stream.WriteAsync(sent, CancellationToken);

                var received = new Byte[sent.Length];
                await stream.ReadExactlyAsync(received, CancellationToken);

                await stream.DisposeAsync();
                await server;

                Assert.Multiple(() => {
                    Assert.That(refused!.Message, Does.Contain("administratively prohibited").IgnoreCase);
                    Assert.That(received, Is.EqualTo(sent), "the echo came back byte-for-byte through the forward");
                });

            }
            finally
            {
                echo.Stop();
            }

        }

        #endregion

    }

}
