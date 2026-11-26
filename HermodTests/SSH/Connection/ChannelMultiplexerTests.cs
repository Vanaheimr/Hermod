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
using System.Buffers.Binary;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M-mux: the connection multiplexer runs many channels concurrently on one connection.
    /// </summary>
    [TestFixture]
    public class ChannelMultiplexerTests
    {

        private static Byte[] EncodeString(String s)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteString(s);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] EncodeUInt32(UInt32 v)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteUInt32(v);
            return abw.WrittenSpan.ToArray();
        }

        private static String ReadString(Byte[] b) { var r = new SshPacketReader(b); return r.ReadString(); }
        private static UInt32 ReadUInt32(Byte[] b) { var r = new SshPacketReader(b); return r.ReadUInt32(); }


        #region Mux_RunsTwoExecChannels_Concurrently

        [Test]
        [CancelAfter(20000)]
        public async Task Mux_RunsTwoExecChannels_Concurrently(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var server = Task.Run(async () =>
            {
                try
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);

                    await using var mux = new SshChannelMultiplexer(t).Start();
                    var handlers = new List<Task>();

                    for (var i = 0; i < 2; i++)
                    {
                        var ch = await mux.AcceptChannelAsync(CancellationToken);
                        handlers.Add(Task.Run(async () =>
                        {
                            var req = await ch.ReadRequestAsync(CancellationToken);   // exec
                            var cmd = ReadString(req!.Value.Data);
                            if (req.Value.WantReply) await ch.ReplyAsync(true, CancellationToken);

                            // A little back-and-forth to prove the two channels are truly interleaved.
                            await ch.SendDataAsync(Encoding.UTF8.GetBytes($"ran: {cmd}\n"), CancellationToken);
                            await Task.Delay(20, CancellationToken);
                            await ch.SendDataAsync(Encoding.UTF8.GetBytes($"done: {cmd}\n"), CancellationToken);

                            await ch.SendRequestAsync("exit-status", false, EncodeUInt32(cmd == "boom" ? 7u : 0u), CancellationToken);
                            await ch.CloseAsync(CancellationToken);
                            await ch.Closed;
                        }, CancellationToken));
                    }

                    await Task.WhenAll(handlers);
                }
                catch { }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, User_Id.Parse("achim"), userKey, CancellationToken: CancellationToken);

            await using var clientMux = new SshChannelMultiplexer(client).Start();

            async Task<(String Output, Int32 Exit)> RunAsync(String command)
            {
                var ch = await clientMux.OpenChannelAsync("session", CancellationToken: CancellationToken);
                await ch.SendRequestAsync("exec", true, EncodeString(command), CancellationToken);

                var output = await new StreamReader(ch.Input).ReadToEndAsync(CancellationToken);

                var exit = -1;
                SshChannelRequest? r;
                while ((r = await ch.ReadRequestAsync(CancellationToken)) is not null)
                    if (r.Value.Type == "exit-status")
                        exit = (Int32) ReadUInt32(r.Value.Data);

                return (output, exit);
            }

            // Two exec channels driven in parallel over the single connection.
            var runA = RunAsync("uname");
            var runB = RunAsync("boom");
            var (outA, exitA) = await runA;
            var (outB, exitB) = await runB;

            await server;

            Assert.Multiple(() => {
                Assert.That(outA,  Is.EqualTo("ran: uname\ndone: uname\n"));
                Assert.That(exitA, Is.EqualTo(0));
                Assert.That(outB,  Is.EqualTo("ran: boom\ndone: boom\n"));
                Assert.That(exitB, Is.EqualTo(7));
            });

        }

        #endregion

        #region CleanDisconnect_FaultsPendingGlobalAndChannelRequests

        /// <summary>
        /// When the peer closes cleanly with <c>SSH_MSG_DISCONNECT</c> while want-reply global or channel
        /// requests are in flight, callers must fail promptly with <see cref="SshChannelClosedException"/>
        /// rather than waiting forever.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task CleanDisconnect_FaultsPendingGlobalAndChannelRequests(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            await using var clientMux = new SshChannelMultiplexer(client) { RekeyLimits = null }.Start();

            // Bare server confirms one channel, reads one GlobalRequest and one ChannelRequest, then disconnects cleanly.
            var serverRun = Task.Run(async () =>
            {
                var open = await server.ReceivePacketAsync(CancellationToken);
                Assert.That(open[0], Is.EqualTo((Byte) SshMessageNumber.ChannelOpen));
                var r = new SshPacketReader(open); r.ReadByte(); r.ReadString();
                var senderId = r.ReadUInt32();
                await server.SendPacketAsync(SshChannelWire.ChannelOpenConfirmation(senderId, 99, 65536, 32768), CancellationToken);

                // Read both in-flight requests without answering either, then send SSH_MSG_DISCONNECT.
                _ = await server.ReceivePacketAsync(CancellationToken);
                _ = await server.ReceivePacketAsync(CancellationToken);

                var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                w.WriteByte((Byte) SshMessageNumber.Disconnect);
                w.WriteUInt32(11);   // SSH_DISCONNECT_BY_APPLICATION
                w.WriteString("bye");
                w.WriteString("");
                await server.SendPacketAsync(abw.WrittenSpan.ToArray(), CancellationToken);
            }, CancellationToken);

            var channel = await clientMux.OpenChannelAsync("session", CancellationToken: CancellationToken);

            var pendingGlobal  = clientMux.SendGlobalRequestWithReplyAsync("custom@example.test", true, [], CancellationToken).AsTask();
            var pendingChannel = channel.SendRequestAsync("exec", true, EncodeString("sleep 60"), CancellationToken).AsTask();

            await serverRun;

            await Assert.ThrowsAsync<SshChannelClosedException>(async () => await pendingGlobal);
            await Assert.ThrowsAsync<SshChannelClosedException>(async () => await pendingChannel);

        }

        #endregion

        #region ConcurrentGlobalRequests_MatchRepliesInStrictFifoOrder

        /// <summary>
        /// Concurrent want-reply global requests must be enqueued and written to the wire in the exact same
        /// order so every caller receives its own reply payload (RFC 4254 §4).
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task ConcurrentGlobalRequests_MatchRepliesInStrictFifoOrder(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            await using var serverMux = new SshChannelMultiplexer(server) {
                RekeyLimits            = null,
                GlobalRequestResponder = info => ValueTask.FromResult<SshGlobalRequestReply?>(SshGlobalRequestReply.WithData(info.Data))
            }.Start();

            await using var clientMux = new SshChannelMultiplexer(client) { RekeyLimits = null }.Start();

            const Int32 count = 32;
            var tasks = Enumerable.Range(0, count).Select(i => Task.Run(async () =>
            {
                var payload = EncodeUInt32((UInt32) i);
                var reply   = await clientMux.SendGlobalRequestWithReplyAsync("echo@example.test", true, payload, CancellationToken);
                return (Expected: (UInt32) i, Actual: ReadUInt32(reply.Data), reply.Success);
            }, CancellationToken)).ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Multiple(() => {
                foreach (var (expected, actual, success) in results)
                {
                    Assert.That(success, Is.True);
                    Assert.That(actual,  Is.EqualTo(expected), "each concurrent global request must receive its own FIFO reply");
                }
            });

        }

        #endregion

        #region CancelledOpenChannel_ClosesOrphanRemoteChannel

        /// <summary>
        /// If a caller cancels <see cref="SshChannelMultiplexer.OpenChannelAsync"/> before the peer's
        /// <c>CHANNEL_OPEN_CONFIRMATION</c> arrives, the late confirmation must trigger a
        /// <c>CHANNEL_CLOSE</c> so the peer does not leak its half of the channel.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task CancelledOpenChannel_ClosesOrphanRemoteChannel(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            await using var clientMux = new SshChannelMultiplexer(client) { RekeyLimits = null }.Start();

            var openCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var serverRun = Task.Run(async () =>
            {
                var open = await server.ReceivePacketAsync(CancellationToken);
                Assert.That(open[0], Is.EqualTo((Byte) SshMessageNumber.ChannelOpen));
                var r = new SshPacketReader(open); r.ReadByte(); r.ReadString();
                var clientLocalId = r.ReadUInt32();

                // Wait until the client has cancelled its OpenChannelAsync call, then send a late confirmation.
                await openCancelled.Task.WaitAsync(CancellationToken);
                await server.SendPacketAsync(SshChannelWire.ChannelOpenConfirmation(clientLocalId, 42, 65536, 32768), CancellationToken);

                var close = await server.ReceivePacketAsync(CancellationToken);
                Assert.That(close[0], Is.EqualTo((Byte) SshMessageNumber.ChannelClose));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(close.AsSpan(1)), Is.EqualTo(42u));
            }, CancellationToken);

            using var openCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await clientMux.OpenChannelAsync("session", CancellationToken: openCts.Token));

            openCancelled.SetResult();
            await serverRun;

        }

        #endregion

    }

}
