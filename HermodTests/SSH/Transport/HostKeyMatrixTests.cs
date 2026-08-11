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
    /// M2 host-key breadth: the full handshake with each host-key type (ssh-ed25519,
    /// ecdsa-sha2-nistp256/384/521, rsa-sha2-256/512), where our server signs the exchange hash and
    /// our client verifies it.
    /// </summary>
    [TestFixture]
    public class HostKeyMatrixTests
    {

        #region (static) MakeHostKey(Algorithm)

        internal static ISshHostKey MakeHostKey(String Algorithm)
            => Algorithm switch {
                   SshAlgorithmNames.HostKey.Ed25519       => SshHostKey.GenerateEd25519(),
                   SshAlgorithmNames.HostKey.EcdsaNistP256 or
                   SshAlgorithmNames.HostKey.EcdsaNistP384 or
                   SshAlgorithmNames.HostKey.EcdsaNistP521 => SshHostKey.GenerateEcdsa(Algorithm),
                   SshAlgorithmNames.HostKey.RsaSha2_256   or
                   SshAlgorithmNames.HostKey.RsaSha2_512   => SshHostKey.GenerateRsa(2048),
                   _                                       => throw new ArgumentException($"Unknown host key algorithm '{Algorithm}'.")
               };

        #endregion


        [TestCase(SshAlgorithmNames.HostKey.Ed25519)]
        [TestCase(SshAlgorithmNames.HostKey.EcdsaNistP256)]
        [TestCase(SshAlgorithmNames.HostKey.EcdsaNistP384)]
        [TestCase(SshAlgorithmNames.HostKey.EcdsaNistP521)]
        [TestCase(SshAlgorithmNames.HostKey.RsaSha2_512)]
        [TestCase(SshAlgorithmNames.HostKey.RsaSha2_256)]
        [CancelAfter(15000)]
        public async Task Handshake_WithHostKey(String HostKeyAlgorithm, CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = MakeHostKey(HostKeyAlgorithm);

            // Force the client to accept exactly the algorithm under test, so the negotiation is deterministic.
            String[] hostKeyAlgs = [ HostKeyAlgorithm ];

            var clientTask = SshHandshake.ClientHandshakeAsync(clientPipe, HostKeyAlgorithms: hostKeyAlgs, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var serverTask = SshHandshake.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);

            using var client = await clientTask;   // the client only completes if it verified the host-key signature
            using var server = await serverTask;

            Assert.Multiple(() => {
                Assert.That(client.Algorithms.HostKey, Is.EqualTo(HostKeyAlgorithm));
                Assert.That(client.SessionId,          Is.EqualTo(server.SessionId));
                // The client received exactly this server's public-key blob.
                Assert.That(client.ServerHostKey,      Is.EqualTo(hostKey.PublicKeyBlob));
            });

        }


        [Test]
        [CancelAfter(15000)]
        public void Handshake_WrongHostKeyAlgorithm_NoCommonAlgorithm(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();

            // Server has an Ed25519 key; the client accepts only RSA — negotiation must fail.
            var clientTask = SshHandshake.ClientHandshakeAsync(clientPipe, HostKeyAlgorithms: [ SshAlgorithmNames.HostKey.RsaSha2_512 ], VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            _ = SshHandshake.ServerHandshakeAsync(serverPipe, SshHostKey.GenerateEd25519(), CancellationToken: CancellationToken);

            Assert.That(async () => await clientTask, Throws.TypeOf<SshWireException>());

        }

    }

}
