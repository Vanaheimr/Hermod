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

    /// <summary>
    /// SFTP served session by session through <see cref="SshServerOptions.SftpSessions"/>: each session sees
    /// the file system its factory made for it, a factory that says no refuses the subsystem, and a session
    /// whose end fires is closed - with its file system disposed of however it ended.
    /// </summary>
    [TestFixture]
    public class SftpSessionsTests
    {

        #region (class) SessionFileSystem

        /// <summary>
        /// A file system made for one session: in memory, disposable, and able to hold a STAT until the
        /// session is cancelled - a request in flight when the session ends.
        /// </summary>
        private sealed class SessionFileSystem(String Owner) : ISftpFileSystem, IAsyncDisposable
        {

            private readonly InMemorySftpFileSystem inner = new ();

            public String                Owner        { get; } = Owner;
            public Boolean               HoldStat     { get; set; }
            public TaskCompletionSource  StatHeld     { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource  Disposed     { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);

            public ValueTask<String> OpenAsync(String Path, SftpOpenFlags Flags, CancellationToken CancellationToken = default)
                => inner.OpenAsync(Path, Flags, CancellationToken);

            public ValueTask<Byte[]> ReadAsync(String Handle, Int64 Offset, Int32 Length, CancellationToken CancellationToken = default)
                => inner.ReadAsync(Handle, Offset, Length, CancellationToken);

            public ValueTask WriteAsync(String Handle, Int64 Offset, ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
                => inner.WriteAsync(Handle, Offset, Data, CancellationToken);

            public ValueTask CloseAsync(String Handle, CancellationToken CancellationToken = default)
                => inner.CloseAsync(Handle, CancellationToken);

            public ValueTask<String> OpenDirectoryAsync(String Path, CancellationToken CancellationToken = default)
                => inner.OpenDirectoryAsync(Path, CancellationToken);

            public ValueTask<IReadOnlyList<SftpDirectoryEntry>> ReadDirectoryAsync(String Handle, CancellationToken CancellationToken = default)
                => inner.ReadDirectoryAsync(Handle, CancellationToken);

            public async ValueTask<SftpFileAttributes> StatAsync(String Path, CancellationToken CancellationToken = default)
            {

                if (HoldStat)
                {
                    StatHeld.TrySetResult();
                    await Task.Delay(Timeout.Infinite, CancellationToken);
                }

                return await inner.StatAsync(Path, CancellationToken);

            }

            public ValueTask MakeDirectoryAsync(String Path, CancellationToken CancellationToken = default)
                => inner.MakeDirectoryAsync(Path, CancellationToken);

            public ValueTask RemoveAsync(String Path, CancellationToken CancellationToken = default)
                => inner.RemoveAsync(Path, CancellationToken);

            public ValueTask RemoveDirectoryAsync(String Path, CancellationToken CancellationToken = default)
                => inner.RemoveDirectoryAsync(Path, CancellationToken);

            public ValueTask RenameAsync(String OldPath, String NewPath, CancellationToken CancellationToken = default)
                => inner.RenameAsync(OldPath, NewPath, CancellationToken);

            public ValueTask<String> RealPathAsync(String Path, CancellationToken CancellationToken = default)
                => inner.RealPathAsync(Path, CancellationToken);

            public ValueTask DisposeAsync()
            {
                Disposed.TrySetResult();
                return ValueTask.CompletedTask;
            }

        }

        #endregion

        #region (private) Fixture

        private readonly ISshHostKey  hostKey  = SshHostKey.GenerateEd25519();
        private readonly ISshHostKey  userKey  = SshHostKey.GenerateEd25519();

        private async Task<(SshServer Server, UInt16 Port)> StartAsync(SftpSessionFactory  Factory,
                                                                       CancellationToken   CancellationToken,
                                                                       ISshAuditSink?      AuditSink       = null,
                                                                       ISftpFileSystem?    SftpFileSystem  = null)
        {

            var server = new SshServer(new SshServerOptions {
                             HostKeys        = [ hostKey ],
                             Authenticator   = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                             ExecHandler     = async (ctx, ct) => { await ctx.WriteAsync($"ran: {ctx.Command}\n", ct); return 0; },
                             SftpSessions    = Factory,
                             SftpFileSystem  = SftpFileSystem,
                             AuditSink       = AuditSink
                         });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return (server, (UInt16) server.LocalEndPoint.Port.ToInt32());

        }

        private ValueTask<SshClient> ConnectAsync(UInt16             Port,
                                                  String             Username,
                                                  CancellationToken  CancellationToken)

            => SshClient.ConnectAsync("127.0.0.1", Port, new SshClientOptions {
                   Username       = Username,
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                   Credentials    = [ userKey ]
               }, CancellationToken);

        private static async Task Within(Task    Happening,
                                         String  What)
        {
            if (await Task.WhenAny(Happening, Task.Delay(TimeSpan.FromSeconds(10))) != Happening)
                Assert.Fail($"{What} did not happen within 10 s.");
        }

        #endregion


        #region EachSession_SeesTheFileSystemItsFactoryMadeForIt

        [Test]
        [CancelAfter(30000)]
        public async Task EachSession_SeesTheFileSystemItsFactoryMadeForIt(CancellationToken CancellationToken)
        {

            var made      = new ConcurrentDictionary<String, SessionFileSystem>();
            var everybody = new InMemorySftpFileSystem();

            var (server, port) = await StartAsync((session, _) => {
                                                      var fileSystem = made.GetOrAdd(session.Username, name => new SessionFileSystem(name));
                                                      return ValueTask.FromResult<SftpSessionOptions?>(new SftpSessionOptions(fileSystem));
                                                  },
                                                  CancellationToken,
                                                  SftpFileSystem: everybody);

            try
            {

                await using var alice = await ConnectAsync(port, "alice", CancellationToken);
                await using var bob   = await ConnectAsync(port, "bob",   CancellationToken);

                var aliceSftp = await alice.OpenSftpClientAsync(CancellationToken);
                var bobSftp   = await bob.  OpenSftpClientAsync(CancellationToken);

                await aliceSftp.UploadAsync("/alice.txt", Encoding.UTF8.GetBytes("alice's"), CancellationToken);
                await bobSftp.  UploadAsync("/bob.txt",   Encoding.UTF8.GetBytes("bob's"),   CancellationToken);

                var aliceSees = (await aliceSftp.ListDirectoryAsync("/", CancellationToken)).Select(entry => entry.Name).ToArray();
                var bobSees   = (await bobSftp.  ListDirectoryAsync("/", CancellationToken)).Select(entry => entry.Name).ToArray();

                await aliceSftp.DisposeAsync();
                await Within(made["alice"].Disposed.Task, "Disposing of alice's file system after she closed SFTP");

                // The server-wide file system is not used where there is a factory.
                var everybodyHas = new List<String>();
                var listing      = await everybody.OpenDirectoryAsync("/", CancellationToken);
                IReadOnlyList<SftpDirectoryEntry> batch;
                while ((batch = await everybody.ReadDirectoryAsync(listing, CancellationToken)).Count > 0)
                    everybodyHas.AddRange(batch.Select(entry => entry.Name));

                Assert.Multiple(() => {
                    Assert.That(made.Keys.Order(),                     Is.EqualTo(new[] { "alice", "bob" }), "the factory was asked with who signed in");
                    Assert.That(aliceSees,                             Does.Contain("alice.txt").And.Not.Contain("bob.txt"));
                    Assert.That(bobSees,                               Does.Contain("bob.txt").And.Not.Contain("alice.txt"));
                    Assert.That(made["bob"].Disposed.Task.IsCompleted, Is.False, "bob's session is still open");
                    Assert.That(everybodyHas,                          Does.Not.Contain("alice.txt").And.Not.Contain("bob.txt"));
                });

                await bobSftp.DisposeAsync();
                await Within(made["bob"].Disposed.Task, "Disposing of bob's file system after he closed SFTP");

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region AnExec_DoesNotAskTheFactory

        [Test]
        [CancelAfter(20000)]
        public async Task AnExec_DoesNotAskTheFactory(CancellationToken CancellationToken)
        {

            var asked = 0;

            var (server, port) = await StartAsync((_, _) => {
                                                      Interlocked.Increment(ref asked);
                                                      return ValueTask.FromResult<SftpSessionOptions?>(new SftpSessionOptions(new InMemorySftpFileSystem()));
                                                  },
                                                  CancellationToken);

            try
            {

                await using var client = await ConnectAsync(port, "achim", CancellationToken);

                var result = await client.ExecuteAsync("status", CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(result.StandardOutput, Is.EqualTo("ran: status\n"));
                    Assert.That(Volatile.Read(ref asked), Is.Zero, "the factory is asked when SFTP is, not for every session channel");
                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region AFactoryThatSaysNo_RefusesTheSubsystem_AndTheConnectionGoesOn

        [Test]
        [CancelAfter(20000)]
        public async Task AFactoryThatSaysNo_RefusesTheSubsystem_AndTheConnectionGoesOn(CancellationToken CancellationToken)
        {

            var audited = new ConcurrentQueue<SshAuditEvent>();

            var (server, port) = await StartAsync((_, _) => ValueTask.FromResult<SftpSessionOptions?>(null),
                                                  CancellationToken,
                                                  AuditSink: new DelegateAuditSink(audited.Enqueue));

            try
            {

                await using var client = await ConnectAsync(port, "achim", CancellationToken);

                var refused = await Assert.CatchAsync<SshWireException>(async () => await client.OpenSftpClientAsync(CancellationToken));
                var result  = await client.ExecuteAsync("status", CancellationToken);

                Assert.Multiple(() => {
                    Assert.That(refused!.Message,      Does.Contain("refused the 'sftp' subsystem"));
                    Assert.That(refused,               Is.TypeOf<SshSubsystemRefusedException>(), "an answer, told apart from a server not reached");
                    Assert.That((refused as SshSubsystemRefusedException)?.Subsystem, Is.EqualTo("sftp"));
                    Assert.That(audited.OfType<PolicyDeniedEvent>().Select(denied => (denied.PolicyType, denied.Target, denied.Reason)),
                                Does.Contain(("subsystem", "sftp", "not served")));
                    Assert.That(audited.OfType<SubsystemRequestedEvent>(), Is.Empty);
                    Assert.That(result.StandardOutput, Is.EqualTo("ran: status\n"), "the connection is still there for what it may do");
                });

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region AFactoryThatFails_RefusesTheSubsystem_WithWhy

        [Test]
        [CancelAfter(20000)]
        public async Task AFactoryThatFails_RefusesTheSubsystem_WithWhy(CancellationToken CancellationToken)
        {

            var audited = new ConcurrentQueue<SshAuditEvent>();

            var (server, port) = await StartAsync((_, _) => throw new InvalidOperationException("the accounts cannot be read"),
                                                  CancellationToken,
                                                  AuditSink: new DelegateAuditSink(audited.Enqueue));

            try
            {

                await using var client = await ConnectAsync(port, "achim", CancellationToken);

                await Assert.CatchAsync<SshWireException>(async () => await client.OpenSftpClientAsync(CancellationToken));

                Assert.That(audited.OfType<PolicyDeniedEvent>().Select(denied => (denied.PolicyType, denied.Target, denied.Reason)),
                            Does.Contain(("subsystem", "sftp", "not served: the accounts cannot be read")),
                            "answered, rather than left waiting for a reply");

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Ends_WhileIdle_ClosesTheChannel_AndDisposesOfTheFileSystem

        [Test]
        [CancelAfter(30000)]
        public async Task Ends_WhileIdle_ClosesTheChannel_AndDisposesOfTheFileSystem(CancellationToken CancellationToken)
        {

            var made = new ConcurrentQueue<(SessionFileSystem FileSystem, CancellationTokenSource Ends)>();

            var (server, port) = await StartAsync((session, _) => {
                                                      var fileSystem = new SessionFileSystem(session.Username);
                                                      var ends       = new CancellationTokenSource();
                                                      made.Enqueue((fileSystem, ends));
                                                      return ValueTask.FromResult<SftpSessionOptions?>(new SftpSessionOptions(fileSystem) { Ends = ends.Token });
                                                  },
                                                  CancellationToken);

            try
            {

                await using var client = await ConnectAsync(port, "achim", CancellationToken);

                var first = await client.OpenSftpClientAsync(CancellationToken);
                await first.UploadAsync("/before.txt", Encoding.UTF8.GetBytes("before"), CancellationToken);

                var (firstFileSystem, firstEnds) = made.Single();

                // Whoever let the session in changes their mind while it waits for its next request.
                await firstEnds.CancelAsync();
                await Within(firstFileSystem.Disposed.Task, "Disposing of the ended session's file system");

                // The client is told by its channel closing, rather than left waiting for an answer.
                using var shortly = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                shortly.CancelAfter(TimeSpan.FromSeconds(5));

                var afterwards = await Assert.CatchAsync<Exception>(async () => await first.StatAsync("/before.txt", shortly.Token));

                // Only the session ended: the connection opens another one.
                var second = await client.OpenSftpClientAsync(CancellationToken);
                await second.UploadAsync("/again.txt", Encoding.UTF8.GetBytes("again"), CancellationToken);
                var again  = await second.DownloadAsync("/again.txt", CancellationToken);
                await second.DisposeAsync();

                Assert.Multiple(() => {
                    Assert.That(afterwards,     Is.Not.InstanceOf<OperationCanceledException>(), "the client was told, not left to time out");
                    Assert.That(made.Count,     Is.EqualTo(2));
                    Assert.That(Encoding.UTF8.GetString(again), Is.EqualTo("again"));
                });

                await first.DisposeAsync();

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

        #region Ends_DuringARequest_FailsIt_AndDisposesOfTheFileSystem

        [Test]
        [CancelAfter(30000)]
        public async Task Ends_DuringARequest_FailsIt_AndDisposesOfTheFileSystem(CancellationToken CancellationToken)
        {

            var fileSystem = new SessionFileSystem("achim") { HoldStat = true };
            var ends       = new CancellationTokenSource();

            var (server, port) = await StartAsync((_, _) => ValueTask.FromResult<SftpSessionOptions?>(new SftpSessionOptions(fileSystem) { Ends = ends.Token }),
                                                  CancellationToken);

            try
            {

                await using var client = await ConnectAsync(port, "achim", CancellationToken);

                var sftp = await client.OpenSftpClientAsync(CancellationToken);

                using var shortly = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                shortly.CancelAfter(TimeSpan.FromSeconds(10));

                var stat = sftp.StatAsync("/anything", shortly.Token).AsTask();

                await Within(fileSystem.StatHeld.Task, "The STAT reaching the file system");

                await ends.CancelAsync();

                var failed = await Assert.CatchAsync<Exception>(async () => await stat);
                await Within(fileSystem.Disposed.Task, "Disposing of the ended session's file system");

                Assert.That(failed, Is.Not.InstanceOf<OperationCanceledException>(), "the request in flight failed when the session ended, rather than timing out");

                await sftp.DisposeAsync();

            }
            finally
            {
                await server.DisposeAsync();
                ends.Dispose();
            }

        }

        #endregion

    }

}
