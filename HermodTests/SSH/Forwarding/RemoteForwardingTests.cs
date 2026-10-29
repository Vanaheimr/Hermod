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
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;
using org.GraphDefined.Vanaheimr.Hermod.Tests;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>
    /// M8/mux: remote (<c>-R</c>) forwarding — an external client reaches a client-side service through the server's forwarded listener.
    /// </summary>
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


        #region RemoteForward_ReachesClientSideService_ThroughServerListener

        [Test]
        [CancelAfter(25000)]
        public async Task RemoteForward_ReachesClientSideService_ThroughServerListener(CancellationToken CancellationToken)
        {

            var (echo, echoPort) = StartEchoServer(CancellationToken);   // the client-side target service

            // Where the server should listen: a port chosen in advance, as the
            // policy names it, and held by the test until the server binds it -
            // rather than one that was free a moment before, and with other test
            // runs on the same machine was now and then given to one of them.
            using var bindPort   = new ClosedPort();

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

                // The server binds 127.0.0.1 when it is asked to forward, next to
                // what keeps the port held while it is handed over.
                bindPort.HandOver();

                // ssh -R bindPort:127.0.0.1:echoPort — the server listens, we relay to our local echo service.
                await using var forward = await SshRemoteForwarding.RequestRemoteForwardAsync(
                                              clientMux, "127.0.0.1", bindPort.Number.ToUInt16(), "127.0.0.1", (UInt16) echoPort, CancellationToken);

                // An external client connects to the SERVER's forwarded port and must reach our echo service.
                using var external = new TcpClient();
                await external.ConnectAsync(IPAddress.Loopback, bindPort.Number.ToInt32(), CancellationToken);
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


        #region (private) the façade pair the OpenSSH-shaped tests use

        // A real SshServer, so the remote-forward responder sits next to the host-key proofs exactly as in
        // production, and a client whose multiplexer the tests drive by hand to see the raw replies.
        private static async Task<(SshServer Server, SshClient Client)> ConnectAsync(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();
            var hostKey = SshHostKey.GenerateEd25519();

            var server  = new SshServer(new SshServerOptions {
                HostKeys           = [ hostKey ],
                Authenticator      = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ForwardingPolicy   = ForwardingPolicy.LoopbackOnly,
                AdvertiseHostKeys  = true
            });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            var client  = await SshClient.ConnectAsync("127.0.0.1", (UInt16) server.LocalEndPoint.Port.ToInt32(), new SshClientOptions {
                Username       = "achim",
                VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                Credentials    = [ userKey ]
            }, CancellationToken);

            return (server, client);

        }

        private static Byte[] AddressAndPort(String Address, UInt32 Port)
        {
            var abw = new System.Buffers.ArrayBufferWriter<Byte>();
            var w   = new SshPacketWriter(abw);
            w.WriteString(Address);
            w.WriteUInt32(Port);
            return abw.WrittenSpan.ToArray();
        }

        /// <summary>
        /// Connect to the server's forwarded port and return the address and port the server named in the
        /// resulting <c>forwarded-tcpip</c> channel.
        /// </summary>
        private static async Task<(String Address, UInt32 Port)> ForwardedChannelForAsync(SshChannelMultiplexer Mux, Int32 Port, CancellationToken CancellationToken)
        {

            var opened = new TaskCompletionSource<Byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            Mux.ChannelAcceptor = info => {
                if (info.ChannelType == "forwarded-tcpip")
                    opened.TrySetResult(info.TypeData);
                return ValueTask.FromResult(info.ChannelType == "forwarded-tcpip");
            };

            using var external = new TcpClient();
            await external.ConnectAsync(IPAddress.Loopback, Port, CancellationToken);

            var typeData = await opened.Task.WaitAsync(CancellationToken);
            var reader   = new SshPacketReader(typeData);

            return (reader.ReadString(), reader.ReadUInt32());

        }

        #endregion

        #region RemoteForward_OnPortZero_ReportsTheAllocatedPort

        /// <summary>
        /// <c>ssh -R 0:host:port</c>: the server chooses the port, says which in its <c>REQUEST_SUCCESS</c>
        /// (RFC 4254 §7.1), and names that port — never 0 — in the <c>forwarded-tcpip</c> channel, since
        /// OpenSSH refuses a channel that matches no forward it asked for. The reply travels through the
        /// same responder slot as the host-key proofs, which this server also serves.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task RemoteForward_OnPortZero_ReportsTheAllocatedPort(CancellationToken CancellationToken)
        {

            var (server, client) = await ConnectAsync(CancellationToken);

            try
            {

                var reply = await client.Multiplexer.SendGlobalRequestWithReplyAsync("tcpip-forward", true, AddressAndPort("127.0.0.1", 0), CancellationToken);

                Assert.That(reply.Success,     Is.True,              "a loopback forward on a chosen port is permitted");
                Assert.That(reply.Data.Length, Is.EqualTo(4),        "the reply to port 0 carries the allocated port");

                var allocated = new SshPacketReader(reply.Data).ReadUInt32();
                Assert.That(allocated,         Is.InRange(1u, 65535u));

                var (address, port) = await ForwardedChannelForAsync(client.Multiplexer, (Int32) allocated, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(address, Is.EqualTo("127.0.0.1"), "the channel names the address as the client spelled it");
                    Assert.That(port,    Is.EqualTo(allocated),   "the channel names the allocated port, not 0");
                });

            }
            finally
            {
                await client.DisposeAsync();
                await server.DisposeAsync();
            }

        }

        #endregion

        #region RemoteForward_BindsLocalhostByName

        /// <summary>
        /// A plain <c>ssh -R 8080:host:80</c> sends the bind address <c>"localhost"</c>. It has to bind the
        /// loopback addresses — and the channel has to say <c>"localhost"</c> back, or OpenSSH does not
        /// recognise it as its own forward.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task RemoteForward_BindsLocalhostByName(CancellationToken CancellationToken)
        {

            var (server, client) = await ConnectAsync(CancellationToken);

            try
            {

                var reply = await client.Multiplexer.SendGlobalRequestWithReplyAsync("tcpip-forward", true, AddressAndPort("localhost", 0), CancellationToken);
                Assert.That(reply.Success, Is.True, "'localhost' is what ssh -R sends without a bind address");

                var allocated       = new SshPacketReader(reply.Data).ReadUInt32();
                var (address, port) = await ForwardedChannelForAsync(client.Multiplexer, (Int32) allocated, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(address, Is.EqualTo("localhost"));
                    Assert.That(port,    Is.EqualTo(allocated));
                });

            }
            finally
            {
                await client.DisposeAsync();
                await server.DisposeAsync();
            }

        }

        #endregion

        #region RemoteForward_RefusesWhatItMustNotBind

        /// <summary>
        /// The other bind addresses keep their meaning: a name other than <c>localhost</c> is not resolved
        /// on the client's behalf, and the wildcard (<c>""</c> / <c>"*"</c>, every address) needs a policy
        /// that permits it — a loopback-only one does not.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task RemoteForward_RefusesWhatItMustNotBind(CancellationToken CancellationToken)
        {

            var (server, client) = await ConnectAsync(CancellationToken);

            try
            {

                foreach (var bindAddress in new[] { "example.com", "", "*", "0.0.0.0" })
                {
                    var reply = await client.Multiplexer.SendGlobalRequestWithReplyAsync("tcpip-forward", true, AddressAndPort(bindAddress, 0), CancellationToken);
                    Assert.That(reply.Success, Is.False, $"bind address '{bindAddress}' must be refused under a loopback-only policy");
                }

            }
            finally
            {
                await client.DisposeAsync();
                await server.DisposeAsync();
            }

        }

        #endregion

        #region CancelTcpIpForward_StopsTheListener

        /// <summary>
        /// <c>cancel-tcpip-forward</c> (RFC 4254 §7.1) closes the server's listener; cancelling a forward
        /// that does not exist is refused.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task CancelTcpIpForward_StopsTheListener(CancellationToken CancellationToken)
        {

            var (server, client) = await ConnectAsync(CancellationToken);

            try
            {

                var mux       = client.Multiplexer;
                var reply     = await mux.SendGlobalRequestWithReplyAsync("tcpip-forward", true, AddressAndPort("127.0.0.1", 0), CancellationToken);
                var allocated = new SshPacketReader(reply.Data).ReadUInt32();

                Assert.That(await mux.SendGlobalRequestAsync("cancel-tcpip-forward", true, AddressAndPort("127.0.0.1", allocated), CancellationToken),
                            Is.True, "an existing forward can be cancelled");

                Assert.That(await mux.SendGlobalRequestAsync("cancel-tcpip-forward", true, AddressAndPort("127.0.0.1", allocated), CancellationToken),
                            Is.False, "a forward that is gone cannot be cancelled twice");

                using var external = new TcpClient();
                await Assert.CatchAsync<SocketException>(async () => await external.ConnectAsync(IPAddress.Loopback, (Int32) allocated, CancellationToken),
                                                   "nothing listens on a cancelled forward");

            }
            finally
            {
                await client.DisposeAsync();
                await server.DisposeAsync();
            }

        }

        #endregion

    }

}
