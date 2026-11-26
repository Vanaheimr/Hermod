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

        #region SendData_RespectsSmallPeerMaxPacket

        /// <summary>
        /// RFC 4254 §5.1: outbound <c>CHANNEL_DATA</c> must respect the peer's advertised maximum packet
        /// size rather than assuming 32 KiB.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task SendData_RespectsSmallPeerMaxPacket(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            await using var clientMux = new SshChannelMultiplexer(client) { RekeyLimits = null }.Start();

            const UInt32 peerMaxPacket = 1024;
            var          sent          = Enumerable.Range(0, 5000).Select(i => (Byte) (i & 0xFF)).ToArray();

            var serverRun = Task.Run(async () =>
            {
                var open = await server.ReceivePacketAsync(CancellationToken);
                var r    = new SshPacketReader(open); r.ReadByte(); r.ReadString();
                var clientLocalId = r.ReadUInt32();
                await server.SendPacketAsync(SshChannelWire.ChannelOpenConfirmation(clientLocalId, 10, 65536, peerMaxPacket), CancellationToken);

                var collected = new MemoryStream();
                while (collected.Length < sent.Length)
                {
                    var pkt = await server.ReceivePacketAsync(CancellationToken);
                    Assert.That(pkt[0], Is.EqualTo((Byte) SshMessageNumber.ChannelData));
                    var pr = new SshPacketReader(pkt); pr.ReadByte(); pr.ReadUInt32();
                    var chunk = pr.ReadBinaryString();
                    Assert.That(chunk.Length, Is.LessThanOrEqualTo((Int32) peerMaxPacket),
                                "every CHANNEL_DATA chunk must respect the peer's MaxPacket");
                    collected.Write(chunk);
                }

                return collected.ToArray();
            }, CancellationToken);

            var channel = await clientMux.OpenChannelAsync("session", CancellationToken: CancellationToken);
            await channel.SendDataAsync(sent, CancellationToken);

            var received = await serverRun;
            Assert.That(received, Is.EqualTo(sent));

        }

        #endregion

        #region SendError_RespectsPeerWindowAndMaxPacket

        /// <summary>
        /// RFC 4254 §5.2: <c>SSH_MSG_CHANNEL_EXTENDED_DATA</c> (stderr) consumes the same channel window
        /// and obeys the same maximum packet size as <c>SSH_MSG_CHANNEL_DATA</c>.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task SendError_RespectsPeerWindowAndMaxPacket(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            await using var clientMux = new SshChannelMultiplexer(client) { RekeyLimits = null }.Start();

            const UInt32 peerWindow    = 2048;
            const UInt32 peerMaxPacket = 1024;
            var          sent          = Enumerable.Range(0, 3000).Select(i => (Byte) (i % 251)).ToArray();

            var serverRun = Task.Run(async () =>
            {
                var open = await server.ReceivePacketAsync(CancellationToken);
                var r    = new SshPacketReader(open); r.ReadByte(); r.ReadString();
                var clientLocalId = r.ReadUInt32();
                await server.SendPacketAsync(SshChannelWire.ChannelOpenConfirmation(clientLocalId, 11, peerWindow, peerMaxPacket), CancellationToken);

                var collected = new MemoryStream();
                // First 2048 bytes arrive in two 1024-byte extended-data packets; then the sender must pause.
                for (var i = 0; i < 2; i++)
                {
                    var pkt = await server.ReceivePacketAsync(CancellationToken);
                    Assert.That(pkt[0], Is.EqualTo((Byte) SshMessageNumber.ChannelExtendedData));
                    var pr = new SshPacketReader(pkt); pr.ReadByte(); pr.ReadUInt32();
                    Assert.That(pr.ReadUInt32(), Is.EqualTo(1u));
                    var chunk = pr.ReadBinaryString();
                    Assert.That(chunk.Length, Is.EqualTo(1024));
                    collected.Write(chunk);
                }

                // Grant window for the remaining 952 bytes.
                await server.SendPacketAsync(SshChannelWire.WindowAdjust(clientLocalId, 1024), CancellationToken);

                var lastPkt = await server.ReceivePacketAsync(CancellationToken);
                Assert.That(lastPkt[0], Is.EqualTo((Byte) SshMessageNumber.ChannelExtendedData));
                var lpr = new SshPacketReader(lastPkt); lpr.ReadByte(); lpr.ReadUInt32(); lpr.ReadUInt32();
                collected.Write(lpr.ReadBinaryString());

                return collected.ToArray();
            }, CancellationToken);

            var channel = await clientMux.OpenChannelAsync("session", CancellationToken: CancellationToken);
            await channel.SendErrorAsync(sent, CancellationToken);

            var received = await serverRun;
            Assert.That(received, Is.EqualTo(sent));

        }

        #endregion

        #region ReceiveWindowOverflow_FaultsConnection

        /// <summary>
        /// A misbehaving peer that ignores our advertised <see cref="SshChannelMultiplexer.InitialWindow"/>
        /// and keeps pouring data must be cut off with <see cref="SshWireException"/> rather than driving
        /// the process into OOM.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task ReceiveWindowOverflow_FaultsConnection(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            await using var clientMux = new SshChannelMultiplexer(client) { RekeyLimits = null }.Start();

            var serverRun = Task.Run(async () =>
            {
                var open = await server.ReceivePacketAsync(CancellationToken);
                var r    = new SshPacketReader(open); r.ReadByte(); r.ReadString();
                var clientLocalId = r.ReadUInt32();
                await server.SendPacketAsync(SshChannelWire.ChannelOpenConfirmation(clientLocalId, 12, 65536, 32768), CancellationToken);

                // Blast 65 × 32 KiB = 2 MiB + 32 KiB without ever waiting for a WINDOW_ADJUST.
                var chunk = new Byte[32 * 1024];
                for (var i = 0; i < 65; i++)
                {
                    try { await server.SendPacketAsync(SshChannelWire.ChannelData(clientLocalId, chunk), CancellationToken); }
                    catch { break; }
                }
            }, CancellationToken);

            _ = await clientMux.OpenChannelAsync("session", CancellationToken: CancellationToken);

            await clientMux.Completion.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            Assert.That(clientMux.EndReason, Is.InstanceOf<SshWireException>());

            await serverRun;

        }

        #endregion

        #region ExecRefusedByServer_ThrowsInsteadOfHanging

        /// <summary>
        /// If a server refuses an <c>exec</c> request (<c>CHANNEL_FAILURE</c>) without closing the channel,
        /// <see cref="SshSessionChannel.ExecuteAsync"/> must fail immediately and close the channel instead
        /// of hanging in <c>CaptureAsync</c>.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task ExecRefusedByServer_ThrowsInsteadOfHanging(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            await using var clientMux = new SshChannelMultiplexer(client) { RekeyLimits = null }.Start();

            var serverRun = Task.Run(async () =>
            {
                var open = await server.ReceivePacketAsync(CancellationToken);
                var r    = new SshPacketReader(open); r.ReadByte(); r.ReadString();
                var clientLocalId = r.ReadUInt32();
                await server.SendPacketAsync(SshChannelWire.ChannelOpenConfirmation(clientLocalId, 13, 65536, 32768), CancellationToken);

                var req = await server.ReceivePacketAsync(CancellationToken);
                Assert.That(req[0], Is.EqualTo((Byte) SshMessageNumber.ChannelRequest));

                // Refuse exec and deliberately leave the channel open.
                await server.SendPacketAsync(SshChannelWire.ChannelFailure(clientLocalId), CancellationToken);

                // Client must close the channel after seeing the refusal.
                Byte[] pkt;
                do { pkt = await server.ReceivePacketAsync(CancellationToken); }
                while (pkt[0] == (Byte) SshMessageNumber.ChannelEof);
                Assert.That(pkt[0], Is.EqualTo((Byte) SshMessageNumber.ChannelClose));
            }, CancellationToken);

            await Assert.ThrowsAsync<SshChannelClosedException>(async () =>
                await SshSessionChannel.ExecuteAsync(clientMux, "forbidden", CancellationToken));

            await serverRun;

        }

        #endregion

    }

}
