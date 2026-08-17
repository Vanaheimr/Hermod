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
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);

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

    }

}
