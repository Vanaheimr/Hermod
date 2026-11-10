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
using System.IO.Pipelines;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Strict KEX on demand: a side that requires it ends a connection whose peer does not signal it in
    /// its first KEXINIT - an OpenSSH older than 9.6, say - before anything is authenticated, with an
    /// <see cref="SshStrictKexRequiredException"/>. A side that does not require it goes on as before,
    /// and two sides that both require it agree on it.
    /// </summary>
    [TestFixture]
    public class StrictKexRequiredTests
    {

        #region (private) OldPeerAsync(Pipe, IsServer, CancellationToken)

        /// <summary>
        /// A peer from before strict KEX: the version exchange, and a KEXINIT without the strict-KEX
        /// marker - a KEXINIT as Hermod sends it on a rekey, where the marker has no place. Then it waits.
        /// </summary>
        private static async Task OldPeerAsync(IDuplexPipe        Pipe,
                                               Boolean            IsServer,
                                               CancellationToken  CancellationToken)
        {

            await SshVersionExchange.WriteAsync(Pipe.Output, SshIdentificationString.Parse("SSH-2.0-OpenSSH_9.3"), CancellationToken);
            await SshVersionExchange.ReadAsync (Pipe.Input,  CancellationToken);

            var kexInit = KexInitMessage.CreateLocal(IsServer, Initial: false);

            Assert.That(kexInit.KexAlgorithms, Has.None.StartsWith("kex-strict-"), "the old peer must not signal strict KEX");

            SshPacketFraming.WritePacket(Pipe.Output, NullTransportCipher.Instance, kexInit.Encode());
            await Pipe.Output.FlushAsync(CancellationToken);

        }

        #endregion


        #region Client_RequiringStrictKex_RefusesAServerWithoutIt

        [Test]
        [CancelAfter(10000)]
        public async Task Client_RequiringStrictKex_RefusesAServerWithoutIt(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            var oldServer = OldPeerAsync(serverPipe, IsServer: true, CancellationToken);

            var refused   = await Assert.ThrowsAsync<SshStrictKexRequiredException>(async () =>
                                await SshTransport.ClientHandshakeAsync(clientPipe,
                                                                        VerifyHostKey:      SshHostKeyVerification.AcceptAnyUnsafe,
                                                                        RequireStrictKex:   true,
                                                                        CancellationToken:  CancellationToken));

            await oldServer;

            Assert.Multiple(() => {
                Assert.That(refused!.WeAreServer, Is.False);
                Assert.That(refused.Message,      Does.Contain("server does not signal strict KEX"));
            });

        }

        #endregion

        #region Server_RequiringStrictKex_RefusesAClientWithoutIt

        [Test]
        [CancelAfter(10000)]
        public async Task Server_RequiringStrictKex_RefusesAClientWithoutIt(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            var oldClient = OldPeerAsync(clientPipe, IsServer: false, CancellationToken);

            var refused   = await Assert.ThrowsAsync<SshStrictKexRequiredException>(async () =>
                                await SshTransport.ServerHandshakeAsync(serverPipe,
                                                                        SshHostKey.GenerateEd25519(),
                                                                        RequireStrictKex:   true,
                                                                        CancellationToken:  CancellationToken));

            await oldClient;

            Assert.Multiple(() => {
                Assert.That(refused!.WeAreServer, Is.True);
                Assert.That(refused.Message,      Does.Contain("client does not signal strict KEX"));
            });

        }

        #endregion

        #region Server_NotRequiringStrictKex_GoesOnWithoutIt

        /// <summary>
        /// Without the requirement, the old client's KEXINIT is taken, and the server goes on to wait for
        /// the key exchange's next message - it has not refused anybody.
        /// </summary>
        [Test]
        [CancelAfter(10000)]
        public async Task Server_NotRequiringStrictKex_GoesOnWithoutIt(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            var oldClient = OldPeerAsync(clientPipe, IsServer: false, CancellationToken);

            using var shortly = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            shortly.CancelAfter(TimeSpan.FromMilliseconds(1000));

            var waiting = await Assert.CatchAsync(async () =>
                              await SshTransport.ServerHandshakeAsync(serverPipe,
                                                                      SshHostKey.GenerateEd25519(),
                                                                      CancellationToken: shortly.Token));

            await oldClient;

            Assert.That(waiting, Is.InstanceOf<OperationCanceledException>(), "the server should still have been waiting for the old client");

        }

        #endregion

        #region BothRequiringStrictKex_AgreeOnIt

        [Test]
        [CancelAfter(10000)]
        public async Task BothRequiringStrictKex_AgreeOnIt(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            var server = Task.Run(async () => await SshTransport.ServerHandshakeAsync(serverPipe,
                                                                                    SshHostKey.GenerateEd25519(),
                                                                                    RequireStrictKex:   true,
                                                                                    CancellationToken:  CancellationToken), CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe,
                                                                       VerifyHostKey:      SshHostKeyVerification.AcceptAnyUnsafe,
                                                                       RequireStrictKex:   true,
                                                                       CancellationToken:  CancellationToken);
            using var served = await server;

            Assert.Multiple(() => {
                Assert.That(client.Algorithms.StrictKex, Is.True);
                Assert.That(served.Algorithms.StrictKex, Is.True);
            });

        }

        #endregion

    }

}
