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
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Subsystems besides SFTP, served session by session (<see cref="SshServerOptions.Subsystems"/>);
    /// credentials confined to some subsystems (<see cref="SshSessionRestrictions.PermittedSubsystems"/>);
    /// and a client that asks for any subsystem and learns when its connection ended.
    /// </summary>
    [TestFixture]
    public class SubsystemFactoryTests
    {

        #region (private) Fixture

        private const String OCPP   = "ocpp@open.charging.cloud";
        private const String WWCP   = "wwcp@open.charging.cloud";
        private const String Fails  = "fails@test.open.charging.cloud";
        private const String Own    = "own@test.open.charging.cloud";
        private const String Raw    = "raw@test.open.charging.cloud";

        private readonly ISshHostKey  hostKey  = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  userKey  = SshHostKey.GenerateEd25519();

        /// <summary>
        /// Whether a second exit-status the handler of <see cref="Own"/> tried to send went out.
        /// </summary>
        private readonly ConcurrentQueue<Boolean> secondExitStatusSent = new ();

        /// <summary>
        /// Takes the one key, and confines the session as told.
        /// </summary>
        private sealed class Confining(Byte[] Key, SshSessionRestrictions Restrictions) : ISshUserAuthenticator
        {

            public IReadOnlyList<String> OfferedMethods { get; } = [ "publickey" ];

            public ValueTask<Boolean> AuthorizePublicKeyAsync(SshPublicKeyAuthRequest Request, CancellationToken CancellationToken = default)
                => ValueTask.FromResult(Request.PublicKeyBlob.AsSpan().SequenceEqual(Key));

            public ValueTask<SshSessionRestrictions> GetRestrictionsAsync(SshPublicKeyAuthRequest Request, CancellationToken CancellationToken = default)
                => ValueTask.FromResult(Restrictions);

        }

        private sealed record Running(SshServer Server, UInt16 Port, CollectingAuditSink Audit, ConcurrentQueue<(String User, String Name)> Asked, ConcurrentQueue<String> Served) : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => Server.DisposeAsync();
        }

        /// <summary>
        /// A server that serves OCPP and WWCP by echoing one line, SFTP over memory, a shell and exec - and
        /// three subsystems that end otherwise: one whose handler fails, and two that send an exit-status
        /// of their own, with <see cref="SshMuxChannel.SendExitStatusAsync"/> and as a raw channel request.
        /// </summary>
        private async Task<Running> StartAsync(SshSessionRestrictions? Restrictions, CancellationToken CancellationToken)
        {

            var audit   = new CollectingAuditSink();
            var asked   = new ConcurrentQueue<(String, String)>();
            var served  = new ConcurrentQueue<String>();

            var server  = new SshServer(new SshServerOptions {
                              HostKeys        = [ hostKey ],
                              Authenticator   = new Confining(userKey.PublicKeyBlob, Restrictions ?? SshSessionRestrictions.None),
                              AuditSink       = audit,
                              ExecHandler     = async (ctx, ct) => { await ctx.WriteAsync("ran\n", ct); return 0; },
                              ShellHandler    = (ctx, ct) => ValueTask.FromResult(0),
                              SftpFileSystem  = new InMemorySftpFileSystem(),
                              Subsystems      = (session, name, ct) => {

                                                    asked.Enqueue((session.Username, name));

                                                    return ValueTask.FromResult<SshSubsystemHandler?>(name switch {

                                                        OCPP or WWCP  => async (context, token) => {
                                                                             served.Enqueue(context.Name);
                                                                             await context.Channel.SendDataAsync(System.Text.Encoding.UTF8.GetBytes($"{context.Name} for {context.Session.Username}\n"), token);
                                                                             // and return: the server closes the channel
                                                                         },

                                                        Fails         => async (context, token) => {
                                                                             await context.Channel.SendDataAsync("about to fail\n"u8.ToArray(), token);
                                                                             throw new InvalidOperationException("The handler failed.");
                                                                         },

                                                        Own           => async (context, token) => {
                                                                             await context.Channel.SendDataAsync("done\n"u8.ToArray(), token);
                                                                             await context.Channel.SendExitStatusAsync(7, token);
                                                                             secondExitStatusSent.Enqueue(await context.Channel.SendExitStatusAsync(9, token));
                                                                         },

                                                        Raw           => async (context, token) => {
                                                                             await context.Channel.SendDataAsync("done\n"u8.ToArray(), token);
                                                                             await context.Channel.SendRequestAsync("exit-status", false, [ 0, 0, 0, 5 ], token);
                                                                         },

                                                        _             => null

                                                    });

                                                }
                          });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return new Running(server, (UInt16) server.LocalEndPoint.Port.ToInt32(), audit, asked, served);

        }

        private ValueTask<SshClient> ConnectAsync(Running Running, CancellationToken CancellationToken)

            => SshClient.ConnectAsync("127.0.0.1", Running.Port, new SshClientOptions {
                   Username       = "cs001",
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                   Credentials    = [ userKey ]
               }, CancellationToken);

        private static async Task<String> ReadToTheEndAsync(SshMuxChannel Channel)
        {
            using var within = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var reader = new StreamReader(Channel.Input);
            return await reader.ReadToEndAsync(within.Token);
        }

        private static async Task<Boolean> Asks(SshClient Client, String Type, Byte[] Payload, CancellationToken CancellationToken)
        {
            var channel = await Client.Multiplexer.OpenChannelAsync("session", CancellationToken: CancellationToken);
            return await channel.SendRequestAsync(Type, true, Payload, CancellationToken);
        }

        /// <summary>
        /// The data of a subsystem's channel to its end, and every exit-status that came on it before it
        /// was closed.
        /// </summary>
        private static async Task<(String Text, UInt32[] ExitStatuses)> RunToTheEndAsync(SshMuxChannel Channel)
        {

            var text = await ReadToTheEndAsync(Channel);

            using var within  = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var statuses      = new List<UInt32>();

            while (await Channel.ReadRequestAsync(within.Token) is SshChannelRequest request)
                if (request.Type == "exit-status")
                    statuses.Add(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(request.Data));

            return (text, [.. statuses]);

        }

        #endregion


        #region Factory_ServesItsSubsystems_AndTheServerClosesTheChannelAfter

        [Test]
        [CancelAfter(30000)]
        public async Task Factory_ServesItsSubsystems_AndTheServerClosesTheChannelAfter(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(null, CancellationToken);
            await using var client  = await ConnectAsync(running, CancellationToken);

            var ocpp      = await client.OpenSubsystemAsync(OCPP, CancellationToken);
            var ocppText  = await ReadToTheEndAsync(ocpp);

            var wwcp      = await client.OpenSubsystemAsync(WWCP, CancellationToken);
            var wwcpText  = await ReadToTheEndAsync(wwcp);

            Assert.Multiple(() => {
                Assert.That(ocppText,                     Is.EqualTo($"{OCPP} for cs001\n"), "the handler was not given the session, or the channel was not closed after it");
                Assert.That(wwcpText,                     Is.EqualTo($"{WWCP} for cs001\n"));
                Assert.That(running.Asked.Select(a => a.Name), Is.EqualTo(new[] { OCPP, WWCP }));
                Assert.That(running.Asked.Select(a => a.User), Is.All.EqualTo("cs001"));
            });

        }

        #endregion

        #region ExitStatus_IsZero_WhereTheHandlerReturned_AndOne_WhereItFailed

        /// <summary>
        /// As sshd tells how the program behind a subsystem exited: without it, OpenSSH's <c>ssh -s</c>
        /// counts every subsystem as failed (exit code 255), however well it went.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ExitStatus_IsZero_WhereTheHandlerReturned_AndOne_WhereItFailed(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(null, CancellationToken);
            await using var client  = await ConnectAsync(running, CancellationToken);

            var ocpp   = await RunToTheEndAsync(await client.OpenSubsystemAsync(OCPP,  CancellationToken));
            var fails  = await RunToTheEndAsync(await client.OpenSubsystemAsync(Fails, CancellationToken));

            Assert.Multiple(() => {
                Assert.That(ocpp. Text,          Is.EqualTo($"{OCPP} for cs001\n"));
                Assert.That(ocpp. ExitStatuses,  Is.EqualTo(new UInt32[] { 0 }), "a handler that returned did not end with exit-status 0, once");
                Assert.That(fails.Text,          Is.EqualTo("about to fail\n"));
                Assert.That(fails.ExitStatuses,  Is.EqualTo(new UInt32[] { 1 }), "a handler that failed did not end with exit-status 1, once");
            });

        }

        #endregion

        #region ExitStatus_OfTheHandlersOwn_IsTheOnlyOne

        /// <summary>
        /// A handler with a status of its own to tell - e.g. NTS-KE, whose answer may be an Error record -
        /// sends it, and the server adds none: whether with SendExitStatusAsync, which sends one only once,
        /// or as a channel request of its own.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ExitStatus_OfTheHandlersOwn_IsTheOnlyOne(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(null, CancellationToken);
            await using var client  = await ConnectAsync(running, CancellationToken);

            var own  = await RunToTheEndAsync(await client.OpenSubsystemAsync(Own, CancellationToken));
            var raw  = await RunToTheEndAsync(await client.OpenSubsystemAsync(Raw, CancellationToken));

            Assert.Multiple(() => {
                Assert.That(own.Text,              Is.EqualTo("done\n"));
                Assert.That(own.ExitStatuses,      Is.EqualTo(new UInt32[] { 7 }), "the handler's exit-status was not the only one");
                Assert.That(secondExitStatusSent,  Is.EqualTo(new[] { false }),    "a second exit-status went out");
                Assert.That(raw.Text,              Is.EqualTo("done\n"));
                Assert.That(raw.ExitStatuses,      Is.EqualTo(new UInt32[] { 5 }), "the server added an exit-status to the handler's raw one");
            });

        }

        #endregion

        #region Factory_NullRefuses_AndSftpStaysWithTheSftpOptions

        [Test]
        [CancelAfter(30000)]
        public async Task Factory_NullRefuses_AndSftpStaysWithTheSftpOptions(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(null, CancellationToken);
            await using var client  = await ConnectAsync(running, CancellationToken);

            var refused = await Assert.ThrowsAsync<SshWireException>(async () => await client.OpenSubsystemAsync("nothing@open.charging.cloud", CancellationToken));

            await using var sftp = await client.OpenSftpClientAsync(CancellationToken);
            await sftp.UploadAsync("/served.txt", [ 1 ], CancellationToken);

            Assert.Multiple(() => {
                Assert.That(refused!.Message,                                  Does.Contain("nothing@open.charging.cloud"));
                Assert.That(running.Asked.Select(a => a.Name),                 Is.EqualTo(new[] { "nothing@open.charging.cloud" }), "the factory was asked for sftp");
                Assert.That(running.Audit.Events.OfType<PolicyDeniedEvent>().Where(e => e.PolicyType == "subsystem").Select(e => e.Target),
                            Is.EqualTo(new[] { "nothing@open.charging.cloud" }));
            });

        }

        #endregion

        #region PermittedSubsystems_ConfineTheCredential

        /// <summary>
        /// A charging station's credential: OCPP, and nothing else - not WWCP, not SFTP, no shell, no exec,
        /// and no forced command either.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task PermittedSubsystems_ConfineTheCredential(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(new SshSessionRestrictions(PermittedSubsystems: new HashSet<String> { OCPP }), CancellationToken);
            await using var client  = await ConnectAsync(running, CancellationToken);

            var ocpp   = await ReadToTheEndAsync(await client.OpenSubsystemAsync(OCPP, CancellationToken));
            var wwcp   = await Asks(client, "subsystem", SshSessionChannel.EncodeString(WWCP),   CancellationToken);
            var sftp   = await Asks(client, "subsystem", SshSessionChannel.EncodeString("sftp"), CancellationToken);
            var shell  = await Asks(client, "shell",     [],                                     CancellationToken);
            var exec   = await Asks(client, "exec",      SshSessionChannel.EncodeString("id"),   CancellationToken);

            Assert.Multiple(() => {

                Assert.That(ocpp,   Is.EqualTo($"{OCPP} for cs001\n"));
                Assert.That(wwcp,   Is.False, "WWCP was served");
                Assert.That(sftp,   Is.False, "SFTP was served");
                Assert.That(shell,  Is.False, "a shell was served");
                Assert.That(exec,   Is.False, "a command ran");

                Assert.That(running.Asked.Select(a => a.Name), Is.EqualTo(new[] { OCPP }), "the factory was asked for what the credential may not have");

                Assert.That(running.Audit.Events.OfType<PolicyDeniedEvent>().Select(e => (e.PolicyType, e.Target)),
                            Is.SupersetOf(new[] { ("subsystem", WWCP), ("subsystem", "sftp"), ("shell", ""), ("exec", "id") }));

            });

        }

        #endregion

        #region PermittedSubsystems_WithAForcedCommand_RunNoCommand

        [Test]
        [CancelAfter(30000)]
        public async Task PermittedSubsystems_WithAForcedCommand_RunNoCommand(CancellationToken CancellationToken)
        {

            await using var running = await StartAsync(new SshSessionRestrictions(ForcedCommand: "/usr/bin/backup-only", PermittedSubsystems: new HashSet<String> { OCPP }), CancellationToken);
            await using var client  = await ConnectAsync(running, CancellationToken);

            var ocpp = await Asks(client, "subsystem", SshSessionChannel.EncodeString(OCPP), CancellationToken);

            Assert.Multiple(() => {
                Assert.That(ocpp,                                                  Is.False, "the subsystem, or the forced command, was served");
                Assert.That(running.Audit.Events.OfType<ExecRequestedEvent>(),     Is.Empty, "the forced command ran");
            });

        }

        #endregion

        #region Restrictions_And_IntersectsPermittedSubsystems

        [Test]
        public void Restrictions_And_IntersectsPermittedSubsystems()
        {

            var both  = new SshSessionRestrictions(PermittedSubsystems: new HashSet<String> { OCPP, WWCP }).
                            And(new SshSessionRestrictions(PermittedSubsystems: new HashSet<String> { OCPP, "sftp" }));

            var one   = SshSessionRestrictions.None.And(new SshSessionRestrictions(PermittedSubsystems: new HashSet<String> { WWCP }));

            Assert.Multiple(() => {
                Assert.That(both.PermittedSubsystems,                 Is.EquivalentTo(new[] { OCPP }));
                Assert.That(one. PermittedSubsystems,                 Is.EquivalentTo(new[] { WWCP }));
                Assert.That(SshSessionRestrictions.None.And(SshSessionRestrictions.None).PermittedSubsystems, Is.Null);
                Assert.That(one.IsRestricted,                         Is.True);
            });

        }

        #endregion

        #region Client_Closed_CompletesWhenTheServerGoes

        [Test]
        [CancelAfter(30000)]
        public async Task Client_Closed_CompletesWhenTheServerGoes(CancellationToken CancellationToken)
        {

            var running = await StartAsync(null, CancellationToken);
            await using var client = await ConnectAsync(running, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(client.Closed.IsCompleted,    Is.False);
                Assert.That(client.EndReason,             Is.Null);
                Assert.That(client.RemoteSocket?.Port.ToInt32(), Is.EqualTo(running.Port));
                Assert.That(client.ServerHostKey,         Is.EqualTo(hostKey.PublicKeyBlob));
            });

            await running.DisposeAsync();

            var closed = await Task.WhenAny(client.Closed, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Multiple(() => {
                Assert.That(closed,            Is.SameAs(client.Closed), "the client did not notice the server go");
                Assert.That(client.EndReason,  Is.Not.Null);
            });

        }

        #endregion

    }

}
