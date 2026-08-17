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

using System.Buffers.Binary;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// What a server owes an <b>unknown</b> channel request (RFC 4254 §5.4), pinned against the peer that
    /// cares most about it.
    ///
    /// <para>
    /// PuTTY sends <c>winadj@putty.projects.tartarus.org</c> to time its window adjustments and waits for
    /// an answer. <c>SSH_MSG_CHANNEL_FAILURE</c> is the right one — PuTTY only needs to know the request
    /// was seen — but <i>no</i> answer strands it, a bug that has bitten several implementations. The
    /// converse matters too: when <c>want_reply</c> is false the RFC says no response will be sent, so an
    /// unsolicited reply would leave the peer's request queue out of step with reality.
    /// </para>
    ///
    /// <para>
    /// This lives in the library's own suite rather than only in the PuTTY interop test because whether
    /// PuTTY sends a winadj is PuTTY's business: plink 0.83 sends none for a plain <c>exec</c>, so the
    /// interop assertion is necessarily conditional. The contract itself is not.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    public class ChannelRequestContractTests
    {

        #region (private) helpers

        private const String WinAdj = "winadj@putty.projects.tartarus.org";

        /// <summary>
        /// A connected client/server multiplexer pair over an in-memory pipe.
        /// </summary>
        private static async Task<(SshChannelMultiplexer Client, SshChannelMultiplexer Server, SshTransport ClientTransport, SshTransport ServerTransport)>
            ConnectedPairAsync(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            var clientTransport = await clientTask;
            var serverTransport = await serverTask;

            // The server's post-handshake EXT_INFO would otherwise be the multiplexer's first packet.
            await clientTransport.ReceivePacketAsync(CancellationToken);

            return (new SshChannelMultiplexer(clientTransport),
                    new SshChannelMultiplexer(serverTransport),
                    clientTransport,
                    serverTransport);

        }

        #endregion


        #region UnknownChannelRequest_WithWantReply_IsAnswered

        /// <summary>
        /// The PuTTY case: an unknown request that wants a reply must get one, and it must be a failure —
        /// answering "success" would claim we did something we did not.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task UnknownChannelRequest_WithWantReply_IsAnswered(CancellationToken CancellationToken)
        {

            var (client, server, clientTransport, serverTransport) = await ConnectedPairAsync(CancellationToken);

            await using (client)
            await using (server)
            using (clientTransport)
            using (serverTransport)
            {

                server.ChannelAcceptor = _ => new ValueTask<Boolean>(true);
                client.Start();
                server.Start();

                var serving = Task.Run(async () => {
                                  var channel = await server.AcceptChannelAsync(CancellationToken);
                                  await SshSessionChannel.ServeAsync(
                                            channel, "hermoduser",
                                            async (context, ct) => { await context.WriteAsync("done\n", ct); return 0; },
                                            null, CancellationToken);
                              }, CancellationToken);

                var session = await client.OpenChannelAsync("session", null, CancellationToken);

                var answered = await session.SendRequestAsync(WinAdj, true, [], CancellationToken);

                Assert.That(answered, Is.False,
                            $"'{WinAdj}' must be answered, and refused — PuTTY needs the answer, not the success");

                // The channel must still be usable afterwards: the request is a measurement, not an error.
                await session.SendRequestAsync("exec", false, EncodeString("after-winadj"), CancellationToken);
                await serving;

            }

        }

        #endregion

        #region UnknownChannelRequest_WithoutWantReply_IsNotAnswered

        /// <summary>
        /// The converse half of RFC 4254 §5.4: without <c>want_reply</c> there must be no response at all.
        /// An unsolicited SUCCESS/FAILURE would be matched against the peer's <i>next</i> request and put
        /// its reply queue permanently out of step.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task UnknownChannelRequest_WithoutWantReply_IsNotAnswered(CancellationToken CancellationToken)
        {

            var (client, server, clientTransport, serverTransport) = await ConnectedPairAsync(CancellationToken);

            await using (client)
            await using (server)
            using (clientTransport)
            using (serverTransport)
            {

                server.ChannelAcceptor = _ => new ValueTask<Boolean>(true);
                client.Start();
                server.Start();

                var serving = Task.Run(async () => {
                                  var channel = await server.AcceptChannelAsync(CancellationToken);
                                  await SshSessionChannel.ServeAsync(
                                            channel, "hermoduser",
                                            async (context, ct) => { await context.WriteAsync("done\n", ct); return 0; },
                                            null, CancellationToken);
                              }, CancellationToken);

                var session = await client.OpenChannelAsync("session", null, CancellationToken);

                // Fire and forget, exactly as PuTTY would when it does not need the timing.
                await session.SendRequestAsync(WinAdj, false, [], CancellationToken);

                // If that produced a stray reply, this request would consume it and return the wrong
                // answer — a pty-req we then never really got.
                var ptyAccepted = await session.SendRequestAsync("pty-req", true, EncodePtyRequest(), CancellationToken);

                Assert.That(ptyAccepted, Is.True,
                            "the pty-req reply must be the answer to the pty-req, not a stray reply to the ignored request");

                await session.SendRequestAsync("exec", false, EncodeString("after-winadj"), CancellationToken);
                await serving;

            }

        }

        #endregion

        #region (private) encoders

        private static Byte[] EncodeString(String Value)
        {
            var bytes  = Encoding.UTF8.GetBytes(Value);
            var buffer = new Byte[4 + bytes.Length];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, (UInt32) bytes.Length);
            bytes.CopyTo(buffer, 4);
            return buffer;
        }

        // term, columns, rows, width, height, modes — the shape a real client sends.
        private static Byte[] EncodePtyRequest()
        {
            var term   = EncodeString("xterm");
            var buffer = new Byte[term.Length + 16 + 4];
            term.CopyTo(buffer, 0);
            var at = term.Length;
            foreach (var value in new UInt32[] { 80, 24, 0, 0 })
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(at), value);
                at += 4;
            }
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(at), 0);   // empty modes string
            return buffer;
        }

        #endregion

    }

}
