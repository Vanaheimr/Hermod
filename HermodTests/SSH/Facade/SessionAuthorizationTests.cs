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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// <see cref="SshServerOptions.AuthorizeSession"/>: asked once a client has signed in, with the whole
    /// session - including the address of this machine the client connected to, which a server listening
    /// on every address learns only from the connection.
    /// </summary>
    [TestFixture]
    public class SessionAuthorizationTests
    {

        #region (private) Fixture

        private readonly ISshHostKey  hostKey  = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  userKey  = SshHostKey.GenerateEd25519();

        private sealed record Running(SshServer Server, UInt16 Port, CollectingAuditSink Audit, ConcurrentQueue<SshSessionInfo> Asked, ConcurrentQueue<String> Ran) : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => Server.DisposeAsync();
        }

        /// <summary>
        /// A server on every IPv4 address, whose exec handler notes what ran.
        /// </summary>
        private async Task<Running> StartAsync(Func<SshSessionInfo, String?>? Decide, CancellationToken CancellationToken)
        {

            var audit   = new CollectingAuditSink();
            var asked   = new ConcurrentQueue<SshSessionInfo>();
            var ran     = new ConcurrentQueue<String>();

            var server  = new SshServer(new SshServerOptions {
                              HostKeys          = [ hostKey ],
                              Authenticator     = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                              AuditSink         = audit,
                              ExecHandler       = async (ctx, ct) => { ran.Enqueue(ctx.Command); await ctx.WriteAsync("ran\n", ct); return 0; },
                              AuthorizeSession  = Decide is null
                                                      ? null
                                                      : (session, ct) => { asked.Enqueue(session); return ValueTask.FromResult(Decide(session)); }
                          });

            await server.StartAsync(new IPSocket(IPv4Address.Any, IPPort.Auto), CancellationToken);

            return new Running(server, (UInt16) server.LocalEndPoint.Port.ToInt32(), audit, asked, ran);

        }

        private ValueTask<SshClient> ConnectAsync(Running Running, CancellationToken CancellationToken)

            => SshClient.ConnectAsync("127.0.0.1", Running.Port, new SshClientOptions {
                   Username       = "achim",
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                   Credentials    = [ userKey ]
               }, CancellationToken);

        #endregion


        #region Authorizer_IsAskedWithTheConnectionsOwnLocalAddress

        /// <summary>
        /// The server listens on 0.0.0.0; the session says the client came in on 127.0.0.1 - which of the
        /// machine's networks it is on, and what a rule for "the device network's address" needs.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Authorizer_IsAskedWithTheConnectionsOwnLocalAddress(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(session => null, CancellationToken);
            await using var client  = await ConnectAsync(running, CancellationToken);

            var result  = await client.ExecuteAsync("uptime", CancellationToken);

            Assert.That(running.Asked.TryPeek(out var session), Is.True, "the authorizer was not asked");

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,              Is.EqualTo("ran\n"));
                Assert.That(session!.Username,                  Is.EqualTo("achim"));
                Assert.That(session!.PublicKeyBlob,             Is.EqualTo(userKey.PublicKeyBlob));
                Assert.That(session!.Local?.IPAddress.ToString(), Is.EqualTo("127.0.0.1"), "the listener's 0.0.0.0, not the connection's address");
                Assert.That(session!.Local?.Port.ToInt32(),     Is.EqualTo(running.Port));
                Assert.That(session!.Peer?.IPAddress.ToString(), Is.EqualTo("127.0.0.1"));
            });

        }

        #endregion

        #region Authorizer_Refuses_TheClientIsDisconnected_AndNothingRuns

        [Test]
        [CancelAfter(30000)]
        public async Task Authorizer_Refuses_TheClientIsDisconnected_AndNothingRuns(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(session => "rule 2: not from the device network", CancellationToken);

            Exception? failed = null;

            try
            {
                await using var client = await ConnectAsync(running, CancellationToken);
                await client.ExecuteAsync("uptime", CancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !CancellationToken.IsCancellationRequested)
            {
                failed = exception;
            }

            Assert.Multiple(() => {
                Assert.That(failed,       Is.Not.Null, "a refused session ran a command");
                Assert.That(running.Ran,  Is.Empty,    "a refused session ran a command");
                Assert.That(running.Audit.Events.OfType<PolicyDeniedEvent>().Select(e => (e.PolicyType, e.Target, e.Reason)),
                            Is.EqualTo(new[] { ("session", "achim", "rule 2: not from the device network") }));
            });

        }

        #endregion

        #region Authorizer_ThatFails_Refuses

        [Test]
        [CancelAfter(30000)]
        public async Task Authorizer_ThatFails_Refuses(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(session => throw new InvalidOperationException("the rules could not be read"), CancellationToken);

            try
            {
                await using var client = await ConnectAsync(running, CancellationToken);
                await client.ExecuteAsync("uptime", CancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !CancellationToken.IsCancellationRequested)
            { }

            Assert.Multiple(() => {
                Assert.That(running.Ran, Is.Empty, "a session the authorizer could not decide on ran a command");
                Assert.That(running.Audit.Events.OfType<PolicyDeniedEvent>().Single().Reason, Does.Contain("the rules could not be read"));
            });

        }

        #endregion

        #region NoAuthorizer_LetsEverySignedInClientIn

        [Test]
        [CancelAfter(30000)]
        public async Task NoAuthorizer_LetsEverySignedInClientIn(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(null, CancellationToken);
            await using var client  = await ConnectAsync(running, CancellationToken);

            Assert.That((await client.ExecuteAsync("uptime", CancellationToken)).StandardOutput, Is.EqualTo("ran\n"));

        }

        #endregion

    }

}
