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
                                              client, "hermoduser", [ userKey ], CancellationToken: CancellationToken);
                               }, CancellationToken);

                var result = await serverTask;
                await authTask;

                Assert.That(result.Username, Is.EqualTo("hermoduser"),
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

    }

}
