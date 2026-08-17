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

using System.Buffers;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M6 connection layer: remote command execution (open session, exec, capture, exit-status).
    /// </summary>
    [TestFixture]
    public class RemoteExecTests
    {

        #region Exec_CapturesStdoutStderrAndExitCode

        [Test]
        [CancelAfter(15000)]
        public async Task Exec_CapturesStdoutStderrAndExitCode(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                await SshConnection.ServeExecAsync(t, "achim", async (context, ct) =>
                {
                    await context.WriteLineAsync($"you ran: {context.Command}", ct);
                    await context.WriteErrorAsync(Encoding.UTF8.GetBytes("a warning\n"), ct);
                    return 7;
                }, CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                Assert.That(await UserAuthentication.ClientPublicKeyAuthenticateAsync(t, "achim", userKey, CancellationToken: CancellationToken), Is.True);
                return await SshConnection.ExecuteAsync(t, "uname -a", CancellationToken);
            }, CancellationToken);

            var result = await clientRun;
            await serverRun;

            Assert.Multiple(() => {
                Assert.That(result.ExitCode,       Is.EqualTo(7));
                Assert.That(result.StandardOutput, Is.EqualTo("you ran: uname -a\n"));
                Assert.That(result.StandardError,  Does.Contain("a warning"));
                Assert.That(result.Success,        Is.False);
            });

        }

        #endregion

        #region Exec_SurvivesAPeerThatWalksAwayAfterTheExitStatus

        private static Byte[] OpenSessionChannel(UInt32 SenderChannel)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelOpen);
            w.WriteString("session");
            w.WriteUInt32(SenderChannel);
            w.WriteUInt32(64 * 1024);
            w.WriteUInt32(32 * 1024);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] ExecRequest(UInt32 RecipientChannel, String Command)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteByte((Byte) SshMessageNumber.ChannelRequest);
            w.WriteUInt32(RecipientChannel);
            w.WriteString("exec");
            w.WriteBoolean(false);
            w.WriteString(Command);
            return abw.WrittenSpan.ToArray();
        }

        /// <summary>
        /// A peer that walks away once it holds the exit status has <i>ended</i> the session, not broken it.
        ///
        /// <para>
        /// OpenSSH does exactly this: it exits the moment it has our exit-status, without waiting to
        /// exchange the CHANNEL_CLOSE that <c>ServeExecAsync</c> otherwise returns on — and on Windows that
        /// process exit reaches us as a TCP reset. Both endings are scripted here, because the transport
        /// reports them as two different exception types for one and the same event: a pipe completed
        /// cleanly becomes an <c>SshWireException</c> about a packet cut in half, a pipe completed with an
        /// I/O failure stays an <c>IOException</c>. Neither may fail the session.
        /// </para>
        ///
        /// <para>
        /// The client is scripted by hand rather than driven through <c>ExecuteAsync</c>: the polite client
        /// sends its own CHANNEL_CLOSE, which is precisely the packet whose absence is under test.
        /// </para>
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        [TestCase(false)]
        [TestCase(true)]
        public async Task Exec_SurvivesAPeerThatWalksAwayAfterTheExitStatus(Boolean AsReset, CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey       = Ed25519KeyPair.Generate();
            var userKey       = SshHostKey.GenerateEd25519();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                await SshConnection.ServeExecAsync(t, "achim", async (context, ct) =>
                {
                    await context.WriteAsync("done\n", ct);
                    return 42;
                }, CancellationToken);
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            Assert.That(await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken), Is.True);

            await client.SendPacketAsync(OpenSessionChannel(0), CancellationToken);

            var serverChannel = 0u;
            while (true)
            {
                var packet = await client.ReceivePacketAsync(CancellationToken);
                if ((SshMessageNumber) packet[0] == SshMessageNumber.ChannelOpenConfirmation)
                {
                    var reader = new SshPacketReader(packet);
                    reader.ReadByte();
                    reader.ReadUInt32();                    // our own channel
                    serverChannel = reader.ReadUInt32();    // theirs
                    break;
                }
            }

            await client.SendPacketAsync(ExecRequest(serverChannel, "whoami"), CancellationToken);

            // Read until the server has said everything it owes us — exit-status, EOF and CLOSE.
            while ((SshMessageNumber) (await client.ReceivePacketAsync(CancellationToken))[0] != SshMessageNumber.ChannelClose)
            { }

            // ...and now simply stop existing, without ever sending a CLOSE of our own.
            await clientPipe.Output.CompleteAsync(AsReset
                                                      ? new IOException("An existing connection was forcibly closed by the remote host.")
                                                      : null);

            Assert.That(async () => await serverRun, Throws.Nothing,
                        "a peer that leaves once it holds the exit status has ended the session, not broken it");

        }

        #endregion

        #region Exec_LargeOutput_IsCapturedFully

        [Test]
        [CancelAfter(15000)]
        public async Task Exec_LargeOutput_IsCapturedFully(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            // ~200 KiB of output crosses several 32 KiB channel packets.
            var line = new String('x', 1000) + "\n";
            const Int32 lineCount = 200;

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                await SshConnection.ServeExecAsync(t, "achim", async (context, ct) =>
                {
                    for (var i = 0; i < lineCount; i++)
                        await context.WriteAsync(line, ct);
                    return 0;
                }, CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                await UserAuthentication.ClientPublicKeyAuthenticateAsync(t, "achim", userKey, CancellationToken: CancellationToken);
                return await SshConnection.ExecuteAsync(t, "generate", CancellationToken);
            }, CancellationToken);

            var result = await clientRun;
            await serverRun;

            Assert.Multiple(() => {
                Assert.That(result.ExitCode,              Is.EqualTo(0));
                Assert.That(result.StandardOutputBytes.Length, Is.EqualTo(line.Length * lineCount));
                Assert.That(result.StandardOutput,        Is.EqualTo(String.Concat(Enumerable.Repeat(line, lineCount))));
            });

        }

        #endregion

    }

}
