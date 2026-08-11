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
using System.Net;
using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>M8: the ssh-agent client — listing identities and requesting signatures from a fake agent, and authenticating with an agent-backed key.</summary>
    [TestFixture]
    public class SshAgentTests
    {

        #region (helper) a minimal in-memory ssh-agent that signs with a real key

        private static async Task RunFakeAgentAsync(Stream Stream, ISshHostKey Key, String Comment, CancellationToken CancellationToken)
        {
            try
            {
                while (true)
                {
                    var lengthBytes = new Byte[4];
                    var got = await ReadUpToAsync(Stream, lengthBytes, CancellationToken);
                    if (got == 0) return;
                    var length  = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
                    var message = new Byte[length];
                    await Stream.ReadExactlyAsync(message, CancellationToken);

                    var type = message[0];
                    var body = message.AsMemory(1);

                    if (type == 11)   // REQUEST_IDENTITIES
                    {
                        var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                        w.WriteUInt32(1);
                        w.WriteBinaryString(Key.PublicKeyBlob);
                        w.WriteString(Comment);
                        await SendAsync(Stream, 12, abw.WrittenMemory, CancellationToken);   // IDENTITIES_ANSWER
                    }
                    else if (type == 13)   // SIGN_REQUEST
                    {
                        var reader = new SshPacketReader(body.Span);
                        reader.ReadBinaryString();               // key blob
                        var data  = reader.ReadBinaryString();   // data to sign
                        var sig   = Key.Sign(Key.AlgorithmNames[0], data);

                        var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                        w.WriteBinaryString(sig);
                        await SendAsync(Stream, 14, abw.WrittenMemory, CancellationToken);   // SIGN_RESPONSE
                    }
                    else
                        await SendAsync(Stream, 5, ReadOnlyMemory<Byte>.Empty, CancellationToken);   // FAILURE
                }
            }
            catch { }
        }

        private static async Task SendAsync(Stream Stream, Byte Type, ReadOnlyMemory<Byte> Body, CancellationToken CancellationToken)
        {
            var frame = new Byte[5 + Body.Length];
            BinaryPrimitives.WriteUInt32BigEndian(frame, (UInt32) (1 + Body.Length));
            frame[4] = Type;
            Body.Span.CopyTo(frame.AsSpan(5));
            await Stream.WriteAsync(frame, CancellationToken);
            await Stream.FlushAsync(CancellationToken);
        }

        private static async Task<Int32> ReadUpToAsync(Stream Stream, Byte[] Buffer, CancellationToken CancellationToken)
        {
            var total = 0;
            while (total < Buffer.Length)
            {
                var n = await Stream.ReadAsync(Buffer.AsMemory(total), CancellationToken);
                if (n == 0) return total;
                total += n;
            }
            return total;
        }

        // A loopback TCP stream pair (client, server).
        private static async Task<(Stream Client, Stream Server)> ConnectedStreamsAsync(CancellationToken CancellationToken)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint) listener.LocalEndpoint).Port;
            var clientTask = new TcpClient();
            await clientTask.ConnectAsync(IPAddress.Loopback, port, CancellationToken);
            var server = await listener.AcceptTcpClientAsync(CancellationToken);
            listener.Stop();
            return (clientTask.GetStream(), server.GetStream());
        }

        #endregion


        #region Agent_ListsIdentities_AndSigns

        [Test]
        [CancelAfter(20000)]
        public async Task Agent_ListsIdentities_AndSigns(CancellationToken CancellationToken)
        {

            var key = SshHostKey.GenerateEd25519();
            var (clientStream, serverStream) = await ConnectedStreamsAsync(CancellationToken);
            var agentRun = RunFakeAgentAsync(serverStream, key, "my laptop key", CancellationToken);

            await using var agent = new SshAgentClient(clientStream);

            var identities = await agent.ListIdentitiesAsync(CancellationToken);
            var data       = Encoding.UTF8.GetBytes("please sign this");
            var signature  = await agent.SignAsync(identities[0].PublicKeyBlob, data, CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(identities, Has.Count.EqualTo(1));
                Assert.That(identities[0].Comment,        Is.EqualTo("my laptop key"));
                Assert.That(identities[0].PublicKeyBlob,  Is.EqualTo(key.PublicKeyBlob));
                Assert.That(SshSignature.Verify(key.PublicKeyBlob, data, signature), Is.True, "the agent's signature verifies against the public key");
            });

            _ = agentRun;

        }

        #endregion

        #region Agent_BackedKey_AuthenticatesToOurServer

        [Test]
        [CancelAfter(20000)]
        public async Task Agent_BackedKey_AuthenticatesToOurServer(CancellationToken CancellationToken)
        {

            var key = SshHostKey.GenerateEd25519();

            // Stand up a fake agent holding the key.
            var (agentClientStream, agentServerStream) = await ConnectedStreamsAsync(CancellationToken);
            _ = RunFakeAgentAsync(agentServerStream, key, "agent key", CancellationToken);
            await using var agent = new SshAgentClient(agentClientStream);
            var identities = await agent.ListIdentitiesAsync(CancellationToken);
            var agentKey   = agent.GetKey(identities[0].PublicKeyBlob);

            // Our SSH server authorizes that public key; the client authenticates using the agent-backed key.
            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(key.PublicKeyBlob);

            var server = Task.Run(async () =>
            {
                try
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
                }
                catch { }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var ok = await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", agentKey, CancellationToken: CancellationToken);

            await server;

            Assert.That(ok, Is.True, "public-key auth completes with the private key held in the agent");

        }

        #endregion

    }

}
