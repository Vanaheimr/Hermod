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
    /// A credential confined to one command (<c>force-command</c>) is confined for a subsystem as well.
    ///
    /// <para>
    /// Shell and exec honoured the forced command, but a subsystem request was looked up before it and
    /// served regardless: a certificate a CA had issued for one backup command opened SFTP on the whole
    /// file system. sshd runs the forced command for a subsystem request too, and serves SFTP only to a
    /// credential forced to plain <c>internal-sftp</c> - so does the server now. Each test runs with both
    /// ways SFTP is configured: a file system for every session, and a factory asked per session (as
    /// WWCP_Node serves it).
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Security")]
    public class ForceCommandSubsystemTests
    {

        #region (private) Fixture

        private sealed record Running(SshServer Server, UInt16 Port, ISshHostKey HostKey, CollectingAuditSink Audit) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
                => Server.DisposeAsync();
        }

        private static (CertifiedKey Credential, SshCertificateAuthorityTrust Trust) IssueForcedTo(String ForcedCommand)
        {

            var caKey   = SshHostKey.GenerateEd25519();
            var userKey = SshHostKey.GenerateEd25519();

            var builder = new OpenSshCertificateBuilder {
                Serial       = 11,
                Type         = SshCertType.User,
                KeyId        = "forced",
                Principals   = [ "achim" ],
                ValidAfter   = DateTimeOffset.UtcNow.AddDays(-1),
                ValidBefore  = DateTimeOffset.UtcNow.AddDays(1)
            };

            foreach (var extension in OpenSshCertificateBuilder.DefaultUserExtensions)
                builder.Extensions.Add(new KeyValuePair<String, Byte[]>(extension.Key, []));

            var abw = new System.Buffers.ArrayBufferWriter<Byte>();
            new SshPacketWriter(abw).WriteString(ForcedCommand);   // the option data is itself an SSH string
            builder.CriticalOptions.Add(new KeyValuePair<String, Byte[]>("force-command", abw.WrittenSpan.ToArray()));

            return (new CertifiedKey(userKey, builder.Sign(userKey.PublicKeyBlob, caKey)),
                    new SshCertificateAuthorityTrust().TrustCA(caKey));

        }

        private static async Task<Running> StartAsync(SshCertificateAuthorityTrust  Trust,
                                                      Boolean                       PerSession,
                                                      Boolean                       WithExec,
                                                      CancellationToken             CancellationToken)
        {

            var hostKey = SshHostKey.GenerateEd25519();
            var audit   = new CollectingAuditSink();

            var server  = new SshServer(new SshServerOptions {
                              HostKeys        = [ hostKey ],
                              Authenticator   = new SshAuthenticationPolicy().WithCertificateAuthority(Trust),
                              AuditSink       = audit,
                              ExecHandler     = WithExec
                                                    ? async (ctx, ct) => {
                                                          await ctx.WriteAsync($"ran:{ctx.Command}|original:{ctx.OriginalCommand ?? "-"}\n", ct);
                                                          return 0;
                                                      }
                                                    : null,
                              SftpFileSystem  = PerSession ? null : new InMemorySftpFileSystem(),
                              SftpSessions    = PerSession ? (session, ct) => ValueTask.FromResult<SftpSessionOptions?>(new SftpSessionOptions(new InMemorySftpFileSystem())) : null
                          });

            await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

            return new Running(server, (UInt16) server.LocalEndPoint.Port.ToInt32(), hostKey, audit);

        }

        private static ValueTask<SshClient> ConnectAsync(Running Running, CertifiedKey Credential, CancellationToken CancellationToken)

            => SshClient.ConnectAsync("127.0.0.1", Running.Port, new SshClientOptions {
                   Username       = "achim",
                   VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(Running.HostKey.PublicKeyBlob),
                   Credentials    = [ Credential ]
               }, CancellationToken);

        /// <summary>
        /// The channel's output to its end - or what came within 5 s, for a channel that does not end
        /// because it is serving SFTP and waits for the client's INIT.
        /// </summary>
        private static async Task<(SshCommandResult? Result, Boolean Ended)> CaptureWithin5s(SshMuxChannel Channel, CancellationToken CancellationToken)
        {

            using var within = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            within.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                return (await SshSessionChannel.CaptureAsync(Channel, within.Token), true);
            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {
                return (null, false);
            }

        }

        #endregion


        #region ForcedCommand_SubsystemRequest_RunsTheForcedCommand

        /// <summary>
        /// As with sshd: the forced command runs instead of the subsystem, which the handler sees as the
        /// original command - and no SFTP is served.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        [CancelAfter(25000)]
        public async Task ForcedCommand_SubsystemRequest_RunsTheForcedCommand(Boolean PerSession, CancellationToken CancellationToken)
        {

            var (credential, trust) = IssueForcedTo("/usr/bin/backup-only");

            await using var running = await StartAsync(trust, PerSession, WithExec: true, CancellationToken);
            await using var client  = await ConnectAsync(running, credential, CancellationToken);

            var channel  = await client.Multiplexer.OpenChannelAsync("session", CancellationToken: CancellationToken);
            var accepted = await channel.SendRequestAsync("subsystem", true, SshSessionChannel.EncodeString("sftp"), CancellationToken);

            var (result, ended) = await CaptureWithin5s(channel, CancellationToken);

            Assert.That(ended, Is.True, "the channel did not end: SFTP was served to a credential confined to one command");

            Assert.Multiple(() => {

                Assert.That(accepted,               Is.True,                                               "sshd accepts the request and runs the forced command");
                Assert.That(result!.StandardOutput, Is.EqualTo("ran:/usr/bin/backup-only|original:sftp\n"), "the forced command must be what runs");
                Assert.That(result!.ExitCode,       Is.EqualTo(0));

                Assert.That(running.Audit.Events.OfType<SubsystemRequestedEvent>(),                   Is.Empty, "a subsystem was served");
                Assert.That(running.Audit.Events.OfType<ExecRequestedEvent>().Select(e => e.Command), Is.EqualTo(new[] { "/usr/bin/backup-only" }));

            });

        }

        #endregion

        #region ForcedCommand_WithoutExecHandler_SubsystemIsRefused

        /// <summary>
        /// Where no command is run, a credential confined to one gets nothing - not the subsystem instead.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        [CancelAfter(25000)]
        public async Task ForcedCommand_WithoutExecHandler_SubsystemIsRefused(Boolean PerSession, CancellationToken CancellationToken)
        {

            var (credential, trust) = IssueForcedTo("/usr/bin/backup-only");

            await using var running = await StartAsync(trust, PerSession, WithExec: false, CancellationToken);
            await using var client  = await ConnectAsync(running, credential, CancellationToken);

            var channel  = await client.Multiplexer.OpenChannelAsync("session", CancellationToken: CancellationToken);
            var accepted = await channel.SendRequestAsync("subsystem", true, SshSessionChannel.EncodeString("sftp"), CancellationToken);

            Assert.Multiple(() => {

                Assert.That(accepted, Is.False, "SFTP was served to a credential confined to one command");

                Assert.That(running.Audit.Events.OfType<SubsystemRequestedEvent>(), Is.Empty, "a subsystem was served");

                Assert.That(running.Audit.Events.OfType<PolicyDeniedEvent>().Where(e => e.PolicyType == "subsystem").Select(e => e.Target),
                            Is.EqualTo(new[] { "sftp" }), "the refusal was not audited");

            });

        }

        #endregion

        #region ForcedInternalSftp_IsServedSftp

        /// <summary>
        /// sshd's one exception, and the usual way to issue an SFTP-only credential: forced to plain
        /// <c>internal-sftp</c>, the SFTP subsystem is served - not the exec handler.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        [CancelAfter(25000)]
        public async Task ForcedInternalSftp_IsServedSftp(Boolean PerSession, CancellationToken CancellationToken)
        {

            var (credential, trust) = IssueForcedTo("internal-sftp");

            await using var running = await StartAsync(trust, PerSession, WithExec: true, CancellationToken);
            await using var client  = await ConnectAsync(running, credential, CancellationToken);
            await using var sftp    = await client.OpenSftpClientAsync(CancellationToken);

            await sftp.UploadAsync("/report.txt", Encoding.UTF8.GetBytes("served"), CancellationToken);

            var downloaded = await sftp.DownloadAsync("/report.txt", CancellationToken);

            Assert.Multiple(() => {

                Assert.That(Encoding.UTF8.GetString(downloaded), Is.EqualTo("served"));

                Assert.That(running.Audit.Events.OfType<SubsystemRequestedEvent>().Select(e => e.Subsystem), Is.EqualTo(new[] { "sftp" }));
                Assert.That(running.Audit.Events.OfType<ExecRequestedEvent>(),                               Is.Empty, "the exec handler ran");

            });

        }

        #endregion

        #region ForcedInternalSftpWithOptions_IsRefused

        /// <summary>
        /// <c>internal-sftp -R</c> asks for a read-only SFTP this server cannot give: refused, rather than
        /// served read-write - and not handed to the exec handler either, which could not run it.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        [CancelAfter(25000)]
        public async Task ForcedInternalSftpWithOptions_IsRefused(Boolean PerSession, CancellationToken CancellationToken)
        {

            var (credential, trust) = IssueForcedTo("internal-sftp -R");

            await using var running = await StartAsync(trust, PerSession, WithExec: true, CancellationToken);
            await using var client  = await ConnectAsync(running, credential, CancellationToken);

            var channel  = await client.Multiplexer.OpenChannelAsync("session", CancellationToken: CancellationToken);
            var accepted = await channel.SendRequestAsync("subsystem", true, SshSessionChannel.EncodeString("sftp"), CancellationToken);

            Assert.Multiple(() => {

                Assert.That(accepted, Is.False, "a read-only credential was served SFTP it could write with");

                Assert.That(running.Audit.Events.OfType<SubsystemRequestedEvent>(), Is.Empty, "a subsystem was served");
                Assert.That(running.Audit.Events.OfType<ExecRequestedEvent>(),      Is.Empty, "the exec handler ran");

            });

        }

        #endregion

    }

}
