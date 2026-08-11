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

using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M2 rekeying (key re-exchange, RFC 4253 §9): a live <see cref="SshTransport"/> re-runs the key
    /// exchange over the already-encrypted channel, installing fresh keys while keeping the original
    /// session id — and traffic keeps flowing across the switch.
    /// </summary>
    [TestFixture]
    public class RekeyTests
    {

        #region (static) HandshakeAsync(Ciphers, CancellationToken)

        private static async Task<(SshTransport Client, SshTransport Server)> HandshakeAsync(String[]?          Ciphers,
                                                                                             CancellationToken  CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, Ciphers: Ciphers, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, Ciphers: Ciphers, CancellationToken: CancellationToken);

            var client = await clientTask;
            var server = await serverTask;

            // The server sends EXT_INFO (server-sig-algs) right after the initial NEWKEYS; consume it on
            // the client so the following traffic assertions see plain data packets.
            if (client.Algorithms.ExtensionInfo)
            {
                var extInfo = await client.ReceivePacketAsync(CancellationToken);
                Assert.That(client.TryHandleExtInfo(extInfo), Is.True, "The first server packet must be EXT_INFO.");
            }

            return (client, server);

        }

        #endregion

        #region (static) AssertTrafficFlowsBothWays(Client, Server, CancellationToken)

        /// <summary>
        /// Application traffic of the given size.
        ///
        /// <para>
        /// The first byte of a packet payload <i>is</i> its message number, so it has to be a real one.
        /// A wholly random payload used to be fine, but the transport now skips SSH_MSG_IGNORE, DEBUG and
        /// UNIMPLEMENTED as RFC 4253 §11 requires — and a random first byte lands on one of those about
        /// once in eighty packets, whereupon the read waits forever for traffic that was legitimately
        /// dropped. That flake cost half an hour; the payload is now a plausible packet.
        /// </para>
        /// </summary>
        private static Byte[] TrafficPayload(Int32 Length)
            => [ (Byte) SshMessageNumber.ChannelData, .. RandomNumberGenerator.GetBytes(Length - 1) ];


        private static async Task AssertTrafficFlowsBothWays(SshTransport Client, SshTransport Server, CancellationToken CancellationToken)
        {

            var clientToServer = TrafficPayload(300);
            await Client.SendPacketAsync(clientToServer, CancellationToken);
            var gotByServer = await Server.ReceivePacketAsync(CancellationToken);

            var serverToClient = TrafficPayload(280);
            await Server.SendPacketAsync(serverToClient, CancellationToken);
            var gotByClient = await Client.ReceivePacketAsync(CancellationToken);

            Assert.Multiple(() => {
                Assert.That(gotByServer, Is.EqualTo(clientToServer));
                Assert.That(gotByClient, Is.EqualTo(serverToClient));
            });

        }

        #endregion


        #region Rekey_KeepsSessionId_RotatesKeys_AndTrafficKeepsFlowing

        [Test]
        [CancelAfter(15000)]
        public async Task Rekey_KeepsSessionId_RotatesKeys_AndTrafficKeepsFlowing(CancellationToken CancellationToken)
        {

            var (c, s) = await HandshakeAsync(null, CancellationToken);

            using var _c = c;
            using var _s = s;

            var sessionIdBefore     = (Byte[]) c.SessionId.Clone();
            var exchangeHashBefore  = (Byte[]) c.ExchangeHash.Clone();

            // Traffic works before the rekey.
            await AssertTrafficFlowsBothWays(c, s, CancellationToken);

            // Both sides initiate simultaneously — the standard symmetric rekey.
            await Task.WhenAll(c.RekeyAsync(CancellationToken).AsTask(),
                               s.RekeyAsync(CancellationToken).AsTask());

            Assert.Multiple(() => {
                Assert.That(c.KeyExchangeCount, Is.EqualTo(2));
                Assert.That(s.KeyExchangeCount, Is.EqualTo(2));
                // The session id is fixed by the first exchange and never changes.
                Assert.That(c.SessionId, Is.EqualTo(sessionIdBefore));
                Assert.That(s.SessionId, Is.EqualTo(sessionIdBefore));
                // A fresh exchange hash proves new ephemeral keys were used.
                Assert.That(c.ExchangeHash, Is.Not.EqualTo(exchangeHashBefore));
                Assert.That(c.ExchangeHash, Is.EqualTo(s.ExchangeHash));
            });

            // Traffic works after the rekey — proving both sides installed matching fresh keys.
            await AssertTrafficFlowsBothWays(c, s, CancellationToken);

        }

        #endregion

        #region Rekey_AcrossCiphers_TrafficKeepsFlowing

        [Test]
        [CancelAfter(15000)]
        [TestCase(SshAlgorithmNames.Cipher.ChaCha20Poly1305)]   // encrypted length, seqnr = nonce
        [TestCase(SshAlgorithmNames.Cipher.Aes256Gcm)]          // AEAD, invocation counter
        [TestCase(SshAlgorithmNames.Cipher.Aes256Ctr)]          // CTR + encrypt-then-MAC (seqnr in the MAC)
        public async Task Rekey_AcrossCiphers_TrafficKeepsFlowing(String Cipher, CancellationToken CancellationToken)
        {

            var (c, s) = await HandshakeAsync([ Cipher ], CancellationToken);

            using var _c = c;
            using var _s = s;

            Assert.That(c.Algorithms.CipherClientToServer, Is.EqualTo(Cipher));

            await AssertTrafficFlowsBothWays(c, s, CancellationToken);

            await Task.WhenAll(c.RekeyAsync(CancellationToken).AsTask(),
                               s.RekeyAsync(CancellationToken).AsTask());

            await AssertTrafficFlowsBothWays(c, s, CancellationToken);

        }

        #endregion

        #region MultipleRekeys_RemainStable

        [Test]
        [CancelAfter(20000)]
        public async Task MultipleRekeys_RemainStable(CancellationToken CancellationToken)
        {

            var (c, s) = await HandshakeAsync(null, CancellationToken);

            using var _c = c;
            using var _s = s;

            var sessionId = (Byte[]) c.SessionId.Clone();

            for (var round = 0; round < 4; round++)
            {

                await AssertTrafficFlowsBothWays(c, s, CancellationToken);

                await Task.WhenAll(c.RekeyAsync(CancellationToken).AsTask(),
                                   s.RekeyAsync(CancellationToken).AsTask());

                Assert.Multiple(() => {
                    Assert.That(c.SessionId,        Is.EqualTo(sessionId));
                    Assert.That(c.ExchangeHash,     Is.EqualTo(s.ExchangeHash));
                    Assert.That(c.KeyExchangeCount, Is.EqualTo(round + 2));
                });

            }

            await AssertTrafficFlowsBothWays(c, s, CancellationToken);

        }

        #endregion

        #region PeerInitiatedRekey_ViaRespondToRekey

        [Test]
        [CancelAfter(15000)]
        public async Task PeerInitiatedRekey_ViaRespondToRekey(CancellationToken CancellationToken)
        {

            var (c, s) = await HandshakeAsync(null, CancellationToken);

            using var _c = c;
            using var _s = s;

            // The client initiates; the server discovers the rekey by reading a KEXINIT off the wire and
            // then hands it to RespondToRekeyAsync (the receive-loop path a connection layer would use).
            var clientRekey = c.RekeyAsync(CancellationToken).AsTask();

            var firstFromClient = await s.ReceivePacketAsync(CancellationToken);
            Assert.That(firstFromClient[0], Is.EqualTo((Byte) SshMessageNumber.KexInit),
                        "A peer-initiated rekey must arrive as SSH_MSG_KEXINIT.");

            await s.RespondToRekeyAsync(firstFromClient, CancellationToken);
            await clientRekey;

            Assert.Multiple(() => {
                Assert.That(c.KeyExchangeCount, Is.EqualTo(2));
                Assert.That(s.KeyExchangeCount, Is.EqualTo(2));
                Assert.That(c.ExchangeHash,     Is.EqualTo(s.ExchangeHash));
            });

            await AssertTrafficFlowsBothWays(c, s, CancellationToken);

        }

        #endregion

    }

}
