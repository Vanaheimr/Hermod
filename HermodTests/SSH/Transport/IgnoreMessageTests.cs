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

using System.IO.Pipelines;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// SSH_MSG_IGNORE, SSH_MSG_DEBUG and SSH_MSG_UNIMPLEMENTED — the three messages RFC 4253 §11 allows a
    /// peer to send <b>at any time</b> and requires the receiver to skip.
    ///
    /// <para>
    /// This is a regression suite for a real interop defect: AsyncSSH pads authentication with
    /// SSH_MSG_IGNORE to blunt traffic analysis, and our authentication loop treated the first one as a
    /// protocol violation — so every AsyncSSH login died. The messages are now consumed centrally in
    /// <c>SshTransport.ReceivePacketAsync</c>, which is what these tests pin down, since the interop test
    /// that found it only runs where WSL and the Python peers are provisioned.
    /// </para>
    ///
    /// <para>
    /// The second half pins the deliberate exception: while a key exchange is running, strict KEX
    /// (the Terrapin countermeasure, CVE-2023-48795) requires that these same messages are <i>refused</i>,
    /// because tolerating them is exactly what let the attack shift the sequence numbers.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    public class IgnoreMessageTests
    {

        #region (private) helpers

        private static Byte[] Message(SshMessageNumber MessageNumber, params Byte[] Rest)
            => [ (Byte) MessageNumber, .. Rest ];

        /// <summary>
        /// A connected, fully handshaked transport pair over an in-memory pipe.
        /// </summary>
        private static async Task<(SshTransport Client, SshTransport Server)> ConnectedPairAsync(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var clientTask = SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            return (await clientTask, await serverTask);

        }

        #endregion


        #region ReceivePacket_SkipsIgnoreDebugAndUnimplemented

        /// <summary>
        /// The defect itself: a peer interleaving the always-legal housekeeping messages with real traffic
        /// must be understood, not disconnected. Each is followed by a real message that has to come back
        /// intact — and in order, proving nothing was consumed twice or lost.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task ReceivePacket_SkipsIgnoreDebugAndUnimplemented(CancellationToken CancellationToken)
        {

            var (client, server) = await ConnectedPairAsync(CancellationToken);

            using (client)
            using (server)
            {

                // SSH_MSG_IGNORE carries an arbitrary string, DEBUG a flag + message + language tag;
                // the payloads are deliberately non-empty so a naive "skip one byte" would not do.
                await client.SendPacketAsync(Message(SshMessageNumber.Ignore,        1, 2, 3, 4), CancellationToken);
                await client.SendPacketAsync(Message(SshMessageNumber.Debug,         0, 0, 0, 0), CancellationToken);
                await client.SendPacketAsync(Message(SshMessageNumber.Unimplemented, 0, 0, 0, 7), CancellationToken);
                await client.SendPacketAsync(Message(SshMessageNumber.ServiceRequest, 42),        CancellationToken);
                await client.SendPacketAsync(Message(SshMessageNumber.Ignore,        9),          CancellationToken);
                await client.SendPacketAsync(Message(SshMessageNumber.ServiceAccept,  43),        CancellationToken);

                var first  = await server.ReceivePacketAsync(CancellationToken);
                var second = await server.ReceivePacketAsync(CancellationToken);

                Assert.Multiple(() => {

                    Assert.That(first,  Is.EqualTo(Message(SshMessageNumber.ServiceRequest, 42)),
                                "the three housekeeping messages before it must have been skipped");

                    Assert.That(second, Is.EqualTo(Message(SshMessageNumber.ServiceAccept, 43)),
                                "skipping must not lose or duplicate the packets around it");

                });

            }

        }

        #endregion

        #region Authentication_SurvivesIgnorePadding

        /// <summary>
        /// The defect in the shape it actually appeared: SSH_MSG_IGNORE arriving <i>during authentication</i>,
        /// which is where AsyncSSH puts it. Both the service-request wait and the request loop must tolerate
        /// it, so this drives the real <see cref="UserAuthentication"/> server loop rather than a bare read.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task Authentication_SurvivesIgnorePadding(CancellationToken CancellationToken)
        {

            var (client, server) = await ConnectedPairAsync(CancellationToken);
            var userKey          = SshHostKey.GenerateEd25519();

            using (client)
            using (server)
            {

                var serverTask = UserAuthentication.ServerAuthenticateAsync(
                                     server,
                                     SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                                     CancellationToken: CancellationToken);

                // Pad exactly where AsyncSSH does: before the service request, and again before the
                // authentication request.
                await client.SendPacketAsync(Message(SshMessageNumber.Ignore, 1, 1, 1), CancellationToken);

                var authTask = Task.Run(async () => {
                                   await client.SendPacketAsync(Message(SshMessageNumber.Ignore, 2, 2), CancellationToken);
                                   return await UserAuthentication.ClientAuthenticateAsync(
                                              client, User_Id.Parse("hermoduser"), [ userKey ], CancellationToken: CancellationToken);
                               }, CancellationToken);

                var result = await serverTask;
                await authTask;

                Assert.That(result.Username, Is.EqualTo(User_Id.Parse("hermoduser")),
                            "authentication must complete despite the SSH_MSG_IGNORE padding around it");

            }

        }

        #endregion

        #region StrictKex_RefusesIgnoreDuringKeyExchange

        /// <summary>
        /// The Terrapin countermeasure, and the reason the skipping above is scoped rather than blanket:
        /// with strict KEX in force an SSH_MSG_IGNORE <i>inside</i> a key exchange must be fatal. The
        /// attack works by inserting exactly such an ignorable packet to shift the sequence numbers, so
        /// tolerating it here would quietly undo the defence.
        /// </summary>
        [Test]
        [CancelAfter(30000)]
        public async Task StrictKex_RefusesIgnoreDuringKeyExchange(CancellationToken CancellationToken)
        {

            var (client, server) = await ConnectedPairAsync(CancellationToken);

            using (client)
            using (server)
            {

                Assert.That(client.Algorithms.StrictKex, Is.True, "the loopback pair must negotiate strict KEX");

                // Start a rekey on the server, then send an SSH_MSG_IGNORE instead of the KEXINIT it waits
                // for: the exchange is in flight, so it must be refused rather than skipped.
                var rekeyTask = server.RekeyAsync(CancellationToken);

                // The server's post-handshake EXT_INFO is still unread, so drain up to the KEXINIT.
                Byte[] peerKexInit;
                do { peerKexInit = await client.ReceivePacketAsync(CancellationToken); }
                while (peerKexInit[0] != (Byte) SshMessageNumber.KexInit);

                await client.SendPacketAsync(Message(SshMessageNumber.Ignore, 5, 5, 5), CancellationToken);

                Assert.That(async () => await rekeyTask,
                            Throws.TypeOf<SshWireException>().With.Message.Contains("Strict KEX"),
                            "an ignorable packet inside a key exchange is what Terrapin exploits");

            }

        }

        #endregion

        #region (private) raw initial-handshake peer for Terrapin / strict-KEX tests

        public enum InjectAt { None, BeforeKexInit, AfterKexInit, BeforeNewKeys, Everywhere }

        private static async ValueTask WriteCleartextAsync(PipeWriter Output, Byte[] Payload, CancellationToken CancellationToken)
        {
            SshPacketFraming.WritePacket(Output, NullTransportCipher.Instance, Payload);
            await Output.FlushAsync(CancellationToken);
        }

        private static Byte[] HousekeepingPacket(SshMessageNumber MessageNumber)
            => MessageNumber switch {
                   SshMessageNumber.Ignore        => [ (Byte) SshMessageNumber.Ignore,        0, 0, 0, 4, 1, 2, 3, 4 ],
                   SshMessageNumber.Debug         => [ (Byte) SshMessageNumber.Debug,         0, 0, 0, 0, 0, 0, 0, 0, 0 ],
                   SshMessageNumber.Unimplemented => [ (Byte) SshMessageNumber.Unimplemented, 0, 0, 0, 7 ],
                   _                              => throw new ArgumentOutOfRangeException(nameof(MessageNumber))
               };

        /// <summary>
        /// Drives the peer side of an initial curve25519-sha256 + ssh-ed25519 handshake directly on the
        /// wire, injecting a housekeeping packet (IGNORE / DEBUG / UNIMPLEMENTED) at the chosen stage.
        /// Without strict-KEX enforcement during the initial handshake, the entire handshake succeeds.
        /// </summary>
        private static async Task RunRawPeerHandshakeAsync(IDuplexPipe       PeerPipe,
                                                           Boolean           PeerIsServer,
                                                           Boolean           OfferStrictKex,
                                                           InjectAt          Inject,
                                                           SshMessageNumber  InjectedMessage,
                                                           CancellationToken CancellationToken)
        {

            var peerId  = SshIdentificationString.Default;
            await SshVersionExchange.WriteAsync(PeerPipe.Output, peerId, CancellationToken);
            var ourId   = await SshVersionExchange.ReadAsync(PeerPipe.Input, CancellationToken);

            var (vC, vS) = PeerIsServer
                               ? (ourId.WireBytes, peerId.ToWireBytes())
                               : (peerId.ToWireBytes(), ourId.WireBytes);

            var packet = HousekeepingPacket(InjectedMessage);

            if (Inject is InjectAt.BeforeKexInit or InjectAt.Everywhere)
                await WriteCleartextAsync(PeerPipe.Output, packet, CancellationToken);

            var hostKey  = PeerIsServer ? Ed25519KeyPair.Generate() : null;
            var peerInit = KexInitMessage.CreateLocal(IsServer:          PeerIsServer,
                                                      KeyExchanges:      [ SshAlgorithmNames.Kex.Curve25519Sha256 ],
                                                      HostKeyAlgorithms: PeerIsServer ? [.. hostKey!.AlgorithmNames] : [ SshAlgorithmNames.HostKey.Ed25519 ],
                                                      Initial:           OfferStrictKex);

            var iPeer = peerInit.Encode();
            await WriteCleartextAsync(PeerPipe.Output, iPeer, CancellationToken);

            var iOur = await SshPacketFraming.ReadPacketAsync(PeerPipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken);

            var (iC, iS) = PeerIsServer ? (iOur, iPeer) : (iPeer, iOur);

            if (Inject is InjectAt.AfterKexInit or InjectAt.Everywhere)
                await WriteCleartextAsync(PeerPipe.Output, packet, CancellationToken);

            using var kex = SshKeyExchange.Create(SshAlgorithmNames.Kex.Curve25519Sha256);

            if (PeerIsServer)
            {
                var initPayload     = await SshPacketFraming.ReadPacketAsync(PeerPipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken);
                var qC              = SshKexCore.ParseEcdhInit(initPayload);
                var (qS, rawSecret) = kex.ServerRespond(qC);
                var kEncoded        = kex.EncodeSharedSecret(rawSecret);
                var h               = ExchangeHash.Compute(kex.HashAlgorithm, vC, vS, iC, iS, hostKey!.PublicKeyBlob, qC, qS, kEncoded);
                var signature       = hostKey.Sign(SshAlgorithmNames.HostKey.Ed25519, h);
                await WriteCleartextAsync(PeerPipe.Output, SshKexCore.BuildEcdhReply(hostKey.PublicKeyBlob, qS, signature), CancellationToken);
            }
            else
            {
                var qC = kex.StartClient();
                await WriteCleartextAsync(PeerPipe.Output, SshKexCore.BuildEcdhInit(qC), CancellationToken);
                _ = await SshPacketFraming.ReadPacketAsync(PeerPipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken);
            }

            if (Inject is InjectAt.BeforeNewKeys or InjectAt.Everywhere)
                await WriteCleartextAsync(PeerPipe.Output, packet, CancellationToken);

            await WriteCleartextAsync(PeerPipe.Output, [ (Byte) SshMessageNumber.NewKeys ], CancellationToken);
            _ = await SshPacketFraming.ReadPacketAsync(PeerPipe.Input, NullTransportCipher.Instance, CancellationToken: CancellationToken);

        }

        private static ValueTask<SshTransport> RunOurHandshakeAsync(IDuplexPipe        OurPipe,
                                                                    Boolean            WeAreServer,
                                                                    Boolean            RequireStrictKex,
                                                                    CancellationToken  CancellationToken)
            => WeAreServer
                   ? SshTransport.ServerHandshakeAsync(OurPipe,
                                                       Ed25519KeyPair.Generate(),
                                                       KeyExchanges:      [ SshAlgorithmNames.Kex.Curve25519Sha256 ],
                                                       RequireStrictKex:  RequireStrictKex,
                                                       CancellationToken: CancellationToken)
                   : SshTransport.ClientHandshakeAsync(OurPipe,
                                                       VerifyHostKey:     SshHostKeyVerification.AcceptAnyUnsafe,
                                                       KeyExchanges:      [ SshAlgorithmNames.Kex.Curve25519Sha256 ],
                                                       HostKeyAlgorithms: [ SshAlgorithmNames.HostKey.Ed25519 ],
                                                       RequireStrictKex:  RequireStrictKex,
                                                       CancellationToken: CancellationToken);

        #endregion

        #region StrictKex_InitialHandshake_RefusesHousekeepingMessages

        /// <summary>
        /// Terrapin (CVE-2023-48795) exploits injecting SSH_MSG_IGNORE / DEBUG / UNIMPLEMENTED into the
        /// <i>initial</i> key exchange (before KEXINIT, between KEXINIT and ECDH, or before NEWKEYS).
        /// Strict KEX must reject every one of those on both client and server — before NEWKEYS installs
        /// <c>Algorithms</c>.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task StrictKex_InitialHandshake_RefusesHousekeepingMessages(
            [Values]                                                                             Boolean           WeAreServer,
            [Values(InjectAt.BeforeKexInit, InjectAt.AfterKexInit, InjectAt.BeforeNewKeys)]      InjectAt          Stage,
            [Values(SshMessageNumber.Ignore, SshMessageNumber.Debug, SshMessageNumber.Unimplemented)] SshMessageNumber MessageNumber,
            CancellationToken CancellationToken)
        {

            var (ourPipe, peerPipe) = DuplexPipe.CreateConnectedPair();

            using var peerCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            var peerTask = Task.Run(async () => {
                               try   { await RunRawPeerHandshakeAsync(peerPipe, !WeAreServer, OfferStrictKex: true, Stage, MessageNumber, peerCts.Token); }
                               catch { /* expected once our side aborts the handshake */ }
                           }, CancellationToken);

            var exception = await Assert.ThrowsAsync<SshWireException>(async () => {
                                using var _ = await RunOurHandshakeAsync(ourPipe, WeAreServer, RequireStrictKex: false, CancellationToken);
                            });

            await peerCts.CancelAsync();
            await peerTask;

            Assert.That(exception!.Message, Does.Contain("Strict KEX"),
                        $"Strict KEX must reject {MessageNumber} at {Stage} during the initial handshake.");

        }

        #endregion

        #region RequireStrictKex_RefusesIgnoreBeforeKexInitImmediately

        /// <summary>
        /// When strict KEX is required, an SSH_MSG_IGNORE as packet 0 is already a violation and must be
        /// refused immediately rather than waiting for a KEXINIT that may never arrive.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task RequireStrictKex_RefusesIgnoreBeforeKexInitImmediately([Values] Boolean WeAreServer,
                                                                                 CancellationToken CancellationToken)
        {

            var (ourPipe, peerPipe) = DuplexPipe.CreateConnectedPair();

            var peerTask = Task.Run(async () => {
                               await SshVersionExchange.WriteAsync(peerPipe.Output, SshIdentificationString.Default, CancellationToken);
                               await SshVersionExchange.ReadAsync (peerPipe.Input,  CancellationToken);
                               await WriteCleartextAsync(peerPipe.Output, HousekeepingPacket(SshMessageNumber.Ignore), CancellationToken);
                           }, CancellationToken);

            var exception = await Assert.ThrowsAsync<SshWireException>(async () => {
                                using var _ = await RunOurHandshakeAsync(ourPipe, WeAreServer, RequireStrictKex: true, CancellationToken);
                            });

            await peerTask;

            Assert.That(exception!.Message, Does.Contain("Strict KEX"));

        }

        #endregion

        #region NonStrictPeer_InitialHandshake_StillToleratesIgnore

        /// <summary>
        /// Control: a legacy peer that does not signal strict KEX (when <c>RequireStrictKex</c> is false)
        /// is still allowed to send RFC 4253 §11 SSH_MSG_IGNORE packets during the initial handshake.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task NonStrictPeer_InitialHandshake_StillToleratesIgnore([Values] Boolean WeAreServer,
                                                                              CancellationToken CancellationToken)
        {

            var (ourPipe, peerPipe) = DuplexPipe.CreateConnectedPair();

            var peerTask = Task.Run(() => RunRawPeerHandshakeAsync(peerPipe,
                                                                   !WeAreServer,
                                                                   OfferStrictKex:  false,
                                                                   InjectAt.Everywhere,
                                                                   SshMessageNumber.Ignore,
                                                                   CancellationToken),
                                    CancellationToken);

            using var transport = await RunOurHandshakeAsync(ourPipe, WeAreServer, RequireStrictKex: false, CancellationToken);
            await peerTask;

            Assert.That(transport.Algorithms.StrictKex, Is.False);

        }

        #endregion

    }

}
