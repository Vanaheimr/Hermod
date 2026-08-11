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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// End-to-end loopback tests for the M1 transport handshake: our client against our server over an
    /// in-memory duplex pipe, with no networking. Proves that curve25519-sha256 + ssh-ed25519 +
    /// aes256-gcm interoperate and that both sides derive identical keys.
    /// </summary>
    [TestFixture]
    public class HandshakeLoopbackTests
    {

        #region CompleteHandshake_YieldsMatchingSessionAndWorkingCiphers

        [Test]
        [CancelAfter(10000)]
        public async Task CompleteHandshake_YieldsMatchingSessionAndWorkingCiphers(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshHandshake.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshHandshake.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;
            using var server = await serverTask;

            Assert.Multiple(() => {

                // Both sides must agree on the session id (the exchange hash) and the algorithms.
                Assert.That(client.SessionId,                    Is.EqualTo(server.SessionId));
                Assert.That(client.SessionId.Length,             Is.EqualTo(32));   // SHA-256
                Assert.That(client.Algorithms.KeyExchange,       Is.EqualTo(SshAlgorithmNames.Kex.MlKem768X25519Sha256));
                Assert.That(client.Algorithms.HostKey,           Is.EqualTo(SshAlgorithmNames.HostKey.Ed25519));
                Assert.That(client.Algorithms.CipherServerToClient, Is.EqualTo(SshAlgorithmNames.Cipher.ChaCha20Poly1305));
                Assert.That(client.Algorithms.StrictKex,         Is.True);
                Assert.That(client.Algorithms.ExtensionInfo,     Is.True);

                // The client must have received exactly this server's host key.
                Assert.That(SshEd25519.ParsePublicKeyBlob(client.ServerHostKey), Is.EqualTo(hostKey.PublicKey));

            });

            // The derived keys must actually work: an encrypted packet each way, decrypted by the peer.
            var clientToServer = "hello from the client"u8.ToArray();
            SshPacketFraming.WritePacket(clientPipe.Output, client.SendCipher, clientToServer);
            await clientPipe.Output.FlushAsync(CancellationToken);
            var receivedByServer = await SshPacketFraming.ReadPacketAsync(serverPipe.Input, server.ReceiveCipher, CancellationToken: CancellationToken);

            var serverToClient = "hello from the server"u8.ToArray();
            SshPacketFraming.WritePacket(serverPipe.Output, server.SendCipher, serverToClient);
            await serverPipe.Output.FlushAsync(CancellationToken);
            var receivedByClient = await SshPacketFraming.ReadPacketAsync(clientPipe.Input, client.ReceiveCipher, CancellationToken: CancellationToken);

            Assert.Multiple(() => {
                Assert.That(receivedByServer, Is.EqualTo(clientToServer));
                Assert.That(receivedByClient, Is.EqualTo(serverToClient));
            });

        }

        #endregion

        #region HostKeyPolicy_Rejection_FailsHandshake

        [Test]
        [CancelAfter(10000)]
        public void HostKeyPolicy_Rejection_FailsHandshake(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            // The client rejects every host key.
            var clientTask = SshHandshake.ClientHandshakeAsync(clientPipe, VerifyHostKey: _ => false, CancellationToken: CancellationToken);
            _ = SshHandshake.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            Assert.That(async () => await clientTask, Throws.TypeOf<SshWireException>());

        }

        #endregion

        #region MultipleHandshakes_ProduceDistinctSessionIds

        [Test]
        [CancelAfter(10000)]
        public async Task MultipleHandshakes_ProduceDistinctSessionIds(CancellationToken CancellationToken)
        {

            var hostKey = Ed25519KeyPair.Generate();

            async Task<Byte[]> HandshakeOnce()
            {
                var (c, s) = DuplexPipe.CreateConnectedPair();
                var ct = SshHandshake.ClientHandshakeAsync(c, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                using var server = await SshHandshake.ServerHandshakeAsync(s, hostKey, CancellationToken: CancellationToken);
                using var client = await ct;
                return client.SessionId;
            }

            var first   = await HandshakeOnce();
            var second  = await HandshakeOnce();

            // Fresh ephemeral keys each time => different exchange hash / session id.
            Assert.That(second, Is.Not.EqualTo(first));

        }

        #endregion

    }

}
