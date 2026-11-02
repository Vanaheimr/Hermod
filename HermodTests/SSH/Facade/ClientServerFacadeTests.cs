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
using System.Security.Cryptography;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>
    /// The high-level SshClient/SshServer façade: one connection runs exec and a port-forward concurrently over the multiplexer.
    /// </summary>
    [TestFixture]
    public class ClientServerFacadeTests
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


        #region Facade_ExecAndForward_ConcurrentlyOnOneConnection

        [Test]
        [CancelAfter(25000)]
        public async Task Facade_ExecAndForward_ConcurrentlyOnOneConnection(CancellationToken CancellationToken)
        {

            var (echo, echoPort) = StartEchoServer(CancellationToken);

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys         = [ hostKey ],
                Authenticator    = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ExecHandler      = async (ctx, ct) => { await ctx.WriteAsync($"ran: {ctx.Command}\n", ct); return 0; },
                ForwardingPolicy = ForwardingPolicy.Custom(NetworkAcl.DenyByDefault().Allow(Cidr: "127.0.0.1/32", Ports: echoPort.ToString()))
            });

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                var port = (UInt16) server.LocalEndPoint.Port.ToInt32();

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username      = "achim",
                    VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                    Credentials   = [ userKey ]
                }, CancellationToken);

                // Kick off an exec and a tunnel at the same time — they must multiplex on the one connection.
                var execTask = client.ExecuteAsync("uname -a", CancellationToken).AsTask();

                await using var tunnel = await client.OpenTcpStreamAsync("127.0.0.1", (UInt16) echoPort, CancellationToken);
                var sent = Encoding.UTF8.GetBytes("through the multiplexed tunnel");
                await tunnel.WriteAsync(sent, CancellationToken);
                var back = new Byte[sent.Length];
                await tunnel.ReadExactlyAsync(back, CancellationToken);

                var result = await execTask;

                Assert.Multiple(() => {
                    Assert.That(result.ExitCode,       Is.EqualTo(0));
                    Assert.That(result.StandardOutput, Is.EqualTo("ran: uname -a\n"), "exec ran over the façade");
                    Assert.That(back,                  Is.EqualTo(sent),             "the forward echoed back over the same connection");
                });

                // A second exec on the same live connection still works.
                var second = await client.ExecuteAsync("whoami", CancellationToken);
                Assert.That(second.StandardOutput, Is.EqualTo("ran: whoami\n"));

            }
            finally
            {
                await server.DisposeAsync();
                echo.Stop();
            }

        }

        #endregion

        #region Facade_RemoteForward_ThroughTheServerListener

        /// <summary>
        /// <c>ssh -R localhost:0:host:port</c> through the façade, next to an exec on the same connection: the
        /// server chooses the port, a connection made there comes out at a target on the client's side, and
        /// after the cancel nothing listens there any more.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task Facade_RemoteForward_ThroughTheServerListener(CancellationToken CancellationToken)
        {

            var (echo, echoPort) = StartEchoServer(CancellationToken);

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys         = [ hostKey ],
                Authenticator    = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ExecHandler      = async (ctx, ct) => { await ctx.WriteAsync($"ran: {ctx.Command}\n", ct); return 0; },
                ForwardingPolicy = ForwardingPolicy.LoopbackOnly
            });

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                var port = (UInt16) server.LocalEndPoint.Port.ToInt32();

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username      = "achim",
                    VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                    Credentials   = [ userKey ]
                }, CancellationToken);

                var forward = await client.RequestRemoteForwardAsync("localhost", 0, "127.0.0.1", (UInt16) echoPort, CancellationToken);

                Assert.That(forward.BoundPort, Is.Not.Zero, "the port the server chose");

                var execTask = client.ExecuteAsync("uname -a", CancellationToken).AsTask();

                using (var external = new TcpClient())
                {

                    await external.ConnectAsync(IPAddress.Loopback, forward.BoundPort, CancellationToken);

                    var stream = external.GetStream();
                    var sent   = Encoding.UTF8.GetBytes("through the reverse tunnel");
                    await stream.WriteAsync(sent, CancellationToken);

                    var back = new Byte[sent.Length];
                    await stream.ReadExactlyAsync(back, CancellationToken);

                    Assert.That(back, Is.EqualTo(sent), "a connection to the server's port reached the client's target");

                }

                Assert.That((await execTask).StandardOutput, Is.EqualTo("ran: uname -a\n"), "exec ran beside the forward");

                Assert.That(await forward.CancelAsync(CancellationToken), Is.True, "the server stopped listening");

                using var late = new TcpClient();
                await Assert.CatchAsync<SocketException>(async () => await late.ConnectAsync(IPAddress.Loopback, forward.BoundPort, CancellationToken),
                                                         "nothing listens on a cancelled forward");

            }
            finally
            {
                await server.DisposeAsync();
                echo.Stop();
            }

        }

        #endregion

        #region Facade_LocalForward_ThroughTheServer

        /// <summary>
        /// <c>ssh -L 0:host:port</c> through the façade: this side listens on a port the OS chooses, two
        /// connections made there at once each reach the target through the server in a channel of their
        /// own, and after the forward is disposed nothing listens there any more.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task Facade_LocalForward_ThroughTheServer(CancellationToken CancellationToken)
        {

            var (echo, echoPort) = StartEchoServer(CancellationToken);

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys         = [ hostKey ],
                Authenticator    = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ForwardingPolicy = ForwardingPolicy.LoopbackOnly
            });

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                var port = (UInt16) server.LocalEndPoint.Port.ToInt32();

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username      = "achim",
                    VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                    Credentials   = [ userKey ]
                }, CancellationToken);

                var forward   = client.StartLocalForward(new IPSocket(IPv4Address.Localhost, IPPort.Auto), "127.0.0.1", (UInt16) echoPort);
                var localPort = forward.LocalEndPoint.Port.ToInt32();

                Assert.That(localPort, Is.Not.Zero, "the port the OS chose");

                async Task<String> RoundTripAsync(String Text)
                {

                    using var local = new TcpClient();
                    await local.ConnectAsync(IPAddress.Loopback, localPort, CancellationToken);

                    var stream = local.GetStream();
                    var sent   = Encoding.UTF8.GetBytes(Text);
                    await stream.WriteAsync(sent, CancellationToken);

                    var back = new Byte[sent.Length];
                    await stream.ReadExactlyAsync(back, CancellationToken);
                    return Encoding.UTF8.GetString(back);

                }

                var results = await Task.WhenAll(RoundTripAsync("first through the tunnel"),
                                                 RoundTripAsync("second through the tunnel"));

                Assert.That(results, Is.EqualTo(new[] { "first through the tunnel", "second through the tunnel" }),
                            "each connection reached the target, and got its own bytes back");

                await forward.DisposeAsync();

                using var late = new TcpClient();
                await Assert.CatchAsync<SocketException>(async () => await late.ConnectAsync(IPAddress.Loopback, localPort, CancellationToken),
                                                         "nothing listens on a disposed forward");

            }
            finally
            {
                await server.DisposeAsync();
                echo.Stop();
            }

        }

        #endregion

        #region Facade_LocalForward_EndsWithTheClient

        /// <summary>
        /// A local forward nobody disposes still ends with its client: the port is free again.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task Facade_LocalForward_EndsWithTheClient(CancellationToken CancellationToken)
        {

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys         = [ hostKey ],
                Authenticator    = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ForwardingPolicy = ForwardingPolicy.LoopbackOnly
            });

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                var port = (UInt16) server.LocalEndPoint.Port.ToInt32();

                var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username      = "achim",
                    VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                    Credentials   = [ userKey ]
                }, CancellationToken);

                var localPort = client.StartLocalForward(new IPSocket(IPv4Address.Localhost, IPPort.Auto), "127.0.0.1", 9).LocalEndPoint.Port.ToInt32();

                await client.DisposeAsync();

                // The accept loop lets go of the port once it sees the client's end; that takes a moment.
                while (true)
                {
                    try
                    {
                        using var probe = new TcpListener(IPAddress.Loopback, localPort);
                        probe.Start();
                        probe.Stop();
                        break;
                    }
                    catch (SocketException)
                    {
                        await Task.Delay(50, CancellationToken);
                    }
                }

                Assert.Throws<ObjectDisposedException>(() => client.StartLocalForward(new IPSocket(IPv4Address.Localhost, IPPort.Auto), "127.0.0.1", 9),
                                                       "a disposed client starts no forward");

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Facade_Sftp_ConcurrentWithExec_OverOneConnection

        [Test]
        [CancelAfter(25000)]
        public async Task Facade_Sftp_ConcurrentWithExec_OverOneConnection(CancellationToken CancellationToken)
        {

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();
            var fileSystem = new InMemorySftpFileSystem();

            var server = new SshServer(new SshServerOptions {
                HostKeys       = [ hostKey ],
                Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ExecHandler    = async (ctx, ct) => { await ctx.WriteAsync($"ran: {ctx.Command}\n", ct); return 0; },
                SftpFileSystem = fileSystem
            });

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                var port = (UInt16) server.LocalEndPoint.Port.ToInt32();

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username      = "achim",
                    VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                    Credentials   = [ userKey ]
                }, CancellationToken);

                var content = RandomNumberGenerator.GetBytes(60_000);   // multi-chunk

                // SFTP subsystem multiplexed alongside a concurrent exec on the same connection.
                var execTask = client.ExecuteAsync("status", CancellationToken).AsTask();

                var sftp = await client.OpenSftpClientAsync(CancellationToken);
                await sftp.UploadAsync("/device.bin", content, CancellationToken);
                var downloaded = await sftp.DownloadAsync("/device.bin", CancellationToken);
                var listing    = await sftp.ListDirectoryAsync("/", CancellationToken);
                await sftp.DisposeAsync();

                var result = await execTask;

                Assert.Multiple(() => {
                    Assert.That(downloaded, Is.EqualTo(content), "SFTP round-trip over the multiplexed subsystem channel");
                    Assert.That(listing.Select(e => e.Name), Does.Contain("device.bin"));
                    Assert.That(result.StandardOutput, Is.EqualTo("ran: status\n"), "exec ran concurrently with SFTP");
                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Facade_TheSecondCredential_IsTriedWhenTheFirstIsRefused

        [Test]
        [CancelAfter(25000)]
        public async Task Facade_TheSecondCredential_IsTriedWhenTheFirstIsRefused(CancellationToken CancellationToken)
        {

            var hostKey  = SshHostKey.GenerateEd25519();
            var otherKey = SshHostKey.GenerateEd25519();
            var userKey  = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys       = [ hostKey ],
                Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ExecHandler    = async (ctx, ct) => { await ctx.WriteAsync($"ran: {ctx.Command}\n", ct); return 0; }
            });

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                var port = (UInt16) server.LocalEndPoint.Port.ToInt32();

                // The key the server does not know comes first: the ssh-userauth service is asked
                // for once, and the second key goes straight to its USERAUTH_REQUEST.
                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username      = "achim",
                    VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                    Credentials   = [ otherKey, userKey ]
                }, CancellationToken);

                var result = await client.ExecuteAsync("whoami", CancellationToken);
                Assert.That(result.StandardOutput, Is.EqualTo("ran: whoami\n"));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Facade_NoCredentialAccepted_IsAnAuthenticationError

        [Test]
        [CancelAfter(25000)]
        public async Task Facade_NoCredentialAccepted_IsAnAuthenticationError(CancellationToken CancellationToken)
        {

            var hostKey  = SshHostKey.GenerateEd25519();
            var userKey  = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys       = [ hostKey ],
                Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob)
            });

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                var port = (UInt16) server.LocalEndPoint.Port.ToInt32();

                // Two keys, both unknown: both are tried, and what comes back is that none was
                // accepted - not a wire error about the second.
                await Assert.ThrowsAsync<SshAuthenticationException>(async () =>
                    await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                        Username      = "achim",
                        VerifyHostKey = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                        Credentials   = [ SshHostKey.GenerateEd25519(), SshHostKey.GenerateEd25519() ]
                    }, CancellationToken));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

    }

}
