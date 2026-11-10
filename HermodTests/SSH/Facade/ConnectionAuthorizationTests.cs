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

using System.Collections.Concurrent;
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

    /// <summary>
    /// <see cref="SshServerOptions.AuthorizeConnection"/>: asked as soon as a connection is accepted,
    /// before the server has sent a byte. A client it turns away sees a connection that closed, not an
    /// SSH server: no version string and no host key, which <see cref="SshServerOptions.AuthorizeSession"/>
    /// cannot avoid, because it is asked only once the client has signed in.
    /// </summary>
    [TestFixture]
    public class ConnectionAuthorizationTests
    {

        #region (private) Fixture

        private readonly ISshHostKey  hostKey  = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  userKey  = SshHostKey.GenerateEd25519();

        private sealed record Running(SshServer Server, UInt16 Port, CollectingAuditSink Audit, ConcurrentQueue<(IPSocket? Peer, IPSocket? Local)> Asked) : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => Server.DisposeAsync();
        }

        /// <summary>
        /// A server on every IPv4 address that runs any command it is given.
        /// </summary>
        private async Task<Running> StartAsync(Func<IPSocket?, String?>?  Decide,
                                               SshServerLimits?           Limits,
                                               CancellationToken          CancellationToken)
        {

            var audit   = new CollectingAuditSink();
            var asked   = new ConcurrentQueue<(IPSocket? Peer, IPSocket? Local)>();

            var server  = new SshServer(new SshServerOptions {
                              HostKeys             = [ hostKey ],
                              Authenticator        = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                              AuditSink            = audit,
                              ExecHandler          = async (ctx, ct) => { await ctx.WriteAsync("ran\n", ct); return 0; },
                              Limits               = Limits ?? new (),
                              AuthorizeConnection  = Decide is null
                                                         ? null
                                                         : (peer, local) => { asked.Enqueue((peer, local)); return Decide(peer); }
                          });

            await server.StartAsync(new IPSocket(IPv4Address.Any, IPPort.Auto), CancellationToken);

            return new Running(server, (UInt16) server.LocalEndPoint.Port.ToInt32(), audit, asked);

        }

        private ValueTask<SshClient> ConnectAsync(Running Running, CancellationToken CancellationToken)

            => SshClient.ConnectAsync("127.0.0.1", Running.Port, new SshClientOptions {
                   Username       = "achim",
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                   Credentials    = [ userKey ]
               }, CancellationToken);

        /// <summary>
        /// What the server sends a client that connects and says nothing: its version string, from a
        /// server that goes on with the handshake - the server speaks first - or nothing, from one that
        /// closes the connection, gracefully or with a reset.
        /// </summary>
        private static async Task<String> FirstWordsAsync(Running Running, CancellationToken CancellationToken)
        {

            using var tcp     = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, Running.Port, timeout.Token);

            var buffer = new Byte[64];

            try
            {
                var read = await tcp.GetStream().ReadAsync(buffer, timeout.Token);
                return Encoding.ASCII.GetString(buffer, 0, read);
            }
            catch (IOException)
            {
                return "";
            }

        }

        #endregion


        #region Refused_NothingIsSent_AndTheRefusalIsAudited

        /// <summary>
        /// A refused connection is closed before the server says anything. The refusal is audited with its
        /// reason and the address it was given for, and the authorizer was told the connection's own local
        /// address, not the listener's 0.0.0.0.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Refused_NothingIsSent_AndTheRefusalIsAudited(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(peer => "not on the list", null, CancellationToken);

            var heard = await FirstWordsAsync(running, CancellationToken);

            Assert.That(running.Asked.TryPeek(out var asked), Is.True, "the authorizer was not asked");

            Assert.Multiple(() => {
                Assert.That(heard,                               Is.Empty,                "a refused client heard from the server");
                Assert.That(asked.Peer?.IPAddress.ToString(),    Is.EqualTo("127.0.0.1"));
                Assert.That(asked.Local?.IPAddress.ToString(),   Is.EqualTo("127.0.0.1"), "the listener's 0.0.0.0, not the connection's address");
                Assert.That(asked.Local?.Port.ToInt32(),         Is.EqualTo(running.Port));
                Assert.That(running.Audit.Events.OfType<PolicyDeniedEvent>().Select(e => (e.PolicyType, e.Target, e.Reason)),
                            Is.EqualTo(new[] { ("connection", "127.0.0.1", "not on the list") }));
                Assert.That(running.Audit.Events.OfType<ConnectionOpenedEvent>(), Is.Empty, "a refused connection was opened");
            });

        }

        #endregion

        #region Allowed_TheHandshakeGoesOn

        /// <summary>
        /// The control: a connection the authorizer lets through hears the server's version string, and a
        /// client signs in and runs a command.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Allowed_TheHandshakeGoesOn(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(peer => null, null, CancellationToken);

            var heard = await FirstWordsAsync(running, CancellationToken);

            await using var client = await ConnectAsync(running, CancellationToken);

            var result = await client.ExecuteAsync("uptime", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(heard,                  Does.StartWith("SSH-2.0-"));
                Assert.That(result.StandardOutput,  Is.EqualTo("ran\n"));
                Assert.That(running.Asked,          Has.Count.EqualTo(2), "asked once for each connection");
                Assert.That(running.Audit.Events.OfType<PolicyDeniedEvent>(), Is.Empty);
            });

        }

        #endregion

        #region Authorizer_ThatFails_Refuses

        [Test]
        [CancelAfter(30000)]
        public async Task Authorizer_ThatFails_Refuses(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(peer => throw new InvalidOperationException("the list could not be read"), null, CancellationToken);

            var heard = await FirstWordsAsync(running, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(heard, Is.Empty, "a connection the authorizer could not decide on heard from the server");
                Assert.That(running.Audit.Events.OfType<PolicyDeniedEvent>().Single().Reason, Does.Contain("the list could not be read"));
            });

        }

        #endregion

        #region RefusedConnections_DoNotCountAgainstTheLimits

        /// <summary>
        /// A refused connection takes no place among the connections that have not authenticated yet:
        /// with one place per address, three refused connections leave it free for the fourth, which the
        /// authorizer lets through.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task RefusedConnections_DoNotCountAgainstTheLimits(CancellationToken CancellationToken)
        {

            var connections = 0;

            await using var running = await StartAsync(peer => Interlocked.Increment(ref connections) <= 3 ? "not yet" : null,
                                                       new SshServerLimits { MaxUnauthenticatedPerAddress = 1 },
                                                       CancellationToken);

            for (var i = 0; i < 3; i++)
                Assert.That(await FirstWordsAsync(running, CancellationToken), Is.Empty, $"refused connection {i + 1} heard from the server");

            await using var client = await ConnectAsync(running, CancellationToken);

            var result = await client.ExecuteAsync("uptime", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput, Is.EqualTo("ran\n"));
                Assert.That(running.Audit.Events.OfType<LimitExceededEvent>(), Is.Empty, "a refused connection was counted");
            });

        }

        #endregion

        #region NoAuthorizer_AcceptsEveryConnection

        [Test]
        [CancelAfter(30000)]
        public async Task NoAuthorizer_AcceptsEveryConnection(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(null, null, CancellationToken);

            Assert.That(await FirstWordsAsync(running, CancellationToken), Does.StartWith("SSH-2.0-"));

        }

        #endregion

    }

}
