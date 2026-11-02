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
    /// <see cref="SshClientOptions.ProxyJump"/>: the façade reaches a server through jump hosts of our own,
    /// each a login of its own with its own host key and credentials.
    /// </summary>
    [TestFixture]
    public class FacadeProxyJumpTests
    {

        #region Facade_ProxyJump_ThroughTwoJumpHosts_ReachesTheServer

        /// <summary>
        /// <c>ssh -J j1,j2 server</c>: every hop checks its own host key and logs in with its own key, each
        /// jump host opens exactly one tunnel, and disposing the client closes both jump hosts too.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Facade_ProxyJump_ThroughTwoJumpHosts_ReachesTheServer(CancellationToken CancellationToken)
        {

            await using var first  = await JumpHostAsync(CancellationToken);
            await using var second = await JumpHostAsync(CancellationToken);
            await using var server = await ServerAsync(CancellationToken);

            var jumpHosts = new Dictionary<UInt16, Peer> { [first.Port] = first, [second.Port] = second };

            await using (var client = await SshClient.ConnectAsync("127.0.0.1", server.Port, new SshClientOptions {
                                                                       Username         = "achim",
                                                                       VerifyHostKey    = server.Verify,
                                                                       Credentials      = [ server.UserKey ],
                                                                       ProxyJump        = [ new SshJumpHost("127.0.0.1", first.Port,  "jumper"),
                                                                                            new SshJumpHost("127.0.0.1", second.Port, "jumper") ],
                                                                       JumpHostOptions  = jumpHost => new SshClientOptions {
                                                                                              Username       = jumpHost.Username!,
                                                                                              VerifyHostKey  = jumpHosts[jumpHost.Port].Verify,
                                                                                              Credentials    = [ jumpHosts[jumpHost.Port].UserKey ]
                                                                                          }
                                                                   }, CancellationToken))
            {

                var result = await client.ExecuteAsync("whoami", CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(result.StandardOutput,     Is.EqualTo("server ran: whoami\n"), "exec on the server, through both jump hosts");
                    Assert.That(Tunnels(first),            Is.EqualTo(1),                      "the first jump host tunnelled to the second");
                    Assert.That(Tunnels(second),           Is.EqualTo(1),                      "the second jump host tunnelled to the server");
                    Assert.That(first.Usernames,           Is.EqualTo(new[] { "jumper" }));
                    Assert.That(server.Usernames,          Is.EqualTo(new[] { "achim" }));
                });

            }

            await ClosedAsync(first,  CancellationToken);
            await ClosedAsync(second, CancellationToken);

        }

        #endregion

        #region Facade_ProxyJump_AWrongKeyAtTheSecondJumpHost_NamesIt

        /// <summary>
        /// The second jump host shows a key nobody expected: the client says which hop it was, the server
        /// is never reached, and the first jump host is closed again.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Facade_ProxyJump_AWrongKeyAtTheSecondJumpHost_NamesIt(CancellationToken CancellationToken)
        {

            await using var first  = await JumpHostAsync(CancellationToken);
            await using var second = await JumpHostAsync(CancellationToken);
            await using var server = await ServerAsync(CancellationToken);

            var someoneElse = SshHostKey.GenerateEd25519().PublicKeyBlob;

            var failure = await Assert.ThrowsAsync<SshJumpHostException>(async () =>
                              await SshClient.ConnectAsync("127.0.0.1", server.Port, new SshClientOptions {
                                  Username         = "achim",
                                  VerifyHostKey    = server.Verify,
                                  Credentials      = [ server.UserKey ],
                                  ProxyJump        = [ new SshJumpHost("127.0.0.1", first.Port), new SshJumpHost("127.0.0.1", second.Port) ],
                                  JumpHostOptions  = jumpHost => new SshClientOptions {
                                                         Username       = "jumper",
                                                         VerifyHostKey  = jumpHost.Port == first.Port ? first.Verify : blob => blob.AsSpan().SequenceEqual(someoneElse),
                                                         Credentials    = [ jumpHost.Port == first.Port ? first.UserKey : second.UserKey ]
                                                     }
                              }, CancellationToken));

            Assert.Multiple(() => {
                Assert.That(failure!.Hop,            Is.EqualTo(2));
                Assert.That(failure.Hops,            Is.EqualTo(2));
                Assert.That(failure.JumpHost.Port,   Is.EqualTo(second.Port));
                Assert.That(failure.InnerException,  Is.Not.Null);
                Assert.That(failure.Message,         Does.StartWith($"Jump host 2 of 2 (127.0.0.1:{second.Port}): "));
                Assert.That(server.Audit.Events.OfType<ConnectionOpenedEvent>(), Is.Empty, "the server was never reached");
            });

            await ClosedAsync(first, CancellationToken);

        }

        #endregion

        #region Facade_ProxyJump_WithoutJumpHostOptions_BorrowsTheKeysAndTheUser

        /// <summary>
        /// Without <see cref="SshClientOptions.JumpHostOptions"/> the jump host borrows the server's options:
        /// its key, and the user its <c>user@</c> names.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Facade_ProxyJump_WithoutJumpHostOptions_BorrowsTheKeysAndTheUser(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();

            await using var jumpHost = await JumpHostAsync(CancellationToken, userKey);
            await using var server   = await ServerAsync(CancellationToken, userKey);

            await using var client = await SshClient.ConnectAsync("127.0.0.1", server.Port, new SshClientOptions {
                                                                      Username       = "achim",
                                                                      VerifyHostKey  = blob => jumpHost.Verify(blob) || server.Verify(blob),
                                                                      Credentials    = [ userKey ],
                                                                      ProxyJump      = SshJumpHost.ParseChain($"jumper@127.0.0.1:{jumpHost.Port}")
                                                                  }, CancellationToken);

            var result = await client.ExecuteAsync("id", CancellationToken);

            Assert.Multiple(() => {
                Assert.That(result.StandardOutput,  Is.EqualTo("server ran: id\n"));
                Assert.That(jumpHost.Usernames,     Is.EqualTo(new[] { "jumper" }), "the user the jump host names");
                Assert.That(server.Usernames,       Is.EqualTo(new[] { "achim" }));
            });

        }

        #endregion

        #region Facade_ProxyJump_WithoutJumpHostOptions_TheServersPasswordIsNotLent

        /// <summary>
        /// A borrowing jump host does not get the server's password: it would read it in the clear. One
        /// that takes only a password then turns the client away, without ever having seen it.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Facade_ProxyJump_WithoutJumpHostOptions_TheServersPasswordIsNotLent(CancellationToken CancellationToken)
        {

            await using var jumpHost = await JumpHostAsync(CancellationToken, AcceptedKey: null);
            await using var server   = await ServerAsync(CancellationToken);

            var failure = await Assert.ThrowsAsync<SshJumpHostException>(async () =>
                              await SshClient.ConnectAsync("127.0.0.1", server.Port, new SshClientOptions {
                                  Username             = "achim",
                                  VerifyHostKey        = blob => jumpHost.Verify(blob) || server.Verify(blob),
                                  Credentials          = [ server.UserKey ],
                                  Password             = "the server's secret",
                                  ProxyJump            = [ new SshJumpHost("127.0.0.1", jumpHost.Port) ]
                              }, CancellationToken));

            Assert.Multiple(() => {
                Assert.That(failure!.InnerException,  Is.InstanceOf<SshAuthenticationException>());
                Assert.That(jumpHost.Passwords,       Is.Empty, "the jump host never saw the server's password");
            });

        }

        #endregion


        #region (private) peers

        /// <summary>
        /// One of our servers: its port, its host key, the key it lets in, and what it saw.
        /// </summary>
        private sealed class Peer(SshServer Server, UInt16 Port, ISshHostKey HostKey, ISshHostKey UserKey, CollectingAuditSink Audit, Recorder Recorder) : IAsyncDisposable
        {

            public UInt16                 Port       { get; } = Port;
            public ISshHostKey            UserKey    { get; } = UserKey;
            public CollectingAuditSink    Audit      { get; } = Audit;
            public IReadOnlyList<String>  Usernames  => Recorder.Usernames.Distinct().ToArray();
            public IReadOnlyList<String>  Passwords  => Recorder.Passwords.ToArray();

            public Boolean Verify(Byte[] Blob)
                => Blob.AsSpan().SequenceEqual(HostKey.PublicKeyBlob);

            public ValueTask DisposeAsync()
                => Server.DisposeAsync();

        }

        /// <summary>
        /// Lets in one key, if any, and keeps every user and password it was asked about.
        /// </summary>
        private sealed class Recorder(Byte[]? AcceptedKey) : ISshUserAuthenticator
        {

            public ConcurrentQueue<String> Usernames { get; } = new ();
            public ConcurrentQueue<String> Passwords { get; } = new ();

            public IReadOnlyList<String> OfferedMethods { get; } = [ "publickey", "password" ];

            public ValueTask<Boolean> AuthorizePublicKeyAsync(SshPublicKeyAuthRequest Request, CancellationToken CancellationToken = default)
            {
                Usernames.Enqueue(Request.Username);
                return ValueTask.FromResult(AcceptedKey is not null && Request.PublicKeyBlob.AsSpan().SequenceEqual(AcceptedKey));
            }

            public ValueTask<Boolean> AuthorizePasswordAsync(String Username, String Password, CancellationToken CancellationToken = default)
            {
                Passwords.Enqueue(Password);
                return ValueTask.FromResult(false);
            }

        }

        private static Task<Peer> JumpHostAsync(CancellationToken CancellationToken)
            => JumpHostAsync(CancellationToken, SshHostKey.GenerateEd25519());

        private static Task<Peer> JumpHostAsync(CancellationToken CancellationToken, ISshHostKey? AcceptedKey)
            => StartAsync(AcceptedKey, Forwarding: true, CancellationToken);

        private static Task<Peer> ServerAsync(CancellationToken CancellationToken, ISshHostKey? UserKey = null)
            => StartAsync(UserKey ?? SshHostKey.GenerateEd25519(), Forwarding: false, CancellationToken);

        private static async Task<Peer> StartAsync(ISshHostKey? UserKey, Boolean Forwarding, CancellationToken CancellationToken)
        {

            var hostKey  = SshHostKey.GenerateEd25519();
            var audit    = new CollectingAuditSink();
            var recorder = new Recorder(UserKey?.PublicKeyBlob);

            var server   = new SshServer(new SshServerOptions {
                               HostKeys          = [ hostKey ],
                               Authenticator     = recorder,
                               AuditSink         = audit,
                               ExecHandler       = Forwarding ? null : async (ctx, ct) => { await ctx.WriteAsync($"server ran: {ctx.Command}\n", ct); return 0; },
                               ForwardingPolicy  = Forwarding ? ForwardingPolicy.LoopbackOnly : ForwardingPolicy.None
                           });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return new Peer(server, (UInt16) server.LocalEndPoint.Port.ToInt32(), hostKey, UserKey ?? SshHostKey.GenerateEd25519(), audit, recorder);

        }

        private static Int32 Tunnels(Peer JumpHost)
            => JumpHost.Audit.Events.OfType<ChannelOpenedEvent>().Count(e => e.ChannelType == "direct-tcpip");

        // A jump host notices the end of a connection a moment after the client has let go of it.
        private static async Task ClosedAsync(Peer JumpHost, CancellationToken CancellationToken)
        {
            while (!JumpHost.Audit.Events.OfType<ConnectionClosedEvent>().Any())
                await Task.Delay(25, CancellationToken);
        }

        #endregion

    }

}
