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

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>
    /// Regression tests for the <c>direct-tcpip</c> forwarding ACL in the <see cref="SshServer"/> façade.
    ///
    /// <para>
    /// The M9 security review found that the façade resolved the client-supplied hostname twice: once in
    /// the channel acceptor (where the ACL was checked) and again at dial time (where it was not). That
    /// breaks the contract <c>NetworkAcl.AllowsAll</c> documents — "the caller must then dial exactly
    /// these addresses without re-resolving" — and lets an attacker-controlled name answer with an
    /// allowed address for the check and a forbidden one for the connection.
    /// </para>
    ///
    /// <para>
    /// The binding decision now happens at the dial site, against the addresses actually dialed. These
    /// tests pin that: a forbidden target must never be connected to, whichever resolution said what.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Security")]
    public class ForwardingRebindingTests
    {

        #region (private) a listener that records whether it was ever reached

        private sealed class Canary : IDisposable
        {

            private readonly TcpListener listener;

            public Int32 Port       { get; }
            public Boolean Reached  { get; private set; }

            public Canary(CancellationToken CancellationToken)
            {

                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                Port = ((IPEndPoint) listener.LocalEndpoint).Port;

                _ = Task.Run(async () => {
                    try
                    {
                        while (true)
                        {
                            var connection = await listener.AcceptTcpClientAsync(CancellationToken);
                            Reached = true;
                            using var stream = connection.GetStream();
                            await stream.WriteAsync(Encoding.UTF8.GetBytes("REACHED"), CancellationToken);
                        }
                    }
                    catch { }
                }, CancellationToken);

            }

            public void Dispose()
            {
                try { listener.Stop(); } catch { }
            }

        }

        #endregion

        #region (private) start a server with the given forwarding policy

        private static async Task<(SshServer Server, UInt16 Port, ISshHostKey HostKey, ISshHostKey UserKey)>
            StartServerAsync(ForwardingPolicy Policy, CancellationToken CancellationToken, SshAddressResolver? Resolver = null)
        {

            var hostKey = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var server = new SshServer(new SshServerOptions {
                HostKeys         = [ hostKey ],
                Authenticator    = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                ExecHandler      = async (ctx, ct) => { await ctx.WriteAsync("ok\n", ct); return 0; },
                ForwardingPolicy = Policy,
                AddressResolver  = Resolver
            });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return (server, (UInt16) server.LocalEndPoint.Port.ToInt32(), hostKey, userKey);

        }

        private static ValueTask<SshClient> ConnectAsync(UInt16 Port, ISshHostKey HostKey, ISshHostKey UserKey, CancellationToken CancellationToken)
            => SshClient.ConnectAsync("127.0.0.1", Port, new SshClientOptions {
                   Username      = "achim",
                   VerifyHostKey = blob => blob.AsSpan().SequenceEqual(HostKey.PublicKeyBlob),
                   Credentials   = [ UserKey ]
               }, CancellationToken);

        #endregion


        #region ForbiddenTarget_IsNeverConnectedTo

        /// <summary>
        /// The decisive property: a target outside the ACL must never receive a connection. The canary
        /// listens on a port the policy does not permit; if the relay ever dials it, <c>Reached</c> flips.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task ForbiddenTarget_IsNeverConnectedTo(CancellationToken CancellationToken)
        {

            using var canary = new Canary(CancellationToken);

            // Permit loopback, but only on a port the canary is NOT listening on.
            var allowedPort = canary.Port == 65535 ? 65534 : canary.Port + 1;
            var policy      = ForwardingPolicy.Custom(NetworkAcl.DenyByDefault()
                                                                .Allow(Cidr: "127.0.0.1/32", Ports: allowedPort.ToString()));

            var (server, port, hostKey, userKey) = await StartServerAsync(policy, CancellationToken);

            try
            {

                await using var client = await ConnectAsync(port, hostKey, userKey, CancellationToken);

                // Opening a tunnel to the forbidden port must fail, and must not reach the canary.
                try
                {
                    await using var tunnel = await client.OpenTcpStreamAsync("127.0.0.1", (UInt16) canary.Port, CancellationToken);
                    var buffer = new Byte[16];
                    await tunnel.ReadAsync(buffer, CancellationToken);
                }
                catch { /* refusing the channel — or tearing it down — are both acceptable */ }

                await Task.Delay(300, CancellationToken);

                Assert.That(canary.Reached, Is.False,
                            "the server must not connect to a target the forwarding ACL forbids");

                // The connection must survive the refusal.
                var result = await client.ExecuteAsync("still alive", CancellationToken);
                Assert.That(result.StandardOutput, Is.EqualTo("ok\n"));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region RebindingResolver_CannotReachTheForbiddenAddress

        /// <summary>
        /// The actual rebinding attack. The resolver plays an attacker-controlled nameserver: the first
        /// answer is an address the ACL permits, every later answer is the forbidden canary. If the ACL
        /// is only evaluated against the first answer and the connection is made from a later one, the
        /// canary is reached — which is exactly the bypass this must prevent.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task RebindingResolver_CannotReachTheForbiddenAddress(CancellationToken CancellationToken)
        {

            using var canary = new Canary(CancellationToken);

            // 127.0.0.2 is permitted; 127.0.0.1 — where the canary actually listens — is not.
            var policy = ForwardingPolicy.Custom(NetworkAcl.DenyByDefault().Allow(Cidr: "127.0.0.2/32"));

            // The attacker's nameserver: the first answer is the permitted address (so the acceptor's
            // check passes), every later answer is the forbidden one the canary is on. The ACL verdict
            // therefore differs between the two resolutions — which is the whole attack.
            var calls    = 0;
            var resolver = new SshAddressResolver((host, ct) => {
                               var call = Interlocked.Increment(ref calls);
                               return ValueTask.FromResult<IReadOnlyList<IPAddress>>(
                                          [ call == 1 ? IPAddress.Parse("127.0.0.2") : IPAddress.Loopback ]);
                           });

            var (server, port, hostKey, userKey) = await StartServerAsync(policy, CancellationToken, resolver);

            try
            {

                await using var client = await ConnectAsync(port, hostKey, userKey, CancellationToken);

                try
                {
                    await using var tunnel = await client.OpenTcpStreamAsync("rebind.example", (UInt16) canary.Port, CancellationToken);
                    var buffer = new Byte[16];
                    await tunnel.ReadAsync(buffer, CancellationToken);
                }
                catch { /* refusal is the expected outcome */ }

                await Task.Delay(300, CancellationToken);

                Assert.Multiple(() => {

                    Assert.That(canary.Reached, Is.False,
                                "the forbidden address must never be dialed, however many times the name resolves");

                    Assert.That(calls, Is.GreaterThan(0), "the resolver must actually have been consulted");

                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region AllowedTarget_StillWorks

        /// <summary>
        /// The tightening must not break legitimate forwarding.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task AllowedTarget_StillWorks(CancellationToken CancellationToken)
        {

            using var canary = new Canary(CancellationToken);

            var policy = ForwardingPolicy.Custom(NetworkAcl.DenyByDefault()
                                                           .Allow(Cidr: "127.0.0.1/32", Ports: canary.Port.ToString()));

            var (server, port, hostKey, userKey) = await StartServerAsync(policy, CancellationToken);

            try
            {

                await using var client = await ConnectAsync(port, hostKey, userKey, CancellationToken);
                await using var tunnel = await client.OpenTcpStreamAsync("127.0.0.1", (UInt16) canary.Port, CancellationToken);

                var buffer = new Byte[7];
                await tunnel.ReadExactlyAsync(buffer, CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(Encoding.UTF8.GetString(buffer), Is.EqualTo("REACHED"));
                    Assert.That(canary.Reached,                  Is.True);
                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

    }

}
