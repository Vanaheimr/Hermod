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
    /// From its KEXINIT to its NEWKEYS a peer may send nothing but the key exchange (RFC 4253 §7.1), and
    /// OpenSSH 10.4 disconnects a peer that sends anything else during a re-exchange. So do we - with a
    /// message that names what came, where the exchange alone would name the packet it expected.
    /// </summary>
    /// <remarks>
    /// Our own transport cannot break the rule: it holds every send from its KEXINIT to its NEWKEYS.
    /// The misbehaving peer therefore writes its KEXINIT through <see cref="SshTransport.SendPacketAsync"/>
    /// as if it were any packet, so that its transport never learns an exchange began, and what it sends
    /// next goes straight out after it.
    /// </remarks>
    [TestFixture]
    public class RekeyStrictnessTests
    {

        #region (static) HandshakeAsync(CancellationToken)

        private static async Task<(SshTransport Client, SshTransport Server)> HandshakeAsync(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            var client = await clientTask;
            var server = await serverTask;

            if (client.Algorithms.ExtensionInfo)
            {
                var extInfo = await client.ReceivePacketAsync(CancellationToken);
                Assert.That(client.TryHandleExtInfo(extInfo), Is.True, "The first server packet must be EXT_INFO.");
            }

            return (client, server);

        }

        #endregion

        #region (static) Packet(MessageNumber)

        private static Byte[] Packet(SshMessageNumber MessageNumber)
            => [ (Byte) MessageNumber, .. RandomNumberGenerator.GetBytes(31) ];

        #endregion

        #region (static) RekeyOutOfTurnAsync(Peer, KexInit, Then, CancellationToken)

        /// <summary>
        /// The misbehaving peer: its KEXINIT, and right behind it what it must not send.
        /// </summary>
        private static async Task RekeyOutOfTurnAsync(SshTransport       Peer,
                                                      KexInitMessage     KexInit,
                                                      Byte[]             Then,
                                                      CancellationToken  CancellationToken)
        {
            await Peer.SendPacketAsync(KexInit.Encode(), CancellationToken);
            await Peer.SendPacketAsync(Then,             CancellationToken);
        }

        #endregion


        #region APacketInsideThePeersKeyExchange_IsRefused

        /// <summary>
        /// Connection traffic, authentication and a service request inside the peer's exchange are
        /// refused, whichever side the peer is - and the refusal names the packet.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task APacketInsideThePeersKeyExchange_IsRefused([Values] Boolean PeerIsServer,
                                                                    [Values(SshMessageNumber.ChannelData,
                                                                            SshMessageNumber.GlobalRequest,
                                                                            SshMessageNumber.UserAuthRequest,
                                                                            SshMessageNumber.ServiceRequest)] SshMessageNumber MessageNumber,
                                                                    CancellationToken CancellationToken)
        {

            var (client, server) = await HandshakeAsync(CancellationToken);

            using var _c = client;
            using var _s = server;

            var (peer, we) = PeerIsServer ? (server, client) : (client, server);

            await RekeyOutOfTurnAsync(peer,
                                      KexInitMessage.CreateLocal(PeerIsServer, Initial: false),
                                      Packet(MessageNumber),
                                      CancellationToken);

            var kexInit = await we.ReceivePacketAsync(CancellationToken);
            Assert.That((SshMessageNumber) kexInit[0], Is.EqualTo(SshMessageNumber.KexInit));

            var exception = await Assert.ThrowsAsync<SshWireException>(async () =>
                                await we.RespondToRekeyAsync(kexInit, CancellationToken));

            Assert.That(exception!.Message, Does.Contain($"{MessageNumber} ({(Byte) MessageNumber}) during its key exchange"));

        }

        #endregion

        #region ASecondKexInit_IsRefused

        /// <summary>
        /// RFC 4253 §7.1: "further SSH_MSG_KEXINIT messages MUST NOT be sent".
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task ASecondKexInit_IsRefused(CancellationToken CancellationToken)
        {

            var (client, server) = await HandshakeAsync(CancellationToken);

            using var _c = client;
            using var _s = server;

            await RekeyOutOfTurnAsync(client,
                                      KexInitMessage.CreateLocal(IsServer: false, Initial: false),
                                      KexInitMessage.CreateLocal(IsServer: false, Initial: false).Encode(),
                                      CancellationToken);

            var kexInit = await server.ReceivePacketAsync(CancellationToken);

            var exception = await Assert.ThrowsAsync<SshWireException>(async () =>
                                await server.RespondToRekeyAsync(kexInit, CancellationToken));

            Assert.That(exception!.Message, Does.Contain("KexInit (20) during its key exchange"));

        }

        #endregion

        #region AWrongGuessThatIsNoKeyExchangePacket_IsRefused

        /// <summary>
        /// A wrong guess is read and dropped unseen - so it has to be a key exchange packet, or what the
        /// peer slipped in under that name would silently vanish. NEWKEYS passes the receive check (it is
        /// part of an exchange) and is caught only by this one.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task AWrongGuessThatIsNoKeyExchangePacket_IsRefused(CancellationToken CancellationToken)
        {

            var (client, server) = await HandshakeAsync(CancellationToken);

            using var _c = client;
            using var _s = server;

            // The server lists a method first that the client does not prefer: the client's choice
            // wins, so the guess is wrong and the client must read and discard it.
            var listed  = KexInitMessage.CreateLocal(IsServer:      true,
                                                     KeyExchanges:  [ SshAlgorithmNames.Kex.Curve25519Sha256,
                                                                      SshAlgorithmNames.Kex.MlKem768X25519Sha256 ],
                                                     Initial:       false);

            var guessing = new KexInitMessage(listed.Cookie,
                                              listed.KexAlgorithms.Where(name => name != SshAlgorithmNames.Kex.KexGuess2).ToArray(),
                                              listed.ServerHostKeyAlgorithms,
                                              listed.EncryptionClientToServer,
                                              listed.EncryptionServerToClient,
                                              listed.MacClientToServer,
                                              listed.MacServerToClient,
                                              listed.CompressionClientToServer,
                                              listed.CompressionServerToClient,
                                              listed.LanguagesClientToServer,
                                              listed.LanguagesServerToClient,
                                              FirstKexPacketFollows: true);

            await RekeyOutOfTurnAsync(server, guessing, Packet(SshMessageNumber.NewKeys), CancellationToken);

            var kexInit = await client.ReceivePacketAsync(CancellationToken);

            var exception = await Assert.ThrowsAsync<SshWireException>(async () =>
                                await client.RespondToRekeyAsync(kexInit, CancellationToken));

            Assert.That(exception!.Message, Does.Contain("guessed key exchange packet").And.Contain("NewKeys (21)"));

        }

        #endregion

    }

}
