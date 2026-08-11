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

    /// <summary>M8/mux: remote (<c>-R</c>) forwarding — an external client reaches a client-side service through the server's forwarded listener.</summary>
    [TestFixture]
    public class RemoteForwardingTests
    {

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

        private static Int32 FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var p = ((IPEndPoint) l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }


        #region RemoteForward_ReachesClientSideService_ThroughServerListener

        [Test]
        [CancelAfter(25000)]
        public async Task RemoteForward_ReachesClientSideService_ThroughServerListener(CancellationToken CancellationToken)
        {

            var (echo, echoPort) = StartEchoServer(CancellationToken);   // the client-side target service
            var bindPort         = FreePort();                            // where the server should listen

            try
            {

                var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
                var hostKey = Ed25519KeyPair.Generate();
                var userKey = SshHostKey.GenerateEd25519();
                var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

                var policy = ForwardingPolicy.Custom(TcpIpForward: NetworkAcl.DenyByDefault().Allow(Cidr: "127.0.0.1/32", Ports: bindPort.ToString()));

                var serverReady = new TaskCompletionSource();
                var serverDone  = new TaskCompletionSource();
                var server = Task.Run(async () =>
                {
                    try
                    {
                        using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                        await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                        await using var mux = new SshChannelMultiplexer(t).Start();
                        SshRemoteForwarding.ServeRemoteForwards(mux, policy, CancellationToken);
                        serverReady.SetResult();
                        await serverDone.Task.WaitAsync(CancellationToken);
                    }
                    catch { serverReady.TrySetResult(); }
                }, CancellationToken);

                using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);
                await using var clientMux = new SshChannelMultiplexer(client).Start();

                await serverReady.Task.WaitAsync(CancellationToken);

                // ssh -R bindPort:127.0.0.1:echoPort — the server listens, we relay to our local echo service.
                await using var forward = await SshRemoteForwarding.RequestRemoteForwardAsync(
                                              clientMux, "127.0.0.1", (UInt16) bindPort, "127.0.0.1", (UInt16) echoPort, CancellationToken);

                // An external client connects to the SERVER's forwarded port and must reach our echo service.
                using var external = new TcpClient();
                await external.ConnectAsync(IPAddress.Loopback, bindPort, CancellationToken);
                var stream  = external.GetStream();
                var payload = Encoding.UTF8.GetBytes("through the reverse tunnel");
                await stream.WriteAsync(payload, CancellationToken);

                var echoed = new Byte[payload.Length];
                await stream.ReadExactlyAsync(echoed, CancellationToken);

                serverDone.SetResult();
                await server;

                Assert.That(echoed, Is.EqualTo(payload), "the echo returned through the reverse (-R) tunnel");

            }
            finally
            {
                echo.Stop();
            }

        }

        #endregion

    }

}
